> **보관 문서 · Garden 실험 — experiment/garden-windows / 3098f20**
> 현재 MVP의 작업 지침이 아닙니다. 원문의 명령, 담당 배정, 경로와 검증 결과는 당시 기록입니다.
> 원래 위치: `docs/team/reports/D-01-qa.md` · [현재 문서 안내](../../../../README.md)

# D-01 — 자동 검증 기준선과 Windows 미검증 목록

작업 ID / 담당: **D-01 / D — 독립 QA**
보고 시각: 2026-09-13 13:47 KST

---

## OS / 브랜치 / 기준 커밋 / 시작 시 변경 상태

| 항목 | 실측값 |
|---|---|
| OS | macOS (Darwin 25.6.0), `OS Version 26.6`, RID `osx-arm64` |
| 브랜치 | `experiment/garden-windows` (`git branch --show-current`) |
| HEAD | `3098f2057bca28f7024aa2a9413556111b3dea09` — 지시서 기준 커밋과 **일치** |
| 시작 시 변경 상태 | `?? docs/team/` (추적되지 않은 팀 문서 디렉터리) 하나뿐. 제품 코드·테스트에 로컬 변경 없음 |
| .NET SDK | `10.0.401`, Host `10.0.12`, MSBuild `18.9.11`, `global.json` 없음 |
| 워크로드 | 설치된 워크로드 없음 (이번 빌드에 불필요) |

**환경 주의 (제품 결함 아님):** 로그인 셸의 `PATH`에 `dotnet`이 없어 `dotnet --info`가
`command not found`로 실패했다. SDK는 `~/.dotnet`에 설치되어 있으며,
`DOTNET_ROOT=$HOME/.dotnet`과 `PATH=$HOME/.dotnet:$PATH`를 설정해 진행했다.
**도구를 새로 설치하거나 업그레이드하지 않았다.** CI는 `actions/setup-dotnet`으로
SDK를 주입하므로 이 문제는 로컬 셸 설정에 한정된다.

---

## 확인한 현재 동작

- `Unfold.slnx`는 `Unfold.Core`, `Unfold.Desktop`, `Tests/Unfold.Tests` 3개 프로젝트다.
  C# 테스트 프로젝트는 **하나**이며 소스는 `CoreTests.cs`(217줄), `UiTests.cs`(83줄),
  `EditorLifecycleTests.cs`(57줄) **총 357줄**이다.
  (`Tests/UnfoldTests/`의 40여 개 Swift 테스트는 이 솔루션에 포함되지 않으며 이번 실행 대상이 아니다.)
- CI(`.github/workflows/desktop.yml`)는 windows-latest / macos-latest에서
  `dotnet restore --locked-mode` → `dotnet test -c Release --no-restore`를 돌린 뒤
  Windows는 `publish-desktop.ps1`, macOS는 `make-macos-bundle.sh`를 실행한다.
  **CI에는 앱 실행(smoke) 단계가 없다.** 게시 산출물은 빌드만 되고 실행되지 않는다.
- 기준 커밋의 Garden 변경은 `AppRuntime.cs +30 / -12`, `PetWindow.cs +37 / -22`,
  `CoreTests.cs +32`다. 추가된 테스트는 **2개**이며 둘 다 `CodecTests`의 자산 검사다.

---

## 검증: 정확한 명령, 종료 코드, 통과·실패·건너뜀 수, 로그 위치

로그 루트: `/private/tmp/claude-501/-Users-hwanghyeonseong-Documents-GitHub-Unfold/e4cc56bf-31c0-4aed-9b26-be3c68459c4b/scratchpad/D-01-logs/`

모든 명령은 `DOTNET_ROOT=$HOME/.dotnet`, `PATH=$HOME/.dotnet:$PATH`,
`DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`, `AVALONIA_TELEMETRY_OPTOUT=1`
아래에서 저장소 루트를 작업 디렉터리로 실행했다.

| # | 명령 | 종료 코드 | 결과 | 로그 |
|---|---|---|---|---|
| 1 | `dotnet --info` | 0 | SDK 10.0.401 확인 | 위 표에 인용 |
| 2 | `dotnet restore Unfold.slnx --locked-mode` | **0** | `All projects are up-to-date for restore.` — 락파일 불일치 없음 | `restore.log` |
| 3 | `dotnet test Unfold.slnx -c Release --no-restore` | **0** | **Failed: 0, Passed: 38, Skipped: 0, Total: 38, Duration 1s** | `test.log` |
| 4 | `dotnet test Unfold.slnx -c Release --no-restore --no-build --list-tests` | **0** | 발견된 테스트 케이스 **정확히 38개** (전수 목록 확보) | `list-tests.log` |
| 5 | `dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test` | **0** | `smoke.json` → `"success": true` | `smoke.log` (빈 출력) |

빌드 경고: `Directory.Build.props`가 `TreatWarningsAsErrors=true`이므로 Release 빌드
성공 자체가 **경고 0** 을 의미한다.

### 자동 검증 상태: `자동 검증 통과` (macOS / osx-arm64 한정)

`dotnet test`의 38/38은 **macOS에서만** 실행되었다. Windows에서의 동일 명령 결과는
이 보고서의 근거가 아니며, 어떤 Windows 항목의 증거로도 사용하지 않는다.

---

## smoke 실행 결과 (macOS, GUI 가능)

전용 격리 경로를 새로 만들어 사용했다. **기본 데이터 경로는 건드리지 않았다.**

```
UNFOLD_DATA_DIR=<scratchpad>/D-01-smoke-data     # 실행 전 rm -rf 후 생성, 내용물 0개
```

실제 사용자 데이터(`~/Library/Application Support/Unfold`)는 최종 수정 시각이
`Sep 11`로 그대로이며 이번 실행이 접근하지 않았다.

프로세스 종료 코드 **0**. 전용 경로에 생성된 파일:

```
<DATA>/settings.json
<DATA>/.instance.lock
<DATA>/Characters/.library.lock
<DATA>/Characters/user-a371064e43b54ec3b0d4d6756c412e47/{character.json,source.piskel,spritesheet.png}
<DATA>/verification/{smoke.json,settings.png,pet.png,editor.png,reminder.png}
```

`<DATA>/verification/smoke.json` 전문:

```json
{
  "success": true,
  "startupMs": 939.1731,
  "totalMs": 4720.7981,
  "workingSetBytes": 128745472,
  "managedBytes": 72102912,
  "oneCoreCpuPercent": 6.270370263086661,
  "characters": 3,
  "imageFiles": 4,
  "idleSeconds": 0.1731558,
  "os": "Unix 26.6.2",
  "framework": "10.0.12"
}
```

생성된 `settings.json`의 `"selectedCharacterId": "sprout"` — 새 프로필의 기본값이
sprout라는 커밋 주장은 **자동 검증 통과**로 확인된다.

### smoke가 실제로 증명하는 것과 증명하지 않는 것

`SmokeDiagnostics.Run`이 예외를 던지는 조건은 6가지뿐이다
(`SmokeDiagnostics.cs:23,30,32,33,36`): 캐릭터 0개, `ActivePet` null, 에디터 미개방,
에디터 저장 실패, 저장본 내용 불일치, 리마인더 미개방. 그 외에는 스크린샷을 찍고
성능 수치를 적을 뿐 **동작을 단언하지 않는다.**

- **증명됨:** 무결한 데이터 경로에서 기동 → 캐릭터 3종 로드 → 펫 창 생성 →
  에디터 저장 왕복 → 리마인더 개방 → `sprout`로 캐릭터 전환 → 정상 종료가
  예외 없이 완주한다. 1코어 CPU 6.3%, 워킹셋 123MB.
- **증명되지 않음:** 낮/밤 어느 클립이 실제로 화면에 걸렸는지, 클릭 반응이
  재생되는지, 펫 숨김이 동작하는지. `smoke.json`에는 `IdleKey`/phase를 기록하는
  필드 자체가 없다. 실행 시각이 13시였으므로 런타임이 밟은 경로는
  `idle-day` **한 쪽뿐**이고, `idle-night` 경로는 이번 실행에서 한 번도 실행되지 않았다.
  `SmokeDiagnostics.cs:40`은 `ShowPet = true`만 설정하며 `false`는 시도하지 않는다.

즉 smoke는 **크래시 부재 증명**이지 **동작 정합성 증명**이 아니다.

---

## 핵심 판정 — 38개 테스트는 Garden 핵심 동작을 커버하는가

### 판정: **커버하지 않는다.** 38개 중 Garden 관련은 2개이며, 둘 다 자산 파일 검사다.

`--list-tests` 전수 목록 38개의 분류:

| 영역 | 개수 | 클래스 |
|---|---:|---|
| 픽셀 문서·편집 세션 | 12 | `PixelTests` |
| 코덱·번들 자산 | 8 | `CodecTests` (이 중 Garden 관련 2) |
| 라이브러리·설정 저장 | 9 | `LibraryTests` |
| 타이머 | 3 | `ClockTests` |
| 에디터 UI (Avalonia headless) | 3 | `UiTests` |
| 에디터 저장/닫기 다이얼로그 | 3 | `EditorLifecycleTests` |

**Avalonia headless 테스트 6개는 전부 `EditorWindow` 대상이다. `PetWindow`를
띄우는 테스트는 0개다.**

### 근거 1 — 테스트 소스에 Garden 런타임 심볼이 존재하지 않는다

`Tests/Unfold.Tests/*.cs` 전체 grep 결과:

| 심볼 | 출현 |
|---|---:|
| `AppRuntime` | **0** |
| `IsNight` | **0** |
| `IdleKey` | **0** |
| `React` | **0** |
| `SetCharacter` | **0** |
| `ShowPet` / `HidePet` | **0** / **0** |
| `AnimationView` | **0** |
| `OpaqueAt` | **0** |
| `PetWindow` | 1 — **주석 안에서만** (`CoreTests.cs:143`) |

`CoreTests.cs:143`의 그 한 줄이 판정을 요약한다:

```
// PetWindow looks the key up by name, so the clip existing is the whole feature.
```

테스트는 클립이 **존재한다**는 사실을 확인하고, 런타임이 그 클립을 **집어 든다**는
것은 주석으로 주장한다. 그 주장을 실행하는 코드는 없다.

### 근거 2 — 네 가지 핵심 동작 각각의 실제 커버리지

| Garden 핵심 동작 | 구현 위치 | 테스트가 실제로 하는 일 | 커버 여부 |
|---|---|---|---|
| **낮·밤 idle 전환** | `AppRuntime.cs:21` `IsNight => DateTime.Now.Hour is < 6 or >= 18`, `AppRuntime.cs:24` `IdleKey`, 틱 에지 `AppRuntime.cs:95` `if (phase != IsNight)`, `ApplyPhase()` `:133` | `BundledPlantCarriesDistinctDayAndNightIdles`는 `idle-day`/`idle-night` 두 클립을 **디스크에서 직접 로드**해 프레임 8개·픽셀 차이·너비 동일·미지 키 폴백을 확인한다. 18시/6시 경계, 에지 1회성, `phase` 초기화, `ApplyPhase`의 예외 삼킴은 **전혀 실행되지 않는다** | ❌ **자산만** |
| **클릭 반응** | `PetWindow.cs:48-54` 릴리즈 핸들러, `:51` 클릭/드래그 판정(5px & 0.22s), `PetWindow.cs:82-107` `React()` | `BundledPlantShipsAClickClipTheRuntimePicksUpByItself`는 매니페스트에 `click` 키가 있고, 6프레임, `Loop=false`, 총 250~400ms, 첫·끝 프레임이 idle 포즈, 고양이엔 없음을 확인한다. **포인터 이벤트를 발생시키지 않고, `React()`를 호출하지 않는다.** 5px/0.22s 임계값, `reacting` 소유권 가드, `generation` 경쟁 조건은 미실행 | ❌ **자산만** |
| **비동기 캐릭터 전환** | `PetWindow.cs:66-78` `SetCharacter()` — `++generation` 후 `await runtime.Clip(key)`, 복귀 시 `current != generation` 폐기. `AppRuntime.cs:122-130` in-flight Task 공유 캐시 | **테스트 0개.** 이 코드의 존재 이유인 경쟁 조건(전환 중 재전환, 리마인더 중 전환)을 재현하는 테스트가 없다 | ❌ **없음** |
| **숨김 / 표시** | `PetWindow.cs:108-110` `ShowPet`/`HidePet`/`ClosePet`, `AppRuntime.cs:140` `UpdatePet()`의 `!Settings.ShowPet` 분기, 트레이 토글 `:221` | **테스트 0개.** smoke도 `ShowPet=true`만 설정한다 | ❌ **없음** |

### 근거 3 — 커밋이 스스로 증언한다

기준 커밋은 `AppRuntime.cs`에 +30줄, `PetWindow.cs`에 +37줄의 **런타임 로직**을
추가하면서 `CoreTests.cs`에는 `[Fact]` **2개**만 추가했고, 그 2개는 모두
런타임을 인스턴스화하지 않는 `CodecTests`의 자산 검사다.

### 판정의 한계 — 이것은 이번 결함이 아니다

- 성장·보상·상점은 아직 존재하지 않는 기능이므로 테스트 부재를 결함으로 분류하지 않는다.
- 위 4개 항목의 미커버는 **결함 확정이 아니라 검증 공백**이다. 동작이 깨졌다는
  증거는 없다. 동작한다는 자동 증거도 없다는 뜻이다.
- `IsNight`가 `DateTime.Now`를 직접 읽어(`AppRuntime.cs:21`) 시각 주입이 불가능하다는
  점은 **테스트 용이성 제약**이다. 현재 설계에서는 18시/6시 경계를 자동 검증할
  방법이 없으며, 이 제약을 없애려면 A 소유 파일의 계약 변경이 필요하다.

---

## 발견 사항

심각도 표기: 결함으로 확정된 항목은 없다. 아래는 **검증 공백**과 **관찰 사실**이다.

1. **[공백 · 높음] Garden 런타임 무커버** — 관찰: 테스트 38개 중 `PetWindow`/`AppRuntime`을
   인스턴스화하는 것이 0개 (`Tests/Unfold.Tests/*.cs` 전수 grep).
   영향: Garden 프로토타입의 회귀를 자동으로 잡을 수 없다. 재현: 위 grep 표.
2. **[공백 · 높음] `idle-night` 경로 런타임 미실행** — 관찰: smoke 실행 시각 13시,
   `AppRuntime.cs:24`에 따라 `IdleKey="idle-day"`. 야간 분기는 자동·수동 어느 쪽으로도
   실행 기록이 없다. 영향: 밤 클립 로드 실패가 18시 이후에야 드러난다.
3. **[공백 · 중간] CI에 실행 단계 없음** — 관찰: `desktop.yml`은 test와 publish만 수행하고
   `--smoke-test`를 호출하지 않는다. 영향: Windows 게시 산출물이 실행 가능한지
   CI가 확인하지 않는다.
4. **[관찰] smoke 리포트에 phase 필드 없음** — `SmokeDiagnostics.cs:44-48`의 리포트
   스키마에 `idleKey`/`isNight`가 없어, smoke 로그만으로는 어느 분기를 밟았는지
   사후에 알 수 없다.
5. **[환경] 로컬 셸 `PATH`에 dotnet 부재** — 제품 결함 아님. CI 영향 없음.

### 실패 구분

**환경 문제:** 1건 (`dotnet` PATH 부재 — 우회로 해결, 도구 설치 없음).
**제품 결함:** **0건.** 실행한 모든 명령이 종료 코드 0으로 통과했다.

---

## macOS Cat 기록과 Windows Garden 증거의 분리

세 문서를 섞지 않는다.

| 문서 | 성격 | 대상 커밋 | 상태 |
|---|---|---|---|
| `docs/dogfooding-log.md` | **macOS · Cat(Mochi) MVP** 실사용 기록 | `84c8405` (Garden 이전) | 일부 기입됨. 여기의 `[x]`는 **고양이**에 대한 macOS 관찰이며, Garden 동작이나 Windows 동작의 증거가 **아니다** |
| `docs/windows-dogfooding-log.md` | **Windows · Cat MVP** 검증 양식 | `dc77ab9` (`release/mvp`) | **전 항목 공란.** A~L 어느 섹션도 체크되지 않았고 Timeline·Session Summary도 비어 있다. 세션이 실행된 저장소 증거 없음 |
| 이 보고서 | **macOS · Garden** 자동 검증 | `3098f20` | 아래 세션 블록 |

`windows-dogfooding-log.md:52`는 **"Garden work does not start until this session
records zero BLOCKERs"** 를 게이트로 명시한다. 그 세션이 실행되었다는 증거는 없고,
Garden 프로토타입 커밋은 이미 존재한다. **이 게이트는 충족되지 않은 채 우회되었다.**
이는 QA가 판정할 문제가 아니라 총괄이 판정할 프로세스 사실로 기록한다.

Cat 양식을 Garden 결과로 소급해 채우지 않았다. 두 파일 모두 **수정하지 않았다.**

### 이번 세션 (Garden / macOS / 자동)

- 일시: 2026-09-13 13:46 KST
- OS: macOS 26.6 · Darwin 25.6.0 · arm64
- 커밋: `3098f20` (`experiment/garden-windows`)
- 빌드: `dotnet build`(Release, `dotnet test` 경유), 게시 아님
- 실행 형태: `dotnet run --smoke-test`, `DiagnosticMode`로 창을 `-32000,-32000`에 배치
- 결과: 자동 38/38 통과, smoke `success: true`, 종료 코드 0
- **육안 UI 확인은 하지 않았다** — 진단 모드는 창을 화면 밖에 두므로 사람이 본 것이 없다.
  캡처된 `pet.png` 등 4개 PNG는 격리 경로에 남아 있으나 이번 보고에서 내용을 판정하지 않았다.

### Windows 세션

**없음.** 이 머신은 macOS이며 Windows 실기기가 없다. `./Scripts/publish-desktop.ps1
-Runtime win-x64`와 `./artifacts/win-x64/Unfold.exe --smoke-test`는 **미실행**이다.

---

## A/B/C 시나리오 커버리지

**보고 시점(13:47 KST)에 `docs/team/reports/`는 비어 있다. A-01 / B-01 / C-01
보고서를 받지 못했다.** 아래는 `docs/team/README.md`의 소유권 표에 기재된 각
담당 영역을 QA가 독자적으로 분류한 것이며, A/B/C가 제출할 실제 시나리오로
갱신되어야 한다.

### A — 핵심 동작 (`AppRuntime`, `PetWindow`, `AnimationView`, `StretchClock`, `AppSettings`, `CharacterLibrary`)

| 시나리오 | 분류 |
|---|---|
| 활동 시간 누적, 1회 발화, 일시정지·유휴·수면 갭 제외 | **기존 자동 검증** (`ClockTests` 3개) |
| 설정 원자적 저장·복원 | **기존 자동 검증** (`AtomicSettingsRoundTrip`) |
| 캐릭터 저장/재로드/충돌/삭제/복구/경로 탈출 | **기존 자동 검증** (`LibraryTests` 9개) |
| sprout가 낮·밤 idle 자산을 갖고, 미지 키가 `idle`로 폴백 | **기존 자동 검증** (`BundledPlantCarriesDistinctDayAndNightIdles`) |
| **18:00 / 06:00 경계에서 `IdleKey`가 뒤집힘** | **추가 검증 필요** — `DateTime.Now` 직접 참조로 현재 주입 불가 |
| **phase 에지가 1회만 발화하고 안정 구간엔 재로드 없음** | **추가 검증 필요** |
| **`ApplyPhase` 실패가 다이얼로그 없이 로그로 흡수됨** | **추가 검증 필요** |
| **`SetCharacter`의 `generation` 폐기 — 전환 중 재전환** | **추가 검증 필요** |
| **클릭이 break 반응을 자르지 않음 (`reacting` 가드)** | **추가 검증 필요** — macOS 수동 1회 기록은 커밋 메시지 주장뿐 |
| **`click` 클립 없는 캐릭터(고양이)에서 클릭 무반응** | **추가 검증 필요** (자산 측면만 자동 검증됨) |
| 클릭 vs 드래그 임계값(5px / 0.22s)의 체감 | **실제 OS 필요** |
| 유휴 시간 조회(`PlatformServices.IdleTime`)의 OS별 정확도 | **실제 OS 필요** |

### B — UI · 자산 (`SettingsWindow`, `Ui`, `sprout/`, 생성 스크립트)

| 시나리오 | 분류 |
|---|---|
| 에디터가 기본·최소 창 크기에서 렌더되고 Save 버튼이 안에 들어옴 | **기존 자동 검증** (`EditorRendersAtDefaultAndMinimumWindowSizes`) |
| 에디터 마우스 그리기·지우기·되돌리기, 프레임/레이어 버튼 | **기존 자동 검증** (`UiTests` 2개) |
| sprout 클립 프레임 수·루프 플래그·지속시간·시접(첫=끝 프레임) | **기존 자동 검증** (`BundledPlantShipsAClickClip…`) |
| **`SettingsWindow` 렌더·미리보기·체크박스 동기화** | **추가 검증 필요** — headless 테스트 0개 |
| **"break" 문구 변경이 전 표면에 일관 적용** (트레이/리마인더/설정) | **추가 검증 필요** |
| **`Ui.Bitmap` 수명 계약 (반환 비트맵의 소유권)** | **추가 검증 필요** |
| 리마인더 창 배치·포커스 탈취 | **실제 OS 필요** |
| 밝은/어두운 배경에서의 가장자리 품질, 얇은 줄기 파지 가능성 | **실제 OS 필요** |
| 화분 불투명 영역이 실제 히트 영역으로 충분한가 | **실제 OS 필요** |

### C — Windows · 배포 (`PlatformServices`, `NativeReminder`, `SingleInstance`, `Program`, 스크립트)

| 시나리오 | 분류 |
|---|---|
| `--smoke-test`가 `UNFOLD_DATA_DIR` 미설정 시 임시 경로를 자동 생성 | **기존 자동 검증** — 코드(`Program.cs:10`) + 이번 smoke가 명시 경로로 통과 |
| 단일 인스턴스 파일 락 생성 | **추가 검증 필요** — 이번 smoke가 `.instance.lock`을 만든 것은 확인했으나 **두 번째 인스턴스의 활성화 요청 경로는 미실행** |
| **`win-x64` 게시 산출물 생성** | **실제 OS 필요** — CI에서만 수행, 이번 미실행 |
| **게시 exe `--smoke-test` 실행** | **실제 OS 필요** — CI에도 없음 |
| **클릭 통과 (`UpdateClickThrough`, `user32.dll` P/Invoke)** | **실제 OS 필요** — Windows 전용 코드 경로, 실행 기록 전무 |
| **DPI 100/125/150% 크기·선명도·히트 영역 일치** | **실제 OS 필요** |
| **Windows 알림 (`NativeReminder`) 및 Focus Assist 상호작용** | **실제 OS 필요** |
| **로그인 시 실행 — HKCU `Run` 값 생성/제거/`--background`** | **실제 OS 필요** |
| **트레이 아이콘 표시·툴팁 카운트다운·Quit 시 프로세스 완전 종료** | **실제 OS 필요** |
| **멀티모니터 이동·재시작 위치 복원·모니터 분리** | **실제 OS 필요** |
| SmartScreen / 코드 서명 | **실제 OS 필요** (문서상 SAFE TO DEFER) |

---

## 원인 가설과 추가 확인 방법

- **가설:** Garden 커밋이 자산 교체 + 얇은 런타임 분기로 설계되었기 때문에, 작성자가
  "자산이 있으면 기존 런타임이 알아서 집는다"는 전제를 세우고 자산 계약만 테스트했다.
  `CoreTests.cs:143`, `:137`의 주석이 이 전제를 문장으로 남기고 있다.
- **확인 방법:** 전제가 참인지는 `PetWindow`를 headless로 띄워 `SetCharacter()` 후
  `AnimationView`에 실제로 걸린 프레임이 요청한 키의 프레임과 일치하는지 비교하면
  결정된다. 이는 아래 회귀 시나리오 R1·R2가 정확히 하는 일이다.

---

## 다른 담당자에게 필요한 요청

- **A에게 (`AppRuntime.cs`):** `IsNight`가 `DateTime.Now`를 직접 읽어(`:21`) 낮·밤
  경계를 자동 검증할 수 없다. 시각 공급자를 주입 가능하게 하거나(`Func<DateTime>`
  또는 내부 `TimeProvider`), 최소한 시각→키 매핑을 순수 정적 함수로 분리해달라.
  **계약 변경이므로 D가 아니라 A가 결정한다.** 확정 전까지 D는 낮·밤 경계를
  `추가 검증 필요`로 유지한다.
- **A에게 (`PetWindow.cs`):** `SetCharacter`/`React`가 `internal`·`public`이라
  테스트에서 호출 가능한지 확인 요청. `React`는 `internal`(`:82`)이므로
  `InternalsVisibleTo`가 필요할 수 있다.
- **C에게:** Windows 실기기 세션의 가용 여부. 위 `실제 OS 필요` 11개 항목은
  D가 이 머신에서 영구히 해소할 수 없다.
- **총괄에게:** `windows-dogfooding-log.md:52`의 게이트가 미충족 상태로 남아 있다는
  사실 확인. 이 게이트를 유효한 계약으로 유지할지 폐기할지는 총괄 판정 사항이다.

---

## 변경한 파일

- `docs/team/reports/D-01-qa.md` (이 파일, 신규)

제품 코드와 기존 테스트를 **수정하지 않았다.** 테스트를 삭제하거나 완화하지 않았다.
`git checkout` / `reset` / `commit` / `stash` / 브랜치 변경을 하지 않았다.

빌드·검증 산출물 (격리, 저장소 밖):
- `<scratchpad>/D-01-logs/{restore,test,list-tests,smoke}.log`
- `<scratchpad>/D-01-smoke-data/**` (전용 `UNFOLD_DATA_DIR`)

저장소 안에 생성된 산출물: `artifacts/verification/pixel-editor.png`,
`artifacts/verification/pixel-editor-minimum.png` — `UiTests`가
`UiTests.cs:74-77`에서 스스로 쓰는 파일이며 D가 의도적으로 만든 것이 아니다.
`src/**/bin`, `obj`, `Tests/**/bin` 빌드 출력도 동일하다.

---

## 미검증 항목과 환경 제약

1. **Windows 전부.** 실기기 없음. 클릭 통과, DPI, 알림, 로그인 실행, 트레이,
   멀티모니터, 게시 exe 실행, SmartScreen — 모두 **미검증**.
2. **`idle-night` 런타임 경로.** 실행 시각이 13시였다. 18:00 이후 재실행하거나
   시스템 시계를 바꾸어야 하는데, 후자는 사용자 머신 상태를 바꾸므로 하지 않았다.
3. **육안 UI 판정.** 진단 모드가 창을 화면 밖에 배치하므로 사람이 화면을 본 적이 없다.
   캡처된 PNG 4개의 내용 검증은 하지 않았다.
4. **Swift 테스트 (`Tests/UnfoldTests/`, 40여 파일).** `Unfold.slnx` 밖이며 이번
   지시서 범위 밖이다. 실행하지 않았다. (참고: 사용자 메모에 따르면
   `major-MacOS` 브랜치가 Swift 전용이며, 이 브랜치의 Swift 코드는 보존물이다.)
5. **두 번째 인스턴스 활성화 경로** (`SingleInstance.RequestActivation`) 미실행.
6. **장시간 실행 (60~90분) 메모리·CPU 추이.** smoke는 2초 샘플 1회뿐이다.

---

## 후속 통합 검증에 필요한 최소 회귀 시나리오

A/B/C의 수정이 통합된 뒤 D가 돌릴 최소 집합. 모두 Avalonia headless에서 실행
가능하며, `GardenIntegrationTests.cs`(README 5항의 D 소유 예약 이름)에 넣는다.
**지금은 만들지 않는다** — 이번 라운드는 조사·검증뿐이다.

| ID | 시나리오 | 무엇을 막는가 | 선행 조건 |
|---|---|---|---|
| **R1** | `PetWindow.SetCharacter()` 후 `AnimationView`에 걸린 프레임이 `runtime.Clip(runtime.IdleKey)`의 프레임과 동일하다 | 자산은 있는데 런타임이 엉뚱한 키를 집는 회귀 | 없음 |
| **R2** | 시각을 05:59→06:00→18:00으로 밀 때 `IdleKey`가 `idle-night`→`idle-day`→`idle-night`로 뒤집히고, 각 안정 구간에서 `SetCharacter`가 **재로드하지 않는다** | 경계 오류, 매 틱 재로드로 인한 CPU 상승 | **A의 시각 주입 계약** |
| **R3** | sprout에 `React(null)`을 걸면 `click` 클립이 1회 재생된 뒤 현재 시각의 idle 루프로 복귀한다. 고양이에 같은 호출을 걸면 idle이 유지된다 | 클릭 반응 소실, 고양이 오작동 | `React`의 테스트 접근성 |
| **R4** | `React("stretch")` 재생 중 `React(null)`을 호출해도 stretch가 끝까지 재생되고, 종료 후 idle로 복귀한다 | 커밋이 명시적으로 주장한 가드(`PetWindow.cs:91`)의 회귀 | R3와 동일 |
| **R5** | `SetCharacter()`가 진행 중일 때 다시 호출하면 나중 호출의 프레임만 화면에 남는다 (`generation` 폐기) | 비동기 전환 경쟁으로 이전 캐릭터가 눌러앉는 회귀 | 없음 |
| **R6** | `UpdateSettings(ShowPet=false)` → 펫 숨김, `true` → 같은 위치로 복귀. 숨김 상태에서 리마인더는 여전히 열린다 | 숨김/표시 회귀, 숨긴 펫의 자발적 재등장 | 없음 |
| **R7** | 빈 `UNFOLD_DATA_DIR`로 시작하면 `selectedCharacterId`가 `sprout`, 기존 `settings.json`이 있으면 그 값이 유지된다 | 사용자 캐릭터가 임의로 교체되는 회귀 | 없음 |
| **R8** | CI에 게시 산출물 smoke 단계 추가 후, Windows 러너에서 `Unfold.exe --smoke-test`가 종료 코드 0과 `success: true`를 낸다 | 실행 불가능한 게시물 출하 | **총괄의 워크플로 변경 승인** |

R1·R3·R5·R6·R7은 **지금 당장 추가 계약 없이** 작성 가능하다. R2와 R4는 A의 결정을,
R8은 총괄의 결정을 기다린다.

---

## 다음 작업 제안 (최대 3개)

1. **A에게 `IsNight` 시각 주입 티켓을 발행한다.** 이것이 병목이다. 계약이 정해지기
   전에는 Garden의 대표 기능인 낮·밤 전환을 아무도 자동 검증할 수 없고, R2·R4가
   막혀 있다. 한 줄짜리 계약 변경이 회귀 스위트 전체를 연다.
2. **D-02로 `GardenIntegrationTests.cs`에 R1·R3·R5·R6·R7 5개를 작성한다.**
   추가 계약 없이 지금 가능하며, `AppRuntime`/`PetWindow`에 대한 자동 커버리지를
   0에서 벗어나게 하는 가장 짧은 경로다. 제품 코드는 건드리지 않는다.
3. **Windows 실기기 세션의 가용 여부를 총괄이 확정한다.**
   `실제 OS 필요` 11개 항목과 `windows-dogfooding-log.md`의 게이트는 코드로 해소되지
   않는다. 실기기가 없다면 "Windows 출시 가능"은 이번 라운드에서 판정 불가이며,
   그 사실을 명시적 결론으로 남기는 편이 공백을 통과로 오독하는 것보다 낫다.
