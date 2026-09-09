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
        let image = try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .png)
        XCTAssertEqual(image.width, 4)
        XCTAssertEqual(image.height, 4)
        XCTAssertEqual(image.pixels.count, 16)
        XCTAssertEqual(image.pixels[0], 0xFF0000FF)
    }

    func test_decodesJPEG() throws {
        let image = try RasterImageDecoder.decode(try jpeg(width: 8, height: 8), format: .jpeg)
        XCTAssertEqual(image.width, 8)
        XCTAssertEqual(image.height, 8)
        // JPEG is lossy, so only the alpha channel is asserted exactly.
        XCTAssertEqual(image.pixels[0] & 255, 255)
    }

    /// A sprite sheet wider than the canvas cap must decode; the import
    /// dialog is what splits or crops it afterwards.
    func test_decodesASheetWiderThanTheCanvasCap() throws {
        let image = try RasterImageDecoder.decode(try png(width: 512, height: 64), format: .png)
        XCTAssertEqual(image.width, 512)
        XCTAssertEqual(image.height, 64)
    }

    /// The whole point of importing: an image far larger than any canvas has
    /// to decode, because cropping and scaling happen afterwards. Bounding
    /// this by the canvas would make cropping a photo impossible — which is
    /// exactly what it did before, leaving the crop path unreachable.
    func test_decodesAnImageLargerThanTheCanvasCap() throws {
        let side = Constants.editorCanvasSideRange.upperBound
        let image = try RasterImageDecoder.decode(try png(width: side * 2, height: side + 256), format: .png)
        XCTAssertEqual(image.width, side * 2)
        XCTAssertEqual(image.height, side + 256)
    }

    func test_rejectsAnImageOverThePixelBudget() throws {
        // Declared dimensions alone must be enough to refuse it — a
        // compressible bomb is a small file that expands enormously.
        let side = Int(Double(Constants.editorMaxImportPixels).squareRoot()) + 64
        XCTAssertThrowsError(try RasterImageDecoder.decode(try png(width: side, height: side), format: .png)) { error in
            XCTAssertTrue("\(error)".contains("too large to import"), "got: \(error)")
        }
    }

    func test_rejectsAnImageWithAPathologicalSide() throws {
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: Constants.editorMaxImportSide + 1, height: 1), format: .png))
    }

    func test_rejectsAPNGWithNoEndChunk() throws {
        var data = try png(width: 4, height: 4)
        data.removeLast(12)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .png))
    }

    func test_rejectsBytesThatAreNotTheDeclaredFormat() throws {
        let data = try png(width: 4, height: 4)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .jpeg))
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
        let image = try RasterImageDecoder.decode(data, format: detected)
        XCTAssertEqual(image.width, 8)
    }

    func test_rejectsANonRasterFormat() throws {
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .unfoldSource))
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .gif))
    }

    // MARK: preview thumbnails

    func test_thumbnail_reducesTheLongSideAndKeepsProportion() throws {
        let source = try RasterImageDecoder.decode(try png(width: 1024, height: 512), format: .png)
        let thumb = source.thumbnail(maxSide: 256)
        XCTAssertEqual(thumb.width, 256)
        XCTAssertEqual(thumb.height, 128)
        XCTAssertEqual(thumb.pixels.count, 256 * 128)
    }

    func test_thumbnail_leavesASmallImageAlone() throws {
        let source = try RasterImageDecoder.decode(try png(width: 64, height: 32), format: .png)
        XCTAssertEqual(source.thumbnail(maxSide: 256), source)
    }

    /// An extreme sprite sheet still produces a valid, tiny image rather than
    /// a zero-height one.
    func test_thumbnail_ofAVeryWideSheetKeepsAtLeastOneRow() throws {
        let source = RasterImageDecoder.Image(
            pixels: Array(repeating: 0xFF0000FF, count: 12_288 * 8), width: 12_288, height: 8)
        let thumb = source.thumbnail(maxSide: 256)
        XCTAssertEqual(thumb.width, 256)
        XCTAssertGreaterThanOrEqual(thumb.height, 1)
        XCTAssertEqual(thumb.pixels.count, thumb.width * thumb.height)
    }
}
