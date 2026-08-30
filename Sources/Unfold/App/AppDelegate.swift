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
    #if DEBUG
    private var debugGIFPreview: DebugGIFPreviewWindowController?
    #endif

    func applicationDidFinishLaunching(_ notification: Notification) {
        let settings = SettingsStore()
        let notifications = NotificationManager()

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

        // DEBUG builds wrap the real system idle reading so it can be
        // overridden instantly from the "Simulate Idle"/"Simulate Active"
        // menu; a release build talks to the plain system monitor with no
        // override surface at all. Either way, the threshold itself is read
        // fresh from Settings on every check, never copied once at startup.
        #if DEBUG
        let debugActivityMonitor = DebugOverridableActivityMonitor(
            wrapping: SystemActivityMonitor(idleThresholdProvider: { settings.idleThresholdDuration })
        )
        let activityMonitor: ActivityMonitoring = debugActivityMonitor
        #else
        let activityMonitor: ActivityMonitoring = SystemActivityMonitor(
            idleThresholdProvider: { settings.idleThresholdDuration }
        )
        #endif

        let timer = StretchTimer(
            intervalProvider: { settings.stretchInterval.duration },
            activityMonitor: activityMonitor
        )
        // Timer only ever hands out a StretchEvent — the coordinator decides
        // what that means (notification + character overlay).
        timer.onStretchDue = { [weak coordinator] event in
            coordinator?.handle(event)
        }

        let settingsWindow = SettingsWindowController(
            settings: settings,
            timer: timer,
            characterManager: characterManager
        )

        #if DEBUG
        let debugGIFPreview = DebugGIFPreviewWindowController()
        #endif

        let statusItemController = StatusItemController(
            timer: timer,
            settings: settings,
            onOpenSettings: { [weak settingsWindow] in settingsWindow?.show() },
            onDebugTriggerStretch: { [weak coordinator] in
                // Bypasses StretchTimer entirely so the timer's own countdown
                // and cycle logic are untouched by debug testing — exactly
                // the same StretchEvent -> StretchCoordinator path a real
                // timer expiry takes. Only reachable via the #if DEBUG menu
                // item in StatusItemController — never wired to any UI in a
                // release build.
                coordinator?.handle(StretchEvent(occurredAt: Date()))
            },
            onDebugSimulateIdle: {
                #if DEBUG
                debugActivityMonitor.forcedIdle = true
                #endif
            },
            onDebugSimulateActive: {
                #if DEBUG
                debugActivityMonitor.forcedIdle = false
                #endif
            },
            onDebugPreviewGIF: {
                // Only reachable via the #if DEBUG menu item in
                // StatusItemController — the closure itself is passed
                // unconditionally to keep StatusItemController's init
                // signature independent of build configuration, same as
                // the other onDebug* closures above.
                #if DEBUG
                debugGIFPreview.show()
                #endif
            }
        )

        self.settings = settings
        self.notifications = notifications
        self.characterManager = characterManager
        self.overlay = overlay
        self.coordinator = coordinator
        self.settingsWindow = settingsWindow
        self.timer = timer
        self.statusItemController = statusItemController
        #if DEBUG
        self.debugGIFPreview = debugGIFPreview
        #endif

        notifications.requestAuthorization()
        timer.start()
    }
}
