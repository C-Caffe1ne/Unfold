import AppKit
import SwiftUI

/// A borderless, transparent, floating panel that shows the current
/// character's `.idle` animation on the desktop for as long as Spine
/// Keepet is running — independent of, and unaffected by, the Stretch
/// Reminder overlay (`OverlayController`/`OverlayWindowController`).
///
/// Phase 1 only: no interaction (click/drag/pointer), no position
/// persistence, no live character-switch tracking, no Settings toggle. The
/// window is created once at launch with whatever character is currently
/// selected and shown for the app's whole lifetime.
///
/// Reuses the exact same pipeline every other character view uses —
/// `CharacterAnimationView` → `AnimationClipLoader`/`AnimatedImageClipLoader`
/// → `AnimationClip` → `SpriteAnimator` → `SpriteAnimationView` — completely
/// unmodified. This controller only owns window chrome/placement; it has no
/// animation logic of its own.
@MainActor
final class DesktopPetWindowController: NSWindowController {

    convenience init(character: Character) {
        let size = CGSize(width: Constants.characterDisplaySize, height: Constants.characterDisplaySize)
        let panel = DesktopPetPanel(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        // Same view + same `.frame(width:height:)` size `StretchOverlayView`
        // already draws the character at — no new renderer, no recentering,
        // no interpolation change, aspect ratio untouched.
        let view = CharacterAnimationView(character: character, key: .idle)
            .frame(width: size.width, height: size.height)
        let hostingController = NSHostingController(rootView: view)
        // `NSHostingController`'s default sizing behavior resizes its
        // window to the SwiftUI content's *preferred* size — which is zero
        // until SwiftUI has run a layout pass. Since the window's exact
        // size is already dictated by `Constants.characterDisplaySize`
        // (not something SwiftUI should get to decide), disable that and
        // set the size explicitly, so the window is never briefly zero-
        // sized or mis-sized before layout catches up.
        hostingController.sizingOptions = []
        panel.contentViewController = hostingController
        panel.setContentSize(size)

        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.isMovableByWindowBackground = false
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        // Phase 1 has no interaction yet: clicks must pass through to
        // whatever app is underneath the pet. Flip to `false` when
        // pointerDown/click/drag land in a later phase.
        panel.ignoresMouseEvents = true

        self.init(window: panel)
        positionAtBottomRight()
    }

    func show() {
        window?.orderFrontRegardless()
    }

    private func positionAtBottomRight() {
        guard let window else { return }
        // `NSScreen.main` can be `nil` (e.g. a headless session); fall back
        // to the first available screen, and do nothing at all — rather
        // than index into a possibly-empty `screens` array — if there's
        // truly no screen.
        guard let screen = NSScreen.main ?? NSScreen.screens.first else { return }

        let origin = Self.bottomRightOrigin(
            visibleFrame: screen.visibleFrame,
            windowSize: window.frame.size,
            margin: Constants.desktopPetScreenMargin
        )
        window.setFrameOrigin(origin)
    }

    /// Bottom-right corner of `visibleFrame` (which already excludes the
    /// Dock and menu bar), inset by `margin` on both edges. Pure and
    /// screen-independent so it's directly unit-testable.
    static func bottomRightOrigin(visibleFrame: CGRect, windowSize: CGSize, margin: CGFloat) -> CGPoint {
        CGPoint(
            x: visibleFrame.maxX - windowSize.width - margin,
            y: visibleFrame.minY + margin
        )
    }
}

/// `NSPanel` subclass: never becomes key or main, on top of the
/// `.nonactivatingPanel` style mask already passed at creation — the pet
/// is purely decorative in Phase 1, so ordering it front must never steal
/// focus or activate Spine Keepet over whatever app the user is using.
private final class DesktopPetPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}
