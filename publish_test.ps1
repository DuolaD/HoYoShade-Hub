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
Write-Host "  - 7z compression: DISABLED" -ForegroundColor Yellow;
Write-Host "  - HDiff creation: DISABLED" -ForegroundColor Yellow;
Write-Host "========================================" -ForegroundColor Cyan;
Write-Host "";

# Run the build script
.\build.ps1 -Version $Version -Architecture $Architecture -Output $Output;

# Build completed
Write-Host "";
Write-Host "========================================" -ForegroundColor Green;
Write-Host "  Build Completed Successfully!" -ForegroundColor Green;
Write-Host "========================================" -ForegroundColor Green;
Write-Host "Output directory: $Output" -ForegroundColor Cyan;
Write-Host "";
Write-Host "Note: This is a test build without packaging." -ForegroundColor Yellow;
Write-Host "Use publish.ps1 for full release builds." -ForegroundColor Yellow;
