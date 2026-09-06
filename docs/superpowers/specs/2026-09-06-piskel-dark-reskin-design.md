# Piskel 다크 리스킨 설계

작성일: 2026-09-06
상태: 설계 승인됨, 구현 계획 대기

## 배경

캐릭터 에디터는 [`CharacterEditorWindowController`](../../../Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift)
가 `WKWebView` 로 띄우는 벤더링된 Piskel 웹앱이다. Piskel 기본 UI는 새까만
배경 + 노란 골드 포인트, 각진 모서리, 화면 가장자리에 붙은 패널 구조다.
사용자는 이 룩을 다음 방향으로 바꾸고 싶어 한다 (레퍼런스 이미지를 참고만 함,
그대로 복제 아님):

- 다크 테마 유지
- 큰 둥근 모서리, 알약형 버튼/칩
- 패널을 분리해 띄우고 부드러운 그림자
- 중앙 픽셀 캔버스는 어두운 워크스페이스로 유지
- 포인트 색을 골드에서 딥 틸그린 `#357867` 로 교체

레퍼런스의 "밝은 외곽 크롬"은 채택하지 않는다. 전체 다크를 유지한다.

## 제약

벤더링된 `Sources/Unfold/Resources/Editor/piskel/` 는 수정하지 않는다.
[`PISKEL-VERSION.txt`](../../../Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt)
의 업데이트 모델은 "디렉터리 전체 교체, 재적용할 패치 없음" 이다. 모든
커스터마이즈는 런타임에 `WKUserScript` 로 주입되는
[`unfold-bridge.css`](../../../Sources/Unfold/Resources/Editor/unfold-bridge.css)
/ `unfold-bridge.js` 에만 존재한다.

Piskel 빌드는 패키지 JS/CSS 파일명에 wall-clock 타임스탬프를 박는다. 따라서
파일명이나 파일 내용에 의존하는 오버라이드는 버전 업 때마다 깨진다. 오버라이드는
**클래스명/구조 셀렉터 기반**으로만 작성한다.

## 접근

검토한 3가지 중 **접근 1 (토큰 오버라이드 레이어)** 를 채택했다.

- **접근 1 — 토큰 오버라이드 레이어 (채택):** `unfold-bridge.css` 에 CSS 변수와
  타겟 오버라이드 규칙을 섹션별로 추가. 단일 파일, 기존 프로젝트 모델과 동일,
  재벤더링 시 같은 파일 재주입으로 생존.
- **접근 2 — Piskel 소스에서 테마 빌드 (기각):** 업스트림 포크 + LESS 수정 +
  `npm run build` + 재벤더링. Node 빌드 필요, 업스트림 반영 때마다 반복, "패치
  없음" 원칙과 충돌.
- **접근 3 — JS 리스타일 (보조만):** `unfold-bridge.js` 로 클래스 태깅/인라인
  스타일. 페인트 후 실행이라 깜빡임, 리뷰 어려움. 접근 1로 막히는 구조 변경이
  나올 때만 fallback. 현재 설계 범위에서는 필요 없다고 판단.

## 구현 대상 파일

`Sources/Unfold/Resources/Editor/unfold-bridge.css` 하단에
`/* ===== Unfold reskin ===== */` 블록 추가. 아래 8개 하위 섹션 순서대로.
`unfold-bridge.js` 는 수정하지 않는다.

## 1. 디자인 토큰

`:root` 에 주입한다.

```css
:root {
  --u-accent:       #357867;             /* gold 대체: 채움/활성 상태 */
  --u-accent-fg:    #5fae9c;             /* 어두운 배경 위 텍스트/아이콘/1px 테두리 */
  --u-panel-bg:     #1c1c1e;             /* 떠 있는 패널 표면 */
  --u-page-bg:      #0f0f10;             /* 패널 사이로 보이는 바닥 */
  --u-control-bg:   #2a2a2d;             /* 비활성 컨트롤/칩 */
  --u-radius-panel:   20px;
  --u-radius-control: 12px;
  --u-radius-pill:    999px;
  --u-shadow:        0 10px 30px rgba(0, 0, 0, 0.45);
  --u-shadow-modal:  0 24px 60px rgba(0, 0, 0, 0.55);
  --u-gap:           12px;
}
```

`#357867` 은 명도가 낮아 새까만 배경 위 얇은 선·작은 텍스트에서 대비가 부족하다.
**규칙: 채움/활성 배경 = `--u-accent`, 텍스트·아이콘·1px 테두리 = `--u-accent-fg`.**

## 2. 액센트 치환

Piskel 은 `:root { --highlight-color: gold }` 를 이미 정의하고 일부 규칙에서
`var(--highlight-color)` 를 쓴다. 먼저 이 변수를 재정의한다.

```css
:root { --highlight-color: var(--u-accent); }
```

그다음 리터럴 `gold` 을 쓰는 규칙(패키지 CSS 기준 약 50곳)을 영역별로 오버라이드한다.
값 종류에 따라 `--u-accent` 또는 `--u-accent-fg` 로 매핑한다.

| 영역 | 셀렉터 (대표) | 매핑 |
| --- | --- | --- |
| 주요 버튼 채움 | `.button-primary` | `background-color: var(--u-accent)`, `color: #fff` |
| 버튼 hover 텍스트 | `.button:hover` | `color: var(--u-accent-fg)` |
| 좌표/줌 표시 | `.cursor-coordinates` | `color: var(--u-accent-fg)` |
| 설정 제목/설명 강조 | `.settings-title` 등 `color: gold` 규칙 | `color: var(--u-accent-fg)` |
| 선택된 툴 | `.tool-icon.selected` (`border: 3px solid gold`) | `border-color: var(--u-accent)` |
| 확장 드로어 탭 | `.right-sticky-section .tool-icon.has-expanded-drawer` (`border-left: 3px solid gold`) | `border-left-color: var(--u-accent)` |
| enabled 토글 | `.preview-toggle-onion-skin-enabled`, `.layers-toggle-preview-enabled` 등 | 텍스트/테두리 `--u-accent-fg`, 채움 `--u-accent` |
| 다이얼로그 테두리 | `#dialog-container` 계열 `border: 3px solid gold` | `border-color: var(--u-accent)` |
| textfield focus | `.textfield:focus` (`border-color: gold`) | `border-color: var(--u-accent-fg)` |
| 인라인 SVG 화살표 아이콘 (data URI `stroke="gold"`) | 해당 배경 이미지 규칙 | 동일 도형을 `stroke='%23357867'` 로 바꾼 data URI 로 교체 |

골드 PNG 아이콘 자산(`img/icons/**/*-gold*.png`)은 교체 범위 밖이다. 개수가 적고
(키보드 단축키 아이콘, 미니맵 그리드/프리뷰 화살표) 다크 배경에서 크게 튀지 않아
그대로 둔다. 실사용에서 거슬리면 후속 작업으로 뺀다.

컬러피커의 파란 그라디언트(`#529de1 → #245e8f`)는 색상 선택 UI의 기능색이므로
**유지한다.** 틸 패널과 나란히 놓고 확인해 시각적으로 부딪히면 그때 조정한다.

## 3. 라운드

| 대상 | 셀렉터 | border-radius |
| --- | --- | --- |
| 일반 버튼/입력 | `.button`, `.textfield` | `var(--u-radius-control)` (기존 2px) |
| 주요 액션 버튼 | `.button-primary`, `#unfold-save-button`, 다이얼로그 confirm/cancel | `var(--u-radius-pill)` |
| 패널류 | `.drawer-content`, `.dialog-content`, sticky 섹션 배경(§5), 하단 프레임 컨테이너 | `var(--u-radius-panel)` |
| 프레임 타일 / 툴 아이콘 | `.preview-tile` 계열, `.tool-icon` | `var(--u-radius-control)` |

`.size-picker-option` 같은 작은 정사각 토글은 pill 로 만들면 뭉개지므로
`--u-radius-control` 까지만 적용한다.

## 4. 플로팅 패널

Piskel sticky 섹션은 `position: fixed` + `max-width`/`width`/`left`/`right` 로
레이아웃이 계산된다. **위치·크기 속성은 건드리지 않는다.** 시각 껍데기만 입힌다.

```css
.left-sticky-section,
.right-sticky-section,
.drawer-content,
/* 하단 프레임 리스트 컨테이너 (실제 클래스는 구현 시 DOM에서 확인) */ {
  background: var(--u-panel-bg);
  border-radius: var(--u-radius-panel);
  box-shadow: var(--u-shadow);
  margin: var(--u-gap);
}
```

- 페이지 바닥을 `--u-page-bg` 로 깔아 `--u-gap` 간격에 보이게 한다:
  `.main-wrapper` 또는 `body` 에 `background: var(--u-page-bg)`.
- `.column-wrapper` 의 `left: 100px / right: 50px` 예약 폭은 유지한다. 패널에
  `margin` 을 주면 예약 폭 안에서 살짝 안쪽으로 들어오는 정도이며 캔버스 영역
  계산에는 영향이 없다. 구현 시 실제로 캔버스가 밀리는지 확인하고, 밀리면
  `margin` 대신 `transform` 또는 내부 `padding` 으로 조정한다.
- `.drawer-content` 의 기존 `box-shadow: 0 0 5px 0 black` 과 한쪽만 둥근
  `border-*-radius: 4px` 는 새 값으로 덮어쓴다.

## 5. 알약 / 칩

- `.button-primary`, `#unfold-save-button`, 다이얼로그 액션 버튼: `--u-radius-pill`
  + `padding: 10px 22px` + `height: auto`.
- 카테고리/토글성 작은 버튼 중 텍스트 라벨이 있는 것만 pill. 아이콘 전용
  정사각 버튼은 §3 의 `--u-radius-control` 유지.

## 6. 모달

```css
#dialog-container,
.dialog-content {
  background: var(--u-panel-bg);
  border-radius: var(--u-radius-panel);
  box-shadow: var(--u-shadow-modal);
}
.dialog-close {
  background: var(--u-control-bg);
  border-radius: var(--u-radius-pill);   /* 원형 */
}
.dialog-head {
  border-bottom-color: var(--u-accent-fg);  /* 기존 gold 밑줄 */
}
#dialog-container-wrapper {
  /* 선택: 오버레이 */
  backdrop-filter: blur(2px);
}
```

대상 모달: resize, keyboard shortcuts, create/import palette, browse backups,
browse local. export/save/import 패널은 이미 `unfold-bridge.css` 에서 숨겨져 있어
제외.

## 7. 검증 (수동)

에디터를 실제로 띄워 확인한다.

- 각 드로잉 툴 선택 시 `--u-accent` 테두리
- 오른쪽 드로어 펼침/접힘, 확장 탭 액센트
- 저장 버튼 기본/hover/disabled 상태
- resize · keyboard shortcuts · palette · backups 모달 각각 라운드/그림자/버튼
- 캔버스 영역이 패널 margin 때문에 밀리지 않는지
- 좌표 표시, 온온스킨/레이어 토글 enabled 상태 색
- 라이트/다크 무관 (Piskel 은 자체 다크 고정)

## 8. 범위 밖 (YAGNI)

- 밝은 외곽 크롬, 레이아웃 재배치
- 아이콘 PNG 자산 교체, 커스텀 아이콘 폰트
- `unfold-bridge.js` 수정
- 컬러피커 파란 그라디언트 재색
- 골드 PNG 아이콘 3종 교체 (거슬리면 후속)
- Swift 쪽 변경

## 리스크

- **셀렉터 취약성:** Piskel 업스트림이 클래스명을 바꾸면 오버라이드가 조용히
  무력화된다. 업데이트 모델이 이미 "재벤더링 후 브리지 수동 점검"을 전제하므로
  허용 범위. 오버라이드 블록에 주석으로 "Piskel 버전 X 기준 셀렉터" 명시.
- **specificity 싸움:** 일부 규칙은 `!important` 필요. 기존
  `unfold-bridge.css` 도 이미 `!important` 를 쓰므로 일관됨. 남발하지 않고
  필요한 선언에만.
- **캔버스 밀림:** §4 참고. 구현 중 확인, margin 방식이 캔버스를 밀면 대안 적용.
