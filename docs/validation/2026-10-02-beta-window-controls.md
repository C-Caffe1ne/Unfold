# 구현 보고서

## 원인

사용자가 첨부한 Beta v1.0.2 주 창의 시스템 제목표시줄을 없애고 닫기·최소화·최대화 버튼을 앱 안에 배치하도록 요청했다. 구현은 이전 타이머 수정과 `중지` 문구 정정을 포함한 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`의 `1.0.2-beta` 소스를 기준으로 했다.

기존 창의 닫기는 앱 종료가 아니라 트레이로 숨기는 동작이다. 새 닫기 버튼에서도 이 동작을 유지해야 하며, 기존 타이머·펫·설정의 문구와 화면 영역을 바꾸지 않아야 한다.

## 변경

- 주 창을 `WindowDecorations.BorderOnly`와 클라이언트 영역 확장으로 구성해 시스템 제목표시줄과 시스템 창 버튼을 제거했다. OS 창 테두리의 크기 조절은 유지했다.
- 왼쪽 상단 앱 프레임 안에 닫기·최소화·최대화의 세 버튼만 배치했다. 기존 macOS 버튼의 빨강·노랑·초록 색상 순서를 사용했다. 버튼에 각각 접근성 이름과 툴팁을 제공한다.
- 닫기는 기존 `Close()` → `Closing` → `HideToTray()`에 연결했다. 타이머를 중지하거나 앱을 종료하지 않는다. 최소화는 OS 최소화 상태, 최대화는 최대화/일반 크기 복원으로 연결했다.
- 빈 상단 영역은 창 이동을 지원한다. macOS는 기존 계정 창과 같은 화면 좌표 기반 드래그를 사용하고, 다른 OS는 `BeginMoveDrag`를 사용한다. 포인터 해제·캡처 상실·비활성화·창 상태 변경·숨김·종료 시 이동을 중단한다. 상단 빈 영역의 더블클릭은 최대화/복원으로 연결한다.
- 기존 상단 여백을 세 버튼에 사용해 타이머·펫의 콘텐츠 시작 위치를 보존했다. 기본 창과 860×680 창에서 타이머 y=31, 높이=216, 펫 y=267은 이전과 동일하다. 제목이나 부가 문구는 추가하지 않았다.

사용한 Avalonia 12 API는 설치된 12.1.2 참조와 [공식 창 API 안내](https://docs.avaloniaui.net/docs/how-to/window-how-to)에서 확인했다.

## 소유 파일

Beta 작업 트리의 아래 5개 파일만 수정했다.

- `src/Unfold.Desktop/SettingsWindow.Chrome.cs` — 새 창 버튼·이동 처리.
- `src/Unfold.Desktop/SettingsWindow.cs` — 창 장식 설정·이동 수명 관리.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs` — 기존 프레임 내부에 버튼 배치.
- `src/Unfold.Desktop/SmokeDiagnostics.cs` — 네이티브 창 제어 진단.
- `Tests/Unfold.Tests/WindowControlTests.cs` — 상태·닫기·이동·입력 영역 회귀 검사.

작업 시작 직전 상태는 `/tmp/unfold-window-controls/before`와 `existing-changes.patch`에 보존했다. 반영할 때 기존 파일 해시를 확인했고 소유 범위 밖의 기존 추적 변경은 동일함을 확인했다. [변경 패치](2026-10-02-beta-window-controls/window-controls.patch)와 [소스 해시](2026-10-02-beta-window-controls/source-inputs.json)를 보관했다.

## 검증

- [집중 Release 검사](2026-10-02-beta-window-controls/window-controls-focused.trx): **27/27 통과**.
- 최종 Core/Desktop/Tests Release 빌드: **경고 0, 오류 0**.
- [전체 Release 검사](2026-10-02-beta-window-controls/window-controls-full.trx): **542/542 통과**, 실패·건너뜀 0.
- 첫 전체 검사에서 새 입력 영역 검사가 한 번 실패했다. 기존 반응형 검사와 같이 창 크기 변경 후 `UpdateLayout()`으로 배치를 완료한 다음 확인하도록 보완했고 최종 전체 검사가 통과했다.
- 새 테스트 데이터 `/tmp/Unfold-beta-window-controls-smoke-nAJ4eZ`로 실행한 macOS 통합 진단: 종료 코드 0, `success=true`, `windowControlsVerified=true`, `timerControlsVerified=true`. [원본 결과](2026-10-02-beta-window-controls/smoke.json).
- macOS 네이티브 창에서 최소화 → 복원 → 최대화 → 복원 → 닫아 숨김 → 다시 표시를 자동 검사했다. 닫기 전후 타이머 중지·일시정지 상태가 유지되는지도 확인했다.
- [주 창](2026-10-02-beta-window-controls/window-controls.png), [860×680 창](2026-10-02-beta-window-controls/home-minimum.png), [640×560 창](2026-10-02-beta-window-controls/responsive-home-top.png)을 렌더링해 세 버튼과 기존 콘텐츠의 배치를 확인했다.
- 포인터 이동·해제·캡처 상실과 버튼 입력 분리는 Avalonia headless 합성 입력으로 검사했다. 이는 실제 마우스 입력 검증과 구별한다.
- 로컬 수정 앱을 재시작했고, 실제 주 창의 접근성 트리에서 `SettingsWindowClose`·`SettingsWindowMinimize`·`SettingsWindowMaximize` 버튼이 표시되며 기존 시스템 창 버튼이 제거된 것을 확인했다.

## 미검증·위험

변경 범위는 첨부 화면의 주 창이다. 계정 창·알림·확인 대화상자의 창 장식은 이번에 수정하지 않았다. macOS의 물리 마우스로 창 이동·테두리 크기 조절을 확인하는 검사와 Windows 실기의 창 이동·크기 조절·최대화 검사는 수행하지 않았다.

로컬 실행용 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-timer-player-2026-10-02/Unfold.app`에 최종 Release 어셈블리를 반영하고 ad-hoc 서명했다. [빌드 정보](2026-10-02-beta-window-controls/local-build.json). 설치된 .NET 10을 사용하는 로컬 Beta 수정본이며 공개 설치 파일의 교체·서명·공증·게시 작업은 포함하지 않는다.
