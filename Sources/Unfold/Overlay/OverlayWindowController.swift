import AppKit
import SwiftUI

/// A borderless, floating panel used to host the stretch-reminder overlay.
///
/// It's an `NSPanel` (not a plain SwiftUI window) so it can float above the
/// user's current app without taking over the screen: no title bar, no Dock
/// bounce, and — via `.nonactivatingPanel` — no forced app switch just to
/// show a reminder.
@MainActor
final class OverlayWindowController: NSWindowController {

    convenience init(rootView: some View) {
        let panel = OverlayPanel(
            contentRect: NSRect(origin: .zero, size: Constants.overlaySize),
            styleMask: [.nonactivatingPanel, .fullSizeContentView],
            backing: .buffered,
            defer: false
        )

        panel.contentViewController = NSHostingController(rootView: rootView)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.isMovableByWindowBackground = true
        panel.hidesOnDeactivate = false

        self.init(window: panel)
    }

    /// Centers on the screen the user is currently working on, not
    /// necessarily the primary display. `NSWindow.center()` centers on
    /// whichever screen contains the *window's current frame* — for a
    /// freshly created window that's effectively always the primary screen,
    /// which is wrong on a multi-monitor setup where the user is working on
    /// a secondary display. Using the screen under the mouse cursor is a
    /// simple, good-enough proxy for "active screen" without any window-
    /// or space-tracking machinery.
    func showCentered() {
        guard let window else { return }

        if let screen = Self.activeScreen() {
            let visible = screen.visibleFrame
            let size = window.frame.size
            window.setFrameOrigin(NSPoint(
                x: visible.midX - size.width / 2,
                y: visible.midY - size.height / 2
            ))
        } else {
            // No screen could be determined at all (e.g. a headless
            // session) — fall back to AppKit's own default rather than
            // indexing into a possibly-empty `NSScreen.screens`.
            window.center()
        }

        window.orderFrontRegardless()
    }

    private static func activeScreen() -> NSScreen? {
        let mouseLocation = NSEvent.mouseLocation
        return NSScreen.screens.first { $0.frame.contains(mouseLocation) }
            ?? NSScreen.main
            ?? NSScreen.screens.first
    }

    func dismiss() {
        window?.orderOut(nil)
    }
}

/// `NSPanel` subclass that allows a nonactivating panel to still become key,
/// so its Dismiss button responds to clicks without the panel dragging
/// focus away from whatever app the user was in.
private final class OverlayPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
