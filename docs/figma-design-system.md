# Figma 디자인 시스템

Unfold의 Avalonia 디자인 토큰과 설정 화면을 Figma에서 직접 수정할 수 있도록 네이티브 변수,
컴포넌트 Variant, 편집 가능한 화면 프레임으로 구성했다.

- 파일: [Unfold Design System · Avalonia](https://www.figma.com/design/m88ZXCdRf0NGqHmsUTDR6i)
- 위치: `카페인's team`의 편집 가능한 초안
- 기준 구현: `src/Unfold.Desktop/DesignSystem.cs`, `Ui.cs`, `SettingsWindow.Layout.cs`

## 페이지 구성

| 페이지 | 내용 |
|---|---|
| `00 · Cover & Guide` | 파일 목적, 코드와 Figma의 역할, 편집 순서 |
| `01 · Design System` | 색상·타이포그래피·간격·모서리 토큰, 아이콘과 상태별 컴포넌트 |
| `02 · Settings Dashboard` | 현재 설정 화면 구조와 3개 본문 탭 내비게이션을 반영한 1120×800 편집 프레임 |

Starter 요금제의 페이지 제한에 맞춰 문서와 컴포넌트를 한 페이지 안의 섹션으로 분리했다.

## 편집 단위

`Core Primitives`에는 원시 색상과 숫자 값을, `Unfold Semantics`에는 실제 UI 역할 이름을 둔다.
화면과 컴포넌트는 의미 토큰에 연결되어 있으므로 전체 색상이나 간격을 바꿀 때는 먼저
Variables 패널의 `Unfold Semantics`를 수정한다.

컴포넌트 세트는 다음 Variant와 편집 속성을 제공한다.

| 컴포넌트 | Variant·속성 |
|---|---|
| `Button` | `Style`: Primary·Secondary·Quiet·Danger, `State`: Default·Hover·Pressed·Disabled, `Label` |
| `Icon Button` | `Style`: Primary·Secondary·Quiet, 4개 상태, `Icon` instance swap |
| `Numeric Input` | Default·Focused·Disabled, `Value`; 외곽만 radius와 border를 소유 |
| `Status Badge` | Running·Paused·Stopped·Idle·Break, `Label` |
| `Card` | Surface·Raised, 제목과 설명 |
| `Navigation Item` | Default·Hover·Selected·Disabled, 아이콘 교체 |

숫자 입력은 앱의 현재 규칙과 같이 값을 왼쪽·세로 중앙에 놓는다. Focused 상태는 Default와
같은 배경과 외곽선을 사용하고, Disabled 상태도 내부 칸에 별도 radius나 border를 만들지 않는다.

## 로컬 빌더 플러그인

Figma Desktop에서 `플러그인 > 개발 > 매니페스트에서 플러그인 가져오기`를 선택하고
`tools/figma-unfold-builder/manifest.json`을 지정하면 `Unfold Design System Builder`가 등록된다.
등록 후 `플러그인 > 개발 > Unfold Design System Builder`에서 실행할 수 있다.

플러그인은 누락된 토큰·컴포넌트를 만들고, 기존 컴포넌트 세트가 있으면 그대로 사용한다.
설정 대시보드는 알려진 레이아웃 계약을 보정한다. 코드를 수정한 뒤에는 다음 검사 후 Figma에서
플러그인을 다시 실행한다.

```sh
node --check tools/figma-unfold-builder/code.js
python3 -m json.tool tools/figma-unfold-builder/manifest.json >/dev/null
```

Figma에서 시각안을 바꾼 것만으로 앱 코드가 바뀌지는 않는다. 제품에 반영할 때는 의미 토큰과
Variant 이름을 기준으로 Avalonia 구현을 갱신하고, `docs/verification.md`의 화면·기능 검사를 수행한다.
