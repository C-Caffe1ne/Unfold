import XCTest
@testable import Unfold

/// `CharacterLibrary` against a temp root — never the real Application
/// Support directory, so tests can't disturb (or depend on) a developer's
/// own saved characters.
final class CharacterLibraryTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-library-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func makePackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try Data("{}".utf8).write(to: dir.appendingPathComponent("character.json"))
    }

    func test_packageDirectories_missingRoot_isEmptyNotAnError() {
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_packageDirectories_listsOnlyDirectories_sortedByName() throws {
        try makePackage(id: "user-b")
        try makePackage(id: "user-a")
        try Data("stray".utf8).write(to: root.appendingPathComponent("loose-file.txt"))

        XCTAssertEqual(library.packageDirectories().map(\.lastPathComponent), ["user-a", "user-b"])
    }

    func test_packageDirectory_rejectsUnsafeIDs() {
        XCTAssertNil(library.packageDirectory(id: ""))
        XCTAssertNil(library.packageDirectory(id: ".."))
        XCTAssertNil(library.packageDirectory(id: "."))
        XCTAssertNil(library.packageDirectory(id: "a/b"))
        XCTAssertNil(library.packageDirectory(id: "/absolute"))
    }

    func test_packageDirectory_acceptsNormalID() {
        let url = library.packageDirectory(id: "user-123")
        XCTAssertEqual(url?.lastPathComponent, "user-123")
    }

    func test_delete_removesPackage() throws {
        try makePackage(id: "user-a")
        try library.delete(id: "user-a")
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_delete_unsafeID_throwsAndRemovesNothing() throws {
        try makePackage(id: "user-a")
        XCTAssertThrowsError(try library.delete(id: ".."))
        XCTAssertEqual(library.packageDirectories().count, 1)
    }

    func test_makeDefault_pointsInsideApplicationSupport() {
        let url = CharacterLibrary.makeDefault().rootDirectory
        XCTAssertEqual(url.lastPathComponent, "Characters")
        XCTAssertEqual(url.deletingLastPathComponent().lastPathComponent, "Unfold")
        XCTAssertTrue(url.path.contains("Application Support"))
    }
}
