using Letterist.Model;
using DocumentPage = Letterist.Model.Page;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Letterist;

public partial class MainWindow
{
    internal enum SidebarTabHeaderVisualMode
    {
        TextAndIcon,
        TextOnly,
        IconOnly
    }

    internal enum PropertiesTabHeaderVisualMode
    {
        TextAndIcon,
        TextOnly,
        IconOnly
    }

    private string _activeLayersSidebarTab = "Layers";
    private string _activePropertiesTab = "Text";
    private SidebarTabHeaderVisualMode _leftSidebarTabHeaderMode = SidebarTabHeaderVisualMode.TextAndIcon;
    private PropertiesTabHeaderVisualMode _propertiesTabHeaderMode = PropertiesTabHeaderVisualMode.TextAndIcon;
    private const string PanelBalloonDragFormat = "letterist/panel-balloon-ids";
    private const string PanelFloatingImageDragFormat = "letterist/panel-floating-image-ids";

    private ToggleButton? PageSetupTabButton => null;
    private FrameworkElement? PageSetupTabContent => null;
    private TextBlock? PageBackgroundImageLabel => PagePropertiesBackgroundImageLabel;
    private TextBlock? BackgroundImageStatus => PagePropertiesBackgroundImageStatus;
    private Button? PageLoadImageButton => PagePropertiesLoadImageButton;
    private Button? PageClearImageButton => PagePropertiesClearImageButton;
    private TextBlock? PageBackgroundImageFitModeLabel => PagePropertiesBackgroundImageFitModeLabel;
    private ComboBox? PageBackgroundImageFitModeComboBox => PagePropertiesBackgroundImageFitModeComboBox;
    private TextBlock? PageCanvasSizeLabel => PagePropertiesCanvasSizeLabel;
    private NumberBox? PageWidthBox => PagePropertiesWidthBox;
    private NumberBox? PageHeightBox => PagePropertiesHeightBox;
    private ComboBox? PageSizePresetComboBox => PagePropertiesPresetComboBox;
    private TextBlock? PageDpiLabel => PagePropertiesDpiLabel;
    private NumberBox? PageDpiBox => PagePropertiesDpiBox;
    private TextBlock? PageDpiHintText => PagePropertiesDpiHintText;
    private TextBlock? PageBackgroundColorLabel => PagePropertiesBackgroundColorLabel;
    private Border? PageBackgroundColorPreview => PagePropertiesBackgroundColorPreview;
    private ComboBox? PageBackgroundColorComboBox => PagePropertiesBackgroundColorComboBox;


    private string _activeLeftSidebarTab = "Layers";

    private void LeftSidebarPagesTab_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Pages");
    }

    private void LeftSidebarLayersTab_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Layers");
    }

    private void LeftSidebarPanelsTab_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Panels");
    }

    private void LeftSidebarGuidesTab_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Guides");
    }

    private void LeftSidebarTabsHeaderBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLeftSidebarTabHeaderMode();
    }

    internal static SidebarTabHeaderVisualMode ChooseLeftSidebarTabHeaderVisualMode(
        double availableWidth,
        double textAndIconWidth,
        double textOnlyWidth,
        double iconOnlyWidth)
    {
        if (availableWidth >= textAndIconWidth)
        {
            return SidebarTabHeaderVisualMode.TextAndIcon;
        }

        if (availableWidth >= textOnlyWidth)
        {
            return SidebarTabHeaderVisualMode.TextOnly;
        }

        return SidebarTabHeaderVisualMode.IconOnly;
    }

    private void UpdateLeftSidebarTabHeaderMode()
    {
        if (LeftSidebarTabsHeaderBorder == null || LeftSidebarTabsHeaderStackPanel == null)
        {
            return;
        }

        var availableWidth = LeftSidebarTabsHeaderBorder.ActualWidth
            - LeftSidebarTabsHeaderBorder.Padding.Left
            - LeftSidebarTabsHeaderBorder.Padding.Right;
        if (availableWidth <= 0)
        {
            return;
        }

        var previousMode = _leftSidebarTabHeaderMode;
        var textAndIconWidth = MeasureLeftSidebarTabHeaderWidth(SidebarTabHeaderVisualMode.TextAndIcon);
        var textOnlyWidth = MeasureLeftSidebarTabHeaderWidth(SidebarTabHeaderVisualMode.TextOnly);
        var iconOnlyWidth = MeasureLeftSidebarTabHeaderWidth(SidebarTabHeaderVisualMode.IconOnly);

        var chosenMode = ChooseLeftSidebarTabHeaderVisualMode(availableWidth, textAndIconWidth, textOnlyWidth, iconOnlyWidth);

        if (chosenMode != previousMode)
        {
            ApplyLeftSidebarTabHeaderMode(chosenMode, updateStoredMode: true);
            return;
        }

        ApplyLeftSidebarTabHeaderMode(chosenMode, updateStoredMode: false);
    }

    private double MeasureLeftSidebarTabHeaderWidth(SidebarTabHeaderVisualMode mode)
    {
        ApplyLeftSidebarTabHeaderMode(mode, updateStoredMode: false);

        var buttons = GetLeftSidebarTabButtons();
        var width = 0.0;
        foreach (var button in buttons)
        {
            button.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            width += button.DesiredSize.Width;
        }

        if (buttons.Count > 1)
        {
            width += (buttons.Count - 1) * LeftSidebarTabsHeaderStackPanel.Spacing;
        }

        return width;
    }

    private IReadOnlyList<ToggleButton> GetLeftSidebarTabButtons()
    {
        return
        [
            LeftSidebarPagesTabButton,
            LeftSidebarLayersTabButton,
            LeftSidebarPanelsTabButton,
            LeftSidebarGuidesTabButton
        ];
    }

    private void ApplyLeftSidebarTabHeaderMode(SidebarTabHeaderVisualMode mode, bool updateStoredMode)
    {
        var showIcons = mode != SidebarTabHeaderVisualMode.TextOnly;
        var showText = mode != SidebarTabHeaderVisualMode.IconOnly;

        var iconVisibility = showIcons ? Visibility.Visible : Visibility.Collapsed;
        var textVisibility = showText ? Visibility.Visible : Visibility.Collapsed;

        LeftSidebarPagesTabIcon.Visibility = iconVisibility;
        LeftSidebarLayersTabIcon.Visibility = iconVisibility;
        LeftSidebarPanelsTabIcon.Visibility = iconVisibility;
        LeftSidebarGuidesTabIcon.Visibility = iconVisibility;

        LeftSidebarPagesTabLabel.Visibility = textVisibility;
        LeftSidebarLayersTabLabel.Visibility = textVisibility;
        LeftSidebarPanelsTabLabel.Visibility = textVisibility;
        LeftSidebarGuidesTabLabel.Visibility = textVisibility;

        if (updateStoredMode)
        {
            _leftSidebarTabHeaderMode = mode;
        }
    }

    private void SwitchLeftSidebarTab(string tabName)
    {
        _activeLeftSidebarTab = tabName;

        LeftSidebarPagesTabButton.IsChecked = tabName == "Pages";
        LeftSidebarLayersTabButton.IsChecked = tabName == "Layers";
        LeftSidebarPanelsTabButton.IsChecked = tabName == "Panels";
        LeftSidebarGuidesTabButton.IsChecked = tabName == "Guides";

        LeftSidebarPagesContent.Visibility = tabName == "Pages" ? Visibility.Visible : Visibility.Collapsed;
        LayersPanelsGrid.Visibility = (tabName == "Layers" || tabName == "Panels") ? Visibility.Visible : Visibility.Collapsed;
        LeftSidebarGuidesContent.Visibility = tabName == "Guides" ? Visibility.Visible : Visibility.Collapsed;

        if (tabName == "Layers" || tabName == "Panels")
        {
            SwitchLayersSidebarTab(tabName);
        }

        if (tabName == "Guides")
        {
            UpdateGuideList();
        }

        UpdatePropertiesPanel();
    }



    private void LayersTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Layers");
    }

    private void PanelsTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchLeftSidebarTab("Panels");
    }

    private void PageSetupTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private ToggleButton? TranslationTabButton => null;

    private void TranslationTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private ToggleButton? TemplatesTabButton => null;

    private void TemplatesTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void SwitchLayersSidebarTab(string tabName)
    {
        _activeLayersSidebarTab = tabName;

        LayersTabButton.IsChecked = tabName == "Layers";
        PanelsTabButton.IsChecked = tabName == "Panels";
        if (TemplatesTabButton != null) TemplatesTabButton.IsChecked = tabName == "Templates";
        if (PageSetupTabButton != null) PageSetupTabButton.IsChecked = tabName == "PageSetup";
        if (TranslationTabButton != null) TranslationTabButton.IsChecked = tabName == "Translation";

        LayersTabContent.Visibility = tabName == "Layers" ? Visibility.Visible : Visibility.Collapsed;
        PanelsTabContent.Visibility = tabName == "Panels" ? Visibility.Visible : Visibility.Collapsed;
        if (PageSetupTabContent != null) PageSetupTabContent.Visibility = tabName == "PageSetup" ? Visibility.Visible : Visibility.Collapsed;

        LayersTabActions.Visibility = tabName == "Layers" ? Visibility.Visible : Visibility.Collapsed;
        PanelsTabActions.Visibility = tabName == "Panels" ? Visibility.Visible : Visibility.Collapsed;
        PageSetupTabActions.Visibility = tabName == "PageSetup" ? Visibility.Visible : Visibility.Collapsed;

        if (tabName == "Panels")
        {
            RefreshPanelList();
        }

        if (tabName == "PageSetup")
        {
            RefreshPageSetup();
        }

    }



    private void RefreshPanelList()
    {
        if (_isUpdatingPanelList) return;
        _isUpdatingPanelList = true;
        try
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null)
            {
                PanelListView.ItemsSource = null;
                return;
            }

            var items = new List<LayerListItemViewModel>();
            var selectedBalloonIds = new HashSet<Guid>(_editorState.SelectedBalloonIds);
            var selectedPanelIds = new HashSet<Guid>(_editorState.SelectedPanelIds);
            var selectedFloatingImageIds = new HashSet<Guid>(_editorState.SelectedFloatingImageIds);

            foreach (var panel in page.Panels.OrderBy(p => p.Order))
            {
                var balloons = new List<(Balloon Balloon, Guid LayerId)>();
                foreach (var layer in page.Layers)
                {
                    if (layer.Kind != Model.LayerKind.Balloon) continue;
                    foreach (var balloon in layer.Balloons.Where(b => b.PanelId == panel.Id))
                    {
                        balloons.Add((balloon, layer.Id));
                    }
                }
                var images = page.FloatingImages.Where(image => image.PanelId == panel.Id).ToList();
                var objectCount = balloons.Count + images.Count;

                var isPanelExpanded = objectCount > 0;

                items.Add(new PanelZoneViewModel
                {
                    Id = panel.Id,
                    Name = $"{panel.Name} ({objectCount})",
                    IsVisible = panel.IsVisible,
                    IsLocked = panel.IsLocked,
                    IsActive = selectedPanelIds.Contains(panel.Id),
                    BalloonCount = objectCount,
                    IsExpanded = isPanelExpanded
                });

                if (objectCount > 0)
                {
                    var index = 0;
                    foreach (var entry in balloons)
                    {
                        index++;
                        items.Add(new PanelBalloonViewModel
                        {
                            Id = entry.Balloon.Id,
                            LayerId = entry.LayerId,
                            PanelId = panel.Id,
                            Name = GetBalloonListName(entry.Balloon, index),
                            HoverToolTip = GetBalloonHoverText(entry.Balloon),
                            IsVisible = panel.IsVisible,
                            IsLocked = panel.IsLocked,
                            IsActive = selectedBalloonIds.Contains(entry.Balloon.Id)
                        });
                    }

                    var imageIndex = 0;
                    foreach (var image in images)
                    {
                        imageIndex++;
                        items.Add(new PanelFloatingImageViewModel
                        {
                            Id = image.Id,
                            PanelId = panel.Id,
                            LayerId = image.LayerId ?? Guid.Empty,
                            Name = GetFloatingImageListName(image, imageIndex),
                            ImagePath = image.ImagePath,
                            IsVisible = image.IsVisible,
                            IsLocked = image.IsLocked,
                            IsActive = selectedFloatingImageIds.Contains(image.Id)
                        });
                    }
                }
            }

            PanelListView.ItemsSource = items;

            foreach (var item in items)
            {
                if (item is PanelZoneViewModel panelVm && selectedPanelIds.Contains(panelVm.Id))
                {
                    PanelListView.SelectedItems.Add(panelVm);
                }
                else if (item is PanelBalloonViewModel balloonVm && selectedBalloonIds.Contains(balloonVm.Id))
                {
                    PanelListView.SelectedItems.Add(balloonVm);
                }
                else if (item is PanelFloatingImageViewModel imageVm && selectedFloatingImageIds.Contains(imageVm.Id))
                {
                    PanelListView.SelectedItems.Add(imageVm);
                }
            }
        }
        finally
        {
            _isUpdatingPanelList = false;
        }
    }

    private void PanelListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelList) return;

        var selectedItems = PanelListView.SelectedItems.Cast<LayerListItemViewModel>().ToList();
        var selectedPanels = selectedItems.OfType<PanelZoneViewModel>().ToList();
        var selectedBalloons = selectedItems.OfType<PanelBalloonViewModel>().ToList();
        var selectedImages = selectedItems.OfType<PanelFloatingImageViewModel>().ToList();

        if (selectedBalloons.Count > 0 || selectedImages.Count > 0)
        {
            if (_editorState.Mode == View.EditorMode.PanelLayout)
            {
                _ = SetPanelLayoutModeAsync(false);
            }

            var balloonIds = selectedBalloons.Select(balloon => balloon.Id).ToList();
            var imageIds = selectedImages.Select(image => image.Id).ToList();

            if (balloonIds.Count > 0)
            {
                var primaryBalloon = _editorState.Document?.SelectedBalloonId;
                if (!primaryBalloon.HasValue || !balloonIds.Contains(primaryBalloon.Value))
                {
                    primaryBalloon = balloonIds[^1];
                }
                _editorState.SetSelection(balloonIds, primaryBalloon, preserveFloatingImageSelection: imageIds.Count > 0);
            }
            else
            {
                _editorState.SelectBalloon(null, preserveFloatingImageSelection: imageIds.Count > 0);
            }

            if (imageIds.Count > 0)
            {
                var primaryImage = _editorState.SelectedFloatingImageId;
                if (!primaryImage.HasValue || !imageIds.Contains(primaryImage.Value))
                {
                    primaryImage = imageIds[^1];
                }
                _editorState.SetFloatingImageSelection(imageIds, primaryImage, preserveBalloonSelection: balloonIds.Count > 0);
            }
            else
            {
                _editorState.SelectFloatingImage(null, preserveBalloonSelection: balloonIds.Count > 0);
            }

            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            return;
        }

        if (selectedPanels.Count > 0)
        {
            var panelIds = selectedPanels.Select(panel => panel.Id).ToList();
            var primary = _editorState.SelectedPanelId;
            if (!primary.HasValue || !panelIds.Contains(primary.Value))
            {
                primary = panelIds[^1];
            }
            _editorState.SetPanelSelection(panelIds, primary);
            _editorState.SelectBalloon(null);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            return;
        }

        _editorState.SelectBalloon(null);
        _editorState.SelectFloatingImage(null, preserveBalloonSelection: true);
        _editorState.ClearPanelSelection();
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
    }

    private void PanelListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_isUpdatingPanelList) return;
        if (_editorState.Document == null) return;

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (shift || ctrl)
        {
            return;
        }

        if (e.ClickedItem is PanelZoneViewModel panelVm)
        {
            _editorState.SelectPanel(panelVm.Id);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            return;
        }

        if (e.ClickedItem is PanelBalloonViewModel balloonVm)
        {
            if (_editorState.Mode == View.EditorMode.PanelLayout)
            {
                _ = SetPanelLayoutModeAsync(false);
            }

            _editorState.SelectBalloon(balloonVm.Id);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            return;
        }

        if (e.ClickedItem is PanelFloatingImageViewModel imageVm)
        {
            if (_editorState.Mode == View.EditorMode.PanelLayout)
            {
                _ = SetPanelLayoutModeAsync(false);
            }

            _editorState.SelectFloatingImage(imageVm.Id);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
        }
    }

    private void PanelListView_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (args.Items.Count != 1) return;

        if (args.Items[0] is PanelBalloonViewModel balloonVm)
        {
            args.Data.SetData(PanelBalloonDragFormat, balloonVm.Id.ToString());
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
        else if (args.Items[0] is PanelFloatingImageViewModel imageVm)
        {
            args.Data.SetData(PanelFloatingImageDragFormat, imageVm.Id.ToString());
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
        else if (args.Items[0] is PanelZoneViewModel)
        {
            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void PanelListView_DragItemsCompleted(object sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult != DataPackageOperation.Move) return;
        if (_editorState.Document?.ActivePage == null) return;
        if (args.Items.Count != 1) return;
        if (args.Items[0] is not PanelZoneViewModel) return;

        var panelItems = PanelListView.Items.OfType<PanelZoneViewModel>().ToList();
        if (panelItems.Count == 0) return;

        var orderedIds = panelItems.Select(item => item.Id).ToList();
        var page = _editorState.Document.ActivePage;
        var currentOrder = page.Panels.OrderBy(panel => panel.Order).Select(panel => panel.Id).ToList();
        if (currentOrder.SequenceEqual(orderedIds)) return;

        _editorState.Execute(new Commands.SetPanelZoneOrdersCommand(page.Id, orderedIds));
        RefreshPanelList();
        MainCanvas.Invalidate();
    }

    private void PanelListView_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(PanelBalloonDragFormat) && !e.DataView.Contains(PanelFloatingImageDragFormat)) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (!TryGetPanelDropTarget(e.OriginalSource as DependencyObject, out var panelId)) return;

        var panel = page.FindPanel(panelId);
        if (panel == null) return;

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.Caption = $"Move to {panel.Name}";
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    private async void PanelListView_Drop(object sender, DragEventArgs e)
    {
        var isBalloonDrop = e.DataView.Contains(PanelBalloonDragFormat);
        var isImageDrop = e.DataView.Contains(PanelFloatingImageDragFormat);
        if (!isBalloonDrop && !isImageDrop) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (!TryGetPanelDropTarget(e.OriginalSource as DependencyObject, out var targetPanelId)) return;

        var panel = page.FindPanel(targetPanelId);
        if (panel == null) return;

        var commands = new List<Commands.ICommand>();
        if (isBalloonDrop)
        {
            var rawBalloonIds = await e.DataView.GetDataAsync(PanelBalloonDragFormat) as string;
            foreach (var balloonId in ParseGuidList(rawBalloonIds))
            {
                var balloon = page.FindBalloon(balloonId);
                if (balloon == null) continue;
                if (balloon.PanelId == targetPanelId) continue;
                commands.Add(new Commands.SetBalloonPanelCommand(balloonId, targetPanelId));
            }
        }

        if (isImageDrop)
        {
            var rawImageIds = await e.DataView.GetDataAsync(PanelFloatingImageDragFormat) as string;
            foreach (var imageId in ParseGuidList(rawImageIds))
            {
                var image = page.FindFloatingImage(imageId);
                if (image == null) continue;
                if (image.PanelId == targetPanelId) continue;
                commands.Add(new Commands.SetFloatingImagePanelCommand(page.Id, imageId, targetPanelId));
            }
        }

        if (commands.Count == 0) return;

        _editorState.ExecuteTransaction("Move balloons to panel", commands);
        RefreshPanelList();
        RefreshLayerList();
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
    }

    private static bool TryGetPanelDropTarget(DependencyObject? source, out Guid panelId)
    {
        panelId = Guid.Empty;
        if (source == null) return false;

        var listViewItem = FindVisualParent<ListViewItem>(source);
        if (listViewItem?.DataContext is PanelZoneViewModel panelVm)
        {
            panelId = panelVm.Id;
            return true;
        }

        if (listViewItem?.DataContext is PanelBalloonViewModel balloonVm)
        {
            panelId = balloonVm.PanelId;
            return true;
        }

        if (listViewItem?.DataContext is PanelFloatingImageViewModel imageVm)
        {
            panelId = imageVm.PanelId;
            return true;
        }

        return false;
    }

    private static List<Guid> ParseGuidList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<Guid>();
        }

        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    private static T? FindVisualParent<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T typed) return typed;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void TogglePanelExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid panelId)
        {
            if (_expandedPanels.Contains(panelId))
            {
                _expandedPanels.Remove(panelId);
            }
            else
            {
                _expandedPanels.Add(panelId);
            }
            RefreshPanelList();
        }
    }

    private void TogglePanelVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var page = _editorState.Document?.ActivePage;
            var panel = page?.FindPanel(id);
            if (panel != null)
            {
                _editorState.Execute(new Commands.SetPanelZoneVisibilityCommand(page!.Id, id, !panel.IsVisible));
                RefreshPanelList();
                MainCanvas.Invalidate();
            }
        }
    }

    private void TogglePanelLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid id)
        {
            var page = _editorState.Document?.ActivePage;
            var panel = page?.FindPanel(id);
            if (panel != null)
            {
                _editorState.Execute(new Commands.SetPanelZoneLockedCommand(page!.Id, id, !panel.IsLocked));
                RefreshPanelList();
            }
        }
    }

    private void PanelDesignModeButton_Click(object sender, RoutedEventArgs e)
    {
        _ = SetPanelLayoutModeAsync(_editorState.Mode != View.EditorMode.PanelLayout);
    }

    private void AddPanel_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null) return;

        var nextOrder = page.Panels.Count + 1;
        var panelName = $"Panel {nextOrder}";
        var bounds = new Model.Rect(100, 100, 400, 600);

        _editorState.Execute(new Commands.CreatePanelZoneCommand(page.Id, panelName, bounds, nextOrder));
        RefreshPanelList();
        MainCanvas.Invalidate();
    }

    private void DeletePanel_Click(object sender, RoutedEventArgs e)
    {
        var panelId = _editorState.SelectedPanelId;
        if (!panelId.HasValue) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var orderedPanels = page.Panels.OrderBy(p => p.Order).ToList();
        var deletedIndex = orderedPanels.FindIndex(p => p.Id == panelId.Value);

        _editorState.Execute(new Commands.DeletePanelZoneCommand(page.Id, panelId.Value));

        Guid? nextPanelId = null;
        if (deletedIndex >= 0 && page.Panels.Count > 0)
        {
            orderedPanels = page.Panels.OrderBy(p => p.Order).ToList();
            if (deletedIndex < orderedPanels.Count)
            {
                nextPanelId = orderedPanels[deletedIndex].Id;
            }
            else if (orderedPanels.Count > 0)
            {
                nextPanelId = orderedPanels[^1].Id;
            }
        }

        _editorState.SelectPanel(nextPanelId);
        RefreshPanelList();
        MainCanvas.Invalidate();
    }

    private void DuplicatePanel_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelectedPanel();
        RefreshPanelList();
    }

    private void AutoOrderPanels_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null || page.Panels.Count == 0) return;

        var orderedIds = ComputeAutoPanelOrder(page);
        if (orderedIds.Count == 0) return;

        var currentOrder = page.Panels.OrderBy(panel => panel.Order).Select(panel => panel.Id).ToList();
        if (currentOrder.SequenceEqual(orderedIds))
        {
            SetStatusMessage(L("panel.status.reading_order_unchanged"));
            return;
        }

        _editorState.Execute(new Commands.SetPanelZoneOrdersCommand(page.Id, orderedIds));
        RefreshPanelList();
        MainCanvas.Invalidate();
        SetStatusMessage(L("panel.status.reading_order_auto_detected"));
    }

    private static List<Guid> ComputeAutoPanelOrder(DocumentPage page)
    {
        var panels = page.Panels.ToList();
        if (panels.Count <= 1)
        {
            return panels.Select(panel => panel.Id).ToList();
        }

        var sorted = panels
            .OrderBy(panel => panel.Bounds.Top)
            .ThenBy(panel => panel.Bounds.Left)
            .ToList();

        var rowGapThreshold = MathF.Max(6f, page.PanelGutterWidth * 0.5f);
        var rows = new List<PanelRow>();

        foreach (var panel in sorted)
        {
            PanelRow? bestRow = null;
            var bestDistance = float.MaxValue;

            foreach (var row in rows)
            {
                if (!row.Overlaps(panel, rowGapThreshold)) continue;
                var distance = MathF.Abs(panel.Bounds.Center.Y - row.CenterY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestRow = row;
                }
            }

            if (bestRow == null)
            {
                var newRow = new PanelRow();
                newRow.Add(panel);
                rows.Add(newRow);
            }
            else
            {
                bestRow.Add(panel);
            }
        }

        var direction = page.ReadingDirection == ReadingDirection.RightToLeft
            ? ReadingDirection.RightToLeft
            : ReadingDirection.LeftToRight;

        var ordered = new List<Guid>();
        foreach (var row in rows.OrderBy(row => row.Top))
        {
            var rowPanels = direction == ReadingDirection.RightToLeft
                ? row.Panels.OrderByDescending(panel => panel.Bounds.Right).ThenBy(panel => panel.Bounds.Top)
                : row.Panels.OrderBy(panel => panel.Bounds.Left).ThenBy(panel => panel.Bounds.Top);

            ordered.AddRange(rowPanels.Select(panel => panel.Id));
        }

        return ordered;
    }

    private sealed class PanelRow
    {
        public List<PanelZone> Panels { get; } = new();
        public float Top { get; private set; }
        public float Bottom { get; private set; }
        public float CenterY => (Top + Bottom) / 2f;

        public void Add(PanelZone panel)
        {
            if (Panels.Count == 0)
            {
                Top = panel.Bounds.Top;
                Bottom = panel.Bounds.Bottom;
            }
            else
            {
                Top = MathF.Min(Top, panel.Bounds.Top);
                Bottom = MathF.Max(Bottom, panel.Bounds.Bottom);
            }

            Panels.Add(panel);
        }

        public bool Overlaps(PanelZone panel, float threshold)
        {
            return panel.Bounds.Top <= Bottom + threshold && panel.Bounds.Bottom >= Top - threshold;
        }
    }



    private void RefreshPageSetup()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var page = doc.ActivePage;

        _isUpdatingProperties = true;
        if (PageNameTextBox != null && page != null) PageNameTextBox.Text = page.Name;
        if (PageWidthBox != null) PageWidthBox.Value = doc.Size.Width;
        if (PageHeightBox != null) PageHeightBox.Value = doc.Size.Height;
        if (PageDpiBox != null) PageDpiBox.Value = doc.DefaultDpi;
        if (PageReadingDirectionComboBox != null && page != null)
        {
            SelectComboBoxItemByTag(PageReadingDirectionComboBox, page.ReadingDirection.ToString());
        }
        if (PageBackgroundImageFitModeComboBox != null && page != null)
        {
            var fitModeTag = page.BackgroundImageFitMode == PanelImageFitMode.Original
                ? "Fit"
                : page.BackgroundImageFitMode.ToString();
            SelectComboBoxItemByTag(PageBackgroundImageFitModeComboBox, fitModeTag);
        }
        if (PageSizePresetComboBox != null)
        {
            SelectComboBoxItemByTag(PageSizePresetComboBox, $"{doc.Size.Width:F0}x{doc.Size.Height:F0}");
            if (PageSizePresetComboBox.SelectedItem == null)
            {
                PageSizePresetComboBox.SelectedIndex = 0;
            }
        }

        if (page != null)
        {
            var bgColor = page.BackgroundColor;
            if (bgColor == null)
            {
                if (PageBackgroundColorComboBox != null) SelectComboBoxItemByTag(PageBackgroundColorComboBox, "Transparent");
                if (PageBackgroundColorPreview != null) PageBackgroundColorPreview.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            else
            {
                var color = bgColor.Value;
                var winColor = Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
                if (PageBackgroundColorPreview != null) PageBackgroundColorPreview.Background = new SolidColorBrush(winColor);

                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                if (PageBackgroundColorComboBox != null && !SelectComboBoxItemByTag(PageBackgroundColorComboBox, hexColor))
                {
                    SelectComboBoxItemByTag(PageBackgroundColorComboBox, "#FFFFFF");
                }
            }
        }

        _isUpdatingProperties = false;

        if (BackgroundImageStatus != null)
        {
            var hasBackground = !string.IsNullOrEmpty(doc.BackgroundImagePath);
            BackgroundImageStatus.Text = hasBackground
                ? System.IO.Path.GetFileName(doc.BackgroundImagePath)
                : L("page.status.no_image");
        }
    }

    private void PagePropertiesNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitPageNameFromProperties();
    }

    private void PagePropertiesNameTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        CommitPageNameFromProperties();
    }

    private void CommitPageNameFromProperties()
    {
        if (_isUpdatingProperties || PageNameTextBox == null) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var nextName = (PageNameTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nextName))
        {
            _isUpdatingProperties = true;
            PageNameTextBox.Text = page.Name;
            _isUpdatingProperties = false;
            return;
        }

        if (string.Equals(nextName, page.Name, StringComparison.Ordinal)) return;

        _editorState.Execute(new Commands.RenamePageCommand(page.Id, nextName));
        _ = RefreshPageListAsync();
    }

    private bool SelectComboBoxItemByTag(ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items)
        {
            if (item is ComboBoxItem comboItem && comboItem.Tag is string itemTag &&
                string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return true;
            }
        }
        return false;
    }

    private static void SelectNumericPresetComboBoxItem(ComboBox comboBox, float value, float tolerance = 0.01f)
    {
        comboBox.SelectedIndex = -1;
        foreach (var item in comboBox.Items)
        {
            if (item is not ComboBoxItem comboItem) continue;
            var tagText = comboItem.Tag?.ToString();
            if (!float.TryParse(tagText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                continue;
            }
            if (MathF.Abs(parsed - value) <= tolerance)
            {
                comboBox.SelectedItem = comboItem;
                return;
            }
        }
    }

    private void PageSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties) return;
        var doc = _editorState.Document;
        if (doc == null) return;

        if (PageWidthBox == null || PageHeightBox == null) return;
        var newWidth = (float)PageWidthBox.Value;
        var newHeight = (float)PageHeightBox.Value;
        if (newWidth > 0 && newHeight > 0)
        {
            _editorState.Execute(new Commands.ResizeDocumentCommand(new Model.Size2(newWidth, newHeight)));
            MainCanvas.Invalidate();
        }
    }

    private void PageSizePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (PageSizePresetComboBox == null) return;
        if (PageSizePresetComboBox.SelectedIndex <= 0) return; // Custom or nothing

        var preset = PageSizePresetComboBox.SelectedItem as ComboBoxItem;
        if (preset == null) return;

        var (width, height) = ParsePagePresetSize(preset.Tag?.ToString());
        if (width <= 0 || height <= 0)
        {
            (width, height) = preset.Content?.ToString() switch
            {
                "US Letter (8.5x11 in)" => (2550f, 3300f),
                "A4 (210x297 mm)" => (2480f, 3508f),
                "US Comic (6.625x10.25 in)" => (1988f, 3075f),
                "Manga B5 (182x257 mm)" => (2150f, 3035f),
                "Full HD (1920x1080)" => (1920f, 1080f),
                "HD (1280x720)" => (1280f, 720f),
                "Instagram Square (1080x1080)" => (1080f, 1080f),
                "Social Media (1200x628)" => (1200f, 628f),
                "Square (1200x1200)" => (1200f, 1200f),
                "3-Panel Strip (2400x800)" => (2400f, 800f),
                "4-Panel Strip (3200x800)" => (3200f, 800f),
                "Webtoon (800x2400)" => (800f, 2400f),
                _ => (0f, 0f)
            };
        }

        if (width > 0 && height > 0)
        {
            _editorState.Execute(new Commands.ResizeDocumentCommand(new Model.Size2(width, height)));
            RefreshPageSetup();
            MainCanvas.Invalidate();
        }
    }

    private static (float Width, float Height) ParsePagePresetSize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) ||
            string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("header", StringComparison.OrdinalIgnoreCase))
        {
            return (0f, 0f);
        }

        var parts = tag.Split('x');
        if (parts.Length != 2)
        {
            return (0f, 0f);
        }

        if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width) &&
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var height))
        {
            return (width, height);
        }

        return (0f, 0f);
    }

    private void PageReadingDirectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || PageReadingDirectionComboBox == null) return;
        if (PageReadingDirectionComboBox.SelectedItem is not ComboBoxItem selected || selected.Tag is not string tag) return;
        if (!Enum.TryParse<ReadingDirection>(tag, out var direction)) return;
        if (page.ReadingDirection == direction) return;

        _editorState.Execute(new Commands.SetPageReadingDirectionCommand(page.Id, direction));
        MainCanvas.Invalidate();
    }

    private void PageDpiBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties) return;
        var doc = _editorState.Document;
        if (doc == null) return;

        if (PageDpiBox == null) return;
        var newDpi = (float)PageDpiBox.Value;
        if (newDpi < 1f) return;

        if (Math.Abs(newDpi - doc.DefaultDpi) > 0.5f)
        {
            _editorState.Execute(new Commands.SetDocumentDpiCommand(newDpi));
        }
    }

    private async void PageBackgroundColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (_editorState == null) return; // Guard against XAML initialization
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (PageBackgroundColorComboBox == null) return;
        var selected = PageBackgroundColorComboBox.SelectedItem as ComboBoxItem;
        if (selected?.Tag is string colorHex)
        {
            Model.Color? newColor;
            if (colorHex == "Transparent")
            {
                newColor = null;
                if (PageBackgroundColorPreview != null) PageBackgroundColorPreview.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            else if (colorHex == "CUSTOM")
            {
                var currentColor = page.BackgroundColor ?? Model.Color.White;
                var customColor = await ShowColorPickerDialogAsync(currentColor);
                if (customColor.HasValue)
                {
                    newColor = customColor.Value;
                    var color = Windows.UI.Color.FromArgb(newColor.Value.A, newColor.Value.R, newColor.Value.G, newColor.Value.B);
                    if (PageBackgroundColorPreview != null) PageBackgroundColorPreview.Background = new SolidColorBrush(color);
                }
                else
                {
                    _isUpdatingProperties = true;
                    PageBackgroundColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                var modelColor = ParseHexColor(colorHex);
                newColor = modelColor;
                var color = Windows.UI.Color.FromArgb(modelColor.A, modelColor.R, modelColor.G, modelColor.B);
                if (PageBackgroundColorPreview != null) PageBackgroundColorPreview.Background = new SolidColorBrush(color);
            }

            _editorState.Execute(new Commands.SetPageBackgroundColorCommand(page.Id, newColor));
            MainCanvas.Invalidate();
        }
    }

    private async void LoadBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var page = _editorState.Document?.ActivePage;
                if (page == null) return;

                var bitmap = await LoadBitmapForImportAsync(file);
                _editorState.Execute(new Commands.SetPageBackgroundImageCommand(page.Id, file.Path));
                _editorState.SetBackgroundImageForPage(page.Id, bitmap);

                RefreshPageSetup();
                MainCanvas.Invalidate();
                StatusText.Text = LF("image.status.loaded", file.Name);
            }
            catch (Exception ex)
            {
                StatusText.Text = LF("image.error.load_failed", ex.Message);
            }
        }
    }

    private void ClearPageBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        _editorState.Execute(new Commands.SetPageBackgroundImageCommand(page.Id, null));
        _editorState.SetBackgroundImageForPage(page.Id, null);
        RefreshPageSetup();
        MainCanvas.Invalidate();
        SetStatusMessage(L("page.status.no_image"));
    }

    private void PageBackgroundImageFitMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || PageBackgroundImageFitModeComboBox == null) return;
        if (PageBackgroundImageFitModeComboBox.SelectedItem is not ComboBoxItem selected || selected.Tag is not string tag) return;
        if (!Enum.TryParse<PanelImageFitMode>(tag, out var fitMode)) return;
        if (page.BackgroundImageFitMode == fitMode) return;

        _editorState.Execute(new Commands.SetPageBackgroundImageFitModeCommand(page.Id, fitMode));
        MainCanvas.Invalidate();
    }



    private ToggleButton? PropGeneralTabButton => PropShapeTabButton; // General renamed to Shape
    private ToggleButton? PropStylesTabButton => null;
    private ToggleButton? PropLinksTabButton => null;
    private ToggleButton? PropGuidesTabButton => null;
    private ToggleButton? PropTemplatesTabButton => null;
    private ScrollViewer? PropGeneralTabScrollViewer => PropShapeTabScrollViewer; // General renamed to Shape
    private ScrollViewer? PropLinksTabScrollViewer => null; // Links merged into Tail
    private FrameworkElement? LinkPropertiesPanel => null; // Links merged into Tail tab directly

    private void PropTextTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Text");
    }

    private void PropShapeTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Shape");
    }

    private void PropEffectsTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Effects");
    }

    private void PropTailTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Tail");
    }

    private void PropAdvancedTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Shape");
    }

    private void PropertiesTabsHeaderBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePropertiesTabHeaderMode();
    }

    internal static PropertiesTabHeaderVisualMode ChoosePropertiesTabHeaderVisualMode(
        double availableWidth,
        double textAndIconWidth,
        double textOnlyWidth,
        double iconOnlyWidth)
    {
        if (availableWidth >= textAndIconWidth)
        {
            return PropertiesTabHeaderVisualMode.TextAndIcon;
        }

        if (availableWidth >= textOnlyWidth)
        {
            return PropertiesTabHeaderVisualMode.TextOnly;
        }

        return PropertiesTabHeaderVisualMode.IconOnly;
    }

    private void UpdatePropertiesTabHeaderMode()
    {
        if (PropertiesTabsHeaderBorder == null || PropertiesTabsHeaderStackPanel == null)
        {
            return;
        }

        var availableWidth = PropertiesTabsHeaderBorder.ActualWidth
            - PropertiesTabsHeaderBorder.Padding.Left
            - PropertiesTabsHeaderBorder.Padding.Right;
        if (availableWidth <= 0)
        {
            return;
        }

        var previousMode = _propertiesTabHeaderMode;
        var textAndIconWidth = MeasurePropertiesTabHeaderWidth(PropertiesTabHeaderVisualMode.TextAndIcon);
        var textOnlyWidth = MeasurePropertiesTabHeaderWidth(PropertiesTabHeaderVisualMode.TextOnly);
        var iconOnlyWidth = MeasurePropertiesTabHeaderWidth(PropertiesTabHeaderVisualMode.IconOnly);

        var chosenMode = ChoosePropertiesTabHeaderVisualMode(availableWidth, textAndIconWidth, textOnlyWidth, iconOnlyWidth);

        if (chosenMode != previousMode)
        {
            ApplyPropertiesTabHeaderMode(chosenMode, updateStoredMode: true);
            return;
        }

        ApplyPropertiesTabHeaderMode(chosenMode, updateStoredMode: false);
    }

    private double MeasurePropertiesTabHeaderWidth(PropertiesTabHeaderVisualMode mode)
    {
        ApplyPropertiesTabHeaderMode(mode, updateStoredMode: false);

        var buttons = GetPropertiesTabButtons();
        var width = 0.0;
        foreach (var button in buttons)
        {
            button.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            width += button.DesiredSize.Width;
        }

        if (buttons.Count > 1)
        {
            width += (buttons.Count - 1) * PropertiesTabsHeaderStackPanel.Spacing;
        }

        return width;
    }

    private IReadOnlyList<ToggleButton> GetPropertiesTabButtons()
    {
        return
        [
            PropShapeTabButton,
            PropTextTabButton,
            PropTailTabButton,
            PropEffectsTabButton
        ];
    }

    private void ApplyPropertiesTabHeaderMode(PropertiesTabHeaderVisualMode mode, bool updateStoredMode)
    {
        var showIcons = mode != PropertiesTabHeaderVisualMode.TextOnly;
        var showText = mode != PropertiesTabHeaderVisualMode.IconOnly;

        var iconVisibility = showIcons ? Visibility.Visible : Visibility.Collapsed;
        var textVisibility = showText ? Visibility.Visible : Visibility.Collapsed;

        PropShapeTabIcon.Visibility = iconVisibility;
        PropTextTabIcon.Visibility = iconVisibility;
        PropTailTabIcon.Visibility = iconVisibility;
        PropEffectsTabIcon.Visibility = iconVisibility;

        PropShapeTabLabel.Visibility = textVisibility;
        PropTextTabLabel.Visibility = textVisibility;
        PropTailTabLabel.Visibility = textVisibility;
        PropEffectsTabLabel.Visibility = textVisibility;

        ToolTipService.SetToolTip(PropShapeTabButton, PropShapeTabLabel.Text);
        ToolTipService.SetToolTip(PropTextTabButton, PropTextTabLabel.Text);
        ToolTipService.SetToolTip(PropTailTabButton, PropTailTabLabel.Text);
        ToolTipService.SetToolTip(PropEffectsTabButton, PropEffectsTabLabel.Text);

        if (updateStoredMode)
        {
            _propertiesTabHeaderMode = mode;
        }
    }

    private void PropGeneralTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Shape");
    }

    private void PropStylesTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void PropLinksTabButton_Click(object sender, RoutedEventArgs e)
    {
        SwitchPropertiesTab("Tail"); // Links merged into Tail
    }

    private void PropGuidesTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void PropTemplatesTabButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void SwitchPropertiesTab(string tabName)
    {
        if (tabName == "General") tabName = "Shape";
        if (tabName == "Links") tabName = "Tail";
        if (tabName == "Fill") tabName = "Effects";
        if (tabName == "Advanced") tabName = "Text";

        _activePropertiesTab = tabName;

        PropTextTabButton.IsChecked = tabName == "Text";
        PropShapeTabButton.IsChecked = tabName == "Shape";
        PropTailTabButton.IsChecked = tabName == "Tail";
        PropEffectsTabButton.IsChecked = tabName == "Effects";

        PropTextTabScrollViewer.Visibility = tabName == "Text" ? Visibility.Visible : Visibility.Collapsed;
        PropShapeTabScrollViewer.Visibility = tabName == "Shape" ? Visibility.Visible : Visibility.Collapsed;
        PropTailTabScrollViewer.Visibility = tabName == "Tail" ? Visibility.Visible : Visibility.Collapsed;
        PropEffectsTabScrollViewer.Visibility = tabName == "Effects" ? Visibility.Visible : Visibility.Collapsed;
        PropAdvancedTabScrollViewer.Visibility = Visibility.Collapsed;

        PropStylesTabScrollViewer.Visibility = Visibility.Collapsed;
        PropGuidesTabScrollViewer.Visibility = Visibility.Collapsed;
        PropTemplatesTabScrollViewer.Visibility = Visibility.Collapsed;
    }



    private void StyleStripComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingStylePresets) return;
        if (StyleStripComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is Guid styleId)
        {
            _selectedBalloonStyleId = styleId;
            SelectStylePreset(BalloonStylePresetComboBox, styleId);
            UpdateStylePresetButtons();

            var style = _editorState.Document?.FindBalloonStyle(styleId);
            if (style != null)
            {
                TryApplyBalloonStyleToSelection(style, statusOnNoSelection: false);
            }
        }
    }

    private void StyleStripEdit_Click(object sender, RoutedEventArgs e)
    {
        ShowBalloonStyleEditorWindow();
    }

}
