using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateNamedBalloonStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateNamedBalloonStyle";
    public string Description => "Create balloon style";

    private readonly Guid _styleId;
    private readonly string _name;
    private readonly BalloonStyle _style;
    private readonly Guid? _parentStyleId;
    private readonly BalloonStyleOverride? _overrides;
    private readonly bool _isQuickSelect;
    private readonly bool _applyExtendedDetails;
    private readonly BalloonShape _shape;
    private readonly string? _customShapePathData;
    private readonly bool _constrainToPanel;
    private readonly TextStyle _textStyle;
    private readonly TextPath? _textPath;
    private readonly IReadOnlyList<BalloonTemplateTail>? _tails;

    public Guid CreatedStyleId => _styleId;

    public CreateNamedBalloonStyleCommand(
        string name,
        BalloonStyle? style = null,
        Guid? styleId = null,
        Guid? parentStyleId = null,
        BalloonStyleOverride? overrides = null,
        bool isQuickSelect = true,
        bool applyExtendedDetails = true,
        BalloonShape shape = BalloonShape.Oval,
        string? customShapePathData = null,
        bool constrainToPanel = false,
        TextStyle? textStyle = null,
        TextPath? textPath = null,
        IEnumerable<BalloonTemplateTail>? tails = null)
    {
        Id = Guid.NewGuid();
        _styleId = styleId ?? Guid.NewGuid();
        _name = name;
        _style = style ?? BalloonStyle.Default;
        _parentStyleId = parentStyleId;
        _overrides = overrides;
        _isQuickSelect = isQuickSelect;
        _applyExtendedDetails = applyExtendedDetails;
        _shape = shape;
        _customShapePathData = customShapePathData;
        _constrainToPanel = constrainToPanel;
        _textStyle = textStyle ?? TextStyle.Default;
        _textPath = textPath?.Clone();
        _tails = tails?.Select(tail => tail.Clone()).ToList();
    }

    public void Execute(Document document)
    {
        var parentStyle = document.ResolveNamedBalloonStyle(_parentStyleId);
        var overrides = _overrides ?? (_parentStyleId.HasValue
            ? BalloonStyleOverride.FromDifference(parentStyle, _style)
            : BalloonStyleOverride.FromStyle(_style));
        var resolved = overrides.ApplyTo(parentStyle);
        document.AddBalloonStyle(new NamedBalloonStyle(
            _styleId,
            _name,
            resolved,
            _parentStyleId,
            overrides,
            _isQuickSelect,
            _applyExtendedDetails,
            _shape,
            _customShapePathData,
            _constrainToPanel,
            _textStyle,
            _textPath?.Clone(),
            _tails?.Select(tail => tail.Clone())));
    }

    public void Undo(Document document)
    {
        document.RemoveBalloonStyle(_styleId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId,
                ["name"] = _name,
                ["style"] = _style,
                ["parentStyleId"] = _parentStyleId,
                ["overrides"] = _overrides,
                ["isQuickSelect"] = _isQuickSelect,
                ["applyExtendedDetails"] = _applyExtendedDetails,
                ["shape"] = _shape,
                ["customShapePathData"] = _customShapePathData,
                ["constrainToPanel"] = _constrainToPanel,
                ["textStyle"] = _textStyle,
                ["textPath"] = _textPath,
                ["tails"] = _tails
            }
        };
    }
}

public sealed class UpdateNamedBalloonStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UpdateNamedBalloonStyle";
    public string Description => "Update balloon style";

    private readonly Guid _styleId;
    private readonly BalloonStyle _newStyle;
    private readonly bool? _newApplyExtendedDetails;
    private readonly BalloonShape? _newShape;
    private readonly string? _newCustomShapePathData;
    private readonly bool _hasNewCustomShapePathData;
    private readonly bool? _newConstrainToPanel;
    private readonly TextStyle? _newTextStyle;
    private readonly bool _hasNewTextStyle;
    private readonly TextPath? _newTextPath;
    private readonly bool _hasNewTextPath;
    private readonly IReadOnlyList<BalloonTemplateTail>? _newTails;
    private readonly bool _hasNewTails;
    private BalloonStyle? _oldStyle;
    private BalloonStyleOverride? _oldOverrides;
    private bool _oldApplyExtendedDetails;
    private BalloonShape _oldShape;
    private string? _oldCustomShapePathData;
    private bool _oldConstrainToPanel;
    private TextStyle? _oldTextStyle;
    private TextPath? _oldTextPath;
    private List<BalloonTemplateTail>? _oldTails;

    public UpdateNamedBalloonStyleCommand(
        Guid styleId,
        BalloonStyle newStyle,
        bool? applyExtendedDetails = null,
        BalloonShape? shape = null,
        string? customShapePathData = null,
        bool hasCustomShapePathData = false,
        bool? constrainToPanel = null,
        TextStyle? textStyle = null,
        bool hasTextStyle = false,
        TextPath? textPath = null,
        bool hasTextPath = false,
        IEnumerable<BalloonTemplateTail>? tails = null,
        bool hasTails = false)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newStyle = newStyle;
        _newApplyExtendedDetails = applyExtendedDetails;
        _newShape = shape;
        _newCustomShapePathData = customShapePathData;
        _hasNewCustomShapePathData = hasCustomShapePathData || customShapePathData != null || shape.HasValue;
        _newConstrainToPanel = constrainToPanel;
        _newTextStyle = textStyle;
        _hasNewTextStyle = hasTextStyle || textStyle != null;
        _newTextPath = textPath?.Clone();
        _hasNewTextPath = hasTextPath || textPath != null || textStyle != null;
        _newTails = tails?.Select(tail => tail.Clone()).ToList();
        _hasNewTails = hasTails || tails != null;
    }

    public void Execute(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        _oldStyle = style.Style;
        _oldOverrides = style.Overrides.Clone();
        _oldApplyExtendedDetails = style.ApplyExtendedDetails;
        _oldShape = style.Shape;
        _oldCustomShapePathData = style.CustomShapePathData;
        _oldConstrainToPanel = style.ConstrainToPanel;
        _oldTextStyle = style.TextStyle;
        _oldTextPath = style.TextPath?.Clone();
        _oldTails = style.Tails.Select(tail => tail.Clone()).ToList();

        var parentStyle = document.ResolveNamedBalloonStyle(style.ParentStyleId);
        var overrides = BalloonStyleOverride.FromDifference(parentStyle, _newStyle);
        style.SetOverrides(overrides);
        style.SetStyle(_newStyle);
        var shouldUpdateExtendedDetails =
            _newApplyExtendedDetails.HasValue ||
            _newShape.HasValue ||
            _hasNewCustomShapePathData ||
            _newConstrainToPanel.HasValue ||
            _hasNewTextStyle ||
            _hasNewTextPath ||
            _hasNewTails;
        if (shouldUpdateExtendedDetails)
        {
            var tailsToApply = _hasNewTails
                ? (_newTails?.Select(tail => tail.Clone()).ToList() ?? new List<BalloonTemplateTail>())
                : style.Tails.Select(tail => tail.Clone()).ToList();
            style.SetExtendedDetails(
                _newApplyExtendedDetails ?? style.ApplyExtendedDetails,
                _newShape ?? style.Shape,
                _hasNewCustomShapePathData ? _newCustomShapePathData : style.CustomShapePathData,
                _newConstrainToPanel ?? style.ConstrainToPanel,
                _hasNewTextStyle ? (_newTextStyle ?? style.TextStyle) : style.TextStyle,
                _hasNewTextPath ? _newTextPath?.Clone() : style.TextPath?.Clone(),
                tailsToApply);
        }
    }

    public void Undo(Document document)
    {
        if (_oldStyle == null) return;

        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        style.SetStyle(_oldStyle);
        if (_oldOverrides != null)
        {
            style.SetOverrides(_oldOverrides);
        }
        if (_oldTextStyle != null)
        {
            style.SetExtendedDetails(
                _oldApplyExtendedDetails,
                _oldShape,
                _oldCustomShapePathData,
                _oldConstrainToPanel,
                _oldTextStyle,
                _oldTextPath?.Clone(),
                _oldTails?.Select(tail => tail.Clone()));
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
                ["styleId"] = _styleId,
                ["style"] = _newStyle,
                ["applyExtendedDetails"] = _newApplyExtendedDetails,
                ["shape"] = _newShape,
                ["customShapePathData"] = _newCustomShapePathData,
                ["constrainToPanel"] = _newConstrainToPanel,
                ["textStyle"] = _newTextStyle,
                ["textPath"] = _newTextPath,
                ["tails"] = _newTails
            }
        };
    }
}

public sealed class RenameNamedBalloonStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenameNamedBalloonStyle";
    public string Description => "Rename balloon style";

    private readonly Guid _styleId;
    private readonly string _newName;
    private string _oldName = "";

    public RenameNamedBalloonStyleCommand(Guid styleId, string newName)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        _oldName = style.Name;
        style.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        style.SetName(_oldName);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId,
                ["name"] = _newName
            }
        };
    }
}

public sealed class SetNamedBalloonStyleQuickSelectCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetNamedBalloonStyleQuickSelect";
    public string Description => "Set balloon style quick select";

    private readonly Guid _styleId;
    private readonly bool _isQuickSelect;
    private bool _oldIsQuickSelect;

    public SetNamedBalloonStyleQuickSelectCommand(Guid styleId, bool isQuickSelect)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _isQuickSelect = isQuickSelect;
    }

    public void Execute(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        _oldIsQuickSelect = style.IsQuickSelect;
        style.SetQuickSelect(_isQuickSelect);
    }

    public void Undo(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        style.SetQuickSelect(_oldIsQuickSelect);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId,
                ["isQuickSelect"] = _isQuickSelect
            }
        };
    }
}

public sealed class DeleteNamedBalloonStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteNamedBalloonStyle";
    public string Description => "Delete balloon style";

    private readonly Guid _styleId;
    private NamedBalloonStyle? _removedStyle;
    private int _removedIndex;

    public DeleteNamedBalloonStyleCommand(Guid styleId)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
    }

    public void Execute(Document document)
    {
        var index = document.IndexOfBalloonStyle(_styleId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Balloon style {_styleId} not found");
        }

        _removedIndex = index;
        _removedStyle = document.BalloonStyles[index].Clone();
        document.RemoveBalloonStyle(_styleId);
    }

    public void Undo(Document document)
    {
        if (_removedStyle == null) return;
        document.InsertBalloonStyle(_removedIndex, _removedStyle);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId
            }
        };
    }
}

public sealed class SetNamedBalloonStyleParentCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetNamedBalloonStyleParent";
    public string Description => "Set balloon style parent";

    private readonly Guid _styleId;
    private readonly Guid? _newParentId;
    private Guid? _oldParentId;
    private BalloonStyleOverride? _oldOverrides;
    private BalloonStyle? _oldStyle;

    public SetNamedBalloonStyleParentCommand(Guid styleId, Guid? parentStyleId)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newParentId = parentStyleId;
    }

    public void Execute(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        _oldParentId = style.ParentStyleId;
        _oldOverrides = style.Overrides.Clone();
        _oldStyle = style.Style;

        var parentStyle = document.ResolveNamedBalloonStyle(_newParentId);
        var overrides = BalloonStyleOverride.FromDifference(parentStyle, style.Style);
        style.SetParentStyleId(_newParentId);
        style.SetOverrides(overrides);
    }

    public void Undo(Document document)
    {
        var style = document.FindBalloonStyle(_styleId)
            ?? throw new InvalidOperationException($"Balloon style {_styleId} not found");

        style.SetParentStyleId(_oldParentId);
        if (_oldOverrides != null)
        {
            style.SetOverrides(_oldOverrides);
        }
        if (_oldStyle != null)
        {
            style.SetStyle(_oldStyle);
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
                ["styleId"] = _styleId,
                ["parentStyleId"] = _newParentId
            }
        };
    }
}

public sealed class CreateNamedTextStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateNamedTextStyle";
    public string Description => "Create text style";

    private readonly Guid _styleId;
    private readonly string _name;
    private readonly TextStyle _style;
    private readonly Guid? _parentStyleId;
    private readonly TextStyleOverride? _overrides;

    public Guid CreatedStyleId => _styleId;

    public CreateNamedTextStyleCommand(
        string name,
        TextStyle? style = null,
        Guid? styleId = null,
        Guid? parentStyleId = null,
        TextStyleOverride? overrides = null)
    {
        Id = Guid.NewGuid();
        _styleId = styleId ?? Guid.NewGuid();
        _name = name;
        _style = style ?? TextStyle.Default;
        _parentStyleId = parentStyleId;
        _overrides = overrides;
    }

    public void Execute(Document document)
    {
        var parentStyle = document.ResolveNamedTextStyle(_parentStyleId);
        var overrides = _overrides ?? (_parentStyleId.HasValue
            ? TextStyleOverride.FromDifference(parentStyle, _style)
            : TextStyleOverride.FromStyle(_style));
        var resolved = overrides.ApplyTo(parentStyle);
        document.AddTextStyle(new NamedTextStyle(_styleId, _name, resolved, _parentStyleId, overrides));
    }

    public void Undo(Document document)
    {
        document.RemoveTextStyle(_styleId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId,
                ["name"] = _name,
                ["style"] = _style,
                ["parentStyleId"] = _parentStyleId,
                ["overrides"] = _overrides
            }
        };
    }
}

public sealed class UpdateNamedTextStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UpdateNamedTextStyle";
    public string Description => "Update text style";

    private readonly Guid _styleId;
    private readonly TextStyle _newStyle;
    private TextStyle? _oldStyle;
    private TextStyleOverride? _oldOverrides;

    public UpdateNamedTextStyleCommand(Guid styleId, TextStyle newStyle)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        _oldStyle = style.Style;
        _oldOverrides = style.Overrides.Clone();

        var parentStyle = document.ResolveNamedTextStyle(style.ParentStyleId);
        var overrides = TextStyleOverride.FromDifference(parentStyle, _newStyle);
        style.SetOverrides(overrides);
        style.SetStyle(_newStyle);
    }

    public void Undo(Document document)
    {
        if (_oldStyle == null) return;

        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        style.SetStyle(_oldStyle);
        if (_oldOverrides != null)
        {
            style.SetOverrides(_oldOverrides);
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
                ["styleId"] = _styleId,
                ["style"] = _newStyle
            }
        };
    }
}

public sealed class SetNamedTextStyleParentCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetNamedTextStyleParent";
    public string Description => "Set text style parent";

    private readonly Guid _styleId;
    private readonly Guid? _newParentId;
    private Guid? _oldParentId;
    private TextStyleOverride? _oldOverrides;
    private TextStyle? _oldStyle;

    public SetNamedTextStyleParentCommand(Guid styleId, Guid? parentStyleId)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newParentId = parentStyleId;
    }

    public void Execute(Document document)
    {
        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        _oldParentId = style.ParentStyleId;
        _oldOverrides = style.Overrides.Clone();
        _oldStyle = style.Style;

        var parentStyle = document.ResolveNamedTextStyle(_newParentId);
        var overrides = TextStyleOverride.FromDifference(parentStyle, style.Style);
        style.SetParentStyleId(_newParentId);
        style.SetOverrides(overrides);
    }

    public void Undo(Document document)
    {
        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        style.SetParentStyleId(_oldParentId);
        if (_oldOverrides != null)
        {
            style.SetOverrides(_oldOverrides);
        }
        if (_oldStyle != null)
        {
            style.SetStyle(_oldStyle);
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
                ["styleId"] = _styleId,
                ["parentStyleId"] = _newParentId
            }
        };
    }
}

public sealed class RenameNamedTextStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenameNamedTextStyle";
    public string Description => "Rename text style";

    private readonly Guid _styleId;
    private readonly string _newName;
    private string _oldName = "";

    public RenameNamedTextStyleCommand(Guid styleId, string newName)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        _oldName = style.Name;
        style.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var style = document.FindTextStyle(_styleId)
            ?? throw new InvalidOperationException($"Text style {_styleId} not found");

        style.SetName(_oldName);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId,
                ["name"] = _newName
            }
        };
    }
}

public sealed class DeleteNamedTextStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteNamedTextStyle";
    public string Description => "Delete text style";

    private readonly Guid _styleId;
    private NamedTextStyle? _removedStyle;
    private int _removedIndex;

    public DeleteNamedTextStyleCommand(Guid styleId)
    {
        Id = Guid.NewGuid();
        _styleId = styleId;
    }

    public void Execute(Document document)
    {
        var index = document.IndexOfTextStyle(_styleId);
        if (index < 0)
        {
            throw new InvalidOperationException($"Text style {_styleId} not found");
        }

        _removedIndex = index;
        _removedStyle = document.TextStyles[index].Clone();
        document.RemoveTextStyle(_styleId);
    }

    public void Undo(Document document)
    {
        if (_removedStyle == null) return;
        document.InsertTextStyle(_removedIndex, _removedStyle);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["styleId"] = _styleId
            }
        };
    }
}
