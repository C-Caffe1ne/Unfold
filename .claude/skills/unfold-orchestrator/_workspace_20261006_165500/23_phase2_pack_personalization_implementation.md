# 구현 보고서 — Phase 2C (UI-06, UI-12, UI-13)

2026년 09월 17일 · `release/mvp` 작업 트리 · `implementation-engineer` / `unfold-implementation`

## 원인

**UI-06.** `PetPackView`의 경고 문구(`warnings`)는 본문 `Ui.Column`의 마지막 요소였고, `install`만
`PageContent`의 고정 `PageActions`에 있었다. 최소 창 480×560에서 본문은 스크롤되므로 경고는
접힌 영역 아래에 남고 설치 버튼만 항상 보였다. 또 경고 상태를 `warnings.Text`/`IsVisible` 두 줄로
직접 조작해 리셋 지점이 `ReadPack` 한 곳에만 있었고, `catch` 경로에는 없었다.

**UI-12.** `BuildRoutineCard`는 `Ui.Column(제목, routines, activeProfile, actions)` 순서로 쌓고
`activeProfile`에 `Muted` 11px를 적용했다. 루틴 선택기 바로 아래의 흐린 작은 글씨는 선택한 루틴의
설명으로 읽혔다. 값만 있고 무엇의 상태인지 알리는 제목이 없었다.

**UI-13.** 라이브러리 하단은 `Ui.Actions(새 X, X 편집, X 사용/적용, X 삭제)`(오른쪽 정렬 `WrapPanel`)와
`Ui.Actions(닫기)`를 `Ui.Column`으로 세로로 쌓았다. 즉 **닫기는 항상 별도 줄**이었고, 600px 최소 폭에서
위 줄 4개와 아래 줄 1개의 비대칭이 고정되어 있었다.

## 변경

### UI-06 — 고정 영역의 경고 요약과 본문 이동

- `PackWarningSummary` 텍스트와 `ReviewPackWarnings` 버튼을 `warningRow`(Grid `*,8,Auto`)에 배치하고,
  이를 `PackWarningBar`(`ContentControl`)를 통해 `PageActions` 푸터에 넣었다.
  푸터 순서는 `status → PackWarningBar → Ui.Actions(install)`로, 설치 버튼의 고정 위치는 그대로다.
- 요약 문구: `확인할 참고 사항 {N}개 · 설치 전에 내용을 확인해 주세요.` (`DesignSystem.Warning`, Caption 크기).
  개수와 확인 필요성을 스크롤 없이 보여 준다. 본문 제목 `미리보기 참고 사항`과 용어를 맞췄다.
- `ReviewPackWarnings`는 `warnings.BringIntoView()`로 본문 경고가 보이는 위치까지 스크롤한다.
- 경고 상태를 `ShowWarnings(IReadOnlyList<string>)` 한 곳으로 모았다. 생성 시, 새 팩을 읽기 시작할 때,
  검사 결과 반영 시, **팩 열기 실패 `catch`에서** 모두 같은 경로를 쓰므로 이전 팩의 요약·본문 경고가 남지 않는다.
- 경고가 없을 때 `PackWarningBar.Content = null`로 두어 접힌 버튼이 `PageActions`에 남지 않는다.
  `DesignSystemTests.PageActionsRemainVisibleAtMinimumSizeAndWhileTheBodyScrolls`가 고정 영역의
  모든 버튼 크기를 검사하므로, `IsVisible=false`만으로는 0×0 버튼이 이 계약을 깼다(실제로 검출되어 수정).

### UI-12 — 현재 알림 설정 상태 그룹

- `activeProfile`을 `SettingsRoutineStatus`(`Border`, `Raised` 배경, 반경 16, 패딩 14/10) 안으로 옮기고
  `현재 알림 설정` 제목(`Muted` 11px)과 값(`Cream` 13px)을 함께 보여 준다.
  카드 배경 `Surface`와 다른 표면색으로 선택기·동작 버튼과 시각적으로 분리된다.
  제목 문구는 감독자 지시(`msg_a6c709b8a8f7`)에 따라 `현재 상태` → `현재 알림 설정`으로 확정했다.
- 카드 본문 순서는 `제목 → routines → SettingsRoutineStatus → actions`. 값 텍스트 이름은
  `SettingsActiveProfile`로 지정했다(필드 선언은 소유 밖 `SettingsWindow.cs`이므로 레이아웃에서 설정).
- `SettingsWindow.cs`의 `activeProfile.Text` 갱신 계약(`직접 설정한 알림` / `업무 프로필 · {이름}`)은 그대로다.

### UI-13 — 주 작업 / 관리 / 닫기 그룹

- 하단을 `LibraryActions`(Grid `Auto,*,Auto`) 한 줄로 재배치했다.
  - 왼쪽 **관리** `LibraryManageActions`: `새 X`, `X 편집`, `X 삭제`
  - 오른쪽 **주 작업 + 닫기**: `LibraryPrimaryAction`(`루틴 사용` / `프로필 적용`) + `닫기`(`Quiet`, `IsCancel`)
- 탭 전환은 관리 그룹과 주 작업 버튼만 교체한다. 그룹 구조(왼쪽 3개 + 오른쪽 주 작업·닫기)는 탭과 무관하게 같다.
- 버튼 이름, `IsEnabled` 계산(`RefreshActions`), 저장·삭제·적용 동작과 `Ui.Primary`/`Ui.Danger` 역할은 바꾸지 않았다.
  `닫기`의 `IsCancel`(제목 표시줄 닫기 == 취소)도 유지했다.

## 소유 파일

| 파일 | 변경 |
|---|---|
| `src/Unfold.Desktop/PetPackWindow.cs` | UI-06 고정 경고 요약, 본문 이동, 상태 리셋 단일화 |
| `src/Unfold.Desktop/SettingsWindow.Layout.cs` | UI-12 `SettingsRoutineStatus` 그룹 |
| `src/Unfold.Desktop/PersonalizationWindow.cs` | UI-13 하단 그룹 재배치 |
| `Tests/Unfold.Tests/PetPackWindowTests.cs` | 경고 팩 생성 헬퍼 + UI-06 회귀 검사, 기존 미리보기 검사에 기하 진단 메시지 추가 |
| `Tests/Unfold.Tests/SettingsDashboardTests.cs` | UI-12 회귀 검사 |
| `Tests/Unfold.Tests/PersonalizationWindowTests.cs` | UI-13 회귀 검사 |

소유 밖 파일은 변경하지 않았다. 같은 트리의 다른 워커 변경(`Ui.cs`, `AppRuntime.cs`, `PetWindow.cs`,
`SmokeDiagnostics.cs`, `DesignSystemTests.cs`, `CustomPetTests.cs`, `BreakReminderTests.cs`)은 보존했다.

## 검증

### 추가한 회귀 검사

1. `PetPackWindowTests.PinnedWarningSummaryCountsWarningsAndScrollsToTheBodyAtTheMinimumWindow`
   - 새 헬퍼 `CreateWarningPack`이 one-shot 반응(`stretch`/`click`/`celebrate`)을 뺀 유효한 팩을 만들어
     `CharacterAssetAudit` 경고 3개를 실제로 발생시킨다(`pack.Audit.Warnings.Count == 3`로 근거 고정).
   - 480×560에서 요약·`참고 사항 보기`·`설치`가 모두 창 안에 있고, 본문 경고는 뷰포트 아래에 있음을 확인한다.
   - `참고 사항 보기` 실행 후 `PageBodyScroll.Offset.Y > 0`이고 본문 경고가 뷰포트 안에 들어옴을 좌표로 확인한다.
   - 경고 없는 팩 → 요약·본문 모두 비고 `Content`가 `null`, 경고 팩 → 다시 표시, 깨진 팩 → 다시 비워짐을 순서대로 확인한다.
   - 고정 영역에 접힌 `ReviewPackWarnings` 버튼이 남지 않음을 확인한다.
2. `SettingsDashboardTests.RoutineCardKeepsTheCurrentStateOutOfThePickerDescription`
   - `SettingsActiveProfile`이 `SettingsRoutineStatus` 안에 있고 `현재 알림 설정` 제목이 함께 있음,
     카드와 그룹의 배경이 다름, 그룹이 선택기 아래·동작 버튼 위에 있고 카드 안쪽으로 들여쓰였음을 좌표로 확인한다.
   - 프로필 적용 후 값이 `업무 프로필 · 집중 작업`으로 바뀌어도 그룹 안에 남는 것을 확인한다.
3. `PersonalizationWindowTests.LibraryFooterGroupsActionsAndKeepsCloseOnThePrimaryRow`
   - 600×580(최소 크기)에서 탭 0 → 1 → 0을 돌며 관리 3개 + 주 작업 + `닫기` 5개가 **같은 Y 좌표 한 줄**에 있고
     모두 창 안에 들어오는지 확인한다(`Distinct()` 행 수 1).
   - 주 작업이 관리 그룹 오른쪽, `닫기`가 주 작업 오른쪽임을 확인해 `닫기`가 다시 단독 줄로 떨어지면 실패한다.
   - 탭을 바꿔도 관리 그룹의 버튼 수가 3개로 유지되는지 확인한다.

### 실행한 명령과 결과

```
dotnet build src/Unfold.Desktop -c Release --no-restore
→ Build succeeded. 0 Warning(s) 0 Error(s)

dotnet test Tests/Unfold.Tests -c Release --no-restore \
  --filter "FullyQualifiedName~PersonalizationWindowTests|FullyQualifiedName~PetPackWindowTests|FullyQualifiedName~SettingsDashboardTests|FullyQualifiedName~DesignSystemTests"
→ Failed: 1, Passed: 30, Total: 31

dotnet test Unfold.slnx -c Release --no-restore
→ Failed: 1, Passed: 174, Total: 175
```

세 항목의 새 검사와 기존 UI-01~UI-03, 고정 작업 영역, 초안 보존, 가로 넘침 검사는 모두 통과한다.

### 범위 밖 실패 1건 (임의 수정하지 않음)

`PetPackWindowTests.EnlargedLightPreviewFitsTheMinimumWidthAndDoesNotInstall`이 실패한다.

```
Preview right 379, surface width 374, scroll viewport 374, client width 480
```

- 원인은 UI-04 담당자의 `Ui.cs` `PageBodyScroll` 변경(`AllowAutoHide = false` + `ScrollGutter` 8px 마진)이다.
  스크롤바 트랙이 레이아웃에 예약되어 480px 최소 폭에서 본문 뷰포트가 424 → 374로 줄고,
  200% 미리보기(384px)가 `PackPreviewSurface`를 넘는다.
- **격리 근거:** 현재 작업 트리를 스크래치로 복사한 뒤 `Ui.cs`, `DesignSystemTests.cs`, `CustomPetTests.cs`만
  HEAD 버전으로 되돌리고 이 Task의 변경(UI-06/12/13)은 전부 유지한 상태에서 같은 테스트가 통과했다.
  따라서 이 실패는 이번 구현이 만든 것이 아니다.
- `Ui.cs`는 이 Task의 금지 범위이고 "기존 미리보기 계약을 유지한다"는 요구가 있어 미리보기 크기 옵션도 줄이지 않았다.
  감독자에게 escalation(`msg_b3fadb68acf2`)으로 보고했다. UI-04 담당자의 거터 폭 또는 미리보기 크기 계약 중
  어느 쪽을 조정할지 결정이 필요하다.
- 실패 원인을 다음 실행에서 바로 읽을 수 있도록 해당 단정에 기하 수치(미리보기 오른쪽, 표면 폭, 뷰포트, 클라이언트 폭)
  진단 메시지를 추가했다. 단정 조건 자체는 바꾸지 않았다.

## 미검증·위험

- **실제 OS 조작 미검증.** 모든 근거는 헤드리스 Avalonia 레이아웃 좌표와 자동 검사다. macOS·Windows에서
  마우스·키보드로 `참고 사항 보기`를 눌러 실제 스크롤 애니메이션과 시선 이동이 자연스러운지, 상태 그룹의
  `Raised` 표면이 실기 대비에서 충분히 구분되는지는 확인하지 않았다.
- **Windows 배율 미검증.** UI-13 한 줄 배치는 600px 최소 폭 헤드리스 기준이다. Windows 125~200% 배율에서
  한글 글꼴 폭이 커지면 `LibraryActions`가 Grid이므로 줄바꿈 대신 가로로 눌릴 수 있다.
  실제 배율 확인이 필요하고, 넘치면 관리 그룹을 별도 줄로 내리는 대안이 있다.
- **경고 요약의 세로 공간.** 경고가 있는 팩에서는 고정 푸터가 한 줄 높아져 본문 스크롤 영역이 줄어든다.
  480×560에서 설치 버튼과 요약이 모두 창 안에 남는 것은 확인했지만, 경고 문구가 매우 길어 줄바꿈되면
  본문 영역이 더 줄어든다. 요약은 개수만 쓰므로 길이는 고정이다.
- **접근성 미구현.** UI-09~UI-11은 금지 범위이므로 상태 그룹·관리 그룹에 접근성 그룹 이름을 붙이지 않았다.
  `ReviewPackWarnings`에만 기존 패턴대로 `AutomationProperties.SetName`/`ToolTip`을 넣었다.
  VoiceOver·Narrator 읽기 순서는 확인하지 않았다.
- **펫 팩 검사·저장 형식은 변경하지 않았다.** `CharacterAssetAudit`의 경고 목록과 `pack.json` 계약을 그대로 읽는다.
- release-qa의 독립 검증이 남아 있다. 위 명령을 그대로 재실행하면 같은 결과를 재현할 수 있다.
