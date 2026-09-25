using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Features.RPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
    public int LogicalCores { get; set; }
    public string TotalPhysicalMemory { get; set; } = string.Empty;
    public string AvailablePhysicalMemory { get; set; } = string.Empty;
    public List<GpuDiagnosticInfo> Gpus { get; set; } = [];
    public string PrimaryResolution { get; set; } = string.Empty;
    public string DisplayDpiScale { get; set; } = string.Empty;
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
}

public class GameDiagnosticInfo
{
    public string Biz { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
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
                var gpus = GetGpuInfo();
                string resolution = $"{User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN)} x {User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN)}";
                
                string dpiScale = "100%";
                if (windowHandle != 0)
                {
                    double scale = User32.GetDpiForWindow(windowHandle) / 96.0;
                    dpiScale = $"{scale:P0}";
                }

                report.Hardware = new HardwareDiagnosticInfo
                {
                    CpuName = GetCpuName(),
                    LogicalCores = Environment.ProcessorCount,
                    TotalPhysicalMemory = totalMem,
                    AvailablePhysicalMemory = availMem,
                    Gpus = gpus,
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

            // 3. Launcher Info
            try
            {
                var (hoyoshadeVer, openHoyoshadeVer) = await GetFrameworkVersionsAsync();
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
                    HoYoShadeFrameworkVersion = hoyoshadeVer,
                    OpenHoYoShadeVersion = openHoyoshadeVer
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

        // 硬件与显示
        sb.AppendLine("【1. 硬件与显示配置 (Hardware & Displays)】");
        sb.AppendLine($"  - 处理器 (CPU): {report.Hardware.CpuName} ({report.Hardware.LogicalCores} 逻辑核心)");
        sb.AppendLine($"  - 物理内存 (RAM): 总计 {report.Hardware.TotalPhysicalMemory} (可用: {report.Hardware.AvailablePhysicalMemory})");
        if (report.Hardware.Gpus.Count > 0)
        {
            for (int i = 0; i < report.Hardware.Gpus.Count; i++)
            {
                var gpu = report.Hardware.Gpus[i];
                var vramInfo = string.IsNullOrWhiteSpace(gpu.VramSize) ? "" : $", 显存: {gpu.VramSize}";
                var verInfo = string.IsNullOrWhiteSpace(gpu.DriverVersion) ? "" : $", 驱动: {gpu.DriverVersion}";
                var dateInfo = string.IsNullOrWhiteSpace(gpu.DriverDate) ? "" : $" ({gpu.DriverDate})";
                sb.AppendLine($"  - 显卡 {i + 1} (GPU): {gpu.Name}{vramInfo}{verInfo}{dateInfo}");
            }
        }
        else
        {
            sb.AppendLine("  - 显卡 (GPU): 未能从注册表获取显卡详细信息");
        }
        sb.AppendLine($"  - 主屏幕分辨率: {report.Hardware.PrimaryResolution} (缩放: {report.Hardware.DisplayDpiScale})");
        sb.AppendLine();

        // 操作系统与运行环境
        sb.AppendLine("【2. 操作系统与运行环境 (OS & Runtime)】");
        sb.AppendLine($"  - 操作系统: {report.System.OsName} {report.System.OsVersion} (Build {report.System.OsBuild})");
        sb.AppendLine($"  - 架构规格: 系统 {report.System.OsArchitecture} / 进程 {report.System.ProcessArchitecture}");
        sb.AppendLine($"  - .NET 运行时: {report.System.DotNetRuntime}");
        sb.AppendLine($"  - WebView2 运行时: {report.System.WebView2Version}");
        sb.AppendLine($"  - 系统开机运行时间: {report.System.SystemUptime}");
        sb.AppendLine();

        // 启动器与框架状态
        sb.AppendLine("【3. 启动器与组件状态 (Launcher & Framework)】");
        sb.AppendLine($"  - 启动器版本: {report.Launcher.Version} (PID: {report.Launcher.ProcessId})");
        sb.AppendLine($"  - 运行权限: {(report.Launcher.IsAdmin ? "管理员权限 (Administrator)" : "普通用户权限 (Standard User)")}");
        sb.AppendLine($"  - 运行模式: 便携模式={report.Launcher.IsPortable}, 移动存储={report.Launcher.IsRemovableStorage}");
        sb.AppendLine($"  - 更新通道: {report.Launcher.UpdateChannel}");
        sb.AppendLine($"  - HoYoShade 核心: {report.Launcher.HoYoShadeFrameworkVersion}");
        sb.AppendLine($"  - OpenHoYoShade: {report.Launcher.OpenHoYoShadeVersion}");
        sb.AppendLine($"  - RPC 后台服务: {(report.Launcher.RpcRunning ? "正在运行 (Running)" : "未运行 (Not Running)")}");
        sb.AppendLine($"  - 网络加密: DoH={(report.Launcher.DohEnabled ? "开启" : "关闭")} [{report.Launcher.DohProvider}], ECH={(report.Launcher.EchEnabled ? "开启" : "关闭")}");
        sb.AppendLine($"  - 下载节点: {report.Launcher.DownloadServer}");
        sb.AppendLine($"  - 程序主目录: {report.Launcher.BaseDirectory}");
        sb.AppendLine($"  - 用户数据目录: {report.Launcher.UserDataFolder}");
        sb.AppendLine($"  - 缓存文件目录: {report.Launcher.CacheFolder}");
        sb.AppendLine();

        // 游戏与注入状态
        sb.AppendLine("【4. 已安装游戏与注入配置 (Configured Games & Injections)】");
        if (report.Games.Count > 0)
        {
            foreach (var game in report.Games)
            {
                sb.AppendLine($"  * {game.GameName} ({game.ServerName} / {game.Biz})");
                sb.AppendLine($"    - 安装路径: {game.InstallPath}");
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

        // 最近日志
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

                    list.Add(new GpuDiagnosticInfo
                    {
                        Name = desc,
                        DriverVersion = version,
                        DriverDate = date,
                        Provider = provider,
                        VramSize = vramStr
                    });
                }
            }
        }
        catch { }
        return list;
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
            foreach (var biz in GameBiz.AllGameBizs)
            {
                string? rawPaths = AppConfig.GetGameInstallPaths(biz);
                if (string.IsNullOrWhiteSpace(rawPaths)) continue;

                var paths = rawPaths.Split(new[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

                    bool enableDx12 = AppConfig.GetEnableDX12(biz);
                    bool ignoreDx12Check = AppConfig.GetIgnoreDX12Check(biz);

                    bool hasDxgi = File.Exists(Path.Combine(path, "dxgi.dll"));
                    bool hasD3d11 = File.Exists(Path.Combine(path, "d3d11.dll"));
                    bool hasReShadeIni = File.Exists(Path.Combine(path, "ReShade.ini"));
                    bool hasReShadeLog = File.Exists(Path.Combine(path, "ReShade.log"));

                    result.Add(new GameDiagnosticInfo
                    {
                        Biz = biz.Value,
                        GameName = biz.ToGameName(),
                        ServerName = biz.ToGameServerName(),
                        InstallPath = Sanitize(path),
                        EnableDX12 = enableDx12,
                        IgnoreDX12Check = ignoreDx12Check,
                        HasDxgiDll = hasDxgi,
                        HasD3d11Dll = hasD3d11,
                        HasReShadeIni = hasReShadeIni,
                        HasReShadeLog = hasReShadeLog
                    });
                }
            }
        }
        catch { }
        return result;
    }

    private static async Task<(string hoyoshade, string openhoyoshade)> GetFrameworkVersionsAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
            {
                return ("Not installed", "Not installed");
            }
            var versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
            var manifest = await versionService.LoadManifestAsync();
            return (manifest.HoYoShade?.Version ?? "Not installed", manifest.OpenHoYoShade?.Version ?? "Not installed");
        }
        catch
        {
            return ("Unknown", "Unknown");
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
