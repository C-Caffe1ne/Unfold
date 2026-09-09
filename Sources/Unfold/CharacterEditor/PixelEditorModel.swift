import Combine
import Foundation

@MainActor
final class PixelEditorModel: ObservableObject {
    @Published private(set) var document: PixelDocument
    @Published var selectedFrame = 0
    @Published var selectedLayer = 0
    @Published var tool: PixelTool = .pencil
    @Published var color: UInt32 = 0xF4B860FF
    @Published var brushSize = 1
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

    func markSaved() { savedDocument = document; objectWillChange.send() }

    func change(_ edit: (inout PixelDocument) -> Void) {
        endStroke()
        let before = document
        var next = document
        edit(&next)
        guard next != before else { return }
        remember(before)
        document = next
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
        clampSelection()
        refreshHistory()
    }

    func redo() {
        endStroke()
        guard let next = redoStack.popLast() else { return }
        undoStack.append(document)
        document = next
        clampSelection()
        refreshHistory()
    }

    func beginStroke(at point: PixelPoint) {
        endStroke()
        guard document.contains(point) else { return }
        isPlaying = false
        if tool == .eyedropper {
            color = document.compositedFrame(at: selectedFrame).pixels[point.y * document.width + point.x]
            return
        }
        strokeStart = point
        lastPoint = point
        strokeDocument = document
        strokeTool = tool
        strokeLayer = selectedLayer
        strokeFrame = selectedFrame
        strokeColor = color
        strokeBrush = brushSize
        continueStroke(at: point)
    }

    func continueStroke(at point: PixelPoint) {
        guard let start = strokeStart, let original = strokeDocument else { return }
        guard document.contains(point) else { lastPoint = nil; return }
        let previous = lastPoint ?? point
        if strokeTool == .fill && point != start { return }
        var next = document
        let isShape = [.line, .rectangle, .ellipse].contains(strokeTool)
        if isShape { next = original }
        next.draw(tool: strokeTool, from: isShape ? start : previous, to: point,
                  color: strokeColor, brush: strokeBrush, layer: strokeLayer, frame: strokeFrame)
        document = next
        lastPoint = point
    }

    /// Called for mouse-up, focus loss, save, and document actions.
    func endStroke() {
        if let before = strokeDocument, before != document { remember(before) }
        strokeStart = nil
        lastPoint = nil
        strokeDocument = nil
    }

    func addFrame(duplicate: Bool) {
        guard document.frameCount < Constants.editorFrameCountRange.upperBound else { return }
        let index = selectedFrame
        change { $0.insertFrame(after: index, duplicate: duplicate) }
        selectedFrame = index + 1
    }

    func deleteFrame() {
        let index = selectedFrame
        change { $0.removeFrame(at: index) }
    }

    func moveFrame(by delta: Int) {
        let from = selectedFrame, to = selectedFrame + delta
        guard (0..<document.frameCount).contains(to) else { return }
        change { $0.moveFrame(from: from, to: to) }
        selectedFrame = to
    }

    func addLayer() {
        guard document.layers.count < PixelDocument.maximumLayers,
              document.byteCount + document.width * document.height * document.frameCount * 4
                <= Constants.editorMaxDocumentBytes else { return }
        change { doc in
            doc.layers.append(PixelLayer(name: "Layer \(doc.layers.count + 1)",
                frames: Array(repeating: PixelFrame(width: doc.width, height: doc.height), count: doc.frameCount)))
        }
        selectedLayer = document.layers.count - 1
    }

    func deleteLayer() {
        guard document.layers.count > 1 else { return }
        let index = selectedLayer
        change { $0.layers.remove(at: index) }
    }

    func moveLayer(by delta: Int) {
        let from = selectedLayer, to = selectedLayer + delta
        guard document.layers.indices.contains(to) else { return }
        change { $0.layers.swapAt(from, to) }
        selectedLayer = to
    }

    func clearFrame() {
        let layer = selectedLayer, frame = selectedFrame
        change { $0.layers[layer].frames[frame] = PixelFrame(width: $0.width, height: $0.height) }
    }

    func flip(horizontal: Bool) {
        let layer = selectedLayer, frame = selectedFrame
        change { $0.flip(layer: layer, frame: frame, horizontal: horizontal) }
    }
}
