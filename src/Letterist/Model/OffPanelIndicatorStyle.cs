using System;

namespace Letterist.Model;

public readonly struct OffPanelIndicatorStyle : IEquatable<OffPanelIndicatorStyle>
{
    public OffPanelIndicatorStyle(Color color, float size)
    {
        Color = color;
        Size = size;
    }

    public Color Color { get; }
    public float Size { get; }

    public static OffPanelIndicatorStyle Default => new(new Color(0, 120, 215, 255), 16f);

    public OffPanelIndicatorStyle With(Color? color = null, float? size = null)
    {
        return new OffPanelIndicatorStyle(color ?? Color, size ?? Size);
    }

    public bool Equals(OffPanelIndicatorStyle other)
    {
        return Color.Equals(other.Color) && Math.Abs(Size - other.Size) < 0.001f;
    }

    public override bool Equals(object? obj) => obj is OffPanelIndicatorStyle other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Color, Size);

    public static bool operator ==(OffPanelIndicatorStyle left, OffPanelIndicatorStyle right) => left.Equals(right);
    public static bool operator !=(OffPanelIndicatorStyle left, OffPanelIndicatorStyle right) => !left.Equals(right);
}
