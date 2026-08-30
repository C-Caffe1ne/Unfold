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
    let animations: [AnimationKey: AnimationSource]

    let source: CharacterSource

    func animation(for key: AnimationKey) -> AnimationSource? {
        animations[key]
    }
}

/// Where one animation's frames come from. `CharacterAnimationView` is the
/// only place that branches on this — everything downstream of it
/// (`AnimationClip`, `SpriteAnimator`) stays format-agnostic either way.
enum AnimationSource: Equatable {
    case spriteSheet(SpriteAnimationDefinition)
    /// `fileName` is resolved the same way `spriteSheet.file` is (package-
    /// relative, via `CharacterAssetLoader.resolveFileURL`). `loop` is the
    /// manifest's own value — it always overrides whatever loop metadata
    /// the GIF file itself carries; see `CharacterManifest.AnimationDTO`.
    case gif(fileName: String, loop: Bool)
}

/// Where a character definition came from. Kept out of Timer/UI decision
/// making — only `CharacterRepository` and `CharacterAssetLoader`
/// implementations care about this.
enum CharacterSource: Equatable {
    case builtIn
    case imported(packageURL: URL)
}
