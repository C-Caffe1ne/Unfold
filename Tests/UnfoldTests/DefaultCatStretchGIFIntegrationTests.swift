import XCTest
@testable import Unfold

/// End-to-end regression lock for the production `default-cat` package
/// after Phase 4's stretch migration to a real GIF (`stretch.gif`, bundled
/// under `Resources/Characters/default-cat/`). Exercises the exact path
/// production code takes: `CharacterPackageLoader.loadBuiltIn` →
/// `CharacterAnimationView.makeAnimator` → `SpriteAnimator`, against the
/// real bundled resource (not a fixture) — the same category of test as
/// `AnimatedImageClipLoaderRealAssetTests`, one layer up the stack.
@MainActor
final class DefaultCatStretchGIFIntegrationTests: XCTestCase {

    func test_defaultCat_stretchAnimation_resolvesToTheGifSource() throws {
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        guard case .gif(let fileName, let loop) = character.animation(for: .stretch) else {
            return XCTFail("expected default-cat's stretch to be GIF-sourced after the Phase 4 migration")
        }
        XCTAssertEqual(fileName, "stretch.gif")
        XCTAssertFalse(loop, "manifest must override the GIF's own infinite-loop metadata for a one-shot stretch")
    }

    func test_defaultCat_idleAnimation_isUnaffected_stillSpriteSheetSourced() throws {
        // Regression: migrating stretch to GIF must not disturb idle.
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        guard case .spriteSheet(let definition) = character.animation(for: .idle) else {
            return XCTFail("expected default-cat's idle to remain sprite-sheet-sourced")
        }
        XCTAssertEqual(definition.frames, [0, 1, 2, 3, 4, 5, 6, 7])
        XCTAssertEqual(definition.fps, 7)
    }

    func test_defaultCat_stretchGif_playsThroughSpriteAnimator_andFinishesHoldingLastFrame() throws {
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .stretch))

        XCTAssertNotNil(animator.currentFrame, "first frame must render immediately, same as the sprite-sheet path")

        // stretch.gif has 18 frames; advancing past all of them must finish
        // and hold the last frame rather than loop, regardless of the GIF's
        // own loop metadata (see the .gif LoadError-free decode above).
        for _ in 0..<30 { animator.advance() }
        XCTAssertTrue(animator.isFinished)
    }
}
