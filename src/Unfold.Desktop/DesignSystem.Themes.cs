using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed record ThemePalette(AppTheme Id, string Name, bool IsDark, string Canvas, string Shell,
    string Surface, string Raised, string Text, string Muted, string Outline, string Line,
    string Accent, string OnAccent, string AccentHover, string Hover, string Halo,
    string Warning, string Error, string Success, string DisabledFill, string DisabledText, string Stopped);

public static partial class DesignSystem
{
    public static IReadOnlyList<ThemePalette> Themes { get; } = Array.AsReadOnly<ThemePalette>([
        new(AppTheme.OatLatte, "오트 라떼", false, "#F3EEE5", "#FBF8F2", "#FFFFFF", "#E9E1D3",
            "#38342E", "#6A6257", "#D2C8B8", "#8C8171", "#756344", "#FFFFFF", "#65543A", "#DED4C3", "#DDD1BD",
            "#865D0C", "#A73D35", "#386246", "#EEE8DE", "#807869", "#D62F32"),
        new(AppTheme.Sage, "세이지", false, "#E6EDE8", "#F4F7F2", "#FFFFFF", "#DCE7DC",
            "#263D32", "#50665A", "#BFCFBF", "#718878", "#496B51", "#FFFFFF", "#395B41", "#CCDCCC", "#C7D7C9",
            "#805812", "#A23C36", "#356B47", "#E0E8E0", "#627568", "#D62F32"),
        new(AppTheme.MidnightBlue, "미드나이트 블루", true, "#101923", "#192430", "#243342", "#2D4052",
            "#E7EFF6", "#B6C6D2", "#465A6C", "#839CB0", "#A8CADF", "#142A3A", "#C0DAEA", "#3A5064", "#3A5268",
            "#E8C382", "#FFB4AE", "#A1D4B7", "#22303E", "#91A5B5", "#FF383C"),
        new(AppTheme.Plum, "플럼", true, "#211C23", "#2B242D", "#382F3A", "#463B45",
            "#F5E9EB", "#D3BCC8", "#665164", "#A2899D", "#E3BBC4", "#37222E", "#F0D1D7", "#574754", "#604C5D",
            "#ECCE91", "#FFB4AE", "#B9D6BC", "#392E39", "#B49CA9", "#FF383C")
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
