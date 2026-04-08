namespace Letterist.Model;

public sealed class NamedTextStyle
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public TextStyle Style { get; private set; }
    public Guid? ParentStyleId { get; private set; }
    public TextStyleOverride Overrides { get; private set; }

    public NamedTextStyle(Guid id, string name, TextStyle style, Guid? parentStyleId = null, TextStyleOverride? overrides = null)
    {
        Id = id;
        Name = name;
        Style = style;
        ParentStyleId = parentStyleId;
        Overrides = overrides ?? TextStyleOverride.FromStyle(style);
    }

    public static NamedTextStyle Create(string name, TextStyle? style = null)
    {
        return new NamedTextStyle(Guid.NewGuid(), name, style ?? TextStyle.Default);
    }

    public NamedTextStyle Clone()
    {
        return new NamedTextStyle(Id, Name, Style, ParentStyleId, Overrides.Clone());
    }

    internal void SetName(string name) => Name = name;
    internal void SetStyle(TextStyle style) => Style = style;
    internal void SetParentStyleId(Guid? parentStyleId) => ParentStyleId = parentStyleId;
    internal void SetOverrides(TextStyleOverride overrides) => Overrides = overrides ?? TextStyleOverride.Empty;
}
