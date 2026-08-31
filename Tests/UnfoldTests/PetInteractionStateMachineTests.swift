import XCTest
@testable import Unfold

/// `PetInteractionStateMachine` is pure timing/state logic — no AppKit, no
/// animation, no wall-clock waits. Every test injects its own `Date`s so
/// transitions are fully deterministic.
final class PetInteractionStateMachineTests: XCTestCase {

    private let t0 = Date(timeIntervalSince1970: 1_000_000)
    private let threshold: TimeInterval = 0.22
    private let dragThreshold: CGFloat = 5

    private func makeMachine() -> PetInteractionStateMachine {
        PetInteractionStateMachine(clickThreshold: threshold, dragThreshold: dragThreshold)
    }

    // 1. initial = idle
    func test_initialState_isIdle() {
        XCTAssertEqual(makeMachine().state, .idle)
    }

    // 2. mouseDown -> pointerDown
    func test_mouseDown_fromIdle_transitionsToPointerDown() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        XCTAssertEqual(machine.state, .pointerDown)
    }

    // 3. 0.1s quick mouseUp -> click
    func test_mouseUp_shortlyAfterMouseDown_transitionsToClick() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(0.1))
        XCTAssertEqual(machine.state, .click)
    }

    // 4. exactly 0.22s -> click (boundary is inclusive)
    func test_mouseUp_atExactlyThreshold_transitionsToClick() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(threshold))
        XCTAssertEqual(machine.state, .click)
    }

    // 5. >0.22s -> pointerUp
    func test_mouseUp_afterThreshold_transitionsToPointerUp() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(threshold + 0.001))
        XCTAssertEqual(machine.state, .pointerUp)
    }

    // 6. click finished -> idle
    func test_animationFinished_fromClick_transitionsToIdle() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(0.1)) // -> click
        machine.animationFinished()
        XCTAssertEqual(machine.state, .idle)
    }

    // 7. pointerUp finished -> idle
    func test_animationFinished_fromPointerUp_transitionsToIdle() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(1)) // -> pointerUp
        machine.animationFinished()
        XCTAssertEqual(machine.state, .idle)
    }

    // 8. pointerDown animation finished while still held -> stays pointerDown
    func test_animationFinished_fromPointerDown_whileMouseStillDown_staysPointerDown() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.animationFinished() // reaction clip finished, mouse never came up
        XCTAssertEqual(machine.state, .pointerDown, "must hold the last frame, not fall back to idle on its own")
    }

    // 9. mouseUp without prior mouseDown is safe (no-op)
    func test_mouseUp_withoutPriorMouseDown_isIgnored() {
        let machine = makeMachine()
        machine.mouseUp(at: t0)
        XCTAssertEqual(machine.state, .idle)
    }

    func test_mouseUp_withoutPriorMouseDown_fromNonIdleState_isIgnored() {
        // A stray mouseUp while already resolving a reaction must not
        // re-derive a bogus duration from a stale/absent mouseDown timestamp.
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(0.1)) // -> click
        machine.mouseUp(at: t0.addingTimeInterval(5)) // spurious second mouseUp
        XCTAssertEqual(machine.state, .click, "a second mouseUp with no matching mouseDown must not change state")
    }

    // 10. duplicate mouseDown is safe
    func test_duplicateMouseDown_whileAlreadyPointerDown_restartsTheHoldTimer() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseDown(at: t0.addingTimeInterval(0.05), location: CGPoint(x: 1, y: 1))
        XCTAssertEqual(machine.state, .pointerDown)

        // Duration must be measured from the *second* mouseDown, not the
        // first — a quick mouseUp right after the duplicate must still
        // read as a click even though the first mouseDown was long ago.
        machine.mouseUp(at: t0.addingTimeInterval(0.1))
        XCTAssertEqual(machine.state, .click)
    }

    // 11. new mouseDown while a reaction (click/pointerUp) is still playing
    //     cancels it and starts a fresh pointerDown
    func test_mouseDown_whileClickReactionStillPlaying_cancelsItAndStartsPointerDown() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(0.1)) // -> click, reaction "still playing"
        XCTAssertEqual(machine.state, .click)

        machine.mouseDown(at: t0.addingTimeInterval(0.15), location: .zero)
        XCTAssertEqual(machine.state, .pointerDown, "a new mouseDown must cancel the in-flight click reaction")
    }

    func test_mouseDown_whilePointerUpReactionStillPlaying_cancelsItAndStartsPointerDown() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: .zero)
        machine.mouseUp(at: t0.addingTimeInterval(1)) // -> pointerUp
        XCTAssertEqual(machine.state, .pointerUp)

        machine.mouseDown(at: t0.addingTimeInterval(1.1), location: .zero)
        XCTAssertEqual(machine.state, .pointerDown)
    }

    // 12. repeated quick clicks are handled safely and deterministically
    func test_repeatedQuickClicks_eachResolveIndependently() {
        let machine = makeMachine()
        for i in 0..<5 {
            let down = t0.addingTimeInterval(TimeInterval(i))
            machine.mouseDown(at: down, location: .zero)
            XCTAssertEqual(machine.state, .pointerDown)
            machine.mouseUp(at: down.addingTimeInterval(0.05))
            XCTAssertEqual(machine.state, .click)
            machine.animationFinished()
            XCTAssertEqual(machine.state, .idle)
        }
    }

    // MARK: - Context captured for a future Drag phase

    func test_mouseDown_capturesLocation_forFutureUse() {
        let machine = makeMachine()
        let location = CGPoint(x: 42, y: 7)
        machine.mouseDown(at: t0, location: location)
        XCTAssertEqual(machine.lastMouseDownLocation, location)
    }

    // MARK: - Drag (Phase 3) — no new PetInteractionState case; `isDragging`
    // is separate interaction context alongside `state`, which stays
    // `.pointerDown` throughout.

    // 1. movement < 5pt -> drag 아님
    func test_mouseDragged_belowThreshold_doesNotStartADrag() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 103, y: 100)) // 3pt

        XCTAssertFalse(machine.isDragging)
        XCTAssertEqual(machine.state, .pointerDown)
    }

    // 2. movement exactly threshold -> drag 시작 (boundary inclusive)
    func test_mouseDragged_atExactlyThreshold_startsADrag() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 105, y: 100)) // exactly 5pt

        XCTAssertTrue(machine.isDragging)
    }

    // 3. movement > threshold -> drag 시작
    func test_mouseDragged_aboveThreshold_startsADrag() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 100, y: 200)) // 100pt

        XCTAssertTrue(machine.isDragging)
        XCTAssertEqual(machine.state, .pointerDown, "dragging is not a new PetInteractionState — the pet stays visually pointerDown")
    }

    // 4. quick drag + mouseUp -> pointerUp, not click
    func test_mouseUp_afterAQuickButDraggedPress_isPointerUp_notClick() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 200, y: 100)) // well past threshold
        machine.mouseUp(at: t0.addingTimeInterval(0.05)) // well under the click-duration threshold

        XCTAssertEqual(machine.state, .pointerUp, "any drag must force pointerUp regardless of how short the press was")
    }

    // 5 & 6 (long no-drag -> pointerUp, quick no-drag -> click) are already
    // covered by the pre-Phase-3 tests above; re-asserted here for clarity
    // that they still hold with drag tracking now present.
    func test_mouseUp_longPressWithoutDragging_isStillPointerUp() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseUp(at: t0.addingTimeInterval(1))
        XCTAssertEqual(machine.state, .pointerUp)
    }

    func test_mouseUp_quickPressWithoutDragging_isStillClick() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseUp(at: t0.addingTimeInterval(0.05))
        XCTAssertEqual(machine.state, .click)
    }

    // 7. drag started, cursor drifts back near the origin -> drag state persists (latched)
    func test_isDragging_staysLatched_evenIfCursorReturnsNearTheOrigin() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 300, y: 100)) // far past threshold
        XCTAssertTrue(machine.isDragging)

        machine.mouseDragged(to: CGPoint(x: 101, y: 100)) // back within 5pt of the origin
        XCTAssertTrue(machine.isDragging, "once a drag starts it must not un-latch just because the cursor drifted back")

        machine.mouseUp(at: t0.addingTimeInterval(0.05))
        XCTAssertEqual(machine.state, .pointerUp, "the drag that happened earlier in the press still forces pointerUp")
    }

    // 8. mouseUp -> drag context reset
    func test_mouseUp_resetsIsDragging() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 300, y: 100))
        machine.mouseUp(at: t0.addingTimeInterval(0.05))

        XCTAssertFalse(machine.isDragging)
    }

    // 9. duplicate mouseDown resets drag context
    func test_duplicateMouseDown_resetsIsDragging() {
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseDragged(to: CGPoint(x: 300, y: 100))
        XCTAssertTrue(machine.isDragging)

        machine.mouseDown(at: t0.addingTimeInterval(0.1), location: CGPoint(x: 300, y: 100))
        XCTAssertFalse(machine.isDragging, "a fresh mouseDown must start a brand-new drag judgment, not inherit the previous press's latch")
    }

    // 10 (mouseUp without prior mouseDown is safe) already covered above;
    // additionally confirm a stray mouseDragged with no active pointerDown
    // is a safe no-op.
    func test_mouseDragged_withoutAnyMouseDown_isIgnored() {
        let machine = makeMachine()
        machine.mouseDragged(to: CGPoint(x: 500, y: 500)) // must not crash
        XCTAssertEqual(machine.state, .idle)
        XCTAssertFalse(machine.isDragging)
    }

    func test_mouseDragged_afterReactionAlreadyResolved_isIgnored() {
        // A stray mouseDragged arriving after mouseUp already resolved the
        // press (e.g. an out-of-order event) must not resurrect dragging.
        let machine = makeMachine()
        machine.mouseDown(at: t0, location: CGPoint(x: 100, y: 100))
        machine.mouseUp(at: t0.addingTimeInterval(0.05)) // -> click
        machine.mouseDragged(to: CGPoint(x: 500, y: 500))

        XCTAssertFalse(machine.isDragging)
        XCTAssertEqual(machine.state, .click)
    }
}
