import Foundation

/// A stretch-reminder character (Cat, Dog, Penguin, ...), built-in or
/// imported by the user. Timer/Overlay code only ever sees this type — it
/// never needs to know a character's concrete origin, package layout, or
/// animation format.
struct Character: Identifiable, Equatable {
    let id: String
    let name: String

    /// SF Symbol shown before any sprite frame has loaded, and as a
    /// fallback if the sprite sheet can't be read.
    let thumbnailSymbolName: String

    /// The single sprite sheet backing every animation this character has.
    let spriteSheet: SpriteSheetDefinition

    /// Animations this character supports, keyed by `AnimationKey`. A
    /// character does not have to implement every built-in key.
    let animations: [AnimationKey: SpriteAnimationDefinition]

    let source: CharacterSource

    func animation(for key: AnimationKey) -> SpriteAnimationDefinition? {
        animations[key]
    }
}

/// Where a character definition came from. Kept out of Timer/UI decision
/// making — only `CharacterRepository` and `CharacterAssetLoader`
/// implementations care about this.
enum CharacterSource: Equatable {
    case builtIn
    case imported(packageURL: URL)
}
