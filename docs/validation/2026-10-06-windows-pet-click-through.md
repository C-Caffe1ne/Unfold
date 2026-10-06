# Windows 펫 클릭 통과 수정 — 2026-10-06

## 원인

사용자는 macOS에서는 투명 영역의 클릭이 통과하지만 Windows에서는 통과하지 않는다고 보고했다.
최신 개발본의 `PetWindow.Input.cs`는 Windows HWND에 `WS_EX_TRANSPARENT`만 설정했다.
Microsoft가 설명하는 다른 앱으로의 마우스 전달은 layered window와 이 플래그의 조합이다.
현재 사용하는 Avalonia 12.1.2의 `WindowImpl`은 기본 합성 경로에서 `WS_EX_NOREDIRECTIONBITMAP`을 사용하며, 시각적으로 투명한 창이라는 사실이 `WS_EX_LAYERED` 설정을 뜻하지 않는다.

추가로 네이티브 호출 성공을 확인하지 않고 내부 `clickThrough`를 변경했고, 다음 요청 값이 같으면 실제 HWND를 확인하지 않았다.
Avalonia가 확장 스타일을 다시 만들면 내부 상태와 실제 입력 상태가 어긋난 채 유지될 수 있었다.
이는 현재 코드와 API 계약에서 확인한 결함이다. 사용자 Windows 환경에서의 수정 전·후 실행 재현은 수행하지 못했다.

근거: [Microsoft Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows),
[SetWindowLongPtrW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw),
[SetLayeredWindowAttributes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setlayeredwindowattributes),
[Avalonia 12.1.2 WindowImpl](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Windows/Avalonia.Win32/WindowImpl.cs).

## 기준과 변경

- 최신 경로: `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`.
- 브랜치 `codex/glb-import-compat`, HEAD `671729ad18ed8dafeae0510375f630e264d67f13`, 프로젝트 버전 `1.1.0-beta`.
- 작업 시작 시 4개 worktree의 브랜치·HEAD·미커밋 변경·프로젝트 버전을 비교했다. 현재 대화 기본 경로의 `release/mvp` (`0.2.2`)는 수정하지 않았다.
- 현재 GLB 호환성 수정이 있는 개발본에 추가했다. 기존 변경 14개 파일의 SHA-256이 작업 전과 동일하다.
- Windows 전용 `WindowsPetWindow`를 분리하고 `WS_EX_LAYERED | WS_EX_TRANSPARENT`를 적용한다. 앱이 새로 추가한 layer만 alpha 255로 초기화한다.
- 기존 layered 렌더러의 속성을 재초기화하지 않는다. 다른 확장 스타일, GPU 합성 설정, 위치·크기·Z 순서·활성 창을 변경하지 않는다.
- 네이티브 반환값과 적용된 스타일을 확인한다. 실패한 전환은 이전 스타일로 복구를 시도하고 성공으로 기록하지 않으며 다음 poll에서 재시도한다.
- Windows에서는 이전 판정과 같아도 HWND 상태를 읽어 스타일 초기화 후 복구한다. 정상적으로 유지되는 상태에서는 스타일 쓰기·프레임 갱신을 반복하지 않는다.
- 보이는 펫 픽셀, 누름 유지·드래그·입력 캡처·우클릭 메뉴·휴식 알림 버튼은 기존 입력 계약을 유지한다. 정보 표시용 호버 말풍선과 투명 영역은 통과 대상이다.
- macOS 네이티브 입력 구현과 GLB 로더·렌더러의 기존 수정은 변경하지 않았다.

## 소유 파일

- `src/Unfold.Desktop/PetWindow.Input.cs`
- `src/Unfold.Desktop/WindowsPetWindow.cs`
- `Tests/Unfold.Tests/WindowsPetWindowTests.cs`
- `Tests/Unfold.Tests/PetClickThroughTests.cs` — 열린 메뉴의 입력 유지 회귀 검사.
- `Tests/Unfold.Tests/PetHoverTests.cs` — 이동한 HWND 판정 헬퍼 참조 갱신.
- 이 기록 및 같은 이름의 근거 디렉터리. 기존 GLB 문서 변경은 그대로 보존했다.

## 검증

| 구분 | 결과 |
|---|---|
| 수정 전 클릭 통과·호버 검사 | 36/36 통과, 종료 코드 0 |
| 수정 후 Windows 네이티브 경계·픽셀·호버·기본 펫 집중 검사 | 92/92 통과, 종료 코드 0 |
| 최종 전체 Release 회귀 검사 | 706/706 통과, 실패·건너뜀 0, 종료 코드 0 |
| macOS 네이티브 백엔드 격리 smoke | `success: true`, 종료 코드 0, 캡처 107개 |
| Windows x64 self-contained / ReadyToRun 교차 빌드 | 성공, 종료 코드 0, PE32+ x86-64 실행 파일 확인 |
| 테스트용 ZIP | CRC 검사 통과, 107,932,590 bytes |
| 정적 검사 | `git diff --check` 통과 |
| 기존 변경 보존 | 시작 시 기존 변경 14개 파일 SHA-256 일치 |
| 실제 Windows / 물리 입력 / GPU 표시 | 실행 호스트가 없어 미검증 |

새 검사 19건은 레이어 추가·기존 레이어 보존·실제 스타일 재조회·외부 스타일 초기화 복구·읽기/쓰기/레이어 초기화/갱신/적용 확인 실패와 재시도·메뉴 입력 유지 등을 다룬다.
Windows API 경계 검사는 fake API로 상태 전환을 검증한다. Win32 실행 성공이나 다른 프로세스로의 실제 클릭 전달 증거가 아니다.
최초 집중 검사 빌드는 옮긴 `HasWindowsHandle`의 기존 테스트 참조 3곳에서 실패했다. 해당 참조를 새 헬퍼로 갱신한 뒤 집중·전체 검사가 통과했다.

실행한 핵심 명령:

```sh
UNFOLD_DATA_DIR=<새 임시 경로> dotnet test Unfold.slnx -c Release --no-restore
UNFOLD_DATA_DIR=<새 임시 경로> dotnet src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll --smoke-test
dotnet publish src/Unfold.Desktop/Unfold.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishSingleFile=false -p:RestoreLockedMode=true -o <별도 임시 경로>
```

macOS smoke 프로필은 `/private/tmp/unfold-win-input-mac-smoke.d6vtkt`다.
이 smoke는 화면 밖 창·프로그래밍된 입력 이벤트를 사용하며 실제 포인터에 따른 클릭 통과는 진단 모드에서 실행하지 않는다.
사용 중인 앱 데이터와 실행 중인 앱은 교체하지 않았다.

[전체 TRX](2026-10-06-windows-pet-click-through/full.trx),
[집중 TRX](2026-10-06-windows-pet-click-through/focused.trx),
[기존 기준 TRX](2026-10-06-windows-pet-click-through/baseline.trx),
[macOS smoke 결과](2026-10-06-windows-pet-click-through/mac-smoke.json),
[빌드·해시](2026-10-06-windows-pet-click-through/verification.json).

## Windows 실기 확인과 배포 경계

테스트용 파일은 `artifacts/Unfold-Windows-x64-clickthrough-preview-20261006.zip`이다.
SHA-256: `d13fa02ee2594e56cb5736f57020764c7420c7b62dda3e6f285a8378c8940c4e`.
현재 GLB 호환성 수정도 포함하며 소스 파일은 포함하지 않는다. 서명되지 않은 개발 빌드다.

기존 Windows Unfold를 트레이에서 종료한 뒤 ZIP 전체를 풀고 `Unfold.exe`를 실행한다.
실기 진단에는 포함된 `README-preview.txt`의 새 `UNFOLD_DATA_DIR` 명령을 사용한다.

1. 메모장·브라우저 등 다른 프로세스 위에 2D/GLB 펫을 각각 놓고 투명 여백·내부 구멍에서 클릭과 스크롤이 뒤쪽 앱에 전달되는지 확인한다.
2. 보이는 몸체 클릭·누름 유지·드래그·우클릭 메뉴·휴식 알림 버튼의 입력을 확인한다.
3. 커서를 멈춘 상태의 프레임 전환, 숨김/표시, 펫·크기 변경 후에도 반복한다.
4. GPU 합성 경로에서 배경이 검어지거나 펫이 사라지거나 깜박이지 않는지 확인한다. DPI 100/150/200%, 다중 모니터는 각각 확인한다.

Windows 실기 통과 전까지 사용자 환경의 해결 완료로 판정하지 않는다.
16ms UI 타이머 방식은 유지되므로 UI 스레드가 지연되면 상태 전환도 지연될 수 있다.
이번 수정은 로컬 미커밋 상태다. 새 버전 지정, GitHub 업로드, 소스 push, 공개 설치 파일 교체, 웹 변경은 수행하지 않았다.
