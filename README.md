# AuroraPomodoro

A Windows x64 Pomodoro desktop application built with WPF.

## Core features

- Focus / Short Break / Long Break cycle (4 focuses per cycle)
- System tray operation (close-to-tray, tray menu)
- Session notifications
- Single-instance behavior
- Persistent settings
- AuroraGlass liquid-glass UI via the airspace-safe WPF D3DImage composition
  bridge (AuroraGlass 0.9.0 Release SDK)

## Current version

0.1.1

## Requirements

- Windows x64
- Microsoft .NET Desktop Runtime 10 (x64)
- Microsoft Visual C++ x64 Redistributable (AuroraGlass native interop)

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

## Dependency

AuroraPomodoro uses the official AuroraGlass 0.9.0 Release SDK
(GitHub release [VeiQiuLab/AuroraGlass v0.9.0](https://github.com/VeiQiuLab/AuroraGlass/releases/tag/v0.9.0),
asset `AuroraGlass-0.9.0-win-x64.zip`, sha256 `93cc1a8b...`).

## Release history

- 0.1.1 — first public build that keeps the real AuroraGlass liquid-glass UI
  while restoring visible and clickable timer controls, using the
  airspace-safe WPF D3DImage composition bridge.
- 0.1.0 — first public release (shipped with a native HwndHost airspace bug).

## License

Licensed under the [Apache License 2.0](LICENSE) (SPDX: `Apache-2.0`).
