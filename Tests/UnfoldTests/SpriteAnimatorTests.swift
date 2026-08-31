import Combine
import XCTest
@testable import Unfold

@MainActor
final class SpriteAnimatorTests: XCTestCase {

    private func frame(_ cell: TestSpriteSheet.Cell, duration: TimeInterval = 1) -> AnimationFrame {
        AnimationFrame(image: TestSpriteSheet.makeImage(cell), duration: duration)
    }

    private func color(of animator: SpriteAnimator) -> TestSpriteSheet.Cell? {
        animator.currentFrame.map(TestSpriteSheet.color(of:))
    }

    // MARK: - Off-by-one on init

    func test_init_rendersFirstFrameImmediately_beforePlayIsEverCalled() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        XCTAssertEqual(color(of: animator), .red)
        XCTAssertFalse(animator.isFinished)
    }

    func test_init_withEmptyClip_currentFrameIsNil() {
        let clip = AnimationClip(frames: [], loop: true)
        let animator = SpriteAnimator(clip: clip)

        XCTAssertNil(animator.currentFrame)
    }

    // MARK: - Deterministic frame-order / loop / finish, via direct advance()

    func test_advance_movesThroughFramesInOrder() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green), frame(.blue)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        animator.advance()
        XCTAssertEqual(color(of: animator), .green)

        animator.advance()
        XCTAssertEqual(color(of: animator), .blue)
    }

    func test_advance_loopsBackToFirstFrame_whenLoopTrue() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        animator.advance() // -> green (index 1, last)
        animator.advance() // -> wraps back to red (index 0)

        XCTAssertEqual(color(of: animator), .red)
        XCTAssertFalse(animator.isFinished)
    }

    func test_advance_staysOnLastFrame_andSetsIsFinished_whenLoopFalse() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: false)
        let animator = SpriteAnimator(clip: clip)

        animator.advance() // -> green (index 1, last)
        XCTAssertFalse(animator.isFinished)

        animator.advance() // would-be index 2, past the end
        XCTAssertEqual(color(of: animator), .green, "non-looping clip holds its last frame")
        XCTAssertTrue(animator.isFinished)

        animator.advance() // calling advance again past finish must not move further or crash
        XCTAssertEqual(color(of: animator), .green)
        XCTAssertTrue(animator.isFinished)
    }

    // MARK: - play() / stop() timer lifecycle (real Timer, short synthetic durations)

    func test_play_advancesFramesAutomatically() {
        let clip = AnimationClip(frames: [frame(.red, duration: 0.02), frame(.green, duration: 0.02)], loop: false)
        let animator = SpriteAnimator(clip: clip)

        animator.play()

        let sawGreen = expectation(description: "advances to green frame")
        let cancellable = animator.$currentFrame.sink { image in
            if let image, TestSpriteSheet.color(of: image) == .green {
                sawGreen.fulfill()
            }
        }
        wait(for: [sawGreen], timeout: 1.0)
        cancellable.cancel()
        animator.stop()
    }

    func test_stop_preventsFurtherAdvance() {
        let clip = AnimationClip(frames: [frame(.red, duration: 0.02), frame(.green, duration: 0.02)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        animator.play()
        animator.stop()

        // Wait well past the frame duration; if stop() failed to cancel the
        // timer, this would observe the frame having advanced to green.
        RunLoop.main.run(until: Date().addingTimeInterval(0.1))

        XCTAssertEqual(color(of: animator), .red)
    }

    func test_play_isNoOp_whenClipHasNoFrames() {
        let clip = AnimationClip(frames: [], loop: true)
        let animator = SpriteAnimator(clip: clip)

        animator.play() // must not crash
        RunLoop.main.run(until: Date().addingTimeInterval(0.05))

        XCTAssertNil(animator.currentFrame)
        XCTAssertFalse(animator.isFinished)
    }

    // MARK: - Replay after finishing (stretch: play() again restarts from frame 0)

    func test_play_replaysFromStart_afterNonLoopingClipFinished() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: false)
        let animator = SpriteAnimator(clip: clip)

        animator.advance() // -> green
        animator.advance() // -> finished, holding green
        XCTAssertTrue(animator.isFinished)

        animator.play()

        XCTAssertFalse(animator.isFinished)
        XCTAssertEqual(color(of: animator), .red, "play() after finishing must restart from the first frame")

        animator.stop()
    }

    func test_play_doesNotRestart_whenStoppedMidPlaybackWithoutFinishing() {
        // stop() mid-playback is a pause, not a finish — resuming with
        // play() again should continue from where it left off, not jump
        // back to frame 0. Only a genuinely *finished* clip restarts.
        let clip = AnimationClip(frames: [frame(.red), frame(.green), frame(.blue)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        animator.advance() // -> green
        animator.stop()
        XCTAssertFalse(animator.isFinished)

        animator.play()

        XCTAssertEqual(color(of: animator), .green, "play() after a mid-playback stop() resumes, it does not restart")

        animator.stop()
    }

    // MARK: - onFinished completion callback (Desktop Pet Phase 2)

    func test_onFinished_isCalledExactlyOnce_whenNonLoopingClipFinishes() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: false)
        let animator = SpriteAnimator(clip: clip)

        var callCount = 0
        animator.onFinished = { callCount += 1 }

        animator.advance() // -> green, not finished yet
        XCTAssertEqual(callCount, 0)

        animator.advance() // -> finishes, holding green
        XCTAssertEqual(callCount, 1)

        animator.advance() // already finished: must not fire again
        XCTAssertEqual(callCount, 1)
    }

    func test_onFinished_isNotCalled_forLoopingClip() {
        let clip = AnimationClip(frames: [frame(.red), frame(.green)], loop: true)
        let animator = SpriteAnimator(clip: clip)

        var callCount = 0
        animator.onFinished = { callCount += 1 }

        animator.advance()
        animator.advance() // wraps back to red
        animator.advance()

        XCTAssertEqual(callCount, 0)
    }

    func test_onFinished_defaultsToNil_existingConsumersAreUnaffected() {
        let clip = AnimationClip(frames: [frame(.red)], loop: false)
        let animator = SpriteAnimator(clip: clip)

        XCTAssertNil(animator.onFinished)
        animator.advance() // must not crash with no callback set
        XCTAssertTrue(animator.isFinished)
    }
}
