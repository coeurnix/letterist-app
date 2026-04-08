namespace Letterist.Model;

public sealed class BalloonLinkStyle
{
    public Color StrokeColor { get; init; } = Color.FromRgba(30, 30, 30, 220);

    public Color FillColor { get; init; } = Color.FromRgba(255, 255, 255, 255);

    public float StrokeWidth { get; init; } = 2f;

    public float ConnectorWidth { get; init; } = 24f;

    public LinkDashStyle DashStyle { get; init; } = LinkDashStyle.Solid;

    public BalloonLinkStyle With(
        Color? strokeColor = null,
        Color? fillColor = null,
        float? strokeWidth = null,
        float? connectorWidth = null,
        LinkDashStyle? dashStyle = null)
    {
        return new BalloonLinkStyle
        {
            StrokeColor = strokeColor ?? StrokeColor,
            FillColor = fillColor ?? FillColor,
            StrokeWidth = strokeWidth ?? StrokeWidth,
            ConnectorWidth = connectorWidth ?? ConnectorWidth,
            DashStyle = dashStyle ?? DashStyle
        };
    }

    public static BalloonLinkStyle Default => new();
}

public enum LinkDashStyle
{
    Solid,
    Dash,
    Dot,
    DashDot
}
