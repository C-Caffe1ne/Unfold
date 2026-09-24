"""Append generated pointer poses; preserve every existing runtime sprite pixel.

Requires Pillow. The five image_gen sources are preserved byte-for-byte. Only
crop, uniform resampling and atlas placement happen here; no anatomy is drawn.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CELL = 256
PETS = ("default-cat", "bori-rabbit", "puppy-dog", "hedgehog", "penguin")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


for pet_id in PETS:
    source = HERE / "source" / f"{pet_id}-pointer.png"
    base = HERE / "base" / pet_id
    manifest = json.loads((base / "character.json").read_text())
    old = Image.open(base / "spritesheet.png").convert("RGBA")
    image = Image.open(source)
    if image.mode != "RGBA" or image.size != (1774, 887):
        raise ValueError(f"{pet_id}: source dimensions changed; inspect its grid again")
    mask = image.getchannel("A").point(lambda alpha: 255 if alpha >= 26 else 0)
    gaps = []
    for y in range(310, 575):
        if mask.crop((0, y, image.width, y + 1)).getbbox() is not None:
            continue
        if not gaps or y != gaps[-1][-1] + 1:
            gaps.append([])
        gaps[-1].append(y)
    gap = max(gaps, key=len)
    if len(gap) < 12:
        raise ValueError(f"{pet_id}: no safe separation between the two pose rows")
    split = (gap[0] + gap[-1]) // 2
    crops, boxes = [], []
    for index in range(8):
        column, row = index % 4, index // 4
        box = (column * image.width // 4, 0 if row == 0 else split,
               (column + 1) * image.width // 4, split if row == 0 else image.height)
        region = image.crop(box)
        bounds = region.getchannel("A").point(lambda alpha: 255 if alpha >= 26 else 0).getbbox()
        if bounds is None or min(bounds[0], bounds[1], region.width - bounds[2], region.height - bounds[3]) < 4:
            raise ValueError(f"{pet_id} pose {index}: missing or clipped source anatomy")
        crop = (bounds[0] - 2, bounds[1] - 2, bounds[2] + 2, bounds[3] + 2)
        crops.append(region.crop(crop))
        boxes.append([box[0] + crop[0], box[1] + crop[1], box[0] + crop[2], box[1] + crop[3]])
    # One factor for the entire new sheet prevents breathing-size jumps. Leave
    # enough headroom for the single bounce; preserve the unscaled source too.
    scale = min(190 / max(p.height for p in crops), 216 / max(p.width for p in crops))
    atlas = Image.new("RGBA", (old.width, old.height + CELL * 2))
    atlas.paste(old, (0, 0))
    offsets = []
    for index, crop in enumerate(crops):
        pose = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.Resampling.LANCZOS)
        x, y = (CELL - pose.width) // 2, 230 - pose.height
        atlas.paste(pose, (index % 4 * CELL + x, old.height + index // 4 * CELL + y))
        offsets.append([x, y])
    first = manifest["spriteSheet"]["columns"] * manifest["spriteSheet"]["rows"]
    manifest["spriteSheet"]["rows"] += 2
    manifest["animations"].update({
        "pickup": {"frames": [0, first, first + 1, first + 2], "fps": 12, "loop": False},
        "held": {"frames": [first + 2, first + 2, first + 3, first + 2], "fps": 2, "loop": True},
        "land": {"frames": [first + 4, first + 5, first + 6, 0], "fps": 10, "loop": False},
    })
    target = ROOT / "Assets" / "Characters" / pet_id
    atlas.save(target / "spritesheet.png")
    (target / "character.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    ledger = {"id": pet_id, "contentVersion": {"default-cat": "2.1.0", "bori-rabbit": "0.3.0"}.get(pet_id, "0.2.0"),
              "source": str(source.relative_to(ROOT)), "sourceSha256": digest(source),
              "baseSpriteSha256": digest(base / "spritesheet.png"), "runtimeSha256": digest(target / "spritesheet.png"),
              "sourceBoxes": boxes, "uniformScale": scale, "cellOffsets": offsets,
              "createdWith": "built-in image_gen", "promptRecord": "generation.md",
              "originalRowsPreserved": True, "newKeys": ["pickup", "held", "land"]}
    (HERE / f"{pet_id}-resource.json").write_text(json.dumps(ledger, indent=2) + "\n")
    assert atlas.crop((0, 0, old.width, old.height)).tobytes() == old.tobytes()
    print(f"{pet_id}: 8 poses, scale={scale:.4f}, original pixels preserved")
