using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests;

public class ImageCommandsTests
{
    [Fact]
    public void LoadBackgroundImageCommand_SetsImagePath()
    {
        var doc = Document.Create("Test");
        var cmd = new LoadBackgroundImageCommand("/path/to/image.png");

        cmd.Execute(doc);

        Assert.Equal("/path/to/image.png", doc.BackgroundImagePath);
    }

    [Fact]
    public void LoadBackgroundImageCommand_Undo_RestoresPreviousPath()
    {
        var doc = Document.Create("Test");
        doc.SetBackgroundImagePath("/old/path.png");

        var cmd = new LoadBackgroundImageCommand("/new/path.png");
        cmd.Execute(doc);

        Assert.Equal("/new/path.png", doc.BackgroundImagePath);

        cmd.Undo(doc);

        Assert.Equal("/old/path.png", doc.BackgroundImagePath);
    }

    [Fact]
    public void LoadBackgroundImageCommand_Serialize_ContainsFilePath()
    {
        var cmd = new LoadBackgroundImageCommand("/path/to/image.png");
        var data = cmd.Serialize();

        Assert.Equal("LoadBackgroundImage", data.Type);
        Assert.Equal("/path/to/image.png", data.Get<string>("filePath"));
    }

    [Fact]
    public void ClearBackgroundImageCommand_ClearsPath()
    {
        var doc = Document.Create("Test");
        doc.SetBackgroundImagePath("/path/to/image.png");

        var cmd = new ClearBackgroundImageCommand();
        cmd.Execute(doc);

        Assert.Null(doc.BackgroundImagePath);
    }

    [Fact]
    public void ClearBackgroundImageCommand_Undo_RestoresPath()
    {
        var doc = Document.Create("Test");
        doc.SetBackgroundImagePath("/path/to/image.png");

        var cmd = new ClearBackgroundImageCommand();
        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal("/path/to/image.png", doc.BackgroundImagePath);
    }

    [Fact]
    public void ResizeDocumentCommand_ChangesSize()
    {
        var doc = Document.Create("Test", new Size2(100, 100));
        var cmd = new ResizeDocumentCommand(new Size2(800, 600));

        cmd.Execute(doc);

        Assert.Equal(800f, doc.Size.Width);
        Assert.Equal(600f, doc.Size.Height);
    }

    [Fact]
    public void ResizeDocumentCommand_Undo_RestoresPreviousSize()
    {
        var doc = Document.Create("Test", new Size2(100, 100));
        var cmd = new ResizeDocumentCommand(new Size2(800, 600));

        cmd.Execute(doc);
        cmd.Undo(doc);

        Assert.Equal(100f, doc.Size.Width);
        Assert.Equal(100f, doc.Size.Height);
    }

    [Fact]
    public void ResizeDocumentCommand_Serialize_ContainsDimensions()
    {
        var cmd = new ResizeDocumentCommand(new Size2(1920, 1080));
        var data = cmd.Serialize();

        Assert.Equal("ResizeDocument", data.Type);
        Assert.Equal(1920f, data.Get<float>("width"));
        Assert.Equal(1080f, data.Get<float>("height"));
    }
}
