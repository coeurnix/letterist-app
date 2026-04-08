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

    private Task ShowFindReplaceDialogAsync(bool replaceMode)
    {
        if (_editorState.Document == null)
        {
            return Task.CompletedTask;
        }

        ShowFindReplaceWindow(replaceMode);
        return Task.CompletedTask;
    }

    private void ShowFindReplaceWindow(bool replaceMode)
    {
        var window = replaceMode ? _replaceWindow : _findWindow;
        if (window == null)
        {
            window = new FindReplaceWindow(this, replaceMode);
            window.Closed += (_, __) =>
            {
                if (replaceMode)
                {
                    _replaceWindow = null;
                }
                else
                {
                    _findWindow = null;
                }
            };

            if (replaceMode)
            {
                _replaceWindow = window;
            }
            else
            {
                _findWindow = window;
            }
        }

        window.SyncFromSession();
        window.Activate();
    }

    private async Task<bool> ExecuteFindNextAsync(TextSearchOptions options, SearchScope scope, TextBlock statusText)
    {
        RecordSearchQuery(options.Query);
        if (!TryFindNextMatch(options, scope, out var result, out var error))
        {
            statusText.Text = error ?? L("find.status.no_matches");
            return false;
        }

        await RevealSearchResultAsync(result);
        _findReplaceSession.UpdateMatch(result.Target.Page.Id, result.Target.Balloon.Id, result.Match.Start, result.Match.Length);
        statusText.Text = LF("find.status.found_match", result.Target.Page.Name);
        return true;
    }

    private async Task ExecuteReplaceAsync(TextSearchOptions options, string replacement, SearchScope scope, TextBlock statusText)
    {
        if (string.IsNullOrWhiteSpace(options.Query))
        {
            statusText.Text = L("find.status.enter_text");
            return;
        }

        RecordSearchQuery(options.Query);

        if (!TryGetSessionResult(scope, out var current))
        {
            var found = await ExecuteFindNextAsync(options, scope, statusText);
            if (!found) return;

            if (!TryGetSessionResult(scope, out current))
            {
                statusText.Text = L("find.status.no_matches");
                return;
            }
        }

        await RevealSearchResultAsync(current);

        var text = GetBalloonTextForSearch(current.Target.Balloon);
        string newText;
        int replacementLength;
        try
        {
            newText = TextSearch.ReplaceMatch(text, options, current.Match, replacement, out replacementLength);
        }
        catch (ArgumentException)
        {
            statusText.Text = L("find.status.invalid_regex");
            return;
        }

        if (string.Equals(text, newText, StringComparison.Ordinal))
        {
            _findReplaceSession.ResetMatch();
            statusText.Text = L("find.status.no_match_replace");
            return;
        }

        if (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId == current.Target.Balloon.Id)
        {
            _editorState.ReplaceEditingText(newText, current.Match.Start, replacementLength);
        }
        else
        {
            _editorState.Execute(new SetBalloonTextCommand(current.Target.Balloon.Id, newText));
        }

        _findReplaceSession.UpdateMatch(current.Target.Page.Id, current.Target.Balloon.Id, current.Match.Start, replacementLength);

        if (TryFindNextMatch(options, scope, out var nextMatch, out _))
        {
            await RevealSearchResultAsync(nextMatch);
            _findReplaceSession.UpdateMatch(nextMatch.Target.Page.Id, nextMatch.Target.Balloon.Id, nextMatch.Match.Start, nextMatch.Match.Length);
            statusText.Text = LF("find.status.replaced_next", nextMatch.Target.Page.Name);
        }
        else
        {
            statusText.Text = L("find.status.replaced_no_more");
            _findReplaceSession.ResetMatch();
        }
    }

    private async Task ExecuteReplaceAllAsync(TextSearchOptions options, string replacement, SearchScope scope, TextBlock statusText)
    {
        if (string.IsNullOrWhiteSpace(options.Query))
        {
            statusText.Text = L("find.status.enter_text");
            return;
        }

        RecordSearchQuery(options.Query);

        if (_editorState.Mode == EditorMode.EditText)
        {
            _editorState.ExitTextEditMode(saveChanges: true);
        }

        var targets = CollectSearchTargets(scope);
        if (targets.Count == 0)
        {
            statusText.Text = L("find.status.no_balloons");
            return;
        }

        var commands = new List<ICommand>();
        int matchCount = 0;
        int balloonCount = 0;

        foreach (var target in targets)
        {
            int count;
            string newText;
            try
            {
                newText = TextSearch.ReplaceAll(target.Balloon.Text, options, replacement, out count);
            }
            catch (ArgumentException)
            {
                statusText.Text = L("find.status.invalid_regex");
                return;
            }

            if (count > 0)
            {
                matchCount += count;
                balloonCount++;
                commands.Add(new SetBalloonTextCommand(target.Balloon.Id, newText));
            }
        }

        if (matchCount == 0)
        {
            statusText.Text = L("find.status.no_matches");
            _findReplaceSession.ResetMatch();
            return;
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Replace all text", commands);
        }

        statusText.Text = LF("find.status.replaced_all", matchCount, balloonCount);
        _findReplaceSession.ResetMatch();
    }

    private bool TryFindNextMatch(TextSearchOptions options, SearchScope scope, out SearchResult result, out string? error)
    {
        result = default;
        error = null;

        if (string.IsNullOrWhiteSpace(options.Query))
        {
            error = L("find.status.enter_text");
            return false;
        }

        var targets = CollectSearchTargets(scope);
        if (targets.Count == 0)
        {
            error = L("find.status.no_balloons");
            return false;
        }

        var (startIndex, startOffset) = GetSearchStart(targets);
        if (startIndex < 0)
        {
            error = L("find.status.no_balloons");
            return false;
        }

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var index = (startIndex + i) % targets.Count;
                var target = targets[index];
                var text = GetBalloonTextForSearch(target.Balloon);
                var offset = i == 0 ? startOffset : 0;
                offset = Math.Clamp(offset, 0, text.Length);

                if (TextSearch.TryFindNext(text, options, offset, out var match))
                {
                    result = new SearchResult(target, match);
                    return true;
                }
            }
        }
        catch (ArgumentException)
        {
            error = L("find.status.invalid_regex");
            return false;
        }

        error = L("find.status.no_matches");
        return false;
    }

    private bool TryGetSessionResult(SearchScope scope, out SearchResult result)
    {
        result = default;
        if (!_findReplaceSession.HasMatch) return false;

        var targets = CollectSearchTargets(scope);
        var index = FindTargetIndex(targets, _findReplaceSession.LastBalloonId!.Value, _findReplaceSession.LastPageId);
        if (index < 0) return false;

        result = new SearchResult(
            targets[index],
            new TextMatch(_findReplaceSession.LastMatchStart, _findReplaceSession.LastMatchLength));
        return true;
    }

    private (int startIndex, int startOffset) GetSearchStart(IReadOnlyList<SearchTarget> targets)
    {
        if (targets.Count == 0) return (-1, 0);

        if (_findReplaceSession.HasMatch)
        {
            var index = FindTargetIndex(targets, _findReplaceSession.LastBalloonId!.Value, _findReplaceSession.LastPageId);
            if (index >= 0)
            {
                var offset = _findReplaceSession.LastMatchStart + _findReplaceSession.LastMatchLength;
                return (index, offset);
            }
        }

        if (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId.HasValue)
        {
            var index = FindTargetIndex(targets, _editorState.EditingBalloonId.Value);
            if (index >= 0)
            {
                var offset = _editorState.EditingSelectionLength > 0
                    ? _editorState.EditingSelectionStart + _editorState.EditingSelectionLength
                    : _editorState.EditingCursorPosition;
                return (index, offset);
            }
        }

        var selectedId = _editorState.Document?.SelectedBalloonId;
        if (selectedId.HasValue)
        {
            var index = FindTargetIndex(targets, selectedId.Value);
            if (index >= 0) return (index, 0);
        }

        return (0, 0);
    }

    private static int FindTargetIndex(IReadOnlyList<SearchTarget> targets, Guid balloonId, Guid? pageId = null)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target.Balloon.Id != balloonId) continue;
            if (pageId.HasValue && target.Page.Id != pageId.Value) continue;
            return i;
        }

        return -1;
    }

    private IReadOnlyList<SearchTarget> CollectSearchTargets(SearchScope scope)
    {
        var doc = _editorState.Document;
        if (doc == null) return Array.Empty<SearchTarget>();

        IEnumerable<DocumentPage> pages = scope == SearchScope.AllPages
            ? doc.Pages
            : doc.ActivePage != null
                ? new[] { doc.ActivePage }
                : Array.Empty<DocumentPage>();

        var targets = new List<SearchTarget>();
        foreach (var page in pages)
        {
            foreach (var layer in page.BalloonLayers)
            {
                foreach (var balloon in layer.Balloons)
                {
                    targets.Add(new SearchTarget(page, balloon));
                }
            }
        }

        return targets;
    }

    private bool TryBuildSearchResultSummaries(
        TextSearchOptions options,
        SearchScope scope,
        out IReadOnlyList<SearchResultSummary> results,
        out int totalMatches,
        out string? error)
    {
        results = Array.Empty<SearchResultSummary>();
        totalMatches = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(options.Query))
        {
            error = L("find.status.enter_text");
            return false;
        }

        var targets = CollectSearchTargets(scope);
        if (targets.Count == 0)
        {
            error = L("find.status.no_balloons");
            return false;
        }

        var summaries = new List<SearchResultSummary>();
        try
        {
            foreach (var target in targets)
            {
                var text = GetBalloonTextForSearch(target.Balloon);
                var matches = TextSearch.FindAll(text, options);
                if (matches.Count == 0) continue;

                totalMatches += matches.Count;
                var preview = BuildSearchPreview(text, matches[0]);
                summaries.Add(new SearchResultSummary(
                    target.Page.Id,
                    target.Balloon.Id,
                    target.Page.Name,
                    preview,
                    matches.Count,
                    matches[0].Start,
                    matches[0].Length));
            }
        }
        catch (ArgumentException)
        {
            error = L("find.status.invalid_regex");
            return false;
        }

        results = summaries;
        if (totalMatches == 0)
        {
            error = L("find.status.no_matches");
            return false;
        }

        return true;
    }

    private string GetBalloonTextForSearch(Balloon balloon)
    {
        if (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId == balloon.Id)
        {
            return _editorState.EditingText;
        }

        return balloon.Text;
    }

    private string BuildSearchPreview(string text, TextMatch match)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return L("find.status.empty");
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ");
        if (normalized.Length == 0)
        {
            return L("find.status.empty");
        }

        const int maxLength = 64;
        if (normalized.Length <= maxLength)
        {
            return normalized.Trim();
        }

        var start = Math.Max(0, match.Start - 20);
        if (start + maxLength > normalized.Length)
        {
            start = Math.Max(0, normalized.Length - maxLength);
        }

        var length = Math.Min(maxLength, normalized.Length - start);
        var snippet = normalized.Substring(start, length).Trim();
        if (start > 0)
        {
            snippet = "..." + snippet;
        }

        if (start + length < normalized.Length)
        {
            snippet += "...";
        }

        return snippet;
    }

    private async Task RevealSearchResultAsync(SearchResult result)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        if (doc.ActivePageId != result.Target.Page.Id)
        {
            _editorState.Execute(new SetActivePageCommand(result.Target.Page.Id));
            await EnsureBackgroundLoadedAsync(result.Target.Page);
            await EnsureFloatingImagesLoadedAsync(result.Target.Page);
        }

        if (_editorState.Mode == EditorMode.EditText &&
            _editorState.EditingBalloonId.HasValue &&
            _editorState.EditingBalloonId.Value != result.Target.Balloon.Id)
        {
            _editorState.ExitTextEditMode(saveChanges: true);
        }

        _editorState.SelectBalloon(result.Target.Balloon.Id);

        if (_editorState.Mode != EditorMode.EditText || _editorState.EditingBalloonId != result.Target.Balloon.Id)
        {
            _editorState.EnterTextEditMode(result.Target.Balloon.Id);
        }

        _editorState.SetTextSelection(result.Match.Start, result.Match.Length);
    }

    private async Task RevealSearchSummaryAsync(SearchResultSummary summary)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var page = doc.FindPage(summary.PageId);
        var balloon = page?.FindBalloon(summary.BalloonId);
        if (page == null || balloon == null) return;

        var result = new SearchResult(
            new SearchTarget(page, balloon),
            new TextMatch(summary.FirstMatchStart, summary.FirstMatchLength));

        await RevealSearchResultAsync(result);
        _findReplaceSession.UpdateMatch(page.Id, balloon.Id, summary.FirstMatchStart, summary.FirstMatchLength);
    }



    private enum SearchScope
    {
        ActivePage,
        AllPages
    }

    private readonly struct SearchTarget
    {
        public SearchTarget(DocumentPage page, Balloon balloon)
        {
            Page = page;
            Balloon = balloon;
        }

        public DocumentPage Page { get; }
        public Balloon Balloon { get; }
    }

    private readonly struct SearchResult
    {
        public SearchResult(SearchTarget target, TextMatch match)
        {
            Target = target;
            Match = match;
        }

        public SearchTarget Target { get; }
        public TextMatch Match { get; }
    }

    private sealed class SearchResultSummary
    {
        public SearchResultSummary(
            Guid pageId,
            Guid balloonId,
            string pageName,
            string previewText,
            int matchCount,
            int firstMatchStart,
            int firstMatchLength)
        {
            PageId = pageId;
            BalloonId = balloonId;
            PageName = pageName;
            PreviewText = previewText;
            MatchCount = matchCount;
            FirstMatchStart = firstMatchStart;
            FirstMatchLength = firstMatchLength;
        }

        public Guid PageId { get; }
        public Guid BalloonId { get; }
        public string PageName { get; }
        public string PreviewText { get; }
        public int MatchCount { get; }
        public int FirstMatchStart { get; }
        public int FirstMatchLength { get; }
    }

    private sealed class FindReplaceSession
    {
        public string Query { get; private set; } = "";
        public string Replacement { get; private set; } = "";
        public bool MatchCase { get; private set; }
        public bool WholeWord { get; private set; }
        public bool UseRegex { get; private set; }
        public SearchScope Scope { get; private set; } = SearchScope.ActivePage;
        public Model.Color HighlightColor { get; private set; } = new Model.Color(0, 120, 215, 90);

        public Guid? LastPageId { get; private set; }
        public Guid? LastBalloonId { get; private set; }
        public int LastMatchStart { get; private set; }
        public int LastMatchLength { get; private set; }

        public bool HasMatch => LastPageId.HasValue && LastBalloonId.HasValue;

        public void UpdateOptions(string query, string replacement, bool matchCase, bool wholeWord, bool useRegex, SearchScope scope)
        {
            query ??= "";
            replacement ??= "";

            var optionsChanged = !string.Equals(Query, query, StringComparison.Ordinal) ||
                                 MatchCase != matchCase ||
                                 WholeWord != wholeWord ||
                                 UseRegex != useRegex ||
                                 Scope != scope;

            Query = query;
            Replacement = replacement;
            MatchCase = matchCase;
            WholeWord = wholeWord;
            UseRegex = useRegex;
            Scope = scope;

            if (optionsChanged)
            {
                ResetMatch();
            }
        }

        public TextSearchOptions BuildOptions()
        {
            return new TextSearchOptions
            {
                Query = Query,
                MatchCase = MatchCase,
                WholeWord = WholeWord,
                UseRegex = UseRegex
            };
        }

        public void UpdateHighlightColor(Model.Color color)
        {
            HighlightColor = color;
        }

        public void UpdateMatch(Guid pageId, Guid balloonId, int start, int length)
        {
            LastPageId = pageId;
            LastBalloonId = balloonId;
            LastMatchStart = start;
            LastMatchLength = length;
        }

        public void ResetMatch()
        {
            LastPageId = null;
            LastBalloonId = null;
            LastMatchStart = 0;
            LastMatchLength = 0;
        }
    }

    private sealed class FindReplaceWindow : Window
    {
        private readonly MainWindow _owner;
        private readonly bool _replaceMode;
        private readonly TextBox _findBox;
        private readonly TextBox? _replaceBox;
        private readonly StackPanel _historySection;
        private readonly ListView _historyList;
        private readonly Border _highlightPreview;
        private readonly ComboBox _highlightColorCombo;
        private readonly Slider _highlightOpacitySlider;
        private readonly TextBlock _highlightOpacityText;
        private readonly CheckBox _matchCaseCheck;
        private readonly CheckBox _wholeWordCheck;
        private readonly CheckBox _regexCheck;
        private readonly ComboBox _scopeCombo;
        private readonly TextBlock _statusText;
        private readonly Button _findNextButton;
        private readonly Button _findAllButton;
        private readonly Button? _replaceButton;
        private readonly Button? _replaceAllButton;
        private readonly TextBlock _resultsSummaryText;
        private readonly StackPanel _resultsPanel;
        private bool _isUpdating;

        public FindReplaceWindow(MainWindow owner, bool replaceMode)
        {
            _owner = owner;
            _replaceMode = replaceMode;
            Title = replaceMode ? _owner.L("find.title.replace") : _owner.L("find.title");

            _findBox = new TextBox { PlaceholderText = _owner.L("find.placeholder") };
            _replaceBox = replaceMode ? new TextBox { PlaceholderText = _owner.L("find.replace_placeholder") } : null;

            _historyList = new ListView
            {
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.None,
                MaxHeight = 90
            };
            _historyList.ItemClick += (_, e) =>
            {
                if (e.ClickedItem is not string query) return;
                _findBox.Text = query;
                _findBox.SelectionStart = _findBox.Text.Length;
                _findBox.SelectionLength = 0;
                OnFieldChanged();
            };

            _historySection = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
            _historySection.Children.Add(new TextBlock
            {
                Text = _owner.L("find.label.recent_searches"),
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            _historySection.Children.Add(_historyList);

            _highlightPreview = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 85, 85, 85)),
                BorderThickness = new Thickness(1)
            };

            _highlightColorCombo = new ComboBox { Width = 140 };
            _highlightColorCombo.Items.Add(new ComboBoxItem { Content = _owner.L("find.color.blue"), Tag = "#0078D7" });
            _highlightColorCombo.Items.Add(new ComboBoxItem { Content = _owner.L("find.color.yellow"), Tag = "#FFCC00" });
            _highlightColorCombo.Items.Add(new ComboBoxItem { Content = _owner.L("find.color.green"), Tag = "#2E8B57" });
            _highlightColorCombo.Items.Add(new ComboBoxItem { Content = _owner.L("find.color.red"), Tag = "#CC3333" });
            _highlightColorCombo.Items.Add(new ComboBoxItem { Content = _owner.L("find.color.gray"), Tag = "#808080" });

            _highlightOpacitySlider = new Slider
            {
                Minimum = 10,
                Maximum = 90,
                StepFrequency = 5,
                Width = 140
            };

            _highlightOpacityText = new TextBlock
            {
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };

            _matchCaseCheck = new CheckBox { Content = _owner.L("find.option.match_case") };
            _wholeWordCheck = new CheckBox { Content = _owner.L("find.option.whole_word") };
            _regexCheck = new CheckBox { Content = _owner.L("find.option.regex") };

            _scopeCombo = new ComboBox { Width = 160 };
            _scopeCombo.Items.Add(_owner.L("find.scope.current_page"));
            _scopeCombo.Items.Add(_owner.L("find.scope.all_pages"));

            _statusText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };

            _findNextButton = new Button { Content = _owner.L("find.button.find_next"), MinWidth = 90 };
            _findAllButton = new Button { Content = _owner.L("find.button.find_all"), MinWidth = 90 };
            _replaceButton = replaceMode ? new Button { Content = _owner.L("find.button.replace"), MinWidth = 90 } : null;
            _replaceAllButton = replaceMode ? new Button { Content = _owner.L("find.button.replace_all"), MinWidth = 90 } : null;
            var closeButton = new Button { Content = _owner.L("common.close"), MinWidth = 90 };

            _resultsSummaryText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };
            _resultsPanel = new StackPanel { Spacing = 6 };

            _findBox.TextChanged += (_, __) => OnFieldChanged();
            if (_replaceBox != null)
            {
                _replaceBox.TextChanged += (_, __) => OnFieldChanged(clearStatus: false);
            }

            _matchCaseCheck.Checked += (_, __) => OnFieldChanged();
            _matchCaseCheck.Unchecked += (_, __) => OnFieldChanged();
            _wholeWordCheck.Checked += (_, __) => OnFieldChanged();
            _wholeWordCheck.Unchecked += (_, __) => OnFieldChanged();
            _regexCheck.Checked += (_, __) => OnFieldChanged();
            _regexCheck.Unchecked += (_, __) => OnFieldChanged();
            _scopeCombo.SelectionChanged += (_, __) => OnFieldChanged();
            _highlightColorCombo.SelectionChanged += (_, __) => OnHighlightChanged();
            _highlightOpacitySlider.ValueChanged += (_, __) => OnHighlightChanged();

            _findBox.KeyDown += async (_, e) =>
            {
                if (e.Key == VirtualKey.Enter)
                {
                    await RunFindNextAsync();
                    e.Handled = true;
                }
            };

            if (_replaceBox != null)
            {
                _replaceBox.KeyDown += async (_, e) =>
                {
                    if (e.Key == VirtualKey.Enter)
                    {
                        await RunReplaceAsync();
                        e.Handled = true;
                    }
                };
            }

            _findNextButton.Click += async (_, __) => await RunFindNextAsync();
            _findAllButton.Click += (_, __) => RefreshResults();
            if (_replaceButton != null)
            {
                _replaceButton.Click += async (_, __) => await RunReplaceAsync();
            }
            if (_replaceAllButton != null)
            {
                _replaceAllButton.Click += async (_, __) => await RunReplaceAllAsync();
            }

            closeButton.Click += (_, __) => Close();

            var panel = new StackPanel { Spacing = 12, Padding = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = _owner.L("find.label.find"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
            panel.Children.Add(_findBox);
            panel.Children.Add(_historySection);

            if (_replaceMode && _replaceBox != null)
            {
                panel.Children.Add(new TextBlock { Text = _owner.L("find.label.replace"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
                panel.Children.Add(_replaceBox);
            }

            var optionsPanel = new StackPanel { Spacing = 6 };
            optionsPanel.Children.Add(_matchCaseCheck);
            optionsPanel.Children.Add(_wholeWordCheck);
            optionsPanel.Children.Add(_regexCheck);

            var scopePanel = new StackPanel { Spacing = 6 };
            scopePanel.Children.Add(new TextBlock { Text = _owner.L("find.label.scope"), FontSize = 12, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
            scopePanel.Children.Add(_scopeCombo);

            var optionsGrid = new Grid();
            optionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            optionsGrid.Children.Add(optionsPanel);
            optionsGrid.Children.Add(scopePanel);
            Grid.SetColumn(scopePanel, 1);
            panel.Children.Add(optionsGrid);

            var highlightPanel = new StackPanel { Spacing = 6 };
            highlightPanel.Children.Add(new TextBlock
            {
                Text = _owner.L("find.label.highlight"),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });

            var highlightColorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            highlightColorRow.Children.Add(_highlightPreview);
            highlightColorRow.Children.Add(_highlightColorCombo);
            highlightPanel.Children.Add(highlightColorRow);

            var opacityGrid = new Grid();
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            opacityGrid.Children.Add(new TextBlock
            {
                    Text = _owner.L("find.label.opacity"),
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            opacityGrid.Children.Add(_highlightOpacityText);
            Grid.SetColumn(_highlightOpacityText, 1);
            highlightPanel.Children.Add(opacityGrid);
            highlightPanel.Children.Add(_highlightOpacitySlider);

            panel.Children.Add(highlightPanel);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            buttonRow.Children.Add(_findNextButton);
            buttonRow.Children.Add(_findAllButton);
            if (_replaceButton != null)
            {
                buttonRow.Children.Add(_replaceButton);
            }
            if (_replaceAllButton != null)
            {
                buttonRow.Children.Add(_replaceAllButton);
            }
            buttonRow.Children.Add(closeButton);
            panel.Children.Add(buttonRow);

            panel.Children.Add(_statusText);

            panel.Children.Add(new TextBlock
            {
                Text = _owner.L("find.label.results"),
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            });
            panel.Children.Add(_resultsSummaryText);

            var resultsScroll = new ScrollViewer
            {
                Content = _resultsPanel,
                MaxHeight = _replaceMode ? 220 : 260,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            panel.Children.Add(resultsScroll);

            Content = panel;

            var size = new SizeInt32(420, _replaceMode ? 620 : 560);
            AppWindow.Resize(size);
        }

        public void SyncFromSession()
        {
            _isUpdating = true;
            var session = _owner._findReplaceSession;

            _findBox.Text = session.Query;
            if (_replaceBox != null)
            {
                _replaceBox.Text = session.Replacement;
            }

            _matchCaseCheck.IsChecked = session.MatchCase;
            _wholeWordCheck.IsChecked = session.WholeWord;
            _regexCheck.IsChecked = session.UseRegex;
            _scopeCombo.SelectedIndex = session.Scope == SearchScope.AllPages ? 1 : 0;
            _statusText.Text = "";
            SyncHighlightControls(session.HighlightColor);
            _owner.UpdateSearchHighlightStyle(session.HighlightColor);
            RefreshSearchHistory();
            ClearResults();

            _isUpdating = false;
            UpdateButtons();
        }

        private void OnFieldChanged(bool clearStatus = true)
        {
            if (_isUpdating) return;
            UpdateSession();
            UpdateButtons();
            if (clearStatus)
            {
                _statusText.Text = "";
            }
            ClearResults();
        }

        private void OnHighlightChanged()
        {
            if (_isUpdating) return;
            UpdateHighlightStyle();
        }

        private void UpdateSession()
        {
            var scope = _scopeCombo.SelectedIndex == 1 ? SearchScope.AllPages : SearchScope.ActivePage;
            _owner._findReplaceSession.UpdateOptions(
                _findBox.Text ?? string.Empty,
                _replaceBox?.Text ?? string.Empty,
                _matchCaseCheck.IsChecked == true,
                _wholeWordCheck.IsChecked == true,
                _regexCheck.IsChecked == true,
                scope);
        }

        private void UpdateButtons()
        {
            var hasQuery = !string.IsNullOrWhiteSpace(_findBox.Text);
            _findNextButton.IsEnabled = hasQuery;
            _findAllButton.IsEnabled = hasQuery;
            if (_replaceButton != null)
            {
                _replaceButton.IsEnabled = hasQuery;
            }
            if (_replaceAllButton != null)
            {
                _replaceAllButton.IsEnabled = hasQuery;
            }
        }

        private void UpdateHighlightStyle()
        {
            var color = BuildHighlightColorFromControls();
            _owner._findReplaceSession.UpdateHighlightColor(color);
            _owner.UpdateSearchHighlightStyle(color);
            UpdateHighlightPreview(color);
        }

        private Model.Color BuildHighlightColorFromControls()
        {
            var baseColor = _owner._findReplaceSession.HighlightColor;
            if (_highlightColorCombo.SelectedItem is ComboBoxItem item &&
                item.Tag is string colorHex)
            {
                baseColor = ParseHexColor(colorHex);
            }

            var opacityPercent = _highlightOpacitySlider.Value;
            var alpha = (byte)Math.Clamp(Math.Round(opacityPercent / 100d * 255d), 0d, 255d);
            _highlightOpacityText.Text = $"{opacityPercent:F0}%";
            return new Model.Color(baseColor.R, baseColor.G, baseColor.B, alpha);
        }

        private void UpdateHighlightPreview(Model.Color color)
        {
            _highlightPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));

            var percent = Math.Clamp(Math.Round(color.A / 255d * 100d), 0d, 100d);
            _highlightOpacityText.Text = $"{percent:F0}%";
        }

        private void SyncHighlightControls(Model.Color color)
        {
            _highlightColorCombo.SelectedIndex = -1;
            var colorTag = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            for (int i = 0; i < _highlightColorCombo.Items.Count; i++)
            {
                if (_highlightColorCombo.Items[i] is ComboBoxItem colorItem &&
                    string.Equals(colorItem.Tag?.ToString(), colorTag, StringComparison.OrdinalIgnoreCase))
                {
                    _highlightColorCombo.SelectedIndex = i;
                    break;
                }
            }

            var percent = Math.Clamp(Math.Round(color.A / 255d * 100d), 10d, 90d);
            _highlightOpacitySlider.Value = percent;
            UpdateHighlightPreview(color);
        }

        private async Task RunFindNextAsync()
        {
            UpdateSession();
            var options = _owner._findReplaceSession.BuildOptions();
            await _owner.ExecuteFindNextAsync(options, _owner._findReplaceSession.Scope, _statusText);
            RefreshResults();
        }

        private async Task RunReplaceAsync()
        {
            if (!_replaceMode || _replaceBox == null) return;
            UpdateSession();
            var options = _owner._findReplaceSession.BuildOptions();
            await _owner.ExecuteReplaceAsync(options, _replaceBox.Text ?? string.Empty, _owner._findReplaceSession.Scope, _statusText);
            RefreshResults();
        }

        private async Task RunReplaceAllAsync()
        {
            if (!_replaceMode || _replaceBox == null) return;
            UpdateSession();
            var options = _owner._findReplaceSession.BuildOptions();
            await _owner.ExecuteReplaceAllAsync(options, _replaceBox.Text ?? string.Empty, _owner._findReplaceSession.Scope, _statusText);
            RefreshResults();
        }

        public void RefreshSearchHistory()
        {
            if (_owner._searchHistory.Count == 0)
            {
                _historyList.ItemsSource = null;
                _historySection.Visibility = Visibility.Collapsed;
                return;
            }

            _historySection.Visibility = Visibility.Visible;
            _historyList.ItemsSource = _owner._searchHistory.ToList();
        }

        private void RefreshResults()
        {
            UpdateSession();
            _resultsPanel.Children.Clear();

            var options = _owner._findReplaceSession.BuildOptions();
            if (!_owner.TryBuildSearchResultSummaries(
                    options,
                    _owner._findReplaceSession.Scope,
                    out var results,
                    out var totalMatches,
                    out var error))
            {
                _resultsSummaryText.Text = error ?? "No matches found.";
                return;
            }

            _owner.RecordSearchQuery(options.Query);
            _resultsSummaryText.Text = $"{totalMatches} match(es) in {results.Count} balloon(s).";

            foreach (var summary in results)
            {
                _resultsPanel.Children.Add(BuildResultButton(summary));
            }
        }

        private void ClearResults()
        {
            _resultsPanel.Children.Clear();
            _resultsSummaryText.Text = string.IsNullOrWhiteSpace(_findBox.Text)
                ? "Enter text to find."
                : "Use Find All to list matches.";
        }

        private Button BuildResultButton(SearchResultSummary summary)
        {
            var header = new TextBlock
            {
                Text = $"{summary.PageName} - {summary.MatchCount} match(es)",
                FontSize = 12,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray)
            };

            var preview = new TextBlock
            {
                Text = summary.PreviewText,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };

            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(header);
            content.Children.Add(preview);

            var button = new Button
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(6, 4, 6, 4)
            };
            button.Click += async (_, __) => await _owner.RevealSearchSummaryAsync(summary);
            return button;
        }
    }


}
