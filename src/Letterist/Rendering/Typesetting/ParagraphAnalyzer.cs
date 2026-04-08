using System;
using System.Collections.Generic;

namespace Letterist.Rendering.Typesetting;

public sealed class ParagraphAnalyzer
{
    public const int BadnessInfinite = 10000;
    public const int BadnessGood = 20;       // Barely noticeable
    public const int BadnessAcceptable = 50; // Acceptable for most uses
    public const int BadnessPoor = 100;      // Noticeable issues
    public const int BadnessBad = 200;       // Significant problems

    public enum Severity
    {
        None,
        Minor,
        Moderate,
        Significant,
        Severe
    }

    public sealed class Issue
    {
        public Issue(string code, string message, Severity severity, int lineIndex = -1)
        {
            Code = code;
            Message = message;
            Severity = severity;
            LineIndex = lineIndex;
        }

        public string Code { get; }

        public string Message { get; }

        public Severity Severity { get; }

        public int LineIndex { get; }
    }

    public sealed class LineAnalysis
    {
        public int LineIndex { get; init; }
        public float Width { get; init; }
        public float MaxWidth { get; init; }
        public int Badness { get; init; }
        public bool IsOverfull { get; init; }
        public bool IsUnderfull { get; init; }
        public float FillRatio { get; init; }
        public List<Issue> Issues { get; init; } = new();
    }

    public sealed class AnalysisResult
    {
        public int LineCount { get; init; }
        public int TotalBadness { get; init; }
        public int AverageBadness { get; init; }
        public int MaxBadness { get; init; }
        public float AverageLineWidth { get; init; }
        public float RagStandardDeviation { get; init; }
        public bool HasOverfullLines { get; init; }
        public bool HasUnderfullLines { get; init; }
        public bool HasWidow { get; init; }
        public bool HasOrphan { get; init; }
        public List<LineAnalysis> Lines { get; init; } = new();
        public List<Issue> Issues { get; init; } = new();

        public Severity OverallSeverity
        {
            get
            {
                if (Issues.Count == 0) return Severity.None;

                var maxSeverity = Severity.None;
                foreach (var issue in Issues)
                {
                    if (issue.Severity > maxSeverity) maxSeverity = issue.Severity;
                }
                return maxSeverity;
            }
        }

        public int QualityScore
        {
            get
            {
                if (LineCount == 0) return 100;

                var score = 100;

                if (AverageBadness > BadnessGood) score -= (AverageBadness - BadnessGood) / 5;

                if (MaxBadness > BadnessAcceptable) score -= (MaxBadness - BadnessAcceptable) / 10;

                if (HasOverfullLines) score -= 20;

                if (HasWidow) score -= 10;
                if (HasOrphan) score -= 10;

                if (RagStandardDeviation > AverageLineWidth * 0.2f) score -= 5;

                return Math.Clamp(score, 0, 100);
            }
        }
    }

    public AnalysisResult Analyze(
        IReadOnlyList<LineBreakResult> breaks,
        float maxWidth,
        TypographySettings settings)
    {
        if (breaks == null || breaks.Count == 0)
        {
            return new AnalysisResult { LineCount = 0 };
        }

        var lines = new List<LineAnalysis>();
        var issues = new List<Issue>();
        var totalBadness = 0;
        var maxBadness = 0;
        var totalWidth = 0f;
        var hasOverfull = false;
        var hasUnderfull = false;

        for (int i = 0; i < breaks.Count; i++)
        {
            var line = breaks[i];
            var isLastLine = (i == breaks.Count - 1);

            var lineAnalysis = AnalyzeLine(line, maxWidth, i, isLastLine, settings);
            lines.Add(lineAnalysis);

            totalBadness += lineAnalysis.Badness;
            if (lineAnalysis.Badness > maxBadness) maxBadness = lineAnalysis.Badness;
            totalWidth += lineAnalysis.Width;

            if (lineAnalysis.IsOverfull) hasOverfull = true;
            if (lineAnalysis.IsUnderfull) hasUnderfull = true;

            issues.AddRange(lineAnalysis.Issues);
        }

        var avgWidth = totalWidth / breaks.Count;
        var avgBadness = totalBadness / breaks.Count;

        var sumSquaredDiff = 0f;
        foreach (var line in lines)
        {
            var diff = line.Width - avgWidth;
            sumSquaredDiff += diff * diff;
        }
        var ragStdDev = MathF.Sqrt(sumSquaredDiff / breaks.Count);

        var hasWidow = false;
        if (breaks.Count > 1 && settings.PreventWidows)
        {
            var lastLine = breaks[^1];
            var wordCount = lastLine.EndWordIndex - lastLine.StartWordIndex + 1;
            if (wordCount < settings.MinLastLineWords)
            {
                hasWidow = true;
                issues.Add(new Issue(
                    "WIDOW",
                    $"Widow: only {wordCount} word(s) on last line (min: {settings.MinLastLineWords})",
                    Severity.Moderate,
                    breaks.Count - 1));
            }
        }

        var hasOrphan = false;

        if (avgBadness > BadnessPoor)
        {
            issues.Add(new Issue(
                "HIGH_AVG_BADNESS",
                $"High average badness: {avgBadness}",
                avgBadness > BadnessBad ? Severity.Significant : Severity.Moderate));
        }

        if (ragStdDev > avgWidth * 0.3f && breaks.Count > 2)
        {
            issues.Add(new Issue(
                "UNEVEN_RAG",
                $"Uneven rag: σ={ragStdDev:F1}px ({ragStdDev / avgWidth * 100:F0}% of avg width)",
                Severity.Minor));
        }

        return new AnalysisResult
        {
            LineCount = breaks.Count,
            TotalBadness = totalBadness,
            AverageBadness = avgBadness,
            MaxBadness = maxBadness,
            AverageLineWidth = avgWidth,
            RagStandardDeviation = ragStdDev,
            HasOverfullLines = hasOverfull,
            HasUnderfullLines = hasUnderfull,
            HasWidow = hasWidow,
            HasOrphan = hasOrphan,
            Lines = lines,
            Issues = issues
        };
    }

    private LineAnalysis AnalyzeLine(
        LineBreakResult line,
        float maxWidth,
        int lineIndex,
        bool isLastLine,
        TypographySettings settings)
    {
        var issues = new List<Issue>();
        var fillRatio = line.Width / maxWidth;
        var isOverfull = line.Width > maxWidth + 0.5f;
        var isUnderfull = !isLastLine && fillRatio < 0.6f;

        if (isOverfull)
        {
            var overage = line.Width - maxWidth;
            var severity = overage > 20f ? Severity.Severe :
                           overage > 10f ? Severity.Significant :
                           overage > 5f ? Severity.Moderate : Severity.Minor;

            issues.Add(new Issue(
                "OVERFULL",
                $"Overfull line: {overage:F1}px over max width",
                severity,
                lineIndex));
        }

        if (isUnderfull)
        {
            var severity = fillRatio < 0.4f ? Severity.Significant :
                           fillRatio < 0.5f ? Severity.Moderate : Severity.Minor;

            issues.Add(new Issue(
                "UNDERFULL",
                $"Underfull line: {fillRatio * 100:F0}% filled",
                severity,
                lineIndex));
        }

        if (settings.ShowBadnessWarnings && line.Badness > settings.BadnessThreshold)
        {
            var severity = line.Badness > 200 ? Severity.Significant :
                           line.Badness > 100 ? Severity.Moderate : Severity.Minor;

            issues.Add(new Issue(
                "HIGH_BADNESS",
                $"High badness: {line.Badness} (threshold: {settings.BadnessThreshold})",
                severity,
                lineIndex));
        }

        return new LineAnalysis
        {
            LineIndex = lineIndex,
            Width = line.Width,
            MaxWidth = maxWidth,
            Badness = line.Badness,
            IsOverfull = isOverfull,
            IsUnderfull = isUnderfull,
            FillRatio = fillRatio,
            Issues = issues
        };
    }

    public static int CalculateBadness(float lineWidth, float maxWidth)
    {
        if (lineWidth > maxWidth)
        {
            return BadnessInfinite;
        }

        var ratio = lineWidth / maxWidth;
        if (ratio >= 0.95f)
        {
            return 0;
        }

        var emptyRatio = 1f - ratio;
        var badness = (int)(emptyRatio * emptyRatio * emptyRatio * 10000);
        return Math.Min(badness, BadnessInfinite);
    }
}
