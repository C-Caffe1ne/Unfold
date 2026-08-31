import XCTest
@testable import Unfold

/// `DesktopPetAnimationController` is the seam between
/// `PetInteractionStateMachine` (pure state/timing decisions) and
/// `SpriteAnimator` (playback) — it owns which `SpriteAnimator` is
/// currently on screen and swaps it as the state machine transitions.
/// Exercised against the real, unmodified `default-cat` idle animation
/// (same production asset `DefaultCatIdleAnimationIntegrationTests` uses),
/// driving `SpriteAnimator.advance()` directly for determinism instead of
/// waiting on real Timers — same technique `SpriteAnimatorTests` uses.
@MainActor
final class DesktopPetAnimationControllerTests: XCTestCase {

    private func makeController() throws -> DesktopPetAnimationController {
        let character = try XCTUnwrap(CharacterPackageLoader.loadBuiltIn(id: "default-cat"))
        return try XCTUnwrap(DesktopPetAnimationController(character: character))
    }

    private let t0 = Date(timeIntervalSince1970: 1_700_000_000)

    // MARK: - Construction

    func test_init_succeeds_forDefaultCat_andStartsIdle() throws {
        let controller = try makeController()
        XCTAssertEqual(controller.interactionState, .idle)
        XCTAssertNotNil(controller.animator.currentFrame)
    }

    func test_init_returnsNil_whenCharacterHasNoIdleAnimation() throws {
        // A character that defines `stretch` but not `idle` — the
        // controller needs idle up front to build both the idle animator
        // and the Phase 2 placeholder reaction clip (see its doc comment).
        let tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-pet-anim-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: tempDir) }

        let json = """
        {
          "id": "no-idle", "name": "NoIdle", "version": 1,
          "spriteSheet": {"file": "s.png", "columns": 8, "rows": 3, "frameWidth": 384, "frameHeight": 384},
          "animations": { "stretch": {"frames": [8,9], "fps": 11, "loop": false} }
        }
        """
        try json.write(to: tempDir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)

        let character = try CharacterPackageLoader.loadImported(packageDirectory: tempDir)
        XCTAssertNil(DesktopPetAnimationController(character: character))
    }

    // MARK: - pointerDown

    func test_mouseDown_transitionsToPointerDown_andSwapsToAFreshAnimator() throws {
        let controller = try makeController()
        let idleAnimator = controller.animator

        controller.mouseDown(at: t0, location: .zero)

        XCTAssertEqual(controller.interactionState, .pointerDown)
        XCTAssertFalse(controller.animator === idleAnimator, "pointerDown must play its own animator, not keep animating the idle one")
    }

    func test_pointerDown_holdsLastFrame_whenReactionFinishesWhileMouseStillDown() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)

        // Drive the reaction clip (8 idle frames, non-looping) to
        // completion without ever calling mouseUp.
        for _ in 0..<8 { controller.animator.advance() }

        XCTAssertTrue(controller.animator.isFinished)
        XCTAssertEqual(controller.interactionState, .pointerDown, "must hold, not fall back to idle on its own")

        // Further advances (a stray timer firing again) must not crash or
        // move anything further.
        controller.animator.advance()
        XCTAssertTrue(controller.animator.isFinished)
        XCTAssertEqual(controller.interactionState, .pointerDown)
    }

    // MARK: - click

    func test_quickMouseUp_transitionsToClick_withItsOwnAnimator() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        let pointerDownAnimator = controller.animator

        controller.mouseUp(at: t0.addingTimeInterval(0.1))

        XCTAssertEqual(controller.interactionState, .click)
        XCTAssertFalse(controller.animator === pointerDownAnimator)
    }

    func test_click_returnsToIdle_onceItsReactionFinishes() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        controller.mouseUp(at: t0.addingTimeInterval(0.1)) // -> click

        for _ in 0..<8 { controller.animator.advance() } // finishes the reaction

        XCTAssertEqual(controller.interactionState, .idle)
        // Back in idle: it must loop, never finish, exactly like Phase 1.
        for _ in 0..<40 { controller.animator.advance() }
        XCTAssertFalse(controller.animator.isFinished)
    }

    // MARK: - pointerUp

    func test_longMouseUp_transitionsToPointerUp_withItsOwnAnimator() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        let pointerDownAnimator = controller.animator

        controller.mouseUp(at: t0.addingTimeInterval(1))

        XCTAssertEqual(controller.interactionState, .pointerUp)
        XCTAssertFalse(controller.animator === pointerDownAnimator)
    }

    func test_pointerUp_returnsToIdle_onceItsReactionFinishes() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        controller.mouseUp(at: t0.addingTimeInterval(1)) // -> pointerUp

        for _ in 0..<8 { controller.animator.advance() }

        XCTAssertEqual(controller.interactionState, .idle)
    }

    // MARK: - Drag (Phase 3)

    func test_mouseDragged_doesNotCreateANewAnimator_whileHeld() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        let pointerDownAnimator = controller.animator

        controller.mouseDragged(to: CGPoint(x: 500, y: 500))
        controller.mouseDragged(to: CGPoint(x: 505, y: 495))
        controller.mouseDragged(to: CGPoint(x: 300, y: 700))

        XCTAssertTrue(controller.animator === pointerDownAnimator, "dragging must never swap out the currently-playing pointerDown reaction")
        XCTAssertEqual(controller.interactionState, .pointerDown, "dragging is not a new PetInteractionState")
    }

    func test_isDragging_reflectsTheStateMachine() throws {
        let controller = try makeController()
        XCTAssertFalse(controller.isDragging)

        controller.mouseDown(at: t0, location: .zero)
        XCTAssertFalse(controller.isDragging)

        controller.mouseDragged(to: CGPoint(x: 500, y: 500))
        XCTAssertTrue(controller.isDragging)
    }

    func test_mouseUp_afterADrag_isPointerUp_notClick_evenIfQuick() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        controller.mouseDragged(to: CGPoint(x: 500, y: 500))

        controller.mouseUp(at: t0.addingTimeInterval(0.05)) // well under click threshold

        XCTAssertEqual(controller.interactionState, .pointerUp)
    }

    // MARK: - Repeated input safety (mirrors PetInteractionStateMachineTests, through the real animator swap)

    func test_newMouseDown_whileClickReactionStillPlaying_cancelsItAndStartsFreshPointerDown() throws {
        let controller = try makeController()
        controller.mouseDown(at: t0, location: .zero)
        controller.mouseUp(at: t0.addingTimeInterval(0.1)) // -> click, reaction in flight
        let clickAnimator = controller.animator

        controller.mouseDown(at: t0.addingTimeInterval(0.15), location: .zero)

        XCTAssertEqual(controller.interactionState, .pointerDown)
        XCTAssertFalse(controller.animator === clickAnimator)

        // The abandoned click animator's completion must not resurrect it —
        // it's simply not `controller.animator` anymore, so driving it
        // further (simulating a stray timer callback) must not disturb the
        // now-current pointerDown state.
        for _ in 0..<8 { clickAnimator.advance() }
        XCTAssertEqual(controller.interactionState, .pointerDown)
    }

    func test_mouseUp_withoutPriorMouseDown_isSafe() throws {
        let controller = try makeController()
        controller.mouseUp(at: t0) // must not crash
        XCTAssertEqual(controller.interactionState, .idle)
    }
}
