using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Svg;

namespace Unfold.Desktop;

/// <summary>Preserves the original SVG viewport and tints its alpha with the inherited foreground.</summary>
internal abstract class SvgIcon : TemplatedControl
{
    private static readonly Dictionary<string, Drawing> Drawings = [];
    private DrawingBrush mask = null!;

    static SvgIcon() => AffectsRender<SvgIcon>(ForegroundProperty);

    protected SvgIcon(string assetName, double size)
    {
        Width = Height = size;
        IsHitTestVisible = false;
        SetAsset(assetName, size);
    }

    protected void SetAsset(string assetName, double size)
    {
        const double inset = 2;
        if (!Drawings.TryGetValue(assetName, out var drawing))
        {
            var image = new SvgImage { Source = SvgSource.Load($"avares://Unfold/Assets/Icons/{assetName}.svg", null) };
            var group = new DrawingGroup();
            using (var context = group.Open())
            {
                // Give stroked paths an integer canvas so tiny floating-point bounds
                // cannot round the mask up by a pixel and shift its center.
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size + inset * 2, size + inset * 2));
                var offset = new Point(inset + (size - image.Size.Width) / 2, inset + (size - image.Size.Height) / 2);
                ((IImage)image).Draw(context, new Rect(image.Size), new Rect(offset, image.Size));
            }
            drawing = group;
            Drawings.Add(assetName, drawing);
        }

        mask = new DrawingBrush(drawing) { Stretch = Stretch.None,
            SourceRect = new RelativeRect(inset, inset, size, size, RelativeUnit.Absolute),
            AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        using (context.PushOpacityMask(mask, bounds))
            context.DrawRectangle(Foreground, null, bounds);
    }
}
