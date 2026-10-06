# 구현 보고서

## 원인

주 창 전체를 채우는 사각 배경과 내부의 큰 라운드 프레임이 중첩되어 있었다. 사용자가 창 자체에 라운딩을 넣고 내부 콘텐츠 영역의 테두리를 제거하도록 요청했다.

현재 로컬 앱의 빌드 정보와 Beta 프로젝트 버전을 확인해 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`의 `1.0.2-beta` 소스를 사용했다. 기반 커밋은 `37c6de354ceb390500d4bcb586bde1a74d16e205`이며, 이전 펫·타이머·창 버튼 수정이 포함된 작업 트리이다. 루트 저장소의 구버전 `0.2.2` 소스는 수정하지 않았다.

## 변경

- 주 창의 시스템 제목표시줄은 제거한 상태로 유지하고 투명 창 배경을 사용했다. 창 전체를 채우는 `SettingsWindowSurface`에 기존 디자인 토큰의 32px 라운딩과 클리핑을 적용해 실제 외곽 네 모서리가 투명해진다.
- `WindowDecorations.BorderOnly`와 클라이언트 영역 확장을 유지해 기존 OS 테두리 크기 조절을 보존한다. Avalonia 12.1.2의 [macOS 창 구현](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/native/Avalonia.Native/src/OSX/WindowImpl.mm#L518-L555)에서 `None`은 기본 크기 조절 마스크를 제외하고 `BorderOnly`는 유지하는 것을 확인했다. 투명 배경과 표면의 라운딩이 보이는 외곽을 정의한다.
- 안쪽 `SettingsFrame`의 테두리, 자체 배경과 라운딩을 제거했다. 제거된 1px 테두리 폭을 패딩으로 보완해 기존 카드와 창 버튼의 위치를 유지했다.
- 최대화와 전체 화면에서는 외곽 라운딩을 0으로, 일반 창 복원 시 32px로 설정한다.
- 테마 색은 새 외곽 표면에 연결했다. 기존 UI 문구, 타이머 동작, 카드 모양과 창 버튼 배치는 유지했다.

구현은 설치된 Avalonia 12.1.2 API와 [공식 창 관리 안내](https://docs.avaloniaui.net/docs/app-development/window-management), [클리핑 안내](https://docs.avaloniaui.net/docs/graphics-animation/clipping-and-masking)를 근거로 했다.

## 소유 파일

Beta 작업 트리의 아래 5개 파일만 수정했다.

- `src/Unfold.Desktop/SettingsWindow.cs` — 투명 창과 시스템 장식 설정.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs` — 외곽 라운딩과 내부 프레임 정리.
- `src/Unfold.Desktop/SmokeDiagnostics.cs` — 실제 투명 창 상태와 렌더링 알파 검증.
- `Tests/Unfold.Tests/WindowControlTests.cs` — 외곽 표면, 복원 시 라운딩, 기존 배치와 입력 영역 검사.
- `Tests/Unfold.Tests/ThemeTests.cs` — 테마 배경의 검증 대상을 새 외곽 표면으로 변경.

작업 전 파일은 `/tmp/unfold-rounded-window/before`에 보존했고 반영 전에 파일 해시를 대조했다. 소유 범위 밖의 기존 추적 변경은 작업 전과 동일했다. [이번 작업의 패치](2026-10-02-beta-rounded-window/rounded-window.patch)와 [소스 입력 해시](2026-10-02-beta-rounded-window/source-inputs.json)를 보관했다.

## 검증

- Core/Desktop/Tests Release 컴파일 성공.
- [최종 집중 검사](2026-10-02-beta-rounded-window/rounded-window-focused.trx): **33/33 통과**, 실패·건너뜀 0. 창 버튼, 타이머, 대시보드, 반응형 배치와 모든 테마를 검사했다.
- [최종 전체 Release 검사](2026-10-02-beta-rounded-window/rounded-window-full.trx): **543/543 통과**, 실패·건너뜀 0.
- 첫 전체 검사에서 테마 검사가 이전 창 배경 위치를 참조하고 있었고 입력 영역 검사에서는 headless 렌더 장면이 배치 갱신보다 늦게 반영되어 실패했다. 테마 검사는 새 표면을 확인하도록 변경하고 입력 검사는 렌더 타이머를 완료한 뒤 수행하도록 보완했다. 검사 조건을 제거하지 않았으며 최종 전체 검사가 통과했다.
- 새 데이터 디렉터리 `/tmp/Unfold-beta-rounded-window-final-smoke-laxKIL`로 macOS 네이티브 진단 실행: 종료 코드 0, `success=true`, `roundedWindowVerified=true`, `windowControlsVerified=true`, `timerControlsVerified=true`. [원본 진단 결과](2026-10-02-beta-rounded-window/smoke.json).
- 네이티브 창의 `ActualTransparencyLevel == Transparent`, 표면의 창 전체 크기, 32px 반경과 클리핑, 안쪽 테두리 0을 검사했다. [외곽 창 렌더링](2026-10-02-beta-rounded-window/rounded-window.png)의 네 모서리 알파는 모두 0이고 상단 중앙 배경 알파는 255였다.
- 최소화 → 복원 → 최대화 → 복원 → 닫아 숨김 → 다시 표시를 네이티브 창에서 자동 검사했다. 닫기 전후 타이머 상태를 유지했다.
- 기본 1120×800 및 860×680에서 타이머 y=31, 높이=216, 펫 y=267을 유지했다. [860×680 화면](2026-10-02-beta-rounded-window/home-minimum.png)과 [640×560 화면](2026-10-02-beta-rounded-window/responsive-home-top.png)도 확인했다.
- `git diff --check` 통과. 로컬 앱 번들의 최종 어셈블리 교체, ad-hoc 재서명과 `codesign --verify --deep --strict` 통과. [로컬 빌드 정보](2026-10-02-beta-rounded-window/local-build.json).

## 미검증·위험

변경 대상은 주 창이다. 물리 마우스로 창 이동·테두리 크기 조절을 확인하는 검사와 Windows 실기 검사는 수행하지 않았다. 포인터 입력 영역과 이동 처리는 Avalonia headless 합성 입력으로 검사했다. 투명을 지원하지 않는 플랫폼에서는 테마 배경으로 대체된다.

로컬 수정 앱 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-timer-player-2026-10-02/Unfold.app`에 반영했다. 실행 프로세스의 동일 경로를 확인했으나 일반 실행에서는 계정 창의 `구매 확인 중` 상태가 표시되어 주 창의 화면 확인은 위의 격리된 네이티브 진단 실행으로 진행했다. 인증 상태를 바꾸거나 우회하지 않았다.

설치된 .NET 10을 사용하는 로컬 Beta 수정본이다. `/Applications/Unfold.app` 교체와 공개 배포 파일의 게시·공증 작업은 포함하지 않는다.
