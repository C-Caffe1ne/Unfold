# 09 작업 계획 — Agency Agents 감사 통합 (2026-10-06)

## 1. 기준과 경계

| 항목 | 값 |
|---|---|
| 감사 대상 | `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold` |
| 브랜치 | `codex/glb-import-compat` |
| 감사 시작 HEAD | `1a74cbf` (`1.1.0-beta`) |
| 감사 종료 HEAD | `bbdb4a8` (`1.1.1-beta`) — 감사 중 다른 세션이 2커밋 추가 |
| 최종 판정 기준 | `bbdb4a8` 작업 트리 (clean), 줄 번호 모두 이 기준 |
| 서버(결제) 소스 | `codex/beta-v1.0.4` `95f7a21` — 앱 브랜치 미병합, 이번 범위 밖 |

이번 단계는 **감사만** 수행했다. 제품 코드는 한 줄도 바꾸지 않았고, 대상 워크트리에는 읽기만 했다.

### 수행한 실행 근거

| 실행 | 결과 |
|---|---|
| 격리 복사본 `restore --locked-mode` (1a74cbf) | 종료 0 |
| 격리 복사본 `src/Unfold.Desktop` Release 빌드 (1a74cbf) | 경고 0, 오류 0 |
| `--smoke-test` (새 `UNFOLD_DATA_DIR`, 1a74cbf) | `success=true`, 캡처 112개 생성 |
| 격리 복사본 재동기화 + 빌드 (bbdb4a8) | restore 0, 빌드 경고 0·오류 0 |
| `--smoke-test` 재실행 (bbdb4a8) | **실패 2회** — Avalonia 렌더 타이머 시작 불가(native error `-6661`). 환경 제약이며 제품 결함이 아니다 |
| 저장된 전체 테스트 결과 재집계 (`final-full.trx`) | 748건 중 **746 통과 / 2 실패** |

전체 테스트는 이번에 재실행하지 않았다. 디스크 여유가 약 6~10GiB였고 테스트 프로젝트 빌드를 피했다.

### 이번 감사가 확정하지 못한 것

- `bbdb4a8` 설정 화면의 실제 모습. 캡처는 `1a74cbf` 것뿐이며, 그 사이 말풍선 설정이
  **방향 선택 → 투명도 + 대사 5종 직접 입력 + 자동 방향**으로 교체됐다. 설정 화면 시각 판정은 재캡처 전까지 보류다.
- 실제 Windows 입력·DPI·GPU, 장시간 메모리, 설치 업데이트, 실거래·OAuth, VoiceOver·Narrator 실측.
  기존 출시 게이트와 동일하며 이번 감사로 해소되지 않았다.

### 투입한 Agency Agents

| ID | 에이전트 | 보고서 | 제출 건수 |
|---|---|---|---|
| A1 | Code Reviewer | `01_bug_core_review.md` | 12 |
| A2 | Desktop App Engineer | `02_bug_desktop_runtime.md` | 14 |
| A3 | Evidence Collector | `03_runtime_evidence.md` | 15 |
| B1 | UX Researcher | `04_ux_heuristic.md` | 15 |
| B2 | Persona Walkthrough Specialist | `05_persona_walkthrough.md` | 15 |
| B3 | UI Designer | `06_ui_visual.md` | 15 |
| B4 | Accessibility Auditor | `07_accessibility.md` | 14 |
| C1 | Reality Checker | `08_reality_check.md` | 86건 재분류 |

C1 판정: 채택 6 · 하향 5 · 상향 1 · 기각 1 · 낡음 3 · 중복 24건을 11묶음으로 통합.
감독자가 추가로 직접 확인한 항목: 로그아웃 버튼 노출, 기록 90일 정리, 기록 손상 처리,
업데이트 백엔드 생성 위치, Windows layered 비대칭 복원, `LiveSetting` 부재, 움직임 축소 미지원,
30초 고정 타임아웃, 루틴 단계 미표시, 테스트 746/748.

**P0 없음.** 유일한 P0 후보(시계 전진 시 기록 삭제)는 트리거 조건이 좁아 P1로 내렸다.

---

## 2. P1 — 출시 전 처리

| ID | 제목 | 근거 | 영향 |
|---|---|---|---|
| ISSUE-01 | 90일 기록 정리가 호출 시각만 신뢰. 시계가 90일 이상 앞으로 간 상태에서 휴식 1건을 완료하면 이전 기록 전체가 지워지고 즉시 저장되어 복구 불가 | `BreakHistory.cs:40`, `AppRuntime.cs:564-566`. 90일 정리 경로는 `Add` 하나뿐, `Load`는 정리 안 함. 백업 없음, 테스트 0건 | 저확률·고영향. 수동 날짜 변경, 오래된 VM 스냅샷 재개에서 성립 |
| ISSUE-02 | 손상 처리 비대칭. 설정은 `.invalid-*` 사본을 남기고 복구하지만 기록은 격리·복구가 없고 그 실행에서 저장이 꺼진다. 안내 문구는 "앱을 종료할 때까지만"이라 영구 비활성을 잘못 전달. 레코드 1건이 검증을 못 넘기면 90일 전체를 버린다. 설정 `.invalid-*`는 상한 없이 누적 | `AppRuntime.cs:110-115`, `:97-106`, `BreakHistory.cs:23-31`, `AppSettings.cs:55` | 사용자가 되돌릴 방법이 없다 |
| ISSUE-03 | 유료 전환 지점 고지 부재. 로그인 단계에 가격·1회 구매·약관·개인정보 안내가 없고 대안은 앱 종료뿐. 가격은 결제 단계에만 나온다 | `Assets/Account/entry-screen.json`(약관·개인정보 문자열 0건), `AccountWindow.axaml:48-52`(`IsVisible="{Binding IsPurchase}"`), `AccountScreenModel.cs:46` | 구글 계정을 연결한 뒤에 유료임을 알게 된다. 스토어·법적 고지 요건과도 관련 |
| ISSUE-04 | 키보드 포커스 표시 부재. `DesignSystem.cs:118`이 `TextBox`·`NumericUpDown`·`ComboBox`의 `FocusAdorner`를 `null`로 지우고, 이어지는 `:121-125`의 `:focus` 스타일이 `BorderBrush`를 기본값과 같은 값으로 되돌려 아무 변화도 만들지 않는다. 회고 날짜 토글은 테두리를 지워 포커스 표시가 없다 | `DesignSystem.cs:112-125`, `BreakReviewWindow.cs:176-181`, `:198-213` | 키보드 사용자가 현재 위치를 알 수 없다. 버튼은 `:focus-visible`이 있어 영향 없음 |
| ISSUE-05 | 검증 문서가 저장된 결과를 과장. `2026-10-06-audit-followup.md`는 "748개 모두 통과"라고 적었으나 `final-full.trx`는 746/748이고 같은 날짜 `summary.json`·`agency-execution.md`는 746으로 기록. followup이 인용한 증거 파일 8종이 워크트리에 없다 | `final-full.trx` 재집계, `summary.json` | 실패 2건은 테스트가 `/private/artifacts`에 쓰려다 난 권한 오류로 제품 결함이 아니다. 문제는 기록의 정확성이며 이후 판단의 근거가 된다 |

---

## 3. P2 — 출시 품질

| ID | 제목 | 근거 |
|---|---|---|
| ISSUE-06 | 팔레트 근접도. `Canvas`/`Shell`/`Surface`/`Raised`/`DisabledFill`/`Hover`가 네 테마 전부 **1.04~1.37:1** 안에 몰려 카드 경계·버튼 면·비활성 상태·호버가 보이지 않는다. 비활성 버튼과 활성 버튼 면색 대비는 **1.04:1** | `DesignSystem.Themes.cs:17-31` hex 재계산(§08 §4). 텍스트 토큰과 `OutlineStrong`은 전부 기준 통과 → `Raised`·`DisabledFill`·`Outline`·`Hover` 4개 값만 조정하면 된다 |
| ISSUE-07 | 저장 모델 불일치. 설정은 자동 저장인데 성공 피드백이 없고(`preferencesStatus`는 오류 전용), 홈은 수동 저장인데 버튼이 `Opacity=0`으로 숨어 있다. 타이머 진행 중 간격 입력이 비활성인 이유가 화면에 남지 않는다 | `SettingsWindow.Preferences.cs:27,76-78,102,143`, `SettingsWindow.cs:160,243-245` |
| ISSUE-08 | 펫 팩 설치 흐름. `저장` 라벨 하나가 설치·업데이트·재설치·교체를 모두 수행하고 확인 단계가 없다. 성공 후 `.backup-*`을 지워 되돌릴 수 없다 | `PetPackWindow.cs:61,209-230`, `CharacterPack.cs:225`. 완화: `:178-182`가 클릭 전 "교체 준비 완료"와 설치 버전을 표시하고, `CharacterPack.cs:186-187`이 사용자 자작 펫 교체를 차단 |
| ISSUE-09 | 말풍선 시간 제약이 고정·비공개. 초대 30초 자동 미루기와 공지 5초 소멸에 예고·사후 안내·조정 수단이 없고, `src/`에 `LiveSetting`이 0회라 보조기술에 아무것도 알려지지 않는다 | `PetReminder.cs:10`, `PetSpeechBubble.cs`, `src/` 전체 grep |
| ISSUE-10 | 용어·라벨 불일치. 같은 동작 키의 한국어 이름이 화면마다 3종, 미루기 용어 3종, `펫 선택`이 두 화면에서 다른 의미 | `CustomPetDraft.cs:51-55`, `GlbPetDraft.cs:10-14`, `PetPackWindow.cs:117-119` |
| ISSUE-11 | 펫 창 발견성·복구. 우클릭 메뉴가 2항목뿐이고 `펫 숨기기` 후 복구 경로를 알리지 않으며 키보드 진입이 트레이 1경로 | `PetWindow.cs`, `AppRuntime.cs` |
| A11Y-10 | 말풍선이 고정 높이·고정 폭 320px인데 `bbdb4a8`가 120자 사용자 대사 5종을 추가해, 설정에서 입력한 문장이 소리 없이 잘릴 수 있다. 완화는 마우스 전용 툴팁뿐 | `SettingsWindow.Preferences.cs:16`, `PetSpeechBubble.cs:29,76-83`, `DesignSystem.cs:30-31` |
| A11Y-06 | 움직임 줄이기 미지원. `src/`에 `AnimationsEnabled=false`가 없고 OS 모션 감소 설정 조회도 없어 380ms 숫자 롤링이 항상 재생된다 | `src/` 전체 grep |
| PW-03 | 휴식을 시작해도 무엇을 할지 안내가 없다. `BreakSession.CurrentStep`은 코어에 있으나 UI에서 호출되지 않고 테스트에서만 쓰인다. `docs/mvp.md`는 루틴이 안내 문구 순서를 제공한다고 적어 문서와 화면이 어긋난다 | `PetSpeechBubble.cs:81-87`, `BreakSession.cs:77-83` |
| BUG-D-01 | Windows 클릭 통과 해제 시 직접 추가한 `WS_EX_LAYERED`를 되돌리지 않는다. 코드 비대칭은 확정, 실제 렌더 영향은 미확정 | `WindowsPetWindow.cs:26` |
| BUG-D-02 | 알림 효과음이 UI 스레드에서 최대 5MiB WAV를 2회 읽고 샘플 단위 볼륨 변환 후 임시 파일까지 쓴다. 알림이 뜨는 순간 펫·말풍선 전환이 끊긴다 | `ReminderSoundPlayer.cs:46-78`, `ReminderSounds.Cleanup.cs:9-16` |
| BUG-D-03 | 커스텀 펫 파일 배정이 GIF 전체를 UI 스레드에서 디코드하고 직후 같은 디코드를 백그라운드에서 중복 수행 | `CustomPetWindow.cs:268` |
| BUG-D-06 | 데이터 디렉터리 생성·인스턴스 락 실패가 처리되지 않아 로그 없이 크래시하거나, 모든 `IOException`을 "중복 실행"으로 보고 종료 코드 0으로 조용히 사라진다 | `Program.cs:67-73` |
| BUG-C-05 | 업데이트 백엔드 생성이 생성자 `try` 블록 밖. Velopack 로케이터·채널 실패가 트레이 구성과 시작 흐름 전체를 끊어 타이머가 시작되지 않는다 | `AppUpdates.cs:84`, `AppRuntime.Updates.cs:12,20`, `AppRuntime.cs:154-161` |
| BUG-C-04 | 설정 로드 catch 필터에 `ArgumentException`이 빠져(같은 코드베이스 다른 곳은 포함) 개인화 검증 실패 시 앱이 조용히 종료 코드 1로 끝난다. 도달 경로 미특정 | `AppRuntime.cs:92-96` vs `:187`, `CharacterLibrary.cs:157` |
| EV-01 | 로그인하지 않은 상태에서도 설정 계정 카드의 `로그아웃`이 보이고 활성. 노출·활성 조건이 세션이 아닌 `AccountSignOutPending`에만 묶여 있다 | `SettingsWindow.Layout.cs:307-312` |
| UI-02 | 확인 대화상자 버튼 위계를 라벨 문자열로 판정한다. `choices[0]`이 파괴적 라벨이면 `Primary`가 어느 버튼에도 붙지 않고, `"제거하기"`처럼 목록에 없는 라벨은 파괴적 동작에 `Primary`가 붙는다 | `Ui.cs:227-231` |
| UI-03 | `Success` 토큰이 전체에서 타이머 상태 점 1곳에만 쓰이고 저장 완료 메시지 3곳은 중립 회색. 토큰 대비는 충분하므로 색 선택이 아니라 적용 누락 | `SettingsWindow.cs:211` |
| EV-03 | 긴 펫 이름이 말줄임 없이 글자 중간에서 잘린다 | `home-minimum-long-name-error.png` |

---

## 4. P3 — 이후

| ID | 제목 | 소유 후보 |
|---|---|---|
| BUG-C-09 | 회고 이동 하한 `-77`이 90일 보존과 정렬되지 않아 `today-84`~`today-90`의 7일에 도달할 수 없다 | `BreakReviewWindow.cs:302,154` |
| BUG-D-12/13 | GLB 렌더 스크래치가 최대 요청 래스터(최대 약 32MiB)로 모델 수명 동안 상주하고 축소되지 않으며, 펫과 편집기가 같은 모델을 공유하면 단일 lock으로 직렬화된다 | `GlbModel.Rendering.cs`, `GlbPetView.cs` |
| BUG-D-14 | 알림 중 드래그로 화면 중앙선을 넘을 때 말풍선 좌↔우가 1회 전환 | `PetWindow.cs:246-247` |
| UI-04~UI-09 | 모달 들여쓰기, 설정 그룹 근접성, 홈 스크롤바 겹침, 기록 아이콘 버튼 3종 불일치, 기준선 어긋남, 라벨 패턴 4종 | `Ui.cs`, `SettingsWindow.Layout.cs`, `PetManagementView.cs` |
| A11Y-12 | 비활성 텍스트 대비 3.33~3.56:1 (WCAG 비활성 면제, 개선 항목) | `DesignSystem.Themes.cs` |
| EV-02 | 픽셀 에디터 화면만 UI 전체가 영어 | `EditorWindow.cs:46-49` — **범위 결정 필요(§7)** |

---

## 5. 실행 큐 — 파일 소유권과 수용 기준

파일 소유권이 겹치지 않는 작업만 같은 단계에서 병렬로 돈다. 각 단계 종료 후 QA가 경계를 검증한다.

### 선행 조건 (코드 작업 시작 전)

| # | 항목 | 이유 |
|---|---|---|
| P-1 | **대상 워크트리 사용권 정리** | `/Users/hwanghyeonseong/.codex/worktrees/beta-v1-1-0/Unfold`에서 다른 Codex 세션이 작업 중이며 감사 중에도 2커밋이 들어왔다. 같은 파일을 동시에 고치면 충돌한다. 어느 세션이 어느 파일을 갖는지 먼저 정한다 |
| P-2 | **진단 캡처 복구** | `--smoke-test`가 Avalonia 렌더 타이머 `-6661`로 실패한다. 화면 세션 확보나 소프트웨어 렌더러 선택으로 복구해야 `bbdb4a8` 기준 시각 검증이 가능하다. ISSUE-06·07, A11Y-10, UI-04~09의 수용 기준이 이것에 달려 있다 |

### 1단계 — P1 (병렬 4)

| 작업 | 소유 파일 | 처리 항목 | 수용 기준 |
|---|---|---|---|
| T1 데이터 보존 | `src/Unfold.Core/BreakHistory.cs`, `src/Unfold.Desktop/AppRuntime.cs`, `src/Unfold.Core/AppSettings.Recovery.cs`, `Tests/Unfold.Tests/` 해당 파일 | ISSUE-01, ISSUE-02, BUG-C-04 | 시각 주입 단위 테스트: `Add(s1, now)` → `Add(s2, now.AddDays(91))` 후 기존 기록이 남는다. 손상 기록 파일이 격리 사본으로 보존되고 다음 실행에서 복구된다. 레코드 1건 손상이 전체를 버리지 않는다. `.invalid-*` 사본에 상한이 있다. 안내 문구가 실제 지속 범위와 일치한다 |
| T2 계정·고지 | `Assets/Account/entry-screen.json`, `src/Unfold.Desktop/AccountWindow.axaml`, `AccountScreenContent.cs`, `AccountScreenModel.cs`, `SettingsWindow.Layout.cs` | ISSUE-03, EV-01, 네비게이션 레일 포커스·선택 역할 | 로그인 단계에 가격·1회 구매·약관·개인정보 고지가 보인다. 세션이 없으면 `로그아웃`이 보이지 않는다. 레일 항목에 포커스 표시와 선택 상태가 전달된다. 최소 창에서 잘리지 않는다 |
| T3 포커스 표시 | `src/Unfold.Desktop/DesignSystem.cs`, `BreakReviewWindow.cs` | ISSUE-04 | Tab만으로 홈·설정·회고를 순회할 때 매 정지점에 보이는 포커스 표시가 있다. 입력 3종의 포커스 상태가 기본 상태와 다르다. 회고 날짜 토글에 포커스 표시가 있다. 네 테마 모두에서 확인 |
| T4 검증 기록 정정 | `docs/validation/2026-10-06-audit-followup.md`, `docs/verification.md` | ISSUE-05 | 저장된 TRX 수치와 문서가 일치한다. 실패 2건의 원인(테스트 출력 경로 권한)과 제품 무관함을 구분해 남긴다. 존재하지 않는 증거 파일 인용을 정리한다. 기존 실패 기록을 지우지 않는다 |

T3와 T5는 `DesignSystem.cs`를 공유하므로 **직렬**이다. T4는 문서만 다루므로 코드 작업과 충돌하지 않는다.

### 2단계 — P2 (병렬 4, T3 완료 후 T5 시작)

| 작업 | 소유 파일 | 처리 항목 | 수용 기준 |
|---|---|---|---|
| T5 팔레트 분리 | `src/Unfold.Desktop/DesignSystem.Themes.cs`, `DesignSystem.cs` | ISSUE-06 | `Raised`·`DisabledFill`·`Outline`·`Hover` 조정 후 면·면 경계 대비가 네 테마 모두 3:1 이상. 텍스트 토큰 대비는 기존 통과 값을 유지. 재계산표를 근거로 첨부 |
| T6 저장 모델·피드백 | `src/Unfold.Desktop/SettingsWindow.Preferences.cs`, `SettingsWindow.cs` | ISSUE-07, UX-05, UI-03 | 자동 저장 성공이 화면에 표시되고 `Success` 토큰을 쓴다. 표시·숨김이 레이아웃을 밀지 않는다. 홈 저장 버튼이 항상 보이고 상태로 구분된다. 간격 입력 비활성 이유가 화면에 남는다 |
| T7 말풍선·시간 제약 | `src/Unfold.Core/PetReminder.cs`, `src/Unfold.Desktop/PetSpeechBubble.cs`, `PetWindow.cs` | ISSUE-09, A11Y-10, PW-03, BUG-D-14 | 자동 미루기 전 예고와 사후 안내가 있고 조정 수단이 결정된 범위대로 동작한다. 상태 변화가 보조기술에 전달된다. 120자 대사가 잘리지 않거나 잘릴 때 전문 접근 경로가 키보드로도 있다. 휴식 중 루틴 단계가 표시된다 |
| T8 펫 팩 흐름 | `src/Unfold.Desktop/PetPackWindow.cs`, `Ui.cs` | ISSUE-08, UI-02 | 교체는 되돌릴 수 없음을 알리는 확인 단계를 지난다. 버튼 라벨이 수행할 동작을 말한다. 대화상자 위계가 라벨 문자열이 아닌 명시적 지정으로 결정된다 |
| T9 UI 스레드 작업 분리 | `src/Unfold.Desktop/ReminderSoundPlayer.cs`, `src/Unfold.Core/ReminderSounds.Cleanup.cs`, `CustomPetWindow.cs` | BUG-D-02, BUG-D-03 | 알림 순간 UI 스레드에서 파일 읽기·변환·쓰기가 사라진다. GIF 디코드가 1회만 수행된다. 기존 효과음·가져오기 회귀 검사 통과 |
| T10 시작 견고성 | `src/Unfold.Desktop/Program.cs`, `AppUpdates.cs`, `AppRuntime.Updates.cs` | BUG-C-05, BUG-D-06 | 업데이트 백엔드 생성 실패가 시작 흐름을 끊지 않고 타이머가 시작된다. 데이터 디렉터리·인스턴스 락 실패가 로그와 사용자 안내로 구분되고, 중복 실행이 아닌 `IOException`을 중복 실행으로 보고하지 않는다 |

### 3단계 — P2 잔여·P3 (선행 완료 후)

| 작업 | 소유 파일 | 선행 | 처리 항목 |
|---|---|---|---|
| T11 용어 통일 | `CustomPetDraft.cs`, `GlbPetDraft.cs`, `PetBuilderView.cs`, `PetPackWindow.cs`, `SettingsWindow.cs` | T6, T8 | ISSUE-10 |
| T12 펫 창 발견성 | `PetWindow.cs`, `AppRuntime.cs` | T1, T7 | ISSUE-11 |
| T13 움직임 축소 | `AnimatedCountdown.cs`, `AnimatedTimeText.cs`, `AppSettings.cs` | T7 | A11Y-06 |
| T14 회고 경계 정렬 | `BreakReviewWindow.cs` | T3 | BUG-C-09 |
| T15 GLB 메모리·공유 | `GlbModel.Rendering.cs`, `GlbPetView.cs` | — | BUG-D-12/13 |
| T16 시각 다듬기 | `Ui.cs`, `SettingsWindow.Layout.cs`, `PetManagementView.cs` | T8(`Ui.cs`), P-2 | UI-04~UI-09, EV-03 |

### 실제 OS·환경 게이트 (코드 작업과 병행하지 않음)

| # | 항목 | 절차 |
|---|---|---|
| G-1 | Windows 클릭 통과의 layered 잔존 영향 | 실제 Windows에서 펫 표시 → 클릭 통과 해제 → 재설정을 20회 반복하며 알파·말풍선 테두리·배경 비침을 캡처 비교하고, 매 사이클 `GetWindowLongPtr(hwnd, -20)`으로 `Layered` 비트 잔존을 기록. DWM 합성 중지(원격 데스크톱) 조건 포함 |
| G-2 | 보조기술 실측 | macOS VoiceOver·Windows Narrator로 말풍선 등장·타이머 상태 변화·저장 결과·오류 전달 확인 |
| G-3 | 장시간 성능 | 고정 모델·해상도·기기에서 실제 앱 30분 이상 RSS/managed/CPU 비교 |
| G-4 | 설치 업데이트·데이터 보존 | 구버전 → 업데이트 후 설정·기록·펫·로그인 보존 확인 |
| G-5 | 실거래·OAuth | 기존 출시 게이트 유지. 서버 소스가 앱 브랜치에 미병합인 점을 함께 처리 |

---

## 6. 검증 명령

```bash
dotnet restore Unfold.slnx --locked-mode
dotnet test Unfold.slnx -c Release --no-restore
```

진단은 매번 **새 빈** 디렉터리로 실행한다.

```bash
UNFOLD_DATA_DIR=<새 빈 디렉터리> dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test
```

단계별로 변경 범위에 맞는 집중 검사를 먼저 돌리고, 단계 종료 시 전체 Release 검사를 한 번 돌린다.
근거 없이 통과한 검사를 반복하지 않는다. 자동 검사 결과와 실제 OS 관찰을 구분해 기록한다.

---

## 7. 사용자 결정이 필요한 항목

| # | 항목 | 선택지 |
|---|---|---|
| D-1 | 자동 미루기 30초와 공지 5초 | 현재 고정값 유지 + 예고·사후 안내만 추가 / 설정에 노출해 조정 가능하게 / 접근성 기준(WCAG 2.2.1)에 맞춰 연장·해제 수단 제공 |
| D-2 | 로그인 전 고지 범위 | 가격·1회 구매만 / 약관·개인정보 링크까지. 후자는 공개할 약관·개인정보 문서가 먼저 필요하다 |
| D-3 | 픽셀 에디터 영어 UI | 한국어화 / 진단 전용임을 문서로 명시하고 그대로 둔다. `docs/mvp.md`는 MVP 기능으로 광고하지 않는다고 적혀 있다 |
| D-4 | 휴식 중 루틴 단계 표시 | 말풍선에 단계 문구 표시 / 현재대로 대사만 유지하고 `docs/mvp.md` 서술을 수정 |
| D-5 | 회고 접근 하한 | `-77` 유지 / 90일 보존과 맞춰 `-84`로 정렬 |
| D-6 | 워크트리 사용권 | 이 세션이 구현을 맡을지, 다른 Codex 세션이 계속할지. 동시 수정은 피해야 한다 |

---

## 8. 기존 항목과의 관계

재보고하지 않은 기존 항목: QA-01 자동 미루기·즉시 만료, R-01 GLB 임의 속도, R-02 GLB 종료 포즈,
A-02 펫 페이지 포커스 — 모두 수정 완료로 기록되어 있다. 별도 추적 P3였던
이름-only 초안·중복 RootNode·내보내기/적용·저장 피드백 중 **저장 피드백**은 ISSUE-07로 구체 근거가 보강됐다.

기각한 주장: 루틴 편집기 포커스(진단 전용 창으로 일반 UI 도달 불가, 원인은 전역 포커스 설정이라 ISSUE-04에 흡수),
"무료 체험 없음"(`docs/mvp.md:67`이 명시한 제품 결정).

낡은 주장: 말풍선 방향 설정에 의존한 발견들(`bbdb4a8`에서 ComboBox 제거),
알림 중 방향이 매 초 뒤집힌다는 서술(현재는 결정적 좌/우 선택).
