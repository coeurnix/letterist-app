namespace Letterist.Model;

public sealed class TextStroke
{
    public Color Color { get; init; } = Color.Black;

    public float Width { get; init; } = 0f;

    public TextStroke Clone()
    {
        return new TextStroke
        {
            Color = Color,
            Width = Width
        };
    }
}
