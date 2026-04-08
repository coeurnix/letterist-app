namespace Letterist.Rendering.Typesetting;

public sealed class TypographySettings
{
    public bool EnableHyphenation { get; init; } = false;

    public int HyphenationMinPrefix { get; init; } = 2;

    public int HyphenationMinSuffix { get; init; } = 3;

    public int HyphenationMaxConsecutive { get; init; } = 2;

    public bool EnableHangingPunctuation { get; init; } = false;

    public string LeftHangingChars { get; init; } = "\"'\u201C\u201D\u2018\u2019\u00AB\u2039";

    public string RightHangingChars { get; init; } = "\"'\u201C\u201D\u2018\u2019\u00BB\u203A.,;:!?-\u2013\u2014";

    public float HangingPercentage { get; init; } = 50f;

    public bool EnableLineBalancing { get; init; } = true;

    public float LineBalanceTarget { get; init; } = 0.7f;

    public bool PreventWidows { get; init; } = true;

    public int MinLastLineWords { get; init; } = 2;

    public bool PreventOrphans { get; init; } = false;

    public bool EnableLigatures { get; init; } = true;

    public bool EnableContextualAlternates { get; init; } = true;

    public bool UseOldstyleFigures { get; init; } = false;

    public bool UseSmallCaps { get; init; } = false;

    public float JustificationWordSpaceStretch { get; init; } = 1.5f;

    public float JustificationWordSpaceShrink { get; init; } = 0.8f;

    public float JustificationLetterSpaceStretch { get; init; } = 1.02f;

    public float JustificationLetterSpaceShrink { get; init; } = 0.98f;

    public bool ShowBadnessWarnings { get; init; } = false;

    public int BadnessThreshold { get; init; } = 50;

    public string NoBreakBeforeChars { get; init; } = ")]}.,;:!?\u00BB\u203A\u201D\u2019";

    public string NoBreakAfterChars { get; init; } = "([{\u00AB\u2039\u201C\u2018";

    public static TypographySettings ComicDefault => new()
    {
        EnableHyphenation = false, // Comics typically avoid hyphenation
        EnableHangingPunctuation = true,
        HangingPercentage = 40f, // Subtle hanging
        EnableLineBalancing = true,
        LineBalanceTarget = 0.65f, // Comics prefer "diamond" shape
        PreventWidows = true,
        MinLastLineWords = 2,
        EnableLigatures = true,
        EnableContextualAlternates = true,
        ShowBadnessWarnings = false
    };

    public static TypographySettings Professional => new()
    {
        EnableHyphenation = true,
        HyphenationMinPrefix = 3,
        HyphenationMinSuffix = 3,
        HyphenationMaxConsecutive = 2,
        EnableHangingPunctuation = true,
        HangingPercentage = 60f,
        EnableLineBalancing = true,
        LineBalanceTarget = 0.75f,
        PreventWidows = true,
        MinLastLineWords = 2,
        EnableLigatures = true,
        EnableContextualAlternates = true,
        ShowBadnessWarnings = true,
        BadnessThreshold = 30
    };

    public static TypographySettings FromLevels(int justificationStrength, int hyphenationLevel, bool hasHyphenationLocale)
    {
        var strengthFactor = justificationStrength / 100f;
        var enableHyphenation = hyphenationLevel > 0 && hasHyphenationLocale;

        return new TypographySettings
        {
            EnableHyphenation = enableHyphenation,
            HyphenationMinPrefix = enableHyphenation ? (hyphenationLevel > 70 ? 2 : hyphenationLevel > 30 ? 2 : 3) : 2,
            HyphenationMinSuffix = enableHyphenation ? (hyphenationLevel > 70 ? 2 : hyphenationLevel > 30 ? 3 : 3) : 3,
            HyphenationMaxConsecutive = enableHyphenation ? (hyphenationLevel > 70 ? 4 : hyphenationLevel > 30 ? 3 : 2) : 2,

            EnableHangingPunctuation = justificationStrength > 20,
            HangingPercentage = 30f + strengthFactor * 50f, // 30-80%

            EnableLineBalancing = true,
            LineBalanceTarget = 0.5f + strengthFactor * 0.35f, // 0.5-0.85

            PreventWidows = justificationStrength > 30,
            MinLastLineWords = justificationStrength > 70 ? 3 : 2,
            PreventOrphans = false,

            EnableLigatures = true,
            EnableContextualAlternates = true,
            UseOldstyleFigures = false,
            UseSmallCaps = false,

            JustificationWordSpaceStretch = 1.2f + strengthFactor * 0.5f, // 1.2-1.7
            JustificationWordSpaceShrink = 0.9f - strengthFactor * 0.15f, // 0.9-0.75
            JustificationLetterSpaceStretch = 1.0f + strengthFactor * 0.04f, // 1.0-1.04
            JustificationLetterSpaceShrink = 1.0f - strengthFactor * 0.04f, // 1.0-0.96

            ShowBadnessWarnings = false,
            BadnessThreshold = 50
        };
    }
}
