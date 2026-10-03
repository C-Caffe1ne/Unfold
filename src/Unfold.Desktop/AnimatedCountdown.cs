using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation.Peers;
using Avalonia.Media;
using Avalonia.Threading;

namespace Unfold.Desktop;

/// <summary>Draws only changed digits as a clipped vertical transition; Text remains the current time for automation.</summary>
internal sealed class AnimatedCountdown : TemplatedControl, IDisposable
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<AnimatedCountdown, string>(nameof(Text), "60:00");
    public static readonly StyledProperty<double> LineHeightProperty = AvaloniaProperty.Register<AnimatedCountdown, double>(nameof(LineHeight), 74);
    private static readonly TimeSpan MotionDuration = TimeSpan.FromMilliseconds(380);
    private readonly DispatcherTimer frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch motionClock = new();
    private readonly Dictionary<char, FormattedText> glyphs = [];
    private readonly HashSet<int> movingDigits = [];
    private string previousText = "";
    private int? previousSeconds;
    private bool previouslyActive, disposed;
    private Typeface? cachedTypeface;
    private double cachedSize, digitWidth;
    private IBrush? cachedBrush;

    public bool AnimationsEnabled { get; set; } = true;
    internal IReadOnlyCollection<int> AnimatedDigitIndices => movingDigits;
    internal bool HasDigitMotion => frameTimer.IsEnabled;
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    static AnimatedCountdown()
    {
        AffectsMeasure<AnimatedCountdown>(TextProperty, LineHeightProperty, FontSizeProperty, FontFamilyProperty,
            FontStyleProperty, FontWeightProperty, FontStretchProperty);
        AffectsRender<AnimatedCountdown>(TextProperty, ForegroundProperty);
    }

    public AnimatedCountdown()
    {
        FontFamily = DesignSystem.AppFont; FontSize = 64; Foreground = DesignSystem.Cream;
        frameTimer.Tick += OnFrame;
    }

    public void UpdateTime(TimeSpan remaining, bool active)
    {
        if (disposed) return;
        var seconds = (int)Math.Floor(Math.Max(0, remaining.TotalSeconds));
        var next = $"{seconds / 60:00}:{seconds % 60:00}";
        var current = Text ?? "";
        var animate = AnimationsEnabled && active && previouslyActive && IsEffectivelyVisible &&
            VisualRoot is not null && previousSeconds == seconds + 1 && current.Length == next.Length;
        previousSeconds = seconds; previouslyActive = active;
        Opacity = active ? 1 : .55;
        if (current == next)
        {
            if (!active || !AnimationsEnabled) StopAnimation();
            return;
        }
        StopAnimation();
        previousText = current; Text = next;
        if (animate)
        {
            for (var index = 0; index < next.Length; index++)
                if (current[index] != next[index] && next[index] != ':') movingDigits.Add(index);
            if (movingDigits.Count > 0) { motionClock.Restart(); frameTimer.Start(); }
        }
        InvalidateVisual();
    }

    public void StopAnimation()
    {
        frameTimer.Stop(); motionClock.Reset(); movingDigits.Clear(); InvalidateVisual();
    }

    private void OnFrame(object? sender, EventArgs args)
    {
        if (!IsEffectivelyVisible || motionClock.Elapsed >= MotionDuration) StopAnimation();
        else InvalidateVisual();
    }

    private void PrepareGlyphs()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        if (cachedTypeface == typeface && cachedSize == FontSize && ReferenceEquals(cachedBrush, Foreground)) return;
        cachedTypeface = typeface; cachedSize = FontSize; cachedBrush = Foreground; glyphs.Clear();
        foreach (var value in "0123456789:")
            glyphs[value] = new FormattedText(value.ToString(), CultureInfo.InvariantCulture, FlowDirection,
                typeface, FontSize, Foreground);
        digitWidth = glyphs.Where(pair => pair.Key != ':').Max(pair => pair.Value.WidthIncludingTrailingWhitespace);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        PrepareGlyphs();
        var value = Text ?? "";
        var width = value.Sum(character => character == ':' ? glyphs[':'].WidthIncludingTrailingWhitespace : digitWidth);
        var height = LineHeight > 0 && double.IsFinite(LineHeight) ? LineHeight : glyphs.Values.Max(glyph => glyph.Height);
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        PrepareGlyphs();
        var value = Text ?? "";
        var progress = Math.Clamp(motionClock.Elapsed.TotalMilliseconds / MotionDuration.TotalMilliseconds, 0, 1);
        double x = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!glyphs.TryGetValue(character, out var next)) continue;
            var width = character == ':' ? next.WidthIncludingTrailingWhitespace : digitWidth;
            using (context.PushClip(new Rect(x, 0, width, Bounds.Height)))
            {
                var nextY = (Bounds.Height - next.Height) / 2;
                if (movingDigits.Contains(index))
                {
                    var old = glyphs[previousText[index]];
                    var outgoing = Math.Clamp(progress / .42, 0, 1);
                    var incoming = Math.Clamp((progress - .42) / .58, 0, 1);
                    if (outgoing < 1)
                        context.DrawText(old, new Point(x + (width - old.WidthIncludingTrailingWhitespace) / 2,
                            (Bounds.Height - old.Height) / 2 - Bounds.Height * outgoing * outgoing));
                    nextY += Bounds.Height * Math.Pow(1 - incoming, 3);
                }
                context.DrawText(next, new Point(x + (width - next.WidthIncludingTrailingWhitespace) / 2, nextY));
            }
            x += width;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        StopAnimation(); base.OnDetachedFromVisualTree(args);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CountdownAutomationPeer(this);

    private sealed class CountdownAutomationPeer(AnimatedCountdown owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string GetNameCore() => "남은 시간 " + owner.Text;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; StopAnimation(); frameTimer.Tick -= OnFrame;
    }
}
