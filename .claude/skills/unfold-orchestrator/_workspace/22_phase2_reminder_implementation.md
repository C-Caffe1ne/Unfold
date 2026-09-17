# 구현 보고서 — Phase 2B / UI-05 접힌 휴식 상태와 복구 경로

## 원인

`AppRuntime.Tick`은 트레이 툴팁과 첫 상태 항목을 `Clock`만으로 만들었다.
알림이 대기하거나 휴식이 진행되면 `Clock.Tick(..., heldForBreak: true)`가 `Remaining`을 줄이지 않으므로
트레이는 `다음 휴식: 12:34 · 진행 중`을 계속 같은 값으로 보여 준다. 말풍선을 접으면 `PetWindow`도
`PetBubbleLayout.Create(direction, expanded: false)`로 192×192 펫만 남기고 `bubble.IsVisible = false`가 되어
알림의 존재를 알리는 표면이 하나도 남지 않았다. 즉 접힌 상태에서 사용자가 볼 수 있는 모든 곳이
"타이머가 멈춰 있다"만 말하고 "휴식 때문에 멈췄다"는 말하지 않았다.

또한 트레이 텍스트는 1초 `DispatcherTimer` 안에서만 갱신됐다. `ShowReminder`/`StartBreak`/`CompleteBreak`/
`SnoozeBreak`/`CancelReminder`는 `RefreshPetNotice()`만 호출했고 이 메서드는 트레이를 건드리지 않아,
상태 전환 직후 최대 1초 동안 트레이가 이전 상태를 표시했다.

## 변경

### 1. 상태 계약 분리 (`AppRuntime.cs`)

`ReminderBadge { None, Waiting, Resting }`와 `TrayReminderStatus(Badge, Status, ToolTip, CanExpand)`를 추가했다.
툴팁·상태 항목·복구 동작·펫 배지가 모두 이 한 계약에서 파생되므로 서로 모순될 수 없다. 네이티브 트레이가
필요 없는 순수 함수라서 테스트가 `NativeMenuItem` 구현 세부에 묶이지 않는다.

- `PetNotice.Invitation` → `휴식 대기 중`, `PetNotice.Resting` → `휴식 중`.
  상태 항목은 `{라벨} · 작업 타이머 {mm:ss} 멈춤`, 툴팁은 `Unfold · {라벨} · 타이머 {mm:ss} 멈춤`으로
  같은 라벨을 앞세운다.
- `Advance`(5분 전 예고)와 `Completed`(완료 안내)는 작업 타이머를 붙잡지 않으므로 기존
  `다음 휴식: … · {시계 상태}` 문구를 유지한다.
- `BadgeFor(notice, bubbleCollapsed)`는 말풍선이 접혔을 때만 `Waiting`/`Resting`을 돌려준다.
  배지 표시와 복구 동작 활성화가 같은 조건을 쓴다.
- 트레이 라벨 자체는 말풍선을 펼쳤을 때도 `휴식 중`으로 유지한다(펼친 상태에서도 사실이며 툴팁과
  어긋나지 않는다). 펼친 상태에서는 `Badge = None`, `CanExpand = false`가 되어 배지와 복구만 사라진다.

### 2. 즉시 갱신 (`AppRuntime.cs`)

- `Tick`의 트레이 갱신 블록을 `RefreshTray()`로 분리했다.
- `RefreshPetNotice()`가 첫 줄에서 `RefreshTray()`를 호출한다. 이 메서드는 이미
  `ShowReminder`, `StartBreak`, `SnoozeBreak`, `CompleteBreak`, `CancelReminder`(→`Stop`/`Reset`), `Tick`이
  호출하므로 알림 생성·시작·완료·미루기·취소가 다음 Tick을 기다리지 않는다.
- `UpdateSettings`가 `await UpdatePet()` 뒤에 `RefreshPetNotice()`를 호출한다. 말풍선 접기/펼치기는
  `BubbleCollapsed` 저장 경로를 지나므로 배지와 복구 동작도 즉시 반영된다.
- 생성자와 `BuildTray()` 끝에서 `RefreshTray()`를 호출해 앱 시작 직후 첫 Tick 전에도 트레이가 비어 있지 않다.
- `AppRuntime.TrayStatus`(public)를 노출해 QA와 테스트가 네이티브 메뉴 없이 현재 트레이 계약을 읽는다.

### 3. 트레이 복구 동작 (`AppRuntime.cs`)

- 트레이 메뉴 상태 항목 바로 아래에 **휴식 알림 펼치기**(`trayExpand`)를 추가했다.
- `CanExpandReminder`(= 활성 알림 + 접힌 말풍선)일 때만 `IsEnabled = true`다.
- `ExpandReminder()`는 펼치기만 한다(`BubbleCollapsed = false`). 토글이 아니므로 이미 펼친 상태를 접지 않는다.
  `Reminder.Session`, `session.Elapsed`, `Clock`은 건드리지 않는다. 창 활성화나 포커스 탈취는 하지 않는다.

### 4. 펫 상태 배지 (`PetWindow.cs`)

- 22×22 원형 `Border`(`PetReminderBadge`) 안에 10×10 `Ellipse`(`PetReminderBadgeMark`)를 두고
  펫 영역 우상단(`layout.Pet` 기준 +6, 우측 여백 6)에 배치했다.
- **대기**는 테두리만 있는 빈 링(`Stroke = Warning`, `Fill = Transparent`, 두께 2),
  **진행**은 채워진 점(`Fill = Cream`, 두께 0)이다. 색·문구가 아니라 채움 여부라는 형태로 두 상태를 나눈다.
- `IsHitTestVisible = false`라 드래그·클릭 반응·Windows 클릭 통과 판정(`UpdateClickThrough`는 펫 스프라이트의
  불투명 픽셀과 말풍선만 본다)에 끼어들지 않는다.
- `RefreshSpeech()`의 조기 반환(레이아웃 무변경 경로)보다 앞에서 `RefreshBadge(next.Pet)`를 호출한다.
  레이아웃이 그대로여도 알림 상태 변화가 배지에 반영된다.
- `AutomationProperties.SetName`으로 `휴식 대기 중`/`휴식 중`을 붙였다. UI-07~UI-11 접근성 단계는 건드리지 않았고,
  새로 추가한 요소에 이름을 비워 두지 않은 것뿐이다.

## 소유 파일

- `src/Unfold.Desktop/AppRuntime.cs`
- `src/Unfold.Desktop/PetWindow.cs`
- `Tests/Unfold.Tests/BreakReminderTests.cs`

소유 파일 밖은 수정하지 않았다. 같은 작업 트리의 다른 워커 변경
(`Ui.cs`, `SettingsWindow.Layout.cs`, `PetPackWindow.cs`, `PersonalizationWindow.cs`,
`DesignSystemTests.cs`, `PetPackWindowTests.cs`, `PersonalizationWindowTests.cs`,
`SettingsDashboardTests.cs`, `CustomPetTests.cs`, `ScrollGutterProbe.cs`)은 그대로 두었다.

## 회귀 검사

`Tests/Unfold.Tests/BreakReminderTests.cs`에 두 개를 추가했다.

1. `TrayStatusNamesWaitingAndRestingAndOffersRecoveryOnlyWhenFolded` — `TrayReminderStatus.Create`의
   순수 계약. 대기/진행/없음, 접힘/펼침, `Advance`·`Completed`의 비개입, 상태 항목과 툴팁이 같은 라벨을
   쓰는지를 고정한다. 트레이 네이티브 구현에 의존하지 않는다.
2. `FoldedReminderKeepsTrayAndBadgeInStepThroughRecoveryAndCompletion` — 실제 `AppRuntime` + `PetWindow`로
   `접힌 대기 → 접힌 진행 → 트레이 복구 → 완료 → 재알림 후 타이머 중지` 전 구간을 돈다. 각 단계에서
   Tick 없이 `TrayStatus`가 바뀌는지, 배지 `IsVisible`과 `Fill`/`StrokeThickness`가 대기와 진행에서
   서로 다른지, 접힌 동안 창이 192×192를 유지하는지, 복구 후 `Reminder.Session`과 `session.Elapsed`가
   보존되고 `settings.json`의 `BubbleCollapsed`가 `false`로 저장되는지, 새 창이 열리지 않는지를 확인한다.

기존 `PetFoldingAndAllDirectionsKeepSessionAndButtonsInsideOneWindow`(4방향·접기·세션 보존)과
`BubbleButtonsAllowEarlyCompletionWithoutOpeningAnotherWindow`는 수정 없이 통과한다.

## 검증

격리 데이터 디렉터리(`UNFOLD_DATA_DIR=$(mktemp -d)`)로 실행했다.

| 명령 | 결과 |
|---|---|
| `dotnet test Tests/Unfold.Tests --no-restore --filter "FullyQualifiedName~BreakReminderTests"` | Passed 5 / Failed 0 |
| `dotnet test Tests/Unfold.Tests --no-restore --filter "…BreakReminderTests\|…AnimationLifecycleTests\|…SettingsUiTests\|…SettingsReliabilityTests\|…PetReminderTests\|…TimerControlTests\|…BreakSessionTests"` | Passed 53 / Failed 0 |
| `dotnet build src/Unfold.Desktop -c Release --no-restore` | 성공, 경고 0 |

**전체 스위트는 통합 QA 대기 상태다.** 같은 작업 트리에서 21·23번 작업이 동시에 진행 중이라
전체 실행 결과는 이 작업의 완료 판정 근거가 아니다. 위 소유 범위 집중 검사만 이 작업의 근거다.

참고로 전체 스위트(`dotnet test Tests/Unfold.Tests --no-restore`)는 Passed 170 / Failed 3이며, 실패 3건은 모두 이 작업 범위 밖이다.
소유 파일 3개를 `git checkout`으로 되돌린 상태에서 같은 3건이 동일하게 실패하는 것을 확인한 뒤 복원했다.

- `DesignSystemTests.PageActionsRemainVisibleAtMinimumSizeAndWhileTheBodyScrolls` — 21번(스크롤) 작업 진행 중.
- `PetPackWindowTests.EnlargedLightPreviewFitsTheMinimumWidthAndDoesNotInstall` — 23번(팩/개인화) 작업 진행 중.
- `CharacterAssetAuditTests.BundledAssetsAreDecodedAndInventoryIncludesHashes` — 테스트 출력 폴더에
  `coco-siamese`, `luna-blue`, `miso-tabby`의 `character.json`이 복사되지 않았다(자산 복사 문제, 코드 무관).

검사 도중 `PersonalizationWindowTests.cs`가 다른 워커의 편집 중간 상태라 테스트 어셈블리 컴파일이 한 번
실패했다. 그 파일은 수정하지 않고 재실행해 통과시켰다.

`Ui.cs` 소유권 복구 지시(`msg_4ee2f0b7e084`)를 받았으나 이 작업은 `Ui.cs`를 한 번도 수정하지 않았다.
현재 `Ui.cs`는 `AllowAutoHide = false`, `bar.Margin = new Thickness(ScrollGutter, 0, 0, 0)` 상태이며 그대로 두었다.

## 미검증·위험

- **실기 미확인.** macOS/Windows 실제 실행에서 트레이 메뉴의 **휴식 알림 펼치기** 비활성 표시와
  배지의 크기·대비를 눈으로 확인하지 않았다. 위 결과는 모두 headless 자동 검사다.
  특히 macOS 메뉴 막대에서 `NativeMenuItem.IsEnabled = false`의 시각적 구분은 OS 렌더링에 달려 있다.
- **배지 영역의 클릭 통과.** 배지는 `IsHitTestVisible = false`이므로 Windows 클릭 통과 판정은 배지 아래의
  펫 픽셀 불투명도만 본다. 즉 배지 위 클릭은 뒤쪽 앱으로 전달된다. 배지는 상태 표시 전용이고 복구 경로는
  트레이이므로 의도한 동작이지만, 배지를 눌러 펼치려는 사용자가 있을 수 있다.
- **배지 위치.** 펫 스프라이트 우상단에 고정한다. 우상단이 불투명한 캐릭터 팩에서는 배지가 펫 몸에
  겹쳐 보일 수 있다. 기본 팩 기준으로만 배치를 정했다.
- **트레이 상태 라벨 범위.** 말풍선을 펼친 상태에서도 트레이는 `휴식 중`/`휴식 대기 중`을 표시한다.
  Task spec은 접힌 경우만 요구했으나, 펼친 경우에도 사실이고 툴팁과 어긋나지 않아 조건을 나누지 않았다.
  접힌 경우로 한정해야 한다면 `TrayReminderStatus.Create`의 `label` 계산에 `bubbleCollapsed`를 더하면 된다.
- **알림·기록 데이터 형식**은 바꾸지 않았다. `BreakSession`, `BreakHistory`, `AppSettings` 스키마 무변경.
