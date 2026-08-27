import Foundation

/// Resolves a character's on-disk files (sprite sheet, and later thumbnail)
/// to a concrete `URL`, and loads them.
///
/// This is the one place that turns a package-relative file name from a
/// manifest into an actual path — and it only ever looks *inside* that
/// character's own package (a bundle subdirectory for built-in characters,
/// the package directory for imported ones). A manifest can't point outside
/// its own package: absolute paths and `..` segments are rejected before
/// any file access happens.
enum CharacterAssetLoader {

    static func loadSpriteSheetImage(for character: Character) -> SpriteSheetImage? {
        guard let url = resolveFileURL(character.spriteSheet.fileName, characterID: character.id, source: character.source) else {
            return nil
        }
        return SpriteSheetImage(definition: character.spriteSheet, fileURL: url)
    }

    /// Resolves `fileName` against the package `source` refers to.
    static func resolveFileURL(_ fileName: String, characterID: String, source: CharacterSource) -> URL? {
        guard isSafeRelativeFileName(fileName) else {
            NSLog("Unfold: rejected unsafe character asset path \"\(fileName)\"")
            return nil
        }

        switch source {
        case .builtIn:
            return Bundle.module.url(
                forResource: (fileName as NSString).deletingPathExtension,
                withExtension: (fileName as NSString).pathExtension,
                subdirectory: "\(Constants.builtInCharactersResourceSubdirectory)/\(characterID)"
            )
        case .imported(let packageURL):
            return packageURL.appendingPathComponent(fileName)
        }
    }

    /// Rejects absolute paths and parent-directory traversal so a manifest
    /// can only ever reference files inside its own package.
    private static func isSafeRelativeFileName(_ fileName: String) -> Bool {
        !fileName.isEmpty
            && !fileName.hasPrefix("/")
            && !fileName.contains("../")
            && fileName != ".."
    }
}
