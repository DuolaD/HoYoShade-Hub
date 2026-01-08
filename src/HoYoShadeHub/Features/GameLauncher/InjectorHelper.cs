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
