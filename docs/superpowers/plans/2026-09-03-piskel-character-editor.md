# Piskel 내장 캐릭터 에디터 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 사용자가 Unfold 안에서 픽셀 캐릭터를 직접 그려 저장하고, 그 캐릭터를 데스크톱 펫과 스트레칭 알림에 쓸 수 있게 한다.

**Architecture:** Piskel(Apache-2.0)의 빌드 산출물을 수정 없이 앱 리소스로 벤더링하고 `WKWebView`에 로컬 파일로 띄운다. 커스터마이징은 전부 `WKUserScript`로 주입하는 자체 JS/CSS에만 담아 업스트림 대비 diff를 0으로 유지한다. 저장 결과는 기존 `character.json` 패키지 포맷으로 떨어지므로, 이미 존재하는 `CharacterPackageLoader` → `ImportedCharacterRepository` → `CharacterManager` 경로를 그대로 탄다.

**Tech Stack:** Swift 5.9 / SwiftUI + AppKit / WebKit(`WKWebView`) / ImageIO / XCTest / SwiftPM 리소스 번들

**Spec:** `docs/superpowers/specs/2026-09-03-piskel-character-editor-design.md`

---

## 이 계획을 실행하기 전에 알아야 할 것

**빌드와 테스트**

```bash
swift build                                   # 컴파일
swift test                                    # 전체 테스트
swift test --filter CharacterLibraryTests     # 한 클래스만
swift build -c release && ./Scripts/make-app-bundle.sh release
open "build/Spine Keepet.app"                 # 샌드박스 서명된 실제 앱
```

`Scripts/make-app-bundle.sh`는 SwiftPM 리소스 번들(`Unfold_Unfold.bundle`)을 `.app`의 **최상위**로 복사하고 `Packaging/Unfold.entitlements`로 애드혹 서명한다. 즉 샌드박스 동작은 `swift run`이 아니라 이 번들로만 검증된다.

**사용자에게 보이는 문구는 전부 영어다.** `Sources/Unfold/Support/Strings.swift` 한 곳에 모여 있고 앱 이름은 "Spine Keepet"이다. 새 문구도 반드시 여기에 넣는다.

**테스트 스타일** — 기존 테스트는 `XCTest`이고, 임시 디렉터리에 합성 패키지를 만들어 실제 파일 경로를 태운다(`Tests/UnfoldTests/CharacterPackageLoaderTests.swift` 참고). SwiftUI 뷰 수명주기는 우회하고 `static` 팩토리를 직접 부른다(`CharacterAnimationView.makeAnimator`). `Tests/UnfoldTests/Support/`에 테스트 전용 헬퍼를 둔다.

**스펙과의 순서 차이** — 스펙의 "작업 순서"는 파이프라인을 1단계로 두었다. 이 계획은 그 안에서 `RenderStyle`과 키 폴백을 **앞으로 당긴다**. `CharacterPackageWriter`(Task 5)가 `renderStyle` 필드를 써야 하므로, 그 필드가 먼저 존재해야 하기 때문이다.

## File Structure

**신규 — `Sources/Unfold/CharacterEditor/`** (에디터 기능이 통째로 여기 모인다)

| 파일 | 책임 |
|---|---|
| `CharacterLibrary.swift` | 사용자 캐릭터 디렉터리의 위치·목록·삭제. 경로를 아는 유일한 타입 |
| `EditorSavePayload.swift` | 브릿지가 보낸 JSON → 검증된 값 객체. 순수 타입, 파일 접근 없음 |
| `CharacterPackageWriter.swift` | 페이로드 → 디스크 위 패키지. 스테이징 + 자기검증 + 원자적 이동 |
| `EditorNavigationPolicy.swift` | "에디터 디렉터리 안의 `file://`만 허용" 규칙. 순수 함수라 테스트된다 |
| `CharacterEditorWindowController.swift` | 창 수명, `WKWebView` 구성, 스크립트 주입, 메시지 수신, 이름 입력 |

**신규 — `Sources/Unfold/Resources/Editor/`**

```
Editor/
  piskel/            벤더링된 Piskel 빌드 산출물 (무수정)
  unfold-bridge.js   저장 버튼 · 시트 합성 · 직렬화 · 초기 로드
  unfold-bridge.css  익스포트 패널 숨김 + 저장 버튼 스타일
  LICENSE-piskel.txt Apache-2.0 전문
  NOTICE-piskel.txt  귀속 고지
  PISKEL-VERSION.txt 벤더링한 커밋 SHA와 날짜
```

**수정**

| 파일 | 변경 |
|---|---|
| `Package.swift` | `.copy("Resources/Editor")` 리소스 추가 |
| `Sources/Unfold/Character/Character.swift` | `RenderStyle` 타입, `renderStyle` 프로퍼티, `resolvedAnimation(for:)` |
| `Sources/Unfold/Character/CharacterManifest.swift` | `renderStyle: String?` |
| `Sources/Unfold/Character/CharacterPackageLoader.swift` | 매니페스트의 `renderStyle`을 모델로 전달 |
| `Sources/Unfold/Character/CharacterAnimationView.swift` | `resolvedAnimation` 사용 + 보간 방식 결정 |
| `Sources/Unfold/Character/CharacterRepository.swift` | `ImportedCharacterRepository` 실제 구현 |
| `Sources/Unfold/Character/CharacterManager.swift` | `@Published availableCharacters` + `reloadCatalog()` |
| `Sources/Unfold/Character/CharacterThumbnailView.swift` | idle 프레임 0 렌더, 실패 시 SF Symbol |
| `Sources/Unfold/DesktopPet/DesktopPetAnimationController.swift` | `renderStyle` 노출 |
| `Sources/Unfold/DesktopPet/DesktopPetView.swift` | 보간 방식 전달 |
| `Sources/Unfold/Settings/SettingsView.swift` | Create / Edit / Delete 액션 |
| `Sources/Unfold/Settings/SettingsWindowController.swift` | 에디터 창 컨트롤러 주입 |
| `Sources/Unfold/App/AppDelegate.swift` | 라이브러리·에디터 배선 |
| `Sources/Unfold/Support/Constants.swift` | 에디터 한계값과 디렉터리 이름 |
| `Sources/Unfold/Support/Strings.swift` | 새 UI 문구 |

**테스트 신규**

`CharacterLibraryTests` · `EditorSavePayloadTests` · `CharacterPackageWriterTests` · `ImportedCharacterRepositoryTests` · `CharacterAnimationFallbackTests` · `EditorNavigationPolicyTests` · `Support/TestPNG.swift`

---

### Task 1: CharacterLibrary — 사용자 캐릭터가 사는 곳

**Files:**
- Create: `Sources/Unfold/CharacterEditor/CharacterLibrary.swift`
- Modify: `Sources/Unfold/Support/Constants.swift`
- Test: `Tests/UnfoldTests/CharacterLibraryTests.swift`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/CharacterLibraryTests.swift`:

```swift
import XCTest
@testable import Unfold

/// `CharacterLibrary` against a temp root — never the real Application
/// Support directory, so tests can't disturb (or depend on) a developer's
/// own saved characters.
final class CharacterLibraryTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-library-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func makePackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try Data("{}".utf8).write(to: dir.appendingPathComponent("character.json"))
    }

    func test_packageDirectories_missingRoot_isEmptyNotAnError() {
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_packageDirectories_listsOnlyDirectories_sortedByName() throws {
        try makePackage(id: "user-b")
        try makePackage(id: "user-a")
        try Data("stray".utf8).write(to: root.appendingPathComponent("loose-file.txt"))

        XCTAssertEqual(library.packageDirectories().map(\.lastPathComponent), ["user-a", "user-b"])
    }

    func test_packageDirectory_rejectsUnsafeIDs() {
        XCTAssertNil(library.packageDirectory(id: ""))
        XCTAssertNil(library.packageDirectory(id: ".."))
        XCTAssertNil(library.packageDirectory(id: "."))
        XCTAssertNil(library.packageDirectory(id: "a/b"))
        XCTAssertNil(library.packageDirectory(id: "/absolute"))
    }

    func test_packageDirectory_acceptsNormalID() {
        let url = library.packageDirectory(id: "user-123")
        XCTAssertEqual(url?.lastPathComponent, "user-123")
    }

    func test_delete_removesPackage() throws {
        try makePackage(id: "user-a")
        try library.delete(id: "user-a")
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_delete_unsafeID_throwsAndRemovesNothing() throws {
        try makePackage(id: "user-a")
        XCTAssertThrowsError(try library.delete(id: ".."))
        XCTAssertEqual(library.packageDirectories().count, 1)
    }

    func test_makeDefault_pointsInsideApplicationSupport() {
        let url = CharacterLibrary.makeDefault().rootDirectory
        XCTAssertEqual(url.lastPathComponent, "Characters")
        XCTAssertEqual(url.deletingLastPathComponent().lastPathComponent, "Unfold")
        XCTAssertTrue(url.path.contains("Application Support"))
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter CharacterLibraryTests`
Expected: 컴파일 실패 — `cannot find 'CharacterLibrary' in scope`

- [ ] **Step 3: Constants에 디렉터리 이름을 추가한다**

`Sources/Unfold/Support/Constants.swift`의 `builtInCharactersResourceSubdirectory` 바로 아래에 추가:

```swift
    /// Application Support 아래에서 이 앱이 쓰는 폴더 이름. 샌드박스에서는
    /// 앱 컨테이너 안으로 매핑되므로 파일 접근 엔타이틀먼트가 필요 없다.
    static let applicationSupportFolderName = "Unfold"

    /// 사용자가 직접 만든 캐릭터 패키지가 한 디렉터리씩 들어가는 폴더.
    /// 번들 안의 `builtInCharactersResourceSubdirectory`와 같은 레이아웃
    /// (`<id>/character.json` + 에셋)을 쓰므로 같은 로더가 양쪽을 읽는다.
    static let userCharactersFolderName = "Characters"
```

- [ ] **Step 4: CharacterLibrary를 구현한다**

`Sources/Unfold/CharacterEditor/CharacterLibrary.swift`:

```swift
import Foundation

/// Where user-created character packages live, and the only type that knows
/// that answer. Everything else — the repository, the writer, the editor
/// window — asks this instead of building paths of its own.
///
/// Every id that becomes a path segment goes through `isSafeID` first, so a
/// value that arrived from the web side can never escape the library root.
/// This mirrors the check `CharacterAssetLoader` already applies to file
/// names inside a package.
struct CharacterLibrary {

    enum LibraryError: Error, CustomStringConvertible {
        case invalidID(String)

        var description: String {
            switch self {
            case .invalidID(let id):
                return "\"\(id)\" is not a usable character id"
            }
        }
    }

    let rootDirectory: URL

    init(rootDirectory: URL) {
        self.rootDirectory = rootDirectory
    }

    /// Production location: `Application Support/Unfold/Characters` inside
    /// the app's own sandbox container.
    static func makeDefault() -> CharacterLibrary {
        guard let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first else {
            preconditionFailure("macOS always reports an Application Support directory for the user domain")
        }
        return CharacterLibrary(
            rootDirectory: base
                .appendingPathComponent(Constants.applicationSupportFolderName, isDirectory: true)
                .appendingPathComponent(Constants.userCharactersFolderName, isDirectory: true)
        )
    }

    /// Every package directory, sorted by name so the character list has a
    /// stable order between launches. A missing root is not an error — it
    /// simply means the user hasn't made a character yet.
    func packageDirectories() -> [URL] {
        let entries: [URL]
        do {
            entries = try FileManager.default.contentsOfDirectory(
                at: rootDirectory,
                includingPropertiesForKeys: [.isDirectoryKey],
                options: [.skipsHiddenFiles]
            )
        } catch CocoaError.fileReadNoSuchFile {
            // The user hasn't saved a character yet. Not a problem.
            return []
        } catch {
            NSLog("Unfold: could not read the character library at \(rootDirectory.path) — \(error)")
            return []
        }

        return entries
            .filter { (try? $0.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true }
            .sorted { $0.lastPathComponent < $1.lastPathComponent }
    }

    /// `nil` when `id` couldn't safely become a path segment.
    func packageDirectory(id: String) -> URL? {
        guard Self.isSafeID(id) else { return nil }
        return rootDirectory.appendingPathComponent(id, isDirectory: true)
    }

    func createRootIfNeeded() throws {
        try FileManager.default.createDirectory(at: rootDirectory, withIntermediateDirectories: true)
    }

    func delete(id: String) throws {
        guard let directory = packageDirectory(id: id) else {
            throw LibraryError.invalidID(id)
        }
        guard FileManager.default.fileExists(atPath: directory.path) else { return }
        try FileManager.default.removeItem(at: directory)
    }

    /// One path segment: ASCII letters, digits, `-`, and `_` only.
    ///
    /// An allowlist rather than a denylist on purpose. Ids reach this from
    /// the editor web view, and excluding known-bad strings kept letting
    /// things through — an embedded NUL, for one, which Foundation
    /// truncates a path component at, collapsing `appendingPathComponent`
    /// back onto the library root itself.
    static func isSafeID(_ id: String) -> Bool {
        guard !id.isEmpty, id.utf8.count <= 128 else { return false }
        return id.allSatisfy { $0.isASCII && ($0.isLetter || $0.isNumber || $0 == "-" || $0 == "_") }
    }
}
```

> 이 검증기는 라이브러리 전체를 지키는 경계다. `""` / `"."` / `".."` / `"a/b"`
> 같은 뻔한 입력뿐 아니라 **임베디드 NUL(`"\u{0}"`)** 도 반드시 막아야 한다 —
> Foundation은 경로 컴포넌트를 NUL에서 잘라내므로, 통과시키면
> `packageDirectory(id:)`가 하위 디렉터리가 아니라 **루트 자체**를 돌려주고
> Task 5의 저장 경로가 사용자의 캐릭터를 전부 지운다. 공백만 있는 id,
> 128바이트 초과, 백슬래시·제어문자에 대한 회귀 테스트도 함께 둔다.

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

Run: `swift test --filter CharacterLibraryTests`
Expected: 명시된 테스트 + 아래 회귀 테스트가 전부 PASS

- [ ] **Step 6: 커밋한다**

```bash
git add Sources/Unfold/CharacterEditor/CharacterLibrary.swift \
        Sources/Unfold/Support/Constants.swift \
        Tests/UnfoldTests/CharacterLibraryTests.swift
git commit -m "feat: add CharacterLibrary for user-created character packages"
```

---

### Task 2: RenderStyle — 픽셀 아트를 흐리지 않게 그린다

**Files:**
- Modify: `Sources/Unfold/Character/Character.swift`
- Modify: `Sources/Unfold/Character/CharacterManifest.swift:33-42`
- Modify: `Sources/Unfold/Character/CharacterPackageLoader.swift:95-107`
- Modify: `Sources/Unfold/Character/CharacterAnimationView.swift:26-28`
- Modify: `Sources/Unfold/DesktopPet/DesktopPetAnimationController.swift`
- Modify: `Sources/Unfold/DesktopPet/DesktopPetView.swift`
- Test: `Tests/UnfoldTests/CharacterManifestTests.swift`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/CharacterManifestTests.swift` 파일 **끝**에 다음 테스트를 추가한다 (기존 테스트는 건드리지 않는다). 이 파일에는 이미 `import XCTest` / `@testable import Unfold`가 있으므로 다시 쓰지 않는다. 클래스 마지막 `}` 앞에 붙인다:

```swift
    // MARK: - renderStyle

    func test_loadImported_noRenderStyle_defaultsToSmooth() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1,
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .smooth)
    }

    func test_loadImported_pixelRenderStyle_isDecoded() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1, "renderStyle": "pixel",
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .pixel)
    }

    /// An unknown value must not fail loading — an older build reading a
    /// newer package should still show the character, just smoothed.
    func test_loadImported_unknownRenderStyle_fallsBackToSmooth() throws {
        let dir = try Self.writeTempManifest("""
        {
          "id": "x", "name": "X", "version": 1, "renderStyle": "hologram",
          "spriteSheet": {"file": "s.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 7, "loop": true} }
        }
        """)
        defer { try? FileManager.default.removeItem(at: dir) }

        let character = try CharacterPackageLoader.loadImported(packageDirectory: dir)
        XCTAssertEqual(character.renderStyle, .smooth)
    }

    /// Shared by the renderStyle cases above: writes `json` as
    /// `character.json` in a fresh temp directory and returns that directory.
    private static func writeTempManifest(_ json: String) throws -> URL {
        let dir = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-manifest-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try json.write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
        return dir
    }
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter CharacterManifestTests`
Expected: 컴파일 실패 — `value of type 'Character' has no member 'renderStyle'`

- [ ] **Step 3: 도메인 모델에 RenderStyle을 추가한다**

`Sources/Unfold/Character/Character.swift`의 `struct Character` **위**에 타입을 추가한다:

```swift
/// How a character's frames should be scaled up to display size. Pixel art
/// turns to mush under smoothing, and illustrated art looks jagged without
/// it — so this travels with the character rather than being one global
/// choice. `SpriteAnimationView` already takes the interpolation mode; this
/// is what decides which one it gets.
enum RenderStyle: String, Equatable {
    case smooth
    case pixel
}
```

같은 파일의 `struct Character` 안, `let animations` 아래에 프로퍼티를 추가한다:

```swift
    /// Defaults to `.smooth` — the bundled illustrated characters' style.
    /// User-created pixel packages set this to `.pixel`.
    let renderStyle: RenderStyle
```

그리고 `struct Character` 안, `func animation(for:)` **위**에 명시적 `init`을 추가한다. 기본값을 준 덕분에 기존 호출부(`BuiltInCharacters.emergencyFallback`, `CharacterPackageLoader.makeCharacter`)는 수정 없이 계속 컴파일된다:

```swift
    init(
        id: String,
        name: String,
        thumbnailSymbolName: String,
        spriteSheet: SpriteSheetDefinition,
        animations: [AnimationKey: AnimationSource],
        renderStyle: RenderStyle = .smooth,
        source: CharacterSource
    ) {
        self.id = id
        self.name = name
        self.thumbnailSymbolName = thumbnailSymbolName
        self.spriteSheet = spriteSheet
        self.animations = animations
        self.renderStyle = renderStyle
        self.source = source
    }
```

- [ ] **Step 4: 매니페스트에 필드를 추가한다**

`Sources/Unfold/Character/CharacterManifest.swift`의 `thumbnailSymbol` 선언 아래에 추가:

```swift
    /// `"pixel"` or `"smooth"`. Absent or unrecognised means `"smooth"` —
    /// an older build reading a newer package still shows the character.
    let renderStyle: String?
```

- [ ] **Step 5: 로더가 값을 전달하게 한다**

`Sources/Unfold/Character/CharacterPackageLoader.swift`의 `makeCharacter`에서 `Character(...)`를 만드는 부분의 `animations: animations,` 다음 줄에 추가:

```swift
            renderStyle: RenderStyle(rawValue: manifest.renderStyle ?? "") ?? .smooth,
```

- [ ] **Step 6: 렌더 경로 두 곳이 이 값을 쓰게 한다**

`Sources/Unfold/Character/CharacterAnimationView.swift`의 `if let animator {` 블록 안을 다음으로 바꾼다:

```swift
                SpriteAnimationView(animator: animator, interpolation: character.renderStyle.interpolation)
```

같은 파일 **맨 끝**(`struct CharacterAnimationView`의 닫는 `}` 뒤)에 추가한다. `RenderStyle`은 도메인 모델이라 SwiftUI를 몰라야 하므로, 변환은 뷰 쪽에 둔다:

```swift
extension RenderStyle {
    /// Pixel art must not be interpolated — `.none` is what keeps a 64px
    /// frame crisp when it's drawn at `Constants.characterDisplaySize`.
    var interpolation: Image.Interpolation {
        switch self {
        case .pixel: return .none
        case .smooth: return .medium
        }
    }
}
```

`Sources/Unfold/DesktopPet/DesktopPetAnimationController.swift`는 `SpriteAnimationView`를 직접 쓰는 `DesktopPetView`에 값을 넘겨줘야 한다. `@Published private(set) var animator: SpriteAnimator` 아래에 추가:

```swift
    /// Handed to `DesktopPetView` so the pet renders with the same
    /// interpolation the overlay uses for this character.
    let renderStyle: RenderStyle
```

같은 파일 `init?(character:)`의 `idleAnimator = SpriteAnimator(clip: idleClip)` **바로 위**에 추가:

```swift
        renderStyle = character.renderStyle
```

`Sources/Unfold/DesktopPet/DesktopPetView.swift`의 `SpriteAnimationView(animator: controller.animator)`를 다음으로 바꾼다:

```swift
            SpriteAnimationView(animator: controller.animator, interpolation: controller.renderStyle.interpolation)
```

- [ ] **Step 7: 테스트가 통과하는지 확인한다**

Run: `swift test`
Expected: 신규 3개 포함 전부 PASS (기존 테스트는 하나도 깨지지 않아야 한다)

- [ ] **Step 8: 커밋한다**

```bash
git add Sources/Unfold/Character/Character.swift \
        Sources/Unfold/Character/CharacterManifest.swift \
        Sources/Unfold/Character/CharacterPackageLoader.swift \
        Sources/Unfold/Character/CharacterAnimationView.swift \
        Sources/Unfold/DesktopPet/DesktopPetAnimationController.swift \
        Sources/Unfold/DesktopPet/DesktopPetView.swift \
        Tests/UnfoldTests/CharacterManifestTests.swift
git commit -m "feat: per-character RenderStyle so pixel art renders unsmoothed"
```

---

### Task 3: 애니메이션 키 폴백 — idle만 그려도 캐릭터가 나온다

**Files:**
- Modify: `Sources/Unfold/Character/Character.swift`
- Modify: `Sources/Unfold/Character/CharacterAnimationView.swift:45`
- Test: `Tests/UnfoldTests/CharacterAnimationFallbackTests.swift`

사용자 캐릭터는 `idle` 하나만 갖는다. 지금 `StretchOverlayView`는 `.stretch`를 요청하고, 없으면 `makeAnimator`가 `nil`을 돌려줘 정적 SF Symbol이 뜬다 — 이 앱의 핵심 순간에 사용자가 만든 캐릭터가 안 나온다는 뜻이다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/CharacterAnimationFallbackTests.swift`:

```swift
import XCTest
@testable import Unfold

/// A character that only defines `idle` must still appear everywhere the
/// app asks for another clip — otherwise a user-made character (which is
/// only required to draw an idle loop) would vanish from the stretch
/// overlay, the one moment the app exists for.
final class CharacterAnimationFallbackTests: XCTestCase {

    private func makeCharacter(animations: [AnimationKey: AnimationSource]) -> Character {
        Character(
            id: "x",
            name: "X",
            thumbnailSymbolName: "pawprint.fill",
            spriteSheet: SpriteSheetDefinition(fileName: "s.png", columns: 2, rows: 1, frameWidth: 8, frameHeight: 8),
            animations: animations,
            source: .builtIn
        )
    }

    private var idleSource: AnimationSource {
        .spriteSheet(SpriteAnimationDefinition(frames: [0, 1], fps: 7, loop: true))
    }

    private var stretchSource: AnimationSource {
        .gif(fileName: "stretch.gif", loop: false)
    }

    func test_resolvedAnimation_exactKeyPresent_returnsThatKey() {
        let character = makeCharacter(animations: [.idle: idleSource, .stretch: stretchSource])
        XCTAssertEqual(character.resolvedAnimation(for: .stretch), stretchSource)
    }

    func test_resolvedAnimation_missingKey_fallsBackToIdle() {
        let character = makeCharacter(animations: [.idle: idleSource])
        XCTAssertEqual(character.resolvedAnimation(for: .stretch), idleSource)
    }

    func test_resolvedAnimation_missingKeyAndNoIdle_returnsNil() {
        let character = makeCharacter(animations: [.yawn: idleSource])
        XCTAssertNil(character.resolvedAnimation(for: .stretch))
    }

    /// `animation(for:)` stays an exact lookup — the fallback is a separate,
    /// explicit decision, not a change to what "does this character define
    /// this clip" means.
    func test_animationFor_staysExact() {
        let character = makeCharacter(animations: [.idle: idleSource])
        XCTAssertNil(character.animation(for: .stretch))
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter CharacterAnimationFallbackTests`
Expected: 컴파일 실패 — `value of type 'Character' has no member 'resolvedAnimation'`

- [ ] **Step 3: 폴백을 도메인 모델에 구현한다**

`Sources/Unfold/Character/Character.swift`의 `func animation(for:)` **아래**에 추가:

```swift
    /// What to actually play for `key`, falling back to `.idle` when this
    /// character doesn't define it. A character is only ever *required* to
    /// have an idle loop; every other moment degrades to that rather than
    /// to a generic symbol.
    ///
    /// Kept separate from `animation(for:)` — that one stays an exact
    /// lookup, so "does this character define a stretch clip?" still has a
    /// truthful answer.
    func resolvedAnimation(for key: AnimationKey) -> AnimationSource? {
        animations[key] ?? animations[.idle]
    }
```

- [ ] **Step 4: 뷰가 폴백을 쓰게 한다**

`Sources/Unfold/Character/CharacterAnimationView.swift`의 `makeAnimator` 첫 줄을 바꾼다:

```swift
        guard let source = character.resolvedAnimation(for: key) else { return nil }
```

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

Run: `swift test`
Expected: 전부 PASS

- [ ] **Step 6: 커밋한다**

```bash
git add Sources/Unfold/Character/Character.swift \
        Sources/Unfold/Character/CharacterAnimationView.swift \
        Tests/UnfoldTests/CharacterAnimationFallbackTests.swift
git commit -m "feat: fall back to the idle clip when a character lacks the requested animation"
```

---

### Task 4: EditorSavePayload — 웹에서 온 값을 하나도 믿지 않는다

**Files:**
- Create: `Sources/Unfold/CharacterEditor/EditorSavePayload.swift`
- Create: `Tests/UnfoldTests/Support/TestPNG.swift`
- Modify: `Sources/Unfold/Support/Constants.swift`
- Test: `Tests/UnfoldTests/EditorSavePayloadTests.swift`

- [ ] **Step 1: 테스트용 PNG 헬퍼를 만든다**

`Tests/UnfoldTests/Support/TestPNG.swift`:

```swift
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Builds real PNG bytes of an exact pixel size, so payload tests can check
/// the "declared geometry vs. what actually decoded" rule against genuine
/// image data rather than a stubbed decoder.
enum TestPNG {

    static func data(width: Int, height: Int) -> Data {
        var pixels = [UInt8](repeating: 0, count: width * height * 4)
        for i in stride(from: 0, to: pixels.count, by: 4) {
            pixels[i] = 200
            pixels[i + 1] = 120
            pixels[i + 2] = 40
            pixels[i + 3] = 255
        }
        let context = CGContext(
            data: &pixels,
            width: width,
            height: height,
            bitsPerComponent: 8,
            bytesPerRow: width * 4,
            space: CGColorSpaceCreateDeviceRGB(),
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        )!
        let image = context.makeImage()!

        let output = NSMutableData()
        let destination = CGImageDestinationCreateWithData(
            output, UTType.png.identifier as CFString, 1, nil
        )!
        CGImageDestinationAddImage(destination, image, nil)
        CGImageDestinationFinalize(destination)
        return output as Data
    }

    static func dataURL(width: Int, height: Int) -> String {
        "data:image/png;base64," + data(width: width, height: height).base64EncodedString()
    }
}
```

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/EditorSavePayloadTests.swift`:

```swift
import XCTest
@testable import Unfold

/// The editor runs untrusted-by-construction code (a vendored web app), so
/// every value it sends is re-checked here before anything touches disk.
/// These tests are the specification of that boundary.
final class EditorSavePayloadTests: XCTestCase {

    /// Valid by default; each test overrides exactly the field it's about.
    private func makeJSON(
        width: Int = 64,
        height: Int = 64,
        fps: String = "12",
        frameCount: Int = 4,
        sheetPNG: String? = nil,
        piskelJSON: String = "{\\\"modelVersion\\\":2}",
        characterID: String? = nil,
        type: String = "save"
    ) -> String {
        // `max(1,)`: the out-of-range cases below declare a 0-wide sheet, and
        // asking for a 0x0 image would fail inside TestPNG rather than in the
        // validation this test is about.
        let png = sheetPNG ?? TestPNG.dataURL(
            width: max(1, width * frameCount),
            height: max(1, height)
        )
        let idField = characterID.map { "\"characterID\": \"\($0)\"," } ?? ""
        return """
        {
          "type": "\(type)",
          \(idField)
          "width": \(width), "height": \(height),
          "fps": \(fps), "frameCount": \(frameCount),
          "sheetPNG": "\(png)",
          "piskelJSON": "\(piskelJSON)"
        }
        """
    }

    // MARK: - Happy path

    func test_decode_validMessage_producesPayload() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON())
        XCTAssertEqual(payload.width, 64)
        XCTAssertEqual(payload.height, 64)
        XCTAssertEqual(payload.frameCount, 4)
        XCTAssertEqual(payload.fps, 12)
        XCTAssertNil(payload.characterID)
        XCTAssertFalse(payload.sheetPNGData.isEmpty)
    }

    func test_decode_carriesCharacterIDWhenPresent() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(characterID: "user-abc"))
        XCTAssertEqual(payload.characterID, "user-abc")
    }

    // MARK: - Frame count bounds

    func test_decode_zeroFrames_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(frameCount: 0)))
    }

    func test_decode_oneFrame_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(frameCount: 1))
        XCTAssertEqual(payload.frameCount, 1)
    }

    func test_decode_maximumFrames_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(width: 8, height: 8, frameCount: 24))
        XCTAssertEqual(payload.frameCount, 24)
    }

    func test_decode_tooManyFrames_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 8, height: 8, frameCount: 25)))
    }

    // MARK: - Canvas size bounds

    func test_decode_maximumCanvas_isAccepted() throws {
        let payload = try EditorSavePayload.decode(from: makeJSON(width: 128, height: 128, frameCount: 1))
        XCTAssertEqual(payload.width, 128)
    }

    func test_decode_oversizedCanvas_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 129, height: 128, frameCount: 1)))
    }

    func test_decode_zeroCanvas_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(width: 0, height: 64, frameCount: 1)))
    }

    // MARK: - fps

    func test_decode_negativeFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "-1")))
    }

    func test_decode_zeroFPS_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(fps: "0")))
    }

    // MARK: - Sheet data

    func test_decode_wrongDataURLPrefix_isRejected() {
        let jpeg = "data:image/jpeg;base64," + TestPNG.data(width: 8, height: 8).base64EncodedString()
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: jpeg)))
    }

    func test_decode_notBase64_isRejected() {
        XCTAssertThrowsError(
            try EditorSavePayload.decode(from: makeJSON(sheetPNG: "data:image/png;base64,%%%not-base64%%%"))
        )
    }

    /// The declared grid is what the manifest will claim, so it has to match
    /// the image that actually decoded — otherwise a character would ship
    /// with a manifest that lies about its own sheet.
    func test_decode_pngSmallerThanDeclaredGrid_isRejected() {
        let tooSmall = TestPNG.dataURL(width: 64, height: 64)   // declared: 4 × 64 wide
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(sheetPNG: tooSmall)))
    }

    // MARK: - Message shape

    func test_decode_wrongMessageType_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(type: "cancel")))
    }

    func test_decode_notJSON_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: "not json at all"))
    }

    func test_decode_emptyPiskelJSON_isRejected() {
        XCTAssertThrowsError(try EditorSavePayload.decode(from: makeJSON(piskelJSON: "")))
    }
}
```

- [ ] **Step 3: 실패를 확인한다**

Run: `swift test --filter EditorSavePayloadTests`
Expected: 컴파일 실패 — `cannot find 'EditorSavePayload' in scope`

- [ ] **Step 4: Constants에 한계값을 추가한다**

`Sources/Unfold/Support/Constants.swift`의 `userCharactersFolderName` 아래에 추가:

```swift
    /// How many frames a user-drawn animation may have. 24 frames at the
    /// editor's default 12fps is a two-second loop — long enough for an
    /// idle animation, short enough that the sprite sheet stays small.
    static let editorFrameCountRange = 1...24

    /// Longest side, in pixels, of a user-drawn frame. Well under Piskel's
    /// own 1024 limit: past this the art stops reading as pixel art at
    /// `characterDisplaySize`, and the sheet stops being cheap to decode.
    static let editorCanvasSideRange = 1...128

    /// Canvas the editor seeds a brand-new character with.
    static let editorDefaultCanvasSide = 64
```

- [ ] **Step 5: EditorSavePayload를 구현한다**

`Sources/Unfold/CharacterEditor/EditorSavePayload.swift`:

```swift
import Foundation
import ImageIO

/// One "save" message from the editor bridge, decoded and validated.
///
/// The editor is a vendored web app running in a `WKWebView`: correct by
/// construction is not something this app can assume about it. So nothing
/// here is trusted — bounds are re-checked, and the geometry the message
/// *claims* is verified against the PNG that actually decoded. A payload
/// that exists is a payload that's safe to write.
struct EditorSavePayload: Equatable {

    enum DecodingError: Error, CustomStringConvertible {
        case malformedJSON
        case unexpectedMessageType(String)
        case frameCountOutOfRange(Int)
        case canvasSizeOutOfRange(width: Int, height: Int)
        case invalidFPS(Double)
        case notAPNGDataURL
        case notBase64
        case undecodablePNG
        case geometryMismatch(declared: String, actual: String)
        case emptySource

        var description: String {
            switch self {
            case .malformedJSON:
                return "the editor sent a message that isn't valid JSON"
            case .unexpectedMessageType(let type):
                return "unexpected message type \"\(type)\""
            case .frameCountOutOfRange(let count):
                return "\(count) frames is outside the allowed \(Constants.editorFrameCountRange)"
            case .canvasSizeOutOfRange(let width, let height):
                return "canvas \(width)x\(height)px is outside the allowed \(Constants.editorCanvasSideRange)px per side"
            case .invalidFPS(let fps):
                return "fps must be a finite positive number, got \(fps)"
            case .notAPNGDataURL:
                return "the sprite sheet is not a PNG data URL"
            case .notBase64:
                return "the sprite sheet's data URL is not valid base64"
            case .undecodablePNG:
                return "the sprite sheet could not be decoded as a PNG"
            case .geometryMismatch(let declared, let actual):
                return "the editor declared a \(declared) sheet but sent a \(actual) image"
            case .emptySource:
                return "the editor sent an empty .piskel document"
            }
        }
    }

    let width: Int
    let height: Int
    let fps: Double
    let frameCount: Int
    let sheetPNGData: Data
    let piskelJSON: String

    /// Set when the user is re-saving a character they opened for editing;
    /// `nil` for a brand-new one. Decides overwrite vs. create.
    let characterID: String?

    private static let pngDataURLPrefix = "data:image/png;base64,"

    private struct Wire: Decodable {
        let type: String
        let width: Int
        let height: Int
        let fps: Double
        let frameCount: Int
        let sheetPNG: String
        let piskelJSON: String
        let characterID: String?
    }

    static func decode(from json: String) throws -> EditorSavePayload {
        guard
            let data = json.data(using: .utf8),
            let wire = try? JSONDecoder().decode(Wire.self, from: data)
        else {
            throw DecodingError.malformedJSON
        }

        guard wire.type == "save" else {
            throw DecodingError.unexpectedMessageType(wire.type)
        }
        guard Constants.editorFrameCountRange.contains(wire.frameCount) else {
            throw DecodingError.frameCountOutOfRange(wire.frameCount)
        }
        guard
            Constants.editorCanvasSideRange.contains(wire.width),
            Constants.editorCanvasSideRange.contains(wire.height)
        else {
            throw DecodingError.canvasSizeOutOfRange(width: wire.width, height: wire.height)
        }
        guard wire.fps.isFinite, wire.fps > 0 else {
            throw DecodingError.invalidFPS(wire.fps)
        }
        guard !wire.piskelJSON.isEmpty else {
            throw DecodingError.emptySource
        }
        guard wire.sheetPNG.hasPrefix(pngDataURLPrefix) else {
            throw DecodingError.notAPNGDataURL
        }

        let base64 = String(wire.sheetPNG.dropFirst(pngDataURLPrefix.count))
        guard let sheetData = Data(base64Encoded: base64) else {
            throw DecodingError.notBase64
        }

        let expectedWidth = wire.width * wire.frameCount
        let (actualWidth, actualHeight) = try pixelSize(of: sheetData)
        guard actualWidth == expectedWidth, actualHeight == wire.height else {
            throw DecodingError.geometryMismatch(
                declared: "\(expectedWidth)x\(wire.height)px",
                actual: "\(actualWidth)x\(actualHeight)px"
            )
        }

        return EditorSavePayload(
            width: wire.width,
            height: wire.height,
            fps: wire.fps,
            frameCount: wire.frameCount,
            sheetPNGData: sheetData,
            piskelJSON: wire.piskelJSON,
            characterID: wire.characterID
        )
    }

    /// Reads the PNG header only — no full decode, and no `NSImage`, whose
    /// size is display-scale dependent (the same reason `SpriteSheetImage`
    /// goes through `CGImageSource`).
    private static func pixelSize(of data: Data) throws -> (Int, Int) {
        guard
            let source = CGImageSourceCreateWithData(data as CFData, nil),
            let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
            let width = properties[kCGImagePropertyPixelWidth] as? Int,
            let height = properties[kCGImagePropertyPixelHeight] as? Int
        else {
            throw DecodingError.undecodablePNG
        }
        return (width, height)
    }
}
```

- [ ] **Step 6: 테스트가 통과하는지 확인한다**

Run: `swift test --filter EditorSavePayloadTests`
Expected: 17개 테스트 전부 PASS

- [ ] **Step 7: 커밋한다**

```bash
git add Sources/Unfold/CharacterEditor/EditorSavePayload.swift \
        Sources/Unfold/Support/Constants.swift \
        Tests/UnfoldTests/Support/TestPNG.swift \
        Tests/UnfoldTests/EditorSavePayloadTests.swift
git commit -m "feat: validate editor save payloads before anything touches disk"
```

---

### Task 5: CharacterPackageWriter — 실패해도 흔적을 남기지 않는다

**Files:**
- Create: `Sources/Unfold/CharacterEditor/CharacterPackageWriter.swift`
- Modify: `Sources/Unfold/Support/Constants.swift`
- Test: `Tests/UnfoldTests/CharacterPackageWriterTests.swift`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/CharacterPackageWriterTests.swift`:

```swift
import XCTest
@testable import Unfold

/// The writer's contract in one line: after it returns, the library either
/// holds a package `CharacterPackageLoader` can read, or it holds exactly
/// what it held before. There is no third outcome.
final class CharacterPackageWriterTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-writer-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func makePayload(
        frameCount: Int = 4,
        side: Int = 64,
        fps: Double = 12,
        characterID: String? = nil
    ) throws -> EditorSavePayload {
        let png = TestPNG.dataURL(width: side * frameCount, height: side)
        let idField = characterID.map { "\"characterID\": \"\($0)\"," } ?? ""
        return try EditorSavePayload.decode(from: """
        {
          "type": "save", \(idField)
          "width": \(side), "height": \(side),
          "fps": \(fps), "frameCount": \(frameCount),
          "sheetPNG": "\(png)",
          "piskelJSON": "{\\"modelVersion\\":2}"
        }
        """)
    }

    // MARK: - Round trip

    func test_write_producesPackageTheLoaderCanRead() throws {
        let payload = try makePayload()
        let character = try CharacterPackageWriter.write(payload: payload, name: "Mari", into: library)

        XCTAssertEqual(character.name, "Mari")
        // 이 두 줄이 함께 있어야 의미가 있다: `thumbnailSymbol`과 `renderStyle`은
        // 매니페스트에서 둘 다 `String?`이라 서로 바꿔 써도 컴파일이 통과한다.
        // 값이 서로 구별되므로 스왑은 여기서 즉시 실패한다.
        XCTAssertEqual(character.renderStyle, .pixel)
        XCTAssertEqual(character.thumbnailSymbolName, "pawprint.fill")
        XCTAssertEqual(character.spriteSheet.columns, 4)
        XCTAssertEqual(character.spriteSheet.rows, 1)
        XCTAssertEqual(character.spriteSheet.frameWidth, 64)

        guard case .spriteSheet(let definition) = character.animation(for: .idle) else {
            return XCTFail("expected an idle sprite-sheet animation")
        }
        XCTAssertEqual(definition.frames, [0, 1, 2, 3])
        XCTAssertEqual(definition.fps, 12)
        XCTAssertTrue(definition.loop)

        // Re-reading through the public loader path proves the package is
        // genuinely well-formed, not just that `write` returned something.
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let reloaded = try CharacterPackageLoader.loadImported(packageDirectory: directory)
        XCTAssertEqual(reloaded.id, character.id)
    }

    func test_write_storesAllThreeFiles() throws {
        let character = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)
        let directory = try XCTUnwrap(library.packageDirectory(id: character.id))
        let names = Set(try FileManager.default.contentsOfDirectory(atPath: directory.path))
        XCTAssertEqual(names, ["character.json", "spritesheet.png", "source.piskel"])
    }

    func test_write_newCharacter_getsUserPrefixedID() throws {
        let character = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)
        XCTAssertTrue(character.id.hasPrefix("user-"), "got \(character.id)")
    }

    // MARK: - Overwrite

    func test_write_sameCharacterID_overwritesInPlace() throws {
        let first = try CharacterPackageWriter.write(payload: try makePayload(), name: "Mari", into: library)

        let updated = try makePayload(frameCount: 2, characterID: first.id)
        let second = try CharacterPackageWriter.write(payload: updated, name: "Mari v2", into: library)

        XCTAssertEqual(second.id, first.id)
        XCTAssertEqual(second.name, "Mari v2")
        XCTAssertEqual(second.spriteSheet.columns, 2)
        XCTAssertEqual(library.packageDirectories().count, 1, "overwrite must not create a second package")
    }

    // MARK: - Failure leaves nothing behind

    func test_write_blankName_throwsAndWritesNothing() throws {
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: try makePayload(), name: "   ", into: library))
        XCTAssertEqual(library.packageDirectories(), [])
    }

    func test_write_unsafeCharacterID_throwsAndWritesNothing() throws {
        let payload = try makePayload(characterID: "..")
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: payload, name: "Mari", into: library))
        XCTAssertEqual(library.packageDirectories(), [])
    }

    /// A failed write must not leave a staging directory lying around for
    /// the repository to trip over on the next launch.
    func test_write_failure_leavesNoStagingDirectory() throws {
        XCTAssertThrowsError(try CharacterPackageWriter.write(payload: try makePayload(), name: "", into: library))
        let entries = (try? FileManager.default.contentsOfDirectory(atPath: root.path)) ?? []
        XCTAssertEqual(entries, [], "expected an empty library root, found \(entries)")
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter CharacterPackageWriterTests`
Expected: 컴파일 실패 — `cannot find 'CharacterPackageWriter' in scope`

- [ ] **Step 3: Constants에 패키지 파일 이름을 추가한다**

`Sources/Unfold/Support/Constants.swift`의 `editorDefaultCanvasSide` 아래에 추가:

```swift
    /// File names inside a user-created character package. These are
    /// constants, never values that came from the editor — which is why a
    /// saved package can't be made to reference a path of the web side's
    /// choosing.
    static let characterManifestFileName = "character.json"
    static let characterSpriteSheetFileName = "spritesheet.png"
    static let characterEditorSourceFileName = "source.piskel"

    /// SF Symbol given to every user-created character until the thumbnail
    /// renders a real frame.
    static let userCharacterThumbnailSymbol = "pawprint.fill"
```

- [ ] **Step 4: CharacterPackageWriter를 구현한다**

`Sources/Unfold/CharacterEditor/CharacterPackageWriter.swift`:

```swift
import Foundation

/// Turns a validated `EditorSavePayload` into a character package on disk.
///
/// Writes go to a staging directory inside the library root first, are
/// verified by loading them back through `CharacterPackageLoader` — the
/// same code path every other character takes — and only then move into
/// place. Staging inside the library keeps the move on one volume, so the
/// final step is a rename rather than a copy that could half-finish.
///
/// The consequence that matters to the user: a failed save never destroys
/// the character they already had, and never leaves a broken package the
/// repository would have to skip.
enum CharacterPackageWriter {

    enum WriteError: Error, CustomStringConvertible {
        case blankName
        case invalidCharacterID(String)
        case selfValidationFailed(String)

        var description: String {
            switch self {
            case .blankName:
                return "a character needs a name"
            case .invalidCharacterID(let id):
                return "\"\(id)\" is not a usable character id"
            case .selfValidationFailed(let reason):
                return "the saved package failed its own validation — \(reason)"
            }
        }
    }

    /// Returns the character as loaded back from its final location, so the
    /// caller works with exactly what the rest of the app will see.
    @discardableResult
    static func write(payload: EditorSavePayload, name: String, into library: CharacterLibrary) throws -> Character {
        let trimmedName = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedName.isEmpty else { throw WriteError.blankName }

        let id = payload.characterID ?? "user-\(UUID().uuidString.lowercased())"
        guard
            CharacterLibrary.isSafeID(id),
            let finalDirectory = library.packageDirectory(id: id)
        else {
            throw WriteError.invalidCharacterID(id)
        }

        try library.createRootIfNeeded()

        let staging = library.rootDirectory
            .appendingPathComponent(".staging-\(UUID().uuidString)", isDirectory: true)
        // Any exit from here on — thrown or successful — leaves no staging
        // directory behind.
        defer { try? FileManager.default.removeItem(at: staging) }

        try FileManager.default.createDirectory(at: staging, withIntermediateDirectories: true)
        try payload.sheetPNGData.write(to: staging.appendingPathComponent(Constants.characterSpriteSheetFileName))
        try Data(payload.piskelJSON.utf8).write(to: staging.appendingPathComponent(Constants.characterEditorSourceFileName))
        try makeManifestData(payload: payload, name: trimmedName, id: id)
            .write(to: staging.appendingPathComponent(Constants.characterManifestFileName))

        do {
            _ = try CharacterPackageLoader.loadImported(packageDirectory: staging)
        } catch {
            throw WriteError.selfValidationFailed(String(describing: error))
        }

        if FileManager.default.fileExists(atPath: finalDirectory.path) {
            try FileManager.default.removeItem(at: finalDirectory)
        }
        try FileManager.default.moveItem(at: staging, to: finalDirectory)

        return try CharacterPackageLoader.loadImported(packageDirectory: finalDirectory)
    }

    /// One row, `frameCount` columns — the sheet's frame order is Piskel's
    /// frame order with no arithmetic in between.
    private static func makeManifestData(payload: EditorSavePayload, name: String, id: String) throws -> Data {
        let manifest = CharacterManifest(
            id: id,
            name: name,
            version: 1,
            spriteSheet: CharacterManifest.SpriteSheetDTO(
                file: Constants.characterSpriteSheetFileName,
                columns: payload.frameCount,
                rows: 1,
                frameWidth: payload.width,
                frameHeight: payload.height
            ),
            animations: [
                AnimationKey.idle.rawValue: CharacterManifest.AnimationDTO(
                    frames: Array(0..<payload.frameCount),
                    fps: payload.fps,
                    gif: nil,
                    loop: true
                )
            ],
            thumbnailSymbol: Constants.userCharacterThumbnailSymbol,
            renderStyle: RenderStyle.pixel.rawValue
        )

        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        return try encoder.encode(manifest)
    }
}
```

> **`thumbnailSymbol`과 `renderStyle`을 서로 바꿔 넣는 실수는 컴파일러가 못 잡는다.**
> Swift의 합성 멤버와이즈 `init`은 인자 레이블을 강제하므로 위치가 밀려 들어갈
> 일은 없다. 위험한 건 *올바른 레이블에 잘못된 값*을 넘기는 경우다 — 둘 다
> `String?`이라 그래도 컴파일이 통과한다. 그래서 방어는 주석이 아니라 아래
> 테스트가 한다: 두 값을 서로 구별되게(`"pawprint.fill"` vs `"pixel"`) 두고
> 로더 왕복 후 **각각 독립적으로** 단언한다.

- [ ] **Step 5: 테스트가 통과하는지 확인한다**

Run: `swift test --filter CharacterPackageWriterTests`
Expected: 7개 테스트 전부 PASS

- [ ] **Step 6: 커밋한다**

```bash
git add Sources/Unfold/CharacterEditor/CharacterPackageWriter.swift \
        Sources/Unfold/Support/Constants.swift \
        Tests/UnfoldTests/CharacterPackageWriterTests.swift
git commit -m "feat: write character packages atomically with loader self-validation"
```

---

### Task 6: ImportedCharacterRepository — 저장한 캐릭터가 목록에 나온다

**Files:**
- Modify: `Sources/Unfold/Character/CharacterRepository.swift:19-28`
- Modify: `Sources/Unfold/Character/CharacterManager.swift`
- Modify: `Sources/Unfold/App/AppDelegate.swift`
- Test: `Tests/UnfoldTests/ImportedCharacterRepositoryTests.swift`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/ImportedCharacterRepositoryTests.swift`:

```swift
import XCTest
@testable import Unfold

/// One corrupt package on disk must cost the user that one character — not
/// the whole list, and not a launch.
final class ImportedCharacterRepositoryTests: XCTestCase {

    private var root: URL!
    private var library: CharacterLibrary!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory
            .appendingPathComponent("unfold-repo-test-\(UUID().uuidString)")
        library = CharacterLibrary(rootDirectory: root)
        try library.createRootIfNeeded()
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: root)
    }

    private func writeValidPackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try """
        {
          "id": "\(id)", "name": "\(id)", "version": 1, "renderStyle": "pixel",
          "spriteSheet": {"file": "spritesheet.png", "columns": 2, "rows": 1, "frameWidth": 8, "frameHeight": 8},
          "animations": { "idle": {"frames": [0,1], "fps": 12, "loop": true} }
        }
        """.write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
        try TestPNG.data(width: 16, height: 8).write(to: dir.appendingPathComponent("spritesheet.png"))
    }

    private func writeBrokenPackage(id: String) throws {
        let dir = root.appendingPathComponent(id, isDirectory: true)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        try "{ not a manifest".write(to: dir.appendingPathComponent("character.json"), atomically: true, encoding: .utf8)
    }

    func test_characters_emptyLibrary_isEmpty() {
        XCTAssertEqual(ImportedCharacterRepository(library: library).characters(), [])
    }

    func test_characters_loadsValidPackages() throws {
        try writeValidPackage(id: "user-a")
        let characters = ImportedCharacterRepository(library: library).characters()
        XCTAssertEqual(characters.map(\.id), ["user-a"])
        XCTAssertEqual(characters.first?.renderStyle, .pixel)
    }

    func test_characters_skipsBrokenPackagesAndKeepsTheRest() throws {
        try writeValidPackage(id: "user-a")
        try writeBrokenPackage(id: "user-broken")
        try writeValidPackage(id: "user-c")

        XCTAssertEqual(ImportedCharacterRepository(library: library).characters().map(\.id), ["user-a", "user-c"])
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter ImportedCharacterRepositoryTests`
Expected: 컴파일 실패 — `extra argument 'library' in call`

- [ ] **Step 3: 리포지토리를 구현한다**

`Sources/Unfold/Character/CharacterRepository.swift`의 `struct ImportedCharacterRepository` 전체(주석 포함)를 다음으로 교체한다:

```swift
/// Characters the user created in the built-in editor, one package
/// directory each under `CharacterLibrary`.
///
/// A package that fails to load is skipped with a log line rather than
/// propagated: one bad directory costs the user that character, not the
/// whole list. Built-in characters take the same approach
/// (`CharacterPackageLoader.loadBuiltIn` returns `nil` on failure).
struct ImportedCharacterRepository: CharacterRepository {

    private let library: CharacterLibrary

    init(library: CharacterLibrary = .makeDefault()) {
        self.library = library
    }

    func characters() -> [Character] {
        library.packageDirectories().compactMap { directory in
            do {
                return try CharacterPackageLoader.loadImported(packageDirectory: directory)
            } catch {
                NSLog("Unfold: skipping unreadable character package at \(directory.lastPathComponent) — \(error)")
                return nil
            }
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

Run: `swift test --filter ImportedCharacterRepositoryTests`
Expected: 3개 테스트 전부 PASS

- [ ] **Step 5: CharacterManager가 목록을 다시 읽을 수 있게 한다**

`Sources/Unfold/Character/CharacterManager.swift`에서 `@Published private(set) var current: Character` 아래에 추가:

```swift
    /// Published rather than computed: saving a new character has to move
    /// the Settings picker, and a computed property gives SwiftUI nothing
    /// to observe.
    @Published private(set) var availableCharacters: [Character]
```

같은 파일의 기존 계산 프로퍼티를 삭제한다:

```swift
    var availableCharacters: [Character] {
        repository.characters()
    }
```

`init`의 본문 전체를 다음으로 교체한다:

```swift
    init(repository: CharacterRepository, settings: SettingsStore) {
        self.repository = repository
        self.settings = settings

        let characters = repository.characters()
        self.availableCharacters = characters
        self.current = Self.resolveCurrent(from: characters, settings: settings)
    }
```

`init` 아래에 추가한다:

```swift
    /// Re-reads every repository and re-resolves the current selection.
    /// Call after the character catalog changes on disk — a save or a
    /// delete in the editor.
    func reloadCatalog() {
        let characters = repository.characters()
        availableCharacters = characters
        if !characters.contains(where: { $0.id == current.id }) {
            current = Self.resolveCurrent(from: characters, settings: settings)
        }
    }

    /// The saved selection if it still exists, otherwise the first
    /// available character — and the stored id is healed on the way, so a
    /// character whose package was deleted doesn't have to be re-resolved
    /// on every future launch.
    private static func resolveCurrent(from characters: [Character], settings: SettingsStore) -> Character {
        if let savedID = settings.selectedCharacterID,
           let match = characters.first(where: { $0.id == savedID }) {
            return match
        }

        let fallback = characters.first ?? BuiltInCharacters.emergencyFallback
        if settings.selectedCharacterID != nil {
            NSLog("Unfold: selectedCharacterID \"\(settings.selectedCharacterID ?? "")\" not found — falling back to \"\(fallback.id)\"")
            settings.selectedCharacterID = fallback.id
        }
        return fallback
    }
```

- [ ] **Step 6: 라이브러리를 조립 지점에서 한 번만 만든다**

`Sources/Unfold/App/AppDelegate.swift`의 프로퍼티 목록에서 `private var settings: SettingsStore?` 아래에 추가:

```swift
    private var characterLibrary: CharacterLibrary?
```

`applicationDidFinishLaunching` 안에서 `let characterManager = CharacterManager(` 블록을 다음으로 교체한다:

```swift
        // One library instance, shared by the repository that reads
        // packages and (later) the editor that writes them — so there is
        // exactly one answer to "where do user characters live".
        let characterLibrary = CharacterLibrary.makeDefault()
        let characterManager = CharacterManager(
            repository: CompositeCharacterRepository(repositories: [
                BuiltInCharacterRepository(),
                ImportedCharacterRepository(library: characterLibrary)
            ]),
            settings: settings
        )
```

같은 함수 아래쪽의 `self.settings = settings` 다음 줄에 추가:

```swift
        self.characterLibrary = characterLibrary
```

- [ ] **Step 7: 전체 테스트와 빌드를 확인한다**

Run: `swift test`
Expected: 전부 PASS

Run: `swift build`
Expected: 경고 없이 성공

- [ ] **Step 8: 커밋한다**

```bash
git add Sources/Unfold/Character/CharacterRepository.swift \
        Sources/Unfold/Character/CharacterManager.swift \
        Sources/Unfold/App/AppDelegate.swift \
        Tests/UnfoldTests/ImportedCharacterRepositoryTests.swift
git commit -m "feat: load user-created character packages from the library"
```

> **여기까지가 스펙의 1단계다.** Piskel 없이도 `Application Support/Unfold/Characters/<id>/`에 손으로 만든 패키지를 넣으면 Settings 목록에 나타난다. 중간에 멈추기 좋은 지점이다.

---

### Task 7: Piskel 벤더링 — 빌드 산출물을 리소스로 넣는다

**Files:**
- Create: `Sources/Unfold/Resources/Editor/piskel/` (벤더링)
- Create: `Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt`
- Create: `Sources/Unfold/Resources/Editor/LICENSE-piskel.txt`
- Create: `Sources/Unfold/Resources/Editor/NOTICE-piskel.txt`
- Modify: `Package.swift:20-24`

이 태스크에는 단위 테스트가 없다 — 결과물이 "번들 안에 파일이 있는가"이고, 그건 Task 9의 창이 실제로 뜨는지로 검증된다.

- [ ] **Step 1: Piskel을 빌드한다**

```bash
cd /tmp
rm -rf piskel-src
git clone https://github.com/piskelapp/piskel.git piskel-src
cd piskel-src
git rev-parse HEAD          # 이 SHA를 적어둔다
npm ci
npm run build
ls dest/prod                # index.html, js/, css/, img/ 가 보여야 한다
```

`npm ci`가 실패하면 `npm install`을 쓴다. `dest/prod/index.html`이 없으면 다음 단계로 넘어가지 말고 빌드 로그를 확인한다.

- [ ] **Step 2: 산출물을 리소스로 복사한다**

```bash
cd /Users/hwanghyeonseong/Documents/GitHub/Unfold
mkdir -p Sources/Unfold/Resources/Editor/piskel
cp -R /tmp/piskel-src/dest/prod/. Sources/Unfold/Resources/Editor/piskel/
cp /tmp/piskel-src/LICENSE Sources/Unfold/Resources/Editor/LICENSE-piskel.txt
ls Sources/Unfold/Resources/Editor/piskel/index.html   # 존재해야 한다
```

- [ ] **Step 3: 출처를 기록한다**

`Sources/Unfold/Resources/Editor/PISKEL-VERSION.txt` (SHA는 Step 1에서 적어둔 값으로 바꾼다):

```
Piskel — https://github.com/piskelapp/piskel
Vendored commit: <STEP 1에서 적어둔 SHA>
Vendored on: 2026-09-03
Built with: npm ci && npm run build  (output: dest/prod)

이 디렉터리의 piskel/ 내용은 수정하지 않는다. Unfold의 커스터마이징은 전부
../unfold-bridge.js 와 ../unfold-bridge.css 에만 있고, 런타임에 WKUserScript로
주입된다. Piskel을 갱신하려면 위 명령을 다시 돌려 piskel/ 을 통째로 교체하면
된다 — 재적용할 패치는 없다.
```

`Sources/Unfold/Resources/Editor/NOTICE-piskel.txt`:

```
This application bundles Piskel, an open source pixel art editor.

Piskel
Copyright the Piskel authors
https://github.com/piskelapp/piskel

Licensed under the Apache License, Version 2.0. A copy of the license is
included as LICENSE-piskel.txt. Piskel is bundled unmodified.
```

- [ ] **Step 4: SwiftPM 리소스로 등록한다**

`Package.swift`의 `Unfold` 타깃 `resources:` 배열을 다음으로 바꾼다:

```swift
            resources: [
                .copy("Resources/Characters"),
                .copy("Resources/Editor")
            ]
```

- [ ] **Step 5: 번들에 실제로 들어갔는지 확인한다**

```bash
swift build
ls .build/debug/Unfold_Unfold.bundle/Editor/piskel/index.html
```

Expected: 경로가 출력된다 (`No such file` 이면 `.copy` 경로를 다시 확인한다)

- [ ] **Step 6: 커밋한다**

Piskel 산출물은 파일 수가 많다. 한 커밋으로 묶어 나중에 갱신할 때 통째로 교체하기 쉽게 한다.

```bash
git add Sources/Unfold/Resources/Editor Package.swift
git commit -m "chore: vendor Piskel build output as an app resource (Apache-2.0, unmodified)"
```

---

### Task 8: EditorNavigationPolicy — 원격 코드가 들어올 문을 없앤다

**Files:**
- Create: `Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift`
- Test: `Tests/UnfoldTests/EditorNavigationPolicyTests.swift`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/UnfoldTests/EditorNavigationPolicyTests.swift`:

```swift
import XCTest
@testable import Unfold

/// The editor may only ever navigate within its own bundled resources.
/// This is what makes "the app never downloads or runs remote code" a
/// property of the code rather than a promise — App Store guideline 2.5.2
/// turns on exactly this.
final class EditorNavigationPolicyTests: XCTestCase {

    private let editorDirectory = URL(fileURLWithPath: "/Apps/Unfold.app/Resources/Editor", isDirectory: true)

    func test_allows_fileInsideEditorDirectory() {
        let url = editorDirectory.appendingPathComponent("piskel/index.html")
        XCTAssertTrue(EditorNavigationPolicy.allows(url: url, editorDirectory: editorDirectory))
    }

    func test_allows_theEditorDirectoryItself() {
        XCTAssertTrue(EditorNavigationPolicy.allows(url: editorDirectory, editorDirectory: editorDirectory))
    }

    func test_rejects_httpsURL() {
        XCTAssertFalse(EditorNavigationPolicy.allows(
            url: URL(string: "https://piskelapp.com")!, editorDirectory: editorDirectory
        ))
    }

    func test_rejects_fileOutsideEditorDirectory() {
        XCTAssertFalse(EditorNavigationPolicy.allows(
            url: URL(fileURLWithPath: "/etc/passwd"), editorDirectory: editorDirectory
        ))
    }

    /// `/Apps/.../EditorEvil` shares a string prefix with the editor
    /// directory but is a different directory — a plain `hasPrefix` on the
    /// path would wrongly allow it.
    func test_rejects_siblingDirectoryWithSharedPrefix() {
        let sibling = URL(fileURLWithPath: "/Apps/Unfold.app/Resources/EditorEvil/x.html")
        XCTAssertFalse(EditorNavigationPolicy.allows(url: sibling, editorDirectory: editorDirectory))
    }

    func test_rejects_traversalOutOfTheEditorDirectory() {
        let escape = editorDirectory.appendingPathComponent("../../../../etc/passwd")
        XCTAssertFalse(EditorNavigationPolicy.allows(url: escape, editorDirectory: editorDirectory))
    }

    func test_rejects_nilURL() {
        XCTAssertFalse(EditorNavigationPolicy.allows(url: nil, editorDirectory: editorDirectory))
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `swift test --filter EditorNavigationPolicyTests`
Expected: 컴파일 실패 — `cannot find 'EditorNavigationPolicy' in scope`

- [ ] **Step 3: 정책을 구현한다**

`Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift`:

```swift
import Foundation

/// Decides whether the editor web view may navigate to a URL.
///
/// The answer is only ever yes for a `file://` URL inside the app's own
/// bundled editor directory. Piskel's UI contains links to the outside
/// world; following one inside the web view would put a live web page —
/// remote code — inside the app. It also means a saved `.piskel` document
/// can never talk the editor into loading something else.
///
/// A free function on purpose: `WKNavigationDelegate` is awkward to test,
/// this isn't.
enum EditorNavigationPolicy {

    static func allows(url: URL?, editorDirectory: URL) -> Bool {
        guard let url, url.isFileURL else { return false }

        // `standardized` resolves `..` before comparison, so a traversal
        // can't sneak past by spelling its way out and back.
        let candidate = url.standardized.path
        let root = editorDirectory.standardized.path

        if candidate == root { return true }
        // The separator is what stops `/…/EditorEvil` from matching
        // `/…/Editor`.
        return candidate.hasPrefix(root.hasSuffix("/") ? root : root + "/")
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

Run: `swift test --filter EditorNavigationPolicyTests`
Expected: 7개 테스트 전부 PASS

- [ ] **Step 5: 커밋한다**

```bash
git add Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift \
        Tests/UnfoldTests/EditorNavigationPolicyTests.swift
git commit -m "feat: restrict the editor web view to its own bundled files"
```

---

### Task 9: 에디터 창 — 그리고, 저장한다

**Files:**
- Create: `Sources/Unfold/Resources/Editor/unfold-bridge.js`
- Create: `Sources/Unfold/Resources/Editor/unfold-bridge.css`
- Create: `Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift`
- Modify: `Sources/Unfold/Support/Strings.swift`

이 태스크는 `WKWebView`가 결과물이라 단위 테스트가 아니라 **실제 앱으로 검증한다.** 스펙이 유일하게 미해결로 남긴 엔타이틀먼트 리스크도 여기서 해소된다.

- [ ] **Step 1: 브릿지 CSS를 쓴다**

`Sources/Unfold/Resources/Editor/unfold-bridge.css`:

```css
/*
 * Unfold의 유일한 Piskel UI 개입. 벤더링된 Piskel 파일은 손대지 않고,
 * 이 규칙들이 런타임에 주입된다.
 *
 * 익스포트/저장 패널을 숨기는 이유: Unfold 안에서 파일을 내려받는 경로는
 * 동작하지 않고(샌드박스), 저장은 "Save to Spine Keepet" 버튼 하나로만
 * 이뤄져야 하기 때문이다. 그리기 도구·프레임 타임라인·팔레트·리사이즈는
 * 전부 그대로 둔다.
 */

[data-setting="export"],
[data-setting="save"],
[data-setting="import"] {
  display: none !important;
}

#unfold-save-button {
  position: fixed;
  top: 8px;
  right: 12px;
  z-index: 10000;
  padding: 7px 16px;
  border: 0;
  border-radius: 6px;
  background: #3478f6;
  color: #fff;
  font: 600 13px/1.2 -apple-system, BlinkMacSystemFont, "Helvetica Neue", sans-serif;
  cursor: pointer;
}

#unfold-save-button:hover { background: #2c6ade; }
#unfold-save-button:disabled { opacity: 0.5; cursor: default; }

#unfold-error {
  position: fixed;
  top: 44px;
  right: 12px;
  z-index: 10000;
  max-width: 320px;
  padding: 8px 12px;
  border-radius: 6px;
  background: #d13438;
  color: #fff;
  font: 400 12px/1.4 -apple-system, BlinkMacSystemFont, "Helvetica Neue", sans-serif;
}
```

- [ ] **Step 2: 브릿지 JS를 쓴다**

`Sources/Unfold/Resources/Editor/unfold-bridge.js`:

```js
/*
 * Unfold <-> Piskel bridge.
 *
 * Piskel 소스는 한 줄도 고치지 않는다. 이 파일이 WKUserScript로 주입되어
 * 저장 버튼을 달고, 프레임을 한 장의 스프라이트 시트로 합성해 네이티브로
 * 넘긴다. 의존하는 Piskel 전역은 아래 여섯 가지뿐이고, 벤더링된 빌드가
 * 고정돼 있으므로 이 표면은 변하지 않는다:
 *
 *   pskl.app.piskelController.getFrameCount / renderFrameAt /
 *     getWidth / getHeight / getFPS / serialize / setPiskel
 *   pskl.utils.FrameUtils.toImage
 *   pskl.utils.serialization.Deserializer.deserialize
 */
(function () {
  "use strict";

  var SAVE_BUTTON_ID = "unfold-save-button";
  var ERROR_ID = "unfold-error";

  /* Piskel은 DOM ready 이후에 스스로를 초기화한다. 주입 시점이 그보다
   * 이를 수 있으므로 컨트롤러가 생길 때까지 기다린다. */
  function whenPiskelReady(callback) {
    if (window.pskl && pskl.app && pskl.app.piskelController && pskl.utils) {
      callback();
      return;
    }
    window.setTimeout(function () { whenPiskelReady(callback); }, 100);
  }

  function post(message) {
    window.webkit.messageHandlers.unfold.postMessage(JSON.stringify(message));
  }

  /* 프레임을 가로 1행으로 이어 붙인다. 시트 인덱스가 Piskel의 프레임
   * 순서와 1:1이라 매니페스트의 frames 배열이 그냥 [0..n-1]이 된다. */
  function buildSheetDataURL(controller) {
    var frameCount = controller.getFrameCount();
    var width = controller.getWidth();
    var height = controller.getHeight();

    var canvas = document.createElement("canvas");
    canvas.width = width * frameCount;
    canvas.height = height;
    var context = canvas.getContext("2d");

    for (var i = 0; i < frameCount; i++) {
      var frame = controller.renderFrameAt(i, true);
      context.drawImage(pskl.utils.FrameUtils.toImage(frame, 1), i * width, 0);
    }
    return canvas.toDataURL("image/png");
  }

  /* 완전히 투명한 size x size 스프라이트 한 장짜리 .piskel 문서.
   * Piskel의 Serializer가 내는 것과 같은 모양이다: layers는 JSON 문자열
   * 배열이고, 청크는 {layout, base64PNG}. */
  function blankPiskelJSON(size) {
    var canvas = document.createElement("canvas");
    canvas.width = size;
    canvas.height = size;

    var layer = JSON.stringify({
      name: "Layer 1",
      opacity: 1,
      frameCount: 1,
      chunks: [{ layout: [[0]], base64PNG: canvas.toDataURL() }]
    });

    return JSON.stringify({
      modelVersion: 2,
      piskel: {
        name: "Unfold Character",
        description: "",
        fps: 12,
        width: size,
        height: size,
        layers: [layer]
      }
    });
  }

  function loadDocument(json, done) {
    pskl.utils.serialization.Deserializer.deserialize(
      JSON.parse(json),
      function (piskel) {
        pskl.app.piskelController.setPiskel(piskel);
        done();
      },
      function (error) {
        window.console.error("Unfold: could not load the saved document", error);
        done();
      }
    );
  }

  function save() {
    var button = document.getElementById(SAVE_BUTTON_ID);
    if (button) { button.disabled = true; }
    clearError();

    var controller = pskl.app.piskelController;
    var init = window.__unfoldInit || {};

    post({
      type: "save",
      width: controller.getWidth(),
      height: controller.getHeight(),
      fps: controller.getFPS(),
      frameCount: controller.getFrameCount(),
      sheetPNG: buildSheetDataURL(controller),
      piskelJSON: controller.serialize(),
      characterID: init.characterID || null
    });
  }

  function clearError() {
    var existing = document.getElementById(ERROR_ID);
    if (existing) { existing.remove(); }
  }

  function installButton(label) {
    var button = document.createElement("button");
    button.id = SAVE_BUTTON_ID;
    button.type = "button";
    button.textContent = label;
    button.addEventListener("click", save);
    document.body.appendChild(button);
  }

  /* 네이티브가 저장에 실패했을 때 부른다. 창은 닫히지 않고 그림도
   * 그대로다 — 사용자가 고쳐서 다시 누르면 된다. */
  window.unfoldBridge = {
    saveFailed: function (message) {
      var button = document.getElementById(SAVE_BUTTON_ID);
      if (button) { button.disabled = false; }
      clearError();

      var banner = document.createElement("div");
      banner.id = ERROR_ID;
      banner.textContent = message;
      document.body.appendChild(banner);
    }
  };

  whenPiskelReady(function () {
    var init = window.__unfoldInit || {};
    var json = init.piskelJSON || blankPiskelJSON(init.canvasSide || 64);

    loadDocument(json, function () {
      installButton(init.saveButtonLabel || "Save");
    });
  });
})();
```

- [ ] **Step 3: 새 문구를 Strings에 추가한다**

`Sources/Unfold/Support/Strings.swift`의 `enum Overlay { ... }` **아래**, 바깥 `enum Strings`의 닫는 `}` 앞에 추가:

```swift
    enum Editor {
        static let windowTitle = "Character Editor"
        static let saveButton = "Save to Spine Keepet"

        static let namePromptTitle = "Name your character"
        static let namePromptMessage = "This is the name you'll see in the character list."
        static let namePromptPlaceholder = "Mochi"
        static let namePromptConfirm = "Save"
        static let namePromptCancel = "Cancel"

        static let unavailableTitle = "The character editor couldn't start"
        static let unavailableMessage = "Its files are missing from this build of Spine Keepet."
        static let unavailableDismiss = "OK"

        static func saveFailed(_ reason: String) -> String {
            "Couldn't save: \(reason)"
        }
    }
```

- [ ] **Step 4: 창 컨트롤러를 구현한다**

`Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift`:

```swift
import AppKit
import WebKit

/// The character editor window: a `WKWebView` running the bundled Piskel
/// build, with Unfold's bridge script injected on top.
///
/// Piskel itself is untouched — everything Unfold changes about it lives in
/// `unfold-bridge.js` / `.css`, injected here. Nothing loads over the
/// network: the web view is pointed at a file URL and
/// `EditorNavigationPolicy` refuses anything else.
@MainActor
final class CharacterEditorWindowController: NSObject {

    private let library: CharacterLibrary
    private let onCharacterSaved: (Character) -> Void

    private var window: NSWindow?
    private var webView: WKWebView?

    /// The character being edited, if this session started from "Edit…".
    private var editingCharacterID: String?

    init(library: CharacterLibrary, onCharacterSaved: @escaping (Character) -> Void) {
        self.library = library
        self.onCharacterSaved = onCharacterSaved
    }

    /// `nil` when this build has no editor resources — checked before a
    /// window is ever shown, so the user never sees an empty web view.
    private static var editorDirectory: URL? {
        Bundle.module.url(forResource: "Editor", withExtension: nil)
    }

    // MARK: - Opening

    func createNewCharacter() {
        open(existingSource: nil, characterID: nil)
    }

    func edit(character: Character) {
        guard
            let directory = library.packageDirectory(id: character.id),
            let source = try? String(
                contentsOf: directory.appendingPathComponent(Constants.characterEditorSourceFileName),
                encoding: .utf8
            )
        else {
            // A character with no `.piskel` alongside it (hand-made, or from
            // a future import path) can't be reopened — start a fresh
            // document rather than refusing outright.
            open(existingSource: nil, characterID: nil)
            return
        }
        open(existingSource: source, characterID: character.id)
    }

    private func open(existingSource: String?, characterID: String?) {
        guard
            let editorDirectory = Self.editorDirectory,
            let indexURL = Self.indexURL(in: editorDirectory)
        else {
            presentEditorUnavailable()
            return
        }

        // Reopening replaces the session rather than stacking windows: the
        // editor holds one document at a time.
        close()
        editingCharacterID = characterID

        let webView = makeWebView(
            editorDirectory: editorDirectory,
            existingSource: existingSource,
            characterID: characterID
        )
        self.webView = webView

        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1100, height: 760),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.title = Strings.Editor.windowTitle
        window.contentView = webView
        window.isReleasedWhenClosed = false
        window.center()
        self.window = window

        webView.loadFileURL(indexURL, allowingReadAccessTo: editorDirectory)

        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }

    private static func indexURL(in editorDirectory: URL) -> URL? {
        let url = editorDirectory
            .appendingPathComponent("piskel", isDirectory: true)
            .appendingPathComponent("index.html")
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }

    func close() {
        webView?.configuration.userContentController.removeAllUserScripts()
        webView?.configuration.userContentController.removeScriptMessageHandler(forName: Self.messageHandlerName)
        window?.close()
        window = nil
        webView = nil
        editingCharacterID = nil
    }

    // MARK: - Web view

    private static let messageHandlerName = "unfold"

    private func makeWebView(editorDirectory: URL, existingSource: String?, characterID: String?) -> WKWebView {
        let controller = WKUserContentController()

        // Injected before Piskel runs, so the bridge finds its parameters
        // already in place rather than waiting for a round trip.
        controller.addUserScript(WKUserScript(
            source: Self.initScript(existingSource: existingSource, characterID: characterID),
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        ))

        if let css = Self.resourceText("unfold-bridge", "css", in: editorDirectory) {
            controller.addUserScript(WKUserScript(
                source: Self.styleInjectionScript(css: css),
                injectionTime: .atDocumentEnd,
                forMainFrameOnly: true
            ))
        }
        if let js = Self.resourceText("unfold-bridge", "js", in: editorDirectory) {
            controller.addUserScript(WKUserScript(
                source: js,
                injectionTime: .atDocumentEnd,
                forMainFrameOnly: true
            ))
        }

        // `WKUserContentController` retains its handler, so the proxy holds
        // this controller weakly — otherwise the window could never
        // deallocate.
        controller.add(ScriptMessageProxy(target: self), name: Self.messageHandlerName)

        let configuration = WKWebViewConfiguration()
        configuration.userContentController = controller

        let webView = WKWebView(frame: .zero, configuration: configuration)
        webView.allowsBackForwardNavigationGestures = false
        webView.navigationDelegate = self
        return webView
    }

    private static func resourceText(_ name: String, _ ext: String, in directory: URL) -> String? {
        try? String(contentsOf: directory.appendingPathComponent("\(name).\(ext)"), encoding: .utf8)
    }

    private static func initScript(existingSource: String?, characterID: String?) -> String {
        // JSONSerialization does the escaping, so a `.piskel` document's
        // quotes and backslashes can't break out of the literal.
        var payload: [String: Any] = [
            "canvasSide": Constants.editorDefaultCanvasSide,
            "saveButtonLabel": Strings.Editor.saveButton
        ]
        payload["piskelJSON"] = existingSource
        payload["characterID"] = characterID

        guard
            let data = try? JSONSerialization.data(withJSONObject: payload),
            let json = String(data: data, encoding: .utf8)
        else {
            return "window.__unfoldInit = {};"
        }
        return "window.__unfoldInit = \(json);"
    }

    private static func styleInjectionScript(css: String) -> String {
        let encoded = (try? JSONSerialization.data(withJSONObject: [css]))
            .flatMap { String(data: $0, encoding: .utf8) } ?? "[\"\"]"
        return """
        (function () {
          var style = document.createElement('style');
          style.textContent = \(encoded)[0];
          document.head.appendChild(style);
        })();
        """
    }

    // MARK: - Saving

    fileprivate func handle(messageBody: Any) {
        guard let json = messageBody as? String else {
            NSLog("Unfold: editor sent a non-string message")
            return
        }

        do {
            let payload = try EditorSavePayload.decode(from: json)
            guard let name = promptForName() else {
                // Cancelled at the name prompt — re-enable the button and
                // leave the drawing exactly as it was.
                reportSaveFailure("")
                return
            }
            let character = try CharacterPackageWriter.write(payload: payload, name: name, into: library)
            onCharacterSaved(character)
            close()
        } catch {
            reportSaveFailure(String(describing: error))
        }
    }

    private func promptForName() -> String? {
        let alert = NSAlert()
        alert.messageText = Strings.Editor.namePromptTitle
        alert.informativeText = Strings.Editor.namePromptMessage
        alert.addButton(withTitle: Strings.Editor.namePromptConfirm)
        alert.addButton(withTitle: Strings.Editor.namePromptCancel)

        let field = NSTextField(frame: NSRect(x: 0, y: 0, width: 220, height: 24))
        field.placeholderString = Strings.Editor.namePromptPlaceholder
        alert.accessoryView = field

        NSApp.activate(ignoringOtherApps: true)
        guard alert.runModal() == .alertFirstButtonReturn else { return nil }

        let name = field.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        return name.isEmpty ? nil : name
    }

    /// An empty `reason` just re-enables the button (the user cancelled);
    /// anything else also shows why.
    private func reportSaveFailure(_ reason: String) {
        let message = reason.isEmpty ? "" : Strings.Editor.saveFailed(reason)
        let encoded = (try? JSONSerialization.data(withJSONObject: [message]))
            .flatMap { String(data: $0, encoding: .utf8) } ?? "[\"\"]"
        webView?.evaluateJavaScript("window.unfoldBridge.saveFailed(\(encoded)[0]);")
    }

    private func presentEditorUnavailable() {
        let alert = NSAlert()
        alert.messageText = Strings.Editor.unavailableTitle
        alert.informativeText = Strings.Editor.unavailableMessage
        alert.addButton(withTitle: Strings.Editor.unavailableDismiss)
        NSApp.activate(ignoringOtherApps: true)
        alert.runModal()
    }
}

// MARK: - Navigation

extension CharacterEditorWindowController: WKNavigationDelegate {

    func webView(
        _ webView: WKWebView,
        decidePolicyFor navigationAction: WKNavigationAction,
        decisionHandler: @escaping (WKNavigationActionPolicy) -> Void
    ) {
        guard let editorDirectory = Self.editorDirectory else {
            decisionHandler(.cancel)
            return
        }

        let allowed = EditorNavigationPolicy.allows(
            url: navigationAction.request.url,
            editorDirectory: editorDirectory
        )
        if !allowed {
            NSLog("Unfold: blocked editor navigation to \(navigationAction.request.url?.absoluteString ?? "nil")")
        }
        decisionHandler(allowed ? .allow : .cancel)
    }
}

// MARK: - Message handler

/// Breaks the retain cycle `WKUserContentController` would otherwise create
/// by holding its message handler strongly.
private final class ScriptMessageProxy: NSObject, WKScriptMessageHandler {

    private weak var target: CharacterEditorWindowController?

    init(target: CharacterEditorWindowController) {
        self.target = target
    }

    func userContentController(_ controller: WKUserContentController, didReceive message: WKScriptMessage) {
        Task { @MainActor [weak target] in
            target?.handle(messageBody: message.body)
        }
    }
}
```

- [ ] **Step 5: 빌드한다**

Run: `swift build`
Expected: 성공

Run: `swift test`
Expected: 전부 PASS (이 태스크는 기존 테스트를 건드리지 않는다)

- [ ] **Step 6: 커밋한다**

```bash
git add Sources/Unfold/Resources/Editor/unfold-bridge.js \
        Sources/Unfold/Resources/Editor/unfold-bridge.css \
        Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift \
        Sources/Unfold/Support/Strings.swift
git commit -m "feat: character editor window hosting the vendored Piskel build"
```

---

### Task 10: Settings 배선 — 만들고, 고치고, 지운다

**Files:**
- Modify: `Sources/Unfold/Settings/SettingsView.swift`
- Modify: `Sources/Unfold/Settings/SettingsWindowController.swift`
- Modify: `Sources/Unfold/App/AppDelegate.swift`
- Modify: `Sources/Unfold/Support/Strings.swift`
- Modify: `Packaging/Unfold.entitlements` (필요할 때만 — Step 6 참조)

- [ ] **Step 1: 문구를 추가한다**

`Sources/Unfold/Support/Strings.swift`의 `enum Settings` 안, `defaultCharacterLabel` 아래에 추가:

```swift
        static let createCharacter = "Create Character…"
        static let editCharacter = "Edit…"
        static let deleteCharacter = "Delete"
        static let deleteConfirmTitle = "Delete this character?"
        static func deleteConfirmMessage(_ name: String) -> String {
            "\"\(name)\" will be removed from your Mac. This can't be undone."
        }
        static let deleteConfirm = "Delete"
        static let deleteCancel = "Cancel"
```

- [ ] **Step 2: SettingsView에 액션을 추가한다**

`Sources/Unfold/Settings/SettingsView.swift`의 저장 프로퍼티 목록(`let onIntervalChanged: () -> Void` 아래)에 추가:

```swift
    let onCreateCharacter: () -> Void
    let onEditCharacter: (Character) -> Void
    let onDeleteCharacter: (Character) -> Void
```

같은 파일의 `init`을 다음으로 교체한다:

```swift
    init(
        settings: SettingsStore,
        characterManager: CharacterManager,
        onIntervalChanged: @escaping () -> Void,
        onCreateCharacter: @escaping () -> Void,
        onEditCharacter: @escaping (Character) -> Void,
        onDeleteCharacter: @escaping (Character) -> Void
    ) {
        self.settings = settings
        self.characterManager = characterManager
        self.onIntervalChanged = onIntervalChanged
        self.onCreateCharacter = onCreateCharacter
        self.onEditCharacter = onEditCharacter
        self.onDeleteCharacter = onDeleteCharacter
        _customIntervalText = State(initialValue: String(settings.stretchInterval.minutes))
    }
```

Characters 섹션 안, `if isCurrentCharacterDefault { ... }` 블록과 그 아래 주석("Future extension point…" 전체)을 다음으로 교체한다:

```swift
                if isCurrentCharacterDefault {
                    Text(Strings.Settings.defaultCharacterLabel)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                HStack {
                    Button(Strings.Settings.createCharacter, action: onCreateCharacter)
                    Spacer()
                    // Only user-made characters can be edited or deleted —
                    // the bundled ones aren't the user's to change.
                    if isCurrentCharacterUserMade {
                        Button(Strings.Settings.editCharacter) {
                            onEditCharacter(characterManager.current)
                        }
                        Button(Strings.Settings.deleteCharacter, role: .destructive) {
                            onDeleteCharacter(characterManager.current)
                        }
                    }
                }
```

`isCurrentCharacterDefault` 계산 프로퍼티 아래에 추가:

```swift
    private var isCurrentCharacterUserMade: Bool {
        if case .imported = characterManager.current.source { return true }
        return false
    }
```

- [ ] **Step 3: SettingsWindowController가 액션을 전달하게 한다**

`Sources/Unfold/Settings/SettingsWindowController.swift`의 저장 프로퍼티에 추가 (`private var window: NSWindow?` 위):

```swift
    private let editor: CharacterEditorWindowController
    private let library: CharacterLibrary
```

`init`을 다음으로 교체한다:

```swift
    init(
        settings: SettingsStore,
        timer: StretchTimer,
        characterManager: CharacterManager,
        editor: CharacterEditorWindowController,
        library: CharacterLibrary
    ) {
        self.settings = settings
        self.timer = timer
        self.characterManager = characterManager
        self.editor = editor
        self.library = library
    }
```

`show()` 안의 `let view = SettingsView(` 호출을 다음으로 교체한다:

```swift
        let view = SettingsView(
            settings: settings,
            characterManager: characterManager,
            // Changing the interval starts a fresh full-length countdown
            // from now, the same policy the menu bar's own interval picker
            // already uses — never an immediate reminder just because the
            // setting changed.
            onIntervalChanged: { [weak timer] in timer?.reset() },
            onCreateCharacter: { [weak editor] in editor?.createNewCharacter() },
            onEditCharacter: { [weak editor] character in editor?.edit(character: character) },
            onDeleteCharacter: { [weak self] character in self?.confirmDelete(character) }
        )
```

`show()` 아래, `bringToFront` 위에 추가:

```swift
    /// Deleting removes files from the user's Mac, so it asks first.
    private func confirmDelete(_ character: Character) {
        let alert = NSAlert()
        alert.messageText = Strings.Settings.deleteConfirmTitle
        alert.informativeText = Strings.Settings.deleteConfirmMessage(character.name)
        alert.alertStyle = .warning
        alert.addButton(withTitle: Strings.Settings.deleteConfirm)
        alert.addButton(withTitle: Strings.Settings.deleteCancel)

        guard alert.runModal() == .alertFirstButtonReturn else { return }

        do {
            try library.delete(id: character.id)
        } catch {
            NSLog("Unfold: could not delete character \"\(character.id)\" — \(error)")
        }
        // Reload either way: if the delete half-succeeded, the list should
        // still reflect what's actually on disk. `CharacterManager` heals
        // the stored selection when the current character disappears.
        characterManager.reloadCatalog()
    }
```

- [ ] **Step 4: AppDelegate에서 조립한다**

`Sources/Unfold/App/AppDelegate.swift`의 프로퍼티 목록에 추가 (`private var settingsWindow: SettingsWindowController?` 위):

```swift
    private var characterEditor: CharacterEditorWindowController?
```

`applicationDidFinishLaunching` 안의 `let settingsWindow = SettingsWindowController(` 블록을 다음으로 교체한다:

```swift
        // Saving a character has to move the Settings picker immediately —
        // that's the whole point of `reloadCatalog`, and it's why the
        // editor is handed a callback rather than a reference to the
        // manager.
        let characterEditor = CharacterEditorWindowController(
            library: characterLibrary,
            onCharacterSaved: { [weak characterManager] character in
                characterManager?.reloadCatalog()
                characterManager?.select(character)
            }
        )

        let settingsWindow = SettingsWindowController(
            settings: settings,
            timer: timer,
            characterManager: characterManager,
            editor: characterEditor,
            library: characterLibrary
        )
```

같은 함수 아래쪽의 `self.settingsWindow = settingsWindow` **위**에 추가:

```swift
        self.characterEditor = characterEditor
```

- [ ] **Step 5: 빌드하고 테스트한다**

Run: `swift build && swift test`
Expected: 성공, 전부 PASS

- [ ] **Step 6: 샌드박스 실제 앱에서 확인한다 — 엔타이틀먼트 리스크 해소 지점**

```bash
swift build -c release
./Scripts/make-app-bundle.sh release
open "build/Spine Keepet.app"
```

메뉴바 아이콘 → Settings… → **Create Character…**

확인할 것:

1. Piskel 에디터가 뜨고 캔버스가 64×64인가
2. 오른쪽 위에 "Save to Spine Keepet" 버튼이 있는가
3. 익스포트/저장/임포트 패널이 안 보이는가
4. 몇 프레임 그리고 저장 → 이름 입력 → Settings 목록에 나타나는가
5. 데스크톱 펫이 그 캐릭터로 바뀌고 **픽셀이 선명한가** (흐릿하면 `renderStyle`이 안 붙은 것)
6. Console.app에서 `Unfold: blocked editor navigation` 이 정상 사용 중에 뜨지 않는가

**웹뷰가 비어 있거나 로드되지 않으면** 스펙이 예상한 엔타이틀먼트 문제다. `Packaging/Unfold.entitlements`에 다음을 추가하고 Step 6을 다시 돌린다:

```xml
    <!--
    WKWebView는 로컬 파일만 로드하더라도 별도 네트워킹 프로세스를 거치므로
    샌드박스에서 이 엔타이틀먼트를 요구한다. 캐릭터 에디터(로컬 Piskel
    번들)를 띄우기 위한 것이고, 앱이 실제로 원격에 접속하지는 않는다 —
    EditorNavigationPolicy가 file:// 외의 모든 네비게이션을 거부한다.
    -->
    <key>com.apple.security.network.client</key><true/>
```

추가하지 않아도 동작했다면 **추가하지 않는다.** 이 파일의 "필요할 때만 추가한다"는 원칙을 지킨다.

- [ ] **Step 7: 커밋한다**

```bash
git add Sources/Unfold/Settings/SettingsView.swift \
        Sources/Unfold/Settings/SettingsWindowController.swift \
        Sources/Unfold/App/AppDelegate.swift \
        Sources/Unfold/Support/Strings.swift \
        Packaging/Unfold.entitlements
git commit -m "feat: create, edit, and delete characters from Settings"
```

---

### Task 11: 썸네일 — 목록에서 내 캐릭터를 알아본다

**Files:**
- Modify: `Sources/Unfold/Character/CharacterThumbnailView.swift`

사용자 캐릭터가 여럿이면 전부 같은 `pawprint.fill`로 보인다. 첫 프레임을 그려 구분되게 한다.

- [ ] **Step 1: 썸네일이 프레임 0을 그리게 한다**

`Sources/Unfold/Character/CharacterThumbnailView.swift` 전체를 교체한다:

```swift
import CoreGraphics
import SwiftUI

/// Renders a character's thumbnail: the first frame of its idle animation
/// when that can be loaded, and the SF Symbol otherwise.
///
/// The symbol alone was fine while every character was built in. Once users
/// make their own, a list of identical pawprints stops telling them which
/// character is which — so this draws the actual art.
struct CharacterThumbnailView: View {
    let character: Character
    var size: CGFloat = 28

    @State private var frame: CGImage?

    var body: some View {
        Group {
            if let frame {
                Image(decorative: frame, scale: 1, orientation: .up)
                    .resizable()
                    .interpolation(character.renderStyle.interpolation)
                    .aspectRatio(contentMode: .fit)
            } else {
                Image(systemName: character.thumbnailSymbolName)
                    .font(.system(size: size * 0.6))
                    .symbolRenderingMode(.hierarchical)
            }
        }
        .frame(width: size, height: size)
        .task(id: character.id) {
            frame = Self.firstIdleFrame(of: character)
        }
    }

    /// `static` for the same reason `CharacterAnimationView.makeAnimator`
    /// is: it's callable from a test without a SwiftUI lifecycle.
    static func firstIdleFrame(of character: Character) -> CGImage? {
        guard
            case .spriteSheet(let definition)? = character.resolvedAnimation(for: .idle),
            let firstIndex = definition.frames.first,
            let sheet = CharacterAssetLoader.loadSpriteSheetImage(for: character)
        else {
            return nil
        }
        return sheet.frame(at: firstIndex)
    }
}
```

- [ ] **Step 2: 빌드하고 테스트한다**

Run: `swift build && swift test`
Expected: 성공, 전부 PASS

- [ ] **Step 3: 눈으로 확인한다**

```bash
swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"
```

Settings → Character 피커를 연다. 사용자 캐릭터는 자기 그림으로, 번들 캐릭터는 자기 첫 프레임으로 보여야 한다.

- [ ] **Step 4: 커밋한다**

```bash
git add Sources/Unfold/Character/CharacterThumbnailView.swift
git commit -m "feat: draw the real first frame as a character thumbnail"
```

---

### Task 12: 라이선스 고지 — Apache-2.0 의무를 지킨다

**Files:**
- Modify: `Sources/Unfold/Settings/SettingsView.swift`
- Modify: `Sources/Unfold/Support/Strings.swift`

Apache-2.0은 라이선스 사본과 귀속 고지를 요구한다. Task 7에서 파일은 번들에 넣었고, 여기서 사용자가 볼 수 있게 한다. Piskel을 수정하지 않았으므로 "변경 명시" 의무는 없다.

- [ ] **Step 1: 문구를 추가한다**

`Sources/Unfold/Support/Strings.swift`의 `enum Settings` 안, `deleteCancel` 아래에 추가:

```swift
        static let aboutSectionTitle = "About"
        static let openSourceLabel = "Open source"
        static let openSourceValue = "Includes Piskel (Apache-2.0)"
        static let openSourceButton = "View License"
```

- [ ] **Step 2: Settings에 섹션을 추가한다**

`Sources/Unfold/Settings/SettingsView.swift`의 Characters 섹션 **아래**, `Form`의 닫는 `}` 앞에 추가:

```swift
            Section(Strings.Settings.aboutSectionTitle) {
                HStack {
                    Text(Strings.Settings.openSourceLabel)
                    Spacer()
                    Text(Strings.Settings.openSourceValue)
                        .foregroundStyle(.secondary)
                }
                Button(Strings.Settings.openSourceButton) {
                    openPiskelLicense()
                }
            }
```

같은 파일의 `isCurrentCharacterUserMade` 아래에 추가:

```swift
    /// Opens the bundled Apache-2.0 text in the user's default text viewer.
    /// The file ships in the editor resources (see `PISKEL-VERSION.txt`).
    private func openPiskelLicense() {
        guard
            let editorDirectory = Bundle.module.url(forResource: "Editor", withExtension: nil)
        else {
            return
        }
        NSWorkspace.shared.open(editorDirectory.appendingPathComponent("LICENSE-piskel.txt"))
    }
```

`SettingsView.swift` 맨 위 import에 추가 (`import SwiftUI` 아래):

```swift
import AppKit
```

- [ ] **Step 3: 빌드하고 확인한다**

```bash
swift build && swift test
swift build -c release && ./Scripts/make-app-bundle.sh release && open "build/Spine Keepet.app"
```

Settings → About → **View License** 를 눌러 Apache-2.0 전문이 열리는지 확인한다.

- [ ] **Step 4: 커밋한다**

```bash
git add Sources/Unfold/Settings/SettingsView.swift Sources/Unfold/Support/Strings.swift
git commit -m "docs: surface the bundled Piskel license in Settings"
```

---

## 완료 후 최종 확인

- [ ] `swift test` — 전부 통과
- [ ] `swift build -c release && ./Scripts/make-app-bundle.sh release` — 성공
- [ ] 샌드박스 앱에서 **만들기 → 저장 → 펫 반영 → 편집으로 다시 열기 → 저장 → 삭제** 왕복
- [ ] 편집으로 다시 열었을 때 이전에 그린 그림이 그대로 나오는가
- [ ] 저장을 취소했을 때 그림이 남아 있고 버튼이 다시 눌리는가
- [ ] `Packaging/Unfold.entitlements`에 실제로 필요했던 것만 들어 있는가
- [ ] `README.md`의 "Project layout"에 `CharacterEditor/`와 `Resources/Editor/`를 추가했는가
