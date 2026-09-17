# 설정 탭 구조 검증

> 후속 변경에서 루틴·업무 프로필 설정 진입점을 제거했다. 현재 상태는
> [루틴·프로필 UI 제거 검증](2026-09-17-remove-routine-profiles.md)과
> [홈 시간 설정 검증](2026-09-17-home-break-time.md)을 함께 확인한다.

2026-09-17 · `release/mvp`의 현재 C#·Avalonia 작업 트리에서 설정 기능을 별도
내비게이션 페이지로 분리했다. 기존 사용자 설정 JSON 형식은 변경하지 않았다.

## 원인

타이머 첫 화면의 오른쪽 열에 스트레칭 간격, 자리 비움, 루틴, 말풍선 위치, 다시 알림,
효과음과 기록이 함께 있어 각 값이 타이머 동작인지 알림 표현인지 구분하기 어려웠다.
루틴과 시간 입력이 하나의 적용 버튼에 묶여 있어 화면을 분리하면 저장 경계가 끊어질 수 있었다.

## 변경

- 사이드바에 **설정 탭**을 추가해 타이머, 설정, 루틴·프로필, 기록, 펫 추가의 다섯
  콘텐츠 페이지를 같은 창에서 전환한다.
- 설정 탭은 **알림 설정**과 **타이머 설정** 카드로 나뉜다.
- 알림 설정은 말풍선 위치, 효과음 사용 여부, **스트레칭 알림**과 **완료 알림** WAV를 관리한다.
- 타이머 설정은 **자리 비움 시간**, **스트레칭 시간**, **다시 알림 시간**을 1분 단위로 관리한다.
  스트레칭 시간은 작업 후 알림까지의 기존 `IntervalMinutes`이며 5~240분이다. 나머지 두 값은
  1~60분이다.
- 진행 중에는 스트레칭 시간만 잠기며 일시정지 또는 중지 후 변경할 수 있다. 같은 카드의
  **적용**이 세 타이머 값을 함께 저장하고 Pause·Stop 상태를 유지한다.
- 타이머 화면의 루틴 선택에는 별도 **적용**을 추가했다. 시간 설정을 저장해도 선택 중인
  루틴 초안을 가져가거나 바꾸지 않는다.
- 업무 프로필 편집과 도움말도 같은 값을 **스트레칭 시간**과 **자리 비움 시간**으로 표시한다.

## 소유 파일

- `src/Unfold.Desktop/SettingsWindow.cs`
- `src/Unfold.Desktop/SettingsWindow.Layout.cs`
- `src/Unfold.Desktop/SettingsWindow.Notifications.cs`
- `src/Unfold.Desktop/SmokeDiagnostics.cs`
- `src/Unfold.Desktop/ProfileEditorWindow.cs`
- `src/Unfold.Desktop/PersonalizationWindow.cs`
- `src/Unfold.Desktop/AppRuntime.cs`
- `src/Unfold.Core/Personalization.cs`
- `Tests/Unfold.Tests/SettingsDashboardTests.cs`
- `Tests/Unfold.Tests/TimerControlTests.cs`

## 검증

| 검사 | 결과 |
|---|---|
| 설정 대시보드·타이머 집중 테스트 | 17 통과, 실패·건너뜀 0 |
| `dotnet test Unfold.slnx -c Release --no-restore --disable-build-servers` | 175 통과, 실패·건너뜀 0 |
| 새 `UNFOLD_DATA_DIR`의 `--smoke-test` | `success: true`, PNG 43장 |
| 860×680 최소 창 | 설정 탭 두 카드 표시, 가로 넘침 없음, 스크롤 접근 가능 |
| 시각 확인 | 알림 설정, 타이머 설정, 최소 크기 타이머 화면의 정렬·잘림 확인 |
| `git diff --check` | 통과 |

원본 진단 결과는 [2026-09-17-settings-tab.json](2026-09-17-settings-tab.json)에 보존했다.
진단은 다섯 내비게이션 탭, 설정 페이지 두 섹션, 펫 초안 유지, 타이머 입력 잠금과
명시적 적용, 루틴·프로필 경계를 같은 프로세스에서 확인했다.

## 미검증·위험

- macOS arm64에서 off-screen 자동 진단과 캡처를 실행했다. 사람의 실제 포인터·키보드 탐색,
  효과음 청취 품질과 장시간 사용성 검수는 아니다.
- 실제 Windows의 글꼴 배치, 100%가 아닌 DPI, 키보드 포커스 순서와 WAV 재생은 미검증이다.
- 기존 저장 형식과 값의 의미는 유지했으므로 마이그레이션은 없다. 여기서 **스트레칭 시간**은
  루틴 동작 시간보다 작업 후 알림까지의 시간이라는 설명을 UI에 함께 표시한다.
