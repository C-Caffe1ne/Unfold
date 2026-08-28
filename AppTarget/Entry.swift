import Unfold

/// Thin entry point for the Xcode app target (`Unfold.xcodeproj`), used for
/// the Mac App Store production build/archive pipeline. Mirrors
/// `Sources/UnfoldCLI/Entry.swift`, the equivalent entry point for the
/// SwiftPM CLI dev/debug pipeline — both just hand off to
/// `UnfoldMain.main()`. All real app logic lives in the shared `Unfold`
/// library target/module; nothing is duplicated here.
@main
struct Entry {
    @MainActor
    static func main() {
        UnfoldMain.main()
    }
}
