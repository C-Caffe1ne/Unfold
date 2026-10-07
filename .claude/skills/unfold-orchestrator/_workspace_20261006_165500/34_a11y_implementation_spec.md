# 3단계 접근성 구현 사양 — UI-07~UI-11

- 작성: 2026-09-17 · 워커 D(a11y-spec)
- 기준: `release/mvp`, 기준 입력 `_workspace/30_ui_redesign_input.md:1-50`, 현재 공유 작업 트리
- 범위: 구현 사양·수용 기준 확정만 수행했다. `src/`와 `Tests/`는 수정하지 않았다.
- 우선순위: UI-07·UI-08은 P1, UI-09·UI-10·UI-11은 P2다(`_workspace/10_ui_accessibility_qa.md:48-187`).

## 판정 경계

- 현재 앱은 `Avalonia.Desktop`·`Avalonia.Themes.Fluent`를 직접 참조하는 C# 데스크톱 앱이다
  (`src/Unfold.Desktop/Unfold.Desktop.csproj:1-15`). 아래 사양은 현재 체크아웃만을 기준으로 한다.
- `/tmp/unfold-phase2-smoke-final.j5Uen8/verification/`의 `pack-error.png`, `weekly-review.png`,
  `speech-top.png`, `routine-library.png`를 코드와 대조했다. 이 PNG는 off-screen 자동 캡처이므로 실제
  키보드·VoiceOver·Narrator의 증거가 아니다(`docs/verification.md:81-91`).
- 자동 검사는 접근성 속성, 상태 계약, 포커스 장식 템플릿과 smoke 산출물을 확인한다. 실제 보조 기술의
  탐색 순서·낭독 문장·OS 포커스 전환은 macOS와 Windows 실기 수용 기준을 별도로 통과해야 한다
  (`docs/verification.md:128-142`).
- 워커 A의 `31_design_system_redesign.md`는 이 문서 작성 시점에 아직 없었다. UI-07 토큰은 아래의
  **임시 이름** `FocusRing`, `FocusRingThickness`, `FocusRingInset`으로 확정하되, 2차 구현 시작 전에
  워커 A가 정의한 동등 토큰이 있으면 이름만 맞춘다. 값과 동작 계약을 중복 정의하지 않는다.

## 공통 완료 기준과 검증 명령

구현 완료는 다음 세 층을 모두 만족할 때로 한정한다.

1. 항목별 신규 headless 테스트와 기존 회귀 테스트가 통과한다.
2. 새 빈 `UNFOLD_DATA_DIR`로 smoke를 실행하고 `smoke.json`의 `accessibility` 하위 다섯 키가 모두
   `true`다. smoke는 속성·상태와 off-screen 렌더만 증명한다.
3. UI-07·UI-08·UI-09·UI-11의 실제 OS 잔여 체크를 macOS와 Windows에서 각각 기록한다. UI-10은
   정적 색상 계약이 핵심이지만 최종 화면 회귀는 같은 실기 세션에서 함께 확인한다.

```sh
dotnet restore Unfold.slnx --locked-mode
dotnet test Unfold.slnx -c Release --no-restore
profile_dir=$(mktemp -d /tmp/unfold-a11y.XXXXXX)
UNFOLD_DATA_DIR="$profile_dir" dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
jq '.accessibility' "$profile_dir/verification/smoke.json"
```

진단 프로필 격리는 `docs/verification.md:69-91`의 계약을 따른다. 실제 구현에서 새 근거 없이 이미 통과한
전체 검사를 반복하지 않고, 먼저 아래 항목별 필터를 실행한 뒤 마지막에 전체 스위트를 한 번 실행한다.

## UI-07 — 입력 필드의 키보드 포커스 표시(P1)

### 현재 코드 근거와 결정

- `TextBox`, `NumericUpDown`, `ComboBox` 공통 스타일은 `FocusAdorner=null`을 지정한다
  (`src/Unfold.Desktop/DesignSystem.cs:74-84`). `:pointerover`, `:focus`, `:focus-within`, `:disabled`도
  모두 `Shell` 배경, `Muted` 테두리, 1px 두께로 고정한다(`DesignSystem.cs:85-91`).
- 템플릿 내부 TextBox·ComboBox 테두리도 모든 상태에서 같은 값이고
  (`DesignSystem.cs:99-120`), `NumericUpDown`의 내부 TextBox는 포커스 장식까지 다시 `null`로 지운다
  (`DesignSystem.cs:121-147`). 이는 기존의 “호버·포커스 때 배경·테두리 색과 두께를 바꾸지 않는다”는
  요구(`docs/design-system.md:50-57`)와 기존 테스트의 포커스 전후 동일 외형 단언
  (`Tests/Unfold.Tests/DesignSystemTests.cs:263-325`)에 맞지만, 포커스 위치를 별도로 표시하지 않는다.
- `input-focus-number.png`, `input-hover-choice.png`, `input-default.png`
  (`docs/validation/images/2026-09-14-input-fields/`)도 외곽 표면이 같은 모습이다. 숫자 입력의 선택된 값이나
  TextBox 캐럿에 의존하지 않고 세 종류 모두에서 동일한 키보드 포커스 신호를 제공해야 한다.

**결정:** 입력 자체의 `Background`, `BorderBrush`, `BorderThickness`와 템플릿 내부 동일 속성은 현재
값을 그대로 둔다. 대신 해당 컨트롤의 `FocusAdorner`에 **컨트롤 안쪽 2px 포커스 링**을 그리는 공통
`FocusAdornerTemplate`을 지정한다. 포커스 링은 입력 표면과 별개의 장식 `Border`이므로 기존 표면 토큰을
변경하지 않으며, 포인터 포커스가 아니라 `NavigationMethod.Tab`/키보드 포커스에서만 나타나는 Avalonia의
포커스 장식 경로를 사용한다. 외곽 음수 margin은 Avalonia에서 잘릴 위험이 있으므로 쓰지 않는다.

### 제안 구현

- **소유 파일:** `src/Unfold.Desktop/DesignSystem.cs`,
  `Tests/Unfold.Tests/DesignSystemTests.cs`, `src/Unfold.Desktop/SmokeDiagnostics.cs`.
- **토큰/의존성:** 워커 A 토큰이 없으면 `FocusRing = Cream`, `FocusRingThickness = 2d`,
  `FocusRingInset = 2d`를 `DesignSystem`에 추가한다. `ControlRadius`를 장식의 곡률로 재사용한다. 워커 A의
  동등 토큰이 생기면 그 이름과 값이 단일 출처다.
- `FuncControlTemplate<Control>` 하나를 만들어 hit-test 불가 `Border`에 위 토큰을 적용하고, 현재
  `FocusAdorner=null` 두 곳을 이 템플릿으로 교체한다. `NumericUpDown`은 실제 키보드 포커스를 받는
  `PART_TextBox`에도 같은 장식을 적용해 값 영역에 링이 보이게 한다. 외곽 NumericUpDown과 화살표의
  배경·테두리·곡률 규칙(`DesignSystem.cs:149-180`)은 건드리지 않는다.
- 마우스 클릭 후에도 링이 남는다면 `:focus-visible` 조건과 같은 결과가 되도록 포커스 장식 표시 조건을
  좁힌다. 이 판정은 headless 속성만으로 끝내지 않고 실제 Tab/클릭 비교로 확정한다.

### 추가 테스트와 단언

- `DesignSystemTests.InputsExposeKeyboardFocusWithoutMutatingTheirSurface`
  - TextBox, NumericUpDown의 `PART_TextBox`, ComboBox를 각각 `Focus(NavigationMethod.Tab)` 한다.
  - 각 컨트롤의 포커스 장식이 null이 아니며, 생성된 장식의 brush/thickness/inset이 포커스 토큰과 같다.
  - 포커스 전후 입력과 템플릿 `Border`의 Background/BorderBrush/BorderThickness가 모두 동일함을 유지한다.
  - 마우스 포인터 이동 뒤에도 기존 표면 속성이 그대로임을 유지한다.
- 기존 `InputsKeepTheirAppearanceWhileHoveredAndEdited`(`DesignSystemTests.cs:263-345`)는 삭제하지 않는다.
  중복 단언은 위 테스트로 옮기더라도 숫자 화살표 배치·곡률·투명 내부 표면 단언은 보존한다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~DesignSystemTests.InputsExposeKeyboardFocusWithoutMutatingTheirSurface|FullyQualifiedName~DesignSystemTests.InputsKeepTheirAppearanceWhileHoveredAndEdited'
```

- **smoke.json 키:** `accessibility.inputFocusIndicatorVerified`.
  SmokeDiagnostics는 TextBox·NumericUpDown·ComboBox를 차례로 Tab 포커스하고, 장식 토큰과 표면 불변을
  단언한 뒤 `input-focus-text.png`, `input-focus-number.png`, `input-focus-choice.png`를 남긴다.
- **수용 기준:** 위 테스트/키가 통과하고, 세 PNG에서 링이 컨트롤 안에 잘리지 않게 보이며, 기존 표면
  색·1px 테두리·크기는 포커스 전후 동일하다.
- **실제 OS 잔여:** macOS·Windows에서 Tab/Shift+Tab만으로 세 입력을 순회해 현재 위치를 식별할 수 있는지,
  마우스 클릭에는 불필요한 링이 나타나지 않는지, 100%·150%·200% 배율에서 2px 링이 잘리거나 흐려지지
  않는지 확인한다. VoiceOver·Narrator 포커스 커서와 시각 링의 대상이 같은지도 미검증이다.

## UI-08 — 비활성 펫 창에서 휴식 동작으로 가는 키보드 경로(P1, 설계 위험)

### 현재 경로 추적

1. `Tick()`이 휴식 시점에 `ShowReminder()`를 호출한다(`src/Unfold.Desktop/AppRuntime.cs:148-157`).
2. `ShowReminder()`는 세션을 초대하고 펫을 갱신한 뒤 애니메이션·말풍선 상태를 갱신한다
   (`AppRuntime.cs:262-284`).
3. `UpdatePet()`/`RefreshPetNotice()`는 `PetWindow.ShowPet()`만 호출한다
   (`AppRuntime.cs:216-225`, `313-320`). `ShowPet()`도 `Show`, 말풍선 갱신, 타이머·애니메이션 시작만 한다
   (`src/Unfold.Desktop/PetWindow.cs:126-128`).
4. 펫 창은 `ShowInTaskbar=false`, `Topmost=true`, `ShowActivated=false`다(`PetWindow.cs:53-59`). 따라서
   알림 때 다른 앱의 포커스를 빼앗지 않는 현재 정책과 일치한다.
5. 말풍선의 시작·미루기·완료 버튼은 각각 `runtime.StartBreak`, `SnoozeBreak`, `CompleteBreak`에 직접
   연결된다(`PetWindow.cs:59`, `src/Unfold.Desktop/PetSpeechBubble.cs:18-38`). 그러나 다른 앱에 포커스가
   남은 상태에서 이 버튼으로 들어가는 키보드 경로는 현재 코드에 없다.
6. 반면 앱에는 이미 `TrayIcon`+`NativeMenu`가 있고(`AppRuntime.cs:345-366`), 휴식 상태와 접힌 말풍선
   복구 항목도 한 계약으로 갱신된다(`AppRuntime.cs:14-39`, `160-173`).

### 대안 평가와 확정안

| 방식 | macOS·Windows 가능성 | 현재 정책/위험 | 결정 |
|---|---|---|---|
| OS 전역 단축키 | Avalonia `KeyBinding`은 포커스된 창/자식 범위이며, 현재 프로젝트에는 전역 등록 의존성·코드가 없다(`Unfold.Desktop.csproj:11-15`, `PetWindow.cs:177-191`의 Win32 interop도 클릭 통과 전용). Windows `RegisterHotKey`와 macOS 별도 구현·권한·충돌 처리가 필요하다. | 새 플랫폼 계층과 단축키 충돌/권한 UX가 생긴다. 양 OS 공통이라고 코드만으로 주장할 수 없다. | 3단계에서 제외. |
| 알림 발생 시 `Activate()`+버튼 `Focus()` | Avalonia API는 있고 설정 창에서 실제 사용 중이다(`AppRuntime.cs:178`, `227-254`). | 알림 때 활성화하지 않는 명시적 정책과 충돌하고 사용자의 입력을 빼앗는다. borderless/Topmost 펫의 실제 AX 포커스는 플랫폼 종속이다. | 자동 활성화 금지. |
| 기존 트레이 `NativeMenu`에 동작 추가 | 현재 앱이 같은 API를 이미 사용하고, Avalonia 공식 TrayIcon 문서는 Windows·macOS 지원을 명시한다. 기존 런타임 콜백을 그대로 재사용할 수 있다(`AppRuntime.cs:286-300`, `345-366`). | OS가 제공하는 트레이/메뉴바 키보드 탐색과 보조 기술 노출은 실기 검증이 필요하다. | **채택.** |

보조 근거: Avalonia 공식 문서의 [TrayIcon](https://docs.avaloniaui.net/controls/navigation/trayicon)은
Windows·macOS와 `NativeMenu`를 지원한다고 명시하고, [Window.Activate API](https://api-docs.avaloniaui.net/docs/M_Avalonia_Controls_WindowBase_Activate)는
창 활성화 API만 보장한다. 이 문서 정보는 실제 OS 동작 통과로 대체하지 않는다.

### 제안 구현

- **소유 파일:** `src/Unfold.Desktop/AppRuntime.cs`, `Tests/Unfold.Tests/BreakReminderTests.cs`,
  `src/Unfold.Desktop/SmokeDiagnostics.cs`. 채택안은 `PetWindow.cs`와 `PetSpeechBubble.cs`를 수정하지 않는다.
- **의존성:** 현재 `Avalonia.Desktop`의 `TrayIcon`·`NativeMenuItem`과 기존 `TrayReminderStatus` 상태 계약만
  사용한다. 새 패키지, OS별 전역 키보드 hook, 권한 요청은 추가하지 않는다.
- `TrayReminderStatus`에 `CanStart`, `CanSnooze`, `CanComplete`를 추가한다. Invitation은 start+snooze,
  Resting은 complete만 true이고 Advance/Completed/None은 모두 false다. 기존 Badge/CanExpand 계약은 유지한다.
- `BuildTray()`에 `휴식 시작`, `n분 뒤에`, `완료` NativeMenuItem을 추가한다. `RefreshTray()`가 Header,
  `IsVisible`, `IsEnabled`를 위 계약에서 한 번에 갱신한다. 클릭은 각각 기존 `StartBreak`, `SnoozeBreak`,
  `CompleteBreak`를 호출하며 실행 직전에 현재 Notice를 재검사해 오래 열린 메뉴의 stale 동작을 막는다.
- 메뉴 동작은 말풍선 버튼과 **동일한 상태 전이로 가는 대체 키보드 경로**다. 알림 발생 자체에서는
  `Activate()`나 `Focus()`를 호출하지 않고 `ShowActivated=false`를 유지한다. 사용자가 트레이 동작을
  선택해도 설정 창을 열거나 펫 창을 활성화하지 않는다.
- 트레이 직접 동작이 실제 OS 보조 기술에서 실패할 때만 2차 대안으로 사용자가 명시적으로 고르는
  `말풍선으로 이동` 메뉴를 검토한다. 이 경우에만 `Activate()` 후 현재 동작 버튼에 `Focus()`하고,
  자동 알림 경로에는 넣지 않는다. 실기 결과 없이 이 대안을 먼저 구현하지 않는다.

### 추가 테스트와 단언

- `BreakReminderTests.TrayActionsMatchInvitationAndRestingWithoutRequestingPetActivation`
  - None/Advance/Completed에는 세 동작이 모두 false다.
  - Invitation에는 start+snooze만 true, Resting에는 complete만 true다.
  - Start/Snooze/Complete 전이 뒤 `TrayStatus`가 즉시 갱신되고 기존 CanExpand/Badge 단언이 유지된다.
  - 생성한 `PetWindow.ShowActivated`가 false이며 상태 전이 중 바뀌지 않는다.
- 기존 `TrayStatusNamesWaitingAndRestingAndOffersRecoveryOnlyWhenFolded`
  (`Tests/Unfold.Tests/BreakReminderTests.cs:80-112`)과
  `FoldedReminderKeepsTrayAndBadgeInStepThroughRecoveryAndCompletion`(`BreakReminderTests.cs:114-184`)을
  회귀로 함께 실행한다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~BreakReminderTests.TrayActions|FullyQualifiedName~BreakReminderTests.TrayStatus|FullyQualifiedName~BreakReminderTests.FoldedReminder'
```

- **smoke.json 키:** `accessibility.reminderTrayActionsVerified`. Invitation→Start→Complete와 별도
  Invitation→Snooze 경로에서 TrayReminderStatus의 허용 동작·헤더와 세션 상태가 일치하고,
  `PetWindow.ShowActivated == false`임을 단언한다. native 메뉴에 실제 키 입력한 증거로 해석하지 않는다.
- **수용 기준:** 알림은 포커스를 빼앗지 않고, 상태마다 트레이에 유효한 동작만 노출되며, 그 동작은
  말풍선 버튼과 같은 세션·타이머·기록 결과를 낸다. 접기/펼치기 복구 계약도 회귀하지 않는다.
- **실제 OS 선행/잔여(출시 판정 전 필수):** 다른 앱의 TextBox에 입력 포커스를 둔 채 알림을 띄운다.
  macOS 메뉴바와 Windows 알림 영역을 각 OS의 키보드 경로로 열어 시작·미루기·완료를 포인터 없이
  실행한다. VoiceOver·Narrator가 항목 이름·사용 가능 상태를 낭독하는지, 실행 후 원래 앱 포커스가
  유지되는지, NativeMenu의 동적 IsVisible/IsEnabled가 즉시 반영되는지 확인한다. Win+B/상태 메뉴
  탐색 같은 정확한 키는 OS 설정에 따라 달라질 수 있으므로 실제 사용 경로를 결과에 기록한다.

## UI-09 — 비활성 동작의 사유를 프로그램적으로 연결(P2)

### 현재 코드 근거와 결정

- 설치 버튼은 처음부터 비활성이고(`src/Unfold.Desktop/PetPackWindow.cs:62-64`), 팩 읽기·미리보기·설치
  상태에 따라 여러 위치에서 enablement가 바뀌지만(`PetPackWindow.cs:174-200`, `202-239`), 버튼에는
  `HelpText`가 없다. 시각 사유는 `PackStatus`에만 있다(`PetPackWindow.cs:43-50`, `89-108`).
- 생성 버튼은 이름과 idle clip, busy 상태를 조합해 활성화한다
  (`src/Unfold.Desktop/CustomPetWindow.cs:297-308`). 현재 버튼에는 해당 조건을 설명하는 접근성 속성이 없다
  (`CustomPetWindow.cs:116-170`).
- 개인화의 여섯 버튼은 선택/기본 루틴/타이머 상태로 enablement가 바뀐다
  (`src/Unfold.Desktop/PersonalizationWindow.cs:151-165`). `프로필 적용`만 이미 HelpText와 ToolTip을
  갱신하므로(`PersonalizationWindow.cs:158-162`) 이 패턴을 여섯 버튼으로 확장한다.

**결정:** 보이는 상태 문장을 버튼 레이블로 바꾸지 않는다. 각 버튼의 현재 활성/비활성 조건과 같은
함수에서 `AutomationProperties.SetHelpText`를 매번 갱신하고, 시각 사용자에게도 같은 설명이 필요하면
동일 문자열을 ToolTip에 넣는다. `SetLabeledBy`는 버튼 이름을 대체하므로 쓰지 않는다.

### 제안 구현

- **소유 파일:** `src/Unfold.Desktop/PetPackWindow.cs`, `CustomPetWindow.cs`,
  `PersonalizationWindow.cs`; 테스트는 각각 `PetPackWindowTests.cs`, `CustomPetTests.cs`,
  `PersonalizationWindowTests.cs`; smoke는 `SmokeDiagnostics.cs`.
- **의존성:** 현재 Avalonia `AutomationProperties.SetHelpText`와 기존 한국어 상태 문구만 사용한다.
  새 접근성 패키지나 저장 데이터 변경은 없다.
- PetPackView에 enablement와 HelpText를 함께 정하는 `RefreshInstallAvailability()`를 둔다. 우선순위는
  설치 중 → 팩 확인 중 → 팩 없음/오류 → 미리보기 준비 중/실패 → 설치 가능이다. 비활성 문장은 다음
  행동을 포함한다(예: “펫 팩을 열고 미리보기 확인이 끝나면 설치할 수 있어요.”).
- CustomPetView.Refresh에서 create 사유를 busy → 이름+idle 모두 없음 → 이름 없음 → idle 없음 → 준비됨
  순으로 계산한다. “펫 이름을 입력해 주세요”, “쉬는 모습 파일을 추가해 주세요”를 기존 UI 용어
  (`CustomPetWindow.cs:36-42`, `92-95`) 그대로 사용한다.
- PersonalizationView.RefreshActions에서 여섯 버튼 모두 HelpText/ToolTip을 갱신한다. 기본 루틴 편집·삭제는
  “기본 루틴은 편집할 수 없어요/삭제할 수 없어요”, 미선택은 “먼저 … 선택해 주세요”, 프로필 적용의
  타이머 차단 문구는 현재 문자열(`PersonalizationWindow.cs:159`)을 보존한다.

### 추가 테스트와 단언

- `PetPackWindowTests.DisabledInstallExplainsTheNextAvailableAction`: 초기·읽는 중·손상 팩·준비 완료 상태에서
  IsEnabled와 HelpText가 함께 바뀌고, 비활성 HelpText가 공백이 아니며 복구 행동을 포함한다.
- `CustomPetTests.CreateButtonExplainsEveryMissingRequirement`: 이름/idle 조합 네 가지에서 HelpText가 각각
  누락 조건을 정확히 포함하고, 준비 완료 때 enabled 설명으로 바뀐다.
- `PersonalizationWindowTests.DisabledLibraryActionsExposeSelectionAndPolicyReasons`: 기본 루틴, 사용자 루틴,
  프로필 없음, 프로필 선택, 타이머 실행 중 상태에서 여섯 버튼의 IsEnabled와 HelpText가 같은 이유를
  나타내며 현재 applyProfile 차단 문구가 유지된다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~PetPackWindowTests.DisabledInstall|FullyQualifiedName~CustomPetTests.CreateButtonExplains|FullyQualifiedName~PersonalizationWindowTests.DisabledLibraryActions'
```

- **smoke.json 키:** `accessibility.disabledActionReasonsVerified`. SmokeDiagnostics가 세 화면에서 비활성
  버튼 이름과 `AutomationProperties.GetHelpText`를 읽고 조건별 예상 복구 동작을 포함하는지 확인한다.
- **수용 기준:** 모든 대상 버튼은 활성 여부가 바뀌는 같은 메서드에서 HelpText도 바뀐다. 비활성일 때
  null/빈 문자열이 없고, 사라진 조건을 계속 말하는 stale HelpText가 없다.
- **실제 OS 잔여:** 비활성 컨트롤을 VoiceOver/Narrator가 기본 탐색에서 건너뛸 수 있다. rotor/탐색 모드와
  native accessibility tree에서 버튼 이름·비활성 상태·HelpText를 실제로 들을 수 있는지 확인한다.
  ToolTip만으로 통과 판정하지 않는다.

## UI-10 — 펫 팩 오류 상태 색상 일관화(P2)

### 현재 코드 근거와 결정

- PackStatus는 생성 시 Muted로 고정된다(`src/Unfold.Desktop/PetPackWindow.cs:89-90`). 팩 열기 실패,
  미리보기 실패, 설치 실패는 오류 문장만 바꾸고 Foreground를 바꾸지 않는다
  (`PetPackWindow.cs:199-200`, `218-219`, `233-238`). Phase 2 `pack-error.png`에서도 오류 문장이 일반
  설명과 같은 낮은 강조로 보인다.
- 다른 화면은 오류에 `DesignSystem.Error`를 명시한다. CustomPetView.ShowError는 오류 색을 설정하고
  (`src/Unfold.Desktop/CustomPetWindow.cs:310-315`), 기록 내보내기 실패도 같은 토큰을 쓴다
  (`src/Unfold.Desktop/BreakReviewWindow.cs:51-57`).

### 제안 구현

- **소유 파일:** `src/Unfold.Desktop/PetPackWindow.cs`,
  `Tests/Unfold.Tests/PetPackWindowTests.cs`, `src/Unfold.Desktop/SmokeDiagnostics.cs`.
- `SetStatus(string text, bool isError = false)` 하나가 Text와 Foreground를 함께 설정하도록 모든 PackStatus
  대입을 통합한다. 세 catch는 Error, 확인 중·준비 완료·설치 중·설치 완료는 Muted를 쓴다. 이로써 오류
  뒤 정상 팩을 열었을 때 Error 색이 남는 역방향 회귀도 막는다.
- **의존성:** 기존 `DesignSystem.Error`/`Muted`만 사용한다(`DesignSystem.cs:15-19`). 워커 A가 색 토큰을
  재정의하면 이름이 아니라 의미 역할을 따른다.

### 추가 테스트와 단언

- `PetPackWindowTests.PackStatusUsesErrorForFailuresAndReturnsToMutedAfterRecovery`
  - 손상 팩 열기 뒤 텍스트가 “열지 못했어요”를 포함하고 Foreground == Error다.
  - 이어 정상 팩을 열면 준비 문구와 Foreground == Muted다.
  - installed callback 실패 경로도 “설치하지 못했어요”와 Error를 함께 단언한다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter FullyQualifiedName~PetPackWindowTests.PackStatusUsesErrorForFailuresAndReturnsToMutedAfterRecovery
```

- **smoke.json 키:** `accessibility.packStatusErrorColorVerified`. 기존 잘못된 팩 경로에서 Error, 다음 정상
  팩 준비 상태에서 Muted로 복구되는 양방향을 확인하고 `pack-error.png`를 갱신한다.
- **수용 기준:** 오류는 문장+Error 색으로 전달되고, 진행/성공 문장은 Muted다. 색상만으로 오류를 전달하지
  않으며 기존 한국어 복구 문구를 보존한다.
- **실제 OS 잔여:** 로직·토큰은 자동 검사로 확정 가능하다. 다만 macOS·Windows 실제 테마/색상 프로필에서
  오류가 다른 상태보다 구별되고 고대비 설정에서 문장이 사라지지 않는지는 최종 화면 회귀로 확인한다.

## UI-11 — 그룹·행·펫 본체 시맨틱(P2)

### 현재 코드 근거와 결정

- PersonalizationTabs는 Name만 있고 접근성 이름이 없다
  (`src/Unfold.Desktop/PersonalizationWindow.cs:73-76`). 대조로 PetManagementTabs는 “펫 추가 방식” 이름을
  설정한다(`src/Unfold.Desktop/PetManagementView.cs:18-27`).
- BreakReviewView는 하나의 3열 Grid에 날짜·횟수·시간 TextBlock을 별도로 추가한다
  (`src/Unfold.Desktop/BreakReviewWindow.cs:35-39`, `74-90`). `weekly-review.png`에서는 행으로 보이지만
  프로그램적 그룹은 없다. 기존 테스트도 날짜와 횟수의 시각 위치만 단언한다
  (`Tests/Unfold.Tests/LocalizationTests.cs:19-40`).
- PetWindow 중앙 AnimationView는 마우스 클릭·드래그 대상이지만(`src/Unfold.Desktop/PetWindow.cs:68-88`)
  접근성 이름이 없다(`PetWindow.cs:41-45`). 말풍선 자체에는 이미 이름이 있다
  (`src/Unfold.Desktop/PetSpeechBubble.cs:33-39`).

### 제안 구현

- **소유 파일:** `src/Unfold.Desktop/PersonalizationWindow.cs`, `BreakReviewWindow.cs`, `PetWindow.cs`;
  테스트는 `PersonalizationWindowTests.cs`, `LocalizationTests.cs`, `BreakReminderTests.cs`;
  smoke는 `SmokeDiagnostics.cs`.
- PersonalizationTabs에 `AutomationProperties.SetName(tabs, "루틴과 업무 프로필")`을 지정한다. 개별
  TabItem Header “내 루틴”·“업무 프로필”은 바꾸지 않는다.
- BreakReviewView의 `days`는 세로 StackPanel로 바꾸고, 날짜마다
  `Grid(Name="BreakReviewDayRow{0..6}", ColumnDefinitions="*,100,110")` 한 개를 만든다. 행 Grid에
  `Name="M월 d일 (요일), n회, n분 n초"`, `ControlTypeOverride=Group`,
  `AccessibilityView=Content`를 지정하고, 중복 낭독을 막기 위해 세 자식 TextBlock은 `AccessibilityView.Raw`로
  둔다. 시각 3열 폭과 8px 행 간격은 유지한다. 실제 보조 기술에서 Raw 정책이 행을 숨기거나 중복 낭독하면
  그 결과를 근거로 조정한다.
- AnimationView에는 기본 이름 “데스크톱 펫”을 설정하고, `SetCharacter()`에서 선택 캐릭터가 있으면
  `“데스크톱 펫 {Manifest.Name}”`으로 갱신한다. 역할은 클릭 가능한 버튼으로 과장하지 않고 Image로
  override한다. 현재는 키보드 Activate 계약이 없기 때문이다.
- **의존성:** `Avalonia.Automation.AccessibilityView`와
  `Avalonia.Automation.Peers.AutomationControlType`; 현재 앱의 Avalonia 참조
  (`Unfold.Desktop.csproj:11-15`). 캐릭터 이름은 기존 `CharacterManifest.Name`만 사용하고 manifest 계약은
  바꾸지 않는다.

### 추가 테스트와 단언

- `PersonalizationWindowTests.PersonalizationTabsExposeAGroupName`: PersonalizationTabs의 Name이
  “루틴과 업무 프로필”이고 두 TabItem Header가 그대로다.
- `LocalizationTests.ReviewRowsExposeOneSemanticSummaryPerDay`: 행이 정확히 7개이고, 각 행의 Name이 같은
  행의 날짜·횟수·시간을 모두 포함하며 ControlType==Group, View==Content다. 자식은 Raw이고 기존 한국어
  요일·열 배치·CSV 단언은 유지한다.
- `BreakReminderTests.PetAnimationNamesTheSelectedDesktopPet`: `PetView`의 이름이 공백이 아니고 기본/선택
  캐릭터 이름 갱신을 반영하며 ControlType==Image다. 말풍선 이름도 계속 “펫의 스트레칭 알림”이다.

```sh
dotnet test Tests/Unfold.Tests/Unfold.Tests.csproj -c Release --no-restore \
  --filter 'FullyQualifiedName~PersonalizationWindowTests.PersonalizationTabsExpose|FullyQualifiedName~LocalizationTests.ReviewRowsExpose|FullyQualifiedName~BreakReminderTests.PetAnimationNames'
```

- **smoke.json 키:** `accessibility.semanticNamesVerified`. 탭 그룹 이름, 7개 기록 행의 요약/Group 역할,
  PetView 이름/Image 역할을 검사한다.
- **수용 기준:** 탭 그룹은 이름이 있고, 기록은 한 날짜의 세 값이 하나의 프로그램적 행으로 묶이며,
  펫 본체는 현재 캐릭터를 식별하는 이름을 가진다. 시각 배치·CSV·말풍선 이름은 회귀하지 않는다.
- **실제 OS 잔여:** VoiceOver·Narrator에서 탭 그룹 진입 이름, 기록 행당 한 번의 자연스러운 낭독 순서,
  PetWindow가 비활성·투명 창이어도 Image 이름이 AX/UIA 트리에 나타나는지 확인한다. Narrator scan mode와
  VoiceOver rotor 모두에서 중복 셀 낭독이 없는지 확인한다. Windows 125%·150%·200%에서 3열 정렬도 함께
  확인하되 이는 시맨틱 자동 검사의 대체가 아니다.

## 구현 순서와 파일 소유권 충돌

| 순서 | 항목 | 우선순위 | 제품 파일 소유권 | 테스트 파일 | 선행/충돌 | 병렬 안전성 |
|---:|---|---|---|---|---|---|
| 0 | 워커 A 토큰 확정 | — | `DesignSystem.cs` | `DesignSystemTests.cs` 가능 | UI-07과 직접 충돌 | UI-07보다 먼저 병합 |
| 1 | UI-08 트레이 동작 | P1 | `AppRuntime.cs` | `BreakReminderTests.cs` | UI-11도 BreakReminderTests 사용, Smoke 공통 | UI-09·UI-10과 제품 코드 병렬 안전 |
| 2 | UI-07 포커스 링 | P1 | `DesignSystem.cs` | `DesignSystemTests.cs` | 워커 A의 동일 파일 완료 후 | UI-08·UI-09·UI-10·UI-11 제품 코드와 병렬 안전 |
| 3 | UI-09 사유 연결 | P2 | `PetPackWindow.cs`, `CustomPetWindow.cs`, `PersonalizationWindow.cs` | 대응 3개 테스트 | UI-10과 PetPackWindow/PetPackWindowTests, UI-11과 PersonalizationWindow/Tests 충돌 | 세 화면끼리는 파일별 분할 가능. UI-10·UI-11과 동시 수정 금지 |
| 4 | UI-10 오류 색 | P2 | `PetPackWindow.cs` | `PetPackWindowTests.cs` | UI-09 설치 상태 helper와 합쳐야 함 | UI-09 PetPack 담당과 한 소유자로 직렬화 |
| 5 | UI-11 시맨틱 | P2 | `PersonalizationWindow.cs`, `BreakReviewWindow.cs`, `PetWindow.cs` | 대응 3개 테스트 | UI-09 Personalization 담당, UI-08 BreakReminderTests와 충돌 | BreakReview/PetWindow는 병렬 가능, Personalization은 UI-09 뒤 |
| 6 | 통합 smoke | P1/P2 | `SmokeDiagnostics.cs` | 전체 | 다섯 항목 모두 이 파일 사용 | 한 명이 마지막에 일괄 통합 |

제품 코드 기준으로 안전한 병렬 묶음은 (A 완료 뒤) **UI-07**, **UI-08**, **UI-09 중 CustomPet**,
**UI-11 중 BreakReview/PetWindow**다. `DesignSystem.cs`를 건드리는 접근성 항목은 UI-07 하나지만 워커 A의
디자인 시스템 재설계와 직접 충돌한다. `SmokeDiagnostics.cs`는 다섯 항목 모두 공통이므로 각 워커가 동시에
수정하지 말고 마지막 통합 소유자가 한 번에 다섯 키를 추가한다.

## 접점

- **워커 A(design-system):** `31_design_system_redesign.md` 부재로 UI-07의 Focus 토큰 이름은 임시다.
  A가 `Focus*` 토큰을 정의하면 UI-07은 이름·값을 그대로 소비하고 별도 색을 만들지 않는다. 기존 입력
  표면 불변 계약(`docs/design-system.md:50-57`)은 유지해야 한다.
- **워커 B(visual-gap):** UI-07의 세 focus PNG와 UI-10의 갱신된 `pack-error.png`가 시각 회귀 기준이다.
  포커스 링의 2px/inset은 B의 밀도·대비 판정과 충돌 시 토큰 단계에서 조정하되 표면 BorderBrush 변경으로
  되돌리지 않는다.
- **워커 C(ux-flow):** UI-08의 트레이 직접 동작은 알림 흐름의 대체 진입점이다. 트레이 메뉴 문구와 상태
  노출 순서는 C의 흐름 설계와 맞추되 `ShowActivated=false`와 “알림 시 포커스 비탈취”를 불변 조건으로 둔다.
- **공유 작업 트리:** 현재 `PersonalizationWindow.cs:158-162`에는 2단계에서 들어온 applyProfile HelpText가
  이미 있으므로 UI-09는 이를 삭제하거나 과거 코드로 되돌리지 않는다. 다른 워커의 기존 미커밋 변경도
  보존한다.

## 최종 수용 체크리스트

- [ ] UI-07: 세 입력이 표면 배경·테두리 색/두께를 바꾸지 않고 Tab 포커스 링을 보인다.
- [ ] UI-08: 자동 알림은 활성화하지 않으며, macOS·Windows 트레이에서 시작·미루기·완료를 키보드와
  VoiceOver/Narrator로 실행했다.
- [ ] UI-09: 대상 비활성 버튼 모두에 현재 조건과 일치하는 non-empty HelpText가 있다.
- [ ] UI-10: 팩 오류는 Error, 다음 진행/성공은 Muted로 복구된다.
- [ ] UI-11: 탭 그룹명, 7개 행 요약, 현재 펫 이름이 자동 검사와 실제 보조 기술에서 확인됐다.
- [ ] 전체 테스트와 격리 smoke는 통과했고, 실제 OS 미검증 항목을 자동 PASS로 기록하지 않았다.
