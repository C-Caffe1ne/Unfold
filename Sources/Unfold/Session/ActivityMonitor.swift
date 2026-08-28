import CoreGraphics
import Foundation

/// Tells the timer whether the user is currently idle (away from the Mac).
///
/// Step 1 uses a lightweight system-wide input-idle query. A later version
/// can swap in per-application tracking by providing another conformance —
/// the timer only depends on this protocol.
protocol ActivityMonitoring {
    /// `true` when there has been no user input for longer than the threshold.
    var isUserIdle: Bool { get }
}

/// Idle detection based on the time since the last HID input event.
///
/// A reference type (not a struct) so `AppDelegate` can hold the same
/// instance handed to `StretchTimer` and, in a DEBUG build, wrap it for
/// on-demand idle simulation without the timer needing to know about that.
final class SystemActivityMonitor: ActivityMonitoring {

    /// Read fresh on every `isUserIdle` check rather than cached once at
    /// init, so a threshold change in Settings takes effect on the very
    /// next check — no restart, no explicit "push the new value in" step.
    private let idleThresholdProvider: () -> TimeInterval

    init(idleThresholdProvider: @escaping () -> TimeInterval = { TimeInterval(Constants.defaultIdleThresholdMinutes * 60) }) {
        self.idleThresholdProvider = idleThresholdProvider
    }

    /// Input event types that count as "the user is here".
    private let inputEventTypes: [CGEventType] = [
        .keyDown,
        .leftMouseDown, .rightMouseDown, .otherMouseDown,
        .mouseMoved,
        .leftMouseDragged, .rightMouseDragged,
        .scrollWheel
    ]

    /// Seconds since the most recent user input of any tracked kind.
    var secondsSinceLastInput: TimeInterval {
        inputEventTypes
            .map { CGEventSource.secondsSinceLastEventType(.combinedSessionState, eventType: $0) }
            .min() ?? 0
    }

    var isUserIdle: Bool {
        secondsSinceLastInput >= idleThresholdProvider()
    }
}

#if DEBUG
/// Wraps a real `ActivityMonitoring` with a debug-only override so idle
/// state can be flipped instantly from the "Simulate Idle" / "Simulate
/// Active" (Debug) menu items instead of waiting out the real threshold.
/// `forcedIdle == nil` (the default) defers to the wrapped monitor's real
/// reading. Compiled out of release builds entirely — release always talks
/// to a plain `SystemActivityMonitor` with no override surface at all.
final class DebugOverridableActivityMonitor: ActivityMonitoring {
    private let wrapped: ActivityMonitoring
    var forcedIdle: Bool?

    init(wrapping wrapped: ActivityMonitoring) {
        self.wrapped = wrapped
    }

    var isUserIdle: Bool {
        forcedIdle ?? wrapped.isUserIdle
    }
}
#endif
