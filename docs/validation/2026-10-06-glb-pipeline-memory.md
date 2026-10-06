# GLB 로딩·렌더링 파이프라인 메모리 개선

2026-10-06, 로컬 구현·검증 완료. 최신 작업 경로는 `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`이며 브랜치 `codex/glb-import-compat`, HEAD `671729a`, 프로젝트 버전 `1.1.0-beta`를 기준으로 했다. 작업 시작 당시 미커밋 상태에 있던 GLB 호환성·Windows 입력 수정도 포함한 빌드를 전후 비교의 기준으로 보존했다. 커밋·공개 배포·설치본 교체는 수행하지 않았다.

## 원인

- 펫 목록을 읽으면서 사용하지 않는 GLB까지 디코딩했다. 런타임·두 편집 목록·초안이 같은 파일에 대해 별도 모델을 만들고 목록의 `Lazy<GlbModel>`이 계속 보유했다.
- 애니메이션 accessor를 `float[][]`로 풀어 키마다 작은 배열을 생성했다. 내장 BIN/JSON도 원본 바이트에서 다시 복사했다.
- 매 프레임 자세·보간 배열, 출력 이미지, Avalonia 비트맵을 새로 만들었다. 기존 큰 래스터 버퍼 재사용만으로는 이 할당을 줄이지 못했다.

## 변경

모든 지원 GLB에 공통으로 적용한다. 모델 이름·경로·캐릭터별 분기와 원본 자산 변경은 없다.

1. 목록은 메타데이터만 읽고 선택·재생·편집 시 모델을 디코딩한다. 설치·팩 감사의 기본 목록 API는 기존 전체 검증을 유지한다. 선택된 손상 모델은 시작 시 기존 기본 펫으로 복구한다.
2. SHA-256 콘텐츠 기준으로 동일 모델을 공유한다. 경로가 달라도 같은 내용이면 공유하고 같은 경로·길이·수정일의 내용 변경도 구분한다. 전역 캐시의 키는 최대 64개이며 모델과 패키지 캐시는 약한 참조를 사용한다. 활성 클립·편집 초안이 더 이상 소유하지 않는 모델은 GC가 회수할 수 있다.
3. 키프레임을 연속 `float[]`로 저장하고 입력 시간 배열과 내장 청크를 재사용한다. 검증과 LINEAR/STEP/CUBICSPLINE·모프 해석은 유지한다.
4. 관절·정점·자세·보간 계산용 저장 공간을 모델 단위로 재사용한다. 공유 모델의 미리보기와 펫이 동시에 요청해도 하나의 모델 잠금 안에서 계산한다.
5. 실제 재생은 뷰가 소유한 두 픽셀 버퍼와 한 `WriteableBitmap`을 재사용한다. 공개 `GetFrame()`의 독립 이미지 계약은 유지한다. 크기·클립 변경 중 도착한 이전 결과는 표시하지 않는다. 정지·분리 시 여분의 뒤쪽 버퍼를 해제한다.

출력 해상도, 화면 배율 대응, 2×2 안티앨리어싱, 최대 20fps 정책, 행동별 방향·몸 회전 고정·투명 클릭 판정은 유지한다. 렌더 크기를 낮춰 수치를 만든 결과가 아니다. UI 문구·색상·레이아웃은 변경하지 않았다.

## 소유 파일

- Core: `CharacterLibrary.cs`, `GlbModel.cs`, `GlbModel.Rendering.cs`, 신규 `GlbModel.Cache.cs`, `GlbPetDraft.cs`
- Desktop: `AnimationView.cs`, `Ui.cs`, `AppRuntime.cs`, `PetBuilderView.cs`, `GlbPetView.cs`
- 회귀: 신규 `Tests/Unfold.Tests/GlbPipelineTests.cs` — 12개 테스트 케이스
- 문서: 이 보고서·증거, `docs/glb-pets.md`, `docs/README.md`

기존 호환성 수정이 들어 있던 두 Core 파일은 그 상태를 보존한 사본과 비교했다. 이번 변경만의 [소스 차이](2026-10-06-glb-pipeline-memory/pipeline-source.diff), 원본/최종 소스·바이너리 해시 및 시작 Git 상태는 [provenance.json](2026-10-06-glb-pipeline-memory/provenance.json)에 있다. 다른 작업의 입력·호버·Windows 코드와 테스트 및 검증 문서는 변경하지 않았다.

## 검증

### GLB 로딩과 화질

로컬 295개 실제 GLB와 6개 합성 GLB를 같은 순서로 검사했다. 실제 파일은 `/Users/hwanghyeonseong/Downloads/BlueArchiveModels-main`에서 읽었으며 저장소·배포물에는 포함하지 않는다. 합성 자료는 정적 모델, 스킨·루트 잠금, 모프, STEP, 밀도 높은 LINEAR/CUBICSPLINE을 포함한다. 실제 자료가 한 컬렉션이므로 모든 제작 도구·모든 GLB의 호환성을 보장하는 전수 검사는 아니다.

- 301개 중 기존에 성공한 **298개 모두 성공**: 실제 292개 + 합성 6개.
- 기존에 거부하던 3개는 시간 키 중복·역순/120초 제한 오류 그대로 거부했다. 새로 실패한 모델은 없다. 파일별 오류는 [비교 결과](2026-10-06-glb-pipeline-memory/comparison.json)에 있다.
- 앞·중간·마지막 클립과 프레임을 선택해 **2,587개 출력 픽셀 SHA-256 전부 일치**. 실제 모델은 192px, 합성 모델은 192/384/576px에서 비교했다. 모든 클립의 모든 프레임을 비교한 결과는 아니다.
- 성공 입력의 콜드 파싱 누적 관리 메모리 할당량: **9,407,577,984 → 4,240,915,888 bytes, 54.92% 감소**. 캐시를 통하지 않는 `Parse()`를 직접 측정했다. 이는 수백 파일을 순차 처리하며 발생한 할당량 합계이며, 앱의 동시 RAM 사용량이나 상주 메모리가 아니다.
- 고밀도 합성 LINEAR: 6,703,224 → 1,235,800 bytes. CUBICSPLINE: 4,569,536 → 894,296 bytes.

원본 수치: [이전](2026-10-06-glb-pipeline-memory/benchmark-before.json), [최종](2026-10-06-glb-pipeline-memory/benchmark-final.json). JSON의 `performance` 항목은 독립 출력 이미지를 만드는 공개 `GetFrame()` 측정이며, 아래의 실제 UI 버퍼 재사용 측정과 다른 경로다.

### 실제 재생 경로의 할당

macOS에서 Avalonia **Headless + Skia**로 UI 재생 경로를 실행했다. 임시 `UNFOLD_DATA_DIR`에서 합성 GLB(8노드·8채널·12,000 시간 키)를 설치하고 펫 192px·편집기·숨김 상태를 차례로 측정했다. 각 상태는 2초 준비 후 8초 동안 `GC.GetTotalAllocatedBytes(true)`로 앱 전체 관리 할당량을 기록했다. 원본과 최종 프로브의 로드된 Core/Desktop DLL 해시를 각각 대조했다.

| 상태 | 이전 할당량 | 최종 할당량 | 확인된 모델 인스턴스 |
|---|---:|---:|---:|
| 펫만 재생, 192px | 2.797 MiB/s | 0.172 MiB/s | 1 → 1 |
| 펫 + 편집기 | 9.048 MiB/s | 0.338 MiB/s | 4 → 1 |
| 편집기 숨김, 펫 재생 | 2.816 MiB/s | 0.171 MiB/s | 4 → 1 |
| 모두 숨김 | 0.002 MiB/s | 0.002 MiB/s | 4 → 1 |

펫만 재생하면 93.84%, 편집기와 함께 재생하면 96.26% 줄었다. 숨긴 편집기의 초안은 사용자 편집 내용을 보존하므로 모델 1개를 계속 소유할 수 있다. 모두 숨겼을 때의 작은 할당 차이는 렌더링 감소율로 해석하지 않는다. GC 횟수·프레임 속도·실제 OS RAM 감소를 주장하는 결과도 아니다.

원본 수치: [이전 UI 측정](2026-10-06-glb-pipeline-memory/playback-before.json), [최종 UI 측정](2026-10-06-glb-pipeline-memory/playback-after.json).

### 회귀와 빌드

- `dotnet test Unfold.slnx -c Release --no-restore`: **718/718 통과**, 실패·건너뜀 0. Release Core/Desktop/Tests 빌드를 포함하며 경고를 오류로 처리한다. [최종 로그](2026-10-06-glb-pipeline-memory/tests-full-final.log)
- 마지막 수명 관리 변경의 집중 검사: GLB 파이프라인 + CharacterPack **27/27 통과**. [로그](2026-10-06-glb-pipeline-memory/tests-lifetime.log)
- 신규 회귀는 고밀도 파싱, 내용 공유·갱신, 캐시/목록의 모델 회수, 목록 지연 로딩, 손상 모델 복구, 보간별 낮은 할당량, 병렬 크기·자세, 출력 버퍼 초기화, 비트맵 픽셀 갱신 및 공개 이미지 불변성을 검사한다.
- 초기 코드에서 파싱 할당·중복 모델·비트맵 재생성 회귀 테스트가 실패함을 확인한 뒤 수정했다. [수정 전 로그](2026-10-06-glb-pipeline-memory/tests-before.log)
- `git diff --check` 통과. 원본 파일 해시와 비교해 소유 범위 밖의 기존 소스·테스트·문서 815개가 모두 그대로임을 확인했다. [보존 검사](2026-10-06-glb-pipeline-memory/preservation.json)

### 재현

최신 체크아웃에서 실행한다. 각 UI 프로브는 자동으로 새 임시 데이터 디렉터리를 만든다. GLB 원본 컬렉션 경로는 선택사항이며 생략하면 합성 6종만 검사한다.

```sh
dotnet test Unfold.slnx -c Release --no-restore
dotnet run -c Release --project docs/validation/2026-10-06-glb-pipeline-memory/probes/benchmark/Benchmark.csproj -- /tmp/glb-benchmark.json /path/to/glb-corpus
dotnet run -c Release --project docs/validation/2026-10-06-glb-pipeline-memory/probes/playback/Playback.csproj -- /tmp/glb-playback.json
```

상대 경로로 보관한 두 프로브도 Release 빌드 경고·오류 0개로 확인했다. [파서 프로브 빌드](2026-10-06-glb-pipeline-memory/portable-benchmark-build.log), [UI 프로브 빌드](2026-10-06-glb-pipeline-memory/portable-playback-build.log).

각 프로브는 수치와 로드한 바이너리 해시를 JSON으로 저장한다. 이전 빌드는 작업 시작 때 따로 보존한 소스/출력을 사용했고, 이전/최종 데이터의 바이너리 해시를 혼용하지 않았다. 소스 프로젝트 경로만 상대 경로로 바꾼 프로브 원본을 함께 보관한다.

## 미검증·위험

- Windows 실기에서 기존 약 400MB가 몇 MB로 줄었는지는 아직 측정하지 않았다. Headless 할당률을 작업 관리자의 Working Set·Private Bytes 감소율로 환산할 수 없다.
- macOS 네이티브 진단은 새 데이터 경로에서 시도했으나 Avalonia.Native RenderTimer 초기화 오류 `-6661`로 종료됐다. [오류 로그](2026-10-06-glb-pipeline-memory/native-smoke-failure.log). 이 환경에서 네이티브 화면·GPU/컴포지터 메모리·물리 입력은 검증하지 못했다.
- 모델의 메시·디코딩 텍스처·애니메이션 데이터 자체는 필요하다. 큰 래스터 색상/깊이 버퍼도 모델이 살아 있는 동안 최대 사용 크기를 보존하며 상한은 합계 32MiB다. 이것은 앱 전체 RAM 상한이 아니다.
- 내용이 다른 모델, 편집 초안, 사용 중인 클립은 각각 필요한 수명을 유지한다. 강제 GC를 제품 코드에 넣지 않았고 파일 원본을 재압축·축소하지 않았다.
- 실제 Windows/macOS에서 동일 GLB와 화면 배율로 펫만 표시/편집기 열기/닫기/다른 펫 전환 후 RAM·재생·투명 입력·호버를 확인하는 실기 검증이 남아 있다.
