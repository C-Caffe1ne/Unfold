import SwiftUI
import UserNotifications

/// V1 settings screen: stretch interval, idle "away" threshold, launch at
/// login, a read-only notifications status line, and a read-only character
/// name. A plain native `Form` — no custom chrome — sized like a small
/// macOS utility settings window, not a full app window.
struct SettingsView: View {

    @ObservedObject var settings: SettingsStore
    @ObservedObject var characterManager: CharacterManager
    let onIntervalChanged: () -> Void

    @State private var customIntervalText: String
    @State private var launchAtLoginEnabled = LaunchAtLogin.isEnabled
    @State private var notificationStatus: UNAuthorizationStatus = .notDetermined

    init(settings: SettingsStore, characterManager: CharacterManager, onIntervalChanged: @escaping () -> Void) {
        self.settings = settings
        self.characterManager = characterManager
        self.onIntervalChanged = onIntervalChanged
        _customIntervalText = State(initialValue: String(settings.stretchInterval.minutes))
    }

    private static let intervalRange = Constants.customIntervalRange
    private static let customTag = -1

    var body: some View {
        Form {
            Section(Strings.Settings.stretchSectionTitle) {
                Picker(Strings.Settings.remindMeEvery, selection: intervalSelectionBinding) {
                    ForEach(Constants.presetIntervalMinutes, id: \.self) { minutes in
                        Text(StretchInterval.preset(minutes: minutes).displayLabel).tag(minutes)
                    }
                    Text(Strings.Menu.custom).tag(Self.customTag)
                }

                if settings.stretchInterval.isCustom {
                    HStack {
                        Text(Strings.Settings.customIntervalLabel)
                        Spacer()
                        TextField("", text: $customIntervalText)
                            .frame(width: 60)
                            .multilineTextAlignment(.trailing)
                            .onChange(of: customIntervalText) { newValue in
                                applyCustomInterval(newValue)
                            }
                            .accessibilityLabel(Strings.Settings.customIntervalLabel)
                        Text("min")
                            .foregroundStyle(.secondary)
                    }
                    Text(Strings.Settings.customIntervalRangeHint(Self.intervalRange))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            Section(Strings.Settings.activitySectionTitle) {
                Picker(Strings.Settings.pauseWhenAway, selection: $settings.idleThresholdMinutes) {
                    ForEach(Constants.idleThresholdPresetMinutes, id: \.self) { minutes in
                        Text(minutes == 1 ? "1 minute" : "\(minutes) minutes").tag(minutes)
                    }
                }
            }

            Section(Strings.Settings.generalSectionTitle) {
                Toggle(Strings.Settings.launchAtLogin, isOn: launchAtLoginBinding)
                if LaunchAtLogin.requiresApproval {
                    Text(Strings.Settings.launchAtLoginNeedsApproval)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                } else if LaunchAtLogin.status == .notFound {
                    Text(Strings.Settings.launchAtLoginNotFound)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }

            Section(Strings.Settings.notificationsSectionTitle) {
                HStack {
                    Text(Strings.Settings.systemNotifications)
                    Spacer()
                    Text(notificationStatusLabel)
                        .foregroundStyle(.secondary)
                }
            }

            Section(Strings.Settings.characterSectionTitle) {
                Picker(Strings.Settings.characterPickerLabel, selection: characterSelectionBinding) {
                    ForEach(characterManager.availableCharacters) { character in
                        Label {
                            Text(character.name)
                        } icon: {
                            CharacterThumbnailView(character: character, size: 20)
                        }
                        .tag(character.id)
                    }
                }
                if isCurrentCharacterDefault {
                    Text(Strings.Settings.defaultCharacterLabel)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                // Future extension point: an "Import Character…" action
                // belongs here once import actually works. A disabled
                // placeholder button would just confuse users in the
                // meantime, so this section intentionally has none yet.
            }
        }
        .formStyle(.grouped)
        .frame(width: 460, height: 420)
        .onAppear {
            launchAtLoginEnabled = LaunchAtLogin.isEnabled
            refreshNotificationStatus()
        }
    }

    // MARK: - Stretch interval

    private var intervalSelectionBinding: Binding<Int> {
        Binding(
            get: {
                settings.stretchInterval.isCustom ? Self.customTag : settings.stretchInterval.minutes
            },
            set: { newValue in
                if newValue == Self.customTag {
                    let seed = settings.stretchInterval.minutes
                    customIntervalText = String(seed)
                    settings.stretchInterval = .custom(minutes: seed)
                } else {
                    settings.stretchInterval = .preset(minutes: newValue)
                }
                onIntervalChanged()
            }
        )
    }

    /// Parses and clamps `text` into `Self.intervalRange`; invalid or
    /// out-of-range input is simply not saved (the last valid value stays
    /// in effect) — never a crash, never a silently-corrupted setting.
    private func applyCustomInterval(_ text: String) {
        guard let minutes = Int(text), Self.intervalRange.contains(minutes) else { return }
        guard settings.stretchInterval != .custom(minutes: minutes) else { return }
        settings.stretchInterval = .custom(minutes: minutes)
        onIntervalChanged()
    }

    // MARK: - Character

    private var characterSelectionBinding: Binding<String> {
        Binding(
            get: { characterManager.current.id },
            set: { newID in
                guard let match = characterManager.availableCharacters.first(where: { $0.id == newID }) else { return }
                characterManager.select(match)
            }
        )
    }

    private var isCurrentCharacterDefault: Bool {
        characterManager.current.id == characterManager.availableCharacters.first?.id
    }

    // MARK: - Launch at login

    private var launchAtLoginBinding: Binding<Bool> {
        Binding(
            get: { launchAtLoginEnabled },
            set: { newValue in
                LaunchAtLogin.setEnabled(newValue)
                // Re-read the real status rather than trusting the toggle
                // click — registration can fail silently (logged, not
                // thrown past `setEnabled`), so this is what keeps the
                // switch from drifting away from what's actually registered.
                launchAtLoginEnabled = LaunchAtLogin.isEnabled
            }
        )
    }

    // MARK: - Notifications

    private var notificationStatusLabel: String {
        switch notificationStatus {
        case .authorized, .provisional, .ephemeral:
            return Strings.Settings.notificationsEnabled
        case .denied:
            return Strings.Settings.notificationsDisabled
        case .notDetermined:
            return Strings.Settings.notificationsNotDetermined
        @unknown default:
            return Strings.Settings.notificationsNotDetermined
        }
    }

    private func refreshNotificationStatus() {
        UNUserNotificationCenter.current().getNotificationSettings { settings in
            DispatchQueue.main.async {
                notificationStatus = settings.authorizationStatus
            }
        }
    }
}
