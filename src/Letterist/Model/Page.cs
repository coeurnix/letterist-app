using System.Linq;

namespace Letterist.Model;

public sealed class Page
{
    public Guid Id { get; }

    public string Name { get; private set; }

    public Size2 Size { get; private set; }

    private readonly List<Layer> _layers = new();

    public IReadOnlyList<Layer> Layers => _layers;

    public Layer? BackgroundLayer => _layers.FirstOrDefault(l => l.Kind == LayerKind.Image);

    public string? BackgroundImagePath => BackgroundLayer?.ImagePath;

    public IEnumerable<Layer> BalloonLayers => _layers.Where(l => l.Kind == LayerKind.Balloon);

    public int BalloonLayerCount => _layers.Count(l => l.Kind == LayerKind.Balloon);

    private readonly List<Guide> _guides = new();

    public IReadOnlyList<Guide> Guides => _guides;
    public bool GuidesLocked { get; private set; }

    private readonly List<PanelZone> _panels = new();

    public IReadOnlyList<PanelZone> Panels => _panels;

    private readonly List<FloatingImage> _floatingImages = new();

    public IReadOnlyList<FloatingImage> FloatingImages => _floatingImages;

    private readonly List<ObjectGroup> _objectGroups = new();

    public IReadOnlyList<ObjectGroup> ObjectGroups
    {
        get
        {
            PruneObjectGroups();
            return _objectGroups;
        }
    }

    private readonly List<LayerGroup> _layerGroups = new();

    public IReadOnlyList<LayerGroup> LayerGroups => _layerGroups;

    private readonly List<BalloonLink> _balloonLinks = new();

    public IReadOnlyList<BalloonLink> BalloonLinks => _balloonLinks;

    public BalloonLinkStyle BalloonLinkStyle { get; private set; } = BalloonLinkStyle.Default;

    public OffPanelIndicatorStyle OffPanelIndicatorStyle { get; private set; } = OffPanelIndicatorStyle.Default;

    public Color? BackgroundColor { get; private set; } = new Color(255, 255, 255, 255); // Default white

    public PanelImageFitMode BackgroundImageFitMode { get; private set; } = PanelImageFitMode.Fill;

    public float PanelGutterWidth { get; private set; } = 10f;
    public Color PanelGutterColor { get; private set; } = new(30, 30, 30, 200);
    public PanelBorderStyle PanelGutterStrokeStyle { get; private set; } = PanelBorderStyle.None;
    public bool PanelGutterFillEnabled { get; private set; }

    public ReadingDirection ReadingDirection { get; private set; } = ReadingDirection.LeftToRight;

    public Guid? SelectedBalloonId { get; private set; }

    public Guid ActiveLayerId { get; private set; }

    public Page(Guid id, string name, Size2 size)
    {
        Id = id;
        Name = name;
        Size = size;

        var backgroundLayer = Layer.CreateBackground();
        _layers.Add(backgroundLayer);

        var defaultLayer = Layer.Create("Layer 1");
        _layers.Add(defaultLayer);
        ActiveLayerId = defaultLayer.Id;
    }

    internal Page(
        Guid id,
        string name,
        Size2 size,
        IEnumerable<Layer> layers,
        Guid activeLayerId,
        Guid? selectedBalloonId,
        string? backgroundImagePath,
        IEnumerable<BalloonLink>? balloonLinks = null,
        IEnumerable<Guide>? guides = null,
        IEnumerable<LayerGroup>? layerGroups = null,
        IEnumerable<PanelZone>? panels = null,
        IEnumerable<FloatingImage>? floatingImages = null,
        IEnumerable<ObjectGroup>? objectGroups = null,
        BalloonLinkStyle? linkStyle = null,
        OffPanelIndicatorStyle? offPanelIndicatorStyle = null,
        ReadingDirection readingDirection = ReadingDirection.LeftToRight,
        float panelGutterWidth = 10f,
        Color? panelGutterColor = null,
        PanelBorderStyle panelGutterStrokeStyle = PanelBorderStyle.None,
        bool panelGutterFillEnabled = false,
        Color? backgroundColor = null,
        PanelImageFitMode backgroundImageFitMode = PanelImageFitMode.Fill,
        bool guidesLocked = false)
    {
        Id = id;
        Name = name;
        Size = size;
        _layers.AddRange(layers);
        EnsureBackgroundLayer(backgroundImagePath);
        EnsureBalloonLayer();
        if (balloonLinks != null)
        {
            _balloonLinks.AddRange(balloonLinks);
        }
        if (guides != null)
        {
            _guides.AddRange(guides);
        }
        if (layerGroups != null)
        {
            _layerGroups.AddRange(layerGroups);
        }
        if (panels != null)
        {
            _panels.AddRange(panels.Select(panel => panel.Clone()));
        }
        if (floatingImages != null)
        {
            _floatingImages.AddRange(floatingImages.Select(image => image.Clone()));
        }
        if (objectGroups != null)
        {
            _objectGroups.AddRange(objectGroups.Select(group => group.Clone()));
        }
        ActiveLayerId = ResolveActiveLayerId(activeLayerId);
        AssignFloatingImagesToValidLayers();
        PruneObjectGroups();
        SelectedBalloonId = selectedBalloonId;
        BalloonLinkStyle = linkStyle ?? BalloonLinkStyle.Default;
        OffPanelIndicatorStyle = offPanelIndicatorStyle ?? OffPanelIndicatorStyle.Default;
        ReadingDirection = readingDirection;
        PanelGutterWidth = Math.Max(0f, panelGutterWidth);
        PanelGutterColor = panelGutterColor ?? new Color(30, 30, 30, 200);
        PanelGutterStrokeStyle = panelGutterStrokeStyle;
        PanelGutterFillEnabled = panelGutterFillEnabled;
        BackgroundColor = backgroundColor ?? new Color(255, 255, 255, 255);
        BackgroundImageFitMode = backgroundImageFitMode;
        GuidesLocked = guidesLocked;
    }

    public static Page Create(string name, Size2 size)
    {
        return new Page(Guid.NewGuid(), name, size);
    }

    public Layer? FindLayer(Guid layerId)
    {
        return _layers.FirstOrDefault(l => l.Id == layerId);
    }

    public Balloon? FindBalloon(Guid balloonId)
    {
        foreach (var layer in _layers)
        {
            var balloon = layer.FindBalloon(balloonId);
            if (balloon != null) return balloon;
        }
        return null;
    }

    public Layer? FindLayerContainingBalloon(Guid balloonId)
    {
        return _layers.FirstOrDefault(l => l.ContainsBalloon(balloonId));
    }

    public bool IsBalloonLinked(Guid balloonId)
    {
        return _balloonLinks.Any(l => l.FirstId == balloonId || l.SecondId == balloonId);
    }

    public Balloon? SelectedBalloon => SelectedBalloonId.HasValue ? FindBalloon(SelectedBalloonId.Value) : null;

    public IEnumerable<Balloon> AllBalloons => _layers.SelectMany(l => l.Balloons);
    public int? GetBalloonReadingOrder(Balloon balloon)
    {
        if (balloon.PanelId.HasValue)
        {
            var panel = FindPanel(balloon.PanelId.Value);
            return panel?.Order;
        }

        return null;
    }

    public int? GetBalloonReadingOrder(Guid balloonId)
    {
        var balloon = FindBalloon(balloonId);
        return balloon != null ? GetBalloonReadingOrder(balloon) : null;
    }

    public IEnumerable<Balloon> VisibleBalloons =>
        _layers
            .Where(l => IsLayerEffectivelyVisible(l.Id))
            .SelectMany(l => l.Balloons)
            .Where(b => b.IsVisible && (!b.PanelId.HasValue || FindPanel(b.PanelId.Value)?.IsVisible != false));

    public int IndexOfLayer(Guid layerId)
    {
        return _layers.FindIndex(l => l.Id == layerId);
    }

    public Page Clone()
    {
        var clone = new Page(Id, Name, Size)
        {
            SelectedBalloonId = SelectedBalloonId,
            ActiveLayerId = ActiveLayerId
        };
        clone._layers.Clear();
        foreach (var layer in _layers)
        {
            clone._layers.Add(layer.Clone());
        }
        clone._balloonLinks.Clear();
        foreach (var link in _balloonLinks)
        {
            clone._balloonLinks.Add(link);
        }
        clone.BalloonLinkStyle = BalloonLinkStyle;
        clone.OffPanelIndicatorStyle = OffPanelIndicatorStyle;
        clone.BackgroundColor = BackgroundColor;
        clone.ReadingDirection = ReadingDirection;
        clone.PanelGutterWidth = PanelGutterWidth;
        clone.PanelGutterColor = PanelGutterColor;
        clone.PanelGutterStrokeStyle = PanelGutterStrokeStyle;
        clone.PanelGutterFillEnabled = PanelGutterFillEnabled;
        clone.GuidesLocked = GuidesLocked;
        clone.BackgroundImageFitMode = BackgroundImageFitMode;
        clone._guides.Clear();
        foreach (var guide in _guides)
        {
            clone._guides.Add(guide.Clone());
        }
        clone._layerGroups.Clear();
        foreach (var group in _layerGroups)
        {
            clone._layerGroups.Add(group.Clone());
        }
        clone._panels.Clear();
        foreach (var panel in _panels)
        {
            clone._panels.Add(panel.Clone());
        }
        clone._floatingImages.Clear();
        foreach (var image in _floatingImages)
        {
            clone._floatingImages.Add(image.Clone());
        }
        clone._objectGroups.Clear();
        foreach (var group in _objectGroups)
        {
            clone._objectGroups.Add(group.Clone());
        }
        return clone;
    }

    internal void SetName(string name) => Name = name;
    internal void SetSize(Size2 size) => Size = size;
    internal void SetBackgroundImagePath(string? path)
    {
        var layer = EnsureBackgroundLayer();
        layer.SetImagePath(path);
    }
    internal void SetSelectedBalloonId(Guid? balloonId) => SelectedBalloonId = balloonId;
    internal void SetActiveLayerId(Guid layerId) => ActiveLayerId = layerId;
    internal void SetBalloonLinkStyle(BalloonLinkStyle style) => BalloonLinkStyle = style;
    internal void SetOffPanelIndicatorStyle(OffPanelIndicatorStyle style) => OffPanelIndicatorStyle = style;
    internal void SetPanelGutterWidth(float width) => PanelGutterWidth = Math.Max(0f, width);
    internal void SetPanelGutterColor(Color color) => PanelGutterColor = color;
    internal void SetPanelGutterStrokeStyle(PanelBorderStyle style) => PanelGutterStrokeStyle = style;
    internal void SetPanelGutterFillEnabled(bool enabled) => PanelGutterFillEnabled = enabled;
    internal void SetGuidesLocked(bool locked) => GuidesLocked = locked;
    internal void SetReadingDirection(ReadingDirection direction) => ReadingDirection = direction;
    internal void SetBackgroundColor(Color? color) => BackgroundColor = color;
    internal void SetBackgroundImageFitMode(PanelImageFitMode fitMode) => BackgroundImageFitMode = fitMode;

    internal void AddLayer(Layer layer)
    {
        _layers.Add(layer);
    }

    internal void InsertLayer(int index, Layer layer)
    {
        _layers.Insert(Math.Clamp(index, 0, _layers.Count), layer);
    }

    internal bool RemoveLayer(Guid layerId)
    {
        var index = IndexOfLayer(layerId);
        if (index >= 0)
        {
            _layers.RemoveAt(index);
            ReassignFloatingImagesFromLayer(layerId);
            return true;
        }
        return false;
    }

    internal void ReorderLayer(Guid layerId, int newIndex)
    {
        var oldIndex = IndexOfLayer(layerId);
        if (oldIndex < 0) return;

        var layer = _layers[oldIndex];
        _layers.RemoveAt(oldIndex);
        _layers.Insert(Math.Clamp(newIndex, 0, _layers.Count), layer);
    }

    internal Layer EnsureBackgroundLayer(string? imagePath = null)
    {
        var background = BackgroundLayer;
        if (background == null)
        {
            background = Layer.CreateBackground("Background", imagePath);
            _layers.Insert(0, background);
            return background;
        }

        var currentIndex = _layers.IndexOf(background);
        if (currentIndex > 0)
        {
            _layers.RemoveAt(currentIndex);
            _layers.Insert(0, background);
        }

        if (!string.IsNullOrWhiteSpace(imagePath) && !string.Equals(background.ImagePath, imagePath, StringComparison.Ordinal))
        {
            background.SetImagePath(imagePath);
        }

        return background;
    }

    internal void EnsureBalloonLayer()
    {
        if (_layers.Any(l => l.Kind == LayerKind.Balloon)) return;
        _layers.Add(Layer.Create("Layer 1"));
    }

    internal Guid ResolveActiveLayerId(Guid requestedId)
    {
        if (requestedId != Guid.Empty)
        {
            var layer = FindLayer(requestedId);
            if (layer != null)
            {
                return requestedId;
            }
        }

        return GetFirstBalloonLayer()?.Id ?? Guid.Empty;
    }

    internal Guid GetDefaultFloatingImageLayerId()
    {
        var active = FindLayer(ActiveLayerId);
        if (active != null && active.Kind != LayerKind.Image)
        {
            return active.Id;
        }

        var firstNonImage = _layers.FirstOrDefault(layer => layer.Kind != LayerKind.Image);
        if (firstNonImage != null)
        {
            return firstNonImage.Id;
        }

        return _layers.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    public Layer? GetFirstBalloonLayer()
    {
        return _layers.FirstOrDefault(l => l.Kind == LayerKind.Balloon);
    }

    public Layer? GetLastBalloonLayer()
    {
        return _layers.LastOrDefault(l => l.Kind == LayerKind.Balloon);
    }

    public bool IsBackgroundLayer(Guid layerId)
    {
        return BackgroundLayer?.Id == layerId;
    }

    internal Guide? FindGuide(Guid guideId)
    {
        return _guides.FirstOrDefault(g => g.Id == guideId);
    }

    internal int IndexOfGuide(Guid guideId)
    {
        return _guides.FindIndex(g => g.Id == guideId);
    }

    internal void AddGuide(Guide guide)
    {
        _guides.Add(guide);
    }

    internal void InsertGuide(int index, Guide guide)
    {
        _guides.Insert(Math.Clamp(index, 0, _guides.Count), guide);
    }

    internal bool RemoveGuide(Guid guideId)
    {
        var index = IndexOfGuide(guideId);
        if (index >= 0)
        {
            _guides.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal PanelZone? FindPanel(Guid panelId)
    {
        return _panels.FirstOrDefault(panel => panel.Id == panelId);
    }

    internal int IndexOfPanel(Guid panelId)
    {
        return _panels.FindIndex(panel => panel.Id == panelId);
    }

    internal void AddPanel(PanelZone panel)
    {
        _panels.Add(panel);
    }

    internal void InsertPanel(int index, PanelZone panel)
    {
        _panels.Insert(Math.Clamp(index, 0, _panels.Count), panel);
    }

    internal bool RemovePanel(Guid panelId)
    {
        var index = IndexOfPanel(panelId);
        if (index >= 0)
        {
            _panels.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void ClearPanels()
    {
        _panels.Clear();
    }

    internal void ReorderPanel(Guid panelId, int newIndex)
    {
        var oldIndex = IndexOfPanel(panelId);
        if (oldIndex < 0) return;

        var panel = _panels[oldIndex];
        _panels.RemoveAt(oldIndex);
        _panels.Insert(Math.Clamp(newIndex, 0, _panels.Count), panel);
    }

    internal FloatingImage? FindFloatingImage(Guid imageId)
    {
        return _floatingImages.FirstOrDefault(image => image.Id == imageId);
    }

    internal Layer? FindLayerForFloatingImage(FloatingImage image)
    {
        if (image.LayerId.HasValue)
        {
            var direct = FindLayer(image.LayerId.Value);
            if (direct != null) return direct;
        }

        var fallbackId = GetDefaultFloatingImageLayerId();
        return fallbackId == Guid.Empty ? null : FindLayer(fallbackId);
    }

    internal int IndexOfFloatingImage(Guid imageId)
    {
        return _floatingImages.FindIndex(image => image.Id == imageId);
    }

    internal void AddFloatingImage(FloatingImage image)
    {
        EnsureFloatingImageLayer(image);
        _floatingImages.Add(image);
    }

    internal void InsertFloatingImage(int index, FloatingImage image)
    {
        EnsureFloatingImageLayer(image);
        _floatingImages.Insert(Math.Clamp(index, 0, _floatingImages.Count), image);
    }

    internal bool RemoveFloatingImage(Guid imageId)
    {
        var index = IndexOfFloatingImage(imageId);
        if (index >= 0)
        {
            _floatingImages.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void ReorderFloatingImage(Guid imageId, int newIndex)
    {
        var oldIndex = IndexOfFloatingImage(imageId);
        if (oldIndex < 0) return;

        var image = _floatingImages[oldIndex];
        _floatingImages.RemoveAt(oldIndex);
        _floatingImages.Insert(Math.Clamp(newIndex, 0, _floatingImages.Count), image);
    }

    private void AssignFloatingImagesToValidLayers()
    {
        foreach (var image in _floatingImages)
        {
            EnsureFloatingImageLayer(image);
        }
    }

    private void EnsureFloatingImageLayer(FloatingImage image)
    {
        if (image.LayerId.HasValue && FindLayer(image.LayerId.Value) != null)
        {
            return;
        }

        var fallbackLayerId = GetDefaultFloatingImageLayerId();
        if (fallbackLayerId != Guid.Empty)
        {
            image.SetLayerId(fallbackLayerId);
        }
    }

    private void ReassignFloatingImagesFromLayer(Guid layerId)
    {
        foreach (var image in _floatingImages)
        {
            if (image.LayerId != layerId) continue;
            image.SetLayerId(null);
            EnsureFloatingImageLayer(image);
        }
    }


    internal bool AddBalloonLink(Guid balloonAId, Guid balloonBId)
    {
        if (balloonAId == balloonBId) return false;
        if (FindBalloon(balloonAId) == null || FindBalloon(balloonBId) == null) return false;

        var link = new BalloonLink(balloonAId, balloonBId);
        if (_balloonLinks.Contains(link)) return false;

        _balloonLinks.Add(link);
        return true;
    }

    internal bool RemoveBalloonLink(Guid balloonAId, Guid balloonBId)
    {
        if (balloonAId == balloonBId) return false;
        var link = new BalloonLink(balloonAId, balloonBId);
        return _balloonLinks.Remove(link);
    }

    internal bool AreBalloonsLinked(Guid balloonAId, Guid balloonBId)
    {
        if (balloonAId == balloonBId) return false;
        var link = new BalloonLink(balloonAId, balloonBId);
        return _balloonLinks.Contains(link);
    }

    internal List<BalloonLink> RemoveLinksForBalloon(Guid balloonId)
    {
        var removed = _balloonLinks.Where(link => link.Contains(balloonId)).ToList();
        if (removed.Count > 0)
        {
            _balloonLinks.RemoveAll(link => link.Contains(balloonId));
        }

        return removed;
    }

    internal List<BalloonLink> ClearBalloonLinks()
    {
        if (_balloonLinks.Count == 0) return new List<BalloonLink>();

        var removed = _balloonLinks.ToList();
        _balloonLinks.Clear();
        return removed;
    }

    internal void AddBalloonLinks(IEnumerable<BalloonLink> links)
    {
        foreach (var link in links)
        {
            if (!_balloonLinks.Contains(link))
            {
                _balloonLinks.Add(link);
            }
        }
    }


    public LayerGroup? FindLayerGroup(Guid groupId)
    {
        return _layerGroups.FirstOrDefault(g => g.Id == groupId);
    }

    public ObjectGroup? FindObjectGroup(Guid groupId)
    {
        PruneObjectGroups();
        return _objectGroups.FirstOrDefault(group => group.Id == groupId);
    }

    public ObjectGroup? FindObjectGroupByBalloon(Guid balloonId)
    {
        PruneObjectGroups();
        return _objectGroups.FirstOrDefault(group => group.ContainsBalloon(balloonId));
    }

    public ObjectGroup? FindObjectGroupByFloatingImage(Guid imageId)
    {
        PruneObjectGroups();
        return _objectGroups.FirstOrDefault(group => group.ContainsFloatingImage(imageId));
    }

    public IEnumerable<Layer> GetLayersInGroup(Guid groupId)
    {
        return _layers.Where(l => l.GroupId == groupId);
    }

    public IEnumerable<Layer> GetUngroupedLayers()
    {
        return _layers.Where(l => l.GroupId == null);
    }

    internal int IndexOfLayerGroup(Guid groupId)
    {
        return _layerGroups.FindIndex(g => g.Id == groupId);
    }

    internal void AddLayerGroup(LayerGroup group)
    {
        _layerGroups.Add(group);
    }

    internal void InsertLayerGroup(int index, LayerGroup group)
    {
        _layerGroups.Insert(Math.Clamp(index, 0, _layerGroups.Count), group);
    }

    internal bool RemoveLayerGroup(Guid groupId)
    {
        var index = IndexOfLayerGroup(groupId);
        if (index >= 0)
        {
            foreach (var layer in _layers.Where(l => l.GroupId == groupId))
            {
                layer.SetGroupId(null);
            }
            _layerGroups.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void SetObjectGroups(IEnumerable<ObjectGroup> groups)
    {
        _objectGroups.Clear();
        foreach (var group in groups)
        {
            _objectGroups.Add(group.Clone());
        }
        PruneObjectGroups();
    }

    public bool IsLayerEffectivelyVisible(Guid layerId)
    {
        var layer = FindLayer(layerId);
        if (layer == null) return false;
        if (!layer.IsVisible) return false;

        if (layer.GroupId.HasValue)
        {
            var group = FindLayerGroup(layer.GroupId.Value);
            if (group != null && !group.IsVisible) return false;
        }

        return true;
    }

    public bool IsLayerEffectivelyLocked(Guid layerId)
    {
        var layer = FindLayer(layerId);
        if (layer == null) return false;
        if (layer.IsLocked) return true;

        if (layer.GroupId.HasValue)
        {
            var group = FindLayerGroup(layer.GroupId.Value);
            if (group != null && group.IsLocked) return true;
        }

        return false;
    }

    public bool IsBalloonEffectivelyVisible(Guid balloonId)
    {
        var balloon = FindBalloon(balloonId);
        if (balloon == null) return false;
        if (!balloon.IsVisible) return false;
        if (!IsLayerEffectivelyVisible(balloon.LayerId)) return false;

        if (balloon.PanelId.HasValue)
        {
            var panel = FindPanel(balloon.PanelId.Value);
            if (panel != null && !panel.IsVisible) return false;
        }

        return true;
    }

    public bool IsBalloonEffectivelyLocked(Guid balloonId)
    {
        var balloon = FindBalloon(balloonId);
        if (balloon == null) return false;
        if (balloon.IsLocked) return true;
        if (IsLayerEffectivelyLocked(balloon.LayerId)) return true;

        if (balloon.PanelId.HasValue)
        {
            var panel = FindPanel(balloon.PanelId.Value);
            if (panel != null && panel.IsLocked) return true;
        }

        return false;
    }

    private void PruneObjectGroups()
    {
        if (_objectGroups.Count == 0) return;

        for (var i = _objectGroups.Count - 1; i >= 0; i--)
        {
            var group = _objectGroups[i];
            var validBalloonIds = group.BalloonIds.Where(id => FindBalloon(id) != null).ToList();
            var validImageIds = group.FloatingImageIds.Where(id => FindFloatingImage(id) != null).ToList();

            if (validBalloonIds.Count + validImageIds.Count < 2)
            {
                _objectGroups.RemoveAt(i);
                continue;
            }

            if (validBalloonIds.Count != group.BalloonIds.Count ||
                validImageIds.Count != group.FloatingImageIds.Count)
            {
                _objectGroups[i] = new ObjectGroup(group.Id, validBalloonIds, validImageIds);
            }
        }
    }
}
