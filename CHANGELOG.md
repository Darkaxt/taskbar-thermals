# Changelog

## 1.2.3

- Keep the normal taskbar background tint while hovering over the system tray or opening its hidden-icons panel. StartAllBack's arrow highlight can no longer become the monitor's background.
- Resume native tint sampling after the interaction ends, preserving the existing rounded surface, transparent margins, live metrics and settings.

## 1.2.2

- Clear stale CPU/GPU readings when sampling pauses or fails, while keeping independently received cooler telemetry. Explain paused updates in the tooltip and restore values when real samples resume.
- Preserve an explicitly selected runtime directory through elevated relaunch and Windows startup. Use the logon event directly, with no arbitrary startup delay.

## 1.2.1

- Match StartAllBack's separated tray with a rounded monitor background, keeping unused taskbar areas transparent.
- Follow the visible tray's current background tint using only its empty margin pixels. Covered tray areas and the monitor itself cannot feed back into the sampled color.
- Preserve the ordinary taskbar's transparent overlay, device metrics, fixed columns, drag/details and hover behavior.
- Verify the actual rounded surface renderer in light/dark at 100%, 150% and 200% DPI.

## 1.2.0

- Replace CPU/GPU taskbar labels with DPI-scaled monochrome device icons.
- Add a fan icon and measured BS2 Pro speed percentage, read from THRM's existing local IPC. For example, 3,300 RPM out of 4,000 RPM displays 83%; stopped fans display 0%, unavailable readings display --%.
- Show RPM, the 4,000 RPM denominator and connection guidance in hover/details. Clear readings on cooler disconnect or THRM exit and recover when THRM returns. Other cooler models are not mislabeled as BS2 Pro.
- Default to CPU/GPU temperature and usage plus cooling-base speed, with power readings optional. Existing metric selections are preserved.
- Keep fan control exclusively in THRM and point update checks to this fork's releases.
- Add actual named-pipe contract checks and an overlay-render diagnostic for light/dark themes at 100%, 150% and 200% DPI.

## 1.1.0

- **Pick what the taskbar shows**: temperature, usage, power and clock for the CPU and GPU, GPU memory temperature, RAM usage, network download/upload and disk read/write. Values are stacked two devices per block.
- **Longer history**: switch the details panel between the last 10 minutes, 1 hour and 24 hours. The 24-hour history is kept per minute (average, min and max) in `history.csv`, so it survives restarts. Charts show the average line with a min–max band, and hovering shows the peak.
- **Network and disk** readings in the details panel.
- **Update check**: once a day the app asks GitHub for the latest release and tells you when a new version is out (can be turned off in settings).
- **Turkish UI**, picked automatically when Windows is in Turkish, or chosen in settings.
- Releases are now built by GitHub Actions.

## 1.0.0

- First release: taskbar overlay with CPU/GPU temperature and usage, details panel with 10-minute charts, spike log, stability watcher (WHEA errors, blue screens, unexpected shutdowns), temperature notifications, settings window, start with Windows.
