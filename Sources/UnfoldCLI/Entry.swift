import Unfold

/// Thin entry point for the SwiftPM CLI dev/debug pipeline
/// (`swift build` / `swift run` / `Scripts/make-app-bundle.sh`). Mirrors
/// `AppTarget/Entry.swift`, which is the equivalent entry point for the
/// Xcode app target's App Store production build — both just hand off to
/// `UnfoldMain.main()`.
@main
struct Entry {
    @MainActor
    static func main() {
        UnfoldMain.main()
    }
}
