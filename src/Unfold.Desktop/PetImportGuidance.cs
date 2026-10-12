using Avalonia.Automation;
using Avalonia.Controls;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Local, read-only help for the existing pet import contracts.</summary>
internal static class PetImportGuidance
{
    internal const string MediaCaption = "GIF · MP4 · PNG/JPG/JPEG/WEBP/BMP · GLB · .unfoldpet";
    internal const string GlbCaption = MediaCaption;
    internal const string GifDecodeFailure = "GIF를 읽지 못했어요. 파일 손상 여부와 이미지 크기·프레임 수를 확인해 주세요.";
    internal const string GlbTimeKeyHint = "애니메이션 시간 키가 중복·역순이거나 범위를 벗어났어요. 시간 키를 0~120초 안에서 겹치지 않게 오름차순으로 정리해 다시 내보내 주세요.";
    private const string TimeKeyPrefix = "GLB 애니메이션 '";
    private const string TimeKeySuffix = "'의 시간 키가 중복·역순이거나 0~120초 범위를 벗어났어요. 원본에서 시간 키를 정리해 다시 내보내 주세요.";
    private static string GifBudgetHint => $"GIF의 크기·프레임 수 또는 디코딩 용량이 한도를 넘었어요. 각 변 2048px·512프레임·디코딩 {ImageCodec.MaxDecodedAnimationBytes / (1024 * 1024)} MiB 이하를 모두 만족하도록 줄여 다시 내보내 주세요.";

    public static Control CreateGuide(bool glb)
    {
        var prefix = glb ? "GlbPet" : "CustomPet";
        var caption = Ui.Caption(glb ? GlbCaption : MediaCaption); caption.Name = prefix + "SupportedFormats";
        var body = new StackPanel { Name = prefix + "PreparationContent", Spacing = 8 };
        foreach (var paragraph in PreparationSteps(glb)) body.Children.Add(Ui.Caption(paragraph));
        var guide = Ui.Disclosure("파일 준비 안내", body);
        guide.Name = prefix + "PreparationGuide"; guide.IsExpanded = false;
        AutomationProperties.SetName(guide, "파일 준비 안내");
        var result = Ui.Column(caption, guide); result.Name = prefix + "ImportGuidance";
        return result;
    }

    // Plain text is shared by the control and contract tests; no draft or editor state is captured.
    internal static IReadOnlyList<string> PreparationSteps(bool glb) => glb
        ? [
            "Blender 원본은 보관하고 PNG 텍스처를 파일 내부에 포함한 GLB 2.0으로 내보내 파일 열기…로 가져와 주세요. 파일 열기…는 2D 파일·GLB·.unfoldpet을 자동으로 구분해요. 2D와 GLB는 같은 팩에 섞을 수 없어요.",
            $"GLB 2.0은 {ImageCodec.MaxFileBytes / (1024 * 1024)} MiB 이하, 내부 JSON과 바이너리 버퍼 하나를 사용해요. 외부 파일·필수 확장·압축 메시 없이 삼각형으로 내보내 주세요. 모든 glTF 모델이 호환되는 것은 아니에요.",
            "텍스처는 내부 PNG·UV0를 사용해요. 텍스처 변환 확장은 지원하지 않아요. 각 변 2048px·64장·디코딩 합계 64 MiB 이하, 모델 정점 30만 개·삼각형 10만 개 이하로 줄여 주세요. 화면에 표시되는 인스턴스의 삼각형 합계도 10만 개 이하예요.",
            "애니메이션 시간 키는 0~120초 안에서 겹치지 않게 오름차순으로 정리해 주세요. 기본 대기는 필수이며 반복해요. 선택 행동은 한 번 또는 반복, 속도는 0.25~3배로 설정해요. 애니메이션 없는 모델도 Static 정지 모델로 사용할 수 있어요. 앱에서 새 동작을 만들지는 않아요.",
            "연결 안 함은 새 반응을 배정하지 않아요. 미리보기에서는 기본 대기를 보여 주지만 실제 펫에서는 보통 현재 동작을 유지해요. 휴식 시작이 비어 있고 걷기가 연결되면 휴식 중 걷기로 보완해요. 들기·드래그·놓기 중 일부를 연결하면 저장할 때 나머지는 기본 대기로 채워져요.",
            "저장하고 적용은 앱에 설치하고 현재 펫으로 적용해요. 펫 팩 만들기…는 .unfoldpet 파일로 보관·공유해요. 파일 열기…로 팩을 다시 열면 이름·버전을 확인해 설치할 수 있어요. 파일행의 제거는 초안에서 모델을 빼고, 펫 삭제는 설치된 펫을 삭제해요."
        ]
        : [
            "Aseprite 원본은 보관하고 애니메이션은 행동별 GIF로, 정지 그림은 PNG 같은 지원 이미지로 내보내 파일 열기…로 가져와 주세요. 파일 열기…는 2D 파일·GLB·.unfoldpet을 자동으로 구분해요. 이미지·GIF·MP4는 함께 넣을 수 있지만 GLB와는 같은 팩에 섞을 수 없어요.",
            "펫 이름과 기본 파일은 필수예요. 나머지 행동은 선택이며, 파일이나 제작 도구의 태그 이름으로 자동 배정되지 않아요. 기본을 포함해 한 번·반복·핑퐁을 고를 수 있어요. 없는 반응은 보통 현재 동작을 유지해요.",
            $"GIF는 {ImageCodec.MaxFileBytes / (1024 * 1024)} MiB·각 변 2048px·512프레임 이하이며, 너비×높이×프레임 수×4의 디코딩 용량도 {ImageCodec.MaxDecodedAnimationBytes / (1024 * 1024)} MiB 이하로 맞춰 주세요. 여러 행동의 디코딩 합계도 같은 한도를 적용해요. 이 값은 앱 전체 메모리 사용량이 아니에요.",
            "투명 배경이 필요하면 원본에서 준비해 주세요. 모든 프레임에 펫이 보여야 해요. GIF 프레임 시간은 읽지만 10ms 이하 지연은 100ms로 조정해요. 파일의 반복 횟수 대신 앱에서 고른 재생 방식을 사용해요.",
            $"정지 이미지는 {ImageCodec.MaxFileBytes / (1024 * 1024)} MiB·각 변 8192px·총 16,777,216픽셀 이하예요. 비율을 유지해 긴 변 최대 {ImageCodec.MaxPetImageDimension}px로 줄이며 확대하지 않아요. PNG 시트는 한 장으로 읽어요. 움직이는 WEBP·APNG는 GIF나 MP4로 내보내 주세요.",
            $"MP4는 {PetMediaImporter.MaxVideoBytes / (1024 * 1024)} MiB·10초 이하로 준비해 주세요. 변환 도구가 있는 앱에서 소리 없이 최대 192px·12fps GIF로 변환해요. 자동 자르기·배경 제거는 하지 않아요. 도구가 없으면 GIF로 내보내 가져와 주세요.",
            "새 펫은 펫 팩 만들기…로 .unfoldpet 파일을 저장한 뒤 이름·버전 확인을 거쳐 앱에 설치해요. 설치 확인을 취소해도 저장된 팩 파일은 남아요. 파일 열기…로 다시 설치할 수 있어요. 설치한 2D 펫을 편집할 때는 저장하고 적용으로 변경을 적용해요. 여러 행동을 넣어 팩 용량 한도를 넘으면 파일 크기도 줄여 주세요."
        ];

    public static string MediaSaveHint(string? name, bool hasIdle, bool busy, bool editing, bool changed)
    {
        if (busy) return "파일을 처리 중이에요. 완료 후 만들거나 저장할 수 있어요.";
        var missingName = string.IsNullOrWhiteSpace(name);
        if (missingName && !hasIdle) return "펫 이름과 기본 파일을 넣어 주세요.";
        if (missingName) return "펫 이름을 입력해 주세요.";
        if (!hasIdle) return "기본에 파일을 넣어 주세요.";
        return editing && !changed ? "저장할 변경 사항이 없어요." : "";
    }

    public static string GlbSaveHint(string? name, bool hasModel, bool busy, bool editing, bool changed)
    {
        if (busy) return "파일을 처리 중이에요. 완료 후 저장할 수 있어요.";
        var missingName = string.IsNullOrWhiteSpace(name);
        if (missingName && !hasModel) return "펫 이름을 입력하고 GLB 파일을 열어 주세요.";
        if (!hasModel) return "GLB 파일을 열어 주세요.";
        if (missingName) return "펫 이름을 입력해 주세요.";
        return editing && !changed ? "저장할 변경 사항이 없어요." : "";
    }

    public static string GlbActionHint(string action, bool assigned, bool isStatic)
    {
        var hint = action == "idle" ? "기본 대기는 필수이며 반복으로 재생해요."
            : assigned ? ""
            : "연결 안 함은 새 반응을 배정하지 않아요. 미리보기에서는 기본 대기를 보여 줘요. 실제 펫에서는 보통 현재 동작을 유지해요.";
        const string staticHint = "애니메이션이 없는 원본은 Static 정지 모델로 사용해요. 움직임이 필요하면 원본에 애니메이션을 넣어 다시 내보내 주세요.";
        return isStatic ? (hint.Length == 0 ? staticHint : hint + " " + staticHint) : hint;
    }

    public static string MediaError(Exception error)
    {
        if (error is InvalidDataException && error.Message == GifDecodeFailure)
        {
            // Only the codec's immediate, known cause is safe to interpret. Never walk arbitrary inner errors.
            var reason = error.InnerException is InvalidDataException inner ? GifError(inner.Message) : null;
            return reason ?? GifDecodeFailure;
        }
        if (error is InvalidDataException)
        {
            var gif = GifError(error.Message); if (gif is not null) return gif;
            var known = error.Message switch
            {
                "GIF는 32 MiB 이하의 파일을 선택해 주세요." or "이미지는 32 MiB 이하로 선택해 주세요." or
                "MP4는 128 MiB 이하의 파일을 선택해 주세요." or
                "GIF, MP4, PNG, JPG, WEBP 또는 BMP 파일을 선택해 주세요." or
                "PNG, JPG, WEBP 또는 BMP 이미지를 선택해 주세요." or
                "움직이는 이미지는 GIF 또는 MP4로 가져와 주세요." or
                "완전히 투명한 프레임이 있어요. 모든 프레임에 펫이 보이는 파일을 선택해 주세요." or
                "완전히 투명한 이미지예요. 펫이 보이는 파일을 선택해 주세요." or
                "펫의 전체 애니메이션이 너무 커요. 이미지 크기나 프레임 수를 줄여 주세요. (최대 128 MiB)" or
                "펫 팩의 파일 용량이 너무 커요. 더 작은 파일을 선택해 주세요." or
                "MP4는 10초 이하의 영상으로 선택해 주세요. 파일을 자동으로 자르지는 않아요." or
                "MP4를 읽지 못했어요. 재생 가능한 영상 파일인지 확인해 주세요." or
                "영상 변환 시간이 초과됐어요. 더 짧거나 작은 MP4를 선택해 주세요." => error.Message,
                "이미지 파일을 읽지 못했어요." or "손상되거나 완전하지 않은 이미지예요." => "이미지가 손상됐거나 완전하지 않아요. 원본에서 지원 이미지 형식으로 다시 내보내 주세요.",
                "이미지가 너무 커요. 1,600만 픽셀 이하로 줄여 주세요." => "이미지 크기가 한도를 넘었어요. 각 변 8192px·총 16,777,216픽셀 이하로 줄여 다시 내보내 주세요.",
                "이미지 크기를 조절하지 못했어요." => "이미지 크기를 줄이지 못했어요. 원본에서 긴 변 512px 이하로 줄여 다시 내보내 주세요.",
                _ => null
            };
            if (known is not null) return known;
        }
        if (error is InvalidOperationException && error.Message == "영상 변환 도구가 없는 앱이에요. MP4 지원 배포본을 사용하거나 GIF 파일을 가져와 주세요.") return error.Message;
        return Ui.ErrorText(error);
    }

    private static string? GifError(string message) => message switch
    {
        "GIF exceeds 32 MiB." => "GIF 파일이 32 MiB를 넘었어요. 파일 크기를 줄여 다시 내보내 주세요.",
        "Decoded GIF exceeds the animation budget." => GifBudgetHint,
        "Cannot decode the GIF." or "Incomplete GIF frame." => "GIF를 끝까지 읽지 못했어요. 원본에서 모든 프레임이 포함된 GIF로 다시 내보내 주세요.",
        "Expected a GIF." => "파일 내용이 GIF가 아니에요. 확장자만 바꾸지 말고 원본에서 GIF로 내보내 주세요.",
        _ => null
    };

    public static string GlbError(Exception error)
    {
        if (error is not InvalidDataException) return Ui.ErrorText(error);
        var message = error.Message;
        if (message.StartsWith(TimeKeyPrefix, StringComparison.Ordinal) && message.EndsWith(TimeKeySuffix, StringComparison.Ordinal)
            && message.Length >= TimeKeyPrefix.Length + TimeKeySuffix.Length) return GlbTimeKeyHint;
        return message switch
        {
            "File exceeds the size limit." => "GLB 파일이 32 MiB를 넘었어요. 파일 크기를 줄여 다시 내보내 주세요.",
            "32 MiB 이하의 GLB 2.0 파일을 선택해 주세요." => "GLB 형식·버전 또는 파일 크기가 맞지 않아요. 32 MiB 이하의 GLB 2.0으로 다시 내보내 주세요.",
            "필수 확장이 없는 glTF 2.0 모델을 사용해 주세요." => "glTF 버전 또는 필수 확장 설정이 맞지 않아요. 필수 확장 없이 GLB 2.0으로 다시 내보내 주세요.",
            "GLB에 포함된 하나의 버퍼만 지원해요." => "모델 데이터가 GLB 내부에 함께 있어야 해요. 외부 파일을 쓰지 않고 바이너리 버퍼 하나를 포함한 GLB로 다시 내보내 주세요.",
            "GLB needs embedded JSON and binary data." => "GLB의 내장 데이터 구조나 크기가 맞지 않아요. 내부 JSON은 8 MiB 이하로 줄이고 바이너리 데이터를 함께 넣어 다시 내보내 주세요.",
            "현재 GLB 텍스처는 파일 내부의 PNG만 지원해요." => "외부 텍스처나 PNG 이외의 텍스처는 읽을 수 없어요. 텍스처를 PNG로 바꿔 GLB 내부에 포함해 다시 내보내 주세요.",
            "압축되지 않은 삼각형 GLB 모델만 지원해요." => "압축 메시 또는 삼각형 이외의 메시 형식은 읽을 수 없어요. 메시 압축과 메시 확장을 끄고 삼각형으로 다시 내보내 주세요.",
            "GLB texture transforms and UV sets other than UV0 are not supported." => "UV0 이외의 UV나 텍스처 변환 확장은 지원하지 않아요. UV0에 변환을 반영하고 텍스처 확장 없이 다시 내보내 주세요.",
            "GLB sparse accessors or mismatched types are not supported." => "Sparse 데이터 또는 데이터 형식이 맞지 않아요. Sparse 없이 일반 데이터로 다시 내보내고 형식을 확인해 주세요.",
            "GLB vertex attributes are inconsistent or use more than four joints." => "정점 속성이나 관절 가중치가 맞지 않아요. 정점별 속성을 맞추고 정점당 관절 가중치를 4개 이하로 줄여 다시 내보내 주세요.",
            "펫 모델은 삼각형 10만 개 이하로 줄여 주세요." => "모델 정점 또는 삼각형 수가 한도를 넘었어요. 정점 30만 개·삼각형 10만 개 이하로 줄여 다시 내보내 주세요.",
            "화면에 표시되는 펫 모델의 삼각형을 10만 개 이하로 줄여 주세요." => "화면에 표시되는 인스턴스의 삼각형 합계가 10만 개를 넘었어요. 모델과 복제 인스턴스를 줄여 다시 내보내 주세요.",
            "GLB textures exceed 64." => "텍스처가 64장을 넘었어요. 텍스처를 합치거나 줄여 다시 내보내 주세요.",
            "GLB textures exceed 64 MiB decoded." => "텍스처의 디코딩 합계가 64 MiB를 넘었어요. 텍스처 크기나 장수를 줄여 다시 내보내 주세요.",
            "Image dimensions exceed the supported limits." => "GLB 텍스처 크기가 한도를 넘었어요. PNG 텍스처의 각 변을 2048px 이하로 줄여 다시 내보내 주세요.",
            "GLB meshes exceed limits." => "모델 메시 수가 맞지 않아요. 메시를 1~256개로 정리해 다시 내보내 주세요.",
            "Too many GLB nodes." => "모델 노드 수가 맞지 않아요. 노드를 1~2048개로 정리해 다시 내보내 주세요.",
            "Too many materials." => "재질이 128개를 넘었어요. 재질을 합치거나 줄여 다시 내보내 주세요.",
            "Too many GLB data blocks." or "GLB decoded data exceeds limits." => "GLB 데이터가 너무 복잡해요. 메시·애니메이션 데이터를 줄여 다시 내보내 주세요.",
            "GLB animations exceed 128." => "애니메이션이 128개를 넘었어요. 필요한 동작만 남겨 다시 내보내 주세요.",
            "Too many GLB animation channels." => "애니메이션 채널이 8192개를 넘었어요. 동작별 채널을 줄여 다시 내보내 주세요.",
            "Unsupported GLB animation target." => "애니메이션 대상이 맞지 않아요. 노드의 이동·회전·크기 또는 모프 가중치 동작으로 다시 내보내 주세요.",
            "Unsupported GLB interpolation." => "애니메이션 보간 방식이 지원되지 않아요. LINEAR·STEP·CUBICSPLINE 중 하나로 다시 내보내 주세요.",
            "Invalid animated GLB node." => "애니메이션 노드의 변환이나 모프 구성이 맞지 않아요. 이동·회전·크기 변환과 모프 설정을 정리해 다시 내보내 주세요.",
            "GLB animation sample count differs." or "Duplicate GLB animation channel." => "애니메이션 키 데이터가 맞지 않아요. 키 개수와 중복 채널을 정리해 다시 내보내 주세요.",
            "지원하지 않는 GLB 알파 재질 형식이에요." => "재질의 알파 형식이 맞지 않아요. OPAQUE·MASK·BLEND 중 하나로 다시 내보내 주세요.",
            "Invalid GLB skeleton." or "GLB bind matrices differ from joints." or "Invalid vertex joint." => "모델의 뼈대나 관절 연결이 맞지 않아요. 스킨당 관절을 512개 이하로 정리하고 바인딩·가중치를 확인해 다시 내보내 주세요.",
            "Invalid GLB morph targets." or "Invalid morph weights." => "모프 데이터가 맞지 않아요. 위치 모프를 32개 이하로 정리하고 정점·가중치 구성을 맞춰 다시 내보내 주세요.",
            "GLB scene is missing." or "GLB scene contains no visible mesh." or "GLB meshes must contain primitives." => "표시할 모델이 없어요. 메시를 포함한 장면을 선택해 다시 내보내 주세요.",
            "Incomplete GLB chunk." or "Invalid GLB chunk." or "GLB JSON must be first." or "Duplicate GLB JSON." or "Duplicate GLB buffer." or
            "GLB buffer range is invalid." or "GLB accessor exceeds its buffer view." or "Invalid GLB triangle indices." or
            "Invalid material reference." or "GLB node graph has invalid or shared children." or "GLB node graph contains a cycle." or
            "Invalid scene node." or "Invalid GLB mesh or skin reference." or "Unsupported GLB component." or
            "Nonfinite GLB data." or "Nonfinite GLB value." or "Invalid GLB vector." or "Invalid GLB quaternion." or "Invalid GLB matrix." or
            "GLB 데이터가 손상됐거나 지원하지 않는 형식이에요." => "GLB 내부 데이터가 손상됐거나 맞지 않아요. 원본에서 GLB 2.0으로 다시 내보내고 파일을 다시 열어 주세요.",
            "The file is not a complete PNG or exceeds 32 MiB." or "Cannot decode the PNG." or "Incomplete PNG pixels." => "내부 PNG 텍스처를 읽지 못했어요. 원본 PNG를 다시 내보내 GLB 내부에 포함해 주세요.",
            _ => Ui.ErrorText(error)
        };
    }
}
