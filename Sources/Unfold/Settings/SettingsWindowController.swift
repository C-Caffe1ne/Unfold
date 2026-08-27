import AppKit
import SwiftUI

/// Hosts `SettingsView` in a plain AppKit window. A menu bar (`.accessory`)
/// app has no automatic Settings scene, so we manage one window by hand and
/// reuse it across opens.
@MainActor
final class SettingsWindowController {

    private let settings: SettingsStore
    private var window: NSWindow?

    init(settings: SettingsStore) {
        self.settings = settings
    }

    func show() {
        if let window {
            bringToFront(window)
            return
        }

        let hosting = NSHostingController(rootView: SettingsView(settings: settings))
        let window = NSWindow(contentViewController: hosting)
        window.title = Strings.Settings.windowTitle
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.center()

        self.window = window
        bringToFront(window)
    }

    private func bringToFront(_ window: NSWindow) {
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
}
