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
        case invalidDurations
        case invalidFrameIndex(Int)

        var description: String {
            switch self {
            case .invalidDurations:
                return "animation has invalid per-frame durations"
            case .noFrames:
                return "animation defines no frames"
            case .invalidFrameIndex(let index):
                return "sprite sheet has no cell at frame index \(index)"
            }
        }
    }

    /// Legacy clips inherit FPS; newer clips preserve each ordered frame hold.
    static func load(spriteSheet: SpriteSheetImage, animation: SpriteAnimationDefinition) throws -> AnimationClip {
        guard !animation.frames.isEmpty else { throw LoadError.noFrames }

        if let durations = animation.frameDurations {
            guard durations.count == animation.frames.count,
                  durations.allSatisfy({ $0.isFinite && (0.01...60).contains($0) }) else {
                throw LoadError.invalidDurations
            }
        }
        let frames = try animation.frames.enumerated().map { offset, index -> AnimationFrame in
            guard let image = spriteSheet.frame(at: index) else {
                throw LoadError.invalidFrameIndex(index)
            }
            return AnimationFrame(image: image, duration: animation.frameDurations?[offset] ?? animation.frameDuration)
        }

        return AnimationClip(frames: frames, loop: animation.loop)
    }
}
