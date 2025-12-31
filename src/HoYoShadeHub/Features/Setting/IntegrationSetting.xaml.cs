using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
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
        InitializeStarwardLauncherSettings();
    }


    #region Starward Launcher


    /// <summary>
    /// Starward启动器URL协议是否可用
    /// </summary>
    public bool IsStarwardProtocolAvailable
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateProtocolStatus();
            }
        }
    }


    /// <summary>
    /// 协议状态文本
    /// </summary>
    public string ProtocolStatusText
    {
        get => field;
        set => SetProperty(ref field, value);
    }


    /// <summary>
    /// 协议状态颜色（使用Brush）
    /// </summary>
    public Microsoft.UI.Xaml.Media.Brush ProtocolStatusColor
    {
        get => field;
        set => SetProperty(ref field, value);
    }


    /// <summary>
    /// 使用Starward启动器启动公开客户端游戏
    /// </summary>
    public bool UseStarwardLauncher
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.UseStarwardLauncher = value;
                // 发送消息通知启动器页面更新状态
                WeakReferenceMessenger.Default.Send(new UseStarwardLauncherChangedMessage(value));
            }
        }
    }


    /// <summary>
    /// 更新协议状态显示
    /// </summary>
    private void UpdateProtocolStatus()
    {
        if (IsStarwardProtocolAvailable)
        {
            ProtocolStatusText = Lang.SettingPage_ProtocolStatusAvailable;
            ProtocolStatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }
        else
        {
            ProtocolStatusText = Lang.SettingPage_ProtocolStatusUnavailable;
            ProtocolStatusColor = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
        }
    }


    /// <summary>
    /// 初始化Starward启动器设置
    /// </summary>
    private void InitializeStarwardLauncherSettings()
    {
        try
        {
            IsStarwardProtocolAvailable = CheckStarwardProtocolAvailable();
            UseStarwardLauncher = AppConfig.UseStarwardLauncher && IsStarwardProtocolAvailable;
            UpdateProtocolStatus();
            
            _logger.LogInformation("Starward protocol available: {Available}, UseStarwardLauncher: {Use}", 
                IsStarwardProtocolAvailable, UseStarwardLauncher);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize Starward launcher settings");
        }
    }


    /// <summary>
    /// 检查starward://协议是否在系统中注册
    /// </summary>
    private static bool CheckStarwardProtocolAvailable()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey("starward");
            if (key != null)
            {
                var urlProtocol = key.GetValue("URL Protocol");
                return urlProtocol != null;
            }
        }
        catch
        {
            // Ignore registry access errors
        }
        return false;
    }


    #endregion


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
                        Lang.SettingPage_GenshinBlenderPluginValidationFailed,
                        Lang.SettingPage_GenshinBlenderPluginMustContainClientExe
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
                        Lang.SettingPage_ZZZBlenderPluginValidationFailed,
                        Lang.SettingPage_ZZZBlenderPluginMustContainLoaderExe
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
