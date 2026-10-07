import argparse, hashlib, json, os, pathlib, platform, shutil

parser = argparse.ArgumentParser(description='Copy exact product assemblies/assets and preserve its runtime policy.')
parser.add_argument('source', type=pathlib.Path)
parser.add_argument('app', type=pathlib.Path)
parser.add_argument('output', type=pathlib.Path)
parser.add_argument('--fixture', action='append', default=[])
args = parser.parse_args()
source, app = args.source.resolve(), args.app.resolve()
if source == app:
    raise SystemExit('Use a separate benchmark output; never modify the selected product.')
def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(65536), b''):
            digest.update(chunk)
    return digest.hexdigest()

config = source / 'Unfold.runtimeconfig.json'
product_config_sha = sha(config)
runtime = json.loads(config.read_text())
policy = runtime.get('runtimeOptions', {}).get('configProperties', {})
if any('heaphardlimit' in key.lower() for key in policy):
    raise SystemExit('The selected runtime configuration contains a heap cap; this benchmark requires an uncapped product.')

def copy_verified(item, category, destination=None):
    before = sha(item)
    destination = destination or app / item.relative_to(source)
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(item, destination)
    if sha(destination) != before or sha(item) != before:
        raise SystemExit('The selected product file changed during copying: ' + str(item.relative_to(source)))
    return {'name': item.name, 'relativePath': str(item.relative_to(source)), 'category': category,
            'source': str(item), 'copied': str(destination), 'bytes': destination.stat().st_size, 'sha256': before}

assemblies = []
for item in sorted(source.glob('*.dll')):
    if item.name == 'Unfold.Tests.dll':
        raise SystemExit('AppSource must be the desktop output, not a test-runner output.')
    assemblies.append(copy_verified(item, 'assembly'))
if not {'Unfold.dll', 'Unfold.Core.dll'}.issubset({item['name'] for item in assemblies}):
    raise SystemExit('Required product DLLs are missing.')
copied_files = []
for item in sorted(source.iterdir()):
    if item.is_file() and (item.suffix.lower() == '.dylib' or '.so' in item.suffixes):
        copied_files.append(copy_verified(item, 'native'))
for name in ['Assets', 'Tools', 'Licenses', 'runtimes']:
    if (source / name).is_dir():
        for item in sorted((source / name).rglob('*')):
            if item.is_file():
                copied_files.append(copy_verified(item, name))
# Direct DLL references do not add NuGet native-RID entries to the harness
# deps.json. Make only the selected macOS native libraries discoverable beside
# the executable; continue to preserve/verify their original runtime paths.
mac_arch = {'arm64': 'arm64', 'aarch64': 'arm64', 'x86_64': 'x64'}.get(platform.machine().lower())
if mac_arch is None:
    raise SystemExit('Unsupported macOS benchmark architecture: ' + platform.machine())
native_names = set()
for native_directory in [source / 'runtimes' / ('osx-' + mac_arch) / 'native', source / 'runtimes' / 'osx' / 'native']:
    if not native_directory.is_dir():
        continue
    for item in sorted(native_directory.glob('*.dylib')):
        if item.name in native_names or (source / item.name).is_file():
            continue
        native_names.add(item.name)
        copied_files.append(copy_verified(item, 'nativeLookup', app / item.name))
# Preserve benchmark framework/dependency setup and merge the exact product policy.
benchmark_config = app / 'Unfold.Tests.runtimeconfig.json'
benchmark_runtime = json.loads(benchmark_config.read_text())
benchmark_options = benchmark_runtime.setdefault('runtimeOptions', {})
benchmark_options.setdefault('configProperties', {}).update(policy)
benchmark_config.write_text(json.dumps(benchmark_runtime, indent=2))
if any(benchmark_options['configProperties'].get(key) != value for key, value in policy.items()):
    raise SystemExit('Product runtime policy was not copied exactly.')
if sha(config) != product_config_sha:
    raise SystemExit('The selected runtime configuration changed during preparation.')
native_source = args.output / 'libunfoldmemory.dylib'
native_sha = sha(native_source)
if sha(app / 'libunfoldmemory.dylib') != native_sha:
    raise SystemExit('The native memory probe copy does not match the compiled library.')
fixtures = [{'path': str(pathlib.Path(item).resolve()), 'bytes': pathlib.Path(item).stat().st_size,
             'sha256': sha(pathlib.Path(item))} for item in args.fixture]
manifest = {'appSource': str(source), 'assemblies': assemblies, 'copiedFiles': copied_files, 'fixtures': fixtures,
            'productRuntimeConfigSha256': product_config_sha, 'benchmarkRuntimeConfigSha256': sha(benchmark_config),
            'runtimeOptions': runtime.get('runtimeOptions'), 'benchmarkRuntimeOptions': benchmark_options,
            'expectedConserveMemory': policy.get('System.GC.ConserveMemory'),
            'gcPolicy': {key: value for key, value in policy.items() if key.startswith('System.GC.')},
            'gcEnvironment': {key: value for key, value in os.environ.items() if key.lower().startswith(('dotnet_gc', 'complus_gc'))},
            'requestedScreenScale': os.environ.get('UNFOLD_MEMORY_SCREEN_SCALE'),
            'expectedGpuCacheBudgetBytes': os.environ.get('UNFOLD_MEMORY_EXPECTED_GPU_CACHE_BYTES'),
            'expectedSoftwareRendering': os.environ.get('UNFOLD_MEMORY_EXPECT_SOFTWARE'),
            'nativeLibrarySource': str(native_source.resolve()), 'nativeLibraryCopied': str(app / 'libunfoldmemory.dylib'),
            'nativeLibrarySha256': native_sha}
(args.output / 'source-manifest.json').write_text(json.dumps(manifest, indent=2))
