# 구현 보고서 — VIS-4 적용, A11Y-1 기각

## 원인

**VIS-4:** `SettingsWindow.cs:189`가 `ActivityError`(실제 장애)와 `Clock.Stopped`(정상 조작)를
한 분기로 묶어 둘 다 `DesignSystem.Error` 적색을 썼다. 사용자가 의도적으로 누른 중지가
오류로 보였다.

## 변경

- `DesignSystem.Themes.cs` — `ThemePalette`에 `Stopped` 필드 추가, 4개 팔레트에 값 지정,
  `ApplyTheme` 토큰 목록에 연결. 밝은 테마 `#D62F32`, 다크 테마 `#FF383C`.
- `SettingsWindow.cs:189-190` — `ActivityError`와 `Clock.Stopped`를 별도 분기로 분리.
- `TimerControlTests.cs:153` — 단정을 `DesignSystem.Stopped`로 갱신.
- `ThemeTests.cs` — 회귀 검사 `StoppedTimerColorStaysDistinctFromTheErrorColor` 추가.

밝은 테마 값은 사용자가 지정한 `#FF383C`의 색상각(358.8°)·채도를 유지하고 명도만 낮춘
변형이다. `#FF383C`는 밝은 테마에서 3.30~3.37:1로 본문 기준 4.5:1에 미달했다.

## 소유 파일

- `src/Unfold.Desktop/DesignSystem.Themes.cs`
- `src/Unfold.Desktop/SettingsWindow.cs`
- `Tests/Unfold.Tests/TimerControlTests.cs`
- `Tests/Unfold.Tests/ThemeTests.cs`

## 검증

| 검사 | 결과 |
|---|---|
| `dotnet test Unfold.slnx -c Release` | **217 통과 / 0 실패 / 0 건너뜀** |
| 밝은 테마 대비 (Shell/Surface) | 오트 라떼 4.59·4.87:1, 세이지 4.51·4.87:1 — AA 통과 |
| 회귀 검사 | `Stopped != Error`를 4테마 전부에서 확인, 밝은 테마는 4.5:1 강제 |

회귀 검사를 `[Fact]`로 쓰면 `ApplyTheme`가 UI 스레드를 요구해 실패했다.
`[AvaloniaFact]`로 전환했다. 이는 ARC-1(전역 가변 브러시 싱글톤)이 드러난 사례다.

## 미결 — 다크 테마 색상 결정 필요

사용자 지정 `#FF383C`는 다크 테마에서 기존 프로젝트 계약을 통과하지 못한다.

| 배경 | 미드나이트 | 플럼 |
|---|---|---|
| Shell | 4.40:1 | 4.22:1 |
| **Raised** | **2.99:1** | **2.98:1** |

`ThemeTests.AllPalettesKeepTextAndPrimaryActionsReadable`는 Warning·Error·Success에
Shell·Surface·Raised 전부 4.5:1을 이미 강제한다. `Stopped`를 그 목록에 넣으면 다크 테마가
실패하므로, 이번 회귀 검사는 밝은 테마만 강제하도록 범위를 좁혔다.

같은 색상각을 유지하고 채도만 낮춘 `#FF8486`이면 미드나이트 4.52:1, 플럼 4.51:1로
Raised까지 통과한다. 색은 사용자 결정 사항이라 임의로 바꾸지 않았다.

## A11Y-1 기각 — 구현하지 않음

감사 보고서의 "탭 이동 시 포커스 위치를 전혀 알 수 없음"은 프로덕션에서 성립하지 않는다.

- `Ui.KeyboardFocusLabel`(`Ui.cs:44`)이 키보드 포커스 시 라벨을 `"<이름> · 선택"`으로 바꾸고
  전경색을 `DesignSystem.FocusRing`으로 바꾼다. 포인터 포커스에는 적용하지 않는다(의도).
- 프로덕션 호출 지점 7곳에서 일관 적용: 펫 크기, 펫 선택, 설정 숫자 입력, 홈 시간 입력,
  말풍선 방향, 펫 관리 필드(`SettingsWindow.Layout.cs:91,99,139,165`,
  `SettingsWindow.Notifications.cs:34`, `PetManagementView.cs:51`).
- 세 개의 `Field` 헬퍼(`Ui.cs:37`, `PetManagementView.cs:48`, `SettingsWindow.Layout.cs:162`)가
  모두 이 함수와 `AutomationProperties.SetLabeledBy`를 호출한다.
- `UiAuditRegressionTests.cs:194` `KeyboardFocusUsesFieldLabelAndLeavesInputBorderUnchanged`가
  "입력 테두리는 바뀌지 않는다"를 명시적으로 단정한다. 테두리 포커스 링 추가는 이 승인된
  계약을 깨뜨린다.
- 감사자가 P0 근거로 든 `RoutineEditorWindow`·`EditorWindow`는 46번 보고서에서 확인한 대로
  프로덕션 진입점이 없는 진단 전용 창이다.

**잔여 위험(P3):** 표시가 컨트롤이 아니라 라벨에 있고 접미사가 작다. 실제 OS와 스크린 리더
낭독은 여전히 미검증이다.
