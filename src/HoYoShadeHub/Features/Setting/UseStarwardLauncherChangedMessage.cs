namespace HoYoShadeHub.Features.Setting;

/// <summary>
/// "使用Starward启动器启动公开客户端游戏"设置变更消息
/// </summary>
internal class UseStarwardLauncherChangedMessage
{
    public bool IsEnabled { get; set; }

    public UseStarwardLauncherChangedMessage(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }
}
