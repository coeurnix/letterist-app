using Letterist.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Letterist;

public sealed partial class MainWindow
{

    private Window? _translationWindow;
    private Panel? _translationWindowContentParent;
    private Panel? _translationWindowActionsParent;

    private void ShowTranslationWindow_Click(object sender, RoutedEventArgs e)
    {
        ShowTranslationWindow();
    }

    private void ShowTranslationWindow()
    {
        if (_translationWindow != null)
        {
            _translationWindow.Activate();
            return;
        }

        _translationWindowContentParent = TranslationTabContent.Parent as Panel;
        _translationWindowActionsParent = TranslationTabActions.Parent as Panel;
        DetachElementFromParent(TranslationTabContent);
        DetachElementFromParent(TranslationTabActions);

        var rootGrid = new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30)),
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        TranslationTabContent.Visibility = Visibility.Visible;
        TranslationTabActions.Visibility = Visibility.Visible;

        Grid.SetRow(TranslationTabContent, 0);
        rootGrid.Children.Add(TranslationTabContent);

        var actionsBar = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(255, 37, 37, 37)),
            Padding = new Thickness(8)
        };
        actionsBar.Child = TranslationTabActions;
        Grid.SetRow(actionsBar, 1);
        rootGrid.Children.Add(actionsBar);

        _translationWindow = new Window();
        _translationWindow.Title = L("sidebar.tab.translate");
        _translationWindow.Content = rootGrid;
        AttachEscapeToCloseWindow(_translationWindow, rootGrid);
        var translationAppWindow = _translationWindow.AppWindow;
        translationAppWindow.Resize(new SizeInt32(760, 820));
        CenterChildWindowOverMainWindow(translationAppWindow);
        TryApplyFontChooserTitleBarTheme(translationAppWindow);
        SetWindowAlwaysOnTop(_translationWindow, isAlwaysOnTop: true);

        _translationWindow.Closed += TranslationWindow_Closed;
        _translationWindow.Activate();
        _ = _translationWindow.DispatcherQueue.TryEnqueue(() => TryApplyFontChooserTitleBarTheme(translationAppWindow));

        RefreshTranslationPanel();
    }

    private void TranslationWindow_Closed(object sender, WindowEventArgs args)
    {
        var rootGrid = _translationWindow?.Content as Grid;
        if (rootGrid != null)
        {
            rootGrid.Children.Clear();
        }

        var actionsBar = TranslationTabActions.Parent as Border;
        if (actionsBar != null)
            actionsBar.Child = null;

        TranslationTabContent.Visibility = Visibility.Collapsed;
        TranslationTabActions.Visibility = Visibility.Collapsed;

        TryAttachToPanel(TranslationTabContent, _translationWindowContentParent);
        TryAttachToPanel(TranslationTabActions, _translationWindowActionsParent);

        _translationWindow = null;
        _translationWindowContentParent = null;
        _translationWindowActionsParent = null;
    }



    private Window? _templatesWindow;
    private Panel? _templatesWindowContentParent;
    private Panel? _templatesWindowActionsParent;

    private void ShowTemplatesWindow_Click(object sender, RoutedEventArgs e)
    {
        ShowTemplatesWindow();
    }

    private void ShowTemplatesWindow()
    {
        if (_templatesWindow != null)
        {
            _templatesWindow.Activate();
            return;
        }

        try
        {
            _templatesWindowContentParent = TemplatesTabContent.Parent as Panel;
            _templatesWindowActionsParent = TemplatesTabActions.Parent as Panel;
            DetachElementFromParent(TemplatesTabContent);
            DetachElementFromParent(TemplatesTabActions);

            var rootGrid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30)),
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            TemplatesTabContent.Visibility = Visibility.Visible;
            TemplatesTabActions.Visibility = Visibility.Visible;

            Grid.SetRow(TemplatesTabContent, 0);
            rootGrid.Children.Add(TemplatesTabContent);

            var actionsBar = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.ColorHelper.FromArgb(255, 37, 37, 37)),
                Padding = new Thickness(8)
            };

            DetachElementFromParent(TemplatesTabActions);
            actionsBar.Child = TemplatesTabActions;
            Grid.SetRow(actionsBar, 1);
            rootGrid.Children.Add(actionsBar);

            _templatesWindow = new Window();
            _templatesWindow.Title = L("sidebar.tab.templates");
            _templatesWindow.Content = rootGrid;
            AttachEscapeToCloseWindow(_templatesWindow, rootGrid);
            var templatesAppWindow = _templatesWindow.AppWindow;
            templatesAppWindow.Resize(new SizeInt32(760, 700));
            CenterChildWindowOverMainWindow(templatesAppWindow);
            TryApplyFontChooserTitleBarTheme(templatesAppWindow);
            SetWindowAlwaysOnTop(_templatesWindow, isAlwaysOnTop: true);

            _templatesWindow.Closed += TemplatesWindow_Closed;
            _templatesWindow.Activate();
            _ = _templatesWindow.DispatcherQueue.TryEnqueue(() => TryApplyFontChooserTitleBarTheme(templatesAppWindow));

            _ = RefreshPanelTemplateLibraryAsync();
        }
        catch (Exception ex)
        {
            StartupLogger.Log("ShowTemplatesWindow failed", ex);
            SetStatusMessage(L("templates.error.open_failed"));
            TryAttachToPanel(TemplatesTabContent, _templatesWindowContentParent);
            TryAttachToPanel(TemplatesTabActions, _templatesWindowActionsParent);
            _templatesWindow = null;
            _templatesWindowContentParent = null;
            _templatesWindowActionsParent = null;
        }
    }

    private void TemplatesWindow_Closed(object sender, WindowEventArgs args)
    {
        DetachElementFromParent(TemplatesTabContent);
        DetachElementFromParent(TemplatesTabActions);

        var rootGrid = _templatesWindow?.Content as Grid;
        if (rootGrid != null)
        {
            rootGrid.Children.Clear();
        }

        var actionsBar = TemplatesTabActions.Parent as Border;
        if (actionsBar != null)
            actionsBar.Child = null;

        TemplatesTabContent.Visibility = Visibility.Collapsed;
        TemplatesTabActions.Visibility = Visibility.Collapsed;

        TryAttachToPanel(TemplatesTabContent, _templatesWindowContentParent);
        TryAttachToPanel(TemplatesTabActions, _templatesWindowActionsParent);

        _templatesWindow = null;
        _templatesWindowContentParent = null;
        _templatesWindowActionsParent = null;
    }


    private static void DetachElementFromParent(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Border border when ReferenceEquals(border.Child, element):
                border.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                contentControl.Content = null;
                break;
            case ScrollViewer scrollViewer when ReferenceEquals(scrollViewer.Content, element):
                scrollViewer.Content = null;
                break;
        }
    }

    private static void TryAttachToPanel(FrameworkElement element, Panel? panel)
    {
        if (panel == null || ReferenceEquals(element.Parent, panel))
        {
            return;
        }

        DetachElementFromParent(element);
        try
        {
            panel.Children.Add(element);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("TryAttachToPanel failed", ex);
        }
    }

    private void SetChildWindowsAlwaysOnTop(bool isAlwaysOnTop)
    {
        SetWindowAlwaysOnTop(_exportWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_translationWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_templatesWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_helpWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_balloonStyleEditorWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_preferencesWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_findWindow, isAlwaysOnTop);
        SetWindowAlwaysOnTop(_replaceWindow, isAlwaysOnTop);
    }

    private static void SetWindowAlwaysOnTop(Window? window, bool isAlwaysOnTop)
    {
        if (window == null) return;

        try
        {
            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = isAlwaysOnTop;
            }
        }
        catch (Exception ex)
        {
            StartupLogger.Log("SetWindowAlwaysOnTop failed", ex);
        }
    }
}
