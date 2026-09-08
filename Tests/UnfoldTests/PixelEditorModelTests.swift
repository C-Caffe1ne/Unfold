import XCTest
@testable import Unfold

@MainActor
final class PixelEditorModelTests: XCTestCase {
    func testEntireDragIsOneUndoAndRedoRestoresIt() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.beginStroke(at: PixelPoint(x: 0, y: 0))
        model.continueStroke(at: PixelPoint(x: 4, y: 0))
        model.continueStroke(at: PixelPoint(x: 7, y: 0))
        model.endStroke()
        let drawn = model.document
        XCTAssertTrue(model.isDirty)
        model.undo()
        XCTAssertFalse(model.isDirty)
        XCTAssertFalse(model.canUndo)
        model.redo()
        XCTAssertEqual(model.document, drawn)
    }

    func testShapePreviewDoesNotLeaveEarlierOutlines() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.tool = .rectangle
        model.beginStroke(at: PixelPoint(x: 0, y: 0))
        model.continueStroke(at: PixelPoint(x: 3, y: 3))
        model.continueStroke(at: PixelPoint(x: 5, y: 5))
        model.endStroke()
        XCTAssertEqual(model.document.layers[0].frames[0].pixels[3 * 8 + 3], 0)
        XCTAssertNotEqual(model.document.layers[0].frames[0].pixels[5 * 8 + 5], 0)
    }

    func testReenteringCanvasResumesWithoutDrawingAcrossOutsideGap() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        model.beginStroke(at: PixelPoint(x: 0, y: 0))
        model.continueStroke(at: PixelPoint(x: -1, y: -1))
        model.continueStroke(at: PixelPoint(x: 7, y: 7))
        model.endStroke()
        XCTAssertEqual(model.document.layers[0].frames[0].pixels.filter { $0 != 0 }.count, 2)
    }

    func testNewEditAfterUndoInvalidatesRedoAndSavePointTracksContent() {
        let model = PixelEditorModel(document: PixelDocument(width: 4, height: 4))
        model.beginStroke(at: PixelPoint(x: 0, y: 0))
        model.endStroke()
        model.markSaved()
        model.undo()
        XCTAssertTrue(model.isDirty)
        model.redo()
        XCTAssertFalse(model.isDirty)
        model.undo()
        model.addFrame(duplicate: false)
        XCTAssertFalse(model.canRedo)
    }

    func testUndoOfAddingSelectedLayerAndFrameClampsSelection() {
        let model = PixelEditorModel(document: PixelDocument(width: 4, height: 4))
        model.addLayer()
        model.addFrame(duplicate: true)
        model.undo()
        XCTAssertEqual(model.selectedFrame, 0)
        model.undo()
        XCTAssertEqual(model.selectedLayer, 0)
        XCTAssertEqual(model.document.layers.count, 1)
    }

    /// A maximal document used to overrun the history budget with two
    /// snapshots, leaving exactly one undo step no matter how many edits
    /// the user made.
    func test_undoKeepsAMinimumDepth_onALargeDocument() {
        var document = PixelDocument(width: 128, height: 128)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 128, height: 128), count: document.frameCount)))
        }
        let model = PixelEditorModel(document: document)

        for i in 0..<5 {
            model.beginStroke(at: PixelPoint(x: i, y: 0))
            model.endStroke()
        }

        var depth = 0
        while model.canUndo { model.undo(); depth += 1 }
        XCTAssertEqual(depth, 5, "five edits should leave five undo steps")
    }

    /// The hard step cap still applies once the minimum depth is satisfied.
    /// Each stroke uses a different colour so it genuinely changes the
    /// document — `endStroke` only records history when something changed,
    /// so repainting a pixel its existing colour would silently not count.
    func test_undoStillTrims_beyondTheMinimumDepth() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        for i in 0..<120 {
            model.color = UInt32(i + 1) << 24 | 0xFF
            model.beginStroke(at: PixelPoint(x: i % 8, y: (i / 8) % 8))
            model.endStroke()
        }
        var depth = 0
        while model.canUndo { model.undo(); depth += 1 }
        XCTAssertGreaterThan(depth, 16, "the minimum depth is a floor, not a ceiling")
        XCTAssertLessThanOrEqual(depth, 100, "the hard step cap still applies")
    }
}
