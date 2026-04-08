using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class CommandHistoryTests
{
    [Fact]
    public void New_HasEmptyStacks()
    {
        var history = new CommandHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
    }

    [Fact]
    public void Record_AddsToUndoStack()
    {
        var history = new CommandHistory();
        var command = new TestCommand("Test");

        history.Record(command);

        Assert.True(history.CanUndo);
        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
    public void Record_ClearsRedoStack()
    {
        var history = new CommandHistory();
        var command1 = new TestCommand("Test 1");
        var command2 = new TestCommand("Test 2");

        history.Record(command1);
        history.Undo();
        Assert.True(history.CanRedo);

        history.Record(command2);

        Assert.False(history.CanRedo);
        Assert.Equal(0, history.RedoCount);
    }

    [Fact]
    public void Undo_MovesCommandToRedoStack()
    {
        var history = new CommandHistory();
        var command = new TestCommand("Test");
        history.Record(command);

        var undone = history.Undo();

        Assert.Equal(command, undone);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(1, history.RedoCount);
    }

    [Fact]
    public void Undo_ReturnsNullWhenEmpty()
    {
        var history = new CommandHistory();

        var undone = history.Undo();

        Assert.Null(undone);
    }

    [Fact]
    public void Redo_MovesCommandToUndoStack()
    {
        var history = new CommandHistory();
        var command = new TestCommand("Test");
        history.Record(command);
        history.Undo();

        var redone = history.Redo();

        Assert.Equal(command, redone);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Redo_ReturnsNullWhenEmpty()
    {
        var history = new CommandHistory();

        var redone = history.Redo();

        Assert.Null(redone);
    }

    [Fact]
    public void PeekUndo_ReturnsWithoutRemoving()
    {
        var history = new CommandHistory();
        var command = new TestCommand("Test");
        history.Record(command);

        var peeked = history.PeekUndo();

        Assert.Equal(command, peeked);
        Assert.Equal(1, history.UndoCount);
    }

    [Fact]
    public void Transaction_GroupsCommands()
    {
        var history = new CommandHistory();

        using (history.BeginTransaction("Group"))
        {
            history.Record(new TestCommand("Test 1"));
            history.Record(new TestCommand("Test 2"));
            history.Record(new TestCommand("Test 3"));
        }

        Assert.Equal(1, history.UndoCount); // All grouped into one

        var undone = history.Undo();
        Assert.IsType<CompositeCommand>(undone);
        Assert.Equal("Group", undone!.Description);
    }

    [Fact]
    public void Transaction_EmptyTransactionNoCommand()
    {
        var history = new CommandHistory();

        using (history.BeginTransaction("Empty"))
        {
        }

        Assert.Equal(0, history.UndoCount);
    }

    [Fact]
    public void Transaction_ThrowsIfNested()
    {
        var history = new CommandHistory();

        using (history.BeginTransaction("First"))
        {
            Assert.Throws<InvalidOperationException>(() => history.BeginTransaction("Second"));
        }
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        var history = new CommandHistory();
        history.Record(new TestCommand("Test 1"));
        history.Record(new TestCommand("Test 2"));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void HistoryChanged_FiresOnRecord()
    {
        var history = new CommandHistory();
        var eventFired = false;
        history.HistoryChanged += (s, e) => eventFired = true;

        history.Record(new TestCommand("Test"));

        Assert.True(eventFired);
    }

    [Fact]
    public void HistoryChanged_FiresOnUndo()
    {
        var history = new CommandHistory();
        history.Record(new TestCommand("Test"));
        var eventFired = false;
        history.HistoryChanged += (s, e) => eventFired = true;

        history.Undo();

        Assert.True(eventFired);
    }

    [Fact]
    public void MaxHistorySize_TrimsOldCommands()
    {
        var history = new CommandHistory(maxHistorySize: 3);

        history.Record(new TestCommand("1"));
        history.Record(new TestCommand("2"));
        history.Record(new TestCommand("3"));
        history.Record(new TestCommand("4"));

        Assert.Equal(3, history.UndoCount);
    }

    private class TestCommand : ICommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string CommandType => "Test";
        public string Description { get; }

        private bool _executed;
        public bool WasExecuted => _executed;
        public int ExecuteCount { get; private set; }
        public int UndoCount { get; private set; }

        public TestCommand(string description)
        {
            Description = description;
        }

        public void Execute(Document document)
        {
            _executed = true;
            ExecuteCount++;
        }

        public void Undo(Document document)
        {
            _executed = false;
            UndoCount++;
        }

        public CommandData Serialize()
        {
            return new CommandData
            {
                Id = Id,
                Type = CommandType,
                Parameters = new Dictionary<string, object?> { ["description"] = Description }
            };
        }
    }
}
