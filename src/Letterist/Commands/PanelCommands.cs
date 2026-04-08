using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreatePanelZoneCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreatePanelZone";
    public string Description => "Create panel";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly string _name;
    private readonly Rect _bounds;
    private readonly Color _color;
    private readonly PanelShape _shape;
    private readonly float _cornerRadius;
    private readonly float _safeMargin;
    private readonly Color _borderColor;
    private readonly float _borderWidth;
    private readonly PanelBorderStyle _borderStyle;
    private readonly string? _customShapePathData;
    private int _order;
    private int _insertIndex;

    public Guid CreatedPanelId => _panelId;

    public CreatePanelZoneCommand(
        Guid pageId,
        string name,
        Rect bounds,
        int order = -1,
        Color? color = null,
        Guid? panelId = null,
        int insertIndex = -1,
        PanelShape shape = PanelShape.Rectangle,
        float cornerRadius = 0f,
        float safeMargin = 0f,
        Color? borderColor = null,
        float? borderWidth = null,
        PanelBorderStyle borderStyle = PanelBorderStyle.Solid,
        string? customShapePathData = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId ?? Guid.NewGuid();
        _name = name;
        _bounds = bounds;
        _order = order;
        _color = color ?? PanelZone.DefaultColor;
        _insertIndex = insertIndex;
        _shape = shape;
        _cornerRadius = cornerRadius;
        _safeMargin = safeMargin;
        _borderColor = borderColor ?? PanelZone.DefaultBorderColor;
        _borderWidth = borderWidth ?? PanelZone.DefaultBorderWidth;
        _borderStyle = borderStyle;
        _customShapePathData = customShapePathData;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        if (_order < 0)
        {
            _order = page.Panels.Count + 1;
        }

        var panel = new PanelZone(
            _panelId,
            _name,
            _bounds,
            _order,
            _color,
            isVisible: true,
            isLocked: false,
            shape: _shape,
            cornerRadius: _cornerRadius,
            safeMargin: _safeMargin,
            borderColor: _borderColor,
            borderWidth: _borderWidth,
            borderStyle: _borderStyle,
            customShapePathData: _customShapePathData);

        if (_insertIndex < 0 || _insertIndex > page.Panels.Count)
        {
            _insertIndex = page.Panels.Count;
        }

        page.InsertPanel(_insertIndex, panel);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.RemovePanel(_panelId);
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
                ["panelId"] = _panelId,
                ["name"] = _name,
                ["x"] = _bounds.X,
                ["y"] = _bounds.Y,
                ["width"] = _bounds.Width,
                ["height"] = _bounds.Height,
                ["order"] = _order,
                ["colorR"] = _color.R,
                ["colorG"] = _color.G,
                ["colorB"] = _color.B,
                ["colorA"] = _color.A,
                ["insertIndex"] = _insertIndex,
                ["shape"] = _shape.ToString(),
                ["cornerRadius"] = _cornerRadius,
                ["safeMargin"] = _safeMargin,
                ["borderColorR"] = _borderColor.R,
                ["borderColorG"] = _borderColor.G,
                ["borderColorB"] = _borderColor.B,
                ["borderColorA"] = _borderColor.A,
                ["borderWidth"] = _borderWidth,
                ["borderStyle"] = _borderStyle.ToString(),
                ["customShapePathData"] = _customShapePathData
            }
        };
    }
}

public sealed class SetPanelZoneBoundsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneBounds";
    public string Description => "Update panel bounds";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly Rect _newBounds;
    private readonly bool _moveBalloons;
    private Rect _oldBounds;
    private readonly List<(Guid BalloonId, Point2 OldPosition)> _movedBalloons = new();

    public SetPanelZoneBoundsCommand(Guid pageId, Guid panelId, Rect newBounds, bool moveBalloons = true)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newBounds = newBounds;
        _moveBalloons = moveBalloons;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldBounds = panel.Bounds;

        var oldCenter = _oldBounds.Center;
        var newCenter = _newBounds.Center;
        var delta = new Point2(newCenter.X - oldCenter.X, newCenter.Y - oldCenter.Y);

        if (_moveBalloons && (Math.Abs(delta.X) > 0.001f || Math.Abs(delta.Y) > 0.001f))
        {
            _movedBalloons.Clear();
            foreach (var layer in page.Layers)
            {
                foreach (var balloon in layer.Balloons)
                {
                    if (balloon.PanelId == _panelId)
                    {
                        _movedBalloons.Add((balloon.Id, balloon.Position));
                        balloon.SetPosition(new Point2(balloon.Position.X + delta.X, balloon.Position.Y + delta.Y));
                    }
                }
            }
        }

        panel.SetBounds(_newBounds);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        foreach (var (balloonId, oldPosition) in _movedBalloons)
        {
            var balloon = document.FindBalloon(balloonId);
            balloon?.SetPosition(oldPosition);
        }

        panel.SetBounds(_oldBounds);
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
                ["panelId"] = _panelId,
                ["x"] = _newBounds.X,
                ["y"] = _newBounds.Y,
                ["width"] = _newBounds.Width,
                ["height"] = _newBounds.Height
            }
        };
    }
}

public sealed class DeletePanelZoneCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeletePanelZone";
    public string Description => "Delete panel";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private PanelZone? _deletedPanel;
    private int _index;
    private readonly Dictionary<Guid, Guid?> _balloonPanelIds = new();
    private readonly Dictionary<Guid, bool> _balloonConstrainStates = new();

    public DeletePanelZoneCommand(Guid pageId, Guid panelId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _index = page.IndexOfPanel(_panelId);
        _deletedPanel = panel.Clone();
        page.RemovePanel(_panelId);

        _balloonPanelIds.Clear();
        _balloonConstrainStates.Clear();
        foreach (var balloon in page.AllBalloons)
        {
            if (balloon.PanelId != _panelId) continue;
            _balloonPanelIds[balloon.Id] = balloon.PanelId;
            _balloonConstrainStates[balloon.Id] = balloon.ConstrainToPanel;
            balloon.SetPanelId(null);
            balloon.SetConstrainToPanel(false);
        }
    }

    public void Undo(Document document)
    {
        if (_deletedPanel == null)
        {
            throw new InvalidOperationException("Cannot undo - no panel deleted");
        }

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.InsertPanel(_index, _deletedPanel.Clone());

        if (_balloonPanelIds.Count > 0)
        {
            foreach (var entry in _balloonPanelIds)
            {
                var balloon = page.FindBalloon(entry.Key);
                if (balloon != null)
                {
                    balloon.SetPanelId(entry.Value);
                    if (_balloonConstrainStates.TryGetValue(entry.Key, out var constrain))
                    {
                        balloon.SetConstrainToPanel(constrain);
                    }
                }
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
                ["panelId"] = _panelId
            }
        };
    }
}

public sealed class SetPanelZoneNameCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneName";
    public string Description => "Rename panel";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly string _newName;
    private string _oldName = "";

    public SetPanelZoneNameCommand(Guid pageId, Guid panelId, string name)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newName = string.IsNullOrWhiteSpace(name) ? "Panel" : name.Trim();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldName = panel.Name;
        panel.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetName(_oldName);
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
                ["panelId"] = _panelId,
                ["name"] = _newName
            }
        };
    }
}

public sealed class SetPanelZoneVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneVisibility";
    public string Description => "Toggle panel visibility";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly bool _newValue;
    private bool _oldValue;

    public SetPanelZoneVisibilityCommand(Guid pageId, Guid panelId, bool isVisible)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newValue = isVisible;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldValue = panel.IsVisible;
        panel.SetVisible(_newValue);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetVisible(_oldValue);
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
                ["panelId"] = _panelId,
                ["isVisible"] = _newValue
            }
        };
    }
}

public sealed class SetPanelZoneLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneLocked";
    public string Description => "Toggle panel lock";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly bool _newValue;
    private bool _oldValue;

    public SetPanelZoneLockedCommand(Guid pageId, Guid panelId, bool isLocked)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newValue = isLocked;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldValue = panel.IsLocked;
        panel.SetLocked(_newValue);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetLocked(_oldValue);
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
                ["panelId"] = _panelId,
                ["isLocked"] = _newValue
            }
        };
    }
}

public sealed class SetPanelZoneShapeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneShape";
    public string Description => "Update panel shape";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly PanelShape _newShape;
    private readonly float _newCornerRadius;
    private readonly string? _newCustomShapePathData;
    private PanelShape _oldShape;
    private float _oldCornerRadius;
    private string? _oldCustomShapePathData;

    public SetPanelZoneShapeCommand(Guid pageId, Guid panelId, PanelShape shape, float cornerRadius = 0f, string? customShapePathData = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newShape = shape;
        _newCornerRadius = cornerRadius;
        _newCustomShapePathData = customShapePathData;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldShape = panel.Shape;
        _oldCornerRadius = panel.CornerRadius;
        _oldCustomShapePathData = panel.CustomShapePathData;

        panel.SetShape(_newShape);
        panel.SetCornerRadius(_newCornerRadius);
        panel.SetCustomShapePathData(_newCustomShapePathData);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetShape(_oldShape);
        panel.SetCornerRadius(_oldCornerRadius);
        panel.SetCustomShapePathData(_oldCustomShapePathData);
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
                ["panelId"] = _panelId,
                ["shape"] = _newShape.ToString(),
                ["cornerRadius"] = _newCornerRadius,
                ["customShapePathData"] = _newCustomShapePathData
            }
        };
    }
}

public sealed class SetPanelSafeMarginCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelSafeMargin";
    public string Description => "Set panel safe margin";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly float _newMargin;
    private float _oldMargin;

    public SetPanelSafeMarginCommand(Guid pageId, Guid panelId, float margin)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newMargin = Math.Max(0f, margin);
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldMargin = panel.SafeMargin;
        panel.SetSafeMargin(_newMargin);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetSafeMargin(_oldMargin);
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
                ["panelId"] = _panelId,
                ["margin"] = _newMargin
            }
        };
    }
}

public sealed class SetPanelGutterOverridesCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelGutterOverrides";
    public string Description => "Set panel gutter overrides";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly float? _newLeft;
    private readonly float? _newTop;
    private readonly float? _newRight;
    private readonly float? _newBottom;
    private float? _oldLeft;
    private float? _oldTop;
    private float? _oldRight;
    private float? _oldBottom;

    public SetPanelGutterOverridesCommand(Guid pageId, Guid panelId, float? left, float? top, float? right, float? bottom)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newLeft = left;
        _newTop = top;
        _newRight = right;
        _newBottom = bottom;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldLeft = panel.GutterLeftOverride;
        _oldTop = panel.GutterTopOverride;
        _oldRight = panel.GutterRightOverride;
        _oldBottom = panel.GutterBottomOverride;

        panel.SetGutterOverrides(_newLeft, _newTop, _newRight, _newBottom);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetGutterOverrides(_oldLeft, _oldTop, _oldRight, _oldBottom);
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
                ["panelId"] = _panelId,
                ["left"] = _newLeft,
                ["top"] = _newTop,
                ["right"] = _newRight,
                ["bottom"] = _newBottom
            }
        };
    }
}

public sealed class SetPanelBleedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelBleed";
    public string Description => "Set panel bleed";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly float _newLeft;
    private readonly float _newTop;
    private readonly float _newRight;
    private readonly float _newBottom;
    private float _oldLeft;
    private float _oldTop;
    private float _oldRight;
    private float _oldBottom;

    public SetPanelBleedCommand(Guid pageId, Guid panelId, float left, float top, float right, float bottom)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newLeft = Math.Max(0f, left);
        _newTop = Math.Max(0f, top);
        _newRight = Math.Max(0f, right);
        _newBottom = Math.Max(0f, bottom);
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldLeft = panel.BleedLeft;
        _oldTop = panel.BleedTop;
        _oldRight = panel.BleedRight;
        _oldBottom = panel.BleedBottom;

        panel.SetBleed(_newLeft, _newTop, _newRight, _newBottom);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetBleed(_oldLeft, _oldTop, _oldRight, _oldBottom);
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
                ["panelId"] = _panelId,
                ["left"] = _newLeft,
                ["top"] = _newTop,
                ["right"] = _newRight,
                ["bottom"] = _newBottom
            }
        };
    }
}

public sealed class SetPanelImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelImage";
    public string Description => "Set panel image";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly string? _newImagePath;
    private readonly PanelImagePlacement? _newPlacement;
    private string? _oldImagePath;
    private PanelImagePlacement? _oldPlacement;

    public SetPanelImageCommand(Guid pageId, Guid panelId, string? imagePath, PanelImagePlacement? placement)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newImagePath = imagePath;
        _newPlacement = placement;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldImagePath = panel.ImagePath;
        _oldPlacement = panel.ImagePlacement;
        panel.SetImage(_newImagePath, _newPlacement);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetImage(_oldImagePath, _oldPlacement);
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
                ["panelId"] = _panelId,
                ["imagePath"] = _newImagePath,
                ["imageOffsetX"] = _newPlacement?.Offset.X,
                ["imageOffsetY"] = _newPlacement?.Offset.Y,
                ["imageScale"] = _newPlacement?.Scale
            }
        };
    }
}

public sealed class SetPanelImagePlacementCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelImagePlacement";
    public string Description => "Update panel image placement";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly PanelImagePlacement? _newPlacement;
    private PanelImagePlacement? _oldPlacement;

    public SetPanelImagePlacementCommand(Guid pageId, Guid panelId, PanelImagePlacement? placement)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newPlacement = placement;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldPlacement = panel.ImagePlacement;
        panel.SetImagePlacement(_newPlacement);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetImagePlacement(_oldPlacement);
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
                ["panelId"] = _panelId,
                ["imageOffsetX"] = _newPlacement?.Offset.X,
                ["imageOffsetY"] = _newPlacement?.Offset.Y,
                ["imageScale"] = _newPlacement?.Scale
            }
        };
    }
}

public sealed class SetPanelImageExportVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelImageExportVisibility";
    public string Description => "Set panel image export visibility";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly bool _newValue;
    private bool _oldValue;

    public SetPanelImageExportVisibilityCommand(Guid pageId, Guid panelId, bool isVisibleInExport)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newValue = isVisibleInExport;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldValue = panel.IsImageVisibleInExport;
        panel.SetImageVisibleInExport(_newValue);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetImageVisibleInExport(_oldValue);
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
                ["panelId"] = _panelId,
                ["isVisibleInExport"] = _newValue
            }
        };
    }
}

public sealed class SetPanelZoneOrderCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneOrder";
    public string Description => "Change panel reading order";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly int _newOrder;
    private int _oldOrder;

    public SetPanelZoneOrderCommand(Guid pageId, Guid panelId, int newOrder)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newOrder = newOrder;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId);
        var panel = page?.FindPanel(_panelId);
        if (panel == null) return;

        _oldOrder = panel.Order;

        var panels = page!.Panels.OrderBy(p => p.Order).ToList();
        var index = panels.FindIndex(p => p.Id == _panelId);
        if (index >= 0)
        {
            panels.RemoveAt(index);
            var insertIndex = Math.Max(0, Math.Min(_newOrder - 1, panels.Count));
            panels.Insert(insertIndex, panel);

            for (int i = 0; i < panels.Count; i++)
            {
                panels[i].SetOrder(i + 1);
            }
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId);
        var panel = page?.FindPanel(_panelId);
        if (panel == null) return;

        var panels = page!.Panels.OrderBy(p => p.Order).ToList();
        var index = panels.FindIndex(p => p.Id == _panelId);
        if (index >= 0)
        {
            panels.RemoveAt(index);
            var insertIndex = Math.Max(0, Math.Min(_oldOrder - 1, panels.Count));
            panels.Insert(insertIndex, panel);

            for (int i = 0; i < panels.Count; i++)
            {
                panels[i].SetOrder(i + 1);
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
                ["panelId"] = _panelId,
                ["order"] = _newOrder
            }
        };
    }
}

public sealed class SetPanelZoneOrdersCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelZoneOrders";
    public string Description => "Reorder panels";

    private readonly Guid _pageId;
    private readonly List<Guid> _orderedPanelIds;
    private readonly Dictionary<Guid, int> _oldOrders = new();

    public SetPanelZoneOrdersCommand(Guid pageId, IEnumerable<Guid> orderedPanelIds)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _orderedPanelIds = orderedPanelIds?.Where(id => id != Guid.Empty).ToList() ?? new List<Guid>();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null) return;

        _oldOrders.Clear();
        foreach (var panel in page.Panels)
        {
            _oldOrders[panel.Id] = panel.Order;
        }

        var panelLookup = page.Panels.ToDictionary(panel => panel.Id, panel => panel);
        var ordered = new List<PanelZone>();
        var seen = new HashSet<Guid>();

        foreach (var id in _orderedPanelIds)
        {
            if (!panelLookup.TryGetValue(id, out var panel)) continue;
            if (!seen.Add(id)) continue;
            ordered.Add(panel);
        }

        foreach (var panel in page.Panels.OrderBy(panel => panel.Order))
        {
            if (seen.Add(panel.Id))
            {
                ordered.Add(panel);
            }
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SetOrder(i + 1);
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null || _oldOrders.Count == 0) return;

        foreach (var panel in page.Panels)
        {
            if (_oldOrders.TryGetValue(panel.Id, out var order))
            {
                panel.SetOrder(order);
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
                ["orderedPanelIds"] = _orderedPanelIds
            }
        };
    }
}

public enum PanelSplitOrientation
{
    Horizontal,
    Vertical
}

public sealed class SplitPanelZoneCommand : ICommand
{
    private const float MinPanelSize = 8f;

    public Guid Id { get; }
    public string CommandType => "SplitPanelZone";
    public string Description => "Split panel";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly PanelSplitOrientation _orientation;
    private readonly float _position;
    private readonly bool _isPercentage;

    private PanelZone? _originalPanel;
    private int _originalIndex;
    private readonly Dictionary<Guid, int> _originalOrders = new();
    private readonly Dictionary<Guid, Guid?> _originalBalloonPanels = new();
    private readonly Dictionary<Guid, bool> _originalBalloonConstraints = new();
    private readonly List<PanelZone> _newPanels = new();

    public SplitPanelZoneCommand(Guid pageId, Guid panelId, PanelSplitOrientation orientation, float position, bool isPercentage)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _orientation = orientation;
        _position = position;
        _isPercentage = isPercentage;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        if (_originalPanel == null)
        {
            _originalPanel = panel.Clone();
            _originalIndex = page.IndexOfPanel(panel.Id);
        }

        _originalOrders.Clear();
        foreach (var existing in page.Panels)
        {
            _originalOrders[existing.Id] = existing.Order;
        }

        _originalBalloonPanels.Clear();
        _originalBalloonConstraints.Clear();
        foreach (var balloon in page.AllBalloons)
        {
            if (balloon.PanelId == panel.Id)
            {
                _originalBalloonPanels[balloon.Id] = balloon.PanelId;
                _originalBalloonConstraints[balloon.Id] = balloon.ConstrainToPanel;
            }
        }

        if (_newPanels.Count == 0)
        {
            var bounds = panel.Bounds;
            var gutter = MathF.Max(0f, page.PanelGutterWidth);
            var available = _orientation == PanelSplitOrientation.Horizontal
                ? MathF.Max(0f, bounds.Height - gutter)
                : MathF.Max(0f, bounds.Width - gutter);

            if (available < MinPanelSize * 2f)
            {
                return;
            }

            var splitSize = _isPercentage
                ? available * Math.Clamp(_position, 0.1f, 0.9f)
                : Math.Clamp(_position, MinPanelSize, available - MinPanelSize);

            Rect firstBounds;
            Rect secondBounds;

            if (_orientation == PanelSplitOrientation.Horizontal)
            {
                firstBounds = new Rect(bounds.X, bounds.Y, bounds.Width, splitSize);
                secondBounds = new Rect(bounds.X, bounds.Y + splitSize + gutter, bounds.Width, available - splitSize);
            }
            else
            {
                firstBounds = new Rect(bounds.X, bounds.Y, splitSize, bounds.Height);
                secondBounds = new Rect(bounds.X + splitSize + gutter, bounds.Y, available - splitSize, bounds.Height);
            }

            var baseName = string.IsNullOrWhiteSpace(panel.Name) ? "Panel" : panel.Name;
            var first = new PanelZone(
                Guid.NewGuid(),
                $"{baseName} A",
                firstBounds,
                panel.Order,
                panel.Color,
                panel.IsVisible,
                panel.IsLocked,
                panel.Shape,
                panel.CornerRadius,
                panel.SafeMargin,
                panel.CustomShapePathData,
                panel.ImagePath,
                panel.ImagePlacement,
                panel.BorderColor,
                panel.BorderWidth,
                panel.BorderStyle,
                panel.IsImageVisibleInExport,
                panel.GutterLeftOverride,
                panel.GutterTopOverride,
                panel.GutterRightOverride,
                panel.GutterBottomOverride,
                panel.BleedLeft,
                panel.BleedTop,
                panel.BleedRight,
                panel.BleedBottom);

            var second = new PanelZone(
                Guid.NewGuid(),
                $"{baseName} B",
                secondBounds,
                panel.Order + 1,
                panel.Color,
                panel.IsVisible,
                panel.IsLocked,
                panel.Shape,
                panel.CornerRadius,
                panel.SafeMargin,
                panel.CustomShapePathData,
                panel.ImagePath,
                panel.ImagePlacement,
                panel.BorderColor,
                panel.BorderWidth,
                panel.BorderStyle,
                panel.IsImageVisibleInExport,
                panel.GutterLeftOverride,
                panel.GutterTopOverride,
                panel.GutterRightOverride,
                panel.GutterBottomOverride,
                panel.BleedLeft,
                panel.BleedTop,
                panel.BleedRight,
                panel.BleedBottom);

            _newPanels.Add(first);
            _newPanels.Add(second);
        }

        page.RemovePanel(panel.Id);

        foreach (var existing in page.Panels)
        {
            if (existing.Order > panel.Order)
            {
                existing.SetOrder(existing.Order + 1);
            }
        }

        if (_originalIndex < 0 || _originalIndex > page.Panels.Count)
        {
            _originalIndex = page.Panels.Count;
        }

        page.InsertPanel(_originalIndex, _newPanels[0].Clone());
        page.InsertPanel(_originalIndex + 1, _newPanels[1].Clone());

        var firstId = _newPanels[0].Id;
        var secondId = _newPanels[1].Id;
        var splitLine = _orientation == PanelSplitOrientation.Horizontal
            ? _newPanels[0].Bounds.Bottom + page.PanelGutterWidth / 2f
            : _newPanels[0].Bounds.Right + page.PanelGutterWidth / 2f;

        foreach (var balloon in page.AllBalloons)
        {
            if (balloon.PanelId != panel.Id) continue;

            var assignFirst = _orientation == PanelSplitOrientation.Horizontal
                ? balloon.Position.Y <= splitLine
                : balloon.Position.X <= splitLine;

            balloon.SetPanelId(assignFirst ? firstId : secondId);
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        foreach (var panel in _newPanels)
        {
            page.RemovePanel(panel.Id);
        }

        if (_originalPanel != null)
        {
            page.InsertPanel(_originalIndex, _originalPanel.Clone());
        }

        foreach (var panel in page.Panels)
        {
            if (_originalOrders.TryGetValue(panel.Id, out var order))
            {
                panel.SetOrder(order);
            }
        }

        foreach (var entry in _originalBalloonPanels)
        {
            var balloon = page.FindBalloon(entry.Key);
            if (balloon != null)
            {
                balloon.SetPanelId(entry.Value);
                if (_originalBalloonConstraints.TryGetValue(entry.Key, out var constrain))
                {
                    balloon.SetConstrainToPanel(constrain);
                }
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
                ["panelId"] = _panelId,
                ["orientation"] = _orientation.ToString(),
                ["position"] = _position,
                ["isPercentage"] = _isPercentage
            }
        };
    }
}

public sealed class MergePanelZonesCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MergePanelZones";
    public string Description => "Merge panels";

    private readonly Guid _pageId;
    private readonly Guid _primaryPanelId;
    private readonly Guid _secondaryPanelId;

    private PanelZone? _secondaryPanel;
    private int _secondaryIndex;
    private Rect _primaryOldBounds;
    private readonly Dictionary<Guid, Guid?> _balloonPanelIds = new();
    private readonly Dictionary<Guid, bool> _balloonConstrainStates = new();

    public MergePanelZonesCommand(Guid pageId, Guid primaryPanelId, Guid secondaryPanelId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _primaryPanelId = primaryPanelId;
        _secondaryPanelId = secondaryPanelId;
    }

    public void Execute(Document document)
    {
        if (_primaryPanelId == _secondaryPanelId) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var primary = page.FindPanel(_primaryPanelId)
            ?? throw new InvalidOperationException($"Panel {_primaryPanelId} not found");
        var secondary = page.FindPanel(_secondaryPanelId)
            ?? throw new InvalidOperationException($"Panel {_secondaryPanelId} not found");

        if (_secondaryPanel == null)
        {
            _secondaryPanel = secondary.Clone();
            _secondaryIndex = page.IndexOfPanel(secondary.Id);
            _primaryOldBounds = primary.Bounds;
        }

        primary.SetBounds(primary.Bounds.Union(secondary.Bounds));

        page.RemovePanel(secondary.Id);

        _balloonPanelIds.Clear();
        _balloonConstrainStates.Clear();
        foreach (var balloon in page.AllBalloons)
        {
            if (balloon.PanelId != _secondaryPanelId) continue;
            _balloonPanelIds[balloon.Id] = balloon.PanelId;
            _balloonConstrainStates[balloon.Id] = balloon.ConstrainToPanel;
            balloon.SetPanelId(_primaryPanelId);
        }
    }

    public void Undo(Document document)
    {
        if (_secondaryPanel == null) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var primary = page.FindPanel(_primaryPanelId)
            ?? throw new InvalidOperationException($"Panel {_primaryPanelId} not found");

        primary.SetBounds(_primaryOldBounds);

        if (_secondaryIndex < 0 || _secondaryIndex > page.Panels.Count)
        {
            _secondaryIndex = page.Panels.Count;
        }
        page.InsertPanel(_secondaryIndex, _secondaryPanel.Clone());

        foreach (var entry in _balloonPanelIds)
        {
            var balloon = page.FindBalloon(entry.Key);
            if (balloon != null)
            {
                balloon.SetPanelId(entry.Value);
                if (_balloonConstrainStates.TryGetValue(entry.Key, out var constrain))
                {
                    balloon.SetConstrainToPanel(constrain);
                }
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
                ["primaryPanelId"] = _primaryPanelId,
                ["secondaryPanelId"] = _secondaryPanelId
            }
        };
    }
}

public sealed class SetPanelGutterWidthCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelGutterWidth";
    public string Description => "Set panel gutter width";

    private readonly Guid _pageId;
    private readonly float _newWidth;
    private float _oldWidth;

    public SetPanelGutterWidthCommand(Guid pageId, float width)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newWidth = width;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null) return;

        _oldWidth = page.PanelGutterWidth;
        page.SetPanelGutterWidth(_newWidth);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null) return;

        page.SetPanelGutterWidth(_oldWidth);
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
                ["width"] = _newWidth
            }
        };
    }
}

public sealed class SetPanelGutterStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelGutterStyle";
    public string Description => "Set panel gutter style";

    private readonly Guid _pageId;
    private readonly Color _newColor;
    private readonly PanelBorderStyle _newStyle;
    private readonly bool _newFillEnabled;
    private Color _oldColor;
    private PanelBorderStyle _oldStyle;
    private bool _oldFillEnabled;

    public SetPanelGutterStyleCommand(Guid pageId, Color color, PanelBorderStyle style, bool fillEnabled)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newColor = color;
        _newStyle = style;
        _newFillEnabled = fillEnabled;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null) return;

        _oldColor = page.PanelGutterColor;
        _oldStyle = page.PanelGutterStrokeStyle;
        _oldFillEnabled = page.PanelGutterFillEnabled;

        page.SetPanelGutterColor(_newColor);
        page.SetPanelGutterStrokeStyle(_newStyle);
        page.SetPanelGutterFillEnabled(_newFillEnabled);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId);
        if (page == null) return;

        page.SetPanelGutterColor(_oldColor);
        page.SetPanelGutterStrokeStyle(_oldStyle);
        page.SetPanelGutterFillEnabled(_oldFillEnabled);
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
                ["colorR"] = _newColor.R,
                ["colorG"] = _newColor.G,
                ["colorB"] = _newColor.B,
                ["colorA"] = _newColor.A,
                ["style"] = _newStyle.ToString(),
                ["fillEnabled"] = _newFillEnabled
            }
        };
    }
}

public sealed class SetPanelBorderColorCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelBorderColor";
    public string Description => "Set panel border color";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly Color _newColor;
    private Color _oldColor;

    public SetPanelBorderColorCommand(Guid pageId, Guid panelId, Color color)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newColor = color;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldColor = panel.BorderColor;
        panel.SetBorderColor(_newColor);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetBorderColor(_oldColor);
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
                ["panelId"] = _panelId,
                ["r"] = _newColor.R,
                ["g"] = _newColor.G,
                ["b"] = _newColor.B,
                ["a"] = _newColor.A
            }
        };
    }
}

public sealed class SetPanelBorderWidthCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelBorderWidth";
    public string Description => "Set panel border width";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly float _newWidth;
    private float _oldWidth;

    public SetPanelBorderWidthCommand(Guid pageId, Guid panelId, float width)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newWidth = width;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldWidth = panel.BorderWidth;
        panel.SetBorderWidth(_newWidth);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetBorderWidth(_oldWidth);
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
                ["panelId"] = _panelId,
                ["width"] = _newWidth
            }
        };
    }
}

public sealed class SetPanelBorderStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPanelBorderStyle";
    public string Description => "Set panel border style";

    private readonly Guid _pageId;
    private readonly Guid _panelId;
    private readonly PanelBorderStyle _newStyle;
    private PanelBorderStyle _oldStyle;

    public SetPanelBorderStyleCommand(Guid pageId, Guid panelId, PanelBorderStyle style)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _panelId = panelId;
        _newStyle = style;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        _oldStyle = panel.BorderStyle;
        panel.SetBorderStyle(_newStyle);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var panel = page.FindPanel(_panelId)
            ?? throw new InvalidOperationException($"Panel {_panelId} not found");

        panel.SetBorderStyle(_oldStyle);
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
                ["panelId"] = _panelId,
                ["style"] = _newStyle.ToString()
            }
        };
    }
}
