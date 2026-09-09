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
    @State private var paletteSelection: Set<Int> = []
    @State private var zoomField = ""
    @State private var hoveredTool: PixelTool?

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
        .onAppear { zoomField = "\(model.zoomPercent)" }
        .onChange(of: model.zoomPercent) { zoomField = "\($0)" }
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
                .pixelTooltip(PixelHelp.text("Undo", key: "⌘Z", "Steps back one whole stroke or document change."))
                .keyboardShortcut("z").disabled(!model.canUndo)
            Button(action: model.redo) { Image(systemName: "arrow.uturn.forward") }
                .pixelTooltip(PixelHelp.text("Redo", key: "⇧⌘Z", "Replays the change Undo took back."))
                .keyboardShortcut("z", modifiers: [.command, .shift]).disabled(!model.canRedo)
            Menu("File") {
                Button("Open…", action: openDocument).keyboardShortcut("o")
                    .help(PixelHelp.text("Open", key: "⌘O", "Opens a .unf document, a PNG or a JPEG."))
                Divider()
                Button("Save", action: save)
                    .help(PixelHelp.text("Save", key: "⌘S", "Writes the document back to its own file."))
                Button("Save As…", action: saveAs)
                    .keyboardShortcut("s", modifiers: [.command, .shift])
                    .help(PixelHelp.text("Save As", key: "⇧⌘S", "Writes a copy as .unf, a PNG sprite sheet or an animated GIF."))
                Divider()
                Button(Strings.Editor.saveButton, action: saveToLibrary)
                    .help(PixelHelp.text(Strings.Editor.saveButton, "Stores the drawing in the character library with its playback order and per-frame timing."))
            }.frame(width: 70)
            .pixelTooltip(PixelHelp.text("File", "Open, save, export a copy, or store the drawing in the character library."))
            Button("Canvas Size…") {
                resizeWidth = model.document.width
                resizeHeight = model.document.height
                showResize = true
            }
            .pixelTooltip(PixelHelp.text("Canvas Size", "Resizes every frame and layer, anchored to the top-left. Shrinking crops, and Undo restores."))
            Spacer()
            Button("Save", action: save)
                .keyboardShortcut("s").buttonStyle(.borderedProminent).tint(.orange)
                .pixelTooltip(PixelHelp.text("Save", key: "⌘S", "Writes the document back to its own file."))
        }.padding(12)
    }

    /// Two columns rather than one: fifteen tools in a single file ran past
    /// the bottom of a short window, putting the last of them behind a scroll.
    ///
    /// The palette and the foreground/background pair sit here rather than in
    /// the inspector, so everything one stroke is made of — the tool, the
    /// colour, the swatch it came from — is reachable without crossing the
    /// window. The colour pair is outside the scroll view: it is what the
    /// canvas is painting with, and it should not be able to scroll away.
    private var tools: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(spacing: 8) {
                    LazyVGrid(columns: Array(repeating: GridItem(.fixed(38), spacing: 6), count: 2), spacing: 6) {
                        ForEach(PixelTool.allCases) { tool in
                            Button { model.tool = tool } label: {
                                Image(systemName: tool.symbol)
                                    .font(.system(size: 17))
                                    .foregroundStyle(model.tool == tool ? Color.orange : Color.primary)
                                    .frame(width: 38, height: 32)
                                    .background(RoundedRectangle(cornerRadius: 6).fill(toolBackground(tool)))
                                    .overlay(RoundedRectangle(cornerRadius: 6)
                                        .strokeBorder(hoveredTool == tool ? Color.orange.opacity(0.55) : .clear))
                                    // The glyph leaves most of the cell transparent;
                                    // without this, hovering between strokes of the
                                    // icon would not count as hovering the tool.
                                    .contentShape(RoundedRectangle(cornerRadius: 6))
                            }.buttonStyle(.plain)
                                .onHover { inside in
                                    if inside { hoveredTool = tool }
                                    else if hoveredTool == tool { hoveredTool = nil }
                                }
                                .animation(.easeOut(duration: 0.12), value: hoveredTool)
                                .pixelTooltip(tool.help).accessibilityLabel(tool.title)
                        }
                    }
                    Divider()
                    PixelPaletteView(model: model, selection: $paletteSelection, importError: $paletteError)
                }.padding(10)
            }
            Divider()
            PixelColorSwatches(model: model).padding(10)
        }.frame(width: 130)
    }

    /// The armed tool keeps its own colour whether or not it is hovered, so
    /// the highlight never leaves which tool is selected in doubt.
    private func toolBackground(_ tool: PixelTool) -> Color {
        if model.tool == tool { return .orange.opacity(0.25) }
        return hoveredTool == tool ? Color.primary.opacity(0.12) : .clear
    }

    private var canvas: some View {
        VStack(spacing: 0) {
            HStack {
                Toggle("Grid", isOn: $model.showGrid)
                    .pixelTooltip(PixelHelp.text("Grid", "Overlays a one-pixel grid once the canvas is zoomed in far enough to show it."))
                Toggle("Onion Skin", isOn: $model.onionSkin)
                    .pixelTooltip(PixelHelp.text("Onion Skin", "Shows the previous frame faintly behind the one being drawn."))
                Toggle("Pixel-perfect", isOn: $model.pixelPerfect)
                    .pixelTooltip(PixelHelp.text("Pixel-perfect pencil", "Drops the middle pixel of a corner before a diagonal step, so a freehand line has no double-thick bends. One-pixel brush only."))
                Stepper("Brush: \(model.brushSize) px", value: $model.brushSize, in: 1...8)
                    .pixelTooltip(PixelHelp.text("Brush Size", "Width in pixels of the pencil, eraser, shapes, spray and brightness stamps."))
                Spacer()
                Button(action: model.zoomOut) { Image(systemName: "minus.magnifyingglass") }
                    .disabled(!model.canZoomOut)
                    .pixelTooltip(PixelHelp.text("Zoom Out", key: "⌘ scroll down", "Steps down one zoom level. Zoom resamples nothing."))
                TextField("Zoom", text: $zoomField)
                    .frame(width: 46).multilineTextAlignment(.trailing).monospacedDigit()
                    .pixelTooltip(PixelHelp.text("Zoom", key: "Return", "Type a percentage; it snaps to the nearest whole pixel scale."))
                    .onSubmit(applyTypedZoom)
                Text("%")
                Button(action: model.zoomIn) { Image(systemName: "plus.magnifyingglass") }
                    .disabled(!model.canZoomIn)
                    .pixelTooltip(PixelHelp.text("Zoom In", key: "⌘ scroll up", "Steps up one zoom level. Zoom resamples nothing."))
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
                    .pixelTooltip(PixelHelp.text(model.isPlaying ? "Pause" : "Play", key: "Return",
                        "Runs the frames in the playback order, each for its own duration."))
                    Spacer()
                    Text("\(Int(model.document.fps)) FPS").monospacedDigit()
                }
                Slider(value: Binding(get: { model.document.fps }, set: { value in model.change { $0.fps = value } }), in: 1...24, step: 1)
                    .accessibilityLabel("Animation speed")
                    .pixelTooltip(PixelHelp.text("Frame Rate", "Frames per second for every frame that has no duration of its own."))
                PixelPlaybackControls(model: model)
                Divider()
                Toggle("Mirror X", isOn: $model.symmetryX)
                    .pixelTooltip(PixelHelp.text("Mirror X", "Also stamps every brush, shape and fill mirrored across the vertical centre line."))
                Toggle("Mirror Y", isOn: $model.symmetryY)
                    .pixelTooltip(PixelHelp.text("Mirror Y", "Also stamps every brush, shape and fill mirrored across the horizontal centre line."))
                Toggle("Darken brightness brush", isOn: $model.darken)
                    .pixelTooltip(PixelHelp.text("Darken brightness brush", "Turns the brightness tool from lightening to darkening."))
                HStack {
                    Button { model.flip(horizontal: true) } label: { Image(systemName: "arrow.left.and.right.righttriangle.left.righttriangle.right") }
                        .pixelTooltip(PixelHelp.text("Flip Horizontally", "Mirrors the selected layer's current cel left to right. Nothing else moves."))
                    Button { model.flip(horizontal: false) } label: { Image(systemName: "arrow.up.and.down.righttriangle.up.righttriangle.down") }
                        .pixelTooltip(PixelHelp.text("Flip Vertically", "Mirrors the selected layer's current cel top to bottom. Nothing else moves."))
                    Button("Clear", action: model.clearFrame)
                        .pixelTooltip(PixelHelp.text("Clear", "Empties the selected layer's current cel. Undo restores it."))
                }
                Divider()
                layers
            }.padding(14)
        }.frame(width: 212)
    }

    private var layers: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("LAYERS").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
            HStack {
                Button("Raise") { model.moveLayer(by: 1) }.disabled(model.selectedLayer == model.document.layers.count - 1)
                    .pixelTooltip(PixelHelp.text("Raise Layer", "Moves the selected layer one place up the stack, in front of its neighbour."))
                Button("Lower") { model.moveLayer(by: -1) }.disabled(model.selectedLayer == 0)
                    .pixelTooltip(PixelHelp.text("Lower Layer", "Moves the selected layer one place down the stack, behind its neighbour."))
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

    /// Rewrites the field from the model afterwards, so a rejected or snapped
    /// entry never leaves a number on screen the canvas is not drawing at.
    private func applyTypedZoom() {
        if let typed = Int(zoomField.filter(\.isNumber)), typed > 0 {
            model.setZoomPercent(typed)
        }
        zoomField = "\(model.zoomPercent)"
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
}
