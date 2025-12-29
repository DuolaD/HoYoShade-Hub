using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;


namespace HoYoShadeHub.Features.Screenshot;

[INotifyPropertyChanged]
public sealed partial class ScreenshotFolderManageDialog : ContentDialog
{


    private ILogger<ScreenshotFolderManageDialog> _logger = AppConfig.GetLogger<ScreenshotFolderManageDialog>();


    public GameId CurrentGameId { get; set; }


    public List<ScreenshotFolder> Folders { get; set; }





    public ScreenshotFolderManageDialog()
    {
        InitializeComponent();
        Loaded += ScreenshotFolderManageDialog_Loaded;
        Unloaded += ScreenshotFolderManageDialog_Unloaded;
    }




    public ObservableCollection<ScreenshotFolder> ScreenshotFolders { get; set; } = new();


    public bool FolderChanged { get; set; }


    public bool CanSave { get; set => SetProperty(ref field, value); }



    private void ScreenshotFolderManageDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Folders is not null)
            {
                foreach (var item in Folders)
                {
                    ScreenshotFolders.Add(item);
                }
            }
        }
        catch { }
    }



    private void ScreenshotFolderManageDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ScreenshotFolders.Clear();
            ScreenshotFolders = null!;
        }
        catch { }
    }




    [RelayCommand]
    private async Task AddFolderAsync()
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (Directory.Exists(folder))
            {
                if (ScreenshotFolders.FirstOrDefault(x => x.Folder == folder) is null)
                {
                    ScreenshotFolders.Add(new ScreenshotFolder(folder));
                    CanSave = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add screenshot folder.");
        }
    }


    /// <summary>
    /// 备份所有文件夹中的截图到用户数据目录/Screenshots/
    /// </summary>
    [RelayCommand]
    private async Task BackupAllScreenshotsAsync()
    {
        try
        {
            TextBlock_BackupResult.Visibility = Visibility.Collapsed;
            
            if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
            {
                _logger.LogWarning("UserDataFolder is null, cannot backup screenshots.");
                TextBlock_BackupResult.Visibility = Visibility.Visible;
                TextBlock_BackupResult.Text = Lang.ScreenshotFolderManageDialog_FailedToBackupScreenshots;
                return;
            }

            // 目标备份文件夹：用户数据目录/Screenshots/
            string backupFolder = Path.Combine(AppConfig.UserDataFolder, "Screenshots");
            Directory.CreateDirectory(backupFolder);
            
            StackPanel_BackingUp.Visibility = Visibility.Visible;
            
            int totalCount = await Task.Run(() =>
            {
                int count = 0;
                
                // 遍历所有文件夹
                foreach (var screenshotFolder in ScreenshotFolders)
                {
                    if (!Directory.Exists(screenshotFolder.Folder))
                    {
                        continue;
                    }
                    
                    try
                    {
                        var files = Directory.GetFiles(screenshotFolder.Folder);
                        foreach (var sourceFile in files)
                        {
                            // 只备份支持的图片格式
                            if (!ScreenshotHelper.IsSupportedExtension(sourceFile))
                            {
                                continue;
                            }
                            
                            string fileName = Path.GetFileName(sourceFile);
                            string targetFile = Path.Combine(backupFolder, fileName);
                            
                            // 如果目标文件不存在，才进行复制
                            if (!File.Exists(targetFile))
                            {
                                File.Copy(sourceFile, targetFile, false);
                                count++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to backup screenshots from folder: {Folder}", screenshotFolder.Folder);
                    }
                }
                
                return count;
            });
            
            StackPanel_BackingUp.Visibility = Visibility.Collapsed;
            TextBlock_BackupResult.Visibility = Visibility.Visible;
            TextBlock_BackupResult.Text = string.Format(Lang.ScreenshotPage_BackedUpNewScreenshots, totalCount);
            
            _logger.LogInformation("Backed up {Count} screenshots to {BackupFolder}", totalCount, backupFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup all screenshots.");
            StackPanel_BackingUp.Visibility = Visibility.Collapsed;
            TextBlock_BackupResult.Visibility = Visibility.Visible;
            TextBlock_BackupResult.Text = Lang.ScreenshotFolderManageDialog_FailedToBackupScreenshots;
        }
    }



    private async void Button_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ScreenshotFolder folder })
            {
                if (Directory.Exists(folder.Folder))
                {
                    await Launcher.LaunchFolderPathAsync(folder.Folder);
                }
            }
        }
        catch { }
    }



    private void Button_RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: ScreenshotFolder folder })
            {
                ScreenshotFolders.Remove(folder);
                CanSave = true;
            }
        }
        catch { }
    }



    [RelayCommand]
    private void Save()
    {
        try
        {
            FolderChanged = true;
            Folders ??= new();
            Folders.Clear();
            Folders.AddRange(ScreenshotFolders.Where(x => x.CanRemove));
            this.Hide();
        }
        catch
        {
            this.Hide();
        }
    }



    [RelayCommand]
    private void Cancel()
    {
        this.Hide();
    }


}
