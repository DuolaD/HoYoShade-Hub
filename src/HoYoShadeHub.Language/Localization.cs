namespace HoYoShadeHub.Language;

public static class Localization
{



    public static readonly IReadOnlyCollection<(string Title, string LangCode)> LanguageList = new List<(string, string)>
    {
        ("English (en-US)", "en-US"),
        ("简体中文 (zh-CN)", "zh-CN"),
        ("繁體中文 - 香港地區 (zh-HK)", "zh-HK"),
        ("繁體中文 - 台灣地區 (zh-TW)", "zh-TW"),
    }.AsReadOnly();


}
