using Letterist.Commands;
using Letterist.Model;
using Letterist.Persistence;
using Xunit;

namespace Letterist.Tests.Model;

public class TranslationWorkflowTests
{
    [Fact]
    public void GetBalloonDisplayText_UsesActiveLanguageWithFallback()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Base");
        create.Execute(document);
        var balloon = document.FindBalloon(create.CreatedBalloonId)!;

        balloon.SetTranslation("fr-FR", new BalloonTranslation("Traduction", "Base", DateTime.UtcNow));
        document.SetActiveLanguage("fr-FR");

        Assert.Equal("Traduction", document.GetBalloonDisplayText(balloon));

        document.SetActiveLanguage("es-ES");
        Assert.Equal("Base", document.GetBalloonDisplayText(balloon));
    }

    [Fact]
    public void IsBalloonTranslationStale_DetectsChangedSource()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Original");
        create.Execute(document);
        var balloonId = create.CreatedBalloonId;

        new SetBalloonTranslationCommand(balloonId, "it-IT", "Tradotto").Execute(document);
        Assert.False(document.IsBalloonTranslationStale(document.FindBalloon(balloonId)!, "it-IT"));

        new SetBalloonTextCommand(balloonId, "Updated Source").Execute(document);
        Assert.True(document.IsBalloonTranslationStale(document.FindBalloon(balloonId)!, "it-IT"));
    }

    [Fact]
    public void Persistence_RoundTrip_PreservesTranslationSettingsAndEntries()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Base text");
        create.Execute(document);
        var balloonId = create.CreatedBalloonId;

        var dispatcher = new CommandDispatcher(document);
        dispatcher.Execute(new SetDocumentBaseLanguageCommand("en-US"));
        dispatcher.Execute(new SetDocumentActiveLanguageCommand("ja-JP"));
        dispatcher.Execute(new SetDocumentTranslationCompareCommand(TranslationCompareMode.SideBySide, "en-US"));
        dispatcher.Execute(new SetDocumentHighlightUntranslatedCommand(false));
        dispatcher.Execute(new SetTranslationLanguageExportVisibilityCommand("ja-JP", false));
        dispatcher.Execute(new SetTranslationLanguageLayoutCommand(
            "ja-JP",
            TranslationTextDirection.Auto,
            TranslationTextOrientation.Vertical,
            mirrorTailsForRtl: true));
        dispatcher.Execute(new SetBalloonTranslationCommand(
            balloonId,
            "ja-JP",
            "翻訳テキスト",
            orientation: TranslationTextOrientation.Vertical));

        var file = DocumentFile.FromDocument(
            document,
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>(),
            new Dictionary<Guid, string?>());
        var restored = file.ToDocument();

        var restoredBalloon = restored.FindBalloon(balloonId);
        Assert.NotNull(restoredBalloon);
        Assert.Equal("en-US", restored.BaseLanguage);
        Assert.Equal("ja-JP", restored.ActiveLanguage);
        Assert.Equal(TranslationCompareMode.SideBySide, restored.TranslationCompareMode);
        Assert.Equal("en-US", restored.CompareLanguage);
        Assert.False(restored.HighlightUntranslated);
        Assert.False(restored.IsLanguageVisibleInExport("ja-JP"));
        var restoredLayout = restored.GetTranslationLanguageLayout("ja-JP");
        Assert.Equal(TranslationTextOrientation.Vertical, restoredLayout.Orientation);
        Assert.Equal("翻訳テキスト", restoredBalloon!.Translations["ja-JP"].Text);
        Assert.Equal(TranslationTextOrientation.Vertical, restoredBalloon.Translations["ja-JP"].Orientation);
    }

    [Fact]
    public void RegisterLanguageMetadata_PreservesKnownLanguage_WhenSwitchingBackToBase()
    {
        var document = Document.Create("Test");
        var dispatcher = new CommandDispatcher(document);

        dispatcher.Execute(new SetTranslationLanguageExportVisibilityCommand("de-DE", true));
        dispatcher.Execute(new SetDocumentActiveLanguageCommand("de-DE"));
        dispatcher.Execute(new SetDocumentActiveLanguageCommand(document.BaseLanguage));

        Assert.Contains("de-DE", document.GetKnownLanguages(), StringComparer.OrdinalIgnoreCase);
    }
}
