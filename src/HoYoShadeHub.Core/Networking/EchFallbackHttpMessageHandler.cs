using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Core.Networking;

public class EchFallbackHttpMessageHandler : DelegatingHandler
{
    public EchFallbackHttpMessageHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (DohService.EnableEch && request.RequestUri != null)
        {
            var host = request.RequestUri.Host;
            bool isEchSupported = false;
            try
            {
                isEchSupported = await DohService.DetectEchSupportAsync(host, cancellationToken);
            }
            catch
            {
                // Ignored
            }

            if (isEchSupported)
            {
                try
                {
                    var response = await CurlExecuteAsync(request, cancellationToken);
                    if (response != null)
                    {
                        return response;
                    }
                }
                catch
                {
                    // Ignored
                }
            }
        }

        // Default path: standard TLS
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage?> CurlExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string curlPath = Path.Combine(AppContext.BaseDirectory, "curl.exe");
        if (!File.Exists(curlPath))
        {
            return null;
        }

        var url = request.RequestUri!.ToString();
        var dohUrl = DohService.GetCurrentDohUrl();
        var argsBuilder = new StringBuilder();
        argsBuilder.Append("--ech true ");
        if (!string.IsNullOrWhiteSpace(dohUrl))
        {
            argsBuilder.Append($"--doh-url \"{dohUrl}\" ");
        }
        
        argsBuilder.Append("-s -L -f ");

        foreach (var header in request.Headers)
        {
            foreach (var val in header.Value)
            {
                argsBuilder.Append($"-H \"{header.Key}: {val}\" ");
            }
        }

        string? tempFile = null;
        if (request.Method == HttpMethod.Post && request.Content != null)
        {
            string postData = await request.Content.ReadAsStringAsync(cancellationToken);
            tempFile = Path.Combine(Path.GetTempPath(), $"curl_post_{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(tempFile, postData, Encoding.UTF8, cancellationToken);
            
            if (request.Content.Headers.ContentType != null)
            {
                argsBuilder.Append($"-H \"Content-Type: {request.Content.Headers.ContentType}\" ");
            }
            argsBuilder.Append($"-d @\"{tempFile}\" ");
        }
        else if (request.Method != HttpMethod.Get)
        {
            argsBuilder.Append($"-X {request.Method.Method} ");
        }

        argsBuilder.Append($"\"{url}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = curlPath,
            Arguments = argsBuilder.ToString(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using (var registration = cancellationToken.Register(() => { try { process.Kill(); } catch { } }))
            {
                await process.WaitForExitAsync(cancellationToken);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                return null;
            }

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stdout, Encoding.UTF8)
            };
            return responseMessage;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }
}
