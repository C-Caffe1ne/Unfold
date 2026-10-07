# Phase 2 통합 QA — UI-04, UI-05, UI-06, UI-12, UI-13

## 역할과 소유권

`release-qa`로 현재 공유 작업 트리의 Phase 2 통합 결과를 독립 검증한다. 제품 코드, 테스트, 문서는 수정하지 않는다.
보고서 `.claude/skills/unfold-orchestrator/_workspace/24_phase2_qa.md`만 소유한다. 같은 작업 트리의 기존 변경을
되돌리거나 정리하지 않는다.

## 읽을 자료

- `docs/validation/2026-09-16-ui-ux-audit.md`의 UI-04, UI-05, UI-06, UI-12, UI-13과 2단계 완료 조건
- `.claude/skills/unfold-orchestrator/_workspace/21_phase2_scroll_implementation.md`
- `.claude/skills/unfold-orchestrator/_workspace/22_phase2_reminder_implementation.md`
- `.claude/skills/unfold-orchestrator/_workspace/23_phase2_pack_personalization_implementation.md`
- `.claude/skills/unfold-release-qa/SKILL.md`
- `docs/verification.md`

## 검증 요구사항

1. 구현 diff를 수용 기준과 대조하고 UI-04/05/06/12/13 밖의 범위 확장이 없는지 확인한다.
2. 새 `UNFOLD_DATA_DIR`을 써서 관련 집중 테스트, `dotnet build src/Unfold.Desktop -c Release --no-restore`,
   `dotnet test Unfold.slnx -c Release --no-restore`, `git diff --check`를 실행한다.
3. 새 빈 데이터 디렉터리에서 Release `--smoke-test`를 실행하고 `verification/smoke.json` 성공 여부와 생성된
   캡처를 확인한다. 설정 1120x800/860x680, 커스텀 펫 660x880/520x620 및 중간 폭의 스크롤바 경계,
   고정 작업 영역, 가로 넘침 여부를 우선 확인한다.
4. 펫 팩 480px 최소 폭에서 384 논리 픽셀 미리보기가 유지되는지와 경고 요약/이동 계약을 확인한다.
5. 접힌 알림의 `휴식 대기 중`/`휴식 중` 상태, 펫 배지, 트레이 복구 경로가 세션을 보존하는지 테스트 근거를
   추적한다. 네이티브 트레이의 실제 표현과 클릭은 자동 검사로 입증하지 않는다.
6. 현재 알림 설정 그룹과 개인화 하단 동작 그룹을 최소 크기에서 검사한다.
7. 실패가 있으면 이번 diff의 회귀인지 기존 환경/자산 문제인지 재현 근거로 구분하고 릴리스 차단 여부를 명시한다.

## 보고 형식

- 기준 브랜치/커밋과 작업 트리 상태
- 수용 기준별 PASS/FAIL/미검증 및 근거
- 실행한 명령, 통과/실패/건너뜀 개수, 스모크 JSON/캡처 경로
- 자동 검사와 실제 macOS/Windows/VoiceOver/Narrator/DPI 검증의 경계
- 발견된 회귀와 릴리스 차단 여부

