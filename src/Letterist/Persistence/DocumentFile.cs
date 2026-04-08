using Letterist.Model;
using System.Linq;
using System.Text.Json.Serialization;

namespace Letterist.Persistence;

internal sealed class DocumentFile
{
    public string Version { get; set; } = "1.2";
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string DefaultUnits { get; set; } = "px";
    public float DefaultDpi { get; set; } = 300f;
    public float? DefaultPageWidth { get; set; }
    public float? DefaultPageHeight { get; set; }
    public bool DefaultPageBackgroundColorSet { get; set; }
    public Color? DefaultPageBackgroundColor { get; set; }
    public bool DefaultPageBackgroundImageSet { get; set; }
    public string? DefaultPageBackgroundImage { get; set; }
    public string BaseLanguage { get; set; } = "en";
    public string ActiveLanguage { get; set; } = "en";
    public TranslationCompareMode TranslationCompareMode { get; set; } = TranslationCompareMode.None;
    public string? CompareLanguage { get; set; }
    public bool HighlightUntranslated { get; set; } = true;
    public Dictionary<string, bool> TranslationLanguageExportVisibility { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TranslationLanguageLayout> TranslationLanguageLayouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Guid ActivePageId { get; set; }
    public List<PageFile> Pages { get; set; } = new();
    public List<PageTemplateFile> PageTemplates { get; set; } = new();
    public List<PanelLayoutTemplateFile> PanelTemplates { get; set; } = new();
    public List<BalloonTemplateFile> BalloonTemplates { get; set; } = new();
    public List<NamedBalloonStyleFile> BalloonStyles { get; set; } = new();
    public List<NamedTextStyleFile> TextStyles { get; set; } = new();

    public static DocumentFile FromDocument(
        Document document,
        IReadOnlyDictionary<Guid, string?> backgroundPaths,
        IReadOnlyDictionary<Guid, string?> panelImagePaths,
        IReadOnlyDictionary<Guid, string?> floatingImagePaths)
    {
        return new DocumentFile
        {
            Id = document.Id,
            Name = document.Name,
            Created = document.Created,
            Modified = document.Modified,
            DefaultUnits = document.DefaultUnits,
            DefaultDpi = document.DefaultDpi,
            DefaultPageWidth = document.DefaultPageSize.Width,
            DefaultPageHeight = document.DefaultPageSize.Height,
            DefaultPageBackgroundColorSet = true,
            DefaultPageBackgroundColor = document.DefaultPageBackgroundColor,
            DefaultPageBackgroundImageSet = true,
            DefaultPageBackgroundImage = document.DefaultPageBackgroundImagePath,
            BaseLanguage = document.BaseLanguage,
            ActiveLanguage = document.ActiveLanguage,
            TranslationCompareMode = document.TranslationCompareMode,
            CompareLanguage = document.CompareLanguage,
            HighlightUntranslated = document.HighlightUntranslated,
            TranslationLanguageExportVisibility = document.TranslationLanguageExportVisibility.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
            TranslationLanguageLayouts = document.TranslationLanguageLayouts.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
            ActivePageId = document.ActivePageId,
            Pages = document.Pages.Select(page =>
                PageFile.FromPage(
                    page,
                    backgroundPaths.TryGetValue(page.Id, out var path) ? path : page.BackgroundImagePath,
                    panelImagePaths,
                    floatingImagePaths)).ToList(),
            PageTemplates = document.PageTemplates.Select(PageTemplateFile.FromTemplate).ToList(),
            PanelTemplates = document.PanelTemplates.Select(PanelLayoutTemplateFile.FromTemplate).ToList(),
            BalloonTemplates = document.BalloonTemplates.Select(BalloonTemplateFile.FromTemplate).ToList(),
            BalloonStyles = document.BalloonStyles.Select(NamedBalloonStyleFile.FromStyle).ToList(),
            TextStyles = document.TextStyles.Select(NamedTextStyleFile.FromStyle).ToList()
        };
    }

    public Document ToDocument()
    {
        var pages = Pages.Select(p => p.ToPage()).ToList();
        if (pages.Count == 0)
        {
            var fallbackSize = DefaultPageWidth.HasValue && DefaultPageHeight.HasValue
                ? new Size2(DefaultPageWidth.Value, DefaultPageHeight.Value)
                : new Size2(1200, 1800);
            return new Document(Id, Name, fallbackSize);
        }

        var defaultPageSize = DefaultPageWidth.HasValue && DefaultPageHeight.HasValue
            ? new Size2(DefaultPageWidth.Value, DefaultPageHeight.Value)
            : (Size2?)null;

        var activePage = pages.FirstOrDefault(p => p.Id == ActivePageId) ?? pages.FirstOrDefault();
        var defaultPageBackgroundColor = DefaultPageBackgroundColorSet
            ? DefaultPageBackgroundColor
            : activePage?.BackgroundColor;
        var defaultPageBackgroundImage = DefaultPageBackgroundImageSet
            ? DefaultPageBackgroundImage
            : activePage?.BackgroundImagePath;

        return new Document(
            Id,
            Name,
            Created,
            Modified,
            pages,
            ActivePageId,
            DefaultUnits,
            DefaultDpi,
            defaultPageSize,
            defaultPageBackgroundColor,
            defaultPageBackgroundImage,
            PageTemplates.Select(t => t.ToTemplate()).ToList(),
            PanelTemplates.Select(t => t.ToTemplate()).ToList(),
            BalloonTemplates.Select(t => t.ToTemplate()).ToList(),
            BalloonStyles.Select(s => s.ToStyle()).ToList(),
            TextStyles.Select(s => s.ToStyle()).ToList(),
            BaseLanguage,
            ActiveLanguage,
            TranslationCompareMode,
            CompareLanguage,
            HighlightUntranslated,
            TranslationLanguageExportVisibility,
            TranslationLanguageLayouts);
    }
}

internal sealed class BalloonTemplateTailFile
{
    public Point2 TargetOffset { get; set; }
    public TailStyle Style { get; set; } = TailStyle.Pointer;
    public float BaseWidth { get; set; } = 16f;
    public Point2? AttachmentDirection { get; set; }
    public Point2? ControlPointOffset { get; set; }
    public float Curvature { get; set; } = 0.3f;
    public float CurveCenter { get; set; } = 0.5f;
    public float Inset { get; set; }

    public static BalloonTemplateTailFile FromTail(BalloonTemplateTail tail)
    {
        return new BalloonTemplateTailFile
        {
            TargetOffset = tail.TargetOffset,
            Style = tail.Style,
            BaseWidth = tail.BaseWidth,
            AttachmentDirection = tail.AttachmentDirection,
            ControlPointOffset = tail.ControlPointOffset,
            Curvature = tail.Curvature,
            CurveCenter = tail.CurveCenter,
            Inset = tail.Inset
        };
    }

    public BalloonTemplateTail ToTail()
    {
        return new BalloonTemplateTail(TargetOffset, Style, BaseWidth, AttachmentDirection, ControlPointOffset, Curvature, CurveCenter, Inset);
    }
}

internal sealed class BalloonTemplateFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public List<string> Tags { get; set; } = new();
    public string PlaceholderText { get; set; } = "Text";
    public BalloonShape Shape { get; set; } = BalloonShape.Oval;
    public string? CustomShapePathData { get; set; }
    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Default;
    public Guid? BalloonStyleId { get; set; }
    public BalloonStyleOverride? BalloonStyleOverrides { get; set; }
    public TextStyle TextStyle { get; set; } = TextStyle.Default;
    public Guid? TextStyleId { get; set; }
    public TextStyleOverride? TextStyleOverrides { get; set; }
    public BalloonTemplateTailFile? Tail { get; set; }
    public bool IsFavorite { get; set; }
    public int? HotkeySlot { get; set; }
    public bool IsBuiltIn { get; set; }

    public static BalloonTemplateFile FromTemplate(BalloonTemplate template)
    {
        return new BalloonTemplateFile
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            Tags = template.Tags.ToList(),
            PlaceholderText = template.PlaceholderText,
            Shape = template.Shape,
            CustomShapePathData = template.CustomShapePathData,
            BalloonStyle = template.BalloonStyle,
            BalloonStyleId = template.BalloonStyleId,
            BalloonStyleOverrides = template.BalloonStyleOverrides.Clone(),
            TextStyle = template.TextStyle,
            TextStyleId = template.TextStyleId,
            TextStyleOverrides = template.TextStyleOverrides.Clone(),
            Tail = template.Tail != null ? BalloonTemplateTailFile.FromTail(template.Tail) : null,
            IsFavorite = template.IsFavorite,
            HotkeySlot = template.HotkeySlot,
            IsBuiltIn = template.IsBuiltIn
        };
    }

    public BalloonTemplate ToTemplate()
    {
        return new BalloonTemplate(
            Id,
            Name,
            Shape,
            BalloonStyle,
            TextStyle,
            PlaceholderText,
            Tail?.ToTail(),
            CustomShapePathData,
            BalloonStyleId,
            BalloonStyleOverrides,
            TextStyleId,
            TextStyleOverrides,
            Description,
            Tags,
            Category,
            IsFavorite,
            HotkeySlot,
            IsBuiltIn);
    }
}

internal sealed class PageFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Size2 Size { get; set; }
    public string? BackgroundImage { get; set; }
    public Guid ActiveLayerId { get; set; }
    public Guid? SelectedBalloonId { get; set; }
    public List<LayerFile> Layers { get; set; } = new();
    public List<BalloonLinkFile> BalloonLinks { get; set; } = new();
    public BalloonLinkStyle LinkStyle { get; set; } = BalloonLinkStyle.Default;
    public OffPanelIndicatorStyle OffPanelIndicatorStyle { get; set; } = OffPanelIndicatorStyle.Default;
    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;
    public float PanelGutterWidth { get; set; } = 10f;
    public Color PanelGutterColor { get; set; } = new(30, 30, 30, 200);
    public PanelBorderStyle PanelGutterStrokeStyle { get; set; } = PanelBorderStyle.None;
    public bool PanelGutterFillEnabled { get; set; }
    public Color? BackgroundColor { get; set; } = new Color(255, 255, 255, 255);
    public PanelImageFitMode BackgroundImageFitMode { get; set; } = PanelImageFitMode.Fill;
    public bool GuidesLocked { get; set; }
    public List<GuideFile> Guides { get; set; } = new();
    public List<LayerGroupFile> LayerGroups { get; set; } = new();
    public List<PanelZoneFile> Panels { get; set; } = new();
    public List<FloatingImageFile> FloatingImages { get; set; } = new();
    public List<ObjectGroupFile> ObjectGroups { get; set; } = new();

    public static PageFile FromPage(
        Page page,
        string? backgroundPath,
        IReadOnlyDictionary<Guid, string?>? panelImagePaths = null,
        IReadOnlyDictionary<Guid, string?>? floatingImagePaths = null)
    {
        var layers = page.Layers.Select(layer =>
        {
            var layerFile = LayerFile.FromLayer(layer);
            if (layer.Kind == LayerKind.Image && !string.IsNullOrWhiteSpace(backgroundPath))
            {
                layerFile.ImagePath = backgroundPath;
            }

            return layerFile;
        }).ToList();

        return new PageFile
        {
            Id = page.Id,
            Name = page.Name,
            Size = page.Size,
            BackgroundImage = backgroundPath,
            ActiveLayerId = page.ActiveLayerId,
            SelectedBalloonId = page.SelectedBalloonId,
            Layers = layers,
            BalloonLinks = page.BalloonLinks.Select(BalloonLinkFile.FromLink).ToList(),
            LinkStyle = page.BalloonLinkStyle,
            OffPanelIndicatorStyle = page.OffPanelIndicatorStyle,
            ReadingDirection = page.ReadingDirection,
            PanelGutterWidth = page.PanelGutterWidth,
            PanelGutterColor = page.PanelGutterColor,
            PanelGutterStrokeStyle = page.PanelGutterStrokeStyle,
            PanelGutterFillEnabled = page.PanelGutterFillEnabled,
            BackgroundColor = page.BackgroundColor,
            BackgroundImageFitMode = page.BackgroundImageFitMode,
            GuidesLocked = page.GuidesLocked,
            Guides = page.Guides.Select(GuideFile.FromGuide).ToList(),
            LayerGroups = page.LayerGroups.Select(LayerGroupFile.FromLayerGroup).ToList(),
            Panels = page.Panels.Select(panel =>
            {
                var imagePath = panelImagePaths != null && panelImagePaths.TryGetValue(panel.Id, out var path)
                    ? path
                    : panel.ImagePath;
                return PanelZoneFile.FromPanel(panel, imagePath);
            }).ToList(),
            FloatingImages = page.FloatingImages.Select(image =>
            {
                var imagePath = floatingImagePaths != null && floatingImagePaths.TryGetValue(image.Id, out var path)
                    ? path
                    : image.ImagePath;
                return FloatingImageFile.FromImage(image, imagePath);
            }).ToList(),
            ObjectGroups = page.ObjectGroups.Select(ObjectGroupFile.FromObjectGroup).ToList()
        };
    }

    public Page ToPage()
    {
        var layers = Layers.Select(l => l.ToLayer()).ToList();
        var links = BalloonLinks.Select(l => l.ToLink()).ToList();
        var guides = Guides.Select(g => g.ToGuide()).ToList();
        var layerGroups = LayerGroups.Select(g => g.ToLayerGroup()).ToList();
        var panels = Panels.Select(p => p.ToPanel()).ToList();
        var floatingImages = FloatingImages.Select(i => i.ToImage()).ToList();
        var objectGroups = ObjectGroups.Select(g => g.ToObjectGroup()).ToList();
        return new Page(
            Id,
            Name,
            Size,
            layers,
            ActiveLayerId,
            SelectedBalloonId,
            BackgroundImage,
            links,
            guides,
            layerGroups,
            panels,
            floatingImages,
            objectGroups,
            LinkStyle,
            OffPanelIndicatorStyle,
            ReadingDirection,
            PanelGutterWidth,
            PanelGutterColor,
            PanelGutterStrokeStyle,
            PanelGutterFillEnabled,
            BackgroundColor,
            BackgroundImageFitMode,
            GuidesLocked);
    }
}

internal sealed class LayerFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public LayerKind Kind { get; set; } = LayerKind.Balloon;
    public string? ImagePath { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public float Opacity { get; set; }
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;
    public Guid? GroupId { get; set; }
    public List<BalloonFile> Balloons { get; set; } = new();
    [JsonPropertyName("Sfx")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LegacyTextOnlyBalloonFile>? LegacyTextOnlyBalloons { get; set; }

    public static LayerFile FromLayer(Layer layer)
    {
        return new LayerFile
        {
            Id = layer.Id,
            Name = layer.Name,
            Kind = layer.Kind,
            ImagePath = layer.ImagePath,
            IsVisible = layer.IsVisible,
            IsLocked = layer.IsLocked,
            Opacity = layer.Opacity,
            BlendMode = layer.BlendMode,
            GroupId = layer.GroupId,
            Balloons = layer.Balloons.Select(BalloonFile.FromBalloon).ToList()
        };
    }

    public Layer ToLayer()
    {
        var layer = new Layer(Id, Name, Kind, ImagePath);
        layer.SetVisible(IsVisible);
        layer.SetLocked(IsLocked);
        layer.SetOpacity(Opacity);
        layer.SetBlendMode(BlendMode);
        layer.SetGroupId(GroupId);
        foreach (var balloon in Balloons)
        {
            layer.AddBalloon(balloon.ToBalloon());
        }
        if (LegacyTextOnlyBalloons != null)
        {
            foreach (var legacyTextOnlyBalloon in LegacyTextOnlyBalloons)
            {
                layer.AddBalloon(legacyTextOnlyBalloon.ToBalloon(layer.Id));
            }
        }
        return layer;
    }
}

internal sealed class LegacyTextOnlyBalloonFile
{
    public Guid Id { get; set; }
    public Guid LayerId { get; set; }
    public Guid? PanelId { get; set; }
    public Point2 Position { get; set; }
    public float Rotation { get; set; }
    public string Text { get; set; } = "Text";
    public TextStyle TextStyle { get; set; } = TextStyle.TextOnlyDefault;
    public TextPath? TextPath { get; set; }

    public static LegacyTextOnlyBalloonFile FromBalloon(Balloon balloon)
    {
        return new LegacyTextOnlyBalloonFile
        {
            Id = balloon.Id,
            LayerId = balloon.LayerId,
            PanelId = balloon.PanelId,
            Position = balloon.Position,
            Rotation = balloon.Rotation,
            Text = balloon.Text,
            TextStyle = balloon.TextStyle,
            TextPath = balloon.TextPath?.Clone()
        };
    }

    public Balloon ToBalloon(Guid layerId)
    {
        var balloon = new Balloon(
            Id,
            layerId,
            Position,
            BalloonShape.None,
            BalloonStyle.Default,
            Text,
            TextStyle,
            panelId: PanelId,
            constrainToPanel: false,
            textPath: TextPath);
        balloon.SetRotation(Rotation);
        return balloon;
    }
}

internal sealed class BalloonFile
{
    public Guid Id { get; set; }
    public Guid LayerId { get; set; }
    public Guid? PanelId { get; set; }
    public bool ConstrainToPanel { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public Point2 Position { get; set; }
    public BalloonShape Shape { get; set; }
    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Default;
    public Guid? BalloonStyleId { get; set; }
    public BalloonStyleOverride? BalloonStyleOverrides { get; set; }
    public string Text { get; set; } = "";
    public TextStyle TextStyle { get; set; } = TextStyle.Default;
    public Guid? TextStyleId { get; set; }
    public TextStyleOverride? TextStyleOverrides { get; set; }
    public List<TextStyleSpan> TextStyleSpans { get; set; } = new();
    public List<BalloonTranslationFile> Translations { get; set; } = new();
    public string? CustomShapePathData { get; set; }
    public TailFile? Tail { get; set; }
    public Size2 ComputedSize { get; set; }
    public float? MaxTextWidth { get; set; }
    public float? MaxTextHeight { get; set; }
    public float Rotation { get; set; }
    public TextPath? TextPath { get; set; }

    public static BalloonFile FromBalloon(Balloon balloon)
    {
        return new BalloonFile
        {
            Id = balloon.Id,
            LayerId = balloon.LayerId,
            PanelId = balloon.PanelId,
            ConstrainToPanel = balloon.ConstrainToPanel,
            IsVisible = balloon.IsVisible,
            IsLocked = balloon.IsLocked,
            Position = balloon.Position,
            Shape = balloon.Shape,
            BalloonStyle = balloon.BalloonStyle,
            BalloonStyleId = balloon.BalloonStyleId,
            BalloonStyleOverrides = balloon.BalloonStyleOverrides.Clone(),
            Text = balloon.Text,
            TextStyle = balloon.TextStyle,
            TextStyleId = balloon.TextStyleId,
            TextStyleOverrides = balloon.TextStyleOverrides.Clone(),
            TextStyleSpans = balloon.TextStyleSpans.Select(span => span.Clone()).ToList(),
            Translations = balloon.Translations.Select(pair => new BalloonTranslationFile
            {
                Language = pair.Key,
                Text = pair.Value.Text,
                SourceTextSnapshot = pair.Value.SourceTextSnapshot,
                UpdatedUtc = pair.Value.UpdatedUtc,
                Orientation = pair.Value.Orientation
            }).ToList(),
            CustomShapePathData = balloon.CustomShapePathData,
            Tail = balloon.Tail != null ? TailFile.FromTail(balloon.Tail) : null,
            ComputedSize = balloon.ComputedSize,
            MaxTextWidth = balloon.MaxTextWidth,
            MaxTextHeight = balloon.MaxTextHeight,
            Rotation = balloon.Rotation,
            TextPath = balloon.TextPath?.Clone()
        };
    }

    public Balloon ToBalloon()
    {
        var translationMap = Translations
            .Where(t => !string.IsNullOrWhiteSpace(t.Language))
            .ToDictionary(
                t => t.Language.Trim(),
                t => new BalloonTranslation(
                    t.Text,
                    t.SourceTextSnapshot ?? string.Empty,
                    t.UpdatedUtc,
                    t.Orientation),
                StringComparer.OrdinalIgnoreCase);

        var balloon = new Balloon(
            Id,
            LayerId,
            Position,
            Shape,
            BalloonStyle,
            Text,
            TextStyle,
            TextStyleSpans,
            CustomShapePathData,
            PanelId,
            ConstrainToPanel,
            isVisible: IsVisible,
            isLocked: IsLocked,
            translations: translationMap);
        var balloonOverrides = BalloonStyleOverrides ?? BalloonStyleOverride.FromStyle(BalloonStyle);
        balloon.SetBalloonStyleReference(BalloonStyleId, balloonOverrides, BalloonStyle);
        var textOverrides = TextStyleOverrides ?? TextStyleOverride.FromStyle(TextStyle);
        balloon.SetTextStyleReference(TextStyleId, textOverrides, TextStyle);
        balloon.SetComputedSize(ComputedSize);
        balloon.SetMaxTextWidth(MaxTextWidth);
        balloon.SetMaxTextHeight(MaxTextHeight);
        balloon.SetRotation(Rotation);
        balloon.SetTextPath(TextPath);
        if (Tail != null)
        {
            balloon.SetTail(Tail.ToTail());
        }
        return balloon;
    }
}

internal sealed class BalloonTranslationFile
{
    public string Language { get; set; } = "";
    public string Text { get; set; } = "";
    public string? SourceTextSnapshot { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public TranslationTextOrientation Orientation { get; set; } = TranslationTextOrientation.Auto;
}

internal sealed class TailFile
{
    public Guid Id { get; set; }
    public Point2 TargetPoint { get; set; }
    public TailStyle Style { get; set; }
    public float BaseWidth { get; set; }
    public Point2? AttachmentDirection { get; set; }
    public Point2? ControlPoint { get; set; }
    public float Curvature { get; set; } = 0.3f;
    public float CurveCenter { get; set; } = 0.5f;
    public float Inset { get; set; }

    public static TailFile FromTail(Tail tail)
    {
        return new TailFile
        {
            Id = tail.Id,
            TargetPoint = tail.TargetPoint,
            Style = tail.Style,
            BaseWidth = tail.BaseWidth,
            AttachmentDirection = tail.AttachmentDirection,
            ControlPoint = tail.ControlPoint,
            Curvature = tail.Curvature,
            CurveCenter = tail.CurveCenter,
            Inset = tail.Inset
        };
    }

    public Tail ToTail()
    {
        var tail = new Tail(Id, TargetPoint, Style, BaseWidth);
        tail.SetAttachmentDirection(AttachmentDirection);
        tail.SetControlPoint(ControlPoint);
        tail.SetCurvature(Curvature);
        tail.SetCurveCenter(CurveCenter);
        tail.SetInset(Inset);
        return tail;
    }
}

internal sealed class BalloonLinkFile
{
    public Guid BalloonAId { get; set; }
    public Guid BalloonBId { get; set; }

    public static BalloonLinkFile FromLink(BalloonLink link)
    {
        return new BalloonLinkFile
        {
            BalloonAId = link.FirstId,
            BalloonBId = link.SecondId
        };
    }

    public BalloonLink ToLink()
    {
        return new BalloonLink(BalloonAId, BalloonBId);
    }
}

internal sealed class GuideFile
{
    public Guid Id { get; set; }
    public GuideOrientation Orientation { get; set; }
    public float Position { get; set; }

    public static GuideFile FromGuide(Guide guide)
    {
        return new GuideFile
        {
            Id = guide.Id,
            Orientation = guide.Orientation,
            Position = guide.Position
        };
    }

    public Guide ToGuide()
    {
        return new Guide(Id, Orientation, Position);
    }
}

internal sealed class LayerGroupFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsExpanded { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }

    public static LayerGroupFile FromLayerGroup(LayerGroup group)
    {
        return new LayerGroupFile
        {
            Id = group.Id,
            Name = group.Name,
            IsExpanded = group.IsExpanded,
            IsVisible = group.IsVisible,
            IsLocked = group.IsLocked
        };
    }

    public LayerGroup ToLayerGroup()
    {
        var group = new LayerGroup(Id, Name);
        group.SetExpanded(IsExpanded);
        group.SetVisible(IsVisible);
        group.SetLocked(IsLocked);
        return group;
    }
}

internal sealed class ObjectGroupFile
{
    public Guid Id { get; set; }
    public List<Guid> BalloonIds { get; set; } = new();
    public List<Guid> FloatingImageIds { get; set; } = new();

    public static ObjectGroupFile FromObjectGroup(ObjectGroup group)
    {
        return new ObjectGroupFile
        {
            Id = group.Id,
            BalloonIds = group.BalloonIds.ToList(),
            FloatingImageIds = group.FloatingImageIds.ToList()
        };
    }

    public ObjectGroup ToObjectGroup()
    {
        return new ObjectGroup(Id, BalloonIds, FloatingImageIds);
    }
}

internal sealed class PanelZoneFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Rect Bounds { get; set; }
    public int Order { get; set; }
    public Color Color { get; set; } = PanelZone.DefaultColor;
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public PanelShape Shape { get; set; } = PanelShape.Rectangle;
    public float CornerRadius { get; set; }
    public float SafeMargin { get; set; }
    public string? CustomShapePathData { get; set; }
    public string? ImagePath { get; set; }
    public PanelImagePlacement? ImagePlacement { get; set; }
    public bool ImageVisibleInExport { get; set; } = true;
    public Color BorderColor { get; set; } = PanelZone.DefaultBorderColor;
    public float BorderWidth { get; set; } = PanelZone.DefaultBorderWidth;
    public PanelBorderStyle BorderStyle { get; set; } = PanelBorderStyle.Solid;
    public float? GutterLeftOverride { get; set; }
    public float? GutterTopOverride { get; set; }
    public float? GutterRightOverride { get; set; }
    public float? GutterBottomOverride { get; set; }
    public float BleedLeft { get; set; }
    public float BleedTop { get; set; }
    public float BleedRight { get; set; }
    public float BleedBottom { get; set; }

    public static PanelZoneFile FromPanel(PanelZone panel, string? imagePathOverride = null)
    {
        return new PanelZoneFile
        {
            Id = panel.Id,
            Name = panel.Name,
            Bounds = panel.Bounds,
            Order = panel.Order,
            Color = panel.Color,
            IsVisible = panel.IsVisible,
            IsLocked = panel.IsLocked,
            Shape = panel.Shape,
            CornerRadius = panel.CornerRadius,
            SafeMargin = panel.SafeMargin,
            CustomShapePathData = panel.CustomShapePathData,
            ImagePath = imagePathOverride ?? panel.ImagePath,
            ImagePlacement = panel.ImagePlacement,
            ImageVisibleInExport = panel.IsImageVisibleInExport,
            BorderColor = panel.BorderColor,
            BorderWidth = panel.BorderWidth,
            BorderStyle = panel.BorderStyle,
            GutterLeftOverride = panel.GutterLeftOverride,
            GutterTopOverride = panel.GutterTopOverride,
            GutterRightOverride = panel.GutterRightOverride,
            GutterBottomOverride = panel.GutterBottomOverride,
            BleedLeft = panel.BleedLeft,
            BleedTop = panel.BleedTop,
            BleedRight = panel.BleedRight,
            BleedBottom = panel.BleedBottom
        };
    }

    public PanelZone ToPanel()
    {
        return new PanelZone(
            Id,
            Name,
            Bounds,
            Order,
            Color,
            IsVisible,
            IsLocked,
            Shape,
            CornerRadius,
            SafeMargin,
            CustomShapePathData,
            ImagePath,
            ImagePlacement,
            BorderColor,
            BorderWidth,
            BorderStyle,
            ImageVisibleInExport,
            GutterLeftOverride,
            GutterTopOverride,
            GutterRightOverride,
            GutterBottomOverride,
            BleedLeft,
            BleedTop,
            BleedRight,
            BleedBottom);
    }
}

internal sealed class FloatingImageFile
{
    public Guid Id { get; set; }
    public Guid? LayerId { get; set; }
    public Guid? PanelId { get; set; }
    public bool ConstrainToPanel { get; set; } = true;
    public string? Name { get; set; }
    public string? Source { get; set; }
    public Rect Bounds { get; set; }
    public string? ImagePath { get; set; }
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool ShadowEnabled { get; set; }
    public Color ShadowColor { get; set; } = Color.Black;
    public float ShadowOpacity { get; set; } = 0.35f;
    public float ShadowOffsetX { get; set; } = 4f;
    public float ShadowOffsetY { get; set; } = 4f;
    public float ShadowFalloff { get; set; } = 8f;
    public bool GlowEnabled { get; set; }
    public Color GlowColor { get; set; } = Color.Yellow;
    public float GlowOpacity { get; set; } = 0.5f;
    public float GlowSize { get; set; } = 6f;

    public static FloatingImageFile FromImage(FloatingImage image, string? imagePathOverride = null)
    {
        return new FloatingImageFile
        {
            Id = image.Id,
            LayerId = image.LayerId,
            PanelId = image.PanelId,
            ConstrainToPanel = image.ConstrainToPanel,
            Name = image.Name,
            Source = image.Source,
            Bounds = image.Bounds,
            ImagePath = imagePathOverride ?? image.ImagePath,
            Rotation = image.Rotation,
            Opacity = image.Opacity,
            IsVisible = image.IsVisible,
            IsLocked = image.IsLocked,
            ShadowEnabled = image.ShadowEnabled,
            ShadowColor = image.ShadowColor,
            ShadowOpacity = image.ShadowOpacity,
            ShadowOffsetX = image.ShadowOffsetX,
            ShadowOffsetY = image.ShadowOffsetY,
            ShadowFalloff = image.ShadowFalloff,
            GlowEnabled = image.GlowEnabled,
            GlowColor = image.GlowColor,
            GlowOpacity = image.GlowOpacity,
            GlowSize = image.GlowSize
        };
    }

    public FloatingImage ToImage()
    {
        return new FloatingImage(
            Id,
            ImagePath,
            Bounds,
            Opacity,
            IsVisible,
            IsLocked,
            LayerId,
            PanelId,
            Name,
            Source,
            Rotation,
            ShadowEnabled,
            ShadowColor,
            ShadowOpacity,
            ShadowOffsetX,
            ShadowOffsetY,
            ShadowFalloff,
            GlowEnabled,
            GlowColor,
            GlowOpacity,
            GlowSize,
            ConstrainToPanel);
    }
}

internal sealed class PageTemplateFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Size2 Size { get; set; }
    public Guid ActiveLayerId { get; set; }
    public List<LayerTemplateFile> Layers { get; set; } = new();
    public List<GuideFile> Guides { get; set; } = new();
    public List<LayerGroupFile> LayerGroups { get; set; } = new();
    public BalloonLinkStyle LinkStyle { get; set; } = BalloonLinkStyle.Default;
    public OffPanelIndicatorStyle OffPanelIndicatorStyle { get; set; } = OffPanelIndicatorStyle.Default;

    public static PageTemplateFile FromTemplate(PageTemplate template)
    {
        return new PageTemplateFile
        {
            Id = template.Id,
            Name = template.Name,
            Size = template.Size,
            ActiveLayerId = template.ActiveLayerId,
            Layers = template.Layers.Select(LayerTemplateFile.FromTemplate).ToList(),
            Guides = template.Guides.Select(GuideFile.FromGuide).ToList(),
            LayerGroups = template.LayerGroups.Select(LayerGroupFile.FromLayerGroup).ToList(),
            LinkStyle = template.BalloonLinkStyle,
            OffPanelIndicatorStyle = template.OffPanelIndicatorStyle
        };
    }

    public PageTemplate ToTemplate()
    {
        return new PageTemplate(
            Id,
            Name,
            Size,
            Layers.Select(layer => layer.ToTemplate()).ToList(),
            ActiveLayerId,
            Guides.Select(guide => guide.ToGuide()).ToList(),
            LayerGroups.Select(group => group.ToLayerGroup()).ToList(),
            LinkStyle,
            OffPanelIndicatorStyle);
    }
}

internal sealed class PanelLayoutTemplateFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    public Size2 Size { get; set; }
    public List<PanelZoneFile> Panels { get; set; } = new();

    public static PanelLayoutTemplateFile FromTemplate(PanelLayoutTemplate template)
    {
        return new PanelLayoutTemplateFile
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            Tags = template.Tags.ToList(),
            Size = template.Size,
            Panels = template.Panels.Select(panel => PanelZoneFile.FromPanel(panel)).ToList()
        };
    }

    public PanelLayoutTemplate ToTemplate()
    {
        return new PanelLayoutTemplate(
            Id,
            Name,
            Size,
            Panels.Select(panel => panel.ToPanel()).ToList(),
            Description,
            Tags,
            Category);
    }
}

internal sealed class LayerTemplateFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public LayerKind Kind { get; set; } = LayerKind.Balloon;
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public float Opacity { get; set; }
    public LayerBlendMode BlendMode { get; set; } = LayerBlendMode.Normal;
    public Guid? GroupId { get; set; }

    public static LayerTemplateFile FromTemplate(LayerTemplate template)
    {
        return new LayerTemplateFile
        {
            Id = template.Id,
            Name = template.Name,
            Kind = template.Kind,
            IsVisible = template.IsVisible,
            IsLocked = template.IsLocked,
            Opacity = template.Opacity,
            BlendMode = template.BlendMode,
            GroupId = template.GroupId
        };
    }

    public LayerTemplate ToTemplate()
    {
        return new LayerTemplate(Id, Name, Kind, IsVisible, IsLocked, Opacity, BlendMode, GroupId);
    }
}

