using Letterist.Model;

namespace Letterist.Commands;

public sealed class LoadBackgroundImageCommand : ICommand
{
    private readonly string _filePath;
    private Size2? _previousSize;
    private string? _previousImagePath;

    public LoadBackgroundImageCommand(string filePath)
    {
        Id = Guid.NewGuid();
        _filePath = filePath;
    }

    public Guid Id { get; }
    public string CommandType => "LoadBackgroundImage";
    public string Description => "Load background image";

    public string FilePath => _filePath;

    public void Execute(Document document)
    {
        _previousSize = document.Size;
        _previousImagePath = document.BackgroundImagePath;

        document.SetBackgroundImagePath(_filePath);
    }

    public void Undo(Document document)
    {
        document.SetBackgroundImagePath(_previousImagePath);
        if (_previousSize.HasValue)
        {
            document.SetSize(_previousSize.Value);
        }
    }

    public CommandData Serialize() => new()
    {
        Id = Id,
        Type = CommandType,
        Parameters = new Dictionary<string, object?>
        {
            ["filePath"] = _filePath
        }
    };
}

public sealed class ClearBackgroundImageCommand : ICommand
{
    private string? _previousImagePath;

    public ClearBackgroundImageCommand()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }
    public string CommandType => "ClearBackgroundImage";
    public string Description => "Clear background image";

    public void Execute(Document document)
    {
        _previousImagePath = document.BackgroundImagePath;
        document.SetBackgroundImagePath(null);
    }

    public void Undo(Document document)
    {
        document.SetBackgroundImagePath(_previousImagePath);
    }

    public CommandData Serialize() => new()
    {
        Id = Id,
        Type = CommandType,
        Parameters = new Dictionary<string, object?>()
    };
}

public sealed class ResizeDocumentCommand : ICommand
{
    private readonly Size2 _newSize;
    private Size2 _previousSize;

    public ResizeDocumentCommand(Size2 newSize)
    {
        Id = Guid.NewGuid();
        _newSize = newSize;
    }

    public Guid Id { get; }
    public string CommandType => "ResizeDocument";
    public string Description => $"Resize document to {_newSize.Width}x{_newSize.Height}";

    public void Execute(Document document)
    {
        _previousSize = document.Size;
        document.SetSize(_newSize);
    }

    public void Undo(Document document)
    {
        document.SetSize(_previousSize);
    }

    public CommandData Serialize() => new()
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
