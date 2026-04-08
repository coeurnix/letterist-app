using Letterist.Model;
using Letterist.View;
using Xunit;

namespace Letterist.Tests;

public class ViewTransformTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var vt = new ViewTransform();

        Assert.Equal(Point2.Zero, vt.PanOffset);
        Assert.Equal(1.0f, vt.Zoom);
        Assert.Equal(100f, vt.ZoomPercent);
    }

    [Fact]
    public void Zoom_ClampsToMinimum()
    {
        var vt = new ViewTransform();

        vt.Zoom = 0.01f; // Below minimum of 0.1

        Assert.Equal(ViewTransform.MinZoom, vt.Zoom);
    }

    [Fact]
    public void Zoom_ClampsToMaximum()
    {
        var vt = new ViewTransform();

        vt.Zoom = 100f; // Above maximum of 8.0

        Assert.Equal(ViewTransform.MaxZoom, vt.Zoom);
    }

    [Fact]
    public void ZoomPercent_ConvertsCorrectly()
    {
        var vt = new ViewTransform();

        vt.ZoomPercent = 50f;

        Assert.Equal(0.5f, vt.Zoom);
        Assert.Equal(50f, vt.ZoomPercent);
    }

    [Fact]
    public void WorldToScreen_AtDefaultZoom_ReturnsOffsetPoint()
    {
        var vt = new ViewTransform();
        vt.PanOffset = new Point2(100, 50);

        var screen = vt.WorldToScreen(new Point2(10, 20));

        Assert.Equal(110f, screen.X); // 10 * 1 + 100
        Assert.Equal(70f, screen.Y);  // 20 * 1 + 50
    }

    [Fact]
    public void ScreenToWorld_AtDefaultZoom_ReturnsOffsetPoint()
    {
        var vt = new ViewTransform();
        vt.PanOffset = new Point2(100, 50);

        var world = vt.ScreenToWorld(new Point2(110, 70));

        Assert.Equal(10f, world.X);
        Assert.Equal(20f, world.Y);
    }

    [Fact]
    public void WorldToScreen_WithZoom_ScalesCorrectly()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;
        vt.PanOffset = new Point2(0, 0);

        var screen = vt.WorldToScreen(new Point2(100, 50));

        Assert.Equal(200f, screen.X); // 100 * 2
        Assert.Equal(100f, screen.Y); // 50 * 2
    }

    [Fact]
    public void ScreenToWorld_WithZoom_ScalesCorrectly()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;
        vt.PanOffset = new Point2(0, 0);

        var world = vt.ScreenToWorld(new Point2(200, 100));

        Assert.Equal(100f, world.X); // 200 / 2
        Assert.Equal(50f, world.Y);  // 100 / 2
    }

    [Fact]
    public void WorldToScreen_ScreenToWorld_RoundTrip()
    {
        var vt = new ViewTransform();
        vt.Zoom = 1.5f;
        vt.PanOffset = new Point2(75, -30);

        var original = new Point2(123.45f, -67.89f);
        var screen = vt.WorldToScreen(original);
        var roundTrip = vt.ScreenToWorld(screen);

        Assert.Equal(original.X, roundTrip.X, precision: 4);
        Assert.Equal(original.Y, roundTrip.Y, precision: 4);
    }

    [Fact]
    public void WorldToScreenSize_ScalesCorrectly()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;

        var screen = vt.WorldToScreenSize(new Size2(100, 50));

        Assert.Equal(200f, screen.Width);
        Assert.Equal(100f, screen.Height);
    }

    [Fact]
    public void ScreenToWorldSize_ScalesCorrectly()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;

        var world = vt.ScreenToWorldSize(new Size2(200, 100));

        Assert.Equal(100f, world.Width);
        Assert.Equal(50f, world.Height);
    }

    [Fact]
    public void Pan_MovesOffset()
    {
        var vt = new ViewTransform();
        vt.PanOffset = new Point2(10, 20);

        vt.Pan(new Point2(5, -10));

        Assert.Equal(15f, vt.PanOffset.X);
        Assert.Equal(10f, vt.PanOffset.Y);
    }

    [Fact]
    public void ZoomAt_CentersOnPoint()
    {
        var vt = new ViewTransform();
        vt.ViewportSize = new Size2(800, 600);
        vt.PanOffset = new Point2(0, 0);
        vt.Zoom = 1.0f;

        var centerScreen = new Point2(400, 300);
        var worldBeforeZoom = vt.ScreenToWorld(centerScreen);

        vt.ZoomAt(2.0f, centerScreen);

        var worldAfterZoom = vt.ScreenToWorld(centerScreen);

        Assert.Equal(worldBeforeZoom.X, worldAfterZoom.X, precision: 2);
        Assert.Equal(worldBeforeZoom.Y, worldAfterZoom.Y, precision: 2);
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.5f;
        vt.PanOffset = new Point2(100, 200);

        vt.Reset();

        Assert.Equal(ViewTransform.DefaultZoom, vt.Zoom);
        Assert.Equal(Point2.Zero, vt.PanOffset);
    }

    [Fact]
    public void ZoomToFit_FitsRectangleInViewport()
    {
        var vt = new ViewTransform();
        vt.ViewportSize = new Size2(800, 600);

        var worldRect = new Rect(0, 0, 400, 300);
        vt.ZoomToFit(worldRect, padding: 0);

        var visibleRect = vt.GetVisibleWorldRect();
        Assert.True(visibleRect.Contains(new Point2(200, 150)));
    }

    [Fact]
    public void GetVisibleWorldRect_ReturnsCorrectRect()
    {
        var vt = new ViewTransform();
        vt.ViewportSize = new Size2(800, 600);
        vt.Zoom = 1.0f;
        vt.PanOffset = new Point2(0, 0);

        var visible = vt.GetVisibleWorldRect();

        Assert.Equal(0f, visible.X);
        Assert.Equal(0f, visible.Y);
        Assert.Equal(800f, visible.Width);
        Assert.Equal(600f, visible.Height);
    }

    [Fact]
    public void TransformChanged_FiresOnZoomChange()
    {
        var vt = new ViewTransform();
        var eventFired = false;
        vt.TransformChanged += (s, e) => eventFired = true;

        vt.Zoom = 2.0f;

        Assert.True(eventFired);
    }

    [Fact]
    public void TransformChanged_FiresOnPanOffsetChange()
    {
        var vt = new ViewTransform();
        var eventFired = false;
        vt.TransformChanged += (s, e) => eventFired = true;

        vt.PanOffset = new Point2(50, 50);

        Assert.True(eventFired);
    }

    [Fact]
    public void TransformChanged_DoesNotFireIfValueUnchanged()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;

        var eventFired = false;
        vt.TransformChanged += (s, e) => eventFired = true;

        vt.Zoom = 2.0f; // Same value

        Assert.False(eventFired);
    }

    [Fact]
    public void GetTransformMatrix_ReturnsCorrectMatrix()
    {
        var vt = new ViewTransform();
        vt.Zoom = 2.0f;
        vt.PanOffset = new Point2(100, 50);

        var matrix = vt.GetTransformMatrix();

        var worldPoint = new System.Numerics.Vector2(10, 20);
        var transformedPoint = System.Numerics.Vector2.Transform(worldPoint, matrix);

        Assert.Equal(120f, transformedPoint.X); // 10 * 2 + 100
        Assert.Equal(90f, transformedPoint.Y);  // 20 * 2 + 50
    }

    [Fact]
    public void CenterOn_CentersViewOnPoint()
    {
        var vt = new ViewTransform();
        vt.ViewportSize = new Size2(800, 600);
        vt.Zoom = 1.0f;

        vt.CenterOn(new Point2(100, 100));

        var viewportCenter = new Point2(400, 300);
        var worldAtCenter = vt.ScreenToWorld(viewportCenter);

        Assert.Equal(100f, worldAtCenter.X, precision: 2);
        Assert.Equal(100f, worldAtCenter.Y, precision: 2);
    }

    [Fact]
    public void ZoomTo100_SetsZoomToDefaultWhileKeepingCenter()
    {
        var vt = new ViewTransform();
        vt.ViewportSize = new Size2(800, 600);
        vt.Zoom = 0.5f;
        vt.CenterOn(new Point2(200, 200));

        var centerScreen = new Point2(400, 300);
        var worldBeforeZoom = vt.ScreenToWorld(centerScreen);

        vt.ZoomTo100();

        Assert.Equal(1.0f, vt.Zoom);

        var worldAfterZoom = vt.ScreenToWorld(centerScreen);
        Assert.Equal(worldBeforeZoom.X, worldAfterZoom.X, precision: 2);
        Assert.Equal(worldBeforeZoom.Y, worldAfterZoom.Y, precision: 2);
    }
}
