import XCTest
@testable import Unfold

final class EditorPackageRevisionTests: XCTestCase {
    private func makeRoot() -> URL {
        FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
    }

    func testDetectsSameSizeSourceChangeAndDeletedPackage() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for name in [Constants.characterEditorSourceFileName, Constants.characterSpriteSheetFileName, Constants.characterManifestFileName] {
            try Data("original".utf8).write(to: root.appendingPathComponent(name))
        }
        let revision = try EditorPackageRevision.read(at: root)
        XCTAssertEqual(revision, try EditorPackageRevision.read(at: root))
        try Data("modified".utf8).write(to: root.appendingPathComponent(Constants.characterEditorSourceFileName))
        XCTAssertNotEqual(revision, try EditorPackageRevision.read(at: root))
        try FileManager.default.removeItem(at: root.appendingPathComponent(Constants.characterManifestFileName))
        XCTAssertThrowsError(try EditorPackageRevision.read(at: root))
    }

    func testDetectsSpriteSheetAndManifestChanges() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for name in [Constants.characterEditorSourceFileName, Constants.characterSpriteSheetFileName, Constants.characterManifestFileName] {
            try Data("original".utf8).write(to: root.appendingPathComponent(name))
        }
        let original = try EditorPackageRevision.read(at: root)
        try Data("new sheet".utf8).write(to: root.appendingPathComponent(Constants.characterSpriteSheetFileName))
        let changedSheet = try EditorPackageRevision.read(at: root)
        XCTAssertNotEqual(original, changedSheet)
        try Data("new manifest".utf8).write(to: root.appendingPathComponent(Constants.characterManifestFileName))
        XCTAssertNotEqual(changedSheet, try EditorPackageRevision.read(at: root))
    }

    // MARK: - Legacy `source.piskel` fallback
    //
    // Packages written by earlier versions of the editor have no
    // `source.unf` -- only the old `source.piskel`. Every read must still
    // find it, and the revision it produces must behave exactly like a
    // current-name package's.

    private func writePackage(sourceFileName: String, root: URL) throws {
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        try Data("source bytes".utf8).write(to: root.appendingPathComponent(sourceFileName))
        try Data("sheet".utf8).write(to: root.appendingPathComponent(Constants.characterSpriteSheetFileName))
        try Data("manifest".utf8).write(to: root.appendingPathComponent(Constants.characterManifestFileName))
    }

    func testPackageWithCurrentSourceFileName_readsSuccessfully() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try writePackage(sourceFileName: Constants.characterEditorSourceFileName, root: root)
        XCTAssertNoThrow(try EditorPackageRevision.read(at: root))
    }

    func testPackageWithOnlyLegacySourceFileName_readsSuccessfully() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try writePackage(sourceFileName: Constants.legacyCharacterEditorSourceFileName, root: root)
        XCTAssertNoThrow(try EditorPackageRevision.read(at: root))
    }

    func testReadingTheSameLegacyPackageTwice_givesAnEqualRevision() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try writePackage(sourceFileName: Constants.legacyCharacterEditorSourceFileName, root: root)
        let first = try EditorPackageRevision.read(at: root)
        let second = try EditorPackageRevision.read(at: root)
        XCTAssertEqual(first, second)
    }

    func testChangingTheLegacySourceFile_changesTheRevision() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try writePackage(sourceFileName: Constants.legacyCharacterEditorSourceFileName, root: root)
        let original = try EditorPackageRevision.read(at: root)
        try Data("changed source bytes".utf8).write(to: root.appendingPathComponent(Constants.legacyCharacterEditorSourceFileName))
        XCTAssertNotEqual(original, try EditorPackageRevision.read(at: root))
    }

    func testPackageWithNeitherSourceFileName_throws() throws {
        let root = makeRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        try Data("sheet".utf8).write(to: root.appendingPathComponent(Constants.characterSpriteSheetFileName))
        try Data("manifest".utf8).write(to: root.appendingPathComponent(Constants.characterManifestFileName))
        XCTAssertThrowsError(try EditorPackageRevision.read(at: root))
    }
}
