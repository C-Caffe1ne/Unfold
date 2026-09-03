import XCTest
@testable import Unfold

/// One corrupt package on disk must cost the user that one character — not
/// the whole list, and not a launch.
final class ImportedCharacterRepositoryTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-repo-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
        try library.createRootIfNeeded()
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func writeValidPackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try """
        {
          "id": "\(id)", "name": "\(id)", "version": 1, "renderStyle": "pixel",
          "spriteSheet": {"file": "spritesheet.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 12, "loop": true} }
        }
        """.write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
        try TestPNG.data(width: 16, height: 8).write(to: dir.appendingPathComponent("spritesheet.png"))
    }

    private func writeBrokenPackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try "{ not a manifest".write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
    }

    func test_characters_emptyLibrary_isEmpty() {
        XCTAssertEqual(ImportedCharacterRepository(library: library).characters(), [])
    }

    func test_characters_loadsValidPackages() throws {
        try writeValidPackage(id: "user-a")
        let characters = ImportedCharacterRepository(library: library).characters()
        XCTAssertEqual(characters.map(\.id), ["user-a"])
        XCTAssertEqual(characters.first?.renderStyle, .pixel)
    }

    func test_characters_skipsBrokenPackagesAndKeepsTheRest() throws {
        try writeValidPackage(id: "user-a")
        try writeBrokenPackage(id: "user-broken")
        try writeValidPackage(id: "user-c")

        XCTAssertEqual(ImportedCharacterRepository(library: library).characters().map(\.id), ["user-a", "user-c"])
    }

    @MainActor
    func test_reloadCatalog_refreshesCurrentCharacterWhenEditedPackageKeepsItsID() {
        let isolatedDefaults = IsolatedUserDefaults.make()
        defer { isolatedDefaults.cleanup() }

        let original = makeCharacter(id: "user-a", name: "Original Name")
        let edited = makeCharacter(id: "user-a", name: "Edited Name")
        let repository = MutableCharacterRepository(characters: [original])
        let settings = SettingsStore(defaults: isolatedDefaults.defaults)
        settings.selectedCharacterID = original.id
        let manager = CharacterManager(repository: repository, settings: settings)

        repository.storedCharacters = [edited]
        manager.reloadCatalog()

        XCTAssertEqual(manager.current.name, "Edited Name")
        XCTAssertEqual(manager.availableCharacters.first?.name, "Edited Name")
    }

    private func makeCharacter(id: String, name: String) -> Character {
        Character(
            id: id,
            name: name,
            thumbnailSymbolName: "person.crop.square",
            spriteSheet: SpriteSheetDefinition(
                fileName: "spritesheet.png",
                columns: 1,
                rows: 1,
                frameWidth: 8,
                frameHeight: 8
            ),
            animations: [:],
            source: .builtIn
        )
    }
}

private final class MutableCharacterRepository: CharacterRepository {
    var storedCharacters: [Character]

    init(characters: [Character]) {
        self.storedCharacters = characters
    }

    func characters() -> [Character] {
        storedCharacters
    }
}
