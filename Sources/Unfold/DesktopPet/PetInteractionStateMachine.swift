import CoreGraphics
import Foundation

/// Pure interaction/timing decision logic for the Desktop Pet's mouse
/// reactions — no AppKit, no animation playback. Timestamps and locations
/// are always passed in (never read from the wall clock or the event
/// system internally), so every transition is directly and deterministically
/// testable.
///
/// `DesktopPetWindowController` owns the two responsibilities this type
/// deliberately doesn't: receiving real `NSEvent`s, and turning a
/// `PetInteractionState` into an actual animation. This type only decides
/// *what state* the pet should be in.
final class PetInteractionStateMachine {

    private(set) var state: PetInteractionState = .idle

    /// Captured on every mouseDown, and the fixed baseline `mouseDragged`
    /// measures distance from — never updated by `mouseDragged` itself, so
    /// distance is measured correctly even once the window has started
    /// moving (see the Phase 3 report on why global screen coordinates are
    /// what callers should actually pass in).
    private(set) var lastMouseDownLocation: CGPoint?

    /// Phase 3: drag is deliberately *not* a new `PetInteractionState` case
    /// — the pet stays visually `.pointerDown` for the whole drag. This is
    /// the separate "interaction context" flag the Phase 3 spec calls for.
    /// Latches `true` once `mouseDragged` crosses `dragThreshold` and stays
    /// `true` for the rest of the press, even if the cursor drifts back
    /// near the origin — reset only by the next `mouseDown`/`mouseUp`.
    private(set) var isDragging = false

    private var mouseDownAt: Date?
    private let clickThreshold: TimeInterval
    private let dragThreshold: CGFloat

    /// `Date` stores an absolute offset from its reference date — for a
    /// real (large-magnitude) timestamp, computing `mouseUp.timeIntervalSince
    /// (mouseDown)` can be off from the "true" elapsed time by a
    /// double-precision ULP or two (routinely ~1e-8s at real Unix-epoch
    /// magnitudes). Comparing to `clickThreshold` with this tolerance means
    /// a press that is, for all real purposes, exactly at the threshold
    /// reliably reads as a click regardless of which way that noise falls —
    /// never a difference a person could perceive, but real timestamps at
    /// production magnitude can and do land within it.
    private static let boundaryEpsilon: TimeInterval = 1e-6

    init(clickThreshold: TimeInterval = Constants.petClickThreshold, dragThreshold: CGFloat = Constants.petDragThreshold) {
        self.clickThreshold = clickThreshold
        self.dragThreshold = dragThreshold
    }

    /// A new mouseDown always cancels whatever reaction is currently
    /// playing (`.click`/`.pointerUp`) and starts a fresh `.pointerDown` —
    /// including a duplicate mouseDown received while already in
    /// `.pointerDown` (e.g. a desynced event pair), which simply restarts
    /// the hold timer *and* the drag judgment from the new timestamp/
    /// location rather than producing an inconsistent state.
    @discardableResult
    func mouseDown(at timestamp: Date, location: CGPoint) -> PetInteractionState {
        mouseDownAt = timestamp
        lastMouseDownLocation = location
        isDragging = false
        state = .pointerDown
        return state
    }

    /// Only meaningful while `.pointerDown` is actually being tracked — a
    /// stray `mouseDragged` with no active press (or arriving after the
    /// press already resolved) is ignored outright, same as an unmatched
    /// `mouseUp`. Distance is always measured from `lastMouseDownLocation`,
    /// which this method never updates, so the judgment stays correct for
    /// the whole press regardless of how many `mouseDragged` calls come in.
    func mouseDragged(to location: CGPoint) {
        guard state == .pointerDown, let origin = lastMouseDownLocation, !isDragging else { return }

        let dx = location.x - origin.x
        let dy = location.y - origin.y
        let distance = (dx * dx + dy * dy).squareRoot()
        if distance >= dragThreshold {
            isDragging = true
        }
    }

    /// A mouseUp only means something while a `.pointerDown` is actually
    /// being tracked — one with no matching mouseDown (a desynced or
    /// duplicate event) is ignored outright rather than guessed at.
    ///
    /// Phase 3: if any drag occurred during this press, the result is
    /// always `.pointerUp` — never `.click` — regardless of how short the
    /// press itself was; see the Phase 3 report §6.
    @discardableResult
    func mouseUp(at timestamp: Date) -> PetInteractionState {
        guard state == .pointerDown, let downAt = mouseDownAt else { return state }

        let heldDuration = timestamp.timeIntervalSince(downAt)
        let wasDragging = isDragging
        mouseDownAt = nil
        isDragging = false
        state = (!wasDragging && heldDuration <= clickThreshold + Self.boundaryEpsilon) ? .click : .pointerUp
        return state
    }

    /// The currently-playing reaction animation finished.
    /// - `.click`/`.pointerUp` return to `.idle`.
    /// - `.pointerDown` deliberately does nothing — the pet holds its last
    ///   frame until the real mouseUp arrives.
    /// - `.idle` is a no-op guard against a stray callback (idle never
    ///   "finishes" in the reaction sense).
    @discardableResult
    func animationFinished() -> PetInteractionState {
        switch state {
        case .click, .pointerUp:
            state = .idle
        case .pointerDown, .idle:
            break
        }
        return state
    }
}
