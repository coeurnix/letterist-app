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
using Microsoft.UI.Xaml.Input;
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

    private static readonly string[] SupportedRasterImageExtensions =
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".webp"
    };

    private static readonly string[] SupportedImageImportExtensions =
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif", ".webp", ".svg"
    };

    private static void AddSupportedImageFileTypes(FileOpenPicker picker, bool includeSvg = true)
    {
        var extensions = includeSvg ? SupportedImageImportExtensions : SupportedRasterImageExtensions;
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }
    }

    private static string NormalizeImageExtension(string extension)
    {
        var trimmed = extension?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        return trimmed.ToLowerInvariant();
    }

    private static bool IsImageFile(string extension, bool includeSvg = true)
    {
        var ext = NormalizeImageExtension(extension);
        if (string.IsNullOrWhiteSpace(ext))
        {
            return false;
        }

        if (includeSvg && ext == ".svg")
        {
            return true;
        }

        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".gif" or ".webp";
    }

    private static bool IsSvgFile(string extension)
    {
        return NormalizeImageExtension(extension) == ".svg";
    }

    private static bool IsGifFile(string extension)
    {
        return NormalizeImageExtension(extension) == ".gif";
    }

    private async Task EnsureNonAnimatedGifAsync(StorageFile file)
    {
        if (!IsGifFile(file.FileType))
        {
            return;
        }

        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        if (decoder.FrameCount > 1)
        {
            throw new InvalidOperationException("Animated GIF is not supported. Please use a non-animated GIF.");
        }
    }

    private async Task<CanvasBitmap> LoadSvgBitmapAsync(StorageFile file)
    {
        if (MainCanvas.Device == null)
        {
            throw new InvalidOperationException("Canvas device is not ready.");
        }

        var content = await FileIO.ReadTextAsync(file);
        if (!TryExtractSvgPathData(content, out var pathData) || string.IsNullOrWhiteSpace(pathData))
        {
            throw new InvalidOperationException("SVG path data was not found.");
        }

        using var geometry = SvgPathParser.TryCreateGeometry(MainCanvas.Device, pathData!)
            ?? throw new InvalidOperationException("SVG path data could not be parsed.");
        var bounds = geometry.ComputeBounds();

        var width = Math.Clamp((int)MathF.Ceiling(MathF.Max(1f, (float)bounds.Width)), 1, 8192);
        var height = Math.Clamp((int)MathF.Ceiling(MathF.Max(1f, (float)bounds.Height)), 1, 8192);

        var renderTarget = new CanvasRenderTarget(MainCanvas.Device, width, height, 96f);
        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            ds.Transform = Matrix3x2.CreateTranslation(-(float)bounds.X, -(float)bounds.Y);

            var color = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            ds.FillGeometry(geometry, color);
            ds.DrawGeometry(geometry, color, 1f);
        }

        return renderTarget;
    }

    private async Task<CanvasBitmap> LoadBitmapForImportAsync(StorageFile file)
    {
        if (MainCanvas.Device == null)
        {
            throw new InvalidOperationException("Canvas device is not ready.");
        }

        if (IsSvgFile(file.FileType))
        {
            return await LoadSvgBitmapAsync(file);
        }

        await EnsureNonAnimatedGifAsync(file);

        using var stream = await file.OpenReadAsync();
        return await CanvasBitmap.LoadAsync(MainCanvas.Device, stream);
    }

    private async Task LoadImageFromFileAsync(StorageFile file)
    {
        try
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null)
            {
                return;
            }

            var bitmap = await LoadBitmapForImportAsync(file);
            _editorState.Execute(new SetPageBackgroundImageCommand(page.Id, file.Path));
            _editorState.SetBackgroundImageForPage(page.Id, bitmap);

            StatusText.Text = LF("image.status.loaded", file.Name);
            RefreshPageSetup();
            MainCanvas.Invalidate();
        }
        catch (Exception ex)
        {
            StatusText.Text = LF("image.error.load_failed", ex.Message);
            StartupLogger.Log("LoadImageFromFileAsync failed", ex);
        }
    }

    private async Task PasteImageFromClipboardAsync()
    {
        try
        {
            var clipboard = Clipboard.GetContent();
            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (clipboard.Contains(StandardDataFormats.Bitmap))
            {
                var streamRef = await clipboard.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();
                var bitmap = await CanvasBitmap.LoadAsync(MainCanvas.Device, stream);

                var selectedPanelId = _editorState.SelectedPanelId;
                var page = _editorState.Document?.ActivePage;
                if (selectedPanelId.HasValue && page != null)
                {
                    var panel = page.FindPanel(selectedPanelId.Value);
                    if (panel != null)
                    {
                        var bounds = CreatePanelConstrainedFloatingImageBounds(
                            panel.Bounds,
                            (float)bitmap.SizeInPixels.Width,
                            (float)bitmap.SizeInPixels.Height);
                        await CreateFloatingImageFromBitmapAsync(
                            bitmap,
                            imagePath: null,
                            panel.Bounds.Center,
                            panelId: panel.Id,
                            boundsOverride: bounds);

                        MainCanvas.Invalidate();
                        StatusText.Text = L("persist.status.pasted_panel_image");
                        return;
                    }
                }

                if (shift)
                {
                    await SetBackgroundImageFromBitmapAsync(bitmap);
                    StatusText.Text = L("persist.status.pasted_background");
                }
                else
                {
                    await CreateFloatingImageFromBitmapAsync(bitmap, imagePath: null, GetDefaultPasteWorldPosition());
                    StatusText.Text = L("persist.status.pasted_floating");
                }
            }
            else if (clipboard.Contains(StandardDataFormats.StorageItems))
            {
                var items = await clipboard.GetStorageItemsAsync();
                var imageFile = items.OfType<StorageFile>()
                    .FirstOrDefault(f => IsImageFile(f.FileType));

                if (imageFile != null)
                {
                    var selectedPanelId = _editorState.SelectedPanelId;
                    var page = _editorState.Document?.ActivePage;
                    if (selectedPanelId.HasValue && page != null)
                    {
                        var panel = page.FindPanel(selectedPanelId.Value);
                        if (panel != null)
                        {
                            await CreateFloatingImageFromFileInPanelAsync(imageFile, page, panel);
                        }
                        StatusText.Text = L("persist.status.pasted_panel_image");
                    }
                    else if (shift)
                    {
                        await LoadBackgroundImageWithoutResizeAsync(imageFile);
                        StatusText.Text = L("persist.status.pasted_background");
                    }
                    else
                    {
                        await CreateFloatingImageFromFileAsync(imageFile, GetDefaultPasteWorldPosition());
                        StatusText.Text = L("persist.status.pasted_floating");
                    }
                }
            }
            else
            {
                StatusText.Text = L("persist.status.no_clipboard_image");
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = LF("image.error.paste_failed", ex.Message);
        }
    }

    private Point2 GetDefaultPasteWorldPosition()
    {
        var doc = _editorState.Document;
        if (doc == null) return new Point2(0f, 0f);

        Point2 center;
        if (_isPointerOverCanvas)
        {
            center = _editorState.ViewTransform.ScreenToWorld(_lastPointerPosition);
        }
        else
        {
            var screenCenter = new Point2((float)(MainCanvas.ActualWidth / 2), (float)(MainCanvas.ActualHeight / 2));
            center = _editorState.ViewTransform.ScreenToWorld(screenCenter);
        }

        _pasteOffsetIndex++;
        var offset = new Point2(16f * _pasteOffsetIndex, 16f * _pasteOffsetIndex);
        center += offset;
        return ClampPointToPage(center, doc.Size);
    }

    private async Task CreateFloatingImageFromFileInPanelAsync(StorageFile file, DocumentPage page, PanelZone panel)
    {
        var bitmap = await LoadBitmapForImportAsync(file);
        var bounds = CreatePanelConstrainedFloatingImageBounds(
            panel.Bounds,
            (float)bitmap.SizeInPixels.Width,
            (float)bitmap.SizeInPixels.Height);
        await CreateFloatingImageFromBitmapAsync(
            bitmap,
            file.Path,
            panel.Bounds.Center,
            source: file.Path,
            panelId: panel.Id,
            boundsOverride: bounds);
    }

    private async Task CreateFloatingImageFromFileAsync(StorageFile file, Point2 center, string? source = null, Guid? panelId = null, Rect? boundsOverride = null)
    {
        var bitmap = await LoadBitmapForImportAsync(file);
        await CreateFloatingImageFromBitmapAsync(bitmap, file.Path, center, source ?? file.Path, panelId, boundsOverride);
    }

    private async Task CreateFloatingImageFromBitmapAsync(
        CanvasBitmap bitmap,
        string? imagePath,
        Point2 center,
        string? source = null,
        Guid? panelId = null,
        Rect? boundsOverride = null)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var bounds = boundsOverride ?? CreateFloatingImageBounds(page.Size, center, bitmap);
        var command = new CreateFloatingImageCommand(
            page.Id,
            imagePath,
            bounds,
            layerId: page.ActiveLayerId,
            source: source);
        _editorState.Execute(command);
        if (panelId.HasValue)
        {
            _editorState.Execute(new SetFloatingImagePanelCommand(page.Id, command.CreatedImageId, panelId));
        }

        _floatingImageBitmaps[command.CreatedImageId] = bitmap;
        _editorState.SelectFloatingImage(command.CreatedImageId);
        MainCanvas.Invalidate();

        await Task.CompletedTask;
    }

    private async Task<int> ImportImagesAsFloatingImagesAsync(IReadOnlyList<StorageFile> files)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || files.Count == 0)
        {
            return 0;
        }

        var importCount = 0;
        var startPosition = GetDefaultPasteWorldPosition();
        var offsetStep = 30f;

        foreach (var file in files)
        {
            try
            {
                var bitmap = await LoadBitmapForImportAsync(file);
                var bounds = CreateFloatingImageBounds(page.Size, startPosition, bitmap);
                var command = new CreateFloatingImageCommand(
                    page.Id,
                    file.Path,
                    bounds,
                    layerId: page.ActiveLayerId,
                    source: file.Path);
                _editorState.Execute(command);

                _floatingImageBitmaps[command.CreatedImageId] = bitmap;
                importCount++;

                startPosition = new Point2(startPosition.X + offsetStep, startPosition.Y + offsetStep);
            }
            catch (Exception ex)
            {
                StartupLogger.Log($"Failed to import floating image '{file.Name}'", ex);
            }
        }

        if (importCount > 0)
        {
            MainCanvas.Invalidate();
        }

        return importCount;
    }

    private static Rect CreateFloatingImageBounds(Size2 pageSize, Point2 center, CanvasBitmap bitmap)
    {
        var width = (float)bitmap.SizeInPixels.Width;
        var height = (float)bitmap.SizeInPixels.Height;
        if (width <= 0f || height <= 0f)
        {
            return new Rect(center.X, center.Y, 1f, 1f);
        }

        var maxWidth = pageSize.Width > 0 ? pageSize.Width * 0.85f : width;
        var maxHeight = pageSize.Height > 0 ? pageSize.Height * 0.85f : height;
        var scale = MathF.Min(1f, MathF.Min(maxWidth / width, maxHeight / height));
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var size = new Size2(width * scale, height * scale);
        return Rect.FromCenterSize(center, size);
    }

    private static Rect CreatePanelConstrainedFloatingImageBounds(Rect panelBounds, float imageWidth, float imageHeight)
    {
        if (panelBounds.Width <= 0f || panelBounds.Height <= 0f || imageWidth <= 0f || imageHeight <= 0f)
        {
            return panelBounds;
        }

        var scale = MathF.Max(panelBounds.Width / imageWidth, panelBounds.Height / imageHeight);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var width = MathF.Max(1f, imageWidth * scale);
        var height = MathF.Max(1f, imageHeight * scale);
        return Rect.FromCenterSize(panelBounds.Center, new Size2(width, height));
    }

    private void Canvas_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems) ||
            e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;

            var screenPos = new Point2((float)e.GetPosition(MainCanvas).X, (float)e.GetPosition(MainCanvas).Y);
            var panel = _editorState.HitTestPanel(screenPos);
            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (panel != null)
            {
                e.DragUIOverride.Caption = LF("persist.drag.load_into_panel", panel.Name);
            }
            else
            {
                e.DragUIOverride.Caption = shift
                    ? L("persist.drag.load_as_background")
                    : L("persist.drag.place_floating");
            }
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private async void Canvas_Drop(object sender, DragEventArgs e)
    {
        var screenPos = new Point2((float)e.GetPosition(MainCanvas).X, (float)e.GetPosition(MainCanvas).Y);
        var worldPos = _editorState.ViewTransform.ScreenToWorld(screenPos);
        var panel = _editorState.HitTestPanel(screenPos);
        var page = _editorState.Document?.ActivePage;
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var imageFile = items.OfType<StorageFile>()
                    .FirstOrDefault(f => IsImageFile(f.FileType));

                if (imageFile != null)
                {
                    if (panel != null && page != null)
                    {
                        await CreateFloatingImageFromFileInPanelAsync(imageFile, page, panel);
                        StatusText.Text = LF("persist.status.loaded_panel", panel.Name);
                    }
                    else if (shift)
                    {
                        await LoadBackgroundImageWithoutResizeAsync(imageFile);
                    }
                    else
                    {
                        await CreateFloatingImageFromFileAsync(imageFile, worldPos);
                        StatusText.Text = L("persist.status.dropped_floating");
                    }
                }
            }
            else if (e.DataView.Contains(StandardDataFormats.Bitmap))
            {
                var streamRef = await e.DataView.GetBitmapAsync();
                using var stream = await streamRef.OpenReadAsync();
                var bitmap = await CanvasBitmap.LoadAsync(MainCanvas.Device, stream);

                if (shift)
                {
                    await SetBackgroundImageFromBitmapAsync(bitmap);
                    StatusText.Text = L("persist.status.dropped_background");
                }
                else
                {
                    if (panel != null && page != null)
                    {
                        var bounds = CreatePanelConstrainedFloatingImageBounds(
                            panel.Bounds,
                            (float)bitmap.SizeInPixels.Width,
                            (float)bitmap.SizeInPixels.Height);
                        await CreateFloatingImageFromBitmapAsync(
                            bitmap,
                            imagePath: null,
                            panel.Bounds.Center,
                            panelId: panel.Id,
                            boundsOverride: bounds);
                        StatusText.Text = LF("persist.status.loaded_panel", panel.Name);
                    }
                    else
                    {
                        await CreateFloatingImageFromBitmapAsync(bitmap, imagePath: null, worldPos);
                        StatusText.Text = L("persist.status.dropped_floating");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = LF("persist.status.drop_failed", ex.Message);
        }
    }

    private async Task LoadBackgroundImageWithoutResizeAsync(StorageFile file)
    {
        try
        {
            var page = _editorState.Document?.ActivePage;
            if (page == null)
            {
                return;
            }

            var bitmap = await LoadBitmapForImportAsync(file);

            _editorState.Execute(new SetPageBackgroundImageCommand(page.Id, file.Path));
            _editorState.SetBackgroundImageForPage(page.Id, bitmap);

            MainCanvas.Invalidate();
            StatusText.Text = LF("persist.status.loaded_bg", file.Name);
            RefreshPageSetup();
        }
        catch (Exception ex)
        {
            StatusText.Text = LF("persist.status.error_loading", ex.Message);
            StartupLogger.Log("LoadBackgroundImageWithoutResizeAsync failed", ex);
        }
    }

    private async Task SetBackgroundImageFromBitmapAsync(CanvasBitmap bitmap)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            return;
        }

        _editorState.Execute(new SetPageBackgroundImageCommand(page.Id, null));
        _editorState.SetBackgroundImageForPage(page.Id, bitmap);
        MainCanvas.Invalidate();
        RefreshPageSetup();
        await Task.CompletedTask;
    }

    private async Task<IReadOnlyList<StorageFile>> GetImportableImageFilesAsync(StorageFolder folder, bool includeSvg = true)
    {
        var files = await folder.GetFilesAsync();
        var filtered = files
            .Where(file => IsImageFile(file.FileType, includeSvg))
            .OrderBy(file => file.Name, Comparer<string>.Create(CompareNaturalFileNames))
            .ToList();
        return filtered;
    }

    private static int CompareNaturalFileNames(string? left, string? right)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var i = 0;
        var j = 0;

        while (i < left.Length && j < right.Length)
        {
            var leftIsDigit = char.IsDigit(left[i]);
            var rightIsDigit = char.IsDigit(right[j]);

            if (leftIsDigit && rightIsDigit)
            {
                var leftStart = i;
                var rightStart = j;

                while (i < left.Length && char.IsDigit(left[i])) i++;
                while (j < right.Length && char.IsDigit(right[j])) j++;

                var leftDigits = left[leftStart..i].TrimStart('0');
                var rightDigits = right[rightStart..j].TrimStart('0');
                if (leftDigits.Length != rightDigits.Length)
                {
                    return leftDigits.Length.CompareTo(rightDigits.Length);
                }

                var numberCompare = string.Compare(leftDigits, rightDigits, StringComparison.Ordinal);
                if (numberCompare != 0)
                {
                    return numberCompare;
                }

                var rawLengthCompare = (i - leftStart).CompareTo(j - rightStart);
                if (rawLengthCompare != 0)
                {
                    return rawLengthCompare;
                }

                continue;
            }

            var leftChar = char.ToUpperInvariant(left[i]);
            var rightChar = char.ToUpperInvariant(right[j]);
            var charCompare = leftChar.CompareTo(rightChar);
            if (charCompare != 0)
            {
                return charCompare;
            }

            i++;
            j++;
        }

        return left.Length.CompareTo(right.Length);
    }

    private async Task<int> ImportImagesAsPagesAsync(
        IReadOnlyList<StorageFile> files,
        bool autoAssignPanels,
        bool setActiveToFirstImportedPage = true)
    {
        var doc = _editorState.Document;
        if (doc == null || files.Count == 0)
        {
            return 0;
        }

        var commands = new List<ICommand>();
        var backgroundBitmaps = new Dictionary<Guid, CanvasBitmap>();
        var floatingBitmaps = new Dictionary<Guid, CanvasBitmap>();
        Guid? firstImportedPageId = null;
        var importCount = 0;

        foreach (var file in files)
        {
            var bitmap = await LoadBitmapForImportAsync(file);
            var width = (float)bitmap.SizeInPixels.Width;
            var height = (float)bitmap.SizeInPixels.Height;
            if (width <= 0f || height <= 0f)
            {
                throw new InvalidOperationException($"Image '{file.Name}' has no pixel data.");
            }

            var pageId = Guid.NewGuid();
            firstImportedPageId ??= pageId;
            var pageSize = new Size2(width, height);
            var fallbackName = $"Page {doc.Pages.Count + importCount + 1}";
            var pageName = Path.GetFileNameWithoutExtension(file.Name);
            if (string.IsNullOrWhiteSpace(pageName))
            {
                pageName = fallbackName;
            }

            commands.Add(new CreatePageCommand(pageName, pageSize, setActive: false, pageId: pageId));

            if (autoAssignPanels)
            {
                var panelId = Guid.NewGuid();
                var panelBounds = new Rect(0f, 0f, pageSize.Width, pageSize.Height);
                var panelName = "Panel 1";
                commands.Add(new CreatePanelZoneCommand(pageId, panelName, panelBounds, order: 1, panelId: panelId));

                var floatingBounds = CreatePanelConstrainedFloatingImageBounds(panelBounds, width, height);
                var createImageCommand = new CreateFloatingImageCommand(
                    pageId,
                    file.Path,
                    floatingBounds,
                    source: file.Path);
                commands.Add(createImageCommand);
                commands.Add(new SetFloatingImagePanelCommand(pageId, createImageCommand.CreatedImageId, panelId));
                floatingBitmaps[createImageCommand.CreatedImageId] = bitmap;
            }
            else
            {
                commands.Add(new SetPageBackgroundImageCommand(pageId, file.Path));
                backgroundBitmaps[pageId] = bitmap;
            }

            importCount++;
        }

        if (importCount == 0)
        {
            return 0;
        }

        if (setActiveToFirstImportedPage && firstImportedPageId.HasValue)
        {
            commands.Add(new SetActivePageCommand(firstImportedPageId.Value));
        }

        _editorState.ExecuteTransactionSafe(
            importCount == 1 ? "Import page image" : $"Import {importCount} page images",
            commands);

        foreach (var (pageId, bitmap) in backgroundBitmaps)
        {
            _editorState.SetBackgroundImageForPage(pageId, bitmap);
        }

        foreach (var (imageId, bitmap) in floatingBitmaps)
        {
            _floatingImageBitmaps[imageId] = bitmap;
        }

        MainCanvas.Invalidate();
        return importCount;
    }





    private async Task<(string? path, bool isPackage)?> PickDocumentToOpenAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".letterist");  // Primary extension
        picker.FileTypeFilter.Add(".zip");        // Legacy support for .letterist.zip
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return null;

        var filePath = file.Path;

        if (filePath.EndsWith(".letterist", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return (filePath, true);
        }

        StatusText.Text = L("persist.status.invalid_document");
        return null;
    }

    private async Task<DocumentSaveTarget?> PickDocumentSaveTargetAsync()
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Letterist Project", new List<string> { ".letterist" });
        picker.SuggestedFileName = _editorState.Document?.Name ?? "Untitled";
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return null;

        var path = file.Path;

        if (!path.EndsWith(".letterist", StringComparison.OrdinalIgnoreCase))
        {
            path += ".letterist";
        }

        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        var workingFolder = CreateTemporaryWorkingFolder();
        return new DocumentSaveTarget
        {
            FolderPath = workingFolder,
            PackagePath = path,
            IsTemporary = true,
            DisplayName = GetDocumentDisplayName(path)
        };
    }

    private async Task SaveDocumentAsync(bool saveAs)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var previousFolder = _currentDocumentFolderPath;
        var previousFolderIsTemporary = _currentDocumentFolderIsTemporary;

        if (_currentDocumentIsAutosave)
        {
            saveAs = true;
        }

        if (saveAs || string.IsNullOrWhiteSpace(_currentDocumentFolderPath))
        {
            var target = await PickDocumentSaveTargetAsync();
            if (target == null) return;

            _currentDocumentFolderPath = target.FolderPath;
            _currentDocumentPackagePath = target.PackagePath;
            _currentDocumentFolderIsTemporary = target.IsTemporary;
            _currentDocumentIsAutosave = false;
            doc.SetName(target.DisplayName);
        }

        var sourceFolder = previousFolder ?? _currentDocumentFolderPath;
        if (string.IsNullOrWhiteSpace(_currentDocumentFolderPath)) return;

        await SaveDocumentToPathAsync(doc, _currentDocumentFolderPath, _currentDocumentPackagePath, sourceFolder);
        doc.ClearDirty();
        UpdateStatusBar();

        if (_currentDocumentPackagePath != null)
        {
            AddRecentDocument(_currentDocumentPackagePath, isPackage: true);
        }
        else if (_currentDocumentFolderPath != null)
        {
            AddRecentDocument(_currentDocumentFolderPath, isPackage: false);
        }

        ClearAutosaveForDocument(doc.Id);

        if (saveAs && previousFolderIsTemporary && !string.IsNullOrWhiteSpace(previousFolder) &&
            !string.Equals(previousFolder, _currentDocumentFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteDirectory(previousFolder);
        }
    }

    private async Task SaveDocumentToPathAsync(Document doc, string folderPath, string? packagePath, string? sourceFolderPath)
    {
        Directory.CreateDirectory(folderPath);
        Directory.CreateDirectory(Path.Combine(folderPath, "assets"));
        Directory.CreateDirectory(Path.Combine(folderPath, "assets", "backgrounds"));
        Directory.CreateDirectory(Path.Combine(folderPath, "assets", "floating-images"));
        Directory.CreateDirectory(Path.Combine(folderPath, "thumbnails"));

        var backgroundPaths = await SaveBackgroundAssetsAsync(doc, folderPath, sourceFolderPath);
        var floatingImagePaths = await SaveFloatingImageAssetsAsync(doc, folderPath, sourceFolderPath);
        await SaveThumbnailsAsync(doc, folderPath);
        await DocumentStorage.SaveAsync(doc, folderPath, backgroundPaths, new Dictionary<Guid, string?>(), floatingImagePaths);

        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            CreatePackageFromFolder(folderPath, packagePath);
        }
    }

    private async Task<Dictionary<Guid, string?>> SaveBackgroundAssetsAsync(Document doc, string folderPath, string? sourceFolderPath)
    {
        var map = new Dictionary<Guid, string?>();

        foreach (var page in doc.Pages)
        {
            string? relativePath = null;
            var backgroundBitmap = _editorState.GetBackgroundImageForPage(page.Id);

            if (backgroundBitmap != null)
            {
                relativePath = Path.Combine("assets", "backgrounds", $"{page.Id}.png");
                var assetPath = Path.Combine(folderPath, relativePath);
                await SaveCanvasBitmapAsync(backgroundBitmap, assetPath);
            }
            else if (!string.IsNullOrWhiteSpace(page.BackgroundImagePath))
            {
                var resolved = ResolveBackgroundPath(page.BackgroundImagePath, sourceFolderPath ?? folderPath);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                {
                    if (!Path.IsPathRooted(page.BackgroundImagePath))
                    {
                        relativePath = page.BackgroundImagePath;
                    }
                    else
                    {
                        var ext = Path.GetExtension(resolved);
                        relativePath = Path.Combine("assets", "backgrounds", $"{page.Id}{ext}");
                        var destPath = Path.Combine(folderPath, relativePath);
                        File.Copy(resolved, destPath, true);
                    }
                }
            }

            map[page.Id] = relativePath;
        }

        return map;
    }

    private async Task SaveThumbnailsAsync(Document doc, string folderPath)
    {
        if (_canvasDevice == null) return;

        foreach (var page in doc.Pages)
        {
            var thumbPath = Path.Combine(folderPath, "thumbnails", $"{page.Id}.png");
            await SavePageThumbnailAsync(page, thumbPath);
        }
    }

    private async Task<Dictionary<Guid, string?>> SaveFloatingImageAssetsAsync(Document doc, string folderPath, string? sourceFolderPath)
    {
        var map = new Dictionary<Guid, string?>();

        foreach (var page in doc.Pages)
        {
            foreach (var image in page.FloatingImages)
            {
                string? relativePath = null;
                if (_floatingImageBitmaps.TryGetValue(image.Id, out var bitmap) && bitmap != null)
                {
                    relativePath = Path.Combine("assets", "floating-images", $"{image.Id}.png");
                    var assetPath = Path.Combine(folderPath, relativePath);
                    await SaveCanvasBitmapAsync(bitmap, assetPath);
                }
                else if (!string.IsNullOrWhiteSpace(image.ImagePath))
                {
                    var resolved = ResolveBackgroundPath(image.ImagePath, sourceFolderPath ?? folderPath);
                    if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    {
                        if (!Path.IsPathRooted(image.ImagePath))
                        {
                            relativePath = image.ImagePath;
                        }
                        else
                        {
                            var ext = Path.GetExtension(resolved);
                            relativePath = Path.Combine("assets", "floating-images", $"{image.Id}{ext}");
                            var destPath = Path.Combine(folderPath, relativePath);
                            File.Copy(resolved, destPath, true);
                        }
                    }
                }

                map[image.Id] = relativePath;
            }
        }

        return map;
    }

    private async Task SavePageThumbnailAsync(DocumentPage page, string filePath)
    {
        if (_canvasDevice == null) return;

        await EnsureFloatingImagesLoadedAsync(page);

        const int width = 256;
        const int height = 360;

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

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        stream.Seek(0);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var fileStream = File.Create(filePath);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
    }

    private static async Task SaveCanvasBitmapAsync(CanvasBitmap bitmap, string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        using var stream = new InMemoryRandomAccessStream();
        await bitmap.SaveAsync(stream, CanvasBitmapFileFormat.Png);
        stream.Seek(0);
        using var fileStream = File.Create(filePath);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
    }

    private async Task LoadDocumentFromFolderAsync(string folderPath)
    {
        ReleaseTemporaryDocumentFolder();
        await LoadDocumentInternalAsync(folderPath, packagePath: null, isTemporary: false, isAutosave: false);
        AddRecentDocument(folderPath, isPackage: false);
    }

    private async Task LoadDocumentFromPackageAsync(string packagePath)
    {
        ReleaseTemporaryDocumentFolder();
        var workingFolder = ExtractPackageToTempFolder(packagePath);
        await LoadDocumentInternalAsync(workingFolder, packagePath, isTemporary: true, isAutosave: false);
        AddRecentDocument(packagePath, isPackage: true);
    }

    private async Task LoadDocumentFromAutosaveAsync(AutosaveCandidate autosave)
    {
        ReleaseTemporaryDocumentFolder();
        await LoadDocumentInternalAsync(autosave.FolderPath, packagePath: null, isTemporary: false, isAutosave: true);
    }

    private async Task LoadDocumentInternalAsync(string folderPath, string? packagePath, bool isTemporary, bool isAutosave)
    {
        var document = await DocumentStorage.LoadAsync(folderPath);
        MigratePanelImagesToFloatingImages(document, folderPath);
        _editorState.SetDocument(document);
        _currentDocumentFolderPath = folderPath;
        _currentDocumentPackagePath = packagePath;
        _currentDocumentFolderIsTemporary = isTemporary;
        _currentDocumentIsAutosave = isAutosave;
        _pasteOffsetIndex = 0;

        if (document.ActivePage != null)
        {
            await EnsureBackgroundLoadedAsync(document.ActivePage);
            await EnsureFloatingImagesLoadedAsync(document.ActivePage);
        }

        RefreshLayerList();
        _ = RefreshPageListAsync();
    }

    private void MigratePanelImagesToFloatingImages(Document document, string? baseFolder)
    {
        foreach (var page in document.Pages)
        {
            foreach (var panel in page.Panels)
            {
                if (!panel.HasImage) continue;

                var bounds = panel.Bounds;
                if (!string.IsNullOrWhiteSpace(panel.ImagePath))
                {
                    var resolvedPath = ResolveBackgroundPath(panel.ImagePath, baseFolder);
                    if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                    {
                        try
                        {
                            using var imageStream = File.OpenRead(resolvedPath);
                            using var randomAccess = imageStream.AsRandomAccessStream();
                            var image = Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccess).AsTask().GetAwaiter().GetResult();
                            bounds = CreatePanelConstrainedFloatingImageBounds(
                                panel.Bounds,
                                image.PixelWidth,
                                image.PixelHeight);
                        }
                        catch
                        {
                            bounds = panel.Bounds;
                        }
                    }
                }

                var floating = new FloatingImage(
                    id: Guid.NewGuid(),
                    imagePath: panel.ImagePath,
                    bounds: bounds,
                    opacity: panel.ImagePlacement?.Opacity ?? 1f,
                    isVisible: true,
                    isLocked: panel.ImagePlacement?.IsLocked ?? false,
                    layerId: page.GetDefaultFloatingImageLayerId(),
                    panelId: panel.Id,
                    source: panel.ImagePath);
                page.AddFloatingImage(floating);

                panel.SetImage(null, null);
                _panelImages.Remove(panel.Id);
            }
        }
    }

    private string? ResolveBackgroundPath(string path, string? baseFolder = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;

        var root = baseFolder ?? _currentDocumentFolderPath;
        if (string.IsNullOrWhiteSpace(root)) return null;
        return Path.Combine(root, path);
    }

    private async Task EnsureBackgroundLoadedAsync(DocumentPage page)
    {
        if (_editorState.GetBackgroundImageForPage(page.Id) != null) return;
        if (string.IsNullOrWhiteSpace(page.BackgroundImagePath)) return;
        if (MainCanvas.Device == null) return;

        var resolvedPath = ResolveBackgroundPath(page.BackgroundImagePath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)) return;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(resolvedPath);
            var bitmap = await LoadBitmapForImportAsync(file);
            _editorState.SetBackgroundImageForPage(page.Id, bitmap);
        }
        catch
        {
        }
    }

    private async Task EnsureFloatingImagesLoadedAsync(DocumentPage page)
    {
        if (MainCanvas.Device == null) return;
        if (page.FloatingImages.Count == 0) return;

        foreach (var image in page.FloatingImages)
        {
            if (_floatingImageBitmaps.ContainsKey(image.Id)) continue;
            if (string.IsNullOrWhiteSpace(image.ImagePath)) continue;

            var resolvedPath = ResolveBackgroundPath(image.ImagePath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath)) continue;

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(resolvedPath);
                var bitmap = await LoadBitmapForImportAsync(file);
                _floatingImageBitmaps[image.Id] = bitmap;
            }
            catch
            {
            }
        }
    }

    private static string CreateTemporaryWorkingFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "Letterist", "packages");
        Directory.CreateDirectory(root);
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string ExtractPackageToTempFolder(string packagePath)
    {
        var folder = CreateTemporaryWorkingFolder();
        ZipFile.ExtractToDirectory(packagePath, folder);
        return folder;
    }

    private static void CreatePackageFromFolder(string folderPath, string packagePath)
    {
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        ZipFile.CreateFromDirectory(folderPath, packagePath, CompressionLevel.Optimal, false);
    }

    private void ReleaseTemporaryDocumentFolder()
    {
        if (!_currentDocumentFolderIsTemporary) return;
        if (!string.IsNullOrWhiteSpace(_currentDocumentFolderPath))
        {
            TryDeleteDirectory(_currentDocumentFolderPath);
        }

        _currentDocumentFolderIsTemporary = false;
    }

    private static string GetDocumentDisplayName(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(".letterist.zip", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^".letterist.zip".Length];
        }

        if (name.EndsWith(".letterist", StringComparison.OrdinalIgnoreCase))
        {
            return name[..^".letterist".Length];
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    private sealed class DocumentSaveTarget
    {
        public string FolderPath { get; set; } = "";
        public string? PackagePath { get; set; }
        public bool IsTemporary { get; set; }
        public string DisplayName { get; set; } = "";
    }



    private void InitializeAutosaveTimer()
    {
        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(GetAutosaveIntervalSeconds())
        };
        _autosaveTimer.Tick += AutosaveTimer_Tick;
        _autosaveTimer.Start();
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        StartupLogger.Log("MainWindow activated");
        var isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
        SetChildWindowsAlwaysOnTop(isWindowActive);

        if (!isWindowActive)
        {
            return;
        }

        if (!_hasConfiguredPresenter)
        {
            _hasConfiguredPresenter = true;
            ConfigureWindowPresenter();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    var hwnd = WindowNative.GetWindowHandle(this);
                    StartupLogger.Log("Delayed window style check");
                    FixWindowExtendedStyles(hwnd);
                }
                catch (Exception ex)
                {
                    StartupLogger.Log("Delayed style check failed", ex);
                }
            };
            timer.Start();
        }

        if (_hasCheckedAutosave) return;
        _hasCheckedAutosave = true;
        await CheckForAutosaveRecoveryAsync();
    }

    private void MainCanvas_Loaded(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log($"MainCanvas loaded size {MainCanvas.ActualWidth:F0}x{MainCanvas.ActualHeight:F0}");
        MainCanvas.Invalidate();
        StartStartupDrawTimer();
    }

    private void StartStartupDrawTimer()
    {
        _startupDrawAttempts = 0;
        if (_startupDrawTimer == null)
        {
            _startupDrawTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _startupDrawTimer.Tick += (_, _) =>
            {
                if (_loggedCanvasResources || _loggedFirstDraw)
                {
                    StopStartupDrawTimer();
                    return;
                }

                _startupDrawAttempts++;
                StartupLogger.Log($"MainCanvas not drawing yet (attempt {_startupDrawAttempts}, size {MainCanvas.ActualWidth:F0}x{MainCanvas.ActualHeight:F0})");
                MainCanvas.Invalidate();

                if (_startupDrawAttempts >= 5)
                {
                    StopStartupDrawTimer();
                }
            };
        }

        _startupDrawTimer.Start();
    }

    private void StopStartupDrawTimer()
    {
        if (_startupDrawTimer == null) return;
        _startupDrawTimer.Stop();
    }

    private async void AutosaveTimer_Tick(object? sender, object e)
    {
        await AutosaveIfNeededAsync();
    }

    private async Task AutosaveIfNeededAsync()
    {
        if (_autosaveInProgress) return;
        var doc = _editorState.Document;
        if (doc == null || !doc.IsDirty) return;
        if (_canvasDevice == null) return;

        _autosaveInProgress = true;
        try
        {
            await SaveAutosaveAsync(doc);
        }
        finally
        {
            _autosaveInProgress = false;
        }
    }

    private async Task SaveAutosaveAsync(Document doc)
    {
        var autosaveFolder = GetAutosaveFolderPath(doc.Id);
        Directory.CreateDirectory(autosaveFolder);

        await SaveDocumentToPathAsync(doc, autosaveFolder, packagePath: null, sourceFolderPath: _currentDocumentFolderPath);
        await SaveAutosavePreviewAsync(doc, autosaveFolder);
        await SaveAutosaveInfoAsync(doc, autosaveFolder);
    }

    private async Task SaveAutosavePreviewAsync(Document doc, string autosaveFolder)
    {
        if (doc.ActivePage == null) return;
        var previewPath = GetAutosavePreviewPath(autosaveFolder);
        await SavePageThumbnailAsync(doc.ActivePage, previewPath);
    }

    private async Task SaveAutosaveInfoAsync(Document doc, string autosaveFolder)
    {
        var info = new AutosaveInfo
        {
            DocumentId = doc.Id,
            DocumentName = doc.Name,
            SavedAtUtc = DateTime.UtcNow,
            SourceFolderPath = _currentDocumentFolderPath,
            SourcePackagePath = _currentDocumentPackagePath
        };

        var json = JsonSerializer.Serialize(info, RecentFilesJsonOptions);
        await File.WriteAllTextAsync(GetAutosaveInfoPath(autosaveFolder), json, Encoding.UTF8);
    }

    private async Task CheckForAutosaveRecoveryAsync()
    {
        if (App.SkipAutosaveRecovery) return;

        var autosave = await LoadLatestAutosaveAsync();
        if (autosave == null) return;

        var action = await ShowAutosaveRecoveryDialogAsync(autosave);
        if (action == AutosaveRecoveryAction.Recover)
        {
            await LoadDocumentFromAutosaveAsync(autosave);
        }
        else if (action == AutosaveRecoveryAction.Discard)
        {
            TryDeleteDirectory(autosave.FolderPath);
        }
    }

    private async Task<AutosaveCandidate?> LoadLatestAutosaveAsync()
    {
        var root = GetAutosaveRootPath();
        if (!Directory.Exists(root)) return null;

        AutosaveCandidate? latest = null;
        foreach (var folder in Directory.GetDirectories(root))
        {
            var infoPath = GetAutosaveInfoPath(folder);
            if (!File.Exists(infoPath)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(infoPath, Encoding.UTF8);
                var info = JsonSerializer.Deserialize<AutosaveInfo>(json, RecentFilesJsonOptions);
                if (info == null) continue;

                if (latest == null || info.SavedAtUtc > latest.Info.SavedAtUtc)
                {
                    latest = new AutosaveCandidate(folder, info);
                }
            }
            catch
            {
            }
        }

        return latest;
    }

    private async Task<AutosaveRecoveryAction> ShowAutosaveRecoveryDialogAsync(AutosaveCandidate autosave)
    {
        var panel = new StackPanel { Spacing = 12 };

        var previewPath = GetAutosavePreviewPath(autosave.FolderPath);
        if (File.Exists(previewPath))
        {
            var image = new Image
            {
                Width = 240,
                Height = 320,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
            };

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(previewPath);
                using var stream = await file.OpenReadAsync();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                image.Source = bitmap;
                panel.Children.Add(image);
            }
            catch
            {
            }
        }

        var infoText = LF("persist.dialog.autosave_found", autosave.Info.SavedAtUtc.ToLocalTime().ToString("g"), autosave.Info.DocumentName);
        panel.Children.Add(new TextBlock
        {
            Text = infoText,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray)
        });

        var dialog = new ContentDialog
        {
            Title = L("persist.dialog.recover_autosave"),
            Content = panel,
            PrimaryButtonText = L("persist.dialog.recover"),
            SecondaryButtonText = L("persist.dialog.discard"),
            CloseButtonText = L("persist.dialog.keep"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => AutosaveRecoveryAction.Recover,
            ContentDialogResult.Secondary => AutosaveRecoveryAction.Discard,
            _ => AutosaveRecoveryAction.Keep
        };
    }

    private void ClearAutosaveForDocument(Guid documentId)
    {
        TryDeleteDirectory(GetAutosaveFolderPath(documentId));
    }

    private static string GetAutosaveRootPath()
    {
        return Path.Combine(GetFallbackDataFolder(), "autosave");
    }

    private static string GetFallbackDataFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Letterist");
    }

    private static string GetFallbackRecentFilesPath()
    {
        return Path.Combine(GetFallbackDataFolder(), "recent.json");
    }

    private static string GetAutosaveFolderPath(Guid documentId)
    {
        return Path.Combine(GetAutosaveRootPath(), documentId.ToString("N"));
    }

    private static string GetAutosaveInfoPath(string autosaveFolder)
    {
        return Path.Combine(autosaveFolder, AutosaveInfoFileName);
    }

    private static string GetAutosavePreviewPath(string autosaveFolder)
    {
        return Path.Combine(autosaveFolder, AutosavePreviewFileName);
    }

    private void RefreshRecentDocumentsMenu()
    {
        RefreshRecentDocumentsMenu(LoadRecentDocuments());
    }

    private void RefreshRecentDocumentsMenu(List<RecentFileEntry> entries)
    {
        if (RecentDocumentsMenu == null) return;

        RecentDocumentsMenu.Items.Clear();
        if (entries.Count == 0)
        {
            RecentDocumentsMenu.Items.Add(new MenuFlyoutItem
            {
                Text = L("persist.menu.no_recent"),
                IsEnabled = false
            });
            return;
        }

        foreach (var entry in entries)
        {
            var item = new MenuFlyoutItem
            {
                Text = entry.DisplayName,
                Tag = entry
            };
            ToolTipService.SetToolTip(item, entry.Path);
            item.Click += RecentDocument_Click;
            RecentDocumentsMenu.Items.Add(item);
        }

        RecentDocumentsMenu.Items.Add(new MenuFlyoutSeparator());
        var clearItem = new MenuFlyoutItem { Text = L("persist.menu.clear_recent") };
        clearItem.Click += ClearRecentDocuments_Click;
        RecentDocumentsMenu.Items.Add(clearItem);
    }

    private void AddRecentDocument(string path, bool isPackage)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var entries = LoadRecentDocuments();
        entries.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
        entries.Insert(0, new RecentFileEntry
        {
            Path = path,
            IsPackage = isPackage,
            DisplayName = GetDocumentDisplayName(path),
            LastOpenedUtc = DateTime.UtcNow
        });

        var limit = GetRecentFilesLimit();
        if (entries.Count > limit)
        {
            entries.RemoveRange(limit, entries.Count - limit);
        }

        SaveRecentDocuments(entries);
        RefreshRecentDocumentsMenu(entries);
    }

    private void RemoveRecentDocument(string path)
    {
        var entries = LoadRecentDocuments();
        var removed = entries.RemoveAll(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed) return;

        SaveRecentDocuments(entries);
        RefreshRecentDocumentsMenu(entries);
    }

    private List<RecentFileEntry> LoadRecentDocuments()
    {
        return LoadRecentDocumentsFromFile();
    }

    private void SaveRecentDocuments(List<RecentFileEntry> entries)
    {
        SaveRecentDocumentsToFile(entries);
    }

    private List<RecentFileEntry> LoadRecentDocumentsFromFile()
    {
        var path = GetFallbackRecentFilesPath();
        if (!File.Exists(path)) return new List<RecentFileEntry>();

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<List<RecentFileEntry>>(json, RecentFilesJsonOptions);
            var entries = parsed ?? new List<RecentFileEntry>();
            var limit = GetRecentFilesLimit();
            if (entries.Count > limit)
            {
                entries = entries.Take(limit).ToList();
            }
            return entries;
        }
        catch
        {
            return new List<RecentFileEntry>();
        }
    }

    private void SaveRecentDocumentsToFile(List<RecentFileEntry> entries)
    {
        var path = GetFallbackRecentFilesPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(entries, RecentFilesJsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private void LoadSearchHistory()
    {
        _searchHistory.Clear();

        var path = GetSearchHistoryPath();
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<string>>(json, RecentFilesJsonOptions);
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var trimmed = entry?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                if (_searchHistory.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _searchHistory.Add(trimmed);
                if (_searchHistory.Count >= MaxSearchHistoryEntries)
                {
                    break;
                }
            }
        }
        catch
        {
        }
    }

    private void SaveSearchHistory()
    {
        var path = GetSearchHistoryPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(_searchHistory, RecentFilesJsonOptions);
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private void RecordSearchQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        var trimmed = query.Trim();
        _searchHistory.RemoveAll(entry => string.Equals(entry, trimmed, StringComparison.OrdinalIgnoreCase));
        _searchHistory.Insert(0, trimmed);

        if (_searchHistory.Count > MaxSearchHistoryEntries)
        {
            _searchHistory.RemoveRange(MaxSearchHistoryEntries, _searchHistory.Count - MaxSearchHistoryEntries);
        }

        SaveSearchHistory();
        _findWindow?.RefreshSearchHistory();
        _replaceWindow?.RefreshSearchHistory();
    }

    private async void RecentDocument_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not RecentFileEntry entry)
        {
            return;
        }

        if (entry.IsPackage)
        {
            if (!File.Exists(entry.Path))
            {
                RemoveRecentDocument(entry.Path);
                StatusText.Text = L("persist.status.recent_package_missing");
                return;
            }

            await LoadDocumentFromPackageAsync(entry.Path);
        }
        else
        {
            if (!Directory.Exists(entry.Path))
            {
                RemoveRecentDocument(entry.Path);
                StatusText.Text = L("persist.status.recent_folder_missing");
                return;
            }

            await LoadDocumentFromFolderAsync(entry.Path);
        }
    }

    private void ClearRecentDocuments_Click(object sender, RoutedEventArgs e)
    {
        SaveRecentDocuments(new List<RecentFileEntry>());
        RefreshRecentDocumentsMenu();
    }

    private static string GetSearchHistoryPath()
    {
        return Path.Combine(GetFallbackDataFolder(), SearchHistoryFileName);
    }

    private static void TryDeleteDirectory(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
        }
        catch
        {
        }
    }

    private enum AutosaveRecoveryAction
    {
        Recover,
        Discard,
        Keep
    }


}
