namespace Unfold.Desktop;

internal sealed class SoundPreviewIcon() : SvgIcon("Sound/play", 16)
{
    private bool isPlaying;
    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (isPlaying == value) return;
            isPlaying = value;
            SetAsset(value ? "Sound/stop" : "Sound/play", 16);
        }
    }
}

internal enum SoundVolumeGlyph { Muted, Low, High }

internal sealed class SoundVolumeIcon() : SvgIcon("Sound/volume-high", 24)
{
    public SoundVolumeGlyph Glyph { get; private set; } = SoundVolumeGlyph.High;

    public void SetVolume(double percent)
    {
        var next = percent <= 0 ? SoundVolumeGlyph.Muted : percent < 50 ? SoundVolumeGlyph.Low : SoundVolumeGlyph.High;
        if (Glyph == next) return;
        Glyph = next;
        SetAsset($"Sound/volume-{next.ToString().ToLowerInvariant()}", 24);
    }
}
