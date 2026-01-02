using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Toolbox;

/// <summary>
/// HTTP 时间同步服务（使用 Cloudflare CDN trace API）
/// </summary>
public class HttpTimeSyncService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

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
    /// 常用的 Cloudflare trace API 端点列表
    /// </summary>
    public static readonly string[] TraceEndpoints = new[]
    {
        "https://www.cloudflare.com/cdn-cgi/trace",
        "https://www.wto.org/cdn-cgi/trace",
        "https://www.canva.cn/cdn-cgi/trace"
    };

    /// <summary>
    /// 从 Cloudflare trace API 获取网络时间
    /// </summary>
    /// <param name="endpoint">trace API 端点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>获取到的网络时间（UTC）</returns>
    public static async Task<DateTime> GetNetworkTimeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync(endpoint, cancellationToken);
        
        // 解析响应，查找 ts= 行
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tsLine = lines.FirstOrDefault(line => line.StartsWith("ts="));
        
        if (tsLine == null)
        {
            throw new InvalidOperationException("Failed to parse timestamp from trace API response");
        }

        // 提取时间戳值（格式：ts=1767344320.000）
        var tsValue = tsLine.Substring(3); // 移除 "ts="
        
        if (!double.TryParse(tsValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double unixTimestamp))
        {
            throw new InvalidOperationException($"Failed to parse timestamp value: {tsValue}");
        }

        // 将 Unix 时间戳转换为 DateTime（UTC）
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(unixTimestamp);
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
    /// 同步系统时间（使用 HTTP trace API）
    /// </summary>
    /// <param name="endpoint">trace API 端点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步后的本地时间</returns>
    public static async Task<DateTime> SyncSystemTimeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var httpTime = await GetNetworkTimeAsync(endpoint, cancellationToken);
        
        if (!SetSystemTimeUtc(httpTime))
        {
            throw new InvalidOperationException("Failed to set system time. Please run as administrator.");
        }

        return httpTime.ToLocalTime();
    }

    /// <summary>
    /// 尝试从多个端点获取时间（自动回退）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>获取到的网络时间（UTC）</returns>
    public static async Task<DateTime> GetNetworkTimeWithFallbackAsync(CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        foreach (var endpoint in TraceEndpoints)
        {
            try
            {
                return await GetNetworkTimeAsync(endpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                // 继续尝试下一个端点
            }
        }

        throw new InvalidOperationException(
            "Failed to get time from all trace endpoints", 
            lastException);
    }

    /// <summary>
    /// 同步系统时间（自动回退到多个端点）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步后的本地时间</returns>
    public static async Task<DateTime> SyncSystemTimeWithFallbackAsync(CancellationToken cancellationToken = default)
    {
        var httpTime = await GetNetworkTimeWithFallbackAsync(cancellationToken);
        
        if (!SetSystemTimeUtc(httpTime))
        {
            throw new InvalidOperationException("Failed to set system time. Please run as administrator.");
        }

        return httpTime.ToLocalTime();
    }
}
