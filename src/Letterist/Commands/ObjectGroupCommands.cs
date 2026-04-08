using Letterist.Model;

namespace Letterist.Commands;

public sealed class GroupObjectsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "GroupObjects";
    public string Description => "Group objects";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly HashSet<Guid> _balloonIds;
    private readonly HashSet<Guid> _floatingImageIds;
    private List<ObjectGroup> _oldGroups = new();

    public GroupObjectsCommand(Guid pageId, IEnumerable<Guid>? balloonIds, IEnumerable<Guid>? floatingImageIds, Guid? groupId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId ?? Guid.NewGuid();
        _balloonIds = balloonIds != null ? new HashSet<Guid>(balloonIds.Where(id => id != Guid.Empty)) : new HashSet<Guid>();
        _floatingImageIds = floatingImageIds != null ? new HashSet<Guid>(floatingImageIds.Where(id => id != Guid.Empty)) : new HashSet<Guid>();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldGroups = page.ObjectGroups.Select(group => group.Clone()).ToList();
        var updatedGroups = _oldGroups.Select(group => group.Clone()).ToList();

        var balloons = new HashSet<Guid>(_balloonIds.Where(id => page.FindBalloon(id) != null));
        var images = new HashSet<Guid>(_floatingImageIds.Where(id => page.FindFloatingImage(id) != null));

        for (var i = updatedGroups.Count - 1; i >= 0; i--)
        {
            var group = updatedGroups[i];
            if (!group.BalloonIds.Any(balloons.Contains) &&
                !group.FloatingImageIds.Any(images.Contains))
            {
                continue;
            }

            balloons.UnionWith(group.BalloonIds);
            images.UnionWith(group.FloatingImageIds);
            updatedGroups.RemoveAt(i);
        }

        if (balloons.Count + images.Count < 2)
        {
            return;
        }

        updatedGroups.Add(new ObjectGroup(_groupId, balloons, images));
        page.SetObjectGroups(updatedGroups);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        page.SetObjectGroups(_oldGroups);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["pageId"] = _pageId,
                ["groupId"] = _groupId,
                ["balloonIds"] = _balloonIds.ToList(),
                ["floatingImageIds"] = _floatingImageIds.ToList()
            }
        };
    }
}

public sealed class UngroupObjectsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UngroupObjects";
    public string Description => "Ungroup objects";

    private readonly Guid _pageId;
    private readonly HashSet<Guid> _balloonIds;
    private readonly HashSet<Guid> _floatingImageIds;
    private List<ObjectGroup> _oldGroups = new();

    public UngroupObjectsCommand(Guid pageId, IEnumerable<Guid>? balloonIds, IEnumerable<Guid>? floatingImageIds)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _balloonIds = balloonIds != null ? new HashSet<Guid>(balloonIds.Where(id => id != Guid.Empty)) : new HashSet<Guid>();
        _floatingImageIds = floatingImageIds != null ? new HashSet<Guid>(floatingImageIds.Where(id => id != Guid.Empty)) : new HashSet<Guid>();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldGroups = page.ObjectGroups.Select(group => group.Clone()).ToList();
        var updatedGroups = new List<ObjectGroup>();

        foreach (var group in _oldGroups)
        {
            var balloons = group.BalloonIds.Where(id => !_balloonIds.Contains(id)).ToList();
            var images = group.FloatingImageIds.Where(id => !_floatingImageIds.Contains(id)).ToList();
            if (balloons.Count + images.Count < 2)
            {
                continue;
            }

            updatedGroups.Add(new ObjectGroup(group.Id, balloons, images));
        }

        page.SetObjectGroups(updatedGroups);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        page.SetObjectGroups(_oldGroups);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["pageId"] = _pageId,
                ["balloonIds"] = _balloonIds.ToList(),
                ["floatingImageIds"] = _floatingImageIds.ToList()
            }
        };
    }
}
