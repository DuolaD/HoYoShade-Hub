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
public sealed partial class UninstallShadeDialog : ContentDialog
{
    private readonly ILogger<UninstallShadeDialog> _logger = AppConfig.GetLogger<UninstallShadeDialog>();

    public UninstallShadeDialog()
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
    
    // 警告标题 - 根据ShadeName动态显示
    public string WarningTitle => string.Format(Lang.UninstallShadeDialog_ConfirmUninstallTitle, ShadeName);
    
    public string WarningMessage1 => string.Format(Lang.UninstallShadeDialog_WarningMessage1, ShadeName);
    
    public string WarningMessage2 => Lang.UninstallShadeDialog_WarningMessage2;

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
            
            // ��һ�ε����ȷ�ϲ���
            if (IsFirstConfirmation)
            {
                _logger.LogInformation("First confirmation, changing IsFirstConfirmation to false");
                IsFirstConfirmation = false;
                _logger.LogInformation("After change: IsFirstConfirmation={IsFirstConfirmation}, ShowUninstallButton={ShowUninstallButton}, ShowConfirmButton={ShowConfirmButton}", 
                    IsFirstConfirmation, ShowUninstallButton, ShowConfirmButton);
                return;
            }

            _logger.LogInformation("Second confirmation, starting uninstall");
            
            // 第二次点击执行卸载
            _cancellationTokenSource = new CancellationTokenSource();
            IsUninstalling = true;
            CanUninstall = false;
            IsIndeterminate = true;
            StatusMessage = Lang.UninstallShadeDialog_PreparingUninstall;
            ShowProgressText = false;

            await Task.Delay(500); // 短暂延迟让UI更新

            // 开始卸载
            await PerformUninstallAsync(_cancellationTokenSource.Token);

            IsIndeterminate = false;
            UninstallProgress = 100;
            StatusMessage = Lang.UninstallShadeDialog_UninstallCompleted;
            IsCompleted = true;
            
            await Task.Delay(1000); // 显示完成状态
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
            _logger.LogError(ex, "Uninstall {shade} failed", ShadeName);
            StatusMessage = $"卸载失败: {ex.Message}";
            IsUninstalling = false;
            CanUninstall = true;
            IsIndeterminate = false;
        }
    }

    private async Task PerformUninstallAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ShadePath))
        {
            _logger.LogWarning("Shade path does not exist: {path}", ShadePath);
            return;
        }

        // 获取所有文件和文件数
        var allFiles = Directory.GetFiles(ShadePath, "*", SearchOption.AllDirectories).ToList();
        var totalFiles = allFiles.Count;
        
        if (totalFiles == 0)
        {
            // 没有文件，直接删除目录
            Directory.Delete(ShadePath, true);
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
            // 删除所有文件
            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    deletedFiles++;
                    
                    // 更新进度 - 在UI线程上执行
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

            // 删除所有空文件夹
            cancellationToken.ThrowIfCancellationRequested();
            
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessage = Lang.UninstallShadeDialog_DeletingDirectories;
            });
            
            try
            {
                Directory.Delete(ShadePath, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete directory: {dir}", ShadePath);
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
