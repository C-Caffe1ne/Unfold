import Foundation
import UniformTypeIdentifiers

/// Every file type the editor can open or write.
///
/// The File menu and the open/save panels are built from this table rather
/// than from hard-coded lists, so adding a format is one case here plus its
/// codec. Nothing in this type knows how to decode anything — it answers
/// only "what is this format called, and may we read or write it".
enum EditorFileFormat: String, CaseIterable, Equatable {
    /// The editor's own document: layers, frames and per-layer opacity all
    /// survive a round trip. This is the app's own format, with its own
    /// `.unf` extension — the bytes underneath are still the Piskel v2 JSON
    /// schema, which is what keeps packages written by earlier versions
    /// (saved as `.piskel`) readable, but that is an implementation detail
    /// invisible to the user.
    case unfoldSource
    /// A horizontal sprite sheet, one row of `frameCount` frames.
    case png
    /// An animated GIF. Write-only for now; import is deferred.
    case gif
    /// Read-only: no alpha channel, and lossy compression destroys pixel edges.
    case jpeg

    /// Extension used when saving. Import also accepts `jpg` — see `matching`.
    var fileExtension: String {
        switch self {
        case .unfoldSource: return "unf"
        case .png: return "png"
        case .gif: return "gif"
        case .jpeg: return "jpeg"
        }
    }

    var displayName: String {
        switch self {
        case .unfoldSource: return "Unfold Document"
        case .png: return "PNG Sprite Sheet"
        case .gif: return "Animated GIF"
        case .jpeg: return "JPEG Image"
        }
    }

    var canRead: Bool {
        switch self {
        case .unfoldSource, .png, .jpeg: return true
        case .gif: return false
        }
    }

    var canWrite: Bool {
        switch self {
        case .unfoldSource, .png, .gif: return true
        case .jpeg: return false
        }
    }

    /// `.unf` is not a registered system type, so this falls back to a
    /// dynamic UTI. That is enough for an open/save panel to filter on the
    /// extension, which is all this is used for.
    var utType: UTType {
        switch self {
        case .unfoldSource: return UTType(filenameExtension: "unf") ?? .data
        case .png: return .png
        case .gif: return .gif
        case .jpeg: return .jpeg
        }
    }

    /// True when writing this format stores everything the document holds,
    /// so the file can be reopened as the same document. PNG composites the
    /// layers into one image and GIF drops partial alpha, so writing either
    /// is an export — the document still lives only in memory.
    var preservesDocument: Bool { self == .unfoldSource }

    static var readable: [EditorFileFormat] { allCases.filter(\.canRead) }
    static var writable: [EditorFileFormat] { allCases.filter(\.canWrite) }

    /// Picks a *starting guess* from the file name. The decoders verify the
    /// actual bytes, so a mislabelled file is caught there, not here.
    static func matching(fileExtension: String) -> EditorFileFormat? {
        let normalised = fileExtension.lowercased()
        if normalised == "jpg" { return .jpeg }
        return allCases.first { $0.fileExtension == normalised }
    }
}
