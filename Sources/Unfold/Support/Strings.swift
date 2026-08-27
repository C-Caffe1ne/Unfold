import Foundation

/// All user-facing copy in one place. Ready to be swapped for a real
/// localisation table later without touching the views.
enum Strings {

    static let appName = "Unfold"

    enum Menu {
        static let nextStretch = "Next stretch"
        static let pause = "Pause"
        static let resume = "Resume"
        static let paused = "Paused"
        static let resetTimer = "Reset Timer"
        static let stretchInterval = "Stretch Interval"
        static let custom = "Custom…"
        static let settings = "Settings…"
        static let quit = "Quit Unfold"
    }

    enum Notification {
        static let title = "Time to stretch"
        static let body = "You've been using your Mac for a while. Take a moment to stretch."
    }

    enum Settings {
        static let windowTitle = "Unfold Settings"
        static let intervalSectionTitle = "Stretch interval"
        static let placeholder = "More settings — including launch at login — will live here in a future update."
    }

    enum CustomPrompt {
        static let title = "Custom interval"
        static func message(range: ClosedRange<Int>) -> String {
            "Minutes between stretch reminders (\(range.lowerBound)–\(range.upperBound))."
        }
        static let confirm = "Set"
        static let cancel = "Cancel"
    }

    enum Overlay {
        static let title = "Time to stretch"
        static let body = "Take a moment to stretch."
        static let dismiss = "Dismiss"
    }
}
