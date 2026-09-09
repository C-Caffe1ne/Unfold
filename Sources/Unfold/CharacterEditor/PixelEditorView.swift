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
        VStack(spacing: 8) {
            ForEach(PixelTool.allCases) { tool in
                Button { model.tool = tool } label: {
                    Image(systemName: tool.symbol)
                        .font(.system(size: 18))
                        .frame(width: 38, height: 34)
                        .background(model.tool == tool ? Color.orange.opacity(0.25) : Color.clear)
                        .clipShape(RoundedRectangle(cornerRadius: 6))
                }.buttonStyle(.plain).help(tool.title).accessibilityLabel(tool.title)
            }
            Divider()
            ColorPicker("Color", selection: Binding(get: { color(model.color) }, set: { model.color = rgba($0) }), supportsOpacity: true)
                .labelsHidden().help("Drawing color")
            Spacer()
        }.padding(10).frame(width: 62)
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
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("FRAMES").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                Spacer()
                Button { model.addFrame(duplicate: false) } label: { Image(systemName: "plus") }
                    .help("Add blank frame").disabled(model.document.frameCount >= 24)
                Button { model.addFrame(duplicate: true) } label: { Image(systemName: "plus.square.on.square") }
                    .help("Duplicate frame").disabled(model.document.frameCount >= 24)
                Button(action: model.deleteFrame) { Image(systemName: "trash") }
                    .help("Delete frame").disabled(model.document.frameCount <= 1)
                Button { model.moveFrame(by: -1) } label: { Image(systemName: "chevron.left") }
                    .help("Move frame earlier").disabled(model.selectedFrame == 0)
                Button { model.moveFrame(by: 1) } label: { Image(systemName: "chevron.right") }
                    .help("Move frame later").disabled(model.selectedFrame == model.document.frameCount - 1)
            }
            ScrollView(.horizontal) {
                HStack(spacing: 8) {
                    ForEach(0..<model.document.frameCount, id: \.self) { index in
                        Button {
                            model.endStroke()
                            model.selectedFrame = index
                        } label: {
                            VStack(spacing: 3) {
                                thumbnail(frame: index).frame(width: 54, height: 54)
                                Text("\(index + 1)").font(.caption2.monospacedDigit())
                            }.padding(5)
                                .background(model.selectedFrame == index ? Color.orange.opacity(0.25) : Color.secondary.opacity(0.08))
                                .clipShape(RoundedRectangle(cornerRadius: 6))
                        }.buttonStyle(.plain).accessibilityLabel("Frame \(index + 1)")
                    }
                }
            }
        }.padding(12).frame(height: 134)
    }

    private var inspector: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("PREVIEW").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                TimelineView(.animation(minimumInterval: 1 / model.document.fps, paused: !model.isPlaying)) { context in
                    let frame = model.isPlaying
                        ? Int(context.date.timeIntervalSinceReferenceDate * model.document.fps) % model.document.frameCount
                        : model.selectedFrame
                    thumbnail(frame: frame).frame(width: 180, height: 160)
                }
                HStack {
                    Button { model.isPlaying.toggle() } label: {
                        Label(model.isPlaying ? "Pause" : "Play", systemImage: model.isPlaying ? "pause.fill" : "play.fill")
                    }
                    Spacer()
                    Text("\(Int(model.document.fps)) FPS").monospacedDigit()
                }
                Slider(value: Binding(get: { model.document.fps }, set: { value in model.change { $0.fps = value } }), in: 1...24, step: 1)
                    .accessibilityLabel("Animation speed")
                Divider()
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
        let colors: [UInt32] = [0x171923FF, 0xFFFFFFFF, 0x9195A3FF, 0xD74949FF, 0xF4B860FF, 0xF3E7A2FF,
                                0x72B883FF, 0x4B8CBFFF, 0x8A6CBFFF, 0xE89FB6FF, 0x805E49FF, 0x00000000]
        return LazyVGrid(columns: Array(repeating: GridItem(.fixed(24)), count: 6), spacing: 6) {
            ForEach(colors, id: \.self) { value in
                Button { model.color = value } label: {
                    ZStack {
                        Rectangle().fill(color(value))
                        if value == 0 { Image(systemName: "slash.circle").foregroundStyle(.secondary) }
                    }.frame(width: 24, height: 24)
                        .overlay(RoundedRectangle(cornerRadius: 3).stroke(model.color == value ? Color.orange : Color.secondary, lineWidth: model.color == value ? 2 : 0.5))
                }.buttonStyle(.plain).help(value == 0 ? "Transparent" : String(format: "#%08X", value))
            }
        }
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
            ForEach(Array(model.document.layers.indices.reversed()), id: \.self) { index in
                Button {
                    model.endStroke()
                    model.selectedLayer = index
                } label: {
                    HStack {
                        Image(systemName: model.document.layers[index].opacity == 0 ? "eye.slash" : "square.3.layers.3d")
                        Text(model.document.layers[index].name).lineLimit(1)
                        Spacer()
                    }.padding(8)
                        .background(model.selectedLayer == index ? Color.orange.opacity(0.25) : Color.secondary.opacity(0.08))
                        .clipShape(RoundedRectangle(cornerRadius: 5))
                }.buttonStyle(.plain)
            }
            TextField("Layer name", text: Binding(get: { model.document.layers[model.selectedLayer].name }, set: { value in
                let index = model.selectedLayer
                model.change { $0.layers[index].name = value }
            }))
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

@MainActor
private struct NativePixelCanvas: NSViewRepresentable {
    @ObservedObject var model: PixelEditorModel
    func makeNSView(context: Context) -> PixelCanvasView { PixelCanvasView(model: model) }
    func updateNSView(_ view: PixelCanvasView, context: Context) { view.needsDisplay = true }
}

@MainActor
private final class PixelCanvasView: NSView {
    let model: PixelEditorModel
    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }

    init(model: PixelEditorModel) {
        self.model = model
        super.init(frame: .zero)
        setAccessibilityElement(true)
        setAccessibilityLabel("Pixel canvas. Pencil B, eraser E, fill F, eyedropper I, line L, rectangle R, ellipse O.")
    }
    required init?(coder: NSCoder) { return nil }

    override func draw(_ dirtyRect: NSRect) {
        let doc = model.document
        let scale = CGFloat(model.zoom)
        let clip = dirtyRect.intersection(bounds)
        guard !clip.isEmpty else { return }
        let minX = max(0, Int(clip.minX / scale)), maxX = min(doc.width, Int(ceil(clip.maxX / scale)))
        let minY = max(0, Int(clip.minY / scale)), maxY = min(doc.height, Int(ceil(clip.maxY / scale)))
        guard minX < maxX, minY < maxY else { return }
        NSGraphicsContext.current?.shouldAntialias = false
        for y in minY..<maxY {
            for x in minX..<maxX {
                NSColor(white: ((x / 4 + y / 4) % 2 == 0) ? 0.22 : 0.28, alpha: 1).setFill()
                NSRect(x: CGFloat(x) * scale, y: CGFloat(y) * scale, width: scale, height: scale).fill()
            }
        }
        func paint(_ frame: PixelFrame, opacity: CGFloat) {
            for y in minY..<maxY {
                for x in minX..<maxX {
                    let pixel = frame.pixels[y * doc.width + x]
                    guard pixel & 255 != 0 else { continue }
                    NSColor(srgbRed: CGFloat((pixel >> 24) & 255) / 255, green: CGFloat((pixel >> 16) & 255) / 255,
                        blue: CGFloat((pixel >> 8) & 255) / 255, alpha: CGFloat(pixel & 255) / 255 * opacity).setFill()
                    NSRect(x: CGFloat(x) * scale, y: CGFloat(y) * scale, width: scale, height: scale).fill(using: .sourceOver)
                }
            }
        }
        if model.onionSkin && model.selectedFrame > 0 { paint(doc.compositedFrame(at: model.selectedFrame - 1), opacity: 0.25) }
        paint(doc.compositedFrame(at: model.selectedFrame), opacity: 1)
        if model.showGrid && model.zoom >= 6 {
            NSColor(white: 0, alpha: 0.15).setFill()
            for x in minX...maxX { NSRect(x: CGFloat(x) * scale, y: clip.minY, width: 1, height: clip.height).fill() }
            for y in minY...maxY { NSRect(x: clip.minX, y: CGFloat(y) * scale, width: clip.width, height: 1).fill() }
        }
    }

    private func point(_ event: NSEvent) -> PixelPoint {
        let location = convert(event.locationInWindow, from: nil)
        return PixelPoint(x: Int(floor(location.x / CGFloat(model.zoom))), y: Int(floor(location.y / CGFloat(model.zoom))))
    }
    override func mouseDown(with event: NSEvent) {
        window?.makeFirstResponder(self)
        model.beginStroke(at: point(event))
    }
    override func mouseDragged(with event: NSEvent) { model.continueStroke(at: point(event)) }
    override func mouseUp(with event: NSEvent) { model.endStroke() }
    override func resignFirstResponder() -> Bool { model.endStroke(); return super.resignFirstResponder() }
    override func resetCursorRects() { addCursorRect(bounds, cursor: .crosshair) }
    override func keyDown(with event: NSEvent) {
        if event.modifierFlags.contains(.command) {
            if event.charactersIgnoringModifiers?.lowercased() == "z" {
                if event.modifierFlags.contains(.shift) { model.redo() } else { model.undo() }
                return
            }
            super.keyDown(with: event)
            return
        }
        let shortcuts: [String: PixelTool] = ["b": .pencil, "e": .eraser, "f": .fill, "i": .eyedropper,
                                               "l": .line, "r": .rectangle, "o": .ellipse]
        if let tool = shortcuts[event.charactersIgnoringModifiers?.lowercased() ?? ""] {
            model.endStroke(); model.tool = tool
        } else if event.charactersIgnoringModifiers == " " { model.isPlaying.toggle() }
        else { super.keyDown(with: event) }
    }
}
