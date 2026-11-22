# Builds the current working copy of TempWidget into gitignored .\dist\ so
# Start.cmd and dist\TempWidget.exe always match the latest local commit (including WIP).
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.

param(
  [switch]$Quiet,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$distDir = Join-Path $root 'dist'
$exePath = Join-Path $distDir 'TempWidget.exe'
$markerPath = Join-Path $distDir '.built-commit'

function Say([string]$msg) {
  if (-not $Quiet) { Write-Host $msg }
}

$head = ''
if (Get-Command git -ErrorAction SilentlyContinue) {
  $gitOut = & git -C $root rev-parse HEAD 2>$null
  if ($LASTEXITCODE -eq 0 -and $gitOut) { $head = "$gitOut".Trim() }
}

if (-not $Force -and (Test-Path -LiteralPath $exePath) -and $head -and (Test-Path -LiteralPath $markerPath)) {
  $built = (Get-Content -LiteralPath $markerPath -TotalCount 1 -ErrorAction SilentlyContinue)
  if ($built -and ($built.Trim() -eq $head)) {
    Say "TempWidget dist\ is already up to date ($($head.Substring(0, [Math]::Min(7, $head.Length))))."
    exit 0
  }
}

# Refuse to clobber a running dist\TempWidget.exe so an elevated/active instance isn't half-overwritten.
if (Test-Path -LiteralPath $exePath) {
  try {
    $fs = [System.IO.File]::Open($exePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $fs.Close()
  } catch {
    Write-Error "dist\TempWidget.exe is currently running. Close it from the tray (Exit) and re-run build-local.ps1."
    exit 2
  }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

$offlineDeps = Join-Path $root '.offline-deps'
$useOffline = (Test-Path -LiteralPath (Join-Path $offlineDeps 'LibreHardwareMonitorLib.dll'))

$buildArgs = @('build', (Join-Path $root 'TempWidget.csproj'), '-c', 'Release', '--nologo')
if ($useOffline) {
  $env:DOTNET_CLI_HOME = Join-Path $root '.dotnet-home'
  $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
  $env:MSBuildEnableWorkloadResolver = 'false'
  $buildArgs += @("-p:OfflineReferenceDirectory=$offlineDeps", '-p:AutomaticallyUseReferenceAssemblyPackages=false', "-p:RestoreSources=$offlineDeps")
}

Say '[TempWidget] Compiling Release (net48 win-x64)...'
$buildOut = & dotnet @buildArgs 2>&1
if ($LASTEXITCODE -ne 0) {
  $buildOut | ForEach-Object { Write-Host $_ }
  exit $LASTEXITCODE
}
if (-not $Quiet) {
  $buildOut | Select-Object -Last 6 | ForEach-Object { Write-Host $_ }
}

$binOut = Join-Path $root 'bin\Release\net48\win-x64'
if (-not (Test-Path -LiteralPath (Join-Path $binOut 'TempWidget.exe'))) {
  Write-Error "Build succeeded but $binOut\TempWidget.exe was not found."
  exit 1
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Force -LiteralPath (Join-Path $binOut 'TempWidget.exe') -Destination $distDir
$cfg = Join-Path $binOut 'TempWidget.exe.config'
if (Test-Path -LiteralPath $cfg) { Copy-Item -Force -LiteralPath $cfg -Destination $distDir }

# Copy runtime DLLs (from bin output, plus .offline-deps if offline build didn't copy transitive DLLs).
Get-ChildItem -LiteralPath $binOut -Filter '*.dll' -ErrorAction SilentlyContinue | ForEach-Object {
  Copy-Item -Force -LiteralPath $_.FullName -Destination $distDir
}
if ($useOffline) {
  Get-ChildItem -LiteralPath $offlineDeps -Filter '*.dll' -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item -Force -LiteralPath $_.FullName -Destination $distDir
  }
}

foreach ($doc in @('README.txt', 'PRIVACY.txt', 'THIRD-PARTY-NOTICES.txt')) {
  $srcDoc = Join-Path $root $doc
  if (Test-Path -LiteralPath $srcDoc) { Copy-Item -Force -LiteralPath $srcDoc -Destination $distDir }
}

if ($head) {
  $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
  [System.IO.File]::WriteAllText($markerPath, $head + "`n", $utf8NoBom)
}

Say "[TempWidget] Ready: $exePath"
exit 0
