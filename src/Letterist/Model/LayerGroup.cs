namespace Letterist.Model;

public sealed class LayerGroup
{
    public Guid Id { get; }

    public string Name { get; private set; }

    public bool IsExpanded { get; private set; }

    public bool IsVisible { get; private set; }

    public bool IsLocked { get; private set; }

    public LayerGroup(Guid id, string name)
    {
        Id = id;
        Name = name;
        IsExpanded = true;
        IsVisible = true;
        IsLocked = false;
    }

    public static LayerGroup Create(string name)
    {
        return new LayerGroup(Guid.NewGuid(), name);
    }

    public LayerGroup Clone()
    {
        return new LayerGroup(Id, Name)
        {
            IsExpanded = IsExpanded,
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
    }

    internal void SetName(string name) => Name = name;
    internal void SetExpanded(bool expanded) => IsExpanded = expanded;
    internal void SetVisible(bool visible) => IsVisible = visible;
    internal void SetLocked(bool locked) => IsLocked = locked;
}
