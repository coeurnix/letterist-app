using Letterist.Model;
using Letterist.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Letterist.Tests.Model;

public class DocumentTests
{
    [Fact]
    public void Create_GeneratesUniqueId()
    {
        var doc1 = Document.Create();
        var doc2 = Document.Create();

        Assert.NotEqual(doc1.Id, doc2.Id);
    }

    [Fact]
    public void Create_SetsDefaultSize()
    {
        var doc = Document.Create();

        Assert.Equal(1200, doc.Size.Width);
        Assert.Equal(1800, doc.Size.Height);
    }

    [Fact]
    public void Create_WithCustomSize()
    {
        var doc = Document.Create("Test", new Size2(800, 600));

        Assert.Equal(800, doc.Size.Width);
        Assert.Equal(600, doc.Size.Height);
    }

    [Fact]
    public void Create_HasDefaultLayer()
    {
        var doc = Document.Create();

        Assert.Equal(2, doc.Layers.Count);
        Assert.Equal("Background", doc.Layers[0].Name);
        Assert.Equal(LayerKind.Image, doc.Layers[0].Kind);
        Assert.Equal("Layer 1", doc.Layers[1].Name);
        Assert.Equal(doc.Layers[1].Id, doc.ActiveLayerId);
    }

    [Fact]
    public void FindLayer_ReturnsCorrectLayer()
    {
        var doc = Document.Create();
        var layerId = doc.Layers[0].Id;

        var found = doc.FindLayer(layerId);

        Assert.NotNull(found);
        Assert.Equal(layerId, found.Id);
    }

    [Fact]
    public void Create_SeedsBuiltInBalloonStyles()
    {
        var doc = Document.Create();

        Assert.True(doc.BalloonStyles.Count >= 10);
        Assert.Contains(doc.BalloonStyles, style => style.Name == "Default Dialogue");
        Assert.Contains(doc.BalloonStyles, style => style.Name == "Narration Caption");
        Assert.Contains(doc.BalloonStyles, style => style.Name == "Manga Dialogue Soft");
        Assert.Contains(doc.BalloonStyles, style => style.Name == "Manga Shout Jagged");
        Assert.Contains(doc.BalloonStyles, style => style.Name == "Manga Narration Box");
        Assert.Contains(doc.BalloonStyles, style => style.IsQuickSelect);
        Assert.Contains(doc.BalloonStyles, style => !style.IsQuickSelect);
        Assert.All(doc.BalloonStyles, style => Assert.True(style.ApplyExtendedDetails));

        var shout = doc.BalloonStyles.First(style => style.Name == "Shout Impact");
        Assert.Equal(BalloonShape.Burst, shout.Shape);
        Assert.Single(shout.Tails);
        Assert.Equal(TailStyle.Pointer, shout.Tails[0].Style);
        Assert.True(shout.TextStyle.Bold);

        var narration = doc.BalloonStyles.First(style => style.Name == "Narration Caption");
        Assert.Equal(BalloonShape.Rectangle, narration.Shape);
        Assert.True(narration.ConstrainToPanel);
        Assert.Empty(narration.Tails);
        Assert.Equal(TextAlignment.Left, narration.TextStyle.Alignment);

        var sfx = doc.BalloonStyles.First(style => style.Name == "SFX Outline");
        Assert.Equal(BalloonShape.None, sfx.Shape);
        Assert.Empty(sfx.Tails);
        Assert.True(sfx.TextStyle.Bold);
        Assert.True(sfx.TextStyle.AllCaps);

        var mangaNarration = doc.BalloonStyles.First(style => style.Name == "Manga Narration Box");
        Assert.False(mangaNarration.IsQuickSelect);
        Assert.Equal(BalloonShape.Rectangle, mangaNarration.Shape);
        Assert.True(mangaNarration.ConstrainToPanel);
        Assert.Empty(mangaNarration.Tails);

        var mangaThought = doc.BalloonStyles.First(style => style.Name == "Manga Thought Cloud");
        Assert.False(mangaThought.IsQuickSelect);
        Assert.Equal(BalloonShape.Thought, mangaThought.Shape);
        Assert.Single(mangaThought.Tails);
        Assert.Equal(TailStyle.ThoughtBubbles, mangaThought.Tails[0].Style);
    }

    [Fact]
    public void Constructor_MergesMissingBuiltInPanelTemplates_ForLegacyDocuments()
    {
        var size = new Size2(1200, 1800);
        var allDefaults = PanelLayoutTemplate.CreateDefaults(size);
        var legacySubset = allDefaults.Take(10).Select(template => template.Clone()).ToList();
        var page = Page.Create("Page 1", size);
        var timestamp = DateTime.UtcNow;

        var doc = new Document(
            Guid.NewGuid(),
            "Legacy",
            timestamp,
            timestamp,
            new[] { page },
            page.Id,
            "px",
            300f,
            size,
            new Color(255, 255, 255, 255),
            null,
            panelTemplates: legacySubset);

        Assert.True(doc.PanelTemplates.Count >= allDefaults.Count);
        foreach (var template in allDefaults)
        {
            Assert.Contains(doc.PanelTemplates, existing => string.Equals(existing.Name, template.Name, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void FindLayer_ReturnsNull_WhenNotFound()
    {
        var doc = Document.Create();

        var found = doc.FindLayer(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var doc = Document.Create("Original");
        var clone = doc.Clone();

        Assert.Equal(doc.Id, clone.Id);
        Assert.Equal(doc.Name, clone.Name);
        Assert.Equal(doc.Size, clone.Size);
        Assert.Equal(doc.Layers.Count, clone.Layers.Count);

        Assert.NotSame(doc.Layers[0], clone.Layers[0]);
        Assert.Equal(doc.Layers[0].Id, clone.Layers[0].Id);
    }

    [Fact]
    public void IsDirty_InitiallyFalse()
    {
        var doc = Document.Create();

        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesDefaultPageSize()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.SetDocumentDefaultPageSizeCommand(new Size2(1440, 2160)));
        dispatcher.Execute(new Letterist.Commands.SetDocumentDefaultBackgroundColorCommand(null));
        dispatcher.Execute(new Letterist.Commands.SetDocumentDefaultBackgroundImageCommand(@"C:\images\bg-default.png"));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        Assert.Equal(1440, restored.DefaultPageSize.Width);
        Assert.Equal(2160, restored.DefaultPageSize.Height);
        Assert.Null(restored.DefaultPageBackgroundColor);
        Assert.Equal(@"C:\images\bg-default.png", restored.DefaultPageBackgroundImagePath);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesBalloonStyleQuickSelect()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var targetStyle = doc.BalloonStyles.First();
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.SetNamedBalloonStyleQuickSelectCommand(targetStyle.Id, false));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var restoredStyle = restored.FindBalloonStyle(targetStyle.Id);

        Assert.NotNull(restoredStyle);
        Assert.False(restoredStyle!.IsQuickSelect);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesBalloonStyleExtendedDetails()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var targetStyle = doc.BalloonStyles.First();
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);

        var update = new Letterist.Commands.UpdateNamedBalloonStyleCommand(
            targetStyle.Id,
            targetStyle.Style.With(strokeWidth: 4f),
            applyExtendedDetails: true,
            shape: BalloonShape.Custom,
            customShapePathData: "M0 0 L20 0 L20 10 Z",
            constrainToPanel: true,
            textStyle: TextStyle.Default.With(bold: true, italic: true, fontSize: 17f),
            textPath: new TextPath(
                new Point2(-18f, 0f),
                new Point2(-8f, -9f),
                new Point2(8f, -9f),
                new Point2(18f, 0f),
                offset: 2f),
            tails: new[]
            {
                new BalloonTemplateTail(new Point2(0f, 70f), TailStyle.Curved, 19f)
            });
        dispatcher.Execute(update);

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var restoredStyle = restored.FindBalloonStyle(targetStyle.Id);

        Assert.NotNull(restoredStyle);
        Assert.True(restoredStyle!.ApplyExtendedDetails);
        Assert.Equal(BalloonShape.Custom, restoredStyle.Shape);
        Assert.Equal("M0 0 L20 0 L20 10 Z", restoredStyle.CustomShapePathData);
        Assert.True(restoredStyle.ConstrainToPanel);
        Assert.True(restoredStyle.TextStyle.Bold);
        Assert.NotNull(restoredStyle.TextPath);
        Assert.Equal(2f, restoredStyle.TextPath!.Offset);
        Assert.Single(restoredStyle.Tails);
        Assert.Equal(TailStyle.Curved, restoredStyle.Tails[0].Style);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesGuidesLocked()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.SetGuidesLockedCommand(doc.ActivePageId, true));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        Assert.True(restored.ActivePage!.GuidesLocked);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesTextOnlyBalloons()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var layerId = doc.BalloonLayers.First().Id;
        var textOnlyBalloonId = Guid.NewGuid();
        var textStyle = TextStyle.Default.With(
            fontSize: 22f,
            bold: true,
            fillType: TextFillType.Linear,
            fillSecondaryColor: new Color(250, 180, 40),
            fillAngle: 32f,
            outlineColor: new Color(18, 24, 32),
            outlineWidth: 2.5f,
            additionalStrokes: new[]
            {
                new TextStroke { Color = new Color(255, 238, 120), Width = 4f },
                new TextStroke { Color = new Color(86, 14, 24), Width = 6f }
            },
            shadows: new[]
            {
                new TextShadow { Color = new Color(0, 0, 0), OffsetX = 4f, OffsetY = 5f, Blur = 2f, Opacity = 0.5f },
                new TextShadow { Color = new Color(255, 90, 32), OffsetX = -3f, OffsetY = 1f, Blur = 0f, Opacity = 0.35f }
            },
            outerGlowEnabled: true,
            outerGlowColor: new Color(255, 240, 90),
            outerGlowSize: 6f,
            outerGlowOpacity: 0.55f,
            innerGlowEnabled: true,
            innerGlowColor: new Color(42, 16, 12),
            innerGlowSize: 3f,
            innerGlowOpacity: 0.4f,
            extrusionEnabled: true,
            extrusionDepth: 9f,
            extrusionAngle: 120f,
            extrusionColor: new Color(40, 8, 8),
            extrusionOpacity: 0.7f,
            motionBlurEnabled: true,
            motionBlurDistance: 7f,
            motionBlurAngle: 22f,
            motionBlurOpacity: 0.33f,
            warpPreset: TextWarpPreset.Wave,
            warpIntensity: 0.45f,
            warpHorizontalDistortion: 0.2f,
            warpVerticalDistortion: -0.15f,
            warpMesh: new TextWarpMesh
            {
                TopLeftOffset = new Point2(-0.1f, 0.05f),
                TopRightOffset = new Point2(0.12f, -0.02f),
                BottomRightOffset = new Point2(0.08f, 0.04f),
                BottomLeftOffset = new Point2(-0.06f, -0.03f)
            });
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.CreateBalloonCommand(
            layerId,
            new Point2(140, 220),
            "KRAK",
            BalloonShape.None,
            BalloonStyle.Default,
            textStyle,
            textOnlyBalloonId));
        dispatcher.Execute(new Letterist.Commands.RotateBalloonCommand(textOnlyBalloonId, 27f));
        dispatcher.Execute(new Letterist.Commands.SetBalloonTextPathCommand(
            textOnlyBalloonId,
            new TextPath(
                new Point2(-40f, 0f),
                new Point2(-16f, -22f),
                new Point2(16f, -22f),
                new Point2(40f, 0f),
                offset: 7f,
                startPosition: 0.1f,
                endPosition: 0.9f,
                reverseDirection: true)));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        var restoredTextOnlyBalloon = restored.FindBalloon(textOnlyBalloonId);
        Assert.NotNull(restoredTextOnlyBalloon);
        Assert.Equal(new Point2(140, 220), restoredTextOnlyBalloon!.Position);
        Assert.Equal("KRAK", restoredTextOnlyBalloon.Text);
        Assert.Equal(22f, restoredTextOnlyBalloon.TextStyle.FontSize);
        Assert.True(restoredTextOnlyBalloon.TextStyle.Bold);
        Assert.Equal(TextFillType.Linear, restoredTextOnlyBalloon.TextStyle.FillType);
        Assert.Equal(new Color(250, 180, 40), restoredTextOnlyBalloon.TextStyle.FillSecondaryColor);
        Assert.Equal(32f, restoredTextOnlyBalloon.TextStyle.FillAngle);
        Assert.Equal(new Color(18, 24, 32), restoredTextOnlyBalloon.TextStyle.OutlineColor);
        Assert.Equal(2.5f, restoredTextOnlyBalloon.TextStyle.OutlineWidth);
        Assert.Equal(2, restoredTextOnlyBalloon.TextStyle.AdditionalStrokes.Count);
        Assert.Equal(new Color(255, 238, 120), restoredTextOnlyBalloon.TextStyle.AdditionalStrokes[0].Color);
        Assert.Equal(4f, restoredTextOnlyBalloon.TextStyle.AdditionalStrokes[0].Width);
        Assert.Equal(new Color(86, 14, 24), restoredTextOnlyBalloon.TextStyle.AdditionalStrokes[1].Color);
        Assert.Equal(6f, restoredTextOnlyBalloon.TextStyle.AdditionalStrokes[1].Width);
        Assert.Equal(2, restoredTextOnlyBalloon.TextStyle.Shadows.Count);
        Assert.Equal(new Color(0, 0, 0), restoredTextOnlyBalloon.TextStyle.Shadows[0].Color);
        Assert.Equal(4f, restoredTextOnlyBalloon.TextStyle.Shadows[0].OffsetX);
        Assert.Equal(2f, restoredTextOnlyBalloon.TextStyle.Shadows[0].Blur);
        Assert.Equal(0.35f, restoredTextOnlyBalloon.TextStyle.Shadows[1].Opacity);
        Assert.True(restoredTextOnlyBalloon.TextStyle.OuterGlowEnabled);
        Assert.Equal(new Color(255, 240, 90), restoredTextOnlyBalloon.TextStyle.OuterGlowColor);
        Assert.Equal(6f, restoredTextOnlyBalloon.TextStyle.OuterGlowSize);
        Assert.Equal(0.55f, restoredTextOnlyBalloon.TextStyle.OuterGlowOpacity);
        Assert.True(restoredTextOnlyBalloon.TextStyle.InnerGlowEnabled);
        Assert.Equal(new Color(42, 16, 12), restoredTextOnlyBalloon.TextStyle.InnerGlowColor);
        Assert.Equal(3f, restoredTextOnlyBalloon.TextStyle.InnerGlowSize);
        Assert.Equal(0.4f, restoredTextOnlyBalloon.TextStyle.InnerGlowOpacity);
        Assert.True(restoredTextOnlyBalloon.TextStyle.ExtrusionEnabled);
        Assert.Equal(9f, restoredTextOnlyBalloon.TextStyle.ExtrusionDepth);
        Assert.Equal(120f, restoredTextOnlyBalloon.TextStyle.ExtrusionAngle);
        Assert.Equal(new Color(40, 8, 8), restoredTextOnlyBalloon.TextStyle.ExtrusionColor);
        Assert.Equal(0.7f, restoredTextOnlyBalloon.TextStyle.ExtrusionOpacity);
        Assert.True(restoredTextOnlyBalloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(7f, restoredTextOnlyBalloon.TextStyle.MotionBlurDistance);
        Assert.Equal(22f, restoredTextOnlyBalloon.TextStyle.MotionBlurAngle);
        Assert.Equal(0.33f, restoredTextOnlyBalloon.TextStyle.MotionBlurOpacity);
        Assert.Equal(TextWarpPreset.Wave, restoredTextOnlyBalloon.TextStyle.WarpPreset);
        Assert.Equal(0.45f, restoredTextOnlyBalloon.TextStyle.WarpIntensity);
        Assert.Equal(0.2f, restoredTextOnlyBalloon.TextStyle.WarpHorizontalDistortion);
        Assert.Equal(-0.15f, restoredTextOnlyBalloon.TextStyle.WarpVerticalDistortion);
        Assert.Equal(new Point2(-0.1f, 0.05f), restoredTextOnlyBalloon.TextStyle.WarpMesh.TopLeftOffset);
        Assert.Equal(27f, restoredTextOnlyBalloon.Rotation);
        Assert.NotNull(restoredTextOnlyBalloon.TextPath);
        Assert.Equal(7f, restoredTextOnlyBalloon.TextPath!.Offset);
        Assert.True(restoredTextOnlyBalloon.TextPath.ReverseDirection);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesBalloonVisibilityAndLock()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var layerId = doc.BalloonLayers.First().Id;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        var create = new Letterist.Commands.CreateBalloonCommand(layerId, new Point2(120, 180), "State");
        dispatcher.Execute(create);

        dispatcher.Execute(new Letterist.Commands.SetBalloonVisibilityCommand(create.CreatedBalloonId, false));
        dispatcher.Execute(new Letterist.Commands.SetBalloonLockedCommand(create.CreatedBalloonId, true));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var balloon = restored.FindBalloon(create.CreatedBalloonId);

        Assert.NotNull(balloon);
        Assert.False(balloon!.IsVisible);
        Assert.True(balloon.IsLocked);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesObjectGroups()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var page = doc.ActivePage!;
        var layerId = doc.BalloonLayers.First().Id;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);

        var createBalloon = new Letterist.Commands.CreateBalloonCommand(layerId, new Point2(140, 220), "Group");
        var createImage = new Letterist.Commands.CreateFloatingImageCommand(page.Id, "image.png", new Rect(30, 40, 120, 80));
        dispatcher.Execute(createBalloon);
        dispatcher.Execute(createImage);
        dispatcher.Execute(new Letterist.Commands.GroupObjectsCommand(
            page.Id,
            new[] { createBalloon.CreatedBalloonId },
            new[] { createImage.CreatedImageId }));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var restoredPage = restored.ActivePage!;

        Assert.Single(restoredPage.ObjectGroups);
        var group = restoredPage.ObjectGroups[0];
        Assert.Contains(createBalloon.CreatedBalloonId, group.BalloonIds);
        Assert.Contains(createImage.CreatedImageId, group.FloatingImageIds);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesFloatingImageName()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var page = doc.ActivePage!;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        var createImage = new Letterist.Commands.CreateFloatingImageCommand(page.Id, "image.png", new Rect(50, 60, 100, 80));
        dispatcher.Execute(createImage);
        dispatcher.Execute(new Letterist.Commands.RenameFloatingImageCommand(page.Id, createImage.CreatedImageId, "Logo"));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var restoredImage = restored.ActivePage!.FindFloatingImage(createImage.CreatedImageId);

        Assert.NotNull(restoredImage);
        Assert.Equal("Logo", restoredImage!.Name);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesFloatingImagePanelConstrainSetting()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var page = doc.ActivePage!;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);

        var panel = new Letterist.Commands.CreatePanelZoneCommand(page.Id, "Panel 1", new Rect(40, 40, 260, 180));
        dispatcher.Execute(panel);

        var createImage = new Letterist.Commands.CreateFloatingImageCommand(page.Id, "image.png", new Rect(60, 70, 140, 100));
        dispatcher.Execute(createImage);
        dispatcher.Execute(new Letterist.Commands.SetFloatingImagePanelCommand(page.Id, createImage.CreatedImageId, panel.CreatedPanelId));
        dispatcher.Execute(new Letterist.Commands.SetFloatingImageConstrainToPanelCommand(page.Id, createImage.CreatedImageId, false));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();
        var restoredImage = restored.ActivePage!.FindFloatingImage(createImage.CreatedImageId);

        Assert.NotNull(restoredImage);
        Assert.Equal(panel.CreatedPanelId, restoredImage!.PanelId);
        Assert.False(restoredImage.ConstrainToPanel);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesPageBackgroundImageFitMode()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var page = doc.ActivePage!;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.SetPageBackgroundImageFitModeCommand(page.Id, PanelImageFitMode.Stretch));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        Assert.Equal(PanelImageFitMode.Stretch, restored.ActivePage!.BackgroundImageFitMode);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesLayerBlendMode()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var layerId = doc.BalloonLayers.First().Id;
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        dispatcher.Execute(new Letterist.Commands.SetLayerBlendModeCommand(layerId, LayerBlendMode.Overlay));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        Assert.Equal(LayerBlendMode.Overlay, restored.FindLayer(layerId)!.BlendMode);
    }

    [Fact]
    public void Persistence_Load_LegacySfxRecords_MigratesToTextOnlyBalloons()
    {
        var documentId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var layerId = Guid.NewGuid();
        var legacyBalloonId = Guid.NewGuid();

        var json = $$"""
        {
          "id": "{{documentId}}",
          "name": "Legacy",
          "created": "2026-01-02T03:04:05Z",
          "modified": "2026-01-02T03:04:05Z",
          "activePageId": "{{pageId}}",
          "pages": [
            {
              "id": "{{pageId}}",
              "name": "Page 1",
              "size": { "width": 1200, "height": 1800 },
              "activeLayerId": "{{layerId}}",
              "layers": [
                {
                  "id": "{{layerId}}",
                  "name": "Layer 1",
                  "kind": "balloon",
                  "isVisible": true,
                  "isLocked": false,
                  "opacity": 1.0,
                  "balloons": [],
                  "Sfx": [
                    {
                      "id": "{{legacyBalloonId}}",
                      "layerId": "{{layerId}}",
                      "position": { "x": 320, "y": 280 },
                      "rotation": 19,
                      "text": "KRAKOOM",
                      "textStyle": {
                        "fontFamily": "Impact",
                        "fontSize": 44,
                        "fillType": "linear",
                        "fillSecondaryColor": { "r": 250, "g": 160, "b": 20, "a": 255 },
                        "fillAngle": 32,
                        "outlineColor": { "r": 20, "g": 20, "b": 20, "a": 255 },
                        "outlineWidth": 4,
                        "shadows": [
                          { "color": { "r": 0, "g": 0, "b": 0, "a": 255 }, "offsetX": 4, "offsetY": 3, "blur": 2, "opacity": 0.4 }
                        ],
                        "outerGlowEnabled": true,
                        "outerGlowColor": { "r": 255, "g": 210, "b": 80, "a": 255 },
                        "outerGlowSize": 8,
                        "outerGlowOpacity": 0.6,
                        "motionBlurEnabled": true,
                        "motionBlurDistance": 6,
                        "motionBlurAngle": 18,
                        "warpPreset": "wave",
                        "warpIntensity": 0.5,
                        "warpHorizontalDistortion": 0.2,
                        "warpVerticalDistortion": -0.1,
                        "warpMesh": {
                          "topLeftOffset": { "x": -0.1, "y": 0.05 },
                          "topRightOffset": { "x": 0.1, "y": -0.03 },
                          "bottomRightOffset": { "x": 0.06, "y": 0.02 },
                          "bottomLeftOffset": { "x": -0.05, "y": -0.02 }
                        }
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        var file = JsonSerializer.Deserialize<DocumentFile>(json, options);
        Assert.NotNull(file);

        var restored = file!.ToDocument();
        var migrated = restored.FindBalloon(legacyBalloonId);

        Assert.NotNull(migrated);
        Assert.Equal(BalloonShape.None, migrated!.Shape);
        Assert.Equal("KRAKOOM", migrated.Text);
        Assert.Equal(new Point2(320, 280), migrated.Position);
        Assert.Equal(19f, migrated.Rotation);
        Assert.Equal(TextFillType.Linear, migrated.TextStyle.FillType);
        Assert.Equal(new Color(250, 160, 20), migrated.TextStyle.FillSecondaryColor);
        Assert.True(migrated.TextStyle.OuterGlowEnabled);
        Assert.True(migrated.TextStyle.MotionBlurEnabled);
        Assert.Equal(TextWarpPreset.Wave, migrated.TextStyle.WarpPreset);
        Assert.Equal(0.5f, migrated.TextStyle.WarpIntensity);
        Assert.Equal(new Point2(-0.1f, 0.05f), migrated.TextStyle.WarpMesh.TopLeftOffset);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesBalloonTextPath()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        var layerId = doc.BalloonLayers.First().Id;
        var create = new Letterist.Commands.CreateBalloonCommand(layerId, new Point2(180, 260), "Curve");
        dispatcher.Execute(create);

        dispatcher.Execute(new Letterist.Commands.SetBalloonTextPathCommand(
            create.CreatedBalloonId,
            new TextPath(
                new Point2(-45f, 0f),
                new Point2(-18f, -24f),
                new Point2(18f, -24f),
                new Point2(45f, 0f),
                offset: 5f,
                startPosition: 0.05f,
                endPosition: 0.85f)));

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        var restoredBalloon = restored.FindBalloon(create.CreatedBalloonId);
        Assert.NotNull(restoredBalloon);
        Assert.NotNull(restoredBalloon!.TextPath);
        Assert.Equal(5f, restoredBalloon.TextPath!.Offset);
        Assert.Equal(0.05f, restoredBalloon.TextPath.StartPosition);
        Assert.Equal(0.85f, restoredBalloon.TextPath.EndPosition);
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesBalloonTemplates()
    {
        var doc = Document.Create("RoundTrip", new Size2(800, 600));
        var dispatcher = new Letterist.Commands.CommandDispatcher(doc);
        var layerId = doc.BalloonLayers.First().Id;

        var createBalloon = new Letterist.Commands.CreateBalloonCommand(
            layerId,
            new Point2(180, 240),
            "Original text",
            BalloonShape.Whisper,
            BalloonStyle.Default.With(strokeWidth: 2.2f),
            TextStyle.Default.With(italic: true, allCaps: false));
        dispatcher.Execute(createBalloon);
        dispatcher.Execute(new Letterist.Commands.CreateTailCommand(
            createBalloon.CreatedBalloonId,
            new Point2(240, 320),
            TailStyle.Curved,
            baseWidth: 13f));

        var createTemplate = new Letterist.Commands.CreateBalloonTemplateCommand(
            createBalloon.CreatedBalloonId,
            "Whisper Variant",
            description: "Quiet dialogue variant.",
            tags: new[] { "whisper", "quiet" },
            category: "Speech",
            placeholderText: "psst...",
            isFavorite: true,
            hotkeySlot: 6);
        dispatcher.Execute(createTemplate);

        var file = DocumentFile.FromDocument(
            doc,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        var restoredTemplate = restored.BalloonTemplates.FirstOrDefault(template => template.Id == createTemplate.CreatedTemplateId);
        Assert.NotNull(restoredTemplate);
        Assert.Equal("Whisper Variant", restoredTemplate!.Name);
        Assert.Equal("Quiet dialogue variant.", restoredTemplate.Description);
        Assert.Equal("Speech", restoredTemplate.Category);
        Assert.Equal("psst...", restoredTemplate.PlaceholderText);
        Assert.Equal(BalloonShape.Whisper, restoredTemplate.Shape);
        Assert.True(restoredTemplate.IsFavorite);
        Assert.Equal(6, restoredTemplate.HotkeySlot);
        Assert.Contains("whisper", restoredTemplate.Tags);
        Assert.NotNull(restoredTemplate.Tail);
        Assert.Equal(TailStyle.Curved, restoredTemplate.Tail!.Style);
        Assert.Equal(13f, restoredTemplate.Tail.BaseWidth);
    }
}
