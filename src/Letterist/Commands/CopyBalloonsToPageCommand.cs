using Letterist.Model;
using System.Collections.Generic;
using System.Linq;

namespace Letterist.Commands;

public sealed class CopyBalloonsToPageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CopyBalloonsToPage";
    public string Description => "Copy balloons to page";

    private readonly Guid _sourcePageId;
    private readonly Guid _targetPageId;
    private readonly List<Guid> _balloonIds;
    private readonly List<Balloon> _templates = new();
    private readonly List<BalloonLink> _createdLinks = new();
    private readonly Dictionary<Guid, Guid> _idMap = new();
    private Guid _targetLayerId;
    private bool _initialized;

    public CopyBalloonsToPageCommand(Guid sourcePageId, Guid targetPageId, IEnumerable<Guid> balloonIds)
    {
        Id = Guid.NewGuid();
        _sourcePageId = sourcePageId;
        _targetPageId = targetPageId;
        _balloonIds = balloonIds?.ToList() ?? new List<Guid>();
    }

    public void Execute(Document document)
    {
        var sourcePage = document.FindPage(_sourcePageId)
            ?? throw new InvalidOperationException($"Source page {_sourcePageId} not found");
        var targetPage = document.FindPage(_targetPageId)
            ?? throw new InvalidOperationException($"Target page {_targetPageId} not found");

        var targetLayer = ResolveTargetLayer(targetPage);

        if (!_initialized)
        {
            _targetLayerId = targetLayer.Id;
            BuildTemplates(sourcePage, _targetLayerId);
            BuildLinks(sourcePage);
            _initialized = true;
        }

        var layer = targetPage.FindLayer(_targetLayerId) ?? targetLayer;
        foreach (var template in _templates)
        {
            var clone = template.Clone();
            clone.SetLayerId(layer.Id);
            layer.AddBalloon(clone);
        }

        if (_createdLinks.Count > 0)
        {
            targetPage.AddBalloonLinks(_createdLinks);
        }
    }

    public void Undo(Document document)
    {
        var targetPage = document.FindPage(_targetPageId)
            ?? throw new InvalidOperationException($"Target page {_targetPageId} not found");
        var layer = targetPage.FindLayer(_targetLayerId)
            ?? throw new InvalidOperationException($"Target layer {_targetLayerId} not found");

        foreach (var template in _templates)
        {
            layer.RemoveBalloon(template.Id);
        }

        if (_createdLinks.Count > 0)
        {
            foreach (var link in _createdLinks)
            {
                targetPage.RemoveBalloonLink(link.FirstId, link.SecondId);
            }
        }
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["sourcePageId"] = _sourcePageId,
                ["targetPageId"] = _targetPageId,
                ["balloonIds"] = _balloonIds.ToArray()
            }
        };
    }

    private void BuildTemplates(Page sourcePage, Guid targetLayerId)
    {
        foreach (var balloonId in _balloonIds)
        {
            var balloon = sourcePage.FindBalloon(balloonId);
            if (balloon == null) continue;

            var clone = balloon.CloneWithNewId();
            clone.SetLayerId(targetLayerId);
            _templates.Add(clone);
            _idMap[balloonId] = clone.Id;
        }
    }

    private void BuildLinks(Page sourcePage)
    {
        if (_idMap.Count < 2) return;

        foreach (var link in sourcePage.BalloonLinks)
        {
            if (_idMap.TryGetValue(link.FirstId, out var newA) &&
                _idMap.TryGetValue(link.SecondId, out var newB))
            {
                _createdLinks.Add(new BalloonLink(newA, newB));
            }
        }
    }

    private static Layer ResolveTargetLayer(Page page)
    {
        var active = page.FindLayer(page.ActiveLayerId);
        if (active != null && active.Kind == LayerKind.Balloon)
        {
            return active;
        }

        return page.GetLastBalloonLayer()
            ?? page.GetFirstBalloonLayer()
            ?? throw new InvalidOperationException("No balloon layer available.");
    }
}
