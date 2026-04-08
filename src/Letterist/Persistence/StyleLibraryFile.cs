using Letterist.Model;

namespace Letterist.Persistence;

internal sealed class StyleLibraryFile
{
    public int Version { get; set; } = 1;
    public List<NamedBalloonStyleFile> BalloonStyles { get; set; } = new();
    public List<NamedTextStyleFile> TextStyles { get; set; } = new();

    public static StyleLibraryFile FromDocument(Document document)
    {
        return new StyleLibraryFile
        {
            BalloonStyles = document.BalloonStyles.Select(NamedBalloonStyleFile.FromStyle).ToList(),
            TextStyles = document.TextStyles.Select(NamedTextStyleFile.FromStyle).ToList()
        };
    }
}

internal sealed class NamedBalloonStyleFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public BalloonStyle Style { get; set; } = BalloonStyle.Default;
    public Guid? ParentStyleId { get; set; }
    public BalloonStyleOverride? Overrides { get; set; }
    public bool IsQuickSelect { get; set; } = true;
    public bool ApplyExtendedDetails { get; set; } = true;
    public BalloonShape Shape { get; set; } = BalloonShape.Oval;
    public string? CustomShapePathData { get; set; }
    public bool ConstrainToPanel { get; set; }
    public TextStyle TextStyle { get; set; } = TextStyle.Default;
    public TextPath? TextPath { get; set; }
    public List<NamedBalloonStyleTailFile> Tails { get; set; } = new();

    public static NamedBalloonStyleFile FromStyle(NamedBalloonStyle style)
    {
        return new NamedBalloonStyleFile
        {
            Id = style.Id,
            Name = style.Name,
            Style = style.Style,
            ParentStyleId = style.ParentStyleId,
            Overrides = style.Overrides.Clone(),
            IsQuickSelect = style.IsQuickSelect,
            ApplyExtendedDetails = style.ApplyExtendedDetails,
            Shape = style.Shape,
            CustomShapePathData = style.CustomShapePathData,
            ConstrainToPanel = style.ConstrainToPanel,
            TextStyle = style.TextStyle,
            TextPath = style.TextPath?.Clone(),
            Tails = style.Tails.Select(NamedBalloonStyleTailFile.FromTail).ToList()
        };
    }

    public NamedBalloonStyle ToStyle()
    {
        return new NamedBalloonStyle(
            Id,
            Name,
            Style,
            ParentStyleId,
            Overrides,
            IsQuickSelect,
            ApplyExtendedDetails,
            Shape,
            CustomShapePathData,
            ConstrainToPanel,
            TextStyle,
            TextPath?.Clone(),
            Tails.Select(tail => tail.ToTail()));
    }
}

internal sealed class NamedBalloonStyleTailFile
{
    public Point2 TargetOffset { get; set; }
    public TailStyle Style { get; set; } = TailStyle.Pointer;
    public float BaseWidth { get; set; } = 16f;
    public Point2? AttachmentDirection { get; set; }
    public Point2? ControlPointOffset { get; set; }
    public float Curvature { get; set; } = 0.3f;
    public float CurveCenter { get; set; } = 0.5f;
    public float Inset { get; set; }

    public static NamedBalloonStyleTailFile FromTail(BalloonTemplateTail tail)
    {
        return new NamedBalloonStyleTailFile
        {
            TargetOffset = tail.TargetOffset,
            Style = tail.Style,
            BaseWidth = tail.BaseWidth,
            AttachmentDirection = tail.AttachmentDirection,
            ControlPointOffset = tail.ControlPointOffset,
            Curvature = tail.Curvature,
            CurveCenter = tail.CurveCenter,
            Inset = tail.Inset
        };
    }

    public BalloonTemplateTail ToTail()
    {
        return new BalloonTemplateTail(TargetOffset, Style, BaseWidth, AttachmentDirection, ControlPointOffset, Curvature, CurveCenter, Inset);
    }
}

internal sealed class NamedTextStyleFile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public TextStyle Style { get; set; } = TextStyle.Default;
    public Guid? ParentStyleId { get; set; }
    public TextStyleOverride? Overrides { get; set; }

    public static NamedTextStyleFile FromStyle(NamedTextStyle style)
    {
        return new NamedTextStyleFile
        {
            Id = style.Id,
            Name = style.Name,
            Style = style.Style,
            ParentStyleId = style.ParentStyleId,
            Overrides = style.Overrides.Clone()
        };
    }

    public NamedTextStyle ToStyle()
    {
        return new NamedTextStyle(Id, Name, Style, ParentStyleId, Overrides);
    }
}
