# 버전 표시·타이머·알림 개선 — 2026-10-05

## 원인

설정 화면에는 버전·업데이트 진입점이 없고 트레이에서만 업데이트를 열 수 있었다.
타이머 UI·설정 저장·프로필·시계 로직은 최소 5분을 전제로 했다.
휴식 초대에는 무응답 종료 시간이 없었고, 호버 시계는 24시간·초 단위로 표시했다.
말풍선 시간은 즉시 교체했으며 종료·중지 버튼은 일반 색상이었다.

작업 기준은 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`,
`codex/beta-v1.0.4`, HEAD `717a63b68071b8ecb24c75bbfbbaa57e7256039d`, `1.0.4-beta`다.
작업 전 `git worktree list`와 각 후보의 브랜치·HEAD·미커밋 변경·프로젝트 버전을 확인했다.
`release/mvp`의 `0.2.2`, `feat/pet-pack-ux`의 `1.0.3-beta`보다 최신인 이 경로에서 구현·빌드·실행했다.
실행 대상은 `src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll`이다.

## 변경

1. 좌측 하단에 `Beta v1.0.4`를 두 줄로 표시한다. 설정의 **앱 정보**에는 같은 버전과
   **업데이트 확인** 버튼을 배치하고 기존 확인·다운로드·재시작 흐름을 연결한다.
2. 설정·로그인 화면의 종료, 타이머 중지, 로그아웃과 종료·중지·삭제·초기화 등의 확인 버튼에
   테마별 Danger(빨간색)를 적용한다. 중지의 호버·누름 상태도 빨간색을 유지한다.
   비활성 버튼은 기존 비활성 색상을 사용하며 OS 트레이 메뉴는 운영체제가 그린다.
3. 알림 간격을 **1~240분**으로 변경한다. UI 입력·검증·저장 복구·프로필·타이머 범위를 함께 맞춘다.
   5분 이하의 작업 간격에는 5분 전 알림을 표시하지 않는다.
4. 휴식 초대가 처음 표시된 시점부터 **30초** 후, 응답이 없으면 설정한 **다시 알림 시간**만큼 미룬다.
   화면 갱신·중복 알림으로 기한을 연장하지 않는다. 수동 미루기·휴식 시작·중지는 기한을 해제한다.
   진행 중인 휴식과 디버그 미리보기에는 자동 미루기를 적용하지 않는다.
   일시정지·완료 기록·완료 효과음을 보존하고, 알림 동안 잠시 표시했던 펫은 다시 숨긴다.
5. 호버 현재 시각을 **오전/오후 hh:mm**으로 표시한다. 자정·정오도 한국어 12시간 형식으로 처리한다.
   남은 시간과 휴식 타이머는 기존처럼 초까지 표시한다.
6. 말풍선 현재 시각·남은 시간·휴식 타이머는 텍스트가 바뀔 때 **180ms** 동안 위로 3px 이동하며
   투명도 55%에서 100%로 전환한다. 동일한 텍스트 갱신은 전환을 재시작하지 않는다.
   숨김·분리 시 프레임 타이머를 중지하며 펫 캔버스와 말풍선·창의 배치에는 효과를 적용하지 않는다.

각 항목의 설명 문장은 제품 UI에 추가하지 않았다. 기존 GLB 최적화, 통합 펫 팩 편집기,
캔버스 변형 제거, 투명 클릭 통과 및 이번 범위 밖의 기존 변경을 보존했다.

## 소유 파일

- Core: `AppSettings.cs`, `Personalization.cs`, `StretchClock.cs`, `PetReminder.cs`.
- Desktop: `SettingsWindow.cs`, `SettingsWindow.Layout.cs`, `ProfileEditorWindow.cs`, `AccountWindow.axaml`,
  `TimerControls.cs`, `Ui.cs`, `DesignSystem.cs`, `AppRuntime.cs`, `PetSpeechBubble.cs`, 새 `AnimatedTimeText.cs`.
- 검사: 새 `TimerRefinementTests.cs`, `PetHoverTests.cs`, `SettingsRecoveryTests.cs`, `AccountScreenTests.cs`, `OriginalCompanionTests.cs`,
  `SmokeDiagnostics.cs`, 새 `SmokeDiagnostics.TimerRefinements.cs`.
- 문서: `docs/settings-ui.md`, `docs/stretch-notifications.md`, `docs/README.md`, `docs/verification.md`, 이 기록과 첨부 근거.

## 검증

| 검사 | 결과 |
|---|---|
| Release 빌드 | 오류·경고 0 |
| 초기 관련 집중 검사 | 96 통과 |
| 최종 입력·타이머 집중 검사 | 19 통과 |
| 최종 전체 자동 검사 | 659 통과 / 실패·건너뜀 0 |
| macOS 격리 통합 진단 | 성공, 112장 생성·주요 화면 검수 |
| 1분 간격 | UI 저장·재로드·시계 값과 1/2/4분 만료 확인 |
| 버전·업데이트 | 1120×800·640×560의 버전, 설정에서 기존 창 열기·중복 방지 확인 |
| Danger 색상 | 자동 검사에서 모든 테마의 기본·호버 색상, macOS 렌더링 확인 |
| 말풍선 시간 | 자정·정오·오후 형식, 값 변경·정지·숨김·분리와 전환 종료 확인 |
| UI 배치 | 전환 중 말풍선 bounds·창 크기·창 위치 유지 확인 |
| 기존 변경 보존 | 시작 시 SHA-256과 비교, 소유 범위 밖 파일 변화 없음 |
| 정적 검사 | `git diff --check` 통과 |

기존 설정 복구 검사에서 3분을 손상된 값으로 취급하던 입력은 새 범위에 맞춰 0분으로 갱신했다.
반복 짧은 클릭 검사에서는 Headless의 `MouseDown`이 반환 전에 렌더링해 약 547ms 걸렸고,
220ms 누름 기준을 넘어 `land`로 재생되는 것을 상태 기록으로 확인했다. 이 상태 전이 검사는
`BeginCompanionPress`/`ReleaseCompanionPress(clicked: true)`로 짧은 클릭을 직접 지정하도록 수정했다.
실제 포인터 이벤트·누름 임계·드래그 검사는 기존대로 유지하며 함께 재검증했다.
제품의 클릭·누름 판정 기준은 변경하지 않았다.

새 자동 검사는 초대가 표시되기 전 무기한 대기, 표시 후 29.999초/30초 경계,
중복 갱신·수동 미루기·휴식 시작과 새 초대의 새 기한을 포함한다.
런타임에서는 설정한 9분 미루기와 실행/일시정지 상태를 확인한다.

macOS 진단은 새 빈 `UNFOLD_DATA_DIR`로 실행한다. OS 창과 실제 렌더러를 사용하되 창은 화면 밖에 두고,
버튼은 프로그래밍 방식으로 호출한다. 실제 시간을 기다려 초대 표시 후 약 30.29초 자동 미루기와 설정한 3분 예약,
일시정지 유지·임시 펫 숨김·완료 기록 미변경을 확인한다. 텍스트 전환은 시작·중간·종료 상태를 캡처한다.

[검증 요약·해시](2026-10-05-timer-settings-refinements/verification.json),
[전체 검사 로그](2026-10-05-timer-settings-refinements/full-tests.log),
[macOS 통합 진단](2026-10-05-timer-settings-refinements/smoke.json),
[홈 화면](2026-10-05-timer-settings-refinements/timer-running.png),
[설정 최소 크기](2026-10-05-timer-settings-refinements/timer-refinement-settings-640.png),
[호버 시계](2026-10-05-timer-settings-refinements/timer-refinement-hover-settled.png).

## 미검증·위험

- Windows 실기 실행, 물리 마우스·키보드 조작은 이번 검증에 포함하지 않았다.
- 업데이트 버튼은 기존 업데이터에 연결했다. 테스트 백엔드의 확인 호출과 개발 실행본의 업데이트 화면을 확인했으며,
  실제 설치본에서 새 릴리스를 다운로드·적용·재시작하는 과정은 이번 작업에서 수행하지 않았다.
- 자동 미루기는 UI 스레드가 실행될 때 처리하므로 절전·UI 정지 중에는 정확한 벽시계 30초 시점의 처리를 보장하지 않는다.
- 버전 증가는 하지 않았으며 커밋·배포·설치본 교체는 수행하지 않았다.
