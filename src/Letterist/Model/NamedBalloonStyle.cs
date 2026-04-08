namespace Letterist.Model;

public sealed class NamedBalloonStyle
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public BalloonStyle Style { get; private set; }
    public Guid? ParentStyleId { get; private set; }
    public BalloonStyleOverride Overrides { get; private set; }
    public bool IsQuickSelect { get; private set; }
    public bool ApplyExtendedDetails { get; private set; }
    public BalloonShape Shape { get; private set; }
    public string? CustomShapePathData { get; private set; }
    public bool ConstrainToPanel { get; private set; }
    public TextStyle TextStyle { get; private set; }
    public TextPath? TextPath { get; private set; }

    private readonly List<BalloonTemplateTail> _tails = new();
    public IReadOnlyList<BalloonTemplateTail> Tails => _tails;

    public NamedBalloonStyle(
        Guid id,
        string name,
        BalloonStyle style,
        Guid? parentStyleId = null,
        BalloonStyleOverride? overrides = null,
        bool isQuickSelect = true,
        bool applyExtendedDetails = true,
        BalloonShape shape = BalloonShape.Oval,
        string? customShapePathData = null,
        bool constrainToPanel = false,
        TextStyle? textStyle = null,
        TextPath? textPath = null,
        IEnumerable<BalloonTemplateTail>? tails = null)
    {
        Id = id;
        Name = name;
        Style = style;
        ParentStyleId = parentStyleId;
        Overrides = overrides ?? BalloonStyleOverride.FromStyle(style);
        IsQuickSelect = isQuickSelect;
        ApplyExtendedDetails = true;
        Shape = shape;
        CustomShapePathData = customShapePathData;
        ConstrainToPanel = constrainToPanel;
        TextStyle = textStyle ?? TextStyle.Default;
        TextPath = textPath?.Clone();
        SetTails(tails);
    }

    public static NamedBalloonStyle Create(string name, BalloonStyle? style = null, bool isQuickSelect = true)
    {
        return new NamedBalloonStyle(Guid.NewGuid(), name, style ?? BalloonStyle.Default, isQuickSelect: isQuickSelect);
    }

    public NamedBalloonStyle Clone()
    {
        return new NamedBalloonStyle(
            Id,
            Name,
            Style,
            ParentStyleId,
            Overrides.Clone(),
            IsQuickSelect,
            ApplyExtendedDetails,
            Shape,
            CustomShapePathData,
            ConstrainToPanel,
            TextStyle,
            TextPath?.Clone(),
            _tails.Select(tail => tail.Clone()));
    }

    internal void SetName(string name) => Name = name;
    internal void SetStyle(BalloonStyle style) => Style = style;
    internal void SetParentStyleId(Guid? parentStyleId) => ParentStyleId = parentStyleId;
    internal void SetOverrides(BalloonStyleOverride overrides) => Overrides = overrides ?? BalloonStyleOverride.Empty;
    internal void SetQuickSelect(bool isQuickSelect) => IsQuickSelect = isQuickSelect;
    internal void SetExtendedDetails(
        bool applyExtendedDetails,
        BalloonShape shape,
        string? customShapePathData,
        bool constrainToPanel,
        TextStyle textStyle,
        TextPath? textPath,
        IEnumerable<BalloonTemplateTail>? tails)
    {
        ApplyExtendedDetails = true;
        Shape = shape;
        CustomShapePathData = customShapePathData;
        ConstrainToPanel = constrainToPanel;
        TextStyle = textStyle ?? TextStyle.Default;
        TextPath = textPath?.Clone();
        SetTails(tails);
    }

    private void SetTails(IEnumerable<BalloonTemplateTail>? tails)
    {
        _tails.Clear();
        if (tails == null)
        {
            return;
        }

        _tails.AddRange(tails.Where(tail => tail != null).Select(tail => tail.Clone()));
    }
}
