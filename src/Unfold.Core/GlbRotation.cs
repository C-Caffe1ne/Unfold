using System.Numerics;
using System.Text.Json.Serialization;

namespace Unfold.Core;

/// <summary>Model rotation in degrees, applied in X, Y, then Z order.</summary>
public readonly record struct GlbRotation(float X = 0, float Y = 0, float Z = 0)
{
    [JsonIgnore] public bool IsValid => Valid(X) && Valid(Y) && Valid(Z);
    private static bool Valid(float value) => float.IsFinite(value) && value is >= -180 and <= 180;
    public GlbRotation Normalize() => new(Angle(X), Angle(Y), Angle(Z));
    private static float Angle(float value)
    {
        value %= 360;
        return value > 180 ? value - 360 : value < -180 ? value + 360 : value;
    }
    internal Matrix4x4 Matrix => Matrix4x4.CreateRotationX(X * MathF.PI / 180) *
        Matrix4x4.CreateRotationY(Y * MathF.PI / 180) * Matrix4x4.CreateRotationZ(Z * MathF.PI / 180);
}
