import Foundation

/// Decides whether the editor web view may navigate to a URL.
///
/// The answer is only ever yes for a `file://` URL inside the app's own
/// bundled editor directory. Piskel's UI contains links to the outside
/// world; following one inside the web view would put a live web page —
/// remote code — inside the app. It also means a saved `.piskel` document
/// can never talk the editor into loading something else.
///
/// A free function on purpose: `WKNavigationDelegate` is awkward to test,
/// this isn't.
enum EditorNavigationPolicy {

    static func allows(url: URL?, editorDirectory: URL) -> Bool {
        guard let url, url.isFileURL else { return false }

        // `standardized` resolves `..` before comparison, so a traversal
        // can't sneak past by spelling its way out and back.
        let candidate = url.standardized.path
        let root = editorDirectory.standardized.path

        if candidate == root { return true }
        // The separator is what stops `/…/EditorEvil` from matching
        // `/…/Editor`.
        return candidate.hasPrefix(root.hasSuffix("/") ? root : root + "/")
    }
}
