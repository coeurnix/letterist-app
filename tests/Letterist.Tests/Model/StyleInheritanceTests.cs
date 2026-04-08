using Letterist.Commands;
using Letterist.Model;
using System.Collections.Generic;
using Xunit;

namespace Letterist.Tests.Model;

public class StyleInheritanceTests
{
    [Fact]
    public void BalloonStyleInheritance_CascadesToBalloon()
    {
        var doc = Document.Create();

        var parentStyle = BalloonStyle.Default.With(fillColor: new Color(10, 20, 30), strokeWidth: 2f);
        var parentCmd = new CreateNamedBalloonStyleCommand("Parent", parentStyle);
        parentCmd.Execute(doc);

        var childOverrides = new BalloonStyleOverride { StrokeWidth = 6f };
        var childCmd = new CreateNamedBalloonStyleCommand("Child", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var layerId = doc.ActiveLayerId;
        var createBalloon = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;

        var linkCmd = new SetBalloonStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId);
        linkCmd.Execute(doc);

        doc.RefreshStyleCache();

        var childStyle = doc.FindBalloonStyle(childCmd.CreatedStyleId)!;
        Assert.Equal(parentStyle.FillColor, childStyle.Style.FillColor);
        Assert.Equal(6f, childStyle.Style.StrokeWidth);
        Assert.Equal(parentStyle.FillColor, balloon.BalloonStyle.FillColor);

        var updatedParent = parentStyle.With(fillColor: new Color(120, 30, 40));
        var updateParent = new UpdateNamedBalloonStyleCommand(parentCmd.CreatedStyleId, updatedParent);
        updateParent.Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal(updatedParent.FillColor, balloon.BalloonStyle.FillColor);
        Assert.Equal(6f, balloon.BalloonStyle.StrokeWidth);
    }

    [Fact]
    public void TextStyleInheritance_CascadesToBalloon()
    {
        var doc = Document.Create();

        var parentStyle = TextStyle.Default.With(fontFamily: "Segoe UI", fontSize: 18f, textColor: new Color(20, 60, 100));
        var parentCmd = new CreateNamedTextStyleCommand("Parent", parentStyle);
        parentCmd.Execute(doc);

        var childOverrides = new TextStyleOverride { Bold = true };
        var childCmd = new CreateNamedTextStyleCommand("Child", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var layerId = doc.ActiveLayerId;
        var createBalloon = new CreateBalloonCommand(layerId, new Point2(200, 120), "Test");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;

        var linkCmd = new SetTextStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId);
        linkCmd.Execute(doc);

        doc.RefreshStyleCache();

        var childStyle = doc.FindTextStyle(childCmd.CreatedStyleId)!;
        Assert.Equal(parentStyle.TextColor, childStyle.Style.TextColor);
        Assert.True(childStyle.Style.Bold);
        Assert.Equal(parentStyle.TextColor, balloon.TextStyle.TextColor);
        Assert.True(balloon.TextStyle.Bold);

        var updatedParent = parentStyle.With(textColor: new Color(200, 40, 50));
        var updateParent = new UpdateNamedTextStyleCommand(parentCmd.CreatedStyleId, updatedParent);
        updateParent.Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal(updatedParent.TextColor, balloon.TextStyle.TextColor);
        Assert.True(balloon.TextStyle.Bold);
    }

    [Fact]
    public void TextStyleInheritance_WarpOverridesPersistAcrossParentUpdates()
    {
        var doc = Document.Create();

        var parentStyle = TextStyle.Default.With(
            warpPreset: TextWarpPreset.ArcDown,
            warpIntensity: 0.25f,
            warpHorizontalDistortion: 0.1f);
        var parentCmd = new CreateNamedTextStyleCommand("Parent Warp", parentStyle);
        parentCmd.Execute(doc);

        var childMesh = new TextWarpMesh
        {
            TopLeftOffset = new Point2(-0.12f, 0.04f),
            TopRightOffset = new Point2(0.08f, -0.02f),
            BottomRightOffset = new Point2(0.05f, 0.06f),
            BottomLeftOffset = new Point2(-0.07f, -0.03f)
        };
        var childOverrides = new TextStyleOverride
        {
            WarpPreset = TextWarpPreset.Wave,
            WarpIntensity = 0.6f,
            WarpHorizontalDistortion = -0.2f,
            WarpVerticalDistortion = 0.15f,
            WarpMesh = childMesh
        };
        var childCmd = new CreateNamedTextStyleCommand("Child Warp", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(180, 110), "Warp");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;
        new SetTextStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId).Execute(doc);

        doc.RefreshStyleCache();

        Assert.Equal(TextWarpPreset.Wave, balloon.TextStyle.WarpPreset);
        Assert.Equal(0.6f, balloon.TextStyle.WarpIntensity);
        Assert.Equal(-0.2f, balloon.TextStyle.WarpHorizontalDistortion);
        Assert.Equal(0.15f, balloon.TextStyle.WarpVerticalDistortion);
        Assert.Equal(childMesh.TopLeftOffset, balloon.TextStyle.WarpMesh.TopLeftOffset);

        var updatedParent = parentStyle.With(
            fontFamily: "Arial",
            warpPreset: TextWarpPreset.Bulge,
            warpIntensity: 0.9f);
        new UpdateNamedTextStyleCommand(parentCmd.CreatedStyleId, updatedParent).Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal("Arial", balloon.TextStyle.FontFamily);
        Assert.Equal(TextWarpPreset.Wave, balloon.TextStyle.WarpPreset);
        Assert.Equal(0.6f, balloon.TextStyle.WarpIntensity);
        Assert.Equal(-0.2f, balloon.TextStyle.WarpHorizontalDistortion);
        Assert.Equal(0.15f, balloon.TextStyle.WarpVerticalDistortion);
        Assert.Equal(childMesh.BottomRightOffset, balloon.TextStyle.WarpMesh.BottomRightOffset);
    }

    [Fact]
    public void TextStyleInheritance_FillOverridesPersistAcrossParentUpdates()
    {
        var doc = Document.Create();

        var parentStyle = TextStyle.Default.With(
            textColor: new Color(18, 28, 44),
            fillType: TextFillType.Linear,
            fillSecondaryColor: new Color(240, 200, 90),
            fillAngle: 20f);
        var parentCmd = new CreateNamedTextStyleCommand("Parent Fill", parentStyle);
        parentCmd.Execute(doc);

        var childOverrides = new TextStyleOverride
        {
            FillType = TextFillType.Image,
            FillImagePath = @"C:\fills\impact.png"
        };
        var childCmd = new CreateNamedTextStyleCommand("Child Fill", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(120, 120), "Fill");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;
        new SetTextStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId).Execute(doc);

        doc.RefreshStyleCache();
        Assert.Equal(TextFillType.Image, balloon.TextStyle.FillType);
        Assert.Equal(@"C:\fills\impact.png", balloon.TextStyle.FillImagePath);
        Assert.Equal(new Color(18, 28, 44), balloon.TextStyle.TextColor);

        var updatedParent = parentStyle.With(textColor: new Color(200, 50, 60), fillSecondaryColor: new Color(120, 210, 245));
        new UpdateNamedTextStyleCommand(parentCmd.CreatedStyleId, updatedParent).Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal(TextFillType.Image, balloon.TextStyle.FillType);
        Assert.Equal(@"C:\fills\impact.png", balloon.TextStyle.FillImagePath);
        Assert.Equal(new Color(200, 50, 60), balloon.TextStyle.TextColor);
    }

    [Fact]
    public void TextStyleInheritance_ShadowOverridesPersistAcrossParentUpdates()
    {
        var doc = Document.Create();

        var parentStyle = TextStyle.Default.With(
            shadows: new[]
            {
                new TextShadow { Color = new Color(0, 0, 0), OffsetX = 2f, OffsetY = 3f, Blur = 1f, Opacity = 0.5f }
            });
        var parentCmd = new CreateNamedTextStyleCommand("Parent Shadow", parentStyle);
        parentCmd.Execute(doc);

        var childOverrides = new TextStyleOverride
        {
            Shadows = new List<TextShadow>
            {
                new TextShadow { Color = new Color(255, 80, 40), OffsetX = -3f, OffsetY = 2f, Blur = 0f, Opacity = 0.35f },
                new TextShadow { Color = new Color(20, 40, 200), OffsetX = 4f, OffsetY = 5f, Blur = 2f, Opacity = 0.45f }
            }
        };
        var childCmd = new CreateNamedTextStyleCommand("Child Shadow", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(120, 120), "Shadow");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;
        new SetTextStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId).Execute(doc);

        doc.RefreshStyleCache();
        Assert.Equal(2, balloon.TextStyle.Shadows.Count);
        Assert.Equal(new Color(255, 80, 40), balloon.TextStyle.Shadows[0].Color);
        Assert.Equal(2f, balloon.TextStyle.Shadows[1].Blur);

        var updatedParent = parentStyle.With(textColor: new Color(110, 30, 50));
        new UpdateNamedTextStyleCommand(parentCmd.CreatedStyleId, updatedParent).Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal(new Color(110, 30, 50), balloon.TextStyle.TextColor);
        Assert.Equal(2, balloon.TextStyle.Shadows.Count);
        Assert.Equal(new Color(20, 40, 200), balloon.TextStyle.Shadows[1].Color);
    }

    [Fact]
    public void TextStyleInheritance_AdvancedEffectOverridesPersistAcrossParentUpdates()
    {
        var doc = Document.Create();

        var parentStyle = TextStyle.Default.With(
            outerGlowEnabled: true,
            outerGlowColor: new Color(255, 220, 90),
            outerGlowSize: 3f,
            extrusionEnabled: true,
            extrusionDepth: 2f,
            extrusionAngle: 140f,
            motionBlurEnabled: false);
        var parentCmd = new CreateNamedTextStyleCommand("Parent Advanced", parentStyle);
        parentCmd.Execute(doc);

        var childOverrides = new TextStyleOverride
        {
            OuterGlowEnabled = true,
            OuterGlowColor = new Color(130, 245, 255),
            OuterGlowSize = 8f,
            OuterGlowOpacity = 0.62f,
            InnerGlowEnabled = true,
            InnerGlowColor = new Color(30, 10, 10),
            InnerGlowSize = 2f,
            ExtrusionEnabled = true,
            ExtrusionDepth = 9f,
            ExtrusionAngle = 102f,
            ExtrusionColor = new Color(16, 16, 16),
            MotionBlurEnabled = true,
            MotionBlurDistance = 6f,
            MotionBlurAngle = -18f,
            MotionBlurOpacity = 0.3f
        };
        var childCmd = new CreateNamedTextStyleCommand("Child Advanced", parentStyle, parentStyleId: parentCmd.CreatedStyleId, overrides: childOverrides);
        childCmd.Execute(doc);

        var createBalloon = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(140, 160), "Advanced");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;
        new SetTextStyleReferenceCommand(balloon.Id, childCmd.CreatedStyleId).Execute(doc);

        doc.RefreshStyleCache();
        Assert.True(balloon.TextStyle.OuterGlowEnabled);
        Assert.Equal(new Color(130, 245, 255), balloon.TextStyle.OuterGlowColor);
        Assert.Equal(8f, balloon.TextStyle.OuterGlowSize);
        Assert.True(balloon.TextStyle.InnerGlowEnabled);
        Assert.Equal(2f, balloon.TextStyle.InnerGlowSize);
        Assert.True(balloon.TextStyle.ExtrusionEnabled);
        Assert.Equal(9f, balloon.TextStyle.ExtrusionDepth);
        Assert.True(balloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(6f, balloon.TextStyle.MotionBlurDistance);
        Assert.Equal(-18f, balloon.TextStyle.MotionBlurAngle);

        var updatedParent = parentStyle.With(
            textColor: new Color(200, 40, 30),
            outerGlowSize: 1f,
            extrusionDepth: 1f,
            motionBlurEnabled: false);
        new UpdateNamedTextStyleCommand(parentCmd.CreatedStyleId, updatedParent).Execute(doc);
        doc.RefreshStyleCache();

        Assert.Equal(new Color(200, 40, 30), balloon.TextStyle.TextColor);
        Assert.Equal(new Color(130, 245, 255), balloon.TextStyle.OuterGlowColor);
        Assert.Equal(8f, balloon.TextStyle.OuterGlowSize);
        Assert.True(balloon.TextStyle.InnerGlowEnabled);
        Assert.Equal(9f, balloon.TextStyle.ExtrusionDepth);
        Assert.True(balloon.TextStyle.MotionBlurEnabled);
        Assert.Equal(6f, balloon.TextStyle.MotionBlurDistance);
    }
}
