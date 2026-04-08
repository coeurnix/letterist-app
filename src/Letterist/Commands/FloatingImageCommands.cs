using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateFloatingImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateFloatingImage";
    public string Description => "Create floating image";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly string? _imagePath;
    private readonly Rect _bounds;
    private readonly float _opacity;
    private readonly bool _isVisible;
    private readonly bool _isLocked;
    private readonly Guid? _layerId;
    private readonly string? _name;
    private readonly string? _source;
    private readonly float _rotation;
    private readonly bool _shadowEnabled;
    private readonly Color _shadowColor;
    private readonly float _shadowOpacity;
    private readonly float _shadowOffsetX;
    private readonly float _shadowOffsetY;
    private readonly float _shadowFalloff;
    private readonly bool _glowEnabled;
    private readonly Color _glowColor;
    private readonly float _glowOpacity;
    private readonly float _glowSize;
    private readonly bool _constrainToPanel;
    private int _insertIndex;

    public Guid CreatedImageId => _imageId;

    public CreateFloatingImageCommand(
        Guid pageId,
        string? imagePath,
        Rect bounds,
        float opacity = 1f,
        bool isVisible = true,
        bool isLocked = false,
        Guid? layerId = null,
        Guid? imageId = null,
        int insertIndex = -1,
        string? name = null,
        string? source = null,
        float rotation = 0f,
        bool shadowEnabled = false,
        Color? shadowColor = null,
        float shadowOpacity = 0.35f,
        float shadowOffsetX = 4f,
        float shadowOffsetY = 4f,
        float shadowFalloff = 8f,
        bool glowEnabled = false,
        Color? glowColor = null,
        float glowOpacity = 0.5f,
        float glowSize = 6f,
        bool constrainToPanel = true)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId ?? Guid.NewGuid();
        _imagePath = imagePath;
        _bounds = bounds;
        _opacity = opacity;
        _isVisible = isVisible;
        _isLocked = isLocked;
        _layerId = layerId;
        _insertIndex = insertIndex;
        _name = name;
        _source = source;
        _rotation = rotation;
        _shadowEnabled = shadowEnabled;
        _shadowColor = shadowColor ?? Color.Black;
        _shadowOpacity = shadowOpacity;
        _shadowOffsetX = shadowOffsetX;
        _shadowOffsetY = shadowOffsetY;
        _shadowFalloff = shadowFalloff;
        _glowEnabled = glowEnabled;
        _glowColor = glowColor ?? Color.Yellow;
        _glowOpacity = glowOpacity;
        _glowSize = glowSize;
        _constrainToPanel = constrainToPanel;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        var image = new FloatingImage(
            _imageId,
            _imagePath,
            _bounds,
            _opacity,
            _isVisible,
            _isLocked,
            _layerId ?? page.GetDefaultFloatingImageLayerId(),
            name: _name,
            source: _source,
            rotation: _rotation,
            shadowEnabled: _shadowEnabled,
            shadowColor: _shadowColor,
            shadowOpacity: _shadowOpacity,
            shadowOffsetX: _shadowOffsetX,
            shadowOffsetY: _shadowOffsetY,
            shadowFalloff: _shadowFalloff,
            glowEnabled: _glowEnabled,
            glowColor: _glowColor,
            glowOpacity: _glowOpacity,
            glowSize: _glowSize,
            constrainToPanel: _constrainToPanel);
        if (_insertIndex < 0 || _insertIndex > page.FloatingImages.Count)
        {
            _insertIndex = page.FloatingImages.Count;
        }

        page.InsertFloatingImage(_insertIndex, image);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.RemoveFloatingImage(_imageId);
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
                ["imageId"] = _imageId,
                ["imagePath"] = _imagePath,
                ["x"] = _bounds.X,
                ["y"] = _bounds.Y,
                ["width"] = _bounds.Width,
                ["height"] = _bounds.Height,
                ["opacity"] = _opacity,
                ["isVisible"] = _isVisible,
                ["isLocked"] = _isLocked,
                ["layerId"] = _layerId,
                ["insertIndex"] = _insertIndex,
                ["name"] = _name,
                ["source"] = _source,
                ["rotation"] = _rotation,
                ["shadowEnabled"] = _shadowEnabled,
                ["shadowColor"] = _shadowColor,
                ["shadowOpacity"] = _shadowOpacity,
                ["shadowOffsetX"] = _shadowOffsetX,
                ["shadowOffsetY"] = _shadowOffsetY,
                ["shadowFalloff"] = _shadowFalloff,
                ["glowEnabled"] = _glowEnabled,
                ["glowColor"] = _glowColor,
                ["glowOpacity"] = _glowOpacity,
                ["glowSize"] = _glowSize,
                ["constrainToPanel"] = _constrainToPanel
            }
        };
    }
}

public sealed class ReorderFloatingImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ReorderFloatingImage";
    public string Description => "Reorder floating image";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly int _newIndex;
    private int _oldIndex = -1;

    public ReorderFloatingImageCommand(Guid pageId, Guid imageId, int newIndex)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newIndex = Math.Max(0, newIndex);
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldIndex = page.IndexOfFloatingImage(_imageId);
        if (_oldIndex < 0)
        {
            throw new InvalidOperationException($"Floating image {_imageId} not found");
        }

        page.ReorderFloatingImage(_imageId, _newIndex);
    }

    public void Undo(Document document)
    {
        if (_oldIndex < 0) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.ReorderFloatingImage(_imageId, _oldIndex);
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
                ["imageId"] = _imageId,
                ["newIndex"] = _newIndex
            }
        };
    }
}

public sealed class SetFloatingImageBoundsCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageBounds";
    public string Description => "Resize floating image";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly Rect _newBounds;
    private Rect? _previousBounds;

    public SetFloatingImageBoundsCommand(Guid pageId, Guid imageId, Rect bounds)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newBounds = bounds;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousBounds = image.Bounds;
        image.SetBounds(_newBounds);
    }

    public void Undo(Document document)
    {
        if (!_previousBounds.HasValue) return;
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetBounds(_previousBounds.Value);
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
                ["imageId"] = _imageId,
                ["x"] = _newBounds.X,
                ["y"] = _newBounds.Y,
                ["width"] = _newBounds.Width,
                ["height"] = _newBounds.Height
            }
        };
    }
}

public sealed class SetFloatingImagePanelCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImagePanel";
    public string Description => "Set floating image panel";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly Guid? _newPanelId;
    private Guid? _previousPanelId;
    private bool _previousConstrainToPanel;

    public SetFloatingImagePanelCommand(Guid pageId, Guid imageId, Guid? panelId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newPanelId = panelId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        if (_newPanelId.HasValue && page.FindPanel(_newPanelId.Value) == null)
        {
            throw new InvalidOperationException($"Panel {_newPanelId.Value} not found on page {_pageId}");
        }

        _previousPanelId = image.PanelId;
        _previousConstrainToPanel = image.ConstrainToPanel;
        image.SetPanelId(_newPanelId);
        if (_newPanelId.HasValue && !_previousPanelId.HasValue)
        {
            image.SetConstrainToPanel(true);
        }
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetPanelId(_previousPanelId);
        image.SetConstrainToPanel(_previousConstrainToPanel);
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
                ["imageId"] = _imageId,
                ["panelId"] = _newPanelId
            }
        };
    }
}

public sealed class SetFloatingImageConstrainToPanelCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageConstrainToPanel";
    public string Description => "Set floating image constrain to panel";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly bool _newValue;
    private bool _previousValue;

    public SetFloatingImageConstrainToPanelCommand(Guid pageId, Guid imageId, bool constrainToPanel)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newValue = constrainToPanel;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousValue = image.ConstrainToPanel;
        image.SetConstrainToPanel(_newValue);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetConstrainToPanel(_previousValue);
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
                ["imageId"] = _imageId,
                ["constrainToPanel"] = _newValue
            }
        };
    }
}

public sealed class SetFloatingImageRotationCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageRotation";
    public string Description => "Set floating image rotation";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly float _newRotation;
    private float? _previousRotation;

    public SetFloatingImageRotationCommand(Guid pageId, Guid imageId, float rotation)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newRotation = rotation;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousRotation = image.Rotation;
        image.SetRotation(_newRotation);
    }

    public void Undo(Document document)
    {
        if (!_previousRotation.HasValue) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");
        image.SetRotation(_previousRotation.Value);
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
                ["imageId"] = _imageId,
                ["rotation"] = _newRotation
            }
        };
    }
}

public sealed class SetFloatingImageShadowCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageShadow";
    public string Description => "Set floating image shadow";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly bool _enabled;
    private readonly Color _color;
    private readonly float _opacity;
    private readonly float _offsetX;
    private readonly float _offsetY;
    private readonly float _falloff;

    private bool _previousEnabled;
    private Color _previousColor;
    private float _previousOpacity;
    private float _previousOffsetX;
    private float _previousOffsetY;
    private float _previousFalloff;
    private bool _capturedPrevious;

    public SetFloatingImageShadowCommand(
        Guid pageId,
        Guid imageId,
        bool enabled,
        Color color,
        float opacity,
        float offsetX,
        float offsetY,
        float falloff)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _enabled = enabled;
        _color = color;
        _opacity = opacity;
        _offsetX = offsetX;
        _offsetY = offsetY;
        _falloff = falloff;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        if (!_capturedPrevious)
        {
            _previousEnabled = image.ShadowEnabled;
            _previousColor = image.ShadowColor;
            _previousOpacity = image.ShadowOpacity;
            _previousOffsetX = image.ShadowOffsetX;
            _previousOffsetY = image.ShadowOffsetY;
            _previousFalloff = image.ShadowFalloff;
            _capturedPrevious = true;
        }

        image.SetShadowStyle(_enabled, _color, _opacity, _offsetX, _offsetY, _falloff);
    }

    public void Undo(Document document)
    {
        if (!_capturedPrevious) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetShadowStyle(
            _previousEnabled,
            _previousColor,
            _previousOpacity,
            _previousOffsetX,
            _previousOffsetY,
            _previousFalloff);
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
                ["imageId"] = _imageId,
                ["enabled"] = _enabled,
                ["color"] = _color,
                ["opacity"] = _opacity,
                ["offsetX"] = _offsetX,
                ["offsetY"] = _offsetY,
                ["falloff"] = _falloff
            }
        };
    }
}

public sealed class SetFloatingImageGlowCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageGlow";
    public string Description => "Set floating image glow";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly bool _enabled;
    private readonly Color _color;
    private readonly float _opacity;
    private readonly float _size;

    private bool _previousEnabled;
    private Color _previousColor;
    private float _previousOpacity;
    private float _previousSize;
    private bool _capturedPrevious;

    public SetFloatingImageGlowCommand(Guid pageId, Guid imageId, bool enabled, Color color, float opacity, float size)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _enabled = enabled;
        _color = color;
        _opacity = opacity;
        _size = size;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        if (!_capturedPrevious)
        {
            _previousEnabled = image.GlowEnabled;
            _previousColor = image.GlowColor;
            _previousOpacity = image.GlowOpacity;
            _previousSize = image.GlowSize;
            _capturedPrevious = true;
        }

        image.SetGlowStyle(_enabled, _color, _opacity, _size);
    }

    public void Undo(Document document)
    {
        if (!_capturedPrevious) return;

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetGlowStyle(_previousEnabled, _previousColor, _previousOpacity, _previousSize);
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
                ["imageId"] = _imageId,
                ["enabled"] = _enabled,
                ["color"] = _color,
                ["opacity"] = _opacity,
                ["size"] = _size
            }
        };
    }
}

public sealed class DeleteFloatingImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteFloatingImage";
    public string Description => "Delete floating image";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private FloatingImage? _removedImage;
    private int _removedIndex = -1;

    public DeleteFloatingImageCommand(Guid pageId, Guid imageId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _removedIndex = page.IndexOfFloatingImage(_imageId);
        _removedImage = image.Clone();
        page.RemoveFloatingImage(_imageId);
    }

    public void Undo(Document document)
    {
        if (_removedImage == null) return;
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var insertIndex = _removedIndex < 0 ? page.FloatingImages.Count : _removedIndex;
        page.InsertFloatingImage(insertIndex, _removedImage);
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
                ["imageId"] = _imageId
            }
        };
    }
}

public sealed class SetFloatingImageOpacityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageOpacity";
    public string Description => "Set floating image opacity";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly float _newOpacity;
    private float? _previousOpacity;

    public SetFloatingImageOpacityCommand(Guid pageId, Guid imageId, float opacity)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newOpacity = opacity;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousOpacity = image.Opacity;
        image.SetOpacity(_newOpacity);
    }

    public void Undo(Document document)
    {
        if (!_previousOpacity.HasValue) return;
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetOpacity(_previousOpacity.Value);
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
                ["imageId"] = _imageId,
                ["opacity"] = _newOpacity
            }
        };
    }
}

public sealed class SetFloatingImageVisibilityCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageVisibility";
    public string Description => "Set floating image visibility";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly bool _newVisibility;
    private bool? _previousVisibility;

    public SetFloatingImageVisibilityCommand(Guid pageId, Guid imageId, bool isVisible)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newVisibility = isVisible;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousVisibility = image.IsVisible;
        image.SetVisible(_newVisibility);
    }

    public void Undo(Document document)
    {
        if (!_previousVisibility.HasValue) return;
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetVisible(_previousVisibility.Value);
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
                ["imageId"] = _imageId,
                ["isVisible"] = _newVisibility
            }
        };
    }
}

public sealed class SetFloatingImageLockedCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetFloatingImageLocked";
    public string Description => "Set floating image locked state";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly bool _newLocked;
    private bool? _previousLocked;

    public SetFloatingImageLockedCommand(Guid pageId, Guid imageId, bool isLocked)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newLocked = isLocked;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousLocked = image.IsLocked;
        image.SetLocked(_newLocked);
    }

    public void Undo(Document document)
    {
        if (!_previousLocked.HasValue) return;
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        image.SetLocked(_previousLocked.Value);
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
                ["imageId"] = _imageId,
                ["isLocked"] = _newLocked
            }
        };
    }
}

public sealed class RenameFloatingImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenameFloatingImage";
    public string Description => "Rename floating image";

    private readonly Guid _pageId;
    private readonly Guid _imageId;
    private readonly string? _newName;
    private string? _previousName;

    public RenameFloatingImageCommand(Guid pageId, Guid imageId, string? name)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _imageId = imageId;
        _newName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");

        _previousName = image.Name;
        image.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");
        var image = page.FindFloatingImage(_imageId)
            ?? throw new InvalidOperationException($"Floating image {_imageId} not found");
        image.SetName(_previousName);
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
                ["imageId"] = _imageId,
                ["name"] = _newName
            }
        };
    }
}
