# Native character editor expansion

Approved in chat on 2026-09-09. Extend the existing SwiftUI/AppKit editor.

- Timeline rows are layers, columns are shared animation frames, cells are independently editable pixel buffers. Reorder columns and layer rows by drag and drop. Frame visibility excludes a frame from playback, not its editable source. Duration is milliseconds per frame, falling back to document FPS.
- Playback: once, loop, ping-pong (no repeated endpoints), selected inclusive range loop. Start deterministically when Play is pressed. Preserve playback in GIF and character library outputs.
- Layers: inline double-click rename committed once, independent visibility/opacity, lock prevents pixel modifications, drag order. Undo/redo retains content and document settings.
- Selection: contiguous magic wand, rectangle and lasso masks; drag selected pixels with move tool, limit painting to selection. Hand pans viewport; Space temporarily pans.
- Drawing: horizontal/vertical symmetry, brightness brush with darken option, spray, two-colour linear gradient, optional one-pixel pixel-perfect pencil.
- Palette: document-local RGBA swatches, add/update/delete/reorder, strict bounded GIMP GPL import.
- Cmd/Ctrl+wheel zooms the viewport without resampling. Map supported tools/actions to Aseprite defaults; preserve text editing shortcuts.
- Store optional extension metadata in the existing Piskel-v2-shaped .unf schema; defaults allow old documents to load. Validate indices, durations, palettes and allocation bounds. Retain atomic package writes and external-change detection.
- Verify model drawing/selection/history, playback timing, malformed imports, source round trips, GIF and package duration propagation; run complete Swift suite and release build. Native drag/pan/focus visual checks require live UI evidence or an explicit remaining-QA note.

## Rulings

The current checkout is already a feature branch (`major-MacOS`), clean before this task. Work stays in the user-selected checkout so changes are immediately reviewable. No automatic commit, merge or push.
