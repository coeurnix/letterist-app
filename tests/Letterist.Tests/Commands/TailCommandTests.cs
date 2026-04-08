using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class TailCommandTests
{
    private static Layer GetFirstBalloonLayer(Document doc)
    {
        return doc.BalloonLayers.First();
    }

    private (Document doc, Guid balloonId) CreateDocumentWithBalloon()
    {
        var doc = Document.Create("Test");
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createCmd = new CreateBalloonCommand(layerId, new Point2(100, 100), "Test");
        createCmd.Execute(doc);
        return (doc, createCmd.CreatedBalloonId);
    }

    [Fact]
    public void CreateTailCommand_AddsTailToBalloon()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var targetPoint = new Point2(100, 200);

        var cmd = new CreateTailCommand(balloonId, targetPoint);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon?.Tail);
        Assert.Equal(targetPoint, balloon.Tail.TargetPoint);
        Assert.Equal(TailStyle.Pointer, balloon.Tail.Style);
    }

    [Fact]
    public void CreateTailCommand_WithCustomStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.ThoughtBubbles);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.ThoughtBubbles, balloon!.Tail!.Style);
    }

    [Fact]
    public void CreateTailCommand_Undo_RemovesTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd.Execute(doc);
        cmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Null(balloon?.Tail);
    }

    [Fact]
    public void CreateTailCommand_AllowsMultipleTails()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd1.Execute(doc);

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300));
        cmd2.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(2, balloon!.Tails.Count);
        Assert.Equal(new Point2(100, 200), balloon.Tails[0].TargetPoint);
        Assert.Equal(new Point2(100, 300), balloon.Tails[1].TargetPoint);
    }

    [Fact]
    public void CreateTailCommand_Undo_RemovesSpecificTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd1.Execute(doc);

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300));
        cmd2.Execute(doc);

        cmd2.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Single(balloon!.Tails);
        Assert.Equal(new Point2(100, 200), balloon.Tails[0].TargetPoint);
    }

    [Fact]
    public void MoveTailTargetCommand_ChangesTargetPoint()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200));
        createTailCmd.Execute(doc);

        var moveCmd = new MoveTailTargetCommand(balloonId, new Point2(150, 250));
        moveCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(new Point2(150, 250), balloon!.Tail!.TargetPoint);
    }

    [Fact]
    public void MoveTailTargetCommand_Undo_RestoresTargetPoint()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var originalTarget = new Point2(100, 200);
        var createTailCmd = new CreateTailCommand(balloonId, originalTarget);
        createTailCmd.Execute(doc);

        var moveCmd = new MoveTailTargetCommand(balloonId, new Point2(150, 250));
        moveCmd.Execute(doc);
        moveCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(originalTarget, balloon!.Tail!.TargetPoint);
    }

    [Fact]
    public void MoveTailTargetCommand_Throws_IfNoTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var moveCmd = new MoveTailTargetCommand(balloonId, new Point2(150, 250));

        Assert.Throws<InvalidOperationException>(() => moveCmd.Execute(doc));
    }

    [Fact]
    public void DeleteTailCommand_RemovesTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200));
        createTailCmd.Execute(doc);

        var deleteCmd = new DeleteTailCommand(balloonId);
        deleteCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Null(balloon?.Tail);
    }

    [Fact]
    public void DeleteTailCommand_Undo_RestoresTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var targetPoint = new Point2(100, 200);
        var createTailCmd = new CreateTailCommand(balloonId, targetPoint, TailStyle.Curved, 20f);
        createTailCmd.Execute(doc);

        var deleteCmd = new DeleteTailCommand(balloonId);
        deleteCmd.Execute(doc);
        deleteCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon?.Tail);
        Assert.Equal(targetPoint, balloon.Tail.TargetPoint);
        Assert.Equal(TailStyle.Curved, balloon.Tail.Style);
        Assert.Equal(20f, balloon.Tail.BaseWidth);
    }

    [Fact]
    public void SetTailStyleCommand_ChangesStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        createTailCmd.Execute(doc);

        var styleCmd = new SetTailStyleCommand(balloonId, TailStyle.Squiggly);
        styleCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.Squiggly, balloon!.Tail!.Style);
    }

    [Fact]
    public void SetTailStyleCommand_Undo_RestoresStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        createTailCmd.Execute(doc);

        var styleCmd = new SetTailStyleCommand(balloonId, TailStyle.ThoughtBubbles);
        styleCmd.Execute(doc);
        styleCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.Pointer, balloon!.Tail!.Style);
    }

    [Fact]
    public void CreateTailCommand_WithCurvedStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var targetPoint = new Point2(100, 200);

        var cmd = new CreateTailCommand(balloonId, targetPoint, TailStyle.Curved);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon?.Tail);
        Assert.Equal(TailStyle.Curved, balloon.Tail.Style);
        Assert.Equal(targetPoint, balloon.Tail.TargetPoint);
    }

    [Fact]
    public void SetTailStyleCommand_ChangesToCurved()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        createTailCmd.Execute(doc);

        var styleCmd = new SetTailStyleCommand(balloonId, TailStyle.Curved);
        styleCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.Curved, balloon!.Tail!.Style);
    }

    [Fact]
    public void MoveTailTargetCommand_WithTailId_MovesSpecificTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd1.Execute(doc);
        var tail1Id = cmd1.CreatedTailId;

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300));
        cmd2.Execute(doc);
        var tail2Id = cmd2.CreatedTailId;

        var moveCmd = new MoveTailTargetCommand(balloonId, new Point2(150, 350), tail2Id);
        moveCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(new Point2(100, 200), balloon!.Tails[0].TargetPoint); // First unchanged
        Assert.Equal(new Point2(150, 350), balloon.Tails[1].TargetPoint);  // Second moved
    }

    [Fact]
    public void DeleteTailCommand_WithTailId_DeletesSpecificTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd1.Execute(doc);
        var tail1Id = cmd1.CreatedTailId;

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300));
        cmd2.Execute(doc);
        var tail2Id = cmd2.CreatedTailId;

        var deleteCmd = new DeleteTailCommand(balloonId, tail1Id);
        deleteCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Single(balloon!.Tails);
        Assert.Equal(new Point2(100, 300), balloon.Tails[0].TargetPoint); // Second tail remains
    }

    [Fact]
    public void SetTailStyleCommand_WithTailId_ChangesSpecificTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        cmd1.Execute(doc);
        var tail1Id = cmd1.CreatedTailId;

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300), TailStyle.Pointer);
        cmd2.Execute(doc);
        var tail2Id = cmd2.CreatedTailId;

        var styleCmd = new SetTailStyleCommand(balloonId, TailStyle.Curved, tail2Id);
        styleCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.Pointer, balloon!.Tails[0].Style); // First unchanged
        Assert.Equal(TailStyle.Curved, balloon.Tails[1].Style);   // Second changed
    }

    [Fact]
    public void Balloon_FindTail_ReturnsCorrectTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200));
        cmd1.Execute(doc);
        var tail1Id = cmd1.CreatedTailId;

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300));
        cmd2.Execute(doc);
        var tail2Id = cmd2.CreatedTailId;

        var balloon = doc.FindBalloon(balloonId)!;
        Assert.Equal(tail1Id, balloon.FindTail(tail1Id)?.Id);
        Assert.Equal(tail2Id, balloon.FindTail(tail2Id)?.Id);
        Assert.Null(balloon.FindTail(Guid.NewGuid())); // Non-existent tail
    }

    [Fact]
    public void Balloon_Clone_ClonesAllTails()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        cmd1.Execute(doc);

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300), TailStyle.Curved);
        cmd2.Execute(doc);

        var balloon = doc.FindBalloon(balloonId)!;
        var clone = balloon.Clone();

        Assert.Equal(2, clone.Tails.Count);
        Assert.Equal(balloon.Tails[0].Id, clone.Tails[0].Id);
        Assert.Equal(balloon.Tails[1].Id, clone.Tails[1].Id);
        Assert.Equal(TailStyle.Pointer, clone.Tails[0].Style);
        Assert.Equal(TailStyle.Curved, clone.Tails[1].Style);
    }

    [Fact]
    public void CreateTailCommand_WithSquigglyStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var targetPoint = new Point2(100, 200);

        var cmd = new CreateTailCommand(balloonId, targetPoint, TailStyle.Squiggly);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.NotNull(balloon?.Tail);
        Assert.Equal(TailStyle.Squiggly, balloon.Tail.Style);
        Assert.Equal(targetPoint, balloon.Tail.TargetPoint);
    }

    [Fact]
    public void SetTailStyleCommand_ChangesToSquiggly()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer);
        createTailCmd.Execute(doc);

        var styleCmd = new SetTailStyleCommand(balloonId, TailStyle.Squiggly);
        styleCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(TailStyle.Squiggly, balloon!.Tail!.Style);
    }

    [Fact]
    public void SetTailWidthCommand_ChangesWidth()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer, 16f);
        createTailCmd.Execute(doc);

        var widthCmd = new SetTailWidthCommand(balloonId, 24f);
        widthCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(24f, balloon!.Tail!.BaseWidth);
    }

    [Fact]
    public void SetTailWidthCommand_Undo_RestoresWidth()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer, 16f);
        createTailCmd.Execute(doc);

        var widthCmd = new SetTailWidthCommand(balloonId, 24f);
        widthCmd.Execute(doc);
        widthCmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(16f, balloon!.Tail!.BaseWidth);
    }

    [Fact]
    public void SetTailWidthCommand_Undo_DoesNotThrow_WhenBalloonWasDeleted()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer, 16f).Execute(doc);

        var widthCmd = new SetTailWidthCommand(balloonId, 24f);
        widthCmd.Execute(doc);
        new DeleteBalloonCommand(balloonId).Execute(doc);

        var exception = Record.Exception(() => widthCmd.Undo(doc));
        Assert.Null(exception);
    }

    [Fact]
    public void SetTailWidthCommand_WithTailId_ChangesSpecificTail()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();

        var cmd1 = new CreateTailCommand(balloonId, new Point2(100, 200), TailStyle.Pointer, 16f);
        cmd1.Execute(doc);

        var cmd2 = new CreateTailCommand(balloonId, new Point2(100, 300), TailStyle.Pointer, 16f);
        cmd2.Execute(doc);
        var tail2Id = cmd2.CreatedTailId;

        var widthCmd = new SetTailWidthCommand(balloonId, 32f, tail2Id);
        widthCmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(16f, balloon!.Tails[0].BaseWidth); // First unchanged
        Assert.Equal(32f, balloon.Tails[1].BaseWidth);  // Second changed
    }

    [Fact]
    public void SetTailAttachmentDirectionCommand_SetsDirection()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200));
        createTailCmd.Execute(doc);

        var direction = new Point2(0, -1);
        var cmd = new SetTailAttachmentDirectionCommand(balloonId, direction);
        cmd.Execute(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Equal(direction, balloon!.Tail!.AttachmentDirection);
    }

    [Fact]
    public void SetTailAttachmentDirectionCommand_Undo_RestoresDirection()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var createTailCmd = new CreateTailCommand(balloonId, new Point2(100, 200));
        createTailCmd.Execute(doc);

        var cmd = new SetTailAttachmentDirectionCommand(balloonId, new Point2(1, 0));
        cmd.Execute(doc);
        cmd.Undo(doc);

        var balloon = doc.FindBalloon(balloonId);
        Assert.Null(balloon!.Tail!.AttachmentDirection);
    }

    private Document CreateTestDocument()
    {
        return Document.Create("Test");
    }

    [Fact]
    public void SetTailCurvatureCommand_ChangesCurvature()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        var createTailCmd = new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved);
        createTailCmd.Execute(doc);

        var setCurvatureCmd = new SetTailCurvatureCommand(balloonId, 0.8f);
        setCurvatureCmd.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.NotNull(tail);
        Assert.Equal(0.8f, tail.Curvature);
    }

    [Fact]
    public void SetTailCurvatureCommand_Undo_RestoresCurvature()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        var createTailCmd = new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved);
        createTailCmd.Execute(doc);

        var originalCurvature = doc.FindBalloon(balloonId)!.Tail!.Curvature;

        var setCurvatureCmd = new SetTailCurvatureCommand(balloonId, -0.5f);
        setCurvatureCmd.Execute(doc);
        setCurvatureCmd.Undo(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.Equal(originalCurvature, tail!.Curvature);
    }

    [Fact]
    public void SetTailCurvatureCommand_ClampsCurvature()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        var createTailCmd = new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved);
        createTailCmd.Execute(doc);

        var setCurvatureCmd = new SetTailCurvatureCommand(balloonId, 2.5f);
        setCurvatureCmd.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.Equal(2f, tail!.Curvature);
    }

    [Fact]
    public void SetTailCurveCenterCommand_ChangesCurveCenter()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved).Execute(doc);

        var cmd = new SetTailCurveCenterCommand(balloonId, 0.8f);
        cmd.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.Equal(0.8f, tail!.CurveCenter);
    }

    [Fact]
    public void SetTailInsetCommand_ChangesInset()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved).Execute(doc);

        var cmd = new SetTailInsetCommand(balloonId, 18f);
        cmd.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.Equal(18f, tail!.Inset);
    }

    [Fact]
    public void SetTailControlPointCommand_SetsControlPoint()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        var createTailCmd = new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved);
        createTailCmd.Execute(doc);

        var controlPoint = new Point2(75, 150);
        var setControlPointCmd = new SetTailControlPointCommand(balloonId, controlPoint);
        setControlPointCmd.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.NotNull(tail);
        Assert.Equal(controlPoint, tail.ControlPoint);
    }

    [Fact]
    public void SetTailControlPointCommand_ClearsControlPoint()
    {
        var doc = CreateTestDocument();
        var layerId = GetFirstBalloonLayer(doc).Id;
        var createBalloonCmd = new CreateBalloonCommand(layerId, new Point2(100, 100));
        createBalloonCmd.Execute(doc);
        var balloonId = createBalloonCmd.CreatedBalloonId;

        var createTailCmd = new CreateTailCommand(balloonId, new Point2(50, 200), TailStyle.Curved);
        createTailCmd.Execute(doc);

        var setControlPointCmd1 = new SetTailControlPointCommand(balloonId, new Point2(75, 150));
        setControlPointCmd1.Execute(doc);
        Assert.NotNull(doc.FindBalloon(balloonId)!.Tail!.ControlPoint);

        var setControlPointCmd2 = new SetTailControlPointCommand(balloonId, null);
        setControlPointCmd2.Execute(doc);

        var tail = doc.FindBalloon(balloonId)!.Tail;
        Assert.Null(tail!.ControlPoint);
    }

    [Fact]
    public void Tail_Clone_PreservesCurvatureAndControlPoint()
    {
        var tail = Tail.Create(new Point2(50, 200), TailStyle.Curved);
        tail.SetCurvature(-0.7f);
        tail.SetControlPoint(new Point2(60, 150));
        tail.SetCurveCenter(0.75f);
        tail.SetInset(12f);

        var clone = tail.Clone();

        Assert.Equal(-0.7f, clone.Curvature);
        Assert.Equal(new Point2(60, 150), clone.ControlPoint);
        Assert.Equal(0.75f, clone.CurveCenter);
        Assert.Equal(12f, clone.Inset);
    }

    [Fact]
    public void SetBalloonTailsFromTemplatesCommand_PreservesPlacement_AppliesStyle()
    {
        var (doc, balloonId) = CreateDocumentWithBalloon();
        var balloon = doc.FindBalloon(balloonId)!;

        var originalTail = Tail.Create(new Point2(130, 260), TailStyle.Pointer, 9f);
        originalTail.SetInset(17f);
        originalTail.SetCurveCenter(0.78f);
        originalTail.SetControlPoint(new Point2(140f, 210f));
        originalTail.SetAttachmentDirection(new Point2(-0.4f, 0.9f));
        balloon.SetTail(originalTail);

        var templates = new[]
        {
            new BalloonTemplateTail(
                targetOffset: new Point2(0f, 120f),
                style: TailStyle.Curved,
                baseWidth: 21f,
                attachmentDirection: new Point2(0f, 1f),
                controlPointOffset: new Point2(18f, 44f),
                curvature: -0.65f,
                curveCenter: 0.25f,
                inset: 3f)
        };

        var command = new SetBalloonTailsFromTemplatesCommand(balloonId, templates, preservePlacement: true);
        command.Execute(doc);

        Assert.Single(balloon.Tails);
        Assert.Equal(TailStyle.Curved, balloon.Tails[0].Style);
        Assert.Equal(21f, balloon.Tails[0].BaseWidth);
        Assert.Equal(-0.65f, balloon.Tails[0].Curvature);
        Assert.Equal(new Point2(130, 260), balloon.Tails[0].TargetPoint);
        Assert.Equal(17f, balloon.Tails[0].Inset);
        Assert.Equal(0.78f, balloon.Tails[0].CurveCenter);
        Assert.Equal(new Point2(140f, 210f), balloon.Tails[0].ControlPoint);
        Assert.Equal(new Point2(-0.4f, 0.9f), balloon.Tails[0].AttachmentDirection);

        command.Undo(doc);
        Assert.Single(balloon.Tails);
        Assert.Equal(TailStyle.Pointer, balloon.Tails[0].Style);
        Assert.Equal(new Point2(130, 260), balloon.Tails[0].TargetPoint);
        Assert.Equal(17f, balloon.Tails[0].Inset);
    }
}
