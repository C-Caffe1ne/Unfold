# Unfold desktop — C# / .NET 10

Unfold now has a C# desktop runtime for Windows and macOS. Avalonia draws the UI;
SkiaSharp decodes PNG/GIF assets. Swift and Piskel JavaScript are not needed to
build or run this version. The original Swift project remains in the repository
as a reference.

`publish-desktop.ps1` copies this file into the packaged app as `README.md`, so
everything below describes what a person running the packaged build can actually
reach. The pixel editor and character library management (create/edit/delete) are
preserved in the source tree for internal reuse and tests, but this build has no
UI entry point for them — nothing to click, nowhere to open them from.

## Windows

The portable build lives in `artifacts/win-x64/Unfold.exe`. Extract the entire
`Unfold-win-x64.zip` archive and run `Unfold.exe` inside it. Keep the adjacent DLLs
and Assets folder together. The published app includes .NET; no SDK/runtime
installation is required for users. The local build is unsigned.

For development, install .NET SDK 10 and run from the repository root:

```powershell
dotnet restore Unfold.slnx
dotnet test Unfold.slnx
dotnet run --project src/Unfold.Desktop
```

Publish a self-contained build:

```powershell
./Scripts/publish-desktop.ps1 -Runtime win-x64
# Windows on ARM:
./Scripts/publish-desktop.ps1 -Runtime win-arm64
```

Closing Settings hides it to the tray. Use the tray menu or run the app again to
show Settings; only one instance runs per data directory. Use **Quit** to exit.
Launch at login is opt-in and registers the published executable under the
current Windows user's Run key. Move the app before enabling this option; if
you move an existing installation, toggle the option off and on again.

## macOS

The same C# sources target Intel and Apple Silicon:

```sh
dotnet test Unfold.slnx
dotnet run --project src/Unfold.Desktop
bash Scripts/make-macos-bundle.sh osx-arm64
# Intel: bash Scripts/make-macos-bundle.sh osx-x64
```

The bundle script includes assets and .NET in `artifacts/Unfold.app` and signs it
ad hoc for local use. Public distribution still requires Developer ID signing
and notarization. The CI matrix compiles/tests on Windows and macOS. A Windows
build is not evidence of tested macOS windowing, login, or notification behavior.

## Features

| Area | C# implementation |
| --- | --- |
| Reminders | 5–240 minute intervals; pause/resume/reset; monotonic elapsed time |
| Activity | Windows `GetLastInputInfo`; macOS CoreGraphics idle time; suspend/dispatcher gaps excluded |
| Tray | Settings, Hide/Show Pet, Pause/Resume, Reset timer, Stretch now, Quit, single-instance activation |
| Notifications | Manual-dismiss reminder window; Windows shell balloon; macOS notification via system scripting |
| Desktop pet | Sprite/GIF playback, click reaction, dragging, saved position, screen clamping |
| Hit testing | Alpha-aware interaction; transparent-pixel click-through on Windows |
| Character picker | Settings lists the built-in character plus any user characters already in the library; selecting one switches the active pet/reminder clip |

macOS desktop-pet window click-through is not implemented yet; alpha hit testing
still rejects transparent pixels inside the app. OS notification delivery depends
on system notification settings. The visible reminder window works independently.

### Preserved, not shipped

The pixel editor (drawing tools, layers, frames, Piskel import/export) and
character library management (create/edit/delete) have no UI entry point in
this build — nothing in the tray or Settings opens them. The code and its
manifest/sprite-sheet format are kept because `CharacterLibrary` also backs
playback of existing characters, and because the internal `--smoke-test`
diagnostic exercises the editor as a regression check. Existing character GIF
animations still play back normally in the shipped app.

## Data and migration

- Windows: `%LOCALAPPDATA%\Unfold\settings.json` and `Characters\`.
- macOS: `~/Library/Application Support/Unfold/`.
- For isolated testing, `UNFOLD_DATA_DIR` overrides the data directory.
- Piskel-format artwork can be read back into `CharacterLibrary` in code (the format
  and loader are preserved), but there is no in-app UI path to do this in the
  shipped build — the pixel editor is not exposed (see
  [Preserved, not shipped](#preserved-not-shipped)). Native Swift UserDefaults and
  sandboxed app settings are not automatically copied either way. The old app's
  data is not deleted.
- Built-in character files are shared from `Sources/Unfold/Resources/Characters`.
  There is a single source of truth for those assets, copied during build/publish.

The editable format supports 1–128 px per side, 1–24 frames and 1–16 layers.
Unsupported versions, hidden Piskel timeline frames, invalid geometry, mismatched
layers, path traversal and oversized files produce an error instead of silently
substituting a blank document.

## Reliability and performance

All pixel editing and timer logic live in `Unfold.Core`, independently of UI.
Strokes are interpolated across mouse events; each stroke is one undo transaction.
Pixel buffers use packed 32-bit color values and the drawing surface reuses its
bitmap. Composited frames and unchanged thumbnails are reused. History retains
at most 100 snapshots and approximately 32 MiB of undo pixel data. Animation
frames are decoded once per selected clip; concurrent consumers share a loading
task. Animation timing follows actual frame durations and hidden previews pause.

The portable build uses ReadyToRun compilation to reduce startup JIT work.
Trimming and NativeAOT are deliberately not enabled because reflection-driven
serialization and UI dependencies need separate compatibility validation.

Settings are written via a flushed temporary file and same-directory replacement.
Character saves use staging validation, an exclusive library lock, backup/rename,
rollback on failure and startup recovery after an interrupted directory swap.
This is recoverable replacement, not a claim that Windows directory renames form
one atomic operation. Revisions detect external edits/deletions before saving.
No automatic cloud sync or data upload is involved.

## Verification

```powershell
dotnet test Unfold.slnx -c Release
$env:UNFOLD_DATA_DIR = Join-Path $PWD '.tools/smoke-profile'
./artifacts/win-x64/Unfold.exe --smoke-test
```

The smoke test uses off-screen windows, suppresses system notifications, creates a
test character in the chosen data directory, opens/edits/saves/reopens it, renders
Settings/editor/pet/reminder PNGs, samples process statistics and exits. Do not
point its data directory at a real user library. If no override is supplied, it
uses a newly created temporary profile. Results are in `verification/smoke.json`
inside that profile. This does not exercise physical multi-monitor dragging,
actual login, or OS notification delivery; those need a manual desktop check.

Tests include pixel algorithms, source/PNG round trips, real bundled GIF decoding,
library overwrite/conflict/recovery, timer idle/sleep handling, and real Avalonia
control input/layout through the headless platform. Verification images generated
by the UI tests live in `artifacts/verification/`. Neither the test suite nor the
smoke test exercises macOS; treat macOS windowing, login, and notification
delivery as unverified from a Windows machine. Current pass/fail counts and dated
smoke-run evidence are tracked in the repository's `docs/release-checklist.md`
and `docs/agent-reports/` (not packaged with the app), not here.
