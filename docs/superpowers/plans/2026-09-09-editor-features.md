# Native Editor Features Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for bounded tool and UI tasks. The parent owns storage/playback integration and final verification.

**Goal:** Implement the approved character editor tools, timeline, layers and options.
**Architecture:** PixelDocument owns persisted state. PixelEditorModel owns transient interaction and Undo; pure helpers calculate masks, palette parsing and playback. Native SwiftUI/AppKit views expose the model. Codec, GIF and package playback share durations and sequence.
**Tech Stack:** Swift 5.9, SwiftUI, AppKit, XCTest, ImageIO; macOS 13 minimum.
**Spec:** docs/superpowers/specs/2026-09-09-editor-features-design.md

## Global Constraints

- Retain old source compatibility, atomic package writes, allocation ceilings, native file panels and text-field shortcuts.
- No dependency additions. Current feature checkout; no automatic commits or pushes.
- Document persistent interfaces: `frameSettings: [PixelFrameSettings]`, settings fields `durationMS: Int?`, `isVisible: Bool`; `settings(at:)`, `duration(at:) -> TimeInterval`; layer fields `isVisible`, `isLocked`; `palette: [UInt32]`; `playbackMode: PixelPlaybackMode` (.once,.loop,.pingPong,.range); `playbackStart`, `playbackEnd` (inclusive).

### Task 1: Document, playback and persistence (parent)
- [ ] Add failing tests for metadata/source round trip, reordered frame timing, once/ping-pong/range/hidden sequence, malformed metadata, GIF and package duration propagation.
- [ ] Add PixelAnimation.swift with optional frame timing defaults and shared playback sequence; preserve metadata on insertion/deletion/move.
- [ ] Persist validated optional fields in PixelDocumentCodec; retain old source defaults and hidden-frame import.
- [ ] Extend sprite animation manifest with optional ordered durations and use them in clip loader. Save approved sequence/loop in package, keeping sheet columns original.
- [ ] GIF uses shared sequence, duration and loop. PNG stays a static visible-frame sheet.
- [ ] Run focused XCTest suites.

### Task 2: Selection, drawing, model and palettes (worker)
- [ ] Write failing behavior tests for selection boundaries and movement, lock safety, mirrored/pixel-perfect strokes, gradient endpoints/alpha, brightness, spray boundaries, GPL parsing and history.
- [ ] Own PixelEditorModel.swift, new PixelSelection.swift, PixelDrawing.swift, PixelPalette.swift, PixelTool.swift (extract enum), new model/tool tests. Parent owns PixelDocument.swift; use parent-provided properties above.
- [ ] Implement model APIs for document edits and transient selection/stroke interactions, preserving one-stroke undo and bounded buffers.
- [ ] Add deterministic elapsed-time preview APIs and Aseprite key mapping consumable by UI.
- [ ] Run focused tests and report API list and verified results.

### Task 3: Native editor UI (worker, after Task 2 interface settles)
- [ ] Own PixelEditorView.swift and extracted native canvas/timeline/inspector views only.
- [ ] Build layer/frame cell grid with drag/drop, frame duration, visibility, playback mode/range; keep small-window scrolling usable.
- [ ] Expose selection/tools, foreground/background colours, options, palette edit/reorder/GPL native file panel.
- [ ] Add inline layer rename, lock, visibility and drag order. Add selection outline, hand/Space panning, wheel zoom and Aseprite keys in native canvas.
- [ ] Run build and focused model tests; report live UI coverage separately.

### Task 4: Integration and review (parent + read-only reviewer)
- [ ] Verify each approved requirement in actual changes; resolve review findings.
- [ ] Run `swift test` and `swift build -c release`; inspect git diff and new files.
- [ ] Update native editor documentation with controls, storage/export semantics and real validation evidence.
