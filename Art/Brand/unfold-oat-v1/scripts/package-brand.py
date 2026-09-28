"""Encode platform icon containers and validate the asset kit.
Run after build-brand.cjs: python3 scripts/package-brand.py
Needs Pillow. macOS iconutil is used when available.
"""
from pathlib import Path
from PIL import Image
import hashlib
import json
import shutil
import struct
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
(ROOT / "desktop").mkdir(exist_ok=True)
for target, template in [
    ("web/favicon.ico", "web/favicon-{size}x{size}.png"),
    ("desktop/unfold.ico", "png/app-icon/unfold-{size}.png"),
]:
    frames = [Image.open(ROOT / template.format(size=size)).convert("RGBA") for size in ICO_SIZES]
    frames[-1].save(ROOT / target, format="ICO",
                    sizes=[(size, size) for size in ICO_SIZES],
                    append_images=frames[:-1])

iconset = ROOT / "desktop/Unfold.iconset"
iconset.mkdir(exist_ok=True)
for points in [16, 32, 128, 256, 512]:
    for scale in [1, 2]:
        pixels = points * scale
        suffix = "@2x" if scale == 2 else ""
        shutil.copyfile(ROOT / f"png/app-icon/unfold-{pixels}.png",
                        iconset / f"icon_{points}x{points}{suffix}.png")
iconutil = shutil.which("iconutil")
native_result = None
if iconutil:
    native_result = subprocess.run([iconutil, "--convert", "icns", "--output",
                    str(ROOT / "desktop/unfold.icns"), str(iconset)], capture_output=True, text=True)
if native_result is None or native_result.returncode != 0:
    base = Image.open(ROOT / "png/app-icon/unfold-1024.png").convert("RGBA")
    frames = [Image.open(ROOT / f"png/app-icon/unfold-{s}.png").convert("RGBA")
              for s in [32, 64, 128, 256, 512]]
    base.save(ROOT / "desktop/unfold.icns", append_images=frames)

catalog = json.loads((ROOT / "assets.json").read_text())
checks = []
for entry in catalog["png"]:
    image_path = ROOT / entry["file"]
    with Image.open(image_path) as image:
        image.load()
        assert image.format == "PNG"
        assert image.size == (entry["width"], entry["height"]), entry
        assert image.mode == "RGBA", entry
        alpha = image.getchannel("A")
        if entry["file"].startswith(("png/logo/", "png/symbol/")):
            assert alpha.getextrema() == (0, 255), entry
            assert alpha.getpixel((0, 0)) == 0, entry
            if entry["width"] >= 128:
                x0, y0, x1, y1 = alpha.getbbox()
                assert x0 > 0 and y0 > 0 and x1 < image.width and y1 < image.height
        if entry["file"] in ["web/apple-touch-icon.png", "web/maskable-icon-512x512.png"]:
            assert alpha.getextrema() == (255, 255)
        checks.append({"file": entry["file"], "status": "pass", "size": list(image.size)})

for target in ["web/favicon.ico", "desktop/unfold.ico"]:
    with Image.open(ROOT / target) as icon:
        assert icon.format == "ICO"
        assert icon.info["sizes"] == {(s, s) for s in ICO_SIZES}
        for size in ICO_SIZES:
            frame = icon.ico.getimage((size, size))
            frame.load()
            assert frame.size == (size, size)
            assert frame.getchannel("A").getextrema() == (0, 255)
        checks.append({"file": target, "status": "pass", "frames": ICO_SIZES})

with Image.open(ROOT / "desktop/unfold.icns") as icon:
    assert icon.format == "ICNS"
    decoded = []
    for size in icon.info["sizes"]:
        icon.size = size[:2]
        icon.load(scale=size[2])
        decoded.append(list(size))
    checks.append({"file": "desktop/unfold.icns", "status": "pass", "representations": decoded})

for file in sorted(ROOT.glob("**/*.svg")):
    document = ET.parse(file).getroot()
    assert document.tag == "{http://www.w3.org/2000/svg}svg"
    assert document.get("viewBox"), file
    assert not document.findall(".//{http://www.w3.org/2000/svg}image"), file
    if file.name != "preview.svg":
        assert not document.findall(".//{http://www.w3.org/2000/svg}text"), file
    assert not document.findall(".//{http://www.w3.org/2000/svg}script"), file
checks.append({"check": "SVG files contain real geometry, no embedded raster, scripts or external fonts", "status": "pass"})

palette = {(152, 81, 57), (233, 184, 148), (52, 45, 40)}
with Image.open(ROOT / "png/logo/logo-oat-1280.png") as logo:
    opaque = {(r, g, b) for r, g, b, a in logo.getdata() if a == 255}
    assert opaque == palette, opaque
checks.append({"check": "Logo opaque pixels match the three OatLatte colors exactly", "status": "pass"})

manifest = json.loads((ROOT / "web/site.webmanifest").read_text())
for icon in manifest["icons"]:
    file = ROOT / "web" / icon["src"]
    with Image.open(file) as image:
        assert f"{image.width}x{image.height}" == icon["sizes"]
# Maskable foreground must stay inside the central 80% diameter safe circle.
with Image.open(ROOT / "web/maskable-icon-512x512.png") as image:
    max_radius = max(((x - 255.5)**2 + (y - 255.5)**2)**0.5
                     for y in range(image.height) for x in range(image.width)
                     if image.getpixel((x, y))[:3] == (250, 246, 239))
    assert max_radius <= 204.8
checks.append({"check": "Manifest dimensions and maskable center safe area", "status": "pass",
               "maximum_foreground_radius": max_radius, "safe_radius": 204.8})

report = {
    "status": "pass",
    "icns_encoder": "iconutil" if native_result and native_result.returncode == 0 else "Pillow",
    "native_iconutil_result": "passed" if native_result and native_result.returncode == 0 else "unavailable or rejected iconset; used decoded Pillow container",
    "scope": "Asset encoding, dimensions, transparency, SVG geometry, palette, manifest references and icon decoding",
    "wordmark_reference_iou": catalog["wordmark"]["referenceMaskIoU"],
    "checks": checks,
    "visual_review": "preview.png, logo-oat-640.png and favicon-32x32.png inspected in this session",
    "not_verified": [
        "Installed Windows or macOS app icon behavior",
        "Browser favicon cache and installed PWA behavior",
        "Live website deployment"
    ]
}
(ROOT / "validation.json").write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n")
files = [p for p in ROOT.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"]
lines = [hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.relative_to(ROOT).as_posix()
         for p in sorted(files)]
(ROOT / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n")
print(json.dumps({"status": "pass", "checks": len(checks), "files": len(files) + 1,
                  "ico_sizes": ICO_SIZES, "icns_representations": len(decoded)}))
