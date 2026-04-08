using Letterist.Commands;
using Letterist.Model;
using Xunit;

namespace Letterist.Tests.Commands;

public class CommandDispatcherTests
{
    [Fact]
    public void Undo_WhenOnlyCommandFails_DiscardsBrokenEntry_WithoutCrash()
    {
        var document = Document.Create("Undo failure test");
        var dispatcher = new CommandDispatcher(document);
        var command = new ThrowOnUndoCommand();

        dispatcher.Execute(command);
        Assert.True(dispatcher.History.CanUndo);
        Assert.False(dispatcher.History.CanRedo);

        var firstUndoResult = dispatcher.Undo();
        Assert.True(firstUndoResult);
        Assert.False(dispatcher.History.CanUndo);
        Assert.False(dispatcher.History.CanRedo);
    }

    [Fact]
    public void Undo_WhenLatestCommandFails_SkipsIt_AndUndoesPrevious()
    {
        var document = Document.Create("Undo skip test");
        var dispatcher = new CommandDispatcher(document);
        var good = new CountingCommand("Good");
        var bad = new ThrowOnUndoCommand();

        dispatcher.Execute(good);
        dispatcher.Execute(bad);

        var undoResult = dispatcher.Undo();
        Assert.True(undoResult);
        Assert.Equal(1, good.UndoCount);
        Assert.False(dispatcher.History.CanUndo);
        Assert.True(dispatcher.History.CanRedo);
    }

    [Fact]
    public void Redo_WhenOnlyCommandFails_DiscardsBrokenEntry_WithoutCrash()
    {
        var document = Document.Create("Redo failure test");
        var dispatcher = new CommandDispatcher(document);
        var command = new ThrowOnExecuteCommand();

        dispatcher.Execute(command);
        Assert.True(dispatcher.Undo());
        Assert.False(dispatcher.History.CanUndo);
        Assert.True(dispatcher.History.CanRedo);

        command.ThrowOnExecute = true;
        var failedRedoResult = dispatcher.Redo();
        Assert.True(failedRedoResult);
        Assert.False(dispatcher.History.CanUndo);
        Assert.False(dispatcher.History.CanRedo);
    }

    private sealed class ThrowOnUndoCommand : ICommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string CommandType => "ThrowOnUndo";
        public string Description => "Throws during undo";
        public bool ThrowOnUndo { get; set; } = true;

        public void Execute(Document document)
        {
        }

        public void Undo(Document document)
        {
            if (ThrowOnUndo)
            {
                throw new InvalidOperationException("Expected undo failure");
            }
        }

        public CommandData Serialize()
        {
            return new CommandData
            {
                Id = Id,
                Type = CommandType,
                Parameters = new Dictionary<string, object?>()
            };
        }
    }

    private sealed class ThrowOnExecuteCommand : ICommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string CommandType => "ThrowOnExecute";
        public string Description => "Throws during execute";
        public bool ThrowOnExecute { get; set; }

        public void Execute(Document document)
        {
            if (ThrowOnExecute)
            {
                throw new InvalidOperationException("Expected redo failure");
            }
        }

        public void Undo(Document document)
        {
        }

        public CommandData Serialize()
        {
            return new CommandData
            {
                Id = Id,
                Type = CommandType,
                Parameters = new Dictionary<string, object?>()
            };
        }
    }

    private sealed class CountingCommand : ICommand
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string CommandType => "Counting";
        public string Description { get; }
        public int ExecuteCount { get; private set; }
        public int UndoCount { get; private set; }

        public CountingCommand(string description)
        {
            Description = description;
        }

        public void Execute(Document document)
        {
            ExecuteCount++;
        }

        public void Undo(Document document)
        {
            UndoCount++;
        }

        public CommandData Serialize()
        {
            return new CommandData
            {
                Id = Id,
                Type = CommandType,
                Parameters = new Dictionary<string, object?>()
            };
        }
    }
}
