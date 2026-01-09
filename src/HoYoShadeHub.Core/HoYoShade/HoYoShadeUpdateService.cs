using HoYoShadeHub.Core.Metadata.Github;
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
        try
        {
            var currentVersion = await _versionService.GetHoYoShadeVersionAsync();
            if (currentVersion == null)
            {
                return null;
            }

            var latestRelease = await GetLatestReleaseAsync(includePrerelease, cancellationToken);
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
        try
        {
            var currentVersion = await _versionService.GetOpenHoYoShadeVersionAsync();
            if (currentVersion == null)
            {
                return null;
            }

            var latestRelease = await GetLatestReleaseAsync(includePrerelease, cancellationToken);
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
    /// 从 GitHub 获取最新版本
    /// </summary>
    private async Task<GithubRelease?> GetLatestReleaseAsync(bool includePrerelease, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");

            string apiUrl = "https://api.github.com/repos/DuolaD/HoYoShade/releases";
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
    /// 比较两个版本号
    /// </summary>
    /// <param name="version1">版本1（例如 "V3.0.1" 或 "V3.0.0-Beta.1"）</param>
    /// <param name="version2">版本2（例如 "V3.0.0" 或 "V3.0.0-Beta.2"）</param>
    /// <returns>如果 version1 > version2 返回正数，相等返回 0，小于返回负数</returns>
    private int CompareVersions(string version1, string version2)
    {
        try
        {
            // Parse versions
            var v1 = ParseVersion(version1);
            var v2 = ParseVersion(version2);

            // Compare main version parts (major.minor.patch)
            for (int i = 0; i < 3; i++)
            {
                if (v1.MainParts[i] != v2.MainParts[i])
                {
                    return v1.MainParts[i].CompareTo(v2.MainParts[i]);
                }
            }

            // Main versions are equal, compare prerelease
            // Non-prerelease > prerelease (e.g., 3.0.0 > 3.0.0-Beta.1)
            if (!v1.IsPrerelease && v2.IsPrerelease) return 1;
            if (v1.IsPrerelease && !v2.IsPrerelease) return -1;

            // Both are prerelease or both are not, compare prerelease parts
            if (v1.IsPrerelease && v2.IsPrerelease)
            {
                // Compare prerelease identifier (alpha < beta < rc)
                int identifierCompare = string.Compare(v1.PrereleaseIdentifier, v2.PrereleaseIdentifier, StringComparison.OrdinalIgnoreCase);
                if (identifierCompare != 0)
                {
                    return identifierCompare;
                }

                // Same identifier, compare version number
                return v1.PrereleaseVersion.CompareTo(v2.PrereleaseVersion);
            }

            return 0;
        }
        catch
        {
            // If comparison fails, assume they are equal
            return 0;
        }
    }

    /// <summary>
    /// 解析版本号
    /// </summary>
    private VersionInfo ParseVersion(string version)
    {
        var info = new VersionInfo();

        if (string.IsNullOrWhiteSpace(version))
        {
            return info;
        }

        // Remove V prefix
        version = version.TrimStart('v', 'V').Trim();

        // Split main version and prerelease
        string[] parts = version.Split('-', 2);
        string mainVersion = parts[0];
        string? prerelease = parts.Length > 1 ? parts[1] : null;

        // Parse main version (major.minor.patch)
        var mainParts = mainVersion.Split('.').Select(p => int.TryParse(p, out int n) ? n : 0).ToArray();
        for (int i = 0; i < Math.Min(3, mainParts.Length); i++)
        {
            info.MainParts[i] = mainParts[i];
        }

        // Parse prerelease
        if (!string.IsNullOrWhiteSpace(prerelease))
        {
            info.IsPrerelease = true;

            // Extract identifier and version (e.g., "Beta.1" -> identifier="Beta", version=1)
            var prereleaseParts = prerelease.Split('.');
            if (prereleaseParts.Length > 0)
            {
                info.PrereleaseIdentifier = prereleaseParts[0].ToLowerInvariant();
            }
            if (prereleaseParts.Length > 1 && int.TryParse(prereleaseParts[1], out int prereleaseVer))
            {
                info.PrereleaseVersion = prereleaseVer;
            }
        }

        return info;
    }

    /// <summary>
    /// 版本信息结构
    /// </summary>
    private class VersionInfo
    {
        public int[] MainParts { get; set; } = new int[3]; // major, minor, patch
        public bool IsPrerelease { get; set; }
        public string PrereleaseIdentifier { get; set; } = "";
        public int PrereleaseVersion { get; set; }
    }

    /// <summary>
    /// 标准化版本号（移除 V 前缀和预览版标签）
    /// </summary>
    private string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        // Remove V prefix
        version = version.TrimStart('v', 'V');

        // Remove prerelease tags (e.g., -beta.1)
        int dashIndex = version.IndexOf('-');
        if (dashIndex > 0)
        {
            version = version.Substring(0, dashIndex);
        }

        return version.Trim();
    }
}
