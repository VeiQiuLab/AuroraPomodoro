# AuroraPomodoro

A Windows x64 Pomodoro desktop application built on the AuroraGlass Liquid
Glass UI SDK.

## Core features

- Focus / Short Break / Long Break cycle (4 focuses per cycle)
- System tray operation (close-to-tray, tray menu)
- Session notifications
- Single-instance behavior
- Persistent settings
- AuroraGlass UI (glass surface via the AuroraGlass WPF adapter)

## Current version

0.1.0

## Requirements

- Windows x64
- Microsoft .NET Desktop Runtime 10 (x64)
- Microsoft Visual C++ x64 Redistributable

## Installation

Use `AuroraPomodoro-0.1.0-win-x64-setup.exe` (per-user install, no admin required).

Default install location:

    %LOCALAPPDATA%\Programs\AuroraPomodoro

## Portable

`AuroraPomodoro-0.1.0-win-x64.zip`

## Settings

    %LOCALAPPDATA%\AuroraPomodoro\settings.json

Settings are preserved across update and uninstall.

## Signing

The 0.1.0 installer is currently unsigned. Windows SmartScreen may show an
"Unknown Publisher" warning.
