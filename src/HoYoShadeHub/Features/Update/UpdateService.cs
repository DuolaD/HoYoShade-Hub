using Grpc.Core;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using HoYoShadeHub.Features.RPC;
using HoYoShadeHub.Helpers;
using HoYoShadeHub.RPC.Update;
using HoYoShadeHub.RPC.Update.Metadata;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Update;

internal class UpdateService
{

    private readonly ILogger<UpdateService> _logger;

    private readonly RpcService _rpcService;

    private readonly MetadataClient _metadataClient;


    public UpdateService(ILogger<UpdateService> logger, RpcService rpcService, MetadataClient metadataClient)
    {
        _logger = logger;
        _rpcService = rpcService;
        _metadataClient = metadataClient;
    }



    /// <summary>
    /// 检查启动器更新（支持服务器序列自动回退与多代理轮询）
    /// </summary>
    /// <param name="disableIgnore">是否禁用忽略版本</param>
    /// <param name="serverIndex">下载服务器索引（-1=自动选择，1=Cloudflare，2=腾讯云，3=阿里云）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<ReleaseInfoDetail?> CheckUpdateAsync(bool disableIgnore, int serverIndex, CancellationToken cancellationToken = default)
    {
        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(true) // [1, 2, 3] -> Cloudflare, Tencent, Alibaba
            : new[] { serverIndex };

        ReleaseInfoDetail? release = null;
        Exception? lastFallbackException = null;

        foreach (var currentServerIndex in serverSequence)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Cloudflare (1) direct uses null proxy, others use proxies from LauncherUpdateProxyManager
            string?[] proxies = currentServerIndex == 1
                ? new string?[] { null }
                : LauncherUpdateProxyManager.GetAllProxiesForServer(currentServerIndex).OrderBy(_ => Random.Shared.Next()).ToArray();

            if (proxies.Length == 0)
            {
                proxies = new string?[] { null };
            }

            foreach (var proxyUrl in proxies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _metadataClient.SetProxyUrl(proxyUrl);
#if DEBUG
                    release = await _metadataClient.GetReleaseInfoAsync(AppConfig.EnablePreviewRelease, RuntimeInformation.ProcessArchitecture, InstallType.Portable, cancellationToken);
#else
                    release = await _metadataClient.GetReleaseInfoAsync(AppConfig.EnablePreviewRelease, RuntimeInformation.ProcessArchitecture, (InstallType)(AppConfig.IsPortable ? 1 : 0), cancellationToken);
#endif
                    if (release != null)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                    if (GitHubRateLimitHelper.IsRateLimitExceeded(ex))
                    {
                        _logger.LogWarning(ex, "Rate limit / 403 / 429 hit on launcher update server {ServerIndex} (proxy: {ProxyUrl}), switching to next available option.", currentServerIndex, proxyUrl ?? "Direct");
                    }
                    else
                    {
                        _logger.LogWarning(ex, "Failed to fetch launcher release info from server {ServerIndex} (proxy: {ProxyUrl}), switching to next available option.", currentServerIndex, proxyUrl ?? "Direct");
                    }
                }
            }

            if (release != null)
            {
                break;
            }
        }

        if (release == null)
        {
            throw lastFallbackException ?? new HttpRequestException("Failed to fetch launcher release info from all fallback servers.");
        }

        _ = NuGetVersion.TryParse(AppConfig.AppVersion, out var currentVersion);
        _ = NuGetVersion.TryParse(AppConfig.IgnoreVersion, out var ignoreVersion);
        _logger.LogInformation("Current version: {currentVersion}, latest version: {latestVersion}, ignore version: {ignoreVersion}.", AppConfig.AppVersion, release.Version, ignoreVersion);

        _ = NuGetVersion.TryParse(release.Version, out var newVersion);
        if (newVersion! > currentVersion!)
        {
            if (disableIgnore || newVersion! > ignoreVersion!)
            {
                return release;
            }
        }
        return null;
    }

    /// <summary>
    /// 检查启动器更新（兼容单代理或默认自动选择）
    /// </summary>
    public async Task<ReleaseInfoDetail?> CheckUpdateAsync(bool disableIgnore = false, string? proxyUrl = null)
    {
        if (proxyUrl == null)
        {
            return await CheckUpdateAsync(disableIgnore, -1);
        }

        _metadataClient.SetProxyUrl(proxyUrl);
        _ = NuGetVersion.TryParse(AppConfig.AppVersion, out var currentVersion);
        _ = NuGetVersion.TryParse(AppConfig.IgnoreVersion, out var ignoreVersion);
#if DEBUG
        var release = await _metadataClient.GetReleaseInfoAsync(AppConfig.EnablePreviewRelease, RuntimeInformation.ProcessArchitecture, InstallType.Portable);
#else
        var release = await _metadataClient.GetReleaseInfoAsync(AppConfig.EnablePreviewRelease, RuntimeInformation.ProcessArchitecture, (InstallType)(AppConfig.IsPortable ? 1 : 0));
#endif
        _logger.LogInformation("Current version: {currentVersion}, latest version: {latestVersion}, ignore version: {ignoreVersion}.", AppConfig.AppVersion, release?.Version, ignoreVersion);
        _ = NuGetVersion.TryParse(release?.Version, out var newVersion);
        if (newVersion! > currentVersion!)
        {
            if (disableIgnore || newVersion! > ignoreVersion!)
            {
                return release;
            }
        }
        return null;
    }



    public static bool UpdateFinished { get; private set; }


    public UpdateState State { get; private set; }

    public int Progress_TotalFileCount { get; private set; }

    public int Progress_DownloadFileCount { get; private set; }

    public long Progress_TotalBytes { get; private set; }

    public long Progress_DownloadBytes { get; private set; }

    public string? ErrorMessage { get; private set; }



    private bool _isUpdating;

    private CancellationTokenSource? _cancellationTokenSource;


    public async Task StartUpdateAsync(ReleaseInfoDetail release, string? proxyUrl = null)
    {
        if (_isUpdating || UpdateFinished)
        {
            State = UpdateFinished ? UpdateState.Finish : State;
            return;
        }
        try
        {
            ClearState();
            _isUpdating = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            State = UpdateState.Pending;
            if (!AppConfig.IsPortable)
            {
                // 无法自动更新
                ErrorMessage = Lang.UpdateService_CannotUpdateAutomatically;
                State = UpdateState.NotSupport;
                return;
            }
            await StartInternalAsync(release, proxyUrl, _cancellationTokenSource.Token);
            if (State is UpdateState.Finish)
            {
                UpdateFinished = true;
            }
            else if (State is not UpdateState.Finish and not UpdateState.Error)
            {
                _logger.LogWarning("Update stopped with unexpected state: {state}", State);
                State = UpdateState.Stop;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start update");
            State = UpdateState.Error;
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isUpdating = false;
        }
    }




    private async Task StartInternalAsync(ReleaseInfoDetail release, string? proxyUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _rpcService.EnsureRpcServerRunningAsync())
            {
                // 操作取消
                State = UpdateState.Stop;
                _logger.LogWarning("Start rpc server cancelled.");
                return;
            }
            var client = RpcService.CreateRpcClient<Updater.UpdaterClient>();
            var request = new UpdateRequest
            {
                Version = release.Version,
                Architecture = (int)release.Architecture,
                InstallType = (int)release.InstallType,
                TargetPath = Path.GetDirectoryName(AppConfig.HoYoShadeHubLauncherExecutePath),
                CurrentVersion = AppConfig.AppVersion,
                ProxyUrl = proxyUrl ?? string.Empty
            };
            using var call = client.Update(request, cancellationToken: cancellationToken);
            await foreach (UpdateProgress progress in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                State = (UpdateState)progress.State;
                Progress_TotalFileCount = progress.TotalFile;
                Progress_DownloadFileCount = progress.DownloadFile;
                Progress_TotalBytes = progress.TotalBytes;
                Progress_DownloadBytes = progress.DownloadBytes;
                ErrorMessage = progress.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start update internal");
            State = UpdateState.Error;
            ErrorMessage = ex.Message;
        }
    }



    public void StopUpdate()
    {
        _cancellationTokenSource?.Cancel();
    }



    private void ClearState()
    {
        State = UpdateState.Stop;
        Progress_TotalFileCount = 0;
        Progress_DownloadFileCount = 0;
        Progress_TotalBytes = 0;
        Progress_DownloadBytes = 0;
        ErrorMessage = null;
    }



}
