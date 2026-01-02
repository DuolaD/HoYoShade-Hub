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
    
    // 警告标题
    public string WarningTitle => $"卸载 {ShadeName} 着色器和插件";
    
    public string WarningMessage1 => $"确定要卸载 {ShadeName} 的着色器和插件吗？";
    
    public string WarningMessage2 => "此操作不会导致预设和游戏截图被删除，卸载后，你可以通过“安装ReShade着色器和插件”重新安装，但此前可供安装的着色器和插件可能不再可用。";

    public bool IsUninstalling { get => field; set => SetProperty(ref field, value); }

    public bool IsIndeterminate { get => field; set => SetProperty(ref field, value); }

    public double UninstallProgress { get => field; set => SetProperty(ref field, value); }

    public string StatusMessage { get => field; set => SetProperty(ref field, value); } = "";

    public string ProgressText { get => field; set => SetProperty(ref field, value); } = "";

    public bool ShowProgressText { get => field; set => SetProperty(ref field, value); }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(ShowUninstallButton))]
    private bool isCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool canUninstall = true;

    public bool CanCancel => !IsUninstalling || IsCompleted;

    public bool ShowUninstallButton => !IsUninstalling && !IsCompleted;
    
    public string CancelButtonText => IsCompleted ? Lang.UninstallShadeDialog_Close : Lang.UninstallShadeDialog_Cancel;
    
    public string UninstallButtonText => Lang.UninstallShadeDialog_Uninstall;

    private CancellationTokenSource? _cancellationTokenSource;

    [RelayCommand]
    private async Task UninstallAsync()
    {
        try
        {
            _logger.LogInformation("UninstallAsync called");
            
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
            _logger.LogError(ex, "Uninstall shaders for {shade} failed", ShadeName);
            StatusMessage = $"卸载失败: {ex.Message}";
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

        // 获取所有文件和文件数
        var allFiles = Directory.GetFiles(shadersPath, "*", SearchOption.AllDirectories).ToList();
        var totalFiles = allFiles.Count;
        
        if (totalFiles == 0)
        {
            // 没有文件，清理所有子文件夹
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

            // 删除所有子文件夹
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
