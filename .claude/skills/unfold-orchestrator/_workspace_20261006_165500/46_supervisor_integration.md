# 46 감독자 통합 — 코드·디자인·UI/UX·QA 전수 검사 (2026-09-20)

기준: `release/mvp` @ `0b72a36` + 미커밋 변경 37개 파일. 검사 전용 Run, 제품 코드 미수정.
워커 5명(sonnet) 병렬 실행 후 감독자가 근거를 교차 검증했다.

## 1. 감독자 검증으로 뒤집힌 워커 판정

워커 보고를 그대로 수락하지 않고 파일:줄로 재확인했다. 다음 4건은 판정이 바뀌었다.

| 워커 판정 | 감독자 검증 결과 | 근거 |
|---|---|---|
| UX-1 **P0** 루틴 안내 소실 = 회귀 | **기각.** 사용자가 요청·승인한 변경 | `docs/plans/2026-09-20-speech-ui-proposal.md:3,10-11`; `SmokeDiagnostics.cs:245`의 인자는 `forbiddenBodyText`로 **표시되면 실패**. 워커가 시그니처를 반대로 읽음 |
| UX-2 **P1** +60분 안내 소실 = 회귀 | **기각.** 문서화된 의도 | `docs/validation/2026-09-20-ui-copy-cleanup.md` 변경 목록 |
| ARC-2 **P2** 루틴 이름 데이터 손실 우려 | **기각.** `BreakReview.Csv()`에 `routine_name` 존치. UI 표시만 축소 | 2차 감사에서 저장 계층 직접 열람 |
| QA "다른 작업자가 테스트 파일 편집 중" | **기각.** 40분간 `src`/`Tests`/`docs` mtime 변화 0 | `find -newermt` 확인. `CS0104` 일시 오류는 **원인 미상**으로 남김 |

## 2. 감독자 검증으로 확대·정정된 판정

| 워커 판정 | 감독자 정정 |
|---|---|
| VIS-4: 2개 상태가 Warning 색 공유 | **4개 상태가 공유**. 더 중요한 건 정상 조작인 "중지됨"이 실제 장애 "상태 확인 필요"와 같은 **Error 적색**(`SettingsWindow.cs:183-190`) |
| A11Y-1: 포커스 링 전면 제거 | **버튼은 정상**(`DesignSystem.cs:80-83`이 `:focus-visible` 테두리 유지). 결함은 `TextBox`/`NumericUpDown`/`ComboBox` 한정 — `:focus` 상태의 배경·테두리가 기본 상태와 **완전히 동일**(`DesignSystem.cs:90-91` vs `96-100`) |
| ARC-10: Theme만 개별 복구 | 확인. 비대칭이 같은 함수 안에 있음 — 4개 필드는 자체 복구, `Validate()`의 8개 필드와 `ValidatePersonalization()`은 `throw` → `AppRuntime.cs:77` `Settings = new()` 전체 초기화 |

## 3. 도달성 필터 — 여러 발견의 심각도를 바꾼 교차 검증

워커 간 상충(UX가 "진단 전용"이라 한 창을 A11Y가 P0 근거로 씀)을 감독자가 해소했다.
`new <Window>(` 전수 grep으로 **프로덕션 생성 경로**를 확인한 결과:

| 창 | 프로덕션 진입점 | 판정 |
|---|---|---|
| `PersonalizationWindow` | 없음 (`SmokeDiagnostics.cs:83` + 테스트만) | 진단 전용 |
| `RoutineEditorWindow` | 없음 (`SmokeDiagnostics.cs:64,75`, `PersonalizationWindow.cs:126`) | 진단 전용 |
| `ProfileEditorWindow` | 없음 (`SmokeDiagnostics.cs:79`) | 진단 전용 |
| `EditorWindow` (픽셀 에디터) | **없음.** `AppRuntime.OpenEditor`의 저장소 내 유일 호출자가 `SmokeDiagnostics.cs:101`, UI 바인딩 0 | 진단 전용 |
| `SettingsWindow`, `PetManagementView`, `CustomPetWindow`, `PetPackWindow`, `BreakReviewWindow`, `PetWindow` | 있음 | 프로덕션 |

승인 근거: `docs/personalization.md:13-14` — "내부 편집기와 자동 진단 경로는 남겨 두지만 제품 기능으로 노출하지 않는다."

**결과:**
- A11Y-3(픽셀 에디터 최소 창 잘림) **P1 → P3**. 일반 사용자 도달 불가.
- A11Y-1의 근거에서 `RoutineEditorWindow`/`EditorWindow`를 제외. 그래도 **발견은 유지** — 같은 스타일이 `SettingsWindow`·`PetManagementView`·`CustomPetWindow`·`PetPackWindow`의 프로덕션 입력에 모두 적용되기 때문이다.
- UX-12(Instruction 표시 경로 0) **P2 유지**. 일반 사용자 영향은 0이나 검증 규칙과 소비처의 불일치는 실재.

## 4. 통합 발견 큐

### P0 — 출시 차단
| ID | 발견 | 근거 | 조치 |
|---|---|---|---|
| QA-1 | Windows 실기 검증 0%. 체크리스트 전면 공란 | `docs/windows-dogfooding-log.md` | Windows 실기에서 설치·타이머·알림·펫 창·설정 저장 확인 후 기입 |

### P1 — 출시 전 처리 권장
| ID | 발견 | 근거 | 조치 |
|---|---|---|---|
| A11Y-1 | 프로덕션 입력 3종의 포커스가 시각적으로 구별되지 않음. `ComboBox`는 대체 단서 전무 | `DesignSystem.cs:94,96-100` | `:focus-visible`에 한해 테두리 복원. 과거 승인(`input-fields.md`)은 호버·포커스 **배경** 고정이 목적이었고 포커스 가시성 제거가 목적이 아니었음 |
| A11Y-2 | 휴식 알림 버튼에 자동 포커스 없음. 키보드 도달 경로가 트레이 "휴식 알림으로 이동" 1곳 | `AppRuntime.cs:369-370`, `PetWindow.cs:121` | 말풍선 표시 시 `FocusAction()` 호출 검토. **실기 확인 필요** |
| ARC-10 | 설정 손상 시 루틴·프로필·펫 선택·창 위치까지 전체 초기화 | `AppSettings.cs:48,61-71` → `AppRuntime.cs:77` | `Theme`처럼 필드별 자체 복구로 통일 |
| QA-2 | `<Version>0.2.0`인데 v0.2.1 커밋 2건 존재, v0.2.1 산출물·체크섬 없음 | `Unfold.Desktop.csproj:6` | 버전 올리고 산출물 재생성 |
| VIS-1 | 홈 기본 창 높이의 약 43%가 빈 배경 | 캡처 + 레이아웃 코드 | 우측 컬럼 재배치 |
| VIS-4 | 6개 타이머 상태가 3색으로 축약. 정상 조작 "중지됨"이 오류 적색 | `SettingsWindow.cs:183-190` | 중지를 중립색으로 분리 |
| UX-3 | 환경설정·홈 시간의 미저장 변경이 종료 시 확인 없이 폐기. 커스텀 펫 초안만 보호 | `SettingsWindow.CanCloseDraft()` | **미승인 gap**. 후속 계획 문서가 `docs/README.md` 목록에 없음 |

### P2 — 후속
VIS-2(기록 0건 300px 공백) · VIS-3(캡처가 코드보다 오래됨, 재캡처 필요) · UX-4(CSV 7일 범위 안내 부재) · UX-9(환경설정 저장 피드백 불일치) · UX-12(Instruction 필수 필드인데 표시 경로 0) · ARC-1(DesignSystem 전역 가변 상태, 테스트 격리가 컴파일러로 강제되지 않음) · ARC-7(`ReminderSoundPlayer.Preview()` 암묵 가정) · ARC-9(MM:SS·펫 크기 계산 중복) · A11Y 대비 2건(밝은 테마 비활성 텍스트 3.58/3.94:1, WCAG 면제 대상) · A11Y Esc 바인딩 부재 · A11Y 모션 감소 옵션 부재 · QA-3(출시 전 freeze 커밋 절차)

## 5. 결함 아님 — 확인된 건전성

- Release 테스트 **215 통과 / 0 실패 / 0 건너뜀**, 빌드 경고 0·오류 0. 문서 주장과 일치.
- 리소스 누수: 지정 8개 파일 + 소유 창 전수 대조에서 `Dispose`/`using` 불일치 **0건**.
- Core↔Desktop 단방향 확정. `src/Unfold.Core/`의 Avalonia 참조 0.
- 저장 5종 전부 `AtomicFile.Write`(temp+move). 쓰기 중단 시 원본 무손상.
- `BreakHistory` 손상 시 재저장 차단 + 배너 고지. 조용한 손실 아님.
- `AutomationProperties.SetName` 52건 전수 조사, 아이콘 전용 버튼 누락 **0건**.
- 상태 배지는 항상 텍스트 병기. 색 단독 전달 아님.
- 4테마 primary 버튼 대비 WCAG AA 통과(시각·접근성 워커 독립 확인 일치).
- `PetWindow` 위치 계산 6곳 전부 `DesktopScaling` 사용. 과거 Retina Dock 버그 미재발.
- `async void` 2곳·`Task.Run` 22곳 전부 예외 보호·UI 스레드 복귀 정상.

## 6. 미검증 — 이번 Run이 증명하지 못한 것

- **실제 OS 조작 전부.** macOS·Windows의 포인터·키보드 입력, Tab 실제 순회 순서, DPI 배율, 고대비 모드.
- **보조기술.** VoiceOver·내레이터 실제 낭독.
- **새 화면 캡처.** 빌드 충돌 방지를 위해 `dotnet` 실행을 QA 워커에 단독 배정했으므로 시각·접근성 감사는 기존 캡처에 의존했다. VIS-3처럼 캡처가 코드보다 오래된 구간이 있다.
- `dotnet publish`·`--smoke-test` 미실행. 패키징은 정적 확인만.
- `PlatformServices.cs`의 Windows 분기 미감사.
- QA 중 관측된 `CS0104` 일시 오류의 원인.
