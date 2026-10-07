# 40 입력 — 전수 검사 Run (2026-09-20)

## 목표
현재 체크아웃(코드 + 미커밋 변경)에 대해 코드, 디자인, UI/UX, QA를 병렬 감사한다.
결과는 발견 사항과 우선순위 큐까지만 만든다.

## 기준
- 브랜치: `release/mvp`
- 기준 커밋: `0b72a36 9/18 v0.2.1`
- 작업 트리: 미커밋 변경 37개 파일(+1229/-570), 신규 문서·테스트 다수
- 감사 대상은 커밋이 아니라 **현재 작업 트리**다.
- 기준 문서: `docs/README.md`, `docs/mvp.md`, `docs/development-plan.md`, `docs/verification.md`,
  `docs/design-system.md`

## 금지 범위
- 제품 코드(`src/`), 테스트(`Tests/`), 문서(`docs/`) **수정 금지**. 검사 전용 Run이다.
- git 상태 변경 금지(commit, stash, checkout, restore 금지).
- 사용자의 미커밋 변경 보존이 최우선이다.

## 파일 소유권
| 워커 | 소유 보고서 | dotnet 실행 |
|---|---|---|
| avalonia-architect | `41_code_architecture_audit.md` | 금지(정적 분석) |
| release-qa | `42_release_qa.md` | **단독 허용** |
| ui-visual-auditor | `43_ui_visual_audit.md` | 금지(정적 + 기존 캡처) |
| ux-flow-auditor | `44_ux_flow_audit.md` | 금지 |
| ui-accessibility-qa | `45_a11y_responsive_qa.md` | 금지 |
| 감독자 | `46_supervisor_integration.md` | 통합만 |

빌드 산출물(`bin/`, `obj/`) 잠금 충돌을 막기 위해 `dotnet build/test/run`은 release-qa만 실행한다.

## 현재 검증 상태(문서 기준, 미확인)
- `docs/validation/2026-09-20-*`: Release 테스트 215개까지 기록, 자동 캡처 존재
- 실제 macOS/Windows 실기 검증은 문서상 미완 항목 존재(`windows-dogfooding-log.md` 공란)
