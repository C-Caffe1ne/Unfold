import XCTest
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

final class AnimatedGIFEncoderTests: XCTestCase {

    // MARK: - Builders

    private func makeDocument(width: Int = 8, height: Int = 8, frames: Int = 1) -> PixelDocument {
        var document = PixelDocument(width: width, height: height)
        // A `while document.frameCount < frames` loop would spin forever if
        // `insertFrame` ever refused the growth, so the bound is a range.
        for _ in 1..<max(1, frames) {
            document.insertFrame(after: document.frameCount - 1, duplicate: false)
        }
        return document
    }

    /// Opaque colours that are distinct for every index, so a caller can ask
    /// for "n different colours" without hand-picking them.
    private func colour(_ index: Int) -> UInt32 {
        UInt32((index % 256) << 24 | (index / 256) << 16 | 0xFF)
    }

    /// A document whose frames hold disjoint runs of distinct opaque colours,
    /// every remaining pixel left transparent.
    private func makeColourfulDocument(frames: Int, coloursPerFrame: Int) -> PixelDocument {
        var document = makeDocument(width: 32, height: 32, frames: frames)
        var next = 0
        for frame in 0..<document.frameCount {
            for pixel in 0..<coloursPerFrame {
                document.layers[0].frames[frame].pixels[pixel] = colour(next)
                next += 1
            }
        }
        return document
    }

    // MARK: - Readers

    private func source(_ data: Data) throws -> CGImageSource {
        try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
    }

    private func gifProperties(_ source: CGImageSource, frame: Int?) throws -> [CFString: Any] {
        let properties = frame.map { CGImageSourceCopyPropertiesAtIndex(source, $0, nil) }
            ?? CGImageSourceCopyProperties(source, nil)
        let container = try XCTUnwrap(properties as? [CFString: Any])
        return try XCTUnwrap(container[kCGImagePropertyGIFDictionary] as? [CFString: Any])
    }

    /// Decodes one GIF frame back to straight-alpha RGBA, the same way
    /// `RasterImageDecoder.pixels(of:width:height:)` does: draw into a
    /// premultiplied sRGB context with interpolation off, then divide the
    /// premultiplication back out.
    private func decode(_ data: Data, frame: Int = 0) throws -> [UInt32] {
        let image = try XCTUnwrap(CGImageSourceCreateImageAtIndex(try source(data), frame, nil))
        let width = image.width, height = image.height
        var bytes = Array(repeating: UInt8(0), count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else { return false }
            context.interpolationQuality = .none
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        XCTAssertTrue(drawn)
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for index in pixels.indices {
            let offset = index * 4
            let alpha = UInt32(bytes[offset + 3])
            guard alpha > 0 else { continue }
            func channel(_ i: Int) -> UInt32 { min(255, (UInt32(bytes[offset + i]) * 255 + alpha / 2) / alpha) }
            pixels[index] = channel(0) << 24 | channel(1) << 16 | channel(2) << 8 | alpha
        }
        return pixels
    }

    // MARK: - Encoding

    func test_encodesEveryFrameOfTheDocument() throws {
        var document = makeDocument(frames: 5)
        XCTAssertEqual(document.frameCount, 5)
        document.layers[0].frames[2].pixels[0] = 0x00FF00FF
        let source = try source(try AnimatedGIFEncoder.encode(document))
        XCTAssertEqual(CGImageSourceGetType(source) as String?, UTType.gif.identifier)
        XCTAssertEqual(CGImageSourceGetCount(source), 5)
    }

    /// FPS 10 rather than the document default of 12: GIF stores a delay in
    /// hundredths of a second, so 1/12 comes back as 0.08 and only a rate
    /// that divides 100 evenly round-trips exactly.
    func test_frameDelayComesFromTheDocumentFPS() throws {
        // Both rates divide 100 evenly, and they differ, so the assertion
        // pins the delay to the document rather than to a constant.
        for (fps, expected) in [(10.0, 0.1), (4.0, 0.25)] {
            var document = makeDocument(frames: 3)
            document.fps = fps
            let source = try source(try AnimatedGIFEncoder.encode(document))
            for frame in 0..<CGImageSourceGetCount(source) {
                let gif = try gifProperties(source, frame: frame)
                let delay = try XCTUnwrap(gif[kCGImagePropertyGIFDelayTime] as? Double)
                let unclamped = try XCTUnwrap(gif[kCGImagePropertyGIFUnclampedDelayTime] as? Double)
                XCTAssertEqual(delay, expected, accuracy: 0.0001, "clamped delay at \(fps) fps")
                XCTAssertEqual(unclamped, expected, accuracy: 0.0001, "unclamped delay at \(fps) fps")
            }
        }
    }

    func test_loopsForever() throws {
        let source = try source(try AnimatedGIFEncoder.encode(makeDocument(frames: 2)))
        XCTAssertEqual(try gifProperties(source, frame: nil)[kCGImagePropertyGIFLoopCount] as? Int, 0)
    }

    /// The guarantee that matters most for a sprite: a character drawn over
    /// nothing still has nothing behind it after the export.
    func test_transparencyRoundTrips() throws {
        var document = makeDocument()
        document.layers[0].frames[0].pixels = document.layers[0].frames[0].pixels.map { _ in 0xFF0000FF }
        document.layers[0].frames[0].pixels[0] = 0

        let decoded = try decode(try AnimatedGIFEncoder.encode(document))
        XCTAssertEqual(decoded.count, 64)
        XCTAssertEqual(decoded[0] & 255, 0, "the transparent pixel came back opaque")
        XCTAssertEqual(decoded[1], 0xFF0000FF, "the opaque pixel lost its colour")
        XCTAssertEqual(decoded.filter { $0 & 255 == 0 }.count, 1)
        XCTAssertEqual(Set(decoded.filter { $0 & 255 != 0 }), [0xFF0000FF])
    }

    /// GIF alpha is one bit: every partial value is forced to fully opaque
    /// or dropped entirely. Asserted as a property rather than against the
    /// cut-off ImageIO currently uses (25%), because that number is its
    /// internal choice and could move in a macOS update, while the one-bit
    /// behaviour is what the format guarantees and what the caller's warning
    /// promises.
    func test_alphaBecomesOneBit() throws {
        var document = makeDocument()
        for (index, alpha) in [0x00, 0x20, 0x40, 0x60, 0x80, 0xC0, 0xFF].enumerated() {
            document.layers[0].frames[0].pixels[index] = 0x00FF0000 | UInt32(alpha)
        }

        let decoded = try decode(try AnimatedGIFEncoder.encode(document))
        for (index, pixel) in decoded.enumerated() {
            let alpha = pixel & 255
            XCTAssertTrue(alpha == 0 || alpha == 255, "pixel \(index) kept a partial alpha of \(alpha)")
        }
        // Not a vacuous pass: something did survive, and it kept its hue.
        XCTAssertEqual(decoded[6], 0x00FF00FF, "the fully opaque pixel should be untouched")
        XCTAssertEqual(decoded[0] & 255, 0, "the fully transparent pixel should stay transparent")
        XCTAssertEqual(Set(decoded.filter { $0 & 255 != 0 }), [0x00FF00FF],
                       "surviving pixels should keep their colour exactly")
    }

    // MARK: - Lossiness

    func test_partialAlphaIsDetected() {
        var document = makeDocument()
        document.layers[0].frames[0].pixels[0] = 0x00FF0080
        XCTAssertTrue(AnimatedGIFEncoder.lossiness(of: document).partialAlpha)
    }

    func test_opaqueAndTransparentPixelsAloneAreNotPartialAlpha() {
        var document = makeDocument(frames: 3)
        document.layers[0].frames[0].pixels[0] = 0x00FF00FF
        document.layers[0].frames[2].pixels[5] = 0x123456FF
        let lossiness = AnimatedGIFEncoder.lossiness(of: document)
        XCTAssertFalse(lossiness.partialAlpha)
        XCTAssertTrue(lossiness.isLossless)
    }

    /// The reason lossiness has to read composited pixels rather than stored
    /// ones: a translucent layer turns fully opaque paint into partial alpha
    /// that only the composite ever shows.
    func test_layerOpacityBelowOneCountsAsPartialAlpha() {
        var document = makeDocument()
        document.layers[0].opacity = 0.5
        document.layers[0].frames[0].pixels[0] = 0x00FF00FF
        XCTAssertTrue(document.layers[0].frames[0].pixels.allSatisfy { $0 == 0 || $0 & 255 == 255 },
                      "the stored pixels must be fully opaque for this test to mean anything")
        XCTAssertTrue(AnimatedGIFEncoder.lossiness(of: document).partialAlpha)
    }

    /// GIF writes one palette for the whole file, so four frames of 100
    /// colours each overflow it even though no single frame does.
    func test_colourReductionCountsAcrossFramesNotWithinOne() {
        let document = makeColourfulDocument(frames: 4, coloursPerFrame: 100)
        let lossiness = AnimatedGIFEncoder.lossiness(of: document)
        XCTAssertTrue(lossiness.colourReduction)
        XCTAssertFalse(lossiness.partialAlpha)
        XCTAssertFalse(lossiness.isLossless)
    }

    func test_aFewColoursAcrossManyFramesIsLossless() {
        let document = makeColourfulDocument(frames: 8, coloursPerFrame: 4)
        XCTAssertTrue(AnimatedGIFEncoder.lossiness(of: document).isLossless)
    }

    func test_exactlyThePaletteCapacityIsNotColourReduction() {
        let document = makeColourfulDocument(frames: 1, coloursPerFrame: AnimatedGIFEncoder.paletteCapacity)
        XCTAssertFalse(AnimatedGIFEncoder.lossiness(of: document).colourReduction)

        let overflowing = makeColourfulDocument(frames: 1, coloursPerFrame: AnimatedGIFEncoder.paletteCapacity + 1)
        XCTAssertTrue(AnimatedGIFEncoder.lossiness(of: overflowing).colourReduction)
    }

    /// Transparency is not a colour: it costs no palette slot, so a document
    /// full of holes must not be reported as needing quantisation.
    func test_transparentPixelsDoNotConsumeThePalette() {
        let document = makeColourfulDocument(frames: 2, coloursPerFrame: AnimatedGIFEncoder.paletteCapacity / 2)
        XCTAssertTrue(document.layers[0].frames[0].pixels.contains(0))
        XCTAssertTrue(AnimatedGIFEncoder.lossiness(of: document).isLossless)
    }
}
