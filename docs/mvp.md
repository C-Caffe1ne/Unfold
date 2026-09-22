# Unfold MVP scope

## Product

A small desktop companion that reminds you to stretch while you work.

Mochi와 잠깐 쉬고, 내 리듬으로 돌아오는 작은 데스크톱 동료.

This document defines the current **Cat MVP with break sessions and local review** on `release/mvp`.
The application is the C#/.NET 10 and Avalonia solution. The 2026-09-13 product
development request extends the reminder into a guided pause with local completion
records. [Product direction](product-direction.md) and [development plan](development-plan.md)
describe the paid-value hypotheses. No payment flow or store is implemented.

## Core loop and implemented behavior

```text
Active computer use → break invitation → start a chosen routine
→ visible original pet stretches once, then walks → timed break → user confirms completion
→ local record + next work interval
```

Whether the user notices, physically stretches, and keeps using the app must be
observed. Those outcomes are not established by a passing build or animation test.

| Area | Current behavior |
|---|---|
| Language | Korean throughout the normal settings, break, review, pet-pack and tray flows. User names and saved history retain their original text. [Language scope](localization.md) |
| Settings layout | Sidebar order is timer, pet addition, review, settings, with no decorative top icon. The timer home places stretch interval and break duration above the review card; the Settings page keeps notification sounds, idle time and snooze. The pet page switches between open/create tabs. Default size 1120×800, minimum 640×560; the home stacks vertically below 860px wide, with scrolling and unsaved input preserved. [Dashboard guide](settings-ui.md) |
| Timer | The home time card groups stretch time (the 5–240 minute work interval) and the next break duration (1–10 minutes). Stretch time is locked while running and can be edited only while manually paused or stopped; break duration can be changed while running because it applies to the next session. Its **저장** button is transparent and disabled when unchanged, appears for edits, and saves valid changes. The Settings tab saves idle/snooze values with its own **저장** button. A five-minute work interval does not emit an advance warning. |
| Timer controls | The state badge explicitly distinguishes running, paused, stopped, idle-paused, and break-held states. The two labelled, keyboard-accessible icon buttons are Play/Pause and Stop. Pause keeps the remaining time; Stop asks for confirmation, then displays the configured full interval; Play resumes or starts that interval after Stop. Approved Stop dismisses an open or pending invitation without adding a completion. |
| Exit | Sidebar and tray exit ask for confirmation, then retain existing unsaved pet-draft/editor guards. Cancel or closing the confirmation leaves the app running. |
| Activity | Idle threshold of 1–60 minutes. Idle time and large dispatcher/sleep gaps do not accrue work time. |
| Reminder | The pet delivers a five-active-minutes warning, due invitation, and explicit completion notice in an attached speech bubble with centered text. Due offers **n분 뒤에** and **휴식 시작**; a running bubble counts the configured break duration and has **완료** below it. Complete at any time after starting; overtime shows +mm:ss and caps at +60:00 without auto-completing. A duration change affects the next invitation, not an open session. The Settings tab configures sounds, bubble position, and opt-in state previews; the pet context menu contains Settings and Hide Pet. No separate reminder window or OS toast. [Speech guide](stretch-notifications.md) |
| Routines | 잠깐의 여유, 눈 쉬어 주기, and 몸 풀어 주기 provide the prompt sequence. The configured 1–10 minute break duration scales that sequence for new sessions. The routine, duration and companion are captured when the invitation opens. They are gentle prompts, not measured exercise or medical advice. |
| Routine/profile compatibility | Routine-library and work-profile setup are no longer exposed in the normal UI. Existing `settings.json` values remain readable so an upgrade does not delete user data, and existing history keeps its captured routine/profile labels. Internal diagnostic components remain for compatibility testing. |
| Manual reminder | Stretch now has been removed from Settings, the tray menu, and the pet menu. Normal invitations come from the automatic timer. The shared reminder method remains available to internal diagnostics. |
| Scheduling | Work time is held while an invitation/session is active. Completion starts a full interval; snooze schedules 1–60 active minutes (default 5). Both preserve manual Pause. Hiding the pet does not end a session; Stop cancels without a completion. |
| Duplicate reminder | Keeps the active session and does not replay the due sound or pet reaction. Concurrent preparations are coalesced. |
| Completion history | Only pressing **완료** after starting adds a local record, including early completion and overtime. New records retain planned seconds and measured active session seconds; summaries use actual seconds when available. Sleep/stalled UI gaps do not accrue time. History is bounded to 2,000 entries; adding a record removes entries older than 90 days. |
| Review/export | Seven-day counts and recorded rest time, previous-period navigation, and CSV export. The CSV retains planned_seconds and appends actual_seconds (blank for older records). Routine/profile snapshots and local dates are preserved. |
| Desktop pet | Frameless, topmost, draggable, position-persistent, optionally hidden, and adjustable from 50–150% in 10% steps. Windows has OS-level transparent-pixel click-through; macOS does not. |
| Original pet animation | Bundled Mochi (cat), 보리 (rabbit), 강아지 (dog), 고슴도치 (hedgehog) and 펭귄 (penguin) have ten clips, with random sleep/look/yawn after 20–40 eligible idle seconds. Press squashes, release bounces and plays surprise → looking around. The third consecutive snooze triggers sulking. During a break, one complete stretch precedes slow movement inside the current monitor work area. Pointer hover, dragging and context menus pause movement; completion, stop, hide and character change end it. Automatic movement does not overwrite saved manual position. |
| Optional reactions | Packages may supply `attention` on invitation, `celebrate` on confirmed completion, and `click`. Missing event clips leave the pet unchanged. Expanded behaviors require the `unfold-original-v1` profile and all ten clips; the custom GIF/MP4 builder retains its five slots and existing playback. |
| Hidden pet | A hidden pet appears temporarily for a new speech reminder without changing ShowPet. Explicit Hide Pet saves ShowPet=false and hides the current reminder without ending its session; routine refresh does not reveal it. Focus Reminder or the next new notification can reveal it again. Advance and completion notices expire after five seconds; invitation and active-break controls remain available. |
| Character picker | A compact 200px selector lists the five bundled pets and valid characters in the local library. When an old installed pack has a bundled ID, the bundled version appears once and the user's original files remain on disk. |
| Pet packs | The sidebar **펫 추가** page has **펫 팩 열기** and **펫 팩 만들기** tabs, preserving drafts while navigating. Open a local .unfoldpet file, inspect animations/version on the current theme background, 100–200% preview size, Pause/Resume and Replay, then save to install, update, or reinstall. Action selection sits below the preview beside size; there is no background selector. Built-in and user-authored IDs are protected. Invalid packs and changed installed files are rejected before replacement. No purchase recovery or remote download. [Pack guide](pet-packs.md) |
| Custom pets | **펫 추가 → 펫 팩 만들기 → 파일 가져오기** assigns GIF/MP4 snapshots to idle, attention, stretch, celebrate and click. Idle is required. Save a validated .unfoldpet, then preview/install through the existing flow. GIF timing is preserved; MP4 up to 10 seconds/128 MiB becomes silent GIF at up to 192px and 12 fps. No background removal. |
| Launch at login | Opt-in Windows registry/macOS LaunchAgent integration. Test it from a published app in its final location. |

These are implementation descriptions, not blanket OS verification claims. See
[verification](verification.md) for commands, recorded results, and untested behavior.

## Scope boundaries

The MVP has no user-facing routine/profile setup, pixel editor, drawing tools or installed-character editing/deletion,
scheduled profile switching, meeting/full-screen detection, clinical exercise
library, cloud sync, AI chat, achievements, XP, in-app shop, multiplayer, or
coding-agent integration. Weekly review/export remains available; routine-library and work-profile data
are retained only for backward compatibility and internal diagnostics. [Compatibility and review guide](personalization.md)

Accounts, sign-in, the three-day trial and Lemon Squeezy payment are **planned but not implemented**.
The current build makes no network calls at all: there is no account, no trial clock, no entitlement
check and no payment lock, and every feature runs locally without signing in. Under that plan,
completion history, settings and pet files still stay on the device. The single-product commercial
boundary is recorded in [product direction](product-direction.md) and must not be described here as
shipped behavior.

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
