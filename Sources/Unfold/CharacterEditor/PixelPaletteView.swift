import AppKit
import SwiftUI

/// The document palette, in the left strip under the tools: an import button
/// above, a two-column grid of swatches ending in the button that adds one,
/// and the destructive controls below.
@MainActor
struct PixelPaletteView: View {
    @ObservedObject var model: PixelEditorModel
    /// Which swatches are picked. Held by the editor so the rest of the
    /// window can clear it when the document underneath changes.
    @Binding var selection: Set<Int>
    @Binding var importError: String?

    /// Where a shift-click measures its range from: the last swatch chosen
    /// without shift, not the lowest index in the selection.
    @State private var anchor: Int?
    @State private var drag: Drag?

    private static let cellWidth: CGFloat = 48
    private static let cellHeight: CGFloat = 30

    /// What a drag picked up. The token rejects a drop that started somewhere
    /// else, and the palette snapshot one aimed at an order that has since
    /// changed.
    private struct Drag {
        let token: String
        let index: Int
        let palette: [UInt32]
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("PALETTE").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
            Button("Import GPL…", action: importPalette)
                .frame(maxWidth: .infinity)
                .pixelTooltip(PixelHelp.text("Import GPL",
                    "Replaces the whole palette from a GIMP .gpl file, up to 256 colors and 1 MiB."))
            grid
            HStack {
                Button("Update", action: updateSelected).disabled(selection.count != 1)
                    .pixelTooltip(PixelHelp.text("Update Swatch",
                        "Replaces the selected swatch with the current foreground color. One swatch at a time."))
                Spacer()
                Button(action: deleteSelected) { Image(systemName: "trash") }
                    .disabled(selection.isEmpty)
                    .pixelTooltip(PixelHelp.text("Delete Swatches",
                        "Removes every selected swatch. A palette is never emptied, so the last one stays."))
                    .accessibilityLabel("Delete selected swatches")
            }
        }
    }

    private var grid: some View {
        LazyVGrid(columns: Array(repeating: GridItem(.fixed(Self.cellWidth), spacing: 6), count: 2), spacing: 6) {
            ForEach(model.document.palette.indices, id: \.self) { index in
                swatch(index)
            }
            addButton
        }
    }

    private func swatch(_ index: Int) -> some View {
        Rectangle()
            .fill(color(model.document.palette[index]))
            .frame(width: Self.cellWidth, height: Self.cellHeight)
            .overlay(Rectangle().strokeBorder(
                selection.contains(index) ? Color.orange : Color.secondary.opacity(0.5),
                lineWidth: selection.contains(index) ? 2 : 1))
            .contentShape(Rectangle())
            .onTapGesture { tap(index) }
            .pixelTooltip(PixelHelp.text("Swatch \(index + 1)",
                String(format: "#%08X", model.document.palette[index])
                    + " RGBA. Click to draw with it, ⌘-click to add it to the selection, ⇧-click for a range, and drag it to reorder."))
            .accessibilityLabel("Swatch \(index + 1)")
            .onDrag { beginDrag(index) }
            .dropDestination(for: String.self) { values, _ in drop(values, at: index) }
    }

    private var addButton: some View {
        Button(action: addSwatch) {
            Image(systemName: "plus")
                .frame(width: Self.cellWidth, height: Self.cellHeight)
                .overlay(Rectangle().strokeBorder(Color.secondary.opacity(0.5), style: StrokeStyle(lineWidth: 1, dash: [3, 2])))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(model.document.palette.count >= PixelPalette.maximumColors)
        .pixelTooltip(PixelHelp.text("Add Swatch",
            "Appends the current foreground color to the palette, up to 256 colors."))
        .accessibilityLabel("Add swatch")
    }

    /// `NSEvent` is read rather than gesture modifiers because the plain tap
    /// and its modified variants have to stay one gesture: split apart, the
    /// unmodified one wins the race and a ⌘-click selects a single swatch.
    private func tap(_ index: Int) {
        let flags = NSEvent.modifierFlags
        if flags.contains(.command) {
            if selection.contains(index) { selection.remove(index) } else { selection.insert(index) }
            anchor = index
        } else if flags.contains(.shift), let start = anchor {
            selection = Set(min(start, index)...max(start, index))
        } else {
            selection = [index]
            anchor = index
            model.color = model.document.palette[index]
        }
    }

    private func addSwatch() {
        model.addPaletteColor(model.color)
        let last = model.document.palette.count - 1
        selection = [last]
        anchor = last
    }

    private func updateSelected() {
        guard let index = selection.first, selection.count == 1 else { return }
        model.updatePaletteColor(at: index, color: model.color)
    }

    private func deleteSelected() {
        model.removePaletteColors(at: selection)
        selection = []
        anchor = nil
    }

    private func beginDrag(_ index: Int) -> NSItemProvider {
        let token = UUID().uuidString
        drag = Drag(token: token, index: index, palette: model.document.palette)
        return NSItemProvider(object: token as NSString)
    }

    private func drop(_ values: [String], at index: Int) -> Bool {
        guard let source = drag, values == [source.token],
              source.palette == model.document.palette, source.index != index
        else { return false }
        drag = nil
        model.movePaletteColor(from: source.index, to: index)
        // Every other index has shifted under the move, so rebuilding the
        // selection around the swatch that moved beats mapping the old one.
        selection = [index]
        anchor = index
        return true
    }

    private func importPalette() {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        panel.title = "Import GIMP Palette (.gpl)"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let handle = try FileHandle(forReadingFrom: url)
            defer { try? handle.close() }
            let data = try handle.read(upToCount: 1_048_577) ?? Data()
            guard data.count <= 1_048_576, let text = String(data: data, encoding: .utf8) else {
                importError = "Choose a UTF-8 GPL file no larger than 1 MiB."
                return
            }
            try model.importGPL(text)
            selection = []
            anchor = nil
        } catch { importError = error.localizedDescription }
    }

    private func color(_ rgba: UInt32) -> Color {
        Color(.sRGB, red: Double((rgba >> 24) & 255) / 255, green: Double((rgba >> 16) & 255) / 255,
              blue: Double((rgba >> 8) & 255) / 255, opacity: Double(rgba & 255) / 255)
    }
}
