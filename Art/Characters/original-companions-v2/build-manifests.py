"""Write clip timelines. The generated PNG originals are never resampled or repainted."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent


def clip(frames, fps=6, loop=False):
    return {"frames": frames, "fps": fps, "loop": loop}


for character_id, name, source, version in [
    ("default-cat", "Mochi", "mochi-atlas.png", "2.0.0"),
    ("bori-rabbit", "보리", "bori-atlas.png", "0.2.0"),
]:
    # Return to the same neutral frame at the ends of gestures. Bori's generated
    # stretch cell 10 faces the other way, so use the consistent right-facing 9.
    stretch = [0, 9, 9, 10, 10, 10, 10, 9, 0] if character_id == "default-cat" else [0, 9, 9, 9, 9, 9, 9, 0]
    manifest = {
        "id": character_id, "name": name, "version": 1, "renderStyle": "smooth",
        "behaviorProfile": "unfold-original-v1",
        "spriteSheet": {"file": "spritesheet.png", "columns": 4, "rows": 6, "frameWidth": 256, "frameHeight": 256},
        "animations": {
            "idle": clip([0, 0, 0, 0, 0, 1, 0, 0], 2, True),
            "attention": clip([0, 8, 8, 8, 0], 5),
            "stretch": clip(stretch, 4),
            "celebrate": clip([0, 12, 12, 12, 12, 0], 6),
            "click": clip([13, 13, 14, 14, 13, 15, 15, 0], 5),
            "sleep": clip([4, 4, 5, 5, 6, 6, 5, 5, 6, 6, 5, 5, 4, 0], 2),
            "look": clip([0, 2, 2, 0, 3, 3, 0], 4),
            "yawn": clip([0, 4, 7, 7, 7, 4, 0], 4),
            "sulk": clip([17, 17, 16, 16, 16, 17, 0], 3),
            # Use only isolated cells: Bori 18/19 include next-row ear fragments.
            "walk": clip([18, 19, 20, 21] if character_id == "default-cat" else [20, 21, 20, 21], 7, True),
        },
    }
    directory = ROOT / "Assets" / "Characters" / character_id
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "character.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    data = (HERE / "source" / source).read_bytes()
    (directory / "spritesheet.png").write_bytes(data)
    ledger = {
        "id": character_id, "contentVersion": version,
        "runtimeDirectory": str(directory.relative_to(ROOT)),
        "sourceFile": str((HERE / "source" / source).relative_to(ROOT)),
        "sourceSha256": hashlib.sha256(data).hexdigest(),
        "createdWith": {"tool": "image_gen", "mode": "built-in", "date": "2026-09-22"},
        "promptRecord": "Art/Characters/original-companions-v2/generation.md",
        "sourceStatus": "original-rgba-png-preserved", "humanArtApproval": "pending",
        "notes": "256px cells; runtime aligns visible baseline without altering original pixels. No layered Aseprite source or rights certification is claimed.",
    }
    (HERE / (character_id + "-resource.json")).write_text(json.dumps(ledger, ensure_ascii=False, indent=2) + "\n")
