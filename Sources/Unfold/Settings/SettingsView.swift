import SwiftUI

/// Placeholder settings screen. Intentionally minimal for step 1 — it shows
/// the current interval and reserves space for real controls later.
struct SettingsView: View {

    @ObservedObject var settings: SettingsStore

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(Strings.Settings.windowTitle)
                .font(.headline)

            HStack {
                Text(Strings.Settings.intervalSectionTitle)
                Spacer()
                Text(settings.stretchInterval.displayLabel)
                    .foregroundStyle(.secondary)
            }

            Divider()

            Text(Strings.Settings.placeholder)
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)

            Spacer()
        }
        .padding(20)
        .frame(width: 360, height: 200, alignment: .topLeading)
    }
}
