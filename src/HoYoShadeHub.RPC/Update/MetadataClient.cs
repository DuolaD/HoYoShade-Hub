using HoYoShadeHub.Helpers;
using HoYoShadeHub.RPC.Update.Github;
using HoYoShadeHub.RPC.Update.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.RPC.Update;

public class MetadataClient
{




    private string API_PREFIX = "https://cdn.cf.storage.hub.hoyosha.de/release";
    
    private string? _proxyUrl;



    private readonly HttpClient _httpClient;


    public MetadataClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient(HoYoShadeHub.Core.Networking.DohService.CreateSocketsHttpHandler())
            {
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HoYoShadeHub.RPC");
        }
        else
        {
            _httpClient = httpClient;
        }
    }
    
    /// <summary>
    /// Set proxy URL for API requests (Tencent Cloud or Alibaba Cloud)
    /// </summary>
    /// <param name="proxyUrl">Proxy URL prefix, or null to use Cloudflare direct</param>
    public void SetProxyUrl(string? proxyUrl)
    {
        _proxyUrl = proxyUrl;
    }




    private async Task<T> CommonGetAsync<T>(string url, CancellationToken cancellationToken = default) where T : class
    {
        T? res = await _httpClient.GetFromJsonAsync(url, typeof(T), MetadataJsonContext.Default, cancellationToken) as T;
        if (res is null)
        {
            throw new JsonException($"Cannot deserialize content to type '{typeof(T).FullName}'");
        }
        else
        {
            return res;
        }
    }




    private string GetUrl(string suffix)
    {
        string baseUrl = $"{API_PREFIX}/{suffix}";
        
        // If proxy is set, use it to proxy the Cloudflare API
        if (!string.IsNullOrWhiteSpace(_proxyUrl))
        {
            return $"{_proxyUrl}/{baseUrl}";
        }
        
        return baseUrl;
    }




    public async Task<ReleaseInfoDetail> GetReleaseInfoAsync(bool isPrerelease, Architecture arch, InstallType type, CancellationToken cancellationToken = default)
    {
        var name = isPrerelease switch
        {
            false => "release_info_stable.json",
            true => "release_info_preview.json",
        };
        var releaseInfo = await CommonGetAsync<ReleaseInfo>(GetUrl(name), cancellationToken);
        string key = $"{arch}-{type}".ToLower();
        if (releaseInfo.Releases?.TryGetValue(key, out var value) ?? false)
        {
            return value;
        }
        else
        {
            throw new PlatformNotSupportedException($"Platform ({arch}, {type}) is not supported.");
        }
    }


    public async Task<ReleaseInfoDetail> GetReleaseInfoAsync(string version, Architecture arch, InstallType type, CancellationToken cancellationToken = default)
    {
        string url = GetUrl($"history/release_info_{version}.json");
        var releaseInfo = await CommonGetAsync<ReleaseInfo>(url, cancellationToken);
        string key = $"{arch}-{type}".ToLower();
        if (releaseInfo.Releases?.TryGetValue(key, out var value) ?? false)
        {
            return value;
        }
        else
        {
            throw new PlatformNotSupportedException($"Platform ({arch}, {type}) is not supported.");
        }
    }



    public async Task<ReleaseManifest> GetReleaseManifestAsync(string url, CancellationToken cancellationToken = default)
    {
        // Apply proxy if it's a URL to our CDN
        if (!string.IsNullOrWhiteSpace(_proxyUrl) && url.Contains("cdn.cf.storage.hub.hoyosha.de"))
        {
            url = $"{_proxyUrl}/{url}";
        }
        return await CommonGetAsync<ReleaseManifest>(url, cancellationToken);
    }



    public async Task<ReleaseManifest> GetReleaseManifestAsync(string version, Architecture arch, InstallType type, CancellationToken cancellationToken = default)
    {
        string url = GetUrl($"manifest/manifest_{version}_{arch}_{type}.json".ToLower());
        return await CommonGetAsync<ReleaseManifest>(url, cancellationToken);
    }




    #region Github

    private static int[] GetServerSequence(int serverIndex)
    {
        int[] defaultOrder = new[] { 1, 2, 3, 0 };
        if (serverIndex == -1)
        {
            return defaultOrder;
        }
        return new[] { serverIndex }.Concat(defaultOrder.Where(s => s != serverIndex)).ToArray();
    }

    private static string CleanProxyInjectedScripts(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // GFM output never includes script tags; any script tags present are injected by cloud CDN proxies (e.g. Cloudflare beacon, EdgeOne hooks)
        try
        {
            return System.Text.RegularExpressions.Regex.Replace(
                html,
                @"<script\b[^>]*>[\s\S]*?<\/script>",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        catch
        {
            return html;
        }
    }

    private async Task<T> CommonGetWithFallbackAsync<T>(string url, int serverIndex = -1, CancellationToken cancellationToken = default) where T : class
    {
        int[] serverSequence = GetServerSequence(serverIndex);
        Exception? lastFallbackException = null;

        // If a specific _proxyUrl was set explicitly, try it first
        if (!string.IsNullOrWhiteSpace(_proxyUrl))
        {
            try
            {
                string proxiedUrl = CloudProxyManager.ApplyProxy(url, _proxyUrl);
                return await CommonGetAsync<T>(proxiedUrl, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastFallbackException = ex;
            }
        }

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
                    ? url
                    : CloudProxyManager.ApplyProxy(url, proxyUrl);

                try
                {
                    return await CommonGetAsync<T>(currentUrl, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                }
            }
        }

        throw lastFallbackException ?? new HttpRequestException($"Failed to fetch metadata from {url} across all fallback servers.");
    }

    public Task<GithubRelease?> GetGithubLatestReleaseAsync(CancellationToken cancellationToken = default)
        => GetGithubLatestReleaseAsync(-1, cancellationToken);

    public async Task<GithubRelease?> GetGithubLatestReleaseAsync(int serverIndex, CancellationToken cancellationToken = default)
    {
        try
        {
            const string url = "https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases?page=1&per_page=1";
            var list = await CommonGetWithFallbackAsync<List<GithubRelease>>(url, serverIndex, cancellationToken);
            return list?.FirstOrDefault();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<List<GithubRelease>> GetGithubReleaseAsync(int page, int perPage, CancellationToken cancellationToken = default)
        => GetGithubReleaseAsync(page, perPage, -1, cancellationToken);

    public async Task<List<GithubRelease>> GetGithubReleaseAsync(int page, int perPage, int serverIndex, CancellationToken cancellationToken = default)
    {
        string url = $"https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases?page={page}&per_page={perPage}";
        var list = await CommonGetWithFallbackAsync<List<GithubRelease>>(url, serverIndex, cancellationToken);
        return list ?? new List<GithubRelease>();
    }

    public Task<GithubRelease?> GetGithubReleaseAsync(string tag, CancellationToken cancellationToken = default)
        => GetGithubReleaseAsync(tag, -1, cancellationToken);

    public async Task<GithubRelease?> GetGithubReleaseAsync(string tag, int serverIndex, CancellationToken cancellationToken = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases/tags/{tag}";
            return await CommonGetWithFallbackAsync<GithubRelease>(url, serverIndex, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<string> RenderGithubMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
        => RenderGithubMarkdownAsync(markdown, -1, cancellationToken);

    public async Task<string> RenderGithubMarkdownAsync(string markdown, int serverIndex, CancellationToken cancellationToken = default)
    {
        const string url = "https://api.github.com/markdown";
        var request = new GithubMarkdownRequest
        {
            Text = markdown,
            Mode = "gfm",
            Context = "DuolaD/HoYoShade-Hub",
        };
        var contentString = JsonSerializer.Serialize(request, typeof(GithubMarkdownRequest), MetadataJsonContext.Default);

        int[] serverSequence = GetServerSequence(serverIndex);
        Exception? lastFallbackException = null;

        async Task<string> ExecutePostAsync(string targetUrl)
        {
            using var content = new StringContent(contentString, new MediaTypeHeaderValue("application/json"));
            using var response = await _httpClient.PostAsync(targetUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync(cancellationToken);
            return CleanProxyInjectedScripts(result);
        }

        // If a specific _proxyUrl was set explicitly, try it first
        if (!string.IsNullOrWhiteSpace(_proxyUrl))
        {
            try
            {
                string proxiedUrl = CloudProxyManager.ApplyProxy(url, _proxyUrl);
                return await ExecutePostAsync(proxiedUrl);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastFallbackException = ex;
            }
        }

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
                    ? url
                    : CloudProxyManager.ApplyProxy(url, proxyUrl);

                try
                {
                    return await ExecutePostAsync(currentUrl);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastFallbackException = ex;
                }
            }
        }

        throw lastFallbackException ?? new HttpRequestException("Failed to render markdown via all fallback servers.");
    }

    #endregion



}
