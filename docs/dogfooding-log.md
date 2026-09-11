# Unfold Dogfooding Log

Fill this in **while** using the app, not afterwards from memory. One session per copy of
this file; if you run a second session, append a new `## Session` block rather than
overwriting the first.

## Session

- Date: 2026-09-11
- Start:
- End:
- macOS version: 26.6.2 (build 25G83)
- Device: Mac14,2 · arm64
- Commit: `84c8405` — docs: document tray pet visibility controls
- Stretch interval used:  (10 min for a first session — the point is to live the
  Core Loop several times, not to judge whether the interval is right)

## Pre-test rule

During this session:

- Do not fix issues while testing.
- Do not implement new ideas.
- Record first, decide later.
- NICE ideas are not MVP work.
- Only BLOCKER and CORE UX may become immediate work.

## Test Scenarios

Tick what held true. When something fails, do not debug it — write the time in the
Timeline and keep working.

### Startup

- [ ] app launches normally
- [ ] pet appears correctly
- [ ] saved position is restored
- [ ] timer starts correctly

Notes:

### Pet

- [ ] idle animation looks correct
- [ ] pet can be dragged
- [ ] pet does not interfere excessively with normal work
- [ ] a plain left click plays nothing — Mochi has no `click` clip, so the `idle` loop
      simply continues
- [ ] no click ever starts the stretch clip
- [ ] clicking the pet mid-stretch does not cut the stretch short

Notes:

### Hide / Show pet

- [ ] tray menu reads **Hide Pet** while the pet is visible
- [ ] tray **Hide Pet** hides it; the item then reads **Show Pet**
- [ ] tray **Show Pet** brings it back in the same place
- [ ] the Settings checkbox does the same thing
- [ ] changing one updates the other immediately
- [ ] the choice survives a restart

Notes:

### Timer

- [ ] tray tooltip countdown matches the interval that was set
- [ ] tray menu and Settings show the same remaining time
- [ ] Pause / Resume behaves as expected
- [ ] Reset restarts the countdown
- [ ] idle-aware pause: time spent away from the keyboard does not count down
- [ ] countdown survives the machine sleeping and waking

Notes:

### Stretch Reminder

- [ ] automatic timer fires
- [ ] Reminder Window appears
- [ ] visible pet plays stretch exactly once
- [ ] pet returns to idle
- [ ] notification appears
- [ ] repeated Stretch now while reminder is open does not restart pet animation
- [ ] hidden pet does not animate when reminder fires
- [ ] a hidden pet never reappears on its own

Notes:

### Manual Stretch

- [ ] tray **Stretch now**
- [ ] Settings **Stretch now**
- [ ] pet context menu **Stretch now**

All three call the same code path. Record whether they *feel* the same — window
placement, focus, timing, whether the pet reacted, whether a notification appeared:

### Long run (60–90 minutes)

- [ ] app stays responsive for the whole session
- [ ] no growing CPU or fan noise
- [ ] pet never vanishes, freezes, or duplicates
- [ ] pet stays where it was left across reminders and window switches
- [ ] the reminder never stole focus at a genuinely bad moment
- [ ] I was still willing to keep it running at the end

Notes:

### Real-use observation

Answer in sentences, not ticks. Short and honest beats complete.

- Did I actually notice the pet stretching?

- Did I understand why it stretched?

- Did I physically stretch?

- Was the reminder too aggressive?

- Was it too easy to ignore?

- Did the pet feel alive or merely animated?

- Did the Reminder Window add value, or did it duplicate the pet?

- Which signal reached me first — the pet, the window, or the OS notification?

- Three signals fire at once. Was that too much?

- Did a click that does nothing feel calm, or did the pet feel dead?

- Was Hide / Show easy to find when I wanted it?

- Did the pet become annoying during focused work?

- Did I want to hide it?

- Did I miss Snooze?

- What moment felt most pleasant?

- What moment felt most awkward?

- After 60–90 minutes, do I want to keep it running tomorrow?

## Timeline

Add a row whenever something happens or something bothers you. Categories are defined
below.

| Time | What happened | What I did | Feeling / friction | Category |
|---|---|---|---|---|
|  |  |  |  |  |

## BLOCKER

Ships-stopping. Any of:

- crash
- app cannot launch
- timer fundamentally broken
- pet disappears incorrectly
- reminder fails
- severe CPU/memory issue
- core feature unusable

## CORE UX

The app works, but the core experience is weakened. For example:

- stretch reaction is too subtle
- reminder timing feels wrong
- pet is distracting
- user does not understand what to do
- pet and reminder window feel redundant

## NICE

Ideas that will not be built in the MVP. For example:

- more pets
- themes
- achievements
- extra customization
- new editor features

## Session Summary

### What worked

### BLOCKER

### CORE UX

### NICE

### Most important observation

One sentence:

> If I could change only one thing before shipping, it would be:

## Final Rule

After the session:

- Do not implement NICE items.
- Rank BLOCKER first.
- Then rank CORE UX by impact on the Core Loop.
- Do not add features without observed evidence.
