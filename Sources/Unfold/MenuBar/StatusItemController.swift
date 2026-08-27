import AppKit
import Combine

/// Builds and maintains the menu bar item and its dropdown menu.
///
/// This is the only place that knows about `NSStatusItem` / `NSMenu`. It
/// reads state from `StretchTimer` and `SettingsStore` and forwards user
/// actions back to them — it holds no timer logic of its own.
@MainActor
final class StatusItemController {

    private let statusItem: NSStatusItem
    private let timer: StretchTimer
    private let settings: SettingsStore
    private let onOpenSettings: () -> Void

    private var cancellables = Set<AnyCancellable>()

    // Menu items whose titles/state change at runtime.
    private let nextStretchItem = NSMenuItem()
    private let pauseItem = NSMenuItem()
    private let intervalItem = NSMenuItem()

    init(
        timer: StretchTimer,
        settings: SettingsStore,
        onOpenSettings: @escaping () -> Void
    ) {
        self.timer = timer
        self.settings = settings
        self.onOpenSettings = onOpenSettings
        self.statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)

        configureButton()
        statusItem.menu = buildMenu()
        observeState()
        refresh()
    }

    // MARK: - Building

    private func configureButton() {
        let image = NSImage(
            systemSymbolName: Constants.menuBarSymbolName,
            accessibilityDescription: Strings.appName
        )
        image?.isTemplate = true
        statusItem.button?.image = image
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.autoenablesItems = false

        let header = NSMenuItem(title: Strings.appName, action: nil, keyEquivalent: "")
        header.isEnabled = false
        menu.addItem(header)
        menu.addItem(.separator())

        nextStretchItem.isEnabled = false
        menu.addItem(nextStretchItem)
        menu.addItem(.separator())

        pauseItem.target = self
        pauseItem.action = #selector(togglePause)
        menu.addItem(pauseItem)

        let resetItem = NSMenuItem(
            title: Strings.Menu.resetTimer,
            action: #selector(resetTimer),
            keyEquivalent: ""
        )
        resetItem.target = self
        menu.addItem(resetItem)
        menu.addItem(.separator())

        intervalItem.title = Strings.Menu.stretchInterval
        intervalItem.submenu = buildIntervalSubmenu()
        menu.addItem(intervalItem)
        menu.addItem(.separator())

        let settingsItem = NSMenuItem(
            title: Strings.Menu.settings,
            action: #selector(openSettings),
            keyEquivalent: ","
        )
        settingsItem.target = self
        menu.addItem(settingsItem)

        let quitItem = NSMenuItem(
            title: Strings.Menu.quit,
            action: #selector(quit),
            keyEquivalent: "q"
        )
        quitItem.target = self
        menu.addItem(quitItem)

        return menu
    }

    private func buildIntervalSubmenu() -> NSMenu {
        let submenu = NSMenu()
        submenu.autoenablesItems = false

        for preset in StretchInterval.presets {
            let item = NSMenuItem(
                title: preset.displayLabel,
                action: #selector(selectPreset(_:)),
                keyEquivalent: ""
            )
            item.target = self
            item.tag = preset.minutes
            submenu.addItem(item)
        }

        submenu.addItem(.separator())

        let customItem = NSMenuItem(
            title: Strings.Menu.custom,
            action: #selector(promptCustomInterval),
            keyEquivalent: ""
        )
        customItem.target = self
        submenu.addItem(customItem)

        return submenu
    }

    // MARK: - State

    private func observeState() {
        timer.$timeRemaining
            .combineLatest(timer.$state)
            .receive(on: RunLoop.main)
            .sink { [weak self] _, _ in self?.refresh() }
            .store(in: &cancellables)

        settings.$stretchInterval
            .receive(on: RunLoop.main)
            .sink { [weak self] _ in self?.refresh() }
            .store(in: &cancellables)
    }

    private func refresh() {
        switch timer.state {
        case .running:
            nextStretchItem.title =
                "\(Strings.Menu.nextStretch): \(TimeFormatting.menuLabel(for: timer.timeRemaining))"
            pauseItem.title = Strings.Menu.pause
        case .paused:
            nextStretchItem.title = "\(Strings.Menu.nextStretch): \(Strings.Menu.paused)"
            pauseItem.title = Strings.Menu.resume
        }

        let current = settings.stretchInterval
        intervalItem.title = "\(Strings.Menu.stretchInterval): \(current.displayLabel)"

        if let submenu = intervalItem.submenu {
            for item in submenu.items where item.tag != 0 {
                let isSelected = !current.isCustom && item.tag == current.minutes
                item.state = isSelected ? .on : .off
            }
        }
    }

    // MARK: - Actions

    @objc private func togglePause() {
        timer.togglePause()
    }

    @objc private func resetTimer() {
        timer.reset()
    }

    @objc private func openSettings() {
        onOpenSettings()
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }

    @objc private func selectPreset(_ sender: NSMenuItem) {
        settings.stretchInterval = .preset(minutes: sender.tag)
        timer.reset()
    }

    @objc private func promptCustomInterval() {
        let range = Constants.customIntervalRange

        let alert = NSAlert()
        alert.messageText = Strings.CustomPrompt.title
        alert.informativeText = Strings.CustomPrompt.message(range: range)
        alert.addButton(withTitle: Strings.CustomPrompt.confirm)
        alert.addButton(withTitle: Strings.CustomPrompt.cancel)

        let field = NSTextField(frame: NSRect(x: 0, y: 0, width: 200, height: 24))
        field.stringValue = String(settings.stretchInterval.minutes)
        alert.accessoryView = field

        NSApp.activate(ignoringOtherApps: true)
        guard alert.runModal() == .alertFirstButtonReturn,
              let entered = Int(field.stringValue) else {
            return
        }

        let clamped = min(max(entered, range.lowerBound), range.upperBound)
        settings.stretchInterval = .custom(minutes: clamped)
        timer.reset()
    }
}
