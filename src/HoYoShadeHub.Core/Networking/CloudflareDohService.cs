using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace HoYoShadeHub.Core.Networking;

public static class CloudflareDohService
{
    private sealed class DnsCacheItem
    {
        public required IPAddress[] Addresses { get; init; }

        public required DateTimeOffset ExpireAt { get; init; }
    }

    private static readonly ConcurrentDictionary<string, DnsCacheItem> _dnsCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient _dohHttpClient = new HttpClient(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        EnableMultipleHttp2Connections = true,
        EnableMultipleHttp3Connections = true,
    })
    {
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
    };

    private static readonly object _lock = new();

    private static IPAddress[] _cloudflareDohServerAddresses = [];

    private static readonly object _networkCapabilityLock = new();

    private static DateTimeOffset _localIPv4LastCheckedAt = DateTimeOffset.MinValue;

    private static bool _hasLocalIPv4 = true;

    private static bool _enabled;



    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            ClearDnsCache(flushSystemDnsCache: true);
            if (value)
            {
                _ = RefreshCloudflareDohServerAddressesAsync();
            }
        }
    }



    public static SocketsHttpHandler CreateSocketsHttpHandler()
    {
        return new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            ConnectCallback = ConnectWithDohAsync,
        };
    }



    public static async Task RefreshCloudflareDohServerAddressesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync("cloudflare-dns.com", cancellationToken);
            if (addresses.Length > 0)
            {
                lock (_lock)
                {
                    _cloudflareDohServerAddresses = addresses;
                }
            }
        }
        catch
        {
        }
    }



    public static void ClearDnsCache(bool flushSystemDnsCache = false)
    {
        _dnsCache.Clear();
        if (!flushSystemDnsCache)
        {
            return;
        }
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            process?.WaitForExit(3000);
        }
        catch
        {
        }
    }



    private static async ValueTask<Stream> ConnectWithDohAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        string host = context.DnsEndPoint.Host;
        var addresses = await ResolveHostAddressesAsync(host, cancellationToken);
        addresses = OrderAddressesByPreference(addresses);

        Exception? lastException = null;
        foreach (var ip in addresses)
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(ip, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                lastException = ex;
                socket.Dispose();
            }
        }

        throw lastException ?? new SocketException((int)SocketError.HostNotFound);
    }



    private static async Task<IPAddress[]> ResolveHostAddressesAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return [ipAddress];
        }

        if (!_enabled)
        {
            return OrderAddressesByPreference(await Dns.GetHostAddressesAsync(host, cancellationToken));
        }

        if (_dnsCache.TryGetValue(host, out var cacheItem) && cacheItem.ExpireAt > DateTimeOffset.UtcNow)
        {
            return cacheItem.Addresses;
        }

        IPAddress[] addresses;
        TimeSpan ttl;

        if (string.Equals(host, "cloudflare-dns.com", StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                addresses = _cloudflareDohServerAddresses;
            }
            if (addresses.Length == 0)
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                lock (_lock)
                {
                    _cloudflareDohServerAddresses = addresses;
                }
            }
            ttl = TimeSpan.FromMinutes(5);
        }
        else
        {
            try
            {
                (addresses, ttl) = await ResolveViaCloudflareDohAsync(host, cancellationToken);
            }
            catch
            {
                addresses = [];
                ttl = TimeSpan.Zero;
            }

            if (addresses.Length == 0)
            {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
                ttl = TimeSpan.FromMinutes(1);
            }
        }

        addresses = OrderAddressesByPreference(addresses);

        if (addresses.Length > 0)
        {
            _dnsCache[host] = new DnsCacheItem
            {
                Addresses = addresses,
                ExpireAt = DateTimeOffset.UtcNow + ttl,
            };
        }

        return addresses;
    }



    private static async Task<(IPAddress[] Addresses, TimeSpan Ttl)> ResolveViaCloudflareDohAsync(string host, CancellationToken cancellationToken)
    {
        bool hasLocalIPv4 = HasLocalIPv4();
        var primaryType = hasLocalIPv4 ? "A" : "AAAA";
        var secondaryType = hasLocalIPv4 ? "AAAA" : "A";

        var primaryResult = await QueryDohRecordSafeAsync(host, primaryType, cancellationToken);

        IPAddress[] addresses = primaryResult.Addresses;
        long minTtl = primaryResult.MinTtl;

        if (addresses.Length == 0)
        {
            var fallbackResult = await QueryDohRecordSafeAsync(host, secondaryType, cancellationToken);
            addresses = fallbackResult.Addresses;
            minTtl = fallbackResult.MinTtl;
        }

        if (minTtl <= 0)
        {
            minTtl = 60;
        }

        return (OrderAddressesByPreference(addresses), TimeSpan.FromSeconds(minTtl));
    }



    private static bool HasLocalIPv4()
    {
        if (!Socket.OSSupportsIPv4)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        lock (_networkCapabilityLock)
        {
            if (now - _localIPv4LastCheckedAt <= TimeSpan.FromSeconds(30))
            {
                return _hasLocalIPv4;
            }

            try
            {
                _hasLocalIPv4 = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                    .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                    .Any(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address));
            }
            catch
            {
                _hasLocalIPv4 = Socket.OSSupportsIPv4;
            }

            _localIPv4LastCheckedAt = now;
            return _hasLocalIPv4;
        }
    }



    private static IPAddress[] OrderAddressesByPreference(IPAddress[] addresses)
    {
        if (addresses.Length <= 1)
        {
            return addresses;
        }

        bool hasLocalIPv4 = HasLocalIPv4();
        if (hasLocalIPv4)
        {
            return addresses
                .OrderByDescending(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .ToArray();
        }

        return addresses
            .OrderByDescending(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
            .ToArray();
    }



    private static async Task<(IPAddress[] Addresses, long MinTtl)> QueryDohRecordSafeAsync(string host, string type, CancellationToken cancellationToken)
    {
        try
        {
            return await QueryDohRecordAsync(host, type, cancellationToken);
        }
        catch
        {
            return ([], 0);
        }
    }



    private static async Task<(IPAddress[] Addresses, long MinTtl)> QueryDohRecordAsync(string host, string type, CancellationToken cancellationToken)
    {
        string url = $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type={type}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/dns-json");

        using var response = await _dohHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("Status", out var status) && status.GetInt32() != 0)
        {
            return ([], 0);
        }

        if (!document.RootElement.TryGetProperty("Answer", out var answer) || answer.ValueKind != JsonValueKind.Array)
        {
            return ([], 0);
        }

        List<IPAddress> addresses = new();
        long minTtl = 0;
        foreach (var item in answer.EnumerateArray())
        {
            if (!item.TryGetProperty("data", out var dataElement))
            {
                continue;
            }
            string? data = dataElement.GetString();
            if (string.IsNullOrWhiteSpace(data) || !IPAddress.TryParse(data, out var address))
            {
                continue;
            }
            addresses.Add(address);

            if (item.TryGetProperty("TTL", out var ttlElement))
            {
                long ttl = ttlElement.GetInt64();
                minTtl = minTtl == 0 ? ttl : Math.Min(minTtl, ttl);
            }
        }

        return (addresses.ToArray(), minTtl);
    }
}
