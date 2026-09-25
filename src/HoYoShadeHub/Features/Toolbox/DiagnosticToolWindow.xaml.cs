using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.System;

namespace HoYoShadeHub.Features.Toolbox;

[INotifyPropertyChanged]
public sealed partial class DiagnosticToolWindow : WindowEx
{
    private readonly ILogger<DiagnosticToolWindow> _logger = AppConfig.GetLogger<DiagnosticToolWindow>();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                OnPropertyChanged(nameof(LoadingVisibility));
            }
        }
    }

    public bool IsNotLoading => !IsLoading;
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    private string _cpuText = "-";
    public string CpuText
    {
        get => _cpuText;
        set => SetProperty(ref _cpuText, value);
    }

    private string _memoryText = "-";
    public string MemoryText
    {
        get => _memoryText;
        set => SetProperty(ref _memoryText, value);
    }

    private string _gpuText = "-";
    public string GpuText
    {
        get => _gpuText;
        set => SetProperty(ref _gpuText, value);
    }

    private string _motherboardText = "-";
    public string MotherboardText
    {
        get => _motherboardText;
        set => SetProperty(ref _motherboardText, value);
    }

    private string _monitorsText = "-";
    public string MonitorsText
    {
        get => _monitorsText;
        set => SetProperty(ref _monitorsText, value);
    }

    private string _disksText = "-";
    public string DisksText
    {
        get => _disksText;
        set => SetProperty(ref _disksText, value);
    }

    private string _audioText = "-";
    public string AudioText
    {
        get => _audioText;
        set => SetProperty(ref _audioText, value);
    }

    private string _networkText = "-";
    public string NetworkText
    {
        get => _networkText;
        set => SetProperty(ref _networkText, value);
    }

    private string _displayText = "-";
    public string DisplayText
    {
        get => _displayText;
        set => SetProperty(ref _displayText, value);
    }

    private string _osText = "-";
    public string OsText
    {
        get => _osText;
        set => SetProperty(ref _osText, value);
    }

    private string _runtimeText = "-";
    public string RuntimeText
    {
        get => _runtimeText;
        set => SetProperty(ref _runtimeText, value);
    }

    private string _uptimeText = "-";
    public string UptimeText
    {
        get => _uptimeText;
        set => SetProperty(ref _uptimeText, value);
    }

    private string _launcherVerText = "-";
    public string LauncherVerText
    {
        get => _launcherVerText;
        set => SetProperty(ref _launcherVerText, value);
    }

    private string _frameworkVerText = "-";
    public string FrameworkVerText
    {
        get => _frameworkVerText;
        set => SetProperty(ref _frameworkVerText, value);
    }

    private string _frameworkDownloadServerText = "-";
    public string FrameworkDownloadServerText
    {
        get => _frameworkDownloadServerText;
        set => SetProperty(ref _frameworkDownloadServerText, value);
    }

    private string _frameworkPreviewChannelText = "-";
    public string FrameworkPreviewChannelText
    {
        get => _frameworkPreviewChannelText;
        set => SetProperty(ref _frameworkPreviewChannelText, value);
    }

    // HoYoShade flavor properties
    private bool _hoYoShadeInstalled;
    public bool HoYoShadeInstalled
    {
        get => _hoYoShadeInstalled;
        set
        {
            if (SetProperty(ref _hoYoShadeInstalled, value))
            {
                OnPropertyChanged(nameof(HoYoShadeInstalledVisibility));
                OnPropertyChanged(nameof(HoYoShadeStatusGlyph));
                OnPropertyChanged(nameof(HoYoShadeStatusText));
                OnPropertyChanged(nameof(HoYoShadeStatusBrush));
            }
        }
    }

    public Visibility HoYoShadeInstalledVisibility => HoYoShadeInstalled ? Visibility.Visible : Visibility.Collapsed;
    public string HoYoShadeStatusGlyph => HoYoShadeInstalled ? "\uE73E" : "\uE711";
    public string HoYoShadeStatusText => HoYoShadeInstalled ? Lang.WelcomeView_Installed : Lang.WelcomeView_NotInstalled;
    public SolidColorBrush HoYoShadeStatusBrush => HoYoShadeInstalled 
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) 
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));

    private string _hoYoShadeVersion = "-";
    public string HoYoShadeVersion
    {
        get => _hoYoShadeVersion;
        set => SetProperty(ref _hoYoShadeVersion, value);
    }

    private string _hoYoShadeReShadeVersion = "-";
    public string HoYoShadeReShadeVersion
    {
        get => _hoYoShadeReShadeVersion;
        set => SetProperty(ref _hoYoShadeReShadeVersion, value);
    }

    private string _hoYoShadePath = "-";
    public string HoYoShadePath
    {
        get => _hoYoShadePath;
        set => SetProperty(ref _hoYoShadePath, value);
    }

    private string _hoYoShadeTotalSize = "0.00 B";
    public string HoYoShadeTotalSize
    {
        get => _hoYoShadeTotalSize;
        set => SetProperty(ref _hoYoShadeTotalSize, value);
    }

    private string _hoYoShadeShaderSize = "0.00 B";
    public string HoYoShadeShaderSize
    {
        get => _hoYoShadeShaderSize;
        set => SetProperty(ref _hoYoShadeShaderSize, value);
    }

    private string _hoYoShadePresetSize = "0.00 B";
    public string HoYoShadePresetSize
    {
        get => _hoYoShadePresetSize;
        set => SetProperty(ref _hoYoShadePresetSize, value);
    }

    private string _hoYoShadeScreenshotSize = "0.00 B";
    public string HoYoShadeScreenshotSize
    {
        get => _hoYoShadeScreenshotSize;
        set => SetProperty(ref _hoYoShadeScreenshotSize, value);
    }

    private string _hoYoShadeOtherSize = "0.00 B";
    public string HoYoShadeOtherSize
    {
        get => _hoYoShadeOtherSize;
        set => SetProperty(ref _hoYoShadeOtherSize, value);
    }

    // OpenHoYoShade flavor properties
    private bool _openHoYoShadeInstalled;
    public bool OpenHoYoShadeInstalled
    {
        get => _openHoYoShadeInstalled;
        set
        {
            if (SetProperty(ref _openHoYoShadeInstalled, value))
            {
                OnPropertyChanged(nameof(OpenHoYoShadeInstalledVisibility));
                OnPropertyChanged(nameof(OpenHoYoShadeStatusGlyph));
                OnPropertyChanged(nameof(OpenHoYoShadeStatusText));
                OnPropertyChanged(nameof(OpenHoYoShadeStatusBrush));
            }
        }
    }

    public Visibility OpenHoYoShadeInstalledVisibility => OpenHoYoShadeInstalled ? Visibility.Visible : Visibility.Collapsed;
    public string OpenHoYoShadeStatusGlyph => OpenHoYoShadeInstalled ? "\uE73E" : "\uE711";
    public string OpenHoYoShadeStatusText => OpenHoYoShadeInstalled ? Lang.WelcomeView_Installed : Lang.WelcomeView_NotInstalled;
    public SolidColorBrush OpenHoYoShadeStatusBrush => OpenHoYoShadeInstalled 
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) 
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));

    private string _openHoYoShadeVersion = "-";
    public string OpenHoYoShadeVersion
    {
        get => _openHoYoShadeVersion;
        set => SetProperty(ref _openHoYoShadeVersion, value);
    }

    private string _openHoYoShadeReShadeVersion = "-";
    public string OpenHoYoShadeReShadeVersion
    {
        get => _openHoYoShadeReShadeVersion;
        set => SetProperty(ref _openHoYoShadeReShadeVersion, value);
    }

    private string _openHoYoShadePath = "-";
    public string OpenHoYoShadePath
    {
        get => _openHoYoShadePath;
        set => SetProperty(ref _openHoYoShadePath, value);
    }

    private string _openHoYoShadeTotalSize = "0.00 B";
    public string OpenHoYoShadeTotalSize
    {
        get => _openHoYoShadeTotalSize;
        set => SetProperty(ref _openHoYoShadeTotalSize, value);
    }

    private string _openHoYoShadeShaderSize = "0.00 B";
    public string OpenHoYoShadeShaderSize
    {
        get => _openHoYoShadeShaderSize;
        set => SetProperty(ref _openHoYoShadeShaderSize, value);
    }

    private string _openHoYoShadePresetSize = "0.00 B";
    public string OpenHoYoShadePresetSize
    {
        get => _openHoYoShadePresetSize;
        set => SetProperty(ref _openHoYoShadePresetSize, value);
    }

    private string _openHoYoShadeScreenshotSize = "0.00 B";
    public string OpenHoYoShadeScreenshotSize
    {
        get => _openHoYoShadeScreenshotSize;
        set => SetProperty(ref _openHoYoShadeScreenshotSize, value);
    }

    private string _openHoYoShadeOtherSize = "0.00 B";
    public string OpenHoYoShadeOtherSize
    {
        get => _openHoYoShadeOtherSize;
        set => SetProperty(ref _openHoYoShadeOtherSize, value);
    }

    private string _rpcStateText = "-";
    public string RpcStateText
    {
        get => _rpcStateText;
        set => SetProperty(ref _rpcStateText, value);
    }

    private string _permissionsText = "-";
    public string PermissionsText
    {
        get => _permissionsText;
        set => SetProperty(ref _permissionsText, value);
    }

    private string _gamesSummaryText = "-";
    public string GamesSummaryText
    {
        get => _gamesSummaryText;
        set => SetProperty(ref _gamesSummaryText, value);
    }

    // Network diagnostic properties
    private bool _showFullIp;
    public bool ShowFullIp
    {
        get => _showFullIp;
        set
        {
            if (SetProperty(ref _showFullIp, value))
            {
                OnPropertyChanged(nameof(IpVisibilityGlyph));
                UpdateNetworkIpDisplays();
            }
        }
    }

    public string IpVisibilityGlyph => _showFullIp ? "\uED1A" : "\uE890";

    private string _networkIpv4Text = "-";
    public string NetworkIpv4Text
    {
        get => _networkIpv4Text;
        set => SetProperty(ref _networkIpv4Text, value);
    }

    private string _networkLocationText = "-";
    public string NetworkLocationText
    {
        get => _networkLocationText;
        set => SetProperty(ref _networkLocationText, value);
    }

    private string _networkAsnText = "-";
    public string NetworkAsnText
    {
        get => _networkAsnText;
        set => SetProperty(ref _networkAsnText, value);
    }

    private string _networkIpv6Text = "-";
    public string NetworkIpv6Text
    {
        get => _networkIpv6Text;
        set => SetProperty(ref _networkIpv6Text, value);
    }

    private string _networkColoText = "-";
    public string NetworkColoText
    {
        get => _networkColoText;
        set => SetProperty(ref _networkColoText, value);
    }

    private string _networkProxyText = "-";
    public string NetworkProxyText
    {
        get => _networkProxyText;
        set => SetProperty(ref _networkProxyText, value);
    }

    private string _networkEncryptionText = "-";
    public string NetworkEncryptionText
    {
        get => _networkEncryptionText;
        set => SetProperty(ref _networkEncryptionText, value);
    }

    private string _networkConclusionText = "-";
    public string NetworkConclusionText
    {
        get => _networkConclusionText;
        set => SetProperty(ref _networkConclusionText, value);
    }

    private SolidColorBrush _networkConclusionBrush = new(Windows.UI.Color.FromArgb(255, 16, 185, 129));
    public SolidColorBrush NetworkConclusionBrush
    {
        get => _networkConclusionBrush;
        set => SetProperty(ref _networkConclusionBrush, value);
    }

    private bool _isRetestingDoh;
    public bool IsRetestingDoh
    {
        get => _isRetestingDoh;
        set
        {
            if (SetProperty(ref _isRetestingDoh, value))
            {
                OnPropertyChanged(nameof(IsNotRetestingDoh));
            }
        }
    }
    public bool IsNotRetestingDoh => !_isRetestingDoh;

    private string _reportText = string.Empty;
    public string ReportText
    {
        get => _reportText;
        set => SetProperty(ref _reportText, value);
    }

    private DiagnosticReport? _currentReport;

    public DiagnosticToolWindow()
    {
        InitializeComponent();
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        Title = Lang.DiagnosticTool_Title;
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(920, 720));
    }

    public void ShowWindow(Microsoft.UI.WindowId windowId)
    {
        try
        {
            CenterInScreen(920, 720);
            Show();
            _ = LoadReportAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show diagnostic tool window");
        }
    }

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private void CustomTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }

    private void UpdateDragRectangles()
    {
        try
        {
            double uiScale = Content.XamlRoot?.RasterizationScale ?? (User32.GetDpiForWindow(WindowHandle) / 96.0);
            int width = (int)(CustomTitleBar.ActualWidth * uiScale);
            int height = (int)(CustomTitleBar.ActualHeight * uiScale);
            SetDragRectangles(new Windows.Graphics.RectInt32(0, 0, width, height));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set drag rectangles");
        }
    }

    private async Task LoadReportAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            InfoBar_Status.IsOpen = false;

            var report = await DiagnosticService.CollectReportAsync(WindowHandle);
            _currentReport = report;

            // 1. Hardware card summaries
            string coreInfo = report.Hardware.PhysicalCores > 0
                ? $"{report.Hardware.PhysicalCores} Cores / {report.Hardware.LogicalCores} Threads"
                : $"{report.Hardware.LogicalCores} Cores";
            CpuText = $"{report.Hardware.CpuName} ({coreInfo})";
            MotherboardText = string.IsNullOrWhiteSpace(report.Hardware.Motherboard) ? "-" : report.Hardware.Motherboard;
            MemoryText = $"{report.Hardware.MemorySummary} [Total: {report.Hardware.TotalPhysicalMemory} / Avail: {report.Hardware.AvailablePhysicalMemory}]";

            if (report.Hardware.Gpus.Count > 0)
            {
                GpuText = string.Join("\n", report.Hardware.Gpus.Select(g =>
                {
                    var extra = new[] { g.VramSize, string.IsNullOrWhiteSpace(g.DriverVersion) ? "" : $"Driver: {g.DriverVersion}", g.Provider }
                        .Where(s => !string.IsNullOrWhiteSpace(s));
                    string extraStr = extra.Any() ? $" ({string.Join(" / ", extra)})" : "";
                    return $"• {g.Name}{extraStr}";
                }));
            }
            else
            {
                GpuText = "-";
            }

            if (report.Hardware.Monitors.Count > 0)
            {
                var monLines = report.Hardware.Monitors.Select(m =>
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrWhiteSpace(m.Manufacturer) || !string.IsNullOrWhiteSpace(m.ModelCode))
                    {
                        parts.Add($"{m.Manufacturer} {m.ModelCode}".Trim());
                    }
                    string extraBracket = parts.Count > 0 ? $" [{string.Join(" ", parts)}]" : "";
                    string sizeStr = string.IsNullOrWhiteSpace(m.DiagonalSize) ? "" : $" ({m.DiagonalSize})";
                    return $"• {m.Name}{extraBracket}{sizeStr}";
                }).ToList();
                monLines.Add($"• Primary: {report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})");
                MonitorsText = string.Join("\n", monLines);
            }
            else
            {
                MonitorsText = $"{report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})";
            }

            if (report.Hardware.Disks.Count > 0)
            {
                DisksText = string.Join("\n", report.Hardware.Disks.Select(d =>
                {
                    string drives = d.DriveLetters.Count > 0 ? string.Join(", ", d.DriveLetters) : "-";
                    return $"• {d.Model} ( {d.SizeString} ) -> [ {drives} ]";
                }));
            }
            else
            {
                DisksText = "-";
            }

            AudioText = report.Hardware.AudioDevices.Count > 0
                ? string.Join("\n", report.Hardware.AudioDevices.Select(a => $"• {a}"))
                : "-";

            NetworkText = report.Hardware.NetworkAdapters.Count > 0
                ? string.Join("\n", report.Hardware.NetworkAdapters.Select(n => $"• {n}"))
                : "-";

            // 2. System info
            DisplayText = $"{report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})";
            OsText = $"{report.System.OsName} {report.System.OsVersion} (Build {report.System.OsBuild}) [{report.System.OsArchitecture}]";
            RuntimeText = $"{report.System.DotNetRuntime} | WebView2: {report.System.WebView2Version}";
            UptimeText = report.System.SystemUptime;

            // 3. Launcher & Framework info
            LauncherVerText = $"{report.Launcher.Version} (PID: {report.Launcher.ProcessId})";
            FrameworkVerText = $"HoYoShade: {report.Launcher.HoYoShadeFrameworkVersion} | OpenHoYoShade: {report.Launcher.OpenHoYoShadeVersion}";
            RpcStateText = report.Launcher.RpcRunning ? "Running" : "Not Running";
            PermissionsText = report.Launcher.IsAdmin ? "Administrator" : "Standard User";
            FrameworkDownloadServerText = report.Launcher.FrameworkDownloadServer;
            FrameworkPreviewChannelText = report.Launcher.FrameworkPreviewChannel ? "✓ Enabled" : "✗ Disabled";

            HoYoShadeInstalled = report.Launcher.HoYoShade.IsInstalled;
            HoYoShadeVersion = report.Launcher.HoYoShade.Version;
            HoYoShadeReShadeVersion = report.Launcher.HoYoShade.ReShadeVersion;
            HoYoShadePath = report.Launcher.HoYoShade.InstallPath;
            HoYoShadeTotalSize = report.Launcher.HoYoShade.TotalSize;
            HoYoShadeShaderSize = report.Launcher.HoYoShade.ShaderSize;
            HoYoShadePresetSize = report.Launcher.HoYoShade.PresetSize;
            HoYoShadeScreenshotSize = report.Launcher.HoYoShade.ScreenshotSize;
            HoYoShadeOtherSize = report.Launcher.HoYoShade.OtherSize;

            OpenHoYoShadeInstalled = report.Launcher.OpenHoYoShade.IsInstalled;
            OpenHoYoShadeVersion = report.Launcher.OpenHoYoShade.Version;
            OpenHoYoShadeReShadeVersion = report.Launcher.OpenHoYoShade.ReShadeVersion;
            OpenHoYoShadePath = report.Launcher.OpenHoYoShade.InstallPath;
            OpenHoYoShadeTotalSize = report.Launcher.OpenHoYoShade.TotalSize;
            OpenHoYoShadeShaderSize = report.Launcher.OpenHoYoShade.ShaderSize;
            OpenHoYoShadePresetSize = report.Launcher.OpenHoYoShade.PresetSize;
            OpenHoYoShadeScreenshotSize = report.Launcher.OpenHoYoShade.ScreenshotSize;
            OpenHoYoShadeOtherSize = report.Launcher.OpenHoYoShade.OtherSize;

            // 4. Games info
            if (report.Games.Count > 0)
            {
                GamesSummaryText = string.Join("\n\n", report.Games.Select(g =>
                {
                    var badges = new System.Collections.Generic.List<string>();
                    if (g.EnableDX12) badges.Add("DX12");
                    if (g.HasDxgiDll) badges.Add("dxgi.dll");
                    if (g.HasD3d11Dll) badges.Add("d3d11.dll");
                    if (g.HasReShadeIni) badges.Add("ReShade.ini");
                    if (g.HasReShadeLog) badges.Add("ReShade.log");
                    string badgeStr = badges.Count > 0 ? $" ({string.Join(", ", badges)})" : "";

                    var launchList = new System.Collections.Generic.List<string>();
                    launchList.Add($"{Lang.GameLauncherPage_LaunchGame}: {(g.EnableGameLaunch ? "✓" : "✗")}");
                    if (g.UseStarwardLauncher)
                    {
                        launchList.Add($"{Lang.GameLauncherPage_LaunchWithStarward}: ✓");
                    }
                    launchList.Add($"HoYoShade: {(g.UseHoYoShade ? "✓" : "✗")}");
                    launchList.Add($"OpenHoYoShade: {(g.UseOpenHoYoShade ? "✓" : "✗")}");
                    if (g.Biz.StartsWith(GameBiz.hk4e, StringComparison.OrdinalIgnoreCase))
                    {
                        launchList.Add($"{Lang.GameLauncherPage_LaunchGenshinBlenderPlugin}: {(g.LaunchGenshinBlenderPlugin ? "✓" : "✗")}");
                    }
                    else if (g.Biz.StartsWith(GameBiz.nap, StringComparison.OrdinalIgnoreCase))
                    {
                        launchList.Add($"{Lang.GameLauncherPage_LaunchZZZBlenderPlugin}: {(g.LaunchZZZBlenderPlugin ? "✓" : "✗")}");
                    }
                    if (g.UsePopupWindow)
                    {
                        launchList.Add($"{Lang.GameSettingPage_UsePopupWindow}: ✓");
                    }
                    if (!string.IsNullOrWhiteSpace(g.StartArgument))
                    {
                        launchList.Add($"{Lang.GameLauncherSettingDialog_CommandLineArgument}: {g.StartArgument}");
                    }

                    string launchStr = $"\n  └ {Lang.GameLauncherPage_LaunchOptions}: {string.Join(" | ", launchList)}";
                    return $"• {g.GameName} [{g.ServerName}]: {g.InstallPath}{badgeStr}{launchStr}";
                }));
            }
            else
            {
                GamesSummaryText = Lang.DiagnosticTool_NoGamesFound;
            }

            // Network card summaries
            UpdateNetworkCardDisplays(report.Network);

            ReportText = DiagnosticService.ToFormattedText(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load diagnostic report");
            ShowStatus(InfoBarSeverity.Error, $"{Lang.DiagnosticTool_ExportFailed} {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Button_CopyReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ReportText)) return;
            ClipboardHelper.SetText(ReportText);
            ShowStatus(InfoBarSeverity.Success, Lang.DiagnosticTool_ReportCopied);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy report text to clipboard");
            ShowStatus(InfoBarSeverity.Error, ex.Message);
        }
    }

    private async void Button_ExportZip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_currentReport == null)
            {
                await LoadReportAsync();
            }

            if (_currentReport == null) return;

            string suggestedFileName = $"HoYoShadeHub_Diagnostic_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string? savePath = await FileDialogHelper.OpenSaveFileDialogAsync(WindowHandle, suggestedFileName, ("Zip Archive (*.zip)", ".zip"));

            if (string.IsNullOrWhiteSpace(savePath)) return;

            IsLoading = true;
            await DiagnosticService.ExportZipAsync(_currentReport, savePath);

            var actionButton = new Button
            {
                Content = Lang.DiagnosticTool_OpenLogFolder,
                Margin = new Thickness(8, 0, 0, 0)
            };
            actionButton.Click += (s, ev) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{savePath}\"",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            ShowStatus(InfoBarSeverity.Success, string.Format(Lang.DiagnosticTool_ExportSuccess, savePath), actionButton);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export diagnostic zip package");
            ShowStatus(InfoBarSeverity.Error, $"{Lang.DiagnosticTool_ExportFailed} {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void Button_OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logFolder = Path.Combine(AppConfig.CacheFolder, "log");
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            await Launcher.LaunchUriAsync(new Uri(logFolder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open log folder");
        }
    }

    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadReportAsync();
    }

    private void ShowStatus(InfoBarSeverity severity, string message, Button? actionButton = null)
    {
        InfoBar_Status.Severity = severity;
        InfoBar_Status.Message = message;
        InfoBar_Status.ActionButton = actionButton;
        InfoBar_Status.IsOpen = true;
    }

    #region Network Diagnostic Handlers

    private void UpdateNetworkIpDisplays()
    {
        if (_currentReport?.Network == null) return;
        var net = _currentReport.Network;

        string displayV4 = _showFullIp
            ? (string.IsNullOrWhiteSpace(net.Ipv4) ? "-" : net.Ipv4)
            : (string.IsNullOrWhiteSpace(net.MaskedIpv4) ? "-" : net.MaskedIpv4);
        NetworkIpv4Text = displayV4;

        if (net.HasIpv6)
        {
            string displayV6 = _showFullIp
                ? (string.IsNullOrWhiteSpace(net.Ipv6) ? "-" : net.Ipv6)
                : (string.IsNullOrWhiteSpace(net.MaskedIpv6) ? "-" : net.MaskedIpv6);
            NetworkIpv6Text = displayV6;
        }
        else
        {
            NetworkIpv6Text = "未检测到 / 无 IPv6 出口";
        }
    }

    private void UpdateNetworkCardDisplays(NetworkDiagnosticInfo net)
    {
        UpdateNetworkIpDisplays();

        string locParts = string.Join(" ", new[] { net.Country, net.Region, net.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        NetworkLocationText = string.IsNullOrWhiteSpace(locParts) ? "-" : locParts;

        string asnText = string.IsNullOrWhiteSpace(net.AsOrganization)
            ? (string.IsNullOrWhiteSpace(net.Asn) ? "-" : net.Asn)
            : $"{net.Asn} {net.AsOrganization}".Trim();
        NetworkAsnText = string.IsNullOrWhiteSpace(asnText) ? "-" : asnText;

        NetworkColoText = string.IsNullOrWhiteSpace(net.CloudflareColo) ? "-" : net.CloudflareColo;

        NetworkProxyText = net.HasSystemProxy
            ? $"已启用 [{net.SystemProxyServer}]"
            : "未开启 (Direct)";

        NetworkEncryptionText = $"DoH: {(net.LauncherDohEnabled ? "开启" : "关闭")} [{net.LauncherDohProvider}] | ECH: {(net.LauncherEchEnabled ? "开启" : "关闭")}";

        NetworkConclusionText = net.DiagnosisConclusion;

        if (net.DohEchRescueAttempted && net.DohEchRescueSuccess)
        {
            NetworkConclusionBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11));
        }
        else if (!net.DirectConnectionSuccess && (!net.DohEchRescueAttempted || !net.DohEchRescueSuccess))
        {
            NetworkConclusionBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        }
        else
        {
            NetworkConclusionBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129));
        }
    }

    private void Button_ToggleIpMask_Click(object sender, RoutedEventArgs e)
    {
        ShowFullIp = !ShowFullIp;
    }

    private async void Button_RetestWithDoh_Click(object sender, RoutedEventArgs e)
    {
        if (IsRetestingDoh || _currentReport == null) return;
        try
        {
            IsRetestingDoh = true;
            NetworkConclusionText = "正在使用 DoH+ECH 加密隧道测试连通性并获取网络信息...";
            var netInfo = await DiagnosticService.CollectNetworkDiagnosticInfoAsync(forceDohEch: true);
            _currentReport.Network = netInfo;
            UpdateNetworkCardDisplays(netInfo);
            ReportText = DiagnosticService.ToFormattedText(_currentReport);
            ShowStatus(InfoBarSeverity.Success, "DoH+ECH 连通性测试已完成并更新报告！");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retest network with DoH+ECH");
            ShowStatus(InfoBarSeverity.Error, $"DoH+ECH 测试遇到异常: {ex.Message}");
        }
        finally
        {
            IsRetestingDoh = false;
        }
    }

    #endregion
}
