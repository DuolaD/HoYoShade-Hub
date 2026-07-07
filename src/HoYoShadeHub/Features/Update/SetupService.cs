using Microsoft.Extensions.Logging;
using HoYoShadeHub.RPC.Update;
using HoYoShadeHub.RPC.Update.Metadata;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.Update;

internal class SetupService
{

    private readonly ILogger<SetupService> _logger;

    private readonly HttpClient _httpClient;

    private readonly MetadataClient _metadataClient;


    public SetupService(ILogger<SetupService> logger, HttpClient httpClient, MetadataClient metadataClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _metadataClient = metadataClient;
    }


    public long SetupTotalBytes { get; private set; }

    public long SetupDownloadBytes { get; private set; }



    private async Task<ReleaseInfoDetail> GetReleaseInfoDetailAsync(CancellationToken cancellationToken = default)
    {
        return await _metadataClient.GetReleaseInfoAsync(AppConfig.EnablePreviewRelease, RuntimeInformation.ProcessArchitecture, AppConfig.InstallType, cancellationToken);
    }



    public async Task<string?> DownloadSetupAsync(ReleaseInfoDetail? detail, CancellationToken cancellationToken = default)
    {
        detail ??= await GetReleaseInfoDetailAsync(cancellationToken);

        if (detail?.Setup is null)
        {
            return null;
        }

        string setupPath = Path.Combine(AppConfig.CacheFolder, detail.Setup.FileName);
        string url = detail.Setup.Url;
        long size = detail.Setup.Size;
        string hash = detail.Setup.Hash;

        if (File.Exists(setupPath))
        {
            if (await CheckSHA256Async(setupPath, size, hash, cancellationToken))
            {
                return setupPath;
            }
            File.Delete(setupPath);
        }

        SetupTotalBytes = detail.Setup.Size;
        await DownloadFileAsync(setupPath, url, size, hash, cancellationToken);
        return setupPath;
    }


    private async Task DownloadFileAsync(string path, string url, long size, string hash, CancellationToken cancellationToken = default)
    {
        using var fs = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        await DownloadFileAsync(fs, url, size, hash, cancellationToken);
    }


    private async Task DownloadFileAsync(Stream stream, string url, long size, string hash, CancellationToken cancellationToken = default)
    {
        bool success = false;
        for (int i = 0; i < 3; i++)
        {
            SetupDownloadBytes = stream.Length;
            if (stream.Length < SetupTotalBytes)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(stream.Length, null);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentRange?.From is not null)
                {
                    stream.Position = response.Content.Headers.ContentRange.From.Value;
                    SetupDownloadBytes = stream.Position;
                }
                using var hs = await response.Content.ReadAsStreamAsync(cancellationToken);
                int read = 0;
                Memory<byte> buffer = new byte[8192];
                while ((read = await hs.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await stream.WriteAsync(buffer[..read], cancellationToken);
                    SetupDownloadBytes += read;
                }
                SetupDownloadBytes = stream.Length;
            }
            stream.Position = 0;
            if (await CheckSHA256Async(stream, hash, cancellationToken))
            {
                success = true;
                break;
            }
            stream.SetLength(0);
        }
        if (!success)
        {
            throw new Exception("Setup file checksum mismatched.");
        }
    }


    private async Task<bool> CheckSHA256Async(string path, long size, string hash, CancellationToken cancellationToken)
    {
        try
        {
            if (new FileInfo(path).Length != size)
            {
                return false;
            }
            using var fs = File.OpenRead(path);
            return await CheckSHA256Async(fs, hash, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckSHA256Async(Stream stream, string hash, CancellationToken cancellationToken)
    {
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var result = await sha256.ComputeHashAsync(stream, cancellationToken);
            return string.Equals(Convert.ToHexString(result), hash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }


    public async Task UpdateAsync(ReleaseInfoDetail detail, CancellationToken cancellationToken = default)
    {
        string? setupPath = await DownloadSetupAsync(detail, cancellationToken);
        if (setupPath is null || !File.Exists(setupPath))
        {
            throw new NotSupportedException("Update is not supported.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo
        {
            FileName = setupPath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = $"""
                update --InstallFolder "{AppContext.BaseDirectory.TrimEnd('\\')}" --OldVersion "{AppConfig.AppVersion}" --NewVersion "{detail.Version}" --Preview "{AppConfig.EnablePreviewRelease}" --pid {Environment.ProcessId}
                """,
        });
    }

}
