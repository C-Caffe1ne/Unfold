import XCTest
@testable import Unfold

/// The editor runs untrusted-by-construction code (a vendored web app), so
/// every value it sends is re-checked here before anything touches disk.
/// These tests are the specification of that boundary.
final class EditorSavePayloadTests: XCTestCase {

    /// Valid by default; each test overrides exactly the field it's about.
    private func makeJSON(
        width: Int = 64,
        height: Int = 64,
        fps: String = "12",
        frameCount: Int = 4,
        sheetPNG: String? = nil,
        piskelJSON: String = "{\\\"modelVersion\\\":2}",
        characterID: String? = nil,
        type: String = "save"
    ) -> String {
        // `max(1,)`: the out-of-range cases below declare a 0-wide sheet, and
        // asking for a 0x0 image would fail inside TestPNG rather than in the
        // validation this test is about.
        let png = sheetPNG ?? TestPNG.dataURL(
            width: max(1, width * frameCount),
            height: max(1, height)
        )
        let idField = characterID.map { "\"characterID\": \"\($0)\"," } ?? ""
        return """
        {
          "type": "\(type)",
          \(idField)
          "width": \(width), "height": \(height),
          "fps": \(fps), "frameCount": \(frameCount),
          "sheetPNG": "\(png)",
          "piskelJSON": "\(piskelJSON)"
        }
        """
    }

    // MARK: - Happy path

    func test_decode_validMessage_producesPayload() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON())
        XCTAssertEqual(payload.width, 64)
        XCTAssertEqual(payload.height, 64)
        XCTAssertEqual(payload.frameCount, 4)
        XCTAssertEqual(payload.fps, 12)
        XCTAssertNil(payload.characterID)
        XCTAssertFalse(payload.sheetPNGData.isEmpty)
    }

    func test_decode_carriesCharacterIDWhenPresent() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(characterID: "user-abc"))
        XCTAssertEqual(payload.characterID, "user-abc")
    }

    // MARK: - Frame count bounds

    func test_decode_zeroFrames_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(frameCount: 0)))
    }

    func test_decode_oneFrame_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(frameCount: 1))
        XCTAssertEqual(payload.frameCount, 1)
    }

    func test_decode_maximumFrames_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(width: 8, height: 8, frameCount: 24))
        XCTAssertEqual(payload.frameCount, 24)
    }

    func test_decode_tooManyFrames_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 8, height: 8, frameCount: 25)))
    }

    // MARK: - Canvas size bounds

    func test_decode_maximumCanvas_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(width: 128, height: 128, frameCount: 1))
        XCTAssertEqual(payload.width, 128)
    }

    func test_decode_oversizedCanvas_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 129, height: 128, frameCount: 1)))
    }

    func test_decode_zeroCanvas_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 0, height: 64, frameCount: 1)))
    }

    // MARK: - fps

    func test_decode_negativeFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "-1")))
    }

    func test_decode_zeroFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "0")))
    }

    // MARK: - Sheet data

    func test_decode_wrongDataURLPrefix_isRejected() {
        let jpeg = "data:image/jpeg;base64," + TestPNG.data(width: 8, height: 8).base64EncodedString()
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: jpeg)))
    }

    func test_decode_notBase64_isRejected() {
        XCTAssertThrowsError(
            try EditorSavePayload.decode(from: makeJSON(sheetPNG: "data:image/png;base64,%%%not-base64%%%"))
        )
    }

    /// The declared grid is what the manifest will claim, so it has to match
    /// the image that actually decoded — otherwise a character would ship
    /// with a manifest that lies about its own sheet.
    func test_decode_pngSmallerThanDeclaredGrid_isRejected() {
        let tooSmall = TestPNG.dataURL(width: 64, height: 64)   // declared: 4 × 64 wide
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: tooSmall)))
    }

    // MARK: - Message shape

    func test_decode_wrongMessageType_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(type: "cancel")))
    }

    func test_decode_notJSON_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: "not json at all"))
    }

    func test_decode_emptyPiskelJSON_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(piskelJSON: "")))
    }
}

