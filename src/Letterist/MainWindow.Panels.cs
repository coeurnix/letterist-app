using Letterist.Commands;
using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Persistence;
using Letterist.Rendering;
using Letterist.View;
using DocumentPage = Letterist.Model.Page;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Numerics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;


namespace Letterist;

public sealed partial class MainWindow : Window
{

    private bool _isCommittingPageRename;

    private async Task RefreshPageListAsync()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            PageListView.ItemsSource = null;
            _pageThumbnailCache.Clear();
            return;
        }

        var previousVerticalOffset = CaptureListVerticalOffset(PageListView);
        var pageIds = doc.Pages.Select(page => page.Id).ToHashSet();
        foreach (var stalePageId in _pageThumbnailCache.Keys.Where(id => !pageIds.Contains(id)).ToList())
        {
            _pageThumbnailCache.Remove(stalePageId);
        }

        _isUpdatingPageList = true;
        try
        {
            var viewModels = new List<PageViewModel>();
            foreach (var page in doc.Pages)
            {
                var fingerprint = BuildPageThumbnailFingerprint(page);
                WriteableBitmap? thumbnail;
                if (_pageThumbnailCache.TryGetValue(page.Id, out var cached) && cached.Fingerprint == fingerprint)
                {
                    thumbnail = cached.Thumbnail;
                }
                else
                {
                    thumbnail = await RenderPageThumbnailAsync(page);
                    _pageThumbnailCache[page.Id] = (fingerprint, thumbnail);
                }

                viewModels.Add(new PageViewModel
                {
                    Id = page.Id,
                    Name = page.Name,
                    SizeText = $"{page.Size.Width:F0} × {page.Size.Height:F0}",
                    Thumbnail = thumbnail,
                    IsActive = page.Id == doc.ActivePageId
                });
            }

            PageListView.ItemsSource = viewModels;
            PageListView.SelectedItem = viewModels.FirstOrDefault(v => v.IsActive);
        }
        finally
        {
            _isUpdatingPageList = false;
        }

        await RestoreListVerticalOffsetAsync(PageListView, previousVerticalOffset);
    }

    private async Task<WriteableBitmap?> RenderPageThumbnailAsync(DocumentPage page)
    {
        if (_canvasDevice == null) return null;

        const int width = 104;
        const int height = 144;

        using var renderTarget = new CanvasRenderTarget(_canvasDevice, width, height, 96);
        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

            if (page.Size.Width > 0 && page.Size.Height > 0)
            {
                var scale = Math.Min(width / page.Size.Width, height / page.Size.Height);
                var offsetX = (width - page.Size.Width * scale) / 2f;
                var offsetY = (height - page.Size.Height * scale) / 2f;

                ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);

                var background = _editorState.GetBackgroundImageForPage(page.Id);
                var renderer = _renderer ?? new DocumentRenderer(new ViewTransform());
                renderer.RenderPageContent(
                    ds,
                    page,
                    background,
                    includeHiddenLayers: false,
                    panelImageResolver: GetPanelImage,
                    floatingImageResolver: GetFloatingImage,
                    textFillImageResolver: GetTextFillImage,
                    translationDocument: _editorState.Document);
            }
        }

        var bytes = renderTarget.GetPixelBytes();
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(bytes, 0, bytes.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private static int BuildPageThumbnailFingerprint(DocumentPage page)
    {
        var hash = new HashCode();
        hash.Add(page.Size.Width);
        hash.Add(page.Size.Height);
        hash.Add(page.BackgroundImagePath ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        hash.Add(page.BackgroundImageFitMode);
        hash.Add(page.BackgroundColor?.R ?? 0);
        hash.Add(page.BackgroundColor?.G ?? 0);
        hash.Add(page.BackgroundColor?.B ?? 0);
        hash.Add(page.BackgroundColor?.A ?? 0);

        foreach (var layer in page.Layers)
        {
            hash.Add(layer.Id);
            hash.Add(layer.Kind);
            hash.Add(layer.IsVisible);
            hash.Add(layer.IsLocked);
            hash.Add(layer.Balloons.Count);
        }

        foreach (var panel in page.Panels.OrderBy(panel => panel.Order))
        {
            hash.Add(panel.Id);
            hash.Add(panel.Order);
            hash.Add(panel.Bounds.X);
            hash.Add(panel.Bounds.Y);
            hash.Add(panel.Bounds.Width);
            hash.Add(panel.Bounds.Height);
            hash.Add(panel.IsVisible);
            hash.Add(panel.IsLocked);
        }

        foreach (var image in page.FloatingImages)
        {
            hash.Add(image.Id);
            hash.Add(image.LayerId ?? Guid.Empty);
            hash.Add(image.IsVisible);
            hash.Add(image.IsLocked);
            hash.Add(image.Bounds.X);
            hash.Add(image.Bounds.Y);
            hash.Add(image.Bounds.Width);
            hash.Add(image.Bounds.Height);
            hash.Add(image.Rotation);
        }

        return hash.ToHashCode();
    }

    private async void PageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPageList || _editorState.Document == null) return;

        if (PageListView.SelectedItem is PageViewModel vm)
        {
            await ActivatePageFromListItemAsync(vm);
        }
    }

    private async void PageListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_isUpdatingPageList || _editorState.Document == null) return;
        if (e.ClickedItem is not PageViewModel vm) return;

        if (PageListView.SelectedItem is PageViewModel selected && selected.Id == vm.Id)
        {
            await ActivatePageFromListItemAsync(vm);
            return;
        }

        PageListView.SelectedItem = vm;
    }

    private async Task ActivatePageFromListItemAsync(PageViewModel vm)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        if (doc.ActivePageId != vm.Id)
        {
            _editorState.Execute(new SetActivePageCommand(vm.Id));
            var page = doc.FindPage(vm.Id);
            if (page != null)
            {
                await EnsureBackgroundLoadedAsync(page);
                await EnsureFloatingImagesLoadedAsync(page);
            }
        }

        _editorState.SelectBalloon(null);
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
    }

    private async void AddPage_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var newPageName = $"Page {doc.Pages.Count + 1}";
        var size = doc.DefaultPageSize;
        _editorState.Execute(new CreatePageCommand(newPageName, size, insertIndex: doc.Pages.Count, setActive: true));

        var newPage = doc.ActivePage;
        if (newPage != null && !string.IsNullOrWhiteSpace(newPage.BackgroundImagePath))
        {
            await EnsureBackgroundLoadedAsync(newPage);
        }
    }

    private void PageTemplatesFlyout_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout) return;

        flyout.Items.Clear();

        var doc = _editorState.Document;
        if (doc == null)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = L("panels.menu.no_document"), IsEnabled = false });
            return;
        }

        var saveItem = new MenuFlyoutItem { Text = L("panels.menu.save_template") };
        saveItem.Click += SaveCurrentPageTemplate_Click;
        flyout.Items.Add(saveItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        if (doc.PageTemplates.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = L("panels.menu.no_templates"), IsEnabled = false });
            return;
        }

        foreach (var template in doc.PageTemplates)
        {
            var templateMenu = new MenuFlyoutSubItem
            {
                Text = BuildPageTemplateLabel(template)
            };
            var createItem = new MenuFlyoutItem
            {
                Text = L("panels.menu.create_page"),
                Tag = template.Id
            };
            createItem.Click += CreatePageFromTemplate_Click;

            var renameItem = new MenuFlyoutItem
            {
                Text = L("panels.menu.rename"),
                Tag = template.Id
            };
            renameItem.Click += RenamePageTemplate_Click;

            var deleteItem = new MenuFlyoutItem
            {
                Text = L("common.delete"),
                Tag = template.Id
            };
            deleteItem.Click += DeletePageTemplate_Click;

            templateMenu.Items.Add(createItem);
            templateMenu.Items.Add(new MenuFlyoutSeparator());
            templateMenu.Items.Add(renameItem);
            templateMenu.Items.Add(deleteItem);
            flyout.Items.Add(templateMenu);
        }
    }

    private async void SaveCurrentPageTemplate_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var suggestedName = GetUniquePageTemplateName($"{page.Name} Template", doc.PageTemplates);
        var name = await PromptForPageTemplateNameAsync(suggestedName);
        if (name == null) return;

        var uniqueName = GetUniquePageTemplateName(name, doc.PageTemplates);
        _editorState.Execute(new CreatePageTemplateCommand(page.Id, uniqueName));
        SetStatusMessage(LF("panels.status.saved_template", uniqueName));
    }

    private void CreatePageFromTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        if (item.Tag is not Guid templateId) return;

        var doc = _editorState.Document;
        if (doc == null) return;

        var template = doc.FindPageTemplate(templateId);
        if (template == null) return;

        var newPageName = $"Page {doc.Pages.Count + 1}";
        _editorState.Execute(new CreatePageFromTemplateCommand(templateId, newPageName, insertIndex: doc.Pages.Count, setActive: true));
        SetStatusMessage(LF("panels.status.created_from_template", template.Name));
    }

    private async void RenamePageTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        if (item.Tag is not Guid templateId) return;

        var doc = _editorState.Document;
        if (doc == null) return;

        var template = doc.FindPageTemplate(templateId);
        if (template == null) return;

        var name = await PromptForTemplateRenameAsync(template.Name);
        if (name == null) return;

        var uniqueName = GetUniquePageTemplateName(name, doc.PageTemplates, template.Id);
        if (string.Equals(uniqueName, template.Name, StringComparison.Ordinal))
        {
            return;
        }

        _editorState.Execute(new RenamePageTemplateCommand(template.Id, uniqueName));
        SetStatusMessage(LF("panels.status.renamed_template", uniqueName));
    }

    private async void DeletePageTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        if (item.Tag is not Guid templateId) return;

        var doc = _editorState.Document;
        if (doc == null) return;

        var template = doc.FindPageTemplate(templateId);
        if (template == null) return;

        var confirmed = await ConfirmDeletePageTemplateAsync(template.Name);
        if (!confirmed) return;

        _editorState.Execute(new DeletePageTemplateCommand(template.Id));
        SetStatusMessage(LF("panels.status.deleted_template", template.Name));
    }

    private async Task<string?> PromptForPageTemplateNameAsync(string suggestedName)
    {
        var nameBox = new TextBox
        {
            Text = suggestedName,
            MinWidth = 220
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = L("panels.dialog.template_name"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
        });
        panel.Children.Add(nameBox);

        var dialog = new ContentDialog
        {
            Title = L("panels.dialog.save_template"),
            Content = panel,
            PrimaryButtonText = L("common.save"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var name = nameBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(name) ? suggestedName : name;
    }

    private async Task<string?> PromptForTemplateRenameAsync(string currentName)
    {
        var nameBox = new TextBox
        {
            Text = currentName,
            MinWidth = 220
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = L("panels.dialog.template_name"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
        });
        panel.Children.Add(nameBox);

        var dialog = new ContentDialog
        {
            Title = L("panels.dialog.rename_template"),
            Content = panel,
            PrimaryButtonText = L("common.rename"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var name = nameBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(name) ? currentName : name;
    }

    private async Task<bool> ConfirmDeletePageTemplateAsync(string templateName)
    {
        var dialog = new ContentDialog
        {
            Title = L("panels.dialog.delete_template"),
            Content = new TextBlock
            {
                Text = LF("panels.dialog.delete_confirm", templateName),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = L("common.delete"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static string GetUniquePageTemplateName(string desiredName, IReadOnlyList<PageTemplate> templates, Guid? excludeTemplateId = null)
    {
        var baseName = string.IsNullOrWhiteSpace(desiredName) ? "Page Template" : desiredName.Trim();
        var existing = new HashSet<string>(
            templates
                .Where(template => !excludeTemplateId.HasValue || template.Id != excludeTemplateId.Value)
                .Select(template => template.Name),
            StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName)) return baseName;

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{baseName} ({index})";
            index++;
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static string BuildPageTemplateLabel(PageTemplate template)
    {
        return $"{template.Name} ({template.Size.Width:F0} x {template.Size.Height:F0})";
    }

    private async void DeletePage_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (doc.Pages.Count <= 1) return;

        var selectedPageIds = PageListView.SelectedItems
            .OfType<PageViewModel>()
            .Select(vm => vm.Id)
            .Distinct()
            .ToList();
        if (selectedPageIds.Count == 0)
        {
            selectedPageIds.Add(doc.ActivePageId);
        }

        if (selectedPageIds.Count >= doc.Pages.Count)
        {
            selectedPageIds = selectedPageIds
                .Take(doc.Pages.Count - 1)
                .ToList();
        }

        if (selectedPageIds.Count == 0)
        {
            return;
        }

        if (!await ConfirmMultiDeleteAsync(MultiDeleteItemKind.Pages, selectedPageIds.Count))
        {
            return;
        }

        if (selectedPageIds.Count == 1)
        {
            _editorState.Execute(new DeletePageCommand(selectedPageIds[0]));
        }
        else
        {
            var commands = selectedPageIds
                .Select(id => (ICommand)new DeletePageCommand(id))
                .ToList();
            _editorState.ExecuteTransaction("Delete pages", commands);
        }

        if (doc.ActivePage != null)
        {
            await EnsureBackgroundLoadedAsync(doc.ActivePage);
            await EnsureFloatingImagesLoadedAsync(doc.ActivePage);
        }
    }

    private async void DuplicatePage_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var activeId = doc.ActivePageId;
        var cmd = new DuplicatePageCommand(activeId);
        _editorState.Execute(cmd);

        if (doc.ActivePage != null)
        {
            await EnsureBackgroundLoadedAsync(doc.ActivePage);
            await EnsureFloatingImagesLoadedAsync(doc.ActivePage);
        }

        _ = RefreshPageListAsync();
        MainCanvas.Invalidate();
    }

    private async void DuplicatePageContext_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (!TryResolvePageFromMenuSender(sender, doc, out var pageId, out _)) return;

        var cmd = new DuplicatePageCommand(pageId);
        _editorState.Execute(cmd);

        if (doc.ActivePage != null)
        {
            await EnsureBackgroundLoadedAsync(doc.ActivePage);
            await EnsureFloatingImagesLoadedAsync(doc.ActivePage);
        }

        _ = RefreshPageListAsync();
        MainCanvas.Invalidate();
    }

    private void MovePageUp_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var activeId = doc.ActivePageId;
        var currentIndex = doc.IndexOfPage(activeId);
        if (currentIndex > 0)
        {
            _editorState.Execute(new ReorderPageCommand(activeId, currentIndex - 1));
        }
    }

    private void MovePageDown_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var activeId = doc.ActivePageId;
        var currentIndex = doc.IndexOfPage(activeId);
        if (currentIndex >= 0 && currentIndex < doc.Pages.Count - 1)
        {
            _editorState.Execute(new ReorderPageCommand(activeId, currentIndex + 1));
        }
    }

    private async void CopySelectionToPage_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (!TryResolvePageFromMenuSender(sender, doc, out var pageId, out var pageName)) return;

        var selectedIds = _editorState.SelectedBalloonIds.ToList();
        if (selectedIds.Count == 0)
        {
            SetStatusMessage(L("panels.status.select_to_copy"));
            return;
        }

        if (doc.ActivePageId == pageId)
        {
            SetStatusMessage(L("panels.status.already_on_page"));
            return;
        }

        _editorState.Execute(new CopyBalloonsToPageCommand(doc.ActivePageId, pageId, selectedIds));
        SetStatusMessage(LF("panels.status.copied_balloons", selectedIds.Count, pageName));
        await RefreshPageListAsync();
    }

    private bool TryResolvePageFromMenuSender(
        object sender,
        Document doc,
        out Guid pageId,
        out string pageName)
    {
        pageId = Guid.Empty;
        pageName = string.Empty;

        if (sender is MenuFlyoutItem item)
        {
            if (item.Tag is PageViewModel vmFromTag)
            {
                pageId = vmFromTag.Id;
                pageName = vmFromTag.Name;
                return true;
            }

            if (item.DataContext is PageViewModel vmFromDataContext)
            {
                pageId = vmFromDataContext.Id;
                pageName = vmFromDataContext.Name;
                return true;
            }
        }

        if (PageListView.SelectedItem is PageViewModel selectedVm)
        {
            pageId = selectedVm.Id;
            pageName = selectedVm.Name;
            return true;
        }

        var activePage = doc.ActivePage;
        if (activePage != null)
        {
            pageId = activePage.Id;
            pageName = activePage.Name;
            return true;
        }

        return false;
    }

    private void PageListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult != DataPackageOperation.Move) return;
        if (_editorState.Document == null) return;
        if (args.Items.Count != 1) return;
        if (args.Items[0] is not PageViewModel vm) return;

        var targetIndex = PageListView.Items.IndexOf(vm);
        if (targetIndex < 0) return;

        var currentIndex = _editorState.Document.IndexOfPage(vm.Id);
        if (currentIndex < 0 || currentIndex == targetIndex) return;

        _editorState.Execute(new ReorderPageCommand(vm.Id, targetIndex));
    }

    private void PageName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_isUpdatingPageList) return;
        if (sender is not TextBlock textBlock) return;
        if (textBlock.DataContext is not PageViewModel vm) return;

        BeginPageRename(vm);
        e.Handled = true;
    }

    private void PageNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitPageRename(textBox, cancel: false);
        }
    }

    private void PageNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitPageRename(textBox, cancel: false);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CommitPageRename(textBox, cancel: true);
            e.Handled = true;
        }
    }

    private void BeginPageRename(PageViewModel vm)
    {
        if (PageListView == null) return;

        var container = PageListView.ContainerFromItem(vm) as ListViewItem;
        if (container == null) return;

        var nameTextBlock = FindDescendant<TextBlock>(container, "PageNameTextBlock");
        var nameTextBox = FindDescendant<TextBox>(container, "PageNameTextBox");
        if (nameTextBlock == null || nameTextBox == null) return;

        nameTextBlock.Visibility = Visibility.Collapsed;
        nameTextBox.Visibility = Visibility.Visible;
        nameTextBox.Text = vm.Name;
        nameTextBox.Focus(FocusState.Programmatic);
        nameTextBox.SelectAll();
    }

    private void CommitPageRename(TextBox textBox, bool cancel)
    {
        if (_isCommittingPageRename) return;
        _isCommittingPageRename = true;

        try
        {
            var vm = textBox.DataContext as PageViewModel;
            if (vm == null)
            {
                EndPageRename(textBox, null);
                return;
            }

            var nextName = textBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(nextName))
            {
                nextName = vm.Name;
            }

            EndPageRename(textBox, vm);

            if (!cancel &&
                _editorState.Document != null &&
                !string.Equals(nextName, vm.Name, StringComparison.Ordinal))
            {
                _editorState.Execute(new RenamePageCommand(vm.Id, nextName));
            }
        }
        finally
        {
            _isCommittingPageRename = false;
        }
    }

    private void EndPageRename(TextBox textBox, PageViewModel? vm)
    {
        if (PageListView == null)
        {
            textBox.Visibility = Visibility.Collapsed;
            return;
        }

        var container = vm != null ? PageListView.ContainerFromItem(vm) as ListViewItem : null;
        if (container == null)
        {
            textBox.Visibility = Visibility.Collapsed;
            return;
        }

        var nameTextBlock = FindDescendant<TextBlock>(container, "PageNameTextBlock");
        if (nameTextBlock != null)
        {
            nameTextBlock.Visibility = Visibility.Visible;
        }

        textBox.Visibility = Visibility.Collapsed;
    }

    private static T? FindDescendant<T>(DependencyObject parent, string? name = null) where T : DependencyObject
    {
        if (parent == null) return null;

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                if (name == null) return typed;
                if (child is FrameworkElement element && element.Name == name) return typed;
            }

            var match = FindDescendant<T>(child, name);
            if (match != null) return match;
        }

        return null;
    }

    private static double? CaptureListVerticalOffset(ListView? listView)
    {
        return listView == null
            ? null
            : FindDescendant<ScrollViewer>(listView)?.VerticalOffset;
    }

    private static async Task RestoreListVerticalOffsetAsync(ListView? listView, double? verticalOffset)
    {
        if (listView == null || !verticalOffset.HasValue)
        {
            return;
        }

        await Task.Yield();

        var viewer = FindDescendant<ScrollViewer>(listView);
        if (viewer == null)
        {
            return;
        }

        var clamped = Math.Clamp(verticalOffset.Value, 0d, viewer.ScrollableHeight);
        viewer.ChangeView(null, clamped, null, disableAnimation: true);
    }



    private void RefreshLayerList()
    {
        if (_isDraggingLayerItem)
        {
            _refreshLayerListPending = true;
            return;
        }

        _isUpdatingLayerList = true;
        try
        {
            if (_editorState.Document == null)
            {
                LayerListView.ItemsSource = null;
                LayerOpacityPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var page = _editorState.Document.ActivePage;
            if (page == null)
            {
                LayerListView.ItemsSource = null;
                LayerOpacityPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var items = new List<LayerListItemViewModel>();
            var selectedBalloonIds = new HashSet<Guid>(_editorState.SelectedBalloonIds);
            var selectedBalloonId = _editorState.Document.SelectedBalloonId;

            void AddLayerWithBalloons(Layer layer, bool isInGroup)
            {
                var balloonsInLayer = layer.Kind == LayerKind.Balloon
                    ? layer.Balloons.ToList()
                    : new List<Balloon>();
                var imagesInLayer = page.FloatingImages
                    .Where(image => image.LayerId == layer.Id)
                    .ToList();
                var balloonCount = balloonsInLayer.Count;
                var imageCount = imagesInLayer.Count;
                var contentCount = balloonCount + imageCount;

                var isExpanded = contentCount > 0;

                items.Add(new LayerViewModel
                {
                    Id = layer.Id,
                    Name = layer.Name,
                    Kind = layer.Kind,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    IsActive = layer.Id == page.ActiveLayerId,
                    GroupId = isInGroup ? layer.GroupId : null,
                    BalloonCount = contentCount,
                    IsExpanded = isExpanded
                });

                if (contentCount == 0) return;

                var index = 0;
                foreach (var balloon in balloonsInLayer.AsEnumerable().Reverse())
                {
                    index++;
                    items.Add(new BalloonViewModel
                    {
                        Id = balloon.Id,
                        LayerId = layer.Id,
                        PanelId = balloon.PanelId,
                        PanelName = balloon.PanelId.HasValue
                            ? page.FindPanel(balloon.PanelId.Value)?.Name
                            : null,
                        Name = GetBalloonListName(balloon, index),
                        HoverToolTip = GetBalloonHoverText(balloon),
                        IsVisible = balloon.IsVisible,
                        IsLocked = balloon.IsLocked,
                        IsActive = selectedBalloonIds.Contains(balloon.Id),
                        IsInGroup = isInGroup
                    });
                }

                var imageIndex = 0;
                foreach (var image in imagesInLayer.AsEnumerable().Reverse())
                {
                    imageIndex++;
                    items.Add(new FloatingImageViewModel
                    {
                        Id = image.Id,
                        LayerId = layer.Id,
                        IsInGroup = isInGroup,
                        Name = GetFloatingImageListName(image, imageIndex),
                        ImagePath = image.ImagePath,
                        IsVisible = image.IsVisible,
                        IsLocked = image.IsLocked,
                        IsActive = _editorState.SelectedFloatingImageIds.Contains(image.Id)
                    });
                }
            }

            var layersReversed = page.Layers.Reverse().ToList();
            var processedLayers = new HashSet<Guid>();

            foreach (var layer in layersReversed)
            {
                if (processedLayers.Contains(layer.Id)) continue;

                if (layer.GroupId.HasValue)
                {
                    var group = page.FindLayerGroup(layer.GroupId.Value);
                    if (group != null && !items.Any(i => i.IsGroup && i.Id == group.Id))
                    {
                        var layersInGroup = page.GetLayersInGroup(group.Id).ToList();

                        var isExpanded = true;

                        items.Add(new LayerGroupViewModel
                        {
                            Id = group.Id,
                            Name = group.Name,
                            IsVisible = group.IsVisible,
                            IsLocked = group.IsLocked,
                            IsExpanded = isExpanded,
                            LayerCount = layersInGroup.Count
                        });

                        foreach (var groupLayer in layersInGroup.AsEnumerable().Reverse())
                        {
                            AddLayerWithBalloons(groupLayer, isInGroup: true);
                            processedLayers.Add(groupLayer.Id);
                        }
                    }
                }
                else
                {
                    AddLayerWithBalloons(layer, isInGroup: false);
                    processedLayers.Add(layer.Id);
                }
            }

            LayerListView.ItemsSource = items;

            LayerListItemViewModel? selectedVm = null;
            if (selectedBalloonId.HasValue)
            {
                selectedVm = items.OfType<BalloonViewModel>().FirstOrDefault(vm => vm.Id == selectedBalloonId.Value);
            }

            if (selectedVm == null && _editorState.SelectedFloatingImageId.HasValue)
            {
                selectedVm = items.OfType<FloatingImageViewModel>().FirstOrDefault(vm => vm.Id == _editorState.SelectedFloatingImageId.Value);
            }

            if (selectedVm == null)
            {
                selectedVm = items.OfType<LayerViewModel>().FirstOrDefault(v => v.Id == page.ActiveLayerId);
            }

            if (selectedVm != null)
            {
                LayerListView.SelectedItem = selectedVm;
            }

            UpdateLayerOpacityPanel();
        }
        finally
        {
            _isUpdatingLayerList = false;
        }
    }

    private static string GetBalloonListName(Balloon balloon, int index)
    {
        var text = balloon.Text ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"Balloon {index}";
        }

        var trimmed = text.Replace("\r", "").Trim();
        var firstLine = trimmed.Split('\n').FirstOrDefault() ?? trimmed;
        firstLine = firstLine.Trim();
        if (firstLine.Length > 32)
        {
            firstLine = $"{firstLine.Substring(0, 32)}...";
        }
        return firstLine;
    }

    private static string? GetBalloonHoverText(Balloon balloon)
    {
        var text = balloon.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Replace("\r", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        const int maxLength = 280;
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return $"{normalized.Substring(0, maxLength).TrimEnd()}...";
    }

    private static string GetFloatingImageListName(FloatingImage image, int index)
    {
        if (!string.IsNullOrWhiteSpace(image.Name))
        {
            return image.Name;
        }

        if (!string.IsNullOrEmpty(image.ImagePath))
        {
            return System.IO.Path.GetFileNameWithoutExtension(image.ImagePath);
        }

        return $"Image {index}";
    }

    private void LayerListView_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayerList) return;

        if (_editorState.Document == null) return;
        var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[^1] : LayerListView.SelectedItem;
        if (selectedItem == null) return;

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var additive = false;

        if (TryHandleModifiedLayerListSelection(selectedItem, shift, ctrl))
        {
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            return;
        }

        var clickedIndex = LayerListView.Items.IndexOf(selectedItem);
        if (!shift && !ctrl && clickedIndex >= 0)
        {
            _layerListSelectionAnchorIndex = clickedIndex;
        }

        if (selectedItem is PanelZoneViewModel panelVm)
        {
            _editorState.SelectPanel(panelVm.Id);
            if (_editorState.Document?.SelectedBalloonId != null)
            {
                _editorState.SelectBalloon(null);
            }
            UpdatePropertiesPanel();
            var panelName = panelVm.Name.Replace($" ({panelVm.BalloonCount})", "");
            SetStatusMessage(LF("panels.status.selected_panel", panelName));
            MainCanvas.Invalidate();
            return;
        }

        if (_editorState.SelectedPanelId.HasValue)
        {
            _editorState.SelectPanel(null);
        }

        if (selectedItem is FloatingImageViewModel imageVm)
        {
            if (_editorState.Document.ActiveLayerId != imageVm.LayerId)
            {
                _editorState.Execute(new SetActiveLayerCommand(imageVm.LayerId));
            }

            if (additive)
            {
                _editorState.ToggleFloatingImageSelection(imageVm.Id, preserveBalloonSelection: true);
            }
            else if (_editorState.SelectedFloatingImageIds.Contains(imageVm.Id))
            {
                _editorState.SetPrimaryFloatingImageSelection(imageVm.Id);
            }
            else
            {
                _editorState.SelectFloatingImage(imageVm.Id);
            }

            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            SetStatusMessage(LF("panels.status.selected_image", imageVm.Name));
            return;
        }

        if (!additive && _editorState.SelectedFloatingImageId.HasValue)
        {
            _editorState.SelectFloatingImage(null);
        }

        if (_editorState.Mode == EditorMode.PanelLayout &&
            (selectedItem is BalloonViewModel || selectedItem is PanelBalloonViewModel))
        {
            _ = SetPanelLayoutModeAsync(false);
        }

        if (selectedItem is PanelBalloonViewModel panelBalloonVm)
        {
            if (_editorState.Document.ActiveLayerId != panelBalloonVm.LayerId)
            {
                _editorState.Execute(new SetActiveLayerCommand(panelBalloonVm.LayerId));
            }

            if (additive)
            {
                _editorState.ToggleBalloonSelection(panelBalloonVm.Id, preserveFloatingImageSelection: true);
            }
            else if (!_editorState.SelectedBalloonIds.Contains(panelBalloonVm.Id) ||
                     _editorState.Document.SelectedBalloonId != panelBalloonVm.Id)
            {
                _editorState.SelectBalloon(panelBalloonVm.Id);
            }
            else
            {
                UpdatePropertiesPanel();
            }
            MainCanvas.Invalidate();
            return;
        }

        if (selectedItem is BalloonViewModel balloonVm)
        {
            if (_editorState.Document.ActiveLayerId != balloonVm.LayerId)
            {
                _editorState.Execute(new SetActiveLayerCommand(balloonVm.LayerId));
            }

            if (additive)
            {
                _editorState.ToggleBalloonSelection(balloonVm.Id, preserveFloatingImageSelection: true);
            }
            else if (!_editorState.SelectedBalloonIds.Contains(balloonVm.Id) ||
                     _editorState.Document.SelectedBalloonId != balloonVm.Id)
            {
                _editorState.SelectBalloon(balloonVm.Id);
            }
            else
            {
                UpdatePropertiesPanel();
            }
            MainCanvas.Invalidate();
            return;
        }

        if (selectedItem is LayerViewModel vm)
        {
            if (_editorState.Document.ActiveLayerId != vm.Id)
            {
                _editorState.Execute(new SetActiveLayerCommand(vm.Id));
            }
            if (_editorState.Document.SelectedBalloonId != null)
            {
                _editorState.SelectBalloon(null);
            }
            UpdatePropertiesPanel();
            UpdateLayerOpacityPanel();
            MainCanvas.Invalidate();
            SetStatusMessage(LF("panels.status.active_layer", vm.Name));
            return;
        }
        else if (selectedItem is LayerGroupViewModel)
        {
            UpdateLayerOpacityPanel();
        }
        else if (selectedItem is PanelSectionHeaderViewModel)
        {
            _expandedPanelSection = !_expandedPanelSection;
            RefreshLayerList();
        }
    }

    private bool TryHandleModifiedLayerListSelection(object selectedItem, bool shift, bool ctrl)
    {
        if (!shift && !ctrl)
        {
            return false;
        }

        if (selectedItem is not BalloonViewModel &&
            selectedItem is not PanelBalloonViewModel &&
            selectedItem is not FloatingImageViewModel &&
            selectedItem is not PanelZoneViewModel)
        {
            return false;
        }

        var clickedIndex = LayerListView.Items.IndexOf(selectedItem);
        if (clickedIndex < 0)
        {
            return false;
        }

        if (ctrl)
        {
            _layerListSelectionAnchorIndex = clickedIndex;

            switch (selectedItem)
            {
                case PanelZoneViewModel panelVm:
                    _editorState.TogglePanelSelection(panelVm.Id);
                    return true;
                case PanelBalloonViewModel panelBalloonVm:
                    _editorState.ToggleBalloonSelection(panelBalloonVm.Id, preserveFloatingImageSelection: true);
                    return true;
                case BalloonViewModel balloonVm:
                    _editorState.ToggleBalloonSelection(balloonVm.Id, preserveFloatingImageSelection: true);
                    return true;
                case FloatingImageViewModel imageVm:
                    _editorState.ToggleFloatingImageSelection(imageVm.Id, preserveBalloonSelection: true);
                    return true;
            }

            return false;
        }

        var anchorIndex = _layerListSelectionAnchorIndex;
        if (anchorIndex < 0 || anchorIndex >= LayerListView.Items.Count)
        {
            anchorIndex = clickedIndex;
            _layerListSelectionAnchorIndex = clickedIndex;
        }

        var start = Math.Min(anchorIndex, clickedIndex);
        var end = Math.Max(anchorIndex, clickedIndex);

        if (selectedItem is PanelZoneViewModel)
        {
            var panelIds = new List<Guid>();
            Guid? primaryPanelId = null;
            for (var index = start; index <= end; index++)
            {
                if (LayerListView.Items[index] is not PanelZoneViewModel panelVm) continue;
                panelIds.Add(panelVm.Id);
                primaryPanelId = panelVm.Id;
            }

            _editorState.SetPanelSelection(panelIds, primaryPanelId);
            return true;
        }

        var balloonIds = new List<Guid>();
        var balloonSeen = new HashSet<Guid>();
        var imageIds = new List<Guid>();
        var imageSeen = new HashSet<Guid>();

        Guid? primaryBalloonId = null;
        Guid? primaryImageId = null;

        for (var index = start; index <= end; index++)
        {
            switch (LayerListView.Items[index])
            {
                case PanelBalloonViewModel panelBalloonVm when balloonSeen.Add(panelBalloonVm.Id):
                    balloonIds.Add(panelBalloonVm.Id);
                    primaryBalloonId = panelBalloonVm.Id;
                    break;
                case BalloonViewModel balloonVm when balloonSeen.Add(balloonVm.Id):
                    balloonIds.Add(balloonVm.Id);
                    primaryBalloonId = balloonVm.Id;
                    break;
                case FloatingImageViewModel imageVm when imageSeen.Add(imageVm.Id):
                    imageIds.Add(imageVm.Id);
                    primaryImageId = imageVm.Id;
                    break;
            }
        }

        _editorState.SetSelection(balloonIds, primaryBalloonId, preserveFloatingImageSelection: imageIds.Count > 0);
        _editorState.SetFloatingImageSelection(imageIds, primaryImageId, preserveBalloonSelection: balloonIds.Count > 0);
        return true;
    }

    private void LayerOpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingLayerOpacity || _editorState.Document?.ActiveLayer == null) return;

        var layer = _editorState.Document.ActiveLayer;
        var newOpacity = (float)(e.NewValue / 100.0);
        newOpacity = Math.Clamp(newOpacity, 0f, 1f);

        LayerOpacityValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(newOpacity - layer.Opacity) < 0.001f) return;
        _editorState.Execute(new SetLayerOpacityCommand(layer.Id, newOpacity));
    }

    private void UpdateLayerOpacityPanel()
    {
        var layer = _editorState.Document?.ActiveLayer;
        if (layer == null)
        {
            LayerOpacityPanel.Visibility = Visibility.Collapsed;
            return;
        }

        LayerOpacityPanel.Visibility = Visibility.Visible;

        _isUpdatingLayerOpacity = true;
        try
        {
            var percent = layer.Opacity * 100f;
            LayerOpacitySlider.Value = percent;
            LayerOpacityValueText.Text = $"{percent:F0}%";
        }
        finally
        {
            _isUpdatingLayerOpacity = false;
        }
    }

    private void LayerListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Delete) return;

        if (LayerListView.SelectedItem is LayerViewModel or LayerGroupViewModel)
        {
            DeleteLayer_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (_editorState.SelectedPanelIds.Count > 0)
        {
            DeleteSelectedPanel();
            e.Handled = true;
        }
    }

    private void LayerListView_DragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (args.Items.Count != 1) return;

        if (args.Items[0] is BalloonViewModel || args.Items[0] is FloatingImageViewModel || args.Items[0] is LayerViewModel)
        {
            args.Data.RequestedOperation = DataPackageOperation.Move;
            _isDraggingLayerItem = true;
            _refreshLayerListPending = false;
        }
    }

    private void LayerListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _isDraggingLayerItem = false;

        try
        {
            if (args.DropResult != DataPackageOperation.Move) return;
            if (_editorState.Document == null) return;
            if (args.Items.Count != 1) return;

            switch (args.Items[0])
            {
                case LayerViewModel vm:
                    ReorderLayerFromLayerList(vm);
                    break;
                case BalloonViewModel vm:
                    ReorderBalloonFromLayerList(vm);
                    break;
                case FloatingImageViewModel vm:
                    ReorderFloatingImageFromLayerList(vm);
                    break;
            }
        }
        finally
        {
            if (_refreshLayerListPending)
            {
                _refreshLayerListPending = false;
                RefreshLayerList();
            }
        }
    }

    private void ReorderLayerFromLayerList(LayerViewModel vm)
    {
        if (vm.Kind == LayerKind.Image) return;

        var layerItems = LayerListView.Items.OfType<LayerViewModel>().ToList();
        var visualIndex = layerItems.FindIndex(item => item.Id == vm.Id);
        if (visualIndex < 0) return;

        var layerCount = _editorState.Document!.Layers.Count;
        var targetIndex = layerCount - 1 - visualIndex;
        var currentIndex = _editorState.Document.IndexOfLayer(vm.Id);

        if (targetIndex == currentIndex) return;

        var backgroundLayer = _editorState.Document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            var minIndex = _editorState.Document.IndexOfLayer(backgroundLayer.Id) + 1;
            if (targetIndex < minIndex)
            {
                targetIndex = minIndex;
            }
        }

        _editorState.Execute(new ReorderLayerCommand(vm.Id, targetIndex));
    }

    private void ReorderBalloonFromLayerList(BalloonViewModel vm)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var layer = doc.FindLayer(vm.LayerId);
        if (layer == null) return;

        var balloon = layer.FindBalloon(vm.Id);
        if (balloon == null) return;

        var visualItems = LayerListView.Items
            .OfType<BalloonViewModel>()
            .Where(item => item.LayerId == vm.LayerId)
            .ToList();
        var targetVisualIndex = visualItems.FindIndex(item => item.Id == vm.Id);
        if (targetVisualIndex < 0) return;

        var scopedIdsBottomToTop = layer.Balloons
            .Select(item => item.Id)
            .ToList();
        var currentScopedIndex = scopedIdsBottomToTop.IndexOf(vm.Id);
        if (currentScopedIndex < 0) return;

        var targetScopedIndex = scopedIdsBottomToTop.Count - 1 - targetVisualIndex;
        if (targetScopedIndex == currentScopedIndex) return;

        var reorderedScoped = new List<Guid>(scopedIdsBottomToTop);
        MoveListItem(reorderedScoped, currentScopedIndex, targetScopedIndex);

        var allIds = layer.Balloons.Select(item => item.Id).ToList();
        var reorderedAll = MergeReorderedSubset(allIds, scopedIdsBottomToTop, reorderedScoped);

        var targetIndex = reorderedAll.IndexOf(vm.Id);
        var currentIndex = layer.IndexOfBalloon(vm.Id);
        if (targetIndex < 0 || targetIndex == currentIndex) return;

        _editorState.Execute(new ReorderBalloonCommand(vm.Id, targetIndex));
    }

    private void ReorderFloatingImageFromLayerList(FloatingImageViewModel vm)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var image = page.FindFloatingImage(vm.Id);
        if (image == null) return;

        var visualItems = LayerListView.Items
            .OfType<FloatingImageViewModel>()
            .Where(item => item.LayerId == vm.LayerId)
            .ToList();
        var targetVisualIndex = visualItems.FindIndex(item => item.Id == vm.Id);
        if (targetVisualIndex < 0) return;

        var scopedIdsBottomToTop = page.FloatingImages
            .Where(item => item.LayerId == vm.LayerId)
            .Select(item => item.Id)
            .ToList();
        var currentScopedIndex = scopedIdsBottomToTop.IndexOf(vm.Id);
        if (currentScopedIndex < 0) return;

        var targetScopedIndex = scopedIdsBottomToTop.Count - 1 - targetVisualIndex;
        if (targetScopedIndex == currentScopedIndex) return;

        var reorderedScoped = new List<Guid>(scopedIdsBottomToTop);
        MoveListItem(reorderedScoped, currentScopedIndex, targetScopedIndex);

        var allIds = page.FloatingImages.Select(item => item.Id).ToList();
        var reorderedAll = MergeReorderedSubset(allIds, scopedIdsBottomToTop, reorderedScoped);

        var targetIndex = reorderedAll.IndexOf(vm.Id);
        var currentIndex = page.IndexOfFloatingImage(vm.Id);
        if (targetIndex < 0 || targetIndex == currentIndex) return;

        _editorState.Execute(new ReorderFloatingImageCommand(page.Id, vm.Id, targetIndex));
    }

    private static void MoveListItem<T>(List<T> list, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;

        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(Math.Clamp(toIndex, 0, list.Count), item);
    }

    private static List<Guid> MergeReorderedSubset(
        IReadOnlyList<Guid> allIds,
        IReadOnlyCollection<Guid> originalSubsetIds,
        IReadOnlyList<Guid> reorderedSubsetIds)
    {
        var merged = new List<Guid>(allIds.Count);
        var subset = new HashSet<Guid>(originalSubsetIds);
        var subsetIndex = 0;

        foreach (var id in allIds)
        {
            if (subset.Contains(id))
            {
                merged.Add(reorderedSubsetIds[subsetIndex]);
                subsetIndex++;
            }
            else
            {
                merged.Add(id);
            }
        }

        return merged;
    }

    private void LayerListItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var dataContext = element.DataContext;
        if (dataContext is not BalloonViewModel && dataContext is not FloatingImageViewModel) return;

        var flyout = new MenuFlyout();

        var moveUp = new MenuFlyoutItem { Text = LF("layers.context.move_up") };
        var moveDown = new MenuFlyoutItem { Text = LF("layers.context.move_down") };
        var moveToTop = new MenuFlyoutItem { Text = LF("layers.context.move_to_top") };
        var moveToBottom = new MenuFlyoutItem { Text = LF("layers.context.move_to_bottom") };

        moveUp.Click += (s, args) => ReorderLayerListItem(dataContext, -1);
        moveDown.Click += (s, args) => ReorderLayerListItem(dataContext, 1);
        moveToTop.Click += (s, args) => ReorderLayerListItem(dataContext, int.MinValue);
        moveToBottom.Click += (s, args) => ReorderLayerListItem(dataContext, int.MaxValue);

        flyout.Items.Add(moveUp);
        flyout.Items.Add(moveDown);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(moveToTop);
        flyout.Items.Add(moveToBottom);

        if (dataContext is BalloonViewModel bvm)
        {
            var layer = _editorState.Document?.FindLayer(bvm.LayerId);
            if (layer != null)
            {
                var layerBalloons = layer.Balloons.ToList();
                var idx = layerBalloons.FindIndex(b => b.Id == bvm.Id);
                moveUp.IsEnabled = idx < layerBalloons.Count - 1;
                moveDown.IsEnabled = idx > 0;
                moveToTop.IsEnabled = idx < layerBalloons.Count - 1;
                moveToBottom.IsEnabled = idx > 0;
            }
        }
        else if (dataContext is FloatingImageViewModel ivm)
        {
            var page = _editorState.Document?.ActivePage;
            if (page != null)
            {
                var layerImages = page.FloatingImages.Where(i => i.LayerId == ivm.LayerId).ToList();
                var idx = layerImages.FindIndex(i => i.Id == ivm.Id);
                moveUp.IsEnabled = idx < layerImages.Count - 1;
                moveDown.IsEnabled = idx > 0;
                moveToTop.IsEnabled = idx < layerImages.Count - 1;
                moveToBottom.IsEnabled = idx > 0;
            }
        }

        flyout.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void ReorderLayerListItem(object dataContext, int direction)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        if (dataContext is BalloonViewModel bvm)
        {
            var layer = doc.FindLayer(bvm.LayerId);
            if (layer == null) return;

            var layerBalloons = layer.Balloons.ToList();
            var currentScopedIndex = layerBalloons.FindIndex(b => b.Id == bvm.Id);
            if (currentScopedIndex < 0) return;

            int targetScopedIndex;
            if (direction == int.MinValue) targetScopedIndex = layerBalloons.Count - 1; // move to top = max model index
            else if (direction == int.MaxValue) targetScopedIndex = 0; // move to bottom = min model index
            else if (direction < 0) targetScopedIndex = currentScopedIndex + 1; // move up visually = increase model index
            else targetScopedIndex = currentScopedIndex - 1; // move down visually = decrease model index

            targetScopedIndex = Math.Clamp(targetScopedIndex, 0, layerBalloons.Count - 1);
            if (targetScopedIndex == currentScopedIndex) return;

            var scopedIds = layerBalloons.Select(b => b.Id).ToList();
            var reorderedScoped = new List<Guid>(scopedIds);
            MoveListItem(reorderedScoped, currentScopedIndex, targetScopedIndex);

            var allIds = layer.Balloons.Select(b => b.Id).ToList();
            var reorderedAll = MergeReorderedSubset(allIds, scopedIds, reorderedScoped);

            var targetIndex = reorderedAll.IndexOf(bvm.Id);
            var currentIndex = layer.IndexOfBalloon(bvm.Id);
            if (targetIndex < 0 || targetIndex == currentIndex) return;

            _editorState.Execute(new ReorderBalloonCommand(bvm.Id, targetIndex));
        }
        else if (dataContext is FloatingImageViewModel ivm)
        {
            var page = doc.ActivePage;
            if (page == null) return;

            var layerImages = page.FloatingImages.Where(i => i.LayerId == ivm.LayerId).ToList();
            var currentIndex = layerImages.FindIndex(i => i.Id == ivm.Id);
            if (currentIndex < 0) return;

            int targetIndex;
            if (direction == int.MinValue) targetIndex = layerImages.Count - 1;
            else if (direction == int.MaxValue) targetIndex = 0;
            else if (direction < 0) targetIndex = currentIndex + 1;
            else targetIndex = currentIndex - 1;

            targetIndex = Math.Clamp(targetIndex, 0, layerImages.Count - 1);
            if (targetIndex == currentIndex) return;

            var scopedIds = layerImages.Select(i => i.Id).ToList();
            var reorderedScoped = new List<Guid>(scopedIds);
            MoveListItem(reorderedScoped, currentIndex, targetIndex);

            var allIds = page.FloatingImages.Select(i => i.Id).ToList();
            var reorderedAll = MergeReorderedSubset(allIds, scopedIds, reorderedScoped);

            var globalTarget = reorderedAll.IndexOf(ivm.Id);
            var globalCurrent = page.IndexOfFloatingImage(ivm.Id);
            if (globalTarget < 0 || globalTarget == globalCurrent) return;

            _editorState.Execute(new ReorderFloatingImageCommand(page.Id, ivm.Id, globalTarget));
        }
    }

    private void ToggleLayerVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Button btn && btn.Tag is Guid id)
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null) return;

            if (btn.DataContext is BalloonViewModel)
            {
                var balloon = page.FindBalloon(id);
                if (balloon != null)
                {
                    _editorState.Execute(new SetBalloonVisibilityCommand(id, !balloon.IsVisible));
                }
                return;
            }

            var floatingImage = page.FindFloatingImage(id);
            if (floatingImage != null)
            {
                _editorState.Execute(new SetFloatingImageVisibilityCommand(page.Id, id, !floatingImage.IsVisible));
                return;
            }

            var panel = page.FindPanel(id);
            if (panel != null)
            {
                _editorState.Execute(new SetPanelZoneVisibilityCommand(page.Id, id, !panel.IsVisible));
                return;
            }

            var group = page.FindLayerGroup(id);
            if (group != null)
            {
                _editorState.Execute(new SetLayerGroupVisibilityCommand(page.Id, id, !group.IsVisible));
                return;
            }

            var layer = _editorState.Document?.FindLayer(id);
            if (layer != null)
            {
                _editorState.Execute(new SetLayerVisibilityCommand(id, !layer.IsVisible));
            }
        }
    }

    private void ToggleLayerLock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Button btn && btn.Tag is Guid id)
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null) return;

            if (btn.DataContext is BalloonViewModel)
            {
                var balloon = page.FindBalloon(id);
                if (balloon != null)
                {
                    _editorState.Execute(new SetBalloonLockedCommand(id, !balloon.IsLocked));
                }
                return;
            }

            var floatingImage = page.FindFloatingImage(id);
            if (floatingImage != null)
            {
                _editorState.Execute(new SetFloatingImageLockedCommand(page.Id, id, !floatingImage.IsLocked));
                return;
            }

            var panel = page.FindPanel(id);
            if (panel != null)
            {
                _editorState.Execute(new SetPanelZoneLockedCommand(page.Id, id, !panel.IsLocked));
                return;
            }

            var group = page.FindLayerGroup(id);
            if (group != null)
            {
                _editorState.Execute(new SetLayerGroupLockedCommand(page.Id, id, !group.IsLocked));
                return;
            }

            var layer = _editorState.Document?.FindLayer(id);
            if (layer != null)
            {
                _editorState.Execute(new SetLayerLockedCommand(id, !layer.IsLocked));
            }
        }
    }

    private void ToggleLayerGroupExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Controls.Button btn) return;

        if (btn.DataContext is LayerGroupViewModel groupVm)
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null) return;

            var groupId = groupVm.Id;
            if (_expandedLayerGroups.Contains(groupId))
            {
                _expandedLayerGroups.Remove(groupId);
            }
            else
            {
                _expandedLayerGroups.Add(groupId);
            }

            _editorState.Execute(new SetLayerGroupExpandedCommand(page.Id, groupId, _expandedLayerGroups.Contains(groupId)));
            RefreshLayerList();
            return;
        }

        if (btn.DataContext is LayerViewModel layerVm && layerVm.BalloonCount > 0)
        {
            var layerId = layerVm.Id;
            if (_expandedLayerBalloons.Contains(layerId))
            {
                _expandedLayerBalloons.Remove(layerId);
            }
            else
            {
                _expandedLayerBalloons.Add(layerId);
            }

            RefreshLayerList();
            return;
        }

        if (btn.DataContext is PanelZoneViewModel panelVm && panelVm.BalloonCount > 0)
        {
            var panelId = panelVm.Id;
            if (_expandedPanels.Contains(panelId))
            {
                _expandedPanels.Remove(panelId);
            }
            else
            {
                _expandedPanels.Add(panelId);
            }

            RefreshLayerList();
        }
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var layerCount = _editorState.Document?.BalloonLayerCount ?? 0;
        _editorState.Execute(new CreateLayerCommand($"Layer {layerCount + 1}"));
    }

    private async void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Document == null) return;

        var selectedLayerCount = LayerListView.SelectedItems
            .Cast<object>()
            .Count(item => item is LayerViewModel or LayerGroupViewModel);
        if (selectedLayerCount == 0 && LayerListView.SelectedItem is LayerViewModel or LayerGroupViewModel)
        {
            selectedLayerCount = 1;
        }

        if (!await ConfirmMultiDeleteAsync(MultiDeleteItemKind.Layers, selectedLayerCount))
        {
            return;
        }

        if (LayerListView.SelectedItem is LayerGroupViewModel groupVm)
        {
            var page = _editorState.Document.ActivePage;
            if (page != null)
            {
                _editorState.Execute(new DeleteLayerGroupCommand(page.Id, groupVm.Id));
            }
            return;
        }

        if (_editorState.Document.Layers.Count <= 1)
        {
            return;
        }

        var activeLayer = _editorState.Document.ActiveLayer;
        if (activeLayer == null) return;
        if (activeLayer.Kind == LayerKind.Image)
        {
            StatusText.Text = L("panels.status.cannot_delete_bg");
            return;
        }

        if (_editorState.Document.BalloonLayerCount <= 1)
        {
            StatusText.Text = L("panels.status.cannot_delete_last");
            return;
        }

        _editorState.Execute(new DeleteLayerCommand(activeLayer.Id));
    }

    private void AddLayerGroup_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var groupCount = page.LayerGroups.Count;
        var cmd = new CreateLayerGroupCommand(page.Id, $"Group {groupCount + 1}");
        _editorState.Execute(cmd);

        _expandedLayerGroups.Add(cmd.CreatedGroupId);
    }

    private void MoveLayerUp_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Document == null) return;

        var activeLayer = _editorState.Document.ActiveLayer;
        if (activeLayer == null) return;
        if (activeLayer.Kind == LayerKind.Image)
        {
            StatusText.Text = L("panels.status.cannot_move_bg");
            return;
        }

        var activeId = activeLayer.Id;
        var layers = _editorState.Document.Layers;
        var currentIndex = -1;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].Id == activeId)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex >= 0 && currentIndex < layers.Count - 1)
        {
            _editorState.Execute(new ReorderLayerCommand(activeId, currentIndex + 1));
        }
    }

    private void MoveLayerDown_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Document == null) return;

        var activeLayer = _editorState.Document.ActiveLayer;
        if (activeLayer == null) return;
        if (activeLayer.Kind == LayerKind.Image)
        {
            StatusText.Text = L("panels.status.cannot_move_bg");
            return;
        }

        var activeId = activeLayer.Id;
        var layers = _editorState.Document.Layers;
        var currentIndex = -1;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].Id == activeId)
            {
                currentIndex = i;
                break;
            }
        }

        var minIndex = 0;
        var backgroundLayer = _editorState.Document.BackgroundLayer;
        if (backgroundLayer != null)
        {
            minIndex = _editorState.Document.IndexOfLayer(backgroundLayer.Id) + 1;
        }

        if (currentIndex > minIndex)
        {
            _editorState.Execute(new ReorderLayerCommand(activeId, currentIndex - 1));
        }
    }

    private void LayerName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock) return;
        if (textBlock.DataContext is not LayerListItemViewModel vm) return;
        if (!CanRenameLayerListItem(vm)) return;
        if (textBlock.Parent is not Grid grid) return;

        foreach (var child in grid.Children)
        {
            if (child is TextBox editBox)
            {
                textBlock.Visibility = Visibility.Collapsed;
                editBox.Visibility = Visibility.Visible;
                editBox.Text = textBlock.Text;
                editBox.SelectAll();
                editBox.Focus(FocusState.Programmatic);
                break;
            }
        }

        e.Handled = true;
    }

    private static bool CanRenameLayerListItem(LayerListItemViewModel vm)
    {
        return vm is LayerViewModel or LayerGroupViewModel or BalloonViewModel or FloatingImageViewModel;
    }

    private void LayerNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitLayerNameEdit(sender as TextBox);
    }

    private void LayerNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not TextBox editBox) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitLayerNameEdit(editBox);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelLayerNameEdit(editBox);
            e.Handled = true;
        }
    }

    private void CommitLayerNameEdit(TextBox? editBox)
    {
        if (editBox == null) return;
        if (editBox.DataContext is not LayerListItemViewModel vm || !CanRenameLayerListItem(vm))
        {
            CancelLayerNameEdit(editBox);
            return;
        }
        if (editBox.Parent is not Grid grid) return;

        var newName = editBox.Text?.Trim() ?? "";
        var page = _editorState.Document?.ActivePage;

        if (editBox.Tag is Guid id && page != null)
        {
            if (vm is LayerViewModel or LayerGroupViewModel)
            {
                var group = page.FindLayerGroup(id);
                if (group != null)
                {
                    if (string.IsNullOrEmpty(newName)) newName = "Group";
                    if (group.Name != newName)
                    {
                        _editorState.Execute(new RenameLayerGroupCommand(page.Id, id, newName));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(newName)) newName = "Layer";
                    var layer = _editorState.Document?.Layers.FirstOrDefault(l => l.Id == id);
                    if (layer != null && layer.Name != newName)
                    {
                        _editorState.Execute(new RenameLayerCommand(id, newName));
                    }
                }
            }
            else if (vm is BalloonViewModel)
            {
                var balloon = page.FindBalloon(id);
                if (balloon != null && !string.Equals(balloon.Text, newName, StringComparison.Ordinal))
                {
                    _editorState.Execute(new SetBalloonTextCommand(id, newName));
                }
            }
            else if (vm is FloatingImageViewModel)
            {
                var image = page.FindFloatingImage(id);
                var nextName = string.IsNullOrWhiteSpace(newName) ? null : newName;
                if (image != null && !string.Equals(image.Name, nextName, StringComparison.Ordinal))
                {
                    _editorState.Execute(new RenameFloatingImageCommand(page.Id, id, nextName));
                }
            }
        }

        editBox.Visibility = Visibility.Collapsed;
        foreach (var child in grid.Children)
        {
            if (child is TextBlock textBlock)
            {
                textBlock.Visibility = Visibility.Visible;
                break;
            }
        }
    }

    private void CancelLayerNameEdit(TextBox? editBox)
    {
        if (editBox == null) return;
        if (editBox.Parent is not Grid grid) return;

        editBox.Visibility = Visibility.Collapsed;
        foreach (var child in grid.Children)
        {
            if (child is TextBlock textBlock)
            {
                textBlock.Visibility = Visibility.Visible;
                break;
            }
        }
    }

    private void LeftSidebarSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var minCanvasWidth = 240.0;
        var maxWidth = RootGrid.ActualWidth - RightSidebarColumn.ActualWidth - minCanvasWidth - 12;
        var minWidth = LeftSidebarColumn.MinWidth;
        if (maxWidth < minWidth)
        {
            maxWidth = minWidth;
        }

        var newWidth = LeftSidebarColumn.ActualWidth + e.HorizontalChange;
        newWidth = Math.Clamp(newWidth, minWidth, maxWidth);
        LeftSidebarColumn.Width = new GridLength(newWidth);
        UpdateLeftSidebarTabHeaderMode();
    }

    private void RightSidebarSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var minCanvasWidth = 240.0;
        var maxWidth = RootGrid.ActualWidth - LeftSidebarColumn.ActualWidth - minCanvasWidth - 12;
        var minWidth = RightSidebarColumn.MinWidth;
        if (maxWidth < minWidth)
        {
            maxWidth = minWidth;
        }

        var newWidth = RightSidebarColumn.ActualWidth - e.HorizontalChange;
        newWidth = Math.Clamp(newWidth, minWidth, maxWidth);
        RightSidebarColumn.Width = new GridLength(newWidth);
        UpdatePropertiesTabHeaderMode();
    }

    private void SidebarSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (RightSidebarGrid == null) return;

        var minHeight = PropertiesRow.MinHeight;
        var maxHeight = RightSidebarGrid.ActualHeight - LayersRow.MinHeight - 6;
        if (maxHeight < minHeight)
        {
            maxHeight = minHeight;
        }

        var newHeight = PropertiesRow.ActualHeight + e.VerticalChange;
        newHeight = Math.Clamp(newHeight, minHeight, maxHeight);
        PropertiesRow.Height = new GridLength(newHeight);
    }


}
