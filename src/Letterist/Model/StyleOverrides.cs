using System;
using System.Collections.Generic;
using System.Linq;

namespace Letterist.Model;

public sealed class BalloonStyleOverride
{
    public Color? FillColor { get; set; }
    public Color? StrokeColor { get; set; }
    public float? StrokeWidth { get; set; }
    public float? Opacity { get; set; }
    public bool? GradientEnabled { get; set; }
    public Color? GradientStartColor { get; set; }
    public Color? GradientEndColor { get; set; }
    public BalloonGradientType? GradientType { get; set; }
    public float? GradientAngle { get; set; }
    public bool? PatternEnabled { get; set; }
    public TextFillPattern? PatternType { get; set; }
    public Color? PatternSecondaryColor { get; set; }
    public float? PatternScale { get; set; }
    public float? PatternAngle { get; set; }
    public string? PatternImagePath { get; set; }
    public bool? ShadowEnabled { get; set; }
    public Color? ShadowColor { get; set; }
    public float? ShadowOpacity { get; set; }
    public float? ShadowOffsetX { get; set; }
    public float? ShadowOffsetY { get; set; }
    public float? ShadowFalloff { get; set; }
    public bool? GlowEnabled { get; set; }
    public Color? GlowColor { get; set; }
    public float? GlowOpacity { get; set; }
    public float? GlowSize { get; set; }
    public float? CornerRadius { get; set; }
    public float? ThoughtSmoothness { get; set; }
    public float? PaddingLeft { get; set; }
    public float? PaddingTop { get; set; }
    public float? PaddingRight { get; set; }
    public float? PaddingBottom { get; set; }
    public float? MinWidth { get; set; }
    public float? MinHeight { get; set; }
    public float? MaxWidth { get; set; }
    public float? MaxHeight { get; set; }

    public static BalloonStyleOverride Empty => new();

    public BalloonStyle ApplyTo(BalloonStyle baseStyle)
    {
        return baseStyle.With(
            fillColor: FillColor,
            strokeColor: StrokeColor,
            strokeWidth: StrokeWidth,
            opacity: Opacity,
            gradientEnabled: GradientEnabled,
            gradientStartColor: GradientStartColor,
            gradientEndColor: GradientEndColor,
            gradientType: GradientType,
            gradientAngle: GradientAngle,
            patternEnabled: PatternEnabled,
            patternType: PatternType,
            patternSecondaryColor: PatternSecondaryColor,
            patternScale: PatternScale,
            patternAngle: PatternAngle,
            patternImagePath: PatternImagePath,
            shadowEnabled: ShadowEnabled,
            shadowColor: ShadowColor,
            shadowOpacity: ShadowOpacity,
            shadowOffsetX: ShadowOffsetX,
            shadowOffsetY: ShadowOffsetY,
            shadowFalloff: ShadowFalloff,
            glowEnabled: GlowEnabled,
            glowColor: GlowColor,
            glowOpacity: GlowOpacity,
            glowSize: GlowSize,
            cornerRadius: CornerRadius,
            thoughtSmoothness: ThoughtSmoothness,
            paddingLeft: PaddingLeft,
            paddingTop: PaddingTop,
            paddingRight: PaddingRight,
            paddingBottom: PaddingBottom,
            minWidth: MinWidth,
            minHeight: MinHeight,
            maxWidth: MaxWidth,
            maxHeight: MaxHeight);
    }

    public BalloonStyleOverride Clone()
    {
        return new BalloonStyleOverride
        {
            FillColor = FillColor,
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            Opacity = Opacity,
            GradientEnabled = GradientEnabled,
            GradientStartColor = GradientStartColor,
            GradientEndColor = GradientEndColor,
            GradientType = GradientType,
            GradientAngle = GradientAngle,
            PatternEnabled = PatternEnabled,
            PatternType = PatternType,
            PatternSecondaryColor = PatternSecondaryColor,
            PatternScale = PatternScale,
            PatternAngle = PatternAngle,
            PatternImagePath = PatternImagePath,
            ShadowEnabled = ShadowEnabled,
            ShadowColor = ShadowColor,
            ShadowOpacity = ShadowOpacity,
            ShadowOffsetX = ShadowOffsetX,
            ShadowOffsetY = ShadowOffsetY,
            ShadowFalloff = ShadowFalloff,
            GlowEnabled = GlowEnabled,
            GlowColor = GlowColor,
            GlowOpacity = GlowOpacity,
            GlowSize = GlowSize,
            CornerRadius = CornerRadius,
            ThoughtSmoothness = ThoughtSmoothness,
            PaddingLeft = PaddingLeft,
            PaddingTop = PaddingTop,
            PaddingRight = PaddingRight,
            PaddingBottom = PaddingBottom,
            MinWidth = MinWidth,
            MinHeight = MinHeight,
            MaxWidth = MaxWidth,
            MaxHeight = MaxHeight
        };
    }

    public static BalloonStyleOverride FromStyle(BalloonStyle style)
    {
        return new BalloonStyleOverride
        {
            FillColor = style.FillColor,
            StrokeColor = style.StrokeColor,
            StrokeWidth = style.StrokeWidth,
            Opacity = style.Opacity,
            GradientEnabled = style.GradientEnabled,
            GradientStartColor = style.GradientStartColor,
            GradientEndColor = style.GradientEndColor,
            GradientType = style.GradientType,
            GradientAngle = style.GradientAngle,
            PatternEnabled = style.PatternEnabled,
            PatternType = style.PatternType,
            PatternSecondaryColor = style.PatternSecondaryColor,
            PatternScale = style.PatternScale,
            PatternAngle = style.PatternAngle,
            PatternImagePath = style.PatternImagePath,
            ShadowEnabled = style.ShadowEnabled,
            ShadowColor = style.ShadowColor,
            ShadowOpacity = style.ShadowOpacity,
            ShadowOffsetX = style.ShadowOffsetX,
            ShadowOffsetY = style.ShadowOffsetY,
            ShadowFalloff = style.ShadowFalloff,
            GlowEnabled = style.GlowEnabled,
            GlowColor = style.GlowColor,
            GlowOpacity = style.GlowOpacity,
            GlowSize = style.GlowSize,
            CornerRadius = style.CornerRadius,
            ThoughtSmoothness = style.ThoughtSmoothness,
            PaddingLeft = style.PaddingLeft,
            PaddingTop = style.PaddingTop,
            PaddingRight = style.PaddingRight,
            PaddingBottom = style.PaddingBottom,
            MinWidth = style.MinWidth,
            MinHeight = style.MinHeight,
            MaxWidth = style.MaxWidth,
            MaxHeight = style.MaxHeight
        };
    }

    public static BalloonStyleOverride FromDifference(BalloonStyle baseStyle, BalloonStyle targetStyle)
    {
        return new BalloonStyleOverride
        {
            FillColor = baseStyle.FillColor.Equals(targetStyle.FillColor) ? null : targetStyle.FillColor,
            StrokeColor = baseStyle.StrokeColor.Equals(targetStyle.StrokeColor) ? null : targetStyle.StrokeColor,
            StrokeWidth = NearlyEqual(baseStyle.StrokeWidth, targetStyle.StrokeWidth) ? null : targetStyle.StrokeWidth,
            Opacity = NearlyEqual(baseStyle.Opacity, targetStyle.Opacity) ? null : targetStyle.Opacity,
            GradientEnabled = baseStyle.GradientEnabled == targetStyle.GradientEnabled ? null : targetStyle.GradientEnabled,
            GradientStartColor = baseStyle.GradientStartColor.Equals(targetStyle.GradientStartColor) ? null : targetStyle.GradientStartColor,
            GradientEndColor = baseStyle.GradientEndColor.Equals(targetStyle.GradientEndColor) ? null : targetStyle.GradientEndColor,
            GradientType = baseStyle.GradientType == targetStyle.GradientType ? null : targetStyle.GradientType,
            GradientAngle = NearlyEqual(baseStyle.GradientAngle, targetStyle.GradientAngle) ? null : targetStyle.GradientAngle,
            PatternEnabled = baseStyle.PatternEnabled == targetStyle.PatternEnabled ? null : targetStyle.PatternEnabled,
            PatternType = baseStyle.PatternType == targetStyle.PatternType ? null : targetStyle.PatternType,
            PatternSecondaryColor = baseStyle.PatternSecondaryColor.Equals(targetStyle.PatternSecondaryColor) ? null : targetStyle.PatternSecondaryColor,
            PatternScale = NearlyEqual(baseStyle.PatternScale, targetStyle.PatternScale) ? null : targetStyle.PatternScale,
            PatternAngle = NearlyEqual(baseStyle.PatternAngle, targetStyle.PatternAngle) ? null : targetStyle.PatternAngle,
            PatternImagePath = string.Equals(baseStyle.PatternImagePath, targetStyle.PatternImagePath, StringComparison.OrdinalIgnoreCase)
                ? null
                : targetStyle.PatternImagePath,
            ShadowEnabled = baseStyle.ShadowEnabled == targetStyle.ShadowEnabled ? null : targetStyle.ShadowEnabled,
            ShadowColor = baseStyle.ShadowColor.Equals(targetStyle.ShadowColor) ? null : targetStyle.ShadowColor,
            ShadowOpacity = NearlyEqual(baseStyle.ShadowOpacity, targetStyle.ShadowOpacity) ? null : targetStyle.ShadowOpacity,
            ShadowOffsetX = NearlyEqual(baseStyle.ShadowOffsetX, targetStyle.ShadowOffsetX) ? null : targetStyle.ShadowOffsetX,
            ShadowOffsetY = NearlyEqual(baseStyle.ShadowOffsetY, targetStyle.ShadowOffsetY) ? null : targetStyle.ShadowOffsetY,
            ShadowFalloff = NearlyEqual(baseStyle.ShadowFalloff, targetStyle.ShadowFalloff) ? null : targetStyle.ShadowFalloff,
            GlowEnabled = baseStyle.GlowEnabled == targetStyle.GlowEnabled ? null : targetStyle.GlowEnabled,
            GlowColor = baseStyle.GlowColor.Equals(targetStyle.GlowColor) ? null : targetStyle.GlowColor,
            GlowOpacity = NearlyEqual(baseStyle.GlowOpacity, targetStyle.GlowOpacity) ? null : targetStyle.GlowOpacity,
            GlowSize = NearlyEqual(baseStyle.GlowSize, targetStyle.GlowSize) ? null : targetStyle.GlowSize,
            CornerRadius = NearlyEqual(baseStyle.CornerRadius, targetStyle.CornerRadius) ? null : targetStyle.CornerRadius,
            ThoughtSmoothness = NearlyEqual(baseStyle.ThoughtSmoothness, targetStyle.ThoughtSmoothness) ? null : targetStyle.ThoughtSmoothness,
            PaddingLeft = NearlyEqual(baseStyle.PaddingLeft, targetStyle.PaddingLeft) ? null : targetStyle.PaddingLeft,
            PaddingTop = NearlyEqual(baseStyle.PaddingTop, targetStyle.PaddingTop) ? null : targetStyle.PaddingTop,
            PaddingRight = NearlyEqual(baseStyle.PaddingRight, targetStyle.PaddingRight) ? null : targetStyle.PaddingRight,
            PaddingBottom = NearlyEqual(baseStyle.PaddingBottom, targetStyle.PaddingBottom) ? null : targetStyle.PaddingBottom,
            MinWidth = NearlyEqual(baseStyle.MinWidth, targetStyle.MinWidth) ? null : targetStyle.MinWidth,
            MinHeight = NearlyEqual(baseStyle.MinHeight, targetStyle.MinHeight) ? null : targetStyle.MinHeight,
            MaxWidth = NearlyEqual(baseStyle.MaxWidth, targetStyle.MaxWidth) ? null : targetStyle.MaxWidth,
            MaxHeight = NearlyEqual(baseStyle.MaxHeight, targetStyle.MaxHeight) ? null : targetStyle.MaxHeight
        };
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Math.Abs(left - right) < 0.01f;
    }
}

public sealed class TextStyleOverride
{
    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public Color? TextColor { get; set; }
    public TextFillType? FillType { get; set; }
    public Color? FillSecondaryColor { get; set; }
    public float? FillAngle { get; set; }
    public TextFillPattern? FillPattern { get; set; }
    public float? FillPatternScale { get; set; }
    public string? FillImagePath { get; set; }
    public Color? OutlineColor { get; set; }
    public float? OutlineWidth { get; set; }
    public List<TextStroke>? AdditionalStrokes { get; set; }
    public List<TextShadow>? Shadows { get; set; }
    public bool? OuterGlowEnabled { get; set; }
    public Color? OuterGlowColor { get; set; }
    public float? OuterGlowSize { get; set; }
    public float? OuterGlowOpacity { get; set; }
    public bool? InnerGlowEnabled { get; set; }
    public Color? InnerGlowColor { get; set; }
    public float? InnerGlowSize { get; set; }
    public float? InnerGlowOpacity { get; set; }
    public bool? ExtrusionEnabled { get; set; }
    public float? ExtrusionDepth { get; set; }
    public float? ExtrusionAngle { get; set; }
    public Color? ExtrusionColor { get; set; }
    public float? ExtrusionOpacity { get; set; }
    public bool? MotionBlurEnabled { get; set; }
    public float? MotionBlurDistance { get; set; }
    public float? MotionBlurAngle { get; set; }
    public float? MotionBlurOpacity { get; set; }
    public bool? AllCaps { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public bool? Underline { get; set; }
    public TextScript? Script { get; set; }
    public float? Tracking { get; set; }
    public float? LineHeight { get; set; }
    public TextAlignment? Alignment { get; set; }
    public float? VerticalOffset { get; set; }
    public TextFitMode? FitMode { get; set; }
    public TextOverflowMode? OverflowMode { get; set; }
    public RagMode? RagMode { get; set; }
    public string? HyphenationLocale { get; set; }
    public int? JustificationStrength { get; set; }
    public int? HyphenationLevel { get; set; }
    public bool? FillHeight { get; set; }
    public TextWarpPreset? WarpPreset { get; set; }
    public float? WarpIntensity { get; set; }
    public float? WarpHorizontalDistortion { get; set; }
    public float? WarpVerticalDistortion { get; set; }
    public TextWarpMesh? WarpMesh { get; set; }

    public static TextStyleOverride Empty => new();

    public TextStyle ApplyTo(TextStyle baseStyle)
    {
        return baseStyle.With(
            fontFamily: FontFamily,
            fontSize: FontSize,
            textColor: TextColor,
            fillType: FillType,
            fillSecondaryColor: FillSecondaryColor,
            fillAngle: FillAngle,
            fillPattern: FillPattern,
            fillPatternScale: FillPatternScale,
            fillImagePath: FillImagePath,
            outlineColor: OutlineColor,
            outlineWidth: OutlineWidth,
            additionalStrokes: AdditionalStrokes,
            shadows: Shadows,
            outerGlowEnabled: OuterGlowEnabled,
            outerGlowColor: OuterGlowColor,
            outerGlowSize: OuterGlowSize,
            outerGlowOpacity: OuterGlowOpacity,
            innerGlowEnabled: InnerGlowEnabled,
            innerGlowColor: InnerGlowColor,
            innerGlowSize: InnerGlowSize,
            innerGlowOpacity: InnerGlowOpacity,
            extrusionEnabled: ExtrusionEnabled,
            extrusionDepth: ExtrusionDepth,
            extrusionAngle: ExtrusionAngle,
            extrusionColor: ExtrusionColor,
            extrusionOpacity: ExtrusionOpacity,
            motionBlurEnabled: MotionBlurEnabled,
            motionBlurDistance: MotionBlurDistance,
            motionBlurAngle: MotionBlurAngle,
            motionBlurOpacity: MotionBlurOpacity,
            allCaps: AllCaps,
            bold: Bold,
            italic: Italic,
            underline: Underline,
            script: Script,
            tracking: Tracking,
            lineHeight: LineHeight,
            alignment: Alignment,
            fitMode: FitMode,
            overflowMode: OverflowMode,
            verticalOffset: VerticalOffset,
            ragMode: RagMode,
            hyphenationLocale: HyphenationLocale,
            justificationStrength: JustificationStrength,
            hyphenationLevel: HyphenationLevel,
            fillHeight: FillHeight,
            warpPreset: WarpPreset,
            warpIntensity: WarpIntensity,
            warpHorizontalDistortion: WarpHorizontalDistortion,
            warpVerticalDistortion: WarpVerticalDistortion,
            warpMesh: WarpMesh);
    }

    public TextStyleOverride Clone()
    {
        return new TextStyleOverride
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            TextColor = TextColor,
            FillType = FillType,
            FillSecondaryColor = FillSecondaryColor,
            FillAngle = FillAngle,
            FillPattern = FillPattern,
            FillPatternScale = FillPatternScale,
            FillImagePath = FillImagePath,
            OutlineColor = OutlineColor,
            OutlineWidth = OutlineWidth,
            AdditionalStrokes = CloneStrokes(AdditionalStrokes),
            Shadows = CloneShadows(Shadows),
            OuterGlowEnabled = OuterGlowEnabled,
            OuterGlowColor = OuterGlowColor,
            OuterGlowSize = OuterGlowSize,
            OuterGlowOpacity = OuterGlowOpacity,
            InnerGlowEnabled = InnerGlowEnabled,
            InnerGlowColor = InnerGlowColor,
            InnerGlowSize = InnerGlowSize,
            InnerGlowOpacity = InnerGlowOpacity,
            ExtrusionEnabled = ExtrusionEnabled,
            ExtrusionDepth = ExtrusionDepth,
            ExtrusionAngle = ExtrusionAngle,
            ExtrusionColor = ExtrusionColor,
            ExtrusionOpacity = ExtrusionOpacity,
            MotionBlurEnabled = MotionBlurEnabled,
            MotionBlurDistance = MotionBlurDistance,
            MotionBlurAngle = MotionBlurAngle,
            MotionBlurOpacity = MotionBlurOpacity,
            AllCaps = AllCaps,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            Script = Script,
            Tracking = Tracking,
            LineHeight = LineHeight,
            Alignment = Alignment,
            VerticalOffset = VerticalOffset,
            FitMode = FitMode,
            OverflowMode = OverflowMode,
            RagMode = RagMode,
            HyphenationLocale = HyphenationLocale,
            JustificationStrength = JustificationStrength,
            HyphenationLevel = HyphenationLevel,
            FillHeight = FillHeight,
            WarpPreset = WarpPreset,
            WarpIntensity = WarpIntensity,
            WarpHorizontalDistortion = WarpHorizontalDistortion,
            WarpVerticalDistortion = WarpVerticalDistortion,
            WarpMesh = WarpMesh?.Clone()
        };
    }

    public static TextStyleOverride FromStyle(TextStyle style)
    {
        return new TextStyleOverride
        {
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            TextColor = style.TextColor,
            FillType = style.FillType,
            FillSecondaryColor = style.FillSecondaryColor,
            FillAngle = style.FillAngle,
            FillPattern = style.FillPattern,
            FillPatternScale = style.FillPatternScale,
            FillImagePath = style.FillImagePath,
            OutlineColor = style.OutlineColor,
            OutlineWidth = style.OutlineWidth,
            AdditionalStrokes = CloneStrokes(style.AdditionalStrokes),
            Shadows = CloneShadows(style.Shadows),
            OuterGlowEnabled = style.OuterGlowEnabled,
            OuterGlowColor = style.OuterGlowColor,
            OuterGlowSize = style.OuterGlowSize,
            OuterGlowOpacity = style.OuterGlowOpacity,
            InnerGlowEnabled = style.InnerGlowEnabled,
            InnerGlowColor = style.InnerGlowColor,
            InnerGlowSize = style.InnerGlowSize,
            InnerGlowOpacity = style.InnerGlowOpacity,
            ExtrusionEnabled = style.ExtrusionEnabled,
            ExtrusionDepth = style.ExtrusionDepth,
            ExtrusionAngle = style.ExtrusionAngle,
            ExtrusionColor = style.ExtrusionColor,
            ExtrusionOpacity = style.ExtrusionOpacity,
            MotionBlurEnabled = style.MotionBlurEnabled,
            MotionBlurDistance = style.MotionBlurDistance,
            MotionBlurAngle = style.MotionBlurAngle,
            MotionBlurOpacity = style.MotionBlurOpacity,
            AllCaps = style.AllCaps,
            Bold = style.Bold,
            Italic = style.Italic,
            Underline = style.Underline,
            Script = style.Script,
            Tracking = style.Tracking,
            LineHeight = style.LineHeight,
            Alignment = style.Alignment,
            VerticalOffset = style.VerticalOffset,
            FitMode = style.FitMode,
            OverflowMode = style.OverflowMode,
            RagMode = style.RagMode,
            HyphenationLocale = style.HyphenationLocale,
            JustificationStrength = style.JustificationStrength,
            HyphenationLevel = style.HyphenationLevel,
            FillHeight = style.FillHeight,
            WarpPreset = style.WarpPreset,
            WarpIntensity = style.WarpIntensity,
            WarpHorizontalDistortion = style.WarpHorizontalDistortion,
            WarpVerticalDistortion = style.WarpVerticalDistortion,
            WarpMesh = style.WarpMesh?.Clone()
        };
    }

    public static TextStyleOverride FromDifference(TextStyle baseStyle, TextStyle targetStyle)
    {
        return new TextStyleOverride
        {
            FontFamily = string.Equals(baseStyle.FontFamily, targetStyle.FontFamily, StringComparison.OrdinalIgnoreCase) ? null : targetStyle.FontFamily,
            FontSize = NearlyEqual(baseStyle.FontSize, targetStyle.FontSize) ? null : targetStyle.FontSize,
            TextColor = baseStyle.TextColor.Equals(targetStyle.TextColor) ? null : targetStyle.TextColor,
            FillType = baseStyle.FillType == targetStyle.FillType ? null : targetStyle.FillType,
            FillSecondaryColor = baseStyle.FillSecondaryColor.Equals(targetStyle.FillSecondaryColor) ? null : targetStyle.FillSecondaryColor,
            FillAngle = NearlyEqual(baseStyle.FillAngle, targetStyle.FillAngle) ? null : targetStyle.FillAngle,
            FillPattern = baseStyle.FillPattern == targetStyle.FillPattern ? null : targetStyle.FillPattern,
            FillPatternScale = NearlyEqual(baseStyle.FillPatternScale, targetStyle.FillPatternScale) ? null : targetStyle.FillPatternScale,
            FillImagePath = string.Equals(baseStyle.FillImagePath, targetStyle.FillImagePath, StringComparison.OrdinalIgnoreCase)
                ? null
                : targetStyle.FillImagePath,
            OutlineColor = baseStyle.OutlineColor.Equals(targetStyle.OutlineColor) ? null : targetStyle.OutlineColor,
            OutlineWidth = NearlyEqual(baseStyle.OutlineWidth, targetStyle.OutlineWidth) ? null : targetStyle.OutlineWidth,
            AdditionalStrokes = StrokeListsEquivalent(baseStyle.AdditionalStrokes, targetStyle.AdditionalStrokes)
                ? null
                : CloneStrokes(targetStyle.AdditionalStrokes),
            Shadows = ShadowListsEquivalent(baseStyle.Shadows, targetStyle.Shadows)
                ? null
                : CloneShadows(targetStyle.Shadows),
            OuterGlowEnabled = baseStyle.OuterGlowEnabled == targetStyle.OuterGlowEnabled ? null : targetStyle.OuterGlowEnabled,
            OuterGlowColor = baseStyle.OuterGlowColor.Equals(targetStyle.OuterGlowColor) ? null : targetStyle.OuterGlowColor,
            OuterGlowSize = NearlyEqual(baseStyle.OuterGlowSize, targetStyle.OuterGlowSize) ? null : targetStyle.OuterGlowSize,
            OuterGlowOpacity = NearlyEqual(baseStyle.OuterGlowOpacity, targetStyle.OuterGlowOpacity) ? null : targetStyle.OuterGlowOpacity,
            InnerGlowEnabled = baseStyle.InnerGlowEnabled == targetStyle.InnerGlowEnabled ? null : targetStyle.InnerGlowEnabled,
            InnerGlowColor = baseStyle.InnerGlowColor.Equals(targetStyle.InnerGlowColor) ? null : targetStyle.InnerGlowColor,
            InnerGlowSize = NearlyEqual(baseStyle.InnerGlowSize, targetStyle.InnerGlowSize) ? null : targetStyle.InnerGlowSize,
            InnerGlowOpacity = NearlyEqual(baseStyle.InnerGlowOpacity, targetStyle.InnerGlowOpacity) ? null : targetStyle.InnerGlowOpacity,
            ExtrusionEnabled = baseStyle.ExtrusionEnabled == targetStyle.ExtrusionEnabled ? null : targetStyle.ExtrusionEnabled,
            ExtrusionDepth = NearlyEqual(baseStyle.ExtrusionDepth, targetStyle.ExtrusionDepth) ? null : targetStyle.ExtrusionDepth,
            ExtrusionAngle = NearlyEqual(baseStyle.ExtrusionAngle, targetStyle.ExtrusionAngle) ? null : targetStyle.ExtrusionAngle,
            ExtrusionColor = baseStyle.ExtrusionColor.Equals(targetStyle.ExtrusionColor) ? null : targetStyle.ExtrusionColor,
            ExtrusionOpacity = NearlyEqual(baseStyle.ExtrusionOpacity, targetStyle.ExtrusionOpacity) ? null : targetStyle.ExtrusionOpacity,
            MotionBlurEnabled = baseStyle.MotionBlurEnabled == targetStyle.MotionBlurEnabled ? null : targetStyle.MotionBlurEnabled,
            MotionBlurDistance = NearlyEqual(baseStyle.MotionBlurDistance, targetStyle.MotionBlurDistance) ? null : targetStyle.MotionBlurDistance,
            MotionBlurAngle = NearlyEqual(baseStyle.MotionBlurAngle, targetStyle.MotionBlurAngle) ? null : targetStyle.MotionBlurAngle,
            MotionBlurOpacity = NearlyEqual(baseStyle.MotionBlurOpacity, targetStyle.MotionBlurOpacity) ? null : targetStyle.MotionBlurOpacity,
            AllCaps = baseStyle.AllCaps == targetStyle.AllCaps ? null : targetStyle.AllCaps,
            Bold = baseStyle.Bold == targetStyle.Bold ? null : targetStyle.Bold,
            Italic = baseStyle.Italic == targetStyle.Italic ? null : targetStyle.Italic,
            Underline = baseStyle.Underline == targetStyle.Underline ? null : targetStyle.Underline,
            Script = baseStyle.Script == targetStyle.Script ? null : targetStyle.Script,
            Tracking = NearlyEqual(baseStyle.Tracking, targetStyle.Tracking) ? null : targetStyle.Tracking,
            LineHeight = NearlyEqual(baseStyle.LineHeight, targetStyle.LineHeight) ? null : targetStyle.LineHeight,
            Alignment = baseStyle.Alignment == targetStyle.Alignment ? null : targetStyle.Alignment,
            VerticalOffset = NearlyEqual(baseStyle.VerticalOffset, targetStyle.VerticalOffset) ? null : targetStyle.VerticalOffset,
            FitMode = baseStyle.FitMode == targetStyle.FitMode ? null : targetStyle.FitMode,
            OverflowMode = baseStyle.OverflowMode == targetStyle.OverflowMode ? null : targetStyle.OverflowMode,
            RagMode = baseStyle.RagMode == targetStyle.RagMode ? null : targetStyle.RagMode,
            HyphenationLocale = string.Equals(baseStyle.HyphenationLocale, targetStyle.HyphenationLocale, StringComparison.OrdinalIgnoreCase)
                ? null
                : targetStyle.HyphenationLocale,
            JustificationStrength = baseStyle.JustificationStrength == targetStyle.JustificationStrength ? null : targetStyle.JustificationStrength,
            HyphenationLevel = baseStyle.HyphenationLevel == targetStyle.HyphenationLevel ? null : targetStyle.HyphenationLevel,
            FillHeight = baseStyle.FillHeight == targetStyle.FillHeight ? null : targetStyle.FillHeight,
            WarpPreset = baseStyle.WarpPreset == targetStyle.WarpPreset ? null : targetStyle.WarpPreset,
            WarpIntensity = NearlyEqual(baseStyle.WarpIntensity, targetStyle.WarpIntensity) ? null : targetStyle.WarpIntensity,
            WarpHorizontalDistortion = NearlyEqual(baseStyle.WarpHorizontalDistortion, targetStyle.WarpHorizontalDistortion) ? null : targetStyle.WarpHorizontalDistortion,
            WarpVerticalDistortion = NearlyEqual(baseStyle.WarpVerticalDistortion, targetStyle.WarpVerticalDistortion) ? null : targetStyle.WarpVerticalDistortion,
            WarpMesh = (baseStyle.WarpMesh?.Equals(targetStyle.WarpMesh) ?? targetStyle.WarpMesh == null)
                ? null
                : targetStyle.WarpMesh?.Clone()
        };
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Math.Abs(left - right) < 0.0001f;
    }

    private static bool StrokeListsEquivalent(IReadOnlyList<TextStroke>? left, IReadOnlyList<TextStroke>? right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount) return false;
        for (int i = 0; i < leftCount; i++)
        {
            var leftStroke = left![i];
            var rightStroke = right![i];
            if (!leftStroke.Color.Equals(rightStroke.Color)) return false;
            if (!NearlyEqual(leftStroke.Width, rightStroke.Width)) return false;
        }

        return true;
    }

    private static bool ShadowListsEquivalent(IReadOnlyList<TextShadow>? left, IReadOnlyList<TextShadow>? right)
    {
        var leftCount = left?.Count ?? 0;
        var rightCount = right?.Count ?? 0;
        if (leftCount != rightCount) return false;

        for (int i = 0; i < leftCount; i++)
        {
            var leftShadow = left![i];
            var rightShadow = right![i];
            if (!leftShadow.Color.Equals(rightShadow.Color)) return false;
            if (!NearlyEqual(leftShadow.OffsetX, rightShadow.OffsetX)) return false;
            if (!NearlyEqual(leftShadow.OffsetY, rightShadow.OffsetY)) return false;
            if (!NearlyEqual(leftShadow.Blur, rightShadow.Blur)) return false;
            if (!NearlyEqual(leftShadow.Opacity, rightShadow.Opacity)) return false;
        }

        return true;
    }

    private static List<TextStroke>? CloneStrokes(IReadOnlyList<TextStroke>? strokes)
    {
        if (strokes == null) return null;
        return strokes.Select(stroke => stroke.Clone()).ToList();
    }

    private static List<TextShadow>? CloneShadows(IReadOnlyList<TextShadow>? shadows)
    {
        if (shadows == null) return null;
        return shadows.Select(shadow => shadow.Clone()).ToList();
    }
}
