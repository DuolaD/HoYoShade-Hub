using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SharpSevenZip;

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
    /// 备份包内部是否有可识别的配置/预设/资源
    /// </summary>
    [JsonIgnore]
    public bool IsValid => IncludesPresets || IncludesReShadeIni || IncludesShaders || IncludesScreenshots;
}

/// <summary>
/// HoYoShade & OpenHoYoShade 备份与还原服务 (基于 7-Zip / SharpSevenZip)
/// </summary>
public static class ShadeBackupRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 导出备份到指定压缩文件 (.zip 或 .7z)
    /// </summary>
    public static async Task ExportBackupAsync(
        string shadePath,
        string shadeName,
        string destinationArchivePath,
        ShadeBackupOptions options,
        IProgress<(double Progress, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(shadePath))
        {
            throw new DirectoryNotFoundException($"Shade directory does not exist: {shadePath}");
        }

        string? destDir = Path.GetDirectoryName(destinationArchivePath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // 创建暂存目录组织打包结构
        string stagingDir = Path.Combine(Path.GetTempPath(), $"HYS_BackupStaging_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            int presetCount = 0;
            bool hasPresets = false;
            bool hasReShadeIni = false;
            bool hasShaders = false;
            bool hasScreenshots = false;

            // 1. Presets
            if (options.BackupPresets)
            {
                string srcPresets = Path.Combine(shadePath, "Presets");
                if (Directory.Exists(srcPresets))
                {
                    string destPresets = Path.Combine(stagingDir, "Presets");
                    CopyDirectory(srcPresets, destPresets);
                    var presetFiles = Directory.GetFiles(destPresets, "*.ini", SearchOption.AllDirectories);
                    presetCount = presetFiles.Length;
                    hasPresets = presetCount > 0;
                }
            }

            // 2. ReShade.ini
            if (options.BackupReShadeIni)
            {
                string srcIni = Path.Combine(shadePath, "ReShade.ini");
                if (File.Exists(srcIni))
                {
                    File.Copy(srcIni, Path.Combine(stagingDir, "ReShade.ini"), true);
                    hasReShadeIni = true;
                }
            }

            // 3. Shaders
            if (options.BackupShaders)
            {
                string srcShaders = Path.Combine(shadePath, "reshade-shaders");
                if (Directory.Exists(srcShaders))
                {
                    string destShaders = Path.Combine(stagingDir, "reshade-shaders");
                    CopyDirectory(srcShaders, destShaders);
                    hasShaders = Directory.EnumerateFileSystemEntries(destShaders).Any();
                }
            }

            // 4. Screenshots
            if (options.BackupScreenshots)
            {
                string srcScreenshots = Path.Combine(shadePath, "Screenshots");
                if (Directory.Exists(srcScreenshots))
                {
                    string destScreenshots = Path.Combine(stagingDir, "Screenshots");
                    CopyDirectory(srcScreenshots, destScreenshots);
                    hasScreenshots = Directory.EnumerateFileSystemEntries(destScreenshots).Any();
                }
            }

            if (!hasPresets && !hasReShadeIni && !hasShaders && !hasScreenshots)
            {
                throw new InvalidOperationException("No components selected or found to back up.");
            }

            // 写入 manifest.json
            var manifest = new ShadeBackupManifest
            {
                ManifestVersion = 1,
                SourceFramework = shadeName,
                FrameworkVersion = options.FrameworkVersion,
                ReShadeVersion = options.ReShadeVersion,
                CreatedAt = DateTime.Now,
                IncludesPresets = hasPresets,
                PresetCount = presetCount,
                IncludesReShadeIni = hasReShadeIni,
                IncludesShaders = hasShaders,
                IncludesScreenshots = hasScreenshots
            };

            string manifestPath = Path.Combine(stagingDir, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);

            // 判断输出格式 (.7z 或 .zip)
            bool is7z = Path.GetExtension(destinationArchivePath).Equals(".7z", StringComparison.OrdinalIgnoreCase);
            var format = is7z ? OutArchiveFormat.SevenZip : OutArchiveFormat.Zip;

            // 压缩输出
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var compressor = new SharpSevenZipCompressor
                {
                    ArchiveFormat = format,
                    CompressionLevel = SharpSevenZip.CompressionLevel.Normal
                };

                compressor.Compressing += (s, e) =>
                {
                    progress?.Report((e.PercentDone, ""));
                };

                compressor.FileCompressionStarted += (s, e) =>
                {
                    progress?.Report((e.PercentDone, e.FileName));
                };

                if (File.Exists(destinationArchivePath))
                {
                    File.Delete(destinationArchivePath);
                }

                compressor.CompressDirectory(stagingDir, destinationArchivePath);
            }, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(stagingDir))
            {
                try { Directory.Delete(stagingDir, true); } catch { }
            }
        }
    }

    /// <summary>
    /// 解析并检测备份包内容 (支持 .zip 与 .7z)
    /// </summary>
    public static async Task<ShadeBackupManifest?> InspectBackupPackageAsync(string archiveFilePath)
    {
        if (!File.Exists(archiveFilePath))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var archive = new SharpSevenZipExtractor(archiveFilePath);

                // 1. 尝试直接查找并提取 manifest.json
                string? manifestFileName = archive.ArchiveFileNames
                    .FirstOrDefault(f => Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(manifestFileName))
                {
                    using var ms = new MemoryStream();
                    archive.ExtractFile(manifestFileName, ms);
                    ms.Position = 0;
                    var manifest = JsonSerializer.Deserialize<ShadeBackupManifest>(ms, JsonOptions);
                    if (manifest != null)
                    {
                        return manifest;
                    }
                }

                // 2. 没有 manifest.json，智能扫描内部文件结构
                bool hasPresets = false;
                bool hasConfig = false;
                bool hasShaders = false;
                bool hasScreenshots = false;
                int presetCount = 0;

                foreach (var fileName in archive.ArchiveFileNames)
                {
                    string norm = fileName.Replace('\\', '/');

                    if (norm.Contains("Presets/", StringComparison.OrdinalIgnoreCase) ||
                        norm.StartsWith("Presets/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasPresets = true;
                        if (norm.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                        {
                            presetCount++;
                        }
                    }
                    else if (Path.GetFileName(norm).Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase))
                    {
                        hasConfig = true;
                    }
                    else if (norm.Contains("reshade-shaders/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasShaders = true;
                    }
                    else if (norm.Contains("Screenshots/", StringComparison.OrdinalIgnoreCase))
                    {
                        hasScreenshots = true;
                    }
                    else if (!norm.Contains('/') && norm.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
                    {
                        presetCount++;
                        hasPresets = true;
                    }
                }

                if (!hasPresets && !hasConfig && !hasShaders && !hasScreenshots)
                {
                    return null;
                }

                return new ShadeBackupManifest
                {
                    ManifestVersion = 1,
                    SourceFramework = "Compatible Backup",
                    CreatedAt = File.GetLastWriteTime(archiveFilePath),
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

            string fileName = $"{shadeName}_AutoSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.7z";
            string snapshotPath = Path.Combine(snapshotFolder, fileName);

            var options = new ShadeBackupOptions
            {
                BackupPresets = true,
                BackupReShadeIni = true,
                BackupShaders = false,
                BackupScreenshots = false,
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
    /// 从备份包还原数据到目标框架目录 (支持 .zip 与 .7z)
    /// </summary>
    public static async Task RestoreBackupAsync(
        string archiveFilePath,
        string destinationShadePath,
        string shadeName,
        ShadeRestoreOptions options,
        IProgress<(double Progress, string Status)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archiveFilePath))
        {
            throw new FileNotFoundException("Backup archive not found", archiveFilePath);
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

        string tempExtractPath = Path.Combine(Path.GetTempPath(), $"HYS_Restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractPath);

        try
        {
            // 2. 使用 SharpSevenZipExtractor 解压整个压缩包
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = new SharpSevenZipExtractor(archiveFilePath);
                archive.Extracting += (s, e) =>
                {
                    progress?.Report((e.FinishPercent * 100.0, ""));
                };
                archive.FileExtractionStarted += (s, e) =>
                {
                    progress?.Report((0, e.FileInfo.FileName));
                };

                archive.ExtractArchive(tempExtractPath);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // 3. 寻找实际根目录（防止有些压缩包外层多套了一层根文件夹）
            string sourceRoot = tempExtractPath;
            if (!File.Exists(Path.Combine(sourceRoot, "manifest.json")) &&
                !Directory.Exists(Path.Combine(sourceRoot, "Presets")) &&
                !File.Exists(Path.Combine(sourceRoot, "ReShade.ini")))
            {
                var subDirs = Directory.GetDirectories(tempExtractPath);
                if (subDirs.Length == 1)
                {
                    sourceRoot = subDirs[0];
                }
            }

            // 4. 还原 ReShade.ini
            string sourceIni = Path.Combine(sourceRoot, "ReShade.ini");
            if (File.Exists(sourceIni) && options.RestoreReShadeIni)
            {
                string targetIni = Path.Combine(destinationShadePath, "ReShade.ini");
                File.Copy(sourceIni, targetIni, true);
                progress?.Report((100, "ReShade.ini"));
            }

            // 5. 还原 Presets 预设
            string sourcePresets = Path.Combine(sourceRoot, "Presets");
            string destPresets = Path.Combine(destinationShadePath, "Presets");
            Directory.CreateDirectory(destPresets);

            if (Directory.Exists(sourcePresets))
            {
                var presetFiles = Directory.GetFiles(sourcePresets, "*", SearchOption.AllDirectories);
                foreach (var file in presetFiles)
                {
                    string relPath = Path.GetRelativePath(sourcePresets, file);
                    string targetFile = Path.Combine(destPresets, relPath);

                    if (File.Exists(targetFile) && options.ConflictResolution == PresetConflictResolution.Rename)
                    {
                        targetFile = GetUniqueFilePath(targetFile);
                    }

                    string? dir = Path.GetDirectoryName(targetFile);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.Copy(file, targetFile, true);
                    progress?.Report((100, Path.GetFileName(file)));
                }
            }

            // 兼顾直接放在根目录的 .ini 预设
            var rootIniFiles = Directory.GetFiles(sourceRoot, "*.ini", SearchOption.TopDirectoryOnly);
            foreach (var file in rootIniFiles)
            {
                if (Path.GetFileName(file).Equals("ReShade.ini", StringComparison.OrdinalIgnoreCase)) continue;

                string targetFile = Path.Combine(destPresets, Path.GetFileName(file));
                if (File.Exists(targetFile) && options.ConflictResolution == PresetConflictResolution.Rename)
                {
                    targetFile = GetUniqueFilePath(targetFile);
                }

                File.Copy(file, targetFile, true);
                progress?.Report((100, Path.GetFileName(file)));
            }

            // 6. 还原 reshade-shaders
            string sourceShaders = Path.Combine(sourceRoot, "reshade-shaders");
            if (Directory.Exists(sourceShaders))
            {
                string destShaders = Path.Combine(destinationShadePath, "reshade-shaders");
                CopyDirectory(sourceShaders, destShaders);
                progress?.Report((100, "reshade-shaders"));
            }

            // 7. 还原 Screenshots
            string sourceScreenshots = Path.Combine(sourceRoot, "Screenshots");
            if (Directory.Exists(sourceScreenshots))
            {
                string destScreenshots = Path.Combine(destinationShadePath, "Screenshots");
                CopyDirectory(sourceScreenshots, destScreenshots);
                progress?.Report((100, "Screenshots"));
            }
        }
        finally
        {
            if (Directory.Exists(tempExtractPath))
            {
                try { Directory.Delete(tempExtractPath, true); } catch { }
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
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
