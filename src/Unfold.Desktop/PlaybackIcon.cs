namespace Unfold.Desktop;

internal enum PlaybackGlyph { Play, Pause, Stop }

internal sealed class PlaybackIcon : SvgIcon
{
    private PlaybackGlyph glyph;

    public PlaybackIcon(PlaybackGlyph glyph) : base(Asset(glyph), 24) => this.glyph = glyph;

    public PlaybackGlyph Glyph
    {
        get => glyph;
        set
        {
            if (glyph == value) return;
            glyph = value;
            SetAsset(Asset(value), 24);
        }
    }

    private static string Asset(PlaybackGlyph glyph) => $"Playback/{glyph.ToString().ToLowerInvariant()}";
}
