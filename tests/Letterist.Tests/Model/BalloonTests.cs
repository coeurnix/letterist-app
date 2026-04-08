using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Model;

public class BalloonTests
{
    private readonly Guid _layerId = Guid.NewGuid();

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        var b1 = Balloon.Create(_layerId, new Point2(100, 100));
        var b2 = Balloon.Create(_layerId, new Point2(100, 100));

        Assert.NotEqual(b1.Id, b2.Id);
    }

    [Fact]
    public void Create_SetsPosition()
    {
        var position = new Point2(150, 250);
        var balloon = Balloon.Create(_layerId, position);

        Assert.Equal(position, balloon.Position);
    }

    [Fact]
    public void Create_SetsLayerId()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100));

        Assert.Equal(_layerId, balloon.LayerId);
    }

    [Fact]
    public void Create_SetsTextContent()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100), "Hello World");

        Assert.Equal("Hello World", balloon.Text);
    }

    [Fact]
    public void Create_DefaultsToOvalShape()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100));

        Assert.Equal(BalloonShape.Oval, balloon.Shape);
    }

    [Fact]
    public void Create_WithCustomShape()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100), shape: BalloonShape.Rectangle);

        Assert.Equal(BalloonShape.Rectangle, balloon.Shape);
    }

    [Fact]
    public void Bounds_ComputedFromCenterAndSize()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100));
        balloon.SetComputedSize(new Size2(80, 60));

        var bounds = balloon.Bounds;

        Assert.Equal(60, bounds.X);  // 100 - 80/2
        Assert.Equal(70, bounds.Y);  // 100 - 60/2
        Assert.Equal(80, bounds.Width);
        Assert.Equal(60, bounds.Height);
        Assert.Equal(new Point2(100, 100), bounds.Center);
    }

    [Fact]
    public void Tail_InitiallyNull()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100));

        Assert.Null(balloon.Tail);
    }

    [Fact]
    public void SetTail_AddsTail()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100));
        var tail = Tail.Create(new Point2(100, 200));

        balloon.SetTail(tail);

        Assert.NotNull(balloon.Tail);
        Assert.Equal(new Point2(100, 200), balloon.Tail.TargetPoint);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100), "Test");
        balloon.SetTail(Tail.Create(new Point2(100, 200)));
        balloon.SetTextPath(TextPath.CreateDefault(new Size2(120, 80)).With(offset: 4f));

        var clone = balloon.Clone();

        Assert.Equal(balloon.Id, clone.Id);
        Assert.Equal(balloon.Position, clone.Position);
        Assert.Equal(balloon.Text, clone.Text);
        Assert.NotNull(clone.Tail);
        Assert.NotSame(balloon.Tail, clone.Tail);
        Assert.Equal(balloon.Tail!.Id, clone.Tail.Id);
        Assert.NotNull(clone.TextPath);
        Assert.Equal(4f, clone.TextPath!.Offset);
    }

    [Fact]
    public void CloneWithNewId_CreatesNewIds()
    {
        var balloon = Balloon.Create(_layerId, new Point2(100, 100), "Test");
        balloon.SetTail(Tail.Create(new Point2(100, 200)));

        var clone = balloon.CloneWithNewId();

        Assert.NotEqual(balloon.Id, clone.Id);
        Assert.NotEqual(balloon.Tail!.Id, clone.Tail!.Id);
        Assert.Equal(balloon.Position, clone.Position);
        Assert.Equal(balloon.Text, clone.Text);
    }

    [Fact]
    public void TextBounds_NoneShape_UsesFullRectTextArea()
    {
        var none = Balloon.Create(_layerId, new Point2(100, 100), "NO BALLOON", BalloonShape.None);
        var rectangle = Balloon.Create(_layerId, new Point2(100, 100), "RECT", BalloonShape.Rectangle);
        none.SetComputedSize(new Size2(180, 120));
        rectangle.SetComputedSize(new Size2(180, 120));

        Assert.Equal(rectangle.TextBounds, none.TextBounds);
    }
}
