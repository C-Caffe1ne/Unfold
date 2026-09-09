import Foundation

/// Turns a character package (`character.json` + its sprite sheet) into a
/// validated `Character` domain model.
///
/// The same loader — and the same validation — is used for bundled and
/// (eventually) imported packages; only where the `character.json` file
/// comes from differs. Keeping this in one place means `CharacterManager`,
/// `SpriteAnimator`, and the overlay can all assume a `Character` they
/// receive is already well-formed.
enum CharacterPackageLoader {

    enum ValidationError: Error, CustomStringConvertible {
        case emptyID
        case invalidGrid
        case invalidFrameSize
        case noAnimations
        case invalidAnimation(key: String)

        var description: String {
            switch self {
            case .emptyID:
                return "character id must not be empty"
            case .invalidGrid:
                return "spriteSheet.columns and .rows must both be > 0"
            case .invalidFrameSize:
                return "spriteSheet.frameWidth and .frameHeight must both be > 0"
            case .noAnimations:
                return "character defines no animations"
            case .invalidAnimation(let key):
                return "animation \"\(key)\" has no frames, a non-positive fps, or a frame index outside the sprite sheet's grid"
            }
        }
    }

    /// Loads a built-in character package bundled under
    /// `Characters/<id>/character.json` in the app's resource bundle.
    static func loadBuiltIn(id: String) -> Character? {
        guard let manifestURL = Bundle.module.url(
            forResource: "character",
            withExtension: "json",
            subdirectory: "\(Constants.builtInCharactersResourceSubdirectory)/\(id)"
        ) else {
            NSLog("Unfold: no bundled character package for id \"\(id)\"")
            return nil
        }

        do {
            return try load(manifestURL: manifestURL, source: .builtIn)
        } catch {
            NSLog("Unfold: failed to load built-in character \"\(id)\" — \(error)")
            return nil
        }
    }

    /// Loads a user-imported character package from an on-disk directory.
    ///
    /// Not called from anywhere yet — this is the entry point the future
    /// "Import Character" flow (Settings → Characters → Import) will use
    /// once it exists.
    static func loadImported(packageDirectory: URL) throws -> Character {
        let manifestURL = packageDirectory.appendingPathComponent("character.json")
        return try load(manifestURL: manifestURL, source: .imported(packageURL: packageDirectory))
    }

    // MARK: - Shared

    private static func load(manifestURL: URL, source: CharacterSource) throws -> Character {
        let data = try Data(contentsOf: manifestURL)
        let manifest = try JSONDecoder().decode(CharacterManifest.self, from: data)
        return try makeCharacter(from: manifest, source: source)
    }

    private static func makeCharacter(from manifest: CharacterManifest, source: CharacterSource) throws -> Character {
        guard !manifest.id.isEmpty else { throw ValidationError.emptyID }

        let sheet = manifest.spriteSheet
        guard sheet.columns > 0, sheet.rows > 0 else { throw ValidationError.invalidGrid }
        guard sheet.frameWidth > 0, sheet.frameHeight > 0 else { throw ValidationError.invalidFrameSize }
        guard !manifest.animations.isEmpty else { throw ValidationError.noAnimations }

        let validFrameRange = 0..<(sheet.columns * sheet.rows)

        var animations: [AnimationKey: AnimationSource] = [:]
        for (rawKey, dto) in manifest.animations {
            animations[AnimationKey(rawKey)] = try makeAnimationSource(rawKey: rawKey, dto: dto, validFrameRange: validFrameRange)
        }

        return Character(
            id: manifest.id,
            name: manifest.name,
            thumbnailSymbolName: manifest.thumbnailSymbol ?? "questionmark.circle",
            spriteSheet: SpriteSheetDefinition(
                fileName: sheet.file,
                columns: sheet.columns,
                rows: sheet.rows,
                frameWidth: sheet.frameWidth,
                frameHeight: sheet.frameHeight
            ),
            animations: animations,
            renderStyle: makeRenderStyle(from: manifest.renderStyle),
            source: source
        )
    }

    /// A manifest that names a style this build doesn't know still loads —
    /// an older build must never refuse a newer package outright. It's
    /// worth a log line though: unlike an absent field (every package
    /// written before render styles existed, which stays silent), an
    /// unrecognised value is either a corrupted manifest or a format this
    /// build is behind on.
    private static func makeRenderStyle(from raw: String?) -> RenderStyle {
        guard let raw else { return .smooth }
        guard let style = RenderStyle(rawValue: raw) else {
            NSLog("Unfold: unknown renderStyle \"\(raw)\" — falling back to .smooth")
            return .smooth
        }
        return style
    }

    /// A `gif` field takes precedence over `frames`/`fps` when a manifest
    /// entry carries both — a malformed manifest fails predictably on the
    /// GIF path rather than silently picking one ambiguously.
    private static func makeAnimationSource(
        rawKey: String,
        dto: CharacterManifest.AnimationDTO,
        validFrameRange: Range<Int>
    ) throws -> AnimationSource {
        if let gifFileName = dto.gif {
            guard !gifFileName.isEmpty else {
                throw ValidationError.invalidAnimation(key: rawKey)
            }
            return .gif(fileName: gifFileName, loop: dto.loop)
        }

        guard
            let frames = dto.frames, !frames.isEmpty,
            let fps = dto.fps, fps.isFinite, fps > 0,
            frames.allSatisfy({ validFrameRange.contains($0) })
        else {
            throw ValidationError.invalidAnimation(key: rawKey)
        }
        if let durations = dto.frameDurations {
            guard durations.count == frames.count,
                  durations.allSatisfy({ $0.isFinite && (0.01...60).contains($0) }) else {
                throw ValidationError.invalidAnimation(key: rawKey)
            }
        }
        return .spriteSheet(SpriteAnimationDefinition(frames: frames, fps: fps, loop: dto.loop,
                                                       frameDurations: dto.frameDurations))
    }
}
