import XCTest

@testable import Unfold

final class PixelAdvancedToolsTests: XCTestCase {
    func testSelectionBoundariesAndWand() {
        XCTAssertEqual(
            PixelSelection.rectangle(
                from: .init(x: -2, y: 0), to: .init(x: 1, y: 1), width: 3, height: 3),
            Set([0, 1, 3, 4]))
        XCTAssertEqual(
            PixelSelection.wand(at: .init(x: 0, y: 0), pixels: [1, 0, 1, 1], width: 2, height: 2),
            Set([0, 2, 3]))
    }
    func testGradientAlphaAndBrightness() {
        XCTAssertEqual(PixelDrawing.interpolate(0x1020_3000, 0x90A0_B0FF, fraction: 0), 0x1020_3000)
        XCTAssertEqual(PixelDrawing.interpolate(0x1020_3000, 0x90A0_B0FF, fraction: 1), 0x90A0_B0FF)
        XCTAssertEqual(PixelDrawing.brightness(0xF000_1080, darken: false), 0xFF1A_2A80)
    }
    func testStrictPalette() throws {
        XCTAssertEqual(
            try PixelPalette.parseGPL(
                "GIMP Palette\nName: Sample\nColumns: 2\n# colors\n255 0 128 Pink\n"), [0xFF00_80FF]
        )
        XCTAssertThrowsError(try PixelPalette.parseGPL("GIMP Palette\n256 0 0"))
        XCTAssertThrowsError(try PixelPalette.parseGPL("255 0 0"))
    }
    @MainActor func testMaskLockAndStrokeUndo() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.selection = [0]
        model.beginStroke(at: .init(x: 0, y: 0))
        model.continueStroke(at: .init(x: 4, y: 0))
        model.endStroke()
        XCTAssertNotEqual(model.document.layers[0].frames[0].pixels[0], 0)
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[1], 0)
        model.undo()
        XCTAssertTrue(model.document.layers[0].frames[0].pixels.allSatisfy { $0 == 0 })
        model.toggleLayerLock(0)
        model.beginStroke(at: .init(x: 0, y: 0))
        model.endStroke()
        XCTAssertTrue(model.document.layers[0].frames[0].pixels.allSatisfy { $0 == 0 })
    }
    @MainActor func testPixelPerfectAndSymmetry() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.pixelPerfect = true
        model.symmetryX = true
        model.beginStroke(at: .init(x: 0, y: 0))
        model.continueStroke(at: .init(x: 1, y: 0))
        model.continueStroke(at: .init(x: 1, y: 1))
        model.endStroke()
        let pixels = model.document.layers[0].frames[0].pixels
        XCTAssertEqual(pixels[1], 0)
        XCTAssertEqual(pixels[6], 0)
        XCTAssertNotEqual(pixels[0], 0)
        XCTAssertNotEqual(pixels[7], 0)
        XCTAssertNotEqual(pixels[9], 0)
        XCTAssertNotEqual(pixels[14], 0)
        model.undo()
        XCTAssertTrue(model.document.layers[0].frames[0].pixels.allSatisfy { $0 == 0 })
    }
    @MainActor func testMoveSelectionClipsAndUndoes() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.layers[0].frames[0].pixels[0] = 0xFF00_00FF
        doc.layers[0].frames[0].pixels[1] = 0x00FF_00FF
        let model = PixelEditorModel(document: doc)
        model.selection = [0, 1]
        model.tool = .move
        model.beginStroke(at: .init(x: 0, y: 0))
        model.continueStroke(at: .init(x: 7, y: 1))
        model.endStroke()
        XCTAssertEqual(model.selection, [15])
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[15], 0xFF00_00FF)
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[0], 0)
        model.undo()
        XCTAssertEqual(model.document, doc)
        XCTAssertNil(model.selection)
    }
    func testLassoIncludesBoundary() {
        let mask = PixelSelection.lasso(
            [.init(x: 0, y: 0), .init(x: 3, y: 0), .init(x: 0, y: 3)], width: 8, height: 8)
        XCTAssertTrue(mask.contains(0))
        XCTAssertTrue(mask.contains(3))
        XCTAssertTrue(mask.contains(24))
        XCTAssertTrue(mask.contains(9))
        XCTAssertFalse(mask.contains(27))
    }
    @MainActor func testGradientMaskAndSprayBounds() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.tool = .gradient
        model.color = 0xFF00_0080
        model.backgroundColor = 0x0000_FF00
        model.selection = [0, 1, 2]
        model.beginStroke(at: .init(x: 0, y: 0))
        model.continueStroke(at: .init(x: 2, y: 0))
        model.endStroke()
        let pixels = model.document.layers[0].frames[0].pixels
        XCTAssertEqual(pixels[0], 0xFF00_0080)
        XCTAssertEqual(pixels[2], 0x0000_FF00)
        XCTAssertEqual(pixels[1], 0x8000_8040)
        XCTAssertEqual(pixels[3], 0)
        model.tool = .spray
        model.brushSize = 8
        model.beginStroke(at: .init(x: 0, y: 0))
        model.endStroke()
        XCTAssertEqual(model.document.layers[0].frames[0].pixels.count, 64)
        XCTAssertTrue(model.document.layers[0].frames[0].pixels.dropFirst(3).allSatisfy { $0 == 0 })
    }
    @MainActor func testPaletteHistoryAndSelectionReset() throws {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let original = model.document.palette
        try model.importGPL("GIMP Palette\n1 2 3 A\n4 5 6 B")
        model.movePaletteColor(from: 0, to: 1)
        XCTAssertEqual(model.document.palette, [0x0405_06FF, 0x0102_03FF])
        model.undo()
        model.undo()
        XCTAssertEqual(model.document.palette, original)
        model.selection = [0]
        model.selectFrame(0)
        XCTAssertNil(model.selection)
        model.selection = [0]
        model.change { $0.resize(width: 16, height: 8) }
        XCTAssertNil(model.selection)
    }
    @MainActor func testPreviewUsesElapsedClock() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.insertFrame(after: 0, duplicate: false)
        doc.frameSettings = [.init(durationMS: 100), .init(durationMS: 200)]
        doc.playbackMode = .once
        let model = PixelEditorModel(document: doc)
        model.startPlayback(at: 10)
        XCTAssertEqual(model.selectedFrame, 0)
        model.tickPlayback(at: 10.11)
        XCTAssertEqual(model.selectedFrame, 1)
        model.tickPlayback(at: 10.31)
        XCTAssertFalse(model.isPlaying)
        model.startPlayback(at: 30)
        XCTAssertEqual(model.selectedFrame, 0)
    }

    @MainActor func testFloodFillCannotCrossSelectionGap() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.selection = [0, 2]
        model.tool = .fill
        model.beginStroke(at: .init(x: 0, y: 0))
        model.endStroke()
        XCTAssertNotEqual(model.document.layers[0].frames[0].pixels[0], 0)
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[2], 0)
    }
    @MainActor func testBrightnessIgnoresTransparentPixels() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.tool = .brightness
        model.beginStroke(at: .init(x: 0, y: 0))
        model.endStroke()
        XCTAssertFalse(model.isDirty)
        XCTAssertFalse(model.canUndo)
    }

    func testAsepriteToolKeys() {
        XCTAssertEqual(PixelTool.shortcut("b", shift: true), .spray)
        XCTAssertEqual(PixelTool.shortcut("q"), .lassoSelection)
        XCTAssertEqual(PixelTool.shortcut("l"), .line)
        XCTAssertEqual(PixelTool.shortcut("u"), .rectangle)
        XCTAssertEqual(PixelTool.shortcut("u", shift: true), .ellipse)
        XCTAssertEqual(PixelTool.shortcut("g", shift: true), .gradient)
        XCTAssertNil(PixelTool.shortcut("d"))
        XCTAssertNil(PixelTool.brightness.shortcutLabel)
    }

    @MainActor func testDeleteSelectionAndLastPaletteColor() throws {
        var doc = PixelDocument(width: 8, height: 8)
        doc.layers[0].frames[0].pixels[0] = 1
        doc.layers[0].frames[0].pixels[1] = 2
        let model = PixelEditorModel(document: doc)
        model.selection = [0]
        model.clearSelectedPixels()
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[0], 0)
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[1], 2)
        XCTAssertEqual(model.selection, [0])
        try model.importGPL("GIMP Palette\n1 2 3")
        model.removePaletteColor(at: 0)
        XCTAssertEqual(model.document.palette.count, 1)
    }

}
