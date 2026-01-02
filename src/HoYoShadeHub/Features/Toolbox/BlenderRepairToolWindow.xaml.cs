using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Frameworks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.UI;

namespace HoYoShadeHub.Features.Toolbox;

[INotifyPropertyChanged]
public sealed partial class BlenderRepairToolWindow : WindowEx
{
    private readonly ILogger<BlenderRepairToolWindow> _logger = AppConfig.GetLogger<BlenderRepairToolWindow>();
    private DispatcherTimer? _localTimeTimer;
    private DispatcherTimer? _accurateTimeTimer;
    private DispatcherTimer? _autoSyncTimer;
    private CancellationTokenSource? _syncCts;
    private bool _isSyncing = false;
    private bool _isRefreshingAccurateTime = false;
    
    // 用于准确时间的计时
    private DateTime? _lastNetworkTime;
    private DateTime? _lastNetworkTimeReceived;

    public BlenderRepairToolWindow()
    {
        InitializeComponent();
        InitializeWindow();
        InitializeTimeSyncControls();
    }

    private void InitializeWindow()
    {
        Title = "Blender Repair Tool";
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        
        // Set window size
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
    }

    private void InitializeTimeSyncControls()
    {
        // Initialize HTTP trace endpoints
        foreach (var endpoint in HttpTimeSyncService.TraceEndpoints)
        {
            ComboBox_HttpEndpoint.Items.Add(endpoint);
        }
        ComboBox_HttpEndpoint.SelectedIndex = 0;

        // Initialize NTP server list
        foreach (var server in NtpTimeSyncService.NtpServers)
        {
            ComboBox_NtpServer.Items.Add(server);
        }
        ComboBox_NtpServer.SelectedIndex = 0;

        // Initialize local time display timer
        _localTimeTimer = new DispatcherTimer();
        _localTimeTimer.Interval = TimeSpan.FromSeconds(1);
        _localTimeTimer.Tick += (s, e) => UpdateLocalTimeDisplay();
        _localTimeTimer.Start();

        // Initialize accurate time display timer (updates every second based on last network time)
        _accurateTimeTimer = new DispatcherTimer();
        _accurateTimeTimer.Interval = TimeSpan.FromSeconds(1);
        _accurateTimeTimer.Tick += (s, e) => UpdateAccurateTimeDisplay();
        _accurateTimeTimer.Start();

        // Initialize auto sync timer
        _autoSyncTimer = new DispatcherTimer();
        _autoSyncTimer.Interval = TimeSpan.FromMinutes(NumberBox_SyncInterval.Value);
        _autoSyncTimer.Tick += AutoSyncTimer_Tick;

        UpdateLocalTimeDisplay();
        _ = RefreshAccurateTimeAsync(); // Initial load
    }

    private void UpdateLocalTimeDisplay()
    {
        TextBlock_LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void UpdateAccurateTimeDisplay()
    {
        if (_lastNetworkTime.HasValue && _lastNetworkTimeReceived.HasValue)
        {
            // 计算从上次获取网络时间到现在经过的时间
            var elapsed = DateTime.UtcNow - _lastNetworkTimeReceived.Value;
            var currentAccurateTime = _lastNetworkTime.Value.Add(elapsed);
            
            TextBlock_AccurateTime.Text = currentAccurateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            TextBlock_AccurateTime.Text = "Loading...";
        }
    }

    private async Task RefreshAccurateTimeAsync()
    {
        if (_isRefreshingAccurateTime)
        {
            return;
        }

        _isRefreshingAccurateTime = true;
        Button_RefreshAccurateTime.IsEnabled = false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Use wto.org endpoint for accurate time display
            var networkTime = await HttpTimeSyncService.GetNetworkTimeAsync(
                "https://www.wto.org/cdn-cgi/trace", 
                cts.Token);

            // 记录网络时间和接收时间
            _lastNetworkTime = networkTime;
            _lastNetworkTimeReceived = DateTime.UtcNow;
            
            // 立即更新显示
            UpdateAccurateTimeDisplay();
            
            _logger.LogInformation("Network time fetched successfully: {Time}", networkTime);
        }
        catch (Exception ex)
        {
            TextBlock_AccurateTime.Text = "Failed to get time";
            _lastNetworkTime = null;
            _lastNetworkTimeReceived = null;
            _logger.LogWarning(ex, "Failed to refresh accurate time");
        }
        finally
        {
            _isRefreshingAccurateTime = false;
            Button_RefreshAccurateTime.IsEnabled = true;
        }
    }

    private async void Button_RefreshAccurateTime_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAccurateTimeAsync();
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            double uiScale = Content.XamlRoot?.RasterizationScale ?? User32.GetDpiForWindow(WindowHandle) / 96.0;
            double x = CustomTitleBar.ActualWidth * uiScale;
            SetDragRectangles(new Windows.Graphics.RectInt32((int)x, 0, 0, (int)(48 * uiScale)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set drag rectangles");
        }
    }

    public void ShowWindow(Microsoft.UI.WindowId windowId)
    {
        try
        {
            CenterInScreen(800, 600);
            Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show window");
        }
    }

    private void RadioButtons_SyncMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RadioButtons_SyncMethod.SelectedItem is RadioButton selectedButton)
        {
            string? method = selectedButton.Tag?.ToString();
            
            if (method == "NTP")
            {
                StackPanel_NtpOptions.Visibility = Visibility.Visible;
                StackPanel_HttpOptions.Visibility = Visibility.Collapsed;
            }
            else if (method == "HTTP")
            {
                StackPanel_NtpOptions.Visibility = Visibility.Collapsed;
                StackPanel_HttpOptions.Visibility = Visibility.Visible;
            }
        }
    }

    private async void Button_SyncTime_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncing)
        {
            ShowInfoBar("Please wait for current sync to complete", InfoBarSeverity.Warning);
            return;
        }

        await SyncTimeAsync();
    }

    private async Task SyncTimeAsync()
    {
        _isSyncing = true;
        Button_SyncTime.IsEnabled = false;
        _syncCts?.Cancel();
        _syncCts = new CancellationTokenSource();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_syncCts.Token);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            DateTime localTime;
            string source;

            // Check which sync method is selected
            if (RadioButtons_SyncMethod.SelectedItem is RadioButton selectedButton && 
                selectedButton.Tag?.ToString() == "HTTP")
            {
                // HTTP sync method
                if (ComboBox_HttpEndpoint.SelectedItem == null)
                {
                    ShowInfoBar("Please select a trace endpoint", InfoBarSeverity.Warning);
                    return;
                }

                string endpoint = ComboBox_HttpEndpoint.SelectedItem.ToString()!;
                ShowInfoBar($"Syncing with {endpoint}...", InfoBarSeverity.Informational);

                localTime = await HttpTimeSyncService.SyncSystemTimeAsync(endpoint, cts.Token);
                source = "Cloudflare trace API";
                
                _logger.LogInformation("Time synced successfully with HTTP endpoint {Endpoint}", endpoint);
            }
            else
            {
                // NTP sync method (default)
                if (ComboBox_NtpServer.SelectedItem == null)
                {
                    ShowInfoBar("Please select an NTP server", InfoBarSeverity.Warning);
                    return;
                }

                string server = ComboBox_NtpServer.SelectedItem.ToString()!;
                ShowInfoBar($"Syncing with {server}...", InfoBarSeverity.Informational);

                localTime = await NtpTimeSyncService.SyncSystemTimeAsync(server, cts.Token);
                source = "NTP server";
                
                _logger.LogInformation("Time synced successfully with NTP server {Server}", server);
            }
            
            ShowInfoBar($"Sync successful from {source}: {localTime:yyyy-MM-dd HH:mm:ss}", InfoBarSeverity.Success);
            UpdateLocalTimeDisplay();
            
            // Also refresh accurate time after sync
            _ = RefreshAccurateTimeAsync();
        }
        catch (OperationCanceledException)
        {
            ShowInfoBar("Sync operation was cancelled or timed out", InfoBarSeverity.Warning);
            _logger.LogWarning("Time sync cancelled or timed out");
        }
        catch (InvalidOperationException ex)
        {
            ShowInfoBar("Failed to set system time. Please run as administrator.", InfoBarSeverity.Error);
            _logger.LogError(ex, "Failed to set system time");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Sync failed: {ex.Message}", InfoBarSeverity.Error);
            _logger.LogError(ex, "Time sync failed");
        }
        finally
        {
            _isSyncing = false;
            Button_SyncTime.IsEnabled = true;
        }
    }

    private void CheckBox_AutoSync_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (CheckBox_AutoSync.IsChecked == true)
        {
            _autoSyncTimer!.Interval = TimeSpan.FromMinutes(NumberBox_SyncInterval.Value);
            _autoSyncTimer.Start();
            ShowInfoBar($"Auto sync enabled, every {NumberBox_SyncInterval.Value} minutes", InfoBarSeverity.Success);
        }
        else
        {
            _autoSyncTimer?.Stop();
            ShowInfoBar("Auto sync disabled", InfoBarSeverity.Informational);
        }
    }

    private void NumberBox_SyncInterval_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (CheckBox_AutoSync.IsChecked == true && _autoSyncTimer != null)
        {
            _autoSyncTimer.Interval = TimeSpan.FromMinutes(sender.Value);
            ShowInfoBar($"Sync interval updated to {sender.Value} minutes", InfoBarSeverity.Informational);
        }
    }

    private async void AutoSyncTimer_Tick(object? sender, object e)
    {
        if (!_isSyncing)
        {
            await SyncTimeAsync();
        }
    }

    private void ShowInfoBar(string message, InfoBarSeverity severity)
    {
        InfoBar_TimeSync.Message = message;
        InfoBar_TimeSync.Severity = severity;
        InfoBar_TimeSync.IsOpen = true;
    }

    private void Button_Placeholder_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder for future functionality
        ShowInfoBar("Feature under development...", InfoBarSeverity.Informational);
    }
}
