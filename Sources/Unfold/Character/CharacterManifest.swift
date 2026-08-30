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
///     "stretch": { "gif": "stretch.gif", "loop": false }
///   }
/// }
/// ```
///
/// An animation's `frames` list is not tied to one row — it can walk across
/// row boundaries (e.g. an 8-frame clip on a narrower sheet naturally spans
/// two rows). An animation is sourced from the shared sprite sheet
/// (`frames`+`fps`) *or* from its own GIF file (`gif`) — never both;
/// `CharacterPackageLoader` picks based on which fields are present.
/// `CharacterPackageLoader` is the only thing that reads this type; it
/// turns a validated manifest into the `Character` domain model the rest of
/// the app uses.
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
        /// Sprite-sheet-sourced animations only — `nil` when `gif` is set.
        let frames: [Int]?
        let fps: Double?

        /// File name of a GIF (relative to this character's own package,
        /// same rules as `spriteSheet.file`) to source this animation from
        /// instead of the sprite sheet. When present, `frames`/`fps` are
        /// ignored by `CharacterPackageLoader` — the GIF's own frames and
        /// per-frame durations are used instead.
        let gif: String?

        /// Authoritative regardless of source: for a GIF-sourced animation
        /// this *overrides* whatever loop metadata is baked into the GIF
        /// file itself, rather than trusting the file. See
        /// `CharacterAnimationView.makeAnimator`.
        let loop: Bool
    }
}
