using Letterist.Commands;
using Letterist.Model;
using Letterist.View;
using Xunit;

namespace Letterist.Tests;

public class ToolbarStateResolverTests
{
    [Fact]
    public void Resolve_NoDocument_ReturnsSelectContext()
    {
        var state = new EditorState();

        var snapshot = ToolbarStateResolver.Resolve(state);

        Assert.False(snapshot.HasDocument);
        Assert.Equal(ToolbarContextKind.Select, snapshot.Context);
        Assert.False(snapshot.CanUndo);
        Assert.False(snapshot.CanRedo);
    }

    [Fact]
    public void Resolve_BalloonSelected_ReturnsBalloonContext()
    {
        var state = new EditorState();
        state.NewDocument("Toolbar", new Size2(800, 600));

        var doc = state.Document!;
        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(100, 120), "Hi");
        state.Execute(createBalloon);
        state.SelectBalloon(createBalloon.CreatedBalloonId);

        var snapshot = ToolbarStateResolver.Resolve(state);

        Assert.Equal(ToolbarContextKind.Balloon, snapshot.Context);
        Assert.True(snapshot.HasBalloonSelection);
        Assert.Equal(1, snapshot.SelectedBalloonCount);
    }

    [Fact]
    public void Resolve_TextEditMode_ReturnsTextEditContext()
    {
        var state = new EditorState();
        state.NewDocument("Toolbar", new Size2(800, 600));

        var doc = state.Document!;
        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(100, 120), "Hi");
        state.Execute(createBalloon);
        state.EnterTextEditMode(createBalloon.CreatedBalloonId);

        var snapshot = ToolbarStateResolver.Resolve(state);

        Assert.Equal(ToolbarContextKind.TextEdit, snapshot.Context);
    }

    [Fact]
    public void Resolve_PanelLayoutMode_ReturnsPanelLayoutContext()
    {
        var state = new EditorState();
        state.NewDocument("Toolbar", new Size2(800, 600));

        var doc = state.Document!;
        var createPanel = new CreatePanelZoneCommand(doc.ActivePageId, "Panel 1", new Rect(20, 20, 200, 180), 1);
        state.Execute(createPanel);
        state.SelectPanel(createPanel.CreatedPanelId);
        state.Mode = EditorMode.PanelLayout;

        var snapshot = ToolbarStateResolver.Resolve(state);

        Assert.Equal(ToolbarContextKind.PanelLayout, snapshot.Context);
        Assert.True(snapshot.HasPanelSelection);
    }

    [Fact]
    public void Resolve_FloatingImageSelected_ReturnsFloatingContext()
    {
        var state = new EditorState();
        state.NewDocument("Toolbar", new Size2(800, 600));

        var doc = state.Document!;
        var createImage = new CreateFloatingImageCommand(
            doc.ActivePageId,
            imagePath: "test.png",
            bounds: new Rect(200, 150, 120, 90));
        state.Execute(createImage);
        state.SelectFloatingImage(createImage.CreatedImageId);

        var snapshot = ToolbarStateResolver.Resolve(state);

        Assert.Equal(ToolbarContextKind.FloatingImage, snapshot.Context);
        Assert.True(snapshot.HasFloatingImageSelection);
    }

    [Fact]
    public void Resolve_TracksUndoRedoAvailability()
    {
        var state = new EditorState();
        state.NewDocument("Toolbar", new Size2(800, 600));

        var doc = state.Document!;
        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(100, 120), "Hi");
        state.Execute(createBalloon);

        var afterExecute = ToolbarStateResolver.Resolve(state);
        Assert.True(afterExecute.CanUndo);
        Assert.False(afterExecute.CanRedo);

        state.Undo();

        var afterUndo = ToolbarStateResolver.Resolve(state);
        Assert.False(afterUndo.CanUndo);
        Assert.True(afterUndo.CanRedo);
    }
}
