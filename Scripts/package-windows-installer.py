#!/usr/bin/env python3
"""Compile a per-user Windows wizard from the matching self-contained publish output."""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--compiler", help="Path to NSIS 3 makensis.exe or makensis")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    version = ET.parse(root / "src/Unfold.Desktop/Unfold.Desktop.csproj").findtext("PropertyGroup/Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+-beta", version):
        raise SystemExit("Expected a beta version in the project.")
    payload = root / "artifacts/win-x64/app"
    for file in ["Unfold.exe", "Unfold.dll", "Unfold.runtimeconfig.json", "coreclr.dll", "Tools/ffmpeg.exe", "Licenses/NoonnuBasicGothic.txt"]:
        if not (payload / file).is_file():
            raise SystemExit("Missing self-contained Windows payload: " + file)
    files = sorted(path for path in payload.rglob("*") if path.is_file())
    if payload.is_symlink() or payload.parent.is_symlink() or any(path.is_symlink() for path in payload.rglob("*")):
        raise SystemExit("Refusing linked Windows payload.")
    if any(path.suffix.lower() in (".log", ".trx") or path.name.startswith(".env") for path in files):
        raise SystemExit("Unexpected local data in Windows payload.")
    exe = (payload / "Unfold.exe").read_bytes()
    offset = int.from_bytes(exe[0x3c:0x40], "little")
    if exe[offset:offset+6] != b"PE\x00\x00\x64\x86":
        raise SystemExit("Expected the Windows AMD64 app host.")
    notes = root / f"docs/releases/v{version}.md"
    if not notes.is_file():
        raise SystemExit("Release notes are required.")
    shutil.copyfile(notes, payload / "RELEASE-NOTES.md")
    shutil.copyfile(root / "docs/cross-platform.md", payload / "README.md")
    shutil.copyfile(root / "THIRD-PARTY-NOTICES.md", payload / "THIRD-PARTY-NOTICES.md")
    compiler = args.compiler or shutil.which("makensis") or shutil.which("makensis.exe")
    if not compiler:
        raise SystemExit("Install NSIS 3 or pass --compiler <makensis.exe>.")
    output = root / f"artifacts/Unfold-v{version}-win-x64-setup.exe"
    switch = "/" if os.name == "nt" else "-"
    subprocess.run([compiler, switch + "V3", switch + "WX", f"{switch}DAPP_VERSION={version}", f"{switch}DAPP_NUMERIC_VERSION={version.split('-')[0]}",
                    f"{switch}DPAYLOAD={payload}", f"{switch}DOUTPUT={output}",
                    f"{switch}DAPP_ICON={root / 'Art/Brand/unfold-lilac-v1/desktop/unfold.ico'}",
                    str(root / "Packaging/Unfold.Windows.nsi")], check=True)
    # Internal evidence only; do not upload the build-input inventory as a public asset.
    manifest = {"version": version, "installer": output.name, "sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
                "payload": {path.relative_to(payload).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
                            for path in sorted(payload.rglob("*")) if path.is_file()}}
    (root / f"artifacts/Unfold-v{version}-win-x64-installer-inputs.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(output)

if __name__ == "__main__":
    main()
