using System.Numerics;
using System.Text.Json.Serialization;

namespace Letterist.Model;

public readonly struct Point2 : IEquatable<Point2>
{
    public float X { get; }
    public float Y { get; }

    [JsonConstructor]
    public Point2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Point2 Zero => new(0, 0);
    public static Point2 One => new(1, 1);

    public Point2 WithX(float x) => new(x, Y);
    public Point2 WithY(float y) => new(X, y);

    public static Point2 operator +(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2 operator *(Point2 p, float scalar) => new(p.X * scalar, p.Y * scalar);
    public static Point2 operator /(Point2 p, float scalar) => new(p.X / scalar, p.Y / scalar);
    public static Point2 operator -(Point2 p) => new(-p.X, -p.Y);

    public float Length => MathF.Sqrt(X * X + Y * Y);
    public float LengthSquared => X * X + Y * Y;

    public Point2 Normalized()
    {
        var len = Length;
        return len > 0 ? this / len : Zero;
    }

    public static float Distance(Point2 a, Point2 b) => (b - a).Length;
    public static float DistanceSquared(Point2 a, Point2 b) => (b - a).LengthSquared;

    public static float Dot(Point2 a, Point2 b) => a.X * b.X + a.Y * b.Y;

    public static Point2 Lerp(Point2 a, Point2 b, float t) => a + (b - a) * t;

    public bool Equals(Point2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Point2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Point2 left, Point2 right) => left.Equals(right);
    public static bool operator !=(Point2 left, Point2 right) => !left.Equals(right);

    public override string ToString() => $"({X:F2}, {Y:F2})";

    public Vector2 ToVector2() => new(X, Y);

    public static Point2 FromVector2(Vector2 v) => new(v.X, v.Y);
}
