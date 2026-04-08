namespace Letterist.Model;

public sealed class PanelZone
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public Rect Bounds { get; private set; }
    public int Order { get; private set; }
    public Color Color { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsLocked { get; private set; }
    public PanelShape Shape { get; private set; }
    public float CornerRadius { get; private set; }
    public float SafeMargin { get; private set; }
    public string? CustomShapePathData { get; private set; }
    public string? ImagePath { get; private set; }
    public PanelImagePlacement? ImagePlacement { get; private set; }
    public bool IsImageVisibleInExport { get; private set; }
    public float? GutterLeftOverride { get; private set; }
    public float? GutterTopOverride { get; private set; }
    public float? GutterRightOverride { get; private set; }
    public float? GutterBottomOverride { get; private set; }
    public float BleedLeft { get; private set; }
    public float BleedTop { get; private set; }
    public float BleedRight { get; private set; }
    public float BleedBottom { get; private set; }

    public Color BorderColor { get; private set; }
    public float BorderWidth { get; private set; }
    public PanelBorderStyle BorderStyle { get; private set; }

    public bool HasImage => ImagePlacement.HasValue || !string.IsNullOrWhiteSpace(ImagePath);

    public static Color DefaultBorderColor => new(30, 30, 30, 220);
    public const float DefaultBorderWidth = 2f;

    public PanelZone(
        Guid id,
        string name,
        Rect bounds,
        int order,
        Color? color = null,
        bool isVisible = true,
        bool isLocked = false,
        PanelShape shape = PanelShape.Rectangle,
        float cornerRadius = 0f,
        float safeMargin = 0f,
        string? customShapePathData = null,
        string? imagePath = null,
        PanelImagePlacement? imagePlacement = null,
        Color? borderColor = null,
        float? borderWidth = null,
        PanelBorderStyle borderStyle = PanelBorderStyle.Solid,
        bool isImageVisibleInExport = true,
        float? gutterLeftOverride = null,
        float? gutterTopOverride = null,
        float? gutterRightOverride = null,
        float? gutterBottomOverride = null,
        float bleedLeft = 0f,
        float bleedTop = 0f,
        float bleedRight = 0f,
        float bleedBottom = 0f)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? "Panel" : name.Trim();
        Bounds = bounds;
        Order = order;
        Color = color ?? DefaultColor;
        IsVisible = isVisible;
        IsLocked = isLocked;
        Shape = shape;
        CornerRadius = Math.Max(0f, cornerRadius);
        SafeMargin = Math.Max(0f, safeMargin);
        CustomShapePathData = customShapePathData;
        ImagePath = imagePath;
        ImagePlacement = imagePlacement;
        IsImageVisibleInExport = isImageVisibleInExport;
        BorderColor = borderColor ?? DefaultBorderColor;
        BorderWidth = borderWidth ?? DefaultBorderWidth;
        BorderStyle = borderStyle;
        GutterLeftOverride = gutterLeftOverride;
        GutterTopOverride = gutterTopOverride;
        GutterRightOverride = gutterRightOverride;
        GutterBottomOverride = gutterBottomOverride;
        BleedLeft = Math.Max(0f, bleedLeft);
        BleedTop = Math.Max(0f, bleedTop);
        BleedRight = Math.Max(0f, bleedRight);
        BleedBottom = Math.Max(0f, bleedBottom);
    }

    public static Color DefaultColor => new(238, 170, 64, 200);

    public static PanelZone Create(string name, Rect bounds, int order)
    {
        return new PanelZone(Guid.NewGuid(), name, bounds, order, DefaultColor, isVisible: true, isLocked: false);
    }

    public PanelZone Clone()
    {
        return new PanelZone(
            Id,
            Name,
            Bounds,
            Order,
            Color,
            IsVisible,
            IsLocked,
            Shape,
            CornerRadius,
            SafeMargin,
            CustomShapePathData,
            ImagePath,
            ImagePlacement,
            BorderColor,
            BorderWidth,
            BorderStyle,
            IsImageVisibleInExport,
            GutterLeftOverride,
            GutterTopOverride,
            GutterRightOverride,
            GutterBottomOverride,
            BleedLeft,
            BleedTop,
            BleedRight,
            BleedBottom);
    }

    public PanelZone CloneWithNewId(bool includeImage = true)
    {
        return new PanelZone(
            Guid.NewGuid(),
            Name,
            Bounds,
            Order,
            Color,
            IsVisible,
            IsLocked,
            Shape,
            CornerRadius,
            SafeMargin,
            CustomShapePathData,
            includeImage ? ImagePath : null,
            includeImage ? ImagePlacement : null,
            BorderColor,
            BorderWidth,
            BorderStyle,
            IsImageVisibleInExport,
            GutterLeftOverride,
            GutterTopOverride,
            GutterRightOverride,
            GutterBottomOverride,
            BleedLeft,
            BleedTop,
            BleedRight,
            BleedBottom);
    }

    public PanelZone CloneWithoutImage()
    {
        return new PanelZone(
            Id,
            Name,
            Bounds,
            Order,
            Color,
            IsVisible,
            IsLocked,
            Shape,
            CornerRadius,
            SafeMargin,
            CustomShapePathData,
            borderColor: BorderColor,
            borderWidth: BorderWidth,
            borderStyle: BorderStyle,
            isImageVisibleInExport: IsImageVisibleInExport,
            gutterLeftOverride: GutterLeftOverride,
            gutterTopOverride: GutterTopOverride,
            gutterRightOverride: GutterRightOverride,
            gutterBottomOverride: GutterBottomOverride,
            bleedLeft: BleedLeft,
            bleedTop: BleedTop,
            bleedRight: BleedRight,
            bleedBottom: BleedBottom);
    }

    internal void SetName(string name) => Name = string.IsNullOrWhiteSpace(name) ? Name : name.Trim();
    internal void SetBounds(Rect bounds) => Bounds = bounds;
    internal void SetOrder(int order) => Order = order;
    internal void SetColor(Color color) => Color = color;
    internal void SetVisible(bool visible) => IsVisible = visible;
    internal void SetLocked(bool locked) => IsLocked = locked;
    internal void SetShape(PanelShape shape) => Shape = shape;
    internal void SetCornerRadius(float radius) => CornerRadius = Math.Max(0f, radius);
    internal void SetSafeMargin(float margin) => SafeMargin = Math.Max(0f, margin);
    internal void SetCustomShapePathData(string? data) => CustomShapePathData = data;
    internal void SetImagePath(string? path) => ImagePath = path;
    internal void SetImagePlacement(PanelImagePlacement? placement) => ImagePlacement = placement;
    internal void SetImageVisibleInExport(bool visible) => IsImageVisibleInExport = visible;
    internal void SetBorderColor(Color color) => BorderColor = color;
    internal void SetBorderWidth(float width) => BorderWidth = Math.Max(0f, width);
    internal void SetBorderStyle(PanelBorderStyle style) => BorderStyle = style;
    internal void SetGutterOverrides(float? left, float? top, float? right, float? bottom)
    {
        GutterLeftOverride = left;
        GutterTopOverride = top;
        GutterRightOverride = right;
        GutterBottomOverride = bottom;
    }
    internal void SetBleed(float left, float top, float right, float bottom)
    {
        BleedLeft = Math.Max(0f, left);
        BleedTop = Math.Max(0f, top);
        BleedRight = Math.Max(0f, right);
        BleedBottom = Math.Max(0f, bottom);
    }
    internal void SetImage(string? path, PanelImagePlacement? placement)
    {
        ImagePath = path;
        ImagePlacement = placement;
    }
}
