# Dependency review for the private source import

Reviewed 2026-11-16 against the supplied notices and the upstream pages below.
The commit contains no dependency source, DLL, NuGet package, driver, installer
or executable release. Dependency declarations refer to external packages;
original third-party license texts/notices are retained in
`THIRD-PARTY-NOTICES.txt`. Image/icon files are application source assets.

| Component | Evidence reviewed | Import/release implication |
|---|---|---|
| LibreHardwareMonitorLib 0.9.6 | [Tagged MPL-2.0 license](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LICENSE) | Only the package reference and API-using application code are imported. Any later distribution of its executable form must satisfy its source availability and notice obligations. |
| DiskInfoToolkit, RAMSPDToolkit-NDD, BlackSharp.Core | [DiskInfoToolkit license](https://github.com/Blacktempel/DiskInfoToolkit/blob/master/LICENSE), [RAMSPDToolkit license](https://github.com/Blacktempel/RAMSPDToolkit/blob/master/LICENSE), [BlackSharp license](https://github.com/Blacktempel/BlackSharp/blob/master/LICENSE) | Current upstream license files identify MPL-2.0. No copies are imported. Recheck the exact restored versions and preserve source/notice obligations when preparing a release. |
| HidSharp 2.6.4 | [Author's project page](https://software.seekye.com/hidsharp) and supplied Apache-2.0 notice | The author identifies Apache licensing. No library copy is imported. Preserve applicable license/attribution requirements for any later redistribution. |
| .NET support libraries | [.NET runtime MIT license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) and supplied notices | No framework/support DLLs are imported. Retain applicable copyright/license notices in any later distribution; inspect exact restored packages then. |
| PawnIO driver | [Upstream license statement and exception](https://github.com/namazso/PawnIO#license) | Upstream describes GPL-2.0-or-later with a specific device-I/O-control exception. This does not establish universal proprietary compatibility, or grant rights for a separate installer. No driver/modules/source are imported. |
| PawnIO_setup.exe | [Official release repository](https://github.com/namazso/PawnIO.Setup) | That repository exposes a README and release hosting, not an explicit installer license in its source tree. Installer redistribution rights remain unverified. The installer is excluded and ignored. Obtain applicable permission/terms before bundling a future release. |

The supplied TempWidget application source did not include an explicit
application-wide license. No new license has been applied. The user's
authorization is for this private source backup/import; it does not establish
unrestricted public or commercial distribution rights for every dependency.

This review supports importing the selected source-only tree. It does not
approve a packaged/public release. The original `SELLING.md` and `STORE.md`
are historical planning notes; their pricing, policy and legal assertions were
not fully revalidated by this import and should not be treated as release approval.
