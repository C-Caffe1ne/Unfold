"""Split pose libraries below Rive's 10 MiB SVG-asset limit."""
from pathlib import Path
import xml.etree.ElementTree as E
import json

ROOT = Path(__file__).resolve().parent
E.register_namespace('', 'http://www.w3.org/2000/svg')
report = {}
for pet in json.loads((ROOT / 'rig-contract.json').read_text()):
    root = E.parse(ROOT / pet / 'rig.svg').getroot()
    poses = list(root.find('{http://www.w3.org/2000/svg}g'))
    batches, current, size = [], [], 0
    for pose in poses:
        content = E.tostring(pose, encoding='unicode')
        if current and size + len(content) > 6_000_000:
            batches.append(current)
            current, size = [], 0
        current.append(content)
        size += len(content)
    if current:
        batches.append(current)
    report[pet] = []
    for index, batch in enumerate(batches):
        file = ROOT / pet / f'import-{index + 1}.svg'
        file.write_text('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">' + ''.join(batch) + '</svg>')
        report[pet].append(str(file.relative_to(ROOT)))
(ROOT / 'import-files.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
