using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.System;


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
                // 验证文件夹是否包含client.exe
                string clientExePath = Path.Combine(folder, "client.exe");
                if (!File.Exists(clientExePath))
                {
                    await ShowErrorDialogAsync(
                        "原神插件验证失败",
                        "所选文件夹必须包含 client.exe 文件"
                    );
                    return;
                }

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
    /// 打开原神Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private async Task OpenGenshinBlenderPluginFolderAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(GenshinBlenderPluginPath) && Directory.Exists(GenshinBlenderPluginPath))
            {
                await Launcher.LaunchUriAsync(new Uri(GenshinBlenderPluginPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open Genshin Blender plugin folder");
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
                // 验证文件夹是否包含loader.exe
                string loaderExePath = Path.Combine(folder, "loader.exe");
                if (!File.Exists(loaderExePath))
                {
                    await ShowErrorDialogAsync(
                        "绝区零插件验证失败",
                        "所选文件夹必须包含 loader.exe 文件"
                    );
                    return;
                }

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
    /// 打开绝区零Blender插件文件夹
    /// </summary>
    [RelayCommand]
    private async Task OpenZZZBlenderPluginFolderAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(ZZZBlenderPluginPath) && Directory.Exists(ZZZBlenderPluginPath))
            {
                await Launcher.LaunchUriAsync(new Uri(ZZZBlenderPluginPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open ZZZ Blender plugin folder");
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


    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = Lang.Common_Confirm,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Show error dialog");
        }
    }


    /// <summary>
    /// 文本截断时自动缩小字体
    /// </summary>
    private void TextBlock_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize -= 1;
        }
    }


    #endregion


}
