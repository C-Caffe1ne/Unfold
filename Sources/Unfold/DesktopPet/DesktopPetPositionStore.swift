import Foundation

/// A saved Desktop Pet position: which screen it was on, and where within
/// that screen's usable movement range (see `DesktopPetGeometry
/// .normalizedPosition`) — not a raw global `CGPoint`, so it survives a
/// resolution change or the pet being restored on a different-sized
/// display than it was saved on.
struct DesktopPetStoredPosition: Equatable {
    /// A `CGDirectDisplayID`, stored as `Int` for straightforward
    /// `UserDefaults` round-tripping. Not an `NSScreen.screens` array
    /// index — those aren't stable across relaunches/reconfigurations.
    let screenIdentifier: Int
    /// Both in `0...1`, within that screen's usable movement range.
    let normalizedX: Double
    let normalizedY: Double
}

/// Persists/restores the Desktop Pet's last dragged-to position.
/// `UserDefaults`-backed, with an injectable instance so tests never touch
/// `.standard`. Deliberately small: three keys, save/load/clear, and
/// defensive validation on load so a corrupt or partially-written value
/// (e.g. from a future incompatible version) always falls back to `nil`
/// rather than crashing or handing back nonsense.
final class DesktopPetPositionStore {

    private enum Key {
        static let screenIdentifier = "desktopPetScreenIdentifier"
        static let normalizedX = "desktopPetNormalizedX"
        static let normalizedY = "desktopPetNormalizedY"
    }

    private let defaults: UserDefaults

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    func save(_ position: DesktopPetStoredPosition) {
        defaults.set(position.screenIdentifier, forKey: Key.screenIdentifier)
        defaults.set(position.normalizedX, forKey: Key.normalizedX)
        defaults.set(position.normalizedY, forKey: Key.normalizedY)
    }

    /// `nil` if nothing has been saved yet, or if what's stored is missing,
    /// the wrong type, non-finite, or outside `0...1` — every one of those
    /// is a "fall back to the Phase 1 default position" case for the
    /// caller, never a crash.
    func load() -> DesktopPetStoredPosition? {
        guard
            let screenIdentifier = defaults.object(forKey: Key.screenIdentifier) as? Int,
            defaults.object(forKey: Key.normalizedX) != nil,
            defaults.object(forKey: Key.normalizedY) != nil
        else {
            return nil
        }

        let x = defaults.double(forKey: Key.normalizedX)
        let y = defaults.double(forKey: Key.normalizedY)
        guard x.isFinite, y.isFinite, (0...1).contains(x), (0...1).contains(y) else {
            return nil
        }

        return DesktopPetStoredPosition(screenIdentifier: screenIdentifier, normalizedX: x, normalizedY: y)
    }

    func clear() {
        defaults.removeObject(forKey: Key.screenIdentifier)
        defaults.removeObject(forKey: Key.normalizedX)
        defaults.removeObject(forKey: Key.normalizedY)
    }
}
