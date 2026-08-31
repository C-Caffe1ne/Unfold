import CoreGraphics

/// Decides whether the Desktop Pet window should currently intercept mouse
/// events (`ignoresMouseEvents = false`) or let them pass through to
/// whatever's underneath (`= true`). Pure decision logic — no `NSWindow`/
/// `NSPanel`/`Timer`/`NSEvent` anywhere in it, so it's directly testable
/// with synthetic geometry and a real `CGImageAlphaHitTester`.
/// `DesktopPetWindowController` is the one real caller: it polls
/// `NSEvent.mouseLocation`/`window.frame`/`animator.currentFrame` on a
/// lightweight `Timer` (`ignoresMouseEvents = true` stops the window from
/// receiving `mouseMoved` itself, so something outside it has to notice the
/// cursor re-entering a visible pixel) and feeds them in here.
enum DesktopPetHitTestPolicy {

    /// Per spec §11/§12: once a press has been accepted (`.pointerDown`,
    /// which also covers mid-drag — see `PetInteractionState`'s doc comment
    /// on why dragging isn't its own case) event ownership must not be lost
    /// just because the reaction animation's visible pixels moved/shrank
    /// under the still-held cursor, or the cursor momentarily left the
    /// window's own frame during a fast drag. `isDragging` is checked
    /// independently as a defensive second signal for the same rule, even
    /// though in practice it only ever becomes `true` while `state` is
    /// already `.pointerDown`.
    static func shouldAcceptMouseEvents(
        interactionState: PetInteractionState?,
        isDragging: Bool,
        globalMouseLocation: CGPoint,
        windowFrame: CGRect,
        currentFrame: CGImage?,
        hitTester: CGImageAlphaHitTester
    ) -> Bool {
        if interactionState == .pointerDown || isDragging {
            return true
        }
        guard windowFrame.contains(globalMouseLocation) else {
            return false
        }
        // No decoded frame to test against (e.g. the SF Symbol fallback
        // when a character's asset fails to load) — preserve the
        // pre-Phase-4 "whole canvas is the hit area" behavior rather than
        // going permanently click-through.
        guard let currentFrame else {
            return true
        }

        let windowLocalX = globalMouseLocation.x - windowFrame.minX
        let windowLocalY = globalMouseLocation.y - windowFrame.minY
        // AppKit's window-local origin is bottom-left, y increasing upward;
        // `SpriteAnimationView` renders top-left-origin, y increasing
        // downward (see `CGImageAlphaHitTester`'s doc comment) — flip Y
        // here, once, at the one real seam between the two coordinate
        // spaces.
        let uiPoint = CGPoint(x: windowLocalX, y: windowFrame.height - windowLocalY)
        let bounds = CGRect(origin: .zero, size: windowFrame.size)

        return hitTester.containsVisiblePixel(image: currentFrame, at: uiPoint, renderedIn: bounds)
    }
}
