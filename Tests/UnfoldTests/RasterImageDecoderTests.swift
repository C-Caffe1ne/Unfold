import XCTest
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

final class RasterImageDecoderTests: XCTestCase {

    private func png(width: Int, height: Int, colour: UInt32 = 0xFF0000FF) throws -> Data {
        try PixelDocumentCodec.encodePNG(Array(repeating: colour, count: width * height),
                                         width: width, height: height)
    }

    private func jpeg(width: Int, height: Int) throws -> Data {
        let image = PixelDocumentCodec.image(Array(repeating: UInt32(0xFF0000FF), count: width * height),
                                             width: width, height: height)!
        let data = NSMutableData()
        let destination = CGImageDestinationCreateWithData(data, UTType.jpeg.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(destination, image, nil)
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        return data as Data
    }

    func test_decodesPNG_withItsPixelsIntact() throws {
        let image = try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .png, maximumSide: 512)
        XCTAssertEqual(image.width, 4)
        XCTAssertEqual(image.height, 4)
        XCTAssertEqual(image.pixels.count, 16)
        XCTAssertEqual(image.pixels[0], 0xFF0000FF)
    }

    func test_decodesJPEG() throws {
        let image = try RasterImageDecoder.decode(try jpeg(width: 8, height: 8), format: .jpeg, maximumSide: 512)
        XCTAssertEqual(image.width, 8)
        XCTAssertEqual(image.height, 8)
        // JPEG is lossy, so only the alpha channel is asserted exactly.
        XCTAssertEqual(image.pixels[0] & 255, 255)
    }

    /// A sprite sheet wider than the canvas cap must decode; the import
    /// dialog is what splits or crops it afterwards.
    func test_decodesASheetWiderThanTheCanvasCap() throws {
        let image = try RasterImageDecoder.decode(try png(width: 512, height: 64), format: .png, maximumSide: 512)
        XCTAssertEqual(image.width, 512)
        XCTAssertEqual(image.height, 64)
    }

    func test_rejectsAnImageTallerThanTheGivenMaximumSide() throws {
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 8, height: 600), format: .png, maximumSide: 512))
    }

    func test_rejectsAPNGWithNoEndChunk() throws {
        var data = try png(width: 4, height: 4)
        data.removeLast(12)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .png, maximumSide: 512))
    }

    func test_rejectsBytesThatAreNotTheDeclaredFormat() throws {
        let data = try png(width: 4, height: 4)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .jpeg, maximumSide: 512))
    }

    // MARK: content wins over the file name

    func test_detectFormat_readsTheBytes_notTheFileName() throws {
        XCTAssertEqual(RasterImageDecoder.detectFormat(try png(width: 4, height: 4)), .png)
        XCTAssertEqual(RasterImageDecoder.detectFormat(try jpeg(width: 8, height: 8)), .jpeg)
    }

    func test_detectFormat_returnsNil_forNonRasterBytes() {
        XCTAssertNil(RasterImageDecoder.detectFormat(Data("{\"modelVersion\":2}".utf8)))
        XCTAssertNil(RasterImageDecoder.detectFormat(Data()))
    }

    /// A JPEG saved with a .png name must still open, as JPEG.
    func test_decodesAMislabelledFile_byItsContent() throws {
        let data = try jpeg(width: 8, height: 8)
        let detected = try XCTUnwrap(RasterImageDecoder.detectFormat(data))
        XCTAssertEqual(detected, .jpeg)
        let image = try RasterImageDecoder.decode(data, format: detected, maximumSide: 512)
        XCTAssertEqual(image.width, 8)
    }

    func test_rejectsANonRasterFormat() throws {
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .unfoldSource, maximumSide: 512))
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .gif, maximumSide: 512))
    }
}
