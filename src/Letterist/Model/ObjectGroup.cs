namespace Letterist.Model;

public sealed class ObjectGroup
{
    private readonly HashSet<Guid> _balloonIds = new();
    private readonly HashSet<Guid> _floatingImageIds = new();

    public Guid Id { get; }
    public IReadOnlyCollection<Guid> BalloonIds => _balloonIds;
    public IReadOnlyCollection<Guid> FloatingImageIds => _floatingImageIds;
    public int MemberCount => _balloonIds.Count + _floatingImageIds.Count;

    public ObjectGroup(Guid id, IEnumerable<Guid>? balloonIds = null, IEnumerable<Guid>? floatingImageIds = null)
    {
        Id = id;

        if (balloonIds != null)
        {
            foreach (var balloonId in balloonIds)
            {
                if (balloonId != Guid.Empty)
                {
                    _balloonIds.Add(balloonId);
                }
            }
        }

        if (floatingImageIds != null)
        {
            foreach (var imageId in floatingImageIds)
            {
                if (imageId != Guid.Empty)
                {
                    _floatingImageIds.Add(imageId);
                }
            }
        }
    }

    public static ObjectGroup Create(IEnumerable<Guid>? balloonIds = null, IEnumerable<Guid>? floatingImageIds = null)
    {
        return new ObjectGroup(Guid.NewGuid(), balloonIds, floatingImageIds);
    }

    public ObjectGroup Clone()
    {
        return new ObjectGroup(Id, _balloonIds, _floatingImageIds);
    }

    public bool ContainsBalloon(Guid balloonId) => _balloonIds.Contains(balloonId);
    public bool ContainsFloatingImage(Guid imageId) => _floatingImageIds.Contains(imageId);
}
