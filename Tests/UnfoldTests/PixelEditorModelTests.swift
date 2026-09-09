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

    /// Dragging a cel offsets one layer against the others, so every other
    /// layer has to come out of it untouched — and the frame count with it,
    /// since the columns stay shared.
    func test_movingACelReordersOnlyItsOwnLayer() {
        let model = PixelEditorModel(document: PixelDocument(width: 4, height: 4))
        model.addFrame(duplicate: false)
        model.addFrame(duplicate: false)
        model.addLayer()
        XCTAssertEqual(model.document.frameCount, 3)
        XCTAssertEqual(model.document.layers.count, 2)
        model.change { doc in
            for layer in doc.layers.indices {
                for frame in 0..<doc.frameCount {
                    doc.layers[layer].frames[frame].pixels[0] = UInt32(layer * 10 + frame + 1) << 24 | 255
                }
            }
        }
        func marks(_ layer: Int) -> [UInt32] {
            (0..<model.document.frameCount).map { model.document.layers[layer].frames[$0].pixels[0] >> 24 }
        }
        let untouched = marks(1)

        model.moveCel(layer: 0, from: 0, to: 2)

        XCTAssertEqual(marks(0), [2, 3, 1], "the cel should land at the end of its own layer")
        XCTAssertEqual(marks(1), untouched, "the other layer must not move")
        XCTAssertEqual(model.document.frameCount, 3, "moving a cel must not change the frame count")
        XCTAssertEqual(model.selectedFrame, 2)
        XCTAssertTrue(model.canUndo)
    }

    func test_aLockedLayerRefusesACelMove() {
        let model = PixelEditorModel(document: PixelDocument(width: 4, height: 4))
        model.addFrame(duplicate: false)
        model.toggleLayerLock(0)
        let before = model.document
        model.moveCel(layer: 0, from: 0, to: 1)
        XCTAssertEqual(model.document, before)
    }

    /// Doubling overshot: one press took a fitted canvas straight to twice
    /// the window, with nothing usable in between. A 64px canvas opens at a
    /// scale of 8, whose fifth rounds to a two-point step — 25% a press,
    /// which is the closest whole-point approximation of the 20% asked for.
    func test_zoomingInStepsUpByAFifthOfTheOpeningScale() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        XCTAssertEqual(model.zoom, 8)
        model.zoomIn()
        XCTAssertEqual(model.zoom, 10)
        XCTAssertEqual(model.zoomPercent, 125)
    }

    func test_zoomingOutStepsDownByAFifthOfTheOpeningScale() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        model.zoomOut()
        XCTAssertEqual(model.zoom, 6)
        XCTAssertEqual(model.zoomPercent, 75)
    }

    /// A typed percentage lands on the nearest drawable scale, and the label
    /// is rewritten to whatever that turned out to be.
    func test_typedZoomSnapsToTheNearestReachableScale() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        // 137% of a scale of 8 is 10.96, nearer the level at 10 than the one
        // at 12, so the field is rewritten to the 125% it actually landed on.
        model.setZoomPercent(137)
        XCTAssertEqual(model.zoom, 10)
        XCTAssertEqual(model.zoomPercent, 125)

        model.setZoomPercent(100)
        XCTAssertEqual(model.zoom, 8)
        XCTAssertEqual(model.zoomPercent, 100)

        model.setZoomPercent(100_000)
        XCTAssertEqual(model.zoom, PixelEditorModel.maximumZoom)
        model.setZoomPercent(1)
        XCTAssertGreaterThanOrEqual(model.zoom, 1)
    }

    /// A scale must be a whole number of points per pixel, so at a small
    /// opening scale two neighbouring percentages round together. Those
    /// duplicates have to be dropped, or a press moves the label and nothing
    /// else — worst at 512px, which opens at one point per pixel.
    func test_everyZoomLevelChangesTheScale() {
        for side in [8, 16, 24, 32, 64, 128, 256, 512] {
            let model = PixelEditorModel(document: PixelDocument(width: side, height: side))
            while model.canZoomOut { model.zoomOut() }
            var seen = [model.zoom]
            while model.canZoomIn {
                model.zoomIn()
                seen.append(model.zoom)
            }
            XCTAssertGreaterThan(seen.count, 1, "side \(side) should offer more than one zoom level")
            XCTAssertEqual(seen, seen.sorted(), "side \(side) is not ascending: \(seen)")
            XCTAssertEqual(Set(seen).count, seen.count, "side \(side) repeats a scale: \(seen)")
            XCTAssertTrue(seen.allSatisfy { (1...PixelEditorModel.maximumZoom).contains($0) },
                          "side \(side) left the renderable range: \(seen)")
        }
    }

    /// Levels are laid out from the opening scale outwards in whole-point
    /// steps, so nothing between the ends is skipped and the opening scale is
    /// always one of them.
    func test_zoomLevelsAreEvenlySpacedAroundTheOpeningScale() {
        XCTAssertEqual(Array(PixelEditorModel.zoomLevels(base: 8).prefix(6)), [2, 4, 6, 8, 10, 12])
        XCTAssertEqual(PixelEditorModel.zoomLevels(base: 24).prefix(5).map { $0 }, [4, 9, 14, 19, 24])
        // One point per pixel is the floor, so a document that opens there
        // cannot zoom out at all.
        XCTAssertEqual(PixelEditorModel.zoomLevels(base: 1).first, 1)
        for base in 1...PixelEditorModel.maximumZoom {
            XCTAssertTrue(PixelEditorModel.zoomLevels(base: base).contains(base), "base \(base)")
        }
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

    /// Builds a palette of known greys through the editor's own import, so
    /// these tests need no back door into the document.
    private func model(palette greys: [Int]) -> PixelEditorModel {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let body = greys.map { "\($0) \($0) \($0)" }.joined(separator: "\n")
        try? model.importGPL("GIMP Palette\nName: Test\n" + body)
        return model
    }

    private func rgb(_ grey: Int) -> UInt32 {
        UInt32(grey) << 24 | UInt32(grey) << 16 | UInt32(grey) << 8 | 255
    }

    func test_deletingSeveralSwatchesAtOnceRemovesExactlyThoseChosen() {
        let model = model(palette: [17, 34, 51, 68])
        model.removePaletteColors(at: [0, 2])
        XCTAssertEqual(model.document.palette, [rgb(34), rgb(68)])
    }

    func test_deletingEverySwatchLeavesTheFirstOneBehind() {
        let model = model(palette: [17, 34, 51])
        model.removePaletteColors(at: [0, 1, 2])
        XCTAssertEqual(model.document.palette, [rgb(17)],
            "a palette must never be emptied; the lowest chosen swatch survives")
    }

    func test_deletingSwatchesIgnoresIndicesThePaletteDoesNotHave() {
        let model = model(palette: [17, 34])
        model.removePaletteColors(at: [1, 9])
        XCTAssertEqual(model.document.palette, [rgb(17)])
    }

    func test_deletingNoSwatchesIsNotADocumentChange() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        let before = model.document.palette
        model.removePaletteColors(at: [])
        XCTAssertEqual(model.document.palette, before)
        XCTAssertFalse(model.canUndo)
    }
}
