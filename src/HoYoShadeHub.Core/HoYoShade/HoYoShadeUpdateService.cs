using HoYoShadeHub.Core.Metadata.Github;
using NuGet.Versioning;
using System;
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

    public HoYoShadeUpdateService(HoYoShadeVersionService versionService)
    {
        _versionService = versionService;
    }

    /// <summary>
    /// 检查 HoYoShade 更新
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckHoYoShadeUpdateAsync(bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        return await CheckHoYoShadeUpdateAsync(includePrerelease, null, cancellationToken);
    }

    /// <summary>
    /// 检查 HoYoShade 更新（使用指定的代理URL）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="proxyUrl">代理URL（如果为null则使用GitHub直连）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckHoYoShadeUpdateAsync(bool includePrerelease = false, string? proxyUrl = null, CancellationToken cancellationToken = default)
    {
        try
        {
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
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 检查 OpenHoYoShade 更新
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckOpenHoYoShadeUpdateAsync(bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        return await CheckOpenHoYoShadeUpdateAsync(includePrerelease, null, cancellationToken);
    }

    /// <summary>
    /// 检查 OpenHoYoShade 更新（使用指定的代理URL）
    /// </summary>
    /// <param name="includePrerelease">是否包含预览版</param>
    /// <param name="proxyUrl">代理URL（如果为null则使用GitHub直连）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果有更新则返回最新版本信息，否则返回 null</returns>
    public async Task<GithubRelease?> CheckOpenHoYoShadeUpdateAsync(bool includePrerelease = false, string? proxyUrl = null, CancellationToken cancellationToken = default)
    {
        try
        {
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
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 从 GitHub 获取最新版本（使用指定的代理URL）
    /// </summary>
    private async Task<GithubRelease?> GetLatestReleaseAsync(bool includePrerelease, string? proxyUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

            string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
            
            // Apply proxy if provided
            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                apiUrl = $"{proxyUrl}/{apiUrl}";
            }
            
            var releases = await client.GetFromJsonAsync<GithubRelease[]>(apiUrl, cancellationToken);

            if (releases == null || releases.Length == 0)
            {
                return null;
            }

            // Filter and find latest version
            var validReleases = releases
                .Where(r => IsVersionV3OrAbove(r.TagName))
                .Where(r => includePrerelease || !r.Prerelease)
                .OrderByDescending(r => r.PublishedAt)
                .ToList();

            return validReleases.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 检查版本是否为 V3 或以上
    /// </summary>
    private bool IsVersionV3OrAbove(string tagName)
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
    private int CompareVersions(string version1, string version2)
    {
        try
        {
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
