using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core.HoYoShade;
using HoYoShadeHub.Core.Metadata.Github;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Features.Setting;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using HoYoShadeHub.Models;
using HoYoShadeHub.RPC;
using HoYoShadeHub.RPC.HoYoShadeInstall;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class QuickSetupView : UserControl
{
    private readonly ILogger<QuickSetupView> _logger = AppConfig.GetLogger<QuickSetupView>();
    private readonly HoYoShadeVersionService _versionService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _languageInitialized;

    public ObservableCollection<DownloadServerItem> DownloadServers { get; } = new();

    [ObservableProperty]
    private DownloadServerItem? selectedDownloadServer;

    partial void OnSelectedDownloadServerChanged(DownloadServerItem? value)
    {
        if (value != null)
        {
            AppConfig.HoYoShadeFrameworkDownloadServer = value.ServerIndex;
        }
    }

    private static readonly HashSet<string> HiddenIncompatibleVersionTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "3.0.0-beta.1",
        "3.0.0-beta.2",
    };

    public QuickSetupView()
    {
        this.InitializeComponent();
        _versionService = new HoYoShadeVersionService(AppConfig.UserDataFolder);
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
        WeakReferenceMessenger.Default.Register<EchSettingChangedMessage>(this, (r, m) => UpdateDownloadServers());
        WeakReferenceMessenger.Default.Register<HoYoShadeInstallationChangedMessage>(this, (r, m) => OnInstallationChanged());
    }

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartInstall))]
    [NotifyPropertyChangedFor(nameof(CanNavigateToCustom))]
    [NotifyPropertyChangedFor(nameof(StartButtonText))]
    private bool isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCompleted))]
    [NotifyPropertyChangedFor(nameof(CanStartInstall))]
    [NotifyPropertyChangedFor(nameof(CanNavigateToCustom))]
    [NotifyPropertyChangedFor(nameof(StartButtonText))]
    private bool isCompleted;

    public bool IsNotCompleted => !IsCompleted;
    public bool CanStartInstall => !IsDownloading && !IsCompleted;
    public bool CanNavigateToCustom => !IsDownloading;
    public string StartButtonText => IsDownloading
        ? Lang.QuickSetupView_Installing
        : (IsCompleted ? Lang.QuickSetupView_Completed : Lang.HoYoShadeDownloadView_DownloadAndInstall);

    [ObservableProperty]
    private double overallProgress;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private string speedAndProgress = "";

    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        HoYoShadeHub.Features.Background.AccentColorHelper.ResetToDefaultLauncherAccentColor();
        InitializeLanguageSelector();
        UpdateDownloadServers();
        await LoadInstalledVersionsAsync();
        CheckInstallationStatus();
    }

    private void OnLanguageChanged()
    {
        Lang.Culture = CultureInfo.CurrentUICulture;
        this.Bindings.Update();
        OnPropertyChanged(nameof(StartButtonText));
        UpdateDownloadServers();
    }

    private void InitializeLanguageSelector()
    {
        try
        {
            var lang = AppConfig.Language;
            ComboBox_Language.Items.Clear();
            ComboBox_Language.Items.Add(new ComboBoxItem
            {
                Content = Lang.ResourceManager.GetString(nameof(Lang.SettingPage_FollowSystem), AppConfig.SystemCulture),
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
            if (ComboBox_Language.SelectedItem is ComboBoxItem item && _languageInitialized)
            {
                var lang = item.Tag as string;
                AppConfig.Language = lang;
                if (string.IsNullOrWhiteSpace(lang))
                {
                    CultureInfo.CurrentUICulture = AppConfig.SystemCulture;
                }
                else
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(lang);
                }
                Lang.Culture = CultureInfo.CurrentUICulture;
                this.Bindings.Update();
                WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
                AppConfig.SaveConfiguration();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void UpdateDownloadServers()
    {
        var selectedIndex = SelectedDownloadServer?.ServerIndex ?? AppConfig.HoYoShadeFrameworkDownloadServer;
        DownloadServers.Clear();
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_AutoSelect, ServerIndex = -1 });
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_GithubDirect, ServerIndex = 0 });
        DownloadServers.Add(new DownloadServerItem { Name = AppConfig.EnableEch ? "Cloudflare ECH" : Lang.HoYoShadeDownloadView_Server_Cloudflare, ServerIndex = 1 });
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_TencentCloud, ServerIndex = 2 });
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_AlibabaCloud, ServerIndex = 3 });

        var toSelect = DownloadServers.FirstOrDefault(x => x.ServerIndex == selectedIndex);
        SelectedDownloadServer = toSelect ?? DownloadServers[0];

        _ = UpdateLatenciesAsync();
    }

    private async Task UpdateLatenciesAsync()
    {
        var httpClient = AppConfig.GetService<HttpClient>();
        if (httpClient == null) return;

        var serversToUpdate = DownloadServers.Where(s => s.ServerIndex != -1).ToList();
        foreach (var server in serversToUpdate)
        {
            server.LatencyText = "Ping...";
            server.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        var tasks = serversToUpdate.Select(async server =>
        {
            long latency = await CloudProxyManager.PingServerAsync(server.ServerIndex, httpClient);
            if (latency >= 0)
            {
                server.LatencyText = $"{latency}ms";
                if (latency <= 600) server.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                else server.LatencyColor = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC5, 0x7F, 0x0A));
            }
            else
            {
                server.LatencyText = "Timeout";
                server.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
            }
        });
        await Task.WhenAll(tasks);
    }

    private async void Button_NetworkSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NetworkSettingDialog { XamlRoot = this.XamlRoot };
        await dialog.ShowAsync();
    }

    private void Hyperlink_NetworkSettings_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        Button_NetworkSettings_Click(sender, null!);
    }

    [RelayCommand]
    private void NavigateToCustom()
    {
        WeakReferenceMessenger.Default.Send(new NavigateToDownloadPageMessage());
    }

    [RelayCommand]
    private void Finish()
    {
        WeakReferenceMessenger.Default.Send(new WelcomePageFinishedMessage());
    }

    [RelayCommand]
    private void Stop()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            IsDownloading = false;
            StatusMessage = Lang.QuickSetupView_StatusCancelled;
            SpeedAndProgress = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop quick setup");
        }
    }

    [RelayCommand]
    private async Task StartInstallAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            IsDownloading = true;
            OverallProgress = 0;
            StatusMessage = Lang.QuickSetupView_StatusPreparing;
            SpeedAndProgress = "";

            // Step 1: Ensure RPC server is running
            await EnsureFreshRpcServerAsync(ct);

            // Step 2: Fetch latest stable HoYoShade release
            StatusMessage = Lang.QuickSetupView_StatusFetchingRelease;
            var (release, asset) = await FetchLatestStableReleaseAsync(ct);
            if (release == null || asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                throw new Exception("未能获取到可用的 HoYoShade 稳定版框架安装包。");
            }

            _logger.LogInformation("Selected release: {TagName}, Asset: {AssetName}", release.TagName, asset.Name);

            // Step 3: Install HoYoShade framework
            StatusMessage = string.Format(Lang.QuickSetupView_StatusDownloadingFramework, release.TagName);
            await InstallFrameworkAsync(release, asset, ct);

            // Step 4: Install all ReShade Shaders & Addons
            StatusMessage = Lang.QuickSetupView_StatusDownloadingShaders;
            await InstallReShadeShadersAsync(ct);

            // Completed
            OverallProgress = 100;
            StatusMessage = Lang.QuickSetupView_StatusFinished;
            SpeedAndProgress = "100%";
            IsCompleted = true;
            IsDownloading = false;

            await LoadInstalledVersionsAsync();
            CheckInstallationStatus();

            // Notify whole application about installation state update
            WeakReferenceMessenger.Default.Send(new HoYoShadeInstallationChangedMessage());
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            StatusMessage = Lang.QuickSetupView_StatusCancelled;
            SpeedAndProgress = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QuickSetup failed");
            IsDownloading = false;
            StatusMessage = $"{ex.Message}";
            SpeedAndProgress = "";
        }
    }

    private IEnumerable<int> GetServerSequence()
    {
        var selected = SelectedDownloadServer?.ServerIndex ?? AppConfig.HoYoShadeFrameworkDownloadServer;
        if (selected != -1)
        {
            // First try selected, then fallback to others
            var fallback = CloudProxyManager.GetAutoSelectFallbackSequence(false);
            return new[] { selected }.Concat(fallback.Where(s => s != selected));
        }
        return CloudProxyManager.GetAutoSelectFallbackSequence(false);
    }

    private async Task<(GithubRelease? release, GithubAsset? asset)> FetchLatestStableReleaseAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

        string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
        var serverSequence = GetServerSequence();
        GithubRelease[]? releases = null;

        foreach (var currentServerIndex in serverSequence)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? proxyUrl = CloudProxyManager.GetProxyUrl(currentServerIndex);
            string currentApiUrl = string.IsNullOrWhiteSpace(proxyUrl) ? apiUrl : CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);

            try
            {
                releases = await client.GetFromJsonAsync<GithubRelease[]>(currentApiUrl, cancellationToken);
                if (releases != null && releases.Length > 0)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch releases from server {ServerIndex}", currentServerIndex);
            }
        }

        if (releases == null)
        {
            return (null, null);
        }

        // Find the latest stable release (Prerelease == false, major >= 3, not hidden)
        foreach (var r in releases)
        {
            if (IsHiddenIncompatibleVersionTag(r.TagName)) continue;
            if (!IsVersionV3OrAbove(r.TagName)) continue;
            if (r.Prerelease) continue; // Skip preview/prerelease versions

            var asset = r.Assets?.FirstOrDefault(a =>
                a.Name.Contains("HoYoShade", StringComparison.OrdinalIgnoreCase) &&
                !a.Name.Contains("OpenHoYoShade", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset != null)
            {
                return (r, asset);
            }
        }

        // Fallback: if no strict stable found, pick the first V3 release
        foreach (var r in releases)
        {
            if (IsHiddenIncompatibleVersionTag(r.TagName)) continue;
            if (!IsVersionV3OrAbove(r.TagName)) continue;

            var asset = r.Assets?.FirstOrDefault(a =>
                a.Name.Contains("HoYoShade", StringComparison.OrdinalIgnoreCase) &&
                !a.Name.Contains("OpenHoYoShade", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (asset != null)
            {
                return (r, asset);
            }
        }

        return (null, null);
    }

    private async Task InstallFrameworkAsync(GithubRelease release, GithubAsset asset, CancellationToken cancellationToken)
    {
        var targetPath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
        var serverSequence = GetServerSequence();
        var httpClient = AppConfig.GetService<HttpClient>();
        bool success = false;
        Exception? lastException = null;

        foreach (var currentServerIndex in serverSequence)
        {
            if (cancellationToken.IsCancellationRequested) break;

            long ping = await CloudProxyManager.PingServerAsync(currentServerIndex, httpClient);
            if (ping < 0) continue;

            string[] proxies = currentServerIndex == 0 ? new string[] { null! } : CloudProxyManager.GetAllProxiesForServer(currentServerIndex).OrderBy(_ => Random.Shared.Next()).ToArray();

            foreach (var proxyUrl in proxies)
            {
                if (cancellationToken.IsCancellationRequested) break;

                string tryUrl = asset.BrowserDownloadUrl;
                if (!string.IsNullOrWhiteSpace(proxyUrl))
                {
                    tryUrl = CloudProxyManager.ApplyProxy(tryUrl, proxyUrl);
                }

                try
                {
                    var client = RpcService.CreateRpcClient<HoYoShadeInstaller.HoYoShadeInstallerClient>();
                    var request = new InstallHoYoShadeRequest
                    {
                        DownloadUrl = tryUrl,
                        TargetPath = targetPath,
                        PresetsHandling = 0, // Overwrite
                        VersionTag = release.TagName,
                    };

                    using var call = client.InstallHoYoShade(request, cancellationToken: cancellationToken);

                    long lastBytes = 0;
                    var lastTime = DateTime.Now;

                    while (await call.ResponseStream.MoveNext(cancellationToken))
                    {
                        var progress = call.ResponseStream.Current;

                        if (progress.TotalBytes > 0)
                        {
                            double dlPercent = (double)progress.DownloadBytes / progress.TotalBytes * 100;
                            OverallProgress = dlPercent * 0.45; // 0% ~ 45% of overall

                            var now = DateTime.Now;
                            var elapsed = (now - lastTime).TotalSeconds;
                            if (elapsed >= 0.5)
                            {
                                var diff = progress.DownloadBytes - lastBytes;
                                if (diff < 0) diff = 0;
                                double speed = diff / elapsed;
                                SpeedAndProgress = $"{FormatSpeed((long)speed)} - {dlPercent:F1}%";
                                lastBytes = progress.DownloadBytes;
                                lastTime = now;
                            }
                        }

                        if (progress.State == 2) // Extracting
                        {
                            StatusMessage = Lang.QuickSetupView_StatusExtractingFramework;
                            OverallProgress = 48;
                        }
                        else if (progress.State == 3) // Finished
                        {
                            OverallProgress = 50;
                            StatusMessage = Lang.QuickSetupView_StatusBuildingIni;
                            await RunIniBuildAsync("HoYoShade", targetPath, cancellationToken);
                            await _versionService.UpdateHoYoShadeVersionAsync(release.TagName, "quick_setup", null);
                            success = true;
                            break;
                        }
                        else if (progress.State == 4) // Error
                        {
                            throw new Exception(progress.ErrorMessage ?? "Framework installation failed");
                        }
                    }

                    if (success) break;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Failed downloading framework from proxy {Proxy}", proxyUrl);
                }
            }

            if (success) break;
        }

        if (!success)
        {
            throw lastException ?? new Exception("下载并安装 HoYoShade 框架失败。");
        }
    }

    private async Task InstallReShadeShadersAsync(CancellationToken cancellationToken)
    {
        var basePath = AppConfig.UserDataFolder;
        var serverSequence = GetServerSequence();
        var httpClient = AppConfig.GetService<HttpClient>();
        bool success = false;
        Exception? lastException = null;

        foreach (var currentServerIndex in serverSequence)
        {
            if (cancellationToken.IsCancellationRequested) break;

            long ping = await CloudProxyManager.PingServerAsync(currentServerIndex, httpClient);
            if (ping < 0) continue;

            string[] proxies = currentServerIndex == 0 ? new string[] { "" } : CloudProxyManager.GetAllProxiesForServer(currentServerIndex).OrderBy(_ => Random.Shared.Next()).ToArray();

            foreach (var proxyUrl in proxies)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var client = RpcService.CreateRpcClient<HoYoShadeInstaller.HoYoShadeInstallerClient>();
                    var request = new InstallReShadePackRequest
                    {
                        BasePath = basePath,
                        InstallTarget = 0, // 0: HoYoShade
                        InstallMode = 0,   // 0: InstallAll (Shaders + Addons)
                        DownloadServer = proxyUrl,
                        EnableEch = AppConfig.EnableEch,
                        DohUrl = AppConfig.EnableEch ? DohService.GetCurrentDohUrl() : "",
                    };

                    using var call = client.InstallReShadePack(request, cancellationToken: cancellationToken);

                    while (await call.ResponseStream.MoveNext(cancellationToken))
                    {
                        var progress = call.ResponseStream.Current;

                        if (progress.State == 1) // Downloading
                        {
                            string typeLabel = progress.CurrentFileType == 0 ? Lang.QuickSetupView_TypeShaders : Lang.QuickSetupView_TypeAddons;
                            StatusMessage = string.Format(Lang.QuickSetupView_StatusDownloadingItem, typeLabel, progress.CurrentFile ?? "");

                            double currentPct = progress.TotalFiles > 0 ? ((double)progress.DownloadedFiles / progress.TotalFiles * 100) : 0;
                            OverallProgress = 50 + (currentPct * 0.5); // 50% ~ 100% of overall

                            string speedText = FormatSpeed(progress.DownloadSpeedBytesPerSec);
                            SpeedAndProgress = $"{speedText} - {currentPct:F1}% ({progress.DownloadedFiles}/{progress.TotalFiles})";
                        }
                        else if (progress.State == 3) // Finished
                        {
                            success = true;
                            break;
                        }
                        else if (progress.State == 4) // Error
                        {
                            throw new Exception(progress.ErrorMessage ?? "ReShade shaders installation failed");
                        }
                    }

                    if (success) break;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Failed downloading shaders from proxy {Proxy}", proxyUrl);
                }
            }

            if (success) break;
        }

        if (!success)
        {
            throw lastException ?? new Exception("下载并安装 ReShade 着色器与插件失败。");
        }
    }

    private async Task RunIniBuildAsync(string frameworkName, string frameworkPath, CancellationToken cancellationToken)
    {
        var launcherResourcePath = Path.Combine(frameworkPath, "LauncherResource");
        var iniBuildPath = Path.Combine(launcherResourcePath, "INIBuild.exe");

        if (!File.Exists(iniBuildPath))
        {
            throw new FileNotFoundException($"INIBuild.exe not found for {frameworkName}: {iniBuildPath}", iniBuildPath);
        }

        _logger.LogInformation("Running INIBuild for {FrameworkName}. Path={IniBuildPath}", frameworkName, iniBuildPath);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = iniBuildPath,
                WorkingDirectory = launcherResourcePath,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start INIBuild.exe for {frameworkName}.");
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"INIBuild.exe exited with code {process.ExitCode} for {frameworkName}.");
        }
    }

    private async Task EnsureFreshRpcServerAsync(CancellationToken cancellationToken)
    {
        if (RpcClientFactory.CheckRpcServerRunning())
        {
            return;
        }

        StatusMessage = Lang.QuickSetupView_StatusPreparing;
        Process.Start(new ProcessStartInfo
        {
            FileName = AppConfig.HoYoShadeHubExecutePath,
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true,
            Arguments = $"rpc {RpcClientFactory.StartupMagic} {Environment.ProcessId}",
        });

        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500, cancellationToken);
            if (RpcClientFactory.CheckRpcServerRunning()) break;
        }

        if (!RpcClientFactory.CheckRpcServerRunning())
        {
            var logPath = Path.Combine(AppConfig.CacheFolder, "log");
            string errorMsg = $"无法启动安装服务。请确认管理员权限或检查杀毒软件是否拦截。\n日志目录：{logPath}";
            throw new Exception(errorMsg);
        }
    }

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec <= 0) return "";
        double kb = bytesPerSec / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB/s";
        double mb = kb / 1024.0;
        return $"{mb:F2} MB/s";
    }

    private static bool IsVersionV3OrAbove(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return false;
        string versionStr = tagName.TrimStart('v', 'V').Trim();
        var parts = versionStr.Split('.');
        return parts.Length > 0 && int.TryParse(parts[0], out int majorVersion) && majorVersion >= 3;
    }

    private static bool IsHiddenIncompatibleVersionTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return false;
        string normalizedTag = tagName.Trim();
        if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTag = normalizedTag[1..];
        }
        return HiddenIncompatibleVersionTags.Contains(normalizedTag);
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

    private void CheckInstallationStatus()
    {
        try
        {
            var hoYoShadeFolder = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            var openHoYoShadeFolder = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");

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

            Debug.WriteLine($"QuickSetupView: Installation status check: HoYoShade={IsHoYoShadeInstalled}, OpenHoYoShade={IsOpenHoYoShadeInstalled}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"QuickSetupView: CheckInstallationStatus error: {ex}");
        }
    }

    private async Task LoadInstalledVersionsAsync()
    {
        try
        {
            var manifest = await _versionService.LoadManifestAsync();
            InstalledHoYoShadeVersion = manifest.HoYoShade?.Version;
            InstalledOpenHoYoShadeVersion = manifest.OpenHoYoShade?.Version;

            Debug.WriteLine($"QuickSetupView: Loaded installed versions: HoYoShade={InstalledHoYoShadeVersion}, OpenHoYoShade={InstalledOpenHoYoShadeVersion}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"QuickSetupView: Failed to load installed versions: {ex.Message}");
            InstalledHoYoShadeVersion = null;
            InstalledOpenHoYoShadeVersion = null;
        }
    }
}
