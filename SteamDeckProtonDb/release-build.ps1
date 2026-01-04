param(
    [string]$Version = $null,
    [switch]$SkipTests = $false
)

# Release build and packaging script for SteamDeckProtonDb plugin

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$parentRoot = Split-Path -Parent $scriptRoot
$solution = Join-Path $scriptRoot 'SteamDeckProtonDb.sln'
$proj = Join-Path $scriptRoot 'SteamDeckProtonDb.csproj'
$extensionYaml = Join-Path $scriptRoot 'extension.yaml'
$releaseDir = Join-Path $scriptRoot 'bin' 'Release'
$packageDir = Join-Path $scriptRoot 'package'
$releasesDir = Join-Path $parentRoot 'releases'

function Read-Yaml {
    param([string]$Path)
    $yaml = @{}
    Get-Content $Path | ForEach-Object {
        if ($_ -match '^\s*([^:]+):\s*(.+)$') {
            $yaml[$matches[1].Trim()] = $matches[2].Trim()
        }
    }
    return $yaml
}

function Update-Yaml {
    param([string]$Path, [hashtable]$Updates)
    $content = Get-Content $Path -Raw
    foreach ($key in $Updates.Keys) {
        $pattern = "(?m)^$key:.*"
        $replacement = "$key`: $($Updates[$key])"
        $content = $content -replace $pattern, $replacement
    }
    Set-Content -Path $Path -Value $content -Encoding UTF8 -NoNewline
    Write-Host "Updated $Path - New values: $($Updates.Keys -join ', ')"
}

function Validate-Files {
    param([string]$OutputDir)
    $requiredFiles = @(
        'SteamDeckProtonDb.dll',
        'extension.yaml',
        'SteamDeckProtonDbSettingsView.xaml',
        'App.config'
    )
    
    $missing = @()
    foreach ($file in $requiredFiles) {
        $path = Join-Path $OutputDir $file
        if (-not (Test-Path $path)) {
            $missing += $file
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Error "Missing required files for packaging: $($missing -join ', ')"
        return $false
    }
    return $true
}

function Find-PlayniteToolbox {
    # Check common Playnite installation locations
    $locations = @(
        "C:\Program Files\Playnite\Toolbox.exe",
        "C:\Program Files (x86)\Playnite\Toolbox.exe",
        "$env:LOCALAPPDATA\Playnite\Toolbox.exe"
    )
    
    foreach ($location in $locations) {
        if (Test-Path $location) {
            return $location
        }
    }
    
    # Try to find in PATH
    try {
        $result = Get-Command Toolbox.exe -ErrorAction Stop
        return $result.Source
    } catch {
        return $null
    }
}

function Create-PextPackage {
    param([string]$SourceDir, [string]$OutputDir, [string]$PackageName)
    
    $toolboxExe = Find-PlayniteToolbox
    if (-not $toolboxExe) {
        Write-Warning "Playnite Toolbox.exe not found. Falling back to manual ZIP packaging."
        Create-ZipPackage -SourceDir $SourceDir -OutputDir $OutputDir -PackageName $PackageName
        return
    }
    
    Write-Host "Using Playnite Toolbox to create .pext package..."
    Write-Host "Toolbox location: $toolboxExe"
    
    # Create temporary staging directory for the extension folder
    $stagingDir = Join-Path $packageDir $PackageName
    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force
    }
    
    Write-Host "Creating staging directory: $stagingDir"
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
    
    Write-Host "Copying files to staging directory..."
    Copy-Item -Path (Join-Path $SourceDir '*') -Destination $stagingDir -Recurse -Force
    
    Write-Host "Running Playnite Toolbox to pack extension..."
    & $toolboxExe pack $stagingDir $OutputDir
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Playnite Toolbox pack failed with exit code $LASTEXITCODE"
        exit 1
    }
    
    Write-Host "Cleaning up staging directory..."
    Remove-Item $stagingDir -Recurse -Force
    
    $pextFile = Join-Path $OutputDir "$PackageName.pext"
    if (Test-Path $pextFile) {
        Write-Host "Package created successfully: $pextFile"
    }
}

function Create-ZipPackage {
    param([string]$SourceDir, [string]$OutputDir, [string]$PackageName)
    
    $outputPath = Join-Path $OutputDir "$PackageName.zip"
    
    if (Test-Path $outputPath) {
        Remove-Item $outputPath -Force
    }
    
    $stagingDir = Join-Path $packageDir $PackageName
    if (Test-Path $stagingDir) {
        Remove-Item $stagingDir -Recurse -Force
    }
    
    Write-Host "Creating package staging directory: $stagingDir"
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
    
    Write-Host "Copying files to staging directory..."
    Copy-Item -Path (Join-Path $SourceDir '*') -Destination $stagingDir -Recurse -Force
    
    Write-Host "Creating ZIP package: $outputPath"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageDir, $outputPath, 'Optimal', $false)
    
    Write-Host "Cleaning up staging directory..."
    Remove-Item $stagingDir -Recurse -Force
    
    Write-Host "Package created successfully: $outputPath"
}

# Main script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SteamDeckProtonDb Release Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Read current version from extension.yaml
$yaml = Read-Yaml $extensionYaml
$currentVersion = $yaml['Version']
Write-Host "Current version: $currentVersion"

# Update version if provided
if ($Version) {
    Write-Host "Updating version to: $Version" -ForegroundColor Yellow
    Update-Yaml -Path $extensionYaml -Updates @{ Version = $Version }
    $currentVersion = $Version
    
    # Verify the update was successful
    $updatedYaml = Read-Yaml $extensionYaml
    $verifyVersion = $updatedYaml['Version']
    Write-Host "Verified extension.yaml version: $verifyVersion" -ForegroundColor Green
    
    if ($verifyVersion -ne $Version) {
        Write-Error "Failed to update extension.yaml version. Expected: $Version, Got: $verifyVersion"
        exit 1
    }
}

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Cyan
dotnet clean $proj -c Release 2>&1 | Out-Null

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
$nugetPath = Join-Path $scriptRoot 'nuget.exe'
if (-not (Test-Path $nugetPath)) {
    Write-Host "Downloading nuget.exe..."
    $url = 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe'
    try {
        Invoke-WebRequest -Uri $url -OutFile $nugetPath -UseBasicParsing
    } catch {
        Write-Error "Failed to download nuget.exe: $_"
        exit 1
    }
}
& $nugetPath restore $solution
if ($LASTEXITCODE -ne 0) {
    Write-Error "nuget restore failed with exit code $LASTEXITCODE"
    exit 1
}

# Run tests unless skipped
if (-not $SkipTests) {
    Write-Host "`nRunning tests..." -ForegroundColor Cyan
    $testProj = Join-Path $parentRoot 'SteamDeckProtonDb.Tests' 'SteamDeckProtonDb.Tests.csproj'
    Push-Location (Split-Path $testProj -Parent)
    try {
        dotnet run --configuration Release
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Tests failed with exit code $LASTEXITCODE"
            exit 1
        }
    } finally {
        Pop-Location
    }
}

# Build in Release configuration
Write-Host "`nBuilding in Release configuration..." -ForegroundColor Cyan
dotnet build $proj -c Release /property:GenerateFullPaths=true "/consoleloggerparameters:NoSummary;ForceNoAlign"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Release build failed with exit code $LASTEXITCODE"
    exit 1
}

# Verify required files exist
Write-Host "`nValidating output files..." -ForegroundColor Cyan
if (-not (Validate-Files $releaseDir)) {
    exit 1
}

# Create releases directory if it doesn't exist
if (-not (Test-Path $releasesDir)) {
    New-Item -ItemType Directory -Path $releasesDir -Force | Out-Null
}

# Package for release
Write-Host "`nPackaging for release..." -ForegroundColor Cyan
$packageName = "SteamDeckProtonDb_v$currentVersion"

Create-PextPackage -SourceDir $releaseDir -OutputDir $releasesDir -PackageName $packageName

# Find the created package (could be .pext or .zip)
# Note: Playnite Toolbox uses ID_major_minor.pext naming, so look for any .pext file first
$pextFiles = Get-ChildItem -Path $releasesDir -Filter "*.pext" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
$zipFiles = Get-ChildItem -Path $releasesDir -Filter "*.zip" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending

$packagePath = $null
$packageType = $null

if ($pextFiles -and $pextFiles.Count -gt 0) {
    $packagePath = $pextFiles[0].FullName
    $packageType = ".pext"
} elseif ($zipFiles -and $zipFiles.Count -gt 0) {
    $packagePath = $zipFiles[0].FullName
    $packageType = ".zip"
} else {
    Write-Error "Package file not found in $releasesDir"
    exit 1
}

# Display package info
$packageItem = Get-Item $packagePath
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Release package created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Package: $($packageItem.Name)"
Write-Host "Type: $packageType"
Write-Host "Size: $([math]::Round($packageItem.Length / 1KB, 2)) KB"
Write-Host "Path: $packagePath"
Write-Host ""

if ($packageType -eq ".pext") {
    Write-Host "To install:"
    Write-Host "  Users can double-click the .pext file to auto-install, or"
    Write-Host "  Place in: %APPDATA%\Playnite\Extensions\SteamDeckProtonDb"
} else {
    Write-Host "To install:"
    Write-Host "  Extract contents to: %APPDATA%\Playnite\Extensions\SteamDeckProtonDb"
}

Write-Host ""
Write-Host "Ready to upload to GitHub Releases!"
