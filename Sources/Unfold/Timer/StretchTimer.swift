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
/// - No UI code here. Observers react to the `@Published` properties.
@MainActor
final class StretchTimer: ObservableObject {

    enum State: Equatable {
        case running
        case paused
    }

    @Published private(set) var state: State = .running

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

        guard state == .running else {
            timeRemaining = max(0, targetDate.timeIntervalSince(now))
            return
        }

        // Don't count time the user spent away from the keyboard.
        if activityMonitor.isUserIdle {
            targetDate = targetDate.addingTimeInterval(elapsed)
        }

        if now >= targetDate {
            onStretchDue?(StretchEvent(occurredAt: now))
            reset()
            return
        }

        timeRemaining = max(0, targetDate.timeIntervalSince(now))
    }

    deinit {
        ticker?.invalidate()
    }
}
