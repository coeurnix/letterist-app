using Letterist.Commands;
using Letterist.Model;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Commands;

public class PageCommandTests
{
    [Fact]
    public void RenamePageCommand_UpdatesPageName()
    {
        var doc = Document.Create("Test");
        var page = doc.Pages[0];
        var original = page.Name;

        var cmd = new RenamePageCommand(page.Id, "Updated Page");
        cmd.Execute(doc);

        Assert.Equal("Updated Page", doc.FindPage(page.Id)!.Name);

        cmd.Undo(doc);
        Assert.Equal(original, doc.FindPage(page.Id)!.Name);
    }

    [Fact]
    public void CreatePageCommand_AppliesDocumentDefaultBackgrounds()
    {
        var doc = Document.Create("Test");
        var dispatcher = new CommandDispatcher(doc);
        dispatcher.Execute(new SetDocumentDefaultBackgroundColorCommand(null));
        dispatcher.Execute(new SetDocumentDefaultBackgroundImageCommand(@"C:\images\default.png"));

        var cmd = new CreatePageCommand("Page 2", new Size2(800, 600), setActive: true);
        cmd.Execute(doc);

        var created = doc.FindPage(cmd.CreatedPageId);
        Assert.NotNull(created);
        Assert.Null(created!.BackgroundColor);
        Assert.Equal(@"C:\images\default.png", created.BackgroundImagePath);
    }

    [Fact]
    public void SetPageBackgroundImageCommand_UpdatesAndRestoresImagePath()
    {
        var doc = Document.Create("Test");
        var page = doc.Pages[0];
        var originalPath = page.BackgroundImagePath;

        var cmd = new SetPageBackgroundImageCommand(page.Id, @"C:\images\page-1.webp");
        cmd.Execute(doc);

        Assert.Equal(@"C:\images\page-1.webp", doc.FindPage(page.Id)!.BackgroundImagePath);

        cmd.Undo(doc);
        Assert.Equal(originalPath, doc.FindPage(page.Id)!.BackgroundImagePath);
    }

    [Fact]
    public void SetPageBackgroundImageFitModeCommand_UpdatesAndRestoresFitMode()
    {
        var doc = Document.Create("Test");
        var page = doc.Pages[0];
        var originalFitMode = page.BackgroundImageFitMode;

        var cmd = new SetPageBackgroundImageFitModeCommand(page.Id, PanelImageFitMode.Stretch);
        cmd.Execute(doc);

        Assert.Equal(PanelImageFitMode.Stretch, doc.FindPage(page.Id)!.BackgroundImageFitMode);

        cmd.Undo(doc);
        Assert.Equal(originalFitMode, doc.FindPage(page.Id)!.BackgroundImageFitMode);
    }

    [Fact]
    public void DuplicatePageCommand_CreatesCopyWithNewIdsAndSetsActivePage()
    {
        var doc = Document.Create("Test");
        var sourcePage = doc.ActivePage!;
        var originalPageCount = doc.Pages.Count;
        var sourceLayerIds = sourcePage.Layers.Select(layer => layer.Id).ToHashSet();

        var cmd = new DuplicatePageCommand(sourcePage.Id);
        cmd.Execute(doc);

        Assert.Equal(originalPageCount + 1, doc.Pages.Count);
        Assert.Equal(cmd.CreatedPageId, doc.ActivePageId);

        var copyPage = doc.FindPage(cmd.CreatedPageId);
        Assert.NotNull(copyPage);
        Assert.Equal($"{sourcePage.Name} (Copy)", copyPage!.Name);
        Assert.Equal(sourcePage.Layers.Count, copyPage.Layers.Count);
        Assert.DoesNotContain(copyPage.Layers, layer => sourceLayerIds.Contains(layer.Id));
    }

    [Fact]
    public void DuplicatePageCommand_UndoRemovesCopyAndRestoresActivePage()
    {
        var doc = Document.Create("Test");
        var originalActivePageId = doc.ActivePageId;
        var originalPageCount = doc.Pages.Count;

        var cmd = new DuplicatePageCommand(originalActivePageId);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(originalPageCount, doc.Pages.Count);
        Assert.Equal(originalActivePageId, doc.ActivePageId);
        Assert.Null(doc.FindPage(cmd.CreatedPageId));
    }

    [Fact]
    public void DuplicatePageCommand_WithBalloons_RemapLayerIdsWithoutThrowing()
    {
        var doc = Document.Create("Test");
        var sourcePage = doc.ActivePage!;
        var sourceLayer = sourcePage.GetFirstBalloonLayer();
        Assert.NotNull(sourceLayer);

        var createBalloon = new CreateBalloonCommand(
            sourceLayer!.Id,
            new Point2(240, 320),
            text: "Hello");
        createBalloon.Execute(doc);

        var cmd = new DuplicatePageCommand(sourcePage.Id);
        cmd.Execute(doc);

        var copyPage = doc.FindPage(cmd.CreatedPageId);
        Assert.NotNull(copyPage);

        var copiedBalloons = copyPage!.AllBalloons.ToList();
        Assert.Single(copiedBalloons);
        var copiedBalloon = copiedBalloons[0];
        var copiedLayerIds = copyPage.Layers.Select(layer => layer.Id).ToHashSet();
        Assert.Contains(copiedBalloon.LayerId, copiedLayerIds);
    }
}
