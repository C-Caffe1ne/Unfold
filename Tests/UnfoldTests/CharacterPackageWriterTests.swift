import XCTest
@testable import Unfold

/// The writer's contract in one line: after it returns, the library either
/// holds a package `CharacterPackageLoader` can read, or it holds exactly
/// what it held before. There is no third outcome.
final class CharacterPackageWriterTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-writer-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func makePayload(
        frameCount: Int = 4,
        side: Int = 64,
        fps: Double = 12,
        characterID: String? = nil
    ) throws -> EditorSavePayload {
        let png = TestPNG.dataURL(width: side * frameCount, height: side)
        let idField = characterID.map { "\"characterID\": \"\($0)\"," } ?? ""
        return try EditorSavePayload.decode(from: """
        {
          "type": "save", \(idField)
          "width": \(side), "height": \(side),
          "fps": \(fps), "frameCount": \(frameCount),
          "sheetPNG": "\(png)",
          "piskelJSON": "{\\\"modelVersion\\\":2}"
        }
        """)
    }

    // MARK: - Round trip

    func test_write_producesPackageTheLoaderCanRead() throws {
        let payload = try makePayload()
        let character = try CharacterPackageWriter.write(payload: payload, name: "Mari", into: library)

        XCTAssertEqual(character.name, "Mari")
        // These two assertions belong together: `thumbnailSymbol` and
        // `renderStyle` are both `String?` in the manifest, so swapping their
        // values still compiles. Distinct values make that mistake fail here.
        XCTAssertEqual(character.renderStyle, .pixel)
        XCTAssertEqual(character.thumbnailSymbolName, "pawprint.fill")
        XCTAssertEqual(character.spriteSheet.columns, 4)
        XCTAssertEqual(character.spriteSheet.rows, 1)
        XCTAssertEqual(character.spriteSheet.frameWidth, 64)

        guard case .spriteSheet(let definition) = character.animation(for: .idle) else {
            return XCTFail("expected an idle sprite-sheet animation")
        }
        XCTAssertEqual(definition.frames, [0, 1, 2, 3])
        XCTAssertEqual(definition.fps, 12)
        XCTAssertTrue(definition.loop)

        // Re-reading through the public loader path proves the package is
        // genuinely well-formed, not just that `write` returned something.
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let reloaded = try CharacterPackageLoader.loadImported(packageDirectory: directory)
        XCTAssertEqual(reloaded.id, character.id)
    }

    func test_write_storesAllThreeFiles() throws {
        let character = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let names = Set(try FileManager.default.contentsOfDirectory(atPath: directory.path))
        XCTAssertEqual(names, ["character.json", "spritesheet.png", "source.piskel"])
    }

    func test_write_newCharacter_getsUserPrefixedID() throws {
        let character = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)
        XCTAssertTrue(character.id.hasPrefix("user-"), "got \(character.id)")
    }

    // MARK: - Overwrite

    func test_write_sameCharacterID_overwritesInPlace() throws {
        let first = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)

        let updated = try makePayload(frameCount: 2, characterID: first.id)
        let second = try CharacterPackageWriter.write(payload: updated, name: "Mari v2", into: library)

        XCTAssertEqual(second.id, first.id)
        XCTAssertEqual(second.name, "Mari v2")
        XCTAssertEqual(second.spriteSheet.columns, 2)
        XCTAssertEqual(library.packageDirectories().count, 1, "overwrite must not create a second package")
    }

    // MARK: - Failure leaves nothing behind

    func test_write_blankName_throwsAndWritesNothing() throws {
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: try makePayload(), name: "   ", into: library))
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_write_unsafeCharacterID_throwsAndWritesNothing() throws {
        let payload = try makePayload(characterID: "..")
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: payload, name: "Mari", into: library))
        XCTAssertEqual(library.packageDirectories(), [])
    }

    /// A failed write must not leave a staging directory lying around for
    /// the repository to trip over on the next launch.
    func test_write_failure_leavesNoStagingDirectory() throws {
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: try makePayload(), name: "", into: library))
        let entries = (try? FileManager.default.contentsOfDirectory(atPath: root.path)) ?? []
        XCTAssertEqual(entries, [], "expected an empty library root, found \(entries)")
    }
}
