# UX 흐름 감사 — 2026-09-20 (release/mvp, 미커밋 변경 포함)

감사자: ux-flow-auditor. 정적 코드 분석 + `git diff` 대조. `dotnet build/test/run` 미실행,
`artifacts/verification/`의 캡처는 2026-09-13~14 구버전 화면뿐이라 참고하지 않음(현재 UI와 불일치).
`docs/validation/2026-09-20-*.md`의 자동 캡처 목록·JSON은 확인했으나 이미지 파일 자체는 열지 않음(코드 근거로 대체).

> **재검증 공지 (2번째 판)**: 감독자가 1판의 UX-1(P0)·UX-2(P1) 판정을 반증했다. `git diff`만으로 "본문·안내
> 문구가 사라졌다 = 회귀"라고 단정하고, 그 제거를 **명시적으로 요청·승인한 시안 문서**
> (`docs/plans/2026-09-20-speech-ui-proposal.md`, `docs/validation/2026-09-20-speech-ui-implementation.md`,
> `docs/validation/2026-09-20-ui-copy-cleanup.md`)와 **그 제거를 강제하는 테스트/스모크 계약**
> (`Tests/Unfold.Tests/BreakReminderTests.cs:174`, `src/Unfold.Desktop/SmokeDiagnostics.cs:245-257`의
> `forbiddenBodyText`)을 확인하지 않은 것이 원인이다. 아래 승인 문서를 전수 재대조해 전체 발견을
> (a) 승인된 의도 / (b) 문서화되지 않은 회귀 / (c) 승인됐지만 부작용이 남은 경우로 재분류했다.
> 반증된 판정은 표에서 지우지 않고 취소선 없이 "재분류 근거"를 덧붙여 그대로 남겼다.

## 요약 (재분류 결과)

1. **[반증 확인]** UX-1(구 P0, "루틴 단계 안내 삭제")과 UX-2(구 P1, "+60분 안내 삭제")는 **회귀가 아니라 사용자
   승인 시안**이다. `speech-ui-proposal.md`가 "사용자의 본문 제거 요청을 반영"했다고 명시하고,
   `BreakReminderTests.cs:174`와 `SmokeDiagnostics.cs:245-257`이 해당 텍스트가 **나타나면 실패**하는 계약으로
   잠가 두었다. 두 항목 모두 (a) 승인된 의도로 재분류했다(아래 표·해설 참조).
2. **[신규 P2]** 그 대신, 승인된 UI 변경이 드러낸 **실질적 정합성 문제**가 있다: `BreakStep.Instruction`은
   여전히 필수·검증 대상 입력 필드(`BreakSession.cs:3,12`, `RoutineEditorWindow.cs:25,39`)인데, 제품 어디에도
   렌더링되지 않는다(전수 grep 확인, 표시 경로 0곳). 다만 이 필드를 편집하는 `RoutineEditorWindow`는
   **진단 전용 경로**(`SmokeDiagnostics.cs`에서만 생성, 일반 UI에서 도달 불가)라 실사용자 노출은 사실상 0에
   가깝다 — 심각도를 P3에서 P2 사이로 완화해 재평가했다(신규 UX-12, 아래 참조).
3. **[P1 유지]** UX-3(설정·홈 시간 카드의 미저장 변경이 종료 시 확인 없이 사라짐)은 어떤 승인 문서에서도
   다뤄지지 않았고, 오히려 `docs/plans/2026-09-18-settings-ui-redesign.md`가 "일반적인 UX 후속 작업은 …
   별도 계획으로 정한다"며 명시적으로 미룬 항목이다. 그 "별도 계획"은 찾지 못했다. **진짜 미승인 gap**이므로
   P1을 유지한다.
4. **[재분류: (a) 승인]** UX-4(CSV 7일 범위 안내 삭제), UX-6(빈 미리보기 안내 삭제), UX-7(GIF/MP4 사전 안내
   삭제), UX-10(완료 요약 문구 삭제)은 모두 `docs/validation/2026-09-20-ui-copy-cleanup.md`의 항목별 변경
   목록에 문자 그대로 대응한다. (a) 승인된 의도로 재분류하되, UX-4는 사용자에게 남는 **잔여 위험**(승인된
   결정의 부작용)이 실질적이라 severity를 P1→P2로 낮춰 유지한다.
5. **[재확인]** UX-5(CSV 실패 메시지 축소), UX-9(설정 탭 저장 성공 피드백 부재)는 (c) "승인된 의도의 부작용"
   또는 두 개의 개별 승인 결정이 조합되어 생긴 잔여 불일치로 재분류했다. UX-8(초대 말풍선에 경량 건너뛰기
   없음), UX-11(기록 페이지네이션 경계 설명 없음)은 어떤 시안에도 등장한 적 없는 **순수 제안**이라 "회귀"
   분류 자체가 성립하지 않음을 명시했다.

---

## 흐름 1 — 첫 실행

**사용자 목표**: 앱을 처음 켠 뒤 펫을 보고 타이머가 동작 중임을 확인한다.

**실제 단계**:
- `Program.cs:37-48` 데이터 루트 생성 → 단일 인스턴스 락 → `App.cs:22-26` `AppRuntime.Start(background:false)` 호출.
- `AppRuntime.cs:105-124` `Start()`: 내장 캐릭터 로드 → `Reload()` → `BuildTray()` → `timer.Start()` → **`Clock.Start()`를 앱이 자동 호출** → `UpdatePet()` → `ShowSettings()`.
- 기본값(`AppSettings.cs:7-28`): 60분 간격, 1분 휴식, `default-cat`, 펫 표시 on, 기본 테마 오트라떼.
- `SettingsWindow.cs:38-131` 생성자에서 홈 대시보드가 기본 탭으로 구성되고 `Refresh()`가 즉시 실행되어 카운트다운·상태 배지·펫 미리보기가 채워진다.

**끊기는 지점**:
- 온보딩·환영 화면이 전혀 없다. 사용자는 아무 설명 없이 "60:00 카운트다운 + 펫"을 보게 된다. `mvp.md`에 온보딩이 범위로 명시돼 있지 않으므로 **의도적 제외**로 판단(코드 추론, 1판과 동일 — 재검증으로 변동 없음).
- `AppRuntime.cs:115-117` 내장 펫 파일이 없으면 `DirectoryNotFoundException`("기본 펫 파일이 없어요. Unfold를 다시 설치해 주세요.")을 던지고 `123`행에서 `ShowSettings()` 후 `Ui.Error`로 표시 — 원인과 행동이 명확한 복구 메시지(확인된 사실).
- 창을 X 버튼으로 닫으면 `SettingsWindow.cs:128` `Closing += (_, e) => { e.Cancel = true; HideToTray(); }` — 실제로는 트레이로 숨을 뿐 앱이 종료되지 않는다. 첫 사용자는 "닫았다"고 착각할 수 있으나, 트레이 아이콘 존재와 종료 방법에 대한 안내가 없다. 트레이 상주 앱의 통상적 동작이라 강한 결함은 아니지만 최초 실행 시 안내 부재는 P2 마찰로 남는다.

---

## 흐름 2 — 타이머·스트레칭 세션

**사용자 목표**: 간격 설정 → 시작 → 실행 중 변경 시도 → 알림 → 완료/초과/건너뜀 → 기록 반영.

**실제 단계**:
- 간격 설정: `SettingsWindow.Layout.cs:155-177` 홈 "시간 설정" 카드(스트레칭 간격 5~240분, 휴식 시간 1~10분)는 자체 "적용" 버튼(`SettingsWindow.cs:58-70`)을 갖는다. 실행 중에는 `interval` 필드가 비활성화되고(`CanEditTimerInterval` = 일시정지/정지 상태만) 툴팁으로 이유를 알려준다(`SettingsWindow.cs:211`). 강제로 바뀐 값은 `discardedInterval` 로직(`SettingsWindow.cs:204-218`)이 되돌리며 "저장하지 않은 변경을 되돌렸어요." 경고를 띄운다 — **상태 전달이 정확한 사례**.
- 실행 중 변경 시도: `AppRuntime.UpdateSettings` (`AppRuntime.cs:166-184`)가 `reschedulesTimer && !CanEditTimerInterval`일 때 `ArgumentException`을 던지고, 이는 `SaveHomeTimingSettings`(`SettingsWindow.cs:162-174`)에서 잡혀 "저장하지 못했어요." 메시지로 표시된다. 버튼은 애초에 비활성화돼 있으므로 실제로 이 예외 경로에 도달하기는 어렵다 — 방어적 코드, 문제로 보지 않음.
- 알림: `AppRuntime.Tick()→ShowReminder()`(`AppRuntime.cs:256-280`)가 `Reminder.Invite`를 호출, `PetWindow.RefreshSpeech()`가 `PetSpeechBubble.Refresh()`를 통해 상태별 UI를 그린다.

### 재검증: UX-1 "루틴 단계 안내 삭제" — 반증됨, (a) 승인된 의도로 재분류

1판에서 `PetSpeechBubble.cs`의 `instruction` TextBlock 제거(휴식 중 `BreakSession.CurrentStep.Instruction`
미표시)를 P0 회귀로 판정했다. 이는 틀렸다. 근거:

- **요청·승인 문서**: `docs/plans/2026-09-20-speech-ui-proposal.md:3` "사용자의 본문 제거·제목 중앙 정렬
  요청을 반영해 제품 코드 적용과 검증을 완료했다." 같은 파일 :10 "4차 시안은 … **제목과 조작 사이의 본문만
  모두 제거한다.**" :11 "아래 제안이 최신 기준이며, 3차 시안의 본문 유지 결정을 대체한다." 구현 결과 절
  "모든 상태의 설명 본문을 제거하고 하단 상태 안내와 네 가지 테마는 유지했다."(단, "하단 상태 안내" 유지
  여부는 UX-2 해설에서 별도로 재검토함.)
- **구현 검증 문서**: `docs/validation/2026-09-20-speech-ui-implementation.md:13` "모든 상태의 설명 본문과
  해당 툴팁을 제거했다." "[완료](images/2026-09-20-speech-ui-implementation/speech-completed.png) | 제목과
  접기 안내만 표시, 본문 제거 확인" — 캡처 목록 자체가 본문 제거를 결과로 기록.
- **테스트 계약**: `Tests/Unfold.Tests/BreakReminderTests.cs:33-37`이 초대 상태에서 루틴 이름·
  `CurrentStep.Instruction`이 **어떤 TextBlock에도 없어야** 함을 `Assert.DoesNotContain`으로 단정.
  같은 파일 `OvertimeTimerAndCompletionButtonFitAfterInstructionBodyIsRemoved` 테스트(이름 자체가 "본문이
  제거된 뒤"를 전제) :174 `Assert.DoesNotContain(... text.Text == session.CurrentStep.Instruction)`.
- **스모크 진단 계약**: `src/Unfold.Desktop/SmokeDiagnostics.cs:245` `VerifySpeechBubble(window, expectedTitle,
  expectedHeight, **params string[] forbiddenBodyText**)`. 123·134·144행에서 `session.CurrentStep.Instruction`을
  이 `forbiddenBodyText` 인자로 넘긴다. 257행: `if (forbiddenBodyText.Any(body => texts.Any(text =>
  text.Contains(body))))  throw new InvalidOperationException("Speech bubble still exposes removed
  instruction body text.")`. 즉 이 문자열이 **나타나면 진단이 실패**한다 — "표시하라"가 아니라 "표시되면
  안 된다"는 잠긴 계약이다. 1판에서 이 시그니처를 확인하지 않고 반대로 읽었다.

**재분류**: (a) 승인된 의도. P0에서 제외한다.

### 재검증: UX-2 "+60분 안내 삭제" — 반증됨, (a) 승인된 의도로 재분류

- `docs/validation/2026-09-20-ui-copy-cleanup.md` 변경 목록: "말풍선의 완료 안내와 **+60분 안내 문구를
  제거했다**." — 명시적이고 구체적으로 이 문구의 제거를 기록.
- `BreakReminderTests.cs:175-176`: `Assert.DoesNotContain(... text.Text is "준비되면 언제든 완료할 수
  있어요." or "+60분에 도달했어요. 완료를 눌러 주세요.")` — 제거 대상 두 문구를 정확히 지정해 부재를 단정.
- **문서 간 해석 정정**: `speech-ui-proposal.md`의 "하단 접기·완료·한도 안내는 상태 이해와 다음 행동에
  필요한 기존 요소이므로 유지한다"는 문장은 **speech-ui 4차 시안(같은 날 앞선 단계) 시점의 결정**이며,
  `ui-copy-cleanup.md`는 그 이후 단계에서 이 하단 안내(12px 캡션, 옛 `hint` 필드)까지 추가로 제거하기로
  범위를 넓힌 것으로 읽힌다. 실제 코드(`PetSpeechBubble.cs`에 `hint` 필드 자체가 없음)와 테스트가 이
  최종 상태와 일치하므로, "제거됨"이 확정된 제품 의도다. `BreakSession.cs:58-59,96`의 오버타임 캡 로직
  자체(`MaximumOvertime`, `Overtime`)는 그대로 유지되어 있어 **동작은 문서화된 대로**(`docs/mvp.md`
  "caps at +60:00 without auto-completing") 정확히 작동한다 — 사라진 것은 그 상태를 설명하던 텍스트뿐이고,
  그 텍스트 제거도 승인된 것이다.

**재분류**: (a) 승인된 의도. P1에서 제외한다.

### 신규 발견 — UX-12: 필수·검증 대상 필드인데 제품 UI 어디에도 렌더링되지 않는 루틴 단계 안내

위 두 항목이 반증되면서 드러난 **진짜 잔여 문제**는 다음과 같다.

- `src/Unfold.Core/BreakSession.cs:3` `public sealed record BreakStep(string Instruction, int Seconds);`
- 같은 파일 :12 `BreakRoutine.Validate()`: `string.IsNullOrWhiteSpace(step.Instruction) || step.Instruction.Length > 180`이면
  `ArgumentException`을 던진다. 즉 **모든 루틴의 모든 단계는 빈칸이 아닌 안내문을 가져야만 유효하다** —
  지금도 강제되는 검증 규칙이다.
- `src/Unfold.Desktop/RoutineEditorWindow.cs:25` 사용자가 단계별 안내(`TextBox`, 최대 180자)를 여전히
  입력·수정할 수 있고, :39 저장 시 빈 문자열 단계만 걸러낼 뿐 나머지는 그대로 `BreakStep.Instruction`에
  담겨 검증을 통과해야 저장된다.
- **표시 경로 전수 확인**: `grep -rn "\.Instruction\b" src/Unfold.Desktop/*.cs src/Unfold.Core/*.cs` 결과
  총 6곳 — `BreakSession.cs`(정의·검증), `RoutineEditorWindow.cs`(입력·저장, 2곳), `SmokeDiagnostics.cs`
  (3곳, 전부 위에서 확인한 **금지 문자열**로 전달됨). 실제 사용자에게 이 텍스트를 그리는 코드는 **0곳**이다.
  `CurrentStep` 자체도 `BreakSession.cs`(정의)와 테스트 파일(`BreakSessionTests.cs`, 순수 로직 검증) 외에는
  제품 UI에서 소비되지 않는다.
- **도달 경로 확인**: `RoutineEditorWindow`를 실제로 생성하는 곳은 `PersonalizationWindow.cs:126,134`뿐이고,
  `PersonalizationWindow` 자체를 생성하는 곳은 `src/Unfold.Desktop/SmokeDiagnostics.cs:83` **한 곳뿐**이다.
  `AppRuntime.cs`·`SettingsWindow*.cs`·트레이 메뉴·펫 컨텍스트 메뉴 어디에도 이 창을 여는 진입점이 없다.
  이는 `docs/personalization.md:9` "이전 버전 데이터를 검사하기 위한 내부 편집기와 자동 진단 경로는
  남겨 두지만 **제품 기능으로 노출하지 않는다**."와 정확히 일치하는, **2026-09-17에 이미 승인된 별도
  결정**이다.

**해석**: `BreakStep.Instruction`은 (1) 여전히 필수·검증 대상 데이터 필드이고, (2) 진단 전용 편집기에서는
여전히 입력받지만, (3) 그 값을 사용자에게 보여주는 코드는 제품에 하나도 없다. "입력은 받지만 절대 보여주지
않는 필수 필드"가 남아 있다는 코디네이터의 지적이 정확하다. 다만 그 편집기 자체가 일반 UI에서 도달 불가능한
진단 전용 경로이므로, **오늘 시점의 일반 사용자가 이 문제를 직접 겪을 경로는 없다.** 실사용자 영향이 있다면
그것은 2026-09-17 이전에 커스텀 루틴을 만든 **레거시 사용자**뿐이다 — 그들의 `settings.json`에 저장된
`CustomRoutine`의 단계별 안내문이 계속 검증·로드는 되지만(`BreakRoutines.ForSettings`), 그 문구를 다시
보거나 고칠 방법이 없다(이 부분은 2026-09-17에 이미 승인된 UI 제거의 알려진 결과이며 신규 문제가 아니다).

**사용자 영향 시나리오(정정판)**: (1) 오늘 시점 신규/일반 사용자 — 영향 없음(진단 전용 경로라 도달 불가).
(2) 레거시 커스텀 루틴 보유 사용자 — 이미 2026-09-17에 승인된 "루틴 UI 제거"의 연장선상 영향으로, 신규
발견이 아님. (3) **유지보수 리스크** — 향후 개발자가 `RoutineEditorWindow`/`PersonalizationWindow`를
어떤 이유로든 다시 노출하거나, 진단을 돌리는 엔지니어가 "이 필드가 필수인 이유"를 오해해 화면에 보이지도
않는 문구를 공들여 작성할 수 있다. 검증 규칙과 실제 소비처가 어긋나 있다는 사실 자체가 코드 정합성 문제다.

**심각도**: **P2 → P3 사이에서 P2로 판정**(P2를 선택한 근거: 직접적 사용자 마찰은 없지만, 필수 검증 규칙이
존재 이유를 잃은 채 남아 있는 것은 향후 회귀·혼란의 소지가 있는 실질적 기술 부채이며, 좁게는 레거시 데이터
소유자에게 "고칠 수 없는 텍스트가 검증만 통과하면 계속 요구된다"는 잠재적 혼란이 있기 때문. 사용자가 오늘
당장 겪는 문제였다면 P0/P1이었을 사안이므로 과소평가하지 않기 위해 P3이 아닌 P2로 유지).

**권장 조치**: 제품 결정 필요 — (1) `Instruction`을 선택 필드로 완화하고 검증에서 빈 문자열을 허용하거나,
(2) `RoutineEditorWindow`/`PersonalizationWindow`를 실제로 완전히 제거된 기능으로 문서화해 검증 규칙과
일치시키거나, (3) 향후 이 안내문을 다시 표시할 계획이 있다면 그 계획을 문서화. 어느 쪽이든 "검증은 요구하되
아무도 보지 않는다"는 현재 상태를 명시적 결정으로 바꿔야 한다.

- Complete: `complete` 버튼 클릭 → `runtime.CompleteBreak()` → `PetReminder.Complete`(`PetReminder.cs:37-45`) → `AppRuntime.FinishBreak`(`AppRuntime.cs:340-358`)이 `BreakHistory.Add` 후 `celebrate` 반응 + 완료음을 재생. `Notice=Completed`로 전환. 완료 요약 문구 제거(구 UX-10)는 아래 흐름 4/5 재검증과 같은 패턴의 (a) 승인된 의도 — 상세는 발견 표 UX-10 참고.
- 건너뛰기(Skip): 말풍선에는 "건너뛰기" 버튼이 없다. `Invitation` 상태의 버튼은 스누즈(`n분 뒤에`)와 "휴식 시작"뿐이다(`PetSpeechBubble.cs:26-34`). 초대를 그냥 무시하려면 홈 카드나 트레이의 "타이머 정지"를 눌러야 하는데, 이는 전체 작업 타이머를 정지시키는 더 큰 동작이다. `speech-ui-proposal.md`의 4차 시안 4가지 버전 모두 초대 상태에는 "제목과 미루기·휴식 시작"만 명시하고 있어, 경량 건너뛰기는 **애초에 어느 시안에도 제안된 적이 없다** — "제거된 회귀"가 아니라 "한 번도 설계되지 않은 기능 공백"이다(UX-8로 재분류, 발견 표 참고).

**심각도**: UX-1·UX-2는 (a) 승인된 의도로 재분류(발견 표에서 판정 변경 근거와 함께 유지). 신규 UX-12는 **P2**.

---

## 흐름 3 — 휴식 회고 (말풍선 완료 → 기록)

이 저장소에는 별도의 "BreakReview.cs 창"이 없고, `BreakReview.cs`는 `BreakHistory.Review()`가 반환하는 순수 데이터 레코드(`BreakReview.cs:7-32`)이며 실제 UI는 `BreakReviewWindow.cs`(흐름 4와 동일 창)다. "기록 남기기와 건너뛰기"는 흐름 2에서 이미 다뤘다(완료=기록 추가, 스누즈/정지=기록 없음). 추가로 확인한 사항:

- `AppRuntime.FinishBreak`(`AppRuntime.cs:340-353`)에서 `BreakHistory.Save` 실패 시 `BreakHistoryError = "이번 휴식을 집계했지만 기록 파일에 저장하지 못했어요."`를 세팅 — 홈 대시보드(`historyStatus`, `SettingsWindow.cs:199-200`)와 기록 탭 모두 이 오류를 노출한다(`OpenReview` 콜백의 `getBreakHistoryError`). **조용한 실패가 아님 — 확인된 사실**(1판과 동일, 변동 없음).

---

## 흐름 4 — 기록 보기

**사용자 목표**: 기간 조작, 요약, 날짜별 상세 확인, 빈 기록 상태 이해, CSV 내보내기.

**실제 단계**: `SettingsWindow.Layout.cs:206-211` `OpenReview()` → `BreakReviewView` 생성 → 7일 창 표시, 이전/다음 7일 이동, 날짜 헤더 펼침/접힘, CSV 내보내기.

### 재검증: UX-4 "CSV 7일 범위 안내 삭제" — (a) 승인된 의도 + (c) 실질적 잔여 위험 병존

- **승인 근거**: `docs/validation/2026-09-20-ui-copy-cleanup.md` 변경 목록 "기록 탭의 **집계·저장 방식 설명**과
  빈 기록 위로 문구를 제거했다." 제거된 문장("완료 당시 기기의 날짜를 기준으로 표시해요. 내보내기에는 화면에
  보이는 7일만 포함돼요.")은 정확히 "저장 방식 설명"에 해당하므로 이 bullet이 커버한다고 판단한다. (a) 승인.
- **추가 확인**: `docs/personalization.md:26` "**CSV 내보내기**는 현재 화면의 7일을 저장한다."로 이 동작 자체가
  제품 문서에도 명시돼 있다 — 코드 동작(`BreakHistory.Review(DateOnly endDay)`, `BreakHistory.cs:52-63`)과
  일치. 즉 "7일만 내보낸다"는 제품 결정 자체는 의심의 여지 없이 확정된 의도다.
- **잔여 위험 — (c)**: 다만 `docs/personalization.md`는 **개발자용 제품 문서**이며 일반 사용자가 앱을 쓰며
  읽는 화면이 아니다. 승인된 것은 "화면 안의 캡션 문구를 지운다"는 결정이지, "그 결정이 사용자에게 미치는
  정보 격차를 그대로 둔다"는 결정까지 명시적으로 승인됐다고 보기는 어렵다. 기록은 최대 2,000건/90일 보관되는데
  (`BreakHistory.cs:16,40`) 내보내기 버튼 자체에는 범위를 알려주는 요소가 전혀 남지 않아, 여러 주의 데이터를
  모으려는 사용자가 "한 번 내보내면 전체가 담긴다"고 오해할 실질적 위험은 남아 있다.
- CSV 내보내기 실패 메시지도 "회고를 내보내지 못했어요. 이 기기의 저장 가능한 위치를 선택해 다시 시도해 주세요."(원인·행동 포함) → "회고를 내보내지 못했어요."(행동 안내 없음)로 축소됨(`BreakReviewWindow.cs` diff, `save` 버튼 catch 블록). `ui-copy-cleanup.md`는 "실제 저장·내보내기·설치 **성공·실패 피드백은 유지했다**"고 명시하므로, 실패 자체를 알리는 기능은 승인 범위 안에서 유지됐다고 볼 수 있다(실패했다는 사실은 여전히 전달됨) — 다만 "유지"를 "글자 그대로 보존"이 아니라 "핵심 신호는 보존"으로 해석했을 때만 성립하는 (c) 판정이며, 구체적 복구 지침은 실제로 줄었다(UX-5로 별도 유지).
- 빈 상태: "이 7일 동안 완료한 휴식이 없어요." 문구는 유지, "첫 휴식은 언제든 괜찮아요." 위로 문구만 제거 — `ui-copy-cleanup.md`의 "빈 기록 위로 문구를 제거했다"에 정확히 대응. (a) 승인.
- 이전/다음 7일 버튼은 경계(`endDay > today.AddDays(-77)`, `BreakReviewWindow.cs` 현재본 `Refresh()`)에서 조용히 비활성화되며 툴팁은 고정 라벨("이전 7일")뿐이다. 어떤 시안 문서에도 이 경계 설명이 언급된 적이 없어 "제거된 것"이 아니라 애초에 다뤄진 적 없는 항목이다(UX-11).

**재분류**: UX-4는 (a) 승인 + (c) 잔여 위험. 승인 문서가 있으므로 "미승인 회귀"라는 1판의 표현은 철회하되, 사용자에게 남는 실질적 정보 격차는 여전히 유효한 개선 여지로 P2로 유지한다(1판 P1 → 재분류 P2, 이유: 승인된 트레이드오프의 부작용이지 방치된 회귀가 아니므로 한 단계 완화하되 삭제하지는 않음).

---

## 흐름 5 — 펫 추가·커스텀 팩

**사용자 목표**: 열기/만들기 탭 전환, GIF·MP4 가져오기, 5개 행동 배정, 미리보기, 설치·교체·업데이트, 실패 경로 처리.

**실제 단계**: `PetManagementView.cs:20-40`이 "펫 팩 열기"(`PetPackView`)/"펫 팩 만들기"(`CustomPetView`) 탭을 구성. 탭 전환 시에도 두 뷰 인스턴스가 유지되어(초안 보존) 미저장 작업이 사라지지 않는다(`PetManagementView.cs:24-29`, 확인된 사실).

**실패 경로 (양호한 부분 — 확인된 사실, 1판과 동일)**:
- `PetMediaImporter.cs:23-24`: GIF 32 MiB / MP4 128 MiB 초과 시 구체적 안내.
- `PetMediaImporter.cs:28`: ffmpeg 미탑재 배포본 → "영상 변환 도구가 없는 앱이에요. MP4 지원 배포본을 사용하거나 GIF 파일을 가져와 주세요." (원인+대안 제시).
- `PetMediaImporter.cs:52,55,57`: 변환 시간초과, 손상 파일, 10초 초과 MP4에 각각 구체적 한국어 메시지.
- `CharacterLibrary.InspectInstallLocked`(`CharacterPack.cs:179-193`)의 ID 충돌/버전 역행/내용 변경 오류는 영어 메시지로 던져지지만, `Ui.ErrorText`(`Ui.cs:147-159`)가 정확히 매핑해 한국어로 치환한다 — **영어 예외 문자열 노출 없음**을 코드로 확인.
- `CustomPetWindow.cs:362-368` `ShowError`는 해당 예외들의 `error.Message`를 그대로 노출하는데, 모두 한국어로 던져짐 — 혼용 언어 없음.

### 재검증: UX-6 "빈 미리보기 안내 삭제" — (a) 승인된 의도로 재분류

- `docs/validation/2026-09-20-ui-copy-cleanup.md` 변경 목록: "펫 팩 열기·만들기의 **빈 미리보기 안내**, 사용법과
  파일 형식 설명을 제거했다." — `previewHint`가 `IsVisible = false`로 하드코딩된 것과 정확히 대응한다.
- **재확인 결과**: 텍스트 캡션 제거는 승인된 범위이고, 그 자리를 대체할 그래픽 플레이스홀더(아이콘 등)를
  두라는 지시나 승인도 없었다. 따라서 "완전한 공백"은 문서가 의도한 결과와 부합한다. (a) 승인.
- 다만 시각적으로 빈 카드가 첫 진입자에게 충분한 단서를 주는지는 **디자인 판단 영역**(ui-visual-auditor
  영역)이며, 텍스트 부재 자체를 회귀로 보지 않는다. 제안 수준으로 격하해 P3으로 낮춘다.

### 재검증: UX-7 "GIF/MP4 사전 안내 삭제" — (a) 승인된 의도로 재분류

- 같은 `ui-copy-cleanup.md` bullet의 "**사용법과 파일 형식 설명을 제거했다**"에 정확히 대응. (a) 승인.
- 실패 시 안내(`PetMediaImporter.cs`)는 `ui-copy-cleanup.md`의 "실제 저장·내보내기·설치 성공·실패 피드백은
  유지했다"는 원칙과 일치하게 그대로 남아 있음을 코드로 재확인했다. 사전 안내가 사라져도 실패 시 원인을
  알 수 있으므로 실질적 마찰은 낮다 — P2에서 P3으로 완화.

- 만들기 탭 상단 상태 캡션("쉬는 모습은 필수예요…")이 삭제되고, 필수 표시는 개별 행동 카드의 "필수 · 반복" 라벨(`CustomPetWindow.cs:114`)에만 남는다. 이 역시 `ui-copy-cleanup.md`의 "펫 팩 열기·만들기의 … 사용법 … 설명을 제거했다"로 커버되는 (a) 승인 범위로 재분류한다. 다만 비활성 "펫 팩 만들기…" 버튼에 이유를 설명하는 툴팁이 없다는 관찰은 어떤 시안에도 언급된 적 없는 별개의(승인/비승인 대상이 아닌) 관찰이므로 낮은 우선순위 제안으로 유지한다(P3).
- 성공 메시지 축소("펫 팩을 저장했어요. '펫 팩 열기' 탭에서 설치할 수 있어요." → "펫 팩을 저장했어요.")도 같은 bullet의 "사용법 설명 제거"로 커버되는 (a) 승인 범위다. 실제로는 저장 직후 자동으로 열기 탭에 파일이 로드되는 콜백(`CustomPetWindow.cs:19`, `PetManagementView.cs:25-29`)이 있어 문구 손실이 흐름 단절로 이어지지 않음(확인된 사실) — P3.
- "설치했어요."로 축소된 설치 성공 메시지도 동일하게 (a) 승인 범위이며, `SelectInstalledCharacter` 콜백이 자동으로 캐릭터를 선택하므로 행동 손실이 없음(확인된 사실) — P3.

**심각도**: UX-6·UX-7 모두 (a) 승인된 의도로 재분류, 각각 P3으로 완화. 회귀로 볼 근거는 없다.

---

## 흐름 6 — 설정

**사용자 목표**: 탭 구조 파악, 변경 감지, 취소/저장, 적용 시점 이해, 테마 전환, 디버그 섹션.

**실제 단계**: `SettingsWindow.Layout.cs:49-69` 좌측 아이콘 레일(타이머/설정/기록/펫 추가 + 테마 버튼 + 종료), `SelectNavigation`이 현재 탭에 `AutomationProperties`로 "· 선택됨"을 부여(접근성 신호는 있으나 시각 레이블은 아이콘뿐 — 시각 계층 이슈는 ui-visual-auditor 영역).

**설정(환경설정) 탭 — 적용 시점**:
- `SettingsWindow.Preferences.cs:45-51` `RefreshPreferencesState()`가 저장 버튼을 "현재 폼 값 ≠ 저장된 설정"일 때만 활성화 — 변경 감지가 정확하다(확인된 사실).
- 실패 시엔 `ShowPreferencesError("설정을 저장하지 못했어요. " + Ui.ErrorText(error))`로 원인 포함(확인된 사실, 양호).

### 재검증: UX-9 "설정 탭 저장 성공 피드백 부재" — (c) 두 개의 개별 승인 결정이 만든 잔여 불일치로 재분류

- `docs/plans/2026-09-18-settings-ui-redesign.md`의 "저장·취소 상태 계약": "처음 열거나 저장을 마친 상태에서는
  저장 버튼을 비활성화한다." 그리고 앞부분에 "**저장 유도 문구는 표시하지 않는다.**" — 즉 저장 성공을
  버튼의 활성/비활성 전환만으로 알리는 것이 **명시적으로 승인된 설계**다. (a) 승인.
- 반면 홈 "시간 설정" 카드의 명시적 "저장했어요." 문구는 같은 문서 구현 결과에 "**홈의 시간 설정 카드와
  적용 버튼은 기존 동작을 유지한다**"고 별도로 명시돼 있다 — 이 카드는 의도적으로 개편 범위 밖에 남겨 두어
  옛 방식(명시적 텍스트 피드백)을 그대로 유지한 것이다. 이것도 (a) 승인.
- **재분류**: 두 결정 모두 개별적으로는 승인됐지만, 그 결과 같은 창 안에서 "저장 성공을 알리는 방식"이
  카드마다 다른 **잔여 불일치**가 남았다. 이는 회귀가 아니라 **두 개의 정당한 결정이 조합되며 생긴 결과**이므로
  (c)로 분류한다. 실사용자 마찰은 낮음(버튼이 비활성화되는 시각적 신호 자체는 존재) — P2 유지, 다만 "발견"이
  아니라 "설계 결정의 부수효과"임을 명시한다.

### 재검증: UX-3 "미저장 설정, 종료 시 무경고 손실" — 승인 문서 없음, (b) 진짜 미승인 gap으로 확정

- `SettingsWindow.cs:149-161` `CanCloseDraft()`는 **`petPage`(커스텀 펫 제작 초안)만** 검사한다. `AppRuntime.Quit()`(`AppRuntime.cs:382-393`)과 macOS/OS 종료 요청(`AppRuntime.cs:99-103`, `desktop.ShutdownRequested`)이 모두 이 `CanCloseDraft()`를 거치는데, 홈 시간 설정 또는 환경설정 탭의 미저장 변경에 대해서는 어떤 검사도 하지 않는다.
- `git diff -- src/Unfold.Desktop/SettingsWindow.cs`로 확인한 결과 `CanCloseDraft()` 메서드 본문은 이번
  미커밋 변경에서 **손대지 않았다** — 즉 이번 diff가 만든 회귀가 아니라 **원래부터 있던 gap**이다.
- `docs/plans/2026-09-18-settings-ui-redesign.md` 도입부: "자동 저장, 효과음 재생 방식, 자리 비움 판정,
  다시 알림 동작과 그 밖의 새 기능은 포함하지 않는다. **일반적인 UX 후속 작업은 UI 완료 후 남은 문제를
  다시 확인해 별도 계획으로 정한다.**" — 종료 시 미저장 변경 보호는 이 "저장·취소 상태 계약" 절 어디에도
  언급되지 않는다. 즉 이 항목은 **승인되지도, 명시적으로 제외되지도 않은 채** "별도 계획"으로 미뤄졌고,
  본 감사에서 확인한 문서 목록(`docs/README.md`) 어디에도 그 "별도 계획"이 존재하지 않는다.
- 재현 경로(코드 추론, 실행 검증 아님): 환경설정 탭에서 값을 바꾸고 저장을 누르지 않은 채 트레이 메뉴
  "Unfold 종료"를 누르면, `Quit()`은 `petPage`에 초안이 없으면 바로 `quitting = true`로 넘어가 앱이
  종료된다 — 확인 대화상자 없이 변경이 사라진다.

**재분류**: (b) 문서화되지 않은 gap — 승인/거부 이력이 전혀 없고, 같은 창 안에서 커스텀 펫 초안만 보호받는
비대칭이 실질적으로 존재한다. 감독자 지시대로 **낮추지 않고 P1을 유지**한다.

**테마 전환**: `SettingsWindow.Layout.cs:65-66` `BuildThemeButton()`이 사이드바 하단에 있음. `SetTheme`(`AppRuntime.cs:185-193`)은 즉시 저장+적용되며 별도 확인 없이 동작 — 테마는 파괴적이지 않으므로 즉시 적용이 적절(문제 없음, 1판과 동일).

**디버그 섹션**: `SettingsWindow.Preferences.cs:74,81-82` `debugToolsEnabled`가 꺼지면 `runtime.CloseReminderPreview()`를 호출해 미리보기 알림을 정리 — 부수효과가 명확히 처리됨(확인된 사실, 양호, 1판과 동일).

---

## 발견 표 (재분류 반영)

| ID | 심각도 (1판→2판) | 분류 | 흐름 | 파일:줄 | 증상 | 사용자 영향 시나리오 | 권장 조치 / 재분류 근거 |
|---|---|---|---|---|---|---|---|
| UX-1 | ~~P0~~ → **반증, 회귀 아님** | (a) 승인 | 타이머·세션 | `PetSpeechBubble.cs`, `BreakReminderTests.cs:174`, `SmokeDiagnostics.cs:245-257` | 휴식 중 말풍선에 루틴 단계 안내가 없음 | 없음 — 사용자 요청·승인된 디자인 | `speech-ui-proposal.md`가 명시적으로 요청, 테스트가 "나타나면 실패"로 계약화. 조치 불필요 |
| UX-2 | ~~P1~~ → **반증, 회귀 아님** | (a) 승인 | 타이머·세션 | `PetSpeechBubble.cs`, `ui-copy-cleanup.md`, `BreakReminderTests.cs:175-176` | +60분 도달 안내 없음 | 없음 — 승인된 문구 제거 | `ui-copy-cleanup.md`가 "+60분 안내 문구를 제거했다"고 명시. 조치 불필요 |
| **UX-12(신규)** | **P2** | 정합성 문제(회귀 아님) | 타이머·세션 | `BreakSession.cs:3,12`, `RoutineEditorWindow.cs:25,39`, `PersonalizationWindow.cs:126`, `SmokeDiagnostics.cs:83` | `BreakStep.Instruction`이 필수·검증 대상이지만 표시 경로가 0곳(전수 grep 확인). 편집 경로(`RoutineEditorWindow`)는 진단 전용이라 일반 사용자는 도달 불가 | 오늘 시점 일반 사용자 영향 없음. 레거시 커스텀 루틴 보유자는 이미 2026-09-17 승인된 UI 제거의 연장. 유지보수 리스크(검증 규칙과 실제 소비처 불일치)가 핵심 | 필드를 선택으로 완화하거나, 진단 전용 경로임을 문서화해 검증 규칙과 실제 용도를 일치시킬 것 |
| UX-3 | **P1 유지** | (b) 미승인 gap | 설정 | `SettingsWindow.cs:149-161` `CanCloseDraft()` | 환경설정·홈 시간 카드의 미저장 변경이 종료 시 확인 없이 사라짐. `settings-ui-redesign.md`가 "별도 계획"으로 미뤘으나 그 계획을 찾지 못함 | 환경설정을 바꾸고 저장 전에 트레이 "Unfold 종료"를 누르면 변경이 소리 없이 사라짐 | `CanCloseDraft()`에 `preferencesSave.IsEnabled` 확인 추가 |
| UX-4 | P1 → **P2** | (a) 승인 + (c) 잔여 위험 | 기록 보기 | `BreakReviewWindow.cs`, `ui-copy-cleanup.md`("기록 탭의 집계·저장 방식 설명…제거"), `personalization.md:26` | CSV가 7일만 포함된다는 사실을 화면에서 알 수 없음(문서에는 있으나 사용자가 보는 화면엔 없음) | 여러 주 데이터를 모으려는 사용자가 한 번의 내보내기로 전체가 담겼다고 오해 | 캡션 복원까지는 아니더라도 내보내기 버튼 근처에 범위 표시를 고려. 승인된 트레이드오프이므로 필수는 아님 |
| UX-5 | **P2 유지** | (c) 승인 범위 내 축소 | 기록 보기 | `BreakReviewWindow.cs`, CSV 실패 메시지 축소 | "이 기기의 저장 가능한 위치를 선택해 다시 시도해 주세요." → "회고를 내보내지 못했어요." | 실패 자체는 알지만 구체적 재시도 방법은 스스로 추측 | `ui-copy-cleanup.md`의 "실패 피드백 유지" 원칙을 "핵심 신호 유지"로 해석해도, 재시도 지침 복원을 검토할 여지는 있음 |
| UX-6 | P2 → **P3** | (a) 승인 | 펫 추가·팩 | `CustomPetWindow.cs:43,145`, `PetPackWindow.cs:46,148` | 빈 미리보기 영역에 안내 텍스트 없음 | 첫 진입자가 빈 카드만 봄(단, `ui-copy-cleanup.md`가 "빈 미리보기 안내…제거"를 명시) | 텍스트 복원은 승인 범위 밖. 시각적 플레이스홀더는 ui-visual-auditor 영역의 제안 사항으로 넘김 |
| UX-7 | P2 → **P3** | (a) 승인 | 펫 추가·팩 | `CustomPetWindow.cs`, GIF/MP4 사전 안내 제거 | 가져오기 전 형식·용량 제한을 알 수 없음 | 대용량 MP4 실패 후에야 제약을 인지(단, 실패 메시지는 견고, 확인된 사실) | `ui-copy-cleanup.md`의 "사용법과 파일 형식 설명을 제거했다"에 명시적으로 포함. 조치 불필요 |
| UX-8 | P2 → **재분류: 제안(순수 신규 아이디어)** | 해당 없음(회귀/승인 판정 대상 아님) | 타이머·세션 | `PetSpeechBubble.cs:26-34` | 초대 말풍선에 경량 "건너뛰기"가 없음 | 지금 쉬고 싶지 않지만 타이머는 유지하고 싶은 사용자가 스누즈 또는 전체 정지(Stop)만 선택 가능 | 어떤 시안에도 등장한 적 없는 기능 제안. 회귀 아님 — 제품 결정 필요 |
| UX-9 | **P2 유지, 원인 재분류** | (c) 두 승인 결정의 조합 | 설정 | `SettingsWindow.Preferences.cs:101-124` vs `SettingsWindow.cs:169-170` | 환경설정 탭 저장은 버튼 비활성화로만, 홈 카드는 "저장했어요." 문구로 알림 | 환경설정 저장 후 확신이 안 서서 재확인 행동을 할 수 있음 | 두 결정 모두 개별적으로 `settings-ui-redesign.md`에서 승인됨("저장 유도 문구 표시 안 함" vs "홈 카드는 기존 동작 유지"). 통합 여부는 제품 결정 |
| UX-10 | P3 → **반증, 회귀 아님** | (a) 승인 | 타이머·세션 | `PetSpeechBubble.cs`, `ui-copy-cleanup.md`("말풍선의 완료 안내…제거"), `BreakReminderTests.cs:38-40` | 완료 직후 요약 문구("n분 n초 쉬었어요") 없음 | 없음 — 홈 대시보드가 오늘 합계를 계속 보여주고, 문구 제거는 승인·테스트로 확정됨 | 조치 불필요 |
| UX-11 | **P3 유지** | 해당 없음(다뤄진 적 없음) | 기록 보기 | `BreakReviewWindow.cs`, 이전/다음 7일 경계 설명 없음 | "이전 7일" 버튼이 경계에서 조용히 비활성화 | 왜 더 못 가는지 궁금할 수 있음(회색 버튼으로 암시는 됨) | 어떤 시안에도 언급된 적 없는 별개 관찰. 우선순위 낮음 |

---

## 의도적 제외 / 승인 여부 재분류 요약

- **온보딩 화면 부재**: `docs/mvp.md`가 핵심 루프에 온보딩을 포함하지 않음. **의도적 제외**(1판과 동일).
- **UX-1, UX-2, UX-4, UX-6, UX-7, UX-10**: `docs/plans/2026-09-20-speech-ui-proposal.md`,
  `docs/validation/2026-09-20-speech-ui-implementation.md`, `docs/validation/2026-09-20-ui-copy-cleanup.md`가
  구체적으로 명시한 **(a) 승인된 의도**다. 1판은 이 문서들을 대조하지 않고 `git diff`만으로 "회귀"라 단정한
  오류를 범했다 — 감독자 지적을 그대로 반영해 정정한다.
- **UX-3**: `docs/plans/2026-09-18-settings-ui-redesign.md`가 스스로 "일반적인 UX 후속 작업은 별도 계획으로
  정한다"고 밝힌 항목이며, 그 별도 계획이 `docs/README.md` 문서 목록에 존재하지 않는다. **(b) 승인도 거부도
  되지 않은 채 방치된 gap**으로 확정.
- **UX-9**: 개별적으로는 두 결정 모두 `settings-ui-redesign.md`에서 승인됐으나, 조합 결과의 불일치는 어느
  문서도 명시적으로 다루지 않았다. **(c) 승인된 결정들의 잔여 부작용**.
- **UX-5**: `ui-copy-cleanup.md`의 "실패 피드백 유지" 원칙과 실제 축소된 문구 사이에 해석의 폭이 있다.
  **(c) 승인 범위 내 축소로 해석 가능하나 구체성 손실은 실재**.
- **UX-12(신규)**: 2026-09-17 `docs/personalization.md`가 승인한 "루틴 UI 제거"와 2026-09-20 "말풍선 본문
  제거"가 **겹쳐서 만들어 낸 결과**다. 각각은 개별적으로 승인됐지만, 그 결과 "필수 검증 필드가 어디서도
  표시되지 않는다"는 조합 효과는 어떤 문서도 명시적으로 검토하지 않았다. 실사용자 도달 경로가 없어 낮은
  심각도로 유지하되, 신규 발견으로 표에 추가했다.

---

## 확인된 사실 / 코드 추론 / 미검증 분리 (2판)

**확인된 사실** (코드·문서를 직접 읽고 대조):
- UX-1, UX-2, UX-4, UX-6, UX-7, UX-10의 승인 근거는 `docs/plans/2026-09-20-speech-ui-proposal.md`,
  `docs/validation/2026-09-20-speech-ui-implementation.md`, `docs/validation/2026-09-20-ui-copy-cleanup.md`를
  직접 읽어 원문 인용으로 확인했다.
- UX-1·UX-2가 테스트·스모크 진단으로 "표시되면 실패"하게 잠겨 있음을 `Tests/Unfold.Tests/BreakReminderTests.cs:33-37,174-176`와 `src/Unfold.Desktop/SmokeDiagnostics.cs:123,134,144,245-257`을 직접 읽어 확인했다(1판에서 놓친 부분).
- UX-12의 "표시 경로 0곳"은 `grep -rn "\.Instruction\b" src/Unfold.Desktop/*.cs src/Unfold.Core/*.cs` 전수 검색으로 확인했다(결과 6곳, 모두 정의/입력/금지-문자열 용도).
- UX-12의 "진단 전용 도달 경로"는 `grep -rn "new PersonalizationWindow\|new RoutineEditorWindow"`로 `PersonalizationWindow`가 `SmokeDiagnostics.cs`에서만 생성됨을 확인했다.
- UX-3의 `CanCloseDraft()` 메서드가 이번 미커밋 diff에서 수정되지 않았음(원래부터 있던 gap)을 `git diff -- src/Unfold.Desktop/SettingsWindow.cs`로 확인했다.
- UX-9의 두 결정이 각각 `docs/plans/2026-09-18-settings-ui-redesign.md`에 명시돼 있음을 원문으로 확인했다.
- 펫 팩 설치 오류 메시지가 영어로 던져지되 `Ui.ErrorText`가 한국어로 정확히 매핑하는 체계는 코드로 확인, 혼용 언어 노출 없음(1판과 동일).

**코드 추론** (실행하지 않고 로직으로 도출, 실제 화면에서 보이는 방식은 미검증):
- UX-3의 재현 시나리오(트레이 종료 시 무경고 손실)는 코드 흐름상 성립하나 실제 macOS/Windows에서 트레이 메뉴를 클릭해 재현하지는 않음.
- UX-12의 "레거시 사용자 영향"은 `BreakRoutines.ForSettings`가 `settings.json`의 `CustomRoutine`을 계속 로드한다는 코드 로직에서 도출했으며, 실제로 2026-09-17 이전 버전에서 저장된 실제 사용자 데이터를 갖고 재현하지는 않음.
- UX-6의 "완전한 공백"이 실제 화면에서 시각적으로 어떻게 보이는지(배경 대비, 카드 테두리)는 시각 판단이 필요해 ui-visual-auditor 영역과 겹친다 — 본 감사는 텍스트 부재만 코드로 확인.

**미검증 (실행 불가)**:
- 실제 macOS/Windows에서 포인터·키보드로 각 흐름을 조작한 결과.
- `artifacts/verification/`의 캡처는 구버전(2026-09-13/14)이라 현재 UI 검증에 사용하지 않음. `docs/validation/2026-09-20-*.md`가 참조하는 최신 캡처는 파일 목록만 확인, 실제 픽셀 검토는 하지 않음.
- 미커밋 변경이 `dotnet test`를 통과하는지, Release 빌드가 정상인지는 이번 감사 범위 밖(다른 워커가 빌드 소유). 단, `BreakReminderTests.cs`·`SmokeDiagnostics.cs`의 코드 자체는 읽어 계약 내용을 확인했다(실행 결과는 아님).
