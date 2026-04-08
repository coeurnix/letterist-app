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
using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Globalization;
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
    private static readonly HttpClient ImageUrlClient = new();


    private void SelectTool_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            _ = SetPanelLayoutModeAsync(false);
            return;
        }

        _editorState.Mode = EditorMode.Select;
        UpdateToolButtonStates();
    }

    private void CreateBalloonTool_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            _ = SetPanelLayoutModeAsync(false);
            return;
        }

        _editorState.Mode = EditorMode.CreateBalloon;
        UpdateToolButtonStates();
    }

    private void ToggleTail_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.EditText)
        {
            _editorState.ExitTextEditMode(saveChanges: true);
        }
        ToggleTailOnSelectedBalloon();
    }

    private void UpdateToolButtonStates()
    {
        var toolbarState = ToolbarStateResolver.Resolve(_editorState);
        var inLayoutMode = toolbarState.Context == ToolbarContextKind.PanelLayout;
        SelectToolButton.IsChecked = !inLayoutMode && _editorState.Mode == EditorMode.Select;
        CreateBalloonButton.IsChecked = !inLayoutMode && _editorState.Mode == EditorMode.CreateBalloon;
        SelectToolButton.IsEnabled = !inLayoutMode;
        CreateBalloonButton.IsEnabled = !inLayoutMode;
        ToggleTailButton.IsEnabled = !inLayoutMode && toolbarState.HasBalloonSelection;
        UpdateToolbarUi(toolbarState);
        UpdatePanelLayoutUi();
        UpdateObjectGroupingMenuState();
        RefreshBalloonTemplateQuickPalette();
        MainCanvas.Invalidate();
    }

    private async void PanelLayoutMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item) return;
        await SetPanelLayoutModeAsync(item.IsChecked);
    }

    private void TypographyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item) return;
        _editorState.ShowTypesettingDiagnostics = item.IsChecked;
        MainCanvas.Invalidate();
    }

    private void PanelSelectTool_Click(object sender, RoutedEventArgs e)
    {
        _panelLayoutTool = PanelLayoutToolMode.Select;
        CancelPanelDrawing();
        CommitPanelShapeEdit();
        UpdatePanelToolButtonStates();
    }

    private void PanelShapeTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button) return;

        if (button.Tag is string shapeStr && Enum.TryParse<PanelDrawTool>(shapeStr, out var drawTool))
        {
            _selectedPanelDrawTool = drawTool;
            _selectedPanelShape = drawTool switch
            {
                PanelDrawTool.Rectangle => PanelShape.Rectangle,
                PanelDrawTool.RoundedRect => PanelShape.RoundedRect,
                PanelDrawTool.Ellipse => PanelShape.Ellipse,
                _ => PanelShape.Custom
            };
        }

        _panelLayoutTool = PanelLayoutToolMode.Draw;
        CancelPanelDrawing();
        CommitPanelShapeEdit();
        UpdatePanelToolButtonStates();
    }

    private void UpdatePanelToolButtonStates()
    {
        var inDrawMode = _panelLayoutTool == PanelLayoutToolMode.Draw;

        PanelSelectToolButton.IsChecked = _panelLayoutTool == PanelLayoutToolMode.Select;

        PanelRectToolButton.IsChecked = inDrawMode && _selectedPanelDrawTool == PanelDrawTool.Rectangle;
        PanelRoundedRectToolButton.IsChecked = inDrawMode && _selectedPanelDrawTool == PanelDrawTool.RoundedRect;
        PanelEllipseToolButton.IsChecked = inDrawMode && _selectedPanelDrawTool == PanelDrawTool.Ellipse;
        if (PanelPolygonToolButton != null)
        {
            PanelPolygonToolButton.IsChecked = inDrawMode && _selectedPanelDrawTool == PanelDrawTool.Polygon;
        }
        if (PanelFreeformToolButton != null)
        {
            PanelFreeformToolButton.IsChecked = inDrawMode && _selectedPanelDrawTool == PanelDrawTool.Freeform;
        }

        if (PanelToolHintText != null)
        {
            PanelToolHintText.Text = GetPanelToolHintText(inDrawMode);
        }
    }

    private async void ExitPanelLayout_Click(object sender, RoutedEventArgs e)
    {
        await SetPanelLayoutModeAsync(false);
    }

    public async void TogglePanelLayoutMode()
    {
        var enterMode = _editorState.Mode != EditorMode.PanelLayout;
        await SetPanelLayoutModeAsync(enterMode);
    }

    public void DuplicateSelectedPanel()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        var selectedPanelId = _editorState.SelectedPanelId;
        if (doc == null || page == null || selectedPanelId == null) return;

        var panel = page.Panels.FirstOrDefault(p => p.Id == selectedPanelId);
        if (panel == null) return;

        var offset = 20f;
        var newBounds = new Rect(panel.Bounds.X + offset, panel.Bounds.Y + offset,
                                 panel.Bounds.Width, panel.Bounds.Height);

        var nextOrder = page.Panels.Count + 1;
        var panelName = $"Panel {nextOrder}";

        _editorState.Execute(new CreatePanelZoneCommand(
            page.Id, panelName, newBounds, nextOrder,
            safeMargin: panel.SafeMargin,
            borderColor: panel.BorderColor,
            borderWidth: panel.BorderWidth,
            borderStyle: panel.BorderStyle,
            shape: panel.Shape,
            cornerRadius: panel.CornerRadius,
            customShapePathData: panel.CustomShapePathData));

        var newPanel = page.Panels.LastOrDefault();
        if (newPanel != null)
        {
            _editorState.SelectPanel(newPanel.Id);
            _lastCreatedPanelId = newPanel.Id;
            _lastCreatedBalloonId = null;
        }

        SetStatusMessage(L("panel.status.duplicated"));
        RefreshLayerList();
        MainCanvas.Invalidate();
        SetRepeatableAction(DuplicateSelectedPanel);
    }

    private async Task SetPanelLayoutModeAsync(bool enabled)
    {
        var doc = _editorState.Document;
        if (enabled)
        {
            if (_editorState.Mode == EditorMode.EditText)
            {
                _editorState.ExitTextEditMode(saveChanges: true);
            }

            if (_editorState.Mode != EditorMode.PanelLayout)
            {
                _modeBeforeLayout = _editorState.Mode == EditorMode.EditText ? EditorMode.Select : _editorState.Mode;
            }

            _editorState.Mode = EditorMode.PanelLayout;
            _editorState.SelectBalloon(null);
            SetStatusMessage(L("panel_layout.status.active"));
        }
        else
        {
            if (_editorState.Mode != EditorMode.PanelLayout)
            {
                UpdatePanelLayoutUi();
                return;
            }

            if (_isEditingPanelShape)
            {
                CommitPanelShapeEdit();
            }

            if (_isPanelPolygonDrawing || _isPanelFreeformDrawing)
            {
                CancelPanelDrawing();
            }

            _editorState.SelectPanel(null);
            _editorState.Mode = _modeBeforeLayout == EditorMode.PanelLayout ? EditorMode.Select : _modeBeforeLayout;
            SetStatusMessage(L("panel_layout.status.exited"));
        }

        _panelPreviewBounds = null;
        UpdateToolButtonStates();
    }

    private void UpdatePanelLayoutUi()
    {
        var inLayoutMode = _editorState.Mode == EditorMode.PanelLayout;
        if (PanelLayoutToolbar != null)
        {
            PanelLayoutToolbar.Visibility = inLayoutMode ? Visibility.Visible : Visibility.Collapsed;
        }

        if (MenuPanelDesignMode != null)
        {
            MenuPanelDesignMode.IsChecked = inLayoutMode;
        }

        if (PanelDesignModeButton != null)
        {
            PanelDesignModeButton.IsChecked = inLayoutMode;
        }

        if (inLayoutMode)
        {
            _panelLayoutTool = PanelLayoutToolMode.Select;
            UpdatePanelToolButtonStates();
        }

    }

    private async Task<bool> ConfirmExitPanelLayoutModeAsync()
    {
        var dialog = new ContentDialog
        {
            Title = L("tools.dialog.exit_panel_layout"),
            Content = new TextBlock
            {
                Text = L("tools.dialog.exit_panel_hint"),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = L("tools.dialog.exit"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }



    private async void AddBalloonMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Document == null) return;

        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            await SetPanelLayoutModeAsync(false);
        }

        AddBalloonAtCursorOrViewportCenter();
    }

    private void ShowGridMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item) return;
        _preferences.Canvas.ShowGrid = item.IsChecked;
        ApplyGridPreferences();
    }

    private void SnapToGridMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item) return;
        _preferences.Canvas.SnapToGrid = item.IsChecked;
        ApplyGridPreferences();
    }

    private void SnapToGuidesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem item) return;
        _editorState.SnapToGuides = item.IsChecked;
        if (SnapToGuidesToggle != null)
        {
            SnapToGuidesToggle.IsOn = _editorState.SnapToGuides;
        }
        if (SnapToolbarToggle != null)
        {
            SnapToolbarToggle.IsChecked = _editorState.SnapToGuides;
        }
        UpdateToolButtonStates();
        MainCanvas.Invalidate();
    }

    private void ToggleGridVisibility()
    {
        _preferences.Canvas.ShowGrid = !_preferences.Canvas.ShowGrid;
        ApplyGridPreferences();
    }

    private void ToggleSnapToGrid()
    {
        _preferences.Canvas.SnapToGrid = !_preferences.Canvas.SnapToGrid;
        ApplyGridPreferences();
    }

    private void ApplyGridPreferences()
    {
        SavePreferences();
        ApplyPreferencesToRenderer();
        SyncGridMenuState();
        MainCanvas.Invalidate();
    }

    private void AddBalloonAtCursorOrViewportCenter()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        if (_editorState.Mode == EditorMode.EditText)
        {
            _editorState.ExitTextEditMode(saveChanges: true);
        }

        Point2 worldPos;
        if (_isPointerOverCanvas)
        {
            worldPos = _editorState.ViewTransform.ScreenToWorld(_lastPointerPosition);
        }
        else
        {
            var centerScreen = new Point2((float)(MainCanvas.ActualWidth / 2), (float)(MainCanvas.ActualHeight / 2));
            worldPos = _editorState.ViewTransform.ScreenToWorld(centerScreen);
        }

        CreateBalloonAtPosition(worldPos);
    }

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await LoadImageFromFileAsync(file);
        }
    }

    private async void ImportFloatingImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            await CreateFloatingImageFromFileAsync(file, GetDefaultPasteWorldPosition());
            SetStatusMessage(LF("image.status.imported_decoration", file.Name));
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("persist.status.error_loading", ex.Message));
            StartupLogger.Log("ImportFloatingImage_Click failed", ex);
        }
    }

    private async void AddImageFromUrl_Click(object sender, RoutedEventArgs e)
    {
        var urlBox = new TextBox
        {
            PlaceholderText = L("tools.dialog.image_url_placeholder"),
            MinWidth = 420
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = L("tools.dialog.image_url_prompt"),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(urlBox);

        var dialog = new ContentDialog
        {
            Title = L("menu.objects.add_image_from_url"),
            Content = panel,
            PrimaryButtonText = L("common.import"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var rawUrl = (urlBox.Text ?? string.Empty).Trim();
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            SetStatusMessage(L("image.status.invalid_url"));
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Letterist/1.0");
            request.Headers.Accept.ParseAdd("image/*,*/*;q=0.8");

            using var response = await ImageUrlClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"URL returned '{mediaType}', not an image.");
            }

            var extension = ResolveImageUrlExtension(uri, mediaType);
            var importDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Letterist",
                "imports");
            Directory.CreateDirectory(importDir);

            var fileName = $"url-image-{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
            var filePath = Path.Combine(importDir, fileName);

            await using (var sourceStream = await response.Content.ReadAsStreamAsync())
            await using (var targetStream = File.Create(filePath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            var file = await StorageFile.GetFileFromPathAsync(filePath);
            await CreateFloatingImageFromFileAsync(file, GetDefaultPasteWorldPosition(), rawUrl);
            SetStatusMessage(LF("image.status.imported_decoration", file.Name));
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("persist.status.error_loading", ex.Message));
            StartupLogger.Log($"AddImageFromUrl_Click failed for URL '{rawUrl}'", ex);
        }
    }

    private static string ResolveImageUrlExtension(Uri uri, string? mediaType)
    {
        var pathExt = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(pathExt))
        {
            var normalized = pathExt.ToLowerInvariant();
            if (normalized is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp" or ".svg")
            {
                return normalized;
            }
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            if (mediaType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
            if (mediaType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("jpg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
            if (mediaType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return ".gif";
            if (mediaType.Contains("bmp", StringComparison.OrdinalIgnoreCase)) return ".bmp";
            if (mediaType.Contains("tiff", StringComparison.OrdinalIgnoreCase)) return ".tif";
            if (mediaType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
            if (mediaType.Contains("svg", StringComparison.OrdinalIgnoreCase)) return ".svg";
        }

        return ".png";
    }

    private async void BatchImportImages_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.FileTypeFilter.Add("*");
        folderPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder == null) return;

        var files = await GetImportableImageFilesAsync(folder, includeSvg: true);
        if (files.Count == 0)
        {
            SetStatusMessage(L("image.error.no_importable"));
            return;
        }

        var infoText = new TextBlock
        {
            Text = LF("image.status.batch_import_format", files.Count, folder.Name),
            TextWrapping = TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            Title = L("tools.dialog.batch_import"),
            Content = infoText,
            PrimaryButtonText = L("common.import"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            var imported = await ImportImagesAsFloatingImagesAsync(files);
            if (imported > 0)
            {
                SetStatusMessage(LF("image.status.imported_floating_images", imported));
            }
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("image.error.batch_import", ex.Message));
        }
    }

    private async void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        await PasteImageFromClipboardAsync();
    }

    private void ClearBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (_editorState.BackgroundImage == null &&
            string.IsNullOrEmpty(page.BackgroundImagePath))
        {
            SetStatusMessage(L("image.error.no_background"));
            return;
        }

        _editorState.Execute(new SetPageBackgroundImageCommand(page.Id, null));
        _editorState.SetBackgroundImageForPage(page.Id, null);
        MainCanvas.Invalidate();
        RefreshPageSetup();
        SetStatusMessage(L("image.status.background_cleared"));
    }

    private void NewDocument_Click(object sender, RoutedEventArgs e)
    {
        ReleaseTemporaryDocumentFolder();
        CreateNewDocumentWithPreferences(L("app.untitled"));
        _currentDocumentFolderPath = null;
        _currentDocumentPackagePath = null;
        _currentDocumentFolderIsTemporary = false;
        _currentDocumentIsAutosave = false;
        _pasteOffsetIndex = 0;
        MainCanvas.Invalidate();
    }

    private async void OpenDocument_Click(object sender, RoutedEventArgs e)
    {
        var result = await PickDocumentToOpenAsync();
        if (result == null) return;

        var (path, isPackage) = result.Value;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (isPackage)
            {
                await LoadDocumentFromPackageAsync(path);
            }
            else
            {
                await LoadDocumentFromFolderAsync(path);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = LF("document.error.open_failed", ex.Message);
        }
    }


    private async void SaveDocument_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsync(saveAs: false);
    }

    private async void SaveAsDocument_Click(object sender, RoutedEventArgs e)
    {
        await SaveDocumentAsync(saveAs: true);
    }

    private async void ExportDocument_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ExportDocumentAsync();
        }
        catch (Exception ex)
        {
            StartupLogger.Log("ExportDocument_Click failed", ex);
            StatusText.Text = L("export.status.failed");
        }
    }

    private void DocumentSettings_Click(object sender, RoutedEventArgs e)
    {
        ShowDocumentSettingsDialog();
    }

    private void ShowDocumentSettingsDialog()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        if (doc.ActivePage == null) return;

        var panel = new StackPanel { Spacing = 12, MinWidth = 300 };

        var nameBox = new TextBox
        {
            Header = L("tools.docsettings.name"),
            Text = doc.Name,
            PlaceholderText = L("tools.docsettings.name_placeholder")
        };
        panel.Children.Add(nameBox);

        var sizePanel = new StackPanel { Spacing = 4 };
        sizePanel.Children.Add(new TextBlock { Text = L("tools.docsettings.default_page_size"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        sizePanel.Children.Add(new TextBlock
        {
            Text = L("tools.docsettings.page_size_hint"),
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 140, 140, 140)),
            FontSize = 11
        });

        var useCurrentPageButton = new Button
        {
            Content = L("tools.docsettings.use_current_page"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 4, 10, 4)
        };
        sizePanel.Children.Add(useCurrentPageButton);

        var sizeGrid = new Grid { ColumnSpacing = 8 };
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var widthBox = new NumberBox { Header = L("common.width"), Value = doc.DefaultPageSize.Width, Minimum = 100, Maximum = 10000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var heightBox = new NumberBox { Header = L("common.height"), Value = doc.DefaultPageSize.Height, Minimum = 100, Maximum = 10000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        Grid.SetColumn(heightBox, 1);
        sizeGrid.Children.Add(widthBox);
        sizeGrid.Children.Add(heightBox);
        sizePanel.Children.Add(sizeGrid);

        var sizePresetCombo = new ComboBox { Header = L("page.header.preset"), HorizontalAlignment = HorizontalAlignment.Stretch };
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.custom"), Tag = "custom" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.us_letter"), Tag = "2550x3300" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.a4"), Tag = "2480x3508" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.us_comic"), Tag = "1988x3075" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.manga_b5"), Tag = "2150x3035" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.full_hd"), Tag = "1920x1080" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.hd"), Tag = "1280x720" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.instagram"), Tag = "1080x1080" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.social_media"), Tag = "1200x628" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.square"), Tag = "1200x1200" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.3_panel"), Tag = "2400x800" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.4_panel"), Tag = "3200x800" });
        sizePresetCombo.Items.Add(new ComboBoxItem { Content = L("page.preset.webtoon"), Tag = "800x2400" });
        sizePresetCombo.SelectedIndex = 0;
        sizePresetCombo.SelectionChanged += (_, _) =>
        {
            if (sizePresetCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag || tag == "custom") return;
            var parts = tag.Split('x');
            if (parts.Length == 2 && double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h))
            {
                widthBox.Value = w;
                heightBox.Value = h;
            }
        };
        sizePanel.Children.Add(sizePresetCombo);

        panel.Children.Add(sizePanel);

        useCurrentPageButton.Click += (_, _) =>
        {
            var activePage = _editorState.Document?.ActivePage;
            if (activePage == null) return;

            widthBox.Value = activePage.Size.Width;
            heightBox.Value = activePage.Size.Height;
        };

        var dpiBox = new NumberBox
        {
            Header = L("tools.docsettings.export_dpi"),
            Value = doc.DefaultDpi,
            Minimum = 72,
            Maximum = 1200,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        panel.Children.Add(dpiBox);

        var unitsCombo = new ComboBox { Header = L("tools.docsettings.default_units"), HorizontalAlignment = HorizontalAlignment.Stretch };
        unitsCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.unit_pixels"), Tag = "px" });
        unitsCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.unit_inches"), Tag = "in" });
        unitsCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.unit_centimeters"), Tag = "cm" });
        unitsCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.unit_millimeters"), Tag = "mm" });
        for (int i = 0; i < unitsCombo.Items.Count; i++)
        {
            if (unitsCombo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == doc.DefaultUnits)
            {
                unitsCombo.SelectedIndex = i;
                break;
            }
        }
        if (unitsCombo.SelectedIndex < 0) unitsCombo.SelectedIndex = 0;
        panel.Children.Add(unitsCombo);

        var panelBoundaryVisibilityCombo = new ComboBox
        {
            Header = L("props.label.panel_boundaries"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panelBoundaryVisibilityCombo.Items.Add(new ComboBoxItem { Content = L("guide.panel_boundary.always"), Tag = PanelBoundaryVisibilityMode.Always.ToString() });
        panelBoundaryVisibilityCombo.Items.Add(new ComboBoxItem { Content = L("guide.panel_boundary.layout_only"), Tag = PanelBoundaryVisibilityMode.LayoutOnly.ToString() });
        panelBoundaryVisibilityCombo.Items.Add(new ComboBoxItem { Content = L("guide.panel_boundary.hover"), Tag = PanelBoundaryVisibilityMode.Hover.ToString() });
        panelBoundaryVisibilityCombo.Items.Add(new ComboBoxItem { Content = L("guide.panel_boundary.hidden"), Tag = PanelBoundaryVisibilityMode.Hidden.ToString() });
        foreach (var item in panelBoundaryVisibilityCombo.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString(), _editorState.PanelBoundaryVisibilityMode.ToString(), StringComparison.Ordinal))
            {
                panelBoundaryVisibilityCombo.SelectedItem = comboItem;
                break;
            }
        }
        if (panelBoundaryVisibilityCombo.SelectedIndex < 0) panelBoundaryVisibilityCombo.SelectedIndex = 0;
        panel.Children.Add(panelBoundaryVisibilityCombo);

        var readingDirectionCombo = new ComboBox
        {
            Header = L("props.label.reading_direction"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        readingDirectionCombo.Items.Add(new ComboBoxItem { Content = L("guide.reading_dir.ltr"), Tag = ReadingDirection.LeftToRight.ToString() });
        readingDirectionCombo.Items.Add(new ComboBoxItem { Content = L("guide.reading_dir.rtl"), Tag = ReadingDirection.RightToLeft.ToString() });
        readingDirectionCombo.Items.Add(new ComboBoxItem { Content = L("guide.reading_dir.manual"), Tag = ReadingDirection.Manual.ToString() });
        var activePage = doc.ActivePage;
        if (activePage != null)
        {
            foreach (var item in readingDirectionCombo.Items)
            {
                if (item is ComboBoxItem comboItem &&
                    string.Equals(comboItem.Tag?.ToString(), activePage.ReadingDirection.ToString(), StringComparison.Ordinal))
                {
                    readingDirectionCombo.SelectedItem = comboItem;
                    break;
                }
            }
        }
        if (readingDirectionCombo.SelectedIndex < 0) readingDirectionCombo.SelectedIndex = 0;
        panel.Children.Add(readingDirectionCombo);

        var defaultBackgroundPanel = new StackPanel { Spacing = 6 };
        defaultBackgroundPanel.Children.Add(new TextBlock
        {
            Text = L("tools.docsettings.default_background"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var defaultBackgroundColorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var defaultBackgroundColorPreview = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 85, 85, 85)),
            BorderThickness = new Thickness(1)
        };
        var defaultBackgroundColorCombo = new ComboBox { Width = 150 };
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_white"), Tag = "#FFFFFF" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_offwhite"), Tag = "#FFFEF5" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_lightgray"), Tag = "#F0F0F0" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_gray"), Tag = "#CCCCCC" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_darkgray"), Tag = "#666666" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_black"), Tag = "#000000" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_lightblue"), Tag = "#E8F4FF" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_cream"), Tag = "#FFFAEB" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_transparent"), Tag = "Transparent" });
        defaultBackgroundColorCombo.Items.Add(new ComboBoxItem { Content = L("tools.docsettings.color_custom"), Tag = "CUSTOM" });
        defaultBackgroundColorRow.Children.Add(defaultBackgroundColorPreview);
        defaultBackgroundColorRow.Children.Add(defaultBackgroundColorCombo);
        defaultBackgroundPanel.Children.Add(defaultBackgroundColorRow);

        var defaultBackgroundImageStatus = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 140, 140, 140))
        };
        defaultBackgroundPanel.Children.Add(defaultBackgroundImageStatus);

        var defaultBackgroundImageButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var chooseDefaultBackgroundImageButton = new Button
        {
            Content = L("tools.docsettings.choose_image"),
            Padding = new Thickness(10, 4, 10, 4)
        };
        var clearDefaultBackgroundImageButton = new Button
        {
            Content = L("common.clear"),
            Padding = new Thickness(10, 4, 10, 4)
        };
        defaultBackgroundImageButtons.Children.Add(chooseDefaultBackgroundImageButton);
        defaultBackgroundImageButtons.Children.Add(clearDefaultBackgroundImageButton);
        defaultBackgroundPanel.Children.Add(defaultBackgroundImageButtons);
        panel.Children.Add(defaultBackgroundPanel);

        Model.Color? selectedDefaultBackgroundColor = doc.DefaultPageBackgroundColor;
        string? selectedDefaultBackgroundImagePath = doc.DefaultPageBackgroundImagePath;
        var isUpdatingDefaultBackgroundColor = false;

        bool SelectDefaultBackgroundColorByTag(string tag)
        {
            foreach (var item in defaultBackgroundColorCombo.Items)
            {
                if (item is ComboBoxItem comboItem && comboItem.Tag is string itemTag &&
                    string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    defaultBackgroundColorCombo.SelectedItem = item;
                    return true;
                }
            }

            return false;
        }

        void UpdateDefaultBackgroundPreview(Model.Color? color)
        {
            if (!color.HasValue)
            {
                defaultBackgroundColorPreview.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                return;
            }

            var c = color.Value;
            defaultBackgroundColorPreview.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B));
        }

        void SetDefaultBackgroundColorSelection(Model.Color? color)
        {
            isUpdatingDefaultBackgroundColor = true;
            if (!color.HasValue)
            {
                SelectDefaultBackgroundColorByTag("Transparent");
            }
            else
            {
                var hex = $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
                if (!SelectDefaultBackgroundColorByTag(hex))
                {
                    SelectDefaultBackgroundColorByTag("CUSTOM");
                }
            }

            UpdateDefaultBackgroundPreview(color);
            isUpdatingDefaultBackgroundColor = false;
        }

        void UpdateDefaultBackgroundImageStatus()
        {
            defaultBackgroundImageStatus.Text = string.IsNullOrWhiteSpace(selectedDefaultBackgroundImagePath)
                ? "No default image"
                : System.IO.Path.GetFileName(selectedDefaultBackgroundImagePath);
        }

        SetDefaultBackgroundColorSelection(selectedDefaultBackgroundColor);
        UpdateDefaultBackgroundImageStatus();

        defaultBackgroundColorCombo.SelectionChanged += async (_, _) =>
        {
            if (isUpdatingDefaultBackgroundColor) return;
            if (defaultBackgroundColorCombo.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not string selectedTag)
            {
                return;
            }

            if (selectedTag == "Transparent")
            {
                selectedDefaultBackgroundColor = null;
                UpdateDefaultBackgroundPreview(selectedDefaultBackgroundColor);
                return;
            }

            if (selectedTag == "CUSTOM")
            {
                var currentColor = selectedDefaultBackgroundColor ?? Model.Color.White;
                var pickedColor = await ShowColorPickerDialogAsync(currentColor);
                if (pickedColor.HasValue)
                {
                    selectedDefaultBackgroundColor = pickedColor.Value;
                }

                SetDefaultBackgroundColorSelection(selectedDefaultBackgroundColor);
                return;
            }

            selectedDefaultBackgroundColor = ParseHexColor(selectedTag);
            UpdateDefaultBackgroundPreview(selectedDefaultBackgroundColor);
        };

        chooseDefaultBackgroundImageButton.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            AddSupportedImageFileTypes(picker, includeSvg: true);
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            selectedDefaultBackgroundImagePath = file.Path;
            UpdateDefaultBackgroundImageStatus();
        };

        clearDefaultBackgroundImageButton.Click += (_, _) =>
        {
            selectedDefaultBackgroundImagePath = null;
            UpdateDefaultBackgroundImageStatus();
        };

        useCurrentPageButton.Click += (_, _) =>
        {
            var activePage = _editorState.Document?.ActivePage;
            if (activePage == null) return;

            selectedDefaultBackgroundColor = activePage.BackgroundColor;
            selectedDefaultBackgroundImagePath = activePage.BackgroundImagePath;
            SetDefaultBackgroundColorSelection(selectedDefaultBackgroundColor);
            UpdateDefaultBackgroundImageStatus();
        };

        var okButton = new Button
        {
            Content = L("common.apply"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            MinWidth = 80
        };
        var cancelButton = new Button
        {
            Content = L("common.cancel"),
            MinWidth = 80
        };

        var dialogButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        dialogButtons.Children.Add(okButton);
        dialogButtons.Children.Add(cancelButton);
        panel.Children.Add(dialogButtons);

        var contentRoot = new ScrollViewer
        {
            Content = panel,
            Padding = new Thickness(16),
            RequestedTheme = ElementTheme.Dark
        };

        var docSettingsWindow = new Window { Title = L("tools.docsettings.title") };
        docSettingsWindow.Content = contentRoot;

        var appWindow = docSettingsWindow.AppWindow;
        if (appWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter)
        {
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);
        }
        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter docPresenter)
        {
            docPresenter.IsResizable = true;
            docPresenter.SetBorderAndTitleBar(true, true);
        }

        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 1080));
        TryApplyFontChooserTitleBarTheme(appWindow);
        CenterChildWindowOverMainWindow(appWindow);

        okButton.Click += (_, _) =>
        {
            var newName = nameBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != doc.Name)
            {
                _editorState.Execute(new SetDocumentNameCommand(newName));
            }

            var newWidth = (float)widthBox.Value;
            var newHeight = (float)heightBox.Value;
            if (Math.Abs(newWidth - doc.DefaultPageSize.Width) > 0.5f || Math.Abs(newHeight - doc.DefaultPageSize.Height) > 0.5f)
            {
                _editorState.Execute(new SetDocumentDefaultPageSizeCommand(new Size2(newWidth, newHeight)));
            }

            var newDpi = (float)dpiBox.Value;
            if (Math.Abs(newDpi - doc.DefaultDpi) > 0.5f)
            {
                _editorState.Execute(new SetDocumentDpiCommand(newDpi));
            }

            if (unitsCombo.SelectedItem is ComboBoxItem selectedUnits && selectedUnits.Tag is string units && units != doc.DefaultUnits)
            {
                _editorState.Execute(new SetDocumentUnitsCommand(units));
            }

            if (panelBoundaryVisibilityCombo.SelectedItem is ComboBoxItem selectedBoundaryItem &&
                selectedBoundaryItem.Tag is string boundaryTag &&
                Enum.TryParse<PanelBoundaryVisibilityMode>(boundaryTag, out var boundaryMode) &&
                boundaryMode != _editorState.PanelBoundaryVisibilityMode)
            {
                _editorState.PanelBoundaryVisibilityMode = boundaryMode;
            }

            var currentPage = doc.ActivePage;
            if (currentPage != null &&
                readingDirectionCombo.SelectedItem is ComboBoxItem selectedDirectionItem &&
                selectedDirectionItem.Tag is string directionTag &&
                Enum.TryParse<ReadingDirection>(directionTag, out var direction) &&
                currentPage.ReadingDirection != direction)
            {
                _editorState.Execute(new SetPageReadingDirectionCommand(currentPage.Id, direction));
            }

            if (doc.DefaultPageBackgroundColor != selectedDefaultBackgroundColor)
            {
                _editorState.Execute(new SetDocumentDefaultBackgroundColorCommand(selectedDefaultBackgroundColor));
            }

            if (!string.Equals(doc.DefaultPageBackgroundImagePath, selectedDefaultBackgroundImagePath, StringComparison.OrdinalIgnoreCase))
            {
                _editorState.Execute(new SetDocumentDefaultBackgroundImageCommand(selectedDefaultBackgroundImagePath));
            }

            RefreshPageSetup();
            MainCanvas.Invalidate();
            SetStatusMessage(L("document.status.settings_updated"));
            try { docSettingsWindow.Close(); } catch { }
        };

        cancelButton.Click += (_, _) =>
        {
            try { docSettingsWindow.Close(); } catch { }
        };

        AttachEscapeToCloseWindow(docSettingsWindow, contentRoot);
        docSettingsWindow.Activate();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => UndoCommandAndRefreshVisuals();

    private void Redo_Click(object sender, RoutedEventArgs e) => RedoCommandAndRefreshVisuals();

    private void UndoCommandAndRefreshVisuals()
    {
        if (!_editorState.Undo())
        {
            return;
        }

        UpdateToolButtonStates();
        _ = RefreshActivePageVisualAssetsAfterHistoryChangeAsync();
    }

    private void RedoCommandAndRefreshVisuals()
    {
        if (!_editorState.Redo())
        {
            return;
        }

        UpdateToolButtonStates();
        _ = RefreshActivePageVisualAssetsAfterHistoryChangeAsync();
    }

    private async Task RefreshActivePageVisualAssetsAfterHistoryChangeAsync()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            MainCanvas.Invalidate();
            return;
        }

        await EnsureBackgroundLoadedAsync(page);
        await EnsureFloatingImagesLoadedAsync(page);
        MainCanvas.Invalidate();
    }

    private void Cut_Click(object sender, RoutedEventArgs e)
    {
        CutSelectedBalloonsToClipboard();
        UpdateToolButtonStates();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedBalloonsToClipboard();
        UpdateToolButtonStates();
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        await PasteBalloonsFromClipboardAsync();
        UpdateToolButtonStates();
    }

    private void CutBalloons_Click(object sender, RoutedEventArgs e)
    {
        CutSelectedBalloonsToClipboard();
    }

    private void CopyBalloons_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedBalloonsToClipboard();
    }

    private async void PasteBalloons_Click(object sender, RoutedEventArgs e)
    {
        await PasteBalloonsFromClipboardAsync();
    }

    private void DuplicateBalloons_Click(object sender, RoutedEventArgs e)
    {
        TryDuplicateFromCurrentContext();
    }

    private void GroupObjects_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var groupingState = GetObjectGroupingState();
        if (!groupingState.CanGroup)
        {
            SetStatusMessage(L("objects.status.group_requires_two"));
            return;
        }

        var balloonIds = _editorState.SelectedBalloonIds.ToList();
        var floatingImageIds = _editorState.SelectedFloatingImageIds.ToList();
        _editorState.Execute(new GroupObjectsCommand(page.Id, balloonIds, floatingImageIds));
        SetStatusMessage(L("objects.status.grouped"));
    }

    private void UngroupObjects_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var groupingState = GetObjectGroupingState();
        if (!groupingState.CanUngroup)
        {
            SetStatusMessage(L("objects.status.no_group_selection"));
            return;
        }

        var balloonIds = _editorState.SelectedBalloonIds.ToList();
        var floatingImageIds = _editorState.SelectedFloatingImageIds.ToList();
        _editorState.Execute(new UngroupObjectsCommand(page.Id, balloonIds, floatingImageIds));
        SetStatusMessage(L("objects.status.ungrouped"));
    }

    private (bool CanGroup, bool CanUngroup) GetObjectGroupingState()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            return (false, false);
        }

        var balloonIds = _editorState.SelectedBalloonIds.ToList();
        var imageIds = _editorState.SelectedFloatingImageIds.ToList();
        var selectionCount = balloonIds.Count + imageIds.Count;
        if (selectionCount == 0)
        {
            return (false, false);
        }

        var hasGroupedSelection =
            balloonIds.Any(id => page.FindObjectGroupByBalloon(id) != null) ||
            imageIds.Any(id => page.FindObjectGroupByFloatingImage(id) != null);

        var canUngroup = hasGroupedSelection;
        var canGroup = !hasGroupedSelection && selectionCount >= 2;
        return (canGroup, canUngroup);
    }

    private void UpdateObjectGroupingMenuState()
    {
        var groupingState = GetObjectGroupingState();
        MenuObjectsGroup.IsEnabled = groupingState.CanGroup;
        MenuObjectsUngroup.IsEnabled = groupingState.CanUngroup;
    }

    private void SelectAllBalloons_Click(object sender, RoutedEventArgs e)
    {
        SelectAllBalloonsOnActiveLayer();
    }

    private async void FindText_Click(object sender, RoutedEventArgs e)
    {
        await ShowFindReplaceDialogAsync(replaceMode: false);
    }

    private async void ReplaceText_Click(object sender, RoutedEventArgs e)
    {
        await ShowFindReplaceDialogAsync(replaceMode: true);
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsLeft();
            return;
        }

        AlignSelectedLeft();
    }

    private void AlignCenter_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsCenter();
            return;
        }

        AlignSelectedCenter();
    }

    private void AlignRight_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsRight();
            return;
        }

        AlignSelectedRight();
    }

    private void AlignTop_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsTop();
            return;
        }

        AlignSelectedTop();
    }

    private void AlignMiddle_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsMiddle();
            return;
        }

        AlignSelectedMiddle();
    }

    private void AlignBottom_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            AlignPanelsBottom();
            return;
        }

        AlignSelectedBottom();
    }

    private void DistributeHorizontally_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            DistributePanelsHorizontally();
            return;
        }

        DistributeSelectedHorizontally();
    }

    private void DistributeVertically_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            DistributePanelsVertically();
            return;
        }

        DistributeSelectedVertically();
    }

    private void PanelAlignLeft_Click(object sender, RoutedEventArgs e) => AlignPanelsLeft();

    private void PanelAlignCenter_Click(object sender, RoutedEventArgs e) => AlignPanelsCenter();

    private void PanelAlignRight_Click(object sender, RoutedEventArgs e) => AlignPanelsRight();

    private void PanelAlignTop_Click(object sender, RoutedEventArgs e) => AlignPanelsTop();

    private void PanelAlignMiddle_Click(object sender, RoutedEventArgs e) => AlignPanelsMiddle();

    private void PanelAlignBottom_Click(object sender, RoutedEventArgs e) => AlignPanelsBottom();

    private void PanelDistributeHorizontally_Click(object sender, RoutedEventArgs e) => DistributePanelsHorizontally();

    private void PanelDistributeVertically_Click(object sender, RoutedEventArgs e) => DistributePanelsVertically();

    private void PanelMatchWidth_Click(object sender, RoutedEventArgs e) => MatchPanelWidths();

    private void PanelMatchHeight_Click(object sender, RoutedEventArgs e) => MatchPanelHeights();

    private void PanelMatchSize_Click(object sender, RoutedEventArgs e) => MatchPanelSizes();

    private void LinkBalloons_Click(object sender, RoutedEventArgs e)
    {
        LinkSelectedBalloons();
    }

    private void UnlinkBalloons_Click(object sender, RoutedEventArgs e)
    {
        UnlinkSelectedBalloons();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        TryDeleteFromCurrentContext();
        UpdateToolButtonStates();
    }

    private bool TryDuplicateFromCurrentContext()
    {
        if (string.Equals(_activeLeftSidebarTab, "Pages", StringComparison.Ordinal))
        {
            DuplicatePage_Click(this, new RoutedEventArgs());
            return true;
        }

        if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelId.HasValue ||
            _editorState.SelectedPanelIds.Count > 0)
        {
            DuplicateSelectedPanel();
            return true;
        }

        if (_editorState.SelectedBalloonIds.Count > 0 || _editorState.SelectedFloatingImageIds.Count > 0)
        {
            DuplicateSelectedBalloons();
            return true;
        }

        return false;
    }

    private bool TryDeleteFromCurrentContext()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            return false;
        }

        if (string.Equals(_activeLeftSidebarTab, "Pages", StringComparison.Ordinal) &&
            PageListView?.SelectedItem is PageViewModel &&
            doc.Pages.Count > 1)
        {
            DeletePage_Click(this, new RoutedEventArgs());
            return true;
        }

        var hasSelectedObjects = _editorState.SelectedBalloonIds.Count > 0 || _editorState.SelectedFloatingImageIds.Count > 0;
        var hasSelectedPanels = _editorState.SelectedPanelIds.Count > 0;

        if (_editorState.Mode != EditorMode.PanelLayout && hasSelectedObjects)
        {
            DeleteSelectedBalloon();
            return true;
        }

        if (hasSelectedPanels)
        {
            DeleteSelectedPanel();
            return true;
        }

        if (LayerListView.SelectedItem is LayerViewModel or LayerGroupViewModel)
        {
            DeleteLayer_Click(this, new RoutedEventArgs());
            return true;
        }

        if (hasSelectedObjects)
        {
            DeleteSelectedBalloon();
            return true;
        }

        return false;
    }

    private enum MultiDeleteItemKind
    {
        Objects,
        Layers,
        Panels,
        Pages
    }

    private async Task<bool> ConfirmMultiDeleteAsync(MultiDeleteItemKind kind, int count)
    {
        if (count <= 1)
        {
            return true;
        }

        var promptKey = kind switch
        {
            MultiDeleteItemKind.Objects => "delete.dialog.multi_objects",
            MultiDeleteItemKind.Layers => "delete.dialog.multi_layers",
            MultiDeleteItemKind.Panels => "delete.dialog.multi_panels",
            MultiDeleteItemKind.Pages => "delete.dialog.multi_pages",
            _ => "delete.dialog.multi_objects"
        };

        var dialog = new ContentDialog
        {
            Title = L("delete.dialog.multi_title"),
            Content = new TextBlock
            {
                Text = LF(promptKey, count),
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = L("common.delete"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void AboutMenu_Click(object sender, RoutedEventArgs e)
    {
        var versionText = LF("help.about.version", GetApplicationVersion());
        var licenseText = LoadBundledLicenseText(out var loadedLicensePath);
        var aboutPanel = new StackPanel { Spacing = 10 };
        aboutPanel.Children.Add(new TextBlock
        {
            Text = $"{L("help.about.message")}\n\n{versionText}",
            TextWrapping = TextWrapping.Wrap
        });
        aboutPanel.Children.Add(new TextBlock
        {
            Text = L("help.about.license_header"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170))
        });
        if (!string.IsNullOrWhiteSpace(loadedLicensePath))
        {
            aboutPanel.Children.Add(new TextBlock
            {
                Text = loadedLicensePath,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 135, 135, 135))
            });
        }
        var licenseViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = licenseText,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 210, 210, 210)),
                Margin = new Thickness(8)
            }
        };
        aboutPanel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 72, 72, 72)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0),
            Height = 220,
            Child = licenseViewer
        });

        var dialog = new ContentDialog
        {
            Title = L("help.about.title"),
            PrimaryButtonText = L("common.ok"),
            Content = aboutPanel,
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private string LoadBundledLicenseText(out string? loadedPath)
    {
        foreach (var candidate in EnumerateLicensePathCandidates())
        {
            try
            {
                if (File.Exists(candidate))
                {
                    loadedPath = candidate;
                    return NormalizeLicenseTextForDisplay(File.ReadAllText(candidate));
                }
            }
            catch
            {
            }
        }

        loadedPath = null;
        var attempted = string.Join(Environment.NewLine, EnumerateLicensePathCandidates().Take(6));
        return NormalizeLicenseTextForDisplay($"{L("help.about.license_missing")}{Environment.NewLine}{Environment.NewLine}{attempted}");
    }

    private static string NormalizeLicenseTextForDisplay(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\0", string.Empty)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
    }

    private static IEnumerable<string> EnumerateLicensePathCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static string Normalize(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        var baseDirectory = AppContext.BaseDirectory;

        IEnumerable<string> BuildCandidates()
        {
            yield return Path.Combine(baseDirectory, "LICENSE.txt");
            yield return Path.Combine(Environment.CurrentDirectory, "LICENSE.txt");

            var probe = baseDirectory;
            for (var i = 0; i < 8; i++)
            {
                probe = Path.Combine(probe, "..");
                yield return Path.Combine(probe, "LICENSE.txt");
            }
        }

        foreach (var path in BuildCandidates())
        {
            var normalized = Normalize(path);
            if (!seen.Add(normalized)) continue;
            yield return normalized;
        }
    }

    private static string GetApplicationVersion()
    {
        var info = typeof(MainWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(info))
        {
            var clean = info.Split('+')[0].Trim();
            if (!string.IsNullOrWhiteSpace(clean))
            {
                return clean;
            }
        }

        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version == null)
        {
            return "1.0.0";
        }

        var patch = version.Build < 0 ? 0 : version.Build;
        return $"{version.Major}.{version.Minor}.{patch}";
    }

    private void ZoomToSelection_Click(object sender, RoutedEventArgs e)
    {
        ZoomToSelection();
    }

    private void ZoomToSelection()
    {
        if (_editorState.Document == null) return;

        Rect? bounds = null;

        var balloonBounds = _editorState.GetSelectionBounds();
        if (balloonBounds.HasValue)
        {
            bounds = balloonBounds;
        }

        foreach (var imageId in _editorState.SelectedFloatingImageIds)
        {
            var image = _editorState.Document.FindFloatingImage(imageId);
            if (image == null) continue;
            bounds = bounds.HasValue ? bounds.Value.Union(image.Bounds) : image.Bounds;
        }

        var panelBounds = _editorState.GetPanelSelectionBounds();
        if (panelBounds.HasValue)
        {
            bounds = bounds.HasValue ? bounds.Value.Union(panelBounds.Value) : panelBounds;
        }

        if (bounds.HasValue)
        {
            _editorState.ViewTransform.ZoomToFit(bounds.Value, 40f);
            UpdateToolButtonStates();
        }
    }

    private void ZoomToFit_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState.Document != null)
        {
            _editorState.ViewTransform.ZoomToFit(new Rect(0, 0, _editorState.Document.Size.Width, _editorState.Document.Size.Height));
            UpdateToolButtonStates();
        }
    }

    private void Zoom100_Click(object sender, RoutedEventArgs e)
    {
        _editorState.ViewTransform.ZoomTo100();
        UpdateToolButtonStates();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        var center = new Point2(_editorState.ViewTransform.ViewportSize.Width / 2,
                                _editorState.ViewTransform.ViewportSize.Height / 2);
        _editorState.ViewTransform.ZoomAt(1.25f, center);
        UpdateToolButtonStates();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        var center = new Point2(_editorState.ViewTransform.ViewportSize.Width / 2,
                                _editorState.ViewTransform.ViewportSize.Height / 2);
        _editorState.ViewTransform.ZoomAt(0.8f, center);
        UpdateToolButtonStates();
    }

    private void ToolbarZoomBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitToolbarZoomInput();
            BlurToolbarZoomBox();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            UpdateStatusBar();
            e.Handled = true;
        }
    }

    private void ToolbarZoomBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitToolbarZoomInput();
    }

    private void CommitToolbarZoomInput()
    {
        if (ToolbarZoomBox == null || _isUpdatingToolbarZoomBox) return;
        if (_editorState.Document == null)
        {
            UpdateStatusBar();
            return;
        }

        var raw = ToolbarZoomBox.Text?.Trim() ?? string.Empty;
        if (raw.EndsWith("%", StringComparison.Ordinal))
        {
            raw = raw.Substring(0, raw.Length - 1).Trim();
        }

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) &&
            !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out percent))
        {
            UpdateStatusBar();
            return;
        }

        percent = Math.Clamp(percent, 5f, 3200f);
        var currentZoomPercent = _editorState.ViewTransform.ZoomPercent;
        if (Math.Abs(percent - currentZoomPercent) < 0.05f)
        {
            UpdateStatusBar();
            return;
        }

        var targetZoom = percent / 100f;
        var factor = targetZoom / _editorState.ViewTransform.Zoom;
        if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
        {
            UpdateStatusBar();
            return;
        }

        var center = new Point2(
            _editorState.ViewTransform.ViewportSize.Width / 2f,
            _editorState.ViewTransform.ViewportSize.Height / 2f);
        _editorState.ViewTransform.ZoomAt(factor, center);
        MainCanvas.Invalidate();
        UpdateToolButtonStates();
    }

    private void BlurToolbarZoomBox()
    {
        if (MainCanvas.Focus(FocusState.Programmatic))
        {
            return;
        }

        _ = RootGrid.Focus(FocusState.Programmatic);
    }


}
