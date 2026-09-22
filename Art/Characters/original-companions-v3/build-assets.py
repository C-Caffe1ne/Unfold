"""Pack approved generated poses without resizing, repainting, or changing their RGBA pixels.

Requires Pillow. Run from any directory; output is limited to the three new pet IDs.
The source PNGs remain byte-for-byte image_gen output.
"""
import hashlib
import json
from pathlib import Path
import runpy

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PETS = ("puppy-dog", "hedgehog", "penguin")
# Reviewed gaps between complete animals in these particular 1254px source images.
# Uniform slicing would cut the hedgehog's stretching pose. Do not reuse these
# boundaries for another generated atlas without inspecting it first.
COLUMNS = (0, 340, 640, 940, 1254)
ROWS = (0, 340, 630, 930, 1254)
CELL = 256
BASELINE = 230


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


for pet_id in PETS:
    source = HERE / "source" / (pet_id + "-atlas.png")
    source_hash = sha256(source)
    with Image.open(source) as original:
        if original.mode != "RGBA" or original.size != (1254, 1254):
            raise ValueError(f"{pet_id}: unexpected source image; re-review the crop boundaries")
        atlas = Image.new("RGBA", (CELL * 4, CELL * 4), (0, 0, 0, 0))
        frames = []
        for index in range(16):
            column, row = index % 4, index // 4
            region_box = (COLUMNS[column], ROWS[row], COLUMNS[column + 1], ROWS[row + 1])
            region = original.crop(region_box)
            bounds = region.getchannel("A").point(lambda alpha: 255 if alpha >= 26 else 0).getbbox()
            if bounds is None:
                raise ValueError(f"{pet_id} frame {index}: empty pose")
            left, top, right, bottom = bounds
            # Retain the original antialiased fringe. Never cut into visible anatomy.
            if min(left, top, region.width - right, region.height - bottom) < 3:
                raise ValueError(f"{pet_id} frame {index}: pose touches a source boundary")
            crop_box = (left - 2, top - 2, right + 2, bottom + 2)
            pose = region.crop(crop_box)
            x, y = (CELL - pose.width) // 2, BASELINE - (bottom - crop_box[1])
            if x < 4 or y < 4 or x + pose.width > CELL - 4 or y + pose.height > CELL - 4:
                raise ValueError(f"{pet_id} frame {index}: pose does not fit; do not silently resize")
            # No mask argument: a masked paste would multiply alpha and alter edges.
            atlas.paste(pose, (column * CELL + x, row * CELL + y))
            frames.append({
                "index": index,
                "sourceBox": [region_box[0] + crop_box[0], region_box[1] + crop_box[1],
                              region_box[0] + crop_box[2], region_box[1] + crop_box[3]],
                "cellOffset": [x, y], "size": list(pose.size),
            })
        directory = ROOT / "Assets" / "Characters" / pet_id
        directory.mkdir(parents=True, exist_ok=True)
        runtime = directory / "spritesheet.png"
        atlas.save(runtime)
    if sha256(source) != source_hash:
        raise RuntimeError(f"{pet_id}: source changed during packing")
    ledger = {
        "id": pet_id, "contentVersion": "0.1.0",
        "sourceFile": str(source.relative_to(ROOT)), "sourceSha256": source_hash,
        "runtimeDirectory": str(directory.relative_to(ROOT)), "runtimeSha256": sha256(runtime),
        "createdWith": {"tool": "image_gen", "mode": "built-in", "date": "2026-09-22"},
        "promptRecord": "Art/Characters/original-companions-v3/generation.md",
        "sourceStatus": "original-rgba-png-preserved",
        "packing": {"tool": "Pillow", "method": "unscaled-rgba-crop-and-place",
                    "cellSize": CELL, "baseline": BASELINE, "frames": frames},
        "humanArtApproval": "pending",
    }
    (HERE / (pet_id + "-resource.json")).write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + "\n")
    print(f"{pet_id}: 16 poses packed; source preserved; runtime SHA256 {sha256(runtime)}")

runpy.run_path(str(HERE / "build-manifests.py"), run_name="__main__")
