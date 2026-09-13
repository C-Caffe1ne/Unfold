# Worker 3 — Settings window UX and async state sync

Scope: `src/Unfold.Desktop/SettingsWindow.cs`, new
`Tests/Unfold.Tests/SettingsUiTests.cs`. Branch `release/mvp`.

This report went through one revision after coordinator review; the
"Findings/Fixes" sections below describe the final state. Where review
changed my mind about the right fix (not just the wording), that's called
out explicitly rather than silently rewritten.

## Findings (confirmed by reading + reproduced by test)

1. **Title advertised a hidden feature.** `Title = "Unfold · Stretch & Create"`,
   but character creation/editing is explicitly hidden in the MVP (`docs/mvp.md`
   "Not MVP" / Scope boundaries). Fixed to `"Unfold · Settings"`, matching how
   the tray menu and pet context menu already refer to this window.
2. **A failed launch-at-login toggle always forced the checkbox to `false`,**
   regardless of whether the user was enabling or disabling. `PlatformServices
   .SetStartAtLogin` can fail before touching the OS state at all (e.g. it
   explicitly refuses when `Environment.ProcessPath`'s file name is `dotnet`),
   so a failed *disable* attempt left the OS state unchanged (still enabled)
   while the checkbox lied and showed unchecked. Fixed by re-querying
   `PlatformServices.StartsAtLogin()` on failure. Review caught a second-order
   gap: that re-query call can itself throw, in which case the original fix
   left the checkbox showing the unconfirmed *attempted* value. Fixed by
   falling back to the pre-attempt state (`!attempted`) when the query itself
   fails, so the checkbox never displays an unconfirmed value either way.
3. **Failed `ShowPet`/character-selection saves left the checkbox/combobox
   showing the attempted (unsaved) value** until some unrelated `Changed`
   event happened to fire a `Refresh()`. Fixed by reverting the control to
   the last known-good value (`runtime.Settings.ShowPet` / `runtime.Selected`)
   in the `catch` block, under the same `updating` guard used elsewhere to
   suppress the control's own change handler while doing so.
4. **`Refresh()` held its reentrancy guard (`updating`) across the `await`
   that loads the character preview clip**, so any `Changed` event that
   arrived while a clip was in flight — including the once-a-second clock
   tick — was dropped outright, silently staling the countdown/status/
   checkbox/combobox sync for as long as the load took. Fixed by narrowing
   `updating` to only the synchronous control-sync block. Separately, that
   block's `catch` was shared with the clip-load `catch`, so a decode failure
   could stomp `state.Text` with a raw exception message, overwriting
   "Paused by you"/"Counting active time". Fixed by giving the async tail its
   own `catch` that only logs.

**Revised finding, corrected after review:** my first pass also changed
*when* `previewCharacter` gets latched — moving it from "immediately, before
the load" to "only after the load succeeds" — on the theory that this let a
failed preview retry later. Review pointed out this doesn't hold up:
`AppRuntime.Clip()` (not in this task's ownership) caches a failed decode
`Task` forever for a given character+key, so retrying the *same* selected
identity can never actually succeed until the library reloads — and a
reload always produces a new `CharacterPackage` instance anyway, which
un-latches naturally regardless of when the flag was set. Retrying on every
`Changed` event (e.g. every second, from the clock tick) with no chance of
success would just re-log the same stale error indefinitely. **Reverted this
part of the change** — `previewCharacter` is latched immediately again, as
in the original code — and kept only the two real fixes in (4) above.

Priority: **Medium-High**. (1) is a doc-accuracy fix. (2) and (3) are real
"state lies to the user after a failure" bugs. (4) is subtler and not
user-visible in the common case (clip decode normally takes microseconds),
but a genuine correctness bug in how `Refresh()` interacts with `Changed`
events that fire during a load.

## Fixes

All changes are in `SettingsWindow.cs`; see the findings above for what each
one addresses. No layout, feature, or public-API surface changes — the
`Preview`-property test hook from an earlier draft was removed after review
(no `InternalsVisibleTo` exists in this repo, so tests use
`window.GetVisualDescendants().OfType<AnimationView>().Single()` instead, the
same pattern `EditorWindow`'s own tests already use for `Canvas`).

Not touched: the hidden Edit/Delete buttons (still built-but-unlaid-out per
the existing comment), the reminder/idle `Apply` button (reads fresh
`NumericUpDown.Value`s each click rather than mirroring a persisted value, so
there's no stale "wrong displayed state" to revert on failure the way there
is for `showPet`/`characters`/`login`).

## Tests

`Tests/Unfold.Tests/SettingsUiTests.cs`, 5 tests, all headless Avalonia
(`AvaloniaFact`), all passing. Each test uses a `Fixture : IDisposable` that
points `UNFOLD_DATA_DIR` at a fresh `TempDirectory`, constructs a real
`AppRuntime` (via the real, concrete `ClassicDesktopStyleApplicationLifetime`
— `IClassicDesktopStyleApplicationLifetime` is marked non-implementable
outside Avalonia, so a hand-written fake doesn't compile) without ever
calling `Start()`/`Quit()` (no tray icon, single-instance lock, or message
loop touched), and on `Dispose()` disposes the runtime and lifetime and
restores the previous `UNFOLD_DATA_DIR` value so the mutation doesn't leak
to other tests.

- `TitleDoesNotAdvertiseTheHiddenCharacterCreator`
- `FailedShowPetSaveRevertsCheckboxToLastKnownState` — makes `settings.json`
  read-only (after an initial save creates it), toggles the checkbox,
  dismisses the resulting error dialog, asserts the checkbox and
  `runtime.Settings.ShowPet` are both still `true`.
- `FailedCharacterSaveRevertsSelectionToLastKnownState` — same technique for
  the character `ComboBox`.
- `LatestCharacterSelectionWinsThePreview` — switches between an opaque and a
  fully transparent character (checked via `AnimationView.OpaqueAt`, found
  through the visual tree) and asserts the preview always ends up matching
  the *last* selection.
- `PreviewRecoversAfterALoadFailureOnceTheCharacterIsReloaded` — builds a
  character with a hand-authored GIF-backed `idle` animation (a sprite-sheet
  character's `Sheet` is `Lazy`-cached forever once `Library.List()`'s own
  validation touches it during `Reload`, so corrupting the PNG afterwards
  can't reproduce a real decode failure; a GIF re-reads and re-decodes its
  file on every `LoadAnimation` call, so corrupting *that* file does).
  Corrupts the GIF, shows the window, asserts nothing rendered; repairs the
  GIF and calls `Reload()` (the real recovery path — a fresh
  `CharacterPackage` instance, with `AppRuntime`'s clip cache cleared as a
  side effect), asserts the preview now renders.

All 5 were also run against the pre-fix code (temporarily reverted in the
working tree, then restored — confirmed restored correctly via a fresh
diff/read before continuing) to confirm they actually catch the bugs they
target: 3 of 5 failed as expected (title, show-pet revert, character
revert). `LatestCharacterSelectionWinsThePreview` and
`PreviewRecoversAfterALoadFailureOnceTheCharacterIsReloaded` pass against
both old and new code — after the revision above, nothing in finding (4)
changes `previewCharacter`'s latch timing anymore (that's what those two
tests would have needed to distinguish), so they're regression/no-breakage
guards for the preview pipeline rather than pins for a specific bug. The
`updating`-scope and shared-`catch` parts of finding (4) are verified by code
review, not by a dedicated test — see "Unresolved" below for why.

### Test evidence

```
dotnet build Unfold.slnx -c Release --no-restore
  -> 빌드했습니다 (build succeeded), 0 warnings, 0 errors

dotnet test Unfold.slnx -c Release --no-restore --no-build
  -> 통과! - 실패: 0, 통과: 55, 건너뜀: 0, 전체: 55
     (baseline 36 + tests added by other workers + my 5 new tests, all green)
```

## Limits of simulating OS login (as requested)

`PlatformServices.SetStartAtLogin` (not in this task's ownership) has real
side effects on the OS — a Windows registry `Run` key, or a macOS
`LaunchAgents` plist — and its *only* guard against running under a test
host is checking that `Environment.ProcessPath`'s file name is literally
`dotnet`, which does not match this project's own test host. **This was not
hypothetical**: an earlier draft of a login-checkbox test actually toggled
the checkbox and, because that guard didn't trigger, wrote a real `Unfold`
entry into `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at
the test executable. Caught via `reg query`/`Get-ItemProperty`, reverted by
hand (`Remove-ItemProperty`), and re-verified clean (twice) before this
report — including after the final full test run above.

Given that, **no test in the final suite exercises the checkbox's failure
path at all**, on or off — per coordinator direction, the safer choice was
to drop that coverage rather than add an injection seam to `SettingsWindow`
or `PlatformServices` just to make it testable (both out of scope for a
small, reviewable fix). The login fix in finding (2) is verified by code
review only. Manual verification would need a published (non-`dotnet`-named)
build, toggling the checkbox with the registry key deliberately pre-set to
the opposite state, and confirming the checkbox lands on the real
post-attempt state rather than the attempted one — not attempted here.

## Unresolved / next recommended work

- `AppRuntime.Clip()` caching a *failed* decode forever (not just a
  successful one) is a separate, pre-existing behavior worth a look by
  whoever owns `AppRuntime.cs`: a transient read error for a character's
  animation currently can't recover without a full library reload. This
  report's `previewCharacter` fix works *with* that behavior (latching
  immediately, since retrying without a reload can't help anyway) rather
  than trying to work around it — flagging it here per coordinator's request
  to state clearly whether recovery requires reload. It does.
- The `updating`-scope fix and the shared-`catch` fix (both in finding 4) are
  not pinned by a dedicated regression test. Reliably forcing the exact
  interleaving needed (a `Changed` event landing *while* `runtime.Clip`'s
  await is still pending) without a controllable delay hook into
  `AppRuntime` would need either a timing-fragile test or a small seam in
  `AppRuntime` — coordinator asked to keep this fix bounded, so this was left
  as code-review-verified rather than adding that seam unprompted.
- Did not change the Edit/Delete buttons, layout, or add any new
  settings/features — out of scope per the task ("no redesign or new
  features").
