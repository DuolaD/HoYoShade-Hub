using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.Core.HoYoShade;

/// <summary>
/// 预设冲突处理策略
/// </summary>
public enum PresetConflictResolution
{
    /// <summary>
    /// 重命名导入文件（保留现有文件，推荐）
    /// </summary>
    Rename = 0,

    /// <summary>
    /// 直接覆盖同名文件
    /// </summary>
    Overwrite = 1
}

/// <summary>
/// 备份选项
/// </summary>
public class ShadeBackupOptions
{
    /// <summary>
    /// 是否备份预设文件 (Presets)
    /// </summary>
    public bool BackupPresets { get; set; } = true;

    /// <summary>
    /// 是否备份 ReShade.ini 全局配置
    /// </summary>
    public bool BackupReShadeIni { get; set; } = true;

    /// <summary>
    /// 是否备份着色器及材质插件 (reshade-shaders)
    /// </summary>
    public bool BackupShaders { get; set; } = false;

    /// <summary>
    /// 是否备份截图文件 (Screenshots)
    /// </summary>
    public bool BackupScreenshots { get; set; } = false;

    /// <summary>
    /// 框架版本
    /// </summary>
    public string FrameworkVersion { get; set; } = string.Empty;

    /// <summary>
    /// ReShade 版本
    /// </summary>
    public string ReShadeVersion { get; set; } = string.Empty;
}

/// <summary>
/// 还原选项
/// </summary>
public class ShadeRestoreOptions
{
    /// <summary>
    /// 同名预设冲突策略
    /// </summary>
    public PresetConflictResolution ConflictResolution { get; set; } = PresetConflictResolution.Rename;

    /// <summary>
    /// 是否还原 ReShade.ini
    /// </summary>
    public bool RestoreReShadeIni { get; set; } = true;

    /// <summary>
    /// 是否在还原前自动创建安全快照
    /// </summary>
    public bool CreateSafetySnapshot { get; set; } = true;

    /// <summary>
    /// 快照保存的基础目录（如 UserDataFolder）
    /// </summary>
    public string? BackupBaseFolder { get; set; }
}

/// <summary>
/// 备份包清单元数据
/// </summary>
public class ShadeBackupManifest
{
    [JsonPropertyName("manifest_version")]
    public int ManifestVersion { get; set; } = 1;

    [JsonPropertyName("source_framework")]
    public string SourceFramework { get; set; } = string.Empty;

    [JsonPropertyName("framework_version")]
    public string FrameworkVersion { get; set; } = string.Empty;

    [JsonPropertyName("reshade_version")]
    public string ReShadeVersion { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("includes_presets")]
    public bool IncludesPresets { get; set; }

    [JsonPropertyName("preset_count")]
    public int PresetCount { get; set; }

    [JsonPropertyName("includes_reshade_ini")]
    public bool IncludesReShadeIni { get; set; }

    [JsonPropertyName("includes_shaders")]
    public bool IncludesShaders { get; set; }

    [JsonPropertyName("includes_screenshots")]
    public bool IncludesScreenshots { get; set; }

    /// <summary>
    /// 备份包内部是否有可识别的配置/预设
    /// </summary>
    [JsonIgnore]
    public bool IsValid => IncludesPresets || IncludesReShadeIni || IncludesShaders || IncludesScreenshots;
}

/// <summary>
/// HoYoShade & OpenHoYoShade 备份与还原核心服务
/// </summary>
public static class ShadeBackupRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 导出备份到指定 zip 文件
    /// </summary>
    public static async Task ExportBackupAsync(
        string shadePath,
        string shadeName,
        string destinationZipPath,
        ShadeBackupOptions options,
        IProgress<(double Progress, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(shadePath))
        {
            throw new DirectoryNotFoundException($"Shade directory does not exist: {shadePath}");
        }

        string? destDir = Path.GetDirectoryName(destinationZipPath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // 临时文件，避免中途失败留下残缺压缩包
        string tempZipPath = destinationZipPath + ".tmp_" + Guid.NewGuid().ToString("N");

        await Task.Run(() =>
        {
            try
            {
                var filesToZip = new List<(string FullPath, string RelativePath)>();

                // 1. Presets 预设
                int presetCount = 0;
                if (options.BackupPresets)
                {
                    string presetsDir = Path.Combine(shadePath, "Presets");
                    if (Directory.Exists(presetsDir))
                    {
                        var presetFiles = Directory.GetFiles(presetsDir, "*", SearchOption.AllDirectories);
                        foreach (var file in presetFiles)
                        {
                            string rel = Path.GetRelativePath(shadePath, file);
                            filesToZip.Add((file, rel));
                            if (Path.GetExtension(file).Equals(".ini", StringComparison.OrdinalIgnoreCase))
                            {
                                presetCount++;
                            }
                        }
                    }
                }

                // 2. ReShade.ini
                bool hasReShadeIni = false;
                if (options.BackupReShadeIni)
                {
                    string iniPath = Path.Combine(shadePath, "ReShade.ini");
                    if (File.Exists(iniPath))
                    {
                        filesToZip.Add((iniPath, "ReShade.ini"));
                        hasReShadeIni = true;
                    }
                }

                // 3. Shaders
                bool hasShaders = false;
                if (options.BackupShaders)
                {
                    string shadersDir = Path.Combine(shadePath, "reshade-shaders");
                    if (Directory.Exists(shadersDir))
                    {
                        var shaderFiles = Directory.GetFiles(shadersDir, "*", SearchOption.AllDirectories);
                        foreach (var file in shaderFiles)
                        {
                            string rel = Path.GetRelativePath(shadePath, file);
                            filesToZip.Add((file, rel));
                        }
                        hasShaders = shaderFiles.Length > 0;
                    }
                }

                // 4. Screenshots
                bool hasScreenshots = false;
                if (options.BackupScreenshots)
                {
                    string screenshotsDir = Path.Combine(shadePath, "Screenshots");
                    if (Directory.Exists(screenshotsDir))
                    {
                        var screenshotFiles = Directory.GetFiles(screenshotsDir, "*", SearchOption.AllDirectories);
                        foreach (var file in screenshotFiles)
                        {
                            string rel = Path.GetRelativePath(shadePath, file);
                            filesToZip.Add((file, rel));
                        }
                        hasScreenshots = screenshotFiles.Length > 0;
                    }
                }

                if (filesToZip.Count == 0 && !hasReShadeIni)
                {
                    throw new InvalidOperationException("No components selected or found to back up.");
                }

                // 构造清单
                var manifest = new ShadeBackupManifest
                {
                    ManifestVersion = 1,
                    SourceFramework = shadeName,
                    FrameworkVersion = options.FrameworkVersion,
                    ReShadeVersion = options.ReShadeVersion,
                    CreatedAt = DateTime.Now,
                    IncludesPresets = options.BackupPresets && presetCount > 0,
                    PresetCount = presetCount,
                    IncludesReShadeIni = hasReShadeIni,
                    IncludesShaders = hasShaders,
                    IncludesScreenshots = hasScreenshots
                };

                // 创建并写入 Zip
                using (var zipStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, false))
                {
                    // 写入 manifest.json
                    var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                    using (var entryStream = manifestEntry.Open())
                    {
                        JsonSerializer.Serialize(entryStream, manifest, JsonOptions);
                    }

                    int total = filesToZip.Count;
                    int current = 0;

                    foreach (var (fullPath, relativePath) in filesToZip)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string entryName = relativePath.Replace('\\', '/');
                        archive.CreateEntryFromFile(fullPath, entryName, CompressionLevel.Optimal);

                        current++;
                        double pct = total > 0 ? (double)current / total * 100.0 : 100.0;
                        progress?.Report((pct, Path.GetFileName(fullPath)));
                    }
                }

                // 替换或覆盖最终目标文件
                if (File.Exists(destinationZipPath))
                {
                    File.Delete(destinationZipPath);
                }
                File.Move(tempZipPath, destinationZipPath);
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); } catch { }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 解析并检测备份包内容
    /// </summary>
    public static async Task<ShadeBackupManifest?> InspectBackupPackageAsync(string zipFilePath)
    {
        if (!File.Exists(zipFilePath))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipFilePath);

                // 1. 尝试直接从 manifest.json 读取
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry != null)
                {
                    using var stream = manifestEntry.Open();
                    var manifest = JsonSerializer.Deserialize<ShadeBackupManifest>(stream, JsonOptions);
                    if (manifest != null)
                    {
                        return manifest;
                    }
                }

                // 2. 没有 manifest.json，智能扫描 zip 内部结构
                bool hasPresets = false;
                bool hasConfig = false;
                bool hasShaders = false;
                bool hasScreenshots = false;
                int presetCount = 0;

                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');

                    if (name.StartsWith("Presets/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPresets = true;
                        if (name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                        {
                            presetCount++;
                        }
                    }
                    else if (name.Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase) ||
                             name.EndsWith("/ReShade.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        hasConfig = true;
                    }
                    else if (name.StartsWith("reshade-shaders/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasShaders = true;
                    }
                    else if (name.StartsWith("Screenshots/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasScreenshots = true;
                    }
                    else if (!name.Contains('/') && name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                    {
                        // 根目录的 ini 预设
                        presetCount++;
                        hasPresets = true;
                    }
                }

                if (!hasPresets && !hasConfig && !hasShaders && !hasScreenshots)
                {
                    return null; // 不是合法的备份包
                }

                return new ShadeBackupManifest
                {
                    ManifestVersion = 1,
                    SourceFramework = "Compatible Backup",
                    CreatedAt = File.GetLastWriteTime(zipFilePath),
                    IncludesPresets = hasPresets,
                    PresetCount = presetCount,
                    IncludesReShadeIni = hasConfig,
                    IncludesShaders = hasShaders,
                    IncludesScreenshots = hasScreenshots
                };
            }
            catch
            {
                return null;
            }
        });
    }

    /// <summary>
    /// 创建导入前的自动安全快照备份
    /// </summary>
    public static async Task<string?> CreateAutoSnapshotAsync(string shadePath, string shadeName, string? backupBaseFolder)
    {
        if (!Directory.Exists(shadePath)) return null;

        try
        {
            string baseFolder = !string.IsNullOrWhiteSpace(backupBaseFolder)
                ? backupBaseFolder
                : Path.GetDirectoryName(shadePath) ?? shadePath;

            string snapshotFolder = Path.Combine(baseFolder, "Backup", "AutoSnapshots");
            Directory.CreateDirectory(snapshotFolder);

            string fileName = $"{shadeName}_AutoSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
            string snapshotPath = Path.Combine(snapshotFolder, fileName);

            var options = new ShadeBackupOptions
            {
                BackupPresets = true,
                BackupReShadeIni = true,
                BackupShaders = false,
                FrameworkVersion = "AutoSnapshot"
            };

            await ExportBackupAsync(shadePath, shadeName, snapshotPath, options);
            return snapshotPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从备份包还原数据到目标框架目录
    /// </summary>
    public static async Task RestoreBackupAsync(
        string zipFilePath,
        string destinationShadePath,
        string shadeName,
        ShadeRestoreOptions options,
        IProgress<(double Progress, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("Backup archive not found", zipFilePath);
        }

        if (!Directory.Exists(destinationShadePath))
        {
            Directory.CreateDirectory(destinationShadePath);
        }

        // 1. 若开启自动快照，先备份当前配置
        if (options.CreateSafetySnapshot)
        {
            progress?.Report((0, "Creating safety snapshot..."));
            await CreateAutoSnapshotAsync(destinationShadePath, shadeName, options.BackupBaseFolder);
        }

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
            int total = entries.Count;
            int current = 0;

            string presetsDir = Path.Combine(destinationShadePath, "Presets");
            string shadersDir = Path.Combine(destinationShadePath, "reshade-shaders");

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string normName = entry.FullName.Replace('\\', '/');

                // 跳过清单文件
                if (normName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    current++;
                    continue;
                }

                // 1. ReShade.ini
                if (normName.Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase) ||
                    normName.EndsWith("/ReShade.ini", StringComparison.OrdinalIgnoreCase))
                {
                    if (options.RestoreReShadeIni)
                    {
                        string targetPath = Path.Combine(destinationShadePath, "ReShade.ini");
                        ExtractEntry(entry, targetPath, true);
                    }
                    current++;
                    progress?.Report((total > 0 ? (double)current / total * 100 : 100, "ReShade.ini"));
                    continue;
                }

                // 2. Presets 目录下的预设
                if (normName.StartsWith("Presets/", StringComparison.OrdinalIgnoreCase))
                {
                    string subRel = normName.Substring("Presets/".Length);
                    string targetFile = Path.Combine(presetsDir, subRel.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(targetFile) && options.ConflictResolution == PresetConflictResolution.Rename)
                    {
                        targetFile = GetUniqueFilePath(targetFile);
                    }

                    ExtractEntry(entry, targetFile, true);
                    current++;
                    progress?.Report((total > 0 ? (double)current / total * 100 : 100, entry.Name));
                    continue;
                }

                // 3. 根目录下的 .ini 预设（兼容没有规范放在 Presets 文件夹的压缩包）
                if (!normName.Contains('/') && normName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    string targetFile = Path.Combine(presetsDir, entry.Name);
                    if (File.Exists(targetFile) && options.ConflictResolution == PresetConflictResolution.Rename)
                    {
                        targetFile = GetUniqueFilePath(targetFile);
                    }

                    ExtractEntry(entry, targetFile, true);
                    current++;
                    progress?.Report((total > 0 ? (double)current / total * 100 : 100, entry.Name));
                    continue;
                }

                // 4. 着色器与材质 (reshade-shaders)
                if (normName.StartsWith("reshade-shaders/", StringComparison.OrdinalIgnoreCase))
                {
                    string subRel = normName.Substring("reshade-shaders/".Length);
                    string targetFile = Path.Combine(shadersDir, subRel.Replace('/', Path.DirectorySeparatorChar));
                    ExtractEntry(entry, targetFile, true);
                    current++;
                    progress?.Report((total > 0 ? (double)current / total * 100 : 100, entry.Name));
                    continue;
                }

                // 5. 游戏截图 (Screenshots)
                if (normName.StartsWith("Screenshots/", StringComparison.OrdinalIgnoreCase))
                {
                    string subRel = normName.Substring("Screenshots/".Length);
                    string targetFile = Path.Combine(destinationShadePath, "Screenshots", subRel.Replace('/', Path.DirectorySeparatorChar));
                    ExtractEntry(entry, targetFile, true);
                    current++;
                    progress?.Report((total > 0 ? (double)current / total * 100 : 100, entry.Name));
                    continue;
                }

                current++;
            }
        }, cancellationToken);
    }

    private static void ExtractEntry(ZipArchiveEntry entry, string destinationPath, bool overwrite)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        entry.ExtractToFile(destinationPath, overwrite);
    }

    private static string GetUniqueFilePath(string filePath)
    {
        string? dir = Path.GetDirectoryName(filePath);
        string filenameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath);

        int counter = 1;
        string newPath = Path.Combine(dir ?? "", $"{filenameWithoutExt} (Imported){ext}");

        while (File.Exists(newPath))
        {
            counter++;
            newPath = Path.Combine(dir ?? "", $"{filenameWithoutExt} (Imported {counter}){ext}");
        }

        return newPath;
    }
}
