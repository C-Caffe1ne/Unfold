# Piskel 내장 캐릭터 에디터 — 설계

날짜: 2026-09-03
상태: 승인됨 (2026-09-03)

## 목표

사용자가 Unfold 안에서 직접 픽셀 캐릭터를 그려 데스크톱 펫과 스트레칭 알림에
쓸 수 있게 한다. 그리기 도구는 Piskel(Apache-2.0)을 앱에 내장해 쓰고,
Unfold는 그 결과물을 기존 캐릭터 패키지 포맷으로 받아 저장한다.

## 배경

Unfold의 캐릭터 파이프라인은 이 기능을 받을 자리를 이미 비워 두었다.

- `CharacterRepository`는 프로토콜이고 `ImportedCharacterRepository`는 빈
  배열을 돌려주는 스텁이다.
- `CharacterPackageLoader.loadImported(packageDirectory:)`는 구현돼 있으나
  호출자가 없다.
- `CharacterSource.imported(packageURL:)`가 도메인 모델에 존재한다.
- `SettingsView`의 Characters 섹션에 "Import Character…" 확장 지점이
  주석으로 예약돼 있다.
- `SpriteAnimationView`는 `interpolation` 파라미터를 노출하며 주석에
  "`.none` keeps pixel art crisp"라고 적혀 있다.

따라서 이 작업은 **읽는 쪽이 아니라 쓰는 쪽을 만드는 일**이다.

## 왜 Piskel인가

Aseprite는 자체 EULA라 제3자 제품에 넣어 배포할 수 없다("You may not
distribute copies of the SOFTWARE PRODUCT to third parties", "compile and
modify ... for your own personal purpose"). Piskel은 Apache-2.0이라 상업적
내장·재배포가 허용되고, 순수 JS/HTML/CSS라 `WKWebView`로 그대로 띄울 수 있다.
캔버스 최대 크기는 1024×1024(`Constants.MAX_WIDTH/MAX_HEIGHT`)로 이 앱에
필요한 범위를 넉넉히 덮는다.

## 결정 사항

| 항목 | 결정 | 근거 |
|---|---|---|
| 애니메이션 범위 | `idle` 하나만 요구 | 진입 장벽 최소화. 부족한 키는 폴백으로 메운다 |
| 해상도 | 저해상도 그대로 저장 + nearest 렌더 | `SpriteSheetImage`가 실제 픽셀 크기 기준으로 크롭하므로 로더 변경이 없다. 파일도 작다 |
| Piskel UI | 저장 경로만 교체 | 포크 diff를 0으로 유지 |
| 편집 재개 | 지원. `.piskel` 원본 동봉 | 픽셀아트는 반복 수정이 본질이고 비용이 거의 없다 |
| 통합 방식 | 무수정 벤더링 + 주입 브릿지 | 업스트림 대비 diff 0, 앱 빌드에 Node 의존 없음 |

## 아키텍처

```
[Settings] "Create Character…"
      ↓
CharacterEditorWindowController  (NSWindow + WKWebView)
      ↓  WKUserScript 주입 (unfold-bridge.js / .css / __unfoldInit)
   Piskel (무수정 벤더링 빌드 산출물)
      ↓  "Unfold에 저장" → WKScriptMessageHandler "unfold"
EditorSavePayload  (검증된 값 객체)
      ↓
CharacterPackageWriter  →  Application Support/Unfold/Characters/<id>/
      ↓                      character.json + spritesheet.png + source.piskel
ImportedCharacterRepository
      ↓
CompositeCharacterRepository → CharacterManager → 기존 렌더 경로
```

### 새 컴포넌트

| 컴포넌트 | 책임 | 의존 |
|---|---|---|
| `CharacterEditorWindowController` | 에디터 창 수명, WKWebView 구성, 스크립트 주입, 저장/취소 처리 | `CharacterPackageWriter`, `CharacterLibrary` |
| `EditorSavePayload` | 브릿지 JSON을 검증된 Swift 값으로 변환. 순수 타입 | 없음 |
| `CharacterPackageWriter` | 페이로드 → 디스크 위 패키지. 원자적 쓰기 + 자기검증 | `CharacterPackageLoader` |
| `CharacterLibrary` | 사용자 캐릭터 디렉터리의 위치·목록·삭제 | `FileManager` |
| `unfold-bridge.js` / `.css` | 저장 버튼 주입, 익스포트 패널 숨김, 프레임→시트 합성, `.piskel` 직렬화 | 아래 Piskel 전역 API |

### 의존하는 Piskel 전역 API

벤더링된 빌드를 고정하므로 이 표면은 발밑에서 바뀌지 않는다.

- `pskl.app.piskelController.getFrameCount()`
- `pskl.app.piskelController.renderFrameAt(index, preserveOpacity)`
- `pskl.app.piskelController.getWidth() / getHeight() / getFPS()`
- `pskl.app.piskelController.serialize()`
- `pskl.app.piskelController.setPiskel(piskel)`
- `pskl.utils.FrameUtils.toImage(frame, zoom)`
- `pskl.utils.serialization.Deserializer.deserialize(data, onSuccess, onError)`

### 기존 코드 변경

1. **`renderStyle` 추가** — `CharacterManifest`에 `renderStyle: String?`,
   `Character`에 `RenderStyle` (`.smooth` 기본 / `.pixel`).
   `CharacterAnimationView`가 이 값으로 `SpriteAnimationView(interpolation:)`을
   정한다. 알 수 없는 값은 `.smooth`로 떨어진다.
2. **애니메이션 키 폴백** — `Character.resolvedAnimation(for:)`을 도메인
   모델에 추가한다. 폴백 사슬은 `요청한 키 → .idle`.
   `CharacterAnimationView`가 이걸 쓴다. 정책이 뷰가 아니라 모델에 있으므로
   단위 테스트가 된다.
3. **`ImportedCharacterRepository` 구현** — 스텁 자리를 채운다. 깨진
   패키지는 로그를 남기고 건너뛴다.
4. **`CharacterManager.reloadCatalog()`** — `availableCharacters`를
   `@Published private(set)`으로 바꾸고 명시적 리로드를 노출한다. 지금은
   계산 프로퍼티라 저장 직후 SwiftUI가 갱신을 알지 못한다.
5. **`SettingsView` Characters 섹션** — 예약된 확장 지점에
   "Create Character…" / "Edit…" / "Delete"를 추가한다.
6. **`CharacterThumbnailView`** — 사용 가능하면 `idle` 프레임 0을 렌더하고,
   아니면 지금처럼 SF Symbol로 떨어진다.

## 데이터 흐름

### 저장

1. 사용자가 "Unfold에 저장"을 클릭한다.
2. `unfold-bridge.js`가 `getFrameCount()`만큼 `renderFrameAt(i, true)` →
   `FrameUtils.toImage()` → 가로 1행 캔버스에 합성 → `toDataURL("image/png")`.
3. `serialize()`로 `.piskel` JSON을 얻는다.
4. `postMessage({type:"save", width, height, fps, frameCount, sheetPNG, piskelJSON})`.
5. Swift가 `EditorSavePayload`로 디코드·검증한다.
6. 네이티브 시트로 이름을 입력받는다. Piskel HTML은 건드리지 않는다.
7. `CharacterPackageWriter`가 임시 디렉터리에 3개 파일을 쓰고,
   `CharacterPackageLoader.loadImported`로 자기검증한 뒤 최종 위치로 원자적
   이동한다. 실패하면 임시 디렉터리를 지운다.
8. `characterManager.reloadCatalog()`로 목록에 즉시 반영한다.

생성되는 매니페스트:

```json
{
  "id": "user-8B3F…", "name": "마리", "version": 1,
  "thumbnailSymbol": "pawprint.fill",
  "renderStyle": "pixel",
  "spriteSheet": { "file": "spritesheet.png", "columns": 8, "rows": 1,
                   "frameWidth": 64, "frameHeight": 64 },
  "animations": { "idle": { "frames": [0,1,2,3,4,5,6,7], "fps": 12, "loop": true } }
}
```

시트를 1행으로 고정하는 이유는 Piskel의 프레임 순서와 시트 인덱스가 1:1로
맞아 변환에 계산이 들어가지 않기 때문이다. `SpriteSheetImage`는 어떤
그리드든 처리하므로 로더 변경은 없다.

### 편집 재개

"Edit…" → `CharacterLibrary`가 `source.piskel`을 읽는다 → `WKUserScript`로
`window.__unfoldInit = {piskelJSON, name, characterID}`를 document start에
주입한다 → 브릿지가 `Deserializer.deserialize()` 후 `setPiskel()`.
저장 시 같은 `characterID`로 덮어쓴다.

## 검증 규칙

`EditorSavePayload`가 강제한다.

- 프레임 수: 1–24
- 캔버스 변: 1–128px
- fps: 유한한 양수
- `sheetPNG`: `data:image/png;base64,` 프리픽스
- 디코드한 PNG의 실제 크기가 `frameCount × width` × `height`와 일치

이름은 페이로드에 들어 있지 않다. 네이티브 시트에서 받은 뒤
`CharacterPackageWriter`가 "공백 제거 후 비어 있지 않음"을 강제한다.

에디터는 캔버스를 64×64로 시딩하되 크기를 강제하지 않는다. 사용자가
리사이즈하면 그 크기가 매니페스트에 그대로 기록된다. 리사이즈 패널을 숨기지
않으므로 주입 CSS가 작아지고 "숨겼는데 다른 경로로 도달 가능"한 구멍도
생기지 않는다.

## 에러 처리

원칙: **사용자가 그린 것은 어떤 실패 경로에서도 사라지지 않는다.**

| 상황 | 처리 |
|---|---|
| 페이로드 검증 실패 | 저장 거부 + `window.unfoldBridge.saveFailed(msg)`. 창을 닫지 않는다 |
| 디스크 쓰기 / 자기검증 실패 | 임시 디렉터리 삭제, 오류 시트, 창 유지 |
| Piskel 리소스 로드 실패 | 에디터를 열지 않고 네이티브 오류 알림. 빈 웹뷰를 보여주지 않는다 |
| 깨진 사용자 패키지 | 그것만 건너뛰고 로그. 목록 전체가 죽지 않는다 |
| 선택 중인 캐릭터 삭제 | `CharacterManager`의 기존 치유 로직이 처리한다. `reloadCatalog()`만 호출한다 |
| 빈 이름 | 저장 거부. id는 UUID라 이름 중복은 허용한다 |

## 보안 / App Store

- `WKNavigationDelegate`가 `file://` 외 모든 네비게이션을 거부한다. Piskel
  안의 외부 링크가 앱에서 열리지 않고, 원격 코드 경로가 존재하지 않는다.
  심사 가이드라인 2.5.2 관점에서 이것이 핵심이다.
- `loadFileURL(_:allowingReadAccessTo:)`로 에디터 리소스 디렉터리만 읽기를
  허용한다.
- JS가 보낸 값은 전부 불신하고 검증한다. 파일명은 브릿지가 정하지 않고
  writer의 상수다.
- 저장 위치는 앱 컨테이너 안의 Application Support이므로 파일 접근
  엔타이틀먼트가 필요 없다.
- Apache-2.0 의무: `LICENSE` + `NOTICE`를 번들에 동봉하고 Settings에
  오픈소스 고지를 추가한다. 소스를 수정하지 않으므로 "변경 명시" 의무는
  발생하지 않는다.

### 미해결 리스크

샌드박스 앱의 `WKWebView`는 로컬 파일만 읽더라도
`com.apple.security.network.client`가 필요하다는 보고가 있다. 현재
`Packaging/Unfold.entitlements`는 "샌드박스 단 하나"를 의도적으로 유지한다.
작업 2단계에서 실제 번들로 확인하고, 필요하면 왜 필요한지 주석과 함께
추가한다.

## 테스트

기존 스타일(정책·로더 단위 테스트, SwiftUI 수명주기 우회)을 따른다.

- `EditorSavePayloadTests` — 프레임 수 경계(0 / 1 / 24 / 25), 변 크기
  경계(128 / 129), dataURL 프리픽스 위조, 음수·NaN fps, 선언과 실제 PNG
  크기 불일치
- `CharacterPackageWriterTests` — 저장 후 `CharacterPackageLoader`가 읽어내는
  왕복, 실패 시 디스크 잔여물 0, 같은 id 덮어쓰기
- `ImportedCharacterRepositoryTests` — 빈 디렉터리 / 정상 1개 / 깨진 것이
  섞여도 정상만 반환
- `CharacterAnimationFallbackTests` — `stretch` 없는 캐릭터에 `.stretch`를
  요청하면 idle 애니메이터, idle조차 없으면 nil
- `CharacterManifestTests` 확장 — `renderStyle` 부재 시 `.smooth`,
  `"pixel"`, 알 수 없는 값은 `.smooth`

수동 확인(자동화하지 않음): 샌드박스 번들에서 Piskel이 로드되는가, 그리기→
저장→펫 반영, 편집 왕복, nearest 렌더의 선명도.

## 작업 순서

1. **쓰기/읽기 파이프라인** — `CharacterLibrary`, `CharacterPackageWriter`,
   `ImportedCharacterRepository`. Piskel 없이 고정 테스트 데이터로 완결되며
   단독으로도 가치가 있다.
2. **Piskel 벤더링 + WKWebView 로드 확인** — 엔타이틀먼트 리스크를 조기에
   해소한다.
3. **브릿지 저장 경로**
4. **편집 재개 + 삭제**
5. **`renderStyle` / 키 폴백 / 썸네일**

## 범위 밖

- `stretch` 등 추가 클립 제작
- 캐릭터 공유·내보내기(`.unfoldcharacter` 배포 파일)
- 포인터 반응 아트
- Piskel UI 재스킨
- 온보딩 튜토리얼
