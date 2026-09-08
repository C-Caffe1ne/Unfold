import XCTest
@testable import Unfold

final class PixelDocumentTests: XCTestCase {
    func testFastStrokeHasNoGapsAndEraseOnlyTouchesOnePixel() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.draw(tool: .pencil, from: PixelPoint(x: 0, y: 0), to: PixelPoint(x: 7, y: 7), color: 0xFF0000FF, brush: 1, layer: 0, frame: 0)
        for i in 0..<8 { XCTAssertEqual(doc.layers[0].frames[0].pixels[i * 8 + i], 0xFF0000FF) }
        doc.draw(tool: .eraser, from: PixelPoint(x: 3, y: 3), to: PixelPoint(x: 3, y: 3), color: 0xFFFFFFFF, brush: 1, layer: 0, frame: 0)
        XCTAssertEqual(doc.layers[0].frames[0].pixels.filter { $0 != 0 }.count, 7)
        XCTAssertEqual(doc.layers[0].frames[0].pixels[27], 0)
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels, doc.layers[0].frames[0].pixels)
    }

    func testFillDoesNotCrossAnEnclosedBoundary() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.draw(tool: .rectangle, from: PixelPoint(x: 1, y: 1), to: PixelPoint(x: 5, y: 5), color: 0xFFFFFFFF, brush: 1, layer: 0, frame: 0)
        doc.draw(tool: .fill, from: PixelPoint(x: 3, y: 3), to: PixelPoint(x: 3, y: 3), color: 0x00FF00FF, brush: 1, layer: 0, frame: 0)
        let pixels = doc.layers[0].frames[0].pixels
        XCTAssertEqual(pixels.filter { $0 == 0x00FF00FF }.count, 9)
        XCTAssertEqual(pixels.filter { $0 == 0xFFFFFFFF }.count, 16)
        XCTAssertEqual(pixels[0], 0)
    }

    func testBrushAtEdgeClipsWithoutWrappingRows() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.draw(tool: .pencil, from: PixelPoint(x: 7, y: 0), to: PixelPoint(x: 7, y: 0), color: 0xFFFFFFFF, brush: 3, layer: 0, frame: 0)
        XCTAssertEqual(doc.layers[0].frames[0].pixels.filter { $0 != 0 }.count, 4)
        XCTAssertEqual(doc.layers[0].frames[0].pixels[8], 0, "must not wrap onto the next row's leftmost pixel")
    }

    func testLayerOrderAndOpacityCompositeCorrectly() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.layers[0].frames[0].pixels[0] = 0xFF0000FF
        doc.layers.append(PixelLayer(name: "Top", opacity: 0.5, frames: [PixelFrame(width: 8, height: 8)]))
        doc.layers[1].frames[0].pixels[0] = 0x0000FFFF
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels[0], 0x800080FF)
        doc.layers[1].opacity = 0
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels[0], 0xFF0000FF)
    }

    func testFrameActionsKeepEveryLayerAlignedAndCopiesIndependent() {
        var doc = PixelDocument(width: 8, height: 8)
        doc.layers[0].frames[0].pixels[0] = 0xFF0000FF
        doc.layers.append(PixelLayer(name: "Top", frames: [PixelFrame(width: 8, height: 8)]))
        doc.insertFrame(after: 0, duplicate: true)
        doc.layers[0].frames[1].pixels[0] = 0x00FF00FF
        XCTAssertEqual(doc.layers[0].frames[0].pixels[0], 0xFF0000FF)
        doc.moveFrame(from: 1, to: 0)
        XCTAssertEqual(doc.layers[0].frames[0].pixels[0], 0x00FF00FF)
        doc.removeFrame(at: 1)
        doc.removeFrame(at: 0)
        XCTAssertEqual(doc.layers.map { $0.frames.count }, [1, 1])
    }

    func testResizeAnchorsTopLeftAndFlipPreservesGeometry() {
        let side = Constants.editorCanvasSideRange.lowerBound
        var doc = PixelDocument(width: side, height: side)
        for i in doc.layers[0].frames[0].pixels.indices { doc.layers[0].frames[0].pixels[i] = UInt32(i + 1) }
        let original = doc.layers[0].frames[0].pixels

        // Growing anchors the existing pixels top-left and pads the new column with transparent pixels.
        doc.resize(width: side + 1, height: side)
        for y in 0..<side {
            for x in 0..<side {
                XCTAssertEqual(doc.layers[0].frames[0].pixels[y * (side + 1) + x], original[y * side + x])
            }
            XCTAssertEqual(doc.layers[0].frames[0].pixels[y * (side + 1) + side], 0)
        }

        // Flip mirrors columns without touching rows.
        let beforeFlip = doc.layers[0].frames[0].pixels
        doc.flip(layer: 0, frame: 0, horizontal: true)
        for y in 0..<doc.height {
            for x in 0..<doc.width {
                XCTAssertEqual(doc.layers[0].frames[0].pixels[y * doc.width + x], beforeFlip[y * doc.width + (doc.width - 1 - x)])
            }
        }

        // Shrinking crops back to the top-left region.
        let beforeShrink = doc.layers[0].frames[0].pixels
        doc.resize(width: side, height: side)
        for y in 0..<side {
            for x in 0..<side {
                XCTAssertEqual(doc.layers[0].frames[0].pixels[y * side + x], beforeShrink[y * (side + 1) + x])
            }
        }
    }

    func testFrameLimitIsEnforcedBeforeAllocation() {
        let side = Constants.editorCanvasSideRange.lowerBound
        var doc = PixelDocument(width: side, height: side)
        for _ in 0..<30 { doc.insertFrame(after: 0, duplicate: false) }
        XCTAssertEqual(doc.frameCount, 24)
    }

    func test_init_clampsToTheCanvasLowerBound() {
        let document = PixelDocument(width: 1, height: 3)
        XCTAssertEqual(document.width, Constants.editorCanvasSideRange.lowerBound)
        XCTAssertEqual(document.height, Constants.editorCanvasSideRange.lowerBound)
    }

    func test_init_clampsToTheCanvasUpperBound() {
        let document = PixelDocument(width: 4096, height: 4096)
        XCTAssertEqual(document.width, Constants.editorCanvasSideRange.upperBound)
        XCTAssertEqual(document.height, Constants.editorCanvasSideRange.upperBound)
    }

    /// The previous maximum document must stay legal, or the new ceiling
    /// would lock users out of characters they already made. This is no
    /// longer a margin check: 128×128×24×16×4 is exactly
    /// `editorMaxDocumentBytes` (24MiB, chosen to equal this maximum), so
    /// this asserts the boundary itself, not something comfortably under it
    /// -- `exceedsByteCeiling`'s `>` (not `>=`) is what keeps this legal.
    func test_theOldMaximumDocumentIsStillWithinTheByteCeiling() {
        var document = PixelDocument(width: 128, height: 128)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 128, height: 128), count: document.frameCount)))
        }
        XCTAssertEqual(document.frameCount, 24)
        XCTAssertEqual(document.layers.count, PixelDocument.maximumLayers)
        XCTAssertEqual(document.byteCount, Constants.editorMaxDocumentBytes,
                       "this document should sit exactly on the ceiling, not under it -- a weaker byteCount would make exceedsByteCeiling pass with slack instead of proving the boundary")
        XCTAssertFalse(document.exceedsByteCeiling)
    }

    func test_insertFrame_stopsAtTheByteCeiling() {
        var document = PixelDocument(width: 512, height: 512)
        while document.layers.count < 8 {
            document.layers.append(PixelLayer(name: "L", frames: [PixelFrame(width: 512, height: 512)]))
        }
        // 512*512*4 = 1 MB per frame-layer, 8 layers => 8 MB per frame.
        for _ in 0..<40 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        XCTAssertFalse(document.exceedsByteCeiling)
        XCTAssertLessThan(document.frameCount, Constants.editorFrameCountRange.upperBound,
                          "the ceiling should bite before the frame cap does at this size")
    }

    func test_resize_isRejectedWhenItWouldCrossTheByteCeiling() {
        var document = PixelDocument(width: 64, height: 64)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 64, height: 64), count: document.frameCount)))
        }
        document.resize(width: 512, height: 512)   // would be 384 MB
        XCTAssertEqual(document.width, 64, "a resize past the ceiling must be a no-op")
    }

    func test_aFullSize512DocumentIsRejectedByTheCeiling() {
        var document = PixelDocument(width: 512, height: 512)
        XCTAssertFalse(document.exceedsByteCeiling, "one frame, one layer is 1 MB")
        document.layers = Array(repeating: PixelLayer(name: "L", frames: document.layers[0].frames),
                                count: PixelDocument.maximumLayers)
        // At 16 layers, each additional frame costs 512*512*16*4 = 16 MB, so
        // insertFrame permanently refuses once frameCount reaches 4 (the 5th
        // frame would be 80 MB) -- `while frameCount < 24` would never
        // terminate. Bound by attempts instead of by the count reached.
        for _ in 0..<Constants.editorFrameCountRange.upperBound {
            document.insertFrame(after: document.frameCount - 1, duplicate: false)
        }
        XCTAssertLessThanOrEqual(document.byteCount, Constants.editorMaxDocumentBytes,
                                 "insertFrame must refuse to grow past the ceiling")
        XCTAssertLessThan(document.frameCount, Constants.editorFrameCountRange.upperBound,
                          "the ceiling should bite before the frame cap does at this size")
    }
}
