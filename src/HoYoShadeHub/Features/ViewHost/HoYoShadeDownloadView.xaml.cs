using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using NuGet.Versioning;
using HoYoShadeHub.Core.Metadata.Github;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Features.Setting;
using HoYoShadeHub.Language;
using HoYoShadeHub.RPC;
using HoYoShadeHub.RPC.HoYoShadeInstall;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using HoYoShadeHub.Helpers;

namespace HoYoShadeHub.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class HoYoShadeDownloadView : UserControl
{
    public HoYoShadeDownloadView()
    {
        this.InitializeComponent();
        DownloadServers = new ObservableCollection<string>();
        PauseResumeButtonText = Lang.HoYoShadeDownloadView_Pause;
        _versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
        
        // Register for installation change messages from other views/windows
        WeakReferenceMessenger.Default.Register<HoYoShadeInstallationChangedMessage>(this, (r, m) => OnInstallationChanged());
        
        Versions.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(CanDownload));
            ImportFromLocalCommand.NotifyCanExecuteChanged();
        };
        UpdateDownloadServers();
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
    }

    private void OnLanguageChanged()
    {
        // Update download servers list
        UpdateDownloadServers();
        
        // Update latest version tag localization
        UpdateLatestVersionTagLocalization();
         
         // Refresh status message based on current state
         if (!IsDownloading && !CanStart && Versions.Count > 0)
         {
             StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
         }
         
         // Refresh pause/resume button text if downloading
         if (IsDownloading && !_isPaused)
         {
            PauseResumeButtonText = Lang.HoYoShadeDownloadView_Pause;
         }
         else if (_isPaused)
         {
             PauseResumeButtonText = Lang.HoYoShadeDownloadView_Resume;
         }
     }

    private void UpdateLatestVersionTagLocalization()
    {
        bool isFirst = true;
        foreach (var release in Versions)
        {
            release.LatestVersionTag = isFirst ? Lang.HoYoShadeDownloadView_LatestVersion : string.Empty;
            isFirst = false;
        }
    }
 
     private void UpdateDownloadServers()
     {
         var selectedIndex = DownloadServers.IndexOf(SelectedDownloadServer);
         DownloadServers.Clear();
         DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_GithubDirect);
         DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_Cloudflare);
         DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_TencentCloud);
         DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_AlibabaCloud);
         if (selectedIndex >= 0 && selectedIndex < DownloadServers.Count)
         {
             SelectedDownloadServer = DownloadServers[selectedIndex];
         }
         else
         {
             SelectedDownloadServer = DownloadServers[0];
         }
     }

    public ObservableCollection<GithubRelease> Versions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private GithubRelease selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    [NotifyPropertyChangedFor(nameof(IsHoYoShadeSelectionEnabled))]
    [NotifyPropertyChangedFor(nameof(IsOpenHoYoShadeSelectionEnabled))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private bool isUpdateMode;

    public string Title => IsUpdateMode ? "让我们更新 HoYoShade 框架" : Lang.HoYoShadeDownloadView_Title;

    // Allow selection always (unless downloading) so users can repair/reinstall if needed
    public bool IsHoYoShadeSelectionEnabled => !IsDownloading;
    public bool IsOpenHoYoShadeSelectionEnabled => !IsDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    private bool isHoYoShadeSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    private bool isOpenHoYoShadeSelected;

    public ObservableCollection<string> DownloadServers { get; }

    [ObservableProperty]
    private string selectedDownloadServer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private bool isDownloading;

    public bool CanDownload => !IsDownloading && SelectedVersion != null && !IsLoadingVersions &&
        ((IsHoYoShadeSelected && (!IsHoYoShadeInstalled || CanInstallVersion(SelectedVersion?.TagName, InstalledHoYoShadeVersion))) || 
         (IsOpenHoYoShadeSelected && (!IsOpenHoYoShadeInstalled || CanInstallVersion(SelectedVersion?.TagName, InstalledOpenHoYoShadeVersion)))) &&
        (IsUpdateMode || !IsHoYoShadeInstalled || !IsOpenHoYoShadeInstalled);
    
    public string DownloadButtonText => _isPaused ? Lang.HoYoShadeDownloadView_Resume : Lang.HoYoShadeDownloadView_DownloadAndInstall;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private string speedAndProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    [NotifyPropertyChangedFor(nameof(ShowPauseResumeButton))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(ShowStopButton))]
    private bool isControlButtonsVisible;
    
    public bool ShowRefreshButton => !IsControlButtonsVisible && !(IsHoYoShadeInstalled && IsOpenHoYoShadeInstalled && !IsUpdateMode);
    
    // Show pause/resume button when control buttons are visible AND not paused
    public bool ShowPauseResumeButton => IsControlButtonsVisible && !_isPaused;
    
    // Stop button can be used both when downloading and when paused
    public bool CanStop => IsControlButtonsVisible;
    
    public bool ShowStopButton => IsControlButtonsVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPauseResumeButton))]
    private string pauseResumeButtonText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    private bool canStart;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshVersionsCommand))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    private bool isLoadingVersions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    [NotifyPropertyChangedFor(nameof(IsHoYoShadeSelectionEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    private bool isHoYoShadeInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    [NotifyPropertyChangedFor(nameof(IsOpenHoYoShadeSelectionEnabled))]
    [NotifyCanExecuteChangedFor(nameof(ImportFromLocalCommand))]
    private bool isOpenHoYoShadeInstalled;

    [ObservableProperty]
    private bool enablePreviewChannel;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoYoShadeVersionDisplay))]
    private string? installedHoYoShadeVersion;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenHoYoShadeVersionDisplay))]
    private string? installedOpenHoYoShadeVersion;

    public string HoYoShadeVersionDisplay => string.IsNullOrEmpty(InstalledHoYoShadeVersion) ? "" : " " + InstalledHoYoShadeVersion;
    public string OpenHoYoShadeVersionDisplay => string.IsNullOrEmpty(InstalledOpenHoYoShadeVersion) ? "" : " " + InstalledOpenHoYoShadeVersion;

    partial void OnEnablePreviewChannelChanged(bool value)
    {
        _ = LoadVersionsAsync();
    }

    public bool CanRefresh => !IsDownloading;
    
    // Can import if:
    // 1. Not currently downloading
    // 2. Versions list has been loaded
    // 3. At least one framework is not installed (regardless of checkbox selection)
    public bool CanImport => !IsDownloading && Versions.Count > 0 && 
        (!IsHoYoShadeInstalled || !IsOpenHoYoShadeInstalled);

    private bool _languageInitialized;

    private async void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        InitializeLanguageSelector();
        _versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
        await LoadInstalledVersionsAsync();
        CheckInstallationStatus();
        _ = LoadVersionsAsync();
        // Notify dependencies
        OnPropertyChanged(nameof(CanDownload));
        OnPropertyChanged(nameof(CanImport));
        ImportFromLocalCommand.NotifyCanExecuteChanged();
        
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
    }

    private void InitializeLanguageSelector()
    {
        try
        {
            var lang = AppConfig.Language;
            ComboBox_Language.Items.Clear();
            ComboBox_Language.Items.Add(new ComboBoxItem
            {
                Content = Lang.ResourceManager.GetString(nameof(Lang.SettingPage_FollowSystem), CultureInfo.InstalledUICulture),
                Tag = "",
            });
            ComboBox_Language.SelectedIndex = 0;
            foreach (var (Title, LangCode) in Localization.LanguageList)
            {
                var box = new ComboBoxItem
                {
                    Content = Title,
                    Tag = LangCode,
                };
                ComboBox_Language.Items.Add(box);
                if (LangCode == lang)
                {
                    ComboBox_Language.SelectedItem = box;
                }
            }
        }
        finally
        {
            _languageInitialized = true;
        }
    }

    private void ComboBox_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ComboBox_Language.SelectedItem is ComboBoxItem item)
            {
                if (_languageInitialized)
                {
                    var lang = item.Tag as string;
                    AppConfig.Language = lang;
                    if (string.IsNullOrWhiteSpace(lang))
                    {
                        CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
                    }
                    else
                    {
                        CultureInfo.CurrentUICulture = new CultureInfo(lang);
                    }
                    // Ensure Lang uses the current culture
                    Lang.Culture = CultureInfo.CurrentUICulture;
                    this.Bindings.Update();
                    WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
                    AppConfig.SaveConfiguration();
                    // Note: UpdateDownloadServers() and other updates are now handled in OnLanguageChanged()
                }
            }
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
            Lang.Culture = CultureInfo.CurrentUICulture;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            _loadVersionsCts?.Cancel();
            _loadVersionsCts = new CancellationTokenSource();
            
            IsLoadingVersions = true;
            StatusMessage = Lang.HoYoShadeDownloadView_StatusFetchingReleases;
            
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");
            
            string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
            
            // Apply proxy based on selected server
            int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
            string? proxyUrl = CloudProxyManager.GetProxyUrl(serverIndex);
            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                apiUrl = CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);
            }
            
            var releases = await client.GetFromJsonAsync<GithubRelease[]>(apiUrl, _loadVersionsCts.Token);
            
            if (releases != null)
            {
                Versions.Clear();
                bool isFirst = true;
                foreach (var release in releases)
                {
                    // Filter: only allow V3 and above
                    if (IsVersionV3OrAbove(release.TagName))
                    {
                        // Filter pre-release versions based on EnablePreviewChannel toggle
                        if (!release.Prerelease || EnablePreviewChannel)
                        {
                            // Mark the first version as latest
                            if (isFirst)
                            {
                                release.LatestVersionTag = Lang.HoYoShadeDownloadView_LatestVersion;
                                isFirst = false;
                            }
                            Versions.Add(release);
                        }
                    }
                }
                if (Versions.Count > 0)
                {
                    SelectedVersion = Versions[0];
                }
            }
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(CanDownload));
            ImportFromLocalCommand.NotifyCanExecuteChanged();
            StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
        }
        catch (OperationCanceledException)
        {
            // Cancelled by user clicking refresh again
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusFailedToLoadVersions, ex.Message);
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    /// <summary>
    /// Check if a version tag is V3 or above
    /// </summary>
    /// <param name="tagName">Version tag name (e.g., "V3.0.1", "v3.1", "V2.9")</param>
    /// <returns>True if version is V3 or above, false otherwise</returns>
    private bool IsVersionV3OrAbove(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        // Remove 'v' or 'V' prefix and any whitespace
        string versionStr = tagName.TrimStart('v', 'V').Trim();
        
        // Try to parse the major version number
        // Split by '.' and get the first part (major version)
        var parts = versionStr.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out int majorVersion))
        {
            return majorVersion >= 3;
        }
        
        // If we can't parse it, be conservative and exclude it
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        if (SelectedVersion == null) return;
        
        try
        {
            _downloadCts = new CancellationTokenSource();
            _isPaused = false; // Reset pause state when starting/resuming download
            IsDownloading = true;
            IsControlButtonsVisible = true;
            PauseResumeButtonText = Lang.HoYoShadeDownloadView_Pause;
            OnPropertyChanged(nameof(ShowPauseResumeButton));

            // Download HoYoShade if selected and can install
            if (IsHoYoShadeSelected && (!IsHoYoShadeInstalled || CanInstallVersion(SelectedVersion?.TagName, InstalledHoYoShadeVersion)))
            {
                await DownloadShadeVariantAsync("HoYoShade", "HoYoShade");
                if (_downloadCts.IsCancellationRequested) return;
            }

            // Download OpenHoYoShade if selected and can install
            if (IsOpenHoYoShadeSelected && (!IsOpenHoYoShadeInstalled || CanInstallVersion(SelectedVersion?.TagName, InstalledOpenHoYoShadeVersion)))
            {
                await DownloadShadeVariantAsync("OpenHoYoShade", "OpenHoYoShade");
                if (_downloadCts.IsCancellationRequested) return;
            }

            // All downloads completed successfully
            StatusMessage = Lang.HoYoShadeDownloadView_StatusFinished;
            
            // Reload installed versions after successful installation
            await LoadInstalledVersionsAsync();
            CheckInstallationStatus(); // Update installation status
            
            // Notify other pages that HoYoShade installation has changed
            WeakReferenceMessenger.Default.Send(new HoYoShadeInstallationChangedMessage());
            
            IsControlButtonsVisible = false;
            SpeedAndProgress = "";
            IsDownloading = false;
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            if (_isPaused)
            {
                StatusMessage = Lang.DownloadGamePage_Paused;
                SpeedAndProgress = "";
            }
            else if (_isStopped)
            {
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                DownloadProgress = 0;
                SpeedAndProgress = "";
                IsControlButtonsVisible = false;
                _isStopped = false;
            }
            else
            {
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                DownloadProgress = 0;
                SpeedAndProgress = "";
                IsControlButtonsVisible = false;
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            IsDownloading = false;
            if (_isPaused)
            {
                StatusMessage = Lang.DownloadGamePage_Paused;
                SpeedAndProgress = "";
            }
            else if (_isStopped)
            {
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                DownloadProgress = 0;
                SpeedAndProgress = "";
                IsControlButtonsVisible = false;
                _isStopped = false;
            }
            else
            {
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                DownloadProgress = 0;
                SpeedAndProgress = "";
                IsControlButtonsVisible = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusError, ex.Message);
            IsDownloading = false;
            IsControlButtonsVisible = false;
        }
    }

    private async Task DownloadShadeVariantAsync(string keyword, string folderName)
    {
        string assetUrl = null;
        if (SelectedVersion.Assets != null)
        {
            var asset = SelectedVersion.Assets.FirstOrDefault(a => a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip"));
            
            // Fallback for HoYoShade
            if (asset == null && keyword == "HoYoShade")
            {
                asset = SelectedVersion.Assets.FirstOrDefault(a => !a.Name.Contains("OpenHoYoShade", StringComparison.OrdinalIgnoreCase) && a.Name.EndsWith(".zip"));
            }
            assetUrl = asset?.BrowserDownloadUrl;
        }

        if (string.IsNullOrEmpty(assetUrl))
        {
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusAssetNotFound + " ({0})", keyword);
            return;
        }

        // Apply proxy based on selected server
        int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
        string? proxyUrl = CloudProxyManager.GetProxyUrl(serverIndex);
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            assetUrl = CloudProxyManager.ApplyProxy(assetUrl, proxyUrl);
        }

        // Check if this is an update (existing installation)
        var targetPath = System.IO.Path.Combine(AppConfig.UserDataFolder, folderName);
        var presetsPath = System.IO.Path.Combine(targetPath, "Presets");
        bool isUpdate = Directory.Exists(presetsPath);
        
        // Determine presets handling option
        int presetsHandling = 0; // 0: Overwrite (default)
        string versionTag = SelectedVersion?.TagName ?? "";
        
        if (isUpdate && IsUpdateMode)
        {
            // Show dialog to ask user how to handle presets
            var (cancelled, option) = await PresetsHandlingDialog.ShowAsync(this.XamlRoot);
            
            if (cancelled)
            {
                // User cancelled
                _downloadCts?.Cancel();
                return;
            }
            
            presetsHandling = (int)option;
        }

        if (!RpcClientFactory.CheckRpcServerRunning())
        {
            StatusMessage = Lang.HoYoShadeDownloadView_StatusStartingRPC;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppConfig.HoYoShadeHubExecutePath,
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                Arguments = $"rpc {RpcClientFactory.StartupMagic} {Environment.ProcessId}",
            });
            
            // Wait for it to start
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(500);
                if (RpcClientFactory.CheckRpcServerRunning()) break;
            }
        }

        // Target path is UserDataFolder/HoYoShade or UserDataFolder/OpenHoYoShade
        // The content will be extracted directly to this folder, not to a subfolder
        var client = RpcService.CreateRpcClient<HoYoShadeInstaller.HoYoShadeInstallerClient>();
        var request = new InstallHoYoShadeRequest
        {
            DownloadUrl = assetUrl,
            TargetPath = targetPath,
            PresetsHandling = presetsHandling,
            VersionTag = versionTag
        };

        using var call = client.InstallHoYoShade(request, cancellationToken: _downloadCts.Token);
        
        long lastBytes = 0;
        var lastTime = DateTime.Now;

        while (await call.ResponseStream.MoveNext(_downloadCts.Token))
        {
            var progress = call.ResponseStream.Current;
            if (progress.TotalBytes > 0)
            {
                DownloadProgress = (double)progress.DownloadBytes / progress.TotalBytes * 100;
                
                var now = DateTime.Now;
                var elapsed = (now - lastTime).TotalSeconds;
                if (elapsed >= 1)
                {
                    var bytesDiff = progress.DownloadBytes - lastBytes;
                    if (bytesDiff < 0) bytesDiff = 0;
                    var speed = bytesDiff / elapsed;
                    
                    string speedStr = FormatBytes((long)speed);
                    SpeedAndProgress = string.Format(Lang.HoYoShadeDownloadView_ProgressFormat, speedStr, DownloadProgress.ToString("F1"));
                    
                    lastBytes = progress.DownloadBytes;
                    lastTime = now;
                }
            }
            
            if (progress.State == 1) StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusDownloading + " ({0})", keyword);
            else if (progress.State == 2) StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusExtracting + " ({0})", keyword);
            else if (progress.State == 3) 
            {
                // Download of this variant completed successfully - save version information
                await SaveVersionInfoAfterInstallAsync(keyword, SelectedVersion.TagName, "github_release");
                DownloadProgress = 0;
                SpeedAndProgress = "";
            }
            else if (progress.State == 4)
            {
                var errorDetail = string.IsNullOrWhiteSpace(progress.ErrorMessage) ? "Unknown error (installer returned empty message)" : progress.ErrorMessage;
                StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusError + " ({0}): {1}", keyword, errorDetail);
                IsDownloading = false;
                IsControlButtonsVisible = false;
                throw new Exception(errorDetail);
            }
        }
    }

    private CancellationTokenSource _downloadCts;
    private CancellationTokenSource _loadVersionsCts;
    private CancellationTokenSource _validationCts; // For SHA256 validation
    private bool _isPaused;
    private bool _isStopped;
    private HoYoShadeVersionService _versionService;

    [RelayCommand]
    private void PauseResume()
    {
        if (IsDownloading)
        {
            _isPaused = true;
            _downloadCts?.Cancel();
            PauseResumeButtonText = Lang.HoYoShadeDownloadView_Resume;
            OnPropertyChanged(nameof(ShowPauseResumeButton));
        }
        else
        {
            _isPaused = false;
            PauseResumeButtonText = Lang.HoYoShadeDownloadView_Pause;
            OnPropertyChanged(nameof(ShowPauseResumeButton));
            DownloadCommand.ExecuteAsync(null);
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _isPaused = false;
        _isStopped = true;
        
        // Cancel download process
        _downloadCts?.Cancel();
        
        // Cancel validation process (for local package import)
        _validationCts?.Cancel();

        // If not downloading (e.g. paused), manually reset UI state
        if (!IsDownloading)
        {
            StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
            DownloadProgress = 0;
            SpeedAndProgress = "";
            IsControlButtonsVisible = false;
            _isStopped = false;
        }

        try
        {
            // Delete zip files for both variants
            var hoYoShadeZip = System.IO.Path.Combine(AppConfig.UserDataFolder, "HoYoShade_Install.zip");
            if (System.IO.File.Exists(hoYoShadeZip)) System.IO.File.Delete(hoYoShadeZip);
            
            var openHoYoShadeZip = System.IO.Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade_Install.zip");
            if (System.IO.File.Exists(openHoYoShadeZip)) System.IO.File.Delete(openHoYoShadeZip);
        }
        catch {}
    }

    private string FormatBytes(long bytes)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
        {
            dblSByte = bytes / 1024.0;
        }
        return String.Format("{0:0.##} {1}", dblSByte, suffix[i]);
    }

    [RelayCommand]
    private void Start()
    {
        WeakReferenceMessenger.Default.Send(new NavigateToReShadeDownloadPageMessage { IsUpdateMode = IsUpdateMode });
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshVersionsAsync()
    {
        await LoadVersionsAsync();
    }
    
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportFromLocalAsync()
    {
        try
        {
            var path = await FileDialogHelper.PickSingleFileAsync(this.XamlRoot, ("ZIP", ".zip"));
            if (string.IsNullOrWhiteSpace(path)) return;
            
            // Create cancellation token source for validation
            _validationCts?.Cancel();
            _validationCts?.Dispose();
            _validationCts = new CancellationTokenSource();
            
            // Disable buttons during validation
            IsDownloading = true;
            IsControlButtonsVisible = true; // Show stop button during validation
            StatusMessage = Lang.HoYoShadeDownloadView_PackageValidating;
            DownloadProgress = 0;
            
            // Validate the package
            var validationResult = await ValidateLocalPackageAsync(path, _validationCts.Token);
            
            // Check if cancelled
            if (_validationCts.Token.IsCancellationRequested)
            {
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                IsDownloading = false;
                IsControlButtonsVisible = false;
                return;
            }
            
            if (!validationResult.IsValid)
            {
                StatusMessage = string.Format(Lang.HoYoShadeDownloadView_PackageInvalid, validationResult.ErrorMessage ?? "Unknown error");
                IsDownloading = false;
                IsControlButtonsVisible = false;
                await Task.Delay(3000); // Show error for 3 seconds
                StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                return;
            }
            
            // Check if trying to install an already installed package type
            if (validationResult.PackageType == "HoYoShade" && IsHoYoShadeInstalled)
            {
                // Check version: allow upgrade but not downgrade
                if (!CanInstallVersion(validationResult.Version, InstalledHoYoShadeVersion))
                {
                    var errorMsg = Lang.ResourceManager.GetString("HoYoShadeDownloadView_PackageVersionLowerOrEqual") 
                        ?? "Cannot install: Selected version ({0}) is lower than or equal to the installed version ({1}). Please select a higher version.";
                    StatusMessage = string.Format(errorMsg, validationResult.Version, InstalledHoYoShadeVersion);
                    IsDownloading = false;
                    IsControlButtonsVisible = false;
                    await Task.Delay(3000);
                    StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                    Debug.WriteLine($"Installation blocked: HoYoShade version {validationResult.Version} is not higher than installed version {InstalledHoYoShadeVersion}");
                    return;
                }
            }
            
            if (validationResult.PackageType == "OpenHoYoShade" && IsOpenHoYoShadeInstalled)
            {
                // Check version: allow upgrade but not downgrade
                if (!CanInstallVersion(validationResult.Version, InstalledOpenHoYoShadeVersion))
                {
                    var errorMsg = Lang.ResourceManager.GetString("HoYoShadeDownloadView_PackageVersionLowerOrEqual") 
                        ?? "Cannot install: Selected version ({0}) is lower than or equal to the installed version ({1}). Please select a higher version.";
                    StatusMessage = string.Format(errorMsg, validationResult.Version, InstalledOpenHoYoShadeVersion);
                    IsDownloading = false;
                    IsControlButtonsVisible = false;
                    await Task.Delay(3000);
                    StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                    Debug.WriteLine($"Installation blocked: OpenHoYoShade version {validationResult.Version} is not higher than installed version {InstalledOpenHoYoShadeVersion}");
                    return;
                }
            }
            
            // Package is valid, start installation
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_PackageValidated, validationResult.PackageType, validationResult.Version);
            await Task.Delay(1500); // Show success message briefly
            
            await InstallFromLocalPackageAsync(path, validationResult.PackageType!);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
            IsDownloading = false;
            IsControlButtonsVisible = false;
            DownloadProgress = 0;
            Debug.WriteLine("ImportFromLocalAsync cancelled by user");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ImportFromLocalAsync error: {ex}");
            var errorDetail = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusError, errorDetail);
            IsDownloading = false;
            IsControlButtonsVisible = false;
            await Task.Delay(3000);
            StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
        }
        finally
        {
            _validationCts?.Dispose();
            _validationCts = null;
        }
    }
    
    private async Task<PackageValidationResult> ValidateLocalPackageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new PackageValidationResult();
        
        try
        {
            Debug.WriteLine($"Starting validation for file: {filePath}");
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 1: Determine package type and version from filename
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            Debug.WriteLine($"File name (without extension): {fileName}");
            
            string? packageType = null;
            string? version = null;
            
            if (fileName.Contains("OpenHoYoShade", StringComparison.OrdinalIgnoreCase))
            {
                packageType = "OpenHoYoShade";
                // Extract version from filename (e.g., "OpenHoYoShade-V3.0.1.zip", "OpenHoYoShade.V3.0.0-beta.1.zip")
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"[Vv]?(\d+\.\d+\.?\d*(?:-[a-zA-Z0-9.]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    version = match.Groups[1].Value;
                    if (!version.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    {
                        version = "V" + version;
                    }
                }
            }
            else if (fileName.Contains("HoYoShade", StringComparison.OrdinalIgnoreCase))
            {
                packageType = "HoYoShade";
                // Extract version from filename (e.g., "HoYoShade-V3.0.1.zip", "HoYoShade.V3.0.0-beta.1.zip")
                var match = System.Text.RegularExpressions.Regex.Match(fileName, @"[Vv]?(\d+\.\d+\.?\d*(?:-[a-zA-Z0-9.]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    version = match.Groups[1].Value;
                    if (!version.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                    {
                        version = "V" + version;
                    }
                }
            }
            
            Debug.WriteLine($"Detected package type: {packageType}, version: {version}");
            
            if (string.IsNullOrEmpty(packageType) || string.IsNullOrEmpty(version))
            {
                result.ErrorMessage = "Cannot determine package type or version from filename";
                Debug.WriteLine($"Validation failed: {result.ErrorMessage}");
                return result;
            }
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 2: Check if version is V3 or above
            if (!IsVersionV3OrAbove(version))
            {
                result.ErrorMessage = string.Format(Lang.HoYoShadeDownloadView_PackageVersionTooOld, version);
                Debug.WriteLine($"Validation failed: {result.ErrorMessage}");
                return result;
            }
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 3: Find matching release in GitHub releases (case-insensitive, flexible matching)
            Debug.WriteLine($"Searching for version {version} in {Versions.Count} releases");
            
            // Normalize version for comparison (remove 'V' prefix, case-insensitive)
            string normalizedVersion = version.TrimStart('V', 'v');
            var matchingRelease = Versions.FirstOrDefault(r => 
            {
                string releaseTag = r.TagName.TrimStart('V', 'v');
                // Try exact match first
                if (releaseTag.Equals(normalizedVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // Try normalized match (beta.1 vs Beta1, etc.)
                string normalizedReleaseTag = releaseTag.Replace("-", "").Replace(".", "").ToLowerInvariant();
                string normalizedFileVersion = normalizedVersion.Replace("-", "").Replace(".", "").ToLowerInvariant();
                return normalizedReleaseTag == normalizedFileVersion;
            });
            
            if (matchingRelease == null)
            {
                result.ErrorMessage = $"Version {version} not found in GitHub releases";
                Debug.WriteLine($"Validation failed: {result.ErrorMessage}");
                Debug.WriteLine($"Available versions: {string.Join(", ", Versions.Select(v => v.TagName))}");
                return result;
            }
            
            Debug.WriteLine($"Found matching release: {matchingRelease.Name} (Tag: {matchingRelease.TagName})");
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 4: Find matching asset
            var asset = matchingRelease.Assets?.FirstOrDefault(a => 
                a.Name.Contains(packageType, StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            
            if (asset == null)
            {
                result.ErrorMessage = $"No matching asset found for {packageType} {version}";
                Debug.WriteLine($"Validation failed: {result.ErrorMessage}");
                return result;
            }
            
            Debug.WriteLine($"Found matching asset: {asset.Name}");
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 5: Calculate SHA256 of local file
            StatusMessage = "Calculating SHA256...";
            Debug.WriteLine("Calculating SHA256 hash...");
            string localSHA256 = await CalculateSHA256Async(filePath, cancellationToken);
            Debug.WriteLine($"Local SHA256: {localSHA256}");
            
            // Check for cancellation
            cancellationToken.ThrowIfCancellationRequested();
            
            // Step 6: Try to get SHA256 from GitHub
            var sha256Asset = matchingRelease.Assets?.FirstOrDefault(a => 
                a.Name.Contains(packageType, StringComparison.OrdinalIgnoreCase) && 
                a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
            
            if (sha256Asset != null)
            {
                Debug.WriteLine($"Found SHA256 asset: {sha256Asset.Name}");
                
                // Download SHA256 file and compare
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");
                
                string sha256Url = sha256Asset.BrowserDownloadUrl;
                
                // Apply proxy based on selected server
                int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
                string? proxyUrl = CloudProxyManager.GetProxyUrl(serverIndex);
                if (!string.IsNullOrWhiteSpace(proxyUrl))
                {
                    sha256Url = CloudProxyManager.ApplyProxy(sha256Url, proxyUrl);
                }
                
                Debug.WriteLine($"Downloading SHA256 from: {sha256Url}");
                
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
                
                var expectedSHA256 = await httpClient.GetStringAsync(sha256Url, cancellationToken);
                expectedSHA256 = expectedSHA256.Trim().Split(' ')[0]; // Get first part (hash only)
                Debug.WriteLine($"Expected SHA256: {expectedSHA256}");
                
                if (!localSHA256.Equals(expectedSHA256, StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = Lang.HoYoShadeDownloadView_PackageSHA256Mismatch;
                    Debug.WriteLine($"Validation failed: SHA256 mismatch");
                    return result;
                }
                
                Debug.WriteLine("SHA256 verification passed");
            }
            else
            {
                // SHA256 file not found - this is a security risk, fail validation
                result.ErrorMessage = $"SHA256 file not found for {packageType} {version}. Cannot verify package integrity.";
                Debug.WriteLine($"Validation failed: {result.ErrorMessage}");
                return result;
            }
            
            // All checks passed
            result.IsValid = true;
            result.PackageType = packageType;
            result.Version = version;
            result.FilePath = filePath;
            
            Debug.WriteLine("Validation successful!");
            return result;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("Validation cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Validation exception: {ex}");
            result.ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.ToString() : ex.Message;
            return result;
        }
    }
    
    private async Task<string> CalculateSHA256Async(string filePath, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        
        // Read file in chunks to support cancellation
        byte[] buffer = new byte[8192];
        int bytesRead;
        
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            cancellationToken.ThrowIfCancellationRequested();
        }
        
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hash = sha256.Hash;
        
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
    
    private async Task InstallFromLocalPackageAsync(string packagePath, string packageType)
    {
        try
        {
            Debug.WriteLine($"Starting local package installation: {packagePath}");
            
            StatusMessage = Lang.HoYoShadeDownloadView_Installing;
            _isPaused = false;
            PauseResumeButtonText = Lang.HoYoShadeDownloadView_Pause;
            OnPropertyChanged(nameof(ShowPauseResumeButton));
            IsControlButtonsVisible = true;
            DownloadProgress = 0;
            
            if (!RpcClientFactory.CheckRpcServerRunning())
            {
                Debug.WriteLine("RPC server not running, starting it...");
                StatusMessage = Lang.HoYoShadeDownloadView_StatusStartingRPC;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = AppConfig.HoYoShadeHubExecutePath,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    Arguments = $"rpc {RpcClientFactory.StartupMagic} {Environment.ProcessId}",
                });
                
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(500);
                    if (RpcClientFactory.CheckRpcServerRunning())
                    {
                        Debug.WriteLine("RPC server started successfully");
                        break;
                    }
                }
                
                if (!RpcClientFactory.CheckRpcServerRunning())
                {
                    throw new Exception("Failed to start RPC server");
                }
            }
            
            var targetPath = System.IO.Path.Combine(AppConfig.UserDataFolder, packageType);
            Debug.WriteLine($"Target installation path: {targetPath}");
            
            // Check if this is an update (existing installation with Presets)
            var presetsPath = System.IO.Path.Combine(targetPath, "Presets");
            bool isUpdate = Directory.Exists(presetsPath);
            
            // Determine presets handling option
            int presetsHandling = 0; // 0: Overwrite (default)
            string versionTag = "";
            
            // Extract version from filename for version tag
            var fileName = Path.GetFileNameWithoutExtension(packagePath);
            var versionMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"[Vv]?(\d+\.\d+\.?\d*(?:-[a-zA-Z0-9.]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (versionMatch.Success)
            {
                versionTag = versionMatch.Groups[1].Value;
                if (!versionTag.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                {
                    versionTag = "V" + versionTag;
                }
            }
            else
            {
                // Fallback version tag if filename doesn't contain valid version
                versionTag = "V" + DateTime.Now.ToString("yyyy.MM.dd.HHmmss");
                Debug.WriteLine($"Version parsing failed, using timestamp as tag: {versionTag}");
            }
            
            // Ask for presets handling if presets folder exists (ignoring IsUpdateMode flag)
            if (isUpdate)
            {
                // Show dialog to ask user how to handle presets
                var (cancelled, option) = await PresetsHandlingDialog.ShowAsync(this.XamlRoot);
                
                if (cancelled)
                {
                    // User cancelled
                    StatusMessage = Lang.HoYoShadeDownloadView_StatusReady;
                    IsDownloading = false;
                    IsControlButtonsVisible = false;
                    return;
                }
                
                presetsHandling = (int)option;
                Debug.WriteLine($"User selected presets handling option: {option} (value: {presetsHandling})");
            }
            
            var client = RpcService.CreateRpcClient<HoYoShadeInstaller.HoYoShadeInstallerClient>();
            
            // Use local file path instead of download URL
            var request = new InstallHoYoShadeRequest
            {
                DownloadUrl = "file://" + packagePath, // Use file:// protocol to indicate local file
                TargetPath = targetPath,
                PresetsHandling = presetsHandling,
                VersionTag = versionTag
            };
            
            Debug.WriteLine($"Sending install request with URL: {request.DownloadUrl}, PresetsHandling: {presetsHandling}, VersionTag: {versionTag}");
            
            using var call = client.InstallHoYoShade(request, cancellationToken: CancellationToken.None);
            
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                var progress = call.ResponseStream.Current;
                Debug.WriteLine($"Installation progress - State: {progress.State}, Error: {progress.ErrorMessage}");
                
                if (progress.State == 2) 
                {
                    StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusExtracting + " ({0})", packageType);
                    DownloadProgress = 50;
                }
                else if (progress.State == 3) 
                {
                    DownloadProgress = 100;
                    Debug.WriteLine("Installation completed successfully");
                    
                    // Save version info after successful installation from local package
                    try
                    {
                        if (!string.IsNullOrEmpty(versionTag))
                        {
                            // Calculate SHA256 for local package
                            string packageSha256 = await CalculateSHA256Async(packagePath);
                            await SaveVersionInfoAfterInstallAsync(packageType, versionTag, "local_import", packageSha256);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to save version info after local install: {ex.Message}");
                    }
                    
                    break;
                }
                else if (progress.State == 4)
                {
                    var errorDetail = string.IsNullOrWhiteSpace(progress.ErrorMessage) ? "Unknown error (installer returned empty message)" : progress.ErrorMessage;
                    var errorMsg = string.Format(Lang.HoYoShadeDownloadView_StatusError + " ({0}): {1}", packageType, errorDetail);
                    Debug.WriteLine($"Installation error: {errorMsg}");
                    StatusMessage = errorMsg;
                    IsDownloading = false;
                    IsControlButtonsVisible = false;
                    throw new Exception(errorDetail);
                }
            }
            
            StatusMessage = Lang.HoYoShadeDownloadView_StatusFinished;
            CheckInstallationStatus(); // Update installation status
            
            // Notify other pages that HoYoShade installation has changed
            WeakReferenceMessenger.Default.Send(new HoYoShadeInstallationChangedMessage());
            
            IsControlButtonsVisible = false;
            IsDownloading = false;
            DownloadProgress = 0;
            
            Debug.WriteLine("Local package installation finished successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"InstallFromLocalPackageAsync error: {ex}");
            var errorDetail = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
            StatusMessage = string.Format(Lang.HoYoShadeDownloadView_StatusError, errorDetail);
            IsDownloading = false;
            IsControlButtonsVisible = false;
        }
    }
    
    private class PackageValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? PackageType { get; set; }
        public string? Version { get; set; }
        public string? FilePath { get; set; }
    }
    
    private void CheckInstallationStatus()
    {
        try
        {
            var hoYoShadeFolder = System.IO.Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            var openHoYoShadeFolder = System.IO.Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
            
            var wasHoYoShadeInstalled = IsHoYoShadeInstalled;
            var wasOpenHoYoShadeInstalled = IsOpenHoYoShadeInstalled;
            
            // Installation detection: folder exists and has any content
            bool HasContent(string path)
            {
                try
                {
                    return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
                }
                catch
                {
                    return false;
                }
            }
            
            IsHoYoShadeInstalled = HasContent(hoYoShadeFolder);
            IsOpenHoYoShadeInstalled = HasContent(openHoYoShadeFolder);
            
            // Set checkbox states: installed=checked, not installed=unchecked (default to HoYoShade when none installed)
            IsHoYoShadeSelected = IsHoYoShadeInstalled || (!IsHoYoShadeInstalled && !IsOpenHoYoShadeInstalled);
            IsOpenHoYoShadeSelected = IsOpenHoYoShadeInstalled;
            
            // Enable "Next" button if at least one is installed
            CanStart = IsHoYoShadeInstalled || IsOpenHoYoShadeInstalled;
            
            // Trigger property changes for checkbox states when installation status changes
            if (wasHoYoShadeInstalled != IsHoYoShadeInstalled)
            {
                OnPropertyChanged(nameof(IsHoYoShadeSelected));
            }
            if (wasOpenHoYoShadeInstalled != IsOpenHoYoShadeInstalled)
            {
                OnPropertyChanged(nameof(IsOpenHoYoShadeSelected));
            }
            OnPropertyChanged(nameof(CanDownload));
            OnPropertyChanged(nameof(CanImport));
            OnPropertyChanged(nameof(Title)); // Start title might need update if logic depended on install state (it doesn't currently but good practice)
            ImportFromLocalCommand.NotifyCanExecuteChanged();

            Debug.WriteLine($"Installation status check: HoYoShade={IsHoYoShadeInstalled}, OpenHoYoShade={IsOpenHoYoShadeInstalled}, CanStart={CanStart}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CheckInstallationStatus error: {ex}");
        }
    }
    
    /// <summary>
    /// 加载已安装的HoYoShade和OpenHoYoShade版本
    /// </summary>
    private async Task LoadInstalledVersionsAsync()
    {
        try
        {
            var manifest = await _versionService.LoadManifestAsync();
            InstalledHoYoShadeVersion = manifest.HoYoShade?.Version;
            InstalledOpenHoYoShadeVersion = manifest.OpenHoYoShade?.Version;
            
            Debug.WriteLine($"Loaded installed versions: HoYoShade={InstalledHoYoShadeVersion}, OpenHoYoShade={InstalledOpenHoYoShadeVersion}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load installed versions: {ex.Message}");
            InstalledHoYoShadeVersion = null;
            InstalledOpenHoYoShadeVersion = null;
        }
    }
    
    /// <summary>
    /// 比较两个版本号 (使用 NuGetVersion 提供工业级语义化版本比较)
    /// 支持所有标准语义化版本格式，包括:
    /// - 标准版本: 3.0.1, 3.1.0
    /// - 预发布版本: 3.0.0-Beta.1, 3.0.0-Alpha.2, 3.0.0-RC.1
    /// - 带构建元数据: 3.0.0+build.123
    /// - 组合格式: 3.0.0-Beta.1+build.456
    /// </summary>
    /// <param name="version1">版本1 (例如: "V3.0.1", "V3.0.0-Beta.1")</param>
    /// <param name="version2">版本2 (例如: "V3.1.0", "V3.0.0-Beta.3")</param>
    /// <returns>如果version1 > version2返回1,如果version1 < version2返回-1,如果相等返回0,无法比较返回null</returns>
    private int? CompareVersions(string? version1, string? version2)
    {
        if (string.IsNullOrWhiteSpace(version1) || string.IsNullOrWhiteSpace(version2))
        {
            return null;
        }
        
        try
        {
            // 移除 'v' 或 'V' 前缀
            string v1 = version1.TrimStart('v', 'V').Trim();
            string v2 = version2.TrimStart('v', 'V').Trim();
            
            // 使用 NuGetVersion 解析版本号
            // NuGetVersion 完全支持语义化版本规范 (SemVer 2.0):
            // - 正确处理主版本、次版本、修订版本的数字比较
            // - 预发布标识的字典序和数字比较 (Beta.1 < Beta.2 < Beta.10)
            // - 正式版 > 预览版 (3.0.0 > 3.0.0-Beta.1)
            // - 预发布标识的优先级 (Alpha < Beta < RC < 正式版)
            if (NuGetVersion.TryParse(v1, out var nugetV1) && NuGetVersion.TryParse(v2, out var nugetV2))
            {
                int result = nugetV1.CompareTo(nugetV2);
                Debug.WriteLine($"CompareVersions: NuGetVersion comparing '{version1}' with '{version2}', result: {result}");
                return result > 0 ? 1 : result < 0 ? -1 : 0;
            }
            
            Debug.WriteLine($"CompareVersions: NuGetVersion parse failed for '{version1}' or '{version2}', falling back to manual parse");
            
            // 如果 NuGetVersion 无法解析,回退到手动解析 (保留作为安全网)
            var parts1 = v1.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            var parts2 = v2.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            
            int maxLength = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;
                
                if (p1 > p2) return 1;
                if (p1 < p2) return -1;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to compare versions '{version1}' and '{version2}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Checks if the selected version can be installed
    /// </summary>
    /// <param name="selectedVersion">The version to install</param>
    /// <param name="installedVersion">The currently installed version</param>
    /// <returns>True if can install</returns>
    private bool CanInstallVersion(string? selectedVersion, string? installedVersion)
    {
        // If not installed, can always install
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return true;
        }
        
        // Compare versions
        var comparison = CompareVersions(selectedVersion, installedVersion);
        
        if (IsUpdateMode)
        {
            // In update mode, allow same or higher version (>= 0)
            return comparison.HasValue && comparison.Value >= 0;
        }
        
        // In clean install mode, strictly existing logic (higher version only)
        // Wait, existing logic was strict higher (> 0).
        return comparison.HasValue && comparison.Value > 0;
    }
    
    /// <summary>
    /// Save version info after installation
    /// </summary>
    private async Task SaveVersionInfoAfterInstallAsync(string packageType, string version, string source, string? sha256 = null)
    {
        try
        {
            if (packageType == "HoYoShade")
            {
                await _versionService.UpdateHoYoShadeVersionAsync(version, source, sha256);
                Debug.WriteLine($"Saved version info for HoYoShade: {version}");
            }
            else if (packageType == "OpenHoYoShade")
            {
                await _versionService.UpdateOpenHoYoShadeVersionAsync(version, source, sha256);
                Debug.WriteLine($"Saved version info for OpenHoYoShade: {version}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save version info for {packageType}: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Handle installation changed message from other views
    /// </summary>
    private async void OnInstallationChanged()
    {
        try
        {
            await LoadInstalledVersionsAsync();
            CheckInstallationStatus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnInstallationChanged error: {ex}");
        }
    }
}
