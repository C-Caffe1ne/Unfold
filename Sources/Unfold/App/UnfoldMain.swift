import AppKit

/// Entry point. Kept deliberately tiny: it only wires up the AppKit
/// application object and hands control to the `AppDelegate`.
///
/// `.accessory` activation policy means Unfold lives in the menu bar only —
/// no Dock icon, no main window — which is the right shape for this utility.
@main
enum UnfoldMain {

    @MainActor
    static func main() {
        let application = NSApplication.shared
        let delegate = AppDelegate()
        application.delegate = delegate
        application.setActivationPolicy(.accessory)
        application.run()
    }
}
