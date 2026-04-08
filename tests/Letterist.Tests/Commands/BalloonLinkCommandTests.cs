using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class BalloonLinkCommandTests
{
    private static Document CreateTestDocument()
    {
        return Document.Create("Test");
    }

    private static Layer GetFirstBalloonLayer(Document doc)
    {
        return doc.BalloonLayers.First();
    }

    private static (Guid FirstId, Guid SecondId) CreateTwoBalloons(Document doc)
    {
        var layerId = GetFirstBalloonLayer(doc).Id;
        var firstCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        var secondCmd = new CreateBalloonCommand(layerId, new Point2(220, 140));

        firstCmd.Execute(doc);
        secondCmd.Execute(doc);

        return (firstCmd.CreatedBalloonId, secondCmd.CreatedBalloonId);
    }

    [Fact]
    public void LinkBalloonsCommand_AddsLink()
    {
        var doc = CreateTestDocument();
        var (firstId, secondId) = CreateTwoBalloons(doc);

        var cmd = new LinkBalloonsCommand(firstId, secondId);
        cmd.Execute(doc);

        Assert.True(doc.ActivePage!.AreBalloonsLinked(firstId, secondId));
        Assert.Single(doc.ActivePage.BalloonLinks);
    }

    [Fact]
    public void LinkBalloonsCommand_Undo_RemovesLink()
    {
        var doc = CreateTestDocument();
        var (firstId, secondId) = CreateTwoBalloons(doc);

        var cmd = new LinkBalloonsCommand(firstId, secondId);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.False(doc.ActivePage!.AreBalloonsLinked(firstId, secondId));
        Assert.Empty(doc.ActivePage.BalloonLinks);
    }

    [Fact]
    public void UnlinkBalloonsCommand_RemovesLink()
    {
        var doc = CreateTestDocument();
        var (firstId, secondId) = CreateTwoBalloons(doc);
        new LinkBalloonsCommand(firstId, secondId).Execute(doc);

        var cmd = new UnlinkBalloonsCommand(firstId, secondId);
        cmd.Execute(doc);

        Assert.False(doc.ActivePage!.AreBalloonsLinked(firstId, secondId));
        Assert.Empty(doc.ActivePage.BalloonLinks);
    }

    [Fact]
    public void DeleteBalloonCommand_RemovesAndRestoresLinks()
    {
        var doc = CreateTestDocument();
        var (firstId, secondId) = CreateTwoBalloons(doc);
        new LinkBalloonsCommand(firstId, secondId).Execute(doc);

        var deleteCmd = new DeleteBalloonCommand(firstId);
        deleteCmd.Execute(doc);

        Assert.Empty(doc.ActivePage!.BalloonLinks);

        deleteCmd.Undo(doc);

        Assert.True(doc.ActivePage.AreBalloonsLinked(firstId, secondId));
        Assert.Single(doc.ActivePage.BalloonLinks);
    }

    [Fact]
    public void SetBalloonLinkStyleCommand_UpdatesAndUndoRestores()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var original = page.BalloonLinkStyle;

        var updated = new BalloonLinkStyle
        {
            StrokeColor = new Color(10, 20, 30, 200),
            StrokeWidth = 3.5f,
            DashStyle = LinkDashStyle.Dash
        };

        var cmd = new SetBalloonLinkStyleCommand(page.Id, updated);
        cmd.Execute(doc);

        Assert.Equal(updated.StrokeColor, page.BalloonLinkStyle.StrokeColor);
        Assert.Equal(updated.StrokeWidth, page.BalloonLinkStyle.StrokeWidth);
        Assert.Equal(updated.DashStyle, page.BalloonLinkStyle.DashStyle);

        cmd.Undo(doc);

        Assert.Equal(original.StrokeColor, page.BalloonLinkStyle.StrokeColor);
        Assert.Equal(original.StrokeWidth, page.BalloonLinkStyle.StrokeWidth);
        Assert.Equal(original.DashStyle, page.BalloonLinkStyle.DashStyle);
    }

    [Fact]
    public void ClearBalloonLinksCommand_RemovesAndUndoRestores()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var (firstId, secondId) = CreateTwoBalloons(doc);
        new LinkBalloonsCommand(firstId, secondId).Execute(doc);

        var cmd = new ClearBalloonLinksCommand(page.Id);
        cmd.Execute(doc);

        Assert.Empty(page.BalloonLinks);

        cmd.Undo(doc);

        Assert.True(page.AreBalloonsLinked(firstId, secondId));
        Assert.Single(page.BalloonLinks);
    }
}
