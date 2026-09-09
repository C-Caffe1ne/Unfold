import Combine
import Foundation

@MainActor
final class PixelEditorModel: ObservableObject {
    @Published private(set) var document: PixelDocument
    @Published var selectedFrame = 0
    @Published var selectedLayer = 0
    @Published var tool: PixelTool = .pencil
    @Published var color: UInt32 = 0xF4B8_60FF
    @Published var brushSize = 1
    @Published var backgroundColor: UInt32 = 0x0000_00FF
    @Published var symmetryX = false
    @Published var symmetryY = false
    @Published var pixelPerfect = false
    @Published var darken = false
    @Published var selection: Set<Int>?
    private var strokeSelection: Set<Int>?
    private var strokePoints: [PixelPoint] = []
    private var strokeBackground: UInt32 = 0
    private var strokeSymmetryX = false
    private var strokeSymmetryY = false
    private var strokeDarken = false
    private var strokePixelPerfect = false
    private var playbackOrigin: TimeInterval = 0
    var canEditCurrentCel: Bool {
        document.layers.indices.contains(selectedLayer)
            && (0..<document.frameCount).contains(selectedFrame)
            && !document.layers[selectedLayer].isLocked
    }
    /// Screen points per document pixel, derived from the step below.
    var zoom: Int { scale(at: zoomStep) }
    /// How many power-of-two steps from the scale the document opened at.
    ///
    /// The step is the stored state, not the scale. Storing the scale meant
    /// clamping it at either end, and a clamped zoom-in followed by an
    /// unclamped zoom-out landed somewhere the user had never been — a 32×32
    /// canvas opened at 17, and in-then-out left it at 71%.
    @Published private var zoomStep = 0
    /// The scale this document opened at, shown to the user as 100%. A raw
    /// 1:1 pixel scale would render a 64×64 canvas at 64 points — accurate,
    /// and impossible to draw on.
    private(set) var baseZoom = 8
    @Published var showGrid = true
    @Published var onionSkin = false
    @Published var isPlaying = false
    @Published private(set) var canUndo = false
    @Published private(set) var canRedo = false
    private var savedDocument: PixelDocument
    private var undoStack: [PixelDocument] = []
    private var redoStack: [PixelDocument] = []
    private var strokeStart: PixelPoint?
    private var lastPoint: PixelPoint?
    private var strokeDocument: PixelDocument?
    private var strokeTool: PixelTool = .pencil
    private var strokeLayer = 0
    private var strokeFrame = 0
    private var strokeColor: UInt32 = 0
    private var strokeBrush = 1
    /// Snapshots are whole documents, but Swift's copy-on-write means the
    /// unchanged frame buffers are shared between them — a stroke only
    /// unshares the one frame it touched. `byteCount` is therefore a large
    /// overestimate of what history actually costs, which is why a minimum
    /// depth is guaranteed before the budget is allowed to trim anything.
    private let historyBudget = 128 * 1024 * 1024
    private let minimumHistoryDepth = 16

    var isDirty: Bool { document != savedDocument }

    init(document: PixelDocument) {
        self.document = document
        savedDocument = document
        baseZoom = Self.fittingZoom(width: document.width, height: document.height)
        zoomStep = 0
    }

    /// Points the canvas should try to fit within when a document opens.
    /// The editor window starts at 1120×780 with a tool rail, an inspector
    /// and a timeline around the canvas, so this is deliberately well under
    /// the window's own width.
    static let fittingViewportSide = 560

    /// Largest scale the opening fit will choose. Past this a canvas opens
    /// larger than the window rather than filling it.
    static let maximumFittingZoom = 24

    /// Largest scale the user may zoom to by hand. Higher than the fitting
    /// cap so that even a canvas that opens fully fitted has somewhere to go:
    /// with the fitting cap at 24, one step up is 48. Cost per redraw falls
    /// as this rises, because fewer document pixels are on screen.
    static let maximumZoom = 64

    /// An opening scale that keeps the canvas inside the viewport, clamped to
    /// what the renderer can draw. Replaces a formula that assumed a 128px
    /// cap and gave a 512px document a scale of 2 — 1024 points wide.
    static func fittingZoom(width: Int, height: Int) -> Int {
        let longest = max(width, height, 1)
        return max(1, min(maximumFittingZoom, fittingViewportSide / longest))
    }

    private func scale(at step: Int) -> Int {
        step >= 0 ? baseZoom << step : baseZoom >> (-step)
    }

    /// Zoom as the user sees it: a multiple of the scale the document opened
    /// at, so "100%" always means "how this document first looked".
    var zoomPercent: Int { Int((100 * pow(2, Double(zoomStep))).rounded()) }
    var canZoomIn: Bool { scale(at: zoomStep + 1) <= Self.maximumZoom }
    var canZoomOut: Bool { scale(at: zoomStep - 1) >= 1 }
    func zoomIn() { if canZoomIn { zoomStep += 1 } }
    func zoomOut() { if canZoomOut { zoomStep -= 1 } }

    func markSaved() {
        savedDocument = document
        objectWillChange.send()
    }

    func change(_ edit: (inout PixelDocument) -> Void) {
        endStroke()
        isPlaying = false
        let before = document
        var next = document
        edit(&next)
        guard next != before else { return }
        remember(before)
        document = next
        selection = nil
        clampSelection()
    }

    private func remember(_ before: PixelDocument) {
        undoStack.append(before)
        redoStack.removeAll()
        trim(&undoStack)
        refreshHistory()
    }

    private func trim(_ stack: inout [PixelDocument]) {
        var bytes = stack.reduce(0) { $0 + $1.byteCount }
        while stack.count > minimumHistoryDepth && (bytes > historyBudget || stack.count > 100) {
            bytes -= stack.removeFirst().byteCount
        }
    }

    private func refreshHistory() {
        canUndo = !undoStack.isEmpty
        canRedo = !redoStack.isEmpty
    }

    private func clampSelection() {
        selectedFrame = min(max(selectedFrame, 0), document.frameCount - 1)
        selectedLayer = min(max(selectedLayer, 0), document.layers.count - 1)
    }

    func undo() {
        endStroke()
        guard let previous = undoStack.popLast() else { return }
        redoStack.append(document)
        document = previous
        selection = nil
        clampSelection()
        refreshHistory()
    }

    func redo() {
        endStroke()
        guard let next = redoStack.popLast() else { return }
        undoStack.append(document)
        document = next
        selection = nil
        clampSelection()
        refreshHistory()
    }

    func beginStroke(at point: PixelPoint) {
        endStroke()
        guard document.contains(point) else { return }
        isPlaying = false
        if tool == .eyedropper {
            color =
                document.compositedFrame(at: selectedFrame).pixels[
                    point.y * document.width + point.x]
            return
        }
        if tool == .hand { return }
        if tool == .magicWand {
            selection = PixelSelection.wand(
                at: point, pixels: document.layers[selectedLayer].frames[selectedFrame].pixels,
                width: document.width, height: document.height)
            return
        }
        guard canEditCurrentCel || tool == .rectangleSelection || tool == .lassoSelection else {
            return
        }
        strokeSelection = selection
        strokePoints = []
        strokeStart = point
        lastPoint = point
        strokeDocument = document
        strokeTool = tool
        strokeLayer = selectedLayer
        strokeFrame = selectedFrame
        strokeColor = color
        strokeBrush = brushSize
        strokeBackground = backgroundColor
        strokeSymmetryX = symmetryX
        strokeSymmetryY = symmetryY
        strokeDarken = darken
        strokePixelPerfect = pixelPerfect
        continueStroke(at: point)
    }

    func continueStroke(at point: PixelPoint) {
        guard let start = strokeStart, let original = strokeDocument else { return }
        guard document.contains(point) else {
            lastPoint = nil
            return
        }
        let previous = lastPoint ?? point
        if strokeTool == .fill && point != start { return }
        if strokeTool == .rectangleSelection {
            selection = PixelSelection.rectangle(
                from: start, to: point, width: document.width, height: document.height)
        } else if strokeTool == .lassoSelection {
            if strokePoints.last != point && strokePoints.count < 4096 {
                strokePoints.append(point)
            }
            selection = PixelSelection.lasso(
                strokePoints, width: document.width, height: document.height)
        } else if strokeTool == .move {
            guard let mask = strokeSelection else { return }
            let dx = point.x - start.x
            let dy = point.y - start.y
            var next = original
            let pixels = original.layers[strokeLayer].frames[strokeFrame].pixels
            for i in mask where pixels.indices.contains(i) {
                next.layers[strokeLayer].frames[strokeFrame].pixels[i] = 0
            }
            for i in mask where pixels.indices.contains(i) {
                let x = i % document.width + dx
                let y = i / document.width + dy
                if document.contains(.init(x: x, y: y)) {
                    next.layers[strokeLayer].frames[strokeFrame].pixels[y * document.width + x] =
                        pixels[i]
                }
            }
            document = next
            selection = PixelSelection.moved(
                mask, dx: dx, dy: dy, width: document.width, height: document.height)
        } else {
            let isShape: Bool = [.line, .rectangle, .ellipse, .gradient].contains(strokeTool)
            var next = isShape ? original : document
            if strokeTool == .pencil && strokePixelPerfect && strokeBrush == 1 {
                for p in PixelDrawing.line(from: previous, to: point) where strokePoints.last != p {
                    guard strokePoints.count < document.width * document.height * 4 else { break }
                    strokePoints.append(p)
                    if strokePoints.count >= 3 {
                        let n = strokePoints.count
                        let a = strokePoints[n - 3]
                        let b = strokePoints[n - 2]
                        let c = strokePoints[n - 1]
                        if abs(a.x - c.x) == 1 && abs(a.y - c.y) == 1 && (a.x == b.x || a.y == b.y)
                            && (b.x == c.x || b.y == c.y)
                        {
                            strokePoints.remove(at: n - 2)
                        }
                    }
                }
                next = original
                var pixels = original.layers[strokeLayer].frames[strokeFrame].pixels
                for p in strokePoints {
                    let xs = strokeSymmetryX ? [p.x, document.width - 1 - p.x] : [p.x]
                    let ys = strokeSymmetryY ? [p.y, document.height - 1 - p.y] : [p.y]
                    for y in ys {
                        for x in xs {
                            let i = y * document.width + x
                            if strokeSelection?.contains(i) ?? true { pixels[i] = strokeColor }
                        }
                    }
                }
                next.layers[strokeLayer].frames[strokeFrame].pixels = pixels
            } else {
                applyDraw(&next, from: isShape ? start : previous, to: point)
            }
            document = next
        }
        lastPoint = point
    }

    private func applyDraw(_ next: inout PixelDocument, from: PixelPoint, to: PixelPoint) {
        PixelDrawing.draw(
            document: &next, tool: strokeTool, from: from, to: to, color: strokeColor,
            background: strokeBackground, brush: strokeBrush, layer: strokeLayer,
            frame: strokeFrame,
            selection: strokeSelection, symmetryX: strokeSymmetryX, symmetryY: strokeSymmetryY,
            darken: strokeDarken)
    }

    /// Called for mouse-up, focus loss, save, and document actions.
    func endStroke() {
        if let before = strokeDocument, before != document { remember(before) }
        strokeStart = nil
        lastPoint = nil
        strokeDocument = nil
        strokePoints = []
        strokeSelection = nil
    }

    func addFrame(duplicate: Bool) {
        guard document.frameCount < Constants.editorFrameCountRange.upperBound else { return }
        let index = selectedFrame
        change { $0.insertFrame(after: index, duplicate: duplicate) }
        selectedFrame = min(index + 1, document.frameCount - 1)
    }

    func deleteFrame() {
        let index = selectedFrame
        change { $0.removeFrame(at: index) }
    }

    func moveFrame(by delta: Int) {
        let from = selectedFrame
        let to = selectedFrame + delta
        guard (0..<document.frameCount).contains(to) else { return }
        change { $0.moveFrame(from: from, to: to) }
        selectedFrame = to
    }

    func addLayer() {
        guard document.layers.count < PixelDocument.maximumLayers,
            document.byteCount + document.width * document.height * document.frameCount * 4
                <= Constants.editorMaxDocumentBytes
        else { return }
        change { doc in
            doc.layers.append(
                PixelLayer(
                    name: "Layer \(doc.layers.count + 1)",
                    frames: Array(
                        repeating: PixelFrame(width: doc.width, height: doc.height),
                        count: doc.frameCount)))
        }
        selectedLayer = document.layers.count - 1
    }

    func deleteLayer() {
        guard document.layers.count > 1 else { return }
        let index = selectedLayer
        change { $0.layers.remove(at: index) }
    }

    func moveLayer(by delta: Int) {
        let from = selectedLayer
        let to = selectedLayer + delta
        guard document.layers.indices.contains(to) else { return }
        change { $0.layers.swapAt(from, to) }
        selectedLayer = to
    }

    func clearSelectedPixels() {
        guard canEditCurrentCel else { return }
        let layer = selectedLayer
        let frame = selectedFrame
        let mask = selection
        change { doc in
            for i in doc.layers[layer].frames[frame].pixels.indices where mask?.contains(i) ?? true
            {
                doc.layers[layer].frames[frame].pixels[i] = 0
            }
        }
        selection = mask
    }

    func clearFrame() {
        guard canEditCurrentCel else { return }
        let layer = selectedLayer
        let frame = selectedFrame
        change { $0.layers[layer].frames[frame] = PixelFrame(width: $0.width, height: $0.height) }
    }

    func flip(horizontal: Bool) {
        guard canEditCurrentCel else { return }
        let layer = selectedLayer
        let frame = selectedFrame
        change { $0.flip(layer: layer, frame: frame, horizontal: horizontal) }
    }
}

extension PixelEditorModel {
    func selectFrame(_ index: Int) {
        endStroke()
        isPlaying = false
        selectedFrame = index
        clampSelection()
        selection = nil
    }
    func selectLayer(_ index: Int) {
        endStroke()
        isPlaying = false
        selectedLayer = index
        clampSelection()
        selection = nil
    }
    func clearSelection() {
        endStroke()
        selection = nil
    }
    func selectAll() {
        endStroke()
        selection = Set(0..<(document.width * document.height))
    }
    func setFrameDuration(_ milliseconds: Int?, at index: Int? = nil) {
        let frame = index ?? selectedFrame
        guard (0..<document.frameCount).contains(frame),
            milliseconds == nil || PixelFrameSettings.durationRange.contains(milliseconds!)
        else { return }
        change { doc in
            doc.frameSettings = (0..<doc.frameCount).map { doc.settings(at: $0) }
            doc.frameSettings[frame].durationMS = milliseconds
        }
    }
    func toggleFrameVisibility(_ index: Int) {
        guard (0..<document.frameCount).contains(index) else { return }
        change { doc in
            doc.frameSettings = (0..<doc.frameCount).map { doc.settings(at: $0) }
            doc.frameSettings[index].isVisible.toggle()
        }
    }
    func moveFrame(from: Int, to: Int) {
        guard (0..<document.frameCount).contains(from), (0..<document.frameCount).contains(to)
        else {
            return
        }
        change { $0.moveFrame(from: from, to: to) }
        selectedFrame = to
    }
    func moveLayer(from: Int, to: Int) {
        guard document.layers.indices.contains(from), document.layers.indices.contains(to),
            from != to
        else { return }
        change {
            let layer = $0.layers.remove(at: from)
            $0.layers.insert(layer, at: to)
        }
        selectedLayer = to
    }
    func toggleLayerVisibility(_ index: Int) {
        guard document.layers.indices.contains(index) else { return }
        change { $0.layers[index].isVisible.toggle() }
    }
    func toggleLayerLock(_ index: Int) {
        guard document.layers.indices.contains(index) else { return }
        change { $0.layers[index].isLocked.toggle() }
    }
    func renameLayer(_ index: Int, name: String) {
        let name = String(name.trimmingCharacters(in: .whitespacesAndNewlines).prefix(256))
        guard document.layers.indices.contains(index), !name.isEmpty else { return }
        change { $0.layers[index].name = name }
    }
    func addPaletteColor(_ value: UInt32) {
        guard document.palette.count < PixelPalette.maximumColors else { return }
        change { $0.palette.append(value) }
    }
    func updatePaletteColor(at index: Int, color: UInt32) {
        guard document.palette.indices.contains(index) else { return }
        change { $0.palette[index] = color }
    }
    func removePaletteColor(at index: Int) {
        guard document.palette.count > 1, document.palette.indices.contains(index) else { return }
        change { $0.palette.remove(at: index) }
    }
    func movePaletteColor(from: Int, to: Int) {
        guard document.palette.indices.contains(from), document.palette.indices.contains(to),
            from != to
        else { return }
        change {
            let color = $0.palette.remove(at: from)
            $0.palette.insert(color, at: to)
        }
    }
    func importGPL(_ text: String) throws {
        let colors = try PixelPalette.parseGPL(text)
        change { $0.palette = colors }
    }
    func startPlayback(at time: TimeInterval) {
        endStroke()
        selection = nil
        playbackOrigin = time
        let sample = document.playbackSample(elapsed: 0)
        if let frame = sample.frame { selectedFrame = frame }
        isPlaying = sample.frame != nil && !sample.finished
    }
    func togglePlayback(at time: TimeInterval) {
        if isPlaying { isPlaying = false } else { startPlayback(at: time) }
    }
    func tickPlayback(at time: TimeInterval) {
        guard isPlaying else { return }
        let sample = document.playbackSample(elapsed: max(0, time - playbackOrigin))
        if let frame = sample.frame { selectedFrame = frame }
        if sample.finished || sample.frame == nil { isPlaying = false }
    }
}
