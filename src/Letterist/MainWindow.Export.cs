using Letterist.Commands;
using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Publishing;
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
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Reflection;
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

    private static readonly Guid KnownWebpContainerFormatGuid = new("E094B0E2-67F2-45B3-B0EA-115337CA7CF3");

    private enum ExportFormat
    {
        Png,
        Jpeg,
        Tiff,
        Webp,
        Pdf,
        Cbz,
        Cbr,
        Epub
    }

    private sealed class ExportOptions
    {
        public ExportFormat Format { get; set; }
        public int Dpi { get; set; }
        public int Quality { get; set; }
        public bool Transparent { get; set; }
        public bool OverlayOnly { get; set; }
        public bool SelectionOnly { get; set; }
        public bool VisibleLayersOnly { get; set; }
        public bool DrawPanelBorders { get; set; } = true;
        public bool IncludeMetadata { get; set; }
        public bool BatchExport { get; set; }
        public bool CurrentPageOnly { get; set; }
        public bool PerLayerExport { get; set; }
        public bool ExportAllLanguages { get; set; }
        public bool ExportVisibleLanguagesOnly { get; set; } = true;
        public bool PerLanguageFolders { get; set; }
        public bool IncludeLanguageCode { get; set; }
        public string LanguageSubset { get; set; } = "";
        public int PageNumberStart { get; set; } = 1;
        public int PageNumberPadding { get; set; } = 2;
        public string FilenamePattern { get; set; } = "";
        public string? OutputFolderOverride { get; set; }
        public PdfVersion PdfVersion { get; set; } = PdfVersion.Pdf17;
        public PdfConformance PdfConformance { get; set; } = PdfConformance.None;
        public PdfFontEmbeddingMode PdfFontEmbeddingMode { get; set; } = PdfFontEmbeddingMode.Subset;
        public PdfColorMode PdfColorMode { get; set; } = PdfColorMode.Rgb;
        public string PdfIccProfileName { get; set; } = "";
        public bool PdfIncludePrinterMarks { get; set; }
        public bool PdfExportSpreads { get; set; }
        public float? PdfCustomPageWidthPoints { get; set; }
        public float? PdfCustomPageHeightPoints { get; set; }
        public string RarExecutablePath { get; set; } = "";
    }

    private sealed class ExportQueueItem : INotifyPropertyChanged
    {
        public ExportQueueItem(string label, string fileName)
        {
            Label = label;
            FileName = fileName;
        }

        public string Label { get; }
        public string FileName { get; }

        private string _status = UiLocalizationService.GetString("export.status.queued");
        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class ExportQueueEntry
    {
        public ExportQueueEntry(DocumentPage page, Guid? layerId, string language, string filePath, string relativePath, ExportQueueItem item)
        {
            Page = page;
            LayerId = layerId;
            Language = language;
            FilePath = filePath;
            RelativePath = relativePath;
            Item = item;
        }

        public DocumentPage Page { get; }
        public Guid? LayerId { get; }
        public string Language { get; }
        public string FilePath { get; }
        public string RelativePath { get; }
        public ExportQueueItem Item { get; }
    }

    private sealed class ExportProgressDialogState
    {
        public ExportProgressDialogState(ContentDialog dialog, ProgressBar progressBar, TextBlock statusText)
        {
            Dialog = dialog;
            ProgressBar = progressBar;
            StatusText = statusText;
        }

        public ContentDialog Dialog { get; }
        public ProgressBar ProgressBar { get; }
        public TextBlock StatusText { get; }
    }

    private async Task ExportDocumentAsync()
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        var options = await ShowExportDialogAsync();
        if (options == null) return;

        SaveExportOptionsToPreferences(options);

        var languages = ExportPlanning.ResolveLanguages(
            doc,
            options.ExportAllLanguages,
            options.ExportVisibleLanguagesOnly,
            options.LanguageSubset);

        if (options.Format == ExportFormat.Pdf)
        {
            if (languages.Count > 1)
            {
                var pdfFolder = options.OutputFolderOverride;
                if (string.IsNullOrWhiteSpace(pdfFolder))
                {
                    pdfFolder = await PickExportFolderAsync();
                }

                if (pdfFolder == null) return;
                await ExportPdfBatchAsync(pdfFolder, options, languages, singlePdfPath: null);
                StatusText.Text = LF("export.status.exported_pdfs", Path.GetFileName(pdfFolder));
                return;
            }

            string? pdfPath;
            if (!string.IsNullOrWhiteSpace(options.OutputFolderOverride))
            {
                Directory.CreateDirectory(options.OutputFolderOverride);
                var baseName = BuildArchiveFileName(doc.Name, languages[0], options.IncludeLanguageCode);
                pdfPath = Path.Combine(options.OutputFolderOverride, baseName + GetOutputExtension(options.Format));
            }
            else
            {
                pdfPath = await PickExportFilePathAsync(doc, options, includeLanguageInName: options.IncludeLanguageCode, language: languages[0]);
            }

            if (pdfPath == null) return;
            var pdfDirectory = Path.GetDirectoryName(pdfPath);
            await ExportPdfBatchAsync(string.IsNullOrWhiteSpace(pdfDirectory) ? "." : pdfDirectory, options, languages, pdfPath);
            StatusText.Text = LF("export.status.exported_format", Path.GetFileName(pdfPath));
            return;
        }

        if (IsArchiveFormat(options.Format))
        {
            if (languages.Count > 1)
            {
                var archiveFolder = options.OutputFolderOverride;
                if (string.IsNullOrWhiteSpace(archiveFolder))
                {
                    archiveFolder = await PickExportFolderAsync();
                }

                if (archiveFolder == null) return;
                await ExportArchiveBatchAsync(archiveFolder, options, languages, singleArchivePath: null);
                StatusText.Text = LF("export.status.exported_archives", Path.GetFileName(archiveFolder));
                return;
            }

            string? archivePath;
            if (!string.IsNullOrWhiteSpace(options.OutputFolderOverride))
            {
                Directory.CreateDirectory(options.OutputFolderOverride);
                var baseName = BuildArchiveFileName(doc.Name, languages[0], options.IncludeLanguageCode);
                archivePath = Path.Combine(options.OutputFolderOverride, baseName + GetOutputExtension(options.Format));
            }
            else
            {
                archivePath = await PickExportFilePathAsync(doc, options, includeLanguageInName: options.IncludeLanguageCode, language: languages[0]);
            }

            if (archivePath == null) return;
            var archiveDirectory = Path.GetDirectoryName(archivePath);
            await ExportArchiveBatchAsync(string.IsNullOrWhiteSpace(archiveDirectory) ? "." : archiveDirectory, options, languages, archivePath);
            StatusText.Text = LF("export.status.exported_format", Path.GetFileName(archivePath));
            return;
        }

        var requiresFolderExport = ShouldExportAllPages(options) || options.PerLayerExport || languages.Count > 1;
        if (requiresFolderExport)
        {
            var folder = options.OutputFolderOverride;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = await PickExportFolderAsync();
            }
            if (folder == null) return;

            await ExportBatchAsync(folder, options, languages);
            StatusText.Text = LF("export.status.exported_folder", Path.GetFileName(folder));
            return;
        }

        string? filePath;
        if (!string.IsNullOrWhiteSpace(options.OutputFolderOverride))
        {
            Directory.CreateDirectory(options.OutputFolderOverride);
            var pageIndex = Math.Max(0, doc.IndexOfPage(doc.ActivePageId)) + 1;
            var fileName = BuildExportFileName(options, doc.Name, pageIndex, doc.ActiveLayer?.Name, languages[0]);
            filePath = Path.Combine(options.OutputFolderOverride, fileName + GetOutputExtension(options.Format));
        }
        else
        {
            filePath = await PickExportFilePathAsync(doc, options, options.IncludeLanguageCode, languages[0]);
        }
        if (filePath == null) return;

        var translationContext = CreateExportTranslationDocument(doc, languages[0]);
        await ExportPageAsync(doc.ActivePage, filePath, options, singleLayerId: null, language: languages[0], translationContext);
        StatusText.Text = LF("export.status.exported_format", Path.GetFileName(filePath));
    }

    private async Task<ExportOptions?> ShowExportDialogAsync()
    {
        var doc = _editorState.Document;
        if (doc == null) return null;
        if (_exportWindow != null)
        {
            _exportWindow.Activate();
            return null;
        }

        var formatOptions = GetSupportedExportFormats();
        var formatCombo = new ComboBox
        {
            ItemsSource = formatOptions,
            DisplayMemberPath = nameof(ExportFormatOption.Label),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var dpiItems = new List<string> { "72", "150", "300", "600" };
        var dpiCombo = new ComboBox
        {
            ItemsSource = dpiItems,
            SelectedIndex = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var qualitySlider = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            Value = 90,
            StepFrequency = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var qualityValue = new TextBlock
        {
            Text = "90",
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };

        var transparentToggle = new ToggleSwitch
        {
            Header = L("export.header.transparent"),
            IsOn = false
        };

        var overlayToggle = new ToggleSwitch
        {
            Header = L("export.header.overlay_only"),
            IsOn = false
        };

        var selectionToggle = new ToggleSwitch
        {
            Header = L("export.header.selection_only"),
            IsOn = false
        };

        var visibleLayersToggle = new ToggleSwitch
        {
            Header = L("export.header.visible_layers_only"),
            IsOn = true
        };

        var drawPanelBordersToggle = new ToggleSwitch
        {
            Header = L("export.header.draw_panel_borders"),
            IsOn = true
        };

        var metadataToggle = new ToggleSwitch
        {
            Header = L("export.header.include_metadata"),
            IsOn = false
        };

        var batchCheck = new CheckBox
        {
            Content = L("export.dialog.batch_export"),
            IsChecked = false
        };

        var currentPageOnlyCheck = new CheckBox
        {
            Content = L("export.dialog.current_page_only"),
            IsChecked = false
        };

        var perLayerCheck = new CheckBox
        {
            Content = L("export.dialog.per_layer"),
            IsChecked = false
        };

        var exportTranslationsToggle = new ToggleSwitch
        {
            Header = L("export.dialog.multilang.header"),
            IsOn = false,
            OnContent = L("common.on"),
            OffContent = L("common.off")
        };

        var visibleLanguagesCheck = new CheckBox
        {
            Content = L("export.dialog.multilang.visible_only"),
            IsChecked = true
        };

        var perLanguageFolderToggle = new ToggleSwitch
        {
            Header = L("export.dialog.multilang.per_language_folders"),
            IsOn = true
        };

        var includeLanguageCodeToggle = new ToggleSwitch
        {
            Header = L("export.dialog.multilang.include_lang_code"),
            IsOn = false
        };

        var languageSubsetBox = new TextBox
        {
            PlaceholderText = L("export.dialog.multilang.subset_placeholder")
        };

        var knownLanguagesText = new TextBlock
        {
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };
        var translationOptionsPanel = new StackPanel
        {
            Spacing = 8,
            Visibility = Visibility.Collapsed
        };

        var pageNumberStartBox = new NumberBox
        {
            Value = 1,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Minimum = 0,
            Maximum = 99999
        };

        var pageNumberPaddingBox = new NumberBox
        {
            Value = 2,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Minimum = 1,
            Maximum = 8
        };

        var patternBox = new TextBox
        {
            Text = "{document}-page-{page}",
            PlaceholderText = L("export.dialog.pattern_placeholder")
        };

        var exportPathLabel = new TextBlock
        {
            Text = L("export.dialog.output_path"),
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        };
        var exportPathBox = new TextBox
        {
            PlaceholderText = L("export.dialog.output_path_placeholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Text = GetValidDefaultExportFolder() ?? string.Empty
        };
        var exportPathBrowseButton = new Button
        {
            Content = L("export.dialog.browse")
        };

        var rarPathLabel = new TextBlock
        {
            Text = L("export.dialog.winrar_path"),
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Visibility = Visibility.Collapsed
        };
        var rarPathBox = new TextBox
        {
            PlaceholderText = L("export.dialog.winrar_path_placeholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };
        var rarPathBrowseButton = new Button
        {
            Content = L("export.dialog.browse"),
            Visibility = Visibility.Collapsed
        };
        var rarPathStatus = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var detectedRarPath = ComicArchiveTools.ResolveRarExecutable(_preferences.ExportDefaults.RarExecutablePath);
        if (!string.IsNullOrWhiteSpace(detectedRarPath))
        {
            rarPathBox.Text = detectedRarPath;
        }
        else if (!string.IsNullOrWhiteSpace(_preferences.ExportDefaults.RarExecutablePath))
        {
            rarPathBox.Text = _preferences.ExportDefaults.RarExecutablePath;
        }

        var previewText = new TextBlock
        {
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };
        var previewImage = new Image
        {
            MaxWidth = 240,
            MaxHeight = 240,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var previewStatus = new TextBlock
        {
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Text = L("export.dialog.preview_unavailable"),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var previewUpdateVersion = 0;
        var previewUpdateInProgress = false;

        ExportFormatOption? SelectedFormatOption()
        {
            return formatCombo.SelectedItem as ExportFormatOption;
        }

        ExportOptions BuildOptionsFromControls()
        {
            var option = SelectedFormatOption();
            return new ExportOptions
            {
                Format = option?.Format ?? ExportFormat.Png,
                Dpi = int.Parse(dpiCombo.SelectedItem?.ToString() ?? "300"),
                Quality = (int)qualitySlider.Value,
                Transparent = transparentToggle.IsOn,
                OverlayOnly = overlayToggle.IsOn,
                SelectionOnly = selectionToggle.IsOn,
                VisibleLayersOnly = visibleLayersToggle.IsOn,
                DrawPanelBorders = drawPanelBordersToggle.IsOn,
                IncludeMetadata = metadataToggle.IsOn,
                BatchExport = batchCheck.IsChecked == true,
                CurrentPageOnly = currentPageOnlyCheck.IsChecked == true,
                PerLayerExport = perLayerCheck.IsChecked == true,
                ExportAllLanguages = exportTranslationsToggle.IsOn,
                ExportVisibleLanguagesOnly = visibleLanguagesCheck.IsChecked != false,
                PerLanguageFolders = perLanguageFolderToggle.IsOn,
                IncludeLanguageCode = includeLanguageCodeToggle.IsOn,
                LanguageSubset = languageSubsetBox.Text?.Trim() ?? string.Empty,
                PageNumberStart = Math.Max(0, (int)pageNumberStartBox.Value),
                PageNumberPadding = Math.Clamp((int)pageNumberPaddingBox.Value, 1, 8),
                FilenamePattern = string.IsNullOrWhiteSpace(patternBox.Text) ? "{document}" : patternBox.Text,
                OutputFolderOverride = string.IsNullOrWhiteSpace(exportPathBox.Text) ? null : exportPathBox.Text.Trim(),
                RarExecutablePath = rarPathBox.Text?.Trim() ?? string.Empty
            };
        }

        void ApplyOptionsToControls(ExportOptions options)
        {
            var formatIndex = formatOptions.FindIndex(opt => opt.Format == options.Format);
            formatCombo.SelectedIndex = formatIndex >= 0 ? formatIndex : 0;
            var dpiText = options.Dpi.ToString();
            if (!dpiItems.Any(value => string.Equals(value, dpiText, StringComparison.Ordinal)))
            {
                dpiItems.Add(dpiText);
                dpiItems.Sort((left, right) =>
                {
                    if (int.TryParse(left, out var leftValue) && int.TryParse(right, out var rightValue))
                    {
                        return leftValue.CompareTo(rightValue);
                    }

                    return string.Compare(left, right, StringComparison.Ordinal);
                });
                dpiCombo.ItemsSource = null;
                dpiCombo.ItemsSource = dpiItems;
            }
            dpiCombo.SelectedItem = dpiText;
            qualitySlider.Value = options.Quality;
            transparentToggle.IsOn = options.Transparent;
            overlayToggle.IsOn = options.OverlayOnly;
            selectionToggle.IsOn = options.SelectionOnly;
            visibleLayersToggle.IsOn = options.VisibleLayersOnly;
            drawPanelBordersToggle.IsOn = options.DrawPanelBorders;
            metadataToggle.IsOn = options.IncludeMetadata;
            batchCheck.IsChecked = options.BatchExport;
            currentPageOnlyCheck.IsChecked = options.CurrentPageOnly;
            perLayerCheck.IsChecked = options.PerLayerExport;
            exportTranslationsToggle.IsOn = options.ExportAllLanguages;
            visibleLanguagesCheck.IsChecked = options.ExportVisibleLanguagesOnly;
            perLanguageFolderToggle.IsOn = options.PerLanguageFolders;
            includeLanguageCodeToggle.IsOn = options.IncludeLanguageCode;
            languageSubsetBox.Text = options.LanguageSubset;
            pageNumberStartBox.Value = Math.Max(0, options.PageNumberStart);
            pageNumberPaddingBox.Value = Math.Clamp(options.PageNumberPadding, 1, 8);
            patternBox.Text = options.FilenamePattern;
            exportPathBox.Text = options.OutputFolderOverride ?? GetValidDefaultExportFolder() ?? string.Empty;

            ApplyFormatRules();
            ApplySelectionRules();
            ApplyLanguageRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        }

        void ApplyFormatRules()
        {
            var option = SelectedFormatOption();
            if (option == null) return;

            transparentToggle.IsEnabled = option.SupportsTransparency;
            overlayToggle.IsEnabled = option.SupportsTransparency;
            if (!option.SupportsTransparency)
            {
                transparentToggle.IsOn = false;
                overlayToggle.IsOn = false;
            }

            qualitySlider.IsEnabled = option.SupportsQuality;
            qualityValue.Text = option.SupportsQuality ? ((int)qualitySlider.Value).ToString() : "N/A";

            var isImageFormat = option.Format is ExportFormat.Png or ExportFormat.Jpeg
                or ExportFormat.Tiff or ExportFormat.Webp;
            currentPageOnlyCheck.Visibility = isImageFormat ? Visibility.Visible : Visibility.Collapsed;
            batchCheck.Visibility = isImageFormat ? Visibility.Collapsed : Visibility.Visible;

            var isCbrFormat = option.Format == ExportFormat.Cbr;
            var rarVisibility = isCbrFormat ? Visibility.Visible : Visibility.Collapsed;
            rarPathLabel.Visibility = rarVisibility;
            rarPathBox.Visibility = rarVisibility;
            rarPathBrowseButton.Visibility = rarVisibility;
            rarPathStatus.Visibility = rarVisibility;
            if (isCbrFormat)
            {
                UpdateRarPathStatus();
            }
        }

        void UpdateRarPathStatus()
        {
            var path = rarPathBox.Text?.Trim();
            var resolved = ComicArchiveTools.ResolveRarExecutable(path);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                rarPathStatus.Text = LF("export.dialog.winrar_found", resolved);
                rarPathStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            }
            else if (!string.IsNullOrWhiteSpace(path))
            {
                rarPathStatus.Text = L("export.dialog.winrar_not_found");
                rarPathStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            }
            else
            {
                rarPathStatus.Text = L("export.dialog.winrar_required");
                rarPathStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            }
        }

        void ApplySelectionRules()
        {
            var option = SelectedFormatOption();
            var isImageFormat = option != null && (option.Format is ExportFormat.Png or ExportFormat.Jpeg
                or ExportFormat.Tiff or ExportFormat.Webp);
            var exportingAllPages = isImageFormat
                ? (currentPageOnlyCheck.IsChecked != true)
                : (batchCheck.IsChecked == true);
            var disableSelection = exportingAllPages || perLayerCheck.IsChecked == true;
            if (disableSelection)
            {
                selectionToggle.IsOn = false;
            }

            selectionToggle.IsEnabled = !disableSelection;
        }

        void ApplyLanguageRules()
        {
            var exportingAllLanguages = exportTranslationsToggle.IsOn;
            translationOptionsPanel.Visibility = exportingAllLanguages ? Visibility.Visible : Visibility.Collapsed;
            visibleLanguagesCheck.IsEnabled = exportingAllLanguages;
            perLanguageFolderToggle.IsEnabled = exportingAllLanguages;
            languageSubsetBox.IsEnabled = exportingAllLanguages;
            if (!exportingAllLanguages)
            {
                perLanguageFolderToggle.IsOn = false;
            }

            var knownLanguages = ExportPlanning.ResolveLanguages(
                doc,
                exportAllLanguages: true,
                visibleOnly: false,
                subset: null);
            knownLanguagesText.Text = knownLanguages.Count == 0
                ? L("export.dialog.multilang.known.none")
                : LF("export.dialog.multilang.known", string.Join(", ", knownLanguages));
        }

        string ResolvePreviewLanguage()
        {
            var resolved = ExportPlanning.ResolveLanguages(
                doc,
                exportTranslationsToggle.IsOn,
                visibleLanguagesCheck.IsChecked == true,
                languageSubsetBox.Text);
            return resolved.Count > 0 ? resolved[0] : doc.ActiveLanguage;
        }

        void UpdateFilenamePreview()
        {
            var option = SelectedFormatOption();
            if (option == null) return;

            var includePage = batchCheck.IsChecked == true;
            var includeLayer = perLayerCheck.IsChecked == true;
            var includeLanguage = includeLanguageCodeToggle.IsOn || exportTranslationsToggle.IsOn;
            var pattern = NormalizeExportPattern(patternBox.Text, includePage, includeLayer, includeLanguage);
            var pageIndex = Math.Max(0, doc.IndexOfPage(doc.ActivePageId)) + Math.Max(0, (int)pageNumberStartBox.Value);
            var layerName = doc.ActiveLayer?.Name ?? "Layer";
            var sample = ExpandExportPattern(
                pattern,
                doc.Name,
                pageIndex,
                Math.Max(1, (int)pageNumberPaddingBox.Value),
                layerName,
                ResolvePreviewLanguage());
            var extension = IsArchiveFormat(option.Format) || option.Format == ExportFormat.Pdf
                ? GetOutputExtension(option.Format)
                : GetRenderedPageExtension(option.Format);
            previewText.Text = $"Preview: {sample}{extension}";
        }

        void SetPreviewMessage(string message)
        {
            previewStatus.Text = message;
            previewImage.Source = null;
        }

        void RequestPreviewUpdate()
        {
            previewUpdateVersion++;
            if (previewUpdateInProgress) return;
            _ = UpdateExportPreviewAsync();
        }

        async Task UpdateExportPreviewAsync()
        {
            previewUpdateInProgress = true;
            while (true)
            {
                var currentVersion = previewUpdateVersion;
                try
                {
                    previewStatus.Text = L("export.dialog.rendering_preview");
                    var previewLanguage = ResolvePreviewLanguage();
                    var bitmap = await RenderExportPreviewAsync(BuildOptionsFromControls(), previewLanguage);
                    if (currentVersion != previewUpdateVersion)
                    {
                        continue;
                    }

                    if (bitmap == null)
                    {
                        SetPreviewMessage(L("export.dialog.preview_unavailable"));
                    }
                    else
                    {
                        previewImage.Source = bitmap;
                        previewStatus.Text = "";
                    }
                }
                catch
                {
                    if (currentVersion == previewUpdateVersion)
                    {
                        SetPreviewMessage(L("export.dialog.preview_unavailable"));
                    }
                }

                if (currentVersion == previewUpdateVersion)
                {
                    previewUpdateInProgress = false;
                    return;
                }
            }
        }

        async Task<BitmapImage?> RenderExportPreviewAsync(ExportOptions options, string language)
        {
            var page = doc.ActivePage;
            if (page == null || MainCanvas.Device == null) return null;

            await EnsureBackgroundLoadedAsync(page);
            await EnsureFloatingImagesLoadedAsync(page);

            var fullWidth = Math.Max(1, (int)Math.Ceiling(page.Size.Width));
            var fullHeight = Math.Max(1, (int)Math.Ceiling(page.Size.Height));
            using var fullRenderTarget = new CanvasRenderTarget(MainCanvas.Device, fullWidth, fullHeight, 96);
            using (var fullDs = fullRenderTarget.CreateDrawingSession())
            {
                var clearColor = options.Transparent || options.OverlayOnly
                    ? Windows.UI.Color.FromArgb(0, 0, 0, 0)
                    : Windows.UI.Color.FromArgb(255, 255, 255, 255);
                fullDs.Clear(clearColor);

                var background = options.OverlayOnly ? null : _editorState.GetBackgroundImageForPage(page.Id);
                var renderer = _renderer ?? new DocumentRenderer(new ViewTransform());
                var previewLayerId = options.PerLayerExport ? doc.ActiveLayer?.Id : null;
                var translationContext = CreateExportTranslationDocument(doc, language);
                renderer.RenderPageContent(
                    fullDs,
                    page,
                    background,
                    includeHiddenLayers: !options.VisibleLayersOnly,
                    singleLayerId: previewLayerId,
                    panelImageResolver: GetPanelImage,
                    floatingImageResolver: GetFloatingImage,
                    textFillImageResolver: GetTextFillImage,
                    translationDocument: translationContext,
                    renderPanelBorders: options.DrawPanelBorders,
                    renderPanelMembershipBadges: false);
            }

            CanvasRenderTarget previewSourceTarget = fullRenderTarget;
            CanvasRenderTarget? selectionTarget = null;
            if (options.SelectionOnly && doc.SelectedBalloon != null && doc.ActivePageId == page.Id)
            {
                var bounds = doc.SelectedBalloon.Bounds;
                var cropWidth = (int)Math.Ceiling(bounds.Width);
                var cropHeight = (int)Math.Ceiling(bounds.Height);
                if (cropWidth > 0 && cropHeight > 0)
                {
                    selectionTarget = new CanvasRenderTarget(MainCanvas.Device, cropWidth, cropHeight, 96);
                    using var cropDs = selectionTarget.CreateDrawingSession();
                    var clearColor = options.Transparent || options.OverlayOnly
                        ? Windows.UI.Color.FromArgb(0, 0, 0, 0)
                        : Windows.UI.Color.FromArgb(255, 255, 255, 255);
                    cropDs.Clear(clearColor);
                    cropDs.DrawImage(fullRenderTarget, -bounds.X, -bounds.Y);
                    previewSourceTarget = selectionTarget;
                }
            }

            const float maxPreviewSize = 220f;
            var sourceWidth = previewSourceTarget.SizeInPixels.Width;
            var sourceHeight = previewSourceTarget.SizeInPixels.Height;
            var scale = MathF.Min(maxPreviewSize / sourceWidth, maxPreviewSize / sourceHeight);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                selectionTarget?.Dispose();
                return null;
            }

            var previewWidth = Math.Max(1, (int)Math.Ceiling(sourceWidth * scale));
            var previewHeight = Math.Max(1, (int)Math.Ceiling(sourceHeight * scale));
            using var previewTarget = new CanvasRenderTarget(MainCanvas.Device, previewWidth, previewHeight, 96);
            using (var previewDs = previewTarget.CreateDrawingSession())
            {
                var clearColor = options.Transparent || options.OverlayOnly
                    ? Windows.UI.Color.FromArgb(0, 0, 0, 0)
                    : Windows.UI.Color.FromArgb(255, 255, 255, 255);
                previewDs.Clear(clearColor);
                previewDs.DrawImage(
                    previewSourceTarget,
                    new Windows.Foundation.Rect(0, 0, previewWidth, previewHeight),
                    new Windows.Foundation.Rect(0, 0, sourceWidth, sourceHeight));
            }

            using var stream = new InMemoryRandomAccessStream();
            await previewTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png, 1.0f);
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            selectionTarget?.Dispose();
            return bitmap;
        }

        var preferredOptions = CreateExportOptionsFromPreferences();

        formatCombo.SelectionChanged += (s, e) =>
        {
            ApplyFormatRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        rarPathBox.TextChanged += (s, e) =>
        {
            UpdateRarPathStatus();
        };

        rarPathBrowseButton.Click += async (s, e) =>
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                rarPathBox.Text = file.Path;
                UpdateRarPathStatus();
            }
        };

        dpiCombo.SelectionChanged += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        batchCheck.Checked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        batchCheck.Unchecked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        currentPageOnlyCheck.Checked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        currentPageOnlyCheck.Unchecked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        perLayerCheck.Checked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        perLayerCheck.Unchecked += (s, e) =>
        {
            ApplySelectionRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        exportTranslationsToggle.Toggled += (s, e) =>
        {
            ApplyLanguageRules();
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        visibleLanguagesCheck.Checked += (s, e) =>
        {
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        visibleLanguagesCheck.Unchecked += (s, e) =>
        {
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        perLanguageFolderToggle.Toggled += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        includeLanguageCodeToggle.Toggled += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        languageSubsetBox.TextChanged += (s, e) =>
        {
            UpdateFilenamePreview();
            RequestPreviewUpdate();
        };

        pageNumberStartBox.ValueChanged += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        pageNumberPaddingBox.ValueChanged += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        patternBox.TextChanged += (s, e) =>
        {
            UpdateFilenamePreview();
        };

        transparentToggle.Toggled += (s, e) =>
        {
            RequestPreviewUpdate();
        };

        overlayToggle.Toggled += (s, e) =>
        {
            if (overlayToggle.IsOn)
            {
                transparentToggle.IsOn = true;
            }
            RequestPreviewUpdate();
        };

        selectionToggle.Toggled += (s, e) =>
        {
            RequestPreviewUpdate();
        };

        visibleLayersToggle.Toggled += (s, e) =>
        {
            RequestPreviewUpdate();
        };

        drawPanelBordersToggle.Toggled += (s, e) =>
        {
            RequestPreviewUpdate();
        };

        exportPathBrowseButton.Click += async (s, e) =>
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                exportPathBox.Text = folder.Path;
            }
        };

        qualitySlider.ValueChanged += (s, e) =>
        {
            qualityValue.Text = qualitySlider.IsEnabled ? ((int)qualitySlider.Value).ToString() : "N/A";
        };

        ApplyFormatRules();
        ApplySelectionRules();
        ApplyLanguageRules();
        UpdateFilenamePreview();
        ApplyOptionsToControls(preferredOptions);

        var controlPanel = new StackPanel { Spacing = 12 };
        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.format"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(formatCombo);

        var rarPathPanel = new Grid { ColumnSpacing = 8 };
        rarPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rarPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rarPathPanel.Children.Add(rarPathBox);
        rarPathPanel.Children.Add(rarPathBrowseButton);
        Grid.SetColumn(rarPathBox, 0);
        Grid.SetColumn(rarPathBrowseButton, 1);
        controlPanel.Children.Add(rarPathLabel);
        controlPanel.Children.Add(rarPathPanel);
        controlPanel.Children.Add(rarPathStatus);

        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.dpi"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(dpiCombo);

        var qualityPanel = new StackPanel { Spacing = 4 };
        qualityPanel.Children.Add(new TextBlock { Text = L("export.dialog.quality"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        qualityPanel.Children.Add(qualitySlider);
        qualityPanel.Children.Add(qualityValue);
        controlPanel.Children.Add(qualityPanel);

        controlPanel.Children.Add(transparentToggle);
        controlPanel.Children.Add(overlayToggle);
        controlPanel.Children.Add(selectionToggle);
        controlPanel.Children.Add(visibleLayersToggle);
        controlPanel.Children.Add(drawPanelBordersToggle);
        controlPanel.Children.Add(metadataToggle);
        controlPanel.Children.Add(batchCheck);
        controlPanel.Children.Add(currentPageOnlyCheck);
        controlPanel.Children.Add(perLayerCheck);

        translationOptionsPanel.Children.Add(visibleLanguagesCheck);
        translationOptionsPanel.Children.Add(perLanguageFolderToggle);
        translationOptionsPanel.Children.Add(includeLanguageCodeToggle);
        translationOptionsPanel.Children.Add(languageSubsetBox);
        translationOptionsPanel.Children.Add(knownLanguagesText);

        var translationHeaderGrid = new Grid();
        translationHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        translationHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var translationHeaderText = new TextBlock
        {
            Text = L("export.dialog.multilang.header"),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var translationHeaderHintText = new TextBlock
        {
            Text = L("export.dialog.multilang.hint"),
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(exportTranslationsToggle, 1);
        translationHeaderGrid.Children.Add(translationHeaderText);
        translationHeaderGrid.Children.Add(exportTranslationsToggle);

        var translationCardContent = new StackPanel { Spacing = 6 };
        translationCardContent.Children.Add(translationHeaderGrid);
        translationCardContent.Children.Add(translationHeaderHintText);
        translationCardContent.Children.Add(translationOptionsPanel);

        var translationCard = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 36, 36, 36)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 62, 62, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Child = translationCardContent
        };
        controlPanel.Children.Add(translationCard);

        controlPanel.Children.Add(exportPathLabel);
        var exportPathPanel = new Grid { ColumnSpacing = 8 };
        exportPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        exportPathPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        exportPathPanel.Children.Add(exportPathBox);
        exportPathPanel.Children.Add(exportPathBrowseButton);
        Grid.SetColumn(exportPathBox, 0);
        Grid.SetColumn(exportPathBrowseButton, 1);
        controlPanel.Children.Add(exportPathPanel);

        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.image_sequence"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.page_start"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(pageNumberStartBox);
        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.page_padding"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(pageNumberPaddingBox);

        controlPanel.Children.Add(new TextBlock { Text = L("export.dialog.filename_pattern"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        controlPanel.Children.Add(patternBox);
        controlPanel.Children.Add(previewText);

        var previewGrid = new Grid();
        previewGrid.Children.Add(previewImage);
        previewGrid.Children.Add(previewStatus);

        var previewBorder = new Border
        {
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke),
            Child = previewGrid
        };

        var previewPanel = new StackPanel { Spacing = 8 };
        previewPanel.Children.Add(new TextBlock { Text = L("export.dialog.preview"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        previewPanel.Children.Add(previewBorder);

        var controlScrollViewer = new ScrollViewer
        {
            Content = controlPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 500
        };

        var contentGrid = new Grid { ColumnSpacing = 16, MinWidth = 600 };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        contentGrid.Children.Add(controlScrollViewer);
        contentGrid.Children.Add(previewPanel);
        Grid.SetColumn(controlScrollViewer, 0);
        Grid.SetColumn(previewPanel, 1);

        var exportButton = new Button
        {
            Content = L("export.dialog.export_button"),
            MinWidth = 110,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        var cancelButton = new Button
        {
            Content = L("common.cancel"),
            MinWidth = 110
        };
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(exportButton);

        var rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        Grid.SetRow(contentGrid, 0);
        rootGrid.Children.Add(contentGrid);
        var buttonBorder = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 37, 37, 37)),
            Padding = new Thickness(12),
            Child = buttonRow
        };
        Grid.SetRow(buttonBorder, 1);
        rootGrid.Children.Add(buttonBorder);

        var windowContent = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30)),
            Padding = new Thickness(14),
            Child = rootGrid
        };

        var completion = new TaskCompletionSource<ExportOptions?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var exportWindow = new Window
        {
            Title = L("export.dialog.title"),
            Content = windowContent
        };
        _exportWindow = exportWindow;

        var completed = false;
        void CompleteAndClose(ExportOptions? result)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            completion.TrySetResult(result);
            try
            {
                exportWindow.Close();
            }
            catch
            {
            }
        }

        cancelButton.Click += (_, _) => CompleteAndClose(null);
        exportButton.Click += (_, _) =>
        {
            var options = BuildOptionsFromControls();
            if (options.Format == ExportFormat.Cbr)
            {
                var resolvedRar = ComicArchiveTools.ResolveRarExecutable(options.RarExecutablePath);
                if (string.IsNullOrWhiteSpace(resolvedRar))
                {
                    rarPathStatus.Visibility = Visibility.Visible;
                    rarPathStatus.Text = L("export.error.cbr_rar_path_required");
                    rarPathStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                    return;
                }

                options.RarExecutablePath = resolvedRar;
            }

            CompleteAndClose(options);
        };

        AttachEscapeToCloseWindow(exportWindow, rootGrid);
        exportWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_exportWindow, exportWindow))
            {
                _exportWindow = null;
            }

            if (completed)
            {
                return;
            }

            completed = true;
            completion.TrySetResult(null);
        };

        var appWindow = exportWindow.AppWindow;
        if (appWindow.Presenter is not OverlappedPresenter)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }
        if (appWindow.Presenter is OverlappedPresenter exportPresenter)
        {
            exportPresenter.IsResizable = true;
            exportPresenter.SetBorderAndTitleBar(true, true);
        }

        appWindow.Resize(new SizeInt32(1240, 900));
        CenterChildWindowOverMainWindow(appWindow);
        TryApplyFontChooserTitleBarTheme(appWindow);
        SetWindowAlwaysOnTop(exportWindow, isAlwaysOnTop: true);

        exportWindow.Activate();
        _ = exportWindow.DispatcherQueue.TryEnqueue(() => TryApplyFontChooserTitleBarTheme(appWindow));

        return await completion.Task;
    }

    private async Task<string?> PickExportFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task<string?> PickExportFilePathAsync(Document doc, ExportOptions options, bool includeLanguageInName, string language)
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add(GetFormatLabel(options.Format), new List<string> { GetOutputExtension(options.Format) });

        var pageIndex = Math.Max(0, doc.IndexOfPage(doc.ActivePageId)) + Math.Max(0, options.PageNumberStart);
        var pattern = NormalizeExportPattern(options.FilenamePattern, includePage: false, includeLayer: false, includeLanguage: includeLanguageInName);
        var fileName = ExpandExportPattern(
            pattern,
            doc.Name,
            pageIndex,
            options.PageNumberPadding,
            doc.ActiveLayer?.Name ?? "Layer",
            language);
        picker.SuggestedFileName = fileName;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task ExportBatchAsync(string outputFolder, ExportOptions options, IReadOnlyList<string> languages)
    {
        var doc = _editorState.Document;
        if (doc == null || MainCanvas.Device == null) return;

        Directory.CreateDirectory(outputFolder);

        var pages = new List<DocumentPage>();
        if (ShouldExportAllPages(options))
        {
            pages.AddRange(doc.Pages);
        }
        else if (doc.ActivePage != null)
        {
            pages.Add(doc.ActivePage);
        }

        var queueEntries = BuildExportQueueEntries(doc, pages, languages, outputFolder, options, usePerLanguageFolder: options.PerLanguageFolders);
        if (queueEntries.Count == 0) return;

        var translationContexts = languages.ToDictionary(
            language => language,
            language => CreateExportTranslationDocument(doc, language),
            StringComparer.OrdinalIgnoreCase);

        var queueItems = new ObservableCollection<ExportQueueItem>(queueEntries.Select(entry => entry.Item));
        var progressDialog = CreateExportProgressDialog(queueItems, queueEntries.Count);
        _ = progressDialog.Dialog.ShowAsync();

        var completed = 0;
        var failed = 0;
        progressDialog.ProgressBar.Value = 0;
        progressDialog.StatusText.Text = LF("export.status.exporting_format", 0, queueEntries.Count);

        foreach (var entry in queueEntries)
        {
            entry.Item.Status = L("export.status.exporting");
            progressDialog.StatusText.Text = LF("export.status.exporting_format", completed + 1, queueEntries.Count);
            try
            {
                var translationContext = translationContexts.TryGetValue(entry.Language, out var value) ? value : doc;
                await ExportPageAsync(entry.Page, entry.FilePath, options, entry.LayerId, entry.Language, translationContext);
                entry.Item.Status = L("export.status.done");
            }
            catch
            {
                failed++;
                entry.Item.Status = L("export.status.failed");
            }

            completed++;
            progressDialog.ProgressBar.Value = completed;
            progressDialog.StatusText.Text = LF("export.status.exporting_format", completed, queueEntries.Count);
        }

        var successCount = completed - failed;
        progressDialog.StatusText.Text = failed == 0
            ? LF("export.status.complete", successCount, successCount == 1 ? string.Empty : "s")
            : LF("export.status.complete_with_failures", successCount, failed);
    }

    private async Task ExportArchiveBatchAsync(string outputFolder, ExportOptions options, IReadOnlyList<string> languages, string? singleArchivePath)
    {
        var doc = _editorState.Document;
        if (doc == null || MainCanvas.Device == null) return;

        Directory.CreateDirectory(outputFolder);

        var pages = new List<DocumentPage>();
        if (ShouldExportAllPages(options))
        {
            pages.AddRange(doc.Pages);
        }
        else if (doc.ActivePage != null)
        {
            pages.Add(doc.ActivePage);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"letterist-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var queueEntries = BuildExportQueueEntries(doc, pages, languages, tempRoot, options, usePerLanguageFolder: false);
            if (queueEntries.Count == 0) return;

            var translationContexts = languages.ToDictionary(
                language => language,
                language => CreateExportTranslationDocument(doc, language),
                StringComparer.OrdinalIgnoreCase);

            var queueItems = new ObservableCollection<ExportQueueItem>(queueEntries.Select(entry => entry.Item));
            var progressDialog = CreateExportProgressDialog(queueItems, queueEntries.Count);
            _ = progressDialog.Dialog.ShowAsync();

            var completed = 0;
            var failed = 0;
            var successfulEntries = new List<ExportQueueEntry>();
            progressDialog.ProgressBar.Value = 0;
            progressDialog.StatusText.Text = LF("export.status.rendering_format", 0, queueEntries.Count);

            foreach (var entry in queueEntries)
            {
                entry.Item.Status = L("export.status.exporting");
                progressDialog.StatusText.Text = LF("export.status.rendering_format", completed + 1, queueEntries.Count);
                try
                {
                    var translationContext = translationContexts.TryGetValue(entry.Language, out var value) ? value : doc;
                    await ExportPageAsync(entry.Page, entry.FilePath, options, entry.LayerId, entry.Language, translationContext);
                    successfulEntries.Add(entry);
                    entry.Item.Status = L("export.status.done");
                }
                catch
                {
                    failed++;
                    entry.Item.Status = L("export.status.failed");
                }

                completed++;
                progressDialog.ProgressBar.Value = completed;
                progressDialog.StatusText.Text = LF("export.status.rendering_format", completed, queueEntries.Count);
            }

            if (successfulEntries.Count == 0)
            {
                progressDialog.StatusText.Text = L("export.status.archive_no_pages");
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(singleArchivePath))
                {
                    await PackageRenderedArchiveAsync(
                        singleArchivePath,
                        options,
                        doc,
                        successfulEntries,
                        languages[0],
                        tempRoot,
                        options.RarExecutablePath);
                }
                else
                {
                    foreach (var group in successfulEntries.GroupBy(entry => entry.Language, StringComparer.OrdinalIgnoreCase))
                    {
                        var archiveName = BuildArchiveFileName(doc.Name, group.Key, includeLanguageCode: true);
                        var archivePath = Path.Combine(outputFolder, archiveName + GetOutputExtension(options.Format));
                        await PackageRenderedArchiveAsync(
                            archivePath,
                            options,
                            doc,
                            group.ToList(),
                            group.Key,
                            tempRoot,
                            options.RarExecutablePath);
                    }
                }
            }
            catch (Exception ex)
            {
                progressDialog.StatusText.Text = UiLocalizationService.Format("export.status.archive_failed", ex.Message);
                return;
            }

            progressDialog.StatusText.Text = failed == 0
                ? L("export.status.archive_complete")
                : LF("export.status.archive_complete_failures", failed);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private async Task ExportPdfBatchAsync(string outputFolder, ExportOptions options, IReadOnlyList<string> languages, string? singlePdfPath)
    {
        var doc = _editorState.Document;
        if (doc == null || MainCanvas.Device == null) return;

        Directory.CreateDirectory(outputFolder);

        var pages = new List<DocumentPage>();
        if (ShouldExportAllPages(options))
        {
            pages.AddRange(doc.Pages);
        }
        else if (doc.ActivePage != null)
        {
            pages.Add(doc.ActivePage);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"letterist-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var queueEntries = BuildExportQueueEntries(doc, pages, languages, tempRoot, options, usePerLanguageFolder: false);
            if (queueEntries.Count == 0) return;

            var translationContexts = languages.ToDictionary(
                language => language,
                language => CreateExportTranslationDocument(doc, language),
                StringComparer.OrdinalIgnoreCase);

            var queueItems = new ObservableCollection<ExportQueueItem>(queueEntries.Select(entry => entry.Item));
            var progressDialog = CreateExportProgressDialog(queueItems, queueEntries.Count);
            _ = progressDialog.Dialog.ShowAsync();

            var completed = 0;
            var failed = 0;
            var successfulEntries = new List<ExportQueueEntry>();
            progressDialog.ProgressBar.Value = 0;
            progressDialog.StatusText.Text = LF("export.status.rendering_format", 0, queueEntries.Count);

            foreach (var entry in queueEntries)
            {
                entry.Item.Status = L("export.status.exporting");
                progressDialog.StatusText.Text = LF("export.status.rendering_format", completed + 1, queueEntries.Count);
                try
                {
                    var translationContext = translationContexts.TryGetValue(entry.Language, out var value) ? value : doc;
                    await ExportPageAsync(entry.Page, entry.FilePath, options, entry.LayerId, entry.Language, translationContext);
                    successfulEntries.Add(entry);
                    entry.Item.Status = L("export.status.done");
                }
                catch
                {
                    failed++;
                    entry.Item.Status = L("export.status.failed");
                }

                completed++;
                progressDialog.ProgressBar.Value = completed;
                progressDialog.StatusText.Text = LF("export.status.rendering_format", completed, queueEntries.Count);
            }

            if (successfulEntries.Count == 0)
            {
                progressDialog.StatusText.Text = L("export.status.pdf_no_pages");
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(singlePdfPath))
                {
                    var language = languages[0];
                    var entries = successfulEntries
                        .Where(entry => string.Equals(entry.Language, language, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(entry => doc.IndexOfPage(entry.Page.Id))
                        .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    await PackageRenderedPdfAsync(singlePdfPath, options, doc, entries, language);
                }
                else
                {
                    foreach (var group in successfulEntries.GroupBy(entry => entry.Language, StringComparer.OrdinalIgnoreCase))
                    {
                        var pdfName = BuildArchiveFileName(doc.Name, group.Key, includeLanguageCode: true);
                        var pdfPath = Path.Combine(outputFolder, pdfName + GetOutputExtension(options.Format));
                        var entries = group
                            .OrderBy(entry => doc.IndexOfPage(entry.Page.Id))
                            .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
                        await PackageRenderedPdfAsync(pdfPath, options, doc, entries, group.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                progressDialog.StatusText.Text = LF("export.status.pdf_failed", ex.Message);
                return;
            }

            progressDialog.StatusText.Text = failed == 0
                ? L("export.status.pdf_complete")
                : LF("export.status.pdf_complete_failures", failed);
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static async Task PackageRenderedPdfAsync(
        string pdfPath,
        ExportOptions options,
        Document document,
        IReadOnlyList<ExportQueueEntry> entries,
        string language)
    {
        var pages = new List<PdfRenderedPage>(entries.Count);
        foreach (var entry in entries)
        {
            var bytes = await File.ReadAllBytesAsync(entry.FilePath);
            var dpi = Math.Max(72, options.Dpi);
            var fallbackWidth = Math.Max(1, (int)Math.Ceiling(entry.Page.Size.Width));
            var fallbackHeight = Math.Max(1, (int)Math.Ceiling(entry.Page.Size.Height));
            var (width, height) = await TryReadImagePixelSizeAsync(bytes, fallbackWidth, fallbackHeight);
            pages.Add(new PdfRenderedPage
            {
                ImageBytes = bytes,
                PixelWidth = width,
                PixelHeight = height,
                Dpi = dpi,
                Label = entry.Item.Label
            });
        }

        var settings = new PdfExportSettings
        {
            Version = options.PdfVersion,
            Conformance = options.PdfConformance,
            FontEmbeddingMode = options.PdfFontEmbeddingMode,
            ColorMode = options.PdfColorMode,
            IccProfileName = options.PdfIccProfileName ?? string.Empty,
            IncludePrinterMarks = options.PdfIncludePrinterMarks,
            ExportSpreads = options.PdfExportSpreads,
            CustomPageWidthPoints = options.PdfCustomPageWidthPoints,
            CustomPageHeightPoints = options.PdfCustomPageHeightPoints,
            Title = document.Name,
            Subject = $"Language: {language}",
            Keywords = string.IsNullOrWhiteSpace(options.LanguageSubset) ? language : options.LanguageSubset
        };

        var result = PdfExportService.WriteImagePdf(pdfPath, pages, settings);
        if (options.IncludeMetadata && result.Warnings.Count > 0)
        {
            var warningsPath = pdfPath + ".warnings.json";
            var payload = new
            {
                documentId = document.Id,
                documentName = document.Name,
                pageCount = result.PageCount,
                exportedAtUtc = DateTime.UtcNow,
                language,
                warnings = result.Warnings
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(warningsPath, json, Encoding.UTF8);
        }
    }

    private static async Task PackageRenderedArchiveAsync(
        string archivePath,
        ExportOptions options,
        Document document,
        IReadOnlyList<ExportQueueEntry> entries,
        string language,
        string tempRoot,
        string? configuredRarPath)
    {
        switch (options.Format)
        {
            case ExportFormat.Cbz:
                CreateComicZipArchive(archivePath, entries);
                break;
            case ExportFormat.Cbr:
                await CreateComicRarArchiveAsync(archivePath, entries, tempRoot, configuredRarPath);
                break;
            case ExportFormat.Epub:
                CreateFixedLayoutEpubArchive(archivePath, document, entries, language);
                break;
            default:
                throw new InvalidOperationException($"Unsupported archive format: {options.Format}");
        }
    }

    private static void CreateComicZipArchive(string archivePath, IEnumerable<ExportQueueEntry> entries)
    {
        var directory = Path.GetDirectoryName(archivePath);
        Directory.CreateDirectory(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var entry in entries.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var archiveName = entry.RelativePath.Replace('\\', '/');
            archive.CreateEntryFromFile(entry.FilePath, archiveName, CompressionLevel.Optimal);

            var metadataPath = entry.FilePath + ".metadata.json";
            if (File.Exists(metadataPath))
            {
                archive.CreateEntryFromFile(
                    metadataPath,
                    (entry.RelativePath + ".metadata.json").Replace('\\', '/'),
                    CompressionLevel.Optimal);
            }
        }
    }

    private static async Task CreateComicRarArchiveAsync(
        string archivePath,
        IReadOnlyList<ExportQueueEntry> entries,
        string tempRoot,
        string? configuredRarPath)
    {
        var directory = Path.GetDirectoryName(archivePath);
        Directory.CreateDirectory(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var rarExecutable = ComicArchiveTools.ResolveRarExecutable(configuredRarPath);
        if (string.IsNullOrWhiteSpace(rarExecutable))
        {
            throw new InvalidOperationException(UiLocalizationService.GetString("export.error.cbr_missing_rar"));
        }

        var listFilePath = Path.Combine(tempRoot, $"cbr-files-{Guid.NewGuid():N}.lst");
        var ordered = entries.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        var listLines = new List<string>(ordered.Count * 2);
        foreach (var entry in ordered)
        {
            listLines.Add($"\"{entry.RelativePath}\"");
            var metadataRelativePath = entry.RelativePath + ".metadata.json";
            if (File.Exists(entry.FilePath + ".metadata.json"))
            {
                listLines.Add($"\"{metadataRelativePath}\"");
            }
        }

        await File.WriteAllLinesAsync(listFilePath, listLines, Encoding.UTF8);
        var args = ComicArchiveTools.BuildRarCreateArguments(archivePath, listFilePath);
        var result = await ComicArchiveTools.RunProcessAsync(rarExecutable, args, tempRoot);
        if (result.TimedOut)
        {
            throw new InvalidOperationException(UiLocalizationService.GetString("export.error.cbr_timeout"));
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput
                : result.StandardError;
            throw new InvalidOperationException(UiLocalizationService.Format("export.error.cbr_failed", result.ExitCode, detail).Trim());
        }
    }

    private static void CreateFixedLayoutEpubArchive(
        string archivePath,
        Document document,
        IReadOnlyList<ExportQueueEntry> entries,
        string language)
    {
        var directory = Path.GetDirectoryName(archivePath);
        Directory.CreateDirectory(string.IsNullOrWhiteSpace(directory) ? "." : directory);
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        var ordered = entries.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);

        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var mimeStream = new StreamWriter(mimeEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            mimeStream.Write("application/epub+zip");
        }

        AddTextEntry(
            archive,
            "META-INF/container.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");

        AddTextEntry(
            archive,
            "OEBPS/styles/fixed.css",
            "html,body{margin:0;padding:0;width:100%;height:100%;}img{width:100%;height:100%;display:block;object-fit:contain;}");

        var pageItems = new List<(string Id, string XhtmlPath, string ImagePath, string Title)>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            var imageExt = Path.GetExtension(entry.RelativePath);
            var pageId = $"page{i + 1}";
            var imagePath = $"images/{pageId}{imageExt}";
            var xhtmlPath = $"xhtml/{pageId}.xhtml";
            pageItems.Add((pageId, xhtmlPath, imagePath, entry.Item.Label));

            archive.CreateEntryFromFile(entry.FilePath, $"OEBPS/{imagePath}".Replace('\\', '/'), CompressionLevel.Optimal);
            AddTextEntry(
                archive,
                $"OEBPS/{xhtmlPath}",
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>{EscapeXml(entry.Item.Label)}</title><link rel=\"stylesheet\" type=\"text/css\" href=\"../styles/fixed.css\"/></head><body><img src=\"../{imagePath}\" alt=\"{EscapeXml(entry.Item.Label)}\"/></body></html>");
        }

        var languageTag = Document.NormalizeLanguageTag(language, document.BaseLanguage);
        var navItems = string.Join("", pageItems.Select(item =>
            $"<li><a href=\"{item.XhtmlPath}\">{EscapeXml(item.Title)}</a></li>"));

        AddTextEntry(
            archive,
            "OEBPS/nav.xhtml",
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"><head><title>{EscapeXml(document.Name)}</title></head><body><nav epub:type=\"toc\" id=\"toc\"><h1>{EscapeXml(document.Name)}</h1><ol>{navItems}</ol></nav></body></html>");

        var xhtmlManifest = string.Join(
            "",
            pageItems.Select(item =>
                $"<item id=\"{item.Id}\" href=\"{item.XhtmlPath}\" media-type=\"application/xhtml+xml\"/>"));
        var imageManifest = string.Join(
            "",
            pageItems.Select(item =>
                $"<item id=\"{item.Id}-img\" href=\"{item.ImagePath}\" media-type=\"{GetMediaType(item.ImagePath)}\"/>"));
        var manifestItems = xhtmlManifest + imageManifest;
        var spineItems = string.Join("", pageItems.Select(item => $"<itemref idref=\"{item.Id}\"/>"));
        var coverId = pageItems.Count > 0 ? pageItems[0].Id + "-img" : string.Empty;

        AddTextEntry(
            archive,
            "OEBPS/content.opf",
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><package version=\"3.0\" unique-identifier=\"bookid\" xmlns=\"http://www.idpf.org/2007/opf\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"bookid\">urn:uuid:{document.Id}</dc:identifier><dc:title>{EscapeXml(document.Name)}</dc:title><dc:language>{EscapeXml(languageTag)}</dc:language><meta property=\"dcterms:modified\">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</meta>{(string.IsNullOrWhiteSpace(coverId) ? string.Empty : $"<meta name=\"cover\" content=\"{coverId}\"/>")}</metadata><manifest><item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>{manifestItems}</manifest><spine>{spineItems}</spine></package>");

        var pageManifest = pageItems.Select((item, index) => new
        {
            page = index + 1,
            id = item.Id,
            title = item.Title,
            xhtml = item.XhtmlPath,
            image = item.ImagePath
        }).ToArray();
        AddTextEntry(
            archive,
            "OEBPS/page-manifest.json",
            JsonSerializer.Serialize(
                new
                {
                    documentId = document.Id,
                    documentName = document.Name,
                    language = languageTag,
                    generatedAtUtc = DateTime.UtcNow,
                    pages = pageManifest
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AddTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string GetMediaType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".tif" or ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private List<ExportQueueEntry> BuildExportQueueEntries(
        Document doc,
        IEnumerable<DocumentPage> pages,
        IReadOnlyList<string> languages,
        string outputFolder,
        ExportOptions options,
        bool usePerLanguageFolder)
    {
        var entries = new List<ExportQueueEntry>();
        var imageExtension = GetRenderedPageExtension(options.Format);

        foreach (var language in languages)
        {
            foreach (var page in pages)
            {
                var pageIndex = Math.Max(0, doc.IndexOfPage(page.Id)) + Math.Max(0, options.PageNumberStart);
                var languageFolder = usePerLanguageFolder ? ExportPlanning.SanitizeFileNameSegment(language) : null;
                if (options.PerLayerExport)
                {
                    foreach (var layer in page.Layers)
                    {
                        if (options.VisibleLayersOnly && !layer.IsVisible) continue;

                        var fileName = BuildExportFileName(options, doc.Name, pageIndex, layer.Name, language);
                        var relativePath = string.IsNullOrWhiteSpace(languageFolder)
                            ? fileName + imageExtension
                            : Path.Combine(languageFolder, fileName + imageExtension);
                        var filePath = Path.Combine(outputFolder, relativePath);
                        var label = BuildExportQueueLabel(page, pageIndex, layer.Name, language);
                        var item = new ExportQueueItem(label, Path.GetFileName(filePath));
                        entries.Add(new ExportQueueEntry(page, layer.Id, language, filePath, relativePath, item));
                    }
                }
                else
                {
                    var fileName = BuildExportFileName(options, doc.Name, pageIndex, layerName: null, language);
                    var relativePath = string.IsNullOrWhiteSpace(languageFolder)
                        ? fileName + imageExtension
                        : Path.Combine(languageFolder, fileName + imageExtension);
                    var filePath = Path.Combine(outputFolder, relativePath);
                    var label = BuildExportQueueLabel(page, pageIndex, layerName: null, language);
                    var item = new ExportQueueItem(label, Path.GetFileName(filePath));
                    entries.Add(new ExportQueueEntry(page, null, language, filePath, relativePath, item));
                }
            }
        }

        return entries;
    }

    private static string BuildExportQueueLabel(DocumentPage page, int pageIndex, string? layerName, string language)
    {
        var pageLabel = string.IsNullOrWhiteSpace(page.Name)
            ? $"Page {pageIndex:D2}"
            : $"Page {pageIndex:D2} - {page.Name}";

        if (string.IsNullOrWhiteSpace(layerName))
        {
            return $"[{language}] {pageLabel}";
        }

        return $"[{language}] {pageLabel} / {layerName}";
    }

    private ExportProgressDialogState CreateExportProgressDialog(ObservableCollection<ExportQueueItem> items, int totalCount)
    {
        var listView = new ListView
        {
            ItemsSource = items,
            MaxHeight = 320
        };

        var templateXaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
            "  <Grid ColumnSpacing='12'>" +
            "    <Grid.ColumnDefinitions>" +
            "      <ColumnDefinition Width='*' />" +
            "      <ColumnDefinition Width='Auto' />" +
            "    </Grid.ColumnDefinitions>" +
            "    <StackPanel Spacing='2'>" +
            "      <TextBlock Text='{Binding Label}' TextTrimming='CharacterEllipsis' />" +
            "      <TextBlock Text='{Binding FileName}' FontSize='11' Foreground='Gray' TextTrimming='CharacterEllipsis' />" +
            "    </StackPanel>" +
            "    <TextBlock Grid.Column='1' Text='{Binding Status}' Foreground='Gray' VerticalAlignment='Center' />" +
            "  </Grid>" +
            "</DataTemplate>";

        listView.ItemTemplate = (DataTemplate)XamlReader.Load(templateXaml);

        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, totalCount),
            Value = 0
        };

        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
            Text = L("export.dialog.starting_export")
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(statusText);
        panel.Children.Add(progressBar);
        panel.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = L("export.dialog.queue_title"),
            Content = panel,
            CloseButtonText = L("common.close"),
            XamlRoot = Content.XamlRoot
        };

        return new ExportProgressDialogState(dialog, progressBar, statusText);
    }

    private async Task ExportPageAsync(
        DocumentPage page,
        string filePath,
        ExportOptions options,
        Guid? singleLayerId,
        string language,
        Document translationContext)
    {
        var doc = _editorState.Document;
        if (doc == null || MainCanvas.Device == null) return;

        await EnsureBackgroundLoadedAsync(page);
        await EnsureFloatingImagesLoadedAsync(page);

        var width = (int)Math.Ceiling(page.Size.Width);
        var height = (int)Math.Ceiling(page.Size.Height);
        if (width <= 0 || height <= 0) return;
        var exportDpi = Math.Max(72, options.Dpi);

        var exportTransform = CreatePixelExportTransform(exportDpi);
        using var renderTarget = new CanvasRenderTarget(
            MainCanvas.Device,
            PixelsToDipsForDpi(width, exportDpi),
            PixelsToDipsForDpi(height, exportDpi),
            exportDpi);
        using (var ds = renderTarget.CreateDrawingSession())
        {
            var clearColor = options.Transparent || options.OverlayOnly
                ? Windows.UI.Color.FromArgb(0, 0, 0, 0)
                : Windows.UI.Color.FromArgb(255, 255, 255, 255);
            ds.Clear(clearColor);
            var background = options.OverlayOnly ? null : _editorState.GetBackgroundImageForPage(page.Id);
            var renderer = _renderer ?? new DocumentRenderer(new ViewTransform());
            renderer.RenderPageContent(
                ds,
                page,
                background,
                includeHiddenLayers: !options.VisibleLayersOnly,
                singleLayerId: singleLayerId,
                transformOverride: exportTransform,
                panelImageResolver: GetPanelImage,
                floatingImageResolver: GetFloatingImage,
                textFillImageResolver: GetTextFillImage,
                translationDocument: translationContext,
                renderPanelBorders: options.DrawPanelBorders,
                renderPanelMembershipBadges: false);
        }

        CanvasRenderTarget finalTarget = renderTarget;
        CanvasRenderTarget? selectionTarget = null;

        if (options.SelectionOnly && doc.SelectedBalloon != null && doc.ActivePageId == page.Id)
        {
            var bounds = doc.SelectedBalloon.Bounds;
            var cropWidth = (int)Math.Ceiling(bounds.Width);
            var cropHeight = (int)Math.Ceiling(bounds.Height);
            if (cropWidth > 0 && cropHeight > 0)
            {
                selectionTarget = new CanvasRenderTarget(
                    MainCanvas.Device,
                    PixelsToDipsForDpi(cropWidth, exportDpi),
                    PixelsToDipsForDpi(cropHeight, exportDpi),
                    exportDpi);
                using var dsCrop = selectionTarget.CreateDrawingSession();
                var clearColor = options.Transparent || options.OverlayOnly
                    ? Windows.UI.Color.FromArgb(0, 0, 0, 0)
                    : Windows.UI.Color.FromArgb(255, 255, 255, 255);
                dsCrop.Clear(clearColor);
                dsCrop.DrawImage(
                    renderTarget,
                    -PixelsToDipsForDpi(bounds.X, exportDpi),
                    -PixelsToDipsForDpi(bounds.Y, exportDpi));
                finalTarget = selectionTarget;
            }
        }

        await SaveCanvasRenderTargetToFileAsync(finalTarget, filePath, options.Format, options.Quality);

        selectionTarget?.Dispose();

        if (options.IncludeMetadata)
        {
            await WriteExportMetadataAsync(page, filePath, options, singleLayerId, language, translationContext);
        }
    }

    private static float PixelsToDipsForDpi(int pixels, int dpi)
    {
        var safeDpi = Math.Max(72, dpi);
        return Math.Max(1f, pixels) * 96f / safeDpi;
    }

    private static float PixelsToDipsForDpi(float pixels, int dpi)
    {
        var safeDpi = Math.Max(72, dpi);
        return pixels * 96f / safeDpi;
    }

    private static Matrix3x2 CreatePixelExportTransform(int dpi)
    {
        var safeDpi = Math.Max(72, dpi);
        var scale = 96f / safeDpi;
        return Matrix3x2.CreateScale(scale, scale);
    }

    private static async Task<(int width, int height)> TryReadImagePixelSizeAsync(
        byte[] imageBytes,
        int fallbackWidth,
        int fallbackHeight)
    {
        try
        {
            using var imageStream = new InMemoryRandomAccessStream();
            await imageStream.WriteAsync(imageBytes.AsBuffer());
            imageStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            return (
                Math.Max(1, (int)decoder.PixelWidth),
                Math.Max(1, (int)decoder.PixelHeight));
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Failed to decode exported image size while packaging PDF.", ex);
            return (Math.Max(1, fallbackWidth), Math.Max(1, fallbackHeight));
        }
    }

    private static async Task SaveCanvasRenderTargetToFileAsync(
        CanvasRenderTarget renderTarget,
        string filePath,
        ExportFormat format,
        int qualityPercent)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var quality = Math.Clamp(qualityPercent / 100f, 0.0f, 1.0f);
        if (format == ExportFormat.Webp && !TryResolveCanvasFormat("Webp", out _))
        {
            await SaveCanvasRenderTargetAsWebpViaEncoderAsync(renderTarget, filePath, quality);
            return;
        }

        using var stream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(stream, ResolveCanvasFormat(format), (float)quality);
        stream.Seek(0);
        using var fileStream = File.Create(filePath);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
    }

    private static async Task SaveCanvasRenderTargetAsWebpViaEncoderAsync(
        CanvasRenderTarget renderTarget,
        string filePath,
        float quality)
    {
        using var pngStream = new InMemoryRandomAccessStream();
        await renderTarget.SaveAsync(pngStream, CanvasBitmapFileFormat.Png, 1f);
        pngStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(pngStream);
        var straightPixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        var straightPixels = straightPixelData.DetachPixelData();
        var premultipliedPixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
        var premultipliedPixels = premultipliedPixelData.DetachPixelData();

        foreach (var encoderId in ResolveWebpEncoderIds())
        {
            if (await TryEncodeWebpAsync(
                    encoderId,
                    filePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    quality,
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Straight,
                    straightPixels))
            {
                return;
            }

            if (await TryEncodeWebpAsync(
                    encoderId,
                    filePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    quality,
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Ignore,
                    straightPixels))
            {
                return;
            }

            if (await TryEncodeWebpAsync(
                    encoderId,
                    filePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    quality,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    premultipliedPixels))
            {
                return;
            }

            if (await TryEncodeWebpAsync(
                    encoderId,
                    filePath,
                    decoder.PixelWidth,
                    decoder.PixelHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    quality,
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    premultipliedPixels))
            {
                return;
            }
        }

        throw new InvalidOperationException();
    }

    private static async Task<bool> TryEncodeWebpAsync(
        Guid encoderId,
        string filePath,
        uint width,
        uint height,
        double dpiX,
        double dpiY,
        float quality,
        BitmapPixelFormat pixelFormat,
        BitmapAlphaMode alphaMode,
        byte[] pixels)
    {
        try
        {
            using var outputStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream);
            encoder.SetPixelData(
                pixelFormat,
                alphaMode,
                width,
                height,
                dpiX,
                dpiY,
                pixels);

            try
            {
                var propertySet = new BitmapPropertySet
                {
                    {
                        "ImageQuality",
                        new BitmapTypedValue((float)Math.Clamp(quality, 0f, 1f), Windows.Foundation.PropertyType.Single)
                    }
                };
                await encoder.BitmapProperties.SetPropertiesAsync(propertySet);
            }
            catch
            {
            }

            await encoder.FlushAsync();
            outputStream.Seek(0);
            using var fileStream = File.Create(filePath);
            await outputStream.AsStreamForRead().CopyToAsync(fileStream);
            return true;
        }
        catch (Exception ex) when (ex is COMException || ex is InvalidOperationException)
        {
            StartupLogger.Log($"WebP encoder attempt failed (encoder={encoderId}, pixel={pixelFormat}, alpha={alphaMode})", ex);
            return false;
        }
    }

    private static IReadOnlyList<Guid> ResolveWebpEncoderIds()
    {
        var ids = new List<Guid>();

        static void AddUnique(List<Guid> buffer, Guid candidate)
        {
            if (candidate == Guid.Empty || buffer.Contains(candidate))
            {
                return;
            }

            buffer.Add(candidate);
        }

        const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        var encoderType = typeof(BitmapEncoder);
        var webpProperty = encoderType.GetProperty("WebpEncoderId", PublicStatic);
        if (webpProperty?.PropertyType == typeof(Guid) &&
            webpProperty.GetValue(null) is Guid knownWebpGuid)
        {
            AddUnique(ids, knownWebpGuid);
        }

        var listMethod = encoderType.GetMethod("GetEncoderInformationEnumerator", PublicStatic, null, Type.EmptyTypes, null);
        if (listMethod?.Invoke(null, null) is System.Collections.IEnumerable codecs)
        {
            foreach (var codec in codecs)
            {
                if (codec == null || !IsWebpCodec(codec))
                {
                    continue;
                }

                var codecType = codec.GetType();
                var containerFormat = codecType.GetProperty("ContainerFormat", PublicInstance);
                if (containerFormat?.GetValue(codec) is Guid containerGuid)
                {
                    AddUnique(ids, containerGuid);
                }

                var codecId = codecType.GetProperty("CodecId", PublicInstance);
                if (codecId?.GetValue(codec) is Guid codecGuid)
                {
                    AddUnique(ids, codecGuid);
                }
            }
        }

        AddUnique(ids, KnownWebpContainerFormatGuid);
        return ids;
    }

    private static bool IsWebpCodec(object codec)
    {
        bool ContainsWebpInProperty(string propertyName)
        {
            var property = codec.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property?.GetValue(codec) is not System.Collections.IEnumerable values)
            {
                return false;
            }

            foreach (var value in values)
            {
                var text = value?.ToString();
                if (!string.IsNullOrWhiteSpace(text) &&
                    text.IndexOf("webp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        return ContainsWebpInProperty("FileExtensions") ||
               ContainsWebpInProperty("MimeTypes");
    }

    private ExportOptions CreateExportOptionsFromPreferences()
    {
        var prefs = _preferences.ExportDefaults;
        var format = prefs.Format?.Trim().ToLowerInvariant() switch
        {
            "jpeg" or "jpg" => ExportFormat.Jpeg,
            "tiff" or "tif" => ExportFormat.Tiff,
            "webp" => ExportFormat.Webp,
            "pdf" => ExportFormat.Pdf,
            "cbz" => ExportFormat.Cbz,
            "cbr" => ExportFormat.Cbr,
            "epub" => ExportFormat.Epub,
            _ => ExportFormat.Png
        };

        return new ExportOptions
        {
            Format = format,
            Dpi = Math.Clamp(prefs.Dpi, 72, 2400),
            Quality = Math.Clamp(prefs.Quality, 1, 100),
            Transparent = prefs.Transparent,
            OverlayOnly = prefs.OverlayOnly,
            SelectionOnly = prefs.SelectionOnly,
            VisibleLayersOnly = prefs.VisibleLayersOnly,
            DrawPanelBorders = prefs.DrawPanelBorders,
            IncludeMetadata = prefs.IncludeMetadata,
            BatchExport = prefs.BatchExport,
            CurrentPageOnly = prefs.CurrentPageOnly,
            PerLayerExport = prefs.PerLayerExport,
            ExportAllLanguages = prefs.ExportAllLanguages,
            ExportVisibleLanguagesOnly = prefs.ExportVisibleLanguagesOnly,
            PerLanguageFolders = prefs.PerLanguageFolders,
            IncludeLanguageCode = prefs.IncludeLanguageCode,
            LanguageSubset = string.Empty,
            PageNumberStart = Math.Max(0, prefs.PageNumberStart),
            PageNumberPadding = Math.Clamp(prefs.PageNumberPadding, 1, 8),
            FilenamePattern = string.IsNullOrWhiteSpace(prefs.FilenamePattern)
                ? "{document}-page-{page}"
                : prefs.FilenamePattern.Trim(),
            OutputFolderOverride = GetValidDefaultExportFolder(),
            RarExecutablePath = prefs.RarExecutablePath ?? string.Empty
        };
    }

    private void SaveExportOptionsToPreferences(ExportOptions options)
    {
        var prefs = _preferences.ExportDefaults;
        prefs.Format = options.Format switch
        {
            ExportFormat.Jpeg => "jpeg",
            ExportFormat.Tiff => "tiff",
            ExportFormat.Webp => "webp",
            ExportFormat.Pdf => "pdf",
            ExportFormat.Cbz => "cbz",
            ExportFormat.Cbr => "cbr",
            ExportFormat.Epub => "epub",
            _ => "png"
        };
        prefs.Dpi = options.Dpi;
        prefs.Quality = options.Quality;
        prefs.Transparent = options.Transparent;
        prefs.OverlayOnly = options.OverlayOnly;
        prefs.SelectionOnly = options.SelectionOnly;
        prefs.VisibleLayersOnly = options.VisibleLayersOnly;
        prefs.DrawPanelBorders = options.DrawPanelBorders;
        prefs.IncludeMetadata = options.IncludeMetadata;
        prefs.BatchExport = options.BatchExport;
        prefs.CurrentPageOnly = options.CurrentPageOnly;
        prefs.PerLayerExport = options.PerLayerExport;
        prefs.ExportAllLanguages = options.ExportAllLanguages;
        prefs.ExportVisibleLanguagesOnly = options.ExportVisibleLanguagesOnly;
        prefs.PerLanguageFolders = options.PerLanguageFolders;
        prefs.IncludeLanguageCode = options.IncludeLanguageCode;
        prefs.PageNumberStart = options.PageNumberStart;
        prefs.PageNumberPadding = options.PageNumberPadding;
        prefs.FilenamePattern = options.FilenamePattern;
        if (!string.IsNullOrWhiteSpace(options.OutputFolderOverride))
        {
            prefs.DefaultFolder = options.OutputFolderOverride;
        }
        if (!string.IsNullOrWhiteSpace(options.RarExecutablePath))
        {
            prefs.RarExecutablePath = options.RarExecutablePath;
        }
        SavePreferences();
    }

    private string? GetValidDefaultExportFolder()
    {
        var folder = _preferences.ExportDefaults.DefaultFolder?.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        try
        {
            return Directory.Exists(folder) ? folder : null;
        }
        catch
        {
            return null;
        }
    }

    private static Document CreateExportTranslationDocument(Document source, string language)
    {
        var normalized = Document.NormalizeLanguageTag(language, source.BaseLanguage);
        if (string.Equals(source.ActiveLanguage, normalized, StringComparison.OrdinalIgnoreCase) &&
            source.TranslationCompareMode == TranslationCompareMode.None)
        {
            return source;
        }

        var clone = source.Clone();
        clone.SetActiveLanguage(normalized);
        clone.SetTranslationCompareMode(TranslationCompareMode.None);
        clone.SetCompareLanguage(null);
        return clone;
    }

    private async Task WriteExportMetadataAsync(
        DocumentPage page,
        string filePath,
        ExportOptions options,
        Guid? singleLayerId,
        string language,
        Document translationContext)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        IEnumerable<Balloon> balloons = page.AllBalloons;

        if (singleLayerId.HasValue)
        {
            var layer = page.FindLayer(singleLayerId.Value);
            balloons = layer?.Balloons ?? Enumerable.Empty<Balloon>();
        }
        else if (options.VisibleLayersOnly)
        {
            balloons = page.Layers.Where(l => l.IsVisible).SelectMany(l => l.Balloons);
        }

        if (options.SelectionOnly && doc.SelectedBalloon != null && doc.ActivePageId == page.Id)
        {
            balloons = new[] { doc.SelectedBalloon };
        }

        var pageIndex = Math.Max(0, doc.IndexOfPage(page.Id)) + 1;
        var metadata = new
        {
            document = new
            {
                id = doc.Id,
                name = doc.Name
            },
            page = new
            {
                id = page.Id,
                name = page.Name,
                index = pageIndex,
                readingDirection = page.ReadingDirection.ToString(),
                width = page.Size.Width,
                height = page.Size.Height,
                panelReadingOrder = page.Panels.OrderBy(panel => panel.Order).Select(panel => panel.Id).ToArray()
            },
            exportedAtUtc = DateTime.UtcNow,
            panels = page.Panels.Select(panel => new
            {
                id = panel.Id,
                name = panel.Name,
                order = panel.Order,
                x = panel.Bounds.X,
                y = panel.Bounds.Y,
                width = panel.Bounds.Width,
                height = panel.Bounds.Height
            }).ToArray(),
            balloons = balloons.Select(balloon => new
            {
                id = balloon.Id,
                panelId = balloon.PanelId,
                readingOrder = page.GetBalloonReadingOrder(balloon),
                x = balloon.Position.X,
                y = balloon.Position.Y,
                width = balloon.ComputedSize.Width,
                height = balloon.ComputedSize.Height,
                text = translationContext.GetBalloonDisplayText(balloon, language),
                sourceText = balloon.Text,
                language
            }).ToArray()
        };

        var metadataPath = filePath + ".metadata.json";
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json, Encoding.UTF8);
    }

    private static string BuildExportFileName(ExportOptions options, string documentName, int pageNumber, string? layerName, string language)
    {
        var includeLanguage = options.IncludeLanguageCode || options.ExportAllLanguages;
        var pattern = NormalizeExportPattern(options.FilenamePattern, ShouldExportAllPages(options), options.PerLayerExport, includeLanguage);
        return ExpandExportPattern(
            pattern,
            documentName,
            pageNumber,
            options.PageNumberPadding,
            layerName,
            language);
    }

    private static string BuildArchiveFileName(string documentName, string language, bool includeLanguageCode)
    {
        var safeDocument = ExportPlanning.SanitizeFileNameSegment(documentName);
        if (!includeLanguageCode)
        {
            return safeDocument;
        }

        var safeLanguage = ExportPlanning.SanitizeFileNameSegment(language);
        return $"{safeDocument}-{safeLanguage}";
    }

    private static bool IsArchiveFormat(ExportFormat format)
    {
        return format is ExportFormat.Cbz or ExportFormat.Cbr or ExportFormat.Epub;
    }

    private static string GetOutputExtension(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Png => ".png",
            ExportFormat.Jpeg => ".jpg",
            ExportFormat.Tiff => ".tiff",
            ExportFormat.Webp => ".webp",
            ExportFormat.Pdf => ".pdf",
            ExportFormat.Cbz => ".cbz",
            ExportFormat.Cbr => ".cbr",
            ExportFormat.Epub => ".epub",
            _ => ".png"
        };
    }

    private static bool IsImageFormat(ExportFormat format)
    {
        return format is ExportFormat.Png or ExportFormat.Jpeg or ExportFormat.Tiff or ExportFormat.Webp;
    }

    private static bool ShouldExportAllPages(ExportOptions options)
    {
        if (IsImageFormat(options.Format))
        {
            return !options.CurrentPageOnly;
        }
        return options.BatchExport;
    }

    private static string GetRenderedPageExtension(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Cbz or ExportFormat.Cbr or ExportFormat.Epub or ExportFormat.Pdf => ".jpg",
            _ => GetOutputExtension(format)
        };
    }

    private static string GetFormatLabel(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Png => UiLocalizationService.GetString("export.format.png"),
            ExportFormat.Jpeg => UiLocalizationService.GetString("export.format.jpeg"),
            ExportFormat.Tiff => UiLocalizationService.GetString("export.format.tiff"),
            ExportFormat.Webp => UiLocalizationService.GetString("export.format.webp"),
            ExportFormat.Pdf => UiLocalizationService.GetString("export.format.pdf"),
            ExportFormat.Cbz => UiLocalizationService.GetString("export.format.cbz"),
            ExportFormat.Cbr => UiLocalizationService.GetString("export.format.cbr"),
            ExportFormat.Epub => UiLocalizationService.GetString("export.format.epub"),
            _ => UiLocalizationService.GetString("export.format.generic")
        };
    }

    private static CanvasBitmapFileFormat ResolveCanvasFormat(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Png => CanvasBitmapFileFormat.Png,
            ExportFormat.Jpeg => CanvasBitmapFileFormat.Jpeg,
            ExportFormat.Tiff => CanvasBitmapFileFormat.Tiff,
            ExportFormat.Webp => TryResolveCanvasFormat("Webp", out var webpFormat) ? webpFormat : CanvasBitmapFileFormat.Png,
            ExportFormat.Pdf or ExportFormat.Cbz or ExportFormat.Cbr or ExportFormat.Epub => CanvasBitmapFileFormat.Jpeg,
            _ => CanvasBitmapFileFormat.Png
        };
    }

    private static List<ExportFormatOption> GetSupportedExportFormats()
    {
        var formats = new List<ExportFormatOption>
        {
            new(ExportFormat.Png, UiLocalizationService.GetString("export.format.png"), ".png", supportsTransparency: true, supportsQuality: false),
            new(ExportFormat.Jpeg, UiLocalizationService.GetString("export.format.jpeg"), ".jpg", supportsTransparency: false, supportsQuality: true),
            new(ExportFormat.Tiff, UiLocalizationService.GetString("export.format.tiff"), ".tiff", supportsTransparency: true, supportsQuality: false),
            new(ExportFormat.Pdf, UiLocalizationService.GetString("export.format.pdf"), ".pdf", supportsTransparency: false, supportsQuality: true),
            new(ExportFormat.Cbz, UiLocalizationService.GetString("export.format.cbz"), ".cbz", supportsTransparency: false, supportsQuality: true),
            new(ExportFormat.Cbr, UiLocalizationService.GetString("export.format.cbr"), ".cbr", supportsTransparency: false, supportsQuality: true),
            new(ExportFormat.Epub, UiLocalizationService.GetString("export.format.epub"), ".epub", supportsTransparency: false, supportsQuality: true)
        };

        return formats;
    }

    private static bool TryResolveCanvasFormat(string formatName, out CanvasBitmapFileFormat format)
    {
        return Enum.TryParse(formatName, ignoreCase: true, out format);
    }

    private static string NormalizeExportPattern(string? pattern, bool includePage, bool includeLayer, bool includeLanguage)
    {
        return ExportPlanning.NormalizePattern(pattern, includePage, includeLayer, includeLanguage);
    }

    private static string ExpandExportPattern(
        string pattern,
        string documentName,
        int pageNumber,
        int pagePadding,
        string? layerName,
        string? language)
    {
        return ExportPlanning.ExpandPattern(pattern, documentName, pageNumber, pagePadding, layerName, language);
    }

    private static string SanitizeFileName(string name)
    {
        return ExportPlanning.SanitizeFileNameSegment(name);
    }

    private sealed class ExportFormatOption
    {
        public ExportFormatOption(ExportFormat format, string label, string extension, bool supportsTransparency, bool supportsQuality)
        {
            Format = format;
            Label = label;
            Extension = extension;
            SupportsTransparency = supportsTransparency;
            SupportsQuality = supportsQuality;
        }

        public ExportFormat Format { get; }
        public string Label { get; }
        public string Extension { get; }
        public bool SupportsTransparency { get; }
        public bool SupportsQuality { get; }
    }


}
