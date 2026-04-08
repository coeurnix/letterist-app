using Letterist.Model;
using Letterist.Persistence;
using System.Linq;

namespace Letterist.Commands;

public sealed class CreatePanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreatePanelLayoutTemplate";
    public string Description => "Create panel layout template";

    private readonly Guid _pageId;
    private readonly Guid _templateId;
    private readonly string _name;
    private PanelLayoutTemplate? _template;

    public Guid CreatedTemplateId => _templateId;

    public CreatePanelLayoutTemplateCommand(Guid pageId, string name, Guid? templateId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _templateId = templateId ?? Guid.NewGuid();
        _name = string.IsNullOrWhiteSpace(name) ? "Panel Layout" : name.Trim();
    }

    public void Execute(Document document)
    {
        if (_template == null)
        {
            var page = document.FindPage(_pageId)
                ?? throw new InvalidOperationException($"Page {_pageId} not found");
            _template = PanelLayoutTemplate.FromPage(page, _name, _templateId);
        }

        document.AddPanelTemplate(_template.Clone());
    }

    public void Undo(Document document)
    {
        document.RemovePanelTemplate(_templateId);
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
                ["templateId"] = _templateId,
                ["name"] = _name
            }
        };
    }
}

public sealed class ApplyPanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ApplyPanelLayoutTemplate";
    public string Description => "Apply panel layout template";

    private readonly Guid _templateId;
    private readonly Guid _pageId;
    private List<PanelZone>? _previousPanels;
    private readonly Dictionary<Guid, Guid?> _previousBalloonPanels = new();
    private readonly Dictionary<Guid, bool> _previousBalloonConstraints = new();

    public ApplyPanelLayoutTemplateCommand(Guid templateId, Guid pageId)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _pageId = pageId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        if (_previousPanels == null)
        {
            _previousPanels = page.Panels.Select(panel => panel.Clone()).ToList();
        }

        _previousBalloonPanels.Clear();
        _previousBalloonConstraints.Clear();
        foreach (var balloon in page.AllBalloons)
        {
            _previousBalloonPanels[balloon.Id] = balloon.PanelId;
            _previousBalloonConstraints[balloon.Id] = balloon.ConstrainToPanel;
            balloon.SetPanelId(null);
            balloon.SetConstrainToPanel(false);
        }

        page.ClearPanels();
        foreach (var panel in template.CreatePanels(page.Size))
        {
            page.AddPanel(panel);
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.ClearPanels();
        if (_previousPanels != null)
        {
            foreach (var panel in _previousPanels)
            {
                page.AddPanel(panel.Clone());
            }
        }

        foreach (var entry in _previousBalloonPanels)
        {
            var balloon = page.FindBalloon(entry.Key);
            if (balloon != null)
            {
                balloon.SetPanelId(entry.Value);
                if (_previousBalloonConstraints.TryGetValue(entry.Key, out var constrain))
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
                ["templateId"] = _templateId,
                ["pageId"] = _pageId
            }
        };
    }
}

public sealed class MergePanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MergePanelLayoutTemplate";
    public string Description => "Merge panel layout template";

    private readonly Guid _templateId;
    private readonly Guid _pageId;
    private List<PanelZone>? _addedPanels;

    public MergePanelLayoutTemplateCommand(Guid templateId, Guid pageId)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _pageId = pageId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        if (_addedPanels == null)
        {
            var baseOrder = page.Panels.Count > 0 ? page.Panels.Max(p => p.Order) : 0;
            var created = template.CreatePanels(page.Size).ToList();
            for (var i = 0; i < created.Count; i++)
            {
                created[i].SetOrder(baseOrder + i + 1);
            }
            _addedPanels = created;
        }

        foreach (var panel in _addedPanels)
        {
            page.AddPanel(panel.Clone());
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        if (_addedPanels == null) return;

        foreach (var panel in _addedPanels)
        {
            page.RemovePanel(panel.Id);
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
                ["templateId"] = _templateId,
                ["pageId"] = _pageId
            }
        };
    }
}

public sealed class RenamePanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenamePanelLayoutTemplate";
    public string Description => "Rename panel layout template";

    private readonly Guid _templateId;
    private readonly string _newName;
    private string _oldName = "";

    public RenamePanelLayoutTemplateCommand(Guid templateId, string newName)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _newName = string.IsNullOrWhiteSpace(newName) ? "Panel Layout" : newName.Trim();
    }

    public void Execute(Document document)
    {
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        _oldName = template.Name;
        template.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        template.SetName(_oldName);
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
                ["name"] = _newName
            }
        };
    }
}

public sealed class UpdatePanelLayoutTemplateMetadataCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "UpdatePanelLayoutTemplateMetadata";
    public string Description => "Update panel layout template metadata";

    private readonly Guid _templateId;
    private readonly string _newName;
    private readonly string? _newDescription;
    private readonly List<string> _newTags;
    private readonly string? _newCategory;
    private string _oldName = "";
    private string? _oldDescription;
    private List<string> _oldTags = new();
    private string? _oldCategory;

    public UpdatePanelLayoutTemplateMetadataCommand(Guid templateId, string name, string? description, IEnumerable<string>? tags, string? category)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _newName = string.IsNullOrWhiteSpace(name) ? "Panel Layout" : name.Trim();
        _newDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _newCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        _newTags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToList() ?? new List<string>();
    }

    public void Execute(Document document)
    {
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        _oldName = template.Name;
        _oldDescription = template.Description;
        _oldCategory = template.Category;
        _oldTags = template.Tags.ToList();

        template.SetMetadata(_newName, _newDescription, _newTags, _newCategory);
    }

    public void Undo(Document document)
    {
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        template.SetMetadata(_oldName, _oldDescription, _oldTags, _oldCategory);
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
                ["name"] = _newName,
                ["description"] = _newDescription,
                ["category"] = _newCategory,
                ["tags"] = _newTags
            }
        };
    }
}

public sealed class DeletePanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeletePanelLayoutTemplate";
    public string Description => "Delete panel layout template";

    private readonly Guid _templateId;
    private PanelLayoutTemplate? _deletedTemplate;
    private int _index;

    public DeletePanelLayoutTemplateCommand(Guid templateId)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
    }

    public void Execute(Document document)
    {
        var template = document.FindPanelTemplate(_templateId)
            ?? throw new InvalidOperationException($"Panel template {_templateId} not found");

        _index = document.IndexOfPanelTemplate(_templateId);
        _deletedTemplate = template.Clone();
        document.RemovePanelTemplate(_templateId);
    }

    public void Undo(Document document)
    {
        if (_deletedTemplate == null)
        {
            throw new InvalidOperationException("Cannot undo - no template was deleted");
        }

        document.InsertPanelTemplate(_index, _deletedTemplate.Clone());
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

public sealed class AddPanelLayoutTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "AddPanelLayoutTemplate";
    public string Description => "Add panel layout template";

    private readonly PanelLayoutTemplate _template;

    public AddPanelLayoutTemplateCommand(PanelLayoutTemplate template)
    {
        Id = Guid.NewGuid();
        _template = template ?? throw new ArgumentNullException(nameof(template));
    }

    public void Execute(Document document)
    {
        document.AddPanelTemplate(_template.Clone());
    }

    public void Undo(Document document)
    {
        document.RemovePanelTemplate(_template.Id);
    }

    public CommandData Serialize()
    {
        var templateFile = PanelLayoutTemplateFile.FromTemplate(_template);
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["template"] = templateFile
            }
        };
    }
}
