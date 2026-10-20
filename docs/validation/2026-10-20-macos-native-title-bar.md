# macOS 신호등 네이티브 배치 검증

2026-10-20 · `codex/pet-overlay-hover`, 시작 HEAD `3541e87`, 프로젝트 `1.2.0`. 앞선 작업: [일반 창 네이티브 크롬](2026-10-19-native-window-chrome.md).

## 원인

macOS 26.6.2에서 현재 SDK로 빌드한 AppKit 창과 Unfold 창의 표준 버튼을 측정해 비교했다([AppKit 기준값](2026-10-20-macos-native-title-bar/appkit-reference.json)).

| 창 | 제목 영역 | 버튼 크기 | 닫기·최소화·확대 x | y | 모서리 |
|---|---:|---:|---|---:|---:|
| AppKit 기본·투명 제목 표시줄 | 32 | 14×14 | 9 · 32 · 55 | 9 | 16 |
| AppKit 통합 툴바(참고) | 66 | 14×14 | 19 · 42 · 65 | 19 | 26 |
| 변경 전 Unfold(설정·계정·업데이트) | 28 | 14×16 | 7 · 27 · 47 | 6 | — |

Unfold는 버튼을 직접 그리지 않는다. AppKit은 **실행 파일이 선언한 SDK**에 맞춰 제목 표시줄을 배치한다. .NET apphost와 `dotnet` 호스트는 SDK 15.5를 선언하므로, macOS 26이 이전 배치(28pt·간격 20)를 그렸다. Avalonia 네이티브 라이브러리(`libAvaloniaNative.dylib`)는 이미 SDK 26.0으로 빌드돼 있다.

## 변경

- `Scripts/declare-macos-appkit-sdk.sh`: apphost의 `LC_BUILD_VERSION` SDK를 26.0으로 바꾸고 최소 OS(12.0)는 유지한 뒤 임시 서명한다. 이미 26 이상이면 아무것도 하지 않는다.
- `Unfold.Desktop.csproj`: macOS에서 apphost를 만든 직후 위 스크립트를 실행한다. 개발 빌드, `dotnet run`, osx-arm64·osx-x64 게시와 번들이 모두 같은 apphost를 쓴다. 번들 서명은 기존 서명 스크립트가 다시 한다.
- 버튼 좌표를 앱이 옮기거나 버튼을 흉내 내지 않는다. 콘텐츠 여백은 실제 `WindowDecorationMargin`(32)을 따른다.

## 검증

새 `UNFOLD_DATA_DIR`, .NET SDK 10.0.401, Xcode 27.0.

| 검사 | 결과 |
|---|---|
| 새 apphost SDK 검사 | 구현 전 `SDK 15.5`로 실패, 구현 후 통과 |
| 빌드·게시 산출물 | `obj` apphost, `bin/Unfold`, 테스트 출력, osx-arm64·osx-x64 게시본 모두 SDK 26.0·최소 OS 12.0·서명 유효 |
| `dotnet build Unfold.slnx -c Release --no-restore` | 경고·오류 0 |
| 전체 `dotnet test Unfold.slnx -c Release --no-build` | **1,107개 통과**, 실패·건너뜀 0 |
| 제품 apphost `bin/Release/net10.0/Unfold --smoke-test` | `success: true`, 약 60.1초. 설정·업데이트·계정 창 `titleBarInset: 32` ([smoke.json](2026-10-20-macos-native-title-bar/macos-smoke-apphost.json)) |

실제 화면은 제품 밖 임시 검증 앱(같은 소스, 같은 스크립트로 SDK 26 선언)에 CGEvent 포인터 입력을 보내 확인했다. 화면은 1920×1080, 배율 1이다.

- 설정·계정·업데이트 창 버튼: 14×14, x=9·32·55, y=9, 제목 영역 32 — AppKit 기준값과 같다.
- 설정 창: 호버 기호, 노랑 최소화·복원, 제목 영역 드래그 이동, 더블클릭 확대·복원, 초록 전체 화면·해제(상단 호버 시 신호등 표시), 빨강 트레이 숨김·재표시.
- 계정 창(구매 필요): 빨강 → 종료 확인 요청 1회·창 유지, 머리글 드래그 이동.
- SDK 15.5와 26 호스트를 같은 진단 런타임으로 비교했다. 펫 창의 스타일·그림자·불투명도·레벨·마우스 통과·Spaces 동작·알파 값이 같고, 펫과 오류 모달 캡처는 픽셀 단위로 동일했다.

| 화면 | 캡처 |
|---|---|
| 설정 창 | [변경 전(SDK 15.5)](2026-10-20-macos-native-title-bar/before-sdk15-settings.png) · [변경 후](2026-10-20-macos-native-title-bar/macos-settings.png) · [호버 6배](2026-10-20-macos-native-title-bar/macos-settings-hover-x6.png) |
| 계정 창 · 전체 화면 | [계정 창](2026-10-20-macos-native-title-bar/macos-account.png) · [전체 화면 상단](2026-10-20-macos-native-title-bar/macos-fullscreen-reveal.png) |

## 검증 경계

- `dotnet Unfold.dll`처럼 `dotnet` 호스트로 실행하면 이전 배치가 유지된다. 배포 번들과 `dotnet run`은 apphost를 쓴다.
- 서명·공증된 배포 번들은 만들지 않았다. 게시 산출물의 apphost만 확인했다.
- macOS 13~15 실기, Retina 배율, Intel Mac은 보지 못했다. 이전 OS는 각자의 기본 배치를 쓸 것으로 보지만 확인하지 않았다.
- 트레이 메뉴 등 다른 AppKit 요소도 macOS 26 SDK 기준으로 그려지며, 이 부분은 따로 비교하지 않았다.
- 첫 입력 검사 중 설정 창 폭이 한 번 860으로 바뀌었으나 같은 순서로 4회 다시 시도해 재현되지 않았다.
