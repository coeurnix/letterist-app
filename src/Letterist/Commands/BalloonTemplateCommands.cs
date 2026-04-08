using Letterist.Model;
using Letterist.Persistence;
using System.Linq;

namespace Letterist.Commands;

internal static class BalloonTemplateCommandHelpers
{
    public static (Guid? StyleId, BalloonStyleOverride Overrides, BalloonStyle ResolvedStyle) ResolveBalloonStyle(
        Document document,
        BalloonTemplate template)
    {
        if (template.BalloonStyleId.HasValue && document.FindBalloonStyle(template.BalloonStyleId.Value) != null)
        {
            var styleId = template.BalloonStyleId.Value;
            var baseStyle = document.ResolveNamedBalloonStyle(styleId);
            var overrides = template.BalloonStyleOverrides.Clone();
            return (styleId, overrides, overrides.ApplyTo(baseStyle));
        }

        var directOverrides = BalloonStyleOverride.FromStyle(template.BalloonStyle);
        return (null, directOverrides, template.BalloonStyle);
    }

    public static (Guid? StyleId, TextStyleOverride Overrides, TextStyle ResolvedStyle) ResolveTextStyle(
        Document document,
        BalloonTemplate template)
    {
        if (template.TextStyleId.HasValue && document.FindTextStyle(template.TextStyleId.Value) != null)
        {
            var styleId = template.TextStyleId.Value;
            var baseStyle = document.ResolveNamedTextStyle(styleId);
            var overrides = template.TextStyleOverrides.Clone();
            return (styleId, overrides, overrides.ApplyTo(baseStyle));
        }

        var directOverrides = TextStyleOverride.FromStyle(template.TextStyle);
        return (null, directOverrides, template.TextStyle);
    }

    public static void ApplyTemplateToBalloon(
        Document document,
        BalloonTemplate template,
        Balloon balloon,
        bool applyPlaceholderText,
        bool replaceTail)
    {
        balloon.SetShape(template.Shape);
        balloon.SetCustomShapePathData(template.CustomShapePathData);

        var balloonStyle = ResolveBalloonStyle(document, template);
        balloon.SetBalloonStyleReference(balloonStyle.StyleId, balloonStyle.Overrides, balloonStyle.ResolvedStyle);

        var textStyle = ResolveTextStyle(document, template);
        balloon.SetTextStyleReference(textStyle.StyleId, textStyle.Overrides, textStyle.ResolvedStyle);

        if (applyPlaceholderText)
        {
            balloon.SetText(template.PlaceholderText);
            balloon.ClearTextStyleSpans();
        }

        if (replaceTail)
        {
            balloon.SetTail(template.Tail?.CreateTailAt(balloon.Position));
        }
    }
}

internal sealed class BalloonTemplateSnapshot
{
    public BalloonShape Shape { get; init; } = BalloonShape.Oval;
    public string? CustomShapePathData { get; init; }
    public Guid? BalloonStyleId { get; init; }
    public BalloonStyleOverride BalloonStyleOverrides { get; init; } = BalloonStyleOverride.Empty;
    public BalloonStyle BalloonStyle { get; init; } = BalloonStyle.Default;
    public Guid? TextStyleId { get; init; }
    public TextStyleOverride TextStyleOverrides { get; init; } = TextStyleOverride.Empty;
    public TextStyle TextStyle { get; init; } = TextStyle.Default;
    public string Text { get; init; } = "";
    public List<TextStyleSpan> TextStyleSpans { get; init; } = new();
    public Tail? Tail { get; init; }

    public static BalloonTemplateSnapshot Capture(Balloon balloon)
    {
        return new BalloonTemplateSnapshot
        {
            Shape = balloon.Shape,
            CustomShapePathData = balloon.CustomShapePathData,
            BalloonStyleId = balloon.BalloonStyleId,
            BalloonStyleOverrides = balloon.BalloonStyleOverrides.Clone(),
            BalloonStyle = balloon.BalloonStyle,
            TextStyleId = balloon.TextStyleId,
            TextStyleOverrides = balloon.TextStyleOverrides.Clone(),
            TextStyle = balloon.TextStyle,
            Text = balloon.Text,
            TextStyleSpans = balloon.TextStyleSpans.Select(span => span.Clone()).ToList(),
            Tail = balloon.Tail?.Clone()
        };
    }

    public void Restore(Balloon balloon)
    {
        balloon.SetShape(Shape);
        balloon.SetCustomShapePathData(CustomShapePathData);
        balloon.SetBalloonStyleReference(BalloonStyleId, BalloonStyleOverrides.Clone(), BalloonStyle);
        balloon.SetTextStyleReference(TextStyleId, TextStyleOverrides.Clone(), TextStyle);
        balloon.SetText(Text);
        balloon.SetTextStyleSpans(TextStyleSpans.Select(span => span.Clone()));
        balloon.SetTail(Tail?.Clone());
    }
}

public sealed class CreateBalloonTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateBalloonTemplate";
    public string Description => "Create balloon template";

    private readonly Guid _sourceBalloonId;
    private readonly Guid _templateId;
    private readonly string _name;
    private readonly string? _description;
    private readonly List<string> _tags;
    private readonly string? _category;
    private readonly string? _placeholderText;
    private readonly bool _isFavorite;
    private readonly int? _hotkeySlot;
    private readonly bool _isBuiltIn;
    private BalloonTemplate? _template;

    public Guid CreatedTemplateId => _templateId;

    public CreateBalloonTemplateCommand(
        Guid sourceBalloonId,
        string name,
        string? description = null,
        IEnumerable<string>? tags = null,
        string? category = null,
        string? placeholderText = null,
        Guid? templateId = null,
        bool isFavorite = false,
        int? hotkeySlot = null,
        bool isBuiltIn = false)
    {
        Id = Guid.NewGuid();
        _sourceBalloonId = sourceBalloonId;
        _templateId = templateId ?? Guid.NewGuid();
        _name = name;
        _description = description;
        _tags = tags?.ToList() ?? new List<string>();
        _category = category;
        _placeholderText = placeholderText;
        _isFavorite = isFavorite;
        _hotkeySlot = hotkeySlot;
        _isBuiltIn = isBuiltIn;
    }

    public void Execute(Document document)
    {
        if (_template == null)
        {
            var source = document.FindBalloon(_sourceBalloonId)
                ?? throw new InvalidOperationException($"Balloon {_sourceBalloonId} not found");

            _template = BalloonTemplate.CreateFromBalloon(
                source,
                _name,
                _description,
                _tags,
                _category,
                _placeholderText,
                _templateId,
                _isFavorite,
                _hotkeySlot,
                _isBuiltIn);
        }

        document.AddBalloonTemplate(_template.Clone());
    }

    public void Undo(Document document)
    {
        document.RemoveBalloonTemplate(_templateId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["sourceBalloonId"] = _sourceBalloonId,
                ["templateId"] = _templateId,
                ["name"] = _name,
                ["description"] = _description,
                ["tags"] = _tags,
                ["category"] = _category,
                ["placeholderText"] = _placeholderText,
                ["isFavorite"] = _isFavorite,
                ["hotkeySlot"] = _hotkeySlot,
                ["isBuiltIn"] = _isBuiltIn
            }
        };
    }
}

public sealed class AddBalloonTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "AddBalloonTemplate";
    public string Description => "Add balloon template";

    private readonly BalloonTemplate _template;

    public AddBalloonTemplateCommand(BalloonTemplate template)
    {
        Id = Guid.NewGuid();
        _template = template?.Clone() ?? throw new ArgumentNullException(nameof(template));
    }

    public void Execute(Document document)
    {
        document.AddBalloonTemplate(_template.Clone());
    }

    public void Undo(Document document)
    {
        document.RemoveBalloonTemplate(_template.Id);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["template"] = BalloonTemplateFile.FromTemplate(_template)
            }
        };
    }
}

public sealed class UpdateBalloonTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UpdateBalloonTemplate";
    public string Description => "Update balloon template";

    private readonly Guid _templateId;
    private readonly BalloonTemplate _newTemplate;
    private BalloonTemplate? _oldTemplate;

    public UpdateBalloonTemplateCommand(Guid templateId, BalloonTemplate template)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _newTemplate = template?.Clone() ?? throw new ArgumentNullException(nameof(template));
        if (_newTemplate.Id != _templateId)
        {
            throw new ArgumentException("Template ID does not match command templateId.", nameof(template));
        }
    }

    public void Execute(Document document)
    {
        var template = document.FindBalloonTemplate(_templateId)
            ?? throw new InvalidOperationException($"Balloon template {_templateId} not found");

        _oldTemplate ??= template.Clone();
        template.SetFrom(_newTemplate);
    }

    public void Undo(Document document)
    {
        if (_oldTemplate == null)
        {
            throw new InvalidOperationException("Cannot undo - no previous template snapshot.");
        }

        var template = document.FindBalloonTemplate(_templateId)
            ?? throw new InvalidOperationException($"Balloon template {_templateId} not found");
        template.SetFrom(_oldTemplate);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["templateId"] = _templateId,
                ["template"] = BalloonTemplateFile.FromTemplate(_newTemplate)
            }
        };
    }
}

public sealed class DeleteBalloonTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteBalloonTemplate";
    public string Description => "Delete balloon template";

    private readonly Guid _templateId;
    private BalloonTemplate? _deletedTemplate;
    private int _index;

    public DeleteBalloonTemplateCommand(Guid templateId)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
    }

    public void Execute(Document document)
    {
        var template = document.FindBalloonTemplate(_templateId)
            ?? throw new InvalidOperationException($"Balloon template {_templateId} not found");

        _index = document.IndexOfBalloonTemplate(_templateId);
        _deletedTemplate = template.Clone();
        document.RemoveBalloonTemplate(_templateId);
    }

    public void Undo(Document document)
    {
        if (_deletedTemplate == null)
        {
            throw new InvalidOperationException("Cannot undo - no template was deleted.");
        }

        document.InsertBalloonTemplate(_index, _deletedTemplate.Clone());
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["templateId"] = _templateId
            }
        };
    }
}

public sealed class ApplyBalloonTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ApplyBalloonTemplate";
    public string Description => "Apply balloon template";

    private readonly Guid _templateId;
    private readonly Guid _balloonId;
    private readonly bool _applyPlaceholderText;
    private readonly bool _replaceTail;
    private BalloonTemplateSnapshot? _snapshot;

    public ApplyBalloonTemplateCommand(
        Guid templateId,
        Guid balloonId,
        bool applyPlaceholderText = false,
        bool replaceTail = true)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _balloonId = balloonId;
        _applyPlaceholderText = applyPlaceholderText;
        _replaceTail = replaceTail;
    }

    public void Execute(Document document)
    {
        var template = document.FindBalloonTemplate(_templateId)
            ?? throw new InvalidOperationException($"Balloon template {_templateId} not found");
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _snapshot ??= BalloonTemplateSnapshot.Capture(balloon);
        BalloonTemplateCommandHelpers.ApplyTemplateToBalloon(document, template, balloon, _applyPlaceholderText, _replaceTail);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        if (_snapshot == null)
        {
            throw new InvalidOperationException("Cannot undo - no balloon snapshot.");
        }

        _snapshot.Restore(balloon);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["templateId"] = _templateId,
                ["balloonId"] = _balloonId,
                ["applyPlaceholderText"] = _applyPlaceholderText,
                ["replaceTail"] = _replaceTail
            }
        };
    }
}

public sealed class CreateBalloonFromTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateBalloonFromTemplate";
    public string Description => "Create balloon from template";

    private readonly Guid _templateId;
    private readonly Guid _balloonId;
    private readonly Guid _layerId;
    private readonly Guid? _panelId;
    private readonly bool _constrainToPanel;
    private readonly Point2 _position;
    private readonly bool _usePlaceholderText;
    private readonly bool _attachTail;
    private Balloon? _createdBalloon;

    public Guid CreatedBalloonId => _balloonId;

    public CreateBalloonFromTemplateCommand(
        Guid templateId,
        Guid layerId,
        Point2 position,
        bool usePlaceholderText = true,
        bool attachTail = true,
        Guid? balloonId = null,
        Guid? panelId = null,
        bool constrainToPanel = false)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _balloonId = balloonId ?? Guid.NewGuid();
        _layerId = layerId;
        _position = position;
        _usePlaceholderText = usePlaceholderText;
        _attachTail = attachTail;
        _panelId = panelId;
        _constrainToPanel = panelId.HasValue && constrainToPanel;
    }

    public void Execute(Document document)
    {
        var template = document.FindBalloonTemplate(_templateId)
            ?? throw new InvalidOperationException($"Balloon template {_templateId} not found");
        var layer = document.FindLayer(_layerId)
            ?? throw new InvalidOperationException($"Layer {_layerId} not found");

        if (_createdBalloon == null)
        {
            var balloonStyle = BalloonTemplateCommandHelpers.ResolveBalloonStyle(document, template);
            var textStyle = BalloonTemplateCommandHelpers.ResolveTextStyle(document, template);
            var text = _usePlaceholderText ? template.PlaceholderText : string.Empty;

            var balloon = new Balloon(
                _balloonId,
                _layerId,
                _position,
                template.Shape,
                balloonStyle.ResolvedStyle,
                text,
                textStyle.ResolvedStyle,
                customShapePathData: template.CustomShapePathData,
                panelId: _panelId,
                constrainToPanel: _constrainToPanel);

            balloon.SetBalloonStyleReference(balloonStyle.StyleId, balloonStyle.Overrides, balloonStyle.ResolvedStyle);
            balloon.SetTextStyleReference(textStyle.StyleId, textStyle.Overrides, textStyle.ResolvedStyle);
            if (_attachTail && template.Tail != null)
            {
                balloon.SetTail(template.Tail.CreateTailAt(_position));
            }

            _createdBalloon = balloon;
        }

        layer.AddBalloon(_createdBalloon.Clone());
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
                ["templateId"] = _templateId,
                ["balloonId"] = _balloonId,
                ["layerId"] = _layerId,
                ["x"] = _position.X,
                ["y"] = _position.Y,
                ["panelId"] = _panelId,
                ["constrainToPanel"] = _constrainToPanel,
                ["usePlaceholderText"] = _usePlaceholderText,
                ["attachTail"] = _attachTail
            }
        };
    }
}
