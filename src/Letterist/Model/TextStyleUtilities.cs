using System;
using System.Collections.Generic;

namespace Letterist.Model;

internal static class TextStyleUtilities
{
    public static bool AreEquivalent(TextStyle a, TextStyle b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        return string.Equals(a.FontFamily, b.FontFamily, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(a.FontSize - b.FontSize) < 0.001f
            && a.TextColor.Equals(b.TextColor)
            && a.FillType == b.FillType
            && a.FillSecondaryColor.Equals(b.FillSecondaryColor)
            && Math.Abs(a.FillAngle - b.FillAngle) < 0.0001f
            && a.FillPattern == b.FillPattern
            && Math.Abs(a.FillPatternScale - b.FillPatternScale) < 0.0001f
            && string.Equals(a.FillImagePath, b.FillImagePath, StringComparison.OrdinalIgnoreCase)
            && a.OutlineColor.Equals(b.OutlineColor)
            && Math.Abs(a.OutlineWidth - b.OutlineWidth) < 0.0001f
            && AreStrokeListsEquivalent(a.AdditionalStrokes, b.AdditionalStrokes)
            && AreShadowListsEquivalent(a.Shadows, b.Shadows)
            && a.OuterGlowEnabled == b.OuterGlowEnabled
            && a.OuterGlowColor.Equals(b.OuterGlowColor)
            && Math.Abs(a.OuterGlowSize - b.OuterGlowSize) < 0.0001f
            && Math.Abs(a.OuterGlowOpacity - b.OuterGlowOpacity) < 0.0001f
            && a.InnerGlowEnabled == b.InnerGlowEnabled
            && a.InnerGlowColor.Equals(b.InnerGlowColor)
            && Math.Abs(a.InnerGlowSize - b.InnerGlowSize) < 0.0001f
            && Math.Abs(a.InnerGlowOpacity - b.InnerGlowOpacity) < 0.0001f
            && a.ExtrusionEnabled == b.ExtrusionEnabled
            && Math.Abs(a.ExtrusionDepth - b.ExtrusionDepth) < 0.0001f
            && Math.Abs(a.ExtrusionAngle - b.ExtrusionAngle) < 0.0001f
            && a.ExtrusionColor.Equals(b.ExtrusionColor)
            && Math.Abs(a.ExtrusionOpacity - b.ExtrusionOpacity) < 0.0001f
            && a.MotionBlurEnabled == b.MotionBlurEnabled
            && Math.Abs(a.MotionBlurDistance - b.MotionBlurDistance) < 0.0001f
            && Math.Abs(a.MotionBlurAngle - b.MotionBlurAngle) < 0.0001f
            && Math.Abs(a.MotionBlurOpacity - b.MotionBlurOpacity) < 0.0001f
            && a.AllCaps == b.AllCaps
            && a.Bold == b.Bold
            && a.Italic == b.Italic
            && a.Underline == b.Underline
            && a.Script == b.Script
            && Math.Abs(a.Tracking - b.Tracking) < 0.0001f
            && Math.Abs(a.LineHeight - b.LineHeight) < 0.0001f
            && a.Alignment == b.Alignment
            && a.FitMode == b.FitMode
            && a.OverflowMode == b.OverflowMode
            && Math.Abs(a.VerticalOffset - b.VerticalOffset) < 0.01f
            && a.RagMode == b.RagMode
            && string.Equals(a.HyphenationLocale ?? string.Empty, b.HyphenationLocale ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            && a.JustificationStrength == b.JustificationStrength
            && a.HyphenationLevel == b.HyphenationLevel
            && a.FillHeight == b.FillHeight
            && a.WarpPreset == b.WarpPreset
            && Math.Abs(a.WarpIntensity - b.WarpIntensity) < 0.0001f
            && Math.Abs(a.WarpHorizontalDistortion - b.WarpHorizontalDistortion) < 0.0001f
            && Math.Abs(a.WarpVerticalDistortion - b.WarpVerticalDistortion) < 0.0001f
            && (a.WarpMesh?.Equals(b.WarpMesh) ?? b.WarpMesh == null);
    }

    public static bool AreInlineEquivalent(TextStyle a, TextStyle b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        return string.Equals(a.FontFamily, b.FontFamily, StringComparison.OrdinalIgnoreCase)
            && Math.Abs(a.FontSize - b.FontSize) < 0.001f
            && a.TextColor.Equals(b.TextColor)
            && a.FillType == b.FillType
            && a.FillSecondaryColor.Equals(b.FillSecondaryColor)
            && Math.Abs(a.FillAngle - b.FillAngle) < 0.0001f
            && a.FillPattern == b.FillPattern
            && Math.Abs(a.FillPatternScale - b.FillPatternScale) < 0.0001f
            && string.Equals(a.FillImagePath, b.FillImagePath, StringComparison.OrdinalIgnoreCase)
            && a.OutlineColor.Equals(b.OutlineColor)
            && Math.Abs(a.OutlineWidth - b.OutlineWidth) < 0.0001f
            && AreStrokeListsEquivalent(a.AdditionalStrokes, b.AdditionalStrokes)
            && AreShadowListsEquivalent(a.Shadows, b.Shadows)
            && a.OuterGlowEnabled == b.OuterGlowEnabled
            && a.OuterGlowColor.Equals(b.OuterGlowColor)
            && Math.Abs(a.OuterGlowSize - b.OuterGlowSize) < 0.0001f
            && Math.Abs(a.OuterGlowOpacity - b.OuterGlowOpacity) < 0.0001f
            && a.InnerGlowEnabled == b.InnerGlowEnabled
            && a.InnerGlowColor.Equals(b.InnerGlowColor)
            && Math.Abs(a.InnerGlowSize - b.InnerGlowSize) < 0.0001f
            && Math.Abs(a.InnerGlowOpacity - b.InnerGlowOpacity) < 0.0001f
            && a.ExtrusionEnabled == b.ExtrusionEnabled
            && Math.Abs(a.ExtrusionDepth - b.ExtrusionDepth) < 0.0001f
            && Math.Abs(a.ExtrusionAngle - b.ExtrusionAngle) < 0.0001f
            && a.ExtrusionColor.Equals(b.ExtrusionColor)
            && Math.Abs(a.ExtrusionOpacity - b.ExtrusionOpacity) < 0.0001f
            && a.MotionBlurEnabled == b.MotionBlurEnabled
            && Math.Abs(a.MotionBlurDistance - b.MotionBlurDistance) < 0.0001f
            && Math.Abs(a.MotionBlurAngle - b.MotionBlurAngle) < 0.0001f
            && Math.Abs(a.MotionBlurOpacity - b.MotionBlurOpacity) < 0.0001f
            && a.Bold == b.Bold
            && a.Italic == b.Italic
            && a.Underline == b.Underline
            && a.Script == b.Script
            && Math.Abs(a.Tracking - b.Tracking) < 0.0001f;
    }

    private static bool AreStrokeListsEquivalent(IReadOnlyList<TextStroke>? left, IReadOnlyList<TextStroke>? right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount) return false;

        for (int i = 0; i < leftCount; i++)
        {
            var leftStroke = left![i];
            var rightStroke = right![i];
            if (!leftStroke.Color.Equals(rightStroke.Color)) return false;
            if (Math.Abs(leftStroke.Width - rightStroke.Width) >= 0.0001f) return false;
        }

        return true;
    }

    private static bool AreShadowListsEquivalent(IReadOnlyList<TextShadow>? left, IReadOnlyList<TextShadow>? right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount) return false;

        for (int i = 0; i < leftCount; i++)
        {
            var leftShadow = left![i];
            var rightShadow = right![i];
            if (!leftShadow.Color.Equals(rightShadow.Color)) return false;
            if (Math.Abs(leftShadow.OffsetX - rightShadow.OffsetX) >= 0.0001f) return false;
            if (Math.Abs(leftShadow.OffsetY - rightShadow.OffsetY) >= 0.0001f) return false;
            if (Math.Abs(leftShadow.Blur - rightShadow.Blur) >= 0.0001f) return false;
            if (Math.Abs(leftShadow.Opacity - rightShadow.Opacity) >= 0.0001f) return false;
        }

        return true;
    }
}
