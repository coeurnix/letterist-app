using Letterist.Commands;
using Letterist.Model;
using System;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Export;

public class ExportPlanningTests
{
    [Fact]
    public void ResolveLanguages_UsesActiveLanguageWhenNotExportingAll()
    {
        var document = Document.Create("Test");
        document.SetActiveLanguage("ja-JP");

        var languages = ExportPlanning.ResolveLanguages(document, exportAllLanguages: false, visibleOnly: true, subset: null);

        Assert.Single(languages);
        Assert.Equal("ja-JP", languages[0]);
    }

    [Fact]
    public void ResolveLanguages_AppliesVisibilityAndSubsetFilters()
    {
        var document = Document.Create("Test");
        var layerId = document.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 120), "Hello");
        create.Execute(document);

        new SetBalloonTranslationCommand(create.CreatedBalloonId, "ja-JP", "こんにちは").Execute(document);
        new SetBalloonTranslationCommand(create.CreatedBalloonId, "fr-FR", "Salut").Execute(document);
        document.SetTranslationLanguageExportVisibility("ja-JP", false);

        var visible = ExportPlanning.ResolveLanguages(document, exportAllLanguages: true, visibleOnly: true, subset: null);
        Assert.Contains("en", visible, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("fr-FR", visible, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ja-JP", visible, StringComparer.OrdinalIgnoreCase);

        var subset = ExportPlanning.ResolveLanguages(document, exportAllLanguages: true, visibleOnly: true, subset: "ja-JP, fr-FR");
        Assert.Single(subset);
        Assert.Equal("fr-FR", subset[0]);
    }

    [Fact]
    public void NormalizePattern_AddsExpectedTokens()
    {
        var pattern = ExportPlanning.NormalizePattern("{document}", includePage: true, includeLayer: true, includeLanguage: true);

        Assert.Equal("{document}-page-{page}-{layer}-{lang}", pattern);
    }

    [Fact]
    public void ExpandPattern_FormatsPageNumberAndSanitizesTokens()
    {
        var expanded = ExportPlanning.ExpandPattern(
            "{document}-{page}-{layer}-{lang}",
            "Issue:1",
            pageNumber: 7,
            pagePadding: 4,
            layerName: "FX/Layer",
            languageTag: "ja-JP");

        Assert.Equal("Issue_1-0007-FX_Layer-ja-JP", expanded);
    }
}
