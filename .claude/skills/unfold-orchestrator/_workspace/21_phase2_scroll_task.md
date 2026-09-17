# Phase 2A — PageBodyScroll 거터와 겹침 회귀

## 역할과 소유권

`implementation-engineer`로 UI-04만 구현한다. 다음 파일만 소유한다.

- `src/Unfold.Desktop/Ui.cs`
- `src/Unfold.Desktop/CustomPetWindow.cs` (공통 거터만으로 부족할 때 필요한 최소 변경)
- `src/Unfold.Desktop/SmokeDiagnostics.cs`
- `Tests/Unfold.Tests/DesignSystemTests.cs`
- `Tests/Unfold.Tests/CustomPetTests.cs`

같은 작업 트리에서 다른 작업자가 일한다. 소유 파일 밖 변경과 기존 변경을 되돌리거나 정리하지 않는다.

## 읽을 자료

- `docs/validation/2026-09-16-ui-ux-audit.md`의 UI-04와 2단계 완료 조건
- `docs/validation/2026-09-16-ui-ux-phase1.md`의 660×880 재현 근거
- `.claude/skills/unfold-implementation/SKILL.md`
- 현재 `Ui.PageContent`, `CustomPetView`, 관련 테스트와 스모크 진단

## 구현 요구사항

1. `PageBodyScroll`의 세로 스크롤바가 본문 입력과 버튼 위에 겹치지 않도록 공통 오른쪽 거터 또는 본문 인셋을 둔다.
2. 공통 페이지의 고정 하단 `PageActions`, 가로 넘침 없음, 기존 여백과 최소 창 동작을 유지한다.
3. 설정 1120×800·860×680, 커스텀 펫 660×880·520×620에서 펫 이름 입력·파일 가져오기·슬롯 버튼과 스크롤바의 실제 경계가 겹치지 않는지 검사한다. 가능한 경우 두 크기 사이도 포함한다.
4. 보임 여부만 확인하지 말고 컨트롤 오른쪽 경계와 세로 스크롤바 왼쪽 경계를 좌표로 비교하는 의미 있는 회귀 검사를 추가한다.
5. `SmokeDiagnostics`에서 같은 기하 계약을 검사하고 기존 캡처·초안 유지·고정 작업 영역 계약을 보존한다.

## 금지 범위

- UI-05~UI-14 구현
- 디자인 토큰 전면 변경, 입력 포커스 스타일 변경
- 창 크기나 데이터 형식 변경

## 검증

- 소유 범위 집중 테스트
- `dotnet build src/Unfold.Desktop -c Release --no-restore`
- 구현 전/후 또는 결함 재현에 준하는 검사 근거
- 보고서는 `.claude/skills/unfold-orchestrator/_workspace/21_phase2_scroll_implementation.md`

