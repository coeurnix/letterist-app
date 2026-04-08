using Letterist.Model;

namespace Letterist.Commands;

public sealed class CreateTailCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "CreateTail";
    public string Description => "Add tail";

    private readonly Guid _balloonId;
    private readonly Guid _tailId;
    private readonly Point2 _targetPoint;
    private readonly TailStyle _style;
    private readonly float _baseWidth;

    public Guid CreatedTailId => _tailId;

    public CreateTailCommand(
        Guid balloonId,
        Point2 targetPoint,
        TailStyle style = TailStyle.Pointer,
        float baseWidth = 16f,
        Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId ?? Guid.NewGuid();
        _targetPoint = targetPoint;
        _style = style;
        _baseWidth = baseWidth;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = new Tail(_tailId, _targetPoint, _style, _baseWidth);
        balloon.AddTail(tail);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        balloon.RemoveTail(_tailId);
    }

    public CommandData Serialize()
    {
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = new Dictionary<string, object?>
            {
                ["balloonId"] = _balloonId,
                ["tailId"] = _tailId,
                ["targetX"] = _targetPoint.X,
                ["targetY"] = _targetPoint.Y,
                ["style"] = _style.ToString(),
                ["baseWidth"] = _baseWidth
            }
        };
    }
}

public sealed class MoveTailTargetCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "MoveTailTarget";
    public string Description => "Move tail";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly Point2 _newTargetPoint;
    private Point2 _oldTargetPoint;

    public MoveTailTargetCommand(Guid balloonId, Point2 newTargetPoint, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newTargetPoint = newTargetPoint;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldTargetPoint = tail.TargetPoint;
        tail.SetTargetPoint(_newTargetPoint);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetTargetPoint(_oldTargetPoint);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["newTargetX"] = _newTargetPoint.X,
            ["newTargetY"] = _newTargetPoint.Y
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class DeleteTailCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "DeleteTail";
    public string Description => "Remove tail";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private Tail? _deletedTail;

    public DeleteTailCommand(Guid balloonId, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;

        if (tail != null)
        {
            _deletedTail = tail.Clone();
            balloon.RemoveTail(tail.Id);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        if (_deletedTail != null)
        {
            balloon.AddTail(_deletedTail.Clone());
        }
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetBalloonTailsFromTemplatesCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetBalloonTailsFromTemplates";
    public string Description => "Set balloon tails";

    private readonly Guid _balloonId;
    private readonly List<BalloonTemplateTail> _templates;
    private readonly bool _preservePlacement;
    private List<Tail>? _oldTails;

    public SetBalloonTailsFromTemplatesCommand(
        Guid balloonId,
        IEnumerable<BalloonTemplateTail>? templates,
        bool preservePlacement = true)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _templates = templates?.Select(template => template.Clone()).ToList() ?? new List<BalloonTemplateTail>();
        _preservePlacement = preservePlacement;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        _oldTails = balloon.Tails.Select(tail => tail.Clone()).ToList();

        var appliedTails = new List<Tail>();
        for (var index = 0; index < _templates.Count; index++)
        {
            var appliedTail = _templates[index].CreateTailAt(balloon.Position);
            if (_preservePlacement && _oldTails.Count > index)
            {
                CopyTailPlacement(_oldTails[index], appliedTail);
            }

            appliedTails.Add(appliedTail);
        }

        balloon.ClearTails();
        foreach (var tail in appliedTails)
        {
            balloon.AddTail(tail);
        }
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        balloon.ClearTails();
        if (_oldTails == null)
        {
            return;
        }

        foreach (var tail in _oldTails)
        {
            balloon.AddTail(tail.Clone());
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
                ["balloonId"] = _balloonId,
                ["preservePlacement"] = _preservePlacement,
                ["tails"] = _templates.Select(template => template.Clone()).ToList()
            }
        };
    }

    private static void CopyTailPlacement(Tail source, Tail destination)
    {
        destination.SetTargetPoint(source.TargetPoint);
        destination.SetAttachmentDirection(source.AttachmentDirection);
        destination.SetControlPoint(source.ControlPoint);
        destination.SetCurveCenter(source.CurveCenter);
        destination.SetInset(source.Inset);
    }
}

public sealed class SetTailWidthCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailWidth";
    public string Description => "Change tail width";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly float _newWidth;
    private float _oldWidth;

    public SetTailWidthCommand(Guid balloonId, float newWidth, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newWidth = newWidth;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldWidth = tail.BaseWidth;
        tail.SetBaseWidth(_newWidth);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetBaseWidth(_oldWidth);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["newWidth"] = _newWidth
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailStyleCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailStyle";
    public string Description => "Change tail style";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly TailStyle _newStyle;
    private TailStyle _oldStyle;

    public SetTailStyleCommand(Guid balloonId, TailStyle newStyle, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newStyle = newStyle;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldStyle = tail.Style;
        tail.SetStyle(_newStyle);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetStyle(_oldStyle);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["newStyle"] = _newStyle.ToString()
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailAttachmentDirectionCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailAttachment";
    public string Description => "Set tail attachment";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly Point2? _newDirection;
    private Point2? _oldDirection;

    public SetTailAttachmentDirectionCommand(Guid balloonId, Point2? newDirection, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newDirection = newDirection;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldDirection = tail.AttachmentDirection;
        tail.SetAttachmentDirection(_newDirection);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetAttachmentDirection(_oldDirection);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        if (_newDirection.HasValue)
        {
            parameters["directionX"] = _newDirection.Value.X;
            parameters["directionY"] = _newDirection.Value.Y;
        }
        else
        {
            parameters["clear"] = true;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailCurvatureCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailCurvature";
    public string Description => "Adjust tail curve";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly float _newCurvature;
    private float _oldCurvature;

    public SetTailCurvatureCommand(Guid balloonId, float newCurvature, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newCurvature = Math.Clamp(newCurvature, -2f, 2f);
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldCurvature = tail.Curvature;
        tail.SetCurvature(_newCurvature);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetCurvature(_oldCurvature);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["curvature"] = _newCurvature
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailCurveCenterCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailCurveCenter";
    public string Description => "Adjust tail curve center";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly float _newCurveCenter;
    private float _oldCurveCenter;

    public SetTailCurveCenterCommand(Guid balloonId, float newCurveCenter, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newCurveCenter = Math.Clamp(newCurveCenter, 0f, 1f);
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldCurveCenter = tail.CurveCenter;
        tail.SetCurveCenter(_newCurveCenter);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command

        tail.SetCurveCenter(_oldCurveCenter);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["curveCenter"] = _newCurveCenter
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailInsetCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailInset";
    public string Description => "Adjust tail inset";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly float _newInset;
    private float _oldInset;

    public SetTailInsetCommand(Guid balloonId, float newInset, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newInset = Math.Clamp(newInset, 0f, 64f);
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldInset = tail.Inset;
        tail.SetInset(_newInset);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetInset(_oldInset);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId,
            ["inset"] = _newInset
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}

public sealed class SetTailControlPointCommand : ICommand
{
    public Guid Id { get; }
    public string CommandType => "SetTailControlPoint";
    public string Description => "Set tail control point";

    private readonly Guid _balloonId;
    private readonly Guid? _tailId;
    private readonly Point2? _newControlPoint;
    private Point2? _oldControlPoint;

    public SetTailControlPointCommand(Guid balloonId, Point2? newControlPoint, Guid? tailId = null)
    {
        Id = Guid.NewGuid();
        _balloonId = balloonId;
        _tailId = tailId;
        _newControlPoint = newControlPoint;
    }

    public void Execute(Document document)
    {
        var balloon = document.FindBalloon(_balloonId)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} not found");

        var tail = GetTargetTail(balloon)
            ?? throw new InvalidOperationException($"Balloon {_balloonId} has no tail");

        _oldControlPoint = tail.ControlPoint;
        tail.SetControlPoint(_newControlPoint);
    }

    public void Undo(Document document)
    {
        var balloon = document.FindBalloon(_balloonId);
        if (balloon == null) return; // Balloon may have been removed by a subsequent command.

        var tail = GetTargetTail(balloon);
        if (tail == null) return; // Tail may have been removed by a subsequent command.

        tail.SetControlPoint(_oldControlPoint);
    }

    private Tail? GetTargetTail(Balloon balloon)
    {
        return _tailId.HasValue ? balloon.FindTail(_tailId.Value) : balloon.Tail;
    }

    public CommandData Serialize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["balloonId"] = _balloonId
        };
        if (_tailId.HasValue)
        {
            parameters["tailId"] = _tailId.Value;
        }
        if (_newControlPoint.HasValue)
        {
            parameters["controlX"] = _newControlPoint.Value.X;
            parameters["controlY"] = _newControlPoint.Value.Y;
        }
        else
        {
            parameters["clear"] = true;
        }
        return new CommandData
        {
            Id = Id,
            Type = CommandType,
            Parameters = parameters
        };
    }
}
