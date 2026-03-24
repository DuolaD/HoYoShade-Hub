using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;

namespace HoYoShadeHub.Features.GameLauncher;

[INotifyPropertyChanged]
public sealed partial class UninstallReShadeClientDialog : ContentDialog
{
    public UninstallReShadeClientDialog()
    {
        this.InitializeComponent();
    }

    public string DialogTitleText => "卸载/还原ReShade改动";

    public string WarningMessage1 => "您确定要卸载/还原当前客户端的ReShade改动吗？";

    public string WarningMessage2 => "这将尝试删除当前客户端中与 ReShade 相关的改动内容。";

    public string WarningMessage3 => "执行后可能无法恢复，请在操作前先完成备份。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUninstallButton))]
    [NotifyPropertyChangedFor(nameof(ShowConfirmButton))]
    private bool isFirstConfirmation = true;

    public bool ShowUninstallButton => IsFirstConfirmation;

    public bool ShowConfirmButton => !IsFirstConfirmation;

    public bool IsConfirmed { get; private set; }

    [RelayCommand]
    private void Uninstall()
    {
        if (IsFirstConfirmation)
        {
            IsFirstConfirmation = false;
            return;
        }

        IsConfirmed = true;
        this.Hide();
    }

    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        this.Hide();
    }
}
