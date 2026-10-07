#!/bin/bash
set -euo pipefail
if [[ $(uname -s) != Darwin ]]; then
  echo 'This benchmark requires a macOS desktop session and Xcode command-line tools.' >&2
  exit 2
fi
if [[ $# -lt 3 ]]; then
  echo 'Usage: run.sh APP_SOURCE FRESH_OUTPUT complete|media|2d|soak [DURATION_FACTOR] [MEDIA_FIXTURE_DIRECTORY]' >&2
  echo 'complete/soak require UNFOLD_MEMORY_HIKARI and UNFOLD_MEMORY_KAZUSA GLB paths.' >&2
  exit 2
fi
task_python=${UNFOLD_MEMORY_PYTHON:-python3}
task_tool=$(cd "$(dirname "$0")" && pwd)
task_source=$("$task_python" -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$1")
task_output=$("$task_python" -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$2")
task_mode=$3
task_factor=${4:-1}
task_fixture=${5:-$task_output/media-fixture}
task_hikari=${UNFOLD_MEMORY_HIKARI:-}
task_kazusa=${UNFOLD_MEMORY_KAZUSA:-}
case "$task_mode" in
  complete|soak)
    if [[ ! -f "$task_hikari" || ! -f "$task_kazusa" ]]; then
      echo 'Set UNFOLD_MEMORY_HIKARI and UNFOLD_MEMORY_KAZUSA to existing GLB files.' >&2
      exit 2
    fi ;;
  media|2d) ;;
  *) echo 'Choose complete, media, 2d or soak.' >&2; exit 2 ;;
esac
# Refuse any existing output, including old logs or a real user profile.
mkdir -p "$(dirname "$task_output")"
mkdir "$task_output"
if [[ "$task_mode" == media && ! -d "$task_fixture" ]]; then
  "$task_python" "$task_tool/create_media_fixture.py" "$task_fixture" > "$task_output/fixture.json"
fi
xcrun clang -dynamiclib "$task_tool/MemInfo.c" -o "$task_output/libunfoldmemory.dylib"
export AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build "$task_tool/Unfold.MemoryBenchmark.csproj" -c Release -m:1 \
  -p:UseSharedCompilation=false -p:AppSource="$task_source" \
  -p:NativeLibrary="$task_output/libunfoldmemory.dylib" \
  -p:BaseIntermediateOutputPath="$task_output/obj/" -o "$task_output/app" > "$task_output/build.log" 2>&1
task_inputs=("$task_source" "$task_output/app" "$task_output")
if [[ "$task_mode" == complete || "$task_mode" == soak ]]; then
  task_inputs+=(--fixture "$task_hikari" --fixture "$task_kazusa")
elif [[ "$task_mode" == media ]]; then
  task_inputs+=(--fixture "$task_fixture/idle.gif" --fixture "$task_fixture/spritesheet.png" --fixture "$task_fixture/character.json")
fi
"$task_python" "$task_tool/prepare_run.py" "${task_inputs[@]}"
export UNFOLD_DATA_DIR="$task_output/profile"
task_exit=0
dotnet "$task_output/app/Unfold.Tests.dll" "$task_output" "$task_mode" "$task_factor" \
  "$task_fixture" "$task_hikari" "$task_kazusa" > "$task_output/native.log" 2>&1 || task_exit=$?
if [[ -f "$task_output/run.json" && -s "$task_output/samples.csv" ]]; then
  task_summary_exit=0
  "$task_python" "$task_tool/summarize.py" "$task_output" > "$task_output/summary.log" || task_summary_exit=$?
  cat "$task_output/summary.log"
  if [[ "$task_exit" == 0 ]]; then
    if [[ "$task_summary_exit" != 0 ]] || ! "$task_python" -c 'import json,sys; sys.exit(0 if json.load(open(sys.argv[1])).get("budget_pass") is True else 2)' "$task_output/summary.json"; then
      task_exit=2
    fi
  fi
elif [[ "$task_exit" == 0 ]]; then
  echo 'The workload did not produce complete memory measurement files.' >&2
  task_exit=2
fi
exit "$task_exit"
