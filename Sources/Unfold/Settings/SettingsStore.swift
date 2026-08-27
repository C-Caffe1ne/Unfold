import Combine
import Foundation

/// Persistent user settings, backed by `UserDefaults`.
///
/// Every stored property writes through on change, so settings survive a
/// quit-and-relaunch with no explicit save step.
@MainActor
final class SettingsStore: ObservableObject {

    private enum Key {
        static let stretchInterval = "stretchInterval"
        static let launchAtLogin = "launchAtLogin"
        static let selectedCharacterID = "selectedCharacterID"
    }

    private let defaults: UserDefaults

    /// The active reminder interval.
    @Published var stretchInterval: StretchInterval {
        didSet {
            defaults.set(stretchInterval.storageValue, forKey: Key.stretchInterval)
        }
    }

    /// Reserved for a future "launch at login" toggle. Persisted now so the
    /// UI can bind to it, but not yet registered with the system.
    @Published var launchAtLogin: Bool {
        didSet {
            defaults.set(launchAtLogin, forKey: Key.launchAtLogin)
        }
    }

    /// `id` of the character shown in the stretch overlay. `nil` means "use
    /// the repository's default" (currently the first built-in character).
    @Published var selectedCharacterID: String? {
        didSet {
            defaults.set(selectedCharacterID, forKey: Key.selectedCharacterID)
        }
    }

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults

        if let raw = defaults.string(forKey: Key.stretchInterval),
           let stored = StretchInterval(storageValue: raw) {
            self.stretchInterval = stored
        } else {
            self.stretchInterval = .default
        }

        self.launchAtLogin = defaults.bool(forKey: Key.launchAtLogin)
        self.selectedCharacterID = defaults.string(forKey: Key.selectedCharacterID)
    }
}
