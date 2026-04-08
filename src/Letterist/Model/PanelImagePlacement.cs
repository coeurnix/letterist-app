namespace Letterist.Model;

public enum PanelImageFitMode
{
    Fill,
    Fit,
    Stretch,
    Original
}

public readonly struct PanelImagePlacement : IEquatable<PanelImagePlacement>
{
    public PanelImagePlacement(Point2 offset, float scale, PanelImageFitMode fitMode = PanelImageFitMode.Fill, bool isLocked = false, float opacity = 1f)
    {
        Offset = offset;
        Scale = scale;
        FitMode = fitMode;
        IsLocked = isLocked;
        Opacity = Math.Clamp(opacity, 0f, 1f);
    }

    public Point2 Offset { get; }
    public float Scale { get; }
    public PanelImageFitMode FitMode { get; }
    public bool IsLocked { get; }
    public float Opacity { get; }

    public PanelImagePlacement With(Point2? offset = null, float? scale = null, PanelImageFitMode? fitMode = null, bool? isLocked = null, float? opacity = null)
    {
        return new PanelImagePlacement(
            offset ?? Offset,
            scale ?? Scale,
            fitMode ?? FitMode,
            isLocked ?? IsLocked,
            opacity ?? Opacity);
    }

    public bool Equals(PanelImagePlacement other)
    {
        return Offset.Equals(other.Offset)
            && Math.Abs(Scale - other.Scale) < 0.0001f
            && FitMode == other.FitMode
            && IsLocked == other.IsLocked
            && Math.Abs(Opacity - other.Opacity) < 0.0001f;
    }

    public override bool Equals(object? obj) => obj is PanelImagePlacement other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Offset, Scale, FitMode, IsLocked, Opacity);

    public static bool operator ==(PanelImagePlacement left, PanelImagePlacement right) => left.Equals(right);
    public static bool operator !=(PanelImagePlacement left, PanelImagePlacement right) => !left.Equals(right);
}
