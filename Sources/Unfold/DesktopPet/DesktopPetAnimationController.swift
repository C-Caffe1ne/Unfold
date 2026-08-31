import AppKit
import Foundation

/// Owns the `SpriteAnimator` currently on screen for the Desktop Pet and
/// swaps it out as `PetInteractionStateMachine` transitions between idle
/// and pointer reactions. `DesktopPetWindowController` only forwards raw
/// AppKit mouse events here — this is where "which state, which clip,
/// which animator" gets decided.
///
/// Reuses `SpriteAnimator`/`AnimationClipLoader` completely unmodified. The
/// only new production capability either of them gained this phase is
/// `SpriteAnimator.onFinished` — a generic completion callback, not an
/// interaction-specific hack — and this controller is simply the first
/// caller to actually use it.
@MainActor
final class DesktopPetAnimationController: ObservableObject {

    @Published private(set) var animator: SpriteAnimator

    var interactionState: PetInteractionState { stateMachine.state }

    /// Phase 3: read by `DesktopPetWindowController` *before* calling
    /// `mouseUp(at:)` (which resets it) to decide whether this press ended
    /// a drag — and therefore whether a new position should be persisted.
    var isDragging: Bool { stateMachine.isDragging }

    private let stateMachine = PetInteractionStateMachine()
    private let idleAnimator: SpriteAnimator

    /// Phase 2 placeholder: no real pointerDown/pointerUp/click art exists
    /// yet, and this phase must not invent one as a production resource
    /// (see the Phase 2 report). Reuses the character's own idle frames —
    /// already loaded for `idleAnimator` — as a non-looping clip, purely so
    /// the real state machine + `SpriteAnimator.onFinished` completion path
    /// gets exercised end-to-end with real production art instead of a
    /// fabricated placeholder image. Swap this for real per-state clips
    /// once that art exists; nothing else in this type needs to change.
    private let reactionClip: AnimationClip

    init?(character: Character) {
        guard
            let idleSource = character.animation(for: .idle),
            case .spriteSheet(let idleDefinition) = idleSource,
            let sheet = CharacterAssetLoader.loadSpriteSheetImage(for: character),
            let idleClip = try? AnimationClipLoader.load(spriteSheet: sheet, animation: idleDefinition)
        else {
            return nil
        }

        idleAnimator = SpriteAnimator(clip: idleClip)
        reactionClip = AnimationClip(frames: idleClip.frames, loop: false)
        animator = idleAnimator
        playRespectingReduceMotion(idleAnimator)
    }

    func mouseDown(at timestamp: Date, location: CGPoint) {
        stateMachine.mouseDown(at: timestamp, location: location)
        enterCurrentState()
    }

    func mouseUp(at timestamp: Date) {
        stateMachine.mouseUp(at: timestamp)
        enterCurrentState()
    }

    /// Never touches `animator`/`enterCurrentState()` — dragging is not a
    /// new `PetInteractionState`, so the currently-playing `.pointerDown`
    /// reaction (or its held last frame) must be completely undisturbed by
    /// window movement. A fresh `SpriteAnimator` is never created here.
    func mouseDragged(to location: CGPoint) {
        stateMachine.mouseDragged(to: location)
    }

    private func enterCurrentState() {
        animator.stop()

        switch stateMachine.state {
        case .idle:
            animator = idleAnimator
            playRespectingReduceMotion(idleAnimator)

        case .pointerDown, .pointerUp, .click:
            let reactionAnimator = SpriteAnimator(clip: reactionClip)
            // Runs only while `reactionAnimator` is still the one and only
            // strong owner of this closure — once superseded (`animator`
            // reassigned elsewhere), nothing keeps `reactionAnimator` alive,
            // its `deinit` invalidates its own timer, and this callback
            // simply never fires again. No "is this still current" check
            // is needed on top of that.
            reactionAnimator.onFinished = { [weak self] in
                self?.handleReactionFinished()
            }
            animator = reactionAnimator

            if NSWorkspace.shared.accessibilityDisplayShouldReduceMotion {
                // No animation will play to ever trigger `onFinished` —
                // resolve the transition immediately instead of leaving a
                // `.click`/`.pointerUp` reaction (or, worse, `.pointerDown`)
                // stuck forever.
                handleReactionFinished()
            } else {
                reactionAnimator.play()
            }
        }
    }

    private func handleReactionFinished() {
        let previousState = stateMachine.state
        let newState = stateMachine.animationFinished()
        // `.pointerDown` intentionally reports back the same state (hold
        // the last frame) — nothing to swap in that case.
        guard newState != previousState else { return }
        enterCurrentState()
    }

    private func playRespectingReduceMotion(_ animator: SpriteAnimator) {
        // Same policy `SpriteAnimationView` already applies to its own
        // auto-play on appear — reused here, not reinvented, since this
        // controller calls `play()` directly instead of going through that
        // view's `onAppear`.
        guard !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
        animator.play()
    }
}
