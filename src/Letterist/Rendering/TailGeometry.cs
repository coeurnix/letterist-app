using Letterist.Model;

namespace Letterist.Rendering;

internal static class TailGeometry
{
    public static Point2 GetRenderedTargetPoint(Balloon balloon, Tail tail)
    {
        return ToRenderedPoint(balloon, tail.TargetPoint);
    }

    public static Point2 GetRenderedAttachmentPoint(Balloon balloon, Tail tail)
    {
        var attachment = ComputeAttachmentPoint(balloon, tail);
        return ToRenderedPoint(balloon, attachment);
    }

    public static Point2 ToRenderedPoint(Balloon balloon, Point2 point)
    {
        if (MathF.Abs(balloon.Rotation) <= 0.01f)
        {
            return point;
        }

        var radians = balloon.Rotation * MathF.PI / 180f;
        return BalloonGeometry.RotatePointAround(point, balloon.Position, radians);
    }

    public static Point2 ToTailSpacePoint(Balloon balloon, Point2 renderedPoint)
    {
        if (MathF.Abs(balloon.Rotation) <= 0.01f)
        {
            return renderedPoint;
        }

        var radians = balloon.Rotation * MathF.PI / 180f;
        return BalloonGeometry.RotatePointAround(renderedPoint, balloon.Position, -radians);
    }

    public static Point2 ComputeAttachmentPoint(Balloon balloon, Tail tail)
    {
        var direction = tail.AttachmentDirection ?? (tail.TargetPoint - balloon.Position);
        var attachment = ComputeAttachmentPoint(balloon, direction);
        if (tail.Inset <= 0.001f)
        {
            return attachment;
        }

        var towardCenter = balloon.Position - attachment;
        var distanceToCenter = towardCenter.Length;
        if (distanceToCenter <= 0.001f)
        {
            return attachment;
        }

        var inset = MathF.Min(tail.Inset, MathF.Max(0f, distanceToCenter - 1f));
        return attachment + (towardCenter / distanceToCenter) * inset;
    }

    public static Point2 ComputeAttachmentPoint(Balloon balloon, Point2 direction)
    {
        var center = balloon.Position;
        var bounds = balloon.Bounds;
        var length = direction.Length;

        if (length <= 0.0001f)
        {
            return center;
        }

        var normalized = direction / length;

        if (balloon.Shape == BalloonShape.Burst)
        {
            if (TryComputeBurstAttachment(center, bounds, normalized, balloon.BalloonStyle.ThoughtSmoothness, out var burstPoint))
            {
                return burstPoint;
            }
        }

        if (balloon.Shape == BalloonShape.Oval || balloon.Shape == BalloonShape.Thought || balloon.Shape == BalloonShape.Splat || balloon.Shape == BalloonShape.Whisper)
        {
            var a = bounds.Width / 2;
            var b = bounds.Height / 2;
            var t = MathF.Atan2(normalized.Y * a, normalized.X * b);
            return new Point2(
                center.X + a * MathF.Cos(t),
                center.Y + b * MathF.Sin(t));
        }

        var halfW = bounds.Width / 2;
        var halfH = bounds.Height / 2;

        float scaleX = normalized.X != 0 ? halfW / MathF.Abs(normalized.X) : float.MaxValue;
        float scaleY = normalized.Y != 0 ? halfH / MathF.Abs(normalized.Y) : float.MaxValue;
        float scale = MathF.Min(scaleX, scaleY);

        return center + normalized * scale;
    }

    private static bool TryComputeBurstAttachment(Point2 center, Rect bounds, Point2 direction, float smoothness, out Point2 attachment)
    {
        attachment = center;

        var outerRadiusX = bounds.Width / 2f;
        var outerRadiusY = bounds.Height / 2f;
        if (outerRadiusX <= 0f || outerRadiusY <= 0f) return false;

        var averageRadius = (outerRadiusX + outerRadiusY) * 0.5f;
        var clampedSmoothness = Math.Clamp(smoothness, 0f, 1f);
        var baseSpikeCount = Math.Clamp((int)(averageRadius / 6f), 10, 24);
        var smoothnessScale = 1.35f - (clampedSmoothness * 0.7f);
        var spikeCount = Math.Clamp((int)MathF.Round(baseSpikeCount * smoothnessScale), 8, 36);
        var totalPoints = spikeCount * 2;
        var angleStep = (MathF.PI * 2f) / totalPoints;

        var innerRadiusX = outerRadiusX * 0.6f;
        var innerRadiusY = outerRadiusY * 0.6f;

        var points = new Point2[totalPoints];
        for (int i = 0; i < totalPoints; i++)
        {
            var angle = angleStep * i;
            var useOuter = i % 2 == 0;
            var radiusX = useOuter ? outerRadiusX : innerRadiusX;
            var radiusY = useOuter ? outerRadiusY : innerRadiusY;
            points[i] = new Point2(
                center.X + MathF.Cos(angle) * radiusX,
                center.Y + MathF.Sin(angle) * radiusY);
        }

        var bestT = float.MaxValue;
        for (int i = 0; i < totalPoints; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % totalPoints];
            if (TryIntersectRaySegment(center, direction, a, b, out var t))
            {
                if (t < bestT)
                {
                    bestT = t;
                }
            }
        }

        if (bestT < float.MaxValue)
        {
            attachment = center + direction * bestT;
            return true;
        }

        return false;
    }

    private static bool TryIntersectRaySegment(Point2 origin, Point2 direction, Point2 a, Point2 b, out float t)
    {
        t = 0f;
        var v2 = b - a;
        var cross = Cross(direction, v2);
        if (MathF.Abs(cross) < 0.0001f) return false;

        var diff = a - origin;
        var t1 = Cross(diff, v2) / cross;
        var u = Cross(diff, direction) / cross;

        if (t1 < 0f || u < 0f || u > 1f) return false;

        t = t1;
        return true;
    }

    private static float Cross(Point2 a, Point2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }
}
