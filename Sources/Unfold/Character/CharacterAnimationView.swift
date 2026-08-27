import SwiftUI

/// Renders one character's animation for a given `AnimationKey`.
///
/// This is the seam between the `Character` domain model and the generic
/// `Sprite/` engine: it resolves the character's sprite sheet + animation
/// definition into a `SpriteAnimator` and hands that to `SpriteAnimationView`.
/// If the sprite sheet can't be loaded, or the character doesn't define this
/// animation, it falls back to a static SF Symbol instead of failing.
///
/// Not `CatAnimationView` — this is the only view any character, built-in or
/// imported, ever needs.
struct CharacterAnimationView: View {
    let character: Character
    let key: AnimationKey

    @State private var animator: SpriteAnimator?

    var body: some View {
        Group {
            if let animator {
                SpriteAnimationView(animator: animator)
            } else {
                Image(systemName: character.thumbnailSymbolName)
                    .font(.system(size: 64))
                    .symbolRenderingMode(.hierarchical)
            }
        }
        .task(id: "\(character.id)#\(key.rawValue)") {
            animator = Self.makeAnimator(character: character, key: key)
        }
    }

    @MainActor
    private static func makeAnimator(character: Character, key: AnimationKey) -> SpriteAnimator? {
        guard
            let definition = character.animation(for: key),
            let sheet = CharacterAssetLoader.loadSpriteSheetImage(for: character)
        else {
            return nil
        }
        return SpriteAnimator(sheet: sheet, animation: definition)
    }
}
