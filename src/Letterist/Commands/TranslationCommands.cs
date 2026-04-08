using Letterist.Model;

namespace Letterist.Commands;

public sealed class SetDocumentBaseLanguageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentBaseLanguage";
    public string Description => "Set base language";

    private readonly string _newLanguage;
    private string _oldLanguage = "en";

    public SetDocumentBaseLanguageCommand(string language)
    {
        Id = Guid.NewGuid();
        _newLanguage = Document.NormalizeLanguageTag(language, "en");
    }

    public void Execute(Document document)
    {
        _oldLanguage = document.BaseLanguage;
        document.SetBaseLanguage(_newLanguage);
    }

    public void Undo(Document document)
    {
        document.SetBaseLanguage(_oldLanguage);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["language"] = _newLanguage
            }
        };
    }
}

public sealed class SetDocumentActiveLanguageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentActiveLanguage";
    public string Description => "Set active language";

    private readonly string _newLanguage;
    private string _oldLanguage = "en";

    public SetDocumentActiveLanguageCommand(string language)
    {
        Id = Guid.NewGuid();
        _newLanguage = language;
    }

    public void Execute(Document document)
    {
        _oldLanguage = document.ActiveLanguage;
        document.SetActiveLanguage(_newLanguage);
    }

    public void Undo(Document document)
    {
        document.SetActiveLanguage(_oldLanguage);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["language"] = _newLanguage
            }
        };
    }
}

public sealed class SetDocumentTranslationCompareCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentTranslationCompare";
    public string Description => "Set translation compare mode";

    private readonly TranslationCompareMode _newMode;
    private readonly string? _newLanguage;
    private TranslationCompareMode _oldMode;
    private string? _oldLanguage;

    public SetDocumentTranslationCompareCommand(TranslationCompareMode mode, string? compareLanguage = null)
    {
        Id = Guid.NewGuid();
        _newMode = mode;
        _newLanguage = compareLanguage;
    }

    public void Execute(Document document)
    {
        _oldMode = document.TranslationCompareMode;
        _oldLanguage = document.CompareLanguage;
        document.SetTranslationCompareMode(_newMode);
        document.SetCompareLanguage(_newLanguage);
    }

    public void Undo(Document document)
    {
        document.SetTranslationCompareMode(_oldMode);
        document.SetCompareLanguage(_oldLanguage);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["mode"] = _newMode,
                ["compareLanguage"] = _newLanguage
            }
        };
    }
}

public sealed class SetDocumentHighlightUntranslatedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentHighlightUntranslated";
    public string Description => "Toggle untranslated highlight";

    private readonly bool _newEnabled;
    private bool _oldEnabled;

    public SetDocumentHighlightUntranslatedCommand(bool enabled)
    {
        Id = Guid.NewGuid();
        _newEnabled = enabled;
    }

    public void Execute(Document document)
    {
        _oldEnabled = document.HighlightUntranslated;
        document.SetHighlightUntranslated(_newEnabled);
    }

    public void Undo(Document document)
    {
        document.SetHighlightUntranslated(_oldEnabled);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["enabled"] = _newEnabled
            }
        };
    }
}

public sealed class SetTranslationLanguageExportVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTranslationLanguageExportVisibility";
    public string Description => "Set translation language export visibility";

    private readonly string _language;
    private readonly bool _newVisible;
    private bool _hadOldValue;
    private bool _oldVisible;

    public SetTranslationLanguageExportVisibilityCommand(string language, bool visible)
    {
        Id = Guid.NewGuid();
        _language = language;
        _newVisible = visible;
    }

    public void Execute(Document document)
    {
        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        _hadOldValue = document.TryGetTranslationLanguageExportVisibility(normalized, out _oldVisible);
        document.SetTranslationLanguageExportVisibility(normalized, _newVisible);
    }

    public void Undo(Document document)
    {
        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (_hadOldValue)
        {
            document.SetTranslationLanguageExportVisibility(normalized, _oldVisible);
        }
        else
        {
            document.RemoveTranslationLanguageExportVisibility(normalized);
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
                ["language"] = _language,
                ["visible"] = _newVisible
            }
        };
    }
}

public sealed class RemoveTranslationLanguageExportVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RemoveTranslationLanguageExportVisibility";
    public string Description => "Remove translation language export visibility";

    private readonly string _language;
    private bool _hadOldValue;
    private bool _oldVisible;

    public RemoveTranslationLanguageExportVisibilityCommand(string language)
    {
        Id = Guid.NewGuid();
        _language = language;
    }

    public void Execute(Document document)
    {
        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (string.Equals(normalized, document.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _hadOldValue = false;
            _oldVisible = true;
            return;
        }

        _hadOldValue = document.TryGetTranslationLanguageExportVisibility(normalized, out _oldVisible);
        if (_hadOldValue)
        {
            document.RemoveTranslationLanguageExportVisibility(normalized);
        }
    }

    public void Undo(Document document)
    {
        if (!_hadOldValue)
        {
            return;
        }

        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (string.Equals(normalized, document.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        document.SetTranslationLanguageExportVisibility(normalized, _oldVisible);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["language"] = _language
            }
        };
    }
}

public sealed class SetBalloonTranslationCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonTranslation";
    public string Description => "Set balloon translation";

    private readonly Guid _balloonId;
    private readonly string _language;
    private readonly string _text;
    private readonly string? _sourceTextSnapshot;
    private readonly TranslationTextOrientation? _orientation;
    private bool _hadOldTranslation;
    private BalloonTranslation _oldTranslation;

    public SetBalloonTranslationCommand(
        Guid balloonId,
        string language,
        string text,
        string? sourceTextSnapshot = null,
        TranslationTextOrientation? orientation = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _language = language;
        _text = text ?? string.Empty;
        _sourceTextSnapshot = sourceTextSnapshot;
        _orientation = orientation;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (string.Equals(normalizedLanguage, document.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot store translation override for base language.");
        }

        _hadOldTranslation = balloon.TryGetTranslation(normalizedLanguage, out _oldTranslation);
        var sourceSnapshot = _sourceTextSnapshot ?? balloon.Text;
        var orientation = _orientation
            ?? (_hadOldTranslation ? _oldTranslation.Orientation : TranslationTextOrientation.Auto);
        balloon.SetTranslation(
            normalizedLanguage,
            new BalloonTranslation(_text, sourceSnapshot, DateTime.UtcNow, orientation));
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);

        if (_hadOldTranslation)
        {
            balloon.SetTranslation(normalizedLanguage, _oldTranslation);
        }
        else
        {
            balloon.RemoveTranslation(normalizedLanguage);
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
                ["language"] = _language,
                ["text"] = _text,
                ["sourceTextSnapshot"] = _sourceTextSnapshot,
                ["orientation"] = _orientation
            }
        };
    }
}

public sealed class SetBalloonTranslationOrientationCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonTranslationOrientation";
    public string Description => "Set balloon translation orientation";

    private readonly Guid _balloonId;
    private readonly string _language;
    private readonly TranslationTextOrientation _orientation;
    private bool _hadOldTranslation;
    private BalloonTranslation _oldTranslation;
    private bool _didChange;

    public SetBalloonTranslationOrientationCommand(Guid balloonId, string language, TranslationTextOrientation orientation)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _language = language;
        _orientation = orientation;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (string.Equals(normalizedLanguage, document.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot set translation orientation for base language.");
        }

        _hadOldTranslation = balloon.TryGetTranslation(normalizedLanguage, out _oldTranslation);
        if (!_hadOldTranslation && _orientation == TranslationTextOrientation.Auto)
        {
            _didChange = false;
            return;
        }

        _didChange = true;
        var text = _hadOldTranslation ? _oldTranslation.Text : string.Empty;
        var snapshot = _hadOldTranslation ? _oldTranslation.SourceTextSnapshot : balloon.Text;
        balloon.SetTranslation(
            normalizedLanguage,
            new BalloonTranslation(text, snapshot, DateTime.UtcNow, _orientation));
    }

    public void Undo(Document document)
    {
        if (!_didChange)
        {
            return;
        }

        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (_hadOldTranslation)
        {
            balloon.SetTranslation(normalizedLanguage, _oldTranslation);
        }
        else
        {
            balloon.RemoveTranslation(normalizedLanguage);
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
                ["language"] = _language,
                ["orientation"] = _orientation
            }
        };
    }
}

public sealed class DeleteBalloonTranslationCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteBalloonTranslation";
    public string Description => "Delete balloon translation";

    private readonly Guid _balloonId;
    private readonly string _language;
    private bool _hadOldTranslation;
    private BalloonTranslation _oldTranslation;

    public DeleteBalloonTranslationCommand(Guid balloonId, string language)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _language = language;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        _hadOldTranslation = balloon.TryGetTranslation(normalizedLanguage, out _oldTranslation);
        if (_hadOldTranslation)
        {
            balloon.RemoveTranslation(normalizedLanguage);
        }
    }

    public void Undo(Document document)
    {
        if (!_hadOldTranslation)
        {
            return;
        }

        var balloon = document.FindBalloonAnywhere(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");
        var normalizedLanguage = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        balloon.SetTranslation(normalizedLanguage, _oldTranslation);
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
                ["language"] = _language
            }
        };
    }
}

public sealed class SetTranslationLanguageLayoutCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTranslationLanguageLayout";
    public string Description => "Set translation language layout";

    private readonly string _language;
    private readonly TranslationLanguageLayout _newLayout;
    private bool _hadOldLayout;
    private TranslationLanguageLayout _oldLayout;

    public SetTranslationLanguageLayoutCommand(
        string language,
        TranslationTextDirection direction,
        TranslationTextOrientation orientation,
        bool mirrorTailsForRtl)
    {
        Id = Guid.NewGuid();
        _language = language;
        _newLayout = new TranslationLanguageLayout(direction, orientation, mirrorTailsForRtl);
    }

    public void Execute(Document document)
    {
        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        _hadOldLayout = document.TryGetTranslationLanguageLayout(normalized, out _oldLayout);
        document.SetTranslationLanguageLayout(normalized, _newLayout);
    }

    public void Undo(Document document)
    {
        var normalized = Document.NormalizeLanguageTag(_language, document.BaseLanguage);
        if (_hadOldLayout)
        {
            document.SetTranslationLanguageLayout(normalized, _oldLayout);
        }
        else
        {
            document.RemoveTranslationLanguageLayout(normalized);
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
                ["language"] = _language,
                ["direction"] = _newLayout.Direction,
                ["orientation"] = _newLayout.Orientation,
                ["mirrorTailsForRtl"] = _newLayout.MirrorTailsForRtl
            }
        };
    }
}
