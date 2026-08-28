import AppKit
import Combine
import Foundation

/// Owns the countdown to the next stretch reminder.
///
/// Design notes:
/// - The countdown is derived from a target `Date`, not a per-second
///   accumulator, so it stays correct even if the 1-second display tick is
///   delayed or coalesced while the app is in the background.
/// - Time spent idle (see `ActivityMonitoring`) does not count down: the
///   target date is pushed forward by however long the last gap actually was.
/// - Sleep is handled here rather than by `ActivityMonitoring` because
///   correcting for it means directly adjusting `targetDate`/`lastTick` —
///   something a simple polled "am I idle right now" signal can't express
///   retroactively for a gap it never had a chance to observe.
/// - No UI code here. Observers react to the `@Published` properties.
@MainActor
final class StretchTimer: ObservableObject {

    enum State: Equatable {
        case running
        case paused
    }

    /// Manual pause/resume only (the user's own toggle). Always wins over
    /// automatic idle pausing — see `isIdlePaused`.
    @Published private(set) var state: State = .running

    /// `true` when the countdown is currently frozen because the user has
    /// been away from the keyboard/mouse longer than `idleThreshold` — an
    /// *automatic* pause, independent of the user's own `state` toggle.
    /// Reported even while manually paused (so the menu can still say why
    /// the countdown isn't moving), but manual pause is what actually stops
    /// `targetDate` from advancing in that case — see `tick()`.
    @Published private(set) var isIdlePaused: Bool = false

    /// Seconds until the next reminder. Clamped at zero.
    @Published private(set) var timeRemaining: TimeInterval

    /// Fired when the countdown reaches zero. The timer reschedules itself
    /// from a full interval immediately afterwards.
    ///
    /// This is the timer's entire public "something happened" surface — it
    /// hands out a `StretchEvent` and nothing more. Deciding what to show
    /// (which character, which overlay) is not this type's job.
    var onStretchDue: ((StretchEvent) -> Void)?

    private let intervalProvider: () -> TimeInterval
    private let activityMonitor: ActivityMonitoring

    private var targetDate: Date
    private var lastTick: Date
    private var ticker: Timer?
    private var wakeObserver: NSObjectProtocol?

    init(
        intervalProvider: @escaping () -> TimeInterval,
        activityMonitor: ActivityMonitoring = SystemActivityMonitor()
    ) {
        self.intervalProvider = intervalProvider
        self.activityMonitor = activityMonitor

        let interval = intervalProvider()
        let now = Date()
        self.targetDate = now.addingTimeInterval(interval)
        self.lastTick = now
        self.timeRemaining = interval

        observeWake()
    }

    // MARK: - Lifecycle

    func start() {
        guard ticker == nil else { return }
        lastTick = Date()

        // The timer is added to the main run loop, so the callback always
        // fires on the main actor — assert that to stay isolation-safe.
        let ticker = Timer(
            timeInterval: Constants.displayTickInterval,
            repeats: true
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.tick()
            }
        }
        RunLoop.main.add(ticker, forMode: .common)
        self.ticker = ticker
        tick()
    }

    // MARK: - Controls

    func togglePause() {
        switch state {
        case .running: pause()
        case .paused: resume()
        }
    }

    func pause() {
        guard state == .running else { return }
        state = .paused
    }

    func resume() {
        guard state == .paused else { return }
        // Re-anchor so the remaining time carries over rather than jumping.
        targetDate = Date().addingTimeInterval(timeRemaining)
        lastTick = Date()
        state = .running
    }

    /// Restart the countdown from a full interval.
    func reset() {
        targetDate = Date().addingTimeInterval(intervalProvider())
        lastTick = Date()
        state = .running
        tick()
    }

    // MARK: - Tick

    private func tick() {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastTick)
        lastTick = now

        isIdlePaused = activityMonitor.isUserIdle

        guard state == .running else {
            // Manual pause freezes everything, including the displayed
            // countdown — nothing here is time-dependent while paused.
            return
        }

        // Don't count time the user spent away from the keyboard: push the
        // deadline forward by exactly the gap that just elapsed, so the
        // *effective* countdown holds still.
        if isIdlePaused {
            targetDate = targetDate.addingTimeInterval(elapsed)
        }

        if now >= targetDate {
            onStretchDue?(StretchEvent(occurredAt: now))
            reset()
            return
        }

        timeRemaining = max(0, targetDate.timeIntervalSince(now))
    }

    // MARK: - Sleep / wake

    /// However long the Mac was actually asleep must never count as active
    /// usage. `CGEventSource`'s idle clock is not documented to behave one
    /// way or the other across a sleep boundary, so this doesn't rely on it:
    /// on wake, the entire gap since the last tick (which stopped firing the
    /// moment the system suspended) is unconditionally excluded, the same
    /// way an idle gap would be. Regular per-second ticks take over again
    /// from there, driven by the real post-wake idle reading.
    private func observeWake() {
        let center = NSWorkspace.shared.notificationCenter
        wakeObserver = center.addObserver(
            forName: NSWorkspace.didWakeNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                self?.handleDidWake()
            }
        }
    }

    private func handleDidWake() {
        guard state == .running else { return }
        let now = Date()
        targetDate = targetDate.addingTimeInterval(now.timeIntervalSince(lastTick))
        lastTick = now
        timeRemaining = max(0, targetDate.timeIntervalSince(now))
    }

    deinit {
        ticker?.invalidate()
        if let wakeObserver {
            NSWorkspace.shared.notificationCenter.removeObserver(wakeObserver)
        }
    }
}
