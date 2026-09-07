# Unfold

A desktop stretch reminder and pixel-art companion, now written in **C# / .NET 10
with Avalonia** for Windows and macOS. Includes a tray timer, animated desktop pet,
character library, and an integrated pixel editor.

## Run on Windows

Extract `artifacts/Unfold-win-x64.zip` and launch `win-x64/Unfold.exe`, keeping the
whole folder together. The portable build includes .NET. Use the tray icon to
open Settings or the editor; **Quit** exits the app.

For development (requires .NET SDK 10):

```powershell
dotnet restore Unfold.slnx
dotnet test Unfold.slnx -c Release
dotnet run --project src/Unfold.Desktop
```

Build the portable app:

```powershell
./Scripts/publish-desktop.ps1 -Runtime win-x64
```

Windows ARM64 uses `-Runtime win-arm64`. On a Mac, run
`bash Scripts/make-macos-bundle.sh osx-arm64` (or `osx-x64` for Intel).

## Features

- Tray timer with pause/reset, custom intervals, automatic idle pause and sleep-gap handling.
- Animated PNG sprite sheets and GIF character clips, draggable desktop pet and stretch reminders.
- Pixel editor: pencil, eraser, fill, eyedropper, line, rectangle, ellipse;
  layers, animation frames, Undo/Redo, resize, flip, zoom, grid and onion skin.
- Piskel v2 compatibility and PNG import/export; validated character saves with
  external-change detection and interrupted-save recovery.
- Windows/macOS launch at login, opt-in through Settings.

The editor supports 1–128 px per side, 1–24 frames and 1–16 layers. Selection/move
tools and GIF editing/export are not implemented. Windows has transparent-pixel
pet click-through; that OS-level behavior is still pending on macOS.

## Project layout

```text
src/Unfold.Core/          Pixel model, codecs, library, settings, timer
src/Unfold.Desktop/       Avalonia UI, tray, native platform adapters
Tests/Unfold.Tests/       C# regression and UI tests
Scripts/                 Run/publish/bundle scripts
Sources/Unfold/Resources/Characters/
                         Shared built-in character assets
Sources/, Package.swift, Unfold.xcodeproj/
                         Preserved legacy Swift implementation
```

The C# build does not use Swift, WebKit, or the vendored Piskel runtime. Existing
artwork can be imported through **Pixel Editor → Open → source.piskel**. Original
Swift settings and sandbox data are preserved; they are not automatically migrated.

See [cross-platform guide](docs/cross-platform.md) for data locations, packaging,
performance choices, verification and platform limits. The old build instructions
are preserved in [legacy Swift README](docs/legacy-swift.md).
