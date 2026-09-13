# Unfold desktop — Windows and macOS

<<<<<<< HEAD
Unfold is a C#/.NET 10 and Avalonia stretch reminder with a desktop cat. Published
packages include .NET. Swift, Xcode, and the Piskel web runtime are not required.
=======
Unfold now has a C# desktop runtime for Windows and macOS. Avalonia draws the UI;
SkiaSharp decodes PNG/GIF assets. Swift and Piskel JavaScript are not needed to
build or run this version. The original Swift project remains in the repository
as a reference.

`publish-desktop.ps1` copies this file into the packaged app as `README.md`, so
everything below describes what a person running the packaged build can actually
reach. The pixel editor and character library management (create/edit/delete) are
preserved in the source tree for internal reuse and tests, but this build has no
UI entry point for them — nothing to click, nowhere to open them from.
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163

## Windows installation

Extract the entire `Unfold-win-x64.zip` and launch `win-x64/Unfold.exe`. Keep the
executable, DLLs, and `Assets` directory together. The local portable build is unsigned.

Closing Settings hides it to the tray. Run the app again or use the tray menu to
open Settings. **Quit Unfold** exits the process. The app uses one instance per data
directory.

Launch at login is optional. Move the published app to its final folder before
enabling it. It registers the executable under the current user's Windows `Run`
registry key. If the app is moved later, turn this option off and on again from
the new location. A checked setting does not prove an actual login launch worked.

## macOS installation

Place `Unfold.app` in its final location and open it. Settings closes to the menu
bar; **Quit Unfold** exits. Login launch uses
`~/Library/LaunchAgents/app.unfold.desktop.plist` and starts the app with
`--background`, keeping Settings hidden.

The default local bundle is signed ad hoc. Public distribution needs a separate
Developer ID signing and notarization process. The bundle script supports a
signing identity through `UNFOLD_CODESIGN_IDENTITY`; it does not submit to
notarization or staple a ticket.

## Visible controls

- Tray/menu bar: countdown, Settings, Hide Pet/Show Pet, Start/Pause/Resume, Stop timer,
  Reset timer, Quit Unfold.
- Pet right-click menu: Settings.
- Settings: timer interval, idle threshold, character selection, pet visibility,
  launch at login, routine selection, **Edit my routine**, **My routines & work profiles**,
  **Review & export**, today's confirmed breaks, and timer controls.
- Reminder: start the selected built-in or custom routine, **In 5 minutes**, or **Skip this break**.
  **I'm refreshed** becomes available after the routine timer and records confirmation.
  Closing the window records no completion. Snooze counts active time, not time away.

Settings uses three icon buttons: Pause/Play, Stop, and Reset. Each has a tooltip and
an accessibility name. Pause keeps the remaining time; Stop shows 00:00; Reset restores
the configured interval and pauses. Use Play to resume or start again. Stop and Reset
dismiss an open invitation without recording completion. Stretch now is no longer exposed.

Changing **Remind me every (min)** to a valid whole minute from 5 to 240 saves and
applies it immediately. Typed input can be committed with Enter. The current running/paused/
stopped state is preserved. Use **Apply reminder settings** for the away threshold and routine.

A plain click on bundled Mochi keeps its current animation; Mochi has no click
clip. The pet stretches when the user starts the routine and then returns to idle. A hidden
pet stays hidden while the reminder window still appears.

An open reminder holds the work timer. Completion schedules a full work interval;
snooze schedules five active minutes; skip/close keeps the remaining work interval.
Existing manual Pause remains in effect. An already-open reminder is reused.
Manual work profiles combine the routine and timer settings. The library and review
workflow is described in the [personalization guide](personalization.md).

## Platform limits

- Transparent-pixel click-through is implemented for Windows only. macOS rejects
  transparent pixels inside the app's hit test, but does not pass those clicks
  through to another application's window.
- System notification delivery depends on OS settings. The in-app reminder works
  independently and activates its window; interruption during focused work needs
  actual user testing.
- Windows/macOS login launch, multi-monitor dragging, display changes, suspend,
  full-screen behavior, and installation trust need tests on the target OS.
- A portable build does not include an installer or an automatic updater.

## Data and compatibility

| Platform | Application data |
|---|---|
| Windows | `%LOCALAPPDATA%\Unfold\` |
| macOS | `~/Library/Application Support/Unfold/` |

`settings.json` stores preferences, the routine library and manually applied work profiles. `Characters/` holds user-created packages and
`unfold.log` records errors. `UNFOLD_DATA_DIR` overrides this directory for isolated
testing. Bundled assets are separate, under the application's `Assets/Characters/`.

`break-history.json` stores confirmed routine completions locally, with session ID,
timestamp, routine, planned seconds, companion ID, and optional routine/profile name snapshots. It contains no keystrokes or
application-usage tracking. Adding a record prunes entries older than 90 days and
keeps at most 2,000. A corrupt history is left intact; Settings explains when new
completions are only kept in memory. Completed seconds describe the selected
routine, not verified physical activity.
The seven-day review exports only its displayed period to a user-selected local CSV file.
No export is uploaded or shared automatically. Older settings and completion records remain readable.

Existing valid user character packages remain selectable. The editor has no
normal UI entry point in the MVP. Legacy Swift preferences and sandbox data are
neither migrated nor deleted automatically.

## Development and packaging

Run these commands from the repository with .NET SDK 10 installed:

```sh
dotnet restore Unfold.slnx --locked-mode
dotnet test Unfold.slnx -c Release --no-restore
dotnet run --project src/Unfold.Desktop
```

On Windows, publish using PowerShell:

```powershell
./Scripts/publish-desktop.ps1 -Runtime win-x64
# Windows ARM64:
./Scripts/publish-desktop.ps1 -Runtime win-arm64
```

On macOS:

```sh
bash Scripts/make-macos-bundle.sh osx-arm64
# Intel:
bash Scripts/make-macos-bundle.sh osx-x64
```

The CI matrix builds/tests Windows x64 and macOS arm64, then packages them. It does
not run the packaged apps or verify physical OS interaction. Other supported
script arguments are not proof of tested architectures.

## Diagnostic authoring path

<<<<<<< HEAD
The retained C# editor, pixel model, and Piskel v2 codec are exercised by regression
tests and the explicit `--smoke-test` diagnostic. They are not visible MVP features.
The authoring model supports 1–128 px per side, 1–24 frames, and 1–16 layers;
existing PNG sprite sheets and GIF character clips are decoded for playback.

From a repository checkout:

```sh
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
=======
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
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163
```

Without `UNFOLD_DATA_DIR`, this creates a new temporary profile. With an override,
use a new empty directory, never a real user library. The diagnostic opens off-screen
windows, suppresses system notifications, creates and edits test artwork, checks a
save/reopen round trip, edits and saves a custom routine, exercises reminder start/confirm/snooze/close,
captures seven PNGs, records a short process
sample, and exits. Read `verification/smoke.json` in that profile.

<<<<<<< HEAD
The diagnostic advances the break session clock programmatically and confirms via
the UI buttons. It verifies session wiring and persisted records, not real elapsed
break duration or a person's movements. Asset-only inspection is available through
`Unfold --validate-characters [directory]`; it opens no windows and writes no profile.
See [pet resource management](pet-resources.md).

A successful smoke run verifies that diagnostic path only. It does not verify real
notification delivery, physical dragging, login startup, full animation playback,
long-term resource use, or whether a person actually stretches.
=======
Tests include pixel algorithms, source/PNG round trips, real bundled GIF decoding,
library overwrite/conflict/recovery, timer idle/sleep handling, and real Avalonia
control input/layout through the headless platform. Verification images generated
by the UI tests live in `artifacts/verification/`. Neither the test suite nor the
smoke test exercises macOS; treat macOS windowing, login, and notification
delivery as unverified from a Windows machine. Current pass/fail counts and dated
smoke-run evidence are tracked in the repository's `docs/release-checklist.md`
and `docs/agent-reports/` (not packaged with the app), not here.
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163
