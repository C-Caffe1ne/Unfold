import CoreGraphics
import Foundation

/// Plays one `SpriteAnimationDefinition` against a `SpriteSheetImage` and
/// publishes the current frame.
///
/// This is the general-purpose playback engine described by the product
/// architecture. It knows nothing about characters, the stretch timer, the
/// overlay, settings, or file import — only a sprite sheet, an animation
/// definition, and wall-clock time. Anything that wants to show *a*
/// character's *some* animation builds one of these; `SpriteAnimator` itself
/// has no idea which character or which animation that is.
///
/// Playback is fully explicit (`play()` / `stop()`) rather than
/// always-on, so nothing keeps a timer ticking — and CPU spent — once the
/// animation is off screen.
@MainActor
final class SpriteAnimator: ObservableObject {

    @Published private(set) var currentFrame: CGImage?
    @Published private(set) var isFinished = false

    private let sheet: SpriteSheetImage
    private let animation: SpriteAnimationDefinition
    private var frameOffset = 0
    private var ticker: Timer?

    init(sheet: SpriteSheetImage, animation: SpriteAnimationDefinition) {
        self.sheet = sheet
        self.animation = animation
        renderCurrentFrame()
    }

    func play() {
        guard ticker == nil, !animation.frames.isEmpty else { return }
        isFinished = false

        let ticker = Timer(timeInterval: animation.frameDuration, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.advance() }
        }
        RunLoop.main.add(ticker, forMode: .common)
        self.ticker = ticker
    }

    func stop() {
        ticker?.invalidate()
        ticker = nil
    }

    private func advance() {
        frameOffset += 1

        if frameOffset >= animation.frames.count {
            guard animation.loop else {
                frameOffset = animation.frames.count - 1
                renderCurrentFrame()
                stop()
                isFinished = true
                return
            }
            frameOffset = 0
        }

        renderCurrentFrame()
    }

    private func renderCurrentFrame() {
        guard animation.frames.indices.contains(frameOffset) else { return }
        currentFrame = sheet.frame(at: animation.frames[frameOffset])
    }

    deinit {
        ticker?.invalidate()
    }
}
