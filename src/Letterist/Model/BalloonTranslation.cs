namespace Letterist.Model;

public readonly record struct BalloonTranslation(
    string Text,
    string SourceTextSnapshot,
    DateTime UpdatedUtc,
    TranslationTextOrientation Orientation = TranslationTextOrientation.Auto);
