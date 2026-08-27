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
struct SystemActivityMonitor: ActivityMonitoring {

    var idleThreshold: TimeInterval = Constants.idleThreshold

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
        secondsSinceLastInput >= idleThreshold
    }
}
