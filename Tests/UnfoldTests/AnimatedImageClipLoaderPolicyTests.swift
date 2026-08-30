import XCTest
@testable import Unfold

/// Tests the pure, deterministic decision logic `AnimatedImageClipLoader`
/// applies to raw GIF metadata — duration resolution/clamping, loop
/// policy, file-size gating. Deliberately kept separate from real
/// ImageIO decode paths (see `AnimatedImageClipLoaderTests`) so these run
/// with zero I/O and zero dependence on how a specific GIF happens to be
/// encoded.
final class AnimatedImageClipLoaderPolicyTests: XCTestCase {

    // MARK: - Frame duration: source priority

    func test_resolveFrameDuration_prefersUnclampedOverClamped() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: 0.3, delayTime: 0.05)
        XCTAssertEqual(duration, 0.3)
    }

    func test_resolveFrameDuration_fallsBackToClamped_whenUnclampedMissing() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: nil, delayTime: 0.3)
        XCTAssertEqual(duration, 0.3)
    }

    // MARK: - Frame duration: validation policy

    func test_resolveFrameDuration_fallsBackTo0_1_whenBothMissing() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: nil, delayTime: nil)
        XCTAssertEqual(duration, 0.1)
    }

    func test_resolveFrameDuration_fallsBackTo0_1_whenNaN() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: .nan, delayTime: nil)
        XCTAssertEqual(duration, 0.1)
    }

    func test_resolveFrameDuration_fallsBackTo0_1_whenInfinite() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: .infinity, delayTime: nil)
        XCTAssertEqual(duration, 0.1)
    }

    func test_resolveFrameDuration_fallsBackTo0_1_whenZero() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: 0, delayTime: nil)
        XCTAssertEqual(duration, 0.1)
    }

    func test_resolveFrameDuration_fallsBackTo0_1_whenNegative() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: -1, delayTime: nil)
        XCTAssertEqual(duration, 0.1)
    }

    func test_resolveFrameDuration_clampsUpTo0_02_whenShortButPositive() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: 0.01, delayTime: nil)
        XCTAssertEqual(duration, 0.02, "a real 10ms animation must not be forced all the way down to the 100ms fallback")
    }

    func test_resolveFrameDuration_keepsValueUnchanged_atExactlyTheMinimum() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: 0.02, delayTime: nil)
        XCTAssertEqual(duration, 0.02)
    }

    func test_resolveFrameDuration_keepsOriginalValue_whenAboveMinimum() {
        let duration = AnimatedImageClipLoader.resolveFrameDuration(unclampedDelayTime: 0.15, delayTime: nil)
        XCTAssertEqual(duration, 0.15)
    }

    // MARK: - Loop policy

    func test_resolveLoop_true_whenGifLoopCountIsZero() {
        XCTAssertTrue(AnimatedImageClipLoader.resolveLoop(gifLoopCount: 0))
    }

    func test_resolveLoop_false_whenLoopCountMissing() {
        // No Netscape loop extension at all — conventionally a "plays once"
        // GIF, not an infinitely-looping one.
        XCTAssertFalse(AnimatedImageClipLoader.resolveLoop(gifLoopCount: nil))
    }

    func test_resolveLoop_false_forFiniteRepeatCount() {
        // AnimationClip.loop is a Bool; it cannot represent "repeat exactly
        // N times." Recommended policy: simplify a finite count to
        // non-looping rather than silently looping forever.
        XCTAssertFalse(AnimatedImageClipLoader.resolveLoop(gifLoopCount: 3))
    }

    // MARK: - File size gate

    func test_validateFileSize_passesAtExactlyTheLimit() {
        XCTAssertNoThrow(try AnimatedImageClipLoader.validateFileSize(bytes: AnimatedImageValidationPolicy.maxFileSizeBytes))
    }

    func test_validateFileSize_throwsFileTooLarge_aboveTheLimit() {
        let oversize = AnimatedImageValidationPolicy.maxFileSizeBytes + 1
        XCTAssertThrowsError(try AnimatedImageClipLoader.validateFileSize(bytes: oversize)) { error in
            XCTAssertEqual(error as? AnimatedImageClipLoader.LoadError, .fileTooLarge(bytes: oversize))
        }
    }
}
