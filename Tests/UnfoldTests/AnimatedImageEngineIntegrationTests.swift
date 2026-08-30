import XCTest
@testable import Unfold

/// Confirms a `AnimatedImageClipLoader`-produced `AnimationClip` plays
/// correctly through the *existing*, unmodified `SpriteAnimator` — the
/// whole point of `AnimationClip` being format-agnostic. No changes to
/// `SpriteAnimator` were needed or made for this Phase.
@MainActor
final class AnimatedImageEngineIntegrationTests: XCTestCase {

    func test_gifDerivedClip_showsFirstFrameImmediately() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())
        let animator = SpriteAnimator(clip: clip)

        XCTAssertNotNil(animator.currentFrame)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
    }

    func test_gifDerivedClip_withVariableDurations_playsThroughAllFramesInOrder() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.variableDurationNoLoopMetadata())
        let animator = SpriteAnimator(clip: clip)

        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
        animator.advance()
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .green)
        animator.advance()
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .blue)
    }

    func test_gifDerivedClip_missingLoopMetadata_finishesAndHoldsLastFrame() throws {
        // variableDurationNoLoopMetadata() has no loop-count property -> loop == false.
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.variableDurationNoLoopMetadata())
        XCTAssertFalse(clip.loop)
        let animator = SpriteAnimator(clip: clip)

        animator.advance()
        animator.advance()
        XCTAssertFalse(animator.isFinished)
        animator.advance() // past the last frame

        XCTAssertTrue(animator.isFinished)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .blue, "holds the last frame")
    }

    func test_gifDerivedClip_infiniteLoop_wrapsBackToFirstFrame() throws {
        // basicFourFrame() has loopCount 0 -> loop == true.
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())
        XCTAssertTrue(clip.loop)
        let animator = SpriteAnimator(clip: clip)

        animator.advance() // green
        animator.advance() // blue
        animator.advance() // white (last)
        animator.advance() // wraps

        XCTAssertFalse(animator.isFinished)
        XCTAssertEqual(GIFFixtureBuilder.pixel(of: animator.currentFrame!, x: 0, y: 0), .red)
    }
}
