# Taskbar Thermals

This fork adds Windows-style CPU/GPU icons and a **Flydigi BS2 Pro fan-speed percentage** to [Quenchh's Taskbar Thermals](https://github.com/Quenchh/taskbar-thermals). Temperature and usage remain visible; power is optional and hidden by default.

The cooling-base reading comes from [THRM](https://github.com/TIANLI0/THRM)'s local Windows named pipe. Run THRM and connect the BS2 Pro there. This app observes measured RPM and never opens the cooler or sends fan-control commands. THRM already has an English interface.

Fan percentage means measured RPM divided by 4,000 RPM: 3,300 RPM displays **83%**. It is not airflow or electrical duty cycle. Hover or open the details panel to see RPM and connection information. A stopped fan shows 0%; unavailable readings show `--%`. Other THRM-connected cooler models are deliberately not presented as this BS2 Pro.

If sensor sampling pauses for more than three configured update intervals, CPU/GPU values change to `--` and the tooltip identifies the stale readings. Fresh samples restore the values automatically; independently received cooler readings continue. This monitors measurement age without cancelling or restarting sensor operations.

Space Station controller coexistence is a separate THRM/device configuration step; simply starting two cooler-control programs can cause competing commands. Keep your controller profiles and use a reviewed cooler-only coexistence setup. Disabling the standalone LibreHardwareMonitor application does not require removing the shared sensor library or PawnIO driver.

Live CPU and GPU temperature and usage right on the Windows 11 taskbar — plus RAM, network and disk if you want them, a 24-hour history panel, temperature-spike logging and a hardware-error watcher for when you're tuning undervolts or BIOS settings.

![CPU, GPU and BS2 Pro overlay](docs/bs2pro-overlay.png)

Overlay renderer preview at 150% scaling, using example readings.

<img src="docs/demo.gif" alt="Details panel: hovering the charts and switching between 10 minutes, 1 hour and 24 hours" width="380"> <img src="docs/settings.png" alt="Settings" width="380">

*Details panel and settings. The chart history in the animation is generated sample data.*

## Features

- **Taskbar overlay** — pick what it shows: CPU/GPU temperature, usage, power and clock, GPU memory temperature, RAM usage, network download/upload, disk read/write. Temperatures turn yellow/red past your thresholds. Hides automatically for full-screen games and videos, just like the taskbar.
- **Details panel** (click the overlay) — current temperature, usage, power and clocks; temperature and usage charts for the last 10 minutes, hour or 24 hours with hover readouts; SoC voltage, Vcore, fan speeds, RAM, network, disk; max temperatures. The 24-hour history survives restarts.
- **Spike log** — when the CPU temperature jumps quickly (default: 10 °C within 5 s), the busiest processes at that moment are written to a CSV, so you can see what caused it.
- **Stability watcher** — watches the Windows event log for WHEA hardware errors, blue screens and unexpected shutdowns, and notifies you. Useful when testing Curve Optimizer, SoC voltage or memory settings: press *Mark as seen* before a test, and any new event belongs to the new setting.
- **Notifications** when the CPU or GPU stays above a temperature for too long.
- **Update check** — once a day, tells you when a new release is out.
- English and Turkish UI (follows the Windows display language, or pick one in settings), dark and light theme, per-monitor DPI aware, drag to reposition, starts with Windows.

## Requirements

- Windows 11, x64 (Windows 10 will likely work but is untested)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) only if you build from source
- The [PawnIO](https://pawnio.eu) driver for CPU temperature and motherboard sensors (LibreHardwareMonitor uses it). GPU readings work without it.
- Administrator rights — reading CPU sensors requires them. With autostart enabled the app starts elevated at logon without a UAC prompt.

## Download

Grab the latest build from [Releases](https://github.com/Darkaxt/taskbar-thermals/releases/latest):

| File | Size | Needs |
|---|---|---|
| `TaskbarThermals.exe` | ~6 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `TaskbarThermals-standalone.exe` | ~65 MB | nothing |

Put the exe wherever you like and run it. It asks for administrator rights (needed for CPU temperature). Right-click the overlay → **Start with Windows** to launch it at logon without a UAC prompt. The exe isn't code-signed, so Windows SmartScreen may warn on first run: *More info → Run anyway*.

Startup is opt-in. Toggle **Start with Windows** off to remove its `TaskbarThermals` scheduled task. For an installation with a dedicated runtime directory, launch with `--runtime-directory <absolute-folder>`; elevated relaunch and startup registration retain that directory for application/child-process scratch. A standalone bundle may use the framework's initial extraction cache before the application configures its own environment.

## Build from source

```powershell
git clone https://github.com/Darkaxt/taskbar-thermals.git
cd taskbar-thermals
.\install.ps1              # builds, installs to %LOCALAPPDATA%\Programs\TaskbarThermals, starts with Windows
.\install.ps1 -NoStartup   # same, without autostart
.\uninstall.ps1            # removes the app (settings and logs are kept)
```

Running `install.ps1` again updates an existing installation.

## Usage

| Action | Result |
|---|---|
| Click | Open / close the details panel |
| Drag | Move the overlay along the taskbar |
| Right-click | Menu: Task Manager, spike log, settings, start with Windows, exit |
| Tray icon | Same panel and menu; notifications come from here |

## Files

Everything lives in `%LOCALAPPDATA%\TaskbarThermals`:

| File | Contents |
|---|---|
| `settings.json` | Settings |
| `spikes.csv` | Temperature spikes with the top CPU processes at that moment |
| `history.csv` | Per-minute average, min and max of CPU/GPU temperature and usage for the last 24 hours |
| `errors.log` | Unexpected errors |

## Diagnostics

```powershell
TaskbarThermals.exe --dump sensors.txt   # every sensor + a 20 s series of CPU power/temperature/fan speeds
TaskbarThermals.exe --preview <folder>   # renders the details panel and settings window to PNGs
TaskbarThermals.exe --demo <folder>      # renders the frames of the README animation
TaskbarThermals.exe --preview-overlay <folder> # production overlay renderer, synthetic readings, three DPI scales and both themes
dotnet run --project tests/TaskbarThermals.Contracts.csproj -c Release # telemetry and real local-pipe contracts
```

## Privacy

Nothing leaves your PC except the optional update check: one HTTPS request shortly after start and then once a day to the GitHub API (`api.github.com/repos/Darkaxt/taskbar-thermals/releases/latest`) with no data attached. Turn it off in settings. Cooler telemetry stays on the local named pipe.

## Releases

Pushing a `v*` tag builds both executables on GitHub Actions and publishes a release with checksums; notes come from [CHANGELOG.md](CHANGELOG.md).

## How it works

Windows 11 no longer supports taskbar deskbands, so the overlay is a borderless, per-pixel-alpha window kept above the taskbar. Sensors are read with [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor); CPU usage comes from the same performance counter Task Manager uses, so the numbers match.

## License

[MIT](LICENSE) — free to use, modify and distribute, including commercially.

Uses [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), licensed under [MPL-2.0](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE).
