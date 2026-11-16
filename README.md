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

This repository contains source, tests, documentation and image/icon assets.
It excludes executable releases, DLLs, installers, credentials, signing keys,
settings, sensor reports, logs and local SDK/package state. It contains no
deployment workflow. Existing packaging scripts need separate review and
inputs before use.

The supplied application source had no explicit application-wide license.
This import does not assign one or grant redistribution rights to third-party
code. Existing upstream notices are preserved. No installer redistribution
permission is inferred from this private source import.
