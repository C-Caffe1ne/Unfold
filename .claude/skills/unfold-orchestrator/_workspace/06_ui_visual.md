# B3 UI 시각 감사 — Unfold beta v1.1.0

## 요약
- 토큰 체계 자체는 살아 있다. 하드코딩 색은 진단·픽셀 에디터 2개 파일로 격리돼 있고, 화면 색은 모두 테마 브러시를 통과한다. 문제는 토큰이 아니라 **토큰을 쓰는 규칙이 화면마다 다르다**는 점이다.
- 가장 급한 것은 상태 표현이다. 비활성 버튼이 활성 버튼과 사실상 같은 면색이고(P1), 성공 피드백은 중립 회색이라 오류(빨강)와만 구분된다.
- 공통 헬퍼가 중복돼 필드 라벨 4종, 카드 반경 2종, 컨테이너 2종(채움 카드/외곽선 박스), 아이콘 버튼 2종이 공존한다. 한 창(CustomPetWindow) 안에서도 라벨 2종이 섞인다.
- 밝은 테마 4종의 표면 단계가 1.10~1.13:1, 카드 테두리 대비가 1.52~1.55:1이라 카드 경계·호버·구분선이 거의 보이지 않는다. 깊이를 색이 아니라 간격으로만 전달하고 있다.
- 간격은 근접성 원칙이 뒤집힌 구간이 있다(설정 그룹 내부 65~113px > 그룹 사이 43~85px).

## 방법·한계
- 캡처 19장(home/timer/settings-options/review/speech/dialog/pack/custom-pet/account/updates/responsive/theme)과 `src/Unfold.Desktop` 디자인 시스템·화면 코드를 대조했다. 일부는 PIL 크롭·픽셀 샘플로 좌표를 확인했다.
- 테마는 Plum(기본)·MidnightBlue 캡처를 실제로 열고, 나머지는 `DesignSystem.Themes.cs`의 팔레트 값으로 대비를 계산했다(계산 스크립트는 세션 내 1회성, 파일로 남기지 않음).
- 캡처는 off-screen 렌더다. 폰트 힌팅·네이티브 창 장식·호버/포커스 실시간 상태는 확인할 수 없어, 그에 의존하는 항목은 확신도를 낮춰 표시했다.
- `theme-*-builder.png`(= `theme-*-pets.png`), `settings-minimum-scrolled.png`(= `settings-minimum.png`)는 중복 파일이라 해당 상태의 시각 근거가 없다. 빌더 화면은 코드 근거로만 판단했다.
- 빌드·실행·쓰기는 하지 않았다. 원본 폴더는 읽기 전용으로만 접근했다.

## 화면군별 관찰
- **홈(타이머/펫)**: 2열 카드 구성과 Surface/Raised 교차가 일관된다. 다만 최소 창에서 기본 Fluent 스크롤바 썸이 펫 카드 안쪽 22px 지점에 겹쳐 그려진다. 펫 카드 하단에 160px 이상 빈 구간이 남는다.
- **타이머 상태**: 진행(Cream)·일시정지(Muted+경고 점)·휴식 대기(Muted) 모두 배지+숫자 색으로 구분된다. 상태 표현 중 가장 잘 정리된 부분이다. 저장 메시지가 나타나면 우측 열 카드가 약 31px 아래로 밀린다.
- **설정(옵션)**: 라벨 좌 / 컨트롤 우 패턴인데 우측 끝선이 드롭다운 948, 슬라이더 895, 재생 버튼 948, "기본" 927로 들쭉날쭉하다. 640px에서는 행이 줄바꿈되며 그룹 내부 간격이 더 벌어진다.
- **기록**: 상단 아이콘 버튼 3개가 채움 사각 2 + 외곽선 원 1로 섞인다. 지표 숫자 크기가 32/28/32로 가운데만 다르다. 푸터 구분선과 CSV 버튼이 본문 760px 열이 아닌 프레임 전폭에 걸린다.
- **말풍선**: 폭 320 고정, 꼬리 위치(좌/우/상/하)와 텍스트 중앙 정렬 모두 정상이다. 초과 시간은 Warning 토큰으로 표시된다. 좌/우형의 버튼 2개 폭이 서로 다른 점만 눈에 띈다.
- **대화상자**: 본문만 16px 더 들여써져 머리말·구분선·버튼과 좌측 기준선이 어긋난다. 오류 대화상자에는 Error 색이나 아이콘이 전혀 없다.
- **펫 팩 / 커스텀 펫**: 펫 팩 창은 하단에 약 280px 빈 공간이 남는다. 커스텀 펫은 "행동 연결"만 외곽선 그룹 박스로, 나머지 화면의 채움 카드와 다른 컨테이너 언어를 쓴다.
- **계정·업데이트**: 업데이트 창은 머리말(UNFOLD / …)과 푸터 구분선이 없어 다른 모달과 구조가 다르다. 로그인 창은 "로그인 · 회원가입"이 우상단과 머리말에 중복 노출된다.
- **테마**: 4종 모두 표면/강조 역할이 일관되게 적용된다. MidnightBlue는 대비가 가장 넉넉하다. 선택된 테마 행에서만 스와치 내부 색이 사라진다.

## 발견

| ID | 심각도 | 제목 | 근거 | 관찰 | 확신도 | 개선안 | 소유 후보 |
|---|---|---|---|---|---|---|---|
| UI-01 | P1 | 비활성 버튼이 활성 버튼과 구분되지 않음 | app-updates.png / `DesignSystem.cs` `AddButtonState(styles, null, ":disabled", DisabledFill, DisabledText)`, `DesignSystem.Themes.cs:18-29` | "업데이트 확인"(비활성)과 "닫기"(활성)의 면색이 Plum 기준 `#E8E1F0` vs `#ECE5F4`로 거의 같다. 구분은 글자색뿐이고 그 대비는 3.43:1(Plum)~3.56:1(OatLatte)이다. 누를 수 있는 버튼으로 오독된다. | 확인됨 | 비활성 면색을 Canvas 쪽으로 한 단계 더 낮추고(예: Plum `#E0D8EC`) 테두리를 `OutlineSubtle`로 명시해 활성 Raised와 최소 1.3:1 이상 벌린다. 또는 비활성에 한해 테두리 제거 + 면색 투명 처리로 "면이 없는 버튼"으로 구분한다. | `DesignSystem.cs`, `DesignSystem.Themes.cs` |
| UI-02 | P2 | 확인 대화상자에 주 행동 위계가 없고, 위계를 라벨 문자열로 결정 | confirm-stop.png / `Ui.cs:229-230` | `choice.Contains("삭제") … or "중지" …`로 danger를 고르고 **else if (index == 0)** 로 primary를 준다. 그래서 danger 선택지가 있는 대화상자에는 primary가 하나도 없고(중지/취소가 같은 무게), 목록에 없는 새 라벨(예: "로그아웃", "지우기")은 위계를 잃는다. | 확인됨 | 호출부가 역할을 넘기도록 바꾼다(예: `Confirm(owner, title, message, (label, Role.Danger), (label, Role.Quiet))`). 과도기에는 최소한 `else if`를 분리해 index 0에는 항상 강조(primary 또는 danger-filled)를 적용한다. | `Ui.cs` |
| UI-03 | P2 | 성공 피드백이 중립 회색이라 상태가 읽히지 않음 | timer-paused.png("저장했어요.") / `SettingsWindow.cs:196`, `CustomPetWindow.cs:312`, `PetPackWindow.cs:220`, `SettingsWindow.cs:211` | 모든 저장 완료 메시지가 `DesignSystem.Muted`다. `Success` 토큰은 전체 코드에서 타이머 상태 점 1곳(`SettingsWindow.cs:211`)에만 쓰인다. 오류만 색이 있고 성공은 색이 없어, 성공/안내/대기 메시지가 전부 같은 회색이다. 메시지가 나타날 때 레이아웃이 31px 밀리기도 한다. | 확인됨 | 완료 메시지에 `DesignSystem.Success`를 적용하고(대비 6.1~7.2:1로 충분), 상태 줄에 고정 높이(또는 `Height = Caption * 1.6`)를 줘 표시/숨김 시 레이아웃이 움직이지 않게 한다. | `SettingsWindow.cs`, `CustomPetWindow.cs`, `PetPackWindow.cs` |
| UI-04 | P2 | 모달 본문만 16px 더 들여써짐 | dialog-error.png, confirm-stop.png, dialog-prompt.png / `Ui.cs` `ModalBody` → `Card(child)` (기본 padding 16), `ModalPage` | 머리말·제목·구분선·버튼은 프레임 inset(20)에 정렬되는데 본문 텍스트만 x+16 지점에서 시작한다. 캡처에서 제목 x≈50, 본문 x≈68. | 확인됨 | `ModalBody`에서 `Card(child, 0)`을 쓰거나 `body.Padding = new(0)`으로 바꾼다. 본문 상하 여백은 이미 `RowDefinitions("Auto,16,Auto,16,Auto")`가 담당한다. | `Ui.cs` |
| UI-05 | P2 | 필드 라벨 패턴 4종 공존, 한 창 안에서도 혼용 | custom-pet-editor.png, home-default.png, settings-options-default.png / `Ui.cs:38-44`(Caption 12·Muted·간격 6), `PetManagementView.cs:49-55`(Body 14·Cream·간격 8), `SettingsWindow.Layout.cs:186-196`(Body 14·Cream·간격 8, 세로), `SettingsWindow.Layout.cs:160-167`(좌우 그리드) | CustomPetWindow가 `Ui.Field`(393행)와 `PetManagementView.Field`(134·152행)를 함께 쓴다. 같은 성격의 입력 라벨이 화면마다 12/Muted와 14/Cream으로 갈린다. | 확인됨 | `Ui.Field`를 단일 구현으로 모으고 `variant`(dense/standard)와 `orientation`(stacked/inline)만 인자로 둔다. `PetManagementView.Field`와 `BuildHomeTimingCard.Field`는 그 호출로 대체한다. | `Ui.cs`, `PetManagementView.cs`, `SettingsWindow.Layout.cs` |
| UI-06 | P2 | 설정 그룹의 근접성 역전(그룹 내부 간격 > 그룹 사이 간격) | settings-options-default.png(스트레칭 알림 라벨 y225 → 해당 슬라이더 y290 = 65px, 전체 소리 y182 → 스트레칭 y225 = 43px), responsive-settings-top.png(113px vs 85px) / `SettingsWindow.Notifications.cs`, `DesignSystem.cs` `SettingsRowGap = 16` | 소리 그룹은 "라벨+보조설명 / 가져오기 버튼 / 슬라이더+재생"이 세 줄로 흩어지는데, 줄 사이 간격이 그룹 사이 간격보다 커서 어느 슬라이더가 어느 알림에 속하는지 묶여 보이지 않는다. 640px에서 더 심해진다. | 확인됨 | 한 알림을 하나의 서브블록으로 묶고 내부 간격을 `Space(8)`, 서브블록 사이를 `Inset(20)` 이상으로 둔다(토큰 추가 없이 기존 8/20만으로 해결). 좁은 폭에서는 라벨-컨트롤을 한 블록으로 줄바꿈시킨다. | `SettingsWindow.Notifications.cs` |
| UI-07 | P2 | 홈 화면 스크롤바가 카드 위에 겹치고, 기본 Fluent 스크롤바가 그대로 노출 | home-minimum.png(썸이 펫 카드 우측 경계 안쪽 22px, 회색 2px), settings-options-default.png·responsive-settings-top.png(화살표 버튼 + 회색 트랙) / `SettingsWindow.Layout.cs:42-47,112-113` vs `Ui.cs:68-80`(`PageBodyScroll`: `AllowAutoHide=false` + 좌측 거터) | 다른 페이지는 `PageBodyScroll`로 거터를 확보하는데 홈의 `SettingsDetailsScroll`/`SettingsDashboardScroll`/`CompanionPreviewScroll`은 원시 `ScrollViewer`라 썸이 콘텐츠 위에 떠서 그려진다. 스크롤바 자체는 토큰 스타일이 전혀 없는 Fluent 기본형(화살표 버튼 포함)이라 둥근 토큰 UI와 이질적이다. | 확인됨 | 홈의 세 ScrollViewer를 `Ui.PageBodyScroll`로 교체한다. 추가로 `DesignSystem.Install`에 ScrollBar 스타일(트랙 투명/썸 `OutlineStrong`·반경 4·화살표 숨김)을 1회 등록해 앱 전체에 적용한다. | `SettingsWindow.Layout.cs`, `DesignSystem.cs` |
| UI-08 | P2 | 기록 화면 아이콘 버튼 3개의 모양·채움이 서로 다름 | review-default.png(◀ ▶ = 채움 라운드 사각, ↻ = 외곽선 원) / `BreakReviewWindow.cs` | 같은 줄에 붙은 동급 아이콘 버튼인데 면 처리(Raised 채움 vs 투명+테두리)와 반경(라운드 사각 vs 완전 원)이 다르다. 비활성 ▶도 UI-01과 같은 이유로 활성과 거의 같아 보인다. | 확인됨 | 세 버튼 모두 40×40, `ControlRadius(12)`, Raised 채움으로 통일하고 새로고침만 필요하면 아이콘으로 구분한다. 아이콘 버튼 규격을 `DesignSystem`에 상수로 올린다(`IconButtonSize = 40`). | `BreakReviewWindow.cs`, `DesignSystem.cs` |
| UI-09 | P2 | 본문은 760px 중앙 열, 푸터는 프레임 전폭이라 기준선이 어긋남 | review-default.png(카드 220~978, 구분선·CSV 버튼 112~1087) / `Ui.cs` `PageContent`(actionBar가 Grid 전폭), `Ui.CenteredBody` | 중앙 정렬된 본문 열과 푸터 액션의 우측 끝선이 109px 어긋난다. 눈이 따라갈 수직 기준선이 두 개가 된다. | 확인됨 | actionBar의 Child도 `CenteredBody(..., 760)`으로 감싸거나, actionBar에 동일 `MaxWidth` + 중앙 정렬을 적용한다. | `Ui.cs` |
| UI-10 | P2 | 표면 단계와 테두리 대비가 너무 낮아 카드 경계·호버가 보이지 않음 | home-default.png, settings-options-bottom.png / `DesignSystem.Themes.cs:18-26` | 계산값(밝은 테마 3종): Surface/Canvas 1.10~1.13:1, Raised/Shell 1.13~1.15:1, Hover/Surface 1.24~1.32:1, Outline/Surface 1.52~1.55:1. 비텍스트 요소 3:1 기준에 모두 미달해 카드 경계·푸터 구분선·호버 상태가 거의 전달되지 않는다. | 확인됨(값은 코드 계산, 실기 렌더는 추정) | 카드 구분은 테두리 대신 그림자나 한 단계 더 벌린 면색으로 옮긴다. 최소한 `Outline`(Plum `#D5CADE` → `#C3B4D0` 수준)을 Surface 대비 3:1 이상으로 조정하고, `Hover`를 Surface 대비 1.6:1 이상으로 어둡게 한다. | `DesignSystem.Themes.cs` |
| UI-11 | P2 | 의미가 다른 토큰이 같은 값으로 붕괴 | `DesignSystem.Themes.cs:24-26`(Sage: Accent `#365D4C` = Success `#365D4C`), `:18-29`(4테마 모두 Hover = Halo), `:66-68`(`(TextTertiary, palette.Muted)`) | Sage에서는 성공 상태와 강조색이 완전히 같은 색이라 "성공"이 색으로 구분되지 않는다. 3단계로 선언된 텍스트 토큰(Cream/Muted/TextTertiary)은 실제로 2단계뿐이고, 펫 후광(Halo)과 버튼 호버가 같은 값이라 한쪽을 조정하면 다른 쪽이 따라 움직인다. | 확인됨 | Sage의 Success를 Accent보다 밝은 녹색(예: `#2F7A57`)으로 분리하고, TextTertiary에 실제 3단계 값을 주거나 토큰을 삭제한다. Halo를 팔레트에서 독립 값으로 둔다. | `DesignSystem.Themes.cs` |
| UI-12 | P2 | 타이포 스케일 이탈(선언 12/14/18/24 외 10·11·13·16·28·32·64 사용) | settings-options-default.png("Beta v1.1.0" 10px), review-default.png(지표 32/28/32) / `SettingsWindow.Layout.cs:94`(10), `EditorWindow.cs:185`(11), `SettingsWindow.cs:16`(13), `SettingsWindow.cs:20`(16), `BreakReviewWindow.cs:45`(32/28/32), `AnimatedCountdown.cs:45`·`SettingsWindow.Layout.cs:141`(64) | 선언된 스케일은 `Caption 12 / Body 14 / Section 18 / Title 24` 뿐인데 실제로는 7개 값이 더 쓰인다. 특히 기록 지표 3개가 32/28/32로 가운데만 작아 같은 등급의 수치가 다른 크기로 보인다. | 확인됨 | `Display 32`, `Countdown 64`, `Micro 10`을 토큰으로 승격하고 11/13/16/28은 가장 가까운 토큰으로 흡수한다. 기록 지표 3개는 32로 통일하고, 긴 값은 `TextTrimming` 대신 줄바꿈으로 처리한다. | `DesignSystem.cs`, `BreakReviewWindow.cs`, `SettingsWindow.*` |
| UI-13 | P2 | 컨테이너 언어 혼용(카드 반경 24 vs 28, 채움 카드 vs 외곽선 박스) | settings-options-default.png, custom-pet-editor.png / `SettingsWindow.Layout.cs:177,271,282,302`(`new(28)`) vs `:136,156,201,212`(`CardRadius` 24), `DesignSystem.cs`(`ControlRadius 12 / CardRadius 24 / FrameRadius 32`), `CustomPetWindow.cs:152` 주변(행동 연결 외곽선 박스) | 같은 설정 페이지 안에서 카드 반경이 24와 28로 섞인다. 28은 선언된 반경 스케일에 없는 값이다. 커스텀 펫의 "행동 연결"만 테두리 박스로 묶여 다른 화면의 채움 카드와 다른 언어를 쓴다. | 확인됨 | `new(28)` 4곳을 `CardRadius`로 바꾼다. 그룹 컨테이너는 "채움 카드(Surface/Raised)" 하나로 통일하고, 중첩이 필요하면 `Raised` 한 단계만 사용한다. | `SettingsWindow.Layout.cs`, `CustomPetWindow.cs` |
| UI-14 | P3 | 선택된 테마의 색 스와치가 강조 배경과 같은 색이라 사라짐 | theme-MidnightBlue-picker.png(선택 행만 내부 사각형이 보이지 않음) / `SettingsWindow.Themes.cs:43-45`(`Background = Brush.Parse(palette.Accent)`), `:30-32`(선택 시 `primary` 클래스 = Accent 면) | 선택 행의 면색이 그 테마의 Accent인데 스와치 내부 사각형도 같은 Accent라 겹쳐 사라진다. 같은 줄의 "✓"도 아이콘이 아니라 텍스트 글리프여서 다른 아이콘들과 굵기·정렬이 다르다. | 확인됨 | 스와치를 `Surface` 배경의 원 위에 올리거나(내부 사각형 주위에 2px Surface 링), 선택 표시를 테두리 + 체크 아이콘으로 바꾼다. "✓"는 `SvgIcon` 체크로 교체한다. | `SettingsWindow.Themes.cs`, `SvgIcon.cs` |
| UI-15 | P3 | 되돌릴 수 있는 동작에 파괴 스타일(danger) 적용 | settings-options-bottom.png("로그아웃"이 Error 색 글자) / `SettingsWindow.Layout.cs:288`(`Ui.Danger(Ui.Quiet(...))`) | danger는 중지·삭제·종료에 쓰이는 등급인데 로그아웃도 같은 빨강을 받는다. 설정 페이지에서 유일하게 빨간 요소라 가장 위험한 동작처럼 보인다. | 확인됨 | `Ui.Quiet`만 남긴다(테두리 `OutlineStrong` + Cream 글자). danger는 데이터 손실이 따르는 동작으로 한정한다. | `SettingsWindow.Layout.cs` |

## 하드코딩 값·토큰 우회

| 위치 | 값 | 판단 |
|---|---|---|
| `src/Unfold.Desktop/PixelCanvas.cs:50` | `Brush.Parse("#353B47")`, `Brush.Parse("#454D5A")` | 투명 체커보드. 테마와 무관해도 밝은 테마에서 캔버스만 어둡게 보인다. 체커보드를 `Raised`/`Hover` 두 단계로 바꾸면 테마를 따라간다. |
| `src/Unfold.Desktop/PixelCanvas.cs:62` | `Brush.Parse("#33000000")` | 그리드 선. `OutlineSubtle` + Opacity로 대체 가능. |
| `src/Unfold.Desktop/PetPackDiagnostics.cs:49,144` | `Brushes.WhiteSmoke` | 진단 전용 경로. 출시 UI 아님 — 유지해도 무방하되 토큰 사용 시 진단 캡처의 테마 재현성이 올라간다. |
| `src/Unfold.Desktop/SettingsWindow.Themes.cs:43-45` | `Brush.Parse(palette.Accent/.Secondary/.Line)` | 다른 테마의 색을 보여주는 스와치라 의도된 우회. 유지. |
| `src/Unfold.Desktop/DesignSystem.cs` 전역 | `FontSize` 리터럴 10/11/13/16/28/32/64 (UI-12 표 참조) | 스케일 밖 값. 토큰 승격 대상. |
| `src/Unfold.Desktop/SettingsWindow.Layout.cs:177,271,282,302` | `new CornerRadius(28)` | 반경 스케일(12/24/32) 밖 값. |
| `src/Unfold.Desktop/Ui.cs:38` | `Card(..., double padding = 16)` | 카드 패딩 기본값 16이 `Inset(20)`·`Gap(12)` 스케일에 없다. 모달 들여쓰기(UI-04)의 원인이기도 하다. |

## 빠른 개선(한 줄 수정)
1. `Ui.cs` `ModalBody`: `Card(child)` → `Card(child, 0)` — 모든 대화상자의 본문 기준선이 머리말·버튼과 맞는다. (UI-04)
2. `SettingsWindow.Layout.cs:177,271,282,302`: `new(28)` → `CardRadius` — 설정 페이지 카드 반경 통일. (UI-13)
3. `SettingsWindow.cs:196`: `homeTimingStatus.Foreground = DesignSystem.Muted` → `DesignSystem.Success`. 같은 방식으로 `CustomPetWindow.cs:312`, `PetPackWindow.cs:220`. (UI-03)
4. `SettingsWindow.Layout.cs:288`: `Ui.Danger(Ui.Quiet(...))` → `Ui.Quiet(...)`. (UI-15)
5. `BreakReviewWindow.cs:45`: `duration = Ui.Text("", 28)` → `32`. (UI-12)
6. `SettingsWindow.Layout.cs:42,46,112`: `new ScrollViewer { ... }` → `Ui.PageBodyScroll(...)` — 홈의 스크롤바 겹침 해소. (UI-07)
7. `Ui.cs:229-230`: `else if (index == 0)` → `if (index == 0 && !isDanger)`를 분리해 danger 대화상자에도 주 행동을 부여. (UI-02)

## 보고하지 않은 항목
- 긴 펫 이름 말줄임 누락, 픽셀 에디터 영어 UI, A-02 펫 페이지 포커스, GLB 고급 설정 Fluent 노출, P3 별도 추적 항목(이름-only 초안, 중복 RootNode, 내보내기/적용·저장 피드백)은 기존 보고분으로 제외했다.
- `theme-*-builder.png`, `settings-minimum-scrolled.png`는 중복 파일이라 해당 상태의 시각 판단을 하지 않았다.
