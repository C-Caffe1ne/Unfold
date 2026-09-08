import XCTest
import UniformTypeIdentifiers
@testable import Unfold

final class EditorFileFormatTests: XCTestCase {

    func test_readableFormats_excludeGIF_whichImportIsDeferredFor() {
        XCTAssertFalse(EditorFileFormat.readable.contains(.gif))
        XCTAssertTrue(EditorFileFormat.readable.contains(.unfoldSource))
        XCTAssertTrue(EditorFileFormat.readable.contains(.png))
        XCTAssertTrue(EditorFileFormat.readable.contains(.jpeg))
    }

    /// JPEG has no alpha channel and is lossy: exporting pixel art to it
    /// would fill transparent pixels black and blur pixel edges.
    func test_writableFormats_excludeJPEG() {
        XCTAssertFalse(EditorFileFormat.writable.contains(.jpeg))
        XCTAssertTrue(EditorFileFormat.writable.contains(.unfoldSource))
        XCTAssertTrue(EditorFileFormat.writable.contains(.png))
        XCTAssertTrue(EditorFileFormat.writable.contains(.gif))
    }

    func test_matching_isCaseInsensitive_andTreatsJPGAsJPEG() {
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "JPG"), .jpeg)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "jpeg"), .jpeg)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "UNF"), .unfoldSource)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "png"), .png)
    }

    func test_matching_returnsNil_forAnUnknownExtension() {
        XCTAssertNil(EditorFileFormat.matching(fileExtension: "bmp"))
        XCTAssertNil(EditorFileFormat.matching(fileExtension: ""))
    }

    /// `.piskel` was the document extension before the app had its own; the
    /// interop type was deliberately withdrawn, so it must not resolve to
    /// anything any more.
    func test_matching_returnsNil_forTheWithdrawnPiskelExtension() {
        XCTAssertNil(EditorFileFormat.matching(fileExtension: "piskel"))
    }

    func test_everyFormatHasANonEmptyExtensionAndDisplayName() {
        for format in EditorFileFormat.allCases {
            XCTAssertFalse(format.fileExtension.isEmpty, "\(format)")
            XCTAssertFalse(format.displayName.isEmpty, "\(format)")
        }
    }

    /// Only the editor's own format round-trips the full document (layers,
    /// frames, per-layer opacity). Writing PNG, GIF, or JPEG is an export —
    /// the in-memory document must not be considered saved afterward.
    func test_onlyUnfoldSource_preservesTheDocument() {
        XCTAssertTrue(EditorFileFormat.unfoldSource.preservesDocument)
        XCTAssertFalse(EditorFileFormat.png.preservesDocument)
        XCTAssertFalse(EditorFileFormat.gif.preservesDocument)
        XCTAssertFalse(EditorFileFormat.jpeg.preservesDocument)
    }
}
