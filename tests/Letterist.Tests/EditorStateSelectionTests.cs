using Letterist.Commands;
using Letterist.Model;
using Letterist.View;
using Xunit;

namespace Letterist.Tests;

public class EditorStateSelectionTests
{
    private static (EditorState state, Guid balloonId, Guid imageId) CreateStateWithBalloonAndImage()
    {
        var state = new EditorState();
        state.NewDocument("Selection Test", new Size2(800, 600));

        var doc = state.Document!;
        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(140, 140), "Test");
        state.Execute(createBalloon);

        var createImage = new CreateFloatingImageCommand(
            doc.ActivePageId,
            imagePath: "test.png",
            bounds: new Rect(240, 120, 100, 80));
        state.Execute(createImage);

        return (state, createBalloon.CreatedBalloonId, createImage.CreatedImageId);
    }

    [Fact]
    public void SelectFloatingImage_PreserveBalloonSelection_KeepsMixedSelection()
    {
        var (state, balloonId, imageId) = CreateStateWithBalloonAndImage();

        state.SelectBalloon(balloonId);
        state.SelectFloatingImage(imageId, preserveBalloonSelection: true);

        Assert.Contains(balloonId, state.SelectedBalloonIds);
        Assert.Contains(imageId, state.SelectedFloatingImageIds);
        Assert.Equal(imageId, state.SelectedFloatingImageId);
    }

    [Fact]
    public void ToggleBalloonSelection_PreserveFloatingImageSelection_KeepsMixedSelection()
    {
        var (state, balloonId, imageId) = CreateStateWithBalloonAndImage();

        state.SelectFloatingImage(imageId);
        state.ToggleBalloonSelection(balloonId, preserveFloatingImageSelection: true);

        Assert.Contains(balloonId, state.SelectedBalloonIds);
        Assert.Contains(imageId, state.SelectedFloatingImageIds);
    }

    [Fact]
    public void SetFloatingImageSelection_PreserveBalloonSelection_KeepsBalloonAndTracksPrimary()
    {
        var (state, balloonId, imageId) = CreateStateWithBalloonAndImage();
        var doc = state.Document!;

        var createSecondImage = new CreateFloatingImageCommand(
            doc.ActivePageId,
            imagePath: "test-2.png",
            bounds: new Rect(380, 160, 120, 90));
        state.Execute(createSecondImage);

        state.SelectBalloon(balloonId);
        state.SetFloatingImageSelection(
            new[] { imageId, createSecondImage.CreatedImageId },
            primaryImageId: createSecondImage.CreatedImageId,
            preserveBalloonSelection: true);

        Assert.Contains(balloonId, state.SelectedBalloonIds);
        Assert.Contains(imageId, state.SelectedFloatingImageIds);
        Assert.Contains(createSecondImage.CreatedImageId, state.SelectedFloatingImageIds);
        Assert.Equal(createSecondImage.CreatedImageId, state.SelectedFloatingImageId);
    }

    [Fact]
    public void UndoRedo_ReplaysSelectionChanges_WhenNoDocumentCommandIntervenes()
    {
        var state = new EditorState();
        state.NewDocument("Selection history", new Size2(800, 600));
        var doc = state.Document!;

        var createA = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(120, 120), "A");
        var createB = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(260, 120), "B");
        state.Execute(createA);
        state.Execute(createB);

        state.SelectBalloon(createA.CreatedBalloonId);
        state.SelectBalloon(createB.CreatedBalloonId);
        Assert.Equal(createB.CreatedBalloonId, doc.SelectedBalloonId);

        var undoResult = state.Undo();

        Assert.True(undoResult);
        Assert.Equal(createA.CreatedBalloonId, doc.SelectedBalloonId);

        var redoResult = state.Redo();

        Assert.True(redoResult);
        Assert.Equal(createB.CreatedBalloonId, doc.SelectedBalloonId);
    }

    [Fact]
    public void Undo_AfterCommand_PrefersCommandHistoryOverOlderSelectionChanges()
    {
        var state = new EditorState();
        state.NewDocument("Selection command boundary", new Size2(800, 600));
        var doc = state.Document!;

        var create = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(180, 180), "A");
        state.Execute(create);
        state.SelectBalloon(create.CreatedBalloonId);

        var move = new MoveBalloonCommand(create.CreatedBalloonId, new Point2(300, 280));
        state.Execute(move);
        Assert.Equal(new Point2(300, 280), doc.FindBalloon(create.CreatedBalloonId)!.Position);

        var undoResult = state.Undo();

        Assert.True(undoResult);
        Assert.Equal(new Point2(180, 180), doc.FindBalloon(create.CreatedBalloonId)!.Position);
        Assert.Equal(create.CreatedBalloonId, doc.SelectedBalloonId);
    }
}
