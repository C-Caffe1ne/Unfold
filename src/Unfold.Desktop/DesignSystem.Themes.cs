using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed record ThemePalette(AppTheme Id, string Name, bool IsDark, string Canvas, string Shell,
    string Surface, string Raised, string Text, string Muted, string Outline, string Line,
    string Accent, string OnAccent, string Secondary, string AccentHover, string Hover, string Halo,
    string Warning, string Error, string Success, string DisabledFill, string DisabledText, string Stopped);

public static partial class DesignSystem
{
    public static IReadOnlyList<ThemePalette> Themes { get; } = Array.AsReadOnly<ThemePalette>([
        // Keep the persisted IDs while replacing their visual identities with the approved brand palettes.
        new(AppTheme.OatLatte, "다정한 오트", false, "#F5EFE6", "#FAF6EF", "#FFFCF7", "#F0E5D8",
            "#342D28", "#706154", "#D8CBBE", "#9A8674", "#985139", "#FFFFFF", "#E9B894", "#84432F", "#EDDDCC", "#EDDDCC",
            "#7B560B", "#9E3F38", "#3F6850", "#EAE1D6", "#807365", "#B63734"),
        new(AppTheme.Sage, "숨 고르는 숲", false, "#F0F2E8", "#F7F8F0", "#FCFCF6", "#E7ECDD",
            "#263D32", "#5D6C5F", "#CAD1BF", "#7B8C76", "#365D4C", "#FFFFFF", "#CEDA9B", "#294B3C", "#E1E6CD", "#E1E6CD",
            "#765912", "#9F3D3D", "#365D4C", "#E3E8D9", "#73806E", "#B63734"),
        new(AppTheme.MidnightBlue, "밤의 버터", true, "#172133", "#1D293F", "#243047", "#2B3850",
            "#F0F1ED", "#B5C0D0", "#46546B", "#8395AD", "#E8CF91", "#263147", "#94ACC5", "#F0DBAE", "#303E55", "#303E55",
            "#ECD09A", "#FFB4AE", "#AFD6BE", "#263248", "#91A0B7", "#FF888B"),
        new(AppTheme.Plum, "유연한 라일락", false, "#F1EDF7", "#F7F4FB", "#FDFCFF", "#ECE5F4",
            "#32283E", "#70627F", "#D5CADE", "#9785AA", "#685187", "#FFFFFF", "#C8BAE6", "#574171", "#E4DAF1", "#E4DAF1",
            "#785515", "#9E3C50", "#3C6956", "#E8E1F0", "#82738F", "#B63750")
    ]);
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.OatLatte;
    // Keep brush identities stable: open popups, dialogs and unsaved pages update in place.
    public static readonly SolidColorBrush Canvas = new(), Shell = new(), Surface = new(), Raised = new(),
        Cream = new(), Ink = new(), Muted = new(), Hover = new(), Accent = new(), AccentHover = new(),
        TextTertiary = new(), OutlineSubtle = new(), OutlineStrong = new(), Error = new(), Warning = new(),
        Success = new(), DisabledFill = new(), DisabledText = new(), FocusRing = new(), PetHalo = new(),
        // A stopped timer is a deliberate user action, so it stays distinct from the error color.
        Stopped = new();

    static DesignSystem() => ApplyTheme(AppTheme.OatLatte);

    public static void ApplyTheme(AppTheme theme)
    {
        var palette = Themes.SingleOrDefault(item => item.Id == theme) ?? Themes[0];
        CurrentTheme = palette.Id;
        (SolidColorBrush Brush, string Color)[] tokens = [
            (Canvas, palette.Canvas), (Shell, palette.Shell), (Surface, palette.Surface), (Raised, palette.Raised),
            (Cream, palette.Text), (Muted, palette.Muted), (TextTertiary, palette.Muted),
            (Accent, palette.Accent), (Ink, palette.OnAccent), (AccentHover, palette.AccentHover), (Hover, palette.Hover),
            (OutlineSubtle, palette.Outline), (OutlineStrong, palette.Line), (FocusRing, palette.Accent), (PetHalo, palette.Halo),
            (Error, palette.Error), (Warning, palette.Warning), (Success, palette.Success),
            (DisabledFill, palette.DisabledFill), (DisabledText, palette.DisabledText), (Stopped, palette.Stopped)
        ];
        foreach (var (brush, color) in tokens) brush.Color = Color.Parse(color);
        if (Application.Current is { } app && app.Styles.OfType<FluentTheme>().FirstOrDefault() is { } fluent)
            ApplyFluentPalette(app, fluent, palette);
    }

    private static void ApplyFluentPalette(Application app, FluentTheme fluent, ThemePalette palette)
    {
        var variant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        fluent.Palettes[variant] = new ColorPaletteResources
        {
            Accent = ColorOf(Accent), RegionColor = ColorOf(Shell), BaseHigh = ColorOf(Cream),
            BaseMediumHigh = ColorOf(Muted), AltHigh = ColorOf(Surface), ErrorText = ColorOf(Error)
        };
        app.RequestedThemeVariant = variant;
    }
}
