using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Letterist;

public sealed partial class MainWindow : Window
{
    private Window? _preferencesWindow;

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowPreferencesDialogAsync();
    }

    private async Task ShowPreferencesDialogAsync()
    {
        await ShowSimplePreferencesDialogAsync();
    }

    private async Task ShowSimplePreferencesDialogAsync()
    {
        var working = _preferences.Clone();
        var categoryPanels = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);
        var categoryItems = new Dictionary<string, ListBoxItem>(StringComparer.OrdinalIgnoreCase);

        var searchBox = new AutoSuggestBox
        {
            PlaceholderText = L("prefs.search.placeholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var categoryList = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            MinWidth = 220
        };

        var panelHost = new Grid();
        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };

        FrameworkElement Field(string label, UIElement control)
        {
            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            panel.Children.Add(control);
            return panel;
        }

        StackPanel AddCategory(string name, string description)
        {
            var stack = new StackPanel { Spacing = 10 };
            stack.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 140, 140, 140))
            });

            var item = new ListBoxItem { Content = name };
            categoryList.Items.Add(item);
            categoryItems[name] = item;
            categoryPanels[name] = stack;

            panelHost.Children.Add(new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Visibility = Visibility.Collapsed
            });

            return stack;
        }

        var languageCombo = new ComboBox();
        var pageWidthBox = new NumberBox();
        var pageHeightBox = new NumberBox();
        var dpiBox = new NumberBox();
        var recentFilesBox = new NumberBox();
        var autosaveBox = new NumberBox();
        var backupCountBox = new NumberBox();

        var unitSystemCombo = new ComboBox();
        var showRulersToggle = new ToggleSwitch();
        var gridColorBox = new TextBox();
        var showGridToggle = new ToggleSwitch();
        var snapToGridToggle = new ToggleSwitch();
        var gridMinorSizeBox = new NumberBox();
        var gridMajorSizeBox = new NumberBox();

        var workspaceColorBox = new TextBox();
        var checkerLightBox = new TextBox();
        var checkerDarkBox = new TextBox();
        var handleSizeBox = new NumberBox();
        var zoomStepBox = new NumberBox();
        var scrollSpeedBox = new NumberBox();

        var exportFormatCombo = new ComboBox();
        var exportDpiBox = new NumberBox();
        var exportQualityBox = new NumberBox();
        var exportFolderBox = new TextBox();
        var exportPatternBox = new TextBox();
        var exportRarPathBox = new TextBox();

        var undoLimitBox = new NumberBox();
        var thumbnailCacheBox = new NumberBox();
        var hwAccelToggle = new ToggleSwitch();
        var bgRenderingToggle = new ToggleSwitch();
        var memoryLimitBox = new NumberBox();

        ConfigureControlSources(
            languageCombo,
            unitSystemCombo,
            exportFormatCombo);

        BuildCategoryPanels(
            AddCategory,
            Field,
            languageCombo,
            pageWidthBox,
            pageHeightBox,
            dpiBox,
            recentFilesBox,
            autosaveBox,
            backupCountBox,
            unitSystemCombo,
            showRulersToggle,
            gridColorBox,
            showGridToggle,
            snapToGridToggle,
            gridMinorSizeBox,
            gridMajorSizeBox,
            workspaceColorBox,
            checkerLightBox,
            checkerDarkBox,
            handleSizeBox,
            zoomStepBox,
            scrollSpeedBox,
            exportFormatCombo,
            exportDpiBox,
            exportQualityBox,
            exportFolderBox,
            exportPatternBox,
            exportRarPathBox,
            undoLimitBox,
            thumbnailCacheBox,
            hwAccelToggle,
            bgRenderingToggle,
            memoryLimitBox);

        ApplyPreferencesToControls(
            working,
            languageCombo,
            pageWidthBox,
            pageHeightBox,
            dpiBox,
            recentFilesBox,
            autosaveBox,
            backupCountBox,
            unitSystemCombo,
            showRulersToggle,
            gridColorBox,
            showGridToggle,
            snapToGridToggle,
            gridMinorSizeBox,
            gridMajorSizeBox,
            workspaceColorBox,
            checkerLightBox,
            checkerDarkBox,
            handleSizeBox,
            zoomStepBox,
            scrollSpeedBox,
            exportFormatCombo,
            exportDpiBox,
            exportQualityBox,
            exportFolderBox,
            exportPatternBox,
            exportRarPathBox,
            undoLimitBox,
            thumbnailCacheBox,
            hwAccelToggle,
            bgRenderingToggle,
            memoryLimitBox);

        WirePreferenceDialogInteractions(
            searchBox,
            categoryList,
            categoryItems,
            categoryPanels,
            panelHost);

        var importButton = new Button { Content = L("prefs.button.import") };
        importButton.Click += async (_, _) =>
        {
            await ImportPreferencesIntoControlsAsync(
                statusText,
                languageCombo,
                pageWidthBox,
                pageHeightBox,
                dpiBox,
                recentFilesBox,
                autosaveBox,
                backupCountBox,
                unitSystemCombo,
                showRulersToggle,
                gridColorBox,
                showGridToggle,
                snapToGridToggle,
                gridMinorSizeBox,
                gridMajorSizeBox,
                workspaceColorBox,
                checkerLightBox,
                checkerDarkBox,
                handleSizeBox,
                zoomStepBox,
                scrollSpeedBox,
                exportFormatCombo,
                exportDpiBox,
                exportQualityBox,
                exportFolderBox,
                exportPatternBox,
                exportRarPathBox,
                undoLimitBox,
                thumbnailCacheBox,
                hwAccelToggle,
                bgRenderingToggle,
                memoryLimitBox);
        };

        var exportButton = new Button { Content = L("prefs.button.export") };
        var resetButton = new Button { Content = L("prefs.button.reset") };
        resetButton.Click += (_, _) =>
        {
            ApplyPreferencesToControls(
                AppPreferences.CreateDefault(),
                languageCombo,
                pageWidthBox,
                pageHeightBox,
                dpiBox,
                recentFilesBox,
                autosaveBox,
                backupCountBox,
                unitSystemCombo,
                showRulersToggle,
                gridColorBox,
                showGridToggle,
                snapToGridToggle,
                gridMinorSizeBox,
                gridMajorSizeBox,
                workspaceColorBox,
                checkerLightBox,
                checkerDarkBox,
                handleSizeBox,
                zoomStepBox,
                scrollSpeedBox,
                exportFormatCombo,
                exportDpiBox,
                exportQualityBox,
                exportFolderBox,
                exportPatternBox,
                exportRarPathBox,
                undoLimitBox,
                thumbnailCacheBox,
                hwAccelToggle,
                bgRenderingToggle,
                memoryLimitBox);
            statusText.Text = L("prefs.status.defaults_loaded");
            statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        };

        await ShowPreferencesDialogCoreAsync(
            searchBox,
            categoryList,
            panelHost,
            statusText,
            importButton,
            exportButton,
            resetButton,
            () =>
            {
                var result = BuildPreferencesFromControls(
                    languageCombo,
                    pageWidthBox,
                    pageHeightBox,
                    dpiBox,
                    recentFilesBox,
                    autosaveBox,
                    backupCountBox,
                    unitSystemCombo,
                    showRulersToggle,
                    gridColorBox,
                    showGridToggle,
                    snapToGridToggle,
                    gridMinorSizeBox,
                    gridMajorSizeBox,
                    workspaceColorBox,
                    checkerLightBox,
                    checkerDarkBox,
                    handleSizeBox,
                    zoomStepBox,
                    scrollSpeedBox,
                    exportFormatCombo,
                    exportDpiBox,
                    exportQualityBox,
                    exportFolderBox,
                    exportPatternBox,
                    exportRarPathBox,
                    undoLimitBox,
                    thumbnailCacheBox,
                    hwAccelToggle,
                    bgRenderingToggle,
                    memoryLimitBox);

                if (!result.Success)
                {
                    return result;
                }

                result.Preferences.Normalize();
                return result;
            });
    }

    private void ConfigureControlSources(
        ComboBox languageCombo,
        ComboBox unitSystemCombo,
        ComboBox exportFormatCombo)
    {
        languageCombo.ItemsSource = UiLocalizationService.SupportedLanguages
            .Select(language => new ComboBoxItem
            {
                Content = UiLocalizationService.GetString($"language.name.{language.Tag}"),
                Tag = language.Tag
            })
            .ToList();
        unitSystemCombo.ItemsSource = Enum.GetValues<UnitSystemPreference>().ToArray();
        exportFormatCombo.ItemsSource = new[] { "png", "jpeg", "tiff", "webp", "pdf", "cbz", "cbr", "epub" };
    }

    private void BuildCategoryPanels(
        Func<string, string, StackPanel> addCategory,
        Func<string, UIElement, FrameworkElement> field,
        ComboBox languageCombo,
        NumberBox pageWidthBox,
        NumberBox pageHeightBox,
        NumberBox dpiBox,
        NumberBox recentFilesBox,
        NumberBox autosaveBox,
        NumberBox backupCountBox,
        ComboBox unitSystemCombo,
        ToggleSwitch showRulersToggle,
        TextBox gridColorBox,
        ToggleSwitch showGridToggle,
        ToggleSwitch snapToGridToggle,
        NumberBox gridMinorSizeBox,
        NumberBox gridMajorSizeBox,
        TextBox workspaceColorBox,
        TextBox checkerLightBox,
        TextBox checkerDarkBox,
        NumberBox handleSizeBox,
        NumberBox zoomStepBox,
        NumberBox scrollSpeedBox,
        ComboBox exportFormatCombo,
        NumberBox exportDpiBox,
        NumberBox exportQualityBox,
        TextBox exportFolderBox,
        TextBox exportPatternBox,
        TextBox exportRarPathBox,
        NumberBox undoLimitBox,
        NumberBox thumbnailCacheBox,
        ToggleSwitch hwAccelToggle,
        ToggleSwitch bgRenderingToggle,
        NumberBox memoryLimitBox)
    {
        var general = addCategory(L("prefs.category.general"), L("prefs.category.general.desc"));
        general.Children.Add(field(L("prefs.field.language"), languageCombo));
        general.Children.Add(field(L("prefs.field.default_page_width"), pageWidthBox));
        general.Children.Add(field(L("prefs.field.default_page_height"), pageHeightBox));
        general.Children.Add(field(L("prefs.field.default_dpi"), dpiBox));
        general.Children.Add(field(L("prefs.field.recent_files_count"), recentFilesBox));
        general.Children.Add(field(L("prefs.field.autosave_interval"), autosaveBox));
        general.Children.Add(field(L("prefs.field.backup_count"), backupCountBox));

        showRulersToggle.Header = L("prefs.field.show_rulers");
        var units = addCategory(L("prefs.category.units"), L("prefs.category.units.desc"));
        units.Children.Add(field(L("prefs.field.unit_system"), unitSystemCombo));
        units.Children.Add(showRulersToggle);

        showGridToggle.Header = L("prefs.field.show_grid_default");
        snapToGridToggle.Header = L("prefs.field.snap_to_grid_default");
        var canvas = addCategory(L("prefs.category.canvas"), L("prefs.category.canvas.desc"));
        canvas.Children.Add(field(L("prefs.field.workspace_bg"), workspaceColorBox));
        canvas.Children.Add(field(L("prefs.field.checker_light"), checkerLightBox));
        canvas.Children.Add(field(L("prefs.field.checker_dark"), checkerDarkBox));
        canvas.Children.Add(field(L("prefs.field.grid_color"), gridColorBox));
        canvas.Children.Add(showGridToggle);
        canvas.Children.Add(snapToGridToggle);
        canvas.Children.Add(field(L("prefs.field.grid_minor_size"), gridMinorSizeBox));
        canvas.Children.Add(field(L("prefs.field.grid_major_size"), gridMajorSizeBox));
        canvas.Children.Add(field(L("prefs.field.handle_size"), handleSizeBox));
        canvas.Children.Add(field(L("prefs.field.zoom_step"), zoomStepBox));
        canvas.Children.Add(field(L("prefs.field.scroll_speed"), scrollSpeedBox));

        var export = addCategory(L("prefs.category.export"), L("prefs.category.export.desc"));
        export.Children.Add(field(L("prefs.field.format"), exportFormatCombo));
        export.Children.Add(field(L("prefs.field.dpi"), exportDpiBox));
        export.Children.Add(field(L("prefs.field.quality"), exportQualityBox));
        export.Children.Add(field(L("prefs.field.default_folder"), exportFolderBox));
        export.Children.Add(field(L("prefs.field.filename_pattern"), exportPatternBox));
        export.Children.Add(field(L("prefs.field.rar_executable"), exportRarPathBox));

        hwAccelToggle.Header = L("prefs.field.hardware_acceleration");
        bgRenderingToggle.Header = L("prefs.field.background_rendering");
        var performance = addCategory(L("prefs.category.performance"), L("prefs.category.performance.desc"));
        performance.Children.Add(field(L("prefs.field.undo_limit"), undoLimitBox));
        performance.Children.Add(field(L("prefs.field.thumbnail_cache"), thumbnailCacheBox));
        performance.Children.Add(hwAccelToggle);
        performance.Children.Add(bgRenderingToggle);
        performance.Children.Add(field(L("prefs.field.memory_limit"), memoryLimitBox));
    }

    private void ApplyPreferencesToControls(
        AppPreferences preferences,
        ComboBox languageCombo,
        NumberBox pageWidthBox,
        NumberBox pageHeightBox,
        NumberBox dpiBox,
        NumberBox recentFilesBox,
        NumberBox autosaveBox,
        NumberBox backupCountBox,
        ComboBox unitSystemCombo,
        ToggleSwitch showRulersToggle,
        TextBox gridColorBox,
        ToggleSwitch showGridToggle,
        ToggleSwitch snapToGridToggle,
        NumberBox gridMinorSizeBox,
        NumberBox gridMajorSizeBox,
        TextBox workspaceColorBox,
        TextBox checkerLightBox,
        TextBox checkerDarkBox,
        NumberBox handleSizeBox,
        NumberBox zoomStepBox,
        NumberBox scrollSpeedBox,
        ComboBox exportFormatCombo,
        NumberBox exportDpiBox,
        NumberBox exportQualityBox,
        TextBox exportFolderBox,
        TextBox exportPatternBox,
        TextBox exportRarPathBox,
        NumberBox undoLimitBox,
        NumberBox thumbnailCacheBox,
        ToggleSwitch hwAccelToggle,
        ToggleSwitch bgRenderingToggle,
        NumberBox memoryLimitBox)
    {
        if (!SelectComboItemByTag(languageCombo, preferences.General.Language))
        {
            SelectComboItemByTag(languageCombo, UiLocalizationService.DefaultLanguage);
        }
        var effectiveLang = string.IsNullOrWhiteSpace(preferences.General.Language)
            ? UiLocalizationService.CurrentLanguage
            : preferences.General.Language;
        var effectiveSize = (preferences.General.IsPageSizeExplicitlySet &&
                             preferences.General.DefaultPageWidth > 0 &&
                             preferences.General.DefaultPageHeight > 0)
            ? (preferences.General.DefaultPageWidth, preferences.General.DefaultPageHeight)
            : GeneralPreferences.GetDefaultPageSizeForLanguage(effectiveLang);
        pageWidthBox.Value = effectiveSize.Item1;
        pageHeightBox.Value = effectiveSize.Item2;
        dpiBox.Value = preferences.General.DefaultDpi;
        recentFilesBox.Value = preferences.General.RecentFilesCount;
        autosaveBox.Value = preferences.General.AutosaveIntervalSeconds;
        backupCountBox.Value = preferences.General.BackupCount;

        unitSystemCombo.SelectedItem = preferences.Units.UnitSystem;
        showRulersToggle.IsOn = preferences.Units.ShowRulers;

        var unitSystem = preferences.Units.UnitSystem;
        gridMinorSizeBox.Minimum = 0.1;
        gridMajorSizeBox.Minimum = 0.1;
        gridMinorSizeBox.Header = LF("prefs.field.minor_with_units", GetUnitSuffix(unitSystem));
        gridMajorSizeBox.Header = LF("prefs.field.major_with_units", GetUnitSuffix(unitSystem));

        workspaceColorBox.Text = ToHex(preferences.Canvas.WorkspaceBackgroundColor);
        checkerLightBox.Text = ToHex(preferences.Canvas.CheckerboardLightColor);
        checkerDarkBox.Text = ToHex(preferences.Canvas.CheckerboardDarkColor);
        gridColorBox.Text = ToHex(preferences.Canvas.GridColor);
        showGridToggle.IsOn = preferences.Canvas.ShowGrid;
        snapToGridToggle.IsOn = preferences.Canvas.SnapToGrid;
        var dpi = Math.Clamp(preferences.General.DefaultDpi, 72, 2400);
        gridMinorSizeBox.Value = PixelsToPreferredUnits(preferences.Canvas.GridMinorSpacing, unitSystem, dpi);
        gridMajorSizeBox.Value = PixelsToPreferredUnits(preferences.Canvas.GridMajorSpacing, unitSystem, dpi);
        handleSizeBox.Value = preferences.Canvas.HandleSize;
        zoomStepBox.Value = preferences.Canvas.ZoomStepPercent;
        scrollSpeedBox.Value = preferences.Canvas.ScrollSpeed;

        exportFormatCombo.SelectedItem = preferences.ExportDefaults.Format;
        exportDpiBox.Value = preferences.ExportDefaults.Dpi;
        exportQualityBox.Value = preferences.ExportDefaults.Quality;
        exportFolderBox.Text = preferences.ExportDefaults.DefaultFolder;
        exportPatternBox.Text = preferences.ExportDefaults.FilenamePattern;
        exportRarPathBox.Text = preferences.ExportDefaults.RarExecutablePath;

        undoLimitBox.Value = preferences.Performance.UndoLimit;
        thumbnailCacheBox.Value = preferences.Performance.ThumbnailCacheMb;
        hwAccelToggle.IsOn = preferences.Performance.HardwareAcceleration;
        bgRenderingToggle.IsOn = preferences.Performance.BackgroundRendering;
        memoryLimitBox.Value = preferences.Performance.MemoryLimitMb;
    }

    private (bool Success, AppPreferences Preferences, string Error) BuildPreferencesFromControls(
        ComboBox languageCombo,
        NumberBox pageWidthBox,
        NumberBox pageHeightBox,
        NumberBox dpiBox,
        NumberBox recentFilesBox,
        NumberBox autosaveBox,
        NumberBox backupCountBox,
        ComboBox unitSystemCombo,
        ToggleSwitch showRulersToggle,
        TextBox gridColorBox,
        ToggleSwitch showGridToggle,
        ToggleSwitch snapToGridToggle,
        NumberBox gridMinorSizeBox,
        NumberBox gridMajorSizeBox,
        TextBox workspaceColorBox,
        TextBox checkerLightBox,
        TextBox checkerDarkBox,
        NumberBox handleSizeBox,
        NumberBox zoomStepBox,
        NumberBox scrollSpeedBox,
        ComboBox exportFormatCombo,
        NumberBox exportDpiBox,
        NumberBox exportQualityBox,
        TextBox exportFolderBox,
        TextBox exportPatternBox,
        TextBox exportRarPathBox,
        NumberBox undoLimitBox,
        NumberBox thumbnailCacheBox,
        ToggleSwitch hwAccelToggle,
        ToggleSwitch bgRenderingToggle,
        NumberBox memoryLimitBox)
    {
        var updated = _preferences.Clone();

        if (!TryParseHexColor(workspaceColorBox.Text, out var workspaceColor) ||
            !TryParseHexColor(checkerLightBox.Text, out var checkerLight) ||
            !TryParseHexColor(checkerDarkBox.Text, out var checkerDark) ||
            !TryParseHexColor(gridColorBox.Text, out var gridColor))
        {
            return (false, updated, L("prefs.error.colors_invalid"));
        }

        var selectedLanguage = (languageCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        updated.General.Language = UiLocalizationService.NormalizeLanguageTag(selectedLanguage ?? _preferences.General.Language);
        updated.General.IsLanguageExplicitlySet = true;
        updated.General.DefaultPageWidth = (float)pageWidthBox.Value;
        updated.General.DefaultPageHeight = (float)pageHeightBox.Value;
        updated.General.IsPageSizeExplicitlySet = pageWidthBox.Value > 0 && pageHeightBox.Value > 0;
        updated.General.DefaultDpi = (int)dpiBox.Value;
        updated.General.RecentFilesCount = (int)recentFilesBox.Value;
        updated.General.AutosaveIntervalSeconds = (int)autosaveBox.Value;
        updated.General.BackupCount = (int)backupCountBox.Value;

        updated.Units.UnitSystem = unitSystemCombo.SelectedItem is UnitSystemPreference unitSystem ? unitSystem : UnitSystemPreference.Pixels;
        updated.Units.ShowRulers = showRulersToggle.IsOn;

        updated.Canvas.WorkspaceBackgroundColor = workspaceColor;
        updated.Canvas.CheckerboardLightColor = checkerLight;
        updated.Canvas.CheckerboardDarkColor = checkerDark;
        updated.Canvas.GridColor = gridColor;
        updated.Canvas.ShowGrid = showGridToggle.IsOn;
        updated.Canvas.SnapToGrid = snapToGridToggle.IsOn;
        var workingDpi = Math.Clamp(updated.General.DefaultDpi, 72, 2400);
        updated.Canvas.GridMinorSpacing = PreferredUnitsToPixels((float)gridMinorSizeBox.Value, updated.Units.UnitSystem, workingDpi);
        updated.Canvas.GridMajorSpacing = PreferredUnitsToPixels((float)gridMajorSizeBox.Value, updated.Units.UnitSystem, workingDpi);
        updated.Canvas.HandleSize = (float)handleSizeBox.Value;
        updated.Canvas.ZoomStepPercent = (float)zoomStepBox.Value;
        updated.Canvas.ScrollSpeed = (float)scrollSpeedBox.Value;

        updated.ExportDefaults.Format = exportFormatCombo.SelectedItem?.ToString() ?? "png";
        updated.ExportDefaults.Dpi = (int)exportDpiBox.Value;
        updated.ExportDefaults.Quality = (int)exportQualityBox.Value;
        updated.ExportDefaults.DefaultFolder = exportFolderBox.Text.Trim();
        updated.ExportDefaults.FilenamePattern = exportPatternBox.Text.Trim();
        updated.ExportDefaults.RarExecutablePath = exportRarPathBox.Text.Trim();

        updated.Performance.UndoLimit = (int)undoLimitBox.Value;
        updated.Performance.ThumbnailCacheMb = (int)thumbnailCacheBox.Value;
        updated.Performance.HardwareAcceleration = hwAccelToggle.IsOn;
        updated.Performance.BackgroundRendering = bgRenderingToggle.IsOn;
        updated.Performance.MemoryLimitMb = (int)memoryLimitBox.Value;

        updated.Normalize();
        return (true, updated, string.Empty);
    }

    private void WirePreferenceDialogInteractions(
        AutoSuggestBox searchBox,
        ListBox categoryList,
        Dictionary<string, ListBoxItem> categoryItems,
        Dictionary<string, StackPanel> categoryPanels,
        Grid panelHost)
    {
        void EnsureSelection()
        {
            if (categoryList.SelectedItem is ListBoxItem selected && selected.Visibility == Visibility.Visible)
            {
                return;
            }

            categoryList.SelectedItem = categoryItems.Values.FirstOrDefault(item => item.Visibility == Visibility.Visible);
        }

        void ApplySearch()
        {
            var query = searchBox.Text?.Trim() ?? string.Empty;
            foreach (var item in categoryItems)
            {
                var visible = string.IsNullOrWhiteSpace(query) ||
                    item.Key.Contains(query, StringComparison.OrdinalIgnoreCase);
                item.Value.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }

            EnsureSelection();
        }

        void ShowSelected()
        {
            var selectedName = (categoryList.SelectedItem as ListBoxItem)?.Content?.ToString();
            var index = 0;
            foreach (var pair in categoryPanels)
            {
                if (panelHost.Children[index] is ScrollViewer scroll)
                {
                    scroll.Visibility = string.Equals(pair.Key, selectedName, StringComparison.OrdinalIgnoreCase)
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                index++;
            }
        }

        searchBox.TextChanged += (_, _) => ApplySearch();
        categoryList.SelectionChanged += (_, _) => ShowSelected();
        categoryList.SelectedItem = categoryItems.Values.FirstOrDefault();
        ApplySearch();
        ShowSelected();
    }

    private async Task ImportPreferencesIntoControlsAsync(
        TextBlock statusText,
        ComboBox languageCombo,
        NumberBox pageWidthBox,
        NumberBox pageHeightBox,
        NumberBox dpiBox,
        NumberBox recentFilesBox,
        NumberBox autosaveBox,
        NumberBox backupCountBox,
        ComboBox unitSystemCombo,
        ToggleSwitch showRulersToggle,
        TextBox gridColorBox,
        ToggleSwitch showGridToggle,
        ToggleSwitch snapToGridToggle,
        NumberBox gridMinorSizeBox,
        NumberBox gridMajorSizeBox,
        TextBox workspaceColorBox,
        TextBox checkerLightBox,
        TextBox checkerDarkBox,
        NumberBox handleSizeBox,
        NumberBox zoomStepBox,
        NumberBox scrollSpeedBox,
        ComboBox exportFormatCombo,
        NumberBox exportDpiBox,
        NumberBox exportQualityBox,
        TextBox exportFolderBox,
        TextBox exportPatternBox,
        TextBox exportRarPathBox,
        NumberBox undoLimitBox,
        NumberBox thumbnailCacheBox,
        ToggleSwitch hwAccelToggle,
        ToggleSwitch bgRenderingToggle,
        NumberBox memoryLimitBox)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        if (!PreferencesStorage.TryImport(file.Path, out var imported, out var error))
        {
            statusText.Text = LF("prefs.status.import_failed", error);
            statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            return;
        }

        ApplyPreferencesToControls(
            imported,
            languageCombo,
            pageWidthBox,
            pageHeightBox,
            dpiBox,
            recentFilesBox,
            autosaveBox,
            backupCountBox,
            unitSystemCombo,
            showRulersToggle,
            gridColorBox,
            showGridToggle,
            snapToGridToggle,
            gridMinorSizeBox,
            gridMajorSizeBox,
            workspaceColorBox,
            checkerLightBox,
            checkerDarkBox,
            handleSizeBox,
            zoomStepBox,
            scrollSpeedBox,
            exportFormatCombo,
            exportDpiBox,
            exportQualityBox,
            exportFolderBox,
            exportPatternBox,
            exportRarPathBox,
            undoLimitBox,
            thumbnailCacheBox,
            hwAccelToggle,
            bgRenderingToggle,
            memoryLimitBox);

        statusText.Text = LF("prefs.status.imported", file.Name);
        statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private Task ShowPreferencesDialogCoreAsync(
        AutoSuggestBox searchBox,
        ListBox categoryList,
        Grid panelHost,
        TextBlock statusText,
        Button importButton,
        Button exportButton,
        Button resetButton,
        Func<(bool Success, AppPreferences Preferences, string Error)> buildPreferences)
    {
        void Persist(AppPreferences preferences)
        {
            _preferences = preferences;
            SavePreferences();
            ApplyWindowPreferences();
            ApplyPreferencesToRenderer();
            ApplyPreferencesToCurrentDocumentDefaults();
            if (_autosaveTimer != null)
            {
                _autosaveTimer.Interval = TimeSpan.FromSeconds(GetAutosaveIntervalSeconds());
            }

            RefreshRecentDocumentsMenu();
            MainCanvas.Invalidate();
        }

        exportButton.Click += async (_, _) =>
        {
            var built = buildPreferences();
            if (!built.Success)
            {
                statusText.Text = built.Error;
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                return;
            }

            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            picker.SuggestedFileName = "letterist-preferences";
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            if (!PreferencesStorage.TryExport(built.Preferences, file.Path, out var error))
            {
                statusText.Text = LF("prefs.status.export_failed", error);
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                return;
            }

            statusText.Text = LF("prefs.status.exported", file.Name);
            statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        };

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        actions.Children.Add(importButton);
        actions.Children.Add(exportButton);
        actions.Children.Add(resetButton);

        var layout = new Grid
        {
            MinWidth = 940,
            MinHeight = 620
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        layout.Children.Add(searchBox);
        Grid.SetColumnSpan(searchBox, 2);
        layout.Children.Add(categoryList);
        Grid.SetRow(categoryList, 1);
        layout.Children.Add(panelHost);
        Grid.SetRow(panelHost, 1);
        Grid.SetColumn(panelHost, 1);

        var footer = new StackPanel { Spacing = 6 };
        footer.Children.Add(actions);
        footer.Children.Add(statusText);
        layout.Children.Add(footer);
        Grid.SetRow(footer, 2);
        Grid.SetColumnSpan(footer, 2);

        var okButton = new Button
        {
            Content = L("prefs.dialog.ok"),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            MinWidth = 80
        };
        var applyButton = new Button
        {
            Content = L("prefs.dialog.apply"),
            MinWidth = 80
        };
        var cancelButton = new Button
        {
            Content = L("prefs.dialog.cancel"),
            MinWidth = 80
        };

        var dialogButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        dialogButtons.Children.Add(okButton);
        dialogButtons.Children.Add(applyButton);
        dialogButtons.Children.Add(cancelButton);

        footer.Children.Add(dialogButtons);

        var contentRoot = new Grid
        {
            Padding = new Thickness(16),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            RequestedTheme = ElementTheme.Dark
        };
        contentRoot.Children.Add(layout);

        try { _preferencesWindow?.Close(); } catch { }

        var prefsWindow = new Window { Title = L("prefs.dialog.title") };
        prefsWindow.Content = contentRoot;

        var appWindow = prefsWindow.AppWindow;
        if (appWindow.Presenter is not OverlappedPresenter)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(true, true);
        }

        appWindow.Resize(new SizeInt32(1060, 820));
        TryApplyFontChooserTitleBarTheme(appWindow);
        CenterChildWindowOverMainWindow(appWindow);

        okButton.Click += (_, _) =>
        {
            var built = buildPreferences();
            if (!built.Success)
            {
                statusText.Text = built.Error;
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                return;
            }
            Persist(built.Preferences);
            try { prefsWindow.Close(); } catch { }
        };

        applyButton.Click += (_, _) =>
        {
            var built = buildPreferences();
            if (!built.Success)
            {
                statusText.Text = built.Error;
                statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed);
                return;
            }
            Persist(built.Preferences);
            statusText.Text = L("prefs.status.applied");
            statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        };

        cancelButton.Click += (_, _) =>
        {
            try { prefsWindow.Close(); } catch { }
        };

        prefsWindow.Closed += (_, _) =>
        {
            _preferencesWindow = null;
        };

        AttachEscapeToCloseWindow(prefsWindow, contentRoot);
        _preferencesWindow = prefsWindow;
        prefsWindow.Activate();
        return Task.CompletedTask;
    }

    private static string ToHex(Color color)
    {
        return color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    private static bool TryParseHexColor(string text, out Color color)
    {
        color = Color.Black;
        var raw = text?.Trim() ?? string.Empty;
        if (raw.StartsWith("#", StringComparison.Ordinal))
        {
            raw = raw[1..];
        }

        if (raw.Length == 6 &&
            byte.TryParse(raw[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r6) &&
            byte.TryParse(raw[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g6) &&
            byte.TryParse(raw[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b6))
        {
            color = new Color(r6, g6, b6, 255);
            return true;
        }

        if (raw.Length == 8 &&
            byte.TryParse(raw[0..2], System.Globalization.NumberStyles.HexNumber, null, out var r8) &&
            byte.TryParse(raw[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g8) &&
            byte.TryParse(raw[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b8) &&
            byte.TryParse(raw[6..8], System.Globalization.NumberStyles.HexNumber, null, out var a8))
        {
            color = new Color(r8, g8, b8, a8);
            return true;
        }

        return false;
    }

    private static float PreferredUnitsToPixels(float value, UnitSystemPreference unitSystem, float dpi)
    {
        var units = MathF.Max(0.1f, value);
        return units * GetPixelsPerPreferredUnit(unitSystem, dpi);
    }

    private static float PixelsToPreferredUnits(float pixels, UnitSystemPreference unitSystem, float dpi)
    {
        var scale = GetPixelsPerPreferredUnit(unitSystem, dpi);
        if (scale <= 0.0001f) return pixels;
        return pixels / scale;
    }

    private static float GetPixelsPerPreferredUnit(UnitSystemPreference unitSystem, float dpi)
    {
        return unitSystem switch
        {
            UnitSystemPreference.Inches => dpi,
            UnitSystemPreference.Centimeters => dpi / 2.54f,
            UnitSystemPreference.Millimeters => dpi / 25.4f,
            UnitSystemPreference.Points => dpi / 72f,
            UnitSystemPreference.Picas => dpi / 6f,
            _ => 1f
        };
    }

    private static string GetUnitSuffix(UnitSystemPreference unitSystem)
    {
        return unitSystem switch
        {
            UnitSystemPreference.Inches => "in",
            UnitSystemPreference.Centimeters => "cm",
            UnitSystemPreference.Millimeters => "mm",
            UnitSystemPreference.Points => "pt",
            UnitSystemPreference.Picas => "pc",
            _ => "px"
        };
    }

    private void LoadPreferences()
    {
        _preferences = PreferencesStorage.Load();
        _preferences.Normalize();
        UiLocalizationService.Initialize(_preferences.General.Language);
    }

    private void SavePreferences()
    {
        _preferences.Normalize();
        PreferencesStorage.Save(_preferences);
    }

    private void ApplyWindowPreferences()
    {
        _preferences.General.Language = UiLocalizationService.NormalizeLanguageTag(_preferences.General.Language);
        UiLocalizationService.SetLanguage(_preferences.General.Language);
        RefreshShortcutBindings();
        ApplyLocalizedUiText();

        if (RootGrid != null)
        {
            RootGrid.RequestedTheme = _preferences.General.Theme switch
            {
                ThemePreference.Light => ElementTheme.Light,
                ThemePreference.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }

        SyncGridMenuState();
        UpdateAutomationDefaultsFromPreferences();
        SyncPanelTemplateStorageFolderFromPreferences();
    }

    private void ApplyPreferencesToRenderer()
    {
        if (_renderer == null) return;

        _selectionHighlightColor = _preferences.General.AccentColor.WithAlpha(90);
        _renderer.TextSelectionHighlightColor = _selectionHighlightColor.ToWindowsColor();
        _renderer.CheckerboardLightColor = _preferences.Canvas.CheckerboardLightColor.ToWindowsColor();
        _renderer.CheckerboardDarkColor = _preferences.Canvas.CheckerboardDarkColor.ToWindowsColor();
        _renderer.ShowRulers = _preferences.Units.ShowRulers;
        _renderer.ShowGrid = _preferences.Canvas.ShowGrid;
        _renderer.GridMinorSpacing = GetGridMinorSpacingPixels();
        _renderer.GridMajorSpacing = GetGridMajorSpacingPixels();
        _renderer.GridBaseColor = _preferences.Canvas.GridColor.ToWindowsColor();
        _renderer.SelectionHandleSize = _preferences.Canvas.HandleSize;
        SyncGridMenuState();
    }

    private void ApplyPreferencesToCurrentDocumentDefaults()
    {
        var document = _editorState.Document;
        if (document == null) return;

        var effectiveSize = GetEffectivePageSize();
        document.SetDefaultPageSize(new Size2(
            Math.Clamp(effectiveSize.Width, 100f, 20000f),
            Math.Clamp(effectiveSize.Height, 100f, 20000f)));
        document.SetDefaultDpi(Math.Clamp(_preferences.General.DefaultDpi, 72, 2400));
        document.SetDefaultUnits(MapUnitSystemToDocumentUnits(_preferences.Units.UnitSystem));

        var page = document.ActivePage;
        if (page != null)
        {
            page.SetPanelGutterWidth(Math.Max(0f, _preferences.PanelDefaults.Gutter));
            page.SetReadingDirection(_preferences.PanelDefaults.ReadingDirection);
        }
    }

    private void CreateNewDocumentWithPreferences(string name)
    {
        var effectiveSize = GetEffectivePageSize();
        var size = new Size2(
            Math.Clamp(effectiveSize.Width, 100f, 20000f),
            Math.Clamp(effectiveSize.Height, 100f, 20000f));
        _editorState.NewDocument(name, size);
        ApplyPreferencesToCurrentDocumentDefaults();
        _lastUsedBalloonShape = GetDefaultBalloonShape();
        _lastUsedBalloonStyle = GetDefaultBalloonStyle();
        _lastUsedTextStyle = GetDefaultTextStyle();
    }

    private (float Width, float Height) GetEffectivePageSize()
    {
        if (_preferences.General.IsPageSizeExplicitlySet &&
            _preferences.General.DefaultPageWidth > 0 &&
            _preferences.General.DefaultPageHeight > 0)
        {
            return (_preferences.General.DefaultPageWidth, _preferences.General.DefaultPageHeight);
        }

        return GeneralPreferences.GetDefaultPageSizeForLanguage(UiLocalizationService.CurrentLanguage);
    }

    private void UpdateAutomationDefaultsFromPreferences()
    {
        App.UpdateAutomationDefaults(_preferences.Automation.EnabledByDefault, _preferences.Automation.Port);
        StartupLogger.Configure(
            _preferences.Automation.EnableLogging,
            string.IsNullOrWhiteSpace(_preferences.Automation.LogFilePath) ? null : _preferences.Automation.LogFilePath);
    }

    private static string MapUnitSystemToDocumentUnits(UnitSystemPreference unitSystem)
    {
        return unitSystem switch
        {
            UnitSystemPreference.Inches => "in",
            UnitSystemPreference.Centimeters => "cm",
            UnitSystemPreference.Millimeters => "mm",
            UnitSystemPreference.Points => "pt",
            UnitSystemPreference.Picas => "pc",
            _ => "px"
        };
    }

    private int GetAutosaveIntervalSeconds() => Math.Clamp(_preferences.General.AutosaveIntervalSeconds, 5, 3600);
    private int GetRecentFilesLimit() => Math.Clamp(_preferences.General.RecentFilesCount, 1, 50);
    private float GetGridMinorSpacingPixels() => Math.Clamp(_preferences.Canvas.GridMinorSpacing, 1f, 2000f);
    private float GetGridMajorSpacingPixels() => Math.Clamp(_preferences.Canvas.GridMajorSpacing, GetGridMinorSpacingPixels(), 20000f);
    private Color GetWorkspaceBackgroundColor() => _preferences.Canvas.WorkspaceBackgroundColor;
    private BalloonShape GetDefaultBalloonShape() => _preferences.BalloonDefaults.Shape;
    private BalloonStyle GetDefaultBalloonStyle() => _preferences.BalloonDefaults.ToBalloonStyle();
    private TextStyle GetDefaultTextStyle() => _preferences.TextDefaults.ToTextStyle();
    private TailStyle GetDefaultTailStyle() => _preferences.TailDefaults.Style;
    private float GetDefaultTailWidth() => Math.Clamp(_preferences.TailDefaults.Width, 1f, 200f);
    private float GetDefaultTailCurvature() => Math.Clamp(_preferences.TailDefaults.Curve, -1f, 1f);
    private float GetPanelDefaultSafeMargin() => Math.Max(0f, _preferences.PanelDefaults.Margin);
    private Color GetPanelDefaultBorderColor() => _preferences.PanelDefaults.BorderColor;
    private float GetPanelDefaultBorderWidth() => Math.Max(0f, _preferences.PanelDefaults.BorderWidth);
    private PanelBorderStyle GetPanelDefaultBorderStyle() => _preferences.PanelDefaults.BorderStyle;

    private void SyncGridMenuState()
    {
        if (ShowGridMenuItem != null)
        {
            ShowGridMenuItem.IsChecked = _preferences.Canvas.ShowGrid;
        }

        if (SnapToGridMenuItem != null)
        {
            SnapToGridMenuItem.IsChecked = _preferences.Canvas.SnapToGrid;
        }

        if (SnapToGuidesMenuItem != null && _editorState != null)
        {
            SnapToGuidesMenuItem.IsChecked = _editorState.SnapToGuides;
        }
    }
}
