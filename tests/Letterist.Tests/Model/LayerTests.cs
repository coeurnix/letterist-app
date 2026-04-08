using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Model;

public class LayerTests
{
    [Fact]
    public void Create_GeneratesUniqueId()
    {
        var layer1 = Layer.Create("Layer 1");
        var layer2 = Layer.Create("Layer 2");

        Assert.NotEqual(layer1.Id, layer2.Id);
    }

    [Fact]
    public void Create_SetsName()
    {
        var layer = Layer.Create("My Layer");

        Assert.Equal("My Layer", layer.Name);
    }

    [Fact]
    public void Create_DefaultsToVisibleAndUnlocked()
    {
        var layer = Layer.Create("Test");

        Assert.True(layer.IsVisible);
        Assert.False(layer.IsLocked);
        Assert.Equal(1.0f, layer.Opacity);
    }

    [Fact]
    public void Balloons_InitiallyEmpty()
    {
        var layer = Layer.Create("Test");

        Assert.Empty(layer.Balloons);
    }

    [Fact]
    public void AddBalloon_AddsToBalloonsList()
    {
        var layer = Layer.Create("Test");
        var balloon = Balloon.Create(layer.Id, new Point2(100, 100), "Hello");

        layer.AddBalloon(balloon);

        Assert.Single(layer.Balloons);
        Assert.Equal(balloon.Id, layer.Balloons[0].Id);
    }

    [Fact]
    public void AddTextOnlyBalloon_UsesNoneShape()
    {
        var layer = Layer.Create("Test");
        var textOnly = Balloon.Create(layer.Id, new Point2(90, 70), "BANG", BalloonShape.None);

        layer.AddBalloon(textOnly);

        Assert.Single(layer.Balloons);
        Assert.Equal(BalloonShape.None, layer.Balloons[0].Shape);
        Assert.Equal(textOnly.Id, layer.Balloons[0].Id);
    }

    [Fact]
    public void AddBalloon_ThrowsIfWrongLayerId()
    {
        var layer = Layer.Create("Test");
        var balloon = Balloon.Create(Guid.NewGuid(), new Point2(100, 100), "Hello");

        Assert.Throws<InvalidOperationException>(() => layer.AddBalloon(balloon));
    }

    [Fact]
    public void RemoveBalloon_RemovesFromList()
    {
        var layer = Layer.Create("Test");
        var balloon = Balloon.Create(layer.Id, new Point2(100, 100), "Hello");
        layer.AddBalloon(balloon);

        var removed = layer.RemoveBalloon(balloon.Id);

        Assert.True(removed);
        Assert.Empty(layer.Balloons);
    }

    [Fact]
    public void RemoveBalloon_ReturnsFalseIfNotFound()
    {
        var layer = Layer.Create("Test");

        var removed = layer.RemoveBalloon(Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public void FindBalloon_ReturnsCorrectBalloon()
    {
        var layer = Layer.Create("Test");
        var balloon = Balloon.Create(layer.Id, new Point2(100, 100), "Hello");
        layer.AddBalloon(balloon);

        var found = layer.FindBalloon(balloon.Id);

        Assert.NotNull(found);
        Assert.Equal(balloon.Id, found.Id);
    }

    [Fact]
    public void FindBalloon_ReturnsNullIfNotFound()
    {
        var layer = Layer.Create("Test");

        var found = layer.FindBalloon(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var layer = Layer.Create("Test");
        var balloon = Balloon.Create(layer.Id, new Point2(100, 100), "Hello");
        layer.AddBalloon(balloon);

        var clone = layer.Clone();

        Assert.Equal(layer.Id, clone.Id);
        Assert.Equal(layer.Name, clone.Name);
        Assert.Single(clone.Balloons);
        Assert.NotSame(layer.Balloons[0], clone.Balloons[0]);
        Assert.Equal(layer.Balloons[0].Id, clone.Balloons[0].Id);
    }
}
