# Handoff — Piskel editor reskin + tool-rail consolidation

> **Read this first.** Everything below the `---` was written at commit
> `8cf5239` and parts of it are now wrong. This section is the current state.

## Current state (2026-09-06, HEAD `e63082b`)

### The `a3f5f2d` "UI consistency pass" was reverted

`a3f5f2d` moved `#main-wrapper` / `.column-wrapper` insets, restyled every
inspector panel, and — the fatal part — monkey-patched Piskel's private
`drawingController.getAvailableWidth_` / `getAvailableHeight_` to measure the
`.main-column` flex slot. That box resolves to 0 during window occlusion and
reflow, which collapsed the canvas. Each attempt to repair it produced a new
rendering regression (occlusion flicker → permanent collapse → erase/draw
visibility toggle), so `04c8af0` restored `unfold-bridge.css` to its `72c156a`
content and stripped the JS additions.

**Do not reintroduce a `getAvailableWidth_` override.** Vendor's version
measures `#main-wrapper`, a fixed-inset element that always has a real size.
It over-reserves roughly one rail width in the consolidated layout, so the
canvas fits with more margin than strictly necessary — that is cosmetic and
deliberately accepted.

One piece of `a3f5f2d` was kept because it fixed a real save bug:
`buildSheetDataURL` passes `renderFrameAt`'s return value straight to
`drawImage`. It returns a canvas, not a `Frame`, so the old
`FrameUtils.toImage(frame, 1)` call was wrong.

### Bridge workarounds for two vendor Piskel rendering bugs (`e63082b`)

Single-click erase used to blank the whole sprite until the next edit while
drag-erase was fine. Root-caused with a scripted harness driving synthetic
mouse events in the real WKWebView (0/10 rounds passing before, 10/10 after):

1. `FrameUtils.drawToCanvas` reuses module-global scratch objects keyed only by
   sprite size, shared by every renderer in the same animation frame. A deferred
   compositing `drawImage` can pick up a later renderer's content — and the tool
   overlay is fully transparent right after an erase.
2. `CachedFrameRenderer.render` stores its cache key *before* painting, so a
   paint that doesn't land leaves the renderer believing the frame is on screen.

`unfold-bridge.js` now forces a 1x1 readback after each `drawToCanvas` and
clears the cache key on entry to every render. Both are in
`installSharedScratchFlush()` / `installRenderCacheInvalidation()` and must be
re-checked on re-vendor.

### Other changes since `8cf5239`

- `036dab5` — `SettingsView.refreshNotificationStatus` now guards on
  `Bundle.main.bundleIdentifier`. `UNUserNotificationCenter.current()` throws
  when there is no app bundle, so `swift run` crashed on opening Settings.
- `9cbe14c` — `whenPiskelReady` polls with a ~10s cap and surfaces a banner
  instead of looping forever; missing `unfold-bridge.css`/`.js` is logged; a
  non-string save message re-enables the save button instead of wedging it.
- A `#if DEBUG` **"Open Character Editor (Debug)"** status-menu item opens the
  editor without going through the Settings window. Added purely as dev tooling
  for the investigation above — safe to delete.

### Open, not done

- `unfold-bridge.css` carries ~200 `!important` declarations. Necessary to beat
  vendor rules, but new rules now have to join the arms race. A theme-only
  rewrite (colours/radius/shadow, no `position`/`display`/layout overrides) is
  the standing recommendation if editor styling is revisited.
- Cosmetic review leftovers: redundant `#preview-list-scroller { height: 100% }`,
  the `JSONSerialization([x])…[0]` string-embedding helper repeated three times
  in `CharacterEditorWindowController`, no length cap on the character name, no
  `prefers-reduced-motion` handling.
- Branch `major` is 6 commits ahead of `origin/major` and unpushed.

---

Date: 2026-09-06
Branch: `major` (NOT pushed — 22 commits ahead of `origin/major`)
HEAD at handoff: `72c156a`
Working tree: clean

This document lets a fresh session on any machine continue the work. Read it top
to bottom, then `git log --oneline 6fc4393..HEAD` for the commit trail.

---

## 1. What this work is

Two sequential pieces of visual work on the **vendored Piskel editor** that runs
in a `WKWebView` inside the character editor
([`CharacterEditorWindowController`](../../../Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift)):

1. **Dark reskin** — keep Piskel's dark theme, swap the gold accent to deep teal
   `#357867`, large rounded corners, floating panels with soft shadows, pill
   buttons, restyled modals. **Done.** 6 feat commits + 1 spec fix.
2. **Tool-rail consolidation** — remove Piskel's left tool rail; put everything on
   one right-side vertical rail (tools cluster top, preferences/resize cluster
   bottom); the settings drawer floats in as an independent fixed panel; reclaim
   the freed left width for the canvas. **Implemented; final holistic review says
   "ready to merge."** 5 feat/anchor commits + 5 verification-driven fix commits.

Both were built with the superpowers workflow: brainstorm → spec → plan →
execute. The consolidation was executed subagent-driven (implementer + spec
review + code-quality review per task).

---

## 2. The one mechanism you need to know

**All customization lives in one file:**
[`Sources/Unfold/Resources/Editor/unfold-bridge.css`](../../../Sources/Unfold/Resources/Editor/unfold-bridge.css)
(361 lines).

- It is injected at runtime as a `<style>` at `.atDocumentEnd`, after Piskel's
  packaged stylesheet, via `WKUserScript`
  (`CharacterEditorWindowController.swift:172-178`).
- The vendored `Sources/Unfold/Resources/Editor/piskel/` directory is **never
  edited**. Update model (see
  [`PISKEL-VERSION.txt`](../../../Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt)):
  replace the whole `piskel/` dir on a re-vendor; there are no patches to
  reapply — but re-check this CSS's selectors, since they target vendor
  markup/classes of Piskel commit `a6b9c02`.
- `unfold-bridge.js` exists but was **not touched** by either piece of work.
- Every rule uses `!important` — it's the house style here (each rule overrides a
  vendor rule of equal/higher specificity).

File structure of `unfold-bridge.css`:
| Lines | Section |
| --- | --- |
| 1–55 | Original: hide export/save/import, hide shim header, `#unfold-save-button`, `#unfold-error` |
| 57–226 | `/* ===== Unfold reskin ===== */` — tokens (`--u-*`), accent swap, radius, floating panels, pills, modals |
| ~228–361 | `/* ===== Unfold tool-rail consolidation ===== */` — `.column-wrapper` reclaim, tool cluster (`#tool-section`), settings cluster (`#application-action-section`), floating drawer, palette column |

Design tokens (defined once at reskin `:root`): `--u-accent: #357867`,
`--u-accent-fg: #5fae9c`, `--u-panel-bg: #1c1c1e`, `--u-page-bg: #0f0f10`,
`--u-control-bg: #2a2a2d`, `--u-radius-panel: 20px`, `--u-radius-control: 12px`,
`--u-radius-pill: 999px`, `--u-shadow`, `--u-shadow-modal`, `--u-gap: 12px`.

---

## 3. Doc map

| Doc | Purpose |
| --- | --- |
| [specs/2026-09-06-piskel-dark-reskin-design.md](../specs/2026-09-06-piskel-dark-reskin-design.md) | Reskin design contract |
| [plans/2026-09-06-piskel-dark-reskin.md](../plans/2026-09-06-piskel-dark-reskin.md) | Reskin implementation plan (7 tasks, all done) |
| [specs/2026-09-06-piskel-tool-rail-consolidation-design.md](../specs/2026-09-06-piskel-tool-rail-consolidation-design.md) | Consolidation design (Approach B). **§2 and §3 code blocks are STALE** — see the plan's Task 4 "Deviation" note; the plan is source of truth. |
| [plans/2026-09-06-piskel-tool-rail-consolidation.md](../plans/2026-09-06-piskel-tool-rail-consolidation.md) | Consolidation plan. Task 4 has a "Deviation (verification-driven)" note at the end recording the fixes below. |

---

## 4. Git state (precise)

```
branch: major   (ahead of origin/major by 22, NOT pushed)
HEAD:   72c156a  fix(editor): drop the tool-cluster fade (drawer no longer overlaps it)
```

Commit trail (oldest → newest):
```
6fc4393 docs: design for Piskel dark reskin via unfold-bridge.css
85c7224 docs: implementation plan for Piskel dark reskin
163e53c feat(editor): reskin tokens + highlight-color remap + page bg
da0ee12 feat(editor): recolor Piskel gold accent to #357867
e3dc93f feat(editor): round Piskel controls, panels, and modals
2550ba4 feat(editor): float Piskel panels with gaps and soft shadow
4368b1f feat(editor): pill-shape primary actions in Piskel
e7406fc feat(editor): restyle Piskel modals to match reskin
e5c3b27 docs: resolve frame-column selector in Piskel reskin spec
34ad6f9 docs: design for consolidating Piskel tool rail to the right (approach B)
4883afb docs: implementation plan for Piskel tool-rail consolidation
5f3a781 feat(editor): reclaim left column width for the canvas
4a43318 feat(editor): move Piskel tool cluster to the top-right rail
6df44a1 fix(editor): drop tools-wrapper float so the rail stacks by block flow
343b158 feat(editor): anchor Piskel preferences/resize to the bottom-right
e416d42 fix(editor): re-assert drawer slide that the base right override suppressed
8934a68 docs: sync spec §3 code block with the drawer-slide fix
97c1a86 fix(editor): keep vendor drawer layout; clip it while collapsed; reserve settings band
1c6661d fix(editor): hide the collapsed drawer so the settings cluster bottom-anchors
ef008f9 fix(editor): float the drawer free of the icon strip; fix swap-colors placement
a84933c fix(editor): neutralize leftover vendor palette offsets; restore swap hover
72c156a fix(editor): drop the tool-cluster fade (drawer no longer overlaps it)
```

`major` also carried unrelated pre-existing work before `6fc4393` (character
CRUD, editor window, etc.). Do **not** merge all of `major` to `main` as "the
reskin" — scope any PR to `6fc4393..HEAD` or cherry-pick.

---

## 5. What remains

### 5a. Cosmetic follow-ups (flagged by final review, NOT applied)

All in `unfold-bridge.css`, all `!important`, all "leftover vendor magic offset"
cleanups of the same kind already done in `a84933c`. None block merge.

1. **`.palette-wrapper` still has vendor `margin-top: 10px; margin-left: 10px`** →
   palette column sits ~5px right of centre, 10px below the tool grid. Add to the
   `#tool-section .palette-wrapper { ... }` rule:
   ```css
   margin: 0 !important;
   ```
2. **Swatch `<input>` keeps vendor `margin-left: 2px`** → 2px L/R asymmetry. Extend
   the existing `#tool-section .palette-wrapper .tool-color-picker input, …` rule:
   ```css
   margin-left: 0 !important;
   ```
3. **Settings icons keep vendor `float: right`** → preferences/resize render as a
   horizontal pair in reversed reading order (resize left of preferences). If a
   vertical stack in reading order is wanted, add:
   ```css
   #application-action-section .tool-icon { float: none !important; }
   ```
4. **Spec staleness** — add a one-line "superseded by plan Task 4 — see Deviation
   note" pointer at the top of
   `specs/2026-09-06-piskel-tool-rail-consolidation-design.md` §2 and §3 so a
   future reader doesn't re-introduce the drawer-breaking `display: block`
   overrides or the `max-height: calc(100vh - 24px)` value.

### 5b. Finish the branch

Not started. Options (superpowers `finishing-a-development-branch`): merge
`6fc4393..HEAD` to `main`, open a PR, keep the branch, or (no) discard. User has
not chosen. `origin/major` is behind by 22 — nothing is pushed.

### 5c. Full test suite

`swift test` last run green before the consolidation fix rounds (267 tests, 0
failures). Re-run before finishing — the changes are CSS-only so it should stay
green, but confirm.

---

## 6. Build / run / verify

```sh
# Build + bundle + launch (menu-bar app "Spine Keepet")
swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"

# CSS sanity check used throughout (the only automated gate for injected CSS)
python3 -c "s=open('Sources/Unfold/Resources/Editor/unfold-bridge.css').read(); assert s.count('{')==s.count('}'), (s.count('{'), s.count('}')); print('braces balanced:', s.count('{'))"
# expect: braces balanced: 51

swift test   # 267 tests, expect 0 failures
```

Manual verification path: menu-bar icon → **Settings** → create/edit a character
→ Piskel editor window opens (1100×760, resizable).

Manual checklist (from the consolidation spec §5, updated for the deviations):
- Left side: only the frame timeline; no tool rail; canvas wider on the left.
- Top-right cluster, top→bottom: pen sizes → drawing tools (2-wide, centred) →
  the two colour pickers → swap-colors (arrow visible, brightens on hover).
- Bottom-right cluster: preferences + resize icons — **stay put when a drawer
  opens/closes** (do not slide).
- Click preferences / resize → drawer appears as a **floating panel left of the
  rails** (`position: fixed`, `right: 128px`), scrollable, correctly styled;
  close → it fully disappears.
- Each drawing tool selects (teal outline) and actually draws.
- Short window: tool cluster scrolls internally (`max-height: calc(100vh -
  108px)`); nothing clips.

---

## 7. Piskel internals learned the hard way (don't re-derive)

From reading `piskel/js/piskel-packaged-2026-09-04-12-08.js`:

- **`ToolController`** binds `mousedown` to the `#tool-section` node and uses
  `#tool-section .tool-icon.selected`. → drawing tools must stay inside
  `#tool-section`; moving the whole node is safe, moving its children out is not.
- **`SettingsController`** binds `click` to `[data-pskl-controller=settings]` (=
  `#application-action-section`) and, in `loadSetting_`, adds the `expanded` class
  to that section (LAST, after `#drawer-container.innerHTML = …` and
  sub-controller init) and reads nothing geometric. `onBodyClick_` closes via
  `Dom.isParent(target, #drawer-container)`. → settings icons must stay inside
  that section; the drawer can be repositioned (even `position: fixed`) and
  outside-click close still works.
- **Drawer reveal (vendor):** `.right-sticky-section.expanded { right: 280px }`
  (NO `!important`) slides the whole 47px-wide fixed section left; the 280px
  `.drawer-content` (which vendor keeps in an off-canvas `table-cell`) becomes
  visible in the gap. Our base rule `#application-action-section.right-sticky-section
  { right: 12px !important }` overrides that vendor rule in every state — this is
  why `e416d42` had to re-assert the slide, and why `ef008f9` ultimately dropped
  the slide entirely and floats `.drawer-content` with `position: fixed` instead.
- **`.drawer-content`** is always in the DOM at fixed `height: 550px; width:
  280px`. If it lands in normal block flow it covers everything — hence
  `#application-action-section.right-sticky-section:not(.expanded) .drawer {
  display: none !important }` (collapsed) + `position: fixed` on
  `.drawer-content` (expanded).
- **Canvas sizing** (`getAvailableWidth_`) sums `#tool-section` +
  `#application-action-section` widths and subtracts them, even though both now
  sit in one 112px right strip. Result: it over-reserves ~100–200px, so the
  sprite renders a bit smaller than the visible canvas area on small windows.
  Accepted Approach-B tradeoff; the error direction never causes overlap. The
  real canvas-width lever is `.column-wrapper { left / right }`.
- **`.swap-colors-button`** vendor rule: `position: relative; top: 50px; left:
  6px; opacity: 0.3` — magic offsets for the vendor's floated palette; neutralized
  in `a84933c`.
- Only **two** `data-setting` drawers are reachable (`user`=preferences,
  `resize`); save/export/import are hidden by the reskin; manage-palette and
  browse-backups render in `#dialog-container` (separate modal path), not the
  drawer.

---

## 8. Residual risks (tell the reviewer / user before merge)

- Zoom-to-fit slack (see §7) — sprite looks smaller than the canvas area on small
  windows. Not a bug.
- Drawer open/close is instant (no animation) — the section no longer moves; only
  the fixed panel appears/disappears. Matches the "floating panel" intent.
- `:has()` is **not** used in the final CSS (the fade rule that used it was
  removed in `72c156a`), so the macOS 13.0–13.2 `:has()` concern is moot.
- Spectrum colour-picker popups position from document coords; opening one after
  scrolling the tool rail internally can be a few px off. Edge case.

---

## 9. Housekeeping

- Visual-companion (superpowers brainstorming) scratch dirs under
  `.superpowers/brainstorm/` — gitignored (`.superpowers/` added to
  `.gitignore`). Safe to delete; a server may still be running on a localhost
  port (`scripts/stop-server.sh <session-dir>` or just ignore — it self-exits
  after 30 min).
- No new dependencies, no Swift changes, no `Package.swift` changes.
