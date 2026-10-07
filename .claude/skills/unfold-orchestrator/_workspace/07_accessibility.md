# B4 접근성 감사 — Unfold 1.1.1-beta

감사 대상: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold` (`bbdb4a8`, `1.1.1-beta`)
기준: WCAG 2.2 AA를 데스크톱(Avalonia 12.1.2 / UI Automation·NSAccessibility)에 적용
작성: 2026-10-06

## 요약

- 텍스트 대비는 4개 테마 전부 통과한다(본문 9.71~14.22:1, 보조 텍스트 4.56~8.77:1). 대비는 이 앱의 약점이 아니다.
- 치명적 문제는 **알림 전달**이다. 코드 전체에 `AutomationProperties.LiveSetting`이 한 번도 쓰이지 않아 말풍선 등장·저장 결과·오류가 보조기술에 전혀 전달되지 않는데, 말풍선 초대는 30초 뒤 자동으로 미뤄지고 사전/완료 공지는 5초 뒤 사라진다(조정 불가).
- 입력 컨트롤의 `FocusAdorner`를 `null`로 지우고 `:focus`/`:focus-visible` 스타일을 평상시와 동일하게 둔 것은 의도된 설계이고(`DesignSystemTests.InputsKeepTheirAppearanceWhileHoveredAndEdited`), 라벨 색·문구 변경으로 대체한다. 그러나 라벨 연결이 빠진 **루틴 편집기 6개 입력**과 **회고 날짜 헤더 토글**에는 어떤 포커스 표시도 남지 않는다.
- 보조(Secondary) 버튼은 테두리가 투명하고 면색만으로 구분되는데 배경과의 대비가 1.06~1.37:1이라, "n분 뒤에"·"중지"·네비게이션 레일의 경계가 사실상 보이지 않는다(1.4.11).
- 움직임 줄이기는 스위치(`AnimationsEnabled`)만 존재하고 OS 설정·앱 설정 어느 쪽과도 연결되어 있지 않아 항상 켜져 있다.

## 방법과 한계

**수행한 것**
- 소스 정적 분석: `src/Unfold.Desktop/` 전 파일, `src/Unfold.Core/{PetReminder,AppSettings,BreakSession}.cs`. `AutomationProperties` / `IsTabStop` / `TabIndex` / `FocusAdorner` / `:focus*` / `Key.*` / `LiveSetting` 전수 grep.
- 대비 계산: 팔레트 hex를 `DesignSystem.Themes.cs:17-30`에서 직접 읽어 WCAG 상대휘도 공식으로 계산 (`<scratchpad>/a11y/contrast.py`).
- 캡처 판독: `responsive-home-top.png`, `responsive-settings-bottom.png`(각 640×560), `speech-top.png`.
- 기존 테스트 확인: `tests/Unfold.Tests/{UiAuditRegressionTests,DesignSystemTests,ResponsiveLayoutTests}.cs`에서 포커스·대비·반응형 커버리지를 확인하고, 이미 테스트로 고정된 설계 결정은 "의도됨"으로 표기했다.

**하지 못한 것 (미검증)**
- VoiceOver·Narrator 실행, 실제 발화 내용, Avalonia의 자동화 피어가 macOS NSAccessibility로 어떻게 매핑되는지. 이 보고서의 "보조기술에 전달되지 않는다"는 모두 **소스에 전달 코드가 없다**는 근거이지 실측이 아니다.
- 빌드·앱 실행·실시간 Tab 순회. Tab 순서는 시각 트리 추가 순서로만 추정했다.
- OS 글꼴 확대(macOS 디스플레이 텍스트 크기, Windows 125/150/200%)와 고대비·강제 색 모드 실측.
- 슬라이더 썸·스크롤바 등 Fluent 기본 템플릿이 그리는 부분의 실제 대비(동적 리소스라 정적 계산 불가).

**기타**
- 과업 설명에는 테마 5종이라고 적혀 있으나 코드에는 4종만 있다(`DesignSystem.Themes.cs:17-30`: 유연한 라일락=Plum·기본, 다정한 오트, 숨 고르는 숲, 밤의 버터). 대비표는 이 4종 기준이다.
- 캡처 파일명의 `*-minimum.png`는 860×680이고, 실제 최소 크기 640×560은 `responsive-*.png`다.
- 기존 항목(A-02 펫 페이지 포커스, QA-01 자동 미루기 계측, 긴 펫 이름 말줄임, 픽셀 에디터 영어 UI)은 새 발견으로 올리지 않았다.

## 대비 계산표

### 텍스트 (1.4.3 / 4.5:1, 큰 글자 3:1)

| 쌍 (전경 / 배경) | 라일락(기본) | 오트 | 숲 | 밤의 버터 | 판정 |
|---|---|---|---|---|---|
| 본문 Text / Canvas | 12.06 | 11.84 | 10.34 | 14.22 | PASS |
| 본문 Text / Shell | 12.78 | 12.56 | 10.94 | 12.84 | PASS |
| 본문 Text / Surface | 13.62 | 13.22 | 11.36 | 11.65 | PASS |
| 버튼 글자 Text / Raised | 11.32 | 10.89 | 9.71 | 10.37 | PASS |
| 보조 Muted / Surface | 5.49 | 5.82 | 5.40 | 7.19 | PASS |
| 보조 Muted / Shell | 5.15 | 5.53 | 5.20 | 7.92 | PASS |
| 보조 Muted / Canvas | 4.86 | 5.21 | 4.92 | 8.77 | PASS |
| 보조 Muted / Raised | 4.56 | 4.79 | 4.62 | 6.40 | PASS (여유 0.06) |
| 주버튼 OnAccent / Accent | 6.74 | 5.88 | 7.43 | 8.54 | PASS |
| 주버튼 hover OnAccent / AccentHover | 8.75 | 7.43 | 9.70 | 9.58 | PASS |
| hover 글자 Text / Hover | 10.34 | 10.19 | 9.13 | 9.51 | PASS |
| danger Error / Raised | 5.31 | 5.25 | 5.43 | 6.95 | PASS |
| danger hover Error / Hover | 4.85 | 4.91 | 5.11 | 6.37 | PASS |
| 오류 Error / Surface | 6.39 | 6.37 | 6.36 | 7.80 | PASS |
| 경고·초과 Warning / Shell | 6.20 | 6.13 | 6.12 | 9.75 | PASS |
| 성공 Success / Surface | 6.14 | 6.20 | 7.21 | 8.30 | PASS |
| 중지 Stopped / Surface | 5.63 | 5.72 | 5.69 | 5.76 | PASS |
| 비활성 DisabledText / DisabledFill | **3.43** | **3.56** | **3.33** | 4.85 | 1.4.3 면제(비활성) — A11Y-12 |

### 비텍스트·포커스 (1.4.11 / 3:1)

| 쌍 | 라일락 | 오트 | 숲 | 밤의 버터 | 판정 |
|---|---|---|---|---|---|
| 입력 테두리 Line / Shell | 3.09 | 3.23 | 3.35 | 4.77 | PASS |
| 포커스 테두리(일반 버튼) Text / Raised | 11.32 | 10.89 | 9.71 | 10.37 | PASS |
| 포커스 테두리(주버튼) OnAccent / Accent | 6.74 | 5.88 | 7.43 | 8.54 | PASS |
| 포커스 토큰 FocusRing(=Accent) / Shell | 6.19 | 5.46 | 6.95 | 9.55 | PASS |
| 선택 상태 Accent / Surface | 6.59 | 5.75 | 7.21 | 8.66 | PASS |
| 아이콘 Text / Shell | 12.78 | 12.56 | 10.94 | 12.84 | PASS |
| **보조버튼 면 Raised / Shell** | **1.13** | **1.15** | **1.13** | **1.24** | **FAIL — A11Y-04** |
| **보조버튼 면 Raised / Surface** | **1.20** | **1.21** | **1.17** | **1.12** | **FAIL — A11Y-04** |
| **보조버튼 면 Raised / Canvas** | **1.07** | **1.09** | **1.06** | **1.37** | **FAIL — A11Y-04** |
| **말풍선 테두리 Outline / Shell** | **1.45** | **1.48** | **1.47** | **1.90** | **FAIL — A11Y-08** |
| 카드 구분선 Outline / Surface | 1.54 | 1.55 | 1.52 | 1.73 | 장식 구분선, 1.4.11 비해당 |
| 펫 후광 Halo / Shell | 1.24 | 1.23 | 1.20 | 1.35 | 장식, 1.4.11 비해당 |

포커스 표시의 대비 자체는 모두 통과한다. 문제는 대비가 아니라 **포커스 표시가 그려지지 않는 컨트롤이 있다**는 점이다(A11Y-02, A11Y-03, A11Y-07).

## 발견

| ID | 심각도 | WCAG | 제목 | 근거 | 영향 | 확신도 | 수정 방향 | 소유 후보 |
|---|---|---|---|---|---|---|---|---|
| A11Y-01 | P1 | 4.1.3 상태 메시지(AA), 2.2.1 시간 조절(A) | 휴식 말풍선이 보조기술에 알려지지 않고 30초 뒤 자동으로 미뤄짐 | `src/Unfold.Core/PetReminder.cs:10,37-45`(InvitationTimeout 30초 고정, 설정 없음), `AppSettings.cs` 전체에 타임아웃 항목 없음, `src/Unfold.Desktop` 전 파일에 `LiveSetting` 0회. 키보드 경로는 트레이 메뉴 "휴식 알림으로 이동"(`AppRuntime.cs:588-589` → `PetWindow.cs:242`) 하나뿐 | 전맹·저시력 스크린리더 사용자, 트레이 접근이 느린 운동 장애 사용자 — 제품의 핵심 기능(휴식 권유)을 사용할 수 없음 | 확인됨(코드 부재는 확정, 실제 발화는 미검증) | ① 말풍선 루트에 `AutomationProperties.SetLiveSetting(bubble, AutomationLiveSetting.Assertive)` + 대사 변경 시 `AutomationProperties.SetName`을 실제 대사로 갱신(현재 "펫의 스트레칭 알림" 고정). ② `AppSettings`에 `InvitationTimeoutSeconds`(기본 30, 0=무제한) 추가해 `PetReminder.InvitationTimeout` 대체. ③ 기본값을 20배 이상 늘리거나 끌 수 있게 설정 UI 노출 | `src/Unfold.Core/PetReminder.cs`, `src/Unfold.Core/AppSettings.cs`, `src/Unfold.Desktop/PetSpeechBubble.cs`, `src/Unfold.Desktop/SettingsWindow.Notifications.cs` |
| A11Y-02 | P1 | 2.4.7 포커스 표시(AA), 1.3.1(A) | 루틴 편집기의 단계 입력 6개에 포커스 표시가 전혀 없음 | `src/Unfold.Desktop/RoutineEditorWindow.cs:25-30` — `Step1~3`(TextBox)·`Seconds1~3`(NumericUpDown)이 `Ui.Row`/`Ui.Card`로만 배치되어 `Ui.Field`를 거치지 않음 → `SetLabeledBy`·`KeyboardFocusLabel` 둘 다 없음. 동시에 `DesignSystem.cs:118,120`에서 `FocusAdornerProperty=null`이고 `:focus`/`:focus-within` 스타일이 평상시와 동일 | 키보드 전용·저시력 사용자 — 6개 필드를 Tab으로 지날 때 현재 위치를 알 수 없음. 라벨 미연결로 스크린리더 Name도 `AutomationProperties.SetName`에만 의존 | 확인됨 | 각 단계를 `Ui.Field($"{n}단계 안내", instructions[n])` / `Ui.Field($"{n}단계 시간(초)", durations[n])`로 감싸 `KeyboardFocusLabel`과 `SetLabeledBy`를 적용. 더 나은 수정은 A11Y-07의 공통 포커스 링 도입 | `src/Unfold.Desktop/RoutineEditorWindow.cs` |
| A11Y-03 | P2 | 2.4.7 포커스 표시(AA) | 회고 날짜 헤더 토글에 포커스 표시가 없음 | `src/Unfold.Desktop/BreakReviewWindow.cs:176-180` — ToggleButton에 `BorderBrush=Brushes.Transparent`, `BorderThickness=new(0)`을 **로컬 값**으로 지정(로컬 값이 스타일보다 우선하므로 Fluent의 `:focus-visible` 테두리가 적용되지 않음). `AddDateHeaderStyles`(199-226)는 `:pointerover`/`:pressed`/`:checked`만 다루고 `:focus-visible` 없음. 이 토글은 `HandleDateHeaderKey`(279-297)로 Enter/Space/←→/↑↓를 처리하는 키보드 조작 대상임 | 키보드 사용자 — 회고 화면에서 어느 날짜에 있는지 보이지 않음 | 확인됨(코드), 가능성 높음(렌더 결과) | `AddDateHeaderStyles`에 `:focus-visible` 셀렉터를 추가해 `PART_ContentPresenter`의 `BorderBrush=DesignSystem.FocusRing`, `BorderThickness=DesignSystem.FocusRingWidth(2)` 지정. 로컬 `BorderBrush`/`BorderThickness` 지정은 제거하고 스타일로 옮긴다 | `src/Unfold.Desktop/BreakReviewWindow.cs` |
| A11Y-04 | P2 | 1.4.11 비텍스트 대비(AA) | 보조 버튼의 면 경계가 배경과 1.06~1.37:1 | `src/Unfold.Desktop/DesignSystem.cs:78-88` — `unfold-action` 기본값이 `Background=Raised`, `BorderBrush=Brushes.Transparent`. 계산표 "보조버튼 면" 행. 캡처 `speech-top.png`의 "5분 뒤에", `responsive-home-top.png`의 "중지"·네비게이션 레일에서 육안 확인 | 저시력·고령 사용자, 밝은 환경 — 클릭 가능한 영역의 경계를 못 찾음. 말풍선에서는 "휴식 시작"(Accent)만 보이고 "n분 뒤에"는 배경에 묻힘 | 확인됨(계산), 가능성 높음(1.4.11 적용 여부는 "경계가 식별에 필요한가"에 달림 — 라벨 텍스트가 있어 완화 해석 여지 있음) | `unfold-action` 기본 스타일의 `BorderBrush`를 `Brushes.Transparent` → `OutlineStrong`으로 바꾸고 `BorderThickness`는 2 유지(포커스 시 `Cream`으로 교체되는 현재 구조와 호환). `OutlineStrong/Shell`은 3.09~4.77:1로 통과한다 | `src/Unfold.Desktop/DesignSystem.cs` |
| A11Y-05 | P2 | 2.2.1 시간 조절(A) | 사전·완료 공지가 5초 뒤 자동으로 사라짐 (조정·연장·끄기 불가) | `src/Unfold.Core/PetReminder.cs:9,30,46,61` — `NoticeDuration=5초` 상수, `AppSettings`에 대응 항목 없음 | 읽기 속도가 느린 사용자, 인지 장애, 스크린리더 사용자 — "5분 뒤에 스트레칭해요"·"스트레칭을 마쳤어요!"를 읽기 전에 사라짐. WCAG 2.2.1 예외(실시간 이벤트·필수 시간 제한) 어디에도 해당하지 않음 | 확인됨 | `AppSettings`에 `NoticeSeconds`(기본 5, 범위 3~60) 추가, `PetReminder`가 생성자/프로퍼티로 주입받도록 변경. 설정 화면 알림 섹션에 "알림이 머무는 시간" 노출 | `src/Unfold.Core/PetReminder.cs`, `src/Unfold.Core/AppSettings.cs`, `src/Unfold.Desktop/SettingsWindow.Notifications.cs` |
| A11Y-06 | P2 | 2.3.3 상호작용 애니메이션(AAA), 2.2.2 일시정지(A) 부분 | 움직임 줄이기 설정이 어디에도 연결되어 있지 않음 | `src/Unfold.Desktop/AnimatedCountdown.cs:30`, `AnimatedTimeText.cs:17` — `AnimationsEnabled`를 `false`로 설정하는 코드가 `src/` 전체에 없음(테스트 `AnimatedCountdownTests.cs:75`에서만 사용). `AppSettings`에 모션 항목 없음. macOS `NSWorkspace.accessibilityDisplayShouldReduceMotion` / Windows `SPI_GETCLIENTAREAANIMATION` 조회 코드 없음 | 전정기관 장애(어지럼증), ADHD·주의 분산에 민감한 사용자 — 초 단위로 1분에 60회 숫자 롤링(380ms)이 계속 재생됨 | 확인됨 | ① `PlatformServices`에 OS 모션 감소 질의 추가, ② `AppSettings.ReduceMotion`(기본 OS 따름) 추가, ③ `AnimatedCountdown`·`AnimatedTimeText` 생성 지점(`PetSpeechBubble.cs:15,20-21`, 홈 타이머)에서 `AnimationsEnabled`를 주입. 펫 애니메이션 자체(`AnimationView`)는 캐릭터 정체성이므로 별도 판단 | `src/Unfold.Desktop/PlatformServices.cs`, `src/Unfold.Core/AppSettings.cs`, `src/Unfold.Desktop/AnimatedCountdown.cs`, `src/Unfold.Desktop/AnimatedTimeText.cs` |
| A11Y-07 | P2 | 2.4.7 포커스 표시(AA), 1.4.1 색에 의한 구분(A) | 입력·선택 컨트롤 자체에 포커스 표시가 없고, 대체 수단이 떨어진 라벨의 색·문구 변경뿐 | `src/Unfold.Desktop/DesignSystem.cs:112-121`(TextBox/NumericUpDown/ComboBox에 `FocusAdornerProperty=null`, `:focus`·`:focus-within`·`:disabled` 모두 평상시와 동일한 Shell+OutlineStrong), `DesignSystem.cs:285-296`(`settings-choice`·`home-choice`·`pet-choice`·`pet-preview-choice`의 `:focus-visible`도 동일). 대체 수단은 `Ui.cs:46-56` `KeyboardFocusLabel` — 라벨 텍스트에 " · 선택"을 붙이고 색을 `FocusRing`으로 바꿈. 이 설계는 `DesignSystemTests`의 `KeyboardFocusUsesFieldLabelAndLeavesInputBorderUnchanged` / `InputsKeepTheirAppearanceWhileHoveredAndEdited`로 고정되어 있음(= 의도된 결정) | 저시력·키보드 사용자 — 포커스 표시가 컨트롤에서 떨어진 라벨에 있어 시선 추적이 끊기고, 확대 뷰에서는 라벨이 화면 밖에 있을 수 있음 | 확인됨(코드), 의도된 설계임을 전제로 함 | 라벨 변경을 유지하되 컨트롤 자체에 2px 링을 추가한다: 각 입력 타입의 `:focus-visible` 스타일에서 `BorderBrush=DesignSystem.FocusRing`(Accent, 5.46~9.55:1)로 교체. `FocusRingWidth`·`FocusRingOffset` 상수가 이미 있으나 입력에는 쓰이지 않고 있다. 색 변경만으로 전달하지 않도록 " · 선택" 문구 병기는 유지 | `src/Unfold.Desktop/DesignSystem.cs`, `src/Unfold.Desktop/Ui.cs` |
| A11Y-08 | P3 | 1.4.11 비텍스트 대비(AA) | 말풍선 테두리가 배경과 1.45~1.90:1이라 바탕화면 위에서 경계가 사라짐 | `src/Unfold.Desktop/PetSpeechBubble.cs:26` — `Background=Shell`, `BorderBrush=DesignSystem.Outline`(=OutlineSubtle). 꼬리도 동일(`PetWindow.cs:28`). 계산표 "말풍선 테두리" 행 | 밝은 바탕화면을 쓰는 저시력 사용자 — 흰 벽지 위에서 흰 말풍선의 경계·꼬리 방향을 인지하기 어려움 | 확인됨(계산). 실제 배경이 사용자 벽지라 체감 정도는 미검증 | 말풍선·꼬리 테두리를 `OutlineStrong`으로 올린다(Shell 대비 3.09~4.77:1). 불투명도 설정(`BubbleOpacityPercent`)이 낮을 때 더 악화되므로 테두리는 불투명 유지 | `src/Unfold.Desktop/PetSpeechBubble.cs`, `src/Unfold.Desktop/PetWindow.cs` |
| A11Y-09 | P3 | 2.1.1 키보드(A) 보완 | 펫 창에 직접적인 키보드 진입 경로가 트레이 메뉴 하나뿐 | `src/Unfold.Desktop/PetWindow.cs:56` — `ShowInTaskbar=false; ShowActivated=false`, `WindowDecorations.None`. 진입은 `AppRuntime.cs:588` 트레이 항목 "휴식 알림으로 이동"뿐이고 `AppRuntime.cs:231`에서 `Reminder.Session is not null`일 때만 활성화됨(사전·완료 공지 때는 비활성). 전역 단축키 등록 코드 없음 | 키보드 전용 사용자 — 트레이/메뉴 막대 포커스 이동(macOS Ctrl+F8, VoiceOver VO+M)이라는 OS 기능을 알아야 하고, A11Y-01의 30초 제한과 겹치면 사실상 도달 불가 | 확인됨(코드) | 전역 단축키(예: Ctrl/Cmd+Shift+U) 또는 설정 창에서 활성 알림으로 이동하는 버튼을 추가. 최소한 설정 창 홈 탭에 "지금 휴식 시작" 버튼이 말풍선 없이도 같은 동작을 하도록 보장 | `src/Unfold.Desktop/AppRuntime.cs`, `src/Unfold.Desktop/SettingsWindow.Layout.cs` |
| A11Y-10 | P3 | 1.4.4 텍스트 크기 조절(AA), 1.4.10 리플로(AA) | 말풍선이 고정 높이라 긴 사용자 대사가 말줄임으로 잘림 | `src/Unfold.Desktop/PetSpeechBubble.cs:29`(`TextTrimming.CharacterEllipsis`), `:68-75`(Notice별 고정 Height), `DesignSystem.cs:36-40`(SpeechAdvanceHeight 96 등 상수). 대사는 `AppSettings.AdvanceDialogue` 등으로 사용자가 자유 입력 | 긴 문장을 쓰는 사용자, 저시력 — 시각적으로 뒷부분이 잘림. (`ToolTip.SetTip(title, title.Text)`는 포인터 전용 대체라 키보드 사용자에게 무효) | 가능성 높음 (렌더 한계는 상수에서 계산되나 실제 잘림 길이는 미측정) | 대사 입력 길이에 상한을 두거나(설정 입력에 `MaxLength`), 말풍선 높이를 `Notice`별 최소값 + 실제 측정 높이로 바꾼다. 말줄임을 유지한다면 툴팁 대신 전체 문구를 `AutomationProperties.HelpText`로도 노출 | `src/Unfold.Desktop/PetSpeechBubble.cs`, `src/Unfold.Desktop/DesignSystem.cs` |
| A11Y-11 | P3 | 1.4.4 텍스트 크기 조절(AA) | OS 글꼴 확대에 대한 대응이 없고 컨트롤 높이가 전부 상수 | `src/Unfold.Desktop/DesignSystem.cs:18-22,25-35` — `SettingsControlHeight=40`, `HomeControlHeight=40`, `ReviewControlHeight=40`, `ReviewDateHeight=44`, 말풍선 높이 4종이 모두 고정 px. `TimerControls.cs:40`(44 고정), `SettingsWindow.Layout.cs:365-366`(Nav 46 고정), `PetPackWindow.cs:132`(44 고정). 앱 내 글꼴 크기 설정 없음 | 저시력 사용자 — macOS/Windows에서 텍스트를 키우면 버튼 안의 한글이 잘릴 가능성 | 추정 (실행·실측 필요) | 고정 `Height`를 `MinHeight`로 바꾸고 `VerticalContentAlignment=Center`를 유지한다. 최소한 200% 확대 상태의 캡처를 출시 게이트에 추가 | `src/Unfold.Desktop/DesignSystem.cs`, `src/Unfold.Desktop/TimerControls.cs`, `src/Unfold.Desktop/SettingsWindow.Layout.cs` |
| A11Y-12 | P3 | 1.4.3 면제 항목(개선 권장) | 비활성 컨트롤 대비 3.33~3.56:1 | 계산표 "비활성" 행. `DesignSystem.cs:101-103`에서 비활성 버튼의 `Opacity`를 1로 되돌려 색만으로 상태를 전달. 기존 테스트 `DesignSystemTests.cs:355`가 임계값을 **3.0**으로 고정 | 저시력·고령 사용자 — "완료" 버튼 등이 비활성인지 단지 흐린 것인지 판별이 어려움 | 확인됨. **WCAG 1.4.3은 비활성 컨트롤을 명시적으로 면제하므로 위반이 아니다.** 품질 개선 항목으로만 제출 | 세 밝은 테마의 `DisabledText`를 한 단계 어둡게(예: 라일락 `#82738F` → `#6E6079`, 4.5:1 이상) 조정. 테스트 임계값도 함께 올린다 | `src/Unfold.Desktop/DesignSystem.Themes.cs`, `tests/Unfold.Tests/DesignSystemTests.cs` |
| A11Y-13 | P3 | 4.1.3 상태 메시지(AA) | 설정 창 페이지 전환 시 새 페이지가 알려지지 않음 | `src/Unfold.Desktop/SettingsWindow.Layout.cs:316-337` — `ShowPage`가 콘텐츠를 교체하고 페이지 안의 첫 컨트롤로 포커스를 옮기지만, 페이지 제목이나 전환 사실을 알리는 호출은 없음. 네비게이션 이름에 " · 선택됨"은 붙지만(`:343`) 포커스는 이미 본문으로 떠난 뒤 | 스크린리더 사용자 — "지금 어느 페이지인가"를 알 수 없고 포커스된 컨트롤 이름만 들림 | 확인됨 | 포커스를 본문의 첫 컨트롤이 아니라 페이지 제목(`PageHeader`의 heading, `Focusable=true`, `IsTabStop=false`)으로 먼저 옮기거나, 창 제목(`Title`)을 페이지명으로 갱신한다 | `src/Unfold.Desktop/SettingsWindow.Layout.cs`, `src/Unfold.Desktop/Ui.cs` |
| A11Y-14 | P3 | 4.1.2 이름·역할·값(A) 보완 | 네비게이션 레일이 선택 역할 없는 일반 Button 묶음 | `src/Unfold.Desktop/SettingsWindow.Layout.cs:338-348,361-371` — 항목이 `Button`이고 선택은 `primary` 클래스(색)와 이름 접미사 " · 선택됨"으로만 전달. `ListBox`/`RadioButton`/`TabControl` 같은 SelectionItem 패턴을 쓰지 않아 "4개 중 2번째" 같은 위치 정보가 없음 | 스크린리더 사용자 — 탐색 구조와 전체 개수를 파악하기 어려움. 이름에 상태가 들어 있어 치명적이진 않음 | 확인됨 | 레일을 `ListBox`(`SelectionMode.Single`)나 `RadioButton` 그룹으로 바꾸고 현재 시각 스타일을 `:selected`에 매핑. 간이 대안으로 `AutomationProperties.SetItemStatus`와 컨테이너에 `SetName("페이지 탐색")` 추가 | `src/Unfold.Desktop/SettingsWindow.Layout.cs` |

### 통과 확인된 항목 (회귀 방지용)

- 접근 가능한 이름: 아이콘 전용 버튼(네비게이션 레일, 타이머 제어, 회고 기간 이동, 펫 팩 재생, GLB 설정 콤보, 효과음 가져오기/초기화)에 `AutomationProperties.SetName`이 모두 붙어 있다. 슬라이더에는 단위까지 들어간 이름("말풍선 불투명도, 퍼센트")이 있다.
- 대상 크기(2.5.8, 24×24): 확인한 모든 클릭 대상이 40px 이상 — 회고 기간 버튼 40, 타이머 제어 110×44, 펫 팩 재생 44×44, 네비게이션 46×46, 입력 `MinHeight` 38.
- 대화상자 키보드: 확인·이름·오류·루틴·프로필 창 모두 `IsCancel`(Esc)·`IsDefault`(Enter)를 설정하고 열릴 때 포커스를 지정한다(`Ui.cs:238,282,331`, `RoutineEditorWindow.cs:33,49`, `ProfileEditorWindow.cs:28,40,47`).
- 회고 날짜 헤더의 키보드 패턴: Enter/Space 토글, ←/→ 접기·펼치기, ↑/↓ 헤더 간 이동이 구현되어 있다(`BreakReviewWindow.cs:279-297`).
- 반응형: 640×560(최소 크기) 캡처에서 가로 스크롤·겹침·잘림 없음. 스크롤바 트랙을 예약해 내용 위에 겹치지 않게 한 처리(`Ui.cs:66-70`)가 유효하다.
- 카운트다운의 자동화 이름: `AnimatedCountdown`이 `ControlAutomationPeer`를 구현해 "남은 시간 MM:SS"를 노출한다(`AnimatedCountdown.cs:144-150`). 다만 변경 알림(LiveSetting)은 없다 → A11Y-01.

## 보조기술 실측 체크리스트

실행하지 못했으므로 다음 절차를 출시 게이트에 남긴다. 각 단계는 "들린 내용"을 그대로 기록한다.

### macOS VoiceOver (Cmd+F5, Safari 아닌 네이티브 앱 대상)

1. 설정 창을 열고 VO+Shift+W로 창 구조를 읽어 네비게이션 레일 4개 항목의 이름·역할·"선택됨" 전달 여부를 확인한다 (A11Y-14).
2. VO+→로 홈 탭을 끝까지 훑어 "남은 시간 MM:SS", 타이머 제어 버튼 이름("타이머 일시정지"/"타이머 계속")이 상태에 맞게 바뀌는지 확인한다.
3. 네비게이션으로 페이지를 전환하고 페이지 이름이 발화되는지 확인한다 (A11Y-13).
4. 알림 간격을 1분으로 줄이고 말풍선이 뜰 때까지 대기한다. **아무 조작 없이** 말풍선 등장이 발화되는지, 30초 뒤 조용히 사라지는지 기록한다 (A11Y-01).
5. 말풍선이 떠 있는 동안 VO+M을 두 번 눌러 메뉴 막대 보조로 이동 → "휴식 알림으로 이동"을 선택 → 포커스가 "휴식 시작" 버튼에 닿는지, 몇 초가 걸렸는지 기록한다 (A11Y-09).
6. 휴식 진행 중 말풍선에서 타이머 값이 갱신될 때 반복 발화 여부를 확인한다(과잉 발화도 결함이다).
7. 설정 > 펫 팩에서 잘못된 파일을 열어 오류 상태 문구가 발화되는지 확인한다 (A11Y-01).
8. 루틴 편집기를 열고 Tab으로 6개 단계 입력을 지나며 각 필드의 이름이 발화되는지, 화면에 포커스 위치가 보이는지 확인한다 (A11Y-02).
9. 시스템 설정 > 손쉬운 사용 > 디스플레이 > "동작 줄이기"를 켜고 카운트다운 롤링이 멈추는지 확인한다 (A11Y-06, 현재 예상은 "멈추지 않음").
10. 디스플레이 텍스트 크기를 최대로 올리고 홈·설정·회고에서 버튼 글자가 잘리는지 캡처한다 (A11Y-11).

### Windows Narrator (Win+Ctrl+Enter)

1. 설정 창에서 Caps+F5(랜드마크)·Caps+F6(항목) 탐색으로 창 구조가 읽히는지 확인한다.
2. Tab만으로 홈 탭 전체를 순회하며 모든 포커스 위치가 **화면에서 보이는지** 기록한다(특히 숫자 입력·콤보박스 — A11Y-07).
3. 회고 탭에서 날짜 헤더를 Tab으로 지나며 포커스 표시 유무와 "펼침/접힘" 발화를 확인한다 (A11Y-03).
4. 루틴 편집기 6개 입력을 Tab으로 지나며 포커스 표시와 이름 발화를 확인한다 (A11Y-02).
5. 알림 간격 1분으로 말풍선을 띄우고, Narrator가 자동으로 알리는지 30초 타임아웃 전에 도달 가능한지 측정한다 (A11Y-01).
6. 트레이 아이콘을 Win+B → 화살표로 선택해 "휴식 알림으로 이동"까지의 키 입력 수를 센다 (A11Y-09).
7. 디스플레이 배율 150%·200%에서 640×560 최소 창을 만들고 가로 스크롤·잘림을 캡처한다 (A11Y-11).
8. 설정 > 접근성 > 시각 효과 > "애니메이션 효과"를 끄고 카운트다운 롤링이 멈추는지 확인한다 (A11Y-06).
9. 고대비 테마(설정 > 접근성 > 대비 테마)를 켜고 4개 테마 각각에서 버튼 경계·포커스 링이 살아남는지 캡처한다 (A11Y-04).
10. 사전 공지("5분 뒤에 스트레칭해요")가 Narrator로 읽히기 전에 5초가 지나 사라지는지 측정한다 (A11Y-05).
