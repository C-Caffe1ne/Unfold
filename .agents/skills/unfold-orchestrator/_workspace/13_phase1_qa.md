# 릴리스 QA 감사 — Phase 1 (UI-01, UI-02, UI-03)

2026년 09월 16일 · `release/mvp` `7e81f77` + 현재 공유 작업 트리 · 독립 검증.
대상: `docs/validation/2026-09-16-ui-ux-audit.md` 1단계, 구현 보고서 `11_phase1_settings_implementation.md`,
`12_phase1_custom_pet_implementation.md`.

## 결론

**Phase 1의 세 항목은 이 트리에서 통과했고, 출시를 막는 결함은 발견하지 않았다.**
전체 Release 테스트 167건이 실패·건너뜀 없이 통과했고, 새 격리 데이터에서 실행한 macOS 네이티브
스모크 진단도 `success: true`로 끝났다. 세 항목의 완료 조건은 코드 경계와 자동 검사로 모두 확인했다.

다만 **UI-01·UI-02·UI-03의 새 동작은 헤드리스 자동 검사에만 근거한다.** 스모크 진단은 이 세 경로를
직접 누르지 않으므로(아래 `미검증` 참고), 실제 마우스·키보드 조작과 Windows 결과는 여전히 없다.
아래 `관찰 4`는 진단 캡처에서 확인한 **2단계(UI-04) 범위의 새 근거**다.

## 환경과 범위

| 항목 | 값 |
|---|---|
| OS | macOS 26.6.2 (25G83), arm64 |
| .NET SDK | 10.0.401 · 런타임 10.0.12 |
| 브랜치 | `release/mvp`, 마지막 커밋 `7e81f77` |
| 작업 트리 | 미커밋 변경 12개 파일(다른 워커·사용자 변경 포함). 이번 QA에서 수정한 파일 없음 |
| 데이터 격리 | 전체 회귀 `/private/tmp/claude-501/unfold-qa-full-80N4uo`, 집중 회귀 `…/unfold-qa-focus-IJKjm8`, 스모크 `…/unfold-qa-smoke-20260916` (각각 새 빈 디렉터리) |

검증 범위에 포함한 변경 파일:
`SettingsWindow.Layout.cs`, `SettingsWindow.cs`, `PersonalizationWindow.cs`, `CustomPetWindow.cs`와
`SettingsDashboardTests.cs`, `PersonalizationWindowTests.cs`, `CustomPetTests.cs`.
같은 트리의 `PetWindow.cs`, `PetPackDiagnostics.cs`, `AnimationLifecycleTests.cs`, `docs/README.md`,
`CLAUDE.md`는 다른 워커·사용자 변경이므로 회귀 영향만 확인하고 감사 대상으로 삼지 않았다.

## 실행한 명령과 결과

```sh
dotnet restore Unfold.slnx
# 종료 코드 0 · All projects are up-to-date

UNFOLD_DATA_DIR=/private/tmp/claude-501/unfold-qa-full-80N4uo \
dotnet test Unfold.slnx -c Release --no-restore
# 종료 코드 0
# Passed!  - Failed: 0, Passed: 167, Skipped: 0, Total: 167, Duration: 37 s

UNFOLD_DATA_DIR=/private/tmp/claude-501/unfold-qa-focus-IJKjm8 \
dotnet test Unfold.slnx -c Release --no-build --filter \
  "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests|\
FullyQualifiedName~CustomPet|FullyQualifiedName~PetPackWindowTests|FullyQualifiedName~SettingsUiTests|\
FullyQualifiedName~DesignSystemTests|FullyQualifiedName~RoutineEditorTests|FullyQualifiedName~TimerControlTests|\
FullyQualifiedName~SettingsReliabilityTests"
# Passed!  - Failed: 0, Passed: 59, Skipped: 0, Total: 59, Duration: 13 s

dotnet build src/Unfold.Desktop -c Release --no-restore
# 종료 코드 0 · 0 Warning(s) 0 Error(s)

git diff --check
# 종료 코드 0 (공백·충돌 표식 없음)

UNFOLD_DATA_DIR=/private/tmp/claude-501/unfold-qa-smoke-20260916 \
dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
# 종료 코드 0
```

`Skipped: 0`이므로 MP4 변환 도구가 필요한 두 테스트도 이번 실행에서 건너뛰지 않고 통과했다.

### 스모크 진단 결과

`verification/smoke.json`의 주요 값:

| 키 | 값 |
|---|---|
| `success` | `true` |
| `imageFiles` | 42 |
| `characters` | 4 · `completedBreaks` 1 · `exportedBreaks` 1 |
| `savedCustomRoutines` | 2 · `workProfiles` 1 |
| `customPetGifAuthoringVerified` | `true` |
| `petPackInstallUpdateRepairVerified` | `true` |
| `sharedDesignDialogsVerified` | `true` · `pinnedPageActionsVerified` `true` |
| `timerControlsVerified` | `true` · `timerPausedApplyWaitSeconds` 1.2 · `timerStopWaitSeconds` 1.2 |
| `settingsLayout` | 860×680, `noHorizontalOverflow` `true`, `inWindowTabsVerified` `true`, `petDraftPreserved` `true` |
| `os` / `framework` | `Unix 26.6.2` / `10.0.12` |

디스크 확인: `verification/` 아래 PNG **42장**(문서 기준값과 일치), CSV 1건, `custom-pet.unfoldpet` 1건.
프로필 전체로는 PNG 46장이며 추가 4장은 설치된 캐릭터 3종과 `pack-source`의 라이브러리 썸네일이다.

환경 실패는 없었다. 감사 기록에 있던 Avalonia RenderTimer `-6661`은 이번 실행에서 재현되지 않았다.

## 경계면 교차 검증

### UI-01 — 편집 대상과 드롭다운 선택

| 확인 | 근거 |
|---|---|
| 편집 대상 선정 | `SettingsWindow.Layout.cs:162` `SelectedEditableRoutine()`이 `routines.SelectedItem is BreakRoutine && BreakRoutines.Find(id) is null`일 때만 반환한다. 기본 루틴과 빈 선택은 `null`. |
| 편집기 id·이름·단계 | `RoutineEditorWindow.cs:16-19`가 `existing?.Id ?? routineId ?? CustomId`, `existing?.Name`, `routine.Steps`를 쓴다. `existing`이 비어 있지 않으므로 **선택한 루틴의 id로 저장**된다. 이전 코드의 `CustomRoutine` 고정 경로가 사라졌다. |
| 저장 계약 | `runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine))` 그대로다. Core 저장 형식 변경 없음. |
| 상태 갱신 양쪽 | 생산자: `routines.SelectionChanged`(`Layout.cs:137`)와 `SettingsWindow.cs:164` `Refresh()`의 목록 재바인딩 직후. 소비자: `RefreshRoutineEditing()`이 `IsEnabled`·`ToolTip`·`AutomationProperties.HelpText`를 함께 쓴다. 루틴 저장·삭제로 `ItemsSource`가 교체돼도 버튼 상태가 따라간다. |
| 비활성 시 안전성 | `OpenRoutine()`이 `null`이면 `Task.CompletedTask`를 돌려준다. 버튼이 비활성이라 UI로는 도달하지 않지만 프로그래밍 호출에서도 잘못된 대상을 열지 않는다. |
| 라이브러리와의 일관성 | `PersonalizationWindow.cs:141`의 `editRoutine.IsEnabled = … BreakRoutines.Find(selected.Id) is null`과 동일한 판정 기준을 쓴다. 두 화면의 계약이 일치한다. |

### UI-02 — 프로필 적용 가능 상태

| 확인 | 근거 |
|---|---|
| 런타임 제약과 UI 표시의 출처 | `AppRuntime.cs:23` `CanEditTimerInterval => Clock.Paused \|\| Clock.Stopped`. `Layout.cs:186`이 같은 속성을 `canApplyProfile`로 넘긴다. UI가 런타임과 **같은 단일 출처**를 읽는다. |
| 런타임 방어선 유지 | `AppRuntime.cs:137-141`의 `reschedulesTimer && !CanEditTimerInterval` → `ArgumentException`은 그대로다. `git status`에 `AppRuntime.cs` 없음. 사전 표시와 최종 방어가 분리돼 있다. |
| 사유 전달 경로 | `PersonalizationWindow.cs:145-150`에서 `ProfileDetail` 본문, `ToolTip`, `AutomationProperties.SetHelpText`에 같은 문장을 넣는다. 비활성 컨트롤에서 Tooltip이 뜨지 않아도 접근성 도움말은 남는다. |
| 선택 보존 | `SettingsWindow.cs:147`이 `Refresh()`가 아닌 `RefreshAvailability()`를 호출한다. `RefreshActions()`만 돌므로 `ItemsSource`·선택이 재설정되지 않는다. 일시정지·중지 전이 후 같은 프로필을 다시 누를 수 있다. |
| 상태 전이 도달성 | `TogglePause()`/`Stop()`이 `Changed?.Invoke()`를 호출(`AppRuntime.cs:130` 등) → `SettingsWindow.Refresh()` → `personalizationPage?.RefreshAvailability()`. 프로필 탭에 머문 채로도 버튼이 되살아난다. |
| 독립 창 호환 | `PersonalizationWindow.cs:16`은 4인자 호출이라 `canApplyProfile ?? (static () => true)`로 종전 동작을 유지한다. 컴파일·회귀 모두 통과(`PersonalizationWindowTests`, `DesignSystemTests`의 4개 호출 지점 포함). |
| 다른 저장 경로 영향 없음 | `루틴 사용`·`프로필 편집`·`프로필 삭제`는 `ActiveProfileId`와 `IntervalMinutes`를 바꾸지 않으므로 `reschedulesTimer`가 `false`다. 종전대로 선택 유무만 본다(`PersonalizationWindow.cs:144`). |

### UI-03 — 커스텀 펫 파일 교체

| 확인 | 근거 |
|---|---|
| 두 진입 경로 모두 확인 통과 | `CustomPetWindow.cs:126` `AssignPending()`과 `:137` `SelectFile(key)`가 `Assign` 앞에서 `ConfirmReplace`를 호출한다. 가져오기 배정과 슬롯별 `파일 선택…` 모두 덮어쓰기 전에 확인한다. |
| 빈 동작은 확인 없음 | `ConfirmReplace`는 `!draft.Clips.TryGetValue(key, out _)`이면 즉시 `true`. 첫 배정 흐름과 스모크 진단 경로에 대화상자가 끼어들지 않는다. |
| 취소 계약 | `Ui.cs:87-104` `Confirm`은 `취소`에 `IsCancel`·`Quiet`·초기 포커스를 주고, 제목 표시줄 닫기는 `-1`을 돌려준다. `ConfirmReplace`는 `choice == 0`일 때만 `true`이므로 **두 취소 경로 모두 교체하지 않는다.** `choices.Length != 1`이라 `바꾸기`에 `IsDefault`가 붙지 않는다. |
| 가져온 파일 보존 | 취소 시 `AssignPending()`이 `pendingPath`를 비우지 않고 `return`한다. 같은 파일을 다른 동작에 배정할 수 있다. |
| 검증 실패 시 기존 클립 보존 | `Assign()`(`:168-182`)은 `PetMediaImporter.Import`가 던지면 `draft.SetClip`에 도달하지 못하고 `ShowError` 후 `false`를 돌려준다. 기존 `draft.Clips[key]`가 그대로 남고 `Refresh()`가 생성 가능 상태를 유지한다. 이 계약은 변경되지 않았다. |
| 다음 빈 동작 이동 | `SelectNextEmptyAction()`(`:159-167`)은 `offset = 1..Count-1`로 순환하며 현재 슬롯을 다시 고르지 않는다. 5개가 모두 차면 현재 선택을 유지한다. `Assign()`이 `draft.SetClip` 후 반환하므로 `draft.Clips`는 이미 최신이다. |
| 부작용 없음 | `action` ComboBox에 `SelectionChanged` 구독자가 없어(`grep` 확인) `SelectedIndex` 변경이 다른 흐름을 건드리지 않는다. |
| 상태 문구 유지 | 취소 경로는 `Refresh()`를 호출하지 않으므로 `CustomPetStatus`의 `…기존 파일을 그대로 두었어요.`가 덮이지 않는다. |
| 소유 창 | `PetManagementView.cs:20`이 설정 창 안 탭에서도 `owner`로 **SettingsWindow**를 넘긴다. 실제로 표시된 창이므로 `ShowDialog(owner)` 계약이 성립하고, 기존 `PetPackView`의 확인 대화상자와 같은 패턴이다. |

### 상호 간섭·기존 기능

| 확인 | 결과 |
|---|---|
| 펫 팩 탭(열기/만들기) | `PetPackWindowTests` 통과. 스모크의 `petPackInstallUpdateRepairVerified: true`, `settings-pet-open-tab.png`·`settings-pet-create-tab.png` 생성. |
| 설정 저장·실패 복구 | `SettingsReliabilityTests`·`SettingsUiTests` 통과. |
| 공통 확인 대화상자 | `DesignSystemTests` 통과, `sharedDesignDialogsVerified: true`, `dialog-confirm/prompt/error.png` 3장 생성. |
| 타이머 런타임 방어 | `AppRuntime.cs` 무변경. `TimerControlTests` 통과, `timerControlsVerified: true`, 일시정지·정지 후 1.2초 대기 유지 확인. |
| 커스텀 펫 내보내기 계약 | 스모크가 `Animations.Count == 2`와 `click.Loop == false`를 검사하고 통과했다. `SelectNextEmptyAction`의 선택 이동이 내보내기 매핑을 바꾸지 않는다. |
| 다른 워커 변경과의 충돌 | `PetWindow.cs`·`PetPackDiagnostics.cs`·`AnimationLifecycleTests.cs`가 함께 있는 상태에서 167건 전체 통과. 파일 소유권이 겹치지 않는다. |

## macOS 실기(네이티브 렌더러) 관찰

스모크 진단은 화면 밖 창과 프로그래밍 방식 입력을 쓰지만 **실제 macOS 네이티브 렌더러**로 그린다.
아래는 그 캡처에서 직접 읽은 내용이며, 사용자 마우스·키보드 조작 결과가 아니다.

1. **`settings.png` (1120×800, 타이머 진행 중)** — `나의 휴식` 카드에서 기본 루틴 `잠깐의 여유 · 60초`가
   선택된 상태로 **내 루틴 편집** 버튼이 흐리게(비활성) 그려지고 **루틴 · 프로필**만 활성이다.
   UI-01의 "기본 루틴은 편집할 수 없다"가 네이티브 렌더링에서 확인됐다.
   같은 캡처에서 알림 간격 입력도 잠겨 있고 `시간을 바꾸려면 타이머를 일시정지하거나 중지해 주세요.`가 보인다.
2. **`settings-routines-tab.png`** — 라이브러리 `내 루틴` 탭에서 기본 루틴 선택 시 `루틴 편집`·`루틴 삭제`가
   비활성, `루틴 사용`·`새 루틴`이 활성이다. 대시보드와 라이브러리의 판정이 일치한다.
3. **`custom-pet-editor.png` (660×880)** — `쉬는 모습 · 필수 · 반복 / stretch.gif · 480×480 · 18프레임 · 4초`,
   `휴식 안내`는 `파일 없음`, 하단 상태는 `클릭 반응에 파일을 넣었어요.`. UI-03 변경 후에도 배정·라벨·
   생성 버튼 활성이 정상이다. 배정이 끝나 pending 행이 숨겨진 상태다.
4. **관찰(2단계 UI-04 관련, 새 근거)** — 같은 `custom-pet-editor.png`는 **기본 크기 660×880**인데도
   `PageBodyScroll` 세로 스크롤바가 `펫 이름` 입력의 오른쪽 테두리와 `파일 가져오기` 버튼 오른쪽 모서리 위에
   겹쳐 그려진다. 감사 문서 UI-04는 "기본 크기에서는 재현되지 않는다"로 기록돼 있으나, 최소 크기
   전용이 아니라 **커스텀 펫 창은 기본 크기에서도 재현**된다. 2단계 작업 범위 조정이 필요하다.

## 통과

- UI-01: 선택한 사용자 루틴의 id·이름·단계가 편집기로 전달되고, 기본 루틴은 편집 버튼이 비활성이며
  사유가 Tooltip과 접근성 도움말에 모두 실린다. (`SettingsDashboardTests` 2건 + 코드 경계 + `settings.png`)
- UI-02: 타이머 진행 중 프로필 적용이 클릭 전에 비활성이고 사유가 보이며, 일시정지 후 목록 선택을 유지한 채
  다시 적용된다. 독립 `PersonalizationWindow`는 종전대로 동작한다.
  (`SettingsDashboardTests` 1건, `PersonalizationWindowTests` 1건 + 코드 경계)
- UI-03: pending 배정과 슬롯 `파일 선택…` 양쪽이 교체 확인을 거치고, 취소·창 닫기·검증 실패에서 기존 클립과
  `펫 팩 만들기…`·`미리보기` 활성이 보존되며, 성공 후 다음 빈 동작으로 이동하고 전부 차면 선택이 유지된다.
  (`CustomPetTests` 3건 + 기존 1건 수정 + 코드 경계)
- 전체 Release 회귀 167건 통과(실패 0, 건너뜀 0), Desktop Release 빌드 경고 0, `git diff --check` 통과.
- 스모크 진단 `success: true`, PNG 42장, 기존 펫 팩·설정·대화상자·타이머 검사 항목 전부 `true`.

## 실패

없음. 이번 검증에서 재현한 제품 결함은 없다.

## 개선 권고 (출시 차단 아님)

1. **UI-02의 과잉 차단 (경미).** 이미 활성인 프로필을 간격 변화 없이 다시 적용하는 경우
   `AppRuntime.UpdateSettings`는 `reschedulesTimer == false`로 허용하지만, UI는 타이머 진행 중이면
   무조건 비활성이다. 무해한 재적용이 막히는 정도이며, 런타임보다 엄격한 쪽이라 데이터 위험은 없다.
2. **`IdlePaused` 상태의 문구.** `CanEditTimerInterval`은 `Paused || Stopped`만 보므로,
   자리 비움 자동 일시정지(`Clock.IdlePaused`) 중에는 화면이 `자리 비움 · 자동 일시정지`라고 알리는데
   적용 버튼 사유는 `타이머를 일시정지하거나 중지한 뒤…`라고만 말한다. 알림 간격 입력의 기존 동작과
   동일하므로 회귀는 아니지만, 2·3단계에서 문구를 함께 정리할 후보다.
3. **독립 `PersonalizationWindow`의 기본값.** `canApplyProfile` 기본값 `true`는 호환성 장치다.
   현재 이 창의 비테스트 호출자는 `SmokeDiagnostics.cs:73` 하나이고 그 경로는 적용 전에
   `runtime.TogglePause()`를 부르므로 사용자에게 도달하지 않는다. 다만 이 창을 실 런타임에 다시 연결하면
   UI-02 게이트가 조용히 사라지므로 주석 이상의 표시가 필요하다.
4. **UI-04 범위 재조정.** 위 `관찰 4` 참고. 2단계 완료 조건의 "기본 크기" 항목에 660×880 커스텀 펫 창을
   포함해야 한다.
5. **스모크 진단 확장.** 아래 `미검증`의 세 경로를 스모크에 추가하면 Phase 1 동작이 매 회귀마다
   네이티브 렌더러로 검사된다.

## 미검증

### 자동 검사 공백 (이 트리에서 확인)

- **`SettingsEditRoutine`을 스모크가 누르지 않는다.** `SmokeDiagnostics.cs`에 이 버튼 이름이 없다.
  UI-01의 "사용자 루틴 선택 → 그 루틴이 열린다"는 헤드리스 테스트에만 근거한다.
  (기본 루틴의 비활성 상태만 `settings.png`로 확인됨.)
- **인창 프로필 탭의 적용 게이트를 스모크가 누르지 않는다.** `SmokeDiagnostics.cs:73-79`는 **독립**
  `PersonalizationWindow`를 쓰고 `canApplyProfile` 기본값 `true`로 동작하므로, 새 게이트 코드가
  스모크에서 실행되지 않는다. `업무 프로필` 탭의 비활성 상태 캡처도 없다.
- **교체 확인 대화상자가 스모크에서 렌더링되지 않는다.** 스모크의 커스텀 펫 경로는 빈 슬롯만 쓰므로
  `ConfirmReplace`가 대화상자를 띄우지 않는다. 이 대화상자의 네이티브 캡처(긴 파일 이름 줄바꿈 포함)는 없다.
- **설정 창 탭 안에서의 교체 확인.** 소유 창이 SettingsWindow가 되는 경로는 코드로만 확인했고
  실행 근거가 없다. 기존 펫 팩 확인 대화상자와 같은 패턴이라 위험은 낮다.
- **회귀 검사의 결함 검출력.** UI-01·UI-02는 구현 담당이 되돌림 실험으로 3건 실패를 확인한 기록이 있다.
  **UI-03 3건에는 같은 확인 기록이 없고**, 이번 QA는 제품 코드·테스트 수정 금지 범위라 직접 수행하지 않았다.
  단언 내용을 읽어 의도와 일치함은 확인했다.

### 실제 OS 조작

- **실제 macOS 사용자 조작 미검증.** 마우스 클릭·Tab 키로 `내 루틴 편집`, `프로필 적용`, 교체 확인의
  `바꾸기`/`취소`를 누른 결과가 없다. 실제 OS 파일 선택 창과 연결한 흐름도 없다.
- **실제 Windows 전면 미검증.** 이번 세션에 Windows 실행이 없다. 비활성 버튼의 Tooltip 표시, 모달 동작,
  확인 대화상자 폭은 플랫폼마다 다르므로 macOS 결과로 대체하지 않는다.
  `docs/windows-dogfooding-log.md`의 미체크 항목은 그대로 미검증이다.
- **보조 기술 미검증.** VoiceOver·Narrator가 `AutomationProperties.HelpText`로 넣은 세 사유 문구를
  실제로 읽는지 확인하지 않았다. 감사 항목 UI-09는 3단계 범위다.
- **최소 창에서의 `ProfileDetail` 두 줄 문구.** 사유가 줄바꿈으로 붙어 프로필 탭 높이가 한 줄 늘어난다.
  860×680 실기 시각 확인은 없다(스모크는 `업무 프로필` 탭을 캡처하지 않는다).
- **모달 중 알림 겹침.** 교체 확인이 열린 동안 휴식 알림이 발생하는 상황은 실기에서 보지 않았다.
- **장시간 사용·자원·서명·공증.** 이번 검증 범위 밖이다. 스모크의 짧은 표본
  (`workingSetBytes` 147MB, `oneCoreCpuPercent` 70.9)을 성능 통과로 해석하지 않았다.

## 출시 차단 요소

**없음.** Phase 1(UI-01, UI-02, UI-03)은 자동 검사 기준으로 통합 가능하다.

출시 전에는 다음이 남아 있으며, 이는 Phase 1의 결함이 아니라 아직 채우지 않은 검증이다.

1. macOS·Windows 실기에서 세 흐름을 실제 입력으로 1회 조작.
2. 감사 문서 2단계(UI-04~UI-06, UI-12, UI-13)와 3단계(UI-07~UI-11) 미구현.
   특히 UI-04는 위 `관찰 4`로 영향 범위가 넓어졌다.
3. Windows 실기 체크리스트 미실행.

## 다음 검증 순서

1. **Phase 1 스모크 보강 (구현 담당):** `SmokeDiagnostics`에 (a) 사용자 루틴을 선택한 뒤
   `SettingsEditRoutine`을 눌러 편집기 이름이 일치하는지, (b) 인창 `업무 프로필` 탭에서 진행 중 적용 버튼
   비활성과 일시정지 후 재활성, (c) 채워진 동작에 파일을 다시 넣어 확인 대화상자 캡처를 추가한다.
   완료 후 이 QA가 재실행할 수 있도록 `smoke.json` 키 이름을 지정해 달라.
2. **UI-03 회귀 검사 결함 검출력 확인 (구현 담당):** `ConfirmReplace`와 `SelectNextEmptyAction`을
   일시 제거한 상태에서 신규 3건이 실패하는지 확인하고 결과를 보고한다. 제품 코드 수정 권한이 있는
   담당이 수행해야 한다.
3. **macOS 실기 1회 (사람 검수):** `내 루틴 편집`에서 추가 루틴 선택 → 이름 일치 확인,
   타이머 진행 중 프로필 탭 → 사유 확인 → 일시정지 → 적용, 채워진 동작 교체 취소 후 기존 파일 유지.
   860×680 최소 창에서 프로필 탭의 두 줄 문구 넘침도 함께 본다.
4. **UI-04 범위 재산정 (감독자 → UI 담당):** 660×880 기본 크기 재현을 2단계 입력에 반영한다.
5. **2단계 구현 후 재실행할 명령 (재사용):**

```sh
cd /Users/hwanghyeonseong/Documents/GitHub/Unfold
dotnet restore Unfold.slnx
UNFOLD_DATA_DIR="$(mktemp -d)" dotnet test Unfold.slnx -c Release --no-restore     # 기준: 167건 이상 통과, Skipped 0
dotnet build src/Unfold.Desktop -c Release --no-restore                            # 기준: 경고 0
git diff --check                                                                    # 기준: 종료 코드 0
UNFOLD_DATA_DIR="$(mktemp -d)" dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
# 기준: 종료 코드 0, smoke.json success:true, verification/ PNG 42장 이상
```

6. **Windows 실기 (환경 확보 후):** `docs/windows-dogfooding-log.md`에 이 세 흐름 항목을 추가해 기록한다.
