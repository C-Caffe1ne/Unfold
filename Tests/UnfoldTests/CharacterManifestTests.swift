import XCTest
@testable import Unfold

/// `CharacterManifest.AnimationDTO` must decode both the original
/// sprite-sheet-only shape (`frames`/`fps`, no `gif`) and the new
/// GIF-sourced shape (`gif`, no `frames`/`fps`) — decoding is pure and has
/// no I/O, so these run against in-memory JSON only.
final class CharacterManifestTests: XCTestCase {

    func test_decodesSpriteSheetAnimation_withoutGifField() throws {
        let json = """
        {
          "id": "x", "name": "X", "version": 1,
          "spriteSheet": {"file": "s.png", "columns": 8, "rows": 3, "frameWidth": 384, "frameHeight": 384},
          "animations": {
            "idle": {"frames": [0,1,2], "fps": 7, "loop": true}
          }
        }
        """
        let manifest = try JSONDecoder().decode(CharacterManifest.self, from: Data(json.utf8))
        let idle = try XCTUnwrap(manifest.animations["idle"])
        XCTAssertEqual(idle.frames, [0, 1, 2])
        XCTAssertEqual(idle.fps, 7)
        XCTAssertNil(idle.gif)
        XCTAssertTrue(idle.loop)
    }

    func test_decodesGifAnimation_withoutFramesOrFps() throws {
        let json = """
        {
          "id": "x", "name": "X", "version": 1,
          "spriteSheet": {"file": "s.png", "columns": 8, "rows": 3, "frameWidth": 384, "frameHeight": 384},
          "animations": {
            "stretch": {"gif": "stretch.gif", "loop": false}
          }
        }
        """
        let manifest = try JSONDecoder().decode(CharacterManifest.self, from: Data(json.utf8))
        let stretch = try XCTUnwrap(manifest.animations["stretch"])
        XCTAssertEqual(stretch.gif, "stretch.gif")
        XCTAssertNil(stretch.frames)
        XCTAssertNil(stretch.fps)
        XCTAssertFalse(stretch.loop)
    }

    // MARK: - renderStyle

    func test_loadImported_noRenderStyle_defaultsToSmooth() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .smooth)
    }

    func test_loadImported_pixelRenderStyle_isDecoded() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1, "renderStyle": "pixel",
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .pixel)
    }

    /// An unknown value must not fail loading — an older build reading a
    /// newer package should still show the character, just smoothed. This
    /// fallback is deliberate and non-fatal (`CharacterPackageLoader.
    /// makeRenderStyle(from:)` logs the anomaly instead); nobody should
    /// later "fix" it into a thrown error.
    func test_loadImported_unknownRenderStyle_fallsBackToSmooth() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1, "renderStyle": "hologram",
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .smooth)
    }

    /// Shared by the renderStyle cases above: writes `json` as
    /// `character.json` in a fresh temp directory and returns that directory.
    private static func writeTempManifest(_ json: String) throws -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-manifest-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try json.write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
        return dir
    }
}
