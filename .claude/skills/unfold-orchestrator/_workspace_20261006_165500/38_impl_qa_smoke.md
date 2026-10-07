# 구현 보고서 — QA-P0 스모크 진단 증거 정정

## 원인

`SettingsPreferencesScroll`의 현재 `Extent.Height`와 `Viewport.Height`는 모두 618이다. 따라서 기존 `SettingsNotificationCard.BringIntoView()`와 `SettingsTimerSettingsCard.BringIntoView()`는 모두 no-op이었고, `settings-notification-options.png`와 `settings-timer-options.png`는 같은 화면을 서로 다른 스크롤 상태인 것처럼 기록했다. 비교 기준 `/tmp/unfold-impl-c.onqBgg/verification/`의 두 파일은 MD5가 모두 `aa6b4cb16f297e5c183e4f5c1c57c0e4`였다.

UI-14는 주간 기록 날짜 행의 펼침 동작과 상세 렌더링이 구현됐지만, 기존 스모크는 `weekly-review.png`를 접힌 상태에서만 캡처했다. 따라서 루틴명·완료 시각·실제 휴식 시간이 캡처 화면에 나타나는지 자동 증거가 없었다.

## 변경

### 설정 옵션 분기

`SettingsPreferencesScroll.Extent.Height > SettingsPreferencesScroll.Viewport.Height`를 실제 분기 조건으로 사용했다.

- 스크롤이 필요하면 `ScrollToHome()`과 `ScrollToEnd()`에서 각각 `settings-options-top.png`, `settings-options-bottom.png`를 만든다. 상단 오프셋 0, 하단 오프셋 양수, 하단의 타이머 카드 완전 노출, 두 PNG의 바이트 차이를 단언한다.
- 스크롤이 필요 없으면 `settings-options-overview.png` 한 장만 만든다. 현재 실행은 `Extent.Height=618`, `Viewport.Height=618`이므로 이 분기를 탔다.
- 기존의 중복 파일명 `settings-notification-options.png`, `settings-timer-options.png`는 더 이상 생성하지 않는다.

### UI-14 펼침 증거

기존 `BreakReviewWindow`가 노출한 이름 있는 `ToggleButton`의 공개 `IsChecked` 동작으로 완료 기록이 있는 날짜를 펼쳤다. 펼친 상세에서 완료 기록의 루틴명, 저장된 offset 기준 `HH:mm 완료`, 실제 휴식 시간 문자열이 모두 `IsVisible`이고 창 경계 안에 있는지 검사한 뒤 `weekly-review-expanded.png`를 캡처한다. 제품 파일이나 새 진입점은 추가하지 않았다.

## 소유 파일

- 수정: `src/Unfold.Desktop/SmokeDiagnostics.cs`
- 신규 보고서: `.claude/skills/unfold-orchestrator/_workspace/38_impl_qa_smoke.md`
- `BreakReviewWindow.cs`, `DesignSystem.cs`, `Ui.cs`, `SettingsWindow*.cs`, 테스트 파일은 수정하지 않았다.

## 변경 후 PNG

비교 기준 42장에서 아래처럼 교체돼 총 개수는 **42장으로 동일**하다.

- 제거: `settings-notification-options.png`, `settings-timer-options.png`
- 추가: `settings-options-overview.png`, `weekly-review-expanded.png`

최종 목록:

```text
additional-routine.png
custom-pet-editor.png
custom-pet-minimum-scrolled.png
custom-pet-minimum.png
custom-pet-preview.png
dialog-confirm.png
dialog-error.png
dialog-prompt.png
editor.png
pack-error.png
pack-installed.png
pack-preview.png
pack-reinstall.png
pack-update.png
pet.png
profile-editor.png
routine-editor.png
routine-library.png
settings-completed.png
settings-minimum-scrolled.png
settings-minimum.png
settings-options-overview.png
settings-pet-create-minimum-scrolled.png
settings-pet-create-minimum.png
settings-pet-create-tab.png
settings-pet-open-minimum.png
settings-pet-open-tab.png
settings-review-tab.png
settings.png
speech-bottom.png
speech-completed.png
speech-five-minutes.png
speech-folded.png
speech-left.png
speech-overtime.png
speech-right.png
speech-top.png
timer-paused.png
timer-stopped.png
weekly-review-expanded.png
weekly-review.png
work-profiles.png
```

## smoke.json 키

| 키 | 의미 | 최종 값 |
|---|---|---|
| `settingsLayout.settingsOptionsScrollRequired` | Extent가 Viewport보다 커서 세로 스크롤 증거가 필요한지 | `false` |
| `settingsLayout.settingsOptionsExtentHeight` | 판정에 사용한 콘텐츠 높이 | `618` |
| `settingsLayout.settingsOptionsViewportHeight` | 판정에 사용한 뷰포트 높이 | `618` |
| `settingsLayout.settingsOptionsTopOffset` | overview/상단 캡처 시 Y 오프셋 | `0` |
| `settingsLayout.settingsOptionsBottomOffset` | 하단 캡처 시 Y 오프셋. overview 분기는 해당 없음 | `null` |
| `settingsLayout.settingsOptionsCapturesDiffer` | 스크롤 분기에서 상·하단 PNG가 다른지. overview 분기는 비교 대상이 없어 null | `null` |
| `settingsLayout.settingsOptionsCaptureFiles` | 실제 생성된 설정 옵션 증거 파일명 | `["settings-options-overview.png"]` |
| `weeklyReview.expandedCaptured` | 기록 날짜를 펼친 뒤 캡처했는지 | `true` |
| `weeklyReview.captureFile` | 펼침 증거 파일명 | `weekly-review-expanded.png` |
| `weeklyReview.routineNameVisible` | 루틴명이 보이고 창 안에 있는지 | `true` |
| `weeklyReview.completionTimeVisible` | 완료 시각이 보이고 창 안에 있는지 | `true` |
| `weeklyReview.actualDurationVisible` | 실제 휴식 시간이 보이고 창 안에 있는지 | `true` |

## MD5 확인

최종 격리 실행 `/tmp/unfold-impl-qa.u8WfgN/verification/`에서:

```text
MD5 (settings-options-overview.png) = aa6b4cb16f297e5c183e4f5c1c57c0e4
MD5 (weekly-review-expanded.png) = 9f9d23620c74a40723bac08c8da9a2b1
MD5 (weekly-review.png) = 384d969324d05d880159965640fc0d1d
```

기존 중복 설정 PNG의 해시는 overview와 같지만, 같은 바이트를 두 이름으로 저장하지 않는다. 접힘/펼침 주간 기록 PNG는 서로 다른 해시다.

## 검증

- `git diff --check`: 출력 없음, 종료 코드 0.
- `dotnet test Unfold.slnx -c Release --no-restore`: 통과 183, 실패 0, 건너뜀 0, 전체 183, 테스트 실행 20초. 직전 기준선과 같은 183개다.
- `dotnet build src/Unfold.Desktop -c Release --no-restore`: 경고 0, 오류 0, 1.71초.
- 새 격리 데이터 `/tmp/unfold-impl-qa.u8WfgN`에서 `UNFOLD_DATA_DIR=... dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test`: 종료 코드 0.
- 최종 `smoke.json`: `success=true`, `imageFiles=42`, `completedBreaks=1`, `exportedBreaks=1`.
- `md5 /tmp/unfold-impl-qa.u8WfgN/verification/settings-*options*.png`: `settings-options-overview.png` 한 장만 출력했다. 기존처럼 같은 이미지 두 장이 남지 않았다.

## 소유 파일 밖에서 발견했지만 고치지 않은 문제

`weekly-review-expanded.png`에서 체크된 날짜 `ToggleButton`의 배경이 밝게 바뀌지만 날짜·횟수·기록된 시간 글자도 밝은 색을 유지해 헤더 텍스트가 육안으로 거의 사라진다. 상세의 루틴명·완료 시각·실제 휴식 시간은 보이며 이번 진단 검사도 통과한다. 이 대비 문제는 `BreakReviewWindow.cs`/공통 ToggleButton 스타일 소유 범위이므로 수정하지 않았다.

## 미검증·위험

- 이번 결과는 macOS의 off-screen 자동 렌더·이벤트 진단이다. 실제 포인터 클릭과 키보드 Enter/Space로 날짜 행을 펼치는 체감은 확인하지 않았다.
- VoiceOver·Narrator의 날짜 행 이름·상태 낭독과 포커스 이동은 확인하지 않았다.
- Windows 100–200% DPI, macOS Retina, 다중 모니터에서의 설정 스크롤 분기와 캡처 배치는 확인하지 않았다.
- 현재는 스크롤 불필요 분기만 실제 실행했다. 향후 글자·간격 변경으로 `Extent > Viewport`가 되면 코드가 상·하단 해시 차이와 타이머 카드 노출을 검사하지만, 이번 실행에서 그 분기를 통과한 것은 아니다.
