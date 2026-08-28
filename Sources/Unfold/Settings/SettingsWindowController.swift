import AppKit
import SwiftUI

/// Hosts `SettingsView` in a plain AppKit window. A menu bar (`.accessory`)
/// app has no automatic Settings scene, so we manage one window by hand and
/// reuse it across opens.
@MainActor
final class SettingsWindowController {

    private let settings: SettingsStore
    private let timer: StretchTimer
    private let characterManager: CharacterManager
    private var window: NSWindow?

    init(settings: SettingsStore, timer: StretchTimer, characterManager: CharacterManager) {
        self.settings = settings
        self.timer = timer
        self.characterManager = characterManager
    }

    func show() {
        if let window {
            bringToFront(window)
            return
        }

        let view = SettingsView(
            settings: settings,
            characterManager: characterManager,
            // Changing the interval starts a fresh full-length countdown
            // from now, the same policy the menu bar's own interval picker
            // already uses — never an immediate reminder just because the
            // setting changed.
            onIntervalChanged: { [weak timer] in timer?.reset() }
        )
        let hosting = NSHostingController(rootView: view)
        let window = NSWindow(contentViewController: hosting)
        window.title = Strings.Settings.windowTitle
        // A small utility settings window, not a resizable app window.
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
