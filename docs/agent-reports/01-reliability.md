# Worker 1 — Settings & startup reliability

Scope: `src/Unfold.Core/AppSettings.cs`, `src/Unfold.Desktop/AppRuntime.cs`.

## Findings

### 1. Confirmed (High) — invalid/out-of-range settings crashed startup unhandled

`AppSettings.Load` throws `InvalidDataException` for a missing/null-deserialized
document, out-of-range `IntervalMinutes`/`IdleMinutes`, or an unsafe
`SelectedCharacterId` (including `null`). `AppRuntime`'s constructor only recovered
from `ex is IOException or JsonException`.

Verified the actual inheritance (not assumed) with a throwaway .NET 10 console
snippet:

```
System.IO.InvalidDataException
  -> System.SystemException -> System.Exception
Is IOException?   False
Is JsonException? False
```

So a hand-edited or corrupted-but-well-formed `settings.json` (e.g.
`{"IntervalMinutes":999}` or `{"SelectedCharacterId":null}`) produced an
`InvalidDataException` that the constructor's `when` filter did not match. Since
`new AppRuntime(desktop)` is called directly in `App.OnFrameworkInitializationCompleted`
with no surrounding try/catch, this exception propagated out of app startup
unhandled — the one case (out-of-range/invalid *values*, as opposed to malformed
JSON) that recovery was supposedly built for was exactly the case that bypassed it.

Also confirmed the same gap for `UnauthorizedAccessException` (e.g. a permission-denied
settings file): it is a `SystemException`, not an `IOException`, so it also escaped
the original filter.

**Fix:** broadened the constructor's recovery filter to
`IOException or UnauthorizedAccessException or JsonException or InvalidDataException`,
and wrapped the invalid-file backup copy in its own try/catch (a locked file or full
disk during the backup no longer defeats the recovery it's supposed to be inside of).

### 2. Confirmed (Medium) — `Save` did not enforce the same invariants as `Load`

`AppSettings.Save` serialized `this` unconditionally. Because `AppSettings` is a
public record with `init` setters, any caller (or a future one) could construct an
out-of-range instance directly (bypassing `Load`) and persist it via the public
`Save(path)` API — the type only validated on the way in, not on the way out.

**Fix:** extracted the existing range/id checks into a private `Validate` used by
both `Load` and `Save`. `Save` now throws `InvalidDataException` before writing
anything, so invalid settings can never reach disk through the public API. Existing
callers (`AppRuntime.UpdateSettings`, `SavePosition`, and every `SettingsWindow`
call site) already wrap `Save` in `catch (Exception ...)` or the narrower set below,
so this doesn't introduce a new unhandled path in practice.

### 3. Confirmed (Low/consistency) — `SavePosition`'s catch was narrower than the constructor's

`SavePosition` only caught `IOException`, while the constructor's recovery caught
`IOException or JsonException` (now also `UnauthorizedAccessException` and
`InvalidDataException`, per #1/#2). Since `SavePosition` calls `Settings.Save`,
which can now throw `InvalidDataException` (defense-in-depth; in practice `Settings`
is always already-valid at runtime) or `UnauthorizedAccessException`, its narrower
catch was an inconsistency waiting to surface as an unhandled exception from a
pointer-release handler in `PetWindow`.

**Fix:** aligned `SavePosition`'s catch to
`IOException or UnauthorizedAccessException or InvalidDataException`.

### 4. Confirmed (Medium) — `UpdatePet` race flagged by coordinator

Raised by the coordinator: `UpdatePet` awaited `pet.SetCharacter()` and then called
`pet.ShowPet()` unconditionally. If a hide (`Settings.ShowPet` flips false via a
concurrent `UpdateSettings`) or a quit (`Dispose()` sets `pet = null` after
`ClosePet()`) completed while that await was in flight, the stale continuation could
either resurrect a pet the user just hid, or call `ShowPet()` through a null field
read (`NullReferenceException`) after quit.

**Fix:** captured the `PetWindow` instance being awaited, and re-checked
`pet == current && Settings.ShowPet` after the await before calling `ShowPet()` —
same "re-check current state after await, before touching the window" pattern
already used elsewhere in this codebase (see `AnimationLifecycleTests.cs`).

## Changed files

- `src/Unfold.Core/AppSettings.cs` — shared `Validate` used by `Load` and `Save`.
- `src/Unfold.Desktop/AppRuntime.cs` — broadened recovery catch + safe backup copy;
  consistent `SavePosition` catch; `UpdatePet` post-await state re-check.
- `Tests/Unfold.Tests/SettingsReliabilityTests.cs` (new) — regression tests below.

## Test evidence

New tests in `Tests/Unfold.Tests/SettingsReliabilityTests.cs` (11 tests):

- Pure `AppSettings` tests (no Avalonia): out-of-range interval/idle minutes and null
  character id throw `InvalidDataException` from `Load`; that exception is verified
  at runtime to be neither `IOException` nor `JsonException` (regression guard for
  finding #1); `Save` rejects invalid settings and does not create/overwrite the file
  (finding #2); valid boundary values (5/240, 1/60, null pet position) round-trip.
- `[AvaloniaFact]` tests that construct a real `AppRuntime` against an isolated
  `UNFOLD_DATA_DIR` temp directory with a real
  `Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime`:
  malformed JSON, out-of-range values, and a null character id each recover to
  default settings without throwing out of the constructor, and the malformed-JSON
  case leaves a `settings.json.invalid-*` backup; a valid settings file loads
  unchanged with no backup created.

Verified the fix actually matters, not just the tests, in an isolated `git worktree`
at `release/mvp` (clean of other workers' in-progress files, see below) with only my
four changed/added files copied in:

- With the original `AppRuntime` catch (`IOException or JsonException` only)
  restored: `dotnet test` → **2 failed** —
  `OutOfRangeSettingsValuesDoNotPreventStartup` and
  `NullSelectedCharacterIdDoesNotPreventStartup` both threw
  `System.IO.InvalidDataException: Invalid settings values.` straight out of
  `AppRuntime..ctor`, exactly as diagnosed. (`MalformedJsonSettingsDoNotPreventStartupAndAreBackedUp`
  still passed, since malformed JSON was already `JsonException`, covered before.)
- With the fix restored: full suite **47/47 passed** in that isolated worktree
  (baseline 36 + my 11 tests).

Also ran the full shared-checkout suite earlier in this task, before another
worker's new file landed: `dotnet test Unfold.slnx -c Release --no-restore --no-build`
→ **50/50 passed** (baseline 36 + tests added by other workers so far, including
these).

## Unresolved / blocked

While finishing verification, `Tests/Unfold.Tests/SettingsUiTests.cs` (new, owned by
another worker — not in my scope) was added to the shared checkout and does not
compile: it defines `FakeDesktopLifetime : IClassicDesktopStyleApplicationLifetime`,
but Avalonia marks that interface not implementable by user code (`CS0535`). This
blocks a final `dotnet test` run of the *whole* solution from this workspace state.
I did not touch that file (outside my ownership); I reported the issue and a working
alternative to the coordinator (construct the concrete
`ClassicDesktopStyleApplicationLifetime` class directly — which is what my own new
`AvaloniaFact` tests do successfully) for relay to its owner. My own two owned
production files build cleanly in isolation
(`dotnet build src/Unfold.Core/Unfold.Core.csproj` and
`src/Unfold.Desktop/Unfold.Desktop.csproj`, 0 errors), and the last full-solution
test run before that file landed was 50/50 green.

## Next recommended work

- Once `SettingsUiTests.cs` compiles, re-run the full `dotnet test Unfold.slnx -c
  Release --no-restore` to confirm the combined suite (this task's 11 new tests +
  other workers' additions) is still all-green.
- Not done here (out of scope / avoided to keep the fix small): `PetX`/`PetY` have no
  range validation at all in `AppSettings`. They're advisory (window position,
  clamped on use elsewhere) so this was left alone, but worth a note if a future pass
  wants stricter validation.
