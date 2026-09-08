import Foundation

/// Where the open document came from, and therefore what a plain Save does.
///
/// One `⌘S` has to behave correctly in two different contexts: a document
/// opened from disk saves back to its file, a character opened from the
/// library saves back into its package. Carrying the origin on the session
/// is what lets a single command do both without the two paths knowing
/// about each other.
enum EditorDocumentOrigin: Equatable {
    /// A brand-new document that has never been written anywhere.
    case none
    case file(URL, EditorFileFormat)
    case character(id: String, revision: EditorPackageRevision)

    /// What a plain Save has to do. Kept here rather than in the window
    /// controller so the three cases are testable without a window.
    enum SaveAction: Equatable {
        case writeFile(URL, EditorFileFormat)
        case writeLibraryPackage(id: String)
        case askForDestination
    }

    var saveAction: SaveAction {
        switch self {
        case .character(let id, _): return .writeLibraryPackage(id: id)
        case .file(let url, let format) where format.canWrite: return .writeFile(url, format)
        case .none, .file: return .askForDestination
        }
    }

    /// False when Save has to ask the user for a destination first — either
    /// because there isn't one yet, or because the origin's format cannot be
    /// written back (a JPEG import).
    var canSaveInPlace: Bool { saveAction != .askForDestination }

    var fileURL: URL? {
        if case .file(let url, _) = self { return url }
        return nil
    }

    var characterID: String? {
        if case .character(let id, _) = self { return id }
        return nil
    }
}
