namespace Letterist.Model;

public sealed class TextStyleSpan
{
    public TextStyleSpan()
    {
    }

    public TextStyleSpan(int start, int length, TextStyle style)
    {
        Start = start;
        Length = length;
        Style = style;
    }

    public int Start { get; set; }
    public int Length { get; set; }
    public TextStyle Style { get; set; } = TextStyle.Default;

    public TextStyleSpan Clone()
    {
        return new TextStyleSpan(Start, Length, Style);
    }
}
