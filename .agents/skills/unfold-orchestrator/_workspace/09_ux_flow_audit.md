# 09. UX 흐름 감사 — 사용자 목표별 마찰·상태 피드백·오류 복구

- 날짜: 2026-09-16
- 담당: ux-flow-auditor (`.claude/agents/ux-flow-auditor.md` / `unfold-ux-flow-audit` 스킬)
- 기준: `release/mvp` 커밋 `7e81f77` + 현재 작업 트리(펫 팩 진단 회귀 수정 포함), `/tmp/unfold-ui-audit-20260916b/verification/`의 캡처 42장 + `smoke.json` + `review.csv`.
- 대상 코드: `SettingsWindow*.cs`, `PersonalizationWindow.cs`, `PetManagementView.cs`, `PetPackWindow.cs`, `CustomPetWindow.cs`, `PetSpeechBubble.cs`, `PetWindow.cs`, `AppRuntime.cs`, `Ui.cs`, `TimerControls.cs`, `RoutineEditorWindow.cs`, `ProfileEditorWindow.cs`, `BreakReviewWindow.cs`, `src/Unfold.Core/Personalization.cs`, `PetReminder.cs`, `StretchClock.cs`, `BreakHistory.cs`, `BreakSession.cs`.
- 기준 문서: `docs/mvp.md`, `docs/settings-ui.md`, `docs/stretch-notifications.md`, `docs/pet-packs.md`.

## 방법과 한계

- 코드 경로 추적(버튼 → 핸들러 → `Unfold.Core` 검증/상태 전이)과 정적 캡처를 근거로 흐름을 재구성했다. 실제 마우스·키보드 조작, macOS/Windows 실기 확인은 하지 않았다. `smoke.json`의 `*Verified: true` 항목은 자동 진단 스크립트의 통과 여부이며, 사람이 인지하는 상태 피드백 체감을 보증하지 않는다.
- `review.csv`의 `routine_name` 값 "글쓰기 휴식"과 설정 캡처(`settings-completed.png`)의 루틴 선택기에 나타난 영문 "Revised writing pause"는 자동 진단이 생성한 **사용자/테스트 데이터**로 판단한다(정상 제품 문구는 전부 한국어). 이는 로컬라이제이션 결함이 아니라 진단 픽스처의 흔적이므로 아래 문제 목록에서 제외했다.
- 문제마다 코드 근거(파일:줄)와 캡처 근거를 함께 표시했다. 근거가 코드 추론뿐이고 캡처로 직접 재현하지 못한 항목은 "코드 근거만"으로 표시했다.

## 요약 (우선순위별)

| # | 목표 | 문제 | 심각도 |
|---|---|---|---|
| 1 | 타이머 설정 | "내 루틴 편집" 버튼이 화면에 표시된 선택과 다른 루틴을 연다 | **P1** |
| 2 | 휴식 시작·완료 | 말풍선을 접은 상태에서 알림이 오면 어디에도 신호가 없어 타이머가 멈춘 이유를 알 수 없다 | **P1** |
| 3 | 루틴·프로필 관리 | "프로필 적용" 버튼이 타이머 진행 중에도 활성 상태라 클릭 후에야 실패를 알려준다 | **P1** |
| 4 | 펫 팩 열기 | 최소 창 크기에서 설치 전 확인해야 할 경고·미리보기 조작이 스크롤 아래 숨고 설치 버튼만 항상 보인다 | **P1** |
| 5 | 펫 팩 만들기 | 파일 가져오기의 기본 배정 대상이 항상 "쉬는 모습"이라 반복 가져오기 시 필수 파일을 실수로 덮어쓸 수 있다 | **P1** |
| 6 | 기록 확인·내보내기 | 개별 휴식 기록을 앱 안에서 볼 방법이 없어 세부 확인은 CSV 내보내기에 의존한다 | P2 |
| 7 | 루틴·프로필 관리 | "직접 설정한 알림/업무 프로필 · N" 캡션이 루틴 선택기 바로 아래 있어 루틴 상태처럼 오인될 수 있다 | P2 |

---

## 목표 1 — 타이머 설정 (알림 간격 · 자리 비움 · 루틴 선택)

**현재 흐름**: 설정 창 타이머 탭 → 가운데 "나의 휴식" 카드에서 루틴 드롭다운(`routines`, `SettingsWindow.Notifications.cs:17`)으로 다음 휴식에 쓸 루틴을 고른다 → 그 아래 "내 루틴 편집"/"루틴 · 프로필" 버튼(`SettingsWindow.Layout.cs:134-135`). 오른쪽 "알림 설정" 카드에서 알림 간격·자리 비움을 바꾸고 **적용**을 누른다. 타이머가 진행 중이면 간격 입력만 비활성화되고, 그 이유가 카드 안 문구(`SettingsWindow.cs:142-145`)와 툴팁으로 즉시 안내된다 — 이 부분은 잘 구현되어 있다(캡처 `settings.png`, `settings-minimum.png`).

### 문제 1.1 — "내 루틴 편집" 버튼이 화면에 보이는 선택과 무관하게 다른 루틴을 연다 (P1)

- **재현 근거(코드)**: `SettingsWindow.Layout.cs:153-154`
  ```csharp
  private Task OpenRoutine() => new RoutineEditorWindow(runtime.Settings.CustomRoutine,
      routine => runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine))).ShowDialog(this);
  ```
  같은 카드 안의 `routines` 드롭다운(`SettingsWindow.Layout.cs:138`, `SettingsWindow.cs:39-40`)은 `runtime.Settings.BreakRoutineId`로 선택된 **아무 루틴이나** 표시할 수 있다(기본 루틴, 추가 루틴 최대 19개, 또는 개인 슬롯 `CustomRoutine` 중 하나). 그런데 "내 루틴 편집" 버튼은 드롭다운의 현재 선택(`routines.SelectedItem`)을 전혀 읽지 않고 항상 `runtime.Settings.CustomRoutine`(ID `my-routine`)만 편집기로 연다.
  - 대조: `PersonalizationWindow.cs:36` `editRoutine = Action("루틴 편집", () => EditRoutine(routines.SelectedItem as BreakRoutine));` — 같은 이름의 동작이 라이브러리 화면에서는 선택 항목을 정확히 반영한다. 동일 의미의 동작이 화면마다 다른 대상을 가리키는 사례다.
- **재현 시나리오**: 사용자가 "글쓰기 휴식"(추가 루틴)을 다음 휴식으로 선택해 둔 상태에서 대시보드의 "내 루틴 편집"을 누르면, 편집기는 "글쓰기 휴식"이 아니라 사용자의 개인 슬롯("나만의 휴식" 등 `CustomRoutine.Name`)을 열어 보여준다. 사용자가 창 제목·필드 내용을 세심히 확인하지 않고 내용을 고쳐 저장하면, 의도한 "글쓰기 휴식"은 그대로 남고 전혀 다른 개인 슬롯이 조용히 바뀐다.
- **소유 파일**: `src/Unfold.Desktop/SettingsWindow.Layout.cs` (문제 지점), `src/Unfold.Desktop/PersonalizationWindow.cs` (올바른 참고 구현).
- **개선 흐름**: "내 루틴 편집"이 `routines.SelectedItem`을 편집 대상으로 넘기도록 고치거나(선택이 기본 루틴이면 버튼을 비활성화하거나 "루틴 · 프로필" 화면으로 안내), 버튼 라벨을 "내 루틴 편집" 대신 "개인 슬롯 편집" 등으로 구분해 대상이 드롭다운 선택과 무관함을 명확히 한다.
- **수용 기준(행동 기반)**: 드롭다운에서 기본 루틴이 아닌 임의의 루틴을 선택한 뒤 "내 루틴 편집"을 누르면, 열리는 편집기의 이름·단계가 방금 선택한 루틴과 일치한다. 또는 대상이 다르면 버튼을 누르기 전에 어떤 항목이 열릴지 사용자가 알 수 있다.

### 문제 1.2 — "직접 설정한 알림 / 업무 프로필 · N" 캡션이 루틴 상태처럼 보일 수 있음 (P2)

- **재현 근거**: `SettingsWindow.Layout.cs:138` `Ui.Column(Label("나의 휴식", ...), routines, activeProfile, actions)` — `activeProfile`은 실제로는 **업무 프로필** 적용 여부를 나타내는 문구(`SettingsWindow.cs:148-150`)인데, 배치상 바로 위 루틴 드롭다운에 대한 부연 설명처럼 읽힌다.
- **소유 파일**: `src/Unfold.Desktop/SettingsWindow.Layout.cs`.
- **개선 흐름**: 캡션 앞에 "적용된 프로필:" 같은 라벨을 붙이거나, 프로필 상태를 타이머 카드 쪽(이미 프로필 이름이 노출되는 곳)으로 옮겨 루틴 캡션과 시각적으로 분리한다.
- **수용 기준**: 사용자가 이 캡션을 보고 "이것이 루틴 이름의 일부"라고 오인하지 않고 프로필 적용 상태로 즉시 인식한다.

---

## 목표 2 — 휴식 시작 · 완료 (말풍선 흐름)

**현재 흐름**: 알림 시간이 되면 펫에 말풍선이 붙어 **n분 뒤에 / 휴식 시작**을 보여준다(`speech-top.png`) → **휴식 시작**을 누르면 루틴 타이머와 **완료** 버튼이 뜬다 → 완료를 누르면 15초간 완료 안내(`speech-completed.png`) 후 사라진다. 초과 시간은 `+mm:ss`로 표시되고 `+60:00`에서 멈춘다(`speech-overtime.png`). 펫 우클릭 → "말풍선 접기"를 선택하면 펫만 남고 말풍선은 숨는다(`speech-folded.png`); 문서(`stretch-notifications.md`)는 "접은 상태에서도 휴식 시간은 진행되며 접기 설정은 재실행 후에도 유지된다"고 명시한다.

### 문제 2.1 — 말풍선을 접은 채로 알림·휴식이 대기 중이면 사용자에게 알릴 방법이 전혀 없다 (P1)

- **재현 근거(코드)**:
  - `PetReminder.cs`의 `Invite`/`Start`는 `Session`을 세팅하고, `StretchClock.Tick(now, idleFor, idleThreshold, heldForBreak)`(`src/Unfold.Core/StretchClock.cs:36-43`)는 `heldForBreak`(=`Reminder.Session is not null || openingReminder`, `AppRuntime.cs:118`)가 참이면 `Paused`와 무관하게 **항상 false를 반환**해 새 초대나 시간 경과가 멈춘다. 즉 알림이 뜬 순간부터 사용자가 응답할 때까지 다음 휴식까지 남은 시간 진행 자체가 멈춘다(문서에도 명시된 의도된 동작).
  - `PetWindow.RefreshSpeech()`(`PetWindow.cs:110-129`)는 `expanded = runtime.Reminder.HasNotice && !runtime.Settings.BubbleCollapsed`로 접힘 여부만 반영해 말풍선을 숨기고, 접힌 동안 펫 외형·툴팁에 어떤 변화도 주지 않는다(`speech-folded.png`에서 확인: 대기 중 상태 표시 없음).
  - `AppRuntime.Tick()`(`AppRuntime.cs:121-126`)이 갱신하는 트레이 텍스트(`tray.ToolTipText`, `trayStatus.Header`)는 `Clock.Stopped/Paused/IdlePaused`만으로 만든 `clockState`를 쓸 뿐 `Reminder.HasNotice`(초대 대기·휴식 중)를 전혀 반영하지 않는다. 설정 창의 `state.Text`(`SettingsWindow.cs:119`, "휴식 중 · 타이머 대기")만이 이 상태를 보여준다.
- **재현 시나리오**: 사용자가 한 번 "말풍선 접기"를 선택해 두고 설정 창을 트레이로 내린 채 작업한다. 알림 시간이 되어 초대가 뜨지만 접혀 있어 보이지 않는다. 남은 시간은 그 순간부터 멈추고, 트레이 아이콘에 마우스를 올려도 "Unfold · 00:00 · 진행 중"처럼 평소와 똑같은 문구만 보인다(진행 중이라는 문구는 실제로는 얼어붙은 상태). 사용자는 타이머가 고장났다고 오인하거나, 다음 휴식이 영영 오지 않는다고 느낄 수 있다. 복구하려면 펫을 우클릭해 "말풍선 펼치기"를 누르거나 트레이 메뉴 "타이머 중지"를 눌러야 하는데, 이 두 가지 모두 사용자가 먼저 "무언가 멈췄다"는 것을 알아야 시도할 동작이다.
- **소유 파일**: `src/Unfold.Desktop/AppRuntime.cs`(트레이 텍스트), `src/Unfold.Desktop/PetWindow.cs`(RefreshSpeech).
- **개선 흐름**: 트레이 툴팁/메뉴 항목이 `Reminder.HasNotice`일 때 "휴식 대기 중 · 펫을 확인해 주세요" 같은 문구로 바뀌게 하거나, 접힌 상태에서도 펫 외형에 작은 표시(배지, 색 변화 등)를 준다.
- **수용 기준(행동 기반)**: 말풍선을 접은 상태에서 알림 시간이 되면, 설정 창을 열지 않고도 트레이 아이콘에 마우스를 올리거나 트레이 메뉴를 여는 것만으로 "휴식 대기 중"임을 확인할 수 있다.

*(참고: 이 문제는 `stretch-notifications.md`에 문서화된 "접기는 알림·휴식을 취소하지 않는다"는 의도된 동작 자체는 그대로 두고, 그 동안의 **상태 피드백 부재**만을 다룬다. 시각적 표현 방식은 `ui-visual-auditor`, 트레이 접근성 이름 문제는 `ui-accessibility-qa`와 공유 대상이다.)*

---

## 목표 3 — 루틴 · 프로필 관리

**현재 흐름**: 설정 창 "루틴 · 프로필" 탭(`PersonalizationWindow.cs`) → "내 루틴"/"업무 프로필" 두 하위 탭. 각 탭은 목록 + "새 루틴/새 프로필", "편집", "사용/적용", "삭제" 버튼으로 구성된다. 삭제는 확인 대화상자(`dialog-confirm.png`)를 거치고, 루틴이 아직 프로필에서 쓰이고 있으면 확인창을 띄우기 전에 구체적 프로필 이름을 알려주며 막는다(`Personalization.cs:67-71` `RemoveRoutine`) — 이 선제 검증은 견고하게 구현되어 있다.

### 문제 3.1 — "프로필 적용" 버튼이 타이머 진행 중에도 활성 상태라 클릭 후에야 실패를 알려준다 (P1)

- **재현 근거(코드)**:
  - `PersonalizationWindow.cs:132-140` `RefreshActions()`는 `applyProfile.IsEnabled = ... = profile is not null;`로만 버튼을 켜고 끈다 — 타이머 실행 상태(`runtime.CanEditTimerInterval`)는 전혀 확인하지 않는다.
  - 반면 같은 창의 대시보드 쪽 "알림 설정" 카드는 `interval.IsEnabled = canEditInterval`로 **사전에** 입력을 잠그고 이유를 안내한다(`SettingsWindow.cs:135, 142-145`). 동일한 제약(간격 변경은 일시정지·정지 중에만)을 다루는 두 화면이 서로 다른 시점에 사용자에게 알려준다.
  - `PersonalizationWindow.cs:50-53`의 `applyProfile` 클릭 → `settings().ApplyProfile(selected.Id)`(`Personalization.cs:99-103`, 프로필의 간격·자리비움·루틴을 적용) → `runtime.UpdateSettings`(`AppRuntime.cs:135-141`)가 `reschedulesTimer && !CanEditTimerInterval`이면 `ArgumentException("타이머를 일시정지하거나 중지한 뒤 알림 시간 또는 업무 프로필을 적용해 주세요.")`를 던진다. 이 예외는 `PersonalizationWindow.cs:85-96`의 `Action()` 래퍼가 잡아 `status` 텍스트로 보여준다(사용자 문구 자체는 정확하고 친절하다).
- **재현 시나리오**: 타이머가 "진행 중"인 동안 "루틴 · 프로필 → 업무 프로필" 탭에서 인터벌이 다른 프로필을 선택하고 "프로필 적용"을 누른다(버튼이 활성 상태라 클릭 가능). 클릭 후에야 "타이머를 일시정지하거나 중지한 뒤..." 오류 문구를 본다. 사용자는 다시 "타이머" 탭으로 이동해 일시정지/정지한 뒤, "루틴 · 프로필" 탭으로 돌아와 같은 프로필을 다시 선택하고 다시 "프로필 적용"을 눌러야 한다.
- **소유 파일**: `src/Unfold.Desktop/PersonalizationWindow.cs`, `src/Unfold.Desktop/AppRuntime.cs`(제약 원본).
- **개선 흐름**: `RefreshActions()`가 `runtime.CanEditTimerInterval`(또는 이를 대체할 콜백)을 참조해 간격이 다른 프로필을 적용할 때는 버튼을 비활성화하고, 대시보드와 동일한 문구("타이머를 일시정지하거나 중지해 주세요")를 프로필 상세 캡션(`detail`)에 미리 보여준다.
- **수용 기준(행동 기반)**: 타이머가 진행 중일 때 간격이 다른 프로필을 선택하면, "프로필 적용"을 누르기 전에 이미 비활성화되어 있거나 이유가 화면에 보인다. 클릭 후에야 실패를 아는 경우가 없다.

---

## 목표 4 — 기록 확인 · 내보내기

**현재 흐름**: "기록 · 내보내기" 탭(`BreakReviewWindow.cs`) → 7일 요약(횟수·총 시간·활동일)과 요일별 표(횟수, 시간)를 보고 "이전/다음 7일"로 탐색, "CSV 내보내기"로 저장한다(`weekly-review.png`, `settings-review-tab.png`). 기록이 없는 주는 "이 7일 동안 완료한 휴식이 없어요. 채워야 할 목표는 없으니 편하게 시작하세요."라는 부담 없는 빈 상태 문구를 보여준다 — 톤이 문서(`mvp.md`)의 제품 성격과 일치하고 잘 되어 있다.

### 문제 4.1 — 개별 휴식 기록(루틴명·시간)을 앱 안에서 확인할 방법이 없다 (P2)

- **재현 근거(코드)**: `src/Unfold.Core/BreakHistory.cs:47-53` `Review(endDay)`는 `entries`(개별 `CompletedBreak`: `RoutineName`, `ProfileName`, `RecordedSeconds`, `CompletedAt` 포함)를 만들지만, `BreakReviewWindow.cs:78-85` `Refresh()`는 이 `Review.Entries`를 화면에 전혀 쓰지 않고 `Review.Days`(요일별 집계: 날짜, 횟수, 총 초)만 그리드에 그린다. 요일 행을 눌러도 아무 동작이 없다(클릭 핸들러 없음).
- **재현 시나리오**: 사용자가 "오늘 어떤 루틴으로 몇 시에 쉬었는지" 확인하고 싶어도, 화면에는 "1회 · 0분 30초"처럼 합계만 보인다. 세부 정보(루틴 이름, 프로필, 정확한 시각)를 보려면 CSV로 내보내 스프레드시트를 열어야 한다.
- **소유 파일**: `src/Unfold.Desktop/BreakReviewWindow.cs`.
- **개선 흐름**: 요일 행을 펼치면 그날의 `Review.Entries`(루틴 이름, 계획/실제 시간)를 나열하는 상세 목록을 보여준다.
- **수용 기준(행동 기반)**: 사용자가 CSV를 내보내지 않고도 특정 날짜를 눌러 그날 완료한 개별 휴식의 루틴 이름과 시간을 앱 안에서 볼 수 있다.

---

## 목표 5 — 펫 팩 열기 (설치 전 확인)

**현재 흐름**: "펫 추가 → 펫 팩 열기" → `.unfoldpet` 선택 → 이름·버전·설치 판정(설치/업데이트/재설치) 표시 → 배경/미리보기 크기 선택 → "미리 볼 동작" 드롭다운으로 반응 재생, 일시정지/다시 재생 → 경고 문구(있으면) 확인 → 설치(`settings-pet-open-tab.png`, `pack-preview.png`, `pack-error.png`).

### 문제 5.1 — 최소 창 크기에서 설치 전 확인 요소가 스크롤 아래 숨고 "설치" 버튼만 항상 보인다 (P1)

- **재현 근거(캡처)**: `settings-pet-open-minimum.png`(860×680, 스크롤 전) — 제목·미리보기·배경/크기 선택까지만 보이고, "미리 볼 동작" 드롭다운, 일시정지/다시 재생 버튼, 경고 문구는 화면 밖으로 잘려 스크롤바만 보인다. 반면 우측 하단 "설치" 버튼은 이 상태에서도 이미 완전히 보인다.
- **재현 근거(코드)**: `PetPackWindow.cs:67-73`
  ```csharp
  var body = Ui.Column(
      Ui.Actions(open), title, version, previewSurface,
      Ui.Row(Ui.Field("배경", background), Ui.Field("미리보기 크기", size)),
      Ui.Field("미리 볼 동작", clips), Ui.Actions(pause, replay), playbackStatus, warnings);
  ...
  Content = Ui.PageContent(..., body, Ui.Column(status, Ui.Actions(install)), "펫 추가");
  ```
  `warnings`가 컬럼 맨 마지막(가장 아래) 요소이고, `Ui.PageContent`(`Ui.cs:56-64`)는 본문(`body`)만 `ScrollViewer`로 감싸고 액션 바(`footer`, 여기서는 "설치" 버튼)는 스크롤 밖 고정 영역에 둔다. 즉 스크롤해야 보이는 정보가 늘어날수록, 스크롤 없이 항상 보이는 "설치" 버튼과의 거리만 멀어진다.
- **재현 시나리오**: 최소 창 크기로 설정 창을 쓰는 사용자가 경고가 있는 펫 팩(예: `pack-error.png`류 상황이 아닌, 크기·해상도 경고가 있는 정상 팩)을 열면, 스크롤하지 않고도 "설치" 버튼을 누를 수 있다. "미리 볼 동작"을 다른 반응으로 바꿔 실제로 재생해 보거나 경고 문구를 읽는 단계는 건너뛰기 쉽다.
- **소유 파일**: `src/Unfold.Desktop/PetPackWindow.cs`, `src/Unfold.Desktop/Ui.cs`(`PageContent`의 스크롤/액션바 분리 구조).
- **개선 흐름**: 경고(`warnings`)를 `previewSurface` 바로 아래처럼 스크롤 없이 보이는 위치로 옮기거나, 경고가 있을 때 "설치" 버튼 옆/위에 축약 배지("⚠ 주의사항 1개")를 붙여 스크롤 없이도 존재를 알린다.
- **수용 기준(행동 기반)**: 최소 창 크기(860×680)에서 경고가 있는 펫 팩을 열었을 때, 스크롤하지 않고도 경고가 있다는 사실을 확인할 수 있다.

---

## 목표 5(계속) — 펫 팩 만들기 (GIF/MP4로 커스텀 펫)

**현재 흐름**: "펫 추가 → 펫 팩 만들기" → 이름 입력 → "파일 가져오기"로 GIF/MP4 선택 → "적용할 동작" 선택 → "동작에 넣기" → 각 동작 카드에서 파일 선택/미리보기/제거 → "펫 팩 만들기…"로 저장 → 자동으로 "펫 팩 열기" 탭으로 전환되어 미리보고 설치(`custom-pet-editor.png`, `custom-pet-minimum-scrolled.png`).

### 문제 5.2 — 파일 가져오기의 기본 배정 대상이 항상 "쉬는 모습"이라 반복 임포트 시 필수 파일을 실수로 덮어쓸 수 있다 (P1)

- **재현 근거(코드)**:
  - `CustomPetWindow.cs:37` `action = new ComboBox { ..., SelectedIndex = 0, ... }`이고 `CustomPetDraft.Actions`(`src/Unfold.Core/CustomPetDraft.cs:39`)의 0번은 `"idle"`(쉬는 모습). "파일 가져오기" → "동작에 넣기" 흐름(`CustomPetWindow.cs:115-126` `ImportMedia`/`AssignPending`)은 이 드롭다운을 **한 번도 자동으로 바꾸지 않는다** — 파일을 가져올 때마다 기본값은 항상 "쉬는 모습"으로 남아 있다.
  - `Assign(key, path)`(`CustomPetWindow.cs:133-148`) → `draft.SetClip(key, clip)`는 확인 절차 없이 해당 동작의 기존 클립을 즉시 교체한다. 이미 채워진 동작을 다시 선택해 "동작에 넣기"를 눌러도 아무 경고 없이 덮어써진다.
- **재현 시나리오**: 사용자가 GIF를 가져와 "쉬는 모습"에 배정한다(정상, 드롭다운 기본값과 일치). 이어서 "휴식 완료"용 두 번째 GIF를 가져오기 위해 "파일 가져오기"를 다시 누르지만, 드롭다운을 "휴식 완료"로 바꾸는 것을 잊고 그대로 "동작에 넣기"를 누르면 방금 만든 "쉬는 모습" 파일이 되돌릴 방법 없이 새 파일로 조용히 대체된다. `custom-pet-editor.png` 캡처에서 실제로 첫 슬롯만 채워지고 다음 파일 가져오기 시 드롭다운이 여전히 기본값을 가리키는 상태가 이 흐름을 뒷받침한다.
- **소유 파일**: `src/Unfold.Desktop/CustomPetWindow.cs`.
- **개선 흐름**: 파일을 배정한 뒤 드롭다운을 아직 비어 있는 다음 동작으로 자동 이동시키거나, 이미 채워진 동작을 다시 선택해 "동작에 넣기"를 누르면 "이미 넣은 파일을 바꿀까요?" 확인을 한 번 거치게 한다.
- **수용 기준(행동 기반)**: 이미 파일이 있는 동작을 선택한 채 "동작에 넣기"를 누르면, 교체 여부를 확인하는 안내가 뜨고 취소하면 기존 파일이 그대로 남는다. 또는 파일을 가져올 때마다 드롭다운이 비어 있는 다음 동작을 기본으로 가리켜 실수로 같은 동작을 다시 채울 가능성이 줄어든다.

---

## 잘 작동하는 부분 (회귀시키지 말 것)

- 알림 간격 잠금 사유를 사전에(클릭 전) 안내하는 "알림 설정" 카드의 문구·툴팁(`SettingsWindow.cs:142-145`).
- 루틴 삭제 시 프로필 참조를 먼저 검사해 구체적 프로필 이름과 함께 막는 흐름(`Personalization.cs:67-71`), 확인 대화상자 취소가 항상 안전한 기본값인 점(`Ui.cs:87-106`).
- 펫 팩 설치 실패/불일치 사유별로 구체적인 한국어 안내(`Ui.cs:113-135` `ErrorText`)와, 설치 후 목록 갱신 실패를 설치 실패와 구분해 알리는 점(`pet-packs.md` 문서와 일치).
- 정지/일시정지/재생 버튼의 접근성 이름과 아이콘이 상태별로 정확히 전환됨(`TimerControls.cs`).
- 빈 기록 주간에 "채워야 할 목표는 없으니 편하게 시작하세요"처럼 압박하지 않는 톤(`BreakReviewWindow.cs:87`).

## 다른 담당과의 접점

- 문제 2.1(트레이/펫 상태 피드백)의 시각적 표현 방식은 `ui-visual-auditor`와, 트레이 메뉴 항목의 접근성 이름은 `ui-accessibility-qa`와 공유 대상이다.
- 문제 5.1(최소 창 크기에서 스크롤 숨김)은 `ui-accessibility-qa`의 "최소 창 크기·스크롤·잘림" 점검 범위와 겹친다 — 동일 근거(`settings-pet-open-minimum.png`)를 공유한다.
