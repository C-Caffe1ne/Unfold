"""Export the website animation from Unfold's actual sprite/manifest (Pillow).

No artwork is redrawn. WebP keeps soft alpha edges; GIF reserves its own
transparent index instead of accidentally treating a pet color as transparent.
"""
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Characters/default-cat"
OUTPUT = ROOT / "web/assets"


def export():
    manifest = json.loads((SOURCE / "character.json").read_text())
    sheet = manifest["spriteSheet"]
    clip = manifest["animations"]["stretch"]
    sprite = Image.open(SOURCE / sheet["file"]).convert("RGBA")
    width, height = sheet["frameWidth"], sheet["frameHeight"]
    frames = []
    for index in clip["frames"]:
        x, y = index % sheet["columns"] * width, index // sheet["columns"] * height
        frames.append(sprite.crop((x, y, x + width, y + height)))
    duration = round(1000 / clip["fps"])
    frames[0].save(OUTPUT / "pet-rest.png", optimize=True)
    frames[0].save(OUTPUT / "pet-stretch.webp", save_all=True,
                   append_images=frames[1:], duration=duration, loop=0,
                   lossless=True, method=6)

    # A shared 255-color palette leaves index 255 exclusively for transparency.
    atlas = Image.new("RGB", (width * len(frames), height), "white")
    for index, frame in enumerate(frames):
        atlas.paste(frame, (index * width, 0), frame.getchannel("A"))
    palette = atlas.quantize(colors=255)
    indexed = []
    for frame in frames:
        encoded = frame.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE)
        mask = frame.getchannel("A").point(lambda alpha: 255 if alpha < 128 else 0)
        encoded.paste(255, mask=mask)
        encoded.info["transparency"] = 255
        indexed.append(encoded)
    indexed[0].save(OUTPUT / "pet-stretch.gif", save_all=True,
                    append_images=indexed[1:], duration=duration, loop=0,
                    transparency=255, disposal=2, optimize=False)
    print(f"Exported {len(frames)} timeline frames, {duration * len(frames)} ms")


if __name__ == "__main__":
    export()
