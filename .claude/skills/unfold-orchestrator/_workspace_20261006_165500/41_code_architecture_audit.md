# 코드·아키텍처 전수 감사 (release/mvp, 작업 트리)

날짜: 2026-09-20 · 대상: `/Users/hwanghyeonseong/Documents/GitHub/Unfold` (브랜치 `release/mvp`, 미커밋 변경 포함)
방법: 정적 분석만 사용. `dotnet build`/`test`/`run`을 실행하지 않았고 제품 코드·문서를 수정하지 않았다.

**갱신(2차 전수 감사)**: 1차는 미커밋 diff 중심이었다. "## 2차 전수 감사" 절에서 리소스 누수·이벤트 구독·UI 스레드·Core/Desktop 경계·저장 계약을 diff와 무관하게 지정 파일 전체를 읽어 이어서 감사했다. 이번 라운드의 핵심 발견은 **ARC-10(P1)** — 설정 로드 시 Theme 필드만 개별 자체 복구되고 나머지 필드(BubbleDirection 등)는 손상 시 설정 전체가 초기화된다.

## 요약 (핵심 발견 5줄 이내)

1. **PetWindow 위치 계산은 현재 `DesktopScaling`을 사용한다** (`PetWindow.cs:22,73,117,119,129,135`) — 메모리에 기록된 Retina Mac Dock 버그는 이 체크아웃에서 재발하지 않았다. 회귀 없음.
2. 미커밋 변경은 말풍선 접기/배지(`BubbleCollapsed`, `ReminderBadge`) 기능을 완전히 제거하고, 펫 크기(50~150%)·디버그 알림 미리보기·색상 테마 4종을 새로 추가했다. 제거된 API의 죽은 참조는 코드베이스 어디에도 남아 있지 않다(grep 확인).
3. `DesignSystem`이 정적 불변 브러시에서 **정적 가변 `SolidColorBrush` 싱글톤 + 런타임 테마 전환(`ApplyTheme`)**으로 바뀌었다. 프로세스 전역에서 공유되는 가변 상태이며, 이를 색상 값으로 단정하는 테스트는 모두 "Timer settings" 컬렉션(직렬 실행)에 들어 있어 현재는 안전하지만, 새 테스트가 이 컬렉션 밖에서 색상을 단정하면 경합이 생긴다(ARC-4).
4. `StretchClock.Stop()`이 `Remaining`을 `TimeSpan.Zero`에서 `Interval`로 바꿔 돌려주도록 의도적으로 변경되었고, 테스트·UI 텍스트가 함께 갱신되어 있다 — 결함이 아니라 승인된 동작 변경으로 판단.
5. `BreakReviewWindow`의 일별 상세 항목(`DetailRow`)에서 루틴 이름 표시가 제거되었다(완료 시각·실제 시간만 남음). 데이터 손실은 아니지만(히스토리·CSV에는 루틴 이름이 남아 있을 가능성이 높음, 코드 미변경) UI에서 루틴 식별이 불가능해진 점은 제품 승인 여부 확인이 필요하다.

## 발견 표

| ID | 심각도 | 파일:줄 | 증상 | 재현·실패 시나리오 | 근거 | 권장 조치 |
|---|---|---|---|---|---|---|
| ARC-1 | P2 | `src/Unfold.Desktop/DesignSystem.Themes.cs:29-46` | `DesignSystem`의 모든 색상 토큰이 `static readonly SolidColorBrush` **싱글톤**이며 `ApplyTheme`이 `.Color`를 그 자리에서 변경한다. 앱 전체·테스트 프로세스 전체가 하나의 전역 가변 상태를 공유한다. | 새로 추가되는 테스트가 `DesignSystem.Cream`/`.Success` 등의 `.Color`(또는 참조가 아닌 실제 렌더 결과)를 다른 xunit 컬렉션에서 단정하면, `ThemeTests`(같은 프로세스 내 "Timer settings" 컬렉션)가 동시에 팔레트를 바꾸는 도중 값이 흔들릴 수 있다. 현재 색상을 단정하는 모든 테스트 파일(`ThemeTests`, `DesignSystemTests`, `TimerControlTests`, `BreakReminderTests`, `PetTabsUiTests`, `SettingsDashboardTests`, `SettingsPreferencesTests`, `UiAuditRegressionTests`)은 `[Collection("Timer settings")]`로 묶여 있어 지금은 직렬화되지만, 이 규율은 컴파일러가 강제하지 않는다. | `git diff -- src/Unfold.Desktop/DesignSystem.Themes.cs`; `grep -rn "\[Collection(\"Timer settings\")\]" Tests/Unfold.Tests/*.cs` (11개 파일 중 색상 단정 없는 파일도 포함); `TimerControlTests.cs:16` `[CollectionDefinition("Timer settings", DisableParallelization = true)]` | 새 테스트 작성 규칙에 "DesignSystem 브러시 색상을 단정하는 테스트는 반드시 `[Collection(\"Timer settings\")]`" 문구를 명시하거나, `DesignSystem`에 테스트 전용 스코프/리셋 훅을 두는 것을 고려. |
| ARC-2 | P2 | `src/Unfold.Desktop/BreakReviewWindow.cs:222-233`(신) vs 구버전 `DetailRow` | 회고 화면의 날짜별 상세 행에서 `entry.RoutineName ?? entry.RoutineId` 표시가 완전히 삭제됨. 완료 시각과 실제 휴식 시간만 남는다. | 여러 루틴(예: "글쓰기 휴식", "눈 쉬어 주기")을 섞어 완료한 뒤 기록 탭을 펼치면 어떤 루틴이었는지 화면에서 구분할 수 없다. CSV 내보내기 경로(`BreakReview.Csv()`, 미변경)는 여전히 루틴 이름을 포함할 가능성이 높지만 본 감사에서 `BreakReview.cs`는 diff 대상이 아니라 직접 확인하지 않았다. | `git diff -- src/Unfold.Desktop/BreakReviewWindow.cs` (구 `DetailRow`는 `routine` TextBlock을 만들었으나 신버전에는 없음) | 제품 담당에게 "루틴 이름 제거가 승인된 UX 결정인지" 확인 필요(`docs/plans/2026-09-20-review-ui-proposal.md`, `docs/validation/2026-09-20-review-ui-implementation.md`가 미커밋 상태로 존재 — 감사자는 열람했으나 "승인" 여부는 사용자 확인 필요). |
| ARC-3 | P2 | `src/Unfold.Desktop/CustomPetWindow.cs:94,145` / `src/Unfold.Desktop/PetPackWindow.cs:148` | `previewHint` TextBlock이 두 파일 모두에서 `IsVisible = false`로 고정되고 텍스트도 빈 문자열로 남아, 사실상 죽은 컨트롤이 되었다(9/20 UI 카피 정리의 부산물로 보임). | 기능적 버그는 아님 — 항상 숨겨진 빈 텍스트 블록이라 화면에 영향 없음. 다만 코드에는 `previewHint.Foreground = ... ? Brushes.DarkSlateGray : DesignSystem.Muted;`처럼 도달 불가능한 분기가 남아 유지보수 시 혼동을 유발한다. | `git diff -- src/Unfold.Desktop/CustomPetWindow.cs src/Unfold.Desktop/PetPackWindow.cs` | 다음 정리 작업에서 `previewHint` 필드와 관련 조건부 로직을 완전히 제거하는 것을 권장(기능 영향 없음, 저위험 정리). |
| ARC-4 | P2 | `src/Unfold.Desktop/AppRuntime.cs:283-306` | 디버그 미리보기(`ShowReminderPreview`/`CloseReminderPreview`/`PresentedReminder`)는 실제 타이머·기록·사운드 상태를 건드리지 않도록 설계되었고 `SmokeDiagnostics.cs:192-208`에서 이를 E2E로 검증하지만, 이 검증은 `dotnet test`가 아니라 `--smoke-test` 인자로만 실행되는 수동 진단 경로(`App.cs:25`)다. `Tests/Unfold.Tests/BreakReminderTests.cs`(129-149줄대)와 `SettingsDashboardTests.cs`(360,368줄)는 `dotnet test`로 자동 실행되며 기본 시나리오(가드 예외, `PreviewNotice` 전이)를 커버한다. | `Tick()`이 `Clock.AdvanceWarningDue`일 때만 `ClearReminderPreview()`를 호출하고, 실제 초대(`Invite`)가 발생하는 경로는 `ShowReminder()` 시작부의 `ClearReminderPreview()`에 의존한다. 두 진입점 외에 `Reminder`가 직접 `Notice`를 바꾸는 다른 경로가 생기면(향후 리팩터링) 미리보기가 실제 알림과 동시에 표시될 위험이 있다 — 현재 코드에는 그런 제3의 경로가 없어 실결함은 아니다. | `src/Unfold.Desktop/AppRuntime.cs:141-148,255-258,284-303` | `PresentedReminder`/`reminderPreview` 무효화가 `Reminder.Notice`가 바뀌는 모든 지점(현재는 2곳)에 결합되어 있다는 불변식을 코드 주석으로 명시하거나, `PetReminder`에 `Changed` 이벤트를 두어 `AppRuntime`이 구독하는 방향으로 리팩터링하면 향후 회귀를 막을 수 있다. |
| ARC-5 | 정보(결함 아님) | `src/Unfold.Core/StretchClock.cs:27,32` | `Stop()`과 `ScheduleAfterBreak()`가 정지 시 `Remaining`을 `TimeSpan.Zero` 대신 `Interval`(설정된 스트레칭 간격)로 채운다. | `TimerControlTests.cs`(수정됨), `SmokeDiagnostics.cs:53-64`가 새 값을 단정하도록 함께 갱신되어 있어 의도된 변경으로 판단. 이전 동작("정지=00:00")과 다르므로 사용자 대면 문구가 바뀐 것을 기록만 해 둔다. | `git diff -- src/Unfold.Core/StretchClock.cs Tests/Unfold.Tests/TimerControlTests.cs` | 조치 불필요. `docs/plans/2026-09-20-pet-scale-timer-debug-proposal.md`(미커밋)가 이 변경의 근거로 보이나 사용자 승인 문서인지는 확인하지 않음. |
| ARC-6 | 정보(결함 아님) | `src/Unfold.Desktop/PetWindow.cs:22,73,117,119,129,135` | `PetAnchor`, 초기 위치, 레이아웃 재계산, 클램핑 모두 `DesktopScaling`을 사용한다. | 메모리(`petwindow-macos-scaling.md`)에 기록된 "RenderScaling을 쓰면 Retina Mac Dock 근처에서 튀는 버그"는 이 체크아웃에서 해당하지 않는다. `RenderScaling` 문자열은 `PetWindow.cs`에 전혀 등장하지 않는다(grep 확인). | `grep -n "RenderScaling\|DesktopScaling" src/Unfold.Desktop/PetWindow.cs` | 조치 불필요. 향후 이 파일을 건드리는 워커에게 "DesktopScaling 유지" 규칙을 명시적으로 전달할 것. |

## 미커밋 변경이 새로 만든 위험 (요약)

작업 트리의 26개 소스 파일 변경은 크게 세 갈래다.

1. **말풍선 접기/배지 기능 제거** (`AppRuntime.cs`, `PetWindow.cs`, `AppSettings.cs`, `PetSpeechBubble.cs`) — `BubbleCollapsed`, `ReminderBadge`, `TrayReminderStatus.CanExpand`, `AppRuntime.ToggleBubble/ExpandReminder`가 모두 제거되었다. 죽은 참조는 남아 있지 않음(전수 grep 확인, ARC-6 절 참조). 구 설정 파일의 `"bubbleCollapsed"` 필드는 `System.Text.Json`이 알 수 없는 속성으로 무시하므로 역직렬화가 깨지지 않으며, `PetReminderTests.cs`가 이를 직접 테스트한다.
2. **펫 크기 조절(50~150%) 추가** (`AppSettings.PetScalePercent`, `PetWindow.RefreshSpeech`, `PetSpeechBubble.PetBubbleLayout.Create`, `SettingsWindow.cs`의 슬라이더) — 검증 범위(50~150, 10 단위)가 저장 계층(`AppSettings.Validate`)과 UI(`Slider.TickFrequency=10, IsSnapToTickEnabled=true`) 양쪽에 있어 일관적이다. `PetBubbleLayout.Create`는 `petSize<=0`이거나 비유한값이면 `ArgumentOutOfRangeException`을 던지는 방어 코드를 추가했다(정상 경로에서는 도달하지 않음, 방어적 프로그래밍으로 적절).
3. **색상 테마 4종 추가** (`DesignSystem.Themes.cs` 신규, `AppSettings.Theme`, `AppRuntime.SetTheme`) — ARC-1 참조. 저장 계층은 `Enum.IsDefined` 가드로 알 수 없는 테마 값을 `OatLatte`로 자연스럽게 복구하며(`AppSettings.cs:33`), `ThemeTests.MissingLegacyAndUnknownThemesUseOatWithoutLosingSettings`가 이를 직접 검증한다.

세 갈래 모두 관련 자동 테스트가 함께 갱신되어 있고(정적으로 읽은 한도 내에서) 논리적으로 일관적이다. 실행해서 통과를 확인하지는 못했다(아래 미검증 항목 참조).

## 확인된 사실 vs 추론

**확인된 사실 (파일을 직접 읽고 grep으로 교차 확인):**
- `PetWindow.cs`는 6곳 모두 `DesktopScaling`을 사용하며 `RenderScaling`은 등장하지 않는다.
- `ReminderBadge`, `PetToggleSpeech`, `BadgeState`, `ExpandReminder`, `CanExpandReminder` 문자열은 `src/`, `Tests/`(obj/ 제외) 어디에도 남아 있지 않다.
- `Unfold.Core.csproj`는 `Unfold.Desktop`을 참조하지 않고, `Unfold.Desktop.csproj`만 `Unfold.Core`를 참조한다 — Core→Desktop 의존 역전은 없다.
- `AppSettings.Validate`는 `Theme`·`PetScalePercent`·기존 필드 모두에 대해 저장 시점 검증을 수행하며, `Load`는 알 수 없는 테마를 예외 대신 기본값으로 복구한다.
- `StretchClock.SetInterval`은 `Stopped`일 때 `Remaining`을 갱신하지 않지만, `AppRuntime.UpdateSettings`가 `SetInterval` 직후 항상 `ScheduleAfterBreak`를 호출하므로 `Remaining`이 새 간격으로 재계산된다 — 두 메서드가 분리되어 있다는 사실 자체는 향후 리팩터링 시 순서를 바꾸면 회귀할 수 있는 암묵적 결합(정보용으로만 기록, 현재는 결함 아님).

**추론 (정적 분석의 한계, 빌드·실행으로 확인 못함):**
- `AppRuntime.ShowReminderPreview`가 만드는 `new BreakSession(routine, characterId, durationSeconds: ...)` 호출이 실제 `BreakSession` 생성자 시그니처와 일치해 컴파일되는지는 소스만으로는 100% 보장할 수 없다(명명 인자를 사용하므로 위치 인자 순서 문제는 없을 것으로 보이나, 컴파일러 확인 없이는 단정 불가).
- `BreakReview.Csv()`가 루틴 이름을 여전히 포함하는지(ARC-2의 완화 근거)는 `BreakReview.cs`가 diff 대상이 아니라 열어보지 않았다 — 코드가 바뀌지 않았으므로 유지될 가능성이 높다는 추론일 뿐 직접 확인은 아니다.
- 디버그 미리보기와 실제 타이머 틱 사이의 경합(ARC-4)은 현재 두 진입점만 존재한다는 코드 읽기에 근거한 추론이며, 런타임 동시성 문제(예: 여러 UI 스레드 이벤트가 겹치는 타이밍)를 실기로 재현해 확인하지는 않았다.

## 2차 전수 감사

1차 감사는 미커밋 diff에 집중해 "diff 범위 밖"으로 넘긴 항목이 많았다. 이번에는 지정된 8개 핵심 파일과 저장 계층 6개 파일을 **전체 diff 여부와 무관하게 처음부터 끝까지 읽고**, 이벤트 구독·타이머·리소스 해제 지점을 전수 대조했다. 여전히 `dotnet build`/`test`/`run`은 실행하지 않았다.

### 1. 리소스 누수 — 전수 대조표

읽은 파일: `AnimationView.cs`, `PetMediaImporter.cs`, `ImageCodec.cs`, `PiskelCodec.cs`, `PixelCanvas.cs`, `ReminderSoundPlayer.cs`, `CharacterLibrary.cs`, `CharacterPack.cs` 전체, 그리고 이 파일들이 만드는 `IDisposable`을 실제로 소유하는 상위 창(`PetWindow`, `SettingsWindow`, `CustomPetWindow`, `PetPackWindow`, `EditorWindow`, `PetManagementView`, `PetPackDiagnostics`)의 생성·해제 지점.

| 자원 | 생성 위치 | 해제 위치 | 판정 |
|---|---|---|---|
| `AnimationView.bitmaps[]` (Bitmap) | `AnimationView.SetFrames` (`AnimationView.cs:42`) | 같은 메서드 시작부에서 이전 배열 전부 `Dispose()`(`:41`), 그리고 `Dispose()`(`:91`)에서 전부 해제 | 정상 |
| `AnimationView` 자체(4곳 소유: `PetWindow.animation`, `SettingsWindow.preview`, `CustomPetView.preview`, `PetPackView.preview`) | 각 소유자 생성자에서 필드 초기화 | `PetWindow.cs:89`(Closed), `SettingsWindow.cs:133`(Dispose), `CustomPetWindow.cs:219`(Dispose), `PetPackWindow.cs:191`(Dispose) | 정상, 4곳 모두 짝이 맞음 |
| `PixelCanvas.bitmap/onion` (WriteableBitmap) | `Upload()`(`PixelCanvas.cs:40-46`, 교체 시 이전 것 `image?.Dispose()`) | `PixelCanvas.Dispose()`(`:78`) + `EditorWindow.cs:111,198`에서 `Canvas.Dispose()` 호출(Closed·Replace 양쪽) | 정상 |
| `EditorWindow.thumbnails[].Bitmap` | `Refresh()`(`EditorWindow.cs:168,177`, 교체 시 이전 것 `item.Bitmap.Dispose()`) | `ClearThumbnails()`(`:148`)가 `Closed`(`:111`)와 `Replace()`(`:198`)에서 호출됨 | 정상 |
| `Process`(ffmpeg, `PetMediaImporter.Import`) | `Process.Start(start)`(`PetMediaImporter.cs:42`) | `using var process`로 스코프 종료 시 자동 Dispose, 타임아웃 시 `Kill(true)` 후 재대기(`:44-53`) | 정상 |
| `Process`(afplay, `ReminderSoundPlayer`) | `Play()`(`:30`) / `Preview()`(`:67`) | `Stop()`(`:39-41`)에서 `Kill()` + `Dispose()` | **부분 취약 — ARC-7 참조** |
| `ZipArchive`/`MemoryStream`(`CharacterPack.Open`, `.Create`) | `CharacterPack.cs:31,81,82` | `using` 블록으로 스코프 종료 시 자동 해제(ZipArchive 해제 시 내부 MemoryStream도 함께 해제) | 정상 |
| `FileStream`(`AtomicFile.Write`, `CharacterPack.Create`의 `outputTemporary`) | `AtomicFile.cs:193`, `CharacterPack.cs:93` | 둘 다 `using`으로 감싸져 있고, 임시 파일은 `finally`에서 `File.Delete` | 정상 |
| `CharacterPack` 임시 디렉터리(`temporaryRoot`) | `CharacterPack.Open`(`:50`) | `Dispose()`(`:167-172`)에서 `Directory.Delete(true)`; `PetPackView`는 `DisposePack()`이 `pack`을 교체 전에 먼저 호출(`PetPackWindow.cs:206`), `ReadPack`의 `finally`가 미전환 `candidate`를 정리(`:223`) | 정상 |
| `FileStream`(`CharacterLibrary.Lock()`, `.library.lock`) | `CharacterLibrary.cs:169` | 모든 호출부가 `using var lease = Lock();` 패턴(`List`, `OpenForEditing`, `Save`, `Delete`, `Install`, `InspectInstall`) | 정상 |
| `IncrementalHash`(`CharacterLibrary.PackRevision`) | `:202` | `using var hash = ...` | 정상 |

**결론:** 지정된 8개 파일과 그 소유 창들의 정적 추적 범위 안에서는 **짝이 맞지 않는 Dispose/using을 찾지 못했다.** 이는 "누수가 전혀 없다"는 런타임 증명이 아니라 "코드가 읽은 범위 내에서 방어적으로 작성되어 있다"는 정적 사실이다.

- **ARC-7 (P2, 확정 결함 아님 — 취약한 불변식)**: `ReminderSoundPlayer.cs:35-74`. `Preview()`는 `process` 필드를 인스턴스 공유 상태로 쓰고, 이전 호출의 `Process`를 다음 호출의 `Stop()`(`:39-41`)이 정리하는 구조다. 이 정리가 안전한 이유는 각 `Preview()` 호출이 **첫 `await` 이전까지 완전히 동기적으로 실행되어** `process = playback` 대입이 다음 호출의 `Stop()`보다 항상 먼저 끝난다는 암묵적 가정 때문이다(코드에 이 가정이 주석으로 남아 있지 않음). 이 메서드에 향후 `Stop()` 이전 구간에 `await`가 추가되면(예: 사운드 파일 크기 확인을 비동기화) 두 `Preview()` 호출이 실제로 인터리빙되어 `process` 필드 경쟁이 발생하고 `Process` 핸들이 새지거나 중복 Kill 대상이 될 수 있다. 현재 코드에서는 재현되지 않는다. `Tests/Unfold.Tests/`에는 `ReminderSoundPlayer`를 직접 대상으로 하는 단위 테스트가 없다(grep 결과 없음) — 동시 `Preview()` 호출 시나리오는 테스트 공백이다.

### 2. 이벤트 구독·수명주기 — 전수 대조

`+=`로 구독하는 모든 지점을 `src/Unfold.Desktop/*.cs`에서 grep하고 대응하는 `-=` 또는 소유자 Dispose/Closed를 대조했다. 핵심 사실은 **이 앱이 창을 반복해서 열고 닫는 구조가 아니라는 점**이다.

- `desktop.MainWindow = settingsWindow`(`AppRuntime.cs:108`)가 **앱 실행 중 단 한 번** 생성되고, 이후 '설정 창을 연다'는 사용자 동작은 전부 `Show()`/`Hide()`/탭 전환일 뿐 재생성이 아니다(`AppRuntime.ShowSettings`, `SettingsWindow.HideToTray`).
- `SettingsWindow.Layout.cs:208,214`의 `reviewPage ??= new BreakReviewView(...)`, `petPage ??= new PetManagementView(...)`는 **지연 생성 후 캐시**되며, 탭을 반복해서 오가도 재생성되지 않는다. 따라서 `PetManagementView` 생성자(`PetManagementView.cs:37-38`)가 붙이는 `packs.BusyChanged += UpdateBusy`, `builder.BusyChanged += UpdateBusy`와 `CustomPetView`/`PetPackView`의 `owner.PropertyChanged += OwnerPropertyChanged`(`CustomPetWindow.cs:188`, `PetPackWindow.cs:131`)는 앱 생애주기당 정확히 1회만 걸린다 — "펫 팩 창을 여러 번 열고 닫으면 구독이 누적된다"는 우려는 **현재 프로덕션 경로에서는 성립하지 않는다**(이는 QA/제품 담당이 검증할 사용자 시나리오 자체가 잘못 설정됐을 수 있다는 뜻이므로 팀에 공유할 가치가 있다).
- `BreakReviewWindow`(독립 `Window` 클래스, `BreakReviewWindow.cs`)는 프로덕션 코드(`AppRuntime.cs`, `SettingsWindow*.cs`) 어디에서도 `new BreakReviewWindow(...)`로 생성되지 않는다(grep 확인). `SmokeDiagnostics.cs:163`와 `Tests/`에서만 사용된다. 즉 "회고 창을 반복해서 열고 닫는" 실제 사용자 경로는 존재하지 않고, 실제로는 `BreakReviewView`가 `SettingsWindow` 안의 탭 콘텐츠로만 존재한다.
- 예외: `EditorWindow`(픽셀 에디터)는 `AppRuntime.OpenEditor`(`:242`)에서 **매번 새로 생성**된다. 이 경우 `Session.Changed += Refresh`(`EditorWindow.cs:104`)는 `Closed`(`:111`)와 `Replace()`(`:198`) 양쪽에서 `-=`로 해제되고, `openedEditor.Closed += (_, _) => { if (editor == openedEditor) editor = null; }`(`AppRuntime.cs:245`)로 참조도 정리된다. `previewTimer`(DispatcherTimer)도 `Closed`에서 `Stop()`된다 — 반복 열기/닫기를 지원하는 유일한 창이며, 대조 결과 누락이 없다.
- `AppRuntime.timer`(1초 틱, `:51`)와 `PetWindow.hitTimer`(40ms 틱, `:41`)는 각각 `AppRuntime.Dispose()`(`:394`, `timer.Stop()`)와 `PetWindow`의 `Closed`/`HidePet`/`ClosePet`(`:89,118-120`) 세 지점 모두에서 `Stop()`된다 — 대조 결과 누락 없음.
- **ARC-8 (정보, 결함 아님)**: 위 사실을 근거로, "설정 창·펫 팩 창·커스텀 펫 창·회고 창을 여러 번 열고 닫을 때 구독·타이머가 누적된다"는 가설은 **이 코드베이스의 실제 아키텍처(단일 상주 창 + 탭 전환)에서는 적용되지 않는다.** 반복 생성·해제가 실제로 일어나는 유일한 창은 `EditorWindow`이며, 그 경로는 대조 결과 정상이다. 이 사실은 향후 QA가 "창을 반복 열고 닫아 누수를 확인"하는 수동 테스트를 설계할 때, 실제로 반복 생성되는 것은 EditorWindow뿐이라는 점을 알아야 시간을 낭비하지 않는다는 의미에서 기록해 둔다.

### 3. UI 스레드 접근 / async void

- `async void`는 코드베이스 전체에 정확히 2곳: `AppRuntime.SavedCharacter`(`AppRuntime.cs:251-254`)와 `SettingsWindow.Refresh`(`SettingsWindow.cs:175-247`). 둘 다 메서드 전체가 try/catch(+finally)로 감싸여 있어 처리되지 않은 예외가 SynchronizationContext로 전파되지 않는다. 확인 완료, 결함 없음.
- `App.cs:22`의 `Dispatcher.UIThread.Post(async () => {...})`도 사실상 async void이지만, 분기하는 세 진입점(`PetPackDiagnostics.Run`, `SmokeDiagnostics.Run`, `AppRuntime.Start`) 모두 자체 최상위 try/catch를 가지고 있어(`PetPackDiagnostics.cs:24,99`, `SmokeDiagnostics.cs` 앞서 확인, `AppRuntime.cs:111,123`) 예외가 이 지점까지 전파되지 않는다. 확인 완료, 결함 없음.
- `Task.Run(...)`으로 백그라운드로 넘기는 모든 지점(`AppRuntime.cs`, `CustomPetWindow.cs`, `PetPackWindow.cs`, `EditorWindow.cs`, `BreakReviewWindow.cs`, `PetMediaImporter.cs` 등 grep으로 22곳 확인)은 전부 `await`로 돌아와 이후 UI 컨트롤을 건드린다. Avalonia의 `Dispatcher` SynchronizationContext가 `await` 이후 연속 실행을 UI 스레드로 되돌리는 표준 패턴이며, `Dispatcher.UIThread.Post`를 명시적으로 써야 하는 경우(예: 다른 스레드에서 시작된 콜백)는 `SettingsWindow.Layout.cs:253`, `SingleInstance.cs:30` 두 곳뿐이고 둘 다 실제로 `Dispatcher.UIThread.Post`를 쓰고 있다. **백그라운드 스레드에서 Dispatcher 없이 UI 속성을 직접 건드리는 지점을 찾지 못했다.**

### 4. Core↔Desktop 경계

- `grep -rn "using Avalonia\|Unfold.Desktop" src/Unfold.Core/*.cs` — 결과 없음. `Unfold.Core.csproj`에는 `ProjectReference`가 전혀 없다(Avalonia 패키지 의존 없음). `Unfold.Desktop.csproj`만 `../Unfold.Core/Unfold.Core.csproj`를 참조한다. **경계 위반 없음, 확정.**
- UI 코드에 섞인 비즈니스 로직: `SettingsWindow*.cs`, `PetWindow.cs`, `BreakReviewWindow.cs`를 읽은 결과, 저장·검증 로직(예: `AppSettings.Validate`, `BreakReview.Csv`)은 모두 Core에 있고 Desktop 쪽은 그 결과를 표시하거나 `runtime.UpdateSettings(...)`를 호출하는 얇은 어댑터로 일관되어 있다. 명백한 로직 유출은 찾지 못했다.
- **ARC-9 (P2, 경미한 중복)**: 동일 로직이 두 곳 이상에 반복 구현되어 있다.
  - 남은 시간 MM:SS 포맷: `AppRuntime.cs:151` `$"{(int)Clock.Remaining.TotalMinutes:00}:{Clock.Remaining.Seconds:00}"`와 `SettingsWindow.cs:181`의 동일 문자열이 각각 독립적으로 작성되어 있다. 포맷이 바뀌면 두 곳을 함께 고쳐야 하며, 하나만 고치면 트레이 툴팁과 설정 화면 숫자가 어긋날 수 있다.
  - 펫 크기 계산식 `DesignSystem.PetBaseSize * percent / 100d`가 프로덕션 코드에 3곳(`PetWindow.cs:126`, `SettingsWindow.cs:143`, `SmokeDiagnostics.cs:207`), 테스트에 4곳 반복된다. 단순 산술식이라 드리프트 위험은 낮지만, 헬퍼(예: `DesignSystem.PetSize(int percent)`)로 추출하면 7곳을 1곳으로 줄일 수 있다.
  - 둘 다 런타임 결함이 아니라 유지보수 리스크로만 기록한다.

### 5. 저장·마이그레이션 계약 (전수)

- **원자적 쓰기 확인(사실)**: `AppSettings.Save`(`AppSettings.cs:58`), `BreakHistory.Save`(`BreakHistory.cs:50-51`), `CharacterLibrary`/`CharacterPack`의 모든 파일 쓰기, `CustomPetDraft.Export`(`CustomPetDraft.cs:69,74,78`)가 전부 `AtomicFile.Write`(`CharacterLibrary.cs:187-198`)를 거친다. 이 함수는 임시 파일에 `FileOptions.WriteThrough`로 쓰고 `Flush(true)`한 뒤 `File.Move(temp, path, true)`로 교체하며, 실패 시 `finally`에서 임시 파일을 지운다. **쓰기 도중 프로세스가 죽어도 원본 파일은 손상되지 않는다(임시 파일만 남고 원본은 그대로이거나, 이미 교체가 끝난 새 파일이다).** 이는 1차 감사에서 추론으로 남겼던 부분을 실제 코드로 확인한 것이다.
- **ARC-2 갱신(1차 감사 완화)**: `BreakReview.Csv()`(`BreakReview.cs:11-24`)를 직접 열람했다. CSV는 `routine_name` 컬럼(`entry.RoutineName ?? entry.RoutineId`)을 여전히 포함한다. 즉 1차 감사에서 지적한 "회고 화면에서 루틴 이름 표시 제거"는 **UI 표시만의 변경이며 데이터 손실이 아니다** — 내보내기에는 루틴 식별 정보가 그대로 남는다. ARC-2의 심각도를 "데이터 손실 우려"에서 "UI 표시 범위 축소, 제품 승인 여부만 확인 필요"로 하향 확정한다.
- **BreakHistory 손상 처리(사실, 결함 아님)**: `BreakHistory.Load`가 던지는 예외는 `AppRuntime.cs:89-94`에서 잡혀 `BreakHistory = new()`(메모리상 빈 기록)로 대체되고 `historyWritable = false`가 설정되며 `BreakHistoryError` 배너가 노출된다. `historyWritable`은 `AppRuntime.cs:345`에서 저장 여부를 게이트하므로, **손상된 원본 파일은 이후에도 재저장으로 덮어써지지 않고 디스크에 그대로 남는다.** 다만 해당 세션에서 새로 완료한 휴식은 앱 종료 시 사라진다는 점이 오류 문구로 사용자에게 고지된다 — 조용한 손실이 아니다.
- **ARC-10 (P1)**: `AppSettings.cs:29-54` `Load()`와 `:61-71` `Validate()`. Theme 필드만 `Enum.IsDefined` 실패 시 `AppTheme.OatLatte`로 자체 복구하고 나머지 설정을 보존하는 반면(`:34`), **`Validate()`가 검사하는 다른 모든 필드**(`IntervalMinutes`, `BreakDurationMinutes`, `IdleMinutes`, `PetScalePercent`, `SelectedCharacterId`, `BubbleDirection`, `SnoozeMinutes`, `ReminderSoundId`/`CompletionSoundId`)는 범위를 벗어나거나 알 수 없는 enum 값이면 `Load()` 안에서 잡히지 않는 `InvalidDataException`을 던진다. 이 예외는 `AppRuntime.cs:75-84`의 생성자 catch에서 처리되는데, 거기서는 **`Settings = new()`로 설정 전체를 공장 초기값으로 되돌린다** — `WorkProfiles`, `AdditionalRoutines`, `SelectedCharacterId`, `PetX`/`PetY`, 저장된 사운드 선택 등 손상과 무관한 값까지 전부 사라진다.
  - 재현 시나리오: 디스크에 저장된 `settings.json`의 `bubbleDirection` 필드가 `99`(또는 향후 버전이 5번째 방향을 추가했다가 사용자가 구버전으로 돌아간 경우, 혹은 파일이 부분적으로 손상된 경우)라면, 다음 실행 시 사용자가 공들여 만든 업무 프로필·내 루틴·선택한 펫·창 위치가 전부 초기값으로 리셋된다. 원본 파일은 (Theme 케이스와 달리) `AppRuntime.cs:79-83`의 `File.Copy(settingsFile, settingsFile + ".invalid-...")` 백업 로직이 **이 예외 유형에도 동일하게 적용되므로** 실제로는 보존된다(코드 재확인: catch 절 하나가 Theme이든 다른 필드든 모든 `InvalidDataException`을 동일하게 처리) — 따라서 완전한 영구 손실은 아니지만, 사용자는 수동으로 `.invalid-` 파일을 찾아 값을 복구해야 하며 앱은 스스로 복구하지 않는다.
  - 심각도 근거: Theme 하나만 예외적으로 자체 치유하도록 고친 이번 미커밋 변경이, 의도치 않게 "다른 필드는 여전히 전체 리셋"이라는 기존 비대칭을 부각시켰다. 사용자 대면 영향(업무 프로필·커스텀 루틴처럼 재입력 비용이 큰 데이터가 사소한 필드 하나 때문에 초기화됨)이 실재하고, 트리거 조건(파일 손상 또는 향후 스키마 변경 후 구버전 실행)이 비현실적이지 않아 P1로 판단한다. 완전 영구 손실은 아니므로(백업 파일 존재) P0는 아니다.
  - 권장 조치: `Load()`에서 `Validate()`를 필드 단위로 분리해, Theme·CustomRoutine과 동일하게 "개별 필드 실패 시 해당 필드만 기본값으로 대체" 패턴을 다른 필드에도 확대 적용할지 제품·구현 담당과 논의.
- **CustomRoutine 필드(대조군, 결함 아님)**: `AppSettings.cs:36-45`는 `CustomRoutine`에 한해 이미 개별 필드 단위 복구(`catch (ArgumentException) { value = value with { CustomRoutine = null }; }`)를 하고 있다. 즉 이 코드베이스에는 "개별 필드 복구"와 "전체 리셋" 두 가지 패턴이 이미 공존하며, ARC-10은 이 불균형을 정확히 지적한 것이다.

### 6. 테스트 공백 표

| 위험 | 관련 코드 | 테스트 존재 여부 | 비고 |
|---|---|---|---|
| ARC-7: `ReminderSoundPlayer` 동시 `Preview()` 호출 시 `process` 필드 경쟁 | `ReminderSoundPlayer.cs:35-74` | 없음 — `ReminderSoundPlayer`를 직접 단위 테스트하는 파일 없음(grep 확인) | 헤드리스 Avalonia 테스트는 UI 버튼을 통해서만 간접 호출하므로 이 경합을 재현하지 못함 |
| ARC-10: `Load()`에서 Theme 이외 필드(BubbleDirection 등) 손상 시 전체 리셋 | `AppSettings.cs:29-54,61-71` | 부분적 — `ThemeTests.cs:27`만 디스크의 손상 JSON을 로드해 자체 복구를 검증. 다른 필드가 손상된 디스크 파일을 로드하는 테스트는 없음(`PetReminderTests.cs:113`은 `Save()` 시 예외만 검증, `Load()` 경로는 다루지 않음) | 전체 리셋이 "의도된 동작"인지 "고쳐야 할 결함"인지를 가르는 테스트가 아예 없어, 이 동작이 회귀인지 원래부터의 설계인지 테스트만으로는 알 수 없음 |
| ARC-9: MM:SS 포맷/펫 크기 계산 중복 | `AppRuntime.cs:151`, `SettingsWindow.cs:181`, `PetWindow.cs:126` 등 | 간접적으로 있음(`TimerControlTests`, `BreakReminderTests`가 각 표시값을 개별적으로 단정) | 두 표시가 서로 일치해야 한다는 것 자체를 검증하는 테스트는 없음(각자 정답과만 비교) |
| 창 반복 열기/닫기 시 구독 누적 | `EditorWindow.cs` (유일하게 반복 생성되는 창) | 명시적 구독 누적 테스트는 없음 — `EditorLifecycleTests.cs`가 존재하지만 이번 감사에서 내용을 직접 열람하지 않음(범위 외 파일) | 반복 열기/닫기를 여러 번(N>2) 수행한 뒤 이벤트 핸들러 개수나 메모리를 확인하는 테스트는 grep상 보이지 않음 |
| `Process`(afplay/ffmpeg) 좀비 프로세스 | `ReminderSoundPlayer.cs`, `PetMediaImporter.cs` | 없음 — 둘 다 OS 프로세스를 실제로 실행하므로 헤드리스 CI 테스트가 이 경로를 검증하기 어려움(플랫폼 의존적) | macOS/Windows 실기에서 `ps`로 프로세스 잔류 여부를 확인하는 수동 절차가 필요하나 `docs/verification.md`에 그런 절차가 있는지는 이번 감사에서 확인하지 않음 |

## 미검증 항목

- `dotnet build`/`dotnet test`를 전혀 실행하지 않았으므로, 위 변경들이 실제로 컴파일되는지, 37개 테스트 파일의 신규/수정 단정이 실제로 통과하는지는 확인하지 못했다. 특히 `SettingsWindow.Debug.cs`(신규 미추적 파일)가 SDK 기본 glob에 포함되어 빌드에 정상 포함되는지는 `.csproj` 내용으로만 추론했고 실제 빌드로 검증하지 않았다.
- `SmokeDiagnostics.cs`의 `VerifyThemes`/`VerifyReviewLayout`/`VerifySpeechBubble` 등과 `PetPackDiagnostics.cs`는 각각 `--smoke-test`/`--review-pet-pack` 인자로만 실행되는 수동 진단 경로이며, macOS/Windows 실기에서 실행한 결과를 관찰하지 않았다.
- ~~`BreakReview.Csv()` 및 `BreakHistory`/`BreakReview` 저장 계층 코드 자체를 열람하지 않았다~~ → **2차 감사에서 해소.** `BreakReview.cs`, `BreakHistory.cs`, `AppSettings.cs`, `CharacterLibrary.cs`, `CharacterPack.cs`, `Personalization.cs`, `CustomPetDraft.cs`, `EditorSession.cs`를 전체 열람했다. ARC-2는 사실로 확정(CSV에 routine_name 보존), ARC-10(P1)을 새로 발견했다.
- ~~리소스 누수 관련 diff 범위 밖 파일을 확인하지 않았다~~ → **2차 감사에서 해소.** `AnimationView.cs`, `PetMediaImporter.cs`, `ImageCodec.cs`, `PiskelCodec.cs`, `PixelCanvas.cs`, `ReminderSoundPlayer.cs`, `CharacterLibrary.cs`, `CharacterPack.cs`와 이들을 소유하는 모든 Desktop 창을 전체 열람해 Dispose/using 짝을 전수 대조했다("2차 전수 감사 §1" 표 참조). 짝이 맞지 않는 지점은 찾지 못했으며, `ReminderSoundPlayer`의 동시 `Preview()` 호출 취약성(ARC-7, 확정 결함 아님)만 기록했다. 이는 정적 추적 범위 내의 결론이며, 런타임에서 실제로 누수가 발생하지 않는다는 것을 실행으로 증명한 것은 아니다.
- Windows 플랫폼 분기(`PlatformServices.cs` 등)는 1·2차 모두 지정 범위에 포함되지 않아 별도로 감사하지 않았다 — 여전히 미검증.
- `Process`(ffmpeg/afplay) 종료 후 실제 OS에서 좀비 프로세스가 남는지는 정적 코드로는 `Kill()`/`Dispose()` 호출 여부만 확인 가능하고, 실기에서 `ps`로 확인하지 않았다.
- `EditorLifecycleTests.cs`는 이번 2차 감사에서도 직접 열람하지 않았다(요청 범위인 37개 테스트 파일 전수 검토가 아니라 6개 코어 파일과 8개 리소스 파일 전수 검토였으므로, 테스트 공백 표는 grep 기반 존재 여부 확인 수준이며 테스트 내용의 충분성까지 평가하지 않았다).
