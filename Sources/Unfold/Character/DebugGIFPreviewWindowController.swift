#if DEBUG
import AppKit
import SwiftUI
import UniformTypeIdentifiers

/// Development-only window for previewing an arbitrary GIF file through the
/// real `AnimatedImageClipLoader` → `AnimationClip` → `SpriteAnimator` →
/// `SpriteAnimationView` path — the same production playback engine
/// production characters use, fed by a source `AnimationClipLoader` (the
/// sprite-sheet path) never touches.
///
/// This entire file is compiled only in DEBUG builds (see the `#if DEBUG`
/// wrapping the whole file). It exists purely so a real GIF can be sanity
/// checked against the Phase 3 decoder + the unmodified playback engine
/// before any production wiring is proposed. It does not read
/// `CharacterManifest`, does not touch `Character`/`CharacterAssetLoader`,
/// and never appears in a Release build's menu or binary.
@MainActor
final class DebugGIFPreviewWindowController: NSObject {

    private var window: NSWindow?

    /// Prompts for a `.gif` file, decodes it via `AnimatedImageClipLoader`,
    /// and shows it playing in a small floating window. Decode failures are
    /// surfaced as an alert with the loader's own `LoadError` description —
    /// nothing is silently swallowed, since the whole point of this window
    /// is to find out whether a real file decodes cleanly.
    func show() {
        let panel = NSOpenPanel()
        panel.title = "Preview GIF Animation (Debug)"
        panel.allowedContentTypes = [.gif]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false

        NSApp.activate(ignoringOtherApps: true)
        guard panel.runModal() == .OK, let url = panel.url else { return }

        do {
            let clip = try AnimatedImageClipLoader.load(url: url)
            presentWindow(for: clip, sourceName: url.lastPathComponent)
        } catch {
            let alert = NSAlert()
            alert.alertStyle = .warning
            alert.messageText = "Failed to load GIF"
            alert.informativeText = "\(url.lastPathComponent): \(error)"
            alert.runModal()
        }
    }

    private func presentWindow(for clip: AnimationClip, sourceName: String) {
        let animator = SpriteAnimator(clip: clip)
        let content = DebugGIFPreviewView(animator: animator, sourceName: sourceName, loop: clip.loop, frameCount: clip.frames.count)
        let hosting = NSHostingController(rootView: content)

        let window = NSWindow(contentViewController: hosting)
        window.title = "GIF Preview (Debug) — \(sourceName)"
        window.styleMask = [.titled, .closable]
        window.setContentSize(NSSize(width: 320, height: 360))
        window.center()
        window.makeKeyAndOrderFront(nil)
        self.window = window
    }
}

private struct DebugGIFPreviewView: View {
    @ObservedObject var animator: SpriteAnimator
    let sourceName: String
    let loop: Bool
    let frameCount: Int

    var body: some View {
        VStack(spacing: 12) {
            SpriteAnimationView(animator: animator, interpolation: .none)
                .frame(width: 256, height: 256)
                .background(Color.gray.opacity(0.15))

            VStack(alignment: .leading, spacing: 4) {
                Text(sourceName).font(.headline)
                Text("frames: \(frameCount)   loop: \(loop ? "true" : "false")")
                Text(animator.isFinished ? "finished (holding last frame)" : "playing")
                    .foregroundStyle(.secondary)
            }
            .font(.caption)
        }
        .padding()
    }
}
#endif
