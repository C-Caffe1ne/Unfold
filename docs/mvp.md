# Unfold MVP scope

## Product

A small desktop companion that reminds you to stretch while you work.

Mochi와 잠깐 쉬고, 내 리듬으로 돌아오는 작은 데스크톱 동료.

This document describes the current **Unfold Beta 1.1.0** development source:
`codex/glb-import-compat`, `c44ef12` plus the current uncommitted GLB compatibility
and Windows input changes, checked on 2026-10-06. Recheck the worktree and version
before implementation; `release/mvp` is an older source.
The application uses C#/.NET 10 and Avalonia. [Product direction](product-direction.md)
and [development plan](development-plan.md) describe the paid-value hypotheses.
Account and purchase-access flows exist in the desktop source; their production
verification is separate from the local break and pet diagnostics.

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
| Settings layout | Sidebar order is timer, pet management, review, settings. The home shows the timer and companion; notification sounds, idle time and snooze are in Settings. Pet management is one editor page; opening a file selects the media or GLB editor by extension, without open/create or format tabs. Default 1120×800, minimum 640×560; narrow layouts scroll and preserve editor drafts. [Dashboard guide](settings-ui.md) |
| Timer | The home time card groups the 1–240 minute work interval and 1–10 minute next break duration. Work interval edits require Pause or Stop; break duration changes apply to the next session. The home **저장** action persists these values. Settings notification/idle/snooze preferences save automatically and report validation/save errors. Intervals of five minutes or less do not emit an advance warning. |
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
| Desktop pet | Frameless, topmost, draggable, position-persistent, optionally hidden, and adjustable from 50–150% in 10% steps. Platform-specific transparent-region hit testing is implemented for macOS and Windows. Headless/fake native API checks do not establish actual Windows input behavior. |
| Original pet animation | Bundled Mochi, 보리, 강아지, 고슴도치 and 펭귄 retain their original behavior profile and clips. Idle reactions and pointer press/release clips remain; the removed canvas squish, lift and bounce effects are not current requirements. The third consecutive snooze can trigger sulking. A break stretch precedes movement within the current work area; hover, dragging and menus hold movement. Automatic movement does not overwrite saved manual position. |
| Optional reactions | Packages may provide invitation, completion, click, hover, pointer and movement reactions. Missing optional clips leave the pet unchanged. Original behavior requires its profile and clip contract; media and GLB authoring use their own supported action lists. |
| Hidden pet | A hidden pet appears temporarily for a new speech reminder without changing ShowPet. Explicit Hide Pet saves ShowPet=false and suppresses the current reminder without ending its session. Advance/completion notices expire after five seconds. An invitation automatically snoozes once after 30 seconds from its first visible presentation; an active break remains until completion or cancellation. |
| Character picker | A compact 200px selector lists the five bundled pets and valid characters in the local library. When an old installed pack has a bundled ID, the bundled version appears once and the user's original files remain on disk. |
| Pet packs | **펫 관리 → 파일 열기…** opens `.unfoldpet` in a separate preview/install dialog. Preview animations/version, then install or reapply through that dialog. Editor drafts survive navigation. A pack content version lower than the installed version is allowed by the current compatibility policy; invalid data and changed installed files are checked before replacement. [Pack guide](pet-packs.md) |
| Custom pets | The same file-open action accepts GIF, MP4, PNG, JPG, WEBP and BMP. Assign media to idle, attention, stretch, celebrate, click, hover, pointerDown and pointerUp; idle is required. Export a validated `.unfoldpet`, then preview/install. GIF timing is preserved; MP4 up to 10 seconds/128 MiB becomes silent GIF at up to 192px and 12 fps. No background removal. |
| GLB pets | Open an embedded GLB model, map supported situations to animation/heading/speed/repeat settings, export or save/apply, and reopen installed GLB pets from the selector. The file-slot trash immediately clears the current model draft while preserving its name, original source file and installed/active pet. **펫 삭제** separately confirms deletion of an installed editable GLB pet and updates runtime selection. [GLB guide](glb-pets.md) |
| Launch at login | Opt-in Windows registry/macOS LaunchAgent integration. Test it from a published app in its final location. |

These are implementation descriptions, not blanket OS verification claims. See
[verification](verification.md) for commands, recorded results, and untested behavior.

## Scope boundaries

The MVP has no user-facing routine/profile setup, pixel editor, drawing tools, or general pixel editing of installed characters,
scheduled profile switching, meeting/full-screen detection, clinical exercise
library, cloud sync, AI chat, achievements, XP, in-app shop, multiplayer, or
coding-agent integration. Weekly review/export remains available; routine-library and work-profile data
are retained only for backward compatibility and internal diagnostics. [Compatibility and review guide](personalization.md)

The commercial plan is free download, Google sign-in, then one-time payment: KRW 4,900 in Korea
or US$3.99 overseas, with **no free trial**. Supabase manages accounts and purchase entitlements.
The desktop source connects account UI, Google browser sign-in, secure session storage,
checkout requests and entitlement checks. Normal startup enables the purchase gate;
internal diagnostic mode bypasses it explicitly. The presence of these paths is not
proof of a completed real OAuth/payment transaction or offline purchase recovery.
Those production checks remain separate release gates. Completion history, settings and pet files remain local. See the
[paid launch plan](plans/2026-09-27-paid-launch.md) for staged acceptance criteria.

The C# editor, pixel model, and Piskel codec remain because regression tests and
`--smoke-test` exercise authoring/save/reopen behavior. They are not advertised as
MVP features. The normal UI does not expose that general pixel editor; GLB management has its own explicit edit/delete flow.

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
