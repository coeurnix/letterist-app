using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreatePageTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreatePageTemplate";
    public string Description => "Create page template";

    private readonly Guid _pageId;
    private readonly Guid _templateId;
    private readonly string _name;
    private PageTemplate? _template;

    public Guid CreatedTemplateId => _templateId;

    public CreatePageTemplateCommand(Guid pageId, string name, Guid? templateId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _templateId = templateId ?? Guid.NewGuid();
        _name = string.IsNullOrWhiteSpace(name) ? "Page Template" : name.Trim();
    }

    public void Execute(Document document)
    {
        if (_template == null)
        {
            var page = document.FindPage(_pageId)
                ?? throw new InvalidOperationException($"Page {_pageId} not found");
            _template = PageTemplate.FromPage(page, _name, _templateId);
        }

        document.AddPageTemplate(_template.Clone());
    }

    public void Undo(Document document)
    {
        document.RemovePageTemplate(_templateId);
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

public sealed class CreatePageFromTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreatePageFromTemplate";
    public string Description => "Create page from template";

    private readonly Guid _templateId;
    private readonly Guid _pageId;
    private readonly string _name;
    private readonly int _insertIndex;
    private readonly bool _setActive;
    private Guid _previousActivePageId;
    private PageTemplate? _template;

    public Guid CreatedPageId => _pageId;

    public CreatePageFromTemplateCommand(Guid templateId, string name, int insertIndex = -1, bool setActive = true, Guid? pageId = null)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _pageId = pageId ?? Guid.NewGuid();
        _name = name ?? "";
        _insertIndex = insertIndex;
        _setActive = setActive;
    }

    public void Execute(Document document)
    {
        _previousActivePageId = document.ActivePageId;

        if (_template == null)
        {
            _template = document.FindPageTemplate(_templateId)?.Clone()
                ?? throw new InvalidOperationException($"Page template {_templateId} not found");
        }

        var pageName = string.IsNullOrWhiteSpace(_name) ? _template.Name : _name.Trim();
        var page = _template.CreatePage(pageName, _pageId);

        if (_insertIndex < 0 || _insertIndex >= document.Pages.Count)
        {
            document.AddPage(page);
        }
        else
        {
            document.InsertPage(_insertIndex, page);
        }

        if (_setActive)
        {
            document.SetActivePageId(_pageId);
        }
    }

    public void Undo(Document document)
    {
        document.RemovePage(_pageId);
        if (_setActive && document.FindPage(_previousActivePageId) != null)
        {
            document.SetActivePageId(_previousActivePageId);
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
                ["pageId"] = _pageId,
                ["name"] = _name,
                ["insertIndex"] = _insertIndex,
                ["setActive"] = _setActive
            }
        };
    }
}

public sealed class RenamePageTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenamePageTemplate";
    public string Description => "Rename page template";

    private readonly Guid _templateId;
    private readonly string _newName;
    private string _oldName = "";

    public RenamePageTemplateCommand(Guid templateId, string newName)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
        _newName = string.IsNullOrWhiteSpace(newName) ? "Page Template" : newName.Trim();
    }

    public void Execute(Document document)
    {
        var template = document.FindPageTemplate(_templateId)
            ?? throw new InvalidOperationException($"Page template {_templateId} not found");

        _oldName = template.Name;
        template.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var template = document.FindPageTemplate(_templateId)
            ?? throw new InvalidOperationException($"Page template {_templateId} not found");

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

public sealed class DeletePageTemplateCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeletePageTemplate";
    public string Description => "Delete page template";

    private readonly Guid _templateId;
    private PageTemplate? _deletedTemplate;
    private int _index;

    public DeletePageTemplateCommand(Guid templateId)
    {
        Id = Guid.NewGuid();
        _templateId = templateId;
    }

    public void Execute(Document document)
    {
        var template = document.FindPageTemplate(_templateId)
            ?? throw new InvalidOperationException($"Page template {_templateId} not found");

        _index = document.IndexOfPageTemplate(_templateId);
        _deletedTemplate = template.Clone();
        document.RemovePageTemplate(_templateId);
    }

    public void Undo(Document document)
    {
        if (_deletedTemplate == null)
        {
            throw new InvalidOperationException("Cannot undo - no template was deleted");
        }

        document.InsertPageTemplate(_index, _deletedTemplate.Clone());
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
