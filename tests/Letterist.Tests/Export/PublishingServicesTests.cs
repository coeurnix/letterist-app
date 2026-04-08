using Letterist.Commands;
using Letterist.Model;
using Letterist.Publishing;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Export;

public class PublishingServicesTests
{
    [Fact]
    public void Preflight_ReportsMissingTranslation_AndBuildsFix()
    {
        var doc = Document.Create("Issue");
        var layerId = doc.BalloonLayers.First().Id;
        var create = new CreateBalloonCommand(layerId, new Point2(100, 120), "Hello world");
        create.Execute(doc);

        var report = PublishingPreflightService.Analyze(
            doc,
            new PublishingPreflightOptions { Languages = new[] { "ja-JP" }, IncludePrintPreparationChecks = false });

        Assert.Contains(report.Issues, issue => issue.Code == "missing_translation");

        var fix = PublishingPreflightService.BuildFixCommands(doc, "fill-missing-translations:ja-JP");
        Assert.Single(fix);

        foreach (var command in fix)
        {
            command.Execute(doc);
        }

        Assert.False(doc.IsBalloonUntranslated(doc.ActivePage!.AllBalloons.First(), "ja-JP"));
    }

    [Fact]
    public void Preflight_BleedFix_CreatesCommandsForEdgePanels()
    {
        var doc = Document.Create("Issue");
        var page = doc.ActivePage!;

        var createPanel = new CreatePanelZoneCommand(
            page.Id,
            "Panel 1",
            new Rect(0, 0, 500, 500));
        createPanel.Execute(doc);

        var fixCommands = PublishingPreflightService.BuildFixCommands(
            doc,
            "normalize-panel-bleed",
            new PublishingPreflightOptions { MinimumBleed = 16f });

        Assert.Single(fixCommands);
        Assert.IsType<SetPanelBleedCommand>(fixCommands[0]);
    }

    [Fact]
    public void PrintPreparation_BuildImposition_CreatesExpectedSheetCount()
    {
        var doc = Document.Create("Issue");
        new CreatePageCommand("P2", new Size2(1200, 1800)).Execute(doc);
        new CreatePageCommand("P3", new Size2(1200, 1800)).Execute(doc);

        var sheets = PrintPreparationService.BuildImposition(doc, pagesPerSheet: 2);

        Assert.Equal(2, sheets.Count);
        Assert.Equal(2, sheets[0].Placements.Count);
        Assert.Single(sheets[1].Placements);
    }

    [Fact]
    public void WebExport_BuildResponsiveTargets_SortsAndCapsToSource()
    {
        var preset = WebExportService.ResolvePreset("responsive");
        var targets = WebExportService.BuildResponsiveTargets(1400, 2100, preset);

        Assert.NotEmpty(targets);
        Assert.True(targets.First().Width <= 1400);
        Assert.True(targets.SequenceEqual(targets.OrderByDescending(item => item.Width)));
    }

    [Fact]
    public void DigitalDistribution_BuildWebtoonPlan_SegmentsTallOutput()
    {
        var doc = Document.Create("Issue");
        new CreatePageCommand("P2", new Size2(1200, 3200)).Execute(doc);
        new CreatePageCommand("P3", new Size2(1200, 3200)).Execute(doc);

        var preset = new WebtoonExportPreset
        {
            Name = "test",
            TargetWidth = 1000,
            MaxSegmentHeight = 3000,
            GapPixels = 20
        };

        var plan = DigitalDistributionService.BuildWebtoonStripPlan(doc.Pages, preset);

        Assert.True(plan.SegmentCount > 1);
        Assert.Equal(doc.Pages.Count, plan.Placements.Count);
    }

    [Fact]
    public void DigitalDistribution_ValidatePackage_FindsMissingRequiredEntries()
    {
        var issues = DigitalDistributionService.ValidatePackage(
            "webtoon",
            new[] { "manifest.json", "segments/part-01.jpg" });

        Assert.Contains(issues, issue => issue.Code == "missing_package_file");
    }
}
