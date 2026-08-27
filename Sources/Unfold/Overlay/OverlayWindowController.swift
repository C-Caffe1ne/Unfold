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

    func showCentered() {
        guard let window else { return }
        window.center()
        window.orderFrontRegardless()
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
