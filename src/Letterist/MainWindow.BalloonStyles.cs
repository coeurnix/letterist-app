using Letterist.Commands;
using Letterist.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using ModelColor = Letterist.Model.Color;

namespace Letterist;

public sealed partial class MainWindow : Window
{
    private Window? _balloonStyleEditorWindow;
    private StackPanel? _balloonStyleEditorListPanel;
    private Border? _balloonStyleEditorPreviewSwatch;
    private TextBlock? _balloonStyleEditorPreviewNameText;
    private TextBlock? _balloonStyleEditorPreviewMetaText;
    private CheckBox? _balloonStyleEditorPreviewQuickSelectCheckBox;
    private Button? _balloonStyleEditorApplyButton;
    private Button? _balloonStyleEditorCreateButton;
    private Button? _balloonStyleEditorRenameButton;
    private Button? _balloonStyleEditorDeleteButton;
    private Button? _balloonStyleEditorImportButton;
    private Button? _balloonStyleEditorExportButton;
    private Guid? _balloonStyleEditorSelectedStyleId;
    private bool _isUpdatingBalloonStyleEditor;
    private Guid? _balloonStyleEditorLastClickedStyleId;
    private DateTime _balloonStyleEditorLastClickUtc;
    private const double BalloonStyleEditorDoubleClickMs = 420;

    private void ShowBalloonStyleEditorWindow()
    {
        if (_balloonStyleEditorWindow != null)
        {
            RefreshBalloonStyleEditorWindow();
            _balloonStyleEditorWindow.Activate();
            return;
        }

        var root = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30))
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var actionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        _balloonStyleEditorApplyButton = CreateStyleEditorActionButton(L("style.button.apply"), BalloonStyleEditorApply_Click);
        _balloonStyleEditorCreateButton = CreateStyleEditorActionButton(L("style.button.new"), BalloonStyleEditorCreate_Click);
        _balloonStyleEditorRenameButton = CreateStyleEditorActionButton(L("style.button.rename"), BalloonStyleEditorRename_Click);
        _balloonStyleEditorDeleteButton = CreateStyleEditorActionButton(L("style.button.delete"), BalloonStyleEditorDelete_Click);
        _balloonStyleEditorImportButton = CreateStyleEditorActionButton(L("common.import"), BalloonStyleEditorImport_Click);
        _balloonStyleEditorExportButton = CreateStyleEditorActionButton(L("common.export"), BalloonStyleEditorExport_Click);
        var closeButton = CreateStyleEditorActionButton(L("common.close"), BalloonStyleEditorClose_Click);

        actionsPanel.Children.Add(_balloonStyleEditorApplyButton);
        actionsPanel.Children.Add(_balloonStyleEditorCreateButton);
        actionsPanel.Children.Add(_balloonStyleEditorRenameButton);
        actionsPanel.Children.Add(_balloonStyleEditorDeleteButton);
        actionsPanel.Children.Add(_balloonStyleEditorImportButton);
        actionsPanel.Children.Add(_balloonStyleEditorExportButton);
        actionsPanel.Children.Add(closeButton);

        var actionsBorder = new Border
        {
            Padding = new Thickness(10, 10, 10, 8),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 37, 37, 37)),
            Child = actionsPanel
        };
        Grid.SetRow(actionsBorder, 0);
        root.Children.Add(actionsBorder);

        _balloonStyleEditorPreviewSwatch = new Border
        {
            Width = 120,
            Height = 64,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
            Background = new SolidColorBrush(Microsoft.UI.Colors.White)
        };

        _balloonStyleEditorPreviewNameText = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        };

        _balloonStyleEditorPreviewMetaText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 175, 175, 175)),
            TextWrapping = TextWrapping.Wrap
        };

        _balloonStyleEditorPreviewQuickSelectCheckBox = new CheckBox
        {
            Content = L("style.quick_select"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
        };
        _balloonStyleEditorPreviewQuickSelectCheckBox.Click += BalloonStyleEditorPreviewQuickSelect_Click;

        var previewTextPanel = new StackPanel
        {
            Spacing = 4
        };
        previewTextPanel.Children.Add(_balloonStyleEditorPreviewNameText);
        previewTextPanel.Children.Add(_balloonStyleEditorPreviewMetaText);
        previewTextPanel.Children.Add(_balloonStyleEditorPreviewQuickSelectCheckBox);

        var previewGrid = new Grid
        {
            ColumnSpacing = 12
        };
        previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_balloonStyleEditorPreviewSwatch, 0);
        Grid.SetColumn(previewTextPanel, 1);
        previewGrid.Children.Add(_balloonStyleEditorPreviewSwatch);
        previewGrid.Children.Add(previewTextPanel);

        var previewBorder = new Border
        {
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 34, 34, 34)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 52, 52, 52)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = previewGrid
        };
        Grid.SetRow(previewBorder, 1);
        root.Children.Add(previewBorder);

        _balloonStyleEditorListPanel = new StackPanel
        {
            Spacing = 6,
            Padding = new Thickness(10)
        };

        var listScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _balloonStyleEditorListPanel
        };
        Grid.SetRow(listScroll, 2);
        root.Children.Add(listScroll);

        _balloonStyleEditorWindow = new Window
        {
            Title = $"{L("props.label.balloon_style")} {L("common.edit")}",
            Content = root
        };
        AttachEscapeToCloseWindow(_balloonStyleEditorWindow, root);
        var styleEditorAppWindow = _balloonStyleEditorWindow.AppWindow;
        styleEditorAppWindow.Resize(new SizeInt32(760, 700));
        CenterChildWindowOverMainWindow(styleEditorAppWindow);
        TryApplyFontChooserTitleBarTheme(styleEditorAppWindow);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            CenterChildWindowOverMainWindow(styleEditorAppWindow);
            TryApplyFontChooserTitleBarTheme(styleEditorAppWindow);
        });
        _balloonStyleEditorWindow.Closed += BalloonStyleEditorWindow_Closed;
        _balloonStyleEditorWindow.Activate();

        RefreshBalloonStyleEditorWindow();
    }

    private static Button CreateStyleEditorActionButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 5, 10, 5),
            MinWidth = 74,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 58, 58, 58))
        };
        button.Click += onClick;
        return button;
    }

    private void BalloonStyleEditorWindow_Closed(object sender, WindowEventArgs args)
    {
        _balloonStyleEditorWindow = null;
        _balloonStyleEditorListPanel = null;
        _balloonStyleEditorPreviewSwatch = null;
        _balloonStyleEditorPreviewNameText = null;
        _balloonStyleEditorPreviewMetaText = null;
        _balloonStyleEditorPreviewQuickSelectCheckBox = null;
        _balloonStyleEditorApplyButton = null;
        _balloonStyleEditorCreateButton = null;
        _balloonStyleEditorRenameButton = null;
        _balloonStyleEditorDeleteButton = null;
        _balloonStyleEditorImportButton = null;
        _balloonStyleEditorExportButton = null;
        _balloonStyleEditorLastClickedStyleId = null;
        _balloonStyleEditorLastClickUtc = default;
    }

    private void RefreshBalloonStyleEditorWindow()
    {
        if (_balloonStyleEditorWindow == null || _balloonStyleEditorListPanel == null)
        {
            return;
        }

        var doc = _editorState.Document;
        _balloonStyleEditorListPanel.Children.Clear();

        if (doc == null || doc.BalloonStyles.Count == 0)
        {
            _balloonStyleEditorSelectedStyleId = null;
            UpdateBalloonStyleEditorPreview(null);
            UpdateBalloonStyleEditorButtons();
            return;
        }

        if (_balloonStyleEditorSelectedStyleId.HasValue &&
            doc.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value) == null)
        {
            _balloonStyleEditorSelectedStyleId = null;
        }

        if (!_balloonStyleEditorSelectedStyleId.HasValue && _selectedBalloonStyleId.HasValue)
        {
            var selectedStyle = doc.FindBalloonStyle(_selectedBalloonStyleId.Value);
            if (selectedStyle != null)
            {
                _balloonStyleEditorSelectedStyleId = selectedStyle.Id;
            }
        }

        _balloonStyleEditorSelectedStyleId ??= doc.BalloonStyles.First().Id;

        foreach (var style in doc.BalloonStyles.OrderBy(style => style.Name, StringComparer.OrdinalIgnoreCase))
        {
            var row = BuildBalloonStyleEditorRow(style, _balloonStyleEditorSelectedStyleId == style.Id);
            _balloonStyleEditorListPanel.Children.Add(row);
        }

        UpdateBalloonStyleEditorPreview(doc.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value));
        UpdateBalloonStyleEditorButtons();
    }

    private FrameworkElement BuildBalloonStyleEditorRow(NamedBalloonStyle style, bool isSelected)
    {
        var row = new Grid
        {
            ColumnSpacing = 8
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var preview = new Border
        {
            Width = 44,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(Math.Max(1, style.Style.StrokeWidth * 0.35f)),
            BorderBrush = ToBrush(style.Style.StrokeColor),
            Background = ToBrush(style.Style.FillColor),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var infoPanel = new StackPanel
        {
            Spacing = 2
        };
        infoPanel.Children.Add(new TextBlock
        {
            Text = style.Name,
            FontSize = 13,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = BuildStyleSummary(style.Style),
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 165, 165, 165)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        infoPanel.Children.Add(new TextBlock
        {
            Text = BuildStyleIndicatorSummary(style, compact: true),
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 130, 130, 130)),
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = 410
        });

        var info = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        info.Children.Add(preview);
        info.Children.Add(infoPanel);

        var selectButton = new Button
        {
            Tag = style.Id,
            Content = info,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 5, 8, 5),
            Background = new SolidColorBrush(isSelected
                ? Microsoft.UI.ColorHelper.FromArgb(255, 56, 78, 108)
                : Microsoft.UI.ColorHelper.FromArgb(255, 42, 42, 42)),
            BorderBrush = new SolidColorBrush(isSelected
                ? Microsoft.UI.ColorHelper.FromArgb(255, 94, 145, 218)
                : Microsoft.UI.ColorHelper.FromArgb(255, 64, 64, 64)),
            BorderThickness = new Thickness(isSelected ? 2 : 1)
        };
        selectButton.Click += BalloonStyleEditorSelectStyle_Click;
        Grid.SetColumn(selectButton, 0);
        row.Children.Add(selectButton);

        var quickSelectCheckBox = new CheckBox
        {
            Tag = style.Id,
            Content = L("style.quick_select"),
            IsChecked = style.IsQuickSelect,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };
        quickSelectCheckBox.Click += BalloonStyleEditorQuickSelect_Click;
        Grid.SetColumn(quickSelectCheckBox, 1);
        row.Children.Add(quickSelectCheckBox);

        return row;
    }

    private void BalloonStyleEditorSelectStyle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid styleId)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var isDoubleClick = _balloonStyleEditorLastClickedStyleId == styleId
            && (nowUtc - _balloonStyleEditorLastClickUtc).TotalMilliseconds <= BalloonStyleEditorDoubleClickMs;
        _balloonStyleEditorLastClickedStyleId = styleId;
        _balloonStyleEditorLastClickUtc = nowUtc;

        SelectBalloonStyleInEditor(styleId);
        if (!isDoubleClick)
        {
            return;
        }

        var style = _editorState.Document?.FindBalloonStyle(styleId);
        if (style == null)
        {
            return;
        }

        TryApplyBalloonStyleToSelection(style, statusOnNoSelection: true);
        UpdatePropertiesPanel();
        UpdateBalloonStyleEditorButtons();
    }

    private void SelectBalloonStyleInEditor(Guid styleId)
    {
        _balloonStyleEditorSelectedStyleId = styleId;
        _selectedBalloonStyleId = styleId;
        SelectStylePreset(BalloonStylePresetComboBox, styleId);
        UpdateStylePresetButtons();
        RefreshBalloonStyleEditorWindow();
    }

    private void BalloonStyleEditorQuickSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingBalloonStyleEditor || sender is not CheckBox checkBox || checkBox.Tag is not Guid styleId)
        {
            return;
        }

        SetBalloonStyleQuickSelect(styleId, checkBox.IsChecked == true);
    }

    private void BalloonStyleEditorPreviewQuickSelect_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingBalloonStyleEditor || _balloonStyleEditorSelectedStyleId == null)
        {
            return;
        }

        SetBalloonStyleQuickSelect(
            _balloonStyleEditorSelectedStyleId.Value,
            _balloonStyleEditorPreviewQuickSelectCheckBox?.IsChecked == true);
    }

    private void SetBalloonStyleQuickSelect(Guid styleId, bool isQuickSelect)
    {
        var style = _editorState.Document?.FindBalloonStyle(styleId);
        if (style == null || style.IsQuickSelect == isQuickSelect)
        {
            return;
        }

        _editorState.Execute(new SetNamedBalloonStyleQuickSelectCommand(styleId, isQuickSelect));
    }

    private void UpdateBalloonStyleEditorPreview(NamedBalloonStyle? style)
    {
        if (_balloonStyleEditorPreviewSwatch == null ||
            _balloonStyleEditorPreviewNameText == null ||
            _balloonStyleEditorPreviewMetaText == null ||
            _balloonStyleEditorPreviewQuickSelectCheckBox == null)
        {
            return;
        }

        _isUpdatingBalloonStyleEditor = true;
        try
        {
            if (style == null)
            {
                _balloonStyleEditorPreviewSwatch.Background = ToBrush(ModelColor.White);
                _balloonStyleEditorPreviewSwatch.BorderBrush = ToBrush(ModelColor.Black);
                _balloonStyleEditorPreviewNameText.Text = L("style.none_selected");
                _balloonStyleEditorPreviewMetaText.Text = string.Empty;
                _balloonStyleEditorPreviewQuickSelectCheckBox.IsChecked = false;
                _balloonStyleEditorPreviewQuickSelectCheckBox.IsEnabled = false;
                return;
            }

            _balloonStyleEditorPreviewSwatch.Background = ToBrush(style.Style.FillColor);
            _balloonStyleEditorPreviewSwatch.BorderBrush = ToBrush(style.Style.StrokeColor);
            _balloonStyleEditorPreviewSwatch.BorderThickness = new Thickness(Math.Max(1, style.Style.StrokeWidth));

            _balloonStyleEditorPreviewNameText.Text = style.Name;
            _balloonStyleEditorPreviewMetaText.Text = $"{BuildStyleSummary(style.Style)}\n{BuildStyleIndicatorSummary(style, compact: false)}";
            _balloonStyleEditorPreviewQuickSelectCheckBox.IsChecked = style.IsQuickSelect;
            _balloonStyleEditorPreviewQuickSelectCheckBox.IsEnabled = true;
        }
        finally
        {
            _isUpdatingBalloonStyleEditor = false;
        }
    }

    private void UpdateBalloonStyleEditorButtons()
    {
        if (_balloonStyleEditorWindow == null)
        {
            return;
        }

        var doc = _editorState.Document;
        var hasDoc = doc != null;
        var selectedStyle = _balloonStyleEditorSelectedStyleId.HasValue
            ? doc?.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value)
            : null;
        var hasSelection = GetSelectedBalloons().Count > 0;

        if (_balloonStyleEditorApplyButton != null)
        {
            _balloonStyleEditorApplyButton.IsEnabled = selectedStyle != null && hasSelection;
        }

        if (_balloonStyleEditorCreateButton != null)
        {
            _balloonStyleEditorCreateButton.IsEnabled = doc?.SelectedBalloon != null;
        }

        if (_balloonStyleEditorRenameButton != null)
        {
            _balloonStyleEditorRenameButton.IsEnabled = selectedStyle != null;
        }

        if (_balloonStyleEditorDeleteButton != null)
        {
            _balloonStyleEditorDeleteButton.IsEnabled = selectedStyle != null && (doc?.BalloonStyles.Count ?? 0) > 1;
        }

        if (_balloonStyleEditorImportButton != null)
        {
            _balloonStyleEditorImportButton.IsEnabled = hasDoc;
        }

        if (_balloonStyleEditorExportButton != null)
        {
            _balloonStyleEditorExportButton.IsEnabled = (doc?.BalloonStyles.Count ?? 0) > 0;
        }
    }

    private void BalloonStyleEditorApply_Click(object sender, RoutedEventArgs e)
    {
        var style = _balloonStyleEditorSelectedStyleId.HasValue
            ? _editorState.Document?.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value)
            : null;
        if (style == null)
        {
            return;
        }

        TryApplyBalloonStyleToSelection(style, statusOnNoSelection: true);
        UpdateBalloonStyleEditorButtons();
    }

    private async void BalloonStyleEditorCreate_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        if (doc == null || balloon == null)
        {
            SetStatusMessage(L("style.error.select_balloon_create"));
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(balloon.Text) ? L("props.label.balloon_style") : balloon.Text.Trim();
        if (suggestedName.Length > 32)
        {
            suggestedName = $"{suggestedName[..32].TrimEnd()}...";
        }

        var name = await PromptStyleNameAsync(L("style.dialog.new_balloon_style"), L("props.label.name"), suggestedName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existingNames = new HashSet<string>(doc.BalloonStyles.Select(style => style.Name), StringComparer.OrdinalIgnoreCase);
        var uniqueName = GetUniqueStyleName(name, existingNames, L("props.label.balloon_style"));
        var cmd = new CreateNamedBalloonStyleCommand(
            uniqueName,
            balloon.BalloonStyle,
            isQuickSelect: true,
            applyExtendedDetails: true,
            shape: balloon.Shape,
            customShapePathData: balloon.CustomShapePathData,
            constrainToPanel: balloon.ConstrainToPanel,
            textStyle: balloon.TextStyle,
            textPath: balloon.TextPath?.Clone(),
            tails: BuildNamedStyleTailSnapshots(balloon));
        _editorState.Execute(cmd);
        _selectedBalloonStyleId = cmd.CreatedStyleId;
        _balloonStyleEditorSelectedStyleId = cmd.CreatedStyleId;
        RefreshStylePresets();
    }

    private async void BalloonStyleEditorRename_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var style = _balloonStyleEditorSelectedStyleId.HasValue
            ? doc?.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value)
            : null;
        if (doc == null || style == null)
        {
            return;
        }

        var name = await PromptStyleNameAsync(L("style.dialog.rename_balloon_style"), L("props.label.name"), style.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existingNames = new HashSet<string>(
            doc.BalloonStyles.Where(item => item.Id != style.Id).Select(item => item.Name),
            StringComparer.OrdinalIgnoreCase);
        var uniqueName = GetUniqueStyleName(name, existingNames, L("props.label.balloon_style"));

        if (!string.Equals(uniqueName, style.Name, StringComparison.Ordinal))
        {
            _editorState.Execute(new RenameNamedBalloonStyleCommand(style.Id, uniqueName));
        }
    }

    private async void BalloonStyleEditorDelete_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var style = _balloonStyleEditorSelectedStyleId.HasValue
            ? doc?.FindBalloonStyle(_balloonStyleEditorSelectedStyleId.Value)
            : null;
        if (doc == null || style == null)
        {
            return;
        }

        if (doc.BalloonStyles.Count <= 1)
        {
            SetStatusMessage(L("style.error.minimum_required"));
            return;
        }

        if (!await ConfirmStyleDeleteAsync(style.Name, "balloon"))
        {
            return;
        }

        _editorState.Execute(new DeleteNamedBalloonStyleCommand(style.Id));
        _balloonStyleEditorSelectedStyleId = doc.BalloonStyles.FirstOrDefault(item => item.Id != style.Id)?.Id;
        _selectedBalloonStyleId = _balloonStyleEditorSelectedStyleId;
        RefreshStylePresets();
    }

    private async void BalloonStyleEditorImport_Click(object sender, RoutedEventArgs e)
    {
        await ImportStyleLibraryAsync();
    }

    private async void BalloonStyleEditorExport_Click(object sender, RoutedEventArgs e)
    {
        await ExportStyleLibraryAsync();
    }

    private void BalloonStyleEditorClose_Click(object sender, RoutedEventArgs e)
    {
        _balloonStyleEditorWindow?.Close();
    }

    private static Brush ToBrush(ModelColor color)
    {
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
    }

    private static string BuildStyleSummary(BalloonStyle style)
    {
        return $"Fill {ToStyleHex(style.FillColor)} | Stroke {style.StrokeWidth:F1}px {ToStyleHex(style.StrokeColor)}";
    }

    private static string BuildStyleIndicatorSummary(NamedBalloonStyle style, bool compact)
    {
        var parts = new List<string>
        {
            $"Shape={style.Shape}"
        };

        if (style.Shape == BalloonShape.Custom && !string.IsNullOrWhiteSpace(style.CustomShapePathData))
        {
            parts.Add("CustomSVG=Yes");
        }

        if (style.Tails.Count == 0)
        {
            parts.Add("Tails=None");
        }
        else
        {
            var tailStyles = string.Join("/", style.Tails.Select(tail => tail.Style).Distinct().OrderBy(x => x));
            parts.Add($"Tails={style.Tails.Count}({tailStyles})");
        }

        parts.Add(style.TextPath != null ? "TextPath=On" : "TextPath=Off");
        parts.Add(style.ConstrainToPanel ? "Constrain=On" : "Constrain=Off");

        return compact
            ? string.Join(" | ", parts)
            : string.Join("   ", parts);
    }

    private static string ToStyleHex(ModelColor color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
