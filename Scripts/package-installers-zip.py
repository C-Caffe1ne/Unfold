#!/usr/bin/env python3
"""Wrap the installer EXE and Mac DMGs in separately downloadable ZIP archives."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--notarized-macos", action="store_true",
                        help="Validate notarized Mac DMGs, package them under new names, and retain the existing Windows ZIP.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    version = ET.parse(root / "src/Unfold.Desktop/Unfold.Desktop.csproj").findtext("PropertyGroup/Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+-beta", version):
        raise SystemExit("Expected a beta version in the project.")
    artifacts = root / "artifacts"
    notes = root / f"docs/releases/v{version}.md"
    if not notes.is_file():
        raise SystemExit("Release notes are required.")
    assets = []
    runtimes = ("osx-arm64", "osx-x64") if args.notarized_macos else ("osx-arm64", "osx-x64", "win-x64")
    for rid in runtimes:
        suffix = "-setup.exe" if rid == "win-x64" else ".dmg"
        installer = artifacts / f"Unfold-v{version}-{rid}{suffix}"
        if not installer.is_file() or installer.is_symlink():
            raise SystemExit(f"Missing installer: {installer.name}")
        with installer.open("rb") as stream:
            if rid == "win-x64":
                valid = stream.read(2) == b"MZ"
            else:
                stream.seek(-512, 2)
                valid = stream.read(4) == b"koly"
        if not valid:
            raise SystemExit(f"Invalid installer format: {installer.name}")
        if args.notarized_macos:
            # A filename or signing identity alone is not evidence of acceptance.
            subprocess.run(["codesign", "--verify", "--strict", str(installer)], check=True)
            subprocess.run(["xcrun", "stapler", "validate", str(installer)], check=True, stdout=sys.stderr)
            subprocess.run(["spctl", "--assess", "--type", "open", "--context",
                            "context:primary-signature", str(installer)], check=True)
        guide = f"Unfold Beta v{version.split('-')[0]} 설치\n\n"
        if rid == "win-x64":
            guide += f"1. ZIP 압축을 푼 뒤 {installer.name}을 실행하세요.\n2. 설치 마법사의 안내에 따라 설치하세요.\n"
        else:
            guide += f"1. ZIP 압축을 푼 뒤 {installer.name}을 여세요.\n2. Unfold.app을 Applications 폴더로 드래그하세요.\n3. 복사한 앱을 실행하고 디스크를 추출하세요.\n"
        guide += "\n업데이트 전에는 실행 중인 Unfold를 완전히 종료하세요.\n앱에서 Google 로그인 후 전달받은 테스터 코드를 입력하세요.\n기존 설정·휴식 기록·커스텀 펫은 별도 데이터 폴더에 보관됩니다.\n"
        if args.notarized_macos:
            guide += "Developer ID 서명과 Apple 공증을 완료한 Mac 배포 파일입니다.\n앱과 DMG에 공증 티켓이 포함됩니다. 최초 실행 시 macOS의 일반적인 열기 확인창이 표시될 수 있습니다.\n"
        else:
            guide += "서명·공증을 완료하지 않은 테스터용 베타이므로 OS 보안 경고가 나타날 수 있습니다.\n"
        variant = "-notarized" if args.notarized_macos else ""
        name = f"Unfold-v{version}-{rid}{variant}-installer.zip"
        archive = artifacts / name
        temporary = archive.with_suffix(".zip.tmp")
        try:
            with ZipFile(temporary, "w", ZIP_DEFLATED, compresslevel=6) as output:
                output.write(installer, installer.name)
                output.writestr("INSTALL.txt", guide)
                output.write(notes, "RELEASE-NOTES.md")
            with ZipFile(temporary) as output:
                if output.testzip() is not None:
                    raise SystemExit(f"Archive CRC failed: {name}")
                expected = [installer.name, "INSTALL.txt", "RELEASE-NOTES.md"]
                if output.namelist() != expected or hashlib.sha256(output.read(installer.name)).digest() != hashlib.sha256(installer.read_bytes()).digest():
                    raise SystemExit(f"Archive contents differ from installer: {name}")
            temporary.replace(archive)
        finally:
            temporary.unlink(missing_ok=True)
        assets.append({"name": name, "runtime": rid, "bytes": archive.stat().st_size,
                       "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(), "installer": installer.name,
                       "installerSha256": hashlib.sha256(installer.read_bytes()).hexdigest()})
    if args.notarized_macos:
        # Preserve the published Windows installer byte for byte; the combined
        # checksum still covers all three downloads shown on the website.
        name = f"Unfold-v{version}-win-x64-installer.zip"
        archive = artifacts / name
        if not archive.is_file() or archive.is_symlink():
            raise SystemExit(f"Existing Windows ZIP is required: {name}")
        with ZipFile(archive) as existing:
            installer_name = f"Unfold-v{version}-win-x64-setup.exe"
            if existing.testzip() is not None:
                raise SystemExit("Existing Windows ZIP failed CRC validation")
            installer_bytes = existing.read(installer_name)
            if installer_bytes[:2] != b"MZ":
                raise SystemExit("Invalid existing Windows installer")
        assets.append({"name": name, "runtime": "win-x64", "bytes": archive.stat().st_size,
                       "sha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                       "installer": installer_name, "installerSha256": hashlib.sha256(installer_bytes).hexdigest()})
    variant = "-notarized" if args.notarized_macos else ""
    checksum = artifacts / f"Unfold-v{version}{variant}-ZIP-SHA256SUMS.txt"
    checksum.write_text("".join(a["sha256"] + "  " + a["name"] + "\n" for a in assets), encoding="utf-8")
    # Internal evidence, never part of a public release.
    (artifacts / f"Unfold-v{version}{variant}-installer-zip-inputs.json").write_text(json.dumps(assets, indent=2), encoding="utf-8")
    print(json.dumps(assets, indent=2))
    print(checksum)


if __name__ == "__main__":
    main()
