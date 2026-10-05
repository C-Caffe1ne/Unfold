using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Unfold.Desktop;

internal sealed class PetEditorWorkspace : Grid
{
    private readonly Window owner;
    private readonly Border stage;
    private readonly Grid previewPane;
    private readonly Control controls, settings, title;
    private bool? wide;
    public PetEditorWorkspace(Window owner, string prefix, Border stage, Control controls, Control settings)
    {
        this.owner = owner; this.stage = stage; this.controls = controls; this.settings = settings;
        Name = prefix + "Workspace";
        title = Heading("미리보기");
        previewPane = new Grid { Name = prefix + "PreviewPane" };
        previewPane.Children.Add(title); Grid.SetRow(stage, 2); previewPane.Children.Add(stage);
        previewPane.Children.Add(controls); Children.Add(previewPane); Children.Add(settings);
        SizeChanged += (_, _) => RefreshLayout();
        AttachedToVisualTree += (_, _) => { owner.PropertyChanged += OwnerChanged; RefreshLayout(); };
        DetachedFromVisualTree += (_, _) => owner.PropertyChanged -= OwnerChanged;
        RefreshLayout();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // Match the preview surface to the action fields, keeping playback and
        // pet selection below it rather than enlarging the surface to a square.
        var height = wide == true ? Math.Max(140, settings.DesiredSize.Height - title.DesiredSize.Height - 8) : 140;
        if (stage.Height == height) return size;
        stage.Height = height;
        return base.MeasureOverride(availableSize);
    }
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
    { if (e.Property == BoundsProperty) RefreshLayout(); }
    private void RefreshLayout()
    {
        var next = Bounds.Width >= 680;
        stage.MinHeight = 140;
        if (wide == next) return; wide = next;
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
