import AppKit
import SwiftUI
import XCTest

@testable import Unfold

/// Covers the canvas wiring that only exists in AppKit: the scroll view panning
/// reaches for, the Aseprite key map, and the Space-to-pan override. The model
/// tests cover what each tool then does to the pixels.
@MainActor
final class NativePixelCanvasTests: XCTestCase {
    private func canvas(_ model: PixelEditorModel, side: Int = 16) -> PixelCanvasView {
        let view = PixelCanvasView(model: model)
        view.frame = NSRect(x: 0, y: 0, width: side * model.zoom, height: side * model.zoom)
        let window = NSWindow(
            contentRect: view.frame, styleMask: [.titled], backing: .buffered, defer: true)
        window.contentView?.addSubview(view)
        return view
    }

    private func key(_ characters: String, keyCode: UInt16, flags: NSEvent.ModifierFlags = [])
        -> NSEvent
    {
        NSEvent.keyEvent(
            with: .keyDown, location: .zero, modifierFlags: flags, timestamp: 0, windowNumber: 0,
            context: nil, characters: characters, charactersIgnoringModifiers: characters,
            isARepeat: false, keyCode: keyCode)!
    }

    private func mouse(_ type: NSEvent.EventType, at point: NSPoint) -> NSEvent {
        NSEvent.mouseEvent(
            with: type, location: point, modifierFlags: [], timestamp: 0, windowNumber: 0,
            context: nil, eventNumber: 0, clickCount: 1, pressure: 1)!
    }

    /// Panning moves the enclosing clip view. If SwiftUI ever stops backing its
    /// ScrollView with an NSScrollView, the hand tool goes silently dead, so
    /// assert the canvas can still find one from inside the real editor layout.
    func testCanvasInEditorLayoutFindsTheScrollViewPanningMoves() throws {
        let model = PixelEditorModel(document: PixelDocument(width: 16, height: 16))
        let host = NSHostingView(
            rootView: PixelEditorView(
                model: model, saveToLibrary: {}, openDocument: {}, save: {}, saveAs: {}))
        host.frame = NSRect(x: 0, y: 0, width: 1120, height: 780)
        let window = NSWindow(
            contentRect: host.frame, styleMask: [.titled], backing: .buffered, defer: true)
        window.contentView = host
        host.layoutSubtreeIfNeeded()
        host.displayIfNeeded()

        func find(_ view: NSView) -> PixelCanvasView? {
            if let canvas = view as? PixelCanvasView { return canvas }
            for child in view.subviews { if let found = find(child) { return found } }
            return nil
        }
        let canvas = try XCTUnwrap(find(host), "PixelEditorView no longer builds a pixel canvas")
        XCTAssertNotNil(
            canvas.enclosingScrollView, "hand and Space panning have no scroll view to move")
    }

    func testAsepriteKeysSelectToolsOnTheCanvas() {
        let model = PixelEditorModel(document: PixelDocument(width: 16, height: 16))
        let view = canvas(model)
        let expected: [(String, UInt16, NSEvent.ModifierFlags, PixelTool)] = [
            ("q", 12, [], .lassoSelection),
            ("m", 46, [], .rectangleSelection),
            ("w", 13, [], .magicWand),
            ("v", 9, [], .move),
            ("h", 4, [], .hand),
            ("u", 32, [], .rectangle),
            ("U", 32, .shift, .ellipse),
            ("B", 11, .shift, .spray),
            ("G", 5, .shift, .gradient),
            ("g", 5, [], .fill),
            ("b", 11, [], .pencil),
        ]
        for (characters, code, flags, tool) in expected {
            view.keyDown(with: key(characters, keyCode: code, flags: flags))
            XCTAssertEqual(model.tool, tool, "\(characters) should select \(tool.title)")
        }
    }

    func testEscapeDeselectsAndCommandAselectsEverything() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let view = canvas(model, side: 8)
        view.keyDown(with: key("a", keyCode: 0, flags: .command))
        XCTAssertEqual(model.selection?.count, 64)
        view.keyDown(with: key("\u{1B}", keyCode: 53))
        XCTAssertNil(model.selection)
    }

    /// Space pans without changing the tool, so a drag while it is held must not
    /// reach the pencil.
    func testSpaceHeldPansInsteadOfDrawingAndReleasingRestoresDrawing() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let view = canvas(model, side: 8)
        let clean = model.document

        view.keyDown(with: key(" ", keyCode: 49))
        view.mouseDown(with: mouse(.leftMouseDown, at: NSPoint(x: 4, y: 4)))
        view.mouseDragged(with: mouse(.leftMouseDragged, at: NSPoint(x: 24, y: 24)))
        view.mouseUp(with: mouse(.leftMouseUp, at: NSPoint(x: 24, y: 24)))
        XCTAssertEqual(model.document, clean, "Space held should pan, not draw")
        XCTAssertEqual(model.tool, .pencil, "Space must not change the selected tool")

        view.keyUp(
            with: NSEvent.keyEvent(
                with: .keyUp, location: .zero, modifierFlags: [], timestamp: 0, windowNumber: 0,
                context: nil, characters: " ", charactersIgnoringModifiers: " ", isARepeat: false,
                keyCode: 49)!)
        view.mouseDown(with: mouse(.leftMouseDown, at: NSPoint(x: 4, y: 4)))
        view.mouseUp(with: mouse(.leftMouseUp, at: NSPoint(x: 4, y: 4)))
        XCTAssertNotEqual(model.document, clean, "releasing Space should restore drawing")
    }

    /// The hand tool must not modify pixels either.
    func testHandToolDragLeavesPixelsUntouched() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let view = canvas(model, side: 8)
        model.tool = .hand
        let clean = model.document
        view.mouseDown(with: mouse(.leftMouseDown, at: NSPoint(x: 4, y: 4)))
        view.mouseDragged(with: mouse(.leftMouseDragged, at: NSPoint(x: 30, y: 30)))
        view.mouseUp(with: mouse(.leftMouseUp, at: NSPoint(x: 30, y: 30)))
        XCTAssertEqual(model.document, clean)
    }
}
