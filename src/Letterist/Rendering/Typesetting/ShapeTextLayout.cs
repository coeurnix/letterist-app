using Letterist.Model;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Letterist.Rendering.Typesetting;

public sealed class ShapeTextLayout
{
    private const char SoftHyphen = '\u00AD';

    public readonly struct LineWidthInfo
    {
        public LineWidthInfo(float yOffset, float width, float leftInset)
        {
            YOffset = yOffset;
            Width = width;
            LeftInset = leftInset;
        }

        public float YOffset { get; }

        public float Width { get; }

        public float LeftInset { get; }
    }

    public static LineWidthInfo[] ComputeLineWidths(
        BalloonShape shape,
        Rect bounds,
        BalloonStyle style,
        float lineHeight,
        int lineCount)
    {
        if (lineCount <= 0) return Array.Empty<LineWidthInfo>();

        var result = new LineWidthInfo[lineCount];
        var centerY = bounds.Height / 2f;
        var totalTextHeight = lineHeight * lineCount;
        var textStartY = centerY - totalTextHeight / 2f;

        for (int i = 0; i < lineCount; i++)
        {
            var lineCenterY = textStartY + (i + 0.5f) * lineHeight - centerY;

            var info = ComputeWidthAtY(shape, bounds, style, lineCenterY);
            result[i] = info;
        }

        return result;
    }

    public static LineWidthInfo ComputeWidthAtY(
        BalloonShape shape,
        Rect bounds,
        BalloonStyle style,
        float yOffsetFromCenter)
    {
        switch (shape)
        {
            case BalloonShape.Oval:
            case BalloonShape.Thought:
            case BalloonShape.Splat:
            case BalloonShape.Whisper:
                return ComputeEllipseWidth(bounds, style, yOffsetFromCenter);

            case BalloonShape.Burst:
                return ComputeBurstWidth(bounds, style, yOffsetFromCenter);

            case BalloonShape.RoundedRect:
                return ComputeRoundedRectWidth(bounds, style, yOffsetFromCenter);

            default:
                return ComputeRectangleWidth(bounds, style);
        }
    }

    private static LineWidthInfo ComputeEllipseWidth(
        Rect bounds,
        BalloonStyle style,
        float yOffsetFromCenter)
    {
        var a = bounds.Width / 2f;
        var b = bounds.Height / 2f;

        var effectiveA = a - (style.PaddingLeft + style.PaddingRight) / 2f;
        var effectiveB = b - (style.PaddingTop + style.PaddingBottom) / 2f;

        effectiveA = MathF.Max(effectiveA, 20f);
        effectiveB = MathF.Max(effectiveB, 10f);

        var absY = MathF.Abs(yOffsetFromCenter);
        if (absY >= effectiveB)
        {
            var centerWidth = 2f * effectiveA;
            return new LineWidthInfo(yOffsetFromCenter, MathF.Max(centerWidth * 0.3f, 40f), a - effectiveA * 0.15f);
        }

        var yRatio = yOffsetFromCenter / effectiveB;
        var xAtY = effectiveA * MathF.Sqrt(1f - yRatio * yRatio);

        var textWidth = 2f * xAtY;

        textWidth = MathF.Max(textWidth, 40f);

        var leftInset = a - xAtY;

        return new LineWidthInfo(yOffsetFromCenter, textWidth, leftInset);
    }

    private static LineWidthInfo ComputeBurstWidth(
        Rect bounds,
        BalloonStyle style,
        float yOffsetFromCenter)
    {
        var a = bounds.Width / 2f * 0.6f;
        var b = bounds.Height / 2f * 0.6f;

        var effectiveA = MathF.Max(a - style.PaddingLeft, 20f);
        var effectiveB = MathF.Max(b - style.PaddingTop, 10f);

        var absY = MathF.Abs(yOffsetFromCenter);
        if (absY >= effectiveB)
        {
            return new LineWidthInfo(yOffsetFromCenter, 40f, bounds.Width / 2f - 20f);
        }

        var yRatio = yOffsetFromCenter / effectiveB;
        var xAtY = effectiveA * MathF.Sqrt(1f - yRatio * yRatio);
        var textWidth = MathF.Max(2f * xAtY, 40f);
        var leftInset = bounds.Width / 2f - xAtY;

        return new LineWidthInfo(yOffsetFromCenter, textWidth, leftInset);
    }

    private static LineWidthInfo ComputeRoundedRectWidth(
        Rect bounds,
        BalloonStyle style,
        float yOffsetFromCenter)
    {
        var radius = style.CornerRadius;
        var halfHeight = bounds.Height / 2f;
        var halfWidth = bounds.Width / 2f;

        var distFromEdge = halfHeight - MathF.Abs(yOffsetFromCenter);

        float horizontalInset = 0f;

        if (distFromEdge < radius && distFromEdge >= 0)
        {
            var d = distFromEdge;
            var insetFromCorner = radius - MathF.Sqrt(radius * radius - (radius - d) * (radius - d));
            horizontalInset = insetFromCorner;
        }

        var fullWidth = bounds.Width - 2f * horizontalInset;
        var textWidth = MathF.Max(fullWidth - style.PaddingLeft - style.PaddingRight, 40f);
        var leftInset = horizontalInset + style.PaddingLeft;

        return new LineWidthInfo(yOffsetFromCenter, textWidth, leftInset);
    }

    private static LineWidthInfo ComputeRectangleWidth(
        Rect bounds,
        BalloonStyle style)
    {
        var textWidth = MathF.Max(bounds.Width - style.PaddingLeft - style.PaddingRight, 40f);
        return new LineWidthInfo(0f, textWidth, style.PaddingLeft);
    }

    public static ShapeAwareLayoutResult CreateShapeAwareLayout(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle textStyle,
        BalloonShape shape,
        Rect bounds,
        BalloonStyle balloonStyle,
        TypographySettings? typographySettings = null,
        IReadOnlyList<TextStyleSpan>? spans = null,
        bool cursorBlinkState = false)
    {
        float inscribeFactor = shape switch
        {
            BalloonShape.Oval or BalloonShape.Thought or BalloonShape.Splat or BalloonShape.Whisper => 0.72f,
            BalloonShape.Burst => 0.60f,
            BalloonShape.RoundedRect when balloonStyle.CornerRadius > 0 =>
                1f - MathF.Min(balloonStyle.CornerRadius / MathF.Min(bounds.Width, bounds.Height), 0.15f) * 0.3f,
            _ => 1f
        };

        var inscribedWidth = bounds.Width * inscribeFactor;
        var inscribedHeight = bounds.Height * inscribeFactor;

        var contentLeft = (bounds.Width - inscribedWidth) / 2f + balloonStyle.PaddingLeft;
        var contentRight = (bounds.Width + inscribedWidth) / 2f - balloonStyle.PaddingRight;
        var availableTextWidth = MathF.Max(contentRight - contentLeft, 1f);
        var textWidth = MathF.Max(availableTextWidth, 40f);
        var textHeight = MathF.Max(inscribedHeight - balloonStyle.PaddingTop - balloonStyle.PaddingBottom, 16f);

        var format = TextLayoutUtilities.CreateTextFormat(textStyle, spans, typographySettings);
        using var layout = new CanvasTextLayout(resourceCreator, text, format, textWidth, textHeight);

        var lineMetrics = layout.LineMetrics;
        var lines = new List<ShapeAwareLine>();
        float y = 0f;

        var contentCenter = (contentLeft + contentRight) / 2f;
        var leftInset = contentCenter - textWidth / 2f;

        for (int i = 0; i < lineMetrics.Length; i++)
        {
            var lineText = ExtractLineText(text, layout, i);
            lines.Add(new ShapeAwareLine(
                lineText,
                leftInset,
                y,
                textWidth,
                lineMetrics[i].Height,
                lineMetrics[i].CharacterCount));
            y += lineMetrics[i].Height;
        }

        return new ShapeAwareLayoutResult(
            lines.ToArray(),
            y,
            textWidth,
            textHeight);
    }

    private static ShapeAwareLayoutResult CreateCurvedShapeLayout(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle textStyle,
        BalloonShape shape,
        Rect bounds,
        BalloonStyle balloonStyle,
        TypographySettings typographySettings,
        IReadOnlyList<TextStyleSpan>? spans = null)
    {
        var format = TextLayoutUtilities.CreateTextFormat(textStyle, spans, typographySettings);
        float lineHeight;
        using (var sampleLayout = new CanvasTextLayout(resourceCreator, "Xgp", format, 1000f, 1000f))
        {
            lineHeight = (float)sampleLayout.LayoutBounds.Height;
        }

        var halfHeight = bounds.Height / 2f;
        var usableHalfHeight = halfHeight - MathF.Max(balloonStyle.PaddingTop, balloonStyle.PaddingBottom);

        if (shape == BalloonShape.Oval || shape == BalloonShape.Thought || shape == BalloonShape.Splat || shape == BalloonShape.Whisper)
        {
            usableHalfHeight = halfHeight * 0.72f;
        }
        else if (shape == BalloonShape.Burst)
        {
            usableHalfHeight = halfHeight * 0.55f;
        }

        var lines = LayoutTextIterative(resourceCreator, text, textStyle, format, shape, bounds, balloonStyle, lineHeight, usableHalfHeight);

        if (lines.Count == 0)
        {
            return new ShapeAwareLayoutResult(
                Array.Empty<ShapeAwareLine>(),
                0f,
                bounds.Width,
                bounds.Height);
        }

        var result = new ShapeAwareLine[lines.Count];
        float totalHeight = 0f;
        float maxWidth = 0f;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            result[i] = new ShapeAwareLine(line.Text, line.X, totalHeight, line.Width, line.Height, line.CharacterCount);
            totalHeight += line.Height;
            if (line.Width > maxWidth) maxWidth = line.Width;
        }

        var containerHeight = bounds.Height - balloonStyle.PaddingTop - balloonStyle.PaddingBottom;

        return new ShapeAwareLayoutResult(
            result,
            totalHeight,
            maxWidth,
            containerHeight);
    }

    private static List<ShapeAwareLine> LayoutTextIterative(
        ICanvasResourceCreator resourceCreator,
        string text,
        TextStyle textStyle,
        CanvasTextFormat format,
        BalloonShape shape,
        Rect bounds,
        BalloonStyle balloonStyle,
        float lineHeight,
        float usableHalfHeight)
    {
        const int MaxIterations = 5;

        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<ShapeAwareLine>();
        }

        var breakableText = PrepareShapeLayoutTextForHyphenation(text, textStyle);

        var trackingPerChar = textStyle.Tracking * textStyle.FontSize;

        var breakingFormat = new CanvasTextFormat
        {
            FontFamily = format.FontFamily,
            FontSize = format.FontSize,
            FontWeight = format.FontWeight,
            FontStyle = format.FontStyle,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.Wrap, // Enable word wrapping for line breaking
            LocaleName = format.LocaleName // Preserve locale for hyphenation
        };

        var centerWidth = ComputeWidthAtY(shape, bounds, balloonStyle, 0f);

        float totalTextWidth;
        using (var measureLayout = new CanvasTextLayout(resourceCreator, breakableText, breakingFormat, 10000f, 10000f))
        {
            totalTextWidth = (float)measureLayout.LayoutBounds.Width;
            totalTextWidth += MathF.Max(0, (breakableText.Length - 1)) * trackingPerChar;
        }

        int estimatedLines = Math.Max(1, (int)MathF.Ceiling(totalTextWidth / (centerWidth.Width * 0.75f)));
        float estimatedHeight = estimatedLines * lineHeight;

        List<ShapeAwareLine>? bestLines = null;

        for (int iteration = 0; iteration < MaxIterations; iteration++)
        {
            float startY = -estimatedHeight / 2f;
            float currentY = startY + lineHeight / 2f;

            var lines = new List<ShapeAwareLine>();
            string remainingText = breakableText;
            int safetyCounter = 0;

            while (!string.IsNullOrEmpty(remainingText) && safetyCounter < 100)
            {
                safetyCounter++;

                var clampedY = MathF.Max(-usableHalfHeight, MathF.Min(usableHalfHeight, currentY));
                var widthInfo = ComputeWidthAtY(shape, bounds, balloonStyle, clampedY);

                var effectiveWidth = widthInfo.Width;
                if (MathF.Abs(trackingPerChar) > 0.001f)
                {
                    var avgCharWidth = textStyle.FontSize * 0.6f; // Rough estimate
                    var estimatedChars = effectiveWidth / avgCharWidth;
                    var trackingOverhead = estimatedChars * MathF.Abs(trackingPerChar);
                    effectiveWidth = MathF.Max(40f, widthInfo.Width - trackingOverhead);
                }

                using var lineLayout = new CanvasTextLayout(resourceCreator, remainingText, breakingFormat, effectiveWidth, 10000f);
                var lineMetrics = lineLayout.LineMetrics;

                if (lineMetrics.Length == 0)
                {
                    break;
                }

                var firstLine = lineMetrics[0];
                var lineCharCount = firstLine.CharacterCount;

                if (lineCharCount <= 0)
                {
                    lineCharCount = 1;
                }

                var lineText = remainingText.Substring(0, Math.Min(lineCharCount, remainingText.Length));
                var trimmedLineText = lineText.TrimEnd('\r', '\n');
                var endsWithSoftHyphen = trimmedLineText.EndsWith(SoftHyphen);
                var mappedCharacterCount = lineCharCount - lineText.Count(c => c == SoftHyphen);
                if (mappedCharacterCount < 0)
                {
                    mappedCharacterCount = 0;
                }

                trimmedLineText = trimmedLineText.Replace(SoftHyphen.ToString(), string.Empty);

                bool wasHyphenated = endsWithSoftHyphen || (lineCharCount < remainingText.Length &&
                                     !char.IsWhiteSpace(remainingText[lineCharCount - 1]) &&
                                     !trimmedLineText.EndsWith("-") &&
                                     lineCharCount > 1 &&
                                     (lineCharCount >= remainingText.Length || char.IsLetter(remainingText[lineCharCount])));

                if (wasHyphenated && !trimmedLineText.EndsWith("-"))
                {
                    trimmedLineText += "-";
                }

                float actualLineHeight = lineHeight;
                using (var finalMeasure = new CanvasTextLayout(resourceCreator, trimmedLineText, format, 10000f, 10000f))
                {
                    actualLineHeight = MathF.Max(lineHeight, (float)finalMeasure.LayoutBounds.Height);
                }

                lines.Add(new ShapeAwareLine(
                    trimmedLineText,
                    widthInfo.LeftInset,
                    0f, // Y will be set later
                    widthInfo.Width,
                    actualLineHeight,
                    mappedCharacterCount));

                remainingText = remainingText.Substring(lineCharCount).TrimStart('\r', '\n');
                currentY += actualLineHeight;

                if (currentY > usableHalfHeight * 1.8f)
                {
                    if (!string.IsNullOrEmpty(remainingText) && lines.Count > 0)
                    {
                        var lastLine = lines[lines.Count - 1];
                        lines[lines.Count - 1] = new ShapeAwareLine(
                            lastLine.Text + " " + remainingText.Replace("\r", " ").Replace("\n", " "),
                            lastLine.X,
                            lastLine.Y,
                            lastLine.Width,
                            lastLine.Height,
                            lastLine.CharacterCount + 1 + remainingText.Length); // +1 for the space
                    }
                    break;
                }
            }

            float actualHeight = 0f;
            foreach (var line in lines)
            {
                actualHeight += line.Height;
            }

            if (bestLines != null && MathF.Abs(actualHeight - estimatedHeight) < lineHeight * 0.25f)
            {
                bestLines = lines;
                break;
            }

            bestLines = lines;
            estimatedHeight = actualHeight;
        }

        if (bestLines != null && bestLines.Count > 0)
        {
            float totalHeight = 0f;
            foreach (var line in bestLines)
            {
                totalHeight += line.Height;
            }

            float startY = -totalHeight / 2f;
            float currentY = startY;
            var finalLines = new List<ShapeAwareLine>(bestLines.Count);

            for (int i = 0; i < bestLines.Count; i++)
            {
                var line = bestLines[i];
                float lineCenterY = currentY + line.Height / 2f;

                var clampedY = MathF.Max(-usableHalfHeight, MathF.Min(usableHalfHeight, lineCenterY));
                var widthInfo = ComputeWidthAtY(shape, bounds, balloonStyle, clampedY);

                finalLines.Add(new ShapeAwareLine(
                    line.Text,
                    widthInfo.LeftInset,
                    0f, // Will be converted to 0-based later
                    widthInfo.Width,
                    line.Height,
                    line.CharacterCount));

                currentY += line.Height;
            }

            return finalLines;
        }

        return bestLines ?? new List<ShapeAwareLine>();
    }

    internal static string PrepareShapeLayoutTextForHyphenation(string text, TextStyle style)
    {
        return text;
    }

    private static string[] SplitIntoWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return parts;
    }

    private static string ExtractLineText(string text, CanvasTextLayout layout, int lineIndex)
    {
        var lineMetrics = layout.LineMetrics;
        if (lineIndex >= lineMetrics.Length) return string.Empty;

        int startChar = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            startChar += lineMetrics[i].CharacterCount;
        }

        var length = lineMetrics[lineIndex].CharacterCount;
        if (startChar + length > text.Length)
        {
            length = text.Length - startChar;
        }

        return text.Substring(startChar, length).TrimEnd();
    }
}

public readonly struct ShapeAwareLine
{
    public ShapeAwareLine(string text, float x, float y, float width, float height, int characterCount = -1)
    {
        Text = text;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        CharacterCount = characterCount >= 0 ? characterCount : text.Length;
    }

    public string Text { get; }
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
    public int CharacterCount { get; }
}

public sealed class ShapeAwareLayoutResult
{
    public ShapeAwareLayoutResult(
        ShapeAwareLine[] lines,
        float totalHeight,
        float maxLineWidth,
        float containerHeight)
    {
        Lines = lines;
        TotalHeight = totalHeight;
        MaxLineWidth = maxLineWidth;
        ContainerHeight = containerHeight;
    }

    public ShapeAwareLine[] Lines { get; }
    public float TotalHeight { get; }
    public float MaxLineWidth { get; }
    public float ContainerHeight { get; }
    public bool IsOverflowing => TotalHeight > ContainerHeight;
}
