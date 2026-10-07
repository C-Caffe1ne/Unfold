# 35. 감독자 통합 — UI/UX 재설계와 3·4단계 구현 큐

- 날짜: 2026-09-17
- Run: `run_7de5f82eeb4c`
- 워커: Codex `gpt-5.6-sol` effort `high` 4명 (A 디자인시스템 / B 시각격차 / C UX흐름 / D 접근성사양)
- 입력: `_workspace/30_ui_redesign_input.md`
- 보고서: `31_design_system_redesign.md`, `32_visual_gap.md`, `33_ux_flow_redesign.md`, `34_a11y_implementation_spec.md`
- 기준 캡처(신규): `/tmp/unfold-redesign-baseline.JHBiAh/verification/` — PNG 42장, `smoke.json success=true`, `imageFiles=42`, `Unix 26.6.2`, .NET `10.0.12`, Release 빌드 경고 0

## 1. 감독자가 직접 검증한 사실

| 항목 | 방법 | 결과 |
|---|---|---|
| 캡처-소스 불일치 (B 결론 5) | `SmokeDiagnostics.cs`의 캡처 이름과 구 캡처 42장 대조 | **사실.** `settings-routines-tab.png`, `settings-speech-options.png`는 더 이상 생성되지 않고, `settings-notification-options.png`, `settings-timer-options.png`는 한 번도 감사된 적 없음 |
| 새 baseline 생성 | 격리 `UNFOLD_DATA_DIR` + Release `--smoke-test` | 성공. PNG 42장, `success: true` |
| 새 캡처 2장의 유효성 | md5 비교 | **두 캡처가 바이트 동일**(`4831c841fd9f3d8e1663a92c714db55a`). `SmokeDiagnostics.cs:488-491`의 `BringIntoView()`가 현재 창 크기에서 no-op이라 스크롤 도달성 증거가 없음 |
| D의 인용 4건 | `DesignSystem.cs:83,135`, `PetWindow.cs:58`, `docs/design-system.md:50-57`, 입력 캡처 폴더 | 전부 일치 |
| 작업 트리 보존 | `git status --porcelain src Tests` | 28개 — 세션 시작 시점과 동일. 워커의 제품 코드 오염 없음 |

## 2. 공통 발견 (2명 이상이 독립적으로 도달)

1. **구 캡처는 현재 제품 UI가 아니다.** B는 진단 캡처 이름으로(`SmokeDiagnostics.cs:385-497`), C는 사이드바 구성으로(`SettingsWindow.Layout.cs:52-60`) 같은 결론에 도달했다. 루틴·업무 프로필은 일반 UI에서 제거됐다(`docs/settings-ui.md:49-50`).
2. **포커스 표시 부재가 최우선 결함이다.** A는 토큰 공백(C21/F01), D는 `FocusAdorner=null` 두 지점(`DesignSystem.cs:83,135`)으로 같은 항목을 P0/P1로 올렸다.
3. **고정 픽셀 골격이 재설계의 최대 위험이다.** B는 회귀 위험 9종, A는 단계적 적용 원칙, C는 말풍선 268px 고정 높이로 같은 위험을 지적했다.
4. **자동 캡처를 실기 증거로 쓰지 않는다.** 네 보고서 모두 미검증 절을 분리했고 중복 없이 일치한다.

## 3. 상충과 감독자 판정

| 번호 | 상충 | A 주장 | D 주장 | 판정 |
|---|---|---|---|---|
| X-1 | 포커스 링의 기하 | F01 `FocusRingWidth/Offset` 2px/2px를 **컨트롤 바깥** 표시로 기술 | **컨트롤 안쪽 2px**. 외곽 음수 margin은 Avalonia에서 잘릴 위험 | **D 채택.** A가 "컨트롤별 방식은 워커 D 소유"로 명시적 위임했고, D의 근거가 구현 수준에서 구체적이다. 토큰 값(2px/2px)과 색(C21 `#8FD3FF`)은 A가 단일 출처 |
| X-2 | 포커스 토큰 이름 | `FocusRing`, `FocusRingWidth`, `FocusRingOffset` | `FocusRing`, `FocusRingThickness`, `FocusRingInset` | **A 채택.** A가 `DesignSystem.cs` 소유자다. D는 "A의 동등 토큰이 생기면 그 이름이 단일 출처"라고 이미 양보했다 |
| X-3 | `DesignSystem.cs` 동시 소유 | A의 P0 의미 토큰 작업 | D의 UI-07 포커스 적용 | **직렬화.** A의 P0-A 병합 후 UI-07 착수. D의 충돌표와 일치 |
| X-4 | 간격 토큰 일괄 변경 | S01–S06으로 4/6/7/10/14/18/22px 정리 | (B) 8px `ScrollGutter`는 Phase 2 겹침 해결 계약 | **B 채택.** `Ui.cs:49-66`의 8px 거터는 간격 토큰 통일에서 **제외**하고 고정값으로 남긴다 |
| X-5 | `SmokeDiagnostics.cs` 동시 소유 | 5개 접근성 항목 + 캡처 정정 | — | **마지막 1명이 일괄 통합.** 각 구현자가 키 이름만 예약하고 직접 수정하지 않는다 |

## 4. 사용자 결정 — 설계 제외 (2026-09-17)

사용자가 아래 3건을 **설계에서 제외**했다. 2차 구현 큐와 이후 단계에서 다루지 않는다.

| 제외 항목 | 내용 | 남는 결과 |
|---|---|---|
| UI-08 트레이 메뉴 확장 | 트레이 `NativeMenu`에 `휴식 시작`·`n분 뒤에`·`완료` 추가 | **키보드 접근 경로 부재가 그대로 남는다.** 다른 앱에 포커스를 둔 키보드 사용자는 말풍선의 시작·미루기·완료에 도달할 수 없다(`PetWindow.cs:58`의 `ShowActivated=false`). 알려진 제약으로 기록하고 3단계 P1에서 제거한다 |
| 라이트 테마 | `RequestedThemeVariant = Dark` 고정 해제 | A의 라이트 팔레트는 `31_design_system_redesign.md`에 참고 자료로만 남는다. **코드에는 다크 값만 구현한다** |
| 루틴·프로필 재도입 | 일반 UI 진입점 복원 | `docs/mvp.md:53-59`의 제외 범위 유지. 진단 호환 화면으로만 존속 |

이에 따라 3단계 잔여 범위는 **UI-07, UI-09, UI-10, UI-11**이며 UI-08은 제외다.

## 4-1. (기록) 승인 요청 시점의 원안

### 원 4. 사용자 승인이 필요한 제품 변경

| 항목 | 내용 | 근거 | 왜 승인이 필요한가 |
|---|---|---|---|
| UI-08 트레이 메뉴 확장 | 트레이 `NativeMenu`에 `휴식 시작`, `n분 뒤에`, `완료` 추가 | `34_a11y_implementation_spec.md` UI-08 | 사용자에게 보이는 메뉴 표면이 늘어난다. 전역 단축키와 자동 활성화는 D가 근거와 함께 기각 |
| 라이트 테마 | 현재 `RequestedThemeVariant = Dark` 고정 해제 | `DesignSystem.cs:24-34`, A P2 | OS 테마 추종 대 앱 설정은 제품 결정이며 구현 범위가 크게 달라진다 |
| 루틴·프로필 재도입 | 일반 UI 진입점 복원 | `docs/mvp.md:53-59`, C P2 | MVP에서 명시적으로 제외된 범위. UI-14 승인이 이 승인으로 확대되지 않는다 |

## 5. 2차 구현 큐

파일 소유권이 겹치지 않는 단위로 나눈다. 같은 줄의 항목은 병렬 실행 가능하다.

| 순서 | 항목 | 우선순위 | 소유 파일 | 선행 | 완료 증거 |
|---|---|---|---|---|---|
| 1 | **A-P0** 의미 색·상태·disabled·경계 토큰 (C09–C11, C16–C21, B01, F01) | P0 | `DesignSystem.cs`, `DesignSystemTests.cs`, `docs/design-system.md` | 없음 | 집중 테스트 + 전체 회귀. 기존 `Cream/Muted/Surface` 정확값 단언 동시 갱신(`DesignSystemTests.cs:194-219,223-240`) |
| 1 | **C-P0** UI-14 날짜 행 펼침 상세 | P0 | `BreakReviewWindow.cs`, 대응 테스트 | 없음. Core 변경 불필요 | 8개 명명 테스트, 구형 기록 null 표기 분기, 최소 창 캡처 |
| 1 | **QA-P0** 캡처 진단 정정 | P0 | `SmokeDiagnostics.cs` | 없음 | 두 캡처가 서로 다른 상태를 증명하거나 이름이 정정됨 |
| 2 | **D UI-07** 입력 포커스 링 | P1 | `DesignSystem.cs`, `DesignSystemTests.cs` | A-P0 병합 | 표면 불변 + Tab 포커스 링. `docs/design-system.md:50-57` 계약 유지 |
| 2 | **A-P0-B** 설정 10–11px → 12px | P1 | `SettingsWindow.Layout.cs`, `SettingsWindow.Notifications.cs` | A-P0 | 860×680 스크롤·줄바꿈 회귀 없음 |
| 3 | **D UI-09** CustomPet 분 | P2 | `CustomPetWindow.cs` + 테스트 | — | 비활성 사유 HelpText |
| 3 | **D UI-11** BreakReview·PetWindow 분 | P2 | `BreakReviewWindow.cs`, `PetWindow.cs` | C-P0과 `BreakReviewWindow.cs` 충돌 → C-P0 뒤 | 행 시맨틱, 펫 접근성 이름 |
| 4 | **D UI-09+UI-10** PetPack 묶음 | P2 | `PetPackWindow.cs`, `PetPackWindowTests.cs` | 한 소유자가 직렬 수행 | 오류 Error 색 + 비활성 사유 |
| 4 | **D UI-09+UI-11** Personalization 묶음 | P2 | `PersonalizationWindow.cs` + 테스트 | 기존 applyProfile HelpText 보존 | 탭 그룹 이름 |
| 5 | **A-P1** 색 이름 이행·타이포·간격·컴포넌트 변형 (라이트 팔레트 제외) | P1 | `DesignSystem.cs`, `Ui.cs`, 각 View | 1~4 완료 | 묶음마다 기본·최소·스크롤 캡처 재생성 |
| 6 | **통합 QA** | — | `SmokeDiagnostics.cs` 일괄 + 전체 검증 | 전 항목 | 전체 테스트, Release 빌드, 격리 스모크, 새 baseline 대비 캡처 비교 |

## 6. 병렬화 규칙

- `DesignSystem.cs`는 A-P0 → D UI-07 → A-P1 순서로 **항상 한 명만** 소유한다.
- `SmokeDiagnostics.cs`는 QA-P0(캡처 정정)과 6단계 통합에서만 수정한다. 그 사이 구현자는 키 이름만 예약한다.
- `BreakReviewWindow.cs`는 C-P0 완료 후 D UI-11이 받는다.
- `PetPackWindow.cs`의 UI-09와 UI-10은 분리 실행하지 않는다.
- 8px `ScrollGutter`(`Ui.cs:49-66`)는 간격 토큰 변경 대상에서 제외한다.
- 각 묶음 완료 직후 새 baseline과 같은 절차로 캡처를 재생성해 비교한다.

## 7. 실기 미검증 (자동 검사로 대체 불가)

- 실제 마우스·키보드로 수행한 날짜 행 펼침, 트레이 동작, 파일 교체, 최소 창 스크롤.
- VoiceOver·Narrator의 낭독 순서와 포커스 이동.
- Windows 100~200% DPI, 다중 모니터, 펫 창 클릭 통과, 작업 영역 clamp.
- 경고가 있는 **실제** 펫 팩의 고정 경고 요약 — 현재 진단 팩은 경고 0개라 한 번도 렌더링되지 않았다.
- 실제 펫 아트의 192/288/384px 품질. 현재 미리보기는 주황 윤곽 진단 고정물이다.
- 접힌 배지의 실제 배경 위 체감 대비와 진행 중 채운 점 비교.

## 8. 구현 실행 제약 (2차 착수 시 확인)

- 워커 전원이 **같은 체크아웃**을 사용한다. 사용자의 미커밋 변경 28개가 기준이므로 새 worktree를 만들면 그 변경이 따라오지 않는다.
- 따라서 파일 소유권이 겹치지 않아도 **구현은 순차 실행**한다. 병렬 `dotnet build`/`test`는 `obj`·`bin` 경합과 반쯤 쓰인 파일로 서로의 검증을 오염시킨다.
- 실행 순서: A-P0(토큰) → C-P0(UI-14) → QA-P0(캡처 진단) → D UI-07 → 나머지 P2.
- `DesignSystemTests.cs`는 `BreakReviewWindow`를 45·91·249행에서 생성한다. C-P0은 **생성자 시그니처를 바꾸지 않는다**. 바꿔야 하면 감독자에게 올린다.
