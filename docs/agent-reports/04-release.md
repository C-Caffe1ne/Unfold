# Agent report 04 — release docs & checklist

Scope: `README.md`, `docs/cross-platform.md`, `docs/release-checklist.md`,
`.github/workflows/desktop.yml`, this report. Baseline: `release/mvp` @
`dc77ab9`, `dotnet test Unfold.slnx -c Release --no-restore` → 36/36 passed
(re-confirmed at the start of this run, same commit, clean tree).

## Findings

### 1. `docs/cross-platform.md` advertised hidden editor/library UI as shipped (confirmed, P2 — docs defect)

`docs/cross-platform.md` is copied verbatim as the packaged app's `README.md`
by `Scripts/publish-desktop.ps1` (line 22: `Copy-Item ... cross-platform.md ...
README.md`). It documented `create/select/edit/delete` under "Library" and a
full pixel-editor tool table as if they were reachable in the shipped app, and
told users to migrate old artwork via **Pixel Editor → Open → source.piskel**.

I confirmed in code that no UI path reaches the editor or library management
in this build:
- `src/Unfold.Desktop/AppRuntime.cs` `BuildTray()` (lines 190–209) builds
  exactly: Settings, Hide/Show Pet, Pause/Resume, Reset timer, Stretch now,
  Quit — no editor item. Matches `docs/mvp.md`'s documented tray menu.
- `src/Unfold.Desktop/SettingsWindow.cs` lines 52–55 construct `Edit`/`Delete`
  buttons but a comment there states, and the layout confirms, they are "left
  out of the layout below" — never added to the visible `body`.
- Only the character picker (`ComboBox`) is wired into the visible Settings
  layout, matching `docs/mvp.md`'s "Settings character picker" feature.

Fix: rewrote the Features table to list only what ships in the UI, added a
"Preserved, not shipped" paragraph naming the editor/library code as
internal-only (kept for `CharacterLibrary` playback reuse and the
`--smoke-test` regression path), and rewrote the migration bullet to say the
Piskel-format loader is preserved in code but has no in-app UI path in this
build. Kept the doc's existing, already-accurate macOS evidence-limit language
("A Windows build is not evidence of tested macOS windowing, login, or
notification behavior") and did not weaken it.

Iterated once on coordinator feedback: removed a `docs/mvp.md` relative link
and a `docs/release-checklist.md` link's implication of packaged availability
(only `cross-platform.md` ships inside the zip), trimmed the "Preserved, not
shipped" section from a table + implementation detail down to one paragraph,
and moved dated smoke-run numbers out of the evergreen doc into this report
and the checklist.

### 2. `README.md` — already accurate (no change needed)

`README.md`'s "Features" and "Not in this release" sections already describe
only shipped UI and correctly note the editor/library are preserved-but-hidden
code. No edit made.

### 3. Reproduced: out-of-range `settings.json` crashes startup instead of falling back to defaults (confirmed bug, **not my file ownership**)

While drafting the "invalid settings" release-checklist item I reproduced
this against the baseline build (`release/mvp` @ `dc77ab9`, packaged
`win-x64`):

- `src/Unfold.Core/AppSettings.cs` `Load()` throws `InvalidDataException` for
  a structurally-valid but out-of-range `settings.json` (e.g.
  `IntervalMinutes: 9999`).
- `src/Unfold.Desktop/AppRuntime.cs` constructor (line ~44) only catches
  `ex is IOException or JsonException` around `AppSettings.Load`.
  `System.IO.InvalidDataException` derives from `Exception`, not
  `IOException`, so it is not caught.
- Evidence: ran the packaged `win-x64` exe with
  `UNFOLD_DATA_DIR` pointed at a directory containing
  `{"IntervalMinutes":9999,"IdleMinutes":5,"SelectedCharacterId":"default-cat","ShowPet":true}`
  via `--smoke-test`. Process exited 1; `unfold.log` recorded an unhandled
  `System.IO.InvalidDataException: Invalid settings values.` thrown from
  `AppSettings.Load` through the `AppRuntime` constructor — no `smoke.json`
  was written, i.e. the crash happens before any recovery path runs.

This file pair (`AppSettings.cs`, `AppRuntime.cs`) is outside my ownership and
is under concurrent revision by another worker on this shared branch
(`git status` shows both modified, plus a new
`Tests/Unfold.Tests/SettingsReliabilityTests.cs`), consistent with
`docs/improvement-plan.md`'s Claude-1 assignment ("설정 로드/저장 검증과 시작
시 오류 복구 경로 확인"). I did not touch either file. I documented the
reproduction in `docs/release-checklist.md`'s "Invalid settings" item as a
known issue pinned to the baseline commit, with instructions to re-run once
the other worker's fix lands, rather than asserting it is fixed or leaving it
undocumented.

### 4. Added a Windows packaged smoke-test step to CI (justified, verified locally)

`.github/workflows/desktop.yml` published the Windows build but never ran
`--smoke-test` against the packaged executable — only `dotnet test` at the
source level. Since `SmokeDiagnostics.cs` already exercises startup, character
save/reopen, the (hidden) editor, and the reminder path end-to-end against the
actual published binary, and I confirmed it runs cleanly and quickly, I added
a `Run packaged Windows smoke test` step after publish that: sets an isolated
`UNFOLD_DATA_DIR` under `RUNNER_TEMP`, runs `Unfold.exe --smoke-test` via
`Start-Process -PassThru` + `Wait-Process -Timeout 120` (direct/backgrounded
invocation does not block a PowerShell script host for a GUI-subsystem exe —
confirmed empirically), fails the step if the process doesn't exit in time,
if `smoke.json` isn't produced, or if `success` is false/exit code is
nonzero, and uploads the verification PNGs/JSON as a build artifact for
debugging. Windows-only, matching the task's constraint against speculative
macOS scripting from this environment.

## Priority

- P2 (docs correctness, user-facing): cross-platform.md fix — done.
- P1 (real defect, other owner): invalid-settings startup crash — reproduced
  and documented, not fixed by me; flagged for the owning worker/coordinator.
- P3 (CI hardening): packaged Windows smoke step — done, additive, no
  existing step removed or weakened.

## Changed files

- `docs/cross-platform.md` — corrected shipped-vs-preserved claims, evidence
  limits, migration bullet; trimmed per coordinator feedback.
- `docs/release-checklist.md` (new) — fresh profile, invalid settings (with
  the reproduced-bug note), timer idle/sleep, pet show/hide + monitors,
  launch at login, notifications, OS matrix (win-x64/osx-arm64 in CI;
  win-arm64/osx-x64 need manual coverage).
- `.github/workflows/desktop.yml` — added Windows packaged smoke-test step
  and its verification-artifact upload.
- `README.md` — reviewed, no change needed.

## Test evidence

- `dotnet test Unfold.slnx -c Release --no-restore` on the clean baseline:
  36/36 passed (re-confirmed before edits).
- Packaged `win-x64` build (`./Scripts/publish-desktop.ps1 -Runtime win-x64`),
  then `Unfold.exe --smoke-test` with an isolated `UNFOLD_DATA_DIR`: exit code
  0, `smoke.json` `{"success": true, "startupMs": ~690–750,
  "totalMs": ~4.4s, "characters": 2, "imageFiles": 4}` (two separate runs,
  consistent).
- Reproduced invalid-settings crash (see Finding 3): exit code 1, no
  `smoke.json`, `unfold.log` shows the uncaught `InvalidDataException`.
- Simulated the new CI PowerShell step locally against the same packaged
  build: exit code 0, correctly parses and validates `smoke.json`, prints the
  JSON payload; also verified the `-PassThru`/`Wait-Process` failure path
  compiles and the corrected `try { Wait-Process ... } catch { ... }` timeout
  form works (Windows PowerShell 5.1 `Wait-Process` has no `-PassThru`
  parameter, unlike an earlier draft of this step — fixed before landing it).

## Unresolved issues / deferred manual checks

- Out-of-range `settings.json` startup crash (Finding 3) — not fixed here;
  owned by another worker's file scope. Release-checklist item is marked as a
  known reproduction against the baseline commit, not a pass.
- No macOS hardware was available in this environment. Every macOS-specific
  checklist item (notifications, login item, click-through gap, actual
  windowing) remains a deferred manual check, as already flagged in
  `docs/cross-platform.md`'s existing evidence-limit language.
- Windows ARM64 and macOS Intel are not built by the current CI matrix
  (`.github/workflows/desktop.yml` only has `windows-latest`/`win-x64` and
  `macos-latest`/`osx-arm64`); flagged in the OS matrix section of
  `docs/release-checklist.md` rather than silently assumed covered.
- The new CI step has not been run inside actual GitHub Actions (no push/PR
  triggered from this session); it is verified by local simulation of the
  same commands against the same packaged binary and by reading Windows
  Actions runner conventions (interactive desktop session, so GUI-subsystem
  windowing APIs are expected to behave as they do locally), not by a live
  Actions run.

## Next recommended work

1. Whoever owns `AppSettings.cs`/`AppRuntime.cs`: confirm the out-of-range
   settings crash (Finding 3) is covered by the in-flight fix and its new
   regression test, then flip the checklist item to Pass.
2. Once merged, trigger the workflow once (push or `workflow_dispatch`) to
   confirm the new Windows smoke step behaves the same in hosted Actions as
   locally, and check the uploaded `Unfold-win-x64-smoke` artifact.
3. When macOS hardware/CI access is available, work through the macOS-specific
   rows of `docs/release-checklist.md` and update its OS matrix with real
   results instead of "not built in CI".
