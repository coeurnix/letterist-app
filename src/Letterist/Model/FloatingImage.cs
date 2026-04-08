namespace Letterist.Model;

public sealed class FloatingImage
{
    public Guid Id { get; }
    public Guid? LayerId { get; private set; }
    public Guid? PanelId { get; private set; }
    public bool ConstrainToPanel { get; private set; }
    public string? Name { get; private set; }
    public string? Source { get; private set; }
    public string? ImagePath { get; private set; }
    public Rect Bounds { get; private set; }
    public float Rotation { get; private set; }
    public float Opacity { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsLocked { get; private set; }
    public bool ShadowEnabled { get; private set; }
    public Color ShadowColor { get; private set; }
    public float ShadowOpacity { get; private set; }
    public float ShadowOffsetX { get; private set; }
    public float ShadowOffsetY { get; private set; }
    public float ShadowFalloff { get; private set; }
    public bool GlowEnabled { get; private set; }
    public Color GlowColor { get; private set; }
    public float GlowOpacity { get; private set; }
    public float GlowSize { get; private set; }

    public FloatingImage(
        Guid id,
        string? imagePath,
        Rect bounds,
        float opacity = 1f,
        bool isVisible = true,
        bool isLocked = false,
        Guid? layerId = null,
        Guid? panelId = null,
        string? name = null,
        string? source = null,
        float rotation = 0f,
        bool shadowEnabled = false,
        Color? shadowColor = null,
        float shadowOpacity = 0.35f,
        float shadowOffsetX = 4f,
        float shadowOffsetY = 4f,
        float shadowFalloff = 8f,
        bool glowEnabled = false,
        Color? glowColor = null,
        float glowOpacity = 0.5f,
        float glowSize = 6f,
        bool constrainToPanel = true)
    {
        Id = id;
        LayerId = layerId;
        PanelId = panelId;
        ConstrainToPanel = panelId.HasValue && constrainToPanel;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        ImagePath = imagePath;
        Bounds = bounds;
        Rotation = rotation;
        Opacity = Math.Clamp(opacity, 0f, 1f);
        IsVisible = isVisible;
        IsLocked = isLocked;
        ShadowEnabled = shadowEnabled;
        ShadowColor = shadowColor ?? Color.Black;
        ShadowOpacity = Math.Clamp(shadowOpacity, 0f, 1f);
        ShadowOffsetX = shadowOffsetX;
        ShadowOffsetY = shadowOffsetY;
        ShadowFalloff = Math.Max(0f, shadowFalloff);
        GlowEnabled = glowEnabled;
        GlowColor = glowColor ?? Color.Yellow;
        GlowOpacity = Math.Clamp(glowOpacity, 0f, 1f);
        GlowSize = Math.Max(0f, glowSize);
    }

    public FloatingImage Clone()
    {
        return new FloatingImage(
            Id,
            ImagePath,
            Bounds,
            Opacity,
            IsVisible,
            IsLocked,
            LayerId,
            PanelId,
            Name,
            Source,
            Rotation,
            ShadowEnabled,
            ShadowColor,
            ShadowOpacity,
            ShadowOffsetX,
            ShadowOffsetY,
            ShadowFalloff,
            GlowEnabled,
            GlowColor,
            GlowOpacity,
            GlowSize,
            ConstrainToPanel);
    }

    internal void SetLayerId(Guid? layerId) => LayerId = layerId;
    internal void SetPanelId(Guid? panelId)
    {
        PanelId = panelId;
        if (!panelId.HasValue)
        {
            ConstrainToPanel = false;
        }
    }
    internal void SetConstrainToPanel(bool constrain) => ConstrainToPanel = PanelId.HasValue && constrain;
    internal void SetSource(string? source) => Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    internal void SetImagePath(string? path) => ImagePath = path;
    internal void SetBounds(Rect bounds) => Bounds = bounds;
    internal void SetRotation(float rotation) => Rotation = rotation;
    internal void SetOpacity(float opacity) => Opacity = Math.Clamp(opacity, 0f, 1f);
    internal void SetVisible(bool visible) => IsVisible = visible;
    internal void SetLocked(bool locked) => IsLocked = locked;
    internal void SetName(string? name) => Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    internal void SetShadowStyle(bool enabled, Color color, float opacity, float offsetX, float offsetY, float falloff)
    {
        ShadowEnabled = enabled;
        ShadowColor = color;
        ShadowOpacity = Math.Clamp(opacity, 0f, 1f);
        ShadowOffsetX = offsetX;
        ShadowOffsetY = offsetY;
        ShadowFalloff = Math.Max(0f, falloff);
    }

    internal void SetGlowStyle(bool enabled, Color color, float opacity, float size)
    {
        GlowEnabled = enabled;
        GlowColor = color;
        GlowOpacity = Math.Clamp(opacity, 0f, 1f);
        GlowSize = Math.Max(0f, size);
    }
}
