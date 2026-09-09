import AppKit
import XCTest
@testable import Unfold

/// Two things stopped the editor's tool keys from ever firing. Both are
/// covered here because both produce the same complaint — "the shortcuts do
/// nothing" — from opposite ends of the event path.
@MainActor
final class PixelShortcutKeyTests: XCTestCase {
    // MARK: - An input method rewrites the character

    /// `charactersIgnoringModifiers` is not the physical key: with 2-set
    /// Korean armed, the editor's own key events reported "ㄷ" for the `e`
    /// key, "ㅎ" for `g` and "ㅍ" for `v`, so nothing matched and no tool ever
    /// changed. The key codes below are the ones those events carried.
    func test_toolKeysResolveWhileAnInputMethodRewritesTheCharacter() {
        XCTAssertEqual(PixelTool.shortcut(characters: "ㄷ", keyCode: 14), .eraser)
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅎ", keyCode: 5), .fill)
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅍ", keyCode: 9), .move)
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅠ", keyCode: 11), .pencil)
    }

    func test_shiftedToolKeysResolveThroughTheSameFallback() {
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅠ", keyCode: 11, shift: true), .spray)
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅎ", keyCode: 5, shift: true), .gradient)
        XCTAssertEqual(PixelTool.shortcut(characters: "ㅕ", keyCode: 32, shift: true), .ellipse)
    }

    /// The character is still preferred, so a Latin layout that puts its
    /// letters somewhere other than ANSI keeps the shortcuts it has today.
    func test_aLatinCharacterStillWinsOverThePhysicalKey() {
        XCTAssertEqual(PixelTool.shortcut(characters: "e", keyCode: 11), .eraser)
        XCTAssertEqual(PixelTool.shortcut(characters: "E", keyCode: 11), .eraser)
    }

    func test_aKeyBoundToNoToolStaysUnbound() {
        XCTAssertNil(PixelTool.shortcut(characters: "ㅇ", keyCode: 2))
        XCTAssertNil(PixelTool.shortcut(characters: nil, keyCode: 999))
    }

    /// The command branch reads the same way, so ⌘Z survives an input method
    /// that would otherwise hand it a jamo.
    func test_commandKeysResolveThroughTheSameFallback() {
        XCTAssertEqual(PixelTool.shortcutLetter(characters: "ㅋ", keyCode: 6), "z")
        XCTAssertEqual(PixelTool.shortcutLetter(characters: "ㅁ", keyCode: 0), "a")
        XCTAssertEqual(PixelTool.shortcutLetter(characters: "ㅇ", keyCode: 2), "d")
    }

    // MARK: - Nothing holds keyboard focus

    private func window() -> NSWindow {
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 300, height: 300),
                              styleMask: [.titled], backing: .buffered, defer: true)
        window.contentView = NSView(frame: NSRect(x: 0, y: 0, width: 300, height: 300))
        return window
    }

    /// A freshly opened editor left the window itself as first responder, so
    /// every tool key went nowhere until the canvas happened to be clicked.
    func test_theCanvasTakesKeyboardFocusWhenNothingElseHoldsIt() {
        let window = window()
        let canvas = PixelCanvasView(model: PixelEditorModel(document: PixelDocument(width: 8, height: 8)))
        canvas.frame = NSRect(x: 0, y: 0, width: 200, height: 200)
        window.contentView?.addSubview(canvas)
        XCTAssertTrue(window.firstResponder === canvas,
            "the canvas should claim focus so its shortcuts work without a click first")
    }

    /// Claiming focus must not mean taking it: the zoom field is in the same
    /// window, and typing a percentage into it has to keep working.
    func test_theCanvasLeavesFocusAloneWhenAFieldAlreadyHasIt() {
        let window = window()
        let field = NSTextField(frame: NSRect(x: 0, y: 220, width: 100, height: 24))
        window.contentView?.addSubview(field)
        XCTAssertTrue(window.makeFirstResponder(field))
        let canvas = PixelCanvasView(model: PixelEditorModel(document: PixelDocument(width: 8, height: 8)))
        canvas.frame = NSRect(x: 0, y: 0, width: 200, height: 200)
        window.contentView?.addSubview(canvas)
        XCTAssertFalse(window.firstResponder === canvas,
            "a field that already had focus should keep it")
    }
}
