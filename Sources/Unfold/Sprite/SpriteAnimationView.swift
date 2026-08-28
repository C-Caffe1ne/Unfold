import AppKit
import SwiftUI

/// Generic sprite-sheet animation renderer. Displays whatever frame
/// `SpriteAnimator` currently has — it has no idea which character or which
/// animation kind it's showing, so a new character never requires a new
/// view type (no `CatAnimationView`, no `DogAnimationView`).
///
/// Starts/stops playback with its own lifecycle, so an animation only
/// consumes CPU while this view is actually on screen.
struct SpriteAnimationView: View {
    @ObservedObject var animator: SpriteAnimator

    /// `.none` keeps pixel art crisp; smoother interpolation suits regular
    /// illustration. Callers pick based on their art style.
    var interpolation: Image.Interpolation = .medium

    var body: some View {
        Group {
            if let frame = animator.currentFrame {
                Image(decorative: frame, scale: 1, orientation: .up)
                    .resizable()
                    .interpolation(interpolation)
                    .aspectRatio(contentMode: .fit)
            } else {
                Color.clear
            }
        }
        .onAppear {
            // Respect "Reduce Motion": the animator has already rendered its
            // first frame in `init`, so simply not starting playback leaves
            // a static pose on screen instead of playing the full clip.
            guard !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion else { return }
            animator.play()
        }
        .onDisappear { animator.stop() }
    }
}
