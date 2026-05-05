# Peek Mode

Status: closes source workstream `RS-05`  
Date: 2026-05-05

Peek mode launches Images as a chromeless, topmost preview surface for shell helpers, file managers, terminal previewers, and editor integrations.

## Invocation

```powershell
Images.exe --peek "C:\path\to\photo.jpg"
```

The contract is intentionally strict:

- Exactly two arguments: `--peek` and one file path.
- The path must resolve to an existing file.
- Device namespace paths such as `\\?\` and `\\.\` are rejected before the viewer opens.
- Extra flags are ignored by falling back to normal launch behavior rather than guessing.

## Behavior

- The window opens borderless, maximized, topmost, and image-first.
- Escape closes the peek window.
- The side panel and bottom toolbar are hidden.
- Window placement is not persisted while in peek mode.
- Background update checks do not run from peek windows.
- The opened image remains local; no network call is made by peek mode.

## Shell Helper Example

Save a small PowerShell helper somewhere on your PATH:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$images = Join-Path $env:ProgramFiles "Images\Images.exe"
if (-not (Test-Path -LiteralPath $images)) {
    $images = Join-Path $env:LOCALAPPDATA "Programs\Images\Images.exe"
}

& $images --peek (Resolve-Path -LiteralPath $Path)
```

File managers or editor integrations can call the helper with the selected file path. Do not keep a resident background process; launch on demand.

## Timing Diagnostics

Images writes local startup timing milestones to the rolling log under:

```text
%LOCALAPPDATA%\Images\Logs\
```

Milestones include app startup entry, main window creation/show, peek window preparation/show, argv open completion, and first image displayed. These timings are local diagnostics only.

## Manual Smoke

Before release:

1. Run `Images.exe --peek "C:\path\to\photo.jpg"`.
2. Confirm the window is borderless, topmost, and image-only.
3. Press Escape and confirm the window closes.
4. Reopen normal Images and confirm prior window geometry was not overwritten by the peek session.
5. Inspect the latest log and confirm startup timing milestones were recorded.
