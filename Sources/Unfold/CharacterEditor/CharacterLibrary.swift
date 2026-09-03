import Foundation

/// Where user-created character packages live, and the only type that knows
/// that answer. Everything else — the repository, the writer, the editor
/// window — asks this instead of building paths of its own.
///
/// Every id that becomes a path segment goes through `isSafeID` first, so a
/// value that arrived from the web side can never escape the library root.
/// This mirrors the check `CharacterAssetLoader` already applies to file
/// names inside a package.
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
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
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
        guard let entries = try? FileManager.default.contentsOfDirectory(
            at: rootDirectory,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        ) else {
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

    /// One path segment, no traversal, no separators.
    static func isSafeID(_ id: String) -> Bool {
        !id.isEmpty
            && id != "."
            && id != ".."
            && !id.contains("/")
            && !id.hasPrefix(".")
    }
}
