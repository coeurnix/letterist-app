namespace Letterist.Model;

public sealed class TextPath : IEquatable<TextPath>
{
    public Point2 Start { get; private set; }
    public Point2 Control1 { get; private set; }
    public Point2 Control2 { get; private set; }
    public Point2 End { get; private set; }

    public float Offset { get; private set; }

    public float StartPosition { get; private set; }

    public float EndPosition { get; private set; }

    public bool ReverseDirection { get; private set; }

    public TextPath(
        Point2 start,
        Point2 control1,
        Point2 control2,
        Point2 end,
        float offset = 0f,
        float startPosition = 0f,
        float endPosition = 1f,
        bool reverseDirection = false)
    {
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
        Offset = offset;
        StartPosition = startPosition;
        EndPosition = endPosition;
        ReverseDirection = reverseDirection;
        NormalizeRange();
    }

    public TextPath Clone()
    {
        return new TextPath(Start, Control1, Control2, End, Offset, StartPosition, EndPosition, ReverseDirection);
    }

    public TextPath With(
        Point2? start = null,
        Point2? control1 = null,
        Point2? control2 = null,
        Point2? end = null,
        float? offset = null,
        float? startPosition = null,
        float? endPosition = null,
        bool? reverseDirection = null)
    {
        return new TextPath(
            start ?? Start,
            control1 ?? Control1,
            control2 ?? Control2,
            end ?? End,
            offset ?? Offset,
            startPosition ?? StartPosition,
            endPosition ?? EndPosition,
            reverseDirection ?? ReverseDirection);
    }

    public static TextPath CreateDefault(Size2 ownerSize)
    {
        var width = MathF.Max(80f, ownerSize.Width);
        var height = MathF.Max(48f, ownerSize.Height);
        var halfSpan = width * 0.42f;
        var arch = height * 0.55f;

        return new TextPath(
            new Point2(-halfSpan, 0f),
            new Point2(-halfSpan * 0.35f, -arch),
            new Point2(halfSpan * 0.35f, -arch),
            new Point2(halfSpan, 0f));
    }

    public static Point2 LocalToWorld(Point2 localPoint, Point2 origin, float rotationDegrees = 0f)
    {
        if (MathF.Abs(rotationDegrees) <= 0.01f)
        {
            return origin + localPoint;
        }

        var radians = rotationDegrees * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var x = localPoint.X * cos - localPoint.Y * sin;
        var y = localPoint.X * sin + localPoint.Y * cos;
        return origin + new Point2(x, y);
    }

    public static Point2 WorldToLocal(Point2 worldPoint, Point2 origin, float rotationDegrees = 0f)
    {
        var local = worldPoint - origin;
        if (MathF.Abs(rotationDegrees) <= 0.01f)
        {
            return local;
        }

        var radians = -rotationDegrees * MathF.PI / 180f;
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var x = local.X * cos - local.Y * sin;
        var y = local.X * sin + local.Y * cos;
        return new Point2(x, y);
    }

    private void NormalizeRange()
    {
        StartPosition = Math.Clamp(StartPosition, 0f, 1f);
        EndPosition = Math.Clamp(EndPosition, 0f, 1f);
        if (EndPosition < StartPosition)
        {
            (StartPosition, EndPosition) = (EndPosition, StartPosition);
        }
    }

    public bool Equals(TextPath? other)
    {
        if (other is null) return false;
        return Start == other.Start
            && Control1 == other.Control1
            && Control2 == other.Control2
            && End == other.End
            && Offset == other.Offset
            && StartPosition == other.StartPosition
            && EndPosition == other.EndPosition
            && ReverseDirection == other.ReverseDirection;
    }

    public override bool Equals(object? obj) => obj is TextPath other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Control1, Control2, End, Offset, StartPosition, EndPosition, ReverseDirection);
    }
}
