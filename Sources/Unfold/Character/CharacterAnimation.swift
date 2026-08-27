import Foundation

/// The moment in the app's flow a character animation is used for.
///
/// Built-in kinds are exposed as static constants for type safety and
/// autocomplete, but this is a plain `String` wrapper (the same pattern as
/// `Notification.Name`) rather than a closed `enum` — a user-imported
/// character package can define its own animation keys (e.g. `"wink"`)
/// without this type, `CharacterManager`, or `SpriteAnimator` ever needing
/// to change.
struct AnimationKey: RawRepresentable, Hashable, Codable {
    let rawValue: String

    init(rawValue: String) {
        self.rawValue = rawValue
    }

    init(_ rawValue: String) {
        self.rawValue = rawValue
    }

    static let idle = AnimationKey("idle")
    static let stretch = AnimationKey("stretch")
    static let yawn = AnimationKey("yawn")
    static let celebration = AnimationKey("celebration")
    static let sleep = AnimationKey("sleep")
}
