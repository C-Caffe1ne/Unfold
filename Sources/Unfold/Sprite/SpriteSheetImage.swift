import CoreGraphics
import Foundation
import ImageIO

/// A decoded sprite sheet plus the geometry needed to crop one cell out of
/// it. Frame rects are always computed from `columns`/`rows` against the
/// image's actual pixel size — never a hardcoded cell size — so sheets of
/// any grid size are supported.
///
/// Loaded once per sprite sheet with `ImageIO` directly (`CGImageSource`)
/// rather than through `NSImage`, which sidesteps `NSImage`'s
/// display-scale-dependent rasterization and always yields the file's true
/// pixel dimensions. Loading decodes the file exactly once; every
/// subsequent `frame(at:)` call crops the same in-memory `CGImage`, so
/// playing an animation never re-reads the sprite sheet from disk.
final class SpriteSheetImage {
    let definition: SpriteSheetDefinition
    private let cgImage: CGImage
    private let cellSize: CGSize

    /// Total number of grid cells (`columns * rows`) — the valid range for
    /// `frame(at:)`.
    var frameCount: Int { definition.columns * definition.rows }

    init?(definition: SpriteSheetDefinition, fileURL: URL) {
        guard definition.columns > 0, definition.rows > 0,
              let source = CGImageSourceCreateWithURL(fileURL as CFURL, nil),
              let cgImage = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else {
            return nil
        }

        self.definition = definition
        self.cgImage = cgImage
        self.cellSize = CGSize(
            width: CGFloat(cgImage.width) / CGFloat(definition.columns),
            height: CGFloat(cgImage.height) / CGFloat(definition.rows)
        )

        Self.logGridMismatchIfNeeded(definition: definition, cgImage: cgImage)
    }

    /// Crops the cell for `frameIndex` (0-indexed, row-major:
    /// `column = frameIndex % columns`, `row = frameIndex / columns`, row 0
    /// is the top row of the file). An index is free to fall in any row —
    /// a clip that spans multiple rows works the same as one that doesn't.
    /// Returns `nil` if the index is outside the sheet's grid.
    func frame(at frameIndex: Int) -> CGImage? {
        guard frameIndex >= 0, frameIndex < frameCount else { return nil }

        let column = frameIndex % definition.columns
        let row = frameIndex / definition.columns
        let rect = CGRect(
            x: CGFloat(column) * cellSize.width,
            y: CGFloat(row) * cellSize.height,
            width: cellSize.width,
            height: cellSize.height
        )
        return cgImage.cropping(to: rect)
    }

    /// Sanity-checks the manifest's declared `frameWidth`/`frameHeight`
    /// against the sheet's real pixel size. A mismatch doesn't fail
    /// loading — cropping always uses the image's actual size divided by
    /// the grid, so playback stays correct either way — but it's a strong
    /// signal the manifest and the art drifted apart, so it's worth a
    /// clear debug log.
    private static func logGridMismatchIfNeeded(definition: SpriteSheetDefinition, cgImage: CGImage) {
        guard definition.frameWidth > 0, definition.frameHeight > 0 else { return }
        let expectedWidth = definition.frameWidth * definition.columns
        let expectedHeight = definition.frameHeight * definition.rows
        guard expectedWidth != cgImage.width || expectedHeight != cgImage.height else { return }

        NSLog(
            "Unfold: sprite sheet \"\(definition.fileName)\" is \(cgImage.width)x\(cgImage.height)px, " +
            "but its manifest declares a \(definition.columns)x\(definition.rows) grid of " +
            "\(definition.frameWidth)x\(definition.frameHeight)px frames " +
            "(expected \(expectedWidth)x\(expectedHeight)px). Using the sheet's actual size."
        )
    }
}
