import XCTest
@testable import Unfold

/// `DesktopPetHitTestPolicy` decides whether the Desktop Pet window should
/// currently accept mouse events (`ignoresMouseEvents = false`) or let them
/// pass through to whatever's underneath. Pure decision logic — no
/// `NSWindow`/`NSPanel`/`Timer`/`NSEvent` anywhere in it — driven only by
/// values `DesktopPetWindowController` reads from real AppKit/animation
/// state, so every branch (alpha hit vs. miss, window-bounds miss, and the
/// §11/§12 "never lose ownership mid-interaction" rule) is directly
/// testable here without a live window or run loop.
final class DesktopPetHitTestPolicyTests: XCTestCase {

    private let bounds = CGRect(x: 0, y: 0, width: 20, height: 20)
    private let windowFrame = CGRect(x: 100, y: 100, width: 20, height: 20) // global screen coords

    private func makeOpaqueImage() -> CGImage {
        GIFFixtureBuilder.makeImage(width: 20, height: 20) { _, _ in .white }
    }

    private func makeTransparentImage() -> CGImage {
        GIFFixtureBuilder.makeImage(width: 20, height: 20) { _, _ in .clear }
    }

    // MARK: - Alpha-based decision (idle, no active interaction)

    func test_idle_cursorOverOpaquePixel_accepts() {
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .idle,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 110, y: 110), // window-local (10, 10)
            windowFrame: windowFrame,
            currentFrame: makeOpaqueImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }

    func test_idle_cursorOverTransparentPixel_rejects() {
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .idle,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 110, y: 110),
            windowFrame: windowFrame,
            currentFrame: makeTransparentImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertFalse(accept)
    }

    func test_idle_cursorOutsideWindowFrame_rejects_regardlessOfImageContent() {
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .idle,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 5000, y: 5000),
            windowFrame: windowFrame,
            currentFrame: makeOpaqueImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertFalse(accept)
    }

    func test_noCurrentFrame_fallsBackToAccepting() {
        // No decoded frame to test against (e.g. the SF Symbol fallback path
        // when a character's asset fails to load) — must not crash, and
        // must preserve the pre-Phase-4 "whole canvas is the hit area"
        // behavior rather than silently becoming permanently click-through.
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .idle,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 110, y: 110),
            windowFrame: windowFrame,
            currentFrame: nil,
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }

    // MARK: - §12: pointerDown ownership — must not be lost mid-hold, even over a transparent pixel

    func test_pointerDown_overTransparentPixel_stillAccepts() {
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .pointerDown,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 110, y: 110),
            windowFrame: windowFrame,
            currentFrame: makeTransparentImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }

    func test_pointerDown_cursorOutsideWindowFrame_stillAccepts() {
        // A drag can carry the cursor beyond the window's own frame for a
        // moment between events — ownership must not drop.
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .pointerDown,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 5000, y: 5000),
            windowFrame: windowFrame,
            currentFrame: makeTransparentImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }

    // MARK: - §11: drag ownership — isDragging alone (defensive, even if state weren't pointerDown)

    func test_isDragging_overTransparentPixel_stillAccepts() {
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: .idle,
            isDragging: true,
            globalMouseLocation: CGPoint(x: 110, y: 110),
            windowFrame: windowFrame,
            currentFrame: makeTransparentImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }

    // MARK: - §15: alpha policy resumes once the interaction ends

    func test_afterInteractionEnds_alphaPolicyResumes_andRejectsATransparentPixel() {
        // pointerUp/click reactions are still mid-flight but the mouse
        // button itself has already been released — hover-based hit
        // testing must apply again immediately, not wait for `.idle`.
        for state: PetInteractionState in [.click, .pointerUp, .idle] {
            let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
                interactionState: state,
                isDragging: false,
                globalMouseLocation: CGPoint(x: 110, y: 110),
                windowFrame: windowFrame,
                currentFrame: makeTransparentImage(),
                hitTester: CGImageAlphaHitTester()
            )
            XCTAssertFalse(accept, "state \(state) must use alpha hit-testing once the mouse button is no longer held")
        }
    }

    func test_nilInteractionState_usesAlphaPolicy() {
        // `nil` happens when there's no `DesktopPetAnimationController` at
        // all — same "no ongoing interaction" treatment as `.idle`.
        let accept = DesktopPetHitTestPolicy.shouldAcceptMouseEvents(
            interactionState: nil,
            isDragging: false,
            globalMouseLocation: CGPoint(x: 110, y: 110),
            windowFrame: windowFrame,
            currentFrame: makeOpaqueImage(),
            hitTester: CGImageAlphaHitTester()
        )
        XCTAssertTrue(accept)
    }
}
