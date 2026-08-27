import Foundation

/// A user-selectable gap between stretch reminders.
enum StretchInterval: Equatable {
    case preset(minutes: Int)
    case custom(minutes: Int)

    var minutes: Int {
        switch self {
        case .preset(let minutes), .custom(let minutes):
            return minutes
        }
    }

    var duration: TimeInterval {
        TimeInterval(minutes) * 60
    }

    var isCustom: Bool {
        if case .custom = self { return true }
        return false
    }

    /// Short label for menus and settings, e.g. `"60 min"`.
    var displayLabel: String {
        "\(minutes) min"
    }
}

extension StretchInterval {

    static let presets: [StretchInterval] =
        Constants.presetIntervalMinutes.map { .preset(minutes: $0) }

    static let `default`: StretchInterval =
        .preset(minutes: Constants.defaultStretchIntervalMinutes)
}

// MARK: - Persistence

extension StretchInterval {

    /// Stable string form for UserDefaults, e.g. `"preset:60"` / `"custom:25"`.
    var storageValue: String {
        switch self {
        case .preset(let minutes): return "preset:\(minutes)"
        case .custom(let minutes): return "custom:\(minutes)"
        }
    }

    init?(storageValue: String) {
        let parts = storageValue.split(separator: ":")
        guard parts.count == 2, let minutes = Int(parts[1]) else { return nil }
        switch parts[0] {
        case "preset": self = .preset(minutes: minutes)
        case "custom": self = .custom(minutes: minutes)
        default: return nil
        }
    }
}
