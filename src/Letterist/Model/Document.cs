using System.Collections.Generic;
using System.Linq;

namespace Letterist.Model;

public sealed class Document
{
    public Guid Id { get; }

    public string Name { get; private set; }

    public DateTime Created { get; }

    public DateTime Modified { get; private set; }

    public string DefaultUnits { get; private set; } = "px";

    public float DefaultDpi { get; private set; } = 300f;
    public Size2 DefaultPageSize { get; private set; } = new Size2(1200, 1800);
    public Color? DefaultPageBackgroundColor { get; private set; } = new Color(255, 255, 255, 255);
    public string? DefaultPageBackgroundImagePath { get; private set; }

    public string BaseLanguage { get; private set; } = "en";

    public string ActiveLanguage { get; private set; } = "en";

    public TranslationCompareMode TranslationCompareMode { get; private set; } = TranslationCompareMode.None;

    public string? CompareLanguage { get; private set; }

    public bool HighlightUntranslated { get; private set; } = true;

    private readonly Dictionary<string, bool> _translationLanguageExportVisibility = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, bool> TranslationLanguageExportVisibility => _translationLanguageExportVisibility;

    private readonly Dictionary<string, TranslationLanguageLayout> _translationLanguageLayouts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, TranslationLanguageLayout> TranslationLanguageLayouts => _translationLanguageLayouts;

    private readonly List<Page> _pages = new();

    public IReadOnlyList<Page> Pages => _pages;

    private readonly List<NamedBalloonStyle> _balloonStyles = new();

    private readonly List<NamedTextStyle> _textStyles = new();

    public IReadOnlyList<NamedBalloonStyle> BalloonStyles => _balloonStyles;
    public IReadOnlyList<NamedTextStyle> TextStyles => _textStyles;

    private readonly List<PageTemplate> _pageTemplates = new();

    public IReadOnlyList<PageTemplate> PageTemplates => _pageTemplates;

    private readonly List<PanelLayoutTemplate> _panelTemplates = new();

    public IReadOnlyList<PanelLayoutTemplate> PanelTemplates => _panelTemplates;

    private readonly List<BalloonTemplate> _balloonTemplates = new();

    public IReadOnlyList<BalloonTemplate> BalloonTemplates => _balloonTemplates;

    public Guid ActivePageId { get; private set; }

    public Page? ActivePage => FindPage(ActivePageId);

    public Size2 Size => ActivePage?.Size ?? new Size2(0, 0);

    public string? BackgroundImagePath => ActivePage?.BackgroundImagePath;

    public Layer? BackgroundLayer => ActivePage?.BackgroundLayer;

    public IReadOnlyList<Layer> Layers => ActivePage?.Layers ?? Array.Empty<Layer>();

    public IReadOnlyList<PanelZone> Panels => ActivePage?.Panels ?? Array.Empty<PanelZone>();

    public IReadOnlyList<FloatingImage> FloatingImages => ActivePage?.FloatingImages ?? Array.Empty<FloatingImage>();

    public IEnumerable<Layer> BalloonLayers => ActivePage?.BalloonLayers ?? Enumerable.Empty<Layer>();

    public int BalloonLayerCount => ActivePage?.BalloonLayerCount ?? 0;

    public Guid? SelectedBalloonId => ActivePage?.SelectedBalloonId;

    public Guid ActiveLayerId => ActivePage?.ActiveLayerId ?? Guid.Empty;

    public Guid? LastBalloonLayerId { get; private set; }

    public bool IsDirty { get; private set; }

    public Document(Guid id, string name, Size2 size)
    {
        Id = id;
        Name = name;
        Created = DateTime.UtcNow;
        Modified = Created;
        DefaultPageSize = size;
        BaseLanguage = "en";
        ActiveLanguage = BaseLanguage;
        _translationLanguageExportVisibility[BaseLanguage] = true;

        var defaultPage = Page.Create("Page 1", size);
        _pages.Add(defaultPage);
        ActivePageId = defaultPage.Id;
        LastBalloonLayerId = defaultPage.GetFirstBalloonLayer()?.Id;

        EnsureDefaultStyles();
        EnsureDefaultTemplates();
        RefreshStyleCache();
    }

    internal Document(
        Guid id,
        string name,
        DateTime created,
        DateTime modified,
        IEnumerable<Page> pages,
        Guid activePageId,
        string defaultUnits,
        float defaultDpi,
        Size2? defaultPageSize,
        Color? defaultPageBackgroundColor,
        string? defaultPageBackgroundImagePath,
        IEnumerable<PageTemplate>? pageTemplates = null,
        IEnumerable<PanelLayoutTemplate>? panelTemplates = null,
        IEnumerable<BalloonTemplate>? balloonTemplates = null,
        IEnumerable<NamedBalloonStyle>? balloonStyles = null,
        IEnumerable<NamedTextStyle>? textStyles = null,
        string? baseLanguage = null,
        string? activeLanguage = null,
        TranslationCompareMode translationCompareMode = TranslationCompareMode.None,
        string? compareLanguage = null,
        bool highlightUntranslated = true,
        IReadOnlyDictionary<string, bool>? translationLanguageExportVisibility = null,
        IReadOnlyDictionary<string, TranslationLanguageLayout>? translationLanguageLayouts = null)
    {
        Id = id;
        Name = name;
        Created = created;
        Modified = modified;
        DefaultUnits = defaultUnits;
        DefaultDpi = defaultDpi;
        DefaultPageSize = defaultPageSize ?? new Size2(1200, 1800);
        DefaultPageBackgroundColor = defaultPageBackgroundColor;
        DefaultPageBackgroundImagePath = defaultPageBackgroundImagePath;
        BaseLanguage = NormalizeLanguageTag(baseLanguage, "en");
        ActiveLanguage = NormalizeLanguageTag(activeLanguage, BaseLanguage);
        TranslationCompareMode = translationCompareMode;
        CompareLanguage = string.IsNullOrWhiteSpace(compareLanguage)
            ? null
            : NormalizeLanguageTag(compareLanguage, BaseLanguage);
        HighlightUntranslated = highlightUntranslated;
        _translationLanguageExportVisibility[BaseLanguage] = true;
        if (translationLanguageExportVisibility != null)
        {
            foreach (var pair in translationLanguageExportVisibility)
            {
                var normalized = NormalizeLanguageTag(pair.Key, BaseLanguage);
                _translationLanguageExportVisibility[normalized] = pair.Value;
            }
        }
        _translationLanguageExportVisibility[BaseLanguage] = true;
        if (translationLanguageLayouts != null)
        {
            foreach (var pair in translationLanguageLayouts)
            {
                var normalized = NormalizeLanguageTag(pair.Key, BaseLanguage);
                if (pair.Value == TranslationLanguageLayout.Default)
                {
                    continue;
                }

                _translationLanguageLayouts[normalized] = pair.Value;
            }
        }

        _pages.AddRange(pages);
        if (pageTemplates != null)
        {
            _pageTemplates.AddRange(pageTemplates.Select(template => template.Clone()));
        }
        if (panelTemplates != null)
        {
            _panelTemplates.AddRange(panelTemplates.Select(template => template.Clone()));
        }
        if (balloonTemplates != null)
        {
            _balloonTemplates.AddRange(balloonTemplates.Select(template => template.Clone()));
        }
        if (balloonStyles != null)
        {
            _balloonStyles.AddRange(balloonStyles.Select(style => style.Clone()));
        }
        if (textStyles != null)
        {
            _textStyles.AddRange(textStyles.Select(style => style.Clone()));
        }
        EnsureDefaultStyles();
        EnsureDefaultTemplates();
        RefreshStyleCache();

        ActivePageId = activePageId == Guid.Empty && _pages.Count > 0 ? _pages[0].Id : activePageId;
        if (defaultPageSize == null && ActivePage != null)
        {
            DefaultPageSize = ActivePage.Size;
        }
        LastBalloonLayerId = ActivePage?.GetFirstBalloonLayer()?.Id;
        var activeLayer = ActiveLayer;
        if (activeLayer != null && activeLayer.Kind == LayerKind.Balloon)
        {
            LastBalloonLayerId = activeLayer.Id;
        }
    }

    public static Document Create(string name = "Untitled", Size2? size = null)
    {
        return new Document(Guid.NewGuid(), name, size ?? new Size2(1200, 1800)); // Default US comic page proportions
    }

    public Page? FindPage(Guid pageId)
    {
        return _pages.FirstOrDefault(p => p.Id == pageId);
    }

    public int IndexOfPage(Guid pageId)
    {
        return _pages.FindIndex(p => p.Id == pageId);
    }

    public Layer? FindLayer(Guid layerId)
    {
        return ActivePage?.FindLayer(layerId);
    }

    public Layer? ActiveLayer => ActivePage?.FindLayer(ActiveLayerId);

    public Balloon? FindBalloon(Guid balloonId)
    {
        return ActivePage?.FindBalloon(balloonId);
    }

    public Balloon? FindBalloonAnywhere(Guid balloonId)
    {
        foreach (var page in _pages)
        {
            var balloon = page.FindBalloon(balloonId);
            if (balloon != null)
            {
                return balloon;
            }
        }

        return null;
    }

    public Page? FindPageContainingBalloon(Guid balloonId)
    {
        return _pages.FirstOrDefault(page => page.FindBalloon(balloonId) != null);
    }

    public PanelZone? FindPanel(Guid panelId)
    {
        return ActivePage?.FindPanel(panelId);
    }

    public FloatingImage? FindFloatingImage(Guid imageId)
    {
        return ActivePage?.FindFloatingImage(imageId);
    }

    public Layer? FindLayerContainingBalloon(Guid balloonId)
    {
        return ActivePage?.FindLayerContainingBalloon(balloonId);
    }

    public Balloon? SelectedBalloon => ActivePage?.SelectedBalloon;

    public IEnumerable<Balloon> AllBalloons => ActivePage?.AllBalloons ?? Enumerable.Empty<Balloon>();

    public string GetBalloonDisplayText(Balloon balloon, string? language = null)
    {
        var targetLanguage = NormalizeLanguageTag(language, ActiveLanguage);
        if (string.Equals(targetLanguage, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return balloon.Text;
        }

        if (balloon.Translations.TryGetValue(targetLanguage, out var translation) &&
            !string.IsNullOrWhiteSpace(translation.Text))
        {
            return translation.Text;
        }

        return balloon.Text;
    }

    public string? GetBalloonTranslationText(Balloon balloon, string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return balloon.Text;
        }

        return balloon.Translations.TryGetValue(normalized, out var translation)
            ? translation.Text
            : null;
    }

    public bool IsBalloonUntranslated(Balloon balloon, string? language = null)
    {
        var targetLanguage = NormalizeLanguageTag(language, ActiveLanguage);
        if (string.Equals(targetLanguage, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !balloon.Translations.TryGetValue(targetLanguage, out var translation) ||
               string.IsNullOrWhiteSpace(translation.Text);
    }

    public bool IsBalloonTranslationStale(Balloon balloon, string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!balloon.Translations.TryGetValue(normalized, out var translation))
        {
            return false;
        }

        return !string.Equals(translation.SourceTextSnapshot, balloon.Text, StringComparison.Ordinal);
    }

    public IReadOnlyList<string> GetKnownLanguages()
    {
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            BaseLanguage,
            ActiveLanguage
        };

        if (!string.IsNullOrWhiteSpace(CompareLanguage))
        {
            languages.Add(CompareLanguage);
        }

        foreach (var entry in _translationLanguageExportVisibility.Keys)
        {
            languages.Add(entry);
        }

        foreach (var entry in _translationLanguageLayouts.Keys)
        {
            languages.Add(entry);
        }

        foreach (var page in _pages)
        {
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    foreach (var tag in balloon.Translations.Keys)
                    {
                        languages.Add(tag);
                    }
                }
            }
        }

        return languages
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool IsLanguageVisibleInExport(string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !_translationLanguageExportVisibility.TryGetValue(normalized, out var visible) || visible;
    }

    public TranslationLanguageLayout GetTranslationLanguageLayout(string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        return _translationLanguageLayouts.TryGetValue(normalized, out var layout)
            ? layout
            : TranslationLanguageLayout.Default;
    }

    public TranslationTextDirection ResolveTranslationTextDirection(string language, bool verticalOrientation)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        var layout = GetTranslationLanguageLayout(normalized);
        if (layout.Direction == TranslationTextDirection.Ltr || layout.Direction == TranslationTextDirection.Rtl)
        {
            return layout.Direction;
        }

        if (IsRtlLanguageTag(normalized))
        {
            return TranslationTextDirection.Rtl;
        }

        if (verticalOrientation && IsCjkLanguageTag(normalized))
        {
            return TranslationTextDirection.Rtl;
        }

        return TranslationTextDirection.Ltr;
    }

    public TranslationTextOrientation ResolveBalloonTranslationOrientation(Balloon balloon, string language)
    {
        var normalized = NormalizeLanguageTag(language, ActiveLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return TranslationTextOrientation.Horizontal;
        }

        if (balloon.Translations.TryGetValue(normalized, out var translation) &&
            translation.Orientation != TranslationTextOrientation.Auto)
        {
            return translation.Orientation;
        }

        var languageLayout = GetTranslationLanguageLayout(normalized);
        return languageLayout.Orientation == TranslationTextOrientation.Auto
            ? TranslationTextOrientation.Horizontal
            : languageLayout.Orientation;
    }

    public bool ShouldMirrorTailsForLanguage(string language)
    {
        var normalized = NormalizeLanguageTag(language, ActiveLanguage);
        return GetTranslationLanguageLayout(normalized).MirrorTailsForRtl;
    }

    public IEnumerable<Balloon> VisibleBalloons => ActivePage?.VisibleBalloons ?? Enumerable.Empty<Balloon>();

    public int IndexOfLayer(Guid layerId)
    {
        return ActivePage?.IndexOfLayer(layerId) ?? -1;
    }

    public Layer? GetPreferredBalloonLayer()
    {
        var active = ActiveLayer;
        if (active != null && active.Kind == LayerKind.Balloon)
        {
            return active;
        }

        if (LastBalloonLayerId.HasValue)
        {
            var lastLayer = FindLayer(LastBalloonLayerId.Value);
            if (lastLayer != null && lastLayer.Kind == LayerKind.Balloon)
            {
                return lastLayer;
            }
        }

        return ActivePage?.GetLastBalloonLayer();
    }

    public Guid GetPreferredBalloonLayerId()
    {
        return GetPreferredBalloonLayer()?.Id ?? Guid.Empty;
    }

    public bool IsBackgroundLayer(Guid layerId)
    {
        return ActivePage?.IsBackgroundLayer(layerId) ?? false;
    }

    public Document Clone()
    {
        var clonePages = _pages.Select(p => p.Clone()).ToList();
        var clone = new Document(
            Id,
            Name,
            Created,
            Modified,
            clonePages,
            ActivePageId,
            DefaultUnits,
            DefaultDpi,
            DefaultPageSize,
            DefaultPageBackgroundColor,
            DefaultPageBackgroundImagePath,
            _pageTemplates.Select(template => template.Clone()),
            _panelTemplates.Select(template => template.Clone()),
            _balloonTemplates.Select(template => template.Clone()),
            _balloonStyles.Select(style => style.Clone()),
            _textStyles.Select(style => style.Clone()),
            BaseLanguage,
            ActiveLanguage,
            TranslationCompareMode,
            CompareLanguage,
            HighlightUntranslated,
            new Dictionary<string, bool>(_translationLanguageExportVisibility, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, TranslationLanguageLayout>(_translationLanguageLayouts, StringComparer.OrdinalIgnoreCase))
        {
            IsDirty = IsDirty,
            LastBalloonLayerId = LastBalloonLayerId
        };
        return clone;
    }

    private void EnsureDefaultStyles()
    {
        if (_balloonStyles.Count == 0 || HasLegacySingleDefaultBalloonStyle())
        {
            _balloonStyles.Clear();
            _balloonStyles.AddRange(BalloonStyleCatalog.CreateBuiltInStyles().Select(style => style.Clone()));
        }

        if (_textStyles.Count == 0)
        {
            _textStyles.Add(NamedTextStyle.Create("Default Text", TextStyle.Default));
        }

    }

    private bool HasLegacySingleDefaultBalloonStyle()
    {
        if (_balloonStyles.Count != 1)
        {
            return false;
        }

        var style = _balloonStyles[0];
        return !style.ParentStyleId.HasValue
            && string.Equals(style.Name, "Default Balloon", StringComparison.Ordinal)
            && BalloonStyleUtilities.AreEquivalent(style.Style, BalloonStyle.Default);
    }

    private void EnsureDefaultTemplates()
    {
        if (_pageTemplates.Count == 0)
        {
            _pageTemplates.AddRange(PageTemplate.CreateDefaults());
        }

        EnsureDefaultPanelTemplates();

        if (_balloonTemplates.Count == 0)
        {
            _balloonTemplates.AddRange(BalloonTemplate.CreateDefaults());
        }
    }

    private void EnsureDefaultPanelTemplates()
    {
        var defaultTemplates = PanelLayoutTemplate.CreateDefaults(Size);
        if (_panelTemplates.Count == 0)
        {
            _panelTemplates.AddRange(defaultTemplates);
            return;
        }

        var existingNames = new HashSet<string>(
            _panelTemplates.Select(template => template.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var template in defaultTemplates)
        {
            if (existingNames.Add(template.Name))
            {
                _panelTemplates.Add(template);
            }
        }
    }

    internal void SetName(string name) => Name = name;
    internal void SetSize(Size2 size) => ActivePage?.SetSize(size);
    internal void SetBackgroundImagePath(string? path) => ActivePage?.SetBackgroundImagePath(path);
    internal void SetSelectedBalloonId(Guid? balloonId) => ActivePage?.SetSelectedBalloonId(balloonId);
    internal void SetActiveLayerId(Guid layerId)
    {
        ActivePage?.SetActiveLayerId(layerId);
        var layer = ActivePage?.FindLayer(layerId);
        if (layer != null && layer.Kind == LayerKind.Balloon)
        {
            LastBalloonLayerId = layerId;
        }
    }
    internal void SetActivePageId(Guid pageId)
    {
        ActivePageId = pageId;
        var activeLayer = ActiveLayer;
        if (activeLayer != null && activeLayer.Kind == LayerKind.Balloon)
        {
            LastBalloonLayerId = activeLayer.Id;
        }
        else
        {
            LastBalloonLayerId = ActivePage?.GetFirstBalloonLayer()?.Id;
        }
    }
    internal void SetDefaultUnits(string units) => DefaultUnits = units;
    internal void SetDefaultDpi(float dpi) => DefaultDpi = dpi;
    internal void SetDefaultPageSize(Size2 size) => DefaultPageSize = size;
    internal void SetDefaultPageBackgroundColor(Color? color) => DefaultPageBackgroundColor = color;
    internal void SetDefaultPageBackgroundImagePath(string? path) => DefaultPageBackgroundImagePath = path;
    internal void SetBaseLanguage(string language)
    {
        var oldBaseLanguage = BaseLanguage;
        BaseLanguage = NormalizeLanguageTag(language, "en");
        if (string.IsNullOrWhiteSpace(ActiveLanguage) ||
            string.Equals(ActiveLanguage, oldBaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            ActiveLanguage = BaseLanguage;
        }
        else
        {
            ActiveLanguage = NormalizeLanguageTag(ActiveLanguage, BaseLanguage);
        }
        if (string.IsNullOrWhiteSpace(CompareLanguage))
        {
            CompareLanguage = null;
        }
        else
        {
            CompareLanguage = NormalizeLanguageTag(CompareLanguage, BaseLanguage);
        }

        _translationLanguageExportVisibility[BaseLanguage] = true;
    }
    internal void SetActiveLanguage(string language) => ActiveLanguage = NormalizeLanguageTag(language, BaseLanguage);
    internal void SetTranslationCompareMode(TranslationCompareMode mode) => TranslationCompareMode = mode;
    internal void SetCompareLanguage(string? language)
    {
        CompareLanguage = string.IsNullOrWhiteSpace(language)
            ? null
            : NormalizeLanguageTag(language, BaseLanguage);
    }
    internal void SetHighlightUntranslated(bool enabled) => HighlightUntranslated = enabled;
    internal void SetTranslationLanguageExportVisibility(string language, bool visible)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            _translationLanguageExportVisibility[normalized] = true;
            return;
        }

        _translationLanguageExportVisibility[normalized] = visible;
    }
    internal bool RemoveTranslationLanguageExportVisibility(string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (string.Equals(normalized, BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _translationLanguageExportVisibility.Remove(normalized);
    }
    internal bool TryGetTranslationLanguageExportVisibility(string language, out bool visible)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        return _translationLanguageExportVisibility.TryGetValue(normalized, out visible);
    }
    internal void SetTranslationLanguageExportVisibilityMap(IReadOnlyDictionary<string, bool> map)
    {
        _translationLanguageExportVisibility.Clear();
        foreach (var pair in map)
        {
            var normalized = NormalizeLanguageTag(pair.Key, BaseLanguage);
            _translationLanguageExportVisibility[normalized] = pair.Value;
        }

        _translationLanguageExportVisibility[BaseLanguage] = true;
    }
    internal void SetTranslationLanguageLayout(string language, TranslationLanguageLayout layout)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        if (layout == TranslationLanguageLayout.Default)
        {
            _translationLanguageLayouts.Remove(normalized);
            return;
        }

        _translationLanguageLayouts[normalized] = layout;
    }
    internal bool RemoveTranslationLanguageLayout(string language)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        return _translationLanguageLayouts.Remove(normalized);
    }
    internal bool TryGetTranslationLanguageLayout(string language, out TranslationLanguageLayout layout)
    {
        var normalized = NormalizeLanguageTag(language, BaseLanguage);
        return _translationLanguageLayouts.TryGetValue(normalized, out layout);
    }
    internal void SetTranslationLanguageLayoutMap(IReadOnlyDictionary<string, TranslationLanguageLayout> map)
    {
        _translationLanguageLayouts.Clear();
        foreach (var pair in map)
        {
            var normalized = NormalizeLanguageTag(pair.Key, BaseLanguage);
            if (pair.Value == TranslationLanguageLayout.Default)
            {
                continue;
            }

            _translationLanguageLayouts[normalized] = pair.Value;
        }
    }
    internal void MarkDirty()
    {
        IsDirty = true;
        Modified = DateTime.UtcNow;
    }
    internal void ClearDirty() => IsDirty = false;

    internal void AddPage(Page page)
    {
        _pages.Add(page);
    }

    internal void InsertPage(int index, Page page)
    {
        _pages.Insert(Math.Clamp(index, 0, _pages.Count), page);
    }

    internal bool RemovePage(Guid pageId)
    {
        var index = IndexOfPage(pageId);
        if (index >= 0)
        {
            _pages.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void ReorderPage(Guid pageId, int newIndex)
    {
        var oldIndex = IndexOfPage(pageId);
        if (oldIndex < 0) return;

        var page = _pages[oldIndex];
        _pages.RemoveAt(oldIndex);
        _pages.Insert(Math.Clamp(newIndex, 0, _pages.Count), page);
    }

    internal void AddLayer(Layer layer)
    {
        ActivePage?.AddLayer(layer);
    }

    internal void InsertLayer(int index, Layer layer)
    {
        ActivePage?.InsertLayer(index, layer);
    }

    internal bool RemoveLayer(Guid layerId)
    {
        return ActivePage?.RemoveLayer(layerId) ?? false;
    }

    internal void ReorderLayer(Guid layerId, int newIndex)
    {
        ActivePage?.ReorderLayer(layerId, newIndex);
    }

    internal NamedBalloonStyle? FindBalloonStyle(Guid styleId)
    {
        return _balloonStyles.FirstOrDefault(style => style.Id == styleId);
    }

    internal int IndexOfBalloonStyle(Guid styleId)
    {
        return _balloonStyles.FindIndex(style => style.Id == styleId);
    }

    internal void AddBalloonStyle(NamedBalloonStyle style)
    {
        _balloonStyles.Add(style);
    }

    internal void InsertBalloonStyle(int index, NamedBalloonStyle style)
    {
        _balloonStyles.Insert(Math.Clamp(index, 0, _balloonStyles.Count), style);
    }

    internal bool RemoveBalloonStyle(Guid styleId)
    {
        var index = IndexOfBalloonStyle(styleId);
        if (index < 0) return false;
        _balloonStyles.RemoveAt(index);
        return true;
    }

    internal NamedTextStyle? FindTextStyle(Guid styleId)
    {
        return _textStyles.FirstOrDefault(style => style.Id == styleId);
    }

    internal int IndexOfTextStyle(Guid styleId)
    {
        return _textStyles.FindIndex(style => style.Id == styleId);
    }

    internal void AddTextStyle(NamedTextStyle style)
    {
        _textStyles.Add(style);
    }

    internal void InsertTextStyle(int index, NamedTextStyle style)
    {
        _textStyles.Insert(Math.Clamp(index, 0, _textStyles.Count), style);
    }

    internal bool RemoveTextStyle(Guid styleId)
    {
        var index = IndexOfTextStyle(styleId);
        if (index < 0) return false;
        _textStyles.RemoveAt(index);
        return true;
    }

    internal PageTemplate? FindPageTemplate(Guid templateId)
    {
        return _pageTemplates.FirstOrDefault(template => template.Id == templateId);
    }

    internal int IndexOfPageTemplate(Guid templateId)
    {
        return _pageTemplates.FindIndex(template => template.Id == templateId);
    }

    internal void AddPageTemplate(PageTemplate template)
    {
        _pageTemplates.Add(template);
    }

    internal void InsertPageTemplate(int index, PageTemplate template)
    {
        _pageTemplates.Insert(Math.Clamp(index, 0, _pageTemplates.Count), template);
    }

    internal bool RemovePageTemplate(Guid templateId)
    {
        var index = IndexOfPageTemplate(templateId);
        if (index < 0) return false;
        _pageTemplates.RemoveAt(index);
        return true;
    }

    internal PanelLayoutTemplate? FindPanelTemplate(Guid templateId)
    {
        return _panelTemplates.FirstOrDefault(template => template.Id == templateId);
    }

    internal BalloonTemplate? FindBalloonTemplate(Guid templateId)
    {
        return _balloonTemplates.FirstOrDefault(template => template.Id == templateId);
    }

    internal BalloonStyle ResolveNamedBalloonStyle(Guid? styleId)
    {
        if (!styleId.HasValue) return BalloonStyle.Default;
        var cache = new Dictionary<Guid, BalloonStyle>();
        var visiting = new HashSet<Guid>();
        return ResolveNamedBalloonStyle(styleId.Value, cache, visiting);
    }

    internal TextStyle ResolveNamedTextStyle(Guid? styleId)
    {
        if (!styleId.HasValue) return TextStyle.Default;
        var cache = new Dictionary<Guid, TextStyle>();
        var visiting = new HashSet<Guid>();
        return ResolveNamedTextStyle(styleId.Value, cache, visiting);
    }

    internal void RefreshStyleCache()
    {
        var balloonCache = new Dictionary<Guid, BalloonStyle>();
        var balloonVisiting = new HashSet<Guid>();
        foreach (var style in _balloonStyles)
        {
            var resolved = ResolveNamedBalloonStyle(style.Id, balloonCache, balloonVisiting);
            style.SetStyle(resolved);
        }

        var textCache = new Dictionary<Guid, TextStyle>();
        var textVisiting = new HashSet<Guid>();
        foreach (var style in _textStyles)
        {
            var resolved = ResolveNamedTextStyle(style.Id, textCache, textVisiting);
            style.SetStyle(resolved);
        }

        foreach (var page in _pages)
        {
            foreach (var layer in page.Layers)
            {
                if (layer.Kind != LayerKind.Balloon) continue;
                foreach (var balloon in layer.Balloons)
                {
                    ApplyResolvedStyles(balloon, balloonCache, textCache);
                }
            }
        }
    }

    private BalloonStyle ResolveNamedBalloonStyle(Guid styleId, Dictionary<Guid, BalloonStyle> cache, HashSet<Guid> visiting)
    {
        if (cache.TryGetValue(styleId, out var cached)) return cached;
        if (!visiting.Add(styleId)) return BalloonStyle.Default;

        var style = FindBalloonStyle(styleId);
        if (style == null)
        {
            visiting.Remove(styleId);
            return BalloonStyle.Default;
        }

        var baseStyle = style.ParentStyleId.HasValue
            ? ResolveNamedBalloonStyle(style.ParentStyleId.Value, cache, visiting)
            : BalloonStyle.Default;

        var overrides = style.Overrides ?? BalloonStyleOverride.Empty;
        var resolved = overrides.ApplyTo(baseStyle);
        cache[styleId] = resolved;
        visiting.Remove(styleId);
        return resolved;
    }

    private TextStyle ResolveNamedTextStyle(Guid styleId, Dictionary<Guid, TextStyle> cache, HashSet<Guid> visiting)
    {
        if (cache.TryGetValue(styleId, out var cached)) return cached;
        if (!visiting.Add(styleId)) return TextStyle.Default;

        var style = FindTextStyle(styleId);
        if (style == null)
        {
            visiting.Remove(styleId);
            return TextStyle.Default;
        }

        var baseStyle = style.ParentStyleId.HasValue
            ? ResolveNamedTextStyle(style.ParentStyleId.Value, cache, visiting)
            : TextStyle.Default;

        var overrides = style.Overrides ?? TextStyleOverride.Empty;
        var resolved = overrides.ApplyTo(baseStyle);
        cache[styleId] = resolved;
        visiting.Remove(styleId);
        return resolved;
    }

    private void ApplyResolvedStyles(Balloon balloon, Dictionary<Guid, BalloonStyle> balloonCache, Dictionary<Guid, TextStyle> textCache)
    {
        var balloonBase = balloon.BalloonStyleId.HasValue && balloonCache.TryGetValue(balloon.BalloonStyleId.Value, out var namedBalloon)
            ? namedBalloon
            : BalloonStyle.Default;
        var balloonOverrides = balloon.BalloonStyleOverrides ?? BalloonStyleOverride.FromStyle(balloon.BalloonStyle);
        var resolvedBalloon = balloonOverrides.ApplyTo(balloonBase);
        balloon.SetBalloonStyleReference(balloon.BalloonStyleId, balloonOverrides, resolvedBalloon);

        var textBase = balloon.TextStyleId.HasValue && textCache.TryGetValue(balloon.TextStyleId.Value, out var namedText)
            ? namedText
            : TextStyle.Default;
        var textOverrides = balloon.TextStyleOverrides ?? TextStyleOverride.FromStyle(balloon.TextStyle);
        var resolvedText = textOverrides.ApplyTo(textBase);
        balloon.SetTextStyleReference(balloon.TextStyleId, textOverrides, resolvedText);
    }

    internal int IndexOfPanelTemplate(Guid templateId)
    {
        return _panelTemplates.FindIndex(template => template.Id == templateId);
    }

    internal void AddPanelTemplate(PanelLayoutTemplate template)
    {
        _panelTemplates.Add(template);
    }

    internal void InsertPanelTemplate(int index, PanelLayoutTemplate template)
    {
        _panelTemplates.Insert(Math.Clamp(index, 0, _panelTemplates.Count), template);
    }

    internal bool RemovePanelTemplate(Guid templateId)
    {
        var index = IndexOfPanelTemplate(templateId);
        if (index < 0) return false;
        _panelTemplates.RemoveAt(index);
        return true;
    }

    internal int IndexOfBalloonTemplate(Guid templateId)
    {
        return _balloonTemplates.FindIndex(template => template.Id == templateId);
    }

    internal void AddBalloonTemplate(BalloonTemplate template)
    {
        _balloonTemplates.Add(template);
    }

    internal void InsertBalloonTemplate(int index, BalloonTemplate template)
    {
        _balloonTemplates.Insert(Math.Clamp(index, 0, _balloonTemplates.Count), template);
    }

    internal bool RemoveBalloonTemplate(Guid templateId)
    {
        var index = IndexOfBalloonTemplate(templateId);
        if (index < 0) return false;
        _balloonTemplates.RemoveAt(index);
        return true;
    }

    internal static string NormalizeLanguageTag(string? language, string fallback)
    {
        var normalizedFallback = string.IsNullOrWhiteSpace(fallback)
            ? "en"
            : fallback.Trim().Replace('_', '-');
        if (string.IsNullOrWhiteSpace(language))
        {
            return normalizedFallback;
        }

        return language.Trim().Replace('_', '-');
    }

    internal static bool IsRtlLanguageTag(string? language)
    {
        var primary = GetLanguagePrimarySubtag(language);
        return primary is "ar" or "fa" or "he" or "iw" or "ur" or "yi" or "ps" or "sd" or "ug" or "dv" or "ku";
    }

    internal static bool IsCjkLanguageTag(string? language)
    {
        var primary = GetLanguagePrimarySubtag(language);
        return primary is "ja" or "zh" or "ko";
    }

    private static string GetLanguagePrimarySubtag(string? language)
    {
        var normalized = NormalizeLanguageTag(language, "en");
        var separatorIndex = normalized.IndexOf('-');
        var primary = separatorIndex >= 0 ? normalized.Substring(0, separatorIndex) : normalized;
        return primary.Trim().ToLowerInvariant();
    }
}
