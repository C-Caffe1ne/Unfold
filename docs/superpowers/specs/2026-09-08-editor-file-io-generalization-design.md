# 캐릭터 에디터 파일 입출력 일반화 설계

작성일: 2026-09-08
상태: 설계 승인됨, 구현 계획 대기
브랜치: `major-MacOS` (Swift 전용 — `src/` 의 C# 빌드는 이 브랜치의 대상이 아니다)

## 배경

캐릭터 에디터는 웹뷰 기반 Piskel에서 네이티브 SwiftUI 구현으로 이미 옮겨졌다
(커밋 `ce5e024`). 그러나 파일 입출력 표면에는 Piskel 시절의 흔적이 그대로 남아
있다.

- File 메뉴가 `Open Piskel or PNG…` / `Export Piskel…` 로 특정 외부 앱을 지목한다
- 열 수 있는 확장자가 `.piskel` 과 `.png` 둘뿐이다
- 저장이 파일 저장이 아니라 라이브러리 등록이며, 문서에 파일 경로 개념이 없다

사용자 요청은 두 갈래다. 첫째, 범용 이미지 파일을 열어 편집할 수 있게 할 것.
둘째, 내보내기에 GIF를 포함하고 UI 문구를 열기/저장/다른 이름으로 저장이라는
일반적인 문서 조작으로 바꿀 것.

### 조사에서 드러난 사실

구현 전 실측으로 확인한 내용이다. 설계의 여러 결정이 여기에 근거한다.

**웹뷰 시절 잔해가 남아 있다.** 코드베이스 어디에도 WebKit 참조가 없는데
`EditorNavigationPolicy.swift` 는 테스트에서만 참조되는 데드 코드로 남아 있고,
`Sources/Unfold/Resources/Editor/` 아래 2.5MB(Piskel 웹런타임 + `unfold-bridge.js/css`)
는 `Package.swift` 에서 `exclude` 되고 `.xcodeproj` 에도 없어 어느 빌드에도
포함되지 않는다. `Strings.Editor` 의 6개 문자열 중 5개가 사용처 0이다.

**디코더는 확장자가 아니라 내용으로 동작한다.** `.json` 으로 저장한 piskel 문서도
그대로 열린다. 확장자는 파일 패널 필터일 뿐이다. 따라서 파일명·확장자 정책을
바꿔도 디코더는 손대지 않아도 된다.

**내보낸 스프라이트시트를 다시 열 수 없다.** `Export PNG Sprite Sheet…` 는
`width × frameCount` 가로 시트를 쓰는데, 임포트는 한 변 128px 상한이라 64×64
8프레임(=512×64)이 거부된다. 프로브 출력:

```
PROBE exported sheet bytes=746, frames=8
PROBE reimport REJECTED -> invalid("Use a complete PNG with the expected
                            dimensions (imports: 1–128 pixels per side).")
```

**애니메이션 GIF 인코딩은 ImageIO만으로 된다.** 의존성이 필요 없다. 다만 GIF는
1비트 투명도만 지원해 반투명 픽셀이 불투명으로 반올림된다.

```
PROBE gif finalize=true bytes=144
PROBE readback type=com.compuserve.gif frameCount=3
PROBE frame0 delay=Optional(0.08)          # 1/12 정확히 보존
PROBE alpha in=255,0,128 -> out alpha = 255, 0, 255
```

**undo가 큰 문서에서 이미 무너져 있다.** 현재 최대 문서(128×128 × 24프레임 ×
16레이어 = 24MB)에서 다섯 번 편집해도 되돌리기는 한 단계만 남는다.
`historyBudget` 이 32MB인데 스냅샷 두 개면 48MB라 `trim` 이 즉시 깎아낸다.

```
PROBE byteCount = 24 MB  (budget 32 MB)
PROBE 5 edits on a max-size document -> undo depth = 1
PROBE 5 edits on the default document (64x64, 16 KB) -> undo depth = 5
```

**배율 표시가 실제와 어긋난다.** `zoom` 은 픽셀당 화면 배율이고 UI는 `zoom * 100`%
로 표시한다. 64×64 문서는 zoom 8로 열려 화면에 `800%` 로 뜬다.

기준선: 기존 테스트 289개 전부 통과.

## 범위

D → A → C 순서로 진행하며 각 단계 완료 후 보고한다.

| 단계 | 내용 |
| --- | --- |
| **D** | 포맷 레지스트리 + 문서 출처 모델, 메뉴 재구성, Piskel 명칭 정리, 데드 코드 삭제 |
| **A** | 범용 래스터 열기(PNG/JPEG), 가져오기 대화상자, 캔버스 상한 확대, undo 수정 |
| **C** | 애니메이션 GIF 내보내기 |

D를 먼저 하는 이유는 `EditorFileFormat` 이 나머지의 접합면이기 때문이다. A와 C는
포맷을 레지스트리에 등록하는 형태로 붙는다. D를 마지막에 두면 메뉴를 세 번
고쳐야 한다.

### 이번 범위 밖

- **Aseprite(`.ase`/`.aseprite`) 지원** — 별도 사이즈의 작업이라 보류한다. 바이너리
  파서(헤더/프레임/청크, ZLIB 셀 데이터, 인덱스·그레이스케일 색상 모드, 블렌드
  모드, 태그)가 필요하고, 이 앱의 문서 모델이 표현하지 못하는 기능이 많아 변환
  정책을 따로 설계해야 한다. `EditorFileFormat` 에 케이스를 더하는 형태로 나중에
  붙는다.
- **GIF 열기** — 후순위. 내보내기만 이번에 한다.
- **JPEG 내보내기** — 알파 채널이 없어 투명 배경이 검게 채워지고 손실 압축이 픽셀
  경계를 뭉갠다. 읽기만 지원한다.
- **라이브러리 저장 경로** — 리비전 검사, 원자적 스테이징, 자체 검증은 이 작업의
  위험 구간이라 격리한다. 호출 지점만 바뀌고 로직은 손대지 않는다.

## 결정 사항

### 1. 문서 출처 통합 모델

저장의 의미를 정의하기 위해 문서가 자기 출처를 들고 있게 한다.

```swift
enum EditorDocumentOrigin: Equatable {
    case unsaved  // 새 문서
    case file(URL, EditorFileFormat)                  // 열기로 온 문서
    case character(id: String, EditorPackageRevision) // 라이브러리에서 온 문서
}
```

- **Save (⌘S)** — 출처에 되쓴다. `.file` 이면 그 경로에 원자적 덮어쓰기,
  `.character` 면 기존 라이브러리 저장 경로, `.unsaved` 이면 Save As로 폴백한다.
  출처 포맷이 쓰기 불가(JPEG)여도 Save As로 폴백한다.
- **Save As (⇧⌘S)** — 경로와 포맷을 고르고 출처를 `.file` 로 갱신한다.
- **Save to Spine Keepet** — 라이브러리 패키지를 쓴다. 기존 로직 그대로.

이 모델을 고른 이유는 하나의 ⌘S가 파일 맥락과 라이브러리 맥락 양쪽에서 맞게
동작하면서, 기존 저장 경로의 안전장치를 그대로 보존하기 때문이다. 문서를 파일
중심으로 단순화하는 대안은 라이브러리에서 연 캐릭터의 ⌘S가 애매해진다.

### 2. 포맷 레지스트리

```swift
enum EditorFileFormat: CaseIterable {
    case unfoldSource   // .unf — 레이어·프레임 전부 보존
    case png            // 가로 스프라이트시트
    case gif            // 애니메이션 (쓰기 전용, 이번 범위)
    case jpeg           // 읽기 전용
}
```

각 케이스가 `utType`, `fileExtension`, `displayName`, `canRead`, `canWrite` 를
제공한다. 메뉴와 파일 패널이 이 테이블을 보고 그려진다.

### 3. 메뉴 구조

```
File ▸  New
        Open…                ⌘O    .unf .png .jpg .jpeg
        ──────────────
        Save                 ⌘S
        Save As…             ⇧⌘S   .unf / .png / .gif
        ──────────────
        Save to Spine Keepet
```

UI 문구는 영어를 유지한다. `Strings.Editor` 가 전부 영어다.

### 4. 크기 방어 — 두 축

| 축 | 값 | 근거 |
| --- | --- | --- |
| 한 변 | `8...512` (기본 64×64) | 사용자 지정 |
| 문서 총량 | 24 MB | undo 붕괴 방지, 기존 최대치와 정확히 일치 |

`editorCanvasSideRange` 를 `1...128` 에서 `8...512` 로 바꾼다. 기본값
`editorDefaultCanvasSide = 64` 는 이미 요구와 일치하므로 그대로 둔다.

한 변만 방어해서는 부족하다. `byteCount = 변 × 변 × 프레임 × 레이어 × 4` 이므로
512×512는 8프레임 2레이어만 돼도 16.8MB이고, 24프레임 16레이어면 384MB다.
따라서 **총량 상한**을 함께 둔다.

처음엔 64MB로 뒀다 — 384MB를 막으면서 기존 최대치(24MB)를 넉넉히 포함하는
값이라 합리적으로 보였다. 하지만 구현 중 512px·24프레임 문서를 노이즈(비압축)
픽셀로 채워 시트/소스 크기를 실측하니, `sheetPNG` 가 약 20.8MB,
`encode()` 결과가 약 55.5MB로 나와 각각 그때의 `editorMaxSheetDataURLBytes`
(8MB)와 `PixelDocumentCodec.maximumSourceBytes`(48MB)를 넘었다. 세 상수가
서로 안 맞았던 것이다.

512가 존재하는 이유는 큰 이미지를 잘라 쓰기 위해서지, 512×512 캔버스를
24프레임·16레이어로 애니메이션하라는 뜻이 아니다 — 캐릭터는 결국
`characterDisplaySize`(192pt)로 그려진다. 그래서 총량 상한을 64MB가 아니라
**24MB로 내려** 곱을 제약하고, 시트 상한을 8MB에서 **32MB로 올렸다**(레이어를
합성한 시트의 원시 크기는 최악의 경우 문서 총량 상한과 같고, 비압축 아트는 PNG로도
그 크기 그대로 나오므로 24MB에 약 30% 여유를 더했다). 소스 상한 48MB는 그대로
둔다 — 레이어당 PNG 한 장을 base64로 담는 소스는 24MB 픽셀이 4/3배 부풀어도
약 32MB이므로 여유가 있다.

24MB는 우연이 아니다. 기존 최대치 128×128×24×16×4 = 25,165,824 바이트 =
24×1024×1024, 정확히 24MiB다. `exceedsByteCeiling` 이 `>` 를 쓰고 `>=` 를
쓰지 않으므로, 이 경계값에 정확히 걸친 문서도 여전히 합법이다 — 오늘 합법인
문서를 하나도 배제하지 않는다.

512×512를 쓰면 프레임·레이어 수가 자연히 제한된다: 24프레임 1레이어, 12프레임
2레이어, 6프레임 4레이어, 3프레임 8레이어 중 하나다.

최소 8px 하한 때문에 1~7px 문서는 거부된다. 실제 존재 가능성은 낮지만 기존
라이브러리에 그런 캐릭터가 있다면 열리지 않는다.

### 5. 열기 흐름

파일 선택 → 내용으로 포맷 판별 → 크기·비율 검사 → 분기한다.

- 한 변 ≤512이고 시트로 의심되지 않으면 바로 연다
- **폭이 높이의 2배 이상 정수배**면 스프라이트시트로 의심한다. 배수가 1인 정사각
  이미지는 해당하지 않는다 — 그러면 모든 정사각 이미지가 대화상자를 띄운다
- 한 변이 512를 넘거나 시트로 의심되면 **가져오기 대화상자**를 띄운다

가져오기 대화상자는 미리보기와 함께 네 가지를 제공한다: 프레임 분할(자동 추정값
제시), 크롭, 한 장으로 축소, 취소. 512 초과 이미지는 경고와 함께 크롭이 기본
선택이 된다.

프레임 분할은 앞서 확인한 시트 재임포트 불가 문제를 함께 해결한다.

### 6. 배율

`baseScale`(문서를 연 배율)을 100%로 표기하고 거기서 배수로 움직인다. 표기 범위는
12.5% ~ 800%다.

표기 배율과 별개로 실제 픽셀 배율은 `1...24` 로 고정 클램프한다. 두 제약이
동시에 걸린다. 예를 들어 `baseScale` 이 8이면 800%는 픽셀 배율 64가 되어야 하지만
24에서 잘린다. 이 경우 도달 불가능한 표기 단계는 UI에서 비활성화한다.

현재 공식 `max(2, min(12, 512 / max(width, height)))` 는 128 이하를 전제하므로
512에서 깨진다(512 문서가 zoom 2로 열려 1024pt가 된다). 뷰포트에 맞추는 계산으로
교체한다.

1:1 픽셀을 100%로 삼는 대안은 기각했다. 64×64가 화면에 64pt로 보여 픽셀아트
편집이 불가능하다.

### 7. undo 수정

`trim` 이 최소 깊이를 보장하도록 고친다.

```swift
private let minimumHistoryDepth = 16
private let historyBudget = 128 * 1024 * 1024
```

```swift
while stack.count > minimumHistoryDepth && (bytes > historyBudget || stack.count > 100) {
    bytes -= stack.removeFirst().byteCount
}
```

`byteCount` 는 논리적 크기라 실제 메모리를 크게 과대평가한다. Swift 배열의
copy-on-write 때문에 스냅샷들이 변경되지 않은 프레임 버퍼를 공유하기 때문이다.
붓질 한 번은 프레임 하나만 분기시키므로 512px 문서에서도 실제 증가분은 1MB
수준이다.

과대평가가 문제가 되는 경우는 캔버스 크기 변경이나 레이어 추가처럼 전체를
건드리는 편집이다. 최악의 경우는 최대 크기 문서에서 그런 구조적 편집을 16번
연속하는 것인데, 문서 총량이 24MB로 묶여 있으므로 상한이 존재한다. 실사용
패턴이 아니다. 문제가 되면 전체 스냅샷 대신 변경 영역 기반 히스토리로 옮긴다.

### 8. GIF 저장 경고

실측에서 확인한 대로 GIF는 반투명을 보존하지 못한다(alpha 128 → 255). 문서에
`0 < alpha < 255` 픽셀이 하나라도 있으면 저장 전 한 번 경고한다. 완전 투명
픽셀은 보존되므로 경고 대상이 아니다.

프레임 지연은 문서 fps에서 `1/fps` 로 산출한다. 실측에서 왕복 보존을 확인했다.

### 9. 명칭 정리

| 현재 | 변경 |
| --- | --- |
| `"Open Piskel or PNG…"` / `"Export Piskel…"` | `"Open…"` / `"Save As…"` |
| `EditorSavePayload.piskelJSON` | `sourceJSON` |
| `export(piskel: Bool)` | 포맷 enum 기반 |
| Piskel을 지목하는 주석·오류 메시지 | 포맷 중립 표현 |

유지하는 것:

- **`source.piskel` → `source.unf`** — 에디터의 문서 확장자가 자체 `.unf`
  로 바뀌면서 패키지 내부 소스 파일명도 함께 바뀐다. 기존 사용자 라이브러리
  호환을 위해 읽기는 옛 `source.piskel` 로 폴백하되, 쓰기는 항상 새 이름만
  쓴다. 디코더가 확장자를 보지 않으므로 파서 수정은 필요 없다.
- **JSON `piskel` 키와 `modelVersion: 2`** — 실제 Piskel 파일 포맷의 와이어
  형식이다. 바꾸면 상호운용이 깨진다. Swift 쪽 식별자만 `CodingKeys` 로 매핑한다.

### 10. 삭제 대상

- `Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift` 와
  `Tests/UnfoldTests/EditorNavigationPolicyTests.swift` — WebKit이 없으므로 데드
  코드
- `Sources/Unfold/Resources/Editor/` 전체 2.5MB — 어느 빌드에도 포함되지 않음
- `Strings.Editor` 의 미사용 5개: `windowTitle`, `unavailableTitle`,
  `unavailableMessage`, `unavailableDismiss`, `namePromptPlaceholder`

## 컴포넌트

신규:

| 파일 | 역할 |
| --- | --- |
| `EditorFileFormat.swift` | 포맷 레지스트리 |
| `EditorDocumentOrigin.swift` | 문서 출처 |
| `RasterImageDecoder.swift` | `CGImageSource` 기반 범용 디코더(PNG/JPEG) |
| `AnimatedGIFEncoder.swift` | ImageIO GIF 라이터 |
| `ImportOptions.swift` | 분할·크롭·축소 결정 모델 |
| `ImportOptionsView.swift` | 가져오기 대화상자 |

수정:

| 파일 | 변경 |
| --- | --- |
| `CharacterEditorWindowController.swift` | 출처 도입, 열기/저장/다른 이름으로 저장 분기 |
| `PixelEditorView.swift` | File 메뉴 재구성, 배율 표시 |
| `PixelEditorModel.swift` | `baseScale`, undo `trim` 수정 |
| `PixelDocumentCodec.swift` | PNG 전용 검사 분리, 명칭 정리 |
| `EditorSavePayload.swift` | `piskelJSON` → `sourceJSON` |
| `PixelDocument.swift` | 총량 상한 검증 |
| `Constants.swift` | 캔버스 범위, 총량 상한 |
| `Strings.swift` | 데드 문자열 제거 |

`PixelDocumentCodec` 에서 래스터 디코딩을 `RasterImageDecoder` 로 분리하는 이유는
현재 `decodePNG` 가 PNG 시그니처와 IEND 청크 검사를 하드코딩하고 있어 JPEG를
받을 수 없기 때문이다. 분리하면 포맷별 무결성 검사를 각자 두면서 픽셀 추출
경로는 공유할 수 있다.

## 에러 처리

- 포맷 판별 실패, 크기 초과, 잘린 파일은 기존 `PixelDocumentCodec.Failure.invalid`
  로 사용자에게 이유를 보여준다
- 쓰기는 원자적으로 한다(`Data.write(options: .atomic)`). 실패해도 기존 파일이
  남는다
- 손실이 발생하는 저장(GIF 반투명, PNG 다중 프레임 합치기)은 진행 전 경고한다
- 라이브러리 저장의 리비전 불일치 처리는 현행 유지

## 테스트 계획

새로 추가:

- 포맷 판별 — 확장자와 내용이 어긋날 때 내용이 이긴다
- 래스터 디코드 — PNG/JPEG 각각, 잘린 파일 거부
- 시트 분할 — 512×64 입력이 64×64 8프레임이 된다 (**오늘 실패하는 케이스**)
- 크롭 — 512 초과 이미지가 지정 영역으로 잘린다
- GIF 왕복 — N프레임 쓰기 후 읽기, 프레임 수와 지연 일치
- 알파 경고 — 반투명 픽셀 유무에 따른 트리거
- 문서 총량 상한 — 24MB 초과 조합 거부; `decode()` 도 곱셈으로 조기 거부한다(레이어 픽셀을 할당하기 전에)
- undo 깊이 — 최대 크기 문서에서 16단계 보장 (**오늘 1단계인 케이스**)
- 출처별 ⌘S 동작 3종

기존 289개 테스트는 전부 통과를 유지한다. 특히 `EditorSavePayloadTests`,
`CharacterPackageWriterTests`, `PixelDocumentCodecTests` 는 캔버스 범위 변경의
영향을 받으므로 주의한다.

## 파급 검토 항목

상한을 512로 올리면 다음이 함께 영향을 받는다. 구현 중 확인한다.

- **스프라이트시트 최대 폭** 3,072px → 12,288px
- **`editorMaxSheetDataURLBytes`** 8MB → **32MB로 올림.** 12,288×512 PNG가
  8MB를 넘을 수 있다는 우려가 실측(20.8MB, 노이즈 픽셀 기준)으로 확인됐다.
  시트는 레이어를 합성한 단일 이미지이므로 원시 크기는 최악의 경우
  `editorMaxDocumentBytes`(24MB)와 같고, 비압축 아트는 PNG로도 그 크기
  그대로 나온다. 그래서 24MB에 약 30% 여유를 더해 32MB로 잡았다 — 문서 총량
  상한에서 유도된 값이지 독립적인 매직 넘버가 아니다
- **`characterDisplaySize`** 192 — 512px 스프라이트의 데스크톱펫 표시
- **`PixelDocumentCodec.maximumSourceBytes`** 48MB, 그대로 둔다 — 소스는
  레이어당 PNG 한 장을 base64로 담으므로 24MB 픽셀이 4/3배 부풀어 약 32MB,
  JSON 오버헤드를 더해도 48MB 안에 여유가 있다 (실측: 약 29.3MB)

## 하위 호환

- 기존 캐릭터 패키지는 그대로 열린다. 캔버스 범위가 넓어지는 방향이라 기존
  128px 이하 문서가 배제되지 않는다
- 패키지 내부 소스 파일명은 `source.unf` 로 바뀌지만 읽기가 옛 `source.piskel`
  로 폴백하고 JSON 스키마도 그대로이므로 라이브러리 마이그레이션이 필요 없다
- 예외: 한 변이 8px 미만인 문서. 실제 존재 가능성은 낮다
