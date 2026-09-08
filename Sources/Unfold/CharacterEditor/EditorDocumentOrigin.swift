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
    case unsaved
    case file(URL, EditorFileFormat)
    /// `revision` is nil when the id is known but the on-disk revision
    /// could not be confirmed after a write (e.g. a read failure right
    /// after saving). A nil revision must make the next save refuse to
    /// overwrite rather than silently duplicate the package.
    case character(id: String, revision: EditorPackageRevision?)

    /// What a plain Save has to do. Kept here rather than in the window
    /// controller so the three cases are testable without a window.
    enum SaveAction: Equatable {
        case writeFile(URL, EditorFileFormat)
        case writeLibraryPackage(id: String)
        case askForDestination
    }

    /// The guard is `preservesDocument`, not `canWrite`. A format we can
    /// write but that cannot hold everything the document has — PNG
    /// composites the layers, GIF drops partial alpha — is an export
    /// destination, never a save destination. Routing it here rather than
    /// leaving the distinction to the caller is what stops Save from
    /// quietly reporting a flattened file as the document's home.
    var saveAction: SaveAction {
        switch self {
        case .character(let id, _): return .writeLibraryPackage(id: id)
        case .file(let url, let format) where format.preservesDocument: return .writeFile(url, format)
        case .unsaved, .file: return .askForDestination
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
