import XCTest
@testable import Unfold

/// A character that only defines `idle` must still appear everywhere the
/// app asks for another clip — otherwise a user-made character (which is
/// only required to draw an idle loop) would vanish from the stretch
/// overlay, the one moment the app exists for.
final class CharacterAnimationFallbackTests: XCTestCase {

    private func makeCharacter(animations: [AnimationKey: AnimationSource]) -> Character {
        Character(
            id: "x",
            name: "X",
            thumbnailSymbolName: "pawprint.fill",
            spriteSheet: SpriteSheetDefinition(fileName: "s.png", columns: 2, rows: 1, frameWidth: 8, frameHeight: 8),
            animations: animations,
            source: .builtIn
        )
    }

    private var idleSource: AnimationSource {
        .spriteSheet(SpriteAnimationDefinition(frames: [0, 1], fps: 7, loop: true))
    }

    private var stretchSource: AnimationSource {
        .gif(fileName: "stretch.gif", loop: false)
    }

    func test_resolvedAnimation_exactKeyPresent_returnsThatKey() {
        let character = makeCharacter(animations: [.idle: idleSource, .stretch: stretchSource])
        XCTAssertEqual(character.resolvedAnimation(for: .stretch), stretchSource)
    }

    func test_resolvedAnimation_missingKey_fallsBackToIdle() {
        let character = makeCharacter(animations: [.idle: idleSource])
        XCTAssertEqual(character.resolvedAnimation(for: .stretch), idleSource)
    }

    func test_resolvedAnimation_missingKeyAndNoIdle_returnsNil() {
        let character = makeCharacter(animations: [.yawn: idleSource])
        XCTAssertNil(character.resolvedAnimation(for: .stretch))
    }

    /// `animation(for:)` stays an exact lookup — the fallback is a separate,
    /// explicit decision, not a change to what "does this character define
    /// this clip" means.
    func test_animationFor_staysExact() {
        let character = makeCharacter(animations: [.idle: idleSource])
        XCTAssertNil(character.animation(for: .stretch))
    }
}
