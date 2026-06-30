# GestureClip

GestureClip is a local Windows desktop tool for text clipboard history and right-button mouse gestures. It runs in the tray, stores data locally, and is designed for fast daily editing actions.

## Current MVP Features

- Tray resident WPF application
- Single-instance startup guard
- Text clipboard history with SQLite storage
- Clipboard search and quick paste overlay
- Global hotkey: `Ctrl + Alt + V`
- Global right-button mouse gestures
- Gesture presets and action HUD
- Clipboard and gesture pause/resume switches
- Privacy blacklist by process name
- Start with Windows
- Diagnostics panel
- Clipboard data cleanup

## Default Gestures

Edit Enhanced mode is the default preset:

- Up: Copy, `Ctrl + C`
- Down: Paste, `Ctrl + V`
- Up then Down: Enter
- Down then Up: Esc
- Left: Back, `Alt + Left`
- Right: Forward, `Alt + Right`
- Left then Right: Select all, `Ctrl + A`
- Right then Left: Undo, `Ctrl + Z`

Clipboard Enhanced mode keeps navigation and editing gestures, but uses:

- Up: Open clipboard history
- Down: Paste latest clipboard item

## Privacy and Data

GestureClip is local-first:

- Clipboard text is stored locally in SQLite.
- No cloud upload or sync is implemented.
- Clipboard recording can be paused.
- Process-level privacy blacklist is supported.
- Logs do not record clipboard text.

Default data locations:

- Database: `%LOCALAPPDATA%\GestureClip\gestureclip.db`
- Logs: `%LOCALAPPDATA%\GestureClip\logs\`

## Development

Requirements:

- Windows
- .NET 8 SDK

Build and test:

```powershell
dotnet restore .\GestureClip.sln
dotnet build .\GestureClip.sln --no-restore
dotnet test .\GestureClip.sln --no-restore
```

Run from source:

```powershell
dotnet run --project .\src\GestureClip.App\GestureClip.App.csproj
```

One-click launch from a downloaded repository:

```text
Start-GestureClip.bat
```

The launcher starts `artifacts/release/GestureClip/GestureClip.exe`. If the release executable is missing, it runs `scripts/publish-win-x64.ps1` first.

## Release

Create a Windows x64 self-contained single-file release:

```powershell
.\scripts\publish-win-x64.ps1
```

Check the release output:

```powershell
.\scripts\check-release.ps1
```

Release output:

```text
artifacts/release/GestureClip/
```

The main executable is `GestureClip.exe`.

## Common Questions

### Why does startup show a development warning?

When running with `dotnet run` or from `bin\Debug`, GestureClip treats the path as a development path. Use the published `GestureClip.exe` for start-with-Windows.

### Can GestureClip control elevated administrator windows?

Not reliably when GestureClip is running without administrator rights. Run GestureClip as administrator if you need to interact with administrator windows.

### Does GestureClip store images or files from the clipboard?

No. The MVP only stores text clipboard history.
