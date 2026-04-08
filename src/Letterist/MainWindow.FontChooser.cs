using Letterist.Diagnostics;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Letterist;

public sealed partial class MainWindow
{
    private const double FontChooserWindowInitialWidth = 920;
    private const double FontChooserWindowInitialHeight = 700;
    private const double FontChooserWindowMinWidth = 620;
    private const double FontChooserWindowMinHeight = 420;
    private const double FontChooserInitialNameColumnWidth = 220;
    private const double FontChooserMinNameColumnWidth = 120;
    private const double FontChooserMaxNameColumnWidth = 420;
    private static List<string>? _cachedSystemFonts;
    private static readonly object FontCacheLock = new();
    private static DataTemplate? _fontChooserItemTemplate;
    private string _currentFontFamily = "Segoe UI";
    private bool _isFontChooserDialogOpen;

    private sealed class FontChooserLayoutState : INotifyPropertyChanged
    {
        private double _nameColumnWidth;

        public FontChooserLayoutState(double nameColumnWidth)
        {
            _nameColumnWidth = nameColumnWidth;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public double NameColumnWidth
        {
            get => _nameColumnWidth;
            set
            {
                if (Math.Abs(_nameColumnWidth - value) < 0.1) return;
                _nameColumnWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameColumnWidth)));
            }
        }
    }

    private sealed class FontChooserRow
    {
        public FontChooserRow(string name, string sampleText, FontChooserLayoutState layout)
        {
            Name = name;
            SampleText = sampleText;
            Layout = layout;
            try
            {
                SampleFont = new FontFamily(name);
            }
            catch
            {
                SampleFont = new FontFamily("Segoe UI");
            }
        }

        public string Name { get; }
        public string SampleText { get; }
        public FontFamily SampleFont { get; }
        public FontChooserLayoutState Layout { get; }
    }

    private void UpdateFontChooserDisplay(string fontFamily)
    {
        _currentFontFamily = fontFamily;
        if (FontChooserButtonText != null)
        {
            FontChooserButtonText.Text = fontFamily;
        }
        if (FontChooserButtonPreview != null)
        {
            FontChooserButtonPreview.Text = L("font_chooser.preview_short");
            FontChooserButtonPreview.FontFamily = new FontFamily(fontFamily);
        }
    }

    private async void FontChooserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isFontChooserDialogOpen)
        {
            return;
        }

        _isFontChooserDialogOpen = true;
        if (FontChooserButton != null)
        {
            FontChooserButton.IsEnabled = false;
        }

        try
        {
            var selectedFont = await ShowFontChooserDialogAsync(_currentFontFamily);
            if (selectedFont != null)
            {
                UpdateFontChooserDisplay(selectedFont);
                ApplyInlineTextStyle(style => style.With(fontFamily: selectedFont));
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Font chooser dialog failed.", ex);
        }
        finally
        {
            _isFontChooserDialogOpen = false;
            if (FontChooserButton != null)
            {
                FontChooserButton.IsEnabled = true;
            }
        }
    }

    private async Task<string?> ShowFontChooserDialogAsync(string currentFont)
    {
        var selectedFont = (string?)null;
        var isClosed = false;
        var isCompleted = false;
        var sampleText = GetFontSampleTextForLanguage(UiLocalizationService.CurrentLanguage);
        var layoutState = new FontChooserLayoutState(FontChooserInitialNameColumnWidth);
        var rows = new ObservableCollection<FontChooserRow>();
        List<string> allFonts = [];
        var completion = new TaskCompletionSource<string?>();
        var chooserWindow = new Window
        {
            Title = L("font_chooser.title")
        };

        var searchBox = new AutoSuggestBox
        {
            PlaceholderText = L("font_chooser.search_placeholder"),
            QueryIcon = new SymbolIcon(Symbol.Find),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var tableGrid = new Grid
        {
            Margin = new Thickness(0, 0, 0, 4)
        };
        var nameColumn = new ColumnDefinition { Width = new GridLength(layoutState.NameColumnWidth) };
        tableGrid.ColumnDefinitions.Add(nameColumn);
        tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        tableGrid.Children.Add(new TextBlock
        {
            Text = L("font_chooser.column.font"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 160, 160, 160))
        });
        var columnSplitter = new Thumb
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 72, 72, 72))
        };
        columnSplitter.DragDelta += (_, args) =>
        {
            var width = Math.Clamp(layoutState.NameColumnWidth + args.HorizontalChange, FontChooserMinNameColumnWidth, FontChooserMaxNameColumnWidth);
            nameColumn.Width = new GridLength(width);
            layoutState.NameColumnWidth = width;
        };
        Grid.SetColumn(columnSplitter, 1);
        tableGrid.Children.Add(columnSplitter);
        var sampleHeader = new TextBlock
        {
            Text = L("font_chooser.column.sample"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 160, 160, 160))
        };
        Grid.SetColumn(sampleHeader, 2);
        tableGrid.Children.Add(sampleHeader);

        var fontListView = new ListView
        {
            IsItemClickEnabled = true,
            SelectionMode = ListViewSelectionMode.Single
        };
        fontListView.HorizontalAlignment = HorizontalAlignment.Stretch;
        fontListView.VerticalAlignment = VerticalAlignment.Stretch;
        fontListView.MinHeight = 220;
        ScrollViewer.SetHorizontalScrollMode(fontListView, ScrollMode.Enabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(fontListView, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollMode(fontListView, ScrollMode.Enabled);
        ScrollViewer.SetVerticalScrollBarVisibility(fontListView, ScrollBarVisibility.Auto);
        fontListView.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
            """
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <ItemsStackPanel/>
            </ItemsPanelTemplate>
            """);
        fontListView.ItemTemplate = GetFontChooserItemTemplate();
        fontListView.ItemsSource = rows;
        fontListView.ItemClick += (s, args) =>
        {
            if (args.ClickedItem is FontChooserRow row)
            {
                fontListView.SelectedItem = row;
            }
        };
        
        var selectButton = new Button
        {
            Content = L("font_chooser.button.select"),
            IsEnabled = false,
            MinWidth = 220,
            Height = 40,
            FontSize = 18,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 52, 233, 146)),
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 10, 10, 10))
        };
        var cancelButton = new Button
        {
            Content = L("common.cancel"),
            MinWidth = 220,
            Height = 40,
            FontSize = 18,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 28, 28, 28)),
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 236, 236, 236))
        };

        void Complete(string? value)
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            selectedFont = value;
            completion.TrySetResult(value);
            chooserWindow.Close();
        }

        var loadingPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };
        loadingPanel.Children.Add(new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = true
        });
        loadingPanel.Children.Add(new TextBlock
        {
            Text = L("font_chooser.loading"),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 180, 180, 180))
        });

        void PopulateRows(string? filter)
        {
            if (allFonts.Count == 0)
            {
                rows.Clear();
                return;
            }

            var filtered = string.IsNullOrWhiteSpace(filter)
                ? allFonts
                : allFonts.Where(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            rows.Clear();
            foreach (var font in filtered)
            {
                rows.Add(new FontChooserRow(font, sampleText, layoutState));
            }

            var selectedIndex = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Name, currentFont, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex >= 0)
            {
                fontListView.SelectedIndex = selectedIndex;
                fontListView.ScrollIntoView(rows[selectedIndex], ScrollIntoViewAlignment.Leading);
            }
        }
        
        _ = LoadFontsAsync();

        searchBox.TextChanged += (s, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                PopulateRows(searchBox.Text);
            }
        };

        fontListView.SelectionChanged += (s, args) =>
        {
            selectButton.IsEnabled = fontListView.SelectedItem is FontChooserRow;
        };

        fontListView.DoubleTapped += (s, args) =>
        {
            if (fontListView.SelectedItem is FontChooserRow row)
            {
                Complete(row.Name);
            }
        };

        selectButton.Click += (s, e) =>
        {
            if (fontListView.SelectedItem is FontChooserRow row)
            {
                Complete(row.Name);
            }
        };
        cancelButton.Click += (s, e) => Complete(null);

        var buttonsPanel = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 560,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        buttonsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(cancelButton, 0);
        Grid.SetColumn(selectButton, 2);
        cancelButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        selectButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        buttonsPanel.Children.Add(cancelButton);
        buttonsPanel.Children.Add(selectButton);

        var contentGrid = new Grid
        {
            Padding = new Thickness(16),
            RequestedTheme = ElementTheme.Dark
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(searchBox, 0);
        contentGrid.Children.Add(searchBox);
        Grid.SetRow(loadingPanel, 1);
        contentGrid.Children.Add(loadingPanel);
        Grid.SetRow(tableGrid, 2);
        contentGrid.Children.Add(tableGrid);
        Grid.SetRow(fontListView, 3);
        contentGrid.Children.Add(fontListView);
        Grid.SetRow(buttonsPanel, 4);
        contentGrid.Children.Add(buttonsPanel);

        chooserWindow.Content = contentGrid;
        AttachEscapeToCloseWindow(chooserWindow, contentGrid);
        var chooserAppWindow = chooserWindow.AppWindow;
        if (chooserAppWindow.Presenter is not OverlappedPresenter)
        {
            chooserAppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        }

        if (chooserAppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(true, true);
        }

        chooserAppWindow.Resize(new SizeInt32((int)FontChooserWindowInitialWidth, (int)FontChooserWindowInitialHeight));
        TryApplyFontChooserTitleBarTheme(chooserAppWindow);

        CenterChildWindowOverMainWindow(chooserAppWindow);

        chooserAppWindow.Changed += (_, _) =>
        {
            var size = chooserAppWindow.Size;
            var width = Math.Max(size.Width, (int)FontChooserWindowMinWidth);
            var height = Math.Max(size.Height, (int)FontChooserWindowMinHeight);
            if (width != size.Width || height != size.Height)
            {
                chooserAppWindow.Resize(new SizeInt32(width, height));
            }
        };

        chooserWindow.Closed += (_, _) =>
        {
            isClosed = true;
            if (!isCompleted)
            {
                isCompleted = true;
                completion.TrySetResult(selectedFont);
            }
        };
        chooserWindow.Activate();
        _ = chooserWindow.DispatcherQueue.TryEnqueue(() => TryApplyFontChooserTitleBarTheme(chooserAppWindow));
        var titleBarRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        titleBarRetryTimer.Tick += (_, _) =>
        {
            titleBarRetryTimer.Stop();
            TryApplyFontChooserTitleBarTheme(chooserAppWindow);
        };
        titleBarRetryTimer.Start();

        async Task LoadFontsAsync()
        {
            try
            {
                var fonts = await GetSystemFontsAsync();
                if (isClosed)
                {
                    return;
                }

                allFonts = fonts;
                loadingPanel.Visibility = Visibility.Collapsed;
                PopulateRows(searchBox.Text);
            }
            catch (Exception ex)
            {
                StartupLogger.Log("Loading system fonts failed.", ex);
                if (isClosed)
                {
                    return;
                }

                loadingPanel.Visibility = Visibility.Collapsed;
                allFonts = ["Segoe UI"];
                PopulateRows(searchBox.Text);
            }
        }

        return await completion.Task;
    }

    private static void TryApplyFontChooserTitleBarTheme(AppWindow appWindow)
    {
        try
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            var background = Windows.UI.Color.FromArgb(255, 26, 26, 26);
            var foreground = Windows.UI.Color.FromArgb(255, 236, 236, 236);
            var hoverBackground = Windows.UI.Color.FromArgb(255, 45, 45, 45);
            var pressedBackground = Windows.UI.Color.FromArgb(255, 65, 65, 65);
            var titleBar = appWindow.TitleBar;

            titleBar.BackgroundColor = background;
            titleBar.ForegroundColor = foreground;
            titleBar.InactiveBackgroundColor = background;
            titleBar.InactiveForegroundColor = foreground;
            titleBar.ButtonBackgroundColor = background;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveBackgroundColor = background;
            titleBar.ButtonInactiveForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = hoverBackground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedBackgroundColor = pressedBackground;
            titleBar.ButtonPressedForegroundColor = foreground;
        }
        catch (COMException ex)
        {
            StartupLogger.Log("Font chooser title bar customization unavailable.", ex);
        }
    }

    private void CenterChildWindowOverMainWindow(AppWindow childWindow)
    {
        try
        {
            var ownerWindow = this.AppWindow;
            if (ownerWindow == null)
            {
                return;
            }

            var ownerPosition = ownerWindow.Position;
            var ownerSize = ownerWindow.Size;
            var childSize = childWindow.Size;
            var x = ownerPosition.X + ((ownerSize.Width - childSize.Width) / 2);
            var y = ownerPosition.Y + ((ownerSize.Height - childSize.Height) / 2);
            childWindow.Move(new PointInt32(Math.Max(0, x), Math.Max(0, y)));
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Centering font chooser window failed.", ex);
        }
    }

    private static DataTemplate GetFontChooserItemTemplate()
    {
        _fontChooserItemTemplate ??= (DataTemplate)XamlReader.Load(
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="8,6" HorizontalAlignment="Left">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Name}"
                               Width="{Binding Layout.NameColumnWidth}"
                               FontSize="13"
                               VerticalAlignment="Center"
                               TextTrimming="CharacterEllipsis"/>
                    <TextBlock Grid.Column="1"
                               Text="{Binding SampleText}"
                               FontFamily="{Binding SampleFont}"
                               FontSize="16"
                               Margin="12,0,0,0"
                               VerticalAlignment="Center"
                               TextWrapping="NoWrap"/>
                </Grid>
            </DataTemplate>
            """);

        return _fontChooserItemTemplate;
    }

    internal static string GetFontSampleTextForLanguage(string? languageTag)
    {
        var language = UiLocalizationService.NormalizeLanguageTag(languageTag);
        return language switch
        {
            "zh-CN" => "视野无边，字里行间皆有风景",
            "ja" => "いろはにほへと ちりぬるを",
            "ko" => "키스의 고유조건은 입술끼리 만나야 한다",
            "es" => "El veloz murcielago hindu come feliz kiwi",
            "fr" => "Portez ce vieux whisky au juge blond qui fume",
            "de" => "Victor jagt zwolf Boxkampfer quer uber den grossen Deich",
            "pt-BR" => "A rapida raposa marrom salta sobre o cao preguicoso",
            _ => "The quick brown fox jumps over the lazy dog"
        };
    }

    private static Task<List<string>> GetSystemFontsAsync()
    {
        lock (FontCacheLock)
        {
            if (_cachedSystemFonts != null)
            {
                return Task.FromResult(_cachedSystemFonts);
            }
        }

        return Task.Run(GetSystemFonts);
    }

    private static List<string> GetSystemFonts()
    {
        lock (FontCacheLock)
        {
            if (_cachedSystemFonts != null)
            {
                return _cachedSystemFonts;
            }

            var fontSet = CanvasFontSet.GetSystemFontSet();
            var fontNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var face in fontSet.Fonts)
            {
                var familyNames = face.FamilyNames;
                if (familyNames.TryGetValue("en-us", out var name) || familyNames.Count > 0)
                {
                    var fontName = name ?? familyNames.Values.First();
                    if (!string.IsNullOrWhiteSpace(fontName))
                    {
                        fontNames.Add(fontName);
                    }
                }
            }

            _cachedSystemFonts = fontNames.ToList();
            return _cachedSystemFonts;
        }
    }
}
