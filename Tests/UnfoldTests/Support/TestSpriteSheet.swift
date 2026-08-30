import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

/// Test-only helpers for building synthetic sprite sheets and standalone
/// images, so `AnimationClipLoader`/`SpriteAnimator` tests never depend on
/// the real Mochi asset (kept untouched, per Phase 2 scope).
enum TestSpriteSheet {

    /// One flat RGBA color per grid cell, in row-major order (cell 0 is
    /// column 0/row 0, matching `SpriteSheetImage.frame(at:)`'s indexing).
    struct Cell {
        let red: UInt8
        let green: UInt8
        let blue: UInt8

        static let red = Cell(red: 255, green: 0, blue: 0)
        static let green = Cell(red: 0, green: 255, blue: 0)
        static let blue = Cell(red: 0, green: 0, blue: 255)
        static let white = Cell(red: 255, green: 255, blue: 255)
    }

    /// Writes a `columns * cellSize` x `rows * cellSize` PNG to a fresh temp
    /// file, one flat color per cell, and loads it back as a
    /// `SpriteSheetImage`. `cells` must have exactly `columns * rows`
    /// entries, in `frame(at:)` order.
    static func makeSheet(columns: Int, rows: Int, cellSize: Int = 4, cells: [Cell]) -> SpriteSheetImage {
        precondition(cells.count == columns * rows, "need exactly columns*rows cell colors")

        let width = columns * cellSize
        let height = rows * cellSize
        var pixels = [UInt8](repeating: 0, count: width * height * 4)

        for row in 0..<rows {
            for column in 0..<columns {
                let cell = cells[row * columns + column]
                for y in 0..<cellSize {
                    for x in 0..<cellSize {
                        let pixelX = column * cellSize + x
                        // PNG/CGImage rows run top-to-bottom; row 0 of the
                        // grid is the top row, matching frame(at:)'s
                        // documented indexing.
                        let pixelY = row * cellSize + y
                        let offset = (pixelY * width + pixelX) * 4
                        pixels[offset] = cell.red
                        pixels[offset + 1] = cell.green
                        pixels[offset + 2] = cell.blue
                        pixels[offset + 3] = 255
                    }
                }
            }
        }

        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let context = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        let cgImage = context.makeImage()!

        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-test-sheet-\(UUID().uuidString)")
            .appendingPathExtension("png")
        let destination = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(destination, cgImage, nil)
        CGImageDestinationFinalize(destination)

        let definition = SpriteSheetDefinition(
            fileName: url.lastPathComponent,
            columns: columns,
            rows: rows,
            frameWidth: cellSize,
            frameHeight: cellSize
        )
        return SpriteSheetImage(definition: definition, fileURL: url)!
    }

    /// A single flat-color `CGImage`, for tests that only care about frame
    /// *identity*/order and don't need a backing sprite sheet at all.
    static func makeImage(_ cell: Cell = .red, size: Int = 2) -> CGImage {
        var pixels = [UInt8](repeating: 0, count: size * size * 4)
        for i in stride(from: 0, to: pixels.count, by: 4) {
            pixels[i] = cell.red
            pixels[i + 1] = cell.green
            pixels[i + 2] = cell.blue
            pixels[i + 3] = 255
        }
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let context = CGContext(
            data: &pixels,
            width: size,
            height: size,
            bitsPerComponent: 8,
            bytesPerRow: size * 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        return context.makeImage()!
    }

    /// Reads back `image`'s color as a `Cell` by resampling it down to a
    /// single pixel, so a test can assert "this decoded frame is the red
    /// one" without caring about `CGImage` instance identity (cropping/PNG
    /// round-tripping don't preserve object identity). Only meaningful for
    /// the flat single-color fixtures this file produces.
    static func color(of image: CGImage) -> Cell {
        var pixel = [UInt8](repeating: 0, count: 4)
        let colorSpace = CGColorSpaceCreateDeviceRGB()
        let context = CGContext(
            data: &pixel,
            width: 1,
            height: 1,
            bitsPerComponent: 8,
            bytesPerRow: 4,
            space: colorSpace,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        context.draw(image, in: CGRect(x: 0, y: 0, width: 1, height: 1))
        return Cell(red: pixel[0], green: pixel[1], blue: pixel[2])
    }
}

extension TestSpriteSheet.Cell: Equatable {}
