using Letterist.Model;

namespace Letterist.View;

public readonly struct SnapFeedback
{
    public SnapFeedback(Point2 anchor, Point2 offset, bool guideSnap)
    {
        Anchor = anchor;
        Offset = offset;
        GuideSnap = guideSnap;
    }

    public Point2 Anchor { get; }
    public Point2 Offset { get; }
    public bool GuideSnap { get; }
}
