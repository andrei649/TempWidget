# TempWidget

Private source import of the supplied 2.1.0 Windows desktop sensor widget,
with fixes for missing CPU temperatures, EC mutex ownership, invalid/stale
fan RPM and elapsed-time polling of slow devices. This revision also removes
privileged startup task creation/repair and separates CSV column schemas.

Targets Windows x64 and .NET Framework 4.8. This is an unreleased source
revision, not an installed, signed or hardware-validated release.

The reviewed Windows Release build passed with zero errors/warnings, and all
64 synthetic tests passed. Real sensors, EC layout, visual rendering,
sleep/resume, energy use and installer behavior remain untested.

Packaged launches conservatively skip automatic onboarding, including a first
manual launch or settings reset. Full sensors can still be requested explicitly
from the widget. MSIX logon activation has not been tested on an installed package.

- [Build and test instructions](DEVELOPMENT.md)
- [Validation evidence and exact limits](VALIDATION.md)
- [Startup/CSV safety review](SAFETY-REVIEW.md)
- [Dependency and redistribution review](DEPENDENCIES.md)
- [Original user documentation](README.txt)
- [Third-party notices](THIRD-PARTY-NOTICES.txt)

## Download

Ready-to-run builds are on the [Releases page](https://github.com/andrei649/TempWidget/releases):

- `TempWidget-<version>-portable.zip`: extract it and run `TempWidget.exe`
  (keep the other files next to it).
- `TempWidget-<version>-x64.msi`: the installer.

Builds are unsigned, so Windows SmartScreen may warn on first start.

## Making a release

The [Release workflow](.github/workflows/release.yml) builds both files on
GitHub and attaches them to a release named after `<Version>` in
`TempWidget.csproj`. Either push a matching tag:

```bash
git tag v2.1.0 && git push origin v2.1.0
```

or run **Actions > Release > Run workflow**. Running it again for the same
version replaces that release's files. The workflow downloads the official
PawnIO 2.2.0 installer and checks its SHA-256 before bundling it.

The git repository itself stays source-only: executables, DLLs, installers,
credentials, signing keys, settings, sensor reports, logs and local SDK/package
state are kept out of it by `.gitignore`.

The supplied application source had no explicit application-wide license.
This import does not assign one or grant redistribution rights to third-party
code. Existing upstream notices are preserved. No installer redistribution
permission is inferred from this private source import.
