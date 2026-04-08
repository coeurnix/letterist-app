using Letterist.Commands;
using Letterist.Model;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Commands;

public class CopyBalloonsToPageCommandTests
{
    [Fact]
    public void CopyBalloonsToPageCommand_CopiesBalloonsAndLinks()
    {
        var doc = Document.Create("Test", new Size2(800, 600));
        var sourcePageId = doc.ActivePageId;
        var sourceLayerId = doc.GetPreferredBalloonLayerId();

        var createBalloon1 = new CreateBalloonCommand(sourceLayerId, new Point2(100, 100), "One");
        createBalloon1.Execute(doc);
        var balloonId1 = createBalloon1.CreatedBalloonId;

        var createBalloon2 = new CreateBalloonCommand(sourceLayerId, new Point2(200, 200), "Two");
        createBalloon2.Execute(doc);
        var balloonId2 = createBalloon2.CreatedBalloonId;

        new CreateTailCommand(balloonId1, new Point2(120, 140)).Execute(doc);
        new CreateTailCommand(balloonId1, new Point2(130, 160)).Execute(doc);
        new LinkBalloonsCommand(balloonId1, balloonId2).Execute(doc);

        var createPage = new CreatePageCommand("Page 2", doc.Size, setActive: false);
        createPage.Execute(doc);
        var targetPageId = createPage.CreatedPageId;

        var cmd = new CopyBalloonsToPageCommand(sourcePageId, targetPageId, new[] { balloonId1, balloonId2 });
        cmd.Execute(doc);

        var targetPage = doc.FindPage(targetPageId)!;
        var copied = targetPage.AllBalloons.ToList();
        Assert.Equal(2, copied.Count);
        Assert.Contains(copied, balloon => balloon.Text == "One");
        Assert.Contains(copied, balloon => balloon.Text == "Two");
        Assert.Single(targetPage.BalloonLinks);

        var copiedOne = copied.First(balloon => balloon.Text == "One");
        Assert.Equal(2, copiedOne.Tails.Count);

        cmd.Undo(doc);
        Assert.Empty(targetPage.AllBalloons);
        Assert.Empty(targetPage.BalloonLinks);

        cmd.Execute(doc);
        Assert.Equal(2, targetPage.AllBalloons.Count());
        Assert.Single(targetPage.BalloonLinks);
    }
}
