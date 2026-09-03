import SwiftUI

/// Renders one character's animation for a given `AnimationKey`.
///
/// This is the seam between the `Character` domain model and the generic
/// `Sprite/` engine: it resolves the character's `AnimationSource` — sprite
/// sheet (via `AnimationClipLoader`) or GIF (via `AnimatedImageClipLoader`)
/// — into an `AnimationClip`, then a `SpriteAnimator`, and hands that to
/// `SpriteAnimationView`. If the source asset can't be loaded, or the
/// character doesn't define this animation, it falls back to a static SF
/// Symbol instead of failing. `AnimationClip` and both loaders are this
/// seam's own implementation detail — no other view needs to know they
/// exist, and `SpriteAnimator`/`SpriteAnimationView` never know which
/// source produced the clip they're playing.
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
                SpriteAnimationView(animator: animator, interpolation: character.renderStyle.interpolation)
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

    /// `static` rather than `private static` specifically so tests can call
    /// it directly (build a `Character`, call this, assert on the resulting
    /// `SpriteAnimator`) without going through SwiftUI view lifecycle —
    /// same reasoning `SpriteAnimator.advance()` was made internal for.
    @MainActor
    static func makeAnimator(character: Character, key: AnimationKey) -> SpriteAnimator? {
        guard let source = character.resolvedAnimation(for: key) else { return nil }

        switch source {
        case .spriteSheet(let definition):
            guard
                let sheet = CharacterAssetLoader.loadSpriteSheetImage(for: character),
                let clip = try? AnimationClipLoader.load(spriteSheet: sheet, animation: definition)
            else {
                return nil
            }
            return SpriteAnimator(clip: clip)

        case .gif(let fileName, let loop):
            guard
                let url = CharacterAssetLoader.resolveFileURL(fileName, characterID: character.id, source: character.source),
                let decoded = try? AnimatedImageClipLoader.load(url: url)
            else {
                return nil
            }
            // The manifest's `loop` always wins over whatever loop metadata
            // is baked into the GIF file itself — see `AnimationSource.gif`.
            let clip = AnimationClip(frames: decoded.frames, loop: loop)
            return SpriteAnimator(clip: clip)
        }
    }
}

extension RenderStyle {
    /// Pixel art must not be interpolated — `.none` is what keeps a 64px
    /// frame crisp when it's drawn at `Constants.characterDisplaySize`.
    var interpolation: Image.Interpolation {
        switch self {
        case .pixel: return .none
        case .smooth: return .medium
        }
    }
}
