using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.RPC.HoYoShadeInstall;

internal class HoYoShadeInstallController : HoYoShadeInstaller.HoYoShadeInstallerBase
{
    private readonly ILogger<HoYoShadeInstallController> _logger;
    private readonly HoYoShadeInstallService _service;

    public HoYoShadeInstallController(ILogger<HoYoShadeInstallController> logger, HoYoShadeInstallService service)
    {
        _logger = logger;
        _service = service;
    }

    public override async Task InstallHoYoShade(InstallHoYoShadeRequest request, IServerStreamWriter<InstallHoYoShadeProgress> responseStream, ServerCallContext context)
    {
        try
        {
            _ = _service.StartInstallAsync(request.DownloadUrl, request.TargetPath, context.CancellationToken, request.PresetsHandling, request.VersionTag);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(context.CancellationToken))
            {
                var progress = new InstallHoYoShadeProgress
                {
                    State = _service.State,
                    TotalBytes = _service.TotalBytes,
                    DownloadBytes = _service.DownloadBytes,
                    ErrorMessage = _service.ErrorMessage
                };

                await responseStream.WriteAsync(progress);

                if (_service.State == 3 || _service.State == 4)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("HoYoShade installation canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HoYoShadeInstallController error");
            var errorMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.ToString() : ex.Message;
            await responseStream.WriteAsync(new InstallHoYoShadeProgress
            {
                State = 4,
                ErrorMessage = errorMessage
            });
        }
    }

    public override async Task InstallReShadePack(InstallReShadePackRequest request, IServerStreamWriter<InstallReShadePackProgress> responseStream, ServerCallContext context)
    {
        try
        {
            var installTarget = (ReShadeInstallTarget)request.InstallTarget;
            bool useProxy = request.DownloadServer.Contains("ghproxy.com", StringComparison.OrdinalIgnoreCase);

            // Fetch packages from official API
            _logger.LogInformation("Fetching packages from official API, useProxy: {UseProxy}", useProxy);
            var effectPackages = await _service.FetchEffectPackagesAsync(useProxy, context.CancellationToken);
            var addons = await _service.FetchAddonsAsync(useProxy, context.CancellationToken);
            
            _logger.LogInformation("Fetched {EffectCount} effect packages and {AddonCount} addons",
                effectPackages.Count, addons.Count);

            // Filter based on install mode
            var selectedEffects = new List<EffectPackage>();
            var selectedAddons = new List<Addon>();

            switch (request.InstallMode)
            {
                case 0: // All - Download ALL packages regardless of Enabled flag
                    selectedEffects = effectPackages.ToList(); // All effect packages
                    selectedAddons = addons.Where(a => a.Enabled).ToList(); // All addons with download URLs
                    _logger.LogInformation("All mode: Selected ALL {EffectCount} effects and {AddonCount} addons",
                        selectedEffects.Count, selectedAddons.Count);
                    break;
                case 1: // EssentialOnly - Download only required/enabled packages
                    selectedEffects = effectPackages.Where(p => !p.Modifiable || p.Selected == true).ToList();
                    // No addons in essential mode
                    _logger.LogInformation("EssentialOnly mode: Selected {EffectCount} essential effects", selectedEffects.Count);
                    break;
                case 2: // Custom - Download user-selected packages
                    var customSet = new HashSet<string>(request.CustomPackages, StringComparer.OrdinalIgnoreCase);
                    selectedEffects = effectPackages.Where(p => customSet.Contains(p.Name)).ToList();
                    selectedAddons = addons.Where(a => customSet.Contains(a.Name)).ToList();
                    _logger.LogInformation("Custom mode: Selected {EffectCount} effects and {AddonCount} addons from {CustomCount} custom names",
                        selectedEffects.Count, selectedAddons.Count, customSet.Count);
                    break;
            }

            if (selectedEffects.Count == 0 && selectedAddons.Count == 0)
            {
                _logger.LogWarning("No packages selected for installation!");
                await responseStream.WriteAsync(new InstallReShadePackProgress
                {
                    State = 4,
                    ErrorMessage = "No packages selected for installation"
                });
                return;
            }

            // Start installation
            _ = _service.InstallReShadePackAsync(
                request.BasePath,
                installTarget,
                useProxy,
                selectedEffects,
                selectedAddons,
                context.CancellationToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(context.CancellationToken))
            {
                var progress = new InstallReShadePackProgress
                {
                    State = _service.ReShadePackState,
                    TotalFiles = _service.TotalFiles,
                    DownloadedFiles = _service.DownloadedFiles,
                    TotalBytes = _service.ReShadePackTotalBytes,
                    DownloadBytes = _service.ReShadePackDownloadBytes,
                    CurrentFile = _service.CurrentFile,
                    ErrorMessage = _service.ReShadePackErrorMessage,
                    CurrentFileType = _service.CurrentFileType,
                    DownloadSpeedBytesPerSec = _service.DownloadSpeedBytesPerSec
                };

                await responseStream.WriteAsync(progress);

                if (_service.ReShadePackState == 3 || _service.ReShadePackState == 4)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ReShade pack installation canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InstallReShadePackController error");
            var errorMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.ToString() : ex.Message;
            await responseStream.WriteAsync(new InstallReShadePackProgress
            {
                State = 4,
                ErrorMessage = errorMessage
            });
        }
    }
}
