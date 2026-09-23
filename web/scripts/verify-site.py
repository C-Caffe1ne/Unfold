"""Local link/ARIA/media regression checks. Run with Python 3 + Pillow."""
import json
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

from PIL import Image, ImageSequence

WEB = Path(__file__).resolve().parents[1]
ROOT = WEB.parent


class Page(HTMLParser):
    def __init__(self, path):
        super().__init__()
        self.path, self.nodes = path, []
        self.feed(path.read_text())
        ids = [attrs["id"] for _, attrs in self.nodes if "id" in attrs]
        assert all(count == 1 for count in Counter(ids).values()), f"Duplicate id: {path}"
        self.ids = set(ids)

    def handle_starttag(self, tag, attrs):
        self.nodes.append((tag, dict(attrs)))


pages = {path.resolve(): Page(path) for path in WEB.glob("*.html")}
link_count = 0
for path, page in pages.items():
    for tag, attrs in page.nodes:
        for attribute in ("aria-controls", "aria-labelledby", "aria-describedby"):
            for target_id in attrs.get(attribute, "").split():
                assert target_id in page.ids, (path.name, attribute, target_id)
        if tag == "img":
            assert "alt" in attrs, (path.name, "missing alt")
            if "src" in attrs:
                with Image.open(path.parent / attrs["src"]) as img:
                    assert img.width * int(attrs["height"]) == img.height * int(attrs["width"]), (path.name, attrs["src"], "aspect ratio")
        for attribute in ("href", "src", "data-animation", "data-still"):
            if attribute not in attrs:
                continue
            url = urlsplit(attrs[attribute])
            if url.scheme or url.netloc:
                continue
            target = (path.parent / unquote(url.path)).resolve() if url.path else path
            assert target.is_file(), (path.name, "missing asset", attrs[attribute])
            if url.fragment and target in pages:
                assert unquote(url.fragment) in pages[target].ids, (path.name, "missing anchor", attrs[attribute])
            link_count += 1

manifest = json.loads((ROOT / "Assets/Characters/default-cat/character.json").read_text())
clip, sheet = manifest["animations"]["stretch"], manifest["spriteSheet"]
sprite = Image.open(ROOT / "Assets/Characters/default-cat" / sheet["file"]).convert("RGBA")
expected_duration = round(1000 / clip["fps"]) * len(clip["frames"])
frame_duration = round(1000 / clip["fps"])
for name in ("pet-stretch.gif", "pet-stretch.webp"):
    elapsed, frames = 0, 0
    with Image.open(WEB / "assets" / name) as animation:
        assert animation.info["loop"] == 0
        for encoded in ImageSequence.Iterator(animation):
            decoded = encoded.convert("RGBA")
            index = clip["frames"][elapsed // frame_duration]
            x = index % sheet["columns"] * sheet["frameWidth"]
            y = index // sheet["columns"] * sheet["frameHeight"]
            expected = sprite.crop((x, y, x + sheet["frameWidth"], y + sheet["frameHeight"]))
            assert decoded.size == expected.size
            source_alpha = list(expected.getchannel("A").getdata())
            actual_alpha = list(decoded.getchannel("A").getdata())
            if name.endswith(".webp"):
                assert source_alpha == actual_alpha, "WebP must preserve soft alpha"
                assert all(a == b or a[3] == b[3] == 0 for a, b in zip(expected.getdata(), decoded.getdata())), "WebP must preserve visible pixels"
            else:
                assert all(actual == (255 if source >= 128 else 0) for source, actual in zip(source_alpha, actual_alpha)), "GIF must use explicit alpha threshold"
            elapsed += encoded.info["duration"]
            frames += 1
        assert elapsed == expected_duration, (name, elapsed, expected_duration)
    print(f"PASS {name}: {frames} encoded frames, {elapsed} ms, alpha verified")
print(f"PASS {len(pages)} HTML pages; {link_count} local references; ids, ARIA, alt and aspect ratios")
