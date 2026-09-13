# Release checklist

Manual checks for a packaged Unfold build, on top of `dotnet test Unfold.slnx -c
Release --no-restore` and the `--smoke-test` diagnostic (see
[cross-platform.md](cross-platform.md#verification)). Each item names the code
path it exercises and what to look for. Use an isolated `UNFOLD_DATA_DIR` for
every check below — never a real user's profile.

## Fresh profile

1. Point `UNFOLD_DATA_DIR` at a new, empty directory and launch the packaged exe.
2. Expect: default settings (60 min interval, 5 min idle, `default-cat`, pet
   shown), tray icon appears, Settings opens with the built-in character
   selected and previewing.

## Invalid settings

1. With the app closed, write a `settings.json` with an out-of-range value, e.g.
   `{"IntervalMinutes":9999,"IdleMinutes":5,"SelectedCharacterId":"default-cat","ShowPet":true}`,
   into `UNFOLD_DATA_DIR`.
2. Launch the app.
3. Expect startup with defaults and a logged error. When copying is possible,
   preserve the original contents in `settings.json.invalid-<timestamp>`.
   Failure to create the backup must not prevent recovery.
4. Repeat with malformed JSON, a JSON `null` root, and an unsafe character id.
   Record results against the exact packaged build being released. The baseline
   startup failure is documented in [the dated report](agent-reports/04-release.md).

## Timer idle / sleep

1. Set a short interval and idle threshold, then leave the machine idle past the
   threshold; confirm the tray countdown stops advancing (idle time excluded).
2. Suspend/resume (sleep) the machine mid-countdown; confirm the elapsed time
   does not jump by the sleep duration when it resumes.
3. Use **Stretch now** from the tray, Settings, and the pet's right-click menu;
   confirm each opens the same reminder and the pet stretches once per open
   reminder (re-invoking while one is already open does not restart it).

## Pet show / hide and monitors

1. Toggle **Show desktop pet** in Settings; confirm the tray item flips between
   **Hide Pet** / **Show Pet** and the pet window appears/disappears immediately.
2. Toggle from the tray item instead; confirm the Settings checkbox reflects the
   change (single setting, two entry points, persisted to `settings.json`).
3. Drag the pet to a second monitor (if available) and near screen edges;
   restart the app and confirm the saved position is honored and clamped inside
   a visible monitor if the previous monitor is now unavailable.

## Launch at login

1. Enable **Launch at login** against a published (non-dev) build.
2. Windows: confirm a `Run` registry value pointing at the published exe.
   macOS: confirm a `LaunchAgents` plist is installed.
3. Move the app to a new path with the option already on; per
   [cross-platform.md](cross-platform.md#windows), toggle the option off and
   back on and confirm the login entry now points at the new location.
4. Disable the option and verify the registration is removed. Restore the prior
   login configuration after testing; `UNFOLD_DATA_DIR` does not isolate OS login
   registration.

## Notifications

1. With OS notifications enabled, trigger a reminder and confirm an OS-level
   notification appears (Windows tray balloon / macOS `osascript` banner) in
   addition to the in-app reminder window.
2. With OS notifications disabled/blocked at the OS level, trigger a reminder
   and confirm the in-app reminder window still appears. Only detectable API or
   process failures are expected to be logged; OS suppression may be silent.

## OS matrix

| Target | CI coverage | Manual coverage needed |
| --- | --- | --- |
| Windows x64 | `windows-latest` in `.github/workflows/desktop.yml` (test + publish) | This checklist, end to end |
| Windows ARM64 | Not built in CI | Full checklist on real ARM64 hardware/VM before shipping that artifact |
| macOS Apple Silicon (arm64) | `macos-latest` in CI (test + bundle) | This checklist on real hardware — CI only proves it compiles and unit-tests, not windowing/login/notifications |
| macOS Intel (x64) | Not built in CI | Full checklist on real Intel hardware before shipping that artifact |

Do not record a macOS row as passed from evidence gathered on Windows, and do
not record Windows ARM64 or macOS Intel as covered by the current CI matrix —
both require a manual or separately-configured run.
