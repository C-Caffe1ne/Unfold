# Unfold desktop — Windows and macOS

Unfold is a C#/.NET 10 and Avalonia stretch reminder with desktop pets. Published
packages include .NET. Swift, Xcode, and the Piskel web runtime are not required.

Version 0.2.1 includes Mochi (cat), 보리 (rabbit), 강아지 (dog), 고슴도치 (hedgehog)
and 펭귄 (penguin). Choose them from Home without installing separate packs.
Release notes are included as `RELEASE-NOTES.md` at the Windows portable folder's
top level and inside `Unfold.app/Contents/Resources/` on macOS.

## Windows installation

Extract the entire `Unfold-v<version>-win-x64.zip` and double-click `win-x64/Unfold.cmd`.
It launches `win-x64/app/Unfold.exe`. Keep the `app` folder next to the launcher;
it holds the executable, DLLs, and `Assets` directory. The local portable build is
unsigned. Launch at login registers the real path inside `app`, so login launch
keeps working even without the launcher after the first run.

Closing Settings hides it to the tray. Run the app again or use the tray menu to
open Settings. **Unfold 종료** exits the process. The app uses one instance per data
directory.

Launch at login is optional. Move the published app to its final folder before
enabling it. It registers the executable under the current user's Windows `Run`
registry key. If the app is moved later, turn this option off and on again from
the new location. A checked setting does not prove an actual login launch worked.

## macOS installation

Place `Unfold.app` in its final location and open it. Settings closes to the menu
bar; **Unfold 종료** exits. Login launch uses
`~/Library/LaunchAgents/app.unfold.desktop.plist` and starts the app with
`--background`, keeping Settings hidden.

The default local bundle is signed ad hoc. Public distribution needs a separate
Developer ID signing and notarization process. The bundle script supports a
signing identity through `UNFOLD_CODESIGN_IDENTITY`; it does not submit to
notarization or staple a ticket.

## Visible controls

- Tray/menu bar: countdown and current state, 설정, 펫 숨기기/펫 표시,
  시작/일시정지/계속, 타이머 정지, Unfold 종료.
- Pet right-click menu: 설정, 펫 숨기기.
- Settings: the timer home contains stretch interval and break duration; a separate settings tab contains idle time, snooze time, stretch/completion sounds and bubble position; character selection, pet visibility,
  launch at login, **Review & export**, **펫 추가**, today's confirmed breaks, and timer controls.
- Pet speech reminder: **n분 뒤에**, **휴식 시작**, and **완료**. The bubble remains visible while its reminder state is active.
  Settings offers four bubble positions, 1–60 minute snooze, and due/completion WAV effects.
  There is no separate reminder window or OS toast.
  **완료** is available from the start of a break. Overtime caps at +60:00 without auto-completion.
  Snooze counts active time, not time away.

Settings uses two icon buttons: Pause/Play and Stop. Each has a tooltip and an
accessibility name. Pause keeps the remaining time; Stop shows the configured full interval. Use Play to resume
or to start a full interval after Stop. Stop dismisses an open invitation without recording
completion. Reset and Stretch now are no longer exposed.

The timer state badge and tray status explicitly show running, paused, stopped, idle-paused,
or break-held state. The home card's **스트레칭 시간 (분)** is disabled while running and accepts whole
minutes from 5 to 240 in one-minute steps while paused or stopped. **휴식 시간 (분)** accepts 1–10
minutes and may be changed while working because it applies to the next break. A typed or stepped value
does not change behavior until the home card's **저장** is pressed. The Settings tab uses its own
**취소·저장** for idle/snooze time and notification preferences. Saving a new stretch interval keeps Pause or Stop intact.

All five bundled pets provide ten behaviors. Eligible idle time triggers sleep, looking around
or yawning; a short click squashes, bounces and plays surprise followed by looking around.
The third consecutive snooze triggers sulking. Starting a break plays one stretch before walking
inside the current monitor's work area; completion, stop, hiding or selecting another pet ends movement.
A hidden pet can appear temporarily for a new reminder without changing the saved visibility setting.

An open reminder holds the work timer and retains the break duration captured when it opened. Completion schedules a full work interval;
snooze schedules five active minutes; skip/close keeps the remaining work interval.
Existing manual Pause remains in effect. An already-open reminder is reused.
Routine and work-profile setup is no longer exposed in the normal UI. Review/export and
legacy data compatibility are described in the [compatibility guide](personalization.md).

## Platform limits

- Transparent-pixel click-through is implemented for Windows only. macOS rejects
  transparent pixels inside the app's hit test, but does not pass those clicks
  through to another application's window.
- Reminders use the pet speech bubble, not an OS notification or a separate reminder window.
  Interruption during focused work still needs actual user testing.
- Windows/macOS login launch, multi-monitor dragging, display changes, suspend,
  full-screen behavior, and installation trust need tests on the target OS.
- A portable build does not include an installer or an automatic updater.

## Data and compatibility

| Platform | Application data |
|---|---|
| Windows | `%LOCALAPPDATA%\Unfold\` |
| macOS | `~/Library/Application Support/Unfold/` |

`settings.json` stores preferences, including `breakDurationMinutes`, and retains legacy routine/profile values so upgrades do not delete them. `Characters/` holds user-created packages and
`Sounds/` holds copied custom WAV effects and generated default effects; `unfold.log` records errors. `UNFOLD_DATA_DIR` overrides this directory for isolated
testing. Bundled assets are separate, under the application's `Assets/Characters/`.

`break-history.json` stores confirmed routine completions locally, with session ID,
timestamp, routine, planned seconds, optional actual seconds, companion ID, and optional routine/profile name snapshots. It contains no keystrokes or
application-usage tracking. Adding a record prunes entries older than 90 days and
keeps at most 2,000. A corrupt history is left intact; Settings explains when new
completions are only kept in memory. Completed seconds describe the selected
routine, not verified physical activity.
The seven-day review exports only its displayed period to a user-selected local CSV file.
No export is uploaded or shared automatically. Older settings and completion records remain readable.

Existing valid user character packages remain selectable. The editor has no
normal UI entry point in the MVP. Legacy Swift preferences and sandbox data are
neither migrated nor deleted automatically.

**펫 추가** opens a local `.unfoldpet` file for preview before installation.
Preview controls use the current theme background and offer 100–200% display size, Pause/Resume,
and Replay. Display size affects this preview only. Completed reactions return to
resting while keeping the selection available for replay.
The **저장** action installs a new ID, updates a newer content version, or reinstalls
the same version. Reinstall restores damaged/missing runtime images from a saved pack.
Built-in companions and existing user-authored IDs cannot be replaced. Invalid archives,
hash/decoder failures and changed installed files are rejected. Successful installation selects
the companion without resuming a paused timer. There is no store, automatic download, or
account purchase recovery. See the [pet pack guide](pet-packs.md).

The sidebar **펫 추가** page switches between **펫 팩 열기** and **펫 팩 만들기** in place.
The action cards on the create tab import GIF/MP4 files. Drafts survive tab navigation and hiding the settings window.
Assign files to five supported actions (idle required), preview each, then save a `.unfoldpet`
and install it through the same preview page. MP4 conversion is local, silent, limited to
10 seconds/128 MiB, and resized proportionally to at most 192px at 12 fps. GIF import preserves
source pixels/timing. Opaque video backgrounds remain visible; there is no background removal.

## Development and packaging

Run these commands from the repository with .NET SDK 10 installed:

```sh
dotnet restore Unfold.slnx --locked-mode
# MP4 import: use the matching RID (win-x64, win-arm64, osx-arm64, osx-x64).
dotnet run --project tools/Unfold.MediaSetup -- osx-arm64 .
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

The CI matrix builds/tests Windows x64 and macOS arm64, then packages them. The Windows
job also defines an isolated packaged smoke run. This is automated diagnostic coverage,
not physical OS interaction or evidence that the current workflow has already passed.
Other supported script arguments are not proof of tested architectures.

Both publishing scripts prepare the pinned LGPL FFmpeg 8.1.2 build in `.tools/media-lgpl/<rid>/`.
The setup tool verifies fixed SHA-256 checksums before extraction and retains upstream licenses,
source provenance, and Windows support DLLs. The first preparation needs network access; cached
archives are reverified on later runs. Normal builds copy prepared files into `Tools/`. The app
does not download software at runtime. `UNFOLD_FFMPEG_PATH` can point to an absolute FFmpeg path
for development; the bundled executable and then `PATH` are the fallbacks. GIF import needs no
FFmpeg. Windows Arm uses the x64 FFmpeg process via Windows x64 emulation; actual Windows/Arm
execution still needs target-OS testing.

## Diagnostic authoring path

The retained C# editor, pixel model, and Piskel v2 codec are exercised by regression
tests and the explicit `--smoke-test` diagnostic. They are not visible MVP features.
The authoring model supports 1–128 px per side, 1–24 frames, and 1–16 layers;
existing PNG sprite sheets and GIF character clips are decoded for playback.

From a repository checkout:

```sh
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

Without `UNFOLD_DATA_DIR`, this creates a new temporary profile. With an override,
use a new empty directory, never a real user library. The diagnostic opens off-screen
windows, suppresses system notifications, creates and edits test artwork, checks a
save/reopen round trip, edits routines/profiles, exercises reminder start/confirm/snooze/close,
exports review CSV and checks timer controls. It also previews, installs, updates and repairs a
diagnostic pet pack, and rejects a malformed archive. It captures diagnostic PNGs, records a short process
sample, and exits. Read `verification/smoke.json` in that profile.

The diagnostic advances the break session clock programmatically and confirms via
the UI buttons. Pack selection and CSV destinations are injected; it does not exercise
the OS file picker. It verifies session wiring and persisted records, not real elapsed
break duration or a person's movements. Asset-only inspection is available through
`Unfold --validate-characters [directory]`; it opens no windows and writes no profile.
See [pet resource management](pet-resources.md).

A successful smoke run verifies that diagnostic path only. It does not verify real
notification delivery, physical dragging, login startup, full animation playback,
long-term resource use, or whether a person actually stretches.
