param(
    [string] $Architecture = "x64",
    [string] $Version = "1.0.0",
    [switch] $Dev
)

$ErrorActionPreference = "Stop";

$build = "build";
$starward = "$build\HoYoShadeHub";
if ($Dev) {
    $metadata = "$build/metadata/dev";
    $history = "$build/metadata/dev/history";
    $package = "$build/release/package/dev";
    $separate = "$build/release/separate_files/dev";
}
else {
    $metadata = "$build/metadata/v1";
    $history = "$build/metadata/v1/history";
    $package = "$build/release/package";
    $separate = "$build/release/separate_files";
}

$null = New-Item -Path $package -ItemType Directory -Force;
$null = New-Item -Path $separate -ItemType Directory -Force;
$null = New-Item -Path $metadata -ItemType Directory -Force;
$null = New-Item -Path $history -ItemType Directory -Force;

if (!(Get-Module -Name 7Zip4Powershell -ListAvailable)) {
    Install-Module -Name 7Zip4Powershell -Force;
}

$portableName = "HoYoShadeHub_Portable_$($Version)_$($Architecture).7z";
$portableFile = "$package/$portableName";

if (!(Test-Path $portableFile)) {
    Compress-7Zip -ArchiveFileName $portableName -Path $starward -OutputPath $package -CompressionLevel Ultra -PreserveDirectoryRoot;
}

$release = [ordered]@{
    Version           = $Version
    Architecture      = $Architecture
    BuildTime         = Get-Date
    DisableAutoUpdate = $false
    Install           = $null
    InstallSize       = 0
    InstallHash       = $null
    Portable          = "https://github.com/DuolaD/HoYoShade-Hub/releases/download/v$Version/$portableName"
    PortableSize      = (Get-Item $portableFile).Length
    PortableHash      = (Get-FileHash $portableFile).Hash
    SeparatePrefix    = "https://github.com/DuolaD/HoYoShade-Hub/releases/download/v$Version/"
};

if ($Dev) {
    $release.Portable = "https://github.com/DuolaD/HoYoShade-Hub/releases/download/v$Version-dev/$portableName";
    $release.SeparatePrefix = "https://github.com/DuolaD/HoYoShade-Hub/releases/download/v$Version-dev/";
}

Out-File -FilePath "$metadata/version_preview_$Architecture.json" -InputObject (ConvertTo-Json $release);

$path = @{l = "Path"; e = { [System.IO.Path]::GetRelativePath($starward, $_.FullName) } };
$size = @{l = "Size"; e = { $_.Length } };
$hash = @{l = "Hash"; e = { (Get-FileHash $_).Hash } };

$release.SeparateFiles = Get-ChildItem -Path $starward -File -Recurse | Select-Object -Property $path, $size, $hash;

Out-File -FilePath "$metadata/release_preview_$Architecture.json" -InputObject (ConvertTo-Json $release);
Copy-Item -Path "$metadata/release_preview_$Architecture.json" -Destination "$history/release_$($Version)_$Architecture.json" -Force;

foreach ($file in $release.SeparateFiles) {
    Move-Item -Path "$starward/$($file.Path)" -Destination "$separate/$($file.Hash)" -Force;
}
