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

/// Characters the user has added themselves (`.unfoldcharacter` packages).
///
/// This is a placeholder: it returns an empty list today. When character
/// import ships, this type gains the logic to scan a characters directory
/// for `.unfoldcharacter` packages and call
/// `CharacterPackageLoader.loadImported(packageDirectory:)` for each —
/// without any change to `CharacterManager` or the UI that reads from it.
struct ImportedCharacterRepository: CharacterRepository {
    func characters() -> [Character] {
        []
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
