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
        argsBuilder.Append("--ech true --ca-native ");
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

        Process? process = null;
        try
        {
            process = new Process { StartInfo = startInfo };

            if (!process.Start())
            {
                process.Dispose();
                return null;
            }

            // Consume stderr asynchronously in background to prevent blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    using var reader = process.StandardError;
                    var buffer = new char[1024];
                    while (await reader.ReadAsync(buffer, 0, buffer.Length) > 0) {}
                }
                catch {}
            });

            // Wait briefly to see if it fails/exits immediately
            await Task.Delay(50, cancellationToken);
            if (process.HasExited && process.ExitCode != 0)
            {
                process.Dispose();
                return null;
            }

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ProcessStream(process.StandardOutput.BaseStream, process, tempFile, cancellationToken))
            };
            return responseMessage;
        }
        catch
        {
            if (process != null)
            {
                try { if (!process.HasExited) process.Kill(); } catch {}
                process.Dispose();
            }
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            return null;
        }
    }
}

public class ProcessStream : Stream
{
    private readonly Stream _innerStream;
    private readonly Process _process;
    private readonly string? _tempFile;
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private bool _disposed;

    public ProcessStream(Stream innerStream, Process process, string? tempFile, CancellationToken cancellationToken)
    {
        _innerStream = innerStream;
        _process = process;
        _tempFile = tempFile;

        if (cancellationToken.CanBeCanceled)
        {
            _cancellationRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch { }
            });
        }
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush() => _innerStream.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _innerStream.ReadAsync(buffer, cancellationToken);
    
    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => _innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cancellationRegistration.Dispose();
                _innerStream.Dispose();
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }
                }
                catch { }
                _process.Dispose();
                if (_tempFile != null && File.Exists(_tempFile))
                {
                    try { File.Delete(_tempFile); } catch { }
                }
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _cancellationRegistration.Dispose();
            await _innerStream.DisposeAsync();
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch { }
            _process.Dispose();
            if (_tempFile != null && File.Exists(_tempFile))
            {
                try { File.Delete(_tempFile); } catch { }
            }
            _disposed = true;
        }
        await base.DisposeAsync();
    }
}
