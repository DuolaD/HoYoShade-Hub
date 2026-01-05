using Microsoft.Extensions.Logging;
using SharpSevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HoYoShadeHub.RPC.HoYoShadeInstall;

public class HoYoShadeInstallService
{
    private readonly ILogger<HoYoShadeInstallService> _logger;
    private readonly HttpClient _httpClient;

    public HoYoShadeInstallService(ILogger<HoYoShadeInstallService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public int State { get; private set; }
    public long TotalBytes { get; private set; }
    public long DownloadBytes { get; private set; }
    public string ErrorMessage { get; private set; }

    // ReShade pack installation properties
    public int ReShadePackState { get; private set; }
    public long TotalFiles { get; private set; }
    public long DownloadedFiles { get; private set; }
    public long ReShadePackTotalBytes { get; private set; }
    public long ReShadePackDownloadBytes { get; private set; }
    public string CurrentFile { get; private set; }
    public string ReShadePackErrorMessage { get; private set; }
    public int CurrentFileType { get; private set; } // 0: Shader, 1: Addon
    public long DownloadSpeedBytesPerSec { get; private set; }

    private DateTime _lastSpeedUpdate = DateTime.Now;
    private long _lastDownloadBytes = 0;

    public async Task StartInstallAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        string zipPath = "";
        bool isLocalFile = url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        
        try
        {
            _logger.LogInformation("Starting HoYoShade installation. URL: {Url}, Target: {Target}", url, targetPath);
            State = 0;
            ErrorMessage = null;

            Directory.CreateDirectory(targetPath);
            
            if (isLocalFile)
            {
                zipPath = url.Substring(7);
                _logger.LogInformation("Using local file: {ZipPath}", zipPath);
                
                if (!File.Exists(zipPath))
                {
                    throw new FileNotFoundException($"Local package not found: {zipPath}");
                }
                
                TotalBytes = new FileInfo(zipPath).Length;
                DownloadBytes = TotalBytes;
            }
            else
            {
                State = 1;
                string folderName = Path.GetFileName(targetPath);
                zipPath = Path.Combine(Path.GetDirectoryName(targetPath) ?? targetPath, $"{folderName}_Install.zip");

                long resumeOffset = 0;
                if (File.Exists(zipPath))
                {
                    resumeOffset = new FileInfo(zipPath).Length;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (resumeOffset > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeOffset, null);
                }
                
                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    long totalContentLength = response.Content.Headers.ContentLength ?? 0;

                    if (response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                    {
                        if (response.Content.Headers.ContentRange?.Length.HasValue ?? false)
                        {
                            TotalBytes = response.Content.Headers.ContentRange.Length.Value;
                        }
                        else
                        {
                            TotalBytes = resumeOffset + totalContentLength;
                        }
                    }
                    else
                    {
                        resumeOffset = 0;
                        TotalBytes = totalContentLength;
                    }

                    DownloadBytes = resumeOffset;

                    FileMode mode = (resumeOffset > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent) ? FileMode.Append : FileMode.Create;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                    using (var fileStream = new FileStream(zipPath, mode, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            DownloadBytes += bytesRead;
                        }
                    }
                }
            }

            State = 2;
            _logger.LogInformation("Extracting HoYoShade zip to {targetPath}.", targetPath);
            
            await Task.Run(() =>
            {
                using var archive = new SharpSevenZipExtractor(zipPath);
                archive.ExtractArchive(targetPath);
            }, cancellationToken);

            if (!isLocalFile && File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            State = 3;
            _logger.LogInformation("HoYoShade installation finished.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("HoYoShade installation canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HoYoShade installation failed.");
            State = 4;
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.ToString() : ex.Message;
            
            if (!isLocalFile && !string.IsNullOrEmpty(zipPath) && File.Exists(zipPath))
            {
                try { File.Delete(zipPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Install ReShade effect packages and addons following official installer logic
    /// Reference: MainWindow.xaml.cs -> InstallStep_DownloadEffectPackage/InstallStep_DownloadAddon
    /// </summary>
    public async Task InstallReShadePackAsync(
        string basePath,
        ReShadeInstallTarget installTarget,
        bool useProxy,
        List<EffectPackage> selectedEffectPackages,
        List<Addon> selectedAddons,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("=== Starting ReShade pack installation ===");
            _logger.LogInformation("BasePath: {BasePath}, Target: {Target}, UseProxy: {UseProxy}",
                basePath, installTarget, useProxy);
            _logger.LogInformation("Selected {EffectCount} effect packages and {AddonCount} addons",
                selectedEffectPackages.Count, selectedAddons.Count);

            ReShadePackState = 1; // Downloading
            ReShadePackErrorMessage = null;
            DownloadedFiles = 0;
            ReShadePackDownloadBytes = 0;
            _lastSpeedUpdate = DateTime.Now;
            _lastDownloadBytes = 0;

            // Determine target directories
            var targetDirs = new List<string>();
            if (installTarget == ReShadeInstallTarget.HoYoShadeOnly || installTarget == ReShadeInstallTarget.Both)
            {
                targetDirs.Add(Path.Combine(basePath, "HoYoShade"));
            }
            if (installTarget == ReShadeInstallTarget.OpenHoYoShadeOnly || installTarget == ReShadeInstallTarget.Both)
            {
                targetDirs.Add(Path.Combine(basePath, "OpenHoYoShade"));
            }

            _logger.LogInformation("Target directories: {Dirs}", string.Join(", ", targetDirs));

            TotalFiles = (selectedEffectPackages.Count + selectedAddons.Count) * targetDirs.Count;
            _logger.LogInformation("Total files to download: {TotalFiles}", TotalFiles);

            if (TotalFiles == 0)
            {
                _logger.LogWarning("No files to download!");
                ReShadePackState = 3;
                return;
            }

            int successCount = 0;
            int failedCount = 0;
            int skippedCount = 0;

            // Install effect packages for each target
            _logger.LogInformation("=== Installing effect packages ===");
            foreach (var targetDir in targetDirs)
            {
                _logger.LogInformation("Installing to target directory: {TargetDir}", targetDir);
                
                foreach (var package in selectedEffectPackages)
                {
                    _logger.LogInformation("Downloading effect package: {PackageName}", package.Name);
                    CurrentFileType = 0; // Shader
                    var result = await DownloadAndInstallEffectPackageAsync(package, targetDir, useProxy, cancellationToken);
                    if (result == InstallResult.Success) successCount++;
                    else if (result == InstallResult.Failed) failedCount++;
                    else skippedCount++;
                }

                _logger.LogInformation("=== Installing addons ===");
                foreach (var addon in selectedAddons)
                {
                    _logger.LogInformation("Downloading addon: {AddonName}", addon.Name);
                    CurrentFileType = 1; // Addon
                    var result = await DownloadAndInstallAddonAsync(addon, targetDir, useProxy, cancellationToken);
                    if (result == InstallResult.Success) successCount++;
                    else if (result == InstallResult.Failed) failedCount++;
                    else skippedCount++;
                }
            }

            // Write search paths to ReShade.ini for each target
            _logger.LogInformation("=== Writing search paths ===");
            foreach (var targetDir in targetDirs)
            {
                WriteSearchPaths(targetDir);
            }

            ReShadePackState = 3; // Finished
            _logger.LogInformation("=== ReShade pack installation finished ===");
            _logger.LogInformation("Successfully installed {Downloaded}/{Total} packages (Success: {Success}, Failed: {Failed}, Skipped: {Skipped})", 
                DownloadedFiles, TotalFiles, successCount, failedCount, skippedCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ReShade pack installation canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReShade pack installation failed.");
            ReShadePackState = 4;
            ReShadePackErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? ex.ToString() : ex.Message;
        }
    }

    private enum InstallResult
    {
        Success,
        Failed,
        Skipped
    }

    /// <summary>
    /// Download and install effect package following official installer logic
    /// Reference: MainWindow.xaml.cs -> InstallStep_DownloadEffectPackage/InstallStep_InstallEffectPackage
    /// </summary>
    private async Task<InstallResult> DownloadAndInstallEffectPackageAsync(
        EffectPackage package,
        string targetBaseDir,
        bool useProxy,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(package.DownloadUrl))
            {
                _logger.LogWarning("Effect package {Package} has empty DownloadUrl, skipping", package.Name);
                DownloadedFiles++; // Still count as processed
                UpdateDownloadSpeed(); // Update speed even when skipping
                return InstallResult.Skipped;
            }

            CurrentFile = package.Name;
            
            // Apply proxy URL if needed (legacy ghproxy support for backward compatibility)
            // New proxy logic should use CloudProxyManager on client side
            var downloadUrl = useProxy ? $"https://ghproxy.com/{package.DownloadUrl}" : package.DownloadUrl;
            
            _logger.LogInformation(">>> Downloading effect package: {Package}", package.Name);
            _logger.LogInformation("    Download URL: {Url}", downloadUrl);

            // Download to temp file
            string downloadPath = Path.Combine(Path.GetTempPath(), "ReShadeSetupDownload.tmp");
            
            _logger.LogInformation("    Sending HTTP request...");
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                
                var fileSize = response.Content.Headers.ContentLength ?? 0;
                var startBytes = ReShadePackDownloadBytes;
                
                _logger.LogInformation("    File size: {Size} bytes, downloading...", fileSize);
                
                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        ReShadePackDownloadBytes += bytesRead;
                        UpdateDownloadSpeed();
                    }
                }
            }

            _logger.LogInformation("    Download complete, extracting...");

            // Extract archive
            string tempPath = Path.Combine(Path.GetTempPath(), "ReShadeSetup");
            string tempPathEffects = null;
            string tempPathTextures = null;
            
            // Delete existing temp directory
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }

            ZipFile.ExtractToDirectory(downloadPath, tempPath);
            _logger.LogInformation("    Extracted to temp directory");

            var effects = Directory.GetFiles(tempPath, "*.fx", SearchOption.AllDirectories);
            _logger.LogInformation("    Found {Count} effect files", effects.Length);

            // Find shader and texture directories (standard names first)
            tempPathEffects = Directory.EnumerateDirectories(tempPath, "Shaders", SearchOption.AllDirectories).FirstOrDefault();
            tempPathTextures = Directory.EnumerateDirectories(tempPath, "Textures", SearchOption.AllDirectories).FirstOrDefault();

            // Fallback: find first directory containing shaders/textures
            if (tempPathEffects == null)
            {
                tempPathEffects = effects.Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
            }
            if (tempPathTextures == null)
            {
                string[] textureExtensions = { "*.png", "*.jpg", "*.jpeg" };
                tempPathTextures = textureExtensions
                    .SelectMany(ext => Directory.EnumerateFiles(tempPath, ext, SearchOption.AllDirectories))
                    .Select(x => Path.GetDirectoryName(x))
                    .OrderBy(x => x.Length)
                    .FirstOrDefault();
            }

            _logger.LogInformation("    Shader directory: {ShaderDir}", tempPathEffects ?? "(none)");
            _logger.LogInformation("    Texture directory: {TextureDir}", tempPathTextures ?? "(none)");

            // Filter effects based on selection
            effects = effects.Where(filePath => tempPathEffects != null && filePath.StartsWith(tempPathEffects)).ToArray();

            // Delete denied effects
            if (package.DenyEffectFiles != null)
            {
                var denyEffects = effects.Where(effectPath => package.DenyEffectFiles.Contains(Path.GetFileName(effectPath)));
                foreach (string effectPath in denyEffects)
                {
                    File.Delete(effectPath);
                }
                effects = effects.Except(denyEffects).ToArray();
            }

            // Delete unselected effects
            if (package.Selected == null && package.EffectFiles != null)
            {
                var disabledEffects = effects.Where(effectPath => 
                    package.EffectFiles.Any(ef => !ef.Selected && ef.FileName == Path.GetFileName(effectPath)));
                foreach (string effectPath in disabledEffects)
                {
                    File.Delete(effectPath);
                }
                effects = effects.Except(disabledEffects).ToArray();
            }

            _logger.LogInformation("    After filtering: {Count} effect files to install", effects.Length);

            // Determine installation paths
            string targetPathEffects = string.IsNullOrEmpty(package.InstallPath)
                ? Path.Combine(targetBaseDir, "reshade-shaders", "Shaders")
                : Path.Combine(targetBaseDir, package.InstallPath);
            
            string targetPathTextures = string.IsNullOrEmpty(package.TextureInstallPath)
                ? Path.Combine(targetBaseDir, "reshade-shaders", "Textures")
                : Path.Combine(targetBaseDir, package.TextureInstallPath);

            _logger.LogInformation("    Target shader path: {Path}", targetPathEffects);
            _logger.LogInformation("    Target texture path: {Path}", targetPathTextures);

            // Move files to target
            if (tempPathEffects != null)
            {
                MoveFiles(tempPathEffects, targetPathEffects);
                _logger.LogInformation("    Copied shader files to target");
            }
            if (tempPathTextures != null)
            {
                MoveFiles(tempPathTextures, targetPathTextures);
                _logger.LogInformation("    Copied texture files to target");
            }

            // Cleanup
            File.Delete(downloadPath);
            Directory.Delete(tempPath, true);

            DownloadedFiles++;
            UpdateDownloadSpeed();
            _logger.LogInformation("<<< Installed effect package {Package} ({Downloaded}/{Total})",
                package.Name, DownloadedFiles, TotalFiles);
            return InstallResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! Failed to install effect package {Package}", package.Name);
            // Don't throw - continue with other packages
            DownloadedFiles++; // Count as processed even if failed
            UpdateDownloadSpeed();
            return InstallResult.Failed;
        }
    }

    /// <summary>
    /// Download and install addon following official installer logic
    /// Reference: MainWindow.xaml.cs -> InstallStep_DownloadAddon/InstallStep_InstallAddon
    /// </summary>
    private async Task<InstallResult> DownloadAndInstallAddonAsync(
        Addon addon,
        string targetBaseDir,
        bool useProxy,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(addon.DownloadUrl))
            {
                _logger.LogWarning("Addon {Addon} has empty DownloadUrl, skipping", addon.Name);
                DownloadedFiles++;
                UpdateDownloadSpeed();
                return InstallResult.Skipped;
            }

            CurrentFile = addon.Name;
            var downloadUrl = useProxy ? $"https://ghproxy.com/{addon.DownloadUrl}" : addon.DownloadUrl;
            
            _logger.LogInformation(">>> Downloading addon: {Addon}", addon.Name);
            _logger.LogInformation("    Download URL: {Url}", downloadUrl);

            string downloadPath = Path.Combine(Path.GetTempPath(), "ReShadeSetupDownload.tmp");
            
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var fileSize = response.Content.Headers.ContentLength ?? 0;
                
                _logger.LogInformation("    File size: {Size} bytes, downloading...", fileSize);
                
                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        ReShadePackDownloadBytes += bytesRead;
                        UpdateDownloadSpeed();
                    }
                }
            }

            _logger.LogInformation("    Download complete");

            string ext = Path.GetExtension(new Uri(addon.DownloadUrl).AbsolutePath);
            string tempPath = null;
            string tempPathEffects = null;

            // Target directory for addon binaries: reshade-shaders\Addons
            string targetPathAddon = Path.Combine(targetBaseDir, "reshade-shaders", "Addons");
            Directory.CreateDirectory(targetPathAddon);

            _logger.LogInformation("    Target addon path: {Path}", targetPathAddon);

            // If not a direct addon file, extract archive
            if (ext != ".addon" && ext != ".addon32" && ext != ".addon64")
            {
                tempPath = Path.Combine(Path.GetTempPath(), "reshade-addons");
                
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, true);
                }

                _logger.LogInformation("    Extracting addon archive...");
                ZipFile.ExtractToDirectory(downloadPath, tempPath);

                // Find addon binary (prefer 64-bit)
                string addonPath = Directory.EnumerateFiles(tempPath, "*.addon64", SearchOption.AllDirectories).FirstOrDefault();
                if (addonPath == null)
                {
                    addonPath = Directory.EnumerateFiles(tempPath, "*.addon32", SearchOption.AllDirectories).FirstOrDefault();
                }
                if (addonPath == null)
                {
                    var addonPaths = Directory.EnumerateFiles(tempPath, "*.addon", SearchOption.AllDirectories);
                    if (addonPaths.Count() == 1)
                    {
                        addonPath = addonPaths.First();
                    }
                    else
                    {
                        addonPath = addonPaths.FirstOrDefault(x => x.Contains("x64") || Path.GetFileNameWithoutExtension(x).EndsWith("64"));
                    }
                }

                if (addonPath == null)
                {
                    throw new FormatException("Add-on archive is missing add-on binary.");
                }

                downloadPath = addonPath;

                // Check for effect files bundled with addon
                var effects = Directory.EnumerateFiles(tempPath, "*.fx", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.EnumerateFiles(tempPath, "*.addonfx", SearchOption.TopDirectoryOnly));
                tempPathEffects = effects.Select(x => Path.GetDirectoryName(x)).OrderBy(x => x.Length).FirstOrDefault();
            }

            // Install addon binary to reshade-shaders\Addons
            string addonFileName = Path.GetFileNameWithoutExtension(tempPath != null ? downloadPath : new Uri(addon.DownloadUrl).AbsolutePath);
            string targetAddonFile = Path.Combine(targetPathAddon, addonFileName + ".addon64");
            File.Copy(downloadPath, targetAddonFile, true);
            _logger.LogInformation("    Installed addon binary: {File}", Path.GetFileName(targetAddonFile));

            // Install bundled effects if any
            if (tempPathEffects != null)
            {
                string targetPathEffects = string.IsNullOrEmpty(addon.EffectInstallPath)
                    ? Path.Combine(targetBaseDir, "reshade-shaders", "Shaders")
                    : Path.Combine(targetBaseDir, addon.EffectInstallPath);
                
                MoveFiles(tempPathEffects, targetPathEffects);
                _logger.LogInformation("    Installed bundled effect files");
            }

            // Cleanup
            File.Delete(downloadPath);
            if (tempPath != null)
            {
                Directory.Delete(tempPath, true);
            }

            DownloadedFiles++;
            UpdateDownloadSpeed();
            _logger.LogInformation("<<< Installed addon {Addon} ({Downloaded}/{Total})",
                addon.Name, DownloadedFiles, TotalFiles);
            return InstallResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! Failed to install addon {Addon}", addon.Name);
            DownloadedFiles++;
            UpdateDownloadSpeed();
            return InstallResult.Failed;
        }
    }

    /// <summary>
    /// Recursively move files from source to target
    /// Reference: MainWindow.xaml.cs -> MoveFiles
    /// </summary>
    private static void MoveFiles(string sourcePath, string targetPath)
    {
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        foreach (string source in Directory.EnumerateFiles(sourcePath))
        {
            string ext = Path.GetExtension(source);
            if (ext == ".addon" || ext == ".addon32" || ext == ".addon64")
            {
                continue;
            }

            string target = Path.Combine(targetPath, Path.GetFileName(source));
            File.Copy(source, target, true);
        }

        // Copy subdirectories recursively
        foreach (string source in Directory.EnumerateDirectories(sourcePath))
        {
            string target = Path.Combine(targetPath, Path.GetFileName(source));
            MoveFiles(source, target);
        }
    }

    /// <summary>
    /// Write search paths to ReShade.ini
    /// Reference: MainWindow.xaml.cs -> WriteSearchPaths
    /// </summary>
    private void WriteSearchPaths(string targetDir)
    {
        try
        {
            string configPath = Path.Combine(targetDir, "ReShade.ini");
            
            // Create minimal config if it doesn't exist
            if (!File.Exists(configPath))
            {
                var lines = new List<string>
                {
                    "[GENERAL]",
                    "EffectSearchPaths=.\\reshade-shaders\\Shaders\\**",
                    "TextureSearchPaths=.\\reshade-shaders\\Textures\\**",
                    "",
                    "[INPUT]",
                    "KeyOverlay=36,0,0,0",
                    "GamepadNavigation=0",
                    ""
                };
                File.WriteAllLines(configPath, lines);
                _logger.LogInformation("Created ReShade.ini at {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write search paths");
        }
    }

    /// <summary>
    /// Update download speed calculation
    /// </summary>
    private void UpdateDownloadSpeed()
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
        
        if (elapsed >= 1.0) // Update every second
        {
            var bytesDiff = ReShadePackDownloadBytes - _lastDownloadBytes;
            if (bytesDiff > 0 && elapsed > 0)
            {
                DownloadSpeedBytesPerSec = (long)(bytesDiff / elapsed);
            }
            else
            {
                DownloadSpeedBytesPerSec = 0;
            }
            
            _lastDownloadBytes = ReShadePackDownloadBytes;
            _lastSpeedUpdate = now;
        }
    }

    /// <summary>
    /// Fetch effect packages from official API
    /// Reference: SelectEffectsPage.xaml.cs -> constructor
    /// </summary>
    public async Task<List<EffectPackage>> FetchEffectPackagesAsync(bool useProxy, CancellationToken cancellationToken)
    {
        try
        {
            var url = ReShadeDownloadServer.GetEffectPackagesUrl(useProxy);
            _logger.LogInformation("Fetching effect packages from {Url}", url);
            
            using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
            var packagesIni = new IniFile(stream);
            
            var packages = new List<EffectPackage>();
            
            foreach (string packageSection in packagesIni.GetSections())
            {
                bool required = packagesIni.GetString(packageSection, "Required") == "1";
                // Initialize Selected based on Required or Enabled flag
                bool? enabled;
                if (required)
                {
                    enabled = true; // Required packages are always enabled
                }
                else
                {
                    // Non-required packages check Enabled flag
                    string enabledStr = packagesIni.GetString(packageSection, "Enabled", "0");
                    enabled = enabledStr == "1"; // true if explicitly enabled, false otherwise
                }

                packagesIni.GetValue(packageSection, "EffectFiles", out string[] effectFiles);
                packagesIni.GetValue(packageSection, "DenyEffectFiles", out string[] denyEffectFiles);

                var item = new EffectPackage
                {
                    Selected = enabled,
                    Modifiable = !required,
                    Name = packagesIni.GetString(packageSection, "PackageName"),
                    Description = packagesIni.GetString(packageSection, "PackageDescription"),
                    InstallPath = packagesIni.GetString(packageSection, "InstallPath", string.Empty),
                    TextureInstallPath = packagesIni.GetString(packageSection, "TextureInstallPath", string.Empty),
                    DownloadUrl = packagesIni.GetString(packageSection, "DownloadUrl"),
                    RepositoryUrl = packagesIni.GetString(packageSection, "RepositoryUrl"),
                    EffectFiles = effectFiles?.Where(x => denyEffectFiles == null || !denyEffectFiles.Contains(x))
                        .Select(x => new EffectFile { FileName = x, Selected = false }).ToArray(),
                    DenyEffectFiles = denyEffectFiles
                };

                packages.Add(item);
                _logger.LogInformation("Loaded effect package: {Name}, Selected: {Selected}, DownloadUrl: {Url}", 
                    item.Name, item.Selected, item.DownloadUrl);
            }

            _logger.LogInformation("Fetched {Count} effect packages total", packages.Count);
            return packages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch effect packages");
            return new List<EffectPackage>();
        }
    }

    /// <summary>
    /// Fetch addons from official API
    /// Reference: SelectAddonsPage.xaml.cs -> constructor
    /// </summary>
    public async Task<List<Addon>> FetchAddonsAsync(bool useProxy, CancellationToken cancellationToken)
    {
        try
        {
            var url = ReShadeDownloadServer.GetAddonsUrl(useProxy);
            _logger.LogInformation("Fetching addons from {Url}", url);
            
            using var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
            var addonsIni = new IniFile(stream);
            
            var addons = new List<Addon>();
            int totalAddons = 0;
            int addonsWithoutUrl = 0;
            
            foreach (string addon in addonsIni.GetSections())
            {
                totalAddons++;
                
                // Prefer 64-bit download URL
                string downloadUrl = addonsIni.GetString(addon, "DownloadUrl64");
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = addonsIni.GetString(addon, "DownloadUrl");
                }

                var item = new Addon
                {
                    Name = addonsIni.GetString(addon, "PackageName"),
                    Description = addonsIni.GetString(addon, "PackageDescription"),
                    EffectInstallPath = addonsIni.GetString(addon, "EffectInstallPath", string.Empty),
                    DownloadUrl = downloadUrl,
                    RepositoryUrl = addonsIni.GetString(addon, "RepositoryUrl")
                };

                addons.Add(item);
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    addonsWithoutUrl++;
                    _logger.LogInformation("Loaded addon: {Name}, Enabled: False, DownloadUrl: (empty)", item.Name);
                }
                else
                {
                    _logger.LogInformation("Loaded addon: {Name}, Enabled: True, DownloadUrl: {Url}", 
                        item.Name, downloadUrl);
                }
            }

            _logger.LogInformation("Fetched {Count} addons total, {EnabledCount} with download URLs ({DisabledCount} without URLs)", 
                totalAddons, addons.Count(a => a.Enabled), addonsWithoutUrl);
            return addons;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch addons");
            return new List<Addon>();
        }
    }
}
