using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.RPC.HoYoShadeInstall;

/// <summary>
/// ReShade 着色器和插件清单拉取服务（支持自动选择序列与多代理轮询）
/// </summary>
public class ReShadePackageService
{
    /// <summary>
    /// 获取着色器和插件清单（并发拉取）
    /// </summary>
    /// <param name="serverIndex">下载服务器索引（-1=自动选择，0=GitHub直连，1=Cloudflare，2=腾讯云，3=阿里云）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<(List<EffectPackage> effects, List<Addon> addons)> FetchPackagesAsync(int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        var effectsTask = FetchEffectPackagesAsync(serverIndex, cancellationToken);
        var addonsTask = FetchAddonsAsync(serverIndex, cancellationToken);

        await Task.WhenAll(effectsTask, addonsTask);
        return (await effectsTask, await addonsTask);
    }

    /// <summary>
    /// 获取 EffectPackages 清单
    /// </summary>
    public async Task<List<EffectPackage>> FetchEffectPackagesAsync(int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        string rawUrl = ReShadeDownloadServer.EffectPackagesUrl;
        using var stream = await FetchStreamWithFallbackAsync(rawUrl, serverIndex, cancellationToken);
        return ParseEffectPackages(stream);
    }

    /// <summary>
    /// 获取 Addons 清单
    /// </summary>
    public async Task<List<Addon>> FetchAddonsAsync(int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        string rawUrl = ReShadeDownloadServer.AddonsUrl;
        using var stream = await FetchStreamWithFallbackAsync(rawUrl, serverIndex, cancellationToken);
        return ParseAddons(stream);
    }

    /// <summary>
    /// 使用服务器序列与代理轮询获取 Stream
    /// </summary>
    private static async Task<MemoryStream> FetchStreamWithFallbackAsync(string rawUrl, int serverIndex, CancellationToken cancellationToken)
    {
        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(false) // [0, 1, 2, 3] -> GitHub, Cloudflare, Tencent, Alibaba
            : new[] { serverIndex };

        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

        Exception? lastFallbackException = null;

        foreach (var currentServerIndex in serverSequence)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string?[] proxies = currentServerIndex == 0
                ? new string?[] { null }
                : CloudProxyManager.GetAllProxiesForServer(currentServerIndex).OrderBy(_ => Random.Shared.Next()).ToArray();

            if (proxies.Length == 0)
            {
                proxies = new string?[] { null };
            }

            foreach (var proxyUrl in proxies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string currentUrl = string.IsNullOrWhiteSpace(proxyUrl)
                    ? rawUrl
                    : CloudProxyManager.ApplyProxy(rawUrl, proxyUrl);

                try
                {
                    using var response = await client.GetAsync(currentUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var memoryStream = new MemoryStream();
                    await response.Content.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;
                    return memoryStream;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                    // Proceed to next proxy or next server
                }
            }
        }

        throw lastFallbackException ?? new HttpRequestException($"Failed to fetch {rawUrl} from all fallback servers.");
    }

    private static List<EffectPackage> ParseEffectPackages(Stream stream)
    {
        var effectsIni = new IniFile(stream);
        var result = new List<EffectPackage>();

        foreach (string packageSection in effectsIni.GetSections())
        {
            bool required = effectsIni.GetString(packageSection, "Required") == "1";
            bool? enabled;
            if (required)
            {
                enabled = true;
            }
            else
            {
                string enabledStr = effectsIni.GetString(packageSection, "Enabled", "0");
                enabled = enabledStr == "1";
            }

            effectsIni.GetValue(packageSection, "EffectFiles", out string[] effectFiles);
            effectsIni.GetValue(packageSection, "DenyEffectFiles", out string[] denyEffectFiles);

            var item = new EffectPackage
            {
                Selected = enabled,
                Modifiable = !required,
                Name = effectsIni.GetString(packageSection, "PackageName"),
                Description = effectsIni.GetString(packageSection, "PackageDescription"),
                InstallPath = effectsIni.GetString(packageSection, "InstallPath", string.Empty),
                TextureInstallPath = effectsIni.GetString(packageSection, "TextureInstallPath", string.Empty),
                DownloadUrl = effectsIni.GetString(packageSection, "DownloadUrl"),
                RepositoryUrl = effectsIni.GetString(packageSection, "RepositoryUrl"),
                EffectFiles = effectFiles?.Where(x => denyEffectFiles == null || !denyEffectFiles.Contains(x))
                    .Select(x => new EffectFile { FileName = x, Selected = false }).ToArray(),
                DenyEffectFiles = denyEffectFiles
            };

            result.Add(item);
        }

        return result;
    }

    private static List<Addon> ParseAddons(Stream stream)
    {
        var addonsIni = new IniFile(stream);
        var result = new List<Addon>();

        foreach (string addon in addonsIni.GetSections())
        {
            string downloadUrl = addonsIni.GetString(addon, "DownloadUrl64");
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = addonsIni.GetString(addon, "DownloadUrl");
            }

            var item = new Addon
            {
                Name = addonsIni.GetString(addon, "PackageName"),
                Description = addonsIni.GetString(addon, "PackageDescription"),
                EffectInstallPath = addonsIni.GetString(addon, "EffectInstallPath", string.Empty),
                DownloadUrl = downloadUrl,
                RepositoryUrl = addonsIni.GetString(addon, "RepositoryUrl"),
                Selected = false // Default to not selected for addons
            };

            result.Add(item);
        }

        return result;
    }
}
