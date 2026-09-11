# Unfold Windows Dogfooding Log

Windows-only verification of the Cat MVP. The macOS record lives in
[dogfooding-log.md](dogfooding-log.md) and is a separate document on purpose — a
Windows build is not evidence of tested macOS behavior, and the reverse is equally
true.

Fill this in **while** using the app, not afterwards from memory. One session per copy
of this file; if you run a second session, append a new `## Session` block rather than
overwriting the first.

**Why this session exists:** the Garden pivot reuses every Windows code path listed
below unchanged — transparent window, click-through, tray, idle detection, position
persistence, launch at login. Anything broken here stays broken after the pivot. This
session runs against the **cat** so that the plant is designed against measured
behavior rather than assumptions.

## Session

- Date:
- Start:
- End:
- Windows version / build:
- CPU:
- GPU:
- Monitor count:
- Monitor scale:
- Focus Assist / Do Not Disturb:
- Commit: `dc77ab9` (`release/mvp`)
- Build:
- Stretch interval:
- Pause when away:

Record the scale for **every** monitor, not just the primary one, and record the Focus
Assist state **before** section G rather than after — the notification result cannot be
interpreted without it.

The stretch interval accepts 5–240 minutes and the idle threshold 1–60 minutes. Their
minimums are the useful settings here: an automatic reminder costs at least five
minutes of wall clock, and idle pause needs at least a minute of untouched keyboard.

## Pre-test rule

During this session:

- Do not fix issues while testing.
- Do not implement new ideas.
- Record first, decide later.
- NICE ideas are not MVP work.
- Every observation goes to the Timeline, then gets classified BLOCKER / CORE UX / NICE.

**Garden work does not start until this session records zero BLOCKERs.**

## Test Scenarios

Tick what held true. Leave a box empty when it was not exercised — an empty box means
"not verified", never "probably fine". When something fails, do not debug it: write the
time in the Timeline and keep working.

### A. Basic Run

- [ ] published `Unfold.exe` launches
- [ ] no console window appears
- [ ] the pet appears
- [ ] the tray icon appears in the notification area
- [ ] the tray tooltip shows a counting-down `Unfold · MM:SS`
- [ ] Settings opens
- [ ] closing Settings with **X** hides it and the app keeps running
- [ ] **Quit Unfold** ends the process completely (confirm in Task Manager)
- [ ] both the pet and the tray icon disappear on quit
- [ ] `%LOCALAPPDATA%\Unfold\unfold.log` holds no fatal error

Notes:

### B. Transparent / Topmost

- [ ] the background around the pet is transparent
- [ ] no black or white rectangle behind the pet
- [ ] the pet stays above other windows
- [ ] correct over a browser
- [ ] correct over an editor
- [ ] correct over a terminal
- [ ] `Win+D` and back leaves the pet correct
- [ ] lock screen and back leaves the pet correct
- [ ] OPTIONAL — behavior over a fullscreen app:

Notes:

### C. Click-through

The Windows-only code path, and the one with no execution record at all. Put a text
editor underneath the pet so a stray click is visible as a caret.

- [ ] clicking a **transparent** area reaches the app underneath
- [ ] clicking the **drawn** area hits the pet
- [ ] the drawn area can be dragged
- [ ] click-through recovers after a drag ends
- [ ] crossing the transparent/drawn boundary — is the transition lag noticeable?
- [ ] right-click opens the pet menu (Settings / Stretch now)

Notes:

### D. DPI

- Current scale: ____%

- [ ] size is correct at the current scale
- [ ] the image is sharp at the current scale
- [ ] the clickable area matches the drawing at the current scale

Test other scales only if the machine allows it, and **restart Unfold after each
change** — the app declares no DPI awareness of its own and relies on the framework
default, so behavior across a live scale change is unverified. Write `NOT TESTED` for
any scale not exercised.

- 100%:
- 125%:
- 150%:

Notes:

### E. Multi-monitor

If there is only one monitor, write `NOT TESTED` here and skip the section.

- [ ] the pet can be dragged to another monitor
- [ ] Quit and restart restores it to the same monitor and position
- [ ] with **different scales** per monitor, the clickable area still matches the drawing
- [ ] disconnecting a monitor does not strand the pet off-screen

Notes:

### F. Idle Detection

Do not run this over Remote Desktop — the underlying idle query reports differently in
a remote session.

- [ ] the countdown advances while you work
- [ ] after **1+ minute** with no keyboard or mouse input at all
- [ ] the tray tooltip shows `· away`
- [ ] the countdown stops
- [ ] resuming input resumes the countdown immediately
- [ ] Settings shows `Paused while you're away`
- [ ] sleep / wake does not consume the countdown

Notes:

### G. Notifications

- Focus Assist / Do Not Disturb state at the start of this section:

- [ ] a Windows notification appears when the reminder fires
- [ ] the in-app reminder window appears
- [ ] both together — is that too much at once?
- [ ] behavior with Focus Assist **ON**:

A suppressed notification is the designed outcome, not a failure: the in-app reminder
is meant to stand on its own. Record which signal reached you first.

Notes:

### H. Stretch

- [ ] the automatic reminder fires
- [ ] the reminder window opens
- [ ] a visible pet plays `stretch` exactly once
- [ ] the pet returns to `idle`
- [ ] **I'm refreshed** closes the reminder
- [ ] tray **Stretch now**
- [ ] Settings **Stretch now**
- [ ] pet context menu **Stretch now**
- [ ] repeating **Stretch now** while the reminder is open does not restart the animation
- [ ] did the reminder steal focus from what you were doing?

All three manual entry points call the same code path as the timer. Record whether they
*feel* the same — placement, focus, timing, whether the pet reacted.

Notes:

### I. Hide / Show

- [ ] tray **Hide Pet** hides the pet
- [ ] the tray item then reads **Show Pet**
- [ ] tray **Show Pet** brings it back in the same place
- [ ] the Settings checkbox stays in step in both directions
- [ ] a reminder still works while the pet is hidden
- [ ] a hidden pet never reappears on its own
- [ ] the hidden state survives a restart

Notes:

### J. Launch at Login

Run this against the **published exe**. It is blocked by design while running through
`dotnet run`.

```powershell
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Unfold
```

- [ ] ON → the `Unfold` value is created under HKCU `Run`
- [ ] the value points at the actual exe path in use
- [ ] the value includes `--background`
- [ ] OFF → the value is removed (the command above now errors)
- [ ] after restarting Unfold, the Settings checkbox matches the registry state

The registry check alone proves the code path and takes under a minute. Actual
autostart needs a **sign out and sign back in**, not a full reboot.

- [ ] OPTIONAL — signed out and back in; Unfold started with tray and pet only, no
      Settings window (this is what `--background` should produce)

Recorded registry value:

Notes:

### K. Garden Asset Design Observation

The point of running this session against the cat. These answers become the input to
the plant's silhouette — measure, do not estimate.

Reference values, fixed in the current build:

| | |
|---|---|
| Pet window | 192 × 192 DIP (192 px at 100%, 288 px at 150%) |
| Source frame | 384 × 384, scaled to fit the window |
| Hit test | pixels with alpha ≥ 26 only |
| Hit tolerance | about 4 source px ≈ 2 DIP around any opaque pixel |
| Click vs. drag | under 5 px of movement **and** under 0.22 s |

- [ ] how much of the 192 × 192 area does the cat actually fill? (attach a screenshot)
- [ ] can you grab **thin parts** — tail, ear tip, whiskers — and drag from them?
- [ ] what does clicking a **semi-transparent edge** feel like — caught, or dropped?
- [ ] where do you *instinctively* aim when you want to drag it?
- [ ] Mochi has no `click` clip, so a plain click does nothing. Calm, or dead?
- [ ] does click-through over transparent pixels feel natural, or does the pet feel
      like it is not really there?
- [ ] edge quality against a **light** background:
- [ ] edge quality against a **dark** background:
- [ ] would a plant need an opaque pot to be draggable at all?

**Could you reliably grab thin parts of the character?**

> YES / NO —

If **NO**: the Garden prototype should use an **opaque pot as the primary hit area**.
A thin sprout stem drawn with soft edges would fall below the alpha ≥ 26 threshold and
become click-through, leaving the plant impossible to grab or move.

Notes:

### L. Long Run — OPTIONAL / Tier 2

Not required to clear the Garden gate. Run it in the background during normal work
rather than as a dedicated sitting.

- [ ] the app stays responsive for 60–90 minutes
- [ ] no growing CPU
- [ ] no fan noise
- Memory at start:
- Memory at end:
- [ ] the pet never vanishes
- [ ] the pet never duplicates
- [ ] the animation never freezes
- [ ] I was still willing to keep it running at the end

Notes:

## Timeline

Add a row whenever something happens or something bothers you. Leave this empty until
the session actually runs.

| Time | Observation | Category |
|---|---|---|
| | | |

## BLOCKER

Ship-stopping on Windows. **Any one of these blocks the start of Garden work until it
is fixed:**

- `Unfold.exe` fails to launch
- immediate crash
- the pet does not appear
- the tray icon does not appear
- the transparent background fails (a black or white rectangle behind the pet)
- click-through does not work at all
- the automatic reminder never fires
- noticeable constant CPU usage
- the pet vanishes, freezes, or duplicates
- the process survives **Quit Unfold**

## CORE UX

The app works, but the core experience is weakened. For example:

- the reminder steals focus at a bad moment
- the notification and the reminder window feel redundant
- the stretch reaction is too subtle to notice
- the pet is distracting during focused work
- the clickable area is hard to hit

## NICE

Ideas that will not be built now. For example:

- more characters
- themes
- a snooze action
- extra customization

## SAFE TO DEFER

Observe and record only. Do **not** act on these in this cycle:

- SmartScreen warnings
- code signing
- an installer
- DPI changes applied while the app is running
- fullscreen game behavior
- the tray icon ending up in the hidden overflow area
- notification and reminder feeling redundant (record as CORE UX, revisit at Garden)
- focus stealing (record as CORE UX, revisit at Garden)
- any macOS issue

On SmartScreen specifically: a locally built exe carries no Mark-of-the-Web, so it will
**not** trigger the warning a real downloader sees. Running it here is not evidence
about distribution. Reproducing that needs the zip delivered over the web to another
machine, and it is out of scope for this session.

## Session Summary

Write this after the session, not during it.

### What worked

### BLOCKER

### CORE UX

### NICE

### Most important observation

One sentence:

>

## Final Rule

After the session:

- Do not implement NICE items.
- Rank BLOCKER first. **Garden work starts only when the BLOCKER list is empty.**
- Then rank CORE UX by impact on the Core Loop.
- Carry section K's answer into the plant's silhouette design before any asset is drawn.
- Do not add features without observed evidence.
