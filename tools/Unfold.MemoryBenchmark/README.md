# macOS 앱 메모리 측정

선택한 **이미 빌드된 Unfold Desktop 출력**을 사용하는 native Avalonia 측정 도구다.
제품을 다시 빌드하거나 실제 사용자 라이브러리·계정·설치 앱을 변경하지 않는다.
현재 macOS에서만 지원한다. .NET 10 SDK, Python 3, Xcode command-line tools,
로그인된 그래픽 세션이 필요하다. 측정 중 다른 빌드·테스트·측정 프로세스를 실행하지 않는다.

`AppSource`는 반드시 명시한다. `run.sh`가 `-p:AppSource`로 해당 폴더의 DLL만
참조해 도구를 빌드하고, 모든 제품 DLL을 측정 출력에 복사한 뒤 원본과 복사본의
SHA-256 일치를 `source-manifest.json`에 기록한다. Assets·Tools·Licenses·runtimes와
제품 native 라이브러리도 파일마다 복사 전후 SHA를 검증하고 기록한다. 제품의
`Unfold.runtimeconfig.json`의 `configProperties`를 측정기 runtimeconfig에 병합해
제품 GC 정책을 유지하고 두 파일의 SHA를 남긴다. 별도 NuGet 버전이나 GC 설정을
주입하지 않는다. 선택한 제품의 `System.GC.ConserveMemory=7`도 그대로 적용한다.
이전 제품에 별도 GC 정책이 없으면 기본 정책을 쓴다. 실제 `GC.GetConfigurationVariables()`와
ConserveMemory 값은 `run.json`에 남기며, 제품 설정과 실값이 다르면 실행을 중단한다.
환경의 GC 설정은 manifest에 기록하며, 전체/per-heap/Percent heap cap 환경 변수는
대소문자와 관계없이 거부한다. 실제 GC 설정의 nonzero HeapHardLimit도 거부한다.

## 실행

출력 경로는 매번 새 경로여야 한다. 도구가 그 아래에 새 `profile`을 만들고
`UNFOLD_DATA_DIR`로 사용한다. 기존 경로는 로그가 하나만 있어도 덮어쓰지 않는다.

```sh
task_product=/absolute/path/to/Unfold.Desktop/bin/Release/net10.0
task_runs=/absolute/path/to/memory-results
export UNFOLD_MEMORY_HIKARI=/absolute/path/to/Hikari.glb
export UNFOLD_MEMORY_KAZUSA=/absolute/path/to/Kazusa.glb
export UNFOLD_MEMORY_SCREEN_SCALE=2
bash tools/Unfold.MemoryBenchmark/run.sh "$task_product" "$task_runs/complete-1" complete
bash tools/Unfold.MemoryBenchmark/run.sh "$task_product" "$task_runs/media-1" media
bash tools/Unfold.MemoryBenchmark/run.sh "$task_product" "$task_runs/soak-1" soak
```

Hikari·Kazusa GLB는 `complete`와 `soak`에서 필수 외부 입력이다. 저장소에 모델을
재배포하지 않는다. 입력 파일의 SHA/크기도 manifest에 남긴다. 전후 비교에는 같은
파일을 사용한다. 제품 출력은 Desktop DLL·Assets·runtimes·Tools와
`Unfold.runtimeconfig.json`이 있는 완전한 출력이어야 한다.

`UNFOLD_MEMORY_SCREEN_SCALE=2`는 실제 Retina 화면을 선택해 측정 창을 배치하고
각 주요 단계·soak 전환의 settings/pet 실제 배율을 검사한다. 해당 배율의 화면이 없으면 실패한다.
1배 비교에는 `1`을 지정한다. 생략하면 OS의 기본 위치를 사용하며 실행별 배율이 달라질 수 있다.
1배/2배 결과를 같은 렌더링 조건의 전후 비교로 섞지 않는다. 150% GLB는 각 조건에서
288px/576px로 기록된다. 제품의 `Program.ConfigureRendering()`을 직접 호출하고 설치된
`SkiaOptions.MaxGpuResourceSizeBytes`도 기록해 제품 그래픽 캐시 정책과 일치시킨다.

| 모드 | 동작 | 예상 시간 |
|---|---|---|
| `complete` | 기본 5펫, 설정 열기/숨기기 20회, 휴식, Hikari·Kazusa 가져오기/저장/홈/150%/재편집, 숨김과 기본 펫 복귀 | 약7분 |
| `media` | 512px 120프레임 GIF를 펫/홈/편집 미리보기에서 재생, 숨김과 기본 펫 복귀 | 약2분20초 |
| `soak` | GLB 2개 설치 후 기본/GLB 펫과 배율·설정·휴식을 반복 | 60회×30초, 전환 시간 포함30분 이상 |
| `2d` | complete의 기본 펫/설정/휴식 구간 | 약3분 |

네 번째 인수는 대기 시간 배수다. 기본값은1이며, 빠른 진단용 배수로 실행한 결과를
정식 시간 측정이라고 보고하지 않는다. `soak`은30분 측정을 유지하도록1만 허용한다.

## GIF 입력

`media`는 처음 실행할 때 출력 아래 `media-fixture`를 생성한다. generator는
원래 측정과 동일하게 512×512, 120프레임, 프레임당80ms, 투명 GIF를 만든다.
디코딩 픽셀은125,829,120바이트(120MiB)로128MiB 입력 제한 안에 있다.
재현용 원본·raw 측정 결과·생성 binary는 커밋하지 않는다.

Pillow가 없으면 별도 환경을 준비한다.

```sh
python3 -m venv tools/Unfold.MemoryBenchmark/.venv
tools/Unfold.MemoryBenchmark/.venv/bin/python -m pip install Pillow
export UNFOLD_MEMORY_PYTHON="$PWD/tools/Unfold.MemoryBenchmark/.venv/bin/python"
```

전후에 같은 GIF 파일을 사용하려면 generator의 출력 경로와 runner의 다섯 번째
인수를 명시한다. generator는 기존 폴더를 덮어쓰지 않는다.

```sh
"$UNFOLD_MEMORY_PYTHON" tools/Unfold.MemoryBenchmark/create_media_fixture.py "$task_runs/shared-fixture"
bash tools/Unfold.MemoryBenchmark/run.sh "$task_product" "$task_runs/media-2" media 1 "$task_runs/shared-fixture"
```

## 결과와 판정

200ms마다 CSV를 기록하고 별도 이미지 캡처를 하지 않는다. `run.json`에는 OS,
프로세스 아키텍처, 제품 버전, DPI/목표 픽셀 크기, 실제 시간과 단계, GC 모드가 남는다.
`summary.json`은 단계·분별 추세와 p50/p95/최댓값, lifetime peak를 정리한다.

| 지표 | 의미 |
|---|---|
| `rss_bytes` | 현재 resident 메모리 |
| `physical_footprint_bytes` | macOS가 계산한 프로세스 physical footprint. compressed/internal/native 비용 때문에 RSS와 다를 수 있음 |
| `rss_peak_bytes`, `physical_peak_bytes` | 커널이 보관한 프로세스 lifetime peak. 200ms 표본 사이 피크도 포함 |
| `managed_bytes` | 강제 GC 없이 읽은 managed 사용량 추정 |
| `heap_last_gc_bytes`, `committed_last_gc_bytes` | 마지막 GC 당시 heap/commit 크기 |
| `allocated_bytes`, `gen0/1/2` | 누적 managed 할당과 정상 GC 횟수 |
| `cpu_total_ms` | 누적 CPU 시간. 요약의 평균은 코어1개=100% 기준 |

예산은 **400,000,000바이트**다. workload 성공, Mach 오류0,
sampled RSS/physical 및 두 lifetime peak가 모두 예산 **미만**이면 `budget_pass=true`다.
예를 들어 RSS380MB라도 physical410MB이면 실패한다. 측정이 정상 종료해도 예산이나
측정 유효성 검사에 실패하면 runner는 종료 코드2를 반환한다. 상세 판정은 `summary.json`에 있다.
강제 GC, heap/process 제한, 메모리 초과 시 프로세스 종료는 사용하지 않는다.

로그인·구매는 모의 응답으로 정상 AppRuntime 진입을 실행한다. 실제 인증·결제·계정
저장 상태는 검증하지 않는다. UI 버튼은 코드로 실행하며 실제 포인터·키보드 입력,
모든 native FPS·화질·GPU 비용·Windows 동작은 별도 검증이 필요하다. 한 실행의
상관된 시계열/p95와 분별 기울기로 모든 입력의 절대 상한이나 누수를 단정하지 않는다.
독립 프로세스에서 반복하고 동일 입력·OS·배율·제품 DLL·GC 정책을 비교한다.
