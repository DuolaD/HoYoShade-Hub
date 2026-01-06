using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Features.Background;
using HoYoShadeHub.Features.GameSelector;
using HoYoShadeHub.Features.HoYoPlay;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.RPC.GameInstall;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
#pragma warning disable MVVMTK0045 // Using [ObservableProperty] on fields is not AOT compatible for WinRT


namespace HoYoShadeHub.Features.GameLauncher;

[INotifyPropertyChanged]
public sealed partial class GameLauncherSettingDialog : ContentDialog
{


    private readonly ILogger<GameLauncherSettingDialog> _logger = AppConfig.GetLogger<GameLauncherSettingDialog>();


    private readonly HoYoPlayService _hoyoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();



    private readonly BackgroundService _backgroundService = AppConfig.GetService<BackgroundService>();


    public GameLauncherSettingDialog()
    {
        this.InitializeComponent();
        this.Loaded += GameLauncherSettingDialog_Loaded;
        this.Unloaded += GameLauncherSettingDialog_Unloaded;
    }



    public GameId CurrentGameId { get; set; }


    public GameBiz CurrentGameBiz { get; set; }

    private ObservableCollection<GameInstallPathItemDialog> _gameInstallPaths = new();
    public ObservableCollection<GameInstallPathItemDialog> GameInstallPaths
    {
        get => _gameInstallPaths;
        set => SetProperty(ref _gameInstallPaths, value);
    }



    private void FlipView_Settings_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var grid = VisualTreeHelper.GetChild(FlipView_Settings, 0);
            if (grid != null)
            {
                var count = VisualTreeHelper.GetChildrenCount(grid);
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(grid, i);
                        if (child is Button button)
                        {
                            button.IsHitTestVisible = false;
                            button.Opacity = 0;
                        }
                        else if (child is ScrollViewer scrollViewer)
                        {
                            scrollViewer.PointerWheelChanged += (_, e) => e.Handled = true;
                        }
                    }
                }
            }
        }
        catch { }
    }



    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        try
        {
            if (args.InvokedItemContainer?.Tag is string index && int.TryParse(index, out int target))
            {
                int steps = target - FlipView_Settings.SelectedIndex;
                if (steps > 0)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        FlipView_Settings.SelectedIndex++;
                    }
                }
                else
                {
                    for (int i = 0; i < -steps; i++)
                    {
                        FlipView_Settings.SelectedIndex--;
                    }
                }
            }
        }
        catch { }
    }




    private async void GameLauncherSettingDialog_Loaded(object sender, RoutedEventArgs e)
    {
        CurrentGameBiz = CurrentGameId?.GameBiz ?? GameBiz.None;
        CheckCanRepairGame();
        await InitializeBasicInfoAsync();
        await InitializeGameInstallPathsAsync();
        InitializeStartArgument();
        InitializeCustomBg();
        await InitializeGamePackagesAsync();
    }


    private void GameLauncherSettingDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        LatestPackageGroups = null!;
        PreInstallPackageGroups = null!;
        FlipView_Settings.Items.Clear();
    }




    [RelayCommand]
    private void Close()
    {
        this.Hide();
    }





    #region 基本信息



    private bool? _hasAudioPackages;


    public bool CanRepairGame { get; set => SetProperty(ref field, value); } = true;


    public GameBizIcon CurrentGameBizIcon { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 安装路径
    /// </summary>
    public string? InstallPath { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 文件夹大小
    /// </summary>
    public string? GameSize { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 添加游戏目录错误信息
    /// </summary>
    public string? AddGameInstallPathError { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 是否可以卸载和修复
    /// </summary>
    public bool UninstallAndRepairEnabled { get; set => SetProperty(ref field, value); }




    /// <summary>
    /// 使用 CMD 启动游戏
    /// </summary>
    public bool StartGameWithCMD
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.StartGameWithCMD = value;
            }
        }
    } = AppConfig.StartGameWithCMD;




    private async Task InitializeBasicInfoAsync()
    {
        try
        {
            if (CurrentGameId.GameBiz.IsKnown())
            {
                CurrentGameBizIcon = new GameBizIcon(CurrentGameId.GameBiz);
            }
            else
            {
                var info = await _hoyoPlayService.GetGameInfoAsync(CurrentGameId);
                CurrentGameBizIcon = new GameBizIcon(info);
            }
            InstallPath = GameLauncherService.GetGameInstallPath(CurrentGameId, out bool storageRemoved);
            GameSize = GetSize(InstallPath);
            if (await _gameLauncherService.GetGameProcessAsync(CurrentGameId) is null)
            {
                UninstallAndRepairEnabled = InstallPath != null && !storageRemoved;
            }
            else
            {
                UninstallAndRepairEnabled = false;
            }
            await InitializeAudioLanguageAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializeBasicInfoAsync ({biz})", CurrentGameBiz);
        }
    }



    private static string? GetSize(string? path)
    {
        if (!Directory.Exists(path))
        {
            return null;
        }
        var size = new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        var gb = (double)size / (1 << 30);
        return $"{gb:F2}GB";
    }



    private async Task InitializeAudioLanguageAsync()
    {
        try
        {
            GameConfig? config = await _hoyoPlayService.GetGameConfigAsync(CurrentGameId);
            if (config is not null)
            {
                if (!string.IsNullOrWhiteSpace(config.AudioPackageScanDir))
                {
                    _hasAudioPackages = true;
                    Segmented_SelectLanguage.SelectedItems.Clear();
                    // Removed AudioLanguage logic
                }
                else
                {
                    _hasAudioPackages = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitializeAudioLanguageAsync ({biz})", CurrentGameBiz);
        }
    }



    /// <summary>
    /// 打开游戏安装文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenInstalGameFolderAsync()
    {
        try
        {
            if (Directory.Exists(InstallPath))
            {
                await Launcher.LaunchUriAsync(new Uri(InstallPath));
            }
        }
        catch { }
    }


    /// <summary>
    /// 删除游戏安装路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task DeleteGameInstllPathAsync()
    {
        try
        {
            GameLauncherService.ChangeGameInstallPath(CurrentGameId, null);
            WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
            await InitializeBasicInfoAsync();
            await TryStopGameInstallTaskAsync();
        }
        catch { }
    }



    /// <summary>
    /// 定位游戏路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task LocateGameAsync()
    {
        try
        {
            string? previousInstallPath = InstallPath;
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                if (DriveHelper.GetDriveType(folder) is DriveType.Network && !new Uri(folder).IsUnc)
                {
                    TextBlock_NetworkDriveWarning.Visibility = Visibility.Visible;
                }
                else
                {
                    TextBlock_NetworkDriveWarning.Visibility = Visibility.Collapsed;
                    
                    // 验证游戏exe是否存在
                    var exeName = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);
                    var exePath = System.IO.Path.Combine(folder, exeName);
                    
                    if (!File.Exists(exePath))
                    {
                        // 验证失败：显示错误，不修改路径
                        AddGameInstallPathError = string.Format(Lang.GameLauncherSettingDialog_GameExeNotFoundInFolder, exeName);
                        _logger.LogWarning("Game exe not found in selected folder: {Path}, expected: {ExeName}", folder, exeName);
                        return;
                    }
                    
                    // 验证成功后才修改路径
                    AddGameInstallPathError = null;
                    GameLauncherService.ChangeGameInstallPath(CurrentGameId, folder);
                    await InitializeBasicInfoAsync();
                    WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
                    if (previousInstallPath != folder)
                    {
                        await TryStopGameInstallTaskAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Locate game failed {GameBiz}", CurrentGameBiz);
        }
    }

    

    private void CheckCanRepairGame()
    {
        // Removed game install task check logic
    }



    /// <summary>
    /// 修复游戏
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task RepairGameAsync()
    {
        // Removed game repair logic
        await Task.CompletedTask;
    }



    [RelayCommand]
    private async Task RepairGameInternalAsync()
    {
        // Removed game repair internal logic
        await Task.CompletedTask;
    }



    private string? _uninstallError;
    public string? UninstallError { get => _uninstallError; set => SetProperty(ref _uninstallError, value); }



    [RelayCommand]
    private void ShowUninstallGameWarning()
    {
        // Uninstall warning removed
    }



    [RelayCommand]
    private async Task UninstallGameAsync()
    {
        // Uninstall functionality removed
        await Task.CompletedTask;
    }



    private void Segmented_SelectLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Segmented segmented)
        {
            CanRepairGame = segmented.SelectedItems.Count > 0;
        }
    }




    private async Task TryStopGameInstallTaskAsync()
    {
        // Removed game install task stop logic
        await Task.CompletedTask;
    }




    #endregion




    #region 启动参数


    /// <summary>
    /// 命令行启动参数
    /// </summary>
    [ObservableProperty]
    public string? _StartGameArgument;
    partial void OnStartGameArgumentChanged(string? value)
    {
        AppConfig.SetStartArgument(CurrentGameBiz, value);
    }


    /// <summary>
    /// 启动游戏后的操作
    /// </summary>
    public int StartGameAction
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.StartGameAction = (StartGameAction)value;
            }
        }
    } = Math.Clamp((int)AppConfig.StartGameAction, 0, 2);


    /// <summary>
    /// 是否启用第三方工具
    /// </summary>
    [ObservableProperty]
    public bool _EnableThirdPartyTool;
    partial void OnEnableThirdPartyToolChanged(bool value)
    {
        AppConfig.SetEnableThirdPartyTool(CurrentGameBiz, value);
    }


    /// <summary>
    /// 第三方工具路径
    /// </summary>
    [ObservableProperty]
    public string? _ThirdPartyToolPath;
    partial void OnThirdPartyToolPathChanged(string? value)
    {
        try
        {
            GameLauncherService.SetThirdPartyToolPath(CurrentGameId, value);
        }
        catch { }
    }



    private void InitializeStartArgument()
    {
        _StartGameArgument = AppConfig.GetStartArgument(CurrentGameBiz);
        _EnableThirdPartyTool = AppConfig.GetEnableThirdPartyTool(CurrentGameBiz);
        _ThirdPartyToolPath = GameLauncherService.GetThirdPartyToolPath(CurrentGameId);
        OnPropertyChanged(nameof(StartGameArgument));
        OnPropertyChanged(nameof(EnableThirdPartyTool));
        OnPropertyChanged(nameof(ThirdPartyToolPath));
    }



    /// <summary>
    /// 修改第三方启动工具路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ChangeThirdPartyPathAsync()
    {
        try
        {
            var file = await FileDialogHelper.PickSingleFileAsync(this.XamlRoot);
            if (File.Exists(file))
            {
                ThirdPartyToolPath = file;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change third party tool path ({biz})", CurrentGameBiz);
        }
    }


    /// <summary>
    /// 打开第三方工具文件夹
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenThirdPartyToolFolderAsync()
    {
        try
        {
            if (File.Exists(ThirdPartyToolPath))
            {
                var folder = Path.GetDirectoryName(ThirdPartyToolPath);
                var file = await StorageFile.GetFileFromPathAsync(ThirdPartyToolPath);
                var option = new FolderLauncherOptions();
                option.ItemsToSelect.Add(file);
                await Launcher.LaunchFolderPathAsync(folder, option);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open third party tool folder {folder}", ThirdPartyToolPath);
        }
    }


    /// <summary>
    /// 删除第三方工具路径
    /// </summary>
    [RelayCommand]
    private void DeleteThirdPartyToolPath()
    {
        ThirdPartyToolPath = null;
    }





    #endregion




    #region 自定义背景



    /// <summary>
    /// 是否启用自定义背景
    /// </summary>
    [ObservableProperty]
    public bool _EnableCustomBg;
    partial void OnEnableCustomBgChanged(bool value)
    {
        AppConfig.SetEnableCustomBg(CurrentGameBiz, value);
        WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
    }


    /// <summary>
    /// 自定义背景，文件名，存储在 UserDataFolder/bg
    /// </summary>
    public string? CustomBg { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 修改背景错误信息
    /// </summary>
    public string? ChangeBgError { get; set => SetProperty(ref field, value); }


    private void InitializeCustomBg()
    {
        _EnableCustomBg = AppConfig.GetEnableCustomBg(CurrentGameBiz);
        CustomBg = AppConfig.GetCustomBg(CurrentGameBiz);
        OnPropertyChanged(nameof(EnableCustomBg));
    }



    /// <summary>
    /// 修改自定义背景
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ChangeCustomBgAsync()
    {
        try
        {
            ChangeBgError = null;
            string? name = await _backgroundService.ChangeCustomBackgroundFileAsync(this.XamlRoot);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            CustomBg = name;
            AppConfig.SetCustomBg(CurrentGameBiz, name);
            WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
        }
        catch (COMException ex)
        {
            ChangeBgError = Lang.GameLauncherSettingDialog_CannotDecodeFile;
            _logger.LogError(ex, "Change custom background failed");
        }
        catch (Exception ex)
        {
            ChangeBgError = Lang.GameLauncherSettingDialog_AnUnknownErrorOccurredPleaseCheckTheLogs;
            _logger.LogError(ex, "Change custom background failed");
        }
    }



    /// <summary>
    /// 打开自定义背景文件
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenCustomBgAsync()
    {
        try
        {
            string path = Path.Join(AppConfig.UserDataFolder, "bg", CustomBg);
            if (File.Exists(path))
            {
                await Launcher.LaunchUriAsync(new Uri(path));
            }
        }
        catch { }
    }



    /// <summary>
    /// 删除自定义背景
    /// </summary>
    [RelayCommand]
    private void DeleteCustomBg()
    {
        CustomBg = null;
        AppConfig.SetCustomBg(CurrentGameBiz, null);
        WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
    }



    /// <summary>
    /// 视频背景音量
    /// </summary>
    public int VideoBgVolume
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(VideoBgVolumeButtonIcon));
                WeakReferenceMessenger.Default.Send(new VideoBgVolumeChangedMessage(value));
                AppConfig.VideoBgVolume = value;
            }
        }
    } = AppConfig.VideoBgVolume;



    /// <summary>
    /// 音量图标
    /// </summary>
    public string VideoBgVolumeButtonIcon => VideoBgVolume switch
    {
        > 66 => "\uE995",
        > 33 => "\uE994",
        > 1 => "\uE993",
        _ => "\uE992",
    };


    private int notMuteVolume = 100;

    /// <summary>
    /// 静音
    /// </summary>
    [RelayCommand]
    private void Mute()
    {
        if (VideoBgVolume > 0)
        {
            notMuteVolume = VideoBgVolume;
            VideoBgVolume = 0;
        }
        else
        {
            VideoBgVolume = notMuteVolume;
        }
    }





    /// <summary>
    /// 接受拖放文件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_BackgroundDragIn_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }



    /// <summary>
    /// 拖放文件，修改自定义背景
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void Grid_BackgroundDragIn_Drop(object sender, DragEventArgs e)
    {
        ChangeBgError = null;
        var defer = e.GetDeferral();
        try
        {
            if ((await e.DataView.GetStorageItemsAsync()).FirstOrDefault() is StorageFile file)
            {
                string? name = await BackgroundService.ChangeCustomBackgroundFileAsync(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }
                CustomBg = name;
                AppConfig.SetCustomBg(CurrentGameBiz, name);
                if (EnableCustomBg)
                {
                    WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
                }
                else
                {
                    EnableCustomBg = true;
                }
            }
        }
        catch (COMException ex)
        {
            ChangeBgError = Lang.GameLauncherSettingDialog_CannotDecodeFile;
            _logger.LogError(ex, "Change custom background failed");
        }
        catch (Exception ex)
        {
            ChangeBgError = Lang.GameLauncherSettingDialog_AnUnknownErrorOccurredPleaseCheckTheLogs;
            _logger.LogError(ex, "Change custom background failed");
        }
        defer.Complete();
    }



    #endregion



    #region 游戏包体



    /// <summary>
    /// 最新版本
    /// </summary>
    public string LatestVersion { get => field; set => SetProperty(ref field, value); }

    /// <summary>
    /// 最新版本包体
    /// </summary>
    public List<PackageGroup> LatestPackageGroups { get => field; set => SetProperty(ref field, value); }

    /// <summary>
    /// 预下载版本
    /// </summary>
    public string PreInstallVersion { get => field; set => SetProperty(ref field, value); }

    /// <summary>
    /// 预下载版本包体
    /// </summary>
    public List<PackageGroup> PreInstallPackageGroups { get => field; set => SetProperty(ref field, value); }




    private async Task InitializeGamePackagesAsync()
    {
        // Removed game package logic
        await Task.CompletedTask;
    }



    private List<PackageGroup> GetGameResourcePackageGroups(GamePackageVersion gameResource)
    {
        return new List<PackageGroup>();
    }



    private async void Button_CopyUrl_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await Task.CompletedTask;
    }



    private async Task CopySuccessAsync(Button button)
    {
        try
        {
            button.IsEnabled = false;
            if (button.Content is FontIcon icon)
            {
                // Accpet
                icon.Glyph = "\uF78C";
                await Task.Delay(1000);
            }
        }
        finally
        {
            button.IsEnabled = true;
            if (button.Content is FontIcon icon)
            {
                // Link
                icon.Glyph = "\uE71B";
            }
        }
    }




    public class PackageGroup
    {
        public string Name { get; set; }

        public List<PackageItem> Items { get; set; }
    }



    public class PackageItem
    {
        public string FileName { get; set; }

        public string Url { get; set; }

        public string Md5 { get; set; }

        public long PackageSize { get; set; }

        public long DecompressSize { get; set; }

        public string PackageSizeString => GetSizeString(PackageSize);

        public string DecompressSizeString => GetSizeString(DecompressSize);

        private string GetSizeString(long size)
        {
            const double KB = 1 << 10;
            const double MB = 1 << 20;
            const double GB = 1 << 30;
            if (size >= GB)
            {
                return $"{size / GB:F2} GB";
            }
            else if (size >= MB)
            {
                return $"{size / MB:F2} MB";
            }
            else
            {
                return $"{size / KB:F2} KB";
            }
        }
    }



    #endregion




    private void TextBlock_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize -= 1;
        }
    }

    #region 游戏目录管理

    private async Task InitializeGameInstallPathsAsync()
    {
        try
        {
            var list = new ObservableCollection<GameInstallPathItemDialog>();
            var paths = GameLauncherService.GetAllGameInstallPaths(CurrentGameBiz);
            var selectedIndex = AppConfig.GetSelectedGameInstallPathIndex(CurrentGameBiz);

            for (int i = 0; i < paths.Count; i++)
            {
                var pathItem = new GameInstallPathItemDialog(this, CurrentGameBiz, i)
                {
                    Path = paths[i],
                    IsSelected = i == selectedIndex
                };
                list.Add(pathItem);
            }

            // 验证游戏exe是否存在
            foreach (var pathItem in list)
            {
                var fullPath = GameLauncherService.GetFullPathIfRelativePath(pathItem.Path);
                var exeName = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);
                var exePath = System.IO.Path.Combine(fullPath, exeName);
                pathItem.IsValid = File.Exists(exePath);
            }
            
            GameInstallPaths = list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize game install paths");
        }
    }

    [RelayCommand]
    private async Task AddGameInstallPathAsync()
    {
        try
        {
            AddGameInstallPathError = null;
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                // 验证是否包含游戏exe
                var exeName = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);
                var exePath = System.IO.Path.Combine(folder, exeName);
                
                if (!File.Exists(exePath))
                {
                    AddGameInstallPathError = string.Format(Lang.GameLauncherSettingDialog_GameExeNotFoundInFolder, exeName);
                    return;
                }

                GameLauncherService.AddGameInstallPath(CurrentGameBiz, folder);
                await InitializeGameInstallPathsAsync();
                await InitializeBasicInfoAsync();
                this.Bindings.Update();
                WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add game install path");
            AddGameInstallPathError = Lang.GameLauncherSettingDialog_AddGameDirectoryFailed;
        }
    }

    internal void OnPathSelected(GameInstallPathItemDialog item)
    {
        try
        {
            var index = GameInstallPaths.IndexOf(item);
            if (index >= 0)
            {
                GameLauncherService.SetSelectedGameInstallPathIndex(CurrentGameBiz, index);
                
                // 更新所有项的选中状态
                for (int i = 0; i < GameInstallPaths.Count; i++)
                {
                    GameInstallPaths[i].IsSelected = i == index;
                }
                
                // 更新显示的安装路径
                _ = InitializeBasicInfoAsync();
                WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Select game install path");
        }
    }

    internal async Task RemoveGameInstallPathAsync(GameInstallPathItemDialog item)
    {
        try
        {
            var index = GameInstallPaths.IndexOf(item);
            if (index >= 0)
            {
                // 如果是最后一个路径，直接清空所有配置
                if (GameInstallPaths.Count == 1)
                {
                    GameLauncherService.ChangeGameInstallPath(CurrentGameBiz, null);
                    // 清空多路径配置
                    AppConfig.SetGameInstallPaths(CurrentGameBiz, null);
                }
                else
                {
                    // 否则正常移除
                    GameLauncherService.RemoveGameInstallPath(CurrentGameBiz, index);
                }
                
                await InitializeGameInstallPathsAsync();
                await InitializeBasicInfoAsync();
                WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remove game install path");
        }
    }

    #endregion


}
