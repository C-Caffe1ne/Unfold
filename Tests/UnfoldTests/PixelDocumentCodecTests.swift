import XCTest
@testable import Unfold

final class PixelDocumentCodecTests: XCTestCase {
    private func fixture() -> PixelDocument {
        var doc = PixelDocument(width: 2, height: 3)
        doc.name = "마리 \"pixel\""
        doc.description = "Layered animation"
        doc.layers[0].frames[0].pixels = [0xFF0000FF, 0, 0x00FF00FF, 0, 0, 0x0000FFFF]
        doc.insertFrame(after: 0, duplicate: true)
        doc.layers[0].frames[1].pixels = [0, 0xFFFFFFFF, 0, 0, 0xFF0000FF, 0]
        var top = PixelFrame(width: 2, height: 3)
        top.pixels[1] = 0x0000FFFF
        doc.layers.append(PixelLayer(name: "Top", opacity: 0.5, frames: [top, top]))
        return doc
    }

    func testPiskelRoundTripPreservesOrientationLayersAndFrames() throws {
        let doc = fixture()
        XCTAssertEqual(try PixelDocumentCodec.decode(PixelDocumentCodec.encode(doc)), doc)
    }

    func testPNGImportPreservesAsymmetricPixelCoordinatesAndAlpha() throws {
        let pixels: [UInt32] = [0xFF0000FF, 0, 0x00FF00FF, 0xFF000080, 0, 0x0000FFFF]
        let png = try PixelDocumentCodec.encodePNG(pixels, width: 2, height: 3)
        let doc = try PixelDocumentCodec.importPNG(png)
        XCTAssertEqual(doc.width, 2)
        XCTAssertEqual(doc.height, 3)
        XCTAssertEqual(doc.layers[0].frames[0].pixels, pixels)
    }

    func testSpriteSheetAndPackageReloadMatchPreview() throws {
        let doc = fixture()
        let payload = try PixelDocumentCodec.savePayload(doc, characterID: nil)
        XCTAssertEqual(payload.frameCount, 2)
        let sheet = try PixelDocumentCodec.importPNG(payload.sheetPNGData)
        XCTAssertEqual(sheet.width, 4)
        for frame in 0..<doc.frameCount {
            let expected = doc.compositedFrame(at: frame)
            for y in 0..<doc.height {
                for x in 0..<doc.width {
                    XCTAssertEqual(sheet.layers[0].frames[0].pixels[y * 4 + frame * 2 + x], expected.pixels[y * 2 + x])
                }
            }
        }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let library = CharacterLibrary(rootDirectory: root)
        let character = try CharacterPackageWriter.write(payload: payload, name: doc.name, into: library)
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let reopened = try PixelDocumentCodec.load(from: directory.appendingPathComponent("source.piskel"))
        XCTAssertEqual(reopened, doc)
        XCTAssertEqual(character.spriteSheet.columns, doc.frameCount)
    }

    func testColumnMajorMultiRowChunkAndMultipleChunks() throws {
        let a: UInt32 = 0xFF0000FF, b: UInt32 = 0x00FF00FF, c: UInt32 = 0x0000FFFF, d: UInt32 = 0xFFFFFFFF
        let grid = try PixelDocumentCodec.encodePNG([a, c, b, d], width: 2, height: 2)
        let extra = try PixelDocumentCodec.encodePNG([a], width: 1, height: 1)
        let data = try source(width: 1, height: 1, frameCount: 5, chunks: [
            ["layout": [[0, 1], [2, 3]], "base64PNG": url(grid)],
            ["layout": [[4]], "base64PNG": url(extra)]
        ])
        let doc = try PixelDocumentCodec.decode(data)
        XCTAssertEqual(doc.layers[0].frames.map { $0.pixels[0] }, [a, b, c, d, a])
    }

    func testLegacyUnchunkedVersionTwoLayerLoads() throws {
        let png = try PixelDocumentCodec.encodePNG([0xFF0000FF, 0x00FF00FF], width: 2, height: 1)
        let layer: [String: Any] = ["name": "Old", "frameCount": 2, "base64PNG": url(png)]
        let layerString = String(decoding: try JSONSerialization.data(withJSONObject: layer), as: UTF8.self)
        let data = try JSONSerialization.data(withJSONObject: ["modelVersion": 2,
            "piskel": ["width": 1, "height": 1, "layers": [layerString]]])
        let doc = try PixelDocumentCodec.decode(data)
        XCTAssertEqual(doc.layers[0].frames.map { $0.pixels[0] }, [0xFF0000FF, 0x00FF00FF])
    }

    func testRejectsMissingDuplicateOutOfRangeAndRaggedFrames() throws {
        let png = try PixelDocumentCodec.encodePNG([0, 0], width: 2, height: 1)
        for layout in [[[0], [0]], [[0], [2]], [[0], []], [[0]]] {
            XCTAssertThrowsError(try PixelDocumentCodec.decode(source(width: 1, height: 1, frameCount: 2,
                chunks: [["layout": layout, "base64PNG": url(png)]])))
        }
    }

    func testRejectsOversizedCanvasAndMismatchedPNGGeometry() throws {
        let png = try PixelDocumentCodec.encodePNG([0], width: 1, height: 1)
        for width in [0, 2, 129, Int.max] {
            XCTAssertThrowsError(try PixelDocumentCodec.decode(source(width: width, height: 1, frameCount: 1,
                chunks: [["layout": [[0]], "base64PNG": url(png)]])))
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
