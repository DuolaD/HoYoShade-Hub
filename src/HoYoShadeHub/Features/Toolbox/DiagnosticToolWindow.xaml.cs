using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        AppWindow.Resize(new Windows.Graphics.SizeInt32(880, 680));
    }

    public void ShowWindow(Microsoft.UI.WindowId windowId)
    {
        try
        {
            CenterInScreen(880, 680);
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

            // Update UI card summaries
            CpuText = $"{report.Hardware.CpuName} ({report.Hardware.LogicalCores} Cores)";
            MemoryText = $"{report.Hardware.TotalPhysicalMemory} (Available: {report.Hardware.AvailablePhysicalMemory})";
            
            if (report.Hardware.Gpus.Count > 0)
            {
                GpuText = string.Join("\n", report.Hardware.Gpus.Select(g =>
                {
                    var extra = new[] { g.VramSize, g.DriverVersion }.Where(s => !string.IsNullOrWhiteSpace(s));
                    string extraStr = extra.Any() ? $" [{string.Join(", ", extra)}]" : "";
                    return $"{g.Name}{extraStr}";
                }));
            }
            else
            {
                GpuText = "Unknown GPU";
            }

            DisplayText = $"{report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})";
            OsText = $"{report.System.OsName} {report.System.OsVersion} (Build {report.System.OsBuild}) [{report.System.OsArchitecture}]";
            RuntimeText = $"{report.System.DotNetRuntime} | WebView2: {report.System.WebView2Version}";
            UptimeText = report.System.SystemUptime;

            LauncherVerText = $"{report.Launcher.Version} (PID: {report.Launcher.ProcessId})";
            FrameworkVerText = $"HoYoShade: {report.Launcher.HoYoShadeFrameworkVersion} | OpenHoYoShade: {report.Launcher.OpenHoYoShadeVersion}";
            RpcStateText = report.Launcher.RpcRunning ? "Running" : "Not Running";
            PermissionsText = report.Launcher.IsAdmin ? "Administrator" : "Standard User";

            if (report.Games.Count > 0)
            {
                GamesSummaryText = string.Join("\n", report.Games.Select(g =>
                {
                    var badges = new System.Collections.Generic.List<string>();
                    if (g.EnableDX12) badges.Add("DX12");
                    if (g.HasDxgiDll) badges.Add("dxgi.dll");
                    if (g.HasD3d11Dll) badges.Add("d3d11.dll");
                    if (g.HasReShadeIni) badges.Add("ReShade.ini");
                    string badgeStr = badges.Count > 0 ? $" ({string.Join(", ", badges)})" : "";
                    return $"• {g.GameName} [{g.ServerName}]: {g.InstallPath}{badgeStr}";
                }));
            }
            else
            {
                GamesSummaryText = Lang.DiagnosticTool_NoGamesFound;
            }

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
            string? savePath = await FileDialogHelper.OpenSaveFileDialogAsync(WindowHandle, suggestedFileName, ("Zip Archive (*.zip)", "*.zip"));

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
}
