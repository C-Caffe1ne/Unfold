# Phase 2C — 펫 팩 경고와 루틴·프로필 정보 구조

## 역할과 소유권

`implementation-engineer`로 UI-06, UI-12, UI-13만 구현한다. 다음 파일만 소유한다.

- `src/Unfold.Desktop/PetPackWindow.cs`
- `src/Unfold.Desktop/SettingsWindow.Layout.cs`
- `src/Unfold.Desktop/PersonalizationWindow.cs`
- `Tests/Unfold.Tests/PetPackWindowTests.cs`
- `Tests/Unfold.Tests/SettingsDashboardTests.cs`
- `Tests/Unfold.Tests/PersonalizationWindowTests.cs`

같은 작업 트리에서 다른 작업자가 일한다. 소유 파일 밖 변경과 기존 변경을 되돌리거나 정리하지 않는다.

## 읽을 자료

- `docs/validation/2026-09-16-ui-ux-audit.md`의 UI-06, UI-12, UI-13과 2단계 완료 조건
- `.claude/skills/unfold-implementation/SKILL.md`
- 현재 펫 팩 본문/고정 작업 영역, 설정 루틴 카드, 라이브러리 하단 동작 배치

## 구현 요구사항

### UI-06

1. 팩에 경고가 있으면 고정 `PageActions` 영역에서 경고 개수와 설치 전 확인 필요성을 스크롤 없이 보여 준다.
2. 고정 영역에 본문 경고로 이동하는 동작을 제공한다. 실행 후 경고 본문이 보이는 위치로 스크롤되어야 한다.
3. 새 팩을 열거나 오류가 나면 요약·본문 경고 상태가 이전 팩에서 남지 않아야 한다.
4. 설치/업데이트/재설치 버튼의 고정 위치와 기존 미리보기 계약을 유지한다.

### UI-12

5. 대시보드의 **직접 설정한 알림 / 업무 프로필**을 선택 루틴 설명처럼 보이지 않게 별도 상태 그룹으로 분리한다. 현재 상태라는 제목과 값을 함께 보여 주고 카드 안에서 기존 선택기·동작과 시각적으로 구분한다.

### UI-13

6. 루틴·프로필 라이브러리 하단을 주 작업, 관리, 닫기 그룹으로 재배치한다. 600px 최소 폭에서 **닫기만 별도 줄로 떨어지는 비대칭**이 없어야 하고, 탭을 바꿔도 그룹 구조가 안정적이어야 한다.
7. 기존 버튼 이름, 비활성화, 저장·삭제·적용 동작은 유지한다.

## 금지 범위

- UI-09~UI-11 접근성/오류 색 단계 구현
- 공통 `Ui.cs` 변경
- 팩 검사나 저장 데이터 형식 변경

## 검증

- 경고가 실제 있는 팩으로 고정 요약, 개수, 이동 동작과 최소 창을 검사한다.
- 설정 카드 그룹과 라이브러리 최소 폭의 기하·동작 회귀 검사를 추가한다.
- 관련 집중 테스트와 `dotnet build src/Unfold.Desktop -c Release --no-restore`
- 보고서는 `.claude/skills/unfold-orchestrator/_workspace/23_phase2_pack_personalization_implementation.md`

