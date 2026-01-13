using System;
using System.Linq;

namespace HoYoShadeHub.Helpers;

/// <summary>
/// Manages launcher update proxy server selection
/// </summary>
public class LauncherUpdateProxyManager
{
    // Tencent Cloud proxy URLs  
    // 腾讯云代理服务器用于代理 https://cdn.cf.storage.hub.hoyosha.de/release
    private static readonly string[] TencentCloudProxies = new[]
    {
        "https://hoyoshadehub-glasses-edgeone.edgeone.app",
        "https://cdn.green.sea.12ae.tx.storage.hub.hoyosha.de",
        "https://cdn.jolly.snowflake.cd46.tx.storage.hub.hoyosha.de",
        "https://cdn.bold.wood.c623.tx.storage.hub.hoyosha.de"
    };

    // Alibaba Cloud proxy URLs
    // 阿里云代理服务器用于代理 https://cdn.cf.storage.hub.hoyosha.de/release
    private static readonly string[] AlibabaCloudProxies = new[]
    {
        "https://hoyoshadehub-glasses.0e9398a1.er.aliyun-esa.net",
        "https://cdn.bold.wood.c623.ali.storage.hub.hoyosha.de",
        "https://cdn.jolly.snowflake.cd46.ali.storage.hub.hoyosha.de",
        "https://cdn.steep.pond.0c55.ali.storage.hub.hoyosha.de"
    };

    private static readonly Random _random = new Random();

    /// <summary>
    /// Get proxy URL for the specified download server
    /// </summary>
    /// <param name="serverIndex">Server index from DownloadServers list (0=Cloudflare(默认), 1=Tencent, 2=Alibaba)</param>
    /// <returns>Proxy URL or null if using Cloudflare direct</returns>
    public static string? GetProxyUrl(int serverIndex)
    {
        return serverIndex switch
        {
            0 => null, // Cloudflare (Direct - the built-in API is already on Cloudflare)
            1 => GetRandomProxy(TencentCloudProxies),
            2 => GetRandomProxy(AlibabaCloudProxies),
            _ => null
        };
    }

    /// <summary>
    /// Get a random proxy from the array
    /// </summary>
    private static string? GetRandomProxy(string[] proxies)
    {
        if (proxies.Length == 0)
        {
            return null;
        }

        int index = _random.Next(proxies.Length);
        return proxies[index];
    }
}
