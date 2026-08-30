import XCTest
@testable import Unfold

/// Exercises `AnimatedImageClipLoader` + `SpriteAnimator` against a real,
/// user-authored GIF instead of a synthetic `GIFFixtureBuilder` fixture —
/// the "does this survive contact with a real encoder's output" check the
/// Phase 4 investigation asked for, kept separate from the fixture-based
/// `AnimatedImageClipLoaderTests` on purpose.
///
/// The file itself (`마리.gif`, at the repo root, not committed as a
/// production or Character asset) is a local-only investigation input — see
/// the Phase 4 completion report. It is NOT the actual Mochi "stretch.gif":
/// decoding it confirmed its visible content is an unrelated cursor/character
/// graphic, not a stretch animation. These tests exist to validate the
/// *pipeline* against a real file's encoding quirks, not to certify this
/// specific file's content for production use. `XCTSkipUnless` keeps this
/// suite green on any checkout where the local file isn't present (it is
/// deliberately not part of the committed fixture set).
final class AnimatedImageClipLoaderRealAssetTests: XCTestCase {

    private static var realAssetURL: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent() // drop filename -> .../Tests/UnfoldTests
            .deletingLastPathComponent() // drop UnfoldTests -> .../Tests
            .deletingLastPathComponent() // drop Tests -> repo root
            .appendingPathComponent("마리.gif")
    }

    private func loadRealAssetOrSkip() throws -> URL {
        let url = Self.realAssetURL
        try XCTSkipUnless(
            FileManager.default.fileExists(atPath: url.path),
            "real GIF investigation input not present at \(url.path) on this checkout"
        )
        return url
    }

    func test_realAsset_decodesSuccessfully() throws {
        let url = try loadRealAssetOrSkip()
        XCTAssertNoThrow(try AnimatedImageClipLoader.load(url: url))
    }

    func test_realAsset_frameCountMatchesEmpiricalInspection() throws {
        let url = try loadRealAssetOrSkip()
        let clip = try AnimatedImageClipLoader.load(url: url)
        XCTAssertEqual(clip.frames.count, 5)
    }

    func test_realAsset_allFramesShareTheSame192x192Canvas() throws {
        let url = try loadRealAssetOrSkip()
        let clip = try AnimatedImageClipLoader.load(url: url)
        for frame in clip.frames {
            XCTAssertEqual(frame.image.width, 192)
            XCTAssertEqual(frame.image.height, 192)
        }
    }

    func test_realAsset_everyFrameDurationIs0_15Seconds() throws {
        let url = try loadRealAssetOrSkip()
        let clip = try AnimatedImageClipLoader.load(url: url)
        for frame in clip.frames {
            XCTAssertEqual(frame.duration, 0.15, accuracy: 0.0001)
        }
    }

    func test_realAsset_loopCountZero_resolvesToLoopTrue() throws {
        let url = try loadRealAssetOrSkip()
        let clip = try AnimatedImageClipLoader.load(url: url)
        XCTAssertTrue(clip.loop, "GIF's own LoopCount=0 metadata means infinite loop — see report §7 on why that's unsuitable for a one-shot production stretch animation")
    }

    @MainActor
    func test_realAsset_playsThroughUnmodifiedSpriteAnimator_andWrapsOnLoop() throws {
        let url = try loadRealAssetOrSkip()
        let clip = try AnimatedImageClipLoader.load(url: url)
        let animator = SpriteAnimator(clip: clip)

        XCTAssertNotNil(animator.currentFrame)
        for _ in 0..<clip.frames.count {
            animator.advance()
        }
        // 5 frames, loop == true: 5 advances from frame 0 lands back on frame 0.
        XCTAssertFalse(animator.isFinished, "a loop==true clip must never report finished")
    }
}
