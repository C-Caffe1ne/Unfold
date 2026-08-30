import XCTest
@testable import Unfold

/// `CharacterPackageLoader.loadImported` against synthetic temp packages —
/// exercises the real file-reading path (not just JSON decoding), covering
/// both animation sources (`AnimationSource.spriteSheet`/`.gif`) and their
/// validation rules. Uses `loadImported` (not `loadBuiltIn`) so these don't
/// depend on the bundled `default-cat` package's exact contents.
final class CharacterPackageLoaderTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-charpkg-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
    }

    private func writeManifest(_ json: String) throws {
        try json.write(to: tempDir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
    }

    private static let baseSpriteSheet = """
    "spriteSheet": {"file": "s.png", "columns": 8, "rows": 3, "frameWidth": 384, "frameHeight": 384}
    """

    // MARK: - Sprite-sheet source (regression)

    func test_loadImported_spriteSheetAnimation_resolvesToSpriteSheetSource() throws {
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"frames": [0,1,2], "fps": 7, "loop": true} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        guard case .spriteSheet(let definition) = character.animation(for: .idle) else {
            return XCTFail("expected .spriteSheet source")
        }
        XCTAssertEqual(definition.frames, [0, 1, 2])
        XCTAssertEqual(definition.fps, 7)
        XCTAssertTrue(definition.loop)
    }

    // MARK: - GIF source

    func test_loadImported_gifAnimation_resolvesToGifSource() throws {
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"gif": "stretch.gif", "loop": false} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        guard case .gif(let fileName, let loop) = character.animation(for: .stretch) else {
            return XCTFail("expected .gif source")
        }
        XCTAssertEqual(fileName, "stretch.gif")
        XCTAssertFalse(loop)
    }

    func test_loadImported_gifAnimation_withLoopTrue_isPreserved() throws {
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"gif": "idle.gif", "loop": true} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        guard case .gif(_, let loop) = character.animation(for: .idle) else {
            return XCTFail("expected .gif source")
        }
        XCTAssertTrue(loop)
    }

    func test_loadImported_gifAnimation_takesPrecedence_whenFramesAlsoPresent() throws {
        // An animation must not carry both a sprite-sheet definition and a
        // GIF at once — `gif` wins when both happen to be present, so a
        // malformed manifest fails predictably rather than ambiguously.
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"frames": [8,9], "fps": 11, "gif": "stretch.gif", "loop": false} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        guard case .gif(let fileName, _) = character.animation(for: .stretch) else {
            return XCTFail("expected .gif source to win over frames")
        }
        XCTAssertEqual(fileName, "stretch.gif")
    }

    // MARK: - Validation failures

    func test_loadImported_throws_whenGifFileNameIsEmpty() throws {
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"gif": "", "loop": false} }
        }
        """)
        XCTAssertThrowsError(try CharacterPackageLoader.loadImported(packageDirectory: tempDir)) { error in
            guard case CharacterPackageLoader.ValidationError.invalidAnimation(let key) = error else {
                return XCTFail("expected .invalidAnimation, got \(error)")
            }
            XCTAssertEqual(key, "stretch")
        }
    }

    func test_loadImported_throws_whenAnimationHasNeitherFramesNorGif() throws {
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"loop": false} }
        }
        """)
        XCTAssertThrowsError(try CharacterPackageLoader.loadImported(packageDirectory: tempDir)) { error in
            guard case CharacterPackageLoader.ValidationError.invalidAnimation(let key) = error else {
                return XCTFail("expected .invalidAnimation, got \(error)")
            }
            XCTAssertEqual(key, "stretch")
        }
    }

    func test_loadImported_stillThrows_forOutOfRangeSpriteSheetFrames() throws {
        // Regression: sprite-sheet frame-range validation must survive the
        // DTO becoming optional-frames.
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"frames": [0, 999], "fps": 7, "loop": true} }
        }
        """)
        XCTAssertThrowsError(try CharacterPackageLoader.loadImported(packageDirectory: tempDir)) { error in
            guard case CharacterPackageLoader.ValidationError.invalidAnimation(let key) = error else {
                return XCTFail("expected .invalidAnimation, got \(error)")
            }
            XCTAssertEqual(key, "idle")
        }
    }
}
