import XCTest
@testable import Unfold

final class EditorDocumentOriginTests: XCTestCase {

    private let url = URL(fileURLWithPath: "/tmp/character.unf")

    func test_newDocument_cannotSaveInPlace_soSaveMustAskForADestination() {
        XCTAssertFalse(EditorDocumentOrigin.unsaved.canSaveInPlace)
    }

    func test_fileOrigin_canSaveInPlace_whenTheFormatPreservesTheDocument() {
        XCTAssertTrue(EditorDocumentOrigin.file(url, .unfoldSource).canSaveInPlace)
    }

    /// PNG and GIF can be written, but neither can hold what the document
    /// has — PNG composites the layers, GIF drops partial alpha. Writing one
    /// is an export, so Save must ask for a destination rather than treat
    /// the file it came from as the document's home.
    func test_fileOrigin_cannotSaveInPlace_whenTheFormatWouldFlattenTheDocument() {
        XCTAssertFalse(EditorDocumentOrigin.file(url, .png).canSaveInPlace)
        XCTAssertFalse(EditorDocumentOrigin.file(url, .gif).canSaveInPlace)
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

    func test_saveAction_writesTheFile_forADocumentPreservingFileOrigin() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .unfoldSource).saveAction,
                       .writeFile(url, .unfoldSource))
    }

    /// Save must never write a flattened PNG in place and report it as a
    /// save. This is the invariant the controller used to have to remember
    /// at each call site; routing it here is what makes forgetting it
    /// impossible.
    func test_saveAction_asksForADestination_whenTheOriginFormatWouldFlattenTheDocument() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .png).saveAction, .askForDestination)
        XCTAssertEqual(EditorDocumentOrigin.file(url, .gif).saveAction, .askForDestination)
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
