# 설정 탭 헤더 제거 검증

2026-09-17 · `release/mvp`의 C#·Avalonia 설정 창에서 사이드바로 전환하는 각 페이지의
경로 문구, 큰 제목과 설명으로 구성된 상단 헤더를 제거했다.

## 변경

- 타이머 탭의 **UNFOLD / 나의 휴식 공간**, 큰 제목과 상태 배지를 제거했다.
- 설정 탭의 **UNFOLD / 설정**, **알림과 타이머를 설정하세요.**와 설명을 제거했다.
- 기록·내보내기 탭과 펫 추가의 열기·만들기 탭은 공통 `PageContent`에서 헤더 없는
  레이아웃을 사용한다.
- 제거된 공간은 각 탭의 실제 카드·미리보기·기록 내용이 사용한다.
- 기록, 펫 팩과 커스텀 펫을 별도 창으로 열 때는 기존 헤더를 유지한다.

## 소유 파일

- `src/Unfold.Desktop/Ui.cs`
- `src/Unfold.Desktop/SettingsWindow.Layout.cs`
- `src/Unfold.Desktop/BreakReviewWindow.cs`
- `src/Unfold.Desktop/PetManagementView.cs`
- `src/Unfold.Desktop/PetPackWindow.cs`
- `src/Unfold.Desktop/CustomPetWindow.cs`
- `src/Unfold.Desktop/SmokeDiagnostics.cs`
- `Tests/Unfold.Tests/SettingsDashboardTests.cs`

## 자동 검증

| 검사 | 결과 |
|---|---|
| 타이머·설정·기록·펫 탭 헤더 부재와 레이아웃 집중 테스트 | 9 통과, 실패·건너뜀 0 |
| `dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers` | 173 통과, 실패·건너뜀 0 |
| 새 `UNFOLD_DATA_DIR`의 `--smoke-test` | `success: true`, PNG 42장 |
| 기본·860×680 최소 창의 네 탭 캡처 | 헤더 문구 없음, 카드와 고정 작업 영역 표시 |
| `git diff --check` | 통과 |

원본 진단 결과는 [2026-09-17-tab-header-removal.json](2026-09-17-tab-header-removal.json)에
보존했다.

## 미검증·위험

- macOS에서 off-screen Avalonia 렌더링과 자동 버튼 이벤트를 실행했다. 실제 포인터,
  VoiceOver와 장시간 사용성 검수는 아니다.
- 실제 Windows의 글꼴 배치, DPI와 키보드 포커스 순서는 이번 변경에서 실행하지 않았다.
