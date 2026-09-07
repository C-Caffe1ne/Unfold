import XCTest
@testable import Unfold

final class EditorPackageRevisionTests: XCTestCase {
    func testDetectsSameSizeSourceChangeAndDeletedPackage() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for name in ["source.piskel", "spritesheet.png", "character.json"] {
            try Data("original".utf8).write(to: root.appendingPathComponent(name))
        }
        let revision = try EditorPackageRevision.read(at: root)
        XCTAssertEqual(revision, try EditorPackageRevision.read(at: root))
        try Data("modified".utf8).write(to: root.appendingPathComponent("source.piskel"))
        XCTAssertNotEqual(revision, try EditorPackageRevision.read(at: root))
        try FileManager.default.removeItem(at: root.appendingPathComponent("character.json"))
        XCTAssertThrowsError(try EditorPackageRevision.read(at: root))
    }

    func testDetectsSpriteSheetAndManifestChanges() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        for name in ["source.piskel", "spritesheet.png", "character.json"] {
            try Data("original".utf8).write(to: root.appendingPathComponent(name))
        }
        let original = try EditorPackageRevision.read(at: root)
        try Data("new sheet".utf8).write(to: root.appendingPathComponent("spritesheet.png"))
        let changedSheet = try EditorPackageRevision.read(at: root)
        XCTAssertNotEqual(original, changedSheet)
        try Data("new manifest".utf8).write(to: root.appendingPathComponent("character.json"))
        XCTAssertNotEqual(changedSheet, try EditorPackageRevision.read(at: root))
    }
}
