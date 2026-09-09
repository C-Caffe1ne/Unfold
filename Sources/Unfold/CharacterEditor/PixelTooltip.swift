import AppKit
import SwiftUI

extension View {
    /// A tooltip the editor draws itself, after `PixelTooltipPanel.delay`.
    ///
    /// The editor draws its own because `.help` hands the string to a tooltip
    /// whose delay it does not own and which, over a plain-styled button,
    /// tracks the drawn glyph rather than the control's frame — so hovering a
    /// tool icon often showed nothing at all.
    ///
    /// `.help` is deliberately not kept alongside it. SwiftUI draws that
    /// tooltip itself, so asking for both put two on screen for one hover:
    /// SwiftUI's below the pointer, and the editor's beside the control. The
    /// text still reaches VoiceOver, as a hint, which draws nothing.
    func pixelTooltip(_ text: String) -> some View {
        accessibilityHint(Text(text)).overlay(PixelTooltipTracker(text: text))
    }
}

private struct PixelTooltipTracker: NSViewRepresentable {
    let text: String
    func makeNSView(context: Context) -> PixelTooltipTrackingView { PixelTooltipTrackingView(text: text) }
    func updateNSView(_ view: PixelTooltipTrackingView, context: Context) { view.text = text }
    static func dismantleNSView(_ view: PixelTooltipTrackingView, coordinator: ()) { view.cancel() }
}

/// Covers one control and waits for the pointer to settle over it.
@MainActor
final class PixelTooltipTrackingView: NSView {
    var text: String {
        didSet { if showing, text != oldValue { present() } }
    }
    private var countdown: Timer?
    private var showing = false

    /// The covered control, in screen coordinates. The tracker is an overlay
    /// sized to that control, so its own bounds are the control's.
    private var controlRect: NSRect? {
        guard let window else { return nil }
        return window.convertToScreen(convert(bounds, to: nil))
    }

    private func present() {
        guard let controlRect else { return }
        showing = true
        PixelTooltipPanel.shared.show(text, near: controlRect)
    }

    init(text: String) {
        self.text = text
        super.init(frame: .zero)
    }
    required init?(coder: NSCoder) { return nil }

    /// The control underneath owns the clicks. A tracking area still reports
    /// entry and exit for a view that hit-tests to nothing.
    override func hitTest(_ point: NSPoint) -> NSView? { nil }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach(removeTrackingArea)
        addTrackingArea(NSTrackingArea(rect: .zero, options: [
            .mouseEnteredAndExited, .activeInActiveApp, .inVisibleRect,
        ], owner: self))
    }

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        if window == nil { cancel() }
    }

    override func mouseEntered(with event: NSEvent) {
        countdown?.invalidate()
        let timer = Timer(timeInterval: PixelTooltipPanel.delay, repeats: false) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self, self.window != nil else { return }
                self.present()
            }
        }
        // A tooltip that never fired while the pointer was moving would need
        // the hand held perfectly still; `.common` also runs during tracking.
        RunLoop.main.add(timer, forMode: .common)
        countdown = timer
    }

    override func mouseExited(with event: NSEvent) { cancel() }

    func cancel() {
        countdown?.invalidate()
        countdown = nil
        if showing {
            showing = false
            PixelTooltipPanel.shared.hide()
        }
    }
}

/// The one tooltip window. Shared, so a second control cannot leave the first
/// one's tooltip stranded on screen.
@MainActor
final class PixelTooltipPanel {
    static let shared = PixelTooltipPanel()
    /// Long enough that crossing the tool rail stays quiet, short enough that
    /// pausing on an icon answers straight away.
    static let delay: TimeInterval = 0.5

    private var panel: NSPanel?
    private var hosting: NSHostingView<PixelTooltipContent>?
    private var dismissals: Any?

    func show(_ text: String, near control: NSRect) {
        let parts = PixelHelp.split(text)
        let content = PixelTooltipContent(heading: parts.heading, detail: parts.detail)
        let panel = panel ?? makePanel()
        hosting?.rootView = content
        guard let hosting else { return }
        hosting.layoutSubtreeIfNeeded()
        let size = hosting.fittingSize
        hosting.frame = NSRect(origin: .zero, size: size)
        panel.setFrame(Self.frame(size: size, near: control,
                                  screens: NSScreen.screens.map(\.visibleFrame)), display: true)
        // Ordering front without making it key leaves the canvas's keyboard
        // focus, and so its shortcuts, exactly where they were.
        panel.orderFront(nil)
        watchForDismissal()
    }

    func hide() {
        panel?.orderOut(nil)
        if let dismissals { NSEvent.removeMonitor(dismissals) }
        dismissals = nil
    }

    /// The gap between the control and the tooltip describing it.
    nonisolated static let gap: CGFloat = 5

    /// Directly under the control, left edges aligned, flipping above it when
    /// the bottom of the screen is in the way.
    ///
    /// The pointer used to be the anchor, and the screen was chosen by asking
    /// which one contained the pointer — with `NSScreen.main` as the fallback.
    /// On a second display that fallback clamped the panel against the *main*
    /// screen's bounds and threw it across the desk, nowhere near the icon
    /// being hovered. The control is an unambiguous anchor and names its own
    /// screen, so neither can happen now.
    nonisolated static func frame(size: NSSize, near control: NSRect, screens: [NSRect]) -> NSRect {
        var origin = NSPoint(x: control.minX, y: control.minY - gap - size.height)
        // A control off every known screen still gets a frame beside itself;
        // clamping to a screen it is not on is what caused the original bug.
        guard let screen = screens.first(where: { $0.intersects(control) }) else {
            return NSRect(origin: origin, size: size)
        }
        if origin.y < screen.minY {
            let above = control.maxY + gap
            if above + size.height <= screen.maxY { origin.y = above }
            else { origin.y = screen.minY }
        }
        origin.y = min(origin.y, screen.maxY - size.height)
        origin.x = min(max(origin.x, screen.minX), max(screen.minX, screen.maxX - size.width))
        return NSRect(origin: origin, size: size)
    }

    private func makePanel() -> NSPanel {
        let panel = NSPanel(contentRect: .zero, styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered, defer: true)
        panel.isFloatingPanel = true
        panel.level = .popUpMenu
        panel.backgroundColor = .clear
        panel.isOpaque = false
        panel.hasShadow = true
        panel.ignoresMouseEvents = true
        panel.hidesOnDeactivate = true
        panel.collectionBehavior = [.transient, .ignoresCycle]
        let hosting = NSHostingView(rootView: PixelTooltipContent(heading: "", detail: ""))
        panel.contentView = hosting
        self.hosting = hosting
        self.panel = panel
        return panel
    }

    /// Anything the user does deliberately puts the tooltip away at once,
    /// rather than leaving it over whatever they just clicked.
    private func watchForDismissal() {
        if let dismissals { NSEvent.removeMonitor(dismissals) }
        dismissals = NSEvent.addLocalMonitorForEvents(matching: [
            .leftMouseDown, .rightMouseDown, .otherMouseDown, .scrollWheel, .keyDown,
        ]) { event in
            MainActor.assumeIsolated { PixelTooltipPanel.shared.hide() }
            return event
        }
    }
}

struct PixelTooltipContent: View {
    let heading: String
    let detail: String

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(heading).font(.caption.weight(.semibold))
            if !detail.isEmpty {
                Text(detail).font(.caption).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
        }
        .frame(maxWidth: 250, alignment: .leading)
        .padding(.horizontal, 9).padding(.vertical, 7)
        .background(RoundedRectangle(cornerRadius: 6).fill(Color(nsColor: .controlBackgroundColor)))
        .overlay(RoundedRectangle(cornerRadius: 6).strokeBorder(Color.secondary.opacity(0.35)))
    }
}
