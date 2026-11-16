# Publishing TempWidget in the Microsoft Store

This is a checklist plus ready-to-paste listing text. Nothing here has been
submitted or reviewed by Microsoft; treat the policy notes as things to
confirm in Partner Center, not as guarantees.

## The one decision: which kind of Store app

TempWidget needs administrator rights and the PawnIO driver for its most
valuable readings: CPU temperature, drive temperatures, laptop fans. The
welcome screen installs PawnIO from the official installer shipped with the
app, after the user clicks "Set up full sensors". That shapes the choice.

| | A. Installer listed in the Store (recommended) | B. MSIX package |
|---|---|---|
| What you upload | A link to `TempWidget-2.1.0-x64.msi` hosted by you | An `.msix` built with `package/build-msix.ps1` |
| Full sensors (admin) | Yes, works like the portable version | Only if Microsoft approves the `allowElevation` capability, which its documentation says is usually refused |
| Code-signing certificate | Required, you buy it | Not needed, the Store signs |
| Hosting | You host the installer at a fixed HTTPS address | Microsoft hosts |
| Updates | You publish a new installer and update the submission | Automatic through the Store |

Without elevation, option B is a limited edition: GPU, RAM, battery and CPU
load, but no CPU temperature. For a temperature widget that is a weak product,
so option A is the realistic route.

## Before either route

1. **Partner Center developer account.** Check the current fee for individual
   accounts on the sign-up page.
2. **Reserve the name.** "TempWidget" may be taken; have a second choice.
3. **Publisher details.** Rebuild with your name so the installer shows it:
   `./build.sh "Your Publisher Name"`.
4. **Privacy policy URL.** Put `PRIVACY.txt` on a public web page (fill in the
   contact address at the bottom first). The listing asks for the URL.
5. **Screenshots.** At least one, 1366x768 or larger. `TempWidget.exe --demo`
   shows simulated readings, handy for clean screenshots on any PC. Say so in
   the caption if you use it.
6. **Ask the PawnIO author.** TempWidget ships the official, unmodified
   `PawnIO_setup.exe`. Other monitoring tools do the same, but I found no
   written redistribution terms for the signed installer. Before publishing,
   ask the author (contact link on pawnio.eu) to confirm that bundling is
   fine. When a new PawnIO version comes out, replace `redist/PawnIO_setup.exe`
   and update the version and SHA-256 in `DriverSetup.cs`.
7. **Test on a second PC**, ideally a desktop with an AMD CPU or GPU. Sensor
   names differ between vendors and only one laptop has been tested so far.

## Option A: installer listed in the Store

1. Buy a code-signing certificate from a certificate authority in the
   Microsoft Trusted Root Program. Self-signed is not accepted.
2. Sign `TempWidget.exe` and then the `.msi` (sign the exe before building the
   installer, or the installer will contain an unsigned exe):
   `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a <file>`
3. Host the signed `.msi` at a permanent HTTPS address. The file at that
   address must not change after submission; use a versioned file name.
4. In Partner Center create an **EXE or MSI app** and give:
   - Package URL: the address from step 3
   - Architecture: x64
   - Installer parameters (silent install): `/qn`
5. Fill in the listing (text below), age rating questionnaire, and submit.

Notes for the certification testers (paste into "Notes for certification"):

> TempWidget is a hardware-sensor widget. On first start a welcome screen
> offers two choices. "Continue with basic sensors" needs no administrator
> rights and shows GPU, memory, battery and CPU load. "Set up full sensors"
> asks for administrator rights and installs the open-source PawnIO driver
> (pawnio.eu) from its official signed installer, which is included in the
> package; nothing is downloaded. The driver is needed for CPU temperature. On a virtual machine most sensors are absent and the widget
> shows only what exists. The app makes no network connections. Start it with
> `--demo` to see simulated readings.

## Option B: MSIX package

1. Fill in the three `REPLACE` values in `package/AppxManifest.xml` from
   Partner Center > Product identity.
2. On Windows with the Windows SDK installed:
   `package\build-msix.ps1 -AppFolder dist\TempWidget`
3. Upload the `.msix`. Partner Center will ask you to justify each restricted
   capability:
   - `runFullTrust`: "Classic desktop (WinForms) application."
   - `allowElevation`: "Reading CPU, drive and embedded-controller sensors
     requires access to a kernel driver that Windows only grants to
     administrators. The app starts unelevated and elevates only when the user
     asks for full sensors."
4. If `allowElevation` is refused, delete that line from the manifest and
   resubmit as a limited edition. Change the listing text to say that CPU
   temperature is not available in the Store edition.

Untested in this route: the build script itself, starting elevated at logon,
and restarting elevated from inside the package. Try both on your PC by installing the `.msix` locally
(sign it with a test certificate) before submitting.

## Listing text

**Name:** TempWidget

**Short description (up to 100 characters)**

Temperatures, fan speeds and load in a small desktop widget that stays out of the way.

**Description**

TempWidget puts your PC's vital signs in one small panel on the desktop: CPU
and GPU temperature, load and power, memory use, fan speeds, drive
temperatures and battery health.

It is built to be light. It reads only the sensors for the sections you show,
checks slow devices such as drives less often, slows down on battery power,
and reads nothing at all while it is hidden or the PC is locked.

- CPU: average and hottest-core temperature, load, power, clock speed
- GPU: temperature, hot spot, memory temperature, load, power, video memory
- RAM use and memory-module temperature
- Fan speeds, drive temperatures, battery charge, rate and wear
- Network download and upload speed
- Every core's temperature, and each drive's fill level and remaining life, on hover
- Optional logging to CSV files for Excel
- Temperature history lines, with lowest and highest values on hover
- CPU or GPU temperature in the tray icon
- Optional alerts when something stays too hot
- Compact mode, dark and light themes, Celsius or Fahrenheit, adjustable size and opacity
- Click-through and always-on-top for use as an overlay

Good to know before installing:

- Full sensor access needs administrator rights, which the welcome screen
  asks for once. Without them TempWidget shows GPU, memory, battery and CPU
  load only.
- CPU temperature, power and clock use the free, open-source PawnIO driver
  (pawnio.eu). TempWidget installs it for you when you choose full sensors.
- Which sensors appear depends on what your hardware reports. Many laptops do
  not report fan speed.

No ads, no account, no telemetry. TempWidget makes no network connections
and collects no data.

**Search terms:** temperature, CPU temp, GPU temp, fan speed, hardware monitor, sensors, widget

**Category:** Utilities & tools

**Third-party notices:** TempWidget uses LibreHardwareMonitorLib and related
libraries under the Mozilla Public License 2.0, HidSharp under the Apache
License 2.0 and .NET libraries under the MIT License. See
THIRD-PARTY-NOTICES.txt in the installation folder.
