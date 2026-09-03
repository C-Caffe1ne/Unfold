import Combine
import Foundation

/// Owns the currently selected character and the catalog it was chosen
/// from. Nothing outside this file (and `CharacterRepository`) knows
/// whether a character is built-in or user-imported.
///
/// Timer code never references this type's contents directly — the
/// `StretchCoordinator` is the only consumer, and it only asks "who is the
/// current character" once a stretch event has already happened.
@MainActor
final class CharacterManager: ObservableObject {

    @Published private(set) var current: Character

    /// Published rather than computed: saving a new character has to move
    /// the Settings picker, and a computed property gives SwiftUI nothing
    /// to observe.
    @Published private(set) var availableCharacters: [Character]

    private let repository: CharacterRepository
    private let settings: SettingsStore

    init(repository: CharacterRepository, settings: SettingsStore) {
        self.repository = repository
        self.settings = settings

        let characters = repository.characters()
        self.availableCharacters = characters
        self.current = Self.resolveCurrent(from: characters, settings: settings)
    }

    /// Re-reads every repository and re-resolves the current selection.
    /// Call after the character catalog changes on disk — a save or a
    /// delete in the editor.
    func reloadCatalog() {
        let characters = repository.characters()
        availableCharacters = characters
        if let refreshedCurrent = characters.first(where: { $0.id == current.id }) {
            // Editing preserves the id but replaces the package contents.
            // Keep the selection and refresh the actual Character value so
            // its name, assets, and animation definitions update immediately.
            current = refreshedCurrent
        } else {
            current = Self.resolveCurrent(from: characters, settings: settings)
        }
    }

    /// The saved selection if it still exists, otherwise the first
    /// available character — and the stored id is healed on the way, so a
    /// character whose package was deleted doesn't have to be re-resolved
    /// on every future launch.
    private static func resolveCurrent(from characters: [Character], settings: SettingsStore) -> Character {
        if let savedID = settings.selectedCharacterID,
           let match = characters.first(where: { $0.id == savedID }) {
            return match
        }

        let fallback = characters.first ?? BuiltInCharacters.emergencyFallback
        if settings.selectedCharacterID != nil {
            NSLog("Unfold: selectedCharacterID \"\(settings.selectedCharacterID ?? "")\" not found — falling back to \"\(fallback.id)\"")
            settings.selectedCharacterID = fallback.id
        }
        return fallback
    }

    func select(_ character: Character) {
        current = character
        settings.selectedCharacterID = character.id
        NSLog("Unfold: selected character \"\(character.id)\" — used from the next stretch reminder onward")
    }
}
