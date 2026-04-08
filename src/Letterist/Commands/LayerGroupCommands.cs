using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateLayerGroupCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateLayerGroup";
    public string Description => "Create layer group";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly string _name;
    private int _insertedIndex;

    public Guid CreatedGroupId => _groupId;

    public CreateLayerGroupCommand(Guid pageId, string name, Guid? groupId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _name = name;
        _groupId = groupId ?? Guid.NewGuid();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = new LayerGroup(_groupId, _name);
        page.AddLayerGroup(group);
        _insertedIndex = page.IndexOfLayerGroup(_groupId);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.RemoveLayerGroup(_groupId);
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
                ["name"] = _name
            }
        };
    }
}

public sealed class DeleteLayerGroupCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteLayerGroup";
    public string Description => "Delete layer group";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private LayerGroup? _deletedGroup;
    private int _index;
    private List<Guid>? _layerIdsInGroup;

    public DeleteLayerGroupCommand(Guid pageId, Guid groupId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _deletedGroup = page.FindLayerGroup(_groupId)?.Clone();
        _index = page.IndexOfLayerGroup(_groupId);
        _layerIdsInGroup = page.GetLayersInGroup(_groupId).Select(l => l.Id).ToList();

        if (_deletedGroup == null)
        {
            throw new InvalidOperationException($"Layer group {_groupId} not found");
        }

        page.RemoveLayerGroup(_groupId);
    }

    public void Undo(Document document)
    {
        if (_deletedGroup == null)
        {
            throw new InvalidOperationException("Cannot undo - no group was deleted");
        }

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.InsertLayerGroup(_index, _deletedGroup.Clone());

        if (_layerIdsInGroup != null)
        {
            foreach (var layerId in _layerIdsInGroup)
            {
                var layer = page.FindLayer(layerId);
                layer?.SetGroupId(_groupId);
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
                ["pageId"] = _pageId,
                ["groupId"] = _groupId
            }
        };
    }
}

public sealed class RenameLayerGroupCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenameLayerGroup";
    public string Description => "Rename layer group";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly string _newName;
    private string? _oldName;

    public RenameLayerGroupCommand(Guid pageId, Guid groupId, string newName)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId;
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId)
            ?? throw new InvalidOperationException($"Layer group {_groupId} not found");

        _oldName = group.Name;
        group.SetName(_newName);
    }

    public void Undo(Document document)
    {
        if (_oldName == null) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId);
        group?.SetName(_oldName);
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
                ["name"] = _newName
            }
        };
    }
}

public sealed class AddLayerToGroupCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "AddLayerToGroup";
    public string Description => "Add layer to group";

    private readonly Guid _pageId;
    private readonly Guid _layerId;
    private readonly Guid _groupId;
    private Guid? _previousGroupId;

    public AddLayerToGroupCommand(Guid pageId, Guid layerId, Guid groupId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _layerId = layerId;
        _groupId = groupId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var layer = page.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        if (layer.Kind == LayerKind.Image)
        {
            throw new InvalidOperationException("Cannot add the background layer to a group");
        }

        var group = page.FindLayerGroup(_groupId)
            ?? throw new InvalidOperationException($"Layer group {_groupId} not found");

        _previousGroupId = layer.GroupId;
        layer.SetGroupId(_groupId);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var layer = page.FindLayer(_layerId);
        layer?.SetGroupId(_previousGroupId);
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
                ["layerId"] = _layerId,
                ["groupId"] = _groupId
            }
        };
    }
}

public sealed class RemoveLayerFromGroupCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RemoveLayerFromGroup";
    public string Description => "Remove layer from group";

    private readonly Guid _pageId;
    private readonly Guid _layerId;
    private Guid? _previousGroupId;

    public RemoveLayerFromGroupCommand(Guid pageId, Guid layerId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _layerId = layerId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var layer = page.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _previousGroupId = layer.GroupId;
        layer.SetGroupId(null);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var layer = page.FindLayer(_layerId);
        layer?.SetGroupId(_previousGroupId);
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
                ["layerId"] = _layerId
            }
        };
    }
}

public sealed class SetLayerGroupVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerGroupVisibility";
    public string Description => "Set layer group visibility";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly bool _visible;
    private bool _previousVisible;

    public SetLayerGroupVisibilityCommand(Guid pageId, Guid groupId, bool visible)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId;
        _visible = visible;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId)
            ?? throw new InvalidOperationException($"Layer group {_groupId} not found");

        _previousVisible = group.IsVisible;
        group.SetVisible(_visible);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId);
        group?.SetVisible(_previousVisible);
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
                ["visible"] = _visible
            }
        };
    }
}

public sealed class SetLayerGroupLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerGroupLocked";
    public string Description => "Set layer group locked";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly bool _locked;
    private bool _previousLocked;

    public SetLayerGroupLockedCommand(Guid pageId, Guid groupId, bool locked)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId;
        _locked = locked;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId)
            ?? throw new InvalidOperationException($"Layer group {_groupId} not found");

        _previousLocked = group.IsLocked;
        group.SetLocked(_locked);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId);
        group?.SetLocked(_previousLocked);
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
                ["locked"] = _locked
            }
        };
    }
}

public sealed class SetLayerGroupExpandedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerGroupExpanded";
    public string Description => "Set layer group expanded";

    private readonly Guid _pageId;
    private readonly Guid _groupId;
    private readonly bool _expanded;
    private bool _previousExpanded;

    public SetLayerGroupExpandedCommand(Guid pageId, Guid groupId, bool expanded)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _groupId = groupId;
        _expanded = expanded;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId)
            ?? throw new InvalidOperationException($"Layer group {_groupId} not found");

        _previousExpanded = group.IsExpanded;
        group.SetExpanded(_expanded);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var group = page.FindLayerGroup(_groupId);
        group?.SetExpanded(_previousExpanded);
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
                ["expanded"] = _expanded
            }
        };
    }
}
