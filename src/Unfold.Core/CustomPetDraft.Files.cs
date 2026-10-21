namespace Unfold.Core;

public sealed partial class CustomPetDraft
{
    // An action's first file lives in `clips`; up to three more play in turn or at random.
    private readonly Dictionary<string, List<ImportedPetClip>> extras = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> orders = new(StringComparer.Ordinal);

    public IReadOnlyList<ImportedPetClip> Files(string action) =>
        !clips.TryGetValue(action, out var first) ? [] : extras.TryGetValue(action, out var more) ? [first, .. more] : [first];

    public string Order(string action) => orders.GetValueOrDefault(action, "random");

    public void SetOrder(string action, string order)
    {
        if (!AvailableActions.Contains(action) || order is not ("random" or "sequence")) throw new ArgumentException(L.T("잘못된 재생 설정이에요."));
        if (order == "random") orders.Remove(action); else orders[action] = order;
    }

    /// <summary>Adds another file to an action. The first file of an action is set with <see cref="SetClip"/>.</summary>
    public void AddFile(string action, ImportedPetClip clip, ImportedPetClip? original = null)
    {
        if (!clips.ContainsKey(action)) { SetClip(action, clip); return; }
        var more = extras.GetValueOrDefault(action) ?? [];
        if (more.Count + 1 >= AnimationDefinition.MaxFiles) throw new InvalidDataException(L.T("한 상황에는 파일을 4개까지 넣을 수 있어요."));
        CheckBudget(clip, replacing: null);
        more.Add(clip); extras[action] = more;
        if (original is not null && !ReferenceEquals(original, clip)) originals[clip] = original;
    }

    public void RemoveFile(string action, int index)
    {
        var files = Files(action);
        if (index < 0 || index >= files.Count) return;
        if (index == 0 && !extras.ContainsKey(action)) { RemoveClip(action); return; }
        originals.Remove(files[index]);
        var more = extras[action];
        if (index == 0) clips[action] = more[0]; // The next file becomes the first.
        more.RemoveAt(Math.Max(0, index - 1));
        if (more.Count == 0) extras.Remove(action);
    }

    private void ReplaceFile(string action, int index, ImportedPetClip clip)
    {
        if (index == 0) { PutClip(action, clip); return; }
        CheckBudget(clip, extras[action][index - 1]);
        extras[action][index - 1] = clip;
    }

    private IEnumerable<ImportedPetClip> AllFiles => clips.Values.Concat(extras.Values.SelectMany(more => more));

    private void CheckBudget(ImportedPetClip clip, ImportedPetClip? replacing)
    {
        var others = AllFiles.Where(item => !ReferenceEquals(item, replacing)).ToArray();
        if (others.Sum(item => item.DecodedBytes) + clip.DecodedBytes > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException(L.T("펫의 전체 애니메이션이 너무 커요. 이미지 크기나 프레임 수를 줄여 주세요. (최대 128 MiB)"));
        if (sourceManifest is null && others.Sum(item => (long)item.FileBytes) + clip.FileBytes > CharacterPack.MaxArchiveBytes - ImageCodec.MaxFileBytes - 64 * 1024)
            throw new InvalidDataException(L.T("펫 팩의 파일 용량이 너무 커요. 더 작은 파일을 선택해 주세요."));
    }

    private static AnimationDefinition WithFiles(AnimationDefinition definition, IReadOnlyList<AnimationDefinition>? variants, string order) =>
        definition with { Variants = variants is { Count: > 0 } ? variants.ToArray() : null, Order = order == "sequence" ? order : null };
}
