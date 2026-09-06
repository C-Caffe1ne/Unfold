# Piskel 툴 레일 오른쪽 통합 설계

작성일: 2026-09-06
상태: 설계 승인됨, 구현 계획 대기
선행: [Piskel 다크 리스킨](2026-09-06-piskel-dark-reskin-design.md) (이 위에 얹음)

## 배경

리스킨 후속 요청. 현재 Piskel 에디터는 좌우 양쪽에 세로 레일이 있다:

- 왼쪽 `#tool-section` — 펜 크기, 드로잉 툴, 팔레트(컬러픽커 + swap colors)
- 왼쪽 `.left-column` — 프레임 타임라인 (`#preview-list-wrapper`)
- 오른쪽 `#application-action-section` — preferences, resize (+ save/export/import, 리스킨에서 숨김)

사용자는 **왼쪽 툴 레일을 없애고 오른쪽 한 줄로 통합**하길 원한다. 최종 오른쪽
레일 순서(위→아래): 펜 크기 → 드로잉 툴 → 컬러픽커 → swap colors → (간격) →
preferences → resize. 프레임 타임라인은 왼쪽에 남는다. 캔버스는 비워진 왼쪽
폭을 회수해 넓어진다.

## 접근 선택

3가지를 검토해 **접근 B (정렬된 두 클러스터)** 를 택했다.

- **A — 완전 연속 단일 레일 (기각):** JS로 두 섹션을 새 flex 컨테이너에 넣어
  세로로 이어붙임. 요청 순서를 정확히 만족하지만 `#application-action-section`
  이 `position: fixed` 를 잃어 드로어 열림(6종 설정 패널)을 `transform` 기반으로
  재구현해야 함. 중간 리스크.
- **B — 정렬된 두 클러스터 (채택):** `#tool-section` 만 우상단으로 옮기고,
  `#application-action-section` 은 기능 무손상으로 우하단에 정렬 + 리스킨 스타일
  공유. 두 클러스터가 한 레일처럼 보이되 사이에 세로 간격이 있음. `unfold-bridge.css`
  만으로 구현 가능(JS 불필요). 낮은 리스크, 의도의 ~90%.
- **C — 오른쪽 2열 레일 (기각):** 어색함.

## Piskel JS 제약 (조사 결과)

`js/piskel-packaged-*.js` 확인:

- `ToolController` 가 `#tool-section` 에 `mousedown` 리스너를 위임한다. 또
  `#tool-section .tool-icon.selected` 하위 셀렉터를 쓴다. → **드로잉 툴은
  `#tool-section` 안에 남아야 한다.** 노드 통째 이동 또는 위치만 CSS 조정은 안전.
- `SettingsController` 가 `[data-pskl-controller=settings]`
  (= `#application-action-section`) 에 `click` 리스너를 위임하고, `.expanded`
  클래스를 이 섹션에 붙여 `right: 0 → 280px` 애니메이션으로 드로어를 연다. →
  **설정 아이콘은 이 섹션 안에 남아야 하고, 섹션은 `position: fixed` + `right`
  기반이어야 드로어가 작동한다.**
- 캔버스 사이징 루틴이 `getSelectorWidth_("#tool-section")` +
  `"#application-action-section"` 폭을 더해 사용 폭을 계산한다. 위치를 바꿔도 이
  계산은 그대로라 캔버스 폭 변화는 작다. 실제 확장 레버는 `.column-wrapper` 의
  `left`/`right` 오버라이드다.

이 리스너들은 위치가 아니라 노드에 걸려 있어, `position: fixed` 값만 덮으면
접근 B는 CSS만으로 성립한다.

## 구현 대상 파일

`Sources/Unfold/Resources/Editor/unfold-bridge.css` — 리스킨 블록 아래에
`/* ===== Unfold tool-rail consolidation ===== */` 섹션 추가. `unfold-bridge.js`
및 벤더 `piskel/` 는 손대지 않는다.

## 1. 왼쪽 — 공간 회수

```css
.column-wrapper {
  left: 0 !important;      /* was 100px — 툴 레일이 쓰던 폭 */
  right: 124px !important; /* was 50px — right:12 + width:100 + 12 여유 */
}
```

프레임 타임라인이 왼쪽 끝(x=0)으로 붙고, `.main-column`(`flex: 1`)이 넓어진다.
§2·§3에서 두 레일의 `margin` 을 `0` 으로 눌러(리스킨 §4가 준
`margin: var(--u-gap)` 상쇄) 폭 계산을 `right`/`width` 로만 하도록 한다.

## 2. 오른쪽 — 툴 클러스터 (상단)

`#tool-section.left-sticky-section` 을 우상단 content-height 로:

```css
#tool-section.left-sticky-section {
  left: auto !important;
  right: 12px !important;
  top: 12px !important;
  bottom: auto !important;
  margin: 0 !important;          /* 리스킨 §4의 margin: var(--u-gap) 상쇄 */
  max-width: none !important;
  width: 100px !important;
  max-height: calc(100vh - 24px) !important;
  overflow-y: auto !important;
}
#tool-section .sticky-section-wrap { display: block !important; height: auto !important; }
#tool-section .vertical-centerer   { display: block !important; }

/* float 잔재 정리, 2열 중앙 정렬 */
#tools-container.tools-wrapper {
  display: flex !important;
  flex-wrap: wrap !important;
  justify-content: center !important;
}
#tool-section .tool-icon { float: none !important; }

/* 넘칠 때 스크롤바 숨김 (다크 유지) */
#tool-section::-webkit-scrollbar { width: 0 !important; height: 0 !important; }
```

내부 순서는 DOM 그대로 (펜 크기 → `#tools-container` → `.palette-wrapper` =
주 픽커 → 보조 픽커 → swap). 재정렬 불필요.

## 3. 오른쪽 — 설정 클러스터 (하단)

`#application-action-section.right-sticky-section` 은 `right` 유지, 세로를
하단으로:

```css
#application-action-section.right-sticky-section {
  right: 12px !important;
  top: auto !important;
  bottom: 12px !important;
  margin: 0 !important;          /* 리스킨 §4의 margin: var(--u-gap) 상쇄 */
  width: 100px !important;
}
#application-action-section .sticky-section-wrap { display: block !important; height: auto !important; }
#application-action-section .vertical-centerer   { display: block !important; }

/* 드로어가 열릴 때만 전체 높이로 복원 — 550px 드로어가 화면 밖으로
 * 나가지 않도록. 리스킨 CSS의 .right-sticky-section.expanded 규칙과 공존. */
#application-action-section.right-sticky-section.expanded {
  top: 12px !important;
  bottom: 12px !important;
}
```

위 base 규칙의 `right: 12px !important` 가 모든 상태에서 매칭되어 벤더의
`.right-sticky-section.expanded { right: 280px }`(no !important)를 덮어버린다.
그래서 `.expanded` 규칙에서 `right: 280px !important` 로 슬라이드를 다시 명시한다.
`transition: all 200ms`(벤더)로 `right` 이동은 애니메이션되지만 `top: auto` ↔
`12px` 전환은 브라우저가 보간하지 않아 높이는 점프한다.

## 4. 통일감

두 클러스터는 리스킨 §4에서 이미 `--u-panel-bg` / `--u-radius-panel` /
`--u-shadow` / `margin: --u-gap` 을 공유한다. 여기서는 폭(둘 다 100px)과 정렬만
맞춘다. 사이 간격은 상·하단 고정 + 각자의 `margin` 으로 자연히 생긴다.
사용자가 명시적 구분선을 원하면 설정 클러스터에
`border-top: 1px solid var(--u-control-bg)` 를 추가 (기본은 간격만).

## 5. 검증 (수동)

`swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"`
→ Settings → 캐릭터 편집 → 에디터.

- [ ] 왼쪽에 프레임 타임라인만. 툴 레일 없음. 캔버스가 왼쪽으로 넓어짐
- [ ] 우상단: 펜 크기 → 드로잉 툴(2열 중앙정렬) → 컬러픽커 → swap colors
- [ ] 우하단: preferences, resize
- [ ] 각 드로잉 툴 클릭 → 선택 표시(틸 테두리) + 실제 드로잉 동작 (`mousedown` 위임 정상)
- [ ] 펜 크기 1~4 동작
- [ ] 주/보조 컬러픽커 열림, swap colors 동작
- [ ] preferences 클릭 → 드로어 열림, 전체 높이 복원, 내용 정상
- [ ] resize 클릭 → 드로어 정상, 리사이즈 적용
- [ ] 팔레트 관리 / 백업 등 다른 드로어 정상
- [ ] 드로어 닫으면 설정 클러스터가 하단으로 복귀
- [ ] 창을 세로로 좁히면 툴 레일 내부 스크롤, 잘림 없음
- [ ] 캔버스 그리기 영역이 레일과 겹치지 않음

## 6. 리스크

- **드로어 열림 시 설정 클러스터 하단→전체높이 점프**가 상단 툴 클러스터와
  시각적으로 겹칠 수 있다. 검증에서 거슬리면 `.expanded` 상태에서
  `#tool-section { opacity: 0.3 !important; pointer-events: none !important; }`
  보정을 추가한다 (계획에 조건부 태스크로 포함).
- **캔버스 폭**: JS `usedWidth` 가 두 섹션 폭을 계속 차감. `.column-wrapper`
  오버라이드가 실제 레버이므로, 기대만큼 안 넓어지면 `right` 값을 미세조정.
- **셀렉터 취약성**: 리스킨과 동일. Piskel 재벤더링 시 재점검. 블록에 "Piskel
  commit a6b9c02 기준" 주석.

## 7. 범위 밖 (YAGNI)

- `unfold-bridge.js` 수정 (B는 CSS로 성립; 검증에서 막힐 때만 fallback,
  그 경우 별도 논의)
- 접근 A의 완전 연속 레일 / 드로어 `transform` 재구현
- 프레임 타임라인 위치·스타일 변경
- 툴 아이콘 자체 재디자인
- Swift 변경
