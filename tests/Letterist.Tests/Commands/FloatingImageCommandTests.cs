using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class FloatingImageCommandTests
{
    private static Document CreateTestDocument()
    {
        return Document.Create("Test");
    }

    [Fact]
    public void CreateFloatingImageCommand_UsesActiveLayerByDefault()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var bounds = new Rect(10, 20, 120, 80);

        var cmd = new CreateFloatingImageCommand(page.Id, "image.png", bounds);
        cmd.Execute(doc);

        var image = page.FindFloatingImage(cmd.CreatedImageId);
        Assert.NotNull(image);
        Assert.Equal(page.ActiveLayerId, image!.LayerId);
    }

    [Fact]
    public void CreateFloatingImageCommand_UsesExplicitLayer()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var createLayer = new CreateLayerCommand("Layer 2");
        createLayer.Execute(doc);

        var targetLayerId = createLayer.CreatedLayerId;
        var bounds = new Rect(0, 0, 64, 64);
        var cmd = new CreateFloatingImageCommand(page.Id, "image.png", bounds, layerId: targetLayerId);
        cmd.Execute(doc);

        var image = page.FindFloatingImage(cmd.CreatedImageId);
        Assert.NotNull(image);
        Assert.Equal(targetLayerId, image!.LayerId);
    }

    [Fact]
    public void DeleteLayerCommand_ReassignsFloatingImagesToValidLayer()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var createLayer = new CreateLayerCommand("Layer 2");
        createLayer.Execute(doc);
        var deletedLayerId = createLayer.CreatedLayerId;
        var fallbackLayerId = page.GetFirstBalloonLayer()!.Id;

        var createImage = new CreateFloatingImageCommand(
            page.Id,
            "image.png",
            new Rect(0, 0, 100, 100),
            layerId: deletedLayerId);
        createImage.Execute(doc);

        var imageId = createImage.CreatedImageId;
        var deleteLayer = new DeleteLayerCommand(deletedLayerId);
        deleteLayer.Execute(doc);

        var image = page.FindFloatingImage(imageId);
        Assert.NotNull(image);
        Assert.Equal(fallbackLayerId, image!.LayerId);
    }

    [Fact]
    public void CreateFloatingImageCommand_Serialize_IncludesLayerId()
    {
        var pageId = Guid.NewGuid();
        var layerId = Guid.NewGuid();
        var cmd = new CreateFloatingImageCommand(pageId, "image.png", new Rect(1, 2, 3, 4), layerId: layerId);

        var data = cmd.Serialize();
        Assert.True(data.Parameters.ContainsKey("layerId"));
        Assert.Equal(layerId, data.Parameters["layerId"]);
    }

    [Fact]
    public void RenameFloatingImageCommand_ExecuteUndo_UpdatesName()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var create = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(10, 20, 120, 80));
        create.Execute(doc);

        var rename = new RenameFloatingImageCommand(page.Id, create.CreatedImageId, "SFX Burst");
        rename.Execute(doc);

        var image = page.FindFloatingImage(create.CreatedImageId);
        Assert.NotNull(image);
        Assert.Equal("SFX Burst", image!.Name);

        rename.Undo(doc);
        Assert.Null(image.Name);
    }

    [Fact]
    public void ReorderFloatingImageCommand_ExecuteUndo_UpdatesZOrder()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var createA = new CreateFloatingImageCommand(page.Id, "a.png", new Rect(0, 0, 10, 10));
        var createB = new CreateFloatingImageCommand(page.Id, "b.png", new Rect(10, 10, 10, 10));
        var createC = new CreateFloatingImageCommand(page.Id, "c.png", new Rect(20, 20, 10, 10));
        createA.Execute(doc);
        createB.Execute(doc);
        createC.Execute(doc);

        var originalOrder = page.FloatingImages.Select(image => image.Id).ToList();

        var reorder = new ReorderFloatingImageCommand(page.Id, createA.CreatedImageId, 2);
        reorder.Execute(doc);

        Assert.Equal(createA.CreatedImageId, page.FloatingImages[2].Id);

        reorder.Undo(doc);
        Assert.Equal(originalOrder, page.FloatingImages.Select(image => image.Id).ToList());
    }

    [Fact]
    public void SetFloatingImagePanelCommand_ExecuteUndo_UpdatesPanelMembership()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var panel = new CreatePanelZoneCommand(page.Id, "Panel 1", new Rect(0, 0, 200, 200));
        panel.Execute(doc);

        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(10, 20, 120, 80));
        createImage.Execute(doc);

        var image = page.FindFloatingImage(createImage.CreatedImageId);
        Assert.NotNull(image);
        Assert.Null(image!.PanelId);

        var setPanel = new SetFloatingImagePanelCommand(page.Id, image.Id, panel.CreatedPanelId);
        setPanel.Execute(doc);
        Assert.Equal(panel.CreatedPanelId, image.PanelId);

        setPanel.Undo(doc);
        Assert.Null(image.PanelId);
    }

    [Fact]
    public void SetFloatingImagePanelCommand_AssigningPanel_DefaultsConstrainToPanel()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var panel = new CreatePanelZoneCommand(page.Id, "Panel 1", new Rect(0, 0, 200, 200));
        panel.Execute(doc);

        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(10, 20, 120, 80));
        createImage.Execute(doc);

        var image = page.FindFloatingImage(createImage.CreatedImageId);
        Assert.NotNull(image);
        Assert.False(image!.ConstrainToPanel);

        var setPanel = new SetFloatingImagePanelCommand(page.Id, image.Id, panel.CreatedPanelId);
        setPanel.Execute(doc);
        Assert.Equal(panel.CreatedPanelId, image.PanelId);
        Assert.True(image.ConstrainToPanel);

        setPanel.Undo(doc);
        Assert.Null(image.PanelId);
        Assert.False(image.ConstrainToPanel);
    }

    [Fact]
    public void SetFloatingImagePanelCommand_Serialize_IncludesPanelId()
    {
        var pageId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var panelId = Guid.NewGuid();
        var cmd = new SetFloatingImagePanelCommand(pageId, imageId, panelId);

        var data = cmd.Serialize();
        Assert.True(data.Parameters.ContainsKey("panelId"));
        Assert.Equal(panelId, data.Parameters["panelId"]);
    }

    [Fact]
    public void SetFloatingImageConstrainToPanelCommand_ExecuteUndo_UpdatesConstrainFlag()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;

        var panel = new CreatePanelZoneCommand(page.Id, "Panel 1", new Rect(0, 0, 200, 200));
        panel.Execute(doc);

        var createImage = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(10, 20, 120, 80));
        createImage.Execute(doc);
        var image = page.FindFloatingImage(createImage.CreatedImageId)!;

        new SetFloatingImagePanelCommand(page.Id, image.Id, panel.CreatedPanelId).Execute(doc);
        Assert.True(image.ConstrainToPanel);

        var command = new SetFloatingImageConstrainToPanelCommand(page.Id, image.Id, false);
        command.Execute(doc);
        Assert.False(image.ConstrainToPanel);

        command.Undo(doc);
        Assert.True(image.ConstrainToPanel);
    }

    [Fact]
    public void SetFloatingImageRotationCommand_ExecuteUndo_UpdatesRotation()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var create = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(0, 0, 100, 80));
        create.Execute(doc);
        var image = page.FindFloatingImage(create.CreatedImageId)!;

        var command = new SetFloatingImageRotationCommand(page.Id, image.Id, 27f);
        command.Execute(doc);
        Assert.Equal(27f, image.Rotation);

        command.Undo(doc);
        Assert.Equal(0f, image.Rotation);
    }

    [Fact]
    public void SetFloatingImageShadowCommand_ExecuteUndo_UpdatesShadow()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var create = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(0, 0, 100, 80));
        create.Execute(doc);
        var image = page.FindFloatingImage(create.CreatedImageId)!;

        var command = new SetFloatingImageShadowCommand(
            page.Id,
            image.Id,
            enabled: true,
            color: new Color(20, 30, 40),
            opacity: 0.6f,
            offsetX: 5f,
            offsetY: -3f,
            falloff: 9f);
        command.Execute(doc);

        Assert.True(image.ShadowEnabled);
        Assert.Equal(new Color(20, 30, 40), image.ShadowColor);
        Assert.Equal(0.6f, image.ShadowOpacity);
        Assert.Equal(5f, image.ShadowOffsetX);
        Assert.Equal(-3f, image.ShadowOffsetY);
        Assert.Equal(9f, image.ShadowFalloff);

        command.Undo(doc);
        Assert.False(image.ShadowEnabled);
    }

    [Fact]
    public void SetFloatingImageGlowCommand_ExecuteUndo_UpdatesGlow()
    {
        var doc = CreateTestDocument();
        var page = doc.ActivePage!;
        var create = new CreateFloatingImageCommand(page.Id, "image.png", new Rect(0, 0, 100, 80));
        create.Execute(doc);
        var image = page.FindFloatingImage(create.CreatedImageId)!;

        var command = new SetFloatingImageGlowCommand(
            page.Id,
            image.Id,
            enabled: true,
            color: new Color(200, 160, 20),
            opacity: 0.4f,
            size: 7f);
        command.Execute(doc);

        Assert.True(image.GlowEnabled);
        Assert.Equal(new Color(200, 160, 20), image.GlowColor);
        Assert.Equal(0.4f, image.GlowOpacity);
        Assert.Equal(7f, image.GlowSize);

        command.Undo(doc);
        Assert.False(image.GlowEnabled);
    }
}
