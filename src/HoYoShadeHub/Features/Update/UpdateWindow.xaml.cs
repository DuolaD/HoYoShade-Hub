using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using NuGet.Versioning;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Features.Setting;
using HoYoShadeHub.Features.ViewHost;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.RPC.Update;
using HoYoShadeHub.RPC.Update.Github;
using HoYoShadeHub.RPC.Update.Metadata;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;
using HoYoShadeHub.Core.HoYoShade;


namespace HoYoShadeHub.Features.Update;

[INotifyPropertyChanged]
public sealed partial class UpdateWindow : WindowEx
{


    private readonly ILogger<UpdateWindow> _logger = AppConfig.GetLogger<UpdateWindow>();


    private readonly MetadataClient _metadataClient = AppConfig.GetService<MetadataClient>();

    private readonly UpdateService _updateService = AppConfig.GetService<UpdateService>();


    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;



    public UpdateWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += _timer_Tick;
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => this.Bindings.Update());
        this.Closed += UpdateWindow_Closed;
    }



    private void InitializeWindow()
    {
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        Title = "HoYoShadeHub - Update";
        RootGrid.RequestedTheme = ShouldAppsUseDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
    }



    private void CenterInScreen()
    {
        RectInt32 workArea = DisplayArea.GetFromWindowId(MainWindowId, DisplayAreaFallback.Nearest).WorkArea;
        if (NewVersion is null)
        {
            Grid_Update.Visibility = Visibility.Collapsed;
            int h = (int)(workArea.Height * 0.95);
            int w = (int)(h / 4.0 * 3.0);
            if (w > workArea.Width)
            {
                w = (int)(workArea.Width * 0.95);
                h = (int)(w * 4.0 / 3.0);
            }
            int x = workArea.X + (workArea.Width - w) / 2;
            int y = workArea.Y + (workArea.Height - h) / 2;
            AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        }
        else
        {
            Button_RemindLatter.Visibility = Visibility.Collapsed;
            int w = (int)(1000 * UIScale);
            int h = (int)(w / 4.0 * 3.0);
            if (w > workArea.Width || h > workArea.Height)
            {
                h = (int)(workArea.Height * 0.9);
                w = (int)(h / 4.0 * 3.0);
                if (w > workArea.Width)
                {
                    w = (int)(workArea.Width * 0.9);
                    h = (int)(w * 4.0 / 3.0);
                }
            }
            int x = workArea.X + (workArea.Width - w) / 2;
            int y = workArea.Y + (workArea.Height - h) / 2;
            AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
        }
    }



    public new void Activate()
    {
        CenterInScreen();
        base.Activate();
    }



    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Check if this is a framework update and adjust UI accordingly
        if (NewVersion?.DisableAutoUpdate ?? false)
        {
            // Hide Hub update checkboxes, show framework update checkbox
            CheckBox_HubUpdate1.Visibility = Visibility.Collapsed;
            CheckBox_HubUpdate2.Visibility = Visibility.Collapsed;
            CheckBox_FrameworkUpdate.Visibility = Visibility.Visible;
            
            // Hide architecture info for framework updates
            TextBlock_ArchLabel.Visibility = Visibility.Collapsed;
            TextBlock_ArchValue.Visibility = Visibility.Collapsed;
            
            // Fetch and display GitHub release time
            await FetchAndDisplayReleaseTimeAsync();
        }
        else
        {
            // Show Hub update checkboxes, hide framework checkbox
            CheckBox_HubUpdate1.Visibility = Visibility.Visible;
            CheckBox_HubUpdate2.Visibility = Visibility.Visible;
            CheckBox_FrameworkUpdate.Visibility = Visibility.Collapsed;
            
            // Show architecture info for Hub updates
            TextBlock_ArchLabel.Visibility = Visibility.Visible;
            TextBlock_ArchValue.Visibility = Visibility.Visible;
            
            // Display build time for Hub updates
            if (NewVersion != null)
            {
                ReleaseTimeText = NewVersion.BuildTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        
        if (UpdateService.UpdateFinished)
        {
            Finish(skipRestart: true);
        }
        _ = LoadUpdateContentAsync();
    }



    private void UpdateWindow_Closed(object sender, WindowEventArgs args)
    {
        _timer.Stop();
        _timer.Tick -= _timer_Tick;
        _updateService.StopUpdate();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        this.Closed -= UpdateWindow_Closed;
    }




    public ReleaseInfoDetail? NewVersion { get; set; }

    /// <summary>
    /// Current framework version (for HoYoShade/OpenHoYoShade updates)
    /// </summary>
    public string? CurrentFrameworkVersion { get; set; }

    /// <summary>
    /// Version of the pending framework update (for showing changelog after update)
    /// </summary>
    public string? PendingFrameworkUpdateVersion { get; set; }

    /// <summary>
    /// Name of the pending framework update (for showing changelog after update)
    /// </summary>
    public string? PendingFrameworkUpdateName { get; set; }


#if DEV
    public string ChannelText => Lang.UpdatePage_DevChannel;
#else
    public string ChannelText => AppConfig.EnablePreviewRelease ? Lang.UpdatePage_PreviewChannel : Lang.UpdatePage_StableChannel;
#endif

    /// <summary>
    /// Get the current version to display (framework version for framework updates, hub version for hub updates)
    /// </summary>
    public string CurrentVersionText => !string.IsNullOrEmpty(CurrentFrameworkVersion) 
        ? CurrentFrameworkVersion 
        : AppConfig.AppVersion;

    /// <summary>
    /// Release time label: "发布时间" for framework, "编译时间" for hub
    /// </summary>
    public string ReleaseTimeLabel => (NewVersion?.DisableAutoUpdate ?? false) 
        ? "发布时间：" 
        : Lang.UpdatePage_BuiltTime;

    /// <summary>
    /// Release time text
    /// </summary>
    public string ReleaseTimeText { get; set; } = "";

    private async Task FetchAndDisplayReleaseTimeAsync()
    {
        try
        {
            if (NewVersion == null) return;
            
            var tag = NewVersion.Version;
            var apiUrl = $"https://api.github.com/repos/DuolaD/HoYoShade/releases/tags/{tag}";
            
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub/1.0");
            
            var response = await httpClient.GetStringAsync(apiUrl);
            var release = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);
            
            var publishedAt = release.GetProperty("published_at").GetString();
            if (!string.IsNullOrEmpty(publishedAt) && DateTimeOffset.TryParse(publishedAt, out var publishTime))
            {
                ReleaseTimeText = publishTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                ReleaseTimeText = NewVersion.BuildTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch GitHub release time, using BuildTime instead");
            ReleaseTimeText = NewVersion?.BuildTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
        }
    }

    private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && NewVersion != null)
            {
                var url = fe.Tag switch
                {
                    "release" => NewVersion.DisableAutoUpdate && !string.IsNullOrEmpty(NewVersion.PackageUrl)
                        ? NewVersion.PackageUrl  // For framework updates, use PackageUrl directly
                        : $"https://github.com/DuolaD/HoYoShade-Hub/releases/tag/{NewVersion.Version}",
                    "package" => NewVersion.PackageUrl,
                    _ => null,
                };
                _logger.LogInformation("Open url: {url}", url);
                if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
                {
                    await Launcher.LaunchUriAsync(uri);
                }
            }
        }
        catch { }
    }




    #region Update



    public bool IsUpdateNowEnabled { get => field; set => SetProperty(ref field, value); } = true;

    public bool IsUpdateRemindLatterEnabled { get => field; set => SetProperty(ref field, value); } = true;

    public bool IsProgressTextVisible { get => field; set => SetProperty(ref field, value); }

    public bool IsProgressBarVisible { get => field; set => SetProperty(ref field, value); }

    public string ProgressBytesText { get => field; set => SetProperty(ref field, value); }

    public string ProgressCountText { get => field; set => SetProperty(ref field, value); }

    public string ProgressPercentText { get => field; set => SetProperty(ref field, value); }

    public string ProgressSpeedText { get => field; set => SetProperty(ref field, value); }

    public string? ErrorMessage { get => field; set => SetProperty(ref field, value); }



    public bool AutoRestartWhenUpdateFinished
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AutoRestartWhenUpdateFinished = value;
            }
        }
    } = AppConfig.AutoRestartWhenUpdateFinished;



    public bool ShowUpdateContentAfterUpdateRestart
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.ShowUpdateContentAfterUpdateRestart = value;
            }
        }
    } = AppConfig.ShowUpdateContentAfterUpdateRestart;

    /// <summary>
    /// Show update content after framework update (replaces both Hub update options)
    /// </summary>
    public bool ShowUpdateContentAfterFrameworkUpdate
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.SetValue(value, "ShowUpdateContentAfterFrameworkUpdate");
            }
        }
    } = AppConfig.GetValue(true, "ShowUpdateContentAfterFrameworkUpdate");



    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        try
        {
            ErrorMessage = null;
            
            // Check if this is a framework update (DisableAutoUpdate = true)
            if (NewVersion?.DisableAutoUpdate ?? false)
            {
                // Framework update: navigate to download page instead of downloading
                _logger.LogInformation("Framework update detected, navigating to download page");
                
                // Store the framework update info to show changelog later
                if (ShowUpdateContentAfterFrameworkUpdate)
                {
                    AppConfig.SetValue(NewVersion.Version, "PendingFrameworkUpdateVersion");
                    // Store the framework name to fetch correct changelog later
                    // Determine framework name from package URL or use CurrentFrameworkVersion
                    string frameworkName = DetermineFrameworkName();
                    AppConfig.SetValue(frameworkName, "PendingFrameworkUpdateName");
                    _logger.LogInformation("Stored pending framework update: {Version} ({Framework})", 
                        NewVersion.Version, frameworkName);
                }
                
                // Send message to navigate to download page
                WeakReferenceMessenger.Default.Send(new NavigateToDownloadPageMessage(true));
                
                // Close the update window
                this.Close();
                return;
            }
            
            // Hub update: proceed with normal update flow
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = false;

            if (NewVersion != null)
            {
                _timer.Start();
                await _updateService.StartUpdateAsync(NewVersion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update now");
            Button_UpdateNow.IsEnabled = true;
            Button_RemindLatter.IsEnabled = true;
        }
    }

    /// <summary>
    /// Determine the framework name from the current update context
    /// </summary>
    private string DetermineFrameworkName()
    {
        // Try to determine from CurrentFrameworkVersion or PackageUrl
        if (!string.IsNullOrEmpty(CurrentFrameworkVersion))
        {
            // CurrentFrameworkVersion is set for framework updates
            // Check the package URL or other indicators
            if (NewVersion?.PackageUrl?.Contains("OpenHoYoShade", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return "OpenHoYoShade";
            }
            else if (NewVersion?.PackageUrl?.Contains("HoYoShade", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return "HoYoShade";
            }
        }
        
        // Default to HoYoShade if we can't determine
        return "HoYoShade";
    }



    private void UpdateProgressState()
    {
        if (_updateService.State is UpdateState.Pending)
        {
            IsProgressTextVisible = true;
            IsProgressBarVisible = true;
            ProgressBar_Update.IsIndeterminate = true;
            UpdateProgressValue();
        }
        else if (_updateService.State is UpdateState.Downloading)
        {
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = false;
            IsProgressBarVisible = true;
            IsProgressTextVisible = true;
            ProgressBar_Update.IsIndeterminate = false;
            UpdateProgressValue();
        }
        else if (_updateService.State is UpdateState.Finish)
        {
            IsProgressTextVisible = false;
            ProgressBar_Update.IsIndeterminate = false;
            ProgressBar_Update.Value = 100;
        }
        else if (_updateService.State is UpdateState.Stop)
        {
            IsUpdateNowEnabled = true;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = null;
        }
        else if (_updateService.State is UpdateState.Error)
        {
            IsUpdateNowEnabled = true;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = _updateService.ErrorMessage;
        }
        else if (_updateService.State is UpdateState.NotSupport)
        {
            IsUpdateNowEnabled = false;
            IsUpdateRemindLatterEnabled = true;
            IsProgressTextVisible = false;
            IsProgressBarVisible = false;
            ErrorMessage = _updateService.ErrorMessage;
        }
    }



    private void UpdateProgressValue()
    {
        if (_updateService.Progress_TotalBytes == 0 || _updateService.Progress_DownloadBytes == 0)
        {
            ProgressBytesText = "";
            ProgressCountText = "";
            return;
        }
        const double mb = 1 << 20;
        ProgressBytesText = $"{_updateService.Progress_DownloadBytes / mb:F2}/{_updateService.Progress_TotalBytes / mb:F2} MB";
        ProgressCountText = $"{_updateService.Progress_DownloadFileCount}/{_updateService.Progress_TotalFileCount}";
        var progress = (double)_updateService.Progress_DownloadBytes / _updateService.Progress_TotalBytes;
        ProgressPercentText = $"{progress:P1}";
        ProgressBar_Update.Value = progress * 100;
    }



    private void _timer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {

        try
        {
            UpdateProgressState();
            if (_updateService.State is UpdateState.Finish)
            {
                _timer.Stop();
                Finish();
            }
            else if (_updateService.State is UpdateState.Stop or UpdateState.Error or UpdateState.NotSupport)
            {
                _timer.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update progress");
        }
    }



    private async void Finish(bool skipRestart = false)
    {
        AppConfig.IgnoreVersion = null;
        Button_UpdateNow.Visibility = Visibility.Collapsed;
        Button_Restart.Visibility = Visibility.Visible;
        AppConfig.GetService<RpcService>().KeepRunningOnExited(false, noLongerChange: true);
        
        // If this was a framework update, update the manifest and notify other views
        if (NewVersion?.DisableAutoUpdate ?? false)
        {
            await UpdateFrameworkVersionManifestAsync();
            WeakReferenceMessenger.Default.Send(new HoYoShadeInstallationChangedMessage());
        }
        
        if (AutoRestartWhenUpdateFinished && !skipRestart)
        {
            Restart();
        }
    }

    private async Task UpdateFrameworkVersionManifestAsync()
    {
        try
        {
            var versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
            string frameworkName = AppConfig.GetValue<string>(null, "PendingFrameworkUpdateName");
            string tag = AppConfig.GetValue<string>(null, "PendingFrameworkUpdateVersion");

            if (string.IsNullOrEmpty(frameworkName)) frameworkName = DetermineFrameworkName();
            if (string.IsNullOrEmpty(tag)) tag = NewVersion?.Version;

            if (!string.IsNullOrEmpty(tag))
            {
                if (frameworkName == "HoYoShade")
                {
                     await versionService.UpdateHoYoShadeVersionAsync(tag, "github_release");
                     _logger.LogInformation("Updated HoYoShade version manifest to {Version}", tag);
                }
                else if (frameworkName == "OpenHoYoShade")
                {
                     await versionService.UpdateOpenHoYoShadeVersionAsync(tag, "github_release");
                     _logger.LogInformation("Updated OpenHoYoShade version manifest to {Version}", tag);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update version manifest");
        }
    }

    [RelayCommand]
    private void Restart()
    {
        try
        {
            string? launcher = AppConfig.HoYoShadeHubLauncherExecutePath;
            if (File.Exists(launcher))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launcher,
                    WorkingDirectory = Path.GetDirectoryName(launcher),
                });
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Restart");
            ErrorMessage = ex.Message;
        }
    }



    [RelayCommand]
    private void RemindMeLatter()
    {
        this.Close();
    }



    [RelayCommand]
    private void IgnoreThisVersion()
    {
        if (NewVersion is null)
        {
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        else
        {
            AppConfig.IgnoreVersion = NewVersion.Version;
        }
        this.Close();
    }




    private void Button_UpdateRemindLatter_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Button_UpdateRemindLatter.Opacity = 1;
    }


    private void Button_UpdateRemindLatter_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Button_UpdateRemindLatter.Opacity = 0;
    }




    #endregion





    #region Update Content WebView





    private async Task LoadUpdateContentAsync()
    {
        try
        {
            StackPanel_Loading.Visibility = Visibility.Visible;
            StackPanel_Error.Visibility = Visibility.Collapsed;

            await webview.EnsureCoreWebView2Async();
            webview.CoreWebView2.Profile.PreferredColorScheme = ShouldAppsUseDarkMode() ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
            webview.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
            webview.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            webview.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            webview.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            string markdown = await GetReleaseContentMarkdownAsync();
            string html = await RenderMarkdownAsync(markdown);
            webview.NavigateToString(html);
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Load recent update content");
            TextBlock_Error.Text = Lang.Common_WebView2ComponentInitializationFailed;
            StackPanel_Loading.Visibility = Visibility.Collapsed;
            StackPanel_Error.Visibility = Visibility.Visible;
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException)
        {
            _logger.LogError(ex, "Load recent update content");
            
            // Check if this is a framework update
            bool isFrameworkUpdate = NewVersion?.DisableAutoUpdate ?? false;
            string tag = NewVersion?.Version ?? AppConfig.AppVersion;
            
            if (isFrameworkUpdate)
            {
                // For framework updates, redirect to HoYoShade repository
                webview.Source = new Uri($"https://github.com/DuolaD/HoYoShade/releases/tag/{tag}");
            }
            else
            {
                // For Hub updates, redirect to Hub repository
                webview.Source = new Uri($"https://github.com/DuolaD/HoYoShade-Hub/releases/tag/{tag}");
            }
            
            webview.Visibility = Visibility.Visible;
            StackPanel_Loading.Visibility = Visibility.Collapsed;
            StackPanel_Error.Visibility = Visibility.Collapsed;
            AppConfig.LastAppVersion = AppConfig.AppVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load recent update content");
            TextBlock_Error.Text = Lang.DownloadGamePage_UnknownError;
            StackPanel_Loading.Visibility = Visibility.Collapsed;
            StackPanel_Error.Visibility = Visibility.Visible;
        }
    }

    private async Task<string> GetReleaseContentMarkdownAsync()
    {
        bool showPrerelease = false;
        NuGetVersion? startVersion, endVersion;
        
        // Check if this is a framework update (DisableAutoUpdate = true)
        bool isFrameworkUpdate = NewVersion?.DisableAutoUpdate ?? false;
        
        // Check if we're showing changelog for a completed framework update
        // We priorititize the properties set on the window instance, then fallback to AppConfig
        string? pendingFrameworkVersion = PendingFrameworkUpdateVersion;
        string? pendingFrameworkName = PendingFrameworkUpdateName;

        if (NewVersion is null)
        {
            if (string.IsNullOrEmpty(pendingFrameworkVersion) || string.IsNullOrEmpty(pendingFrameworkName))
            {
                // Fallback to checking AppConfig directly if properties weren't set
                // This handles cases where UpdateWindow might be instantiated differently
                pendingFrameworkVersion = AppConfig.GetValue<string>(null, "PendingFrameworkUpdateVersion");
                pendingFrameworkName = AppConfig.GetValue<string>(null, "PendingFrameworkUpdateName");
            }
            
            if (!string.IsNullOrEmpty(pendingFrameworkVersion) && !string.IsNullOrEmpty(pendingFrameworkName))
            {
                _logger.LogInformation("Fetching changelog for completed framework update: {Version} ({Framework})", 
                    pendingFrameworkVersion, pendingFrameworkName);
                isFrameworkUpdate = true;
            }
        }
        
        if (isFrameworkUpdate)
        {
            // For framework updates, fetch the GitHub release directly
            try
            {
                // Use the version from either NewVersion or pending update
                var tag = NewVersion?.Version ?? pendingFrameworkVersion;
                
                if (!string.IsNullOrEmpty(tag))
                {
                    var frameworkMarkdown = new StringBuilder();
                    
                    // Fetch release from GitHub API (both frameworks are in DuolaD/HoYoShade repo)
                    var repoOwner = "DuolaD";
                    var repoName = "HoYoShade";
                    
                    // Use GitHub API to get the release
                    var apiUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/tags/{tag}";
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub/1.0");
                    
                    var response = await httpClient.GetStringAsync(apiUrl);
                    var release = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);
                    
                    var name = release.GetProperty("name").GetString() ?? tag;
                    var body = release.GetProperty("body").GetString() ?? "";
                    
                    frameworkMarkdown.AppendLine($"# {name}");
                    frameworkMarkdown.AppendLine();
                    frameworkMarkdown.AppendLine(body);
                    
                    return frameworkMarkdown.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch framework release info");
                // Rethrow to trigger the fallback logic in LoadUpdateContentAsync
                throw;
            }
        }
        
        // Original logic for Hub updates
        if (NewVersion is null)
        {
            _ = NuGetVersion.TryParse(AppConfig.LastAppVersion, out startVersion);
            _ = NuGetVersion.TryParse(AppConfig.AppVersion, out endVersion);
        }
        else
        {
            _ = NuGetVersion.TryParse(AppConfig.AppVersion, out startVersion);
            _ = NuGetVersion.TryParse(NewVersion.Version, out endVersion);
        }
        startVersion ??= new NuGetVersion(0, 0, 0);
        endVersion ??= new NuGetVersion(int.MaxValue, int.MaxValue, int.MaxValue);
        if (endVersion.IsPrerelease)
        {
            showPrerelease = true;
            if (startVersion.IsPrerelease)
            {
                if (startVersion.Patch - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major, startVersion.Minor, startVersion.Patch - 1);
                }
                else if (startVersion.Minor - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major, startVersion.Minor - 1, int.MaxValue);
                }
                else if (startVersion.Major - 1 >= 0)
                {
                    startVersion = new NuGetVersion(startVersion.Major - 1, int.MaxValue, int.MaxValue);
                }
            }
        }

        var releases = await _metadataClient.GetGithubReleaseAsync(1, 20);
        var markdown = new StringBuilder();
        int count = 0;
        foreach (var release in releases)
        {
            if (NuGetVersion.TryParse(release.TagName, out var version))
            {
                if (version > startVersion && version <= endVersion)
                {
                    // 只显示最新的几个连续的预览版，最新稳定版之前的预览版不显示
                    if (!version.IsPrerelease && !release.Prerelease)
                    {
                        showPrerelease = false;
                    }
                    if (!(showPrerelease ^ version.IsPrerelease))
                    {
                        AppendReleaseToStringBuilder(release, markdown);
                        count++;
                    }
                }
            }
            else
            {
                AppendReleaseToStringBuilder(release, markdown);
                count++;
            }
            if (count >= 10)
            {
                break;
            }
        }
        if (markdown.Length == 0)
        {
            try
            {
                var r = await _metadataClient.GetGithubReleaseAsync(NewVersion?.Version ?? AppConfig.AppVersion);
                if (r is not null)
                {
                    AppendReleaseToStringBuilder(r, markdown);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (releases.FirstOrDefault() is GithubRelease r)
                {
                    AppendReleaseToStringBuilder(r, markdown);
                }
            }
        }
        return markdown.ToString();
    }



    private static void AppendReleaseToStringBuilder(GithubRelease release, StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"# {release.Name}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(release.Body);
        stringBuilder.AppendLine("<br>");
        stringBuilder.AppendLine();
    }



    private async Task<string> RenderMarkdownAsync(string markdown)
    {
        string html = await _metadataClient.RenderGithubMarkdownAsync(markdown);
        var cssFile = Path.Combine(AppContext.BaseDirectory, @"Assets\CSS\github-markdown.css");
        string? css = null;
        if (File.Exists(cssFile))
        {
            css = await File.ReadAllTextAsync(cssFile);
            css = $"<style>{css}</style>";
        }
        else
        {
            css = """<link href="https://cdnjs.cloudflare.com/ajax/libs/github-markdown-css/5.8.1/github-markdown.min.css" type="text/css" rel="stylesheet" />""";
        }
        
        // Add GitHub Alerts CSS support
        string alertsCss = """
            <style>
              /* GitHub Alerts Styling */
              .markdown-alert {
                padding: 0.5rem 1rem;
                margin-bottom: 16px;
                color: inherit;
                border-left: 0.25em solid #888;
              }
              .markdown-alert > :first-child {
                margin-top: 0;
              }
              .markdown-alert > :last-child {
                margin-bottom: 0;
              }
              .markdown-alert .markdown-alert-title {
                display: flex;
                font-weight: 500;
                align-items: center;
                line-height: 1;
              }
              .markdown-alert .markdown-alert-title svg {
                margin-right: 0.5rem;
              }
              
              /* Note Alert */
              .markdown-alert.markdown-alert-note {
                border-left-color: #0969da;
              }
              .markdown-alert.markdown-alert-note .markdown-alert-title {
                color: #0969da;
              }
              
              /* Important Alert */
              .markdown-alert.markdown-alert-important {
                border-left-color: #8250df;
              }
              .markdown-alert.markdown-alert-important .markdown-alert-title {
                color: #8250df;
              }
              
              /* Warning Alert */
              .markdown-alert.markdown-alert-warning {
                border-left-color: #9a6700;
              }
              .markdown-alert.markdown-alert-warning .markdown-alert-title {
                color: #9a6700;
              }
              
              /* Tip Alert */
              .markdown-alert.markdown-alert-tip {
                border-left-color: #1a7f37;
              }
              .markdown-alert.markdown-alert-tip .markdown-alert-title {
                color: #1a7f37;
              }
              
              /* Caution Alert */
              .markdown-alert.markdown-alert-caution {
                border-left-color: #cf222e;
              }
              .markdown-alert.markdown-alert-caution .markdown-alert-title {
                color: #cf222e;
              }
              
              /* Dark mode support */
              @media (prefers-color-scheme: dark) {
                .markdown-alert.markdown-alert-note {
                  border-left-color: #2f81f7;
                }
                .markdown-alert.markdown-alert-note .markdown-alert-title {
                  color: #2f81f7;
                }
                .markdown-alert.markdown-alert-important {
                  border-left-color: #a371f7;
                }
                .markdown-alert.markdown-alert-important .markdown-alert-title {
                  color: #a371f7;
                }
                .markdown-alert.markdown-alert-warning {
                  border-left-color: #d29922;
                }
                .markdown-alert.markdown-alert-warning .markdown-alert-title {
                  color: #d29922;
                }
                .markdown-alert.markdown-alert-tip {
                  border-left-color: #3fb950;
                }
                .markdown-alert.markdown-alert-tip .markdown-alert-title {
                  color: #3fb950;
                }
                .markdown-alert.markdown-alert-caution {
                  border-left-color: #f85149;
                }
                .markdown-alert.markdown-alert-caution .markdown-alert-title {
                  color: #f85149;
                }
              }
            </style>
            """;
        
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <base target="_blank">
              {{css}}
              {{alertsCss}}
              <style>
                @media (prefers-color-scheme: light) {
                  ::-webkit-scrollbar {
                    width: 6px
                  }
                  ::-webkit-scrollbar-thumb {
                    background-color: #b8b8b8;
                    border-radius: 1000px 0px 0px 1000px
                  }
                  ::-webkit-scrollbar-thumb:hover {
                    background-color: #8b8b8b
                  }
                }
                @media (prefers-color-scheme: dark) {
                  ::-webkit-scrollbar {
                    width: 6px
                  }
                  ::-webkit-scrollbar-thumb {
                    background-color: #646464;
                    border-radius: 1000px 0px 0px 1000px
                  }
                  ::-webkit-scrollbar-thumb:hover {
                    background-color: #8b8b8b
                  }
                }
              </style>
            </head>
            <body style="margin: 12px 24px 12px 24px; overflow-x: hidden;">
              <article class="markdown-body" style="background: transparent;">
                {{html}}
              </article>
              <script>
                document.querySelectorAll('img[data-canonical-src]').forEach(img => {
                  const canonical = img.getAttribute('data-canonical-src');
                  if (canonical) {
                    img.src = canonical;
                  }
                  const parent = img.parentElement;
                  if (parent && parent.tagName.toLowerCase() === 'a') {
                    parent.href = canonical;
                  }
                });
              </script>
            </body>
            </html>
            """;
    }



    private void CoreWebView2_DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
    {
        webview.Focus(FocusState.Programmatic);
        webview.Visibility = Visibility.Visible;
        StackPanel_Loading.Visibility = Visibility.Collapsed;
        StackPanel_Error.Visibility = Visibility.Collapsed;
    }



    private void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        try
        {
            _ = Launcher.LaunchUriAsync(new Uri(args.Uri));
            args.Handled = true;
        }
        catch { }
    }



    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadUpdateContentAsync();
    }




    #endregion





    #region Converter



    public static string ByteLengthToString(long byteLength)
    {
        double length = byteLength;
        return length switch
        {
            >= (1 << 30) => $"{length / (1 << 30):F2} GB",
            >= (1 << 20) => $"{length / (1 << 20):F2} MB",
            _ => $"{length / (1 << 10):F2} KB",
        };
    }



    public static Visibility StringToVisibility(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
    }


    #endregion



}
