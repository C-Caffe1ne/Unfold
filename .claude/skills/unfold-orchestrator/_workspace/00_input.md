# 00 입력 — Agency Agents 버그·UI/UX 감사 (2026-10-06)

## 목표

최근 설치한 Agency Agents(`~/.claude/agents`, 2026-10-06 16:23 설치)로 최신 개발본의
버그·오류, UI/UX·직관성을 감사하고 우선순위·소유 파일·검증 기준이 있는 작업 계획을 만든다.
이번 단계는 감사만 한다. 제품 코드는 수정하지 않는다.

## 기준 소스

| 항목 | 값 |
|---|---|
| 최신 앱 소스 | `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold` |
| 브랜치 / HEAD | `codex/glb-import-compat` / `1a74cbf` (`18a3a31` 이후 문서 변경만) |
| 버전 | `1.1.0-beta` |
| 서버(결제) 소스 | `codex/beta-v1.0.4` `95f7a21` — 앱 브랜치에 미병합, 이번 감사 범위 밖 |
| 이전 소스 | `release/mvp` `0.2.2` (현재 세션 체크아웃) — 감사 대상 아님 |

최신 워크트리에서 Codex 세션이 동작 중이며 `docs/windows-dogfooding-log.md`를 미커밋 수정 중이다.
따라서 그 워크트리에는 쓰기·빌드·테스트를 하지 않는다.

## 격리 실행본

- 소스 복사본: scratchpad `unfold-src-1a74cbf` (bin/obj/artifacts/.git/.tools 제외, `web/assets` 포함)
- `dotnet restore --locked-mode` 통과, `src/Unfold.Desktop` Release 빌드 경고 0·오류 0
- 진단: 새 `UNFOLD_DATA_DIR`로 `--smoke-test` 실행 (결과는 03 보고서에 기록)
- 전체 테스트는 재실행하지 않는다. `18a3a31`→`1a74cbf`는 문서 변경뿐이며 오늘 기록된
  748/748(독립 실행 기준, 출력 경로 권한 실패 2건은 표준 위치 재검사 통과) 결과를 근거로 사용한다.
- 디스크 여유 약 6GiB. 추가 전체 빌드·테스트·게시는 하지 않는다.

## 이미 알려진 항목 (재보고 금지, 상태만 인용)

- 수정 완료: QA-01 자동 미루기·즉시 만료, R-01 GLB 임의 속도, R-02 GLB 종료 포즈, A-02 펫 페이지 포커스
- 미검증 출시 게이트: 실제 Windows 클릭 통과·DPI·GPU, 장시간 성능, 설치 업데이트·데이터 보존, 실거래·OAuth
- 별도 추적 P3: 이름-only 초안, 중복 RootNode, 내보내기/적용·저장 피드백
- 근거: `docs/validation/2026-10-06-audit-followup.md`, `docs/plans/2026-10-06-audit-followup.md`,
  `docs/plans/2026-10-06-agency-execution.md`

## 팀 구성 (Agency Agents, Orca CLI 없음 → Claude Code Agent 도구로 배치)

| ID | 에이전트 | 영역 | 보고서 |
|---|---|---|---|
| A1 | Code Reviewer | Core·AppRuntime 상태/저장/타이머/계정·업데이트 | `01_bug_core_review.md` |
| A2 | Desktop App Engineer | 펫 창·플랫폼 통합·GLB/미디어·리소스 수명 | `02_bug_desktop_runtime.md` |
| A3 | Evidence Collector | smoke 결과·캡처·기존 TRX 증거 분석 | `03_runtime_evidence.md` |
| B1 | UX Researcher | 핵심 흐름 휴리스틱 평가 | `04_ux_heuristic.md` |
| B2 | Persona Walkthrough Specialist | 첫 사용자·제작자 페르소나 직관성 | `05_persona_walkthrough.md` |
| B3 | UI Designer | 시각 계층·디자인 시스템 일관성 | `06_ui_visual.md` |
| B4 | Accessibility Auditor | 키보드·접근성 이름·대비·최소 창 | `07_accessibility.md` |
| C1 | Reality Checker | 상위 발견 교차 검증·판정 | `08_reality_check.md` |
| — | 감독자 | 통합 작업 계획 | `09_work_plan.md` |

## 금지 범위

제품 코드 수정, 최신 워크트리 쓰기, 실제 사용자 데이터 사용, 사용자 데스크톱에서 일반 앱 GUI 실행,
공개 배포·결제·외부 메시지, MVP 범위 밖 기능을 버그로 분류하는 것.
