import CoreGraphics
import Foundation

/// How an incoming image becomes a document.
///
/// Pure logic with no UI, so the awkward cases — a sheet whose frame size
/// does not divide it, a crop outside the image, more frames than the
/// document allows — are testable without a window.
enum ImportOptions: Equatable {
    /// One frame, at the image's own size.
    case single
    /// A sprite sheet cut into `frameWidth × frameHeight` cells, read
    /// left-to-right then top-to-bottom.
    case split(frameWidth: Int, frameHeight: Int)
    /// A region of the image, in image coordinates with the origin top-left.
    case crop(CGRect)
    /// Nearest-neighbour downscale until both sides fit the canvas cap.
    case scaleToFit

    private static var maximumSide: Int { Constants.editorCanvasSideRange.upperBound }

    /// What the import dialog offers first.
    static func suggestion(width: Int, height: Int) -> ImportOptions {
        // A ratio of 1 is an integer multiple as well, so require at least 2
        // cells — otherwise every square image would look like a sheet.
        if height <= maximumSide, width > height, width % height == 0 {
            let frames = width / height
            if frames >= 2, Constants.editorFrameCountRange.contains(frames) {
                return .split(frameWidth: height, frameHeight: height)
            }
        }
        if width > maximumSide || height > maximumSide {
            let side = min(maximumSide, min(width, height))
            return .crop(CGRect(x: (width - side) / 2, y: (height - side) / 2, width: side, height: side))
        }
        return .single
    }

    static func apply(_ options: ImportOptions, to image: RasterImageDecoder.Image) throws -> PixelDocument {
        switch options {
        case .single:
            try check(width: image.width, height: image.height, frames: 1)
            return document(width: image.width, height: image.height, frames: [image.pixels])

        case .split(let frameWidth, let frameHeight):
            guard frameWidth > 0, frameHeight > 0,
                  image.width % frameWidth == 0, image.height % frameHeight == 0 else {
                throw PixelDocumentCodec.Failure.invalid(
                    "A \(image.width)×\(image.height) image does not divide into \(frameWidth)×\(frameHeight) frames.")
            }
            let columns = image.width / frameWidth
            let rows = image.height / frameHeight
            try check(width: frameWidth, height: frameHeight, frames: columns * rows)
            var frames: [[UInt32]] = []
            for row in 0..<rows {
                for column in 0..<columns {
                    frames.append(region(of: image, x: column * frameWidth, y: row * frameHeight,
                                         width: frameWidth, height: frameHeight))
                }
            }
            return document(width: frameWidth, height: frameHeight, frames: frames)

        case .crop(let rect):
            let x = Int(rect.origin.x), y = Int(rect.origin.y)
            let width = Int(rect.width), height = Int(rect.height)
            // Checked against the source before the destination: a rect that
            // does not overlap the image has no pixels to extract, which is
            // the more fundamental problem than whether the result would fit
            // the canvas — and mirrors .split checking divisibility (against
            // the source) before frame count (against the destination).
            guard x >= 0, y >= 0,
                  x + width <= image.width, y + height <= image.height else {
                throw PixelDocumentCodec.Failure.invalid("That crop region lies outside the image.")
            }
            try check(width: width, height: height, frames: 1)
            return document(width: width, height: height,
                            frames: [region(of: image, x: x, y: y, width: width, height: height)])

        case .scaleToFit:
            let longest = max(image.width, image.height)
            guard longest > 0 else { throw PixelDocumentCodec.Failure.invalid("The image is empty.") }
            let factor = min(1.0, Double(maximumSide) / Double(longest))
            let width = max(Constants.editorCanvasSideRange.lowerBound, Int((Double(image.width) * factor).rounded(.down)))
            let height = max(Constants.editorCanvasSideRange.lowerBound, Int((Double(image.height) * factor).rounded(.down)))
            try check(width: width, height: height, frames: 1)
            var pixels = Array(repeating: UInt32(0), count: width * height)
            for y in 0..<height {
                let sourceY = min(image.height - 1, y * image.height / height)
                for x in 0..<width {
                    let sourceX = min(image.width - 1, x * image.width / width)
                    pixels[y * width + x] = image.pixels[sourceY * image.width + sourceX]
                }
            }
            return document(width: width, height: height, frames: [pixels])
        }
    }

    /// One gate for every geometry an import can produce: the canvas bounds,
    /// the frame count, and the total the document may hold.
    private static func check(width: Int, height: Int, frames: Int) throws {
        guard Constants.editorCanvasSideRange.contains(width),
              Constants.editorCanvasSideRange.contains(height) else {
            throw PixelDocumentCodec.Failure.invalid(
                "Frames must be \(Constants.editorCanvasSideRange.lowerBound)–\(Constants.editorCanvasSideRange.upperBound) pixels per side.")
        }
        guard Constants.editorFrameCountRange.contains(frames) else {
            throw PixelDocumentCodec.Failure.invalid(
                "That makes \(frames) frames; the limit is \(Constants.editorFrameCountRange.upperBound).")
        }
        guard width * height * frames * 4 <= Constants.editorMaxDocumentBytes else {
            throw PixelDocumentCodec.Failure.invalid("That import would be too large to edit.")
        }
    }

    private static func region(of image: RasterImageDecoder.Image, x: Int, y: Int, width: Int, height: Int) -> [UInt32] {
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for row in 0..<height {
            let source = (y + row) * image.width + x
            pixels.replaceSubrange(row * width..<(row * width + width),
                                   with: image.pixels[source..<(source + width)])
        }
        return pixels
    }

    private static func document(width: Int, height: Int, frames: [[UInt32]]) -> PixelDocument {
        var result = PixelDocument(width: width, height: height)
        result.layers[0].frames = frames.map { pixels in
            var frame = PixelFrame(width: width, height: height)
            frame.pixels = pixels
            return frame
        }
        return result
    }
}
