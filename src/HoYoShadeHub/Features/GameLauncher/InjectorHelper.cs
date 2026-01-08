using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HoYoShadeHub.Features.GameLauncher;

/// <summary>
/// Helper class for handling HoYoShade/OpenHoYoShade injector operations and error reporting
/// </summary>
public static class InjectorHelper
{
    /// <summary>
    /// Get user-friendly error message for injector exit code
    /// </summary>
    /// <param name="exitCode">Injector process exit code</param>
    /// <param name="shadeName">Name of the shader framework (HoYoShade or OpenHoYoShade)</param>
    /// <returns>Localized error message</returns>
    public static string GetErrorMessage(int exitCode, string shadeName)
    {
        // Inline error messages - can be moved to Lang resource files later
        return exitCode switch
        {
            InjectorErrorCodes.INJECTION_ERROR_FILE_INTEGRITY => 
                $"{shadeName} file integrity check failed. Some required files may be missing or corrupted.",
            
            InjectorErrorCodes.INJECTION_ERROR_BLACKLIST_PROCESS => 
                $"{shadeName} cannot inject into this process. The process name is blacklisted.",
            
            InjectorErrorCodes.INJECTION_ERROR_INVALID_PARAM => 
                $"{shadeName} received invalid parameters.",
            
            InjectorErrorCodes.INJECTION_ERROR_MISSING_EXE_SUFFIX => 
                $"{shadeName} error: Process name must end with .exe",
            
            _ => $"{shadeName} injector exited with error code: 0x{exitCode:X}"
        };
    }

    /// <summary>
    /// Monitor injector process and return exit code
    /// </summary>
    /// <param name="injectorProcess">The injector process to monitor</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="shadeName">Name of the shader framework</param>
    /// <returns>Exit code of the injector process</returns>
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
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Error monitoring {ShadeName} injector exit code", shadeName);
            return -1;
        }
    }
}
