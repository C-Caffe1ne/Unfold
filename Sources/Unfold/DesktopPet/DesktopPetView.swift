import SwiftUI

/// The Desktop Pet's on-screen content: whatever `SpriteAnimator`
/// `DesktopPetAnimationController` currently has selected for
/// `PetInteractionState.idle`/`.pointerDown`/`.pointerUp`/`.click`. All
/// state/animation-swapping decisions happen in the controller — this view
/// only renders whichever animator it's handed.
struct DesktopPetView: View {
    @ObservedObject var controller: DesktopPetAnimationController

    var body: some View {
        ZStack(alignment: .top) {
            SpriteAnimationView(animator: controller.animator, interpolation: controller.renderStyle.interpolation)

            #if DEBUG
            // Dev-only visual confirmation of state transitions — see the
            // Phase 2 report on why no real per-state art exists yet.
            // Excluded from Release builds entirely.
            Text(String(describing: controller.interactionState))
                .font(.system(size: 10, weight: .medium, design: .monospaced))
                .padding(.horizontal, 4)
                .padding(.vertical, 1)
                .background(.black.opacity(0.6), in: Capsule())
                .foregroundStyle(.white)
                .padding(.top, 2)
            #endif
        }
        .frame(width: Constants.characterDisplaySize, height: Constants.characterDisplaySize)
    }
}
