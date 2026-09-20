# UI 시각 디자인 감사 (2026-09-20)

`release/mvp` 작업 트리 기준. `dotnet build/test/run`을 실행하지 않고 **기존 PNG 캡처 + 레이아웃 코드 정적 분석**으로 진행했다.
빌드를 소유한 다른 워커의 잠금과 충돌하지 않도록 제품 코드·테스트·문서는 전혀 수정하지 않았다.

## 요약

1. **홈 화면 오른쪽 컬럼에 큰 빈 공간** — 문구 정리(ui-copy-cleanup)로 "오늘의 작은 쉼" 카드가 짧아졌고 펫 크기 슬라이더로 왼쪽 컬럼이 길어지면서, 두 컬럼 높이 불균형이 심해졌다(P1).
2. **기록(Review) 화면의 0건 상태에서 카드 아래 대형 빈 공간** — 데이터가 없을 때 본문이 내용 높이만큼만 차지해 하단까지 빈 배경이 남는다(P1).
3. **펫 팩 화면의 안내 문구가 코드에서 통째로 비워짐** — 미리보기 박스 힌트, 하단 재생 안내, 페이지 부제목이 모두 빈 문자열로 바뀌어 첫 진입 시 안내가 전혀 없다(P1, 코드로 확인·캡처는 불일치).
4. **"휴식 대기 중"과 "휴식 중"이 같은 경고색을 공유** — 의미가 다른 두 타이머 상태가 배지 색으로 구분되지 않는다(P1).
5. **디자인 시스템 토큰(Space/Gap/Inset, Caption/Body/Section/Title) 우회가 코드 전반에 광범위** — 간격·글자 크기 매직 넘버가 수십 곳에 흩어져 있어 토큰 변경 시 일부만 반영될 위험이 있다(P2, 시스템적).

---

## 발견 표

| ID | 심각도 | 화면 | 근거(파일:줄 / 캡처) | 현재 값 → 기대 값 | 근거 | 권장 조치 |
|---|---|---|---|---|---|---|
| VIS-1 | P1 | 홈(기본/최소) | `docs/validation/images/2026-09-20-pet-scale-timer-debug/home-default.png`, `home-minimum.png` (18:25, 최신) | 오른쪽 컬럼("시간 설정"+"오늘의 작은 쉼")이 y≈450에서 끝나고 창 하단(y≈790)까지 약 340px(기본, 전체 높이의 43%), 최소 창에서도 약 210px(31%)가 빈 배경 | 왼쪽 펫 카드는 창 끝까지 채워지는데 오른쪽 두 카드만 끝나 시각적으로 컬럼이 절단된 것처럼 보임. ui-copy-cleanup이 "오늘의 작은 쉼" 카드의 설명 2줄을 제거([docs/validation/2026-09-20-ui-copy-cleanup.md]), pet-scale-timer-debug가 왼쪽에 "펫 크기" 슬라이더를 추가하면서 격차가 커짐 | 오른쪽 컬럼에 세 번째 카드(예: 다가오는 알림 미리보기)를 추가하거나, "오늘의 작은 쉼" 카드를 `VerticalAlignment.Stretch`로 늘려 아래 여백을 카드 내부 여백으로 흡수 |
| VIS-2 | P1 | 기록(Review), 0건 상태 | `docs/validation/images/2026-09-20-themes/theme-MidnightBlue-review.png`, `theme-Plum-review.png` (15:22) | "날짜별 기록" 카드가 y≈445에서 끝나고 하단 안내·CSV 버튼(y≈748)까지 약 300px 빈 배경 | 데이터가 있는 `review-default.png`(7일치 행 포함, 근거 있는 상태)는 카드가 거의 끝까지 채워지는 반면, 0건일 때는 안내 문구 1줄+버튼만 있는 카드로 축소되고 남는 영역이 그대로 비움 | 0건 상태 카드를 본문 남은 높이까지 채우는 빈 상태 일러스트/설명으로 확장하거나, `sections`/`body` 컨테이너에 `VerticalAlignment.Stretch` 적용 |
| VIS-3 | P1 | 펫 팩 열기/만들기 | `src/Unfold.Desktop/PetPackWindow.cs:43,46,84-87,129,148` (git diff), 캡처 `docs/validation/images/2026-09-20-ui-copy-cleanup/pet-open.png`, `pet-create.png` (18:23) | `previewHint`(미리보기 박스 중앙 안내), `playbackStatus`(하단 "파일을 열면 동작을 선택할 수 있어요."), 페이지 부제("`.unfoldpet` 파일을 열어 미리보고 설치하세요.")가 모두 `""`로 초기화됨(코드), `previewHint.IsVisible = false` 명시 → 기대: 기존처럼 빈 상태 안내 문구 표시 | `git diff -- src/Unfold.Desktop/PetPackWindow.cs`에서 세 문자열이 빈 값으로 바뀐 것을 직접 확인. 단, `pet-open.png` 캡처에는 옛 문구("펫 팩을 선택하면 설치 전에 미리볼 수 있어요.")가 아직 보여 **캡처와 현재 코드가 불일치**(캡처가 이 특정 편집 이전 상태일 가능성) | 빈 미리보기 박스에 최소한의 안내(점선 테두리 + "파일을 열어 확인하세요" 등)를 복원하거나 의도적 제거라면 디자인 근거를 문서화. implementation-engineer가 새 캡처로 현재 렌더링을 재확인 필요 |
| VIS-4 | P1 | 홈/설정 상태 배지 6종 | `src/Unfold.Desktop/SettingsWindow.cs:183-191` | `stateBrush`가 Success(기본) / Error(`ActivityError`·`Stopped`) / Warning(`ActiveReminder≠null` 또는 `Paused` 또는 `IdlePaused`) 3색뿐인데 텍스트는 "진행 중/일시정지/중지됨/자리 비움/휴식 대기 중/휴식 중/상태 확인 필요" 7종 → "휴식 대기 중"(Invitation)과 "휴식 중"(Resting)이 둘 다 `ActiveReminder≠null`이라 같은 Warning 색을 씀 | 코드 로직 직접 추적. 두 상태는 사용자에게 "아직 안 쉬는 중"과 "지금 쉬는 중"으로 의미가 다르지만 배지 색·점 색으로는 구분 불가 | Resting 상태에 Success 계열(또는 별도 Accent) 색을 배정해 대기/진행 중 구분 |
| VIS-5 | P2 | 홈 vs 기록 통계 숫자 | `src/Unfold.Desktop/SettingsWindow.Layout.cs:19`(`Label("0", 40)`) vs `src/Unfold.Desktop/BreakReviewWindow.cs:45`(`Ui.Text("", 32)`, `Ui.Text("", 28)`) | 홈의 "오늘의 작은 쉼" 큰 숫자 = 40px, 기록 화면의 "완료한 휴식"/"휴식한 날" = 32px, "기록된 시간"만 28px | 같은 "핵심 지표 숫자" 역할인데 화면마다(40/32), 심지어 한 행 안에서도(32/28) 크기가 다름. `review-default.png`에서 "3분 10초"가 옆의 "1"보다 살짝 작게 렌더링되는 것으로 육안 확인 | DesignSystem에 `StatNumber`류 공유 토큰을 만들어 세 위치 모두 동일 크기 사용, 긴 텍스트(분초)는 줄바꿈이나 `TextTrimming`으로 해결 |
| VIS-6 | P2 | 전 화면 | 예: `src/Unfold.Desktop/SettingsWindow.Layout.cs:175`(`body.Spacing = 16`) vs 같은 파일 151행(`body.Spacing = Inset`, 20) vs 183행(`body.Spacing = Space`, 8) | 같은 홈 탭 안에서 "타이머 설정" 카드=20, "시간 설정" 카드=16(토큰 없음), "오늘의 작은 쉼" 카드=8로 카드 내부 세로 리듬이 제각각 | 코드 직접 대조. `docs/design-system.md`는 Space(8)/Gap(12)/Inset(20) 3단계 척도를 명시하지만 16은 그 어느 것도 아님 | 16을 Gap(12) 또는 Inset(20) 중 하나로 정리하거나 새 named 토큰 추가 |
| VIS-7 | P2 | 전 화면(시스템적) | 예 `Ui.cs:90`(`header.Spacing=6`), `CustomPetWindow.cs:116,120`(4,3), `PetPackWindow.cs:126`(6), `SettingsWindow.Layout.cs:64,66,77,98`(12,12,4,4), `SettingsWindow.Notifications.cs:143,152,153`(4,12,0), `BreakReviewWindow.cs:244`(4) 등 다수 | `DesignSystem.Space/Gap/Inset` 토큰이 있음에도 대부분의 `.Spacing =`/`new Thickness(...)` 호출이 리터럴 숫자를 직접 사용 | grep 결과 20곳 이상에서 토큰 대신 리터럴 사용. 값 자체는 대체로 4/8/12 그리드에 맞지만, 이름 없는 상수라 앞으로 토큰 값이 바뀌면 이 지점들은 갱신되지 않음 | 디자인 시스템에 `Space/2`(4) 같은 세부 단계를 추가하고 기존 리터럴을 일괄 치환 |
| VIS-8 | P2 | 4테마 대비(참고) | `src/Unfold.Desktop/DesignSystem.Themes.cs:20-24`, 픽셀 샘플: Plum 버튼 텍스트 대 배경 대비 약 8.1:1, MidnightBlue 약 9:1(직접 크롭·픽셀 샘플로 계산) | 오트 라떼/세이지는 진한 배경+흰 글자, 미드나이트 블루/플럼은 옅은 배경+어두운 글자로 "적용" 버튼이 렌더링됨 | 계산한 대비율은 WCAG AA(4.5:1) 통과. 결함은 아니지만 두 라이트 테마와 두 다크 테마에서 primary 버튼의 "무게감"이 다르게 느껴짐(진한 필 vs 파스텔 필) | 결함 아님, 참고 사항으로만 기록. 필요시 다크 테마 primary 버튼도 더 채도 높은 강조색 검토 |

---

## 토큰 이탈 목록 (파일:줄 + 하드코딩 값)

- `src/Unfold.Desktop/SettingsWindow.cs:14` — `Ui.Text("60:00", 52, Ui.Accent)`: 초기값 52px이 즉시 `SettingsWindow.Layout.cs:111`의 `countdown.FontSize = 64`로 덮어써짐(죽은 코드, 혼란 유발). `DesignSystem`에 타이머 전용 크기 토큰 없음.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:111` — `countdown.FontSize = 64; ... countdown.LineHeight = 70;`: 홈 화면 최대 숫자, named 토큰 없음.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:19` — `Label("0", 40)`: 오늘 완료 횟수 큰 숫자, named 토큰 없음(VIS-5 참고).
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:20,182` — `Label("", 12)`, `Label("회", 12, Muted)`: 값은 `Caption`(12)과 같지만 토큰 미사용.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:175` — `body.Spacing = 16`(VIS-6).
- `src/Unfold.Desktop/BreakReviewWindow.cs:45` — `Ui.Text("", 32)`, `Ui.Text("", 28)`: 기록 화면 3열 지표 숫자, 서로 다른 값+토큰 미사용(VIS-5).
- `src/Unfold.Desktop/Ui.cs:90` — `header.Spacing = 6`.
- `src/Unfold.Desktop/CustomPetWindow.cs:116,120` — `.Spacing = 4`, `.Spacing = 3`.
- `src/Unfold.Desktop/PetPackWindow.cs:126` — `feedback.Spacing = 6`.
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:64,66` — `links.Spacing = 12`, `bottom.Spacing = 12`(값은 `Gap`과 동일하지만 리터럴).
- `src/Unfold.Desktop/SettingsWindow.Layout.cs:77,98` — `.Spacing = 4`(intro), `.Spacing = 4`(scaleField).
- `src/Unfold.Desktop/SettingsWindow.Notifications.cs:143,152,153` — `.Spacing = 4`, `soundGroup.Spacing = 12`, `fields.Spacing = 0`.
- `src/Unfold.Desktop/PixelCanvas.cs:50` — `Brush.Parse("#353B47")`, `Brush.Parse("#454D5A")`: `DesignSystem` 밖에서 색을 직접 파싱(픽셀 에디터 전용 도구라 영향 범위는 작지만 유일한 `Color.Parse` 계열 이탈).
- `src/Unfold.Desktop/EditorWindow.cs`, `PetPackDiagnostics.cs` 등: 12/14/18px 캡션·타이틀 다수 리터럴(디버그/에디터 전용 화면이라 우선순위 낮음).

메타 관찰: `DesignSystem.cs:17-18`은 `Caption=12, Body=14, Section=18, Title=24`와 `Space=8, Gap=12, Inset=20`을 정의하지만, 실제 호출부 다수가 이 상수 대신 같은 값의 리터럴을 쓴다. 시각적으로는 지금 당장 어긋나지 않지만(대부분 같은 숫자), 토큰의 "단일 진실 공급원" 목적이 무너져 있어 추후 리스케일 작업에서 회귀 위험이 크다.

---

## 테마별 위험 목록

| 테마 | 관찰 | 상태 |
|---|---|---|
| 오트 라떼(기본) | 카드 배경(#FFFFFF)·본문(#38342E) 대비 높음, primary 버튼(#756344 배경/흰 글자) 위계 뚜렷 | 확인된 사실(캡처) |
| 세이지 | 오트 라떼와 동일 패턴(진한 그린 버튼/흰 글자), 구조적 문제 없음 | 확인된 사실(캡처) |
| 미드나이트 블루 | primary 버튼이 옅은 하늘색 필+어두운 남색 글자로 렌더링(라이트 테마와 반대 극성). 픽셀 샘플로 계산한 대비 ≈9:1로 WCAG 통과 | 확인된 사실(캡처+픽셀 샘플), 결함 아님 |
| 플럼 | 동일하게 옅은 핑크 필+짙은 자주 글자. 대비 ≈8.1:1로 통과하나 두 라이트 테마 대비 버튼의 시각적 강조가 약해 보임(VIS-8) | 확인된 사실(캡처+픽셀 샘플) |
| 4테마 공통 | VIS-2(기록 0건 빈 공간), VIS-4(상태 배지 색 공유)는 테마와 무관하게 모든 팔레트에서 동일하게 재현됨(구조적 레이아웃/로직 문제이므로) | 확인된 사실(MidnightBlue·Plum 캡처로 교차 확인) |

---

## 확인된 사실 / 코드로만 추론한 것 / 미검증

**확인된 사실(캡처로 직접 봄)**
- VIS-1(홈 오른쪽 컬럼 빈 공간), VIS-2(기록 0건 빈 공간)는 2026-09-20 최신 캡처(각각 18:25, 15:22)에서 픽셀 단위로 측정해 확인했다.
- VIS-8 테마별 버튼 대비는 PNG를 크롭해 픽셀 색상을 직접 샘플링하고 WCAG 대비율을 계산해 확인했다(결함 아님으로 결론).
- 4테마 × home/settings/pets/builder/review/speech/picker 캡처를 육안으로 훑어 카드 반경(24px)·타이포 계층(제목 18px 세미볼드/본문 14px/캡션 12px)이 화면 전반에서 일관됨을 확인했다.
- 말풍선 4방향(top/bottom/left/right) 폭이 실제로 모두 320px임을 `file` 명령으로 확인해, 육안상 폭이 달라 보인 것은 착시였음을 정정했다.

**코드로만 추론한 것**
- VIS-3(펫 팩 안내 문구 제거)은 `git diff`로 코드 변경을 확인했지만, 그 결과가 반영된 새 캡처가 없다(가장 최신 `pet-open.png`도 옛 문구를 담고 있어 이 특정 편집 이전 상태로 추정).
- VIS-4(상태 배지 색 공유)는 `Refresh()`의 조건 분기를 코드로 추적한 결과이며, "휴식 중" 상태의 실제 배지 캡처는 확보하지 못했다(타이머가 실제로 휴식 진행 중인 순간의 SettingsWindow 캡처가 데이터셋에 없음).
- VIS-5, VIS-6, VIS-7(토큰 이탈)은 grep과 코드 대조로 확인했으며, 렌더링 결과가 캡처에서 육안으로 구분 가능한 것(VIS-5의 32/28px 차이)과 그렇지 않은 것(VIS-6/7 대부분)을 섞어 보고했다.

**미검증(새 캡처 불가로 확인 못함)**
- Windows에서의 4테마 렌더링, 125/150/200% 배율에서의 토큰 이탈 영향.
- VIS-3 수정 여부(실제로 의도된 제거인지 실수인지)는 이번 감사 범위 밖이며 담당 구현자 확인이 필요하다.
- "휴식 중" 상태의 실제 배지 캡처.

---

## 참고: 캡처 신선도 메모

같은 2026-09-20에도 여러 워커가 순차로 작업해 화면별로 최신 캡처 디렉터리가 다르다(mtime 기준: review-ui-implementation 14:45 → themes 15:22 → speech-ui-implementation 16:07 → ui-copy-cleanup 18:23 → pet-scale-timer-debug 18:25 → 최상위 `artifacts/verification/pixel-editor*.png` 18:32). 이 감사는 화면별로 가장 최신 디렉터리를 우선 근거로 삼았고, `docs/validation/images/2026-09-20-home-ui-implementation/settings-review-tab.png`처럼 review-ui-implementation 이전에 찍힌 기록 화면 캡처(큰 빈 공간이 있는 구버전 단일 컬럼 레이아웃)는 현재 `BreakReviewWindow.cs`(3열 지표 레이아웃)와 불일치하므로 낡은 근거로 배제했다.
