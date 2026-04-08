using Letterist.Model;
using Letterist.Rendering.Typesetting;
using Letterist;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Numerics;

namespace Letterist.Rendering;

internal static class TextLayoutUtilities
{
    private const float MinFitFontSize = 6f;
    private const float MinFitTracking = -0.15f;  // Increased range for more visible tracking adjustments
    private const float MaxFitTracking = 0.05f;   // Allow slight expansion tracking too
    private const int FitIterations = 10;         // More iterations for better precision

    private static readonly MethodInfo? SetCharacterSpacingMethod = typeof(CanvasTextLayout)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "SetCharacterSpacing" && m.GetParameters().Length == 5);

    private static readonly Type? StartIndexType = SetCharacterSpacingMethod?.GetParameters()[0].ParameterType;
    private static readonly Type? LengthType = SetCharacterSpacingMethod?.GetParameters()[1].ParameterType;

    private static readonly MethodInfo? SetFillColorMethod = typeof(CanvasTextLayout)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "SetColor" && m.GetParameters().Length == 3);

    private static readonly MethodInfo? SetTypographyMethod = typeof(CanvasTextLayout)
        .GetMethods()
        .FirstOrDefault(m => m.Name == "SetTypography" && m.GetParameters().Length == 3);

    private static readonly Type? TypographyStartIndexType = SetTypographyMethod?.GetParameters()[0].ParameterType;
    private static readonly Type? TypographyLengthType = SetTypographyMethod?.GetParameters()[1].ParameterType;

    private static readonly int FillColorIndex = GetFillColorIndex();
    private static readonly int FillStartIndex = GetFillStartIndex();
    private static readonly int FillLengthIndex = GetFillLengthIndex();

    private static int GetFillColorIndex()
    {
        if (SetFillColorMethod == null) return -1;
        var parameters = SetFillColorMethod.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(Windows.UI.Color))
            {
                return i;
            }
        }
        return -1;
    }

    private static int GetFillStartIndex()
    {
        if (SetFillColorMethod == null) return -1;
        var parameters = SetFillColorMethod.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(int) || parameters[i].ParameterType == typeof(uint))
            {
                return i;
            }
        }
        return -1;
    }

    private static int GetFillLengthIndex()
    {
        if (SetFillColorMethod == null) return -1;
        var parameters = SetFillColorMethod.GetParameters();
        var foundFirst = false;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(int) || parameters[i].ParameterType == typeof(uint))
            {
                if (foundFirst) return i;
                foundFirst = true;
            }
        }
        return -1;
    }

    public static TypographySettings CreateTypographySettings(TextStyle textStyle)
    {
        var effectiveHyphenationLevel = ResolveHyphenationLevel(textStyle);
        return TypographySettings.FromLevels(
            textStyle.JustificationStrength,
            effectiveHyphenationLevel,
            !string.IsNullOrEmpty(textStyle.HyphenationLocale));
    }

    public static CanvasTextFormat CreateTextFormat(
        TextStyle textStyle,
        IReadOnlyList<TextStyleSpan>? spans,
        TypographySettings? typographySettings = null,
        bool isRtl = false,
        string? languageTag = null,
        bool preferVerticalCjk = false,
        string? sampleText = null)
    {
        typographySettings ??= CreateTypographySettings(textStyle);
        var resolvedFontFamily = CjkFontSupport.ResolveFontFamily(
            textStyle.FontFamily,
            languageTag,
            sampleText,
            preferVerticalCjk);

        var horizontalAlignment = textStyle.Alignment switch
        {
            TextAlignment.Left => CanvasHorizontalAlignment.Left,
            TextAlignment.Right => CanvasHorizontalAlignment.Right,
            _ => CanvasHorizontalAlignment.Center
        };

        if (textStyle.RagMode == RagMode.Justified)
        {
            horizontalAlignment = CanvasHorizontalAlignment.Justified;
        }

        var wordWrapping = CanvasWordWrapping.WholeWord;

        if (typographySettings.EnableHyphenation && !string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            wordWrapping = CanvasWordWrapping.Wrap;
        }

        var format = new CanvasTextFormat
        {
            FontFamily = resolvedFontFamily,
            FontSize = textStyle.FontSize,
            FontWeight = textStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = textStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = wordWrapping
        };

        format.Direction = isRtl
            ? CanvasTextDirection.RightToLeftThenTopToBottom
            : CanvasTextDirection.LeftToRightThenTopToBottom;

        format.LineSpacingMode = CanvasLineSpacingMode.Proportional;
        format.LineSpacing = textStyle.LineHeight;

        if (!string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            format.LocaleName = textStyle.HyphenationLocale;
        }

        return format;
    }

    private static float CalculateMaximumLineHeight(TextStyle baseStyle, IReadOnlyList<TextStyleSpan>? spans)
    {
        float maxLineHeight = baseStyle.FontSize * baseStyle.LineHeight;

        if (spans != null)
        {
            foreach (var span in spans)
            {
                var spanStyle = span.Style ?? baseStyle;
                var spanLineHeight = spanStyle.FontSize * spanStyle.LineHeight;
                if (spanLineHeight > maxLineHeight)
                {
                    maxLineHeight = spanLineHeight;
                }
            }
        }

        return maxLineHeight;
    }

    public static void ApplyTypographyFeatures(CanvasTextLayout layout, int textLength, TypographySettings settings)
    {
        if (layout == null || textLength <= 0) return;
        if (SetTypographyMethod == null) return;

        var typography = new CanvasTypography();

        if (settings.EnableLigatures)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.StandardLigatures,
                Parameter = 1
            });
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.DiscretionaryLigatures,
                Parameter = 1
            });
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.ContextualLigatures,
                Parameter = 1
            });
        }

        if (settings.EnableContextualAlternates)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.ContextualAlternates,
                Parameter = 1
            });
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.ContextualSwash,
                Parameter = 1
            });
        }

        typography.AddFeature(new CanvasTypographyFeature
        {
            Name = CanvasTypographyFeatureName.Kerning,
            Parameter = 1
        });

        if (settings.UseOldstyleFigures)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.OldStyleFigures,
                Parameter = 1
            });
        }

        if (settings.UseSmallCaps)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.SmallCapitals,
                Parameter = 1
            });
        }

        object start = TypographyStartIndexType == typeof(uint) ? (uint)0 : 0;
        object len = TypographyLengthType == typeof(uint) ? (uint)textLength : textLength;

        try
        {
            SetTypographyMethod.Invoke(layout, new object?[] { start, len, typography });
        }
        catch
        {
        }
    }

    public static CanvasTextLayout CreateTextLayout(
        ICanvasResourceCreator resourceCreator,
        TextStyle textStyle,
        string text,
        float width,
        float height,
        IReadOnlyList<TextStyleSpan>? spans,
        float opacity = 1f,
        TypographySettings? typographySettings = null,
        bool isRtl = false)
    {
        typographySettings ??= TypographySettings.ComicDefault;
        var effectiveHyphenationLevel = ResolveHyphenationLevel(textStyle);

        var displayText = text ?? string.Empty;
        if (typographySettings.EnableHyphenation && !string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            displayText = Hyphenation.ApplyHyphenation(displayText, textStyle.HyphenationLocale, effectiveHyphenationLevel);
        }

        var format = CreateTextFormat(
            textStyle,
            spans,
            typographySettings,
            isRtl,
            languageTag: null,
            preferVerticalCjk: false,
            sampleText: displayText);
        var layout = new CanvasTextLayout(resourceCreator, displayText, format, width, height);
        var length = displayText.Length;

        ApplyTracking(layout, textStyle, length);

        if (textStyle.Underline && length > 0)
        {
            layout.SetUnderline(0, length, true);
        }

        if (textStyle.Script != TextScript.Normal && length > 0)
        {
            ApplyTypography(layout, 0, length, textStyle.Script);
        }

        ApplyTypographyFeatures(layout, length, typographySettings);

        if (spans != null && spans.Count > 0 && length > 0)
        {
            ApplyTextStyleSpans(layout, textStyle, spans, length, opacity, sampleText: displayText);
        }

        return layout;
    }

    private static int ResolveHyphenationLevel(TextStyle textStyle)
    {
        return 0;
    }

    public sealed class TextLayoutDiagnostics
    {
        public int LineCount { get; init; }
        public int CharacterCount { get; init; }
        public float[] LineWidths { get; init; } = Array.Empty<float>();
        public int[] LineBadness { get; init; } = Array.Empty<int>();
        public float AverageLineWidth { get; init; }
        public float MaxLineWidth { get; init; }
        public float MinLineWidth { get; init; }
        public float RagVariance { get; init; }
        public float LayoutWidth { get; init; }
        public float LayoutHeight { get; init; }
        public float ContainerWidth { get; init; }
        public float ContainerHeight { get; init; }
        public bool WasShrunk { get; init; }
        public bool WasTrackAdjusted { get; init; }
        public float EffectiveFontSize { get; init; }
        public float EffectiveTracking { get; init; }
        public int TotalBadness { get; init; }
        public int MaxBadness { get; init; }
        public int QualityScore { get; init; }
        public string DiagnosticText => BuildDiagnosticText();

        private string BuildDiagnosticText()
        {
            var lines = new List<string>
            {
                $"Lines: {LineCount}",
                $"Characters: {CharacterCount}",
                $"Text: {LayoutWidth:F0}×{LayoutHeight:F0}"
            };

            if (WasShrunk)
            {
                lines.Add($"Font scaled to: {EffectiveFontSize:F1}pt");
            }

            if (WasTrackAdjusted)
            {
                lines.Add($"Tracking adjusted to: {EffectiveTracking:F3}");
            }

            if (LineCount > 1)
            {
                lines.Add($"Rag variance: {RagVariance:F1}px");

                if (MaxBadness > 50)
                {
                    lines.Add($"Max badness: {MaxBadness}");
                }

                lines.Add($"Quality: {QualityScore}%");
            }

            return string.Join("\n", lines);
        }
    }

    public sealed class FittedTextLayout : IDisposable
    {
        public FittedTextLayout(CanvasTextLayout layout, TextStyle effectiveStyle, bool isOverflowing, TextLayoutDiagnostics? diagnostics = null)
        {
            Layout = layout;
            EffectiveStyle = effectiveStyle;
            IsOverflowing = isOverflowing;
            Diagnostics = diagnostics;
        }

        public CanvasTextLayout Layout { get; }
        public TextStyle EffectiveStyle { get; }
        public bool IsOverflowing { get; }
        public TextLayoutDiagnostics? Diagnostics { get; }

        public void Dispose()
        {
            Layout.Dispose();
        }
    }

    public static FittedTextLayout CreateFittedTextLayout(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan>? spans,
        float width,
        float height,
        bool allowFit,
        float opacity = 1f,
        bool computeDiagnostics = false,
        TypographySettings? typographySettings = null,
        bool isRtl = false)
    {
        spans ??= Array.Empty<TextStyleSpan>();
        var effectiveStyle = baseStyle;
        IReadOnlyList<TextStyleSpan> effectiveSpans = spans;
        var wasShrunk = false;
        var wasTrackAdjusted = false;

        var layout = CreateTextLayout(resourceCreator, effectiveStyle, text, width, height, effectiveSpans, opacity, typographySettings, isRtl);
        var isOverflowing = IsOverflowing(layout, width, height);

        if (allowFit && !string.IsNullOrEmpty(text))
        {
            switch (baseStyle.FitMode)
            {
                case TextFitMode.ShrinkToFit:
                    if (TryFindOptimalShrinkScale(resourceCreator, text, baseStyle, spans, width, height, opacity, isOverflowing, isRtl, out var scale))
                    {
                        effectiveStyle = ScaleFontSize(baseStyle, scale);
                        effectiveSpans = AdjustSpans(spans, style => ScaleFontSize(style, scale));
                        layout.Dispose();
                        layout = CreateTextLayout(resourceCreator, effectiveStyle, text, width, height, effectiveSpans, opacity, typographySettings, isRtl);
                        isOverflowing = IsOverflowing(layout, width, height);
                        wasShrunk = Math.Abs(scale - 1f) > 0.01f;
                    }
                    break;

                case TextFitMode.TrackToFit:
                    if (TryFindOptimalTracking(resourceCreator, text, baseStyle, spans, width, height, opacity, isOverflowing, isRtl, out var tracking))
                    {
                        var delta = tracking - baseStyle.Tracking;
                        effectiveStyle = baseStyle.With(tracking: tracking);
                        effectiveSpans = AdjustSpans(spans, style => style.With(tracking: style.Tracking + delta));
                        layout.Dispose();
                        layout = CreateTextLayout(resourceCreator, effectiveStyle, text, width, height, effectiveSpans, opacity, typographySettings, isRtl);
                        isOverflowing = IsOverflowing(layout, width, height);
                        wasTrackAdjusted = Math.Abs(tracking - baseStyle.Tracking) > 0.001f;
                    }
                    break;
            }
        }

        TextLayoutDiagnostics? diagnostics = null;
        if (computeDiagnostics)
        {
            diagnostics = ComputeDiagnostics(layout, width, height, effectiveStyle, wasShrunk, wasTrackAdjusted);
        }

        return new FittedTextLayout(layout, effectiveStyle, isOverflowing, diagnostics);
    }

    public static TextLayoutDiagnostics ComputeDiagnostics(
        CanvasTextLayout layout,
        float containerWidth,
        float containerHeight,
        TextStyle effectiveStyle,
        bool wasShrunk,
        bool wasTrackAdjusted)
    {
        var lineMetrics = layout.LineMetrics;
        var lineCount = lineMetrics.Length;
        var characterCount = lineMetrics.Sum(metric => metric.CharacterCount);

        var clusterMetrics = layout.ClusterMetrics;
        var lineWidths = ComputeLineWidthsFromClusters(lineMetrics, clusterMetrics);

        var lineBadness = new int[lineCount];
        int totalBadness = 0;
        int maxBadness = 0;

        float totalWidth = 0f;
        float maxWidth = 0f;
        float minWidth = float.MaxValue;

        for (int i = 0; i < lineWidths.Length; i++)
        {
            var lineWidth = lineWidths[i];
            totalWidth += lineWidth;
            if (lineWidth > maxWidth) maxWidth = lineWidth;
            if (lineWidth < minWidth && lineWidth > 0) minWidth = lineWidth;

            var isLastLine = (i == lineCount - 1);
            var badness = CalculateLineBadness(lineWidth, containerWidth, isLastLine);
            lineBadness[i] = badness;
            totalBadness += badness;
            if (badness > maxBadness) maxBadness = badness;
        }

        if (minWidth == float.MaxValue) minWidth = 0f;

        var avgWidth = lineCount > 0 ? totalWidth / lineCount : 0f;
        var avgBadness = lineCount > 0 ? totalBadness / lineCount : 0;

        float sumSquaredDiff = 0f;
        for (int i = 0; i < lineWidths.Length; i++)
        {
            var diff = maxWidth - lineWidths[i];
            sumSquaredDiff += diff * diff;
        }
        var ragVariance = lineCount > 0 ? MathF.Sqrt(sumSquaredDiff / lineCount) : 0f;

        var qualityScore = CalculateQualityScore(avgBadness, maxBadness, ragVariance, avgWidth);

        return new TextLayoutDiagnostics
        {
            LineCount = lineCount,
            CharacterCount = characterCount,
            LineWidths = lineWidths,
            LineBadness = lineBadness,
            AverageLineWidth = avgWidth,
            MaxLineWidth = maxWidth,
            MinLineWidth = minWidth,
            RagVariance = ragVariance,
            LayoutWidth = (float)layout.LayoutBounds.Width,
            LayoutHeight = (float)layout.LayoutBounds.Height,
            ContainerWidth = containerWidth,
            ContainerHeight = containerHeight,
            WasShrunk = wasShrunk,
            WasTrackAdjusted = wasTrackAdjusted,
            EffectiveFontSize = effectiveStyle.FontSize,
            EffectiveTracking = effectiveStyle.Tracking,
            TotalBadness = totalBadness,
            MaxBadness = maxBadness,
            QualityScore = qualityScore
        };
    }

    private static int CalculateLineBadness(float lineWidth, float maxWidth, bool isLastLine)
    {
        if (lineWidth > maxWidth)
        {
            return 10000; // Overfull
        }

        var ratio = lineWidth / maxWidth;

        if (isLastLine && ratio >= 0.3f)
        {
            return Math.Max(0, (int)((1f - ratio) * 20));
        }

        if (ratio >= 0.95f)
        {
            return 0; // Nearly perfect
        }

        var emptyRatio = 1f - ratio;
        var badness = (int)(emptyRatio * emptyRatio * emptyRatio * 10000);
        return Math.Min(badness, 10000);
    }

    private static int CalculateQualityScore(int avgBadness, int maxBadness, float ragVariance, float avgWidth)
    {
        var score = 100;

        if (avgBadness > 20) score -= Math.Min(30, (avgBadness - 20) / 5);

        if (maxBadness > 50) score -= Math.Min(25, (maxBadness - 50) / 10);

        if (avgWidth > 0)
        {
            var ragPct = ragVariance / avgWidth;
            if (ragPct > 0.2f) score -= Math.Min(15, (int)((ragPct - 0.2f) * 50));
        }

        return Math.Clamp(score, 0, 100);
    }

    private static float[] ComputeLineWidthsFromClusters(
        CanvasLineMetrics[] lineMetrics,
        CanvasClusterMetrics[] clusterMetrics)
    {
        var lineWidths = new float[lineMetrics.Length];
        int clusterIndex = 0;

        for (int lineIndex = 0; lineIndex < lineMetrics.Length; lineIndex++)
        {
            var charCount = lineMetrics[lineIndex].CharacterCount;
            float lineWidth = 0f;
            int charsProcessed = 0;

            while (charsProcessed < charCount && clusterIndex < clusterMetrics.Length)
            {
                lineWidth += clusterMetrics[clusterIndex].Width;
                charsProcessed += clusterMetrics[clusterIndex].CharacterCount;
                clusterIndex++;
            }

            lineWidths[lineIndex] = lineWidth;
        }

        return lineWidths;
    }

    public static void ApplyTracking(CanvasTextLayout layout, TextStyle style, int textLength)
    {
        ApplyTracking(layout, style, 0, textLength);
    }

    public static void ApplyTracking(CanvasTextLayout layout, TextStyle style, int startIndex, int length)
    {
        if (layout == null || length <= 0) return;
        if (SetCharacterSpacingMethod == null) return;
        if (Math.Abs(style.Tracking) < 0.0001f) return;

        var spacing = style.Tracking * style.FontSize;
        var leading = spacing / 2f;
        var trailing = spacing / 2f;
        var minAdvance = 0f;

        object start = StartIndexType == typeof(uint) ? (uint)startIndex : startIndex;
        object len = LengthType == typeof(uint) ? (uint)length : length;

        try
        {
            SetCharacterSpacingMethod.Invoke(layout, new object?[] { start, len, leading, trailing, minAdvance });
        }
        catch
        {
        }
    }

    public static void ApplyFillColor(CanvasTextLayout layout, Windows.UI.Color color, int startIndex, int length)
    {
        if (layout == null || length <= 0) return;
        if (SetFillColorMethod == null) return;
        if (FillColorIndex < 0 || FillStartIndex < 0 || FillLengthIndex < 0) return;

        var parameters = SetFillColorMethod.GetParameters();
        var args = new object?[3];
        args[FillColorIndex] = color;
        args[FillStartIndex] = parameters[FillStartIndex].ParameterType == typeof(uint) ? (uint)startIndex : startIndex;
        args[FillLengthIndex] = parameters[FillLengthIndex].ParameterType == typeof(uint) ? (uint)length : length;

        try
        {
            SetFillColorMethod.Invoke(layout, args);
        }
        catch
        {
        }
    }

    public static void ApplyTextStyleSpans(
        CanvasTextLayout layout,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        int textLength,
        float opacity = 1f,
        string? languageTag = null,
        bool preferVerticalCjk = false,
        string? sampleText = null)
    {
        if (layout == null || spans == null || spans.Count == 0) return;
        if (textLength == 0) return;

        foreach (var span in spans)
        {
            if (span.Length <= 0) continue;

            var start = Math.Clamp(span.Start, 0, textLength);
            var length = Math.Clamp(span.Length, 0, textLength - start);
            if (length <= 0) continue;

            var style = span.Style ?? baseStyle;
            if (TextStyleUtilities.AreInlineEquivalent(style, baseStyle)) continue;

            if (!string.Equals(style.FontFamily, baseStyle.FontFamily, StringComparison.OrdinalIgnoreCase))
            {
                var spanFontFamily = CjkFontSupport.ResolveFontFamily(
                    style.FontFamily,
                    languageTag,
                    sampleText,
                    preferVerticalCjk);
                layout.SetFontFamily(start, length, spanFontFamily);
            }

            if (Math.Abs(style.FontSize - baseStyle.FontSize) > 0.001f)
            {
                layout.SetFontSize(start, length, style.FontSize);
            }

            if (style.Bold != baseStyle.Bold)
            {
                layout.SetFontWeight(start, length, style.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal);
            }

            if (style.Italic != baseStyle.Italic)
            {
                layout.SetFontStyle(start, length, style.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal);
            }

            if (style.Underline != baseStyle.Underline)
            {
                layout.SetUnderline(start, length, style.Underline);
            }

            if (style.Script != baseStyle.Script)
            {
                ApplyTypography(layout, start, length, style.Script);
            }

            if (!style.TextColor.Equals(baseStyle.TextColor))
            {
                ApplyFillColor(layout, ApplyOpacity(style.TextColor, opacity), start, length);
            }

            if (Math.Abs(style.Tracking - baseStyle.Tracking) > 0.0001f)
            {
                ApplyTracking(layout, style, start, length);
            }
        }
    }

    private static Windows.UI.Color ApplyOpacity(Color color, float opacity)
    {
        var clamped = Math.Clamp(opacity, 0f, 1f);
        var alpha = (byte)Math.Clamp(color.A * clamped, 0f, 255f);
        return Windows.UI.Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static void ApplyTypography(CanvasTextLayout layout, int startIndex, int length, TextScript script)
    {
        if (layout == null || length <= 0) return;
        if (SetTypographyMethod == null) return;

        var typography = new CanvasTypography();
        if (script == TextScript.Superscript)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.Superscript,
                Parameter = 1
            });
        }
        else if (script == TextScript.Subscript)
        {
            typography.AddFeature(new CanvasTypographyFeature
            {
                Name = CanvasTypographyFeatureName.Subscript,
                Parameter = 1
            });
        }

        object start = TypographyStartIndexType == typeof(uint) ? (uint)startIndex : startIndex;
        object len = TypographyLengthType == typeof(uint) ? (uint)length : length;

        try
        {
            SetTypographyMethod.Invoke(layout, new object?[] { start, len, typography });
        }
        catch
        {
        }
    }

    public static Vector2 GetTextOrigin(Rect textBounds, CanvasTextLayout layout, float verticalOffset, bool clampToBounds = false)
    {
        var bounds = layout.DrawBounds;
        if (bounds.Height <= 0)
        {
            bounds = layout.LayoutBounds;
        }

        var centeredY = textBounds.Y
                        + (textBounds.Height - (float)bounds.Height) * 0.5f
                        - (float)bounds.Y
                        + verticalOffset;

        if (clampToBounds)
        {
            var drawStartY = centeredY + (float)bounds.Y;

            if (drawStartY < textBounds.Y)
            {
                centeredY = textBounds.Y - (float)bounds.Y;
            }
        }

        return new Vector2(textBounds.X, centeredY);
    }

    public static Vector2 GetLayoutAlignmentOffset(CanvasTextLayout layout)
    {
        var layoutBounds = layout.LayoutBounds;
        var drawBounds = layout.DrawBounds;
        if (drawBounds.Height <= 0 && drawBounds.Width <= 0)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            (float)(drawBounds.X - layoutBounds.X),
            (float)(drawBounds.Y - layoutBounds.Y));
    }

    public static float GetManualAlignedLineOriginX(
        CanvasTextLayout layout,
        float containerLeft,
        float containerWidth,
        TextAlignment alignment)
    {
        var drawBounds = layout.DrawBounds;
        var layoutBounds = layout.LayoutBounds;
        var hasDrawBounds = drawBounds.Width > 0;

        var visualWidth = hasDrawBounds
            ? (float)drawBounds.Width
            : (float)layoutBounds.Width;
        var visualOffsetX = hasDrawBounds
            ? (float)drawBounds.X
            : (float)layoutBounds.X;

        float alignedLeft = containerLeft;
        if (alignment == TextAlignment.Center)
        {
            alignedLeft += (containerWidth - visualWidth) * 0.5f;
        }
        else if (alignment == TextAlignment.Right)
        {
            alignedLeft += containerWidth - visualWidth;
        }

        return alignedLeft - visualOffsetX;
    }

    public static bool TryGetCaretRegion(CanvasTextLayout layout, int textLength, int caretIndex, out CanvasTextLayoutRegion region)
    {
        region = default;
        if (textLength <= 0) return false;

        var regions = layout.GetCharacterRegions(0, textLength);
        var regionList = regions as CanvasTextLayoutRegion[] ?? regions.ToArray();
        if (regionList.Length == 0) return false;

        var safeIndex = caretIndex >= textLength ? textLength - 1 : Math.Clamp(caretIndex, 0, textLength - 1);
        if (safeIndex >= regionList.Length)
        {
            safeIndex = regionList.Length - 1;
        }

        region = regionList[safeIndex];
        return true;
    }

    public static bool TryGetCaretPosition(CanvasTextLayout layout, int textLength, int caretIndex, out Vector2 caret)
    {
        caret = default;
        if (textLength <= 0) return false;

        var safeIndex = Math.Clamp(caretIndex, 0, textLength);
        var useTrailingEdge = safeIndex >= textLength;
        var layoutIndex = useTrailingEdge ? textLength - 1 : safeIndex;
        if (layoutIndex < 0) return false;

        caret = layout.GetCaretPosition(layoutIndex, useTrailingEdge);
        return true;
    }

    private static bool IsOverflowing(CanvasTextLayout layout, float width, float height)
    {
        if (width <= 0 || height <= 0) return false;
        var layoutWidth = (float)layout.LayoutBounds.Width;
        var layoutHeight = (float)layout.LayoutBounds.Height;
        return layoutWidth > width + 0.5f || layoutHeight > height + 0.5f;
    }

    private static bool TryFindOptimalShrinkScale(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        bool isCurrentlyOverflowing,
        bool isRtl,
        out float scale)
    {
        scale = 1f;
        if (string.IsNullOrEmpty(text)) return false;

        var minScale = MinFitFontSize <= 0 ? 1f : MinFitFontSize / baseStyle.FontSize;
        if (minScale >= 1f)
        {
            return false;
        }

        minScale = Math.Clamp(minScale, 0.25f, 1f);

        if (!isCurrentlyOverflowing)
        {
            using var currentLayout = CreateTextLayout(
                resourceCreator, baseStyle, text, width, height,
                spans.Count > 0 ? spans : null, opacity, isRtl: isRtl);

            var layoutHeight = (float)currentLayout.LayoutBounds.Height;
            var heightRatio = layoutHeight / height;

            if (heightRatio < 0.9f)
            {
                return false;
            }
        }

        var bestScale = minScale;
        var foundFit = false;
        float low = minScale;
        float high = 1f;

        for (int i = 0; i < FitIterations; i++)
        {
            var mid = (low + high) / 2f;
            using var layout = CreateTextLayout(
                resourceCreator,
                ScaleFontSize(baseStyle, mid),
                text,
                width,
                height,
                AdjustSpans(spans, style => ScaleFontSize(style, mid)),
                opacity,
                isRtl: isRtl);

            if (IsOverflowing(layout, width, height))
            {
                high = mid;
            }
            else
            {
                foundFit = true;
                bestScale = mid;
                low = mid;
            }
        }

        if (foundFit && bestScale > minScale)
        {
            scale = bestScale * 0.98f; // 2% margin for safety
            scale = Math.Max(scale, minScale);
        }
        else
        {
            scale = minScale;
        }

        return Math.Abs(scale - 1f) > 0.01f;
    }

    private static bool TryFindOptimalTracking(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        bool isCurrentlyOverflowing,
        bool isRtl,
        out float tracking)
    {
        tracking = baseStyle.Tracking;
        if (string.IsNullOrEmpty(text)) return false;

        if (isCurrentlyOverflowing)
        {
            return TryFindTightenedTracking(resourceCreator, text, baseStyle, spans, width, height, opacity, isRtl, out tracking);
        }

        using var currentLayout = CreateTextLayout(
            resourceCreator, baseStyle, text, width, height,
            spans.Count > 0 ? spans : null, opacity, isRtl: isRtl);

        var lineMetrics = currentLayout.LineMetrics;
        if (lineMetrics.Length <= 1)
        {
            var lineWidth = (float)currentLayout.LayoutBounds.Width;
            var fillRatio = lineWidth / width;

            if (fillRatio < 0.85f && fillRatio > 0.5f)
            {
                return TryFindLoosenedTracking(resourceCreator, text, baseStyle, spans, width, height, opacity, isRtl, out tracking);
            }
            return false;
        }

        var clusterMetrics = currentLayout.ClusterMetrics;
        var lineWidths = ComputeLineWidthsFromClusters(lineMetrics, clusterMetrics);

        float avgFill = 0f;
        for (int i = 0; i < lineWidths.Length - 1; i++) // Skip last line
        {
            avgFill += lineWidths[i] / width;
        }
        avgFill /= Math.Max(1, lineWidths.Length - 1);

        if (avgFill < 0.8f && avgFill > 0.5f)
        {
            return TryFindLoosenedTracking(resourceCreator, text, baseStyle, spans, width, height, opacity, isRtl, out tracking);
        }

        return false;
    }

    private static bool TryFindTightenedTracking(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        bool isRtl,
        out float tracking)
    {
        tracking = baseStyle.Tracking;

        var minTracking = MinFitTracking;
        if (baseStyle.Tracking <= minTracking)
        {
            return false;
        }

        var bestTracking = minTracking;
        var foundFit = false;
        float low = minTracking;
        float high = baseStyle.Tracking;

        for (int i = 0; i < FitIterations; i++)
        {
            var mid = (low + high) / 2f;
            var delta = mid - baseStyle.Tracking;
            using var layout = CreateTextLayout(
                resourceCreator,
                baseStyle.With(tracking: mid),
                text,
                width,
                height,
                AdjustSpans(spans, style => style.With(tracking: style.Tracking + delta)),
                opacity,
                isRtl: isRtl);

            if (IsOverflowing(layout, width, height))
            {
                high = mid;
            }
            else
            {
                foundFit = true;
                bestTracking = mid;
                low = mid;
            }
        }

        tracking = foundFit ? bestTracking : minTracking;
        return Math.Abs(tracking - baseStyle.Tracking) > 0.005f;
    }

    private static bool TryFindLoosenedTracking(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        bool isRtl,
        out float tracking)
    {
        tracking = baseStyle.Tracking;

        var maxTracking = Math.Min(MaxFitTracking, baseStyle.Tracking + 0.08f);
        if (baseStyle.Tracking >= maxTracking)
        {
            return false;
        }

        float low = baseStyle.Tracking;
        float high = maxTracking;
        var bestTracking = baseStyle.Tracking;
        var foundImprovement = false;

        for (int i = 0; i < FitIterations; i++)
        {
            var mid = (low + high) / 2f;
            var delta = mid - baseStyle.Tracking;
            using var layout = CreateTextLayout(
                resourceCreator,
                baseStyle.With(tracking: mid),
                text,
                width,
                height,
                AdjustSpans(spans, style => style.With(tracking: style.Tracking + delta)),
                opacity,
                isRtl: isRtl);

            if (IsOverflowing(layout, width, height))
            {
                high = mid;
            }
            else
            {
                foundImprovement = true;
                bestTracking = mid;
                low = mid;
            }
        }

        if (foundImprovement && bestTracking > baseStyle.Tracking)
        {
            tracking = baseStyle.Tracking + (bestTracking - baseStyle.Tracking) * 0.9f;
            return Math.Abs(tracking - baseStyle.Tracking) > 0.005f;
        }

        return false;
    }

    private static bool TryFindShrinkScale(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        out float scale)
    {
        return TryFindOptimalShrinkScale(resourceCreator, text, baseStyle, spans, width, height, opacity, true, isRtl: false, out scale);
    }

    private static bool TryFindTracking(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle baseStyle,
        IReadOnlyList<TextStyleSpan> spans,
        float width,
        float height,
        float opacity,
        out float tracking)
    {
        return TryFindTightenedTracking(resourceCreator, text, baseStyle, spans, width, height, opacity, isRtl: false, out tracking);
    }

    private static TextStyle ScaleFontSize(TextStyle style, float scale)
    {
        return style.With(fontSize: Math.Max(1f, style.FontSize * scale));
    }

    private static List<TextStyleSpan> AdjustSpans(
        IReadOnlyList<TextStyleSpan> spans,
        Func<TextStyle, TextStyle> adjust)
    {
        if (spans.Count == 0) return new List<TextStyleSpan>();

        var list = new List<TextStyleSpan>(spans.Count);
        foreach (var span in spans)
        {
            list.Add(new TextStyleSpan(span.Start, span.Length, adjust(span.Style)));
        }
        return list;
    }
}
