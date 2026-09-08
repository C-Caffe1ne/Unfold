import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

/// Reads and writes the editor's own document. The file is named
/// `source.unf` (falling back to the legacy `source.piskel` on read — see
/// `EditorPackageRevision.sourceFile(in:)`), but the bytes are still the
/// Piskel v2 JSON schema underneath; that is what keeps packages written by
/// earlier versions of the app compatible. Invalid sources are rejected
/// before a session or an existing package changes.
enum PixelDocumentCodec {
    enum Failure: Error, LocalizedError {
        case invalid(String)
        var errorDescription: String? {
            switch self { case .invalid(let reason): return reason }
        }
    }
    static let maximumSourceBytes = 48 * 1024 * 1024
    private static let prefix = "data:image/png;base64,"

    private struct Source: Codable {
        var modelVersion: Int
        var sprite: Sprite

        enum CodingKeys: String, CodingKey {
            case modelVersion
            // The on-disk key is still Piskel's. Only the Swift name changed;
            // renaming the key would strand every package already written.
            case sprite = "piskel"
        }
    }
    private struct Sprite: Codable {
        var name: String?
        var description: String?
        var fps: Double?
        var width: Int
        var height: Int
        var layers: [String]
        var hiddenFrames: [Int]?
    }
    private struct Layer: Codable {
        var name: String
        var opacity: Double?
        var frameCount: Int
        var chunks: [Chunk]?
        var base64PNG: String?
    }
    private struct Chunk: Codable {
        /// Piskel uses column-major layout[x][y], unlike our pixel buffer.
        var layout: [[Int]]
        var base64PNG: String
    }

    static func load(from url: URL) throws -> PixelDocument {
        let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        guard size <= maximumSourceBytes else { throw Failure.invalid("The source file is too large.") }
        return try decode(Data(contentsOf: url))
    }

    static func decode(_ data: Data) throws -> PixelDocument {
        guard data.count <= maximumSourceBytes else { throw Failure.invalid("The source file is too large.") }
        let source = try JSONDecoder().decode(Source.self, from: data)
        let sprite = source.sprite
        guard source.modelVersion == 2 else { throw Failure.invalid("Only document version 2 is supported.") }
        guard Constants.editorCanvasSideRange.contains(sprite.width), Constants.editorCanvasSideRange.contains(sprite.height),
              (1...PixelDocument.maximumLayers).contains(sprite.layers.count) else {
            throw Failure.invalid("Use a canvas from 1–128 pixels and 1–16 layers.")
        }
        let fps = sprite.fps ?? 12
        guard fps.isFinite, Constants.editorFPSRange.contains(fps) else { throw Failure.invalid("Animation speed must be 1–24 FPS.") }
        // Hidden timeline frames are not equivalent to transparent frames. Do not silently flatten them.
        guard sprite.hiddenFrames?.isEmpty != false else { throw Failure.invalid("Unhide timeline frames before opening this document.") }
        var document = PixelDocument(width: sprite.width, height: sprite.height)
        document.name = sprite.name ?? "Unfold Character"
        document.description = sprite.description ?? ""
        document.fps = fps
        var layers: [PixelLayer] = []
        var expectedCount: Int?
        for string in sprite.layers {
            let layer = try JSONDecoder().decode(Layer.self, from: Data(string.utf8))
            let opacity = layer.opacity ?? 1
            guard Constants.editorFrameCountRange.contains(layer.frameCount), opacity.isFinite, (0...1).contains(opacity),
                  expectedCount == nil || expectedCount == layer.frameCount else {
                throw Failure.invalid("Layers must have the same 1–24 frames and a valid opacity.")
            }
            expectedCount = layer.frameCount
            let chunks: [Chunk]
            if let stored = layer.chunks { chunks = stored }
            else if let png = layer.base64PNG {
                chunks = [Chunk(layout: (0..<layer.frameCount).map { [$0] }, base64PNG: png)]
            } else { throw Failure.invalid("A layer is missing its pixels.") }
            guard !chunks.isEmpty, chunks.count <= layer.frameCount else { throw Failure.invalid("Invalid layer chunks.") }
            var frames = Array(repeating: PixelFrame(width: sprite.width, height: sprite.height), count: layer.frameCount)
            var seen = Set<Int>()
            for chunk in chunks {
                let columns = chunk.layout.count
                let rows = chunk.layout.first?.count ?? 0
                guard columns > 0, rows > 0, columns <= layer.frameCount, rows <= layer.frameCount,
                      columns * rows <= layer.frameCount,
                      chunk.layout.allSatisfy({ $0.count == rows }), chunk.base64PNG.hasPrefix(prefix),
                      chunk.base64PNG.utf8.count <= Constants.editorMaxSheetDataURLBytes,
                      let png = Data(base64Encoded: String(chunk.base64PNG.dropFirst(prefix.count))) else {
                    throw Failure.invalid("Invalid chunk layout or PNG.")
                }
                let (pixels, width, _) = try decodePNG(png, expectedWidth: sprite.width * columns, expectedHeight: sprite.height * rows)
                for x in 0..<columns {
                    for y in 0..<rows {
                        let index = chunk.layout[x][y]
                        guard (0..<layer.frameCount).contains(index), seen.insert(index).inserted else {
                            throw Failure.invalid("The document contains duplicate or invalid frame indices.")
                        }
                        for py in 0..<sprite.height {
                            for px in 0..<sprite.width {
                                frames[index].pixels[py * sprite.width + px] = pixels[(y * sprite.height + py) * width + x * sprite.width + px]
                            }
                        }
                    }
                }
            }
            guard seen.count == layer.frameCount else { throw Failure.invalid("The document is missing animation frames.") }
            layers.append(PixelLayer(name: layer.name, opacity: opacity, frames: frames))
        }
        document.layers = layers
        return document
    }

    static func encode(_ document: PixelDocument) throws -> Data {
        let encoder = JSONEncoder()
        let layers = try document.layers.map { layer -> String in
            let sheet = sheetPixels(layer.frames, width: document.width, height: document.height)
            let png = try encodePNG(sheet, width: document.width * document.frameCount, height: document.height)
            let chunk = Chunk(layout: (0..<document.frameCount).map { [$0] }, base64PNG: prefix + png.base64EncodedString())
            let data = try encoder.encode(Layer(name: layer.name, opacity: layer.opacity, frameCount: document.frameCount, chunks: [chunk]))
            return String(decoding: data, as: UTF8.self)
        }
        let source = Source(modelVersion: 2, sprite: Sprite(name: document.name, description: document.description,
            fps: document.fps, width: document.width, height: document.height, layers: layers))
        let result = try encoder.encode(source)
        guard result.count <= maximumSourceBytes else { throw Failure.invalid("The source file is too large.") }
        return result
    }

    static func sheetPNG(_ document: PixelDocument) throws -> Data {
        let frames = (0..<document.frameCount).map { document.compositedFrame(at: $0) }
        return try encodePNG(sheetPixels(frames, width: document.width, height: document.height),
                             width: document.width * document.frameCount, height: document.height)
    }

    static func savePayload(_ document: PixelDocument, characterID: String?) throws -> EditorSavePayload {
        let png = try sheetPNG(document)
        let source = try encode(document)
        var wire: [String: Any] = ["type": "save", "width": document.width, "height": document.height,
            "fps": document.fps, "frameCount": document.frameCount, "sheetPNG": prefix + png.base64EncodedString(),
            "sourceJSON": String(decoding: source, as: UTF8.self)]
        wire["characterID"] = characterID
        // Use the same geometry/size validation as existing package saves.
        let wireData = try JSONSerialization.data(withJSONObject: wire)
        return try EditorSavePayload.decode(from: String(decoding: wireData, as: UTF8.self))
    }

    static func importPNG(_ data: Data) throws -> PixelDocument {
        let (pixels, width, height) = try decodePNG(data)
        var document = PixelDocument(width: width, height: height)
        document.layers[0].frames[0].pixels = pixels
        return document
    }

    private static func sheetPixels(_ frames: [PixelFrame], width: Int, height: Int) -> [UInt32] {
        var pixels = Array(repeating: UInt32(0), count: width * height * frames.count)
        for (index, frame) in frames.enumerated() {
            for y in 0..<height {
                let source = y * width
                let target = y * width * frames.count + index * width
                pixels.replaceSubrange(target..<(target + width), with: frame.pixels[source..<(source + width)])
            }
        }
        return pixels
    }

    static func image(_ pixels: [UInt32], width: Int, height: Int) -> CGImage? {
        guard pixels.count == width * height else { return nil }
        var bytes = [UInt8]()
        bytes.reserveCapacity(pixels.count * 4)
        for pixel in pixels {
            bytes.append(UInt8((pixel >> 24) & 255)); bytes.append(UInt8((pixel >> 16) & 255))
            bytes.append(UInt8((pixel >> 8) & 255)); bytes.append(UInt8(pixel & 255))
        }
        guard let provider = CGDataProvider(data: Data(bytes) as CFData) else { return nil }
        return CGImage(width: width, height: height, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: width * 4,
            space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.last.rawValue),
            provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)
    }

    static func encodePNG(_ pixels: [UInt32], width: Int, height: Int) throws -> Data {
        guard let image = image(pixels, width: width, height: height) else { throw Failure.invalid("Could not render the pixels.") }
        let data = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(data, UTType.png.identifier as CFString, 1, nil) else {
            throw Failure.invalid("Could not create a PNG.")
        }
        CGImageDestinationAddImage(destination, image, nil)
        guard CGImageDestinationFinalize(destination) else { throw Failure.invalid("Could not encode the PNG.") }
        return data as Data
    }

    private static func decodePNG(_ data: Data, expectedWidth: Int? = nil, expectedHeight: Int? = nil) throws -> ([UInt32], Int, Int) {
        guard data.count <= Constants.editorMaxSheetDataURLBytes,
              data.range(of: Data([0, 0, 0, 0, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82]), options: .backwards) != nil,
              let source = CGImageSourceCreateWithData(data as CFData, nil),
              CGImageSourceGetType(source) as String? == UTType.png.identifier,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0,
              width == (expectedWidth ?? width), height == (expectedHeight ?? height),
              width <= (expectedWidth ?? 128), height <= (expectedHeight ?? 128),
              CGImageSourceGetStatus(source) == .statusComplete,
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
              image.width == width, image.height == height,
              CGImageSourceGetStatusAtIndex(source, 0) == .statusComplete else {
            throw Failure.invalid("Use a complete PNG with the expected dimensions (imports: 1–128 pixels per side).")
        }
        // Preserve straight-alpha samples when ImageIO exposes RGBA directly.
        // A trip through an 8-bit premultiplied CGContext would otherwise
        // round low-alpha color channels even on a native save/reopen.
        let order = image.bitmapInfo.intersection(.byteOrderMask)
        if image.bitsPerComponent == 8, image.bitsPerPixel == 32, image.alphaInfo == .last,
           image.colorSpace?.name == CGColorSpace.sRGB,
           order.isEmpty || order == .byteOrder32Big,
           let providerData = image.dataProvider?.data {
            let raw = providerData as Data
            if raw.count >= image.bytesPerRow * height {
                var pixels = Array(repeating: UInt32(0), count: width * height)
                for y in 0..<height {
                    for x in 0..<width {
                        let offset = y * image.bytesPerRow + x * 4
                        let alpha = UInt32(raw[offset + 3])
                        if alpha > 0 {
                            pixels[y * width + x] = UInt32(raw[offset]) << 24 | UInt32(raw[offset + 1]) << 16
                                | UInt32(raw[offset + 2]) << 8 | alpha
                        }
                    }
                }
                return (pixels, width, height)
            }
        }
        var bytes = Array(repeating: UInt8(0), count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height, bitsPerComponent: 8,
                bytesPerRow: width * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else { return false }
            context.interpolationQuality = .none
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        guard drawn else { throw Failure.invalid("Could not decode the PNG pixels.") }
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for index in pixels.indices {
            let offset = index * 4
            let alpha = UInt32(bytes[offset + 3])
            guard alpha > 0 else { continue }
            func channel(_ i: Int) -> UInt32 { min(255, (UInt32(bytes[offset + i]) * 255 + alpha / 2) / alpha) }
            pixels[index] = channel(0) << 24 | channel(1) << 16 | channel(2) << 8 | alpha
        }
        return (pixels, width, height)
    }
}
