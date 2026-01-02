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
    private DispatcherTimer? _autoSyncTimer;
    private CancellationTokenSource? _syncCts;
    private bool _isSyncing = false;

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

        // Initialize auto sync timer
        _autoSyncTimer = new DispatcherTimer();
        _autoSyncTimer.Interval = TimeSpan.FromMinutes(NumberBox_SyncInterval.Value);
        _autoSyncTimer.Tick += AutoSyncTimer_Tick;

        UpdateLocalTimeDisplay();
    }

    private void UpdateLocalTimeDisplay()
    {
        TextBlock_LocalTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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

    private async void Button_SyncTime_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncing)
        {
            ShowInfoBar("Please wait for current sync to complete", InfoBarSeverity.Warning);
            return;
        }

        if (ComboBox_NtpServer.SelectedItem == null)
        {
            ShowInfoBar("Please select an NTP server", InfoBarSeverity.Warning);
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

        string server = ComboBox_NtpServer.SelectedItem.ToString()!;
        ShowInfoBar($"Syncing with {server}...", InfoBarSeverity.Informational);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_syncCts.Token);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var localTime = await NtpTimeSyncService.SyncSystemTimeAsync(server, cts.Token);
            
            ShowInfoBar($"Sync successful: {localTime:yyyy-MM-dd HH:mm:ss}", InfoBarSeverity.Success);
            UpdateLocalTimeDisplay();
            
            _logger.LogInformation("Time synced successfully with {Server}", server);
        }
        catch (OperationCanceledException)
        {
            ShowInfoBar("Sync operation was cancelled", InfoBarSeverity.Warning);
            _logger.LogWarning("Time sync cancelled for server {Server}", server);
        }
        catch (InvalidOperationException ex)
        {
            ShowInfoBar("Failed to set system time. Please run as administrator.", InfoBarSeverity.Error);
            _logger.LogError(ex, "Failed to set system time");
        }
        catch (Exception ex)
        {
            ShowInfoBar($"Sync failed: {ex.Message}", InfoBarSeverity.Error);
            _logger.LogError(ex, "Time sync failed for server {Server}", server);
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
