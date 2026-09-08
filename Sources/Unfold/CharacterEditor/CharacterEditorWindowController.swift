import AppKit
import Combine
import SwiftUI

/// One native editing session. File import, session replacement, and closing
/// all pass through the same unsaved-work gate. Saving uses the existing
/// staged/validated character package writer.
@MainActor
final class CharacterEditorWindowController: NSObject, NSWindowDelegate {
    private let library: CharacterLibrary
    private let onCharacterSaved: (Character) -> Void
    private var window: NSWindow?
    private var model: PixelEditorModel?
    private var origin: EditorDocumentOrigin = .unsaved
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
        open(document: PixelDocument(), origin: .unsaved)
    }

    func edit(character: Character) {
        if window != nil, origin.characterID == character.id {
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
            guard let sourceURL = EditorPackageRevision.sourceFile(in: directory) else {
                throw PixelDocumentCodec.Failure.invalid("This character does not have an editable source in your library.")
            }
            var document = try PixelDocumentCodec.load(from: sourceURL)
            guard revision == (try EditorPackageRevision.read(at: directory)) else {
                throw PixelDocumentCodec.Failure.invalid("This character changed while opening. Open it again.")
            }
            document.name = character.name
            guard mayReplaceSession() else { return }
            open(document: document, origin: .character(id: character.id, revision: revision))
        } catch { present(error: error) }
    }

    private func open(document: PixelDocument, origin: EditorDocumentOrigin) {
        teardown()
        let model = PixelEditorModel(document: document)
        self.model = model
        self.origin = origin
        let view = PixelEditorView(model: model,
            saveToLibrary: { [weak self] in _ = self?.saveToLibrary() },
            openDocument: { [weak self] in self?.openDocument() },
            save: { [weak self] in _ = self?.save() },
            saveAs: { [weak self] in self?.saveAs() })
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
        origin = .unsaved
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
    private func saveToLibrary() -> Bool {
        guard let model, !isSaving else { return false }
        model.endStroke()
        isSaving = true
        defer { isSaving = false }
        let name: String
        if origin.characterID == nil {
            guard let entered = promptForName(defaultName: model.document.name) else { return false }
            name = entered
        } else { name = model.document.name }
        do {
            var document = model.document
            document.name = name
            let payload = try PixelDocumentCodec.savePayload(document, characterID: origin.characterID)
            if case .character(let id, let expected) = origin {
                guard let expected, let directory = library.packageDirectory(id: id),
                      let current = try? EditorPackageRevision.read(at: directory), current == expected else {
                    throw PixelDocumentCodec.Failure.invalid("This character was changed or deleted outside this editor. Save your work to a file first, then reopen the character. The library has not been overwritten.")
                }
            }
            let character = try CharacterPackageWriter.write(payload: payload, name: name, into: library)
            // Subsequent saves update the same package rather than duplicating
            // it. Always set the id, even if re-reading the revision fails
            // right after the write — a nil revision makes the next save
            // refuse to overwrite instead of silently starting a second
            // package under a fresh id.
            origin = .character(id: character.id,
                revision: library.packageDirectory(id: character.id).flatMap { try? EditorPackageRevision.read(at: $0) })
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

    /// Writes back to wherever the document came from. Falls through to
    /// Save As when there is no writable destination yet.
    @discardableResult
    private func save() -> Bool {
        guard let model else { return false }
        model.endStroke()
        switch origin.saveAction {
        case .writeLibraryPackage:
            return saveToLibrary()
        case .writeFile(let url, let format):
            do {
                try write(model.document, to: url, format: format)
                model.markSaved()
                return true
            } catch {
                present(error: error)
                return false
            }
        case .askForDestination:
            return saveAs()
        }
    }

    /// Returns true only when the write leaves the *document* saved, not
    /// merely when a file was written. `mayReplaceSession` reads this return
    /// value to decide whether the session is safe to discard — an export to
    /// a lossy format writes a file but must not answer that question yes.
    @discardableResult
    private func saveAs() -> Bool {
        guard let model else { return false }
        model.endStroke()
        let panel = NSSavePanel()
        panel.allowedContentTypes = EditorFileFormat.writable.map(\.utType)
        panel.nameFieldStringValue = "\(model.document.name).\(EditorFileFormat.unfoldSource.fileExtension)"
        guard panel.runModal() == .OK, let url = panel.url else { return false }
        let format = EditorFileFormat.matching(fileExtension: url.pathExtension) ?? .unfoldSource
        guard format.canWrite else {
            present(error: PixelDocumentCodec.Failure.invalid("\(format.displayName) files cannot be written."))
            return false
        }
        do {
            try write(model.document, to: url, format: format)
            // An export leaves the document where it was: still attached to
            // its own origin, still unsaved, because the file that was just
            // written cannot be reopened as this document.
            guard format.preservesDocument else {
                present(exportedWithoutSaving: format)
                return false
            }
            origin = .file(url, format)
            model.markSaved()
            window?.title = "\(model.document.name) — Pixel Editor"
            return true
        } catch {
            present(error: error)
            return false
        }
    }

    /// Single write path for every file format, so Save and Save As cannot
    /// drift apart.
    private func write(_ document: PixelDocument, to url: URL, format: EditorFileFormat) throws {
        let data: Data
        switch format {
        case .unfoldSource: data = try PixelDocumentCodec.encode(document)
        case .png: data = try PixelDocumentCodec.sheetPNG(document)
        case .gif: throw PixelDocumentCodec.Failure.invalid("GIF export is not implemented yet.")
        case .jpeg: throw PixelDocumentCodec.Failure.invalid("JPEG files cannot be written.")
        }
        try data.write(to: url, options: .atomic)
    }

    private func openDocument() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = EditorFileFormat.readable.map(\.utType)
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let format = EditorFileFormat.matching(fileExtension: url.pathExtension) ?? .unfoldSource
            let document: PixelDocument
            switch format {
            case .unfoldSource:
                document = try PixelDocumentCodec.load(from: url)
            case .png, .jpeg:
                let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
                guard size <= Constants.editorMaxSheetDataURLBytes else {
                    throw PixelDocumentCodec.Failure.invalid("The image is too large.")
                }
                document = try PixelDocumentCodec.importPNG(Data(contentsOf: url))
            case .gif:
                throw PixelDocumentCodec.Failure.invalid("Opening GIF files is not supported yet.")
            }
            guard mayReplaceSession() else { return }
            // A raster import (PNG/JPEG) is not something the document can be
            // saved back to — the source file cannot represent layers or
            // multiple frames, so the session starts unattached.
            open(document: document, origin: format.preservesDocument ? .file(url, format) : .unsaved)
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

    /// Says plainly that a file was written but the document is still
    /// unsaved. Without this, a Save As to an export format during the
    /// close prompt would either lose the document or refuse to close with
    /// no explanation.
    private func present(exportedWithoutSaving format: EditorFileFormat) {
        let alert = NSAlert()
        alert.messageText = "Exported, but not saved"
        alert.informativeText = "\(format.displayName) cannot store this document's layers and frames, so your work is still unsaved. Save a \(EditorFileFormat.unfoldSource.displayName) file to keep them."
        alert.addButton(withTitle: "OK")
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}
