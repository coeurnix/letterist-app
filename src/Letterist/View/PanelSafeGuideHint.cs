namespace Letterist.View;

public enum PanelSafeGuideHintKind
{
    Normal,
    Inside,
    Outside
}

public readonly struct PanelSafeGuideHint
{
    public PanelSafeGuideHint(Guid panelId, PanelSafeGuideHintKind kind)
    {
        PanelId = panelId;
        Kind = kind;
    }

    public Guid PanelId { get; }
    public PanelSafeGuideHintKind Kind { get; }
}
