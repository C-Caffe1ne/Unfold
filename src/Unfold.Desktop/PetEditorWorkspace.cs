using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Unfold.Desktop;

internal sealed class PetEditorWorkspace : Grid
{
    private readonly Window owner;
    private readonly Border stage;
    private readonly Grid previewPane;
    private readonly Control controls, settings, title;
    private readonly double previewMinHeight, previewMaxHeight;
    private ScrollViewer? previewScroll;
    private readonly bool fullWidthPreview;
    private bool? wide;
    public PetEditorWorkspace(Window owner, string prefix, Border stage, Control controls, Control settings, double previewMinHeight = 140, bool fullWidthPreview = false, double? previewMaxHeight = null)
    {
        this.owner = owner; this.stage = stage; this.controls = controls; this.settings = settings;
        this.previewMinHeight = previewMinHeight; this.fullWidthPreview = fullWidthPreview;
        this.previewMaxHeight = Math.Max(previewMinHeight, previewMaxHeight ?? previewMinHeight);
        Name = prefix + "Workspace";
        title = Heading("미리보기");
        previewPane = new Grid { Name = prefix + "PreviewPane" };
        previewPane.Children.Add(title); Grid.SetRow(stage, 2); previewPane.Children.Add(stage);
        if (!fullWidthPreview) previewPane.Children.Add(controls);
        Children.Add(previewPane);
        if (fullWidthPreview) Children.Add(controls);
        Children.Add(settings);
        SizeChanged += (_, _) => { RefreshLayout(); if (CanGrowPreview) InvalidateMeasure(); };
        AttachedToVisualTree += (_, _) =>
        {
            owner.PropertyChanged += OwnerChanged;
            if (CanGrowPreview)
            {
                previewScroll = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
                if (previewScroll is not null) previewScroll.PropertyChanged += PreviewViewportChanged;
            }
            RefreshLayout(); InvalidateMeasure();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            owner.PropertyChanged -= OwnerChanged;
            if (previewScroll is not null) previewScroll.PropertyChanged -= PreviewViewportChanged;
            previewScroll = null;
        };
        RefreshLayout();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // Full-width previews grow within the visible page, independently of the
        // action fields below. Side-by-side previews follow those fields.
        var height = fullWidthPreview ? FullPreviewHeight() : wide == true
            ? Math.Max(previewMinHeight, settings.DesiredSize.Height - title.DesiredSize.Height - 8) : previewMinHeight;
        if (stage.Height == height) return size;
        stage.Height = height;
        return base.MeasureOverride(availableSize);
    }
    private bool CanGrowPreview => fullWidthPreview && previewMaxHeight > previewMinHeight;
    private double FullPreviewHeight()
    {
        if (!CanGrowPreview || previewScroll is null || previewScroll.Viewport.Height <= 0) return previewMinHeight;
        // Undo scrolling so moving to the lower settings cannot change the height.
        var top = this.TranslatePoint(default, previewScroll)?.Y ?? 0;
        top += previewScroll.Offset.Y;
        var available = previewScroll.Viewport.Height - top - title.DesiredSize.Height - 8 - 16;
        return Math.Clamp(available, previewMinHeight, previewMaxHeight);
    }
    private void PreviewViewportChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    { if (e.Property == ScrollViewer.ViewportProperty) InvalidateMeasure(); }
    internal Grid Identity(string name, TextBox input, Button open)
    {
        var row = new Grid { Name = name, ColumnDefinitions = new("Auto,12,Auto,*") };
        row.Children.Add(PetManagementView.Field("펫 이름", input)); Grid.SetColumn(open, 2); row.Children.Add(open);
        previewPane.SizeChanged += (_, _) => input.Width = previewPane.Bounds.Width / 2;
        return row;
    }
    internal static TextBlock Heading(string text)
    {
        var label = Ui.Text(text, DesignSystem.Section); label.FontWeight = FontWeight.SemiBold; return label;
    }
    private void OwnerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != BoundsProperty) return;
        RefreshLayout(); if (CanGrowPreview) InvalidateMeasure();
    }
    private void RefreshLayout()
    {
        var next = Bounds.Width >= 680;
        stage.MinHeight = previewMinHeight;
        if (wide == next) return; wide = next;
        if (fullWidthPreview)
        {
            ColumnDefinitions = new("*,24,*"); RowDefinitions = new("Auto,16,Auto");
            Grid.SetColumnSpan(previewPane, 3);
            previewPane.ColumnDefinitions = new("*"); previewPane.RowDefinitions = new("Auto,8,Auto");
            Grid.SetColumn(controls, 0); Grid.SetRow(controls, 2);
            Grid.SetColumn(settings, 2); Grid.SetRow(settings, 2);
            controls.VerticalAlignment = settings.VerticalAlignment = VerticalAlignment.Top;
            return;
        }
        ColumnDefinitions = next ? new("*,24,*") : new("*");
        RowDefinitions = next ? new("Auto") : new("Auto,16,Auto");
        Grid.SetColumn(settings, next ? 2 : 0); Grid.SetRow(settings, next ? 0 : 2);
        previewPane.ColumnDefinitions = next ? new("*") : new("140,16,*");
        previewPane.RowDefinitions = next ? new("Auto,8,*,8,Auto") : new("Auto,8,Auto");
        Grid.SetColumnSpan(title, next ? 1 : 3);
        Grid.SetColumn(controls, next ? 0 : 2); Grid.SetRow(controls, next ? 4 : 2);
        controls.VerticalAlignment = next ? VerticalAlignment.Top : VerticalAlignment.Center;
    }
}
