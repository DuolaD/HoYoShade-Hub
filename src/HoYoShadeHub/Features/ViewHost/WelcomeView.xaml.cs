using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Features.Database;
using HoYoShadeHub.Features.Setting;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Language;
using HoYoShadeHub.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.System;


namespace HoYoShadeHub.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class WelcomeView : UserControl
{


    public WelcomeView()
    {
        this.InitializeComponent();
        DohProviders = new ObservableCollection<DownloadServerItem>();
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
    }





    public string? UserDataFolder { get; set => SetProperty(ref field, value); }


    public string? UserDataFolderErrorMessage { get; set => SetProperty(ref field, value); }


    public string? WebView2Version { get; set => SetProperty(ref field, value); }


    public bool WebpDecoderSupport { get; set => SetProperty(ref field, value); }


    public string? NetworkDelay { get; set => SetProperty(ref field, value); }


    public string? NetworkSpeed { get; set => SetProperty(ref field, value); }


    public string DohRecommendationText => GetLangString("WelcomeView_DohRecommendation");


    public string DohSwitchText => GetLangString("SettingPage_DohDnsOverHttps");


    public string DohProviderText => GetLangString("SettingPage_DohProvider");


    private bool _welcomeEnableDoh;


    public bool EnableDoh
    {
        get => _welcomeEnableDoh;
        set
        {
            if (_welcomeEnableDoh == value)
            {
                return;
            }

            _welcomeEnableDoh = value;
            AppConfig.EnableDoh = value;
            OnPropertyChanged();
            TestSpeedCommand.Execute(null);
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
            TestSpeedCommand.Execute(null);
        }
    }


    public bool CanStartHoYoShadeHub { get; set => SetProperty(ref field, value); }


    public bool IsWin11 { get; set => SetProperty(ref field, value); }


    private bool _languageInitialized;


    private static string GetLangString(string key)
    {
        return Lang.ResourceManager.GetString(key, Lang.Culture)
            ?? Lang.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }


    private async void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        InitializeLanguageSelector();
        InitializeDohProviders();
        IsWin11 = Environment.OSVersion.Version >= new Version(10, 0, 22000);
        InitializeDefaultUserDataFolder();
        await CheckWritePermissionAsync();
        CheckWebView2Support();
        await CheckWebpDecoderSupportAsync();
        TestSpeedCommand.Execute(null);
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
            if (ComboBox_Language.SelectedItem is ComboBoxItem item)
            {
                if (_languageInitialized)
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
                    // Ensure Lang uses the current culture
                    Lang.Culture = CultureInfo.CurrentUICulture;
                    this.Bindings.Update();
                    WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
                    AppConfig.SaveConfiguration();
                    // Note: Error message updates are now handled in OnLanguageChanged()
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
            Debug.WriteLine(ex);
        }
    }


    private void OnLanguageChanged()
    {
        RefreshDohProviderNames();
        OnPropertyChanged(nameof(DohRecommendationText));
        OnPropertyChanged(nameof(DohSwitchText));
        OnPropertyChanged(nameof(DohProviderText));
        // Re-check write permission to update error messages in current language
        _ = CheckWritePermissionAsync();
    }


    private void InitializeDohProviders()
    {
        _welcomeEnableDoh = AppConfig.EnableDoh;
        OnPropertyChanged(nameof(EnableDoh));

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



    private void InitializeDefaultUserDataFolder()
    {
        try
        {
            string? parentFolder = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName;
            if (AppConfig.IsAppInRemovableStorage && AppConfig.IsPortable)
            {
                UserDataFolder = parentFolder;
            }
            else if (AppConfig.IsAppInRemovableStorage)
            {
                UserDataFolder = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory)!, ".HoYoShadeLauncherData");
            }
            else if (AppConfig.IsPortable)
            {
                UserDataFolder = parentFolder;
            }
            else
            {
#if DEBUG || DEV
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HoYoShadeLauncher");
#else
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoYoShadeLauncher");
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



    private async Task CheckWritePermissionAsync()
    {
        try
        {
            UserDataFolderErrorMessage = null;
            CanStartHoYoShadeHub = false;
            if (!Directory.Exists(UserDataFolder) || !Path.IsPathFullyQualified(UserDataFolder))
            {
                UserDataFolderErrorMessage = Lang.DownloadGamePage_TheFolderDoesNotExist;
                return;
            }
            string folder = Path.GetFullPath(UserDataFolder);
            if (folder == Path.GetPathRoot(folder))
            {
                UserDataFolderErrorMessage = Lang.LauncherPage_PleaseDoNotSelectTheRootDirectoryOfADrive;
                return;
            }
            string baseDir = AppContext.BaseDirectory.TrimEnd('/', '\\');
            if (folder.StartsWith(baseDir))
            {
                UserDataFolderErrorMessage = Lang.SelectDirectoryPage_AutoDeleteAfterUpdate;
                return;
            }
            var file = Path.Combine(folder, Random.Shared.Next(int.MaxValue).ToString());
            await File.WriteAllTextAsync(file, "");
            File.Delete(file);
            CanStartHoYoShadeHub = true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // 没有写入权限
            UserDataFolderErrorMessage = Lang.SelectDirectoryPage_NoWritePermission;
            Debug.WriteLine(ex);
        }
        catch (Exception ex)
        {
            UserDataFolderErrorMessage = ex.Message;
            Debug.WriteLine(ex);
        }

    }



    [RelayCommand]
    private async Task ChangeUserDataFolderAsync()
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (Directory.Exists(folder))
            {
                UserDataFolder = folder;
                await CheckWritePermissionAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    [RelayCommand]
    private async Task TestSpeedAsync()
    {
        try
        {
            const string url = "https://speed.cloudflare.com/__down?bytes=102400";
            NetworkDelay = null;
            NetworkSpeed = null;
            using HttpClient httpClient = new HttpClient(DohService.CreateSocketsHttpHandler())
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
            var sw = Stopwatch.StartNew();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            sw.Stop();
            NetworkDelay = $"{sw.ElapsedMilliseconds}ms";
            sw.Start();
            var bytes = await response.Content.ReadAsByteArrayAsync();
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
            Debug.WriteLine(ex);
        }
    }





    private void CheckWebView2Support()
    {
        try
        {
            WebView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    private async Task CheckWebpDecoderSupportAsync()
    {
        try
        {
            // 一个webp图片
            byte[] bytes = Convert.FromBase64String("UklGRiQAAABXRUJQVlA4IBgAAAAwAQCdASoBAAEAAgA0JaQAA3AA/vv9UAA=");
            using MemoryStream ms = new MemoryStream(bytes);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.WebpDecoderId, ms.AsRandomAccessStream());
            WebpDecoderSupport = true;
        }
        catch (Exception ex)
        {
            // 0x88982F8B
            Debug.WriteLine(ex);
        }
    }



    private async void Hyperlink_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        try
        {
            if (sender.NavigateUri.Scheme is "http" or "https")
            {
                return;
            }
            await Launcher.LaunchUriAsync(sender.NavigateUri);
        }
        catch { }
    }



    [RelayCommand]
    private void Start()
    {
        try
        {
            if (!Directory.Exists(UserDataFolder))
            {
                UserDataFolderErrorMessage = Lang.DownloadGamePage_TheFolderDoesNotExist;
                CanStartHoYoShadeHub = false;
                return;
            }
            AppConfig.UserDataFolder = UserDataFolder;
            DatabaseService.SetDatabase(UserDataFolder);
            AppConfig.EnableDoh = EnableDoh;
            if (SelectedDohProvider is not null)
            {
                AppConfig.DohProvider = (DohProvider)SelectedDohProvider.ServerIndex;
            }
            AppConfig.SaveConfiguration();
            WeakReferenceMessenger.Default.Send(new NavigateToDownloadPageMessage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }





}
