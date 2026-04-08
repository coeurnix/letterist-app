using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateGuideCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateGuide";
    public string Description => "Create guide";

    private readonly Guid _pageId;
    private readonly Guid _guideId;
    private readonly GuideOrientation _orientation;
    private readonly float _position;

    public Guid CreatedGuideId => _guideId;

    public CreateGuideCommand(Guid pageId, GuideOrientation orientation, float position, Guid? guideId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _guideId = guideId ?? Guid.NewGuid();
        _orientation = orientation;
        _position = position;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var guide = new Guide(_guideId, _orientation, _position);
        page.AddGuide(guide);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.RemoveGuide(_guideId);
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
                ["guideId"] = _guideId,
                ["orientation"] = _orientation.ToString(),
                ["position"] = _position
            }
        };
    }
}

public sealed class MoveGuideCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MoveGuide";
    public string Description => "Move guide";

    private readonly Guid _pageId;
    private readonly Guid _guideId;
    private readonly float _newPosition;
    private float _oldPosition;

    public MoveGuideCommand(Guid pageId, Guid guideId, float newPosition)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _guideId = guideId;
        _newPosition = newPosition;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var guide = page.FindGuide(_guideId)
            ?? throw new InvalidOperationException($"Guide {_guideId} not found");

        _oldPosition = guide.Position;
        guide.SetPosition(_newPosition);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var guide = page.FindGuide(_guideId)
            ?? throw new InvalidOperationException($"Guide {_guideId} not found");

        guide.SetPosition(_oldPosition);
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
                ["guideId"] = _guideId,
                ["position"] = _newPosition
            }
        };
    }
}

public sealed class DeleteGuideCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteGuide";
    public string Description => "Delete guide";

    private readonly Guid _pageId;
    private readonly Guid _guideId;
    private Guide? _deletedGuide;
    private int _guideIndex;

    public DeleteGuideCommand(Guid pageId, Guid guideId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _guideId = guideId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _deletedGuide = page.FindGuide(_guideId)?.Clone();
        _guideIndex = page.IndexOfGuide(_guideId);

        if (_deletedGuide == null)
        {
            throw new InvalidOperationException($"Guide {_guideId} not found");
        }

        page.RemoveGuide(_guideId);
    }

    public void Undo(Document document)
    {
        if (_deletedGuide == null)
        {
            throw new InvalidOperationException("Cannot undo - no guide was deleted");
        }

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.InsertGuide(_guideIndex, _deletedGuide.Clone());
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
                ["guideId"] = _guideId
            }
        };
    }
}

public sealed class SetGuidesLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetGuidesLocked";
    public string Description => _locked ? "Lock guides" : "Unlock guides";

    private readonly Guid _pageId;
    private readonly bool _locked;
    private bool _previousLocked;

    public SetGuidesLockedCommand(Guid pageId, bool locked)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _locked = locked;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _previousLocked = page.GuidesLocked;
        page.SetGuidesLocked(_locked);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetGuidesLocked(_previousLocked);
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
                ["locked"] = _locked
            }
        };
    }
}
