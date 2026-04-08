using Letterist.Model;
using Letterist.Diagnostics;

namespace Letterist.Commands;

public sealed class CommandDispatcher
{
    private readonly Document _document;
    private readonly CommandHistory _history;
    private readonly Dictionary<string, Func<CommandData, ICommand>> _commandFactories = new();

    public event EventHandler<CommandEventArgs>? CommandExecuting;

    public event EventHandler<CommandEventArgs>? CommandExecuted;

    public event EventHandler<CommandEventArgs>? CommandUndone;

    public event EventHandler<CommandEventArgs>? CommandRedone;

    public CommandDispatcher(Document document, CommandHistory? history = null)
    {
        _document = document;
        _history = history ?? new CommandHistory();
    }

    public Document Document => _document;

    public CommandHistory History => _history;

    public void Execute(ICommand command)
    {
        CommandExecuting?.Invoke(this, new CommandEventArgs(command));

        command.Execute(_document);
        _history.Record(command);
        _document.MarkDirty();

        CommandExecuted?.Invoke(this, new CommandEventArgs(command));
    }

    public void ExecuteTransaction(string description, params ICommand[] commands)
    {
        using var transaction = _history.BeginTransaction(description);
        foreach (var command in commands)
        {
            CommandExecuting?.Invoke(this, new CommandEventArgs(command));
            command.Execute(_document);
            _history.Record(command);
            CommandExecuted?.Invoke(this, new CommandEventArgs(command));
        }
        _document.MarkDirty();
    }

    public void ExecuteTransactionSafe(string description, IEnumerable<ICommand> commands)
    {
        var transaction = _history.BeginTransactionInternal(description);
        var executed = new List<ICommand>();

        try
        {
            foreach (var command in commands)
            {
                CommandExecuting?.Invoke(this, new CommandEventArgs(command));
                command.Execute(_document);
                _history.Record(command);
                CommandExecuted?.Invoke(this, new CommandEventArgs(command));
                executed.Add(command);
            }

            _document.MarkDirty();
            transaction.Dispose();
        }
        catch
        {
            for (int i = executed.Count - 1; i >= 0; i--)
            {
                try
                {
                    executed[i].Undo(_document);
                }
                catch
                {
                }
            }

            transaction.Cancel();
            transaction.Dispose();
            throw;
        }
    }

    public void ExecuteTransaction(string description, IEnumerable<ICommand> commands)
    {
        ExecuteTransaction(description, commands.ToArray());
    }

    public bool Undo()
    {
        var hadProgress = false;

        while (true)
        {
            var command = _history.Undo();
            if (command == null) return hadProgress;

            try
            {
                command.Undo(_document);
                _document.MarkDirty();
                CommandUndone?.Invoke(this, new CommandEventArgs(command));
                return true;
            }
            catch (Exception ex)
            {
                _history.DiscardFailedUndo(command);
                StartupLogger.Log($"Undo failed for command '{command.CommandType}' ({command.Id}) and was discarded", ex);
                hadProgress = true;
            }
        }
    }

    public bool Redo()
    {
        var hadProgress = false;

        while (true)
        {
            var command = _history.Redo();
            if (command == null) return hadProgress;

            try
            {
                command.Execute(_document);
                _document.MarkDirty();
                CommandRedone?.Invoke(this, new CommandEventArgs(command));
                return true;
            }
            catch (Exception ex)
            {
                _history.DiscardFailedRedo(command);
                StartupLogger.Log($"Redo failed for command '{command.CommandType}' ({command.Id}) and was discarded", ex);
                hadProgress = true;
            }
        }
    }

    public void RegisterCommandFactory(string commandType, Func<CommandData, ICommand> factory)
    {
        _commandFactories[commandType] = factory;
    }

    public ICommand CreateCommand(CommandData data)
    {
        if (!_commandFactories.TryGetValue(data.Type, out var factory))
        {
            throw new InvalidOperationException($"No factory registered for command type: {data.Type}");
        }
        return factory(data);
    }

    public void ExecuteFromData(CommandData data)
    {
        var command = CreateCommand(data);
        Execute(command);
    }

    public void ExecuteFromData(string description, IEnumerable<CommandData> dataList)
    {
        var commands = dataList.Select(CreateCommand).ToArray();
        ExecuteTransaction(description, commands);
    }
}

public sealed class CommandEventArgs : EventArgs
{
    public ICommand Command { get; }

    public CommandEventArgs(ICommand command)
    {
        Command = command;
    }
}
