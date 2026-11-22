# Builds the current working copy of TempWidget into gitignored .\dist\ so
# Start.cmd and dist\TempWidget.exe always match the latest local commit (including WIP).
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.

param(
  [switch]$Quiet,
  [switch]$Force,
  [switch]$SkipLauncher
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

$rootExe = Join-Path $root 'TempWidget.exe'
if (-not $Force -and (Test-Path -LiteralPath $exePath) -and (Test-Path -LiteralPath $rootExe) -and $head -and (Test-Path -LiteralPath $markerPath)) {
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

# Build a lightweight zero-console root launcher (.\TempWidget.exe, gitignored via *.exe)
# with the app icon so double-clicking TempWidget.exe directly in the repo root builds if
# stale and starts .\dist\TempWidget.exe cleanly without cluttering the root with DLLs.
# Skip when invoked by the running launcher itself (-SkipLauncher) or if already locked.
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$rootExe = Join-Path $root 'TempWidget.exe'
$needLauncher = (-not $SkipLauncher) -and (Test-Path -LiteralPath $csc) -and ($Force -or -not (Test-Path -LiteralPath $rootExe))
if ($needLauncher -and (Test-Path -LiteralPath $rootExe)) {
  try {
    $lfs = [System.IO.File]::Open($rootExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $lfs.Close()
  } catch {
    $needLauncher = $false
  }
}
if ($needLauncher) {
  $launcherDir = Join-Path $root 'obj\launcher'
  New-Item -ItemType Directory -Force -Path $launcherDir | Out-Null
  $launcherCs = Join-Path $launcherDir 'Launcher.cs'
  $launcherSrc = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string dist = Path.Combine(root, "dist");
        string target = Path.Combine(dist, "TempWidget.exe");
        string marker = Path.Combine(dist, ".built-commit");
        string builder = Path.Combine(root, "build-local.ps1");

        bool needBuild = !File.Exists(target);
        if (!needBuild && File.Exists(builder))
        {
            string head = ReadGitHead(root);
            string built = File.Exists(marker) ? (File.ReadAllText(marker) ?? "").Trim() : "";
            if (!string.IsNullOrEmpty(head) && !string.Equals(head, built, StringComparison.OrdinalIgnoreCase))
                needBuild = true;
        }

        bool wantAdmin = ShouldRequestAdmin(args);
        if (needBuild || wantAdmin)
            StopUnelevatedRunningCopies(target);

        if (needBuild && File.Exists(builder))
        {
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + builder + "\" -SkipLauncher")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                var err = new System.Text.StringBuilder();
                p.ErrorDataReceived += (s, ev) => { if (ev.Data != null) err.AppendLine(ev.Data); };
                p.BeginErrorReadLine();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    string detail = (err.ToString() + "\n" + stdout).Trim();
                    if (detail.Length > 500) detail = detail.Substring(0, 500);
                    MessageBox.Show("TempWidget local build failed (exit " + p.ExitCode + ").\n\n" + detail,
                        "TempWidget", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return p.ExitCode;
                }
            }
        }

        if (!File.Exists(target))
        {
            MessageBox.Show("Missing " + target + ". Run build-local.ps1 first.", "TempWidget", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        string forwarded = JoinArgs(args);
        if (wantAdmin)
        {
            try
            {
                string adminArgs = string.IsNullOrEmpty(forwarded) ? "--restarted" : "--restarted " + forwarded;
                Process.Start(new ProcessStartInfo(target, adminArgs) { Verb = "runas", WorkingDirectory = dist, UseShellExecute = true });
                return 0;
            }
            catch
            {
                // UAC prompt declined: fall back gracefully to limited mode for this session.
                forwarded = string.IsNullOrEmpty(forwarded) ? "--limited" : "--limited " + forwarded;
            }
        }
        Process.Start(new ProcessStartInfo(target, forwarded) { WorkingDirectory = dist, UseShellExecute = true });
        return 0;
    }

    private static bool ShouldRequestAdmin(string[] args)
    {
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] ?? "";
                if (string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--limited", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }
        try
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                if (new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                    return false;
        }
        catch { }
        try
        {
            string ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TempWidget", "settings.ini");
            if (!File.Exists(ini)) return true; // default on local dev launcher: full sensors
            foreach (string line in File.ReadAllLines(ini))
            {
                if (line.StartsWith("PreferElevated=", StringComparison.OrdinalIgnoreCase))
                    return line.Substring("PreferElevated=".Length).Trim() != "0";
            }
        }
        catch { }
        return true;
    }

    private static void StopUnelevatedRunningCopies(string targetExe)
    {
        int self = Process.GetCurrentProcess().Id;
        foreach (var proc in Process.GetProcessesByName("TempWidget"))
        {
            try
            {
                if (proc.Id == self) continue;
                string path = proc.MainModule != null ? proc.MainModule.FileName : "";
                if (!string.Equals(path, targetExe, StringComparison.OrdinalIgnoreCase)) continue;
                if (proc.CloseMainWindow()) proc.WaitForExit(1500);
                if (!proc.HasExited) { proc.Kill(); proc.WaitForExit(1500); }
            }
            catch { /* elevated copy cannot be closed from unelevated launcher; leave untouched */ }
            finally { proc.Dispose(); }
        }
    }

    private static string ReadGitHead(string root)
    {
        try
        {
            var psi = new ProcessStartInfo("git", "-C \"" + root.TrimEnd('\\') + "\" rev-parse HEAD")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                string s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                return p.ExitCode == 0 ? s : "";
            }
        }
        catch { return ""; }
    }

    private static string JoinArgs(string[] args)
    {
        if (args == null || args.Length == 0) return "";
        var parts = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i] ?? "";
            parts[i] = a.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0 ? "\"" + a.Replace("\"", "\\\"") + "\"" : a;
        }
        return string.Join(" ", parts);
    }
}
'@
  $utf8Bom = New-Object System.Text.UTF8Encoding($true)
  [System.IO.File]::WriteAllText($launcherCs, $launcherSrc, $utf8Bom)
  $iconPath = Join-Path $root 'Assets\TempWidget.ico'
  $cscArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', "/out:$rootExe", '/r:System.Windows.Forms.dll')
  if (Test-Path -LiteralPath $iconPath) { $cscArgs += "/win32icon:$iconPath" }
  $cscArgs += $launcherCs
  $cscOut = & $csc @cscArgs 2>&1
  if ($LASTEXITCODE -ne 0) {
    $cscOut | ForEach-Object { Write-Host $_ }
    Write-Error "Root launcher build failed."
    exit $LASTEXITCODE
  }
}

Say "[TempWidget] Ready: $rootExe (-> $exePath)"
exit 0
