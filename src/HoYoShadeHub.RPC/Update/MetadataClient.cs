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
            _httpClient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
            _httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
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
        return await CommonGetAsync<ReleaseManifest>(url, cancellationToken);
    }



    public async Task<ReleaseManifest> GetReleaseManifestAsync(string version, Architecture arch, InstallType type, CancellationToken cancellationToken = default)
    {
        string url = GetUrl($"manifest/manifest_{version}_{arch}_{type}.json".ToLower());
        return await CommonGetAsync<ReleaseManifest>(url, cancellationToken);
    }




    #region Github



    public async Task<GithubRelease?> GetGithubLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        const string url = "https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases?page=1&per_page=1";
        var list = await CommonGetAsync<List<GithubRelease>>(url, cancellationToken);
        return list?.FirstOrDefault();
    }



    public async Task<List<GithubRelease>> GetGithubReleaseAsync(int page, int perPage, CancellationToken cancellationToken = default)
    {
        string url = $"https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases?page={page}&per_page={perPage}";
        var list = await CommonGetAsync<List<GithubRelease>>(url, cancellationToken);
        return list ?? new List<GithubRelease>();
    }



    public async Task<GithubRelease?> GetGithubReleaseAsync(string tag, CancellationToken cancellationToken = default)
    {
        string url = $"https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases/tags/{tag}";
        return await CommonGetAsync<GithubRelease>(url, cancellationToken);
    }


    public async Task<string> RenderGithubMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
    {
        const string url = "https://api.github.com/markdown";
        var request = new GithubMarkdownRequest
        {
            Text = markdown,
            Mode = "gfm",
            Context = "DuolaD/HoYoShade-Hub",
        };
        var content = new StringContent(JsonSerializer.Serialize(request, typeof(GithubMarkdownRequest), MetadataJsonContext.Default), new MediaTypeHeaderValue("application/json"));
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }



    #endregion



}
