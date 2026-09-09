import Foundation

/// Says, in one sentence a user can act on, what a write is about to throw
/// away.
///
/// This lives apart from the alert that shows it on purpose. The alert is
/// `NSAlert.runModal`, which no test can reach, and a string no test can
/// reach is a string that drifts away from the truth as the encoders change
/// underneath it. Every claim made here was measured against the encoder that
/// will make the write, so it belongs somewhere a test can hold it to that.
///
/// The copy is assembled per loss rather than written out per format, so a
/// document that loses two things is warned about both in one breath instead
/// of being warned about the first and finding out about the second on disk.
enum EditorWriteWarning {

    /// What writing this document as this format would lose, as a sentence
    /// to show the user, or nil when the write loses nothing.
    ///
    /// Nil is the answer for a lossless write, not an empty string, so a
    /// caller cannot accidentally show an empty alert: there is nothing to
    /// ask about when nothing is at stake.
    static func text(for document: PixelDocument, format: EditorFileFormat) -> String? {
        let sentences: [String]
        switch format {
        // The format the editor's own documents live in. Layers, frames and
        // per-layer opacity all survive it, so there is never anything to
        // warn about — that is the whole point of having it.
        case .unfoldSource:
            sentences = []
        case .png:
            sentences = pngSentences(for: document)
        case .gif:
            sentences = gifSentences(for: document)
        // Never written: `EditorFileFormat.canWrite` turns a JPEG
        // destination away before any of this is reached, so there is no
        // write here to describe.
        case .jpeg:
            sentences = []
        }
        return sentences.isEmpty ? nil : sentences.joined(separator: " ")
    }

    /// PNG holds exactly one image, so everything the document arranges
    /// *around* a single image — the timeline and the layer stack — has to be
    /// resolved on the way out. Neither loss shows in the resulting picture,
    /// which is what makes them worth saying: the file looks right, and only
    /// the reopening tells the user what it cost.
    ///
    /// The timeline is the delicate half, and the honest word for what
    /// happens to it is "reconstructed". Reopening a sheet does usually get
    /// the frames back — `ImportOptions.suggestion` spots a horizontal strip
    /// and offers to split it — but it is reading the proportions of an
    /// image, not a timeline stored in the file, and the guess it makes
    /// assumes square frames. Saying the animation is lost would be false;
    /// saying nothing would leave the user expecting the file to remember.
    /// The frame rate genuinely is gone: nothing in a PNG can carry it, so
    /// what reopens runs at whatever speed a fresh document starts at.
    private static func pngSentences(for document: PixelDocument) -> [String] {
        var sentences: [String] = []
        if document.frameCount > 1 {
            sentences.append("The \(document.frameCount) frames will be written side by side as one horizontal sprite sheet, and reopening the file asks how to slice the image back into frames rather than reading the timeline from it.")
            sentences.append("A PNG stores no frame rate, so a reopened sheet plays at the editor's default speed rather than this document's.")
        }
        if document.layers.count > 1 {
            sentences.append("The \(document.layers.count) layers will be flattened into one image.")
        }
        return sentences
    }

    /// GIF's losses are properties of the pixels, not of the document's
    /// shape, so unlike PNG they cannot be read off a frame count — the
    /// encoder is asked instead, and it answers from the composited frames.
    private static func gifSentences(for document: PixelDocument) -> [String] {
        let lossiness = AnimatedGIFEncoder.lossiness(of: document)
        guard !lossiness.isLossless else { return [] }
        var sentences: [String] = []
        if lossiness.partialAlpha {
            // Leading with the disappearing is deliberate, and it is the
            // measurement that decides the order: ImageIO rounds alpha at
            // 25% (64 of 255), writing anything below it as nothing at all
            // and everything at or above it fully opaque. A hard edge is
            // obvious the moment the file is opened and can be fixed; a soft
            // glow or an anti-aliased fringe silently going missing is the
            // outcome a user would not think to look for.
            sentences.append("Pixels less than about a quarter opaque will disappear, and every other partly transparent pixel will become fully opaque.")
        }
        if lossiness.colourReduction {
            // The capacity is interpolated rather than written into the
            // sentence, because it is a measured property of ImageIO's
            // encoder and the copy must move if the measurement ever does.
            sentences.append("A GIF shares one palette of \(AnimatedGIFEncoder.paletteCapacity) colours across every frame and this document has more, so some colours will shift to the nearest one that palette can hold.")
        }
        return sentences
    }
}
