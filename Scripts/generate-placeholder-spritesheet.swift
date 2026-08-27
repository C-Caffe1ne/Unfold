#!/usr/bin/env swift
import AppKit
import CoreGraphics

// Generates the placeholder sprite sheet for the "default-cat" built-in
// character, matching Unfold's V1 sprite sheet spec:
//   - 384x384 px frames, transparent background, 8 columns
//   - idle:    frames 0-7   (row 0)               8f @ 7fps,  loop
//   - stretch: frames 8-19  (row 1 + first half of row 2)  12f @ 11fps, once
//
// stretch is deliberately 12 frames on an 8-column sheet so it spans two
// rows — this is what exercises the frame-index-based (not row-based)
// animation model. Each frame carries a big index label so playback order
// is verifiable at a glance; the "pose" is a rough placeholder for the
// described stretch beats (normal -> reach forward -> lower/raise hips ->
// max stretch -> slight recover), not final art.
//
// Regenerate with: swift Scripts/generate-placeholder-spritesheet.swift

let columns = 8
let frameSize = 384
let totalFrames = 20 // 8 idle + 12 stretch
let rows = Int(ceil(Double(totalFrames) / Double(columns))) // -> 3
let width = columns * frameSize
let height = rows * frameSize

guard let colorSpace = CGColorSpace(name: CGColorSpace.sRGB),
      let context = CGContext(
        data: nil,
        width: width,
        height: height,
        bitsPerComponent: 8,
        bytesPerRow: 0,
        space: colorSpace,
        bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
      )
else {
    fatalError("could not create bitmap context")
}

NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(cgContext: context, flipped: false)

let catColor = NSColor(calibratedRed: 0.85, green: 0.55, blue: 0.25, alpha: 1.0)
let shadowColor = NSColor.black.withAlphaComponent(0.15)
let labelColor = NSColor.black.withAlphaComponent(0.55)

/// `context` is a plain CG bitmap context: y = 0 is the BOTTOM row of
/// pixels. To make frame index 0 land at the visual TOP-LEFT of the
/// exported file (row 0 = top, matching how `SpriteSheetImage` reads
/// `frameIndex / columns` as the row), flip the row while drawing.
func rectFor(frameIndex: Int) -> NSRect {
    let column = frameIndex % columns
    let row = frameIndex / columns
    let flippedRow = rows - 1 - row
    let origin = NSPoint(x: column * frameSize, y: flippedRow * frameSize)
    return NSRect(origin: origin, size: NSSize(width: frameSize, height: frameSize))
}

func drawLabel(_ text: String, in rect: NSRect) {
    let attrs: [NSAttributedString.Key: Any] = [
        .font: NSFont.boldSystemFont(ofSize: CGFloat(frameSize) * 0.12),
        .foregroundColor: labelColor
    ]
    let string = NSAttributedString(string: text, attributes: attrs)
    let size = string.size()
    string.draw(at: NSPoint(x: rect.minX + 12, y: rect.maxY - size.height - 10))
}

func drawShadow(in rect: NSRect) {
    shadowColor.setFill()
    NSBezierPath(ovalIn: rect.insetBy(dx: rect.width * 0.22, dy: rect.height * 0.06).offsetBy(dx: 0, dy: -rect.height * 0.32)).fill()
}

// idle: frames 0-7, gentle bob (breathing), roughly constant silhouette.
for local in 0..<8 {
    let frameIndex = local
    let rect = rectFor(frameIndex: frameIndex)
    drawShadow(in: rect)

    let t = Double(local) / 8.0
    let bob = sin(t * 2 * Double.pi) * (rect.height * 0.03)
    let body = rect.insetBy(dx: rect.width * 0.24, dy: rect.height * 0.24).offsetBy(dx: 0, dy: bob)
    catColor.setFill()
    NSBezierPath(ovalIn: body).fill()

    drawLabel("\(frameIndex)", in: rect)
}

// stretch: frames 8-19 (12 total), spanning rows 1 and 2.
//   local 0-2  : normal pose
//   local 3-5  : front legs reaching forward
//   local 6-8  : lower back, hips raised
//   local 9-10 : max stretch
//   local 11   : slight recover
for local in 0..<12 {
    let frameIndex = 8 + local
    let rect = rectFor(frameIndex: frameIndex)
    drawShadow(in: rect)
    catColor.setFill()

    switch local {
    case 0...2:
        let body = rect.insetBy(dx: rect.width * 0.24, dy: rect.height * 0.24)
        NSBezierPath(ovalIn: body).fill()

    case 3...5:
        let body = rect.insetBy(dx: rect.width * 0.26, dy: rect.height * 0.24)
        NSBezierPath(ovalIn: body).fill()
        let reach = CGFloat(local - 3) / 2.0 // 0 -> 1 across this beat
        let leg = NSRect(
            x: rect.midX,
            y: rect.midY - rect.height * 0.06,
            width: rect.width * (0.18 + 0.16 * reach),
            height: rect.height * 0.12
        )
        NSBezierPath(ovalIn: leg).fill()

    case 6...8:
        let lower = CGFloat(local - 6) / 2.0 // 0 -> 1 across this beat
        let body = rect.insetBy(dx: rect.width * 0.22, dy: rect.height * (0.30 + 0.04 * lower))
            .offsetBy(dx: -rect.width * 0.05, dy: -rect.height * 0.10)
        NSBezierPath(ovalIn: body).fill()
        let hip = NSRect(
            x: rect.midX - rect.width * 0.02,
            y: rect.midY + rect.height * (0.04 + 0.08 * lower),
            width: rect.width * 0.30,
            height: rect.height * (0.22 + 0.10 * lower)
        )
        NSBezierPath(ovalIn: hip).fill()

    case 9...10:
        let body = rect.insetBy(dx: rect.width * 0.27, dy: rect.height * 0.08)
        NSBezierPath(ovalIn: body).fill()

    default: // 11: slight recover
        let body = rect.insetBy(dx: rect.width * 0.25, dy: rect.height * 0.14)
        NSBezierPath(ovalIn: body).fill()
    }

    drawLabel("\(frameIndex)", in: rect)
}

NSGraphicsContext.restoreGraphicsState()

guard let cgImage = context.makeImage() else {
    fatalError("could not render context to image")
}

let bitmap = NSBitmapImageRep(cgImage: cgImage)
guard let png = bitmap.representation(using: .png, properties: [:]) else {
    fatalError("could not encode PNG")
}

let outputPath = CommandLine.arguments.count > 1
    ? CommandLine.arguments[1]
    : "Sources/Unfold/Resources/Characters/default-cat/spritesheet.png"
let outputURL = URL(fileURLWithPath: outputPath)

try! FileManager.default.createDirectory(
    at: outputURL.deletingLastPathComponent(),
    withIntermediateDirectories: true
)
try! png.write(to: outputURL)
print("wrote \(outputURL.path) (\(width)x\(height), \(columns)x\(rows) grid, frame=\(frameSize)px, \(totalFrames) frames used)")
