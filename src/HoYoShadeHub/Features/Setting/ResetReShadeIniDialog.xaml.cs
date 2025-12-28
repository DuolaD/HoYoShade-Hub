using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Language;
using System;
using System.Diagnostics;
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

    [RelayCommand]
    private async Task ResetAsync()
    {
        try
        {
            _logger.LogInformation("ResetAsync called, ShadePath={ShadePath}", ShadePath);

            if (!Directory.Exists(ShadePath))
            {
                _logger.LogWarning("Shade path does not exist: {path}", ShadePath);
                this.Hide();
                return;
            }

            // INIBuild.exe 位于 LauncherResource 子目录下
            string iniBuildPath = Path.Combine(ShadePath, "LauncherResource", "INIBuild.exe");

            if (File.Exists(iniBuildPath))
            {
                _logger.LogInformation("Running INIBuild.exe at {path}", iniBuildPath);

                var processInfo = new ProcessStartInfo
                {
                    FileName = iniBuildPath,
                    WorkingDirectory = ShadePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(processInfo);
                
                if (process == null)
                {
                    _logger.LogError("Failed to start INIBuild.exe - Process.Start returned null");
                }
                else
                {
                    _logger.LogInformation("INIBuild.exe started successfully, PID: {pid}", process.Id);

                    // 读取输出
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // 等待进程完成
                    await process.WaitForExitAsync();

                    string output = await outputTask;
                    string error = await errorTask;

                    _logger.LogInformation("INIBuild.exe completed with exit code: {ExitCode}", process.ExitCode);

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        _logger.LogInformation("INIBuild.exe output: {output}", output);
                    }

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        _logger.LogWarning("INIBuild.exe error: {error}", error);
                    }
                }
            }
            else
            {
                _logger.LogWarning("INIBuild.exe not found at {path}", iniBuildPath);
            }

            // 关闭弹窗
            this.Hide();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset ReShade.ini failed");
            // 即使出错也关闭弹窗
            this.Hide();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        this.Hide();
    }
}
