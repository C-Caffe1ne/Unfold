# 구현 보고서

## 원인

- `BreakReviewWindow.cs`의 날짜 행은 `ToggleButton.Background`를 투명으로 지정했지만, 실제 글자 뒤를 그리는 Fluent 템플릿의 `PART_ContentPresenter`에는 `:checked` 상태 배경이 별도로 적용됐다.
- 펼친 상태에서 프레젠터 배경이 `#DFE5D1`(`DesignSystem.Cream`)이 된 반면, 날짜·횟수·시간 `TextBlock`도 `Ui.Text` 기본 전경 `#DFE5D1`을 사용했다. 추가한 회귀 검사를 수정 전 구현에 실행했을 때 실효 대비는 `1.00:1`이었다.

## 변경

- 날짜 헤더 `ToggleButton` 인스턴스의 로컬 스타일만 추가해 `:checked` 상태의 `PART_ContentPresenter` 배경을 투명하게 만들었다. 펼침은 선택이 아니므로 지속 강조 배경을 제거하고, 기존 체브론 방향과 접근성 이름으로 상태를 계속 전달한다.
- 펼친 헤더의 `:pointerover`에는 `DesignSystem.Hover`, `:pressed`에는 `DesignSystem.OutlineSubtle`을 적용해 호버·누름 피드백을 유지했다. 접힘 및 포커스 스타일, 키보드 동작, 펼침 유지·기간 이동 초기화 로직은 바꾸지 않았다.
- 전역 `DesignSystem.cs`와 `Ui.cs`, `BreakReviewWindow`의 public 생성자 시그니처는 변경하지 않았다.

## 소유 파일

- `src/Unfold.Desktop/BreakReviewWindow.cs`
- `Tests/Unfold.Tests/BreakReviewTests.cs`
- 요청된 본 보고서: `.claude/skills/unfold-orchestrator/_workspace/39_impl_c_expanded_header_fix.md`

## 검증

- 추가 테스트: `ExpandedDateHeaderTextKeepsMinimumContrastAgainstEffectiveBackground`
  - 펼친 날짜 헤더의 날짜·횟수·시간 세 글자 요소 각각에 대해 렌더 트리에서 가장 가까운 불투명 실효 배경을 찾고 WCAG 명암비가 최소 `4.5:1`인지 단언한다.
  - 수정 후 실효 조합은 `#DFE5D1` 전경 / `#2B2F2A` 배경이며 명암비는 `10.5435:1`이다.
- 결함 검출력 역검증: 구현 수정 전에 새 테스트만 추가해 실행했고 실패했다. 실제 실패 메시지는 `foreground=#ffdfe5d1, background=#ffdfe5d1`, `contrast was 1.00:1`이었다. 이후 구현 수정을 적용해 같은 테스트가 통과하는 것을 확인했다.
- `git diff --check`: 통과, 출력 없음.
- `dotnet test Unfold.slnx -c Release --filter FullyQualifiedName~BreakReviewTests`: 전체 9, 통과 9, 실패 0, 건너뜀 0.
- `dotnet test Unfold.slnx -c Release --no-restore`: 전체 184, 통과 184, 실패 0, 건너뜀 0. 직전 기준선 183에서 새 검사 1개만 증가했다.
- `dotnet build src/Unfold.Desktop -c Release --no-restore`: 경고 0, 오류 0.
- 새 격리 데이터 디렉터리 `/tmp/unfold-impl-c2.vEe21l`에서 스모크 실행: `smoke.json`의 `success=true`, `imageFiles=42`, `weeklyReview.expandedCaptured=true`; 진단용 데이터 PNG 4장을 포함한 디렉터리 전체 PNG는 46장이다.
- 캡처 육안 확인: 기존 `/tmp/unfold-impl-qa.W7sebE/verification/weekly-review-expanded.png`에서는 펼친 9월 17일 헤더가 밝은 크림 막대로 채워져 체브론만 보였다. 새 `/tmp/unfold-impl-c2.vEe21l/verification/weekly-review-expanded.png`에서는 지속 강조 배경이 사라졌고 `9월 17일 (목)`, `1회`, `3분 10초`가 접힌 행과 같은 밝은 전경으로 모두 읽힌다. 상세 행도 계속 표시된다.

## 미검증·위험

- 자동 Headless 테스트와 macOS 스모크 캡처로 확인했다. Windows 실제 렌더링과 실제 사용자의 체감은 이번 작업에서 확인하지 않았다.
- 소유 파일 밖에서 새로 발견했으나 고치지 않은 문제는 없다.
