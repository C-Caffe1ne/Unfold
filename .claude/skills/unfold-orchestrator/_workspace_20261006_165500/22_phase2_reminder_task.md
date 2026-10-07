# Phase 2B — 접힌 휴식 상태와 복구 경로

## 역할과 소유권

`implementation-engineer`로 UI-05만 구현한다. 다음 파일만 소유한다.

- `src/Unfold.Desktop/AppRuntime.cs`
- `src/Unfold.Desktop/PetWindow.cs`
- `Tests/Unfold.Tests/BreakReminderTests.cs`
- 필요하면 기존 런타임 테스트 파일 한 개(보고서에 이유 명시)

같은 작업 트리에서 다른 작업자가 일한다. 소유 파일 밖 변경과 기존 변경을 되돌리거나 정리하지 않는다.

## 읽을 자료

- `docs/validation/2026-09-16-ui-ux-audit.md`의 UI-05와 2단계 완료 조건
- `docs/stretch-notifications.md`
- `.claude/skills/unfold-implementation/SKILL.md`
- `AppRuntime.Tick/BuildTray/RefreshPetNotice`, `PetWindow.RefreshSpeech`, `PetReminder` 상태 계약

## 구현 요구사항

1. 말풍선이 접혔고 휴식 알림이 대기 중이면 트레이 상태에서 **휴식 대기 중**, 휴식이 시작됐으면 **휴식 중**을 분명히 표시한다. 툴팁과 첫 상태 항목이 서로 모순되지 않아야 한다.
2. 트레이에서 현재 휴식 알림을 펼치는 명시적 복구 동작을 제공한다. 활성 알림이 있고 말풍선이 접혔을 때만 쓸 수 있어야 하며, 실행하면 세션·경과 시간을 유지한 채 말풍선을 펼친다.
3. 접힌 펫에는 휴식 알림 존재를 알리는 작고 방해가 적은 상태 배지를 표시한다. 대기와 진행을 색 또는 문구만이 아닌 최소한의 시각 상태로 구분하고, 펼친 말풍선이나 알림이 없을 때는 숨긴다.
4. 알림 생성·시작·완료·미루기·취소·말풍선 펼침 직후 트레이와 배지가 다음 1초 Tick을 기다리지 않고 갱신되도록 한다.
5. 기존 드래그, 클릭 반응, 투명 창 클릭 통과, 4방향 말풍선, 접기/펼치기, 세션 보존을 깨지 않는다.
6. 접힌 대기 → 접힌 진행 → 트레이 복구 → 완료/해제 상태의 회귀 검사를 추가한다. 테스트가 트레이 네이티브 구현 세부에 과도하게 묶이지 않도록 상태 계산 계약을 분리해도 된다.

## 금지 범위

- 창 강제 활성화나 다른 앱의 포커스 탈취
- UI-07~UI-11 접근성 단계 구현
- 알림·기록 데이터 형식 변경

## 검증

- `BreakReminderTests`와 관련 런타임 집중 테스트
- `dotnet build src/Unfold.Desktop -c Release --no-restore`
- 보고서는 `.claude/skills/unfold-orchestrator/_workspace/22_phase2_reminder_implementation.md`

