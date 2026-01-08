using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.GameLauncher;

/// <summary>
/// Helper class for handling HoYoShade/OpenHoYoShade injector operations and error reporting
/// </summary>
public static class InjectorHelper
{
    /// <summary>
    /// Ready marker that injector outputs to stderr when validation passes
    /// </summary>
    public const string READY_MARKER = "HOYOSHADE_READY:9999";

    /// <summary>
    /// Get user-friendly error message for injector exit code
    /// </summary>
    /// <param name="exitCode">Injector process exit code</param>
    /// <param name="shadeName">Name of the shader framework (HoYoShade or OpenHoYoShade)</param>
    /// <returns>Localized error message</returns>
    public static string GetErrorMessage(int exitCode, string shadeName)
    {
        return exitCode switch
        {
            InjectorErrorCodes.INJECTION_ERROR_FILE_INTEGRITY => 
                string.Format(Lang.Injector_FileIntegrityCheckFailed, shadeName),
            
            InjectorErrorCodes.INJECTION_ERROR_BLACKLIST_PROCESS => 
                string.Format(Lang.Injector_BlacklistProcess, shadeName),
            
            InjectorErrorCodes.INJECTION_ERROR_INVALID_PARAM => 
                string.Format(Lang.Injector_InvalidParameters, shadeName),
            
            InjectorErrorCodes.INJECTION_ERROR_MISSING_EXE_SUFFIX => 
                string.Format(Lang.Injector_MissingExeSuffix, shadeName),
            
            _ => string.Format(Lang.Injector_UnknownError, shadeName, exitCode)
        };
    }

    /// <summary>
    /// Start injector and wait for it to be ready (output READY_MARKER to stderr) or fail with error code.
    /// The injector will continue running in background waiting for the game process.
    /// </summary>
    /// <param name="injectExePath">Path to inject.exe</param>
    /// <param name="gameExeName">Game executable name or shortcut (e.g., -YS)</param>
    /// <param name="shadePath">Working directory for the injector</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="shadeName">Name of the shader framework</param>
    /// <param name="timeoutMs">Timeout in milliseconds to wait for ready marker</param>
    /// <returns>
    /// Tuple of (success, exitCode, process):
    /// - success=true, exitCode=9999, process=running injector process (ready to inject)
    /// - success=false, exitCode=1001-1004, process=null (validation failed)
    /// - success=false, exitCode=-1, process=null (timeout or error)
    /// </returns>
    public static async Task<(bool success, int exitCode, Process? process)> StartAndWaitForReadyAsync(
        string injectExePath,
        string gameExeName,
        string shadePath,
        ILogger logger,
        string shadeName,
        int timeoutMs = 30000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = injectExePath,
                Arguments = gameExeName,
                UseShellExecute = false,
                WorkingDirectory = shadePath,
                CreateNoWindow = true, // Hide console window completely
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            logger.LogInformation("Starting {ShadeName} injector: {InjectPath} {GameExe}",
                shadeName, injectExePath, gameExeName);

            Process? process = Process.Start(startInfo);
            if (process == null)
            {
                logger.LogError("Failed to start {ShadeName} injector process", shadeName);
                return (false, -1, null);
            }

            logger.LogInformation("{ShadeName} injector started (PID: {Pid}), waiting for ready signal...",
                shadeName, process.Id);

            // Wait for either:
            // 1. Ready marker in stderr (validation passed, injector is waiting for game)
            // 2. Process exit with error code (validation failed)
            // 3. Timeout
            
            using var cts = new CancellationTokenSource(timeoutMs);
            var readyTcs = new TaskCompletionSource<bool>();
            int? detectedExitCode = null;

            // Monitor stdout for logging (but ready marker is in stderr)
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    logger.LogDebug("{ShadeName} stdout: {Data}", shadeName, e.Data);
                }
            };

            // Monitor stderr for ready marker
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    logger.LogDebug("{ShadeName} stderr: {Data}", shadeName, e.Data);
                    if (e.Data.Contains(READY_MARKER))
                    {
                        logger.LogInformation("{ShadeName} injector is ready (received ready marker)", shadeName);
                        readyTcs.TrySetResult(true);
                    }
                }
            };

            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) =>
            {
                detectedExitCode = process.ExitCode;
                logger.LogInformation("{ShadeName} injector exited with code: {ExitCode}", shadeName, detectedExitCode);
                readyTcs.TrySetResult(false);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for ready signal, process exit, or timeout
            try
            {
                var completedTask = await Task.WhenAny(
                    readyTcs.Task,
                    Task.Delay(timeoutMs, cts.Token)
                );

                if (completedTask == readyTcs.Task && readyTcs.Task.Result)
                {
                    // Ready marker received - injector is running and waiting for game
                    logger.LogInformation("{ShadeName} validation passed, injector is waiting for game process", shadeName);
                    return (true, InjectorErrorCodes.INJECTION_READY, process);
                }
                else if (detectedExitCode.HasValue)
                {
                    // Process exited - check exit code
                    int exitCode = detectedExitCode.Value;
                    if (InjectorErrorCodes.IsInjectorError(exitCode))
                    {
                        logger.LogWarning("{ShadeName} validation failed with error code: {ExitCode}", shadeName, exitCode);
                        return (false, exitCode, null);
                    }
                    else
                    {
                        logger.LogInformation("{ShadeName} injector exited with code: {ExitCode}", shadeName, exitCode);
                        return (false, exitCode, null);
                    }
                }
                else
                {
                    // Timeout
                    logger.LogWarning("{ShadeName} injector timed out waiting for ready signal", shadeName);
                    try { process.Kill(); } catch { }
                    return (false, -1, null);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("{ShadeName} injector operation cancelled", shadeName);
                try { process.Kill(); } catch { }
                return (false, -1, null);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting {ShadeName} injector", shadeName);
            return (false, -1, null);
        }
    }

    /// <summary>
    /// Monitor injector process and return exit code (legacy method for backward compatibility)
    /// </summary>
    public static async Task<int> MonitorInjectorExitCodeAsync(Process injectorProcess, ILogger logger, string shadeName)
    {
        try
        {
            await Task.Run(() => injectorProcess.WaitForExit());
            int exitCode = injectorProcess.ExitCode;
            
            if (exitCode == 0)
            {
                logger.LogInformation("{ShadeName} injector exited successfully (exit code: 0)", shadeName);
            }
            else if (InjectorErrorCodes.IsInjectorReady(exitCode))
            {
                logger.LogInformation("{ShadeName} injector is ready (exit code: {ExitCode})", shadeName, exitCode);
            }
            else if (InjectorErrorCodes.IsInjectorError(exitCode))
            {
                logger.LogWarning("{ShadeName} injector exited with error code: {ExitCode}", shadeName, exitCode);
            }
            else
            {
                logger.LogInformation("{ShadeName} injector exited with code: {ExitCode}", shadeName, exitCode);
            }
            
            return exitCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error monitoring {ShadeName} injector exit code", shadeName);
            return -1;
        }
    }
}
