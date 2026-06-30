# GestureClip Regression Checklist

Use this checklist before publishing a build.

## Startup And Tray

- Program starts.
- Tray icon appears.
- Settings window opens.
- Tray Exit truly exits the app.
- Starting multiple times does not create multiple running instances.

## Clipboard

- Copying text saves a history item.
- Repeated identical text is not saved repeatedly.
- Pausing clipboard recording prevents new saves.
- Resuming clipboard recording saves again.
- `Ctrl + Alt + V` opens clipboard history.
- Search works.
- Pasting a history item works.
- Pasting a history item does not pollute history.

## Mouse Gestures

- Normal right click still opens context menus.
- Up gesture copies.
- Down gesture pastes.
- Up then Down sends Enter.
- Down then Up sends Esc.
- Left gesture sends Back.
- Right gesture sends Forward.
- Disabling gestures restores normal right-click behavior.
- Gesture HUD appears and follows the gesture path.
- Pages do not freeze or lose click input.

## Blacklist

- Adding `notepad.exe` prevents clipboard recording from Notepad.
- Removing `notepad.exe` restores recording from Notepad.
- Gesture blocking switch takes effect for a blacklisted process.

## Start With Windows

- Enabling startup creates `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\GestureClip`.
- Disabling startup removes the Run value.
- Published startup path points to `GestureClip.exe`, not `dotnet.exe`.

## Data Cleanup

- History count displays correctly.
- Clear unpinned keeps pinned items.
- Clear all removes pinned and unpinned items.
- Max item cleanup works.
- Retention-days cleanup works.

## Diagnostics

- Diagnostics can be copied.
- Diagnostics do not contain clipboard text.
- Open logs directory works.
- Open data directory works.

## Release Package

- Published `GestureClip.exe` starts.
- Tray icon appears in the release build.
- Database remains under `%LOCALAPPDATA%\GestureClip`.
- Logs remain under `%LOCALAPPDATA%\GestureClip\logs`.
