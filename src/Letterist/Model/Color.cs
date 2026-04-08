using System.Text.Json.Serialization;

namespace Letterist.Model;

public readonly struct Color : IEquatable<Color>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte A { get; }

    [JsonConstructor]
    public Color(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static Color FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r, g, b, a);

    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Yellow => new(255, 255, 0);
    public static Color Transparent => new(0, 0, 0, 0);

    public Color WithAlpha(byte alpha) => new(R, G, B, alpha);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    public Windows.UI.Color ToWindowsColor() => Windows.UI.Color.FromArgb(A, R, G, B);
}
