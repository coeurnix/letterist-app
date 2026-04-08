using System.Linq;

namespace Letterist.Model;

public sealed class PageTemplate
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public Size2 Size { get; }
    public Guid ActiveLayerId { get; }

    private readonly List<LayerTemplate> _layers = new();
    public IReadOnlyList<LayerTemplate> Layers => _layers;

    private readonly List<Guide> _guides = new();
    public IReadOnlyList<Guide> Guides => _guides;

    private readonly List<LayerGroup> _layerGroups = new();
    public IReadOnlyList<LayerGroup> LayerGroups => _layerGroups;

    public BalloonLinkStyle BalloonLinkStyle { get; private set; }
    public OffPanelIndicatorStyle OffPanelIndicatorStyle { get; private set; }

    public PageTemplate(
        Guid id,
        string name,
        Size2 size,
        IEnumerable<LayerTemplate> layers,
        Guid activeLayerId,
        IEnumerable<Guide>? guides = null,
        IEnumerable<LayerGroup>? layerGroups = null,
        BalloonLinkStyle? linkStyle = null,
        OffPanelIndicatorStyle? offPanelIndicatorStyle = null)
    {
        Id = id;
        Name = name;
        Size = size;
        ActiveLayerId = activeLayerId;
        _layers.AddRange(layers.Select(layer => layer.Clone()));
        if (guides != null)
        {
            _guides.AddRange(guides.Select(guide => guide.Clone()));
        }
        if (layerGroups != null)
        {
            _layerGroups.AddRange(layerGroups.Select(group => group.Clone()));
        }
        BalloonLinkStyle = linkStyle != null ? CloneLinkStyle(linkStyle) : BalloonLinkStyle.Default;
        OffPanelIndicatorStyle = offPanelIndicatorStyle ?? OffPanelIndicatorStyle.Default;
    }

    public static PageTemplate FromPage(Page page, string name, Guid? templateId = null)
    {
        return new PageTemplate(
            templateId ?? Guid.NewGuid(),
            name,
            page.Size,
            page.Layers.Select(LayerTemplate.FromLayer),
            page.ActiveLayerId,
            page.Guides,
            page.LayerGroups,
            page.BalloonLinkStyle,
            page.OffPanelIndicatorStyle);
    }

    public Page CreatePage(string name, Guid? pageId = null)
    {
        var groupMap = new Dictionary<Guid, Guid>();
        var newGroups = new List<LayerGroup>();
        foreach (var group in _layerGroups)
        {
            var newId = Guid.NewGuid();
            groupMap[group.Id] = newId;
            var newGroup = new LayerGroup(newId, group.Name);
            newGroup.SetExpanded(group.IsExpanded);
            newGroup.SetVisible(group.IsVisible);
            newGroup.SetLocked(group.IsLocked);
            newGroups.Add(newGroup);
        }

        var layerMap = new Dictionary<Guid, Guid>();
        var newLayers = new List<Layer>();
        foreach (var templateLayer in _layers)
        {
            var newId = Guid.NewGuid();
            layerMap[templateLayer.Id] = newId;

            var layer = new Layer(newId, templateLayer.Name, templateLayer.Kind);
            layer.SetVisible(templateLayer.IsVisible);
            layer.SetLocked(templateLayer.IsLocked);
            layer.SetOpacity(templateLayer.Opacity);
            layer.SetBlendMode(templateLayer.BlendMode);
            if (templateLayer.GroupId.HasValue &&
                groupMap.TryGetValue(templateLayer.GroupId.Value, out var newGroupId))
            {
                layer.SetGroupId(newGroupId);
            }
            newLayers.Add(layer);
        }

        var newGuides = _guides
            .Select(guide => new Guide(Guid.NewGuid(), guide.Orientation, guide.Position))
            .ToList();

        var activeLayerId = layerMap.TryGetValue(ActiveLayerId, out var mapped)
            ? mapped
            : Guid.Empty;

        return new Page(
            pageId ?? Guid.NewGuid(),
            name,
            Size,
            newLayers,
            activeLayerId,
            selectedBalloonId: null,
            backgroundImagePath: null,
            balloonLinks: null,
            guides: newGuides,
            layerGroups: newGroups,
            panels: null,
            floatingImages: null,
            linkStyle: CloneLinkStyle(BalloonLinkStyle),
            offPanelIndicatorStyle: OffPanelIndicatorStyle);
    }

    public PageTemplate Clone()
    {
        return new PageTemplate(
            Id,
            Name,
            Size,
            _layers.Select(layer => layer.Clone()),
            ActiveLayerId,
            _guides.Select(guide => guide.Clone()),
            _layerGroups.Select(group => group.Clone()),
            CloneLinkStyle(BalloonLinkStyle),
            OffPanelIndicatorStyle);
    }

    public static IReadOnlyList<PageTemplate> CreateDefaults()
    {
        return new List<PageTemplate>
        {
            CreateDefaultTemplate("Default (1200 x 1800)", new Size2(1200, 1800)),
            CreateDefaultTemplate("Wide (1800 x 1200)", new Size2(1800, 1200)),
            CreateDefaultTemplate("Square (1600 x 1600)", new Size2(1600, 1600)),
            CreateDefaultTemplate("Tall (1200 x 2400)", new Size2(1200, 2400))
        };
    }

    private static PageTemplate CreateDefaultTemplate(string name, Size2 size)
    {
        var page = Page.Create("Template", size);
        return FromPage(page, name);
    }

    private static BalloonLinkStyle CloneLinkStyle(BalloonLinkStyle style)
    {
        return new BalloonLinkStyle
        {
            StrokeColor = style.StrokeColor,
            StrokeWidth = style.StrokeWidth,
            DashStyle = style.DashStyle
        };
    }

    internal void SetName(string name) => Name = name;
}

public sealed class LayerTemplate
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public LayerKind Kind { get; }
    public bool IsVisible { get; }
    public bool IsLocked { get; }
    public float Opacity { get; }
    public LayerBlendMode BlendMode { get; }
    public Guid? GroupId { get; }

    public LayerTemplate(Guid id, string name, LayerKind kind, bool isVisible, bool isLocked, float opacity, LayerBlendMode blendMode, Guid? groupId)
    {
        Id = id;
        Name = name;
        Kind = kind;
        IsVisible = isVisible;
        IsLocked = isLocked;
        Opacity = opacity;
        BlendMode = blendMode;
        GroupId = groupId;
    }

    public static LayerTemplate FromLayer(Layer layer)
    {
        return new LayerTemplate(
            layer.Id,
            layer.Name,
            layer.Kind,
            layer.IsVisible,
            layer.IsLocked,
            layer.Opacity,
            layer.BlendMode,
            layer.GroupId);
    }

    public LayerTemplate Clone()
    {
        return new LayerTemplate(Id, Name, Kind, IsVisible, IsLocked, Opacity, BlendMode, GroupId);
    }
}
