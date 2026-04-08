namespace Letterist.Model;

public sealed class BalloonStyle
{
    public Color FillColor { get; init; } = Color.White;

    public Color StrokeColor { get; init; } = Color.Black;

    public float StrokeWidth { get; init; } = 2f;

    public float Opacity { get; init; } = 1f;

    public bool GradientEnabled { get; init; } = false;

    public Color GradientStartColor { get; init; } = Color.White;

    public Color GradientEndColor { get; init; } = new Color(221, 221, 221);

    public BalloonGradientType GradientType { get; init; } = BalloonGradientType.Linear;

    public float GradientAngle { get; init; } = 90f;

    public bool PatternEnabled { get; init; } = false;

    public TextFillPattern PatternType { get; init; } = TextFillPattern.DiagonalStripes;

    public Color PatternSecondaryColor { get; init; } = new Color(221, 221, 221);

    public float PatternScale { get; init; } = 1f;

    public float PatternAngle { get; init; } = 45f;

    public string PatternImagePath { get; init; } = string.Empty;

    public bool ShadowEnabled { get; init; } = false;

    public Color ShadowColor { get; init; } = Color.Black;

    public float ShadowOpacity { get; init; } = 0.35f;

    public float ShadowOffsetX { get; init; } = 4f;

    public float ShadowOffsetY { get; init; } = 4f;

    public float ShadowFalloff { get; init; } = 8f;

    public bool GlowEnabled { get; init; } = false;

    public Color GlowColor { get; init; } = Color.Yellow;

    public float GlowOpacity { get; init; } = 0.5f;

    public float GlowSize { get; init; } = 6f;

    public float CornerRadius { get; init; } = 12f;

    public float ThoughtSmoothness { get; init; } = 0.55f;

    public float PaddingLeft { get; init; } = 12f;
    public float PaddingTop { get; init; } = 8f;
    public float PaddingRight { get; init; } = 12f;
    public float PaddingBottom { get; init; } = 8f;

    public float MinWidth { get; init; } = 80f;

    public float MinHeight { get; init; } = 50f;

    public float MaxWidth { get; init; } = 0f;

    public float MaxHeight { get; init; } = 0f;

    public BalloonStyle With(
        Color? fillColor = null,
        Color? strokeColor = null,
        float? strokeWidth = null,
        float? opacity = null,
        bool? gradientEnabled = null,
        Color? gradientStartColor = null,
        Color? gradientEndColor = null,
        BalloonGradientType? gradientType = null,
        float? gradientAngle = null,
        bool? patternEnabled = null,
        TextFillPattern? patternType = null,
        Color? patternSecondaryColor = null,
        float? patternScale = null,
        float? patternAngle = null,
        string? patternImagePath = null,
        bool? shadowEnabled = null,
        Color? shadowColor = null,
        float? shadowOpacity = null,
        float? shadowOffsetX = null,
        float? shadowOffsetY = null,
        float? shadowFalloff = null,
        bool? glowEnabled = null,
        Color? glowColor = null,
        float? glowOpacity = null,
        float? glowSize = null,
        float? cornerRadius = null,
        float? thoughtSmoothness = null,
        float? paddingLeft = null,
        float? paddingTop = null,
        float? paddingRight = null,
        float? paddingBottom = null,
        float? minWidth = null,
        float? minHeight = null,
        float? maxWidth = null,
        float? maxHeight = null)
    {
        return new BalloonStyle
        {
            FillColor = fillColor ?? FillColor,
            StrokeColor = strokeColor ?? StrokeColor,
            StrokeWidth = strokeWidth ?? StrokeWidth,
            Opacity = ClampOpacity(opacity ?? Opacity),
            GradientEnabled = gradientEnabled ?? GradientEnabled,
            GradientStartColor = gradientStartColor ?? GradientStartColor,
            GradientEndColor = gradientEndColor ?? GradientEndColor,
            GradientType = gradientType ?? GradientType,
            GradientAngle = gradientAngle ?? GradientAngle,
            PatternEnabled = patternEnabled ?? PatternEnabled,
            PatternType = patternType ?? PatternType,
            PatternSecondaryColor = patternSecondaryColor ?? PatternSecondaryColor,
            PatternScale = Math.Clamp(patternScale ?? PatternScale, 0.25f, 8f),
            PatternAngle = patternAngle ?? PatternAngle,
            PatternImagePath = patternImagePath ?? PatternImagePath,
            ShadowEnabled = shadowEnabled ?? ShadowEnabled,
            ShadowColor = shadowColor ?? ShadowColor,
            ShadowOpacity = ClampOpacity(shadowOpacity ?? ShadowOpacity),
            ShadowOffsetX = shadowOffsetX ?? ShadowOffsetX,
            ShadowOffsetY = shadowOffsetY ?? ShadowOffsetY,
            ShadowFalloff = Math.Max(0f, shadowFalloff ?? ShadowFalloff),
            GlowEnabled = glowEnabled ?? GlowEnabled,
            GlowColor = glowColor ?? GlowColor,
            GlowOpacity = ClampOpacity(glowOpacity ?? GlowOpacity),
            GlowSize = Math.Max(0f, glowSize ?? GlowSize),
            CornerRadius = cornerRadius ?? CornerRadius,
            ThoughtSmoothness = Math.Clamp(thoughtSmoothness ?? ThoughtSmoothness, 0f, 1f),
            PaddingLeft = paddingLeft ?? PaddingLeft,
            PaddingTop = paddingTop ?? PaddingTop,
            PaddingRight = paddingRight ?? PaddingRight,
            PaddingBottom = paddingBottom ?? PaddingBottom,
            MinWidth = minWidth ?? MinWidth,
            MinHeight = minHeight ?? MinHeight,
            MaxWidth = maxWidth ?? MaxWidth,
            MaxHeight = maxHeight ?? MaxHeight
        };
    }

    public BalloonStyle WithPadding(float horizontal, float vertical)
    {
        return With(paddingLeft: horizontal, paddingRight: horizontal, paddingTop: vertical, paddingBottom: vertical);
    }

    public static BalloonStyle Default => new();

    public static BalloonStyle Caption => new()
    {
        FillColor = new Color(255, 255, 200), // Light yellow
        CornerRadius = 0f,
        PaddingLeft = 8f,
        PaddingRight = 8f,
        PaddingTop = 6f,
        PaddingBottom = 6f
    };

    private static float ClampOpacity(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}

public enum BalloonGradientType
{
    Linear,
    Radial
}
