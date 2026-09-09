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
            // Always passes today, and is not dead code. `saveAction` only
            // ever yields `.writeFile` for a format whose `preservesDocument`
            // is true, so `.unfoldSource` is the only format that reaches
            // this line and its warning is always nil. The guard is here so
            // that the day a second format becomes save-in-place, Save cannot
            // quietly start making lossy writes unwarned while Save As still
            // asks.
            guard confirmLossyWrite(model.document, format: format) else { return false }
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
        guard confirmLossyWrite(model.document, format: format) else { return false }
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

    /// Warns before a write that cannot carry everything the document holds.
    /// Returns false when the user backs out.
    ///
    /// Both save paths ask this rather than the one that exports today,
    /// because which formats are lossy is a property of the formats, not of
    /// the command the user reached for.
    private func confirmLossyWrite(_ document: PixelDocument, format: EditorFileFormat) -> Bool {
        guard let warning = EditorWriteWarning.text(for: document, format: format) else { return true }
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = "Save as \(format.displayName)?"
        alert.informativeText = warning
        alert.addButton(withTitle: "Save")
        alert.addButton(withTitle: "Cancel")
        NSApp.activate(ignoringOtherApps: true)
        return alert.runModal() == .alertFirstButtonReturn
    }

    /// Single write path for every file format, so Save and Save As cannot
    /// drift apart.
    private func write(_ document: PixelDocument, to url: URL, format: EditorFileFormat) throws {
        let data: Data
        switch format {
        case .unfoldSource: data = try PixelDocumentCodec.encode(document)
        case .png: data = try PixelDocumentCodec.sheetPNG(document)
        case .gif: data = try AnimatedGIFEncoder.encode(document)
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
            // The file's bytes decide what it is; the extension only decided
            // whether the panel would offer it. A JPEG named .png still opens.
            let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= PixelDocumentCodec.maximumSourceBytes else {
                throw PixelDocumentCodec.Failure.invalid("The file is too large.")
            }
            let data = try Data(contentsOf: url)
            guard let format = RasterImageDecoder.detectFormat(data) else {
                // Not a raster image, so it must be a source document.
                let document = try PixelDocumentCodec.decode(data)
                guard mayReplaceSession() else { return }
                open(document: document, origin: .file(url, .unfoldSource))
                return
            }
            let image = try RasterImageDecoder.decode(data, format: format)
            let suggestion = ImportOptions.suggestion(width: image.width, height: image.height)
            if suggestion == .single {
                let document = try ImportOptions.apply(.single, to: image)
                guard mayReplaceSession() else { return }
                open(document: document, origin: .unsaved)
                return
            }
            presentImportOptions(for: image, suggestion: suggestion)
        } catch { present(error: error) }
    }

    /// A raster import always starts an unattached session: the PNG or JPEG
    /// it came from cannot hold layers or frames, so it is not somewhere the
    /// document could be saved back to.
    private func presentImportOptions(for image: RasterImageDecoder.Image, suggestion: ImportOptions) {
        let sheet = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 380, height: 460),
            styleMask: [.titled], backing: .buffered, defer: false)
        // The buttons only record the answer and end the session. Acting on it
        // has to wait until `runModal` has returned: applying an import can
        // reach `mayReplaceSession`, which runs an alert modally, and starting
        // a nested modal session immediately after `stopModal` can consume the
        // stop that was meant for this one.
        var chosen: ImportOptions?
        let dismiss: (ImportOptions?) -> Void = { options in
            chosen = options
            NSApp.stopModal()
        }
        // The dialog still reports and edits the image's real dimensions; only
        // what it draws is reduced.
        let preview = image.thumbnail(maxSide: 512)
        let view = ImportOptionsView(
            imageWidth: image.width, imageHeight: image.height,
            preview: PixelDocumentCodec.image(preview.pixels, width: preview.width, height: preview.height),
            suggestion: suggestion,
            confirm: { dismiss($0) }, cancel: { dismiss(nil) })
        sheet.contentView = NSHostingView(rootView: view)
        sheet.center()
        NSApp.runModal(for: sheet)

        sheet.orderOut(nil)
        // Also breaks a retain cycle: the hosting view holds the SwiftUI
        // closures, which capture `sheet`, so the window and the imported
        // image it previews would otherwise never deallocate.
        sheet.contentView = nil

        // Cancelling leaves the current session exactly as it was — nothing
        // has been mutated at this point, and `mayReplaceSession` has not run.
        guard let chosen else { return }
        do {
            let document = try ImportOptions.apply(chosen, to: image)
            guard mayReplaceSession() else { return }
            open(document: document, origin: .unsaved)
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
