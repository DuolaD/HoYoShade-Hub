using Microsoft.Extensions.Logging;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Features.GameSetting;
using HoYoShadeHub.Features.HoYoPlay;
using HoYoShadeHub.Features.PlayTime;
using HoYoShadeHub.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.GameLauncher;

internal partial class GameLauncherService
{


    private readonly ILogger<GameLauncherService> _logger;


    private readonly HoYoPlayService _hoYoPlayService;

    private readonly PlayTimeService _playTimeService;


    public GameLauncherService(ILogger<GameLauncherService> logger, HoYoPlayService hoYoPlayService, PlayTimeService playTimeService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _playTimeService = playTimeService;
    }





    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId)
    {
        return GetGameInstallPath(gameId.GameBiz);
    }


    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameBiz gameBiz)
    {
        // 首先尝试获取多路径配置
        var paths = AppConfig.GetGameInstallPaths(gameBiz);
        if (!string.IsNullOrWhiteSpace(paths))
        {
            var pathList = paths.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var selectedIndex = AppConfig.GetSelectedGameInstallPathIndex(gameBiz);
            
            if (pathList.Length > 0 && selectedIndex >= 0 && selectedIndex < pathList.Length)
            {
                var selectedPath = pathList[selectedIndex].Trim();
                selectedPath = GetFullPathIfRelativePath(selectedPath);
                if (Directory.Exists(selectedPath))
                {
                    return Path.GetFullPath(selectedPath);
                }
            }
        }

        // 回退到单一路径配置
        var path = AppConfig.GetGameInstallPath(gameBiz);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        path = GetFullPathIfRelativePath(path);
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }
        else if (AppConfig.GetGameInstallPathRemovable(gameBiz))
        {
            return path;
        }
        else
        {
            ChangeGameInstallPath(gameBiz, null);
            return null;
        }
    }



    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="storageRemoved">可移动存储设备已移除</param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId, out bool storageRemoved)
    {
        storageRemoved = false;

        // 首先尝试获取多路径配置
        var paths = AppConfig.GetGameInstallPaths(gameId.GameBiz);
        if (!string.IsNullOrWhiteSpace(paths))
        {
            var pathList = paths.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var selectedIndex = AppConfig.GetSelectedGameInstallPathIndex(gameId.GameBiz);
            
            if (pathList.Length > 0 && selectedIndex >= 0 && selectedIndex < pathList.Length)
            {
                var selectedPath = pathList[selectedIndex].Trim();
                selectedPath = GetFullPathIfRelativePath(selectedPath);
                if (Directory.Exists(selectedPath))
                {
                    return selectedPath;
                }
                else if (AppConfig.GetGameInstallPathRemovable(gameId.GameBiz))
                {
                    storageRemoved = true;
                    return selectedPath;
                }
            }
        }

        // 回退到单一路径配置
        var path = AppConfig.GetGameInstallPath(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        path = GetFullPathIfRelativePath(path);
        if (Directory.Exists(path))
        {
            return path;
        }
        else if (AppConfig.GetGameInstallPathRemovable(gameId.GameBiz))
        {
            storageRemoved = true;
            return path;
        }
        else
        {
            ChangeGameInstallPath(gameId, null);
            return null;
        }
    }



    /// <summary>
    /// 本地游戏版本
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<Version?> GetLocalGameVersionAsync(GameId gameId, string? installPath = null)
    {
        return await GetLocalGameVersionAsync(gameId.GameBiz, installPath);
    }



    /// <summary>
    /// 本地游戏版本
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<Version?> GetLocalGameVersionAsync(GameBiz gameBiz, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameBiz);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }
        var config = Path.Join(installPath, "config.ini");
        if (File.Exists(config))
        {
            var str = await File.ReadAllTextAsync(config);
            var matches = GameVersionRegex().Matches(str);
            Version? version = null;
            if (matches.Count > 0)
            {
                _ = Version.TryParse(matches[^1].Groups[1].Value, out version);
            }
            return version;
        }
        else
        {
            _logger.LogWarning("config.ini not found: {path}", config);
            return null;
        }
    }


    [GeneratedRegex(@"game_version=(.+)")]
    private static partial Regex GameVersionRegex();



    /// <summary>
    /// 最新游戏版本
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <returns></returns>
    public async Task<(Version? Latest, Version? Predownload)> GetLatestGameVersionAsync(GameId gameId)
    {
        GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId);
        if (config is null)
        {
            throw new ArgumentOutOfRangeException($"Game config is null ({gameId.Id}, {gameId.GameBiz}).");
        }
        if (config.DefaultDownloadMode is DownloadMode.DOWNLOAD_MODE_CHUNK or DownloadMode.DOWNLOAD_MODE_LDIFF)
        {
            GameBranch? gameBranch = await _hoYoPlayService.GetGameBranchAsync(gameId);
            if (gameBranch is null)
            {
                throw new ArgumentOutOfRangeException($"Game branch is null ({gameId.Id}, {gameId.GameBiz}).");
            }
            _ = Version.TryParse(gameBranch.Main.Tag, out Version? latestVersion);
            _ = Version.TryParse(gameBranch.PreDownload?.Tag, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
        else
        {
            GamePackage package = await _hoYoPlayService.GetGamePackageAsync(gameId);
            _ = Version.TryParse(package.Main.Major?.Version, out Version? latestVersion);
            _ = Version.TryParse(package.PreDownload.Major?.Version, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
    }




    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<string> GetGameExeNameAsync(GameId gameId)
    {
        string? name = GetGameExeName(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(name))
        {
            var config = await _hoYoPlayService.GetGameConfigAsync(gameId);
            name = config?.ExeFileName;
        }
        return name ?? throw new ArgumentOutOfRangeException($"Unknown game ({gameId.Id}, {gameId.GameBiz}).");
    }



    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameExeName(GameBiz gameBiz)
    {
        string? name = gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => "YuanShen.exe",
            GameBiz.hk4e_global => "GenshinImpact.exe",
            GameBiz.nap_beta_prebeta => "ZZZ.exe",
            GameBiz.nap_beta_postbeta => "ZenlessZoneZeroBeta.exe",
            _ => gameBiz.Game switch
            {
                GameBiz.hkrpg => "StarRail.exe",
                GameBiz.bh3 => "BH3.exe",
                GameBiz.nap => "ZenlessZoneZero.exe",
                _ => null,
            },
        };
        return name;
    }



    /// <summary>
    /// 游戏进程文件是否存在
    /// </summary>
    /// <param name="biz"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<bool> IsGameExeExistsAsync(GameId gameId, string? installPath = null)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            var exe = Path.Join(installPath, await GetGameExeNameAsync(gameId));
            return File.Exists(exe);
        }
        return false;
    }



    /// <summary>
    /// 获取游戏进程
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<Process?> GetGameProcessAsync(GameId gameId)
    {
        int currentSessionId = Process.GetCurrentProcess().SessionId;
        var name = (await GetGameExeNameAsync(gameId)).Replace(".exe", "");
        return Process.GetProcessesByName(name).Where(x => x.SessionId == currentSessionId && !IsProcessPending(x)).FirstOrDefault();
    }



    /// <summary>
    /// 检测进程挂起
    /// </summary>
    /// <param name="process"></param>
    /// <returns></returns>
    public static bool IsProcessPending(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
            foreach (ProcessThread thread in process.Threads)
            {
                if (thread.ThreadState is not ThreadState.Wait)
                {
                    return false;
                }
                else if (thread.WaitReason is not ThreadWaitReason.Suspended)
                {
                    return false;
                }
            }
            return true;
        }
        catch { }
        return true;
    }



    /// <summary>
    /// 启动游戏
    /// </summary>
    /// <returns></returns>
    public async Task<Process?> StartGameAsync(GameId gameId, string? installPath = null)
    {
        const int ERROR_CANCELLED = 0x000004C7;
        try
        {
            if (await GetGameProcessAsync(gameId) is Process existingProcess)
            {
                throw new Exception($"Game is running: {existingProcess.ProcessName}.exe ({existingProcess.Id}).");
            }
            string? exe = null, arg = null, verb = null;
            if (Directory.Exists(installPath))
            {
                var e = Path.Join(installPath, await GetGameExeNameAsync(gameId));
                if (File.Exists(e))
                {
                    exe = e;
                }
            }
            bool thirdPartyTool = false;
            if (string.IsNullOrWhiteSpace(exe) && AppConfig.GetEnableThirdPartyTool(gameId.GameBiz))
            {
                exe = GetThirdPartyToolPath(gameId);
                if (File.Exists(exe))
                {
                    thirdPartyTool = true;
                    verb = Path.GetExtension(exe) is ".exe" or ".bat" ? "runas" : "";
                }
                else
                {
                    exe = null;
                    SetThirdPartyToolPath(gameId, null);
                    _logger.LogWarning("Third party tool not found: {path}", exe);
                }
            }
            if (string.IsNullOrWhiteSpace(exe))
            {
                var folder = GetGameInstallPath(gameId);
                var name = await GetGameExeNameAsync(gameId);
                exe = Path.Join(folder, name);
                verb = "runas";
                if (!File.Exists(exe))
                {
                    _logger.LogWarning("Game exe not found: {path}", exe);
                    throw new FileNotFoundException("Game exe not found", name);
                }
            }
            arg = AppConfig.GetStartArgument(gameId.GameBiz)?.Trim();
            
            if (AppConfig.GetUsePopupWindow(gameId.GameBiz))
            {
                arg += " -popupwindow";
            }

            if (gameId.GameBiz.Game is GameBiz.hk4e)
            {
                GameSettingService.SetGenshinEnableHDR(gameId.GameBiz, AppConfig.EnableGenshinHDR);
            }
            if (!thirdPartyTool && AppConfig.StartGameWithCMD)
            {
                arg = $"""/c start "" /d "{Path.GetDirectoryName(exe)}" "{exe}" {arg}""";
                exe = "cmd.exe";
            }
            _logger.LogInformation("Start game ({biz})\r\npath: {exe}\r\narg: {arg}", gameId, exe, arg);
            var info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arg,
                UseShellExecute = true,
                Verb = verb,
                WorkingDirectory = Path.GetDirectoryName(exe),
            };
            Process? process = Process.Start(info);
            if (process != null)
            {
                if (thirdPartyTool || AppConfig.StartGameWithCMD)
                {
                    return await _playTimeService.StartProcessToLogAsync(gameId);
                }
                else
                {
                    await _playTimeService.StartProcessToLogAsync(gameId, process.Id);
                    return process;
                }
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
        {
            // Operation canceled
            _logger.LogInformation("Start game operation canceled.");
        }
        return null;
    }




    /// <summary>
    /// 修改游戏安装目录
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? ChangeGameInstallPath(GameId gameId, string? path)
    {
        return ChangeGameInstallPath(gameId.GameBiz, path);
    }


    /// <summary>
    /// 修改游戏安装目录
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? ChangeGameInstallPath(GameBiz gameBiz, string? path)
    {
        if (Directory.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            AppConfig.SetGameInstallPath(gameBiz, relativePath);
            AppConfig.SetGameInstallPathRemovable(gameBiz, removable);
        }
        else
        {
            path = null;
            AppConfig.SetGameInstallPath(gameBiz, null);
            AppConfig.SetGameInstallPathRemovable(gameBiz, false);
        }
        return path;
    }



    /// <summary>
    /// 如果安装在可移动存储设备中，获取相对路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="removableStorage"></param>
    /// <returns></returns>
    public static string GetRelativePathIfInRemovableStorage(string path, out bool removableStorage)
    {
        removableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(path);
        if (removableStorage && Path.GetPathRoot(AppConfig.HoYoShadeHubExecutePath) == Path.GetPathRoot(path))
        {
            path = Path.GetRelativePath(Path.GetDirectoryName(AppConfig.ConfigPath)!, path);
        }
        return path;
    }



    /// <summary>
    /// 如果安装在可移动存储设备中，获取完整路径
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetFullPathIfRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }
        else
        {
            return Path.GetFullPath(path, Path.GetDirectoryName(AppConfig.ConfigPath)!);
        }
    }




    /// <summary>
    /// 获取第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetThirdPartyToolPath(GameId gameId)
    {
        string? path = AppConfig.GetThirdPartyToolPath(gameId.GameBiz);
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = GetFullPathIfRelativePath(path);
        }
        if (File.Exists(path))
        {
            return path;
        }
        else
        {
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
            return null;
        }
    }


    /// <summary>
    /// 设置第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? SetThirdPartyToolPath(GameId gameId, string? path)
    {
        if (File.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, relativePath);
        }
        else
        {
            path = null;
            AppConfig.SetThirdPartyToolPath(gameId.GameBiz, null);
        }
        return path;
    }

    /// <summary>
    /// 获取游戏的所有安装路径（自动迁移单一路径）
    /// </summary>
    public static List<string> GetAllGameInstallPaths(GameBiz gameBiz)
    {
        var paths = AppConfig.GetGameInstallPaths(gameBiz);
        var pathList = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(paths))
        {
            pathList = paths.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
        }
        
        // 如果多目录列表为空，尝试从单一路径配置迁移
        if (pathList.Count == 0)
        {
            var singlePath = AppConfig.GetGameInstallPath(gameBiz);
            if (!string.IsNullOrWhiteSpace(singlePath))
            {
                pathList.Add(singlePath.Trim());
                // 自动保存到多目录配置
                AppConfig.SetGameInstallPaths(gameBiz, singlePath.Trim());
                AppConfig.SetSelectedGameInstallPathIndex(gameBiz, 0);
            }
        }
        
        return pathList;
    }

    /// <summary>
    /// 添加游戏安装路径（智能模式：自动迁移单一路径）
    /// </summary>
    public static void AddGameInstallPath(GameBiz gameBiz, string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        path = Path.GetFullPath(path);
        string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
        
        var existingPaths = GetAllGameInstallPaths(gameBiz);
        
        // 如果多目录列表为空，检查是否有单一路径配置需要迁移
        if (existingPaths.Count == 0)
        {
            var singlePath = AppConfig.GetGameInstallPath(gameBiz);
            if (!string.IsNullOrWhiteSpace(singlePath))
            {
                // 迁移单一路径到多目录列表
                existingPaths.Add(singlePath.Trim());
            }
        }
        
        // 检查是否已存在（不区分大小写比较）
        var fullExistingPaths = existingPaths.Select(p => {
            var fullPath = GetFullPathIfRelativePath(p);
            return Path.GetFullPath(fullPath);
        }).ToList();
        
        var fullNewPath = Path.GetFullPath(path);
        
        if (!fullExistingPaths.Contains(fullNewPath, StringComparer.OrdinalIgnoreCase))
        {
            existingPaths.Add(relativePath);
            AppConfig.SetGameInstallPaths(gameBiz, string.Join("|", existingPaths));
            
            // 如果只有1个路径（即刚刚添加的第一个路径），同步设置到单路径配置中，确保回退逻辑和 UI 刷新正常工作
            if (existingPaths.Count == 1)
            {
                AppConfig.SetGameInstallPath(gameBiz, relativePath);
                AppConfig.SetGameInstallPathRemovable(gameBiz, removable);
                AppConfig.SetSelectedGameInstallPathIndex(gameBiz, 0);
            }
            // 如果这是添加的第二个目录（总数变2），设置默认选中索引为0（维持原有路径为选中状态）
            else if (existingPaths.Count == 2)
            {
                AppConfig.SetSelectedGameInstallPathIndex(gameBiz, 0);
            }
        }
    }

    /// <summary>
    /// 移除游戏安装路径
    /// </summary>
    public static void RemoveGameInstallPath(GameBiz gameBiz, int index)
    {
        var existingPaths = GetAllGameInstallPaths(gameBiz);
        if (index >= 0 && index < existingPaths.Count)
        {
            existingPaths.RemoveAt(index);
            AppConfig.SetGameInstallPaths(gameBiz, string.Join("|", existingPaths));
            
            // 如果移除的是当前选中的路径，重置为第一个
            var selectedIndex = AppConfig.GetSelectedGameInstallPathIndex(gameBiz);
            if (selectedIndex == index)
            {
                AppConfig.SetSelectedGameInstallPathIndex(gameBiz, 0);
            }
            else if (selectedIndex > index)
            {
                AppConfig.SetSelectedGameInstallPathIndex(gameBiz, selectedIndex - 1);
            }
        }
    }

    /// <summary>
    /// 设置选中的游戏安装路径索引
    /// </summary>
    public static void SetSelectedGameInstallPathIndex(GameBiz gameBiz, int index)
    {
        var existingPaths = GetAllGameInstallPaths(gameBiz);
        if (index >= 0 && index < existingPaths.Count)
        {
            AppConfig.SetSelectedGameInstallPathIndex(gameBiz, index);
        }
    }




}
