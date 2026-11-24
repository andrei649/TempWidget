A small always-on-top Windows widget for CPU and GPU temperature, load, fans, RAM, drives, network and battery.

## Download

| File | For |
|---|---|
| **`TempWidget-*-portable.zip`** | No installation. Right-click the zip, **Extract All**, then run `TempWidget.exe` from the extracted folder. |
| **`TempWidget-*-x64.msi`** | Installs to Program Files with a Start menu shortcut. |

Needs Windows 10 (1809 or later) or Windows 11, 64-bit.

## First start

- The download is not code-signed, so Windows may show **"Windows protected your PC"**. Click **More info**, then **Run anyway**.
- A welcome screen offers **Set up full sensors**: Windows asks once for administrator rights and TempWidget installs the PawnIO sensor driver it ships with (official, signed, no download). This is what makes CPU temperature work.
- Prefer not to? **Continue with basic sensors** shows GPU, memory, battery and CPU load without administrator rights.

TempWidget makes no network connections and collects no data. Licences of the included components are in `THIRD-PARTY-NOTICES.txt`.
