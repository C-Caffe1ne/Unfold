import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Turns PNG or JPEG bytes into straight-alpha RGBA pixels.
///
/// Split out of `PixelDocumentCodec`, whose decoder hard-codes PNG's
/// signature and IEND checks and so cannot accept a JPEG. That decoder is
/// still the right one for the per-chunk PNGs inside a `.unf` document;
/// this one serves the import path, where the file could be either format
/// and could be a sprite sheet far wider than a single canvas.
enum RasterImageDecoder {

    struct Image: Equatable {
        let pixels: [UInt32]
        let width: Int
        let height: Int

        /// The three fields are only meaningful together. Nothing in this
        /// module should be able to hand a consumer a buffer whose length
        /// disagrees with the geometry beside it — a mismatch surfaces later
        /// as an out-of-bounds read during drawing or compositing, far from
        /// wherever it was introduced.
        init(pixels: [UInt32], width: Int, height: Int) {
            precondition(pixels.count == width * height,
                         "\(width)x\(height) needs \(width * height) pixels, got \(pixels.count)")
            self.pixels = pixels
            self.width = width
            self.height = height
        }

        /// A nearest-neighbour subsample, for previewing an import before the
        /// user has decided what to keep.
        ///
        /// Rendering the full image instead would mean expanding every pixel
        /// to four bytes — up to `editorMaxImportPixels × 4`, or 64 MB — on
        /// the main thread, to draw a thumbnail a couple of hundred points
        /// wide. Nearest-neighbour rather than a smooth filter because the
        /// subject is pixel art often enough for hard edges to matter.
        func thumbnail(maxSide: Int) -> Image {
            let longest = max(width, height)
            guard maxSide > 0, longest > maxSide else { return self }
            let scaledWidth = max(1, width * maxSide / longest)
            let scaledHeight = max(1, height * maxSide / longest)
            var scaled = Array(repeating: UInt32(0), count: scaledWidth * scaledHeight)
            for y in 0..<scaledHeight {
                let sourceY = min(height - 1, y * height / scaledHeight)
                for x in 0..<scaledWidth {
                    scaled[y * scaledWidth + x] = pixels[sourceY * width + min(width - 1, x * width / scaledWidth)]
                }
            }
            return Image(pixels: scaled, width: scaledWidth, height: scaledHeight)
        }
    }

    /// The 12 bytes every complete PNG stream ends with: a zero-length chunk,
    /// the ASCII type `IEND`, and its constant CRC-32. Searched for rather
    /// than compared against the tail, because some encoders append metadata
    /// after it.
    private static let pngEndChunk = Data([0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82])

    /// Identifies a raster format from its leading bytes, so a mislabelled
    /// file opens as what it actually is. Returns nil for anything that is
    /// not a raster import format — a `.unf` document, most obviously.
    static func detectFormat(_ data: Data) -> EditorFileFormat? {
        if data.count >= 8, data.prefix(8) == Data([137, 80, 78, 71, 13, 10, 26, 10]) { return .png }
        if data.count >= 2, data.prefix(2) == Data([0xFF, 0xD8]) { return .jpeg }
        return nil
    }

    /// Decodes an image for import. The bound is a pixel budget, not a canvas
    /// size: an import is allowed to be far larger than any canvas, because
    /// deciding what to keep — crop, split, scale — happens afterwards. A
    /// canvas-sized bound here would make cropping a photo impossible.
    static func decode(_ data: Data, format: EditorFileFormat) throws -> Image {
        guard data.count <= Constants.editorMaxSheetDataURLBytes else {
            throw PixelDocumentCodec.Failure.invalid("The image file is too large.")
        }
        try verify(data, format: format)

        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
              CGImageSourceGetType(source) as String? == format.utType.identifier,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0 else {
            throw PixelDocumentCodec.Failure.invalid("That file is not a readable \(format.displayName).")
        }
        // Checked against the header before decoding, so a compressed bomb is
        // refused on its declared dimensions rather than after it expands.
        guard width <= Constants.editorMaxImportSide, height <= Constants.editorMaxImportSide,
              width * height <= Constants.editorMaxImportPixels else {
            throw PixelDocumentCodec.Failure.invalid(
                "That image is \(width)×\(height) px, which is too large to import.")
        }
        guard CGImageSourceGetStatus(source) == .statusComplete,
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
              image.width == width, image.height == height,
              CGImageSourceGetStatusAtIndex(source, 0) == .statusComplete else {
            throw PixelDocumentCodec.Failure.invalid("Use a complete \(format.displayName).")
        }
        return Image(pixels: try pixels(of: image, width: width, height: height), width: width, height: height)
    }

    private static func verify(_ data: Data, format: EditorFileFormat) throws {
        switch format {
        case .png:
            // A stream cut off partway through its compressed data can still
            // decode to a full-sized image, so the IEND check is the reliable
            // backstop for truncation.
            guard data.range(of: pngEndChunk, options: .backwards) != nil else {
                throw PixelDocumentCodec.Failure.invalid("The PNG data looks truncated (no IEND chunk found).")
            }
        case .jpeg:
            guard data.count >= 4, data.prefix(2) == Data([0xFF, 0xD8]),
                  data.suffix(2) == Data([0xFF, 0xD9]) else {
                throw PixelDocumentCodec.Failure.invalid("The JPEG data looks truncated.")
            }
        case .gif, .unfoldSource:
            throw PixelDocumentCodec.Failure.invalid("\(format.displayName) is not a raster import format.")
        }
    }

    /// Preserves straight-alpha samples when ImageIO exposes RGBA directly.
    /// A trip through an 8-bit premultiplied context would otherwise round
    /// low-alpha colour channels even on a native save and reopen.
    private static func pixels(of image: CGImage, width: Int, height: Int) throws -> [UInt32] {
        let order = image.bitmapInfo.intersection(.byteOrderMask)
        if image.bitsPerComponent == 8, image.bitsPerPixel == 32, image.alphaInfo == .last,
           image.colorSpace?.name == CGColorSpace.sRGB,
           order.isEmpty || order == .byteOrder32Big,
           let providerData = image.dataProvider?.data {
            let raw = providerData as Data
            if raw.count >= image.bytesPerRow * height {
                var result = Array(repeating: UInt32(0), count: width * height)
                for y in 0..<height {
                    for x in 0..<width {
                        let offset = y * image.bytesPerRow + x * 4
                        let alpha = UInt32(raw[offset + 3])
                        if alpha > 0 {
                            result[y * width + x] = UInt32(raw[offset]) << 24 | UInt32(raw[offset + 1]) << 16
                                | UInt32(raw[offset + 2]) << 8 | alpha
                        }
                    }
                }
                return result
            }
        }
        var bytes = Array(repeating: UInt8(0), count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else { return false }
            context.interpolationQuality = .none
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        guard drawn else { throw PixelDocumentCodec.Failure.invalid("Could not decode the image pixels.") }
        var result = Array(repeating: UInt32(0), count: width * height)
        for index in result.indices {
            let offset = index * 4
            let alpha = UInt32(bytes[offset + 3])
            guard alpha > 0 else { continue }
            func channel(_ i: Int) -> UInt32 { min(255, (UInt32(bytes[offset + i]) * 255 + alpha / 2) / alpha) }
            result[index] = channel(0) << 24 | channel(1) << 16 | channel(2) << 8 | alpha
        }
        return result
    }
}
