param(
    [string] $Architecture = "x64",
    [string] $Version = "1.0.0",
    [string] $Output = "build/HoYoShadeHub",
    [int] $DiffCount = 5,
    [array] $DiffTags = @()
)

$ErrorActionPreference = "Stop";

Write-Host "========================================" -ForegroundColor Cyan;
Write-Host "  Fast Build Mode (Test)" -ForegroundColor Cyan;
Write-Host "  - Setup Installer Build: ENABLED (Fast Compression)" -ForegroundColor Green;
Write-Host "  - HDiff creation: DISABLED" -ForegroundColor Yellow;
Write-Host "========================================" -ForegroundColor Cyan;
Write-Host "";

$cleanupTargets = @(
    $Output
)
foreach ($target in $cleanupTargets) {
    Remove-Item -Recurse -Force $target -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $Output -Force | Out-Null
New-Item -ItemType Directory -Force -Path "src/HoYoShadeHub.Setup/Assets"

# 1. Run the build script to compile launcher and main app
.\build.ps1 -Version $Version -Architecture $Architecture -Output $Output;

# 2. Build the hollow Setup program (uninstaller)
Write-Host "Building hollow Setup program for uninstallation..." -ForegroundColor Green
dotnet publish src/HoYoShadeHub.Setup -c Release -r "win-$Architecture" -o "build/setup_temp/" -p:Version=$Version
$hollowSetupExe = "build/HoYoShadeHub.Setup.exe"
Move-Item "build/setup_temp/HoYoShadeHub.Setup.exe" $hollowSetupExe -Force
Remove-Item "build/setup_temp" -Recurse -Force -ErrorAction SilentlyContinue

# 3. Copy hollow Setup program to the app version folder
Copy-Item $hollowSetupExe "$Output/app-$Version/HoYoShadeHub.Setup.exe" -Force

# 4. Compress the app version folder into Assets/HoYoShadeHub.7z (for embedding in the Setup installer)
if (!(Get-Module -Name 7Zip4Powershell -ListAvailable)) {
    Install-Module -Name 7Zip4Powershell -Force;
}
Write-Host "Compressing app files into HoYoShadeHub.7z (Fast Mode)..." -ForegroundColor Green
Remove-Item "src/HoYoShadeHub.Setup/Assets/HoYoShadeHub.7z" -ErrorAction SilentlyContinue
# Compress the contents of the app version folder with Fast level for testing speed
Compress-7Zip -ArchiveFileName "HoYoShadeHub.7z" -Path "$Output/app-$Version" -OutputPath "src/HoYoShadeHub.Setup/Assets/" -CompressionLevel Fast

# 5. Build the final Setup program with the embedded HoYoShadeHub.7z
Write-Host "Building final Setup installer..." -ForegroundColor Green
dotnet publish src/HoYoShadeHub.Setup -c Release -r "win-$Architecture" -o "build/setup_final/" -p:Version=$Version
$setupInstaller = "HoYoShadeHub_Setup_$($Version)_$($Architecture).exe"
$setupInstallerPath = "build/$setupInstaller"
Move-Item "build/setup_final/HoYoShadeHub.Setup.exe" $setupInstallerPath -Force
Remove-Item "build/setup_final" -Recurse -Force -ErrorAction SilentlyContinue

# Clean up temporary Assets/HoYoShadeHub.7z and hollow uninstaller
Remove-Item "src/HoYoShadeHub.Setup/Assets/HoYoShadeHub.7z" -Force -ErrorAction SilentlyContinue
Remove-Item $hollowSetupExe -Force -ErrorAction SilentlyContinue
Remove-Item "$Output/app-$Version/HoYoShadeHub.Setup.exe" -Force -ErrorAction SilentlyContinue

# Build completed
Write-Host "";
Write-Host "========================================" -ForegroundColor Green;
Write-Host "  Build Completed Successfully!" -ForegroundColor Green;
Write-Host "========================================" -ForegroundColor Green;
Write-Host "Portable folder output: $Output" -ForegroundColor Cyan;
Write-Host "Setup installer output: $setupInstallerPath" -ForegroundColor Cyan;
Write-Host "";
