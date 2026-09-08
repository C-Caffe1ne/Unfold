import XCTest
@testable import Unfold

final class PixelDocumentCodecTests: XCTestCase {
    /// A non-square canvas at the new minimum: any bug that transposes
    /// width/height, or rows/columns, during encode/decode would move these
    /// corner markers to the wrong pixel index.
    private func fixture() -> PixelDocument {
        var doc = PixelDocument(width: Constants.editorCanvasSideRange.lowerBound,
                                 height: Constants.editorCanvasSideRange.lowerBound + 1)
        doc.name = "마리 \"pixel\""
        doc.description = "Layered animation"
        func index(_ x: Int, _ y: Int) -> Int { y * doc.width + x }
        doc.layers[0].frames[0].pixels[index(0, 0)] = 0xFF0000FF
        doc.layers[0].frames[0].pixels[index(doc.width - 1, 0)] = 0x00FF00FF
        doc.layers[0].frames[0].pixels[index(0, doc.height - 1)] = 0x0000FFFF
        doc.insertFrame(after: 0, duplicate: true)
        doc.layers[0].frames[1].pixels = Array(repeating: 0, count: doc.width * doc.height)
        doc.layers[0].frames[1].pixels[index(doc.width - 1, doc.height - 1)] = 0xFFFFFFFF
        doc.layers[0].frames[1].pixels[index(1, 0)] = 0xFF0000FF
        var top = PixelFrame(width: doc.width, height: doc.height)
        top.pixels[index(1, 1)] = 0x0000FFFF
        doc.layers.append(PixelLayer(name: "Top", opacity: 0.5, frames: [top, top]))
        return doc
    }

    func testPiskelRoundTripPreservesOrientationLayersAndFrames() throws {
        let doc = fixture()
        XCTAssertEqual(try PixelDocumentCodec.decode(PixelDocumentCodec.encode(doc)), doc)
    }

    func testPNGImportPreservesAsymmetricPixelCoordinatesAndAlpha() throws {
        let width = Constants.editorCanvasSideRange.lowerBound
        let height = width + 1
        var pixels = Array(repeating: UInt32(0), count: width * height)
        pixels[0] = 0xFF0000FF                            // (0, 0)
        pixels[width - 1] = 0x00FF00FF                     // (width - 1, 0)
        pixels[(height - 1) * width] = 0x0000FFFF          // (0, height - 1)
        pixels[width + 1] = 0xFF000080                     // (1, 1), semi-transparent
        let png = try PixelDocumentCodec.encodePNG(pixels, width: width, height: height)
        let doc = try PixelDocumentCodec.importPNG(png)
        XCTAssertEqual(doc.width, width)
        XCTAssertEqual(doc.height, height)
        XCTAssertEqual(doc.layers[0].frames[0].pixels, pixels)
    }

    /// `decodePNG` has no lower-bound check of its own, so a PNG smaller
    /// than the canvas minimum reaches `PixelDocument.init`, which clamps
    /// up. `importPNG` must pad the extra rows/columns rather than blindly
    /// assigning the (too-small) decoded buffer into the (clamped-up) frame.
    func testPNGImportOfATinyImagePadsUpToTheCanvasMinimum() throws {
        let pixels: [UInt32] = [0xFF0000FF, 0x00FF00FF, 0x0000FFFF, 0xFFFFFFFF, 0, 0]
        let png = try PixelDocumentCodec.encodePNG(pixels, width: 3, height: 2)
        let doc = try PixelDocumentCodec.importPNG(png)
        let side = Constants.editorCanvasSideRange.lowerBound
        XCTAssertEqual(doc.width, side)
        XCTAssertEqual(doc.height, side)
        XCTAssertEqual(doc.layers[0].frames[0].pixels.count, side * side)
        for y in 0..<2 {
            for x in 0..<3 {
                XCTAssertEqual(doc.layers[0].frames[0].pixels[y * side + x], pixels[y * 3 + x])
            }
        }
        XCTAssertEqual(doc.layers[0].frames[0].pixels[3], 0, "padding beyond the imported width must stay transparent")
    }

    func testSpriteSheetAndPackageReloadMatchPreview() throws {
        let doc = fixture()
        let payload = try PixelDocumentCodec.savePayload(doc, characterID: nil)
        XCTAssertEqual(payload.frameCount, 2)
        let sheet = try PixelDocumentCodec.importPNG(payload.sheetPNGData)
        XCTAssertEqual(sheet.width, doc.width * doc.frameCount)
        for frame in 0..<doc.frameCount {
            let expected = doc.compositedFrame(at: frame)
            for y in 0..<doc.height {
                for x in 0..<doc.width {
                    XCTAssertEqual(sheet.layers[0].frames[0].pixels[y * doc.width * doc.frameCount + frame * doc.width + x],
                                   expected.pixels[y * doc.width + x])
                }
            }
        }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let library = CharacterLibrary(rootDirectory: root)
        let character = try CharacterPackageWriter.write(payload: payload, name: doc.name, into: library)
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let reopened = try PixelDocumentCodec.load(from: directory.appendingPathComponent(Constants.characterEditorSourceFileName))
        XCTAssertEqual(reopened, doc)
        XCTAssertEqual(character.spriteSheet.columns, doc.frameCount)
    }

    /// A solid `side`×`side` PNG per cell, arranged column-major to match
    /// how `decode` reads `chunk.layout[x][y]`: column `x` occupies pixel
    /// columns `[x*side, (x+1)*side)`, row `y` occupies pixel rows
    /// `[y*side, (y+1)*side)`.
    private func grid2x2PNG(topLeft: UInt32, bottomLeft: UInt32, topRight: UInt32, bottomRight: UInt32, side: Int) throws -> Data {
        var pixels = Array(repeating: UInt32(0), count: side * 2 * side * 2)
        for y in 0..<(side * 2) {
            for x in 0..<(side * 2) {
                let color = y < side
                    ? (x < side ? topLeft : topRight)
                    : (x < side ? bottomLeft : bottomRight)
                pixels[y * side * 2 + x] = color
            }
        }
        return try PixelDocumentCodec.encodePNG(pixels, width: side * 2, height: side * 2)
    }

    func testColumnMajorMultiRowChunkAndMultipleChunks() throws {
        let a: UInt32 = 0xFF0000FF, b: UInt32 = 0x00FF00FF, c: UInt32 = 0x0000FFFF, d: UInt32 = 0xFFFFFFFF
        let side = Constants.editorCanvasSideRange.lowerBound
        // layout [[0, 1], [2, 3]]: column 0 = (frame 0 top, frame 1 bottom), column 1 = (frame 2 top, frame 3 bottom).
        let grid = try grid2x2PNG(topLeft: a, bottomLeft: b, topRight: c, bottomRight: d, side: side)
        let extra = try PixelDocumentCodec.encodePNG(Array(repeating: a, count: side * side), width: side, height: side)
        let data = try source(width: side, height: side, frameCount: 5, chunks: [
            ["layout": [[0, 1], [2, 3]], "base64PNG": url(grid)],
            ["layout": [[4]], "base64PNG": url(extra)]
        ])
        let doc = try PixelDocumentCodec.decode(data)
        XCTAssertEqual(doc.layers[0].frames.map { $0.pixels[0] }, [a, b, c, d, a])
    }

    func testLegacyUnchunkedVersionTwoLayerLoads() throws {
        let side = Constants.editorCanvasSideRange.lowerBound
        var pixels = Array(repeating: UInt32(0), count: side * 2 * side)
        for y in 0..<side {
            for x in 0..<(side * 2) { pixels[y * side * 2 + x] = x < side ? 0xFF0000FF : 0x00FF00FF }
        }
        let png = try PixelDocumentCodec.encodePNG(pixels, width: side * 2, height: side)
        let layer: [String: Any] = ["name": "Old", "frameCount": 2, "base64PNG": url(png)]
        let layerString = String(decoding: try JSONSerialization.data(withJSONObject: layer), as: UTF8.self)
        let data = try JSONSerialization.data(withJSONObject: ["modelVersion": 2,
            "piskel": ["width": side, "height": side, "layers": [layerString]]])
        let doc = try PixelDocumentCodec.decode(data)
        XCTAssertEqual(doc.layers[0].frames.map { $0.pixels[0] }, [0xFF0000FF, 0x00FF00FF])
    }

    func testRejectsMissingDuplicateOutOfRangeAndRaggedFrames() throws {
        let side = Constants.editorCanvasSideRange.lowerBound
        let png = try PixelDocumentCodec.encodePNG(Array(repeating: UInt32(0), count: side * 2 * side), width: side * 2, height: side)
        for layout in [[[0], [0]], [[0], [2]], [[0], []], [[0]]] {
            XCTAssertThrowsError(try PixelDocumentCodec.decode(source(width: side, height: side, frameCount: 2,
                chunks: [["layout": layout, "base64PNG": url(png)]])))
        }
    }

    func testRejectsOversizedCanvasAndMismatchedPNGGeometry() throws {
        let png = try PixelDocumentCodec.encodePNG([0], width: 1, height: 1)
        let height = Constants.editorCanvasSideRange.upperBound   // held valid so only `width` is under test
        // 0: below the canvas minimum, so rejected before the PNG is even
        // looked at. upperBound - 1: a valid canvas size, but the 1x1 PNG
        // doesn't match its declared chunk geometry. upperBound + 1: above
        // the canvas maximum. Int.max: extreme/overflow guard.
        for width in [0, Constants.editorCanvasSideRange.upperBound - 1, Constants.editorCanvasSideRange.upperBound + 1, Int.max] {
            XCTAssertThrowsError(try PixelDocumentCodec.decode(source(width: width, height: height, frameCount: 1,
                chunks: [["layout": [[0]], "base64PNG": url(png)]])))
        }
    }

    /// A well-compressing `.unf` can pass the `maximumSourceBytes` file-size
    /// gate while its *declared* geometry still multiplies past
    /// `editorMaxDocumentBytes` -- the same 384MB-style blowup the ceiling
    /// exists to prevent, reached by opening a file instead of growing one.
    /// The two layers here carry no pixel data at all (no `chunks`, no
    /// `base64PNG`) and the whole input is a few hundred bytes: if `decode`
    /// only caught this by actually allocating the frames, this input
    /// wouldn't trigger it. It must be rejected on the declared numbers
    /// alone, before either layer's pixels are ever looked at.
    func testRejectsDeclaredGeometryOverTheByteCeilingBeforeAllocatingFrames() throws {
        let side = Constants.editorCanvasSideRange.upperBound
        let frameCount = Constants.editorFrameCountRange.upperBound
        // side*side*frameCount*4 = exactly editorMaxDocumentBytes with one
        // layer (that's the single-layer case at the cap); two layers
        // doubles it, well past the ceiling.
        let layer: [String: Any] = ["name": "L", "frameCount": frameCount]
        let layerString = String(decoding: try JSONSerialization.data(withJSONObject: layer), as: UTF8.self)
        let data = try JSONSerialization.data(withJSONObject: ["modelVersion": 2,
            "piskel": ["width": side, "height": side, "fps": 12, "layers": [layerString, layerString]]])
        XCTAssertLessThan(data.count, 1024, "the input must stay tiny -- proving the check doesn't need a genuinely huge document to fire")
        XCTAssertThrowsError(try PixelDocumentCodec.decode(data)) { error in
            guard case PixelDocumentCodec.Failure.invalid(let message) = error else {
                return XCTFail("expected Failure.invalid, got \(error)")
            }
            XCTAssertEqual(message, "This document is too large to open.",
                           "a layer missing its pixels ('base64PNG'/'chunks' absent) would throw a different message if the size check ran too late")
        }
    }

    func testRejectsTruncatedAndNonPNGFiles() throws {
        let png = try PixelDocumentCodec.encodePNG([0xFFFFFFFF], width: 1, height: 1)
        XCTAssertThrowsError(try PixelDocumentCodec.importPNG(Data(png.prefix(png.count / 2))))
        XCTAssertThrowsError(try PixelDocumentCodec.importPNG(Data("not png".utf8)))
        XCTAssertThrowsError(try PixelDocumentCodec.decode(Data("not json".utf8)))
    }

    private func url(_ data: Data) -> String { "data:image/png;base64," + data.base64EncodedString() }

    private func source(width: Int, height: Int, frameCount: Int, chunks: [[String: Any]]) throws -> Data {
        let layer: [String: Any] = ["name": "Layer", "opacity": 1, "frameCount": frameCount, "chunks": chunks]
        let layerString = String(decoding: try JSONSerialization.data(withJSONObject: layer), as: UTF8.self)
        return try JSONSerialization.data(withJSONObject: ["modelVersion": 2,
            "piskel": ["width": width, "height": height, "fps": 12, "layers": [layerString]]])
    }
}
