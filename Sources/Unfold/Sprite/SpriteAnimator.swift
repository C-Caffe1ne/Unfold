import CoreGraphics
import Foundation

/// Plays an `AnimationClip` and publishes the current frame.
///
/// This is the general-purpose playback engine described by the product
/// architecture. It knows nothing about characters, the stretch timer, the
/// overlay, settings, or file import — nor does it know whether its frames
/// came from a sprite sheet, a GIF, or anything else; `AnimationClip` is
/// already fully decoded by the time it gets here. Anything that wants to
/// show *a* character's *some* animation builds one of these.
///
/// Each frame carries its own display duration, so playback advances by
/// scheduling a single one-shot timer for "how long the current frame
/// stays up," then rescheduling with the *next* frame's own duration —
/// rather than one fixed-interval repeating timer. A sprite-sheet-derived
/// clip happens to give every frame the same duration (its animation's
/// `1 / fps`), so this reduces to exactly the old fixed-interval timing;
/// nothing here assumes frames are evenly spaced.
///
/// Playback is fully explicit (`play()` / `stop()`) rather than
/// always-on, so nothing keeps a timer ticking — and CPU spent — once the
/// animation is off screen.
@MainActor
final class SpriteAnimator: ObservableObject {

    @Published private(set) var currentFrame: CGImage?
    @Published private(set) var isFinished = false

    /// Called exactly once, synchronously, the moment `isFinished` becomes
    /// `true` — right after a non-looping clip renders its last frame.
    /// Generic on purpose, not an interaction-specific hack: every existing
    /// consumer (Stretch overlay, GIF- and sprite-sheet-backed clips alike)
    /// is unaffected since this defaults to `nil` — Desktop Pet interaction
    /// (Phase 2) is simply the first caller to actually set it, to know
    /// when a `.click`/`.pointerUp` reaction has finished playing without
    /// polling `isFinished` on a timer of its own.
    var onFinished: (() -> Void)?

    private let clip: AnimationClip
    private var frameOffset = 0
    private var ticker: Timer?

    init(clip: AnimationClip) {
        self.clip = clip
        renderCurrentFrame()
    }

    /// Starts (or resumes) playback. A clip that already ran to completion
    /// (`isFinished`) restarts from the first frame — matching production
    /// behavior of playing the full animation again on every stretch
    /// trigger. A clip merely paused with `stop()` before finishing resumes
    /// from wherever it left off.
    func play() {
        guard ticker == nil, !clip.frames.isEmpty else { return }

        if isFinished {
            frameOffset = 0
            renderCurrentFrame()
        }
        isFinished = false
        scheduleNext()
    }

    func stop() {
        ticker?.invalidate()
        ticker = nil
    }

    /// Schedules a one-shot timer for the *currently displayed* frame's
    /// duration — i.e. how much longer it stays on screen before
    /// `advance()` moves to the next one. Called once from `play()` for the
    /// frame already on screen, and again from `advance()` for each frame
    /// it renders.
    private func scheduleNext() {
        guard clip.frames.indices.contains(frameOffset) else { return }

        let duration = clip.frames[frameOffset].duration
        let timer = Timer(timeInterval: duration, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated { self?.advance() }
        }
        RunLoop.main.add(timer, forMode: .common)
        ticker = timer
    }

    /// Moves to the next frame: wraps to the start for a looping clip,
    /// holds on the last frame and marks `isFinished` for a non-looping
    /// one. `internal` (not `private`) so tests can drive frame-by-frame
    /// transitions directly and deterministically, without waiting on the
    /// real `Timer` this is normally only ever called from.
    func advance() {
        guard !isFinished else { return }

        frameOffset += 1

        if frameOffset >= clip.frames.count {
            guard clip.loop else {
                frameOffset = clip.frames.count - 1
                renderCurrentFrame()
                stop()
                isFinished = true
                onFinished?()
                return
            }
            frameOffset = 0
        }

        renderCurrentFrame()
        scheduleNext()
    }

    private func renderCurrentFrame() {
        guard clip.frames.indices.contains(frameOffset) else { return }
        currentFrame = clip.frames[frameOffset].image
    }

    deinit {
        ticker?.invalidate()
    }
}
