import Foundation

/// A source of characters. `CharacterManager` and the UI only ever talk to
/// this protocol, so built-in and user-imported characters are
/// indistinguishable to the rest of the app.
protocol CharacterRepository {
    func characters() -> [Character]
}

/// The characters that ship with the app.
struct BuiltInCharacterRepository: CharacterRepository {
    func characters() -> [Character] {
        BuiltInCharacters.all
    }
}

/// Characters the user created in the built-in editor, one package
/// directory each under `CharacterLibrary`.
///
/// A package that fails to load is skipped with a log line rather than
/// propagated: one bad directory costs the user that character, not the
/// whole list. Built-in characters take the same approach
/// (`CharacterPackageLoader.loadBuiltIn` returns `nil` on failure).
struct ImportedCharacterRepository: CharacterRepository {

    private let library: CharacterLibrary

    init(library: CharacterLibrary = .makeDefault()) {
        self.library = library
    }

    func characters() -> [Character] {
        library.packageDirectories().compactMap { directory in
            do {
                return try CharacterPackageLoader.loadImported(packageDirectory: directory)
            } catch {
                NSLog("Unfold: skipping unreadable character package at \(directory.lastPathComponent) — \(error)")
                return nil
            }
        }
    }
}

/// Presents every known repository as one flat list.
///
/// ```
/// CharacterRepository (composite)
/// ├─ Built-in characters
/// └─ Imported characters
/// ```
struct CompositeCharacterRepository: CharacterRepository {
    private let repositories: [CharacterRepository]

    init(repositories: [CharacterRepository] = [
        BuiltInCharacterRepository(),
        ImportedCharacterRepository()
    ]) {
        self.repositories = repositories
    }

    func characters() -> [Character] {
        repositories.flatMap { $0.characters() }
    }
}
