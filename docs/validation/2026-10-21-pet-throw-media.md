# 던지기·배경 지우기·행동별 여러 파일 검증

2026-10-21 · `codex/pet-throw-media`, 시작 `9d01b7d` (`codex/pet-overlay-hover`, 프로젝트 `1.2.1`). 계획: [적용 계획](../plans/2026-10-21-pet-throw-media.md).

## 변경

- **던지기·착지** (`0627014`): 드래그 스프링 추종, 빠른 놓기의 비행·반사·바닥 마찰·착지 위치 저장. 느린 놓기는 기존처럼 그 자리에 둔다. 동작 줄이기·진단 모드는 기존 직접 이동.
- **배경 지우기·발 위치 맞춤** (`fc9fb4b`): 행동 카드의 체크박스와 허용 범위. CbCr 색차·가장자리 색 번짐 제거, 정사각형 캔버스 발 정렬, FFmpeg GIF 재인코딩 후 프레임별 지연 재기록.
- **행동별 여러 파일**: `AnimationDefinition.variants`·`order` 선택 필드, 팩 검증·감사·예산, 실행 시 무작위(직전 반복 없음)·차례대로 선택, 새 팩·설치 펫 편집 내보내기, 편집 화면의 추가 파일 목록·재생 순서. 배경 정리는 같은 행동의 모든 파일에 적용한다.

## 검증

.NET SDK 10.0.401, Release 빌드 경고·오류 0, `git diff --check` 통과.

- 새 검사: 던지기 12, 미디어 정리 13(실제 FFmpeg 2 포함), 편집 화면 정리 1, 여러 파일 계약 9, 초안 4, 편집 화면·런타임 2.
- 결함 주입 확인: 던지기 경로를 막으면 던지기 검사 3개, 디스플레이 보정 없이 1개가 실패했다.
- 전체 `dotnet test Unfold.slnx`: **1,236 통과**, 실패 1. 실패한 `PurchaseGateTests.LockedQuitDraftGuardUsesAccountModalAndCannotRevealHome`은 시작 커밋 `9d01b7d`에서도 같은 이유로 실패했다(펫 관리가 '내 펫' 목록으로 열림). 다른 검사처럼 `PetLibraryTestNavigation.OpenBuilder`를 거치도록 고친 뒤 해당 클래스 11개 통과.
- 기존 검사 조정: 드래그 직후 위치를 바로 확인하던 호버 검사는 스프링이 자리 잡은 뒤 확인한다. 파일 배정 전후 카드 크기 검사는 스크롤바가 생기지 않도록 창 높이를 960으로 둔다.

## 실제 macOS 진단

macOS `--smoke-test`는 시작 커밋 `9d01b7d`에서도 설정 레이아웃 단계(펫 관리가 만들기 화면을 바로 연다는 이전 단정)에서 실패했다. [진단 수정](2026-10-21-smoke-pet-library.md) `claude/smoke-pet-library-builder`를 2026-10-22에 병합(`72c2686`)한 뒤 다시 실행했다.

- macOS arm64, Release, 새 빈 `UNFOLD_DATA_DIR`: 종료 0, `success: true`, 69초, PNG 115장. `petManagementLibraryFirstVerified`·`petManagementBuilderVerified`·`customPetGifAuthoringVerified` 참.
- `custom-pet-editor.png`에서 행동 카드의 '다른 파일 추가'(파일 1개라 재생 순서 숨김)와 '배경 지우기'·'발 위치 맞춤'이 카드 안에 잘리지 않고 그려진 것을 확인했다.
- 병합으로 바뀐 코드는 `SmokeDiagnostics.cs`뿐이라 이를 참조하는 `LocalizationTests`·`PurchaseGateTests` 28개만 다시 실행해 통과했다.

## 타이머 바 정리 (2026-10-22)

- 호버 바의 `⋯` 빠른 메뉴(내 펫·알림 소리·설정)와 그 전용 경로·문구·검사를 제거했다. 설정은 펫 우클릭 메뉴에 남아 있다.
- CPU·메모리 행은 게이지와 정수 %를 함께 보인다. 표시한 % 기준으로 30 미만 파랑, 60 미만 초록, 85 미만 주황, 그 이상 빨강이며, 밝은·어두운 테마별 채움색은 트랙 대비 3:1 이상이다.
- macOS 메모리는 파일 캐시를 점유로 세던 정의(이 Mac에서 99%)에서 활성 상태 보기의 '사용된 메모리'(앱 메모리 + 와이어드 + 압축)로 바꿨다. Windows와 같은 '즉시 사용 가능한 메모리 제외' 설명을 쓴다.
- 검증: 관련 5개 클래스 91개 통과, 헤드리스 렌더 캡처에서 두 게이지 정렬·주황색·% 확인, 실제 macOS에서 앱 측정 82.0%와 같은 순간 `vm_stat` 계산 81.9% 일치, macOS `--smoke-test` 종료 0·PNG 115장.
- 실제 화면에서 펫에 마우스를 올려 바를 직접 보지는 않았다. Windows 표시는 미검증이다.

## 검증 경계

- 진단은 던지기, 추가 파일 배정·재생 순서, 배경 정리 실행을 조작하지 않는다. 이 동작은 헤드리스 검사로만 확인했다.
- 실제 OS의 던지기 손맛·멀티 모니터 경계, Windows 입력, 실제 그린스크린 MP4의 배경 품질은 관찰하지 않았다.
