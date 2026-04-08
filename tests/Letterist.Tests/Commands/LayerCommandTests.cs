using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class LayerCommandTests
{
    private static Layer GetFirstBalloonLayer(Document doc)
    {
        return doc.BalloonLayers.First();
    }

    [Fact]
    public void CreateLayerCommand_AddsLayer()
    {
        var doc = Document.Create("Test");
        var initialCount = doc.Layers.Count;

        var cmd = new CreateLayerCommand("New Layer");
        cmd.Execute(doc);

        Assert.Equal(initialCount + 1, doc.Layers.Count);
        Assert.Equal("New Layer", doc.Layers[^1].Name);
    }

    [Fact]
    public void CreateLayerCommand_AtSpecificIndex()
    {
        var doc = Document.Create("Test");
        new CreateLayerCommand("Layer 2").Execute(doc);
        new CreateLayerCommand("Layer 3").Execute(doc);

        var cmd = new CreateLayerCommand("Inserted", insertIndex: 1);
        cmd.Execute(doc);

        Assert.Equal("Inserted", doc.Layers[1].Name);
    }

    [Fact]
    public void CreateLayerCommand_Undo_RemovesLayer()
    {
        var doc = Document.Create("Test");
        var initialCount = doc.Layers.Count;

        var cmd = new CreateLayerCommand("New Layer");
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(initialCount, doc.Layers.Count);
    }

    [Fact]
    public void DeleteLayerCommand_RemovesLayer()
    {
        var doc = Document.Create("Test");
        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layerIdToDelete = createCmd.CreatedLayerId;

        var deleteCmd = new DeleteLayerCommand(layerIdToDelete);
        deleteCmd.Execute(doc);

        Assert.Equal(2, doc.Layers.Count); // Background + remaining balloon layer
        Assert.Null(doc.FindLayer(layerIdToDelete));
    }

    [Fact]
    public void DeleteLayerCommand_Throws_IfLastLayer()
    {
        var doc = Document.Create("Test");
        var lastLayerId = GetFirstBalloonLayer(doc).Id;

        var deleteCmd = new DeleteLayerCommand(lastLayerId);

        Assert.Throws<InvalidOperationException>(() => deleteCmd.Execute(doc));
    }

    [Fact]
    public void DeleteLayerCommand_Undo_RestoresLayer()
    {
        var doc = Document.Create("Test");
        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layerId = createCmd.CreatedLayerId;

        var balloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        balloonCmd.Execute(doc);

        var deleteCmd = new DeleteLayerCommand(layerId);
        deleteCmd.Execute(doc);
        deleteCmd.Undo(doc);

        var restoredLayer = doc.FindLayer(layerId);
        Assert.NotNull(restoredLayer);
        Assert.Equal("Layer 2", restoredLayer.Name);
        Assert.Single(restoredLayer.Balloons);
    }

    [Fact]
    public void DeleteLayerCommand_UpdatesActiveLayer()
    {
        var doc = Document.Create("Test");
        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        doc.SetActiveLayerId(layer2Id);

        var deleteCmd = new DeleteLayerCommand(layer2Id);
        deleteCmd.Execute(doc);

        Assert.NotEqual(layer2Id, doc.ActiveLayerId);
        Assert.Equal(GetFirstBalloonLayer(doc).Id, doc.ActiveLayerId);
    }

    [Fact]
    public void RenameLayerCommand_ChangesName()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new RenameLayerCommand(layerId, "Renamed Layer");
        cmd.Execute(doc);

        Assert.Equal("Renamed Layer", doc.FindLayer(layerId)!.Name);
    }

    [Fact]
    public void RenameLayerCommand_Undo_RestoresName()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;
        var originalName = doc.FindLayer(layerId)!.Name;

        var cmd = new RenameLayerCommand(layerId, "Renamed Layer");
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(originalName, doc.FindLayer(layerId)!.Name);
    }

    [Fact]
    public void ReorderLayerCommand_MovesLayer()
    {
        var doc = Document.Create("Test");
        new CreateLayerCommand("Layer 2").Execute(doc);
        new CreateLayerCommand("Layer 3").Execute(doc);

        var layer1Id = GetFirstBalloonLayer(doc).Id;

        var cmd = new ReorderLayerCommand(layer1Id, 2);
        cmd.Execute(doc);

        Assert.Equal(layer1Id, doc.Layers[2].Id);
    }

    [Fact]
    public void ReorderLayerCommand_Undo_RestoresOrder()
    {
        var doc = Document.Create("Test");
        new CreateLayerCommand("Layer 2").Execute(doc);
        new CreateLayerCommand("Layer 3").Execute(doc);

        var layer1Id = GetFirstBalloonLayer(doc).Id;
        var originalOrder = doc.Layers.Select(l => l.Id).ToList();

        var cmd = new ReorderLayerCommand(layer1Id, 2);
        cmd.Execute(doc);
        cmd.Undo(doc);

        var restoredOrder = doc.Layers.Select(l => l.Id).ToList();
        Assert.Equal(originalOrder, restoredOrder);
    }

    [Fact]
    public void SetLayerVisibilityCommand_HidesLayer()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerVisibilityCommand(layerId, false);
        cmd.Execute(doc);

        Assert.False(doc.FindLayer(layerId)!.IsVisible);
    }

    [Fact]
    public void SetLayerVisibilityCommand_Undo_RestoresVisibility()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerVisibilityCommand(layerId, false);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.True(doc.FindLayer(layerId)!.IsVisible);
    }

    [Fact]
    public void SetLayerLockedCommand_LocksLayer()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerLockedCommand(layerId, true);
        cmd.Execute(doc);

        Assert.True(doc.FindLayer(layerId)!.IsLocked);
    }

    [Fact]
    public void SetLayerLockedCommand_Undo_RestoresLockState()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerLockedCommand(layerId, true);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.False(doc.FindLayer(layerId)!.IsLocked);
    }

    [Fact]
    public void SetActiveLayerCommand_ChangesActiveLayer()
    {
        var doc = Document.Create("Test");
        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        var cmd = new SetActiveLayerCommand(layer2Id);
        cmd.Execute(doc);

        Assert.Equal(layer2Id, doc.ActiveLayerId);
    }

    [Fact]
    public void SetActiveLayerCommand_Undo_RestoresActiveLayer()
    {
        var doc = Document.Create("Test");
        var originalActiveId = doc.ActiveLayerId;
        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        var cmd = new SetActiveLayerCommand(layer2Id);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(originalActiveId, doc.ActiveLayerId);
    }

    [Fact]
    public void SetActiveLayerCommand_Throws_IfLayerNotFound()
    {
        var doc = Document.Create("Test");

        var cmd = new SetActiveLayerCommand(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => cmd.Execute(doc));
    }

    [Fact]
    public void SetLayerOpacityCommand_SetsOpacity()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerOpacityCommand(layerId, 0.5f);
        cmd.Execute(doc);

        Assert.Equal(0.5f, doc.FindLayer(layerId)!.Opacity);
    }

    [Fact]
    public void SetLayerOpacityCommand_Undo_RestoresOpacity()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerOpacityCommand(layerId, 0.5f);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(1.0f, doc.FindLayer(layerId)!.Opacity);
    }

    [Fact]
    public void SetLayerOpacityCommand_ClampsValue()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd1 = new SetLayerOpacityCommand(layerId, -0.5f);
        cmd1.Execute(doc);
        Assert.Equal(0f, doc.FindLayer(layerId)!.Opacity);

        var cmd2 = new SetLayerOpacityCommand(layerId, 1.5f);
        cmd2.Execute(doc);
        Assert.Equal(1f, doc.FindLayer(layerId)!.Opacity);
    }

    [Fact]
    public void SetLayerOpacityCommand_Throws_IfLayerNotFound()
    {
        var doc = Document.Create("Test");

        var cmd = new SetLayerOpacityCommand(Guid.NewGuid(), 0.5f);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute(doc));
    }

    [Fact]
    public void SetLayerBlendModeCommand_SetsBlendMode()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerBlendModeCommand(layerId, LayerBlendMode.Multiply);
        cmd.Execute(doc);

        Assert.Equal(LayerBlendMode.Multiply, doc.FindLayer(layerId)!.BlendMode);
    }

    [Fact]
    public void SetLayerBlendModeCommand_Undo_RestoresBlendMode()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new SetLayerBlendModeCommand(layerId, LayerBlendMode.Screen);
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(LayerBlendMode.Normal, doc.FindLayer(layerId)!.BlendMode);
    }

    [Fact]
    public void MergeLayersCommand_MovesBalloons()
    {
        var doc = Document.Create("Test");
        var targetLayerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Source");
        createCmd.Execute(doc);
        var sourceLayerId = createCmd.CreatedLayerId;

        var balloonCmd = new CreateBalloonCommand(sourceLayerId, new Point2(100, 100), "Test");
        balloonCmd.Execute(doc);
        var balloonId = balloonCmd.CreatedBalloonId;

        var mergeCmd = new MergeLayersCommand(sourceLayerId, targetLayerId);
        mergeCmd.Execute(doc);

        Assert.Null(doc.FindLayer(sourceLayerId));
        Assert.NotNull(doc.FindLayer(targetLayerId)!.FindBalloon(balloonId));
    }

    [Fact]
    public void MergeLayersCommand_MovesTextOnlyBalloons()
    {
        var doc = Document.Create("Test");
        var targetLayerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Source");
        createCmd.Execute(doc);
        var sourceLayerId = createCmd.CreatedLayerId;

        var textOnlyBalloonId = Guid.NewGuid();
        new CreateBalloonCommand(
            sourceLayerId,
            new Point2(100, 100),
            "BAM",
            BalloonShape.None,
            BalloonStyle.Default,
            TextStyle.Default,
            textOnlyBalloonId).Execute(doc);

        var mergeCmd = new MergeLayersCommand(sourceLayerId, targetLayerId);
        mergeCmd.Execute(doc);

        Assert.Null(doc.FindLayer(sourceLayerId));
        Assert.NotNull(doc.FindLayer(targetLayerId)!.FindBalloon(textOnlyBalloonId));
    }

    [Fact]
    public void MergeLayersCommand_Undo_RestoresSourceLayer()
    {
        var doc = Document.Create("Test");
        var targetLayerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Source");
        createCmd.Execute(doc);
        var sourceLayerId = createCmd.CreatedLayerId;

        var balloonCmd = new CreateBalloonCommand(sourceLayerId, new Point2(100, 100), "Test");
        balloonCmd.Execute(doc);
        var balloonId = balloonCmd.CreatedBalloonId;

        var mergeCmd = new MergeLayersCommand(sourceLayerId, targetLayerId);
        mergeCmd.Execute(doc);
        mergeCmd.Undo(doc);

        Assert.NotNull(doc.FindLayer(sourceLayerId));
        Assert.NotNull(doc.FindLayer(sourceLayerId)!.FindBalloon(balloonId));
        Assert.Null(doc.FindLayer(targetLayerId)!.FindBalloon(balloonId));
    }

    [Fact]
    public void MergeLayersCommand_Throws_IfSameLayer()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new MergeLayersCommand(layerId, layerId);

        Assert.Throws<InvalidOperationException>(() => cmd.Execute(doc));
    }

    [Fact]
    public void FlattenVisibleCommand_CombinesLayers()
    {
        var doc = Document.Create("Test");
        var layer1Id = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        new CreateBalloonCommand(layer1Id, new Point2(100, 100), "B1").Execute(doc);
        new CreateBalloonCommand(layer2Id, new Point2(200, 200), "B2").Execute(doc);

        var flattenCmd = new FlattenVisibleCommand("Flattened");
        flattenCmd.Execute(doc);

        Assert.Equal(2, doc.Layers.Count); // Background + flattened
        var flattened = doc.Layers.First(l => l.Name == "Flattened");
        Assert.Equal(2, flattened.Balloons.Count);
    }

    [Fact]
    public void FlattenVisibleCommand_CombinesTextOnlyBalloons()
    {
        var doc = Document.Create("Test");
        var layer1Id = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        new CreateBalloonCommand(layer1Id, new Point2(100, 100), "WHAM", BalloonShape.None).Execute(doc);
        new CreateBalloonCommand(layer2Id, new Point2(200, 200), "POW", BalloonShape.None).Execute(doc);

        var flattenCmd = new FlattenVisibleCommand("Flattened");
        flattenCmd.Execute(doc);

        var flattened = doc.Layers.First(l => l.Name == "Flattened");
        Assert.Equal(2, flattened.Balloons.Count(balloon => balloon.Shape == BalloonShape.None));
    }

    [Fact]
    public void FlattenVisibleCommand_Undo_RestoresLayers()
    {
        var doc = Document.Create("Test");
        var layer1Id = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        new CreateBalloonCommand(layer1Id, new Point2(100, 100), "B1").Execute(doc);
        new CreateBalloonCommand(layer2Id, new Point2(200, 200), "B2").Execute(doc);

        var flattenCmd = new FlattenVisibleCommand("Flattened");
        flattenCmd.Execute(doc);
        flattenCmd.Undo(doc);

        Assert.Equal(3, doc.Layers.Count); // Background + 2 balloon layers
        Assert.NotNull(doc.FindLayer(layer1Id));
        Assert.NotNull(doc.FindLayer(layer2Id));
    }

    [Fact]
    public void FlattenVisibleCommand_SkipsHiddenLayers()
    {
        var doc = Document.Create("Test");
        var layer1Id = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateLayerCommand("Layer 2");
        createCmd.Execute(doc);
        var layer2Id = createCmd.CreatedLayerId;

        new SetLayerVisibilityCommand(layer2Id, false).Execute(doc);

        new CreateBalloonCommand(layer1Id, new Point2(100, 100), "B1").Execute(doc);
        new CreateBalloonCommand(layer2Id, new Point2(200, 200), "B2").Execute(doc);

        var flattenCmd = new FlattenVisibleCommand("Flattened");
        flattenCmd.Execute(doc);

        Assert.Equal(3, doc.Layers.Count); // Background + flattened + hidden layer 2
        Assert.Single(doc.Layers.First(l => l.Name == "Flattened").Balloons);
    }
}
