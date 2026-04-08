using Letterist.Commands;
using Letterist.Model;
using System.Linq;
using Xunit;

namespace Letterist.Tests.Commands;

public class PanelCommandTests
{
    [Fact]
    public void CreatePanelZoneCommand_AddsAndRemovesPanel()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var bounds = new Rect(12, 34, 200, 160);
        var cmd = new CreatePanelZoneCommand(page.Id, "Panel 1", bounds, order: 1);

        cmd.Execute(doc);

        var panel = page.Panels.Single(p => p.Id == cmd.CreatedPanelId);
        Assert.Equal(bounds, panel.Bounds);
        Assert.Equal("Panel 1", panel.Name);
        Assert.Equal(1, panel.Order);

        cmd.Undo(doc);
        Assert.Empty(page.Panels);
    }

    [Fact]
    public void SetPanelImageExportVisibilityCommand_UpdatesAndUndoRestores()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var panel = PanelZone.Create("Panel", new Rect(0, 0, 200, 150), 1);
        page.AddPanel(panel);

        var original = panel.IsImageVisibleInExport;
        var cmd = new SetPanelImageExportVisibilityCommand(page.Id, panel.Id, !original);

        cmd.Execute(doc);
        Assert.Equal(!original, panel.IsImageVisibleInExport);

        cmd.Undo(doc);
        Assert.Equal(original, panel.IsImageVisibleInExport);
    }

    [Fact]
    public void SetPanelSafeMarginCommand_UpdatesAndUndoRestores()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;

        var panel = PanelZone.Create("Panel", new Rect(0, 0, 200, 150), 1);
        page.AddPanel(panel);

        var cmd = new SetPanelSafeMarginCommand(page.Id, panel.Id, 24f);
        cmd.Execute(doc);
        Assert.Equal(24f, panel.SafeMargin);

        cmd.Undo(doc);
        Assert.Equal(0f, panel.SafeMargin);
    }
}
