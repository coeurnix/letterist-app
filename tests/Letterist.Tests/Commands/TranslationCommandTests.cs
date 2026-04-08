using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class TranslationCommandTests
{
    [Fact]
    public void SetDocumentLanguageCommands_UpdateAndUndo()
    {
        var document = Document.Create("Test");
        var dispatcher = new CommandDispatcher(document);

        dispatcher.Execute(new SetDocumentBaseLanguageCommand("en-US"));
        dispatcher.Execute(new SetDocumentActiveLanguageCommand("fr-FR"));

        Assert.Equal("en-US", document.BaseLanguage);
        Assert.Equal("fr-FR", document.ActiveLanguage);

        Assert.True(dispatcher.Undo());
        Assert.Equal("en-US", document.ActiveLanguage);

        Assert.True(dispatcher.Undo());
        Assert.Equal("en", document.BaseLanguage);
    }

    [Fact]
    public void SetBalloonTranslationCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Hello there");
        create.Execute(document);
        var balloonId = create.CreatedBalloonId;

        var command = new SetBalloonTranslationCommand(balloonId, "es-ES", "Hola");
        command.Execute(document);

        var balloon = document.FindBalloon(balloonId)!;
        Assert.Equal("Hola", balloon.Translations["es-ES"].Text);
        Assert.Equal("Hello there", balloon.Translations["es-ES"].SourceTextSnapshot);

        command.Undo(document);
        Assert.False(balloon.Translations.ContainsKey("es-ES"));
    }

    [Fact]
    public void DeleteBalloonTranslationCommand_RemovesAndRestores()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Source");
        create.Execute(document);
        var balloonId = create.CreatedBalloonId;

        new SetBalloonTranslationCommand(balloonId, "de-DE", "Quelle").Execute(document);
        var command = new DeleteBalloonTranslationCommand(balloonId, "de-DE");
        command.Execute(document);

        var balloon = document.FindBalloon(balloonId)!;
        Assert.False(balloon.Translations.ContainsKey("de-DE"));

        command.Undo(document);
        Assert.Equal("Quelle", balloon.Translations["de-DE"].Text);
    }

    [Fact]
    public void SetTranslationLanguageExportVisibilityCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test");
        var command = new SetTranslationLanguageExportVisibilityCommand("ja-JP", false);

        command.Execute(document);
        Assert.False(document.IsLanguageVisibleInExport("ja-JP"));

        command.Undo(document);
        Assert.True(document.IsLanguageVisibleInExport("ja-JP"));
    }

    [Fact]
    public void RemoveTranslationLanguageExportVisibilityCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test");
        var dispatcher = new CommandDispatcher(document);
        dispatcher.Execute(new SetTranslationLanguageExportVisibilityCommand("ja-JP", true));

        var remove = new RemoveTranslationLanguageExportVisibilityCommand("ja-JP");
        remove.Execute(document);
        Assert.DoesNotContain("ja-JP", document.TranslationLanguageExportVisibility.Keys, StringComparer.OrdinalIgnoreCase);

        remove.Undo(document);
        Assert.Contains("ja-JP", document.TranslationLanguageExportVisibility.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.True(document.IsLanguageVisibleInExport("ja-JP"));
    }

    [Fact]
    public void SetTranslationLanguageLayoutCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test");
        var command = new SetTranslationLanguageLayoutCommand(
            "ja-JP",
            TranslationTextDirection.Auto,
            TranslationTextOrientation.Vertical,
            mirrorTailsForRtl: true);

        command.Execute(document);
        var layout = document.GetTranslationLanguageLayout("ja-JP");
        Assert.Equal(TranslationTextDirection.Auto, layout.Direction);
        Assert.Equal(TranslationTextOrientation.Vertical, layout.Orientation);
        Assert.True(layout.MirrorTailsForRtl);

        command.Undo(document);
        var reset = document.GetTranslationLanguageLayout("ja-JP");
        Assert.Equal(TranslationLanguageLayout.Default, reset);
    }

    [Fact]
    public void SetBalloonTranslationOrientationCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 100), "Base");
        create.Execute(document);
        var balloonId = create.CreatedBalloonId;

        new SetBalloonTranslationCommand(balloonId, "ja-JP", "縦書き").Execute(document);

        var command = new SetBalloonTranslationOrientationCommand(
            balloonId,
            "ja-JP",
            TranslationTextOrientation.Vertical);
        command.Execute(document);

        var balloon = document.FindBalloon(balloonId)!;
        Assert.Equal(TranslationTextOrientation.Vertical, balloon.Translations["ja-JP"].Orientation);

        command.Undo(document);
        Assert.Equal(TranslationTextOrientation.Auto, balloon.Translations["ja-JP"].Orientation);
    }
}
