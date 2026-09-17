---
name: unfold-orchestrator
description: "Orca에서 Claude Code 멀티에이전트로 Unfold 개발을 계획·분배·감독한다. 개발 계획, 구현, QA, BM 우선순위, 다음 작업, 부분 재실행, 업데이트, 수정, 보완, 이전 결과 개선 요청에는 반드시 이 스킬을 사용한다."
---

# Unfold Orchestrator

Unfold의 C#·Avalonia 개발을 Orca의 감독형 Run, Task, Dispatch로 조율한다.

## 실행 모드: Orca 감독자형 에이전트 팀

| 워커 | 역할 파일 | 스킬 | 초기 출력 |
|---|---|---|---|
| product-strategist | `.claude/agents/product-strategist.md` | `unfold-product-planning` | `.claude/skills/unfold-orchestrator/_workspace/01_product_strategy.md` |
| avalonia-architect | `.claude/agents/avalonia-architect.md` | `unfold-runtime-audit` | `.claude/skills/unfold-orchestrator/_workspace/02_architecture.md` |
| release-qa | `.claude/agents/release-qa.md` | `unfold-release-qa` | `.claude/skills/unfold-orchestrator/_workspace/03_release_qa.md` |
| implementation-engineer | `.claude/agents/implementation-engineer.md` | `unfold-implementation` | Task별 구현 보고서 |
| ui-visual-auditor | `.claude/agents/ui-visual-auditor.md` | `unfold-ui-visual-audit` | UI 시각 감사 보고서 |
| ux-flow-auditor | `.claude/agents/ux-flow-auditor.md` | `unfold-ux-flow-audit` | 사용자 흐름 감사 보고서 |
| ui-accessibility-qa | `.claude/agents/ui-accessibility-qa.md` | `unfold-ui-accessibility-qa` | 접근성·반응형 QA 보고서 |

## Phase 0: 컨텍스트 확인

1. 현재 브랜치와 Git 상태를 확인하고 사용자의 기존 변경을 보존한다.
2. `.claude/skills/unfold-orchestrator/_workspace/`가 없으면 초기 실행한다.
3. 기존 `_workspace/`와 부분 수정 요청이 있으면 해당 워커만 재호출한다.
4. 새 목표로 전체 재실행할 때는 기존 폴더를 같은 위치의 `_workspace_YYYYMMDD_HHMMSS/`로 보존한다.

## Phase 1: 준비

1. `docs/README.md`, `docs/mvp.md`, `docs/development-plan.md`, `docs/verification.md`에서 현재 범위와 검증 원칙을 확인한다.
2. `.claude/skills/unfold-orchestrator/_workspace/00_input.md`에 목표, 브랜치, 기준 문서, 금지 범위, 현재 검증 상태를 기록한다.
3. 제품 코드 변경 전 감사인지, 파일 소유권이 정해진 구현 단계인지 구분한다.

## Phase 2: 작업 분배

1. `orca orchestration run-create`로 하나의 Run을 만든다.
2. 독립 작업은 먼저 모두 시작한다. 각 Task spec에는 Target, Change, Constraints, Ownership, Observable acceptance를 넣는다.
3. Claude Code 워커는 `orca orchestration worker-start --worktree current --agent claude --model opus`로 실행한다.
4. 같은 체크아웃에서 병렬 실행할 때는 서로 다른 보고서만 소유하게 한다. 코드 구현은 겹치지 않는 파일 소유권이 확정된 뒤 시작한다.

## Phase 3: 감독

1. Orca inbox에서 질문, escalation, worker_done을 처리한다.
2. 워커의 상충 발견은 삭제하지 않고 근거와 출처를 유지한다.
3. 완료 보고는 실제 보고서, 명령 결과, 변경 파일과 대조한 뒤 수락한다.
4. 타임아웃과 빈 응답은 실패로 간주하지 않는다. Orca가 제공하는 다음 동작을 따른다.

## Phase 4: 통합과 후속 구현 큐

1. 세 보고서에서 공통 발견, 상충, 미검증을 분리한다.
2. 후속 작업을 P0/P1/P2, 의존성, 소유 파일, 검증 명령, 실제 OS 한계로 정리한다.
3. 구현 작업은 워커별 파일 소유권이 겹치지 않을 때만 병렬화한다.
4. 각 구현 모듈 완료 직후 release-qa가 생산자·소비자 경계를 점진적으로 검증한다.

## 데이터 흐름

```text
사용자 목표 → 감독자 → Orca Run/Task/Dispatch
                    ├─ 제품·BM 보고서
                    ├─ 아키텍처 보고서
                    └─ 릴리스 QA 보고서
                              ↓
                     감독자 검토·통합
                              ↓
               우선순위·소유권·수용 기준 작업 큐
```

## 에러 핸들링

- 시작 실패는 같은 작업을 중복 실행하지 말고 Orca receipt의 실패 단계와 복구 명령을 따른다.
- 워커 1명 실패는 1회 복구 후 해당 영역을 미검증으로 남기고 나머지를 통합한다.
- 과반 실패, 범위 충돌, 외부 영향이 필요한 변경은 감독자가 사용자에게 상태와 선택지를 보고한다.
- 수락된 완료 뒤에는 워커를 재사용, 유지, 해제 중 하나로 명시적으로 정리한다.

## 테스트 시나리오

### 정상 흐름

1. 세 Claude Code 워커가 서로 다른 보고서를 작성한다.
2. 각 보고서가 현재 C# 구현 근거와 미검증 항목을 포함한다.
3. 감독자가 세 결과를 통합해 소유권이 겹치지 않는 후속 작업 큐를 만든다.

### 오류 흐름

1. QA 워커의 테스트 실행이 환경 문제로 실패한다.
2. QA는 정적 근거와 실행 실패를 분리해 보고한다.
3. 감독자는 QA 영역을 미검증으로 표시하고 제품·아키텍처 결과의 통합을 계속한다.
