# TempWidget development

This is an unreleased patch based on the supplied 2.1.0 source archive. The
application still targets Windows x64 and .NET Framework 4.8. It has not been
installed or started against real hardware during this review.

## Build and run synthetic tests on Windows

Prerequisites: a .NET SDK and .NET Framework 4.8. Normal package restoration
uses the versions pinned in `TempWidget.csproj`.

```powershell
dotnet build tests/TempWidget.Tests.csproj -c Release
& ./tests/bin/Release/net48/TempWidget.Tests.exe
```

The test runner adds no test-framework packages and exits with 0 only if every assertion passes.
Its fakes implement the real LibreHardwareMonitor interfaces. It calls row
builders, elapsed-time scheduling, a fake EC transport, temperature formatting,
tray selection, history, alerts and CSV formatting. It never constructs
`Computer`, `PawnIoEcFanDevice`, a Form, or the application entry point. It does
not write user settings, user logs or startup entries. CSV filesystem tests
write synthetic fixtures only under the test output directory in the workspace;
registry and task behavior is tested through a fake store.

## Offline build from an existing portable release

If NuGet is unreachable, put **only the DLL files** from the supplied 2.1.0
portable archive into `.offline-deps` in this directory. Do not run the original
EXE or installer. `LibreHardwareMonitorLib.dll` must have assembly version
0.9.6.0. The .NET Framework 4.8 targeting pack must already be installed.

```powershell
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:MSBuildEnableWorkloadResolver = 'false'
$deps = Join-Path $PWD '.offline-deps'
dotnet build tests/TempWidget.Tests.csproj -c Release --nologo "-p:OfflineReferenceDirectory=$deps" -p:AutomaticallyUseReferenceAssemblyPackages=false "-p:RestoreSources=$deps"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& ./tests/bin/Release/net48/TempWidget.Tests.exe
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
```

The application output is `bin/Release/net48/win-x64/TempWidget.exe`. These
commands build the application and execute only the synthetic tests. They do
not launch TempWidget or install a driver. The ordinary build without
`OfflineReferenceDirectory` continues to use NuGet package references.

The original Linux packaging script is retained. It requires the separately
supplied official PawnIO installer at `redist/PawnIO_setup.exe` to make complete
release packages. Building application source and tests does not require that
installer. Installer/MSIX generation, signing and distribution are separate,
unverified steps for this patch.

## Reading behavior

- An absent, zero or nonfinite CPU temperature produces a stable `cpu.temp`
  row (or indexed CPU key) with `N/A` in full/compact views and the tray tooltip.
  An unavailable tray selection uses the normal app icon. Hottest-of-CPU/GPU
  mode uses whichever valid reading remains. Missing readings do not enter
  history or alert comparisons. CSV preserves known numeric columns with empty
  cells for unavailable readings. They reset consecutive
  over-temperature samples without falsely rearming an alert.
- Direct EC fan reads require acquisition of `Global\Access_EC`. Missing mutex
  access, timeout or an acquisition exception causes no port reads. A failed,
  implausible or all-ones sample becomes unknown on that poll; old RPM is not
  reused. Unknown EC fans show `N/A`. Zero period retains the existing stopped
  fan interpretation. Existing verified-board/explicit opt-in gating remains.
- Drive SMART, DIMM sensors and network adapter discovery have a 30-second
  minimum interval; battery polling has a 10-second minimum interval. These
  preserve the original defaults at a 2-second widget refresh. A monotonic
  stopwatch replaces tick counts. Actual reads occur on the first full poll
  after a deadline, so a 60-second refresh reads no faster than once a minute.
  CPU/GPU-only background polls do not consume the skipped groups' deadlines.
  Resuming after a pause performs one due read, without a catch-up burst.

## Source-only private import

The prepared source export includes source, tests, assets, documentation and
third-party notices. It excludes the bundled installer, dependency DLLs, build
outputs, logs, settings, local SDK state, credentials and repository metadata.
The private import contains source and assets only. Preserve
`THIRD-PARTY-NOTICES.txt` and read `DEPENDENCIES.md` before distributing a built
release. No new ownership/license claim has been added for the supplied
application source. The earlier validation snapshot is recorded in `VALIDATION.md`.

The synthetic fakes were checked against the tagged upstream interfaces:
[IHardware 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/IHardware.cs)
and [ISensor 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/Hardware/ISensor.cs).

The startup/CSV follow-up is documented in `SAFETY-REVIEW.md`. Startup entries
request basic mode with `--startup`; persisted elevation preferences do not
cause automatic UAC requests. Legacy scheduler tasks are only queried and
warned about, never repaired or deleted. CSV v2 sorts columns by stable key,
includes key/section/label/unit in its schema, and uses a full SHA-256 schema
suffix in the daily filename. One file contains one header and one schema,
including across application restarts. Only recognized expired v2 logs are
eligible for retention cleanup; legacy logs are left alone.

MSIX startup declares `uap10:Parameters="--startup"`. Package identity is not
treated as proof of logon activation: unmarked packaged launches also suppress
automatic onboarding as a conservative fallback. This includes the first
manual packaged launch and settings-reset case. Explicit full-sensor/setup
actions remain available. Unit tests check the actual manifest and this policy;
they do not validate Windows activation of an installed MSIX package.
