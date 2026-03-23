using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using HoYoShadeHub.Features.Update;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.Models;
using System;
using System.Linq;
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
        DownloadServers = new ObservableCollection<DownloadServerItem>();
        UpdateDownloadServers();
        
        // Register for language change messages
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (r, m) => UpdateDownloadServers());
    }
    
    public ObservableCollection<DownloadServerItem> DownloadServers { get; }

    private DownloadServerItem _selectedDownloadServer;
    public DownloadServerItem SelectedDownloadServer 
    { 
        get => _selectedDownloadServer; 
        set
        {
            if (SetProperty(ref _selectedDownloadServer, value))
            {
                // Save the selected server index to AppConfig
                if (value != null)
                {
                    AppConfig.LauncherUpdateDownloadServer = value.ServerIndex;
                }
            }
        }
    }

    private void UpdateDownloadServers()
    {
        // Get the saved index from AppConfig
        int savedIndex = AppConfig.LauncherUpdateDownloadServer;
        
        DownloadServers.Clear();
        // Add Auto Select option
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_AutoSelect, ServerIndex = -1 });
        // Skip GitHub direct for launcher updates
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_Cloudflare, ServerIndex = 1 });
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_TencentCloud, ServerIndex = 2 });
        DownloadServers.Add(new DownloadServerItem { Name = Lang.HoYoShadeDownloadView_Server_AlibabaCloud, ServerIndex = 3 });
        
        var toSelect = DownloadServers.FirstOrDefault(x => x.ServerIndex == savedIndex);
        _selectedDownloadServer = toSelect ?? DownloadServers[0];
        
        OnPropertyChanged(nameof(SelectedDownloadServer));
        
        _ = UpdateLatenciesAsync();
    }

    private async Task UpdateLatenciesAsync()
    {
        var httpClient = AppConfig.GetService<System.Net.Http.HttpClient>();
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
            int serverIndex = SelectedDownloadServer?.ServerIndex ?? -1;
            string? proxyUrl = LauncherUpdateProxyManager.GetProxyUrl(serverIndex);
            
            // Pass proxy URL to CheckUpdateAsync (we only check updates, Auto Select fallback for checking can just use the first available or we can modify CheckUpdateAsync to take serverIndex and do fallback)
            // Wait, CheckUpdateAsync only checks metadata. We can just use the proxyUrl.
            // If AutoSelect (-1), we can just try Cloudflare (1) for metadata check.
            if (serverIndex == -1)
            {
                proxyUrl = LauncherUpdateProxyManager.GetProxyUrl(1); // Default to Cloudflare for metadata
            }
            
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
