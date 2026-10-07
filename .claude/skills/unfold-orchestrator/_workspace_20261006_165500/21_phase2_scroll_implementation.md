# 구현 보고서 — Phase 2A · UI-04 PageBodyScroll 거터와 겹침 회귀

2026년 09월 17일 · `release/mvp` 현재 작업 트리 · C#/.NET 10 Avalonia · `implementation-engineer`.

## 원인

`Ui.PageContent`가 만드는 `PageBodyScroll`은 Fluent 기본값인 오버레이 스크롤바를 썼다.
Fluent의 `ScrollViewer` 템플릿은 `ScrollContentPresenter`가 스크롤바 열까지 가로지르므로
세로 스크롤바가 본문 위에 **그려진다**. 본문 폭을 꽉 채우는 컨트롤은 그 아래로 들어간다.

구현 전 좌표(커스텀 펫 만들기, 헤드리스 Skia, 클라이언트 660×880):

| 항목 | X 좌표 |
|---|---|
| 본문 오른쪽 경계 | 631 |
| `CustomPetName` 오른쪽 경계 | 631 |
| `ImportPetMedia` 오른쪽 경계 | 631 |
| `AssignPetMedia` 오른쪽 경계 | 615 |
| 세로 스크롤바 왼쪽 경계 | 615 |

스크롤바가 본문 경계보다 **16px 안쪽**에서 시작한다. 즉 펫 이름 입력과 **파일 가져오기**
버튼의 오른쪽 16px이 스크롤바 아래에 깔리고, **동작에 넣기**는 정확히 맞닿는다.
520×620 최소 크기에서도 같은 값(본문 491, 스크롤바 475)으로 재현했다.
감사 문서의 "660×880에서도 재현" 기록과 일치한다.

## 변경

`Ui.PageBodyScroll(Control)`을 새로 두고 `PageContent`가 그것을 쓴다.

1. `AllowAutoHide = false` — Fluent 템플릿이 스크롤바를 **오버레이 대신 레이아웃에 예약**한다.
   예약은 스크롤바가 보일 때만 생기므로, 본문이 창에 들어가는 페이지는 아무것도 잃지 않는다.
2. `PART_VerticalScrollBar`에 왼쪽 `ScrollGutter = DesignSystem.Space = 8` 여백을 둔다.
   본문은 네이티브 트랙 폭과 8px 거터를 함께 내주므로 컨트롤과 트랙이 겹치지 않고,
   트랙은 `ScrollViewer` 안에 남아 정적 네이티브 렌더에서도 보인다.

구현 후 좌표(같은 창, 660×880): 본문 오른쪽 607, 스크롤바 왼쪽 615, 트랙 오른쪽 631.
520×620에서는 본문 467 / 스크롤바 475 / 트랙 491이다.
1400×1600처럼 스크롤이 필요 없는 크기에서는 스크롤바가 사라지고 본문이 `PageActions`와
같은 위치(1371)에서 끝난다.

예약 트랙과 거터가 좁은 펫 팩 창의 본문 폭을 줄이므로 `PetPackWindow`의 프레임 인셋은
16px에서 10px로 조정했다. 480px 최소 폭에서도 384 논리 픽셀 미리보기와 가로 넘침 없음
계약을 함께 지킨다. 뷰어를 음수 여백으로 프레임 인셋까지 확장하는 대안은 트랙이 정적
네이티브 캡처에 그려지지 않아 통합 QA에서 폐기했다.

`CustomPetWindow.cs`는 공통 거터만으로 요구 좌표를 모두 만족해 변경하지 않았다.

### 회귀 검사

`Tests/Unfold.Tests/DesignSystemTests.cs`에 `PageBodyScrollGeometry` 헬퍼를 추가했다.
보임 여부가 아니라 **좌표**를 비교한다.

- `ClearsScrollBar`: 스크롤바가 있을 때 `PageActions` 오른쪽 − 본문 오른쪽 = 트랙 폭 + 8,
  스크롤바 왼쪽 − 본문 오른쪽 = 8, 트랙 오른쪽 ≤ 뷰어 오른쪽(= 그려질 수 있는 조건),
  트랙 오른쪽 ≤ `PageFrame` 안쪽 경계, 그리고 지정한 컨트롤마다
  오른쪽 경계 ≤ 스크롤바 왼쪽 − 8.
- `KeepsFullWidthWithoutScrollBar`: 스크롤바가 없으면 본문 오른쪽 = `PageActions` 오른쪽.
  거터가 상시 인셋이 되지 않았음을 고정한다.

이 헬퍼를 쓰는 검사 세 개를 추가했다.

| 검사 | 위치 | 크기 |
|---|---|---|
| `TheBodyScrollBarNeverCoversTheNameFieldImportOrActionSlots` | `CustomPetTests.cs` | 660×880, 590×750, 520×620 + 스크롤 끝, 그리고 1400×1600(스크롤바 없음) |
| `SettingsPetBuilderKeepsItsInputsAndButtonsClearOfTheBodyScrollBar` | `DesignSystemTests.cs` | 설정 펫 만들기 탭 1120×800, 990×740, 860×680 + 스크롤 끝 |
| `TheBodyScrollBarReservesItsTrackInsteadOfCoveringPageControls` | `DesignSystemTests.cs` | 루틴·프로필 편집기, 루틴 라이브러리, 기록, 펫 팩의 최소 크기 + 스크롤 끝 |

검사 대상 컨트롤: `CustomPetName`, `ImportPetMedia`, `AssignPetMedia`, `CustomPetAction`,
`CustomPetFile_idle`, `CustomPetRemove_idle`, `CustomPetPreview_idle`, `CustomPetFile_click`.

`SmokeDiagnostics`에는 `VerifyBodyScrollGutter`를 추가해 같은 계약을 네이티브 진단에서도
검사한다. 커스텀 펫 창은 660×880 · 590×750 · 520×620과 스크롤 끝에서, 설정 펫 만들기 탭은
1120×800 · 990×740 · 860×680과 스크롤 끝에서 확인한다. 기존 가로 넘침, 고정 작업 영역,
초안 유지, 캡처 파일 이름과 개수는 그대로 두었다.

## 소유 파일

| 파일 | 변경 |
|---|---|
| `src/Unfold.Desktop/Ui.cs` | `ScrollGutter` 상수, `PageBodyScroll` 팩터리, `PageContent`가 이를 사용 |
| `src/Unfold.Desktop/SmokeDiagnostics.cs` | `VerifyBodyScrollGutter`, `PetBuilderControls`와 6개 호출 지점 |
| `src/Unfold.Desktop/PetPackWindow.cs` | 480px 최소 폭의 384px 미리보기를 보존하도록 프레임 인셋 조정 |
| `Tests/Unfold.Tests/DesignSystemTests.cs` | `PageBodyScrollGeometry` 헬퍼, 검사 2개, `SettingsScope`, `Fits` 실패 메시지 보강 |
| `Tests/Unfold.Tests/CustomPetTests.cs` | 커스텀 펫 창 좌표 회귀 검사 1개 |

`src/Unfold.Desktop/CustomPetWindow.cs`는 변경이 필요하지 않아 그대로 두었다.
같은 작업 트리의 다른 워커 파일(`AppRuntime.cs`, `PetWindow.cs`,
`PersonalizationWindow.cs`, `SettingsWindow.Layout.cs`와 해당 테스트)은 건드리지 않았다.

## 검증

- **결함 재현(구현 전)**: 위 좌표표. 660×880과 520×620 모두에서 스크롤바가 본문 경계보다
  16px 안쪽에서 시작했다.
- **구현 전/후 대비**: `Ui.cs`를 오버레이 동작으로 되돌리면 새 검사 3개가 모두 실패한다.
  스크롤바 왼쪽이 본문보다 16px 안으로 들어오던 기존 좌표를 회귀 검사가 검출한다.
- **집중 테스트**: `dotnet test Unfold.slnx -c Release --no-restore --filter
  "FullyQualifiedName~DesignSystemTests|FullyQualifiedName~CustomPet|FullyQualifiedName~PetPackWindowTests|
  FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PersonalizationWindowTests|
  FullyQualifiedName~UiTests|FullyQualifiedName~SettingsUiTests|FullyQualifiedName~RoutineEditorTests"`
  → **50개 통과, 실패 0, 건너뜀 0**.
- **폭 계약 보존**: `PetPackWindowTests.EnlargedLightPreviewFitsTheMinimumWidthAndDoesNotInstall`
  (480px 최소 폭 · 384px 미리보기)이 통과한다.
- **빌드**: `dotnet build src/Unfold.Desktop -c Release --no-restore` → 경고 0, 오류 0.
- 임시 프로브 파일은 남기지 않았다.

## 미검증·위험

- 통합 QA의 격리 Release 스모크는 `success: true`, PNG 42장을 만들었다. macOS 네이티브
  off-screen 캡처에서 기본·최소·스크롤 끝의 스크롤바가 보이고 입력·버튼과 분리된 것을 확인했다.
- 실제 사용자의 macOS 마우스·키보드 조작, Windows·DPI 배율·보조 기술은 실행하지 않았다.
- 트랙 폭 16px은 Fluent 기본값이다. 다른 플랫폼 테마가 더 넓은 트랙을 쓰면 트랙 오른쪽이
  달라질 수 있으나 회귀 검사는 실제 트랙 폭과 거터 합을 사용한다.
- UI-05~UI-14, 디자인 토큰, 입력 포커스 스타일, 창 크기, 데이터 형식은 건드리지 않았다.
