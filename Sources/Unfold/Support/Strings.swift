import Foundation

/// All user-facing copy in one place. Ready to be swapped for a real
/// localisation table later without touching the views.
enum Strings {

    static let appName = "Unfold"

    enum Menu {
        static let nextStretch = "Next stretch"
        static let pausedWhileAway = "Paused while you're away"
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

        static let stretchSectionTitle = "Stretch Reminder"
        static let remindMeEvery = "Remind me every"
        static let customIntervalLabel = "Custom interval"
        static func customIntervalRangeHint(_ range: ClosedRange<Int>) -> String {
            "\(range.lowerBound)–\(range.upperBound) minutes"
        }

        static let activitySectionTitle = "Activity"
        static let pauseWhenAway = "Pause when I'm away for"

        static let generalSectionTitle = "General"
        static let launchAtLogin = "Launch Unfold at login"
        static let launchAtLoginNeedsApproval = "Approval may be required in System Settings."
        static let launchAtLoginNotFound = "Launch at Login isn't available for this build."

        static let notificationsSectionTitle = "Notifications"
        static let systemNotifications = "System notifications"
        static let notificationsEnabled = "Enabled"
        static let notificationsDisabled = "Disabled in System Settings"
        static let notificationsNotDetermined = "Not requested yet"

        static let characterSectionTitle = "Character"
        static let characterPickerLabel = "Character"
        static let defaultCharacterLabel = "Default"
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
