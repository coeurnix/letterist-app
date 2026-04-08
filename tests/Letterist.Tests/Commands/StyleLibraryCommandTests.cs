using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class StyleLibraryCommandTests
{
    [Fact]
    public void CreateNamedBalloonStyleCommand_AddsStyle()
    {
        var doc = Document.Create("Test");
        var initialCount = doc.BalloonStyles.Count;
        var style = BalloonStyle.Default.With(strokeWidth: 3f);

        var cmd = new CreateNamedBalloonStyleCommand("New Balloon", style);
        cmd.Execute(doc);

        Assert.Equal(initialCount + 1, doc.BalloonStyles.Count);
        Assert.Contains(doc.BalloonStyles, s => s.Id == cmd.CreatedStyleId);

        cmd.Undo(doc);
        Assert.Equal(initialCount, doc.BalloonStyles.Count);
    }

    [Fact]
    public void UpdateNamedBalloonStyleCommand_UpdatesStyle()
    {
        var doc = Document.Create("Test");
        var target = doc.BalloonStyles[0];
        var original = target.Style;
        var updated = original.With(strokeWidth: original.StrokeWidth + 2f);

        var cmd = new UpdateNamedBalloonStyleCommand(target.Id, updated);
        cmd.Execute(doc);

        Assert.Equal(updated.StrokeWidth, doc.FindBalloonStyle(target.Id)!.Style.StrokeWidth);

        cmd.Undo(doc);
        Assert.Equal(original.StrokeWidth, doc.FindBalloonStyle(target.Id)!.Style.StrokeWidth);
    }

    [Fact]
    public void SetNamedBalloonStyleQuickSelectCommand_TogglesAndUndoes()
    {
        var doc = Document.Create("Test");
        var target = doc.BalloonStyles[0];

        var cmd = new SetNamedBalloonStyleQuickSelectCommand(target.Id, false);
        cmd.Execute(doc);

        Assert.False(doc.FindBalloonStyle(target.Id)!.IsQuickSelect);

        cmd.Undo(doc);
        Assert.True(doc.FindBalloonStyle(target.Id)!.IsQuickSelect);
    }

    [Fact]
    public void CreateNamedBalloonStyleCommand_CapturesExtendedDetails()
    {
        var doc = Document.Create("Test");
        var textPath = new TextPath(
            new Point2(-20f, 0f),
            new Point2(-10f, -10f),
            new Point2(10f, -10f),
            new Point2(20f, 0f),
            offset: 3f);
        var tails = new[]
        {
            new BalloonTemplateTail(
                new Point2(0f, 80f),
                TailStyle.Curved,
                18f,
                attachmentDirection: new Point2(0f, 1f),
                controlPointOffset: new Point2(12f, 6f),
                curvature: -0.4f)
        };

        var cmd = new CreateNamedBalloonStyleCommand(
            "Extended",
            BalloonStyle.Default.With(strokeWidth: 3f),
            applyExtendedDetails: true,
            shape: BalloonShape.Custom,
            customShapePathData: "M0 0 L1 0 L1 1 Z",
            constrainToPanel: true,
            textStyle: TextStyle.Default.With(bold: true, italic: true),
            textPath: textPath,
            tails: tails);
        cmd.Execute(doc);

        var style = doc.FindBalloonStyle(cmd.CreatedStyleId);
        Assert.NotNull(style);
        Assert.True(style!.ApplyExtendedDetails);
        Assert.Equal(BalloonShape.Custom, style.Shape);
        Assert.Equal("M0 0 L1 0 L1 1 Z", style.CustomShapePathData);
        Assert.True(style.ConstrainToPanel);
        Assert.True(style.TextStyle.Bold);
        Assert.True(style.TextStyle.Italic);
        Assert.NotNull(style.TextPath);
        Assert.Equal(3f, style.TextPath!.Offset);
        Assert.Single(style.Tails);
        Assert.Equal(TailStyle.Curved, style.Tails[0].Style);
        Assert.Equal(18f, style.Tails[0].BaseWidth);
        Assert.Equal(new Point2(0f, 1f), style.Tails[0].AttachmentDirection);
        Assert.Equal(new Point2(12f, 6f), style.Tails[0].ControlPointOffset);
        Assert.Equal(-0.4f, style.Tails[0].Curvature);
    }

    [Fact]
    public void UpdateNamedBalloonStyleCommand_PartialExtendedUpdate_DoesNotResetOtherExtendedFields()
    {
        var doc = Document.Create("Test");
        var create = new CreateNamedBalloonStyleCommand(
            "Extended",
            BalloonStyle.Default.With(strokeWidth: 2.5f),
            applyExtendedDetails: true,
            shape: BalloonShape.Custom,
            customShapePathData: "M0 0 L3 0 L3 2 Z",
            constrainToPanel: true,
            textStyle: TextStyle.Default.With(bold: true, ragMode: RagMode.Justified, fillHeight: true),
            textPath: new TextPath(
                new Point2(-10f, 0f),
                new Point2(-4f, -6f),
                new Point2(4f, -6f),
                new Point2(10f, 0f),
                offset: 4f),
            tails: new[]
            {
                new BalloonTemplateTail(new Point2(0f, 70f), TailStyle.Pointer, 14f, curvature: 0.6f)
            });
        create.Execute(doc);

        var before = doc.FindBalloonStyle(create.CreatedStyleId)!;
        var update = new UpdateNamedBalloonStyleCommand(
            before.Id,
            before.Style.With(strokeWidth: before.Style.StrokeWidth + 1f),
            applyExtendedDetails: false);
        update.Execute(doc);

        var after = doc.FindBalloonStyle(before.Id)!;
        Assert.True(after.ApplyExtendedDetails);
        Assert.Equal(BalloonShape.Custom, after.Shape);
        Assert.Equal("M0 0 L3 0 L3 2 Z", after.CustomShapePathData);
        Assert.True(after.ConstrainToPanel);
        Assert.True(after.TextStyle.Bold);
        Assert.Equal(RagMode.Justified, after.TextStyle.RagMode);
        Assert.True(after.TextStyle.FillHeight);
        Assert.NotNull(after.TextPath);
        Assert.Equal(4f, after.TextPath!.Offset);
        Assert.Single(after.Tails);
        Assert.Equal(0.6f, after.Tails[0].Curvature);

        update.Undo(doc);
        var restored = doc.FindBalloonStyle(before.Id)!;
        Assert.True(restored.ApplyExtendedDetails);
        Assert.Equal(BalloonShape.Custom, restored.Shape);
        Assert.Equal("M0 0 L3 0 L3 2 Z", restored.CustomShapePathData);
        Assert.True(restored.ConstrainToPanel);
        Assert.True(restored.TextStyle.Bold);
        Assert.Equal(RagMode.Justified, restored.TextStyle.RagMode);
        Assert.True(restored.TextStyle.FillHeight);
        Assert.NotNull(restored.TextPath);
        Assert.Equal(4f, restored.TextPath!.Offset);
        Assert.Single(restored.Tails);
        Assert.Equal(0.6f, restored.Tails[0].Curvature);
    }

    [Fact]
    public void RenameNamedTextStyleCommand_RenamesStyle()
    {
        var doc = Document.Create("Test");
        var target = doc.TextStyles[0];
        var original = target.Name;

        var cmd = new RenameNamedTextStyleCommand(target.Id, "Updated Name");
        cmd.Execute(doc);

        Assert.Equal("Updated Name", doc.FindTextStyle(target.Id)!.Name);

        cmd.Undo(doc);
        Assert.Equal(original, doc.FindTextStyle(target.Id)!.Name);
    }
}
