param(
    [string] $Architecture = "x64",
    [string] $Version = "1.0.0",
    [string] $Output = "build/HoYoShadeHub",
    [int] $DiffCount = 5,
    [array] $DiffTags = @()
)

$ErrorActionPreference = "Stop";

# Ensure temporary and build folders exist
New-Item -ItemType Directory -Force -Path "src/HoYoShadeHub.Setup/Assets"
New-Item -ItemType Directory -Force -Path "build/release/package"
New-Item -ItemType Directory -Force -Path "build/release/manifest"

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
Write-Host "Compressing app files into HoYoShadeHub.7z..." -ForegroundColor Green
Remove-Item "src/HoYoShadeHub.Setup/Assets/HoYoShadeHub.7z" -ErrorAction SilentlyContinue
# Compress the contents of the app version folder
Compress-7Zip -ArchiveFileName "HoYoShadeHub.7z" -Path "$Output/app-$Version" -OutputPath "src/HoYoShadeHub.Setup/Assets/" -CompressionLevel Ultra

# 5. Build the final Setup program with the embedded HoYoShadeHub.7z
Write-Host "Building final Setup installer..." -ForegroundColor Green
dotnet publish src/HoYoShadeHub.Setup -c Release -r "win-$Architecture" -o "build/setup_final/" -p:Version=$Version
$setupInstaller = "HoYoShadeHub_Setup_$($Version)_$($Architecture).exe"
$setupInstallerPath = "build/release/package/$setupInstaller"
Move-Item "build/setup_final/HoYoShadeHub.Setup.exe" $setupInstallerPath -Force
Remove-Item "build/setup_final" -Recurse -Force -ErrorAction SilentlyContinue

# Clean up temporary Assets/HoYoShadeHub.7z
Remove-Item "src/HoYoShadeHub.Setup/Assets/HoYoShadeHub.7z" -Force -ErrorAction SilentlyContinue

# 6. Compress Setup package update files (.7z)
$setupFilesPackage = "HoYoShadeHub_Setup_Files_$($Version)_$($Architecture).7z"
$setupFilesPackagePath = "build/release/package/$setupFilesPackage"
Write-Host "Creating setup update files package $setupFilesPackage ..." -ForegroundColor Green;
Compress-7Zip -ArchiveFileName $setupFilesPackage -Path "$Output/app-$Version" -OutputPath 'build/release/package/' -CompressionLevel Ultra;

# 7. Create the portable package (standard .7z package)
$portablePackage = "HoYoShadeHub_Portable_$($Version)_$($Architecture).7z";
$portablePackagePath = "build/release/package/$portablePackage"
Write-Host "Creating portable package $portablePackage ..." -ForegroundColor Green;
Compress-7Zip -ArchiveFileName $portablePackage -Path $Output -OutputPath 'build/release/package/' -CompressionLevel Ultra -PreserveDirectoryRoot;


# 8. Pack manifests and files via BuildTool for both portable and setup
Write-Host "Packing portable release..." -ForegroundColor Green;
dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- pack $Output -v $Version -a $Architecture -t portable;

Write-Host "Packing setup release..." -ForegroundColor Green;
dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- pack "$Output/app-$Version" -v $Version -a $Architecture -t setup;


# 9. Generate diffs and release info
if ($DiffTags.Count -eq 0 -and $DiffCount -gt 0) {
    if ($env:GITHUB_TOKEN) {
        $json = Invoke-WebRequest 'https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases' -Headers @{ Authorization = "Bearer $env:GITHUB_TOKEN" } | ConvertFrom-Json;
    }
    else {
        $json = Invoke-WebRequest 'https://api.github.com/repos/DuolaD/HoYoShade-Hub/releases' | ConvertFrom-Json;
    }

    $pre = $true;
    $stableCount = 0;
    foreach ($r in $json) {
        if ($r.tag_name -eq $Version) {
            continue;
        }
        if ($pre -and $r.tag_name -like '*-*') {
            $DiffTags += $r.tag_name;
            Write-Host "Creating diff for $($r.tag_name) (portable)..." -ForegroundColor Green;
            dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np $Output -nv $Version -ov $r.tag_name -a $Architecture -t portable;
            
            Write-Host "Creating diff for $($r.tag_name) (setup)..." -ForegroundColor Green;
            dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np "$Output/app-$Version" -nv $Version -ov $r.tag_name -a $Architecture -t setup;
        }
        if ($r.tag_name -notlike '*-*') {
            $pre = $false;
            $stableCount += 1;
            $DiffTags += $r.tag_name;
            Write-Host "Creating diff for $($r.tag_name) (portable)..." -ForegroundColor Green;
            dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np $Output -nv $Version -ov $r.tag_name -a $Architecture -t portable;

            Write-Host "Creating diff for $($r.tag_name) (setup)..." -ForegroundColor Green;
            dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np "$Output/app-$Version" -nv $Version -ov $r.tag_name -a $Architecture -t setup;
        }
        if ($stableCount -ge $DiffCount) {
            break;
        }
    }
}
elseif ($DiffTags.Count -gt 0) {
    foreach ($tag in $DiffTags) {
        Write-Host "Creating diff for $tag (portable)..." -ForegroundColor Green;
        dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np $Output -nv $Version -ov $tag -a $Architecture -t portable;

        Write-Host "Creating diff for $tag (setup)..." -ForegroundColor Green;
        dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- diff -np "$Output/app-$Version" -nv $Version -ov $tag -a $Architecture -t setup;
    }
}


# 10. Generate release info for portable and setup, then combine them
$diffArgs = if ($DiffTags.Count -gt 0) { @("-d", ($DiffTags -join " ")) } else { @() }

Write-Host "Creating portable release info..." -ForegroundColor Green;
dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- release create "build/release_info_$($Version)_$($Architecture)_portable.json" -v $Version -a $Architecture -t portable -p $portablePackagePath @diffArgs;

Write-Host "Creating setup release info..." -ForegroundColor Green;
dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- release create "build/release_info_$($Version)_$($Architecture)_setup.json" -v $Version -a $Architecture -t setup -p $setupFilesPackagePath -sp $setupInstallerPath @diffArgs;

Write-Host "Combining release info files..." -ForegroundColor Green;
dotnet run --project 'src/BuildTool' -c Release -p:Platform=x64 -- release combine "build/release_info_$($Version)_$($Architecture).json" -i "build/release_info_$($Version)_$($Architecture)_portable.json" "build/release_info_$($Version)_$($Architecture)_setup.json";


# Clean up temporary build files
Remove-Item 'build/release/temp' -Recurse -Force -ErrorAction SilentlyContinue;
Remove-Item "build/release_info_$($Version)_$($Architecture)_portable.json" -Force -ErrorAction SilentlyContinue;
Remove-Item "build/release_info_$($Version)_$($Architecture)_setup.json" -Force -ErrorAction SilentlyContinue;
Remove-Item $hollowSetupExe -Force -ErrorAction SilentlyContinue;
Remove-Item "$Output/app-$Version/HoYoShadeHub.Setup.exe" -Force -ErrorAction SilentlyContinue;

Write-Host "Packaging completed successfully!" -ForegroundColor Green;
