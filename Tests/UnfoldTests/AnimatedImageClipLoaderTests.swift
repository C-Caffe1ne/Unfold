import XCTest
@testable import Unfold

/// Exercises `AnimatedImageClipLoader.load(url:)` end-to-end against real
/// GIFs encoded on the fly by `GIFFixtureBuilder` (via real `ImageIO`
/// decode) — as opposed to `AnimatedImageClipLoaderPolicyTests`, which
/// tests the pure decision functions with zero I/O.
final class AnimatedImageClipLoaderTests: XCTestCase {

    // MARK: - Frame count / order / dimensions

    func test_load_preservesFrameCount() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())
        XCTAssertEqual(clip.frames.count, 4)
    }

    func test_load_preservesFrameOrder() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())

        let colors = clip.frames.map { GIFFixtureBuilder.pixel(of: $0.image, x: 0, y: 0) }
        XCTAssertEqual(colors, [.red, .green, .blue, .white])
    }

    func test_load_preservesCGImageDimensions() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())

        for frame in clip.frames {
            XCTAssertEqual(frame.image.width, 4)
            XCTAssertEqual(frame.image.height, 4)
        }
    }

    // MARK: - Duration

    func test_load_preservesVariableDurations() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.variableDurationNoLoopMetadata())

        XCTAssertEqual(clip.frames.count, 3)
        XCTAssertEqual(clip.frames[0].duration, 0.05, accuracy: 0.0001)
        XCTAssertEqual(clip.frames[1].duration, 0.2, accuracy: 0.0001)
        XCTAssertEqual(clip.frames[2].duration, 0.05, accuracy: 0.0001)
    }

    // MARK: - Loop metadata

    func test_load_infiniteLoopGif_setsLoopTrue() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.basicFourFrame())
        XCTAssertTrue(clip.loop)
    }

    func test_load_missingLoopMetadata_setsLoopFalse() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.variableDurationNoLoopMetadata())
        XCTAssertFalse(clip.loop)
    }

    func test_load_finiteLoopCount_setsLoopFalse() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.finiteLoopCount(3))
        XCTAssertFalse(clip.loop)
    }

    // MARK: - Transparency + ImageIO compositing

    func test_load_decodesTransparency() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.transparencyAndCompositing())

        // The green square in frame 1 must actually be opaque green.
        let greenSquare = GIFFixtureBuilder.pixel(of: clip.frames[1].image, x: 0, y: 0)
        XCTAssertEqual(greenSquare.g, 255)
        XCTAssertEqual(greenSquare.a, 255)
    }

    /// The empirical check the plan calls for — and it disproves the
    /// convenient assumption. `ImageIO`'s public `CGImageDestination` API
    /// has no key to set a GIF frame's disposal method (checked against the
    /// SDK headers; see `GIFFixtureBuilder.transparencyAndCompositing`), so
    /// there's no way to construct, from public API alone, a true
    /// delta-encoded frame that relies on disposal="keep previous frame"
    /// to be complete. What this test *can* and does confirm:
    /// `CGImageSourceCreateImageAtIndex` returns frame 1 with exactly the
    /// pixel data (including alpha) it was encoded with — transparent
    /// stays transparent, it is not silently composited against frame 0.
    ///
    /// The consequence for `AnimatedImageClipLoader`: it's safe for
    /// self-contained frames (each frame fully specifies its own visible
    /// content — the common case for GIFs exported by most tools), but a
    /// GIF that leans on disposal="keep" delta-frame encoding would decode
    /// with genuinely missing pixels, since nothing composites it back in
    /// at any layer. That's a real, currently-unhandled limitation — see
    /// the Phase 3 report, not a false alarm this test papers over.
    func test_load_returnsEachFramesOwnPixelsAsEncoded_withoutInventingCompositing() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.transparencyAndCompositing())

        let outsideGreenSquare = GIFFixtureBuilder.pixel(of: clip.frames[1].image, x: 4, y: 4)
        XCTAssertEqual(outsideGreenSquare, .clear, "frame 1 was encoded fully transparent outside the green square")
    }

    // MARK: - Canvas consistency

    func test_load_allFramesShareTheSameCanvasSize() throws {
        let clip = try AnimatedImageClipLoader.load(url: GIFFixtureBuilder.transparencyAndCompositing())

        for frame in clip.frames {
            XCTAssertEqual(frame.image.width, 6)
            XCTAssertEqual(frame.image.height, 6)
        }
    }

    // MARK: - Validation limits

    func test_load_throwsCanvasTooLarge_whenWidthExceedsLimit() {
        let url = GIFFixtureBuilder.oversizedCanvas()
        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            guard case .canvasTooLarge = error as? AnimatedImageClipLoader.LoadError else {
                return XCTFail("expected .canvasTooLarge, got \(error)")
            }
        }
    }

    func test_load_throwsTooManyFrames_whenFrameCountExceedsLimit() {
        let url = GIFFixtureBuilder.tooManyFrames()
        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            XCTAssertEqual(
                error as? AnimatedImageClipLoader.LoadError,
                .tooManyFrames(count: AnimatedImageValidationPolicy.maxFrameCount + 1)
            )
        }
    }

    func test_load_throwsFileTooLarge_forRealOversizedFileOnDisk() {
        let url = GIFFixtureBuilder.oversizedFile()
        defer { try? FileManager.default.removeItem(at: url) }

        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            guard case .fileTooLarge = error as? AnimatedImageClipLoader.LoadError else {
                return XCTFail("expected .fileTooLarge, got \(error)")
            }
        }
    }

    // MARK: - Format / integrity rejection

    func test_load_throwsUnsupportedFormat_forRealImageOfTheWrongType() {
        // A real, validly-decodable PNG — just saved at a `.gif` path.
        // Must be rejected by its actual decoded type, not its extension.
        let url = GIFFixtureBuilder.pngMisnamedAsGIF()
        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            XCTAssertEqual(error as? AnimatedImageClipLoader.LoadError, .unsupportedFormat)
        }
    }

    func test_load_throwsUnreadableSource_forGarbageBytes() {
        let url = GIFFixtureBuilder.garbageBytes()
        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            XCTAssertEqual(error as? AnimatedImageClipLoader.LoadError, .unreadableSource)
        }
    }

    func test_load_throwsUnreadableSource_forMissingFile() {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("does-not-exist-\(UUID().uuidString).gif")
        XCTAssertThrowsError(try AnimatedImageClipLoader.load(url: url)) { error in
            XCTAssertEqual(error as? AnimatedImageClipLoader.LoadError, .unreadableSource)
        }
    }
}
