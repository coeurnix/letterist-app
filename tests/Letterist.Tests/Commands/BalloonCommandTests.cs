using Letterist.Commands;
using Letterist.Model;
using System.Collections.Generic;
using Xunit;

namespace Letterist.Tests.Commands;

public class BalloonCommandTests
{
    private Document CreateTestDocument()
    {
        return Document.Create("Test");
    }

    private static Layer GetFirstBalloonLayer(Document doc)
    {
        return doc.BalloonLayers.First();
    }

    [Fact]
    public void CreateBalloonCommand_AddsBalloonToLayer()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var position = new Point2(100, 100);

        var cmd = new CreateBalloonCommand(layerId, position, "Hello");
        cmd.Execute(doc);

        Assert.Single(GetFirstBalloonLayer(doc).Balloons);
        var balloon = GetFirstBalloonLayer(doc).Balloons[0];
        Assert.Equal(position, balloon.Position);
        Assert.Equal("Hello", balloon.Text);
        Assert.Equal(cmd.CreatedBalloonId, balloon.Id);
    }

    [Fact]
    public void CreateBalloonCommand_Undo_RemovesBalloon()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        cmd.Execute(doc);
        Assert.Single(GetFirstBalloonLayer(doc).Balloons);

        cmd.Undo(doc);

        Assert.Empty(GetFirstBalloonLayer(doc).Balloons);
    }

    [Fact]
    public void CreateBalloonCommand_WithCustomShape()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd = new CreateBalloonCommand(layerId, new Point2(100, 100), shape: BalloonShape.Rectangle);
        cmd.Execute(doc);

        Assert.Equal(BalloonShape.Rectangle, GetFirstBalloonLayer(doc).Balloons[0].Shape);
    }

    [Fact]
    public void MoveBalloonCommand_ChangesPosition()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var moveCmd = new MoveBalloonCommand(balloonId, new Point2(200, 300));
        moveCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon);
        Assert.Equal(new Point2(200, 300), balloon.Position);
    }

    [Fact]
    public void MoveBalloonCommand_Undo_RestoresPosition()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var originalPosition = new Point2(100, 100);
        var createCmd = new CreateBalloonCommand(layerId, originalPosition);
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var moveCmd = new MoveBalloonCommand(balloonId, new Point2(200, 300));
        moveCmd.Execute(doc);
        moveCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(originalPosition, balloon!.Position);
    }

    [Fact]
    public void ReorderBalloonCommand_ExecuteUndo_UpdatesZOrder()
    {
        var doc = CreateTestDocument();
        var layer = GetFirstBalloonLayer(doc);

        var createA = new CreateBalloonCommand(layer.Id, new Point2(10, 10), "A");
        var createB = new CreateBalloonCommand(layer.Id, new Point2(20, 20), "B");
        var createC = new CreateBalloonCommand(layer.Id, new Point2(30, 30), "C");
        createA.Execute(doc);
        createB.Execute(doc);
        createC.Execute(doc);

        var originalOrder = layer.Balloons.Select(balloon => balloon.Id).ToList();

        var reorder = new ReorderBalloonCommand(createA.CreatedBalloonId, 2);
        reorder.Execute(doc);

        Assert.Equal(createA.CreatedBalloonId, layer.Balloons[2].Id);

        reorder.Undo(doc);
        Assert.Equal(originalOrder, layer.Balloons.Select(balloon => balloon.Id).ToList());
    }

    [Fact]
    public void SetBalloonConstrainToPanelCommand_ClampsCenterAndAllowsPartialOverflow()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var panelBounds = new Rect(0, 0, 200, 200);
        var panelCmd = new CreatePanelZoneCommand(pageId, "Panel 1", panelBounds);
        panelCmd.Execute(doc);
        var panelId = panelCmd.CreatedPanelId;

        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), panelId: panelId);
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var resizeCmd = new ResizeBalloonCommand(balloonId, new Size2(40, 40), new Point2(100, 100));
        resizeCmd.Execute(doc);

        var constrainCmd = new SetBalloonConstrainToPanelCommand(balloonId, true);
        constrainCmd.Execute(doc);

        var moveCmd = new MoveBalloonCommand(balloonId, new Point2(500, 500));
        moveCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        var panel = doc.FindPanel(panelId)!;

        Assert.True(panel.Bounds.Contains(balloon.Position));
        Assert.False(panel.Bounds.Contains(balloon.Bounds));
        Assert.NotEqual(new Point2(500, 500), balloon.Position);
    }

    [Fact]
    public void SetBalloonPanelCommand_ClearsConstraintWhenRemoved()
    {
        var doc = CreateTestDocument();
        var pageId = doc.ActivePageId;
        var panelCmd = new CreatePanelZoneCommand(pageId, "Panel 1", new Rect(0, 0, 200, 200));
        panelCmd.Execute(doc);
        var panelId = panelCmd.CreatedPanelId;

        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(60, 60), panelId: panelId, constrainToPanel: true);
        createCmd.Execute(doc);
        var balloon = doc.FindBalloon(createCmd.CreatedBalloonId)!;

        Assert.True(balloon.ConstrainToPanel);

        var setPanelCmd = new SetBalloonPanelCommand(balloon.Id, null);
        setPanelCmd.Execute(doc);

        Assert.False(balloon.ConstrainToPanel);
        Assert.Null(balloon.PanelId);
    }

    [Fact]
    public void DeleteBalloonCommand_RemovesBalloon()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var deleteCmd = new DeleteBalloonCommand(balloonId);
        deleteCmd.Execute(doc);

        Assert.Empty(GetFirstBalloonLayer(doc).Balloons);
        Assert.Null(doc.FindBalloon(balloonId));
    }

    [Fact]
    public void DeleteBalloonCommand_Undo_RestoresBalloon()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test Text");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var deleteCmd = new DeleteBalloonCommand(balloonId);
        deleteCmd.Execute(doc);
        deleteCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon);
        Assert.Equal("Test Text", balloon.Text);
        Assert.Equal(new Point2(100, 100), balloon.Position);
    }

    [Fact]
    public void DeleteBalloonCommand_ClearsSelection()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        doc.SetSelectedBalloonId(balloonId);

        var deleteCmd = new DeleteBalloonCommand(balloonId);
        deleteCmd.Execute(doc);

        Assert.Null(doc.SelectedBalloonId);
    }

    [Fact]
    public void SetBalloonTextCommand_ChangesText()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Original");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var textCmd = new SetBalloonTextCommand(balloonId, "Updated");
        textCmd.Execute(doc);

        Assert.Equal("Updated", doc.FindBalloon(balloonId)!.Text);
    }

    [Fact]
    public void SetBalloonTextCommand_Undo_RestoresText()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Original");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var textCmd = new SetBalloonTextCommand(balloonId, "Updated");
        textCmd.Execute(doc);
        textCmd.Undo(doc);

        Assert.Equal("Original", doc.FindBalloon(balloonId)!.Text);
    }

    [Fact]
    public void SetBalloonVisibilityCommand_ExecuteUndo_TogglesAndRestoresSelection()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Visible");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        doc.SetSelectedBalloonId(balloonId);

        var cmd = new SetBalloonVisibilityCommand(balloonId, false);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        Assert.False(balloon.IsVisible);
        Assert.Null(doc.SelectedBalloonId);

        cmd.Undo(doc);
        Assert.True(balloon.IsVisible);
        Assert.Equal(balloonId, doc.SelectedBalloonId);
    }

    [Fact]
    public void SetBalloonLockedCommand_ExecuteUndo_TogglesAndRestoresSelection()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Locked");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        doc.SetSelectedBalloonId(balloonId);

        var cmd = new SetBalloonLockedCommand(balloonId, true);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        Assert.True(balloon.IsLocked);
        Assert.Null(doc.SelectedBalloonId);

        cmd.Undo(doc);
        Assert.False(balloon.IsLocked);
        Assert.Equal(balloonId, doc.SelectedBalloonId);
    }

    [Fact]
    public void SetBalloonTextCommand_ClearsInlineSpans()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Original");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var balloon = doc.FindBalloon(balloonId)!;
        balloon.SetTextStyleSpans(new[]
        {
            new TextStyleSpan(0, 3, balloon.TextStyle.With(bold: true))
        });

        var textCmd = new SetBalloonTextCommand(balloonId, "Updated");
        textCmd.Execute(doc);

        Assert.Empty(doc.FindBalloon(balloonId)!.TextStyleSpans);

        textCmd.Undo(doc);
        Assert.Single(doc.FindBalloon(balloonId)!.TextStyleSpans);
    }

    [Fact]
    public void SetBalloonRichTextCommand_PersistsSpans()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Original");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var spans = new List<TextStyleSpan>
        {
            new TextStyleSpan(0, 4, TextStyle.Default.With(bold: true))
        };

        var richCmd = new SetBalloonRichTextCommand(balloonId, "Bold", spans);
        richCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        Assert.Single(balloon.TextStyleSpans);
        Assert.True(balloon.TextStyleSpans[0].Style.Bold);
        Assert.Equal(4, balloon.TextStyleSpans[0].Length);

        richCmd.Undo(doc);
        Assert.Equal("Original", doc.FindBalloon(balloonId)!.Text);
    }

    [Fact]
    public void SetBalloonShapeCommand_ChangesShape()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), shape: BalloonShape.Oval);
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var shapeCmd = new SetBalloonShapeCommand(balloonId, BalloonShape.RoundedRect);
        shapeCmd.Execute(doc);

        Assert.Equal(BalloonShape.RoundedRect, doc.FindBalloon(balloonId)!.Shape);
    }

    [Fact]
    public void SetBalloonShapeCommand_Undo_RestoresShape()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), shape: BalloonShape.Oval);
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var shapeCmd = new SetBalloonShapeCommand(balloonId, BalloonShape.Rectangle);
        shapeCmd.Execute(doc);
        shapeCmd.Undo(doc);

        Assert.Equal(BalloonShape.Oval, doc.FindBalloon(balloonId)!.Shape);
    }

    [Fact]
    public void SetBalloonCustomShapeCommand_SetsShapeAndPath()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), shape: BalloonShape.Oval);
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var pathData = "M0 0 L10 0 L10 10 Z";
        var cmd = new SetBalloonCustomShapeCommand(balloonId, pathData);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        Assert.Equal(BalloonShape.Custom, balloon.Shape);
        Assert.Equal(pathData, balloon.CustomShapePathData);

        cmd.Undo(doc);
        balloon = doc.FindBalloon(balloonId)!;
        Assert.Equal(BalloonShape.Oval, balloon.Shape);
        Assert.Null(balloon.CustomShapePathData);
    }

    [Fact]
    public void SetBalloonTextPathCommand_ExecuteAndUndo_RestoresPath()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Path");
        createCmd.Execute(doc);
        var balloon = doc.FindBalloon(createCmd.CreatedBalloonId)!;

        var path = new TextPath(
            new Point2(-40f, 0f),
            new Point2(-10f, -30f),
            new Point2(10f, -30f),
            new Point2(40f, 0f),
            offset: 8f,
            startPosition: 0.1f,
            endPosition: 0.9f,
            reverseDirection: true);

        var cmd = new SetBalloonTextPathCommand(balloon.Id, path);
        cmd.Execute(doc);

        Assert.NotNull(balloon.TextPath);
        Assert.Equal(8f, balloon.TextPath!.Offset);
        Assert.True(balloon.TextPath.ReverseDirection);

        cmd.Undo(doc);
        Assert.Null(balloon.TextPath);
    }

    [Fact]
    public void SetBalloonStyleReferenceCommand_WithExtendedStyle_AppliesAllNonGeometryDetails()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var pageId = doc.ActivePageId;
        var panelCmd = new CreatePanelZoneCommand(pageId, "Panel", new Rect(0, 0, 300, 300));
        panelCmd.Execute(doc);
        var panelId = panelCmd.CreatedPanelId;

        var sourceCreate = new CreateBalloonCommand(layerId, new Point2(110, 120), "Source", panelId: panelId, constrainToPanel: true);
        sourceCreate.Execute(doc);
        var source = doc.FindBalloon(sourceCreate.CreatedBalloonId)!;
        source.SetShape(BalloonShape.Custom);
        source.SetCustomShapePathData("M0 0 L10 0 L10 10 Z");
        source.SetBalloonStyle(BalloonStyle.Default.With(fillColor: new Color(240, 220, 180), strokeWidth: 3.4f));
        source.SetTextStyle(TextStyle.Default.With(bold: true, italic: true, fontSize: 18f));
        source.SetTextPath(new TextPath(
            new Point2(-35f, 0f),
            new Point2(-12f, -18f),
            new Point2(12f, -18f),
            new Point2(35f, 0f),
            offset: 6f,
            reverseDirection: true));
        source.ClearTails();
        source.AddTail(new Tail(Guid.NewGuid(), new Point2(110, 220), TailStyle.Curved, 20f));
        source.AddTail(new Tail(Guid.NewGuid(), new Point2(70, 215), TailStyle.Pointer, 15f));

        var styleCreate = new CreateNamedBalloonStyleCommand(
            "Extended Snapshot",
            source.BalloonStyle,
            applyExtendedDetails: true,
            shape: source.Shape,
            customShapePathData: source.CustomShapePathData,
            constrainToPanel: source.ConstrainToPanel,
            textStyle: source.TextStyle,
            textPath: source.TextPath?.Clone(),
            tails: source.Tails.Select(tail => BalloonTemplateTail.FromTail(tail, source.Position)));
        styleCreate.Execute(doc);

        var targetCreate = new CreateBalloonCommand(layerId, new Point2(260, 260), "Target");
        targetCreate.Execute(doc);
        var target = doc.FindBalloon(targetCreate.CreatedBalloonId)!;
        target.SetShape(BalloonShape.Rectangle);
        target.SetCustomShapePathData(null);
        target.SetBalloonStyle(BalloonStyle.Default.With(fillColor: new Color(200, 255, 200), strokeWidth: 1f));
        target.SetTextStyle(TextStyle.Default.With(bold: false, italic: false, fontSize: 12f));
        target.ClearTails();
        target.AddTail(new Tail(Guid.NewGuid(), new Point2(300, 320), TailStyle.Pointer, 10f));
        target.Tails[0].SetInset(19f);
        target.Tails[0].SetControlPoint(new Point2(280f, 330f));
        target.Tails[0].SetAttachmentDirection(new Point2(0.6f, 0.8f));
        target.SetRotation(32f);
        target.SetComputedSize(new Size2(180, 90));

        var originalText = target.Text;
        var originalPosition = target.Position;
        var originalRotation = target.Rotation;
        var originalSize = target.ComputedSize;
        var originalTailTarget = target.Tails[0].TargetPoint;
        var originalTailInset = target.Tails[0].Inset;
        var originalTailControl = target.Tails[0].ControlPoint;
        var originalTailAttachment = target.Tails[0].AttachmentDirection;

        var applyCmd = new SetBalloonStyleReferenceCommand(target.Id, styleCreate.CreatedStyleId);
        applyCmd.Execute(doc);

        Assert.Equal(source.Shape, target.Shape);
        Assert.Equal(source.CustomShapePathData, target.CustomShapePathData);
        Assert.False(target.ConstrainToPanel);
        Assert.True(BalloonStyleUtilities.AreEquivalent(source.BalloonStyle, target.BalloonStyle));
        Assert.True(TextStyleUtilities.AreEquivalent(source.TextStyle, target.TextStyle));
        Assert.Equal(source.TextPath, target.TextPath);
        Assert.Equal(2, target.Tails.Count);
        Assert.Equal(TailStyle.Curved, target.Tails[0].Style);
        Assert.Equal(TailStyle.Pointer, target.Tails[1].Style);
        Assert.Equal(20f, target.Tails[0].BaseWidth);
        Assert.Equal(source.Tails[0].Curvature, target.Tails[0].Curvature);
        Assert.Equal(originalTailTarget, target.Tails[0].TargetPoint);
        Assert.Equal(originalTailInset, target.Tails[0].Inset);
        Assert.Equal(originalTailControl, target.Tails[0].ControlPoint);
        Assert.Equal(originalTailAttachment, target.Tails[0].AttachmentDirection);

        Assert.Equal(originalText, target.Text);
        Assert.Equal(originalPosition, target.Position);
        Assert.Equal(originalRotation, target.Rotation);
        Assert.Equal(originalSize, target.ComputedSize);

        applyCmd.Undo(doc);
        Assert.Equal(BalloonShape.Rectangle, target.Shape);
        Assert.Single(target.Tails);
        Assert.Equal(32f, target.Rotation);
        Assert.Equal(originalText, target.Text);
    }

    [Fact]
    public void SetTextStyleCommand_ExecuteUndoRedo_PreservesAdvancedStyleProperties()
    {
        var doc = CreateTestDocument();
        var dispatcher = new CommandDispatcher(doc);
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(180, 220), "KRAK");
        dispatcher.Execute(createCmd);
        var balloon = doc.FindBalloon(createCmd.CreatedBalloonId)!;
        var originalStyle = balloon.TextStyle;

        var advancedStyle = TextStyle.Default.With(
            fontFamily: "Impact",
            fontSize: 36f,
            textColor: new Color(245, 240, 230),
            fillType: TextFillType.Linear,
            fillSecondaryColor: new Color(255, 180, 40),
            fillAngle: 35f,
            fillPattern: TextFillPattern.Crosshatch,
            fillPatternScale: 1.6f,
            fillImagePath: @"C:\fills\fire.png",
            outlineColor: new Color(24, 24, 30),
            outlineWidth: 3.5f,
            additionalStrokes: new[]
            {
                new TextStroke { Color = new Color(255, 230, 120), Width = 4f },
                new TextStroke { Color = new Color(130, 10, 20), Width = 6f }
            },
            shadows: new[]
            {
                new TextShadow { Color = new Color(0, 0, 0), OffsetX = 4f, OffsetY = 5f, Blur = 2f, Opacity = 0.45f },
                new TextShadow { Color = new Color(255, 80, 20), OffsetX = -3f, OffsetY = 1f, Blur = 0f, Opacity = 0.35f }
            },
            outerGlowEnabled: true,
            outerGlowColor: new Color(255, 220, 90),
            outerGlowSize: 7f,
            outerGlowOpacity: 0.6f,
            innerGlowEnabled: true,
            innerGlowColor: new Color(40, 16, 12),
            innerGlowSize: 3f,
            innerGlowOpacity: 0.4f,
            extrusionEnabled: true,
            extrusionDepth: 8f,
            extrusionAngle: 125f,
            extrusionColor: new Color(40, 8, 8),
            extrusionOpacity: 0.7f,
            motionBlurEnabled: true,
            motionBlurDistance: 6f,
            motionBlurAngle: 24f,
            motionBlurOpacity: 0.33f,
            bold: true,
            italic: true,
            allCaps: true,
            tracking: 0.08f,
            lineHeight: 1.3f,
            alignment: TextAlignment.Left,
            fitMode: TextFitMode.ShrinkToFit,
            overflowMode: TextOverflowMode.Clip,
            verticalOffset: 1.5f,
            ragMode: RagMode.Tight,
            hyphenationLocale: "en-US",
            justificationStrength: 81,
            hyphenationLevel: 42,
            fillHeight: true,
            warpPreset: TextWarpPreset.Flag,
            warpIntensity: 0.5f,
            warpHorizontalDistortion: 0.2f,
            warpVerticalDistortion: -0.1f,
            warpMesh: new TextWarpMesh
            {
                TopLeftOffset = new Point2(-0.08f, 0.04f),
                TopRightOffset = new Point2(0.1f, -0.03f),
                BottomRightOffset = new Point2(0.07f, 0.03f),
                BottomLeftOffset = new Point2(-0.06f, -0.02f)
            });

        dispatcher.Execute(new SetTextStyleCommand(balloon.Id, advancedStyle));

        Assert.Equal(TextFillType.Linear, balloon.TextStyle.FillType);
        Assert.Equal(2, balloon.TextStyle.AdditionalStrokes.Count);
        Assert.Equal(2, balloon.TextStyle.Shadows.Count);
        Assert.True(balloon.TextStyle.OuterGlowEnabled);
        Assert.True(balloon.TextStyle.InnerGlowEnabled);
        Assert.True(balloon.TextStyle.ExtrusionEnabled);
        Assert.True(balloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(TextWarpPreset.Flag, balloon.TextStyle.WarpPreset);
        Assert.Equal(0.5f, balloon.TextStyle.WarpIntensity);
        Assert.Equal(new Point2(-0.08f, 0.04f), balloon.TextStyle.WarpMesh.TopLeftOffset);
        Assert.Equal(RagMode.Tight, balloon.TextStyle.RagMode);
        Assert.Equal("en-US", balloon.TextStyle.HyphenationLocale);
        Assert.True(balloon.TextStyle.FillHeight);

        dispatcher.Undo();

        Assert.Equal(originalStyle.FillType, balloon.TextStyle.FillType);
        Assert.Equal(originalStyle.AdditionalStrokes.Count, balloon.TextStyle.AdditionalStrokes.Count);
        Assert.Equal(originalStyle.Shadows.Count, balloon.TextStyle.Shadows.Count);
        Assert.Equal(originalStyle.OuterGlowEnabled, balloon.TextStyle.OuterGlowEnabled);
        Assert.Equal(originalStyle.ExtrusionEnabled, balloon.TextStyle.ExtrusionEnabled);
        Assert.Equal(originalStyle.MotionBlurEnabled, balloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(originalStyle.WarpPreset, balloon.TextStyle.WarpPreset);
        Assert.Equal(originalStyle.WarpIntensity, balloon.TextStyle.WarpIntensity);
        Assert.Equal(originalStyle.WarpMesh.TopLeftOffset, balloon.TextStyle.WarpMesh.TopLeftOffset);
        Assert.Equal(originalStyle.RagMode, balloon.TextStyle.RagMode);
        Assert.Equal(originalStyle.HyphenationLocale, balloon.TextStyle.HyphenationLocale);
        Assert.Equal(originalStyle.FillHeight, balloon.TextStyle.FillHeight);

        dispatcher.Redo();

        Assert.Equal(TextFillType.Linear, balloon.TextStyle.FillType);
        Assert.Equal(2, balloon.TextStyle.AdditionalStrokes.Count);
        Assert.Equal(2, balloon.TextStyle.Shadows.Count);
        Assert.True(balloon.TextStyle.OuterGlowEnabled);
        Assert.True(balloon.TextStyle.InnerGlowEnabled);
        Assert.True(balloon.TextStyle.ExtrusionEnabled);
        Assert.True(balloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(TextWarpPreset.Flag, balloon.TextStyle.WarpPreset);
        Assert.Equal(0.5f, balloon.TextStyle.WarpIntensity);
        Assert.Equal(new Point2(-0.08f, 0.04f), balloon.TextStyle.WarpMesh.TopLeftOffset);
        Assert.Equal(RagMode.Tight, balloon.TextStyle.RagMode);
        Assert.Equal("en-US", balloon.TextStyle.HyphenationLocale);
        Assert.True(balloon.TextStyle.FillHeight);
    }

    [Fact]
    public void CommandDispatcher_ExecuteAndUndo()
    {
        var doc = CreateTestDocument();
        var dispatcher = new CommandDispatcher(doc);
        var layerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        dispatcher.Execute(createCmd);

        Assert.Single(GetFirstBalloonLayer(doc).Balloons);
        Assert.True(doc.IsDirty);

        dispatcher.Undo();

        Assert.Empty(GetFirstBalloonLayer(doc).Balloons);
    }

    [Fact]
    public void CommandDispatcher_Redo()
    {
        var doc = CreateTestDocument();
        var dispatcher = new CommandDispatcher(doc);
        var layerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        dispatcher.Execute(createCmd);
        dispatcher.Undo();

        Assert.Empty(GetFirstBalloonLayer(doc).Balloons);

        dispatcher.Redo();

        Assert.Single(GetFirstBalloonLayer(doc).Balloons);
    }

    [Fact]
    public void CommandDispatcher_Transaction_GroupsCommands()
    {
        var doc = CreateTestDocument();
        var dispatcher = new CommandDispatcher(doc);
        var layerId = GetFirstBalloonLayer(doc).Id;

        var cmd1 = new CreateBalloonCommand(layerId, new Point2(100, 100), "One");
        var cmd2 = new CreateBalloonCommand(layerId, new Point2(200, 200), "Two");

        dispatcher.ExecuteTransaction("Create two balloons", cmd1, cmd2);

        Assert.Equal(2, GetFirstBalloonLayer(doc).Balloons.Count);
        Assert.Equal(1, dispatcher.History.UndoCount);

        dispatcher.Undo();

        Assert.Empty(GetFirstBalloonLayer(doc).Balloons);
    }

    [Fact]
    public void RotateBalloonCommand_ChangesRotation()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        var rotateCmd = new RotateBalloonCommand(balloonId, 45f);
        rotateCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon);
        Assert.Equal(45f, balloon.Rotation);
    }

    [Fact]
    public void RotateBalloonCommand_Undo_RestoresRotation()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;

        Assert.Equal(0f, doc.FindBalloon(balloonId)!.Rotation);

        var rotateCmd = new RotateBalloonCommand(balloonId, 90f);
        rotateCmd.Execute(doc);
        Assert.Equal(90f, doc.FindBalloon(balloonId)!.Rotation);

        rotateCmd.Undo(doc);
        Assert.Equal(0f, doc.FindBalloon(balloonId)!.Rotation);
    }

    [Fact]
    public void Balloon_Clone_PreservesRotation()
    {
        var balloon = Balloon.Create(Guid.NewGuid(), new Point2(100, 100), "Test");
        balloon.SetRotation(30f);

        var clone = balloon.Clone();

        Assert.Equal(30f, clone.Rotation);
    }

    [Fact]
    public void Balloon_CloneWithNewId_PreservesRotation()
    {
        var balloon = Balloon.Create(Guid.NewGuid(), new Point2(100, 100), "Test");
        balloon.SetRotation(-15f);

        var clone = balloon.CloneWithNewId();

        Assert.NotEqual(balloon.Id, clone.Id);
        Assert.Equal(-15f, clone.Rotation);
    }

    [Fact]
    public void ResizeBalloonCommand_ManualFitting_PreservesNewMaxTextWidth()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test text");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        var balloon = doc.FindBalloon(balloonId)!;

        balloon.SetMaxTextWidth(80f);
        balloon.SetMaxTextHeight(44f);
        var originalMaxTextWidth = balloon.MaxTextWidth;
        var originalMaxTextHeight = balloon.MaxTextHeight;

        var newSize = new Size2(150, 60);
        var newMaxTextWidth = 130f; // New MaxTextWidth based on new size
        var newMaxTextHeight = 46f;
        var resizeCmd = new ResizeBalloonCommand(
            balloonId,
            newSize,
            balloon.Position,
            wasManualFitting: true,
            newMaxTextWidth: newMaxTextWidth,
            newMaxTextHeight: newMaxTextHeight);
        resizeCmd.Execute(doc);

        Assert.Equal(newSize, balloon.ComputedSize);
        Assert.Equal(newMaxTextWidth, balloon.MaxTextWidth);
        Assert.Equal(newMaxTextHeight, balloon.MaxTextHeight);
        Assert.NotEqual(originalMaxTextWidth, balloon.MaxTextWidth);
        Assert.NotEqual(originalMaxTextHeight, balloon.MaxTextHeight);
    }

    [Fact]
    public void ResizeBalloonCommand_ManualFitting_Undo_RestoresOriginalMaxTextWidth()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        var balloon = doc.FindBalloon(balloonId)!;

        balloon.SetMaxTextWidth(80f);
        balloon.SetMaxTextHeight(44f);
        var originalSize = balloon.ComputedSize;
        var originalMaxTextWidth = balloon.MaxTextWidth;
        var originalMaxTextHeight = balloon.MaxTextHeight;

        var newSize = new Size2(150, 60);
        var newMaxTextWidth = 130f;
        var newMaxTextHeight = 46f;
        var resizeCmd = new ResizeBalloonCommand(
            balloonId,
            newSize,
            balloon.Position,
            wasManualFitting: true,
            newMaxTextWidth: newMaxTextWidth,
            newMaxTextHeight: newMaxTextHeight);
        resizeCmd.Execute(doc);

        resizeCmd.Undo(doc);

        Assert.Equal(originalSize, balloon.ComputedSize);
        Assert.Equal(originalMaxTextWidth, balloon.MaxTextWidth);
        Assert.Equal(originalMaxTextHeight, balloon.MaxTextHeight);
    }

    [Fact]
    public void ResizeBalloonCommand_ManualFitting_WithoutExplicitMaxDims_UsesNewSize()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;

        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        createCmd.Execute(doc);
        var balloonId = createCmd.CreatedBalloonId;
        var balloon = doc.FindBalloon(balloonId)!;

        balloon.SetMaxTextWidth(80f);
        balloon.SetMaxTextHeight(44f);

        var newSize = new Size2(200f, 120f);
        var resizeCmd = new ResizeBalloonCommand(balloonId, newSize, balloon.Position, wasManualFitting: true);
        resizeCmd.Execute(doc);

        var style = balloon.BalloonStyle;
        Assert.Equal(newSize, balloon.ComputedSize);
        Assert.Equal(newSize.Width - style.PaddingLeft - style.PaddingRight, balloon.MaxTextWidth);
        Assert.Equal(newSize.Height - style.PaddingTop - style.PaddingBottom, balloon.MaxTextHeight);
    }
}
