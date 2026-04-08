namespace Letterist.Commands;

public sealed class CommandHistory
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private readonly int _maxHistorySize;

    private CommandTransaction? _currentTransaction;

    public event EventHandler? HistoryChanged;

    public CommandHistory(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public int UndoCount => _undoStack.Count;

    public int RedoCount => _redoStack.Count;

    public ICommand? PeekUndo() => _undoStack.Count > 0 ? _undoStack.Peek() : null;

    public ICommand? PeekRedo() => _redoStack.Count > 0 ? _redoStack.Peek() : null;

    public bool IsInTransaction => _currentTransaction != null;

    public void Record(ICommand command)
    {
        if (_currentTransaction != null)
        {
            _currentTransaction.Add(command);
        }
        else
        {
            PushUndo(command);
        }
        _redoStack.Clear(); // New command invalidates redo history
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public ICommand? Undo()
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("Cannot undo while a transaction is in progress");
        }

        if (_undoStack.Count == 0) return null;

        var command = _undoStack.Pop();
        _redoStack.Push(command);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return command;
    }

    public ICommand? Redo()
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("Cannot redo while a transaction is in progress");
        }

        if (_redoStack.Count == 0) return null;

        var command = _redoStack.Pop();
        _undoStack.Push(command);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return command;
    }

    internal void DiscardFailedUndo(ICommand command)
    {
        if (_redoStack.Count == 0 || !ReferenceEquals(_redoStack.Peek(), command))
        {
            return;
        }

        _redoStack.Pop();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void DiscardFailedRedo(ICommand command)
    {
        if (_undoStack.Count == 0 || !ReferenceEquals(_undoStack.Peek(), command))
        {
            return;
        }

        _undoStack.Pop();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public IDisposable BeginTransaction(string description)
    {
        return BeginTransactionInternal(description);
    }

    internal CommandTransaction BeginTransactionInternal(string description)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress");
        }

        _currentTransaction = new CommandTransaction(this, description);
        return _currentTransaction;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentTransaction = null;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PushUndo(ICommand command)
    {
        _undoStack.Push(command);

        while (_undoStack.Count > _maxHistorySize)
        {
            var temp = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < temp.Length - 1; i++)
            {
                _undoStack.Push(temp[temp.Length - 1 - i]);
            }
        }
    }

    internal void CommitTransaction()
    {
        if (_currentTransaction == null) return;

        var commands = _currentTransaction.Commands;
        if (commands.Count > 0)
        {
            var composite = new CompositeCommand(
                _currentTransaction.Description,
                commands.ToArray());
            PushUndo(composite);
        }

        _currentTransaction = null;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void CancelTransaction()
    {
        _currentTransaction = null;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class CommandTransaction : IDisposable
{
    private readonly CommandHistory _history;
    private bool _disposed;
    private bool _canceled;

    public string Description { get; }
    public List<ICommand> Commands { get; } = new();

    public CommandTransaction(CommandHistory history, string description)
    {
        _history = history;
        Description = description;
    }

    public void Add(ICommand command)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CommandTransaction));
        Commands.Add(command);
    }

    public void Cancel()
    {
        if (_disposed) return;
        _canceled = true;
        _history.CancelTransaction();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_canceled)
        {
            _history.CommitTransaction();
        }
    }
}

public sealed class CompositeCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "Composite";
    public string Description { get; }

    private readonly ICommand[] _commands;

    public CompositeCommand(string description, params ICommand[] commands)
    {
        Id = Guid.NewGuid();
        Description = description;
        _commands = commands;
    }

    public void Execute(Model.Document document)
    {
        foreach (var command in _commands)
        {
            command.Execute(document);
        }
    }

    public void Undo(Model.Document document)
    {
        for (int i = _commands.Length - 1; i >= 0; i--)
        {
            _commands[i].Undo(document);
        }
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["description"] = Description,
                ["commands"] = _commands.Select(c => c.Serialize()).ToArray()
            }
        };
    }
}
