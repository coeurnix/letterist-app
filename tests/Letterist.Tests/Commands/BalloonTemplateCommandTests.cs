using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class BalloonTemplateCommandTests
{
    [Fact]
    public void CreateBalloonTemplateCommand_AddsTemplate_AndUndoRemoves()
    {
        var doc = Document.Create("Test");
        var layerId = doc.BalloonLayers.First().Id;
        var createBalloon = new CreateBalloonCommand(layerId, new Point2(180, 240), "Hero line");
        createBalloon.Execute(doc);
        var balloon = doc.FindBalloon(createBalloon.CreatedBalloonId)!;
        new CreateTailCommand(balloon.Id, new Point2(220, 320), TailStyle.Pointer, baseWidth: 20f).Execute(doc);

        var initialCount = doc.BalloonTemplates.Count;
        var createTemplate = new CreateBalloonTemplateCommand(
            balloon.Id,
            "Hero Dialogue",
            category: "Speech",
            placeholderText: "Template text",
            isFavorite: true,
            hotkeySlot: 5);

        createTemplate.Execute(doc);

        Assert.Equal(initialCount + 1, doc.BalloonTemplates.Count);
        var template = Assert.Single(doc.BalloonTemplates.Where(item => item.Id == createTemplate.CreatedTemplateId));
        Assert.Equal("Hero Dialogue", template.Name);
        Assert.Equal("Speech", template.Category);
        Assert.Equal("Template text", template.PlaceholderText);
        Assert.True(template.IsFavorite);
        Assert.Equal(5, template.HotkeySlot);
        Assert.NotNull(template.Tail);

        createTemplate.Undo(doc);
        Assert.Equal(initialCount, doc.BalloonTemplates.Count);
    }

    [Fact]
    public void ApplyBalloonTemplateCommand_ExecuteAndUndo_RestoresBalloon()
    {
        var doc = Document.Create("Test");
        var layerId = doc.BalloonLayers.First().Id;

        var sourceCreate = new CreateBalloonCommand(
            layerId,
            new Point2(220, 240),
            "Source",
            BalloonShape.Burst,
            BalloonStyle.Default.With(strokeWidth: 4f),
            TextStyle.Default.With(bold: true, fontSize: 18f));
        sourceCreate.Execute(doc);
        var source = doc.FindBalloon(sourceCreate.CreatedBalloonId)!;
        new CreateTailCommand(source.Id, new Point2(280, 350), TailStyle.Curved, baseWidth: 14f).Execute(doc);

        var targetCreate = new CreateBalloonCommand(
            layerId,
            new Point2(520, 250),
            "Keep me",
            BalloonShape.Oval,
            BalloonStyle.Default.With(strokeWidth: 1f),
            TextStyle.Default.With(italic: true, fontSize: 12f));
        targetCreate.Execute(doc);
        var target = doc.FindBalloon(targetCreate.CreatedBalloonId)!;
        new CreateTailCommand(target.Id, new Point2(560, 360), TailStyle.Pointer, baseWidth: 8f).Execute(doc);

        var createTemplate = new CreateBalloonTemplateCommand(source.Id, "Action Bubble");
        createTemplate.Execute(doc);

        var originalShape = target.Shape;
        var originalText = target.Text;
        var originalStrokeWidth = target.BalloonStyle.StrokeWidth;
        var originalTailTarget = target.Tail!.TargetPoint;

        var apply = new ApplyBalloonTemplateCommand(createTemplate.CreatedTemplateId, target.Id, applyPlaceholderText: false, replaceTail: true);
        apply.Execute(doc);

        target = doc.FindBalloon(target.Id)!;
        Assert.Equal(BalloonShape.Burst, target.Shape);
        Assert.Equal(originalText, target.Text);
        Assert.Equal(4f, target.BalloonStyle.StrokeWidth);
        Assert.NotNull(target.Tail);
        Assert.NotEqual(originalTailTarget, target.Tail!.TargetPoint);

        apply.Undo(doc);

        target = doc.FindBalloon(target.Id)!;
        Assert.Equal(originalShape, target.Shape);
        Assert.Equal(originalText, target.Text);
        Assert.Equal(originalStrokeWidth, target.BalloonStyle.StrokeWidth);
        Assert.Equal(originalTailTarget, target.Tail!.TargetPoint);
    }

    [Fact]
    public void CreateBalloonFromTemplateCommand_CreatesBalloon_AndUndoRemoves()
    {
        var doc = Document.Create("Test");
        var layerId = doc.BalloonLayers.First().Id;

        var namedBalloonStyle = new CreateNamedBalloonStyleCommand("Template Balloon", BalloonStyle.Default.With(strokeWidth: 3f));
        namedBalloonStyle.Execute(doc);
        var namedTextStyle = new CreateNamedTextStyleCommand("Template Text", TextStyle.Default.With(bold: true, fontSize: 20f));
        namedTextStyle.Execute(doc);

        var sourceCreate = new CreateBalloonCommand(layerId, new Point2(180, 260), "Placeholder");
        sourceCreate.Execute(doc);
        var source = doc.FindBalloon(sourceCreate.CreatedBalloonId)!;
        new SetBalloonStyleReferenceCommand(source.Id, namedBalloonStyle.CreatedStyleId).Execute(doc);
        new SetTextStyleReferenceCommand(source.Id, namedTextStyle.CreatedStyleId).Execute(doc);
        new CreateTailCommand(source.Id, new Point2(240, 350), TailStyle.Pointer, baseWidth: 16f).Execute(doc);

        var createTemplate = new CreateBalloonTemplateCommand(
            source.Id,
            "Linked Template",
            placeholderText: "Template text");
        createTemplate.Execute(doc);

        var targetPosition = new Point2(700, 400);
        var createFromTemplate = new CreateBalloonFromTemplateCommand(
            createTemplate.CreatedTemplateId,
            layerId,
            targetPosition);

        createFromTemplate.Execute(doc);

        var created = doc.FindBalloon(createFromTemplate.CreatedBalloonId);
        Assert.NotNull(created);
        Assert.Equal(BalloonShape.Oval, created!.Shape);
        Assert.Equal("Template text", created.Text);
        Assert.Equal(namedBalloonStyle.CreatedStyleId, created.BalloonStyleId);
        Assert.Equal(namedTextStyle.CreatedStyleId, created.TextStyleId);
        Assert.NotNull(created.Tail);

        var sourceOffset = source.Tail!.TargetPoint - source.Position;
        var createdOffset = created.Tail!.TargetPoint - created.Position;
        Assert.Equal(sourceOffset, createdOffset);

        createFromTemplate.Undo(doc);
        Assert.Null(doc.FindBalloon(createFromTemplate.CreatedBalloonId));
    }
}
