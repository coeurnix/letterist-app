using Letterist.Commands;
using Letterist.Model;
using Letterist.View;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Globalization;

namespace Letterist;

public sealed partial class MainWindow : Window
{
    private ToggleButton? ToolbarSmartPunctuationToggle => null;

    private void UpdateToolbarUi(ToolbarStateSnapshot state)
    {
        UpdatePersistentToolbar(state);
        UpdateContextualToolbar(state);
    }

    private void UpdatePersistentToolbar(ToolbarStateSnapshot state)
    {
        SetToolbarButtonState(SaveToolbarButton, state.HasDocument, "Save (Ctrl+S)", "Create or open a document first.");
        SetToolbarButtonState(ExportToolbarButton, state.HasDocument, "Export (Ctrl+Shift+E)", "Create or open a document first.");

        SetToolbarButtonState(UndoButton, state.CanUndo, "Undo (Ctrl+Z)", "Nothing to undo.");
        SetToolbarButtonState(RedoButton, state.CanRedo, "Redo (Ctrl+Y)", "Nothing to redo.");

        SetToolbarButtonState(CutButton, state.HasBalloonSelection, "Cut (Ctrl+X)", "Select one or more balloons.");
        SetToolbarButtonState(CopyButton, state.HasBalloonSelection, "Copy (Ctrl+C)", "Select one or more balloons.");
        SetToolbarButtonState(PasteButton, state.HasDocument, "Paste (Ctrl+V)", "Create or open a document first.");
        SetToolbarButtonState(DeleteToolbarButton, state.HasAnySelection, "Delete (Delete)", "Select something to delete.");

        SetToolbarButtonState(ZoomOutToolbarButton, state.HasDocument, "Zoom Out (Ctrl+-)", "Create or open a document first.");
        SetToolbarButtonState(ZoomInToolbarButton, state.HasDocument, "Zoom In (Ctrl++)", "Create or open a document first.");
        SetToolbarButtonState(ZoomFitToolbarButton, state.HasDocument, "Zoom to Fit (Ctrl+0)", "Create or open a document first.");

        if (SnapToolbarToggle != null)
        {
            SnapToolbarToggle.IsEnabled = state.HasDocument;
            SnapToolbarToggle.IsChecked = state.SnapEnabled;
            ToolTipService.SetToolTip(
                SnapToolbarToggle,
                state.HasDocument
                    ? "Snap to Guides"
                    : "Snap to Guides\nUnavailable: Create or open a document first.");
        }

    }

    private void UpdateContextualToolbar(ToolbarStateSnapshot state)
    {
    }

    private static string GetContextLabel(ToolbarContextKind context)
    {
        return context switch
        {
            ToolbarContextKind.Balloon => "Balloon",
            ToolbarContextKind.TextEdit => "Text Edit",
            ToolbarContextKind.PanelLayout => "Panel Layout",
            ToolbarContextKind.FloatingImage => "Floating Image",
            _ => "Select"
        };
    }

    private static void SetVisibility(FrameworkElement? element, bool visible)
    {
        if (element == null) return;
        element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetToolbarButtonState(ButtonBase? button, bool enabled, string tooltip, string disabledReason)
    {
        if (button == null) return;
        button.IsEnabled = enabled;
        if (enabled || string.IsNullOrWhiteSpace(disabledReason))
        {
            ToolTipService.SetToolTip(button, tooltip);
            return;
        }

        ToolTipService.SetToolTip(button, $"{tooltip}\nUnavailable: {disabledReason}");
    }

    private FloatingImage? GetSelectedFloatingImage()
    {
        var page = _editorState.Document?.ActivePage;
        var imageId = _editorState.SelectedFloatingImageId;
        if (page == null || !imageId.HasValue) return null;
        return page.FindFloatingImage(imageId.Value);
    }

    private Balloon? GetPrimarySelectedBalloon()
    {
        var doc = _editorState.Document;
        if (doc == null) return null;

        if (doc.SelectedBalloonId.HasValue)
        {
            return doc.FindBalloon(doc.SelectedBalloonId.Value);
        }

        var firstSelected = _editorState.SelectedBalloonIds.FirstOrDefault();
        return firstSelected == Guid.Empty ? null : doc.FindBalloon(firstSelected);
    }

    private static string GetCurrentTailStyleLabel(Balloon? balloon)
    {
        if (balloon?.Tail == null) return "None";

        return balloon.Tail.Style switch
        {
            TailStyle.Pointer => "Pointer",
            TailStyle.Curved => "Curved",
            TailStyle.ThoughtBubbles => "Bubbles",
            TailStyle.Squiggly => "Squiggly",
            TailStyle.None => "Hidden",
            _ => "Pointer"
        };
    }

    private void ToolbarDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelIds.Count > 0)
        {
            DeleteSelectedPanel();
        }
        else if (_editorState.SelectedFloatingImageId.HasValue)
        {
            DeleteSelectedFloatingImage();
        }
        else
        {
            DeleteSelectedBalloon();
        }

        UpdateToolButtonStates();
    }

    private void ToolbarSnapToggle_Click(object sender, RoutedEventArgs e)
    {
        _editorState.SnapToGuides = SnapToolbarToggle.IsChecked == true;
        if (SnapToGuidesToggle != null)
        {
            SnapToGuidesToggle.IsOn = _editorState.SnapToGuides;
        }
        if (SnapToGuidesMenuItem != null)
        {
            SnapToGuidesMenuItem.IsChecked = _editorState.SnapToGuides;
        }

        UpdateToolButtonStates();
        MainCanvas.Invalidate();
    }

    private void ToolbarBalloonShapePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string shapeText ||
            !Enum.TryParse<BalloonShape>(shapeText, out var shape))
        {
            return;
        }

        var doc = _editorState.Document;
        if (doc == null) return;

        var selectedIds = _editorState.SelectedBalloonIds.ToList();
        if (selectedIds.Count == 0 && doc.SelectedBalloonId.HasValue)
        {
            selectedIds.Add(doc.SelectedBalloonId.Value);
        }

        if (selectedIds.Count == 0) return;

        var commands = new List<ICommand>();
        foreach (var id in selectedIds)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon == null || balloon.Shape == shape) continue;
            commands.Add(new SetBalloonShapeCommand(id, shape));
        }

        if (commands.Count == 0) return;
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Set balloon shape", commands);
        }
    }

    private void ToolbarCycleFitMode_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var primaryId = doc.SelectedBalloonId ?? _editorState.SelectedBalloonIds.FirstOrDefault();
        if (primaryId == Guid.Empty) return;

        var primary = doc.FindBalloon(primaryId);
        if (primary == null) return;

        var next = primary.TextStyle.FitMode switch
        {
            TextFitMode.GrowBalloon => TextFitMode.None,
            TextFitMode.None => TextFitMode.ShrinkToFit,
            _ => TextFitMode.GrowBalloon
        };

        var commands = new List<ICommand>();
        var selectedIds = _editorState.SelectedBalloonIds.ToList();
        if (selectedIds.Count == 0) selectedIds.Add(primaryId);
        foreach (var id in selectedIds)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon == null || balloon.TextStyle.FitMode == next) continue;
            commands.Add(new SetTextStyleCommand(id, balloon.TextStyle.With(fitMode: next)));
        }

        if (commands.Count == 0) return;
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Set fit mode", commands);
        }
    }

    private string GetCurrentFitModeLabel()
    {
        var doc = _editorState.Document;
        var primaryId = doc?.SelectedBalloonId;
        if (!primaryId.HasValue) return "Auto";

        var balloon = doc?.FindBalloon(primaryId.Value);
        if (balloon == null) return "Auto";

        return balloon.TextStyle.FitMode switch
        {
            TextFitMode.GrowBalloon => "Auto",
            TextFitMode.None => "Manual",
            TextFitMode.ShrinkToFit => "Shrink",
            TextFitMode.TrackToFit => "Track",
            _ => "Auto"
        };
    }

    private static string GetFitModeGlyph(string fitModeLabel)
    {
        return fitModeLabel switch
        {
            "Manual" => "\uE70F",
            "Shrink" => "\uE71F",
            "Track" => "\uE8D2",
            _ => "\uE8FB"
        };
    }

    private void ToolbarTextAlignPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string alignTag) return;

        var alignment = alignTag switch
        {
            "Left" => Model.TextAlignment.Left,
            "Right" => Model.TextAlignment.Right,
            _ => Model.TextAlignment.Center
        };

        ApplyInlineTextStyle(style => style.With(alignment: alignment));
    }

    private void ToolbarSmartPunctuationToggle_Click(object sender, RoutedEventArgs e)
    {
        _editorState.EnableSmartPunctuation = ToolbarSmartPunctuationToggle?.IsChecked == true;
        MainCanvas.Invalidate();
    }

    private void ToolbarTextSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode != EditorMode.EditText) return;
        _editorState.SelectAll();
        MainCanvas.Invalidate();
    }

    private void ToolbarAdjustFontSize_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string deltaText ||
            !float.TryParse(deltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        var balloon = GetPrimarySelectedBalloon();
        if (balloon == null) return;

        var current = GetActiveTextStyleForProperties(balloon);
        var next = Math.Clamp(current.FontSize + delta, 6f, 96f);
        if (MathF.Abs(next - current.FontSize) < 0.01f) return;

        ApplyInlineTextStyle(style => style.With(fontSize: next));
        UpdateToolButtonStates();
    }

    private void ToolbarSetFontSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not string sizeText ||
            !float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            return;
        }

        var clampedSize = Math.Clamp(size, 6f, 96f);
        ApplyInlineTextStyle(style => style.With(fontSize: clampedSize));
        UpdateToolButtonStates();
    }

    private void ToolbarSetFontPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not string fontName || string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        ApplyInlineTextStyle(style => style.With(fontFamily: fontName.Trim()));
        UpdateToolButtonStates();
    }

    private void ToolbarTextColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string hex) return;
        ApplyInlineTextStyle(style => style.With(textColor: ParseHexColor(hex)));
    }

    private async void ToolbarTextColorCustom_Click(object sender, RoutedEventArgs e)
    {
        var balloon = GetPrimarySelectedBalloon();
        if (balloon == null) return;

        var activeStyle = GetActiveTextStyleForProperties(balloon);
        var customColor = await ShowColorPickerDialogAsync(activeStyle.TextColor);
        if (!customColor.HasValue) return;

        ApplyInlineTextStyle(style => style.With(textColor: customColor.Value));
    }

    private void ToolbarBalloonFillSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string hex) return;

        var fillColor = ParseHexColor(hex);
        var selected = GetSelectedBalloons();
        if (selected.Count == 0) return;

        var commands = selected
            .Where(balloon => !balloon.BalloonStyle.FillColor.Equals(fillColor))
            .Select(balloon => (ICommand)new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(fillColor: fillColor)))
            .ToList();

        ExecuteToolbarCommandBatch("Set balloon fill", commands);
    }

    private void ToolbarAdjustBalloonStrokeWidth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string deltaText ||
            !float.TryParse(deltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        var selected = GetSelectedBalloons();
        if (selected.Count == 0) return;

        var commands = selected
            .Select(balloon =>
            {
                var next = Math.Clamp(balloon.BalloonStyle.StrokeWidth + delta, 0.5f, 24f);
                return MathF.Abs(next - balloon.BalloonStyle.StrokeWidth) < 0.01f
                    ? null
                    : (ICommand)new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(strokeWidth: next));
            })
            .Where(cmd => cmd != null)
            .Cast<ICommand>()
            .ToList();

        ExecuteToolbarCommandBatch("Adjust balloon stroke", commands);
    }

    private void ToolbarToggleTailForSelection_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedBalloons();
        if (selected.Count == 0) return;

        var commands = new List<ICommand>();
        foreach (var balloon in selected)
        {
            if (balloon.Tail != null)
            {
                commands.Add(new DeleteTailCommand(balloon.Id));
            }
            else
            {
                var tailTarget = new Point2(balloon.Position.X, balloon.Position.Y + balloon.Bounds.Height + 50f);
                commands.Add(new CreateTailCommand(balloon.Id, tailTarget));
            }
        }

        ExecuteToolbarCommandBatch("Toggle tails", commands);
    }

    private void ToolbarCycleTailStyle_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedBalloons();
        if (selected.Count == 0) return;

        var commands = new List<ICommand>();
        foreach (var balloon in selected)
        {
            if (balloon.Tail == null)
            {
                var tailTarget = new Point2(balloon.Position.X, balloon.Position.Y + balloon.Bounds.Height + 50f);
                commands.Add(new CreateTailCommand(balloon.Id, tailTarget));
                continue;
            }

            var nextStyle = balloon.Tail.Style switch
            {
                TailStyle.Pointer => TailStyle.Curved,
                TailStyle.Curved => TailStyle.ThoughtBubbles,
                TailStyle.ThoughtBubbles => TailStyle.Squiggly,
                TailStyle.Squiggly => TailStyle.None,
                TailStyle.None => TailStyle.Pointer,
                _ => TailStyle.Pointer
            };

            commands.Add(new SetTailStyleCommand(balloon.Id, nextStyle, balloon.Tail.Id));
        }

        ExecuteToolbarCommandBatch("Cycle tail style", commands);
    }

    private void ToolbarAdjustTailWidth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string deltaText ||
            !float.TryParse(deltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var delta))
        {
            return;
        }

        var selected = GetSelectedBalloons();
        if (selected.Count == 0) return;

        var commands = new List<ICommand>();
        foreach (var balloon in selected)
        {
            if (balloon.Tail == null) continue;

            var nextWidth = Math.Clamp(balloon.Tail.BaseWidth + delta, 6f, 80f);
            if (MathF.Abs(nextWidth - balloon.Tail.BaseWidth) < 0.01f) continue;
            commands.Add(new SetTailWidthCommand(balloon.Id, nextWidth, balloon.Tail.Id));
        }

        ExecuteToolbarCommandBatch("Adjust tail width", commands);
    }

    private void ExecuteToolbarCommandBatch(string description, List<ICommand> commands)
    {
        if (commands.Count == 0) return;
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction(description, commands);
        }

        UpdatePropertiesPanel();
        UpdateToolButtonStates();
        MainCanvas.Invalidate();
    }

    private async void ToolbarSplitPanel_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        await ShowSplitPanelDialogAsync(panel);
    }

    private async void ToolbarMergePanel_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        await ShowMergePanelDialogAsync(panel);
    }

    private void ToolbarChangePanelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string deltaText || !int.TryParse(deltaText, out var delta))
        {
            return;
        }

        var panelId = _editorState.SelectedPanelId;
        if (!panelId.HasValue) return;

        ChangePanelOrder(panelId.Value, delta);
    }

    private void ToolbarToggleFloatingImageVisibility_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        var image = GetSelectedFloatingImage();
        if (page == null || image == null) return;

        _editorState.Execute(new SetFloatingImageVisibilityCommand(page.Id, image.Id, !image.IsVisible));
    }

    private void ToolbarToggleFloatingImageLock_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        var image = GetSelectedFloatingImage();
        if (page == null || image == null) return;

        _editorState.Execute(new SetFloatingImageLockedCommand(page.Id, image.Id, !image.IsLocked));
    }

    private void ToolbarDeleteFloatingImage_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedFloatingImage();
        UpdateToolButtonStates();
    }

}
