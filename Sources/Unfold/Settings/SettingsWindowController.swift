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
    private let editor: CharacterEditorWindowController
    private let library: CharacterLibrary
    private var window: NSWindow?

    init(
        settings: SettingsStore,
        timer: StretchTimer,
        characterManager: CharacterManager,
        editor: CharacterEditorWindowController,
        library: CharacterLibrary
    ) {
        self.settings = settings
        self.timer = timer
        self.characterManager = characterManager
        self.editor = editor
        self.library = library
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
            onIntervalChanged: { [weak timer] in timer?.reset() },
            onCreateCharacter: { [weak editor] in editor?.createNewCharacter() },
            onEditCharacter: { [weak editor] character in editor?.edit(character: character) },
            onDeleteCharacter: { [weak self] character in self?.confirmDelete(character) }
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

    /// Deleting removes files from the user's Mac, so it asks first.
    private func confirmDelete(_ character: Character) {
        let alert = NSAlert()
        alert.messageText = Strings.Settings.deleteConfirmTitle
        alert.informativeText = Strings.Settings.deleteConfirmMessage(character.name)
        alert.alertStyle = .warning
        alert.addButton(withTitle: Strings.Settings.deleteConfirm)
        alert.addButton(withTitle: Strings.Settings.deleteCancel)

        guard alert.runModal() == .alertFirstButtonReturn else { return }

        do {
            try library.delete(id: character.id)
        } catch {
            NSLog("Unfold: could not delete character \"\(character.id)\" — \(error)")
        }
        // Reload either way: if the delete half-succeeded, the list should
        // still reflect what's actually on disk. `CharacterManager` heals
        // the stored selection when the current character disappears.
        characterManager.reloadCatalog()
    }

    private func bringToFront(_ window: NSWindow) {
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
}
