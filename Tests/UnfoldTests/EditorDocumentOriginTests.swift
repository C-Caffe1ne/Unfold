import XCTest
@testable import Unfold

final class EditorDocumentOriginTests: XCTestCase {

    private let url = URL(fileURLWithPath: "/tmp/character.piskel")

    func test_newDocument_cannotSaveInPlace_soSaveMustAskForADestination() {
        XCTAssertFalse(EditorDocumentOrigin.unsaved.canSaveInPlace)
    }

    func test_fileOrigin_canSaveInPlace_whenTheFormatIsWritable() {
        XCTAssertTrue(EditorDocumentOrigin.file(url, .unfoldSource).canSaveInPlace)
        XCTAssertTrue(EditorDocumentOrigin.file(url, .png).canSaveInPlace)
    }

    /// A document opened from a JPEG has nowhere to save back to: writing
    /// JPEG is not supported, so Save has to fall through to Save As.
    func test_fileOrigin_cannotSaveInPlace_whenTheFormatIsReadOnly() {
        XCTAssertFalse(EditorDocumentOrigin.file(url, .jpeg).canSaveInPlace)
    }

    func test_characterOrigin_canSaveInPlace() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertTrue(EditorDocumentOrigin.character(id: "user-1", revision: revision).canSaveInPlace)
    }

    func test_fileURL_isOnlySetForFileOrigins() throws {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .png).fileURL, url)
        XCTAssertNil(EditorDocumentOrigin.unsaved.fileURL)
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertNil(EditorDocumentOrigin.character(id: "user-1", revision: revision).fileURL)
    }

    func test_characterID_isOnlySetForCharacterOrigins() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertEqual(EditorDocumentOrigin.character(id: "user-1", revision: revision).characterID, "user-1")
        XCTAssertNil(EditorDocumentOrigin.file(url, .png).characterID)
        XCTAssertNil(EditorDocumentOrigin.unsaved.characterID)
    }

    // MARK: what a plain Save does — the three origins

    func test_saveAction_writesTheFile_forAWritableFileOrigin() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .png).saveAction, .writeFile(url, .png))
    }

    func test_saveAction_writesTheLibraryPackage_forACharacterOrigin() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertEqual(EditorDocumentOrigin.character(id: "user-1", revision: revision).saveAction,
                       .writeLibraryPackage(id: "user-1"))
    }

    func test_saveAction_asksForADestination_forANewDocument() {
        XCTAssertEqual(EditorDocumentOrigin.unsaved.saveAction, .askForDestination)
    }

    /// A JPEG import has a file, but not one that can be written back.
    func test_saveAction_asksForADestination_whenTheOriginFormatIsReadOnly() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .jpeg).saveAction, .askForDestination)
    }

    /// The id is enough to route Save to the library package even when the
    /// revision could not be confirmed after a write. Routing still says
    /// "write the package" — it is `saveToLibrary`'s job to refuse the
    /// overwrite when the revision is unknown, not the router's.
    func test_saveAction_writesTheLibraryPackage_evenWhenTheRevisionIsUnknown() {
        XCTAssertEqual(EditorDocumentOrigin.character(id: "user-1", revision: nil).saveAction,
                       .writeLibraryPackage(id: "user-1"))
    }

    /// `EditorPackageRevision.read` needs all three package files present.
    private func makePackage() throws -> URL {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("origin-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        for name in [Constants.characterEditorSourceFileName,
                     Constants.characterSpriteSheetFileName,
                     Constants.characterManifestFileName] {
            try Data("x".utf8).write(to: directory.appendingPathComponent(name))
        }
        return directory
    }
}
