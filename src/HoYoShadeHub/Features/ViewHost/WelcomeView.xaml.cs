using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
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
    private bool _welcomeEnableDoh;
    private DohProvider _welcomeDohProvider = DohProvider.Cloudflare;
    private bool _welcomeEnableEch;


    public WelcomeView()
    {
        this.InitializeComponent();
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => OnLanguageChanged());
    }





    public string? UserDataFolder { get; set => SetProperty(ref field, value); }


    public string? UserDataFolderErrorMessage { get; set => SetProperty(ref field, value); }


    public string? WebView2Version { get; set => SetProperty(ref field, value); }


    public bool WebpDecoderSupport { get; set => SetProperty(ref field, value); }


    public string? NetworkDelay { get; set => SetProperty(ref field, value); }


    public string? NetworkSpeed { get; set => SetProperty(ref field, value); }


    public string SystemProxyTitleText => GetLangString("SettingPage_SystemProxyStatus");


    public string? SystemProxyStatusText { get; set => SetProperty(ref field, value); }


    public string DohRecommendationText => GetLangString("WelcomeView_DohRecommendation");





    public bool CanStartHoYoShadeHub { get; set => SetProperty(ref field, value); }


    public bool IsWin11 { get; set => SetProperty(ref field, value); }


    private bool _languageInitialized;


    private static string GetLangString(string key)
    {
        return Lang.ResourceManager.GetString(key, Lang.Culture)
            ?? Lang.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
            ?? key;
    }


    public Thickness GetMargin(double top)
    {
        bool isEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);
        double factor = isEnglish ? 0.8 : 1.0;
        return new Thickness(0, top * factor, 0, 0);
    }


    public Thickness GetScrollViewerMargin()
    {
        bool isEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase);
        return isEnglish ? new Thickness(48, 36, 48, 28) : new Thickness(48, 44, 48, 28);
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
            Debug.WriteLine(ex);
            SystemProxyStatusText = GetLangString("SettingPage_SystemProxyDisabled");
        }
        OnPropertyChanged(nameof(SystemProxyTitleText));
    }


    private async void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        InitializeLanguageSelector();
        IsWin11 = Environment.OSVersion.Version >= new Version(10, 0, 22000);
        InitializeDefaultUserDataFolder();
        await CheckWritePermissionAsync();
        CheckWebView2Support();
        await CheckWebpDecoderSupportAsync();

        _welcomeEnableDoh = AppConfig.EnableDoh;
        _welcomeDohProvider = AppConfig.DohProvider;
        _welcomeEnableEch = AppConfig.EnableEch;

        RefreshSystemProxyStatus();
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
        OnPropertyChanged(nameof(DohRecommendationText));
        // Re-check write permission to update error messages in current language
        _ = CheckWritePermissionAsync();
        RefreshSystemProxyStatus();
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
            RefreshSystemProxyStatus();
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

            // Persist the cached welcome network settings to the database now that UserDataFolder is set
            AppConfig.EnableDoh = _welcomeEnableDoh;
            AppConfig.DohProvider = _welcomeDohProvider;
            AppConfig.EnableEch = _welcomeEnableEch;

            AppConfig.SaveConfiguration();
            WeakReferenceMessenger.Default.Send(new NavigateToDownloadPageMessage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private async void Hyperlink_NetworkSettings_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        var dialog = new NetworkSettingDialog
        {
            XamlRoot = this.XamlRoot,
            IsWelcomeMode = true,
            InitialEnableDoh = _welcomeEnableDoh,
            InitialDohProvider = _welcomeDohProvider,
            InitialEnableEch = _welcomeEnableEch
        };
        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            _welcomeEnableDoh = dialog.ConfirmedEnableDoh;
            _welcomeDohProvider = dialog.ConfirmedDohProvider;
            _welcomeEnableEch = dialog.ConfirmedEnableEch;

            TestSpeedCommand.Execute(null);
        }
    }





}
