# TempWidget 2.1.0 unreleased patch: validation

Validation snapshot before private repository import; date: 2026-11-16.
Work was performed in a separate copy. The supplied Downloads
archives were read, not changed. No Git checkout, AGENTS.md, `.agents/skills`
or SKILL.md was present in the supplied source tree or workspace. Applicable
ancestor AGENTS.md paths were checked; none was found.

## Result

| Finding | Implemented behavior | Synthetic evidence |
|---|---|---|
| CPU temperature vanished on null/zero | Stable temperature row shows N/A; valid alias/average fallback remains usable | Missing, null, zero, negative, NaN, infinities, recovery, indexed CPUs, compact flag and both units |
| EC reads continued without mutex ownership | Shared mutex must be acquired; failure/timeout causes zero port reads | Fake transport checks zero reads/releases on contention and acquisition exception; one release on owned reads/exceptions |
| Old EC RPM looked current after failures | Each poll starts unknown; missing bytes, exceptions, implausible/all-ones samples show N/A without a numeric RPM | Successful read followed by timeout/failure/recovery; per-fan byte failures; third-fan identity; stopped fan |
| Drive/battery cadence depended on ticks | Monotonic elapsed deadlines: slow groups 30 seconds, battery 10 seconds | Refresh at 500, 2000, 5000 and 60000 ms; hidden/full transitions; repeated same-time calls; long pause |
| No tests supplied | Standalone net48/x64 runner, with no added test-framework package | 36 passed, 0 failed |

Unavailable/nonfinite readings are excluded from history, numeric CSV output
and alerts. They reset consecutive hot samples. Tray hottest mode chooses the
remaining valid CPU/GPU reading. Tests call the same row builders and helpers
used by production; the EC transport is replaced by an explicitly injected fake.

## Build and execution evidence

- .NET SDK 10.0.404, Windows x64, installed .NET Framework 4.8 targeting pack.
- Application target remains `net48`, `win-x64`, x64. Manifest remains
  `asInvoker`, with the existing Windows 10/11 compatibility declaration.
- Final Release build of application and test project: **0 warnings, 0 errors**.
- The synthetic runner executed on Windows x64 CLR4: **36 passed, 0 failed**.
- One measured runner invocation took **562 ms wall time**, including startup
  and console output. This is a synthetic test duration, not an application
  CPU/RAM/power measurement.
- At 500 ms refresh over two simulated minutes, the scheduler permits 5 slow
  group reads and 13 battery reads, including the initial poll. At 60000 ms,
  each group is read 3 times; it does not issue catch-up reads.
- Raw local logs: `artifacts/build.log`, `artifacts/synthetic-tests.log`.
  Local offline DLL hashes: `artifacts/offline-dependency-hashes.json`.
- Built application: `bin/Release/net48/win-x64/TempWidget.exe`.
  Test executable: `tests/bin/Release/net48/TempWidget.Tests.exe`.
  Only the test executable was run.

The normal NuGet build could not restore packages: NU1301, DNS lookup for
api.nuget.org failed in the restricted environment. The successful offline
build used DLLs extracted from the supplied portable archive, including
LibreHardwareMonitorLib assembly **0.9.6.0**, and installed framework reference
assemblies. No packages, drivers or SDK workloads were installed to resolve
this. The normal online restore path remains unverified here.

On the first SDK invocation, the SDK printed its standard first-run message
saying an ASP.NET Core HTTPS development certificate was installed. Later
invocations disabled automatic certificate generation. No certificate trust
command was run and the certificate store was not inspected or modified
manually. A read-only metadata script attempted in legacy Windows PowerShell
was blocked by its execution policy; no policy was changed. Interface checking
instead used the tagged upstream source linked in DEVELOPMENT.md and the
successful compilation against the actual release DLL.

## Safety and untested areas

No PawnIO installer or supplied TempWidget binary was executed. The rebuilt
application was not launched, so its existing onboarding, elevation, startup
repair and hardware-opening paths were not entered. No UAC elevation, autostart
change, hardware scan or direct EC read was requested. At this validation
snapshot no repository had been published and no data had been uploaded.
The subsequent private source import does not add hardware or release validation.

The following remain untested:

- Real CPU/GPU/drive/battery values and hardware update errors on this laptop.
- Actual EC register correctness, timing, torn reads and coexistence with
  firmware or another monitor. Synthetic mutex failures prove the control flow,
  not physical controller behavior. The all-ones sample is conservatively
  treated as unavailable; this has not been checked on hardware.
- PawnIO installation, administrator access and permission transitions.
- Visual rendering, DPI, taskbar/tray integration, sleep/resume and battery
  saver behavior of a running widget. Deadline logic is tested synthetically.
- App CPU/RAM use, energy use, thermal overhead and long-running stability.
- Windows versions or hardware other than this test environment.
- MSI/MSIX creation, signing, install/uninstall, Store submission and Linux
  packaging execution. The source-archive packaging list now includes tests
  and developer/validation docs. Even the nonexecuting `bash -n build.sh` check
  was blocked: Git Bash could not create its MSYS named object (0xC0000022).
  No sandbox escalation was attempted; Bash parsing remains unverified.

These limitations do not block source review or preparing a private import.
Real hardware measurements should follow a separate reviewed launch decision.

## Source-only import

The clean export includes application source, synthetic tests, assets, build
scripts, documentation and third-party notices. It omits installer executables,
DLLs, build outputs, SDK state, settings, sensor reports, logs, credentials and
Git metadata. Export paths and text were checked for user profile paths,
private-key/token patterns and local runtime data; no matches were found.
Public upstream copyright/license notices are retained.

## Input archive SHA-256

| Supplied archive | SHA-256 |
|---|---|
| TempWidget-2.1.0-source.zip | E32923A2D77935CBFA79DFE7A6D647B6A7BC4D9B54F608992F87127E0A5975B2 |
| TempWidget-2.1.0-portable.zip | 7E05A53F356B9AA73C994B663EAF91AA4288B06E00689487239EF2F4502CD399 |
| TempWidget-2.1.0-installer.zip | C193333006C287A85D697B36463F8B8CB11B6F49F71B8D04D450C83F5D0F8A79 |
