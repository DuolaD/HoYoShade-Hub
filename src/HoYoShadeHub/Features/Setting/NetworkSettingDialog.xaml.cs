using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Language;
using HoYoShadeHub.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Setting;

[INotifyPropertyChanged]
public sealed partial class NetworkSettingDialog : ContentDialog
{
    private readonly ILogger<NetworkSettingDialog> _logger = AppConfig.GetLogger<NetworkSettingDialog>();

    public bool IsWelcomeMode { get; set; }
    public DohProvider InitialDohProvider { get; set; } = DohProvider.Cloudflare;
    public bool InitialEnableDoh { get; set; }
    public bool InitialEnableEch { get; set; }

    public bool ConfirmedEnableDoh { get; private set; }
    public bool ConfirmedEnableEch { get; private set; }
    public DohProvider ConfirmedDohProvider { get; private set; }

    private bool _originalDohEnabled;
    private DohProvider _originalDohProvider;
    private bool _originalEchEnabled;

    public NetworkSettingDialog()
    {
        this.InitializeComponent();
        DohProviders = new ObservableCollection<DownloadServerItem>();

        this.Loaded += NetworkSettingDialog_Loaded;
        this.Unloaded += NetworkSettingDialog_Unloaded;
    }

    private void NetworkSettingDialog_Loaded(object sender, RoutedEventArgs e)
    {
        _originalDohEnabled = IsWelcomeMode ? InitialEnableDoh : AppConfig.EnableDoh;
        _originalDohProvider = IsWelcomeMode ? InitialDohProvider : AppConfig.DohProvider;
        _originalEchEnabled = IsWelcomeMode ? InitialEnableEch : AppConfig.EnableEch;

        _enableDoh = _originalDohEnabled;
        _enableEch = _originalEchEnabled;
        OnPropertyChanged(nameof(EnableDoh));
        OnPropertyChanged(nameof(EnableEch));

        // Initialize DohService state
        DohService.Enabled = _originalDohEnabled;
        DohService.Provider = _originalDohProvider;
        DohService.EnableEch = _originalEchEnabled;

        InitializeDohProviders();
        RefreshSystemProxyStatus();
        _ = RefreshNetworkStatusAsync();
    }

    private void NetworkSettingDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        _networkStatusCancellationTokenSource?.Cancel();
        _networkStatusCancellationTokenSource?.Dispose();
        _networkStatusCancellationTokenSource = null;
    }

    private bool _enableDoh;
    public bool EnableDoh
    {
        get => _enableDoh;
        set
        {
            if (_enableDoh == value)
            {
                return;
            }

            _enableDoh = value;
            OnPropertyChanged();
            if (!_enableDoh)
            {
                EnableEch = false;
            }
            else
            {
                OnPropertyChanged(nameof(EnableEch));
            }
            OnNetworkSettingChanged(nameof(EnableDoh));
            _ = RefreshNetworkStatusAsync();
        }
    }

    private bool _enableEch;
    public bool EnableEch
    {
        get => _enableEch;
        set
        {
            if (_enableEch == value)
            {
                return;
            }

            _enableEch = value;
            OnPropertyChanged();
            OnNetworkSettingChanged(nameof(EnableEch));
        }
    }

    public ObservableCollection<DownloadServerItem> DohProviders { get; }

    private DownloadServerItem? _selectedDohProvider;

    public DownloadServerItem? SelectedDohProvider
    {
        get => _selectedDohProvider;
        set
        {
            if (ReferenceEquals(_selectedDohProvider, value))
            {
                return;
            }

            _selectedDohProvider = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DohDescriptionText));
            OnNetworkSettingChanged(nameof(SelectedDohProvider));
            _ = RefreshNetworkStatusAsync();
        }
    }

    private void OnNetworkSettingChanged(string propertyName)
    {
        // Update DohService in memory so speed tests / ping work in both modes
        if (propertyName == nameof(EnableDoh))
        {
            DohService.Enabled = EnableDoh;
            WeakReferenceMessenger.Default.Send(new EchSettingChangedMessage());
        }
        else if (propertyName == nameof(EnableEch))
        {
            DohService.EnableEch = EnableEch;
            WeakReferenceMessenger.Default.Send(new EchSettingChangedMessage());
        }
        else if (propertyName == nameof(SelectedDohProvider) && SelectedDohProvider is not null)
        {
            DohService.Provider = (DohProvider)SelectedDohProvider.ServerIndex;
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ConfirmedEnableDoh = EnableDoh;
        ConfirmedEnableEch = EnableEch;
        ConfirmedDohProvider = SelectedDohProvider is not null
            ? (DohProvider)SelectedDohProvider.ServerIndex
            : DohProvider.Cloudflare;

        if (!IsWelcomeMode)
        {
            AppConfig.EnableDoh = ConfirmedEnableDoh;
            AppConfig.DohProvider = ConfirmedDohProvider;
            AppConfig.EnableEch = ConfirmedEnableEch;
            AppConfig.SaveConfiguration();
        }
    }

    private void ContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (args.Result != ContentDialogResult.Primary)
        {
            // Revert DohService state to original values
            DohService.Enabled = _originalDohEnabled;
            DohService.Provider = _originalDohProvider;
            DohService.EnableEch = _originalEchEnabled;
            WeakReferenceMessenger.Default.Send(new EchSettingChangedMessage());
        }
    }

    public string DohSwitchText => GetLangString("SettingPage_DohDnsOverHttps");

    public string EchSwitchText => "ECH (Encrypted Client Hello)";

    public string DohProviderText => GetLangString("SettingPage_DohProvider");

    public string DohDescriptionText => string.Format(CultureInfo.CurrentCulture, GetLangString("SettingPage_DohDescriptionMostDnsRequestsFormat"), SelectedDohProvider?.Name ?? "Cloudflare");

    public string NetworkDescriptionText => GetLangString("SettingPage_NetworkDescription");

    public string DohDisabledDescriptionText => GetLangString("SettingPage_DohDisabledDescription");

    public string EchDescriptionText => GetLangString("SettingPage_EchDescription");

    public string EchRequirementNoticeText => GetLangString("SettingPage_EchRequirementNotice");

    public string? NetworkDelay { get; set => SetProperty(ref field, value); }

    public string? NetworkSpeed { get; set => SetProperty(ref field, value); }

    public bool IsRefreshingNetworkStatus { get; set => SetProperty(ref field, value); }

    public string SystemProxyTitleText => GetLangString("SettingPage_SystemProxyStatus");

    public string? SystemProxyStatusText { get; set => SetProperty(ref field, value); }

    private CancellationTokenSource? _networkStatusCancellationTokenSource;

    private static string GetLangString(string key)
    {
        return Lang.ResourceManager.GetString(key, Lang.Culture)
            ?? Lang.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }

    private void RefreshSystemProxyStatus()
    {
        try
        {
            Uri targetUri = new Uri("https://hoyosha.de/");
            Uri? proxy = HttpClient.DefaultProxy.GetProxy(targetUri);
            if (proxy is not null && proxy != targetUri)
            {
                SystemProxyStatusText = string.Format(CultureInfo.CurrentCulture, GetLangString("SettingPage_SystemProxyEnabled"), proxy.ToString());
            }
            else
            {
                SystemProxyStatusText = GetLangString("SettingPage_SystemProxyDisabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check system proxy.");
            SystemProxyStatusText = GetLangString("SettingPage_SystemProxyDisabled");
        }
        OnPropertyChanged(nameof(SystemProxyTitleText));
    }

    private void InitializeDohProviders()
    {
        int savedProvider = (int)_originalDohProvider;

        DohProviders.Clear();
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.Cloudflare), ServerIndex = (int)DohProvider.Cloudflare });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.Google), ServerIndex = (int)DohProvider.Google });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.CleanBrowsing), ServerIndex = (int)DohProvider.CleanBrowsing });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.OpenDns), ServerIndex = (int)DohProvider.OpenDns });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.Quad9), ServerIndex = (int)DohProvider.Quad9 });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.AdGuard), ServerIndex = (int)DohProvider.AdGuard });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.Aliyun), ServerIndex = (int)DohProvider.Aliyun });
        DohProviders.Add(new DownloadServerItem { Name = GetDohProviderName(DohProvider.Tencent), ServerIndex = (int)DohProvider.Tencent });

        SelectedDohProvider = DohProviders.FirstOrDefault(x => x.ServerIndex == savedProvider) ?? DohProviders[0];
        _ = UpdateDohLatenciesAsync();
    }

    private static string GetDohProviderName(DohProvider provider)
    {
        return provider switch
        {
            DohProvider.Aliyun => Lang.HoYoShadeDownloadView_Server_AlibabaCloud,
            DohProvider.Tencent => Lang.HoYoShadeDownloadView_Server_TencentCloud,
            DohProvider.OpenDns => "OpenDNS",
            _ => provider.ToString(),
        };
    }

    private async Task UpdateDohLatenciesAsync()
    {
        try
        {
            foreach (var provider in DohProviders)
            {
                provider.LatencyText = "Tcping...";
                provider.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }

            var tasks = DohProviders.Select(async provider =>
            {
                try
                {
                    long latency = await DohService.TcpPingAsync((DohProvider)provider.ServerIndex);
                    if (latency >= 0)
                    {
                        provider.LatencyText = $"{latency}ms";
                        if (latency <= 600)
                        {
                            provider.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
                        }
                        else
                        {
                            provider.LatencyColor = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC5, 0x7F, 0x0A));
                        }
                    }
                    else
                    {
                        provider.LatencyText = "Timeout";
                        provider.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TcpPing task failed.");
                }
            });
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateDohLatenciesAsync failed.");
        }
    }

    private async Task RefreshNetworkStatusAsync()
    {
        _networkStatusCancellationTokenSource?.Cancel();
        _networkStatusCancellationTokenSource?.Dispose();
        var cancellationTokenSource = new CancellationTokenSource();
        _networkStatusCancellationTokenSource = cancellationTokenSource;

        try
        {
            const string url = "https://speed.cloudflare.com/__down?bytes=102400";
            NetworkDelay = null;
            NetworkSpeed = null;
            IsRefreshingNetworkStatus = true;
            using var httpClient = new HttpClient(DohService.CreateSocketsHttpHandler())
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
            var sw = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationTokenSource.Token);
            sw.Stop();
            NetworkDelay = $"{sw.ElapsedMilliseconds}ms";
            sw.Start();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationTokenSource.Token);
            sw.Stop();
            double speed = bytes.Length / 1024.0 / sw.Elapsed.TotalSeconds;
            if (speed < 1024)
            {
                NetworkSpeed = $"{speed:0.00}KB/s";
            }
            else
            {
                NetworkSpeed = $"{speed / 1024:0.00}MB/s";
            }
        }
        catch (Exception ex)
        {
            NetworkSpeed = Lang.WelcomeView_NetworkErrorYouCanContinueUsingHoYoShadeHubButYouWonTReceiveFutureUpdates;
            _logger.LogWarning(ex, "Refresh network status failed.");
        }
        finally
        {
            if (ReferenceEquals(_networkStatusCancellationTokenSource, cancellationTokenSource))
            {
                IsRefreshingNetworkStatus = false;
            }
        }
    }

    private async void Button_RefreshNetworkStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshNetworkStatusAsync();
    }
}
