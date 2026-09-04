import XCTest
@testable import Unfold

/// The editor may only ever navigate within its own bundled resources.
/// This is what makes "the app never downloads or runs remote code" a
/// property of the code rather than a promise — App Store guideline 2.5.2
/// turns on exactly this.
final class EditorNavigationPolicyTests: XCTestCase {

    private let editorDirectory = URL(fileURLWithPath: "/Apps/Unfold.app/Resources/Editor", isDirectory: true)

    func test_allows_fileInsideEditorDirectory() {
        let url = editorDirectory.appendingPathComponent("piskel/index.html")
        XCTAssertTrue(EditorNavigationPolicy.allows(url: url, editorDirectory: editorDirectory))
    }

    func test_allows_theEditorDirectoryItself() {
        XCTAssertTrue(EditorNavigationPolicy.allows(url: editorDirectory, editorDirectory: editorDirectory))
    }

    func test_rejects_httpsURL() {
        XCTAssertFalse(EditorNavigationPolicy.allows(
            url: URL(string: "https://piskelapp.com")!, editorDirectory: editorDirectory
        ))
    }

    func test_rejects_fileOutsideEditorDirectory() {
        XCTAssertFalse(EditorNavigationPolicy.allows(
            url: URL(fileURLWithPath: "/etc/passwd"), editorDirectory: editorDirectory
        ))
    }

    /// `/Apps/.../EditorEvil` shares a string prefix with the editor
    /// directory but is a different directory — a plain `hasPrefix` on the
    /// path would wrongly allow it.
    func test_rejects_siblingDirectoryWithSharedPrefix() {
        let sibling = URL(fileURLWithPath: "/Apps/Unfold.app/Resources/EditorEvil/x.html")
        XCTAssertFalse(EditorNavigationPolicy.allows(url: sibling, editorDirectory: editorDirectory))
    }

    func test_rejects_traversalOutOfTheEditorDirectory() {
        let escape = editorDirectory.appendingPathComponent("../../../../etc/passwd")
        XCTAssertFalse(EditorNavigationPolicy.allows(url: escape, editorDirectory: editorDirectory))
    }

    func test_rejects_nilURL() {
        XCTAssertFalse(EditorNavigationPolicy.allows(url: nil, editorDirectory: editorDirectory))
    }
}
