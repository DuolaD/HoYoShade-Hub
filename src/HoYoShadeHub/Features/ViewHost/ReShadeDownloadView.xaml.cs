using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Features.Setting;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using HoYoShadeHub.RPC.HoYoShadeInstall;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace HoYoShadeHub.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class ReShadeDownloadView : UserControl
{
    private readonly ILogger<ReShadeDownloadView> _logger = AppConfig.GetLogger<ReShadeDownloadView>();
    private readonly RpcService _rpcService = AppConfig.GetService<RpcService>();
    private readonly HoYoShadeVersionService _versionService;
    private CancellationTokenSource _cancellationTokenSource;

    public ReShadeDownloadView()
    {
        this.InitializeComponent();
        DownloadServers = new ObservableCollection<string>();
        _versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
        
        // Register for installation change messages from other views/windows
        WeakReferenceMessenger.Default.Register<HoYoShadeInstallationChangedMessage>(this, (r, m) => OnInstallationChanged());
        
        UpdateDownloadServers();
        UpdateContentMargin();
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
    }

    private void OnLanguageChanged()
    {
        // Ensure Lang uses the current culture (fix for async methods capturing old culture)
        Lang.Culture = CultureInfo.CurrentUICulture;

        // Update download servers list
        UpdateDownloadServers();

        // Update margin based on language
        UpdateContentMargin();

        // Refresh status message based on current state
        if (!IsDownloading)
        {
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        }
    }

    [ObservableProperty]
    private Thickness contentMargin = new Thickness(48, 110, 48, 120);

    private void UpdateContentMargin()
    {
        // For English language, use smaller top margin to prevent scrolling
        var currentCulture = CultureInfo.CurrentUICulture.Name.ToLower();
        double topMargin;

        if (currentCulture.StartsWith("en"))
        {
            // English: much less vertical offset (smaller top margin)
            // If in update mode, reduce further to accommodate the hint text
            topMargin = IsUpdateMode ? 30 : 60;
        }
        else
        {
            // Chinese and other languages: slightly less vertical offset
            topMargin = IsUpdateMode ? 60 : 80;
        }

        ContentMargin = new Thickness(48, topMargin, 48, 120);
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

    public ObservableCollection<string> DownloadServers { get; }

    [ObservableProperty]
    private string selectedDownloadServer;

    [ObservableProperty]
    private bool isHoYoShadeInstalled;

    [ObservableProperty]
    private bool isOpenHoYoShadeInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoYoShadeVersionDisplay))]
    private string? installedHoYoShadeVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenHoYoShadeVersionDisplay))]
    private string? installedOpenHoYoShadeVersion;

    public string HoYoShadeVersionDisplay => string.IsNullOrEmpty(InstalledHoYoShadeVersion) ? "" : " " + InstalledHoYoShadeVersion;
    public string OpenHoYoShadeVersionDisplay => string.IsNullOrEmpty(InstalledOpenHoYoShadeVersion) ? "" : " " + InstalledOpenHoYoShadeVersion;

    public bool AreBothInstalled => IsHoYoShadeInstalled && IsOpenHoYoShadeInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanInstallToHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToOpenHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToBoth))]
    private bool isUpdateMode;

    public string Title => IsUpdateMode ? "接下来，让我们更新 ReShade 着色器和插件" : Lang.ReShadeDownloadView_Title;
    
    public string UpdateHintText => "如果本次更新涉及到ReShade版本升级，我们建议你更新所有已安装的着色器和插件。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanInstallToHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToOpenHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToBoth))]
    private bool isInstallToHoYoShadeOnly = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanInstallToHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToOpenHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToBoth))]
    private bool isInstallToOpenHoYoShadeOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanInstallToHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToOpenHoYoShadeOnly))]
    [NotifyPropertyChangedFor(nameof(CanInstallToBoth))]
    private bool isInstallToBoth;

    // Properties to control radio button enabled state
    // Logic:
    // - If only one framework is installed  only that framework's option is enabled
    // - If both frameworks are installed  all options enabled initially, then disable after installation
    // - In Update Mode, options remain enabled even after installation (to allow re-install/update)
    public bool CanInstallToHoYoShadeOnly =>
        IsHoYoShadeInstalled && (!_hasInstalledHoYoShadeTarget || IsUpdateMode);

    public bool CanInstallToOpenHoYoShadeOnly =>
        IsOpenHoYoShadeInstalled && (!_hasInstalledOpenHoYoShadeTarget || IsUpdateMode);

    public bool CanInstallToBoth =>
        IsHoYoShadeInstalled && IsOpenHoYoShadeInstalled &&
        (!_hasInstalledHoYoShadeTarget || IsUpdateMode) && (!_hasInstalledOpenHoYoShadeTarget || IsUpdateMode);

    [ObservableProperty]
    private bool isInstallAll = true;

    [ObservableProperty]
    private bool isInstallEssentialOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCustomizeButton))]
    private bool isCustomizeInstall;

    partial void OnIsCustomizeInstallChanged(bool value)
    {
        if (value)
        {
            _ = CustomizeAsync();
        }
    }

    partial void OnIsUpdateModeChanged(bool value)
    {
        UpdateContentMargin();
    }

    // Track installation history to enable Next button and auto-switch logic
    private bool _hasInstalledAtLeastOnce = false;
    private bool _hasInstalledHoYoShadeTarget = false;
    private bool _hasInstalledOpenHoYoShadeTarget = false;
    private bool _initialHoYoShadeState = false;
    private bool _initialOpenHoYoShadeState = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    [NotifyPropertyChangedFor(nameof(CanSkip))]
    [NotifyPropertyChangedFor(nameof(ShowRefreshButton))]
    [NotifyPropertyChangedFor(nameof(ShowPauseButton))]
    [NotifyPropertyChangedFor(nameof(ShowStopButton))]
    [NotifyPropertyChangedFor(nameof(CanNext))]
    private bool isDownloading;

    public bool CanDownload => !IsDownloading &&
        (IsInstallToHoYoShadeOnly || IsInstallToOpenHoYoShadeOnly || IsInstallToBoth);

    public bool CanRefresh => !IsDownloading;

    public bool CanSkip => !IsDownloading;

    public bool CanNext => !IsDownloading && _hasInstalledAtLeastOnce;

    public bool ShowRefreshButton => !IsDownloading;

    public bool ShowCustomizeButton => IsCustomizeInstall && !IsDownloading;

    public bool ShowPauseButton => IsDownloading;

    public bool ShowStopButton => IsDownloading;

    public bool CanImport => !IsDownloading;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private string speedAndProgress;

    private bool _languageInitialized;

    private async void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        InitializeLanguageSelector();
        CheckInstallationStatus();
        await LoadInstalledVersionsAsync();

        // Store initial installation states
        _initialHoYoShadeState = IsHoYoShadeInstalled;
        _initialOpenHoYoShadeState = IsOpenHoYoShadeInstalled;

        // DO NOT initialize _hasInstalledHoYoShadeTarget or _hasInstalledOpenHoYoShadeTarget here
        // These flags track shader/addon installation in the CURRENT SESSION, not framework installation
        // They should start as false and only be set to true when user actually installs shaders/addons

        // Refresh the CanInstall properties
        OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
        OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
        OnPropertyChanged(nameof(CanInstallToBoth));

        StatusMessage = Lang.ReShadeDownloadView_StatusReady;
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
                }
            }
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.CurrentUICulture = CultureInfo.InstalledUICulture;
        }
        catch (Exception ex)
        {// ...existing code...
        }
    }

    private void CheckInstallationStatus()
    {
        try
        {
            var hoYoShadeFolder = System.IO.Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            var openHoYoShadeFolder = System.IO.Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");

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

            // Logic: Select installation target based on which frameworks are installed
            // - If only HoYoShade is installed → select HoYoShade (install shaders to the installed framework)
            // - If only OpenHoYoShade is installed → select OpenHoYoShade (install shaders to the installed framework)
            // - If both are installed → default to HoYoShade, all options available
            // - If neither is installed → default to HoYoShade (should not happen in this page)
            if (IsHoYoShadeInstalled && !IsOpenHoYoShadeInstalled)
            {
                // Only HoYoShade installed → select HoYoShade
                IsInstallToHoYoShadeOnly = true;
                IsInstallToOpenHoYoShadeOnly = false;
                IsInstallToBoth = false;
            }
            else if (!IsHoYoShadeInstalled && IsOpenHoYoShadeInstalled)
            {
                // Only OpenHoYoShade installed → select OpenHoYoShade
                IsInstallToHoYoShadeOnly = false;
                IsInstallToOpenHoYoShadeOnly = true;
                IsInstallToBoth = false;
            }
            else if (IsHoYoShadeInstalled && IsOpenHoYoShadeInstalled)
            {
                // Both installed → default to HoYoShade, but all options available
                IsInstallToHoYoShadeOnly = true;
                IsInstallToOpenHoYoShadeOnly = false;
                IsInstallToBoth = false;
            }
            else
            {
                // Neither installed → default to HoYoShade (fallback, shouldn't happen)
                IsInstallToHoYoShadeOnly = true;
                IsInstallToOpenHoYoShadeOnly = false;
                IsInstallToBoth = false;
            }

            OnPropertyChanged(nameof(AreBothInstalled));
            OnPropertyChanged(nameof(CanDownload));

            Debug.WriteLine($"Installation status check: HoYoShade={IsHoYoShadeInstalled}, OpenHoYoShade={IsOpenHoYoShadeInstalled}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CheckInstallationStatus error: {ex}");
        }
    }

    private List<string> _customSelectedPackages = new();
    private List<EffectPackage> _cachedEffectPackages;
    private List<Addon> _cachedAddons;

    [RelayCommand]
    private async Task CustomizeAsync()
    {
        try
        {
            if (_cachedEffectPackages == null || _cachedAddons == null)
            {
                await FetchPackagesAsync();
            }

            if (_cachedEffectPackages == null || _cachedAddons == null)
            {
                // Failed to fetch
                return;
            }

            var dialog = new ReShadeCustomSelectionDialog(_cachedEffectPackages, _cachedAddons);
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();

            if (dialog.DialogResult == ContentDialogResult.Primary)
            {
                _customSelectedPackages = dialog.GetSelectedPackages();
            }
            else
            {
                // If canceled and no packages selected previously, maybe switch back to default?
                // Or just keep as is.
                if (IsCustomizeInstall && _customSelectedPackages.Count == 0)
                {
                    // Optional: switch back to InstallAll if user cancels without selecting anything
                    // IsInstallAll = true; 
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CustomizeAsync");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private async Task FetchPackagesAsync()
    {
        try
        {
            StatusMessage = "Fetching package lists...";
            using var client = new HttpClient();
            
            // Get proxy URL based on selected server
            int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
            string? proxyUrl = CloudProxyManager.GetProxyUrl(serverIndex);

            // Fetch Effects
            string effectsUrl = ReShadeDownloadServer.EffectPackagesUrl;
            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                effectsUrl = CloudProxyManager.ApplyProxy(effectsUrl, proxyUrl);
            }
            
            using var effectsStream = await client.GetStreamAsync(effectsUrl);
            var effectsIni = new IniFile(effectsStream);
            _cachedEffectPackages = new List<EffectPackage>();

            foreach (string packageSection in effectsIni.GetSections())
            {
                bool required = effectsIni.GetString(packageSection, "Required") == "1";
                bool? enabled;
                if (required)
                {
                    enabled = true;
                }
                else
                {
                    string enabledStr = effectsIni.GetString(packageSection, "Enabled", "0");
                    enabled = enabledStr == "1";
                }

                effectsIni.GetValue(packageSection, "EffectFiles", out string[] effectFiles);
                effectsIni.GetValue(packageSection, "DenyEffectFiles", out string[] denyEffectFiles);

                var item = new EffectPackage
                {
                    Selected = enabled,
                    Modifiable = !required,
                    Name = effectsIni.GetString(packageSection, "PackageName"),
                    Description = effectsIni.GetString(packageSection, "PackageDescription"),
                    InstallPath = effectsIni.GetString(packageSection, "InstallPath", string.Empty),
                    TextureInstallPath = effectsIni.GetString(packageSection, "TextureInstallPath", string.Empty),
                    DownloadUrl = effectsIni.GetString(packageSection, "DownloadUrl"),
                    RepositoryUrl = effectsIni.GetString(packageSection, "RepositoryUrl"),
                    EffectFiles = effectFiles?.Where(x => denyEffectFiles == null || !denyEffectFiles.Contains(x))
                        .Select(x => new EffectFile { FileName = x, Selected = false }).ToArray(),
                    DenyEffectFiles = denyEffectFiles
                };

                _cachedEffectPackages.Add(item);
            }

            // Fetch Addons
            string addonsUrl = ReShadeDownloadServer.AddonsUrl;
            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                addonsUrl = CloudProxyManager.ApplyProxy(addonsUrl, proxyUrl);
            }
            
            using var addonsStream = await client.GetStreamAsync(addonsUrl);
            var addonsIni = new IniFile(addonsStream);
            _cachedAddons = new List<Addon>();

            foreach (string addon in addonsIni.GetSections())
            {
                string downloadUrl = addonsIni.GetString(addon, "DownloadUrl64");
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = addonsIni.GetString(addon, "DownloadUrl");
                }

                var item = new Addon
                {
                    Name = addonsIni.GetString(addon, "PackageName"),
                    Description = addonsIni.GetString(addon, "PackageDescription"),
                    EffectInstallPath = addonsIni.GetString(addon, "EffectInstallPath", string.Empty),
                    DownloadUrl = downloadUrl,
                    RepositoryUrl = addonsIni.GetString(addon, "RepositoryUrl"),
                    Selected = false // Default to not selected for addons
                };

                _cachedAddons.Add(item);
            }

            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error fetching packages: {ex.Message}";
            _logger.LogError(ex, "Failed to fetch packages");
            _cachedEffectPackages = null;
            _cachedAddons = null;
        }
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            StatusMessage = Lang.ReShadeDownloadView_StatusDownloading;
            SpeedAndProgress = "";

            // Track which target we're installing to
            bool installingToHoYoShade = IsInstallToHoYoShadeOnly || IsInstallToBoth;
            bool installingToOpenHoYoShade = IsInstallToOpenHoYoShadeOnly || IsInstallToBoth;

            _cancellationTokenSource = new CancellationTokenSource();

            // Ensure RPC server is running
            if (!await _rpcService.EnsureRpcServerRunningAsync())
            {
                StatusMessage = Lang.ReShadeDownloadView_StatusError.Replace("{0}", "RPC server not available");
                IsDownloading = false;
                return;
            }

            // Determine installation target
            int installTarget = IsInstallToHoYoShadeOnly ? 0 : (IsInstallToOpenHoYoShadeOnly ? 1 : 2);

            // Determine installation mode
            int installMode = IsInstallAll ? 0 : (IsInstallEssentialOnly ? 1 : 2);

            // Check custom packages
            if (installMode == 2 && (_customSelectedPackages == null || _customSelectedPackages.Count == 0))
            {
                // If custom mode but no packages selected, try to open dialog
                await CustomizeAsync();
                if (_customSelectedPackages == null || _customSelectedPackages.Count == 0)
                {
                    IsDownloading = false;
                    StatusMessage = "No packages selected.";
                    return;
                }
            }

            // Determine download server - pass the server index to RPC
            // RPC will use CloudProxyManager to handle proxy selection
            int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
            string? proxyUrl = CloudProxyManager.GetProxyUrl(serverIndex);
            bool useProxy = !string.IsNullOrWhiteSpace(proxyUrl);
            
            // For backward compatibility with RPC, pass a proxy indicator string
            // The RPC service will need to be updated to use the same CloudProxyManager logic
            string downloadServer = SelectedDownloadServer;

            var basePath = AppConfig.UserDataFolder;

            // Create RPC client
            var client = RpcService.CreateRpcClient<HoYoShadeInstaller.HoYoShadeInstallerClient>();

            var request = new InstallReShadePackRequest
            {
                BasePath = basePath,
                InstallTarget = installTarget,
                InstallMode = installMode,
                DownloadServer = downloadServer
            };

            if (installMode == 2)
            {
                request.CustomPackages.AddRange(_customSelectedPackages);
            }

            // Call RPC and stream progress
            using var call = client.InstallReShadePack(request, cancellationToken: _cancellationTokenSource.Token);

            while (await call.ResponseStream.MoveNext(_cancellationTokenSource.Token))
            {
                var progress = call.ResponseStream.Current;

                // Update UI with progress
                if (progress.TotalFiles > 0)
                {
                    DownloadProgress = (double)progress.DownloadedFiles / progress.TotalFiles * 100;
                }

                if (progress.State == 1) // Downloading
                {
                    // Format type label with proper localization
                    string typeLabel = progress.CurrentFileType == 0
                        ? Lang.ReShadeDownloadView_TypeShaders
                        : Lang.ReShadeDownloadView_TypeAddons;

                    StatusMessage = $"{Lang.ReShadeDownloadView_StatusDownloading}: [{typeLabel}] {progress.CurrentFile ?? ""}";

                    // Format progress: [speed] - [percentage] - [count]
                    // Important: Use the LATEST values from progress for accurate percentage
                    double currentProgress = progress.TotalFiles > 0 ? ((double)progress.DownloadedFiles / progress.TotalFiles * 100) : 0;
                    string speedText = FormatSpeed(progress.DownloadSpeedBytesPerSec);
                    string percentText = currentProgress.ToString("F1") + "%";
                    string countText = $"{progress.DownloadedFiles}/{progress.TotalFiles}";
                    SpeedAndProgress = $"{speedText} - {percentText} - {countText}";
                }
                else if (progress.State == 3) // Finished
                {
                    StatusMessage = Lang.ReShadeDownloadView_StatusFinished;
                    DownloadProgress = 100;

                    // Update final progress display - hide speed info when complete for cleaner look
                    if (progress.TotalFiles > 0)
                    {
                        SpeedAndProgress = $"100.0% - {progress.TotalFiles}/{progress.TotalFiles}";
                    }

                    // Mark installation as completed
                    _hasInstalledAtLeastOnce = true;

                    // Handle auto-switch logic for single target installations
                    if (IsInstallToHoYoShadeOnly && (!_hasInstalledHoYoShadeTarget || IsUpdateMode))
                    {
                        // Just installed HoYoShade only
                        _hasInstalledHoYoShadeTarget = true;

                        // Refresh UI states
                        OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                        OnPropertyChanged(nameof(CanInstallToBoth));

                        if (!IsUpdateMode)
                        {
                            // Disable HoYoShade options and check if OpenHoYoShade is available
                            
                            // If OpenHoYoShade option is available, auto-select it; otherwise don't select anything
                            IsInstallToHoYoShadeOnly = false;
                            IsInstallToBoth = false;
                            if (CanInstallToOpenHoYoShadeOnly)
                            {
                                IsInstallToOpenHoYoShadeOnly = true;
                            }
                            else
                            {
                                IsInstallToOpenHoYoShadeOnly = false;
                            }
                        }
                    }
                    else if (IsInstallToOpenHoYoShadeOnly && (!_hasInstalledOpenHoYoShadeTarget || IsUpdateMode))
                    {
                        // Just installed OpenHoYoShade only
                        _hasInstalledOpenHoYoShadeTarget = true;
                        
                        // Refresh UI states
                        OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                        OnPropertyChanged(nameof(CanInstallToBoth));

                        if (!IsUpdateMode)
                        {
                            // Disable OpenHoYoShade options and check if HoYoShade is available

                            // If HoYoShade option is available, auto-select it; otherwise don't select anything
                            IsInstallToOpenHoYoShadeOnly = false;
                            IsInstallToBoth = false;
                            if (CanInstallToHoYoShadeOnly)
                            {
                                IsInstallToHoYoShadeOnly = true;
                            }
                            else
                            {
                                IsInstallToHoYoShadeOnly = false;
                            }
                        }
                    }
                    else if (IsInstallToBoth)
                    {
                        // Installed to both targets
                        _hasInstalledHoYoShadeTarget = true;
                        _hasInstalledOpenHoYoShadeTarget = true;

                        // Refresh UI states
                        OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                        OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                        OnPropertyChanged(nameof(CanInstallToBoth));

                        if (!IsUpdateMode)
                        {
                            // Disable all options and don't select anything
                            IsInstallToHoYoShadeOnly = false;
                            IsInstallToOpenHoYoShadeOnly = false;
                            IsInstallToBoth = false;
                        }
                    }

                    OnPropertyChanged(nameof(CanNext));
                    OnPropertyChanged(nameof(CanDownload));
                    break;
                }
                else if (progress.State == 4) // Error
                {
                    StatusMessage = string.Format(Lang.ReShadeDownloadView_StatusError, progress.ErrorMessage ?? "Unknown error");
                    break;
                }
            }

            IsDownloading = false;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
            IsDownloading = false;
            _logger.LogInformation("ReShade pack download canceled by user");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format(Lang.ReShadeDownloadView_StatusError, ex.Message);
            IsDownloading = false;
            _logger.LogError(ex, "ReShade pack download failed");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        const double KB = 1 << 10;
        const double MB = 1 << 20;

        if (bytesPerSecond >= MB)
        {
            return $"{bytesPerSecond / MB:F2} MB/s";
        }
        else if (bytesPerSecond >= KB)
        {
            return $"{bytesPerSecond / KB:F2} KB/s";
        }
        else
        {
            return $"{bytesPerSecond} B/s";
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        CheckInstallationStatus();
        StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        DownloadProgress = 0;
        SpeedAndProgress = "";
    }

    [RelayCommand]
    private void Pause()
    {
        // Pause is not implemented for file-by-file downloads, use Stop instead
        Stop();
    }

    [RelayCommand]
    private void Stop()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            IsDownloading = false;
            DownloadProgress = 0;
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
            SpeedAndProgress = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop download");
        }
    }

    [RelayCommand]
    private void Skip()
    {
        // Skip this step and go to next
        WeakReferenceMessenger.Default.Send(new WelcomePageFinishedMessage());
    }

    [RelayCommand]
    private void Next()
    {
        // Complete this step and go to next
        WeakReferenceMessenger.Default.Send(new WelcomePageFinishedMessage());
    }

    [RelayCommand]
    private async Task ImportFromLocalAsync()
    {
        try
        {
            var path = await FileDialogHelper.PickSingleFileAsync(this.XamlRoot, ("ZIP", ".zip"));
            if (string.IsNullOrWhiteSpace(path)) return;

            StatusMessage = Lang.ReShadeDownloadView_FolderImporting;
            IsDownloading = true;

            // Create temporary directory for extraction
            string tempDir = Path.Combine(Path.GetTempPath(), "ReShadeImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                _logger.LogInformation("Extracting ReShade package from: {Path} to {TempDir}", path, tempDir);

                // Extract the ZIP file using SharpSevenZip
                await Task.Run(() =>
                {
                    using var archive = new SharpSevenZip.SharpSevenZipExtractor(path);
                    archive.ExtractArchive(tempDir);
                });

                // Check if there's a reshade-shaders folder at the root level
                string actualSourceDir = tempDir;
                var topLevelDirs = Directory.GetDirectories(tempDir);
                var topLevelDirNames = topLevelDirs.Select(d => Path.GetFileName(d)).ToList();

                // If there's only one folder and it's named "reshade-shaders", enter it
                if (topLevelDirNames.Count == 1 &&
                    topLevelDirNames[0].Equals("reshade-shaders", StringComparison.OrdinalIgnoreCase))
                {
                    actualSourceDir = topLevelDirs[0];
                    _logger.LogInformation("Detected reshade-shaders folder, entering it for validation");
                }

                // Validate folder structure from the actual source directory
                var subdirectories = Directory.GetDirectories(actualSourceDir);
                var requiredFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Shaders",
                    "Textures"
                };

                var allowedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Shaders",
                    "Textures",
                    "Addons"
                };

                // Get actual folder names from the source directory
                var actualFolderNames = new HashSet<string>(
                    subdirectories.Select(dir => Path.GetFileName(dir)),
                    StringComparer.OrdinalIgnoreCase);

                // Validate:
                // 1. All actual folders must be in the allowed list (no unexpected folders)
                // 2. All required folders must exist (Shaders and Textures)
                bool hasOnlyAllowedFolders = actualFolderNames.IsSubsetOf(allowedFolderNames);
                bool hasAllRequiredFolders = requiredFolderNames.IsSubsetOf(actualFolderNames);
                bool isValidStructure = hasOnlyAllowedFolders && hasAllRequiredFolders;

                if (!isValidStructure)
                {
                    StatusMessage = Lang.ReShadeDownloadView_FolderStructureInvalid;
                    IsDownloading = false;
                    await Task.Delay(3000); // Show error for 3 seconds
                    StatusMessage = Lang.ReShadeDownloadView_StatusReady;
                    _logger.LogWarning("Invalid folder structure in package: {Path}", path);
                    return;
                }

                _logger.LogInformation("Valid ReShade package structure detected");

                // Determine target directory based on installation selection
                var targetDirs = new List<string>();
                if (IsInstallToHoYoShadeOnly || IsInstallToBoth)
                {
                    targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "HoYoShade", "reshade-shaders"));
                }
                if (IsInstallToOpenHoYoShadeOnly || IsInstallToBoth)
                {
                    targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade", "reshade-shaders"));
                }

                if (targetDirs.Count == 0)
                {
                    // No target selected, default to HoYoShade
                    targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "HoYoShade", "reshade-shaders"));
                    _logger.LogInformation("No target selected, defaulting to HoYoShade");
                }

                // Copy files to each target directory
                foreach (var targetDir in targetDirs)
                {
                    _logger.LogInformation("Moving ReShade files to: {TargetDir}", targetDir);
                    Directory.CreateDirectory(targetDir);

                    // Copy each subfolder (Shaders, Textures, Addons)
                    foreach (var sourceSubdir in subdirectories)
                    {
                        var folderName = Path.GetFileName(sourceSubdir);
                        var targetSubdir = Path.Combine(targetDir, folderName);

                        _logger.LogInformation("Copying {Folder} folder...", folderName);
                        await CopyDirectoryAsync(sourceSubdir, targetSubdir);
                    }
                }

                StatusMessage = Lang.ReShadeDownloadView_FolderImported;
                IsDownloading = false;

                // Mark installation as completed
                _hasInstalledAtLeastOnce = true;

                // Handle auto-switch logic for single target installations
                if (IsInstallToHoYoShadeOnly && (!_hasInstalledHoYoShadeTarget || IsUpdateMode))
                {
                    // Just installed HoYoShade only
                    _hasInstalledHoYoShadeTarget = true;

                    // Refresh UI states
                    OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                    OnPropertyChanged(nameof(CanInstallToBoth));

                    if (!IsUpdateMode)
                    {
                        // Disable HoYoShade options and check if OpenHoYoShade is available
                        
                        // If OpenHoYoShade option is available, auto-select it; otherwise don't select anything
                        IsInstallToHoYoShadeOnly = false;
                        IsInstallToBoth = false;
                        if (CanInstallToOpenHoYoShadeOnly)
                        {
                            IsInstallToOpenHoYoShadeOnly = true;
                        }
                        else
                        {
                            IsInstallToOpenHoYoShadeOnly = false;
                        }
                    }
                }
                else if (IsInstallToOpenHoYoShadeOnly && (!_hasInstalledOpenHoYoShadeTarget || IsUpdateMode))
                {
                    // Just installed OpenHoYoShade only
                    _hasInstalledOpenHoYoShadeTarget = true;
                    
                    // Refresh UI states
                    OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                    OnPropertyChanged(nameof(CanInstallToBoth));

                    if (!IsUpdateMode)
                    {
                        // Disable OpenHoYoShade options and check if HoYoShade is available

                        // If HoYoShade option is available, auto-select it; otherwise don't select anything
                        IsInstallToOpenHoYoShadeOnly = false;
                        IsInstallToBoth = false;
                        if (CanInstallToHoYoShadeOnly)
                        {
                            IsInstallToHoYoShadeOnly = true;
                        }
                        else
                        {
                            IsInstallToHoYoShadeOnly = false;
                        }
                    }
                }
                else if (IsInstallToBoth)
                {
                    // Installed to both targets
                    _hasInstalledHoYoShadeTarget = true;
                    _hasInstalledOpenHoYoShadeTarget = true;

                    // Refresh UI states
                    OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                    OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                    OnPropertyChanged(nameof(CanInstallToBoth));

                    if (!IsUpdateMode)
                    {
                        // Disable all options and don't select anything
                        IsInstallToHoYoShadeOnly = false;
                        IsInstallToOpenHoYoShadeOnly = false;
                        IsInstallToBoth = false;
                    }
                }

                OnPropertyChanged(nameof(CanNext));
                OnPropertyChanged(nameof(CanDownload));

                _logger.LogInformation("ReShade package import completed successfully");

                await Task.Delay(2000); // Show success message for 2 seconds
                StatusMessage = Lang.ReShadeDownloadView_StatusReady;
            }
            finally
            {
                // Clean up temporary directory
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete temporary directory: {TempDir}", tempDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import ReShade package");
            StatusMessage = string.Format(Lang.ReShadeDownloadView_StatusError, ex.Message);
            IsDownloading = false;
            await Task.Delay(3000);
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        }
    }

    [RelayCommand]
    private async Task ImportFromFolderAsync()
    {
        try
        {
            var path = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            // Validate folder structure
            StatusMessage = Lang.ReShadeDownloadView_FolderImporting;
            IsDownloading = true;

            // Check folder structure: Shaders and Textures are required, Addons is optional
            var subdirectories = Directory.GetDirectories(path);
            var requiredFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Shaders",
                "Textures"
            };

            var allowedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Shaders",
                "Textures",
                "Addons"
            };

            // Get actual folder names from the selected directory
            var actualFolderNames = new HashSet<string>(
                subdirectories.Select(dir => Path.GetFileName(dir)),
                StringComparer.OrdinalIgnoreCase);

            // Validate:
            // 1. All actual folders must be in the allowed list (no unexpected folders)
            // 2. All required folders must exist (Shaders and Textures)
            bool hasOnlyAllowedFolders = actualFolderNames.IsSubsetOf(allowedFolderNames);
            bool hasAllRequiredFolders = requiredFolderNames.IsSubsetOf(actualFolderNames);
            bool isValidStructure = hasOnlyAllowedFolders && hasAllRequiredFolders;

            if (!isValidStructure)
            {
                StatusMessage = Lang.ReShadeDownloadView_FolderStructureInvalid;
                IsDownloading = false;
                await Task.Delay(3000); // Show error for 3 seconds
                StatusMessage = Lang.ReShadeDownloadView_StatusReady;
                _logger.LogWarning("Invalid folder structure selected: {Path}", path);
                return;
            }

            _logger.LogInformation("Valid ReShade folder structure detected: {Path}", path);

            // Determine target directory based on installation selection
            var targetDirs = new List<string>();
            if (IsInstallToHoYoShadeOnly || IsInstallToBoth)
            {
                targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "HoYoShade", "reshade-shaders"));
            }
            if (IsInstallToOpenHoYoShadeOnly || IsInstallToBoth)
            {
                targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade", "reshade-shaders"));
            }

            if (targetDirs.Count == 0)
            {
                // No target selected, default to HoYoShade
                targetDirs.Add(Path.Combine(AppConfig.UserDataFolder, "HoYoShade", "reshade-shaders"));
                _logger.LogInformation("No target selected, defaulting to HoYoShade");
            }

            // Copy files to each target directory
            foreach (var targetDir in targetDirs)
            {
                _logger.LogInformation("Copying ReShade folder to: {TargetDir}", targetDir);
                Directory.CreateDirectory(targetDir);

                // Copy each subfolder (Shaders, Textures, Addons)
                foreach (var sourceSubdir in subdirectories)
                {
                    var folderName = Path.GetFileName(sourceSubdir);
                    var targetSubdir = Path.Combine(targetDir, folderName);

                    _logger.LogInformation("Copying {Folder} folder...", folderName);
                    await CopyDirectoryAsync(sourceSubdir, targetSubdir);
                }
            }

            StatusMessage = Lang.ReShadeDownloadView_FolderImported;
            IsDownloading = false;

            // Mark installation as completed
            _hasInstalledAtLeastOnce = true;

            // Handle auto-switch logic for single target installations (same as DownloadAsync)
            if (IsInstallToHoYoShadeOnly && (!_hasInstalledHoYoShadeTarget || IsUpdateMode))
            {
                // Just installed HoYoShade only
                _hasInstalledHoYoShadeTarget = true;

                // Refresh UI states
                OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                OnPropertyChanged(nameof(CanInstallToBoth));

                if (!IsUpdateMode)
                {
                    // If OpenHoYoShade option is available, auto-select it; otherwise don't select anything
                    IsInstallToHoYoShadeOnly = false;
                    IsInstallToBoth = false;
                    if (CanInstallToOpenHoYoShadeOnly)
                    {
                        IsInstallToOpenHoYoShadeOnly = true;
                    }
                    else
                    {
                        IsInstallToOpenHoYoShadeOnly = false;
                    }
                }
            }
            else if (IsInstallToOpenHoYoShadeOnly && (!_hasInstalledOpenHoYoShadeTarget || IsUpdateMode))
            {
                // Just installed OpenHoYoShade only
                _hasInstalledOpenHoYoShadeTarget = true;

                // Refresh UI states
                OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                OnPropertyChanged(nameof(CanInstallToBoth));

                if (!IsUpdateMode)
                {
                    // If HoYoShade option is available, auto-select it; otherwise don't select anything
                    IsInstallToOpenHoYoShadeOnly = false;
                    IsInstallToBoth = false;
                    if (CanInstallToHoYoShadeOnly)
                    {
                        IsInstallToHoYoShadeOnly = true;
                    }
                    else
                    {
                        IsInstallToHoYoShadeOnly = false;
                    }
                }
            }
            else if (IsInstallToBoth)
            {
                // Installed to both targets
                _hasInstalledHoYoShadeTarget = true;
                _hasInstalledOpenHoYoShadeTarget = true;

                // Refresh UI states
                OnPropertyChanged(nameof(CanInstallToHoYoShadeOnly));
                OnPropertyChanged(nameof(CanInstallToOpenHoYoShadeOnly));
                OnPropertyChanged(nameof(CanInstallToBoth));

                if (!IsUpdateMode)
                {
                    // Disable all options and don't select anything
                    IsInstallToHoYoShadeOnly = false;
                    IsInstallToOpenHoYoShadeOnly = false;
                    IsInstallToBoth = false;
                }
            }

            OnPropertyChanged(nameof(CanNext));
            OnPropertyChanged(nameof(CanDownload));

            _logger.LogInformation("ReShade folder import completed successfully");

            await Task.Delay(2000); // Show success message for 2 seconds
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import ReShade folder");
            StatusMessage = string.Format(Lang.ReShadeDownloadView_StatusError, ex.Message);
            IsDownloading = false;
            await Task.Delay(3000);
            StatusMessage = Lang.ReShadeDownloadView_StatusReady;
        }
    }

    /// <summary>
    /// Recursively copy directory contents
    /// </summary>
    private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        await Task.Run(() =>
        {
            // Create target directory if it doesn't exist
            Directory.CreateDirectory(targetDir);

            // Copy all files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, targetFile, overwrite: true);
            }

            // Recursively copy subdirectories
            foreach (var subdir in Directory.GetDirectories(sourceDir))
            {
                var targetSubdir = Path.Combine(targetDir, Path.GetFileName(subdir));
                CopyDirectoryAsync(subdir, targetSubdir).Wait();
            }
        });
    }
    
    /// <summary>
    /// Handle installation changed message from other views
    /// </summary>
    private async void OnInstallationChanged()
    {
        try
        {
            CheckInstallationStatus();
            await LoadInstalledVersionsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnInstallationChanged error");
        }
    }

    /// <summary>
    /// Load installed versions of HoYoShade and OpenHoYoShade
    /// </summary>
    private async Task LoadInstalledVersionsAsync()
    {
        try
        {
            var manifest = await _versionService.LoadManifestAsync();
            InstalledHoYoShadeVersion = manifest.HoYoShade?.Version;
            InstalledOpenHoYoShadeVersion = manifest.OpenHoYoShade?.Version;
            
            Debug.WriteLine($"ReShadeDownloadView: Loaded installed versions: HoYoShade={InstalledHoYoShadeVersion}, OpenHoYoShade={InstalledOpenHoYoShadeVersion}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ReShadeDownloadView: Failed to load installed versions: {ex.Message}");
            InstalledHoYoShadeVersion = null;
            InstalledOpenHoYoShadeVersion = null;
        }
    }
}
