import XCTest
@testable import Unfold

final class ImportOptionsTests: XCTestCase {

    private func image(width: Int, height: Int) -> RasterImageDecoder.Image {
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for i in pixels.indices { pixels[i] = UInt32(i % 251) << 24 | 0xFF }
        return RasterImageDecoder.Image(pixels: pixels, width: width, height: height)
    }

    // MARK: suggestion

    func test_squareImageWithinTheCap_isOpenedAsASingleFrame() {
        XCTAssertEqual(ImportOptions.suggestion(width: 64, height: 64), .single)
    }

    /// A ratio of exactly 1 is an integer multiple too — treating it as a
    /// sheet would send every square image through the dialog.
    func test_aRatioOfOneIsNotASheet() {
        XCTAssertEqual(ImportOptions.suggestion(width: 128, height: 128), .single)
    }

    func test_anExportedSheetIsSuggestedForSplitting() {
        XCTAssertEqual(ImportOptions.suggestion(width: 512, height: 64),
                       .split(frameWidth: 64, frameHeight: 64))
    }

    func test_aNonIntegerRatioIsNotASheet() {
        XCTAssertEqual(ImportOptions.suggestion(width: 100, height: 64), .single)
    }

    func test_anOversizedImageIsSuggestedForCropping() {
        guard case .crop(let rect) = ImportOptions.suggestion(width: 1920, height: 1080) else {
            return XCTFail("expected a crop suggestion")
        }
        XCTAssertEqual(rect.width, CGFloat(Constants.editorCanvasSideRange.upperBound))
        XCTAssertEqual(rect.height, CGFloat(Constants.editorCanvasSideRange.upperBound))
    }

    // MARK: apply

    func test_split_producesOneFramePerCell() throws {
        let document = try ImportOptions.apply(.split(frameWidth: 64, frameHeight: 64), to: image(width: 512, height: 64))
        XCTAssertEqual(document.width, 64)
        XCTAssertEqual(document.height, 64)
        XCTAssertEqual(document.frameCount, 8)
        XCTAssertEqual(document.layers.count, 1)
    }

    /// The round trip that fails today: export a sheet, reopen it.
    func test_split_reopensAnExportedSheet() throws {
        var original = PixelDocument(width: 64, height: 64)
        while original.frameCount < 8 { original.insertFrame(after: original.frameCount - 1, duplicate: false) }
        original.layers[0].frames[3].pixels[10] = 0xABCDEF12

        let sheet = try PixelDocumentCodec.sheetPNG(original)
        let decoded = try RasterImageDecoder.decode(sheet, format: .png, maximumSide: Constants.editorCanvasSideRange.upperBound)
        let reopened = try ImportOptions.apply(.split(frameWidth: 64, frameHeight: 64), to: decoded)

        XCTAssertEqual(reopened.frameCount, 8)
        XCTAssertEqual(reopened.layers[0].frames[3].pixels[10], 0xABCDEF12)
    }

    func test_split_rejectsAFrameSizeThatDoesNotDivideTheImage() {
        XCTAssertThrowsError(try ImportOptions.apply(.split(frameWidth: 30, frameHeight: 64), to: image(width: 512, height: 64)))
    }

    func test_split_rejectsMoreFramesThanTheDocumentAllows() {
        XCTAssertThrowsError(try ImportOptions.apply(.split(frameWidth: 8, frameHeight: 64), to: image(width: 512, height: 64)),
                             "64 frames exceeds the 24-frame maximum")
    }

    func test_crop_takesTheRequestedRegion() throws {
        let source = image(width: 1920, height: 1080)
        let document = try ImportOptions.apply(.crop(CGRect(x: 100, y: 50, width: 128, height: 128)), to: source)
        XCTAssertEqual(document.width, 128)
        XCTAssertEqual(document.height, 128)
        XCTAssertEqual(document.frameCount, 1)
        XCTAssertEqual(document.layers[0].frames[0].pixels[0], source.pixels[50 * 1920 + 100])
    }

    func test_crop_rejectsARegionOutsideTheImage() {
        XCTAssertThrowsError(try ImportOptions.apply(.crop(CGRect(x: 400, y: 0, width: 256, height: 64)), to: image(width: 512, height: 64)))
    }

    func test_single_rejectsAnImageBeyondTheCanvasCap() {
        XCTAssertThrowsError(try ImportOptions.apply(.single, to: image(width: 1920, height: 1080)))
    }

    func test_scaleToFit_bringsAnOversizedImageWithinTheCap() throws {
        let document = try ImportOptions.apply(.scaleToFit, to: image(width: 1920, height: 1080))
        XCTAssertLessThanOrEqual(document.width, Constants.editorCanvasSideRange.upperBound)
        XCTAssertLessThanOrEqual(document.height, Constants.editorCanvasSideRange.upperBound)
        XCTAssertEqual(document.width, 512)
        XCTAssertEqual(document.height, 288)
    }

    /// Splitting must not be able to build a document past the byte ceiling.
    func test_split_rejectsAResultOverTheByteCeiling() {
        // 512x512 cells across a 24-cell sheet would be 24 MB in one layer,
        // which is exactly the ceiling — one more row would exceed it.
        XCTAssertThrowsError(
            try ImportOptions.apply(.split(frameWidth: 512, frameHeight: 512),
                                    to: image(width: 512 * 24, height: 512 * 2)))
    }
}
