using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace HoYoShadeHub.Features.Setting;

[INotifyPropertyChanged]
public sealed partial class ShadeBackupRestoreDialog : ContentDialog
{
    private readonly ILogger<ShadeBackupRestoreDialog> _logger = AppConfig.GetLogger<ShadeBackupRestoreDialog>();

    public ShadeBackupRestoreDialog()
    {
        this.InitializeComponent();
    }

    #region 基本属性与模式切换

    private string _shadePath = string.Empty;
    public string ShadePath
    {
        get => _shadePath;
        set
        {
            if (SetProperty(ref _shadePath, value))
            {
                OnPropertyChanged(nameof(CurrentPathText));
            }
        }
    }

    private string _shadeName = string.Empty;
    public string ShadeName
    {
        get => _shadeName;
        set
        {
            if (SetProperty(ref _shadeName, value))
            {
                OnPropertyChanged(nameof(DialogTitle));
            }
        }
    }

    public string FrameworkVersion { get; set; } = string.Empty;

    public string ReShadeVersion { get; set; } = string.Empty;

    /// <summary>
    /// 是否执行了还原操作（供调用方判断是否需要刷新占用大小）
    /// </summary>
    public bool DidRestore { get; private set; }

    public string DialogTitle => string.Format(Lang.BackupRestoreDialog_TitleFormat, ShadeName);

    public string CurrentPathText => string.Format(Lang.BackupRestoreDialog_CurrentPathFormat, ShadePath);

    private int _currentTab = 0; // 0: 备份, 1: 还原
    public int CurrentTab
    {
        get => _currentTab;
        set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsBackupTabVisible));
                OnPropertyChanged(nameof(IsRestoreTabVisible));
                OnPropertyChanged(nameof(BackupTabBackground));
                OnPropertyChanged(nameof(RestoreTabBackground));
                ClearError();
            }
        }
    }

    public bool IsBackupTabVisible => CurrentTab == 0;
    public bool IsRestoreTabVisible => CurrentTab == 1;

    public Brush BackupTabBackground => CurrentTab == 0
        ? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
        : new SolidColorBrush(Colors.Transparent);

    public Brush RestoreTabBackground => CurrentTab == 1
        ? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
        : new SolidColorBrush(Colors.Transparent);

    [RelayCommand]
    private void SwitchToBackupTab()
    {
        if (!IsOperating)
        {
            CurrentTab = 0;
        }
    }

    [RelayCommand]
    private void SwitchToRestoreTab()
    {
        if (!IsOperating)
        {
            CurrentTab = 1;
        }
    }

    #endregion

    #region 导出备份属性与命令

    private bool _backupPresets = true;
    public bool BackupPresets
    {
        get => _backupPresets;
        set
        {
            if (SetProperty(ref _backupPresets, value))
            {
                OnPropertyChanged(nameof(CanExecuteBackup));
            }
        }
    }

    private bool _backupReShadeIni = true;
    public bool BackupReShadeIni
    {
        get => _backupReShadeIni;
        set
        {
            if (SetProperty(ref _backupReShadeIni, value))
            {
                OnPropertyChanged(nameof(CanExecuteBackup));
            }
        }
    }

    private bool _backupShaders = false;
    public bool BackupShaders
    {
        get => _backupShaders;
        set
        {
            if (SetProperty(ref _backupShaders, value))
            {
                OnPropertyChanged(nameof(CanExecuteBackup));
            }
        }
    }

    private bool _backupScreenshots = false;
    public bool BackupScreenshots
    {
        get => _backupScreenshots;
        set
        {
            if (SetProperty(ref _backupScreenshots, value))
            {
                OnPropertyChanged(nameof(CanExecuteBackup));
            }
        }
    }

    public bool CanExecuteBackup => !IsOperating && (BackupPresets || BackupReShadeIni || BackupShaders || BackupScreenshots);

    private bool _isBackupSuccess = false;
    public bool IsBackupSuccess
    {
        get => _isBackupSuccess;
        set
        {
            if (SetProperty(ref _isBackupSuccess, value))
            {
                OnPropertyChanged(nameof(CloseButtonText));
            }
        }
    }

    private string _lastBackupSavedPath = string.Empty;
    public string LastBackupSavedPath
    {
        get => _lastBackupSavedPath;
        set => SetProperty(ref _lastBackupSavedPath, value);
    }

    [RelayCommand]
    private async Task StartBackupAsync()
    {
        if (!CanExecuteBackup)
        {
            SetError(Lang.BackupRestoreDialog_NoComponentsSelected);
            return;
        }

        ClearError();
        IsBackupSuccess = false;

        try
        {
            string suggestedFileName = $"{ShadeName}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string? destinationPath = await FileDialogHelper.OpenSaveFileDialogAsync(
                this.XamlRoot,
                suggestedFileName,
                (Lang.BackupRestoreDialog_ZipFilterName, ".zip"));

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return; // 用户取消
            }

            IsOperating = true;
            IsIndeterminate = false;
            OperatingProgress = 0;
            OperatingStatusText = Lang.BackupRestoreDialog_BackingUp;

            var options = new ShadeBackupOptions
            {
                BackupPresets = BackupPresets,
                BackupReShadeIni = BackupReShadeIni,
                BackupShaders = BackupShaders,
                BackupScreenshots = BackupScreenshots,
                FrameworkVersion = FrameworkVersion,
                ReShadeVersion = ReShadeVersion
            };

            var progress = new Progress<(double Progress, string Status)>(report =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    OperatingProgress = report.Progress;
                    OperatingStatusText = report.Status;
                });
            });

            await ShadeBackupRestoreService.ExportBackupAsync(
                ShadePath,
                ShadeName,
                destinationPath,
                options,
                progress);

            LastBackupSavedPath = destinationPath;
            IsBackupSuccess = true;
            OperatingStatusText = string.Empty;

            InAppToast.MainWindow?.Success(Lang.BackupRestoreDialog_BackupSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export backup failed for {ShadeName}", ShadeName);
            SetError(string.Format(Lang.BackupRestoreDialog_ErrorFormat, ex.Message));
        }
        finally
        {
            IsOperating = false;
        }
    }

    [RelayCommand]
    private async Task OpenLastBackupFolderAsync()
    {
        if (!string.IsNullOrWhiteSpace(LastBackupSavedPath) && File.Exists(LastBackupSavedPath))
        {
            try
            {
                var item = await StorageFile.GetFileFromPathAsync(LastBackupSavedPath);
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(LastBackupSavedPath));
                var options = new FolderLauncherOptions
                {
                    ItemsToSelect = { item }
                };
                await Launcher.LaunchFolderAsync(folder, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Open last backup folder failed");
            }
        }
    }

    #endregion

    #region 导入还原属性与命令

    private string _selectedBackupFilePath = string.Empty;
    public string SelectedBackupFilePath
    {
        get => _selectedBackupFilePath;
        set
        {
            if (SetProperty(ref _selectedBackupFilePath, value))
            {
                OnPropertyChanged(nameof(HasSelectedFile));
                OnPropertyChanged(nameof(HasSelectedFileNot));
                OnPropertyChanged(nameof(SelectedFileName));
            }
        }
    }

    public string SelectedFileName => Path.GetFileName(SelectedBackupFilePath);

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedBackupFilePath);
    public bool HasSelectedFileNot => !HasSelectedFile;

    private ShadeBackupManifest? _selectedManifest;
    public ShadeBackupManifest? SelectedManifest
    {
        get => _selectedManifest;
        set
        {
            if (SetProperty(ref _selectedManifest, value))
            {
                OnPropertyChanged(nameof(HasValidPackage));
                OnPropertyChanged(nameof(CanExecuteRestore));
                OnPropertyChanged(nameof(PackageSourceText));
                OnPropertyChanged(nameof(PackageCreatedText));
                OnPropertyChanged(nameof(PackagePresetsText));
                OnPropertyChanged(nameof(PackageShadersText));
                OnPropertyChanged(nameof(PackageConfigText));
                OnPropertyChanged(nameof(PackageScreenshotsText));
                OnPropertyChanged(nameof(CanRestoreReShadeIni));
            }
        }
    }

    public bool HasValidPackage => SelectedManifest != null && SelectedManifest.IsValid;

    public string PackageSourceText => string.Format(Lang.BackupRestoreDialog_PackageSourceFormat, SelectedManifest?.SourceFramework ?? "-");
    public string PackageCreatedText => string.Format(Lang.BackupRestoreDialog_PackageCreatedFormat, SelectedManifest?.CreatedAt.ToString("yyyy-MM-dd HH:mm") ?? "-");
    public string PackagePresetsText => string.Format(Lang.BackupRestoreDialog_PackagePresetsFormat, SelectedManifest?.PresetCount ?? 0);
    public string PackageShadersText => string.Format(Lang.BackupRestoreDialog_PackageShadersFormat, SelectedManifest?.IncludesShaders == true ? Lang.Common_Yes : Lang.Common_No);
    public string PackageConfigText => string.Format(Lang.BackupRestoreDialog_PackageConfigFormat, SelectedManifest?.IncludesReShadeIni == true ? Lang.Common_Yes : Lang.Common_No);
    public string PackageScreenshotsText => string.Format(Lang.BackupRestoreDialog_PackageScreenshotsFormat, SelectedManifest?.IncludesScreenshots == true ? Lang.Common_Yes : Lang.Common_No);

    private int _conflictResolutionIndex = 0; // 0: Rename, 1: Overwrite
    public int ConflictResolutionIndex
    {
        get => _conflictResolutionIndex;
        set => SetProperty(ref _conflictResolutionIndex, value);
    }

    private bool _restoreReShadeIni = true;
    public bool RestoreReShadeIni
    {
        get => _restoreReShadeIni;
        set => SetProperty(ref _restoreReShadeIni, value);
    }

    public bool CanRestoreReShadeIni => IsOperatingNot && SelectedManifest?.IncludesReShadeIni == true;

    private bool _createSafetySnapshot = true;
    public bool CreateSafetySnapshot
    {
        get => _createSafetySnapshot;
        set => SetProperty(ref _createSafetySnapshot, value);
    }

    public bool CanExecuteRestore => !IsOperating && HasValidPackage;

    private bool _isRestoreSuccess = false;
    public bool IsRestoreSuccess
    {
        get => _isRestoreSuccess;
        set
        {
            if (SetProperty(ref _isRestoreSuccess, value))
            {
                OnPropertyChanged(nameof(CloseButtonText));
            }
        }
    }

    [RelayCommand]
    private async Task SelectBackupFileAsync()
    {
        ClearError();
        IsRestoreSuccess = false;

        try
        {
            string? filePath = await FileDialogHelper.PickSingleFileAsync(
                this.XamlRoot,
                (Lang.BackupRestoreDialog_ZipFilterName, ".zip"));

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            SelectedBackupFilePath = filePath;
            var manifest = await ShadeBackupRestoreService.InspectBackupPackageAsync(filePath);
            SelectedManifest = manifest;

            if (manifest == null || !manifest.IsValid)
            {
                SetError(Lang.BackupRestoreDialog_InvalidPackage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspect backup package failed");
            SetError(string.Format(Lang.BackupRestoreDialog_ErrorFormat, ex.Message));
        }
    }

    [RelayCommand]
    private async Task StartRestoreAsync()
    {
        if (!CanExecuteRestore || string.IsNullOrWhiteSpace(SelectedBackupFilePath))
        {
            return;
        }

        ClearError();
        IsRestoreSuccess = false;

        try
        {
            IsOperating = true;
            IsIndeterminate = false;
            OperatingProgress = 0;
            OperatingStatusText = Lang.BackupRestoreDialog_Restoring;

            var options = new ShadeRestoreOptions
            {
                ConflictResolution = ConflictResolutionIndex == 1
                    ? PresetConflictResolution.Overwrite
                    : PresetConflictResolution.Rename,
                RestoreReShadeIni = RestoreReShadeIni && SelectedManifest?.IncludesReShadeIni == true,
                CreateSafetySnapshot = CreateSafetySnapshot,
                BackupBaseFolder = AppConfig.UserDataFolder
            };

            var progress = new Progress<(double Progress, string Status)>(report =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    OperatingProgress = report.Progress;
                    OperatingStatusText = report.Status;
                });
            });

            await ShadeBackupRestoreService.RestoreBackupAsync(
                SelectedBackupFilePath,
                ShadePath,
                ShadeName,
                options,
                progress);

            DidRestore = true;
            IsRestoreSuccess = true;
            OperatingStatusText = string.Empty;

            InAppToast.MainWindow?.Success(Lang.BackupRestoreDialog_RestoreSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore backup failed for {ShadeName}", ShadeName);
            SetError(string.Format(Lang.BackupRestoreDialog_ErrorFormat, ex.Message));
        }
        finally
        {
            IsOperating = false;
        }
    }

    #endregion

    #region 通用操作进度与底部按钮

    private bool _isOperating = false;
    public bool IsOperating
    {
        get => _isOperating;
        set
        {
            if (SetProperty(ref _isOperating, value))
            {
                OnPropertyChanged(nameof(IsOperatingNot));
                OnPropertyChanged(nameof(CanExecuteBackup));
                OnPropertyChanged(nameof(CanExecuteRestore));
                OnPropertyChanged(nameof(CanRestoreReShadeIni));
            }
        }
    }

    public bool IsOperatingNot => !IsOperating;

    private double _operatingProgress = 0;
    public double OperatingProgress
    {
        get => _operatingProgress;
        set => SetProperty(ref _operatingProgress, value);
    }

    private bool _isIndeterminate = false;
    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set => SetProperty(ref _isIndeterminate, value);
    }

    private string _operatingStatusText = string.Empty;
    public string OperatingStatusText
    {
        get => _operatingStatusText;
        set => SetProperty(ref _operatingStatusText, value);
    }

    private string _statusErrorMessage = string.Empty;
    public string StatusErrorMessage
    {
        get => _statusErrorMessage;
        set
        {
            if (SetProperty(ref _statusErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasErrorMessage));
            }
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(StatusErrorMessage);

    public string CloseButtonText => (IsBackupSuccess || IsRestoreSuccess)
        ? Lang.BackupRestoreDialog_Close
        : Lang.BackupRestoreDialog_Cancel;

    private void SetError(string message)
    {
        StatusErrorMessage = message;
    }

    private void ClearError()
    {
        StatusErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsOperating)
        {
            this.Hide();
        }
    }

    #endregion
}
