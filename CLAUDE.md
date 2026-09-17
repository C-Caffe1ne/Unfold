@AGENTS.md

## 하네스: Unfold 제품 개발

**목표:** 현재 C#·Avalonia 구현을 근거로 제품 방향, 기술 작업, 검증을 분리하고 Orca의 Claude Code 워커가 충돌 없이 수행하도록 조율한다.

**트리거:** Unfold의 개발 계획, 구현, 검증, 출시 준비, BM 우선순위, 후속 작업 분배 요청에는 `unfold-orchestrator` 스킬을 사용하라. 단순한 코드 위치 질문은 직접 답할 수 있다.

**변경 이력:**
| 날짜 | 변경 내용 | 대상 | 사유 |
|---|---|---|---|
| 2026-09-16 | 초기 감독자형 하네스 구성 | agents, skills, orchestrator | Orca에서 Claude Code 멀티에이전트 개발을 감독하기 위해 |
| 2026-09-16 | C# 구현 역할 추가 | implementation-engineer, unfold-implementation | 감사에서 확인된 회귀를 파일 소유권 단위로 수정하기 위해 |
| 2026-09-16 | UI/UX 감사 역할 추가 | ui-visual-auditor, ux-flow-auditor, ui-accessibility-qa | 전체 화면의 시각 일관성, 흐름, 접근성·반응형을 분리 검수하기 위해 |
