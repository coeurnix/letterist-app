using System.Text.Json.Serialization;

namespace Letterist.Model;

public readonly struct Rect : IEquatable<Rect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    [JsonConstructor]
    public Rect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public static Rect FromPositionSize(Point2 position, Size2 size) =>
        new(position.X, position.Y, size.Width, size.Height);

    public static Rect FromCenterSize(Point2 center, Size2 size) =>
        new(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height);

    public static Rect FromCorners(Point2 topLeft, Point2 bottomRight) =>
        new(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

    public static Rect Empty => new(0, 0, 0, 0);

    public Point2 Position => new(X, Y);
    public Size2 Size => new(Width, Height);

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public Point2 TopLeft => new(Left, Top);
    public Point2 TopRight => new(Right, Top);
    public Point2 BottomLeft => new(Left, Bottom);
    public Point2 BottomRight => new(Right, Bottom);
    public Point2 Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(Point2 point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public bool Contains(Rect other) =>
        other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    public bool Intersects(Rect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public Rect Inflate(float horizontal, float vertical) =>
        new(X - horizontal, Y - vertical, Width + horizontal * 2, Height + vertical * 2);

    public Rect Offset(Point2 offset) =>
        new(X + offset.X, Y + offset.Y, Width, Height);

    public Rect Union(Rect other)
    {
        var left = MathF.Min(Left, other.Left);
        var top = MathF.Min(Top, other.Top);
        var right = MathF.Max(Right, other.Right);
        var bottom = MathF.Max(Bottom, other.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    public bool Equals(Rect other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    public override string ToString() => $"[{X:F2}, {Y:F2}, {Width:F2}, {Height:F2}]";

    public Windows.Foundation.Rect ToWindowsRect() => new(X, Y, Width, Height);
}
