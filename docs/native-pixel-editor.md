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

A selection restricts every painting tool to its mask. An empty selection paints
nothing; no selection paints everywhere. Selection tools work on a locked layer;
tools that modify pixels do not.

## Canvas

- `Space` held pans without changing the tool. Middle-drag pans too.
- `⌘`/`⌃` + scroll wheel zooms the viewport. Zoom resamples nothing.
- `Return` plays and pauses. `Escape` deselects, `⌘A` selects all, `⌘D`
  deselects, `Delete` clears the selected pixels.
- `⌘Z` / `⇧⌘Z` undo and redo. `⌘S` saves, `⇧⌘S` saves as, `⌘O` opens.
- Keys reach the canvas only while it holds keyboard focus, so text fields keep
  their normal native editing behaviour.

The selection outline is drawn from the mask's own edges, so it stays one line
thick whichever tool built it.

## Timeline

Rows are layers, columns are frames, and each cell is an independently editable
pixel buffer. Drag a column header to reorder frames; drag a layer header to
reorder layers.

- **Per-frame duration**: 10 ms – 60 s. Left unset, a frame inherits the
  document FPS (1–24).
- **Frame visibility** excludes a frame from playback. It does not remove the
  frame or its pixels, which stay editable.
- **Playback**: once, loop, ping-pong (no repeated endpoints) and an inclusive
  range loop. Playback starts deterministically from the moment Play is pressed.
- **Layers**: double-click the name to rename (committed once, on blur or
  Return), toggle visibility, toggle lock, set opacity, and drag to reorder.
  Visibility and opacity are separate: opacity zero also hides a layer, but
  visibility does not disturb the opacity value.

Undo and redo restore pixels and document settings together, and clear any stale
selection. A complete mouse stroke is one Undo operation. History keeps at most
100 snapshots inside a 128 MiB budget, but never trims below 16 entries — whole
documents are snapshotted, and copy-on-write means the untouched frames are
shared, so the byte count is a large overestimate of what history really costs.

## Palette

Document-local RGBA swatches: add, update, delete and reorder. The last swatch
cannot be deleted. **Import GPL…** reads a GIMP palette, rejecting anything over
1 MiB or 256 colours, and validating the header, the column count and every RGB
channel. An empty import is rejected rather than applied.

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
executed **406 tests with 0 failures**, and `swift build -c release` completed.
The macOS GitHub Actions workflow runs the same commands on push, PR or manual
dispatch; adding the workflow does not mean it has run.

`NativePixelCanvasTests` drives synthesized AppKit events, which covers the
wiring but not what the pixels look like. Still unverified by any automated
test, and worth a pass by hand before release:

1. Drag a frame and a layer header and confirm the drop lands where the
   insertion point showed.
2. Hold Space mid-stroke, pan, release, and confirm drawing resumes.
3. `⌘`-scroll at both zoom limits, and check the selection outline stays one
   pixel wide at every zoom.
4. Double-click a layer name, press Escape, and confirm the old name survives.
5. Import a real `.gpl` palette and export a GIF whose frames have mixed
   durations; compare the result against the in-editor preview.
