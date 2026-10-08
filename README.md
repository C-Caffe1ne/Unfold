# Unfold

**Beta v1.1.2 (development)** · C#/.NET 10 · Avalonia · macOS / Windows

Current development version: `1.1.2-beta`. Latest public release: `v1.1.1-beta`.

A small desktop companion that reminds you to stretch while you work.

Unfold runs in the Windows tray or macOS menu bar. A timer counts active computer
use, pauses while you are away, and invites you to a short break with a pet.
Choose one of five pets, start when ready, or snooze. The pet stretches when you
start. Confirming the finished break saves a local completion record.

## Run

For development, install .NET SDK 10 and run:

```sh
dotnet restore Unfold.slnx --locked-mode
dotnet run --project src/Unfold.Desktop
```

For a Windows portable build, extract the entire `Unfold-v<version>-win-x64.zip`
archive and run the root `Unfold.exe` launcher. Keep the extracted folder intact;
the published build includes .NET.

Closing Settings keeps Unfold running. Use **Unfold 종료** in the tray menu to exit.

## Features

- Five built-in pets with idle reactions, click/hold/release animations, dragging, saved position, size and Show/Hide controls.
- Stretch reminders delivered in the pet's speech bubble, snooze and explicit break completion.
- Separate stretch interval, break duration, away threshold and snooze settings.
- Today's confirmed breaks, seven-day reviews, and local CSV export.
- Local pet packs: preview animations, install, update, and reinstall from a saved `.unfoldpet` file.
- A 1–240 minute timer with one-minute adjustment while paused or stopped, followed by Save.
- A clear running/paused/stopped state badge and icon controls for Play/Pause and Stop. Stop shows the saved full interval.
- Four color themes with Lilac as the default, Korean UI and custom SVG icons.
- Custom pets from GIF/MP4 or still images; WAV/MP3 effects with master and separate notification volumes.
- Google sign-in, checkout/purchase-confirmation screens, email display and sign-out.
- Account-bound free access codes for testers, restored after sign-in without entering the code again.
- Opt-in launch at login; configure it from the published app in its final location.

Transparent-pixel click-through is implemented for Windows and macOS. Visible pet pixels
remain interactive; transparent pixels pass through to the window underneath. Actual OS behavior
and distribution readiness are tracked separately from automated tests in the
[verification guide](docs/verification.md).

The unused pixel editor and routine/profile authoring components have been removed.
Existing pet sources, routine/profile settings and history remain compatible.
See [MVP scope](docs/mvp.md) for the full boundary.
The beta requires Google sign-in and either a verified Live purchase or a valid account-bound free grant.
Testers can redeem `admin` from the bottom-right code button after sign-in.
Saved sign-in is restored before the account window opens. Production OAuth and
payment checks are tracked separately in the [account guide](docs/account-screen.md).
Public Beta v1.1.1 Mac installers are Developer ID signed and notarized; Windows
installers are unsigned. See the [compatibility and review guide](docs/personalization.md).
The [pet pack guide](docs/pet-packs.md) explains local installation and recovery.
[Latest public release notes](docs/releases/v1.1.1-beta.md) describe installation, included work and beta limitations.

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
Developer ID signing and Apple notarization when the corresponding local
certificate and notarytool profile are configured. Installation, data paths,
and platform limits are documented in the [cross-platform guide](docs/cross-platform.md).

## Project layout

```text
src/Unfold.Core/       Timer, settings, codecs, character library, legacy compatibility
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
