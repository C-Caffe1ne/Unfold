# 구현 보고서

## 원인

설정 탭은 변경 사항을 폼에 보관한 뒤 하단 저장 버튼을 눌러 적용하는 구조였다. 다른 페이지로 이동할 때 저장·저장 안 함·취소를 고르는 모달이 표시되었다. 사용자는 두 버튼과 이탈 모달을 제거하고 설정 변경을 즉시 적용하도록 요청했다.

현재 로컬 앱의 `build.json`, Beta 프로젝트 버전과 두 앱 번들의 버전을 확인했다. 구현 소스는 이전 펫·타이머·창 버튼·외곽 라운딩 수정이 포함된 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`, 버전 `1.0.2-beta`, 기반 커밋 `37c6de354ceb390500d4bcb586bde1a74d16e205`이다. 루트 저장소의 구버전 소스는 수정하지 않았다.

## 변경

- 설정 탭의 저장·취소 버튼과 버튼을 위해 예약했던 하단 공간을 제거했다. 기존 오류 표시 영역은 오류가 있을 때 표시하도록 유지했다.
- 말풍선 위치, 전체·개별 음량, 알림 효과음 사용, 자리 비움·다시 알림 시간과 디버그 도구 설정 변경을 즉시 런타임에 적용하고 파일에 저장한다. 기존 펫 표시·자동 실행 설정의 즉시 적용 동작도 유지한다.
- 효과음 가져오기가 끝나면 즉시 저장한다. 기본 효과음으로 복원할 때도 바로 적용하며, 다른 항목이 참조하는 파일과 사용자의 원본 파일은 보존한다.
- 설정을 저장하지 않고 이동하는 확인 모달과 해당 이동 차단 경로를 제거했다. 효과음 가져오기 중에 다른 페이지로 이동해도 완료된 결과가 적용된다.
- 숫자 입력이 비어 있거나 1~60분의 정수가 아니면 마지막 유효값을 유지한다. 이 상태에서도 다른 유효한 설정은 적용된다. 기존 입력 오류 문구를 사용한다.
- 저장 실패 시 편집값과 기존 오류 표시를 유지하고 다음 변경 또는 설정 탭 재진입에서 재시도한다. 오래된 비동기 작업의 오류가 최신 변경 상태를 덮지 않도록 변경 순서를 확인한다.
- 빠른 연속 편집도 각 변경 시 바로 저장한다. `AppRuntime.UpdateSettings`가 펫 갱신을 기다리기 전에 파일 저장과 런타임 값 변경을 완료하는 계약을 사용했다. 타이머 진행 시간과 홈 시간 설정의 별도 저장 범위는 유지했다.

추가 UI 문구나 기능은 넣지 않았다.

## 소유 파일

Beta 작업 트리의 아래 15개 파일만 수정했다.

실행 코드 5개:

- `src/Unfold.Desktop/SettingsWindow.Preferences.cs`
- `src/Unfold.Desktop/SettingsWindow.Layout.cs`
- `src/Unfold.Desktop/SettingsWindow.Notifications.cs`
- `src/Unfold.Desktop/Ui.cs`
- `src/Unfold.Desktop/SmokeDiagnostics.cs`

회귀 검사 10개:

- `Tests/Unfold.Tests/SettingsPreferencesTests.cs`
- `Tests/Unfold.Tests/SettingsNavigationGuardTests.cs`
- `Tests/Unfold.Tests/SettingsDashboardTests.cs`
- `Tests/Unfold.Tests/SoundCleanupTests.cs`
- `Tests/Unfold.Tests/ResponsiveLayoutTests.cs`
- `Tests/Unfold.Tests/UiAuditRegressionTests.cs`
- `Tests/Unfold.Tests/ThemeTests.cs`
- `Tests/Unfold.Tests/NotificationLayoutTests.cs`
- `Tests/Unfold.Tests/Mp3SoundTests.cs`
- `Tests/Unfold.Tests/TimerControlTests.cs`

작업 전 파일은 `/tmp/unfold-settings-immediate-2026-10-03/before`에 보존했다. 반영 전에 파일 해시를 대조했고, 소유 범위 밖의 기존 추적 변경이 작업 전과 동일함을 확인했다. [이번 작업의 패치](2026-10-03-beta-settings-immediate/settings-immediate.patch)와 [소스 입력 해시](2026-10-03-beta-settings-immediate/source-inputs.json)를 보관했다.

## 검증

- Core/Desktop/Tests Release 컴파일 성공.
- [집중 검사](2026-10-03-beta-settings-immediate/settings-immediate-focused.trx): **76/76 통과**, 실패·건너뜀 0.
- [전체 Release 검사](2026-10-03-beta-settings-immediate/settings-immediate-full.trx): **543/543 통과**, 실패·건너뜀 0.
- 직접 숫자 입력, 슬라이더 101회 연속 변경과 최종값 저장, 체크박스·드롭다운 변경, 창 숨김·해제 후 값 유지, 재시작 후 저장값 복원, 잘못된 숫자 입력, 저장 실패와 재시도, 다른 페이지 이동과 가져오기 중 이동을 검사했다.
- WAV·MP3 가져오기, 미리듣기 전환·취소, 기본 효과음 복원, 공유 파일의 참조 유지, 실패한 가져오기·저장 시 기존 파일 보호와 종료 중 변환 취소도 검사했다.
- 최초 macOS 진단은 앱 코드 실행 전에 Avalonia.Native RenderTimer 초기화 오류 `-6661`로 종료됐다. [초기 오류 로그](2026-10-03-beta-settings-immediate/initial-native-startup-error.log). GUI 앱 상태 확인 후 코드를 바꾸지 않고 새 테스트 데이터로 다시 실행했다.
- 최종 macOS 진단 `/tmp/Unfold-beta-settings-immediate-retry-kyJk02`: 종료 코드 0, `success=true`. `automaticApply=true`, `saveCancelRemoved=true`, `navigationWithoutModal=true`, `timerControlsVerified=true`, `windowControlsVerified=true`, `roundedWindowVerified=true`. [원본 결과](2026-10-03-beta-settings-immediate/smoke.json).
- 네이티브 렌더링에서 [860×680 상단](2026-10-03-beta-settings-immediate/settings-options-minimum.png), [하단](2026-10-03-beta-settings-immediate/settings-options-bottom.png), [1120×800 화면](2026-10-03-beta-settings-immediate/settings-options-default.png)과 [변경 직후 화면](2026-10-03-beta-settings-immediate/settings-options-applied.png)을 확인했다. 버튼과 하단 예약 공간이 제거되고, 스크롤 영역의 높이는 554px에서 618px로 늘었다. 640×560 반응형 화면도 검사했다.
- `git diff --check` 통과. 로컬 앱 번들의 최종 어셈블리를 교체하고 ad-hoc 재서명했으며 `codesign --verify --deep --strict` 통과. [로컬 빌드 정보](2026-10-03-beta-settings-immediate/local-build.json).

## 미검증·위험

입력·저장·화면 이동 검사는 Avalonia headless 합성 입력과 격리된 macOS 네이티브 진단으로 수행했다. 물리 마우스·키보드 입력, 실제 효과음 청취와 Windows 실기 검사는 수행하지 않았다.

일반 실행 앱의 계정 창은 `로그인 정보를 안전하게 저장하거나 삭제하지 못했어요. OS 보안 저장소 접근을 확인해 주세요.` 상태였다. 일반 로그인 완료 후 설정 탭에 접근하는 검사는 수행하지 않았으며 인증 정보를 바꾸거나 우회하지 않았다.

반영 대상은 `/Users/hwanghyeonseong/Documents/GitHub/Unfold/artifacts/local-timer-player-2026-10-02/Unfold.app`의 로컬 Beta 수정본이다. `/Applications/Unfold.app` 교체나 공개 설치 파일 게시·공증 작업은 포함하지 않는다.
