# Piskel 캐릭터 에디터 — 진행 상태

이 문서는 다른 세션·다른 환경에서 이어서 작업할 때 가장 먼저 읽어야 하는
파일이다. 스펙(`2026-09-03-piskel-character-editor-design.md`)과 계획
(`2026-09-03-piskel-character-editor.md`)은 "무엇을 왜 만드는가"를 담고,
이 문서는 "지금 실제로 어디까지 됐고, 계획 문서와 실제 코드가 어디서
갈라졌는가"를 담는다.

**이 문서를 먼저 읽지 않고 계획 문서의 코드 블록만 보고 이어서 만들면
Task 4·7·8·9·10에서 이미 고쳐진 취약점/버그를 다시 만들게 된다. 아래
"계획 문서와 실제 코드가 갈라진 지점"을 반드시 읽을 것.**

마지막 갱신: 2026-09-04, 커밋 `6cf4167` 기준.

## 지금 상태 한눈에

- 브랜치: `major` (아직 `main`에 머지되지 않음)
- HEAD: `6cf4167`
- 테스트: `swift test` → **267 tests, 0 failures** (Task 9·10은 단위 테스트를 추가하지 않는다 — 둘 다 UI/WKWebView 결과물)
- 진행: **Task 1~10 완료**, Task 11~12 미착수
  - Task 9: 승인됨 (`76f35d8`)
  - Task 10: 코드 완료 + 스펙/품질 리뷰 통과 (`8d6ecea`, `6cf4167`). **저장→이름→목록/펫 반영, 삭제 왕복은 사람 손으로만 검증 가능** — 이 환경은 `WKWebView` 안의 저장 버튼에 클릭을 주입하지 못한다 (합성 `AXPress`/`CGEvent` 모두 무효). 배선 경로(Settings → Create Character… → Piskel 로드된 에디터 창)와 엔타이틀먼트는 AX로 확인됨.
  - `fd656c1` (`.gitignore`에 `.worktrees/` 한 줄)은 Task 10 리뷰 중 서브에이전트가 만든 무관 커밋 — 소스 변경 없음
- 워크플로: `superpowers:subagent-driven-development` — 태스크마다 구현자
  서브에이전트 1개 + 스펙 준수 검토 1개 + 코드 품질 검토 1개. Critical/
  Important는 반드시 수정 후 재검토, Minor는 상황에 따라 기록만 하고 진행.

## 이어서 진행하는 방법

1. 이 문서 전체를 읽는다
2. `docs/superpowers/specs/2026-09-03-piskel-character-editor-design.md` (설계, 승인됨— 변경 없음)
3. `docs/superpowers/plans/2026-09-03-piskel-character-editor.md` (구현 계획) — **Task 11의 "Files" 섹션부터** 읽는다. Task 1~10은 완료됐다 (Task 9·10 코드는 계획 문서 코드 블록이 아니라 위 "갈라진 지점" + 실제 소스를 근거로)
4. `superpowers:subagent-driven-development` 스킬을 그대로 이어서 쓴다 — 같은 패턴(구현자 → 스펙 검토 → 품질 검토, Critical/Important는 재검토까지)을 유지할 것
5. `swift test`로 267개 테스트가 여전히 통과하는지 먼저 확인한 뒤 Task 11부터 시작
6. Task 10의 저장/삭제 왕복은 아직 사람 검증이 안 됐다 (위 "알려진 리스크" 참고) — Task 11로 넘어가기 전에 한 번 돌려보는 게 좋다

## 태스크별 진행 상황

| Task | 내용 | 상태 | 관련 커밋 |
|---|---|---|---|
| 1 | CharacterLibrary | ✅ 승인 | `f8d94d7`, `6b1ae1c` |
| 2 | RenderStyle | ✅ 승인 | `d17b663`, `d500aea` |
| 3 | 애니메이션 키 폴백 | ✅ 승인 | `94c425c`, `df436c6` |
| 4 | EditorSavePayload | ✅ 승인 | `0199f2d`, `72e425d`, `9a5ce37`, `629dec5` |
| 5 | CharacterPackageWriter | ✅ 승인 | `916dbf7`, `aa5754f` |
| 6 | ImportedCharacterRepository | ✅ 승인 | `b6c60e2`, `e9c89c6` |
| 7 | Piskel 벤더링 | ✅ 승인 | `ba14b32`, `83837b2` |
| 8 | EditorNavigationPolicy | ✅ 승인 | `5f5a5e0`, `f9d0ca1` |
| 9 | 에디터 창 (WKWebView + 브릿지) | ✅ 승인 | `76f35d8` |
| 10 | Settings 배선 (만들기/편집/삭제) | ✅ 코드 완료·리뷰 통과 (저장/삭제 왕복 사람 검증 대기) | `8d6ecea`, `6cf4167` |
| 11 | 썸네일 | ⬜ 미착수 | — |
| 12 | 라이선스 고지 UI | ⬜ 미착수 | — |

문서용 커밋(`ccd0fb4`, `5f4f3b7`, `43b7f6b`, `fd82c88`, `3602a2d`, `232a70d`,
`4acd7c9` 등 `docs:` 접두사)은 위 표에서 생략했다 — 계획/스펙 문서 자체를
고친 커밋이고 소스 변경은 없다.

## 계획 문서와 실제 코드가 갈라진 지점 (중요)

아래 세 태스크는 리뷰 과정에서 계획 문서에 적힌 코드보다 실제 구현이
더 엄격해졌다. **계획 문서의 해당 Task 섹션 코드 블록은 갱신되지
않았으므로, 이 세 곳은 계획 문서가 아니라 실제 소스 파일을 근거로 삼을 것.**

### Task 4 — EditorSavePayload

계획 문서 원본은 fps를 "유한한 양수"로만 검증하고, `sheetPNG` 크기 상한이
없고, PNG 헤더만 읽고 실제 디코드는 하지 않는다. 코드 품질 검토에서
"이건 신뢰할 수 없는 웹뷰 입력을 검증하는 보안 경계다, 공격해봐라"라고
지시한 결과 세 가지가 추가됐다:

- `Constants.editorFPSRange = 1.0...24.0` — Piskel 자체 fps 슬라이더
  (`preview.html`의 `min="0" max="24"`)를 근거로 잡은 상한. 무제한 fps는
  `1/fps` 계산에서 타이머 폭주로 이어질 수 있었다
- `Constants.editorMaxSheetDataURLBytes = 8MB` — 페이로드 문자열이
  검증되기 전에 전부 메모리에 올라가는 것을 막는 크기 상한
- `decodedPixelSize`가 헤더만 읽던 것에서 `CGImageSourceCreateImageAtIndex`로
  실제 전체 디코드하도록 변경. 다만 이것도 완전하지 않다는 게 실측으로
  드러나서 — 원본 PNG를 절반 길이로 자른 것도 여전히 정상 디코드됨 — IEND
  청크(PNG 스트림의 마지막 12바이트) 존재 여부를 추가로 검사해 그 틈을
  막았다 (`pngEndChunk`, `truncatedPNG` 에러 케이스)
- 타입 doc comment가 "존재하는 페이로드는 쓰기에 안전하다"는 과장된 문구에서
  "모양(shape)은 보장하지만 내용(content)은 아니다, 검증과 재생이 같은
  ImageIO 경로를 타므로 그게 안전 경계로 충분하다"는 정확한 문구로 바뀌었다

실제 근거: `Sources/Unfold/CharacterEditor/EditorSavePayload.swift`,
`Sources/Unfold/Support/Constants.swift`,
`Tests/UnfoldTests/EditorSavePayloadTests.swift` (24개 테스트).

### Task 7 — Piskel 벤더링

계획 문서의 `NOTICE-piskel.txt` 템플릿은 Piskel 자체만 언급한다. 코드
품질 검토에서 Piskel이 번들한 MIT 라이선스 서브 의존성(jQuery, jQuery UI,
jQuery Tiny Pub/Sub, JSZip, zlib.js, Spectrum Colorpicker, gif.js, Q,
그리고 Apache-2.0인 bootstrap-tooltip.js)이 고지에서 빠졌다는 지적이
나왔다. 재배포자로서 Unfold가 이 고지 의무를 Piskel의 Apache-2.0 허가와
별개로 진다. 실제 헤더와 업스트림 원본을 직접 대조해 9개 라이브러리를
전부 열거하도록 확장했다 — 이 과정에서 원 리뷰의 두 가지 오류(한 라이브러리를
throttle/debounce로 잘못 특정, "전부 MIT"라는 프레이밍이 틀림 — bootstrap-
tooltip.js는 Apache-2.0)를 구현자가 직접 1차 자료 대조로 잡아냈고, 이후
독립 재검토에서 업스트림 LICENSE 파일을 실제로 가져와 대조해 확인됐다.

런타임 가치가 없는 소스맵 파일도 제거해 `piskel/` 디렉터리가 5.7M → 2.5M로
줄었다.

실제 근거: `Sources/Unfold/Resources/Editor/NOTICE-piskel.txt`,
`Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt`.

### Task 8 — EditorNavigationPolicy

계획 문서 원본 구현(`url.standardized.path`)에는 **실제로 뚫리는 취약점**이
있었다. 평문 경로 순회(`../../../../etc/passwd`)는 막지만, 퍼센트 인코딩한
동일한 순회(`%2e%2e/%2e%2e/etc/passwd`)는 통과시켰다 — `.standardized`가
URL의 인코딩된 문자열 형태에서 `..`를 축약하는데, 그 시점엔 `%2e%2e`가
그냥 불투명한 경로 조각이고, 디코딩은 `.path`가 그 이후에 하기 때문이다.
이건 이 기능 전체가 "원격 코드를 절대 실행하지 않는다"(App Store 가이드라인
2.5.2)는 근거로 삼는 유일한 방어선이었다.

컨트롤러가 직접 Swift 스크립트로 재현해 확인한 뒤 수정을 지시했고, 수정본도
사전에 직접 검증했다. 수정: 디코딩된 경로로 새 URL을 다시 만들어 그것을
표준화(`URL(fileURLWithPath: url.path).standardizedFileURL.path`). 이후
이중/삼중 퍼센트 인코딩, 유니코드 look-alike, 백슬래시, NULL 바이트까지
실제 프로브 스크립트로 재공격했지만 추가 우회는 없었다 — 구조적으로 디코딩이
정확히 한 번만 일어나고 그 뒤로 아무도 재디코딩하지 않아서 비대칭이 생길
수 없다는 것까지 확인됐다.

**Task 9를 위한 주의사항**: `EditorNavigationPolicy`는 아직 어디서도
호출되지 않는다(순수 함수만 존재, `WKNavigationDelegate` 배선은 Task 9
몫). 배선할 때 검사에 넘기는 URL 객체와 실제로 로드에 쓰는 URL 객체가
반드시 같아야 한다 — 이번 버그의 근본 원인이 "검사 시점 디코딩"과
"사용 시점 디코딩"의 불일치였다.

실제 근거: `Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift`,
`Tests/UnfoldTests/EditorNavigationPolicyTests.swift` (10개 테스트,
퍼센트 인코딩 우회 회귀 테스트 3개 포함).

### Task 9 — 에디터 창 (CharacterEditorWindowController + 브릿지)

계획 문서 Step 1~4의 코드 블록은 최초 버전이다. 코드 품질 검토에서 4가지
Important가 나와 전부 수정 후 재검토됐다 (`76f35d8`은 이미 수정본):

- **창 닫기 시 웹뷰 teardown** — 계획 원본에는 `NSWindowDelegate`가 없어서
  타이틀바 빨간 버튼으로 닫으면 `close()`가 안 불리고 숨겨진 `WKWebView`가
  계속 돌며 `unfold` 메시지 핸들러가 등록된 채 남았다. 실제 코드는
  `NSWindowDelegate` 채택 + `windowWillClose`에서 `teardown()` 호출.
  `teardown()`은 `guard let webView else { return }`로 idempotent —
  코드 경로·유저 경로·`deinit` 어디서 와도 두 번째 호출은 no-op.
- **`editingCharacterID` 게이트** — 계획 원본은 `handle()`이 웹뷰가 왕복시킨
  `payload.characterID`를 그대로 덮어쓰기 타깃으로 썼다. "새 캐릭터" 세션이
  임의의 기존 id를 보내 그 캐릭터를 덮어쓸 수 있었다. 실제 코드는 decode 직후
  `guard payload.characterID == editingCharacterID` (둘 다 nil=신규, 정확히
  일치=편집). 死 필드였던 `editingCharacterID`가 권위 있는 덮어쓰기 게이트가 됨.
- **`unfold-bridge.js` — `save()` try/catch** — 버튼을 먼저 비활성화한 뒤
  fallible 작업(`buildSheetDataURL`/`serialize`)을 하면 예외 시 버튼이 영구
  잠겼다. 실제 코드는 본문을 try/catch로 감싸 catch에서 버튼 복구 + 배너.
- **`saveFailed("")` 빈 배너** — 이름 프롬프트 취소가 빈 빨간 배너를 남겼다.
  실제 코드는 `saveFailed`에서 `if (!message) { return; }`.

그 외 반영된 Minor: Piskel 로고 링크(`.fake-piskelapp-header`) CSS로 숨김
(인프레임 리로드로 미저장 작업 유실 방지) / `loadDocument`의 `JSON.parse`를
try/catch로 감싸 blank 폴백 / `allowingReadAccessTo`를 `Editor/`→`Editor/piskel`로
좁힘 / `editorDirectory`를 `static let`으로 1회 해석 / 핸들러 이름 상수를
파일 스코프 `private let`으로 이동 (nonisolated `deinit`에서 참조하려고).

**웹뷰 실행 없이 검증 못 한 것** (Task 10 실물에서 확인됨): `Bundle.module`의
`Editor/` 디렉터리 해석은 OK (릴리스 번들에서 Piskel DOM 전체 렌더 확인).
브릿지 JS의 Piskel 전역 6종 표면과 `blankPiskelJSON` 형태는 여전히 정적 가정 —
사람 손 저장 검증이 최종 확인.

실제 근거: `Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift`
(371줄), `Sources/Unfold/Resources/Editor/unfold-bridge.{js,css}`,
`Sources/Unfold/Support/Strings.swift`의 `enum Editor`.

### Task 10 — Settings 배선

Step 1~4는 계획 문서 그대로 반영됐다. 리뷰에서 Critical/Important 0.
Minor 4개 중 1개만 수정:

- **Settings 창 높이** (`6cf4167`) — 계획은 `.frame(width: 460, height: 420)`을
  건드리지 않았는데, 이 높이는 **Task 10 이전부터** 이미 폼 콘텐츠(collapsed
  ~607pt, custom interval expanded ~628pt span)를 잘라내 스크롤바가 떴다.
  창은 `[.titled, .closable]`이라 사용자가 키울 수 없다. 실물 앱을 AX로 측정해
  worst case(custom interval 펼친 상태) 콘텐츠 높이를 재고 `height: 672`로
  키웠다 (창 전체 460×704). 이후 두 상태 모두 스크롤바 없음, "Create Character…"
  버튼이 프레임 안에 34pt 여유로 들어옴을 확인. 창은 여전히 non-resizable
  (계획 의도 유지). collapsed 상태에서 하단 ~78pt 여백은 non-resizable + 펼친
  폼도 담아야 하는 대가.
- 안 고친 Minor 3개 (계획 의도대로): `select(character)`가 `reloadCatalog()`
  뒤 `@Published`를 한 번 더 emit (edit 경로에서 중복이나 값은 동일, new 경로엔
  필수) / 삭제 실패 시 `NSLog`만, 사용자 alert 없음 / Create/Edit 두 번 눌러
  미저장 아트 있는 에디터가 프롬프트 없이 teardown되는 것 (Task 9 영역).

**엔타이틀먼트 확정: `com.apple.security.network.client` 불필요.** 샌드박스
릴리스 앱에서 `WKWebView`가 로컬 Piskel을 전부 렌더(AX로 `AXWebArea "Piskel"`,
`[64x64]` 캔버스, "Save to Spine Keepet" 버튼 확인), `blocked editor navigation`
/ nav-fail / 앱 레벨 sandbox·network 거부 로그 없음. WebContent 렌더러
프로세스의 표준 sandbox noise(`networkd.plist` 읽기 차단 등)는 `WKWebView`
쓰는 모든 샌드박스 앱에 나오고 로드를 막지 않는다. `Packaging/Unfold.entitlements`는
`com.apple.security.app-sandbox` 하나만 유지.

실제 근거: `Sources/Unfold/Settings/SettingsView.swift`,
`Sources/Unfold/Settings/SettingsWindowController.swift`,
`Sources/Unfold/App/AppDelegate.swift`, `Sources/Unfold/Support/Strings.swift`.

## 그 외 계획 대비 개선 사항 (Task 1·2·3·5·6 — 계획 문서도 이미 갱신됨)

이 항목들은 계획 문서 자체가 리뷰 결과를 반영해 이미 고쳐져 있으므로 계획
문서를 그대로 봐도 된다. 왜 이렇게 됐는지만 기록해 둔다.

- **Task 1 `CharacterLibrary.isSafeID`**: denylist(`""`, `"."`, `".."`, `/` 포함
  거부)에서 allowlist(ASCII 영숫자·`-`·`_`, 128바이트 상한)로 변경. denylist는
  임베디드 NUL 바이트(`"\u{0}"`)를 걸러내지 못했고, 이게 `packageDirectory(id:)`를
  라이브러리 루트 자체로 해석시켜 Task 5의 저장 경로에서 사용자 캐릭터
  전체를 삭제할 수 있었다.
- **Task 2**: 매니페스트에 알 수 없는 `renderStyle` 값이 있을 때 조용히
  `.smooth`로 떨어지던 것에 `NSLog` 로깅을 추가 — 필드 부재(정상)와 필드
  존재-but-알 수 없음(이상 신호)을 구분해야 디버깅이 가능하다.
- **Task 3**: 기존 테스트(`test_missingAnimationKey_returnsNil`)가 폴백 구현
  이후에도 초록불이었지만 **잘못된 이유**로 통과하고 있었다(키 없음이 아니라
  에셋 파일 없음 때문에 nil). GIF 픽스처 기반으로 교체해 실제 폴백 지점
  (`makeAnimator`)에서 검증하도록 고쳤다. `OverlayController`의 확장 지점
  주석에도 "폴백된 idle 클립은 루핑이라 `isFinished`가 안 선다"는 함정을
  기록해 뒀다.
- **Task 5 `CharacterPackageWriter`**: 기존 패키지 교체를
  `removeItem` 후 `moveItem`으로 하면 그 사이에 사용자의 이전 캐릭터가
  사라진 상태로 실패할 수 있는 창(window)이 생겼다.
  `FileManager.replaceItemAt(_:withItemAt:backupItemName:)`로 바꿔 한 번의
  파일시스템 연산으로 원자적 치환되게 했다. `validatePackage` 클로저를
  주입 가능하게 바꿔 테스트에서 자기검증 실패 경로를 시뮬레이션할 수
  있게 했다.
- **Task 6 `CharacterManager.reloadCatalog()`**: 계획 문서의 최초 코드
  블록은 "id가 사라졌을 때만" 현재 캐릭터를 재해석했는데, 바로 다음
  문단은 "같은 id라도 내용이 바뀌면(편집) 갱신해야 한다"고 요구하는
  내부 모순이 있었다. 실제 구현은 후자(올바른 쪽)를 따른다 — 계획
  문서도 이미 이 버전으로 갱신돼 있다.

## Task 11~12 — 남은 작업 요약

계획 문서(`2026-09-03-piskel-character-editor.md`)의 해당 섹션이 정확한
1차 자료다. 요약만 남긴다.

- **Task 11 — 썸네일** (계획 문서 Task 11): `CharacterThumbnailView` 전체를
  교체해 idle 프레임 0을 렌더, 실패 시 SF Symbol. `firstIdleFrame(of:)`는
  `static`(테스트 가능). `swift build && swift test` + 눈 확인.
- **Task 12 — 라이선스 고지 UI**: Settings에 "About" 섹션 + "View License"
  버튼으로 `LICENSE-piskel.txt`를 열게 함. `SettingsView.swift`에
  `import AppKit` 추가 필요. Task 7에서 이미 `NOTICE-piskel.txt`가
  9개 서드파티 라이브러리까지 포함해 완성돼 있으므로, 이 태스크는
  Settings UI만 추가하면 된다. **주의**: `6cf4167`이 창 높이를 672로
  키웠으므로 About 섹션 추가 후 다시 잘리는지 확인할 것.

## 알려진 리스크 / 확인 필요 항목

- **엔타이틀먼트: 해소됨.** Task 10 실물 검증에서 `com.apple.security.network.client`
  불필요로 확정 (위 "Task 10" 절 참고). `Packaging/Unfold.entitlements`는
  `com.apple.security.app-sandbox` 하나만 유지.
- **저장/삭제 왕복 사람 검증 대기**: Task 10의 배선 경로·엔타이틀먼트는 AX로
  확인됐지만, 실제 "그리기 → 저장 → 이름 → 목록/펫 반영 → 픽셀 선명 → 편집으로
  재오픈 → 저장 → 삭제" 왕복은 이 환경에서 자동화 불가(`WKWebView` 콘텐츠에
  클릭 주입 안 됨). 사람이 샌드박스 앱에서 한 번 돌려봐야 한다:
  `swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"`
  → 메뉴바 → Settings… → Create Character…. 계획 문서 Task 10 Step 6의
  체크리스트 6항목 확인.
- **Settings 창 collapsed 시 하단 여백 ~78pt**: `6cf4167`이 높이를 672로 키운
  결과. non-resizable 창이 펼친 폼도 담아야 해서 생긴 것. 눈으로 보고 거슬리면
  resizable로 바꾸는 게 대안(계획 의도에서 이탈).
- **Piskel 벤더링 커밋**: `a6b9c02daefceb10093f71e92d52d16920ccb16e`
  (2026-09-04 벤더링). Piskel을 갱신하려면
  `Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt`의 절차를 그대로
  따르면 된다 — 재적용할 패치는 없음.
- **워크플로 규율**: Critical/Important 발견 시 절대 다음 태스크로
  넘어가지 말고 수정 → 재검토까지 마칠 것. 지금까지 이 절차에서 실제
  버그가 여러 번 나왔다(위 "갈라진 지점" 참고) — 검토자에게 "보고를
  믿지 말고 직접 재현하라"고 매번 명시적으로 지시하는 것이 핵심이었다.
