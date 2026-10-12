using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class PetImportGuidanceTests
{
    [Theory]
    [InlineData("현재 GLB 텍스처는 파일 내부의 PNG만 지원해요.", "PNG", "내부에 포함")]
    [InlineData("GLB에 포함된 하나의 버퍼만 지원해요.", "버퍼 하나", "외부 파일")]
    [InlineData("필수 확장이 없는 glTF 2.0 모델을 사용해 주세요.", "필수 확장", "다시 내보내")]
    [InlineData("압축되지 않은 삼각형 GLB 모델만 지원해요.", "압축", "삼각형")]
    [InlineData("GLB texture transforms and UV sets other than UV0 are not supported.", "UV0", "확장 없이")]
    [InlineData("GLB sparse accessors or mismatched types are not supported.", "Sparse", "일반 데이터")]
    [InlineData("GLB vertex attributes are inconsistent or use more than four joints.", "관절 가중치", "4개 이하")]
    [InlineData("펫 모델은 삼각형 10만 개 이하로 줄여 주세요.", "정점 30만", "삼각형 10만")]
    [InlineData("화면에 표시되는 펫 모델의 삼각형을 10만 개 이하로 줄여 주세요.", "인스턴스", "합계")]
    [InlineData("GLB textures exceed 64.", "64장", "줄여")]
    [InlineData("GLB textures exceed 64 MiB decoded.", "디코딩 합계", "64 MiB")]
    [InlineData("Image dimensions exceed the supported limits.", "2048px", "PNG")]
    [InlineData("Unsupported GLB interpolation.", "LINEAR·STEP·CUBICSPLINE", "다시 내보내")]
    [InlineData("GLB animations exceed 128.", "128개", "동작만 남겨")]
    [InlineData("Invalid GLB skeleton.", "512개 이하", "바인딩")]
    [InlineData("File exceeds the size limit.", "32 MiB", "파일 크기")]
    [InlineData("GLB needs embedded JSON and binary data.", "내장 데이터 구조나 크기", "JSON은 8 MiB 이하")]
    public void KnownGlbReasonsOfferSpecificRecovery(string reason, string detail, string remedy)
    {
        var error = new InvalidDataException(reason); var hint = PetImportGuidance.GlbError(error);
        Assert.Contains(detail, hint); Assert.Contains(remedy, hint);
        Assert.NotEqual(Ui.ErrorText(error), hint);
    }

    [Theory]
    [InlineData("GIF exceeds 32 MiB.", "32 MiB", "다시 내보내")]
    [InlineData("Decoded GIF exceeds the animation budget.", "2048px·512프레임", "128 MiB")]
    [InlineData("Cannot decode the GIF.", "끝까지 읽지", "모든 프레임")]
    [InlineData("Incomplete GIF frame.", "끝까지 읽지", "모든 프레임")]
    [InlineData("Expected a GIF.", "내용이 GIF가 아니", "확장자만 바꾸지")]
    public void OnlyKnownImmediateGifCausesExplainTheFailure(string reason, string detail, string remedy)
    {
        var error = new InvalidDataException(PetImportGuidance.GifDecodeFailure, new InvalidDataException(reason));
        var hint = PetImportGuidance.MediaError(error);
        Assert.Contains(detail, hint); Assert.Contains(remedy, hint);
        Assert.DoesNotContain("손상 여부", hint);
    }

    [Fact]
    public void ActualGifDecoderFailureUsesRecoveryWithoutEchoingTheSourcePath()
    {
        const string privatePath = "/Users/private-owner/secret-pet.gif";
        var error = Assert.Throws<InvalidDataException>(() => ImportedPetClip.FromGif(privatePath, "not a gif"u8.ToArray()));
        var hint = PetImportGuidance.MediaError(error);
        Assert.Contains("모든 프레임", hint); Assert.DoesNotContain(privatePath, hint);
    }

    [Fact]
    public void ActualGlbParserReasonsReachTheLocalMapper()
    {
        var interpolation = Assert.Throws<InvalidDataException>(() => GlbModel.Parse(GlbTests.Fixture(interpolation: "BEZIER")));
        Assert.Contains("LINEAR·STEP·CUBICSPLINE", PetImportGuidance.GlbError(interpolation));
        var time = Assert.Throws<InvalidDataException>(() => GlbModel.Parse(GlbTests.Fixture(duration: 121)));
        Assert.Equal(PetImportGuidance.GlbTimeKeyHint, PetImportGuidance.GlbError(time));
        var required = RewriteGlb(root => root["extensionsRequired"] = new JsonArray("KHR_materials_unlit"));
        var extension = Assert.Throws<InvalidDataException>(() => GlbModel.Parse(required));
        Assert.Contains("필수 확장 없이", PetImportGuidance.GlbError(extension));
    }

    [Fact]
    public void TimeKeyFailureRecognizesTheFixedShapeAndDropsAuthoredAnimationText()
    {
        const string authored = "private animation /Users/private-owner/model.glb ' <script>secret</script>";
        var error = new InvalidDataException($"GLB 애니메이션 '{authored}'의 시간 키가 중복·역순이거나 0~120초 범위를 벗어났어요. 원본에서 시간 키를 정리해 다시 내보내 주세요.");
        var hint = PetImportGuidance.GlbError(error);
        Assert.Equal(PetImportGuidance.GlbTimeKeyHint, hint);
        Assert.DoesNotContain(authored, hint); Assert.DoesNotContain("private-owner", hint); Assert.DoesNotContain("<script>", hint);
        foreach (var message in new[] { error.Message + " appended private text", "GLB 애니메이션 'secret' unknown failure" })
        {
            var unknown = new InvalidDataException(message);
            Assert.Equal(Ui.ErrorText(unknown), PetImportGuidance.GlbError(unknown));
        }
    }

    [Fact]
    public void UnknownExceptionsAndArbitraryInnerErrorsUseSafeFallbacks()
    {
        const string raw = "private model /Users/private-owner/secret.glb unexpected decoder output";
        Exception[] errors = [new InvalidDataException(raw), new InvalidOperationException(raw), new IOException(raw),
            new UnauthorizedAccessException(raw), new FileNotFoundException(raw), new Exception(raw),
            new InvalidDataException(raw, new InvalidDataException("GLB textures exceed 64."))];
        foreach (var error in errors)
        {
            Assert.Equal(Ui.ErrorText(error), PetImportGuidance.GlbError(error));
            Assert.Equal(Ui.ErrorText(error), PetImportGuidance.MediaError(error));
            Assert.DoesNotContain(raw, PetImportGuidance.GlbError(error)); Assert.DoesNotContain(raw, PetImportGuidance.MediaError(error));
        }
        var unknownGif = new InvalidDataException(PetImportGuidance.GifDecodeFailure, new InvalidDataException(raw));
        Assert.Equal(PetImportGuidance.GifDecodeFailure, PetImportGuidance.MediaError(unknownGif));
        var nested = new InvalidDataException(PetImportGuidance.GifDecodeFailure,
            new IOException(raw, new InvalidDataException("GIF exceeds 32 MiB.")));
        Assert.Equal(PetImportGuidance.GifDecodeFailure, PetImportGuidance.MediaError(nested));
        var converted = new InvalidDataException("MP4를 읽지 못했어요. 재생 가능한 영상 파일인지 확인해 주세요.", new IOException(raw));
        Assert.Equal(converted.Message, PetImportGuidance.MediaError(converted));
    }

    [Theory]
    [InlineData("완전히 투명한 프레임이 있어요. 모든 프레임에 펫이 보이는 파일을 선택해 주세요.")]
    [InlineData("완전히 투명한 이미지예요. 펫이 보이는 파일을 선택해 주세요.")]
    [InlineData("펫의 전체 애니메이션이 너무 커요. 이미지 크기나 프레임 수를 줄여 주세요. (최대 128 MiB)")]
    [InlineData("MP4는 10초 이하의 영상으로 선택해 주세요. 파일을 자동으로 자르지는 않아요.")]
    public void SafeExistingMediaValidationRemainsSpecific(string reason)
        => Assert.Equal(reason, PetImportGuidance.MediaError(new InvalidDataException(reason)));

    [Fact]
    public void MissingConversionToolKeepsTheKnownSafeRemedy()
    {
        const string reason = "영상 변환 도구가 없는 앱이에요. MP4 지원 배포본을 사용하거나 GIF 파일을 가져와 주세요.";
        Assert.Equal(reason, PetImportGuidance.MediaError(new InvalidOperationException(reason)));
        Assert.Equal(Ui.ErrorText(new InvalidOperationException(reason)), PetImportGuidance.GlbError(new InvalidOperationException(reason)));
    }

    [Theory]
    [InlineData(" /Users/private-owner/Tools/ffmpeg")]
    [InlineData("\nconverter stderr: /Users/private-owner/secret.mp4")]
    public void ConversionToolMessagesWithUnknownDetailsDoNotExposePrivatePaths(string details)
    {
        const string reason = "영상 변환 도구가 없는 앱이에요. MP4 지원 배포본을 사용하거나 GIF 파일을 가져와 주세요.";
        var error = new InvalidOperationException(reason + details);
        var hint = PetImportGuidance.MediaError(error);
        Assert.Equal(Ui.ErrorText(error), hint);
        Assert.NotEqual(reason, hint); Assert.DoesNotContain("private-owner", hint);
        Assert.DoesNotContain(details, hint);
    }

    [Fact]
    public void MediaSaveHintsExplainOnlyTheCurrentMissingConditions()
    {
        Assert.Equal("펫 이름과 기본 파일을 넣어 주세요.", PetImportGuidance.MediaSaveHint(" ", false, false, false, true));
        Assert.Equal("펫 이름을 입력해 주세요.", PetImportGuidance.MediaSaveHint(null, true, false, false, true));
        Assert.Equal("기본에 파일을 넣어 주세요.", PetImportGuidance.MediaSaveHint("pet", false, false, false, true));
        Assert.Contains("처리 중", PetImportGuidance.MediaSaveHint(null, false, true, false, false));
        Assert.Equal("", PetImportGuidance.MediaSaveHint("pet", true, false, false, false));
        Assert.Equal("저장할 변경 사항이 없어요.", PetImportGuidance.MediaSaveHint("pet", true, false, true, false));
        Assert.Equal("", PetImportGuidance.MediaSaveHint("pet", true, false, true, true));
    }

    [Fact]
    public void GlbSaveHintsExplainOnlyTheCurrentMissingConditions()
    {
        Assert.Equal("펫 이름을 입력하고 GLB 파일을 열어 주세요.", PetImportGuidance.GlbSaveHint(null, false, false, false, false));
        Assert.Equal("GLB 파일을 열어 주세요.", PetImportGuidance.GlbSaveHint("pet", false, false, false, true));
        Assert.Equal("펫 이름을 입력해 주세요.", PetImportGuidance.GlbSaveHint(" ", true, false, false, true));
        Assert.Contains("처리 중", PetImportGuidance.GlbSaveHint(null, false, true, true, false));
        Assert.Equal("", PetImportGuidance.GlbSaveHint("pet", true, false, false, false));
        Assert.Equal("저장할 변경 사항이 없어요.", PetImportGuidance.GlbSaveHint("pet", true, false, true, false));
        Assert.Equal("", PetImportGuidance.GlbSaveHint("pet", true, false, true, true));
    }

    [Fact]
    public void ActionHintsDistinguishRequiredLoopStaticAndUnassignedPreview()
    {
        Assert.Contains("필수이며 반복", PetImportGuidance.GlbActionHint("idle", true, false));
        Assert.Equal("", PetImportGuidance.GlbActionHint("click", true, false));
        var unassigned = PetImportGuidance.GlbActionHint("click", false, false);
        Assert.Contains("새 반응을 배정하지", unassigned); Assert.Contains("미리보기에서는 기본 대기", unassigned);
        Assert.Contains("보통 현재 동작", unassigned);
        var staticIdle = PetImportGuidance.GlbActionHint("idle", true, true);
        Assert.Contains("필수이며 반복", staticIdle); Assert.Contains("애니메이션이 없는 원본은 Static", staticIdle);
        Assert.DoesNotContain("Static은 정지", staticIdle);
        Assert.DoesNotContain("private action", PetImportGuidance.GlbActionHint("private action", false, true));
    }

    [Fact]
    public void PreparationCopyMatchesCurrentFormatPlaybackAndSaveContracts()
    {
        var media = string.Join("\n", PetImportGuidance.PreparationSteps(false));
        var glb = string.Join("\n", PetImportGuidance.PreparationSteps(true));
        Assert.Contains("Aseprite 원본은 보관", media); Assert.Contains("행동별 GIF로", media);
        Assert.Contains("정지 그림은 PNG 같은 지원 이미지로 내보내", media);
        Assert.Contains("Blender 원본은 보관", glb);
        Assert.Contains("PNG 텍스처를 파일 내부에 포함한 GLB 2.0으로 내보내", glb);
        Assert.Equal(32, ImageCodec.MaxFileBytes / (1024 * 1024));
        Assert.Equal(128, ImageCodec.MaxDecodedAnimationBytes / (1024 * 1024));
        Assert.Equal(128, PetMediaImporter.MaxVideoBytes / (1024 * 1024));
        Assert.Equal(512, ImageCodec.MaxPetImageDimension);
        Assert.Contains("한 번·반복·핑퐁", media); Assert.Contains("10ms 이하 지연은 100ms", media);
        Assert.Contains("디코딩 합계", media); Assert.Contains("앱 전체 메모리 사용량이 아니", media);
        Assert.Contains("펫 팩 만들기…", media); Assert.Contains("설치 확인을 취소해도 저장된 팩 파일은 남", media);
        Assert.Contains("필수 확장", glb); Assert.Contains("내부 PNG·UV0", glb);
        Assert.Contains("기본 대기는 필수이며 반복", glb); Assert.Contains("Static 정지 모델", glb);
        Assert.Contains("저장하고 적용은 앱에 설치", glb); Assert.Contains("파일로 보관·공유", glb);
        Assert.Contains("휴식 시작이 비어 있고 걷기가 연결되면", glb);
        var draft = new CustomPetDraft();
        foreach (var mode in new[] { 0, 1, 2 }) { draft.SetPlayback("idle", mode); Assert.Equal(mode, draft.Playback("idle")); }
        Assert.True(GlbPetDraft.RequiredLoop("idle")); Assert.Null(GlbPetDraft.RequiredLoop("click"));
        Assert.Contains("JPG/JPEG", PetImportGuidance.MediaCaption); Assert.Contains("GLB", PetImportGuidance.MediaCaption);
        Assert.Contains(".unfoldpet", PetImportGuidance.MediaCaption); Assert.Contains(".unfoldpet", PetImportGuidance.GlbCaption);
        Assert.Equal(PetImportGuidance.MediaCaption, PetImportGuidance.GlbCaption);
    }

    private static byte[] RewriteGlb(Action<JsonObject> edit)
    {
        var bytes = GlbTests.Fixture(); var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        var root = JsonNode.Parse(bytes.AsSpan(20, jsonLength))!.AsObject(); edit(root);
        var json = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString()); var paddedLength = (json.Length + 3) / 4 * 4;
        var binary = bytes.AsSpan(28 + jsonLength).ToArray();
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write(28 + paddedLength + binary.Length);
        writer.Write(paddedLength); writer.Write(0x4E4F534Au); writer.Write(json);
        for (var i = json.Length; i < paddedLength; i++) writer.Write((byte)' ');
        writer.Write(binary.Length); writer.Write(0x004E4942u); writer.Write(binary); return stream.ToArray();
    }
}
