# 31. 디자인 시스템 재설계 방향

- 날짜: 2026-09-17
- 기준: `release/mvp`, 기준 커밋 `2f02e8a` + Phase 1·2 미커밋 작업 트리
- 범위: 토큰·공통 컴포넌트 방향 확정만 수행. `src/`·`Tests/`는 수정하지 않았다.
- 입력: `_workspace/30_ui_redesign_input.md:1-50`, `_workspace/08_ui_visual_audit.md:84-128`, `src/Unfold.Desktop/DesignSystem.cs:12-214`, `src/Unfold.Desktop/Ui.cs:11-167`
- 캡처: `/tmp/unfold-phase2-smoke-final.j5Uen8/verification/`의 PNG 42장 전부를 기본·최소·스크롤·상태별로 열어 확인했다. 캡처는 96 DPI `RenderTargetBitmap`으로 창 콘텐츠를 그리는 자동 진단 결과다(`src/Unfold.Desktop/SmokeDiagnostics.cs:516-531`).

## 결론

1. 차콜·크림의 브랜드 방향은 유지한다. 전면 교체 대신 **의미 토큰 분리 → 작은 글자·비활성·포커스 결함 교정 → 표면/컴포넌트 정규화 → 선택적 라이트 테마** 순서로 적용한다.
2. 현행 색의 텍스트 대비는 대체로 충분하지만, `Outline`은 프레임과 카드 경계를 약하게 만들고 `Raised`는 보조 버튼·대형 카드·상태 배지를 동시에 맡아 표면 위계가 흐려진다(`DesignSystem.cs:15-19,41-69`; `settings.png`, `settings-speech-options.png`).
3. 10·11·12·13·17·20·22px가 화면별로 직접 지정되어 있다. 특히 우측 설정 카드의 10–11px 도움말은 860×680에서 밀도가 높고 읽기 부담이 크다(`SettingsWindow.Layout.cs:110-146`; `settings-minimum.png`, `settings-speech-options.png`).
4. Phase 2 이후 `_workspace/08_ui_visual_audit.md:88-89`의 스크롤바 겹침과 5버튼 단독 줄바꿈은 최신 캡처에서 해소됐다(`settings-pet-create-minimum.png`, `custom-pet-minimum.png`, `routine-library.png`, `work-profiles.png`). 기본/최소 폭을 함께 보아야 한다는 회귀 원칙은 계속 유효하다(`_workspace/08_ui_visual_audit.md:90,101-104`).
5. P0는 기존 레이아웃을 흔들지 않는 의미 토큰과 상태 교정이다. P1에서 스케일과 변형을 통일하고, P2에서 라이트 테마·장식 반경을 선택적으로 적용한다.

## 1. 현행 토큰과 실제 사용처

### 1.1 색·표면·상태

모든 현행 색 선언은 `DesignSystem.cs:15-19`, Fluent 팔레트 연결은 `DesignSystem.cs:24-34`에 있다. `Ui.Background/Panel/Accent`는 각각 `Canvas/Surface/Cream`의 별칭이다(`Ui.cs:13`).

| 현행 토큰 | 값 | 확인한 실제 사용처 | 현재 역할 충돌/공백 |
|---|---:|---|---|
| `Canvas` | `#141713` | 모든 `.unfold-page` 창 바탕(`DesignSystem.cs:36-40`), 설정 창 바탕(`SettingsWindow.Layout.cs:26-29`) | 창 밖 여백 전용으로는 적절하나 `Shell`과 명도 단계가 작다(`settings.png`, `dialog-confirm.png`). |
| `Shell` | `#1D201D` | 페이지 프레임(`Ui.cs:87-92`), 입력 배경(`DesignSystem.cs:74-120`), 말풍선(`PetSpeechBubble.cs:18-22`) | 프레임·입력·말풍선의 서로 다른 의미가 한 색에 묶였다. |
| `Surface` | `#2B2F2A` | 카드(`Ui.cs:34-35`), 목록(`DesignSystem.cs:197-200`), 비활성 버튼(`DesignSystem.cs:67-69`) | 콘텐츠 카드와 disabled fill을 함께 맡는다. |
| `Raised` | `#363C33` | 일반 버튼(`DesignSystem.cs:41-51`), 펫 카드·오늘 카드(`SettingsWindow.Layout.cs:67-82,144-151`), 타이머 상태 배지(`SettingsWindow.cs:13-15`) | 인터랙션·대형 표면·상태 컨테이너가 구분되지 않는다(`settings.png`). |
| `Cream` | `#DFE5D1` | 기본 글자, Primary 배경, 선택 목록, 체크·선택, 아이콘(`DesignSystem.cs:30-32,52-53,182-200`; `Ui.cs:24`) | `TextPrimary`, `AccentFill`, `SelectionFill`이 결합되어 라이트 테마 전환 시 깨지기 쉽다. |
| `Ink` | `#252A23` | Primary 위 글자·선택 글자·체크 glyph(`DesignSystem.cs:52-53,72-73,182-195`) | 역할은 `OnAccent`인데 물성 이름이라 테마 의미가 불명확하다. |
| `Muted` | `#B6BEB0` | 설명문, 입력 경계, Primary pressed, 비활성 글자(`Ui.cs:32-33`; `DesignSystem.cs:64,67,74-91`) | 보조 텍스트·강한 입력 경계·pressed·disabled가 한 값이다. |
| `Outline` | `#444B40` | 프레임·하단 작업선·Quiet 경계(`Ui.cs:82-92`; `DesignSystem.cs:54-55`) | `Shell` 대비 약 1.82:1, `Surface` 대비 약 1.51:1이라 장식선과 조작 경계를 분리할 필요가 있다. 실제로 카드 중첩 경계가 약하다(`settings.png`, `weekly-review.png`). |
| `Hover` | `#505A48` | 일반·Danger pointer-over(`DesignSystem.cs:60-66`) | 버튼용 상태인데 명칭이 전역적이다. |
| `AccentHover` | `#F0F3E9` | Primary pointer-over(`DesignSystem.cs:63`) | Accent 계열로 유지 가능하나 라이트 테마 값은 별도여야 한다. |
| `Error` | `#FFB4A3` | 오류 메시지·Danger 텍스트·정지 상태(`DesignSystem.cs:56-57`; `SettingsWindow.cs:148-153`) | 오류와 파괴 동작은 있으나 오류 컨테이너/테두리 토큰은 없다(`dialog-confirm.png`, `pack-error.png`). |
| `Warning` | `#E5C58C` | 일시정지·자리 비움·초과 휴식·팩 참고(`SettingsWindow.cs:148-153`; `PetSpeechBubble.cs:63-64`) | 경고는 있으나 성공 상태가 없어 저장 완료가 `Muted`로 돌아간다(`BreakReviewWindow.cs:54-57,89`). |
| 성공 | 없음 | 저장/적용 완료는 `Muted` 사용(`PersonalizationWindow.cs:115-118`; `SettingsWindow.cs:124-139`) | 완료와 보조 설명이 시각적으로 동일하다. |
| 포커스 | 버튼은 `Cream`/Primary는 `Ink`, 입력은 별도 표시 없음 | 버튼 `:focus-visible`(`DesignSystem.cs:70-73`), 입력 `FocusAdorner=null` 및 모든 상태 동일 경계(`DesignSystem.cs:74-91`) | 일관된 포커스 색·폭·오프셋 토큰이 없다. 정적 PNG에서는 실제 키보드 포커스를 검증할 수 없다. |

### 1.2 타이포그래피

| 현행 토큰/직접값 | 값 | 실제 사용처 | 진단 |
|---|---:|---|---|
| `Caption` | 12 | `Ui.Caption`, 필드 라벨·도움말(`DesignSystem.cs:20`; `Ui.cs:32-40`) | 공통 12는 유지 가능하지만 설정 전용 10·11px가 우회한다. |
| 직접 소형 | 10, 11, 12, 13 | 설정 설명·상태, 대시보드 보조 문구(`SettingsWindow.Layout.cs:71,79-81,110-150`; `BreakReviewWindow.cs:59-60`) | 10–11px는 P0에서 제거한다(`settings-minimum.png`, `settings-speech-options.png`). |
| `Body` | 14 | 창 기본, 버튼, 입력, 일반 텍스트(`DesignSystem.cs:20,36-50,74-80`; `Ui.cs:24`) | 본문 기준으로 유지한다. |
| 직접 강조 본문 | 16, 17 | 오늘 상태·카드 제목·말풍선 제목(`SettingsWindow.cs:17`; `SettingsWindow.Notifications.cs:33-34`; `PetSpeechBubble.cs:13`) | 의미 토큰 없이 반복된다. |
| `Section` | 18 | 선언만 있고 직접 참조 없음(`DesignSystem.cs:20`; 저장소 검색 기준) | 17px 직접값을 흡수하는 실제 섹션 토큰으로 전환한다. |
| 직접 중간 제목 | 20, 22 | 펫 이름, 회고 요약(`SettingsWindow.Layout.cs:15-16`; `BreakReviewWindow.cs:35`) | `Heading`/`Metric` 역할로 분리한다. |
| `Title` | 24 | 공통 페이지 제목(`DesignSystem.cs:20`; `Ui.cs:68-79`) | 유지한다. |
| 직접 메트릭 | 34, 48, 52, 64 | 말풍선 타이머, 오늘 횟수, 타이머(`PetSpeechBubble.cs:13`; `SettingsWindow.Layout.cs:15-16,85-88`; `SettingsWindow.cs:13`) | 화면별 숫자를 메트릭 스케일로 명명한다. |
| 굵기/행간 | `SemiBold` 일부, 행간 토큰 없음 | 공통 제목·말풍선 제목(`Ui.cs:75`; `PetSpeechBubble.cs:23`) | 제목/섹션/본문의 굵기·행간 계약을 명문화해야 한다. |

### 1.3 간격·반경·표면 계층

| 현행 토큰/직접값 | 값 | 실제 사용처 | 진단 |
|---|---:|---|---|
| `Space` | 8 | 액션 간격·스크롤 거터(`DesignSystem.cs:21`; `Ui.cs:42-50`) | 유지. |
| `Gap` | 12 | 기본 Column, 팩 미리보기, 라이브러리 액션(`Ui.cs:25-26`; `PetPackWindow.cs:79-83`; `PersonalizationWindow.cs:87-95`) | 유지. |
| `Inset` | 20 | 공통 PageFrame(`DesignSystem.cs:21`; `Ui.cs:87-96`) | 유지하되 카드 padding과 분리 명명한다. |
| 직접 간격 | 4, 6, 7, 10, 12, 14, 16, 18, 20, 22, 24 | 필드·헤더·카드·설정 전용 배치(`Ui.cs:40,72-83,90`; `SettingsWindow.Layout.cs:30-48,69-150`; `CustomPetWindow.cs:96-110`) | 6·7·10·14·18·22가 임의값으로 누적돼 밀도 리듬이 불규칙하다. |
| `ControlRadius` | 12 | 버튼·입력(`DesignSystem.cs:22,41-48,74-84`) | 작은 버튼에도 커서 캡슐에 가까워 보인다(`settings-speech-options.png`). |
| `CardRadius` | 24 | 공통 카드·미리보기(`DesignSystem.cs:22`; `Ui.cs:34-35`; `CustomPetWindow.cs:137-149`) | 카드 중첩과 약한 경계가 결합해 덩어리 구분을 흐린다. |
| `FrameRadius` | 32 | 공통 페이지 프레임(`DesignSystem.cs:22`; `Ui.cs:87-92`) | 브랜드 프레임으로 유지 가능하나 대화상자에서는 큰 편이다(`dialog-confirm.png`). |
| 직접 반경 | 11, 18, 22, 28, 32/64 | 배지·행동 카드·말풍선·설정 카드(`PetWindow.cs:27-33`; `CustomPetWindow.cs:103-113`; `PetSpeechBubble.cs:20-22`; `SettingsWindow.Layout.cs:42-82`) | 형태 역할이 명명되지 않아 새 화면이 임의 반경을 추가하기 쉽다. |

## 2. Phase 2 캡처 진단

### 2.1 42장 열람 범위

- 설정/상태 18장: `settings.png`, `settings-completed.png`, `settings-minimum.png`, `settings-minimum-scrolled.png`, `settings-pet-open-tab.png`, `settings-pet-open-minimum.png`, `settings-pet-create-tab.png`, `settings-pet-create-minimum.png`, `settings-pet-create-minimum-scrolled.png`, `settings-review-tab.png`, `settings-routines-tab.png`, `settings-speech-options.png`, `timer-paused.png`, `timer-stopped.png`, `routine-editor.png`, `additional-routine.png`, `profile-editor.png`, `editor.png`.
- 라이브러리/대화상자 6장: `routine-library.png`, `work-profiles.png`, `weekly-review.png`, `dialog-confirm.png`, `dialog-error.png`, `dialog-prompt.png`.
- 펫 팩/제작 9장: `pack-preview.png`, `pack-installed.png`, `pack-reinstall.png`, `pack-update.png`, `pack-error.png`, `custom-pet-editor.png`, `custom-pet-minimum.png`, `custom-pet-minimum-scrolled.png`, `custom-pet-preview.png`.
- 펫/말풍선 9장: `pet.png`, `speech-folded.png`, `speech-top.png`, `speech-bottom.png`, `speech-left.png`, `speech-right.png`, `speech-five-minutes.png`, `speech-completed.png`, `speech-overtime.png`.

### 2.2 남은 공통 시각 문제

| 우선순위 | 관찰 | 근거 | 사용자 영향 | 방향 |
|---|---|---|---|---|
| P0 | 10–11px 설명/상태가 우측 300px 카드에 집중된다. | `SettingsWindow.Layout.cs:110-150`; `settings.png`, `settings-minimum.png`, `settings-speech-options.png` | 한국어 획이 작은 크기에서 뭉치고, 정보량이 많은 최소 창에서 읽기 비용이 커진다. | 의미 있는 텍스트 최솟값 12, 카드 라벨 13으로 올리고 불필요한 반복 설명은 화면별 정보구조 작업으로 넘긴다. |
| P0 | 비활성 버튼이 `Surface + Muted + 전체 Opacity .55`라 배경과 글자가 함께 흐려진다. | `DesignSystem.cs:67-69`; `pack-installed.png`, `pack-error.png`, `settings-pet-create-minimum-scrolled.png` | 비활성임은 보이지만 라벨 판독성이 지나치게 낮다. | opacity를 제거하고 `DisabledFill/DisabledText/DisabledBorder`를 직접 지정한다. |
| P0 | 키보드 포커스 의미 토큰이 없고 입력은 FocusAdorner가 제거된 채 동일 테두리를 유지한다. | `DesignSystem.cs:70-91`; `_workspace/08_ui_visual_audit.md:108-109,125` | 버튼과 입력의 포커스 표현이 달라질 수 있고 정적 캡처만으로 발견되지 않는다. | 본 보고서에서 색·폭·오프셋 토큰만 정의하고 컨트롤별 적용은 워커 D의 UI-07 사양에 위임한다. |
| P1 | `Shell → Surface → Raised`가 모두 비슷한 차콜이고 `Outline`도 약해, 카드 안 카드·상태 배지·보조 버튼이 같은 깊이로 보인다. | `DesignSystem.cs:15-19`; `settings.png`, `settings-speech-options.png`, `weekly-review.png`, `custom-pet-editor.png` | 제목→설명→콘텐츠→행동의 계층보다 둥근 덩어리 반복이 먼저 보인다. | 표면 명도 단계를 넓히고 장식 경계/조작 경계를 분리하며 `Raised` 역할을 세분화한다. |
| P1 | 크림색 하나가 글자·Primary·선택을 모두 담당한다. | `DesignSystem.cs:30-32,52-73,182-200`; `settings-routines-tab.png`, `dialog-prompt.png` | 다크에서는 동작하지만 라이트 팔레트에서 선택/Primary/텍스트를 독립 조정할 수 없다. | `TextPrimary`, `AccentFill`, `OnAccent`, `SelectionFill`로 의미 분리한다. |
| P1 | 17/20/22 제목과 34/48/64 숫자, 4–24 간격이 화면별 직접값으로 반복된다. | 1.2·1.3의 코드 근거; `settings.png`, `weekly-review.png`, `speech-overtime.png` | 새 화면마다 미세한 크기·간격 차이가 늘어난다. | 정규 타이포·간격·메트릭 스케일로 흡수한다. |
| P2 | 큰 반경(24–32, 특수 64)이 작은 대화상자와 조밀한 카드에도 동일하게 반복된다. | `DesignSystem.cs:22`; `Ui.cs:34-35,87-92`; `dialog-confirm.png`, `profile-editor.png` | 친근함은 유지되지만 정보 밀도 화면이 다소 부풀어 보인다. | P0/P1 안정화 후 반경만 단계적으로 낮춘다. |

### 2.3 `_workspace/08` 공통 패턴 재확인

| 기존 지적 | Phase 2 이후 판정 | 근거 |
|---|---|---|
| 최소 폭 `PageBodyScroll`이 상단 입력/버튼과 겹침(`_workspace/08_ui_visual_audit.md:88,96-98`) | **해소**. `AllowAutoHide=false`와 8px 거터가 추가됐고 최신 최소 캡처에서 입력·버튼과 스크롤바 사이가 분리된다. | `Ui.cs:49-66`; `settings-pet-create-minimum.png`, `custom-pet-minimum.png`, `settings-pet-open-minimum.png` |
| 5버튼에서 `닫기`만 둘째 줄로 이동(`_workspace/08_ui_visual_audit.md:89,99`) | **해소**. 관리 동작은 왼쪽, Primary+닫기는 오른쪽 그룹으로 나뉘어 한 줄이다. | `PersonalizationWindow.cs:67-95`; `routine-library.png`, `work-profiles.png` |
| 기본 폭만 확인하면 최소 폭 결함을 놓침(`_workspace/08_ui_visual_audit.md:90`) | **유효한 회귀 원칙**. 최소 폭은 스크롤·축약·카드 노출량이 기본 폭과 다르다. | `settings-pet-create-tab.png` ↔ `settings-pet-create-minimum.png`; `custom-pet-editor.png` ↔ `custom-pet-minimum.png` |
| 프레임/로고가 밀려 보임(`_workspace/08_ui_visual_audit.md:111-120`) | **새 회귀 없음**. 동일 프레임 여백·반경이 최신 설정 탭에도 유지된다. | `SettingsWindow.Layout.cs:40-43`; `settings-review-tab.png`, `settings-routines-tab.png` |

## 3. 재설계 토큰

제안값은 구현 전 방향값이다. 다크 색은 현재 브랜드를 유지하면서 역할을 분리하고, 라이트 색은 같은 의미 역할의 별도 팔레트다. `P0`는 현행 결함 교정, `P1`은 일관성 확보, `P2`는 선택적 개선이다.

### 3.1 색·표면·상태

| ID/우선순위 | 토큰 | 현재값 → 제안값(다크 / 라이트) | 근거 | 영향 화면 |
|---|---|---|---|---|
| C01 P1 | `Canvas` | `#141713` → `#101310` / `#F1F4ED` | 최외곽 배경을 Shell과 분리. `DesignSystem.cs:15`; `settings.png` | 모든 창 |
| C02 P1 | `Shell` | `#1D201D` → `#1A1F1B` / `#FBFCF8` | 페이지·말풍선의 기본 바탕. 입력 배경은 별도 `FieldFill`로 분리. `Ui.cs:87-92`; `PetSpeechBubble.cs:20-22` | 페이지 프레임, 말풍선, 대화상자 |
| C03 P1 | `Surface` | `#2B2F2A` → `#2D342E` / `#E8EDE4` | Shell보다 명확한 콘텐츠 그룹을 만든다. `Ui.cs:34-35`; `weekly-review.png` | 카드·목록·미리보기 |
| C04 P1 | `SurfaceRaised` | `Raised #363C33` → `#414B42` / `#D5DED2` | 중첩 카드·상태 컨테이너만 담당. `SettingsWindow.Layout.cs:82,151`; `settings.png` | 펫 카드, 오늘 카드, 상태 배지 |
| C05 P1 | `ControlFill` | `Raised #363C33` 공유 → `#354036` / `#E2E8DE` | 일반 버튼을 Raised surface에서 분리. `DesignSystem.cs:41-51`; `pack-preview.png` | Secondary 버튼·아이콘 버튼 |
| C06 P1 | `FieldFill` | `Shell #1D201D` 공유 → `#161B17` / `#FFFFFF` | 입력 가능 영역을 페이지 바탕과 구분. `DesignSystem.cs:74-120`; `profile-editor.png` | TextBox, NumericUpDown, ComboBox |
| C07 P1 | `TextPrimary` | `Cream #DFE5D1` 공유 → `#F0F4E6` / `#1D241E` | 기본 글자를 Accent fill에서 분리. `Ui.cs:24`; `settings.png` | 모든 제목·본문 |
| C08 P1 | `TextSecondary` | `Muted #B6BEB0` 공유 → `#C4CCBE` / `#4C584E` | 본문 보조 설명. 다크 Shell 대비 약 10.45:1. `Ui.cs:32-33` | 설명·필드 보조문 |
| C09 P0 | `TextTertiary` | 10–11px `Muted` 직접 사용 → `#9CA798` / `#657267` + 크기 최소 12 | 정보 우선순위는 낮추되 판독성을 유지. `SettingsWindow.Layout.cs:110-150` | 설정 도움말·상태 |
| C10 P0 | `OutlineSubtle` | `Outline #444B40` → `#4F5B51` / `#BCC7BA` | 장식선·카드 경계용. 현행 한 색 분리. `Ui.cs:82-92` | 프레임·카드·구분선 |
| C11 P0 | `OutlineStrong` | 입력의 `Muted #B6BEB0` → `#849187` / `#77857A` | 조작 가능한 입력·Quiet 버튼 경계는 장식선보다 강해야 한다. `DesignSystem.cs:54-55,74-91`; `dialog-prompt.png` | 입력·Quiet·선택 컨트롤 |
| C12 P1 | `InteractionHover` | `Hover #505A48` → `#4D5A4E` / `#D4DCD1` | hover를 버튼 전용 의미로 명명. `DesignSystem.cs:60-66` | 일반·Danger 버튼 |
| C13 P1 | `AccentFill` | `Cream #DFE5D1` → `#DDE7CE` / `#344432` | Primary/선택 강조 전용. `DesignSystem.cs:52-53,190-195` | Primary, 선택 목록·탭 |
| C14 P1 | `AccentHover` | `#F0F3E9` → `#EEF4E5` / `#263628` | 테마별 Primary hover 확보. `DesignSystem.cs:63` | Primary hover |
| C15 P1 | `OnAccent` | `Ink #252A23` → `#20271F` / `#F7FAF2` | Accent 위 글자·아이콘 의미를 명확히 한다. `DesignSystem.cs:52-53,182-195` | Primary·선택·체크 glyph |
| C16 P0 | `Error` | `#FFB4A3` → `#FFB4AB` / `#A33A32` | 오류/파괴 의미 유지, 라이트 대비값 추가. `dialog-confirm.png`, `pack-error.png` | 오류·삭제·정지 |
| C17 P0 | `Warning` | `#E5C58C` → `#F2CD7D` / `#7A5600` | 일시정지·초과시간을 TextSecondary와 분리. `timer-paused.png`, `speech-overtime.png` | 일시정지·자리 비움·참고 |
| C18 P0 | `Success` | 없음 → `#9ED8AC` / `#236B3B` | 저장·설치 완료가 설명문과 같아지는 공백을 해소. `PersonalizationWindow.cs:115-118`; `pack-installed.png` | 저장·적용·설치 완료 |
| C19 P0 | `DisabledFill` | `Surface` + 전체 `.55` → `#292F29` / `#E1E5DF` | 배경까지 투명해지는 현상을 제거. `DesignSystem.cs:67-69`; `pack-installed.png` | 모든 disabled 컨트롤 |
| C20 P0 | `DisabledText` | `Muted` + 전체 `.55` → `#929C91` / `#5E685F` | 제안 fill 대비 약 4.82:1/4.55:1로 라벨 판독 유지. `DesignSystem.cs:67-69` | 모든 disabled 라벨·아이콘 |
| C21 P0 | `FocusRing` | 버튼 `Cream/Ink`, 입력 없음 → `#8FD3FF` / `#005FCC` | 배경·Accent와 다른 단일 의미색. 버튼/입력 적용 사양은 워커 D가 소유. `DesignSystem.cs:70-91` | 모든 키보드 포커스 가능 컨트롤 |
| C22 P1 | `SelectionFill` | `Cream` 공유 → 기본은 `AccentFill`, 필요 시 테마별 파생 | 선택과 Primary가 같은 의미 계열임을 유지하되 독립 변경 가능하게 한다. `DesignSystem.cs:182-200`; `settings-routines-tab.png` | ListBox, ComboBox, 텍스트 선택 |

### 3.2 타이포그래피

시스템 기본 글꼴과 한국어 fallback은 유지한다(`docs/design-system.md:21-24`). 숫자 메트릭은 고정폭 숫자를 지원하는 시스템 폰트 기능 사용 가능성을 P2에서 검토하되 새 글꼴을 번들하지 않는다.

| ID/우선순위 | 토큰 | 현재값 → 제안값 | 근거 | 영향 화면 |
|---|---|---|---|---|
| T01 P0 | `LabelSmall` | 직접 10–11 → 12, Medium | 최소 판독 크기 교정. `SettingsWindow.Layout.cs:110-150`; `settings-minimum.png` | 설정 카드 라벨·도움말 |
| T02 P1 | `Caption` | 12 → 13, Regular, line-height 18 | 긴 설명의 한국어 행간을 안정화. `Ui.cs:32-33`; `custom-pet-minimum.png` | 설명·상태·힌트 |
| T03 P1 | `Body` | 14 → 14, Regular, line-height 20 | 현행 기준 유지. `DesignSystem.cs:20,36-50` | 본문·입력·버튼 |
| T04 P1 | `BodyStrong` | 직접 16 → 16, SemiBold, line-height 22 | 카드 내 핵심 값/짧은 제목의 반복값 흡수. `SettingsWindow.cs:17` | 오늘 상태·중요 값 |
| T05 P1 | `Section` | 선언 18(미사용), 직접 17 → 18, SemiBold, line-height 24 | 공통 섹션 제목으로 실제 사용. `DesignSystem.cs:20`; `SettingsWindow.Notifications.cs:33-34` | 카드 제목·말풍선 제목 |
| T06 P1 | `Heading` | 직접 20/22 → 20, SemiBold, line-height 28 | 이름/요약 계층 통일. `SettingsWindow.Layout.cs:15-16`; `BreakReviewWindow.cs:35` | 펫 이름·회고 요약 |
| T07 P1 | `Title` | 24 → 24, SemiBold, line-height 32 | 페이지 제목 유지. `Ui.cs:75-79` | 공통 페이지 헤더 |
| T08 P1 | `MetricSmall/Medium/Large` | 직접 34/48/52/64 → 34/48/64, Regular 또는 Light | 52 초기값을 제거하고 세 단계로 명명. `PetSpeechBubble.cs:13`; `SettingsWindow.cs:13`; `SettingsWindow.Layout.cs:16,87` | 말풍선·오늘 횟수·타이머 |

### 3.3 간격·반경·경계

| ID/우선순위 | 토큰 | 현재값 → 제안값 | 근거 | 영향 화면 |
|---|---|---|---|---|
| S01 P1 | `Space1` | 직접 4/6/7 → 4(아이콘 내부만), 일반 인접 간격은 8 | 6·7px 변형 축소. `Ui.cs:40,77`; `CustomPetWindow.cs:96-110` | 필드·행동 카드 |
| S02 P1 | `Space2` | `Space 8` → 8 | 기본 행·액션·스크롤 거터 유지. `Ui.cs:25,42-50` | 전 화면 |
| S03 P1 | `Space3` | `Gap 12`, 직접 10 → 12 | 기본 Column과 카드 항목 간격 통일. `Ui.cs:26`; `SettingsWindow.Notifications.cs:71-75` | 전 화면 |
| S04 P1 | `Space4` | 직접 14/16 → 16 | 카드 간·헤더/본문 분리. `SettingsWindow.Layout.cs:30-42`; `Ui.cs:72-84` | 설정·PageContent |
| S05 P1 | `Space5` | `Inset 20`, 직접 18/22 → 20 | 공통 페이지/카드 inset 유지. `Ui.cs:87-96`; `SettingsWindow.Layout.cs:69-82` | 페이지·설정 카드 |
| S06 P1 | `Space6/8` | 직접 24 → 24, 신규 32 | 큰 섹션 분리만 사용. `SettingsWindow.Layout.cs:69-101` | 대시보드 큰 카드 |
| R01 P1 | `ControlRadius` | 12 → 10 | 소형 버튼/입력의 과도한 캡슐감을 줄임. `DesignSystem.cs:41-48,74-84`; `settings-speech-options.png` | 버튼·입력 |
| R02 P1 | `CardRadius` | 24, 직접 18/28 → 16(일반) / 20(강조) | 정보 카드와 강조 표면을 두 단계로 제한. `Ui.cs:34-35`; `SettingsWindow.Layout.cs:102-151` | 카드·미리보기 |
| R03 P2 | `FrameRadius` | 32 → 24(일반 창), 28(설정 대시보드) | 대화상자와 대형 설정 창을 분리해 밀도 조절. `Ui.cs:87-92`; `SettingsWindow.Layout.cs:40-43` | 모든 프레임 |
| R04 P1 | `PillRadius` | 직접 22/64 등 → `999` 또는 높이/2라는 의미 토큰 | 배지·완전 원형만 예외로 명명. `SettingsWindow.Layout.cs:49-82`; `PetWindow.cs:27-33` | 배지·로고·상태 pill |
| B01 P0 | `BorderSubtle/Strong` | 모두 1px 한 색 → 1px/1px, 색 C10/C11 분리 | 두께보다 색 역할을 먼저 분리해 레이아웃 변화를 막는다. `DesignSystem.cs:45-46,81-90`; `Ui.cs:82-92` | 카드·프레임·입력·Quiet |
| F01 P0 | `FocusRingWidth/Offset` | 버튼 border 2, 입력 없음 → 2px / 2px | 컨트롤 크기를 바꾸지 않는 외곽 표시 토큰. 컨트롤별 방식은 워커 D 소유. `DesignSystem.cs:45-46,70-91` | 모든 포커스 가능 컨트롤 |

### 3.4 버튼·입력·카드 변형

| ID/우선순위 | 변형 | 현재값 → 제안값 | 근거 | 영향 화면 |
|---|---|---|---|---|
| V01 P1 | `Button.Primary` | `Cream/Ink` → `AccentFill/OnAccent`; hover=`AccentHover` | 역할 토큰만 교체해 기존 CTA 우선순위 유지. `DesignSystem.cs:52-64` | 적용·저장·설치·휴식 시작 |
| V02 P1 | `Button.Secondary` | `Raised/Cream`, 투명 border → `ControlFill/TextPrimary`, `OutlineSubtle` | 표면 카드와 버튼을 분리. `DesignSystem.cs:41-51` | 편집·파일·재생 |
| V03 P1 | `Button.Quiet` | transparent + `Outline` → transparent + `OutlineStrong/TextSecondary` | 조용하지만 조작 경계는 보이게 한다. `DesignSystem.cs:54-55`; `dialog-prompt.png` | 취소·닫기·새로고침 |
| V04 P1 | `Button.Danger` | 텍스트만 Error → 기본은 ghost Error, 확인 대화상자는 `Error` 12% 컨테이너 + Error | 파괴 동작을 일반 Secondary와 구분. `DesignSystem.cs:56-66`; `dialog-confirm.png` | 삭제·제거 |
| V05 P0 | `Button.Disabled` | `Surface/Muted` + opacity .55 → `DisabledFill/DisabledText/OutlineSubtle`, opacity 1 | 상태와 판독성을 동시에 유지. `DesignSystem.cs:67-69`; `pack-installed.png` | 모든 disabled 버튼 |
| V06 P1 | `Input.Default` | `Shell/Muted 1px` → `FieldFill/OutlineStrong 1px/TextPrimary` | 입력 가능 영역을 표면에서 분리. `DesignSystem.cs:74-120`; `profile-editor.png` | 텍스트·숫자·선택 입력 |
| V07 P0 | `Input.Focus` | 배경/경계 변화 없음, FocusAdorner 없음 → F01/C21 토큰 사용 | 실제 적용은 UI-07 사양에 위임. `DesignSystem.cs:74-91`; `DesignSystemTests.cs:263-345` | 모든 입력 |
| V08 P1 | `Card.Base` | `Surface`, radius 24, padding 16 → `Surface`, radius 16, padding 16 | 기본 정보 그룹의 일관성. `Ui.cs:34-35` | 폼·확인·기록 |
| V09 P1 | `Card.Raised` | `Raised` 임의 사용 → `SurfaceRaised`, radius 20, padding 20 | 대시보드의 핵심 카드에만 사용. `SettingsWindow.Layout.cs:67-102,144-151` | 펫·타이머·오늘 카드 |
| V10 P1 | `Card.Status` | 상태 배지별 임의 구성 → `SurfaceRaised` + 상태 아이콘/텍스트 토큰 | 색 점만이 아니라 텍스트와 함께 상태 전달을 유지. `SettingsWindow.Layout.cs:95-102`; `timer-paused.png`, `timer-stopped.png` | 타이머 상태·저장 피드백 |

## 4. 단계적 적용 순서

### P0 — 현행 결함 교정

1. **P0-A 의미 상태 토큰**: C09–C11, C16–C21, B01, F01을 `DesignSystem.cs`에 추가하고 기존 이름은 한 단계 동안 별칭으로 유지한다. disabled opacity를 제거하고 성공/경고/오류를 설명문과 분리한다.
2. **P0-B 작은 글자 교정**: T01을 설정 전용 직접 10–11px에만 적용한다. 카드 높이·스크롤 회귀가 생기면 설명 삭제가 아니라 줄바꿈/스크롤을 유지한다.
3. **P0-C 포커스 계약 접점**: C21/F01까지만 정의한다. 워커 D가 UI-07에서 버튼·입력·목록·탭·체크박스별 적용과 키보드 수용 기준을 확정한 후 구현한다.

### P1 — 일관성 확보

1. C01–C15/C22로 색 이름을 의미 기반으로 이행하고, T02–T08/S01–S06/R01–R04를 공통 토큰화한다.
2. V01–V10을 `Ui` 팩토리/클래스로 제공한 다음 공통 Page·대화상자 → 설정 대시보드 → 펫 팩/제작 → 진단 전용 호환 화면 순으로 전환한다.
3. 화면별 직접값을 한 번에 제거하지 않는다. 각 묶음마다 기본·최소·스크롤 PNG를 재생성해 높이·줄바꿈 회귀를 확인한다.

### P2 — 선택적 개선

1. OS 테마를 따를지 앱 설정으로 둘지 제품 결정을 먼저 한 뒤 라이트 팔레트를 활성화한다. 현재는 `RequestedThemeVariant = Dark`로 고정돼 있다(`DesignSystem.cs:24-34`).
2. R03의 프레임 반경 축소, 메트릭 고정폭 숫자, 장식 카드 반경 정리는 사용자 선호와 실제 화면 비교 후 선택한다.
3. 진단 전용 픽셀 에디터는 MVP 사용자 화면이 아니므로 마지막에 토큰만 상속하고 배치 재설계는 하지 않는다(`docs/mvp.md:53-64`; `editor.png`).

## 5. 소유 파일·의존성·검증

| 작업 | 우선순위 | 소유 파일 | 의존성/접점 | 검증 |
|---|---|---|---|---|
| 의미 색·disabled·상태 토큰(C09–C20, B01) | P0 | `src/Unfold.Desktop/DesignSystem.cs`, `Tests/Unfold.Tests/DesignSystemTests.cs`, `docs/design-system.md` | 기존 테스트가 `Cream/Muted/Surface` 정확값을 단언하므로 동시 갱신 필요(`DesignSystemTests.cs:194-219,223-240`). | V1, V2, V3, V5 |
| 포커스 토큰(C21, F01) | P0 | `DesignSystem.cs` | **워커 D UI-07이 컨트롤별 적용 사양 소유**. 이 보고서는 토큰만 확정. | V1, V2, V4, Windows/macOS 실기 |
| 설정 10–11px 교정(T01) | P0 | `src/Unfold.Desktop/SettingsWindow.Layout.cs`, `SettingsWindow.Notifications.cs` | 글자 증가로 300px 우측 열과 최소 높이 스크롤량 변화 가능. | V2, V3, V5 (`settings*.png`) |
| 의미 색 이름·타이포·간격 이행(C01–C15/C22, T02–T08, S01–S06) | P1 | `DesignSystem.cs`, `Ui.cs`, 각 View/Window의 직접값 | 워커 B 화면별 gap 결과와 화면 묶음 순서 조율. 제품 로직 의존 없음. | V1–V5 |
| 버튼·입력·카드 변형(V01–V10, R01–R04) | P1 | `DesignSystem.cs`, `Ui.cs`, `SettingsWindow.Layout.cs`, `CustomPetWindow.cs`, `PetPackWindow.cs`, `PersonalizationWindow.cs`, `PetSpeechBubble.cs` | 워커 D의 포커스/상태 사양, 워커 B의 화면별 완료 기준. | V1–V5 + 실제 pointer/keyboard |
| 라이트 팔레트 활성화 | P2 | `DesignSystem.cs`, 앱 테마 선택 정책 파일(구현 시 확정), `docs/design-system.md` | 현재 Dark 강제 제거 정책 결정, OS 테마 변경 이벤트 확인. | V1–V5를 다크/라이트 각각 + 두 OS 실기 |

검증 명령/수용 기준:

- **V1 정적**: `git diff --check -- src/Unfold.Desktop Tests/Unfold.Tests docs/design-system.md`
- **V2 집중 테스트**: `dotnet test Unfold.slnx -c Release --no-restore --filter FullyQualifiedName~DesignSystemTests`
- **V3 전체 회귀**: `dotnet test Unfold.slnx -c Release --no-restore`
- **V4 포커스/상태 자동 검사**: `DesignSystemTests`에 다크/라이트 색 대비, disabled opacity 1, `:focus-visible` 토큰, 입력·목록·탭 포커스 시각 속성 단언을 추가한다. 기존 포커스·입력 테스트 위치는 `DesignSystemTests.cs:194-219,263-345`다.
- **V5 격리 스모크**: 새 빈 `UNFOLD_DATA_DIR`로 `dotnet run --project src/Unfold.Desktop -c Release --no-build -- --smoke-test`를 실행하고, `smoke.json success=true`, PNG 수, 기본/최소/스크롤 캡처를 확인한다(`docs/verification.md:69-91`). 색 변경 전후에는 `settings.png`, `settings-minimum.png`, `settings-speech-options.png`, `dialog-confirm.png`, `pack-installed.png`, `custom-pet-minimum.png`, `speech-overtime.png`를 최소 비교 세트로 삼는다.

## 6. 다크/라이트 및 Windows/macOS 위험

| 위험 | 깨질 수 있는 항목 | 대응/검증 근거 |
|---|---|---|
| 현재 앱이 Dark를 강제 | OS가 라이트여도 앱은 다크이며 제안 라이트 팔레트가 자동 사용되지 않는다. | `DesignSystem.cs:24-34`. P2에서 정책을 먼저 결정하고 테마별 캡처를 분리한다. |
| `Cream`의 다중 역할 | 라이트에서 Cream을 그대로 Primary/선택/본문에 쓰면 밝은 배경과 강조가 사라진다. | C07/C13/C15/C22로 분리. 현재 결합 근거 `DesignSystem.cs:30-32,52-73,182-200`. |
| Fluent template part 의존 | `PART_ContentPresenter`, `PART_BorderElement`, NumericUpDown 내부 `RepeatButton` 이름/모양이 테마·Avalonia 버전에 따라 달라질 수 있다. | `DesignSystem.cs:99-179`. Windows/macOS 각각 TextBox/ComboBox/NumericUpDown의 default/hover/focus/disabled를 실제 렌더링 확인한다. |
| 시스템 글꼴/fallback 차이 | Windows의 Segoe UI/한국어 fallback과 macOS의 SF 계열/한국어 fallback에서 같은 10–12px의 폭·획 밀도·줄바꿈이 다르다. | T01/T02로 최솟값과 행간을 보장하고 860×680, 500×520, 320px 말풍선을 두 OS에서 확인한다(`settings-minimum.png`, `profile-editor.png`, `speech-top.png`). |
| 스크롤바 두께·overlay 차이 | Fluent scrollbar의 트랙/auto-hide 표현이 OS 및 입력 방식에 따라 달라 최소 폭 거터가 다시 좁아질 수 있다. | `Ui.cs:49-66`; `settings-pet-create-minimum.png`, `custom-pet-minimum.png`. Windows DPI 100/125/150/200%와 macOS Retina에서 실기 확인한다. |
| OS 고대비/강조색 | 고정 hex와 투명 border는 Windows High Contrast에서 소실될 수 있고, macOS Increase Contrast에서도 기대한 단계가 달라질 수 있다. | `OutlineStrong`, FocusRing과 시스템 색 fallback을 UI-07에서 정한다. 자동 PNG는 이 설정을 검증하지 않는다. |
| OS 외곽 UI | 파일 선택기·제목 표시줄·트레이 메뉴는 앱 토큰이 적용되지 않아 라이트/다크 경계가 불연속일 수 있다. | `docs/design-system.md:76-78`. 실제 파일 선택기와 게시 앱에서 확인한다. |
| 반투명 disabled | 전체 opacity는 배경과 합성돼 OS/렌더링 차이에 따라 판독성이 달라진다. | V05에서 opacity 1과 고정 색을 사용한다. 현재 근거 `DesignSystem.cs:67-69`. |

## 7. 자동 확인과 실제 OS 미검증

### 이번 1차에서 확인한 사실

- 현재 체크아웃 코드에서 모든 공통 토큰 선언과 참조를 정적 확인했다(`DesignSystem.cs:12-214`; `Ui.cs:11-167`).
- Phase 2 PNG 42장을 전부 열어 기본·최소·스크롤·상태 화면을 비교했다. 캡처 파이프라인은 96 DPI off-screen `RenderTargetBitmap`이다(`SmokeDiagnostics.cs:516-531`).
- 스크롤 거터와 라이브러리 액션 재배치는 코드와 최신 PNG 양쪽에서 확인했다(`Ui.cs:49-66`; `PersonalizationWindow.cs:67-95`; `settings-pet-create-minimum.png`, `routine-library.png`).
- 이번 작업은 보고서만 작성했으므로 테스트·빌드·새 스모크는 실행하지 않았다.

### 실제 OS에서 여전히 미검증

- 실제 마우스 hover/press, 키보드 Tab 순서·포커스 링 인지성, VoiceOver, Narrator.
- Windows 100/125/150/200% DPI, macOS Retina, 다중 모니터, overlay scrollbar와 테마 전환.
- Windows High Contrast, macOS Increase Contrast/Reduce Transparency, 사용자 강조색.
- 시스템 글꼴 fallback에서 한국어 12/13/14px 줄바꿈과 잘림, 실제 OS 제목 표시줄·파일 선택기·트레이 메뉴의 테마 연결.
- 자동 캡처는 실제 OS 입력과 DPI의 증거가 아니라는 경계는 `docs/verification.md:81-91,119-142`와 같다.

## 8. 접점

- **워커 D / UI-07**: 본 보고서는 `FocusRing` 다크 `#8FD3FF`, 라이트 `#005FCC`, `FocusRingWidth=2`, `FocusRingOffset=2`까지만 정의한다. 버튼·TextBox·NumericUpDown·ComboBox·ListBox·Tab·CheckBox별 적용 방식, 고대비 fallback, 키보드 수용 기준은 D가 작성한다. 근거는 현재 버튼만 `:focus-visible` 경계를 갖고 입력은 `FocusAdorner=null`인 `DesignSystem.cs:70-91`이다.
- **워커 B / 화면별 visual gap**: 설정 우측 카드의 10–11px 밀도, 카드 중첩 위계, 반경 축소의 화면별 적용 순서와 재캡처 완료 기준은 B 결과와 합쳐야 한다. 근거 화면은 `settings.png`, `settings-minimum.png`, `settings-speech-options.png`, `custom-pet-editor.png`다.
- **워커 C / UX flow**: `Success` 토큰은 저장·적용·설치 완료의 시각 상태만 정의한다. 메시지 노출 시간·복구 행동·빈 상태 문구는 C의 흐름 소유다(`PersonalizationWindow.cs:115-118`; `pack-installed.png`).
- `_workspace/08`의 두 공통 레이아웃 결함은 해소됐으므로 되돌리지 않는다. 특히 `Ui.PageBodyScroll`의 거터(`Ui.cs:49-66`)와 `PersonalizationWindow`의 좌/우 액션 그룹(`PersonalizationWindow.cs:67-95`)을 재설계 중 보존한다.
