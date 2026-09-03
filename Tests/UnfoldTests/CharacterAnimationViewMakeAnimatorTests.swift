import XCTest
@testable import Unfold

/// Exercises `CharacterAnimationView.makeAnimator` — the seam that routes a
/// `Character`'s animation to either `AnimationClipLoader` (sprite sheet) or
/// `AnimatedImageClipLoader` (GIF) and hands the result to the *same*,
/// unmodified `SpriteAnimator`. `makeAnimator` is `static` (not `private`)
/// specifically so this can call it directly without any SwiftUI view
/// lifecycle plumbing — the same reasoning `SpriteAnimator.advance()` was
/// made internal for in Phase 2.
@MainActor
final class CharacterAnimationViewMakeAnimatorTests: XCTestCase {

    private var tempDir: URL!

    override func setUpWithError() throws {
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-charview-test-\(UUID().uuidString)")
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

    /// `basicFourFrame()` is encoded with GIF loopCount 0 (infinite) — used
    /// deliberately so tests can prove the manifest's `loop` value overrides
    /// it, in both directions.
    private func installGifFixture(named fileName: String) throws {
        let source = GIFFixtureBuilder.basicFourFrame()
        try FileManager.default.copyItem(at: source, to: tempDir.appendingPathComponent(fileName))
    }

    func test_gifSourcedAnimation_playsThroughSpriteAnimator() throws {
        try installGifFixture(named: "stretch.gif")
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"gif": "stretch.gif", "loop": false} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)

        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .stretch))
        XCTAssertNotNil(animator.currentFrame)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
    }

    func test_gifSourcedAnimation_manifestLoopFalse_overridesGifsOwnInfiniteLoopMetadata() throws {
        try installGifFixture(named: "stretch.gif")
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "stretch": {"gif": "stretch.gif", "loop": false} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .stretch))

        // basicFourFrame() has 4 frames and GIF loopCount 0 (infinite); if
        // the override didn't take effect this would wrap forever instead
        // of finishing.
        for _ in 0..<4 { animator.advance() }
        XCTAssertTrue(animator.isFinished, "manifest loop=false must win over the GIF's own infinite-loop metadata")
    }

    func test_gifSourcedAnimation_manifestLoopTrue_keepsLoopingPastGifFrameCount() throws {
        try installGifFixture(named: "idle.gif")
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"gif": "idle.gif", "loop": true} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .idle))

        for _ in 0..<4 { animator.advance() } // wraps back to frame 0
        XCTAssertFalse(animator.isFinished)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
    }

    func test_spriteSheetSourcedAnimation_stillWorks_unaffectedByGifRouting() throws {
        // Regression: routing logic must not disturb the pre-existing
        // sprite-sheet path for a character that defines no GIF animations.
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"frames": [0,1,2], "fps": 7, "loop": true} }
        }
        """)
        // No real spritesheet.png on disk -> CharacterAssetLoader.loadSpriteSheetImage
        // returns nil (image can't be read), which makeAnimator handles the
        // same way it always has: return nil rather than crash.
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        XCTAssertNil(CharacterAnimationView.makeAnimator(character: character, key: .idle))
    }

    /// The fallback, proven at the seam the view actually calls — not just
    /// on the model. A character that only drew an idle loop must still
    /// produce a playable animator when the app asks for `.stretch`.
    func test_missingAnimationKey_fallsBackToIdle() throws {
        try installGifFixture(named: "idle.gif")
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "idle": {"gif": "idle.gif", "loop": true} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)

        let animator = try XCTUnwrap(
            CharacterAnimationView.makeAnimator(character: character, key: .stretch),
            "a character with only an idle clip must still animate for .stretch"
        )
        XCTAssertNotNil(animator.currentFrame)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
    }

    /// No match and no idle to fall back to — the only case that still
    /// legitimately yields `nil`. Uses a loadable GIF so the result can't
    /// be confounded by a missing asset file, which is what made the
    /// previous version of this test pass for the wrong reason.
    func test_missingAnimationKeyAndNoIdle_returnsNil() throws {
        try installGifFixture(named: "yawn.gif")
        try writeManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          \(Self.baseSpriteSheet),
          "animations": { "yawn": {"gif": "yawn.gif", "loop": true} }
        }
        """)
        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        XCTAssertNil(CharacterAnimationView.makeAnimator(character: character, key: .stretch))
    }
}
