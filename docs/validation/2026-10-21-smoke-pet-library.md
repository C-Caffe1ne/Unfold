# smoke 진단 · 펫 라이브러리 경유 빌더 이동

2026-10-21. 기준 `codex/pet-overlay-hover` `9d01b7d`, 버전 `1.2.1`. 새 브랜치 `claude/smoke-pet-library-builder`에서 수정했다. 제품 동작은 바꾸지 않았다.

## 원인

`--smoke-test`가 설정 레이아웃 단계의 "unified builder import controls" 검사(`SmokeDiagnostics.cs:674`)에서 실패했다.
펫 라이브러리 개편 후 펫 관리는 '내 펫' 라이브러리에서 시작한다. 하지만 진단은 빌더가 바로 열린다고 가정했다.
이 가정은 이후 단계에도 있었다. 펫 탭에서 진단 앞부분에 열어 둔 빌더가 남아 있다고 보거나, 새 `PetManagementView`가 빌더로 열린다고 봤다.

## 변경 (`src/Unfold.Desktop/SmokeDiagnostics.cs`만)

- `OpenPetBuilder`·`ReturnToPetLibrary` 도우미를 추가했다. 앱과 같은 `PetLibraryCreate`·`PetLibraryBack` 버튼으로 이동한다. 헤드리스 `PetLibraryTestNavigation.OpenBuilder`와 같은 방식이다.
- 설정 레이아웃 단계에서는 먼저 창 안에 라이브러리가 열렸는지 확인한다. 빌더가 없고 `PetLibrarySearch`가 있어야 한다. 이 화면을 `settings-pet-library-tab.png`로 캡처한 뒤 빌더를 연다. 초안 정리 후에는 라이브러리로 돌아간다. 보고서에는 `petManagementLibraryFirstVerified` 필드를 추가했다.
- 반응형 레이아웃 단계는 640×560에서 라이브러리(`responsive-pet-library.png`)를 캡처하고 overflow를 검사한다. 이어서 빌더를 열어 `responsive-pet-create.png`를 캡처한 뒤 라이브러리로 돌아간다.
- 테마 단계의 `theme-*-pets.png`는 이제 라이브러리를 보여 준다. `theme-*-builder.png`는 빌더를 명시적으로 연 뒤 캡처한다.
- 펫 팩 단계는 새 `PetManagementView`에서 먼저 빌더를 연 다음 `OpenPetBuilderFile`을 누른다.

## 실제 macOS에서 관찰한 사실

환경은 macOS 26.6.2(25G83) arm64, .NET 10.0.12, Release 빌드다. 매번 새 빈 `UNFOLD_DATA_DIR`을 썼다.

| 실행 | 소스 | 결과 |
|---|---|---|
| 기준 | `9d01b7d` 그대로 | 종료 1, `success: false`, `:674` "unified builder import controls" |
| 최소 패치 | 설정 단계에서 빌더만 열기 | 종료 1, `VerifyPetPacks`에서 `OpenPetBuilderFile`을 찾지 못함 |
| 수정 1차 | 최종 수정 | 종료 0, `success: true`, 59.3초, PNG 111장 |
| 수정 2차 | 최종 수정을 다시 빌드 | 종료 0, `success: true`, 58.8초, PNG 111장 |

캡처를 직접 열어 확인했다. 펫 탭 첫 화면은 '내 펫' 라이브러리다. '펫 만들기'를 누르면 빌더가 열리고, 초안 이름이 유지된다.
테마 빌더 캡처는 비어 있는 새 빌더다. 640×560 라이브러리 캡처에는 가로 넘침이 없다.
이 진단은 화면 밖 창과 코드 버튼 이벤트로 실행한다. 실제 마우스·키보드 입력은 검증하지 않았다.

## 자동 검사

- `dotnet build src/Unfold.Desktop -c Release`: 경고 0개, 오류 0개
- `dotnet test Unfold.slnx`: 실행을 시작했지만 이 기록을 작성할 때까지 결과를 확인하지 못했다.
  참고로 같은 기준에서 `PurchaseGateTests`는 라이브러리 개편 전 가정 때문에 실패할 수 있다(`codex/pet-throw-media`에서 수정됨).
- 솔루션 전체 Release 빌드: 아직 실행하지 않았다.

Windows는 미검증이다.
