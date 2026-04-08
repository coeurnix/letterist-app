using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class GuideCommandTests
{
    [Fact]
    public void CreateGuideCommand_AddsGuide()
    {
        var doc = Document.Create("Test", new Size2(400, 300));
        var pageId = doc.ActivePageId;

        var cmd = new CreateGuideCommand(pageId, GuideOrientation.Horizontal, 42f);
        cmd.Execute(doc);

        var guide = doc.ActivePage!.Guides.Single();
        Assert.Equal(cmd.CreatedGuideId, guide.Id);
        Assert.Equal(GuideOrientation.Horizontal, guide.Orientation);
        Assert.Equal(42f, guide.Position);
    }

    [Fact]
    public void CreateGuideCommand_Undo_RemovesGuide()
    {
        var doc = Document.Create("Test", new Size2(400, 300));
        var pageId = doc.ActivePageId;

        var cmd = new CreateGuideCommand(pageId, GuideOrientation.Vertical, 120f);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Empty(doc.ActivePage!.Guides);
    }

    [Fact]
    public void MoveGuideCommand_UpdatesPosition_AndUndoRestores()
    {
        var doc = Document.Create("Test", new Size2(400, 300));
        var pageId = doc.ActivePageId;

        var create = new CreateGuideCommand(pageId, GuideOrientation.Horizontal, 10f);
        create.Execute(doc);

        var guideId = doc.ActivePage!.Guides.Single().Id;
        var move = new MoveGuideCommand(pageId, guideId, 80f);
        move.Execute(doc);

        Assert.Equal(80f, doc.ActivePage!.Guides.Single().Position);

        move.Undo(doc);
        Assert.Equal(10f, doc.ActivePage!.Guides.Single().Position);
    }

    [Fact]
    public void DeleteGuideCommand_Removes_AndUndoRestores()
    {
        var doc = Document.Create("Test", new Size2(400, 300));
        var pageId = doc.ActivePageId;

        var create = new CreateGuideCommand(pageId, GuideOrientation.Vertical, 55f);
        create.Execute(doc);

        var guideId = doc.ActivePage!.Guides.Single().Id;
        var delete = new DeleteGuideCommand(pageId, guideId);
        delete.Execute(doc);

        Assert.Empty(doc.ActivePage!.Guides);

        delete.Undo(doc);
        Assert.Single(doc.ActivePage!.Guides);
        Assert.Equal(55f, doc.ActivePage!.Guides.Single().Position);
    }

    [Fact]
    public void SetGuidesLockedCommand_Toggles_AndUndoRestores()
    {
        var doc = Document.Create("Test", new Size2(400, 300));
        var pageId = doc.ActivePageId;

        var cmd = new SetGuidesLockedCommand(pageId, true);
        cmd.Execute(doc);

        Assert.True(doc.ActivePage!.GuidesLocked);

        cmd.Undo(doc);
        Assert.False(doc.ActivePage!.GuidesLocked);
    }
}
