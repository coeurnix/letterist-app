using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreatePageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreatePage";
    public string Description => "Create page";

    private readonly Guid _pageId;
    private readonly string _name;
    private readonly Size2 _size;
    private readonly int _insertIndex;
    private readonly bool _setActive;
    private Guid _previousActivePageId;

    public Guid CreatedPageId => _pageId;

    public CreatePageCommand(string name, Size2 size, int insertIndex = -1, bool setActive = true, Guid? pageId = null)
    {
        Id = Guid.NewGuid();
        _pageId = pageId ?? Guid.NewGuid();
        _name = name;
        _size = size;
        _insertIndex = insertIndex;
        _setActive = setActive;
    }

    public void Execute(Document document)
    {
        _previousActivePageId = document.ActivePageId;
        var page = new Page(_pageId, _name, _size);
        page.SetBackgroundColor(document.DefaultPageBackgroundColor);
        page.SetBackgroundImagePath(document.DefaultPageBackgroundImagePath);

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
                ["pageId"] = _pageId,
                ["name"] = _name,
                ["width"] = _size.Width,
                ["height"] = _size.Height,
                ["insertIndex"] = _insertIndex,
                ["setActive"] = _setActive
            }
        };
    }
}

public sealed class DeletePageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeletePage";
    public string Description => "Delete page";

    private readonly Guid _pageId;
    private Page? _deletedPage;
    private int _pageIndex;
    private Guid _previousActivePageId;
    private bool _wasActive;

    public DeletePageCommand(Guid pageId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
    }

    public void Execute(Document document)
    {
        if (document.Pages.Count <= 1)
        {
            throw new InvalidOperationException("Cannot delete the last page");
        }

        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _pageIndex = document.IndexOfPage(_pageId);
        _deletedPage = page.Clone();
        _previousActivePageId = document.ActivePageId;
        _wasActive = document.ActivePageId == _pageId;

        document.RemovePage(_pageId);

        if (_wasActive && document.Pages.Count > 0)
        {
            var newIndex = Math.Clamp(_pageIndex, 0, document.Pages.Count - 1);
            document.SetActivePageId(document.Pages[newIndex].Id);
        }
    }

    public void Undo(Document document)
    {
        if (_deletedPage == null)
        {
            throw new InvalidOperationException("Cannot undo - no page was deleted");
        }

        document.InsertPage(_pageIndex, _deletedPage.Clone());

        if (_wasActive)
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
                ["pageId"] = _pageId
            }
        };
    }
}

public sealed class ReorderPageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "ReorderPage";
    public string Description => "Reorder page";

    private readonly Guid _pageId;
    private readonly int _newIndex;
    private int _oldIndex;

    public ReorderPageCommand(Guid pageId, int newIndex)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newIndex = newIndex;
    }

    public void Execute(Document document)
    {
        _oldIndex = document.IndexOfPage(_pageId);
        if (_oldIndex < 0)
        {
            throw new InvalidOperationException($"Page {_pageId} not found");
        }

        document.ReorderPage(_pageId, _newIndex);
    }

    public void Undo(Document document)
    {
        document.ReorderPage(_pageId, _oldIndex);
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
                ["newIndex"] = _newIndex
            }
        };
    }
}

public sealed class RenamePageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "RenamePage";
    public string Description => "Rename page";

    private readonly Guid _pageId;
    private readonly string _newName;
    private string _oldName = "";

    public RenamePageCommand(Guid pageId, string newName)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
                   ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldName = page.Name;
        page.SetName(_newName);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
                   ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetName(_oldName);
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
                ["name"] = _newName
            }
        };
    }
}

public sealed class SetActivePageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetActivePage";
    public string Description => "Set active page";

    private readonly Guid _pageId;
    private Guid _oldActivePageId;

    public SetActivePageCommand(Guid pageId)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
    }

    public void Execute(Document document)
    {
        if (document.FindPage(_pageId) == null)
        {
            throw new InvalidOperationException($"Page {_pageId} not found");
        }

        _oldActivePageId = document.ActivePageId;
        document.SetActivePageId(_pageId);
    }

    public void Undo(Document document)
    {
        if (document.FindPage(_oldActivePageId) != null)
        {
            document.SetActivePageId(_oldActivePageId);
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
                ["pageId"] = _pageId
            }
        };
    }
}

public sealed class SetPageReadingDirectionCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPageReadingDirection";
    public string Description => "Set reading direction";

    private readonly Guid _pageId;
    private readonly ReadingDirection _newDirection;
    private ReadingDirection _oldDirection;

    public SetPageReadingDirectionCommand(Guid pageId, ReadingDirection direction)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newDirection = direction;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldDirection = page.ReadingDirection;
        page.SetReadingDirection(_newDirection);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetReadingDirection(_oldDirection);
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
                ["direction"] = _newDirection.ToString()
            }
        };
    }
}

public sealed class SetPageBackgroundColorCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPageBackgroundColor";
    public string Description => "Set background color";

    private readonly Guid _pageId;
    private readonly Color? _newColor;
    private Color? _oldColor;

    public SetPageBackgroundColorCommand(Guid pageId, Color? color)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newColor = color;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldColor = page.BackgroundColor;
        page.SetBackgroundColor(_newColor);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetBackgroundColor(_oldColor);
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
                ["color"] = _newColor?.ToString()
            }
        };
    }
}

public sealed class SetPageBackgroundImageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPageBackgroundImage";
    public string Description => "Set background image";

    private readonly Guid _pageId;
    private readonly string? _newImagePath;
    private string? _oldImagePath;

    public SetPageBackgroundImageCommand(Guid pageId, string? imagePath)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldImagePath = page.BackgroundImagePath;
        page.SetBackgroundImagePath(_newImagePath);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetBackgroundImagePath(_oldImagePath);
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
                ["imagePath"] = _newImagePath
            }
        };
    }
}

public sealed class SetPageBackgroundImageFitModeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetPageBackgroundImageFitMode";
    public string Description => "Set background image fit mode";

    private readonly Guid _pageId;
    private readonly PanelImageFitMode _newFitMode;
    private PanelImageFitMode _oldFitMode;

    public SetPageBackgroundImageFitModeCommand(Guid pageId, PanelImageFitMode fitMode)
    {
        Id = Guid.NewGuid();
        _pageId = pageId;
        _newFitMode = fitMode;
    }

    public void Execute(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        _oldFitMode = page.BackgroundImageFitMode;
        page.SetBackgroundImageFitMode(_newFitMode);
    }

    public void Undo(Document document)
    {
        var page = document.FindPage(_pageId)
            ?? throw new InvalidOperationException($"Page {_pageId} not found");

        page.SetBackgroundImageFitMode(_oldFitMode);
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
                ["fitMode"] = _newFitMode.ToString()
            }
        };
    }
}

public sealed class DuplicatePageCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DuplicatePage";
    public string Description => "Duplicate page";

    private readonly Guid _sourcePageId;
    private readonly Guid _newPageId;
    private readonly string? _newName;
    private int _insertIndex;
    private Guid _previousActivePageId;

    public Guid CreatedPageId => _newPageId;

    public DuplicatePageCommand(Guid sourcePageId, string? newName = null, Guid? newPageId = null)
    {
        Id = Guid.NewGuid();
        _sourcePageId = sourcePageId;
        _newPageId = newPageId ?? Guid.NewGuid();
        _newName = newName;
    }

    public void Execute(Document document)
    {
        var sourcePage = document.FindPage(_sourcePageId)
            ?? throw new InvalidOperationException($"Source page {_sourcePageId} not found");

        _previousActivePageId = document.ActivePageId;
        _insertIndex = document.IndexOfPage(_sourcePageId) + 1;

        var duplicatedPage = ClonePageWithNewIds(sourcePage);

        if (_insertIndex >= document.Pages.Count)
        {
            document.AddPage(duplicatedPage);
        }
        else
        {
            document.InsertPage(_insertIndex, duplicatedPage);
        }

        document.SetActivePageId(_newPageId);
    }

    private Page ClonePageWithNewIds(Page source)
    {
        var newName = _newName ?? $"{source.Name} (Copy)";
        var newPage = new Page(_newPageId, newName, source.Size);
        var defaultLayerIds = newPage.Layers.Select(layer => layer.Id).ToList();
        foreach (var layerId in defaultLayerIds)
        {
            newPage.RemoveLayer(layerId);
        }

        newPage.SetBackgroundColor(source.BackgroundColor);
        newPage.SetBackgroundImageFitMode(source.BackgroundImageFitMode);

        newPage.SetReadingDirection(source.ReadingDirection);
        newPage.SetPanelGutterWidth(source.PanelGutterWidth);
        newPage.SetPanelGutterColor(source.PanelGutterColor);
        newPage.SetPanelGutterStrokeStyle(source.PanelGutterStrokeStyle);
        newPage.SetPanelGutterFillEnabled(source.PanelGutterFillEnabled);

        var layerIdMap = new Dictionary<Guid, Guid>();
        var balloonIdMap = new Dictionary<Guid, Guid>();
        Guid? newActiveLayerId = null;

        foreach (var sourceLayer in source.Layers)
        {
            var newLayerId = Guid.NewGuid();
            layerIdMap[sourceLayer.Id] = newLayerId;

            var newLayer = new Layer(newLayerId, sourceLayer.Name, sourceLayer.Kind);
            newLayer.SetVisible(sourceLayer.IsVisible);
            newLayer.SetLocked(sourceLayer.IsLocked);
            newLayer.SetOpacity(sourceLayer.Opacity);
            newLayer.SetBlendMode(sourceLayer.BlendMode);

            if (sourceLayer.Kind == LayerKind.Image)
            {
                newLayer.SetImagePath(sourceLayer.ImagePath);
            }

            foreach (var sourceBalloon in sourceLayer.Balloons)
            {
                var clonedBalloon = sourceBalloon.CloneWithNewId();
                clonedBalloon.SetLayerId(newLayerId);
                balloonIdMap[sourceBalloon.Id] = clonedBalloon.Id;
                newLayer.AddBalloon(clonedBalloon);
            }

            newPage.AddLayer(newLayer);

            if (source.ActiveLayerId == sourceLayer.Id)
            {
                newActiveLayerId = newLayerId;
            }
        }

        if (newActiveLayerId.HasValue)
        {
            newPage.SetActiveLayerId(newActiveLayerId.Value);
        }

        foreach (var guide in source.Guides)
        {
            newPage.AddGuide(new Guide(Guid.NewGuid(), guide.Orientation, guide.Position));
        }
        newPage.SetGuidesLocked(source.GuidesLocked);

        var panelIdMap = new Dictionary<Guid, Guid>();
        foreach (var panel in source.Panels)
        {
            var clonedPanel = panel.CloneWithNewId();
            panelIdMap[panel.Id] = clonedPanel.Id;
            newPage.AddPanel(clonedPanel);
        }

        foreach (var layer in newPage.Layers)
        {
            foreach (var balloon in layer.Balloons)
            {
                if (!balloon.PanelId.HasValue)
                {
                    continue;
                }

                if (panelIdMap.TryGetValue(balloon.PanelId.Value, out var remappedPanelId))
                {
                    balloon.SetPanelId(remappedPanelId);
                }
                else
                {
                    balloon.SetPanelId(null);
                }
            }
        }

        foreach (var image in source.FloatingImages)
        {
            var clonedImage = new FloatingImage(
                Guid.NewGuid(),
                image.ImagePath,
                image.Bounds,
                image.Opacity,
                image.IsVisible,
                image.IsLocked,
                image.LayerId.HasValue && layerIdMap.TryGetValue(image.LayerId.Value, out var newLayerId) ? newLayerId : null,
                image.PanelId.HasValue && panelIdMap.TryGetValue(image.PanelId.Value, out var newPanelId) ? newPanelId : null,
                image.Name,
                image.Source,
                image.Rotation,
                image.ShadowEnabled,
                image.ShadowColor,
                image.ShadowOpacity,
                image.ShadowOffsetX,
                image.ShadowOffsetY,
                image.ShadowFalloff,
                image.GlowEnabled,
                image.GlowColor,
                image.GlowOpacity,
                image.GlowSize,
                image.ConstrainToPanel);

            newPage.AddFloatingImage(clonedImage);
        }

        foreach (var link in source.BalloonLinks)
        {
            if (balloonIdMap.TryGetValue(link.FirstId, out var newFirstId) &&
                balloonIdMap.TryGetValue(link.SecondId, out var newSecondId))
            {
                newPage.AddBalloonLink(newFirstId, newSecondId);
            }
        }

        var imageIdMap = new Dictionary<Guid, Guid>();
        foreach (var image in newPage.FloatingImages)
        {
            var sourceImage = source.FloatingImages.FirstOrDefault(i => i.ImagePath == image.ImagePath && i.Bounds == image.Bounds);
            if (sourceImage != null)
            {
                imageIdMap[sourceImage.Id] = image.Id;
            }
        }

        foreach (var group in source.ObjectGroups)
        {
            var newBalloonIds = group.BalloonIds
                .Where(id => balloonIdMap.ContainsKey(id))
                .Select(id => balloonIdMap[id])
                .ToList();

            var newImageIds = group.FloatingImageIds
                .Where(id => imageIdMap.ContainsKey(id))
                .Select(id => imageIdMap[id])
                .ToList();

            if (newBalloonIds.Count > 0 || newImageIds.Count > 0)
            {
                newPage.SetObjectGroups(newPage.ObjectGroups.Concat(new[] {
                    new ObjectGroup(Guid.NewGuid(), newBalloonIds, newImageIds)
                }).ToList());
            }
        }

        var groupIdMap = new Dictionary<Guid, Guid>();
        foreach (var layerGroup in source.LayerGroups)
        {
            var newGroup = new LayerGroup(Guid.NewGuid(), layerGroup.Name);
            newGroup.SetExpanded(layerGroup.IsExpanded);
            newGroup.SetVisible(layerGroup.IsVisible);
            newPage.AddLayerGroup(newGroup);
            groupIdMap[layerGroup.Id] = newGroup.Id;
        }

        foreach (var newLayer in newPage.Layers)
        {
            var sourceLayerEntry = layerIdMap.FirstOrDefault(kv => kv.Value == newLayer.Id);
            if (sourceLayerEntry.Key != Guid.Empty)
            {
                var sourceLayer = source.FindLayer(sourceLayerEntry.Key);
                if (sourceLayer?.GroupId != null && groupIdMap.TryGetValue(sourceLayer.GroupId.Value, out var newGroupId))
                {
                    newLayer.SetGroupId(newGroupId);
                }
            }
        }

        newPage.SetBalloonLinkStyle(source.BalloonLinkStyle);
        newPage.SetOffPanelIndicatorStyle(source.OffPanelIndicatorStyle);

        return newPage;
    }

    public void Undo(Document document)
    {
        document.RemovePage(_newPageId);
        if (document.FindPage(_previousActivePageId) != null)
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
                ["sourcePageId"] = _sourcePageId,
                ["newPageId"] = _newPageId,
                ["newName"] = _newName
            }
        };
    }
}
