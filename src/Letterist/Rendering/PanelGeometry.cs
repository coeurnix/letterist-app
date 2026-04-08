using Letterist.Model;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;

namespace Letterist.Rendering;

internal static class PanelGeometry
{
    public static CanvasGeometry CreateGeometry(ICanvasResourceCreator creator, PanelZone panel)
    {
        var bounds = panel.Bounds;
        return panel.Shape switch
        {
            PanelShape.Ellipse => CanvasGeometry.CreateEllipse(creator, bounds.Center.ToVector2(), bounds.Width / 2f, bounds.Height / 2f),
            PanelShape.RoundedRect => CanvasGeometry.CreateRoundedRectangle(
                creator,
                bounds.ToWindowsRect(),
                MathF.Min(panel.CornerRadius, bounds.Width / 2f),
                MathF.Min(panel.CornerRadius, bounds.Height / 2f)),
            PanelShape.Custom => TryCreateCustomGeometry(creator, panel) ?? CanvasGeometry.CreateRectangle(creator, bounds.ToWindowsRect()),
            _ => CanvasGeometry.CreateRectangle(creator, bounds.ToWindowsRect())
        };
    }

    private static CanvasGeometry? TryCreateCustomGeometry(ICanvasResourceCreator creator, PanelZone panel)
    {
        var pathData = panel.CustomShapePathData;
        if (string.IsNullOrWhiteSpace(pathData)) return null;

        var geometry = SvgPathParser.TryCreateGeometry(creator, pathData);
        if (geometry == null) return null;

        var pathBounds = geometry.ComputeBounds();
        if (pathBounds.Width <= 0 || pathBounds.Height <= 0)
        {
            geometry.Dispose();
            return null;
        }

        var bounds = panel.Bounds;
        var scaleX = bounds.Width / (float)pathBounds.Width;
        var scaleY = bounds.Height / (float)pathBounds.Height;
        var translate = new Vector2(
            bounds.X - (float)pathBounds.X * scaleX,
            bounds.Y - (float)pathBounds.Y * scaleY);

        var transform = Matrix3x2.CreateScale(scaleX, scaleY) * Matrix3x2.CreateTranslation(translate);
        var transformed = geometry.Transform(transform);
        geometry.Dispose();
        return transformed;
    }
}
