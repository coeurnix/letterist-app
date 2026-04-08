using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateBalloon";
    public string Description => "Create balloon";

    private readonly Guid _balloonId;
    private readonly Guid _layerId;
    private readonly Guid? _panelId;
    private readonly bool _constrainToPanel;
    private readonly Point2 _position;
    private readonly string _text;
    private readonly BalloonShape _shape;
    private readonly BalloonStyle _balloonStyle;
    private readonly TextStyle _textStyle;
    private readonly TextPath? _textPath;

    public Guid CreatedBalloonId => _balloonId;

    public CreateBalloonCommand(
        Guid layerId,
        Point2 position,
        string text = "",
        BalloonShape shape = BalloonShape.Oval,
        BalloonStyle? balloonStyle = null,
        TextStyle? textStyle = null,
        Guid? balloonId = null,
        Guid? panelId = null,
        bool constrainToPanel = false,
        TextPath? textPath = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId ?? Guid.NewGuid();
        _layerId = layerId;
        _panelId = panelId;
        _constrainToPanel = panelId.HasValue && constrainToPanel;
        _position = position;
        _text = text;
        _shape = shape;
        _balloonStyle = balloonStyle ?? BalloonStyle.Default;
        _textStyle = textStyle ?? TextStyle.Default;
        _textPath = textPath?.Clone();
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        var balloon = new Balloon(_balloonId, _layerId, _position, _shape, _balloonStyle, _text, _textStyle, panelId: _panelId, constrainToPanel: _constrainToPanel, textPath: _textPath);
        layer.AddBalloon(balloon);
    }

    public void Undo(Document document)
    {
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.RemoveBalloon(_balloonId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["layerId"] = _layerId,
                ["positionX"] = _position.X,
                ["positionY"] = _position.Y,
                ["text"] = _text,
                ["shape"] = _shape.ToString(),
                ["panelId"] = _panelId,
                ["constrainToPanel"] = _constrainToPanel,
                ["textPath"] = _textPath
            }
        };
    }
}

public sealed class MoveBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MoveBalloon";
    public string Description => "Move balloon";

    private readonly Guid _balloonId;
    private readonly Point2 _newPosition;
    private Point2 _oldPosition;

    public MoveBalloonCommand(Guid balloonId, Point2 newPosition)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newPosition = newPosition;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldPosition = balloon.Position;
        var targetPosition = _newPosition;
        if (balloon.ConstrainToPanel && balloon.PanelId.HasValue)
        {
            var panel = document.FindPanel(balloon.PanelId.Value);
            if (panel != null)
            {
                targetPosition = ConstrainPositionToPanel(targetPosition, balloon.ComputedSize, panel.Bounds);
            }
        }

        balloon.SetPosition(targetPosition);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetPosition(_oldPosition);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newPositionX"] = _newPosition.X,
                ["newPositionY"] = _newPosition.Y
            }
        };
    }

    private static Point2 ConstrainPositionToPanel(Point2 position, Size2 size, Rect panelBounds)
    {
        _ = size;

        var x = Math.Clamp(position.X, panelBounds.Left, panelBounds.Right);
        var y = Math.Clamp(position.Y, panelBounds.Top, panelBounds.Bottom);

        return new Point2(x, y);
    }
}

public sealed class ReorderBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ReorderBalloon";
    public string Description => "Reorder balloon";

    private readonly Guid _balloonId;
    private readonly int _newIndex;
    private int _oldIndex = -1;
    private Guid _layerId = Guid.Empty;

    public ReorderBalloonCommand(Guid balloonId, int newIndex)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newIndex = Math.Max(0, newIndex);
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayerContainingBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found in any layer");

        _layerId = layer.Id;
        _oldIndex = layer.IndexOfBalloon(_balloonId);
        if (_oldIndex < 0) return;

        layer.ReorderBalloon(_balloonId, _newIndex);
    }

    public void Undo(Document document)
    {
        if (_oldIndex < 0 || _layerId == Guid.Empty) return;

        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        layer.ReorderBalloon(_balloonId, _oldIndex);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newIndex"] = _newIndex
            }
        };
    }
}

public sealed class SetBalloonPanelCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonPanel";
    public string Description => "Set balloon panel";

    private readonly Guid _balloonId;
    private readonly Guid? _newPanelId;
    private Guid? _oldPanelId;
    private bool _oldConstrainToPanel;

    public SetBalloonPanelCommand(Guid balloonId, Guid? panelId)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newPanelId = panelId;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldPanelId = balloon.PanelId;
        _oldConstrainToPanel = balloon.ConstrainToPanel;
        balloon.SetPanelId(_newPanelId);
        if (!_newPanelId.HasValue)
        {
            balloon.SetConstrainToPanel(false);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetPanelId(_oldPanelId);
        balloon.SetConstrainToPanel(_oldConstrainToPanel);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["panelId"] = _newPanelId
            }
        };
    }
}

public sealed class SetBalloonConstrainToPanelCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonConstrainToPanel";
    public string Description => "Toggle panel constraint";

    private readonly Guid _balloonId;
    private readonly bool _newValue;
    private bool _oldValue;
    private Point2 _oldPosition;

    public SetBalloonConstrainToPanelCommand(Guid balloonId, bool constrainToPanel)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newValue = constrainToPanel;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldValue = balloon.ConstrainToPanel;
        _oldPosition = balloon.Position;

        var canConstrain = _newValue && balloon.PanelId.HasValue;
        balloon.SetConstrainToPanel(canConstrain);

        if (canConstrain)
        {
            var panel = document.FindPanel(balloon.PanelId!.Value);
            if (panel != null)
            {
                var constrained = ConstrainPositionToPanel(balloon.Position, balloon.ComputedSize, panel.Bounds);
                balloon.SetPosition(constrained);
            }
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetConstrainToPanel(_oldValue);
        balloon.SetPosition(_oldPosition);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["constrainToPanel"] = _newValue
            }
        };
    }

    private static Point2 ConstrainPositionToPanel(Point2 position, Size2 size, Rect panelBounds)
    {
        _ = size;

        var x = Math.Clamp(position.X, panelBounds.Left, panelBounds.Right);
        var y = Math.Clamp(position.Y, panelBounds.Top, panelBounds.Bottom);

        return new Point2(x, y);
    }
}

public sealed class SetBalloonVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonVisibility";
    public string Description => "Set balloon visibility";

    private readonly Guid _balloonId;
    private readonly bool _newVisible;
    private bool _oldVisible;
    private bool _clearedSelection;

    public SetBalloonVisibilityCommand(Guid balloonId, bool visible)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newVisible = visible;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldVisible = balloon.IsVisible;
        _clearedSelection = document.SelectedBalloonId == _balloonId && !_newVisible;

        balloon.SetVisible(_newVisible);
        if (_clearedSelection)
        {
            document.SetSelectedBalloonId(null);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetVisible(_oldVisible);
        if (_clearedSelection)
        {
            document.SetSelectedBalloonId(_balloonId);
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
                ["balloonId"] = _balloonId,
                ["visible"] = _newVisible
            }
        };
    }
}

public sealed class SetBalloonLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonLocked";
    public string Description => "Set balloon locked";

    private readonly Guid _balloonId;
    private readonly bool _newLocked;
    private bool _oldLocked;
    private bool _clearedSelection;

    public SetBalloonLockedCommand(Guid balloonId, bool locked)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newLocked = locked;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldLocked = balloon.IsLocked;
        _clearedSelection = document.SelectedBalloonId == _balloonId && _newLocked;

        balloon.SetLocked(_newLocked);
        if (_clearedSelection)
        {
            document.SetSelectedBalloonId(null);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetLocked(_oldLocked);
        if (_clearedSelection)
        {
            document.SetSelectedBalloonId(_balloonId);
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
                ["balloonId"] = _balloonId,
                ["locked"] = _newLocked
            }
        };
    }
}

public sealed class DeleteBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteBalloon";
    public string Description => "Delete balloon";

    private readonly Guid _balloonId;
    private Balloon? _deletedBalloon;
    private int _indexInLayer;
    private List<BalloonLink> _removedLinks = new();

    public DeleteBalloonCommand(Guid balloonId)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
    }

    public void Execute(Document document)
    {
        var layer = document.FindLayerContainingBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found in any layer");

        var page = document.ActivePage
            ?? throw new InvalidOperationException("No active page");

        _deletedBalloon = layer.FindBalloon(_balloonId)?.Clone();
        _indexInLayer = layer.IndexOfBalloon(_balloonId);

        layer.RemoveBalloon(_balloonId);
        _removedLinks = page.RemoveLinksForBalloon(_balloonId);

        if (document.SelectedBalloonId == _balloonId)
        {
            document.SetSelectedBalloonId(null);
        }
    }

    public void Undo(Document document)
    {
        if (_deletedBalloon == null)
            throw new InvalidOperationException("Cannot undo - no balloon was deleted");

        var layer = document.FindLayer(_deletedBalloon.LayerId)
            ?? throw new InvalidOperationException($"Layer {_deletedBalloon.LayerId} not found");

        layer.InsertBalloon(_indexInLayer, _deletedBalloon.Clone());
        if (_removedLinks.Count > 0 && document.ActivePage != null)
        {
            document.ActivePage.AddBalloonLinks(_removedLinks);
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
                ["balloonId"] = _balloonId
            }
        };
    }
}

public sealed class SetBalloonTextCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonText";
    public string Description => "Edit text";

    private readonly Guid _balloonId;
    private readonly string _newText;
    private string _oldText = "";
    private List<TextStyleSpan> _oldSpans = new();

    public SetBalloonTextCommand(Guid balloonId, string newText)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newText = newText;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldText = balloon.Text;
        _oldSpans = balloon.TextStyleSpans.Select(span => span.Clone()).ToList();
        balloon.SetText(_newText);
        balloon.ClearTextStyleSpans();
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetText(_oldText);
        balloon.SetTextStyleSpans(_oldSpans);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newText"] = _newText
            }
        };
    }
}

public sealed class SetBalloonRichTextCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonRichText";
    public string Description => "Edit rich text";

    private readonly Guid _balloonId;
    private readonly string _newText;
    private readonly List<TextStyleSpan> _newSpans;
    private string _oldText = "";
    private List<TextStyleSpan> _oldSpans = new();

    public SetBalloonRichTextCommand(Guid balloonId, string newText, IEnumerable<TextStyleSpan> spans)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newText = newText;
        _newSpans = spans.Select(span => span.Clone()).ToList();
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldText = balloon.Text;
        _oldSpans = balloon.TextStyleSpans.Select(span => span.Clone()).ToList();
        balloon.SetText(_newText);
        balloon.SetTextStyleSpans(_newSpans);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetText(_oldText);
        balloon.SetTextStyleSpans(_oldSpans);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["text"] = _newText,
                ["spans"] = _newSpans
            }
        };
    }
}

public sealed class SetBalloonShapeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonShape";
    public string Description => "Change shape";

    private readonly Guid _balloonId;
    private readonly BalloonShape _newShape;
    private BalloonShape _oldShape;

    public SetBalloonShapeCommand(Guid balloonId, BalloonShape newShape)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newShape = newShape;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldShape = balloon.Shape;
        balloon.SetShape(_newShape);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetShape(_oldShape);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newShape"] = _newShape.ToString()
            }
        };
    }
}

public sealed class SetBalloonCustomShapeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonCustomShape";
    public string Description => "Set custom shape";

    private readonly Guid _balloonId;
    private readonly string? _pathData;
    private string? _oldPathData;
    private BalloonShape _oldShape;

    public SetBalloonCustomShapeCommand(Guid balloonId, string? pathData)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _pathData = pathData;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldPathData = balloon.CustomShapePathData;
        _oldShape = balloon.Shape;

        balloon.SetCustomShapePathData(_pathData);
        if (!string.IsNullOrWhiteSpace(_pathData))
        {
            balloon.SetShape(BalloonShape.Custom);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetCustomShapePathData(_oldPathData);
        balloon.SetShape(_oldShape);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["pathData"] = _pathData
            }
        };
    }
}

public sealed class SetBalloonTextPathCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonTextPath";
    public string Description => "Set balloon text path";

    private readonly Guid _balloonId;
    private readonly TextPath? _newTextPath;
    private TextPath? _oldTextPath;

    public SetBalloonTextPathCommand(Guid balloonId, TextPath? textPath)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newTextPath = textPath?.Clone();
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldTextPath = balloon.TextPath?.Clone();
        balloon.SetTextPath(_newTextPath);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetTextPath(_oldTextPath);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["path"] = _newTextPath
            }
        };
    }
}

public sealed class SetBalloonStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonStyle";
    public string Description => "Change style";

    private readonly Guid _balloonId;
    private readonly BalloonStyle _newStyle;
    private BalloonStyle? _oldStyle;
    private Guid? _oldStyleId;
    private BalloonStyleOverride? _oldOverrides;

    public SetBalloonStyleCommand(Guid balloonId, BalloonStyle newStyle)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldStyle = balloon.BalloonStyle;
        _oldStyleId = balloon.BalloonStyleId;
        _oldOverrides = balloon.BalloonStyleOverrides.Clone();

        var baseStyle = document.ResolveNamedBalloonStyle(balloon.BalloonStyleId);
        var overrides = balloon.BalloonStyleId.HasValue
            ? BalloonStyleOverride.FromDifference(baseStyle, _newStyle)
            : BalloonStyleOverride.FromStyle(_newStyle);
        var resolved = overrides.ApplyTo(baseStyle);
        balloon.SetBalloonStyleReference(balloon.BalloonStyleId, overrides, resolved);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var restoredStyle = _oldStyle ?? BalloonStyle.Default;
        var restoredOverrides = _oldOverrides ?? BalloonStyleOverride.FromStyle(restoredStyle);
        balloon.SetBalloonStyleReference(_oldStyleId, restoredOverrides, restoredStyle);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["style"] = _newStyle
            }
        };
    }
}

public sealed class ResizeBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ResizeBalloon";
    public string Description => "Resize balloon";

    private readonly Guid _balloonId;
    private readonly Size2 _newSize;
    private readonly Point2 _newPosition;
    private readonly bool _wasManualFitting;
    private readonly float? _newMaxTextWidth;
    private readonly float? _newMaxTextHeight;
    private Size2 _oldSize;
    private Point2 _oldPosition;
    private float? _oldMaxTextWidth;
    private float? _oldMaxTextHeight;

    public ResizeBalloonCommand(
        Guid balloonId,
        Size2 newSize,
        Point2 newPosition,
        bool wasManualFitting = false,
        float? newMaxTextWidth = null,
        float? newMaxTextHeight = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newSize = newSize;
        _newPosition = newPosition;
        _wasManualFitting = wasManualFitting;
        _newMaxTextWidth = newMaxTextWidth;
        _newMaxTextHeight = newMaxTextHeight;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldSize = balloon.ComputedSize;
        _oldPosition = balloon.Position;
        _oldMaxTextWidth = balloon.MaxTextWidth;
        _oldMaxTextHeight = balloon.MaxTextHeight;

        balloon.SetComputedSize(_newSize);
        balloon.SetPosition(_newPosition);

        var preserveMaxTextDims = _wasManualFitting || _oldMaxTextWidth.HasValue || _oldMaxTextHeight.HasValue;
        var style = balloon.BalloonStyle;
        var computedMaxTextWidth = MathF.Max(1f, _newSize.Width - style.PaddingLeft - style.PaddingRight);
        var computedMaxTextHeight = MathF.Max(1f, _newSize.Height - style.PaddingTop - style.PaddingBottom);

        if (preserveMaxTextDims)
        {
            balloon.SetMaxTextWidth(_newMaxTextWidth ?? computedMaxTextWidth);
            balloon.SetMaxTextHeight(_newMaxTextHeight ?? computedMaxTextHeight);
        }
        else
        {
            balloon.SetMaxTextWidth(computedMaxTextWidth);
            balloon.SetMaxTextHeight(computedMaxTextHeight);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetComputedSize(_oldSize);
        balloon.SetPosition(_oldPosition);
        balloon.SetMaxTextWidth(_oldMaxTextWidth);
        balloon.SetMaxTextHeight(_oldMaxTextHeight);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newWidth"] = _newSize.Width,
                ["newHeight"] = _newSize.Height,
                ["newX"] = _newPosition.X,
                ["newY"] = _newPosition.Y,
                ["wasManualFitting"] = _wasManualFitting,
                ["newMaxTextWidth"] = _newMaxTextWidth,
                ["newMaxTextHeight"] = _newMaxTextHeight
            }
        };
    }
}

public sealed class SetTextStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTextStyle";
    public string Description => "Change text style";

    private readonly Guid _balloonId;
    private readonly TextStyle _newStyle;
    private TextStyle? _oldStyle;
    private Guid? _oldStyleId;
    private TextStyleOverride? _oldOverrides;

    public SetTextStyleCommand(Guid balloonId, TextStyle newStyle)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldStyle = balloon.TextStyle;
        _oldStyleId = balloon.TextStyleId;
        _oldOverrides = balloon.TextStyleOverrides.Clone();

        var baseStyle = document.ResolveNamedTextStyle(balloon.TextStyleId);
        var overrides = balloon.TextStyleId.HasValue
            ? TextStyleOverride.FromDifference(baseStyle, _newStyle)
            : TextStyleOverride.FromStyle(_newStyle);
        var resolved = overrides.ApplyTo(baseStyle);
        balloon.SetTextStyleReference(balloon.TextStyleId, overrides, resolved);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var restoredStyle = _oldStyle ?? TextStyle.Default;
        var restoredOverrides = _oldOverrides ?? TextStyleOverride.FromStyle(restoredStyle);
        balloon.SetTextStyleReference(_oldStyleId, restoredOverrides, restoredStyle);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["style"] = _newStyle
            }
        };
    }
}

public sealed class SetBalloonStyleReferenceCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonStyleReference";
    public string Description => "Set balloon style reference";

    private readonly Guid _balloonId;
    private readonly Guid? _styleId;
    private readonly BalloonStyleOverride? _overrides;

    private Guid? _oldStyleId;
    private BalloonStyleOverride? _oldOverrides;
    private BalloonStyle? _oldStyle;
    private BalloonShape _oldShape;
    private string? _oldCustomShapePathData;
    private bool _oldConstrainToPanel;
    private Guid? _oldTextStyleId;
    private TextStyleOverride? _oldTextStyleOverrides;
    private TextStyle? _oldTextStyle;
    private TextPath? _oldTextPath;
    private List<Tail>? _oldTails;

    public SetBalloonStyleReferenceCommand(Guid balloonId, Guid? styleId, BalloonStyleOverride? overrides = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _styleId = styleId;
        _overrides = overrides;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldStyleId = balloon.BalloonStyleId;
        _oldOverrides = balloon.BalloonStyleOverrides.Clone();
        _oldStyle = balloon.BalloonStyle;
        _oldShape = balloon.Shape;
        _oldCustomShapePathData = balloon.CustomShapePathData;
        _oldConstrainToPanel = balloon.ConstrainToPanel;
        _oldTextStyleId = balloon.TextStyleId;
        _oldTextStyleOverrides = balloon.TextStyleOverrides.Clone();
        _oldTextStyle = balloon.TextStyle;
        _oldTextPath = balloon.TextPath?.Clone();
        _oldTails = balloon.Tails.Select(tail => tail.Clone()).ToList();

        BalloonStyleOverride overrides;
        BalloonStyle baseStyle;
        NamedBalloonStyle? referencedStyle = null;
        if (_styleId.HasValue)
        {
            referencedStyle = document.FindBalloonStyle(_styleId.Value);
            baseStyle = document.ResolveNamedBalloonStyle(_styleId);
            overrides = _overrides ?? BalloonStyleOverride.Empty;
        }
        else
        {
            baseStyle = BalloonStyle.Default;
            overrides = _overrides ?? BalloonStyleOverride.FromStyle(balloon.BalloonStyle);
        }

        var resolved = overrides.ApplyTo(baseStyle);
        balloon.SetBalloonStyleReference(_styleId, overrides, resolved);
        if (referencedStyle?.ApplyExtendedDetails == true)
        {
            balloon.SetShape(referencedStyle.Shape);
            balloon.SetCustomShapePathData(referencedStyle.CustomShapePathData);
            balloon.SetConstrainToPanel(referencedStyle.ConstrainToPanel && balloon.PanelId.HasValue);

            var textStyleOverrides = TextStyleOverride.FromStyle(referencedStyle.TextStyle);
            balloon.SetTextStyleReference(null, textStyleOverrides, referencedStyle.TextStyle);
            balloon.SetTextPath(referencedStyle.TextPath?.Clone());

            var existingTails = balloon.Tails.Select(tail => tail.Clone()).ToList();
            balloon.ClearTails();
            for (var index = 0; index < referencedStyle.Tails.Count; index++)
            {
                var styledTail = referencedStyle.Tails[index].CreateTailAt(balloon.Position);
                if (existingTails.Count > index)
                {
                    CopyTailPlacement(existingTails[index], styledTail);
                }

                balloon.AddTail(styledTail);
            }
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var restoredStyle = _oldStyle ?? BalloonStyle.Default;
        var restoredOverrides = _oldOverrides ?? BalloonStyleOverride.FromStyle(restoredStyle);
        balloon.SetBalloonStyleReference(_oldStyleId, restoredOverrides, restoredStyle);

        balloon.SetShape(_oldShape);
        balloon.SetCustomShapePathData(_oldCustomShapePathData);
        balloon.SetConstrainToPanel(_oldConstrainToPanel);
        var restoredTextStyle = _oldTextStyle ?? TextStyle.Default;
        var restoredTextStyleOverrides = _oldTextStyleOverrides ?? TextStyleOverride.FromStyle(restoredTextStyle);
        balloon.SetTextStyleReference(_oldTextStyleId, restoredTextStyleOverrides, restoredTextStyle);
        balloon.SetTextPath(_oldTextPath?.Clone());
        balloon.ClearTails();
        if (_oldTails != null)
        {
            foreach (var tail in _oldTails)
            {
                balloon.AddTail(tail.Clone());
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
                ["balloonId"] = _balloonId,
                ["styleId"] = _styleId,
                ["overrides"] = _overrides
            }
        };
    }

    private static void CopyTailPlacement(Tail source, Tail destination)
    {
        destination.SetTargetPoint(source.TargetPoint);
        destination.SetAttachmentDirection(source.AttachmentDirection);
        destination.SetControlPoint(source.ControlPoint);
        destination.SetCurveCenter(source.CurveCenter);
        destination.SetInset(source.Inset);
    }
}

public sealed class SetTextStyleReferenceCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTextStyleReference";
    public string Description => "Set text style reference";

    private readonly Guid _balloonId;
    private readonly Guid? _styleId;
    private readonly TextStyleOverride? _overrides;

    private Guid? _oldStyleId;
    private TextStyleOverride? _oldOverrides;
    private TextStyle? _oldStyle;

    public SetTextStyleReferenceCommand(Guid balloonId, Guid? styleId, TextStyleOverride? overrides = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _styleId = styleId;
        _overrides = overrides;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldStyleId = balloon.TextStyleId;
        _oldOverrides = balloon.TextStyleOverrides.Clone();
        _oldStyle = balloon.TextStyle;

        TextStyleOverride overrides;
        TextStyle baseStyle;
        if (_styleId.HasValue)
        {
            baseStyle = document.ResolveNamedTextStyle(_styleId);
            overrides = _overrides ?? TextStyleOverride.Empty;
        }
        else
        {
            baseStyle = TextStyle.Default;
            overrides = _overrides ?? TextStyleOverride.FromStyle(balloon.TextStyle);
        }

        var resolved = overrides.ApplyTo(baseStyle);
        balloon.SetTextStyleReference(_styleId, overrides, resolved);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var restoredStyle = _oldStyle ?? TextStyle.Default;
        var restoredOverrides = _oldOverrides ?? TextStyleOverride.FromStyle(restoredStyle);
        balloon.SetTextStyleReference(_oldStyleId, restoredOverrides, restoredStyle);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["styleId"] = _styleId,
                ["overrides"] = _overrides
            }
        };
    }
}

public sealed class RotateBalloonCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RotateBalloon";
    public string Description => "Rotate balloon";

    private readonly Guid _balloonId;
    private readonly float _newRotation;
    private float _oldRotation;

    public RotateBalloonCommand(Guid balloonId, float newRotation)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _newRotation = newRotation;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldRotation = balloon.Rotation;
        balloon.SetRotation(_newRotation);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        balloon.SetRotation(_oldRotation);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["newRotation"] = _newRotation
            }
        };
    }
}
