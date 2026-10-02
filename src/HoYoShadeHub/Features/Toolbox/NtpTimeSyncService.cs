using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Toolbox;

/// <summary>
/// NTP 时间同步服务
/// </summary>
public class NtpTimeSyncService
{
    // Win32 API 结构体，用于设置系统时间
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    // 导入 Win32 API 函数，用于设置系统时间
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemTime(ref SYSTEMTIME time);

    /// <summary>
    /// 常用的 NTP 服务器列表
    /// </summary>
    public static readonly string[] NtpServers = new[]
    {
        "time.windows.com",      // 微软时间服务器（默认）
        "ntp.aliyun.com",        // 阿里时间服务器
        "ntp.ntsc.ac.cn",        // 中科院国家授时中心
        "ntp.tencent.com",       // 腾讯公共 NTP
        "ntp.sjtu.edu.cn",       // 上海交通大学
        "cn.pool.ntp.org",       // 自动 NTP 服务器池
        "ntp.cnnic.cn"           // CNNIC（中国互联网络信息中心）
    };

    /// <summary>
    /// 从 NTP 服务器获取网络时间
    /// </summary>
    /// <param name="ntpServer">NTP 服务器地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>获取到的网络时间（UTC）</returns>
    public static async Task<DateTime> GetNetworkTimeAsync(string ntpServer, CancellationToken cancellationToken = default)
    {
        const int timeoutMs = 5000; // 5秒超时
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeoutMs);

        var ntpData = new byte[48];
        ntpData[0] = 0x1B; // NTP协议版本

        var addresses = await Dns.GetHostEntryAsync(ntpServer, linkedCts.Token);
        if (addresses.AddressList == null || addresses.AddressList.Length == 0)
        {
            throw new InvalidOperationException($"No IP address found for NTP server '{ntpServer}'.");
        }

        // 优先使用 IPv4，若无 IPv4 则使用 IPv6
        var targetIp = addresses.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)
            ?? addresses.AddressList[0];

        var ipEndPoint = new IPEndPoint(targetIp, 123);

        using var socket = new Socket(targetIp.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

        // 连接到 NTP 服务器
        await socket.ConnectAsync(ipEndPoint, linkedCts.Token);

        // 发送 NTP 请求
        await socket.SendAsync(ntpData, SocketFlags.None, linkedCts.Token);

        // 接收 NTP 响应
        await socket.ReceiveAsync(ntpData, SocketFlags.None, linkedCts.Token);

        // 解析 NTP 响应时间
        const byte serverReplyTime = 40;
        uint intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
        uint fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

        intPart = SwapEndianness(intPart);
        fractPart = SwapEndianness(fractPart);

        ulong milliseconds = ((ulong)intPart * 1000) + (((ulong)fractPart * 1000) / 0x100000000L);
        return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);
    }

    /// <summary>
    /// 设置系统时间
    /// </summary>
    /// <param name="newTime">要设置的新时间（UTC）</param>
    /// <returns>是否设置成功</returns>
    public static bool SetSystemTimeUtc(DateTime newTime)
    {
        DateTime utcTime = newTime.ToUniversalTime();

        var sysTime = new SYSTEMTIME
        {
            wYear = (ushort)utcTime.Year,
            wMonth = (ushort)utcTime.Month,
            wDay = (ushort)utcTime.Day,
            wHour = (ushort)utcTime.Hour,
            wMinute = (ushort)utcTime.Minute,
            wSecond = (ushort)utcTime.Second,
            wMilliseconds = (ushort)utcTime.Millisecond
        };

        return SetSystemTime(ref sysTime);
    }

    /// <summary>
    /// 同步系统时间
    /// </summary>
    /// <param name="ntpServer">NTP 服务器地址</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步后的本地时间</returns>
    public static async Task<DateTime> SyncSystemTimeAsync(string ntpServer, CancellationToken cancellationToken = default)
    {
        var ntpTime = await GetNetworkTimeAsync(ntpServer, cancellationToken);
        
        if (!SetSystemTimeUtc(ntpTime))
        {
            throw new InvalidOperationException("Failed to set system time. Please run as administrator.");
        }

        return ntpTime.ToLocalTime();
    }

    /// <summary>
    /// 交换32位无符号整数的字节序（大端转小端）
    /// </summary>
    private static uint SwapEndianness(uint x)
    {
        return ((x & 0x000000ff) << 24) +
               ((x & 0x0000ff00) << 8) +
               ((x & 0x00ff0000) >> 8) +
               ((x & 0xff000000) >> 24);
    }
}
