using Letterist.Model;

namespace Letterist.Rendering;

internal static class BalloonGeometry
{
    private const float RotationHandleOffset = 30f;
    private const float RotationHandleLineInset = 4f;

    public static Rect GetRotatedBounds(Balloon balloon)
    {
        var bounds = balloon.Bounds;
        if (MathF.Abs(balloon.Rotation) <= 0.01f)
        {
            return bounds;
        }

        var center = bounds.Center;
        var rotationRadians = balloon.Rotation * MathF.PI / 180f;
        var sin = MathF.Sin(rotationRadians);
        var cos = MathF.Cos(rotationRadians);

        var corners = new Point2[]
        {
            bounds.TopLeft,
            bounds.TopRight,
            bounds.BottomLeft,
            bounds.BottomRight
        };

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        foreach (var corner in corners)
        {
            var dx = corner.X - center.X;
            var dy = corner.Y - center.Y;
            var rotatedX = center.X + dx * cos - dy * sin;
            var rotatedY = center.Y + dx * sin + dy * cos;

            minX = MathF.Min(minX, rotatedX);
            maxX = MathF.Max(maxX, rotatedX);
            minY = MathF.Min(minY, rotatedY);
            maxY = MathF.Max(maxY, rotatedY);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public static Point2 RotatePointAround(Point2 point, Point2 center, float rotationRadians)
    {
        var sin = MathF.Sin(rotationRadians);
        var cos = MathF.Cos(rotationRadians);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;

        return new Point2(
            center.X + dx * cos - dy * sin,
            center.Y + dx * sin + dy * cos);
    }

    public static Point2 GetRotationHandlePosition(Balloon balloon)
    {
        var bounds = balloon.Bounds;
        var center = balloon.Position;
        var rotationRadians = balloon.Rotation * MathF.PI / 180f;
        var handleDistance = bounds.Height / 2 + RotationHandleOffset;

        return new Point2(
            center.X - handleDistance * MathF.Sin(rotationRadians),
            center.Y - handleDistance * MathF.Cos(rotationRadians));
    }

    public static Point2 GetRotationHandleAnchor(Balloon balloon)
    {
        var bounds = balloon.Bounds;
        var center = balloon.Position;
        var rotationRadians = balloon.Rotation * MathF.PI / 180f;
        var anchorDistance = bounds.Height / 2 + RotationHandleLineInset;

        return new Point2(
            center.X - anchorDistance * MathF.Sin(rotationRadians),
            center.Y - anchorDistance * MathF.Cos(rotationRadians));
    }
}
