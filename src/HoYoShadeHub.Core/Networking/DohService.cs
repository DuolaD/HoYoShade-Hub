using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace HoYoShadeHub.Core.Networking;

public enum DohProvider
{
    Cloudflare = 0,
    Google = 1,
    CleanBrowsing = 2,
    OpenDns = 3,
    Quad9 = 4,
    AdGuard = 5,
    Aliyun = 6,
    Tencent = 7,
}

public static class DohService
{
    private sealed class DnsCacheItem
    {
        public required IPAddress[] Addresses { get; init; }

        public required DateTimeOffset ExpireAt { get; init; }
    }

    private sealed class DohProviderEndpoint
    {
        public required string DohHost { get; init; }

        public required string DohEndpoint { get; init; }

        public required IPAddress[] BootstrapDnsServers { get; init; }
    }

    private static readonly ConcurrentDictionary<string, DnsCacheItem> _dnsCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient _dohHttpClient = new HttpClient(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        EnableMultipleHttp2Connections = true,
        EnableMultipleHttp3Connections = true,
        ConnectCallback = ConnectWithDohAsync,
    })
    {
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
    };

    private static readonly Dictionary<DohProvider, DohProviderEndpoint> _providerEndpoints = new()
    {
        [DohProvider.Cloudflare] = new DohProviderEndpoint
        {
            DohHost = "cloudflare-dns.com",
            DohEndpoint = "https://cloudflare-dns.com/dns-query",
            BootstrapDnsServers = ParseIpAddresses("1.1.1.1", "1.0.0.1", "2606:4700:4700::1111", "2606:4700:4700::1001"),
        },
        [DohProvider.Google] = new DohProviderEndpoint
        {
            DohHost = "dns.google",
            DohEndpoint = "https://dns.google/dns-query",
            BootstrapDnsServers = ParseIpAddresses("8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844"),
        },
        [DohProvider.CleanBrowsing] = new DohProviderEndpoint
        {
            DohHost = "doh.cleanbrowsing.org",
            DohEndpoint = "https://doh.cleanbrowsing.org/doh/security-filter",
            BootstrapDnsServers = ParseIpAddresses("185.228.168.9", "185.228.169.9", "2a0d:2a00:1::2", "2a0d:2a00:2::2"),
        },
        [DohProvider.OpenDns] = new DohProviderEndpoint
        {
            DohHost = "doh.opendns.com",
            DohEndpoint = "https://doh.opendns.com/dns-query",
            BootstrapDnsServers = ParseIpAddresses("208.67.222.222", "208.67.220.220", "2620:119:35::35", "2620:119:53::53"),
        },
        [DohProvider.Quad9] = new DohProviderEndpoint
        {
            DohHost = "dns.quad9.net",
            DohEndpoint = "https://dns.quad9.net/dns-query",
            BootstrapDnsServers = ParseIpAddresses("9.9.9.9", "149.112.112.112", "2620:fe::fe", "2620:fe::9"),
        },
        [DohProvider.AdGuard] = new DohProviderEndpoint
        {
            DohHost = "dns.adguard-dns.com",
            DohEndpoint = "https://dns.adguard-dns.com/dns-query",
            BootstrapDnsServers = ParseIpAddresses("94.140.14.14", "94.140.15.15", "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff"),
        },
        [DohProvider.Aliyun] = new DohProviderEndpoint
        {
            DohHost = "dns.alidns.com",
            DohEndpoint = "https://dns.alidns.com/dns-query",
            BootstrapDnsServers = ParseIpAddresses("223.5.5.5", "223.6.6.6", "2400:3200::1", "2400:3200:baba::1"),
        },
        [DohProvider.Tencent] = new DohProviderEndpoint
        {
            DohHost = "doh.pub",
            DohEndpoint = "https://doh.pub/dns-query",
            BootstrapDnsServers = ParseIpAddresses("119.29.29.29", "182.254.116.116", "2402:4e00::", "2402:4e00:1::"),
        },
    };

    private static readonly object _lock = new();

    private static IPAddress[] _dohServerAddresses = [];

    private static readonly object _networkCapabilityLock = new();

    private static DateTimeOffset _localIPv4LastCheckedAt = DateTimeOffset.MinValue;

    private static bool _hasLocalIPv4 = true;

    private static bool _enabled;

    private static DohProvider _provider = DohProvider.Cloudflare;



    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            ClearDnsCache(flushSystemDnsCache: true);
            if (value)
            {
                _ = RefreshDohServerAddressesAsync();
            }
        }
    }



    public static DohProvider Provider
    {
        get => _provider;
        set
        {
            if (_provider == value)
            {
                return;
            }

            _provider = value;
            lock (_lock)
            {
                _dohServerAddresses = [];
            }
            ClearDnsCache(flushSystemDnsCache: true);
            if (_enabled)
            {
                _ = RefreshDohServerAddressesAsync();
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
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
        };
    }



    public static async Task RefreshDohServerAddressesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = GetProviderEndpoint(_provider);
            var addresses = await ResolveByBootstrapDnsAsync(endpoint.DohHost, endpoint.BootstrapDnsServers, cancellationToken);
            if (addresses.Length > 0)
            {
                lock (_lock)
                {
                    _dohServerAddresses = addresses;
                }
            }
        }
        catch
        {
        }
    }



    public static async Task<long> TcpPingAsync(DohProvider provider, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = GetProviderEndpoint(provider);
            var addresses = await ResolveByBootstrapDnsAsync(endpoint.DohHost, endpoint.BootstrapDnsServers, cancellationToken);
            addresses = OrderAddressesByPreference(addresses);
            if (addresses.Length == 0)
            {
                return -1;
            }

            var stopwatch = Stopwatch.StartNew();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            if (await TryConnectAsync(addresses, 443, timeoutCts.Token) is not null)
            {
                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
        }
        catch
        {
        }

        return -1;
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



    private static DohProviderEndpoint GetProviderEndpoint(DohProvider provider)
    {
        if (_providerEndpoints.TryGetValue(provider, out var endpoint))
        {
            return endpoint;
        }

        return _providerEndpoints[DohProvider.Cloudflare];
    }



    private static async ValueTask<Stream> ConnectWithDohAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        string host = context.DnsEndPoint.Host;
        var addresses = await ResolveHostAddressesAsync(host, cancellationToken);
        addresses = OrderAddressesByPreference(addresses);

        if (await TryConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken) is Stream primaryStream)
        {
            return primaryStream;
        }

        var fallbackFamilyAddresses = await ResolveFallbackFamilyAddressesAsync(host, addresses, cancellationToken);
        fallbackFamilyAddresses = OrderAddressesByPreference(fallbackFamilyAddresses);

        if (await TryConnectAsync(fallbackFamilyAddresses, context.DnsEndPoint.Port, cancellationToken) is Stream fallbackStream)
        {
            return fallbackStream;
        }

        throw new SocketException((int)SocketError.HostNotFound);
    }



    private static async Task<Stream?> TryConnectAsync(IPAddress[] addresses, int port, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        foreach (var ip in addresses)
        {
            var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(ip, port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                lastException = ex;
                socket.Dispose();
            }
        }

        _ = lastException;
        return null;
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

        var endpoint = GetProviderEndpoint(_provider);

        IPAddress[] addresses;
        TimeSpan ttl;

        if (string.Equals(host, endpoint.DohHost, StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                addresses = _dohServerAddresses;
            }

            if (addresses.Length == 0)
            {
                addresses = await ResolveByBootstrapDnsAsync(endpoint.DohHost, endpoint.BootstrapDnsServers, cancellationToken);
                lock (_lock)
                {
                    _dohServerAddresses = addresses;
                }
            }

            ttl = TimeSpan.FromMinutes(5);
        }
        else
        {
            try
            {
                (addresses, ttl) = await ResolveViaDohAsync(host, cancellationToken);
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



    private static async Task<(IPAddress[] Addresses, TimeSpan Ttl)> ResolveViaDohAsync(string host, CancellationToken cancellationToken)
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



    private static async Task<IPAddress[]> ResolveFallbackFamilyAddressesAsync(string host, IPAddress[] primaryAddresses, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out _))
        {
            return [];
        }

        bool hasLocalIPv4 = HasLocalIPv4();
        AddressFamily secondaryFamily = hasLocalIPv4 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;

        if (primaryAddresses.Any(ip => ip.AddressFamily == secondaryFamily))
        {
            return [];
        }

        var endpoint = GetProviderEndpoint(_provider);
        IPAddress[] fallbackAddresses;
        if (string.Equals(host, endpoint.DohHost, StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                fallbackAddresses = _dohServerAddresses
                    .Where(ip => ip.AddressFamily == secondaryFamily)
                    .ToArray();
            }

            if (fallbackAddresses.Length == 0)
            {
                fallbackAddresses = (await ResolveByBootstrapDnsAsync(host, endpoint.BootstrapDnsServers, cancellationToken))
                    .Where(ip => ip.AddressFamily == secondaryFamily)
                    .ToArray();
            }
        }
        else
        {
            string fallbackType = hasLocalIPv4 ? "AAAA" : "A";
            var fallbackResult = await QueryDohRecordSafeAsync(host, fallbackType, cancellationToken);
            fallbackAddresses = fallbackResult.Addresses;

            if (fallbackAddresses.Length == 0)
            {
                fallbackAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            }

            fallbackAddresses = fallbackAddresses.Where(ip => ip.AddressFamily == secondaryFamily).ToArray();
        }

        if (fallbackAddresses.Length > 0)
        {
            var merged = primaryAddresses.Concat(fallbackAddresses).Distinct().ToArray();
            _dnsCache[host] = new DnsCacheItem
            {
                Addresses = OrderAddressesByPreference(merged),
                ExpireAt = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1),
            };
        }

        return fallbackAddresses;
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



    private static async Task<IPAddress[]> ResolveByBootstrapDnsAsync(string host, IPAddress[] dnsServers, CancellationToken cancellationToken)
    {
        bool hasLocalIPv4 = HasLocalIPv4();
        string primaryType = hasLocalIPv4 ? "A" : "AAAA";
        string secondaryType = hasLocalIPv4 ? "AAAA" : "A";

        var primary = await QueryBootstrapServersForTypeAsync(host, primaryType, dnsServers, cancellationToken);
        if (primary.Length > 0)
        {
            return primary;
        }

        return await QueryBootstrapServersForTypeAsync(host, secondaryType, dnsServers, cancellationToken);
    }



    private static async Task<IPAddress[]> QueryBootstrapServersForTypeAsync(string host, string type, IPAddress[] dnsServers, CancellationToken cancellationToken)
    {
        var result = new HashSet<IPAddress>();

        foreach (var server in dnsServers)
        {
            var query = await QueryDnsServerRecordSafeAsync(host, type, server, cancellationToken);
            foreach (var address in query.Addresses)
            {
                result.Add(address);
            }
        }

        return result.ToArray();
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



    private static async Task<(IPAddress[] Addresses, long MinTtl)> QueryDnsServerRecordSafeAsync(string host, string type, IPAddress dnsServer, CancellationToken cancellationToken)
    {
        try
        {
            return await QueryDnsServerRecordAsync(host, type, dnsServer, cancellationToken);
        }
        catch
        {
            return ([], 0);
        }
    }



    private static async Task<(IPAddress[] Addresses, long MinTtl)> QueryDnsServerRecordAsync(string host, string type, IPAddress dnsServer, CancellationToken cancellationToken)
    {
        ushort queryType = type switch
        {
            "A" => 1,
            "AAAA" => 28,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        byte[] queryMessage = BuildDnsQueryMessage(host, queryType);

        using var socket = new Socket(dnsServer.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        await socket.ConnectAsync(new IPEndPoint(dnsServer, 53), cancellationToken);
        await socket.SendAsync(queryMessage, SocketFlags.None, cancellationToken);

        var buffer = new byte[2048];
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
        int received = await socket.ReceiveAsync(buffer, SocketFlags.None, timeoutCts.Token);
        if (received <= 0)
        {
            return ([], 0);
        }

        var response = new byte[received];
        Buffer.BlockCopy(buffer, 0, response, 0, received);
        return ParseDnsResponse(response, queryType);
    }



    private static async Task<(IPAddress[] Addresses, long MinTtl)> QueryDohRecordAsync(string host, string type, CancellationToken cancellationToken)
    {
        ushort queryType = type switch
        {
            "A" => 1,
            "AAAA" => 28,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        byte[] queryMessage = BuildDnsQueryMessage(host, queryType);
        string dnsParam = Convert.ToBase64String(queryMessage).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var endpoint = GetProviderEndpoint(_provider);
        string url = $"{endpoint.DohEndpoint}?dns={dnsParam}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/dns-message");

        using var response = await _dohHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        byte[] responseMessage = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return ParseDnsResponse(responseMessage, queryType);
    }



    private static byte[] BuildDnsQueryMessage(string host, ushort queryType)
    {
        var labels = host.Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0)
        {
            throw new ArgumentException("Invalid host.", nameof(host));
        }

        int qnameLength = labels.Sum(x => x.Length + 1) + 1;
        byte[] message = new byte[12 + qnameLength + 4];
        ushort id = (ushort)Random.Shared.Next(ushort.MaxValue + 1);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(0, 2), id);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(2, 2), 0x0100);
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(4, 2), 1);

        int offset = 12;
        foreach (var label in labels)
        {
            int byteCount = Encoding.ASCII.GetByteCount(label);
            if (byteCount == 0 || byteCount > 63)
            {
                throw new ArgumentException("Invalid host.", nameof(host));
            }

            message[offset++] = (byte)byteCount;
            Encoding.ASCII.GetBytes(label, message.AsSpan(offset, byteCount));
            offset += byteCount;
        }

        message[offset++] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(offset, 2), queryType);
        offset += 2;
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(offset, 2), 1);

        return message;
    }



    private static (IPAddress[] Addresses, long MinTtl) ParseDnsResponse(byte[] message, ushort expectedType)
    {
        if (message.Length < 12)
        {
            return ([], 0);
        }

        ushort flags = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(2, 2));
        int rcode = flags & 0x000F;
        if (rcode != 0)
        {
            return ([], 0);
        }

        ushort questionCount = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(4, 2));
        ushort answerCount = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(6, 2));

        int offset = 12;
        for (int i = 0; i < questionCount; i++)
        {
            if (!TrySkipDnsName(message, ref offset) || offset + 4 > message.Length)
            {
                return ([], 0);
            }

            offset += 4;
        }

        List<IPAddress> addresses = [];
        long minTtl = 0;

        for (int i = 0; i < answerCount; i++)
        {
            if (!TrySkipDnsName(message, ref offset) || offset + 10 > message.Length)
            {
                return (addresses.ToArray(), minTtl);
            }

            ushort type = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset, 2));
            offset += 2;
            _ = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset, 2));
            offset += 2;
            uint ttl = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(offset, 4));
            offset += 4;
            ushort rdLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset, 2));
            offset += 2;

            if (offset + rdLength > message.Length)
            {
                return (addresses.ToArray(), minTtl);
            }

            if (type == expectedType)
            {
                if (type == 1 && rdLength == 4)
                {
                    addresses.Add(new IPAddress(message.AsSpan(offset, 4)));
                    minTtl = minTtl == 0 ? ttl : Math.Min(minTtl, ttl);
                }
                else if (type == 28 && rdLength == 16)
                {
                    addresses.Add(new IPAddress(message.AsSpan(offset, 16)));
                    minTtl = minTtl == 0 ? ttl : Math.Min(minTtl, ttl);
                }
            }

            offset += rdLength;
        }

        return (addresses.ToArray(), minTtl);
    }



    private static bool TrySkipDnsName(byte[] message, ref int offset)
    {
        int index = offset;
        while (index < message.Length)
        {
            byte length = message[index];

            if (length == 0)
            {
                index++;
                offset = index;
                return true;
            }

            if ((length & 0xC0) == 0xC0)
            {
                if (index + 1 >= message.Length)
                {
                    return false;
                }

                index += 2;
                offset = index;
                return true;
            }

            index++;
            if (index + length > message.Length)
            {
                return false;
            }

            index += length;
        }

        return false;
    }



    private static IPAddress[] ParseIpAddresses(params string[] rawAddresses)
    {
        var list = new List<IPAddress>();
        foreach (var raw in rawAddresses)
        {
            if (IPAddress.TryParse(raw, out var ip))
            {
                list.Add(ip);
            }
        }
        return list.ToArray();
    }
}
