using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.UI;

namespace HoYoShadeHub.Features.Toolbox;

enum FileDeleteResult
{
    Success,
    NotFound,
    Failed
}

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
        Title = Lang.BlenderRepairTool_WindowTitle;
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        
        // Set window size
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));
        
        // 监听窗口激活事件以刷新配置
        this.Activated += BlenderRepairToolWindow_Activated;
    }

    private void BlenderRepairToolWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // 窗口激活时刷新配置（例如从其他窗口切换回来时）
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            // 可以在这里添加配置刷新逻辑，但为了性能考虑，我们只在按钮点击时检测
            _logger.LogDebug("Window activated, plugin configurations will be checked on button click");
        }
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

    private async void Button_ResetClientTarget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 清除配置缓存，重新从数据库读取最新配置
            AppConfig.ClearCache();
            
            // 使用正确的配置属性名
            string? genshinPath = AppConfig.GenshinBlenderPluginPath;
            string? zzzPath = AppConfig.ZZZBlenderPluginPath;
            
            bool hasGenshin = !string.IsNullOrEmpty(genshinPath) && Directory.Exists(genshinPath);
            bool hasZZZ = !string.IsNullOrEmpty(zzzPath) && Directory.Exists(zzzPath);

            _logger.LogInformation("Plugin path detection: Genshin={GenshinPath} (exists={HasGenshin}), ZZZ={ZzzPath} (exists={HasZZZ})", 
                genshinPath ?? "null", hasGenshin, zzzPath ?? "null", hasZZZ);

            if (!hasGenshin && !hasZZZ)
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_NoPluginPathConfigured);
                return;
            }

            // 创建选择界面
            var dialogContent = new StackPanel { Spacing = 12 };
            
            // 警告信息
            dialogContent.Children.Add(new TextBlock
            {
                Text = Lang.BlenderRepairTool_ResetConfirmMessage,
                TextWrapping = TextWrapping.Wrap
            });

            // 游戏选择标题
            dialogContent.Children.Add(new TextBlock
            {
                Text = Lang.BlenderRepairTool_SelectGamesToRepair,
                Margin = new Thickness(0, 8, 0, 0),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            // 原神复选框
            var genshinCheckBox = new CheckBox
            {
                Content = hasGenshin ? Lang.BlenderRepairTool_GenshinImpact : $"{Lang.BlenderRepairTool_GenshinImpact} ({Lang.BlenderRepairTool_NotConfigured})",
                IsEnabled = hasGenshin,
                Margin = new Thickness(0, 4, 0, 0)
            };

            // 绝区零复选框
            var zzzCheckBox = new CheckBox
            {
                Content = hasZZZ ? Lang.BlenderRepairTool_ZenlessZoneZero : $"{Lang.BlenderRepairTool_ZenlessZoneZero} ({Lang.BlenderRepairTool_NotConfigured})",
                IsEnabled = hasZZZ,
                Margin = new Thickness(0, 4, 0, 0)
            };

            dialogContent.Children.Add(genshinCheckBox);
            dialogContent.Children.Add(zzzCheckBox);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Lang.BlenderRepairTool_ResetConfirmTitle,
                Content = dialogContent,
                PrimaryButtonText = Lang.Common_Cancel,
                SecondaryButtonText = Lang.Common_Continue,
                DefaultButton = ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Secondary)
            {
                return; // 用户取消
            }

            // 检查是否选择了游戏
            bool selectedGenshin = genshinCheckBox.IsChecked == true;
            bool selectedZZZ = zzzCheckBox.IsChecked == true;

            if (!selectedGenshin && !selectedZZZ)
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_SelectAtLeastOneGame);
                return;
            }

            // 执行删除操作
            int successCount = 0;
            int failCount = 0;
            int notFoundCount = 0;

            if (selectedGenshin)
            {
                var deleteResult = await DeletePluginConfigFileInternal(true, genshinPath!);
                if (deleteResult == FileDeleteResult.Success)
                    successCount++;
                else if (deleteResult == FileDeleteResult.NotFound)
                    notFoundCount++;
                else
                    failCount++;
            }

            if (selectedZZZ)
            {
                var deleteResult = await DeletePluginConfigFileInternal(false, zzzPath!);
                if (deleteResult == FileDeleteResult.Success)
                    successCount++;
                else if (deleteResult == FileDeleteResult.NotFound)
                    notFoundCount++;
                else
                    failCount++;
            }

            // 显示结果
            if (successCount > 0 && failCount == 0 && notFoundCount == 0)
            {
                ShowPluginRepairSuccess(string.Format(Lang.BlenderRepairTool_SuccessMessage, successCount));
            }
            else if (notFoundCount > 0 && successCount == 0 && failCount == 0)
            {
                ShowPluginRepairError($"选中的游戏插件中未找到 config 文件。");
            }
            else if (successCount > 0)
            {
                string message = $"成功删除 {successCount} 个文件";
                if (notFoundCount > 0) message += $"，{notFoundCount} 个文件未找到";
                if (failCount > 0) message += $"，{failCount} 个文件删除失败";
                message += "。";
                ShowPluginRepairSuccess(message);
            }
            else
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_AllFailedMessage);
            }
        }
        catch (Exception ex)
        {
            ShowPluginRepairError($"Failed: {ex.Message}");
            _logger.LogError(ex, "Failed to reset client target");
        }
    }

    private async void Button_FixLoginError_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 清除配置缓存，重新从数据库读取最新配置
            AppConfig.ClearCache();
            
            // 使用正确的配置属性名
            string? genshinPath = AppConfig.GenshinBlenderPluginPath;
            string? zzzPath = AppConfig.ZZZBlenderPluginPath;
            
            bool hasGenshin = !string.IsNullOrEmpty(genshinPath) && Directory.Exists(genshinPath);
            bool hasZZZ = !string.IsNullOrEmpty(zzzPath) && Directory.Exists(zzzPath);

            _logger.LogInformation("Plugin path detection: Genshin={GenshinPath} (exists={HasGenshin}), ZZZ={ZzzPath} (exists={HasZZZ})", 
                genshinPath ?? "null", hasGenshin, zzzPath ?? "null", hasZZZ);

            if (!hasGenshin && !hasZZZ)
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_NoPluginPathConfigured);
                return;
            }

            // 创建选择界面
            var dialogContent = new StackPanel { Spacing = 12 };
            
            // 警告信息
            dialogContent.Children.Add(new TextBlock
            {
                Text = Lang.BlenderRepairTool_FixLoginConfirmMessage,
                TextWrapping = TextWrapping.Wrap
            });

            // 游戏选择标题
            dialogContent.Children.Add(new TextBlock
            {
                Text = Lang.BlenderRepairTool_SelectGamesToRepair,
                Margin = new Thickness(0, 8, 0, 0),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            // 原神复选框
            var genshinCheckBox = new CheckBox
            {
                Content = hasGenshin ? Lang.BlenderRepairTool_GenshinCookie : $"{Lang.BlenderRepairTool_GenshinImpact} ({Lang.BlenderRepairTool_NotConfigured})",
                IsEnabled = hasGenshin,
                Margin = new Thickness(0, 4, 0, 0)
            };

            // 绝区零复选框
            var zzzCheckBox = new CheckBox
            {
                Content = hasZZZ ? Lang.BlenderRepairTool_ZZZCookie : $"{Lang.BlenderRepairTool_ZenlessZoneZero} ({Lang.BlenderRepairTool_NotConfigured})",
                IsEnabled = hasZZZ,
                Margin = new Thickness(0, 4, 0, 0)
            };

            dialogContent.Children.Add(genshinCheckBox);
            dialogContent.Children.Add(zzzCheckBox);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = Lang.BlenderRepairTool_FixLoginError,
                Content = dialogContent,
                PrimaryButtonText = Lang.Common_Cancel,
                SecondaryButtonText = Lang.Common_Continue,
                DefaultButton = ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Secondary)
            {
                return; // 用户取消
            }

            // 检查是否选择了游戏
            bool selectedGenshin = genshinCheckBox.IsChecked == true;
            bool selectedZZZ = zzzCheckBox.IsChecked == true;

            if (!selectedGenshin && !selectedZZZ)
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_SelectAtLeastOneGame);
                return;
            }

            // 执行删除操作
            int successCount = 0;
            int failCount = 0;
            int notFoundCount = 0;

            if (selectedGenshin)
            {
                var deleteResult = await DeletePluginCookieFileInternal(true, genshinPath!);
                if (deleteResult == FileDeleteResult.Success)
                    successCount++;
                else if (deleteResult == FileDeleteResult.NotFound)
                    notFoundCount++;
                else
                    failCount++;
            }

            if (selectedZZZ)
            {
                var deleteResult = await DeletePluginCookieFileInternal(false, zzzPath!);
                if (deleteResult == FileDeleteResult.Success)
                    successCount++;
                else if (deleteResult == FileDeleteResult.NotFound)
                    notFoundCount++;
                else
                    failCount++;
            }

            // 显示结果
            if (successCount > 0 && failCount == 0 && notFoundCount == 0)
            {
                ShowPluginRepairSuccess(string.Format(Lang.BlenderRepairTool_CookieSuccessMessage, successCount));
            }
            else if (notFoundCount > 0 && successCount == 0 && failCount == 0)
            {
                ShowPluginRepairError($"选中的游戏插件中未找到 cookies.json 文件。");
            }
            else if (successCount > 0)
            {
                string message = $"成功删除 {successCount} 个文件";
                if (notFoundCount > 0) message += $"，{notFoundCount} 个文件未找到";
                if (failCount > 0) message += $"，{failCount} 个文件删除失败";
                message += "。";
                ShowPluginRepairSuccess(message);
            }
            else
            {
                ShowPluginRepairError(Lang.BlenderRepairTool_AllFailedMessage);
            }
        }
        catch (Exception ex)
        {
            ShowPluginRepairError($"Failed: {ex.Message}");
            _logger.LogError(ex, "Failed to fix login error");
        }
    }

    private async Task<FileDeleteResult> DeletePluginConfigFileInternal(bool isGenshin, string pluginPath)
    {
        string gameName = isGenshin ? "Genshin Impact" : "Zenless Zone Zero";
        string configFile = Path.Combine(pluginPath, "config");
        
        if (!File.Exists(configFile))
        {
            _logger.LogWarning("Config file not found for {Game} at {Path}", gameName, configFile);
            return FileDeleteResult.NotFound;
        }

        try
        {
            File.Delete(configFile);
            _logger.LogInformation("Deleted config file for {Game} at {Path}", gameName, configFile);
            return FileDeleteResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete config file for {Game}", gameName);
            return FileDeleteResult.Failed;
        }
    }

    private async Task<FileDeleteResult> DeletePluginCookieFileInternal(bool isGenshin, string pluginPath)
    {
        string gameName = isGenshin ? "Genshin Impact" : "Zenless Zone Zero";
        string cookieFile = Path.Combine(pluginPath, "cookies.json");
        
        if (!File.Exists(cookieFile))
        {
            _logger.LogWarning("Cookie file (cookies.json) not found for {Game} at {Path}", gameName, cookieFile);
            return FileDeleteResult.NotFound;
        }

        try
        {
            File.Delete(cookieFile);
            _logger.LogInformation("Deleted cookie file cookies.json for {Game} at {Path}", gameName, cookieFile);
            return FileDeleteResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cookie file for {Game}", gameName);
            return FileDeleteResult.Failed;
        }
    }

    private async Task DeletePluginConfigFile(bool isGenshin)
    {
        string gameName = isGenshin ? "Genshin Impact" : "Zenless Zone Zero";
        string? pluginPath = isGenshin ? AppConfig.GenshinBlenderPluginPath : AppConfig.ZZZBlenderPluginPath;
        
        if (string.IsNullOrEmpty(pluginPath) || !Directory.Exists(pluginPath))
        {
            ShowPluginRepairError($"{gameName} plugin path not configured or not found.");
            return;
        }

        string configFile = Path.Combine(pluginPath, "config");
        
        if (!File.Exists(configFile))
        {
            ShowPluginRepairError($"Config file not found at: {configFile}");
            return;
        }

        try
        {
            File.Delete(configFile);
            ShowPluginRepairSuccess($"Successfully deleted config file for {gameName}.\nPlease restart the client.");
            _logger.LogInformation("Deleted config file for {Game} at {Path}", gameName, configFile);
        }
        catch (Exception ex)
        {
            ShowPluginRepairError($"Failed to delete config file: {ex.Message}");
            _logger.LogError(ex, "Failed to delete config file for {Game}", gameName);
        }
    }

    private async Task DeletePluginCookieFile(bool isGenshin)
    {
        string gameName = isGenshin ? "Genshin Impact" : "Zenless Zone Zero";
        string cookieFileName = isGenshin ? "cookie.txt" : "cookies.json";
        string? pluginPath = isGenshin ? AppConfig.GenshinBlenderPluginPath : AppConfig.ZZZBlenderPluginPath;
        
        if (string.IsNullOrEmpty(pluginPath) || !Directory.Exists(pluginPath))
        {
            ShowPluginRepairError($"{gameName} plugin path not configured or not found.");
            return;
        }

        string cookieFile = Path.Combine(pluginPath, cookieFileName);
        
        if (!File.Exists(cookieFile))
        {
            ShowPluginRepairError($"Cookie file ({cookieFileName}) not found at: {cookieFile}");
            return;
        }

        try
        {
            File.Delete(cookieFile);
            ShowPluginRepairSuccess($"Successfully deleted {cookieFileName} for {gameName}.\nPlease scan QR code to login again.");
            _logger.LogInformation("Deleted cookie file {FileName} for {Game} at {Path}", cookieFileName, gameName, cookieFile);
        }
        catch (Exception ex)
        {
            ShowPluginRepairError($"Failed to delete cookie file: {ex.Message}");
            _logger.LogError(ex, "Failed to delete cookie file for {Game}", gameName);
        }
    }

    private void ShowPluginRepairSuccess(string message)
    {
        InfoBar_PluginRepair.Message = message;
        InfoBar_PluginRepair.Severity = InfoBarSeverity.Success;
        InfoBar_PluginRepair.IsOpen = true;
    }

    private void ShowPluginRepairError(string message)
    {
        InfoBar_PluginRepair.Message = message;
        InfoBar_PluginRepair.Severity = InfoBarSeverity.Error;
        InfoBar_PluginRepair.IsOpen = true;
    }
}
