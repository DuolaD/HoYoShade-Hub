using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace HoYoShadeHub.Core.HoYoShade;

/// <summary>
/// HoYoShade 版本清单服务
/// </summary>
public class HoYoShadeVersionService
{
    private readonly string _manifestPath;

    public HoYoShadeVersionService(string userDataFolder)
    {
        _manifestPath = Path.Combine(userDataFolder, "hoyoshade_manifest.json");
    }

    /// <summary>
    /// 加载版本清单
    /// </summary>
    /// <returns>版本清单对象，如果不存在则返回空清单</returns>
    public async Task<HoYoShadeVersionManifest> LoadManifestAsync()
    {
        try
        {
            if (!File.Exists(_manifestPath))
            {
                return new HoYoShadeVersionManifest();
            }

            var json = await File.ReadAllTextAsync(_manifestPath);
            var manifest = JsonSerializer.Deserialize<HoYoShadeVersionManifest>(json);
            
            if (manifest == null)
            {
                return new HoYoShadeVersionManifest();
            }

            return manifest;
        }
        catch (Exception)
        {
            return new HoYoShadeVersionManifest();
        }
    }

    /// <summary>
    /// 保存版本清单
    /// </summary>
    /// <param name="manifest">要保存的版本清单</param>
    public async Task SaveManifestAsync(HoYoShadeVersionManifest manifest)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(manifest, options);
            await File.WriteAllTextAsync(_manifestPath, json);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 更新 HoYoShade 版本信息
    /// </summary>
    /// <param name="version">版本号</param>
    /// <param name="source">来源</param>
    /// <param name="sha256">SHA256 校验值（可选）</param>
    public async Task UpdateHoYoShadeVersionAsync(string version, string source, string? sha256 = null)
    {
        var manifest = await LoadManifestAsync();
        
        manifest.HoYoShade = new FrameworkVersionInfo
        {
            Version = version,
            InstalledAt = DateTime.Now,
            Source = source,
            Sha256 = sha256
        };

        await SaveManifestAsync(manifest);
    }

    /// <summary>
    /// 更新 OpenHoYoShade 版本信息
    /// </summary>
    /// <param name="version">版本号</param>
    /// <param name="source">来源</param>
    /// <param name="sha256">SHA256 校验值（可选）</param>
    public async Task UpdateOpenHoYoShadeVersionAsync(string version, string source, string? sha256 = null)
    {
        var manifest = await LoadManifestAsync();
        
        manifest.OpenHoYoShade = new FrameworkVersionInfo
        {
            Version = version,
            InstalledAt = DateTime.Now,
            Source = source,
            Sha256 = sha256
        };

        await SaveManifestAsync(manifest);
    }

    /// <summary>
    /// 获取 HoYoShade 版本信息
    /// </summary>
    public async Task<FrameworkVersionInfo?> GetHoYoShadeVersionAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest.HoYoShade;
    }

    /// <summary>
    /// 获取 OpenHoYoShade 版本信息
    /// </summary>
    public async Task<FrameworkVersionInfo?> GetOpenHoYoShadeVersionAsync()
    {
        var manifest = await LoadManifestAsync();
        return manifest.OpenHoYoShade;
    }

    /// <summary>
    /// 清除 HoYoShade 版本信息
    /// </summary>
    public async Task ClearHoYoShadeVersionAsync()
    {
        var manifest = await LoadManifestAsync();
        manifest.HoYoShade = null;
        await SaveManifestAsync(manifest);
    }

    /// <summary>
    /// 清除 OpenHoYoShade 版本信息
    /// </summary>
    public async Task ClearOpenHoYoShadeVersionAsync()
    {
        var manifest = await LoadManifestAsync();
        manifest.OpenHoYoShade = null;
        await SaveManifestAsync(manifest);
    }
}
