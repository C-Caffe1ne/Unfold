# C#·Avalonia 아키텍처 감사 및 네이티브 창 버튼 구현 계획

작성일: 2026-10-05. 상태: 구현 전 계획. 제품 코드와 기존 문서는 수정하지 않았다.

## 현재 구성과 데이터 흐름

최신 개발본은 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`, `codex/beta-v1.0.4`, HEAD `5d09db9010792ba2c2c8b6440470a43506dc3cff`, 프로젝트 `1.0.4-beta`다. `release/mvp`의 `0.2.2`, `feat/pet-pack-ux`의 `1.0.3-beta`와 구별했다. 현재 실행 검증 대상은 최신 작업 트리의 `src/Unfold.Desktop/bin/Release/net10.0/Unfold.dll`이며 기존 설치 앱은 기준으로 사용하지 않는다.

설치된 Avalonia 버전은 `12.1.2`다. NuGet 메타데이터의 공식 소스 커밋 `d3c867a9e2de379249b03dbeb3495bd7f076a81a`와 동일한 소스를 읽었다.

현재 메인 창(SettingsWindow)은 다음 흐름이다.

- `SettingsWindow.cs:45`: `BorderOnly`, 클라이언트 영역 확장, 제목표시줄 높이 0, 투명 배경.
- `SettingsWindow.Chrome.cs:53`: 높이 24px 바에 앱이 직접 만든 버튼을 표시. Windows 역할 지정은 입력 의미를 부여하며 버튼의 실제 그리기는 앱이 담당한다.
- `SettingsWindow.Layout.cs:73`: 고정 24px 행, 가장자리 31px 여백, 32px 외곽 반경과 클리핑.
- 닫기: `Close()` → `Closing` 취소 → `HideToTray()`; 최소화/최대화는 `WindowState` 변경.
- macOS 상단 드래그는 좌표 기반 자체 구현이며 더블클릭도 앱에서 최대화/복원한다.

직전 커스텀 버튼 변경 2개 파일은 미커밋 상태다. 기존 검증 기록의 해시와 동일함을 확인했다. 다른 기존 작업을 초기화하거나 되돌리지 않는다.

## 구현·문서 정합성

### 가능 여부와 권장 구성

| OS | 실제 네이티브 버튼 | 권장 최초 구현 |
|---|---|---|
| macOS | 가능. AppKit `NSWindow`의 표준 닫기·최소화·확대/전체 화면 버튼 | `WindowDecorations.Full`, `ExtendClientAreaToDecorationsHint=true`, 높이 힌트는 기본값 `-1`. 실제 제목표시줄 높이를 확보해 콘텐츠와 겹치지 않도록 한다. |
| Windows | 가능. OS가 그리는 시스템 제목표시줄과 캡션 버튼 | `WindowDecorations.Full`, `ExtendClientAreaToDecorationsHint=false`. 기본 비클라이언트 영역과 시스템 입력 처리를 사용한다. |

macOS 근거:

- [네이티브 WindowImpl.mm](https://github.com/AvaloniaUI/Avalonia/blob/d3c867a9e2de379249b03dbeb3495bd7f076a81a/native/Avalonia.Native/src/OSX/WindowImpl.mm#L380)은 확장 시 제목 텍스트를 숨기고 제목표시줄 배경을 투명하게 처리한다.
- [같은 파일의 UpdateAppearance](https://github.com/AvaloniaUI/Avalonia/blob/d3c867a9e2de379249b03dbeb3495bd7f076a81a/native/Avalonia.Native/src/OSX/WindowImpl.mm#L595)는 `standardWindowButton`으로 실제 AppKit 버튼 3개를 가져와 `Full`일 때 표시한다.
- [C# 네이티브 백엔드](https://github.com/AvaloniaUI/Avalonia/blob/d3c867a9e2de379249b03dbeb3495bd7f076a81a/src/Avalonia.Native/WindowImpl.cs#L186)는 실제 제목표시줄 높이를 반환하며 `NeedsManagedDecorations=false`다.

Windows의 중요한 구분:

- [공식 안내](https://docs.avaloniaui.net/docs/platform-specific-guides/windows#custom-title-bars)는 `Full`과 확장 영역을 설명하지만, 설치 버전 소스까지 확인해야 실제 그리기 주체를 구분할 수 있다.
- [12.1.2 Win32 구현](https://github.com/AvaloniaUI/Avalonia/blob/d3c867a9e2de379249b03dbeb3495bd7f076a81a/src/Windows/Avalonia.Win32/WindowImpl.cs#L1628)은 확장 모드에서 `NeedsManagedDecorations=true`, `RequestedDrawnDecorations=TitleBar`다. `Full + true`만으로 변경하면 Avalonia가 그리는 제목표시줄 경로가 포함되므로 이번의 실제 OS 버튼 요구를 만족한다고 판정하지 않는다.
- 같은 구현은 `Full`에서 `WS_CAPTION`과 `WS_SYSMENU`를 사용한다. 확장을 끄면 기본 OS 프레임 경로가 된다.
- [Microsoft DWM 안내](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe)에 따르면 커스텀 프레임과 시스템 캡션 버튼의 통합도 가능하다. 다만 Avalonia의 입력 처리와 그리기 경로를 함께 다뤄야 하므로, Windows에서 현재처럼 제목표시줄 없이 완전히 통합된 모양까지 요구할 경우 별도 Win32 검증 작업으로 분리한다. 최초 구현의 기본 범위에 넣지 않는다.

Avalonia 11의 `ExtendClientAreaChromeHints` 예제를 가져오지 않는다. 해당 API는 [Avalonia 12에서 제거](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes#window-decoration-changes)됐다. 최초 구현에는 패키지 변경이나 직접 AppKit/Win32 호출을 추가할 필요가 없다.

## 위험과 기술 부채

1. **외형 변경:** Windows 기본 제목표시줄 높이·제목·색상·버튼 위치와 OS 외곽이 생긴다. macOS 버튼의 크기·간격·호버도 해당 OS가 결정한다. 현재 앱의 상단 24px와 외곽 반경 32px를 동시에 고정하는 것을 수용 기준으로 삼지 않는다. 내부 카드·글꼴·색상 테마는 보존한다.
2. **여백:** macOS 확장 모드에서는 `WindowDecorationMargin` 등 실제 보고된 상단 영역을 반영한다. Windows 비확장 모드에서는 제목표시줄이 클라이언트 영역 밖에 있으므로 앱에서 그 높이를 다시 더하지 않는다.
3. **외곽 투명도:** 메인 창의 픽셀 투명도와 자체 외곽 클리핑은 네이티브 프레임 아래 빈 모서리/이중 경계를 만들 수 있다. 메인 창의 외곽은 OS에 맡기고 앱 배경을 가장자리까지 채우는 방향으로 조정한다. 펫 창의 투명도는 별도 계약이므로 건드리지 않는다.
4. **전체 화면:** macOS 초록 버튼을 앱의 최대화 토글에 재연결하지 않는다. AppKit 동작을 유지하고 전체 화면 진입·해제와 복원 후 화면 배치를 검사한다.
5. **닫기:** 네이티브 닫기도 기존 `Closing`에 도달해야 한다. 창 숨김 뒤 타이머·펫 유지, 트레이 재열기, 별도 앱 종료 동작을 검증한다.
6. **기존 검사 전제:** `BorderOnly`, 커스텀 버튼 이름 3개, `y=31`, 투명한 32px 외곽을 강제하는 검사와 통합 진단은 새 계약으로 바꿔야 한다. 단순히 검사를 삭제하지 않는다.

## 작업 분할과 파일 소유권

| 순서 | 작업 | 주 소유 파일 | 완료 기준 |
|---|---|---|---|
| 1 | OS별 네이티브 구성의 작은 검증 창으로 실제 버튼과 그리기 주체 확인 | 제품 코드 밖 임시 검증 도구 | macOS 표준 NSWindow 버튼, Windows 비확장 OS 제목표시줄. 커스텀/Avalonia 대체 버튼 중복 없음 |
| 2 | 메인 창의 OS별 장식 설정, 자체 창 버튼과 자체 드래그·더블클릭 제거 | `SettingsWindow.cs`, `SettingsWindow.Chrome.cs` | OS 기본 버튼·이동 동작 사용. 불필요해진 이동 상태와 `EndWindowMove()` 호출도 함께 정리 |
| 3 | 상단 여백·배경·외곽 조정, 최대화/전체 화면 복원 처리 | `SettingsWindow.Layout.cs`, 필요한 범위의 `SettingsWindow.cs` | 기본 1120×800과 최소 640×560에서 겹침·잘림·중복 여백 없음. 실제 클라이언트 영역 기준으로 검증 |
| 4 | 기능 계약에 맞춰 집중 검사와 네이티브 진단 수정 | `WindowControlTests.cs`, `SmokeDiagnostics.cs` | 트레이 숨김/재열기, 최소화/복원, 상태별 레이아웃 검증. 앱 버튼 이름 조회에 의존하지 않음 |
| 5 | 두 OS 실제 실행·호버·입력 검증과 캡처 | 새 검증 보고서, 필요한 경우 기존 Windows CI 진단 보완 | OS별 기본/호버/최대화 또는 전체 화면/복원 캡처와 입력 결과 확보 |

대상은 사용자가 지적한 메인 창이다. 계정 창·확인 대화상자·펫 창의 장식은 일괄 변경하지 않는다. 전역 `DesignSystem` 변경도 최초 범위에 포함하지 않는다.

## 의존성·수용 기준

1단계의 두 OS 네이티브 경로 확인 후 2→3→4→5 순서로 진행한다. macOS 확장 영역과 네이티브 버튼의 공존에서 문제가 생기면 기본 비확장 제목표시줄을 기준 동작으로 비교한다. 커스텀 신호등으로 자동 대체하지 않는다.

- macOS: 기본/호버/비활성 상태의 실제 버튼 표시, 닫기·최소화·전체 화면·해제, 상단 드래그의 실제 좌표 이동, 시스템 설정에 따른 더블클릭, Retina 화면과 최소 크기.
- Windows: 실제 제목표시줄의 최소화·최대화·복원·닫기, Windows 11 최대화 호버 스냅 메뉴(지원/활성화된 환경), 상단 드래그·테두리 크기 조절, 100/125/150/200% DPI, 최대화 시 작업 표시줄을 침범하지 않는지 확인.
- 공통: 닫기 후 타이머 상태 유지, 재열기, 앱 종료, 네 테마, 페이지 탐색과 최소 크기 잘림. 버튼 모양·간격은 시스템이 관리한다.
- 자동 검사: 변경 전 관련 검사 → 변경 후 창 제어/테마/반응형 집중 검사 → Release 빌드 → 새 `UNFOLD_DATA_DIR`의 네이티브 진단. 프레임 변경 영향에 맞춰 전체 회귀 검사 여부를 판단한다.
- 기존 `.github/workflows/desktop.yml`은 Windows/macOS 매트릭스와 Windows 패키지 진단을 제공한다. 빌드/자동 진단과 실제 포인터 호버·스냅 검증은 구별해 기록한다.
- 캡처는 반드시 실제 OS 창에서 얻는다. Avalonia `RenderTargetBitmap`만으로는 OS 비클라이언트 버튼의 표시를 증명할 수 없다. Mac에서 Windows 스타일을 표시한 화면을 Windows 실기 증거로 사용하지 않는다.

## 미검증 항목

이번 단계는 현재 코드와 설치된 Avalonia 버전의 공식 구현을 읽어 가능 여부와 계획을 확정한 것이다. 제품 코드를 수정하거나 새 네이티브 창을 실행하지 않았으며, 테스트도 재실행하지 않았다. Unfold의 실제 투명도/테마/레이아웃과 조합한 결과 및 Windows 실기 동작은 구현 단계에서 확인한다. 직전 커스텀 버튼 검사 29개 통과 결과는 새 네이티브 구현의 검증 근거로 재사용하지 않는다. 설치 앱 교체·서명·공증·게시 작업은 이 계획에 포함하지 않는다.
