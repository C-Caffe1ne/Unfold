#!/usr/bin/env python3
"""Build editable, source-derived vector poses; never modifies runtime assets.

Requires Pillow, numpy and VTracer 1.0.0-alpha.4 (c83de444).
No PNG is embedded in an SVG. The rig uses authored pose switching, not invented
skeletal in-betweens: the source service is a frame-animation system.
"""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
PETS = ['default-cat', 'bori-rabbit', 'puppy-dog', 'hedgehog', 'penguin']
NS = '{http://www.w3.org/2000/svg}'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build_pose(job):
    pet, index, executable = job
    source = ROOT / 'Assets/Characters' / pet
    sheet = Image.open(source / 'spritesheet.png').convert('RGBA')
    x, y = index % 4 * 256, index // 4 * 256
    frame = sheet.crop((x, y, x + 256, y + 256))
    a = np.array(frame)
    ys, xs = np.where(a[:, :, 3] >= 26)
    dx = (256 - int(xs.min()) - int(xs.max()) - 1) / 2
    dy = 230.4 - int(ys.max()) - 1
    out = HERE / pet
    (out / 'poses').mkdir(parents=True, exist_ok=True)
    (out / 'reference').mkdir(exist_ok=True)
    frame.save(out / 'reference' / f'pose-{index:02}.png')
    with tempfile.TemporaryDirectory(prefix='unfold-vector-') as temp:
        temp = Path(temp)
        enlarged = np.array(frame.resize((1024, 1024), Image.Resampling.LANCZOS)
                            .filter(ImageFilter.GaussianBlur(1.5)))
        enlarged[enlarged[:, :, 3] < 128] = 0
        enlarged[enlarged[:, :, 3] >= 128, 3] = 255
        Image.fromarray(enlarged).save(temp / 'input.png')
        subprocess.run([executable, str(temp / 'input.png'), str(temp / 'trace.svg'),
                        '--hierarchical', 'cutout', '--mode', 'spline',
                        '--filter-speckle', '6', '--max-colors', '64',
                        '--color-precision', '7', '--gradient-step', '16',
                        '--simplify', '2', '--path-precision', '2', '--optimize', '0'],
                       check=True, capture_output=True)
        root = ET.parse(temp / 'trace.svg').getroot()
        colors = {}
        for path in root:
            assert path.tag == NS + 'path', path.tag
            assert not path.get('transform'), 'Expected absolute path coordinates'
            colors.setdefault(path.get('fill'), []).append(path.get('d'))
    paths = []
    for i, (color, contours) in enumerate(colors.items()):
        paths.append(f'<path id="tone-{i:03}" d="{" ".join(contours)}" fill="{color}" '
                     f'stroke="{color}" stroke-width="1.2" stroke-linejoin="round"/>')
    content = '\n'.join(paths)
    document = (f'<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" '
                f'viewBox="0 0 256 256"><title>{pet} source pose {index}</title>'
                f'<g id="source-pose-{index:02}" transform="scale(0.25)">{content}</g></svg>')
    (out / 'poses' / f'pose-{index:02}.svg').write_text(document)
    print(f'{pet} pose {index:02}: {len(colors)} compound paths', flush=True)
    return index, {'offset': [dx, dy], 'paths': len(colors), 'svg': f'poses/pose-{index:02}.svg'}, content


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--vtracer', required=True)
    args = parser.parse_args()
    contracts = {}
    for pet in PETS:
        source = ROOT / 'Assets/Characters' / pet
        manifest = json.loads((source / 'character.json').read_text())
        indices = sorted({i for clip in manifest['animations'].values() for i in clip['frames']})
        contracts[pet] = {'id': pet, 'name': manifest['name'], 'canvas': [256, 256],
                          'sourceHashes': {name: sha(source / name) for name in ['character.json', 'spritesheet.png']},
                          'animations': manifest['animations'], 'poses': {}}
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
            results = list(executor.map(build_pose, [(pet, i, args.vtracer) for i in indices]))
        groups = []
        for i, info, paths in sorted(results):
            contracts[pet]['poses'][str(i)] = info
            dx, dy = info['offset']
            groups.append(f'<g id="pose_{i:02}" opacity="{1 if i == 0 else 0}">'
                          f'<g id="alignment_{i:02}" transform="translate({dx:.2f} {dy:.2f}) scale(0.25)">'
                          f'{paths}</g></g>')
        bundle = ('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">'
                  f'<title>{pet} source-faithful pose rig</title><g id="source_poses">' + ''.join(groups) + '</g></svg>')
        (HERE / pet / 'rig.svg').write_text(bundle)
    (HERE / 'rig-contract.json').write_text(json.dumps(contracts, indent=2) + '\n')


if __name__ == '__main__':
    main()
