using Letterist.Commands;
using Letterist.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text;
using Windows.System;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Letterist;

public sealed partial class MainWindow
{
    private readonly List<TargetLanguageViewModel> _targetLanguages = new();
    private readonly List<TranslationBalloonListItemViewModel> _translationRows = new();
    private Guid? _selectedTranslationBalloonId;
    private bool _isUpdatingTranslationUi;
    private List<string> _translationResultsErrors = new();

    private void RefreshTranslationPanel()
    {
        if (_isUpdatingTranslationUi) return;
        if (TranslationTabContent == null) return;

        var doc = _editorState.Document;
        _isUpdatingTranslationUi = true;
        try
        {
            if (doc == null)
            {
                TranslationActiveLanguageComboBox.ItemsSource = null;
                TranslationLanguageListView.ItemsSource = null;
                TranslationBalloonListView.ItemsSource = null;
                TranslationSummaryText.Text = L("translation.summary.no_document");
                TranslationSourceTextBlock.Text = L("translation.source.none");
                TranslationTargetTextBox.Text = string.Empty;
                TranslationTargetTextBox.IsEnabled = false;
                TranslationNewLanguageBox.Text = string.Empty;
                TranslationAddLanguageButton.IsEnabled = false;
                TranslationNoLanguagesText.Visibility = Visibility.Visible;
                return;
            }

            var languages = doc.GetKnownLanguages()
                .Where(lang => !string.Equals(lang, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _targetLanguages.Clear();
            foreach (var lang in languages)
            {
                var (translated, total) = CountTranslations(doc, lang);
                _targetLanguages.Add(new TargetLanguageViewModel
                {
                    Name = lang,
                    TranslatedCount = translated,
                    TotalCount = total
                });
            }

            TranslationLanguageListView.ItemsSource = _targetLanguages.ToList();
            TranslationNoLanguagesText.Visibility = _targetLanguages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            var activeLanguages = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = doc.BaseLanguage + " (Source)", Tag = doc.BaseLanguage }
            };
            foreach (var lang in languages)
            {
                activeLanguages.Add(new ComboBoxItem { Content = lang, Tag = lang });
            }
            TranslationActiveLanguageComboBox.ItemsSource = activeLanguages;
            SelectComboItemByTag(TranslationActiveLanguageComboBox, doc.ActiveLanguage);

            _translationRows.Clear();
            var filter = (TranslationSearchBox.Text ?? string.Empty).Trim();
            var entries = BuildTranslationEntries(doc, doc.ActiveLanguage);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(filter) &&
                    entry.SourceText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.TargetText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    entry.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _translationRows.Add(entry);
            }

            TranslationBalloonListView.ItemsSource = _translationRows.ToList();
            var translatedCount = _translationRows.Count(row => row.IsTranslated);
            TranslationSummaryText.Text = LF("translation.summary.stats", _translationRows.Count, translatedCount);

            var preferredSelection = _selectedTranslationBalloonId ?? doc.SelectedBalloonId;
            var selectedRow = preferredSelection.HasValue
                ? _translationRows.FirstOrDefault(row => row.BalloonId == preferredSelection.Value)
                : null;
            TranslationBalloonListView.SelectedItem = selectedRow;
            UpdateTranslationDetails();
        }
        finally
        {
            _isUpdatingTranslationUi = false;
        }
    }

    private (int translated, int total) CountTranslations(Document doc, string language)
    {
        int translated = 0;
        int total = 0;
        var normalizedLanguage = Document.NormalizeLanguageTag(language, doc.BaseLanguage);

        foreach (var page in doc.Pages)
        {
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    total++;
                    var targetText = doc.GetBalloonTranslationText(balloon, language) ?? string.Empty;
                    if (!string.Equals(targetText, balloon.Text, StringComparison.Ordinal) && !string.IsNullOrEmpty(targetText))
                    {
                        translated++;
                    }
                }
            }
        }

        return (translated, total);
    }

    private List<TranslationBalloonListItemViewModel> BuildTranslationEntries(Document doc, string language)
    {
        var entries = new List<TranslationBalloonListItemViewModel>();
        var normalizedLanguage = Document.NormalizeLanguageTag(language, doc.BaseLanguage);
        int pageNumber = 0;

        foreach (var page in doc.Pages)
        {
            pageNumber++;
            int balloonNumber = 0;

            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    balloonNumber++;
                    var targetText = doc.GetBalloonTranslationText(balloon, language) ?? string.Empty;
                    var isTranslated = !string.Equals(targetText, balloon.Text, StringComparison.Ordinal) && !string.IsNullOrEmpty(targetText);

                    entries.Add(new TranslationBalloonListItemViewModel
                    {
                        BalloonId = balloon.Id,
                        PageId = page.Id,
                        PageNumber = pageNumber,
                        BalloonNumber = balloonNumber,
                        SourceText = balloon.Text,
                        TargetText = targetText,
                        IsTranslated = isTranslated,
                        Title = $"{page.Name} / #{balloonNumber}",
                        Preview = TruncateSingleLine(balloon.Text, 80)
                    });
                }
            }
        }

        return entries;
    }

    private void RefreshTranslationSelectionFromEditor()
    {
        if (_isUpdatingTranslationUi) return;
        var doc = _editorState.Document;
        if (doc?.SelectedBalloonId == null) return;

        var row = _translationRows.FirstOrDefault(item => item.BalloonId == doc.SelectedBalloonId.Value);
        if (row == null) return;

        _isUpdatingTranslationUi = true;
        try
        {
            _selectedTranslationBalloonId = row.BalloonId;
            TranslationBalloonListView.SelectedItem = row;
            TranslationBalloonListView.ScrollIntoView(row);
            UpdateTranslationDetails();
        }
        finally
        {
            _isUpdatingTranslationUi = false;
        }
    }

    private void UpdateTranslationDetails()
    {
        var doc = _editorState.Document;
        var selected = TranslationBalloonListView.SelectedItem as TranslationBalloonListItemViewModel;

        if (doc == null || selected == null)
        {
            TranslationSourceTextBlock.Text = L("translation.source.none");
            TranslationTargetTextBox.Text = string.Empty;
            TranslationTargetTextBox.IsEnabled = false;
            TranslationDetailStatusText.Text = L("translation.detail.select_balloon");
            return;
        }

        _selectedTranslationBalloonId = selected.BalloonId;
        TranslationSourceTextBlock.Text = LF("translation.source.with_text", selected.SourceText);
        TranslationTargetTextBox.Text = selected.TargetText;
        var editable = !string.Equals(doc.ActiveLanguage, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase);
        TranslationTargetTextBox.IsEnabled = editable;
        TranslationDetailStatusText.Text = editable
            ? LF("translation.detail.editing", doc.ActiveLanguage)
            : L("translation.detail.base_equals_active");
    }

    private static string TruncateSingleLine(string text, int maxChars)
    {
        var value = (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (value.Length <= maxChars) return value;
        return value.Substring(0, Math.Max(0, maxChars - 1)) + "\u2026";
    }

    private static bool SelectComboItemByTag(ComboBox combo, string? tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem comboItem && comboItem.Tag is string comboTag &&
                string.Equals(comboTag, tag ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = comboItem;
                return true;
            }
        }

        return false;
    }

    private void TranslationNewLanguageBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTranslationUi) return;
        var value = TranslationNewLanguageBox.Text?.Trim() ?? string.Empty;
        TranslationAddLanguageButton.IsEnabled = !string.IsNullOrWhiteSpace(value);
    }

    private void TranslationNewLanguageBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = TryAddTranslationLanguage();
    }

    private void TranslationAddLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _ = TryAddTranslationLanguage();
    }

    private bool TryAddTranslationLanguage()
    {
        var doc = _editorState.Document;
        if (doc == null) return false;

        var languageName = TranslationNewLanguageBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(languageName)) return false;
        var normalizedLanguage = Document.NormalizeLanguageTag(languageName, doc.BaseLanguage);

        var existingLanguages = doc.GetKnownLanguages();
        if (existingLanguages.Any(lang => string.Equals(lang, normalizedLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatusMessage(LF("translation.status.language_exists", normalizedLanguage));
            return true;
        }

        _editorState.ExecuteTransactionSafe(
            $"Add translation language: {normalizedLanguage}",
            new List<ICommand>
            {
                new SetTranslationLanguageExportVisibilityCommand(normalizedLanguage, visible: true),
                new SetDocumentActiveLanguageCommand(normalizedLanguage)
            });

        TranslationNewLanguageBox.Text = string.Empty;
        SetStatusMessage(LF("translation.status.language_added", normalizedLanguage));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
        return true;
    }

    private void TranslationDeleteLanguageButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (sender is not Button button || button.Tag is not string languageNameRaw) return;
        var languageName = Document.NormalizeLanguageTag(languageNameRaw, doc.BaseLanguage);

        var commands = new List<ICommand>();
        foreach (var page in doc.Pages)
        {
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    if (balloon.Translations.ContainsKey(languageName))
                    {
                        commands.Add(new DeleteBalloonTranslationCommand(balloon.Id, languageName));
                    }
                }
            }
        }

        commands.Add(new SetTranslationLanguageLayoutCommand(
            languageName,
            TranslationTextDirection.Auto,
            TranslationTextOrientation.Auto,
            mirrorTailsForRtl: false));
        commands.Add(new RemoveTranslationLanguageExportVisibilityCommand(languageName));

        if (string.Equals(doc.ActiveLanguage, languageName, StringComparison.OrdinalIgnoreCase))
        {
            commands.Add(new SetDocumentActiveLanguageCommand(doc.BaseLanguage));
        }

        if (string.Equals(doc.CompareLanguage, languageName, StringComparison.OrdinalIgnoreCase))
        {
            commands.Add(new SetDocumentTranslationCompareCommand(doc.TranslationCompareMode, compareLanguage: null));
        }

        if (commands.Count > 0)
        {
            _editorState.ExecuteTransactionSafe($"Remove language: {languageName}", commands);
        }

        SetStatusMessage(LF("translation.status.language_removed", languageName));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private void TranslationLanguageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTranslationUi) return;
        var doc = _editorState.Document;
        if (doc == null) return;

        var selected = TranslationLanguageListView.SelectedItem as TargetLanguageViewModel;
        if (selected == null) return;

        _editorState.Execute(new SetDocumentActiveLanguageCommand(selected.Name));
        SetStatusMessage(LF("translation.status.active_language_set", selected.Name));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private void TranslationActiveLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTranslationUi) return;
        var doc = _editorState.Document;
        if (doc == null) return;
        if (TranslationActiveLanguageComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string language) return;
        if (string.IsNullOrWhiteSpace(language)) return;
        if (string.Equals(language, doc.ActiveLanguage, StringComparison.OrdinalIgnoreCase)) return;

        _editorState.Execute(new SetDocumentActiveLanguageCommand(language));
        SetStatusMessage(LF("translation.status.active_language_set", language));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private void TranslationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingTranslationUi) return;
        RefreshTranslationPanel();
    }

    private void TranslationBalloonListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTranslationUi) return;
        var doc = _editorState.Document;
        var selected = TranslationBalloonListView.SelectedItem as TranslationBalloonListItemViewModel;
        if (doc == null || selected == null)
        {
            UpdateTranslationDetails();
            return;
        }

        _selectedTranslationBalloonId = selected.BalloonId;
        if (doc.ActivePageId != selected.PageId)
        {
            _editorState.Execute(new SetActivePageCommand(selected.PageId));
            doc = _editorState.Document;
            if (doc == null) return;
        }

        _editorState.SelectBalloon(selected.BalloonId);
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
        UpdateTranslationDetails();
    }

    private void TranslationSaveButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (!_selectedTranslationBalloonId.HasValue) return;
        if (string.Equals(doc.ActiveLanguage, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            SetStatusMessage(L("translation.status.switch_target_before_save"));
            return;
        }

        var value = TranslationTargetTextBox.Text ?? string.Empty;
        _editorState.Execute(new SetBalloonTranslationCommand(_selectedTranslationBalloonId.Value, doc.ActiveLanguage, value));
        SetStatusMessage(L("translation.status.saved"));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private void TranslationClearButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (!_selectedTranslationBalloonId.HasValue) return;
        if (string.Equals(doc.ActiveLanguage, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            SetStatusMessage(L("translation.status.base_text_cannot_clear"));
            return;
        }

        _editorState.Execute(new DeleteBalloonTranslationCommand(_selectedTranslationBalloonId.Value, doc.ActiveLanguage));
        SetStatusMessage(L("translation.status.cleared"));
        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private async void ExportTranslationsButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var targetLanguages = doc.GetKnownLanguages()
            .Where(lang => !string.Equals(lang, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetLanguages.Count == 0)
        {
            SetStatusMessage(L("translation.export_strings.no_languages"));
            return;
        }

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        var baseFileName = SanitizeFileName(doc.Name);

        try
        {
            foreach (var language in targetLanguages)
            {
                var csvContent = BuildTranslationCsv(doc, language);
                var fileName = $"{baseFileName}.translation.{language}.csv";
                var filePath = Path.Combine(folder.Path, fileName);
                await File.WriteAllTextAsync(filePath, csvContent, Encoding.UTF8);
            }

            SetStatusMessage(LF("translation.export_strings.success", targetLanguages.Count, folder.Path));
        }
        catch (Exception ex)
        {
            SetStatusMessage($"Export failed: {ex.Message}");
        }
    }

    private string BuildTranslationCsv(Document doc, string language)
    {
        var lines = new List<string>
        {
            "id,page,balloon,source,target"
        };

        int pageNumber = 0;
        foreach (var page in doc.Pages)
        {
            pageNumber++;
            int balloonNumber = 0;

            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    balloonNumber++;
                    var targetText = doc.GetBalloonTranslationText(balloon, language) ?? balloon.Text;
                    lines.Add(string.Join(",",
                        EscapeCsv(balloon.Id.ToString()),
                        pageNumber.ToString(),
                        balloonNumber.ToString(),
                        EscapeCsv(balloon.Text),
                        EscapeCsv(targetText)));
                }
            }
        }

        return string.Join("\n", lines);
    }

    private async void ImportTranslationsButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;

        var baseFileName = SanitizeFileName(doc.Name);
        var targetLanguages = doc.GetKnownLanguages()
            .Where(lang => !string.Equals(lang, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var files = Directory.GetFiles(folder.Path, $"{baseFileName}.translation.*.csv");
        if (files.Length == 0)
        {
            files = Directory.GetFiles(folder.Path, "*.translation.*.csv");
        }

        if (files.Length == 0)
        {
            SetStatusMessage(L("translation.import_strings.no_files"));
            return;
        }

        var commands = new List<ICommand>();
        var errors = new List<string>();
        var importedCount = 0;
        var languagesImported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split('.');
            if (parts.Length < 3 || !string.Equals(parts[^2], "translation", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var language = parts[^1];

            if (!targetLanguages.Any(lang => string.Equals(lang, language, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
                var (imported, fileErrors) = ParseAndApplyTranslationCsv(doc, language, content, commands);
                importedCount += imported;
                errors.AddRange(fileErrors);
                if (imported > 0)
                {
                    languagesImported.Add(language);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        if (commands.Count > 0)
        {
            _editorState.ExecuteTransactionSafe("Import translations", commands);
        }

        if (errors.Count > 0)
        {
            _translationResultsErrors = errors;
            ShowTranslationResults(
                LF("translation.import_strings.partial", importedCount, errors.Count),
                string.Join("\n", errors.Take(50)));
        }
        else if (importedCount > 0)
        {
            SetStatusMessage(LF("translation.import_strings.success", importedCount, languagesImported.Count));
        }
        else
        {
            SetStatusMessage(L("translation.status.no_changes_to_import"));
        }

        RefreshTranslationPanel();
        MainCanvas.Invalidate();
    }

    private (int imported, List<string> errors) ParseAndApplyTranslationCsv(Document doc, string language, string content, List<ICommand> commands)
    {
        var errors = new List<string>();
        var imported = 0;

        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (lines.Count <= 1) return (0, errors);

        var headers = ParseCsvLine(lines[0])
            .Select((name, index) => new { name = name.Trim(), index })
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);

        if (!headers.TryGetValue("id", out var idIndex)) return (0, errors);
        if (!headers.TryGetValue("page", out var pageIndex)) return (0, errors);
        if (!headers.TryGetValue("balloon", out var balloonIndex)) return (0, errors);
        if (!headers.TryGetValue("target", out var targetIndex)) return (0, errors);

        for (int i = 1; i < lines.Count; i++)
        {
            var columns = ParseCsvLine(lines[i]);
            if (idIndex >= columns.Count || targetIndex >= columns.Count) continue;

            var targetText = columns[targetIndex];
            Balloon? balloon = null;

            if (Guid.TryParse(columns[idIndex], out var balloonId))
            {
                balloon = doc.FindBalloonAnywhere(balloonId);
            }

            if (balloon == null && pageIndex < columns.Count && balloonIndex < columns.Count)
            {
                if (int.TryParse(columns[pageIndex], out var pageNum) && int.TryParse(columns[balloonIndex], out var balloonNum))
                {
                    balloon = FindBalloonByNumber(doc, pageNum, balloonNum);
                    if (balloon == null)
                    {
                        errors.Add(LF("translation.results.not_found", pageNum, balloonNum));
                        continue;
                    }
                }
            }

            if (balloon == null) continue;

            var currentText = doc.GetBalloonTranslationText(balloon, language) ?? string.Empty;
            if (string.Equals(currentText, targetText, StringComparison.Ordinal)) continue;

            commands.Add(new SetBalloonTranslationCommand(balloon.Id, language, targetText));
            imported++;
        }

        return (imported, errors);
    }

    private Balloon? FindBalloonByNumber(Document doc, int pageNumber, int balloonNumber)
    {
        int currentPage = 0;
        foreach (var page in doc.Pages)
        {
            currentPage++;
            if (currentPage != pageNumber) continue;

            int currentBalloon = 0;
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    currentBalloon++;
                    if (currentBalloon == balloonNumber)
                    {
                        return balloon;
                    }
                }
            }
        }

        return null;
    }

    private void ShowTranslationResults(string summary, string details)
    {
        TranslationResultsPanel.Visibility = Visibility.Visible;
        TranslationResultsTitleText.Text = L("translation.results.title");
        TranslationResultsSummaryText.Text = summary;
        TranslationResultsDetailsText.Text = details;
    }

    private void TranslationResultsDismissButton_Click(object sender, RoutedEventArgs e)
    {
        TranslationResultsPanel.Visibility = Visibility.Collapsed;
        _translationResultsErrors.Clear();
    }

}
