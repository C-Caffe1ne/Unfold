import Foundation
import ImageIO

/// One "save" message from the editor bridge, decoded and validated.
///
/// The editor is a vendored web app running in a `WKWebView`: correct by
/// construction is not something this app can assume about it. So nothing
/// here is trusted — bounds are re-checked, and the geometry the message
/// *claims* is verified against the PNG that actually decoded. A payload
/// that exists is a payload that's safe to write.
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
            case .geometryMismatch(let declared, let actual):
                return "the editor declared a \(declared) sheet but sent a \(actual) image"
            case .emptySource:
                return "the editor sent an empty .piskel document"
            }
        }
    }

    let width: Int
    let height: Int
    let fps: Double
    let frameCount: Int
    let sheetPNGData: Data
    let piskelJSON: String

    /// Set when the user is re-saving a character they opened for editing;
    /// `nil` for a brand-new one. Decides overwrite vs. create.
    let characterID: String?

    private static let pngDataURLPrefix = "data:image/png;base64,"

    private struct Wire: Decodable {
        let type: String
        let width: Int
        let height: Int
        let fps: Double
        let frameCount: Int
        let sheetPNG: String
        let piskelJSON: String
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
        guard !wire.piskelJSON.isEmpty else {
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
            piskelJSON: wire.piskelJSON,
            characterID: wire.characterID
        )
    }

    /// Fully decodes the PNG and reads pixel size off the resulting
    /// `CGImage` — not just the header — so a structurally-valid IHDR with
    /// truncated or corrupt pixel data is caught here, at save time, rather
    /// than surfacing later as a silent failure in `SpriteSheetImage.init?`.
    /// The largest sheet this app accepts is 3072×128px, so a full decode
    /// stays cheap. Goes through `CGImageSource` rather than `NSImage`,
    /// whose reported size is display-scale dependent (the same reason
    /// `SpriteSheetImage` goes through `CGImageSource`).
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
