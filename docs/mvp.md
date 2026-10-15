# Unfold MVP scope

## Product

A small desktop companion that reminds you to stretch while you work.

Mochi와 잠깐 쉬고, 내 리듬으로 돌아오는 작은 데스크톱 동료.

This document describes the `1.1.4-beta` development source `f9953a0` on
`codex/pet-overlay-hover`, checked on 2026-10-14.
The latest public release is **Beta v1.1.4** (`v1.1.4-beta`, source `4c64bfc`).
The later local commit adds rest countdown digit motion and is not included in that release.
See the [handoff](HANDOFF.md), [release notes](releases/v1.1.4-beta.md),
[release verification](validation/2026-10-14-beta-v1.1.4-release.md) and
[rest countdown checks](validation/2026-10-14-rest-timer-motion.md).
Recheck all worktrees and the latest deployment before implementation;
`release/mvp` is an older source.
The application uses C#/.NET 10 and Avalonia. [Product direction](product-direction.md)
and [development plan](development-plan.md) describe the paid-value hypotheses.
Account and purchase-access flows exist in the desktop source; their production
verification is separate from the local break and pet diagnostics.

## Core loop and implemented behavior

```text
Active computer use → break invitation → start the configured break
→ visible pet maintains the configured rest action → timed break → user confirms completion
→ local record + next work interval
```

Whether the user notices, physically stretches, and keeps using the app must be
observed. Those outcomes are not established by a passing build or animation test.

| Area | Current behavior |
|---|---|
| Language | Korean throughout the normal settings, break, review, pet-pack and tray flows. User names and saved history retain their original text. [Language scope](localization.md) |
| Settings layout | Sidebar order is Home, pet management, review, theme, then Settings and Exit. The home shows the timer and companion; notification sounds, idle time and snooze are in Settings. Pet management is one editor page; opening a file selects the media or GLB editor by extension, without open/create or format tabs. Default 1120×800, minimum 640×560; narrow layouts scroll and preserve editor drafts. [Dashboard guide](settings-ui.md) |
| Timer | The home time card groups the 1–240 minute work interval and 1–10 minute next break duration. Both are editable only while stopped with no active break; valid edits apply and save automatically. Starting before a queued save restores the saved values. Settings notification/idle/snooze preferences save separately and report validation/save errors. Intervals of five minutes or less do not emit an advance warning. |
| Timer controls | The state badge distinguishes running, paused, stopped, idle-paused, and break-held states. The labelled icon buttons are Play/Pause and Stop. Pause keeps the remaining time. Stop immediately cancels an open or pending invitation without adding a completion and displays the configured full interval with digit motion. Play starts that interval after Stop. |
| Exit | Sidebar and tray exit ask for confirmation, then retain the unsaved pet-draft guard. Cancel or closing the confirmation leaves the app running. |
| Activity | Idle threshold of 1–60 minutes. Idle time and large dispatcher/sleep gaps do not accrue work time. |
| Reminder | The pet delivers a five-active-minutes warning, due invitation, and explicit completion notice in an attached speech bubble with centered text. Due offers **n분 뒤에** and **휴식 시작**; a running bubble counts the configured break duration and has **완료** below it. Complete at any time after starting; overtime shows +mm:ss and caps at +60:00 without auto-completing. A duration change affects the next invitation, not an open session. The Settings tab automatically saves sounds, opacity, five contextual dialogue strings, and opt-in state previews; bubble placement follows the pet and screen work area; the pet context menu contains Settings and Hide Pet. The speech bubble is rendered in a separate overlay window; no OS toast. [Speech guide](stretch-notifications.md) |
| Routines | 잠깐의 여유, 눈 쉬어 주기, and 몸 풀어 주기 provide the prompt sequence. The configured 1–10 minute break duration scales that sequence for new sessions. The routine, duration and companion are captured when the invitation opens. They are gentle prompts, not measured exercise or medical advice. |
| Retired data migration | On startup, retired routine/profile settings are removed without backup while current timer and pet preferences are kept. Historical routine/profile labels remain in records and CSV. Valid installed pets retain playable assets; their obsolete `source.piskel` files are removed without backup. |
| Manual reminder | Stretch now has been removed from Settings, the tray menu, and the pet menu. Normal invitations come from the automatic timer. The shared reminder method remains available to internal diagnostics. |
| Scheduling | Work time is held while an invitation/session is active. Completion starts a full interval; snooze schedules 1–60 active minutes (default 5). Both preserve manual Pause. Hiding the pet does not end a session; Stop cancels without a completion. |
| Duplicate reminder | Keeps the active session and does not replay the due sound or pet reaction. Concurrent preparations are coalesced. |
| Completion history | Only pressing **완료** after starting adds a local record, including early completion and overtime. New records retain planned seconds and measured active session seconds; summaries use actual seconds when available. Sleep/stalled UI gaps do not accrue time. History is bounded to 2,000 entries; adding a record removes entries older than 90 days. |
| Review/export | Seven-day counts and recorded rest time, previous-period navigation, and CSV export. The CSV retains planned_seconds and appends actual_seconds (blank for older records). Routine/profile snapshots and local dates are preserved. |
| Desktop pet | Frameless, topmost, draggable, position-persistent, optionally hidden, and adjustable from 50–150% in 10% steps. Platform-specific transparent-region hit testing is implemented for macOS and Windows. Headless/fake native API checks do not establish actual Windows input behavior. |
| Original pet animation | Bundled Mochi, 보리, 강아지, 고슴도치 and 펭귄 retain their original behavior profile and clips. Idle and pointer press/release reactions remain. The third consecutive snooze can trigger sulking. The rest action repeats or holds its terminal pose until completion or Stop. Completion plays once in full, then returns to idle; dragging and menus hold automatic movement. Pointer hover does not interrupt the action. Automatic movement does not overwrite saved manual position. |
| Pet windows | Pet rendering/input, reminder bubble/tail, and the timer bar use separate transparent windows. The bar below the pet shows Korean current time and timer controls while hovering over the pet or bar, then fades out when leaving both. Click or pickup hides it immediately. The development-only rest countdown animates changed digits; public 1.1.4 retains its earlier text transition. |
| Optional reactions | Packages may provide invitation, completion, click, pointer and movement reactions. Legacy hover mappings remain readable but are not played. Missing optional clips leave the pet unchanged. Original behavior requires its profile and clip contract; media and GLB authoring use their own supported action lists. |
| Hidden pet | A hidden pet appears temporarily for a new speech reminder without changing ShowPet. Explicit Hide Pet saves ShowPet=false and suppresses the current reminder without ending its session. Advance/completion notices expire after five seconds. An invitation automatically snoozes once after 30 seconds from its first visible presentation; an active break remains until completion or cancellation. |
| Character picker | A compact 200px selector lists the five bundled pets and valid characters in the local library. When an old installed pack has a bundled ID, the bundled version appears once and the user's original files remain on disk. |
| Pet packs | **펫 관리 → 파일 열기…** validates `.unfoldpet`, then shows a save/update confirmation with name and version. The separate pack preview window has been removed. Editor drafts survive navigation. A pack content version lower than the installed version is allowed by the current compatibility policy; invalid data and changed installed files are checked before replacement. [Pack guide](pet-packs.md) |
| Custom pets | The same file-open action accepts GIF, MP4, PNG, JPG, WEBP and BMP. Assign media to idle, attention, stretch, celebrate, click, pointerDown and pointerUp; idle is required. Export a validated `.unfoldpet`, then confirm saving it to the library. GIF timing is preserved; MP4 up to 10 seconds/128 MiB becomes silent GIF at up to 192px and 12 fps. No background removal. |
| GLB pets | Open an embedded GLB model, map supported situations to animation/heading/speed/repeat settings, export or save/apply, and reopen installed GLB pets from the selector. The file-slot trash immediately clears the current model draft while preserving its name, original source file and installed/active pet. **펫 삭제** separately confirms deletion of an installed editable GLB pet and updates runtime selection. [GLB guide](glb-pets.md) |
| Launch at login | Opt-in Windows registry/macOS LaunchAgent integration. Test it from a published app in its final location. |

These are implementation descriptions, not blanket OS verification claims. See
[verification](verification.md) for commands, recorded results, and untested behavior.

## Scope boundaries

The MVP has no user-facing routine/profile setup, pixel editor, drawing tools, or general pixel editing of installed characters,
scheduled profile switching, meeting/full-screen detection, clinical exercise
library, cloud sync, AI chat, achievements, XP, in-app shop, multiplayer, or
coding-agent integration. Weekly review/export remains available; historical routine/profile labels remain available in stored records and CSV. [Compatibility and review guide](personalization.md)

The commercial plan is free download, Google sign-in, then one-time payment of US$2.99,
with **no free trial**. Supabase manages accounts and purchase entitlements.
The desktop source connects account UI, Google browser sign-in, secure session storage,
checkout requests and entitlement checks. Normal startup enables the purchase gate;
internal diagnostic mode bypasses it explicitly. Saved OS credentials restore the session
and access before showing an account window; logout deletes those credentials.
The presence of these paths is not
proof of a completed real OAuth/payment transaction or offline purchase recovery.
Those production checks remain separate release gates. Completion history, settings and pet files remain local. See the
[paid launch plan](plans/2026-09-27-paid-launch.md) for staged acceptance criteria.

The C# pixel editor, drawing canvas and editor session have been removed.
The legacy pixel model, Piskel codec and source editing operations have been removed.
The timer page retains its pet preview. The separate pack import preview window has been removed;
media and GLB editing previews remain available,
including GLB edit/delete management.

`CharacterLibrary` and the image codecs are runtime dependencies: the app uses
them to discover and play character packages, including existing user artwork.
Removing editor entry points must not remove those packages or their loading path.

`Assets/Characters/` is the live source of bundled assets. The C# project copies it
to `Assets/Characters/` under build and publish output. The retired Swift source,
tests, Xcode project, and Swift-only build workflow are recoverable from Git history;
see the [archive index](archive/README.md).

Resource production, provenance, clip contracts, and the read-only asset audit are
defined in [pet resources](pet-resources.md). The [2026-09-22 original packs](../Art/Characters/original-companions-v2/README.md)
replace the live Mochi atlas and add bundled Bori, using transparent 256px cells.
The [dog, hedgehog and penguin packs](../Art/Characters/original-companions-v3/README.md)
add three more pets with the same ten-clip profile and 16 poses in 256px cells.
Generation prompts, source PNGs and hashes are retained. The legacy Mochi GIF and
[Bori 0.1.0 candidate](../Art/Characters/bori-rabbit/README.md) remain as historical inputs.
Runtime compatibility and automated captures are not final art or commercial-rights approval.

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
