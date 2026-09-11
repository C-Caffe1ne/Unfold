# Unfold

A tiny desktop companion that reminds you to move while you work.

Written in **C# / .NET 10 with Avalonia** for Windows and macOS: a tray timer, an
animated desktop pet that stretches with you, and idle-aware reminders. See
[MVP scope](docs/mvp.md) for what ships and what does not.

## Run on Windows

Extract `artifacts/Unfold-win-x64.zip` and launch `win-x64/Unfold.exe`, keeping the
whole folder together. The portable build includes .NET. Use the tray icon to
open Settings; **Quit** exits the app.

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

- Draggable desktop companion that idles on your desktop and stretches when you click it.
- Stretch reminders with an animated character, plus Windows/macOS system notifications.
- Tray timer with pause/reset, custom intervals, automatic idle pause and sleep-gap handling.
- Four bundled cats — Mochi, Coco, Luna and Miso — swapped from the Settings picker,
  the tray's **Character** menu, or a right-click on the pet itself.
- Show/hide the pet and opt into launch at login, through Settings.
- Animated PNG sprite sheets and GIF character clips.

Windows has transparent-pixel pet click-through; that OS-level behavior is still
pending on macOS.

### Not in this release

The repository also contains a pixel editor and character library used to author the
built-in characters. Its user-facing entry points are disabled in the MVP build — the
code is preserved, not shipped. Details in [MVP scope](docs/mvp.md).

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

The C# build does not use Swift, WebKit, or the vendored Piskel runtime, though it
does link in the shared character assets under `Sources/Unfold/Resources/Characters/`.
Original Swift settings and sandbox data are preserved; they are not automatically
migrated.

See [MVP scope](docs/mvp.md) for what this release ships. The [cross-platform
guide](docs/cross-platform.md) covers data locations, packaging, performance choices,
verification and platform limits, and the old build instructions are preserved in the
[legacy Swift README](docs/legacy-swift.md).
