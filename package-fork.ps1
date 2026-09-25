# Thunderstore / r2modman package for the unofficial stability fork.
# Usage: .\package-fork.ps1 [-ValheimPath "G:/Steam/steamapps/common/Valheim"]
param(
  [string]$ValheimPath = "G:/Steam/steamapps/common/Valheim"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$meta = Join-Path $root "thunderstore"
$manifest = Get-Content (Join-Path $meta "manifest.json") -Raw | ConvertFrom-Json
$name = $manifest.name
$version = $manifest.version_number

if ($manifest.description.Length -gt 250) { throw "manifest description is over 250 chars" }
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "version_number must be Major.Minor.Patch: $version" }
if ($name -notmatch '^[A-Za-z0-9_]+$') { throw "name may only contain letters, digits and underscores: $name" }

Write-Host "Building $name $version (Release, no deploy)..." -ForegroundColor Cyan
dotnet build (Join-Path $root "BestAutoSort.csproj") -c Release -p:Deploy=false -p:ValheimPath=$ValheimPath
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

$dll = Join-Path $root "bin/Release/net472/BestAutoSort.dll"
if (-not (Test-Path $dll)) { throw "Expected output missing: $dll" }

# Archive path -> source file. Thunderstore wants manifest/icon/README at the root.
$entries = [ordered]@{
  "manifest.json"            = Join-Path $meta "manifest.json"
  "README.md"                = Join-Path $meta "README.md"
  "icon.png"                 = Join-Path $root "icon.png"
  "CHANGELOG.md"             = Join-Path $root "CHANGELOG.md"
  "LICENSE"                  = Join-Path $root "LICENSE"
  "plugins/BestAutoSort.dll" = $dll
}

$outDir = Join-Path $root "dist"
New-Item -ItemType Directory -Force $outDir | Out-Null
$zipPath = Join-Path $outDir "$name-$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }

# System.IO.Compression with explicit forward-slash entry names: Windows
# PowerShell 5.1 Compress-Archive writes backslashes, which break extraction.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($key in $entries.Keys) {
    $src = $entries[$key]
    if (-not (Test-Path $src)) { throw "Missing package file: $src" }
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $src, $key, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
}
finally {
  $zip.Dispose()
}

Write-Host "Packed: $zipPath" -ForegroundColor Green
$entries.Keys | ForEach-Object { Write-Host "  $_" }
