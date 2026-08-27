import Foundation
import UserNotifications

/// Thin wrapper around `UNUserNotificationCenter` for the single "time to
/// stretch" alert.
///
/// The system notification APIs need a real app bundle with a bundle
/// identifier. When Unfold is run as a bare `swift run` binary there is no
/// bundle, so every call degrades to a log line instead of crashing. Build
/// the `.app` (see `Scripts/make-app-bundle.sh`) to get real notifications.
@MainActor
final class NotificationManager {

    private var isAvailable: Bool {
        Bundle.main.bundleIdentifier != nil
    }

    func requestAuthorization() {
        guard isAvailable else { return }
        UNUserNotificationCenter.current()
            .requestAuthorization(options: [.alert, .sound]) { _, error in
                if let error {
                    NSLog("Unfold: notification authorization failed — \(error.localizedDescription)")
                }
            }
    }

    func notifyStretchDue() {
        guard isAvailable else {
            NSLog("Unfold: stretch due (notifications unavailable in this build)")
            return
        }

        let content = UNMutableNotificationContent()
        content.title = Strings.Notification.title
        content.body = Strings.Notification.body
        content.sound = .default

        let request = UNNotificationRequest(
            identifier: "unfold.stretch.\(UUID().uuidString)",
            content: content,
            trigger: nil
        )

        UNUserNotificationCenter.current().add(request) { error in
            if let error {
                NSLog("Unfold: failed to deliver notification — \(error.localizedDescription)")
            }
        }
    }
}
