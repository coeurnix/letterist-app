namespace Letterist.Model;

public sealed class TextShadow
{
    public Color Color { get; init; } = Color.Black;

    public float OffsetX { get; init; } = 2f;

    public float OffsetY { get; init; } = 2f;

    public float Blur { get; init; } = 0f;

    public float Opacity { get; init; } = 0.45f;

    public static TextShadow Default => new() { Opacity = 0f, OffsetX = 2f, OffsetY = 2f };

    public TextShadow Clone()
    {
        return new TextShadow
        {
            Color = Color,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            Blur = Blur,
            Opacity = Opacity
        };
    }
}
