
using Letterist.Commands;
using Letterist.Model;

namespace Letterist.Publishing;

internal enum PublishingIssueSeverity
{
    Info,
    Warning,
    Error
}

internal enum PublishingIssueCategory
{
    Preflight,
    PrintPreparation,
    WebExport,
    DigitalDistribution
}

internal sealed class PublishingIssue
{
    public required string Code { get; init; }
    public required PublishingIssueSeverity Severity { get; init; }
    public required PublishingIssueCategory Category { get; init; }
    public required string Message { get; init; }
    public string Suggestion { get; init; } = string.Empty;
    public string FixId { get; init; } = string.Empty;
    public string Context { get; init; } = string.Empty;
}

internal sealed class PreflightFixSuggestion
{
    public required string FixId { get; init; }
    public required string Title { get; init; }
    public required int EstimatedCommandCount { get; init; }
}

internal sealed class PublishingPreflightOptions
{
    public IReadOnlyList<string> Languages { get; init; } = Array.Empty<string>();
    public float MinimumTextPointSize { get; init; } = 8f;
    public float MinimumStrokeWidth { get; init; } = 0.7f;
    public float RecommendedSafeMargin { get; init; } = 18f;
    public float MinimumBleed { get; init; } = 12f;
    public bool IncludePrintPreparationChecks { get; init; } = true;
    public PdfColorMode PrintColorMode { get; init; } = PdfColorMode.Rgb;
    public string IccProfileName { get; init; } = string.Empty;
    public float InkCoverageWarningThreshold { get; init; } = 280f;
}

internal sealed class PublishingPreflightReport
{
    public required IReadOnlyList<string> Languages { get; init; }
    public required IReadOnlyList<PublishingIssue> Issues { get; init; }
    public required IReadOnlyList<PreflightFixSuggestion> FixSuggestions { get; init; }
}

internal sealed class PrintPageBoxes
{
    public required Rect Trim { get; init; }
    public required Rect Bleed { get; init; }
    public required Rect Safe { get; init; }
}

internal sealed class PrintImpositionPlacement
{
    public int Slot { get; init; }
    public Guid PageId { get; init; }
    public string PageName { get; init; } = string.Empty;
}

internal sealed class PrintImpositionSheet
{
    public int SheetNumber { get; init; }
    public IReadOnlyList<PrintImpositionPlacement> Placements { get; init; } = Array.Empty<PrintImpositionPlacement>();
}

internal sealed class WebExportPreset
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Format { get; init; }
    public required int Quality { get; init; }
    public required IReadOnlyList<int> Widths { get; init; }
    public int TargetKilobytes { get; init; }
}

internal sealed class WebResponsiveTarget
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required string Suffix { get; init; }
    public required int Quality { get; init; }
    public required string Format { get; init; }
    public int EstimatedKilobytes { get; init; }
}

internal sealed class GuidedViewPanel
{
    public Guid PanelId { get; init; }
    public int Order { get; init; }
    public Rect Bounds { get; init; }
}

internal sealed class GuidedViewManifest
{
    public Guid PageId { get; init; }
    public string PageName { get; init; } = string.Empty;
    public string Language { get; init; } = "en";
    public IReadOnlyList<GuidedViewPanel> Panels { get; init; } = Array.Empty<GuidedViewPanel>();
}

internal sealed class WebtoonExportPreset
{
    public required string Name { get; init; }
    public required int TargetWidth { get; init; }
    public required int MaxSegmentHeight { get; init; }
    public required int GapPixels { get; init; }
}

internal sealed class WebtoonStripPlacement
{
    public Guid PageId { get; init; }
    public string PageName { get; init; } = string.Empty;
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int SegmentIndex { get; init; }
}

internal sealed class WebtoonStripPlan
{
    public required string PresetName { get; init; }
    public required int TotalWidth { get; init; }
    public required int TotalHeight { get; init; }
    public required int SegmentCount { get; init; }
    public required IReadOnlyList<WebtoonStripPlacement> Placements { get; init; }
}

internal sealed class PlatformPackageTemplate
{
    public required string Platform { get; init; }
    public required string ContainerFormat { get; init; }
    public required IReadOnlyList<string> RequiredFiles { get; init; }
    public required IReadOnlyDictionary<string, string> MetadataTemplate { get; init; }
}

internal static class PublishingPreflightService
{
    public static PublishingPreflightReport Analyze(Document document, PublishingPreflightOptions? options = null)
    {
        options ??= new PublishingPreflightOptions();
        var languages = ResolveLanguages(document, options.Languages);

        var issues = new List<PublishingIssue>();

        AddTranslationIssues(document, languages, issues);
        AddTypographyIssues(document, options, issues);
        AddPanelGeometryIssues(document, options, issues);

        if (options.IncludePrintPreparationChecks)
        {
            AddPrintPreparationIssues(document, options, issues);
        }

        var fixSuggestions = BuildFixSuggestions(document, options, issues);

        return new PublishingPreflightReport
        {
            Languages = languages,
            Issues = issues,
            FixSuggestions = fixSuggestions
        };
    }

    public static IReadOnlyList<ICommand> BuildFixCommands(Document document, string fixId, PublishingPreflightOptions? options = null)
    {
        options ??= new PublishingPreflightOptions();
        if (string.IsNullOrWhiteSpace(fixId))
        {
            return Array.Empty<ICommand>();
        }

        if (fixId.StartsWith("fill-missing-translations", StringComparison.OrdinalIgnoreCase))
        {
            var language = ParseFixLanguage(fixId, document.ActiveLanguage);
            return BuildMissingTranslationFixCommands(document, language);
        }

        if (string.Equals(fixId, "raise-small-text", StringComparison.OrdinalIgnoreCase))
        {
            return BuildRaiseSmallTextCommands(document, options.MinimumTextPointSize);
        }

        if (string.Equals(fixId, "normalize-panel-bleed", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPanelBleedFixCommands(document, options.MinimumBleed);
        }

        if (string.Equals(fixId, "set-panel-safe-margin", StringComparison.OrdinalIgnoreCase))
        {
            return BuildPanelSafeMarginFixCommands(document, options.RecommendedSafeMargin);
        }

        return Array.Empty<ICommand>();
    }

    private static IReadOnlyList<string> ResolveLanguages(Document document, IReadOnlyList<string> requested)
    {
        if (requested.Count == 0)
        {
            return new[] { Document.NormalizeLanguageTag(document.ActiveLanguage, document.BaseLanguage) };
        }

        return requested
            .Select(language => Document.NormalizeLanguageTag(language, document.BaseLanguage))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(language => language, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddTranslationIssues(Document document, IReadOnlyList<string> languages, List<PublishingIssue> issues)
    {
        var balloons = EnumerateBalloons(document).ToArray();

        foreach (var language in languages)
        {
            if (string.Equals(language, document.BaseLanguage, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var missing = balloons.Count(row => document.IsBalloonUntranslated(row.Balloon, language));
            if (missing > 0)
            {
                issues.Add(new PublishingIssue
                {
                    Code = "missing_translation",
                    Severity = PublishingIssueSeverity.Error,
                    Category = PublishingIssueCategory.Preflight,
                    Message = $"{missing} balloon(s) are missing translation text for {language}.",
                    Suggestion = "Copy source text into missing translations, then review and edit.",
                    FixId = $"fill-missing-translations:{language}",
                    Context = language
                });
            }

            var stale = balloons.Count(row => document.IsBalloonTranslationStale(row.Balloon, language));
            if (stale > 0)
            {
                issues.Add(new PublishingIssue
                {
                    Code = "stale_translation",
                    Severity = PublishingIssueSeverity.Warning,
                    Category = PublishingIssueCategory.Preflight,
                    Message = $"{stale} balloon(s) have stale translations in {language}.",
                    Suggestion = "Re-check translated text against updated source copy.",
                    Context = language
                });
            }
        }
    }

    private static void AddTypographyIssues(Document document, PublishingPreflightOptions options, List<PublishingIssue> issues)
    {
        var rows = EnumerateBalloons(document).ToArray();

        var tooSmall = rows
            .Where(row => row.Balloon.TextStyle.FontSize < options.MinimumTextPointSize)
            .ToArray();
        if (tooSmall.Length > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "text_too_small",
                Severity = PublishingIssueSeverity.Warning,
                Category = PublishingIssueCategory.Preflight,
                Message = $"{tooSmall.Length} balloon(s) use text smaller than {options.MinimumTextPointSize:0.##} pt.",
                Suggestion = "Raise small text to the minimum readability threshold.",
                FixId = "raise-small-text"
            });
        }

        var thinStroke = rows
            .Where(row => row.Balloon.BalloonStyle.StrokeWidth > 0 && row.Balloon.BalloonStyle.StrokeWidth < options.MinimumStrokeWidth)
            .ToArray();
        if (thinStroke.Length > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "stroke_too_thin",
                Severity = PublishingIssueSeverity.Warning,
                Category = PublishingIssueCategory.Preflight,
                Message = $"{thinStroke.Length} balloon(s) use stroke thinner than {options.MinimumStrokeWidth:0.##}px.",
                Suggestion = "Increase stroke width for reliable print reproduction."
            });
        }
    }

    private static void AddPanelGeometryIssues(Document document, PublishingPreflightOptions options, List<PublishingIssue> issues)
    {
        var lowBleedPanels = new List<(Guid pageId, Guid panelId)>();
        var lowSafeMarginPanels = new List<(Guid pageId, Guid panelId)>();

        foreach (var page in document.Pages)
        {
            foreach (var panel in page.Panels)
            {
                if (TouchesPageEdge(page, panel) &&
                    (panel.BleedLeft < options.MinimumBleed || panel.BleedTop < options.MinimumBleed || panel.BleedRight < options.MinimumBleed || panel.BleedBottom < options.MinimumBleed))
                {
                    lowBleedPanels.Add((page.Id, panel.Id));
                }

                if (panel.SafeMargin < options.RecommendedSafeMargin)
                {
                    lowSafeMarginPanels.Add((page.Id, panel.Id));
                }
            }
        }

        if (lowBleedPanels.Count > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "insufficient_bleed",
                Severity = PublishingIssueSeverity.Warning,
                Category = PublishingIssueCategory.PrintPreparation,
                Message = $"{lowBleedPanels.Count} panel(s) touching page edges have insufficient bleed.",
                Suggestion = "Apply minimum bleed on edge-touching panels.",
                FixId = "normalize-panel-bleed"
            });
        }

        if (lowSafeMarginPanels.Count > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "safe_margin_small",
                Severity = PublishingIssueSeverity.Info,
                Category = PublishingIssueCategory.PrintPreparation,
                Message = $"{lowSafeMarginPanels.Count} panel(s) use a safe margin below {options.RecommendedSafeMargin:0.##}px.",
                Suggestion = "Increase panel safe margin to reduce trim risk.",
                FixId = "set-panel-safe-margin"
            });
        }
    }

    private static void AddPrintPreparationIssues(Document document, PublishingPreflightOptions options, List<PublishingIssue> issues)
    {
        if (options.PrintColorMode == PdfColorMode.Cmyk && string.IsNullOrWhiteSpace(options.IccProfileName))
        {
            issues.Add(new PublishingIssue
            {
                Code = "missing_icc_profile",
                Severity = PublishingIssueSeverity.Warning,
                Category = PublishingIssueCategory.PrintPreparation,
                Message = "CMYK export selected without an ICC profile name.",
                Suggestion = "Set an ICC profile name before final print export."
            });
        }

        issues.Add(new PublishingIssue
        {
            Code = "overprint_preview_note",
            Severity = PublishingIssueSeverity.Info,
            Category = PublishingIssueCategory.PrintPreparation,
            Message = "Overprint simulation is informational in Letterist print prep.",
            Suggestion = "Verify final overprint behavior in downstream RIP/prepress tools."
        });

        var highCoverage = 0;
        var nonRichBlack = 0;

        foreach (var (_, _, balloon) in EnumerateBalloons(document))
        {
            var coverage = PrintPreparationService.EstimateInkCoverage(balloon.TextStyle.TextColor, options.PrintColorMode);
            if (coverage > options.InkCoverageWarningThreshold)
            {
                highCoverage++;
            }

            if (options.PrintColorMode == PdfColorMode.Cmyk &&
                PrintPreparationService.IsNearBlack(balloon.TextStyle.TextColor) &&
                !PrintPreparationService.IsRichBlack(balloon.TextStyle.TextColor))
            {
                nonRichBlack++;
            }
        }

        if (highCoverage > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "ink_coverage_high",
                Severity = PublishingIssueSeverity.Warning,
                Category = PublishingIssueCategory.PrintPreparation,
                Message = $"{highCoverage} text style(s) exceed {options.InkCoverageWarningThreshold:0.#}% estimated ink coverage.",
                Suggestion = "Reduce dense dark mixtures to improve drying and avoid setoff."
            });
        }

        if (nonRichBlack > 0)
        {
            issues.Add(new PublishingIssue
            {
                Code = "rich_black_recommended",
                Severity = PublishingIssueSeverity.Info,
                Category = PublishingIssueCategory.PrintPreparation,
                Message = $"{nonRichBlack} near-black text style(s) are not rich black.",
                Suggestion = "Use rich black mix for large black display text in CMYK workflows."
            });
        }
    }

    private static IReadOnlyList<PreflightFixSuggestion> BuildFixSuggestions(Document document, PublishingPreflightOptions options, IEnumerable<PublishingIssue> issues)
    {
        var suggestions = new List<PreflightFixSuggestion>();

        foreach (var fixId in issues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.FixId))
            .Select(issue => issue.FixId)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var commands = BuildFixCommands(document, fixId, options);
            if (commands.Count == 0)
            {
                continue;
            }

            suggestions.Add(new PreflightFixSuggestion
            {
                FixId = fixId,
                Title = GetFixTitle(fixId),
                EstimatedCommandCount = commands.Count
            });
        }

        return suggestions;
    }

    private static string GetFixTitle(string fixId)
    {
        if (fixId.StartsWith("fill-missing-translations", StringComparison.OrdinalIgnoreCase))
        {
            var language = ParseFixLanguage(fixId, "active");
            return $"Fill missing translations ({language})";
        }

        return fixId.ToLowerInvariant() switch
        {
            "raise-small-text" => "Raise small text to minimum size",
            "normalize-panel-bleed" => "Normalize panel bleed on page edges",
            "set-panel-safe-margin" => "Set minimum panel safe margins",
            _ => fixId
        };
    }

    private static IReadOnlyList<ICommand> BuildMissingTranslationFixCommands(Document document, string language)
    {
        var commands = new List<ICommand>();
        foreach (var (_, _, balloon) in EnumerateBalloons(document))
        {
            if (!document.IsBalloonUntranslated(balloon, language))
            {
                continue;
            }

            commands.Add(new SetBalloonTranslationCommand(
                balloon.Id,
                language,
                balloon.Text,
                balloon.Text,
                null));
        }

        return commands;
    }

    private static IReadOnlyList<ICommand> BuildRaiseSmallTextCommands(Document document, float minimumTextSize)
    {
        var commands = new List<ICommand>();
        foreach (var (_, _, balloon) in EnumerateBalloons(document))
        {
            if (balloon.TextStyle.FontSize >= minimumTextSize)
            {
                continue;
            }

            commands.Add(new SetTextStyleCommand(
                balloon.Id,
                balloon.TextStyle.With(fontSize: minimumTextSize)));
        }

        return commands;
    }

    private static IReadOnlyList<ICommand> BuildPanelBleedFixCommands(Document document, float minimumBleed)
    {
        var commands = new List<ICommand>();

        foreach (var page in document.Pages)
        {
            foreach (var panel in page.Panels)
            {
                if (!TouchesPageEdge(page, panel))
                {
                    continue;
                }

                var left = panel.BleedLeft < minimumBleed ? minimumBleed : panel.BleedLeft;
                var top = panel.BleedTop < minimumBleed ? minimumBleed : panel.BleedTop;
                var right = panel.BleedRight < minimumBleed ? minimumBleed : panel.BleedRight;
                var bottom = panel.BleedBottom < minimumBleed ? minimumBleed : panel.BleedBottom;

                if (Math.Abs(left - panel.BleedLeft) < 0.01f &&
                    Math.Abs(top - panel.BleedTop) < 0.01f &&
                    Math.Abs(right - panel.BleedRight) < 0.01f &&
                    Math.Abs(bottom - panel.BleedBottom) < 0.01f)
                {
                    continue;
                }

                commands.Add(new SetPanelBleedCommand(page.Id, panel.Id, left, top, right, bottom));
            }
        }

        return commands;
    }

    private static IReadOnlyList<ICommand> BuildPanelSafeMarginFixCommands(Document document, float minimumSafeMargin)
    {
        var commands = new List<ICommand>();

        foreach (var page in document.Pages)
        {
            foreach (var panel in page.Panels)
            {
                if (panel.SafeMargin >= minimumSafeMargin)
                {
                    continue;
                }

                commands.Add(new SetPanelSafeMarginCommand(page.Id, panel.Id, minimumSafeMargin));
            }
        }

        return commands;
    }

    private static bool TouchesPageEdge(Page page, PanelZone panel)
    {
        const float epsilon = 1f;
        var bounds = panel.Bounds;
        return bounds.Left <= epsilon ||
               bounds.Top <= epsilon ||
               bounds.Right >= page.Size.Width - epsilon ||
               bounds.Bottom >= page.Size.Height - epsilon;
    }

    private static string ParseFixLanguage(string fixId, string fallback)
    {
        var separator = fixId.IndexOf(':');
        if (separator <= 0 || separator >= fixId.Length - 1)
        {
            return fallback;
        }

        return fixId[(separator + 1)..];
    }

    private static IEnumerable<(Page Page, Layer Layer, Balloon Balloon)> EnumerateBalloons(Document document)
    {
        foreach (var page in document.Pages)
        {
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons)
                {
                    continue;
                }

                foreach (var balloon in layer.Balloons)
                {
                    yield return (page, layer, balloon);
                }
            }
        }
    }
}

internal static class PrintPreparationService
{
    public static PrintPageBoxes BuildPageBoxes(Page page, float bleed = 12f, float safeMargin = 18f)
    {
        var trim = new Rect(0, 0, page.Size.Width, page.Size.Height);
        var bleedBox = new Rect(-bleed, -bleed, page.Size.Width + bleed * 2, page.Size.Height + bleed * 2);
        var safeWidth = Math.Max(1f, page.Size.Width - safeMargin * 2);
        var safeHeight = Math.Max(1f, page.Size.Height - safeMargin * 2);
        var safe = new Rect(safeMargin, safeMargin, safeWidth, safeHeight);

        return new PrintPageBoxes
        {
            Trim = trim,
            Bleed = bleedBox,
            Safe = safe
        };
    }

    public static IReadOnlyList<PrintImpositionSheet> BuildImposition(Document document, int pagesPerSheet = 2)
    {
        pagesPerSheet = Math.Clamp(pagesPerSheet, 1, 16);

        var sheets = new List<PrintImpositionSheet>();
        for (int i = 0; i < document.Pages.Count; i += pagesPerSheet)
        {
            var placements = new List<PrintImpositionPlacement>();
            var slice = document.Pages.Skip(i).Take(pagesPerSheet).ToArray();
            for (int slot = 0; slot < slice.Length; slot++)
            {
                placements.Add(new PrintImpositionPlacement
                {
                    Slot = slot,
                    PageId = slice[slot].Id,
                    PageName = slice[slot].Name
                });
            }

            sheets.Add(new PrintImpositionSheet
            {
                SheetNumber = sheets.Count + 1,
                Placements = placements
            });
        }

        return sheets;
    }

    public static float EstimateInkCoverage(Color color, PdfColorMode colorMode)
    {
        if (colorMode == PdfColorMode.Rgb)
        {
            return ((color.R + color.G + color.B) / 765f) * 100f;
        }

        RgbToCmyk(color, out var c, out var m, out var y, out var k);
        return (c + m + y + k) * 100f;
    }

    public static bool IsRichBlack(Color color)
    {
        RgbToCmyk(color, out var c, out var m, out var y, out var k);
        return k >= 0.9f && c >= 0.45f && m >= 0.3f && y >= 0.3f;
    }

    public static bool IsNearBlack(Color color)
    {
        return color.R < 40 && color.G < 40 && color.B < 40;
    }

    public static Color SimulateCmykPreview(Color color)
    {
        RgbToCmyk(color, out var c, out var m, out var y, out var k);

        var adjustedK = Math.Clamp(k + 0.08f, 0f, 1f);
        var adjustedC = Math.Clamp(c * 0.92f, 0f, 1f);
        var adjustedM = Math.Clamp(m * 0.92f, 0f, 1f);
        var adjustedY = Math.Clamp(y * 0.92f, 0f, 1f);

        var r = (byte)Math.Clamp((1f - adjustedC) * (1f - adjustedK) * 255f, 0f, 255f);
        var g = (byte)Math.Clamp((1f - adjustedM) * (1f - adjustedK) * 255f, 0f, 255f);
        var b = (byte)Math.Clamp((1f - adjustedY) * (1f - adjustedK) * 255f, 0f, 255f);

        return new Color(r, g, b, color.A);
    }

    private static void RgbToCmyk(Color color, out float c, out float m, out float y, out float k)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;

        k = 1f - MathF.Max(r, MathF.Max(g, b));
        if (k >= 1f - 1e-5f)
        {
            c = m = y = 0f;
            k = 1f;
            return;
        }

        c = (1f - r - k) / (1f - k);
        m = (1f - g - k) / (1f - k);
        y = (1f - b - k) / (1f - k);
    }
}

internal static class WebExportService
{
    private static readonly IReadOnlyList<WebExportPreset> Presets =
    [
        new WebExportPreset
        {
            Name = "web",
            Description = "General-purpose web export",
            Format = "webp",
            Quality = 80,
            Widths = new[] { 2048, 1440, 960 },
            TargetKilobytes = 450
        },
        new WebExportPreset
        {
            Name = "social-feed",
            Description = "Square feed image preset",
            Format = "webp",
            Quality = 82,
            Widths = new[] { 1080 },
            TargetKilobytes = 350
        },
        new WebExportPreset
        {
            Name = "social-story",
            Description = "Story/reel card preset",
            Format = "jpeg",
            Quality = 86,
            Widths = new[] { 1080 },
            TargetKilobytes = 400
        },
        new WebExportPreset
        {
            Name = "responsive",
            Description = "Multi-breakpoint responsive set",
            Format = "webp",
            Quality = 78,
            Widths = new[] { 2400, 1800, 1366, 960, 640 },
            TargetKilobytes = 500
        }
    ];

    public static IReadOnlyList<WebExportPreset> GetPresets() => Presets;

    public static WebExportPreset ResolvePreset(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Presets[0];
        }

        return Presets.FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? Presets[0];
    }

    public static IReadOnlyList<WebResponsiveTarget> BuildResponsiveTargets(int sourceWidth, int sourceHeight, WebExportPreset preset)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return Array.Empty<WebResponsiveTarget>();
        }

        var targets = new List<WebResponsiveTarget>();
        foreach (var width in preset.Widths.OrderByDescending(value => value))
        {
            var targetWidth = Math.Min(sourceWidth, width);
            var targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * (targetWidth / (double)sourceWidth)));
            var estimated = EstimateTargetKilobytes(targetWidth, targetHeight, preset.Quality, preset.Format);
            targets.Add(new WebResponsiveTarget
            {
                Width = targetWidth,
                Height = targetHeight,
                Suffix = $"w{targetWidth}",
                Quality = preset.Quality,
                Format = preset.Format,
                EstimatedKilobytes = estimated
            });
        }

        return targets
            .GroupBy(target => target.Width)
            .Select(group => group.First())
            .OrderByDescending(target => target.Width)
            .ToArray();
    }

    private static int EstimateTargetKilobytes(int width, int height, int quality, string format)
    {
        var pixelCount = width * height;
        var baseBitsPerPixel = string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase) ? 0.85 : 1.1;
        var qualityFactor = Math.Clamp(quality, 1, 100) / 100.0;
        var estimatedBytes = pixelCount * baseBitsPerPixel * qualityFactor / 8.0;
        return Math.Max(1, (int)Math.Round(estimatedBytes / 1024.0));
    }
}

internal static class DigitalDistributionService
{
    private static readonly IReadOnlyList<WebtoonExportPreset> WebtoonPresets =
    [
        new WebtoonExportPreset { Name = "line-webtoon", TargetWidth = 1080, MaxSegmentHeight = 12800, GapPixels = 40 },
        new WebtoonExportPreset { Name = "tapas", TargetWidth = 940, MaxSegmentHeight = 4000, GapPixels = 32 },
        new WebtoonExportPreset { Name = "generic", TargetWidth = 1080, MaxSegmentHeight = 8000, GapPixels = 24 }
    ];

    private static readonly IReadOnlyDictionary<string, PlatformPackageTemplate> PlatformTemplates =
        new Dictionary<string, PlatformPackageTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["webtoon"] = new PlatformPackageTemplate
            {
                Platform = "webtoon",
                ContainerFormat = "zip",
                RequiredFiles = new[] { "manifest.json", "cover.jpg", "segments/" },
                MetadataTemplate = new Dictionary<string, string>
                {
                    ["title"] = "Your Series Title",
                    ["language"] = "en",
                    ["orientation"] = "vertical",
                    ["rating"] = "all"
                }
            },
            ["comixology"] = new PlatformPackageTemplate
            {
                Platform = "comixology",
                ContainerFormat = "cbz",
                RequiredFiles = new[] { "manifest.json", "pages/", "guided-view.json" },
                MetadataTemplate = new Dictionary<string, string>
                {
                    ["title"] = "Book Title",
                    ["language"] = "en",
                    ["layout"] = "guided-view"
                }
            },
            ["globalcomix"] = new PlatformPackageTemplate
            {
                Platform = "globalcomix",
                ContainerFormat = "zip",
                RequiredFiles = new[] { "manifest.json", "pages/", "metadata.json" },
                MetadataTemplate = new Dictionary<string, string>
                {
                    ["title"] = "Book Title",
                    ["language"] = "en",
                    ["format"] = "comic"
                }
            }
        };

    public static GuidedViewManifest BuildGuidedViewManifest(Page page, string language)
    {
        var panels = page.Panels
            .OrderBy(panel => panel.Order)
            .Select(panel => new GuidedViewPanel
            {
                PanelId = panel.Id,
                Order = panel.Order,
                Bounds = panel.Bounds
            })
            .ToArray();

        return new GuidedViewManifest
        {
            PageId = page.Id,
            PageName = page.Name,
            Language = string.IsNullOrWhiteSpace(language) ? "en" : language,
            Panels = panels
        };
    }

    public static IReadOnlyList<WebtoonExportPreset> GetWebtoonPresets() => WebtoonPresets;

    public static WebtoonStripPlan BuildWebtoonStripPlan(IReadOnlyList<Page> pages, WebtoonExportPreset preset)
    {
        var placements = new List<WebtoonStripPlacement>();
        var totalY = 0;
        var segmentIndex = 0;
        var segmentBottom = preset.MaxSegmentHeight;

        foreach (var page in pages)
        {
            var width = Math.Max(1, (int)Math.Round(page.Size.Width));
            var height = Math.Max(1, (int)Math.Round(page.Size.Height));
            var scale = preset.TargetWidth / (double)width;
            var scaledHeight = Math.Max(1, (int)Math.Round(height * scale));

            if (totalY + scaledHeight > segmentBottom && totalY > 0)
            {
                segmentIndex++;
                segmentBottom = (segmentIndex + 1) * preset.MaxSegmentHeight;
            }

            placements.Add(new WebtoonStripPlacement
            {
                PageId = page.Id,
                PageName = page.Name,
                Y = totalY,
                Width = preset.TargetWidth,
                Height = scaledHeight,
                SegmentIndex = segmentIndex
            });

            totalY += scaledHeight + preset.GapPixels;
        }

        var totalHeight = Math.Max(0, totalY - preset.GapPixels);
        return new WebtoonStripPlan
        {
            PresetName = preset.Name,
            TotalWidth = preset.TargetWidth,
            TotalHeight = totalHeight,
            SegmentCount = segmentIndex + 1,
            Placements = placements
        };
    }

    public static PlatformPackageTemplate? ResolveTemplate(string platform)
    {
        if (PlatformTemplates.TryGetValue(platform, out var template))
        {
            return template;
        }

        return null;
    }

    public static IReadOnlyList<PublishingIssue> ValidatePackage(string platform, IReadOnlyCollection<string> packagePaths)
    {
        var template = ResolveTemplate(platform);
        if (template == null)
        {
            return
            [
                new PublishingIssue
                {
                    Code = "unknown_platform",
                    Severity = PublishingIssueSeverity.Error,
                    Category = PublishingIssueCategory.DigitalDistribution,
                    Message = $"Unknown platform template: {platform}",
                    Suggestion = "Choose one of the supported platform templates."
                }
            ];
        }

        var issues = new List<PublishingIssue>();
        foreach (var required in template.RequiredFiles)
        {
            var exists = packagePaths.Any(path =>
                path.StartsWith(required, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, required, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                continue;
            }

            issues.Add(new PublishingIssue
            {
                Code = "missing_package_file",
                Severity = PublishingIssueSeverity.Error,
                Category = PublishingIssueCategory.DigitalDistribution,
                Message = $"Required package entry is missing: {required}",
                Suggestion = "Regenerate package files using the platform template."
            });
        }

        return issues;
    }
}
