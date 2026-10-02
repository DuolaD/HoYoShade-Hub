using HoYoShadeHub.Core.Metadata.Github;
using HoYoShadeHub.Core.Networking;
using HoYoShadeHub.Helpers;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HoYoShadeHub.Core.Metadata;

public class MetadataClient
{


    private const string API_PREFIX_CLOUDFLARE = "https://cdn.autumn.recipe.2dcd.cf.storage.hub.hoyosha.de/https://raw.githubusercontent.com/DuolaD/HoYoShade-Hub/metadata";

    private const string API_PREFIX_GITHUB = "https://raw.githubusercontent.com/DuolaD/HoYoShade-Hub/metadata";

    private const string API_PREFIX_JSDELIVR = "https://cdn.jsdelivr.net/gh/DuolaD/HoYoShade-Hub@metadata";


    private string API_PREFIX = API_PREFIX_CLOUDFLARE;

#if DEV
    private const string API_VERSION = "dev";
#else
    private const string API_VERSION = "v1";
#endif


    private readonly HttpClient _httpClient;


    public MetadataClient(int apiIndex = 0, HttpClient? httpClient = null)
    {
        SetApiPrefix(apiIndex);
        _httpClient = httpClient ?? new HttpClient(DohService.CreateSocketsHttpHandler()) { DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher };
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HoYoShadeHub");
        }
    }



    public void SetApiPrefix(int index)
    {
        API_PREFIX = index switch
        {
            1 => API_PREFIX_GITHUB,
            2 => API_PREFIX_JSDELIVR,
            _ => API_PREFIX_CLOUDFLARE,
        };
    }



    private async Task<T> CommonGetAsync<T>(string url, CancellationToken cancellationToken = default) where T : class
    {
        var res = await _httpClient.GetFromJsonAsync(url, typeof(T), MetadataJsonContext.Default, cancellationToken) as T;
        if (res == null)
        {
            throw new NullReferenceException("");
        }
        else
        {
            return res;
        }
    }




    private string GetUrl(string suffix)
    {
        return $"{API_PREFIX}/{API_VERSION}/{suffix}";
    }




    public async Task<ReleaseVersion> GetVersionAsync(bool isPrerelease, Architecture architecture, CancellationToken cancellationToken = default)
    {
#if DEV
        isPrerelease = true;
#endif
        var name = (isPrerelease, architecture) switch
        {
            (false, Architecture.X64) => "version_stable_x64.json",
            (true, Architecture.X64) => "version_preview_x64.json",
            (false, Architecture.Arm64) => "version_stable_arm64.json",
            (true, Architecture.Arm64) => "version_preview_arm64.json",
            _ => throw new PlatformNotSupportedException($"{architecture} is not supported."),
        };
        var url = GetUrl(name);
        return await CommonGetAsync<ReleaseVersion>(url, cancellationToken);
    }



    public async Task<ReleaseVersion> GetReleaseAsync(bool isPrerelease, Architecture architecture, CancellationToken cancellationToken = default)
    {
#if DEV
        isPrerelease = true;
#endif
        var name = (isPrerelease, architecture) switch
        {
            (false, Architecture.X64) => "release_stable_x64.json",
            (true, Architecture.X64) => "release_preview_x64.json",
            (false, Architecture.Arm64) => "release_stable_arm64.json",
            (true, Architecture.Arm64) => "release_preview_arm64.json",
            _ => throw new PlatformNotSupportedException($"{architecture} is not supported."),
        };
        var url = GetUrl(name);
        return await CommonGetAsync<ReleaseVersion>(url, cancellationToken);
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
