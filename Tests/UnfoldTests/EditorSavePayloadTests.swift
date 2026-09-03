import ImageIO
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

    func test_decode_oversizedHeight_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 128, height: 129, frameCount: 1)))
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

    func test_decode_maximumFPS_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(fps: "24"))
        XCTAssertEqual(payload.fps, 24)
    }

    func test_decode_excessiveFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "25")))
    }

    /// A runaway value must not reach `SpriteAnimationDefinition`, where
    /// `1 / fps` would become a near-zero timer interval.
    func test_decode_absurdFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "1e300")))
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

    func test_decode_pngLargerThanDeclaredGrid_isRejected() {
        let tooBig = TestPNG.dataURL(width: 512, height: 64)   // declared: 4 × 64 = 256 wide
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: tooBig)))
    }

    /// The geometry check would reject this eventually, but only after the
    /// whole string had been materialised and decoded. The size guard has
    /// to run first.
    func test_decode_oversizedSheetString_isRejected() {
        let huge = "data:image/png;base64," + String(repeating: "A", count: Constants.editorMaxSheetDataURLBytes + 1)
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: huge)))
    }

    /// A valid PNG header with the pixel data truncated so severely that
    /// `ImageIO` can't produce a `CGImage` from it at all — caught by the
    /// full-decode step in `decodedPixelSize`, before the `IEND` check ever
    /// runs.
    ///
    /// ImageIO's PNG decoder turns out to be lenient about truncation: a
    /// solid-color 256x64 test PNG here is ~568 bytes, and cutting it down
    /// to *half* that (284 bytes) still decodes a full 256x64 image — see
    /// `test_decode_truncatedPNGWithoutIEND_isRejected` below, which is
    /// exactly that case. Probing the cutoff directly found ImageIO only
    /// gives up somewhere around the 20% mark (~120 bytes) for this
    /// fixture. This test uses a fixed 40-byte prefix — comfortably below
    /// that boundary, leaving only the PNG signature and part of the IHDR
    /// chunk — which reliably fails to decode.
    func test_decode_truncatedPNG_isRejected() {
        let valid = TestPNG.data(width: 256, height: 64)
        let truncated = valid.prefix(40)
        let url = "data:image/png;base64," + truncated.base64EncodedString()
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: url)))
    }

    /// The case the header/decode checks alone let through: half the byte
    /// length of a valid PNG, which `CGImageSourceCreateImageAtIndex` still
    /// decodes to a full-sized, geometry-matching image (see the comment
    /// above) — so a decode-only check would have accepted this payload.
    /// It's missing its `IEND` chunk (truncated at 284 of 568 bytes, well
    /// before where `IEND` lives), so the `IEND` guard is what has to catch
    /// it.
    func test_decode_truncatedPNGWithoutIEND_isRejected() throws {
        let valid = TestPNG.data(width: 256, height: 64)
        let truncated = Data(valid.prefix(valid.count / 2))
        let source = try XCTUnwrap(CGImageSourceCreateWithData(truncated as CFData, nil))
        let image = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 0, nil))
        XCTAssertEqual(image.width, 256)
        XCTAssertEqual(image.height, 64)

        let pngEndChunk = Data([
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ])
        XCTAssertNil(truncated.range(of: pngEndChunk))

        let url = "data:image/png;base64," + truncated.base64EncodedString()
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: url))) { error in
            guard let decodingError = error as? EditorSavePayload.DecodingError else {
                XCTFail("Expected EditorSavePayload.DecodingError.truncatedPNG, got \(error)")
                return
            }
            guard case .truncatedPNG = decodingError else {
                XCTFail("Expected truncatedPNG, got \(decodingError)")
                return
            }
        }
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
