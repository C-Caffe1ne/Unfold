import Foundation

/// Mirrors `character.json` inside a character package (bundled today,
/// user-imported later):
///
/// ```json
/// {
///   "id": "default-cat",
///   "name": "Mochi",
///   "version": 1,
///   "spriteSheet": {
///     "file": "spritesheet.png",
///     "columns": 8,
///     "rows": 3,
///     "frameWidth": 384,
///     "frameHeight": 384
///   },
///   "animations": {
///     "idle": { "frames": [0,1,2,3,4,5,6,7], "fps": 7, "loop": true },
///     "stretch": { "frames": [8,9,10,11,12,13,14,15,16,17,18,19], "fps": 11, "loop": false }
///   }
/// }
/// ```
///
/// An animation's `frames` list is not tied to one row — it can walk across
/// row boundaries (as `stretch` does above: 8 frames finish row 1, the
/// remaining 4 start row 2). `CharacterPackageLoader` is the only thing that
/// reads this type; it turns a validated manifest into the `Character`
/// domain model the rest of the app uses.
struct CharacterManifest: Codable, Equatable {
    let id: String
    let name: String
    let version: Int
    let spriteSheet: SpriteSheetDTO
    let animations: [String: AnimationDTO]

    /// Optional SF Symbol shown before any sprite frame loads. Not part of
    /// the package's required contract — falls back to a generic symbol
    /// when absent.
    let thumbnailSymbol: String?

    struct SpriteSheetDTO: Codable, Equatable {
        let file: String
        let columns: Int
        let rows: Int

        /// Intended per-frame canvas size in pixels (V1 default: 384×384).
        /// Used to validate the sheet's actual size, not to compute crop
        /// rects — those always come from the image's real pixel size.
        let frameWidth: Int
        let frameHeight: Int
    }

    struct AnimationDTO: Codable, Equatable {
        /// Frame indices, in playback order. May span multiple rows.
        let frames: [Int]
        let fps: Double
        let loop: Bool
    }
}
