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
                return "fps must be a finite positive number, got \(fps)"
            case .notAPNGDataURL:
                return "the sprite sheet is not a PNG data URL"
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
        guard wire.fps.isFinite, wire.fps > 0 else {
            throw DecodingError.invalidFPS(wire.fps)
        }
        guard !wire.piskelJSON.isEmpty else {
            throw DecodingError.emptySource
        }
        guard wire.sheetPNG.hasPrefix(pngDataURLPrefix) else {
            throw DecodingError.notAPNGDataURL
        }

        let base64 = String(wire.sheetPNG.dropFirst(pngDataURLPrefix.count))
        guard let sheetData = Data(base64Encoded: base64) else {
            throw DecodingError.notBase64
        }

        let expectedWidth = wire.width * wire.frameCount
        let (actualWidth, actualHeight) = try pixelSize(of: sheetData)
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

    /// Reads the PNG header only — no full decode, and no `NSImage`, whose
    /// size is display-scale dependent (the same reason `SpriteSheetImage`
    /// goes through `CGImageSource`).
    private static func pixelSize(of data: Data) throws -> (Int, Int) {
        guard
            let source = CGImageSourceCreateWithData(data as CFData, nil),
            let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
            let width = properties[kCGImagePropertyPixelWidth] as? Int,
            let height = properties[kCGImagePropertyPixelHeight] as? Int
        else {
            throw DecodingError.undecodablePNG
        }
        return (width, height)
    }
}
