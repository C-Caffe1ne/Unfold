# Worker 2 — Animation/PetWindow lifecycle

Scope: `src/Unfold.Desktop/PetWindow.cs`, `src/Unfold.Desktop/AnimationView.cs`,
new `Tests/Unfold.Tests/AnimationLifecycleTests.cs`. Branch `release/mvp`.

## Findings (confirmed by reading + reproduced by test)

1. **`AnimationView.SetFrames` restarted the timer regardless of a prior
   `SetRunning(false)`.** `SetFrames` unconditionally did
   `if (HasTopLevel() && frames.Count > 1) timer.Start();`. `PetWindow.HidePet()`
   pauses via `animation.SetRunning(false)`, but any in-flight `React()` or
   `SetCharacter()` call that resolves its `await runtime.Clip(...)` afterwards
   still calls `SetFrames(...)`, which silently resumed the timer on a hidden
   view. Confirmed with `PausedViewIgnoresClipSwapAndStaysStopped`.
2. **A pending `React()` could keep animating/rewriting frames after the pet
   was hidden or closed**, because neither `HidePet()` nor `ClosePet()` gave
   `SetFrames`/`SetRunning` any way to know playback should stay stopped.
   Same root cause as (1) for the hidden case.
3. **`ClosePet()` did not bump `generation`.** `SetCharacter()`/`React()` guard
   themselves against stale results with the `generation` counter, but
   `ClosePet()` never incremented it, so a `Clip(...)` load already in flight
   when the pet closes could still land and call into the now-disposed
   `AnimationView`, resurrecting bitmaps/timers on a view whose `Closed`
   handler already called `animation.Dispose()`.

Priority: **High** — wasted timers/CPU on hidden pets and a use-after-dispose
style resurrection on close are real lifecycle bugs, not hypothetical; both
were directly reachable from existing call sites (`AppRuntime.UpdatePet`,
`AppRuntime.ShowReminder`, `AppRuntime.Dispose`).

## Fixes

- `AnimationView.cs`: added a `running` flag (mirrors `SetRunning`) that
  `SetFrames` now honors — a clip swap while paused updates content but no
  longer starts the timer or the elapsed stopwatch. Added a `disposed` flag
  set by `Dispose()`; `SetFrames` and `SetRunning` are no-ops once disposed,
  so this is the last line of defense against a stale async continuation
  regardless of caller-side races.
- `PetWindow.cs`: `ClosePet()` now increments `generation` before stopping
  `hitTimer` and calling `Close()`, so in-flight `SetCharacter()`/`React()`
  awaits see a stale generation and return without touching the (about to be)
  disposed `AnimationView`.
- Click-vs-stretch policy, transparent hit-testing (`OpaqueAt`,
  `UpdateClickThrough`), and drag/click pointer handling were not touched.

## Tests

New `Tests/Unfold.Tests/AnimationLifecycleTests.cs`, 3 tests, all headless
Avalonia (`AvaloniaFact`), all passing:

- `PausedViewIgnoresClipSwapAndStaysStopped` — pauses via `SetRunning(false)`,
  then calls `SetFrames` again (simulating a reaction's clip load finishing
  after hide) and asserts the timer stays disabled.
- `DisposedViewIgnoresStaleCallbacksAndStaysEmpty` — disposes the view, then
  calls `SetFrames`/`SetRunning(true)` again (simulating a reaction resolving
  after `ClosePet()`) and asserts the timer stays disabled and `OpaqueAt`
  stays false (no bitmaps resurrected).
- `VisibleRunningAnimationAdvancesAndCompletes` — guards against
  over-suppressing the fix: a normal, visible, running clip still advances
  and fires `Completed`.

`ClosePet()`'s `generation` bump itself is not exercised end-to-end by a test:
doing so needs a real `AppRuntime` (tray icon, `Selected`/`Clip` backed by the
character library), which is heavyweight, OS-integration-dependent, and — per
the coordinator's "avoid global test environment mutation" note and Worker 1
owning `AppRuntime` — out of scope here. The `AnimationView`-level disposed
guard is what actually makes that race harmless, and is what the tests above
verify directly.

No test asserts real native click-through or physical monitor behavior;
`UpdateClickThrough` is Windows-only P/Invoke and untouched by this change.

### Test evidence

```
dotnet build src/Unfold.Desktop/Unfold.Desktop.csproj -c Release --no-restore
  -> Build succeeded, 0 warnings, 0 errors

dotnet test Unfold.slnx -c Release --no-restore
  -> 통과! - 실패: 0, 통과: 50, 건너뜀: 0, 전체: 50 (baseline 36 + tests added
     by other workers + my 3 new tests, all green, at the time this run was
     taken)
```

A later `dotnet build`/`dotnet test` of the full solution failed on an
unrelated, concurrently-edited file — `Tests/Unfold.Tests/SettingsUiTests.cs`
(a `FakeDesktopLifetime : IClassicDesktopStyleApplicationLifetime` that
doesn't implement the interface). That file is not owned by this task, was
mid-edit by another worker, and is outside `PetWindow.cs`/`AnimationView.cs`
scope — left untouched per instructions. `src/Unfold.Desktop` itself still
builds clean in isolation (see above). Coordinator owns the final combined
run.

## Unresolved / next recommended work

- The `generation`-bump fix in `ClosePet()` is verified by code inspection and
  by the `AnimationView` disposed-guard tests, not by an end-to-end
  `AppRuntime`-driven regression test. If a future pass wants that coverage,
  it would need a lightweight seam (e.g. an interface for the pieces of
  `AppRuntime` that `PetWindow` actually uses) rather than constructing the
  full `AppRuntime` (tray icon, single-instance pipe, settings file I/O) in
  tests — that's a larger, separate change and not attempted here to keep
  this fix small and reviewable.
- No behavior change was made to `HidePet()` (no `generation` bump there):
  the existing `React()` tail (`character = null; await SetCharacter();`)
  relies on running to completion even while hidden so the character resets
  to `idle` correctly before the pet is shown again; bumping `generation` on
  `HidePet()` would skip that reset and could leave a stale reaction frame
  showing when the pet is re-shown. Confirmed by tracing `AppRuntime.UpdatePet`
  → `SetCharacter()` → `ShowPet()`.
