# 펫 크기·타이머·디버그 구현 보고서

## 원인

- 바탕화면 펫은 고정 192px로만 렌더링되어 사용자가 작업 환경에 맞게 크기를 조절할 수 없었다.
- 타이머 정지는 남은 시간을 `00:00`으로 바꿔 다음 재생이 어느 시간부터 시작하는지 화면과 상태만으로 알기 어려웠다.
- 펫 우클릭의 말풍선 접기는 트레이 복구 항목·배지·저장값까지 별도 숨은 상태를 만들었다.
- 실제 알림을 기다리지 않고 사전 안내·초대·휴식·완료 화면을 확인할 방법이 없었다.

## 변경

- 홈의 펫 선택 위에 50~150%, 10% 단위 슬라이더와 현재 퍼센트를 추가했다. 별도 `축소 / 기본 / 확대` 문구는 표시하지 않는다.
- 펫 크기 제목은 슬라이더 위, 현재 퍼센트는 아래에 배치했다. 홈에 있던 바탕화면 펫 표시와 로그인 시 자동 실행은 설정 탭의 `앱 동작` 카드로 옮기고 기존 즉시 적용 동작을 유지했다.
- 홈 미리보기와 바탕화면 펫은 같은 `AnimationView` 크기 계약으로 50%=96px, 100%=192px, 150%=288px를 직접 사용한다. 최소 창의 150%에서만 미리보기 영역을 세로로 확인하며 설정 필드는 고정한다. 말풍선 폭과 글자 크기는 유지하고 네 방향 배치만 다시 계산한다.
- 정지는 `StretchClock.Interval`을 남은 시간으로 유지한다. 홈에는 현재 전체 간격과 `중지됨` 상태명만 표시한다.
- 펫 우클릭 메뉴는 `설정` 한 항목만 남겼다. `bubbleCollapsed`가 포함된 이전 JSON은 알 수 없는 필드로 무시해 계속 불러온다.
- 설정 하단에 저장 가능한 `디버그 도구 사용`과 5분 전·스트레칭·휴식 진행·완료·종료 버튼을 추가했다. 미리보기에는 별도 `PetReminder`를 사용해 실제 타이머, 휴식 세션, 기록, 다시 알림, 효과음 호출을 바꾸지 않는다.
- 홈·설정·기록·펫 추가·말풍선의 고정 설명 문구를 제거했다. 필드명, 값, 파일 메타데이터와 실제 성공·실패 피드백은 유지했다.
- 현재 실행 문서의 접기·정지 표시 설명을 새 계약에 맞췄다. 과거 날짜별 검증 기록은 당시 근거로 보존했다.

## 소유 파일

- 상태·저장: `src/Unfold.Core/AppSettings.cs`, `src/Unfold.Core/StretchClock.cs`
- 런타임·펫: `src/Unfold.Desktop/AppRuntime.cs`, `PetWindow.cs`, `PetSpeechBubble.cs`
- 설정 UI: `SettingsWindow.cs`, `SettingsWindow.Layout.cs`, `SettingsWindow.Preferences.cs`, `SettingsWindow.Debug.cs`, `DesignSystem.cs`
- 진단·회귀: `SmokeDiagnostics.cs`, `BreakReminderTests.cs`, `SettingsDashboardTests.cs`, `TimerControlTests.cs`, `PetReminderTests.cs`, `UiAuditRegressionTests.cs`

## 검증

| 구분 | 결과 |
|---|---|
| Release 전체 빌드 | 성공, 경고 0·오류 0 |
| 변경 범위 집중 테스트 | 23 통과, 실패·건너뜀 0 |
| 크기 일관성 후속 테스트 | 11 통과, 실패·건너뜀 0 |
| 옵션 위치 후속 테스트 | 16 통과, 실패·건너뜀 0 |
| UI 설명 문구 후속 테스트 | 13 통과, 실패·건너뜀 0 |
| 최종 Release 전체 테스트 | 215 통과, 실패·건너뜀 0 |
| 격리 스모크 진단 | `success: true`, PNG 82장 |
| 상태 경계 | 펫 메뉴 한 항목, 50/100/150% 크기, 네 디버그 상태의 실제 타이머·기록·효과음 불변 확인 |
| 레이아웃 | 1120×800·860×680에서 가로 넘침 없음, 50/100/150% 미리보기와 바탕화면 펫의 동일 논리 크기 확인 |

자동 캡처:

- [기본 홈](images/2026-09-20-pet-scale-timer-debug/home-default.png)
- [최소 홈](images/2026-09-20-pet-scale-timer-debug/home-minimum.png)
- [설정 하단](images/2026-09-20-pet-scale-timer-debug/settings-options-bottom.png)
- [중지 타이머](images/2026-09-20-pet-scale-timer-debug/timer-stopped.png)

원본 구조화 결과는 [JSON](2026-09-20-pet-scale-timer-debug-implementation.json)에 있다.

## 미검증·위험

- 캡처는 macOS 호스트의 off-screen Avalonia 자동 진단이다. 실제 마우스 슬라이더 조작, 펫 우클릭 메뉴 체감, 다중 모니터와 Retina/DPI 전환을 확인한 실기 결과는 아니다.
- Windows 실제 클릭 통과, 배율 전환, 다중 모니터 경계 보정은 미검증이다.
- 디버그 미리보기는 효과음 호출이 증가하지 않는 것을 자동 확인했다. 실제 스피커 출력과 사용자가 느끼는 음량은 이번 범위에서 재생하지 않았다.
- 50%와 150%의 바탕화면 체감 크기와 장시간 애니메이션 자원 사용은 사람 검수가 남아 있다.
