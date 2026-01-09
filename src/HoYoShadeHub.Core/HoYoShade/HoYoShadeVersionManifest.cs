using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HoYoShadeHub.Core.HoYoShade;

/// <summary>
/// HoYoShade 框架版本清单
/// </summary>
public class HoYoShadeVersionManifest
{
    /// <summary>
    /// HoYoShade 框架信息
    /// </summary>
    [JsonPropertyName("HoYoShade")]
    public FrameworkVersionInfo? HoYoShade { get; set; }

    /// <summary>
    /// OpenHoYoShade 框架信息
    /// </summary>
    [JsonPropertyName("OpenHoYoShade")]
    public FrameworkVersionInfo? OpenHoYoShade { get; set; }
}

/// <summary>
/// 框架版本信息
/// </summary>
public class FrameworkVersionInfo
{
    /// <summary>
    /// 版本号 (例如: "V3.0.1")
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 安装时间
    /// </summary>
    [JsonPropertyName("installed_at")]
    public DateTime InstalledAt { get; set; }

    /// <summary>
    /// 来源 (github_release, local_import)
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 文件校验值 (可选)
    /// </summary>
    [JsonPropertyName("files")]
    public Dictionary<string, string>? Files { get; set; }

    /// <summary>
    /// SHA256 校验值 (可选)
    /// </summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}
