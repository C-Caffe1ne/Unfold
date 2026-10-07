# 구현 보고서 — `--review-pet-pack` 계약 불일치 수정

작성: 2026-09-16 · 담당: implementation-engineer · 기준 커밋: `7e81f77` (`release/mvp`)
근거 감사: [릴리스 QA 감사](03_release_qa.md) "6. 펫 팩 재생 진단 — 실패"

## 원인

`PetPackDiagnostics`가 펫 창의 **시각 트리 모양을 추측**해서 재생 상태를 읽었다.

- `src/Unfold.Desktop/PetPackDiagnostics.cs:67,74` — `runtime.ActivePet.Content`를 `AnimationView`로 캐스팅.
- `src/Unfold.Desktop/PetWindow.cs:42` — `Content`는 `7e81f77`에서 말풍선과 꼬리를 함께 담는
  `Canvas`로 바뀌었다(`canvas.Children.Add(animation); … Content = canvas;`).

캐스팅은 예외 대신 **조용히 `null`**이 됐고, `startedInExpectedMode`가 항상 `false`가 되어
루프의 첫 항목인 `idle`에서 `InvalidOperationException("The desktop pet did not enter the requested reaction.")`이
발생했다. 팩 내용과 무관하게 종료 코드 1이었다.

같은 커밋에서 `SmokeDiagnostics.cs`는 갱신됐지만 `PetPackDiagnostics.cs`는 손대지 않았다.
즉 결함은 특정 팩이 아니라 **생산자(`PetWindow`)와 소비자(`PetPackDiagnostics`)의 계약 부재**다.
레이아웃이 바뀔 때마다 소비자가 조용히 깨지는 구조였으므로, 증상 자리(캐스팅)만 고치지 않고
펫의 애니메이션 표면을 명시적 계약으로 노출했다.

## 변경

### 1. `src/Unfold.Desktop/PetWindow.cs` — 명시적 내부 계약

```csharp
private readonly AnimationView animation = new() { Width = 192, Height = 192 };
// Content is the layout canvas that also carries the bubble and tail, and its shape
// changes with the bubble layout. Diagnostics and tests read playback state and render
// the pet through this contract instead of casting Content or walking the visual tree.
internal AnimationView PetView => animation;
```

어셈블리 내부 소비자가 `Content`나 `GetVisualDescendants()`로 애니메이션을 다시 유추하지 않는다.
말풍선 방향·접기에 따라 `Canvas`의 자식 구성과 좌표가 바뀌어도 계약은 그대로다.

### 2. `src/Unfold.Desktop/PetPackDiagnostics.cs` — 계약 사용 + 실제 펫 캡처

- 루프 진입 전에 펫을 한 번만 확보한다.
  `var pet = runtime.ActivePet ?? throw new InvalidOperationException("The desktop pet is not open.");`
  펫이 없으면 "반응에 실패했다"가 아니라 **없다는 사실 그대로** 보고된다.
- 반복 여부 확인 두 곳이 계약을 읽는다.
  - `var startedInExpectedMode = key == "idle" ? pet.PetView.Repeats : !pet.PetView.Repeats;`
  - `if (!pet.PetView.Repeats || …) throw …("Playback did not retain or restore the idle loop.");`
  - 보고서 필드 `petReturnedToIdleLoop`도 같은 값을 쓴다.
- **렌더 캡처가 실제 펫의 `AnimationView`를 대상으로 한다.** 클립마다
  `Capture(pet.PetView, "<key>-pet.png")`를 추가했다. 기존 `<key>.png`는 검토 창(dark/light 사본)이고,
  새 `<key>-pet.png`는 화면에 도는 **펫 본체**다. 감사에서 지적된 "클립별 재생 캡처 0장"이 해소된다.
- 컨트롤용 `Capture(Control, string)` 오버로드를 추가했다. 레이아웃이 안 된 표면(0×0)은
  빈 PNG를 남기지 않고 `InvalidOperationException`으로 실패한다.
- 숨긴 펫 확인(`await pet.React(key)`, `if (pet.IsVisible)`)도 같은 참조를 쓴다.
- `pet-review.json`에 `capturedLivePetView: true`를 추가해 어떤 표면을 캡처했는지 산출물에 남긴다.

### 3. `Tests/Unfold.Tests/AnimationLifecycleTests.cs` — 회귀 검사 1개 추가

`PetAnimationSurfaceIsPublishedAsAContractAndTracksTheLiveClipMode` (`[AvaloniaFact]`).
격리된 `UNFOLD_DATA_DIR`에서 헤드리스 `PetWindow`를 띄우고 다음을 확인한다.

| 확인 | 의미 |
|---|---|
| `Assert.Same(live, PetView(pet))` | 계약이 **실제로 렌더되는** `AnimationView`를 가리킨다 |
| `Assert.IsNotType<AnimationView>(pet.Content)` | 옛 `Content is AnimationView` 캐스팅이 왜 항상 실패했는지 고정 |
| `Bounds.Size == 192×192`, `OpaqueAt(96,96)` | 클립별 렌더 캡처가 빈 이미지가 아니다 |
| `Repeats` true → false → true | 진단이 캡처 사이에 판정하는 두 상태(1회 반응 / 복귀한 idle 루프) |

`PetWindow.PetView`와 `AnimationView.Repeats`는 `internal`이고 테스트 어셈블리에는
`InternalsVisibleTo`가 없다. 이 파일이 이미 `timer` 필드를 리플렉션으로 읽고 있어 같은 방식을 따랐다
(`PetView(…)`, `Repeats(…)` 헬퍼). `InternalsVisibleTo` 추가는 소유 범위 밖인 `.csproj` 변경이라 하지 않았다.
계약이 사라지면 리플렉션 조회가 `null`이 되어 검사가 실패한다.

## 소유 파일

수정: 위 3개 파일 + 이 보고서. `AppRuntime.cs`, 버전, README, 기존 문서, 다른 워커의 산출물은 건드리지 않았다.
`git status`의 `CLAUDE.md` 수정과 미추적 `.claude/`는 이 작업 이전부터 있던 상태이며 그대로 두었다.

## 검증

모든 실행은 macOS (Darwin 25.6.0) arm64, .NET SDK 10.0.401에서 자동 검사로 수행했다.
진단 산출물은 저장소가 아니라 세션 스크래치패드의 **새 빈 `UNFOLD_DATA_DIR`**에 남겼다.

### 1. 빌드 — 통과

```sh
dotnet build Unfold.slnx -c Release
```
종료 코드 0. **0 Warning(s), 0 Error(s)** (`TreatWarningsAsErrors=true`).

### 2. 집중 검사 — 통과

```sh
dotnet test Unfold.slnx -c Release --no-build --filter "FullyQualifiedName~AnimationLifecycleTests"
```
**Failed: 0, Passed: 5, Skipped: 0** (기존 4 + 신규 1), 718 ms.

### 3. Release 전체 테스트 — 통과

```sh
dotnet test Unfold.slnx -c Release --no-build
```
**Failed: 0, Passed: 160, Skipped: 0, Total: 160**, 13 s.
감사 기준선 159개 대비 **+1**이며 늘어난 1개가 이번 회귀 검사다.

### 4. 격리 펫 팩 재생 진단 — 통과 (감사에서 실패했던 항목)

```sh
UNFOLD_DATA_DIR=<새 빈 디렉터리> \
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --review-pet-pack <repo>/artifacts/pet-packs/Bori-0.1.0.unfoldpet
```

**종료 코드 0.** `verification/pet-review.json`: `"success": true`, `id: bori-rabbit`,
`capturedLivePetView: true`, `imageFiles: 14`.

| 클립 | plannedMs | observedMs | repeat | petStartedInExpectedMode | petReturnedToIdleLoop |
|---|---|---|---|---|---|
| idle | 4000 | 4132 | true | true | true |
| attention | 1125 | 1250 | false | true | true |
| celebrate | 1125 | 1254 | false | true | true |
| click | 1000 | 1135 | false | true | true |
| stretch | 2500 | 2631 | false | true | true |

PNG 14장(감사 시점 4장 → 14장): 팩 미리보기 4장 + 클립별 검토 창 5장 + **클립별 실제 펫 5장**
(`idle-pet.png`, `attention-pet.png`, `celebrate-pet.png`, `click-pet.png`, `stretch-pet.png`).
`*-pet.png`는 모두 192×192이며, 열어서 확인한 결과 `stretch-pet.png`는 양팔을 든 스트레칭 포즈,
`click-pet.png`는 앉은 기본 포즈로 **클립마다 다른 실제 프레임**이 찍혔다. 빈 이미지가 아니다.

원인을 바꾸지 않은 채 같은 명령을 반복하지 않았다. 각 검사는 변경 후 1회 실행이다.

## 미검증·위험

- **실제 OS 관찰 아님.** 위 결과는 모두 오프스크린(-32000,-32000) 진단 창과 헤드리스 테스트에서 나온
  자동 검사 결과다. 사람이 데스크톱에서 펫을 보고 확인한 것이 아니다.
- **Windows 미검증.** macOS arm64에서만 실행했다. `--review-pet-pack`의 Windows 동작은 이번 범위 밖이다.
- **게시 산출물 미재실행.** 소스 빌드(`bin/Release/net10.0/Unfold.dll`)로만 진단했다.
  자체 포함 게시본에서의 재확인은 release-qa 몫이다.
- **다른 팩 미확인.** `Bori-0.1.0.unfoldpet` 1개만 돌렸다. 원인이 팩 독립적인 계약 불일치였으므로
  다른 팩도 같은 경로를 타지만, 실행 근거는 이 팩 하나다.
- **캡처 대상이 확장 레이아웃에서 바뀔 수 있다.** 진단은 말풍선이 접힌 192×192 상태에서만 캡처한다
  (`runtime.Reset()`으로 알림이 없는 상태). 말풍선이 펼쳐진 레이아웃에서 `Capture(Control, …)`의
  좌표 기준은 이번에 확인하지 않았다.
- **테스트의 리플렉션 의존.** 계약이 `internal`인 동안 회귀 검사는 이름(`PetView`, `Repeats`)에 묶인다.
  이름을 바꾸면 컴파일 오류가 아니라 실행 시 실패로 드러난다. `InternalsVisibleTo`를 추가하면
  없앨 수 있으나 `.csproj`가 소유 범위 밖이라 하지 않았다.
