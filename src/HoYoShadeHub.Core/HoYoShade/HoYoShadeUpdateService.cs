using HoYoShadeHub.Core.Metadata.Github;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Helpers;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Core.HoYoShade;

/// <summary>
/// HoYoShade 更新检测服务
/// </summary>
public class HoYoShadeUpdateService
{
    private readonly HoYoShadeVersionService _versionService;

    private static readonly HashSet<string> HiddenIncompatibleVersionTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "3.0.0-beta.1",
        "3.0.0-beta.2",
    };

    public HoYoShadeUpdateService(HoYoShadeVersionService versionService)
    {
        _versionService = versionService;
    }

    /// <summary>
    /// 检查 HoYoShade 更新（使用默认自动选择回落）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckHoYoShadeUpdateAsync(bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        return await CheckHoYoShadeUpdateAsync(includePrerelease, -1, cancellationToken);
    }

    /// <summary>
    /// 检查 HoYoShade 更新（支持指定下载服务器或自动选择回落）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="serverIndex">下载服务器索引（-1=自动选择，0=GitHub直连，1=Cloudflare，2=腾讯云，3=阿里云）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckHoYoShadeUpdateAsync(bool includePrerelease, int serverIndex, CancellationToken cancellationToken = default)
    {
        var currentVersion = await _versionService.GetHoYoShadeVersionAsync();
        if (currentVersion == null)
        {
            return null;
        }

        var latestRelease = await GetLatestReleaseAsync(includePrerelease, serverIndex, cancellationToken);
        if (latestRelease == null)
        {
            return null;
        }

        // Compare versions
        if (CompareVersions(latestRelease.TagName, currentVersion.Version) > 0)
        {
            return latestRelease;
        }

        return null;
    }

    /// <summary>
    /// 检查 HoYoShade 更新（使用指定的代理URL）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="proxyUrl">代理URL（如果为null则使用自动选择回落）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckHoYoShadeUpdateAsync(bool includePrerelease, string? proxyUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return await CheckHoYoShadeUpdateAsync(includePrerelease, -1, cancellationToken);
        }

        var currentVersion = await _versionService.GetHoYoShadeVersionAsync();
        if (currentVersion == null)
        {
            return null;
        }

        var latestRelease = await GetLatestReleaseAsync(includePrerelease, proxyUrl, cancellationToken);
        if (latestRelease == null)
        {
            return null;
        }

        // Compare versions
        if (CompareVersions(latestRelease.TagName, currentVersion.Version) > 0)
        {
            return latestRelease;
        }

        return null;
    }

    /// <summary>
    /// 检查 OpenHoYoShade 更新（使用默认自动选择回落）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckOpenHoYoShadeUpdateAsync(bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        return await CheckOpenHoYoShadeUpdateAsync(includePrerelease, -1, cancellationToken);
    }

    /// <summary>
    /// 检查 OpenHoYoShade 更新（支持指定下载服务器或自动选择回落）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="serverIndex">下载服务器索引（-1=自动选择，0=GitHub直连，1=Cloudflare，2=腾讯云，3=阿里云）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckOpenHoYoShadeUpdateAsync(bool includePrerelease, int serverIndex, CancellationToken cancellationToken = default)
    {
        var currentVersion = await _versionService.GetOpenHoYoShadeVersionAsync();
        if (currentVersion == null)
        {
            return null;
        }

        var latestRelease = await GetLatestReleaseAsync(includePrerelease, serverIndex, cancellationToken);
        if (latestRelease == null)
        {
            return null;
        }

        // Compare versions
        if (CompareVersions(latestRelease.TagName, currentVersion.Version) > 0)
        {
            return latestRelease;
        }

        return null;
    }

    /// <summary>
    /// 检查 OpenHoYoShade 更新（使用指定的代理URL）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="proxyUrl">代理URL（如果为null则使用自动选择回落）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckOpenHoYoShadeUpdateAsync(bool includePrerelease, string? proxyUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            return await CheckOpenHoYoShadeUpdateAsync(includePrerelease, -1, cancellationToken);
        }

        var currentVersion = await _versionService.GetOpenHoYoShadeVersionAsync();
        if (currentVersion == null)
        {
            return null;
        }

        var latestRelease = await GetLatestReleaseAsync(includePrerelease, proxyUrl, cancellationToken);
        if (latestRelease == null)
        {
            return null;
        }

        // Compare versions
        if (CompareVersions(latestRelease.TagName, currentVersion.Version) > 0)
        {
            return latestRelease;
        }

        return null;
    }

    /// <summary>
    /// 从 GitHub 获取发布版本列表（支持指定下载服务器或自动选择回落）
    /// </summary>
    /// <param name="serverIndex">下载服务器索引（-1=自动选择，0=GitHub直连，1=Cloudflare，2=腾讯云，3=阿里云）</param>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<List<GithubRelease>> GetReleasesAsync(int serverIndex = -1, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

        string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(false)
            : new[] { serverIndex };

        GithubRelease[]? releases = null;
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

                string currentApiUrl = string.IsNullOrWhiteSpace(proxyUrl)
                    ? apiUrl
                    : CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);

                try
                {
                    releases = await client.GetFromJsonAsync<GithubRelease[]>(currentApiUrl, cancellationToken);
                    if (releases != null && releases.Length > 0)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                    // Proceed to next proxy / server
                }
            }

            if (releases != null && releases.Length > 0)
            {
                break;
            }
        }

        if (releases == null || releases.Length == 0)
        {
            throw lastFallbackException ?? new HttpRequestException("Failed to fetch release list from all fallback servers.");
        }

        var validReleases = releases
            .Where(r => IsVersionV3OrAbove(r.TagName))
            .Where(r => !IsHiddenIncompatibleVersionTag(r.TagName))
            .Where(r => includePrerelease || !r.Prerelease)
            .OrderByDescending(r => r.PublishedAt)
            .ToList();

        return validReleases;
    }

    /// <summary>
    /// 从 GitHub 获取最新版本（支持指定下载服务器或自动选择回落）
    /// </summary>
    public async Task<GithubRelease?> GetLatestReleaseAsync(bool includePrerelease = false, int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        var releases = await GetReleasesAsync(serverIndex, includePrerelease, cancellationToken);
        return releases.FirstOrDefault();
    }

    /// <summary>
    /// 获取资源对应的 SHA256 校验码字符串（支持多代理回落与 403 规避）
    /// </summary>
    public async Task<string> FetchAssetSha256Async(string sha256Url, int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(false)
            : new[] { serverIndex };

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
                    ? sha256Url
                    : CloudProxyManager.ApplyProxy(sha256Url, proxyUrl);

                try
                {
                    var response = await client.GetStringAsync(currentUrl, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        return response.Trim().Split(' ')[0];
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                }
            }
        }

        throw lastFallbackException ?? new HttpRequestException("Failed to fetch SHA256 from all fallback servers.");
    }

    /// <summary>
    /// 获取框架版本发布说明 Markdown（支持多代理回落与 403 规避）
    /// </summary>
    public async Task<string?> FetchFrameworkChangelogMarkdownAsync(string tagName, int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub/1.0");

        string apiUrl = $"https://api.github.com/repos/DuolaD/HoYoShade/releases/tags/{tagName}";
        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(false)
            : new[] { serverIndex };

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

                string currentApiUrl = string.IsNullOrWhiteSpace(proxyUrl)
                    ? apiUrl
                    : CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);

                try
                {
                    var response = await client.GetStringAsync(currentApiUrl, cancellationToken);
                    var release = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);

                    var name = release.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : tagName;
                    var body = release.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"# {name ?? tagName}");
                    sb.AppendLine();
                    sb.AppendLine(body ?? "");
                    return sb.ToString();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                }
            }
        }

        throw lastFallbackException ?? new HttpRequestException($"Failed to fetch changelog for {tagName} from all fallback servers.");
    }

    /// <summary>
    /// 获取指定 Tag 的发布时间（支持多代理回落与 403 规避）
    /// </summary>
    public async Task<DateTimeOffset?> FetchFrameworkReleaseTimeAsync(string tagName, int serverIndex = -1, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub/1.0");

        string apiUrl = $"https://api.github.com/repos/DuolaD/HoYoShade/releases/tags/{tagName}";
        int[] serverSequence = serverIndex == -1
            ? CloudProxyManager.GetAutoSelectFallbackSequence(false)
            : new[] { serverIndex };

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

                string currentApiUrl = string.IsNullOrWhiteSpace(proxyUrl)
                    ? apiUrl
                    : CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);

                try
                {
                    var response = await client.GetStringAsync(currentApiUrl, cancellationToken);
                    var release = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);

                    if (release.TryGetProperty("published_at", out var pubProp))
                    {
                        var pubStr = pubProp.GetString();
                        if (!string.IsNullOrEmpty(pubStr) && DateTimeOffset.TryParse(pubStr, out var dt))
                        {
                            return dt;
                        }
                    }
                }
                catch
                {
                    // Continue to next proxy / server
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 从 GitHub 获取最新版本（使用指定的单个代理URL）
    /// </summary>
    private async Task<GithubRelease?> GetLatestReleaseAsync(bool includePrerelease, string proxyUrl, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(DohService.CreateSocketsHttpHandler())
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

        string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
        apiUrl = CloudProxyManager.ApplyProxy(apiUrl, proxyUrl);

        var releases = await client.GetFromJsonAsync<GithubRelease[]>(apiUrl, cancellationToken);
        if (releases == null || releases.Length == 0)
        {
            return null;
        }

        var validReleases = releases
            .Where(r => IsVersionV3OrAbove(r.TagName))
            .Where(r => !IsHiddenIncompatibleVersionTag(r.TagName))
            .Where(r => includePrerelease || !r.Prerelease)
            .OrderByDescending(r => r.PublishedAt)
            .ToList();

        return validReleases.FirstOrDefault();
    }

    /// <summary>
    /// 检查版本是否为 V3 或以上
    /// </summary>
    public static bool IsVersionV3OrAbove(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        string versionStr = tagName.TrimStart('v', 'V').Trim();
        var parts = versionStr.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out int majorVersion))
        {
            return majorVersion >= 3;
        }

        return false;
    }

    /// <summary>
    /// 检查是否为已知不兼容的隐藏版本
    /// </summary>
    public static bool IsHiddenIncompatibleVersionTag(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        string normalizedTag = tagName.Trim();
        if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTag = normalizedTag[1..];
        }

        return HiddenIncompatibleVersionTags.Contains(normalizedTag);
    }

    /// <summary>
    /// 比较两个版本号 (使用 NuGetVersion 提供的标准语义化版本比较)
    /// 支持所有标准语义化版本格式，例如:
    /// - 标准版本: 3.0.1, 3.1.0
    /// - 预发布版本: 3.0.0-Beta.1, 3.0.0-Alpha.2, 3.0.0-RC.1
    /// - 构建元数据: 3.0.0+build.123
    /// - 组合格式: 3.0.0-Beta.1+build.456
    /// </summary>
    /// <param name="version1">版本1，例如 "V3.0.1" 或 "V3.0.0-Beta.1"）</param>
    /// <param name="version2">版本2，例如 "V3.0.0" 或 "V3.0.0-Beta.2"）</param>
    /// <returns>如果 version1 > version2 返回正数；相等返回 0；小于返回负数</returns>
    public static int CompareVersions(string? version1, string? version2)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(version1) && string.IsNullOrWhiteSpace(version2))
            {
                return 0;
            }
            if (string.IsNullOrWhiteSpace(version1))
            {
                return -1;
            }
            if (string.IsNullOrWhiteSpace(version2))
            {
                return 1;
            }

            // 移除 'v' 或 'V' 前缀
            string v1 = version1.TrimStart('v', 'V').Trim();
            string v2 = version2.TrimStart('v', 'V').Trim();

            // 使用 NuGetVersion 解析版本号
            // NuGetVersion 完全支持语义化版本规范 (SemVer 2.0):
            // - 正确处理主版本、次版本、修订版本的数字比较
            // - 预发布标识符的分段字典比较 (Beta.1 < Beta.2 < Beta.10)
            // - 正式版 > 预发布版 (3.0.0 > 3.0.0-Beta.1)
            // - 预发布标识符的优先级 (Alpha < Beta < RC < 正式版)
            if (NuGetVersion.TryParse(v1, out var nugetV1) && NuGetVersion.TryParse(v2, out var nugetV2))
            {
                return nugetV1.CompareTo(nugetV2);
            }

            // 如果 NuGetVersion 无法解析,使用数字比较
            var parts1 = v1.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
            var parts2 = v2.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();

            int maxLength = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;

                if (p1 != p2)
                {
                    return p1.CompareTo(p2);
                }
            }

            return 0;
        }
        catch
        {
            // 版本比较失败,假定两者相等
            return 0;
        }
    }
}
