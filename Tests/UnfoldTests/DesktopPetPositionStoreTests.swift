import XCTest
@testable import Unfold

/// Exercises `DesktopPetPositionStore` against an isolated `UserDefaults`
/// suite (never `.standard`) — save/load round-trip, and every "don't
/// crash, just fall back" corruption case §14/§17 of the Phase 3 spec asks
/// for.
final class DesktopPetPositionStoreTests: XCTestCase {

    private var defaults: UserDefaults!
    private var cleanup: (() -> Void)!

    override func setUpWithError() throws {
        (defaults, cleanup) = IsolatedUserDefaults.make()
    }

    override func tearDownWithError() throws {
        cleanup()
    }

    private func makeStore() -> DesktopPetPositionStore {
        DesktopPetPositionStore(defaults: defaults)
    }

    // MARK: - Round trip

    func test_load_returnsNil_whenNothingSaved() {
        XCTAssertNil(makeStore().load())
    }

    func test_save_thenLoad_roundTripsExactly() {
        let store = makeStore()
        let position = DesktopPetStoredPosition(screenIdentifier: 42, normalizedX: 0.75, normalizedY: 0.1)

        store.save(position)

        XCTAssertEqual(store.load(), position)
    }

    func test_save_persistsAcrossStoreInstances_sameDefaults() {
        // A fresh `DesktopPetPositionStore` reading the same `UserDefaults`
        // (simulating a relaunch) must see what an earlier instance saved.
        let position = DesktopPetStoredPosition(screenIdentifier: 7, normalizedX: 0.5, normalizedY: 0.5)
        makeStore().save(position)

        XCTAssertEqual(makeStore().load(), position)
    }

    func test_clear_removesASavedPosition() {
        let store = makeStore()
        store.save(DesktopPetStoredPosition(screenIdentifier: 1, normalizedX: 0.5, normalizedY: 0.5))

        store.clear()

        XCTAssertNil(store.load())
    }

    // MARK: - Invalid/corrupt stored values (§14/§17: never crash, fall back)

    func test_load_returnsNil_whenNormalizedXIsOutOfRange() {
        defaults.set(1, forKey: "desktopPetScreenIdentifier")
        defaults.set(1.5, forKey: "desktopPetNormalizedX") // out of 0...1
        defaults.set(0.5, forKey: "desktopPetNormalizedY")

        XCTAssertNil(makeStore().load())
    }

    func test_load_returnsNil_whenNormalizedYIsNegative() {
        defaults.set(1, forKey: "desktopPetScreenIdentifier")
        defaults.set(0.5, forKey: "desktopPetNormalizedX")
        defaults.set(-0.2, forKey: "desktopPetNormalizedY")

        XCTAssertNil(makeStore().load())
    }

    func test_load_returnsNil_whenScreenIdentifierHasTheWrongType() {
        defaults.set("not-a-number", forKey: "desktopPetScreenIdentifier")
        defaults.set(0.5, forKey: "desktopPetNormalizedX")
        defaults.set(0.5, forKey: "desktopPetNormalizedY")

        XCTAssertNil(makeStore().load())
    }

    func test_load_returnsNil_whenOnlyPartiallyPresent() {
        // e.g. a future/incompatible version wrote some but not all keys.
        defaults.set(1, forKey: "desktopPetScreenIdentifier")
        defaults.set(0.5, forKey: "desktopPetNormalizedX")
        // desktopPetNormalizedY deliberately missing.

        XCTAssertNil(makeStore().load())
    }

    func test_load_returnsNil_whenNormalizedValueIsNaN() {
        defaults.set(1, forKey: "desktopPetScreenIdentifier")
        defaults.set(Double.nan, forKey: "desktopPetNormalizedX")
        defaults.set(0.5, forKey: "desktopPetNormalizedY")

        XCTAssertNil(makeStore().load())
    }

    func test_load_acceptsBoundaryValuesZeroAndOne() {
        let position = DesktopPetStoredPosition(screenIdentifier: 3, normalizedX: 0, normalizedY: 1)
        makeStore().save(position)

        XCTAssertEqual(makeStore().load(), position)
    }
}
