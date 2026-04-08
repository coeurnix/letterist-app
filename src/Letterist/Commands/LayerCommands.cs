using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateLayer";
    public string Description => "Create layer";

    private readonly Guid _layerId;
    private readonly string _name;
    private readonly int _insertIndex;

    public Guid CreatedLayerId => _layerId;

    public CreateLayerCommand(string name, int insertIndex = -1, Guid? layerId = null)
    {
        Id = Guid.NewGuid();
        _layerId = layerId ?? Guid.NewGuid();
        _name = name;
        _insertIndex = insertIndex;
    }

    public void Execute(Document document)
    {
        var layer = new Layer(_layerId, _name);
        var insertIndex = _insertIndex;
        var backgroundLayer = document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            var minIndex = document.IndexOfLayer(backgroundLayer.Id) + 1;
            if (insertIndex >= 0 && insertIndex < minIndex)
            {
                insertIndex = minIndex;
            }
        }

        if (insertIndex < 0 || insertIndex >= document.Layers.Count)
        {
            document.AddLayer(layer);
        }
        else
        {
            document.InsertLayer(insertIndex, layer);
        }
    }

    public void Undo(Document document)
    {
        document.RemoveLayer(_layerId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["name"] = _name,
                ["insertIndex"] = _insertIndex
            }
        };
    }
}

public sealed class CreateImageLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateImageLayer";
    public string Description => "Create image layer";

    private readonly Guid _layerId;
    private readonly string _name;
    private readonly string _imagePath;
    private readonly int _insertIndex;

    public Guid CreatedLayerId => _layerId;

    public CreateImageLayerCommand(string name, string imagePath, int insertIndex = -1, Guid? layerId = null)
    {
        Id = Guid.NewGuid();
        _layerId = layerId ?? Guid.NewGuid();
        _name = name;
        _imagePath = imagePath;
        _insertIndex = insertIndex;
    }

    public void Execute(Document document)
    {
        var layer = Layer.CreateBackground(_name, _imagePath);
        var newLayer = new Layer(_layerId, _name, LayerKind.Image, _imagePath);

        var insertIndex = _insertIndex;
        var backgroundLayer = document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            var minIndex = document.IndexOfLayer(backgroundLayer.Id) + 1;
            if (insertIndex >= 0 && insertIndex < minIndex)
            {
                insertIndex = minIndex;
            }
        }

        if (insertIndex < 0 || insertIndex >= document.Layers.Count)
        {
            document.AddLayer(newLayer);
        }
        else
        {
            document.InsertLayer(insertIndex, newLayer);
        }
    }

    public void Undo(Document document)
    {
        document.RemoveLayer(_layerId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["name"] = _name,
                ["imagePath"] = _imagePath,
                ["insertIndex"] = _insertIndex
            }
        };
    }
}

public sealed class DeleteLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteLayer";
    public string Description => "Delete layer";

    private readonly Guid _layerId;
    private Layer? _deletedLayer;
    private int _layerIndex;
    private bool _wasActiveLayer;

    public DeleteLayerCommand(Guid layerId)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        if (layer.Kind == LayerKind.Image && document.BackgroundLayer?.Id == _layerId)
        {
            throw new InvalidOperationException("Cannot delete the background layer");
        }

        if (layer.Kind == LayerKind.Balloon && document.BalloonLayerCount <= 1)
        {
            throw new InvalidOperationException("Cannot delete the last layer");
        }

        _layerIndex = document.IndexOfLayer(_layerId);
        _deletedLayer = layer.Clone();
        _wasActiveLayer = document.ActiveLayerId == _layerId;

        document.RemoveLayer(_layerId);

        if (_wasActiveLayer && document.BalloonLayerCount > 0)
        {
            var forwardIndex = Math.Min(_layerIndex, document.Layers.Count - 1);
            var nextLayer = document.Layers
                .Skip(forwardIndex)
                .FirstOrDefault(l => l.Kind == LayerKind.Balloon)
                ?? document.Layers.LastOrDefault(l => l.Kind == LayerKind.Balloon);

            if (nextLayer != null)
            {
                document.SetActiveLayerId(nextLayer.Id);
            }
        }

        if (document.SelectedBalloonId.HasValue && _deletedLayer.ContainsBalloon(document.SelectedBalloonId.Value))
        {
            document.SetSelectedBalloonId(null);
        }
    }

    public void Undo(Document document)
    {
        if (_deletedLayer == null)
            throw new InvalidOperationException("Cannot undo - no layer was deleted");

        document.InsertLayer(_layerIndex, _deletedLayer.Clone());

        if (_wasActiveLayer)
        {
            document.SetActiveLayerId(_layerId);
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
                ["layerId"] = _layerId
            }
        };
    }
}

public sealed class RenameLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenameLayer";
    public string Description => "Rename layer";

    private readonly Guid _layerId;
    private readonly string _newName;
    private string _oldName = "";

    public RenameLayerCommand(Guid layerId, string newName)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _oldName = layer.Name;
        layer.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.SetName(_oldName);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["newName"] = _newName
            }
        };
    }
}

public sealed class ReorderLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ReorderLayer";
    public string Description => "Reorder layer";

    private readonly Guid _layerId;
    private readonly int _newIndex;
    private int _oldIndex;

    public ReorderLayerCommand(Guid layerId, int newIndex)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _newIndex = newIndex;
    }

    public void Execute(Document document)
    {
        _oldIndex = document.IndexOfLayer(_layerId);
        if (_oldIndex < 0)
        {
            throw new InvalidOperationException($"Layer {_layerId} not found");
        }

        var layer = document.FindLayer(_layerId);
        if (layer != null && layer.Kind == LayerKind.Image)
        {
            throw new InvalidOperationException("Cannot reorder the background layer");
        }

        var newIndex = _newIndex;
        var backgroundLayer = document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            var minIndex = document.IndexOfLayer(backgroundLayer.Id) + 1;
            if (newIndex < minIndex)
            {
                newIndex = minIndex;
            }
        }

        document.ReorderLayer(_layerId, newIndex);
    }

    public void Undo(Document document)
    {
        document.ReorderLayer(_layerId, _oldIndex);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["newIndex"] = _newIndex
            }
        };
    }
}

public sealed class SetLayerVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerVisibility";
    public string Description { get; }

    private readonly Guid _layerId;
    private readonly bool _visible;
    private bool _oldVisible;

    public SetLayerVisibilityCommand(Guid layerId, bool visible)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _visible = visible;
        Description = visible ? "Show layer" : "Hide layer";
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _oldVisible = layer.IsVisible;
        layer.SetVisible(_visible);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.SetVisible(_oldVisible);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["visible"] = _visible
            }
        };
    }
}

public sealed class SetLayerLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerLocked";
    public string Description { get; }

    private readonly Guid _layerId;
    private readonly bool _locked;
    private bool _oldLocked;

    public SetLayerLockedCommand(Guid layerId, bool locked)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _locked = locked;
        Description = locked ? "Lock layer" : "Unlock layer";
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _oldLocked = layer.IsLocked;
        layer.SetLocked(_locked);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.SetLocked(_oldLocked);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["locked"] = _locked
            }
        };
    }
}

public sealed class SetActiveLayerCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetActiveLayer";
    public string Description => "Set active layer";

    private readonly Guid _layerId;
    private Guid _oldActiveLayerId;

    public SetActiveLayerCommand(Guid layerId)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
    }

    public void Execute(Document document)
    {
        if (document.FindLayer(_layerId) == null)
        {
            throw new InvalidOperationException($"Layer {_layerId} not found");
        }

        _oldActiveLayerId = document.ActiveLayerId;
        document.SetActiveLayerId(_layerId);
    }

    public void Undo(Document document)
    {
        document.SetActiveLayerId(_oldActiveLayerId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId
            }
        };
    }
}

public sealed class SetLayerOpacityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerOpacity";
    public string Description => "Set layer opacity";

    private readonly Guid _layerId;
    private readonly float _opacity;
    private float _oldOpacity;

    public SetLayerOpacityCommand(Guid layerId, float opacity)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _opacity = Math.Clamp(opacity, 0f, 1f);
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _oldOpacity = layer.Opacity;
        layer.SetOpacity(_opacity);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.SetOpacity(_oldOpacity);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["opacity"] = _opacity
            }
        };
    }
}

public sealed class SetLayerBlendModeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetLayerBlendMode";
    public string Description => "Set layer blend mode";

    private readonly Guid _layerId;
    private readonly LayerBlendMode _blendMode;
    private LayerBlendMode _oldBlendMode;

    public SetLayerBlendModeCommand(Guid layerId, LayerBlendMode blendMode)
    {
        Id = Guid.NewGuid();
        _layerId = layerId;
        _blendMode = blendMode;
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        _oldBlendMode = layer.BlendMode;
        layer.SetBlendMode(_blendMode);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.SetBlendMode(_oldBlendMode);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["layerId"] = _layerId,
                ["blendMode"] = _blendMode.ToString()
            }
        };
    }
}

public sealed class MergeLayersCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MergeLayers";
    public string Description => "Merge layers";

    private readonly Guid _sourceLayerId;
    private readonly Guid _targetLayerId;
    private Layer? _sourceLayer;
    private int _sourceIndex;
    private bool _wasActiveLayer;

    public MergeLayersCommand(Guid sourceLayerId, Guid targetLayerId)
    {
        Id = Guid.NewGuid();
        _sourceLayerId = sourceLayerId;
        _targetLayerId = targetLayerId;
    }

    public void Execute(Document document)
    {
        if (_sourceLayerId == _targetLayerId)
            throw new InvalidOperationException("Cannot merge layer into itself");

        var sourceLayer = document.FindLayer(_sourceLayerId)
            ?? throw new InvalidOperationException($"Source layer {_sourceLayerId} not found");
        var targetLayer = document.FindLayer(_targetLayerId)
            ?? throw new InvalidOperationException($"Target layer {_targetLayerId} not found");

        if (sourceLayer.Kind != LayerKind.Balloon || targetLayer.Kind != LayerKind.Balloon)
        {
            throw new InvalidOperationException("Cannot merge image layers");
        }

        _sourceIndex = document.IndexOfLayer(_sourceLayerId);
        _sourceLayer = sourceLayer.Clone();
        _wasActiveLayer = document.ActiveLayerId == _sourceLayerId;

        foreach (var balloon in sourceLayer.Balloons.ToList())
        {
            var cloned = balloon.Clone();
            cloned.SetLayerId(_targetLayerId);
            sourceLayer.RemoveBalloon(balloon.Id);
            targetLayer.AddBalloon(cloned);
        }

        document.RemoveLayer(_sourceLayerId);

        if (_wasActiveLayer)
        {
            document.SetActiveLayerId(_targetLayerId);
        }
    }

    public void Undo(Document document)
    {
        if (_sourceLayer == null)
            throw new InvalidOperationException("Cannot undo - no merge was performed");

        var targetLayer = document.FindLayer(_targetLayerId)
            ?? throw new InvalidOperationException($"Target layer {_targetLayerId} not found");

        var restoredSource = _sourceLayer.Clone();
        document.InsertLayer(_sourceIndex, restoredSource);

        foreach (var balloon in restoredSource.Balloons)
        {
            var targetBalloon = targetLayer.FindBalloon(balloon.Id);
            if (targetBalloon != null)
            {
                targetLayer.RemoveBalloon(balloon.Id);
            }
        }

        if (_wasActiveLayer)
        {
            document.SetActiveLayerId(_sourceLayerId);
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
                ["sourceLayerId"] = _sourceLayerId,
                ["targetLayerId"] = _targetLayerId
            }
        };
    }
}

public sealed class FlattenVisibleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "FlattenVisible";
    public string Description => "Flatten visible layers";

    private readonly Guid _resultLayerId;
    private readonly string _resultLayerName;
    private List<Layer>? _originalLayers;
    private List<int>? _originalIndices;
    private Guid _oldActiveLayerId;

    public Guid ResultLayerId => _resultLayerId;

    public FlattenVisibleCommand(string resultLayerName = "Flattened", Guid? resultLayerId = null)
    {
        Id = Guid.NewGuid();
        _resultLayerName = resultLayerName;
        _resultLayerId = resultLayerId ?? Guid.NewGuid();
    }

    public void Execute(Document document)
    {
        var visibleLayers = document.Layers
            .Where(l => l.Kind == LayerKind.Balloon && l.IsVisible)
            .ToList();
        if (visibleLayers.Count == 0)
            throw new InvalidOperationException("No visible layers to flatten");

        _oldActiveLayerId = document.ActiveLayerId;
        _originalLayers = visibleLayers.Select(l => l.Clone()).ToList();
        _originalIndices = visibleLayers.Select(l => document.IndexOfLayer(l.Id)).ToList();

        var flattenedLayer = new Layer(_resultLayerId, _resultLayerName);

        foreach (var layer in visibleLayers)
        {
            foreach (var balloon in layer.Balloons)
            {
                var cloned = balloon.Clone();
                cloned.SetLayerId(_resultLayerId);
                flattenedLayer.AddBalloon(cloned);
            }

        }

        foreach (var layer in visibleLayers)
        {
            document.RemoveLayer(layer.Id);
        }

        var insertIndex = _originalIndices.Min();
        var backgroundLayer = document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            var minIndex = document.IndexOfLayer(backgroundLayer.Id) + 1;
            if (insertIndex < minIndex)
            {
                insertIndex = minIndex;
            }
        }
        if (insertIndex >= document.Layers.Count)
        {
            document.AddLayer(flattenedLayer);
        }
        else
        {
            document.InsertLayer(insertIndex, flattenedLayer);
        }

        document.SetActiveLayerId(_resultLayerId);
    }

    public void Undo(Document document)
    {
        if (_originalLayers == null || _originalIndices == null)
            throw new InvalidOperationException("Cannot undo - no flatten was performed");

        document.RemoveLayer(_resultLayerId);

        var layersWithIndices = _originalLayers.Zip(_originalIndices, (l, i) => (Layer: l, Index: i))
            .OrderBy(x => x.Index)
            .ToList();

        foreach (var (layer, index) in layersWithIndices)
        {
            if (index >= document.Layers.Count)
            {
                document.AddLayer(layer.Clone());
            }
            else
            {
                document.InsertLayer(index, layer.Clone());
            }
        }

        document.SetActiveLayerId(_oldActiveLayerId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["resultLayerId"] = _resultLayerId,
                ["resultLayerName"] = _resultLayerName
            }
        };
    }
}
