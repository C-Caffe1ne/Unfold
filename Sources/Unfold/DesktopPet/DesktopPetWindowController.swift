import AppKit
import SwiftUI

/// A borderless, transparent, floating panel that shows the current
/// character's animation on the desktop for as long as Spine Keepet is
/// running — independent of, and unaffected by, the Stretch Reminder
/// overlay (`OverlayController`/`OverlayWindowController`).
///
/// Phase 1 added always-on idle display. Phase 2 added pointerDown/
/// pointerUp/click mouse interaction (see `PetInteractionStateMachine`/
/// `DesktopPetAnimationController`). Phase 3 added dragging the pet around
/// the screen and remembering where it was dropped. Phase 4 makes only the
/// character's actually-drawn pixels intercept mouse events — the
/// transparent rest of the 192×192 canvas click-throughs to whatever app is
/// underneath (see `DesktopPetHitTestPolicy`/`CGImageAlphaHitTester`) —
/// still no live character-switch tracking, no Settings toggle. The window
/// is created once at launch with whatever character is currently selected
/// and shown for the app's whole lifetime.
///
/// Reuses the exact same playback pipeline every other character view uses
/// — `AnimationClipLoader` → `AnimationClip` → `SpriteAnimator` →
/// `SpriteAnimationView` — completely unmodified. This controller owns
/// window chrome/placement and raw AppKit mouse-event capture; it hands
/// every actual state/animation decision to `DesktopPetAnimationController`,
/// every actual placement/clamping/screen-selection computation to the
/// AppKit-independent `DesktopPetGeometry`, and every click-through
/// decision to `DesktopPetHitTestPolicy`.
@MainActor
final class DesktopPetWindowController: NSWindowController {

    private var animationController: DesktopPetAnimationController?
    private var positionStore = DesktopPetPositionStore()

    /// Captured at mouseDown, cleared at mouseUp — this press's drag
    /// baseline, in *global screen* coordinates (not window-local: once a
    /// drag starts moving the window, a window-local coordinate's own
    /// reference frame would be shifting underneath it on every event,
    /// corrupting any delta computed from it).
    private var dragStartWindowOrigin: CGPoint?
    private var dragStartGlobalMouseLocation: CGPoint?

    private var screenParametersObserver: NSObjectProtocol?

    /// Reads the current global cursor position — `NSEvent.mouseLocation`
    /// (a real system call) in production, injectable so drag tests can
    /// control it deterministically instead of depending on wherever the
    /// real cursor happens to be on the machine running the test. Not
    /// `private`, same reasoning `SystemActivityMonitor`'s injected
    /// providers exist for.
    var currentGlobalMouseLocation: () -> CGPoint = { NSEvent.mouseLocation }

    /// Phase 4: alpha hit-testing so only the character's actually-drawn
    /// pixels intercept mouse events — transparent canvas click-through to
    /// whatever's underneath. `CGImageAlphaHitTester` owns its own decode
    /// cache; `DesktopPetHitTestPolicy` is the pure decision this
    /// controller feeds live values into.
    private let hitTester = CGImageAlphaHitTester()
    private var hitTestTimer: Timer?

    /// `nil` only if the character's idle asset failed to load (see the SF
    /// Symbol fallback below). Not `private` specifically so tests can
    /// drive real `NSEvent`s at the window and assert on the resulting
    /// interaction state without reaching into AppKit's own event queue —
    /// same reasoning `SpriteAnimator.advance()` was made internal for.
    var interactionState: PetInteractionState? { animationController?.interactionState }

    convenience init(character: Character, positionStore: DesktopPetPositionStore = DesktopPetPositionStore()) {
        let size = CGSize(width: Constants.characterDisplaySize, height: Constants.characterDisplaySize)
        let panel = DesktopPetPanel(
            contentRect: NSRect(origin: .zero, size: size),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        let animationController = DesktopPetAnimationController(character: character)
        let hostingController: NSHostingController<AnyView>
        if let animationController {
            hostingController = NSHostingController(rootView: AnyView(DesktopPetView(controller: animationController)))
        } else {
            // Same fallback `CharacterAnimationView` uses when its asset
            // can't be loaded — a static SF Symbol instead of failing.
            hostingController = NSHostingController(rootView: AnyView(
                Image(systemName: character.thumbnailSymbolName)
                    .font(.system(size: 64))
                    .symbolRenderingMode(.hierarchical)
                    .frame(width: size.width, height: size.height)
            ))
        }
        // `NSHostingController`'s default sizing behavior resizes its
        // window to the SwiftUI content's *preferred* size — which is zero
        // until SwiftUI has run a layout pass. Since the window's exact
        // size is already dictated by `Constants.characterDisplaySize`
        // (not something SwiftUI should get to decide), disable that and
        // set the size explicitly, so the window is never briefly zero-
        // sized or mis-sized before layout catches up.
        hostingController.sizingOptions = []
        panel.contentViewController = hostingController
        panel.setContentSize(size)

        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.level = .floating
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.isMovableByWindowBackground = false
        panel.hidesOnDeactivate = false
        panel.isReleasedWhenClosed = false
        // Phase 2: the pet needs real mouseDown/mouseUp (and, as of Phase 3,
        // mouseDragged). `.nonactivatingPanel` (set above) plus
        // `canBecomeKey`/`canBecomeMain` staying `false` (see
        // `DesktopPetPanel` below) together mean AppKit still delivers
        // these events without ever making the panel key/main or activating
        // Spine Keepet over whatever app the user is using.
        panel.ignoresMouseEvents = false

        self.init(window: panel)
        self.animationController = animationController
        self.positionStore = positionStore

        panel.onMouseDown = { [weak self] event in self?.handleMouseDown(event) }
        panel.onMouseDragged = { [weak self] event in self?.handleMouseDragged(event) }
        panel.onMouseUp = { [weak self] event in self?.handleMouseUp(event) }

        positionAtStartup()
        observeScreenParameterChanges()
        startHitTestPolling()
    }

    deinit {
        if let screenParametersObserver {
            NotificationCenter.default.removeObserver(screenParametersObserver)
        }
        hitTestTimer?.invalidate()
    }

    func show() {
        window?.orderFrontRegardless()
    }

    // MARK: - Mouse events

    /// Hit area is still the full 192×192 pet canvas, not just the
    /// character's visible/alpha pixels — see the Phase 2 report §13 (and
    /// the Phase 3 report, which carries the same limitation forward) for
    /// why, and for why that doesn't block adding real alpha hit-testing
    /// later (it would only gate this single call site).
    private func handleMouseDown(_ event: NSEvent) {
        guard let window else { return }
        let globalLocation = currentGlobalMouseLocation()
        dragStartWindowOrigin = window.frame.origin
        dragStartGlobalMouseLocation = globalLocation
        animationController?.mouseDown(at: Date(), location: globalLocation)
    }

    /// Never rebuilds or restarts the current animation (see
    /// `DesktopPetAnimationController.mouseDragged`) — only ever moves the
    /// window, and only once the state machine has actually latched
    /// `isDragging`, so a sub-threshold jitter never nudges the pet.
    private func handleMouseDragged(_ event: NSEvent) {
        guard
            let window,
            let startOrigin = dragStartWindowOrigin,
            let startLocation = dragStartGlobalMouseLocation
        else {
            return
        }

        let currentGlobalLocation = currentGlobalMouseLocation()
        animationController?.mouseDragged(to: currentGlobalLocation)
        guard animationController?.isDragging == true else { return }

        let dragged = DesktopPetGeometry.draggedOrigin(
            startWindowOrigin: startOrigin,
            startGlobalLocation: startLocation,
            currentGlobalLocation: currentGlobalLocation
        )
        window.setFrameOrigin(clampedOrigin(dragged, windowSize: window.frame.size))
    }

    private func handleMouseUp(_ event: NSEvent) {
        // Read *before* calling mouseUp(at:), which resets it — see
        // `DesktopPetAnimationController.isDragging`'s doc comment.
        let wasDragging = animationController?.isDragging ?? false

        animationController?.mouseUp(at: Date())

        if wasDragging {
            savePositionIfPossible()
        }
        dragStartWindowOrigin = nil
        dragStartGlobalMouseLocation = nil
    }

    // MARK: - Alpha hit-testing (Phase 4)

    /// Why a poll at all, rather than just handling `mouseMoved`: once
    /// `ignoresMouseEvents` is `true`, AppKit stops delivering *any* event
    /// (including `mouseMoved`) to this window — nothing would ever notice
    /// the cursor re-entering a visible pixel to flip it back. A
    /// lightweight timer polling `NSEvent.mouseLocation` (a plain,
    /// no-permission-required system call — no Accessibility/Input
    /// Monitoring/Screen Recording entitlement, no global event tap) is the
    /// simplest fix that doesn't touch entitlements. ~30Hz keeps the CPU
    /// cost of "check a point against a rect, and only sometimes re-decode
    /// a ~150KB image" negligible for an always-running overlay — see
    /// `CGImageAlphaHitTester`'s single-slot decode cache, which only
    /// re-decodes when the visible frame actually changes (idle plays at a
    /// few fps, far slower than the poll).
    private func startHitTestPolling() {
        let timer = Timer(timeInterval: 1.0 / 30.0, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.refreshMouseEventAcceptance() }
        }
        RunLoop.main.add(timer, forMode: .common)
        hitTestTimer = timer
    }

    /// One tick of the poll: decides via `DesktopPetHitTestPolicy` and
    /// applies the result to `window.ignoresMouseEvents`. Not `private` so
    /// tests can drive it directly and deterministically, without spinning
    /// the real run loop or depending on the real system cursor position —
    /// same reasoning `currentGlobalMouseLocation` is injectable for.
    func refreshMouseEventAcceptance() {
        guard let window else { return }
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: animationController?.interactionState,
            isDragging: animationController?.isDragging ?? false,
            globalMouseLocation: currentGlobalMouseLocation(),
            windowFrame: window.frame,
            currentFrame: animationController?.animator.currentFrame,
            hitTester: hitTester
        )
        window.ignoresMouseEvents = !accept
    }

    // MARK: - Placement

    private func positionAtStartup() {
        guard let window else { return }
        let screens = NSScreen.screens
        guard !screens.isEmpty else { return }

        if
            let stored = positionStore.load(),
            let matchingScreen = screens.first(where: { $0.displayID == stored.screenIdentifier })
        {
            let visibleFrame = matchingScreen.visibleFrame
            let origin = DesktopPetGeometry.origin(
                fromNormalized: CGPoint(x: stored.normalizedX, y: stored.normalizedY),
                windowSize: window.frame.size,
                visibleFrame: visibleFrame
            )
            window.setFrameOrigin(DesktopPetGeometry.clamp(CGRect(origin: origin, size: window.frame.size), to: visibleFrame).origin)
            return
        }

        // No saved position, its screen is gone, or the saved value was
        // corrupt (`positionStore.load()` already returns `nil` for that
        // case) — the Phase 1 default.
        positionAtBottomRight(on: NSScreen.main ?? screens[0])
    }

    private func positionAtBottomRight(on screen: NSScreen) {
        guard let window else { return }
        let origin = DesktopPetGeometry.bottomRightOrigin(
            visibleFrame: screen.visibleFrame,
            windowSize: window.frame.size,
            margin: Constants.desktopPetScreenMargin
        )
        window.setFrameOrigin(origin)
    }

    /// Clamps `origin` into whichever on-screen `visibleFrame` the
    /// resulting window frame would overlap the most — not necessarily the
    /// screen the drag started on, since a drag can cross from one monitor
    /// to another.
    private func clampedOrigin(_ origin: CGPoint, windowSize: CGSize) -> CGPoint {
        let visibleFrames = NSScreen.screens.map(\.visibleFrame)
        let proposedFrame = CGRect(origin: origin, size: windowSize)
        guard let index = DesktopPetGeometry.bestScreenIndex(forWindowFrame: proposedFrame, among: visibleFrames) else {
            return origin
        }
        return DesktopPetGeometry.clamp(proposedFrame, to: visibleFrames[index]).origin
    }

    private func savePositionIfPossible() {
        guard let window else { return }
        let screens = NSScreen.screens
        let visibleFrames = screens.map(\.visibleFrame)
        guard
            let index = DesktopPetGeometry.bestScreenIndex(forWindowFrame: window.frame, among: visibleFrames),
            let displayID = screens[index].displayID
        else {
            return
        }

        let normalized = DesktopPetGeometry.normalizedPosition(
            forOrigin: window.frame.origin,
            windowSize: window.frame.size,
            visibleFrame: visibleFrames[index]
        )
        positionStore.save(DesktopPetStoredPosition(
            screenIdentifier: Int(displayID),
            normalizedX: normalized.x,
            normalizedY: normalized.y
        ))
    }

    /// Display disconnect/resolution-change safety net (§15): keeps the pet
    /// from being stranded off-screen. Deliberately simple — just re-clamps
    /// the current frame into whichever visible screen it now overlaps
    /// most; it does not attempt to preserve the pet's relative position
    /// through the reconfiguration beyond that.
    private func observeScreenParameterChanges() {
        screenParametersObserver = NotificationCenter.default.addObserver(
            forName: NSApplication.didChangeScreenParametersNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            // `queue: .main` guarantees this always runs on the main
            // thread — same reasoning `SpriteAnimator`'s Timer callback
            // uses `MainActor.assumeIsolated` for.
            MainActor.assumeIsolated {
                self?.clampToCurrentScreens()
            }
        }
    }

    private func clampToCurrentScreens() {
        guard let window else { return }
        let visibleFrames = NSScreen.screens.map(\.visibleFrame)
        guard let index = DesktopPetGeometry.bestScreenIndex(forWindowFrame: window.frame, among: visibleFrames) else { return }
        let clamped = DesktopPetGeometry.clamp(window.frame, to: visibleFrames[index])
        window.setFrameOrigin(clamped.origin)
    }
}

/// `NSPanel` subclass: never becomes key or main, on top of the
/// `.nonactivatingPanel` style mask already passed at creation — ordering
/// it front, or clicking/dragging it, must never steal focus or activate
/// Spine Keepet over whatever app the user is using. This holds across
/// Phase 1 (no interaction), Phase 2 (click/press), and Phase 3 (drag): a
/// window can receive raw mouse events without becoming key/main or
/// triggering app activation — that combination is exactly what a
/// floating, non-activating tool panel is for.
///
/// `mouseDown`/`mouseDragged`/`mouseUp` are overridden here (rather than
/// relying on SwiftUI gesture recognizers on the hosted content) because
/// the pet's content is a plain, non-interactive `Image` — with no
/// `Button`/gesture anywhere in it to consume the event, `NSResponder`'s
/// default implementation on whatever view SwiftUI hit-tests to just
/// forwards up the responder chain, reaching the window itself.
private final class DesktopPetPanel: NSPanel {
    var onMouseDown: ((NSEvent) -> Void)?
    var onMouseDragged: ((NSEvent) -> Void)?
    var onMouseUp: ((NSEvent) -> Void)?

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    override func mouseDown(with event: NSEvent) {
        onMouseDown?(event)
    }

    override func mouseDragged(with event: NSEvent) {
        onMouseDragged?(event)
    }

    override func mouseUp(with event: NSEvent) {
        onMouseUp?(event)
    }
}

extension NSScreen {
    /// A `CGDirectDisplayID` — the stable-enough-for-this-purpose identifier
    /// macOS itself uses for a physical display, as opposed to an
    /// `NSScreen.screens` array index (which reorders/reassigns across
    /// reconfigurations and must never be persisted).
    var displayID: Int? {
        (deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.intValue
    }
}
