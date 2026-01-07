namespace HoYoShadeHub.Core.HoYoPlay;


/// <summary>
/// 启动器 ID
/// </summary>
public abstract class LauncherId
{

    public const string ChinaOfficial = "jGHBHlcOq1";

    public const string GlobalOfficial = "VYTpXlbWo8";

    public const string BilibiliGenshin = "umfgRO5gh5";

    public const string BilibiliStarRail = "6P5gHMNyK3";

    public const string BilibiliZZZ = "xV0f4r1GT0";


    public static bool IsChinaOfficial(string launcherId)
    {
        return launcherId is ChinaOfficial;
    }


    public static bool IsGlobalOfficial(string launcherId)
    {
        return launcherId is GlobalOfficial;
    }


    public static bool IsBilibili(string launcherId)
    {
        return launcherId is BilibiliGenshin or BilibiliStarRail or BilibiliZZZ;
    }


    public static string? FromGameBiz(GameBiz biz)
    {
        return biz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hkrpg_cn or GameBiz.bh3_cn or GameBiz.nap_cn => ChinaOfficial,
            GameBiz.hk4e_global or GameBiz.hkrpg_global or GameBiz.bh3_global or GameBiz.nap_global => GlobalOfficial,
            GameBiz.hk4e_bilibili => BilibiliGenshin,
            GameBiz.hkrpg_bilibili => BilibiliStarRail,
            GameBiz.nap_bilibili => BilibiliZZZ,
            GameBiz.hk4e_cn_beta => ChinaOfficial,  // 中国服测试服使用中国服启动器
            GameBiz.hk4e_os_beta => GlobalOfficial, // 国际服测试服使用国际服启动器
            GameBiz.nap_beta_prebeta or GameBiz.nap_beta_postbeta => ChinaOfficial,
            string value when value.EndsWith("_cn") => ChinaOfficial,
            string value when value.EndsWith("_global") => GlobalOfficial,
            _ => null,
        };
    }


    public static string? FromGameId(GameId gameId) => FromGameBiz(gameId.GameBiz);


    public static List<(GameBiz GameBiz, string LauncherId)> GetBilibiliLaunchers()
    {
        return new List<(GameBiz, string)>
        {
            (GameBiz.hk4e_bilibili, BilibiliGenshin),
            (GameBiz.hkrpg_bilibili, BilibiliStarRail),
            (GameBiz.nap_bilibili, BilibiliZZZ),
        };
    }
}

