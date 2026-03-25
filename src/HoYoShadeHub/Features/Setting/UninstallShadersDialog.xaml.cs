using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Language;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Setting;

[INotifyPropertyChanged]
public sealed partial class UninstallShadersDialog : ContentDialog
{
    private readonly ILogger<UninstallShadersDialog> _logger = AppConfig.GetLogger<UninstallShadersDialog>();

    public UninstallShadersDialog()
    {
        this.InitializeComponent();
    }

    private string _shadePath = "";
    public string ShadePath
    {
        get => _shadePath;
        set
        {
            if (SetProperty(ref _shadePath, value))
            {
                OnPropertyChanged(nameof(WarningTitle));
                OnPropertyChanged(nameof(WarningMessage1));
            }
        }
    }

    private string _shadeName = "";
    public string ShadeName
    {
        get => _shadeName;
        set
        {
            if (SetProperty(ref _shadeName, value))
            {
                OnPropertyChanged(nameof(WarningTitle));
                OnPropertyChanged(nameof(WarningMessage1));
            }
        }
    }

    public string WarningTitle => string.Format(Lang.UninstallShadersDialog_Title, ShadeName);

    public string WarningMessage1 => string.Format(Lang.UninstallShadersDialog_Message1, ShadeName);

    public string WarningMessage2 => Lang.UninstallShadersDialog_Message2;

    public bool IsUninstalling { get => field; set => SetProperty(ref field, value); }

    public bool IsIndeterminate { get => field; set => SetProperty(ref field, value); }

    public double UninstallProgress { get => field; set => SetProperty(ref field, value); }

    public string StatusMessage { get => field; set => SetProperty(ref field, value); } = "";

    public string ProgressText { get => field; set => SetProperty(ref field, value); } = "";

    public bool ShowProgressText { get => field; set => SetProperty(ref field, value); }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(ShowUninstallButton))]
    [NotifyPropertyChangedFor(nameof(ShowConfirmButton))]
    private bool isCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UninstallButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowUninstallButton))]
    [NotifyPropertyChangedFor(nameof(ShowConfirmButton))]
    private bool isFirstConfirmation = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool canUninstall = true;

    public bool CanCancel => !IsUninstalling || IsCompleted;

    public bool ShowUninstallButton => !IsUninstalling && !IsCompleted && IsFirstConfirmation;

    public bool ShowConfirmButton => !IsUninstalling && !IsCompleted && !IsFirstConfirmation;

    public string CancelButtonText => IsCompleted ? Lang.UninstallShadeDialog_Close : Lang.UninstallShadeDialog_Cancel;

    public string UninstallButtonText => IsFirstConfirmation ? Lang.UninstallShadeDialog_Uninstall : Lang.UninstallShadeDialog_ConfirmUninstall;

    private CancellationTokenSource? _cancellationTokenSource;

    [RelayCommand]
    private async Task UninstallAsync()
    {
        try
        {
            _logger.LogInformation("UninstallAsync called, IsFirstConfirmation={IsFirstConfirmation}", IsFirstConfirmation);

            if (IsFirstConfirmation)
            {
                IsFirstConfirmation = false;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            IsUninstalling = true;
            CanUninstall = false;
            IsIndeterminate = true;
            StatusMessage = Lang.UninstallShadeDialog_PreparingUninstall;
            ShowProgressText = false;

            await Task.Delay(500);

            await PerformUninstallAsync(_cancellationTokenSource.Token);

            IsIndeterminate = false;
            UninstallProgress = 100;
            StatusMessage = Lang.UninstallShadeDialog_UninstallCompleted;
            IsCompleted = true;

            await Task.Delay(1000);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Uninstall cancelled by user");
            StatusMessage = Lang.UninstallShadeDialog_UninstallCancelled;
            IsUninstalling = false;
            CanUninstall = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall shaders for {shade} failed", ShadeName);
            StatusMessage = $"\u5378\u8F7D\u5931\u8D25: {ex.Message}";
            IsUninstalling = false;
            CanUninstall = true;
            IsIndeterminate = false;
        }
    }

    private async Task PerformUninstallAsync(CancellationToken cancellationToken)
    {
        string shadersPath = Path.Combine(ShadePath, "reshade-shaders");

        if (!Directory.Exists(shadersPath))
        {
            _logger.LogWarning("Shaders path does not exist: {path}", shadersPath);
            return;
        }

        var allFiles = Directory.GetFiles(shadersPath, "*", SearchOption.AllDirectories).ToList();
        var totalFiles = allFiles.Count;

        if (totalFiles == 0)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(shadersPath))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean empty directories in: {dir}", shadersPath);
            }
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            IsIndeterminate = false;
            ShowProgressText = true;
        });

        int deletedFiles = 0;

        await Task.Run(() =>
        {
            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    deletedFiles++;

                    var currentProgress = (double)deletedFiles / totalFiles * 100;
                    var progressText = $"{currentProgress:F1}%";
                    var statusMsg = string.Format(Lang.UninstallShadeDialog_DeletingFiles, deletedFiles, totalFiles);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UninstallProgress = currentProgress;
                        ProgressText = progressText;
                        StatusMessage = statusMsg;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {file}", file);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessage = Lang.UninstallShadeDialog_DeletingDirectories;
            });

            try
            {
                foreach (var dir in Directory.GetDirectories(shadersPath))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete subdirectories in: {dir}", shadersPath);
            }
        }, cancellationToken);
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsCompleted)
        {
            this.Hide();
        }
        else if (!IsUninstalling)
        {
            this.Hide();
        }
        else
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}
