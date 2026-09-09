import XCTest
@testable import Unfold

final class EditorWriteWarningTests: XCTestCase {

    // MARK: - Builders

    /// Frames are added with a bounded `for`, never `while frameCount < n`:
    /// `insertFrame` refuses silently at the frame and byte ceilings, and a
    /// `while` written against a refusal spins forever instead of failing.
    private func makeDocument(frames: Int = 1, layers: Int = 1) -> PixelDocument {
        var document = PixelDocument(width: 8, height: 8)
        for _ in 1..<max(1, frames) {
            document.insertFrame(after: document.frameCount - 1, duplicate: false)
        }
        for index in 1..<max(1, layers) {
            document.layers.append(PixelLayer(name: "Layer \(index + 1)",
                                              frames: document.layers[0].frames))
        }
        return document
    }

    /// Opaque and distinct for every index, so a test can ask for "n
    /// different colours" without hand-picking any.
    private func colour(_ index: Int) -> UInt32 {
        UInt32((index % 256) << 24 | (index / 256) << 16 | 0xFF)
    }

    /// Fills the document with `count` distinct opaque colours, spilling
    /// across frames when one frame cannot hold them all.
    private func paint(_ document: inout PixelDocument, distinctColours count: Int) {
        let perFrame = document.width * document.height
        for index in 0..<count {
            let frame = index / perFrame
            guard frame < document.frameCount else { return }
            document.layers[0].frames[frame].pixels[index % perFrame] = colour(index)
        }
    }

    // MARK: - Nothing to say

    func test_gif_returnsNil_whenTheDocumentIsSomethingGIFCanCarryWhole() {
        var document = makeDocument()
        paint(&document, distinctColours: 4)
        XCTAssertNil(EditorWriteWarning.text(for: document, format: .gif))
    }

    /// The one format that stores everything, so it can never have anything
    /// to warn about — not even for a document every other format damages.
    func test_unfoldSource_returnsNil_forADocumentEveryOtherFormatWouldDamage() {
        var document = makeDocument(frames: 5, layers: 3)
        paint(&document, distinctColours: 300)
        document.layers[1].frames[0].pixels[0] = 0xFF_00_00_20
        XCTAssertNil(EditorWriteWarning.text(for: document, format: .unfoldSource))
    }

    // MARK: - GIF

    /// Asserts on the word that carries the *disappearing*, not on the whole
    /// sentence: re-wording the copy must stay free, dropping the meaning
    /// must not.
    func test_gif_warnsThatFaintPixelsDisappear_whenAlphaIsPartial() throws {
        var document = makeDocument()
        document.layers[0].frames[0].pixels[0] = 0xFF_00_00_20
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .gif))
        XCTAssertTrue(warning.lowercased().contains("disappear"), warning)
    }

    /// A half-opacity layer over opaque paint is partial alpha in the
    /// composite even though no stored pixel is, and the warning has to come
    /// from what gets written, not from what is stored.
    func test_gif_warnsAboutAlpha_whenALayerSitsBelowFullOpacity() throws {
        var document = makeDocument()
        paint(&document, distinctColours: 4)
        document.layers[0].opacity = 0.5
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .gif))
        XCTAssertTrue(warning.lowercased().contains("disappear"), warning)
    }

    func test_gif_namesThePaletteCapacity_whenTheDocumentExceedsIt() throws {
        var document = makeDocument(frames: 5)
        paint(&document, distinctColours: AnimatedGIFEncoder.paletteCapacity + 20)
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .gif))
        XCTAssertTrue(warning.contains("\(AnimatedGIFEncoder.paletteCapacity)"), warning)
    }

    func test_gif_reportsBothLossesInOneString_whenBothApply() throws {
        var document = makeDocument(frames: 5)
        paint(&document, distinctColours: AnimatedGIFEncoder.paletteCapacity + 20)
        document.layers[0].frames[0].pixels[63] = 0xFF_00_00_20
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .gif))
        XCTAssertTrue(warning.lowercased().contains("disappear"), warning)
        XCTAssertTrue(warning.contains("\(AnimatedGIFEncoder.paletteCapacity)"), warning)
    }

    // MARK: - PNG

    func test_png_namesTheFrameCount_whenThereIsMoreThanOneFrame() throws {
        let document = makeDocument(frames: 6)
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .png))
        XCTAssertTrue(warning.contains("6"), warning)
    }

    /// One frame and one layer is the shape a PNG already is, so the write
    /// changes nothing structural and there is nothing to say.
    func test_png_returnsNil_forASingleFrameOnASingleLayer() {
        var document = makeDocument()
        paint(&document, distinctColours: 4)
        XCTAssertNil(EditorWriteWarning.text(for: document, format: .png))
    }

    func test_png_warnsAboutFlattening_forOneFrameOnSeveralLayers() throws {
        let document = makeDocument(layers: 3)
        let warning = try XCTUnwrap(EditorWriteWarning.text(for: document, format: .png))
        XCTAssertTrue(warning.lowercased().contains("flatten"), warning)
    }
}
