import Foundation

/// Where user-created character packages live, and the only type that knows
/// that answer. Everything else — the repository, the writer, the editor
/// window — asks this instead of building paths of its own.
///
/// Every id that becomes a path segment goes through `isSafeID` first, so a
/// value that arrived from the web side can never escape the library root.
/// This is the path-segment counterpart to the relative-file-name check in
/// `CharacterAssetLoader` — stricter, because a segment has no legitimate
/// reason to contain anything but an identifier.
struct CharacterLibrary {

    enum LibraryError: Error, CustomStringConvertible {
        case invalidID(String)

        var description: String {
            switch self {
            case .invalidID(let id):
                return "\"\(id)\" is not a usable character id"
            }
        }
    }

    let rootDirectory: URL

    init(rootDirectory: URL) {
        self.rootDirectory = rootDirectory
    }

    /// Production location: `Application Support/Unfold/Characters` inside
    /// the app's own sandbox container.
    static func makeDefault() -> CharacterLibrary {
        guard let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first else {
            preconditionFailure("macOS always reports an Application Support directory for the user domain")
        }
        return CharacterLibrary(
            rootDirectory: base
                .appendingPathComponent(Constants.applicationSupportFolderName, isDirectory: true)
                .appendingPathComponent(Constants.userCharactersFolderName, isDirectory: true)
        )
    }

    /// Every package directory, sorted by name so the character list has a
    /// stable order between launches. A missing root is not an error — it
    /// simply means the user hasn't made a character yet.
    func packageDirectories() -> [URL] {
        let entries: [URL]
        do {
            entries = try FileManager.default.contentsOfDirectory(
                at: rootDirectory,
                includingPropertiesForKeys: [.isDirectoryKey],
                options: [.skipsHiddenFiles]
            )
        } catch CocoaError.fileReadNoSuchFile {
            // The user hasn't saved a character yet. Not a problem.
            return []
        } catch {
            NSLog("Unfold: could not read the character library at \(rootDirectory.path) — \(error)")
            return []
        }

        return entries
            .filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true }
            .sorted { $0.lastPathComponent < $1.lastPathComponent }
    }

    /// `nil` when `id` couldn't safely become a path segment.
    func packageDirectory(id: String) -> URL? {
        guard Self.isSafeID(id) else { return nil }
        return rootDirectory.appendingPathComponent(id, isDirectory: true)
    }

    func createRootIfNeeded() throws {
        try FileManager.default.createDirectory(at: rootDirectory, withIntermediateDirectories: true)
    }

    func delete(id: String) throws {
        guard let directory = packageDirectory(id: id) else {
            throw LibraryError.invalidID(id)
        }
        guard FileManager.default.fileExists(atPath: directory.path) else { return }
        try FileManager.default.removeItem(at: directory)
    }

    /// One path segment: ASCII letters, digits, `-`, and `_` only.
    ///
    /// An allowlist rather than a denylist on purpose. Ids reach this from
    /// the editor web view, and excluding known-bad strings kept letting
    /// things through — an embedded NUL, for one, which Foundation
    /// truncates a path component at, collapsing `appendingPathComponent`
    /// back onto the library root itself.
    static func isSafeID(_ id: String) -> Bool {
        guard !id.isEmpty, id.utf8.count <= 128 else { return false }
        return id.allSatisfy { $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "-" || $0 == "_") }
    }
}
