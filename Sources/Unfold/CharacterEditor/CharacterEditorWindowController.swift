import AppKit
import WebKit

/// The name the bridge posts save messages to
/// (`window.webkit.messageHandlers.unfold`). File-scoped rather than a
/// member so `deinit` can unregister it without a cross-isolation hop.
private let editorMessageHandlerName = "unfold"

/// The character editor window: a `WKWebView` running the bundled Piskel
/// build, with Unfold's bridge script injected on top.
///
/// Piskel itself is untouched — everything Unfold changes about it lives in
/// `unfold-bridge.js` / `.css`, injected here. Nothing loads over the
/// network: the web view is pointed at a file URL and
/// `EditorNavigationPolicy` refuses anything else.
@MainActor
final class CharacterEditorWindowController: NSObject {

    private let library: CharacterLibrary
    private let onCharacterSaved: (Character) -> Void

    private var window: NSWindow?
    private var webView: WKWebView?

    /// The character this editing session is allowed to overwrite; `nil`
    /// for a brand-new character. This — not the `characterID` the web view
    /// echoes back in its save message — is the authoritative overwrite
    /// gate: `handle(messageBody:)` refuses any save whose declared
    /// `characterID` doesn't match this exactly.
    private var editingCharacterID: String?

    init(library: CharacterLibrary, onCharacterSaved: @escaping (Character) -> Void) {
        self.library = library
        self.onCharacterSaved = onCharacterSaved
    }

    deinit {
        // Safety net: if a Task 10 owner releases this controller while the
        // editor is still on screen, close the window and unregister the
        // script handler so Piskel's injected timer loop doesn't run on
        // headless. Such an owner is UI code and releases on the main
        // actor, which is what lets `deinit` reach `@MainActor` state here.
        MainActor.assumeIsolated {
            let openWindow = window
            openWindow?.delegate = nil
            teardown()
            openWindow?.close()
        }
    }

    /// `nil` when this build has no editor resources — checked before a
    /// window is ever shown, so the user never sees an empty web view.
    /// Resolved once: it's a `Bundle` lookup and the answer can't change
    /// over the process's life.
    private static let editorDirectory: URL? = Bundle.module.url(forResource: "Editor", withExtension: nil)

    // MARK: - Opening

    func createNewCharacter() {
        open(existingSource: nil, characterID: nil)
    }

    func edit(character: Character) {
        guard
            let directory = library.packageDirectory(id: character.id),
            let source = try? String(
                contentsOf: directory.appendingPathComponent(Constants.characterEditorSourceFileName),
                encoding: .utf8
            )
        else {
            // A character with no `.piskel` alongside it (hand-made, or from
            // a future import path) can't be reopened — start a fresh
            // document rather than refusing outright.
            open(existingSource: nil, characterID: nil)
            return
        }
        open(existingSource: source, characterID: character.id)
    }

    private func open(existingSource: String?, characterID: String?) {
        guard
            let editorDirectory = Self.editorDirectory,
            let indexURL = Self.indexURL(in: editorDirectory)
        else {
            presentEditorUnavailable()
            return
        }

        // Reopening replaces the session rather than stacking windows: the
        // editor holds one document at a time.
        close()
        editingCharacterID = characterID

        let webView = makeWebView(
            editorDirectory: editorDirectory,
            existingSource: existingSource,
            characterID: characterID
        )
        self.webView = webView

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1100, height: 760),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.title = Strings.Editor.windowTitle
        window.contentView = webView
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        self.window = window

        // Only `piskel/` needs to be readable from the web view: the bridge
        // JS/CSS and the licence files are read natively, never over a file
        // URL. `indexURL` lives inside this narrower root.
        webView.loadFileURL(
            indexURL,
            allowingReadAccessTo: editorDirectory.appendingPathComponent("piskel", isDirectory: true)
        )

        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    private static func indexURL(in editorDirectory: URL) -> URL? {
        let url = editorDirectory
            .appendingPathComponent("piskel", isDirectory: true)
            .appendingPathComponent("index.html")
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }

    /// Closes the editor from code. Whether the close starts here or at the
    /// window's red button, `teardown()` runs exactly once — closing the
    /// window synchronously fires `windowWillClose(_:)`, which calls it.
    func close() {
        if let window {
            window.close()
        } else {
            teardown()
        }
    }

    /// Idempotent: stops Piskel's injected `setTimeout` loop and
    /// unregisters the `unfold` message handler so a hidden web view can
    /// never drive `handle(messageBody:)` after the window is gone. The
    /// `guard` makes a second call (code close + `windowWillClose`, or a
    /// `deinit` after an explicit close) a no-op.
    private func teardown() {
        guard let webView else { return }
        webView.configuration.userContentController.removeAllUserScripts()
        webView.configuration.userContentController.removeScriptMessageHandler(forName: editorMessageHandlerName)
        window?.delegate = nil
        self.webView = nil
        window = nil
        editingCharacterID = nil
    }

    // MARK: - Web view

    private func makeWebView(editorDirectory: URL, existingSource: String?, characterID: String?) -> WKWebView {
        let controller = WKUserContentController()

        // Injected before Piskel runs, so the bridge finds its parameters
        // already in place rather than waiting for a round trip.
        controller.addUserScript(WKUserScript(
            source: Self.initScript(existingSource: existingSource, characterID: characterID),
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        ))

        if let css = Self.resourceText("unfold-bridge", "css", in: editorDirectory) {
            controller.addUserScript(WKUserScript(
                source: Self.styleInjectionScript(css: css),
                injectionTime: .atDocumentEnd,
                forMainFrameOnly: true
            ))
        }
        if let js = Self.resourceText("unfold-bridge", "js", in: editorDirectory) {
            controller.addUserScript(WKUserScript(
                source: js,
                injectionTime: .atDocumentEnd,
                forMainFrameOnly: true
            ))
        }

        // `WKUserContentController` retains its handler, so the proxy holds
        // this controller weakly — otherwise the window could never
        // deallocate.
        controller.add(ScriptMessageProxy(target: self), name: editorMessageHandlerName)

        let configuration = WKWebViewConfiguration()
        configuration.userContentController = controller

        let webView = WKWebView(frame: .zero, configuration: configuration)
        webView.allowsBackForwardNavigationGestures = false
        webView.navigationDelegate = self
        return webView
    }

    private static func resourceText(_ name: String, _ ext: String, in directory: URL) -> String? {
        try? String(contentsOf: directory.appendingPathComponent("\(name).\(ext)"), encoding: .utf8)
    }

    private static func initScript(existingSource: String?, characterID: String?) -> String {
        // JSONSerialization does the escaping, so a `.piskel` document's
        // quotes and backslashes can't break out of the literal.
        var payload: [String: Any] = [
            "canvasSide": Constants.editorDefaultCanvasSide,
            "saveButtonLabel": Strings.Editor.saveButton
        ]
        payload["piskelJSON"] = existingSource
        payload["characterID"] = characterID

        guard
            let data = try? JSONSerialization.data(withJSONObject: payload),
            let json = String(data: data, encoding: .utf8)
        else {
            return "window.__unfoldInit = {};"
        }
        return "window.__unfoldInit = \(json);"
    }

    private static func styleInjectionScript(css: String) -> String {
        // `JSONSerialization` only emits a top-level array or object, so the
        // CSS string rides inside a one-element array that the generated JS
        // immediately unwraps with `[0]`. The point is the escaping: the
        // stylesheet text can't break out of the JS string literal.
        let encoded = (try? JSONSerialization.data(withJSONObject: [css]))
            .flatMap { String(data: $0, encoding: .utf8) } ?? "[\"\"]"
        return """
        (function () {
          var style = document.createElement('style');
          style.textContent = \(encoded)[0];
          document.head.appendChild(style);
        })();
        """
    }

    // MARK: - Saving

    fileprivate func handle(messageBody: Any) {
        guard let json = messageBody as? String else {
            NSLog("Unfold: editor sent a non-string message")
            return
        }

        do {
            let payload = try EditorSavePayload.decode(from: json)

            // The overwrite target comes from `editingCharacterID`, set when
            // the window opened — never from the web view. A "new character"
            // session (`nil`) that posts some existing id, or an "edit"
            // session that posts a different one, is refused before any
            // write: `payload.characterID` round-tripped through untrusted
            // JS and `CharacterPackageWriter` would treat it as the package
            // to replace.
            guard payload.characterID == editingCharacterID else {
                NSLog("Unfold: editor save rejected — declared characterID \(payload.characterID ?? "nil") does not match the editing session \(editingCharacterID ?? "nil")")
                reportSaveFailure("this drawing doesn't belong to the character that was opened")
                return
            }

            guard let name = promptForName() else {
                // Cancelled at the name prompt — re-enable the button and
                // leave the drawing exactly as it was.
                reportSaveFailure("")
                return
            }
            let character = try CharacterPackageWriter.write(payload: payload, name: name, into: library)
            onCharacterSaved(character)
            close()
        } catch {
            reportSaveFailure(String(describing: error))
        }
    }

    private func promptForName() -> String? {
        let alert = NSAlert()
        alert.messageText = Strings.Editor.namePromptTitle
        alert.informativeText = Strings.Editor.namePromptMessage
        alert.addButton(withTitle: Strings.Editor.namePromptConfirm)
        alert.addButton(withTitle: Strings.Editor.namePromptCancel)

        let field = NSTextField(frame: NSRect(x: 0, y: 0, width: 220, height: 24))
        field.placeholderString = Strings.Editor.namePromptPlaceholder
        alert.accessoryView = field

        NSApp.activate(ignoringOtherApps: true)
        guard alert.runModal() == .alertFirstButtonReturn else { return nil }

        let name = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? nil : name
    }

    /// An empty `reason` just re-enables the button (the user cancelled) —
    /// the bridge shows no banner for it; anything else also shows why.
    private func reportSaveFailure(_ reason: String) {
        let message = reason.isEmpty ? "" : Strings.Editor.saveFailed(reason)
        // `JSONSerialization` needs a container, so the string is wrapped in
        // a one-element array and unwrapped again (`[0]`) on the JS side —
        // this is just a safe way to embed an arbitrary string literal.
        let encoded = (try? JSONSerialization.data(withJSONObject: [message]))
            .flatMap { String(data: $0, encoding: .utf8) } ?? "[\"\"]"
        webView?.evaluateJavaScript("window.unfoldBridge.saveFailed(\(encoded)[0]);")
    }

    private func presentEditorUnavailable() {
        let alert = NSAlert()
        alert.messageText = Strings.Editor.unavailableTitle
        alert.informativeText = Strings.Editor.unavailableMessage
        alert.addButton(withTitle: Strings.Editor.unavailableDismiss)
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}

// MARK: - Navigation

extension CharacterEditorWindowController: WKNavigationDelegate {

    func webView(
        _ webView: WKWebView,
        decidePolicyFor navigationAction: WKNavigationAction,
        decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
    ) {
        guard let editorDirectory = Self.editorDirectory else {
            decisionHandler(.cancel)
            return
        }

        let allowed = EditorNavigationPolicy.allows(
            url: navigationAction.request.url,
            editorDirectory: editorDirectory
        )
        if !allowed {
            NSLog("Unfold: blocked editor navigation to \(navigationAction.request.url?.absoluteString ?? "nil")")
        }
        decisionHandler(allowed ? .allow : .cancel)
    }
}

// MARK: - Window lifecycle

extension CharacterEditorWindowController: NSWindowDelegate {

    /// Fires for the red close button as well as for `close()`. `teardown()`
    /// is idempotent, so the two paths can't double-run.
    func windowWillClose(_ notification: Notification) {
        teardown()
    }
}

// MARK: - Message handler

/// Breaks the retain cycle `WKUserContentController` would otherwise create
/// by holding its message handler strongly.
private final class ScriptMessageProxy: NSObject, WKScriptMessageHandler {

    private weak var target: CharacterEditorWindowController?

    init(target: CharacterEditorWindowController) {
        self.target = target
    }

    func userContentController(_ controller: WKUserContentController, didReceive message: WKScriptMessage) {
        Task { @MainActor [weak target] in
            target?.handle(messageBody: message.body)
        }
    }
}
