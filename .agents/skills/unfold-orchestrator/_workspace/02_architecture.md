# C#·Avalonia 아키텍처 감사

- 날짜: 2026-09-16 / 브랜치: `release/mvp` / 커밋: `7e81f77`
- 대상: `src/Unfold.Core`, `src/Unfold.Desktop`, `Tests/Unfold.Tests`, `docs/mvp.md`
- 근거 구분: 아래 **[코드]** 는 현재 체크아웃 정적 확인, **[실행]** 은 이번 세션에서 실제로 돌린 명령의 결과다.
  `archive/`, `reference/`, `Sources/`, `Unfold.xcodeproj`, `.swiftpm`은 현재 런타임으로 보지 않았다.

**[실행] 이번 감사에서 실제로 확인한 것**

```
dotnet build Unfold.slnx -c Release      → exit 0, 0 Warning(s), 0 Error(s)
dotnet test  Unfold.slnx -c Release      → Passed 159 / Failed 0 / Skipped 0 (17 s)
```

`Directory.Build.props`가 `TreatWarningsAsErrors=true`, `Nullable=enable`, `net10.0`,
`RestorePackagesWithLockFile=true`를 전 프로젝트에 강제하므로 경고 0은 빌드 게이트의 결과다.
스모크 진단(`--smoke-test`)과 펫 팩 진단(`--review-pet-pack`)은 **실행하지 않았다**(§미검증 항목).

---

## 현재 구성과 데이터 흐름

### 프로젝트 경계

| 프로젝트 | 역할 | 외부 의존 |
|---|---|---|
| `Unfold.Core` (17개 파일) | 도메인·저장 계약. Avalonia 참조 없음 | SkiaSharp 3.119.4 |
| `Unfold.Desktop` (21개 파일, `WinExe`, `AssemblyName=Unfold`) | Avalonia UI·플랫폼 서비스·진단 | Avalonia 12.1.2 (+Fluent), Core |
| `Tests/Unfold.Tests` | xunit.v3 3.2.2 + Avalonia.Headless.XUnit | Desktop 전체 참조 |
| `tools/Unfold.MediaSetup` | MP4 임포트용 ffmpeg(LGPL) 배치. **`Unfold.slnx`에 없다** | — |

`Unfold.Desktop.csproj`가 빌드 입력으로 끌어오는 자산은 두 갈래다.
`Assets/Characters/**/*` → 출력 `Assets/Characters/`(번들 펫), `.tools/media-lgpl/$(MediaRuntimeIdentifier)/*` → 출력 `Tools/`(ffmpeg).
`Art/`는 제작 원장이며 번들에 포함되지 않는다.

### 단일 상태 소유자: `AppRuntime`

`AppRuntime`(329줄)이 사실상 유일한 애플리케이션 서비스다. 보유 상태와 소비자는 다음과 같다.

```
Program.Main ──(.instance.lock, FileShare.None)── App.OnFrameworkInitializationCompleted
                                                     │
                                       ┌─────────────┴─────────────┐
                              --smoke-test / --review-pet-pack   AppRuntime.Start(background)
                                                                   │
   ┌────────────────┬──────────────┬───────────────┬───────────────┴────────┬──────────────┐
AppSettings     StretchClock    PetReminder    CharacterLibrary        BreakHistory     TrayIcon
(settings.json) (monotonic)     (BreakSession) (+ clips 캐시)       (break-history.json)
   │                │               │               │                     │
   └────────────────┴───────────────┴───────────────┴─────────────────────┘
                                  event Action? Changed          ← 1 Hz DispatcherTimer + 모든 변경 지점
                                          │
              ┌───────────────────────────┴─────────────────┐
     SettingsWindow.Refresh                          PetWindow.RefreshSpeech
     (+ 하위 페이지 4종)                              (+ PetSpeechBubble.Refresh)
```

핵심 성질:

- **시간은 단조 시계 1개**(`Stopwatch monotonic`, AppRuntime.cs:31). `StretchClock`·`BreakSession` 모두
  `TimeSpan now`를 주입받는 순수 상태 기계라 테스트·진단에서 시간을 합성할 수 있다(`SmokeDiagnostics` 126–128행이 이를 사용).
- **10초 초과 델타 폐기**(StretchClock.cs:43, BreakSession.cs:92). sleep·디스패처 정체가 작업시간/휴식시간으로 적립되지 않는다.
- **`Changed`는 무인자 브로드캐스트**다. 어떤 필드가 바뀌었는지 신호가 없어 모든 구독자가 전체 재계산한다.
- UI 갱신은 **폴링형**(1 Hz Tick → `Changed`)과 **이벤트형**(`UpdateSettings` 끝의 `Changed`)이 섞여 있다.

### 저장 계약

| 파일 | 위치 | 생산자 | 소비자 | 보호 |
|---|---|---|---|---|
| `settings.json` | `AppPaths.DataRoot` | `AppSettings.Save` | `AppSettings.Load` | 256 KiB 상한, 원자적 쓰기, 실패 시 `.invalid-<ts>` 사본 후 기본값 복구 (AppRuntime.cs:54–65) |
| `break-history.json` | 〃 | `BreakHistory.Save` | `BreakHistory.Load` | 4 MiB·2000건 상한, `Version==1`, SessionId 중복 거부 |
| `Characters/<id>/` | 〃 | `CharacterLibrary.Save`(에디터) / `Install`(팩) | `List`/`LoadPackage` | `.library.lock` 단일 쓰기, `.stage-*`/`.backup-*` 롤백, `Recover()` |
| `Sounds/*.wav` | 〃 | `ReminderSounds.Import` | `ReminderSounds.Resolve` | SHA-256 파일명, 5 MiB·30초·PCM 8/16bit 검증 |
| `verification/` | 〃 | 진단 모드만 | 사람 | `UNFOLD_DATA_DIR` 격리 |

`AppPaths.DataRoot`는 `UNFOLD_DATA_DIR` → macOS `~/Library/Application Support/Unfold` → 그 외 `LocalApplicationData/Unfold` 순이다.
모든 쓰기는 `AtomicFile.Write`(임시 파일 + `FileOptions.WriteThrough` + `Flush(true)` + `File.Move` 덮어쓰기)를 거친다.

### 캐릭터·manifest 소비 계약 (요청 범위)

`CharacterLibrary.LoadPackage`가 **유일한 manifest 해석 지점**이고, 세 소비자가 같은 함수를 공유한다.

```
character.json ──LoadPackage()── CharacterPackage
   │  Version==1, SafeId(id), Name 비어있지 않음
   │  sheet: cols/rows 1..256, frame 1..2048, cols*fw ≤ 4096, rows*fh ≤ 4096
   │  실제 PNG 크기 == cols*fw × rows*fh  (CharacterLibrary.cs:88)
   │  "idle" 필수                        (CharacterLibrary.cs:89)
   │  clip = Gif 경로(존재 확인) XOR Frames[1..4096]+Fps[1..120]
   ├─▶ 런타임 재생:  AppRuntime.Clip(key) → LoadAnimation(key)  ※없는 키는 idle로 폴백 (CharacterLibrary.cs:28)
   ├─▶ 팩 검증:      CharacterPack.ValidatePayload → 16클립·128 MiB 예산 + CharacterAssetAudit
   └─▶ 자산 감사:    CharacterAssetAudit.InspectPackage → idle must loop / oneshot must not loop
```

여기서 **경로 3개가 같은 manifest를 서로 다른 불변식으로 본다**는 점이 이 코드베이스의 가장 중요한 계약이다.

- 런타임(`LoadAnimation`)은 **관대**하다. 미정의 키는 조용히 idle로 대체한다.
- 팩 검증(`ValidatePayload`)은 **엄격**하다. `audit.Files`가 `pack.json`의 인벤토리와 **집합 동치**여야 한다(CharacterPack.cs:125).
- 감사(`CharacterAssetAudit.InspectPackage`)가 인벤토리 대상 파일을 정의한다: `character.json` + `spriteSheet.File` + 모든 `definition.Gif`(CharacterAssetAudit.cs:48,54).
  → **`source.piskel`은 인벤토리에 절대 들어가지 않는다.** 이것이 "에디터 작품"과 "펫 팩"을 가르는 실제 경계다.

리비전(무결성 지문)도 두 체계가 공존하며, 이는 의도된 분리다.

| | 대상 | 해시 입력 | 사용처 |
|---|---|---|---|
| `CharacterLibrary.Revision` | 에디터 작품 | `source.piskel`, `spritesheet.png`, `character.json` 3개 고정 | `OpenForEditing`/`Save` 낙관적 동시성 |
| `CharacterLibrary.PackRevision` | 설치된 팩 | 디렉터리 내 **전체 파일**(`pack.json` 포함) | `InspectInstall`/`Install` 낙관적 동시성 |

`InspectInstallLocked`(CharacterPack.cs:186)가 `source.piskel` 존재를 보고 팩 설치를 거부하므로 두 체계가 교차하지 않는다.
**후속 작업자가 이 둘을 "중복"으로 보고 통합하면 에디터 작품이 팩으로 덮어써진다.**

커스텀 펫은 이 계약을 GIF만으로 만족시킨다. `CustomPetDraft.Export`(CustomPetDraft.cs:69–74)는
idle 첫 프레임을 1×1 스프라이트시트로 써서 `SpriteSheet` 필수 필드를 채우고, 모든 동작을 `Gif:`로 선언하며
`Loop = (key == "idle")`로 감사 규칙(idle 반복, oneshot 비반복)을 충족한다.
즉 **스프라이트시트는 커스텀 펫에서 순수한 형식 세금**이고, 실제로 슬라이싱되지 않는다.

### UI 합성

`SettingsWindow`는 partial 3분할(`.cs` 상태·Refresh / `.Layout.cs` 대시보드·사이드바 / `.Notifications.cs` 말풍선 카드)이고,
좌측 레일 4탭이 `settingsPageHost.Content`를 갈아끼운다.

```
SettingsNavTimer   → dashboardPage (대시보드 그리드, 생성자에서 1회 구성)
SettingsNavRoutines→ PersonalizationView   (?? = 지연 생성, 진입마다 Refresh())
SettingsNavReview  → BreakReviewView       (?? = 지연 생성, 진입마다 Refresh())
SettingsNavPacks   → PetManagementView     (?? = 지연 생성, Refresh 없음 = 초안 보존)
```

모달 창은 `RoutineEditorWindow`, `ProfileEditorWindow`, `Ui.Confirm/Prompt/Error`만 운영 경로에서 쓰인다.

---

## 구현·문서 정합성

### 일치 확인(표본 교차)

| `docs/mvp.md` 주장 | 코드 근거 | 판정 |
|---|---|---|
| 타이머 5–240분, 실행 중 간격 잠금, 명시적 **적용** | `AppSettings.Validate`(5..240) + `CanEditTimerInterval`(AppRuntime.cs:23) + `interval.IsEnabled`(SettingsWindow.cs:136) + `reminderApply` | 일치 |
| Stop은 00:00, Play는 Stop 후 전체 간격 재시작 | `StretchClock.Stop`(Remaining=Zero) / `Start`(`if (Stopped) Remaining = Interval`) | 일치 |
| Stop이 열린 초대를 완료 기록 없이 해제 | `AppRuntime.Stop → CancelReminder → Reminder.Cancel → Skip()` → `FinishBreak` else 분기(기록 없음) | 일치 |
| 초과 +60:00 상한, 자동 완료 없음 | `BreakSession.MaximumOvertime` + `Tick`의 `available` 클램프, `Complete()`는 사용자 호출만 | 일치 |
| 접기/숨김이 세션을 끝내지 않음 | `ToggleBubble`은 `BubbleCollapsed`만 변경, `PetReminder.Session` 불변 | 일치 |
| 완료만 기록, 계획초·실측초 병기 | `BreakHistory.Add`의 `State != Completed` 가드 + `ActualSeconds` + `RecordedSeconds` 폴백 | 일치 |
| 90일 초과 삭제, 2000건 상한 | BreakHistory.cs:40,42 | 일치 |
| CSV에 `planned_seconds` 유지 + `actual_seconds` 추가 | `BreakReview.Csv` 헤더(BreakReview.cs:13) | 일치 |
| Windows만 픽셀 클릭 통과 | `PetWindow.UpdateClickThrough`의 `if (!OperatingSystem.IsWindows()) return`(PetWindow.cs:139) | 일치 |
| Mochi는 idle 8프레임 + `stretch.gif`, `click` 없음 | `Assets/Characters/default-cat/character.json` (frames 0..7, fps 7, stretch=gif) | 일치 |
| 사용자용 픽셀 에디터 없음 | `AppRuntime.OpenEditor` 호출자는 `SmokeDiagnostics.cs:90` **하나뿐** | 일치 |
| 수동 "지금 스트레칭" 제거, 진단만 사용 | `ShowReminder()` 호출자: `Tick`(자동), `SmokeDiagnostics`, `PetPackDiagnostics` | 일치 |
| 스누즈 1–60분, 기본 5 | `AppSettings.SnoozeMinutes` 검증 + `FinishBreak`의 `ScheduleAfterBreak(delay)` | 일치 |
| 중복 알림이 세션·효과음을 재시작하지 않음 | `ShowReminder` 최상단 `if (Reminder.Session is not null)` + `openingReminder` 코얼레싱 | 일치 |
| 루틴 저장이 다음 초대용 루틴을 선택 | `AppSettings.SaveRoutine`이 `BreakRoutineId = routine.Id`(Personalization.cs:65) | 일치 |

### 불일치·문서 공백

1. **`docs/verification.md`의 테스트 수가 실측과 어긋난다.** 문서가 인용하는 최신 수치는 147개
   (`validation/2026-09-16-custom-pet.md` 기준)인데 **[실행]** 결과는 159개다. 문서가 시점 기록이라 "틀렸다"기보다
   현재 커밋(`7e81f77`)에 대한 기록이 없다. 새 검증 기록 없이 이 숫자를 인용하면 안 된다.
2. **CI의 OS 비대칭이 문서에 없다.** `.github/workflows/desktop.yml`에서 패키지 스모크 테스트는
   **Windows 전용**이고(`if: runner.os == 'Windows'`), macOS 잡은 `dotnet test` + 번들 생성까지만 한다.
   `docs/verification.md`는 스모크 실행 명령만 안내할 뿐 "macOS 패키지 앱은 CI가 검증하지 않는다"를 말하지 않는다.
   `windows-dogfooding-log.md`가 Windows 실기 양식인 것과 짝이 맞지 않는다.
3. **`SaveRoutine`이 `ActiveProfileId`를 지운다**(Personalization.cs:65). `docs/mvp.md`는 "저장이 루틴을 선택한다"만
   적고 업무 프로필 해제는 적지 않았다. 사용자가 프로필을 쓰다가 루틴을 편집하면 상단 표시가 "직접 설정한 알림"으로 바뀐다.
4. **기록 파일 열기 실패 시 동작이 문서에 없다.** `historyWritable=false`가 되면 세션 내 집계는 되지만
   디스크에는 쓰지 않는다(AppRuntime.cs:71–72, 275). 문구는 UI에만 있고 `mvp.md` "Completion history" 항목에는 없다.
5. **`tools/Unfold.MediaSetup`이 솔루션 밖**이다. `Unfold.slnx`는 3개 프로젝트만 포함하므로
   `dotnet build Unfold.slnx`로는 MP4 임포트 전제(ffmpeg 배치)가 준비되지 않는다.
   `verification.md`는 이를 언급하나(테스트 건너뜀), `cross-platform.md`·빌드 입력 설명과의 연결은 확인 대상이다.

---

## 위험과 기술 부채

우선순위는 **핵심 루프(작업 → 초대 → 휴식 → 기록) 손상 가능성** 기준이다.

### R1 · `SavePosition`이 설정 스냅샷 경합을 만든다 — 중

`AppRuntime.SavePosition`(AppRuntime.cs:153–158)은 `Settings`를 직접 교체하고 저장하지만 `Changed`를 발생시키지 않는다.
한편 모든 UI 핸들러는 `runtime.Settings with { ... }` 형태로 **호출 시점 스냅샷**을 캡처해 `UpdateSettings`에 넘긴다.
펫 드래그 종료와 `await`가 섞인 설정 저장이 교차하면 `PetX/PetY`가 소리 없이 되돌아간다.
드래그 1회당 1회 디스크 쓰기라는 점도 함께 본다.

- 생산자: `PetWindow.PointerReleased`(PetWindow.cs:58–64) → 소비자: `PetWindow.Opened`(PetWindow.cs:71).
- 양쪽 경계는 연결되어 있다. 결함은 연결이 아니라 **동시성**이다.

### R2 · 오류 메시지 소실 — 중

`UpdateSettings`는 실행 중 간격 변경을 `ArgumentException("타이머를 일시정지하거나 중지한 뒤 …")`로 막는데,
`SettingsWindow.SaveReminderSettings`의 catch(SettingsWindow.cs:108–109)가 이를 일반 문구
"알림 설정을 저장하지 못했어요"로 덮는다. 같은 패턴이 `PersonalizationView.Action`에서는 반대로
`ArgumentException`일 때 원문을 보존한다(PersonalizationWindow.cs:93). 두 화면의 오류 정책이 다르다.

### R3 · `CharacterManifest.Animations`가 가변 `Dictionary`로 공유된다 — 중

`CharacterManifest`(CharacterLibrary.cs:9–10)는 record지만 `Dictionary<string, AnimationDefinition>`를 노출한다.
`CharacterPackage`는 `Characters` 목록에 캐시되므로, 어떤 코드든 런타임 중 맵을 바꾸면 재생·감사·팩 검증이 동시에 어긋난다.
현재 실제 변형은 진단 코드에서만 일어난다(`SmokeDiagnostics.cs:230`이 `manifest.Animations[key] = …`).
읽기 전용 래핑이 없다는 사실 자체가 부채다.

### R4 · `RenderStyle`이 검증되지 않는 자유 문자열 — 하

`LoadPackage`는 `RenderStyle`을 전혀 검증하지 않고, 소비자는 `== "pixel"` 한 곳뿐이다
(PetWindow.cs:83,97 / SettingsWindow.cs:175 / PetPackWindow.cs:158). 생산자는 세 값을 쓴다:
에디터 저장 `"pixel"`, 커스텀 펫 `"smooth"`, 번들 Mochi `null`. 오타는 조용히 `smooth`로 흡수된다.

### R5 · 운영 경로와 진단 경로가 서로 다른 호스트를 쓴다 — 중

`PersonalizationWindow` / `BreakReviewWindow` / `PetPackWindow` / `CustomPetWindow` 4개 Window는
**운영 UI에서 한 번도 생성되지 않는다**. 운영은 `SettingsWindow` 안의 `*View`를 쓰고,
Window 래퍼는 `SmokeDiagnostics`(73, 146, 236, 270, 296행)와 테스트에서만 생성된다.

결과: 스모크 진단과 headless 테스트가 **사용자가 실제로 보는 호스트를 검사하지 않는다.**
`VerifySettingsLayout`이 in-window 탭을 별도로 검사해 일부를 보완하지만(사이드바 4탭·초안 보존 확인),
`PersonalizationView`/`BreakReviewView`의 버튼 동작은 Window 래퍼 경로로만 검증된다.

### R6 · `AppRuntime` 단일 클래스의 책임 과밀 — 중

329줄에 7개 책임(설정 영속화, 시계 틱, 트레이, 캐릭터 라이브러리·클립 캐시, 리마인더 오케스트레이션,
창 수명주기, 효과음)이 모여 있다. 병렬 작업에서 **모든 런타임 변경이 이 한 파일에서 충돌한다.**
후속 작업 분배의 최대 제약이며, 아래 §작업 분할이 이를 전제로 설계됐다.

### R7 · `Reload()`가 클립 캐시 전체를 버린다 — 하

`AppRuntime.Reload`(AppRuntime.cs:105)의 `clips.Clear()`는 설치·에디터 저장 시 idle 클립을
펫과 설정 미리보기 양쪽에서 재디코딩시킨다. 캐시 키가 `DirectoryPath:key`라 디렉터리별 무효화가 가능한데도 전역으로 비운다.
호출 빈도가 낮아 현재 영향은 작다.

### R8 · 1 Hz 전체 재계산 — 하

`Tick` → `Changed` → `SettingsWindow.Refresh`가 매초 `BreakHistory.ForDay`(최대 2000건 LINQ)와
문자열 포매팅 10여 개를 수행한다. 현재 규모에선 문제없지만 기록 상한을 올리면 먼저 드러날 지점이다.

### R9 · `ReminderSounds` 인스턴스 이중 생성 — 하

`ReminderSoundPlayer`(ReminderSoundPlayer.cs:10)와 효과음 가져오기 핸들러(SettingsWindow.Notifications.cs:51)가
같은 `Sounds` 경로로 서로 다른 인스턴스를 만든다. 경로 문자열이 두 곳에 하드코딩돼 있다.

### R10 · `RemoveRoutine` 이중 호출 — 하

`PersonalizationView.deleteRoutine`(PersonalizationWindow.cs:45–47)이 확인 대화상자 **앞에서** 결과를 버리는
`settings().RemoveRoutine(selected.Id);`를 호출한다. "프로필이 참조 중" 예외를 먼저 띄우려는 의도는 타당하나,
검증과 실행을 같은 메서드로 두 번 돌리는 형태라 의도가 코드에 드러나지 않는다.

### R11 · 캐시된 페이지가 `BreakHistory` 인스턴스에 고정된다 — 하(잠복)

`OpenReview`가 `new BreakReviewView(this, runtime.BreakHistory.Review, …)`로 **메서드 그룹**을 캡처한다.
현재 `AppRuntime.BreakHistory`는 생성자에서만 대입되므로 안전하다. 그러나 선언이 `private set`이라
누군가 재로드 기능을 넣는 순간 캐시된 페이지가 낡은 인스턴스를 읽는다.

### R12 · 저장소 잔재 — 하 (코드 위험 아님, 작업자 혼선 위험)

`Sources/`(내용물이 `.DS_Store`뿐), `Unfold.xcodeproj`, `.swiftpm`, `.worktrees`, `.build`가 루트에 남아 있다.
`docs/README.md` 정리 기록은 Swift 소스 제거를 명시하지만 이 껍데기들은 남았다. 삭제는 사용자 승인 사항이다.

---

## 작업 분할과 파일 소유권

`AppRuntime.cs`가 단일 경합 지점이므로 **웨이브 안에서는 이 파일을 한 작업 단위만 소유**하도록 배치했다.
각 단위는 주 소유 파일(쓰기 권한)과 참조 전용 파일을 구분한다.

### Wave 0 — `src/` 무접촉, 완전 병렬

| 단위 | 목적 | 주 소유 파일 | 금지 |
|---|---|---|---|
| **W0-A** 검증 기록 갱신 | 커밋 `7e81f77` 기준 159개 테스트·빌드 경고 0을 새 `validation/` 기록으로 남기고 `docs/verification.md`에서 참조 | `docs/validation/2026-09-16-runtime-audit.md`(신규), `docs/verification.md` | 다른 `docs/*.md`, `src/**` |
| **W0-B** CI 검증 범위 문서화 | macOS 패키지 스모크가 CI에 없음을 `verification.md` "실제 OS 확인"에 명시 | `docs/verification.md` | W0-A와 같은 파일 → **W0-A와 직렬**, 또는 W0-A에 병합 |
| **W0-C** 문서 공백 3건 | `SaveRoutine`의 프로필 해제, 기록 저장 실패 시 동작, `Unfold.MediaSetup`이 솔루션 밖인 점 반영 | `docs/mvp.md`, `docs/personalization.md` | `src/**`, `docs/verification.md` |

> W0-A·W0-B는 같은 파일을 쓴다. 감독자는 **둘을 하나로 묶거나** W0-A → W0-B 직렬로 돌린다.

### Wave 1 — Core 전용, `Desktop` 무접촉, 서로 병렬

| 단위 | 대응 위험 | 주 소유 파일 | 참조 전용 |
|---|---|---|---|
| **W1-A** manifest 계약 강화 | R3, R4 | `src/Unfold.Core/CharacterLibrary.cs` | `CharacterPack.cs`, `CharacterAssetAudit.cs`, `CustomPetDraft.cs` |
| **W1-B** 계약 회귀 테스트 | R3, R4 | `Tests/Unfold.Tests/CharacterAssetAuditTests.cs`, `Tests/Unfold.Tests/CharacterPackTests.cs` | `src/**`(읽기만) |

W1-A 범위 한정: `Animations`를 읽기 전용으로 노출하고 `RenderStyle`을 허용 집합(`null`/`"pixel"`/`"smooth"`)으로 검증한다.
**리비전 체계 2종(`Revision` / `PackRevision`)은 통합하지 않는다** — §계약 참조.
`SmokeDiagnostics.cs:230`이 맵을 변형하므로 W1-A는 그 호출부를 깨뜨린다 → **W2-C가 W1-A에 의존**한다.

### Wave 2 — Desktop 런타임. `AppRuntime.cs`는 W2-A 단독 소유

| 단위 | 대응 위험 | 주 소유 파일 | 참조 전용 |
|---|---|---|---|
| **W2-A** 펫 위치 저장 경합 제거 | R1, R11 | `src/Unfold.Desktop/AppRuntime.cs` | `PetWindow.cs` |
| **W2-B** 설정 화면 오류 정책 통일 | R2, R9 | `src/Unfold.Desktop/SettingsWindow.cs`, `SettingsWindow.Notifications.cs` | `AppRuntime.cs`, `Ui.cs` |
| **W2-C** 진단 코드의 manifest 사용 수정 | W1-A 후속 | `src/Unfold.Desktop/SmokeDiagnostics.cs` | `AppRuntime.cs` |
| **W2-D** 루틴 삭제 검증 명료화 | R10 | `src/Unfold.Desktop/PersonalizationWindow.cs` | `src/Unfold.Core/Personalization.cs` |

W2-A 범위 한정: 위치 저장을 별도 상태로 분리하거나 `UpdateSettings`와 동일한 단일 갱신 경로로 합류시킨다.
`BreakHistory` 선언은 `private set` → `readonly`로 좁힌다(R11 봉인). **`Changed` 시그니처는 바꾸지 않는다** — Wave 3과 충돌한다.

### Wave 3 — 구조 변경. 선행 웨이브 병합 후 단독 실행

| 단위 | 대응 위험 | 주 소유 파일 | 성격 |
|---|---|---|---|
| **W3-A** `AppRuntime` partial 분할 | R6, R7, R8 | `src/Unfold.Desktop/AppRuntime.cs` → `AppRuntime.cs`(수명주기·트레이) + `AppRuntime.Session.cs`(시계·리마인더·기록) + `AppRuntime.Characters.cs`(라이브러리·클립 캐시) | 동작 무변경 리팩터 |
| **W3-B** 운영 호스트 검증 확대 | R5 | `src/Unfold.Desktop/SmokeDiagnostics.cs` | W2-C와 같은 파일 → **W2-C와 직렬** |
| **W3-C** macOS 패키지 스모크 CI | 문서 불일치 2 | `.github/workflows/desktop.yml`, `Scripts/make-macos-bundle.sh` | 외부 영향(CI 실행) → 감독자 승인 필요 |

W3-A는 **행동을 바꾸지 않는 순수 분할**로 한정한다. R7·R8의 실제 최적화는 분할 후 별도 단위로 제안한다.
W3-B는 `PersonalizationView`/`BreakReviewView`를 `SettingsWindow` 안에서 직접 눌러 검증하도록 확장한다
(Window 래퍼 경로는 테스트에서 유지 — 삭제하면 기존 테스트가 깨진다).

### Wave 4 — 승인 대기

| 단위 | 주 소유 | 조건 |
|---|---|---|
| **W4-A** 저장소 잔재 정리(R12) | `Sources/`, `Unfold.xcodeproj`, `.swiftpm` | **사용자 승인 없이 착수 금지.** 되돌리기 어려운 삭제이고 `.gitignore`·번들 스크립트 영향 확인이 선행돼야 한다 |

### 파일 소유권 요약(충돌 방지 표)

| 파일 | 소유 단위 | 웨이브 |
|---|---|---|
| `src/Unfold.Core/CharacterLibrary.cs` | W1-A | 1 |
| `src/Unfold.Desktop/AppRuntime.cs` | W2-A → W3-A | 2 → 3 (직렬) |
| `src/Unfold.Desktop/SettingsWindow*.cs` | W2-B | 2 |
| `src/Unfold.Desktop/SmokeDiagnostics.cs` | W2-C → W3-B | 2 → 3 (직렬) |
| `src/Unfold.Desktop/PersonalizationWindow.cs` | W2-D | 2 |
| `Tests/Unfold.Tests/Character*Tests.cs` | W1-B | 1 |
| `docs/verification.md` | W0-A(+W0-B) | 0 |
| `docs/mvp.md`, `docs/personalization.md` | W0-C | 0 |
| `.github/workflows/desktop.yml` | W3-C | 3 |

같은 웨이브 안에서 위 표의 파일이 겹치는 단위는 없다. `src/Unfold.Core/Personalization.cs`,
`src/Unfold.Desktop/PetWindow.cs`, `Ui.cs`, `DesignSystem.cs`는 이번 제안에서 **어느 단위도 쓰지 않는다**(참조 전용).

---

## 의존성·수용 기준

### 의존 순서

```
W0-A ─▶ W0-B          (같은 파일)
W0-C                  (독립)
W1-A ─┬─▶ W2-C ─▶ W3-B
      └─▶ W1-B        (W1-A 병합 후 기대값 갱신)
W2-A ─▶ W3-A          (같은 파일)
W2-B, W2-D            (독립)
W3-C                  (승인 후 독립)
W4-A                  (승인 후 최종)
```

전체 웨이브 게이트: `dotnet build Unfold.slnx -c Release`가 **경고 0**을 유지해야 한다
(`TreatWarningsAsErrors=true`이므로 경고는 곧 빌드 실패다).

### 단위별 수용 기준

| 단위 | 수용 기준 |
|---|---|
| **W0-A/B** | 새 기록에 커밋 해시·OS·아키텍처·실행 명령·`Passed 159` 원문이 들어간다. macOS 패키지 스모크가 CI에 **없다**는 사실이 `verification.md`에 문장으로 남는다. 통과하지 않은 검사를 통과로 적지 않는다 |
| **W0-C** | 3개 공백(프로필 해제, 기록 저장 실패 동작, MediaSetup이 솔루션 밖)이 각각 담당 문서 1곳에만 기재되고 다른 문서에 복제되지 않는다 |
| **W1-A** | ① `CharacterManifest.Animations`를 외부에서 변형할 수 없다. ② `renderStyle`이 허용 집합 밖이면 `LoadPackage`가 `InvalidDataException`을 던진다. ③ **기존 `Assets/Characters/default-cat`과 `Art/Characters/bori-rabbit/runtime`이 그대로 로드된다**(`--validate-characters` 종료코드 0). ④ `Revision`/`PackRevision` 두 함수의 해시 입력이 변경되지 않는다 — 변경 시 기존 사용자 라이브러리가 "외부에서 바뀜"으로 오판된다 |
| **W1-B** | 가변 manifest 변형 시도와 잘못된 `renderStyle`에 대한 테스트가 각각 추가되고, 기존 159개가 모두 통과한 채 총 개수가 늘어난다 |
| **W2-A** | ① 드래그 저장과 설정 저장이 교차해도 `PetX/PetY`와 나머지 설정이 함께 보존된다(회귀 테스트로 증명). ② `AppRuntime.BreakHistory`가 재대입 불가다. ③ `Changed` 이벤트 시그니처·발생 지점 수가 변하지 않는다. ④ 스모크 진단의 펫 위치 관련 단계가 계속 통과한다 |
| **W2-B** | ① 실행 중 간격 변경 시도의 원문 안내("타이머를 일시정지하거나 중지한 뒤 …")가 화면에 보인다. ② `ArgumentException` 원문 보존 / 그 외 일반 문구라는 정책이 `SettingsWindow`와 `PersonalizationView`에서 동일하다. ③ `Sounds` 디렉터리 경로 리터럴이 한 곳으로 줄어든다 |
| **W2-C** | W1-A 이후에도 `--smoke-test`가 `smoke.json`에 `success: true`를 기록한다(실제 실행 결과 첨부) |
| **W2-D** | ① `RemoveRoutine` 호출이 확인 전 1회·확인 후 1회로 남더라도 그 의도가 코드에서 읽힌다. ② 프로필이 참조 중인 루틴 삭제 시 참조 프로필 이름이 담긴 원문 안내가 확인 대화상자 **앞에** 뜬다. ③ `PersonalizationTests`/`PersonalizationWindowTests`가 통과한다 |
| **W3-A** | ① `git diff`에 순수 이동 외 동작 변경이 없다. ② 공개 API(`Settings`, `Clock`, `Reminder`, `Characters`, `Changed`, `Start/Reload/UpdateSettings/ShowReminder/Quit`) 시그니처가 동일하다. ③ 159개 + 신규 테스트 전부 통과. ④ `--smoke-test` `success: true` |
| **W3-B** | ① `SettingsWindow` 사이드바로 진입한 `PersonalizationView`/`BreakReviewView`에서 저장·삭제·CSV 내보내기 버튼 이벤트가 실행되고 결과가 검사된다. ② Window 래퍼 경로 테스트가 삭제되지 않는다. ③ 캡처 PNG 수 증가가 `smoke.json`에 반영된다 |
| **W3-C** | ① macOS 잡에서 번들 앱으로 `--smoke-test`를 실행하고 `smoke.json.success`를 게이트로 쓴다. ② 실패 시 `verification/`과 `unfold.log`를 아티팩트로 올린다. ③ Windows 잡 동작이 바뀌지 않는다. ④ **실행 전 감독자 승인** |
| **W4-A** | ① 사용자 승인 기록. ② 삭제 후 `dotnet build`·`dotnet test`·`Scripts/make-macos-bundle.sh`가 모두 성공. ③ `docs/README.md` 정리 범위 절에 삭제 항목이 기록된다 |

### 공통 제약(모든 단위)

- 로컬 검증은 **새 `UNFOLD_DATA_DIR`** 로만 한다. 실사용 라이브러리를 지정하지 않는다.
- 변경 범위에 맞는 검사만 고른다. 새 근거 없이 통과한 검사를 반복하지 않는다.
- 자동 검사 결과와 실기 OS 관찰을 보고에서 분리한다.
- 다른 작업자의 변경을 되돌리지 않는다. 표의 참조 전용 파일은 읽기만 한다.

---

## 표본 교차 확인 (UI 이벤트 → 서비스 → 저장 → UI 갱신)

각 흐름을 **생산자와 소비자 양쪽 코드를 모두 읽어** 확인했다. 정적 존재 확인이 아니다.

### S1 · 알림 설정 적용 — 연결됨

| 단계 | 근거 |
|---|---|
| UI 이벤트 | `reminderApply` Click → 소수점·범위 선검증 (SettingsWindow.cs:48–54) |
| 도메인 | `AppSettings.ApplyReminder` → `WorkProfile.Validate` 재사용 + 루틴 존재 확인 + `ActiveProfileId=null` (Personalization.cs:79–84) |
| 서비스 | `AppRuntime.UpdateSettings` → `reschedulesTimer` 판정 → `CanEditTimerInterval` 가드 (AppRuntime.cs:137–141) |
| 저장 | `value.Save(settingsFile)` → `AtomicFile.Write` |
| 시계 | `Clock.SetInterval` + `Clock.ScheduleAfterBreak(now)` (AppRuntime.cs:145) |
| UI 갱신 | `Changed` → `SettingsWindow.Refresh`의 `displayedInterval` 비교로 입력 되돌림 (SettingsWindow.cs:151) + `Tick`의 트레이 툴팁 (AppRuntime.cs:123–126) |

교차 확인 결과: **`IdleMinutes` 변경은 의도적으로 시계를 재예약하지 않는다.** `Tick`이 매초
`TimeSpan.FromMinutes(Settings.IdleMinutes)`를 읽으므로(AppRuntime.cs:118) 즉시 반영된다. 양쪽이 일관된다.

### S2 · 휴식 완료 → 기록 → 화면 3곳 — 연결됨

| 단계 | 근거 |
|---|---|
| UI 이벤트 | `PetSpeechBubble.complete` (PetSpeechBubble.cs:29) → `runtime.CompleteBreak` |
| 도메인 | `PetReminder.Complete` → `session.Tick(now)` → `session.Complete()` → `CompletedSeconds` 확정 → `Session=null` → `Notice=Completed(15초)` → `Finished` 발행 (PetReminder.cs:37–45) |
| 서비스 | `AppRuntime.FinishBreak`(생성자에서 구독, AppRuntime.cs:175) → `Clock.ScheduleAfterBreak` → `BreakHistory.Add` |
| 저장 | `BreakHistory.Save(historyFile)`, `historyWritable` 가드 + 실패 시 `BreakHistoryError` 설정 (AppRuntime.cs:275–281) |
| UI 갱신 ① | 말풍선: `RefreshPetNotice` → `bubble.Refresh` → "스트레칭을 마쳤어요!" + `CompletedSeconds` 표시 (PetSpeechBubble.cs:48,55) |
| UI 갱신 ② | 대시보드: `Changed` → `Refresh` → `BreakHistory.ForDay` → `today`/`todayCount`/`historyStatus` (SettingsWindow.cs:125–132) |
| UI 갱신 ③ | 주간 회고: `OpenReview`가 진입마다 `reviewPage.Refresh()` → `read(endDay)` 재조회 (SettingsWindow.Layout.cs:168–169) |
| 펫 반응 | `ReactToBreak(session,"celebrate")` — Mochi에는 `celebrate`가 없어 `React`가 조기 반환(PetWindow.cs:93–94). `docs/mvp.md`의 "Missing event clips leave the pet unchanged"와 일치 |

경계 3개 모두에서 소비자가 실제로 재조회한다. **누락된 갱신 경로는 없다.**

### S3 · 펫 팩 설치 → 캐릭터 선택 — 연결됨

| 단계 | 근거 |
|---|---|
| UI 이벤트 | `InstallPetPack` → `PetPackView.InstallPack` (PetPackWindow.cs:166) |
| 서비스 | `library.Install(pack, target.Revision)` — 파일 락 → `Recover()` → 리비전 재확인 → `CopyTo` → **재확인** → `Directory.Move` 백업 롤백 (CharacterPack.cs:212–234) |
| 저장 | `Characters/<id>/` 교체 + `pack.json` 동봉 |
| 서비스 갱신 | `installed(result)` = `AppRuntime.SelectInstalledCharacter` → `Reload()`(목록 재작성 + `clips.Clear()` + `Changed`) → `UpdateSettings(SelectedCharacterId)` (AppRuntime.cs:107–110) |
| UI 갱신 ① | 설정: `characters.ItemsSource` 재바인딩(`ReferenceEquals` 비교) + `SelectedItem = runtime.Selected` (SettingsWindow.cs:161–162) |
| UI 갱신 ② | 펫: `UpdatePet` → `PetWindow.SetCharacter` → 새 인스턴스라 참조 비교 불일치 → idle 재로드 (PetWindow.cs:79–83) |
| UI 갱신 ③ | 미리보기: `previewCharacter != selected` → 재디코딩 (SettingsWindow.cs:163–166) |

교차 확인 결과: 설치 후 화면 3곳이 모두 새 인스턴스를 집는다. 다만 **`Reload()` 한 번에 idle 클립이
펫·미리보기 양쪽에서 재디코딩**된다(R7). 기능상 정확하고 비용만 낭비된다.

### S4 · 펫 드래그 위치 — 연결됨, 동시성 결함

S1~S3과 달리 여기서만 결함이 나왔다. §R1 참조.
좌표 계산이 `DesktopScaling`을 쓴다는 점(PetWindow.cs:22, 71, 130–131, PetSpeechBubble.cs:87)은
Retina macOS에서 확인된 기존 수정이며 이번 감사에서도 유지되고 있다. **`RenderScaling`으로 되돌리지 않는다.**

### S5 · 효과음 가져오기 — 연결됨

`ImportDueSound` → `ReminderSounds.Import`(SHA-256 파일명, PCM WAV 검증) → `ReminderSoundId` 저장
→ `AppSettings.Validate`의 64자리 hex 검사 통과 → 재생 시 `ReminderSounds.Resolve`가 **재검증 후** 실패하면 기본음으로 폴백
(ReminderSounds.cs:21–34). 생산자·소비자 검증 규칙이 동일하다. 다만 카드 라벨은 생성자에서 1회 구성된
지역 클로저(`RefreshLabel`)로만 갱신되고 `SettingsWindow.Refresh`와 연결돼 있지 않다 — 현재 다른 변경 경로가
없어 드러나지 않는 잠복 항목이다.

---

## 미검증 항목

이번 감사에서 **하지 않은** 것을 명시한다. 아래를 통과·정상으로 보고하면 안 된다.

1. **`--smoke-test` 패키지 진단을 실행하지 않았다.** `SmokeDiagnostics`는 정적으로만 읽었다.
   `smoke.json`의 `success` 여부, PNG 42장 생성, 실제 dispatcher 타이밍은 미확인이다.
2. **`--review-pet-pack` 펫 팩 재생 진단을 실행하지 않았다.**
3. **실기 OS 동작 전부 미확인**: 트레이/메뉴바, 투명 배경, 드래그, 다중 모니터·DPI 변경,
   Windows 클릭 통과, 로그인 시 자동 실행, sleep/wake, 실제 효과음 출력.
4. **Windows에서 아무것도 실행하지 않았다.** 이 감사는 macOS(Darwin 25.6.0) 단일 환경이다.
   `PlatformServices`의 `GetLastInputInfo`, 레지스트리 Run 키, `winmm.dll` 재생은 코드만 읽었다.
5. **MP4 임포트 경로 미검증.** `.tools/media-lgpl/`에 ffmpeg가 존재하는 것만 확인했고
   `PetMediaImporter.Import`의 실제 변환은 돌리지 않았다. 159개 통과에 MP4 테스트가 포함됐는지
   (`verification.md`는 도구 없으면 건너뛴다고 명시) 개별 확인하지 않았다.
6. **성능·메모리·장시간 안정성 미측정.** R8의 1 Hz 비용은 코드 기반 추정이다.
7. **R1(위치 경합)을 재현하지 않았다.** 코드 경로 분석으로 도출한 결함이며, 실제 발생 빈도는 미측정이다.
   W2-A는 수정 전 재현 테스트부터 요구한다.
8. **제품 가치·BM 판단은 이 보고서 범위가 아니다.** 우선순위는 기술 위험 기준이며
   제품 우선순위는 제품 담당 보고서와 감독자가 결정한다.
9. **`Art/Characters/*/resource.json`의 권리·제작자 공란은 결함으로 보지 않았다.**
   원장이 `rightsStatus: needs-documentation`으로 미확정을 명시한 상태이며,
   확인되지 않은 제작자·라이선스를 임의로 채우는 작업은 제안하지 않는다.
