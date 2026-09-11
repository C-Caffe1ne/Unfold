# Unfold MVP

## Product

A tiny desktop companion that reminds you to move while you work.

한국어 설명:
일하다 몸을 잊었을 때, 먼저 기지개를 켜는 작은 데스크탑 동료.

The MVP ships the **C# / Avalonia** runtime (`src/Unfold.Core`, `src/Unfold.Desktop`)
on Windows and macOS. The Swift tree is legacy — see [Scope boundaries](#scope-boundaries).

## Core Loop

```
Work
→ stretch timer fires
→ pet reacts and stretches
→ user notices
→ user stretches
→ pet returns to idle
```

How the loop is wired today, so nobody documents an intention as a feature:

| Step | Where it happens |
|---|---|
| Timer fires | `StretchClock.Tick` → `AppRuntime.ShowReminder()`. **Stretch now** — in the tray menu, in Settings, and in the pet's right-click menu — calls the same method, so a manual stretch behaves exactly like an automatic one |
| Pet stretches | `ShowReminder()` opens the reminder window on the character's `stretch` clip and asks a visible pet for the same clip through `PetWindow.React("stretch")`; both animate at once |
| Pet reacts | Clicking the pet plays its `click` clip, falling back to `stretch` when the character has none (the built-in Mochi has none, so a click makes it stretch) |
| Returns to idle | `PetWindow.React()` plays the clip once, then restores the `idle` loop when it ends |

Two deliberate limits on the pet's reaction:

- A hidden pet stays silent. With **Show pet** off, the reminder still appears and the
  pet is not asked to animate.
- A reminder that is already open is only re-activated, so firing **Stretch now** again
  does not restart a stretch the pet is already playing.

## MVP Features

Every item below is implemented and verified in the C# runtime today.

| Feature | Notes |
|---|---|
| Desktop pet | Frameless, always-on-top, transparent window; draggable and position-persistent. Click-through over transparent pixels is Windows-only; macOS is pending. |
| Idle animation | The selected character's `idle` clip loops for as long as the pet is shown, apart from the one-shot reactions below. |
| Stretch reminder | Centered reminder window playing the `stretch` clip, dismissed with **I'm refreshed**. |
| Stretch animation | Built-in Mochi ships `stretch.gif`; used by the reminder window, by the pet's reaction to that reminder, and by the pet's click reaction. |
| Stretch timer | 5–240 min interval; countdown in the tray tooltip, tray menu and Settings. Pause / Resume, Reset, and **Stretch now**. |
| Idle-aware pause | 1–60 min idle threshold; idle time does not accrue toward the next stretch. Sleep and dispatcher gaps are not counted as active use. |
| Notifications | OS-level: Windows tray balloon (`Shell_NotifyIcon`), macOS `display notification` via `osascript`. Failure is logged and the in-app reminder still shows. |
| Show / Hide pet | Settings checkbox, persisted in `settings.json`. |
| Launch at login | Opt-in per OS: Windows `Run` registry key, macOS `LaunchAgents` plist. Requires a published build. |

Also user-visible in the MVP build, and intentionally kept: the tray menu
(Settings / Pause / Reset timer / Stretch now / Quit), the pet's right-click menu
(Settings / Stretch now), and the Settings character picker — which lists the built-in
character plus any characters a user created before the editor was hidden.

## Not MVP

- Pixel editor
- Character creator
- Character editing
- AI chat
- Cloud sync
- Accounts
- Achievements
- XP
- Store
- Multiplayer
- Coding-agent integrations
- General-purpose pixel-art tooling

### Checked and deliberately excluded

- **Snooze** — not implemented. The reminder window has one action, **I'm refreshed**,
  which closes it. There is no snooze button, setting, or delayed re-fire. Do not
  document it as existing.

## Product Rules

Before implementing any new feature, ask:

1. Is this required to ship the current MVP?
2. Has a real user requested it?

If both answers are No, do not implement it.

Further principles:

- Development is a validation tool, not validation itself.
- Prefer shipping and observing over expanding scope.
- Do not improve the Pixel Editor during the MVP cycle.
- Do not change the product's technology stack before first market validation unless
  a release-blocking technical problem requires it.

## Scope boundaries

For anyone — human or agent — working inside the MVP cycle:

- **The Pixel Editor code stays.** Its user-facing entry points are hidden (tray item
  and the Settings Create / Edit / Delete buttons), but `AppRuntime.OpenEditor`,
  `EditorWindow`, `PixelCanvas`, `EditorSession` and the Piskel codec are intact and
  still covered by tests and the `--smoke-test` run. Removing them breaks the build
  and the smoke test.
- **`CharacterLibrary` is not editor-only.** Character playback reads the library, so
  it must keep working regardless of what happens to the editor UI.
- **`Sources/Unfold/Resources/Characters/**` is a live build input**, not legacy. The
  C# project links those files in as `Assets/Characters` — the built-in Mochi comes
  from there. The rest of the Swift tree is preserved reference and is not built by
  the MVP.
- **The pre-MVP state is preserved** on the `archive/editor-heavy` branch. Nothing has
  to be deleted to keep the MVP small.

## Related documents

- [Cross-platform guide](cross-platform.md) — data locations, packaging, performance, platform limits
- [Legacy Swift README](legacy-swift.md) — the preserved macOS implementation
- [Native pixel editor](native-pixel-editor.md) — describes the **Swift** editor, not
  the C# one that ships (hidden) in this build
