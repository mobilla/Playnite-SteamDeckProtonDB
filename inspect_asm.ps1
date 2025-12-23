$path = 'Z:\src\playnite_protondb\SteamDeckProtonDb\bin\Debug\SteamDeckProtonDb.dll'
$asm = [Reflection.Assembly]::LoadFrom($path)
Write-Host "Assembly: $($asm.FullName)"
$asm.GetManifestResourceNames() | ForEach-Object { Write-Host "Resource: $_" }
Write-Host "--- End ---"