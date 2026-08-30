import CoreGraphics
import Foundation

/// Converts a sprite-sheet-backed animation into the format-agnostic
/// `AnimationClip` the playback engine actually consumes.
///
/// This is the sprite-sheet side of what will eventually be a small family
/// of loaders (a GIF/APNG-backed one arrives in a later phase) — none of
/// which `SpriteAnimator` or `SpriteAnimationView` ever need to know about.
enum AnimationClipLoader {

    enum LoadError: Error, Equatable, CustomStringConvertible {
        case noFrames
        case invalidFrameIndex(Int)

        var description: String {
            switch self {
            case .noFrames:
                return "animation defines no frames"
            case .invalidFrameIndex(let index):
                return "sprite sheet has no cell at frame index \(index)"
            }
        }
    }

    /// Every frame gets the animation's single `1 / fps` duration — this is
    /// the exact timing sprite-sheet playback has always used, just
    /// expressed per-frame instead of as one animation-wide interval, so
    /// converting one of today's animations changes nothing about how it
    /// plays.
    static func load(spriteSheet: SpriteSheetImage, animation: SpriteAnimationDefinition) throws -> AnimationClip {
        guard !animation.frames.isEmpty else { throw LoadError.noFrames }

        let duration = animation.frameDuration
        let frames = try animation.frames.map { index -> AnimationFrame in
            guard let image = spriteSheet.frame(at: index) else {
                throw LoadError.invalidFrameIndex(index)
            }
            return AnimationFrame(image: image, duration: duration)
        }

        return AnimationClip(frames: frames, loop: animation.loop)
    }
}
