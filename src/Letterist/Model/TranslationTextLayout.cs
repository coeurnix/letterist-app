namespace Letterist.Model;

public enum TranslationTextDirection
{
    Auto = 0,
    Ltr = 1,
    Rtl = 2
}

public enum TranslationTextOrientation
{
    Auto = 0,
    Horizontal = 1,
    Vertical = 2
}

public readonly record struct TranslationLanguageLayout(
    TranslationTextDirection Direction,
    TranslationTextOrientation Orientation,
    bool MirrorTailsForRtl)
{
    public static TranslationLanguageLayout Default => new(
        TranslationTextDirection.Auto,
        TranslationTextOrientation.Auto,
        MirrorTailsForRtl: true);
}
