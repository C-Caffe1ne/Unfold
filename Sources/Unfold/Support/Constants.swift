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

    /// Default "away" threshold, in minutes, before the countdown freezes —
    /// used only to seed `SettingsStore` on first launch. From then on the
    /// live value is user-editable in Settings and read fresh on every
    /// check (see `SystemActivityMonitor`'s `idleThresholdProvider`), not
    /// copied once at startup.
    ///
    /// Product policy: the gap between the last input and this threshold
    /// still counts as active usage (the user could plausibly still be
    /// reading the screen); only time *beyond* the threshold is excluded.
    /// A returning user resumes accruing active time immediately.
    static let defaultIdleThresholdMinutes = 5

    /// Selectable "pause when I'm away for" presets in Settings, in minutes.
    static let idleThresholdPresetMinutes = [1, 3, 5, 10, 15]

    /// Interval used on the very first launch, before the user picks one.
    static let defaultStretchIntervalMinutes = 60

    /// Selectable presets shown in the menu and Settings, in minutes.
    /// "Custom" is separate.
    static let presetIntervalMinutes = [30, 45, 60, 90, 120]

    /// Allowed bounds for a custom interval, in minutes. Shared by the menu
    /// bar's Custom… prompt and the Settings window so there's one answer
    /// to "what's a valid interval," not two.
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

    /// Longest mouseDown→mouseUp duration on the Desktop Pet that still
    /// counts as a "click" rather than a long press. At or under this reads
    /// as `.click`; anything longer reads as `.pointerUp`.
    static let petClickThreshold: TimeInterval = 0.22

    /// Cursor movement (in points, global screen coordinates) from
    /// mouseDown before a press is treated as a drag rather than a
    /// click/long-press candidate.
    static let petDragThreshold: CGFloat = 5

    /// Gap kept between the Desktop Pet window and the screen's right/bottom
    /// `visibleFrame` edges, so it doesn't sit flush against the edge (or
    /// over the Dock — `visibleFrame` already excludes that).
    static let desktopPetScreenMargin: CGFloat = 24

    /// Minimum alpha (0...255) a Desktop Pet pixel needs to count as
    /// "visible" for click-through hit-testing. `default-cat`'s real
    /// spritesheet is measured to be ~98.5% hard alpha (0 or 255), but does
    /// have a real anti-aliased edge band — this sits at the spec's
    /// recommended `alpha >= 0.1` (0.1 * 255 ≈ 25.5, rounded up to 26), so
    /// faint edge-antialiasing pixels read as transparent (pass-through)
    /// rather than part of the hit area.
    static let petHitTestAlphaThreshold: UInt8 = 26

    /// Extra radius (in display points, converted to source-image pixels by
    /// the current aspect-fit scale) searched around the cursor's mapped
    /// pixel before giving up on a hit — makes thin extremities (tail, ear
    /// tips) easier to grab without pixel-perfect precision, while staying
    /// far short of turning the hit area back into a rectangle.
    static let petHitTestPaddingPoints: CGFloat = 2

    /// Top-level folder, inside the app's resource bundle, that holds one
    /// subdirectory per built-in character package. Mirrors
    /// `Sources/Unfold/Resources/Characters` — see the `resources:` entry
    /// in Package.swift (SwiftPM copies that directory to the bundle root
    /// under its own name, dropping the `Resources/` prefix).
    static let builtInCharactersResourceSubdirectory = "Characters"

    /// Folder name for this app inside Application Support. In the sandbox,
    /// it maps into the app's own container, so no file-access entitlement
    /// is needed.
    static let applicationSupportFolderName = "Unfold"

    /// Folder that holds one directory per user-created character package.
    /// Uses the same layout as `builtInCharactersResourceSubdirectory`
    /// (`<id>/character.json` + assets), so the same loader reads both.
    static let userCharactersFolderName = "Characters"
}
