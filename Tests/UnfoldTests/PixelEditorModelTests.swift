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
}
