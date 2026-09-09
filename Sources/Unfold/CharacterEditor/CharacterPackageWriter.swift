import Foundation

/// Turns a validated `EditorSavePayload` into a character package on disk.
///
/// Writes go to a staging directory inside the library root first, are
/// verified by loading them back through `CharacterPackageLoader` -- the
/// same code path every other character takes -- and only then move into
/// place. Staging inside the library keeps the move on one volume, so the
/// final step is a rename rather than a copy that could half-finish.
///
/// The consequence that matters to the user: a failed save never destroys
/// the character they already had, and never leaves a broken package the
/// repository would have to skip.
enum CharacterPackageWriter {

    enum WriteError: Error, CustomStringConvertible {
        case blankName
        case invalidCharacterID(String)
        case selfValidationFailed(String)

        var description: String {
            switch self {
            case .blankName:
                return "a character needs a name"
            case .invalidCharacterID(let id):
                return "\"\(id)\" is not a usable character id"
            case .selfValidationFailed(let reason):
                return "the saved package failed its own validation -- \(reason)"
            }
        }
    }

    /// Returns the character as loaded back from its final location, so the
    /// caller works with exactly what the rest of the app will see.
    @discardableResult
    static func write(
        payload: EditorSavePayload,
        name: String,
        into library: CharacterLibrary,
        validatePackage: (URL) throws -> Character = CharacterPackageLoader.loadImported(packageDirectory:)
    ) throws -> Character {
        let trimmedName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedName.isEmpty else { throw WriteError.blankName }

        let id = payload.characterID ?? "user-\(UUID().uuidString.lowercased())"
        guard
            CharacterLibrary.isSafeID(id),
            let finalDirectory = library.packageDirectory(id: id)
        else {
            throw WriteError.invalidCharacterID(id)
        }

        try library.createRootIfNeeded()

        let staging = library.rootDirectory
            .appendingPathComponent(".staging-\(UUID().uuidString)", isDirectory: true)
        // Any exit from here on -- thrown or successful -- leaves no staging
        // directory behind.
        defer { try? FileManager.default.removeItem(at: staging) }

        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try payload.sheetPNGData.write(to: staging.appendingPathComponent(Constants.characterSpriteSheetFileName))
        try Data(payload.sourceJSON.utf8).write(to: staging.appendingPathComponent(Constants.characterEditorSourceFileName))
        try makeManifestData(payload: payload, name: trimmedName, id: id)
            .write(to: staging.appendingPathComponent(Constants.characterManifestFileName))

        let stagedCharacter: Character
        do {
            stagedCharacter = try validatePackage(staging)
        } catch {
            throw WriteError.selfValidationFailed(String(describing: error))
        }

        if FileManager.default.fileExists(atPath: finalDirectory.path) {
            // `removeItem` followed by `moveItem` creates a failure window in
            // which the user's previous character is already gone. Foundation's
            // same-volume replacement swaps the staged directory into place as
            // one filesystem operation and consumes `staging` on success.
            _ = try FileManager.default.replaceItemAt(
                finalDirectory,
                withItemAt: staging,
                backupItemName: nil
            )
        } else {
            try FileManager.default.moveItem(at: staging, to: finalDirectory)
        }

        // Nothing after installation may throw: otherwise the caller could
        // receive failure after the library has already changed. The staged
        // package was loaded from the same bytes immediately above; only its
        // package URL changes when the directory is installed.
        return relocated(stagedCharacter, to: finalDirectory)
    }

    private static func relocated(_ character: Character, to packageDirectory: URL) -> Character {
        Character(
            id: character.id,
            name: character.name,
            thumbnailSymbolName: character.thumbnailSymbolName,
            spriteSheet: character.spriteSheet,
            animations: character.animations,
            renderStyle: character.renderStyle,
            source: .imported(packageURL: packageDirectory)
        )
    }

    /// One row, `frameCount` columns -- the sheet's frame order is Piskel's
    /// frame order with no arithmetic in between.
    private static func makeManifestData(payload: EditorSavePayload, name: String, id: String) throws -> Data {
        let manifest = CharacterManifest(
            id: id,
            name: name,
            version: 1,
            spriteSheet: CharacterManifest.SpriteSheetDTO(
                file: Constants.characterSpriteSheetFileName,
                columns: payload.frameCount,
                rows: 1,
                frameWidth: payload.width,
                frameHeight: payload.height
            ),
            animations: [
                AnimationKey.idle.rawValue: CharacterManifest.AnimationDTO(
                    frames: payload.playbackFrames ?? Array(0..<payload.frameCount),
                    fps: payload.fps,
                    gif: nil,
                    loop: payload.loop,
                    frameDurations: payload.frameDurations
                )
            ],
            thumbnailSymbol: Constants.userCharacterThumbnailSymbol,
            renderStyle: RenderStyle.pixel.rawValue
        )

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        return try encoder.encode(manifest)
    }
}
