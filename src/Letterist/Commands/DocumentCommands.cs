using Letterist.Model;

namespace Letterist.Commands;

public sealed class SetDocumentNameCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentName";
    public string Description => "Set document name";

    private readonly string _newName;
    private string _oldName = "";

    public SetDocumentNameCommand(string name)
    {
        Id = Guid.NewGuid();
        _newName = name;
    }

    public void Execute(Document document)
    {
        _oldName = document.Name;
        document.SetName(_newName);
    }

    public void Undo(Document document)
    {
        document.SetName(_oldName);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["name"] = _newName
            }
        };
    }
}

public sealed class SetDocumentDpiCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentDpi";
    public string Description => "Set document DPI";

    private readonly float _newDpi;
    private float _oldDpi;

    public SetDocumentDpiCommand(float dpi)
    {
        Id = Guid.NewGuid();
        _newDpi = dpi;
    }

    public void Execute(Document document)
    {
        _oldDpi = document.DefaultDpi;
        document.SetDefaultDpi(_newDpi);
    }

    public void Undo(Document document)
    {
        document.SetDefaultDpi(_oldDpi);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["dpi"] = _newDpi
            }
        };
    }
}

public sealed class SetDocumentUnitsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentUnits";
    public string Description => "Set document units";

    private readonly string _newUnits;
    private string _oldUnits = "px";

    public SetDocumentUnitsCommand(string units)
    {
        Id = Guid.NewGuid();
        _newUnits = units;
    }

    public void Execute(Document document)
    {
        _oldUnits = document.DefaultUnits;
        document.SetDefaultUnits(_newUnits);
    }

    public void Undo(Document document)
    {
        document.SetDefaultUnits(_oldUnits);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["units"] = _newUnits
            }
        };
    }
}

public sealed class SetDocumentDefaultPageSizeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentDefaultPageSize";
    public string Description => "Set default page size";

    private readonly Size2 _newSize;
    private Size2 _oldSize;

    public SetDocumentDefaultPageSizeCommand(Size2 size)
    {
        Id = Guid.NewGuid();
        _newSize = size;
    }

    public void Execute(Document document)
    {
        _oldSize = document.DefaultPageSize;
        document.SetDefaultPageSize(_newSize);
    }

    public void Undo(Document document)
    {
        document.SetDefaultPageSize(_oldSize);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["width"] = _newSize.Width,
                ["height"] = _newSize.Height
            }
        };
    }
}

public sealed class SetDocumentDefaultBackgroundColorCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentDefaultBackgroundColor";
    public string Description => "Set default page background color";

    private readonly Color? _newColor;
    private Color? _oldColor;

    public SetDocumentDefaultBackgroundColorCommand(Color? color)
    {
        Id = Guid.NewGuid();
        _newColor = color;
    }

    public void Execute(Document document)
    {
        _oldColor = document.DefaultPageBackgroundColor;
        document.SetDefaultPageBackgroundColor(_newColor);
    }

    public void Undo(Document document)
    {
        document.SetDefaultPageBackgroundColor(_oldColor);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["color"] = _newColor
            }
        };
    }
}

public sealed class SetDocumentDefaultBackgroundImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetDocumentDefaultBackgroundImage";
    public string Description => "Set default page background image";

    private readonly string? _newPath;
    private string? _oldPath;

    public SetDocumentDefaultBackgroundImageCommand(string? path)
    {
        Id = Guid.NewGuid();
        _newPath = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public void Execute(Document document)
    {
        _oldPath = document.DefaultPageBackgroundImagePath;
        document.SetDefaultPageBackgroundImagePath(_newPath);
    }

    public void Undo(Document document)
    {
        document.SetDefaultPageBackgroundImagePath(_oldPath);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["path"] = _newPath
            }
        };
    }
}

public sealed class SetPageSizeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPageSize";
    public string Description => "Set page size";

    private readonly Guid _pageId;
    private readonly Size2 _newSize;
    private Size2 _oldSize;

    public SetPageSizeCommand(Guid pageId, Size2 size)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newSize = size;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        _oldSize = page.Size;
        page.SetSize(_newSize);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        page.SetSize(_oldSize);
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
                ["width"] = _newSize.Width,
                ["height"] = _newSize.Height
            }
        };
    }
}
