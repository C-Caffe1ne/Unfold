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

    /// The exact same traversal as `test_rejects_traversalOutOfTheEditorDirectory`,
    /// spelled with percent-encoded dots. `.standardized` collapses `..` in a
    /// URL's *encoded* string form — `%2e%2e` isn't literal `..` yet at that
    /// point, so it has nothing to collapse. Only `.path` decodes it back
    /// into `..`, and by then the check has already run. This is the classic
    /// path-traversal-filter bypass: encode what the filter looks for, let
    /// something downstream decode it after the filter already said yes.
    func test_rejects_percentEncodedTraversal() {
        let encoded = URL(string: "file:///Apps/Unfold.app/Resources/Editor/%2e%2e/%2e%2e/etc/passwd")!
        XCTAssertFalse(EditorNavigationPolicy.allows(url: encoded, editorDirectory: editorDirectory))
    }

    func test_rejects_singlePercentEncodedTraversalSegment() {
        let encoded = URL(string: "file:///Apps/Unfold.app/Resources/Editor/%2e%2e/passwd")!
        XCTAssertFalse(EditorNavigationPolicy.allows(url: encoded, editorDirectory: editorDirectory))
    }

    /// Mixing a plain `..` with an encoded one — makes sure the fix isn't
    /// accidentally only handling the case where every segment is encoded.
    func test_rejects_mixedPlainAndEncodedTraversal() {
        let mixed = URL(string: "file:///Apps/Unfold.app/Resources/Editor/../%2e%2e/etc/passwd")!
        XCTAssertFalse(EditorNavigationPolicy.allows(url: mixed, editorDirectory: editorDirectory))
    }
}
