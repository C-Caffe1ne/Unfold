import AppKit
import Combine
import SwiftUI
import UniformTypeIdentifiers

/// One native editing session. File import, session replacement, and closing
/// all pass through the same unsaved-work gate. Saving uses the existing
/// staged/validated character package writer.
@MainActor
final class CharacterEditorWindowController: NSObject, NSWindowDelegate {
    private let library: CharacterLibrary
    private let onCharacterSaved: (Character) -> Void
    private var window: NSWindow?
    private var model: PixelEditorModel?
    private var editingCharacterID: String?
    private var editingRevision: EditorPackageRevision?
    private var observation: AnyCancellable?
    private var isSaving = false

    init(library: CharacterLibrary, onCharacterSaved: @escaping (Character) -> Void) {
        self.library = library
        self.onCharacterSaved = onCharacterSaved
    }

    deinit {
        MainActor.assumeIsolated {
            window?.delegate = nil
            window?.close()
        }
    }

    func createNewCharacter() {
        guard mayReplaceSession() else { return }
        open(document: PixelDocument(), characterID: nil)
    }

    func edit(character: Character) {
        if window != nil, editingCharacterID == character.id {
            NSApp.activate(ignoringOtherApps: true)
            window?.makeKeyAndOrderFront(nil)
            return
        }
        do {
            guard let directory = library.packageDirectory(id: character.id) else {
                throw PixelDocumentCodec.Failure.invalid("This character does not have an editable source in your library.")
            }
            // Decode before touching the current session. Never substitute a
            // blank canvas for a missing or damaged source of an existing ID.
            let revision = try EditorPackageRevision.read(at: directory)
            var document = try PixelDocumentCodec.load(from: directory.appendingPathComponent(Constants.characterEditorSourceFileName))
            guard revision == (try EditorPackageRevision.read(at: directory)) else {
                throw PixelDocumentCodec.Failure.invalid("This character changed while opening. Open it again.")
            }
            document.name = character.name
            guard mayReplaceSession() else { return }
            open(document: document, characterID: character.id, revision: revision)
        } catch { present(error: error) }
    }

    private func open(document: PixelDocument, characterID: String?, revision: EditorPackageRevision? = nil) {
        teardown()
        let model = PixelEditorModel(document: document)
        self.model = model
        editingCharacterID = characterID
        editingRevision = revision
        let view = PixelEditorView(model: model,
            save: { [weak self] in _ = self?.save() },
            importDocument: { [weak self] in self?.importDocument() },
            exportDocument: { [weak self] in self?.export(piskel: true) },
            exportPNG: { [weak self] in self?.export(piskel: false) })
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1120, height: 780),
            styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "\(document.name) — Pixel Editor"
        window.contentView = NSHostingView(rootView: view)
        window.minSize = NSSize(width: 920, height: 660)
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        self.window = window
        // @Published sends before mutation; update the window flag next turn.
        observation = model.objectWillChange.sink { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.window?.isDocumentEdited = self?.model?.isDirty ?? false
            }
        }
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    func close() { window?.performClose(nil) }

    func canTerminate() -> Bool { mayReplaceSession() }

    func windowShouldClose(_ sender: NSWindow) -> Bool { mayReplaceSession() }
    func windowWillClose(_ notification: Notification) { teardown(closeWindow: false) }
    func windowDidResignKey(_ notification: Notification) {
        model?.endStroke()
        model?.isPlaying = false
    }

    private func teardown(closeWindow: Bool = true) {
        observation = nil
        model?.endStroke()
        model?.isPlaying = false
        window?.delegate = nil
        if closeWindow { window?.close() }
        window?.contentView = nil
        window = nil
        model = nil
        editingCharacterID = nil
        editingRevision = nil
    }

    /// Returns false on cancelled prompts and failed saves, leaving the
    /// window, selection, history, and overwrite target intact.
    private func mayReplaceSession() -> Bool {
        guard !isSaving else { return false }
        model?.endStroke()
        guard model?.isDirty == true else { return true }
        let alert = NSAlert()
        alert.messageText = "Save your pixel art before closing?"
        alert.informativeText = "Your changes will be lost if you discard them."
        alert.addButton(withTitle: "Save")
        alert.addButton(withTitle: "Cancel")
        alert.addButton(withTitle: "Discard Changes")
        switch alert.runModal() {
        case .alertFirstButtonReturn: return save()
        case .alertThirdButtonReturn: return true
        default: return false
        }
    }

    @discardableResult
    private func save() -> Bool {
        guard let model, !isSaving else { return false }
        model.endStroke()
        isSaving = true
        defer { isSaving = false }
        let name: String
        if editingCharacterID == nil {
            guard let entered = promptForName(defaultName: model.document.name) else { return false }
            name = entered
        } else { name = model.document.name }
        do {
            var document = model.document
            document.name = name
            let payload = try PixelDocumentCodec.savePayload(document, characterID: editingCharacterID)
            if let id = editingCharacterID {
                guard let expected = editingRevision, let directory = library.packageDirectory(id: id),
                      let current = try? EditorPackageRevision.read(at: directory), current == expected else {
                    throw PixelDocumentCodec.Failure.invalid("This character was changed or deleted outside this editor. Export your Piskel file to keep this work, then reopen the character. The library has not been overwritten.")
                }
            }
            let character = try CharacterPackageWriter.write(payload: payload, name: name, into: library)
            // Subsequent saves update the same package rather than duplicating it.
            editingCharacterID = character.id
            editingRevision = library.packageDirectory(id: character.id).flatMap { try? EditorPackageRevision.read(at: $0) }
            model.change { $0.name = name }
            model.markSaved()
            window?.title = "\(name) — Pixel Editor"
            window?.isDocumentEdited = false
            onCharacterSaved(character)
            return true
        } catch {
            present(error: error)
            return false
        }
    }

    private func promptForName(defaultName: String) -> String? {
        let alert = NSAlert()
        alert.messageText = Strings.Editor.namePromptTitle
        alert.informativeText = Strings.Editor.namePromptMessage
        alert.addButton(withTitle: Strings.Editor.namePromptConfirm)
        alert.addButton(withTitle: Strings.Editor.namePromptCancel)
        let field = NSTextField(frame: NSRect(x: 0, y: 0, width: 260, height: 24))
        field.stringValue = defaultName
        alert.accessoryView = field
        alert.window.initialFirstResponder = field
        guard alert.runModal() == .alertFirstButtonReturn else { return nil }
        let value = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else {
            present(error: PixelDocumentCodec.Failure.invalid("A character needs a name."))
            return nil
        }
        return value
    }

    private func importDocument() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.png, UTType(filenameExtension: "piskel") ?? .data]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let document: PixelDocument
            if url.pathExtension.lowercased() == "png" {
                let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
                guard size <= Constants.editorMaxSheetDataURLBytes else { throw PixelDocumentCodec.Failure.invalid("The PNG is too large.") }
                document = try PixelDocumentCodec.importPNG(Data(contentsOf: url))
            } else { document = try PixelDocumentCodec.load(from: url) }
            guard mayReplaceSession() else { return }
            // Imported files always start a new package; never inherit an ID
            // from the document that happened to be open before import.
            open(document: document, characterID: nil)
        } catch { present(error: error) }
    }

    private func export(piskel: Bool) {
        guard let model else { return }
        model.endStroke()
        let panel = NSSavePanel()
        panel.allowedContentTypes = piskel ? [UTType(filenameExtension: "piskel") ?? .data] : [.png]
        panel.nameFieldStringValue = piskel ? "character.piskel" : "spritesheet.png"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let data: Data
            if piskel { data = try PixelDocumentCodec.encode(model.document) }
            else { data = try PixelDocumentCodec.sheetPNG(model.document) }
            try data.write(to: url, options: .atomic)
        } catch { present(error: error) }
    }

    private func present(error: Error) {
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = "Pixel Editor"
        alert.informativeText = (error as? LocalizedError)?.errorDescription ?? String(describing: error)
        alert.addButton(withTitle: "OK")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}
