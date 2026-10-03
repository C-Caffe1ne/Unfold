#!/usr/bin/env python3
"""Package an already published Windows app on hosts without PowerShell."""
import argparse
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("runtime", choices=("win-x64", "win-arm64"), nargs="?", default="win-x64")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
version = ET.parse(root / "src/Unfold.Desktop/Unfold.Desktop.csproj").findtext("PropertyGroup/Version")
if not version or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", version):
    raise SystemExit("Invalid project version.")
package = root / "artifacts" / args.runtime
payload = package / "app"
if package.is_symlink() or payload.is_symlink() or not (payload / "Unfold.exe").is_file():
    raise SystemExit("Publish the matching Windows app into artifacts/<runtime>/app first.")
shutil.copyfile(root / "docs/cross-platform.md", payload / "README.md")
shutil.copyfile(root / "THIRD-PARTY-NOTICES.md", payload / "THIRD-PARTY-NOTICES.md")
notes = root / f"docs/releases/v{version}.md"
if notes.is_file():
    shutil.copyfile(notes, package / "RELEASE-NOTES.md")

# Read the canonical launcher and instructions, keeping the PowerShell and Python paths identical.
script = (root / "Scripts/publish-desktop.ps1").read_text(encoding="utf-8")
for variable, name in (("launcher", "Unfold.cmd"), ("readme", "README.txt")):
    match = re.search(rf"Set-Content -LiteralPath \${variable} -Encoding UTF8 -Value @'\n(.*?)\n'@", script, re.S)
    if not match:
        raise SystemExit(f"Missing canonical {variable} in publish-desktop.ps1.")
    (package / name).write_bytes((match[1].replace("\r\n", "\n").replace("\n", "\r\n") + "\r\n").encode("utf-8"))

archive = root / f"artifacts/Unfold-v{version}-{args.runtime}.zip"
temporary = archive.with_suffix(".zip.tmp")
try:
    with ZipFile(temporary, "w", ZIP_DEFLATED, compresslevel=6) as output:
        for path in sorted(package.rglob("*")):
            if path.is_symlink():
                raise SystemExit(f"Refusing linked package entry: {path}")
            if path.is_file():
                output.write(path, path.relative_to(package.parent).as_posix())
    temporary.replace(archive)
finally:
    temporary.unlink(missing_ok=True)
print(archive)
