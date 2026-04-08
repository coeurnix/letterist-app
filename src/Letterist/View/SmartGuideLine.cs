using Letterist.Model;

namespace Letterist.View;

public readonly struct SmartGuideLine
{
    public SmartGuideLine(GuideOrientation orientation, float position, SmartGuideKind kind)
    {
        Orientation = orientation;
        Position = position;
        Kind = kind;
    }

    public GuideOrientation Orientation { get; }
    public float Position { get; }
    public SmartGuideKind Kind { get; }
}

public enum SmartGuideKind
{
    Grid,
    Guide,
    Alignment
}
