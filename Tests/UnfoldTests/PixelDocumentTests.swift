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
        var doc = PixelDocument(width: 7, height: 7)
        doc.draw(tool: .rectangle, from: PixelPoint(x: 1, y: 1), to: PixelPoint(x: 5, y: 5), color: 0xFFFFFFFF, brush: 1, layer: 0, frame: 0)
        doc.draw(tool: .fill, from: PixelPoint(x: 3, y: 3), to: PixelPoint(x: 3, y: 3), color: 0x00FF00FF, brush: 1, layer: 0, frame: 0)
        let pixels = doc.layers[0].frames[0].pixels
        XCTAssertEqual(pixels.filter { $0 == 0x00FF00FF }.count, 9)
        XCTAssertEqual(pixels.filter { $0 == 0xFFFFFFFF }.count, 16)
        XCTAssertEqual(pixels[0], 0)
    }

    func testBrushAtEdgeClipsWithoutWrappingRows() {
        var doc = PixelDocument(width: 4, height: 4)
        doc.draw(tool: .pencil, from: PixelPoint(x: 3, y: 0), to: PixelPoint(x: 3, y: 0), color: 0xFFFFFFFF, brush: 3, layer: 0, frame: 0)
        XCTAssertEqual(doc.layers[0].frames[0].pixels.filter { $0 != 0 }.count, 4)
        XCTAssertEqual(doc.layers[0].frames[0].pixels[4], 0)
    }

    func testLayerOrderAndOpacityCompositeCorrectly() {
        var doc = PixelDocument(width: 1, height: 1)
        doc.layers[0].frames[0].pixels = [0xFF0000FF]
        var upper = PixelFrame(width: 1, height: 1)
        upper.pixels = [0x0000FFFF]
        doc.layers.append(PixelLayer(name: "Top", opacity: 0.5, frames: [upper]))
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels, [0x800080FF])
        doc.layers[1].opacity = 0
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels, [0xFF0000FF])
    }

    func testFrameActionsKeepEveryLayerAlignedAndCopiesIndependent() {
        var doc = PixelDocument(width: 2, height: 1)
        doc.layers[0].frames[0].pixels = [0xFF0000FF, 0]
        doc.layers.append(PixelLayer(name: "Top", frames: [PixelFrame(width: 2, height: 1)]))
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
        var doc = PixelDocument(width: 2, height: 2)
        doc.layers[0].frames[0].pixels = [1, 2, 3, 4]
        doc.resize(width: 3, height: 2)
        XCTAssertEqual(doc.layers[0].frames[0].pixels, [1, 2, 0, 3, 4, 0])
        doc.flip(layer: 0, frame: 0, horizontal: true)
        XCTAssertEqual(doc.layers[0].frames[0].pixels, [0, 2, 1, 0, 4, 3])
        doc.resize(width: 2, height: 1)
        XCTAssertEqual(doc.layers[0].frames[0].pixels, [0, 2])
    }

    func testFrameLimitIsEnforcedBeforeAllocation() {
        var doc = PixelDocument(width: 1, height: 1)
        for _ in 0..<30 { doc.insertFrame(after: 0, duplicate: false) }
        XCTAssertEqual(doc.frameCount, 24)
    }
}
