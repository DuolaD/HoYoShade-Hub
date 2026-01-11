using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using SharpSevenZip;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Features.Database;
using HoYoShadeHub.Features.GameLauncher;
using HoYoShadeHub.Features.ViewHost;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class FileManageSetting : PageBase
{

    private readonly ILogger<FileManageSetting> _logger = AppConfig.GetLogger<FileManageSetting>();
    private readonly HoYoShadeVersionService _versionService = new(AppConfig.UserDataFolder);


    public FileManageSetting()
    {
        this.InitializeComponent();
    }
    
    /// <summary>
    /// HoYoShade框架预览版渠道
    /// </summary>
    public bool EnableHoYoShadePreviewChannel
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnableHoYoShadePreviewChannel = value;
            }
        }
    } = AppConfig.EnableHoYoShadePreviewChannel;
    
    /// <summary>
    /// HoYoShade 更新检测结果
    /// </summary>
    public string? HoYoShadeUpdateInfo { get => field; set => SetProperty(ref field, value); }
    
    /// <summary>
    /// OpenHoYoShade 更新检测结果
    /// </summary>
    public string? OpenHoYoShadeUpdateInfo { get => field; set => SetProperty(ref field, value); }



    protected override void OnLoaded()
    {
        GetLastBackupTime();
        _ = UpdateCacheSizeAsync();
        _ = UpdateHoYoShadeSizeAsync();
        _ = LoadVersionInfoAsync();
        
        // Register for installation change messages
        WeakReferenceMessenger.Default.Register<HoYoShadeInstallationChangedMessage>(this, async (r, m) => 
        {
            await UpdateHoYoShadeSizeAsync();
            await LoadVersionInfoAsync();
        });
    }




    #region 数据文件夹



    /// <summary>
    /// 修改数据文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ChangeUserDataFolderAsync()
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Lang.SettingPage_ReselectDataFolder,
                // 当前数据文件夹的位置是：
                // 想要重新选择吗？（你需要在选择前手动迁移数据文件）
                Content = $"""
                {Lang.SettingPage_TheCurrentLocationOfTheDataFolderIs}

                {AppConfig.UserDataFolder}

                {Lang.SettingPage_WouldLikeToReselectDataFolder}
                """,
                PrimaryButtonText = Lang.Common_Yes,
                SecondaryButtonText = Lang.Common_Cancel,
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result is ContentDialogResult.Primary)
            {
                AppConfig.UserDataFolder = null!;
                AppConfig.SaveConfiguration();
                AppInstance.GetCurrent().UnregisterKey();
                Process.Start(AppConfig.HoYoShadeHubExecutePath);
                App.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change data folder");
        }
    }



    /// <summary>
    /// 打开数据文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenUserDataFolderAsync()
    {
        try
        {
            if (Directory.Exists(AppConfig.UserDataFolder))
            {
                await Launcher.LaunchUriAsync(new Uri(AppConfig.UserDataFolder));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open user data folder");
        }
    }


    /// <summary>
    /// 删除所有设置
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task DeleteAllSettingAsync()
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Lang.SettingPage_DeleteAllSettings,
                // 删除完成后，将自动重启软件。
                Content = Lang.SettingPage_AfterDeletingTheSoftwareWillBeRestartedAutomatically,
                PrimaryButtonText = Lang.Common_Delete,
                SecondaryButtonText = Lang.Common_Cancel,
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                AppConfig.DeleteAllSettings();
                AppInstance.GetCurrent().UnregisterKey();
                Process.Start(AppConfig.HoYoShadeHubExecutePath);
                App.Current.Exit();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete all setting");
        }

    }



    private void TextBlock_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize -= 1;
        }
    }



    #endregion




    #region 备份数据库



    public string LastDatabaseBackupTime { get; set => SetProperty(ref field, value); }


    private void GetLastBackupTime()
    {
        try
        {
            if (DatabaseService.TryGetValue("LastBackupDatabase", out string? file, out DateTime time))
            {
                file = Path.Join(AppConfig.UserDataFolder, "DatabaseBackup", file);
                if (File.Exists(file))
                {
                    LastDatabaseBackupTime = $"{Lang.SettingPage_LastBackup}  {time:yyyy-MM-dd HH:mm:ss}";
                }
                else
                {
                    _logger.LogWarning("Last backup database file not found: {file}", file);
                }
            }
        }
        catch { }
    }



    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        try
        {
            if (Directory.Exists(AppConfig.UserDataFolder))
            {
                var folder = Path.Combine(AppConfig.UserDataFolder, "DatabaseBackup");
                Directory.CreateDirectory(folder);
                DateTime time = DateTime.Now;
                await Task.Run(() =>
                {
                    string file = Path.Combine(folder, $"HoYoShadeHubDatabase_{time:yyyyMMdd_HHmmss}.db");
                    string archive = Path.ChangeExtension(file, ".7z");
                    DatabaseService.BackupDatabase(file);
                    new SharpSevenZipCompressor().CompressFiles(archive, file);
                    DatabaseService.SetValue("LastBackupDatabase", Path.GetFileName(archive), time);
                    File.Delete(file);
                });
                LastDatabaseBackupTime = $"{Lang.SettingPage_LastBackup}  {time:yyyy-MM-dd HH:mm:ss}";
            }
        }        
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup database");
            LastDatabaseBackupTime = ex.Message;
        }
    }



    [RelayCommand]
    private async Task OpenLastBackupDatabaseAsync()
    {
        try
        {
            if (DatabaseService.TryGetValue("LastBackupDatabase", out string? file, out DateTime time))
            {
                file = Path.Join(AppConfig.UserDataFolder, "DatabaseBackup", file);
                if (File.Exists(file))
                {
                    var item = await StorageFile.GetFileFromPathAsync(file);
                    var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(file));
                    var options = new FolderLauncherOptions
                    {
                        ItemsToSelect = { item }
                    };
                    await Launcher.LaunchFolderAsync(folder, options);
                }
                else
                {
                    _logger.LogWarning("Last backup database file not found: {file}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open last backup database");
        }
    }







    #endregion




    #region HoYoShade Framework



    public bool HoYoShadeInstalled { get => field; set => SetProperty(ref field, value); }

    public string HoYoShadePath { get => field; set => SetProperty(ref field, value); } = "";
    
    public string HoYoShadeVersion { get => field; set => SetProperty(ref field, value); } = "";

    public string HoYoShadeTotalSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string HoYoShadeShaderSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string HoYoShadePresetSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string HoYoShadeScreenshotSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string HoYoShadeOtherSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";


    public bool OpenHoYoShadeInstalled { get => field; set => SetProperty(ref field, value); }

    public string OpenHoYoShadePath { get => field; set => SetProperty(ref field, value); } = "";
    
    public string OpenHoYoShadeVersion { get => field; set => SetProperty(ref field, value); } = "";

    public string OpenHoYoShadeTotalSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string OpenHoYoShadeShaderSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string OpenHoYoShadePresetSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string OpenHoYoShadeScreenshotSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string OpenHoYoShadeOtherSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";


    /// <summary>
    /// 安装HoYoShade框架
    /// </summary>
    [RelayCommand]
    private void InstallHoYoShadeFramework()
    {
        try
        {
            _logger.LogInformation("Navigate to HoYoShade installation page");
            WeakReferenceMessenger.Default.Send(new NavigateToDownloadPageMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install HoYoShade framework");
        }
    }


    /// <summary>
    /// 安装ReShade着色器和插件
    /// </summary>
    [RelayCommand]
    private void InstallReShadeShaders()
    {
        try
        {
            _logger.LogInformation("Navigate to ReShade shader installation page");
            WeakReferenceMessenger.Default.Send(new NavigateToReShadeDownloadPageMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install ReShade shaders");
        }
    }


    /// <summary>
    /// 更新HoYoShade占用大小
    /// </summary>
    /// <returns></returns>
    private async Task UpdateHoYoShadeSizeAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
            {
                _logger.LogWarning("UserDataFolder is not set, cannot detect HoYoShade");
                return;
            }

            bool hoYoShadeFound = false;
            bool openHoYoShadeFound = false;
            
            // HoYoShade 占用统计
            long hoYoShadeTotalSize = 0;
            long hoYoShadeShaderSize = 0;
            long hoYoShadePresetSize = 0;
            long hoYoShadeScreenshotSize = 0;
            
            // OpenHoYoShade 占用统计
            long openHoYoShadeTotalSize = 0;
            long openHoYoShadeShaderSize = 0;
            long openHoYoShadePresetSize = 0;
            long openHoYoShadeScreenshotSize = 0;

            _logger.LogInformation("Starting HoYoShade detection in UserDataFolder: {UserDataFolder}", AppConfig.UserDataFolder);

            // 检测并计算 HoYoShade (在用户数据目录下)
            string hoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            _logger.LogInformation("Checking HoYoShade path: {Path}, Exists: {Exists}", hoYoShadePath, Directory.Exists(hoYoShadePath));
            
            if (Directory.Exists(hoYoShadePath))
            {
                hoYoShadeFound = true;
                
                // 计算HoYoShade目录总大小
                hoYoShadeTotalSize = await GetFolderSizeLongAsync(hoYoShadePath);
                _logger.LogInformation("HoYoShade directory size: {Size} bytes", hoYoShadeTotalSize);
                
                // 计算着色器及插件占用 (reshade-shaders文件夹)
                string shadersPath = Path.Combine(hoYoShadePath, "reshade-shaders");
                if (Directory.Exists(shadersPath))
                {
                    hoYoShadeShaderSize = await GetFolderSizeLongAsync(shadersPath);
                    _logger.LogInformation("HoYoShade shaders directory size: {Size} bytes", hoYoShadeShaderSize);
                }
                
                // 计算预设占用 (Presets文件夹)
                string presetsPath = Path.Combine(hoYoShadePath, "Presets");
                if (Directory.Exists(presetsPath))
                {
                    hoYoShadePresetSize = await GetFolderSizeLongAsync(presetsPath);
                    _logger.LogInformation("HoYoShade presets directory size: {Size} bytes", hoYoShadePresetSize);
                }
                
                // 计算截图占用 (Screenshots文件夹)
                string screenshotsPath = Path.Combine(hoYoShadePath, "Screenshots");
                if (Directory.Exists(screenshotsPath))
                {
                    hoYoShadeScreenshotSize = await GetFolderSizeLongAsync(screenshotsPath);
                    _logger.LogInformation("HoYoShade screenshots directory size: {Size} bytes", hoYoShadeScreenshotSize);
                }
            }

            // 检测并计算 OpenHoYoShade (在用户数据目录下)
            string openHoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
            _logger.LogInformation("Checking OpenHoYoShade path: {Path}, Exists: {Exists}", openHoYoShadePath, Directory.Exists(openHoYoShadePath));
            
            if (Directory.Exists(openHoYoShadePath))
            {
                openHoYoShadeFound = true;
                
                // 计算OpenHoYoShade目录总大小
                openHoYoShadeTotalSize = await GetFolderSizeLongAsync(openHoYoShadePath);
                _logger.LogInformation("OpenHoYoShade directory size: {Size} bytes", openHoYoShadeTotalSize);
                
                // 计算着色器及插件占用
                string openShadersPath = Path.Combine(openHoYoShadePath, "reshade-shaders");
                if (Directory.Exists(openShadersPath))
                {
                    openHoYoShadeShaderSize = await GetFolderSizeLongAsync(openShadersPath);
                    _logger.LogInformation("OpenHoYoShade shaders directory size: {Size} bytes", openHoYoShadeShaderSize);
                }
                
                // 计算预设占用
                string openPresetsPath = Path.Combine(openHoYoShadePath, "Presets");
                if (Directory.Exists(openPresetsPath))
                {
                    openHoYoShadePresetSize = await GetFolderSizeLongAsync(openPresetsPath);
                    _logger.LogInformation("OpenHoYoShade presets directory size: {Size} bytes", openHoYoShadePresetSize);
                }
                
                // 计算截图占用
                string openScreenshotsPath = Path.Combine(openHoYoShadePath, "Screenshots");
                if (Directory.Exists(openScreenshotsPath))
                {
                    openHoYoShadeScreenshotSize = await GetFolderSizeLongAsync(openScreenshotsPath);
                    _logger.LogInformation("OpenHoYoShade screenshots directory size: {Size} bytes", openHoYoShadeScreenshotSize);
                }
            }

            _logger.LogInformation("Detection complete. HoYoShade: {HoYoShade}, OpenHoYoShade: {OpenHoYoShade}", 
                hoYoShadeFound, openHoYoShadeFound);

            // 更新HoYoShade显示
            HoYoShadeInstalled = hoYoShadeFound;
            if (hoYoShadeFound)
            {
                HoYoShadePath = hoYoShadePath;
                HoYoShadeTotalSize = FormatSize(hoYoShadeTotalSize);
                HoYoShadeShaderSize = FormatSize(hoYoShadeShaderSize);
                HoYoShadePresetSize = FormatSize(hoYoShadePresetSize);
                HoYoShadeScreenshotSize = FormatSize(hoYoShadeScreenshotSize);
                
                // 计算其它内容占用
                long otherSize = hoYoShadeTotalSize - hoYoShadeShaderSize - hoYoShadePresetSize - hoYoShadeScreenshotSize;
                HoYoShadeOtherSize = FormatSize(Math.Max(0, otherSize));
                
                _logger.LogInformation("HoYoShade sizes - Total: {Total}, Shaders: {Shaders}, Presets: {Presets}, Screenshots: {Screenshots}, Other: {Other}",
                    HoYoShadeTotalSize, HoYoShadeShaderSize, HoYoShadePresetSize, HoYoShadeScreenshotSize, HoYoShadeOtherSize);
            }
            else
            {
                HoYoShadePath = "";
                HoYoShadeTotalSize = "0.00 KB";
                HoYoShadeShaderSize = "0.00 KB";
                HoYoShadePresetSize = "0.00 KB";
                HoYoShadeScreenshotSize = "0.00 KB";
                HoYoShadeOtherSize = "0.00 KB";
            }

            // 更新OpenHoYoShade显示
            OpenHoYoShadeInstalled = openHoYoShadeFound;
            if (openHoYoShadeFound)
            {
                OpenHoYoShadePath = openHoYoShadePath;
                OpenHoYoShadeTotalSize = FormatSize(openHoYoShadeTotalSize);
                OpenHoYoShadeShaderSize = FormatSize(openHoYoShadeShaderSize);
                OpenHoYoShadePresetSize = FormatSize(openHoYoShadePresetSize);
                OpenHoYoShadeScreenshotSize = FormatSize(openHoYoShadeScreenshotSize);
                
                // 计算其它内容占用
                long openOtherSize = openHoYoShadeTotalSize - openHoYoShadeShaderSize - openHoYoShadePresetSize - openHoYoShadeScreenshotSize;
                OpenHoYoShadeOtherSize = FormatSize(Math.Max(0, openOtherSize));
                
                _logger.LogInformation("OpenHoYoShade sizes - Total: {Total}, Shaders: {Shaders}, Presets: {Presets}, Screenshots: {Screenshots}, Other: {Other}",
                    OpenHoYoShadeTotalSize, OpenHoYoShadeShaderSize, OpenHoYoShadePresetSize, OpenHoYoShadeScreenshotSize, OpenHoYoShadeOtherSize);
            }
            else
            {
                OpenHoYoShadePath = "";
                OpenHoYoShadeTotalSize = "0.00 KB";
                OpenHoYoShadeShaderSize = "0.00 KB";
                OpenHoYoShadePresetSize = "0.00 KB";
                OpenHoYoShadeScreenshotSize = "0.00 KB";
                OpenHoYoShadeOtherSize = "0.00 KB";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update HoYoShade size");
        }
    }


    /// <summary>
    /// 获取文件夹大小(长整型)
    /// </summary>
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
            return 0;
        });
    }


    /// <summary>
    /// 格式化文件大小显示
    /// </summary>
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


    /// <summary>
    /// 卸载HoYoShade
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task UninstallHoYoShadeAsync()
    {
        try
        {
            var dialog = new UninstallShadeDialog
            {
                ShadePath = HoYoShadePath,
                ShadeName = "HoYoShade",
                XamlRoot = this.XamlRoot,
                Title = Lang.FileSettingPage_UninstallHoYoShade,
            };
            
            await dialog.ShowAsync();
            
            // Clear version info after uninstall
            await _versionService.ClearHoYoShadeVersionAsync();
            
            // 刷新安装状态
            await UpdateHoYoShadeSizeAsync();
            await LoadVersionInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall HoYoShade");
        }
    }


    /// <summary>
    /// 卸载HoYoShade着色器和插件
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task UninstallHoYoShadeShadersAsync()
    {
        try
        {
            var dialog = new UninstallShadersDialog
            {
                ShadePath = HoYoShadePath,
                ShadeName = "HoYoShade",
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
            
            // 刷新安装状态
            await UpdateHoYoShadeSizeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall HoYoShade shaders");
        }
    }


    /// <summary>
    /// 打开HoYoShade文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenHoYoShadeFolderAsync()
    {
        try
        {
            if (Directory.Exists(HoYoShadePath))
            {
                await Launcher.LaunchUriAsync(new Uri(HoYoShadePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open HoYoShade folder");
        }
    }


    /// <summary>
    /// 卸载OpenHoYoShade
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task UninstallOpenHoYoShadeAsync()
    {
        try
        {
            var dialog = new UninstallShadeDialog
            {
                ShadePath = OpenHoYoShadePath,
                ShadeName = "OpenHoYoShade",
                XamlRoot = this.XamlRoot,
                Title = Lang.FileSettingPage_UninstallOpenHoYoShade,
            };
            
            await dialog.ShowAsync();
            
            // Clear version info after uninstall
            await _versionService.ClearOpenHoYoShadeVersionAsync();
            
            // 刷新安装状态
            await UpdateHoYoShadeSizeAsync();
            await LoadVersionInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall OpenHoYoShade");
        }
    }


    /// <summary>
    /// 卸载OpenHoYoShade着色器和插件
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task UninstallOpenHoYoShadeShadersAsync()
    {
        try
        {
            var dialog = new UninstallShadersDialog
            {
                ShadePath = OpenHoYoShadePath,
                ShadeName = "OpenHoYoShade",
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
            
            // 刷新安装状态
            await UpdateHoYoShadeSizeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall OpenHoYoShade shaders");
        }
    }


    /// <summary>
    /// 打开OpenHoYoShade文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenOpenHoYoShadeFolderAsync()
    {
        try
        {
            if (Directory.Exists(OpenHoYoShadePath))
            {
                await Launcher.LaunchUriAsync(new Uri(OpenHoYoShadePath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open OpenHoYoShade folder");
        }
    }


    /// <summary>
    /// 重置HoYoShade的ReShade.ini
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ResetHoYoShadeReShadeIniAsync()
    {
        try
        {
            var dialog = new ResetReShadeIniDialog
            {
                ShadePath = HoYoShadePath,
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset HoYoShade ReShade.ini");
        }
    }


    /// <summary>
    /// 重置OpenHoYoShade的ReShade.ini
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ResetOpenHoYoShadeReShadeIniAsync()
    {
        try
        {
            var dialog = new ResetReShadeIniDialog
            {
                ShadePath = OpenHoYoShadePath,
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset OpenHoYoShade ReShade.ini");
        }
    }


    /// <summary>
    /// 自定义注入HoYoShade
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task CustomInjectHoYoShadeAsync()
    {
        try
        {
            var dialog = new CustomInjectDialog
            {
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
            if (dialog.Result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.ProcessName))
            {
                await StartShaderInjectorAsync(HoYoShadePath, "HoYoShade", dialog.ProcessName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom inject HoYoShade");
        }
    }


    /// <summary>
    /// 自定义注入OpenHoYoShade
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task CustomInjectOpenHoYoShadeAsync()
    {
        try
        {
            var dialog = new CustomInjectDialog
            {
                XamlRoot = this.XamlRoot,
            };
            
            await dialog.ShowAsync();
            if (dialog.Result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.ProcessName))
            {
                await StartShaderInjectorAsync(OpenHoYoShadePath, "OpenHoYoShade", dialog.ProcessName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom inject OpenHoYoShade");
        }
    }


    private async Task StartShaderInjectorAsync(string shadePath, string shadeName, string processName)
    {
        try
        {
            if (!Directory.Exists(shadePath))
            {
                _logger.LogWarning("{ShadeName} directory not found at {Path}", shadeName, shadePath);
                InAppToast.MainWindow?.Error($"{shadeName} not installed");
                return;
            }

            string injectExePath = Path.Combine(shadePath, "inject.exe");
            if (!File.Exists(injectExePath))
            {
                _logger.LogWarning("inject.exe not found in {ShadeName} at {Path}", shadeName, injectExePath);
                InAppToast.MainWindow?.Error($"inject.exe not found in {shadeName}");
                return;
            }

            _logger.LogInformation("Starting {ShadeName} injector: {InjectPath} {ProcessName}",
                shadeName, injectExePath, processName);

            var injectStartInfo = new ProcessStartInfo
            {
                FileName = injectExePath,
                Arguments = processName,
                UseShellExecute = false,
                WorkingDirectory = shadePath,
                CreateNoWindow = false
            };

            Process? injectorProcess = Process.Start(injectStartInfo);
            if (injectorProcess == null)
            {
                _logger.LogError("Failed to start {ShadeName} injector process", shadeName);
                InAppToast.MainWindow?.Error($"Failed to start {shadeName} injector");
                return;
            }

            _logger.LogInformation("{ShadeName} injector started (PID: {Pid})",
                shadeName, injectorProcess.Id);
            InAppToast.MainWindow?.Success($"{shadeName} injector started");

            _ = Task.Run(async () =>
            {
                try
                {
                    await injectorProcess.WaitForExitAsync();
                    int exitCode = injectorProcess.ExitCode;

                    if (exitCode == 0)
                    {
                        _logger.LogInformation("{ShadeName} injector exited successfully", shadeName);
                    }
                    else if (InjectorErrorCodes.IsInjectorError(exitCode))
                    {
                        _logger.LogWarning("{ShadeName} injector exited with error code: {ExitCode}", shadeName, exitCode);
                        string errorMessage = InjectorHelper.GetErrorMessage(exitCode, shadeName);
                        
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            InAppToast.MainWindow?.Error(errorMessage, null, 8000);
                        });
                    }
                    else
                    {
                        _logger.LogInformation("{ShadeName} injector exited with code: {ExitCode}", shadeName, exitCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring {ShadeName} injector exit code", shadeName);
                }
            });
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start {ShadeName} injector", shadeName);
            InAppToast.MainWindow?.Error($"Failed to start {shadeName} injector: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载版本信息
    /// </summary>
    private async Task LoadVersionInfoAsync()
    {
        try
        {
            var hoYoShadeInfo = await _versionService.GetHoYoShadeVersionAsync();
            if (hoYoShadeInfo != null)
            {
                HoYoShadeVersion = hoYoShadeInfo.Version;
                _logger.LogInformation("Loaded HoYoShade version: {Version}", hoYoShadeInfo.Version);
            }
            else
            {
                HoYoShadeVersion = "";
            }

            var openHoYoShadeInfo = await _versionService.GetOpenHoYoShadeVersionAsync();
            if (openHoYoShadeInfo != null)
            {
                OpenHoYoShadeVersion = openHoYoShadeInfo.Version;
                _logger.LogInformation("Loaded OpenHoYoShade version: {Version}", openHoYoShadeInfo.Version);
            }
            else
            {
                OpenHoYoShadeVersion = "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load version info");
            HoYoShadeVersion = "";
            OpenHoYoShadeVersion = "";
        }
    }
    
    /// <summary>
    /// 检查 HoYoShade 更新
    /// </summary>
    [RelayCommand]
    private async Task CheckHoYoShadeUpdateAsync()
    {
        try
        {
            HoYoShadeUpdateInfo = "Checking for updates...";
            
            var updateService = new HoYoShadeUpdateService(_versionService);
            var latestRelease = await updateService.CheckHoYoShadeUpdateAsync(
                AppConfig.EnableHoYoShadePreviewChannel);
            
            if (latestRelease != null)
            {
                HoYoShadeUpdateInfo = $"New version available: {latestRelease.TagName}";
                _logger.LogInformation("HoYoShade update available: {Version}", latestRelease.TagName);
                
                // Show update dialog
                await ShowUpdateDialogAsync("HoYoShade", latestRelease.TagName, latestRelease.Body);
            }
            else
            {
                HoYoShadeUpdateInfo = "You are running the latest version";
                _logger.LogInformation("HoYoShade is up to date");
                InAppToast.MainWindow?.Success("HoYoShade is up to date!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check HoYoShade update");
            HoYoShadeUpdateInfo = $"Failed to check for updates: {ex.Message}";
            InAppToast.MainWindow?.Error($"Failed to check for updates: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 检查 OpenHoYoShade 更新
    /// </summary>
    [RelayCommand]
    private async Task CheckOpenHoYoShadeUpdateAsync()
    {
        try
        {
            OpenHoYoShadeUpdateInfo = "Checking for updates...";
            
            var updateService = new HoYoShadeUpdateService(_versionService);
            var latestRelease = await updateService.CheckOpenHoYoShadeUpdateAsync(
                AppConfig.EnableHoYoShadePreviewChannel);
            
            if (latestRelease != null)
            {
                OpenHoYoShadeUpdateInfo = $"New version available: {latestRelease.TagName}";
                _logger.LogInformation("OpenHoYoShade update available: {Version}", latestRelease.TagName);
                
                // Show update dialog
                await ShowUpdateDialogAsync("OpenHoYoShade", latestRelease.TagName, latestRelease.Body);
            }
            else
            {
                OpenHoYoShadeUpdateInfo = "You are running the latest version";
                _logger.LogInformation("OpenHoYoShade is up to date");
                InAppToast.MainWindow?.Success("OpenHoYoShade is up to date!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check OpenHoYoShade update");
            OpenHoYoShadeUpdateInfo = $"Failed to check for updates: {ex.Message}";
            InAppToast.MainWindow?.Error($"Failed to check for updates: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 显示更新对话框
    /// </summary>
    private async Task ShowUpdateDialogAsync(string frameworkName, string newVersion, string releaseNotes)
    {
        try
        {
            // Get current framework version
            string currentVersion = "0.0.0";
            if (frameworkName == "HoYoShade")
            {
                var currentInfo = await _versionService.GetHoYoShadeVersionAsync();
                currentVersion = currentInfo?.Version ?? "0.0.0";
            }
            else if (frameworkName == "OpenHoYoShade")
            {
                var currentInfo = await _versionService.GetOpenHoYoShadeVersionAsync();
                currentVersion = currentInfo?.Version ?? "0.0.0";
            }
            
            // Fetch GitHub Release to get download URL and package size
            string? packageDownloadUrl = null;
            long packageSize = 0;
            
            try
            {
                var apiUrl = $"https://api.github.com/repos/DuolaD/HoYoShade/releases/tags/{newVersion}";
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub/1.0");
                
                var response = await httpClient.GetStringAsync(apiUrl);
                var release = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);
                
                // Find the correct asset based on framework name
                var assets = release.GetProperty("assets");
                string assetNamePattern = frameworkName == "HoYoShade" 
                    ? "HoYoShade-" 
                    : "OpenHoYoShade-";
                
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.StartsWith(assetNamePattern) && assetName.EndsWith(".zip"))
                    {
                        packageDownloadUrl = asset.GetProperty("browser_download_url").GetString();
                        packageSize = asset.GetProperty("size").GetInt64();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch GitHub release assets for {FrameworkName}", frameworkName);
            }
            
            // Create a ReleaseInfoDetail-like object for the framework update
            var frameworkRelease = new HoYoShadeHub.RPC.Update.Metadata.ReleaseInfoDetail
            {
                Version = newVersion,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture,
                InstallType = HoYoShadeHub.RPC.Update.Metadata.InstallType.Portable,
                BuildTime = DateTimeOffset.Now,
                DisableAutoUpdate = true, // 禁用自动更新，框架更新需要手动下载
                PackageUrl = packageDownloadUrl ?? $"https://github.com/DuolaD/HoYoShade/releases/tag/{newVersion}",
                PackageSize = packageSize,
            };
            
            // Create and show UpdateWindow with current version info
            var updateWindow = new HoYoShadeHub.Features.Update.UpdateWindow 
            { 
                NewVersion = frameworkRelease,
                Title = $"{frameworkName} - Update",
                // Pass the current framework version
                CurrentFrameworkVersion = currentVersion
            };
            
            updateWindow.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Show update dialog");
        }
    }


    #endregion


    #region 缓存


    public string LogCacheSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string ImageCacheSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string WebCacheSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";

    public string GameCacheSize { get => field; set => SetProperty(ref field, value); } = "0.00 KB";


    /// <summary>
    /// 更新缓存大小
    /// </summary>
    /// <returns></returns>
    private async Task UpdateCacheSizeAsync()
    {
        try
        {
            var (logSize, imageSize, webSize, gameSize) = await Task.Run(() =>
            {
                long logSize = 0;
                long imageSize = 0;
                long webSize = 0;
                long gameSize = 0;

                // Log cache
                string logFolder = Path.Combine(AppConfig.CacheFolder, "log");
                if (Directory.Exists(logFolder))
                {
                    logSize = Directory.GetFiles(logFolder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }

                // Image cache
                string imageCacheFolder = Path.Combine(AppConfig.CacheFolder, "cache");
                if (Directory.Exists(imageCacheFolder))
                {
                    imageSize = Directory.GetFiles(imageCacheFolder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }
                
                // Thumbnail cache
                string thumbCacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");
                if (Directory.Exists(thumbCacheFolder))
                {
                    imageSize += Directory.GetFiles(thumbCacheFolder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }

                // Webview cache
                string webviewFolder = Path.Combine(AppConfig.CacheFolder, "webview");
                if (Directory.Exists(webviewFolder))
                {
                    webSize = Directory.GetFiles(webviewFolder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }

                // Game resource cache
                string gameResourceFolder = Path.Combine(AppConfig.CacheFolder, "GameResource");
                if (Directory.Exists(gameResourceFolder))
                {
                    gameSize = Directory.GetFiles(gameResourceFolder, "*", SearchOption.AllDirectories)
                        .Sum(file => new FileInfo(file).Length);
                }

                return (logSize, imageSize, webSize, gameSize);
            });

            // Update properties on UI thread
            LogCacheSize = FormatSize(logSize);
            ImageCacheSize = FormatSize(imageSize);
            WebCacheSize = FormatSize(webSize);
            GameCacheSize = FormatSize(gameSize);
            
            _logger.LogInformation("Cache sizes updated - Log: {Log}, Image: {Image}, Web: {Web}, Game: {Game}",
                LogCacheSize, ImageCacheSize, WebCacheSize, GameCacheSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update cache size");
        }
    }


    /// <summary>
    /// 打开日志文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenLogFolderAsync()
    {
        try
        {
            string logFolder = Path.Combine(AppConfig.CacheFolder, "log");
            if (Directory.Exists(logFolder))
            {
                await Launcher.LaunchUriAsync(new Uri(logFolder));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open log folder");
        }
    }


    /// <summary>
    /// 清理缓存
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                // 清理图片缓存
                string imageCacheFolder = Path.Combine(AppConfig.CacheFolder, "cache");
                if (Directory.Exists(imageCacheFolder))
                {
                    Directory.Delete(imageCacheFolder, true);
                }

                // 清理缩略图缓存
                string thumbCacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");
                if (Directory.Exists(thumbCacheFolder))
                {
                    Directory.Delete(thumbCacheFolder, true);
                }

                // 清理Webview缓存
                string webviewFolder = Path.Combine(AppConfig.CacheFolder, "webview");
                if (Directory.Exists(webviewFolder))
                {
                    Directory.Delete(webviewFolder, true);
                }

                // 清理游戏资源缓存
                string gameResourceFolder = Path.Combine(AppConfig.CacheFolder, "GameResource");
                if (Directory.Exists(gameResourceFolder))
                {
                    Directory.Delete(gameResourceFolder, true);
                }
            });

            await UpdateCacheSizeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clear cache");
        }
    }


    #endregion

}
