using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;

namespace Letterist.Rendering.Typesetting;

public sealed class LineBreaker
{
    private readonly ICanvasResourceCreator _resourceCreator;
    private readonly CanvasTextFormat _measureFormat;

    public LineBreaker(ICanvasResourceCreator resourceCreator, CanvasTextFormat measureFormat)
    {
        _resourceCreator = resourceCreator;
        _measureFormat = measureFormat;
    }

    public List<LineBreakResult> BreakLines(
        string text,
        float maxWidth,
        TypographySettings settings)
    {
        if (string.IsNullOrEmpty(text))
            return new List<LineBreakResult>();

        var words = TokenizeWords(text);
        if (words.Count == 0)
            return new List<LineBreakResult>();

        var wordWidths = MeasureWords(words);

        var breaks = FindBreakPoints(words, wordWidths, maxWidth, settings);

        if (settings.EnableLineBalancing && breaks.Count > 1)
        {
            breaks = BalanceLines(words, wordWidths, breaks, maxWidth, settings);
        }

        if (settings.PreventWidows && breaks.Count > 1)
        {
            breaks = PreventWidowLine(words, wordWidths, breaks, maxWidth, settings);
        }

        return breaks;
    }

    private List<WordToken> TokenizeWords(string text)
    {
        var words = new List<WordToken>();
        var current = new System.Text.StringBuilder();
        var startIndex = 0;
        var hasLeadingSpace = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    words.Add(new WordToken(
                        current.ToString(),
                        startIndex,
                        hasLeadingSpace));
                    current.Clear();
                    hasLeadingSpace = true;
                }
                else
                {
                    hasLeadingSpace = true;
                }
            }
            else
            {
                if (current.Length == 0)
                {
                    startIndex = i;
                }
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            words.Add(new WordToken(
                current.ToString(),
                startIndex,
                hasLeadingSpace));
        }

        return words;
    }

    private float[] MeasureWords(List<WordToken> words)
    {
        var widths = new float[words.Count];

        for (int i = 0; i < words.Count; i++)
        {
            using var layout = new CanvasTextLayout(
                _resourceCreator,
                words[i].Text,
                _measureFormat,
                float.MaxValue,
                float.MaxValue);

            widths[i] = (float)layout.LayoutBounds.Width;
        }

        return widths;
    }

    private float MeasureSpace()
    {
        using var layout = new CanvasTextLayout(
            _resourceCreator,
            " ",
            _measureFormat,
            float.MaxValue,
            float.MaxValue);

        return (float)layout.LayoutBounds.Width;
    }

    private List<LineBreakResult> FindBreakPoints(
        List<WordToken> words,
        float[] wordWidths,
        float maxWidth,
        TypographySettings settings)
    {
        if (words.Count == 0) return new List<LineBreakResult>();

        var spaceWidth = MeasureSpace();

        if (words.Count <= 2)
        {
            return FindBreakPointsGreedy(words, wordWidths, maxWidth, spaceWidth);
        }


        const float infiniteCost = float.MaxValue;
        const float linePenalty = 10f; // Base penalty for each line
        const float looseness = 0f; // 0 = tight, positive = looser

        var dp = new float[words.Count + 1];
        var prev = new int[words.Count + 1]; // Previous break point for optimal path

        for (int i = 0; i <= words.Count; i++)
        {
            dp[i] = infiniteCost;
            prev[i] = -1;
        }
        dp[0] = 0f; // Starting point has no cost

        for (int i = 0; i < words.Count; i++)
        {
            if (dp[i] >= infiniteCost) continue;

            float lineWidth = 0f;

            for (int j = i; j < words.Count; j++)
            {
                if (j > i) lineWidth += spaceWidth;
                lineWidth += wordWidths[j];

                if (lineWidth > maxWidth * 1.1f && j > i)
                {
                    break;
                }

                var isLastLine = (j == words.Count - 1);
                var demerits = CalculateDemerits(lineWidth, maxWidth, isLastLine, looseness);

                if (demerits >= infiniteCost) continue;

                var totalCost = dp[i] + demerits + linePenalty;

                if (totalCost < dp[j + 1])
                {
                    dp[j + 1] = totalCost;
                    prev[j + 1] = i;
                }
            }
        }

        var breakpoints = new List<int>();
        int current = words.Count;

        while (current > 0 && prev[current] >= 0)
        {
            breakpoints.Add(current - 1); // End word index
            current = prev[current];
        }

        if (breakpoints.Count == 0)
        {
            return FindBreakPointsGreedy(words, wordWidths, maxWidth, spaceWidth);
        }

        breakpoints.Reverse();

        var breaks = new List<LineBreakResult>();
        int lineStart = 0;

        foreach (var endWord in breakpoints)
        {
            float lineW = 0f;
            for (int w = lineStart; w <= endWord; w++)
            {
                if (w > lineStart) lineW += spaceWidth;
                lineW += wordWidths[w];
            }

            breaks.Add(new LineBreakResult(
                lineStart,
                endWord,
                lineW,
                CalculateLineBadness(lineW, maxWidth)));

            lineStart = endWord + 1;
        }

        return breaks;
    }

    private float CalculateDemerits(float lineWidth, float maxWidth, bool isLastLine, float looseness)
    {
        var ratio = lineWidth / maxWidth;

        if (isLastLine && ratio <= 1f && ratio >= 0.3f)
        {
            return 1f; // Minimal demerits for reasonable last lines
        }

        if (ratio > 1.05f)
        {
            return 100000f; // Very high penalty
        }

        float badness;
        if (ratio > 1f)
        {
            badness = 10000f * (ratio - 1f) * (ratio - 1f);
        }
        else if (ratio >= 0.95f)
        {
            badness = 0f;
        }
        else
        {
            var emptyRatio = 1f - ratio;
            badness = 100f * emptyRatio * emptyRatio * emptyRatio;
        }

        var demerits = (1f + badness) * (1f + badness);

        demerits += looseness * 10f;

        return demerits;
    }

    private List<LineBreakResult> FindBreakPointsGreedy(
        List<WordToken> words,
        float[] wordWidths,
        float maxWidth,
        float spaceWidth)
    {
        var breaks = new List<LineBreakResult>();
        var lineStart = 0;
        var lineWidth = 0f;

        for (int i = 0; i < words.Count; i++)
        {
            var wordWidth = wordWidths[i];
            var spaceNeeded = (i > lineStart) ? spaceWidth : 0f;

            if (lineWidth + spaceNeeded + wordWidth <= maxWidth || i == lineStart)
            {
                lineWidth += spaceNeeded + wordWidth;
            }
            else
            {
                breaks.Add(new LineBreakResult(
                    lineStart,
                    i - 1,
                    lineWidth,
                    CalculateLineBadness(lineWidth, maxWidth)));

                lineStart = i;
                lineWidth = wordWidth;
            }
        }

        if (lineStart < words.Count)
        {
            breaks.Add(new LineBreakResult(
                lineStart,
                words.Count - 1,
                lineWidth,
                CalculateLineBadness(lineWidth, maxWidth)));
        }

        return breaks;
    }

    private int CalculateLineBadness(float lineWidth, float maxWidth)
    {
        if (lineWidth > maxWidth)
        {
            return 10000;
        }

        var ratio = lineWidth / maxWidth;
        if (ratio >= 0.95f)
        {
            return 0; // Nearly full - perfect
        }

        var emptyRatio = 1f - ratio;
        var badness = (int)(emptyRatio * emptyRatio * emptyRatio * 10000);
        return Math.Min(badness, 10000);
    }

    private List<LineBreakResult> BalanceLines(
        List<WordToken> words,
        float[] wordWidths,
        List<LineBreakResult> breaks,
        float maxWidth,
        TypographySettings settings)
    {
        if (breaks.Count <= 1)
            return breaks;

        var spaceWidth = MeasureSpace();
        var totalWidth = 0f;
        for (int i = 0; i < wordWidths.Length; i++)
        {
            totalWidth += wordWidths[i];
            if (i > 0) totalWidth += spaceWidth;
        }

        var avgLineWidth = totalWidth / breaks.Count;

        var minWidth = float.MaxValue;
        var maxLineWidth = 0f;
        foreach (var line in breaks)
        {
            if (line.Width < minWidth) minWidth = line.Width;
            if (line.Width > maxLineWidth) maxLineWidth = line.Width;
        }

        var balanceRatio = minWidth / maxLineWidth;
        if (balanceRatio >= settings.LineBalanceTarget)
        {
            return breaks; // Already balanced enough
        }

        var newBreaks = new List<LineBreakResult>();
        var lineStart = 0;
        var lineWidth = 0f;
        var targetWidth = Math.Min(avgLineWidth * 1.1f, maxWidth);

        for (int i = 0; i < words.Count; i++)
        {
            var wordWidth = wordWidths[i];
            var spaceNeeded = (i > lineStart) ? spaceWidth : 0f;
            var newLineWidth = lineWidth + spaceNeeded + wordWidth;

            var shouldBreak = lineWidth >= targetWidth * 0.8f &&
                              newLineWidth > targetWidth &&
                              i > lineStart;

            if (shouldBreak)
            {
                newBreaks.Add(new LineBreakResult(
                    lineStart,
                    i - 1,
                    lineWidth,
                    CalculateLineBadness(lineWidth, maxWidth)));

                lineStart = i;
                lineWidth = wordWidth;
            }
            else if (newLineWidth > maxWidth && i > lineStart)
            {
                newBreaks.Add(new LineBreakResult(
                    lineStart,
                    i - 1,
                    lineWidth,
                    CalculateLineBadness(lineWidth, maxWidth)));

                lineStart = i;
                lineWidth = wordWidth;
            }
            else
            {
                lineWidth = newLineWidth;
            }
        }

        if (lineStart < words.Count)
        {
            newBreaks.Add(new LineBreakResult(
                lineStart,
                words.Count - 1,
                lineWidth,
                CalculateLineBadness(lineWidth, maxWidth)));
        }

        return newBreaks;
    }

    private List<LineBreakResult> PreventWidowLine(
        List<WordToken> words,
        float[] wordWidths,
        List<LineBreakResult> breaks,
        float maxWidth,
        TypographySettings settings)
    {
        if (breaks.Count < 2)
            return breaks;

        var lastLine = breaks[^1];
        var lastLineWords = lastLine.EndWordIndex - lastLine.StartWordIndex + 1;

        if (lastLineWords >= settings.MinLastLineWords)
            return breaks; // No widow

        var secondLastLine = breaks[^2];
        if (secondLastLine.EndWordIndex == secondLastLine.StartWordIndex)
            return breaks; // Previous line only has one word, can't help

        var spaceWidth = MeasureSpace();
        var prevLastWord = words[secondLastLine.EndWordIndex];
        var prevLastWordWidth = wordWidths[secondLastLine.EndWordIndex];

        var newLastLineWidth = lastLine.Width + spaceWidth + prevLastWordWidth;
        var newSecondLastWidth = secondLastLine.Width - spaceWidth - prevLastWordWidth;

        if (newSecondLastWidth < maxWidth * 0.5f)
            return breaks; // Would create bad rag

        if (newLastLineWidth > maxWidth)
            return breaks; // Would overflow

        var newBreaks = new List<LineBreakResult>(breaks);
        newBreaks[^2] = new LineBreakResult(
            secondLastLine.StartWordIndex,
            secondLastLine.EndWordIndex - 1,
            newSecondLastWidth,
            CalculateLineBadness(newSecondLastWidth, maxWidth));
        newBreaks[^1] = new LineBreakResult(
            secondLastLine.EndWordIndex,
            lastLine.EndWordIndex,
            newLastLineWidth,
            CalculateLineBadness(newLastLineWidth, maxWidth));

        return newBreaks;
    }
}

public readonly struct WordToken
{
    public WordToken(string text, int startIndex, bool hasLeadingSpace)
    {
        Text = text;
        StartIndex = startIndex;
        HasLeadingSpace = hasLeadingSpace;
    }

    public string Text { get; }
    public int StartIndex { get; }
    public bool HasLeadingSpace { get; }
}

public readonly struct LineBreakResult
{
    public LineBreakResult(int startWordIndex, int endWordIndex, float width, int badness)
    {
        StartWordIndex = startWordIndex;
        EndWordIndex = endWordIndex;
        Width = width;
        Badness = badness;
    }

    public int StartWordIndex { get; }
    public int EndWordIndex { get; }
    public float Width { get; }
    public int Badness { get; }
}
