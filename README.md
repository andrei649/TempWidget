# TempWidget

A small always-on-top Windows widget for CPU and GPU temperature, load, fans,
RAM, drives, network and battery. Light on resources, no ads, no account, no
network connections.

## Download and run

### ⬇️ [Download TempWidget (portable zip)](https://github.com/andrei649/TempWidget/releases/latest/download/TempWidget-portable.zip)

Right-click the downloaded zip, choose **Extract All**, and run `TempWidget.exe`
from the extracted folder. Prefer an installer? Get
[TempWidget-x64.msi](https://github.com/andrei649/TempWidget/releases/latest/download/TempWidget-x64.msi).
Older versions are on the [Releases page](https://github.com/andrei649/TempWidget/releases).

> **Not** the green **Code → Download ZIP** button: that is the source code, which
> has no `TempWidget.exe` and needs the .NET SDK to build.

The download is not code-signed, so Windows may show **"Windows protected your
PC"**: click **More info**, then **Run anyway**. On first start choose **Set up
full sensors**; Windows asks once for administrator rights and the PawnIO
sensor driver (included) is installed for CPU temperature.

Requires 64-bit Windows 10 (1809 or later) or Windows 11.

## For developers

Windows x64 (.NET Framework 4.8) desktop sensor widget (`2.1.3`), with fixes for
missing CPU temperatures, EC mutex ownership, invalid/stale fan RPM, elapsed-time
polling of slow devices, safe per-user startup (`--startup`) and versioned CSV
column schemas.

Version `2.1.3` fixes the MSI installer, which failed on every machine with
error 2613 (RemoveExistingProducts was not directly after InstallInitialize).
`build.sh` now renumbers and checks that sequence, and a
[friend test](.github/workflows/friend-test.yml) installs the published MSI and
portable zip on a clean Windows VM.

Version `2.1.2` shows a clear "extract the zip first" message when
`TempWidget.exe` is opened from inside the downloaded zip, and adds friend-facing
download instructions and release notes.

Version `2.1.1` adds live hardware-verified fixes on System76 Bonobo WS (`bonw16`):

- **EC fan register cap on `bonw16`:** probes only the two physical fans (`0xD0`/`0xD2`) and skips unpopulated `0xD4`/`0xD5` so phantom `Fan 3` RPM values never latch.
- **Task Scheduler absence probe (`0x80070002`):** treats CLR `System.IO.FileNotFoundException` from `ITaskFolder::GetTask` as an absent legacy task so no false `Review startup settings` banner appears after removing `\TempWidget`.
- **Direct full-sensor manual launch (`PreferElevated`):** manual unpackaged launches honor `PreferElevated` after full-sensor setup (opening with administrator rights for CPU/NVMe/RAM/EC sensors), while Windows logon (`--startup`), MSIX, `--demo`, `--restarted` and `--limited` never auto-elevate.

The Windows Release build passes with zero errors/warnings and all **68 synthetic
tests** pass, alongside live desktop verification on `System76 Bonobo WS (bonw16)`.
MSIX logon activation, code signing and sleep/resume remain untested on an
installed Store package.

Packaged launches conservatively skip automatic onboarding, including a first
manual launch or settings reset. Full sensors can still be requested explicitly
from the widget. MSIX logon activation has not been tested on an installed package.

- [Build and test instructions](DEVELOPMENT.md)
- [Validation evidence and exact limits](VALIDATION.md)
- [Startup/CSV safety review](SAFETY-REVIEW.md)
- [Dependency and redistribution review](DEPENDENCIES.md)
- [Original user documentation](README.txt)
- [Third-party notices](THIRD-PARTY-NOTICES.txt)

## Run locally (latest / WIP)

For developers with a git clone and the .NET SDK: double-click `TempWidget.exe` or `Start.cmd` in the repository root to build (automatically when `dist\TempWidget.exe` is missing or behind the current commit) and launch the latest local build from `dist\TempWidget.exe`.

- Rebuild explicitly: `Start.cmd --rebuild` or `powershell -NoProfile -ExecutionPolicy Bypass -File .\build-local.ps1 -Force`
- Run simulated demo mode: `TempWidget.exe --demo` or `Start.cmd --demo`
- `TempWidget.exe` (root launcher) and `dist\` stay gitignored so the repository remains clean for `git pull` / `update-repos.ps1`, while always keeping a runnable executable directly in the repo folder.

## Download

Ready-to-run builds are on the [Releases page](https://github.com/andrei649/TempWidget/releases):

- **Rolling WIP pre-release (`wip`):** automatically rebuilt on every push to `main` or `wip/**` by [WIP Release](.github/workflows/wip-release.yml) (`TempWidget-wip-portable.zip` and `TempWidget-wip-x64.msi`).
- `TempWidget-<version>-portable.zip`: extract it and run `TempWidget.exe`
  (keep the other files next to it).
- `TempWidget-<version>-x64.msi`: the installer.

Builds are unsigned, so Windows SmartScreen may warn on first start.

## Making a release

The [Release workflow](.github/workflows/release.yml) builds both files on
GitHub and attaches them to a release named after `<Version>` in
`TempWidget.csproj`. Either push a matching tag:

```bash
git tag v2.1.3 && git push origin v2.1.3
```

or run **Actions > Release > Run workflow**. Running it again for the same
version replaces that release's files. The workflow downloads the official
PawnIO 2.2.0 installer and checks its SHA-256 before bundling it.

The git repository itself stays source-only: executables, DLLs, installers,
credentials, signing keys, settings, sensor reports, logs and local SDK/package
state are kept out of it by `.gitignore`.

The application source has no explicit application-wide license; all rights
are reserved by the author. Third-party components keep their own licences
(see `THIRD-PARTY-NOTICES.txt`). Releases include the unmodified, official
signed PawnIO 2.2.0 installer, which is GPL-2.0-or-later; its source is at
https://github.com/namazso/PawnIO.
