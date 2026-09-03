import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Builds real PNG bytes of an exact pixel size, so payload tests can check
/// the "declared geometry vs. what actually decoded" rule against genuine
/// image data rather than a stubbed decoder.
enum TestPNG {

    static func data(width: Int, height: Int) -> Data {
        var pixels = [UInt8](repeating: 0, count: width * height * 4)
        for i in stride(from: 0, to: pixels.count, by: 4) {
            pixels[i] = 200
            pixels[i + 1] = 120
            pixels[i + 2] = 40
            pixels[i + 3] = 255
        }
        let context = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        let image = context.makeImage()!

        let output = NSMutableData()
        let destination = CGImageDestinationCreateWithData(
            output, UTType.png.identifier as CFString, 1, nil
        )!
        CGImageDestinationAddImage(destination, image, nil)
        CGImageDestinationFinalize(destination)
        return output as Data
    }

    static func dataURL(width: Int, height: Int) -> String {
        "data:image/png;base64," + data(width: width, height: height).base64EncodedString()
    }
}
