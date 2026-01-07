using System.Collections.ObjectModel;

namespace HoYoShadeHub.Core;

public record struct GameBiz
{

    private string _value;
    public string Value => _value ?? "";


    public string Game => Value.Contains('_') ? Value.Substring(0, Value.IndexOf('_')) : Value;


    public string Server => Value.Contains('_') ? Value.Substring(Value.IndexOf('_') + 1) : "";

  

    public GameBiz(string? value)
    {
        _value = value ?? "";
    }


    public const string bh3 = "bh3";
    public const string bh3_cn = "bh3_cn";
    public const string bh3_global = "bh3_global";
    public const string bh3_beta = "bh3_beta";   // 内测/创作者体验服（通用）

    public const string bh3_os = "bh3_os";      // 东南亚
    public const string bh3_jp = "bh3_jp";
    public const string bh3_kr = "bh3_kr";
    public const string bh3_asia = "bh3_asia";  // 繁中


    public const string hk4e = "hk4e";
    public const string hk4e_cn = "hk4e_cn";
    public const string hk4e_global = "hk4e_global";
    public const string hk4e_bilibili = "hk4e_bilibili";
    public const string hk4e_cn_beta = "hk4e_cn_beta";   // 中国服 公测/创作者体验服
    public const string hk4e_os_beta = "hk4e_os_beta";   // 国际服 公测/创作者体验服

    //public const string clgm_cn = "clgm_cn";
    //public const string clgm_global = "clgm_global";


    public const string hkrpg = "hkrpg";
    public const string hkrpg_cn = "hkrpg_cn";
    public const string hkrpg_global = "hkrpg_global";
    public const string hkrpg_bilibili = "hkrpg_bilibili";
    public const string hkrpg_beta = "hkrpg_beta";   // 内测/创作者体验服（通用）


    public const string nap = "nap";
    public const string nap_cn = "nap_cn";
    public const string nap_global = "nap_global";
    public const string nap_bilibili = "nap_bilibili";
    public const string nap_beta_prebeta = "nap_beta_prebeta";   // 公测前测试服
    public const string nap_beta_postbeta = "nap_beta_postbeta"; // 公测后测试服/创作者体验服


    public const string hna = "hna";
    public const string hna_cbt1 = "hna_cbt1";   // 第一次内测


    public const string None = "";



    public static ReadOnlyCollection<GameBiz> AllGameBizs { get; private set; } = new List<GameBiz>
    {
        bh3_cn,
        bh3_global,
        bh3_beta,
        //bh3_os,
        //bh3_jp,
        //bh3_kr,
        //bh3_usa,
        //bh3_asia,
        //bh3_eur,
        hk4e_cn,
        hk4e_global,
        hk4e_bilibili,
        hk4e_cn_beta,
        hk4e_os_beta,
        //clgm_cn,
        //clgm_global,
        hkrpg_cn,
        hkrpg_global,
        hkrpg_bilibili,
        hkrpg_beta,
        nap_cn,
        nap_global,
        nap_bilibili,
        nap_beta_prebeta,
        nap_beta_postbeta,
        hna_cbt1,
    }.AsReadOnly();




    public static bool TryParse(string? value, out GameBiz gameBiz)
    {
        gameBiz = new(value);
        return gameBiz.IsKnown();
    }



    public override string ToString() => Value;
    public static implicit operator GameBiz(string? value) => new(value);
    public static implicit operator string(GameBiz value) => value.Value;




    public bool IsKnown() => Value switch
    {
        bh3_cn or bh3_global or bh3_beta => true,
        hk4e_cn or hk4e_global or hk4e_bilibili or hk4e_cn_beta or hk4e_os_beta => true,
        //clgm_cn or clgm_global => true,
        hkrpg_cn or hkrpg_global or hkrpg_bilibili or hkrpg_beta => true,
        nap_cn or nap_global or nap_bilibili or nap_beta_prebeta or nap_beta_postbeta => true,
        hna_cbt1 => true,
        _ => false,
    };


    public bool IsChinaServer() => Server is "cn";


    public bool IsGlobalServer() => Server is "global";


    public bool IsBilibili() => Server is "bilibili";

    public bool IsBetaServer() => Server is "beta_prebeta" or "beta_postbeta" or "cn_beta" or "os_beta" or "beta" or "cbt1";



    public GameBiz ToGame() => Game;


    public string ToGameName() => Game switch
    {
        bh3 => CoreLang.Game_HonkaiImpact3rd,
        hk4e => CoreLang.Game_GenshinImpact,
        hkrpg => CoreLang.Game_HonkaiStarRail,
        nap => CoreLang.Game_ZZZ,
        hna => CoreLang.Game_NexusAnima,
        _ => "",
    };


    public string ToGameServerName() => Server switch
    {
        "cn" => CoreLang.GameServer_ChinaServer,
        "global" => CoreLang.GameServer_GlobalServer,
        "bilibili" => CoreLang.GameServer_Bilibili,
        "beta_prebeta" => CoreLang.GameServer_BetaPreBeta,
        "beta_postbeta" => CoreLang.GameServer_BetaPostBeta,
        "cn_beta" => CoreLang.GameServer_CNBeta,
        "os_beta" => CoreLang.GameServer_OSBeta,
        "beta" => CoreLang.GameServer_Beta,
        "cbt1" => CoreLang.GameServer_CBT1,
        _ => "",
    };


    public string GetGameRegistryKey() => Value switch
    {
        hk4e_cn or hk4e_bilibili => GameRegistry.GamePath_hk4e_cn,
        hk4e_global => GameRegistry.GamePath_hk4e_global,
        hk4e_cn_beta => GameRegistry.GamePath_hk4e_cn_beta,
        hk4e_os_beta => GameRegistry.GamePath_hk4e_os_beta,
        //clgm_cn => GameRegistry.GamePath_hk4e_cloud,
        hkrpg_cn or hkrpg_bilibili => GameRegistry.GamePath_hkrpg_cn,
        hkrpg_global => GameRegistry.GamePath_hkrpg_global,
        hkrpg_beta => GameRegistry.GamePath_hkrpg_beta,
        bh3_cn => GameRegistry.GamePath_bh3_cn,
        bh3_global => GameRegistry.GamePath_bh3_global,
        bh3_beta => GameRegistry.GamePath_bh3_beta,
        bh3_jp => GameRegistry.GamePath_bh3_jp,
        bh3_kr => GameRegistry.GamePath_bh3_kr,
        bh3_os => GameRegistry.GamePath_bh3_overseas,
        bh3_asia => GameRegistry.GamePath_bh3_tw,
        nap_cn or nap_bilibili => GameRegistry.GamePath_nap_cn,
        nap_global => GameRegistry.GamePath_nap_global,
        nap_beta_prebeta => GameRegistry.GamePath_nap_beta_prebeta,
        nap_beta_postbeta => GameRegistry.GamePath_nap_beta_postbeta,
        hna_cbt1 => GameRegistry.GamePath_hna_cbt1,
        _ => "HKEY_CURRENT_USER",
    };





}


