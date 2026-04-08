namespace Letterist.Model;

public sealed class Layer
{
    public Guid Id { get; }

    public string Name { get; private set; }

    public LayerKind Kind { get; }

    public string? ImagePath { get; private set; }

    public bool IsVisible { get; private set; }

    public bool IsLocked { get; private set; }

    public float Opacity { get; private set; }

    public LayerBlendMode BlendMode { get; private set; }

    public Guid? GroupId { get; private set; }

    public Rect PanelBounds { get; private set; }

    public PanelShape PanelShape { get; private set; }

    public float PanelCornerRadius { get; private set; }

    public Color PanelBorderColor { get; private set; }

    public float PanelBorderWidth { get; private set; }

    public PanelBorderStyle PanelBorderStyle { get; private set; }

    public int PanelOrder { get; private set; }

    public string? PanelImagePath { get; private set; }

    private readonly List<Balloon> _balloons = new();

    public IReadOnlyList<Balloon> Balloons => _balloons;


    public bool IsImageLayer => Kind == LayerKind.Image;
    public bool IsPanelLayer => Kind == LayerKind.Panel;
    public bool CanContainBalloons => Kind == LayerKind.Balloon || Kind == LayerKind.Panel;

    public Layer(Guid id, string name, LayerKind kind = LayerKind.Balloon, string? imagePath = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        ImagePath = kind == LayerKind.Image ? imagePath : null;
        IsVisible = true;
        IsLocked = kind == LayerKind.Image;
        Opacity = 1.0f;
        BlendMode = LayerBlendMode.Normal;

        PanelBounds = new Rect(0, 0, 400, 600);
        PanelShape = PanelShape.Rectangle;
        PanelCornerRadius = 0f;
        PanelBorderColor = new Color(30, 30, 30, 220);
        PanelBorderWidth = 2f;
        PanelBorderStyle = PanelBorderStyle.Solid;
        PanelOrder = 0;
    }

    public static Layer Create(string name)
    {
        return new Layer(Guid.NewGuid(), name);
    }

    public static Layer CreateBackground(string name = "Background", string? imagePath = null)
    {
        var layer = new Layer(Guid.NewGuid(), name, LayerKind.Image, imagePath)
        {
            IsLocked = false
        };
        return layer;
    }

    public static Layer CreatePanel(string name, Rect bounds, int order = 0)
    {
        var layer = new Layer(Guid.NewGuid(), name, LayerKind.Panel)
        {
            PanelBounds = bounds,
            PanelOrder = order,
            IsLocked = false
        };
        return layer;
    }

    public Balloon? FindBalloon(Guid balloonId)
    {
        return _balloons.FirstOrDefault(b => b.Id == balloonId);
    }

    public bool ContainsBalloon(Guid balloonId)
    {
        return _balloons.Any(b => b.Id == balloonId);
    }

    public int IndexOfBalloon(Guid balloonId)
    {
        return _balloons.FindIndex(b => b.Id == balloonId);
    }

    public Layer Clone()
    {
        var clone = new Layer(Id, Name, Kind, ImagePath)
        {
            IsVisible = IsVisible,
            IsLocked = IsLocked,
            Opacity = Opacity,
            BlendMode = BlendMode,
            GroupId = GroupId,
            PanelBounds = PanelBounds,
            PanelShape = PanelShape,
            PanelCornerRadius = PanelCornerRadius,
            PanelBorderColor = PanelBorderColor,
            PanelBorderWidth = PanelBorderWidth,
            PanelBorderStyle = PanelBorderStyle,
            PanelOrder = PanelOrder,
            PanelImagePath = PanelImagePath
        };
        foreach (var balloon in _balloons)
        {
            clone._balloons.Add(balloon.Clone());
        }
        return clone;
    }

    internal void SetName(string name) => Name = name;
    internal void SetVisible(bool visible) => IsVisible = visible;
    internal void SetLocked(bool locked) => IsLocked = locked;
    internal void SetOpacity(float opacity) => Opacity = Math.Clamp(opacity, 0f, 1f);
    internal void SetBlendMode(LayerBlendMode blendMode) => BlendMode = blendMode;
    internal void SetGroupId(Guid? groupId) => GroupId = groupId;
    internal void SetImagePath(string? path)
    {
        if (Kind != LayerKind.Image)
        {
            throw new InvalidOperationException("Only image layers can store image paths.");
        }

        ImagePath = path;
    }

    internal void AddBalloon(Balloon balloon)
    {
        if (!CanContainBalloons)
        {
            throw new InvalidOperationException("Cannot add balloons to an image layer.");
        }

        if (balloon.LayerId != Id)
        {
            throw new InvalidOperationException($"Balloon {balloon.Id} belongs to layer {balloon.LayerId}, not {Id}");
        }
        _balloons.Add(balloon);
    }

    internal void InsertBalloon(int index, Balloon balloon)
    {
        if (balloon.LayerId != Id)
        {
            throw new InvalidOperationException($"Balloon {balloon.Id} belongs to layer {balloon.LayerId}, not {Id}");
        }
        _balloons.Insert(Math.Clamp(index, 0, _balloons.Count), balloon);
    }

    internal bool RemoveBalloon(Guid balloonId)
    {
        var index = IndexOfBalloon(balloonId);
        if (index >= 0)
        {
            _balloons.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal void ReorderBalloon(Guid balloonId, int newIndex)
    {
        var oldIndex = IndexOfBalloon(balloonId);
        if (oldIndex < 0) return;

        var balloon = _balloons[oldIndex];
        _balloons.RemoveAt(oldIndex);
        _balloons.Insert(Math.Clamp(newIndex, 0, _balloons.Count), balloon);
    }

    internal void ClearBalloons() => _balloons.Clear();

    internal void SetPanelBounds(Rect bounds) => PanelBounds = bounds;
    internal void SetPanelShape(PanelShape shape) => PanelShape = shape;
    internal void SetPanelCornerRadius(float radius) => PanelCornerRadius = Math.Max(0f, radius);
    internal void SetPanelBorderColor(Color color) => PanelBorderColor = color;
    internal void SetPanelBorderWidth(float width) => PanelBorderWidth = Math.Max(0f, width);
    internal void SetPanelBorderStyle(PanelBorderStyle style) => PanelBorderStyle = style;
    internal void SetPanelOrder(int order) => PanelOrder = order;
    internal void SetPanelImagePath(string? path) => PanelImagePath = path;
}
