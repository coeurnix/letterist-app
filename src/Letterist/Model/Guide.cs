namespace Letterist.Model;

public enum GuideOrientation
{
    Horizontal,
    Vertical
}

public sealed class Guide
{
    public Guid Id { get; }
    public GuideOrientation Orientation { get; private set; }
    public float Position { get; private set; }

    public Guide(Guid id, GuideOrientation orientation, float position)
    {
        Id = id;
        Orientation = orientation;
        Position = position;
    }

    public static Guide Create(GuideOrientation orientation, float position)
    {
        return new Guide(Guid.NewGuid(), orientation, position);
    }

    public Guide Clone() => new(Id, Orientation, Position);

    internal void SetPosition(float position) => Position = position;
}
