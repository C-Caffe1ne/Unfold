# Unfold MVP scope

## Product

A small desktop companion that reminds you to stretch while you work.

Mochi와 잠깐 쉬고, 내 리듬으로 돌아오는 작은 데스크톱 동료.

This document defines the current **Cat MVP with break sessions and a personalization demo** on `release/mvp`.
The application is the C#/.NET 10 and Avalonia solution. The 2026-09-13 product
development request extends the reminder into a guided pause with local completion
records. [Product direction](product-direction.md) and [development plan](development-plan.md)
describe the paid-value hypotheses. No payment flow or store is implemented.

## Core loop and implemented behavior

```text
Active computer use → break invitation → start a chosen routine
→ visible Mochi stretches once → timed steps → user confirms completion
→ local record + next work interval
```

Whether the user notices, physically stretches, and keeps using the app must be
observed. Those outcomes are not established by a passing build or animation test.

| Area | Current behavior |
|---|---|
| Timer | 5–240 minutes; valid whole-minute interval changes immediately save and update the countdown. Settings has labelled, keyboard-accessible icon buttons for Pause/Resume, Stop, and Reset. Idle threshold and routine selection still use Apply reminder settings. |
| Timer controls | Pause keeps the remaining time. Stop clears the current countdown to 00:00. Reset restores the configured interval and pauses. Play resumes a paused countdown or starts a full interval after Stop. Stop/Reset dismiss an open or pending invitation without adding a completion. |
| Activity | Idle threshold of 1–60 minutes. Idle time and large dispatcher/sleep gaps do not accrue work time. |
| Reminder | A centered invitation with **Start**, **In 5 minutes**, and **Skip this break**. Starting runs the selected timed routine; **I'm refreshed** becomes available when it reaches the end. OS notification delivery is attempted independently. |
| Routines | Small reset (60 seconds), Look away (20 seconds), and Room to move (90 seconds). The routine and companion are captured when the invitation opens. They are gentle prompts, not measured exercise or medical advice. |
| Routine library | Edit and save up to 20 custom routines with 1–3 prompts, 1–300 seconds per prompt and a maximum of 600 seconds total. The original personal slot is preserved alongside 19 additional routines. Saving selects the routine for future invitations; an already-open session keeps its original steps. Built-ins cannot be edited or removed. Profile references must be changed before deleting a routine. |
| Work profiles | Save up to 10 named combinations of routine, reminder interval, and away threshold. Apply manually. A different profile or interval loads a new work interval while preserving Pause; a stopped countdown remains at 00:00 until Play or Reset. An open session retains its original routine/profile context. No automatic schedule or meeting detection is included. |
| Manual reminder | Stretch now has been removed from Settings, the tray menu, and the pet menu. Normal invitations come from the automatic timer. The shared reminder method remains available to internal diagnostics. |
| Scheduling | Work time is held while an invitation/session is open. Confirmed completion starts a full work interval; snooze schedules five active minutes. Both preserve manual Pause. Skip/close keeps the remaining interval and records no completion. |
| Duplicate reminder | An already-open reminder is activated; it keeps the same session and does not restart the pet reaction. Concurrent opening requests are coalesced. |
| Completion history | Only explicit confirmation after the countdown adds a local record. Settings shows today's count and planned routine time. Sleep/stalled UI gaps do not complete a session. History is bounded to 2,000 entries; adding a record removes entries older than 90 days. |
| Review/export | Seven-day daily counts and planned time, previous-period navigation, and CSV export of the displayed period. New records retain their original routine/profile names. Old records still load; dates follow the completion's recorded local day. |
| Desktop pet | Frameless, topmost, draggable, position-persistent, and optionally hidden. Windows has OS-level transparent-pixel click-through; macOS does not. |
| Mochi animation | Eight idle frames plus `stretch.gif`. A plain click has no reaction because Mochi has no `click` clip. Starting a break makes a visible pet stretch once, then return to idle. |
| Optional reactions | Packages may supply `attention` on invitation, `celebrate` on confirmed completion, and `click`. Missing event clips leave the pet unchanged. Current Mochi does not supply these three. |
| Hidden pet | Stays hidden and does not react to reminders; the reminder window still opens. |
| Character picker | Lists bundled Mochi and valid characters already in the user's local library. |
| Pet packs | Open a local .unfoldpet file, inspect animations/version, then install, update, or reinstall. Built-in and user-authored IDs are protected. Invalid packs and changed installed files are rejected before replacement. No purchase recovery or remote download. [Pack guide](pet-packs.md) |
| Launch at login | Opt-in Windows registry/macOS LaunchAgent integration. Test it from a published app in its final location. |

These are implementation descriptions, not blanket OS verification claims. See
[verification](verification.md) for commands, recorded results, and untested behavior.

## Scope boundaries

The MVP has no user-facing pixel editor, character creation/editing/deletion,
scheduled profile switching, meeting/full-screen detection, clinical exercise
library, accounts, cloud sync, AI chat, achievements, XP, shop, multiplayer, or
coding-agent integration. The routine library, manually applied work profiles and weekly review/export
are implemented as a second-stage demo. This build has no payment locks. The proposed Free/Plus
commercial boundary remains a hypothesis. [Personalization guide](personalization.md)

The C# editor, pixel model, and Piskel codec remain because regression tests and
`--smoke-test` exercise authoring/save/reopen behavior. They are not advertised as
MVP features. Settings does not construct hidden Edit/Delete buttons.

`CharacterLibrary` and the image codecs are runtime dependencies: the app uses
them to discover and play character packages, including existing user artwork.
Removing editor entry points must not remove those packages or their loading path.

`Assets/Characters/` is the live source of bundled assets. The C# project copies it
to `Assets/Characters/` under build and publish output. The retired Swift source,
tests, Xcode project, and Swift-only build workflow are recoverable from Git history;
see the [archive index](archive/README.md).

Resource production, provenance, clip contracts, and the read-only asset audit are
defined in [pet resources](pet-resources.md). Existing Mochi exports retain their
bytes; the audit flags its stretch transparency/canvas mismatch and missing optional
reactions. Runtime compatibility is not a premium-art quality approval.

## Change rules

- Follow the user's current task and keep changes within its scope.
- Prioritize release blockers and observed problems in the core loop.
- Add features when required by the approved scope or supported by user evidence.
- Do not expand the editor, add more characters, or change the technology stack as
  incidental MVP work.
- Distinguish code inspection, automated verification, OS observations, and product
  demand. Record unperformed checks as unverified.
- Archived plans and experiment-specific instructions do not authorize new work.

Document roles and conflict handling are defined in the [documentation index](README.md).
