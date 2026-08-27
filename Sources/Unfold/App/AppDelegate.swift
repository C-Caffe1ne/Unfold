import AppKit

/// Composition root. Builds the object graph once at launch and keeps
/// strong references to the long-lived pieces. No business logic lives here.
@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {

    private var settings: SettingsStore?
    private var timer: StretchTimer?
    private var notifications: NotificationManager?
    private var characterManager: CharacterManager?
    private var overlay: OverlayController?
    private var coordinator: StretchCoordinator?
    private var statusItemController: StatusItemController?
    private var settingsWindow: SettingsWindowController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        let settings = SettingsStore()
        let notifications = NotificationManager()
        let settingsWindow = SettingsWindowController(settings: settings)

        let characterManager = CharacterManager(
            repository: CompositeCharacterRepository(),
            settings: settings
        )
        let overlay = OverlayController()
        let coordinator = StretchCoordinator(
            notifications: notifications,
            overlay: overlay,
            characterManager: characterManager
        )

        let timer = StretchTimer(
            intervalProvider: { settings.stretchInterval.duration }
        )
        // Timer only ever hands out a StretchEvent — the coordinator decides
        // what that means (notification + character overlay).
        timer.onStretchDue = { [weak coordinator] event in
            coordinator?.handle(event)
        }

        let statusItemController = StatusItemController(
            timer: timer,
            settings: settings,
            onOpenSettings: { [weak settingsWindow] in settingsWindow?.show() }
        )

        self.settings = settings
        self.notifications = notifications
        self.characterManager = characterManager
        self.overlay = overlay
        self.coordinator = coordinator
        self.settingsWindow = settingsWindow
        self.timer = timer
        self.statusItemController = statusItemController

        notifications.requestAuthorization()
        timer.start()
    }
}
