import Foundation
import ImageIO

/// Validated save data on the way into a character package.
///
/// This was once the boundary between a Piskel web view and the Swift
/// host, which is why it validates a JSON message rather than a struct.
/// The web view is gone and `PixelDocumentCodec.savePayload` is now the
/// only producer, but the checks are worth keeping: they are what stands
/// between a malformed document and an overwritten character package.
///
/// Bounds are re-checked, and the declared geometry is verified against
/// the PNG that actually decoded. What that
/// buys is a guarantee about shape, not content: a payload that exists
/// decoded to an image whose dimensions match what the message declared,
/// and isn't missing its closing chunk. It is not a guarantee that the
/// pixels are exactly what the user drew — a full content check isn't
/// cheap to make watertight (see `decodedPixelSize`'s note). What makes
/// that an acceptable boundary is that validation and playback decode the
/// same bytes through the same `ImageIO` path (this type and
/// `SpriteSheetImage`), so nothing accepted here can render differently
/// than it validated.
struct EditorSavePayload: Equatable {

    enum DecodingError: Error, CustomStringConvertible {
        case malformedJSON
        case unexpectedMessageType(String)
        case frameCountOutOfRange(Int)
        case canvasSizeOutOfRange(width: Int, height: Int)
        case invalidFPS(Double)
        case notAPNGDataURL
        case sheetTooLarge(bytes: Int)
        case notBase64
        case undecodablePNG
        case truncatedPNG
        case geometryMismatch(declared: String, actual: String)
        case emptySource

        var description: String {
            switch self {
            case .malformedJSON:
                return "the editor sent a message that isn't valid JSON"
            case .unexpectedMessageType(let type):
                return "unexpected message type \"\(type)\""
            case .frameCountOutOfRange(let count):
                return "\(count) frames is outside the allowed \(Constants.editorFrameCountRange)"
            case .canvasSizeOutOfRange(let width, let height):
                return "canvas \(width)x\(height)px is outside the allowed \(Constants.editorCanvasSideRange)px per side"
            case .invalidFPS(let fps):
                return "fps \(fps) is outside the allowed \(Constants.editorFPSRange)"
            case .notAPNGDataURL:
                return "the sprite sheet is not a PNG data URL"
            case .sheetTooLarge(let bytes):
                return "sprite sheet data is \(bytes) bytes, over the \(Constants.editorMaxSheetDataURLBytes)-byte limit"
            case .notBase64:
                return "the sprite sheet's data URL is not valid base64"
            case .undecodablePNG:
                return "the sprite sheet could not be decoded as a PNG"
            case .truncatedPNG:
                return "the sprite sheet's PNG data looks truncated (no IEND chunk found)"
            case .geometryMismatch(let declared, let actual):
                return "the editor declared a \(declared) sheet but sent a \(actual) image"
            case .emptySource:
                return "the editor produced an empty source document"
            }
        }
    }

    let width: Int
    let height: Int
    let fps: Double
    let frameCount: Int
    let sheetPNGData: Data
    let sourceJSON: String

    /// Set when the user is re-saving a character they opened for editing;
    /// `nil` for a brand-new one. Decides overwrite vs. create.
    let characterID: String?

    private static let pngDataURLPrefix = "data:image/png;base64,"

    /// The 12 bytes every complete PNG stream ends with: a zero-length
    /// chunk (4 bytes), the ASCII chunk type `IEND` (4 bytes), and its
    /// CRC-32 (4 bytes) — constant because a zero-length chunk always
    /// hashes to the same checksum. A stream cut short during transfer or
    /// encoding simply doesn't contain this sequence.
    ///
    /// Searched for, not compared against the tail with equality: some
    /// encoders append bytes after `IEND` (trailing metadata, padding),
    /// and a legitimate sheet from an unusual encoder shouldn't be
    /// rejected just because `IEND` isn't the very last thing in the file.
    private static let pngEndChunk = Data([0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82])

    private struct Wire: Decodable {
        let type: String
        let width: Int
        let height: Int
        let fps: Double
        let frameCount: Int
        let sheetPNG: String
        let sourceJSON: String
        let characterID: String?
    }

    static func decode(from json: String) throws -> EditorSavePayload {
        guard
            let data = json.data(using: .utf8),
            let wire = try? JSONDecoder().decode(Wire.self, from: data)
        else {
            throw DecodingError.malformedJSON
        }

        guard wire.type == "save" else {
            throw DecodingError.unexpectedMessageType(wire.type)
        }
        guard Constants.editorFrameCountRange.contains(wire.frameCount) else {
            throw DecodingError.frameCountOutOfRange(wire.frameCount)
        }
        guard
            Constants.editorCanvasSideRange.contains(wire.width),
            Constants.editorCanvasSideRange.contains(wire.height)
        else {
            throw DecodingError.canvasSizeOutOfRange(width: wire.width, height: wire.height)
        }
        guard wire.fps.isFinite, Constants.editorFPSRange.contains(wire.fps) else {
            throw DecodingError.invalidFPS(wire.fps)
        }
        guard !wire.sourceJSON.isEmpty else {
            throw DecodingError.emptySource
        }
        guard wire.sheetPNG.hasPrefix(pngDataURLPrefix) else {
            throw DecodingError.notAPNGDataURL
        }
        guard wire.sheetPNG.utf8.count <= Constants.editorMaxSheetDataURLBytes else {
            throw DecodingError.sheetTooLarge(bytes: wire.sheetPNG.utf8.count)
        }

        let base64 = String(wire.sheetPNG.dropFirst(pngDataURLPrefix.count))
        guard let sheetData = Data(base64Encoded: base64) else {
            throw DecodingError.notBase64
        }

        let expectedWidth = wire.width * wire.frameCount
        let (actualWidth, actualHeight) = try decodedPixelSize(of: sheetData)

        // A full decode already rejects most truncation (see
        // `decodedPixelSize`), but not all of it — a stream cut off partway
        // through its compressed pixel data can still decode to a
        // full-sized image if enough of the deflate stream survived. The
        // `IEND` check is the reliable backstop for exactly that case.
        guard sheetData.range(of: pngEndChunk, options: .backwards) != nil else {
            throw DecodingError.truncatedPNG
        }

        guard actualWidth == expectedWidth, actualHeight == wire.height else {
            throw DecodingError.geometryMismatch(
                declared: "\(expectedWidth)x\(wire.height)px",
                actual: "\(actualWidth)x\(actualHeight)px"
            )
        }

        return EditorSavePayload(
            width: wire.width,
            height: wire.height,
            fps: wire.fps,
            frameCount: wire.frameCount,
            sheetPNGData: sheetData,
            sourceJSON: wire.sourceJSON,
            characterID: wire.characterID
        )
    }

    /// Fully decodes the PNG and reads pixel size off the resulting
    /// `CGImage` — not just the header — because a header-only read can't
    /// tell a complete file from one that stops partway through the pixel
    /// data. This catches most truncation, but a probe during development
    /// found it isn't exhaustive: a fixture PNG cut to half its byte length
    /// still decoded to a full-sized `CGImage` (ImageIO's decoder tolerates
    /// more missing compressed data than that would suggest). The `IEND`
    /// check in `decode(from:)` is what closes that specific gap; this
    /// function's guarantee is narrower — the bytes decode to *some* image,
    /// and its pixel dimensions are what `decode(from:)` checks against the
    /// declared geometry. Goes through `CGImageSource` rather than
    /// `NSImage`, whose reported size is display-scale dependent (the same
    /// reason `SpriteSheetImage` goes through `CGImageSource`).
    private static func decodedPixelSize(of data: Data) throws -> (Int, Int) {
        guard
            let source = CGImageSourceCreateWithData(data as CFData, nil),
            let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else {
            throw DecodingError.undecodablePNG
        }
        return (image.width, image.height)
    }
}
