using Letterist.Model;
using Microsoft.Graphics.Canvas;
using System.Numerics;

namespace Letterist.Rendering;

internal static class TextWarpRenderer
{
    private const float WarpThreshold = 0.0001f;

    public static bool IsWarpEnabled(TextStyle style)
    {
        if (style == null) return false;

        var hasPresetWarp = style.WarpPreset != TextWarpPreset.None && MathF.Abs(style.WarpIntensity) > WarpThreshold;
        var hasDistortion = MathF.Abs(style.WarpHorizontalDistortion) > WarpThreshold ||
                            MathF.Abs(style.WarpVerticalDistortion) > WarpThreshold;
        var hasMesh = style.WarpMesh != null && !style.WarpMesh.IsIdentity;
        return hasPresetWarp || hasDistortion || hasMesh;
    }

    public static Rect EstimateWarpedBounds(Rect sourceBounds, TextStyle style)
    {
        if (!IsWarpEnabled(style) || sourceBounds.Width <= 0f || sourceBounds.Height <= 0f)
        {
            return sourceBounds;
        }

        const int samples = 16;
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        for (int yi = 0; yi <= samples; yi++)
        {
            var v = yi / (float)samples;
            for (int xi = 0; xi <= samples; xi++)
            {
                var u = xi / (float)samples;
                var point = WarpPoint(sourceBounds, u, v, style);
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }
        }

        if (minX == float.MaxValue || minY == float.MaxValue)
        {
            return sourceBounds;
        }

        return new Rect(
            minX - 1f,
            minY - 1f,
            MathF.Max(1f, (maxX - minX) + 2f),
            MathF.Max(1f, (maxY - minY) + 2f));
    }

    public static Point2 GetNormalizedOffset(float u, float v, TextStyle style)
    {
        if (!IsWarpEnabled(style))
        {
            return Point2.Zero;
        }

        var safeU = Math.Clamp(u, 0f, 1f);
        var safeV = Math.Clamp(v, 0f, 1f);
        var dx = 0f;
        var dy = 0f;

        ApplyPresetWarp(ref dx, ref dy, safeU, safeV, style);
        ApplyDistortionWarp(ref dx, ref dy, safeU, safeV, style);
        ApplyMeshWarp(ref dx, ref dy, safeU, safeV, style.WarpMesh);
        return new Point2(dx, dy);
    }

    public static void DrawWarpedImage(
        CanvasDrawingSession ds,
        ICanvasImage source,
        Rect sourceBounds,
        TextStyle style,
        float opacity = 1f)
    {
        if (!IsWarpEnabled(style))
        {
            ds.DrawImage(source, sourceBounds.ToWindowsRect(), sourceBounds.ToWindowsRect(), opacity);
            return;
        }

        if (sourceBounds.Width <= 0f || sourceBounds.Height <= 0f)
        {
            return;
        }

        var columns = Math.Clamp((int)MathF.Ceiling(sourceBounds.Width / 24f), 8, 64);
        var rows = Math.Clamp((int)MathF.Ceiling(sourceBounds.Height / 24f), 4, 64);

        for (int row = 0; row < rows; row++)
        {
            var v0 = row / (float)rows;
            var v1 = (row + 1) / (float)rows;

            for (int col = 0; col < columns; col++)
            {
                var u0 = col / (float)columns;
                var u1 = (col + 1) / (float)columns;

                var sx = sourceBounds.X + (u0 * sourceBounds.Width);
                var sy = sourceBounds.Y + (v0 * sourceBounds.Height);
                var sw = MathF.Max(0.0001f, (u1 - u0) * sourceBounds.Width);
                var sh = MathF.Max(0.0001f, (v1 - v0) * sourceBounds.Height);

                var p00 = WarpPoint(sourceBounds, u0, v0, style);
                var p10 = WarpPoint(sourceBounds, u1, v0, style);
                var p01 = WarpPoint(sourceBounds, u0, v1, style);

                var matrix = BuildAffineMatrix(sx, sy, sw, sh, p00, p10, p01);
                var sourceRect = new Windows.Foundation.Rect(sx, sy, sw, sh);
                var previous = ds.Transform;
                ds.Transform = matrix * previous;
                ds.DrawImage(source, sourceRect, sourceRect, opacity);
                ds.Transform = previous;
            }
        }
    }

    private static Matrix3x2 BuildAffineMatrix(
        float sourceX,
        float sourceY,
        float sourceWidth,
        float sourceHeight,
        Point2 destTopLeft,
        Point2 destTopRight,
        Point2 destBottomLeft)
    {
        var col = (destTopRight - destTopLeft) / sourceWidth;
        var row = (destBottomLeft - destTopLeft) / sourceHeight;

        var tx = destTopLeft.X - (sourceX * col.X) - (sourceY * row.X);
        var ty = destTopLeft.Y - (sourceX * col.Y) - (sourceY * row.Y);

        return new Matrix3x2(
            col.X,
            col.Y,
            row.X,
            row.Y,
            tx,
            ty);
    }

    private static Point2 WarpPoint(Rect bounds, float u, float v, TextStyle style)
    {
        var offset = GetNormalizedOffset(u, v, style);
        var x = bounds.X + ((u + offset.X) * bounds.Width);
        var y = bounds.Y + ((v + offset.Y) * bounds.Height);
        return new Point2(x, y);
    }

    private static void ApplyPresetWarp(ref float dx, ref float dy, float u, float v, TextStyle style)
    {
        var intensity = Math.Clamp(style.WarpIntensity, -1f, 1f);
        if (MathF.Abs(intensity) <= WarpThreshold) return;

        var centeredU = (u * 2f) - 1f;
        var centeredV = (v * 2f) - 1f;

        switch (style.WarpPreset)
        {
            case TextWarpPreset.ArcUp:
                dy += -0.35f * intensity * (1f - (centeredU * centeredU));
                break;
            case TextWarpPreset.ArcDown:
                dy += 0.35f * intensity * (1f - (centeredU * centeredU));
                break;
            case TextWarpPreset.Bulge:
                dx += 0.28f * intensity * centeredU * (1f - MathF.Abs(centeredV));
                break;
            case TextWarpPreset.Pinch:
                dx += -0.28f * intensity * centeredU * (1f - MathF.Abs(centeredV));
                break;
            case TextWarpPreset.Wave:
                dy += 0.22f * intensity * MathF.Sin(u * MathF.PI * 2f);
                break;
            case TextWarpPreset.Flag:
                dy += 0.25f * intensity * MathF.Sin(u * MathF.PI) * centeredV;
                break;
        }
    }

    private static void ApplyDistortionWarp(ref float dx, ref float dy, float u, float v, TextStyle style)
    {
        dx += Math.Clamp(style.WarpHorizontalDistortion, -1f, 1f) * (v - 0.5f) * 0.65f;
        dy += Math.Clamp(style.WarpVerticalDistortion, -1f, 1f) * (u - 0.5f) * 0.65f;
    }

    private static void ApplyMeshWarp(ref float dx, ref float dy, float u, float v, TextWarpMesh? mesh)
    {
        if (mesh == null || mesh.IsIdentity) return;

        var tl = new Point2(0f, 0f) + mesh.TopLeftOffset;
        var tr = new Point2(1f, 0f) + mesh.TopRightOffset;
        var br = new Point2(1f, 1f) + mesh.BottomRightOffset;
        var bl = new Point2(0f, 1f) + mesh.BottomLeftOffset;

        var warped =
            (tl * ((1f - u) * (1f - v))) +
            (tr * (u * (1f - v))) +
            (br * (u * v)) +
            (bl * ((1f - u) * v));

        dx += warped.X - u;
        dy += warped.Y - v;
    }
}
