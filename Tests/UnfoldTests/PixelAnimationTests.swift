import XCTest
import ImageIO
@testable import Unfold

final class PixelAnimationTests: XCTestCase {
    private func document() -> PixelDocument {
        var doc = PixelDocument(width: 8, height: 8)
        doc.insertFrame(after: 0, duplicate: false)
        doc.insertFrame(after: 1, duplicate: false)
        doc.frameSettings = [.init(durationMS: 100), .init(durationMS: 200), .init(durationMS: 300)]
        return doc
    }

    func testPlaybackUsesElapsedTimeAndStopsOnLastFrame() {
        var doc = document()
        doc.playbackMode = .once
        XCTAssertEqual(doc.playbackSample(elapsed: 0).frame, 0)
        XCTAssertEqual(doc.playbackSample(elapsed: 0.15).frame, 1)
        XCTAssertEqual(doc.playbackSample(elapsed: 0.35).frame, 2)
        XCTAssertTrue(doc.playbackSample(elapsed: 0.7).finished)
        XCTAssertEqual(doc.playbackSample(elapsed: 0.7).frame, 2)
        doc.playbackMode = .loop
        XCTAssertEqual(doc.playbackSample(elapsed: 0.65).frame, 0)
        XCTAssertFalse(doc.playbackSample(elapsed: 0.65).finished)
    }

    func testPingPongRangeAndAllHiddenHaveDefinedSequences() {
        var doc = document()
        doc.playbackMode = .pingPong
        XCTAssertEqual(doc.playbackSequence, [0, 1, 2, 1])
        doc.frameSettings[1].isVisible = false
        XCTAssertEqual(doc.playbackSequence, [0, 2])
        doc.playbackMode = .range
        doc.playbackStart = 1; doc.playbackEnd = 2
        XCTAssertEqual(doc.playbackSequence, [2])
        doc.frameSettings[2].isVisible = false
        XCTAssertTrue(doc.playbackSequence.isEmpty)
        XCTAssertNil(doc.playbackSample(elapsed: 2).frame)
        XCTAssertTrue(doc.playbackSample(elapsed: 2).finished)
    }

    func testFrameMetadataFollowsReorderInsertAndDelete() {
        var doc = document()
        doc.layers[0].frames[0].pixels[0] = 0xFF0000FF
        doc.moveFrame(from: 0, to: 2)
        XCTAssertEqual(doc.duration(at: 2), 0.1, accuracy: 0.00001)
        XCTAssertEqual(doc.layers[0].frames[2].pixels[0], 0xFF0000FF)
        doc.insertFrame(after: 2, duplicate: true)
        XCTAssertEqual(doc.settings(at: 3), doc.settings(at: 2))
        doc.removeFrame(at: 0)
        XCTAssertEqual(doc.frameSettings.count, doc.frameCount)
    }

    func testMetadataRoundTripAndVisibilityPreservesOpacity() throws {
        var doc = document()
        doc.playbackMode = .range; doc.playbackStart = 1; doc.playbackEnd = 2
        doc.frameSettings[0].isVisible = false
        doc.layers[0].isLocked = true; doc.layers[0].isVisible = false
        doc.layers[0].opacity = 0.5
        doc.layers[0].frames[0].pixels[0] = 0xFF0000FF
        doc.palette = [0x123456FF, 0x00000000]
        XCTAssertEqual(doc.compositedFrame(at: 0).pixels[0], 0)
        let copy = try PixelDocumentCodec.decode(PixelDocumentCodec.encode(doc))
        XCTAssertEqual(copy, doc)
        XCTAssertEqual(copy.layers[0].opacity, 0.5)
    }

    func testInvalidMetadataIsRejected() throws {
        let data = try PixelDocumentCodec.encode(document())
        var root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var sprite = try XCTUnwrap(root["piskel"] as? [String: Any])
        var ext = try XCTUnwrap(sprite["unfold"] as? [String: Any])
        ext["frameSettings"] = [["durationMS": 0, "isVisible": true]]
        sprite["unfold"] = ext; root["piskel"] = sprite
        XCTAssertThrowsError(try PixelDocumentCodec.decode(JSONSerialization.data(withJSONObject: root)))
    }

    func testGIFHonorsDurationsHiddenFramesAndOnce() throws {
        var doc = document()
        doc.playbackMode = .once
        doc.frameSettings[1].isVisible = false
        let gif = try AnimatedGIFEncoder.encode(doc)
        let source = try XCTUnwrap(CGImageSourceCreateWithData(gif as CFData, nil))
        XCTAssertEqual(CGImageSourceGetCount(source), 2)
        for (index, expected) in [0.1, 0.3].enumerated() {
            let props = try XCTUnwrap(CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any])
            let gifProps = try XCTUnwrap(props[kCGImagePropertyGIFDictionary] as? [CFString: Any])
            let delay = try XCTUnwrap(gifProps[kCGImagePropertyGIFUnclampedDelayTime] as? Double)
            XCTAssertEqual(delay, expected, accuracy: 0.011)
        }
        XCTAssertNil(gif.range(of: Data("NETSCAPE2.0".utf8)), "Once must omit the looping extension")
    }
}

extension PixelAnimationTests {
    func testLibraryPackagePreservesPlaybackAndClipDurations() throws {
        var doc = document()
        doc.playbackMode = .pingPong
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let library = CharacterLibrary(rootDirectory: root)
        let payload = try PixelDocumentCodec.savePayload(doc, characterID: nil)
        let character = try CharacterPackageWriter.write(payload: payload, name: "Timing", into: library)
        guard case .spriteSheet(let definition) = character.animation(for: .idle) else { return XCTFail("Missing animation") }
        XCTAssertEqual(definition.frames, [0, 1, 2, 1])
        let sheet = TestSpriteSheet.makeSheet(columns: 3, rows: 1, cells: [.red, .green, .blue])
        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: definition)
        XCTAssertEqual(clip.frames.map(\.duration), [0.1, 0.2, 0.3, 0.2])
        XCTAssertTrue(clip.loop)
    }

    func testPayloadRejectsOutOfBoundsPlaybackAndMismatchedDurations() throws {
        let payload = try PixelDocumentCodec.savePayload(document(), characterID: nil)
        var wire: [String: Any] = ["type": "save", "width": payload.width, "height": payload.height,
            "fps": payload.fps, "frameCount": payload.frameCount,
            "sheetPNG": "data:image/png;base64," + payload.sheetPNGData.base64EncodedString(), "sourceJSON": payload.sourceJSON,
            "playbackFrames": [9], "frameDurations": [0.1]]
        XCTAssertThrowsError(try EditorSavePayload.decode(from: String(decoding: JSONSerialization.data(withJSONObject: wire), as: UTF8.self)))
        wire["playbackFrames"] = [0, 1]
        XCTAssertThrowsError(try EditorSavePayload.decode(from: String(decoding: JSONSerialization.data(withJSONObject: wire), as: UTF8.self)))
    }
}

extension PixelAnimationTests {
    func testLegacySourceDefaultsAndHiddenFrames() throws {
        var doc = document()
        doc.frameSettings = []
        let data = try PixelDocumentCodec.encode(doc)
        var root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var sprite = try XCTUnwrap(root["piskel"] as? [String: Any])
        sprite.removeValue(forKey: "unfold")
        root["piskel"] = sprite
        let legacy = try PixelDocumentCodec.decode(JSONSerialization.data(withJSONObject: root))
        XCTAssertEqual(legacy.duration(at: 1), 1 / 12.0)
        XCTAssertEqual(legacy.palette, PixelDocument.defaultPalette)
        XCTAssertEqual(legacy.playbackSequence, [0, 1, 2])
        sprite["hiddenFrames"] = [1]
        root["piskel"] = sprite
        let hidden = try PixelDocumentCodec.decode(JSONSerialization.data(withJSONObject: root))
        XCTAssertEqual(hidden.playbackSequence, [0, 2])
        sprite["hiddenFrames"] = [-1]
        root["piskel"] = sprite
        XCTAssertThrowsError(try PixelDocumentCodec.decode(JSONSerialization.data(withJSONObject: root)))
    }

    func testAllHiddenDocumentStillSavesSourceButRejectsAnimatedOutputs() throws {
        var doc = document()
        for index in doc.frameSettings.indices { doc.frameSettings[index].isVisible = false }
        XCTAssertEqual(try PixelDocumentCodec.decode(PixelDocumentCodec.encode(doc)), doc)
        XCTAssertThrowsError(try AnimatedGIFEncoder.encode(doc))
        XCTAssertThrowsError(try PixelDocumentCodec.savePayload(doc, characterID: nil))
    }

    func testLayerLockGuardsDirectDrawAndFlipWithoutChangingPixels() {
        var doc = document()
        doc.layers[0].frames[0].pixels[0] = 0xF00000FF
        doc.layers[0].isLocked = true
        let before = doc
        doc.draw(tool: .pencil, from: .init(x: 1, y: 1), to: .init(x: 4, y: 4),
                 color: 0xFFFFFFFF, brush: 1, layer: 0, frame: 0)
        doc.flip(layer: 0, frame: 0, horizontal: true)
        XCTAssertEqual(doc, before)
    }
}
