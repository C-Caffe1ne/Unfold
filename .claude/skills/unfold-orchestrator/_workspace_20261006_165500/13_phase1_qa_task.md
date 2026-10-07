# Phase 1 통합 QA — UI-01, UI-02, UI-03

## 역할

`release-qa`로서 현재 공유 작업 트리의 Phase 1 구현을 독립 검증한다. 제품 코드와 기존 문서는
수정하지 않고 보고서만 작성한다.

## 읽을 자료

- `docs/validation/2026-09-16-ui-ux-audit.md`의 1단계와 UI-01~UI-03
- `.claude/skills/unfold-orchestrator/_workspace/11_phase1_settings_implementation.md`
- `.claude/skills/unfold-orchestrator/_workspace/12_phase1_custom_pet_implementation.md`
- 실제 변경 파일과 관련 테스트
- `docs/verification.md`

## 검증 범위

1. UI-01: 기본 루틴은 편집할 수 없고, 선택한 사용자 루틴의 이름·단계·ID가 편집기로 전달되는지.
2. UI-02: 타이머 진행 중 프로필 적용이 클릭 전에 차단되고 이유가 보이며, 일시정지/중지 후 선택을
   유지한 채 다시 적용할 수 있는지. 독립 PersonalizationWindow 호환성도 확인한다.
3. UI-03: pending 배정과 슬롯 파일 선택 모두 교체 확인을 거치며, 취소·창 닫기·검증 실패에서 기존
   클립과 생성 가능 상태를 보존하는지. 성공 후 다음 빈 동작 이동과 전체 채움 상태도 확인한다.
4. 두 구현이 기존 펫 팩 탭, 설정 저장, 확인 대화상자, 타이머 런타임 방어를 깨지 않는지.

## 실행

- 새 `UNFOLD_DATA_DIR`을 사용한다.
- 변경 범위 집중 테스트.
- `dotnet test Unfold.slnx -c Release --no-restore` 전체 회귀.
- `dotnet build src/Unfold.Desktop -c Release --no-restore`.
- `git diff --check`.
- 그래픽 세션에서 새 격리 데이터로 Release 스모크 진단을 한 번 실행하고 `smoke.json`과 PNG 수를 확인한다.
  환경 때문에 실행되지 않으면 제품 실패와 분리해 정확한 오류를 기록한다.

## 출력

`.claude/skills/unfold-orchestrator/_workspace/13_phase1_qa.md`에 환경, 명령과 종료 결과,
코드 경계 검토, 통과/실패, macOS 관찰, Windows·보조 기술 미검증을 작성하고 Orca로 완료 보고한다.

다른 작업자와 사용자의 기존 변경을 보존한다. 제품 코드·테스트·기존 문서는 수정하지 않는다.
