# Native pixel editor

The character editor now opens a SwiftUI window with an AppKit drawing surface.
It does not instantiate a WKWebView, inject JavaScript, run Piskel timers, or load
web resources. The vendored Piskel files and notices remain in the repository
for reference; `Package.swift` excludes `Resources/Editor` from the app target.

## Editing

- Pencil, eraser, flood fill, eyedropper, line, rectangle and ellipse.
- Brushes from 1–8 pixels, color/alpha picker, palette, grid, zoom and scrolling.
- Previous-frame onion skin and a separate animation preview at 1–24 FPS.
- Add, duplicate, delete and reorder frames; add, delete, name, reorder and set
  opacity on layers. Opacity zero hides a layer without deleting its pixels.
- Flip or clear the selected layer/frame. Resize all frames with top-left
  anchoring; shrinking requires confirmation and can be undone.
- A complete mouse stroke is one Undo operation. Shape previews replace their
  previous preview. History is capped at 100 snapshots and approximately 32 MiB
  of pixel data per retained undo stack.

Canvas shortcuts (when the drawing surface has keyboard focus): **B** pencil,
**E** eraser, **F** fill, **I** eyedropper, **L** line, **R** rectangle, **O** ellipse,
**Space** play/pause. **Command-S** saves; **Command-Z / Shift-Command-Z** undo/redo.
Text fields retain their normal native text editing behavior.

## Files and lifecycle

The native editor reads/writes Piskel model version 2, including multi-layer,
multi-chunk, column-major layouts and the older unchunked version-2 PNG layout.
Editable sources still use `source.piskel`. The app's package writer retains
staging, package validation, and replacement of the existing character ID.
Repeated saves update that ID and leave the editor open.

The session fingerprints the source, sheet and manifest. A package changed or
deleted outside the editor is rejected on save, keeping the drawing available
for export. This detects changes before replacement; it is not a cross-process
filesystem lock.

Open imports a Piskel document or a single PNG into a new character session.
Export writes a Piskel source or a composited horizontal PNG sprite sheet.
PNG import is a single image/frame, not automatic sprite-sheet slicing.
Native open/save panels are covered by the user-selected read/write sandbox
entitlement; no network entitlement is needed.

Closing, opening another session and quitting the app offer Save, Cancel or
Discard when there are changes. A cancelled/failed save keeps the current
document. Missing or malformed existing sources produce an error, never a blank
replacement under the original character ID. Export does not mark the library
document saved.

Supported bounds are 1–128 pixels per side, 1–24 frames and 1–16 layers. Older
Piskel model versions, hidden timeline frames, mismatched layer lengths, invalid
PNG geometry and sources above the input limits are rejected explicitly.
This is not complete Piskel feature parity: selection/move tools, GIF import or
export, and Piskel's other advanced transformations are not implemented.

## Validation

Regression suites added:

- `PixelDocumentTests`: stroke interpolation, single-click erase, fill boundaries,
  edge clipping, compositing, frame alignment, resize, flip and frame limits.
- `PixelEditorModelTests`: stroke Undo/Redo, shape preview replacement, pointer
  reentry, save-point tracking and selection bounds after Undo.
- `PixelDocumentCodecTests`: asymmetric PNG orientation/alpha, source round trip,
  preview-to-sheet agreement, package reopening, legacy and multi-chunk layouts,
  invalid frame indices, geometry and truncated files.
- `EditorPackageRevisionTests`: same-size outside edits, changed sprite sheets
  and manifests, and deleted package files.

Run on macOS with the project toolchain:

```sh
swift test
swift build -c release
```

The macOS GitHub Actions workflow runs those commands on push/PR or manual
dispatch. Adding the workflow does not mean it has run.

Implementation environment: Windows, without Swift/Xcode. `swift test` could not
start because the Swift executable is unavailable. Native compilation, XCTest
results, rendering and signed sandbox behavior are **not verified here**.

Before release, on a Mac:

1. Draw and single-click erase; drag quickly and leave/reenter the canvas.
2. Undo/redo strokes, shapes, resize and frame/layer changes.
3. Open a real multi-layer Piskel character and compare colors, frame order and
   opacity to its original. Export/reopen an asymmetric PNG to check orientation.
4. Save twice and confirm only one library character exists; close/reopen it.
5. Cancel name/close dialogs; try a malformed source and a failed filesystem save.
6. Quit with unsaved work and verify Cancel keeps the app and drawing alive.
7. Test import/export in the signed sandboxed app and preview at maximum bounds.
