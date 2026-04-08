using System;

namespace Letterist.Model;

public static class BalloonStyleUtilities
{
    public static bool AreEquivalent(BalloonStyle a, BalloonStyle b)
    {
        return a.FillColor.Equals(b.FillColor)
               && a.StrokeColor.Equals(b.StrokeColor)
               && NearlyEqual(a.StrokeWidth, b.StrokeWidth)
               && NearlyEqual(a.Opacity, b.Opacity)
               && a.GradientEnabled == b.GradientEnabled
               && a.GradientStartColor.Equals(b.GradientStartColor)
               && a.GradientEndColor.Equals(b.GradientEndColor)
               && a.GradientType == b.GradientType
               && NearlyEqual(a.GradientAngle, b.GradientAngle)
               && a.PatternEnabled == b.PatternEnabled
               && a.PatternType == b.PatternType
               && a.PatternSecondaryColor.Equals(b.PatternSecondaryColor)
               && NearlyEqual(a.PatternScale, b.PatternScale)
               && NearlyEqual(a.PatternAngle, b.PatternAngle)
               && string.Equals(a.PatternImagePath, b.PatternImagePath, StringComparison.OrdinalIgnoreCase)
               && a.ShadowEnabled == b.ShadowEnabled
               && a.ShadowColor.Equals(b.ShadowColor)
               && NearlyEqual(a.ShadowOpacity, b.ShadowOpacity)
               && NearlyEqual(a.ShadowOffsetX, b.ShadowOffsetX)
               && NearlyEqual(a.ShadowOffsetY, b.ShadowOffsetY)
               && NearlyEqual(a.ShadowFalloff, b.ShadowFalloff)
               && a.GlowEnabled == b.GlowEnabled
               && a.GlowColor.Equals(b.GlowColor)
               && NearlyEqual(a.GlowOpacity, b.GlowOpacity)
               && NearlyEqual(a.GlowSize, b.GlowSize)
               && NearlyEqual(a.CornerRadius, b.CornerRadius)
               && NearlyEqual(a.ThoughtSmoothness, b.ThoughtSmoothness)
               && NearlyEqual(a.PaddingLeft, b.PaddingLeft)
               && NearlyEqual(a.PaddingTop, b.PaddingTop)
               && NearlyEqual(a.PaddingRight, b.PaddingRight)
               && NearlyEqual(a.PaddingBottom, b.PaddingBottom)
               && NearlyEqual(a.MinWidth, b.MinWidth)
               && NearlyEqual(a.MinHeight, b.MinHeight)
               && NearlyEqual(a.MaxWidth, b.MaxWidth)
               && NearlyEqual(a.MaxHeight, b.MaxHeight);
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Math.Abs(left - right) < 0.01f;
    }
}
