# 접근성·반응형 QA — Unfold Avalonia UI

2026-09-20 · `release/mvp` 작업 트리(정적 분석) 대상. **코드 수정 없음, `dotnet build/test/run` 미실행.**
근거: 소스 정적 분석(`src/Unfold.Desktop/*.cs`), WCAG 2.1 상대 휘도 공식으로 직접 계산한 대비비,
`docs/validation/images/2026-09-20-*` 및 `artifacts/verification/pixel-editor-minimum.png`의 기존 캡처 육안 대조.
스크린 리더(VoiceOver/내레이터) 실행, Windows DPI 배율, 실제 키보드·마우스 조작은 수행하지 않았다.

## 요약 (핵심 차단 5줄)

1. **포커스 표시 완전 제거**: `DesignSystem.cs:94`가 `TextBox`/`NumericUpDown`/`ComboBox`의 기본 포커스 링(`FocusAdornerProperty`)을 명시적으로 `null`로 지우고, `:focus` 상태의 배경·테두리를 기본 상태와 동일하게 고정했다(`DesignSystem.cs:96-102`, `110-120`, `148`). 일부 필드는 `Ui.KeyboardFocusLabel`로 라벨 텍스트 색·문구 변경이 보완하지만, **`RoutineEditorWindow`의 3단계 안내·시간 입력 6개**(`RoutineEditorWindow.cs:25-30`)와 **`EditorWindow`(픽셀 에디터)의 Hex/FPS/브러시 입력**은 이 보완조차 없어 탭 이동 시 시각적으로 전혀 구별되지 않는다.
2. **휴식 시작·완료 버튼이 키보드로 기본 도달 불가**: 펫 말풍선(`PetSpeechBubble`)의 "휴식 시작/완료/5분 뒤에" 버튼은 리마인더가 뜰 때 자동으로 포커스를 받지 않는다. 키보드로 도달하는 유일한 경로는 시스템 트레이 메뉴의 "휴식 알림으로 이동"(`AppRuntime.cs:310,369-370` → `PetWindow.cs:114`)뿐이며, 자동 트리거 호출은 코드 전체에서 이 한 곳뿐이다.
3. **픽셀 에디터 좌측 도구 열이 스크롤 없이 잘림**: `EditorWindow.cs:47-67`의 도구 `StackPanel`(15개 컨트롤)이 `ScrollViewer`로 감싸지지 않아, 최소 창(980×700)에서 `artifacts/verification/pixel-editor-minimum.png`가 "Flip ↕" 버튼 하단 잘림과 "Clear frame" 버튼 미노출을 실제로 보여준다.
4. **비활성 텍스트 대비 미달(밝은 테마)**: OatLatte·Sage 테마의 `DisabledText`/`DisabledFill` 조합이 각각 3.58:1, 3.94:1로 4.5:1 기준 미달(WCAG는 비활성 컴포넌트를 대비 요건에서 제외하지만 가독성 참고용으로 보고).
5. **장식용 테두리(`Outline`/`OutlineSubtle`) 대비가 4개 테마 전부 1.5~2.2:1**로 3:1 미달이나, 실제 입력 필드 테두리는 `OutlineStrong`(`Line` 토큰)을 쓰고 있어 3.5~5.5:1로 통과 — 카드·프레임 구분선 등 장식 요소에 한정된 낮은 심각도 항목이다.

---

## 1. 대비 계산 표 (WCAG 2.1 상대 휘도 공식, 직접 계산)

계산 스크립트: `scratchpad/contrast.py` (session-local, 프로젝트 파일 아님). 공식: `L = 0.2126R+0.7152G+0.0722B`(sRGB→선형 변환 포함), `ratio = (L1+0.05)/(L2+0.05)`.
색상값 출처: `src/Unfold.Desktop/DesignSystem.Themes.cs:17-28`.

### 본문/보조 텍스트 (기준 4.5:1)

| 테마 | 조합 | 색상값 | 비율 | 판정 |
|---|---|---|---|---|
| OatLatte | Text on Canvas | #38342E / #F3EEE5 | 10.70:1 | PASS |
| OatLatte | Text on Surface | #38342E / #FFFFFF | 12.36:1 | PASS |
| OatLatte | Muted(보조·캡션) on Raised | #6A6257 / #E9E1D3 | 4.62:1 | PASS(근소) |
| Sage | Text on Canvas | #263D32 / #E6EDE8 | 9.82:1 | PASS |
| Sage | Muted on Raised | #50665A / #DCE7DC | 4.87:1 | PASS |
| MidnightBlue | Text on Canvas | #E7EFF6 / #101923 | 15.25:1 | PASS |
| MidnightBlue | Muted on Raised | #B6C6D2 / #2D4052 | 6.10:1 | PASS |
| Plum | Text on Canvas | #F5E9EB / #211C23 | 14.13:1 | PASS |
| Plum | Muted on Raised | #D3BCC8 / #463B45 | 5.98:1 | PASS |

본문·보조텍스트는 4개 테마·모든 배경(Canvas/Shell/Surface/Raised) 조합에서 전부 통과(4.62:1~15.25:1). 상세 24개 조합은 스크립트 출력 참고.

### 상태 텍스트: 오류/경고/성공 (기준 4.5:1)

| 테마 | 오류 Error | 경고 Warning | 성공 Success | 판정 |
|---|---|---|---|---|
| OatLatte | #A73D35/Canvas #F3EEE5 → 5.41:1 | #865D0C/Canvas → 5.07:1 | #386246/Canvas → 6.05:1 | 전부 PASS |
| Sage | #A23C36/Canvas → 5.45:1 | #805812/Canvas → 5.31:1 | #356B47/Canvas → 5.27:1 | 전부 PASS |
| MidnightBlue | #FFB4AE/Canvas → 10.45:1 | #E8C382/Canvas → 10.60:1 | #A1D4B7/Canvas → 10.64:1 | 전부 PASS |
| Plum | #FFB4AE/Canvas → 9.87:1 | #ECCE91/Canvas → 11.00:1 | #B9D6BC/Canvas → 10.68:1 | 전부 PASS |

### 비활성(Disabled) 텍스트 — 기준 4.5:1 (WCAG상 비활성 컴포넌트는 대비 요건 면제, 참고용)

| 테마 | DisabledText / DisabledFill | 비율 | 판정 |
|---|---|---|---|
| OatLatte | #807869 / #EEE8DE | **3.58:1** | FAIL |
| Sage | #627568 / #E0E8E0 | **3.94:1** | FAIL |
| MidnightBlue | #91A5B5 / #22303E | 5.29:1 | PASS |
| Plum | #B49CA9 / #392E39 | 5.09:1 | PASS |

밝은 테마 2종만 미달. 단, 비활성 버튼은 별도로 `Visual.OpacityProperty=1d`(`DesignSystem.cs:78-79`)를 강제해 Fluent 기본 반투명 처리도 없다 — 대비값 자체가 최종 렌더 색이다.

### Primary 버튼 텍스트 (기준 4.5:1)

| 테마 | OnAccent on Accent | OnAccent on AccentHover | 판정 |
|---|---|---|---|
| OatLatte | #FFFFFF/#756344 → 5.79:1 | 7.29:1 | PASS |
| Sage | #FFFFFF/#496B51 → 5.99:1 | 7.65:1 | PASS |
| MidnightBlue | #142A3A/#A8CADF → 8.56:1 | 10.17:1 | PASS |
| Plum | #37222E/#E3BBC4 → 8.51:1 | 10.35:1 | PASS |

### UI 컴포넌트 경계 (기준 3:1)

| 테마 | 입력 테두리(OutlineStrong=Line, 실사용) | 장식용 구분선(OutlineSubtle=Outline) | 판정 |
|---|---|---|---|
| OatLatte | #8C8171/Surface → 3.82:1 | #D2C8B8/Surface → 1.65:1 | 입력 PASS / 장식선 FAIL |
| Sage | #718878/Surface → 3.82:1 | #BFCFBF/Surface → 1.63:1 | 입력 PASS / 장식선 FAIL |
| MidnightBlue | #839CB0/Surface → 4.52:1 | #465A6C/Surface → 1.81:1 | 입력 PASS / 장식선 FAIL |
| Plum | #A2899D/Surface → 4.04:1 | #665164/Surface → 1.79:1 | 입력 PASS / 장식선 FAIL |

중요: `DesignSystem.cs:92,100`가 `TextBox`/`NumericUpDown`/`ComboBox`의 테두리를 `OutlineStrong`(Line)으로 설정하므로 **실제 입력 필드 경계는 4개 테마 모두 3:1을 통과**한다. `OutlineSubtle`(Outline 별칭, `DesignSystem.cs:16`)은 `Ui.PageFrame`의 창 프레임 테두리(`Ui.cs:104`)와 `BreakReviewWindow` 푸터 구분선 등 **장식용 분리선**에만 쓰여 기능 식별에 필수가 아니므로 WCAG 1.4.11 적용 대상이 아닐 가능성이 높다 — P2로 하향 보고.

### 대비 실패 총계

**8개 조합 실패** (테마×조합): OatLatte 3건(비활성 텍스트 2 + 장식선 2 → 중복 제외 시 4건), Sage 4건, MidnightBlue 2건(장식선만), Plum 2건(장식선만). 순수 텍스트 대비 실패는 **밝은 테마 2개의 비활성 텍스트 2건**뿐이며 WCAG상 면제 대상.

---

## 2. 접근성 이름(AutomationProperties) 전수 조사

전체 52건의 `AutomationProperties.*` 호출을 확인(`grep -rn "AutomationProperties" src/Unfold.Desktop/*.cs`). 아이콘 전용 버튼(`new Button`/`IconButton`/`.Content = PathIcon`) 생성 지점을 전수 대조한 결과:

| 파일:줄 | 컨트롤 | 이름 지정 여부 |
|---|---|---|
| `TimerControls.cs:28,32-35` | 타이머 일시정지/정지 아이콘 버튼 | SetName 있음 |
| `BreakReviewWindow.cs:251-253` | 기간 이동(◀▶↻) 아이콘 버튼 | SetName 있음 |
| `PetPackWindow.cs:152,157-161` | 미리듣기 정지/재생 아이콘 버튼 | SetName 있음 |
| `SettingsWindow.Layout.cs:295-298` | 좌측 내비게이션 아이콘 버튼 | SetName 있음 |
| `SettingsWindow.Themes.cs:18-20,35` | 테마 선택 아이콘/스와치 버튼 | SetName 있음 |
| `CustomPetWindow.cs:393-399` | 파일 선택/제거/미리보기 아이콘 버튼 | SetName 있음 |

**누락 발견 없음** — 코드베이스 내 모든 `new Button`은 `TimerControls.cs:32` 한 곳뿐이고, 나머지는 전부 `Ui.Button/Ui.Action(text)` 텍스트 버튼(콘텐츠=가시 텍스트라 접근성 이름이 자동 상속됨)이며, 아이콘 전용 버튼은 예외 없이 인접 줄에서 `SetName`을 호출한다.

라벨-입력 연결(`SetLabeledBy`)도 `Ui.Field`/`PetManagementView.Field` 헬퍼를 통해 일관 적용됨(`Ui.cs:37-43`, `PetManagementView.cs:51`). 단, 아래 §4의 `RoutineEditorWindow` 단계 입력과 `EditorWindow`(픽셀 에디터) 입력은 `SetName`은 있으나(`RoutineEditorWindow.cs:28-29`) `SetLabeledBy`/`Field` 패턴을 쓰지 않아 캡션과의 프로그램적 연결이 약함 — 근접 배치로 시각적으로만 연결됨(`RoutineEditorWindow.cs:30`, `EditorWindow.cs:47-67`).

---

## 3. 키보드 도달 불가 컨트롤 목록

| 컨트롤 | 파일:줄 | 문제 | 근거 |
|---|---|---|---|
| 펫 말풍선 "휴식 시작/완료/5분 뒤에" | `PetSpeechBubble.cs:26-28`, `PetWindow.cs:44-46,114` | 리마인더 표시 시 자동 포커스 없음. `PetWindow`는 `ShowActivated=false`로 생성되어 기본적으로 키보드 입력을 받는 활성 창이 되지 않는다. 유일한 자동 포커스 경로(`FocusReminder()` → `Activate()+bubble.FocusAction()`)는 시스템 트레이 메뉴 "휴식 알림으로 이동" 클릭에서만 호출됨(`AppRuntime.cs:310,369-370`). 전역 단축키(`KeyBinding`/`HotKey`) 없음(grep 결과 0건). | 정적 코드 확인. 실제 OS에서 Tab만으로 이 버튼에 도달되는지는 미검증. |
| 픽셀 에디터 "Clear frame" 버튼 | `EditorWindow.cs:67`, 좌측 도구 `StackPanel`(줄 47) | 최소 창(980×700)에서 스크롤 없는 고정 열 하단에 위치, 화면 밖으로 밀려남 | `artifacts/verification/pixel-editor-minimum.png` 육안 확인: "Flip ↕" 잘림, "Clear frame" 비노출 |
| 픽셀 캔버스 실제 그리기 동작 | `PixelCanvas.cs:23,69-77` | `Focusable=true`로 탭 진입은 가능하나 픽셀 배치 자체는 `OnPointerPressed`/`OnPointerReleased`만 처리 — 키보드로 실제 그리기 동작(색 채우기)을 수행할 대체 수단 없음 | 정적 코드 확인. 도구 전환(B/E/F/I/L/R/O)만 키보드 지원(`EditorWindow.cs:113-127`) |

---

## 4. 포커스 가시성 상세

`DesignSystem.cs`의 포커스 관련 토큰: `FocusRingWidth=2, FocusRingOffset=2`(줄 22, **선언만 되어 있고 실제 렌더링에 쓰이는 곳 없음** — grep 결과 `FocusRingWidth`/`FocusRingOffset` 참조 0건), `FocusRing` 브러시(`DesignSystem.Themes.cs:35`, 값=Accent)는 **`Ui.cs:51` 단 한 곳**, 즉 `Ui.KeyboardFocusLabel`의 라벨 텍스트 색상 변경에만 쓰인다.

**`docs/validation/2026-09-14-input-fields.md`가 언급한 "호버·포커스 배경·테두리 고정"**(제거)의 실체: `DesignSystem.cs:96-102`(TextBox/NumericUpDown/ComboBox 공통), `110-120`(TextBox 내부 Border), `121-133`(일반 ComboBox), `135-161`(NumericUpDown 내부 TextBox)에서 `:pointerover/:focus/:focus-within/:disabled` 상태의 Background/BorderBrush/BorderThickness를 전부 기본 상태와 **동일한 값**으로 고정했다. 추가로 `94`, `148`에서 `Control.FocusAdornerProperty = null`로 Avalonia 기본 포커스 링(점선 사각형)까지 명시적으로 제거했다.

**보완 메커니즘(`Ui.KeyboardFocusLabel`, `Ui.cs:44-53`)**: 입력이 Tab/방향키로 포커스를 받으면 위쪽 캡션 텍스트가 `"{label} · 선택"`으로 바뀌고 색이 Accent로 변한다. 이 보완이 적용된 곳과 안 된 곳:

| 보완 적용(양호) | 보완 미적용(포커스 시각 표시 전무) |
|---|---|
| `SettingsWindow.Layout.cs:139,165` (홈 탭 간격/자리비움/다시알림), `170-172` (알림간격/휴식시간), `91,99` (펫 크기/펫 선택) | `RoutineEditorWindow.cs:25-30` — 3단계 안내 TextBox 3개, 시간 NumericUpDown 3개 |
| `ProfileEditorWindow.cs:42-45` (Ui.Field 경유) | `EditorWindow.cs:23,27,53` — Hex TextBox, FPS NumericUpDown, 브러시 ComboBox (Ui.Field 미사용, `grep`으로 Field(/KeyboardFocusLabel 호출 0건 확인) |
| `CustomPetWindow.cs:142,385` (PetManagementView.Field/Ui.Field 경유) | |
| `PetPackWindow.cs:118,121` (PetManagementView.Field 경유) | |
| `SettingsWindow.Notifications.cs:34` (bubbleDirection, 수동 호출) | |
| `Ui.cs:177` (Prompt 다이얼로그 이름 입력, Field 경유) | |

버튼류(`unfold-action`/`primary` 클래스)는 `DesignSystem.cs:80-83`에서 `:focus-visible` 시 `BorderBrush`를 `Cream`/`Ink`로 바꿔 **테두리 기반 포커스 표시가 유지**된다. `pet-action-preview`(펫 카드 미리보기 버튼)도 `:focus-visible`에서 1px `Cream` 테두리(`DesignSystem.cs:290`)가 생긴다.

---

## 5. 최소 창 크기·반응형 위험 표

| 창 | 기본 | 최소 | 스크롤 여부 | 위험 | 근거 |
|---|---|---|---|---|---|
| SettingsWindow | 1120×800 | 860×680 | `Ui.PageBodyScroll`(있음, `Ui.cs:69-80`) | 낮음 — 실측 캡처에서 긴 펫 이름 2줄 래핑·저장 오류 문구 모두 정상 표시 | `docs/validation/images/2026-09-20-home-ui-implementation/home-minimum.png`, `home-minimum-long-name-error.png` 육안 확인 |
| BreakReviewWindow | 620×650 | 560×600 | `Ui.Page`→scroll 있음 | 낮음 — 캡처상 "CSV 내보내기"·"닫기" 버튼 모두 가시 | `docs/validation/images/2026-09-20-review-ui-implementation/review-compatibility-minimum.png` 육안 확인 |
| **EditorWindow(픽셀 에디터)** | 1140×820 | 980×700 | **좌측 도구 열 스크롤 없음**(`EditorWindow.cs:47-99`), 우측 인스펙터만 `ScrollViewer`(줄 `right = new ScrollViewer{...}`) | **높음 — 하단 컨트롤 잘림 확인됨** | `artifacts/verification/pixel-editor-minimum.png` 육안 확인: "Flip ↕" 텍스트 잘림, "Clear frame" 미노출 |
| PetPackWindow | 520×850 | 480×560 | `PetManagementView.Page` 패턴 사용(scroll 추정, 미직접 캡처 확인) | 중간 — 기본↔최소 높이차 290px로 가장 큼. 미리보기는 `size.SelectionChanged`로 적응(`PetPackWindow.cs:100`)하나 실제 최소 캡처 미확보 | 코드 확인, 캡처 미검증 |
| CustomPetWindow | 660×880 | 520×620 | `Ui.PageFrame` 사용 + `previewSurface` 높이 적응형(`CustomPetWindow.cs:213`, `ClientSize.Height<740` 시 156px로 축소) | 낮음 — 의도적 반응형 처리 확인됨 | 코드 확인 |
| PersonalizationWindow | 640×620 | 600×580 | `Ui.PageContent` 사용(scroll 있음), 단 `routines`/`profiles` `ListBox` 각 고정 `Height=230`(`PersonalizationWindow.cs:25-26`) | 낮음 — `ListBox` 자체 내부 스크롤 보유, 외곽 페이지 스크롤과 이중 방어 | 코드 확인, 캡처 미확보 |
| ProfileEditorWindow | 500×520 | `CanResize=false`(최소 개념 없음) | `Ui.Page` 사용(scroll 있음) | 낮음 — 창 크기 고정이라 축소 시나리오 자체가 없음 | 코드 확인 |
| RoutineEditorWindow | 500×700 | `CanResize=false` | `Ui.Page` 사용(scroll 있음) | 낮음(크기 고정) / **포커스 표시는 §4 참고(별도 문제)** | 코드 확인 |

---

## 6. 텍스트 크기 대응 (고정 높이 컨테이너)

| 위치 | 파일:줄 | 문제 |
|---|---|---|
| `PetSpeechBubble` | `PetSpeechBubble.cs:19,44-51`, `DesignSystem.cs:28-30` | 상태별 고정 `Height`(Advance 96 / Invitation 159 / Resting 196 / Completed 113)에 `title.TextWrapping=Wrap`은 적용되어 있으나(줄 23), 제목 문자열은 코드에 하드코딩된 고정 문구뿐이라(`"스트레칭할 시간이에요"` 등) 실사용 시 긴 사용자 정의 문자열이 들어갈 지점은 아님 — 위험 낮음. 단 향후 문구 길이가 늘면 고정 높이라 잘릴 수 있음(여유 없음). |
| 홈 화면 펫 이름 | `docs/validation/images/2026-09-20-home-ui-implementation/home-minimum-long-name-error.png` | 긴 펫 이름이 최대 2줄로 래핑되도록 구현됨(§ home-ui-implementation.md 기술) — 정상 동작 확인. 3줄 이상 넘는 이름은 결과 미검증(공백 없는 초장문 미테스트). |
| `EditorWindow` 미리보기 `Image` | `EditorWindow.cs:25` | `Height=160, Width=180` 고정, `Stretch=Uniform`이라 잘림 대신 축소 렌더링 — 텍스트 아님, 위험 없음. |

---

## 7. 상태 전달 (색상 외 수단)

- **타이머 상태 배지**: `timerStateDot`(색상 원, `SettingsWindow.cs:15`)은 항상 `state` 텍스트(`SettingsWindow.cs:15`, "진행 중"/"일시정지"/"중지" 등)와 함께 표시됨 — 색상 단독 전달 아님(코드·캡처 모두 확인, `home-minimum.png`에 "● 진행 중" 텍스트 병기).
- **저장 오류**: `homeTimingStatus.Foreground = DesignSystem.Error`와 함께 문구가 항상 표시(`SettingsWindow.cs:58-63`) — 색상+텍스트 병행.
- **선택된 테마/내비게이션 항목**: `SettingsWindow.Layout.cs:272` 등에서 `AutomationProperties.SetName`에 `" · 선택됨"` 텍스트를 추가해 스크린 리더에도 선택 상태 전달 — 시각적으로도 배경색 변경과 병행 추정(정적 코드로 배경 setter까지는 미대조).
- **체크박스**: Fluent 기본 체크마크 글리프(`CheckGlyph`, `DesignSystem.cs:200-202`) 사용 — 색상만이 아닌 형태(✓)로 상태 전달.
- **타이머 진행(카운트다운) 실시간 갱신**: `countdown`(`SettingsWindow.cs:14`) 텍스트가 매초 갱신되는 것으로 보이나, `AutomationProperties.LiveSetting` 등 스크린 리더 라이브 리전 지정 코드는 발견되지 않음(grep 결과 없음) — **동적 갱신이 스크린 리더에 자동 안내되는지 미검증**(정적 코드상 라이브 리전 마킹 없음, 실제 VoiceOver/내레이터 동작 확인 필요).

---

## 8. 움직임·소리 사용자 제어

- **효과음**: `AppSettings.ReminderSoundsEnabled`(`src/Unfold.Core/AppSettings.cs:22`, 기본 `true`)로 전체 껐다 켰다 가능, `ReminderSoundPlayer.cs:18`에서 `if (!settings.ReminderSoundsEnabled) return;`로 실제 차단 확인 — **소리 끄기 컨트롤 존재**.
- **펫 애니메이션(움직임)**: `AnimationView.cs`에 "모션 줄이기"·애니메이션 정지 옵션 없음. `showPet` 체크박스(`SettingsWindow.cs:65-70`)로 펫 전체를 화면에서 숨길 수는 있으나, 표시 상태에서 애니메이션 속도/유무를 조절하는 별도 컨트롤은 없음. OS 수준 "동작 줄이기" 설정을 조회해 반영하는 코드도 없음(grep 결과 없음) — **모션 감소 옵션 부재**, 전정기관 민감 사용자에게 영향 가능하나 심각도는 낮음(펫 자체를 숨기는 대안 존재).
- **소리로만 전달되는 정보**: 리마인더 발생 시 소리와 별개로 항상 말풍선 텍스트(`PetSpeechBubble`)가 동반 표시되는 구조(§7)라 소리 단독 정보 전달 사례는 발견되지 않음.

---

## 발견 표

| ID | 심각도 | 파일:줄 | 증상 | 영향받는 사용자 | 권장 조치 |
|---|---|---|---|---|---|
| A11Y-1 | **P0** | `DesignSystem.cs:94,96-102,110-120,148`; `RoutineEditorWindow.cs:25-30`; `EditorWindow.cs:23,27,53` | TextBox/NumericUpDown/ComboBox의 네이티브 포커스 링이 명시적으로 제거되고 `:focus` 배경·테두리가 기본 상태와 동일 — 라벨 보완(`KeyboardFocusLabel`)이 없는 루틴 편집 6개 입력·픽셀 에디터 3개 입력은 탭 이동 시 포커스 위치를 전혀 알 수 없음 | 키보드 전용 사용자, 저시력 사용자 | 최소한 이 두 창에도 `Ui.Field`/`KeyboardFocusLabel` 패턴 적용, 또는 `:focus-visible`에 테두리색 변경 추가 |
| A11Y-2 | **P0/P1** | `PetWindow.cs:44-46,114`; `PetSpeechBubble.cs:26-28`; `AppRuntime.cs:310,369-370` | 휴식 리마인더 말풍선의 핵심 액션(시작/완료/스누즈)이 표시 시 자동 포커스되지 않고, 키보드 도달 경로가 트레이 메뉴 한 곳뿐 | 키보드 전용 사용자 (핵심 앱 목적 자체를 완료 못 할 수 있음) | 리마인더 표시(`PetNotice` 전이) 시 `FocusReminder()`(또는 동등 로직)를 자동 호출하도록 연결 |
| A11Y-3 | **P1** | `EditorWindow.cs:47-99` | 픽셀 에디터 좌측 도구 열이 `ScrollViewer` 없이 고정 배치되어 최소 창(980×700)에서 하단 컨트롤(Flip ↕, Clear frame)이 잘리고 도달 불가 | 저해상도/작은 창 사용자, 저시력 확대 사용자 | 좌측 `tools` 패널도 우측 인스펙터처럼 `ScrollViewer`로 감싸기 |
| A11Y-4 | P2 | `DesignSystem.Themes.cs:19,22` (OatLatte/Sage `DisabledFill`/`DisabledText`) | 밝은 테마 2종의 비활성 텍스트 대비 3.58~3.94:1로 본문 기준(4.5:1) 미달(WCAG상 비활성 요소는 면제 대상) | 저시력 사용자(비활성 상태 텍스트 식별) | 필수는 아니나 `DisabledText`를 한 단계 더 어둡게 조정 권장 |
| A11Y-5 | P2 | `Ui.cs:104` (`PageFrame` 테두리), `DesignSystem.cs:16,44` (`Outline`=`OutlineSubtle`) | 4개 테마 전부 창 프레임·구분선 테두리 대비 1.5~2.2:1로 3:1 미달 — 단 장식용이라 WCAG 1.4.11 적용 여부 불확실 | 저시력 사용자(창 경계 인지) | 우선순위 낮음, 필요 시 `Line` 토큰으로 교체 검토 |
| A11Y-6 | P2 | `PixelCanvas.cs:69-77` | 실제 그리기(픽셀 배치)가 포인터 전용, 키보드 대체 수단 없음 | 마우스 사용이 어려운 사용자 | 커스텀 펫 제작이 필수 기능이 아니라면 낮은 우선순위, 필요 시 방향키+스페이스 배치 등 고려 |
| A11Y-7 | P2 | `AnimationView.cs` 전체 | 펫 애니메이션에 "모션 줄이기" 옵션 없음(전체 숨김만 가능) | 전정기관 민감 사용자 | OS 수준 reduce-motion 조회 또는 앱 내 애니메이션 속도 옵션 추가 검토 |
| A11Y-8 | P2 | `CustomPetWindow.cs`, `PetPackWindow.cs`, `EditorWindow.cs` | 이 3개 창은 `IsCancel=true` 버튼이 없어 Esc 키로 즉시 닫히지 않음(대신 타이틀바 닫기 시 `Closing` 핸들러의 확인 대화상자로 보호됨) | 키보드 전용 사용자 | Esc를 확인 대화상자 트리거로 연결하는 `KeyBinding` 추가 검토 |
| A11Y-9 | 정보 | 전체 코드베이스 | `AutomationProperties.SetName` 52건 전수 조사 결과 아이콘 전용 버튼 누락 없음 | — | 조치 불요, 회귀 방지용 스냅샷 참고 가치 |

---

## 검증 구분

**정적 코드로 확인한 사실**: DesignSystem.cs의 포커스 스타일 제거 범위(§4), AutomationProperties 전수 커버리지(§2), 창 크기 상수와 ScrollViewer 유무(§5), 사운드/모션 제어 코드 존재 여부(§8), PetWindow `ShowActivated=false`와 `FocusReminder` 호출 지점(§3의 A11Y-2).

**계산으로 확인한 사실**: 4개 테마 × 24개 전경/배경 조합의 WCAG 상대 휘도 대비비(§1) — `scratchpad/contrast.py`로 직접 계산, 원본 hex 값은 `DesignSystem.Themes.cs:17-28`에서 그대로 인용.

**기존 off-screen 캡처 육안 대조로 확인한 사실**: `pixel-editor-minimum.png`의 컨트롤 잘림(A11Y-3), `home-minimum.png`/`home-minimum-long-name-error.png`/`review-compatibility-minimum.png`의 최소 창 레이아웃 정상 여부(§5). 이 캡처들은 macOS native off-screen 렌더링이며 실제 포인터·키보드 조작을 촬영한 것이 아니다(`docs/validation/2026-09-20-home-ui-implementation.md` 명시).

**실제 OS·보조기술 미검증** (반드시 실기 확인 필요):
- VoiceOver(macOS)·Narrator(Windows) 실제 낭독 순서, 포커스 이동 시 실제 발화 여부, 타이머 텍스트 실시간 갱신의 라이브 리전 안내 여부(§7)
- Tab 키 실제 순회 순서가 시각 순서와 일치하는지(코드에 `TabIndex` 오버라이드가 없어 기본 시각 순서를 따를 것으로 추정되나 실기 미확인)
- `PetWindow`(`ShowActivated=false`)가 실제 macOS/Windows에서 `Activate()` 호출 시 정말 키보드 포커스를 받는지(§3, A11Y-2의 코드적 근거는 확실하나 런타임 동작은 플랫폼 의존적)
- Windows DPI 125/150/200% 배율에서의 레이아웃 재현
- 고대비 모드(OS 레벨) 및 브라우저/OS 폰트 크기 확대 시 텍스트 잘림
- 스크린 리더로 `AutomationProperties.SetLabeledBy` 연결이 실제로 "라벨: 값" 형태로 읽히는지

---

## 파일 소유권 / 팀 통신

- 발견된 원인은 모두 `src/Unfold.Desktop/DesignSystem.cs`, `RoutineEditorWindow.cs`, `EditorWindow.cs`, `PetWindow.cs`, `PetSpeechBubble.cs`, `AppRuntime.cs` — 공용 디자인 시스템(`DesignSystem.cs`) 이슈이므로 ui-visual-auditor와 공유 필요. 휴식 시작/완료 흐름(A11Y-2)은 핵심 사용자 흐름이므로 ux-flow-auditor와도 공유 필요.
- **감독자 즉시 보고 대상**: A11Y-1, A11Y-2(핵심 작업 키보드 도달 불가/포커스 불가), A11Y-3(잘림으로 인한 도달 불가).
