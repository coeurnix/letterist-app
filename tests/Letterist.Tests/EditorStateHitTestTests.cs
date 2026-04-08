using Letterist.Commands;
using Letterist.Model;
using Letterist.Rendering;
using Letterist.View;
using Xunit;

namespace Letterist.Tests;

public class EditorStateHitTestTests
{
    private static EditorState CreateStateWithBalloon(Point2 position, Size2 size)
    {
        var state = new EditorState();
        state.NewDocument("Test", new Size2(800, 600));

        state.ViewTransform.Zoom = 1f;
        state.ViewTransform.PanOffset = Point2.Zero;

        var doc = state.Document!;
        var create = new CreateBalloonCommand(doc.ActiveLayerId, position, "Test");
        state.Execute(create);

        var resize = new ResizeBalloonCommand(create.CreatedBalloonId, size, position);
        state.Execute(resize);

        state.SelectBalloon(create.CreatedBalloonId);
        return state;
    }

    private static (EditorState state, Guid imageId) CreateStateWithConstrainedImage(Rect imageBounds, Rect panelBounds)
    {
        var state = new EditorState();
        state.NewDocument("Test", new Size2(800, 600));
        state.ViewTransform.Zoom = 1f;
        state.ViewTransform.PanOffset = Point2.Zero;

        var doc = state.Document!;
        var createImage = new CreateFloatingImageCommand(doc.ActivePageId, "test.png", imageBounds);
        state.Execute(createImage);

        var createPanel = new CreatePanelZoneCommand(doc.ActivePageId, "Panel 1", panelBounds, order: 1);
        state.Execute(createPanel);

        state.Execute(new SetFloatingImagePanelCommand(doc.ActivePageId, createImage.CreatedImageId, createPanel.CreatedPanelId));
        state.Execute(new SetFloatingImageConstrainToPanelCommand(doc.ActivePageId, createImage.CreatedImageId, true));

        return (state, createImage.CreatedImageId);
    }

    [Fact]
    public void HitTestBalloon_UsesRotation()
    {
        var center = new Point2(200, 200);
        var size = new Size2(100, 100);
        var state = CreateStateWithBalloon(center, size);

        var balloon = state.Document!.SelectedBalloon!;
        state.Execute(new RotateBalloonCommand(balloon.Id, 45f));

        var interiorPoint = new Point2(center.X + 10f, center.Y);
        var rotatedPoint = BalloonGeometry.RotatePointAround(interiorPoint, center, MathF.PI / 4f);
        var screenPoint = state.ViewTransform.WorldToScreen(rotatedPoint);

        var hit = state.HitTestBalloon(screenPoint);

        Assert.NotNull(hit);
        Assert.Equal(balloon.Id, hit!.Id);
    }

    [Fact]
    public void HitTestRotationHandle_ReturnsSelectedBalloon()
    {
        var state = CreateStateWithBalloon(new Point2(200, 200), new Size2(120, 80));
        var balloon = state.Document!.SelectedBalloon!;

        var handleWorld = BalloonGeometry.GetRotationHandlePosition(balloon);
        var screenPoint = state.ViewTransform.WorldToScreen(handleWorld);

        var hit = state.HitTestRotationHandle(screenPoint);

        Assert.NotNull(hit);
        Assert.Equal(balloon.Id, hit!.Id);
    }

    [Fact]
    public void HitTestResizeHandle_PrefersNearestHandleWhenHitRadiiOverlap()
    {
        var state = CreateStateWithBalloon(new Point2(200, 200), new Size2(40, 40));
        state.ViewTransform.Zoom = 0.1f;

        var balloon = state.Document!.SelectedBalloon!;
        var inflatedBounds = balloon.Bounds.Inflate(4, 4);
        var screenPoint = state.ViewTransform.WorldToScreen(inflatedBounds.BottomRight);

        var hit = state.HitTestResizeHandle(screenPoint);

        Assert.Equal(ResizeHandle.BottomRight, hit);
    }

    [Fact]
    public void HitTestFloatingImage_ConstrainedToPanel_IgnoresInvisibleAreaOutsidePanel()
    {
        var (state, imageId) = CreateStateWithConstrainedImage(
            imageBounds: new Rect(40, 40, 220, 220),
            panelBounds: new Rect(120, 120, 80, 80));

        var screenPoint = state.ViewTransform.WorldToScreen(new Point2(80, 80));

        var hit = state.HitTestFloatingImage(screenPoint);

        Assert.Null(hit);
        Assert.DoesNotContain(imageId, state.SelectedFloatingImageIds);
    }

    [Fact]
    public void HitTestFloatingImage_ConstrainedToPanel_HitsInsidePanelArea()
    {
        var (state, imageId) = CreateStateWithConstrainedImage(
            imageBounds: new Rect(40, 40, 220, 220),
            panelBounds: new Rect(120, 120, 80, 80));

        var screenPoint = state.ViewTransform.WorldToScreen(new Point2(140, 140));

        var hit = state.HitTestFloatingImage(screenPoint);

        Assert.NotNull(hit);
        Assert.Equal(imageId, hit!.Id);
    }
}
