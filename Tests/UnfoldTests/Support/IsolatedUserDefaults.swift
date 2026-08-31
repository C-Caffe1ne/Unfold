import Foundation

/// A fresh, isolated `UserDefaults` suite per call, so tests exercising
/// `UserDefaults`-backed types (`DesktopPetPositionStore`, and any
/// `DesktopPetWindowController` test that now restores a saved position on
/// `init`) never read or write the real `UserDefaults.standard` on the
/// machine running them. Call `cleanup()` from the test's `tearDown` to
/// remove the suite's on-disk domain.
enum IsolatedUserDefaults {
    static func make() -> (defaults: UserDefaults, cleanup: () -> Void) {
        let suiteName = "unfold-test-\(UUID().uuidString)"
        let defaults = UserDefaults(suiteName: suiteName)!
        return (defaults, { defaults.removePersistentDomain(forName: suiteName) })
    }
}
