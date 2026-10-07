# 33. UX 흐름 재설계 — UI-14와 사용자 목표 기반 정보 구조

- 날짜: 2026-09-17
- 기준: `release/mvp`의 현재 작업 트리(`2f02e8a` + Phase 1·2 및 후속 미커밋 변경)
- 런타임: C# · .NET 10 · Avalonia
- 범위: 보고서만 작성. `src/`와 `Tests/`는 수정하지 않음.
- 기준 입력: `_workspace/30_ui_redesign_input.md`, `_workspace/09_ux_flow_audit.md`

## 결론

1. 기존 문제 1.1·1.2·3.1은 루틴·프로필 일반 UI가 제거되어 **사용자 경로에서는 해소**됐다. 다만 `PersonalizationView`의 독립 실행 기본값은 타이머 상태를 모르는 호환 경로이므로, 기능을 다시 노출할 때 그대로 연결하면 3.1이 재발할 수 있다(`SettingsWindow.Layout.cs:52-60`, `PersonalizationWindow.cs:30-37,151-165`).
2. 문제 2.1·5.1·5.2는 트레이/펫 배지 복구, 고정 경고 요약, 동작별 직접 선택·교체 확인으로 코드상 해소됐다(`AppRuntime.cs:10-39,160-172,345-366`, `PetWindow.cs:22-36,129-168`, `PetPackWindow.cs:89-108,141-153`, `CustomPetWindow.cs:69-116,197-242`).
3. 문제 4.1은 **잔존**한다. 회고 화면은 `Review.Entries`를 보유하면서도 날짜별 횟수·합계만 렌더링한다(`BreakReviewWindow.cs:74-90`, `BreakHistory.cs:52-62`). 따라서 UI-14가 4단계의 P0 구현 대상이다.
4. UI-14에 필요한 새 기록 데이터는 이미 있다. `CompletedAt`, `RoutineName`, `ActualSeconds`가 저장되고 새 완료 시 실제 경과 시간을 기록한다(`BreakHistory.cs:5-9,34-43`). 저장 버전 변경은 필요 없지만, 구형 version 1 기록은 `RoutineName`과 `ActualSeconds`가 없을 수 있으므로 실제 시간을 추정해서 표시해서는 안 된다(`BreakHistory.cs:14,20-31`, `docs/personalization.md:31-38,52-53`).
5. 현재 정보 구조는 기록·펫 추가에는 명확하지만, 타이머 관련 설정이 두 탭에 나뉘고 루틴 관리는 의도적으로 제거되어 요청된 네 사용자 목표와 완전히 일치하지 않는다(`SettingsWindow.Layout.cs:52-60,105-151,179-187`, `docs/mvp.md:29-35,53-59`). UI-14에서는 기록 흐름만 확정하고, 루틴 관리 재도입은 별도 제품 범위 승인 뒤 다룬다.

---

## 1. 기존 UX 문제 재판정

### 판정표

| 문제 | 판정 | 현재 근거 | 남은 위험 |
|---|---|---|---|
| 1.1 선택과 다른 루틴 편집 | **해소(일반 UI)** | 설정 사이드바는 타이머·설정·기록·펫 추가 4개이고 루틴 선택/편집 컨트롤을 만들지 않는다(`SettingsWindow.Layout.cs:52-60`; `SettingsDashboardTests.cs:121-129`). | 내부 호환 편집기는 남아 있지만 일반 앱에서 생성하지 않는다(`PersonalizationWindow.cs:10-17`; `docs/personalization.md:3-14`). 재노출 시 선택 대상 계약을 다시 검토해야 한다. |
| 1.2 프로필 캡션 오인 | **해소(일반 UI)** | 홈은 펫/타이머/시간/오늘 기록 카드만 구성하고, 프로필 캡션이나 루틴 카드가 없다(`SettingsWindow.Layout.cs:26-43,67-151`). | 진단용 `PersonalizationView`의 프로필 상세는 남아 있으나 일반 내비게이션에서 도달하지 않는다(`PersonalizationWindow.cs:73-98`; `docs/personalization.md:13-14`). |
| 2.1 접힌 말풍선 상태 부재 | **해소(코드·자동 검사)** | 접힌 초대/휴식을 `Waiting`/`Resting`으로 구분하고 트레이 문구·툴팁·펼치기 가능 여부를 하나의 계약으로 만든다(`AppRuntime.cs:10-39,160-172`). 펫에도 빈 링/채운 점 배지를 표시한다(`PetWindow.cs:22-36,153-168`). 트레이의 **휴식 알림 펼치기**는 세션을 유지한 채 접힘만 해제한다(`AppRuntime.cs:294-300,345-366`). | `speech-folded.png`와 자동 테스트는 상태 존재를 보여 주지만 실제 메뉴 막대 클릭, Windows 트레이, 배지 체감 대비는 미검증이다(`docs/validation/2026-09-17-ui-ux-phase2.md:45-52`). |
| 3.1 진행 중 프로필 적용 후 실패 | **해소(일반 UI), 잠재 잔존** | 일반 UI에서 프로필 적용 진입점이 제거됐다(`SettingsWindow.Layout.cs:52-60`; `docs/mvp.md:35,55-59`). 내부 뷰는 `canApplyProfile`이 거짓이면 클릭 전에 비활성화하고 사유를 표시한다(`PersonalizationWindow.cs:151-165`). | 독립 창 생성자는 `canApplyProfile`을 전달하지 않아 기본값 `true`를 쓴다(`PersonalizationWindow.cs:12-17,30-37`). 일반 UI에 되살릴 때는 반드시 `runtime.CanEditTimerInterval`을 주입해야 한다(`AppRuntime.cs:55,180-186`). |
| 4.1 앱 안에서 개별 기록 확인 불가 | **잔존** | `BreakReviewView.Refresh()`는 7개 `BreakDay`의 날짜·횟수·합계만 만들고 `Review.Entries`를 상세 UI에 쓰지 않는다(`BreakReviewWindow.cs:74-90`). | UI-14 구현 필요. |
| 5.1 경고가 접힌 본문 아래에 숨음 | **해소(코드·자동 검사)** | 본문 경고와 같은 소스에서 고정 작업 영역의 경고 개수와 **참고 사항 보기**를 만들고, 누르면 본문 경고를 화면에 가져온다(`PetPackWindow.cs:89-108,141-153`). | `settings-pet-open-minimum.png` 계열은 off-screen 렌더링이다. 최소 창의 실제 휠·키보드 스크롤과 Windows DPI는 미검증이다(`docs/validation/2026-09-17-ui-ux-phase2.md:45-52`). |
| 5.2 기본 동작에 조용히 덮어쓰기 | **해소(코드·자동 검사)** | 일반 만들기 화면은 각 행동 카드의 파일 선택을 해당 key에 직접 연결하고(`CustomPetWindow.cs:69-116`), 기존 파일이 있으면 이름을 명시한 교체 확인을 거친다(`CustomPetWindow.cs:208-230`). 초기 파일 배정 경로도 성공 뒤 다음 빈 동작으로 이동한다(`CustomPetWindow.cs:197-205,232-242`). | 실제 파일 선택기에서 연속 가져오기·취소·교체를 포인터와 키보드로 수행한 증거는 없다(`docs/validation/2026-09-17-pet-builder-layout.md:57-61`). |

### 재발 방지 제안

- **P1 · 제거된 루틴/프로필 UI의 잠재 계약 표시**: `PersonalizationWindow.cs`의 독립 실행과 런타임 연결 실행을 타입 또는 필수 콜백으로 구분해, 재노출 시 `canApplyProfile` 누락을 컴파일/테스트에서 드러낸다. 소유 파일: `src/Unfold.Desktop/PersonalizationWindow.cs`, `Tests/Unfold.Tests/PersonalizationWindowTests.cs`. 의존성: 루틴·프로필 기능 재도입에 대한 제품 승인. 검증 명령: `dotnet test Unfold.slnx -c Release --no-restore --filter FullyQualifiedName~PersonalizationWindowTests`. 현재 MVP에서는 진입점을 다시 추가하지 않는다(`docs/mvp.md:53-59`).

---

## 2. UI-14 확정 구현 사양 — 날짜 행에서 개별 휴식 보기

### 2.1 저장 계약 판정

UI-14는 **Core 저장 형식 변경 없이 구현 가능**하다.

| 화면 데이터 | 현재 저장 필드 | 판정 |
|---|---|---|
| 루틴명 | `CompletedBreak.RoutineName`; 새 완료 시 `session.Routine.Name` 스냅샷 저장(`BreakHistory.cs:5-6,34-39`) | 새 기록은 충족. 구형 기록은 null 가능하므로 `RoutineId`를 대체 표기로 쓴다. 이는 CSV 계약과 같다(`docs/personalization.md:33-36`). |
| 완료 시각 | `CompletedBreak.CompletedAt`; `AppRuntime.FinishBreak()`가 `DateTimeOffset.Now`로 추가(`BreakHistory.cs:5-6`; `AppRuntime.cs:326-339`) | 충족. 저장된 UTC offset을 유지해 그날의 `HH:mm`으로 표시한다. |
| 실제 휴식 시간 | `CompletedBreak.ActualSeconds`; 새 완료 시 `Ceiling(session.Elapsed.TotalSeconds)` 저장(`BreakHistory.cs:5-9,34-39`) | 새 기록은 충족. 0초 조기 완료와 +60분 상한도 유효 범위다(`BreakHistory.cs:64-70`; `PetReminderTests.cs:10-24,28-38`). |
| 날짜별 상세 목록 | `BreakReview.Entries`; 주간 범위의 개별 항목을 완료 시각 순으로 반환(`BreakHistory.cs:52-62`) | 충족. Desktop이 아직 렌더링하지 않을 뿐이다. |

#### 마이그레이션 영향

- `StoredHistory.Version`은 1을 유지한다. 선택 필드 추가 방식이어서 기존 version 1 파일을 계속 읽는다(`BreakHistory.cs:14,20-31,50-51`; `docs/personalization.md:52-53`).
- 구형 기록의 `ActualSeconds == null`은 복원 불가능하다. `RecordedSeconds`는 집계를 위해 계획 시간으로 대체하지만(`BreakHistory.cs:8,45-49`), UI에서 이를 **실제 휴식**이라고 표기하면 사실과 다르다. 상세 행에는 **실제 시간 미기록 · 당시 목표 N분 N초**로 표시한다.
- 구형 `RoutineName == null`이면 `RoutineId`를 표시한다. 현재 루틴 사전에서 이름을 다시 찾지 않는다. 나중의 이름 변경/삭제로 과거 표시가 달라지지 않아야 하기 때문이다(`docs/personalization.md:34,40-41`).
- 따라서 UI-14 자체의 Core 소유 변경은 **없음**이다. 만약 모든 과거 기록에 실제 시간을 강제로 요구한다면 그것은 마이그레이션으로 해결할 수 없는 데이터 부재이며 요구사항을 변경해야 한다.

### 2.2 화면 구조

현재 `Grid days`를 날짜 헤더와 상세 영역을 가진 반복 행으로 바꾼다(`BreakReviewWindow.cs:38,79-86`). 구현은 `ItemsControl` + 날짜별 `Expander` 또는 같은 의미의 명시적 토글 행을 사용한다.

```text
9월 17일 (목)                   2회   3분 20초   ▾
  잠깐의 여유        09:42 완료        실제 휴식 1분 05초
  눈 쉬어 주기       14:18 완료        실제 휴식 2분 15초
9월 16일 (수)                   0회   0분 00초
```

- 날짜 헤더의 정보는 `날짜 / 횟수 / 기록된 시간 / 펼침 상태`다. 기존 주간 합계 카드와 기간 이동은 유지한다(`BreakReviewWindow.cs:67-70,76-88`).
- 상세 한 건은 `루틴명 / 완료 시각 / 실제 휴식 시간`을 한 행에 표시한다. 프로필명·펫 이름·목표 시간은 UI-14의 필수 정보가 아니므로 기본 행에 추가하지 않는다.
- 실제 시간이 없는 구형 기록은 `실제 시간 미기록 · 당시 목표 20초`처럼 한 칸에 대체 상태를 표시한다. 0초는 누락이 아니라 유효한 `실제 휴식 0초`다(`docs/stretch-notifications.md:47-51`).
- 0건 날짜는 chevron과 토글 동작을 제공하지 않는다. `0회 · 0분 0초` 텍스트만 남겨 불필요한 7개 포커스 정지를 만들지 않는다.
- 한 번에 여러 날짜를 펼칠 수 있다. 사용자가 비교 중인 상세를 임의로 닫지 않는다. **이전/다음 7일** 이동 시 새 구간은 모두 접힌 상태로 시작하고, 같은 구간의 **새로고침**은 아직 존재하는 펼친 날짜를 유지한다.

### 2.3 정렬 순서

- 날짜 행은 **최신 날짜부터** 표시한다. 현재 `Review.Days`는 시작일부터 오름차순이므로 Desktop에서 역순으로 렌더링한다(`BreakHistory.cs:54-62`). 오늘/가장 최근 휴식을 첫 화면에서 바로 찾게 하는 결정이다.
- 날짜 안의 완료 기록도 **완료 시각 내림차순**, 같은 시각이면 `SessionId` 오름차순으로 고정한다. Core의 기본 배열은 시각 오름차순이므로 Desktop에서 해당 날짜를 그룹화하며 역정렬한다(`BreakHistory.cs:55-56`).
- CSV와 `BreakReview.Entries`의 기존 정렬은 바꾸지 않는다. UI 정렬만 바꿔 저장·내보내기 회귀 범위를 줄인다.

### 2.4 상호작용과 키보드

- 포인터: 기록이 있는 날짜 헤더의 전체 영역을 클릭하면 펼침/접힘을 전환한다. 작은 chevron만 클릭 대상으로 만들지 않는다.
- Tab/Shift+Tab: 기록이 있는 날짜 헤더, 이전/다음 7일, 새로고침, CSV 내보내기 순으로 이동한다. 상세 텍스트 자체는 읽기 전용이며 별도 Tab 정지를 만들지 않는다.
- Enter/Space: 포커스된 날짜를 펼치거나 접는다.
- Left/Right: Left는 접고 Right는 펼친다. 이미 원하는 상태면 변화가 없다.
- Up/Down: 날짜 헤더 사이를 최신→과거 / 과거→최신으로 이동한다. 0건 날짜는 건너뛴다.
- 펼친 상세가 추가돼도 포커스는 날짜 헤더에 남는다. 접을 때 숨겨진 자식으로 포커스가 유실되는 경우가 없어야 한다.
- 자동화 이름 예: `9월 17일 목요일, 휴식 2회, 실제 휴식 3분 20초, 접힘`. 펼친 상태 변경을 접근성 상태로 노출한다. 실제 VoiceOver/Narrator 동작 확인은 워커 D 사양과 target-OS QA가 필요하다.

### 2.5 빈 상태

- **현재 7일이 비었을 때**: 7개 0행 표 대신 카드 안에 `이 7일 동안 완료한 휴식이 없어요.`와 `첫 휴식은 언제든 괜찮아요.`를 표시하고 **타이머로 돌아가기**를 제공한다. 현재 하단 문구의 비압박 톤을 유지한다(`BreakReviewWindow.cs:88`; `SettingsWindow.cs:155-157`).
- **과거 7일이 비었을 때**: `이 기간에는 완료한 휴식이 없어요.`만 표시한다. 과거 구간에서 새 휴식을 유도하는 버튼은 문맥이 맞지 않으므로 제공하지 않는다.
- 빈 상태에서도 이전/다음 7일과 CSV 내보내기는 유지한다. 빈 CSV도 헤더만 내보낼 수 있는 현재 계약을 보존한다(`docs/personalization.md:28-42`).

### 2.6 소유권·의존성·우선순위

- **우선순위: P0 (4단계 UI-14)**
- 소유 파일: `src/Unfold.Desktop/BreakReviewWindow.cs`.
- 테스트 소유 파일: `Tests/Unfold.Tests/PersonalizationWindowTests.cs`, `Tests/Unfold.Tests/LocalizationTests.cs`; 필요하면 상세 데이터 픽스처만 `Tests/Unfold.Tests/PersonalizationTests.cs`에 추가한다.
- Core 의존성: 읽기 전용으로 `BreakReview.Entries`와 `CompletedBreak`를 사용. `src/Unfold.Core/BreakHistory.cs` 변경 없음.
- UI 의존성: 디자인 토큰·상태 아이콘은 워커 A/B 결과를 적용하되 정보 순서·문구·키보드 계약은 이 사양을 유지한다.

### 2.7 수용 기준과 테스트 이름

1. `ReviewDateRowExpandsToShowRoutineCompletionTimeAndActualDuration`
   - 기록이 있는 날짜를 Enter로 펼치면 루틴명, 저장된 offset의 완료 시각, `ActualSeconds`가 보인다.
2. `ReviewDateRowShowsLegacyPlannedDurationWithoutCallingItActual`
   - `RoutineName`/`ActualSeconds`가 없는 version 1 기록은 routine ID와 `실제 시간 미기록 · 당시 목표 …`를 보인다.
3. `ReviewDateRowsSortNewestFirstAndEntriesLatestFirst`
   - 날짜와 같은 날짜의 항목이 각각 내림차순이며 동일 시각 tie-break가 안정적이다.
4. `ReviewEmptyCurrentWeekOffersReturnToTimerWithoutHidingPeriodNavigation`
   - 현재 구간 빈 상태에 **타이머로 돌아가기**가 있고 이전 구간/CSV 동작은 남는다.
5. `ReviewEmptyPastWeekDoesNotOfferStartAction`
   - 과거 빈 구간에는 현재 행동 CTA가 없다.
6. `ReviewExpansionPersistsOnRefreshAndResetsAcrossPeriods`
   - 같은 구간 새로고침은 펼침을 보존하고 7일 이동은 접힌 상태로 시작한다.
7. `ReviewDateHeadersToggleWithEnterSpaceAndArrowKeys`
   - 키보드 계약과 0건 날짜 건너뛰기를 검증한다.
8. `ReviewDetailKeepsKoreanWeekdayAndLongRoutineNameWithinMinimumWindow`
   - 560×600 독립 창과 860×680 설정 탭에서 한글 요일·긴 이름이 겹치지 않고 본문 스크롤로 접근된다.

검증 명령:

```sh
dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~PersonalizationWindowTests|FullyQualifiedName~LocalizationTests|FullyQualifiedName~PersonalizationTests"
dotnet test Unfold.slnx -c Release --no-restore
dotnet build src/Unfold.Desktop -c Release --no-restore
git diff --check
```

자동 스모크는 새 `UNFOLD_DATA_DIR`에서 `weekly-review-collapsed.png`, `weekly-review-expanded.png`, `weekly-review-empty-current.png`, `weekly-review-empty-past.png`, `settings-review-expanded-minimum.png`를 남긴다. 이 캡처는 렌더링·배치 근거이지 실제 키보드·VoiceOver·Narrator 증거가 아니다.

---

## 3. 빈 상태와 첫 실행 경험

### 3.1 펫이 없을 때

현재 정상 설치에는 내장 펫이 최소 1개 필요하며, 없으면 시작 중 오류로 처리한다(`AppRuntime.cs:128-138`). 따라서 두 상태를 섞지 않는다.

- **사용자 추가 펫 0개(정상 첫 실행)**: 홈에는 내장 Mochi를 선택 상태로 보여 주고, 펫 탭에는 `Mochi와 먼저 시작해도 좋아요.` 다음에 **펫 팩 열기**와 **펫 팩 만들기** 두 선택을 병렬로 제시한다. 사용자는 펫 추가 없이도 타이머를 시작할 수 있어야 한다(`docs/mvp.md:41-47`).
- **사용 가능한 펫 전체 0개(설치 손상)**: 일반 빈 상태가 아니라 복구 오류다. `기본 펫 파일을 찾지 못했어요. Unfold를 다시 설치해 주세요.`를 설정 본문에 유지하고, 펫 만들기를 필수 복구처럼 제시하지 않는다. 현재 런타임도 같은 오류를 발생시킨다(`AppRuntime.cs:128-138`).
- **P1**. 소유 파일: `SettingsWindow.cs`, `SettingsWindow.Layout.cs`, `PetManagementView.cs`. 의존성: 내장 펫 로드 결과와 오류 전달. 검증 명령: `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~CustomPetTests"`.

### 3.2 기록이 없을 때

- 현재 홈의 `첫 휴식은 언제든 괜찮아요.`와 회고의 `채워야 할 목표는 없으니 편하게 시작하세요.`는 압박하지 않는 좋은 톤이다(`SettingsWindow.cs:155-157`; `BreakReviewWindow.cs:87-90`).
- 회고 카드 내부에 현재/과거 구간을 구분한 빈 상태를 두고, 현재 구간에서만 **타이머로 돌아가기**를 제공한다. 버튼은 `SettingsWindow.OpenDashboard()`를 호출한 뒤 타이머 재생/일시정지 버튼으로 포커스를 이동하는 기존 복귀 계약을 재사용한다(`SettingsWindow.Layout.cs:154-159`).
- **P0(UI-14에 포함)**. 소유 파일: `BreakReviewWindow.cs`, `SettingsWindow.Layout.cs`(탭 복귀 콜백 전달). 의존성: `BreakReviewView`에 선택적 `returnToTimer` 콜백. 검증 명령: UI-14 집중 명령과 동일.

### 3.3 루틴이 기본값뿐일 때

- 현재 Core에는 3개 내장 루틴이 있지만 일반 UI에서는 선택·생성·편집을 제공하지 않으며, 사용자 루틴 0개는 의도된 MVP 상태다(`BreakSession.cs:17-42`; `docs/mvp.md:34-35,53-59`). 따라서 `새 루틴 만들기` CTA를 추가하면 현재 제품 범위를 우회한다.
- 첫 실행에서는 `잠깐의 여유로 시작해요. 휴식 시간에 맞춰 안내가 이어져요.`라고 현재 동작을 설명하고, 다음 행동은 **시간 설정** 또는 타이머 시작으로 유도한다. 루틴 라이브러리가 비었다는 오류처럼 표현하지 않는다.
- 루틴 관리가 다시 승인되면 별도 **휴식** 탭에서 `기본 루틴으로 먼저 시작`과 `내 루틴 만들기`를 선택하게 하고, 홈 카드에 편집 바로가기를 중복 배치하지 않는다. 과거 1.1의 재발을 막기 위해 편집은 목록의 명시적 선택 항목만 대상으로 한다(`_workspace/09_ux_flow_audit.md` 문제 1.1; `PersonalizationWindow.cs:39-50,120-127`).
- **P2 · 제품 범위 승인 전 구현 금지**. 소유 파일: `SettingsWindow.Layout.cs`, `PersonalizationWindow.cs`, `AppRuntime.cs`; 의존성: `docs/mvp.md` 범위 변경 승인. 검증 명령: `dotnet test Unfold.slnx -c Release --no-restore --filter FullyQualifiedName~PersonalizationWindowTests`.

---

## 4. 말풍선 흐름과 콘텐츠 기반 최소 높이

### 4.1 현재 정보 구조

| 상태 | 현재 제목/내용 | 현재 행동 | 근거 |
|---|---|---|---|
| 5분 전 안내 | `5분 뒤에 스트레칭해요` + 마무리 안내 | 없음 | `PetSpeechBubble.cs:43-57,59-67` |
| 초대 | 루틴명·목표 시간 + 준비 안내 | **N분 뒤에 / 휴식 시작** | `PetSpeechBubble.cs:46,53-54,59-63` |
| 휴식 진행 | 현재 단계 + 남은 시간 | **완료** | `PetSpeechBubble.cs:47,55,60-66` |
| 초과 | `조금 더 쉬어도 좋아요` + `+mm:ss` | **완료** | `PetSpeechBubble.cs:47,63-66`; `speech-overtime.png` |
| 완료 | 완료 시간 + 다음 휴식 안내 | 없음, 15초 뒤 사라짐 | `PetSpeechBubble.cs:48,56`; `docs/stretch-notifications.md:8-10` |
| 접힘 | 말풍선 없음, 펫 배지·트레이 상태·펼치기 복구 | 트레이 **휴식 알림 펼치기** 또는 펫 우클릭 | `PetWindow.cs:129-168`; `AppRuntime.cs:29-39,294-300,345-366` |

정보의 순서는 `상태 제목 → 지금 할 일 → 시간 → 주 행동 → 보조 안내`로 유지한다. 초대에서 **휴식 시작**을 주 행동, **N분 뒤에**를 보조 행동으로 유지하고, 휴식 중에는 **완료** 하나만 둔다. 완료·5분 전 안내처럼 행동이 없는 상태에는 버튼 자리를 예약하지 않는다.

### 4.2 과도한 여백 원인과 개선

`PetSpeechBubble`은 모든 상태에 `Width = 320; Height = 268`을 강제하고, 본문 Grid의 설명 행을 `*`로 둔다(`PetSpeechBubble.cs:20-24,33-38`). 그래서 버튼·타이머가 없는 완료 상태도 같은 높이를 차지하며 `speech-completed.png`에서 설명과 하단 힌트 사이에 큰 빈 공간이 남는다. 상/하/좌/우 창 크기와 펫 위치도 268 높이를 전제로 고정돼 있다(`PetSpeechBubble.cs:73-85`).

개선 사양:

- `Height` 고정을 제거하고 `Width = 320`, `MinHeight`만 둔다. Grid 행은 `Auto` 기반으로 바꾸며 설명은 최대 4줄 계약을 유지한다(`PetSpeechBubble.cs:20-24`). 권장 최소 높이는 176 논리 px이고, 초대/진행 내용이 필요하면 측정된 높이만큼 자연스럽게 늘어난다.
- `PetSpeechBubble.Refresh()` 뒤 `Measure(Size.Infinity)`로 `DesiredSize.Height`를 얻고, `PetBubbleLayout.Create(direction, expanded, bubbleSize)`가 그 높이로 창·꼬리·펫 위치를 계산한다. 상/하는 `bubbleHeight + 204`, 좌/우는 `max(192, bubbleHeight)`를 사용하고, 좌/우 펫은 말풍선 높이에 대해 수직 가운데 정렬한다. 현재 위치 고정값은 교체 대상이다(`PetSpeechBubble.cs:73-91`; `PetWindow.cs:129-150`).
- 상태 전환 시 펫의 화면상 anchor를 먼저 보존하고 새 크기로 다시 배치한다. 현재 `PetAnchor`와 `layout.Position()` 계약을 유지해야 말풍선 높이 변화가 펫 순간 이동으로 보이지 않는다(`PetWindow.cs:38-40,139-150`; `PetSpeechBubble.cs:87-91`).
- 완료 상태 힌트 `펫 우클릭으로 말풍선을 접을 수 있어요.`는 15초 뒤 사라질 알림에서 가치가 낮다. 완료 상태에서는 `다음 휴식 때 다시 만나요.`를 본문에 이미 포함하므로 힌트를 숨긴다(`PetSpeechBubble.cs:56,65-67`). 초대 상태의 접기 안내는 유지한다.
- 초과의 `+60분에 도달했어요. 완료를 눌러 주세요.`는 시간 상한과 다음 행동을 함께 말하므로 유지한다(`PetSpeechBubble.cs:65-66`).

### 4.3 소유권·검증

- **P1**. 소유 파일: `src/Unfold.Desktop/PetSpeechBubble.cs`, `src/Unfold.Desktop/PetWindow.cs`.
- 테스트 소유 파일: `Tests/Unfold.Tests/BreakReminderTests.cs`, `Tests/Unfold.Tests/PetReminderTests.cs`, `src/Unfold.Desktop/SmokeDiagnostics.cs`.
- 의존성: 워커 A의 spacing/radius 토큰, 워커 B의 상태별 시각 밀도 기준, 워커 D의 UI-08 키보드 진입 사양.
- 제안 테스트 이름: `SpeechBubbleMeasuresHeightFromVisibleContent`, `SpeechBubbleCompletionIsShorterThanInvitation`, `SpeechBubbleDynamicLayoutPreservesPetAnchorInEveryDirection`, `SpeechBubbleOvertimeKeepsTimerAndCompleteVisible`, `SpeechBubbleCollapsePreservesSessionAcrossDynamicHeight`.
- 검증 명령:

```sh
dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~BreakReminderTests|FullyQualifiedName~PetReminderTests"
dotnet test Unfold.slnx -c Release --no-restore
dotnet build src/Unfold.Desktop -c Release --no-restore
git diff --check
```

자동 캡처는 `speech-five-minutes.png`, `speech-top.png`, `speech-overtime.png`, `speech-completed.png`, `speech-folded.png`를 상태별로 다시 비교한다. 실제 포인터 입력, 키보드 진입, 소리 체감, Windows DPI/다중 모니터 배치는 별도 실기 확인 대상이다.

---

## 5. 설정 대시보드 정보 구조 평가와 재배치안

### 5.1 현재 목표 대응

| 사용자 목표 | 현재 시작점 | 평가 |
|---|---|---|
| 타이머 설정 | 타이머 탭의 스트레칭/휴식 시간 + 설정 탭의 자리 비움/다시 알림 | **부분 불일치**. 하나의 목표가 두 탭에 흩어져 있다(`SettingsWindow.Layout.cs:105-141,179-187`; `SettingsWindow.Notifications.cs:71-75`). |
| 루틴 관리 | 일반 UI 없음 | **불일치하나 의도된 범위 제한**. 기능을 연결하는 것은 UI 재배치가 아니라 제품 범위 변경이다(`docs/mvp.md:35,53-59`). |
| 펫 관리 | 타이머 탭의 선택/표시 + 펫 추가 탭의 열기/만들기 | **부분 불일치**. 선택과 추가가 분리돼 있고 설치된 펫의 삭제/편집은 MVP에 없다(`SettingsWindow.Layout.cs:67-82,172-176`; `docs/mvp.md:45-47,53-59`). |
| 기록 확인 | 기록 탭 + 홈의 오늘 요약 | **대체로 일치**. 홈 요약이 기록 탭으로 연결되고 같은 창에서 열린다(`SettingsWindow.Layout.cs:144-170`). UI-14 상세만 부족하다. |

아이콘만 있는 64px 내비게이션은 자동화 이름과 툴팁을 제공하지만, 네 목표를 처음 배우는 사용자는 hover/스크린리더 전까지 의미를 읽기 어렵다(`SettingsWindow.Layout.cs:40-64,217-224`). 이 시각 표현 변경은 워커 A/B와의 접점이다.

### 5.2 목표 기반 재배치안

현재 MVP 범위를 유지하는 즉시안:

1. **타이머** — 남은 시간, 상태, 재생/일시정지/중지, 스트레칭 시간, 휴식 시간, 자리 비움 시간을 한 페이지에 둔다. 모두 작업 주기 자체를 바꾸는 값이다.
2. **알림** — 현재 `설정` 탭을 `알림`으로 구체화하고 말풍선 위치, 다시 알림 시간, 스트레칭/완료 효과음을 둔다. `다시 알림`은 초대의 **N분 뒤에** 행동과 직접 연결된다(`PetSpeechBubble.cs:27-29,62`; `SettingsWindow.Notifications.cs:13-75`).
3. **기록** — 오늘 요약과 UI-14 주간 상세를 같은 목표 아래 둔다. 홈의 오늘 카드는 짧은 진입점으로 유지한다.
4. **펫** — 홈의 펫 선택/표시와 현재 펫 추가의 열기/만들기를 한 목표로 묶는다. 로그인 시 자동 실행은 펫 그룹에서 떼어 앱 보조 설정으로 둔다(`SettingsWindow.Layout.cs:67-82`).
5. **루틴** — 현재는 내비게이션에 추가하지 않는다. 내장 안내가 자동으로 실행됨을 타이머/초대에서 설명한다. 사용자 루틴 관리가 다시 승인될 때만 **휴식** 탭으로 추가하고, 목록 선택→사용/편집의 한 경로로 만든다.

우선순위와 소유권:

- **P1 · 타이머/알림 그룹 재배치**. 소유 파일: `SettingsWindow.Layout.cs`, `SettingsWindow.Notifications.cs`, `SettingsWindow.cs`. 의존성: 워커 A/B의 내비게이션 라벨 표현과 최소 폭. 검증 명령: `dotnet test Unfold.slnx -c Release --no-restore --filter FullyQualifiedName~SettingsDashboardTests`.
- **P1 · 펫 선택과 추가의 목표 통합**. 소유 파일: `SettingsWindow.Layout.cs`, `PetManagementView.cs`, `PetPackWindow.cs`, `CustomPetWindow.cs`. 의존성: 현재 초안 보존 계약(`SettingsDashboardTests.cs:155-184`). 검증 명령: `dotnet test Unfold.slnx -c Release --no-restore --filter "FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~PetPackWindowTests|FullyQualifiedName~CustomPetTests"`.
- **P2 · 루틴 관리 재도입**. 소유 파일: `SettingsWindow.Layout.cs`, `PersonalizationWindow.cs`, `AppRuntime.cs`. 의존성: `docs/mvp.md` 제품 범위 변경 승인과 기존 데이터 UX 결정. 검증 명령: 개인화 집중 테스트 + 전체 테스트. UI-14와 묶어 구현하지 않는다.

---

## 6. 단계별 구현 큐

| 우선순위 | 제안 | 소유 파일 | 선행 의존성 | 완료 증거 |
|---|---|---|---|---|
| **P0** | UI-14 날짜 행 상세·현재/과거 빈 상태 | `BreakReviewWindow.cs`; 관련 테스트 2~3개 | 워커 A/B의 상세 행 토큰만 반영 | 8개 명명 테스트, 최소 창 캡처, 전체 테스트/빌드 |
| **P1** | 말풍선 콘텐츠 기반 최소 높이 | `PetSpeechBubble.cs`, `PetWindow.cs`; 관련 테스트/스모크 | A/B 시각 밀도, D UI-08 접점 | 5개 상태 높이/anchor 회귀, 5개 캡처 |
| **P1** | 타이머·알림·펫 그룹 목표 재정렬 | `SettingsWindow*.cs`, `PetManagementView.cs` 및 기존 펫 뷰 | A/B 내비게이션·최소 폭 결정 | 목표별 진입 테스트, 초안 보존, 860×680 캡처 |
| **P1** | 재노출 시 프로필 적용 사전 차단 계약 | `PersonalizationWindow.cs` | 루틴/프로필 재도입 승인 시에만 | 런타임 콜백 누락 방지 테스트 |
| **P2** | 사용자 루틴 관리 재도입 | 개인화/설정/AppRuntime | 명시적 MVP 범위 변경 | 별도 사양·구현·실기 검증 |

---

## 7. 자동 확인과 실제 OS 미검증

### 이번 감사에서 확인한 것

- 현재 코드와 문서를 정적으로 교차 확인했다. 2.1·5.1·5.2의 수정 계약과 4.1의 잔존은 위 파일·줄 근거와 일치한다.
- 새 격리 `UNFOLD_DATA_DIR`에서 관련 회귀(`SettingsDashboardTests`, `PersonalizationWindowTests`, `BreakReminderTests`, `PetPackWindowTests`, `CustomPetTests`)를 실행해 **26개 통과, 실패 0, 건너뜀 0**을 확인했다. 이는 자동 검사 결과이며 실제 OS 입력 증거는 아니다.
- Phase 2 기록은 집중 회귀 36개, 전체 175개, Release 빌드와 `git diff --check` 통과를 보고한다(`docs/validation/2026-09-17-ui-ux-phase2.md:19-27`).
- `/tmp/unfold-phase2-smoke-final.j5Uen8/verification/`의 `speech-top.png`, `speech-completed.png`, `speech-overtime.png`, `speech-folded.png`, `settings-pet-open-minimum.png`, `settings-review-tab.png`, `weekly-review.png`를 비교했다. 특히 `speech-completed.png`는 고정 높이의 불필요한 여백을, `weekly-review.png`는 날짜 합계만 있는 현 상태를 보여 준다.
- 스모크의 `success: true`와 PNG 42장은 off-screen macOS Avalonia 렌더링 및 프로그램 방식 입력 결과다(`docs/validation/2026-09-17-ui-ux-phase2.md:29-46`). 사람의 조작 성공으로 해석하지 않는다.

### 미검증으로 남기는 것

- 실제 macOS 마우스·트랙패드로 날짜 펼침, 펫 경고 복구, 동적 말풍선 드래그/화면 끝 배치.
- 실제 키보드만으로 날짜 행 펼침·접힘과 말풍선 진입·행동 실행.
- VoiceOver/Narrator의 펼침 상태·상세 읽기 순서·펫 배지/트레이 메뉴 명명.
- Windows 100%/125%/150%/200% DPI, 다중 모니터와 작업 표시줄 위치에서 동적 말풍선 clamp 및 투명 창 클릭 통과.
- 실제 알림/완료 WAV의 인지 품질, 장시간 사용에서 초대·접힘·초과 흐름의 피로도.

---

## 8. 접점

### 워커 A · 디자인 시스템

- UI-14 날짜 헤더의 펼침 상태, 상세 행의 3단 정보, 빈 상태 CTA에 필요한 spacing/typography/state 토큰은 A가 결정한다. 정보 순서와 구형 기록 표기는 이 보고서의 계약을 유지한다(`BreakReviewWindow.cs:35-39,67-90`).
- 말풍선의 최소 높이 176px은 정보 구조 기준의 하한이다. 실제 패딩·행 간격으로 더 커질 수 있으나 완료/5분 전 상태가 다시 268px 고정 높이가 되어서는 안 된다(`PetSpeechBubble.cs:20-38`; `speech-completed.png`).

### 워커 B · 시각 격차

- 아이콘 전용 사이드바의 학습 가능성, 날짜 행 chevron/hover/focus/expanded 시각 상태, 펫 배지 대비는 B의 시각 변경 목록과 합쳐야 한다(`SettingsWindow.Layout.cs:46-64,217-224`; `PetWindow.cs:153-168`).
- 펫 팩 경고는 코드상 해소됐지만 `settings-pet-open-minimum.png`에서 고정 요약의 시각 우선순위가 설치 버튼에 묻히지 않는지 B가 판단한다(`PetPackWindow.cs:89-108`).

### 워커 D · 접근성 구현 사양, UI-08

말풍선 키보드 진입(UI-08)의 **구현 사양은 워커 D에 위임**한다. 이 보고서는 다음 흐름 요구사항만 제공한다.

- 초대가 나타나도 현재 앱의 작업 포커스를 강제로 빼앗지 않는다. 사용자가 명시적으로 진입하면 초대에서는 **휴식 시작**, 진행 중에는 **완료**가 첫 주 행동으로 식별돼야 한다(`PetSpeechBubble.cs:27-32,59-67`; `PetWindow.cs:56-59`).
- **N분 뒤에**는 보조 행동이며, 실행 후 포커스가 사라진 말풍선에 남지 않아야 한다. 세션은 Snoozed로 끝나고 작업 타이머가 설정 시간 뒤 다시 예약된다(`AppRuntime.cs:286-288,326-343`).
- 접힌 대기/휴식에는 트레이의 **휴식 알림 펼치기**라는 복구 경로가 있고, 펼친 뒤 기존 세션·경과 시간을 보존해야 한다(`AppRuntime.cs:294-300,345-366`).
- Esc/창 닫기를 완료나 미루기로 오인하지 않는다. 중지·완료·미루기는 각각 명시적 사용자 행동으로만 발생해야 한다(`docs/stretch-notifications.md:8-12`).
- Advance/Completed처럼 행동 없는 일시 상태는 키보드 포커스 대상으로 만들지 않는다. UI-08의 실제 focus route, shortcut, automation peer, VoiceOver/Narrator 수용 기준과 구현 파일은 D가 확정한다.

### 제품 범위 접점

- 요청된 사용자 목표 중 `루틴 관리`는 현재 MVP에서 명시적으로 제외되어 있다(`docs/mvp.md:53-59`). 이번 UX 보고서는 그 불일치를 숨기지 않되, UI-14 구현 승인으로 루틴 생성·편집 진입점까지 승인된 것으로 해석하지 않는다.
- UI-14는 서버·계정·클라우드·분석 SDK 없이 현재 로컬 `BreakHistory`만 사용한다(`docs/mvp.md:39-40,53-59`).
