import CryptoKit
import Foundation

/// Detects deletion or outside edits before an open session overwrites a package.
/// Content hashes also catch modifications that preserve file dates and sizes.
struct EditorPackageRevision: Equatable {
    private let digests: [Data]

    static func read(at directory: URL) throws -> EditorPackageRevision {
        guard let sourceURL = sourceFile(in: directory) else {
            throw PixelDocumentCodec.Failure.invalid("This character's package is missing its source file.")
        }
        let files = [
            (sourceURL, PixelDocumentCodec.maximumSourceBytes),
            (directory.appendingPathComponent(Constants.characterSpriteSheetFileName), Constants.editorMaxSheetDataURLBytes),
            (directory.appendingPathComponent(Constants.characterManifestFileName), 1024 * 1024)
        ]
        let digests = try files.map { url, limit -> Data in
            let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= limit else { throw PixelDocumentCodec.Failure.invalid("The character package is too large to edit.") }
            let bytes = try Data(contentsOf: url, options: .mappedIfSafe)
            guard bytes.count <= limit else { throw PixelDocumentCodec.Failure.invalid("The character package changed while opening.") }
            return Data(SHA256.hash(data: bytes))
        }
        return EditorPackageRevision(digests: digests)
    }

    /// The source file inside `directory`, preferring the current name and
    /// falling back to what older versions wrote. Returns nil when neither
    /// is present.
    static func sourceFile(in directory: URL) -> URL? {
        let current = directory.appendingPathComponent(Constants.characterEditorSourceFileName)
        if FileManager.default.fileExists(atPath: current.path) { return current }
        let legacy = directory.appendingPathComponent(Constants.legacyCharacterEditorSourceFileName)
        if FileManager.default.fileExists(atPath: legacy.path) { return legacy }
        return nil
    }
}
