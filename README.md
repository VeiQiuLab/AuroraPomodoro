# AuroraPomodoro

A Windows x64 Pomodoro desktop application built with WPF.

## Core features

- Focus / Short Break / Long Break cycle (4 focuses per cycle)
- System tray operation (close-to-tray, tray menu)
- Session notifications
- Single-instance behavior
- Persistent settings
- Pure WPF UI

## Current version

0.1.1

## Requirements

- Windows x64
- Microsoft .NET Desktop Runtime 10 (x64)

## Installation

Use `AuroraPomodoro-0.1.1-win-x64-setup.exe` (per-user install, no admin required).

Default install location:

    %LOCALAPPDATA%\Programs\AuroraPomodoro

## Portable

`AuroraPomodoro-0.1.1-win-x64.zip`

## Settings

    %LOCALAPPDATA%\AuroraPomodoro\settings.json

Settings are preserved across update and uninstall.

## Signing

The 0.1.1 installer is currently unsigned. Windows SmartScreen may show an
"Unknown Publisher" warning.

## Release history

- 0.1.1 — hotfix: removed the native HwndHost composition path that caused the
  0.1.0 UI airspace failure, restoring visible and clickable timer controls.
- 0.1.0 — first public release.

## License

Licensed under the [Apache License 2.0](LICENSE) (SPDX: `Apache-2.0`).
