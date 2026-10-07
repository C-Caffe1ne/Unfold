# 펫 팩 진단 수정 — 독립 QA 검증

작성: 2026-09-16 · 담당: release-qa · 대상 커밋: `7e81f77` (`release/mvp`) + 작업 트리 변경
검증 대상: [구현 보고서](05_pet_pack_diagnostic_fix.md) (task_f1527a6b7a43) 및
근거가 된 [릴리스 QA 감사](03_release_qa.md) "6. 펫 팩 재생 진단 — 실패"

구현 워커의 주장을 그대로 옮기지 않고, 모든 항목을 이 세션에서 직접 재실행하거나
소스를 대조해 독립적으로 판정했다. 구현 워커가 제시한 수치와 아래 실측이 일치하는지도 표시한다.

## 환경과 범위

| 항목 | 값 |
|---|---|
| OS / 아키텍처 | macOS (Darwin 25.6.0) arm64 |
| .NET SDK | 10.0.401 · 런타임 `10.0.12` |
| 브랜치 / HEAD | `release/mvp` / `7e81f77` + 작업 트리 diff |
| 작업 트리 변경 | `Tests/Unfold.Tests/AnimationLifecycleTests.cs`, `src/Unfold.Desktop/PetPackDiagnostics.cs`, `src/Unfold.Desktop/PetWindow.cs` (구현 워커) + `CLAUDE.md`(이 작업 이전부터 존재, 무관) |
| 데이터 격리 | 이번 세션에서 새로 만든 임시 디렉터리 `/tmp/qa_50603`와 그 아래 새 빈 `UNFOLD_DATA_DIR` 1개 사용. 구현 워커의 산출물이나 사용자 라이브러리는 참조하지 않음 |
| 제품 코드·테스트·기존 문서 수정 | 없음. 이 보고서 1개 파일만 작성 |

## 실행한 명령과 결과

### 1. `git diff --check` — 통과

```sh
git diff --check
```

종료 코드 **0**, 출력 없음. 공백 오류·충돌 마커 없음.

### 2. 회귀 원인이 실제로 제거됐는지 소스 대조 — 확인

구현 보고서가 주장한 원인·수정을 코드에서 직접 재확인했다(재실행이 아니라 diff 대조).

| 주장 | 대조 결과 |
|---|---|
| `PetWindow.Content`가 `7e81f77`에서 `Canvas`가 됐다 | `src/Unfold.Desktop/PetWindow.cs:38` `canvas.Children.Add(animation); canvas.Children.Add(bubble); canvas.Children.Add(tail); Content = canvas;` — 사실 |
| `PetPackDiagnostics`가 `Content is AnimationView`로 캐스팅해 항상 실패했다 | 수정 전 코드(`git show 7e81f77:...PetPackDiagnostics.cs`)의 `:67,74`가 정확히 이 캐스팅이었음을 이전 감사(03)에서 이미 재현·확인함. 이번 세션은 **수정 후** 코드만 재확인 |
| 새 계약 `PetWindow.PetView`가 실제 렌더되는 뷰를 가리킨다 | `PetWindow.cs:23-27` `private readonly AnimationView animation = new(...); internal AnimationView PetView => animation;`이고, 같은 `animation` 필드가 생성자에서 `canvas.Children.Add(animation)`으로 시각 트리에 들어가며 `SetCharacter()`·`React()`가 이 필드에 `SetFrames`를 호출한다(`:82,96`). **동일 인스턴스** — 새 표면을 만든 게 아니라 이미 렌더 중인 표면을 노출한 것 |
| `PetPackDiagnostics`가 이제 `pet.PetView`만 읽는다 | `PetPackDiagnostics.cs`에 `runtime.ActivePet?.Content`·`as AnimationView` 패턴이 더 이상 없음(diff에서 두 곳 모두 `pet.PetView`로 치환). `grep -n "Content is\|Content as" PetPackDiagnostics.cs` 결과 0건 |
| 헤드리스 테스트가 같은 계약을 검증한다 | `AnimationLifecycleTests.cs`의 새 테스트가 리플렉션으로 `PetWindow.PetView`를 읽어 `GetVisualDescendants().OfType<AnimationView>().Single()`과 `Assert.Same`으로 비교하고, `Assert.IsNotType<AnimationView>(pet.Content)`로 **옛 캐스팅이 실패할 수밖에 없었던 이유**를 직접 고정 |

`Capture(Control, string)` 오버로드가 0×0 표면에서 `InvalidOperationException`을 던지는지도 코드로 확인(`PetPackDiagnostics.cs` 새 메서드, `if (size.Width < 1 || size.Height < 1) throw ...`) — 빈 이미지를 조용히 남기지 않는 안전장치가 실재한다.

### 3. Release 빌드 — 통과

```sh
dotnet build Unfold.slnx -c Release
```

종료 코드 **0**. **0 Warning(s), 0 Error(s)**.

### 4. 집중 회귀 검사 — 통과

```sh
dotnet test Unfold.slnx -c Release --no-build --filter "FullyQualifiedName~AnimationLifecycleTests"
```

**Failed: 0, Passed: 5, Skipped: 0, Total: 5**, 767 ms.
구현 보고서 수치(718 ms, 5개)와 통과/실패/건너뜀 수가 일치. 시간 차이는 재실행이므로 정상.

### 5. Release 전체 테스트 — 통과

```sh
dotnet test Unfold.slnx -c Release --no-build
```

**Failed: 0, Passed: 160, Skipped: 0, Total: 160**, 14 s.
구현 보고서의 160개(감사 기준선 159 + 회귀 검사 1)와 일치. 건너뜀 0 — MP4 변환 테스트 2건이 이번에도 조건부 skip이 아니라 ffmpeg 동봉 상태에서 실행됐다(이전 감사 03의 확인 사항과 동일 조건).

### 6. 격리 `--review-pet-pack` 진단 — 통과

```sh
UNFOLD_DATA_DIR=/tmp/qa_50603/petreview \
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --review-pet-pack <repo>/artifacts/pet-packs/Bori-0.1.0.unfoldpet
```

**종료 코드 0.** 이 세션에서 새로 만든 빈 `UNFOLD_DATA_DIR`이며 구현 워커의 진단 결과와는
별도 실행이다. `verification/pet-review.json`을 직접 읽어 아래를 확인했다.

- `"success": true`
- `"capturedLivePetView": true`
- `"imageFiles": 14`
- `playback` 배열 **5개** 항목, 전부 `petStartedInExpectedMode: true`, `petReturnedToIdleLoop: true`:

| key | plannedMs | observedMs | repeat | petStartedInExpectedMode | petReturnedToIdleLoop |
|---|---|---|---|---|---|
| idle | 4000 | 4131.72 | true | true | true |
| attention | 1125 | 1257.03 | false | true | true |
| celebrate | 1125 | 1249.29 | false | true | true |
| click | 1000 | 1122.36 | false | true | true |
| stretch | 2499.999 | 2632.75 | false | true | true |

모든 클립이 10초 미만이라 `verification.md`의 재생 진단 상한을 위반하지 않는다.
구현 보고서의 수치(같은 5개 클립, 같은 `plannedMs`/`repeat`/판정값)와 표가 일치한다.
`observedMs`는 실행마다 수 ms~수십 ms 단위로 다를 수 있으며 실제로 1~3ms 차이가 있었다 —
`verification.md`가 명시한 대로 프레임 정확도·성능 수치로 해석하지 않는다.

### 7. PNG 개수·크기·빈 이미지 여부 — 직접 재검증(구현 보고서 인용 아님)

```sh
ls /tmp/qa_50603/petreview/verification/*.png | wc -l   # 14
sips -g pixelWidth -g pixelHeight -g hasAlpha <각 *-pet.png>
```

- **개수 14장**: `pack-preview.png`, `pack-installed.png`, `pack-preview-large-{light,dark}-2x.png`(팩 설치·미리보기 4장) + `{idle,attention,celebrate,click,stretch}.png`(검토 창 dark/light 합성 캡처 5장) + `{idle,attention,celebrate,click,stretch}-pet.png`(**실제 펫 표면 캡처 5장**).
- **`*-pet.png` 5장 전부 192×192, `hasAlpha: yes`**(`sips`로 실측, JSON을 읽지 않고 직접 확인).
- **빈 이미지 여부**: PNG를 직접 디코딩(zlib 압축 해제 + PNG 필터 복원, PIL 미사용·자체 구현)해 픽셀 단위로 확인했다.

| 파일 | 불투명 픽셀 비율 | 불투명 영역의 서로 다른 RGB 수 |
|---|---|---|
| idle-pet.png | 27.8% (10,262/36,864) | 5,189 |
| attention-pet.png | 28.3% (10,440/36,864) | 5,210 |
| celebrate-pet.png | 28.1% (10,349/36,864) | 4,943 |
| click-pet.png | 28.8% (10,621/36,864) | 5,233 |
| stretch-pet.png | 33.1% (12,212/36,864) | 5,573 |

전부 27~33%의 불투명 픽셀과 수천 개의 서로 다른 색상을 가진다 — 전면 투명이나 단색 채움이
아니라 실제 캐릭터 스프라이트가 그려졌다. SHA-256 해시도 5개 모두 서로 달라
(`df6def3…`, `03d9211…`, `742cbc6…`, `ddb20bd…`, `9fa1b88…`) 클립마다 다른 프레임이 찍혔음을
독립적으로 확인했다. 이 검증은 구현 보고서에 없던 항목이며, JSON의 `capturedLivePetView: true`를
그대로 받아들이지 않고 실제 이미지 바이트를 확인한 결과다.

## 경계면 교차 검증 — 생산자·소비자 판정

| 경계 | 생산자 | 소비자 | 판정 |
|---|---|---|---|
| 펫 애니메이션 표면 | `PetWindow.PetView`(내부 `animation` 필드, 시각 트리에 실제로 붙어 있고 `SetCharacter`/`React`가 갱신) | `PetPackDiagnostics`가 `pet.PetView.Repeats`로 재생 모드 판정, `pet.PetView`를 그대로 캡처 | **일치.** 캐스팅·시각 트리 추측 없이 동일 인스턴스를 주고받는다. 회귀 재발 조건은 `PetWindow`가 `PetView`가 가리키는 필드를 바꾸거나 이 필드를 시각 트리에서 빼는 경우로 좁혀졌다 |
| 계약의 테스트 검증 | `AnimationLifecycleTests`의 새 테스트가 `PetWindow.PetView`를 리플렉션으로 읽음 | 같은 리플렉션 경로가 `PetPackDiagnostics`의 접근 방식과 동일(둘 다 `internal` 멤버 직접 접근) | **일치.** 계약 이름이 바뀌면 진단은 컴파일 오류로, 테스트는 리플렉션 `null`로 각각 드러난다 — 구현 보고서가 스스로 지적한 한계이며 이 세션에서도 동일하게 확인됨 |
| 반응 종료 후 idle 복귀 | `React()`가 `Task.Delay` 후 `character = null; await SetCharacter()`로 idle을 다시 `SetFrames(..., true, ...)` | 진단이 `reaction` 태스크를 `await`한 뒤 `pet.PetView.Repeats`를 재확인 | **일치.** 5개 클립 전부 `petReturnedToIdleLoop: true`로 실측 확인 |
| 숨긴 펫과 반응 | `AppRuntime.ReactToBreak`은 `IsVisible: true`일 때만 반응(범위 밖, 03 감사에서 확인됨) | 진단은 `ShowPet=false` 설정 후 `pet.IsVisible`을 직접 확인 | **일치.** `hiddenPetStayedHidden: true`를 JSON에서 확인, 이번 수정과 무관한 경로이므로 회귀 없음 |
| 게시 산출물 | (범위 밖) | (범위 밖) | **미검증** — 이번 검증은 소스 빌드(`bin/Release/net10.0/Unfold.dll`)만 사용했다. 구현 보고서도 이 항목을 명시적으로 release-qa 몫으로 넘겼다 |

## 통과·실패·미검증

### 통과 — 이 세션에서 독립 재실행

- `git diff --check` (종료 0)
- 소스 대조로 원인 제거 확인(`Content is AnimationView` 패턴 0건, `PetView` 계약이 동일 인스턴스를 가리킴)
- `dotnet build -c Release` (종료 0, 경고 0)
- 집중 테스트 `AnimationLifecycleTests` 5/5 통과
- Release 전체 테스트 160/160 통과, 건너뜀 0
- 새 격리 `UNFOLD_DATA_DIR`에서 `--review-pet-pack Bori-0.1.0.unfoldpet` 종료 코드 0,
  `success: true`, `capturedLivePetView: true`, `playback` 5개 전부 기대값
- PNG 14장, `*-pet.png` 5장 전부 192×192·비어 있지 않음(직접 픽셀 디코딩, 서로 다른 해시)

구현 보고서가 제시한 모든 수치(테스트 개수, `imageFiles`, `plannedMs`/`repeat`/판정 필드)가
이번 독립 실행 결과와 **일치**했다. 부풀리거나 실행하지 않은 것을 통과로 적은 흔적은 없었다.

### 실패 — 없음

이번 검증 범위 안에서 실패한 명령은 없다.

### 미검증 (이번 세션에서 실행하지 않음, 통과로 쓰지 않음)

- **다른 팩**: `Bori-0.1.0.unfoldpet` 1개만 실행했다(작업 지시가 이 팩을 지정). 원인이 팩과 무관한
  시각 트리 계약 문제였으므로 다른 팩도 같은 경로를 타지만 실행 근거는 없다.
- **게시(publish) 산출물 재실행**: 소스 빌드로만 확인했다. `dotnet publish -r osx-arm64` 이후의
  재확인은 하지 않았다.
- **실제 macOS 관찰**: 이번 진단도 off-screen(-32000,-32000) 창과 자동화된 대기·판정이며, 사람이
  화면에서 펫을 보고 확인한 것이 아니다.
- **Windows**: 전혀 실행하지 않았다. macOS 결과를 Windows 판정으로 대체하지 않는다.
- **말풍선 펼침 레이아웃에서의 캡처**: 진단은 접힌 192×192 상태(`runtime.Reset()`)에서만 캡처한다.
  구현 보고서가 스스로 지적한 한계이며 이번 세션도 별도로 확인하지 않았다.
- **회귀 재현(수정 전 상태로 되돌려 실패를 재현하는 것)**: 의도적으로 하지 않았다. 공유 저장소에서
  `git stash` 등으로 제품 코드를 일시적으로 되돌리는 것은 다른 작업자와 충돌할 위험이 있어
  피했다. 대신 03 감사가 이미 확보한 실패 재현 증거(종료 코드 1, 동일 예외 메시지)와 이번 소스
  대조(캐스팅 패턴 완전 제거, 동일 인스턴스 계약 확인)로 원인 제거를 판정했다.

## 종합 판정

구현 워커의 주장은 이 세션의 독립 실행과 **모두 일치**했다. `PetWindow.Content`가 `Canvas`로 바뀐
뒤에도 `PetPackDiagnostics`가 캐스팅으로 애니메이션 표면을 추측하던 것이 실패 원인이었고,
새 `PetWindow.PetView` 계약은 실제로 렌더 중인 동일 `AnimationView` 인스턴스를 가리키므로
증상(캐스팅 실패)이 아니라 계약 부재라는 근본 원인을 제거했다. 회귀 테스트는 옛 캐스팅이
왜 실패했는지(`Content`가 더 이상 `AnimationView`가 아님)와 새 계약이 실제 렌더 뷰와 동일함을
함께 고정해, 다음에 `PetWindow`의 시각 트리가 다시 바뀌어도 컴파일 시점 또는 테스트 시점에
드러나도록 한다. 04 감독 계획의 P0-1 완료 증거(집중 테스트 + 전체 테스트 + 격리
`--review-pet-pack` 성공)를 이 세션에서 충족했다고 판정한다.

미해결 출시 차단 요소(P0-2 버전 문자열 불일치, Windows 실기 미검증)는 이번 작업 범위 밖이며
[04_supervisor_plan.md](04_supervisor_plan.md)의 큐에 이미 반영되어 있다.
