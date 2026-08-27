import SwiftUI

/// The stretch-reminder card shown in the center overlay.
///
/// The character animation is the visual anchor — it's given the most
/// space and appears above the text, per the product direction that the
/// character should register before the message does.
struct StretchOverlayView: View {
    let character: Character
    let onDismiss: () -> Void

    var body: some View {
        VStack(spacing: 18) {
            CharacterAnimationView(character: character, key: .stretch)
                .frame(width: Constants.characterDisplaySize, height: Constants.characterDisplaySize)

            VStack(spacing: 4) {
                Text(Strings.Overlay.title)
                    .font(.title3.weight(.semibold))
                Text(Strings.Overlay.body)
                    .font(.callout)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
            }

            Button(Strings.Overlay.dismiss, action: onDismiss)
                .buttonStyle(.borderedProminent)
                .keyboardShortcut(.defaultAction)
        }
        .padding(28)
        .frame(width: Constants.overlaySize.width, height: Constants.overlaySize.height)
        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 22, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .strokeBorder(.white.opacity(0.12))
        )
        .shadow(color: .black.opacity(0.25), radius: 24, y: 8)
    }
}
