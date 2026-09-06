# Piskel Tool Rail Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove Piskel's left tool rail and reflow its contents into a single right-side vertical rail (tools cluster top, preferences/resize cluster bottom), reclaiming the freed left width for the canvas — CSS-only, appended to `unfold-bridge.css`.

**Architecture:** Approach B from the spec. `#tool-section` and `#application-action-section` are both `position: fixed`; their event listeners are bound to the nodes, not their positions, so only CSS position/layout properties change. `#tool-section` moves to top-right; `#application-action-section` stays vendor-driven (drawer intact) but anchors bottom-right and restores full height while a drawer is open. No `unfold-bridge.js` or vendored `piskel/` changes.

**Tech Stack:** Plain CSS. SwiftPM copies `Resources/Editor/` verbatim into the app bundle.

**Spec:** `docs/superpowers/specs/2026-09-06-piskel-tool-rail-consolidation-design.md`
**Builds on:** `docs/superpowers/plans/2026-09-06-piskel-dark-reskin.md` (the reskin block this appends below)
**Piskel version:** vendored commit `a6b9c02`. Selectors are class/id based; re-check on re-vendor.

---

## File Structure

| File | Responsibility | Change |
| --- | --- | --- |
| `Sources/Unfold/Resources/Editor/unfold-bridge.css` | The Piskel UI override layer | Append a `/* ===== Unfold tool-rail consolidation ===== */` section below the existing reskin block |

Nothing else changes.

## Conventions for every task

- **Append only**, below the previous section. The file currently ends with:
  `#dialog-container-wrapper { backdrop-filter: blur(2px); }`
- **Brace-balance check** after each edit:
  ```bash
  python3 -c "s=open('Sources/Unfold/Resources/Editor/unfold-bridge.css').read(); assert s.count('{')==s.count('}'), (s.count('{'), s.count('}')); print('braces balanced:', s.count('{'))"
  ```
  Expected: `braces balanced: <n>`, no `AssertionError`.
- **Commit** after each task with the message shown.
- `!important` is required throughout — every rule here overrides a vendor rule of equal or higher specificity. Consistent with the rest of the file.

---

## Task 1: Section header + reclaim the left column width

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the header and `.column-wrapper` override**

Append to the end of the file:

```css

/* ============================================================================
 * Unfold tool-rail consolidation — drop the left rail, stack everything on the
 * right (tools cluster top, preferences/resize cluster bottom). CSS only.
 * Selectors target Piskel commit a6b9c02. See
 * docs/superpowers/specs/2026-09-06-piskel-tool-rail-consolidation-design.md
 * ==========================================================================*/

/* Left rail is gone: reclaim its 100px, reserve the right rail's ~124px
 * (right:12 + width:100 + 12 breathing). */
.column-wrapper {
  left: 0 !important;
  right: 124px !important;
}
```

- [ ] **Step 2: Brace-balance check** — run the command; expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): reclaim left column width for the canvas"
```

---

## Task 2: Tool cluster — move `#tool-section` to top-right

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the tool-cluster block**

```css

/* --- tools cluster: top-right, content height, internal scroll if tall --- */
#tool-section.left-sticky-section {
  left: auto !important;
  right: 12px !important;
  top: 12px !important;
  bottom: auto !important;
  margin: 0 !important;                        /* cancel reskin margin: var(--u-gap) */
  max-width: none !important;
  width: 100px !important;
  max-height: calc(100vh - 24px) !important;
  overflow-y: auto !important;
}

/* Vendor uses table/table-cell for full-height vertical centering; switch to
 * normal block flow so children stack top-down. */
#tool-section .sticky-section-wrap {
  display: block !important;
  height: auto !important;
}
#tool-section .vertical-centerer {
  display: block !important;
}

/* Drawing tools: vendor floats .tool-icon left inside a 100px box. Replace the
 * float with a centered wrap grid. */
#tools-container.tools-wrapper {
  display: flex !important;
  flex-wrap: wrap !important;
  justify-content: center !important;
}
#tool-section .tool-icon {
  float: none !important;
}

/* Hide the scrollbar when the rail overflows (keep the dark look). */
#tool-section::-webkit-scrollbar {
  width: 0 !important;
  height: 0 !important;
}
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): move Piskel tool cluster to the top-right rail"
```

---

## Task 3: Settings cluster — anchor `#application-action-section` bottom-right

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append)

- [ ] **Step 1: Append the settings-cluster block**

```css

/* --- preferences/resize cluster: bottom-right, same width as the tools rail --- */
#application-action-section.right-sticky-section {
  right: 12px !important;
  top: auto !important;
  bottom: 12px !important;
  margin: 0 !important;                        /* cancel reskin margin: var(--u-gap) */
  width: 100px !important;
}
#application-action-section .sticky-section-wrap {
  display: block !important;
  height: auto !important;
}
#application-action-section .vertical-centerer {
  display: block !important;
}

/* While a drawer is open, restore full height so the 550px drawer content lays
 * out on-screen. Vendor's .right-sticky-section.expanded { right: 280px } still
 * does the reveal slide; transition: all 200ms animates the height change too. */
#application-action-section.right-sticky-section.expanded {
  top: 12px !important;
  bottom: 12px !important;
}
```

- [ ] **Step 2: Brace-balance check** — expected balanced.

- [ ] **Step 3: Commit**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): anchor Piskel preferences/resize to the bottom-right"
```

---

## Task 4: Build, verify in the app, apply the conditional drawer tweak

No automated test covers injected CSS. Verify by eye.

**Files:**
- Modify: `Sources/Unfold/Resources/Editor/unfold-bridge.css` (append — only if Step 3 finding is hit)

- [ ] **Step 1: Build and launch**

```bash
swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"
```
Expected: builds clean, app launches.

- [ ] **Step 2: Open the editor**

Menu bar item → Settings → create or edit a character → Piskel editor window opens.

- [ ] **Step 3: Walk the verification checklist (spec §5)**

- [ ] Left side shows only the frame timeline; no tool rail. Canvas is wider on the left.
- [ ] Top-right, top to bottom: pen-size picker → drawing tools (2-wide, centered) → primary/secondary color pickers → swap-colors.
- [ ] Bottom-right: preferences and resize icons.
- [ ] Each drawing tool: click selects it (teal outline) and it actually draws on the canvas (the `#tool-section` mousedown delegation still fires).
- [ ] Pen sizes 1–4 respond.
- [ ] Primary/secondary color pickers open; swap-colors works.
- [ ] Click preferences → drawer opens, the settings cluster grows to full height, drawer content renders correctly.
- [ ] Click resize → drawer works; a resize actually applies to the canvas.
- [ ] Manage palette / browse backups (if reachable) → their drawers render fine.
- [ ] Close the drawer → settings cluster returns to the bottom.
- [ ] Shrink the window vertically → the tool rail scrolls internally; nothing is clipped off-screen.
- [ ] The canvas drawing area does not overlap either rail. If it does, adjust `.column-wrapper { right }` in Task 1 upward and re-verify.

- [ ] **Step 4: Conditional — dim the tool cluster while a drawer is open**

Only if Step 3 shows the tools cluster (top-right) visually colliding with the settings cluster when it expands: append

```css

/* When a drawer is open the settings cluster fills the height; fade the tools
 * cluster so the overlap does not read as broken. */
#main-wrapper:has(#application-action-section.expanded) #tool-section.left-sticky-section {
  opacity: 0.3 !important;
  pointer-events: none !important;
}
```

Then brace-balance check, then:

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.css
git commit -m "feat(editor): fade tool cluster while a settings drawer is open"
```

If Step 3 showed no collision, skip this step and note it in the completion report.

- [ ] **Step 5: Quit the test app** — **Quit Spine Keepet** from its menu bar item.

**Deviation (verification-driven):** Task 3's `#application-action-section .sticky-section-wrap { display: block }` and `.vertical-centerer { display: block }` overrides broke the drawer (the always-present 550px `.drawer-content` fell into block flow, covering the tools). They were removed; the vendor table layout is kept and `#application-action-section.right-sticky-section:not(.expanded) .drawer { display: none }` hides the drawer cell while collapsed instead. Spec §3's code block still shows the old approach — treat this plan's Task 4 as the source of truth.

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
| --- | --- |
| §1 Left space reclaim | Task 1 |
| §2 Tool cluster (top-right, layout reset, tools flex-wrap, scrollbar hide) | Task 2 |
| §3 Settings cluster (bottom-right, `.expanded` full-height restore) | Task 3 |
| §4 Unification (width match, spacing) | Tasks 2 + 3 (`width: 100px` on both; spacing from top/bottom anchors). Explicit divider is spec-optional and not requested — not built. |
| §5 Manual verification | Task 4 Steps 1–3, 5 |
| §6 Risk: drawer-open collision | Task 4 Step 4 (conditional) |
| §6 Risk: canvas width | Task 4 Step 3 last bullet (adjust `.column-wrapper right`) |
| §7 Out of scope | Nothing to build |

No gaps.

**Placeholder scan:** No "TBD" / "handle edge cases" / "similar to Task N". Every CSS step is the literal block to append. Task 4 Step 4 is explicitly conditional with its trigger stated.

**Type / selector consistency:** IDs and classes match the vendored markup verified during design: `#tool-section.left-sticky-section`, `#tools-container.tools-wrapper`, `#application-action-section.right-sticky-section`, `.sticky-section-wrap`, `.vertical-centerer`, `.column-wrapper`, `#main-wrapper`. The `margin: 0 !important` in Tasks 2–3 deliberately cancels the `margin: var(--u-gap)` the reskin plan's Task 4 set on these same selectors — the two plans are consistent and the later rule wins the cascade. `.right-sticky-section.expanded` is a vendor rule (`right: 280px`); Task 3 adds `top`/`bottom` to the same selector without touching `right`.
