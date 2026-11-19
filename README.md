# Timer

A lightweight countdown timer for Windows 10 and Windows 11. The interface stays quiet: open it, start a timer in one click, and get a local sound plus an on-screen alert when time runs out. No account, no network, and no personal data collection.

## Features

- Set hours, minutes, and seconds. Start, pause, resume, and reset.
- Quick presets: 1, 5, 10, 15, 25, 30, and 60 minutes. Choosing a preset starts it immediately.
- A progress ring and a large digital countdown.
- Three local alert sounds, volume, mute, preview, and an optional repeat until the alert is dismissed.
- An in-app alert when the countdown finishes. A Windows notification is also sent when the system allows it.
- Minimizing hides the window in the notification area and keeps the countdown running. The tray menu can open, start or pause, reset, or exit.
- Closing the window exits by default. Settings can change that to minimize to the notification area.
- Optional always-on-top, a compact floating timer, and light, dark, or system theme.
- Optional launch at Windows startup. Settings are stored locally in `%LocalAppData%\WindowsTimer\settings.json`.

## Keyboard

- `Space`: start or pause, unless focus is in a time field.
- `R`: reset, unless focus is in a time field.
- `Esc`: close Settings or the time-up alert.

## Timer accuracy

The countdown does not subtract one second on each UI tick. The engine uses a monotonic clock (`Stopwatch`) and derives the remaining time from elapsed time. UI refresh only redraws the display. Time spent paused is not counted. The completion event fires once per run.

System sleep pauses the countdown. The monotonic clock does not advance while Windows is suspended, so the timer continues from the remaining time at the moment sleep began. Sleep time is neither subtracted nor added. `SleepPolicy` documents this behavior, and tests cover it.

## Requirements

- Windows 10 version 1809 or later, or Windows 11
- Development: .NET SDK 10 and Inno Setup 6
- The installer is a self-contained x64 app. The target PC does not need a separate .NET install.

## Build and package

From the repository root:

```powershell
.\build.ps1
```

The script runs the tests, publishes a self-contained `publish\Timer.exe`, and uses Inno Setup to produce `dist\Timer-Setup.exe`.

Build and test only:

```powershell
dotnet test WindowsTimer.sln -c Release
dotnet build src\WindowsTimer\WindowsTimer.csproj -c Release
```

Publish manually:

```powershell
dotnet publish src\WindowsTimer\WindowsTimer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\Timer.iss
```

## Installation

Run `dist\Timer-Setup.exe`. The installer:

- Installs the app into the current user's programs folder (`%LocalAppData%\Programs\Timer` when no administrator rights are required)
- Creates a Start menu shortcut
- Offers a desktop shortcut named Timer, checked by default
- Registers an uninstaller, available from Windows Settings under Apps

The desktop shortcut can be cleared in the setup wizard. Closing the main window exits. The minimize button hides the window in the notification area and leaves the countdown running.

## Project layout

```
WindowsTimer/
├── src/WindowsTimer.Core/    timer engine, settings, and alert tones
├── src/WindowsTimer/         WPF interface and Windows integration
├── tests/WindowsTimer.Tests/
├── installer/Timer.iss
├── assets/GenerateIcon.ps1
└── WindowsTimer.sln
```

Diagnostic logs are written to `%LocalAppData%\WindowsTimer\logs\timer.log`. They record runtime status and exception types only, not personal information.

## Verification

These checks were run on this PC on 2026-11-19.

Verified:

- `dotnet test`: all 32 tests passed. Coverage includes countdown, pause and resume, reset, zero duration, a single completion event, repeated clicks, long and very short durations, the sleep policy, and settings and tone generation.
- The Release build succeeded and produced a self-contained `publish\Timer.exe`.
- `dist\Timer-Setup.exe` compiled. After a silent install, the program is at `%LocalAppData%\Programs\Timer\Timer.exe`.
- The desktop shortcut named Timer points at that program. The Start menu shortcut is in the Timer program group.
- The installed program launches, and the window title is Timer.
- Uninstall removes the program files, the desktop shortcut, and the Start menu shortcut. The app was reinstalled after that check.
- Light and dark interfaces were opened. The 25-minute preset was selected.
- After start, the window title changed from 25:00 to the remaining time. Pause stopped that change. Reset returned to Ready.
- Settings were written to `settings.json`, and the theme was set back to follow the system.
- The mini window is about 286 by 84 and shows the countdown. Expand returns to the main window.
- A 3-second countdown showed the Time's up window. The log did not record a sound playback failure.
- After minimize, the process kept running and the main window was hidden. The close button exited the process.

Not fully confirmed on this machine:

- The Windows notification banner was not visible in a screenshot. A failed call is written to the log; this completion did not log a failure, but the banner itself was not seen. The in-app alert and sound still work when system notifications are unavailable.
- Speaker output of the alert sound was not listened to separately.
- Real system sleep and resume were not exercised. The policy and monotonic-clock tests cover that behavior.
- The notification-area right-click menu items were not clicked one by one. Minimize leaving the process running was confirmed.
