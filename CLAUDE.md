@AGENTS.md

새 세션·IDE에서는 [작업 인계](docs/HANDOFF.md)를 먼저 읽는다. 브랜치·제품 HEAD·원격·태그·미커밋 변경과 실제 실행 경로를 다시 확인한다.

## 하네스: Unfold 제품 개발

**목표:** 현재 C#·Avalonia 구현과 공통 문서를 근거로 제품 작업·검증을 이어간다. 여러 워커가 필요하면 파일 소유권을 나눠 기존 변경을 보존한다. 특정 IDE나 Orca 설치를 일반 실행의 전제로 삼지 않는다.

**진행:** 사용 가능한 환경에서 요청에 필요한 Unfold 스킬을 적용한다. 공통 규칙은 `AGENTS.md`, 재개 상태는 `docs/HANDOFF.md`, 기능 문서는 `docs/README.md`를 따른다. 과거 하네스 이력은 현재 사용자 요청의 범위나 승인을 대신하지 않는다.

**변경 이력:**
| 날짜 | 변경 내용 | 대상 | 사유 |
|---|---|---|---|
| 2026-09-16 | 초기 감독자형 하네스 구성 | agents, skills, orchestrator | Orca에서 Claude Code 멀티에이전트 개발을 감독하기 위해 |
| 2026-09-16 | C# 구현 역할 추가 | implementation-engineer, unfold-implementation | 감사에서 확인된 회귀를 파일 소유권 단위로 수정하기 위해 |
| 2026-09-16 | UI/UX 감사 역할 추가 | ui-visual-auditor, ux-flow-auditor, ui-accessibility-qa | 전체 화면의 시각 일관성, 흐름, 접근성·반응형을 분리 검수하기 위해 |
