# UI 접근성·반응형 QA

작성: 2026-09-16 · 담당: ui-accessibility-qa · 기준 커밋: `release/mvp` `7e81f77` + 작업 트리
(`CLAUDE.md`, `Tests/Unfold.Tests/AnimationLifecycleTests.cs`, `src/Unfold.Desktop/PetPackDiagnostics.cs`,
`src/Unfold.Desktop/PetWindow.cs` — 다른 작업자의 변경, 무관)
입력: [07_ui_audit_input.md](07_ui_audit_input.md), `/tmp/unfold-ui-audit-20260916b/verification/`의 PNG 42장·`smoke.json`

제품 코드·테스트·기존 문서는 수정하지 않았다. 이 파일만 작성했다.

## 범위와 방법

- `DesignSystem.cs`·`Ui.cs`·`SettingsWindow*.cs`·각 편집/확인 창·`PetWindow.cs`·`PetSpeechBubble.cs`·
  `PersonalizationWindow.cs`·`PetPackWindow.cs`·`CustomPetWindow.cs`·`PetManagementView.cs`·
  `TimerControls.cs`를 직접 읽고 `AutomationProperties`, `SetLabeledBy`, 포커스·비활성 상태 로직을 추적했다.
- `DesignSystemTests.cs`·`SettingsDashboardTests.cs`·`LocalizationTests.cs`·`UiTests.cs`를 읽어 코드 주장과
  헤드리스 테스트 어서션이 실제로 같은 것을 검증하는지 대조했다(재실행이 아니라 소스 대조).
- `settings-minimum.png`, `settings-minimum-scrolled.png`, `custom-pet-minimum.png`,
  `custom-pet-minimum-scrolled.png`, `settings-pet-create-minimum.png`,
  `settings-pet-create-minimum-scrolled.png`, `settings-pet-open-minimum.png`, `dialog-*.png`,
  `speech-*.png`, `pack-error.png`를 `sips`로 픽셀 크기를 확인한 뒤 Read 도구로 시각 검사했다.
- 대비는 `DesignSystem.cs`의 실제 hex 토큰으로 WCAG 상대휘도 공식을 직접 계산해 코드-수치를 교차 검증했다
  (아래 "대비" 절). 디스플레이 감마·색상 프로필은 반영하지 않으므로 자동 계산으로 한정한다.
- `smoke.json`은 오프스크린 자동 진단 산출물이다. `settingsLayout.noHorizontalOverflow`·
  `detailsScrollVerified` 같은 필드는 그 진단이 자체로 주장하는 값이며, 이 보고서는 그 주장을 코드·캡처와
  대조한 뒤에만 통과로 인정했다(아래 표에 출처 구분).

## 화면별 판정 요약

| 화면 | 키보드 | 레이블 | 대비 | 최소 창·스크롤 |
|---|---|---|---|---|
| 설정 대시보드(기본/최소/스크롤) | 부분 실패 — P1-1 | 통과 | 통과 | 통과 |
| 알림 설정 카드 | 부분 실패 — P1-1 | 통과 | 통과 | 통과(카드 내 스크롤 없음) |
| 말풍선 알림 카드 | 부분 실패 — P1-1 | 통과 | 통과 | 통과 |
| 나의 휴식 카드 · 루틴·프로필 라이브러리 | 부분 실패 — P1-1, P2-3 | 부분 실패 — P2-5 | 통과 | 통과 |
| 기록·내보내기 | 통과(버튼) | 부분 실패 — P2-6 | 통과 | 통과 |
| 펫 추가 · 펫 팩 열기 | 부분 실패 — P1-1 | 부분 실패 — P2-3 | 통과, 단 오류 색상 미적용 — P2-4 | 통과 |
| 펫 추가 · 펫 팩 만들기 | 부분 실패 — P1-1 | 부분 실패 — P2-3 | 통과 | 통과 |
| 내 루틴 편집 / 업무 프로필 편집 | 부분 실패 — P1-1 | 통과 | 통과 | 통과(고정 크기, `CanResize=false`) |
| 확인·이름 입력·오류 대화상자 | 통과(버튼 포커스·기본/취소 규칙) | 통과 | 통과 | 통과 |
| 펫 말풍선(4방향·상태) | **실패 — P1-2** | 통과(전체 이름), 부분 — P2-7 | 통과 | 해당 없음(고정 크기) |

"통과"는 이 세션에서 코드·테스트·캡처로 직접 확인한 항목이고, "미검증"은 별도 절에 모았다.

## P0 — 없음

이번 감사에서 핵심 작업이 완전히 도달 불가능하거나 크래시하는 P0급 결함은 찾지 못했다.

## P1 — 우선 수정 필요

### P1-1. TextBox·NumericUpDown·ComboBox에 키보드 포커스 시각 표시가 없다

- **재현 상태(코드 근거):** `src/Unfold.Desktop/DesignSystem.cs`의
  `foreach (var type in new[] { typeof(TextBox), typeof(NumericUpDown), typeof(ComboBox) })` 블록이
  `Control.FocusAdornerProperty`를 `null`로 지워 기본 포커스 사각형을 없앤다. 같은 블록 아래
  `":pointerover", ":focus", ":focus-within", ":disabled"` 네 상태 모두 동일한
  `Background=Shell, BorderBrush=Muted, BorderThickness=1`을 설정해, 포커스 상태와 평상시 상태가
  픽셀 단위로 같다. `Button`류는 별도로 `:focus-visible`에 `BorderBrush=Cream`(또는 `primary`는 `Ink`)을
  지정해 포커스가 보이지만, 이 세 컨트롤 타입에는 같은 처리가 없다.
- **테스트로 이 동작이 의도된 것임을 확인:**
  `Tests/Unfold.Tests/DesignSystemTests.cs`의 `InputsKeepTheirAppearanceWhileHoveredAndEdited`가
  `editor.Focus(NavigationMethod.Tab); ... Assert.Equal(original, (editor.Background, editor.BorderBrush, ...))`,
  `choice.Focus(NavigationMethod.Tab); ... Assert.Equal(choiceAppearance, (...))`로 포커스 전후 외형이
  같아야 한다고 **명시적으로 단언**한다. 즉 회귀가 아니라 설계된 동작이며, `docs/design-system.md`의
  "텍스트·숫자·선택 입력 필드는 호버와 포커스 때 배경·테두리 색과 두께가 바뀌지 않는다"와 일치한다.
  그러나 같은 문서 문장 "입력 포커스와 키보드 조작은 유지하며, 텍스트 선택과 커서는 그대로 표시한다"는
  `TextBox`에는 캐럿으로 부분 보완되지만 `ComboBox`·`NumericUpDown`에는 캐럿이 없어 보완되지 않는다.
- **영향 범위:** 알림 간격/자리 비움(`ReminderInterval`, `ReminderIdle`), 캐릭터 선택(`CharacterPicker`),
  루틴 선택(`RoutinePicker`), 말풍선 위치(`BubbleDirection`)·다시 알릴 시간(`SnoozeMinutes`), 루틴 이름과
  3단계 안내·시간(`RoutineName`, `Step1-3`, `Seconds1-3`), 프로필 이름·간격·자리 비움·루틴
  (`ProfileName`, `ProfileInterval`, `ProfileIdle`, `ProfileRoutine`), 커스텀 펫 이름·동작 선택
  (`CustomPetName`, `CustomPetAction`), 팩 미리보기 배경·크기·동작(`PackBackground`, `PackSize`, `PackClip`)
  등 앱의 거의 모든 입력 필드.
- **소유 파일:** `src/Unfold.Desktop/DesignSystem.cs` (해당 스타일 블록), 필요하면
  `Tests/Unfold.Tests/DesignSystemTests.cs`(위 어서션 갱신).
- **자동 검사로 확인 가능한 것:** 헤드리스 테스트로 `Focus(NavigationMethod.Tab)` 전후
  `BorderBrush`/`Background`/포커스 어도너 유무를 비교하는 새 어서션 추가, 또는 기존
  `InputsKeepTheirAppearanceWhileHoveredAndEdited`의 의도를 뒤집는 리뷰.
- **실제 OS 검증이 필요한 것:** macOS·Windows에서 Tab만으로 설정 대시보드를 순회하며 현재 포커스가
  어떤 필드에 있는지 시각적으로 식별 가능한지 확인. 특히 ComboBox·NumericUpDown은 캐럿도 없어
  실기에서 "포커스가 어디 있는지 전혀 안 보임"으로 나타날 가능성이 높다 — 이 세션은 헤드리스 코드 대조만
  했고 실제 렌더링·실제 키보드 입력은 미검증이다.

### P1-2. 펫 스트레칭 말풍선(휴식 시작/5분 뒤에/완료)이 키보드만으로 도달 불가능할 수 있다

- **재현 상태(코드 근거):** `src/Unfold.Desktop/PetWindow.cs:40`에서
  `ShowInTaskbar = false; Topmost = true; ShowActivated = false;`로 창을 생성한다.
  `ShowPet()`(`PetWindow.cs`)은 `Show(); RefreshSpeech(); hitTimer.Start(); animation.SetRunning(true);`만
  호출하며 `Activate()`를 호출하지 않는다. `grep -n "Activate()" src/Unfold.Desktop/PetWindow.cs`는
  0건이다. 대조로 `AppRuntime.cs:133`의 `ShowSettings()`는
  `if (!DiagnosticMode) settingsWindow.Activate();`로 명시적으로 활성화하고, `AppRuntime.cs:188,205`의
  에디터 창도 `.Activate()`를 호출한다 — 설정·에디터 창과 달리 펫 창만 활성화 호출이 없다.
  전역 단축키도 없다: `grep -rn "GlobalHotkey\|RegisterHotKey\|KeyBinding\|KeyGesture"
  src/Unfold.Desktop/`는 0건.
- **결과적으로 발생 가능한 문제:** 알림이 떠서 말풍선이 펼쳐져도(`RefreshPetNotice` → `ShowPet`)
  OS 포커스는 사용자가 작업 중이던 다른 창에 남는다. 키보드만 쓰는 사용자는 마우스로 펫을 클릭해
  창을 활성화하지 않는 한 Tab으로 `PetBreakStart`/`PetBreakSnooze`/`PetBreakComplete` 버튼에
  진입할 방법이 코드상 보이지 않는다. 이는 `docs/mvp.md`·`docs/stretch-notifications.md`가 전제하는
  "알림 → 휴식 시작/완료" 핵심 흐름의 키보드 경로가 막혀 있을 수 있다는 뜻이다.
- **소유 파일:** `src/Unfold.Desktop/PetWindow.cs`(`ShowPet`, 생성자의 `ShowActivated`),
  `src/Unfold.Desktop/AppRuntime.cs`(`RefreshPetNotice`가 알림 발생 시점을 안다).
- **자동 검사로 확인 가능한 것:** 헤드리스 테스트로 `ShowActivated`가 언제 어떻게 바뀌는지, 또는
  알림 발생 시 `Activate()`/`Focus()` 호출이 실제로 실행되는지 스파이로 확인하는 테스트 추가.
  현재 테스트 스위트에 이 경로를 검증하는 테스트는 없다 — `grep -rn "PetSpeechBubble\|PetBreakStart"
  Tests/Unfold.Tests/`로 확인한 결과 헤드리스 UI 테스트에서 버튼 존재나 `Refresh()` 출력만 검증하고
  실제 창 활성화·포커스 이동은 다루지 않는다.
- **실제 OS 검증이 필요한 것(필수, 미룰 수 없음):** macOS·Windows 실기에서 다른 앱에 포커스가 있는
  상태로 알림을 띄운 뒤 Tab 키만으로 펫 창에 진입할 수 있는지 확인. 아이콘 클릭 없이 Narrator/VoiceOver가
  알림 자체를 announce하는지도 확인. 이 항목은 코드 근거만으로 "실패"를 확정하기보다 "설계상 위험 —
  실기 확인 시급"으로 표시하며, 이번 세션은 실기 OS를 실행하지 않아 최종 확정은 유보한다.

## P2 — 개선 권장

### P2-3. 비활성 버튼의 사유가 스크린리더와 프로그램적으로 연결되지 않는다

- **재현 상태:** `src/Unfold.Desktop/PetPackWindow.cs`의 `install`(`InstallPetPack`) 버튼은
  `pack`이 로드되기 전 `IsEnabled = false`로 시작하고(`:51`), 사유는 근처 `status` `TextBlock`의
  문장("펫 팩을 선택해 주세요" 등)으로만 전달된다. `Ui.Field`가 쓰는 `AutomationProperties.SetLabeledBy`
  패턴과 달리, 이 상태 텍스트는 `AutomationProperties.SetLabeledBy`나 `SetHelpText`로 버튼과 연결되어
  있지 않다(`grep -n "SetLabeledBy\|SetHelpText" src/Unfold.Desktop/PetPackWindow.cs` 0건).
  `src/Unfold.Desktop/CustomPetWindow.cs`의 `create`(`CreateCustomPetPack`, `:189`)도 동일 패턴:
  `IsEnabled`는 이름과 `idle` 클립 존재 여부로 계산되지만 비활성 사유가 버튼에 연결되지 않는다.
  `src/Unfold.Desktop/PersonalizationWindow.cs:135-138`의 `editRoutine`/`deleteRoutine`/`useRoutine`/
  `editProfile`/`deleteProfile`/`applyProfile`도 선택 유무로만 `IsEnabled`가 바뀌고 사유 연결이 없다.
- **영향:** 스크린리더 사용자가 비활성 버튼에 포커스했을 때 "비활성"만 듣고 왜 그런지, 무엇을 하면
  풀리는지 알 수 없다. 시각 사용자에게는 인접 텍스트로 보이지만 이는 시각적 근접성일 뿐 접근성 트리
  연결이 아니다.
- **소유 파일:** `src/Unfold.Desktop/PetPackWindow.cs`, `src/Unfold.Desktop/CustomPetWindow.cs`,
  `src/Unfold.Desktop/PersonalizationWindow.cs`.
- **자동 검사:** 헤드리스 테스트로 `AutomationProperties.GetHelpText(button)`이 null이 아님을 단언.
- **실제 OS 검증:** VoiceOver/Narrator로 비활성 버튼에 포커스했을 때 사유가 낭독되는지 확인 — 미검증.

### P2-4. `PetPackWindow`의 오류 상태 텍스트가 `Error` 색으로 바뀌지 않는다

- **재현 상태:** `PetPackWindow.cs`의 실패 처리 3곳
  (`:144 status.Text = "팩을 열지 못했어요. " + Ui.ErrorText(error);`,
  `:163 status.Text = "미리보기를 재생하지 못했어요. " + Ui.ErrorText(error);`,
  `InstallPack` 실패 분기)는 `status.Foreground`를 재설정하지 않는다. 생성자에서
  `status.Foreground = DesignSystem.Muted`로 한 번 설정된 뒤 그대로 남는다.
  대조로 `CustomPetWindow.cs`의 `ShowError`는 `status.Foreground = DesignSystem.Error;`를 명시적으로
  설정한다. `pack-error.png` 캡처에서도 "팩을 열지 못했어요…" 문장이 다른 안내 문구와 같은 옅은
  회녹색(Muted)으로 보이고 코랄색(Error)으로 강조되지 않는다 — 코드와 캡처가 일치한다.
- **판정 근거:** 오류 문구 자체("못했어요")가 실패를 텍스트로 전달하므로 순수한 "색상 단독 상태 전달"
  위반은 아니다. 다만 `docs/design-system.md`의 색상 표("오류 `Error` `#FFB4A3` 저장 오류, 삭제 동작")와
  앱 다른 곳의 실제 동작(예: `reminderSettingsStatus.Foreground = DesignSystem.Error`,
  `CustomPetWindow.ShowError`)에 비해 이 창만 시각적 긴급도가 낮아, 저시력 사용자가 실패를 빠르게
  알아채기 어렵다.
- **소유 파일:** `src/Unfold.Desktop/PetPackWindow.cs`.
- **자동 검사:** 실패 경로 실행 후 `status.Foreground == DesignSystem.Error`를 단언하는 헤드리스 테스트.
- **실제 OS 검증:** 불필요(정적 색상 값 문제, 코드 대조로 충분히 확정).

### P2-5. `PersonalizationTabs`에 `AutomationProperties.SetName`이 없다

- **재현 상태:** `PersonalizationWindow.cs`의
  `var tabs = new TabControl { Name = "PersonalizationTabs", ItemsSource = ... }`에는
  `AutomationProperties.SetName` 호출이 없다. 대조로 `PetManagementView.cs`의 동일 패턴
  `tabs.ItemsSource = ...; AutomationProperties.SetName(tabs, "펫 추가 방식");`는 이름을 명시한다.
  같은 앱 안에서 탭 컨트롤 접근성 이름 부여 방식이 일관되지 않는다.
- **소유 파일:** `src/Unfold.Desktop/PersonalizationWindow.cs`.
- **자동 검사:** `AutomationProperties.GetName(tabs)`가 null이 아님을 단언.
- **실제 OS 검증:** VoiceOver/Narrator가 탭 그룹 진입 시 "내 루틴, 업무 프로필" 탭 목록을 어떤 이름으로
  소개하는지 확인 — 개별 `TabItem.Header`만으로 충분할 수도 있어 실기 확인 전까지는 미검증.

### P2-6. `BreakReviewView`의 날짜·횟수·시간이 표 시맨틱 없이 개별 텍스트로만 나열된다

- **재현 상태:** `BreakReviewWindow.cs`의 `Refresh()`가 `days`(`Grid`, `ColumnDefinitions="*,100,110"`)에
  각 행마다 `Ui.Text(day.Date...)`, `Ui.Text($"{day.Count}회")`, `Ui.Text($"{day.Seconds/60}분...")`
  세 개의 독립된 `TextBlock`을 추가할 뿐, `AutomationProperties.SetLabeledBy`나 그룹핑 컨테이너로
  묶지 않는다. `Tests/Unfold.Tests/LocalizationTests.cs`의
  `ReviewUsesKoreanWeekdaysOnAnEnglishSystemAndPreservesOldExportNames`도 `monday.Bounds.Right <=
  count.Bounds.X`처럼 **시각적 위치**만 검증하고 접근성 트리 연관은 검증하지 않는다.
- **영향:** 스크린리더가 셀 단위로 탐색할 때 "9월 14일 (월)", "1회", "0분 20초"가 어느 요일의 값인지
  프로그램적으로 연결되지 않을 수 있다.
- **소유 파일:** `src/Unfold.Desktop/BreakReviewWindow.cs`.
- **자동 검사:** 어렵다(레이아웃 시맨틱 부재 자체가 핵심 결함이라 헤드리스로는 "존재하지 않음"만 확인 가능).
- **실제 OS 검증:** VoiceOver/Narrator로 기록 표를 탐색했을 때 행 단위로 묶여 들리는지 확인 — 미검증.

### P2-7. `PetWindow`의 중앙 캐릭터(`AnimationView`)에 접근성 이름이 없다

- **재현 상태:** `PetWindow.cs`의 `animation` 필드(`AnimationView`)는 드래그·클릭(스트레칭 반응 트리거)의
  주 대상이지만 `AutomationProperties.SetName`이 호출되지 않는다
  (`grep -n "AutomationProperties" src/Unfold.Desktop/PetWindow.cs` → 0건, `PetWindow.cs` 전체에서
  접근성 이름 관련 호출이 없음). `PetSpeechBubble` 자체에는 `AutomationProperties.SetName(this, "펫의
  스트레칭 알림")`가 있어(`PetSpeechBubble.cs`) 말풍선은 이름을 갖지만, 항상 보이는 펫 본체는 이름이 없다.
- **소유 파일:** `src/Unfold.Desktop/PetWindow.cs`.
- **자동 검사:** `AutomationProperties.GetName(animation)` 단언 추가.
- **실제 OS 검증:** 낮은 우선순위 — 펫 본체는 주로 마우스 상호작용 대상이라 실기 확인 없이도 코드 수정만으로
  해소 가능.

## 통과로 확정한 항목(코드·테스트·캡처로 직접 확인)

- **레이블→입력 연결:** `Ui.Field`가 `AutomationProperties.SetLabeledBy(input, caption)`을 항상 실행하며
  (`Ui.cs`), `SettingsWindow.Layout.cs`의 `interval`/`idle`/`routines`/`characters`,
  `RoutineEditorWindow`의 이름·3단계 안내·시간, `ProfileEditorWindow`의 전 필드,
  `CustomPetWindow`의 이름·동작·각 슬롯 버튼, `PetPackWindow`의 배경·크기·재생 동작에 개별
  `AutomationProperties.SetName`이 일관되게 붙어 있다(각 파일에서 직접 확인).
- **대비:** `DesignSystem.cs` 토큰으로 계산한 WCAG 상대휘도 대비는 모두 4.5:1을 크게 상회한다 —
  Cream/Ink 14.0:1, Cream/Raised 8.79:1, Muted/Canvas 9.45:1, Muted/Shell 8.60:1, Muted/Raised 5.93:1,
  Error/Surface 7.98:1, Error/Shell 9.64:1, Warning/Surface 8.23:1, Warning/Shell 9.95:1(이번 세션의
  Python 계산, `DesignSystemTests.cs`의 `Contrast()` 헬퍼와 같은 공식). `DesignSystemTests.cs`의
  `ButtonStatesKeepPrimaryTextLegibleAndExposeKeyboardFocus`가 Cream/Ink≥7, Muted/Surface≥4.5를
  직접 단언해 코드-테스트가 일치한다. (`Outline`(경계선, `#444B40`) 대 `Shell`(`#1D201D`)은 1.82:1로
  낮지만 이는 장식적 프레임 테두리이며 WCAG 1.4.11의 "식별에 필수적인 UI 경계"에 해당하는 입력 필드
  테두리는 `Muted` 기준으로 8.60:1이라 별도 결함으로 분류하지 않았다.)
- **색상 단독 상태 전달:** 타이머 상태 배지(`timerStateDot`+`state` 텍스트, `SettingsWindow.Layout.cs`의
  `Refresh()`)는 항상 색과 문장을 함께 바꾼다. `PetSpeechBubble.Refresh()`의 `hint`/`timer`도 마찬가지로
  색상 변화에 텍스트 변화가 동반된다. 예외는 위 P2-4(오류 시 색상 자체가 변하지 않는 것으로, 반대 방향의
  문제).
- **최소 창·스크롤·잘림·가로 넘침:** `settings-minimum.png`/`settings-minimum-scrolled.png`(860×680),
  `custom-pet-minimum.png`/`custom-pet-minimum-scrolled.png`(520×620),
  `settings-pet-create-minimum.png`/`-scrolled.png`, `settings-pet-open-minimum.png`을 직접 열어
  확인한 결과 가로 스크롤바나 잘린 버튼이 보이지 않으며, 하단 고정 액션 버튼("적용", "펫 팩 만들기…")이
  항상 창 안에 있다. `SettingsDashboardTests.DashboardKeepsThePetAndTimerVisibleWhileOnlyDetailsScroll`이
  860×680을 포함한 3개 크기에서 `SettingsCompanionCard`·`SettingsTimerCard`·`TimerToggle`·
  `TimerStop`·`ApplyReminderSettings`·`SettingsQuit`·`LaunchAtLogin`의 경계가 창 안에 있음을 픽셀
  단위로 단언하고, `DesignSystemTests.PageActionsRemainVisibleAtMinimumSizeAndWhileTheBodyScrolls`가
  `RoutineEditorWindow`·`ProfileEditorWindow`·`PersonalizationWindow`·`BreakReviewWindow`·
  `PetPackWindow`에 대해 같은 패턴을 검증한다. `smoke.json`의
  `settingsLayout.noHorizontalOverflow: true`, `detailsScrollVerified: true`,
  `minimumWidth: 860, minimumHeight: 680`은 이 코드·캡처 증거와 일치하므로 그 범위에서만 신뢰했다
  (자동 진단 자체의 주장을 단독 근거로 쓰지 않았다).
- **확인/취소 규칙:** `Ui.Confirm`(`Ui.cs`)이 취소 버튼에 먼저 포커스하고 `IsDefault`를 삭제 버튼에
  주지 않는 규칙은 `DesignSystemTests.NestedEditorsAndDeleteConfirmationShareTheThemeAndCancelSafely`가
  `Assert.True(Button(confirm, "취소").IsFocused); Assert.False(Button(confirm, "삭제").IsDefault);`로
  직접 검증하고, `dialog-confirm.png` 캡처도 이와 일치하는 레이아웃을 보인다.

## 미검증 — 실제 OS·보조 기술 확인 필요

이 세션은 macOS/Windows 실기를 구동하지 않았고 VoiceOver·Narrator·Windows 고DPI를 실행하지 않았다.
`/tmp/unfold-ui-audit-20260916b/verification/`의 PNG는 오프스크린 자동 캡처이며 실제 마우스·키보드·
스크린리더 증거로 해석하지 않았다. 아래는 코드 근거만으로는 확정할 수 없어 실기 확인이 필요한 항목이다.

- P1-2(펫 말풍선 키보드 도달성)의 최종 확정 — 다른 앱에 포커스가 있는 상태에서 알림 발생 시 실제로
  Tab이 펫 창에 진입하는지, OS가 어떤 형태로든 알림을 활성화 없이도 announce하는지.
  또한 실기에서만 확인 가능한 `docs/verification.md` "실제 OS 확인" 항목의 조작 경로와 겹친다.
- P1-1의 체감 심각도 — 실제 디스플레이에서 Tab 이동 시 포커스 위치를 전혀 알 수 없는지, 아니면 OS
  수준의 다른 표시(예: 창 관리자 하이라이트)가 보완하는지.
- Windows 고DPI 배율(125%/150%/200%)에서 860×680 최소 크기와 버튼 히트박스가 동일하게 유지되는지.
- 실제 VoiceOver rotor로 설정 대시보드를 순회했을 때 `AutomationProperties.SetLabeledBy`로 연결된
  레이블이 필드 이름과 함께 낭독되는지(코드상 연결은 확인했으나 실제 AX 트리 반영은 플랫폼 종속적).
- 실제 다중 모니터·모니터 분리 시 펫 창(`PetWindow.RefreshSpeech`의 `Screens.ScreenFromPoint` 로직)의
  키보드 포커스·탭 순서 유지 여부.

체크되지 않은 위 항목은 미검증이며, 다른 브랜치·OS·과거 버전의 통과를 이 보고서에 복사하지 않았다.

## 팀 통신

- P1-1(입력 포커스 표시 부재)과 P2-4(오류 색상 미적용)는 `DesignSystem.cs`·공통 컴포넌트 문제이므로
  ui-visual-auditor와 공유가 필요하다.
- P1-2(펫 말풍선 키보드 도달성)는 핵심 작업(휴식 시작/완료)이 막힐 수 있는 항목이라 감독자에게 즉시
  보고 대상이다.
