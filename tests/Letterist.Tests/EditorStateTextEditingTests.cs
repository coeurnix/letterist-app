using Letterist.Commands;
using Letterist.Model;
using Letterist.View;
using Xunit;

namespace Letterist.Tests;

public class EditorStateTextEditingTests
{
    private static EditorState CreateStateWithBalloon(string text)
    {
        var state = new EditorState();
        state.NewDocument("Test", new Size2(800, 600));

        var doc = state.Document!;
        var cmd = new CreateBalloonCommand(doc.ActiveLayerId, new Point2(100, 100), text);
        state.Execute(cmd);
        state.EnterTextEditMode(cmd.CreatedBalloonId);

        return state;
    }

    [Fact]
    public void InsertText_ReplacesSelection()
    {
        var state = CreateStateWithBalloon("Hello world");

        state.SetCursorPosition(6);
        state.SetCursorPosition(11, extendSelection: true);
        state.InsertText("Letterist");

        Assert.Equal("Hello Letterist", state.EditingText);
        Assert.False(state.HasSelection);
        Assert.Equal("Hello Letterist".Length, state.EditingCursorPosition);
    }

    [Fact]
    public void DeleteCharacterBefore_DeletesSelection()
    {
        var state = CreateStateWithBalloon("Hello world");

        state.SetCursorPosition(0);
        state.SetCursorPosition(5, extendSelection: true);
        state.DeleteCharacterBefore();

        Assert.Equal(" world", state.EditingText);
        Assert.False(state.HasSelection);
        Assert.Equal(0, state.EditingCursorPosition);
    }

    [Fact]
    public void UndoRedoTextEdit_RestoresContent()
    {
        var state = CreateStateWithBalloon("Hello");

        state.SetCursorPosition(5);
        state.InsertText("!");
        Assert.Equal("Hello!", state.EditingText);

        Assert.True(state.UndoTextEdit());
        Assert.Equal("Hello", state.EditingText);

        Assert.True(state.RedoTextEdit());
        Assert.Equal("Hello!", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_ConvertsDoubleQuotes()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("\"Hello\" he said");

        Assert.Equal("\u201CHello\u201D he said", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_ConvertsSingleQuotes()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("'Tis the season");

        Assert.Equal("\u2018Tis the season", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_ConvertsApostrophe()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("don't");

        Assert.Equal("don\u2019t", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_ConvertsEmDash()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("wait--what?");

        Assert.Equal("wait\u2014what?", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_ConvertsEllipsis()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("hmm...");

        Assert.Equal("hmm\u2026", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_Disabled_KeepsStraightQuotes()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = false;

        state.InsertText("\"Hello\" he said");

        Assert.Equal("\"Hello\" he said", state.EditingText);
    }

    [Fact]
    public void SmartPunctuation_NestedQuotes()
    {
        var state = CreateStateWithBalloon("");
        state.EnableSmartPunctuation = true;

        state.InsertText("He said \"I'm fine\"");

        Assert.Equal("He said \u201CI\u2019m fine\u201D", state.EditingText);
    }

    [Fact]
    public void ApplyTextStyleToSelection_DoesNotShiftUnrelatedTrailingSpans()
    {
        var state = CreateStateWithBalloon("abcdefghij");

        state.SetTextSelection(8, 2);
        Assert.True(state.ApplyTextStyleToSelection(TextStyle.Default.With(italic: true)));

        state.SetTextSelection(0, 1);
        Assert.True(state.ApplyTextStyleToSelection(TextStyle.Default.With(bold: true)));

        Assert.Equal(2, state.EditingTextStyleSpans.Count);

        var leading = state.EditingTextStyleSpans[0];
        Assert.Equal(0, leading.Start);
        Assert.Equal(1, leading.Length);
        Assert.True(leading.Style.Bold);

        var trailing = state.EditingTextStyleSpans[1];
        Assert.Equal(8, trailing.Start);
        Assert.Equal(2, trailing.Length);
        Assert.True(trailing.Style.Italic);
    }

    [Fact]
    public void RepeatedStyleChanges_DoNotCorruptOtherStyledRanges()
    {
        var state = CreateStateWithBalloon("abcdefghij");

        state.SetTextSelection(7, 3);
        Assert.True(state.ApplyTextStyleToSelection(TextStyle.Default.With(italic: true)));

        for (int i = 0; i < 4; i++)
        {
            state.SetTextSelection(0, 2);
            Assert.True(state.ApplyTextStyleToSelection(TextStyle.Default.With(bold: true)));

            state.SetTextSelection(0, 2);
            Assert.True(state.ApplyTextStyleToSelection(TextStyle.Default));
        }

        Assert.Single(state.EditingTextStyleSpans);
        var trailing = state.EditingTextStyleSpans[0];
        Assert.Equal(7, trailing.Start);
        Assert.Equal(3, trailing.Length);
        Assert.True(trailing.Style.Italic);
        Assert.False(trailing.Style.Bold);
    }

    [Fact]
    public void SetInsertionTextStyle_WhenNoSelection_StylesOnlyNewlyTypedText()
    {
        var state = CreateStateWithBalloon("Hello");

        state.SetCursorPosition(state.EditingText.Length);
        Assert.True(state.SetInsertionTextStyle(TextStyle.Default.With(bold: true)));
        state.InsertText("!");

        Assert.Equal("Hello!", state.EditingText);
        Assert.Single(state.EditingTextStyleSpans);

        var span = state.EditingTextStyleSpans[0];
        Assert.Equal(5, span.Start);
        Assert.Equal(1, span.Length);
        Assert.True(span.Style.Bold);
    }

    [Fact]
    public void SetInsertionTextStyle_ToggleOff_AppliesOnlyWhileEnabled()
    {
        var state = CreateStateWithBalloon("X");

        state.SetCursorPosition(state.EditingText.Length);
        Assert.True(state.SetInsertionTextStyle(TextStyle.Default.With(italic: true)));
        state.InsertText("A");

        Assert.True(state.SetInsertionTextStyle(TextStyle.Default.With(italic: false)));
        state.InsertText("B");

        Assert.Equal("XAB", state.EditingText);
        Assert.Single(state.EditingTextStyleSpans);

        var span = state.EditingTextStyleSpans[0];
        Assert.Equal(1, span.Start);
        Assert.Equal(1, span.Length);
        Assert.True(span.Style.Italic);
    }
}
