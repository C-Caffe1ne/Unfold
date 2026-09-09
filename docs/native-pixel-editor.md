# Native pixel editor

The character editor opens a SwiftUI window with an AppKit drawing surface. It
does not instantiate a WKWebView, inject JavaScript, run Piskel timers, or load
web resources. The vendored Piskel runtime and its notices have been removed
from the repository; nothing in `Package.swift` needs to exclude it anymore.

## Tools

| Tool | Key | Behaviour |
|---|---|---|
| Pencil | `B` | Freehand. **Pixel-perfect** drops the middle pixel of an orthogonal corner before a diagonal step. |
| Spray | `⇧B` | Random stamps inside a bounded circle along the drag path. |
| Eraser | `E` | Clears to transparent. |
| Fill | `G` | Flood fill. Cannot cross a gap in the selection. |
| Gradient | `⇧G` | One linear ramp from the foreground to the background colour across the drawn span. |
| Eyedropper | `I` | Samples the composited frame. |
| Line / Rectangle / Ellipse | `L` / `U` / `⇧U` | Shape preview replaces the previous preview. |
| Rectangle selection | `M` | |
| Lasso | `Q` | Scanline fill of the traced polygon, boundary included. Capped at 4096 points. |
| Magic wand | `W` | Contiguous exact-RGBA match in the active layer/frame cel. |
| Move | `V` | Drags the selected pixels, erasing the source and clipping at the canvas edge. |
| Hand | `H` | Pans the viewport. |
| Brightness | — | ±26 per RGB channel. **Darken** flips the direction. Fully transparent pixels are left byte-identical. |

**Mirror X / Mirror Y** apply to brush, shape and fill stamps. They do not
change the gradient ramp, which stays a single linear two-colour ramp.

Hovering any control for half a second shows a tooltip whose first line is its
shortcut and its name, and whose second says what it does. The tool shortcuts
printed there are the ones `PixelTool.shortcut` answers to, checked by a test,
so a tooltip cannot advertise a key that nothing handles.

The tooltip is the editor's own panel, not `NSToolTipManager`: the system
tooltip owns its delay and, over a plain-styled button, tracks the drawn glyph
rather than the control's frame, so hovering a tool icon showed nothing.
`.pixelTooltip` overlays a tracking view that hit-tests to nothing — clicks go
to the control underneath — and orders a borderless panel in without taking key,
leaving the canvas's keyboard focus alone.

It does **not** also apply `.help`. SwiftUI draws that tooltip itself rather
than setting `NSView.toolTip`, so asking for both put two tooltips on screen
for one hover: SwiftUI attaches its help region to a wrapper larger than the
control, which dropped its tooltip far below the icon, while the editor's own
sat beside it. The text still reaches VoiceOver as an accessibility hint, which
draws nothing. A test asserts the `HelpView` wrapper is absent, and a companion
test asserts `.help` is what would introduce it.

The panel is anchored to the control the tracker covers: directly beneath it,
left edges aligned, flipping above when the bottom of the screen is in the way,
and clamped against the screen that control is actually on. It used to be
placed from `NSEvent.mouseLocation` and clamped against whichever screen was
judged to hold the pointer, falling back to `NSScreen.main` — so on a second
display the tooltip could be thrown back onto the main one, nowhere near the
icon being hovered. The arithmetic is a pure function over the control rect and
the screen rects, and is tested as one. Menu items keep `.help` only, having no
view to overlay and no second tooltip to collide with.

Tool icons also light up under the pointer, and the armed tool keeps its own
colour whether hovered or not.

A selection restricts every painting tool to its mask. An empty selection paints
nothing; no selection paints everywhere. Selection tools work on a locked layer;
tools that modify pixels do not.

## Canvas

- The bar above the canvas carries what is set while looking at it: grid,
  onion skin, pixel-perfect and the brush size, then the zoom controls.
- `Space` held pans without changing the tool. Middle-drag pans too.
- `⌘`/`⌃` + scroll wheel zooms the viewport, as do the magnifiers beside the
  canvas. Typing a percentage into the field between them jumps straight to a
  level. Zoom resamples nothing.
- `Return` plays and pauses. `Escape` deselects, `⌘A` selects all, `⌘D`
  deselects, `Delete` clears the selected pixels.
- `⌘Z` / `⇧⌘Z` undo and redo. `⌘S` saves, `⇧⌘S` saves as, `⌘O` opens.
- Keys reach the canvas only while it holds keyboard focus, so text fields keep
  their normal native editing behaviour. The canvas claims that focus as it
  opens, but only when nothing else in the window has it — before this, a fresh
  editor left the window itself as first responder and no tool key did anything
  until the canvas happened to be clicked.
- A shortcut is matched on the character the key produced, and on the physical
  key when that character is not a Latin letter. `charactersIgnoringModifiers`
  ignores modifiers, not input methods: with 2-set Korean armed, the `e` key
  reports `ㄷ`, and every tool shortcut silently stopped matching. Preferring
  the character keeps a Latin layout that moves its letters working; the ANSI
  key code is only the fallback, and it is what Korean, Japanese and Pinyin
  input methods all sit on.

The selection outline is drawn from the mask's own edges, so it stays one line
thick whichever tool built it.

## Timeline

Layer names sit in their own panel on the left; the frames start at the left
edge of the grid beside it. One vertical scroll wraps both, so their rows stay
level, and only the frame side scrolls sideways.

Rows are layers, columns are frames, and each cell is an independently editable
cel. There are three ways to reorder:

- **A whole frame**: drag its numbered header, or move the selected one a place
  at a time with the chevrons beside the frame buttons. Every layer follows.
- **A single cel**: drag the cell itself. Only that layer's cels reorder, which
  is how one layer is offset against another. The frame count does not change,
  and a drop onto a different layer is refused rather than overwriting the cel
  already there. A locked layer refuses the move.
- **A layer**: drag its name.

- **Per-frame duration**: 10 ms – 60 s. Left unset, a frame inherits the
  document FPS (1–24).
- **Frame visibility** excludes a frame from playback. It does not remove the
  frame or its pixels, which stay editable.
- **Playback**: once, loop, ping-pong (no repeated endpoints) and an inclusive
  range loop. Playback starts deterministically from the moment Play is pressed.
- **Layers**: add and delete from the buttons at the top of the name panel.
  Double-click a name to rename, toggle visibility, toggle lock, and drag to
  reorder. Return commits a new name; Escape or a click anywhere else discards
  it, because neither is a confirmation. The inspector keeps Raise and Lower
  for restacking without a drag.

  Per-layer opacity is still read, written and composited — a document that
  carries it renders exactly as before — but nothing in the editor sets it, so
  a layer drawn here is fully opaque and hidden by its visibility toggle alone.

Undo and redo restore pixels and document settings together, and clear any stale
selection. A complete mouse stroke is one Undo operation. History keeps at most
100 snapshots inside a 128 MiB budget, but never trims below 16 entries — whole
documents are snapshotted, and copy-on-write means the untouched frames are
shared, so the byte count is a large overestimate of what history really costs.

## Palette

The left strip holds everything one stroke is made of, top to bottom: the tool
grid, the palette, then the foreground and background colours. The colour pair
sits outside the strip's scroll view — it is what the canvas paints with, so it
cannot scroll out of sight.

Document-local RGBA swatches, two to a row. The dashed **+** cell at the end of
the grid appends the foreground colour; **Update** replaces one swatch with it.
Selecting works the macOS way: a plain click picks one swatch and arms it for
drawing, `⌘` adds or removes one, and `⇧` takes the range from the last
plain-clicked swatch. The trash button under the grid deletes the whole
selection at once, except that a palette is never emptied — asked to delete
every swatch, it keeps the lowest one.

Reordering is by drag, one swatch at a time, and the drop is refused unless the
palette is still the one the drag started from. **Import GPL…** above the grid
reads a GIMP palette, rejecting anything over 1 MiB or 256 colours, and
validating the header, the column count and every RGB channel. An empty import
is rejected rather than applied.

The foreground and background colours are two overlapping squares with a swap
arrow, and clicking either opens the system colour panel. They draw themselves
rather than taking a stock `NSColorWell` style: no stock style shows what is
behind a partly transparent colour, and the foreground swatch regularly is one,
so each square paints a checkerboard first. A test renders the well through
AppKit's real display path and reads the pixels back, so a swatch that stopped
drawing itself would fail there rather than in front of the user. The wells
refuse first responder, leaving the canvas its keyboard focus.

## Files and lifecycle

The editor reads and writes Piskel model version 2, including multi-layer,
multi-chunk and column-major layouts. Its own documents use `.unf`; packages
written by earlier versions still carry `source.piskel` and are still read.

Extension metadata lives in an `unfold` object inside that schema: `version`,
`frameSettings` (per-frame duration and visibility), `playbackMode`,
`playbackStart`, `playbackEnd` and `palette`. Layers carry `isVisible` and
`isLocked`. Every field is optional, so a document saved before this metadata
existed opens with defaults: inherited FPS, loop playback and the default
palette. Frame counts, durations, playback indices and palette size are all
validated before any pixels are allocated. A document that declares hidden
timeline frames now opens with them hidden, instead of being rejected.

Open reads `.unf`, PNG and JPEG. Save As writes `.unf`, PNG and GIF; GIF import
is not implemented. Native open/save panels are covered by the user-selected
read/write sandbox entitlement; no network entitlement is needed.

**Export semantics.** GIF export follows the playback sequence: the frame order,
each frame's own delay, and the looping extension, which is omitted for *once*.
The PNG sprite sheet is static and keeps one column per frame, hidden frames
included — the saved character's playback list indexes those columns, so
dropping a column would invalidate it. Saving to the library stores the playback
frame order, per-frame durations in seconds and the loop flag in the manifest,
and refuses a document whose playback range shows no frames at all. Older
packages without `frameDurations` still play at their fixed FPS.

The session fingerprints the source, sheet and manifest. A package changed or
deleted outside the editor is rejected on save, keeping the drawing available
for export. This detects changes before replacement; it is not a cross-process
filesystem lock.

Closing, opening another session and quitting the app offer Save, Cancel or
Discard when there are changes. A cancelled or failed save keeps the current
document. Missing or malformed existing sources produce an error, never a blank
replacement under the original character ID. Export does not mark the library
document saved.

Bounds: 8–512 pixels per side, 1–24 frames, 1–16 layers, and a 24 MiB ceiling on
`width × height × frames × layers × 4` bytes. Older Piskel model versions,
mismatched layer lengths, invalid PNG geometry and sources above the input
limits are rejected explicitly.

## Validation

Regression suites:

- `PixelDocumentTests`: stroke interpolation, single-click erase, fill
  boundaries, edge clipping, compositing, frame alignment, resize, flip and
  frame limits.
- `PixelEditorModelTests`: stroke Undo/Redo, shape preview replacement, pointer
  reentry, save-point tracking and selection bounds after Undo.
- `PixelAdvancedToolsTests`: clipped rectangle selection, contiguous wand, lasso
  boundary, move offset/clipping/undo, selection masking, strokes on a locked
  layer, mirrored pixel-perfect corners, gradient endpoints and alpha, spray
  bounds, brightness preserving transparent pixels, strict GPL parsing, palette
  history and ordering, and selection reset after frame or geometry changes.
- `PixelAnimationTests`: metadata and legacy-source round trips, reordered frame
  timing, once/ping-pong/range/hidden sequences, malformed metadata, and GIF and
  package duration propagation.
- `PixelEditorHelpTests`: the tooltip format and its heading split, the half
  second delay, that every tool carries a description and a shortcut that really
  selects it, and that the editor layout still builds tracking views that let
  clicks through.
- `NativePixelCanvasTests`: the canvas finds the scroll view that panning moves
  when built inside the real editor layout, the Aseprite key map selects tools,
  `⌘A`/`Escape` select and deselect, and neither Space-held nor the hand tool
  touches pixels.
- `PixelDocumentCodecTests`: asymmetric PNG orientation and alpha, source round
  trip, preview-to-sheet agreement, package reopening, legacy and multi-chunk
  layouts, invalid frame indices, geometry and truncated files.
- `EditorPackageRevisionTests`: same-size outside edits, changed sprite sheets
  and manifests, and deleted package files.

Run on macOS with the project toolchain:

```sh
swift test
swift build -c release
```

Last run on 2026-09-09, macOS 26.0 with Swift 6.3.3 (arm64): `swift test`
executed **420 tests with 0 failures**, and `swift build -c release` completed.
The macOS GitHub Actions workflow runs the same commands on push, PR or manual
dispatch; adding the workflow does not mean it has run.

`NativePixelCanvasTests` drives synthesized AppKit events, which covers the
wiring but not what the pixels look like. What the automated suite cannot see
was checked by hand on 2026-09-09, on the build in this repository:

- Space-held panning releases back into drawing, and the hand tool pans.
- GPL import replaces the palette.
- Dragging reorders a whole frame, a single cel within its own layer, and a
  layer; the chevrons move the selected frame one place.
- Zoom steps across its range, and a typed percentage lands on a real level.
- A rename survives a click outside the field, and Return still commits it.
- The two-panel timeline and the two-column tool rail lay out as intended.

Tooltips were reworked after that check and have not been hand-verified: whether
the panel appears, where it sits and how it reads is not something the suite can
see.

Worth repeating before a release, since none of it is guarded by a test:

1. Draw at the smallest and largest zoom, and check the selection outline stays
   one pixel wide at both.
2. Shrink the window to its minimum and confirm the tool rail, the layer panel
   and the frame grid all stay reachable.
3. Export a GIF whose frames have mixed durations and compare it against the
   in-editor preview.
4. Save a character to the library, reopen it, and confirm the playback order
   and per-frame timing survived the round trip.
