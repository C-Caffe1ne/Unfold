import AppKit
import SwiftUI

@MainActor
struct PixelEditorView: View {
    @ObservedObject var model: PixelEditorModel
    let saveToLibrary: () -> Void
    let openDocument: () -> Void
    let save: () -> Void
    let saveAs: () -> Void
    @State private var resizeWidth = 64
    @State private var resizeHeight = 64
    @State private var showResize = false
    @State private var confirmCrop = false
    @State private var paletteError: String?
    @State private var paletteIndex: Int?

    var body: some View {
        VStack(spacing: 0) {
            toolbar
            Divider()
            HStack(spacing: 0) {
                tools
                Divider()
                VStack(spacing: 0) {
                    canvas
                    Divider()
                    timeline
                }
                Divider()
                inspector
            }
            Divider()
            HStack {
                Text("\(model.document.width) × \(model.document.height) px")
                Text("·  Frame \(model.selectedFrame + 1) / \(model.document.frameCount)")
                Spacer()
                Text(model.isDirty ? "Unsaved changes" : "Saved")
                Text("·  \(model.tool.title)")
            }
            .font(.caption).foregroundStyle(.secondary).padding(.horizontal, 14).padding(.vertical, 8)
        }
        .background(Color(nsColor: .windowBackgroundColor))
        .frame(minWidth: 900, minHeight: 620)
        .sheet(isPresented: $showResize) { resizeSheet }
        .alert("Palette import failed", isPresented: Binding(get: { paletteError != nil }, set: { if !$0 { paletteError = nil } })) { Button("OK") { paletteError = nil } } message: { Text(paletteError ?? "") }
        .task(id: model.isPlaying) {
            while model.isPlaying && !Task.isCancelled {
                model.tickPlayback(at: Date().timeIntervalSinceReferenceDate)
                try? await Task.sleep(nanoseconds: 16_666_667)
            }
        }
    }

    private var toolbar: some View {
        HStack(spacing: 12) {
            Image(systemName: "square.grid.3x3.fill").foregroundStyle(.orange)
            Text("Pixel Editor").font(.headline)
            Divider().frame(height: 22)
            Button(action: model.undo) { Image(systemName: "arrow.uturn.backward") }
                .help("Undo (⌘Z)").keyboardShortcut("z").disabled(!model.canUndo)
            Button(action: model.redo) { Image(systemName: "arrow.uturn.forward") }
                .help("Redo (⇧⌘Z)").keyboardShortcut("z", modifiers: [.command, .shift]).disabled(!model.canRedo)
            Menu("File") {
                Button("Open…", action: openDocument).keyboardShortcut("o")
                Divider()
                Button("Save", action: save)
                Button("Save As…", action: saveAs)
                    .keyboardShortcut("s", modifiers: [.command, .shift])
                Divider()
                Button(Strings.Editor.saveButton, action: saveToLibrary)
            }.frame(width: 70)
            Button("Canvas Size…") {
                resizeWidth = model.document.width
                resizeHeight = model.document.height
                showResize = true
            }
            Spacer()
            Button("Save", action: save)
                .keyboardShortcut("s").buttonStyle(.borderedProminent).tint(.orange)
        }.padding(12)
    }

    private var tools: some View {
        ScrollView {
        VStack(spacing: 8) {
            ForEach(PixelTool.allCases) { tool in
                Button { model.tool = tool } label: {
                    Image(systemName: tool.symbol)
                        .font(.system(size: 18))
                        .frame(width: 38, height: 34)
                        .background(model.tool == tool ? Color.orange.opacity(0.25) : Color.clear)
                        .clipShape(RoundedRectangle(cornerRadius: 6))
                }.buttonStyle(.plain).help(tool.title + (tool.shortcutLabel.map { " (" + $0 + ")" } ?? "")).accessibilityLabel(tool.title)
            }
            Divider()
            ColorPicker("Color", selection: Binding(get: { color(model.color) }, set: { model.color = rgba($0) }), supportsOpacity: true)
                .labelsHidden().help("Drawing color")
            Spacer()
        }.padding(10)
        }.frame(width: 62)
    }

    private var canvas: some View {
        VStack(spacing: 0) {
            HStack {
                Toggle("Grid", isOn: $model.showGrid)
                Toggle("Onion Skin", isOn: $model.onionSkin)
                Spacer()
                Button(action: model.zoomOut) { Image(systemName: "minus.magnifyingglass") }
                    .disabled(!model.canZoomOut).help("Zoom out")
                Text("\(model.zoomPercent)%").monospacedDigit().frame(width: 55)
                Button(action: model.zoomIn) { Image(systemName: "plus.magnifyingglass") }
                    .disabled(!model.canZoomIn).help("Zoom in")
            }.font(.caption).toggleStyle(.checkbox).padding(10)
            GeometryReader { geometry in
                ScrollView([.horizontal, .vertical]) {
                    NativePixelCanvas(model: model)
                        .frame(width: CGFloat(model.document.width * model.zoom), height: CGFloat(model.document.height * model.zoom))
                        .padding(24)
                        .frame(minWidth: geometry.size.width, minHeight: geometry.size.height)
                }
            }.background(Color(nsColor: .underPageBackgroundColor))
        }
    }

    private var timeline: some View {
        PixelTimelineView(model: model).frame(height: 220)
    }

    private var inspector: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("PREVIEW").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                thumbnail(frame: model.selectedFrame).frame(width: 180, height: 160)
                HStack {
                    Button { model.togglePlayback(at: Date().timeIntervalSinceReferenceDate) } label: {
                        Label(model.isPlaying ? "Pause" : "Play", systemImage: model.isPlaying ? "pause.fill" : "play.fill")
                    }
                    Spacer()
                    Text("\(Int(model.document.fps)) FPS").monospacedDigit()
                }
                Slider(value: Binding(get: { model.document.fps }, set: { value in model.change { $0.fps = value } }), in: 1...24, step: 1)
                    .accessibilityLabel("Animation speed")
                PixelPlaybackControls(model: model)
                Divider()
                ColorPicker("Foreground", selection: Binding(get: { color(model.color) }, set: { model.color = rgba($0) }))
                ColorPicker("Background", selection: Binding(get: { color(model.backgroundColor) }, set: { model.backgroundColor = rgba($0) }))
                Toggle("Mirror X", isOn: $model.symmetryX)
                Toggle("Mirror Y", isOn: $model.symmetryY)
                Toggle("Pixel-perfect pencil", isOn: $model.pixelPerfect)
                Toggle("Darken brightness brush", isOn: $model.darken)
                Stepper("Brush: \(model.brushSize) px", value: $model.brushSize, in: 1...8)
                palette
                HStack {
                    Button { model.flip(horizontal: true) } label: { Image(systemName: "arrow.left.and.right.righttriangle.left.righttriangle.right") }
                        .help("Flip current layer frame horizontally")
                    Button { model.flip(horizontal: false) } label: { Image(systemName: "arrow.up.and.down.righttriangle.up.righttriangle.down") }
                        .help("Flip current layer frame vertically")
                    Button("Clear", action: model.clearFrame).help("Clear current layer frame (undoable)")
                }
                Divider()
                layers
            }.padding(14)
        }.frame(width: 212)
    }

    private var palette: some View {
        VStack(alignment: .leading) {
            Text("PALETTE").font(.caption.weight(.semibold))
            LazyVGrid(columns: Array(repeating: GridItem(.fixed(24)), count: 6), spacing: 6) {
                ForEach(model.document.palette.indices, id: \.self) { index in
                    Button { paletteIndex = index; model.color = model.document.palette[index] } label: {
                        Rectangle().fill(color(model.document.palette[index])).frame(width: 24, height: 24)
                            .border(paletteIndex == index ? Color.orange : Color.secondary)
                    }.buttonStyle(.plain).help(String(format: "#%08X", model.document.palette[index]))
                }
            }
            HStack {
                Button("Add") { model.addPaletteColor(model.color) }
                Button("Update") { if let i = paletteIndex { model.updatePaletteColor(at: i, color: model.color) } }.disabled(paletteIndex == nil)
            }
            HStack {
                Button("Delete") { if let i = paletteIndex { model.removePaletteColor(at: i); paletteIndex = nil } }.disabled(paletteIndex == nil)
                Button("←") { moveSwatch(-1) }.help("Move swatch earlier")
                Button("→") { moveSwatch(1) }.help("Move swatch later")
            }
            Button("Import GPL…", action: importPalette)
        }
    }

    private func moveSwatch(_ delta: Int) {
        guard let index = paletteIndex, model.document.palette.indices.contains(index + delta) else { return }
        model.movePaletteColor(from: index, to: index + delta)
        paletteIndex = index + delta
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
                paletteError = "Choose a UTF-8 GPL file no larger than 1 MiB."
                return
            }
            try model.importGPL(text)
            paletteIndex = nil
        } catch { paletteError = error.localizedDescription }
    }

    private var layers: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("LAYERS").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                Spacer()
                Button(action: model.addLayer) { Image(systemName: "plus") }.help("Add layer")
                    .disabled(model.document.layers.count >= PixelDocument.maximumLayers)
                Button(action: model.deleteLayer) { Image(systemName: "trash") }.help("Delete layer")
                    .disabled(model.document.layers.count <= 1)
            }
            HStack {
                Text("Opacity").font(.caption)
                Spacer()
                Text("\(Int(model.document.layers[model.selectedLayer].opacity * 100))%").font(.caption.monospacedDigit())
            }
            Slider(value: Binding(get: { model.document.layers[model.selectedLayer].opacity }, set: { value in
                let index = model.selectedLayer
                model.change { $0.layers[index].opacity = value }
            }), in: 0...1, step: 0.05).accessibilityLabel("Layer opacity")
            HStack {
                Button("Raise") { model.moveLayer(by: 1) }.disabled(model.selectedLayer == model.document.layers.count - 1)
                Button("Lower") { model.moveLayer(by: -1) }.disabled(model.selectedLayer == 0)
            }
        }
    }

    private var resizeSheet: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Canvas Size").font(.headline)
            Stepper("Width: \(resizeWidth) px", value: $resizeWidth, in: Constants.editorCanvasSideRange)
            Stepper("Height: \(resizeHeight) px", value: $resizeHeight, in: Constants.editorCanvasSideRange)
            Text("Pixels stay at their original size, anchored to the top-left. Shrinking crops every frame and layer; Undo restores them.")
                .font(.callout).foregroundStyle(.secondary)
            HStack {
                Spacer()
                Button("Cancel") { showResize = false }.keyboardShortcut(.cancelAction)
                Button("Resize") {
                    if resizeWidth < model.document.width || resizeHeight < model.document.height { confirmCrop = true }
                    else { applyResize() }
                }.keyboardShortcut(.defaultAction)
            }
        }.padding(24).frame(width: 360)
            .alert("Crop pixels outside the new canvas?", isPresented: $confirmCrop) {
                Button("Cancel", role: .cancel) {}
                Button("Resize", role: .destructive) { applyResize() }
            }
    }

    private func applyResize() {
        model.change { $0.resize(width: resizeWidth, height: resizeHeight) }
        showResize = false
    }

    private func thumbnail(frame: Int) -> some View {
        ZStack {
            Color(nsColor: .controlBackgroundColor)
            if let image = PixelDocumentCodec.image(model.document.compositedFrame(at: frame).pixels,
                width: model.document.width, height: model.document.height) {
                Image(decorative: image, scale: 1).resizable().interpolation(.none).scaledToFit().padding(3)
            }
        }.clipShape(RoundedRectangle(cornerRadius: 4))
    }

    private func color(_ rgba: UInt32) -> Color {
        Color(.sRGB, red: Double((rgba >> 24) & 255) / 255, green: Double((rgba >> 16) & 255) / 255,
              blue: Double((rgba >> 8) & 255) / 255, opacity: Double(rgba & 255) / 255)
    }

    private func rgba(_ color: Color) -> UInt32 {
        guard let rgb = NSColor(color).usingColorSpace(.sRGB) else { return model.color }
        func byte(_ value: CGFloat) -> UInt32 { UInt32((min(max(value, 0), 1) * 255).rounded()) }
        return byte(rgb.redComponent) << 24 | byte(rgb.greenComponent) << 16 | byte(rgb.blueComponent) << 8 | byte(rgb.alphaComponent)
    }
}

