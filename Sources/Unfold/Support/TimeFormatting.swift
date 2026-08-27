import Foundation

/// Formats a remaining-time interval for display in the menu.
enum TimeFormatting {

    /// Compact label, e.g. `"42 min"`, `"1 min"`, `"< 1 min"`.
    static func menuLabel(for remaining: TimeInterval) -> String {
        let seconds = max(0, remaining)
        if seconds < 60 {
            return "< 1 min"
        }
        let minutes = Int((seconds / 60).rounded(.up))
        return "\(minutes) min"
    }
}
