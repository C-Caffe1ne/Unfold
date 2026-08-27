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

    private let repository: CharacterRepository
    private let settings: SettingsStore

    var availableCharacters: [Character] {
        repository.characters()
    }

    init(repository: CharacterRepository, settings: SettingsStore) {
        self.repository = repository
        self.settings = settings

        let characters = repository.characters()
        if let savedID = settings.selectedCharacterID,
           let match = characters.first(where: { $0.id == savedID }) {
            self.current = match
        } else {
            self.current = characters.first ?? BuiltInCharacters.emergencyFallback
        }
    }

    func select(_ character: Character) {
        current = character
        settings.selectedCharacterID = character.id
    }
}
