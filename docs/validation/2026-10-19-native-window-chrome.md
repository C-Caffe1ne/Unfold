# 일반 창 네이티브 크롬 검증

2026-10-19 · `codex/pet-overlay-hover`, 시작 HEAD `1c585fc`, 프로젝트 `1.2.0`. 커밋하지 않았다.
이전 계획: [네이티브 창 버튼](../plans/2026-10-05-native-window-controls.md).
후속: macOS 신호등은 [네이티브 배치 검증](2026-10-20-macos-native-title-bar.md)에서 32pt·x=9/32/55로 바뀌었다. 아래 28pt 값은 변경 전 측정이다.

## 변경 전 확인

- **macOS 설정 창은 어긋나 있지 않았다.** 신호등 좌표가 AppKit 표준 창과 같다(닫기·최소화·확대 x=7·27·47, y=6, 제목 영역 28pt).
  실제 포인터로 호버 기호, 최소화, 닫기(트레이 숨김·타이머 유지), 초록(전체 화면), 제목 영역 드래그, 더블클릭 확대·복원이 모두 동작했다.
- **macOS 계정 창에는 신호등이 없었다.** `WindowDecorations="None"`이라 NSWindow 표준 버튼이 없고, 이동은 좌표 계산 드래그였다.
- 업데이트·기록·커스텀 펫 창은 제목 글자가 있는 일반 제목 표시줄이었다. 펫 편집은 설정 창 안의 페이지라 별도 창이 아니다.
- Windows: Avalonia 12.1.2는 제목 영역을 확장하면 DWM 프레임 확장을 0으로 두고 시스템 메뉴의 닫기를 끈 뒤, Avalonia가 그린 45×30 버튼을 쓴다.
  사용자 결정에 따라 **확장하지 않고 Windows 11 네이티브 제목 표시줄을 테마 색으로 칠한다.**

## 변경

- `NativeWindowChrome`: `Full` 장식, macOS만 제목 영역 확장(높이 `-1`). 실제 `WindowDecorationMargin`만큼 본문을 내리고, 그 띠에 `TitleBar` 역할을 줘 OS 이동·더블클릭을 쓴다. 띠와 본문 배경은 테마 Canvas다.
- Windows 11(빌드 22000 이상): `DWMWA_CAPTION_COLOR`를 Canvas 색으로 칠하고 `SetWindowThemeAttribute`로 제목 글자·아이콘만 숨긴다. 시스템 메뉴, 작업 표시줄·Alt+Tab 제목, 캡션 버튼·스냅·드래그는 OS 그대로다. 테마를 바꾸면 다시 칠한다. 다크 모드 속성은 Avalonia가 앱 테마에 맞춰 설정한다. Windows 10은 표준 제목 표시줄을 유지한다.
- 적용: 설정, 계정, 업데이트, 기록, 커스텀 펫 창. 펫·말풍선·타이머 창과 확인 대화상자는 바꾸지 않았다.
- 계정 창: 머리글 드래그 영역에 `TitleBar` 역할을 줬다. macOS는 AppKit 이동·더블클릭, Windows는 기존 `BeginMoveDrag`. 좌표 계산 드래그 코드는 제거했다.
  구매가 필요한 상태에서 OS 닫기(빨간 버튼·Cmd+W·Alt+F4)는 기존 「Unfold 종료」 확인으로 연결한다. 프로그램 닫기는 이전처럼 무시한다.
- `SettingsWindow.Chrome.cs`의 내용을 공통 파일로 옮겼다.
- 스모크 진단 `nativeChrome`: 설정·계정·업데이트 창의 장식 구성, 제목 띠 높이와 입력 역할을 확인한다. Windows 11에서는 칠하기 HRESULT와 색이 맞지 않으면 실패한다.

## 검증

새 `UNFOLD_DATA_DIR`, macOS 26.6.2, .NET SDK 10.0.401.

| 검사 | 결과 |
|---|---|
| 새 검사 10개 | 구현 전 9개 실패 확인. 환영 화면 닫기 1개는 기존 동작 보호용으로 처음부터 통과. 기존 macOS 좌표 드래그 검사는 OS 이동 계약 검사로 교체 |
| 창 제어·계정·기록·업데이트·테마·종료 관련 집중 검사 | 88개 통과 |
| `dotnet build Unfold.slnx -c Release --no-restore` | 경고·오류 0 |
| 전체 `dotnet test Unfold.slnx -c Release --no-build` | **1,106개 통과**, 실패·건너뜀 0 |
| macOS 실제 창 `--smoke-test` | `success: true`, 약 57.7초. 설정·업데이트·계정 창 `extended: true`, `titleBarInset: 28` ([smoke.json](2026-10-19-native-window-chrome/macos-smoke.json)) |
| `git diff --check` | 통과 |

`nativeCaptionInputVerified`는 자동 진단이 실제 버튼을 누르지 않으므로 `false`로 둔다.
아래 실제 입력은 제품 밖 임시 검증 앱(같은 소스의 실제 창, 새 데이터 폴더)에 CGEvent 포인터 입력을 보내 확인했다. 화면은 1920×1080, 배율 1이다.

- 계정 창(구매 필요 상태): 표준 버튼 3개, 호버 기호, 빨간 버튼 → 종료 확인 요청 1회·창 유지, 노란 버튼 최소화, 머리글 드래그 이동(+80, +40), 머리글 더블클릭 확대 → 제목 띠 더블클릭 복원.
- 업데이트 창: 버튼 표시(크기 고정 창이라 확대 비활성), 밝은·다크 테마, 빨간 버튼으로 닫힘.
- 설정 창: 변경 전과 같은 배치.

| 화면 | 캡처 |
|---|---|
| 계정 창 변경 전 / 후 | [전](2026-10-19-native-window-chrome/macos-account-before.png) · [후](2026-10-19-native-window-chrome/macos-account-after.png) · [호버 6배](2026-10-19-native-window-chrome/macos-account-hover-x6.png) |
| 업데이트 창 | [밝은 테마](2026-10-19-native-window-chrome/macos-update-after.png) · [다크 테마](2026-10-19-native-window-chrome/macos-update-after-dark.png) |
| 설정 창 | [변경 후](2026-10-19-native-window-chrome/macos-settings-after.png) · [변경 전 호버 8배](2026-10-19-native-window-chrome/macos-settings-hover-x8.png) |

## 검증 경계

- **Windows 실제 동작은 미검증이다.** 제목 표시줄 색, 제목 글자·아이콘 숨김, 캡션 버튼, 스냅 레이아웃, 드래그·더블클릭, Windows 10 표준 표시를 이 Mac에서 관찰하지 못했다.
  `windows-latest` CI의 스모크가 실행되면 `nativeChrome.*.windowsTitleBar`에 칠하기 결과가 남고, 실패 HRESULT면 진단이 실패한다.
- 연결된 두 화면이 모두 배율 1이라 Retina 배율은 실제로 보지 못했다.
- 제목 막대 더블클릭은 Avalonia 백엔드가 확대·복원으로 처리한다. macOS 「제목 막대 더블클릭」 설정(최소화·없음)은 확인하지 않았다.
- 설치 앱 교체·서명·배포는 하지 않았다.
