using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Threading.Tasks;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class IntegrationSetting : PageBase
{

    private readonly ILogger<IntegrationSetting> _logger = AppConfig.GetLogger<IntegrationSetting>();


    public IntegrationSetting()
    {
        this.InitializeComponent();
    }


    protected override void OnLoaded()
    {
        InitializeBlenderPluginPaths();
    }


    #region Blender Plugin


    /// <summary>
    /// 原神Blender插件路径
    /// </summary>
    public string? GenshinBlenderPluginPath
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(GenshinBlenderPluginPathDisplay));
            }
        }
    }


    /// <summary>
    /// 原神Blender插件路径显示文本
    /// </summary>
    public string GenshinBlenderPluginPathDisplay => string.IsNullOrWhiteSpace(GenshinBlenderPluginPath) ? Lang.SettingPage_NotSet : GenshinBlenderPluginPath;


    /// <summary>
    /// 绝区零Blender插件路径
    /// </summary>
    public string? ZZZBlenderPluginPath
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ZZZBlenderPluginPathDisplay));
            }
        }
    }


    /// <summary>
    /// 绝区零Blender插件路径显示文本
    /// </summary>
    public string ZZZBlenderPluginPathDisplay => string.IsNullOrWhiteSpace(ZZZBlenderPluginPath) ? Lang.SettingPage_NotSet : ZZZBlenderPluginPath;


    /// <summary>
    /// 初始化Blender插件路径
    /// </summary>
    private void InitializeBlenderPluginPaths()
    {
        try
        {
            GenshinBlenderPluginPath = AppConfig.GenshinBlenderPluginPath;
            ZZZBlenderPluginPath = AppConfig.ZZZBlenderPluginPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize Blender plugin paths");
        }
    }


    /// <summary>
    /// 选择原神Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private async Task SelectGenshinBlenderPluginFolderAsync()
    {
        try
        {
            var folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                GenshinBlenderPluginPath = folder;
                AppConfig.GenshinBlenderPluginPath = folder;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Select Genshin Blender plugin folder");
        }
    }


    /// <summary>
    /// 解绑原神Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private void UnbindGenshinBlenderPluginFolder()
    {
        try
        {
            GenshinBlenderPluginPath = null;
            AppConfig.GenshinBlenderPluginPath = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unbind Genshin Blender plugin folder");
        }
    }


    /// <summary>
    /// 选择绝区零Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private async Task SelectZZZBlenderPluginFolderAsync()
    {
        try
        {
            var folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                ZZZBlenderPluginPath = folder;
                AppConfig.ZZZBlenderPluginPath = folder;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Select ZZZ Blender plugin folder");
        }
    }


    /// <summary>
    /// 解绑绝区零Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private void UnbindZZZBlenderPluginFolder()
    {
        try
        {
            ZZZBlenderPluginPath = null;
            AppConfig.ZZZBlenderPluginPath = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unbind ZZZ Blender plugin folder");
        }
    }


    #endregion


}
