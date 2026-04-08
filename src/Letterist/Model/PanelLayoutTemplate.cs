using System.Linq;

namespace Letterist.Model;

public sealed class PanelLayoutTemplate
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? Category { get; private set; }
    public Size2 Size { get; }

    private readonly List<PanelZone> _panels = new();
    public IReadOnlyList<PanelZone> Panels => _panels;
    private readonly List<string> _tags = new();
    public IReadOnlyList<string> Tags => _tags;

    public PanelLayoutTemplate(
        Guid id,
        string name,
        Size2 size,
        IEnumerable<PanelZone> panels,
        string? description = null,
        IEnumerable<string>? tags = null,
        string? category = null)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Panel Layout" : name.Trim();
        Size = size;
        _panels.AddRange(panels.Select(panel => panel.CloneWithoutImage()));
        Description = NormalizeOptionalText(description);
        Category = NormalizeOptionalText(category);
        _tags.AddRange(NormalizeTags(tags));
    }

    public static PanelLayoutTemplate FromPage(Page page, string name, Guid? templateId = null)
    {
        return new PanelLayoutTemplate(templateId ?? Guid.NewGuid(), name, page.Size, page.Panels);
    }

    public IReadOnlyList<PanelZone> CreatePanels(Size2 targetSize)
    {
        var sourceWidth = Math.Max(Size.Width, 1f);
        var sourceHeight = Math.Max(Size.Height, 1f);
        var scaleX = targetSize.Width / sourceWidth;
        var scaleY = targetSize.Height / sourceHeight;
        var radiusScale = MathF.Min(scaleX, scaleY);

        var panels = new List<PanelZone>(_panels.Count);
        foreach (var panel in _panels)
        {
            var bounds = new Rect(
                panel.Bounds.X * scaleX,
                panel.Bounds.Y * scaleY,
                panel.Bounds.Width * scaleX,
                panel.Bounds.Height * scaleY);

            panels.Add(new PanelZone(
                Guid.NewGuid(),
                panel.Name,
                bounds,
                panel.Order,
                panel.Color,
                panel.IsVisible,
                panel.IsLocked,
                panel.Shape,
                panel.CornerRadius * radiusScale,
                panel.SafeMargin * radiusScale,
                panel.CustomShapePathData,
                gutterLeftOverride: panel.GutterLeftOverride,
                gutterTopOverride: panel.GutterTopOverride,
                gutterRightOverride: panel.GutterRightOverride,
                gutterBottomOverride: panel.GutterBottomOverride,
                bleedLeft: panel.BleedLeft * scaleX,
                bleedTop: panel.BleedTop * scaleY,
                bleedRight: panel.BleedRight * scaleX,
                bleedBottom: panel.BleedBottom * scaleY));
        }

        return panels;
    }

    public PanelLayoutTemplate Clone()
    {
        return new PanelLayoutTemplate(
            Id,
            Name,
            Size,
            _panels.Select(panel => panel.CloneWithoutImage()),
            Description,
            _tags,
            Category);
    }

    public static IReadOnlyList<PanelLayoutTemplate> CreateDefaults(Size2? templateSize = null)
    {
        var presets = new List<PanelTemplatePreset>();
        if (templateSize.HasValue)
        {
            presets.Add(new PanelTemplatePreset(
                "Current Page",
                templateSize.Value,
                InferPresetFamily(templateSize.Value),
                new[] { "current", "custom" }));
        }

        foreach (var preset in GetStandardPagePresets())
        {
            if (!presets.Any(existing => AreSizesEquivalent(existing.Size, preset.Size)))
            {
                presets.Add(preset);
            }
        }

        var templates = new List<PanelLayoutTemplate>();
        foreach (var preset in presets)
        {
            AddPresetTemplates(templates, preset);
        }

        return templates;
    }

    private enum PanelTemplatePresetFamily
    {
        Portrait,
        Landscape,
        Square,
        Strip3,
        Strip4,
        Webtoon
    }

    private readonly struct PanelTemplatePreset
    {
        public PanelTemplatePreset(string name, Size2 size, PanelTemplatePresetFamily family, string[] tags)
        {
            Name = name;
            Size = size;
            Family = family;
            Tags = tags;
        }

        public string Name { get; }
        public Size2 Size { get; }
        public PanelTemplatePresetFamily Family { get; }
        public string[] Tags { get; }
    }

    private static IEnumerable<PanelTemplatePreset> GetStandardPagePresets()
    {
        return new[]
        {
            new PanelTemplatePreset("US Letter", new Size2(2550, 3300), PanelTemplatePresetFamily.Portrait, new[] { "print", "letter" }),
            new PanelTemplatePreset("A4", new Size2(2480, 3508), PanelTemplatePresetFamily.Portrait, new[] { "print", "a4" }),
            new PanelTemplatePreset("US Comic", new Size2(1988, 3075), PanelTemplatePresetFamily.Portrait, new[] { "print", "comic" }),
            new PanelTemplatePreset("Manga B5", new Size2(2150, 3035), PanelTemplatePresetFamily.Portrait, new[] { "manga", "print" }),
            new PanelTemplatePreset("Full HD", new Size2(1920, 1080), PanelTemplatePresetFamily.Landscape, new[] { "screen", "16:9" }),
            new PanelTemplatePreset("HD", new Size2(1280, 720), PanelTemplatePresetFamily.Landscape, new[] { "screen", "16:9" }),
            new PanelTemplatePreset("Instagram Square", new Size2(1080, 1080), PanelTemplatePresetFamily.Square, new[] { "social", "1:1" }),
            new PanelTemplatePreset("Social Media", new Size2(1200, 628), PanelTemplatePresetFamily.Landscape, new[] { "social", "landscape" }),
            new PanelTemplatePreset("Square", new Size2(1200, 1200), PanelTemplatePresetFamily.Square, new[] { "social", "1:1" }),
            new PanelTemplatePreset("3-Panel Strip", new Size2(2400, 800), PanelTemplatePresetFamily.Strip3, new[] { "strip", "3-panel" }),
            new PanelTemplatePreset("4-Panel Strip", new Size2(3200, 800), PanelTemplatePresetFamily.Strip4, new[] { "strip", "4-panel" }),
            new PanelTemplatePreset("Webtoon", new Size2(800, 2400), PanelTemplatePresetFamily.Webtoon, new[] { "webtoon", "vertical" })
        };
    }

    private static PanelTemplatePresetFamily InferPresetFamily(Size2 size)
    {
        var safeWidth = Math.Max(1f, size.Width);
        var safeHeight = Math.Max(1f, size.Height);
        var ratio = safeWidth / safeHeight;

        if (ratio >= 3.5f) return PanelTemplatePresetFamily.Strip4;
        if (ratio >= 2.4f) return PanelTemplatePresetFamily.Strip3;
        if (ratio <= 0.5f) return PanelTemplatePresetFamily.Webtoon;
        if (MathF.Abs(ratio - 1f) <= 0.15f) return PanelTemplatePresetFamily.Square;
        return ratio > 1f ? PanelTemplatePresetFamily.Landscape : PanelTemplatePresetFamily.Portrait;
    }

    private static bool AreSizesEquivalent(Size2 left, Size2 right, float tolerance = 0.5f)
    {
        return MathF.Abs(left.Width - right.Width) <= tolerance &&
               MathF.Abs(left.Height - right.Height) <= tolerance;
    }

    private static void AddPresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        switch (preset.Family)
        {
            case PanelTemplatePresetFamily.Portrait:
                AddPortraitPresetTemplates(templates, preset);
                break;
            case PanelTemplatePresetFamily.Landscape:
                AddLandscapePresetTemplates(templates, preset);
                break;
            case PanelTemplatePresetFamily.Square:
                AddSquarePresetTemplates(templates, preset);
                break;
            case PanelTemplatePresetFamily.Strip3:
                AddStrip3PresetTemplates(templates, preset);
                break;
            case PanelTemplatePresetFamily.Strip4:
                AddStrip4PresetTemplates(templates, preset);
                break;
            case PanelTemplatePresetFamily.Webtoon:
                AddWebtoonPresetTemplates(templates, preset);
                break;
        }
    }

    private static void AddPortraitPresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var gutter = Math.Max(12f, Math.Min(size.Width, size.Height) * 0.02f);
        var category = preset.Name;

        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "Single panel"),
            size,
            columns: 1,
            rows: 1,
            gutter,
            "Full-page splash panel.",
            category,
            BuildPresetTags(preset, "single", "splash")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "2-panel horizontal"),
            size,
            columns: 1,
            rows: 2,
            gutter,
            "Two stacked beats.",
            category,
            BuildPresetTags(preset, "stacked", "story")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "4-panel grid (2x2)"),
            size,
            columns: 2,
            rows: 2,
            gutter,
            "Balanced 2x2 pacing.",
            category,
            BuildPresetTags(preset, "grid", "4-panel")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "6-panel grid (2x3)"),
            size,
            columns: 2,
            rows: 3,
            gutter,
            "Traditional six-panel cadence.",
            category,
            BuildPresetTags(preset, "grid", "6-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Hero + 3 supports"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.54f),
                new Rect(0f, 0.58f, 0.32f, 0.42f),
                new Rect(0.34f, 0.58f, 0.32f, 0.42f),
                new Rect(0.68f, 0.58f, 0.32f, 0.42f)
            },
            "Large opener with three follow-up beats.",
            category,
            BuildPresetTags(preset, "hero", "story", "beats")));

        if (preset.Name.Equals("US Comic", StringComparison.OrdinalIgnoreCase))
        {
            AddUsComicPresetTemplates(templates, preset, gutter);
        }
        else if (preset.Name.Equals("Manga B5", StringComparison.OrdinalIgnoreCase))
        {
            AddMangaB5PresetTemplates(templates, preset, gutter);
        }
    }

    private static void AddUsComicPresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset, float gutter)
    {
        var size = preset.Size;
        var category = preset.Name;

        templates.Add(BuildLetterboxTemplate(
            BuildTemplateName(preset, "Cinematic letterbox"),
            size,
            gutter,
            "Three wide rows for cinematic page turns.",
            category,
            BuildPresetTags(preset, "cinematic", "letterbox")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Top splash + 4 beats"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.42f),
                new Rect(0f, 0.46f, 0.48f, 0.25f),
                new Rect(0.52f, 0.46f, 0.48f, 0.25f),
                new Rect(0f, 0.75f, 0.48f, 0.25f),
                new Rect(0.52f, 0.75f, 0.48f, 0.25f)
            },
            "Large opener with a clean two-by-two follow-up rhythm.",
            category,
            BuildPresetTags(preset, "splash", "5-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Left pillar cascade"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.36f, 1f),
                new Rect(0.4f, 0f, 0.6f, 0.3f),
                new Rect(0.4f, 0.34f, 0.6f, 0.32f),
                new Rect(0.4f, 0.7f, 0.6f, 0.3f)
            },
            "Anchor scene on the left with a cascading right-side sequence.",
            category,
            BuildPresetTags(preset, "pillar", "story")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Triptych opener + footer"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.32f, 0.56f),
                new Rect(0.34f, 0f, 0.32f, 0.56f),
                new Rect(0.68f, 0f, 0.32f, 0.56f),
                new Rect(0f, 0.6f, 1f, 0.4f)
            },
            "Three top beats ending in a full-width payoff.",
            category,
            BuildPresetTags(preset, "triptych", "payoff")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Cross-cut action six"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.58f, 0.32f),
                new Rect(0.62f, 0f, 0.38f, 0.2f),
                new Rect(0.62f, 0.24f, 0.38f, 0.34f),
                new Rect(0f, 0.36f, 0.58f, 0.24f),
                new Rect(0f, 0.64f, 0.48f, 0.36f),
                new Rect(0.52f, 0.62f, 0.48f, 0.38f)
            },
            "Asymmetric six-panel layout for cross-cut action scenes.",
            category,
            BuildPresetTags(preset, "action", "6-panel", "asymmetric")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Inset reveal"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.34f),
                new Rect(0f, 0.38f, 0.32f, 0.62f),
                new Rect(0.36f, 0.38f, 0.64f, 0.28f),
                new Rect(0.36f, 0.7f, 0.64f, 0.3f)
            },
            "Opener plus inset right-side beats for reveal pacing.",
            category,
            BuildPresetTags(preset, "inset", "story")));
    }

    private static void AddMangaB5PresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset, float gutter)
    {
        var size = preset.Size;
        var category = preset.Name;

        templates.Add(BuildMangaTemplate(
            BuildTemplateName(preset, "Dynamic manga flow"),
            size,
            gutter,
            "Top panorama with staggered middle and bottom beats.",
            category,
            BuildPresetTags(preset, "manga", "dynamic")));
        templates.Add(BuildLetterboxTemplate(
            BuildTemplateName(preset, "Quiet dialogue rows"),
            size,
            gutter,
            "Three horizontal bands for dialogue-heavy pacing.",
            category,
            BuildPresetTags(preset, "dialogue", "letterbox")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Tall center action"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.27f, 0.46f),
                new Rect(0.31f, 0f, 0.38f, 1f),
                new Rect(0.73f, 0f, 0.27f, 0.46f),
                new Rect(0f, 0.5f, 0.27f, 0.5f),
                new Rect(0.73f, 0.5f, 0.27f, 0.5f)
            },
            "Tall center panel framed by side reaction beats.",
            category,
            BuildPresetTags(preset, "action", "focus")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Vertical cadence 5-beat"),
            size,
            new[]
            {
                new Rect(0.05f, 0f, 0.9f, 0.16f),
                new Rect(0f, 0.2f, 0.62f, 0.2f),
                new Rect(0.38f, 0.44f, 0.62f, 0.2f),
                new Rect(0f, 0.68f, 0.56f, 0.14f),
                new Rect(0.44f, 0.84f, 0.56f, 0.16f)
            },
            "Alternating widths to match right-to-left manga pacing.",
            category,
            BuildPresetTags(preset, "manga", "5-panel", "vertical")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Dual splash with coda"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.3f),
                new Rect(0f, 0.34f, 1f, 0.38f),
                new Rect(0f, 0.76f, 0.48f, 0.24f),
                new Rect(0.52f, 0.76f, 0.48f, 0.24f)
            },
            "Two dominant moments followed by a two-panel coda.",
            category,
            BuildPresetTags(preset, "splash", "story")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Staggered dialogue lane"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.58f, 0.24f),
                new Rect(0.62f, 0f, 0.38f, 0.24f),
                new Rect(0f, 0.28f, 0.4f, 0.3f),
                new Rect(0.44f, 0.28f, 0.56f, 0.3f),
                new Rect(0f, 0.62f, 1f, 0.38f)
            },
            "Staggered dialogue beats ending in a wide resolution panel.",
            category,
            BuildPresetTags(preset, "dialogue", "staggered")));
    }

    private static void AddLandscapePresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var gutter = Math.Max(10f, Math.Min(size.Width, size.Height) * 0.018f);
        var category = preset.Name;

        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "Single panel"),
            size,
            columns: 1,
            rows: 1,
            gutter,
            "Landscape splash panel.",
            category,
            BuildPresetTags(preset, "single", "splash")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "Triptych columns"),
            size,
            columns: 3,
            rows: 1,
            gutter,
            "Three equal horizontal reading beats.",
            category,
            BuildPresetTags(preset, "columns", "triptych")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "4-panel grid (2x2)"),
            size,
            columns: 2,
            rows: 2,
            gutter,
            "Even four-beat landscape sequence.",
            category,
            BuildPresetTags(preset, "grid", "4-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Center focus"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.29f, 0.46f),
                new Rect(0.33f, 0f, 0.34f, 0.46f),
                new Rect(0.71f, 0f, 0.29f, 0.46f),
                new Rect(0f, 0.5f, 1f, 0.5f)
            },
            "Top trio with a full-width payoff panel.",
            category,
            BuildPresetTags(preset, "focus", "story")));
    }

    private static void AddSquarePresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var gutter = Math.Max(10f, Math.Min(size.Width, size.Height) * 0.02f);
        var category = preset.Name;

        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "Single panel"),
            size,
            columns: 1,
            rows: 1,
            gutter,
            "Square splash frame.",
            category,
            BuildPresetTags(preset, "single", "splash")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "2x2 beat"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.49f, 0.49f),
                new Rect(0.51f, 0f, 0.49f, 0.49f),
                new Rect(0f, 0.51f, 0.49f, 0.49f),
                new Rect(0.51f, 0.51f, 0.49f, 0.49f)
            },
            "Balanced square sequence for social posts.",
            category,
            BuildPresetTags(preset, "grid", "4-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Triptych"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.32f, 1f),
                new Rect(0.34f, 0f, 0.32f, 1f),
                new Rect(0.68f, 0f, 0.32f, 1f)
            },
            "Three equal columns for comparison or pacing.",
            category,
            BuildPresetTags(preset, "columns", "triptych")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Hero + pair"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.56f),
                new Rect(0f, 0.6f, 0.48f, 0.4f),
                new Rect(0.52f, 0.6f, 0.48f, 0.4f)
            },
            "Top hero frame with two closing panels.",
            category,
            BuildPresetTags(preset, "hero", "story")));
    }

    private static void AddStrip3PresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var category = preset.Name;

        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Equal thirds"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.32f, 1f),
                new Rect(0.34f, 0f, 0.32f, 1f),
                new Rect(0.68f, 0f, 0.32f, 1f)
            },
            "Classic three-panel strip pacing.",
            category,
            BuildPresetTags(preset, "strip", "columns")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Lead + 2 supports"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.5f, 1f),
                new Rect(0.52f, 0f, 0.48f, 0.48f),
                new Rect(0.52f, 0.52f, 0.48f, 0.48f)
            },
            "Large setup panel followed by two responses.",
            category,
            BuildPresetTags(preset, "hero", "story")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Center focus triptych"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.21f, 1f),
                new Rect(0.23f, 0f, 0.54f, 1f),
                new Rect(0.79f, 0f, 0.21f, 1f)
            },
            "Wide center beat with narrow side panels.",
            category,
            BuildPresetTags(preset, "focus", "strip")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Hero + footer triplet"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.68f),
                new Rect(0f, 0.72f, 0.32f, 0.28f),
                new Rect(0.34f, 0.72f, 0.32f, 0.28f),
                new Rect(0.68f, 0.72f, 0.32f, 0.28f)
            },
            "One cinematic opener and three short follow-ups.",
            category,
            BuildPresetTags(preset, "hero", "beats")));
    }

    private static void AddStrip4PresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var category = preset.Name;

        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "Equal quarters"),
            size,
            columns: 4,
            rows: 1,
            gutter: Math.Max(8f, Math.Min(size.Width, size.Height) * 0.015f),
            description: "Four equal strip beats.",
            category: category,
            tags: BuildPresetTags(preset, "strip", "4-panel")));
        templates.Add(BuildGridTemplate(
            BuildTemplateName(preset, "2x2 blocks"),
            size,
            columns: 2,
            rows: 2,
            gutter: Math.Max(8f, Math.Min(size.Width, size.Height) * 0.015f),
            description: "Two rows of broad panels.",
            category: category,
            tags: BuildPresetTags(preset, "grid", "4-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Hero + 3 supports"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.6f),
                new Rect(0f, 0.64f, 0.32f, 0.36f),
                new Rect(0.34f, 0.64f, 0.32f, 0.36f),
                new Rect(0.68f, 0.64f, 0.32f, 0.36f)
            },
            "Large top panel with three reaction beats.",
            category,
            BuildPresetTags(preset, "hero", "story")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Staggered four beat"),
            size,
            new[]
            {
                new Rect(0f, 0f, 0.46f, 0.48f),
                new Rect(0.5f, 0f, 0.5f, 0.3f),
                new Rect(0.5f, 0.34f, 0.5f, 0.66f),
                new Rect(0f, 0.52f, 0.46f, 0.48f)
            },
            "Asymmetric strip flow for dynamic pacing.",
            category,
            BuildPresetTags(preset, "asymmetric", "story")));
    }

    private static void AddWebtoonPresetTemplates(List<PanelLayoutTemplate> templates, PanelTemplatePreset preset)
    {
        var size = preset.Size;
        var category = preset.Name;

        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "4 beats"),
            size,
            new[]
            {
                new Rect(0.08f, 0f, 0.84f, 0.2f),
                new Rect(0.08f, 0.26f, 0.84f, 0.2f),
                new Rect(0.08f, 0.52f, 0.84f, 0.2f),
                new Rect(0.08f, 0.78f, 0.84f, 0.2f)
            },
            "Even vertical pacing with breathing room between beats.",
            category,
            BuildPresetTags(preset, "vertical", "4-panel")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Dialogue rhythm"),
            size,
            new[]
            {
                new Rect(0.12f, 0f, 0.76f, 0.16f),
                new Rect(0.2f, 0.22f, 0.68f, 0.14f),
                new Rect(0.08f, 0.42f, 0.8f, 0.2f),
                new Rect(0.18f, 0.68f, 0.7f, 0.14f),
                new Rect(0.1f, 0.86f, 0.8f, 0.14f)
            },
            "Alternating widths tuned for dialogue flow.",
            category,
            BuildPresetTags(preset, "dialogue", "vertical")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Action drop"),
            size,
            new[]
            {
                new Rect(0.08f, 0f, 0.84f, 0.18f),
                new Rect(0.02f, 0.24f, 0.96f, 0.48f),
                new Rect(0.1f, 0.78f, 0.8f, 0.22f)
            },
            "Short setup, tall action panel, compact resolution.",
            category,
            BuildPresetTags(preset, "action", "vertical")));
        templates.Add(BuildNormalizedTemplate(
            BuildTemplateName(preset, "Center splash"),
            size,
            new[]
            {
                new Rect(0f, 0f, 1f, 0.22f),
                new Rect(0f, 0.26f, 1f, 0.48f),
                new Rect(0f, 0.78f, 1f, 0.22f)
            },
            "Small opener, big center moment, compact closer.",
            category,
            BuildPresetTags(preset, "splash", "vertical")));
    }

    private static string BuildTemplateName(PanelTemplatePreset preset, string layoutName)
    {
        return $"{preset.Name} - {layoutName}";
    }

    private static string[] BuildPresetTags(PanelTemplatePreset preset, params string[] additionalTags)
    {
        var tags = new List<string>(preset.Tags.Length + additionalTags.Length + 2)
        {
            "preset",
            NormalizeTagToken(preset.Name)
        };

        tags.AddRange(preset.Tags);
        tags.AddRange(additionalTags);

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeTagToken(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }
        return normalized.Trim('-');
    }

    private static PanelLayoutTemplate BuildGridTemplate(
        string name,
        Size2 size,
        int columns,
        int rows,
        float gutter,
        string? description = null,
        string? category = null,
        params string[] tags)
    {
        var usableWidth = Math.Max(1f, size.Width - gutter * (columns + 1));
        var usableHeight = Math.Max(1f, size.Height - gutter * (rows + 1));
        var panelWidth = usableWidth / columns;
        var panelHeight = usableHeight / rows;

        var panels = new List<PanelZone>();
        var index = 1;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var x = gutter + col * (panelWidth + gutter);
                var y = gutter + row * (panelHeight + gutter);
                panels.Add(new PanelZone(
                    Guid.NewGuid(),
                    $"Panel {index}",
                    new Rect(x, y, panelWidth, panelHeight),
                    index,
                    PanelZone.DefaultColor,
                    isVisible: true,
                    isLocked: false,
                    shape: PanelShape.Rectangle));
                index++;
            }
        }

        return new PanelLayoutTemplate(Guid.NewGuid(), name, size, panels, description, tags, category);
    }

    private static PanelLayoutTemplate BuildMangaTemplate(
        string name,
        Size2 size,
        float gutter,
        string? description = null,
        string? category = null,
        params string[] tags)
    {
        var panels = new List<PanelZone>();
        var usableWidth = Math.Max(1f, size.Width - gutter * 2);
        var usableHeight = Math.Max(1f, size.Height - gutter * 2);

        var topHeight = usableHeight * 0.38f;
        var middleHeight = usableHeight * 0.30f;
        var bottomHeight = usableHeight - topHeight - middleHeight - gutter * 2;

        var topRect = new Rect(gutter, gutter, usableWidth, topHeight);
        panels.Add(new PanelZone(Guid.NewGuid(), "Panel 1", topRect, 1));

        var midY = gutter + topHeight + gutter;
        var midLeftWidth = usableWidth * 0.6f - gutter / 2f;
        var midRightWidth = usableWidth - midLeftWidth - gutter;
        panels.Add(new PanelZone(Guid.NewGuid(), "Panel 2", new Rect(gutter, midY, midLeftWidth, middleHeight), 2));
        panels.Add(new PanelZone(Guid.NewGuid(), "Panel 3", new Rect(gutter + midLeftWidth + gutter, midY, midRightWidth, middleHeight), 3));

        var bottomY = midY + middleHeight + gutter;
        var bottomLeftWidth = usableWidth * 0.4f - gutter / 2f;
        var bottomRightWidth = usableWidth - bottomLeftWidth - gutter;
        panels.Add(new PanelZone(Guid.NewGuid(), "Panel 4", new Rect(gutter, bottomY, bottomLeftWidth, bottomHeight), 4));
        panels.Add(new PanelZone(Guid.NewGuid(), "Panel 5", new Rect(gutter + bottomLeftWidth + gutter, bottomY, bottomRightWidth, bottomHeight), 5));

        return new PanelLayoutTemplate(Guid.NewGuid(), name, size, panels, description, tags, category);
    }

    private static PanelLayoutTemplate BuildLetterboxTemplate(
        string name,
        Size2 size,
        float gutter,
        string? description = null,
        string? category = null,
        params string[] tags)
    {
        var panels = new List<PanelZone>();
        var usableWidth = Math.Max(1f, size.Width - gutter * 2);
        var usableHeight = Math.Max(1f, size.Height - gutter * 4);

        var panelHeight = usableHeight / 3f;
        for (var row = 0; row < 3; row++)
        {
            var x = gutter;
            var y = gutter + row * (panelHeight + gutter);
            panels.Add(new PanelZone(
                Guid.NewGuid(),
                $"Panel {row + 1}",
                new Rect(x, y, usableWidth, panelHeight),
                row + 1,
                PanelZone.DefaultColor,
                isVisible: true,
                isLocked: false,
                shape: PanelShape.Rectangle));
        }

        return new PanelLayoutTemplate(Guid.NewGuid(), name, size, panels, description, tags, category);
    }

    private static PanelLayoutTemplate BuildNormalizedTemplate(
        string name,
        Size2 size,
        IReadOnlyList<Rect> normalizedRects,
        string? description = null,
        string? category = null,
        params string[] tags)
    {
        var safeRects = normalizedRects
            .Where(rect => rect.Width > 0f && rect.Height > 0f)
            .ToList();

        if (safeRects.Count == 0)
        {
            return BuildGridTemplate(name, size, columns: 1, rows: 1, gutter: Math.Max(8f, Math.Min(size.Width, size.Height) * 0.01f), description, category, tags);
        }

        var margin = Math.Max(8f, Math.Min(size.Width, size.Height) * 0.02f);
        var usableWidth = Math.Max(1f, size.Width - margin * 2f);
        var usableHeight = Math.Max(1f, size.Height - margin * 2f);

        var panels = new List<PanelZone>(safeRects.Count);
        for (var index = 0; index < safeRects.Count; index++)
        {
            var normalized = safeRects[index];
            var x = margin + normalized.X * usableWidth;
            var y = margin + normalized.Y * usableHeight;
            var width = Math.Max(1f, normalized.Width * usableWidth);
            var height = Math.Max(1f, normalized.Height * usableHeight);
            panels.Add(new PanelZone(
                Guid.NewGuid(),
                $"Panel {index + 1}",
                new Rect(x, y, width, height),
                index + 1,
                PanelZone.DefaultColor,
                isVisible: true,
                isLocked: false,
                shape: PanelShape.Rectangle));
        }

        return new PanelLayoutTemplate(Guid.NewGuid(), name, size, panels, description, tags, category);
    }

    internal void SetName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }
    }

    internal void SetMetadata(string name, string? description, IEnumerable<string>? tags, string? category)
    {
        SetName(name);
        Description = NormalizeOptionalText(description);
        Category = NormalizeOptionalText(category);
        _tags.Clear();
        _tags.AddRange(NormalizeTags(tags));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null) yield break;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}
