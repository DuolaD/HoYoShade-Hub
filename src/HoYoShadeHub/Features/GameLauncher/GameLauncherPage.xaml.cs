using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Features.Background;
using HoYoShadeHub.Features.HoYoPlay;
using HoYoShadeHub.Features.Overlay;
using HoYoShadeHub.Features.ViewHost;
using HoYoShadeHub.Frameworks;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.RPC.GameInstall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;


namespace HoYoShadeHub.Features.GameLauncher;

public sealed partial class GameLauncherPage : PageBase
{


    private readonly ILogger<GameLauncherPage> _logger = AppConfig.GetLogger<GameLauncherPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();

    private readonly BackgroundService _backgroundService = AppConfig.GetService<BackgroundService>();

    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _dispatchTimer;


    public GameLauncherPage()
    {
        this.InitializeComponent();
        _dispatchTimer = DispatcherQueue.CreateTimer();
        _dispatchTimer.Interval = TimeSpan.FromMilliseconds(100);
    }



    protected override void OnLoaded()
    {
        InitializeGameFeature();
        CheckGameVersion();
        CheckShadeInstallation();
        _ = InitializeGameServerAsync();
        _ = InitializeBackgameImageSwitcherAsync();
        WeakReferenceMessenger.Default.Register<GameInstallPathChangedMessage>(this, OnGameInstallPathChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<RemovableStorageDeviceChangedMessage>(this, OnRemovableStorageDeviceChanged);
        WeakReferenceMessenger.Default.Register<BackgroundChangedMessage>(this, OnBackgroundChanged);
    }



    protected override void OnUnloaded()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _dispatchTimer.Stop();
        BackgroundImages = null!;
    }




    private void InitializeGameFeature()
    {
        GameFeatureConfig feature = GameFeatureConfig.FromGameId(CurrentGameId);
    }


    private void CheckShadeInstallation()
    {
        try
        {
            // Check HoYoShade installation
            string hoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
            IsHoYoShadeInstalled = Directory.Exists(hoYoShadePath) && 
                                   Directory.GetFiles(hoYoShadePath, "*.dll").Length > 0;

            // Check OpenHoYoShade installation
            string openHoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
            IsOpenHoYoShadeInstalled = Directory.Exists(openHoYoShadePath) && 
                                       Directory.GetFiles(openHoYoShadePath, "*.dll").Length > 0;

            // Uncheck options if shaders are not installed
            if (!IsHoYoShadeInstalled && UseHoYoShade)
            {
                UseHoYoShade = false;
            }
            if (!IsOpenHoYoShadeInstalled && UseOpenHoYoShade)
            {
                UseOpenHoYoShade = false;
            }

            // Check Blender plugin configurations
            CheckBlenderPluginConfigurations();

            _logger.LogInformation("HoYoShade installed: {HoYoShade}, OpenHoYoShade installed: {OpenHoYoShade}", 
                IsHoYoShadeInstalled, IsOpenHoYoShadeInstalled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check shade installation");
            IsHoYoShadeInstalled = false;
            IsOpenHoYoShadeInstalled = false;
        }
    }

    private void CheckBlenderPluginConfigurations()
    {
        try
        {
            // Check Genshin Impact Blender Plugin
            bool isGenshinGame = CurrentGameBiz.ToGame().Value == GameBiz.hk4e;
            IsGenshinBlenderPluginVisible = isGenshinGame ? Visibility.Visible : Visibility.Collapsed;
            
            if (isGenshinGame)
            {
                string? genshinPluginPath = AppConfig.GenshinBlenderPluginPath;
                IsGenshinBlenderPluginConfigured = !string.IsNullOrWhiteSpace(genshinPluginPath) && 
                                                   Directory.Exists(genshinPluginPath) &&
                                                   File.Exists(Path.Combine(genshinPluginPath, "client.exe"));
                
                if (!IsGenshinBlenderPluginConfigured && LaunchGenshinBlenderPlugin)
                {
                    LaunchGenshinBlenderPlugin = false;
                }
            }

            // Check ZZZ Blender Plugin
            bool isZZZGame = CurrentGameBiz.ToGame().Value == GameBiz.nap;
            IsZZZBlenderPluginVisible = isZZZGame ? Visibility.Visible : Visibility.Collapsed;
            
            if (isZZZGame)
            {
                string? zzzPluginPath = AppConfig.ZZZBlenderPluginPath;
                IsZZZBlenderPluginConfigured = !string.IsNullOrWhiteSpace(zzzPluginPath) && 
                                               Directory.Exists(zzzPluginPath) &&
                                               File.Exists(Path.Combine(zzzPluginPath, "loader.exe"));
                
                if (!IsZZZBlenderPluginConfigured && LaunchZZZBlenderPlugin)
                {
                    LaunchZZZBlenderPlugin = false;
                }
            }

            _logger.LogInformation("Blender plugins - Genshin configured: {Genshin}, ZZZ configured: {ZZZ}", 
                IsGenshinBlenderPluginConfigured, IsZZZBlenderPluginConfigured);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check blender plugin configurations");
        }
    }


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledLocateGameEnabled))]
    public partial GameState GameState { get; set; }


    private bool _enableGameLaunch = true;
    public bool EnableGameLaunch
    {
        get => _enableGameLaunch;
        set
        {
            if (SetProperty(ref _enableGameLaunch, value))
            {
                OnPropertyChanged(nameof(ShouldEnableStartButton));
            }
        }
    }

    /// <summary>
    /// 启动按钮是否应该可用 - 只要勾选了任何一个启动选项就可用
    /// </summary>
    public bool ShouldEnableStartButton => EnableGameLaunch || LaunchGenshinBlenderPlugin || LaunchZZZBlenderPlugin;

    private bool _useHoYoShade;
    public bool UseHoYoShade
    {
        get => _useHoYoShade;
        set
        {
            if (SetProperty(ref _useHoYoShade, value) && value)
            {
                // Uncheck OpenHoYoShade if HoYoShade is checked
                UseOpenHoYoShade = false;
                _logger.LogInformation("UseHoYoShade enabled, UseOpenHoYoShade disabled");
            }
        }
    }

    private bool _useOpenHoYoShade;
    public bool UseOpenHoYoShade
    {
        get => _useOpenHoYoShade;
        set
        {
            if (SetProperty(ref _useOpenHoYoShade, value) && value)
            {
                // Uncheck HoYoShade if OpenHoYoShade is checked
                UseHoYoShade = false;
                _logger.LogInformation("UseOpenHoYoShade enabled, UseHoYoShade disabled");
            }
        }
    }

    private bool _isHoYoShadeInstalled;
    public bool IsHoYoShadeInstalled
    {
        get => _isHoYoShadeInstalled;
        set => SetProperty(ref _isHoYoShadeInstalled, value);
    }

    private bool _isOpenHoYoShadeInstalled;
    public bool IsOpenHoYoShadeInstalled
    {
        get => _isOpenHoYoShadeInstalled;
        set => SetProperty(ref _isOpenHoYoShadeInstalled, value);
    }

    // Blender plugin properties
    private bool _launchGenshinBlenderPlugin;
    public bool LaunchGenshinBlenderPlugin
    {
        get => _launchGenshinBlenderPlugin;
        set
        {
            if (SetProperty(ref _launchGenshinBlenderPlugin, value))
            {
                if (value)
                {
                    // Disable game launch when Blender plugin is selected
                    EnableGameLaunch = false;
                    UpdateGameLaunchCheckboxState();
                    _logger.LogInformation("LaunchGenshinBlenderPlugin enabled, EnableGameLaunch disabled");
                }
                else
                {
                    UpdateGameLaunchCheckboxState();
                }
                OnPropertyChanged(nameof(ShouldEnableStartButton));
            }
        }
    }

    private bool _launchZZZBlenderPlugin;
    public bool LaunchZZZBlenderPlugin
    {
        get => _launchZZZBlenderPlugin;
        set
        {
            if (SetProperty(ref _launchZZZBlenderPlugin, value))
            {
                if (value)
                {
                    // Disable game launch when Blender plugin is selected
                    EnableGameLaunch = false;
                    UpdateGameLaunchCheckboxState();
                    _logger.LogInformation("LaunchZZZBlenderPlugin enabled, EnableGameLaunch disabled");
                }
                else
                {
                    UpdateGameLaunchCheckboxState();
                }
                OnPropertyChanged(nameof(ShouldEnableStartButton));
            }
        }
    }

    private bool _isGameLaunchCheckboxEnabled = true;
    public bool IsGameLaunchCheckboxEnabled
    {
        get => _isGameLaunchCheckboxEnabled;
        set => SetProperty(ref _isGameLaunchCheckboxEnabled, value);
    }

    private void UpdateGameLaunchCheckboxState()
    {
        // Disable game launch checkbox if any Blender plugin is selected
        IsGameLaunchCheckboxEnabled = !LaunchGenshinBlenderPlugin && !LaunchZZZBlenderPlugin;
        
        // If no Blender plugin is selected and game launch is unchecked, re-enable it
        if (IsGameLaunchCheckboxEnabled && !EnableGameLaunch)
        {
            EnableGameLaunch = true;
        }
    }

    private bool _isGenshinBlenderPluginConfigured;
    public bool IsGenshinBlenderPluginConfigured
    {
        get => _isGenshinBlenderPluginConfigured;
        set => SetProperty(ref _isGenshinBlenderPluginConfigured, value);
    }

    private bool _isZZZBlenderPluginConfigured;
    public bool IsZZZBlenderPluginConfigured
    {
        get => _isZZZBlenderPluginConfigured;
        set => SetProperty(ref _isZZZBlenderPluginConfigured, value);
    }

    private Visibility _isGenshinBlenderPluginVisible = Visibility.Collapsed;
    public Visibility IsGenshinBlenderPluginVisible
    {
        get => _isGenshinBlenderPluginVisible;
        set => SetProperty(ref _isGenshinBlenderPluginVisible, value);
    }

    private Visibility _isZZZBlenderPluginVisible = Visibility.Collapsed;
    public Visibility IsZZZBlenderPluginVisible
    {
        get => _isZZZBlenderPluginVisible;
        set => SetProperty(ref _isZZZBlenderPluginVisible, value);
    }

    private void ComboBox_LaunchMode_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        // Removed - no longer using ComboBox
    }



    [RelayCommand]
    private async Task ClickStartGameButtonAsync()
    {
        await Task.Delay(1);
        switch (GameState)
        {
            case GameState.None:
                break;
            case GameState.StartGame:
                await StartGameAsync();
                break;
            case GameState.GameIsRunning:
            case GameState.InstallGame:
                await InstallGameAsync();
                break;
            case GameState.Installing:
            case GameState.UpdateGame:
                await UpdateGameAsync();
                break;
            case GameState.UpdatePlugin:
            case GameState.ResumeDownload:
                await ResumeDownloadAsync();
                break;
            case GameState.ComingSoon:
                break;
            default:
                break;
        }
    }




    #region Game Server


    private List<GameServerConfig>? _gameServers;
    public List<GameServerConfig>? GameServers 
    { 
        get => _gameServers;
        set => SetProperty(ref _gameServers, value);
    }

    [ObservableProperty]
    public partial GameServerConfig? SelectedGameServer { get; set; }
    partial void OnSelectedGameServerChanged(GameServerConfig? oldValue, GameServerConfig? newValue)
    {
        if (oldValue is not null && newValue is not null)
        {
            AppConfig.LastGameIdOfBH3Global = newValue.GameId;
            WeakReferenceMessenger.Default.Send(new BH3GlobalGameServerChangedMessage(newValue.GameId));
        }
    }


    /// <summary>
    /// 初始化区服选项，仅崩坏三国际服使用
    /// </summary>
    /// <returns></returns>
    private async Task InitializeGameServerAsync()
    {
        try
        {
            GameInfo? gameInfo;
            if (CurrentGameBiz == GameBiz.bh3_global)
            {
                gameInfo = await _hoYoPlayService.GetGameInfoAsync(GameId.FromGameBiz(GameBiz.bh3_global)!);
            }
            else
            {
                gameInfo = await _hoYoPlayService.GetGameInfoAsync(CurrentGameId);
            }
            if (gameInfo?.GameServerConfigs?.Count > 0)
            {
                GameServers = gameInfo.GameServerConfigs;
                if (GameServers.FirstOrDefault(x => x.GameId == CurrentGameId.Id) is GameServerConfig config)
                {
                    SelectedGameServer = config;
                }
                else
                {
                    SelectedGameServer = GameServers.FirstOrDefault();
                    if (SelectedGameServer is not null)
                    {
                        CurrentGameId.Id = SelectedGameServer.GameId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize game server");
        }
    }



    #endregion




    #region Game Version


    private string? _gameInstallPath;
    public string? GameInstallPath 
    { 
        get => _gameInstallPath;
        set => SetProperty(ref _gameInstallPath, value);
    }

    /// <summary>
    /// 可移动存储设备提示
    /// </summary>
    private bool _isInstallPathRemovableTipEnabled;
    public bool IsInstallPathRemovableTipEnabled 
    { 
        get => _isInstallPathRemovableTipEnabled;
        set
        {
            if (SetProperty(ref _isInstallPathRemovableTipEnabled, value))
            {
                OnPropertyChanged(nameof(InstalledLocateGameEnabled));
            }
        }
    }

    /// <summary>
    /// 已安装？定位游戏
    /// </summary>
    public bool InstalledLocateGameEnabled => GameState is GameState.InstallGame && !IsInstallPathRemovableTipEnabled;

    /// <summary>
    /// 预下载按钮是否可用
    /// </summary>
    private bool _isPredownloadButtonEnabled;
    public bool IsPredownloadButtonEnabled 
    { 
        get => _isPredownloadButtonEnabled;
        set => SetProperty(ref _isPredownloadButtonEnabled, value);
    }

    /// <summary>
    /// 预下载是否完成
    /// </summary>
    private bool _isPredownloadFinished;
    public bool IsPredownloadFinished 
    { 
        get => _isPredownloadFinished;
        set => SetProperty(ref _isPredownloadFinished, value);
    }


    private Version? localGameVersion;


    private bool isGameExeExists;



    private async void CheckGameVersion()
    {
        try
        {
            GameInstallPath = GameLauncherService.GetGameInstallPath(CurrentGameId, out bool storageRemoved);
            IsInstallPathRemovableTipEnabled = storageRemoved;
            if (GameInstallPath is null || storageRemoved)
            {
                GameState = GameState.InstallGame;
                return;
            }
            isGameExeExists = await _gameLauncherService.IsGameExeExistsAsync(CurrentGameId);
            localGameVersion = await _gameLauncherService.GetLocalGameVersionAsync(CurrentGameId);
            if (isGameExeExists && localGameVersion != null)
            {
                GameState = GameState.StartGame;
            }
            else
            {
                GameState = GameState.InstallGame;
                return;
            }
            await CheckGameRunningAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check game version");
        }
    }





    /// <summary>
    /// 定位游戏路径
    /// </summary>
    /// <returns></returns>
    private async Task LocateGameAsync()
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                if (DriveHelper.GetDriveType(folder) is DriveType.Network && !new Uri(folder).IsUnc)
                {
                    InAppToast.MainWindow?.Warning(null, Lang.InstallGameDialog_MappedNetworkDrivesAreNotSupportedPleaseUseANetworkSharePathStartingWithDoubleBackslashes, 0);
                }
                else
                {
                    GameLauncherService.ChangeGameInstallPath(CurrentGameId, folder);
                    CheckGameVersion();
                    WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Locate game");
        }
    }



    /// <summary>
    /// 定位游戏路径
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void Hyperlink_LocateGame_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        await LocateGameAsync();
    }




    private void OnGameInstallPathChanged(object _, GameInstallPathChangedMessage message)
    {
        CheckGameVersion();
    }




    private void OnMainWindowStateChanged(object _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate && (message.ElapsedOver(TimeSpan.FromMinutes(10)) || message.IsCrossingHour))
            {
                CheckGameVersion();
            }
        }
        catch { }
    }




    private void OnRemovableStorageDeviceChanged(object _, RemovableStorageDeviceChangedMessage message)
    {
        try
        {
            CheckGameVersion();
        }
        catch { }
    }




    #endregion




    #region Start Game




    private Timer processTimer;


    [ObservableProperty]
    private partial Process? GameProcess { get; set; }
    partial void OnGameProcessChanged(Process? oldValue, Process? newValue)
    {
        processTimer?.Stop();
        if (processTimer is null)
        {
            processTimer = new(1000);
            processTimer.Elapsed += (_, _) => CheckGameExited();
        }
        if (newValue != null)
        {
            processTimer?.Start();
            RunningGameInfo = $"{newValue.ProcessName}.exe ({newValue.Id})";
            RunningGameService.AddRuninngGame(CurrentGameBiz, newValue);
        }
        else
        {
            RunningGameInfo = null;
            _logger.LogInformation("Game process exited");
        }
    }



    private string? _runningGameInfo;
    public string? RunningGameInfo 
    { 
        get => _runningGameInfo;
        set => SetProperty(ref _runningGameInfo, value);
    }



    private async Task<bool> CheckGameRunningAsync()
    {
        try
        {
            GameProcess = await _gameLauncherService.GetGameProcessAsync(CurrentGameId);
            if (GameProcess != null)
            {
                GameState = GameState.GameIsRunning;
                _logger.LogInformation("Game is running ({name}, {pid})", GameProcess.ProcessName, GameProcess.Id);
                return true;
            }
        }
        catch { }
        return false;
    }




    private void CheckGameExited()
    {
        try
        {
            if (GameProcess != null)
            {
                if (GameProcess.HasExited)
                {
                    DispatcherQueue.TryEnqueue(CheckGameVersion);
                    GameProcess = null;
                }
            }
        }
        catch { }
    }




    [RelayCommand]
    private async Task StartGameAsync()
    {
        try
        {
            bool launchingBlenderPlugin = LaunchGenshinBlenderPlugin || LaunchZZZBlenderPlugin;
            bool useShader = UseHoYoShade || UseOpenHoYoShade;

            // Case 1: Both Blender plugin and shader are selected
            // Start shader injector first, then launch Blender plugin (which will start the game)
            if (launchingBlenderPlugin && useShader)
            {
                // Start shader injector (it will wait for game process)
                if (UseHoYoShade)
                {
                    string hoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
                    await StartShaderInjectorOnlyAsync(hoYoShadePath, "HoYoShade");
                }
                else if (UseOpenHoYoShade)
                {
                    string openHoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
                    await StartShaderInjectorOnlyAsync(openHoYoShadePath, "OpenHoYoShade");
                }

                // Launch Blender plugin (it will start the game itself)
                if (LaunchGenshinBlenderPlugin)
                {
                    await LaunchGenshinBlenderPluginAsync();
                }

                if (LaunchZZZBlenderPlugin)
                {
                    await LaunchZZZBlenderPluginAsync();
                }

                _logger.LogInformation("Shader injector and Blender plugin launched");
                return;
            }

            // Case 2: Only Blender plugin is selected (no shader)
            if (launchingBlenderPlugin)
            {
                if (LaunchGenshinBlenderPlugin)
                {
                    await LaunchGenshinBlenderPluginAsync();
                }

                if (LaunchZZZBlenderPlugin)
                {
                    await LaunchZZZBlenderPluginAsync();
                }
                return;
            }

            // Case 3: Normal game launch with or without shader (no Blender plugin)
            Process? process = null;

            if (UseHoYoShade)
            {
                string hoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "HoYoShade");
                process = await LaunchGameWithShadeAsync(hoYoShadePath, "HoYoShade");
            }
            else if (UseOpenHoYoShade)
            {
                string openHoYoShadePath = Path.Combine(AppConfig.UserDataFolder, "OpenHoYoShade");
                process = await LaunchGameWithShadeAsync(openHoYoShadePath, "OpenHoYoShade");
            }
            else
            {
                process = await _gameLauncherService.StartGameAsync(CurrentGameId);
            }

            if (process is not null)
            {
                GameState = GameState.GameIsRunning;
                GameProcess = process;
                WeakReferenceMessenger.Default.Send(new GameStartedMessage());
            }
        }
        catch (FileNotFoundException)
        {
            CheckGameVersion();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start game");
        }
    }

    // 在 LaunchGameWithShadeAsync 方法后面添加这个新方法
    private async Task StartShaderInjectorOnlyAsync(string shadePath, string shadeName)
    {
        try
        {
            if (!Directory.Exists(shadePath))
            {
                _logger.LogWarning("{ShadeName} directory not found at {Path}", shadeName, shadePath);
                InAppToast.MainWindow?.Error($"{shadeName} 未安装，请先安装后再使用");
                return;
            }

            string injectExePath = Path.Combine(shadePath, "inject.exe");
            if (!File.Exists(injectExePath))
            {
                _logger.LogWarning("inject.exe not found in {ShadeName} at {Path}", shadeName, injectExePath);
                InAppToast.MainWindow?.Error($"{shadeName} 中未找到 inject.exe");
                return;
            }

            var gameExeName = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);

            _logger.LogInformation("Starting {ShadeName} injector: {InjectPath} {GameExe}",
                shadeName, injectExePath, gameExeName);

            var injectStartInfo = new ProcessStartInfo
            {
                FileName = injectExePath,
                Arguments = gameExeName,
                UseShellExecute = false,
                WorkingDirectory = shadePath,
                CreateNoWindow = true
            };

            Process.Start(injectStartInfo);
            _logger.LogInformation("{ShadeName} injector started, waiting for game process", shadeName);
            InAppToast.MainWindow?.Success($"已启动 {shadeName} 注入器");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start {ShadeName} injector", shadeName);
            InAppToast.MainWindow?.Error($"启动 {shadeName} 注入器失败: {ex.Message}");
        }
    }


    private async Task LaunchGenshinBlenderPluginAsync()
    {
        try
        {
            string? pluginPath = AppConfig.GenshinBlenderPluginPath;
            if (string.IsNullOrWhiteSpace(pluginPath) || !Directory.Exists(pluginPath))
            {
                _logger.LogWarning("Genshin Blender plugin path not configured");
                InAppToast.MainWindow?.Error("原神Blender/留影机插件路径未配置，请在设置中配置");
                return;
            }

            string clientExePath = Path.Combine(pluginPath, "client.exe");
            if (!File.Exists(clientExePath))
            {
                _logger.LogWarning("client.exe not found in {Path}", pluginPath);
                InAppToast.MainWindow?.Error("未找到 client.exe，请检查插件路径");
                return;
            }

            _logger.LogInformation("Launching Genshin Blender plugin: {Path}", clientExePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = clientExePath,
                WorkingDirectory = pluginPath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            InAppToast.MainWindow?.Success("已启动原神Blender/留影机插件");
            _logger.LogInformation("Genshin Blender plugin launched successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch Genshin Blender plugin");
            InAppToast.MainWindow?.Error($"启动原神Blender/留影机插件失败: {ex.Message}");
        }
    }

    private async Task LaunchZZZBlenderPluginAsync()
    {
        try
        {
            string? pluginPath = AppConfig.ZZZBlenderPluginPath;
            if (string.IsNullOrWhiteSpace(pluginPath) || !Directory.Exists(pluginPath))
            {
                _logger.LogWarning("ZZZ Blender plugin path not configured");
                InAppToast.MainWindow?.Error("绝区零Blender/留影机插件路径未配置，请在设置中配置");
                return;
            }

            string loaderExePath = Path.Combine(pluginPath, "loader.exe");
            if (!File.Exists(loaderExePath))
            {
                _logger.LogWarning("loader.exe not found in {Path}", pluginPath);
                InAppToast.MainWindow?.Error("未找到 loader.exe，请检查插件路径");
                return;
            }

            _logger.LogInformation("Launching ZZZ Blender plugin: {Path}", loaderExePath);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = loaderExePath,
                WorkingDirectory = pluginPath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            InAppToast.MainWindow?.Success("已启动绝区零Blender/留影机插件");
            _logger.LogInformation("ZZZ Blender plugin launched successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch ZZZ Blender plugin");
            InAppToast.MainWindow?.Error($"启动绝区零Blender/留影机插件失败: {ex.Message}");
        }
    }

    private async Task<Process?> LaunchGameWithShadeAsync(string shadePath, string shadeName)
    {
        try
        {
            if (!Directory.Exists(shadePath))
            {
                _logger.LogWarning("{ShadeName} directory not found at {Path}", shadeName, shadePath);
                InAppToast.MainWindow?.Error($"{shadeName} 未安装，请先安装后再使用");
                return null;
            }

            // Check if inject.exe exists
            string injectExePath = Path.Combine(shadePath, "inject.exe");
            if (!File.Exists(injectExePath))
            {
                _logger.LogWarning("inject.exe not found in {ShadeName} at {Path}", shadeName, injectExePath);
                InAppToast.MainWindow?.Error($"{shadeName} 中未找到 inject.exe");
                return null;
            }

            var gameInstallPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
            if (string.IsNullOrWhiteSpace(gameInstallPath))
            {
                _logger.LogWarning("Game install path not found");
                InAppToast.MainWindow?.Error("未找到游戏安装路径");
                return null;
            }

            var gameExeName = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);
            var gameExePath = Path.Combine(gameInstallPath, gameExeName);
            
            if (!File.Exists(gameExePath))
            {
                _logger.LogWarning("Game exe not found: {Path}", gameExePath);
                throw new FileNotFoundException("Game exe not found", gameExeName);
            }

            // Step 1: Start inject.exe (don't wait for it to finish)
            _logger.LogInformation("Starting {ShadeName} injector: {InjectPath} {GameExe}", 
                shadeName, injectExePath, gameExeName);

            var injectStartInfo = new ProcessStartInfo
            {
                FileName = injectExePath,
                Arguments = gameExeName,
                UseShellExecute = false,
                WorkingDirectory = shadePath,
                CreateNoWindow = true
            };

            Process.Start(injectStartInfo);
            _logger.LogInformation("Injector started, waiting before launching game...");

            // Step 2: Wait a short delay to let injector initialize
            await Task.Delay(500);

            // Step 3: Launch the game normally
            _logger.LogInformation("Launching game normally");
            var gameProcess = await _gameLauncherService.StartGameAsync(CurrentGameId, gameInstallPath);
            
            if (gameProcess != null)
            {
                InAppToast.MainWindow?.Success($"已使用 {shadeName} 启动游戏");
                _logger.LogInformation("Successfully launched game with {ShadeName}, process: {Name} ({Id})", 
                    shadeName, gameProcess.ProcessName, gameProcess.Id);
                return gameProcess;
            }
            else
            {
                _logger.LogWarning("Failed to start game process");
                InAppToast.MainWindow?.Error("游戏启动失败");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Launch game with {ShadeName}", shadeName);
            InAppToast.MainWindow?.Error($"使用 {shadeName} 启动游戏失败: {ex.Message}");
            return null;
        }
    }


    #endregion




    #region Install Game




    private async Task InstallGameAsync()
    {
        await LocateGameAsync();
    }


    private async Task ResumeDownloadAsync()
    {
        await LocateGameAsync();
    }






    #endregion




    #region Predownload




    [RelayCommand]
    private async Task PredownloadAsync()
    {
        // Removed predownload functionality
        await Task.CompletedTask;
    }





    #endregion



    #region Update



    private async Task UpdateGameAsync()
    {
        // Removed update functionality
        await Task.CompletedTask;
    }



    #endregion



    #region Game Install Task (Removed)



    private GameInstallContext? _gameInstallTask;




    private async Task ChangeGameInstallTaskStateAsync()
    {
        // Removed game install task logic
        await Task.CompletedTask;
    }



    private void UpdateGameInstallTask()
    {
        // Removed game install task logic
    }



    #endregion



    #region Drop Background File




    private void RootGrid_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            Border_BackgroundDragIn.Opacity = 1;
        }
    }




    private async void RootGrid_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        Border_BackgroundDragIn.Opacity = 0;
        var defer = e.GetDeferral();
        try
        {
            if ((await e.DataView.GetStorageItemsAsync()).FirstOrDefault() is StorageFile file)
            {
                string? name = await BackgroundService.ChangeCustomBackgroundFileAsync(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }
                AppConfig.SetCustomBg(CurrentGameBiz, name);
                AppConfig.SetEnableCustomBg(CurrentGameBiz, true);
                WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
            }
        }
        catch (COMException ex)
        {
            InAppToast.MainWindow?.Error(Lang.GameLauncherSettingDialog_CannotDecodeFile);
            _logger.LogError(ex, "Change custom background failed");
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(Lang.GameLauncherSettingDialog_AnUnknownErrorOccurredPleaseCheckTheLogs);
            _logger.LogError(ex, "Change custom background failed");
        }
        defer.Complete();
    }



    private void RootGrid_DragLeave(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        Border_BackgroundDragIn.Opacity = 0;
    }



    #endregion




    #region Game Setting



    [RelayCommand]
    private async Task OpenGameLauncherSettingDialogAsync()
    {
        await new GameLauncherSettingDialog { CurrentGameId = this.CurrentGameId, XamlRoot = this.XamlRoot }.ShowAsync();
    }




    #endregion



    #region Switch Background Image


    private const string PlayIcon = "\uF5B0";

    private const string PauseIcon = "\uE62E";


    private List<GameBackground> _backgroundImages;
    public List<GameBackground> BackgroundImages 
    { 
        get => _backgroundImages;
        set => SetProperty(ref _backgroundImages, value);
    }

    private bool _canStopVideo;
    public bool CanStopVideo 
    { 
        get => _canStopVideo;
        set => SetProperty(ref _canStopVideo, value);
    }

    private string _startStopButtonIcon;
    public string StartStopButtonIcon 
    { 
        get => _startStopButtonIcon;
        set => SetProperty(ref _startStopButtonIcon, value);
    }


    private int currentBackgroundImageIndex;
    public int CurrentBackgroundImageIndex
    {
        get => currentBackgroundImageIndex;
        set
        {
            if (SetProperty(ref currentBackgroundImageIndex, value))
            {
                ChangeBackgroundImageIndex(value);
            }
        }
    }


    private void OnBackgroundChanged(object _, BackgroundChangedMessage message)
    {
        if (message.GameBackground is null)
        {
            _ = InitializeBackgameImageSwitcherAsync();
        }
    }


    private async Task InitializeBackgameImageSwitcherAsync()
    {
        try
        {
            CanStopVideo = false;
            BackgroundImages = await _backgroundService.GetGameBackgroundsAsync(CurrentGameId);
            if (BackgroundImages.Count > 1)
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Visible;
                GameBackground? currentBackground = await _backgroundService.GetSuggestedGameBackgroundAsync(CurrentGameId);
                if (currentBackground != null && BackgroundImages.FirstOrDefault(x => x.Id == currentBackground.Id) is GameBackground current)
                {
                    currentBackgroundImageIndex = Math.Clamp(BackgroundImages.IndexOf(current), 0, BackgroundImages.Count - 1);
                    OnPropertyChanged(nameof(CurrentBackgroundImageIndex));
                    CanStopVideo = current.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
                    if (CanStopVideo)
                    {
                        current.StopVideo = currentBackground.StopVideo;
                        StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
                    }
                }
            }
            else
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize background image switcher {GameBiz}", CurrentGameBiz);
        }
    }


    private void ChangeBackgroundImageIndex(int index)
    {
        try
        {
            if (index < 0 || index >= BackgroundImages.Count)
            {
                return;
            }
            GameBackground current = BackgroundImages[index];
            WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage(current));
            CanStopVideo = current.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
            if (CanStopVideo)
            {
                StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
            }
        }
        catch { }
    }


    private void Border_SwitchBackgroundImage_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Border_SwitchBackgroundImage.Opacity = 1;
    }


    private void Border_SwitchBackgroundImage_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Border_SwitchBackgroundImage.Opacity = 0;
    }


    int _switchBackgroundTotalDelta = 0;

    private void Border_SwitchBackgroundImage_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        _switchBackgroundTotalDelta += delta;
        if (_switchBackgroundTotalDelta <= -120)
        {
            CurrentBackgroundImageIndex++;
            _switchBackgroundTotalDelta = 0;
        }
        else if (_switchBackgroundTotalDelta >= 120)
        {
            CurrentBackgroundImageIndex--;
            _switchBackgroundTotalDelta = 0;
        }
    }


    [RelayCommand]
    private async Task CopyCurrentBackgroundImageAsync()
    {
        try
        {
            string? path = null;
            GameBackground? background = AppBackground.Current.CurrentGameBackground;
            if (background?.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
            {
                path = background.Background.Url;
            }
            else if (background?.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !background.StopVideo)
            {
                string name = Path.GetFileName(background.Video.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            else if (background is not null)
            {
                string name = Path.GetFileName(background.Background.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            if (File.Exists(path))
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file);
                InAppToast.MainWindow?.Information(Lang.Common_CopiedToClipboard);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy current background image {GameBiz}", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private async Task SaveCurrentBackgroundImageAsync()
    {
        try
        {
            string? path = null;
            GameBackground? background = AppBackground.Current.CurrentGameBackground;
            if (background?.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
            {
                path = background.Background.Url;
            }
            else if (background?.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !background.StopVideo)
            {
                string name = Path.GetFileName(background.Video.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            else if (background is not null)
            {
                string name = Path.GetFileName(background.Background.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            if (File.Exists(path))
            {
                var savePath = await FileDialogHelper.OpenSaveFileDialogAsync(this.XamlRoot, Path.GetFileName(path));
                if (!string.IsNullOrWhiteSpace(savePath))
                {
                    File.Copy(path, savePath, true);
                    var file = await StorageFile.GetFileFromPathAsync(savePath);
                    var options = new FolderLauncherOptions();
                    options.ItemsToSelect.Add(file);
                    await Launcher.LaunchFolderAsync(await file.GetParentAsync(), options);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save as current background image {GameBiz}", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private void StartOrStopVideoBackground()
    {
        try
        {
            GameBackground current = BackgroundImages[CurrentBackgroundImageIndex];
            if (current.Type is GameBackground.BACKGROUND_TYPE_VIDEO)
            {
                current.StopVideo = !current.StopVideo;
                StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
                WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage(current));
            }
        }
        catch { }
    }


    #endregion



    #region Cloud Game




    #endregion


}
