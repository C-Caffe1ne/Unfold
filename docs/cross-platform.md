# Unfold desktop — Windows and macOS

Unfold is a C#/.NET 10 and Avalonia stretch reminder with desktop pets. Published
packages include .NET. Swift, Xcode, and the Piskel web runtime are not required.

As of 2026-10-08, the latest public release is **Beta v1.1.2**
(`1.1.2-beta`, tag `v1.1.2-beta`, source `ce86c7b`).
It includes Mochi (cat), 보리 (rabbit), 강아지 (dog), 고슴도치 (hedgehog)
and 펭귄 (penguin). Choose them from Home without installing separate packs.
When `docs/releases/v<version>.md` exists, packaging includes it as `RELEASE-NOTES.md` in the Windows portable/installed
`current` folder and inside `Unfold.app/Contents/Resources/` on macOS.
This beta requires Google sign-in and a verified Live purchase or valid free grant before
starting the timer, desktop pet, or settings. After sign-in, testers can use the bottom-right
code button to redeem `admin`; the server saves a free grant for that account.
After the first sign-in in this build, the refresh credential is kept in the OS credential
store. Restart restores the session and verifies server access without another Google
sign-in or code entry. Logout deletes the stored credential. Network failure preserves it
for retry but does not grant offline access.
Windows code signing is pending. Public Mac distribution uses Developer ID signing and
Apple notarization. See the [current public release notes](releases/v1.1.2-beta.md)
and [release/update verification](validation/2026-10-08-beta-v1.1.2-release.md).
Beta v1.0.3 and later managed installations can check for updates from the tray/menu bar;
Beta v1.1.1 and later also provide **설정 → 앱 정보 → 업데이트 확인**.
Beta v1.1.2 shows a modal with release notes once per launch when a newer version is
available, after account restoration. Background launches defer the notice until the
account or Settings window opens. Download and restart require the user's action.
v1.0.2 and earlier require a one-time manual installation of a newer release.
`Unfold --version` prints the release identity without opening the app or creating a user profile.

Custom GLB 2.0 pets can be imported through **펫 관리 → 파일 열기…**, with event-specific
animation mappings and a fixed body direction. Rendering follows the displayed size and
screen scale, up to 1024 pixels. See [GLB pets](glb-pets.md) for supported formats and limits.

## Windows installation

Run `Unfold-v<version>-win-x64-setup.exe` and follow the installation wizard.
It installs for the current user into `%LOCALAPPDATA%\DokhuStudio.Unfold.Updates`, creates a
Start menu shortcut and registers Windows app removal. The stable root `Unfold.exe` launcher
starts the versioned payload in `current/` and survives updates.
Quit the running app before installing or removing it. The updater closes and restarts its managed
app when applying an update. An already enabled login launch is migrated to the stable launcher
on the new app's first run; it is never enabled for a user who has opted out.
Uninstall removes the managed app files and its startup entry; settings, break history,
custom pets and saved login credentials remain in their separate user locations.

The ZIP remains available as a portable alternative: extract the entire
`Unfold-v<version>-win-x64.zip` and run the root `Unfold.exe`. Keep the extracted folder intact.
Both forms currently lack a Windows code signature.

Closing Settings hides it to the tray. Run the app again or use the tray menu to
open Settings. **Unfold 종료** exits the process. The app uses one instance per data
directory.

Launch at login is optional. Move the published app to its final folder before
enabling it. It registers the executable under the current user's Windows `Run`
registry key. If the app is moved later, turn this option off and on again from
the new location. A checked setting does not prove an actual login launch worked.

## macOS installation

Open the matching `Unfold-v<version>-osx-arm64.dmg` or `osx-x64.dmg`, drag `Unfold.app`
to the included `Applications` link, then open the copied app and eject the disk image.
Quit the existing app before replacing it. ZIP and tar.gz archives remain build outputs.
Settings closes to the menu
bar; **Unfold 종료** exits. Login launch uses
`~/Library/LaunchAgents/app.unfold.desktop.plist` and starts the app with
`--background`. Saved login is restored and access is verified before starting paid features.

The default local bundle is signed ad hoc. Public distribution uses Developer ID
signing and notarization. Set `UNFOLD_CODESIGN_IDENTITY` to select the certificate
and `UNFOLD_NOTARY_PROFILE` to select a local notarytool keychain profile. The bundle
script signs all nested native files, submits the app and staples its ticket, then
creates, notarizes and staples the DMG. Rejection stops packaging. Credentials are
never stored in the repository. `UNFOLD_NOTARY_LOG_DIR` selects the diagnostic log folder.
Use the `-notarized-installer.zip` Mac assets for the signed Beta v1.1.2 distribution;
the earlier Mac installer ZIPs remain available as historical ad-hoc builds.

## Visible controls

- Tray/menu bar: countdown and current state, 설정, 펫 숨기기/펫 표시,
  시작/일시정지/계속, 타이머 정지, Unfold 종료.
- Pet right-click menu: 설정, 펫 숨기기.
- Settings: the timer home contains stretch interval and break duration; a separate settings tab contains idle time, snooze time, stretch/completion sounds, bubble opacity and five dialogue strings; character selection, pet visibility,
  launch at login, **Review & export**, **펫 관리**, today's confirmed breaks, and timer controls.
- Pet speech reminder: **n분 뒤에**, **휴식 시작**, and **완료**. The bubble remains visible while its reminder state is active.
  An unanswered invitation automatically snoozes after 30 seconds using the configured snooze time.
  Bubble position follows the pet and screen work area automatically. Settings offers
  0–100% bubble opacity, five dialogue strings of 1–120 characters, 1–60 minute snooze,
  and due/completion WAV, MP3 or OGG effects.
  There is no separate reminder window or OS toast.
  **완료** is available from the start of a break. Overtime caps at +60:00 without auto-completion.
  Snooze counts active time, not time away.

Settings uses two icon buttons: Pause/Play and Stop. Each has a tooltip and an
accessibility name. Pause keeps the remaining time; Stop shows the configured full interval. Use Play to resume
or to start a full interval after Stop. Stop dismisses an open invitation without recording
completion. Reset and Stretch now are no longer exposed.

The timer state badge and tray status explicitly show running, paused, stopped, idle-paused,
or break-held state. The home card's **스트레칭 알림 간격 (분)** is disabled while running and accepts whole
minutes from 1 to 240 in one-minute steps while paused or stopped. **휴식 시간 (분)** accepts 1–10
minutes and may be changed while working because it applies to the next break. A typed or stepped value
does not change behavior until the home card's **저장** is pressed. The Settings tab uses its own
automatic saving for idle/snooze time and notification preferences, including sound import/reset.
There are no Settings-page Save/Cancel buttons or navigation-save dialog.
Saving a new stretch interval keeps Pause or Stop intact.

All five bundled pets provide the original reactions plus pointer hold/release behaviors. Eligible idle time triggers sleep, looking around
or yawning; a short click plays its assigned reaction without moving or scaling the pet canvas.
The third consecutive snooze triggers sulking. Starting a break plays one stretch before walking
inside the current monitor's work area; completion, stop, hiding or selecting another pet ends movement.
A hidden pet can appear temporarily for a new reminder without changing the saved visibility setting.

An open reminder holds the work timer and retains the break duration captured when it opened. Completion schedules a full work interval;
snooze schedules the configured 1–60 active minutes (default five); skip/close keeps the remaining work interval.
Existing manual Pause remains in effect. An already-open reminder is reused.
Routine and work-profile setup is no longer exposed in the normal UI. Review/export and
legacy data compatibility are described in the [compatibility guide](personalization.md).

## Platform limits

- Transparent-pixel click-through is implemented for Windows and macOS. The current
  displayed frame determines the clickable pet pixels; held drags and reminder
  controls retain input. Native macOS hit-test evidence and the physical-input /
  Windows verification boundary are recorded in the [input review](validation/2026-10-05-pet-click-through.md).
- Reminders use the pet speech bubble, not an OS notification or a separate reminder window.
  Interruption during focused work still needs actual user testing.
- Windows/macOS login launch, multi-monitor dragging, display changes, suspend,
  full-screen behavior, and installation trust need tests on the target OS.
- Update availability requires the updater metadata and helper from a published managed package.
  Running directly from source is not an installed update target.

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

Existing valid user character packages remain selectable. The pixel editor has been removed;
old source-file contracts remain under `src/Unfold.Core/Compatibility/`.
Legacy Swift preferences and sandbox data are
neither migrated nor deleted automatically.

**펫 관리 → 파일 열기…** opens a local `.unfoldpet` file in a confirmation window before installation.
Preview controls use the current theme background and offer 100–200% display size, Pause/Resume,
and Replay. Display size affects this preview only. Completed reactions return to
resting while keeping the selection available for replay.
The **저장** action installs a new ID, updates a newer content version, reinstalls matching contents,
or replaces an existing pack with any valid older or same-version archive. Content versions do not
restrict application compatibility. Reinstall restores damaged/missing runtime images from a saved pack.
Built-in companions and existing user-authored IDs cannot be replaced. Invalid archives,
hash/decoder failures and changed installed files are rejected. Successful installation selects
the companion without resuming a paused timer. There is no pet store or automatic pet download.
See the [pet pack guide](pet-packs.md).

The sidebar **펫 관리** opens the builder directly, without sub-tabs.
Its file button accepts existing packs, GLB, GIF/MP4 and still PNG/JPG/JPEG/WEBP/BMP images. Drafts survive sidebar navigation and hiding the settings window.
Assign files to supported actions (idle required), preview each, then save a `.unfoldpet`
and install it through the confirmation window. MP4 conversion is local, silent, limited to
10 seconds/128 MiB, and resized proportionally to at most 192px at 12 fps. GIF import preserves
source pixels/timing. Opaque video backgrounds remain visible; there is no background removal.
Still images retain aspect ratio and transparency, apply photo EXIF orientation, and shrink to at
most 512px without enlarging small images. Inputs are bounded to 32 MiB, 8192px per side,
and 16,777,216 pixels. They use single-frame PNG sheet clips in the existing version-1 pack format.
MP3 and OGG (Vorbis/Opus) effects are converted locally to mono/44.1 kHz/16-bit PCM WAV for the existing preview,
volume, automatic-save and playback paths. Both source files and stored effects must be at most
5 MiB; effects over 30 seconds are rejected rather than shortened. Source files are unchanged.

## Development and packaging

Run these commands from the repository with .NET SDK 10 installed:

```sh
dotnet restore Unfold.slnx --locked-mode
# MP4 / MP3 / OGG import: use the matching RID (win-x64, win-arm64, osx-arm64, osx-x64).
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

For the current updater-enabled Windows x64 installer, restore the pinned Velopack
tool and package the published x64 payload:

```powershell
dotnet tool restore
python Scripts/package-updates.py --runtime win-x64
```

This creates `artifacts/Unfold-v<version>-win-x64-setup.exe`, a managed portable ZIP,
and the update package/feed under `artifacts/update-releases/win-x64/`. It installs for the
current user without requesting administrator privileges. The existing ZIP remains
available as a portable alternative. The installer intentionally retains the user's
settings, pet packs and saved credentials when uninstalling.
The earlier NSIS helper `Scripts/package-windows-installer.py` is a legacy packaging
path; it does not create the current managed update feed.

Without PowerShell, prepare the matching media tools, publish Windows into
`artifacts/win-x64/app`, then run `python3 Scripts/package-windows.py win-x64`.
This copies the launcher and instructions from the canonical PowerShell script;
cross-publishing does not verify execution on Windows.

On macOS:

```sh
bash Scripts/make-macos-bundle.sh osx-arm64
# Intel:
bash Scripts/make-macos-bundle.sh osx-x64
```

The macOS script creates a `.app`, ZIP, tar.gz and a compressed DMG for the selected
architecture. The DMG contains Unfold and a shortcut to Applications. A Developer ID
certificate and notarization are still required for a Gatekeeper-trusted public release;
the default local build uses ad-hoc signing.

The CI matrix builds/tests Windows x64 and macOS arm64, then packages them. The Windows
job also defines an isolated packaged smoke run and a silent installer verification
(`Scripts/verify-windows-installer.ps1`) covering fresh installation, payload hashes,
installed version, login opt-out, reinstall, uninstall and preservation of a sentinel file.
It does not prove running-app protection, migration from a legacy startup entry,
an older-version update, or preservation of actual settings/history/pets/login credentials.
Those require separate target-OS scenarios. Beta v1.1.1 additionally has a
recorded public 1.1.0 → 1.1.1 update/apply/restart check in Windows CI; see the
[v1.1.1 release report](validation/2026-10-07-beta-v1.1.1-release.md).
Beta v1.1.2 has a recorded public 1.1.1 → 1.1.2 apply/restart check on Mac ARM64
and Windows x64 CI, with isolated data markers preserved; see the
[v1.1.2 release report](validation/2026-10-08-beta-v1.1.2-release.md).
Run this verifier only
in a disposable environment; it refuses an existing installation or startup entry.
This is automated diagnostic coverage,
not physical OS interaction or evidence that the current workflow has already passed.
Other supported script arguments are not proof of tested architectures.

Both publishing scripts prepare the pinned LGPL FFmpeg 8.1.2 build in `.tools/media-lgpl/<rid>/`.
The setup tool verifies fixed SHA-256 checksums before extraction and retains upstream licenses,
source provenance, and Windows support DLLs. The first preparation needs network access; cached
archives are reverified on later runs. Normal builds copy prepared files into `Tools/`. The app
does not download software at runtime. `UNFOLD_FFMPEG_PATH` can point to an absolute FFmpeg path
for development; the bundled executable and then `PATH` are the fallbacks. GIF and still-image import need no
FFmpeg. Windows Arm uses the x64 FFmpeg process via Windows x64 emulation; actual Windows/Arm
execution still needs target-OS testing.

## Legacy compatibility diagnostics

The pixel-editor UI has been removed. The legacy pixel model and Piskel v2 codec
under `src/Unfold.Core/Compatibility/` are exercised by regression tests and the
explicit `--smoke-test` diagnostic. The source model supports 1–128 px per side, 1–24 frames, and 1–16 layers;
existing PNG sprite sheets and GIF character clips are decoded for playback.

From a repository checkout:

```sh
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

Without `UNFOLD_DATA_DIR`, this creates a new temporary profile. With an override,
use a new empty directory, never a real user library. The diagnostic opens off-screen
windows, suppresses system notifications, creates and edits test artwork, checks a
save/reopen round trip, restores saved routine/profile fixtures, exercises reminder start/confirm/snooze/close,
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

## App updates

Use **업데이트 확인** in the tray/menu bar. A new release is downloaded inside Unfold;
**재시작하여 적용** applies it after the user chooses to restart. Active breaks and unsaved
pet drafts must be resolved before applying. Closing the update window leaves the
download running. Startup does not auto-apply a previously downloaded update.

The app reads the public `C-Caffe1ne/Unfold` GitHub Releases. Each release must include
the matching `releases.<runtime>-beta.json` (or `-stable.json`) and its referenced `.nupkg`.
Windows x64/Arm64 and Mac arm64/x64 have distinct feeds. No GitHub token is included in the app.
After publishing Windows, `python Scripts/package-updates.py --runtime win-x64` prepares
these assets without uploading them. On Mac, `bash Scripts/make-macos-bundle.sh <rid>`
creates a fresh bundle, update feed, ZIP and DMG; do not repack an already managed `.app`.
The Velopack SDK and local `vpk` tool are pinned to 1.2.0.

Existing Beta v1.0.2 and older apps have no updater. They must install this updater-enabled
build once after fully quitting the previous process. On Windows the new install and uninstall
registration are separate from the old NSIS installation; verify the new app before removing
the old one. User data remains at `%LOCALAPPDATA%\Unfold` /
`~/Library/Application Support/Unfold`; credentials keep the existing OS vault and bundle identity.

Public Mac update packages must be signed and notarized. Windows signing is recommended.
Local unsigned/ad-hoc build verification does not establish Windows execution or public trust.
Mac packaging preserves the existing resource/symlink layout and minimal app entitlement.
Developer ID builds pre-sign native libraries and let Velopack sign its helper and completed
bundle. Without an identity, the local validation bundle uses ad-hoc signing without hardened
runtime; its complete `.nupkg` and feed hashes are resealed together. These local packages
are not substitutes for signed and notarized public releases.
