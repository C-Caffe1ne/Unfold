import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

/// Builds small, deterministic animated GIFs at test time via `ImageIO`
/// (`CGImageDestination`), so `AnimatedImageClipLoaderTests` never depends
/// on downloading or committing binary GIF blobs. Every fixture here is
/// generated fresh into a temp file per call — cheap (a handful of pixels
/// per frame) and automatically cleaned up by the OS temp directory.
///
/// This file lives under `Tests/UnfoldTests/Fixtures/` per the Phase 3
/// plan; the "fixtures" are these builder functions plus the GIFs they
/// produce on demand, rather than pre-baked binary files, so every fixture
/// stays inspectable as plain Swift and never drifts from what the test
/// that uses it actually asserts.
enum GIFFixtureBuilder {

    struct RGBA: Equatable {
        let r: UInt8
        let g: UInt8
        let b: UInt8
        let a: UInt8

        static let red = RGBA(r: 255, g: 0, b: 0, a: 255)
        static let green = RGBA(r: 0, g: 255, b: 0, a: 255)
        static let blue = RGBA(r: 0, g: 0, b: 255, a: 255)
        static let white = RGBA(r: 255, g: 255, b: 255, a: 255)
        static let clear = RGBA(r: 0, g: 0, b: 0, a: 0)
    }

    /// One frame going into a fixture GIF: pixel content plus its own
    /// delay-time metadata (`nil` omits the key entirely, exercising the
    /// "missing duration" path against a real decoded source).
    struct FrameSpec {
        let pixel: (Int, Int) -> RGBA
        let delayTime: Double?
        let unclampedDelayTime: Double?

        /// A single flat color across the whole canvas.
        static func flat(_ color: RGBA, delayTime: Double? = 0.1, unclampedDelayTime: Double? = nil) -> FrameSpec {
            FrameSpec(pixel: { _, _ in color }, delayTime: delayTime, unclampedDelayTime: unclampedDelayTime ?? delayTime)
        }
    }

    // MARK: - Image construction

    static func makeImage(width: Int, height: Int, pixel: (Int, Int) -> RGBA) -> CGImage {
        var buffer = [UInt8](repeating: 0, count: width * height * 4)
        for y in 0..<height {
            for x in 0..<width {
                let color = pixel(x, y)
                let offset = (y * width + x) * 4
                buffer[offset] = color.r
                buffer[offset + 1] = color.g
                buffer[offset + 2] = color.b
                buffer[offset + 3] = color.a
            }
        }
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let context = CGContext(
            data: &buffer,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        return context.makeImage()!
    }

    /// Reads back a single pixel from a decoded frame, for fixtures that
    /// need to check *where* a color is (transparency/compositing tests),
    /// not just "the frame is flat color X". Renders the whole image into
    /// a same-size buffer using the exact same byte layout `makeImage`
    /// writes with (row-major, row 0 = top, premultiplied-last RGBA), then
    /// indexes directly — avoids relying on `CGContext.draw`'s coordinate
    /// flip behavior when scaling into a smaller destination.
    static func pixel(of image: CGImage, x: Int, y: Int) -> RGBA {
        let width = image.width
        let height = image.height
        var buffer = [UInt8](repeating: 0, count: width * height * 4)
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let context = CGContext(
            data: &buffer,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        // CGContext's drawing origin is bottom-left; drawing at (0,0) with
        // the context's full height places image row 0 at the *top* of the
        // context, matching `makeImage`'s row-major/top-down buffer layout.
        context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        let offset = (y * width + x) * 4
        return RGBA(r: buffer[offset], g: buffer[offset + 1], b: buffer[offset + 2], a: buffer[offset + 3])
    }

    // MARK: - GIF encoding

    /// Writes `frames` as an animated GIF to a fresh temp file and returns
    /// its URL. `loopCount == nil` omits the loop-count property entirely
    /// (no Netscape loop extension), matching a real "plays once" GIF.
    @discardableResult
    static func writeGIF(
        width: Int,
        height: Int,
        frames: [FrameSpec],
        loopCount: Int? = 0
    ) -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-test-gif-\(UUID().uuidString)")
            .appendingPathExtension("gif")

        let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.gif.identifier as CFString, frames.count, nil)!

        if let loopCount {
            let properties: [CFString: Any] = [
                kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: loopCount]
            ]
            CGImageDestinationSetProperties(destination, properties as CFDictionary)
        }

        for spec in frames {
            let image = makeImage(width: width, height: height, pixel: spec.pixel)
            var gifFrameProperties: [CFString: Any] = [:]
            if let delayTime = spec.delayTime {
                gifFrameProperties[kCGImagePropertyGIFDelayTime] = delayTime
            }
            if let unclampedDelayTime = spec.unclampedDelayTime {
                gifFrameProperties[kCGImagePropertyGIFUnclampedDelayTime] = unclampedDelayTime
            }
            let frameProperties: [CFString: Any] = gifFrameProperties.isEmpty
                ? [:]
                : [kCGImagePropertyGIFDictionary: gifFrameProperties]
            CGImageDestinationAddImage(destination, image, frameProperties as CFDictionary)
        }

        let success = CGImageDestinationFinalize(destination)
        precondition(success, "test fixture GIF failed to encode")
        return url
    }

    // MARK: - Named fixtures

    /// 4 frames, 4x4 canvas, distinct flat colors, uniform 0.1s delay,
    /// infinite loop (loopCount 0). The "everything is normal" case.
    static func basicFourFrame() -> URL {
        writeGIF(
            width: 4, height: 4,
            frames: [.flat(.red), .flat(.green), .flat(.blue), .flat(.white)],
            loopCount: 0
        )
    }

    /// 3 frames with different delays and no loop-count property at all —
    /// exercises variable-duration preservation and the "missing loop
    /// metadata" default in one real decoded source.
    static func variableDurationNoLoopMetadata() -> URL {
        writeGIF(
            width: 4, height: 4,
            frames: [
                .flat(.red, delayTime: 0.05, unclampedDelayTime: 0.05),
                .flat(.green, delayTime: 0.2, unclampedDelayTime: 0.2),
                .flat(.blue, delayTime: 0.05, unclampedDelayTime: 0.05),
            ],
            loopCount: nil
        )
    }

    /// Explicit finite repeat count (3) — exercises the "can't fully
    /// represent, simplify to non-looping" policy against a real source.
    static func finiteLoopCount(_ count: Int = 3) -> URL {
        writeGIF(width: 2, height: 2, frames: [.flat(.red), .flat(.green)], loopCount: count)
    }

    /// Frame 0 is opaque red across the whole canvas. Frame 1 is fully
    /// transparent except a 2x2 opaque green square in the top-left.
    /// `ImageIO`'s public `CGImageProperties` API has no GIF disposal-method
    /// key to set explicitly (checked against the SDK headers — only
    /// `kCGImagePropertyGIFLoopCount`/`DelayTime`/`UnclampedDelayTime`/
    /// `ImageColorMap`/`HasGlobalColorMap`/`CanvasPixelWidth`/`CanvasPixelHeight`/
    /// `FrameInfoArray` exist), so this exercises whatever disposal
    /// `CGImageDestination` picks by default for an alpha-carrying GIF
    /// frame — which is exactly the "does ImageIO composite for us"
    /// question this fixture exists to answer empirically.
    static func transparencyAndCompositing() -> URL {
        let canvas = 6
        let frame0 = FrameSpec.flat(.red, delayTime: 0.1, unclampedDelayTime: 0.1)
        let frame1 = FrameSpec(
            pixel: { x, y in (x < 2 && y < 2) ? .green : .clear },
            delayTime: 0.1,
            unclampedDelayTime: 0.1
        )
        return writeGIF(width: canvas, height: canvas, frames: [frame0, frame1], loopCount: 0)
    }

    /// Canvas wider than `AnimatedImageValidationPolicy.maxWidth`, single
    /// frame, trivial pixel content (stays tiny despite the pixel count).
    static func oversizedCanvas(width: Int = AnimatedImageValidationPolicy_maxWidthPlusOne, height: Int = 2) -> URL {
        writeGIF(width: width, height: height, frames: [.flat(.red)], loopCount: 0)
    }

    /// One more frame than `AnimatedImageValidationPolicy.maxFrameCount`,
    /// each a 1x1 canvas so total encoded size stays trivial.
    static func tooManyFrames(count: Int = AnimatedImageValidationPolicy_maxFrameCountPlusOne) -> URL {
        let colors: [RGBA] = [.red, .green, .blue, .white]
        let frames = (0..<count).map { FrameSpec.flat(colors[$0 % colors.count]) }
        return writeGIF(width: 1, height: 1, frames: frames, loopCount: 0)
    }

    /// A single-frame image encoded as a real PNG but saved at a `.gif`
    /// path — exercises rejecting a mistyped file by its real decoded
    /// type, not its extension.
    static func pngMisnamedAsGIF() -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-test-png-\(UUID().uuidString)")
            .appendingPathExtension("gif")
        let image = makeImage(width: 2, height: 2, pixel: { _, _ in .red })
        let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(destination, image, nil)
        precondition(CGImageDestinationFinalize(destination), "test fixture PNG failed to encode")
        return url
    }

    /// Not an image at all — arbitrary bytes at a `.gif` path.
    static func garbageBytes() -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-test-garbage-\(UUID().uuidString)")
            .appendingPathExtension("gif")
        try! Data([0x00, 0x01, 0x02, 0x42, 0x13, 0x37, 0xFF, 0xEE]).write(to: url)
        return url
    }

    /// A large-but-cheap-to-write file (zero bytes), for exercising the
    /// file-size gate against a real file on disk without committing a
    /// multi-megabyte binary anywhere.
    static func oversizedFile(bytes: Int = AnimatedImageValidationPolicy.maxFileSizeBytes + 1) -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-test-oversized-\(UUID().uuidString)")
            .appendingPathExtension("gif")
        FileManager.default.createFile(atPath: url.path, contents: Data(count: bytes))
        return url
    }
}

// Kept as top-level constants (rather than inline arithmetic at call sites)
// so every "just over the limit" fixture reads as obviously derived from
// the real policy, not a magic number that could quietly drift from it.
let AnimatedImageValidationPolicy_maxWidthPlusOne = AnimatedImageValidationPolicy.maxWidth + 8
let AnimatedImageValidationPolicy_maxFrameCountPlusOne = AnimatedImageValidationPolicy.maxFrameCount + 1
