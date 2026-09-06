# Piskel Dark Reskin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reskin the vendored Piskel editor to a dark theme with `#357867` accent, large radii, floating panels, and pill controls — entirely through the runtime-injected `unfold-bridge.css`.

**Architecture:** Piskel runs in a `WKWebView` inside `CharacterEditorWindowController`. Its packaged stylesheet is never edited; all customization is appended to `Sources/Unfold/Resources/Editor/unfold-bridge.css`, which the controller injects as a `<style>` at document end. This plan adds one `/* ===== Unfold reskin ===== */` block in six ordered sections, then verifies visually in the built app.

**Tech Stack:** Plain CSS. Swift Package Manager copies `Resources/Editor/` verbatim into the app bundle (`.copy` in `Package.swift`), so `swift run` / `swift build` picks up edits with no processing step.

**Spec:** `docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md`

**Piskel version this targets:** vendored commit `a6b9c02` (2026-09-04). Selectors below are class/structure based and version-independent, but a future re-vendor should re-check them (see `PISKEL-VERSION.txt`).

---

## File Structure

| File | Responsibility | Change |
| --- | --- | --- |
| `Sources/Unfold/Resources/Editor/unfold-bridge.css` | The one and only Piskel UI override layer, injected at runtime | Append reskin block (all six sections) |
| `docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md` | Design contract | One-line edit in Task 7 to resolve the "frame strip container" open item |

No other files change. `unfold-bridge.js`, the vendored `piskel/` directory, and all Swift are untouched.

## Conventions for every task

- **Append only.** Add each section to the end of `unfold-bridge.css`, below the previous one. Never edit the existing rules at the top of the file (they hide export/save/import and the shim header).
- **Brace-balance check** after each edit:
  ```bash
  python3 -c "s=open('Sources/Unfold/Resources/Editor/unfold-bridge.css').read(); assert s.count('{')==s.count('}'), (s.count('{'), s.count('}')); print('braces balanced:', s.count('{'))"
  ```
  Expected: `braces balanced: <n>` with no `AssertionError`.
- **Commit** after each task with the message shown.
- Use `!important` only on declarations that lose to Piskel's specificity. The existing file already uses it, so this is consistent. Do not blanket-apply it.

---

## Task 1: Tokens, highlight-color remap, page background

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the section header and token block**

Append to the end of the file:

```css

/* ============================================================================
 * Unfold reskin — dark theme, #357867 accent, large radius, floating panels.
 * Selectors target Piskel commit a6b9c02. Vendored piskel/ is untouched.
 * See docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md
 * ==========================================================================*/

:root {
  --u-accent:        #357867;                       /* fills / active states  */
  --u-accent-fg:     #5fae9c;                       /* text / icons / hairlines on dark */
  --u-panel-bg:      #1c1c1e;                       /* floating panel surface */
  --u-page-bg:       #0f0f10;                       /* shows through panel gaps */
  --u-control-bg:    #2a2a2d;                       /* inactive controls / chips */
  --u-radius-panel:   20px;
  --u-radius-control: 12px;
  --u-radius-pill:    999px;
  --u-shadow:         0 10px 30px rgba(0, 0, 0, 0.45);
  --u-shadow-modal:   0 24px 60px rgba(0, 0, 0, 0.55);
  --u-gap:            12px;

  /* Piskel defines this on html,body and uses var(--highlight-color) in some
   * rules — remapping it here recolors those for free. */
  --highlight-color: var(--u-accent) !important;
}

/* Bottom layer visible between the floating panels added in Task 4. */
html, body { background: var(--u-page-bg) !important; }
```

- [ ] **Step 2: Brace-balance check**

Run the brace-balance command from "Conventions". Expected: balanced, no error.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): reskin tokens + highlight-color remap + page bg"
```

---

## Task 2: Accent — replace literal `gold`

Piskel uses the literal keyword `gold` in ~50 rules that `--highlight-color` does not cover. Override them by area. Fills and active borders get `--u-accent`; text, icons, and 1px hairlines get the lighter `--u-accent-fg`.

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the accent block**

```css

/* --- accent: text / hairline (lighter tint for contrast on near-black) --- */
.highlight,
a, a:visited,
.tooltip-shortcut,
.cursor-coordinates,
.settings-title,
.local-piskel-list-head,
.cheatsheet-link,
.cheatsheet-shortcut-editable .cheatsheet-key,
.create-palette-new-color,
.import-image-file-name,
.import-meta-label,
.import-resize-option :checked + span,
.insert-mode-option :checked + span,
.import-resize-warning,
.dialog-performance-info-body sup,
#current-user-agent {
  color: var(--u-accent-fg) !important;
}

.textfield:focus,
.cheatsheet-shortcut-editable .cheatsheet-key,
.browse-backups .browse-backups-disclaimer .backups-icon,
.create-palette-color:hover,
.create-palette-new-color {
  border-color: var(--u-accent-fg) !important;
}

.button:hover { color: var(--u-accent-fg) !important; }

/* --- accent: fills / active borders --- */
.button-primary {
  background-color: var(--u-accent) !important;
  color: #fff !important;
}
.button-primary:hover {
  background-color: var(--u-accent-fg) !important;
  color: #fff !important;
}

.tool-icon.selected:before,
.background-picker.selected,
.grid-colors-item.selected,
.create-palette-color.selected {
  border-color: var(--u-accent) !important;
}

.right-sticky-section .tool-icon.has-expanded-drawer {
  border-left-color: var(--u-accent) !important;
}

.import-info { border-right-color: var(--u-accent) !important; }

.cheatsheet-actions { background-color: var(--u-accent) !important; }

.dialog-head {
  background: var(--u-accent) !important;
  color: #fff !important;
}

/* Primary/secondary palette color markers are inline SVGs with stroke="gold".
 * Re-point them at the same paths drawn with #357867 (%23357867). */
.palettes-list-primary-color:before {
  background: url("data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A//www.w3.org/2000/svg%22%20height%3D%2215%22%20width%3D%2215%22%3E%3Cpath%20stroke%3D%22%23357867%22%20stroke-width%3D%222%22%20d%3D%22M1%203v10h10z%22/%3E%3C/svg%3E") !important;
}
.palettes-list-secondary-color:before {
  background: url("data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A//www.w3.org/2000/svg%22%20height%3D%2215%22%20width%3D%2215%22%3E%3Cpath%20stroke%3D%22%23357867%22%20stroke-width%3D%222%22%20d%3D%22M3%2013h10V3z%22/%3E%3C/svg%3E") !important;
}
```

- [ ] **Step 2: Brace-balance check** — run the command; expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): recolor Piskel gold accent to #357867"
```

---

## Task 3: Radius

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the radius block**

```css

/* --- radius --- */
.button,
.textfield,
.textfield-small { border-radius: var(--u-radius-control) !important; }

/* Pill on the primary actions only. */
.button-primary,
#unfold-save-button,
.dialog-content .button { border-radius: var(--u-radius-pill) !important; }

/* Panels. .drawer-content also drops its one-sided 4px corners + old shadow. */
.drawer-content,
.dialog-content,
#dialog-container { border-radius: var(--u-radius-panel) !important; }

/* Frame thumbnails and tool buttons. */
.preview-tile,
.tool-icon,
.add-frame-action { border-radius: var(--u-radius-control) !important; }

/* The animated-preview panel in the right column has a one-sided radius. */
#animated-preview-container.preview-container {
  border-radius: var(--u-radius-panel) !important;
}
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): round Piskel controls, panels, and modals"
```

---

## Task 4: Floating panels

Piskel's sticky rails are `position: fixed` with computed `left`/`right`/`width`/`max-width`. Only paint a surface + shadow + margin on them. Do **not** touch position or size properties.

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the floating-panel block**

```css

/* --- floating panels --- */
#tool-section.left-sticky-section,
#application-action-section.right-sticky-section,
.drawer-content,
#preview-list-wrapper.preview-list-wrapper,
#animated-preview-container.preview-container {
  background: var(--u-panel-bg) !important;
  border-radius: var(--u-radius-panel) !important;
  box-shadow: var(--u-shadow) !important;
  margin: var(--u-gap) !important;
}

/* .drawer-content ships an inner shadow + 550px fixed height + dark grey bg;
 * keep the height, override the look. */
.drawer-content { background-color: var(--u-panel-bg) !important; }

/* The expanded-drawer tab keeps its own darker fill so it reads as attached. */
.right-sticky-section .tool-icon.has-expanded-drawer {
  background-color: var(--u-control-bg) !important;
}
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): float Piskel panels with gaps and soft shadow"
```

---

## Task 5: Pills and chips

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the pill block**

```css

/* --- pills --- */
.button-primary,
#unfold-save-button,
.dialog-content .button {
  border-radius: var(--u-radius-pill) !important;
  height: auto !important;
  padding: 10px 22px !important;
}

/* Square icon-only toggles must stay boxy, not pill (would smear the sprite). */
.size-picker-option,
.pen-size-option { border-radius: var(--u-radius-control) !important; }
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): pill-shape primary actions in Piskel"
```

---

## Task 6: Modals

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the modal block**

```css

/* --- modals --- */
#dialog-container {
  background: var(--u-panel-bg) !important;
  border: 1px solid var(--u-accent) !important;
  border-radius: var(--u-radius-panel) !important;
  box-shadow: var(--u-shadow-modal) !important;
}
.dialog-content { background: var(--u-panel-bg) !important; }

.dialog-close {
  background: var(--u-control-bg) !important;
  border-radius: var(--u-radius-pill) !important;
}

#dialog-container-wrapper { backdrop-filter: blur(2px); }
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): restyle Piskel modals to match reskin"
```

---

## Task 7: Build, verify in the app, reconcile spec

No automated test covers injected CSS. Verify by eye in the real editor.

**Files:**
- Modify: `docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md` (one line)

- [ ] **Step 1: Build and launch the bundled app**

```bash
swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"
```
Expected: builds clean, app launches in the menu bar.

- [ ] **Step 2: Open the character editor**

From the app's menu bar item, open Settings → create or edit a character → the Piskel editor window opens.

- [ ] **Step 3: Walk the verification checklist**

Confirm each, matching spec §7:

- [ ] Left tool rail, right action rail, and the frames column each read as a separate rounded panel with a shadow and a gap of dark `#0f0f10` around them.
- [ ] Selecting each drawing tool shows a `#357867` border, not gold.
- [ ] Expand the right drawer (e.g. the frames/settings drawer): panel is rounded `#1c1c1e`; the active tab shows the `#357867` left edge.
- [ ] The "Save to Spine Keepet" button is a teal pill; hover lightens to `#5fae9c`; disabled state still dims.
- [ ] Canvas coordinate/zoom readout text is `#5fae9c`, legible.
- [ ] Onion-skin and layer toggles in their enabled state use the new accent.
- [ ] Open **Resize** (settings): modal is rounded `#1c1c1e` with a soft shadow, teal 1px border, circular close button, teal header bar, pill Cancel/confirm buttons.
- [ ] Open **Keyboard shortcuts**, **Manage palette** (create palette), and **Browse backups** if reachable: same panel + button treatment; no stray gold.
- [ ] The pixel canvas itself is still dark (unchanged).
- [ ] The canvas drawing area is not visibly clipped or pushed off-center by the panel margins. If it is, change Task 4's `margin: var(--u-gap)` on `#tool-section` / `#application-action-section` / `.preview-list-wrapper` to `padding` on the inner wrap, or to a `transform: translate`, and re-verify.

- [ ] **Step 4: Resolve the spec's open item**

In `docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md` §4, replace the comment line

```
/* 하단 프레임 리스트 컨테이너 (실제 클래스는 구현 시 DOM에서 확인) */ {
```

with

```
#preview-list-wrapper.preview-list-wrapper,  /* 왼쪽 프레임 컬럼 (하단 아님) */
```

and in the same section's prose, correct "하단 프레임 스트립" to "왼쪽 프레임 컬럼".

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-09-06-piskel-dark-reskin-design.md
git commit -m "docs: resolve frame-column selector in Piskel reskin spec"
```

- [ ] **Step 6: Quit the test app**

Use **Quit Spine Keepet** from its menu bar item.

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
| --- | --- |
| §1 Tokens | Task 1 |
| §2 Accent (`--highlight-color` + literal gold table + SVG markers) | Task 1 (remap) + Task 2 |
| §3 Radius | Task 3 |
| §4 Floating panels (+ page bg, + canvas-shift fallback) | Task 1 (page bg) + Task 4 (+ Task 7 Step 3 fallback) |
| §5 Pills / chips | Task 5 |
| §6 Modals | Task 6 |
| §7 Manual verification | Task 7 |
| §8 Out of scope | Nothing to build |
| Risks (selector fragility, specificity, canvas shift) | Version note in header; `!important` convention; Task 7 fallback |

No gaps.

**Placeholder scan:** No "TBD"/"handle edge cases"/"similar to Task N". Every CSS step is the literal block to append. The one comment token in the spec is explicitly resolved in Task 7 Step 4.

**Type consistency:** Token names (`--u-accent`, `--u-accent-fg`, `--u-panel-bg`, `--u-page-bg`, `--u-control-bg`, `--u-radius-panel`, `--u-radius-control`, `--u-radius-pill`, `--u-shadow`, `--u-shadow-modal`, `--u-gap`) are defined once in Task 1 and referenced identically in Tasks 2–6. Selector IDs match the vendored markup verified during planning: `#tool-section`, `#application-action-section`, `#drawer-container`/`.drawer-content`, `#preview-list-wrapper`, `#animated-preview-container`, `#dialog-container`, `#dialog-container-wrapper`, `#unfold-save-button`.

**Note:** Tasks 3 and 5 both set `border-radius` on `.button-primary` / `#unfold-save-button` / `.dialog-content .button`. This is intentional and idempotent — Task 3 rounds them as part of the radius pass, Task 5 re-declares alongside the padding/height that makes them true pills. Later cascade wins with the same value; no conflict. If executing as one pass, Task 5's rule may be merged into Task 3.
