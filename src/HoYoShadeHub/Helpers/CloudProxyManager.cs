using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Helpers;

/// <summary>
/// Manages cloud proxy selection and fallback logic for downloading
/// </summary>
public class CloudProxyManager
{
    // Cloudflare proxy URLs
    private static readonly string[] CloudflareProxies = new[]
    {
        "https://hoyoshadehub-glasses.pages.dev/",
        "https://cdn.autumn.recipe.2dcd.cf.storage.hub.hoyosha.de",
        "https://cdn.delicate.meadow.be18.cf.storage.hub.hoyosha.de",
        "https://cdn.weathered.wave.q2c3.cf.storage.hub.hoyosha.de"
    };

    // Tencent Cloud proxy URLs
    private static readonly string[] TencentCloudProxies = new[]
    {
        "https://hoyoshadehub-glasses-edgeone.edgeone.app",
        "https://cdn.green.sea.12ae.tx.storage.hub.hoyosha.de",
        "https://cdn.jolly.snowflake.cd46.tx.storage.hub.hoyosha.de",
        "https://cdn.bold.wood.c623.tx.storage.hub.hoyosha.de"
    };

    // Alibaba Cloud proxy URLs
    private static readonly string[] AlibabaCloudProxies = new[]
    {
        "https://hoyoshadehub-glasses.0e9398a1.er.aliyun-esa.net",
        "https://cdn.bold.wood.c623.ali.storage.hub.hoyosha.de",
        "https://cdn.jolly.snowflake.cd46.ali.storage.hub.hoyosha.de",
        "https://cdn.steep.pond.0c55.ali.storage.hub.hoyosha.de"
    };

    private static readonly Random _random = new Random();

    /// <summary>
    /// Get proxy URL prefix for the specified download server
    /// </summary>
    /// <param name="serverIndex">Server index from DownloadServers list (0=GitHub, 1=Cloudflare, 2=Tencent, 3=Alibaba)</param>
    /// <returns>Proxy URL prefix or null if using GitHub direct</returns>
    public static string? GetProxyUrl(int serverIndex)
    {
        return serverIndex switch
        {
            0 => null, // GitHub (Direct)
            1 => GetRandomProxy(CloudflareProxies),
            2 => GetRandomProxy(TencentCloudProxies),
            3 => GetRandomProxy(AlibabaCloudProxies),
            _ => null
        };
    }

    /// <summary>
    /// Apply proxy URL to the original URL
    /// </summary>
    /// <param name="originalUrl">Original URL to download</param>
    /// <param name="proxyUrl">Proxy URL prefix</param>
    /// <returns>Proxied URL or original URL if proxy is null</returns>
    public static string ApplyProxy(string originalUrl, string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return originalUrl;
        }

        return $"{proxyUrl}/{originalUrl}";
    }

    /// <summary>
    /// Get all proxy URLs for a specific cloud provider
    /// </summary>
    /// <param name="serverIndex">Server index (1=Cloudflare, 2=Tencent, 3=Alibaba)</param>
    /// <returns>Array of proxy URLs</returns>
    public static string[] GetAllProxiesForServer(int serverIndex)
    {
        return serverIndex switch
        {
            1 => CloudflareProxies,
            2 => TencentCloudProxies,
            3 => AlibabaCloudProxies,
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    /// Get the fallback sequence for Auto Select mode
    /// </summary>
    public static int[] GetAutoSelectFallbackSequence(bool isLauncherUpdate)
    {
        if (isLauncherUpdate)
        {
            // Launcher Update: Cloudflare -> Tencent -> Alibaba
            return new[] { 1, 2, 3 };
        }
        else
        {
            // HoYoShade/ReShade: GitHub -> Tencent -> Random(Cloudflare, Alibaba)
            var sequence = new List<int> { 0, 2 };
            if (_random.Next(2) == 0)
            {
                sequence.Add(1);
                sequence.Add(3);
            }
            else
            {
                sequence.Add(3);
                sequence.Add(1);
            }
            return sequence.ToArray();
        }
    }

    /// <summary>
    /// Try downloading with fallback to other proxies if the selected one fails
    /// </summary>
    /// <param name="originalUrl">Original URL to download</param>
    /// <param name="serverIndex">Server index from DownloadServers list</param>
    /// <param name="httpClient">HttpClient to use for requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>HttpResponseMessage from successful proxy, or throws if all fail</returns>
    public static async Task<HttpResponseMessage> DownloadWithFallbackAsync(
        string originalUrl,
        int serverIndex,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        // For GitHub direct, no fallback needed
        if (serverIndex == 0)
        {
            return await httpClient.GetAsync(originalUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        var proxies = GetAllProxiesForServer(serverIndex);
        if (proxies.Length == 0)
        {
            // Fallback to direct if no proxies
            return await httpClient.GetAsync(originalUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        // Shuffle proxies to try them in random order
        var shuffledProxies = proxies.OrderBy(_ => _random.Next()).ToArray();
        Exception? lastException = null;

        foreach (var proxy in shuffledProxies)
        {
            try
            {
                var proxiedUrl = ApplyProxy(originalUrl, proxy);
                var response = await httpClient.GetAsync(proxiedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                // If successful, return immediately
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                // If not successful, dispose and try next
                response.Dispose();
            }
            catch (Exception ex)
            {
                lastException = ex;
                // Continue to next proxy
            }
        }

        // All proxies failed, throw the last exception
        throw lastException ?? new HttpRequestException($"All proxy servers failed for URL: {originalUrl}");
    }

    /// <summary>
    /// Ping the download server to measure latency
    /// </summary>
    /// <param name="serverIndex">Server index</param>
    /// <param name="httpClient">HttpClient</param>
    /// <returns>Latency in milliseconds, or -1 if failed</returns>
    public static async Task<long> PingServerAsync(int serverIndex, HttpClient httpClient)
    {
        string pingUrl;
        if (serverIndex == 0) // GitHub Direct
        {
            pingUrl = "https://github.com/";
        }
        else
        {
            var proxies = GetAllProxiesForServer(serverIndex);
            if (proxies.Length == 0) return -1;
            // Use the first proxy to check latency, with /success.html/ to avoid 403 Forbidden
            string proxy = proxies[0].TrimEnd('/');
            pingUrl = $"{proxy}/success.html/";
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Send HEAD request to avoid downloading the whole page if possible, 
            // but GET is safer for just a quick success.html
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var request = new HttpRequestMessage(HttpMethod.Get, pingUrl);
            // Disable keep-alive to avoid connection reuse skewing the latency
            request.Headers.ConnectionClose = true;
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return stopwatch.ElapsedMilliseconds;
            }
            return -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Get a random proxy from the array
    /// </summary>
    private static string GetRandomProxy(string[] proxies)
    {
        if (proxies.Length == 0)
        {
            return null;
        }

        int index = _random.Next(proxies.Length);
        return proxies[index];
    }
}
