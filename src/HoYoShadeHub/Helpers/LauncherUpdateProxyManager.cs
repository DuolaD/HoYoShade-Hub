using System;
using System.Linq;

namespace HoYoShadeHub.Helpers;

/// <summary>
/// Manages launcher update proxy server selection
/// </summary>
public class LauncherUpdateProxyManager
{
    // Tencent Cloud proxy URLs  
    // ��Ѷ�ƴ������������ڴ��� https://cdn.cf.storage.hub.hoyosha.de/release
    private static readonly string[] TencentCloudProxies = new[]
    {
        "https://hoyoshadehub-glasses-edgeone.edgeone.app",
        "https://cdn.green.sea.12ae.tx.storage.hub.hoyosha.de",
        "https://cdn.jolly.snowflake.cd46.tx.storage.hub.hoyosha.de",
        "https://cdn.bold.wood.c623.tx.storage.hub.hoyosha.de"
    };

    // Alibaba Cloud proxy URLs
    // �����ƴ������������ڴ��� https://cdn.cf.storage.hub.hoyosha.de/release
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
    /// <param name="serverIndex">Server index from DownloadServers list (0=Cloudflare(Ĭ��), 1=Tencent, 2=Alibaba)</param>
    /// <returns>Proxy URL or null if using Cloudflare direct</returns>
    public static string? GetProxyUrl(int serverIndex)
    {
        return serverIndex switch
        {
            1 => null, // Cloudflare (Direct)
            2 => GetRandomProxy(TencentCloudProxies),
            3 => GetRandomProxy(AlibabaCloudProxies),
            _ => null
        };
    }

    /// <summary>
    /// Get all proxy URLs for a specific cloud provider
    /// </summary>
    public static string[] GetAllProxiesForServer(int serverIndex)
    {
        return serverIndex switch
        {
            2 => TencentCloudProxies,
            3 => AlibabaCloudProxies,
            _ => Array.Empty<string>()
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
