using System.Text.Json.Serialization;

namespace Letterist.Model;

public readonly struct Size2 : IEquatable<Size2>
{
    public float Width { get; }
    public float Height { get; }

    [JsonConstructor]
    public Size2(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public static Size2 Zero => new(0, 0);

    public Size2 WithWidth(float width) => new(width, Height);
    public Size2 WithHeight(float height) => new(Width, height);

    public static Size2 operator +(Size2 a, Size2 b) => new(a.Width + b.Width, a.Height + b.Height);
    public static Size2 operator -(Size2 a, Size2 b) => new(a.Width - b.Width, a.Height - b.Height);
    public static Size2 operator *(Size2 s, float scalar) => new(s.Width * scalar, s.Height * scalar);
    public static Size2 operator /(Size2 s, float scalar) => new(s.Width / scalar, s.Height / scalar);

    public float Area => Width * Height;

    public bool Contains(Size2 other) => Width >= other.Width && Height >= other.Height;

    public bool Equals(Size2 other) => Width == other.Width && Height == other.Height;
    public override bool Equals(object? obj) => obj is Size2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(Size2 left, Size2 right) => left.Equals(right);
    public static bool operator !=(Size2 left, Size2 right) => !left.Equals(right);

    public override string ToString() => $"{Width:F2} x {Height:F2}";
}
