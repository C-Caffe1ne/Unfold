#!/usr/bin/env python3
"""Create installable Velopack releases and matching update feeds without publishing them."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

PACKAGE_ID = "DokhuStudio.Unfold.Updates"

def seal_local_mac_release(root, output, feed, data, portable, version):
    """Local ad-hoc builds must not enable hardened runtime without a Team ID.

    Velopack always enables it when a signing identity is supplied. Seal the
    completed bundle locally, preserving Velopack's symlink encoding in the
    full package, then update the feed hashes. Production uses Developer ID.
    """
    full = next(a for a in data["Assets"] if a["Type"] == "Full" and a["Version"] == version)
    package = output / full["FileName"]
    with tempfile.TemporaryDirectory(prefix="unfold-local-update-sign-") as scratch:
        subprocess.run(["ditto", "-x", "-k", str(portable), scratch], check=True)
        bundles = list(Path(scratch).glob("*.app"))
        if len(bundles) != 1:
            raise SystemExit("Expected one application bundle")
        app = bundles[0]
        subprocess.run(["codesign", "--force", "--sign", "-", str(app / "Contents/MacOS/UpdateMac")], check=True)
        subprocess.run(["codesign", "--force", "--sign", "-", "--entitlements",
                        str(root / "Packaging/Unfold.Desktop.entitlements"), str(app)], check=True)
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        temporary = package.with_suffix(".nupkg.tmp")
        with ZipFile(package) as original, ZipFile(temporary, "w", ZIP_DEFLATED) as archive:
            for entry in original.infolist():
                if not entry.filename.startswith("lib/app/"):
                    archive.writestr(entry, original.read(entry))
            for path in sorted(app.rglob("*")):
                name = "lib/app/" + path.relative_to(app).as_posix()
                if path.is_symlink():
                    target = os.readlink(path)
                    if os.path.isabs(target) or not path.resolve().is_relative_to(app.resolve()):
                        raise SystemExit("Application link escapes the bundle")
                    archive.writestr(name + ".__symlink", target)
                elif path.is_file():
                    entry = ZipInfo(name); entry.create_system = 3
                    entry.external_attr = path.stat().st_mode << 16
                    entry.compress_type = ZIP_DEFLATED
                    archive.writestr(entry, path.read_bytes())
        temporary.replace(package)
        portable.unlink()
        subprocess.run(["ditto", "-c", "-k", "--keepParent", str(app), str(portable)], check=True)
    payload = package.read_bytes()
    full.update(Size=len(payload), SHA1=hashlib.sha1(payload).hexdigest().upper(),
                SHA256=hashlib.sha256(payload).hexdigest().upper())
    feed.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, choices=("win-x64", "win-arm64", "osx-arm64", "osx-x64"))
    parser.add_argument("--payload", type=Path, help="Published Windows app directory or macOS .app bundle")
    parser.add_argument("--output", type=Path, help="Feed/asset output directory")
    parser.add_argument("--vpk", type=Path, help="Explicit Velopack 1.2.0 tool, otherwise use the pinned local tool")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    version = ET.parse(root / "src/Unfold.Desktop/Unfold.Desktop.csproj").findtext("PropertyGroup/Version")
    if not version or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", version):
        raise SystemExit("Invalid project version")
    channel = args.runtime + ("-beta" if "-" in version else "-stable")
    mac = args.runtime.startswith("osx-")
    mac_identity = os.environ.get("UNFOLD_CODESIGN_IDENTITY")
    if mac_identity == "-":
        mac_identity = None
    payload = args.payload or root / ("artifacts/Unfold.app" if mac else f"artifacts/{args.runtime}/app")
    output = args.output or root / f"artifacts/update-releases/{args.runtime}"
    if payload.is_symlink() or not payload.is_dir() or any(p.is_symlink() for p in payload.rglob("*")):
        raise SystemExit("Expected a real published payload directory without linked entries")
    binary = payload / ("Contents/MacOS/Unfold" if mac else "Unfold.exe")
    if not binary.is_file():
        raise SystemExit("Missing published app executable")
    if output.is_symlink() or any(parent.is_symlink() for parent in output.parents):
        raise SystemExit("Refusing linked package output")
    notes = root / f"docs/releases/v{version}.md"
    if not notes.is_file():
        raise SystemExit("Release notes are required")
    tool = [str(args.vpk)] if args.vpk else ["dotnet", "tool", "run", "vpk", "--"]
    directive = "[osx]" if mac else "[win]"
    with tempfile.TemporaryDirectory(prefix="unfold-update-pack-") as scratch:
        prepared = Path(scratch) / ("Unfold.app" if mac else "app")
        shutil.copytree(payload, prepared)
        documentation = prepared / "Contents/Resources" if mac else prepared
        documentation.mkdir(parents=True, exist_ok=True)
        for source, target in [(root / "docs/cross-platform.md", "README.md"),
                               (root / "THIRD-PARTY-NOTICES.md", "THIRD-PARTY-NOTICES.md"), (notes, "RELEASE-NOTES.md")]:
            shutil.copyfile(source, documentation / target)
        command = tool + [directive, "pack", "--skip-updates", "--yes", "--packId", PACKAGE_ID,
                          "--packTitle", "Unfold", "--packAuthors", "Dokhu Studio", "--packVersion", version,
                          "--packDir", str(prepared), "--mainExe", "Unfold" if mac else "Unfold.exe",
                          "--runtime", args.runtime, "--channel", channel, "--outputDir", str(output),
                          "--releaseNotes", str(notes), "--icon", str(root / "Art/Brand/unfold-lilac-v1/desktop" / ("unfold.icns" if mac else "unfold.ico"))]
        if mac:
            # DMG/ZIP distribution retains the existing bundle identifier and data paths.
            command += ["--noInst", "--signEntitlements", str(root / "Packaging/Unfold.Desktop.entitlements")]
            identity = mac_identity
            signing_identity = identity or "-"
            profile = os.environ.get("UNFOLD_NOTARY_PROFILE")
            if profile and not identity:
                raise SystemExit("Notarization requires the application signing identity")
            signing_environment = os.environ.copy()
            signing_environment["UNFOLD_CODESIGN_IDENTITY"] = signing_identity
            subprocess.run(["bash", str(root / "Scripts/sign-macos-app.sh"), str(prepared)],
                           env=signing_environment, check=True)
            if identity:
                command += ["--signAppIdentity", identity, "--signDisableDeep", "true"]
            else:
                command += ["--delta", "None"]
            if profile:
                command += ["--notaryProfile", profile]
        else:
            command += ["--shortcuts", "StartMenuRoot"]
        subprocess.run(command, cwd=root, check=True)
    feed = output / f"releases.{channel}.json"
    data = json.loads(feed.read_text(encoding="utf-8"))
    portable = next(output.glob("*-Portable.zip"))
    if mac and not mac_identity:
        seal_local_mac_release(root, output, feed, data, portable, version)
    assets = data["Assets"]
    full = [a for a in assets if a["Type"] == "Full" and a["Version"] == version]
    if len(full) != 1 or full[0]["PackageId"] != PACKAGE_ID:
        raise SystemExit("Feed/package identity mismatch")
    for asset in assets:
        name = asset["FileName"]
        if name != Path(name).name:
            raise SystemExit("Unsafe asset filename")
        path = output / name
        if path.stat().st_size != asset["Size"] or hashlib.sha256(path.read_bytes()).hexdigest().lower() != asset["SHA256"].lower():
            raise SystemExit("Package checksum mismatch: " + name)
    with ZipFile(portable) as archive:
        if archive.testzip() is not None:
            raise SystemExit("Portable ZIP CRC failure")
    artifacts = root / "artifacts"
    artifacts.mkdir(exist_ok=True)
    shutil.copyfile(portable, artifacts / f"Unfold-v{version}-{args.runtime}.zip")
    if not mac:
        setup = next(output.glob("*-Setup.exe"))
        shutil.copyfile(setup, artifacts / f"Unfold-v{version}-{args.runtime}-setup.exe")
    else:
        # Package the actual updater-enabled app, not the pre-Velopack input bundle.
        with tempfile.TemporaryDirectory(prefix="unfold-update-bundle-") as scratch:
            subprocess.run(["ditto", "-x", "-k", str(portable), scratch], check=True)
            bundles = list(Path(scratch).glob("*.app"))
            if len(bundles) != 1:
                raise SystemExit("Expected one updater-enabled application bundle")
            app = artifacts / "Unfold.app"
            if app.is_symlink():
                raise SystemExit("Refusing linked application output")
            if app.exists():
                shutil.rmtree(app)
            # Preserve the native helper's relative symlink and macOS signature metadata.
            subprocess.run(["ditto", str(bundles[0]), str(app)], check=True)
            subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
    inventory = {"version": version, "packageId": PACKAGE_ID, "channel": channel, "runtime": args.runtime,
                 "feed": feed.name, "published": False,
                 "assets": {p.name: {"bytes": p.stat().st_size, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()}
                            for p in sorted(output.iterdir()) if p.is_file()}}
    (output / "local-build-inputs.json").write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    print(feed)

if __name__ == "__main__":
    main()
