# 캐릭터 에디터 파일 입출력 일반화 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 캐릭터 에디터의 Piskel 전용 파일 표면을 포맷 레지스트리 기반의 일반적인 열기/저장/다른 이름으로 저장으로 바꾸고, PNG·JPEG 범용 열기와 애니메이션 GIF 내보내기를 추가한다.

**Architecture:** `EditorFileFormat`(포맷 레지스트리)과 `EditorDocumentOrigin`(문서 출처)이 접합면이다. 메뉴와 파일 패널은 레지스트리를 보고 그려지고, 하나의 Save가 출처에 되쓴다. 래스터 디코딩은 `PixelDocumentCodec` 에서 `RasterImageDecoder` 로 분리해 PNG 전용 검사와 픽셀 추출 경로를 나눈다. 라이브러리 저장 경로는 호출 지점만 바뀌고 로직은 손대지 않는다.

**Tech Stack:** Swift 6.3, SwiftUI, AppKit, ImageIO / CoreGraphics / UniformTypeIdentifiers, XCTest

**설계 문서:** [2026-09-08-editor-file-io-generalization-design.md](../specs/2026-09-08-editor-file-io-generalization-design.md)

**진행 방식:** D → A → C 순차. 각 단계 완료 후 사용자에게 보고하고 다음으로 넘어간다.

---

## 사전 확인

작업 시작 전 기준선을 기록한다.

```bash
swift test 2>&1 | tail -3
```

기대: `Executed 289 tests, with 0 failures`

---

## 파일 구조

### 신규

| 파일 | 책임 |
| --- | --- |
| `Sources/Unfold/CharacterEditor/EditorFileFormat.swift` | 포맷 레지스트리. 확장자·UTType·읽기/쓰기 가능 여부만 안다. 코덱은 모른다 |
| `Sources/Unfold/CharacterEditor/EditorDocumentOrigin.swift` | 문서 출처. Save가 목적지를 물어야 하는지 판정 |
| `Sources/Unfold/CharacterEditor/RasterImageDecoder.swift` | PNG/JPEG 바이트 → 픽셀. 포맷별 무결성 검사 포함 |
| `Sources/Unfold/CharacterEditor/ImportOptions.swift` | 분할·크롭·축소 결정과 그 적용. 순수 함수, UI 없음 |
| `Sources/Unfold/CharacterEditor/ImportOptionsView.swift` | 가져오기 대화상자 |
| `Sources/Unfold/CharacterEditor/AnimatedGIFEncoder.swift` | ImageIO 애니메이션 GIF 라이터 |

### 수정

| 파일 | 변경 |
| --- | --- |
| `CharacterEditorWindowController.swift` | 출처 도입, 열기/저장/다른 이름으로 저장 |
| `PixelEditorView.swift` | File 메뉴, 배율 표시, 캔버스 크기 범위 |
| `PixelEditorModel.swift` | `baseScale`, undo `trim` |
| `PixelDocumentCodec.swift` | 래스터 디코딩 분리, 명칭 정리 |
| `EditorSavePayload.swift` | `piskelJSON` → `sourceJSON` |
| `PixelDocument.swift` | 총량 상한 검증, 하한 클램프 |
| `Constants.swift` | 캔버스 범위, 총량 상한 |
| `Strings.swift` | 데드 문자열 제거 |

### 삭제

- `Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift`
- `Tests/UnfoldTests/EditorNavigationPolicyTests.swift`
- `Sources/Unfold/Resources/Editor/` (디렉터리 전체)

---

# Phase D — 접합면과 정리

## Task 1: 데드 코드 삭제

웹뷰 시절 잔해를 먼저 치운다. 남겨두면 이후 작업에서 "이건 왜 있지"를 반복하게 된다.

**Files:**
- Delete: `Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift`
- Delete: `Tests/UnfoldTests/EditorNavigationPolicyTests.swift`
- Delete: `Sources/Unfold/Resources/Editor/` (전체)
- Modify: `Sources/Unfold/Support/Strings.swift:80-97`

- [ ] **Step 1: 삭제 전 프로덕션 참조가 없음을 확인**

```bash
grep -rn "EditorNavigationPolicy" --include="*.swift" Sources/
grep -rn "WebKit\|WKWebView" --include="*.swift" Sources/ Tests/
```

기대: 두 명령 모두 출력 없음 (`EditorNavigationPolicy.swift` 자기 정의 한 줄만 나오면 그것도 삭제 대상)

- [ ] **Step 2: 데드 문자열 참조가 없음을 확인**

```bash
for s in unavailableTitle unavailableMessage unavailableDismiss windowTitle namePromptPlaceholder; do
  printf "%-22s " "$s"
  grep -rn "Editor.$s" --include="*.swift" Sources Tests | grep -v "Strings.swift" | wc -l | tr -d ' '
done
```

기대: 전부 `0`

- [ ] **Step 3: 삭제**

```bash
git rm -q Sources/Unfold/CharacterEditor/EditorNavigationPolicy.swift
git rm -q Tests/UnfoldTests/EditorNavigationPolicyTests.swift
git rm -rq Sources/Unfold/Resources/Editor
```

- [ ] **Step 4: `Strings.Editor` 에서 데드 문자열 제거**

`Sources/Unfold/Support/Strings.swift` 의 `enum Editor` 블록을 아래로 교체한다.

```swift
    enum Editor {
        static let saveButton = "Save to Spine Keepet"

        static let namePromptTitle = "Name your character"
        static let namePromptMessage = "This is the name you'll see in the character list."
        static let namePromptConfirm = "Save"
        static let namePromptCancel = "Cancel"

        static func saveFailed(_ reason: String) -> String {
            "Couldn't save: \(reason)"
        }
    }
```

- [ ] **Step 5: 빌드와 테스트**

```bash
swift build 2>&1 | tail -3
swift test 2>&1 | tail -3
```

기대: 빌드 성공. 테스트는 `EditorNavigationPolicyTests` 가 사라져 289개보다 줄어든 수로 `0 failures`. 새 개수를 기록해 이후 태스크의 기준으로 삼는다.

- [ ] **Step 6: `Package.swift` 의 `exclude` 정리**

`Resources/Editor` 가 사라졌으므로 `exclude` 도 지운다.

```swift
        .target(
            name: "Unfold",
            path: "Sources/Unfold",
            resources: [
                .copy("Resources/Characters")
            ]
        ),
```

- [ ] **Step 7: 빌드 재확인**

```bash
swift build 2>&1 | tail -3
```

기대: `Build complete!` (경고 없이)

- [ ] **Step 8: 커밋**

```bash
git add -A
git commit -m "chore(editor): drop the web-view era leftovers

The editor has been native since ce5e024. EditorNavigationPolicy guarded
web-view navigation and no longer has a caller; the 2.5 MB vendored
Piskel runtime under Resources/Editor was excluded from the SPM target
and absent from the Xcode project, so it shipped in no build. Five of the
six Strings.Editor entries had no reference either."
```

---

## Task 2: `EditorFileFormat`

포맷 레지스트리. 메뉴와 파일 패널이 하드코딩된 목록 대신 이걸 본다.

**Files:**
- Create: `Sources/Unfold/CharacterEditor/EditorFileFormat.swift`
- Test: `Tests/UnfoldTests/EditorFileFormatTests.swift`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/UnfoldTests/EditorFileFormatTests.swift`:

```swift
import XCTest
import UniformTypeIdentifiers
@testable import Unfold

final class EditorFileFormatTests: XCTestCase {

    func test_readableFormats_excludeGIF_whichImportIsDeferredFor() {
        XCTAssertFalse(EditorFileFormat.readable.contains(.gif))
        XCTAssertTrue(EditorFileFormat.readable.contains(.unfoldSource))
        XCTAssertTrue(EditorFileFormat.readable.contains(.png))
        XCTAssertTrue(EditorFileFormat.readable.contains(.jpeg))
    }

    /// JPEG has no alpha channel and is lossy: exporting pixel art to it
    /// would fill transparent pixels black and blur pixel edges.
    func test_writableFormats_excludeJPEG() {
        XCTAssertFalse(EditorFileFormat.writable.contains(.jpeg))
        XCTAssertTrue(EditorFileFormat.writable.contains(.unfoldSource))
        XCTAssertTrue(EditorFileFormat.writable.contains(.png))
        XCTAssertTrue(EditorFileFormat.writable.contains(.gif))
    }

    func test_matching_isCaseInsensitive_andTreatsJPGAsJPEG() {
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "JPG"), .jpeg)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "jpeg"), .jpeg)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "PISKEL"), .unfoldSource)
        XCTAssertEqual(EditorFileFormat.matching(fileExtension: "png"), .png)
    }

    func test_matching_returnsNil_forAnUnknownExtension() {
        XCTAssertNil(EditorFileFormat.matching(fileExtension: "bmp"))
        XCTAssertNil(EditorFileFormat.matching(fileExtension: ""))
    }

    func test_everyFormatHasANonEmptyExtensionAndDisplayName() {
        for format in EditorFileFormat.allCases {
            XCTAssertFalse(format.fileExtension.isEmpty, "\(format)")
            XCTAssertFalse(format.displayName.isEmpty, "\(format)")
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter EditorFileFormatTests 2>&1 | tail -20
```

기대: 컴파일 실패, `cannot find 'EditorFileFormat' in scope`

- [ ] **Step 3: 구현**

`Sources/Unfold/CharacterEditor/EditorFileFormat.swift`:

```swift
import Foundation
import UniformTypeIdentifiers

/// Every file type the editor can open or write.
///
/// The File menu and the open/save panels are built from this table rather
/// than from hard-coded lists, so adding a format is one case here plus its
/// codec. Nothing in this type knows how to decode anything — it answers
/// only "what is this format called, and may we read or write it".
enum EditorFileFormat: String, CaseIterable, Equatable {
    /// The editor's own document: layers, frames and per-layer opacity all
    /// survive a round trip. This is the app's own format, with its own
    /// `.unf` extension — the bytes underneath are still the Piskel v2 JSON
    /// schema, which is what keeps packages written by earlier versions
    /// (saved as `.piskel`) readable, but that is an implementation detail
    /// invisible to the user.
    case unfoldSource
    /// A horizontal sprite sheet, one row of `frameCount` frames.
    case png
    /// An animated GIF. Write-only for now; import is deferred.
    case gif
    /// Read-only: no alpha channel, and lossy compression destroys pixel edges.
    case jpeg

    /// Extension used when saving. Import also accepts `jpg` — see `matching`.
    var fileExtension: String {
        switch self {
        case .unfoldSource: return "unf"
        case .png: return "png"
        case .gif: return "gif"
        case .jpeg: return "jpeg"
        }
    }

    var displayName: String {
        switch self {
        case .unfoldSource: return "Unfold Document"
        case .png: return "PNG Sprite Sheet"
        case .gif: return "Animated GIF"
        case .jpeg: return "JPEG Image"
        }
    }

    var canRead: Bool {
        switch self {
        case .unfoldSource, .png, .jpeg: return true
        case .gif: return false
        }
    }

    var canWrite: Bool {
        switch self {
        case .unfoldSource, .png, .gif: return true
        case .jpeg: return false
        }
    }

    /// `.unf` is not a registered system type, so this falls back to a
    /// dynamic UTI. That is enough for an open/save panel to filter on the
    /// extension, which is all this is used for.
    var utType: UTType {
        switch self {
        case .unfoldSource: return UTType(filenameExtension: "unf") ?? .data
        case .png: return .png
        case .gif: return .gif
        case .jpeg: return .jpeg
        }
    }

    static var readable: [EditorFileFormat] { allCases.filter(\.canRead) }
    static var writable: [EditorFileFormat] { allCases.filter(\.canWrite) }

    /// Picks a *starting guess* from the file name. The decoders verify the
    /// actual bytes, so a mislabelled file is caught there, not here.
    static func matching(fileExtension: String) -> EditorFileFormat? {
        let normalised = fileExtension.lowercased()
        if normalised == "jpg" { return .jpeg }
        return allCases.first { $0.fileExtension == normalised }
    }
}
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter EditorFileFormatTests 2>&1 | tail -10
```

기대: `Executed 5 tests, with 0 failures`

- [ ] **Step 5: 커밋**

```bash
git add Sources/Unfold/CharacterEditor/EditorFileFormat.swift Tests/UnfoldTests/EditorFileFormatTests.swift
git commit -m "feat(editor): add the file format registry"
```

---

## Task 3: `EditorDocumentOrigin`

Save가 목적지를 물어야 하는지 판정하는 타입.

**Files:**
- Create: `Sources/Unfold/CharacterEditor/EditorDocumentOrigin.swift`
- Test: `Tests/UnfoldTests/EditorDocumentOriginTests.swift`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/UnfoldTests/EditorDocumentOriginTests.swift`:

```swift
import XCTest
@testable import Unfold

final class EditorDocumentOriginTests: XCTestCase {

    private let url = URL(fileURLWithPath: "/tmp/character.piskel")

    func test_newDocument_cannotSaveInPlace_soSaveMustAskForADestination() {
        XCTAssertFalse(EditorDocumentOrigin.unsaved.canSaveInPlace)
    }

    func test_fileOrigin_canSaveInPlace_whenTheFormatIsWritable() {
        XCTAssertTrue(EditorDocumentOrigin.file(url, .unfoldSource).canSaveInPlace)
        XCTAssertTrue(EditorDocumentOrigin.file(url, .png).canSaveInPlace)
    }

    /// A document opened from a JPEG has nowhere to save back to: writing
    /// JPEG is not supported, so Save has to fall through to Save As.
    func test_fileOrigin_cannotSaveInPlace_whenTheFormatIsReadOnly() {
        XCTAssertFalse(EditorDocumentOrigin.file(url, .jpeg).canSaveInPlace)
    }

    func test_characterOrigin_canSaveInPlace() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertTrue(EditorDocumentOrigin.character(id: "user-1", revision: revision).canSaveInPlace)
    }

    func test_fileURL_isOnlySetForFileOrigins() throws {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .png).fileURL, url)
        XCTAssertNil(EditorDocumentOrigin.unsaved.fileURL)
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertNil(EditorDocumentOrigin.character(id: "user-1", revision: revision).fileURL)
    }

    func test_characterID_isOnlySetForCharacterOrigins() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertEqual(EditorDocumentOrigin.character(id: "user-1", revision: revision).characterID, "user-1")
        XCTAssertNil(EditorDocumentOrigin.file(url, .png).characterID)
        XCTAssertNil(EditorDocumentOrigin.unsaved.characterID)
    }

    // MARK: what a plain Save does — the three origins

    func test_saveAction_writesTheFile_forAWritableFileOrigin() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .png).saveAction, .writeFile(url, .png))
    }

    func test_saveAction_writesTheLibraryPackage_forACharacterOrigin() throws {
        let directory = try makePackage()
        let revision = try EditorPackageRevision.read(at: directory)
        XCTAssertEqual(EditorDocumentOrigin.character(id: "user-1", revision: revision).saveAction,
                       .writeLibraryPackage(id: "user-1"))
    }

    func test_saveAction_asksForADestination_forANewDocument() {
        XCTAssertEqual(EditorDocumentOrigin.unsaved.saveAction, .askForDestination)
    }

    /// A JPEG import has a file, but not one that can be written back.
    func test_saveAction_asksForADestination_whenTheOriginFormatIsReadOnly() {
        XCTAssertEqual(EditorDocumentOrigin.file(url, .jpeg).saveAction, .askForDestination)
    }

    /// `EditorPackageRevision.read` needs all three package files present.
    private func makePackage() throws -> URL {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("origin-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: directory) }
        for name in [Constants.characterEditorSourceFileName,
                     Constants.characterSpriteSheetFileName,
                     Constants.characterManifestFileName] {
            try Data("x".utf8).write(to: directory.appendingPathComponent(name))
        }
        return directory
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter EditorDocumentOriginTests 2>&1 | tail -20
```

기대: 컴파일 실패, `cannot find 'EditorDocumentOrigin' in scope`

- [ ] **Step 3: 구현**

`Sources/Unfold/CharacterEditor/EditorDocumentOrigin.swift`:

```swift
import Foundation

/// Where the open document came from, and therefore what a plain Save does.
///
/// One `⌘S` has to behave correctly in two different contexts: a document
/// opened from disk saves back to its file, a character opened from the
/// library saves back into its package. Carrying the origin on the session
/// is what lets a single command do both without the two paths knowing
/// about each other.
enum EditorDocumentOrigin: Equatable {
    /// A brand-new document that has never been written anywhere.
    case unsaved
    case file(URL, EditorFileFormat)
    case character(id: String, revision: EditorPackageRevision)

    /// What a plain Save has to do. Kept here rather than in the window
    /// controller so the three cases are testable without a window.
    enum SaveAction: Equatable {
        case writeFile(URL, EditorFileFormat)
        case writeLibraryPackage(id: String)
        case askForDestination
    }

    var saveAction: SaveAction {
        switch self {
        case .character(let id, _): return .writeLibraryPackage(id: id)
        case .file(let url, let format) where format.canWrite: return .writeFile(url, format)
        case .unsaved, .file: return .askForDestination
        }
    }

    /// False when Save has to ask the user for a destination first — either
    /// because there isn't one yet, or because the origin's format cannot be
    /// written back (a JPEG import).
    var canSaveInPlace: Bool { saveAction != .askForDestination }

    var fileURL: URL? {
        if case .file(let url, _) = self { return url }
        return nil
    }

    var characterID: String? {
        if case .character(let id, _) = self { return id }
        return nil
    }
}
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter EditorDocumentOriginTests 2>&1 | tail -10
```

기대: `Executed 10 tests, with 0 failures`

- [ ] **Step 5: 커밋**

```bash
git add Sources/Unfold/CharacterEditor/EditorDocumentOrigin.swift Tests/UnfoldTests/EditorDocumentOriginTests.swift
git commit -m "feat(editor): add the document origin type"
```

---

## Task 4: `piskelJSON` → `sourceJSON`

와이어 포맷은 이제 완전히 내부용이다. `PixelDocumentCodec.savePayload` 가 딕셔너리를 만들어 곧바로 `EditorSavePayload.decode` 로 넘기는 것이 유일한 생산자다. 따라서 키 이름을 바꿔도 외부 호환성 문제가 없다.

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/EditorSavePayload.swift:57-58,68,95,122,162`
- Modify: `Sources/Unfold/CharacterEditor/PixelDocumentCodec.swift:144`
- Modify: `Sources/Unfold/CharacterEditor/CharacterPackageWriter.swift:63`
- Modify: `Tests/UnfoldTests/EditorSavePayloadTests.swift:17,36,220-221`
- Modify: `Tests/UnfoldTests/CharacterPackageWriterTests.swift:42`

- [ ] **Step 1: 유일한 생산자임을 확인**

```bash
grep -rn "EditorSavePayload.decode" --include="*.swift" Sources/ Tests/
```

기대: `PixelDocumentCodec.swift` 한 곳과 테스트들만. 프로덕션 생산자가 하나임을 확인한 뒤 진행한다.

- [ ] **Step 2: 프로덕션 코드 치환**

```bash
sed -i '' 's/piskelJSON/sourceJSON/g' \
  Sources/Unfold/CharacterEditor/EditorSavePayload.swift \
  Sources/Unfold/CharacterEditor/PixelDocumentCodec.swift \
  Sources/Unfold/CharacterEditor/CharacterPackageWriter.swift
```

- [ ] **Step 3: 테스트 치환**

```bash
sed -i '' 's/piskelJSON/sourceJSON/g' \
  Tests/UnfoldTests/EditorSavePayloadTests.swift \
  Tests/UnfoldTests/CharacterPackageWriterTests.swift
sed -i '' 's/test_decode_emptyPiskelJSON_isRejected/test_decode_emptySourceJSON_isRejected/' \
  Tests/UnfoldTests/EditorSavePayloadTests.swift
```

- [ ] **Step 4: 남은 오류 메시지 문구 수정**

`Sources/Unfold/CharacterEditor/EditorSavePayload.swift` 의 `emptySource` 케이스 문구를 포맷 중립으로 바꾼다.

```swift
            case .emptySource:
                return "the editor produced an empty source document"
```

- [ ] **Step 5: 테스트**

```bash
swift test 2>&1 | tail -3
```

기대: `0 failures`. Task 1에서 기록한 개수와 같아야 한다.

- [ ] **Step 6: 잔여 확인**

```bash
grep -rn "piskelJSON" --include="*.swift" Sources/ Tests/
```

기대: 출력 없음

- [ ] **Step 7: 커밋**

```bash
git add -A
git commit -m "refactor(editor): rename the save payload's source field

The wire format is internal now — PixelDocumentCodec.savePayload is its
only producer — so the field can say what it holds rather than naming the
app the schema came from."
```

---

## Task 5: 윈도 컨트롤러에 출처 도입

`editingCharacterID` 와 `editingRevision` 두 변수를 하나의 `origin` 으로 대체하고, Save / Save As 를 나눈다.

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift`

- [ ] **Step 1: 상태 변수 교체**

`CharacterEditorWindowController.swift:15-16` 의 두 줄을 하나로 바꾼다.

```swift
    private var origin: EditorDocumentOrigin = .unsaved
```

- [ ] **Step 2: `open` 시그니처 변경**

`open(document:characterID:revision:)` 을 아래로 바꾼다.

```swift
    private func open(document: PixelDocument, origin: EditorDocumentOrigin) {
        teardown()
        let model = PixelEditorModel(document: document)
        self.model = model
        self.origin = origin
        let view = PixelEditorView(model: model,
            saveToLibrary: { [weak self] in _ = self?.saveToLibrary() },
            openDocument: { [weak self] in self?.openDocument() },
            save: { [weak self] in _ = self?.save() },
            saveAs: { [weak self] in self?.saveAs() })
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1120, height: 780),
            styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "\(document.name) — Pixel Editor"
        window.contentView = NSHostingView(rootView: view)
        window.minSize = NSSize(width: 920, height: 660)
        window.isReleasedWhenClosed = false
        window.delegate = self
        window.center()
        self.window = window
        // @Published sends before mutation; update the window flag next turn.
        observation = model.objectWillChange.sink { [weak self] _ in
            Task { @MainActor [weak self] in
                self?.window?.isDocumentEdited = self?.model?.isDirty ?? false
            }
        }
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
```

- [ ] **Step 3: 호출부 세 곳 갱신**

`createNewCharacter`:

```swift
    func createNewCharacter() {
        guard mayReplaceSession() else { return }
        open(document: PixelDocument(), origin: .unsaved)
    }
```

`edit(character:)` 의 마지막 두 줄:

```swift
            guard mayReplaceSession() else { return }
            open(document: document, origin: .character(id: character.id, revision: revision))
```

`edit(character:)` 첫 분기의 `editingCharacterID == character.id` 를 바꾼다.

```swift
        if window != nil, origin.characterID == character.id {
```

`teardown` 의 두 줄을 하나로:

```swift
        origin = .unsaved
```

- [ ] **Step 4: `save` 를 `saveToLibrary` 로 개명하고 출처를 쓰게 한다**

기존 `save()` 를 아래로 바꾼다. 라이브러리 쓰기 로직 자체는 그대로다 — 읽는 곳만 `origin` 으로 옮겼다.

```swift
    @discardableResult
    private func saveToLibrary() -> Bool {
        guard let model, !isSaving else { return false }
        model.endStroke()
        isSaving = true
        defer { isSaving = false }
        let name: String
        if origin.characterID == nil {
            guard let entered = promptForName(defaultName: model.document.name) else { return false }
            name = entered
        } else { name = model.document.name }
        do {
            var document = model.document
            document.name = name
            let payload = try PixelDocumentCodec.savePayload(document, characterID: origin.characterID)
            if case .character(let id, let expected) = origin {
                guard let directory = library.packageDirectory(id: id),
                      let current = try? EditorPackageRevision.read(at: directory), current == expected else {
                    throw PixelDocumentCodec.Failure.invalid("This character was changed or deleted outside this editor. Save your work to a file first, then reopen the character. The library has not been overwritten.")
                }
            }
            let character = try CharacterPackageWriter.write(payload: payload, name: name, into: library)
            // Subsequent saves update the same package rather than duplicating it.
            if let directory = library.packageDirectory(id: character.id),
               let revision = try? EditorPackageRevision.read(at: directory) {
                origin = .character(id: character.id, revision: revision)
            }
            model.change { $0.name = name }
            model.markSaved()
            window?.title = "\(name) — Pixel Editor"
            window?.isDocumentEdited = false
            onCharacterSaved(character)
            return true
        } catch {
            present(error: error)
            return false
        }
    }
```

- [ ] **Step 5: `mayReplaceSession` 의 저장 분기를 갱신**

```swift
        case .alertFirstButtonReturn: return saveToLibrary()
```

- [ ] **Step 6: `save` 와 `saveAs` 추가**

`export(piskel:)` 을 삭제하고 아래 두 메서드를 넣는다.

```swift
    /// Writes back to wherever the document came from. Falls through to
    /// Save As when there is no writable destination yet.
    @discardableResult
    private func save() -> Bool {
        guard let model else { return false }
        model.endStroke()
        switch origin.saveAction {
        case .writeLibraryPackage:
            return saveToLibrary()
        case .writeFile(let url, let format):
            do {
                try write(model.document, to: url, format: format)
                model.markSaved()
                return true
            } catch {
                present(error: error)
                return false
            }
        case .askForDestination:
            saveAs()
            return false
        }
    }

    private func saveAs() {
        guard let model else { return }
        model.endStroke()
        let panel = NSSavePanel()
        let formats = EditorFileFormat.writable
        panel.allowedContentTypes = formats.map(\.utType)
        panel.nameFieldStringValue = "\(model.document.name).\(EditorFileFormat.unfoldSource.fileExtension)"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let format = EditorFileFormat.matching(fileExtension: url.pathExtension) ?? .unfoldSource
        guard format.canWrite else {
            present(error: PixelDocumentCodec.Failure.invalid("\(format.displayName) files cannot be written."))
            return
        }
        do {
            try write(model.document, to: url, format: format)
            origin = .file(url, format)
            model.markSaved()
            window?.title = "\(model.document.name) — Pixel Editor"
        } catch { present(error: error) }
    }

    /// Single write path for every file format, so Save and Save As cannot
    /// drift apart.
    private func write(_ document: PixelDocument, to url: URL, format: EditorFileFormat) throws {
        let data: Data
        switch format {
        case .unfoldSource: data = try PixelDocumentCodec.encode(document)
        case .png: data = try PixelDocumentCodec.sheetPNG(document)
        case .gif: throw PixelDocumentCodec.Failure.invalid("GIF export is not implemented yet.")
        case .jpeg: throw PixelDocumentCodec.Failure.invalid("JPEG files cannot be written.")
        }
        try data.write(to: url, options: .atomic)
    }
```

`write` 의 `.gif` 케이스는 Task 14에서 채운다. 지금 던지는 오류는 이 단계에서 도달할 수 없다 — `saveAs` 가 `EditorFileFormat.writable` 에서 고르게 하지만 GIF 인코더가 아직 없기 때문이다. Task 14 전까지 GIF를 고르면 이 메시지가 뜬다.

- [ ] **Step 7: `importDocument` 를 `openDocument` 로 개명**

기존 `importDocument()` 를 아래로 바꾼다. 포맷 판별을 레지스트리에 위임한다. 픽셀 디코딩 일반화는 Task 10에서 한다.

```swift
    private func openDocument() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = EditorFileFormat.readable.map(\.utType)
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let format = EditorFileFormat.matching(fileExtension: url.pathExtension) ?? .unfoldSource
            let document: PixelDocument
            switch format {
            case .unfoldSource:
                document = try PixelDocumentCodec.load(from: url)
            case .png, .jpeg:
                let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
                guard size <= Constants.editorMaxSheetDataURLBytes else {
                    throw PixelDocumentCodec.Failure.invalid("The image is too large.")
                }
                document = try PixelDocumentCodec.importPNG(Data(contentsOf: url))
            case .gif:
                throw PixelDocumentCodec.Failure.invalid("Opening GIF files is not supported yet.")
            }
            guard mayReplaceSession() else { return }
            open(document: document, origin: .file(url, format))
        } catch { present(error: error) }
    }
```

- [ ] **Step 8: 빌드**

```bash
swift build 2>&1 | tail -20
```

기대: `PixelEditorView` 의 이니셜라이저 인자 이름이 아직 안 맞아 실패한다. Task 6에서 맞춘다. 이 단계에서는 오류가 `PixelEditorView` 관련 한 종류인지만 확인한다.

---

## Task 6: File 메뉴 재구성

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/PixelEditorView.swift:7-10,46-69`

- [ ] **Step 1: 클로저 프로퍼티 교체**

`PixelEditorView.swift:7-10` 을 바꾼다.

```swift
    let saveToLibrary: () -> Void
    let openDocument: () -> Void
    let save: () -> Void
    let saveAs: () -> Void
```

- [ ] **Step 2: 툴바 메뉴 교체**

`toolbar` 의 `Menu("File")` 블록과 저장 버튼을 바꾼다.

```swift
            Menu("File") {
                Button("Open…", action: openDocument).keyboardShortcut("o")
                Divider()
                Button("Save", action: save)
                Button("Save As…", action: saveAs)
                    .keyboardShortcut("s", modifiers: [.command, .shift])
                Divider()
                Button(Strings.Editor.saveButton, action: saveToLibrary)
            }.frame(width: 70)
```

`⌘S` 는 아래 주 버튼이 이미 갖고 있으므로 메뉴 항목에는 붙이지 않는다 — 같은 단축키를 두 곳에 두면 SwiftUI가 어느 쪽을 발화할지 보장하지 않는다.

- [ ] **Step 3: 주 저장 버튼을 `save` 로 연결**

`toolbar` 마지막의 버튼을 바꾼다.

```swift
            Button("Save", action: save)
                .keyboardShortcut("s").buttonStyle(.borderedProminent).tint(.orange)
```

- [ ] **Step 4: 빌드**

```bash
swift build 2>&1 | tail -10
```

기대: `Build complete!`

- [ ] **Step 5: 전체 테스트**

```bash
swift test 2>&1 | tail -3
```

기대: `0 failures`

- [ ] **Step 6: 앱을 띄워 메뉴를 눈으로 확인**

```bash
swift build && ./Scripts/make-app-bundle.sh debug && open "build/Spine Keepet.app"
```

확인 항목: File 메뉴가 `Open… / Save / Save As… / Save to Spine Keepet` 네 항목이고 Piskel이라는 단어가 없다.

- [ ] **Step 7: 커밋**

```bash
git add -A
git commit -m "feat(editor): give the editor open/save/save-as

Save now writes back to wherever the document came from — a file or a
library character — instead of always meaning 'add to the library'. The
menu no longer names the app the file schema came from."
```

**→ Phase D 완료. 사용자에게 보고한다.**

---

# Phase A — 범용 열기와 크기

## Task 7: 캔버스 범위와 총량 상한

**Files:**
- Modify: `Sources/Unfold/Support/Constants.swift:112-118,138-143`

- [ ] **Step 1: 상수 교체**

`Constants.swift` 의 `editorCanvasSideRange` 주석과 값을 바꾼다.

```swift
    /// Longest side, in pixels, of a user-drawn frame. The lower bound keeps
    /// a canvas big enough to draw on; the upper bound is what the import
    /// dialog crops or splits down to.
    static let editorCanvasSideRange = 8...512

    /// Ceiling on a document's total pixel storage
    /// (`width × height × frameCount × layers × 4` bytes).
    ///
    /// The side bound alone is not enough: at 512px a single frame-layer
    /// costs 1 MB, so 24 frames × 16 layers would be 384 MB. But 512px only
    /// exists so a large imported image can be cropped down rather than
    /// rejected outright — the character is ultimately drawn at
    /// `characterDisplaySize` (192pt), so nobody legitimately needs to
    /// *animate* a full 512×512 canvas across 24 frames and 16 layers. This
    /// constrains the product instead, which keeps `editorMaxSheetDataURLBytes`
    /// and `PixelDocumentCodec.maximumSourceBytes` sane.
    ///
    /// 24 MiB is exactly the old maximum: 128×128 × 24 × 16 × 4 =
    /// 25,165,824 bytes = 24 × 1024 × 1024. `exceedsByteCeiling` uses `>`,
    /// not `>=`, so every document that was legal before this constant
    /// existed is still legal, sitting exactly on the boundary.
    static let editorMaxDocumentBytes = 24 * 1024 * 1024
```

> **후기(구현 중 정정):** 처음엔 64MB로 뒀었다. 384MB를 막으면서 기존 최대치
> 24MB를 넉넉히 포함하는 값이라 합리적으로 보였지만, Task 8 검증 단계에서
> 512px·24프레임 문서를 노이즈(비압축) 픽셀로 채워 실측하니 `sheetPNG` 가 약
> 20.8MB, `encode()` 결과가 약 55.5MB로 나와 각각 `editorMaxSheetDataURLBytes`
> (8MB)와 `maximumSourceBytes`(48MB)를 넘었다. 세 상수가 서로 안 맞았다.
> 결정: 총량 상한을 64MB가 아니라 **24MB**로 내려 곱을 제약하고(위 코드가 그
> 최종값이다), 시트 상한을 32MB로 올린다(Step 2). 소스 상한 48MB는 그대로 둔다.

- [ ] **Step 2: 스프라이트 시트 상한을 문서 총량 상한에서 유도**

`editorMaxSheetDataURLBytes` 를 8MB에서 32MB로 올리고, 주석도 128×128 근거 대신
`editorMaxDocumentBytes` 에서 유도되도록 다시 쓴다. 시트는 레이어를 합성해 만든
단일 이미지이므로, 최악의 경우(레이어 1장이 총량 예산을 전부 쓰는 경우) 원시
크기가 `editorMaxDocumentBytes` 와 같다. 비압축에 가까운 아트(디더링, 노이즈,
그라데이션)는 PNG로도 그 크기 그대로 나오므로, 24MB에 약 30% 여유를 더해 32MB로
잡는다.

```swift
    /// Ceiling on the base64 sprite-sheet string the editor may send.
    ///
    /// Derived from `editorMaxDocumentBytes`, not independent of it: a sheet
    /// is the document's frames *composited* down to a single layer, so its
    /// raw (uncompressed) size is at most `editorMaxDocumentBytes` — worst
    /// case, a single-layer document spends its whole budget on that one
    /// layer's pixels. Incompressible art (dithering, noise, gradients)
    /// PNG-encodes close to that raw size, so this constant is
    /// `editorMaxDocumentBytes` plus roughly a 30% margin, rounded up.
    static let editorMaxSheetDataURLBytes = 32 * 1024 * 1024
```

`PixelDocumentCodec.maximumSourceBytes`(48MB)는 그대로 둔다. 소스는 레이어당
PNG 한 장을 base64로 담으므로 24MB 픽셀이 4/3배 부풀어 약 32MB, JSON 오버헤드를
더해도 48MB 안에 여유가 있다.

- [ ] **Step 3: 빌드와 테스트**

```bash
swift build 2>&1 | tail -3
swift test 2>&1 | tail -20
```

기대: 캔버스 범위에 의존하던 기존 테스트가 깨질 수 있다. 실패 목록을 기록한다 — Task 8에서 함께 다룬다.

---

## Task 8: 문서 총량 검증

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/PixelDocument.swift:50-55,177-200`
- Test: `Tests/UnfoldTests/PixelDocumentTests.swift`

- [ ] **Step 1: 실패하는 테스트 추가**

`Tests/UnfoldTests/PixelDocumentTests.swift` 끝에 추가한다.

```swift
    func test_init_clampsToTheCanvasLowerBound() {
        let document = PixelDocument(width: 1, height: 3)
        XCTAssertEqual(document.width, Constants.editorCanvasSideRange.lowerBound)
        XCTAssertEqual(document.height, Constants.editorCanvasSideRange.lowerBound)
    }

    func test_init_clampsToTheCanvasUpperBound() {
        let document = PixelDocument(width: 4096, height: 4096)
        XCTAssertEqual(document.width, Constants.editorCanvasSideRange.upperBound)
        XCTAssertEqual(document.height, Constants.editorCanvasSideRange.upperBound)
    }

    /// The previous maximum document must stay legal, or the new ceiling
    /// would break existing characters.
    func test_theOldMaximumDocumentIsStillWithinTheByteCeiling() {
        var document = PixelDocument(width: 128, height: 128)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 128, height: 128), count: document.frameCount)))
        }
        XCTAssertFalse(document.exceedsByteCeiling)
        XCTAssertLessThanOrEqual(document.byteCount, Constants.editorMaxDocumentBytes)
    }

    func test_insertFrame_stopsAtTheByteCeiling() {
        var document = PixelDocument(width: 512, height: 512)
        while document.layers.count < 8 {
            document.layers.append(PixelLayer(name: "L", frames: [PixelFrame(width: 512, height: 512)]))
        }
        // 512*512*4 = 1 MB per frame-layer, 8 layers => 8 MB per frame.
        for _ in 0..<40 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        XCTAssertFalse(document.exceedsByteCeiling)
        XCTAssertLessThanOrEqual(document.byteCount, Constants.editorMaxDocumentBytes)
    }

    func test_resize_isRejectedWhenItWouldCrossTheByteCeiling() {
        var document = PixelDocument(width: 64, height: 64)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 64, height: 64), count: document.frameCount)))
        }
        document.resize(width: 512, height: 512)   // would be 384 MB
        XCTAssertEqual(document.width, 64, "resize past the ceiling must be a no-op")
    }
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter PixelDocumentTests 2>&1 | tail -20
```

기대: `exceedsByteCeiling` 미정의로 컴파일 실패

- [ ] **Step 3: 구현**

`PixelDocument.swift` 의 `init` 을 바꾼다.

```swift
    init(width: Int = Constants.editorDefaultCanvasSide, height: Int = Constants.editorDefaultCanvasSide) {
        let range = Constants.editorCanvasSideRange
        self.width = min(max(width, range.lowerBound), range.upperBound)
        self.height = min(max(height, range.lowerBound), range.upperBound)
        layers = [PixelLayer(name: "Layer 1", frames: [PixelFrame(width: self.width, height: self.height)])]
    }
```

`byteCount` 옆에 추가한다.

```swift
    var exceedsByteCeiling: Bool { byteCount > Constants.editorMaxDocumentBytes }

    /// Bytes this document would occupy with the given geometry, without
    /// building it. Used to reject a growth before it allocates.
    private func projectedBytes(width: Int, height: Int, frames: Int, layers: Int) -> Int {
        width * height * frames * layers * 4
    }
```

`resize` 의 guard를 확장한다.

```swift
    mutating func resize(width newWidth: Int, height newHeight: Int) {
        guard Constants.editorCanvasSideRange.contains(newWidth),
              Constants.editorCanvasSideRange.contains(newHeight),
              projectedBytes(width: newWidth, height: newHeight,
                             frames: frameCount, layers: layers.count) <= Constants.editorMaxDocumentBytes else { return }
```

`insertFrame` 의 guard를 확장한다.

```swift
    mutating func insertFrame(after index: Int, duplicate: Bool) {
        guard frameCount < Constants.editorFrameCountRange.upperBound, (0..<frameCount).contains(index),
              projectedBytes(width: width, height: height,
                             frames: frameCount + 1, layers: layers.count) <= Constants.editorMaxDocumentBytes else { return }
```

- [ ] **Step 4: `addLayer` 도 막는다**

`Sources/Unfold/CharacterEditor/PixelEditorModel.swift` 의 `addLayer` guard를 확장한다.

```swift
    func addLayer() {
        guard document.layers.count < PixelDocument.maximumLayers,
              document.byteCount + document.width * document.height * document.frameCount * 4
                <= Constants.editorMaxDocumentBytes else { return }
```

- [ ] **Step 5: 통과 확인**

```bash
swift test --filter PixelDocumentTests 2>&1 | tail -10
swift test 2>&1 | tail -3
```

기대: 전부 `0 failures`. Task 7에서 기록한 실패가 남아 있으면 여기서 해결한다 — 캔버스 범위를 하드코딩한 테스트는 `Constants.editorCanvasSideRange` 를 쓰도록 고친다.

- [ ] **Step 6: 커밋**

```bash
git add -A
git commit -m "feat(editor): raise the canvas cap to 512px behind a byte ceiling

The side bound alone does not bound memory: at 512px a full 24-frame
16-layer document would be 384 MB. A 64 MB total ceiling constrains the
product of side, frames and layers instead, and excludes nothing that was
legal at the old 128px cap."
```

---

## Task 9: undo 깊이 보장

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/PixelEditorModel.swift:29,59-64`
- Test: `Tests/UnfoldTests/PixelEditorModelTests.swift`

- [ ] **Step 1: 실패하는 테스트 추가**

`Tests/UnfoldTests/PixelEditorModelTests.swift` 끝에 추가한다.

```swift
    /// A maximal document used to overrun the history budget with two
    /// snapshots, leaving exactly one undo step no matter how many edits
    /// the user made.
    func test_undoKeepsAMinimumDepth_onALargeDocument() {
        var document = PixelDocument(width: 128, height: 128)
        while document.frameCount < 24 { document.insertFrame(after: document.frameCount - 1, duplicate: false) }
        while document.layers.count < PixelDocument.maximumLayers {
            document.layers.append(PixelLayer(name: "L", frames: Array(repeating: PixelFrame(width: 128, height: 128), count: document.frameCount)))
        }
        let model = PixelEditorModel(document: document)

        for i in 0..<5 {
            model.beginStroke(at: PixelPoint(x: i, y: 0))
            model.endStroke()
        }

        var depth = 0
        while model.canUndo { model.undo(); depth += 1 }
        XCTAssertEqual(depth, 5, "five edits should leave five undo steps")
    }

    func test_undoStillTrims_beyondTheMinimumDepth() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        for i in 0..<120 {
            model.beginStroke(at: PixelPoint(x: i % 8, y: (i / 8) % 8))
            model.endStroke()
        }
        var depth = 0
        while model.canUndo { model.undo(); depth += 1 }
        XCTAssertLessThanOrEqual(depth, 100, "the hard step cap still applies")
    }
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter PixelEditorModelTests 2>&1 | grep -E "XCTAssertEqual failed|Executed"
```

기대: 첫 테스트가 `("1") is not equal to ("5")` 로 실패

- [ ] **Step 3: 구현**

`PixelEditorModel.swift:29` 을 바꾼다.

```swift
    /// Snapshots are whole documents, but Swift's copy-on-write means the
    /// unchanged frame buffers are shared between them — a stroke only
    /// unshares the one frame it touched. `byteCount` is therefore a large
    /// overestimate of what history actually costs, which is why a minimum
    /// depth is guaranteed before the budget is allowed to trim anything.
    private let historyBudget = 128 * 1024 * 1024
    private let minimumHistoryDepth = 16
```

`trim` 을 바꾼다.

```swift
    private func trim(_ stack: inout [PixelDocument]) {
        var bytes = stack.reduce(0) { $0 + $1.byteCount }
        while stack.count > minimumHistoryDepth && (bytes > historyBudget || stack.count > 100) {
            bytes -= stack.removeFirst().byteCount
        }
    }
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter PixelEditorModelTests 2>&1 | tail -5
swift test 2>&1 | tail -3
```

기대: 전부 `0 failures`

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "fix(editor): stop undo collapsing to one step on large documents

A 24 MB document overran the 32 MB history budget with two snapshots, so
trim cut the stack to a single entry however many edits had been made.
Guarantee a minimum depth first: copy-on-write means the snapshots share
every frame a stroke did not touch, so byteCount vastly overstates what
history costs."
```

---

## Task 10: `RasterImageDecoder`

`PixelDocumentCodec.decodePNG` 는 PNG 시그니처와 IEND 청크 검사를 하드코딩해 JPEG를 받을 수 없다. 포맷별 무결성 검사와 픽셀 추출을 분리한다.

**Files:**
- Create: `Sources/Unfold/CharacterEditor/RasterImageDecoder.swift`
- Modify: `Sources/Unfold/CharacterEditor/PixelDocumentCodec.swift:195-255`
- Test: `Tests/UnfoldTests/RasterImageDecoderTests.swift`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/UnfoldTests/RasterImageDecoderTests.swift`:

```swift
import XCTest
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

final class RasterImageDecoderTests: XCTestCase {

    private func png(width: Int, height: Int, colour: UInt32 = 0xFF0000FF) throws -> Data {
        try PixelDocumentCodec.encodePNG(Array(repeating: colour, count: width * height),
                                         width: width, height: height)
    }

    private func jpeg(width: Int, height: Int) throws -> Data {
        let image = PixelDocumentCodec.image(Array(repeating: UInt32(0xFF0000FF), count: width * height),
                                             width: width, height: height)!
        let data = NSMutableData()
        let destination = CGImageDestinationCreateWithData(data, UTType.jpeg.identifier as CFString, 1, nil)!
        CGImageDestinationAddImage(destination, image, nil)
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        return data as Data
    }

    func test_decodesPNG_withItsPixelsIntact() throws {
        let image = try RasterImageDecoder.decode(try png(width: 4, height: 4), format: .png, maximumSide: 512)
        XCTAssertEqual(image.width, 4)
        XCTAssertEqual(image.height, 4)
        XCTAssertEqual(image.pixels.count, 16)
        XCTAssertEqual(image.pixels[0], 0xFF0000FF)
    }

    func test_decodesJPEG() throws {
        let image = try RasterImageDecoder.decode(try jpeg(width: 8, height: 8), format: .jpeg, maximumSide: 512)
        XCTAssertEqual(image.width, 8)
        XCTAssertEqual(image.height, 8)
        // JPEG is lossy, so only the alpha channel is asserted exactly.
        XCTAssertEqual(image.pixels[0] & 255, 255)
    }

    /// A sprite sheet wider than the canvas cap must decode; the import
    /// dialog is what splits or crops it afterwards.
    func test_decodesASheetWiderThanTheCanvasCap() throws {
        let image = try RasterImageDecoder.decode(try png(width: 512, height: 64), format: .png, maximumSide: 512)
        XCTAssertEqual(image.width, 512)
        XCTAssertEqual(image.height, 64)
    }

    func test_rejectsAnImageBeyondTheGivenMaximumSide() throws {
        XCTAssertThrowsError(
            try RasterImageDecoder.decode(try png(width: 600, height: 8), format: .png, maximumSide: 512))
    }

    func test_rejectsAPNGWithNoEndChunk() throws {
        var data = try png(width: 4, height: 4)
        data.removeLast(12)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .png, maximumSide: 512))
    }

    func test_rejectsBytesThatAreNotTheDeclaredFormat() throws {
        let data = try png(width: 4, height: 4)
        XCTAssertThrowsError(try RasterImageDecoder.decode(data, format: .jpeg, maximumSide: 512))
    }

    // MARK: content wins over the file name

    func test_detectFormat_readsTheBytes_notTheFileName() throws {
        XCTAssertEqual(RasterImageDecoder.detectFormat(try png(width: 4, height: 4)), .png)
        XCTAssertEqual(RasterImageDecoder.detectFormat(try jpeg(width: 8, height: 8)), .jpeg)
    }

    func test_detectFormat_returnsNil_forNonRasterBytes() {
        XCTAssertNil(RasterImageDecoder.detectFormat(Data("{\"modelVersion\":2}".utf8)))
        XCTAssertNil(RasterImageDecoder.detectFormat(Data()))
    }

    /// A JPEG saved with a .png name must still open, as JPEG.
    func test_decodesAMislabelledFile_byItsContent() throws {
        let data = try jpeg(width: 8, height: 8)
        let detected = try XCTUnwrap(RasterImageDecoder.detectFormat(data))
        XCTAssertEqual(detected, .jpeg)
        let image = try RasterImageDecoder.decode(data, format: detected, maximumSide: 512)
        XCTAssertEqual(image.width, 8)
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter RasterImageDecoderTests 2>&1 | tail -20
```

기대: `cannot find 'RasterImageDecoder' in scope`

- [ ] **Step 3: 구현**

`Sources/Unfold/CharacterEditor/RasterImageDecoder.swift`:

```swift
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Turns PNG or JPEG bytes into straight-alpha RGBA pixels.
///
/// Split out of `PixelDocumentCodec` because that type's decoder hard-coded
/// PNG's signature and IEND checks, which JPEG cannot satisfy. Format-specific
/// integrity checks live in `verify`; the pixel extraction below is shared.
enum RasterImageDecoder {

    struct Image: Equatable {
        let pixels: [UInt32]
        let width: Int
        let height: Int
    }

    /// The 12 bytes every complete PNG stream ends with: a zero-length chunk,
    /// the ASCII type `IEND`, and its constant CRC-32. Searched for rather
    /// than compared against the tail, because some encoders append metadata
    /// after it.
    private static let pngEndChunk = Data([0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82])

    /// Identifies a raster format from its leading bytes, so a mislabelled
    /// file opens as what it actually is. Returns nil for anything that is
    /// not a raster import format — a `.unf` document, most obviously.
    static func detectFormat(_ data: Data) -> EditorFileFormat? {
        if data.count >= 8, data.prefix(8) == Data([137, 80, 78, 71, 13, 10, 26, 10]) { return .png }
        if data.count >= 2, data.prefix(2) == Data([0xFF, 0xD8]) { return .jpeg }
        return nil
    }

    static func decode(_ data: Data, format: EditorFileFormat, maximumSide: Int) throws -> Image {
        guard data.count <= Constants.editorMaxSheetDataURLBytes else {
            throw PixelDocumentCodec.Failure.invalid("The image is too large.")
        }
        try verify(data, format: format)

        guard let source = CGImageSourceCreateWithData(data as CFData, nil),
              CGImageSourceGetType(source) as String? == format.utType.identifier,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0, width <= maximumSide * Constants.editorFrameCountRange.upperBound,
              height <= maximumSide,
              CGImageSourceGetStatus(source) == .statusComplete,
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
              image.width == width, image.height == height,
              CGImageSourceGetStatusAtIndex(source, 0) == .statusComplete else {
            throw PixelDocumentCodec.Failure.invalid(
                "Use a complete \(format.displayName) no more than \(maximumSide) pixels tall.")
        }
        return Image(pixels: try pixels(of: image, width: width, height: height), width: width, height: height)
    }

    private static func verify(_ data: Data, format: EditorFileFormat) throws {
        switch format {
        case .png:
            // A stream cut off partway through its compressed data can still
            // decode to a full-sized image, so the IEND check is the reliable
            // backstop for truncation.
            guard data.range(of: pngEndChunk, options: .backwards) != nil else {
                throw PixelDocumentCodec.Failure.invalid("The PNG data looks truncated (no IEND chunk found).")
            }
        case .jpeg:
            guard data.count >= 4, data.prefix(2) == Data([0xFF, 0xD8]),
                  data.suffix(2) == Data([0xFF, 0xD9]) else {
                throw PixelDocumentCodec.Failure.invalid("The JPEG data looks truncated.")
            }
        case .gif, .unfoldSource:
            throw PixelDocumentCodec.Failure.invalid("\(format.displayName) is not a raster import format.")
        }
    }

    /// Preserves straight-alpha samples when ImageIO exposes RGBA directly.
    /// A trip through an 8-bit premultiplied context would otherwise round
    /// low-alpha colour channels even on a native save and reopen.
    private static func pixels(of image: CGImage, width: Int, height: Int) throws -> [UInt32] {
        let order = image.bitmapInfo.intersection(.byteOrderMask)
        if image.bitsPerComponent == 8, image.bitsPerPixel == 32, image.alphaInfo == .last,
           image.colorSpace?.name == CGColorSpace.sRGB,
           order.isEmpty || order == .byteOrder32Big,
           let providerData = image.dataProvider?.data {
            let raw = providerData as Data
            if raw.count >= image.bytesPerRow * height {
                var result = Array(repeating: UInt32(0), count: width * height)
                for y in 0..<height {
                    for x in 0..<width {
                        let offset = y * image.bytesPerRow + x * 4
                        let alpha = UInt32(raw[offset + 3])
                        if alpha > 0 {
                            result[y * width + x] = UInt32(raw[offset]) << 24 | UInt32(raw[offset + 1]) << 16
                                | UInt32(raw[offset + 2]) << 8 | alpha
                        }
                    }
                }
                return result
            }
        }
        var bytes = Array(repeating: UInt8(0), count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width * 4, space: CGColorSpace(name: CGColorSpace.sRGB)!,
                bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue) else { return false }
            context.interpolationQuality = .none
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        guard drawn else { throw PixelDocumentCodec.Failure.invalid("Could not decode the image pixels.") }
        var result = Array(repeating: UInt32(0), count: width * height)
        for index in result.indices {
            let offset = index * 4
            let alpha = UInt32(bytes[offset + 3])
            guard alpha > 0 else { continue }
            func channel(_ i: Int) -> UInt32 { min(255, (UInt32(bytes[offset + i]) * 255 + alpha / 2) / alpha) }
            result[index] = channel(0) << 24 | channel(1) << 16 | channel(2) << 8 | alpha
        }
        return result
    }
}
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter RasterImageDecoderTests 2>&1 | tail -10
swift test 2>&1 | tail -3
```

기대: 전부 `0 failures`. `PixelDocumentCodec.decodePNG` 는 아직 그대로 남아 있고 `.unf` 문서 내부의 Piskel 스키마 청크 디코딩에 계속 쓰인다 — 이 태스크에서는 제거하지 않는다.

- [ ] **Step 5: 커밋**

```bash
git add Sources/Unfold/CharacterEditor/RasterImageDecoder.swift Tests/UnfoldTests/RasterImageDecoderTests.swift
git commit -m "feat(editor): add a raster decoder for PNG and JPEG"
```

---

## Task 11: `ImportOptions`

분할·크롭·축소 결정과 그 적용. UI가 없는 순수 로직이라 따로 테스트한다.

**Files:**
- Create: `Sources/Unfold/CharacterEditor/ImportOptions.swift`
- Test: `Tests/UnfoldTests/ImportOptionsTests.swift`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/UnfoldTests/ImportOptionsTests.swift`:

```swift
import XCTest
@testable import Unfold

final class ImportOptionsTests: XCTestCase {

    private func image(width: Int, height: Int) -> RasterImageDecoder.Image {
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for i in pixels.indices { pixels[i] = UInt32(i % 251) << 24 | 0xFF }
        return RasterImageDecoder.Image(pixels: pixels, width: width, height: height)
    }

    // MARK: suggestion

    func test_squareImageWithinTheCap_isOpenedAsASingleFrame() {
        XCTAssertEqual(ImportOptions.suggestion(width: 64, height: 64), .single)
    }

    /// A ratio of exactly 1 is an integer multiple too — treating it as a
    /// sheet would send every square image through the dialog.
    func test_aRatioOfOneIsNotASheet() {
        XCTAssertEqual(ImportOptions.suggestion(width: 128, height: 128), .single)
    }

    func test_anExportedSheetIsSuggestedForSplitting() {
        XCTAssertEqual(ImportOptions.suggestion(width: 512, height: 64),
                       .split(frameWidth: 64, frameHeight: 64))
    }

    func test_aNonIntegerRatioIsNotASheet() {
        XCTAssertEqual(ImportOptions.suggestion(width: 100, height: 64), .single)
    }

    func test_anOversizedImageIsSuggestedForCropping() {
        guard case .crop(let rect) = ImportOptions.suggestion(width: 1920, height: 1080) else {
            return XCTFail("expected a crop suggestion")
        }
        XCTAssertEqual(rect.width, CGFloat(Constants.editorCanvasSideRange.upperBound))
        XCTAssertEqual(rect.height, CGFloat(Constants.editorCanvasSideRange.upperBound))
    }

    // MARK: apply

    func test_split_producesOneFramePerCell() throws {
        let document = try ImportOptions.apply(.split(frameWidth: 64, frameHeight: 64), to: image(width: 512, height: 64))
        XCTAssertEqual(document.width, 64)
        XCTAssertEqual(document.height, 64)
        XCTAssertEqual(document.frameCount, 8)
        XCTAssertEqual(document.layers.count, 1)
    }

    /// The round trip that fails today: export a sheet, reopen it.
    func test_split_reopensAnExportedSheet() throws {
        var original = PixelDocument(width: 64, height: 64)
        while original.frameCount < 8 { original.insertFrame(after: original.frameCount - 1, duplicate: false) }
        original.layers[0].frames[3].pixels[10] = 0xABCDEF12

        let sheet = try PixelDocumentCodec.sheetPNG(original)
        let decoded = try RasterImageDecoder.decode(sheet, format: .png, maximumSide: 512)
        let reopened = try ImportOptions.apply(.split(frameWidth: 64, frameHeight: 64), to: decoded)

        XCTAssertEqual(reopened.frameCount, 8)
        XCTAssertEqual(reopened.layers[0].frames[3].pixels[10], 0xABCDEF12)
    }

    func test_split_rejectsAFrameSizeThatDoesNotDivideTheImage() {
        XCTAssertThrowsError(try ImportOptions.apply(.split(frameWidth: 30, frameHeight: 64), to: image(width: 512, height: 64)))
    }

    func test_split_rejectsMoreFramesThanTheDocumentAllows() {
        XCTAssertThrowsError(try ImportOptions.apply(.split(frameWidth: 8, frameHeight: 64), to: image(width: 512, height: 64)),
                             "64 frames exceeds the 24-frame maximum")
    }

    func test_crop_takesTheRequestedRegion() throws {
        let source = image(width: 1920, height: 1080)
        let document = try ImportOptions.apply(.crop(CGRect(x: 100, y: 50, width: 128, height: 128)), to: source)
        XCTAssertEqual(document.width, 128)
        XCTAssertEqual(document.height, 128)
        XCTAssertEqual(document.frameCount, 1)
        XCTAssertEqual(document.layers[0].frames[0].pixels[0], source.pixels[50 * 1920 + 100])
    }

    func test_crop_rejectsARegionOutsideTheImage() {
        XCTAssertThrowsError(try ImportOptions.apply(.crop(CGRect(x: 0, y: 0, width: 600, height: 64)), to: image(width: 512, height: 64)))
    }

    func test_single_rejectsAnImageBeyondTheCanvasCap() {
        XCTAssertThrowsError(try ImportOptions.apply(.single, to: image(width: 1920, height: 1080)))
    }

    func test_scaleToFit_bringsAnOversizedImageWithinTheCap() throws {
        let document = try ImportOptions.apply(.scaleToFit, to: image(width: 1920, height: 1080))
        XCTAssertLessThanOrEqual(document.width, Constants.editorCanvasSideRange.upperBound)
        XCTAssertLessThanOrEqual(document.height, Constants.editorCanvasSideRange.upperBound)
        XCTAssertEqual(document.width, 512)
        XCTAssertEqual(document.height, 288)
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter ImportOptionsTests 2>&1 | tail -20
```

기대: `cannot find 'ImportOptions' in scope`

- [ ] **Step 3: 구현**

`Sources/Unfold/CharacterEditor/ImportOptions.swift`:

```swift
import CoreGraphics
import Foundation

/// How an incoming image becomes a document.
///
/// Pure logic with no UI, so the awkward cases — a sheet whose frame size
/// does not divide it, a crop outside the image, more frames than the
/// document allows — are testable without a window.
enum ImportOptions: Equatable {
    /// One frame, at the image's own size.
    case single
    /// A horizontal sprite sheet cut into `frameWidth × frameHeight` cells.
    case split(frameWidth: Int, frameHeight: Int)
    /// A region of the image, in image coordinates with the origin top-left.
    case crop(CGRect)
    /// Nearest-neighbour downscale until both sides fit the canvas cap.
    case scaleToFit

    private static var maximumSide: Int { Constants.editorCanvasSideRange.upperBound }

    /// What the import dialog offers first.
    static func suggestion(width: Int, height: Int) -> ImportOptions {
        // A ratio of 1 is an integer multiple as well, so require at least 2
        // cells — otherwise every square image would look like a sheet.
        if height <= maximumSide, width > height, width % height == 0 {
            let frames = width / height
            if frames >= 2, Constants.editorFrameCountRange.contains(frames) {
                return .split(frameWidth: height, frameHeight: height)
            }
        }
        if width > maximumSide || height > maximumSide {
            let side = min(maximumSide, min(width, height))
            return .crop(CGRect(x: (width - side) / 2, y: (height - side) / 2, width: side, height: side))
        }
        return .single
    }

    static func apply(_ options: ImportOptions, to image: RasterImageDecoder.Image) throws -> PixelDocument {
        switch options {
        case .single:
            try checkCanvas(width: image.width, height: image.height)
            return document(width: image.width, height: image.height, frames: [image.pixels])

        case .split(let frameWidth, let frameHeight):
            try checkCanvas(width: frameWidth, height: frameHeight)
            guard frameWidth > 0, frameHeight > 0,
                  image.width % frameWidth == 0, image.height % frameHeight == 0 else {
                throw PixelDocumentCodec.Failure.invalid(
                    "A \(image.width)×\(image.height) image does not divide into \(frameWidth)×\(frameHeight) frames.")
            }
            let columns = image.width / frameWidth
            let rows = image.height / frameHeight
            let count = columns * rows
            guard Constants.editorFrameCountRange.contains(count) else {
                throw PixelDocumentCodec.Failure.invalid(
                    "That frame size makes \(count) frames; the limit is \(Constants.editorFrameCountRange.upperBound).")
            }
            var frames: [[UInt32]] = []
            for row in 0..<rows {
                for column in 0..<columns {
                    frames.append(region(of: image, x: column * frameWidth, y: row * frameHeight,
                                         width: frameWidth, height: frameHeight))
                }
            }
            return document(width: frameWidth, height: frameHeight, frames: frames)

        case .crop(let rect):
            let x = Int(rect.origin.x), y = Int(rect.origin.y)
            let width = Int(rect.width), height = Int(rect.height)
            try checkCanvas(width: width, height: height)
            guard x >= 0, y >= 0, width > 0, height > 0,
                  x + width <= image.width, y + height <= image.height else {
                throw PixelDocumentCodec.Failure.invalid("That crop region lies outside the image.")
            }
            return document(width: width, height: height,
                            frames: [region(of: image, x: x, y: y, width: width, height: height)])

        case .scaleToFit:
            let longest = max(image.width, image.height)
            guard longest > 0 else { throw PixelDocumentCodec.Failure.invalid("The image is empty.") }
            let factor = min(1.0, Double(maximumSide) / Double(longest))
            let width = max(Constants.editorCanvasSideRange.lowerBound, Int((Double(image.width) * factor).rounded(.down)))
            let height = max(Constants.editorCanvasSideRange.lowerBound, Int((Double(image.height) * factor).rounded(.down)))
            try checkCanvas(width: width, height: height)
            var pixels = Array(repeating: UInt32(0), count: width * height)
            for y in 0..<height {
                let sourceY = min(image.height - 1, y * image.height / height)
                for x in 0..<width {
                    let sourceX = min(image.width - 1, x * image.width / width)
                    pixels[y * width + x] = image.pixels[sourceY * image.width + sourceX]
                }
            }
            return document(width: width, height: height, frames: [pixels])
        }
    }

    private static func checkCanvas(width: Int, height: Int) throws {
        guard Constants.editorCanvasSideRange.contains(width),
              Constants.editorCanvasSideRange.contains(height) else {
            throw PixelDocumentCodec.Failure.invalid(
                "Frames must be \(Constants.editorCanvasSideRange.lowerBound)–\(Constants.editorCanvasSideRange.upperBound) pixels per side.")
        }
    }

    private static func region(of image: RasterImageDecoder.Image, x: Int, y: Int, width: Int, height: Int) -> [UInt32] {
        var pixels = Array(repeating: UInt32(0), count: width * height)
        for row in 0..<height {
            let source = (y + row) * image.width + x
            pixels.replaceSubrange(row * width..<(row * width + width),
                                   with: image.pixels[source..<(source + width)])
        }
        return pixels
    }

    private static func document(width: Int, height: Int, frames: [[UInt32]]) -> PixelDocument {
        var result = PixelDocument(width: width, height: height)
        result.layers[0].frames = frames.map { pixels in
            var frame = PixelFrame(width: width, height: height)
            frame.pixels = pixels
            return frame
        }
        return result
    }
}
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter ImportOptionsTests 2>&1 | tail -10
```

기대: `Executed 13 tests, with 0 failures`

- [ ] **Step 5: 커밋**

```bash
git add Sources/Unfold/CharacterEditor/ImportOptions.swift Tests/UnfoldTests/ImportOptionsTests.swift
git commit -m "feat(editor): add import splitting, cropping and downscaling

Reopening an exported sprite sheet works for the first time: a 512x64
sheet splits back into eight 64x64 frames instead of being rejected for
exceeding the canvas cap."
```

---

## Task 12: 가져오기 대화상자와 배선

**Files:**
- Create: `Sources/Unfold/CharacterEditor/ImportOptionsView.swift`
- Modify: `Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift` (`openDocument`)

- [ ] **Step 1: 대화상자 작성**

`Sources/Unfold/CharacterEditor/ImportOptionsView.swift`:

```swift
import SwiftUI

/// Asks how an image should become a document. Shown only when the answer is
/// not obvious: an oversized image, or one shaped like a sprite sheet.
@MainActor
struct ImportOptionsView: View {
    let imageWidth: Int
    let imageHeight: Int
    let preview: CGImage?
    let confirm: (ImportOptions) -> Void
    let cancel: () -> Void

    @State private var choice: Choice
    @State private var frameWidth: Int
    @State private var frameHeight: Int
    @State private var cropX = 0
    @State private var cropY = 0
    @State private var cropSide: Int

    private enum Choice: String, CaseIterable, Identifiable {
        case split = "Split into frames"
        case crop = "Crop"
        case scale = "Scale to fit"
        var id: String { rawValue }
    }

    init(imageWidth: Int, imageHeight: Int, preview: CGImage?,
         suggestion: ImportOptions,
         confirm: @escaping (ImportOptions) -> Void, cancel: @escaping () -> Void) {
        self.imageWidth = imageWidth
        self.imageHeight = imageHeight
        self.preview = preview
        self.confirm = confirm
        self.cancel = cancel
        let cap = Constants.editorCanvasSideRange.upperBound
        switch suggestion {
        case .split(let width, let height):
            _choice = State(initialValue: .split)
            _frameWidth = State(initialValue: width)
            _frameHeight = State(initialValue: height)
            _cropSide = State(initialValue: min(cap, min(imageWidth, imageHeight)))
        case .crop(let rect):
            _choice = State(initialValue: .crop)
            _frameWidth = State(initialValue: min(cap, imageWidth))
            _frameHeight = State(initialValue: min(cap, imageHeight))
            _cropSide = State(initialValue: Int(rect.width))
        case .single, .scaleToFit:
            _choice = State(initialValue: .scale)
            _frameWidth = State(initialValue: min(cap, imageWidth))
            _frameHeight = State(initialValue: min(cap, imageHeight))
            _cropSide = State(initialValue: min(cap, min(imageWidth, imageHeight)))
        }
    }

    private var isOversized: Bool {
        let cap = Constants.editorCanvasSideRange.upperBound
        return imageWidth > cap || imageHeight > cap
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Import Image").font(.headline)
            Text("\(imageWidth) × \(imageHeight) px")
                .font(.callout).foregroundStyle(.secondary)
            if isOversized {
                Label("Larger than \(Constants.editorCanvasSideRange.upperBound) px per side. Crop or scale it to fit.",
                      systemImage: "exclamationmark.triangle.fill")
                    .font(.callout).foregroundStyle(.orange)
            }
            if let preview {
                Image(decorative: preview, scale: 1)
                    .resizable().interpolation(.none).scaledToFit()
                    .frame(maxWidth: 260, maxHeight: 160)
                    .background(Color(nsColor: .underPageBackgroundColor))
            }
            Picker("", selection: $choice) {
                ForEach(Choice.allCases) { Text($0.rawValue).tag($0) }
            }.pickerStyle(.segmented).labelsHidden()

            switch choice {
            case .split:
                Stepper("Frame width: \(frameWidth) px", value: $frameWidth,
                        in: Constants.editorCanvasSideRange)
                Stepper("Frame height: \(frameHeight) px", value: $frameHeight,
                        in: Constants.editorCanvasSideRange)
                Text(splitSummary).font(.caption).foregroundStyle(.secondary)
            case .crop:
                Stepper("Size: \(cropSide) px", value: $cropSide, in: Constants.editorCanvasSideRange)
                Stepper("Left: \(cropX) px", value: $cropX, in: 0...max(0, imageWidth - cropSide))
                Stepper("Top: \(cropY) px", value: $cropY, in: 0...max(0, imageHeight - cropSide))
            case .scale:
                Text("Scaled with nearest-neighbour sampling, so pixels stay hard-edged.")
                    .font(.caption).foregroundStyle(.secondary)
            }

            HStack {
                Spacer()
                Button("Cancel", action: cancel).keyboardShortcut(.cancelAction)
                Button("Open") { confirm(selected) }.keyboardShortcut(.defaultAction)
            }
        }.padding(24).frame(width: 380)
    }

    private var splitSummary: String {
        guard frameWidth > 0, frameHeight > 0,
              imageWidth % frameWidth == 0, imageHeight % frameHeight == 0 else {
            return "That frame size does not divide the image evenly."
        }
        let count = (imageWidth / frameWidth) * (imageHeight / frameHeight)
        return "\(count) frame\(count == 1 ? "" : "s")"
    }

    private var selected: ImportOptions {
        switch choice {
        case .split: return .split(frameWidth: frameWidth, frameHeight: frameHeight)
        case .crop: return .crop(CGRect(x: cropX, y: cropY, width: cropSide, height: cropSide))
        case .scale: return .scaleToFit
        }
    }
}
```

- [ ] **Step 2: `openDocument` 를 대화상자에 연결**

`CharacterEditorWindowController.swift` 의 `openDocument` 을 아래로 바꾼다.

```swift
    private func openDocument() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = EditorFileFormat.readable.map(\.utType)
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            // The file's bytes decide what it is; the extension only decided
            // whether the panel would offer it. A JPEG named .png still opens.
            let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= PixelDocumentCodec.maximumSourceBytes else {
                throw PixelDocumentCodec.Failure.invalid("The file is too large.")
            }
            let data = try Data(contentsOf: url)
            guard let format = RasterImageDecoder.detectFormat(data) else {
                // Not a raster image, so it must be a source document.
                let document = try PixelDocumentCodec.decode(data)
                guard mayReplaceSession() else { return }
                open(document: document, origin: .file(url, .unfoldSource))
                return
            }
            guard data.count <= Constants.editorMaxSheetDataURLBytes else {
                throw PixelDocumentCodec.Failure.invalid("The image is too large.")
            }
            let image = try RasterImageDecoder.decode(data, format: format,
                                                      maximumSide: Constants.editorCanvasSideRange.upperBound)
            let suggestion = ImportOptions.suggestion(width: image.width, height: image.height)
            if suggestion == .single {
                let document = try ImportOptions.apply(.single, to: image)
                guard mayReplaceSession() else { return }
                open(document: document, origin: .file(url, format))
                return
            }
            presentImportOptions(for: image, suggestion: suggestion, url: url, format: format)
        } catch { present(error: error) }
    }

    private func presentImportOptions(for image: RasterImageDecoder.Image, suggestion: ImportOptions,
                                      url: URL, format: EditorFileFormat) {
        let sheet = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 380, height: 420),
            styleMask: [.titled], backing: .buffered, defer: false)
        let finish: (ImportOptions?) -> Void = { [weak self] options in
            NSApp.stopModal()
            sheet.orderOut(nil)
            guard let self, let options else { return }
            do {
                let document = try ImportOptions.apply(options, to: image)
                guard self.mayReplaceSession() else { return }
                // A raster import always starts a new document: its file is
                // not a source the editor can save layers and frames back to.
                self.open(document: document, origin: format.canWrite ? .file(url, format) : .unsaved)
            } catch { self.present(error: error) }
        }
        let view = ImportOptionsView(
            imageWidth: image.width, imageHeight: image.height,
            preview: PixelDocumentCodec.image(image.pixels, width: image.width, height: image.height),
            suggestion: suggestion,
            confirm: { finish($0) }, cancel: { finish(nil) })
        sheet.contentView = NSHostingView(rootView: view)
        sheet.center()
        NSApp.runModal(for: sheet)
    }
```

- [ ] **Step 3: 빌드와 테스트**

```bash
swift build 2>&1 | tail -10
swift test 2>&1 | tail -3
```

기대: `Build complete!`, `0 failures`

- [ ] **Step 4: 손으로 확인**

```bash
./Scripts/make-app-bundle.sh debug && open "build/Spine Keepet.app"
```

확인 항목:
1. 프레임 8개짜리 문서를 만들고 `Save As…` 로 PNG 저장
2. `Open…` 으로 그 PNG를 다시 연다
3. 가져오기 대화상자가 뜨고 "8 frames"라고 표시된다
4. Open을 누르면 8프레임 문서가 열린다

- [ ] **Step 5: 커밋**

```bash
git add -A
git commit -m "feat(editor): add the import dialog"
```

---

## Task 13: 배율을 문서 기준 100%로

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/PixelEditorModel.swift:12,36`
- Modify: `Sources/Unfold/CharacterEditor/PixelEditorView.swift:95-99`
- Test: `Tests/UnfoldTests/PixelEditorModelTests.swift`

- [ ] **Step 1: 실패하는 테스트 추가**

`Tests/UnfoldTests/PixelEditorModelTests.swift` 끝에 추가한다.

```swift
    func test_aFreshDocumentOpensAtOneHundredPercent() {
        let model = PixelEditorModel(document: PixelDocument())
        XCTAssertEqual(model.zoomPercent, 100)
    }

    func test_aLargeDocumentAlsoOpensAtOneHundredPercent() {
        let model = PixelEditorModel(document: PixelDocument(width: 512, height: 512))
        XCTAssertEqual(model.zoomPercent, 100)
        XCTAssertGreaterThanOrEqual(model.zoom, 1)
    }

    func test_zoomingInDoublesThePercentage() {
        let model = PixelEditorModel(document: PixelDocument(width: 64, height: 64))
        let base = model.zoom
        model.zoomIn()
        XCTAssertEqual(model.zoom, base * 2)
        XCTAssertEqual(model.zoomPercent, 200)
    }

    func test_zoomIsClampedToTheRenderableRange() {
        let model = PixelEditorModel(document: PixelDocument(width: 8, height: 8))
        for _ in 0..<12 { model.zoomIn() }
        XCTAssertLessThanOrEqual(model.zoom, 24)
        for _ in 0..<12 { model.zoomOut() }
        XCTAssertGreaterThanOrEqual(model.zoom, 1)
    }
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter PixelEditorModelTests 2>&1 | tail -20
```

기대: `zoomPercent` / `zoomIn` / `zoomOut` 미정의로 컴파일 실패

- [ ] **Step 3: 구현**

`PixelEditorModel.swift` 의 `zoom` 선언을 바꾼다.

```swift
    /// Screen points per document pixel.
    @Published var zoom = 8
    /// The scale the document opened at. Shown to the user as 100% — a raw
    /// 1:1 pixel scale would render a 64×64 canvas at thumbnail size.
    private(set) var baseZoom = 8
```

`init` 의 zoom 계산을 바꾼다.

```swift
    init(document: PixelDocument) {
        self.document = document
        savedDocument = document
        baseZoom = Self.fittingZoom(width: document.width, height: document.height)
        zoom = baseZoom
    }

    /// Picks an opening scale that keeps the canvas inside a typical editor
    /// viewport. Clamped to the range the canvas can actually render.
    static func fittingZoom(width: Int, height: Int) -> Int {
        let viewport = 560
        let longest = max(width, height, 1)
        return max(1, min(24, viewport / longest))
    }

    var zoomPercent: Int { Int((Double(zoom) / Double(baseZoom) * 100).rounded()) }
    var canZoomIn: Bool { zoom < 24 }
    var canZoomOut: Bool { zoom > 1 }
    func zoomIn() { zoom = min(24, zoom * 2) }
    func zoomOut() { zoom = max(1, zoom / 2) }
```

- [ ] **Step 4: 뷰 갱신**

`PixelEditorView.swift` 의 배율 컨트롤 세 줄을 바꾼다.

```swift
                Button(action: model.zoomOut) { Image(systemName: "minus.magnifyingglass") }
                    .disabled(!model.canZoomOut).help("Zoom out")
                Text("\(model.zoomPercent)%").monospacedDigit().frame(width: 55)
                Button(action: model.zoomIn) { Image(systemName: "plus.magnifyingglass") }
                    .disabled(!model.canZoomIn).help("Zoom in")
```

- [ ] **Step 5: 캔버스 크기 시트의 범위 갱신**

`PixelEditorView.swift` 의 `resizeSheet` 두 Stepper를 바꾼다.

```swift
            Stepper("Width: \(resizeWidth) px", value: $resizeWidth, in: Constants.editorCanvasSideRange)
            Stepper("Height: \(resizeHeight) px", value: $resizeHeight, in: Constants.editorCanvasSideRange)
```

- [ ] **Step 6: 통과 확인**

```bash
swift test 2>&1 | tail -3
```

기대: `0 failures`

- [ ] **Step 7: 손으로 확인**

```bash
swift build && ./Scripts/make-app-bundle.sh debug && open "build/Spine Keepet.app"
```

확인 항목: 새 캐릭터를 열면 배율이 `100%` 로 표시되고, 캔버스 크기가 변경 전과 비슷하게 보인다.

- [ ] **Step 8: 커밋**

```bash
git add -A
git commit -m "feat(editor): show zoom relative to the opening scale

A 64x64 document opened at scale 8 and read '800%'. The opening scale is
now what 100% means, so zooming reads as a multiple of what the user
first saw."
```

**→ Phase A 완료. 사용자에게 보고한다.**

---

# Phase C — GIF 내보내기

## Task 14: `AnimatedGIFEncoder`

**Files:**
- Create: `Sources/Unfold/CharacterEditor/AnimatedGIFEncoder.swift`
- Test: `Tests/UnfoldTests/AnimatedGIFEncoderTests.swift`

- [ ] **Step 1: 실패하는 테스트 작성**

`Tests/UnfoldTests/AnimatedGIFEncoderTests.swift`:

```swift
import XCTest
import ImageIO
import UniformTypeIdentifiers
@testable import Unfold

final class AnimatedGIFEncoderTests: XCTestCase {

    private func document(frames: Int, fps: Double = 12) -> PixelDocument {
        var document = PixelDocument(width: 8, height: 8)
        document.fps = fps
        while document.frameCount < frames {
            document.insertFrame(after: document.frameCount - 1, duplicate: false)
        }
        for index in 0..<document.frameCount {
            document.layers[0].frames[index].pixels[0] = UInt32(index + 1) << 24 | 0xFF
        }
        return document
    }

    func test_encodesEveryFrame() throws {
        let data = try AnimatedGIFEncoder.encode(document(frames: 5))
        let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
        XCTAssertEqual(CGImageSourceGetType(source) as String?, UTType.gif.identifier)
        XCTAssertEqual(CGImageSourceGetCount(source), 5)
    }

    func test_frameDelayComesFromTheDocumentFPS() throws {
        let data = try AnimatedGIFEncoder.encode(document(frames: 3, fps: 10))
        let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
        let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any]
        let gif = try XCTUnwrap(properties?[kCGImagePropertyGIFDictionary] as? [CFString: Any])
        let delay = try XCTUnwrap(gif[kCGImagePropertyGIFUnclampedDelayTime] as? Double)
        XCTAssertEqual(delay, 0.1, accuracy: 0.001)
    }

    /// GIF carries one bit of transparency, so a half-transparent pixel comes
    /// back fully opaque. Callers warn about this before writing.
    func test_reportsWhetherADocumentHasPartialAlpha() {
        var opaque = PixelDocument(width: 4, height: 4)
        opaque.layers[0].frames[0].pixels[0] = 0xFF0000FF
        opaque.layers[0].frames[0].pixels[1] = 0x00000000
        XCTAssertFalse(AnimatedGIFEncoder.hasPartialAlpha(opaque))

        var translucent = PixelDocument(width: 4, height: 4)
        translucent.layers[0].frames[0].pixels[0] = 0xFF000080
        XCTAssertTrue(AnimatedGIFEncoder.hasPartialAlpha(translucent))
    }

    /// A layer opacity below 1 makes composited pixels partially transparent
    /// even when every stored pixel is opaque.
    func test_partialLayerOpacityCountsAsPartialAlpha() {
        var document = PixelDocument(width: 4, height: 4)
        document.layers[0].frames[0].pixels[0] = 0xFF0000FF
        document.layers[0].opacity = 0.5
        XCTAssertTrue(AnimatedGIFEncoder.hasPartialAlpha(document))
    }
}
```

- [ ] **Step 2: 실패 확인**

```bash
swift test --filter AnimatedGIFEncoderTests 2>&1 | tail -20
```

기대: `cannot find 'AnimatedGIFEncoder' in scope`

- [ ] **Step 3: 구현**

`Sources/Unfold/CharacterEditor/AnimatedGIFEncoder.swift`:

```swift
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

/// Writes a document's composited frames as an animated GIF.
///
/// ImageIO does this with no third-party dependency. What it cannot do is
/// carry partial transparency: GIF has a single transparent palette index,
/// so a pixel at alpha 128 comes back at 255. `hasPartialAlpha` is what lets
/// a caller warn before that happens.
enum AnimatedGIFEncoder {

    static func encode(_ document: PixelDocument) throws -> Data {
        let frames = (0..<document.frameCount).map { document.compositedFrame(at: $0) }
        guard !frames.isEmpty else { throw PixelDocumentCodec.Failure.invalid("The document has no frames.") }
        let data = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(
            data, UTType.gif.identifier as CFString, frames.count, nil) else {
            throw PixelDocumentCodec.Failure.invalid("Could not create a GIF.")
        }
        CGImageDestinationSetProperties(destination, [
            kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]
        ] as CFDictionary)
        let delay = 1 / max(1, document.fps)
        for frame in frames {
            guard let image = PixelDocumentCodec.image(frame.pixels, width: document.width, height: document.height) else {
                throw PixelDocumentCodec.Failure.invalid("Could not render a frame.")
            }
            CGImageDestinationAddImage(destination, image, [
                kCGImagePropertyGIFDictionary: [
                    kCGImagePropertyGIFUnclampedDelayTime: delay,
                    kCGImagePropertyGIFDelayTime: delay
                ]
            ] as CFDictionary)
        }
        guard CGImageDestinationFinalize(destination) else {
            throw PixelDocumentCodec.Failure.invalid("Could not encode the GIF.")
        }
        return data as Data
    }

    /// True when any composited pixel is partly transparent, which GIF will
    /// round to fully opaque.
    static func hasPartialAlpha(_ document: PixelDocument) -> Bool {
        for index in 0..<document.frameCount {
            for pixel in document.compositedFrame(at: index).pixels {
                let alpha = pixel & 255
                if alpha > 0 && alpha < 255 { return true }
            }
        }
        return false
    }
}
```

- [ ] **Step 4: 통과 확인**

```bash
swift test --filter AnimatedGIFEncoderTests 2>&1 | tail -10
```

기대: `Executed 4 tests, with 0 failures`

- [ ] **Step 5: 커밋**

```bash
git add Sources/Unfold/CharacterEditor/AnimatedGIFEncoder.swift Tests/UnfoldTests/AnimatedGIFEncoderTests.swift
git commit -m "feat(editor): add an animated GIF encoder"
```

---

## Task 15: GIF 저장 배선과 경고

**Files:**
- Modify: `Sources/Unfold/CharacterEditor/CharacterEditorWindowController.swift` (`write`, `saveAs`)

- [ ] **Step 1: `write` 의 GIF 케이스를 채운다**

Task 5에서 남겨둔 `throw` 를 바꾼다.

```swift
        case .gif: data = try AnimatedGIFEncoder.encode(document)
```

- [ ] **Step 2: 손실 경고를 추가**

`write` 호출 전에 확인하도록 `saveAs` 와 `save` 가 함께 쓰는 게이트를 넣는다.

```swift
    /// Warns once before a save that cannot carry everything the document
    /// holds. Returns false when the user backs out.
    private func confirmLossyWrite(_ document: PixelDocument, format: EditorFileFormat) -> Bool {
        let message: String
        switch format {
        case .gif where AnimatedGIFEncoder.hasPartialAlpha(document):
            message = "GIF stores one bit of transparency. Partly transparent pixels will become fully opaque."
        case .png where document.frameCount > 1:
            message = "The \(document.frameCount) frames will be written as one horizontal sprite sheet."
        default:
            return true
        }
        let alert = NSAlert()
        alert.alertStyle = .warning
        alert.messageText = "Save as \(format.displayName)?"
        alert.informativeText = message
        alert.addButton(withTitle: "Save")
        alert.addButton(withTitle: "Cancel")
        return alert.runModal() == .alertFirstButtonReturn
    }
```

- [ ] **Step 3: `save` 와 `saveAs` 에서 게이트를 호출**

`save` 의 `.writeFile` 케이스:

```swift
        case .writeFile(let url, let format):
            guard confirmLossyWrite(model.document, format: format) else { return false }
            do {
                try write(model.document, to: url, format: format)
                model.markSaved()
                return true
            } catch {
                present(error: error)
                return false
            }
```

`saveAs` 의 쓰기 직전:

```swift
        guard confirmLossyWrite(model.document, format: format) else { return }
        do {
            try write(model.document, to: url, format: format)
```

- [ ] **Step 4: 빌드와 테스트**

```bash
swift build 2>&1 | tail -5
swift test 2>&1 | tail -3
```

기대: `Build complete!`, `0 failures`

- [ ] **Step 5: 손으로 확인**

```bash
./Scripts/make-app-bundle.sh debug && open "build/Spine Keepet.app"
```

확인 항목:
1. 프레임 여러 개를 그리고 `Save As…` → `.gif` 로 저장
2. 반투명 픽셀이 있으면 경고가 뜬다
3. Finder에서 저장된 GIF를 Quick Look으로 열면 애니메이션이 재생된다

- [ ] **Step 6: 커밋**

```bash
git add -A
git commit -m "feat(editor): export animated GIF

Warns first when the document holds partial alpha, which GIF's one-bit
transparency rounds to fully opaque."
```

**→ Phase C 완료. 사용자에게 보고한다.**

---

## 마무리

- [ ] **잔여 Piskel 명칭 확인**

```bash
grep -rn "iskel" --include="*.swift" Sources/ Tests/
```

기대: `source.piskel` 파일명(레거시 읽기 폴백)과 JSON `piskel` 키, 그리고 그 둘을 설명하는 주석만 남는다. `.piskel` 확장자 자체는 더 이상 유지 대상이 아니다 — 사용자 대상 확장자는 `.unf` 이고, `.piskel` 은 열기/저장 패널과 `EditorFileFormat.matching(fileExtension:)` 에서 완전히 제거됐다. UI 문구나 Swift 식별자에 `.piskel` 확장자나 "Piskel"이라는 이름이 남아 있으면 고친다.

- [ ] **파급 검토: 512px 문서를 끝까지 통과시킨다**

설계의 "파급 검토 항목"을 실제로 확인한다. 임시 테스트를 만들어 돌리고 지운다.

```swift
// Tests/UnfoldTests/TempLargeDocumentProbeTests.swift
import XCTest
@testable import Unfold

final class TempLargeDocumentProbeTests: XCTestCase {
    /// A 512px document has to survive the whole save path: sheet PNG,
    /// source encode, and the payload's own size bounds.
    func test_probe_aLargeDocumentSurvivesTheSavePath() throws {
        var document = PixelDocument(width: 512, height: 512)
        while document.frameCount < 8 {
            document.insertFrame(after: document.frameCount - 1, duplicate: false)
        }
        for index in 0..<document.frameCount {
            for i in stride(from: 0, to: 512 * 512, by: 7) {
                document.layers[0].frames[index].pixels[i] = UInt32(i &* 2654435761) | 0xFF
            }
        }
        print("PROBE byteCount = \(document.byteCount / 1024 / 1024) MB")
        let sheet = try PixelDocumentCodec.sheetPNG(document)
        print("PROBE sheet = \(sheet.count / 1024) KB, limit \(Constants.editorMaxSheetDataURLBytes / 1024) KB")
        let source = try PixelDocumentCodec.encode(document)
        print("PROBE source = \(source.count / 1024) KB, limit \(PixelDocumentCodec.maximumSourceBytes / 1024) KB")
        let payload = try PixelDocumentCodec.savePayload(document, characterID: nil)
        print("PROBE payload accepted, frames = \(payload.frameCount)")
    }
}
```

```bash
swift test --filter TempLargeDocumentProbeTests 2>&1 | grep -E "PROBE|error|failed"
rm Tests/UnfoldTests/TempLargeDocumentProbeTests.swift
```

`sheet` 이 `editorMaxSheetDataURLBytes` 를 넘거나 `source` 가 `maximumSourceBytes` 를 넘으면, 그 상수를 올리거나 문서 총량 상한을 내린다. 어느 쪽인지 사용자에게 보고하고 결정을 받는다.

- [ ] **파급 검토: 데스크톱펫 표시**

`Constants.characterDisplaySize` 는 192다. 512px 스프라이트로 캐릭터를 저장한 뒤 데스크톱펫으로 띄워 축소가 자연스러운지 눈으로 확인한다. 픽셀아트가 뭉개지면 `CharacterAnimationView` 의 보간 설정을 확인한다.

- [ ] **전체 테스트**

```bash
swift test 2>&1 | tail -3
```

- [ ] **README 갱신**

`README.md:36,57-58` 이 Piskel 호환성과 `Pixel Editor → Open → source.piskel` 을 언급한다. C# 빌드 기준 문서이므로 이 브랜치에서 고칠지는 사용자에게 확인한다.
