import Foundation

/// How a sprite sheet image is laid out: an even grid of `columns × rows`
/// cells, all the same size. No two characters need the same grid — a
/// `SpriteSheetImage` derives each cell's size from these numbers and the
/// image's own pixel dimensions, so an 8×2 sheet and a 10×6 sheet work the
/// same way.
struct SpriteSheetDefinition: Equatable {
    /// File name of the sheet image, relative to the character package.
    /// Never an absolute path — see `CharacterAssetLoader`.
    let fileName: String
    let columns: Int
    let rows: Int

    /// The intended per-frame canvas size in pixels (V1 default: 384×384).
    /// Used only to sanity-check the sheet's actual pixel size against the
    /// manifest at load time — cropping itself always derives cell size
    /// from the image's real dimensions, never from these numbers.
    let frameWidth: Int
    let frameHeight: Int
}
