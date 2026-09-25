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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace HoYoShadeHub.Features.Toolbox;

public class DiagnosticReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public HardwareDiagnosticInfo Hardware { get; set; } = new();
    public SystemDiagnosticInfo System { get; set; } = new();
    public LauncherDiagnosticInfo Launcher { get; set; } = new();
    public List<GameDiagnosticInfo> Games { get; set; } = [];
    public List<string> RecentLogSnippets { get; set; } = [];
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

    public static async Task<DiagnosticReport> CollectReportAsync(nint windowHandle = 0)
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

            // 4. Games
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

    public static string ToFormattedText(DiagnosticReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                 HoYoShade Hub 诊断报告 (Diagnostic Report)                      ");
        sb.AppendLine($"生成时间 (Generated At): {report.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("* 隐私说明: 涉及的用户名与私有路径已自动去识别化脱敏处理");
        sb.AppendLine("================================================================================");
        sb.AppendLine();

        // 1. 本机硬件配置单
        sb.AppendLine("【1. 本机硬件配置单 (Hardware Configuration)】");
        string coreText = report.Hardware.PhysicalCores > 0
            ? $"{report.Hardware.PhysicalCores} Cores / {report.Hardware.LogicalCores} Threads"
            : $"{report.Hardware.LogicalCores} Cores";
        sb.AppendLine($"  - 处理器 (CPU): {report.Hardware.CpuName} ({coreText})");
        sb.AppendLine($"  - 主板型号 (Motherboard): {(string.IsNullOrWhiteSpace(report.Hardware.Motherboard) ? "-" : report.Hardware.Motherboard)}");
        sb.AppendLine($"  - 物理内存 (RAM): {report.Hardware.MemorySummary} [Total: {report.Hardware.TotalPhysicalMemory} / Avail: {report.Hardware.AvailablePhysicalMemory}]");

        if (report.Hardware.Gpus.Count > 0)
        {
            sb.AppendLine("  - 显卡与驱动 (GPU):");
            for (int i = 0; i < report.Hardware.Gpus.Count; i++)
            {
                var gpu = report.Hardware.Gpus[i];
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(gpu.VramSize)) parts.Add(gpu.VramSize);
                if (!string.IsNullOrWhiteSpace(gpu.DriverVersion)) parts.Add($"Driver: {gpu.DriverVersion}");
                if (!string.IsNullOrWhiteSpace(gpu.Provider)) parts.Add(gpu.Provider);
                string extra = parts.Count > 0 ? $" ({string.Join(" / ", parts)})" : "";
                sb.AppendLine($"      * {gpu.Name}{extra}");
            }
        }
        else
        {
            sb.AppendLine("  - 显卡与驱动 (GPU): -");
        }

        if (report.Hardware.Monitors.Count > 0)
        {
            sb.AppendLine("  - 显示器设备 (Monitors):");
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
            sb.AppendLine($"      * Primary: {report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})");
        }
        else
        {
            sb.AppendLine($"  - 屏幕分辨率 (Display): {report.Hardware.PrimaryResolution} ({report.Hardware.DisplayDpiScale})");
        }

        if (report.Hardware.Disks.Count > 0)
        {
            sb.AppendLine("  - 存储磁盘与对应盘符 (Disks & Drive Letters):");
            foreach (var disk in report.Hardware.Disks)
            {
                string drives = disk.DriveLetters.Count > 0 ? string.Join(", ", disk.DriveLetters) : "-";
                sb.AppendLine($"      * {disk.Model} ( {disk.SizeString} ) -> [ {drives} ]");
            }
        }
        else
        {
            sb.AppendLine("  - 存储磁盘与对应盘符: -");
        }

        if (report.Hardware.AudioDevices.Count > 0)
        {
            sb.AppendLine("  - 声卡设备 (Audio):");
            foreach (var audio in report.Hardware.AudioDevices)
            {
                sb.AppendLine($"      * {audio}");
            }
        }

        if (report.Hardware.NetworkAdapters.Count > 0)
        {
            sb.AppendLine("  - 网卡设备 (Network):");
            foreach (var net in report.Hardware.NetworkAdapters)
            {
                sb.AppendLine($"      * {net}");
            }
        }
        sb.AppendLine();

        // 2. 操作系统与运行环境
        sb.AppendLine("【2. 操作系统与运行环境 (OS & Runtime)】");
        sb.AppendLine($"  - 操作系统: {report.System.OsName} {report.System.OsVersion} (Build {report.System.OsBuild})");
        sb.AppendLine($"  - 架构规格: 系统 {report.System.OsArchitecture} / 进程 {report.System.ProcessArchitecture}");
        sb.AppendLine($"  - .NET 运行时: {report.System.DotNetRuntime}");
        sb.AppendLine($"  - WebView2 运行时: {report.System.WebView2Version}");
        sb.AppendLine($"  - 系统开机运行时间: {report.System.SystemUptime}");
        sb.AppendLine();

        // 3. 启动器与框架状态
        sb.AppendLine("【3. 启动器与框架状态 (Launcher & Framework)】");
        sb.AppendLine($"  - 启动器版本: {report.Launcher.Version} (PID: {report.Launcher.ProcessId})");
        sb.AppendLine($"  - 运行权限: {(report.Launcher.IsAdmin ? "管理员权限 (Administrator)" : "普通用户权限 (Standard User)")}");
        sb.AppendLine($"  - 运行模式: 便携模式={report.Launcher.IsPortable}, 移动存储={report.Launcher.IsRemovableStorage}");
        sb.AppendLine($"  - 更新通道: {report.Launcher.UpdateChannel}");
        sb.AppendLine($"  - RPC 后台服务: {(report.Launcher.RpcRunning ? "正在运行 (Running)" : "未运行 (Not Running)")}");
        sb.AppendLine($"  - 网络加密: DoH={(report.Launcher.DohEnabled ? "开启" : "关闭")} [{report.Launcher.DohProvider}], ECH={(report.Launcher.EchEnabled ? "开启" : "关闭")}");
        sb.AppendLine($"  - 启动器下载节点: {report.Launcher.DownloadServer}");
        sb.AppendLine($"  - 程序主目录: {report.Launcher.BaseDirectory}");
        sb.AppendLine($"  - 用户数据目录: {report.Launcher.UserDataFolder}");
        sb.AppendLine($"  - 缓存文件目录: {report.Launcher.CacheFolder}");
        sb.AppendLine();
        sb.AppendLine("  [HoYoShade 框架通用设置]");
        sb.AppendLine($"  - 框架下载服务器: {report.Launcher.FrameworkDownloadServer}");
        sb.AppendLine($"  - 加入预览版更新通道: {(report.Launcher.FrameworkPreviewChannel ? "开启 (Enabled)" : "关闭 (Disabled)")}");
        sb.AppendLine();
        sb.AppendLine("  * HoYoShade:");
        sb.AppendLine($"    - 状态: {(report.Launcher.HoYoShade.IsInstalled ? "已安装 (Installed)" : "未安装 (Not Installed)")}");
        if (report.Launcher.HoYoShade.IsInstalled)
        {
            sb.AppendLine($"    - 框架版本: {report.Launcher.HoYoShade.Version}");
            sb.AppendLine($"    - ReShade版本: {report.Launcher.HoYoShade.ReShadeVersion}");
            sb.AppendLine($"    - 安装路径: {report.Launcher.HoYoShade.InstallPath}");
            sb.AppendLine($"    - 存储总占用: {report.Launcher.HoYoShade.TotalSize}");
            sb.AppendLine($"    - 着色器及插件占用: {report.Launcher.HoYoShade.ShaderSize}");
            sb.AppendLine($"    - 预设占用: {report.Launcher.HoYoShade.PresetSize}");
            sb.AppendLine($"    - 截图占用: {report.Launcher.HoYoShade.ScreenshotSize}");
            sb.AppendLine($"    - 其他内容占用: {report.Launcher.HoYoShade.OtherSize}");
        }
        sb.AppendLine();
        sb.AppendLine("  * OpenHoYoShade:");
        sb.AppendLine($"    - 状态: {(report.Launcher.OpenHoYoShade.IsInstalled ? "已安装 (Installed)" : "未安装 (Not Installed)")}");
        if (report.Launcher.OpenHoYoShade.IsInstalled)
        {
            sb.AppendLine($"    - 框架版本: {report.Launcher.OpenHoYoShade.Version}");
            sb.AppendLine($"    - ReShade版本: {report.Launcher.OpenHoYoShade.ReShadeVersion}");
            sb.AppendLine($"    - 安装路径: {report.Launcher.OpenHoYoShade.InstallPath}");
            sb.AppendLine($"    - 存储总占用: {report.Launcher.OpenHoYoShade.TotalSize}");
            sb.AppendLine($"    - 着色器及插件占用: {report.Launcher.OpenHoYoShade.ShaderSize}");
            sb.AppendLine($"    - 预设占用: {report.Launcher.OpenHoYoShade.PresetSize}");
            sb.AppendLine($"    - 截图占用: {report.Launcher.OpenHoYoShade.ScreenshotSize}");
            sb.AppendLine($"    - 其他内容占用: {report.Launcher.OpenHoYoShade.OtherSize}");
        }
        sb.AppendLine();

        // 4. 游戏与注入状态
        sb.AppendLine("【4. 已安装游戏与注入配置 (Configured Games & Injections)】");
        if (report.Games.Count > 0)
        {
            foreach (var game in report.Games)
            {
                sb.AppendLine($"  * {game.GameName} ({game.ServerName} / {game.Biz})");
                sb.AppendLine($"    - 安装路径: {game.InstallPath}");

                var opts = new List<string>();
                opts.Add($"启动游戏={game.EnableGameLaunch}");
                if (game.UseStarwardLauncher) opts.Add("Starward=True");
                opts.Add($"HoYoShade={game.UseHoYoShade}");
                opts.Add($"OpenHoYoShade={game.UseOpenHoYoShade}");
                if (game.Biz.StartsWith(GameBiz.hk4e, StringComparison.OrdinalIgnoreCase))
                {
                    opts.Add($"原神Blender插件={game.LaunchGenshinBlenderPlugin}");
                }
                else if (game.Biz.StartsWith(GameBiz.nap, StringComparison.OrdinalIgnoreCase))
                {
                    opts.Add($"绝区零Blender插件={game.LaunchZZZBlenderPlugin}");
                }
                if (game.UsePopupWindow) opts.Add("无边框窗口=True");
                sb.AppendLine($"    - 启动选项: {string.Join(", ", opts)}");
                if (!string.IsNullOrWhiteSpace(game.StartArgument))
                {
                    sb.AppendLine($"    - 启动参数: {game.StartArgument}");
                }

                sb.AppendLine($"    - DX12 状态: 启用={game.EnableDX12}, 忽略检查={game.IgnoreDX12Check}");
                var files = new List<string>();
                if (game.HasDxgiDll) files.Add("dxgi.dll [存在]");
                if (game.HasD3d11Dll) files.Add("d3d11.dll [存在]");
                if (game.HasReShadeIni) files.Add("ReShade.ini [存在]");
                if (game.HasReShadeLog) files.Add("ReShade.log [存在]");
                string filesStr = files.Count > 0 ? string.Join(", ", files) : "未检测到核心注入文件";
                sb.AppendLine($"    - 注入文件检测: {filesStr}");
            }
        }
        else
        {
            sb.AppendLine("  (尚未配置任何游戏安装路径)");
        }
        sb.AppendLine();

        // 5. 最近日志
        sb.AppendLine("【5. 最近本地运行日志摘要 (Recent Launcher Logs)】");
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
            sb.AppendLine("  (暂无可用日志内容)");
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
                    await writer.WriteAsync(JsonSerializer.Serialize(report, options));
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

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            File.Move(tempZip, zipPath);
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
}
