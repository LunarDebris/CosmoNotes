# CosmoBoard

A small frameless, cross-platform C#/.NET + Avalonia desktop widget for displaying the newest `YYYY-MM-DD.md` file in a selected folder.

## Features

- Windows and Linux desktop support.
- Finds the newest file by the date encoded in its filename.
- Recognizes task lines such as `- [ ] Task` and `- [x] Completed task`.
- Other lines are displayed under **Notes**.
- Borderless window with a compact date header.
- Drag the window from the header.
- Drag the bottom-right corner to resize.
- Right-click for Refresh, Always on Top, Background/Tray, Settings, and Exit.
- Optional system tray mode.
- Folder selection is stored in `%APPDATA%/CosmoBoard/settings.json` on Windows and the corresponding application-data location on Linux.
- Automatically refreshes when Markdown files in the selected folder change.

## Requirements

- .NET 8 SDK
- Internet access for the first `dotnet restore` so NuGet can download Avalonia packages.
- The Makefile repairs missing Windows profile environment variables when invoked from MSYS/MINGW/Cygwin before launching native `dotnet.exe`.
- `make` for the Makefile commands.

## Build

```sh
make build
```

If you are diagnosing an MSYS/MINGW environment, `make doctor` prints the Windows profile paths that will be passed to `dotnet` without invoking .NET itself.

`make clean` is intentionally filesystem-only (`rm -rf bin obj .nuget NuGet`) and never invokes `dotnet clean`, so it remains safe even when NuGet initialization is broken.

Run locally:

```sh
make run
```

Publish a self-contained Windows x64 executable:

```sh
make publish-win
```

Publish a self-contained Linux x64 executable:

```sh
make publish-linux
```

Publish both:

```sh
make publish-all
```

The published application is placed under `bin/publish/`.

## Markdown format

Tasks are recognized from lines matching:

```md
- [ ] An incomplete task
- [x] A completed task
```

`[X]` is also treated as completed. Other Markdown content is displayed as notes, with simple heading markers and unordered-list markers removed for a compact widget presentation.

## Configuration

Settings are stored in a JSON file so the folder can also be changed without using the UI. The exact platform-specific application-data directory is selected by .NET's `Environment.SpecialFolder.ApplicationData`.

A sample file is included at `SampleNotes/2026-09-12.md`.

## Interaction

- **Left-drag the date header** to move the window.
- **Drag the bottom-right grip** to resize it.
- **Right-click** the widget for its context menu.
- **Refresh** immediately rereads the newest dated file.
- **Background / Tray** hides the widget while leaving it running in the system tray.


## MSYS / Git Bash build notes

The Makefile mirrors the Windows/MSYS environment-repair section from the known-working Makefile supplied for comparison. Run `make doctor`, then `make clean`, then `make build`. The clean target never invokes `dotnet`.

## fixed6 compile fixes

This revision keeps the fixed5 MSYS/NuGet environment handling and fixes the Avalonia/C# compile errors reported with Avalonia 11.3.20:

- Removed `WithInterFont()` because the Inter font package is not referenced.
- Renamed the generated `Content` field to `ItemsPanel` so it no longer hides `Window.Content`.
- Attaches the context menu directly to the named root border instead of walking the visual tree.
- Removed the invalid `PositionProperty` check; window position is still saved whenever bounds are persisted/closed.
- Captures the resize pointer as `IInputElement` rather than `Visual`.
- Explicitly names the tuple fields in `MarkdownFileService` so `candidate.date` is preserved.
