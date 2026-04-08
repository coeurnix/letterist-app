using Letterist.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Windows.Graphics;
using Windows.System;

namespace Letterist;

public sealed partial class MainWindow
{
    private Window? _helpWindow;
    private WebView2? _helpWebView;
    private TextBox? _helpSearchBox;
    private Button? _helpSearchPreviousButton;
    private Button? _helpSearchNextButton;
    private TextBlock? _helpSearchStatusText;
    private string? _helpLastSearchQuery;
    private int _helpSearchMatchCount;
    private int _helpSearchCurrentIndex = -1;

    private void HelpQuickstart_Click(object sender, RoutedEventArgs e)
    {
        ShowHelpWindow("quickstart");
    }

    private void HelpContents_Click(object sender, RoutedEventArgs e)
    {
        ShowHelpWindow("contents");
    }

    private void ShowHelpWindow(string section)
    {
        var helpPath = GetHelpPath(section);
        if (string.IsNullOrEmpty(helpPath) || !File.Exists(helpPath))
        {
            SetStatusMessage($"Help file not found: {section}");
            return;
        }

        if (_helpWindow != null)
        {
            NavigateHelpWebView(helpPath);
            _helpWindow.Activate();
            return;
        }

        _helpWindow = new Window();
        _helpWindow.Title = L("help.window_title");

        var rootGrid = new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30)),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        var searchBar = new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 37, 37, 37)),
            Padding = new Thickness(12, 8, 12, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var searchIcon = new FontIcon
        {
            Glyph = "\uE721",
            FontSize = 14,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 136, 136, 136)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(searchIcon, 0);
        searchBar.Children.Add(searchIcon);

        _helpSearchBox = new TextBox
        {
            PlaceholderText = L("help.search_placeholder"),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 45, 45, 45)),
            BorderThickness = new Thickness(1),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 60, 60, 60)),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        _helpSearchBox.KeyDown += HelpSearchBox_KeyDown;
        Grid.SetColumn(_helpSearchBox, 1);
        searchBar.Children.Add(_helpSearchBox);

        _helpSearchPreviousButton = new Button
        {
            Content = L("help.search_previous"),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 4, 0),
            MinWidth = 56,
            IsEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        _helpSearchPreviousButton.Click += HelpSearchPreviousButton_Click;
        Grid.SetColumn(_helpSearchPreviousButton, 2);
        searchBar.Children.Add(_helpSearchPreviousButton);

        _helpSearchNextButton = new Button
        {
            Content = L("help.search_next"),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 56,
            IsEnabled = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        _helpSearchNextButton.Click += HelpSearchNextButton_Click;
        Grid.SetColumn(_helpSearchNextButton, 3);
        searchBar.Children.Add(_helpSearchNextButton);

        _helpSearchStatusText = new TextBlock
        {
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 136, 136, 136)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(_helpSearchStatusText, 2);
        searchBar.Children.Add(_helpSearchStatusText);

        Grid.SetRow(searchBar, 0);
        rootGrid.Children.Add(searchBar);

        _helpWebView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_helpWebView, 1);
        rootGrid.Children.Add(_helpWebView);

        _helpWindow.Content = rootGrid;
        AttachEscapeToCloseWindow(_helpWindow, rootGrid);

        var helpAppWindow = _helpWindow.AppWindow;
        helpAppWindow.Resize(new SizeInt32(900, 700));
        CenterChildWindowOverMainWindow(helpAppWindow);
        TryApplyFontChooserTitleBarTheme(helpAppWindow);
        SetWindowAlwaysOnTop(_helpWindow, isAlwaysOnTop: true);

        _helpWindow.Closed += HelpWindow_Closed;
        _helpWindow.Activate();

        _ = _helpWindow.DispatcherQueue.TryEnqueue(() =>
        {
            TryApplyFontChooserTitleBarTheme(helpAppWindow);
        });

        _ = InitializeHelpWebViewAsync(helpPath);
    }

    private void HelpSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var query = _helpSearchBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                if (string.Equals(query, _helpLastSearchQuery, StringComparison.OrdinalIgnoreCase) && _helpSearchMatchCount > 0)
                {
                    _ = NavigateHelpSearchResultAsync(forward: true);
                }
                else
                {
                    _ = ExecuteHelpSearchAsync(query);
                }
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            _ = ClearHelpSearchAsync();
            e.Handled = true;
        }
    }

    private void HelpSearchPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _ = NavigateHelpSearchResultAsync(forward: false);
    }

    private void HelpSearchNextButton_Click(object sender, RoutedEventArgs e)
    {
        _ = NavigateHelpSearchResultAsync(forward: true);
    }

    private async Task ExecuteHelpSearchAsync(string query)
    {
        if (_helpWebView?.CoreWebView2 == null) return;

        try
        {
            await ClearHelpSearchHighlightsAsync();

            var escapedQuery = query
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            var script = $@"
(function() {{
    var query = '{escapedQuery}'.toLowerCase();
    var body = document.body;
    var walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT, null, false);
    var matches = 0;
    var firstMatch = null;
    var nodesToProcess = [];

    while (walker.nextNode()) {{
        var node = walker.currentNode;
        if (node.nodeValue.toLowerCase().indexOf(query) !== -1) {{
            nodesToProcess.push(node);
        }}
    }}

    for (var i = 0; i < nodesToProcess.length; i++) {{
        var node = nodesToProcess[i];
        var text = node.nodeValue;
        var lower = text.toLowerCase();
        var idx = lower.indexOf(query);
        if (idx === -1) continue;

        var parent = node.parentNode;
        if (parent.classList && parent.classList.contains('letterist-search-highlight')) continue;

        var frag = document.createDocumentFragment();
        var lastIdx = 0;
        while (idx !== -1) {{
            if (idx > lastIdx) {{
                frag.appendChild(document.createTextNode(text.substring(lastIdx, idx)));
            }}
            var span = document.createElement('span');
            span.className = 'letterist-search-highlight';
            span.style.backgroundColor = '#F9A825';
            span.style.color = '#000';
            span.style.borderRadius = '2px';
            span.style.padding = '0 1px';
            span.textContent = text.substring(idx, idx + query.length);
            frag.appendChild(span);
            if (!firstMatch) firstMatch = span;
            matches++;
            lastIdx = idx + query.length;
            idx = lower.indexOf(query, lastIdx);
        }}
        if (lastIdx < text.length) {{
            frag.appendChild(document.createTextNode(text.substring(lastIdx)));
        }}
        parent.replaceChild(frag, node);
    }}

    if (firstMatch) {{
        firstMatch.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
        firstMatch.classList.add('letterist-search-active');
        firstMatch.style.backgroundColor = '#FF6D00';
    }}

    return {{ count: matches, currentIndex: firstMatch ? 0 : -1 }};
}})()";

            var result = await _helpWebView.CoreWebView2.ExecuteScriptAsync(script);
            _helpLastSearchQuery = query;
            var (count, currentIndex) = ParseHelpSearchResult(result);
            UpdateHelpSearchUi(count, currentIndex);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Help search failed", ex);
        }
    }

    private async Task NavigateHelpSearchResultAsync(bool forward)
    {
        if (_helpWebView?.CoreWebView2 == null || _helpSearchMatchCount <= 0) return;

        try
        {
            var direction = forward ? "1" : "-1";
            var script = $@"
(function() {{
    var highlights = Array.from(document.querySelectorAll('.letterist-search-highlight'));
    if (highlights.length === 0) {{
        return {{ count: 0, currentIndex: -1 }};
    }}

    var currentIndex = highlights.findIndex(function(node) {{
        return node.classList.contains('letterist-search-active');
    }});
    if (currentIndex < 0) currentIndex = 0;

    var current = highlights[currentIndex];
    current.classList.remove('letterist-search-active');
    current.style.backgroundColor = '#F9A825';

    var nextIndex = (currentIndex + ({direction}) + highlights.length) % highlights.length;
    var next = highlights[nextIndex];
    next.classList.add('letterist-search-active');
    next.style.backgroundColor = '#FF6D00';
    next.scrollIntoView({{ behavior: 'smooth', block: 'center' }});

    return {{ count: highlights.length, currentIndex: nextIndex }};
}})()";

            var result = await _helpWebView.CoreWebView2.ExecuteScriptAsync(script);
            var (count, currentIndex) = ParseHelpSearchResult(result);
            UpdateHelpSearchUi(count, currentIndex);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Help search navigate failed", ex);
        }
    }

    private static (int Count, int CurrentIndex) ParseHelpSearchResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return (0, -1);
        }

        try
        {
            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            var count = root.TryGetProperty("count", out var countNode) ? countNode.GetInt32() : 0;
            var currentIndex = root.TryGetProperty("currentIndex", out var indexNode) ? indexNode.GetInt32() : -1;
            return (Math.Max(0, count), currentIndex);
        }
        catch
        {
            return (0, -1);
        }
    }

    private void UpdateHelpSearchUi(int count, int currentIndex)
    {
        _helpSearchMatchCount = Math.Max(0, count);
        _helpSearchCurrentIndex = _helpSearchMatchCount > 0
            ? Math.Clamp(currentIndex, 0, _helpSearchMatchCount - 1)
            : -1;

        if (_helpSearchStatusText != null)
        {
            _helpSearchStatusText.Text = _helpSearchMatchCount > 0
                ? $"{LF("help.search_results", _helpSearchMatchCount)} ({_helpSearchCurrentIndex + 1}/{_helpSearchMatchCount})"
                : L("help.search_no_results");
        }

        var hasResults = _helpSearchMatchCount > 0;
        if (_helpSearchPreviousButton != null)
        {
            _helpSearchPreviousButton.IsEnabled = hasResults;
        }
        if (_helpSearchNextButton != null)
        {
            _helpSearchNextButton.IsEnabled = hasResults;
        }
    }

    private async Task ClearHelpSearchAsync()
    {
        if (_helpSearchBox != null)
        {
            _helpSearchBox.Text = "";
        }
        if (_helpSearchStatusText != null)
        {
            _helpSearchStatusText.Text = "";
        }
        _helpLastSearchQuery = null;
        _helpSearchMatchCount = 0;
        _helpSearchCurrentIndex = -1;
        if (_helpSearchPreviousButton != null) _helpSearchPreviousButton.IsEnabled = false;
        if (_helpSearchNextButton != null) _helpSearchNextButton.IsEnabled = false;
        await ClearHelpSearchHighlightsAsync();
    }

    private async Task ClearHelpSearchHighlightsAsync()
    {
        if (_helpWebView?.CoreWebView2 == null) return;

        try
        {
            var clearScript = @"
(function() {
    var highlights = document.querySelectorAll('.letterist-search-highlight');
    for (var i = highlights.length - 1; i >= 0; i--) {
        var span = highlights[i];
        var parent = span.parentNode;
        parent.replaceChild(document.createTextNode(span.textContent), span);
        parent.normalize();
    }
})()";
            await _helpWebView.CoreWebView2.ExecuteScriptAsync(clearScript);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Help clear search failed", ex);
        }
    }

    private async Task InitializeHelpWebViewAsync(string helpPath)
    {
        if (_helpWebView == null) return;

        try
        {
            await _helpWebView.EnsureCoreWebView2Async();
            _helpWebView.CoreWebView2.Settings.IsScriptEnabled = true;
            _helpWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _helpWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            NavigateHelpWebView(helpPath);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Failed to initialize WebView2 for help", ex);
        }
    }

    private void NavigateHelpWebView(string helpPath)
    {
        if (_helpWebView?.CoreWebView2 != null)
        {
            var builder = new UriBuilder(new Uri(helpPath))
            {
                Query = $"lang={Uri.EscapeDataString(UiLocalizationService.CurrentLanguage)}"
            };
            _helpWebView.CoreWebView2.Navigate(builder.Uri.AbsoluteUri);
        }
    }

    private void HelpWindow_Closed(object sender, WindowEventArgs args)
    {
        _helpWebView = null;
        _helpSearchBox = null;
        _helpSearchPreviousButton = null;
        _helpSearchNextButton = null;
        _helpSearchStatusText = null;
        _helpLastSearchQuery = null;
        _helpSearchMatchCount = 0;
        _helpSearchCurrentIndex = -1;
        _helpWindow = null;
    }

    private static string? GetHelpPath(string section)
    {
        var exeDir = AppContext.BaseDirectory;
        var helpDir = Path.Combine(exeDir, "Help", section);
        var indexPath = Path.Combine(helpDir, "index.html");
        return indexPath;
    }
}
