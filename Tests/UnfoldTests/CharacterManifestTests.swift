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
}
