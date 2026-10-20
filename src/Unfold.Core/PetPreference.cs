namespace Unfold.Core;

public sealed record PetPreference
{
    /// <summary>Null until a size has been chosen or the pet has been selected.</summary>
    public int? ScalePercent { get; init; }
    public bool Favorite { get; init; }
}
