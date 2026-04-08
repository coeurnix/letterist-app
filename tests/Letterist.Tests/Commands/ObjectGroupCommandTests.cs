using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class ObjectGroupCommandTests
{
    [Fact]
    public void GroupObjectsCommand_ExecuteUndo_CreatesAndRestoresGroup()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;

        var createBalloon = new CreateBalloonCommand(layerId, new Point2(100, 100), "A");
        createBalloon.Execute(doc);
        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(40, 50, 120, 80));
        createImage.Execute(doc);

        var group = new GroupObjectsCommand(
            page.Id,
            new[] { createBalloon.CreatedBalloonId },
            new[] { createImage.CreatedImageId });

        group.Execute(doc);
        Assert.Single(page.ObjectGroups);
        Assert.Contains(createBalloon.CreatedBalloonId, page.ObjectGroups[0].BalloonIds);
        Assert.Contains(createImage.CreatedImageId, page.ObjectGroups[0].FloatingImageIds);

        group.Undo(doc);
        Assert.Empty(page.ObjectGroups);
    }

    [Fact]
    public void UngroupObjectsCommand_RemovesSelectedMembersFromGroup()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;

        var createA = new CreateBalloonCommand(layerId, new Point2(100, 100), "A");
        var createB = new CreateBalloonCommand(layerId, new Point2(200, 100), "B");
        createA.Execute(doc);
        createB.Execute(doc);

        var group = new GroupObjectsCommand(page.Id, new[] { createA.CreatedBalloonId, createB.CreatedBalloonId }, Array.Empty<Guid>());
        group.Execute(doc);
        Assert.Single(page.ObjectGroups);

        var ungroup = new UngroupObjectsCommand(page.Id, new[] { createA.CreatedBalloonId }, Array.Empty<Guid>());
        ungroup.Execute(doc);

        Assert.Empty(page.ObjectGroups);
    }
}
