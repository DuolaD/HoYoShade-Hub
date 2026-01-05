using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HoYoShadeHub.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.System;

namespace HoYoShadeHub.Features.GameLauncher;

public partial class GameInstallPathItemDialog : ObservableObject
{
    private readonly GameLauncherSettingDialog _dialog;
    private readonly GameBiz _gameBiz;
    private readonly int _index;

    public GameInstallPathItemDialog(GameLauncherSettingDialog dialog, GameBiz gameBiz, int index)
    {
        _dialog = dialog;
        _gameBiz = gameBiz;
        _index = index;
    }

    [ObservableProperty]
    private string _path = string.Empty;
    
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isValid = true;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsSelected) && _isSelected)
        {
            _dialog.OnPathSelected(this);
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        var fullPath = GameLauncherService.GetFullPathIfRelativePath(_path);
        if (Directory.Exists(fullPath))
        {
            await Launcher.LaunchUriAsync(new Uri(fullPath)).AsTask();
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        await _dialog.RemoveGameInstallPathAsync(this);
    }
}
