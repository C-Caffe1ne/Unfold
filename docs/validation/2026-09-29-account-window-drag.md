# 최초 실행 계정 창 드래그 수정·재검증

2026-09-29 · macOS · 현재 작업 트리 · C#/.NET 10/Avalonia 12.1.2

## 앞선 수정의 한계

첫 수정은 `AccountDragHandle` Grid에 투명 배경을 넣어 빈 공간의 hit-test를 보완했다.
관련 45개 검사는 입력 전달과 버튼 동작만 확인했으며 실제 창 좌표 이동을 검사하지 않았다.
따라서 그 결과로 창 이동 해결을 선언한 것은 검증 범위를 넘는 판단이었다.

## 추가 확인과 변경

- 기존 드래그 영역은 창 안쪽의 높이 30px 헤더뿐이었다. 맨 위 바깥 여백과 헤더 위 패딩은
  이벤트를 받지 못했다. 새 투명 입력 영역은 화면 맨 위부터 90px까지 포함한다.
  기존 카드·버튼·글자 배치는 바꾸지 않았다.
- macOS에서 CUA의 드래그를 전달했을 때 기존 `BeginMoveDrag` 호출 경로에서 좌표가 움직이지 않았다.
  같은 진단 세션의 이후 별도 입력에서는 좁은 헤더를 통한 이동이 기록됐다.
  따라서 이를 모든 macOS 입력에서 발생하는 AppKit 결함으로 단정하지 않는다.
- macOS 계정 창은 기존 펫 창처럼 포인터 누름 위치와 창 시작 위치를 저장해
  데스크톱 좌표 차이로 이동한다. Windows는 기존 네이티브 이동 처리를 유지한다.
- 네이티브 진단에서 누름과 해제 사이의 이동 이벤트에 held-button flag가 없는 것을 관찰했다.
  시작 시 왼쪽 버튼을 확인하고, 이후에는 캡처한 포인터의 release·capture loss·deactivate·close로
  이동을 끝낸다. 이동 이벤트의 버튼 플래그만 보고 중간에 드래그를 취소하지 않는다.

Avalonia의 기본 macOS 구현은 [WindowImplBase.cs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.Native/WindowImplBase.cs)에서
네이티브 이동을 요청하고, [WindowBaseImpl.mm](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/native/Avalonia.Native/src/OSX/WindowBaseImpl.mm)에서
마지막 마우스 누름 이벤트로 AppKit 창 이동을 호출한다. 외부 라이브러리는 변경하지 않았다.

## 자동 검사

매 실행 새 임시 `UNFOLD_DATA_DIR`을 사용했다.

| 검사 | 결과 |
|---|---|
| `Account` 및 `ConfirmationActionTests`, Release | 46건 통과, 실패·건너뜀 0, 종료 코드 0 |
| 진단 앱 Release 빌드 | 경고 0, 오류 0 |
| `git diff --check` | 통과 |

추가 회귀 검사는 맨 위 5px의 입력, 실제 `Window.Position` 변경, held-button flag 없는 이동,
해제 후 정지, 오른쪽 버튼 거부, 캡처 해제 후 정지를 확인한다.
기본·최소 크기와 로그인·구매 화면의 입력 영역, 로그인·종료 버튼 비간섭도 검사한다.
개발 중 발견한 nullable 컴파일 오류와 테스트의 포인터 캡처 관찰 방식을 보완한 뒤 위 결과를 얻었다.

## 실제 macOS 검증

제품 `AccountWindow`를 생성하는 별도 진단 실행 파일에서 좌표·입력을 기록하고 CUA로 조작했다.
새 프로필을 사용했으며 제품 실행본과 진단 앱의 `Unfold.dll` SHA-256 일치를 확인했다.

| 입력 | 크기 | 시작 좌표 | 종료 좌표 | 결과 |
|---|---|---|---|---|
| 맨 위 5px에서 오른쪽 100, 아래 60으로 끌기 | 940×620 | (490,245) | (590,305) | 통과 |
| 헤더에서 오른쪽 80, 아래 40으로 끌기 | 940×620 | (590,305) | (670,345) | 통과 |
| 맨 위 5px에서 왼쪽 90, 아래 50으로 끌기 | 640×560 | (670,345) | (580,395) | 통과 |

실행 환경의 DesktopScaling·RenderScaling은 모두 1이다.
[좌표·입력 증거](2026-09-29-account-window-drag-native.json)에 수정 전후 측정 범위와 해시를 보관했다.

## 미검증

실제 Windows 창 이동, 다중 모니터·DPI 변경, 장시간 사람 입력은 미검증이다.
이번 작업에서는 로그인·결제 서버를 호출하지 않았으며 결제 웹훅 수정·배포·재처리는 수행하지 않았다.

## 변경 파일

- `src/Unfold.Desktop/AccountWindow.axaml`
- `src/Unfold.Desktop/AccountWindow.axaml.cs`
- `Tests/Unfold.Tests/AccountWindowDragTests.cs`
- `docs/account-screen.md`
- 이 검증 기록과 좌표 증거 JSON

기존 계정·종료 화면 변경은 유지했다. 진단 실행 파일은 `artifacts/verification/account-drag/`에만 있다.
