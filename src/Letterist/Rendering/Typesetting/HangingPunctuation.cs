using System;
using System.Collections.Generic;

namespace Letterist.Rendering.Typesetting;

public static class HangingPunctuation
{
    public readonly struct ProtrusionInfo
    {
        public ProtrusionInfo(float leftProtrusion, float rightProtrusion)
        {
            LeftProtrusion = leftProtrusion;
            RightProtrusion = rightProtrusion;
        }

        public float LeftProtrusion { get; }

        public float RightProtrusion { get; }

        public static ProtrusionInfo None => new(0f, 0f);
        public static ProtrusionInfo Left(float amount) => new(amount, 0f);
        public static ProtrusionInfo Right(float amount) => new(0f, amount);
        public static ProtrusionInfo Both(float amount) => new(amount, amount);
    }

    private static readonly Dictionary<char, ProtrusionInfo> DefaultProtrusionTable = new()
    {
        { '"', ProtrusionInfo.Both(1.0f) },
        { '\'', ProtrusionInfo.Both(1.0f) },
        { '\u201C', ProtrusionInfo.Left(1.0f) },  // Left double quote "
        { '\u201D', ProtrusionInfo.Right(1.0f) }, // Right double quote "
        { '\u2018', ProtrusionInfo.Left(1.0f) },  // Left single quote '
        { '\u2019', ProtrusionInfo.Right(1.0f) }, // Right single quote '
        { '\u00AB', ProtrusionInfo.Left(0.5f) },  // Left guillemet «
        { '\u00BB', ProtrusionInfo.Right(0.5f) }, // Right guillemet »
        { '\u2039', ProtrusionInfo.Left(0.5f) },  // Left single guillemet ‹
        { '\u203A', ProtrusionInfo.Right(0.5f) }, // Right single guillemet ›

        { '.', ProtrusionInfo.Right(0.7f) },
        { ',', ProtrusionInfo.Right(0.7f) },
        { ';', ProtrusionInfo.Right(0.5f) },
        { ':', ProtrusionInfo.Right(0.5f) },

        { '-', ProtrusionInfo.Right(0.7f) },
        { '\u2013', ProtrusionInfo.Right(0.5f) }, // En dash –
        { '\u2014', ProtrusionInfo.Right(0.3f) }, // Em dash —

        { '!', ProtrusionInfo.Right(0.3f) },
        { '?', ProtrusionInfo.Right(0.3f) },

        { '(', ProtrusionInfo.Left(0.2f) },
        { ')', ProtrusionInfo.Right(0.2f) },
        { '[', ProtrusionInfo.Left(0.1f) },
        { ']', ProtrusionInfo.Right(0.1f) },
    };

    public static ProtrusionInfo GetProtrusion(char c)
    {
        return DefaultProtrusionTable.TryGetValue(c, out var info) ? info : ProtrusionInfo.None;
    }

    public static bool ShouldHangLeft(char c, TypographySettings settings)
    {
        if (!settings.EnableHangingPunctuation) return false;
        return settings.LeftHangingChars.Contains(c);
    }

    public static bool ShouldHangRight(char c, TypographySettings settings)
    {
        if (!settings.EnableHangingPunctuation) return false;
        return settings.RightHangingChars.Contains(c);
    }

    public static float CalculateLeftHangingOffset(char firstChar, float charWidth, TypographySettings settings)
    {
        if (!settings.EnableHangingPunctuation) return 0f;
        if (!ShouldHangLeft(firstChar, settings)) return 0f;

        var protrusion = GetProtrusion(firstChar);
        var hangAmount = protrusion.LeftProtrusion * (settings.HangingPercentage / 100f);
        return -charWidth * hangAmount;
    }

    public static float CalculateRightHangingAllowance(char lastChar, float charWidth, TypographySettings settings)
    {
        if (!settings.EnableHangingPunctuation) return 0f;
        if (!ShouldHangRight(lastChar, settings)) return 0f;

        var protrusion = GetProtrusion(lastChar);
        var hangAmount = protrusion.RightProtrusion * (settings.HangingPercentage / 100f);
        return charWidth * hangAmount;
    }

    public readonly struct LineAdjustment
    {
        public LineAdjustment(float leftOffset, float widthAllowance)
        {
            LeftOffset = leftOffset;
            WidthAllowance = widthAllowance;
        }

        public float LeftOffset { get; }

        public float WidthAllowance { get; }

        public static LineAdjustment None => new(0f, 0f);
    }

    public static LineAdjustment CalculateLineAdjustment(
        string lineText,
        float firstCharWidth,
        float lastCharWidth,
        TypographySettings settings)
    {
        if (string.IsNullOrEmpty(lineText)) return LineAdjustment.None;
        if (!settings.EnableHangingPunctuation) return LineAdjustment.None;

        var firstChar = lineText[0];
        var lastChar = lineText[^1];

        var leftOffset = CalculateLeftHangingOffset(firstChar, firstCharWidth, settings);
        var widthAllowance = CalculateRightHangingAllowance(lastChar, lastCharWidth, settings);

        return new LineAdjustment(leftOffset, widthAllowance);
    }
}
