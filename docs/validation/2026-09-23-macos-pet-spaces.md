# macOS 전체화면·Space 펫 표시 — 2026-09-23

## 원인과 변경

`Topmost`는 창의 높이만 조절한다. 기존 펫 창에는 다른 Space에 참여하는 설정이 없었고,
Avalonia는 NSWindow를 `FullScreenPrimary`로 생성했다. 공간 참여 옵션만 추가한 첫 실제
검사에서도 Chrome 전체화면에서 펫의 `isOnActiveSpace`와 가시 상태가 모두 false였다.

- `MacPetWindow`에서 펫을 표시하기 전에 `CanJoinAllSpaces`, `FullScreenAuxiliary`,
  macOS 13 이상에서는 `CanJoinAllApplications`를 적용한다. 상충하는 역할 비트를 제거하고
  나머지 창 옵션은 보존한다. 네이티브 NSWindow 핸들만 처리한다.
- `Program.ConfigureRendering`에서 `MacOSPlatformOptions.ShowInDock = false`를 지정한다.
  기존 배포 Info.plist의 `LSUIElement = true`와 런타임 활성화 정책을 일치시킨다.
  따라서 macOS에서는 메뉴바 앱으로 실행되며 Dock/앱 전환기 항목은 표시하지 않는다.
- 펫과 말풍선은 같은 창이므로 함께 표시된다. 설정 창에는 공간 참여 옵션을 적용하지 않는다.
  기존 표시/숨기기와 포커스 정책, 창 높이, 드래그·좌표·렌더링 방식은 유지한다.

공개 API 근거: [Apple의 CanJoinAllApplications](https://developer.apple.com/documentation/appkit/nswindow/collectionbehavior-swift.struct/canjoinallapplications),
[Avalonia 12.1.2 창 초기화](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/native/Avalonia.Native/src/OSX/WindowImpl.mm),
[Avalonia의 앱 시작 옵션 적용](https://github.com/AvaloniaUI/Avalonia/blob/12.1.2/src/Avalonia.Native/AvaloniaNativePlatform.cs).
비트 값과 macOS 13 가용성은 설치된 Apple SDK의 `AppKit/NSWindow.h`와 대조했다.

## 자동 검사

- Release 전체 테스트: **311 통과 / 0 실패 / 0 건너뜀**.
- 새 회귀 검사 5개: 상충하는 Space/전체화면/Stage Manager 역할 제거, 기존 옵션 보존,
  재적용, 구형 OS 분기, headless 핸들에서 Objective-C 호출 및 강제 표시 방지.
- 최종 네이티브 하네스 Release 빌드: 경고 0 / 오류 0.
- `git diff --check` 통과.

로컬 결과: `artifacts/validation/2026-09-23-mac-pet/mac-pet-final.trx`.

## 실제 macOS 창 검사

환경: macOS 26.6.2 (25G83), Apple Silicon. 새 `UNFOLD_DATA_DIR` 사용.
최종 코드의 `Program.ConfigureRendering`, `AppRuntime`, `PetWindow`를 실행하는 임시
`.app` 하네스를 사용했다. 화면 밖 렌더링이나 headless 검사가 아니다.
Chrome의 별도 빈 창을 UI 도구로 전체화면 전환하고, AppKit 창 상태를 읽었다.

| 조건 | 관찰 |
|---|---|
| Chrome 전체화면, Chrome 활성 | 펫의 현재 Space 참여·가시 상태 true, 펫 key window false |
| 전체화면에서 알림 표시 | 말풍선 Invitation 상태, 현재 Space 참여·가시 상태 true |
| 펫 숨기기 | visible·가시 상태 false |
| 전체화면에서 다시 표시 | 현재 Space 참여·가시 상태 true, 키보드 포커스 유지 |
| 일반 창으로 돌아오기 | 펫 표시 유지 |
| 설정 창 | collectionBehavior 128 유지; 펫 전용 262401과 분리 |

네이티브 값: 펫 collectionBehavior 262401 (`0x40101`), level 3, hidesOnDeactivate false,
앱 activationPolicy 1. 펫과 말풍선 콘텐츠는 UI 도구의 창 캡처로도 확인했다.
창 캡처 자체는 데스크톱 전체 합성 캡처가 아니며, 전체화면 참여 판정은 UI 전환과
`isOnActiveSpace`·`occlusionState`·전면 앱 PID 기록을 함께 근거로 삼았다.
요약 데이터는 [macos-pet-spaces.json](2026-09-23-macos-pet-spaces.json)에 있다.

## 검증 한계

Stage Manager 켜기/끄기, 별도 일반 데스크톱 여러 개, 다중 모니터, macOS 13~15·Intel,
배타적 전체화면 게임, 다른 Mac과 Windows 실기는 미검증이다. Windows 네이티브 코드는
변경하지 않았다. 이번 작업은 소스와 로컬 빌드에 적용했으며 배포 압축파일은 재생성하지 않았다.
기존에 실행 중이거나 설치된 앱에는 새 빌드로 재실행해야 적용된다.
