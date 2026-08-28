import SwiftUI

/// Renders a character's thumbnail. Backed by an SF Symbol today
/// (`Character.thumbnailSymbolName`), but callers — the Settings picker
/// included — only ever see this view, never the symbol name itself, so a
/// future `thumbnail.png`-based character only has to change this one
/// view's body, not every place a thumbnail is shown.
struct CharacterThumbnailView: View {
    let character: Character
    var size: CGFloat = 28

    var body: some View {
        Image(systemName: character.thumbnailSymbolName)
            .font(.system(size: size * 0.6))
            .symbolRenderingMode(.hierarchical)
            .frame(width: size, height: size)
    }
}
