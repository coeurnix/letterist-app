namespace Letterist.View;

public enum ToolbarContextKind
{
    Select,
    Balloon,
    TextEdit,
    PanelLayout,
    FloatingImage
}

public readonly record struct ToolbarStateSnapshot(
    ToolbarContextKind Context,
    bool HasDocument,
    int SelectedBalloonCount,
    int SelectedPanelCount,
    int SelectedFloatingImageCount,
    bool CanUndo,
    bool CanRedo,
    bool SnapEnabled)
{
    public bool HasBalloonSelection => SelectedBalloonCount > 0;
    public bool HasPanelSelection => SelectedPanelCount > 0;
    public bool HasFloatingImageSelection => SelectedFloatingImageCount > 0;
    public bool HasAnySelection => HasBalloonSelection || HasPanelSelection || HasFloatingImageSelection;
}

public static class ToolbarStateResolver
{
    public static ToolbarStateSnapshot Resolve(EditorState state)
    {
        var selectedBalloonCount = state.SelectedBalloonIds.Count;
        var selectedPanelCount = state.SelectedPanelIds.Count;
        var selectedFloatingImageCount = state.SelectedFloatingImageIds.Count;

        var context = ResolveContext(state.Mode, selectedBalloonCount, selectedFloatingImageCount);
        var canUndo = state.CommandDispatcher?.History.CanUndo ?? false;
        var canRedo = state.CommandDispatcher?.History.CanRedo ?? false;

        return new ToolbarStateSnapshot(
            context,
            state.Document != null,
            selectedBalloonCount,
            selectedPanelCount,
            selectedFloatingImageCount,
            canUndo,
            canRedo,
            state.SnapToGuides);
    }

    private static ToolbarContextKind ResolveContext(EditorMode mode, int selectedBalloonCount, int selectedFloatingImageCount)
    {
        if (mode == EditorMode.PanelLayout)
        {
            return ToolbarContextKind.PanelLayout;
        }

        if (mode == EditorMode.EditText)
        {
            return ToolbarContextKind.TextEdit;
        }

        if (selectedFloatingImageCount > 0)
        {
            return ToolbarContextKind.FloatingImage;
        }

        if (selectedBalloonCount > 0)
        {
            return ToolbarContextKind.Balloon;
        }

        return ToolbarContextKind.Select;
    }
}
