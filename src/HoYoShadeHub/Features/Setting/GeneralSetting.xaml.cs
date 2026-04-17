using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Features.ViewHost;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Language;
using HoYoShadeHub.Models;
using System.Diagnostics;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class GeneralSetting : PageBase
{

    private readonly ILogger<GeneralSetting> _logger = AppConfig.GetLogger<GeneralSetting>();


    public GeneralSetting()
    {
        this.InitializeComponent();
        DohProviders = new ObservableCollection<DownloadServerItem>();
    }



    protected override void OnLoaded()
    {
        InitializeLanguageSelector();
        InitializeCloseWindowOption();
        InitializeDohProviders();
    }


    protected override void OnUnloaded()
    {
        _networkStatusCancellationTokenSource?.Cancel();
        _networkStatusCancellationTokenSource?.Dispose();
        _networkStatusCancellationTokenSource = null;
    }



    public bool DefaultDisableVideoBackgroundPlayback
    {
        get => AppConfig.DefaultDisableVideoBackgroundPlayback;
        set
        {
            AppConfig.DefaultDisableVideoBackgroundPlayback = value;
            OnPropertyChanged();
        }
    }


    public bool EnableDoh
    {
        get => AppConfig.EnableDoh;
        set
        {
            if (AppConfig.EnableDoh == value)
            {
                return;
            }

            AppConfig.EnableDoh = value;
            OnPropertyChanged();
            _ = RefreshNetworkStatusAsync();
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
            if (value != null)
            {
                var provider = (DohProvider)value.ServerIndex;
                if (AppConfig.DohProvider != provider)
                {
                    AppConfig.DohProvider = provider;
                }
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(DohDescriptionText));
            OnPropertyChanged(nameof(DohDescriptionText2));
            _ = RefreshNetworkStatusAsync();
        }
    }


    public string DohSwitchText => GetLangString("SettingPage_DohDnsOverHttps");


    public string DohProviderText => GetLangString("SettingPage_DohProvider");


    public string DohIntroText => GetLangString("SettingPage_DohIntro");


    public string DohDescriptionText => string.Format(CultureInfo.CurrentCulture, GetLangString("SettingPage_DohDescriptionMostDnsRequestsFormat"), SelectedDohProvider?.Name ?? "Cloudflare");


    public string DohDescriptionText2 => string.Format(CultureInfo.CurrentCulture, GetLangString("SettingPage_DohDescriptionOtherAppsFormat"), SelectedDohProvider?.Name ?? "Cloudflare");


    public string DohVpnProxyNoticeText => GetLangString("SettingPage_DohVpnProxyNotice");


    public string DohTroubleshootingText => GetLangString("SettingPage_DohTroubleshooting");


    public string? NetworkDelay { get; set => SetProperty(ref field, value); }


    public string? NetworkSpeed { get; set => SetProperty(ref field, value); }


    public bool IsRefreshingNetworkStatus { get; set => SetProperty(ref field, value); }


    private CancellationTokenSource? _networkStatusCancellationTokenSource;


    private static string GetLangString(string key)
    {
        return Lang.ResourceManager.GetString(key, Lang.Culture)
            ?? Lang.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }


    private void InitializeDohProviders()
    {
        int savedProvider = (int)AppConfig.DohProvider;

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


    private void RefreshDohProviderNames()
    {
        foreach (var provider in DohProviders)
        {
            provider.Name = GetDohProviderName((DohProvider)provider.ServerIndex);
        }

        OnPropertyChanged(nameof(DohDescriptionText));
        OnPropertyChanged(nameof(DohDescriptionText2));
    }


    private async Task UpdateDohLatenciesAsync()
    {
        foreach (var provider in DohProviders)
        {
            provider.LatencyText = "Tcping...";
            provider.LatencyColor = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        var tasks = DohProviders.Select(async provider =>
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
        });
        await Task.WhenAll(tasks);
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
            using HttpClient httpClient = new HttpClient(DohService.CreateSocketsHttpHandler())
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
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
        catch (OperationCanceledException)
        {
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




    #region 语言



    private bool _languageInitialized;


    /// <summary>
    /// 语言
    /// </summary>
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



    /// <summary>
    /// 语言切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ComboBox_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ComboBox_Language.SelectedItem is ComboBoxItem item)
            {
                if (_languageInitialized)
                {
                    var lang = item.Tag as string;
                    _logger.LogInformation("Language change to {lang}", lang);
                    AppConfig.Language = lang;
                    if (string.IsNullOrWhiteSpace(lang))
                    {
                        CultureInfo.CurrentUICulture = AppConfig.SystemCulture;
                    }
                    else
                    {
                        CultureInfo.CurrentUICulture = new CultureInfo(lang);
                    }
                    // Ensure Lang uses the current culture
                    Lang.Culture = CultureInfo.CurrentUICulture;
                    RefreshDohProviderNames();
                    this.Bindings.Update();
                    WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
                    AppConfig.SaveConfiguration();
                }
            }
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.CurrentUICulture = AppConfig.SystemCulture;
            Lang.Culture = CultureInfo.CurrentUICulture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change Language");
        }
    }



    #endregion



    #region 关闭窗口选项



    private bool _closeWindowOptionInitialized;



    /// <summary>
    /// 初始化关闭窗口选项
    /// </summary>
    private void InitializeCloseWindowOption()
    {
        try
        {
            var option = AppConfig.CloseWindowOption;
            if (option is MainWindowCloseOption.Hide)
            {
                RadioButton_CloseWindowOption_Hide.IsChecked = true;
            }
            else if (option is MainWindowCloseOption.Exit)
            {
                RadioButton_CloseWindowOption_Exit.IsChecked = true;
            }
            _closeWindowOptionInitialized = true;
        }
        catch { }
    }



    /// <summary>
    /// 关闭窗口选项切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void RadioButton_CloseWindowOption_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_closeWindowOptionInitialized)
            {
                if (sender is FrameworkElement fe)
                {
                    AppConfig.CloseWindowOption = fe.Tag switch
                    {
                        MainWindowCloseOption option => option,
                        _ => 0,
                    };
                }
            }
        }
        catch { }
    }



    #endregion



    #region 游戏账号切换



    #endregion



    #region 系统视觉效果



    /// <summary>
    /// 透明/动画效果
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void Hyperlink_VisualEffects_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-settings:easeofaccess-visualeffects"));
    }


    private async void Button_RefreshNetworkStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshNetworkStatusAsync();
    }



    #endregion



}
