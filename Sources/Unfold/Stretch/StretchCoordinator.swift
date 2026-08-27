import Foundation

/// Reacts to a `StretchEvent` by fanning it out to the system notification
/// and the character overlay.
///
/// This is the seam described by the product architecture:
///
///   Timer → Stretch Event → (here) → Character Manager → Character
///                                  → Overlay
///
/// `StretchTimer` never imports this type, and this type never reaches back
/// into timer internals — it only receives events. `AppDelegate` wires the
/// two together but contains no logic itself.
@MainActor
final class StretchCoordinator {

    private let notifications: NotificationManager
    private let overlay: OverlayController
    private let characterManager: CharacterManager

    init(
        notifications: NotificationManager,
        overlay: OverlayController,
        characterManager: CharacterManager
    ) {
        self.notifications = notifications
        self.overlay = overlay
        self.characterManager = characterManager
    }

    func handle(_ event: StretchEvent) {
        notifications.notifyStretchDue()
        overlay.presentStretchReminder(for: characterManager.current)
    }
}
