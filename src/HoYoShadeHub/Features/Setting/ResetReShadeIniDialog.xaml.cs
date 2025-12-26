using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Setting;

[INotifyPropertyChanged]
public sealed partial class ResetReShadeIniDialog : ContentDialog
{
    private readonly ILogger<ResetReShadeIniDialog> _logger = AppConfig.GetLogger<ResetReShadeIniDialog>();

    public ResetReShadeIniDialog()
    {
        this.InitializeComponent();
    }

    private string _shadePath = "";
    public string ShadePath 
    { 
        get => _shadePath; 
        set => SetProperty(ref _shadePath, value);
    }

    public bool IsResetting { get => field; set => SetProperty(ref field, value); }

    public string StatusMessage { get => field; set => SetProperty(ref field, value); } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(ShowResetButton))]
    private bool isCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool canReset = true;

    public bool CanCancel => !IsResetting || IsCompleted;

    public bool ShowResetButton => !IsResetting && !IsCompleted;

    public string CancelButtonText => IsCompleted ? Lang.ResetReShadeIniDialog_Close : Lang.ResetReShadeIniDialog_Cancel;

    [RelayCommand]
    private async Task ResetAsync()
    {
        try
        {
            _logger.LogInformation("ResetAsync called, ShadePath={ShadePath}", ShadePath);
            
            IsResetting = true;
            CanReset = false;
            StatusMessage = Lang.ResetReShadeIniDialog_Resetting;

            await Task.Delay(500); // 短暂延迟让UI更新

            // 开始重置
            await PerformResetAsync();

            StatusMessage = Lang.ResetReShadeIniDialog_ResetCompleted;
            IsCompleted = true;
            
            await Task.Delay(1000); // 显示完成状态
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset ReShade.ini failed");
            StatusMessage = $"{Lang.ResetReShadeIniDialog_ResetFailed}: {ex.Message}";
            IsResetting = false;
            CanReset = true;
        }
    }

    private async Task PerformResetAsync()
    {
        if (!Directory.Exists(ShadePath))
        {
            _logger.LogWarning("Shade path does not exist: {path}", ShadePath);
            throw new DirectoryNotFoundException(Lang.ResetReShadeIniDialog_ShadePathNotFound);
        }

        await Task.Run(() =>
        {
            string reshadeIniPath = Path.Combine(ShadePath, "ReShade.ini");
            
            if (!File.Exists(reshadeIniPath))
            {
                _logger.LogWarning("ReShade.ini does not exist: {path}", reshadeIniPath);
                throw new FileNotFoundException(Lang.ResetReShadeIniDialog_ReShadeIniNotFound);
            }

            try
            {
                // 删除现有的ReShade.ini
                File.Delete(reshadeIniPath);
                _logger.LogInformation("Deleted existing ReShade.ini");
                
                // 创建新的默认ReShade.ini
                string defaultIniContent = @"[GENERAL]
EffectSearchPaths=.\reshade-shaders\Shaders\**
TextureSearchPaths=.\reshade-shaders\Textures\**

[INPUT]
KeyOverlay=36,0,0,0
GamepadNavigation=0
";
                File.WriteAllText(reshadeIniPath, defaultIniContent);
                _logger.LogInformation("Created new default ReShade.ini");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset ReShade.ini");
                throw;
            }
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        this.Hide();
    }
}
