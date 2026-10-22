from pathlib import Path
from zipfile import ZipFile
import json, hashlib, struct, plistlib, subprocess, xml.etree.ElementTree as ET
out=Path('/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-in-app-updates-2026-10-03')
report=Path(__file__).parent
results={}
for rid in ['win-x64','osx-arm64','osx-x64']:
    directory=out/'update-releases'/rid
    feed=directory/f'releases.{rid}-beta.json'
    data=json.loads(feed.read_text())
    packages=[]
    for asset in data['Assets']:
        file=directory/asset['FileName']; payload=file.read_bytes()
        assert len(payload)==asset['Size']
        assert hashlib.sha256(payload).hexdigest().lower()==asset['SHA256'].lower()
        assert hashlib.sha1(payload).hexdigest().lower()==asset['SHA1'].lower()
        assert asset['PackageId']=='DokhuStudio.Unfold.Updates' and asset['Version']=='1.0.3-beta'
        with ZipFile(file) as archive: assert archive.testzip() is None
        packages.append(dict(filename=file.name,bytes=len(payload),sha256=hashlib.sha256(payload).hexdigest()))
    details={}
    with ZipFile(out/f'Unfold-v1.0.3-beta-{rid}.zip') as archive:
        assert archive.testzip() is None
        if rid=='win-x64':
            assert 'Unfold.exe' in archive.namelist() and 'Update.exe' in archive.namelist()
            exe=archive.read('current/Unfold.exe'); offset=struct.unpack_from('<I',exe,0x3c)[0]
            assert exe[offset:offset+4]==b'PE\0\0'
            assert struct.unpack_from('<H',exe,offset+4)[0]==0x8664
            metadata=ET.fromstring(archive.read('current/sq.version'))
            assert metadata.findtext('.//{*}version')=='1.0.3-beta'
            assert metadata.findtext('.//{*}channel')==rid+'-beta'
            details.update(mainExeMachine='AMD64',stableLauncher=True,nativeUpdater=True,portableFileCount=len(archive.namelist()))
    if rid.startswith('osx'):
        app=out/('Unfold.app' if rid=='osx-arm64' else 'Unfold-x64.app')
        subprocess.run(['codesign','--verify','--deep','--strict',str(app)],check=True)
        info=plistlib.loads((app/'Contents/Info.plist').read_bytes())
        assert info['CFBundleIdentifier']=='app.unfold.desktop'
        assert info['UnfoldReleaseVersion']=='1.0.3-beta' and info['CFBundleVersion']=='1000003'
        metadata=ET.parse(app/'Contents/Resources/sq.version')
        assert metadata.findtext('.//{*}version')=='1.0.3-beta'
        assert metadata.findtext('.//{*}channel')==rid+'-beta'
        # codesign's plain '-' output is a human-readable DER dictionary on this OS.
        xml=subprocess.run(['codesign','-d','--entitlements',':-',str(app)],capture_output=True,check=True).stdout
        keys=plistlib.loads(xml)
        assert keys=={'com.apple.security.cs.allow-jit':True}
        assert all(not str(p.readlink()).startswith('/') and p.resolve().is_relative_to(app.resolve()) for p in app.rglob('*') if p.is_symlink())
        details.update(bundleIdentifier=info['CFBundleIdentifier'],bundleVersion=info['CFBundleVersion'],appEntitlements=keys,signatureType='ad-hoc',relativeBundleLinks=True)
    results[rid]=dict(success=True,feed=feed.name,packages=packages,**details)
result=dict(success=True,version='1.0.3-beta',platforms=results,published=False)
(report/'packages-final.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps(result,indent=2))
