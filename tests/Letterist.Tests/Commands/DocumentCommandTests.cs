using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class DocumentCommandTests
{
    [Fact]
    public void NewDocument_DefaultPageSize_MatchesInitialSize()
    {
        var size = new Size2(900, 1300);
        var document = Document.Create("Test", size);

        Assert.Equal(size.Width, document.DefaultPageSize.Width);
        Assert.Equal(size.Height, document.DefaultPageSize.Height);
    }

    [Fact]
    public void SetDocumentDefaultPageSizeCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test", new Size2(800, 1200));
        var dispatcher = new CommandDispatcher(document);
        var newSize = new Size2(1000, 1500);

        dispatcher.Execute(new SetDocumentDefaultPageSizeCommand(newSize));
        Assert.Equal(newSize.Width, document.DefaultPageSize.Width);
        Assert.Equal(newSize.Height, document.DefaultPageSize.Height);

        Assert.True(dispatcher.Undo());
        Assert.Equal(800, document.DefaultPageSize.Width);
        Assert.Equal(1200, document.DefaultPageSize.Height);
    }

    [Fact]
    public void SetDocumentDefaultPageSizeCommand_Serialize_ContainsSize()
    {
        var cmd = new SetDocumentDefaultPageSizeCommand(new Size2(1234, 2345));

        var data = cmd.Serialize();

        Assert.Equal("SetDocumentDefaultPageSize", data.Type);
        Assert.Equal(1234f, data.Get<float>("width"));
        Assert.Equal(2345f, data.Get<float>("height"));
    }

    [Fact]
    public void SetDocumentDefaultBackgroundColorCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test", new Size2(800, 1200));
        var dispatcher = new CommandDispatcher(document);
        var newColor = new Color(240, 240, 230, 255);

        dispatcher.Execute(new SetDocumentDefaultBackgroundColorCommand(newColor));
        Assert.Equal(newColor, document.DefaultPageBackgroundColor);

        Assert.True(dispatcher.Undo());
        Assert.Equal(new Color(255, 255, 255, 255), document.DefaultPageBackgroundColor);
    }

    [Fact]
    public void SetDocumentDefaultBackgroundImageCommand_ExecuteAndUndo_Works()
    {
        var document = Document.Create("Test", new Size2(800, 1200));
        var dispatcher = new CommandDispatcher(document);

        dispatcher.Execute(new SetDocumentDefaultBackgroundImageCommand(@"C:\images\bg.png"));
        Assert.Equal(@"C:\images\bg.png", document.DefaultPageBackgroundImagePath);

        Assert.True(dispatcher.Undo());
        Assert.Null(document.DefaultPageBackgroundImagePath);
    }
}
