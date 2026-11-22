# Builds an .msix for the packaged Store route (STORE.md, option B). Run on Windows.
# Needs the Windows SDK (for makeappx.exe). Usage:  .\build-msix.ps1 -AppFolder ..\dist\TempWidget
param([Parameter(Mandatory = $true)][string]$AppFolder, [string]$Output = "TempWidget-2.1.1-x64.msix")
$ErrorActionPreference = "Stop"

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows SDK." }

$manifest = Join-Path $PSScriptRoot "AppxManifest.xml"
if ((Get-Content $manifest -Raw) -match "REPLACE") { throw "Fill in the REPLACE values in AppxManifest.xml first (from Partner Center > Product identity)." }

$stage = Join-Path $env:TEMP "TempWidget-msix"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item $stage -ItemType Directory | Out-Null
Copy-Item (Join-Path $AppFolder "*") $stage -Recurse
Copy-Item $manifest $stage
New-Item (Join-Path $stage "Assets") -ItemType Directory | Out-Null
Copy-Item (Join-Path $PSScriptRoot "Assets\*") (Join-Path $stage "Assets") -Exclude "StoreListing-*"

# resources.pri lets Windows pick the right logo size (the .scale-200 / .targetsize-32 files).
$makepri = Join-Path $makeappx.DirectoryName "makepri.exe"
$config = Join-Path $env:TEMP "TempWidget-priconfig.xml"
& $makepri createconfig /cf $config /dl en-US /o
& $makepri new /pr $stage /cf $config /of (Join-Path $stage "resources.pri") /o
if ($LASTEXITCODE -ne 0) { throw "makepri failed." }

& $makeappx.FullName pack /d $stage /p $Output /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed." }
Write-Host "Created $Output. Upload it in Partner Center; the Store signs it for you."
