namespace HoYoShadeHub.Features.Update;

/// <summary>
/// 当自动检测或检查到 HoYoShade Hub 启动器新版本时发送的消息
/// </summary>
/// <param name="Version">最新版本号，例如 "0.0.0-Beta.6"</param>
public record LauncherUpdateDetectedMessage(string Version);
