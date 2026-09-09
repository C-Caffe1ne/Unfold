import Foundation

/// One playable clip within a sprite sheet: an ordered list of frame
/// indices, how fast to play them, and whether it loops.
///
/// Frame indices are **not** scoped to a single row — `frames` can walk
/// across row boundaries freely (e.g. a 12-frame clip on an 8-column sheet
/// naturally spans two rows). Index → cell position is `SpriteSheetImage`'s
/// job, not this type's.
///
/// This type has no idea what the clip is *for* — "stretch" vs. "idle" is
/// `AnimationKey`'s concern, one layer up in `Character`. That's what lets
/// `SpriteAnimator` stay completely generic.
struct SpriteAnimationDefinition: Equatable {
    let frames: [Int]
    let fps: Double
    let loop: Bool
    var frameDurations: [TimeInterval]? = nil

    var frameDuration: TimeInterval {
        guard fps > 0 else { return .greatestFiniteMagnitude }
        return 1 / fps
    }
}
