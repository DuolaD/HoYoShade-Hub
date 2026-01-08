namespace HoYoShadeHub.Features.GameLauncher;

/// <summary>
/// HoYoShade/OpenHoYoShade injector error codes
/// Corresponds to inject_mod.cpp error codes
/// </summary>
public static class InjectorErrorCodes
{
    /// <summary>
    /// File integrity check failed (文件完整性检查失败)
    /// </summary>
    public const int INJECTION_ERROR_FILE_INTEGRITY = 1001;

    /// <summary>
    /// Blacklist process (黑名单进程)
    /// </summary>
    public const int INJECTION_ERROR_BLACKLIST_PROCESS = 1002;

    /// <summary>
    /// Invalid parameter (参数无效)
    /// </summary>
    public const int INJECTION_ERROR_INVALID_PARAM = 1003;

    /// <summary>
    /// Process name doesn't end with .exe (进程名不以.exe结尾)
    /// </summary>
    public const int INJECTION_ERROR_MISSING_EXE_SUFFIX = 1004;

    /// <summary>
    /// Check if the given exit code represents an injector error
    /// </summary>
    public static bool IsInjectorError(int exitCode)
    {
        return exitCode >= 1001 && exitCode <= 1004;
    }

    /// <summary>
    /// Check if this error should prevent game launch
    /// </summary>
    public static bool ShouldPreventGameLaunch(int exitCode)
    {
        return IsInjectorError(exitCode);
    }
}
