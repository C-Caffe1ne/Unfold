import AppKit

/// Entry point. Kept deliberately tiny: it only wires up the AppKit
/// application object and hands control to the `AppDelegate`.
///
/// `.accessory` activation policy means Unfold lives in the menu bar only —
/// no Dock icon, no main window — which is the right shape for this utility.
///
/// Not `@main` itself: this type is shared by two thin entry points — the
/// SwiftPM CLI executable (`Sources/UnfoldCLI/main.swift`, local dev/debug)
/// and the Xcode app target (`AppTarget/main.swift`, App Store production
/// build) — each of which just calls `UnfoldMain.main()` directly.
public enum UnfoldMain {

    @MainActor
    public static func main() {
        let application = NSApplication.shared
        let delegate = AppDelegate()
        application.delegate = delegate
        application.setActivationPolicy(.accessory)
        application.run()
    }
}
