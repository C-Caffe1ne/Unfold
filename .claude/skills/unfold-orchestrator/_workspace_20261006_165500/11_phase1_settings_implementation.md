# 구현 보고서 — Phase 1A (UI-01, UI-02)

2026년 09월 16일 · `release/mvp` 작업 트리 · .NET 10 / Avalonia 12.1.2.
대상: `docs/validation/2026-09-16-ui-ux-audit.md`의 UI-01, UI-02.

## 원인

### UI-01 — 편집 대상이 드롭다운 선택과 무관했다

`SettingsWindow.Layout.cs`의 `OpenRoutine()`이 대시보드 `RoutinePicker`의 선택을 읽지 않고
항상 `runtime.Settings.CustomRoutine`을 편집기에 넘겼다.

```csharp
private Task OpenRoutine() => new RoutineEditorWindow(runtime.Settings.CustomRoutine, ...)
```

`RoutineEditorWindow`는 `existing`이 `null`이면 `routineId ?? BreakRoutines.CustomId`를 id로 쓰므로,
결과적으로 **선택과 무관하게 항상 `my-routine` 슬롯이 저장 대상**이 되었다. 추가 루틴(`routine-…`)을
선택한 채 **내 루틴 편집**을 누르면 보이지 않는 다른 루틴을 덮어썼다. 버튼은 기본 루틴이 선택된
상태에서도 항상 활성이어서, 편집할 수 없는 대상을 고른 사용자에게도 편집 가능하다는 신호를 주었다.

라이브러리(`PersonalizationView.RefreshActions`)는 이미 `BreakRoutines.Find(id) is null`로 기본 루틴을
막고 있었으므로, 대시보드만 같은 계약에서 벗어나 있었다.

### UI-02 — 프로필 적용 제약이 클릭 뒤에만 드러났다

`AppRuntime.UpdateSettings`는 `ActiveProfileId`가 바뀌면 타이머를 재예약하므로
`CanEditTimerInterval`(= `Clock.Paused || Clock.Stopped`)이 아닐 때 `ArgumentException`을 던진다.
`SettingsWindow`는 같은 제약을 **알림 간격** 필드에는 클릭 전에 반영하고 있었지만
(`interval.IsEnabled = canEditInterval`), `PersonalizationView`는 런타임을 전혀 모르고
`applyProfile.IsEnabled = profile is not null`만 판단했다. 타이머 진행 중에도 버튼이 활성으로 보였고,
사용자는 누른 뒤에야 오류 문구를 만나 타이머 탭과 프로필 탭을 왕복해야 했다.

## 변경

### UI-01

`src/Unfold.Desktop/SettingsWindow.Layout.cs`

- `SelectedEditableRoutine()`을 추가해 `routines.SelectedItem`이 사용자 루틴일 때만 반환한다
  (`BreakRoutines.Find(id) is null`). 기본 루틴과 빈 선택은 `null`이다.
- `OpenRoutine()`은 이 값을 `RoutineEditorWindow(existing: selected, …)`로 넘긴다. `existing`이
  비어 있지 않으므로 편집기의 id·이름·단계가 선택한 루틴 그대로다. 저장 경로는
  기존 `runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine))` 계약을 그대로 쓴다.
- `RefreshRoutineEditing()`이 버튼 활성 상태와 안내 문구를 함께 갱신한다.
  - 사용자 루틴: `선택한 ‘{이름}’ 루틴의 이름과 단계를 편집해요.`
  - 기본 루틴/빈 선택: `기본 루틴은 편집할 수 없어요. ‘루틴 · 프로필’에서 새 루틴을 만들어 보세요.`
  - 같은 문장을 `ToolTip.SetTip`과 `AutomationProperties.SetHelpText`에 모두 넣었다. 비활성
    버튼은 플랫폼에 따라 Tooltip이 뜨지 않으므로 접근성 도움말을 병행한다.
- `routines.SelectionChanged`에 `RefreshRoutineEditing`을 연결하고, 카드 생성 시 한 번 호출한다.

`src/Unfold.Desktop/SettingsWindow.cs`

- `Refresh()`의 루틴 목록 재바인딩 직후 `RefreshRoutineEditing()`을 호출해, 루틴이 저장·삭제되어
  `ItemsSource`가 교체될 때도 버튼 상태와 문구가 최신 선택을 따르게 했다.

### UI-02

`src/Unfold.Desktop/PersonalizationWindow.cs`

- `PersonalizationView` 생성자에 선택적 `Func<bool>? canApplyProfile = null`을 추가했다.
  `null`이면 `static () => true`로 기존 동작을 유지한다. 위치는 마지막 매개변수이며
  기존 4개 인자 호출은 그대로 컴파일된다.
- `RefreshActions()`에서 적용 가능 여부를 분리했다.
  - `editProfile`/`deleteProfile`은 종전대로 선택 유무만 본다(타이머와 무관한 동작).
  - `applyProfile.IsEnabled = profile is not null && blocked is null`.
  - 막힌 사유는 `타이머를 일시정지하거나 중지한 뒤 프로필을 적용할 수 있어요.`이며
    `ProfileDetail` 문구 끝에 줄바꿈으로 덧붙고, `ToolTip` + `AutomationProperties.SetHelpText`에도 실린다.
  - 적용 가능할 때는 `선택한 프로필의 루틴과 알림 간격, 자리 비움 기준을 지금 적용해요.`를 쓴다.
- 목록을 다시 만들지 않고 버튼 상태만 재평가하는 `public void RefreshAvailability() => RefreshActions();`를
  추가했다. `Refresh()`는 `ItemsSource`와 선택을 재설정하므로 상태 전이용으로는 쓰지 않는다.

`src/Unfold.Desktop/SettingsWindow.Layout.cs`

- `OpenPersonalization()`이 `canApplyProfile: () => runtime.CanEditTimerInterval`을 전달한다.

`src/Unfold.Desktop/SettingsWindow.cs`

- `Refresh()`에서 `personalizationPage?.RefreshAvailability()`를 호출한다. `runtime.Changed`는
  `TogglePause`/`Stop`에서 발생하므로, 타이머가 일시정지·중지로 바뀌면 프로필 탭에 머문 채로도
  버튼이 다시 활성화된다. `Refresh()`가 아닌 `RefreshAvailability()`를 쓰기 때문에 목록 선택은 유지된다.

`AppRuntime.UpdateSettings`의 방어 검사는 건드리지 않았다. UI는 사전 표시를 담당하고 런타임은
마지막 방어선으로 남는다.

### 유지한 계약

- `AppSettings.SaveRoutine` / `runtime.UpdateSettings` 저장 경로 변경 없음.
- `PersonalizationWindow`의 공개 생성자 시그니처 변경 없음. 독립 창은 런타임 콜백이 없으므로
  적용 가능 상태를 기본값 `true`로 사용한다.
- Core 저장 형식, 타이머 규칙, 디자인 토큰, 스크롤바, 말풍선, 펫 팩 레이아웃은 손대지 않았다.

## 소유 파일

| 파일 | 변경 |
|---|---|
| `src/Unfold.Desktop/SettingsWindow.Layout.cs` | +29 / -3 · 편집 대상 선택, 버튼 상태·도움말, 프로필 콜백 전달 |
| `src/Unfold.Desktop/SettingsWindow.cs` | +4 · `Refresh()`에서 두 화면의 상태 재평가 |
| `src/Unfold.Desktop/PersonalizationWindow.cs` | +20 / -4 · 적용 가능 여부 주입, 사유 문구, `RefreshAvailability` |
| `Tests/Unfold.Tests/SettingsDashboardTests.cs` | +78 · UI-01 회귀 2건, UI-02 회귀 1건 |
| `Tests/Unfold.Tests/PersonalizationWindowTests.cs` | +14 · 독립 창 호환 회귀 1건 |

소유 파일 밖은 수정하지 않았다. 같은 작업 트리의 다른 워커 산출물
(`CustomPetWindow.cs`, `CustomPetTests.cs`, `PetWindow.cs`, `PetPackDiagnostics.cs`,
`AnimationLifecycleTests.cs`, `docs/README.md`, `CLAUDE.md`)과 사용자의 기존 변경은 그대로 두었다.

## 검증

### 추가한 회귀 검사

| 테스트 | 검증 대상 |
|---|---|
| `SettingsDashboardTests.EditRoutineFollowsThePickerAndStaysLockedOnBuiltIns` | 기본 루틴 선택 시 `SettingsEditRoutine` 비활성 + 도움말에 `루틴 · 프로필` 안내, 사용자 루틴 선택 시 활성 + 루틴명 안내, 다시 기본 루틴으로 돌아가면 비활성 |
| `SettingsDashboardTests.EditRoutineOpensTheRoutineTheUserSelected` | 추가 루틴 `writing`과 `my-routine`이 모두 있을 때, 선택한 `writing`의 이름·1단계 안내·초가 편집기에 그대로 열리고, 저장이 `writing`만 바꾸며 `CustomRoutine`은 보존됨 |
| `SettingsDashboardTests.ProfileApplyShowsTheTimerBlockBeforeTheClickAndReturnsWhenPaused` | 타이머 진행 중 `프로필 적용` 비활성 + `ProfileDetail`·도움말에 `타이머를 일시정지하거나 중지` 표시, `TogglePause` 후 선택 유지·버튼 재활성·문구 제거·실제 적용 성공 |
| `PersonalizationWindowTests.StandaloneLibraryKeepsProfileApplyAvailableWithoutARuntime` | 독립 `PersonalizationWindow`는 런타임 콜백 없이 적용 버튼이 활성이고 사유 문구가 없으며 적용이 동작 |

### 실행한 명령과 결과

```
dotnet build Unfold.slnx -c Release
→ Build succeeded. 0 Warning(s) 0 Error(s)

UNFOLD_DATA_DIR=/tmp/unfold-phase1-settings-20260916 \
dotnet test Unfold.slnx -c Release --no-build \
  --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests"
→ Passed! Failed: 0, Passed: 15, Skipped: 0, Total: 15

UNFOLD_DATA_DIR=/tmp/unfold-phase1-settings-20260916b \
dotnet test Unfold.slnx -c Release --no-build \
  --filter "FullyQualifiedName~DesignSystemTests|FullyQualifiedName~SettingsUiTests|FullyQualifiedName~RoutineEditorTests|FullyQualifiedName~PersonalizationTests|FullyQualifiedName~SettingsReliabilityTests|FullyQualifiedName~TimerControlTests"
→ Passed! Failed: 0, Passed: 41, Skipped: 0, Total: 41

UNFOLD_DATA_DIR=/tmp/unfold-phase1-settings-20260916c \
dotnet test Unfold.slnx -c Release --no-build \
  --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests|FullyQualifiedName~DesignSystemTests"
→ Passed! Failed: 0, Passed: 21, Skipped: 0, Total: 21
```

각 실행은 새 `UNFOLD_DATA_DIR`을 사용했다. 두 번째 실행은 이 변경이 접하는 인접 화면
(`PersonalizationWindow` 테마·삭제 확인, 설정 저장 실패 경로, 루틴 편집기 수명, 타이머 컨트롤)을
포함한다. 전체 회귀는 감독자 통합 후 실행 대상으로 남긴다.

### 회귀 검사가 결함을 실제로 잡는지 확인

세 테스트가 통과만 하는 검사가 아닌지 확인하기 위해, 수정 전 동작을 코드에 되돌린 뒤
(`OpenRoutine`이 `CustomRoutine`을 열고, `routineEdit.IsEnabled = true`,
`applyProfile.IsEnabled = profile is not null`, 사유 문구 제거) 같은 세 테스트를 실행했다.

```
→ Failed! Failed: 3, Passed: 0, Total: 3
  EditRoutineOpensTheRoutineTheUserSelected                  Assert.Equal() Failure: Strings differ
  ProfileApplyShowsTheTimerBlockBeforeTheClickAndReturnsWhenPaused  Assert.False() Failure
  EditRoutineFollowsThePickerAndStaysLockedOnBuiltIns        Assert.False() Failure
```

확인 뒤 수정 코드를 복원하고 다시 빌드·실행해 통과를 확인했다(위 세 번째 실행).

### QA 재현 명령

```sh
cd /Users/hwanghyeonseong/Documents/GitHub/Unfold
dotnet build Unfold.slnx -c Release
UNFOLD_DATA_DIR="$(mktemp -d)" dotnet test Unfold.slnx -c Release --no-build \
  --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests"
```

## 미검증·위험

- **실제 OS 조작 미확인.** 모든 결과는 Avalonia 헤드리스 자동 검사다. macOS·Windows에서 마우스와
  Tab 키로 대시보드 편집 버튼과 프로필 적용을 실제로 눌러 본 결과는 이 세션에 없다.
- **비활성 버튼의 Tooltip 노출.** Avalonia에서 비활성 컨트롤은 Tooltip을 띄우지 못할 수 있다.
  그래서 `AutomationProperties.SetHelpText`를 함께 넣었지만, VoiceOver·Narrator가 이 도움말을
  실제로 읽는지는 확인하지 않았다. 감사 항목 UI-09(비활성 사유의 접근성 연결)는 3단계 범위이므로
  이번 변경에서 전면 정리하지 않았다.
- **`ProfileDetail`의 두 줄 문구.** 사유를 줄바꿈으로 덧붙여 최소 창에서 프로필 탭 높이가
  한 줄 늘어난다. `ListBox` 높이가 230으로 고정되어 있어 헤드리스 검사에서는 넘침이 없었으나,
  실기 최소 창(860×680)에서의 시각 확인은 하지 않았다.
- **`SettingsEditRoutine` 라벨.** 버튼 문구는 `내 루틴 편집` 그대로다. 이제 선택한 루틴을 편집하므로
  선택 대상 표시가 더 명확한 라벨이 나을 수 있으나, Task 범위 밖이라 바꾸지 않았다.
- **UI-12와의 인접성.** `activeProfile`(`직접 설정한 알림 / 업무 프로필`) 문구가 루틴 선택기 바로 아래
  있어 선택 루틴의 설명처럼 읽히는 문제는 2단계 항목이므로 그대로 두었다.
- **UI-03 이후 항목 미구현.** 커스텀 펫 파일 교체, 스크롤바 겹침, 접힌 말풍선 상태 등은
  이번 변경 범위가 아니다. `CustomPetWindow.cs`는 다른 워커가 같은 트리에서 수정 중이며
  위 테스트 실행에는 그 변경이 포함된 상태로 통과했다.
