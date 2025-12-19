param()

# Downloads nuget.exe if missing, restores NuGet packages, then builds the project.
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$nugetPath = Join-Path $scriptRoot 'nuget.exe'
$solution = Join-Path $scriptRoot 'SteamDeckProtonDb.sln'
$proj = Join-Path $scriptRoot 'SteamDeckProtonDb.csproj'

function Download-NuGet {
    param($OutPath)
    $url = 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe'
    Write-Host "Downloading nuget.exe from $url to $OutPath"
    Invoke-WebRequest -Uri $url -OutFile $OutPath -UseBasicParsing
}

if (-not (Test-Path $nugetPath)) {
    try {
        Download-NuGet -OutPath $nugetPath
    } catch {
        Write-Error "Failed to download nuget.exe: $_"
        exit 2
    }
}

Write-Host "Restoring NuGet packages for solution: $solution"
& $nugetPath restore $solution
if ($LASTEXITCODE -ne 0) {
    Write-Error "nuget restore failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Running dotnet build for project: $proj"
dotnet build $proj /property:GenerateFullPaths=true "/consoleloggerparameters:NoSummary;ForceNoAlign"
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build completed successfully."
