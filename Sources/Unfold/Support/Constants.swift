import CoreGraphics
import Foundation

/// Tunable values that would otherwise be magic numbers scattered through
/// the code. Adjust behaviour here, not at the call sites.
enum Constants {

    /// Menu bar icon, as an SF Symbol name.
    /// Alternatives worth trying: `figure.walk`, `arrow.up.and.down`.
    static let menuBarSymbolName = "figure.stand"

    /// How often the countdown display refreshes, in seconds.
    /// This is only a UI cadence — the countdown itself is derived from a
    /// target `Date`, so a delayed tick never drifts.
    static let displayTickInterval: TimeInterval = 1

    /// After this many seconds without keyboard/mouse input, the countdown
    /// freezes so that time spent away from the Mac does not count.
    static let idleThreshold: TimeInterval = 60

    /// Interval used on the very first launch, before the user picks one.
    static let defaultStretchIntervalMinutes = 60

    /// Selectable presets shown in the menu, in minutes. "Custom" is separate.
    static let presetIntervalMinutes = [30, 45, 60, 90, 120]

    /// Allowed bounds for a custom interval, in minutes.
    static let customIntervalRange = 5...240

    /// Size of the stretch-reminder overlay panel. Recommended range for the
    /// width is 360–440pt; this sits in the middle of it.
    static let overlaySize = CGSize(width: 380, height: 380)

    /// Logical size the character animation is drawn at inside the overlay.
    /// Recommended range is 160–220pt; frames are authored at 384×384px so
    /// this stays crisp on Retina displays.
    static let characterDisplaySize: CGFloat = 192

    /// How long the overlay stays on screen before dismissing itself, if at
    /// all. `nil` is the V1 default — the overlay stays until the user
    /// dismisses it. Set a value here (or drive it from
    /// `SpriteAnimator.isFinished`) to bring back a timed auto-dismiss.
    static let overlayAutoDismissDelay: TimeInterval? = nil

    /// Top-level folder, inside the app's resource bundle, that holds one
    /// subdirectory per built-in character package. Mirrors
    /// `Sources/Unfold/Resources/Characters` — see the `resources:` entry
    /// in Package.swift (SwiftPM copies that directory to the bundle root
    /// under its own name, dropping the `Resources/` prefix).
    static let builtInCharactersResourceSubdirectory = "Characters"
}
