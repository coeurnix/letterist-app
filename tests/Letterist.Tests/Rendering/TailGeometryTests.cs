using Letterist.Commands;
using Letterist.Model;
using Letterist.Rendering;
using Xunit;

namespace Letterist.Tests.Rendering;

public class TailGeometryTests
{
    [Fact]
    public void ToTailSpacePoint_RoundTripsRenderedPoint_ForRotatedBalloon()
    {
        var balloon = CreateBalloon(rotation: 37f);
        var source = new Point2(242f, 171f);

        var rendered = TailGeometry.ToRenderedPoint(balloon, source);
        var roundTripped = TailGeometry.ToTailSpacePoint(balloon, rendered);

        AssertClose(source, roundTripped);
    }

    [Fact]
    public void GetRenderedTargetPoint_AppliesBalloonRotation()
    {
        var balloon = CreateBalloon(rotation: 90f);
        var tail = Tail.Create(new Point2(balloon.Position.X + 30f, balloon.Position.Y));
        balloon.AddTail(tail);

        var renderedTarget = TailGeometry.GetRenderedTargetPoint(balloon, tail);
        var expected = BalloonGeometry.RotatePointAround(tail.TargetPoint, balloon.Position, MathF.PI / 2f);

        AssertClose(expected, renderedTarget);
    }

    [Fact]
    public void GetRenderedAttachmentPoint_AppliesBalloonRotation()
    {
        var balloon = CreateBalloon(rotation: -42f);
        var tail = Tail.Create(new Point2(balloon.Position.X + 30f, balloon.Position.Y + 10f));
        tail.SetAttachmentDirection(new Point2(1f, 0f));
        balloon.AddTail(tail);

        var unrotatedAttachment = TailGeometry.ComputeAttachmentPoint(balloon, tail);
        var renderedAttachment = TailGeometry.GetRenderedAttachmentPoint(balloon, tail);
        var expected = BalloonGeometry.RotatePointAround(unrotatedAttachment, balloon.Position, -42f * MathF.PI / 180f);

        AssertClose(expected, renderedAttachment);
    }

    private static Balloon CreateBalloon(float rotation)
    {
        var document = Document.Create("Tail geometry");
        var layerId = document.BalloonLayers.First().Id;

        var create = new CreateBalloonCommand(layerId, new Point2(200f, 200f), "Sample");
        create.Execute(document);

        var resize = new ResizeBalloonCommand(create.CreatedBalloonId, new Size2(120f, 80f), new Point2(200f, 200f));
        resize.Execute(document);

        var rotate = new RotateBalloonCommand(create.CreatedBalloonId, rotation);
        rotate.Execute(document);

        return document.FindBalloon(create.CreatedBalloonId)!;
    }

    private static void AssertClose(Point2 expected, Point2 actual, float epsilon = 0.001f)
    {
        Assert.InRange(actual.X, expected.X - epsilon, expected.X + epsilon);
        Assert.InRange(actual.Y, expected.Y - epsilon, expected.Y + epsilon);
    }
}
