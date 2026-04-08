using System;
using System.Collections.Generic;
using System.Linq;

namespace Letterist.Model;

public sealed class TextStyle
{
    public string FontFamily { get; init; } = "Segoe UI";

    public float FontSize { get; init; } = 14f;

    public Color TextColor { get; init; } = Color.Black;

    public TextFillType FillType { get; init; } = TextFillType.Solid;

    public Color FillSecondaryColor { get; init; } = Color.White;

    public float FillAngle { get; init; } = 90f;

    public TextFillPattern FillPattern { get; init; } = TextFillPattern.DiagonalStripes;

    public float FillPatternScale { get; init; } = 1f;

    public string FillImagePath { get; init; } = string.Empty;

    public Color OutlineColor { get; init; } = Color.Black;

    public float OutlineWidth { get; init; } = -1f;

    public IReadOnlyList<TextStroke> AdditionalStrokes { get; init; } = Array.Empty<TextStroke>();

    public IReadOnlyList<TextShadow> Shadows { get; init; } = Array.Empty<TextShadow>();

    public bool OuterGlowEnabled { get; init; } = false;

    public Color OuterGlowColor { get; init; } = Color.Yellow;

    public float OuterGlowSize { get; init; } = 0f;

    public float OuterGlowOpacity { get; init; } = 0.7f;

    public bool InnerGlowEnabled { get; init; } = false;

    public Color InnerGlowColor { get; init; } = Color.Yellow;

    public float InnerGlowSize { get; init; } = 0f;

    public float InnerGlowOpacity { get; init; } = 0.45f;

    public bool ExtrusionEnabled { get; init; } = false;

    public float ExtrusionDepth { get; init; } = 0f;

    public float ExtrusionAngle { get; init; } = 135f;

    public Color ExtrusionColor { get; init; } = Color.Black;

    public float ExtrusionOpacity { get; init; } = 0.75f;

    public bool MotionBlurEnabled { get; init; } = false;

    public float MotionBlurDistance { get; init; } = 0f;

    public float MotionBlurAngle { get; init; } = 0f;

    public float MotionBlurOpacity { get; init; } = 0.4f;

    public bool AllCaps { get; init; } = false;

    public bool Bold { get; init; } = false;

    public bool Italic { get; init; } = false;

    public bool Underline { get; init; } = false;

    public TextScript Script { get; init; } = TextScript.Normal;

    public float Tracking { get; init; } = 0f;

    public float LineHeight { get; init; } = 1.2f;

    public TextAlignment Alignment { get; init; } = TextAlignment.Center;

    public float VerticalOffset { get; init; } = 0f;

    public TextFitMode FitMode { get; init; } = TextFitMode.None;

    public TextOverflowMode OverflowMode { get; init; } = TextOverflowMode.Warn;

    public RagMode RagMode { get; init; } = RagMode.Natural;

    public string HyphenationLocale { get; init; } = "";

    public int JustificationStrength { get; init; } = 50;

    public int HyphenationLevel { get; init; } = 0;

    public bool FillHeight { get; init; } = false;

    public TextWarpPreset WarpPreset { get; init; } = TextWarpPreset.None;

    public float WarpIntensity { get; init; } = 0f;

    public float WarpHorizontalDistortion { get; init; } = 0f;

    public float WarpVerticalDistortion { get; init; } = 0f;

    public TextWarpMesh WarpMesh { get; init; } = TextWarpMesh.Identity;

    public TextStyle With(
        string? fontFamily = null,
        float? fontSize = null,
        Color? textColor = null,
        TextFillType? fillType = null,
        Color? fillSecondaryColor = null,
        float? fillAngle = null,
        TextFillPattern? fillPattern = null,
        float? fillPatternScale = null,
        string? fillImagePath = null,
        Color? outlineColor = null,
        float? outlineWidth = null,
        IEnumerable<TextStroke>? additionalStrokes = null,
        IEnumerable<TextShadow>? shadows = null,
        bool? outerGlowEnabled = null,
        Color? outerGlowColor = null,
        float? outerGlowSize = null,
        float? outerGlowOpacity = null,
        bool? innerGlowEnabled = null,
        Color? innerGlowColor = null,
        float? innerGlowSize = null,
        float? innerGlowOpacity = null,
        bool? extrusionEnabled = null,
        float? extrusionDepth = null,
        float? extrusionAngle = null,
        Color? extrusionColor = null,
        float? extrusionOpacity = null,
        bool? motionBlurEnabled = null,
        float? motionBlurDistance = null,
        float? motionBlurAngle = null,
        float? motionBlurOpacity = null,
        bool? allCaps = null,
        bool? bold = null,
        bool? italic = null,
        bool? underline = null,
        TextScript? script = null,
        float? tracking = null,
        float? lineHeight = null,
        TextAlignment? alignment = null,
        TextFitMode? fitMode = null,
        TextOverflowMode? overflowMode = null,
        float? verticalOffset = null,
        RagMode? ragMode = null,
        string? hyphenationLocale = null,
        int? justificationStrength = null,
        int? hyphenationLevel = null,
        bool? fillHeight = null,
        TextWarpPreset? warpPreset = null,
        float? warpIntensity = null,
        float? warpHorizontalDistortion = null,
        float? warpVerticalDistortion = null,
        TextWarpMesh? warpMesh = null)
    {
        return new TextStyle
        {
            FontFamily = fontFamily ?? FontFamily,
            FontSize = fontSize ?? FontSize,
            TextColor = textColor ?? TextColor,
            FillType = fillType ?? FillType,
            FillSecondaryColor = fillSecondaryColor ?? FillSecondaryColor,
            FillAngle = fillAngle ?? FillAngle,
            FillPattern = fillPattern ?? FillPattern,
            FillPatternScale = fillPatternScale ?? FillPatternScale,
            FillImagePath = fillImagePath ?? FillImagePath,
            OutlineColor = outlineColor ?? OutlineColor,
            OutlineWidth = outlineWidth ?? OutlineWidth,
            AdditionalStrokes = CloneAdditionalStrokes(additionalStrokes ?? AdditionalStrokes),
            Shadows = CloneShadows(shadows ?? Shadows),
            OuterGlowEnabled = outerGlowEnabled ?? OuterGlowEnabled,
            OuterGlowColor = outerGlowColor ?? OuterGlowColor,
            OuterGlowSize = outerGlowSize ?? OuterGlowSize,
            OuterGlowOpacity = outerGlowOpacity ?? OuterGlowOpacity,
            InnerGlowEnabled = innerGlowEnabled ?? InnerGlowEnabled,
            InnerGlowColor = innerGlowColor ?? InnerGlowColor,
            InnerGlowSize = innerGlowSize ?? InnerGlowSize,
            InnerGlowOpacity = innerGlowOpacity ?? InnerGlowOpacity,
            ExtrusionEnabled = extrusionEnabled ?? ExtrusionEnabled,
            ExtrusionDepth = extrusionDepth ?? ExtrusionDepth,
            ExtrusionAngle = extrusionAngle ?? ExtrusionAngle,
            ExtrusionColor = extrusionColor ?? ExtrusionColor,
            ExtrusionOpacity = extrusionOpacity ?? ExtrusionOpacity,
            MotionBlurEnabled = motionBlurEnabled ?? MotionBlurEnabled,
            MotionBlurDistance = motionBlurDistance ?? MotionBlurDistance,
            MotionBlurAngle = motionBlurAngle ?? MotionBlurAngle,
            MotionBlurOpacity = motionBlurOpacity ?? MotionBlurOpacity,
            AllCaps = allCaps ?? AllCaps,
            Bold = bold ?? Bold,
            Italic = italic ?? Italic,
            Underline = underline ?? Underline,
            Script = script ?? Script,
            Tracking = tracking ?? Tracking,
            LineHeight = lineHeight ?? LineHeight,
            Alignment = alignment ?? Alignment,
            FitMode = fitMode ?? FitMode,
            OverflowMode = overflowMode ?? OverflowMode,
            VerticalOffset = verticalOffset ?? VerticalOffset,
            RagMode = ragMode ?? RagMode,
            HyphenationLocale = hyphenationLocale ?? HyphenationLocale,
            JustificationStrength = justificationStrength ?? JustificationStrength,
            HyphenationLevel = hyphenationLevel ?? HyphenationLevel,
            FillHeight = fillHeight ?? FillHeight,
            WarpPreset = warpPreset ?? WarpPreset,
            WarpIntensity = warpIntensity ?? WarpIntensity,
            WarpHorizontalDistortion = warpHorizontalDistortion ?? WarpHorizontalDistortion,
            WarpVerticalDistortion = warpVerticalDistortion ?? WarpVerticalDistortion,
            WarpMesh = (warpMesh ?? WarpMesh).Clone()
        };
    }

    private static List<TextStroke> CloneAdditionalStrokes(IEnumerable<TextStroke> strokes)
    {
        return strokes
            .Where(stroke => stroke != null)
            .Select(stroke => stroke.Clone())
            .ToList();
    }

    private static List<TextShadow> CloneShadows(IEnumerable<TextShadow> shadows)
    {
        return shadows
            .Where(shadow => shadow != null)
            .Select(shadow => shadow.Clone())
            .ToList();
    }

    public static TextStyle Default => new()
    {
        AllCaps = true
    };

    public static TextStyle ComicDefault => new()
    {
        FontFamily = "Comic Sans MS", // Placeholder - real comics use specialized fonts
        FontSize = 12f,
        AllCaps = true,
        Bold = true,
        Alignment = TextAlignment.Center
    };

    public static TextStyle TextOnlyDefault => new()
    {
        FontFamily = "Segoe UI Black",
        FontSize = 48f,
        TextColor = Color.White,
        OutlineColor = Color.Black,
        OutlineWidth = 6f,
        AllCaps = true,
        Bold = true,
        Tracking = 0.03f,
        LineHeight = 1f,
        Alignment = TextAlignment.Center
    };
}

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public enum TextScript
{
    Normal,
    Superscript,
    Subscript
}

public enum TextFillType
{
    Solid,
    Linear,
    Radial,
    Pattern,
    Image
}

public enum TextFillPattern
{
    DiagonalStripes,
    Dots,
    Checkerboard,
    Crosshatch
}

public enum TextFitMode
{
    GrowBalloon,
    ShrinkToFit,
    TrackToFit,
    None
}

public enum TextOverflowMode
{
    Warn,
    Clip,
    Allow
}

public enum RagMode
{
    Natural,

    Tight,

    Loose,

    Justified
}
