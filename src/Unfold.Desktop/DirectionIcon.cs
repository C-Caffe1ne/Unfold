namespace Unfold.Desktop;

internal enum IconDirection { Up, Down, Left, Right }

internal sealed class DirectionIcon : SvgIcon
{
    private IconDirection direction;

    public DirectionIcon(IconDirection direction) : base(Asset(direction), 24) => this.direction = direction;

    public IconDirection Direction
    {
        get => direction;
        set
        {
            if (direction == value) return;
            direction = value;
            SetAsset(Asset(value), 24);
        }
    }

    private static string Asset(IconDirection direction) => $"Directions/{direction.ToString().ToLowerInvariant()}";
}
