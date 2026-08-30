import CoreGraphics
import Foundation

/// One decoded frame ready to display: an image and how long it stays on
/// screen before the next frame. `duration` is the frame's *own* dwell
/// time — not a fixed animation-wide fps — so a clip can freely mix frame
/// durations (needed once GIF/APNG sources land; a sprite-sheet-derived
/// clip just happens to give every frame the same duration).
struct AnimationFrame {
    let image: CGImage
    let duration: TimeInterval
}

/// A fully-decoded, format-agnostic animation ready to play. This is the
/// only thing `SpriteAnimator` knows about — it has no idea whether the
/// frames came from a sprite sheet, a GIF, or anything else added later.
/// `AnimationClipLoader` (and, in a future phase, a GIF/APNG equivalent)
/// is what produces one of these.
struct AnimationClip {
    let frames: [AnimationFrame]
    let loop: Bool
}
