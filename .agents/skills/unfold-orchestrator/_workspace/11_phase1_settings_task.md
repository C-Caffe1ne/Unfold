# Phase 1A — 루틴 편집 대상과 프로필 적용 상태 수정

## 목표

`docs/validation/2026-09-16-ui-ux-audit.md`의 UI-01, UI-02만 구현한다.

## 소유 파일

- `src/Unfold.Desktop/SettingsWindow.Layout.cs`
- `src/Unfold.Desktop/SettingsWindow.cs`
- `src/Unfold.Desktop/PersonalizationWindow.cs`
- `Tests/Unfold.Tests/SettingsDashboardTests.cs`
- `Tests/Unfold.Tests/PersonalizationWindowTests.cs`
- 구현 보고서: `.claude/skills/unfold-orchestrator/_workspace/11_phase1_settings_implementation.md`

다른 작업자도 같은 작업 트리에 있다. 소유 파일 밖의 변경과 기존 변경을 되돌리거나 정리하지 않는다.

## 요구사항

### UI-01

- 대시보드의 `SettingsEditRoutine`은 `RoutinePicker.SelectedItem`인 사용자 루틴을 편집해야 한다.
- `BreakRoutines.Find(id)`로 찾을 수 있는 기본 루틴은 직접 편집할 수 없으므로 버튼을 비활성화한다.
- 기본 루틴에서 비활성인 이유와 `루틴 · 프로필`에서 새 루틴을 만들 수 있다는 안내를 Tooltip 또는 접근성 도움말로 제공한다.
- 사용자 루틴을 선택하면 버튼이 다시 활성화되고, 열린 `RoutineEditorWindow`의 이름과 단계가 그 선택과 일치해야 한다.
- 저장은 기존 `AppSettings.SaveRoutine`과 `runtime.UpdateSettings` 계약을 유지한다.

### UI-02

- 설정 창 안의 `PersonalizationView`는 `runtime.CanEditTimerInterval`을 받아 프로필 적용 가능 여부를 클릭 전에 표시해야 한다.
- 타이머 진행 중에는 선택한 프로필이 있어도 `프로필 적용`을 비활성화하고, 상세 문구에 `타이머를 일시정지하거나 중지`해야 한다는 이유를 표시한다.
- 일시정지 또는 중지로 바뀌면 목록 선택을 잃지 않고 버튼이 활성화되어야 한다.
- `PersonalizationWindow`의 기존 공개 생성자와 독립 사용은 호환되어야 한다. 런타임 콜백이 없는 독립 창은 기존처럼 적용 가능 상태를 기본값으로 사용한다.
- 런타임의 `UpdateSettings` 방어 검사는 유지한다.

## 회귀 검사

- 선택한 추가 루틴이 편집기에 그대로 열리는 테스트.
- 기본 루틴에서 편집 버튼이 비활성이고 사용자 루틴에서 활성인 테스트.
- 타이머 진행 중 프로필 적용 버튼과 이유 문구, 일시정지 후 재활성화를 검증하는 테스트.
- 관련 집중 테스트를 실행하고 결과를 보고한다. 전체 테스트는 감독자가 통합 후 실행한다.

## 금지 범위

- UI-03 이후 단계 구현.
- 디자인 토큰, 스크롤바, 말풍선, 펫 팩 레이아웃 변경.
- Core 저장 형식이나 타이머 규칙 변경.

