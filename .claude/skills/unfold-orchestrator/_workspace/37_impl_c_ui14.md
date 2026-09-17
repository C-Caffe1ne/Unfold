# 구현 보고서 — C-P0 UI-14 주간 기록 날짜 행 펼침

## 원인

`BreakReview.Entries`에는 날짜별 상세에 필요한 `CompletedAt`, `RoutineName`, `ActualSeconds`가 이미 저장되지만, `BreakReviewWindow`는 `Review.Days`의 집계만 오름차순 표로 렌더링하고 개별 완료 기록을 표시하지 않았다. 따라서 Core 저장 계약이나 `StoredHistory.Version`을 바꿀 필요 없이 Desktop 렌더링·상호작용만 보완했다.

## 변경

### 화면 구조와 상호작용

- 날짜 행을 최신 날짜부터 렌더링한다.
- 기록이 있는 날짜만 전체 헤더 영역이 `ToggleButton`으로 동작하며, 날짜·횟수·기록된 시간·chevron을 표시한다. 0건 날짜에는 토글과 포커스 정지를 만들지 않았다.
- 펼친 상세는 `루틴명 / 저장된 offset의 HH:mm 완료 / 실제 휴식 시간` 순서로 표시한다. 날짜 안에서는 완료 시각 내림차순, 같은 시각은 `SessionId` 오름차순이다.
- Enter·Space는 펼침/접힘, Left·Right는 접기/펼치기, Up·Down은 기록이 있는 날짜 헤더 사이 이동으로 연결했다. 상세 텍스트는 포커스 정지가 아니며 토글 후 포커스는 헤더에 남는다.
- 자동화 이름은 날짜, 휴식 횟수, 기록된 시간, 접힘/펼침 상태를 포함하고 `ToggleButton.IsChecked`로 상태를 노출한다.
- 같은 기간 새로고침은 아직 존재하는 펼친 날짜를 보존하고, 이전/다음 7일 이동은 펼침 상태를 초기화한다. 여러 날짜를 동시에 펼칠 수 있다.
- 현재 7일이 비면 `이 7일 동안 완료한 휴식이 없어요.`, `첫 휴식은 언제든 괜찮아요.`, `타이머로 돌아가기`를 표시한다. 과거 빈 기간에는 `이 기간에는 완료한 휴식이 없어요.`만 표시한다. 이전/다음 7일과 CSV 내보내기는 유지했다.
- 기존 주간 합계, 기간 이동, 새로고침, CSV 내보내기, 고정 하단 작업 영역과 설정 탭의 `showHeader` 동작을 보존했다.

### 구형 기록 표기 분기

- `RoutineName == null`이면 저장된 `RoutineId`를 그대로 표시하며 현재 루틴 사전을 조회하지 않는다.
- `ActualSeconds == null`이면 `실제 시간 미기록 · 당시 목표 N초` 또는 `N분 NN초`로 표시한다. 계획 시간을 실제 휴식이라고 부르지 않는다.
- `ActualSeconds == 0`은 누락으로 처리하지 않고 `실제 휴식 0초`로 표시한다.
- 주간 합계 안내도 실제 시간이 없는 구형 기록은 당시 목표 시간을 합산한다고 명시했다.

## 소유 파일

- 수정: `src/Unfold.Desktop/BreakReviewWindow.cs`
- 신규: `Tests/Unfold.Tests/BreakReviewTests.cs`
- 보고서: `.claude/skills/unfold-orchestrator/_workspace/37_impl_c_ui14.md`

`BreakReviewWindow`의 public 생성자 시그니처 `BreakReviewWindow(Func<DateOnly, BreakReview>, Func<string?>?, Func<BreakReview, Task<string?>>?, DateOnly?)`는 변경하지 않았다. `src/Unfold.Core/*`, `DesignSystem.cs`, `Ui.cs`, `SmokeDiagnostics.cs` 및 기존 테스트 파일은 수정하지 않았다.

## 추가 테스트

1. `ReviewDateRowExpandsToShowRoutineCompletionTimeAndActualDuration`
   - Enter로 펼침, 포커스 유지, 저장된 offset의 `09:42`, 실제 65초의 `1분 05초`, 유효한 실제 0초, 자동화 펼침 상태를 단언한다.
2. `ReviewDateRowShowsLegacyPlannedDurationWithoutCallingItActual`
   - null 이름은 routine ID, null 실제 시간은 `실제 시간 미기록 · 당시 목표 20초`이며 `실제 휴식 20초`가 아님을 단언한다.
3. `ReviewDateRowsSortNewestFirstAndEntriesLatestFirst`
   - 7개 날짜가 최신순이고, 상세가 완료 시각 내림차순·동일 시각 `SessionId` 오름차순임을 단언한다.
4. `ReviewEmptyCurrentWeekOffersReturnToTimerWithoutHidingPeriodNavigation`
   - 현재 빈 기간의 CTA·비압박 문구와 이전 7일·CSV 버튼이 함께 보임을 단언한다.
5. `ReviewEmptyPastWeekDoesNotOfferStartAction`
   - 과거 빈 기간에는 CTA가 없고 과거 전용 문구·다음 7일·CSV가 남음을 단언한다.
6. `ReviewExpansionPersistsOnRefreshAndResetsAcrossPeriods`
   - 같은 기간 새로고침 보존과 양방향 기간 이동 초기화를 단언한다.
7. `ReviewDateHeadersToggleWithEnterSpaceAndArrowKeys`
   - Enter·Space·Left·Right 및 Up·Down 이동, 0건 날짜의 토글 부재를 단언한다.
8. `ReviewDetailKeepsKoreanWeekdayAndLongRoutineNameWithinMinimumWindow`
   - 560×600 독립 창과 860×680 설정 탭에서 한글 요일, 60자 루틴명 줄바꿈, 상세 열 비겹침을 단언한다.

## 검증

- `git diff --check`: 출력 없음, 종료 코드 0.
- `dotnet test Unfold.slnx -c Release --filter FullyQualifiedName~BreakReviewTests`: 통과 8, 실패 0, 건너뜀 0, 전체 8, 1초.
- `dotnet test Unfold.slnx -c Release --no-restore`: 통과 183, 실패 0, 건너뜀 0, 전체 183, 16초. 기준선 175에서 신규 8개만큼 증가했다.
- `dotnet build src/Unfold.Desktop -c Release --no-restore`: 경고 0, 오류 0, 1.18초.
- 새 격리 데이터 `/tmp/unfold-impl-c.onqBgg`에서 `UNFOLD_DATA_DIR=... dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test`: 종료 코드 0. `smoke.json`의 `success=true`, `imageFiles=42`, `completedBreaks=1`, `exportedBreaks=1`; `verification/` PNG도 42장이다.

## 캡처 비교

기준선 `/tmp/unfold-impl-a.MGim9I/verification/`과 신규 `/tmp/unfold-impl-c.onqBgg/verification/`을 비교했다.

- `weekly-review.png`: 기준선의 날짜 오름차순 정적 7일 표에서 신규 최신순 날짜 행으로 바뀌었고, 9월 17일 기록 행에 횟수·시간·chevron이 보인다. 그러나 스모크가 날짜 헤더를 누르지 않아 **접힌 상태만 캡처되며 상세 행은 캡처에 남지 않는다**.
- `settings-review-tab.png`: 기준선의 7개 0행 표 대신 현재 기간 빈 상태 문구와 `타이머로 돌아가기` CTA가 보인다. 완료 기록이 생기기 전 캡처라 펼칠 행이 없고 **펼침 상태는 캡처에 남지 않는다**.
- 두 신규 파일은 기준선과 SHA-256이 각각 달라 실제 렌더 변경이 발생했다. 자동 캡처는 배치 근거이며 실제 포인터·키보드·보조기기 사용성 증거는 아니다.

## 미검증·위험

- 소유 파일 밖에서 발견했지만 고치지 않은 문제: `SmokeDiagnostics.cs`가 `weekly-review.png`에서 날짜 헤더를 펼치지 않고, `settings-review-tab.png`를 완료 기록 생성 전에 캡처한다. 사양에 적힌 expanded 캡처 이름들도 현재 생성하지 않으므로 시각 증거가 접힌 상태에 한정된다. QA-P0 소유 범위로 남겼다.
- 자동 테스트로 키보드 라우팅과 포커스를 확인했지만 실제 macOS 키보드 입력, VoiceOver 낭독·토글 상태 전달, 포인터 전체 영역 클릭 감각은 사람이 확인하지 않았다.
- Windows Narrator, Windows 렌더링·DPI, macOS Retina·다중 모니터, 860×680 실제 화면의 스크롤 체감은 미검증이다.
- 자동 스모크는 현재 macOS 환경에서 통과했지만 장시간 사용과 실제 CSV 저장 선택기 동작은 검증하지 않았다.
