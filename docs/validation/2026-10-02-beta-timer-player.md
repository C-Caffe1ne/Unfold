# 구현 보고서

## 원인

사용자가 선택한 중앙 배치 타이머 시안과 Pinterest 영상의 숫자 전환을 현재 Beta 앱에 적용했다. 기존 타이머는 숫자·상태·조작 버튼이 분산돼 있었고, 일시정지·중지 상태의 숫자 표현과 실행 상태의 차이가 작았다.

작업 전 최신 공개 릴리스 `v1.0.2-beta`, 설치 앱 `/Applications/Unfold.app`의 숫자 버전 `1.0.2`, 실제 Beta 소스의 프로젝트 버전 `1.0.2-beta`를 확인했다. 구현 대상은 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`다. 이 작업 트리의 HEAD는 `37c6de354ceb390500d4bcb586bde1a74d16e205` detached 상태지만 배포 Beta 코드와 이후 펫 수정이 미커밋 작업 트리에 포함돼 있다. 현재 문서를 보관하는 `release/mvp` / `c93e168` / `0.2.2` 체크아웃은 구현 기준으로 사용하지 않았다.

배포 입력 해시 216개와 비교했을 때 작업 시작 전 차이는 기존 게시 도구·릴리스 노트 4개와 앞서 승인된 펫 수정 9개였다. 해당 변경을 보존하고 타이머 관련 파일만 수정했다.

## 변경

- 상태 배지 → 남은 시간 → 조작 버튼을 중앙에 세로로 배치했다. 최소 860×680 창에서 기존 펫 영역을 유지하도록 타이머 카드 높이를 216px로 맞췄다.
- 실제 1초 감소 때 바뀌는 숫자 자리만 380ms 동안 위로 전환한다. 기존 숫자가 위로 나가고 다음 숫자가 아래에서 들어오며, 콜론과 변하지 않는 자리는 고정한다.
- 일시정지·중지 상태의 숫자 불투명도를 55%로 낮췄다. 상태 배지와 조작 버튼은 선명하게 유지한다. 유휴 자동 일시정지와 진행 중인 휴식 알림도 비활성 숫자로 표시한다.
- 버튼은 아이콘과 `시작` / `일시정지` / `계속`, `중지` 텍스트를 함께 표시한다. 타이머 버튼의 호버·누름 색 피드백을 제거하고 키보드 포커스는 유지한다.
- 일시정지·시간 초기화·창 숨김·분리·종료 때 숫자 애니메이션을 중단한다. 240분 표시와 분 경계의 여러 자리 변경을 처리한다.
- 통합 진단에서 발견된 회귀를 수정했다. 타이머가 중지돼 있어도 휴식 알림이 열려 있으면 `중지` 버튼을 사용할 수 있어야 알림을 취소할 수 있다. 이를 회귀 검사에 추가했다.
- 사용자의 문구 정정 요청에 따라 버튼의 `정지`를 `중지`로, 툴팁과 접근성 이름의 `타이머 정지`를 `타이머 중지`로 바로잡았다. 이 후속 수정의 앱 소유 파일은 `TimerControls.cs` 하나이며, 다른 문구·배치·요소는 바꾸지 않았다.

## 소유 파일

Beta 작업 트리의 아래 9개 파일만 구현 대상으로 수정했다. [변경 패치](2026-10-02-beta-timer-player/timer-update.patch)는 이번 작업 시작 직전 Beta 상태를 기준으로 작성했다.

- `src/Unfold.Desktop/AnimatedCountdown.cs` — 새 숫자 렌더러와 애니메이션 수명 관리.
- `src/Unfold.Desktop/SettingsWindow.cs` — 상태 갱신·비활성 표시·창 수명 연결.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs` — 중앙 배치.
- `src/Unfold.Desktop/TimerControls.cs` — 아이콘·한국어 텍스트·조작 가능 상태.
- `src/Unfold.Desktop/DesignSystem.cs` — 타이머 버튼의 호버·누름 스타일.
- `src/Unfold.Desktop/SmokeDiagnostics.cs` — 상태·배치·숫자 전환 진단.
- `Tests/Unfold.Tests/AnimatedCountdownTests.cs` — 새 숫자 전환 회귀 검사.
- `Tests/Unfold.Tests/TimerControlTests.cs` — 버튼·비활성 표시·알림 취소 회귀 검사.
- `Tests/Unfold.Tests/SettingsDashboardTests.cs` — 중앙 배치 계약 검사.

작업 전 파일과 기존 전체 추적 변경 패치는 `/tmp/unfold-timer-update`에 보존했다. 변경을 반영할 때마다 소유 파일 해시를 확인했다. 소유 범위 밖의 기존 추적 변경은 작업 전후 동일하며, 최종 9개 파일의 [SHA-256 기록](2026-10-02-beta-timer-player/source-inputs.json)을 보관했다.

## 검증

- [집중 Release 검사](2026-10-02-beta-timer-player/timer-focused.trx): **44/44 통과**, 실패·건너뜀 0.
- Core/Desktop/Tests Release 빌드: **경고 0, 오류 0**.
- [전체 Release 검사](2026-10-02-beta-timer-player/timer-full.trx): **539/539 통과**, 실패·건너뜀 0.
- 새 `UNFOLD_DATA_DIR=/tmp/Unfold-beta-timer-smoke-It72jJ`로 실행한 macOS 통합 진단: 종료 코드 0, `success=true`, `timerControlsVerified=true`, `timerDigitMotionVerified=true`. [진단 원본](2026-10-02-beta-timer-player/smoke.json).
- 실제 Avalonia 렌더링의 [실행](2026-10-02-beta-timer-player/timer-running.png), [일시정지](2026-10-02-beta-timer-player/timer-paused.png), [중지](2026-10-02-beta-timer-player/timer-stopped.png), [최소 창](2026-10-02-beta-timer-player/home-minimum.png)을 확인했다.
- macOS 유휴 상태로 실제 시계가 자동 정지할 수 있어, 숫자 이동은 별도 네이티브 진단 창에 47:28 → 47:27을 주입해 검사했다. [이전 숫자가 나가는 프레임](2026-10-02-beta-timer-player/timer-roll-out.png)과 [다음 숫자가 들어오는 프레임](2026-10-02-beta-timer-player/timer-roll-in.png)을 확인했다. 이는 OS에서 렌더링한 자동 진단이며 물리 입력 검사가 아니다.
- Beta 작업 트리 `git diff --check` 통과. 실행용 로컬 번들의 `Unfold.dll` SHA-256은 최종 검사한 Beta 빌드와 동일하다.
- 문구 정정 후 [타이머 집중 Release 검사](2026-10-02-beta-timer-player/timer-label-focused.trx) **10/10 통과**와 Core/Desktop/Tests 빌드를 확인했다. 위 539개 전체 검사는 문구 정정 직전 결과이며, 문자열 두 곳만 바꾼 후속 수정에서 전체 검사를 반복하지 않았다. 로컬 실행 번들도 다시 빌드한 어셈블리로 갱신했다.
- 문구 정정 후 macOS 진단의 [홈 화면 캡처](2026-10-02-beta-timer-player/timer-label.png)에서 `중지` 버튼을 확인했다. 이번 추가 통합 진단은 이후 확인 대화상자 검사에서 `TimerStop` 비활성 상태로 중단됐다. [해당 결과](2026-10-02-beta-timer-player/timer-label-smoke.json). 따라서 이 실행을 전체 통합 진단 통과로 보고하지 않는다. 문구 두 곳 이외의 앱 코드는 이번 후속 수정에서 변경하지 않았다.

## 미검증·위험

실행용 수정본은 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-timer-player-2026-10-02/Unfold.app`에 만들었다. 표시 이름은 `Unfold Beta v1.0.2 · 타이머 수정본`이며 `--version`은 `Unfold Beta v1.0.2 (1.0.2-beta)`다. [빌드 정보](2026-10-02-beta-timer-player/local-build.json). 이 번들은 설치된 .NET 10을 사용하는 로컬 실행용이며 ad-hoc 서명했다.

로컬 앱을 실행했으나 저장된 계정의 로그인 확인 오류로 현재 `다시 확인` 화면에 머물러 있다. 한 번 재확인했으며 로그인·구매 게이트 코드는 변경하지 않았다. 자동 진단의 계정 서비스는 테스트 대역이므로 실제 로그인·구매 성공을 의미하지 않는다.

Windows 실기와 물리 입력, OS의 동작 줄이기 설정 연동, 공개 설치 파일의 서명·공증·게시 검사는 수행하지 않았다. 숫자 렌더러 자체의 애니메이션 비활성 기능만 검사했다. `/Applications/Unfold.app`과 공개 Beta 다운로드는 이번 로컬 수정본으로 교체하지 않았다.
