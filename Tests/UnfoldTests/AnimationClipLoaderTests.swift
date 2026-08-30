import XCTest
@testable import Unfold

final class AnimationClipLoaderTests: XCTestCase {

    // MARK: - fps → frame duration

    func test_load_convertsIdleProductionFps_7fpsToFrameDuration() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [0], fps: 7, loop: true)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertEqual(clip.frames[0].duration, 1.0 / 7.0, accuracy: 0.0001)
    }

    func test_load_convertsStretchProductionFps_11fpsToFrameDuration() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [0], fps: 11, loop: false)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertEqual(clip.frames[0].duration, 1.0 / 11.0, accuracy: 0.0001)
    }

    func test_load_givesEveryFrameTheSameDuration() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 2, rows: 1, cells: [.red, .green])
        let animation = SpriteAnimationDefinition(frames: [0, 1, 0, 1], fps: 7, loop: true)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertEqual(clip.frames.map(\.duration), Array(repeating: 1.0 / 7.0, count: 4))
    }

    // MARK: - frame order

    func test_load_preservesFrameOrder_includingRepeatsAndSkips() throws {
        let sheet = TestSpriteSheet.makeSheet(
            columns: 2, rows: 2,
            cells: [.red, .green, .blue, .white]
        )
        // Cell layout (row-major): 0=red 1=green 2=blue 3=white.
        // Order deliberately skips index 1 and repeats index 3.
        let animation = SpriteAnimationDefinition(frames: [3, 0, 2, 3], fps: 7, loop: true)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        let colors = clip.frames.map { TestSpriteSheet.color(of: $0.image) }
        XCTAssertEqual(colors, [.white, .red, .blue, .white])
    }

    // MARK: - loop passthrough

    func test_load_passesThroughLoopTrue() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [0], fps: 7, loop: true)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertTrue(clip.loop)
    }

    func test_load_passesThroughLoopFalse() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [0], fps: 11, loop: false)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertFalse(clip.loop)
    }

    // MARK: - invalid duration defense
    //
    // Regression lock, not new behavior: `SpriteAnimationDefinition
    // .frameDuration` already guards fps <= 0 by returning
    // `.greatestFiniteMagnitude` instead of dividing by zero. Phase 2 leans
    // on that harder than before — a bad duration now feeds a real
    // `Timer(timeInterval:)` per frame — so it's worth locking in here too.

    func test_load_handlesZeroFps_withoutCrashing_byProducingAHugeDuration() throws {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [0], fps: 0, loop: true)

        let clip = try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)

        XCTAssertEqual(clip.frames[0].duration, .greatestFiniteMagnitude)
    }

    // MARK: - defensive errors

    func test_load_throwsNoFrames_whenAnimationDefinesNoFrames() {
        let sheet = TestSpriteSheet.makeSheet(columns: 1, rows: 1, cells: [.red])
        let animation = SpriteAnimationDefinition(frames: [], fps: 7, loop: true)

        XCTAssertThrowsError(try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)) { error in
            XCTAssertEqual(error as? AnimationClipLoader.LoadError, .noFrames)
        }
    }

    func test_load_throwsInvalidFrameIndex_whenAnimationReferencesOutOfRangeCell() {
        let sheet = TestSpriteSheet.makeSheet(columns: 2, rows: 1, cells: [.red, .green])
        let animation = SpriteAnimationDefinition(frames: [0, 5], fps: 7, loop: true)

        XCTAssertThrowsError(try AnimationClipLoader.load(spriteSheet: sheet, animation: animation)) { error in
            XCTAssertEqual(error as? AnimationClipLoader.LoadError, .invalidFrameIndex(5))
        }
    }
}
