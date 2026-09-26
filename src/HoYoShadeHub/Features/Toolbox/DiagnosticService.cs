using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Features.GameLauncher;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace HoYoShadeHub.Features.Toolbox;

public class DiagnosticReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public HardwareDiagnosticInfo Hardware { get; set; } = new();
    public SystemDiagnosticInfo System { get; set; } = new();
    public LauncherDiagnosticInfo Launcher { get; set; } = new();
    public NetworkDiagnosticInfo Network { get; set; } = new();
    public List<GameDiagnosticInfo> Games { get; set; } = [];
    public List<string> RecentLogSnippets { get; set; } = [];
}

public class NetworkDiagnosticInfo
{
    public bool IsEnabled { get; set; }
    public string Ipv4 { get; set; } = string.Empty;
    public string MaskedIpv4 { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Asn { get; set; } = string.Empty;
    public string AsOrganization { get; set; } = string.Empty;
    public string CloudflareColo { get; set; } = string.Empty;
    public string SuccessfulTier { get; set; } = string.Empty;

    public bool HasIpv6 { get; set; }
    public string Ipv6 { get; set; } = string.Empty;
    public string MaskedIpv6 { get; set; } = string.Empty;

    public bool HasSystemProxy { get; set; }
    public string SystemProxyServer { get; set; } = string.Empty;

    public bool LauncherDohEnabled { get; set; }
    public string LauncherDohProvider { get; set; } = string.Empty;
    public bool LauncherEchEnabled { get; set; }

    public bool DirectConnectionSuccess { get; set; }
    public string DirectConnectionError { get; set; } = string.Empty;
    public bool DohEchRescueAttempted { get; set; }
    public bool DohEchRescueSuccess { get; set; }
    public string DohEchRescueDetails { get; set; } = string.Empty;

    public string DiagnosisConclusion { get; set; } = string.Empty;
}

public class HardwareDiagnosticInfo
{
    public string CpuName { get; set; } = string.Empty;
    public int PhysicalCores { get; set; }
    public int LogicalCores { get; set; }
    public string Motherboard { get; set; } = string.Empty;
    public string MemorySummary { get; set; } = string.Empty;
    public string TotalPhysicalMemory { get; set; } = string.Empty;
    public string AvailablePhysicalMemory { get; set; } = string.Empty;
    public List<MemoryStickInfo> MemorySticks { get; set; } = [];
    public List<GpuDiagnosticInfo> Gpus { get; set; } = [];
    public List<DiskDiagnosticInfo> Disks { get; set; } = [];
    public List<MonitorDiagnosticInfo> Monitors { get; set; } = [];
    public List<string> AudioDevices { get; set; } = [];
    public List<string> NetworkAdapters { get; set; } = [];
    public string PrimaryResolution { get; set; } = string.Empty;
    public string DisplayDpiScale { get; set; } = string.Empty;
}

public class MemoryStickInfo
{
    public string Capacity { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string MemoryType { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
}

public class DiskDiagnosticInfo
{
    public int Index { get; set; }
    public string Model { get; set; } = string.Empty;
    public string SizeString { get; set; } = string.Empty;
    public ulong TotalSizeBytes { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public List<string> DriveLetters { get; set; } = [];
}

public class MonitorDiagnosticInfo
{
    public string Name { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string DiagonalSize { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
}

public class GpuDiagnosticInfo
{
    public string Name { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string VramSize { get; set; } = string.Empty;
}

public class SystemDiagnosticInfo
{
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string OsArchitecture { get; set; } = string.Empty;
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string DotNetRuntime { get; set; } = string.Empty;
    public string WebView2Version { get; set; } = string.Empty;
    public string SystemUptime { get; set; } = string.Empty;
}

public class LauncherDiagnosticInfo
{
    public string Version { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsPortable { get; set; }
    public bool IsRemovableStorage { get; set; }
    public string UpdateChannel { get; set; } = string.Empty;
    public string BaseDirectory { get; set; } = string.Empty;
    public string UserDataFolder { get; set; } = string.Empty;
    public string CacheFolder { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string DownloadServer { get; set; } = string.Empty;
    public bool DohEnabled { get; set; }
    public bool EchEnabled { get; set; }
    public string DohProvider { get; set; } = string.Empty;
    public bool RpcRunning { get; set; }
    public string HoYoShadeFrameworkVersion { get; set; } = string.Empty;
    public string OpenHoYoShadeVersion { get; set; } = string.Empty;

    // Detailed Framework information (Settings & Flavors)
    public string FrameworkDownloadServer { get; set; } = string.Empty;
    public bool FrameworkPreviewChannel { get; set; }
    public FrameworkFlavorDiagnosticInfo HoYoShade { get; set; } = new();
    public FrameworkFlavorDiagnosticInfo OpenHoYoShade { get; set; } = new();
}

public class FrameworkFlavorDiagnosticInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ReShadeVersion { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string TotalSize { get; set; } = "0.00 B";
    public string ShaderSize { get; set; } = "0.00 B";
    public string PresetSize { get; set; } = "0.00 B";
    public string ScreenshotSize { get; set; } = "0.00 B";
    public string OtherSize { get; set; } = "0.00 B";
}

public class GameDiagnosticInfo
{
    public string Biz { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;

    // Launch Options (启动选项)
    public bool EnableGameLaunch { get; set; } = true;
    public bool UseStarwardLauncher { get; set; }
    public bool UseHoYoShade { get; set; }
    public bool UseOpenHoYoShade { get; set; }
    public bool LaunchGenshinBlenderPlugin { get; set; }
    public bool LaunchZZZBlenderPlugin { get; set; }
    public bool UsePopupWindow { get; set; }
    public string? StartArgument { get; set; }

    // DX12 & Injection Files
    public bool EnableDX12 { get; set; }
    public bool IgnoreDX12Check { get; set; }
    public bool HasDxgiDll { get; set; }
    public bool HasD3d11Dll { get; set; }
    public bool HasReShadeIni { get; set; }
    public bool HasReShadeLog { get; set; }
}

public static class DiagnosticService
{
    private static readonly ILogger _logger = AppConfig.GetLogger<DiagnosticReport>();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static async Task<DiagnosticReport> CollectReportAsync(nint windowHandle = 0, bool? enableNetwork = null)
    {
        var report = new DiagnosticReport
        {
            Timestamp = DateTime.Now
        };

        await Task.Run(async () =>
        {
            // 1. Hardware Info
            try
            {
                var (totalMem, availMem) = GetMemoryInfo();
                var (cpuName, physCores, logCores) = GetCpuDetails();
                string motherboard = GetMotherboardInfo();
                var (memSummary, memSticks) = GetDetailedMemoryInfo(totalMem, availMem);
                var gpus = GetGpuInfo();
                var disks = GetDiskInfo();
                var monitors = GetMonitorInfo();
                var audio = GetAudioDevices();
                var network = GetNetworkAdapters();

                string resolution = $"{User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN)} x {User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN)}";
                string dpiScale = "100%";
                if (windowHandle != 0)
                {
                    double scale = User32.GetDpiForWindow(windowHandle) / 96.0;
                    dpiScale = $"{scale:P0}";
                }

                report.Hardware = new HardwareDiagnosticInfo
                {
                    CpuName = cpuName,
                    PhysicalCores = physCores,
                    LogicalCores = logCores,
                    Motherboard = motherboard,
                    MemorySummary = memSummary,
                    TotalPhysicalMemory = totalMem,
                    AvailablePhysicalMemory = availMem,
                    MemorySticks = memSticks,
                    Gpus = gpus,
                    Disks = disks,
                    Monitors = monitors,
                    AudioDevices = audio,
                    NetworkAdapters = network,
                    PrimaryResolution = resolution,
                    DisplayDpiScale = dpiScale
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect hardware diagnostic info");
            }

            // 2. System Info
            try
            {
                var (osName, displayVer, build) = GetOsInfo();
                report.System = new SystemDiagnosticInfo
                {
                    OsName = osName,
                    OsVersion = displayVer,
                    OsBuild = build,
                    OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    DotNetRuntime = RuntimeInformation.FrameworkDescription,
                    WebView2Version = GetWebView2Version(),
                    SystemUptime = GetSystemUptime()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect system diagnostic info");
            }

            // 3. Launcher & Framework Info
            try
            {
                var versionService = !string.IsNullOrWhiteSpace(AppConfig.UserDataFolder)
                    ? new HoYoShadeVersionService(AppConfig.UserDataFolder)
                    : null;
                var manifest = versionService != null
                    ? await versionService.LoadManifestAsync()
                    : new HoYoShadeVersionManifest();

                var hoyoshadeInfo = await CollectFrameworkFlavorInfoAsync("HoYoShade", "HoYoShade", manifest);
                var openHoyoshadeInfo = await CollectFrameworkFlavorInfoAsync("OpenHoYoShade", "OpenHoYoShade", manifest);

                report.Launcher = new LauncherDiagnosticInfo
                {
                    Version = AppConfig.AppVersion ?? "Unknown",
                    ProcessId = Environment.ProcessId,
                    IsAdmin = AppConfig.IsAdmin,
                    IsPortable = AppConfig.IsPortable,
                    IsRemovableStorage = AppConfig.IsAppInRemovableStorage,
                    UpdateChannel = AppConfig.EnablePreviewRelease ? "Preview" : "Release",
                    BaseDirectory = Sanitize(AppContext.BaseDirectory),
                    UserDataFolder = Sanitize(AppConfig.UserDataFolder),
                    CacheFolder = Sanitize(AppConfig.CacheFolder),
                    ConfigPath = Sanitize(AppConfig.ConfigPath),
                    DownloadServer = AppConfig.LauncherUpdateDownloadServer == -1 ? "Auto" : $"Server #{AppConfig.LauncherUpdateDownloadServer}",
                    DohEnabled = DohService.Enabled,
                    EchEnabled = DohService.EnableEch,
                    DohProvider = DohService.Provider.ToString(),
                    RpcRunning = CheckRpcRunning(),
                    HoYoShadeFrameworkVersion = hoyoshadeInfo.IsInstalled ? hoyoshadeInfo.Version : Lang.WelcomeView_NotInstalled,
                    OpenHoYoShadeVersion = openHoyoshadeInfo.IsInstalled ? openHoyoshadeInfo.Version : Lang.WelcomeView_NotInstalled,
                    FrameworkDownloadServer = GetFrameworkDownloadServerName(AppConfig.HoYoShadeFrameworkDownloadServer),
                    FrameworkPreviewChannel = AppConfig.EnableHoYoShadePreviewChannel,
                    HoYoShade = hoyoshadeInfo,
                    OpenHoYoShade = openHoyoshadeInfo
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect launcher diagnostic info");
            }

            // 4. Network Info
            try
            {
                bool checkNetwork = enableNetwork ?? AppConfig.EnableDiagnosticNetworkInfo;
                if (checkNetwork)
                {
                    report.Network = await CollectNetworkDiagnosticInfoAsync();
                    report.Network.IsEnabled = true;
                }
                else
                {
                    report.Network = new NetworkDiagnosticInfo
                    {
                        IsEnabled = false,
                        DiagnosisConclusion = Lang.DiagnosticTool_NetworkDisabledNotice
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect network diagnostic info");
                report.Network = new NetworkDiagnosticInfo
                {
                    IsEnabled = true,
                    DiagnosisConclusion = string.Format(Lang.DiagnosticTool_Conclusion_Exception, ex.Message)
                };
            }

            // 5. Games
            try
            {
                report.Games = GetGameInstallations();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect game diagnostic info");
            }

            // 5. Recent Logs
            try
            {
                report.RecentLogSnippets = GetRecentLogLines(250);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect recent log lines");
            }
        });

        return report;
    }

    public static string GetDiagnosisConclusion(NetworkDiagnosticInfo? net)
    {
        if (net == null || !net.IsEnabled)
        {
            return Lang.DiagnosticTool_NetworkDisabledNotice;
        }
        if (!string.IsNullOrWhiteSpace(net.DirectConnectionError) && !net.DohEchRescueAttempted)
        {
            return string.Format(Lang.DiagnosticTool_Conclusion_Exception, net.DirectConnectionError);
        }
        if (net.DirectConnectionSuccess)
        {
            return Lang.DiagnosticTool_Conclusion_DirectSuccess;
        }
        if (net.DohEchRescueAttempted)
        {
            if (net.DohEchRescueSuccess)
            {
                return Lang.DiagnosticTool_Conclusion_DohRescueSuccess;
            }
            else
            {
                return Lang.DiagnosticTool_Conclusion_AllFailed;
            }
        }
        return !string.IsNullOrWhiteSpace(net.DiagnosisConclusion) ? net.DiagnosisConclusion : Lang.DiagnosticTool_Conclusion_DirectSuccess;
    }

    public static string GetDohRescueDetails(NetworkDiagnosticInfo? net)
    {
        if (net == null) return string.Empty;
        if (net.DohEchRescueSuccess)
        {
            return Lang.DiagnosticTool_DohRescueDetails_Success;
        }
        if (!string.IsNullOrWhiteSpace(net.DohEchRescueDetails) && net.DohEchRescueDetails.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Format(Lang.DiagnosticTool_DohRescueDetails_Exception, net.DohEchRescueDetails["Error:".Length..].Trim());
        }
        return Lang.DiagnosticTool_DohRescueDetails_Failed;
    }

    public static string ToFormattedText(DiagnosticReport report, bool? maskIp = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine($"                 {Lang.DiagnosticTool_Report_Title}                      ");
        sb.AppendLine(string.Format(Lang.DiagnosticTool_Report_GeneratedAt, $"{report.Timestamp:yyyy-MM-dd HH:mm:ss}"));
        sb.AppendLine(Lang.DiagnosticTool_Report_PrivacyNotice);
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        // 1. 本机硬件配置单
        sb.AppendLine(Lang.DiagnosticTool_Report_SecHardware);
        string coreText = report.Hardware.PhysicalCores > 0
            ? string.Format(Lang.DiagnosticTool_Report_CoresThreads, report.Hardware.PhysicalCores, report.Hardware.LogicalCores)
            : string.Format(Lang.DiagnosticTool_Report_Cores, report.Hardware.LogicalCores);
        sb.AppendLine($"  - {Lang.DiagnosticTool_Cpu}: {report.Hardware.CpuName} ({coreText})");
        sb.AppendLine($"  - {Lang.DiagnosticTool_Motherboard}: {(string.IsNullOrWhiteSpace(report.Hardware.Motherboard) ? "-" : report.Hardware.Motherboard)}");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_MemorySummary, report.Hardware.MemorySummary, report.Hardware.TotalPhysicalMemory, report.Hardware.AvailablePhysicalMemory)}");

        if (report.Hardware.Gpus.Count > 0)
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Report_Gpu}");
            for (int i = 0; i < report.Hardware.Gpus.Count; i++)
            {
                var gpu = report.Hardware.Gpus[i];
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(gpu.VramSize)) parts.Add(gpu.VramSize);
                if (!string.IsNullOrWhiteSpace(gpu.DriverVersion)) parts.Add(string.Format(Lang.DiagnosticTool_Report_Driver, gpu.DriverVersion));
                if (!string.IsNullOrWhiteSpace(gpu.Provider)) parts.Add(gpu.Provider);
                string extra = parts.Count > 0 ? $" ({string.Join(" / ", parts)})" : "";
                sb.AppendLine($"      * {gpu.Name}{extra}");
            }
        }
        else
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Report_Gpu} -");
        }

        if (report.Hardware.Monitors.Count > 0)
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Monitors}:");
            foreach (var mon in report.Hardware.Monitors)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(mon.Manufacturer) || !string.IsNullOrWhiteSpace(mon.ModelCode))
                {
                    parts.Add($"{mon.Manufacturer} {mon.ModelCode}".Trim());
                }
                string extraBracket = parts.Count > 0 ? $" [{string.Join(" ", parts)}]" : "";
                string sizeStr = string.IsNullOrWhiteSpace(mon.DiagonalSize) ? "" : $" ({mon.DiagonalSize})";
                sb.AppendLine($"      * {mon.Name}{extraBracket}{sizeStr}");
            }
            sb.AppendLine($"      * {string.Format(Lang.DiagnosticTool_Report_PrimaryDisplay, report.Hardware.PrimaryResolution, report.Hardware.DisplayDpiScale)}");
        }
        else
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Display}: {report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})");
        }

        if (report.Hardware.Disks.Count > 0)
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Report_DisksDrives}");
            foreach (var disk in report.Hardware.Disks)
            {
                string drives = disk.DriveLetters.Count > 0 ? string.Join(", ", disk.DriveLetters) : "-";
                sb.AppendLine($"      * {disk.Model} ( {disk.SizeString} ) -> [ {drives} ]");
            }
        }
        else
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Report_DisksDrives} -");
        }

        if (report.Hardware.AudioDevices.Count > 0)
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Audio}:");
            foreach (var audio in report.Hardware.AudioDevices)
            {
                sb.AppendLine($"      * {audio}");
            }
        }

        if (report.Hardware.NetworkAdapters.Count > 0)
        {
            sb.AppendLine($"  - {Lang.DiagnosticTool_Network}:");
            foreach (var net in report.Hardware.NetworkAdapters)
            {
                sb.AppendLine($"      * {net}");
            }
        }
        sb.AppendLine();

        // 2. 操作系统与运行环境
        sb.AppendLine(Lang.DiagnosticTool_Report_SecSystem);
        sb.AppendLine($"  - {Lang.DiagnosticTool_OS}: {report.System.OsName} {report.System.OsVersion} (Build {report.System.OsBuild})");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_OsArch, report.System.OsArchitecture, report.System.ProcessArchitecture)}");
        sb.AppendLine($"  - {Lang.DiagnosticTool_Runtime}: {report.System.DotNetRuntime}");
        sb.AppendLine($"  - WebView2: {report.System.WebView2Version}");
        sb.AppendLine($"  - {Lang.DiagnosticTool_Uptime}: {report.System.SystemUptime}");
        sb.AppendLine();

        // 3. 启动器与框架状态
        sb.AppendLine(Lang.DiagnosticTool_Report_SecLauncher);
        sb.AppendLine($"  - {Lang.DiagnosticTool_LauncherVer}: {report.Launcher.Version} (PID: {report.Launcher.ProcessId})");
        string adminText = report.Launcher.IsAdmin ? Lang.DiagnosticTool_Admin : Lang.DiagnosticTool_StandardUser;
        sb.AppendLine($"  - {Lang.DiagnosticTool_Permissions}: {adminText}");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_RunningMode, report.Launcher.IsPortable, report.Launcher.IsRemovableStorage)}");
        string updateChannelDisplay = report.Launcher.UpdateChannel == "Preview" ? Lang.DiagnosticTool_Report_ChannelPreview : Lang.DiagnosticTool_Report_ChannelRelease;
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_UpdateChannel, updateChannelDisplay)}");
        string rpcText = report.Launcher.RpcRunning ? Lang.DiagnosticTool_Running : Lang.DiagnosticTool_NotRunning;
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_RpcService, rpcText)}");
        string dohState = report.Launcher.DohEnabled ? Lang.DiagnosticTool_On : Lang.DiagnosticTool_Off;
        string echState = report.Launcher.EchEnabled ? Lang.DiagnosticTool_On : Lang.DiagnosticTool_Off;
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_NetworkEncryption, dohState, report.Launcher.DohProvider, echState)}");
        string dlServer = report.Launcher.DownloadServer;
        if (dlServer == "Auto" || dlServer == Lang.HoYoShadeDownloadView_Server_AutoSelect) dlServer = Lang.DiagnosticTool_Report_DownloadNode_Auto;
        else if (dlServer.StartsWith("Server #")) dlServer = string.Format(Lang.DiagnosticTool_Report_DownloadNode_Server, dlServer["Server #".Length..]);
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_LauncherDownloadNode, dlServer)}");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_BaseDirectory, report.Launcher.BaseDirectory)}");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_UserDataFolder, report.Launcher.UserDataFolder)}");
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_CacheFolder, report.Launcher.CacheFolder)}");
        sb.AppendLine();
        sb.AppendLine($"  {Lang.DiagnosticTool_Report_FrameworkGeneralSettings}");
        string fwServer = GetFrameworkDownloadServerName(AppConfig.HoYoShadeFrameworkDownloadServer);
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_FrameworkDownloadServer, fwServer)}");
        string fwPreview = report.Launcher.FrameworkPreviewChannel ? Lang.DiagnosticTool_Enabled : Lang.DiagnosticTool_Disabled;
        sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_FrameworkPreviewChannel, fwPreview)}");
        sb.AppendLine();
        sb.AppendLine("  * HoYoShade:");
        string hysStatus = report.Launcher.HoYoShade.IsInstalled ? Lang.WelcomeView_Installed : Lang.WelcomeView_NotInstalled;
        sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_Status, hysStatus)}");
        if (report.Launcher.HoYoShade.IsInstalled)
        {
            sb.AppendLine($"    - {Lang.FileSettingPage_FrameworkVersion}: {report.Launcher.HoYoShade.Version}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ReShadeVersion}: {report.Launcher.HoYoShade.ReShadeVersion}");
            sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_InstallPath, report.Launcher.HoYoShade.InstallPath)}");
            sb.AppendLine($"    - {Lang.FileSettingPage_TotalSize}: {report.Launcher.HoYoShade.TotalSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ShaderAndAddonSize}: {report.Launcher.HoYoShade.ShaderSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_PresetSize}: {report.Launcher.HoYoShade.PresetSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ScreenshotSize}: {report.Launcher.HoYoShade.ScreenshotSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_OtherContentSize}: {report.Launcher.HoYoShade.OtherSize}");
        }
        sb.AppendLine();
        sb.AppendLine("  * OpenHoYoShade:");
        string ohysStatus = report.Launcher.OpenHoYoShade.IsInstalled ? Lang.WelcomeView_Installed : Lang.WelcomeView_NotInstalled;
        sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_Status, ohysStatus)}");
        if (report.Launcher.OpenHoYoShade.IsInstalled)
        {
            sb.AppendLine($"    - {Lang.FileSettingPage_FrameworkVersion}: {report.Launcher.OpenHoYoShade.Version}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ReShadeVersion}: {report.Launcher.OpenHoYoShade.ReShadeVersion}");
            sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_InstallPath, report.Launcher.OpenHoYoShade.InstallPath)}");
            sb.AppendLine($"    - {Lang.FileSettingPage_TotalSize}: {report.Launcher.OpenHoYoShade.TotalSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ShaderAndAddonSize}: {report.Launcher.OpenHoYoShade.ShaderSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_PresetSize}: {report.Launcher.OpenHoYoShade.PresetSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_ScreenshotSize}: {report.Launcher.OpenHoYoShade.ScreenshotSize}");
            sb.AppendLine($"    - {Lang.FileSettingPage_OtherContentSize}: {report.Launcher.OpenHoYoShade.OtherSize}");
        }
        sb.AppendLine();

        // 4. 网络环境与出口诊断
        sb.AppendLine(Lang.DiagnosticTool_Report_SecNetwork);
        if (!report.Network.IsEnabled && string.IsNullOrWhiteSpace(report.Network.Ipv4) && string.IsNullOrWhiteSpace(report.Network.SuccessfulTier))
        {
            sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_Status, Lang.DiagnosticTool_NetworkDisabledNotice)}");
            sb.AppendLine();
        }
        else
        {
            bool isMasked = maskIp ?? AppConfig.DiagnosticIpMasking;
            string v4Display = isMasked
                ? (string.IsNullOrWhiteSpace(report.Network.MaskedIpv4) ? "-" : report.Network.MaskedIpv4)
                : (string.IsNullOrWhiteSpace(report.Network.Ipv4) ? "-" : report.Network.Ipv4);
            string v4Suffix = isMasked ? Lang.DiagnosticTool_Report_Ipv4MaskedSuffix : Lang.DiagnosticTool_Report_Ipv4PlainSuffix;
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkIpv4}: {v4Display}{v4Suffix}");

            string countryDisplay = !string.IsNullOrWhiteSpace(report.Network.CountryCode)
                ? FormatCountry(report.Network.CountryCode)
                : report.Network.Country;
            string locParts = string.Join(" ", new[] { countryDisplay, report.Network.Region, report.Network.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(locParts)) locParts = "-";
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkLocation}: {locParts}");

            string asnText = string.IsNullOrWhiteSpace(report.Network.AsOrganization)
                ? (string.IsNullOrWhiteSpace(report.Network.Asn) ? "-" : report.Network.Asn)
                : $"{report.Network.Asn} {report.Network.AsOrganization}".Trim();
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkAsn}: {asnText}");
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkColo}: {(string.IsNullOrWhiteSpace(report.Network.CloudflareColo) ? "-" : report.Network.CloudflareColo)}");

            string ipv6Text;
            if (report.Network.HasIpv6)
            {
                string v6Display = isMasked
                    ? (string.IsNullOrWhiteSpace(report.Network.MaskedIpv6) ? "-" : report.Network.MaskedIpv6)
                    : (string.IsNullOrWhiteSpace(report.Network.Ipv6) ? "-" : report.Network.Ipv6);
                string v6Suffix = isMasked ? Lang.DiagnosticTool_Report_Ipv4MaskedSuffix : Lang.DiagnosticTool_Report_Ipv4PlainSuffix;
                ipv6Text = $"{v6Display}{v6Suffix}";
            }
            else
            {
                ipv6Text = Lang.DiagnosticTool_Ipv6NotDetected;
            }
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkIpv6}: {ipv6Text}");

            string proxyText = report.Network.HasSystemProxy
                ? string.Format(Lang.DiagnosticTool_ProxyEnabled, report.Network.SystemProxyServer)
                : Lang.DiagnosticTool_ProxyDisabled;
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkProxy}: {proxyText}");

            string netDoh = report.Network.LauncherDohEnabled ? Lang.DiagnosticTool_On : Lang.DiagnosticTool_Off;
            string netEch = report.Network.LauncherEchEnabled ? Lang.DiagnosticTool_On : Lang.DiagnosticTool_Off;
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkEncryption}: DoH={netDoh} [{report.Network.LauncherDohProvider}], ECH={netEch}");

            string tierText = string.IsNullOrWhiteSpace(report.Network.SuccessfulTier) ? Lang.DiagnosticTool_Report_TierNone : report.Network.SuccessfulTier;
            sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_ProbingTier, tierText)}");

            if (report.Network.DohEchRescueAttempted)
            {
                string rescueState = report.Network.DohEchRescueSuccess ? Lang.DiagnosticTool_DohRescue_Success : Lang.DiagnosticTool_DohRescue_Failed;
                string rescueDetails = GetDohRescueDetails(report.Network);
                sb.AppendLine($"  - {string.Format(Lang.DiagnosticTool_Report_DohEchRescue, rescueState, rescueDetails)}");
            }
            string conclusion = GetDiagnosisConclusion(report.Network);
            sb.AppendLine($"  - {Lang.DiagnosticTool_NetworkConclusion}: {conclusion}");
            sb.AppendLine();
        }

        // 5. 游戏与注入状态
        sb.AppendLine(Lang.DiagnosticTool_Report_SecGames);
        if (report.Games.Count > 0)
        {
            foreach (var game in report.Games)
            {
                string gameName = new GameBiz(game.Biz).ToGameName();
                if (string.IsNullOrWhiteSpace(gameName)) gameName = game.GameName;
                string serverName = new GameBiz(game.Biz).ToGameServerName();
                if (string.IsNullOrWhiteSpace(serverName)) serverName = game.ServerName;

                sb.AppendLine($"  * {gameName} ({serverName} / {game.Biz})");
                sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_InstallPath, game.InstallPath)}");

                var opts = new List<string>();
                opts.Add($"{Lang.DiagnosticTool_Report_LaunchGame}={game.EnableGameLaunch}");
                if (game.UseStarwardLauncher) opts.Add("Starward=True");
                opts.Add($"HoYoShade={game.UseHoYoShade}");
                opts.Add($"OpenHoYoShade={game.UseOpenHoYoShade}");
                if (game.Biz.StartsWith(GameBiz.hk4e, StringComparison.OrdinalIgnoreCase))
                {
                    opts.Add($"{Lang.DiagnosticTool_Report_GenshinBlender}={game.LaunchGenshinBlenderPlugin}");
                }
                else if (game.Biz.StartsWith(GameBiz.nap, StringComparison.OrdinalIgnoreCase))
                {
                    opts.Add($"{Lang.DiagnosticTool_Report_ZZZBlender}={game.LaunchZZZBlenderPlugin}");
                }
                if (game.UsePopupWindow) opts.Add($"{Lang.DiagnosticTool_Report_BorderlessWindow}=True");
                sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_LaunchOptions, string.Join(", ", opts))}");
                if (!string.IsNullOrWhiteSpace(game.StartArgument))
                {
                    sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_LaunchArguments, game.StartArgument)}");
                }

                sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_Dx12Status, game.EnableDX12, game.IgnoreDX12Check)}");
                var files = new List<string>();
                if (game.HasDxgiDll) files.Add($"dxgi.dll {Lang.DiagnosticTool_Report_FilePresent}");
                if (game.HasD3d11Dll) files.Add($"d3d11.dll {Lang.DiagnosticTool_Report_FilePresent}");
                if (game.HasReShadeIni) files.Add($"ReShade.ini {Lang.DiagnosticTool_Report_FilePresent}");
                if (game.HasReShadeLog) files.Add($"ReShade.log {Lang.DiagnosticTool_Report_FilePresent}");
                string filesStr = files.Count > 0 ? string.Join(", ", files) : Lang.DiagnosticTool_Report_NoInjectionFiles;
                sb.AppendLine($"    - {string.Format(Lang.DiagnosticTool_Report_InjectionFiles, filesStr)}");
            }
        }
        else
        {
            sb.AppendLine($"  {Lang.DiagnosticTool_NoGamesFound}");
        }
        sb.AppendLine();

        // 6. 最近日志
        sb.AppendLine(Lang.DiagnosticTool_Report_SecLogs);
        sb.AppendLine("--------------------------------------------------------------------------------");
        if (report.RecentLogSnippets.Count > 0)
        {
            foreach (var line in report.RecentLogSnippets)
            {
                sb.AppendLine(line);
            }
        }
        else
        {
            sb.AppendLine($"  {Lang.DiagnosticTool_Report_NoLogs}");
        }
        sb.AppendLine("================================================================================");

        return sb.ToString();
    }

    public static async Task ExportZipAsync(DiagnosticReport report, string zipPath)
    {
        string? tempZip = Path.Combine(Path.GetTempPath(), $"HoYoShade_Diag_{Guid.NewGuid():N}.tmp");
        try
        {
            using (var zipStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // 1. diagnostic_report.txt
                var textEntry = archive.CreateEntry("diagnostic_report.txt", CompressionLevel.Optimal);
                using (var entryStream = textEntry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    await writer.WriteAsync(ToFormattedText(report));
                }

                // 2. diagnostic_report.json
                var jsonEntry = archive.CreateEntry("diagnostic_report.json", CompressionLevel.Optimal);
                using (var entryStream = jsonEntry.Open())
                using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var exportReport = report;
                    if (AppConfig.DiagnosticIpMasking && report.Network != null)
                    {
                        var serialized = JsonSerializer.Serialize(report, options);
                        exportReport = JsonSerializer.Deserialize<DiagnosticReport>(serialized, options) ?? report;
                        if (!string.IsNullOrWhiteSpace(exportReport.Network.MaskedIpv4))
                        {
                            exportReport.Network.Ipv4 = exportReport.Network.MaskedIpv4;
                        }
                        if (!string.IsNullOrWhiteSpace(exportReport.Network.MaskedIpv6))
                        {
                            exportReport.Network.Ipv6 = exportReport.Network.MaskedIpv6;
                        }
                    }
                    await writer.WriteAsync(JsonSerializer.Serialize(exportReport, options));
                }

                // 3. logs folder
                var logFolder = Path.Combine(AppConfig.CacheFolder, "log");
                if (Directory.Exists(logFolder))
                {
                    var logFiles = Directory.GetFiles(logFolder, "*.log")
                                            .Select(f => new FileInfo(f))
                                            .OrderByDescending(f => f.LastWriteTime)
                                            .Take(5);

                    foreach (var logFile in logFiles)
                    {
                        try
                        {
                            var logEntry = archive.CreateEntry($"logs/{logFile.Name}", CompressionLevel.Optimal);
                            string logText;
                            using (var fs = new FileStream(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            using (var reader = new StreamReader(fs, Encoding.UTF8))
                            {
                                logText = await reader.ReadToEndAsync();
                            }
                            logText = Sanitize(logText);
                            using (var entryStream = logEntry.Open())
                            using (var writer = new StreamWriter(entryStream, Encoding.UTF8))
                            {
                                await writer.WriteAsync(logText);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to include log file {File} in diagnostic package", logFile.Name);
                        }
                    }
                }
            }

            string? targetFolder = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrWhiteSpace(targetFolder) && !Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            File.Move(tempZip, zipPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                text = Regex.Replace(text, Regex.Escape(userProfile), @"C:\Users\<USER>", RegexOptions.IgnoreCase);
            }

            string userName = Environment.UserName;
            if (!string.IsNullOrEmpty(userName) && userName.Length > 1)
            {
                text = Regex.Replace(text, @"(?<=Users\\)[^\\]+", "<USER>", RegexOptions.IgnoreCase);
            }

            if (!string.IsNullOrEmpty(AppConfig.stoken))
            {
                text = text.Replace(AppConfig.stoken, "******");
            }
            if (!string.IsNullOrEmpty(AppConfig.mid))
            {
                text = text.Replace(AppConfig.mid, "******");
            }
        }
        catch { }

        return text;
    }

    private static (string total, string avail) GetMemoryInfo()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref mem))
            {
                double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double availGb = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                return ($"{totalGb:F1} GB", $"{availGb:F1} GB");
            }
        }
        catch { }
        return ("Unknown", "Unknown");
    }

    private static (string name, int physicalCores, int logicalCores) GetCpuDetails()
    {
        string name = GetCpuName();
        int physicalCores = 0;
        int logicalCores = Environment.ProcessorCount;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                string? wmiName = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(wmiName))
                {
                    name = wmiName;
                }
                if (obj["NumberOfCores"] != null)
                {
                    physicalCores += Convert.ToInt32(obj["NumberOfCores"]);
                }
            }
        }
        catch { }

        if (physicalCores == 0) physicalCores = logicalCores;
        return (name, physicalCores, logicalCores);
    }

    private static string GetMotherboardInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                string mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                string prod = obj["Product"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(mfg)) return prod;
                if (string.IsNullOrWhiteSpace(prod)) return mfg;
                if (prod.Contains(mfg, StringComparison.OrdinalIgnoreCase)) return prod;
                return $"{mfg} {prod}".Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Win32_BaseBoard");
        }
        return "Unknown Motherboard";
    }

    private static (string summary, List<MemoryStickInfo> sticks) GetDetailedMemoryInfo(string totalPhys, string availPhys)
    {
        var sticks = new List<MemoryStickInfo>();
        string memType = "DDR4";
        uint maxSpeed = 0;
        ulong totalCapacityBytes = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType, PartNumber FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get())
            {
                ulong cap = 0;
                if (obj["Capacity"] != null)
                {
                    cap = Convert.ToUInt64(obj["Capacity"]);
                    totalCapacityBytes += cap;
                }

                uint speed = 0;
                if (obj["ConfiguredClockSpeed"] != null && Convert.ToUInt32(obj["ConfiguredClockSpeed"]) > 0)
                {
                    speed = Convert.ToUInt32(obj["ConfiguredClockSpeed"]);
                }
                else if (obj["Speed"] != null)
                {
                    speed = Convert.ToUInt32(obj["Speed"]);
                }
                if (speed > maxSpeed) maxSpeed = speed;

                if (obj["SMBIOSMemoryType"] != null)
                {
                    uint typeNum = Convert.ToUInt32(obj["SMBIOSMemoryType"]);
                    if (typeNum > 0)
                    {
                        memType = GetSmbiosMemoryType(typeNum);
                    }
                }

                double stickGb = cap / (1024.0 * 1024.0 * 1024.0);
                string capStr = stickGb >= 1.0 ? $"{Math.Round(stickGb):F0}GB" : $"{cap / (1024 * 1024)}MB";
                string part = obj["PartNumber"]?.ToString()?.Trim() ?? "";

                sticks.Add(new MemoryStickInfo
                {
                    Capacity = capStr,
                    Speed = speed > 0 ? $"{speed}MHz" : "",
                    MemoryType = memType,
                    PartNumber = part
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Win32_PhysicalMemory");
        }

        if (sticks.Count > 0)
        {
            double totalGb = totalCapacityBytes / (1024.0 * 1024.0 * 1024.0);
            string totalGbStr = $"{Math.Round(totalGb):F0}GB";
            string speedStr = maxSpeed > 0 ? $" {maxSpeed}MHz" : "";
            string breakdown = string.Join(" + ", sticks.Select(s => s.Capacity));
            string summary = $"{totalGbStr} {memType}{speedStr} ( {breakdown} )";
            return (summary, sticks);
        }

        return ($"{totalPhys} (Available: {availPhys})", sticks);
    }

    private static string GetSmbiosMemoryType(uint type)
    {
        return type switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "DDR4",
            31 => "LPDDR4",
            32 => "HBM",
            33 => "HBM2",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => "DDR"
        };
    }

    private static List<DiskDiagnosticInfo> GetDiskInfo()
    {
        var list = new List<DiskDiagnosticInfo>();
        try
        {
            // 1. Map Disk Index to Drive Letters via Win32_LogicalDiskToPartition
            var diskDrives = new Dictionary<int, List<string>>();
            try
            {
                using var assocSearcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
                foreach (var item in assocSearcher.Get())
                {
                    string? ant = item["Antecedent"]?.ToString();
                    string? dep = item["Dependent"]?.ToString();
                    if (string.IsNullOrEmpty(ant) || string.IsNullOrEmpty(dep)) continue;

                    var antMatch = Regex.Match(ant, @"Disk\s*#(?<disk>\d+)", RegexOptions.IgnoreCase);
                    var depMatch = Regex.Match(dep, @"DeviceID\s*=\s*""(?<drive>[A-Za-z]:)""", RegexOptions.IgnoreCase);

                    if (antMatch.Success && depMatch.Success && int.TryParse(antMatch.Groups["disk"].Value, out int diskIdx))
                    {
                        string drive = depMatch.Groups["drive"].Value.ToUpperInvariant();
                        if (!diskDrives.TryGetValue(diskIdx, out var drives))
                        {
                            drives = [];
                            diskDrives[diskIdx] = drives;
                        }
                        if (!drives.Contains(drive))
                        {
                            drives.Add(drive);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Win32_LogicalDiskToPartition");
            }

            // 2. Query Win32_DiskDrive
            using var diskSearcher = new ManagementObjectSearcher("SELECT Index, Model, Size, MediaType, InterfaceType FROM Win32_DiskDrive");
            foreach (var obj in diskSearcher.Get())
            {
                int index = obj["Index"] != null ? Convert.ToInt32(obj["Index"]) : -1;
                string model = obj["Model"]?.ToString()?.Trim() ?? "Unknown Disk";
                ulong sizeBytes = obj["Size"] != null ? Convert.ToUInt64(obj["Size"]) : 0;
                string mediaType = obj["MediaType"]?.ToString()?.Trim() ?? "";
                string iface = obj["InterfaceType"]?.ToString()?.Trim() ?? "";

                // TBToolbox uses decimal GB (Size / 1,000,000,000)
                ulong decimalGb = (ulong)Math.Round(sizeBytes / 1_000_000_000.0);
                string sizeStr = sizeBytes > 0 ? $"{decimalGb}GB" : "Unknown Size";

                List<string> drives = [];
                if (index >= 0 && diskDrives.TryGetValue(index, out var foundDrives))
                {
                    drives = foundDrives.OrderBy(d => d).ToList();
                }

                list.Add(new DiskDiagnosticInfo
                {
                    Index = index,
                    Model = model,
                    SizeString = sizeStr,
                    TotalSizeBytes = sizeBytes,
                    MediaType = mediaType,
                    InterfaceType = iface,
                    DriveLetters = drives
                });
            }

            list = list.OrderBy(d => d.Index).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Win32_DiskDrive");
        }

        return list;
    }

    private static List<MonitorDiagnosticInfo> GetMonitorInfo()
    {
        var list = new List<MonitorDiagnosticInfo>();
        try
        {
            // 1. Get sizes from WmiMonitorBasicDisplayParams
            var monitorSizes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var paramSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams");
                foreach (var p in paramSearcher.Get())
                {
                    string? inst = p["InstanceName"]?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(inst)) continue;

                    int w = p["MaxHorizontalImageSize"] != null ? Convert.ToInt32(p["MaxHorizontalImageSize"]) : 0;
                    int h = p["MaxVerticalImageSize"] != null ? Convert.ToInt32(p["MaxVerticalImageSize"]) : 0;
                    if (w > 0 && h > 0)
                    {
                        double diagInches = Math.Sqrt(w * w + h * h) / 2.54;
                        string diagStr = diagInches % 1.0 < 0.1 || diagInches % 1.0 > 0.9 ? $"{Math.Round(diagInches):F0}\"" : $"{diagInches:F1}\"";
                        monitorSizes[inst] = diagStr;
                    }
                }
            }
            catch { }

            // 2. Get Monitor ID info
            using var idSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, UserFriendlyName, ProductCodeID, ManufacturerName, SerialNumberID FROM WmiMonitorID");
            foreach (var m in idSearcher.Get())
            {
                string inst = m["InstanceName"]?.ToString()?.Trim() ?? "";

                string name = DecodeWmiUShortArray(m["UserFriendlyName"] as ushort[]);
                string code = DecodeWmiUShortArray(m["ProductCodeID"] as ushort[]);
                string mfg = DecodeWmiUShortArray(m["ManufacturerName"] as ushort[]);
                string serial = DecodeWmiUShortArray(m["SerialNumberID"] as ushort[]);

                string diagSize = "";
                if (!string.IsNullOrEmpty(inst))
                {
                    foreach (var kvp in monitorSizes)
                    {
                        if (inst.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase) || kvp.Key.StartsWith(inst, StringComparison.OrdinalIgnoreCase))
                        {
                            diagSize = kvp.Value;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(code))
                {
                    list.Add(new MonitorDiagnosticInfo
                    {
                        Name = string.IsNullOrWhiteSpace(name) ? "Generic Monitor" : name,
                        ModelCode = code,
                        Manufacturer = mfg,
                        DiagonalSize = diagSize,
                        SerialNumber = serial
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query WmiMonitorID");
        }

        return list;
    }

    private static string DecodeWmiUShortArray(ushort[]? array)
    {
        if (array == null || array.Length == 0) return string.Empty;
        var chars = array.Where(c => c != 0).Select(c => (char)c).ToArray();
        return new string(chars).Trim();
    }

    private static List<string> GetAudioDevices()
    {
        var list = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Status FROM Win32_SoundDevice");
            foreach (var obj in searcher.Get())
            {
                string? status = obj["Status"]?.ToString();
                string? name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && (status == null || status.Equals("OK", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Win32_SoundDevice");
        }
        return list;
    }

    private static List<string> GetNetworkAdapters()
    {
        var list = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, PhysicalAdapter FROM Win32_NetworkAdapter");
            foreach (var obj in searcher.Get())
            {
                bool isPhysical = obj["PhysicalAdapter"] != null && Convert.ToBoolean(obj["PhysicalAdapter"]);
                string? name = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (isPhysical && !Regex.IsMatch(name, @"Virtual|WAN|Miniport|Bluetooth|Tunnel|TAP|Pcap|Pseudo", RegexOptions.IgnoreCase))
                {
                    if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(name);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Win32_NetworkAdapter");
        }
        return list;
    }

    private static List<GpuDiagnosticInfo> GetGpuInfo()
    {
        var list = new List<GpuDiagnosticInfo>();
        try
        {
            using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey != null)
            {
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    if (subKeyName.StartsWith("Properties") || !int.TryParse(subKeyName, out _))
                        continue;

                    using var subKey = classKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    string? desc = subKey.GetValue("DriverDesc")?.ToString();
                    if (string.IsNullOrWhiteSpace(desc)) continue;

                    string version = subKey.GetValue("DriverVersion")?.ToString() ?? "";
                    string date = subKey.GetValue("DriverDate")?.ToString() ?? "";
                    string provider = subKey.GetValue("ProviderName")?.ToString() ?? "";

                    string vramStr = "";
                    object? qwMem = subKey.GetValue("HardwareInformation.qwMemorySize");
                    if (qwMem is long qwLong && qwLong > 0)
                    {
                        double gb = (ulong)qwLong / (1024.0 * 1024.0 * 1024.0);
                        vramStr = gb >= 1.0 ? $"{gb:F1} GB" : $"{((ulong)qwLong / (1024.0 * 1024.0)):F0} MB";
                    }
                    else if (subKey.GetValue("HardwareInformation.MemorySize") is int memInt && memInt > 0)
                    {
                        double gb = (uint)memInt / (1024.0 * 1024.0 * 1024.0);
                        vramStr = gb >= 1.0 ? $"{gb:F1} GB" : $"{((uint)memInt / (1024.0 * 1024.0)):F0} MB";
                    }

                    // Check for board vendor from MatchingDeviceId or subkey
                    string matchingId = subKey.GetValue("MatchingDeviceId")?.ToString() ?? "";
                    string boardVendor = GetVendorFromSubsystem(matchingId);
                    if (string.IsNullOrEmpty(boardVendor))
                    {
                        boardVendor = provider;
                    }

                    list.Add(new GpuDiagnosticInfo
                    {
                        Name = desc,
                        DriverVersion = version,
                        DriverDate = date,
                        Provider = boardVendor,
                        VramSize = vramStr
                    });
                }
            }
        }
        catch { }
        return list;
    }

    private static string GetVendorFromSubsystem(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return string.Empty;
        var match = Regex.Match(deviceId, @"SUBSYS_[0-9A-Fa-f]{4}(?<vendor>[0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            string subVendor = match.Groups["vendor"].Value.ToUpperInvariant();
            return subVendor switch
            {
                "1043" => "ASUS",
                "1462" => "MSI",
                "1458" => "GIGABYTE",
                "3842" => "EVGA",
                "10DE" => "NVIDIA",
                "8086" => "Intel",
                "1002" => "AMD",
                "1569" => "Palit / Colorful",
                "1DA2" => "Sapphire",
                "1EAE" => "Colorful",
                "7377" => "Colorful",
                "1849" => "ASRock",
                "19DA" => "Zotac",
                "1B4C" => "GALAX",
                "1025" => "Acer",
                "1028" => "Dell",
                "103C" => "HP",
                "17AA" => "Lenovo",
                _ => string.Empty
            };
        }
        return string.Empty;
    }

    private static string GetCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown CPU";
        }
        catch
        {
            return "Unknown CPU";
        }
    }

    private static (string name, string displayVer, string build) GetOsInfo()
    {
        string name = "Windows";
        string displayVer = "";
        string build = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                name = key.GetValue("ProductName")?.ToString() ?? "Windows";
                displayVer = key.GetValue("DisplayVersion")?.ToString() ?? "";
                var currentBuild = key.GetValue("CurrentBuild")?.ToString() ?? Environment.OSVersion.Version.Build.ToString();
                var ubr = key.GetValue("UBR")?.ToString();
                build = string.IsNullOrEmpty(ubr) ? currentBuild : $"{currentBuild}.{ubr}";
            }
        }
        catch
        {
            build = Environment.OSVersion.Version.ToString();
        }
        return (name, displayVer, build);
    }

    private static string GetWebView2Version()
    {
        try
        {
            return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            return "Not detected";
        }
    }

    private static string GetSystemUptime()
    {
        try
        {
            var ts = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static List<GameDiagnosticInfo> GetGameInstallations()
    {
        var result = new List<GameDiagnosticInfo>();
        try
        {
            var candidateBizs = new List<GameBiz>();
            var seenBizs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Games currently added/selected in the launcher (in order)
            if (!string.IsNullOrWhiteSpace(AppConfig.SelectedGameBizs))
            {
                var selected = AppConfig.SelectedGameBizs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var s in selected)
                {
                    if (seenBizs.Add(s))
                    {
                        candidateBizs.Add(new GameBiz(s));
                    }
                }
            }

            // 2. Current active game in launcher
            GameBiz currentBiz = AppConfig.CurrentGameBiz;
            if (!string.IsNullOrWhiteSpace(currentBiz.Value) && seenBizs.Add(currentBiz.Value))
            {
                candidateBizs.Add(currentBiz);
            }

            // 3. Any other known games that have configured paths in AppConfig
            foreach (var biz in GameBiz.AllGameBizs)
            {
                if (string.IsNullOrWhiteSpace(biz.Value) || seenBizs.Contains(biz.Value))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(AppConfig.GetGameInstallPath(biz)) ||
                    !string.IsNullOrWhiteSpace(AppConfig.GetGameInstallPaths(biz)))
                {
                    seenBizs.Add(biz.Value);
                    candidateBizs.Add(biz);
                }
            }

            foreach (var biz in candidateBizs)
            {
                if (string.IsNullOrWhiteSpace(biz.Value)) continue;

                var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Primary path resolved via GameLauncherService
                try
                {
                    string? launcherPath = GameLauncherService.GetGameInstallPath(biz);
                    if (!string.IsNullOrWhiteSpace(launcherPath))
                    {
                        candidatePaths.Add(launcherPath.Trim());
                    }
                }
                catch { }

                // 2. Multiple paths configured in AppConfig
                string? rawPaths = AppConfig.GetGameInstallPaths(biz);
                if (!string.IsNullOrWhiteSpace(rawPaths))
                {
                    var paths = rawPaths.Split(new[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var p in paths)
                    {
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            candidatePaths.Add(p.Trim());
                        }
                    }
                }

                // 3. Single path configured in AppConfig (saved by auto search or individual selector)
                string? singlePath = AppConfig.GetGameInstallPath(biz);
                if (!string.IsNullOrWhiteSpace(singlePath))
                {
                    candidatePaths.Add(singlePath.Trim());
                }

                // Query launch options for this game client
                var gameId = GameId.FromGameBiz(biz) ?? new GameId { Id = biz.Value, GameBiz = biz };
                bool enableGameLaunch = AppConfig.GetEnableGameLaunchOption(gameId);
                bool useStarward = AppConfig.GetUseStarwardLaunchOption(gameId);
                bool useHoYoShade = AppConfig.GetUseHoYoShadeLaunchOption(gameId);
                bool useOpenHoYoShade = AppConfig.GetUseOpenHoYoShadeLaunchOption(gameId);
                bool launchGenshinBlender = AppConfig.GetLaunchGenshinBlenderPluginOption(gameId);
                bool launchZZZBlender = AppConfig.GetLaunchZZZBlenderPluginOption(gameId);
                bool usePopupWindow = AppConfig.GetUsePopupWindow(biz);
                string? startArgument = AppConfig.GetStartArgument(biz);

                if (useHoYoShade && useOpenHoYoShade)
                {
                    useOpenHoYoShade = false;
                }
                if (enableGameLaunch && useStarward)
                {
                    useStarward = false;
                }
                if (launchGenshinBlender || launchZZZBlender)
                {
                    enableGameLaunch = false;
                    useStarward = false;
                }

                var visitedNormalizedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rawPath in candidatePaths)
                {
                    try
                    {
                        string p = GameLauncherService.GetFullPathIfRelativePath(rawPath);
                        bool exists = Directory.Exists(p);
                        if (!exists && !AppConfig.GetGameInstallPathRemovable(biz))
                        {
                            continue;
                        }

                        string normalized = exists ? Path.GetFullPath(p).TrimEnd('\\', '/') : p.TrimEnd('\\', '/');
                        if (!visitedNormalizedPaths.Add(normalized))
                        {
                            continue;
                        }

                        bool enableDx12 = AppConfig.GetEnableDX12(biz);
                        bool ignoreDx12Check = AppConfig.GetIgnoreDX12Check(biz);

                        bool hasDxgi = exists && File.Exists(Path.Combine(normalized, "dxgi.dll"));
                        bool hasD3d11 = exists && File.Exists(Path.Combine(normalized, "d3d11.dll"));
                        bool hasReShadeIni = exists && File.Exists(Path.Combine(normalized, "ReShade.ini"));
                        bool hasReShadeLog = exists && File.Exists(Path.Combine(normalized, "ReShade.log"));

                        string gameName = biz.ToGameName();
                        if (string.IsNullOrWhiteSpace(gameName)) gameName = biz.Value;

                        string serverName = biz.ToGameServerName();
                        if (string.IsNullOrWhiteSpace(serverName)) serverName = biz.Server;

                        result.Add(new GameDiagnosticInfo
                        {
                            Biz = biz.Value,
                            GameName = gameName,
                            ServerName = serverName,
                            InstallPath = Sanitize(normalized),
                            EnableGameLaunch = enableGameLaunch,
                            UseStarwardLauncher = useStarward,
                            UseHoYoShade = useHoYoShade,
                            UseOpenHoYoShade = useOpenHoYoShade,
                            LaunchGenshinBlenderPlugin = launchGenshinBlender,
                            LaunchZZZBlenderPlugin = launchZZZBlender,
                            UsePopupWindow = usePopupWindow,
                            StartArgument = Sanitize(startArgument),
                            EnableDX12 = enableDx12,
                            IgnoreDX12Check = ignoreDx12Check,
                            HasDxgiDll = hasDxgi,
                            HasD3d11Dll = hasD3d11,
                            HasReShadeIni = hasReShadeIni,
                            HasReShadeLog = hasReShadeLog
                        });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect game installations");
        }
        return result;
    }

    private static async Task<FrameworkFlavorDiagnosticInfo> CollectFrameworkFlavorInfoAsync(string name, string folderName, HoYoShadeVersionManifest manifest)
    {
        var info = new FrameworkFlavorDiagnosticInfo { Name = name };
        if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
        {
            info.Version = Lang.WelcomeView_NotInstalled;
            info.ReShadeVersion = "-";
            return info;
        }

        string fullPath = Path.Combine(AppConfig.UserDataFolder, folderName);
        info.InstallPath = Sanitize(fullPath);

        if (!Directory.Exists(fullPath))
        {
            info.IsInstalled = false;
            info.Version = Lang.WelcomeView_NotInstalled;
            info.ReShadeVersion = "-";
            return info;
        }

        info.IsInstalled = true;

        // Version from manifest
        string? manifestVer = name switch
        {
            "HoYoShade" => manifest.HoYoShade?.Version,
            "OpenHoYoShade" => manifest.OpenHoYoShade?.Version,
            _ => null
        };
        info.Version = !string.IsNullOrWhiteSpace(manifestVer) ? manifestVer : "Unknown";

        // ReShade version from ReShade64.dll
        string reshadeDll = Path.Combine(fullPath, "ReShade64.dll");
        info.ReShadeVersion = GetDllProductVersion(reshadeDll);

        // Calculate sizes asynchronously
        try
        {
            long total = await GetFolderSizeLongAsync(fullPath);
            long shaders = 0;
            long presets = 0;
            long screenshots = 0;

            string shadersPath = Path.Combine(fullPath, "reshade-shaders");
            if (Directory.Exists(shadersPath))
            {
                shaders = await GetFolderSizeLongAsync(shadersPath);
            }

            string presetsPath = Path.Combine(fullPath, "Presets");
            if (Directory.Exists(presetsPath))
            {
                presets = await GetFolderSizeLongAsync(presetsPath);
            }

            string screenshotsPath = Path.Combine(fullPath, "Screenshots");
            if (Directory.Exists(screenshotsPath))
            {
                screenshots = await GetFolderSizeLongAsync(screenshotsPath);
            }

            long other = Math.Max(0, total - shaders - presets - screenshots);

            info.TotalSize = FormatSize(total);
            info.ShaderSize = FormatSize(shaders);
            info.PresetSize = FormatSize(presets);
            info.ScreenshotSize = FormatSize(screenshots);
            info.OtherSize = FormatSize(other);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate storage size for {Name}", name);
        }

        return info;
    }

    public static string GetFrameworkDownloadServerName(int serverIndex)
    {
        return serverIndex switch
        {
            -1 => Lang.HoYoShadeDownloadView_Server_AutoSelect,
            0 => Lang.HoYoShadeDownloadView_Server_GithubDirect,
            1 => AppConfig.EnableEch ? "Cloudflare ECH" : Lang.HoYoShadeDownloadView_Server_Cloudflare,
            2 => Lang.HoYoShadeDownloadView_Server_TencentCloud,
            3 => Lang.HoYoShadeDownloadView_Server_AlibabaCloud,
            _ => Lang.HoYoShadeDownloadView_Server_AutoSelect
        };
    }

    private static string GetDllProductVersion(string dllPath)
    {
        try
        {
            if (!File.Exists(dllPath))
            {
                return "-";
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(dllPath);
            return !string.IsNullOrWhiteSpace(versionInfo.ProductVersion) ? versionInfo.ProductVersion : "-";
        }
        catch
        {
            return "-";
        }
    }

    private static async Task<long> GetFolderSizeLongAsync(string folder)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }
            }
            catch
            {
            }
            return 0L;
        });
    }

    private static string FormatSize(long bytes)
    {
        const long KB = 1024;
        const long MB = 1024 * 1024;
        const long GB = 1024 * 1024 * 1024;

        if (bytes < KB)
        {
            return $"{bytes:F2} B";
        }
        else if (bytes < MB)
        {
            return $"{bytes / (double)KB:F2} KB";
        }
        else if (bytes < GB)
        {
            return $"{bytes / (double)MB:F2} MB";
        }
        else
        {
            return $"{bytes / (double)GB:F2} GB";
        }
    }

    private static bool CheckRpcRunning()
    {
        try
        {
            return RpcService.CheckRpcServerRunning();
        }
        catch
        {
            return false;
        }
    }

    private static List<string> GetRecentLogLines(int maxLines = 250)
    {
        var lines = new List<string>();
        try
        {
            var logFolder = Path.Combine(AppConfig.CacheFolder, "log");
            if (!Directory.Exists(logFolder)) return lines;

            var latestLog = Directory.GetFiles(logFolder, "*.log")
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.LastWriteTime)
                                     .FirstOrDefault();
            if (latestLog == null) return lines;

            using var fs = new FileStream(latestLog.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            var allLines = new List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                allLines.Add(line);
            }

            int skip = Math.Max(0, allLines.Count - maxLines);
            foreach (var l in allLines.Skip(skip))
            {
                lines.Add(Sanitize(l));
            }
        }
        catch (Exception ex)
        {
            lines.Add($"[Error reading logs: {ex.Message}]");
        }
        return lines;
    }

    #region Network Diagnostic Helpers

    public static string MaskIpAddress(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return "-";
        ip = ip.Trim();
        if (IPAddress.TryParse(ip, out var addr))
        {
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var parts = ip.Split('.');
                if (parts.Length == 4)
                {
                    return $"{parts[0]}.{parts[1]}.***.***";
                }
            }
            else if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                var parts = ip.Split(':');
                if (parts.Length >= 3)
                {
                    return $"{parts[0]}:{parts[1]}:****:****:****";
                }
            }
        }
        return ip;
    }

    public static string FormatCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        code = code.Trim().ToUpperInvariant();
        var culture = Lang.Culture ?? System.Globalization.CultureInfo.CurrentUICulture;
        bool isZhCn = culture.Name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase);
        bool isZhTw = culture.Name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase);
        bool isZhHk = culture.Name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase);
        bool isZh = isZhCn || isZhTw || isZhHk;

        if (!isZh)
        {
            return code switch
            {
                "CN" => "China (CN)",
                "HK" => "Hong Kong (HK)",
                "MO" => "Macao (MO)",
                "TW" => "Taiwan (TW)",
                "US" => "United States (US)",
                "JP" => "Japan (JP)",
                "KR" => "South Korea (KR)",
                "SG" => "Singapore (SG)",
                "GB" or "UK" => "United Kingdom (GB)",
                "DE" => "Germany (DE)",
                "CA" => "Canada (CA)",
                "AU" => "Australia (AU)",
                "RU" => "Russia (RU)",
                "FR" => "France (FR)",
                "NL" => "Netherlands (NL)",
                "IN" => "India (IN)",
                _ => FormatCountryFallback(code, false)
            };
        }
        else if (isZhTw || isZhHk)
        {
            return code switch
            {
                "CN" => "中國 (CN)",
                "HK" => "中國香港 (HK)",
                "MO" => "中國澳門 (MO)",
                "TW" => "中國台灣 (TW)",
                "US" => "美國 (US)",
                "JP" => "日本 (JP)",
                "KR" => "韓國 (KR)",
                "SG" => "新加坡 (SG)",
                "GB" or "UK" => "英國 (GB)",
                "DE" => "德國 (DE)",
                "CA" => "加拿大 (CA)",
                "AU" => "澳大利亞 (AU)",
                "RU" => "俄羅斯 (RU)",
                "FR" => "法國 (FR)",
                "NL" => "荷蘭 (NL)",
                "IN" => "印度 (IN)",
                _ => FormatCountryFallback(code, true)
            };
        }
        else
        {
            return code switch
            {
                "CN" => "中国 (CN)",
                "HK" => "中国香港 (HK)",
                "MO" => "中国澳门 (MO)",
                "TW" => "中国台湾 (TW)",
                "US" => "美国 (US)",
                "JP" => "日本 (JP)",
                "KR" => "韩国 (KR)",
                "SG" => "新加坡 (SG)",
                "GB" or "UK" => "英国 (GB)",
                "DE" => "德国 (DE)",
                "CA" => "加拿大 (CA)",
                "AU" => "澳大利亚 (AU)",
                "RU" => "俄罗斯 (RU)",
                "FR" => "法国 (FR)",
                "NL" => "荷兰 (NL)",
                "IN" => "印度 (IN)",
                _ => FormatCountryFallback(code, true)
            };
        }
    }

    private static string FormatCountryFallback(string code, bool isZh)
    {
        try
        {
            if (code.Length == 2)
            {
                var reg = new System.Globalization.RegionInfo(code);
                string name = isZh ? reg.DisplayName : reg.EnglishName;
                return $"{name} ({reg.TwoLetterISORegionName})";
            }
        }
        catch { }
        return code;
    }

    public static async Task<NetworkDiagnosticInfo> CollectNetworkDiagnosticInfoAsync(bool forceDohEch = false)
    {
        var info = new NetworkDiagnosticInfo
        {
            LauncherDohEnabled = DohService.Enabled,
            LauncherDohProvider = DohService.Provider.ToString(),
            LauncherEchEnabled = DohService.EnableEch
        };

        // 1. Detect System Proxy
        try
        {
            var targetUri = new Uri("https://hoyosha.de/");
            var proxy = HttpClient.DefaultProxy.GetProxy(targetUri);
            if (proxy != null && proxy != targetUri)
            {
                info.HasSystemProxy = true;
                info.SystemProxyServer = proxy.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check system proxy during network diagnosis.");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        if (forceDohEch)
        {
            await ExecuteDohEchRescueAsync(info, cts.Token);
            return info;
        }

        // Phase 1: Native network probe (Direct / no DoH)
        try
        {
            using var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(1),
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            using var directClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };

            var ipv6Task = DetectIpv6Async(directClient, info, cts.Token);

            bool tier1Success = await TryTier1CloudflareMetaAsync(directClient, info, cts.Token);
            if (!tier1Success)
            {
                _logger.LogInformation("Network diag: Tier 1 failed/timed out. Falling back to Tier 2 (icanhazip + ip-api).");
                bool tier2Success = await TryTier2IcanhazipAndIpApiAsync(directClient, info, cts.Token);
                if (!tier2Success)
                {
                    _logger.LogInformation("Network diag: Tier 2 failed. Falling back to Tier 3 (Cloudflare Trace).");
                    bool tier3Success = await TryTier3CloudflareTraceAsync(directClient, info, cts.Token);
                    if (tier3Success)
                    {
                        info.DirectConnectionSuccess = true;
                    }
                }
                else
                {
                    info.DirectConnectionSuccess = true;
                }
            }
            else
            {
                info.DirectConnectionSuccess = true;
            }

            try
            {
                await ipv6Task;
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Network diag: native direct probing encountered error.");
            info.DirectConnectionSuccess = false;
            info.DirectConnectionError = ex.Message;
        }

        // Phase 2: If native direct probing failed on all tiers, test DoH + ECH rescue!
        if (!info.DirectConnectionSuccess)
        {
            _logger.LogInformation("Network diag: all direct tiers failed. Starting DoH + ECH rescue probe...");
            await ExecuteDohEchRescueAsync(info, cts.Token);
        }
        else
        {
            info.DiagnosisConclusion = Lang.DiagnosticTool_Conclusion_DirectSuccess;
        }

        return info;
    }

    private static async Task<bool> TryTier1CloudflareMetaAsync(HttpClient client, NetworkDiagnosticInfo info, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://speed.cloudflare.com/meta");
            request.Headers.Referrer = new Uri("https://speed.cloudflare.com/");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HoYoShadeHub");

            using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return false;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("clientIp", out var ipElem)) return false;

            string? ip = ipElem.GetString();
            if (string.IsNullOrWhiteSpace(ip)) return false;

            info.Ipv4 = ip;
            info.MaskedIpv4 = MaskIpAddress(ip);

            if (root.TryGetProperty("country", out var countryElem))
            {
                string c = countryElem.GetString() ?? "";
                info.CountryCode = c;
                info.Country = FormatCountry(c);
            }
            if (root.TryGetProperty("region", out var regElem)) info.Region = regElem.GetString() ?? "";
            if (root.TryGetProperty("city", out var cityElem)) info.City = cityElem.GetString() ?? "";

            if (root.TryGetProperty("asn", out var asnElem))
            {
                if (asnElem.ValueKind == JsonValueKind.Number)
                {
                    info.Asn = $"AS{asnElem.GetInt64()}";
                }
                else if (asnElem.ValueKind == JsonValueKind.String)
                {
                    string s = asnElem.GetString() ?? "";
                    info.Asn = s.StartsWith("AS", StringComparison.OrdinalIgnoreCase) ? s : $"AS{s}";
                }
            }

            if (root.TryGetProperty("asOrganization", out var orgElem))
            {
                info.AsOrganization = orgElem.GetString() ?? "";
            }

            if (root.TryGetProperty("colo", out var coloElem) && coloElem.TryGetProperty("iata", out var iataElem))
            {
                info.CloudflareColo = iataElem.GetString() ?? "";
            }

            info.SuccessfulTier = "Cloudflare Speed Meta (Tier 1)";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryTier2IcanhazipAndIpApiAsync(HttpClient client, NetworkDiagnosticInfo info, CancellationToken ct)
    {
        try
        {
            string? ip = null;
            // 1. Fetch IP from icanhazip (Cloudflare-owned)
            try
            {
                using var req1 = new HttpRequestMessage(HttpMethod.Get, "https://icanhazip.com");
                req1.Headers.UserAgent.ParseAdd("curl/8.0.0");
                using var resp1 = await client.SendAsync(req1, ct);
                if (resp1.IsSuccessStatusCode)
                {
                    string raw = (await resp1.Content.ReadAsStringAsync(ct)).Trim();
                    if (Regex.IsMatch(raw, @"^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$"))
                    {
                        ip = raw;
                    }
                }
            }
            catch { }

            // Fallback to api.ipify.org if icanhazip failed
            if (string.IsNullOrWhiteSpace(ip))
            {
                try
                {
                    using var req2 = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
                    req2.Headers.UserAgent.ParseAdd("curl/8.0.0");
                    using var resp2 = await client.SendAsync(req2, ct);
                    if (resp2.IsSuccessStatusCode)
                    {
                        string raw = (await resp2.Content.ReadAsStringAsync(ct)).Trim();
                        if (Regex.IsMatch(raw, @"^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$"))
                        {
                            ip = raw;
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(ip)) return false;

            info.Ipv4 = ip;
            info.MaskedIpv4 = MaskIpAddress(ip);

            // 2. Query ip-api.com with localization
            try
            {
                var culture = Lang.Culture ?? System.Globalization.CultureInfo.CurrentUICulture;
                string langParam = culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
                using var reqApi = new HttpRequestMessage(HttpMethod.Get, $"http://ip-api.com/json/{ip}?lang={langParam}");
                using var respApi = await client.SendAsync(reqApi, ct);
                if (respApi.IsSuccessStatusCode)
                {
                    string json = await respApi.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                    {
                        if (root.TryGetProperty("country", out var cElem)) info.Country = cElem.GetString() ?? "";
                        if (root.TryGetProperty("countryCode", out var ccElem)) info.CountryCode = ccElem.GetString() ?? "";
                        if (root.TryGetProperty("regionName", out var rElem)) info.Region = rElem.GetString() ?? "";
                        if (root.TryGetProperty("city", out var cityElem)) info.City = cityElem.GetString() ?? "";
                        if (root.TryGetProperty("as", out var asElem)) info.Asn = asElem.GetString() ?? "";
                        if (root.TryGetProperty("isp", out var ispElem)) info.AsOrganization = ispElem.GetString() ?? "";
                    }
                }
            }
            catch { }

            info.SuccessfulTier = "icanhazip + ip-api.com (Tier 2)";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryTier3CloudflareTraceAsync(HttpClient client, NetworkDiagnosticInfo info, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.cloudflare.com/cdn-cgi/trace");
            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return false;

            string trace = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(trace)) return false;

            var lines = trace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? ip = null;
            string? loc = null;
            string? colo = null;
            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line[..eq].Trim();
                string v = line[(eq + 1)..].Trim();
                if (k.Equals("ip", StringComparison.OrdinalIgnoreCase)) ip = v;
                else if (k.Equals("loc", StringComparison.OrdinalIgnoreCase)) loc = v;
                else if (k.Equals("colo", StringComparison.OrdinalIgnoreCase)) colo = v;
            }

            if (string.IsNullOrWhiteSpace(ip)) return false;

            info.Ipv4 = ip;
            info.MaskedIpv4 = MaskIpAddress(ip);
            if (!string.IsNullOrWhiteSpace(loc))
            {
                info.CountryCode = loc;
                info.Country = FormatCountry(loc);
            }
            if (!string.IsNullOrWhiteSpace(colo)) info.CloudflareColo = colo;

            info.SuccessfulTier = "Cloudflare Trace (Tier 3)";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task DetectIpv6Async(HttpClient client, NetworkDiagnosticInfo info, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2.5));

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://ipv6.icanhazip.com");
            req.Headers.UserAgent.ParseAdd("curl/8.0.0");
            using var resp = await client.SendAsync(req, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                string raw = (await resp.Content.ReadAsStringAsync(cts.Token)).Trim();
                if (raw.Contains(':') && IPAddress.TryParse(raw, out var addr) && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    info.HasIpv6 = true;
                    info.Ipv6 = raw;
                    info.MaskedIpv6 = MaskIpAddress(raw);
                }
            }
        }
        catch
        {
            info.HasIpv6 = false;
        }
    }

    private static async Task ExecuteDohEchRescueAsync(NetworkDiagnosticInfo info, CancellationToken ct)
    {
        info.DohEchRescueAttempted = true;
        bool origEnabled = DohService.Enabled;
        bool origEch = DohService.EnableEch;
        var origProvider = DohService.Provider;

        try
        {
            DohService.Enabled = true;
            DohService.EnableEch = true;
            DohService.Provider = DohProvider.Cloudflare;

            using var handler = DohService.CreateSocketsHttpHandler();
            using var rescueClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };

            bool success = await TryTier1CloudflareMetaAsync(rescueClient, info, ct);
            if (!success)
            {
                success = await TryTier3CloudflareTraceAsync(rescueClient, info, ct);
            }

            if (success)
            {
                info.DohEchRescueSuccess = true;
                info.DohEchRescueDetails = Lang.DiagnosticTool_DohRescueDetails_Success;
                info.DiagnosisConclusion = Lang.DiagnosticTool_Conclusion_DohRescueSuccess;
            }
            else
            {
                info.DohEchRescueSuccess = false;
                info.DohEchRescueDetails = Lang.DiagnosticTool_DohRescueDetails_Failed;
                info.DiagnosisConclusion = Lang.DiagnosticTool_Conclusion_AllFailed;
            }
        }
        catch (Exception ex)
        {
            info.DohEchRescueSuccess = false;
            info.DohEchRescueDetails = string.Format(Lang.DiagnosticTool_DohRescueDetails_Exception, ex.Message);
            info.DiagnosisConclusion = string.Format(Lang.DiagnosticTool_Conclusion_Exception, ex.Message);
        }
        finally
        {
            DohService.Enabled = origEnabled;
            DohService.EnableEch = origEch;
            DohService.Provider = origProvider;
        }
    }

    #endregion
}
