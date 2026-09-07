import CryptoKit
import Foundation

/// Detects deletion or outside edits before an open session overwrites a package.
/// Content hashes also catch modifications that preserve file dates and sizes.
struct EditorPackageRevision: Equatable {
    private let digests: [Data]

    static func read(at directory: URL) throws -> EditorPackageRevision {
        let files = [
            (Constants.characterEditorSourceFileName, PixelDocumentCodec.maximumSourceBytes),
            (Constants.characterSpriteSheetFileName, Constants.editorMaxSheetDataURLBytes),
            (Constants.characterManifestFileName, 1024 * 1024)
        ]
        let digests = try files.map { name, limit -> Data in
            let url = directory.appendingPathComponent(name)
            let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= limit else { throw PixelDocumentCodec.Failure.invalid("The character package is too large to edit.") }
            let bytes = try Data(contentsOf: url, options: .mappedIfSafe)
            guard bytes.count <= limit else { throw PixelDocumentCodec.Failure.invalid("The character package changed while opening.") }
            return Data(SHA256.hash(data: bytes))
        }
        return EditorPackageRevision(digests: digests)
    }
}
