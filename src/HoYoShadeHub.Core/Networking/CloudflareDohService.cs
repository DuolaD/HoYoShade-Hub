using System.Collections.Concurrent;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

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
        ConnectCallback = ConnectWithDohAsync,
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
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
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

        IPAddress[] fallbackAddresses;
        if (string.Equals(host, "cloudflare-dns.com", StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                fallbackAddresses = _cloudflareDohServerAddresses
                    .Where(ip => ip.AddressFamily == secondaryFamily)
                    .ToArray();
            }

            if (fallbackAddresses.Length == 0)
            {
                fallbackAddresses = (await Dns.GetHostAddressesAsync(host, cancellationToken))
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
        ushort queryType = type switch
        {
            "A" => 1,
            "AAAA" => 28,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        byte[] queryMessage = BuildDnsQueryMessage(host, queryType);
        string dnsParam = Convert.ToBase64String(queryMessage).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string url = $"https://cloudflare-dns.com/dns-query?dns={dnsParam}";
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
            int byteCount = System.Text.Encoding.ASCII.GetByteCount(label);
            if (byteCount == 0 || byteCount > 63)
            {
                throw new ArgumentException("Invalid host.", nameof(host));
            }

            message[offset++] = (byte)byteCount;
            System.Text.Encoding.ASCII.GetBytes(label, message.AsSpan(offset, byteCount));
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
}
