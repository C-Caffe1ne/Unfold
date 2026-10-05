# 펫 투명 영역 클릭 통과 — 2026-10-05

## 원인

투명 영역을 확인하는 타이머가 Windows 네이티브 창만 처리했다.
macOS는 투명 영역에서 펫 반응을 시작하지 않더라도 뒤쪽 앱으로 입력을 넘기는 연결이 없었다.
기존 픽셀 검사는 가장자리 주변까지 입력 영역으로 넓혀, 실제 투명한 픽셀도 클릭 대상으로 판정했다.

작업 기준은 `/Users/hwanghyeonseong/.codex/worktrees/6bca/Unfold`, `codex/beta-v1.0.4`,
HEAD `717a63b`, `1.0.4-beta`다. 다른 체크아웃의 브랜치·HEAD·미커밋 상태와
프로젝트 버전(`0.2.2`, `1.0.3-beta`)을 확인한 뒤 최신 개발본에서 작업했다.
실행·검증 대상은 해당 경로의 Release `Unfold.dll`이다.

## 변경

- Windows와 macOS가 현재 표시 중인 프레임의 픽셀을 같은 기준으로 판정한다.
- 클릭에는 가장자리 확장 판정을 사용하지 않는다. 투명 여백과 캐릭터 내부 구멍은 통과한다.
- 기존 알파 기준(26/255 이상)은 유지하며, hover의 가장자리 허용과 안정된 말풍선 배치도 유지한다.
- macOS에서 실제 NSWindow의 `ignoresMouseEvents`를 전환한다. 로컬 포인터 좌표를 사용해 화면 원점·화면 배율 변환을 중복하지 않는다.
- 입력 확인 간격을 40ms에서 16ms로 줄이고, 수신한 이동 이벤트에서도 갱신한다.
- 누른 상태·드래그·펫 내부 입력 캡처·열린 메뉴는 입력을 유지한다. 캡처가 다른 창으로 이동하면 투명 영역을 다시 통과시킨다.
- 휴식 말풍선 버튼은 클릭할 수 있다. 정보를 표시하는 hover 말풍선은 클릭을 통과시킨다.
- Headless 창과 화면 밖 진단에는 실제 데스크톱 커서를 적용하지 않는다.
- 앞서 변경한 GLB 메모리 최적화, 통합 편집기, 캔버스 변형 제거를 보존했다. 제품 UI 설명은 추가하지 않았다.

## 소유 파일

런타임: `PetWindow.Input.cs`, `PetWindow.cs`, `MacPetWindow.cs`, `AnimationView.cs`.
검사: `PetClickThroughTests.cs`, `PetHoverTests.cs`, `OriginalCompanionTests.cs`.
문서: `README.md`, `docs/README.md`, `docs/cross-platform.md`, `docs/verification.md`, 이 기록과 첨부 근거.
기존 변경 중 이번 소유 범위 밖의 파일은 시작 시점 SHA-256과 일치한다.

## 검증

| 검사 | 결과 |
|---|---|
| Release 빌드 | 오류·경고 0 |
| 픽셀·호버·기본 펫·커스텀·GLB 집중 검사 | 89 통과 |
| 최종 입력·캡처 전환 집중 검사 | 32 통과 |
| 최종 전체 자동 검사 | 642 통과 / 실패·건너뜀 0 |
| macOS 실제 NSWindow 클릭 대상 조회 | 2D 3건 + Kazusa GLB 3건 모두 통과 |
| 네이티브 포인터 좌표 변환 | NSEvent 화면 좌표와 일치 |
| 포인터 polling 연결 | 앱의 클릭 통과 상태와 NSWindow 속성 일치 |
| 정적 검사 | git diff --check 통과 |

자동 검사는 투명 여백·내부 구멍·프레임 교체·50/150% 크기·좌우 반전·드래그·숨김·말풍선·타 창 캡처 이동을 포함한다.
기존 검사 중 투명 가장자리 허용에 의존하던 클릭 좌표는 실제 보이는 픽셀로 수정했다.

네이티브 검증은 새 프로필 `/private/tmp/unfold-clickthrough-native-verified`의 임시 앱에서 수행했다.
그 앱이 사용한 `Unfold.dll`은 최종 개발본 DLL과 SHA-256이 같다.
AppKit `windowNumberAtPoint:belowWindowWithWindowNumber:`로 OS의 클릭 대상 창을 조회했다.
투명한 샘플은 실제 뒤쪽 창 ID, 보이는 샘플은 펫 창 ID와 일치했다.
초기 진단은 뒤쪽에 특정 검사 창이 있다고 가정했지만 다른 앱이 가릴 수 있어,
최종 진단에서는 실제 바로 아래 창을 조회해 비교했다.

CUA의 창 지정 클릭으로 증가한 카운터는 OS의 창 간 입력 전달 근거로 사용하지 않았다.
최종 근거는 네이티브 창 선택 조회와 속성 확인이며, 물리 마우스 클릭 전달과 구분한다.

[원시 결과·해시](2026-10-05-pet-click-through/verification.json),
[네이티브 창 선택 결과](2026-10-05-pet-click-through/native-routing.json),
[좌표·polling 결과](2026-10-05-pet-click-through/native-pointer.json),
[전체 검사 로그](2026-10-05-pet-click-through/full-tests.log),
[임시 네이티브 검증 코드](2026-10-05-pet-click-through/NativeInputProbe.cs).

네이티브 API 계약은 Apple의 [ignoresMouseEvents](https://developer.apple.com/documentation/appkit/nswindow/ignoresmouseevents),
[mouseLocationOutsideOfEventStream](https://developer.apple.com/documentation/appkit/nswindow/mouselocationoutsideofeventstream),
[windowNumber(at:belowWindowWithWindowNumber:)](https://developer.apple.com/documentation/appkit/nswindow/windownumber(at:belowwindowwithwindownumber:))와
Microsoft의 [Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)를 확인했다.

## 미검증·위험

- 실제 물리 마우스 클릭·드래그의 전체 흐름과 Windows 실기 실행은 미검증이다.
- 클릭 통과 전환은 UI 스레드의 16ms polling을 사용하므로 UI 스레드가 지연되면 갱신도 지연될 수 있다.
- macOS 네이티브 판정은 현재 디스플레이에서 확인했다. 다중 디스플레이 간 이동과 Intel Mac은 별도 실기 확인이 필요하다.
- 커밋·배포·설치본 교체는 수행하지 않았다. 최신 개발본으로 실행해야 반영된다.
