# 08 Reality Check — C1 (bbdb4a8 / 1.1.1-beta 기준)

## 1. 요약

- 검증 기준: 참고 사본 `unfold-src-1a74cbf`(`.source-head` = `bbdb4a8`)와 원본 워크트리
  `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`(HEAD `bbdb4a8`, 작업 트리 clean)의
  `src/` 전체를 `diff -rq`로 비교해 **차이 0건**을 확인했다. 아래 모든 줄 번호는 `bbdb4a8` 기준이다.
- 감독자 지정 8개 항목(A11Y-02, UX-01, UX-02/PW-01, UI-01+UI-10+A11Y-04, UI-02, BUG-D-01, BUG-C-01, BUG-C-09)을
  코드로 재확인했고, 7개 보고서 86건 전체를 ID 단위로 훑어 중복·낡음을 분류했다.
- 판정 결과: **그대로 채택 6건, 심각도 하향 5건, 심각도 상향 1건, 기각 1건, 낡음 3건, 중복 통합 24건 → 11 묶음**.
- 대비는 `DesignSystem.Themes.cs`의 hex로 직접 재계산했다(WCAG 상대휘도 공식, 스크립트는 scratchpad `contrast.py`).
  UI-10과 A11Y-04의 수치는 **재계산값과 일치**했고, UI-01의 수치는 보고서보다 **더 나쁜 1.04:1**이었다.
- 가장 중요한 정정 2건: (a) **A11Y-02의 루틴 편집기는 일반 UI에서 도달 불가**(진단 전용) → 기각·A11Y-07로 흡수,
  (b) **"무료 체험 없음"은 결함이 아니라 문서화된 제품 결정**(`docs/mvp.md:67` "no free trial") → 해당 주장 기각,
  단 "로그인 전 가격·약관 고지 부재"는 유효.

---

## 2. 판정표

### 2.1 감독자가 지정한 8개 항목

| 원본 ID | 제목 | 원본 심각도 | 판정 | 조정 심각도 | 검증 근거 | 이유 |
|---|---|---|---|---|---|---|
| A11Y-02 | 루틴 편집기 단계 입력 6개에 포커스 표시 전무 | P1 | **기각 (중복 흡수)** | — | `RoutineEditorWindow` 생성자는 `PersonalizationWindow.cs:126`과 `SmokeDiagnostics.cs:87,98`에서만 호출. `PersonalizationWindow`는 `SmokeDiagnostics.cs:106`에서만 생성(전체 `src/` grep, 다른 참조 0건). `docs/mvp.md:39,60`이 루틴/프로필 설정 UI 제거를 명시 | 일반 사용자가 도달할 수 없는 진단 전용 창. 또한 포커스 표시 부재의 원인은 이 창이 아니라 `DesignSystem.cs:118`의 전역 `FocusAdornerProperty=null`이므로 A11Y-07과 같은 결함이다. 독립 항목으로 유지하면 중복 집계 |
| UX-01 | 펫 팩 `저장`이 설치된 펫을 확인 없이 덮어씀 | P1 | **심각도 하향** | P2 | `PetPackWindow.cs:61`(`Ui.Button("저장", () => _ = InstallPack())`), `:209-230`(`InstallPack`에 `Ui.Confirm` 호출 없음 — `Ui.cs:219`의 `Confirm`은 `PetPackWindow.cs`에서 0회 호출). **반증 근거**: `:179-182`가 클릭 전에 `"교체 준비 완료"/"업데이트 준비 완료"/"재설치 준비 완료"`를 표시하고 `:178`이 `설치된 버전 N`을 함께 노출. `CharacterPack.cs:186-187`은 `source.piskel`이 있는(= 사용자가 직접 그린) 펫의 교체를 `InvalidDataException`으로 **차단** | 확인 대화상자 부재는 사실이고 되돌리기도 불가(`CharacterPack.cs:225` 성공 후 `.backup-*` 삭제). 그러나 ① 클릭 전 상태 문구로 교체임을 알리고 ② 사용자 원본 작업물은 코드로 보호된다. 손실 범위는 "재설치 가능한 외부 팩의 이전 버전"이므로 P1(데이터 손실)이 아니라 P2(확인 단계 누락) |
| UX-02 / PW-01 | 로그인 화면에 가격·1회 결제·약관 고지 없음 | P1 / P1 | **일부 채택 / 일부 기각** | P1 (고지 부재) | `Assets/Account/entry-screen.json` 전문 확인: `약관`·`개인정보`·`terms`·`privacy` 문자열 **0건**(`AccountWindow.axaml`, `AccountScreenContent.cs`도 0건). 로그인 단계에 노출되는 것은 `loginStep`("로그인 · 회원가입"), `welcomeTitle`, `googleButton`, `quitButton`, `codeButton`뿐. 가격 블록은 `AccountWindow.axaml:48-52`에서 `IsVisible="{Binding IsPurchase}"`로 **결제 단계에만** 표시되고, 거기서는 `productLabel`("Unfold · 1회 구매") + `Price`(KRW 4,900 / US$3.99, `AccountScreenModel.cs:46`)가 모두 보인다 | **채택**: 유료 전환 전 가격·약관·개인정보 고지가 없다. 결제 단계에는 가격·1회 구매 표기가 있으므로 "가격 표기 자체가 없다"는 서술은 부정확 — 문제는 *노출 시점*이다. **기각**: "무료 체험 없음"은 `docs/mvp.md:67`이 `**no free trial**`로 못 박은 제품 결정이므로 결함이 아니다 |
| UI-01 | 비활성 버튼이 활성과 거의 같은 면색 | P1 | **심각도 하향 (수치는 상향)** | P2 | 재계산: `DisabledFill` vs `Raised` = **1.04 / 1.04 / 1.04 / 1.09** (Plum/Oat/Sage/Midnight). 보고서보다 나쁘다. 원인 확인: `DesignSystem.cs:100-103`이 `:disabled`에 `DisabledFill`을 깔고 `unfold-action`의 `Opacity`를 `1d`로 **고정**해 Fluent 기본 투명도 단서까지 제거 | 면색 단서가 사실상 없음은 확인. 다만 전경색이 `Cream`→`DisabledText`로 바뀌어 상태 단서가 완전히 0은 아니고, WCAG 1.4.3/1.4.11은 비활성 컨트롤을 면제한다. 접근성 위반이 아닌 가용성 결함이므로 P2 |
| UI-10 | 밝은 테마 표면 단계·카드 테두리 대비 1.1~1.55:1 | P2 | **채택** | P2 | 재계산: `Surface`vs`Canvas` 1.10~1.22, `Shell`vs`Canvas` 1.06~1.11, `Surface`vs`Shell` 1.04~1.10, `Outline`vs`Surface` **1.52~1.73**(밝은 테마 최대 1.55 = Oat). 보고서 수치와 일치 | 4개 토큰 쌍 전부 3:1 미달. UI-01·A11Y-04와 같은 팔레트 근접도 문제이므로 ISSUE-06으로 통합 |
| A11Y-04 | 보조 버튼 경계 대비 1.06~1.37:1 | P2 | **채택 (범위 축소)** | P2 | 재계산: `Raised`vs`Canvas` = **1.06 / 1.09 / 1.06 / 1.37** — 보고서 수치와 소수점까지 일치. 근거 줄도 정확(`DesignSystem.cs:67-69`: `Background=Raised`, `BorderBrush=Transparent`). **반증**: `quiet` 클래스는 `:78-79`에서 `BorderBrush=OutlineStrong`을 받고, `Line`vs`Shell` = 3.09~4.77 / `Line`vs`Surface` = 3.29~4.32로 **3:1 통과** | 기본 `unfold-action`(채움형)에만 해당한다. "보조 버튼 전체"로 쓰면 과장 — `quiet` 변형은 기준을 넘는다. 단 `Line`vs`Canvas`는 Plum 2.91로 미달이므로 캔버스 위 quiet 버튼은 예외 |
| UI-02 | 확인 대화상자에서 danger가 있으면 primary가 사라짐 | P2 | **채택 (서술 정정)** | P2 | `Ui.cs:227-231`: `if (danger 라벨) Danger(button); else if (index == 0) Primary(button);` — `choices[0]`이 파괴적 라벨이면 `Primary`가 **아무 버튼에도 적용되지 않는다**(확인됨). 라벨 문자열 판정도 확인: `Contains("삭제")`, `EndsWith("제거")`, 그리고 `"버리기"/"종료"/"중지"/"저장 안 함"/"초기화"/"Delete"/"Discard"/"Crop"` 하드코딩 | 두 주장 모두 사실. 다만 `Danger()`는 `DesignSystem.cs:80-81`에서 **전경색만** `Error`로 바꾸므로 버튼은 `Raised` 채움 + 빨간 글자로 남고, `"취소"`는 `:228`에서 `Quiet()`(투명 + `OutlineStrong` 테두리)를 받는다. 즉 두 버튼의 **구분은 유지**된다. "위계가 사라진다"는 과장이고, 실질 결함은 **라벨 문자열 기반 분기의 취약성**("제거하기"처럼 어긋나는 라벨은 파괴적 동작에 `Primary`가 붙는다) |
| BUG-D-01 | Windows 클릭 통과의 layered 잔존 | P1 | **심각도 하향** | P2 | `WindowsPetWindow.cs:26`: `desired = enabled ? original \| Layered \| Transparent : original & ~Transparent;` — 해제 시 `Transparent`만 제거하고 `Layered`는 유지. **확인됨**. 코드로 판단 가능한 범위: `:32-34` 주석이 "기존 레이어에 `SetLayeredWindowAttributes`를 부르면 `UpdateLayeredWindow` 렌더 표면을 무효화할 수 있다"고 이미 인지하면서 **반대 방향(레이어를 남기는 쪽) 위험은 다루지 않는다**. 추가한 레이어는 `:71`의 `SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA)`로 alpha=255 초기화되어 있어 **즉시 보이는 투명도 변화는 없다**. 펫 창은 `PetWindow.cs:55`에서 `TransparencyLevelHint = [WindowTransparencyLevel.Transparent]`를 쓰고 `Win32PlatformOptions`를 재정의하지 않으므로 Avalonia.Desktop **12.1.2** 기본 컴포지터 경로를 탄다 | 비대칭 복원은 코드 결함으로 확정. 그러나 Avalonia 12.1.2가 투명 창을 DirectComposition으로 그리는지, 잔존 `WS_EX_LAYERED`가 그 표면과 충돌하는지는 **저장소 안에서 판정 불가**. 사용자 영향은 미확정이므로 P1 유지 근거 없음 → P2 + 실기 확인 항목(§5-1) |
| BUG-C-01 | 시계 점프 시 기록 전체 삭제 | P0 | **심각도 하향** | P1 | `BreakHistory.cs:40` `completions.RemoveAll(previous => previous.CompletedAt < completedAt.AddDays(-90));` — 확인됨. 호출 경로는 `Add`(`:34`) **단 하나**이고 `Load`(`:18-33`)는 정리하지 않는다. 다른 90일 정리 경로 **없음**(`AddDays(-90)` grep 1건). 호출부 `AppRuntime.cs:564`가 `DateTimeOffset.Now`를 넘기고 `:566`에서 즉시 `Save` → `AtomicFile.Write`로 전체 교체, 백업 없음. `Validate`(`:64-71`)는 미래 `CompletedAt`을 거르지 않는다 | 영향(전체·영구 소실, 테스트 0건)은 P0급이지만 **트리거 조건이 비대칭으로 좁다**: 시계가 **90일 이상 미래로** 이동해야 한다. CMOS 방전·NTP 글리치 같은 흔한 시나리오는 과거 방향이거나 수 시간 단위여서 `completedAt.AddDays(-90)` 컷오프가 더 과거로 밀리므로 **무해하다**(RemoveAll이 0건). 실제로 걸리는 경로는 사용자의 수동 날짜 변경, 오래된 VM 스냅샷 재개 정도다. 저확률·고영향 + 수정 비용 낮음 → P1 |
| BUG-C-09 | 78~90일 구간 기록 접근 불가 | P3 | **채택 (범위 정정)** | P3 | `BreakReviewWindow.cs:302` `requested < today.AddDays(-77) ? today.AddDays(-77) : requested`, `:154` `previous.IsEnabled = endDay > today.AddDays(-77)`. `Review(endDay)`는 `BreakHistory.cs:54`에서 `endDay.AddDays(-6)`부터 7일을 만든다. 따라서 **도달 가능한 최과거일은 `today-83`**이고 보존은 90일 → **접근 불가 구간은 `today-84` ~ `today-90`의 7일**이다. 보고서의 "78~90일"은 부정확 | `-77`은 7의 배수(11주)로 고른 의도적 경계지만 90일 보존 기준과 정렬되지 않았다. 의도된 범위라기보다 정렬 누락. 영향 작음 → P3 유지 |

### 2.2 낡은 항목 (`bbdb4a8`에서 전제가 바뀜)

| 원본 ID | 제목 | 판정 | 근거 |
|---|---|---|---|
| BUG-D-14 | 알림 말풍선 표시 중 드래그하면 방향이 매 초 뒤집힘 | **낡음 → 심각도 하향** (P3 유지, 근거 교체) | 보고서가 인용한 `PetSpeechBubble.cs:139-162`(`CreateExpanded`의 방향 후보 재선택)는 **더 이상 알림 배치 경로가 아니다**. `bbdb4a8`의 `PetWindow.cs:265-267`은 `PetBubbleLayout.AutomaticDirection` + `CreateSurface`를 쓰고, `AutomaticDirection`(`PetSpeechBubble.cs:167-168`)은 "펫이 작업 영역 좌/우 절반 중 어디인가"만 보는 **결정적** 좌/우 선택이다. 따라서 ① 위↔아래 플립은 발생하지 않고, ② 같은 위치에서 매 초 진동하지도 않는다. 남는 현상은 `keepHoverLayout`(`PetWindow.cs:246-247`)이 `hover`에만 걸리므로 알림 중 드래그로 **화면 중앙선을 넘을 때 좌↔우가 1회 바뀌는 것**뿐이다 |
| UX-03 | 설정 자동 저장인데 성공 피드백 없음 | **발견은 채택 / 시나리오 서술은 낡음** (P2) | 발견 자체는 재확인됨: `SettingsWindow.Preferences.cs:27`의 `preferencesStatus`는 `Foreground = DesignSystem.Error`로 생성되고 `:76-78 ShowPreferencesError`에서만 표시되며, 성공 경로(`:102`, `:143`)는 `IsVisible = false`로 끈다. 성공 전용 문구·토큰이 없다. 다만 보고서의 시나리오 문장("사용자가 **말풍선 위치를 '위'로** 바꾸고")은 `bbdb4a8`에서 방향 ComboBox가 제거되어 성립하지 않는다. 같은 카드에서 실제로 반응 없는 컨트롤은 `BubbleOpacityPercent` 슬라이더와 5종 대사 입력(`:13-16`)이다 |
| EV-04 / EV-05 및 03 보고서의 캡처 인벤토리 | 설정 화면 캡처 근거 | **캡처 낡음** | 캡처는 `1a74cbf`에서 뽑혔고, 03 보고서 인벤토리에 `settings-direction-popup`("말풍선 방향 선택(위/아래/왼쪽/오른쪽), 현재 '위'") 행이 남아 있다. `bbdb4a8` 설정 UI에는 해당 ComboBox가 없다(`SettingsWindow.Preferences.cs`에 `BubbleDirection` 참조 0건; 남은 참조는 `SmokeDiagnostics.cs:148-154`와 레이아웃 내부뿐). `bbdb4a8` 기준 재진단은 Avalonia 렌더 타이머 시작 실패(native error -6661)로 실행하지 못했으므로, **설정 화면 캡처에 의존하는 모든 판단은 재캡처 전까지 미확정**이다 |

### 2.3 상향 1건

| 원본 ID | 제목 | 원본 | 판정 | 조정 | 근거 |
|---|---|---|---|---|---|
| A11Y-10 | 말풍선 고정 높이로 긴 사용자 대사가 말줄임 | P3 | **심각도 상향** | P2 | `bbdb4a8`가 **120자 사용자 입력 대사 5종**을 새로 추가했다(`SettingsWindow.Preferences.cs:16` `MaxLength = 120`). 말풍선 높이는 여전히 알림 종류별 상수(`PetSpeechBubble.cs:76-83` → `DesignSystem.cs:30-31` `SpeechAdvanceHeight=96` … `SpeechRestingHeight=196`)이고 폭은 `SpeechBubbleWidth=320` 고정, 제목은 `:29`에서 `TextTrimming.CharacterEllipsis`다. 즉 **사용자가 설정에서 입력한 문장이 소리 없이 잘릴 수 있는 새 표면이 생겼다**. 완화책은 `:88`의 `ToolTip.SetTip(title, title.Text)`뿐으로 마우스 전용이다 |

### 2.4 기타 확인 메모 (원본 서술의 부분 오류)

| 원본 ID | 메모 |
|---|---|
| PW-11 | "미저장 이탈 경고가 없음"은 부정확하다. `SettingsWindow.cs:246-250`이 되돌림 **후** `"저장하지 않은 변경을 되돌렸어요."`를 `Warning` 색으로 표시한다. 유효한 부분은 `:160` `homeTimingApply.Opacity = changed ? 1 : 0`(버튼 자체가 보이지 않음)이며 이는 UX-04와 동일 결함이다 |
| UX-05 / PW-02 | `SettingsWindow.cs:245`의 `ToolTip.SetTip(interval, …)` 확인. 비활성 컨트롤이 툴팁을 띄우지 못하는 것은 Avalonia 기본 동작이므로 발견 유효. 단 같은 블록 `:243-244`가 `homeTimingStatus`를 **비우고 숨기므로**, 화면에 남는 설명이 0이라는 점이 핵심 근거다 |
| A11Y-07 | 확인됨, 그리고 보고서보다 강하다. `DesignSystem.cs:112-125`가 `TextBox`/`NumericUpDown`/`ComboBox`에 `FocusAdornerProperty=null`을 주고, **이어지는 `:121-125`의 `:focus`/`:focus-within` 스타일이 `BorderBrush`를 기본값과 같은 `OutlineStrong`으로 되돌린다**. 즉 포커스 상태 스타일이 존재하면서 아무 변화도 만들지 않는다. 반면 `Button`은 `:104-107`에서 `:focus-visible` 테두리(`Cream`/`Ink`)를 받으므로 버튼은 영향 없다 |
| A11Y-03 | 확인됨. `BreakReviewWindow.cs:176-181`의 `ToggleButton`은 `BorderThickness=0`·`BorderBrush=Transparent`이고, `:198-213 AddDateHeaderStyles`는 `:pointerover`·checked presenter 배경만 추가해 **포커스 상태를 전혀 다루지 않는다** |
| A11Y-12 | 재계산 `DisabledText` vs `DisabledFill` = 3.43 / 3.56 / 3.33 / 4.85. 보고서의 "3.33~3.56"은 밝은 3테마 기준으로 정확. WCAG 비활성 면제 적용 → P3 적정 |
| UI-03 | `DesignSystem.Success` 토큰의 `src/Unfold.Desktop` 전체 사용처는 **`SettingsWindow.cs:211` 1곳**(타이머 상태 캡션)뿐이다. 즉 "성공 피드백이 중립 회색"이라는 주장의 전제(성공 토큰이 피드백에 쓰이지 않음)는 코드로 확인된다. 토큰 자체의 대비는 충분하다(`Success`vs`Surface` 6.14~8.30) → 색 선택이 아니라 **적용 누락** 문제다 |
| EV-01 | 감독자 확정 항목. `SettingsWindow.Layout.cs:311` `accountSignOut.IsEnabled = !runtime.AccountSignOutPending;` — `runtime.AccountSession` null 검사가 없음을 재확인했다(`:309`에서 session을 읽어 이메일 표시에만 쓴다) |

---

## 3. 중복 통합표

| 통합 ID | 묶인 원본 ID | 공통 제목 | 최종 심각도 | 소유 후보 파일 |
|---|---|---|---|---|
| ISSUE-01 | BUG-C-01 | 90일 기록 정리가 호출 시각만 신뢰해 시계 전진 시 전체 기록을 영구 삭제 | P1 | `src/Unfold.Core/BreakHistory.cs` |
| ISSUE-02 | BUG-C-02, BUG-C-03, BUG-C-06 | 기록·설정 손상 처리의 비대칭 — 기록은 격리·복구가 없고 영구 비활성되며, 설정은 `.invalid-*`가 무한 누적 | P1 | `src/Unfold.Desktop/AppRuntime.cs`, `src/Unfold.Core/BreakHistory.cs`, `src/Unfold.Core/AppSettings.Recovery.cs` |
| ISSUE-03 | UX-02, PW-01, UX-11 | 유료 전환 지점의 고지 부재 — 로그인 전 가격·1회 구매·약관·개인정보 안내가 없고, 실패 문구가 원인·다음 행동을 주지 않음 | P1 | `Assets/Account/entry-screen.json`, `src/Unfold.Desktop/AccountWindow.axaml` |
| ISSUE-04 | A11Y-02, A11Y-03, A11Y-07, A11Y-14 | 키보드 포커스 표시 부재 — 입력 3종의 포커스 스타일이 무효이고, 회고 날짜 토글·네비게이션 레일은 포커스·선택 역할이 없음 | P1 | `src/Unfold.Desktop/DesignSystem.cs`, `BreakReviewWindow.cs`, `SettingsWindow.Layout.cs` |
| ISSUE-05 | EV-08, EV-09 | 검증 문서가 저장된 테스트 결과를 과장(746/748 → "748 모두 통과")하고 같은 날짜 문서끼리 모순 | P1 | `docs/validation/2026-10-06-audit-followup.md` |
| ISSUE-06 | UI-01, UI-10, A11Y-04, A11Y-08, UI-11, UI-14, EV-11 | 팔레트 근접도 — 표면 단계·카드 테두리·채움형 버튼 면·비활성 상태·말풍선 테두리·테마 스와치가 모두 3:1 미달이고 서로 다른 의미의 토큰이 같은 값으로 붕괴 | P2 | `src/Unfold.Desktop/DesignSystem.Themes.cs`, `DesignSystem.cs` |
| ISSUE-07 | UX-03, UX-04, PW-11 | 저장 모델 불일치 — 설정은 자동 저장인데 성공 피드백이 없고, 홈은 수동 저장인데 버튼이 `Opacity=0`으로 숨음 | P2 | `src/Unfold.Desktop/SettingsWindow.Preferences.cs`, `SettingsWindow.cs` |
| ISSUE-08 | UX-01, PW-07, UX-07 | 펫 팩 설치 흐름 — `저장` 라벨이 설치·교체를 숨기고 확인 단계가 없으며, 임베드된 내부 페이지가 제목·설명을 잃음 | P2 | `src/Unfold.Desktop/PetPackWindow.cs`, `Ui.cs` |
| ISSUE-09 | UX-06, A11Y-01, A11Y-05, PW-14 | 말풍선 시간 제약이 고정·비공개 — 초대 30초 자동 미루기와 공지 5초 소멸에 예고·사후 안내·조정 수단이 없고 보조기술에도 알려지지 않음 | P2 | `src/Unfold.Core/PetReminder.cs`, `src/Unfold.Desktop/PetSpeechBubble.cs` |
| ISSUE-10 | PW-04, PW-09, UX-09, EV-13 | 용어·라벨 불일치 — 동작 키 한국어 이름 3종, 미루기 용어 3종, `펫 선택`이 두 화면에서 다른 의미, 펫 목록 이름 표기 혼용 | P2 | `src/Unfold.Core/CustomPetDraft.cs`, `src/Unfold.Desktop/PetBuilderView.cs`, `SettingsWindow.cs`, `PetSpeechBubble.cs` |
| ISSUE-11 | UX-10, PW-12, A11Y-09 | 펫 창의 발견성·복구 — 우클릭 메뉴 2항목뿐이고 `펫 숨기기` 복구 경로를 알리지 않으며 키보드 진입이 트레이 1경로뿐 | P2 | `src/Unfold.Desktop/PetWindow.cs`, `AppRuntime.cs` |

통합되지 않은 단독 유효 항목(심각도 그대로): BUG-C-04, BUG-C-05, BUG-C-07, BUG-C-08, BUG-C-09, BUG-C-10, BUG-C-11, BUG-C-12,
BUG-D-01~D-13, UI-02, UI-03~UI-09, UI-12, UI-13, UI-15, UX-05/PW-02(쌍), UX-08, UX-12, UX-13, UX-14, UX-15,
PW-03, PW-05, PW-06, PW-08, PW-10, PW-13, PW-15, A11Y-06, A11Y-10, A11Y-11, A11Y-12, A11Y-13,
EV-01~EV-03, EV-06, EV-07, EV-10, EV-12, EV-14, EV-15.

---

## 4. 직접 재계산한 대비표

방법: `DesignSystem.Themes.cs:17-31`의 hex → sRGB 역감마 → WCAG 상대휘도 → `(L1+0.05)/(L2+0.05)`.
기준: 비텍스트/UI 경계 3:1(WCAG 1.4.11), 텍스트 4.5:1(1.4.3). 스크립트: scratchpad `contrast.py`.

| 토큰 쌍 | 유연한 라일락 | 다정한 오트 | 숨 고르는 숲 | 밤의 버터 | 최저 | 판정 |
|---|---|---|---|---|---|---|
| `DisabledFill` vs `Raised` (UI-01 비활성 vs 활성 면색) | 1.04 | 1.04 | 1.04 | 1.09 | **1.04** | 미달 (3:1) |
| `Raised` vs `Surface` (A11Y-04/UI-10 채움 버튼 vs 카드) | 1.20 | 1.21 | 1.17 | 1.12 | **1.12** | 미달 |
| `Raised` vs `Shell` | 1.13 | 1.15 | 1.13 | 1.24 | **1.13** | 미달 |
| `Raised` vs `Canvas` (A11Y-04 인용 범위) | 1.07 | 1.09 | 1.06 | 1.37 | **1.06** | 미달 — 보고서 "1.06~1.37" 일치 |
| `Outline` vs `Surface` (UI-10 카드 테두리) | 1.54 | 1.55 | 1.52 | 1.73 | **1.52** | 미달 — 보고서 상한 1.55 일치 |
| `Outline` vs `Shell` | 1.45 | 1.48 | 1.47 | 1.90 | **1.45** | 미달 |
| `Outline` vs `Canvas` | 1.36 | 1.39 | 1.39 | 2.11 | **1.36** | 미달 |
| `Surface` vs `Canvas` (UI-10 표면 단계) | 1.13 | 1.12 | 1.10 | 1.22 | **1.10** | 미달 — 보고서 하한 1.1 일치 |
| `Shell` vs `Canvas` | 1.06 | 1.06 | 1.06 | 1.11 | **1.06** | 미달 |
| `Surface` vs `Shell` | 1.07 | 1.05 | 1.04 | 1.10 | **1.04** | 미달 |
| `Hover` vs `Surface` (호버 가시성) | 1.32 | 1.30 | 1.24 | 1.22 | **1.22** | 미달 |
| `Hover` vs `Raised` (버튼 호버) | 1.10 | 1.07 | 1.06 | 1.09 | **1.06** | 미달 |
| `Line`(=`OutlineStrong`) vs `Shell` (quiet 버튼 테두리) | 3.09 | 3.23 | 3.35 | 4.77 | **3.09** | **통과** — A11Y-04 반증 |
| `Line` vs `Surface` | 3.29 | 3.40 | 3.48 | 4.32 | **3.29** | **통과** |
| `Line` vs `Canvas` | 2.91 | 3.04 | 3.17 | 5.28 | **2.91** | 미달 (라일락만) |
| `DisabledText` vs `DisabledFill` (A11Y-12) | 3.43 | 3.56 | 3.33 | 4.85 | **3.33** | 4.5 미달 / 비활성 면제 |
| `Text` vs `Canvas` (본문) | 12.06 | 11.84 | 10.34 | 14.22 | **10.34** | 통과 |
| `Muted` vs `Canvas` (보조 텍스트) | 4.86 | 5.21 | 4.92 | 8.77 | **4.86** | 통과 |
| `Muted` vs `Surface` | 5.49 | 5.82 | 5.40 | 7.19 | **5.40** | 통과 |
| `Accent` vs `Canvas` | 5.84 | 5.14 | 6.57 | 10.57 | **5.14** | 통과 |
| `OnAccent` vs `Accent` (주 버튼 텍스트) | 6.74 | 5.88 | 7.43 | 8.54 | **5.88** | 통과 |
| `Success` vs `Surface` (UI-03) | 6.14 | 6.20 | 7.21 | 8.30 | **6.14** | 통과 — 색이 아니라 적용 누락 문제 |

**해석**: 세 발견(UI-01·UI-10·A11Y-04)은 서로 다른 증상이지만 **단일 원인**이다 — 네 팔레트 모두
`Canvas`/`Shell`/`Surface`/`Raised`/`DisabledFill`/`Hover`가 1.04~1.37:1 안에 몰려 있다.
반면 텍스트 토큰(`Text`/`Muted`/`OnAccent`)과 `Line`은 전부 기준을 넘는다. 즉 **면·면 경계만 실패하고
텍스트 대비는 건강하다**. 따라서 수정은 토큰 전면 재설계가 아니라 `Raised`·`DisabledFill`·`Outline`·`Hover`
4개 값의 명도 분리로 충분하며, ISSUE-06 하나로 묶어 `DesignSystem.Themes.cs`에서 처리하는 것이 맞다.

---

## 5. 실기·실행으로만 확정 가능한 항목

| # | 항목 | 왜 코드로 확정 불가 | 확인 절차 |
|---|---|---|---|
| 1 | BUG-D-01 잔존 `WS_EX_LAYERED`의 실제 렌더 영향 | Avalonia.Desktop 12.1.2의 Win32 백엔드가 `WindowTransparencyLevel.Transparent`를 DirectComposition으로 처리하는지, 그 표면이 `WS_EX_LAYERED`와 공존 가능한지는 패키지 내부 동작이며 저장소에 근거가 없다 | 실제 Windows에서 ① 펫 표시 → ② 커서를 펫 위로 올려 클릭 통과 해제 → ③ 커서를 빼서 재설정, 이 사이클을 20회 반복하며 펫 알파·말풍선 테두리·배경 비침을 스크린샷으로 비교. `GetWindowLongPtr(hwnd, -20)`을 매 사이클 로깅해 `Layered` 비트 잔존을 확인. 추가로 DWM 합성 중지(원격 데스크톱 세션)에서 동일 반복 |
| 2 | `bbdb4a8` 설정 화면의 실제 모습 | `1a74cbf` 캡처만 있고 `bbdb4a8` 재진단이 Avalonia 렌더 타이머 시작 실패(native error -6661)로 중단됨. 방향 ComboBox 제거, 투명도 슬라이더·5종 대사 입력 추가 후의 밀도·정렬·잘림은 미관측 | 렌더 타이머 실패 원인(헤드리스/소프트웨어 렌더러 선택) 해결 후 `--smoke-test`를 새 `UNFOLD_DATA_DIR`로 재실행해 설정 알림 페이지를 4테마 × 640×560/기본 크기로 재캡처. ISSUE-06·ISSUE-07·A11Y-10·EV-04·EV-05·EV-10·UI-04~UI-09의 시각 판정은 이 캡처 이후로 보류 |
| 3 | A11Y-10 대사 잘림의 실제 임계 | 120자 입력이 `SpeechBubbleWidth=320`·종류별 고정 높이에서 몇 글자부터 잘리는지는 폰트 메트릭(NoonnuBasicGothic) 실측이 필요 | 각 알림 종류별로 10/40/80/120자 한국어 문장을 설정에 입력하고 말풍선을 캡처해 말줄임 시작 지점을 기록. 스크린리더(NVDA/VoiceOver)로 잘린 전문이 읽히는지 함께 확인 |
| 4 | ISSUE-04 포커스 표시의 실제 가시성 | `:focus-visible`이 Avalonia 12.1.2에서 키보드 진입에만 붙는지, `FocusAdorner=null` + 무효 포커스 스타일 조합에서 OS 고대비 모드가 대체 표시를 주는지는 런타임 확인 사항 | macOS·Windows 각각에서 Tab만으로 홈 → 설정 → 회고 전 경로를 순회하며 매 스톱을 캡처. Windows 고대비 테마 On/Off 비교 포함 |
| 5 | ISSUE-01 트리거 재현 | 시스템 시계를 90일 전진시키는 실험은 호스트 상태를 바꾸므로 제품 코드 수정 없이는 격리 재현이 안전하지 않다 | `BreakHistory.Add`에 시각을 주입하는 단위 테스트로 대체 재현(`Add(session, now)` → `Add(session2, now.AddDays(91))` 후 `Completions.Count == 1` 확인). 실기 시계 변경은 불필요 |
| 6 | ISSUE-03 결제·OAuth 실거래 | 앱 브랜치에 서버(결제) 소스가 미병합이며 실거래·OAuth는 기존 출시 게이트 | 고지 문구 추가 자체는 코드 변경으로 검증 가능. 실거래 확인은 기존 게이트에 유지 |
