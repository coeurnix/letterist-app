using Letterist.Model;

namespace Letterist.Commands;

public sealed class LinkBalloonsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "LinkBalloons";
    public string Description => "Link balloons";

    private readonly Guid _balloonAId;
    private readonly Guid _balloonBId;
    private bool _linkAdded;

    public LinkBalloonsCommand(Guid balloonAId, Guid balloonBId)
    {
        Id = Guid.NewGuid();
        _balloonAId = balloonAId;
        _balloonBId = balloonBId;
    }

    public void Execute(Document document)
    {
        var page = document.ActivePage ?? throw new InvalidOperationException("No active page");
        _linkAdded = page.AddBalloonLink(_balloonAId, _balloonBId);
    }

    public void Undo(Document document)
    {
        if (!_linkAdded) return;
        var page = document.ActivePage ?? throw new InvalidOperationException("No active page");
        page.RemoveBalloonLink(_balloonAId, _balloonBId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonAId"] = _balloonAId,
                ["balloonBId"] = _balloonBId
            }
        };
    }
}

public sealed class UnlinkBalloonsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UnlinkBalloons";
    public string Description => "Unlink balloons";

    private readonly Guid _balloonAId;
    private readonly Guid _balloonBId;
    private bool _linkRemoved;

    public UnlinkBalloonsCommand(Guid balloonAId, Guid balloonBId)
    {
        Id = Guid.NewGuid();
        _balloonAId = balloonAId;
        _balloonBId = balloonBId;
    }

    public void Execute(Document document)
    {
        var page = document.ActivePage ?? throw new InvalidOperationException("No active page");
        _linkRemoved = page.RemoveBalloonLink(_balloonAId, _balloonBId);
    }

    public void Undo(Document document)
    {
        if (!_linkRemoved) return;
        var page = document.ActivePage ?? throw new InvalidOperationException("No active page");
        page.AddBalloonLink(_balloonAId, _balloonBId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonAId"] = _balloonAId,
                ["balloonBId"] = _balloonBId
            }
        };
    }
}

public sealed class SetBalloonLinkStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonLinkStyle";
    public string Description => "Set balloon link style";

    private readonly Guid _pageId;
    private readonly BalloonLinkStyle _newStyle;
    private BalloonLinkStyle? _oldStyle;

    public SetBalloonLinkStyleCommand(Guid pageId, BalloonLinkStyle newStyle)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        _oldStyle = page.BalloonLinkStyle;
        page.SetBalloonLinkStyle(_newStyle);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        page.SetBalloonLinkStyle(_oldStyle ?? BalloonLinkStyle.Default);
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
                ["strokeR"] = _newStyle.StrokeColor.R,
                ["strokeG"] = _newStyle.StrokeColor.G,
                ["strokeB"] = _newStyle.StrokeColor.B,
                ["strokeA"] = _newStyle.StrokeColor.A,
                ["fillR"] = _newStyle.FillColor.R,
                ["fillG"] = _newStyle.FillColor.G,
                ["fillB"] = _newStyle.FillColor.B,
                ["fillA"] = _newStyle.FillColor.A,
                ["strokeWidth"] = _newStyle.StrokeWidth,
                ["connectorWidth"] = _newStyle.ConnectorWidth,
                ["dashStyle"] = _newStyle.DashStyle.ToString()
            }
        };
    }
}

public sealed class ClearBalloonLinksCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ClearBalloonLinks";
    public string Description => "Clear balloon links";

    private readonly Guid _pageId;
    private List<BalloonLink> _removed = new();

    public ClearBalloonLinksCommand(Guid pageId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        _removed = page.ClearBalloonLinks();
    }

    public void Undo(Document document)
    {
        if (_removed.Count == 0) return;
        var page = document.FindPage(_pageId) ?? throw new InvalidOperationException("Page not found");
        page.AddBalloonLinks(_removed);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["pageId"] = _pageId
            }
        };
    }
}
