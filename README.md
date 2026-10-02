# Unfold

A small desktop companion that reminds you to stretch while you work.

Unfold runs in the Windows tray or macOS menu bar. A timer counts active computer
use, pauses while you are away, and invites you to a short break with Mochi the cat.
Start when ready, or snooze for five minutes. Mochi stretches
when you start. Confirming the finished routine saves a local completion record.

## Run

For development, install .NET SDK 10 and run from this checkout on macOS:

```sh
bash Scripts/run-desktop.sh
```

On Windows, use `./Scripts/run-desktop.ps1` in PowerShell. These launchers build
and run the current Release source with a new temporary data directory on each
run. Set `UNFOLD_DATA_DIR` explicitly to reuse a development profile. Edit source
under `src/` and `Assets/`; build and published copies are generated output.

For a Windows portable build, extract the entire `Unfold-v<version>-win-x64.zip`
archive and double-click `win-x64/Unfold.cmd`. Keep the `app` folder next to it; the
published build includes .NET.

Closing Settings keeps Unfold running. Use **Unfold 종료** in the tray menu to exit.

## Features

- Five bundled pets with idle and pointer reactions, dragging, saved position, and Show/Hide controls.
- Automatic stretch reminders with a 1–10 minute break duration, snooze, and explicit completion.
- Existing routine and work-profile data remains readable for compatibility; their setup UI is no longer exposed.
- Today's confirmed breaks, seven-day reviews, and local CSV export.
- Local pet packs: preview animations, install, update, and reinstall from a saved `.unfoldpet` file.
- A 5–240 minute timer with one-minute adjustment while paused or stopped, followed by **저장**.
- A clear running/paused/stopped state badge and icon controls for Play/Pause and Stop. Stop displays the saved full interval; Play starts it.
- Pet speech bubbles for break invitations, progress, and completion notices.
- Opt-in launch at login; configure it from the published app in its final location.

Transparent-pixel click-through is implemented only for Windows. Actual OS behavior
and distribution readiness are tracked separately from automated tests in the
[verification guide](docs/verification.md).

The pixel editor is retained for diagnostics and regression tests. It has no
user-facing entry point in this MVP. See [MVP scope](docs/mvp.md) for the full boundary.
Google sign-in and server purchase checks are connected through the first-run account
screen. Global access enforcement, automatic login, and offline entitlements remain
future work; see the [account guide](docs/account-screen.md). See the
[routine, profile, and review guide](docs/personalization.md) for compatibility.
The [pet pack guide](docs/pet-packs.md) explains local installation and recovery. No store or purchase recovery is included.

## Build and verify

```sh
dotnet test Unfold.slnx -c Release
```

Build a self-contained Windows portable app in PowerShell:

```powershell
./Scripts/publish-desktop.ps1 -Runtime win-x64
```

Build a macOS application bundle on a Mac:

```sh
bash Scripts/make-macos-bundle.sh osx-arm64
```

The default macOS bundle is signed ad hoc for local testing. The script supports
Developer ID signing but does not perform notarization. Installation, data paths,
and platform limits are documented in the [cross-platform guide](docs/cross-platform.md).

## Project layout

```text
src/Unfold.Core/       Timer, settings, image codecs, character library, pixel model
src/Unfold.Desktop/    Avalonia UI, runtime, platform adapters, diagnostics
Tests/Unfold.Tests/    C# core, codec, library, and headless UI tests
Assets/Characters/    Built-in character packages copied into the application
Scripts/              C# run, publish, and macOS bundle scripts
Packaging/            macOS direct-distribution entitlements
docs/                 Current guides, verification records, reference, and archive
Art/Characters/      Authoring/provenance ledger, excluded from app bundles
```

The runtime uses C#/.NET 10, Avalonia, and SkiaSharp. Swift/Xcode and the Piskel web
runtime are not part of this checkout. Historical implementations and plans are
described in the [archive index](docs/archive/README.md).

Start with the [documentation index](docs/README.md) before changing product scope
or following an old plan. Third-party component notices are in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The normal app UI is in Korean. See the [Korean UI guide](docs/localization.md) for scope and data compatibility.
