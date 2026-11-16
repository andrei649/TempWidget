TempWidget 2 - a small desktop widget for temperatures, fans and other sensors
===============================================================================

GETTING STARTED
1. Install with TempWidget-2.1.0-x64.msi, or extract the portable zip anywhere
   and run TempWidget.exe (keep the other files next to it).
2. On first start a welcome screen offers "Set up full sensors". One click:
   - Windows asks once for administrator rights
   - the PawnIO sensor driver is installed for you (included, no download)
   - TempWidget is set to start with Windows, if you leave that ticked
3. Prefer not to? Choose "Continue with basic sensors" (GPU, memory, battery,
   CPU load). You can switch to full sensors later by clicking the
   "Limited mode" line on the widget.

USING IT
- Drag the widget anywhere. Near a screen edge it snaps into place.
- Left-click the tray icon to hide or show it. While it is hidden or the PC is
  locked, no sensors are read at all (unless you turn on "Keep monitoring
  while the widget is hidden").
- Hover a temperature to see its lowest, average and highest value. Hover the
  CPU heading for every core, and a drive for how full and how worn it is.
- Right-click for compact mode, always on top, lock position, click-through,
  settings and exit.
- Colours: green = fine, amber = warm, red = hot.

SETTINGS
General  refresh rate, size, dark/light theme, Celsius/Fahrenheit, opacity,
         start with Windows, slower refresh on battery
Display  which sections to show, compact mode, history lines, and whether the
         tray icon shows the CPU or GPU temperature
Alerts   a Windows notification when CPU, GPU or a drive stays too hot;
         optional logging of every reading to a daily CSV file
About    version, sensor report for support, licences

WHAT IT SHOWS (when the PC provides it)
CPU      average and hottest-core temperature, load, power, clock
GPU      temperature, hot spot, memory temperature, load, power, VRAM
RAM      usage and module temperature
Fans     speed of every fan the PC reports
Drives   temperature of every drive
Network  download and upload speed of the connection in use
Battery  charge, charge/discharge rate, time left, voltage, wear

FANS ON LAPTOPS
Many laptops do not report fan speed in a standard way. TempWidget reads it
directly from the embedded controller only on laptops where that has been
verified (currently: System76 Bonobo WS bonw16). On other Clevo-based laptops
you can try Settings -> Display -> Laptop fan readout -> On (read-only).

UPGRADING FROM VERSION 1
Version 2 reads the same settings file. Exit version 1 first, start version 2,
and the "Start with Windows" entry is moved to the new location automatically.

PRIVACY
No network connections, no telemetry, no accounts. See PRIVACY.txt.

UNINSTALL
Settings -> untick "Start with Windows", Exit, then uninstall from Windows
Settings (or delete the portable folder). Settings live in %APPDATA%\TempWidget.
The PawnIO driver is left in place because other programs may use it; remove
it from Windows Settings -> Apps -> Installed apps -> PawnIO if you want to.

Licences of the included open-source components: THIRD-PARTY-NOTICES.txt
