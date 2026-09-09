import AppKit
import SwiftUI

@MainActor
struct NativePixelCanvas: NSViewRepresentable {
    @ObservedObject var model: PixelEditorModel
    func makeNSView(context: Context) -> PixelCanvasView { PixelCanvasView(model: model) }
    func updateNSView(_ view: PixelCanvasView, context: Context) { view.refresh() }
}

/// The drawing surface. AppKit owns it because SwiftUI has no equivalent of
/// per-pixel drawing, cursor rects, scroll-wheel modifiers or key handling
/// that stays out of the way of text fields.
@MainActor
final class PixelCanvasView: NSView {
    let model: PixelEditorModel
    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    /// Space held down pans without changing the selected tool, like Aseprite.
    private var spacePanning = false
    /// Set at mouse-down when the drag is moving the viewport rather than
    /// pixels. Kept separate from `panAnchor` so a missing scroll view makes the
    /// pan do nothing rather than fall through to the drawing tools.
    private var isPanning = false
    private var panAnchor: NSPoint?
    /// Wheel notches are fractional on trackpads; zoom one step per whole notch.
    private var zoomAccumulator: CGFloat = 0

    init(model: PixelEditorModel) {
        self.model = model
        super.init(frame: .zero)
        setAccessibilityElement(true)
        setAccessibilityLabel(
            "Pixel canvas. Pencil B, spray shift B, eraser E, fill G, gradient shift G, "
                + "eyedropper I, line L, rectangle U, ellipse shift U, rectangle selection M, "
                + "lasso Q, magic wand W, move V, hand H. Space pans, Command wheel zooms, "
                + "Return plays, Escape deselects.")
    }
    required init?(coder: NSCoder) { return nil }

    /// SwiftUI re-renders on every model change; the tool decides the cursor.
    func refresh() {
        needsDisplay = true
        window?.invalidateCursorRects(for: self)
    }

    // MARK: - Drawing

    override func draw(_ dirtyRect: NSRect) {
        let doc = model.document
        let scale = CGFloat(model.zoom)
        let clip = dirtyRect.intersection(bounds)
        guard !clip.isEmpty else { return }
        let minX = max(0, Int(clip.minX / scale)), maxX = min(doc.width, Int(ceil(clip.maxX / scale)))
        let minY = max(0, Int(clip.minY / scale)), maxY = min(doc.height, Int(ceil(clip.maxY / scale)))
        guard minX < maxX, minY < maxY else { return }
        NSGraphicsContext.current?.shouldAntialias = false
        for y in minY..<maxY {
            for x in minX..<maxX {
                NSColor(white: ((x / 4 + y / 4) % 2 == 0) ? 0.22 : 0.28, alpha: 1).setFill()
                NSRect(x: CGFloat(x) * scale, y: CGFloat(y) * scale, width: scale, height: scale).fill()
            }
        }
        func paint(_ frame: PixelFrame, opacity: CGFloat) {
            for y in minY..<maxY {
                for x in minX..<maxX {
                    let pixel = frame.pixels[y * doc.width + x]
                    guard pixel & 255 != 0 else { continue }
                    NSColor(srgbRed: CGFloat((pixel >> 24) & 255) / 255, green: CGFloat((pixel >> 16) & 255) / 255,
                        blue: CGFloat((pixel >> 8) & 255) / 255, alpha: CGFloat(pixel & 255) / 255 * opacity).setFill()
                    NSRect(x: CGFloat(x) * scale, y: CGFloat(y) * scale, width: scale, height: scale).fill(using: .sourceOver)
                }
            }
        }
        if model.onionSkin && model.selectedFrame > 0 { paint(doc.compositedFrame(at: model.selectedFrame - 1), opacity: 0.25) }
        paint(doc.compositedFrame(at: model.selectedFrame), opacity: 1)
        if model.showGrid && model.zoom >= 6 {
            NSColor(white: 0, alpha: 0.15).setFill()
            for x in minX...maxX { NSRect(x: CGFloat(x) * scale, y: clip.minY, width: 1, height: clip.height).fill() }
            for y in minY...maxY { NSRect(x: clip.minX, y: CGFloat(y) * scale, width: clip.width, height: 1).fill() }
        }
        drawSelectionOutline(width: doc.width, scale: scale, minX: minX, maxX: maxX, minY: minY, maxY: maxY)
    }

    /// Traces only the edges where a selected pixel meets an unselected one, so
    /// the outline stays one line thick however the mask was built.
    private func drawSelectionOutline(
        width: Int, scale: CGFloat, minX: Int, maxX: Int, minY: Int, maxY: Int
    ) {
        guard let selection = model.selection, !selection.isEmpty else { return }
        let path = NSBezierPath()
        for y in minY..<maxY {
            for x in minX..<maxX {
                guard selection.contains(y * width + x) else { continue }
                let left = CGFloat(x) * scale, top = CGFloat(y) * scale
                let right = left + scale, bottom = top + scale
                if x == 0 || !selection.contains(y * width + x - 1) {
                    path.move(to: NSPoint(x: left, y: top))
                    path.line(to: NSPoint(x: left, y: bottom))
                }
                if x == width - 1 || !selection.contains(y * width + x + 1) {
                    path.move(to: NSPoint(x: right, y: top))
                    path.line(to: NSPoint(x: right, y: bottom))
                }
                if y == 0 || !selection.contains((y - 1) * width + x) {
                    path.move(to: NSPoint(x: left, y: top))
                    path.line(to: NSPoint(x: right, y: top))
                }
                if !selection.contains((y + 1) * width + x) {
                    path.move(to: NSPoint(x: left, y: bottom))
                    path.line(to: NSPoint(x: right, y: bottom))
                }
            }
        }
        guard !path.isEmpty else { return }
        path.lineWidth = 1
        NSColor.white.setStroke()
        path.stroke()
        NSColor.black.setStroke()
        path.setLineDash([3, 3], count: 2, phase: 0)
        path.stroke()
    }

    // MARK: - Pointer

    private func point(_ event: NSEvent) -> PixelPoint {
        let location = convert(event.locationInWindow, from: nil)
        return PixelPoint(x: Int(floor(location.x / CGFloat(model.zoom))), y: Int(floor(location.y / CGFloat(model.zoom))))
    }

    private var isPanningGesture: Bool { spacePanning || model.tool == .hand }

    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
        if isPanningGesture { beginPan(event) } else { model.beginStroke(at: point(event)) }
    }
    override func mouseDragged(with event: NSEvent) {
        if isPanning { continuePan(event) } else { model.continueStroke(at: point(event)) }
    }
    override func mouseUp(with event: NSEvent) {
        if isPanning { endPan() } else { model.endStroke() }
    }
    /// Middle-drag pans regardless of the active tool, matching Aseprite.
    override func otherMouseDown(with event: NSEvent) { beginPan(event) }
    override func otherMouseDragged(with event: NSEvent) { continuePan(event) }
    override func otherMouseUp(with event: NSEvent) { endPan() }

    /// Claims keyboard focus on arrival, but only when the window has none of
    /// its own — a field that already holds it keeps it, so typing a zoom
    /// percentage still behaves like any other text field.
    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        guard let window, window.firstResponder === window else { return }
        window.makeFirstResponder(self)
    }

    override func resignFirstResponder() -> Bool {
        model.endStroke()
        endPan()
        spacePanning = false
        refresh()
        return super.resignFirstResponder()
    }

    override func resetCursorRects() {
        addCursorRect(bounds, cursor: isPanningGesture ? (isPanning ? .closedHand : .openHand) : .crosshair)
    }

    // MARK: - Panning and zooming

    private func beginPan(_ event: NSEvent) {
        model.endStroke()
        isPanning = true
        panAnchor = enclosingScrollView?.contentView.convert(event.locationInWindow, from: nil)
        window?.invalidateCursorRects(for: self)
    }

    /// Keeps the document point grabbed at mouse-down under the cursor. Each
    /// event re-reads the position in clip coordinates, so the pan self-corrects
    /// once the clip view clamps at an edge.
    private func continuePan(_ event: NSEvent) {
        guard let anchor = panAnchor, let scroll = enclosingScrollView else { return }
        let clip = scroll.contentView
        let current = clip.convert(event.locationInWindow, from: nil)
        var origin = clip.bounds.origin
        origin.x -= current.x - anchor.x
        origin.y -= current.y - anchor.y
        clip.scroll(to: origin)
        scroll.reflectScrolledClipView(clip)
    }

    private func endPan() {
        guard isPanning else { return }
        isPanning = false
        panAnchor = nil
        window?.invalidateCursorRects(for: self)
    }

    /// Command or Control plus wheel zooms the viewport. Everything else is left
    /// to the enclosing scroll view so ordinary scrolling still works.
    override func scrollWheel(with event: NSEvent) {
        let flags = event.modifierFlags
        guard flags.contains(.command) || flags.contains(.control) else {
            super.scrollWheel(with: event)
            return
        }
        if event.phase.contains(.began) || event.momentumPhase.contains(.began) { zoomAccumulator = 0 }
        zoomAccumulator += event.hasPreciseScrollingDeltas ? event.scrollingDeltaY / 24 : event.scrollingDeltaY
        while zoomAccumulator >= 1 { zoomAccumulator -= 1; model.zoomIn() }
        while zoomAccumulator <= -1 { zoomAccumulator += 1; model.zoomOut() }
    }

    // MARK: - Keys

    override func keyDown(with event: NSEvent) {
        let flags = event.modifierFlags
        if flags.contains(.command) {
            switch PixelTool.shortcutLetter(characters: event.charactersIgnoringModifiers,
                                            keyCode: event.keyCode) ?? "" {
            case "z":
                if flags.contains(.shift) { model.redo() } else { model.undo() }
            case "a": model.selectAll()
            case "d": model.clearSelection()
            default: super.keyDown(with: event)
            }
            return
        }
        switch event.keyCode {
        case 49:  // Space: hold to pan.
            if !spacePanning {
                spacePanning = true
                window?.invalidateCursorRects(for: self)
            }
            return
        case 36, 76:  // Return / keypad Enter: play, as in Aseprite.
            model.togglePlayback(at: Date().timeIntervalSinceReferenceDate)
            return
        case 51, 117:  // Delete / forward delete: clear the selected pixels.
            model.clearSelectedPixels()
            return
        case 53:  // Escape: deselect.
            model.clearSelection()
            return
        default: break
        }
        if let tool = PixelTool.shortcut(characters: event.charactersIgnoringModifiers,
                                         keyCode: event.keyCode, shift: flags.contains(.shift)) {
            model.endStroke()
            model.tool = tool
            window?.invalidateCursorRects(for: self)
            return
        }
        super.keyDown(with: event)
    }

    override func keyUp(with event: NSEvent) {
        guard event.keyCode == 49 else {
            super.keyUp(with: event)
            return
        }
        spacePanning = false
        endPan()
        window?.invalidateCursorRects(for: self)
    }
}
