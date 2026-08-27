import Foundation

/// The fact that it is time to stretch. `StretchTimer` produces this and
/// knows nothing beyond it — no character, no overlay, no notification
/// content. Everything that reacts to a stretch reminder reacts to this
/// value, not to the timer directly.
struct StretchEvent: Equatable {
    let occurredAt: Date
}
