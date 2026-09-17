# 릴리스 QA 감사

작성: 2026-09-16 · 담당: release-qa · 대상 커밋: `7e81f77` (`release/mvp`, "9/16 v0.2.0")

## 환경과 범위

| 항목 | 값 |
|---|---|
| OS | macOS (Darwin 25.6.0), 앱 보고 문자열 `Unix 26.6.2` |
| 아키텍처 | arm64 (Apple Silicon) |
| .NET SDK | 10.0.401 · 런타임 `10.0.12` |
| UI 프레임워크 | Avalonia 12.1.2 (`Avalonia.Desktop`, `Avalonia.Themes.Fluent`) |
| 브랜치 / HEAD | `release/mvp` / `7e81f77` (2026-09-16 18:25:23 +0900) |
| 작업 트리 | `CLAUDE.md` 수정, `.claude/` 미추적. **제품 코드·테스트·docs 변경 없음** |
| 데이터 격리 | 진단마다 새 빈 `UNFOLD_DATA_DIR` 3개 사용. 사용자 라이브러리 미사용 |
| 텔레메트리 | `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `AVALONIA_TELEMETRY_OPTOUT=1` |

**새 근거의 성격.** 직전 기록인 [스트레칭 말풍선 검증](../../../../docs/validation/2026-09-16-stretch-speech.md)은
17:56의 **커밋 전 작업 트리**에서 158개 통과를 기록했다. 이번 감사는 18:25에 만들어진
**커밋된 트리 `7e81f77`**을 대상으로 하며, 그 사이 추가된 설정 적용 검사 1개가 포함된 상태다.
따라서 같은 검사의 단순 반복이 아니라 릴리스 후보 커밋에 대한 최초 실행이다.
또한 `Unfold.Desktop.csproj`가 이번 커밋에서 게시 대상 항목(ffmpeg 번들)을 추가했으므로
게시 경로를 다시 실행할 근거가 있다. 게시 산출물 진단과 펫 팩 재생 진단은 이번에 처음 실행했다.

**범위 밖.** 제품 코드·기존 문서는 수정하지 않았다. 다른 작업자의 변경은 건드리지 않았고
저장소 안 `artifacts/`에도 쓰지 않았다. 모든 산출물은 세션 스크래치패드에 남겼다.

## 실행한 명령과 결과

산출물 루트: `/private/tmp/claude-501/-Users-hwanghyeonseong-Documents-GitHub-Unfold/bdfb1865-74bb-458b-8f7a-4e8704336aab/scratchpad/`

### 1. 의존성 복원 — 통과

```sh
dotnet restore Unfold.slnx --locked-mode
```

3개 프로젝트 복원, 약 4.0초. 잠금 파일과 불일치 없음.

### 2. Release 테스트 — 통과

```sh
dotnet test Unfold.slnx -c Release --no-restore
```

**종료 코드 0. Failed: 0, Passed: 159, Skipped: 0, Total: 159, Duration 16 s.**
빌드+테스트 로그 전체에서 `warning`/`error` 문자열 **0건**.

- 직전 기록의 158개 대비 **+1개**. 커밋 직전에 추가된 말풍선 설정 적용 검사가 포함된 결과다.
- `Skipped: 0`이 유의미하다. `CustomPetTests`의 MP4 테스트 2개는
  `SkipUnless = nameof(HasVideoTool)` 조건부이며, `HasVideoTool`은 `PetMediaImporter.FindFFmpeg()`에 의존한다.
  `Tests/Unfold.Tests/bin/Release/net10.0/Tools/ffmpeg`(Mach-O arm64, ffmpeg 8.1.2)가 존재했으므로
  **실제 MP4→GIF 변환과 과길이·손상 MP4 거부가 이번 실행에서 실제로 수행됐다**. 건너뛴 검사가 아니다.

### 3. 소스 빌드의 진단 실행 (격리 프로필) — 통과

```sh
UNFOLD_DATA_DIR=<새 빈 디렉터리> \
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

**종료 코드 0.** `verification/smoke.json`의 `success: true`, PNG **42장** 생성.

| 지표 | 값 |
|---|---|
| `startupMs` | 1348.16 |
| `totalMs` | 15136.73 |
| `workingSetBytes` | 110,198,784 (약 105 MiB) |
| `managedBytes` | 109,850,232 |
| `oneCoreCpuPercent` | 55.96 |
| `characters` / `imageFiles` | 4 / 42 |
| `completedBreaks` / `exportedBreaks` | 1 / 1 (`simulated*` 플래그 true) |

플래그 전체 true: `timerControlsVerified`, `petSpeechDirectionsVerified`,
`speechOvertimeAndFoldVerified`, `soundRequestsVerified`, `hiddenPetNoticeVerified`,
`stopWhileOpening`, `petPackInstallUpdateRepairVerified`, `customPetGifAuthoringVerified`,
`sharedDesignDialogsVerified`, `pinnedPageActionsVerified`,
`settingsLayout`(860×680 최소 크기, 탭·스크롤·가로 넘침 없음, 펫 초안 보존).

### 4. macOS arm64 자체 포함 게시 — 통과

```sh
dotnet publish src/Unfold.Desktop -c Release -r osx-arm64 --self-contained true \
  -p:PublishReadyToRun=true -o <스크래치 디렉터리>
```

**종료 코드 0, 경고 0건.** 산출물 155 MB. `Unfold`는 Mach-O arm64.
`Tools/ffmpeg`(Mach-O arm64)와 라이선스 4종(`COPYING.LGPLv2.1`, `FFMPEG-LICENSE.md`,
`OPENSSL-LICENSE`, `SOURCE.txt`)이 게시물에 포함됐다. RID별 `Content` 글롭이 osx-arm64를 정확히 선택했다.

### 5. 게시된 실행 파일의 진단 (별도 격리 프로필) — 통과

```sh
UNFOLD_DATA_DIR=<두 번째 새 빈 디렉터리> <게시 디렉터리>/Unfold --smoke-test
```

**종료 코드 0**, `success: true`, PNG **42장**, `startupMs` 1859.84, `totalMs` 15128.43,
`workingSetBytes` 146,292,736. 소스 빌드가 아니라 **배포 형태의 바이너리**에서 얻은 결과다.

### 6. 펫 팩 재생 진단 — **실패**

```sh
UNFOLD_DATA_DIR=<세 번째 새 빈 디렉터리> \
dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --review-pet-pack <repo>/artifacts/pet-packs/Bori-0.1.0.unfoldpet
```

**종료 코드 1.** `verification/pet-review.json`:

```
"success": false,
"error": "System.InvalidOperationException: The desktop pet did not enter the requested reaction.
   at Unfold.Desktop.PetPackDiagnostics.Run(...) PetPackDiagnostics.cs:line 68"
```

PNG는 4장만 생성됐다(`pack-preview.png`, `pack-preview-large-light-2x.png`,
`pack-preview-large-dark-2x.png`, `pack-installed.png`). 클립별 재생 캡처는 한 장도 없다.

**근본 원인(코드로 확인).**

- `src/Unfold.Desktop/PetPackDiagnostics.cs:67`과 `:74`가 `runtime.ActivePet.Content`를
  `AnimationView`로 캐스팅한다.
- `src/Unfold.Desktop/PetWindow.cs:38`에서 `Content`는 이제 `Canvas`다
  (`canvas.Children.Add(animation); canvas.Children.Add(bubble); canvas.Children.Add(tail); Content = canvas;`).
- `git show 7e81f77 -- src/Unfold.Desktop/PetWindow.cs`가 이 변경을 직접 보여준다:
  `- ... Content = animation;` → `+ ... Content = canvas;`.
- 같은 커밋에서 `SmokeDiagnostics.cs`는 +168줄로 갱신됐지만 **`PetPackDiagnostics.cs`는 변경 목록에 없다**.
  이 파일의 마지막 수정은 `e0149e7`(2026-09-14)이다.
- 결과적으로 루프의 **첫 항목인 `idle`에서 즉시 예외**가 발생하며, 어떤 팩을 넣어도 실패한다.

원인을 바꾸지 않은 채 같은 명령을 반복하지 않았다. 재현은 1회이고 판정 근거는 소스 대조다.

## 경계면 교차 검증

소스 양쪽을 대조해 확인한 결과다. 별도 표기가 없으면 자동 검사 또는 정적 대조 근거다.

| 경계 | 생산자 | 소비자 | 결과 |
|---|---|---|---|
| 말풍선 방향 설정 | `SettingsWindow.Notifications.cs:15-30` (`ComboBox "BubbleDirection"`) | `PetWindow.cs:111` → `PetBubbleLayout.Create` (`PetSpeechBubble.cs:73-81`, 4방향 모두 정의) | 일치. `AppSettings.cs:58`이 `Enum.IsDefined`로 범위 밖 값 거부 |
| 미루기 분 | `SettingsWindow.Notifications.cs:19-31` (`NumericUpDown "SnoozeMinutes"`) | `PetWindow.cs:108` `bubble.Refresh(runtime.Reminder, Settings.SnoozeMinutes)` | 일치. 1~60 범위 검증이 저장 시점(`AppSettings.cs:58`)과 UI 양쪽에 존재 |
| 효과음 설정 | `SettingsWindow.Notifications.cs:42-64` | `ReminderSoundPlayer.cs:16-19`, `AppRuntime.cs:149,254` | 일치. `ReminderSoundId`/`CompletionSoundId`를 `ValidSoundId`로 검증(`AppSettings.cs:59`), 경로 탈출 문자열 거부 테스트 존재 |
| 접기 상태 | `AppRuntime.cs:244` `ToggleBubble` | `PetWindow.cs:43,109,110` 메뉴 헤더·확장 판정 | 일치. 접기가 세션을 종료하지 않음(`HasNotice`와 분리) |
| 휴식 기록 CSV | `BreakReview.cs:13` 헤더 8열 | `BreakReview.cs:17-21` 행 8셀 | 일치. `actual_seconds`가 **마지막 열로 추가**되어 기존 7열 순서 보존. `Cell()`이 수식 문자 앞에 `'` 삽입 |
| 실제/계획 휴식 시간 | `BreakHistory.Add` (`ActualSeconds = ceil(session.Elapsed)`) | `CompletedBreak.RecordedSeconds`(`ActualSeconds ?? Seconds`) → `ForDay`, `Review` 합계 | 일치. 구버전 레코드(`ActualSeconds = null`)는 계획 시간으로 대체돼 하위 호환 |
| 알림 상태 → 펫 표시 | `AppRuntime.Tick`, `StartBreak`/`SnoozeBreak`/`CompleteBreak`/`CancelReminder` 모두 `RefreshPetNotice()` 호출 (`AppRuntime.cs:120,132,217,234,239-241`) | `PetWindow.RefreshSpeech`, `ShowPet`/`HidePet` (`AppRuntime.cs:258-265`) | 일치. 상태 전이 경로마다 갱신이 걸려 있고 초과 시간 표시도 `Tick`에서 매 주기 갱신 |
| 숨긴 펫 + 반응 | `AppRuntime.ReactToBreak` (`:268`)이 `pet is { IsVisible: true }`일 때만 `React` | `PetWindow.React` | 일치. 숨긴 펫을 반응이 드러내지 않는 계약이 코드에 존재. 단, 이 계약의 **런타임 증거였던 검사는 아래 실패 항목에 포함** |
| 창 위치 · 고DPI | `PetWindow.cs:22,71,112,114,124,130` 전부 `DesktopScaling` | — | 일치. `RenderScaling` 사용처는 `PetPackDiagnostics.cs:145`의 기록용 필드 하나뿐 |
| 펫 창 시각 트리 ↔ 진단 | `PetWindow.cs:38` `Content = Canvas` | `PetPackDiagnostics.cs:67,74` `Content is AnimationView` | **불일치. 이번 실패의 원인** |
| 미디어 도구 번들 | `.tools/media-lgpl/$(MediaRuntimeIdentifier)/*` (`Unfold.Desktop.csproj:17-21`) | `PetMediaImporter.FindFFmpeg()` (`UNFOLD_FFMPEG_PATH` → `AppContext.BaseDirectory/Tools` → `PATH`) | osx-arm64 게시에서 일치 확인. **`.tools/`는 `.gitignore:18`로 미추적**이므로 새 클론에서는 글롭이 조용히 비고, 도구 없는 배포본이 만들어진다 |
| 도구 부재 시 동작 | — | `PetMediaImporter.cs:28` | 안전. 한국어 안내 예외("영상 변환 도구가 없는 앱이에요...")로 실패하며 무음 실패가 아니다 |
| 버전 문자열 | `Unfold.Desktop.csproj:6` `<Version>0.1.1</Version>` | `Scripts/publish-desktop.ps1:15`(zip 이름), `Scripts/make-macos-bundle.sh:11,35`(`CFBundleShortVersionString`) | **불일치. 커밋은 "v0.2.0"인데 버전 단일 출처는 0.1.1** |

## 통과·실패·미검증

### 자동 검사 — 통과 (커밋 `7e81f77`, macOS arm64)

- Release 전체 테스트 **159개 통과 / 실패 0 / 건너뜀 0**, 빌드 경고 0.
- 실제 ffmpeg 8.1.2(arm64)로 **MP4→GIF 변환 성공, 과길이·손상 MP4 거부** 2건 실제 실행.
- 소스 빌드 진단: 종료 코드 0, `success: true`, PNG 42장, 말풍선 4방향·초과·접기·효과음 요청 횟수·
  숨긴 펫 안내·타이머 조작·펫 팩 설치/업데이트/복구·커스텀 펫 GIF 저작·설정 레이아웃 전부 true.
- macOS arm64 자체 포함 게시: 종료 코드 0, 경고 0, ffmpeg와 LGPL 라이선스 파일 동봉.
- **게시된 바이너리**의 진단: 종료 코드 0, `success: true`, PNG 42장.

### 자동 검사 — 실패

- **펫 팩 재생 진단(`--review-pet-pack`)이 종료 코드 1로 실패한다.** `PetPackDiagnostics`가
  `PetWindow.Content`를 `AnimationView`로 가정하지만 현재는 `Canvas`다. 첫 클립(`idle`)에서 중단된다.
- 이 실패는 진단 코드의 계약 불일치이며, **제품 런타임의 펫 애니메이션 결함으로 확인된 것은 아니다**.
  실제 재생은 `PetWindow`의 비공개 `animation` 필드가 담당하고 그 경로는 이 검사가 건드리지 않는다.
  그러나 재생 정상 동작의 **증거는 사라졌다**(아래 미검증 참조).
- 자동 테스트 159개 중 이 실패를 잡을 수 있는 것은 없다. `PetPackDiagnostics`를 참조하는 테스트는
  존재하지 않으며(`App.cs:24`, `Program.cs:12`의 CLI 경로만 사용), 헤드리스 테스트는
  `PetWindow.Content`의 구조를 검사하지 않는다.

### 실제 macOS에서 관찰 — 부분 통과

이번 실행은 모두 실제 macOS arm64 기기에서 수행했으나, 창은 off-screen이고 버튼 이벤트는 프로그래밍 방식이다.

- 통과: 게시된 앱이 실제 macOS에서 초기화·종료(종료 코드 0), 펫 팩 **미리보기 조작**(일시정지 유지,
  Replay, 192/384 전환, 밝은·어두운 2배 캡처, 가로 넘침 없음), **팩 설치·선택·타이머 Pause 유지**,
  설치물 자산 감사 오류 0 — 여기까지는 실패한 진단에서도 예외 이전에 완료됐고 PNG 4장으로 남았다.
- 미실행: 실제 마우스·키보드 입력, OS 파일 선택 창, 드래그, 다중 모니터·DPI 전환,
  로그인 항목 실행, 실제 60분 대기, 장시간 자원 사용, 사람의 청감 확인.
- 기존 macOS 실기 기록은 [2026-09-11 부분 관찰](../../../../docs/validation/2026-09-11-macos-dogfooding.md)이며
  커밋 `84c8405` 기준이다. 현재 커밋의 통과 증거로 쓰지 않는다.

### 실제 Windows — 전부 미검증

- [Windows 실기 체크리스트](../../../../docs/windows-dogfooding-log.md)는 **체크된 항목 0개, 미체크 53개**이며
  문서 스스로 "아직 실행 결과가 기입되지 않았다"고 명시한다.
- 이번 macOS 결과를 Windows 판정으로 옮기지 않는다. 특히 다음은 Windows 고유이며 증거가 없다:
  투명 영역 클릭 통과, 트레이 동작, `win-x64`/`win-arm64` 게시물과 동봉 ffmpeg의 실행,
  OS 신뢰·서명 경고, 고DPI 혼합 모니터에서의 말풍선 배치.

### 사람 검수 필요 — 미실행

- 말풍선 문구와 안내의 자연스러움, 초과 시간 표시가 주는 압박감.
- 알림이 실제 작업 집중을 끊는 정도와 반복 사용 의사(60–90분 도그푸딩).
- 펫 애니메이션 프레임의 시각적 자연스러움, 2배 렌더링 결과의 아트 승인.
- 효과음의 실제 청감(이전 기록의 `afplay` 종료 코드 0은 재생 명령 성공일 뿐 청취 증거가 아니다).

### 미검증 (증거 없음)

- **펫 팩 클립 재생, 1회 반응 완료 이벤트, 반응 후 idle 루프 복귀, 숨긴 상태 유지** — 이를 확인하는
  유일한 경로가 위 실패로 막혔다. 이 항목들은 현재 커밋에서 통과로 기록할 수 없다.
- 깨끗한 클론에서의 빌드 재현성(`.tools/` 미추적으로 `Unfold.MediaSetup` 선행 실행 필요).
- Windows·Intel Mac 게시물 생성과 실행.
- 코드 서명·공증·배포 채널.
- 장시간 안정성과 자원 사용. 진단의 `oneCoreCpuPercent` 55.96은 15초 표본이며 성능 판정 근거가 아니다.

## 출시 차단 요소

| 순위 | 항목 | 근거 | 성격 |
|---|---|---|---|
| 1 | **펫 팩 재생 검증 경로 단절** | `PetPackDiagnostics.cs:67,74` ↔ `PetWindow.cs:38`. 종료 코드 1 | 검증 차단. 이것이 고쳐지기 전에는 팩 재생 품질에 대한 출시 판단 근거가 없다 |
| 2 | **Windows 실기 증거 0** | 체크리스트 53개 전부 미체크 | 출시 차단. Windows를 지원 대상에 넣는다면 macOS 결과로 대체 불가 |
| 3 | **버전 문자열 0.1.1 ↔ 릴리스 v0.2.0 불일치** | `Unfold.Desktop.csproj:6`이 zip 이름과 `CFBundleShortVersionString`의 단일 출처 | 출시 차단. 지금 패키징하면 `Unfold-v0.1.1-*.zip`과 번들 버전 0.1.1이 나온다 |
| 4 | **미디어 도구가 저장소에 없음** | `.gitignore:18`의 `.tools/`, `Unfold.Desktop.csproj:17` 글롭 | 출시 위험. 선행 단계를 모르는 빌드는 MP4 기능이 빠진 배포본을 조용히 만든다. 런타임 안내는 정상이므로 무음 실패는 아니다 |
| 5 | **서명·공증 미확인** | 이번 및 이전 기록 모두 범위 밖 | 배포 전 필수 |

1·3은 제품 코드 수정이 필요하므로 이 감사 범위 밖이다. 구현 담당에게 전달해야 한다.

## 다음 검증 순서

1. **`PetPackDiagnostics`를 현재 `PetWindow` 시각 트리에 맞춘다.** `Content`를 캐스팅하는 대신
   `GetVisualDescendants().OfType<AnimationView>()`로 펫의 애니메이션 뷰를 찾도록 `:67`과 `:74`를 고친다.
   재발 방지로 헤드리스 테스트 1개를 추가해 "펫 창에서 `AnimationView`를 찾을 수 있고
   반응 후 `Repeats`가 복원된다"를 고정한다. 그 뒤 위 6번 명령을 재실행해
   `pet-review.json`의 `success: true`와 클립별 PNG를 확보한다.
2. 1번 통과 후 **동일 명령을 게시된 바이너리로도 1회** 실행해 배포 형태에서의 재생을 확인한다.
3. **버전을 0.2.0으로 올린 뒤** `Scripts/make-macos-bundle.sh`로 번들을 만들고
   `CFBundleShortVersionString`과 zip 이름을 확인한다(현재는 재작업이 확정되므로 지금 실행하지 않았다).
4. **Windows 실기 세션.** `win-x64` 게시 → `Tools/ffmpeg.exe` 동봉 확인 → 격리 `UNFOLD_DATA_DIR`로
   진단 1회 → [체크리스트](../../../../docs/windows-dogfooding-log.md)를 복사한 새 기록에 클릭 통과,
   말풍선 4방향, 트레이, 고DPI를 기입한다. 이 단계만이 2번 차단 요소를 해제한다.
5. **macOS 60–90분 도그푸딩.** 실제 간격으로 알림을 받고 초과 표시·미루기·완료를 손으로 조작해
   사람 검수 항목을 채운다.
6. `.tools/` 선행 단계를 빌드 실패나 명시적 경고로 드러낼지 결정한다(제품/아키텍처 판단 필요).
7. 서명·공증을 별도 세션으로 실행한다.

### 재사용 가능한 검사 명령

후속 구현의 모듈 완료 직후 아래 순서로 점진 QA를 돌린다. 매번 새 `UNFOLD_DATA_DIR`을 쓴다.

```sh
export DOTNET_CLI_TELEMETRY_OPTOUT=1 AVALONIA_TELEMETRY_OPTOUT=1
dotnet restore Unfold.slnx --locked-mode
dotnet test Unfold.slnx -c Release --no-restore                 # 기대: 159개 이상 통과, 실패·건너뜀 0
UNFOLD_DATA_DIR="$(mktemp -d)" \
  dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test   # 기대: 종료 0, success true, PNG 42장
UNFOLD_DATA_DIR="$(mktemp -d)" \
  dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll \
  --review-pet-pack artifacts/pet-packs/Bori-0.1.0.unfoldpet    # 현재 종료 1 — 1번 수정 후 종료 0이 수용 기준
```

수용 기준: 테스트 건너뜀 0(건너뜀이 생기면 ffmpeg 준비 여부를 먼저 확인),
`smoke.json`의 모든 `*Verified` 플래그 true, PNG 42장,
`pet-review.json`의 `success: true`와 클립별 `petReturnedToIdleLoop: true`.
