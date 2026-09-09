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
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
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
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
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

    func test_addLayer_stopsAtTheByteCeiling() {
        var document = PixelDocument(width: 512, height: 512)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        let model = PixelEditorModel(document: document)
        for _ in 0..<PixelDocument.maximumLayers { model.addLayer() }
        XCTAssertLessThanOrEqual(model.document.byteCount, Constants.editorMaxDocumentBytes)
        XCTAssertLessThan(model.document.layers.count, PixelDocument.maximumLayers,
                          "the ceiling should bite before the layer cap does at this size")
    }

    func test_aFreshDocumentOpensAtOneHundredPercent() {
        let model = PixelEditorModel(document: PixelDocument())
        XCTAssertEqual(model.zoomPercent, 100)
    }

    /// The old formula gave a 512px document a scale of 2, opening it at
    /// 1024 points — wider than the window it lives in.
    func test_aLargeDocumentOpensAtOneHundredPercentAndFitsTheViewport() {
        let model = PixelEditorModel(document: PixelDocument(width: 512, height: 512))
        XCTAssertEqual(model.zoomPercent, 100)
        XCTAssertGreaterThanOrEqual(model.zoom, 1)
        XCTAssertLessThanOrEqual(512 * model.zoom, PixelEditorModel.fittingViewportSide)
    }

    func test_aTinyDocumentStillOpensLargeEnoughToDrawOn() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        XCTAssertEqual(model.zoomPercent, 100)
        XCTAssertGreaterThanOrEqual(8 * model.zoom, 128, "an 8px canvas should not open thumbnail-sized")
    }

    func test_zoomingInDoublesThePercentage() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        let base = model.zoom
        model.zoomIn()
        XCTAssertEqual(model.zoom, base * 2)
        XCTAssertEqual(model.zoomPercent, 200)
    }

    func test_zoomingOutHalvesThePercentage() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        let base = model.zoom
        model.zoomOut()
        XCTAssertEqual(model.zoom, base / 2)
        XCTAssertEqual(model.zoomPercent, 50)
    }

    func test_zoomIsClampedToTheRenderableRange() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        for _ in 0..<12 { model.zoomIn() }
        XCTAssertLessThanOrEqual(model.zoom, PixelEditorModel.maximumZoom)
        XCTAssertFalse(model.canZoomIn)
        for _ in 0..<12 { model.zoomOut() }
        XCTAssertGreaterThanOrEqual(model.zoom, 1)
        XCTAssertFalse(model.canZoomOut)
    }

    /// Returning to the opening scale must read exactly 100% again, not 99%
    /// or 101% from rounding.
    func test_zoomingBackToTheOpeningScaleReadsOneHundredPercent() {
        for side in [8, 16, 64, 128, 512] {
            let model = PixelEditorModel(document: PixelDocument(width: side, height: side))
            model.zoomIn()
            model.zoomOut()
            XCTAssertEqual(model.zoomPercent, 100, "side \(side)")
        }
    }

    /// Every reachable zoom level must be reversible: stepping in and back
    /// out, or out and back in, returns to exactly where it started. This is
    /// what storing a step rather than a scale buys.
    func test_zoomingIsReversibleAtEveryReachableLevel() {
        for side in [8, 16, 24, 32, 64, 128, 256, 512] {
            let model = PixelEditorModel(document: PixelDocument(width: side, height: side))
            let opening = model.zoom
            XCTAssertEqual(model.zoomPercent, 100, "side \(side) should open at 100%")

            while model.canZoomIn {
                let before = model.zoom
                model.zoomIn()
                model.zoomOut()
                XCTAssertEqual(model.zoom, before, "side \(side): in then out changed the scale")
                model.zoomIn()
            }
            while model.canZoomOut { model.zoomOut() }
            while model.zoom < opening { model.zoomIn() }
            XCTAssertEqual(model.zoom, opening, "side \(side): could not return to the opening scale")
            XCTAssertEqual(model.zoomPercent, 100, "side \(side): back at the opening scale but not 100%")
        }
    }
}
