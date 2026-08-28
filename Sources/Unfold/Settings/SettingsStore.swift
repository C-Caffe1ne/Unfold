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
        static let idleThresholdMinutes = "idleThresholdMinutes"
        static let selectedCharacterID = "selectedCharacterID"
    }

    private let defaults: UserDefaults

    /// The active reminder interval.
    @Published var stretchInterval: StretchInterval {
        didSet {
            defaults.set(stretchInterval.storageValue, forKey: Key.stretchInterval)
        }
    }

    /// How many minutes of no keyboard/mouse input before the countdown
    /// pauses. Read live by `SystemActivityMonitor` — never copied once at
    /// startup — so a change here takes effect on the very next idle check.
    @Published var idleThresholdMinutes: Int {
        didSet {
            defaults.set(idleThresholdMinutes, forKey: Key.idleThresholdMinutes)
        }
    }

    /// `idleThresholdMinutes` as a `TimeInterval`, for callers that want
    /// seconds (e.g. `ActivityMonitoring` providers) without doing the
    /// `* 60` at every call site.
    var idleThresholdDuration: TimeInterval {
        TimeInterval(idleThresholdMinutes * 60)
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

        let storedIdleMinutes = defaults.object(forKey: Key.idleThresholdMinutes) as? Int
        self.idleThresholdMinutes = storedIdleMinutes ?? Constants.defaultIdleThresholdMinutes

        self.selectedCharacterID = defaults.string(forKey: Key.selectedCharacterID)
    }
}
