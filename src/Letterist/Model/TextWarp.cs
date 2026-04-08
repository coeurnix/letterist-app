namespace Letterist.Model;

public enum TextWarpPreset
{
    None,
    ArcUp,
    ArcDown,
    Bulge,
    Pinch,
    Wave,
    Flag
}

public sealed class TextWarpMesh : IEquatable<TextWarpMesh>
{
    public Point2 TopLeftOffset { get; init; } = Point2.Zero;
    public Point2 TopRightOffset { get; init; } = Point2.Zero;
    public Point2 BottomRightOffset { get; init; } = Point2.Zero;
    public Point2 BottomLeftOffset { get; init; } = Point2.Zero;

    public static TextWarpMesh Identity => new();

    public bool IsIdentity =>
        IsNearlyZero(TopLeftOffset) &&
        IsNearlyZero(TopRightOffset) &&
        IsNearlyZero(BottomRightOffset) &&
        IsNearlyZero(BottomLeftOffset);

    public TextWarpMesh Clone()
    {
        return new TextWarpMesh
        {
            TopLeftOffset = TopLeftOffset,
            TopRightOffset = TopRightOffset,
            BottomRightOffset = BottomRightOffset,
            BottomLeftOffset = BottomLeftOffset
        };
    }

    public TextWarpMesh With(
        Point2? topLeftOffset = null,
        Point2? topRightOffset = null,
        Point2? bottomRightOffset = null,
        Point2? bottomLeftOffset = null)
    {
        return new TextWarpMesh
        {
            TopLeftOffset = topLeftOffset ?? TopLeftOffset,
            TopRightOffset = topRightOffset ?? TopRightOffset,
            BottomRightOffset = bottomRightOffset ?? BottomRightOffset,
            BottomLeftOffset = bottomLeftOffset ?? BottomLeftOffset
        };
    }

    public bool Equals(TextWarpMesh? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return NearlyEqual(TopLeftOffset, other.TopLeftOffset)
            && NearlyEqual(TopRightOffset, other.TopRightOffset)
            && NearlyEqual(BottomRightOffset, other.BottomRightOffset)
            && NearlyEqual(BottomLeftOffset, other.BottomLeftOffset);
    }

    public override bool Equals(object? obj) => Equals(obj as TextWarpMesh);

    public override int GetHashCode()
    {
        return HashCode.Combine(
            TopLeftOffset.GetHashCode(),
            TopRightOffset.GetHashCode(),
            BottomRightOffset.GetHashCode(),
            BottomLeftOffset.GetHashCode());
    }

    private static bool IsNearlyZero(Point2 value)
    {
        return MathF.Abs(value.X) < 0.0001f && MathF.Abs(value.Y) < 0.0001f;
    }

    private static bool NearlyEqual(Point2 left, Point2 right)
    {
        return MathF.Abs(left.X - right.X) < 0.0001f && MathF.Abs(left.Y - right.Y) < 0.0001f;
    }
}
