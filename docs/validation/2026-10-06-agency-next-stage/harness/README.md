# Unfold native GLB endurance harness

Ownership: only this `/tmp/unfold-agency-soak-20261006` directory. Product source is read-only.
Baseline: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`, `codex/glb-import-compat`, `1a74cbf2720937b3e4712f821f481603064281de`, `1.1.0-beta`.

The harness uses `AssemblyName=Unfold.Tests` for the product's existing friend-assembly access. It references the existing standard Release DLLs and copies bundled assets/native libraries. It does not build or change the product. `Program.ConfigureRendering` retains the product's macOS native/OpenGL configuration. A custom native desktop application hosts the actual `AppRuntime`, `PetWindow`, and `AnimationView`. `runtime.Start(true,true)` bypasses purchase and account services; `runtime.Stop()` holds the break timer. GLB imports use the product's `GlbPetDraft.Save`, not GUI import controls.

## Recorded 2026-10-06 execution

The supervisor built this harness with zero warnings/errors and ran a 60-second native pilot, then a 2160-second (36-minute) native measurement with 120 seconds warmup and 5-second sampling. The examples below retain the initial author's shorter template durations; the actual comparable run used:

```sh
dotnet build /tmp/unfold-agency-soak-20261006/SoakHarness.csproj -c Release -p:SourceBuild=/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold/src/Unfold.Desktop/bin/Release/net10.0
dotnet /tmp/unfold-agency-soak-20261006/bin/Release/net10.0/Unfold.Tests.dll --seconds 2160 --warmup 120 --sample 5 /Users/hwanghyeonseong/Downloads/BlueArchiveModels-main/Hikari.glb /Users/hwanghyeonseong/Downloads/Kazusa.glb
```

The original temporary harness path and product build path are historical execution paths. To reproduce from the saved evidence, copy this harness directory to the temporary path or adjust paths and `SourceBuild` deliberately; use a fresh run/profile. The supervisor's saved `analyze.py` excludes the final post-stop/capture sample from resource summaries/plots and the initial short-interval CPU sample from the CPU plot. Raw NDJSON retains both.

## Original preparation commands

Build the harness (supervisor executes; initial author did not build):

```sh
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet restore /tmp/unfold-agency-soak-20261006/SoakHarness.csproj --ignore-failed-sources
AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build /tmp/unfold-agency-soak-20261006/SoakHarness.csproj -c Release --no-restore
```

Pilot native process (ten seconds of measurement after import/setup; later cycle cleanup can extend wall time):

```sh
dotnet /tmp/unfold-agency-soak-20261006/bin/Release/net10.0/Unfold.Tests.dll --seconds 10 --warmup 2 --sample 1 /Users/hwanghyeonseong/Downloads/BlueArchiveModels-main/Hikari.glb /Users/hwanghyeonseong/Downloads/Kazusa.glb
```

30-minute baseline:

```sh
dotnet /tmp/unfold-agency-soak-20261006/bin/Release/net10.0/Unfold.Tests.dll --seconds 1800 --warmup 120 --sample 5 /Users/hwanghyeonseong/Downloads/BlueArchiveModels-main/Hikari.glb /Users/hwanghyeonseong/Downloads/Kazusa.glb
```

Each run creates a fresh `runs/<UTC>-<id>/profile` and sets `UNFOLD_DATA_DIR` before constructing runtime state. Models and isolated installed packs remain private under `/tmp`; do not commit GLB files, imported packs, or profile copies to the repository. The console prints `SOAK_ROOT=...`. Keep the machine awake and the pointer away from the pet while running.

The measured interval is at least 1800 monotonic seconds, including 120 seconds of warmup and at least 1680 seconds thereafter. Import/setup is outside this interval. A final sample is appended on orderly shutdown so sample elapsed time spans the complete measurement. No forced GC occurs. Metrics are append-only, flushed NDJSON, preserving partial data after interruption.

Output:

- `environment.json`: exact copied product assembly SHA256/location, baseline, OS, architecture, timing parameters, scope.
- `imports.json`: private model filenames/hashes/sizes/triangles/clip mappings and import timings.
- `samples.jsonl`: RSS (`WorkingSet64`), private bytes, managed bytes, last-GC heap and fragmentation, allocated bytes, GC counts, CPU, thread count, heartbeat age, native window coordinates, scaling, target/rendered resolution, frame index, sparse live-image fingerprint.
- `events.jsonl`: repeated GLB model selection, action/pointer lifecycle, hide/show and brief 2D control dwells.
- `start-pet.png`, `end-pet.png`: native-host rendered-control captures. These are not physical-pointer proof or whole-screen screenshots.
- `result.json`: success, measured duration, failure/log checks, lifecycle counts and per-model frame/image liveness.

CPU `100%` equals one full logical core; the machine-normalized percentage divides by `Environment.ProcessorCount`. `GC.GetTotalMemory(false)` is an estimate of managed live/allocated memory without forcing collection. `GetGCMemoryInfo().HeapSizeBytes` is the last completed GC snapshot. RSS growth alone does not prove a memory leak. Fingerprint/index observations prove animation changes in product buffers; they do not provide FPS, displayed-frame deadlines, compositor latency, native input delivery, or Windows evidence. The diagnostic lifetime does not exercise login/payment, Velopack launch or installation/update paths. There is no approved performance SLA; these measurements establish a baseline.

Analyse samples by model and action (rather than pooling unlike workloads), compare post-warmup first and last five-minute medians and rolling trend, and report quantiles as descriptive baseline results. Ordinary per-sample confidence intervals would overstate certainty because observations are time-correlated; use contiguous time-block resampling if an interval is needed. A single host/run does not establish fleet-wide 95% SLA compliance.

For the resource plot, the supervisor used Python 3.9 with the packages recorded in `plot-requirements.txt`, installed only under `/tmp/unfold-next-stage-20261006/plot-deps`. The numerical JSON analysis uses the Python standard library; plotting additionally requires Matplotlib. Set `PYTHONPATH` to that temporary dependency directory and `MPLCONFIGDIR` to a writable temporary directory when reproducing the original analysis.
