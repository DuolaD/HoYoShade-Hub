using HoYoShadeHub.Core;
using HoYoShadeHub.Core.HoYoPlay;
using HoYoShadeHub.Features.GameLauncher;
using HoYoShadeHub.Features.GameSetting;
using HoYoShadeHub.Features.Screenshot;
using System.Collections.Generic;

namespace HoYoShadeHub.Features;

internal partial class GameFeatureConfig
{


    private GameFeatureConfig()
    {

    }


    /// <summary>
    /// 支持的页面
    /// </summary>
    public List<string> SupportedPages { get; init; } = [];


    /// <summary>
    /// 支持硬链接
    /// </summary>
    public bool SupportHardLink { get; init; }


    /// <summary>
    /// 支持每日便笺
    /// </summary>
    public bool SupportDailyNote { get; init; }


    public static GameFeatureConfig FromGameId(GameId? gameId)
    {
        if (gameId is null)
        {
            return None;
        }
        GameFeatureConfig config = gameId.GameBiz.Value switch
        {
            GameBiz.bh3_cn => bh3_cn,
            GameBiz.bh3_global => bh3_global,
            GameBiz.hk4e_cn => hk4e_cn,
            GameBiz.hk4e_global => hk4e_global,
            GameBiz.hk4e_bilibili => hk4e_bilibili,
            GameBiz.hk4e_cn_beta => hk4e_beta,
            GameBiz.hk4e_os_beta => hk4e_beta,
            GameBiz.hkrpg_cn => hkrpg_cn,
            GameBiz.hkrpg_global => hkrpg_global,
            GameBiz.hkrpg_bilibili => hkrpg_bilibili,
            GameBiz.hkrpg_beta => hkrpg_beta,
            GameBiz.nap_cn => nap_cn,
            GameBiz.nap_global => nap_global,
            GameBiz.nap_bilibili => nap_bilibili,
            GameBiz.nap_beta_prebeta => nap_beta,
            GameBiz.nap_beta_postbeta => nap_beta,
            _ => Default,
        };
        return config;
    }




    private static readonly GameFeatureConfig None = new();


    private static readonly GameFeatureConfig Default = new()
    {
        SupportedPages = [nameof(GameLauncherPage)]
    };


    private static readonly GameFeatureConfig bh3_cn = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig bh3_global = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hk4e_cn = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hk4e_global = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hk4e_bilibili = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hk4e_beta = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
    };


    private static readonly GameFeatureConfig hkrpg_cn = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hkrpg_global = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hkrpg_bilibili = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig hkrpg_beta = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
    };



    private static readonly GameFeatureConfig nap_cn = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig nap_global = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig nap_bilibili = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
        SupportDailyNote = true,
    };


    private static readonly GameFeatureConfig nap_beta = new()
    {
        SupportedPages =
        [
            nameof(GameLauncherPage),
            nameof(GameSettingPage),
            nameof(ScreenshotPage),
        ],
        SupportHardLink = true,
    };

}
