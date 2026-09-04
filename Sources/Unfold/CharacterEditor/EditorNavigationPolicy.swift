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

        // `url.standardized` collapses `..` in the URL's *encoded* string form,
        // where a percent-encoded segment like "%2e%2e" is just an opaque path
        // component — there's nothing for it to collapse. `.path` percent-decodes
        // that back into a literal ".." *after* `.standardized` already ran, so
        // an encoded traversal (`%2e%2e/%2e%2e/etc/passwd`) would sail through a
        // check built that way even though the identical plain-text traversal
        // gets rejected. Rebuilding a fresh file URL from the decoded path and
        // standardizing *that* is what actually collapses it. This is verified
        // against the plain-text and percent-encoded traversal cases exercised
        // by the tests; it is not a claim that every conceivable path-spelling
        // trick is covered.
        let candidate = URL(fileURLWithPath: url.path).standardizedFileURL.path
        let root = editorDirectory.standardized.path

        if candidate == root { return true }
        // The separator is what stops `/…/EditorEvil` from matching
        // `/…/Editor`.
        return candidate.hasPrefix(root.hasSuffix("/") ? root : root + "/")
    }
}
