# UI/UX 재설계 + 잔여 큐 입력 (3단계)

- 날짜: 2026-09-17
- 브랜치: `release/mvp` (기준 커밋 `2f02e8a` + Phase 1·2 미커밋 작업 트리)
- 런타임: C# · .NET 10 · Avalonia (macOS 실행 확인)
- 워커: Codex `gpt-5.6-sol`, reasoning effort `high`, Orca 감독형 Run
- 사용자 승인 범위: (1) 디자인 시스템 재설계 방향 확정 + 잔여 감사 큐 마무리,
  (2) 1차는 보고서만 작성하고 감독자 승인 뒤 2차에서 파일 소유권 분리 구현.

## 현재 상태

| 단계 | 항목 | 상태 |
|---|---|---|
| 1단계 | UI-01, UI-02, UI-03 | 구현·자동 검증 완료 |
| 2단계 | UI-04, UI-05, UI-06, UI-12, UI-13 | 구현·자동 검증 완료 (175 테스트, 스모크 PASS) |
| 3단계 | UI-07, UI-08, UI-09, UI-10, UI-11 | **미착수** |
| 4단계 | UI-14 | **미착수** |
| 재설계 | 시각 언어 전반 | 이번 실행에서 방향 확정 |

## 근거 자산

- 감사 원본: `docs/validation/2026-09-16-ui-ux-audit.md`
- 1차 감사 보고서: `_workspace/08_ui_visual_audit.md`, `09_ux_flow_audit.md`, `10_ui_accessibility_qa.md`
- 최신 캡처(Phase 2 적용 후, PNG 42장 + `smoke.json`): `/tmp/unfold-phase2-smoke-final.j5Uen8/verification/`
- 비교용 Phase 2 이전 캡처: `/tmp/unfold-ui-audit-20260916b/verification/`
- 디자인 소유 파일: `src/Unfold.Desktop/DesignSystem.cs`, `src/Unfold.Desktop/Ui.cs`

## 1차 금지 범위

- 제품 코드(`src/`) 및 테스트 수정 금지. 보고서 파일 1개씩만 생성한다.
- 사용자의 기존 미커밋 변경을 되돌리거나 stash 하지 않는다.
- 자동 off-screen 캡처를 실제 마우스·키보드·VoiceOver·Narrator·Windows DPI 증거로 해석하지 않는다.
- Swift/`archive/`/`reference/` 자료를 현재 구현 근거로 삼지 않는다.
- 한국어 문체와 기존 용어표를 유지한다. 라벨 문구를 임의로 바꾸지 않는다.

## 산출물 소유권 (겹침 금지)

| 워커 | 보고서 | 주제 |
|---|---|---|
| A. design-system | `31_design_system_redesign.md` | 토큰·타이포·간격·상태 표현 재설계 방향 |
| B. visual-gap | `32_visual_gap.md` | 화면별 현행 캡처 대비 변경 목록과 회귀 위험 |
| C. ux-flow | `33_ux_flow_redesign.md` | UI-14 포함 흐름·빈 상태·정보 구조 재설계 |
| D. a11y-spec | `34_a11y_implementation_spec.md` | UI-07~UI-11 구현 사양과 수용 기준 |

## 공통 수용 기준

1. 모든 주장에 파일·줄 또는 캡처 파일명 근거를 단다.
2. 제안마다 소유 파일, 의존성, 검증 명령, P0/P1/P2를 명시한다.
3. 자동 검사로 확인한 사실과 실제 OS 미검증 항목을 분리해 적는다.
4. 다른 워커 영역과 충돌하는 발견은 삭제하지 말고 "접점" 절에 남긴다.
