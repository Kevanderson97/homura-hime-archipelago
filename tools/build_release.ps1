<#
.SYNOPSIS
    Builds everything a player needs, into dist/:
      HomuraHimeArchipelago-plugin-<version>.zip   the BepInEx plugin (extract into the game folder)
      homura_hime.apworld                          the Archipelago world (put into Archipelago's custom_worlds folder)
      Homura-Hime.yaml                             a sample player yaml with every option

.PARAMETER GameDir
    The Homura Hime install folder with BepInEx 5 (the plugin is compiled against the game's own assemblies; none of
    them end up in the release). Default: the HOMURA_HIME_DIR environment variable, else the usual Steam location.

.PARAMETER Python
    A Python 3 interpreter for tools/build_apworld.py (default: python).
#>
param(
    [string]$GameDir = $(if ($env:HOMURA_HIME_DIR) { $env:HOMURA_HIME_DIR } else { "C:\Program Files (x86)\Steam\steamapps\common\Homura Hime" }),
    [string]$Python = "python"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$staging = Join-Path $dist "staging"

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $staging | Out-Null

# 1. the plugin
dotnet build (Join-Path $root "plugin") -c Release -p:GameDir="$GameDir" -v q
if ($LASTEXITCODE -ne 0) { throw "plugin build failed" }
$version = ([xml](Get-Content (Join-Path $root "plugin\HomuraHimeAP.csproj"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$build = Join-Path $root "plugin\bin\Release"
# The game ships Newtonsoft.Json itself, so only the plugin and the Archipelago client library are packed. The folder
# layout matches the game folder, so players extract the zip straight into it.
$pluginFolder = Join-Path $staging "BepInEx\plugins\HomuraHimeArchipelago"
New-Item -ItemType Directory -Force $pluginFolder | Out-Null
Copy-Item (Join-Path $build "HomuraHimeArchipelago.dll"), (Join-Path $build "Archipelago.MultiClient.Net.dll") $pluginFolder
Copy-Item (Join-Path $root "LICENSE"), (Join-Path $root "THIRD-PARTY-NOTICES.md"), (Join-Path $root "AI-Disclosure.md") $pluginFolder
# Zip with forward slashes in the entry names (Windows PowerShell's Compress-Archive writes backslashes, which some
# unzip tools extract as odd file names).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open((Join-Path $dist "HomuraHimeArchipelago-plugin-$version.zip"), "Create")
foreach ($file in Get-ChildItem $staging -Recurse -File) {
    $entry = $file.FullName.Substring($staging.Length + 1).Replace("\", "/")
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry)
}
$zip.Dispose()

# 2. the world
& $Python (Join-Path $root "tools\build_apworld.py")
if ($LASTEXITCODE -ne 0) { throw "apworld build failed" }

# 3. the sample yaml
Copy-Item (Join-Path $root "apworld\sample\Homura-Hime.yaml") $dist

Remove-Item $staging -Recurse -Force

# everything the README promises must be there
foreach ($expected in "HomuraHimeArchipelago-plugin-$version.zip", "homura_hime.apworld", "Homura-Hime.yaml") {
    if (-not (Test-Path (Join-Path $dist $expected))) { throw "the release is missing $expected" }
}
# the versions inside the files must be the ones in the name: the dll reports the project version, and the manifest of the
# .apworld must be one Archipelago accepts (otherwise the world loads as 0.0.0)
$dllVersion = (Get-Item (Join-Path $build "HomuraHimeArchipelago.dll")).VersionInfo.FileVersion
if ($dllVersion -ne "$version.0") { throw "the plugin dll reports version $dllVersion, expected $version.0" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $dist "homura_hime.apworld"))
$entry = $archive.GetEntry("homura_hime/archipelago.json")
$manifest = (New-Object IO.StreamReader($entry.Open())).ReadToEnd() | ConvertFrom-Json
$archive.Dispose()
if ($manifest.world_version -ne $version) { throw "the apworld is version $($manifest.world_version), the plugin $version" }
if (-not $manifest.compatible_version -or -not $manifest.version) { throw "the apworld manifest lacks the container versions Archipelago needs" }

Write-Host "Release files:" -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object { Write-Host ("  {0}  ({1:N0} KB)" -f $_.Name, ($_.Length / 1KB)) }
