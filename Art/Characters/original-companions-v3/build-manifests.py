"""Write the three new pets' behavior timelines; never overwrite the older pets."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def clip(frames, fps=6, loop=False):
    return {"frames": frames, "fps": fps, "loop": loop}


for character_id, name in [("puppy-dog", "강아지"), ("hedgehog", "고슴도치"), ("penguin", "펭귄")]:
    manifest = {
        "id": character_id, "name": name, "version": 1,
        "renderStyle": "smooth", "behaviorProfile": "unfold-original-v1",
        "spriteSheet": {"file": "spritesheet.png", "columns": 4, "rows": 4,
                        "frameWidth": 256, "frameHeight": 256},
        "animations": {
            "idle": clip([0, 0, 0, 0, 0, 1, 0, 0], 2, True),
            "attention": clip([0, 7, 7, 7, 0], 5),
            "stretch": clip([0, 8, 8, 8, 8, 8, 8, 0], 4),
            "celebrate": clip([0, 9, 9, 9, 9, 0], 6),
            "click": clip([10, 10, 2, 2, 10, 3, 3, 0], 5),
            "sleep": clip([4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 4, 0], 2),
            "look": clip([0, 2, 2, 0, 3, 3, 0], 4),
            "yawn": clip([0, 4, 6, 6, 6, 4, 0], 4),
            "sulk": clip([11, 11, 11, 11, 11, 11, 0], 3),
            "walk": clip([12, 13, 14, 15], 7, True),
        },
    }
    directory = ROOT / "Assets" / "Characters" / character_id
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "character.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
