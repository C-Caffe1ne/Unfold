import Foundation

/// V1 limits for any animated image (GIF today; APNG or others later) that
/// gets decoded into an `AnimationClip`. Centralized here — rather than
/// scattered through `AnimatedImageClipLoader` — specifically so a future
/// Creator/custom-import feature can tune these in one place instead of
/// hunting through decode logic.
enum AnimatedImageValidationPolicy {
    /// Widest a decoded frame's canvas may be.
    static let maxWidth = 512
    /// Tallest a decoded frame's canvas may be.
    static let maxHeight = 512
    /// Most frames a single clip may contain.
    static let maxFrameCount = 60
    /// Largest source file `load(url:)` will even attempt to open.
    static let maxFileSizeBytes = 5 * 1024 * 1024
    /// Shortest frame duration playback will actually honor. Anything
    /// shorter is clamped up to this, not rejected — a very fast animation
    /// should still play, just not faster than this floor.
    static let minFrameDuration: TimeInterval = 0.02
    /// Used whenever a frame's real duration is missing or unusable
    /// (absent, non-finite, or <= 0).
    static let fallbackFrameDuration: TimeInterval = 0.1
}
