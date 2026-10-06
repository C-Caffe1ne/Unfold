# 분야별 수정 실행 계획 — 2026-10-06

기준: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`,
`codex/glb-import-compat`, `c44ef12`, `1.1.0-beta` + 기존 미커밋 변경.
사용자가 감사 계획의 분야별 실행을 승인했다. 기존 변경을 보존하고 파일 소유권을 나눈다.
공개 배포·실거래·기존 앱 교체는 이 실행 단계에 포함하지 않는다.

| 순서 | 분야 / 항목 | 우선순위 | 소유 파일 | 완료 기준 |
|---|---|---|---|---|
| 1 | QA-01 자동 미루기 계측·진단 보완 | P1 | SmokeDiagnostics.TimerRefinements.cs, AppRuntime.cs, TimerRefinementTests.cs | 실제 deadline 기준, 조건별 값·실패 원인 기록, Pause/3분/숨김/기록/횟수 확인 |
| 2 | R-01 GLB 임의 속도 | P2 | GlbPetView.cs, GlbEditorUxTests.cs | 1.25배 팩의 행동 편집·저장·재로드가 속도와 함께 유지 |
| 2 | R-02 one-shot 종료 포즈 | P2 | GlbModel.Rendering.cs, AnimationView.cs, GlbTests.cs, GlbTerminalFrameTests.cs | clip.End 이미지 일치, Completed 1회, 완료 후 확대/loop/pause 유지 |
| 2 병행 | A-02 펫 페이지 포커스 | P2 | SettingsWindow.Layout.cs, UiAuditRegressionTests.cs | media/GLB 이름 입력 포커스와 최소창 노출, Tab/Shift+Tab 회귀 |
| 병행 | D-01 기준·범위 문서 | P2 | AGENTS.md, docs/README.md·mvp.md·development-plan.md·verification.md·glb-pets.md | 현재 source/version/UI 계약, 운영 미검증 구별 |
| 3 | G-01 OS 실기 | P1 출시 게이트 | 검증 보고서 | 실제 Windows 클릭 통과/GPU/DPI 및 macOS 물리 입력 |
| 3 | G-02 성능 | P1 출시 게이트 | 임시 측정 harness·보고서 | 모델/반복 전환의 RAM·CPU 측정, 장시간·실제 앱 검증 경계 명시 |
| 3 | G-03 릴리스 입력 | P1 출시 게이트 | 임시 clean snapshot/publish·보고서 | locked restore, 동일 입력 빌드/게시와 데이터 보존, 설치/업데이트 한계 구분 |

공유 빌드는 UI 집중 검사 → GLB 집중 검사 → 감독자 통합 Release 순서로 직렬 실행한다.
감사 담당은 수정 후 검토를 맡고 구현 담당이 지정 파일만 변경한다.
P3 이름-only 초안, 중복 RootNode, 내보내기/적용·저장 피드백은 별도 추적이며 임의 기능을 추가하지 않는다.

진단 최초 2회 실패는 과거 관찰값이 없어 원인 미확정이다. 조건별 계측 실행은 정상 동작을 확인했다.
기존 실패를 삭제하거나 통과 결과로 덮지 않는다. 최종 결과는
[분야별 구현·검증 기록](../validation/2026-10-06-audit-followup.md)에 기록한다.

## 실행 상태

QA-01(즉시 만료 갱신 포함), R-01, R-02, A-02, D-01 완료. 최종 전체 검사 748/748, 독립 Release 빌드 경고·오류 0, macOS ARM64 게시본 smoke 통과. OS·장시간 성능·설치 검증의 남은 범위와 다음 순서는 [실행 결과](../validation/2026-10-06-audit-followup.md)에 있다.
