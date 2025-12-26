using System;
using System.Collections.Generic;

namespace HoYoShadeHub.RPC.HoYoShadeInstall;

/// <summary>
/// Installation target for ReShade packages
/// </summary>
public enum ReShadeInstallTarget
{
    HoYoShadeOnly = 0,
    OpenHoYoShadeOnly = 1,
    Both = 2
}

/// <summary>
/// Download server URLs for ReShade package lists
/// Based on https://github.com/crosire/reshade-shaders repository
/// </summary>
public static class ReShadeDownloadServer
{
    public const string EffectPackagesUrl = "https://raw.githubusercontent.com/crosire/reshade-shaders/list/EffectPackages.ini";
    public const string AddonsUrl = "https://raw.githubusercontent.com/crosire/reshade-shaders/list/Addons.ini";
    
    public static string GetEffectPackagesUrl(bool useProxy)
    {
        return useProxy ? $"https://ghproxy.com/{EffectPackagesUrl}" : EffectPackagesUrl;
    }
    
    public static string GetAddonsUrl(bool useProxy)
    {
        return useProxy ? $"https://ghproxy.com/{AddonsUrl}" : AddonsUrl;
    }
}

/// <summary>
/// Individual effect file within a package
/// Based on official installer's EffectFile class
/// </summary>
public class EffectFile
{
    public bool Selected { get; set; }
    public string FileName { get; set; }
}

/// <summary>
/// ReShade effect package definition from EffectPackages.ini
/// Based on official installer's EffectPackage class
/// </summary>
public class EffectPackage
{
    /// <summary>
    /// Selection state: true = all selected, false = none, null = partial
    /// </summary>
    public bool? Selected { get; set; } = false;
    
    /// <summary>
    /// Whether user can modify selection
    /// </summary>
    public bool Modifiable { get; set; } = true;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Package description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Installation path for shader files (relative to game base path)
    /// </summary>
    public string InstallPath { get; set; }
    
    /// <summary>
    /// Installation path for texture files (relative to game base path)
    /// </summary>
    public string TextureInstallPath { get; set; }
    
    /// <summary>
    /// Download URL for package archive
    /// </summary>
    public string DownloadUrl { get; set; }
    
    /// <summary>
    /// Repository URL for more information
    /// </summary>
    public string RepositoryUrl { get; set; }
    
    /// <summary>
    /// Individual effect files in this package
    /// </summary>
    public EffectFile[] EffectFiles { get; set; }
    
    /// <summary>
    /// Effect files to exclude from installation
    /// </summary>
    public string[] DenyEffectFiles { get; set; }
}

/// <summary>
/// ReShade addon package definition from Addons.ini
/// Based on official installer's Addon class
/// </summary>
public class Addon
{
    /// <summary>
    /// Whether this addon is enabled (has download URL)
    /// </summary>
    public bool Enabled => !string.IsNullOrEmpty(DownloadUrl);
    
    /// <summary>
    /// Whether user selected this addon
    /// </summary>
    public bool Selected { get; set; } = false;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Package description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Installation path for effect files bundled with addon
    /// </summary>
    public string EffectInstallPath { get; set; }
    
    /// <summary>
    /// Download URL for addon binary
    /// </summary>
    public string DownloadUrl { get; set; }
    
    /// <summary>
    /// Repository URL for more information
    /// </summary>
    public string RepositoryUrl { get; set; }
}
