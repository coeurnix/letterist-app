using Letterist.Commands;
using Letterist.Model;
using Letterist.View;
using Xunit;

namespace Letterist.Tests.Model;

public class ObjectGroupSelectionTests
{
    [Fact]
    public void SelectBalloon_SelectsGroupedFloatingImage()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;

        var createBalloon = new CreateBalloonCommand(layerId, new Point2(120, 160), "A");
        createBalloon.Execute(doc);
        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(30, 40, 100, 80));
        createImage.Execute(doc);
        new GroupObjectsCommand(
            page.Id,
            new[] { createBalloon.CreatedBalloonId },
            new[] { createImage.CreatedImageId }).Execute(doc);

        var editor = new EditorState();
        editor.SetDocument(doc);
        editor.SelectBalloon(createBalloon.CreatedBalloonId);

        Assert.Contains(createBalloon.CreatedBalloonId, editor.SelectedBalloonIds);
        Assert.Contains(createImage.CreatedImageId, editor.SelectedFloatingImageIds);
    }

    [Fact]
    public void SelectFloatingImage_SelectsGroupedBalloon()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;

        var createBalloon = new CreateBalloonCommand(layerId, new Point2(120, 160), "A");
        createBalloon.Execute(doc);
        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(30, 40, 100, 80));
        createImage.Execute(doc);
        new GroupObjectsCommand(
            page.Id,
            new[] { createBalloon.CreatedBalloonId },
            new[] { createImage.CreatedImageId }).Execute(doc);

        var editor = new EditorState();
        editor.SetDocument(doc);
        editor.SelectFloatingImage(createImage.CreatedImageId);

        Assert.Contains(createImage.CreatedImageId, editor.SelectedFloatingImageIds);
        Assert.Contains(createBalloon.CreatedBalloonId, editor.SelectedBalloonIds);
    }

    [Fact]
    public void ToggleBalloonSelection_DeselectsWholeGroup()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;

        var createBalloon = new CreateBalloonCommand(layerId, new Point2(120, 160), "A");
        createBalloon.Execute(doc);
        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(30, 40, 100, 80));
        createImage.Execute(doc);
        new GroupObjectsCommand(
            page.Id,
            new[] { createBalloon.CreatedBalloonId },
            new[] { createImage.CreatedImageId }).Execute(doc);

        var editor = new EditorState();
        editor.SetDocument(doc);
        editor.SelectBalloon(createBalloon.CreatedBalloonId);
        editor.ToggleBalloonSelection(createBalloon.CreatedBalloonId);

        Assert.Empty(editor.SelectedBalloonIds);
        Assert.Empty(editor.SelectedFloatingImageIds);
    }
}
