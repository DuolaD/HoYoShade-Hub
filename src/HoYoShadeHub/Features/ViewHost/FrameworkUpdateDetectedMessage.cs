namespace HoYoShadeHub.Features.ViewHost;

/// <summary>
/// 当自动检测或检查到 HoYoShade / OpenHoYoShade 框架新版本时发送的消息
/// </summary>
/// <param name="FrameworkName">框架名称，例如 "HoYoShade" 或 "OpenHoYoShade"</param>
/// <param name="Version">最新版本号，例如 "V3.0.0-Beta.9"</param>
public record FrameworkUpdateDetectedMessage(string FrameworkName, string Version);
