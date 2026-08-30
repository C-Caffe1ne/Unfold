import XCTest
@testable import Unfold

/// The Desktop Pet's whole job is playing the current character's `.idle`
/// animation, reusing `CharacterAnimationView.makeAnimator` unchanged — this
/// locks that exact path against the real bundled `default-cat` package
/// (same category as `DefaultCatStretchGIFIntegrationTests`, for `.idle`
/// instead of `.stretch`).
@MainActor
final class DefaultCatIdleAnimationIntegrationTests: XCTestCase {

    func test_defaultCat_idleGif_loadsAndPlaysThroughSpriteAnimator() throws {
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .idle))

        XCTAssertNotNil(animator.currentFrame, "first frame must render immediately")
    }

    func test_defaultCat_idleAnimation_loopsIndefinitely_neverFinishes() throws {
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        let animator = try XCTUnwrap(CharacterAnimationView.makeAnimator(character: character, key: .idle))

        // idle is 8 frames, loop=true — advancing well past one full cycle
        // must keep wrapping, never finish (a Desktop Pet that "finishes"
        // idle would freeze on one pose forever).
        for _ in 0..<40 { animator.advance() }
        XCTAssertFalse(animator.isFinished)
        XCTAssertNotNil(animator.currentFrame)
    }
}
