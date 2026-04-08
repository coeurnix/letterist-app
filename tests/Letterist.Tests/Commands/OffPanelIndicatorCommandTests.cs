using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class OffPanelIndicatorCommandTests
{
    [Fact]
    public void SetOffPanelIndicatorStyleCommand_UpdatesAndUndoRestores()
    {
        var doc = Document.Create("Test");
        var page = doc.ActivePage!;
        var original = page.OffPanelIndicatorStyle;

        var updated = new OffPanelIndicatorStyle(new Color(20, 40, 60, 180), 24f);
        var cmd = new SetOffPanelIndicatorStyleCommand(page.Id, updated);
        cmd.Execute(doc);

        Assert.Equal(updated.Color, page.OffPanelIndicatorStyle.Color);
        Assert.Equal(updated.Size, page.OffPanelIndicatorStyle.Size);

        cmd.Undo(doc);

        Assert.Equal(original.Color, page.OffPanelIndicatorStyle.Color);
        Assert.Equal(original.Size, page.OffPanelIndicatorStyle.Size);
    }
}
