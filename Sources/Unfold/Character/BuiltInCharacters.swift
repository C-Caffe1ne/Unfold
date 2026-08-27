import Foundation

/// Catalog of characters shipped with the app.
///
/// Each entry is a package under `Sources/Unfold/Resources/Characters/<id>/` (a
/// `character.json` manifest plus its sprite sheet), loaded through the
/// same `CharacterPackageLoader` a future imported package would use.
/// Adding Dog, Penguin, Rabbit, Robot, ... later means adding a package
/// directory and its id here — nothing else in the app changes.
enum BuiltInCharacters {

    private static let ids = ["default-cat"]

    static let all: [Character] = ids.compactMap { CharacterPackageLoader.loadBuiltIn(id: $0) }

    /// A character with no sprite sheet at all — just a symbol. Used only
    /// if, for some reason, every bundled character package fails to load,
    /// so the app always has *something* to show rather than crashing.
    static let emergencyFallback = Character(
        id: "fallback",
        name: "Unfold",
        thumbnailSymbolName: "pawprint.fill",
        spriteSheet: SpriteSheetDefinition(fileName: "", columns: 1, rows: 1, frameWidth: 1, frameHeight: 1),
        animations: [:],
        source: .builtIn
    )
}
