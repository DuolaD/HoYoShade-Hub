using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using HoYoShadeHub.Features.Update;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;


namespace HoYoShadeHub.Features.Setting;

public sealed partial class AboutSetting : PageBase
{


    private readonly ILogger<AboutSetting> _logger = AppConfig.GetLogger<AboutSetting>();


    public AboutSetting()
    {
        this.InitializeComponent();
        DownloadServers = new ObservableCollection<string>();
        UpdateDownloadServers();
        
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => UpdateDownloadServers());
    }
    
    public ObservableCollection<string> DownloadServers { get; }

    private string _selectedDownloadServer;
    public string SelectedDownloadServer 
    { 
        get => _selectedDownloadServer; 
        set
        {
            if (SetProperty(ref _selectedDownloadServer, value))
            {
                // Save the selected server index to AppConfig
                int selectedIndex = DownloadServers.IndexOf(value);
                if (selectedIndex >= 0)
                {
                    AppConfig.LauncherUpdateDownloadServer = selectedIndex;
                }
            }
        }
    }

    private void UpdateDownloadServers()
    {
        // Get the saved index from AppConfig
        int savedIndex = AppConfig.LauncherUpdateDownloadServer;
        
        DownloadServers.Clear();
        DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_Cloudflare);
        DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_TencentCloud);
        DownloadServers.Add(Lang.HoYoShadeDownloadView_Server_AlibabaCloud);
        
        // Restore the previously selected server
        if (savedIndex >= 0 && savedIndex < DownloadServers.Count)
        {
            _selectedDownloadServer = DownloadServers[savedIndex];
        }
        else
        {
            _selectedDownloadServer = DownloadServers[0];
        }
        
        OnPropertyChanged(nameof(SelectedDownloadServer));
    }




    /// <summary>
    /// 预览版
    /// </summary>
    public bool EnablePreviewRelease
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnablePreviewRelease = value;
            }
        }
    } = AppConfig.EnablePreviewRelease;


    /// <summary>
    /// 是最新版
    /// </summary>
    public bool IsUpdated { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 更新错误文本
    /// </summary>
    public string? UpdateErrorText { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        try
        {
            IsUpdated = false;
            UpdateErrorText = null;
            
            // Get proxy URL from selected server
            int serverIndex = DownloadServers.IndexOf(SelectedDownloadServer);
            string? proxyUrl = LauncherUpdateProxyManager.GetProxyUrl(serverIndex);
            
            // Pass proxy URL to CheckUpdateAsync
            var release = await AppConfig.GetService<UpdateService>().CheckUpdateAsync(true, proxyUrl);
            if (release != null)
            {
                new UpdateWindow { NewVersion = release }.Activate();
            }
            else
            {
                IsUpdated = true;
            }
        }
        catch (Exception ex)
        {
            UpdateErrorText = ex.Message;
            _logger.LogError(ex, "Check update");
        }
    }




}
