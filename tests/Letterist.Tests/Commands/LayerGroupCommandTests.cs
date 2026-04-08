using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class LayerGroupCommandTests
{
    private Document CreateTestDocument()
    {
        var doc = new Document(Guid.NewGuid(), "Test Doc", new Size2(800, 600));
        return doc;
    }

    [Fact]
    public void CreateLayerGroupCommand_AddsGroup()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var cmd = new CreateLayerGroupCommand(pageId, "Test Group");

        cmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.Single(page.LayerGroups);
        Assert.Equal("Test Group", page.LayerGroups[0].Name);
        Assert.Equal(cmd.CreatedGroupId, page.LayerGroups[0].Id);
    }

    [Fact]
    public void CreateLayerGroupCommand_Undo_RemovesGroup()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var cmd = new CreateLayerGroupCommand(pageId, "Test Group");

        cmd.Execute(doc);
        cmd.Undo(doc);

        var page = doc.ActivePage!;
        Assert.Empty(page.LayerGroups);
    }

    [Fact]
    public void DeleteLayerGroupCommand_RemovesGroup()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var deleteCmd = new DeleteLayerGroupCommand(pageId, groupId);
        deleteCmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.Empty(page.LayerGroups);
    }

    [Fact]
    public void DeleteLayerGroupCommand_Undo_RestoresGroup()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var deleteCmd = new DeleteLayerGroupCommand(pageId, groupId);
        deleteCmd.Execute(doc);
        deleteCmd.Undo(doc);

        var page = doc.ActivePage!;
        Assert.Single(page.LayerGroups);
        Assert.Equal("Test Group", page.LayerGroups[0].Name);
    }

    [Fact]
    public void RenameLayerGroupCommand_ChangesName()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Original");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var renameCmd = new RenameLayerGroupCommand(pageId, groupId, "Renamed");
        renameCmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.Equal("Renamed", page.FindLayerGroup(groupId)!.Name);
    }

    [Fact]
    public void RenameLayerGroupCommand_Undo_RestoresName()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Original");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var renameCmd = new RenameLayerGroupCommand(pageId, groupId, "Renamed");
        renameCmd.Execute(doc);
        renameCmd.Undo(doc);

        var page = doc.ActivePage!;
        Assert.Equal("Original", page.FindLayerGroup(groupId)!.Name);
    }

    [Fact]
    public void AddLayerToGroupCommand_SetsGroupId()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        Assert.Equal(groupId, page.FindLayer(layerId)!.GroupId);
    }

    [Fact]
    public void AddLayerToGroupCommand_Undo_RestoresPreviousGroupId()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);
        addCmd.Undo(doc);

        Assert.Null(page.FindLayer(layerId)!.GroupId);
    }

    [Fact]
    public void RemoveLayerFromGroupCommand_ClearsGroupId()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        var removeCmd = new RemoveLayerFromGroupCommand(pageId, layerId);
        removeCmd.Execute(doc);

        Assert.Null(page.FindLayer(layerId)!.GroupId);
    }

    [Fact]
    public void RemoveLayerFromGroupCommand_Undo_RestoresGroupId()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        var removeCmd = new RemoveLayerFromGroupCommand(pageId, layerId);
        removeCmd.Execute(doc);
        removeCmd.Undo(doc);

        Assert.Equal(groupId, page.FindLayer(layerId)!.GroupId);
    }

    [Fact]
    public void SetLayerGroupVisibilityCommand_TogglesVisibility()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var visCmd = new SetLayerGroupVisibilityCommand(pageId, groupId, false);
        visCmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.False(page.FindLayerGroup(groupId)!.IsVisible);
    }

    [Fact]
    public void SetLayerGroupVisibilityCommand_Undo_RestoresVisibility()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var visCmd = new SetLayerGroupVisibilityCommand(pageId, groupId, false);
        visCmd.Execute(doc);
        visCmd.Undo(doc);

        var page = doc.ActivePage!;
        Assert.True(page.FindLayerGroup(groupId)!.IsVisible);
    }

    [Fact]
    public void SetLayerGroupLockedCommand_TogglesLock()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var lockCmd = new SetLayerGroupLockedCommand(pageId, groupId, true);
        lockCmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.True(page.FindLayerGroup(groupId)!.IsLocked);
    }

    [Fact]
    public void SetLayerGroupLockedCommand_Undo_RestoresLock()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var lockCmd = new SetLayerGroupLockedCommand(pageId, groupId, true);
        lockCmd.Execute(doc);
        lockCmd.Undo(doc);

        var page = doc.ActivePage!;
        Assert.False(page.FindLayerGroup(groupId)!.IsLocked);
    }

    [Fact]
    public void SetLayerGroupExpandedCommand_TogglesExpanded()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var expandCmd = new SetLayerGroupExpandedCommand(pageId, groupId, false);
        expandCmd.Execute(doc);

        var page = doc.ActivePage!;
        Assert.False(page.FindLayerGroup(groupId)!.IsExpanded);
    }

    [Fact]
    public void Page_IsLayerEffectivelyVisible_ConsidersGroupVisibility()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        Assert.True(page.IsLayerEffectivelyVisible(layerId));

        var visCmd = new SetLayerGroupVisibilityCommand(pageId, groupId, false);
        visCmd.Execute(doc);

        Assert.False(page.IsLayerEffectivelyVisible(layerId));
    }

    [Fact]
    public void Page_IsLayerEffectivelyLocked_ConsidersGroupLock()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        Assert.False(page.IsLayerEffectivelyLocked(layerId));

        var lockCmd = new SetLayerGroupLockedCommand(pageId, groupId, true);
        lockCmd.Execute(doc);

        Assert.True(page.IsLayerEffectivelyLocked(layerId));
    }

    [Fact]
    public void DeleteLayerGroupCommand_UngroupsLayers()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        var deleteCmd = new DeleteLayerGroupCommand(pageId, groupId);
        deleteCmd.Execute(doc);

        Assert.Null(page.FindLayer(layerId)!.GroupId);
        Assert.NotNull(page.FindLayer(layerId));
    }

    [Fact]
    public void DeleteLayerGroupCommand_Undo_RestoresLayersToGroup()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var page = doc.ActivePage!;
        var layerId = page.BalloonLayers.First().Id;

        var createCmd = new CreateLayerGroupCommand(pageId, "Test Group");
        createCmd.Execute(doc);
        var groupId = createCmd.CreatedGroupId;

        var addCmd = new AddLayerToGroupCommand(pageId, layerId, groupId);
        addCmd.Execute(doc);

        var deleteCmd = new DeleteLayerGroupCommand(pageId, groupId);
        deleteCmd.Execute(doc);
        deleteCmd.Undo(doc);

        Assert.Equal(groupId, page.FindLayer(layerId)!.GroupId);
    }
}
