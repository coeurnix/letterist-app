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
    private HttpListener? _httpListener;
    private CancellationTokenSource? _serverCts;
    private CanvasDevice? _canvasDevice;

    private readonly EditorState _editorState;
    private DocumentRenderer? _renderer;
    private string? _currentDocumentFolderPath;
    private string? _currentDocumentPackagePath;
    private bool _currentDocumentFolderIsTemporary;
    private bool _currentDocumentIsAutosave;
    private DispatcherTimer? _autosaveTimer;
    private bool _autosaveInProgress;
    private bool _hasCheckedAutosave;
    private int _pasteOffsetIndex;
    private bool _isUpdatingPageList;
    private bool _isUpdatingLayerList;
    private bool _isDraggingLayerItem;
    private bool _refreshLayerListPending;
    private bool _isUpdatingPanelList;
    private bool _isUpdatingLayerOpacity;
    private readonly HashSet<Guid> _expandedLayerGroups = new();
    private readonly HashSet<Guid> _expandedLayerBalloons = new();
    private readonly HashSet<Guid> _expandedPanels = new();
    private bool _expandedPanelSection = true;
    private readonly Dictionary<Guid, CanvasBitmap> _panelImages = new();
    private readonly Dictionary<Guid, CanvasBitmap> _floatingImageBitmaps = new();
    private readonly Dictionary<Guid, (int Fingerprint, WriteableBitmap? Thumbnail)> _pageThumbnailCache = new();
    private readonly Dictionary<Guid, TextBox> _guidePositionTextBoxes = new();
    private readonly Dictionary<string, CanvasBitmap> _textFillBitmaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _textFillBitmapFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly FindReplaceSession _findReplaceSession = new();
    private readonly List<string> _searchHistory = new();
    private readonly Dictionary<string, ShortcutGesture> _shortcutGestures = new(StringComparer.OrdinalIgnoreCase);
    private AppPreferences _preferences = AppPreferences.CreateDefault();
    private Model.Color _selectionHighlightColor = new(0, 120, 215, 90);
    private FindReplaceWindow? _findWindow;
    private FindReplaceWindow? _replaceWindow;
    private Window? _exportWindow;
    private bool _hasConfiguredPresenter;
    private bool _loggedCanvasResources;
    private bool _loggedFirstDraw;
    private DispatcherTimer? _startupDrawTimer;
    private int _startupDrawAttempts;
    private double _propertiesScrollOffset;
    private int _layerListSelectionAnchorIndex = -1;
    private string? _statusOverrideText;
    private DateTime _statusOverrideExpiresUtc;
    private SnapFeedback? _activeSnapFeedback;
    private bool _isUpdatingGuidePositionInputs;
    private bool _pendingInitialPageFit = true;

    private DispatcherTimer? _cursorBlinkTimer;
    private bool _cursorBlinkState;
    private const int CursorBlinkInterval = 530;

    private bool _isPanning;
    private Point2 _lastPointerPosition;
    private bool _isManipulating;
    private bool _touchTapPending;
    private bool _touchTapMoved;
    private bool _resizeDragBaselinePending;
    private bool _moveDragBaselinePending;
    private bool _panelMoveDragBaselinePending;
    private bool _rotationDragBaselinePending;
    private PanelShape _selectedPanelShape = PanelShape.Rectangle;
    private PanelLayoutToolMode _panelLayoutTool = PanelLayoutToolMode.Select; // Default to select mode
    private PanelDrawTool _selectedPanelDrawTool = PanelDrawTool.Rectangle;
    private Point2 _touchTapStartScreen;
    private const float TouchTapThreshold = 6f;
    private bool _isPointerOverCanvas;
    private float? _textCaretDesiredX;
    private Rect? _panelPreviewBounds;
    private readonly List<Point2> _panelPolygonPoints = new();
    private Point2? _panelPolygonPreviewPoint;
    private bool _isPanelPolygonDrawing;
    private DateTime _lastPanelPolygonClickTime;
    private Point2? _lastPanelPolygonClickScreen;
    private readonly List<Point2> _panelFreeformPoints = new();
    private bool _isPanelFreeformDrawing;
    private bool _isEditingPanelShape;
    private Guid? _editingPanelId;
    private List<PanelPathAnchor> _editingPanelAnchors = new();
    private bool _editingPanelClosed = true;
    private Rect _editingPanelOriginalBounds;
    private string? _editingPanelOriginalPathData;
    private int _editingPanelHandleAnchorIndex = -1;
    private PanelEditHandleType _editingPanelHandleType = PanelEditHandleType.Anchor;
    private Rect? _lastMarqueeSelectionBounds;
    private EditorMode _modeBeforeLayout = EditorMode.Select;
    private readonly Dictionary<Guid, float> _panelAspectRatios = new();
    private readonly HashSet<Guid> _panelAspectLocked = new();
    private float? _panelCustomAspectRatio;
    private Guid? _lastPanelTemplateId;
    private List<PanelTemplateViewModel> _panelTemplateLibraryItems = new();
    private string? _panelTemplateStorageFolderPath;
    private Guid? _lastCreatedBalloonId;
    private Guid? _lastCreatedPanelId;
    private Guid? _dragTextPathBalloonId;
    private TextPath? _dragTextPathOriginal;
    private TextPathHandleType _dragTextPathHandleType = TextPathHandleType.None;
    private BalloonShape? _lastUsedBalloonShape;
    private BalloonStyle? _lastUsedBalloonStyle;
    private TextStyle? _lastUsedTextStyle;
    private TailStyle? _lastUsedTailStyle;
    private float? _lastUsedTailWidth;
    private float? _lastUsedTailCurvature;
    private float? _lastUsedTailCurveCenter;
    private float? _lastUsedTailInset;
    private BalloonStyleClipboardData? _copiedBalloonStyle;
    private Guid? _selectedBalloonTemplateId;
    private Guid? _activeBalloonTemplateId;
    private readonly List<Guid> _recentBalloonTemplateIds = new();
    private bool _isUpdatingBalloonTemplateUi;
    private bool _isBalloonTemplateEyedropperActive;
    private Action? _repeatLastAction;
    private bool _isCanvasFullscreen;
    private bool _isUpdatingToolbarZoomBox;
    private GridLength _leftSidebarWidth;
    private GridLength _rightSidebarWidth;
    private double _leftSidebarMinWidth;
    private double _rightSidebarMinWidth;
    private const float PanelMinSize = 8f;
    private const float PanelVertexHandleSize = 6f;

    private DateTime _lastClickTime;
    private Guid? _lastClickedBalloonId;
    private DateTime _lastTextClickTime;
    private Guid? _lastTextClickBalloonId;
    private DateTime _lastPanelClickTime;
    private Guid? _lastClickedPanelId;
    private string _lastSelectionRefreshToken = string.Empty;
    private Guid? _statusBalloonCountPageId;
    private int _statusBalloonCount = -1;
    private const double DoubleClickTimeMs = 400;
    private const int DefaultAutosaveIntervalSeconds = 30;
    private const int DefaultMaxRecentFiles = 10;
    private const int MaxSearchHistoryEntries = 12;
    private const string RecentFilesSettingsKey = "RecentFiles";
    private const string LegacyPanelTemplateStorageSettingsKey = "PanelTemplateStorageFolder";
    private const string AutosaveInfoFileName = "autosave.json";
    private const string AutosavePreviewFileName = "preview.png";
    private const string ExportPresetsFileName = "export-presets.json";
    private const string SearchHistoryFileName = "search-history.json";
    private const string BalloonClipboardFormat = "Letterist.Balloons";
    private const string PanelClipboardFormat = "Letterist.Panels";
    private const float RulerThickness = 18f;
    private const float GuideHitThreshold = 6f;
    private const float SnapThresholdScreen = 8f;
    private static readonly JsonSerializerOptions RecentFilesJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions ClipboardJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions AutomationCommandJsonOptions = CommandData.JsonOptions;
    private static readonly JsonSerializerOptions ExportPresetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private enum PanelDrawTool
    {
        Rectangle,
        RoundedRect,
        Ellipse,
        Polygon,
        Freeform
    }

    private enum PanelEditHandleType
    {
        Anchor,
        InHandle,
        OutHandle
    }

    private sealed record BalloonStyleClipboardData(
        BalloonShape Shape,
        string? CustomShapePathData,
        BalloonStyle BalloonStyle,
        TextStyle TextStyle,
        bool ConstrainToPanel,
        IReadOnlyList<BalloonTemplateTail> Tails);

    private enum TextPathHandleType
    {
        None,
        Start,
        Control1,
        Control2,
        End
    }

    private sealed class PanelPathAnchor
    {
        public PanelPathAnchor(Point2 position)
        {
            Position = position;
        }

        public Point2 Position { get; set; }
        public Point2? InHandle { get; set; }
        public Point2? OutHandle { get; set; }
    }

    public MainWindow()
    {
        StartupLogger.Log("MainWindow ctor start");
        try
        {
            this.InitializeComponent();
            TryApplyAppIcon();
        }
        catch (Exception ex)
        {
            StartupLogger.Log("InitializeComponent failed", ex);
            throw;
        }

        StartupLogger.Log("MainWindow InitializeComponent complete");
        EnsurePageMenuOrder();
        EnsureMenuHotkeysAreVisible();
        UiLocalizationService.LanguageChanged += UiLocalizationService_LanguageChanged;
        LoadPreferences();
        ApplyWindowPreferences();
        StartupLogger.Log($"MainWindow content type: {Content?.GetType().FullName ?? "null"}");

        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        LinkWidthSlider.Minimum = 1;
        LinkWidthSlider.Maximum = 8;
        LinkWidthSlider.StepFrequency = 0.5;
        LinkWidthSlider.Value = 2;
        _isUpdatingProperties = wasUpdating;

        _editorState = new EditorState();
        _editorState.RedrawRequired += (s, e) =>
        {
            MainCanvas.Invalidate();
        };
        _editorState.DocumentChanged += (s, e) =>
        {
            InvalidateStatusBalloonCountCache();
            _pendingInitialPageFit = true;
            _expandedLayerGroups.Clear();
            _expandedLayerBalloons.Clear();
            _panelAspectRatios.Clear();
            _panelAspectLocked.Clear();
            _panelCustomAspectRatio = null;
            _lastPanelTemplateId = null;
            _floatingImageBitmaps.Clear();
            _pageThumbnailCache.Clear();
            _textFillBitmaps.Clear();
            _textFillBitmapFailures.Clear();
            RefreshLayerList();
            RefreshPanelList();
            _ = RefreshPanelTemplateLibraryAsync();
            _ = RefreshPageListAsync();
            RefreshStylePresets();
            RefreshBalloonTemplateControls();
            RefreshTranslationPanel();
            UpdateToolButtonStates();
            _lastSelectionRefreshToken = BuildSelectionRefreshToken();
        };
        _editorState.SelectionChanged += (s, e) =>
        {
            var shouldRefreshSelectionLists = ShouldRefreshSelectionLists();
            UpdatePropertiesPanel();
            UpdateToolButtonStates();
            if (shouldRefreshSelectionLists)
            {
                RefreshLayerList();
                RefreshPanelList();
            }
            RefreshTranslationSelectionFromEditor();
            UpdateBalloonStyleEditorButtons();

            var selectedBalloon = _editorState.Document?.SelectedBalloon;
            if (selectedBalloon != null)
            {
                _lastUsedBalloonShape = selectedBalloon.Shape;
                _lastUsedBalloonStyle = selectedBalloon.BalloonStyle;
                _lastUsedTextStyle = selectedBalloon.TextStyle;
                CaptureLastUsedTailFromBalloon(selectedBalloon);
            }
        };
        _editorState.LayersChanged += (s, e) =>
        {
            InvalidateStatusBalloonCountCache();
            RefreshLayerList();
            RefreshPanelList();
            UpdatePropertiesPanel();
            RefreshTranslationPanel();
            UpdateToolButtonStates();
        };
        _editorState.PagesChanged += (s, e) =>
        {
            InvalidateStatusBalloonCountCache();
            _ = RefreshPageListAsync();
            RefreshTranslationPanel();
        };
        _editorState.StylesChanged += (s, e) =>
        {
            RefreshStylePresets();
            RefreshBalloonTemplateControls();
            RefreshTranslationPanel();
            UpdateToolButtonStates();
        };
        _editorState.CommandHistoryChanged += (s, e) =>
        {
            UpdateToolButtonStates();
            if (string.Equals(_activeLayersSidebarTab, "Translation", StringComparison.Ordinal))
            {
                RefreshTranslationPanel();
            }
        };
        _editorState.TextEditingChanged += (s, e) =>
        {
            UpdatePropertiesPanel();
            UpdateToolButtonStates();

            if (_editorState.EditingBalloonId.HasValue)
            {
                StartCursorBlinkTimer();
            }
            else
            {
                StopCursorBlinkTimer();
            }
        };

        CreateNewDocumentWithPreferences(L("app.untitled"));
        RefreshLayerList();
        _ = RefreshPageListAsync();
        RefreshStylePresets();
        RefreshBalloonTemplateControls();
        RefreshTranslationPanel();
        UpdateToolButtonStates();
        _lastSelectionRefreshToken = BuildSelectionRefreshToken();

        RefreshRecentDocumentsMenu();
        LoadSearchHistory();
        SyncPanelTemplateStorageFolderFromPreferences();
        InitializeAutosaveTimer();
        this.Activated += MainWindow_Activated;

        if (App.AutomationMode)
        {
            StartAutomationServer();
        }

        this.Closed += MainWindow_Closed;
        MainCanvas.Loaded += MainCanvas_Loaded;
        _ = DispatcherQueue.TryEnqueue(UpdateLeftSidebarTabHeaderMode);
        _ = DispatcherQueue.TryEnqueue(UpdatePropertiesTabHeaderMode);
        StartupLogger.Log("MainWindow ctor complete");
    }

    private bool ShouldRefreshSelectionLists()
    {
        var token = BuildSelectionRefreshToken();
        if (string.Equals(token, _lastSelectionRefreshToken, StringComparison.Ordinal))
        {
            return false;
        }

        _lastSelectionRefreshToken = token;
        return true;
    }

    private string BuildSelectionRefreshToken()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            return "none";
        }

        static string JoinIds(IEnumerable<Guid> ids)
        {
            return string.Join(",", ids.OrderBy(id => id).Select(id => id.ToString("N")));
        }

        var pageId = doc.ActivePageId.ToString("N");
        var layerId = doc.ActiveLayer?.Id.ToString("N") ?? "-";
        var balloonIds = JoinIds(_editorState.SelectedBalloonIds);
        var imageIds = JoinIds(_editorState.SelectedFloatingImageIds);
        var panelIds = JoinIds(_editorState.SelectedPanelIds);

        return $"{pageId}|{layerId}|{balloonIds}|{imageIds}|{panelIds}";
    }

    private void EnsurePageMenuOrder()
    {
        if (MainMenuBar?.Items == null)
        {
            return;
        }

        var pageIndex = MainMenuBar.Items.IndexOf(MenuLayerItem);
        var viewIndex = MainMenuBar.Items.IndexOf(MenuViewItem);
        if (pageIndex < 0 || viewIndex < 0 || pageIndex < viewIndex)
        {
            return;
        }

        MainMenuBar.Items.RemoveAt(pageIndex);
        MainMenuBar.Items.Insert(viewIndex, MenuLayerItem);
    }

    private void EnsureMenuHotkeysAreVisible()
    {
        if (MainMenuBar?.Items == null)
        {
            return;
        }

        foreach (var rootItem in MainMenuBar.Items)
        {
            if (rootItem is MenuBarItem menuBarItem)
            {
                EnsureMenuHotkeysAreVisible(menuBarItem.Items);
            }
        }
    }

    private void TryApplyAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (!File.Exists(iconPath))
            {
                return;
            }

            AppWindow.SetIcon(iconPath);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Failed to apply app icon", ex);
        }
    }

    private static void EnsureMenuHotkeysAreVisible(IList<MenuFlyoutItemBase> items)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case MenuFlyoutSubItem subItem:
                    EnsureMenuHotkeysAreVisible(subItem.Items);
                    break;
                case MenuFlyoutItem menuItem:
                    EnsureMenuHotkeyVisible(menuItem);
                    break;
            }
        }
    }

    private static void EnsureMenuHotkeyVisible(MenuFlyoutItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.KeyboardAcceleratorTextOverride))
        {
            return;
        }

        var accelerator = item.KeyboardAccelerators.FirstOrDefault();
        if (accelerator == null)
        {
            return;
        }

        var label = FormatAcceleratorLabel(accelerator);
        if (!string.IsNullOrWhiteSpace(label))
        {
            item.KeyboardAcceleratorTextOverride = label;
        }
    }

    private static string FormatAcceleratorLabel(KeyboardAccelerator accelerator)
    {
        var keyLabel = FormatAcceleratorKey(accelerator.Key);
        if (string.IsNullOrWhiteSpace(keyLabel))
        {
            return string.Empty;
        }

        var parts = new List<string>(4);
        var modifiers = accelerator.Modifiers;
        if (modifiers.HasFlag(VirtualKeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(VirtualKeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(VirtualKeyModifiers.Menu)) parts.Add("Alt");
        if (modifiers.HasFlag(VirtualKeyModifiers.Windows)) parts.Add("Win");
        parts.Add(keyLabel);

        return string.Join("+", parts);
    }

    private static string FormatAcceleratorKey(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.None => string.Empty,
            VirtualKey.Number0 => "0",
            VirtualKey.Number1 => "1",
            VirtualKey.Number2 => "2",
            VirtualKey.Number3 => "3",
            VirtualKey.Number4 => "4",
            VirtualKey.Number5 => "5",
            VirtualKey.Number6 => "6",
            VirtualKey.Number7 => "7",
            VirtualKey.Number8 => "8",
            VirtualKey.Number9 => "9",
            VirtualKey.Add => "+",
            VirtualKey.Subtract => "-",
            VirtualKey.Multiply => "*",
            VirtualKey.Divide => "/",
            VirtualKey.Space => "Space",
            VirtualKey.Enter => "Enter",
            VirtualKey.Delete => "Delete",
            VirtualKey.Back => "Backspace",
            VirtualKey.Escape => "Esc",
            VirtualKey.PageUp => "PgUp",
            VirtualKey.PageDown => "PgDn",
            VirtualKey.Left => "Left",
            VirtualKey.Up => "Up",
            VirtualKey.Right => "Right",
            VirtualKey.Down => "Down",
            _ => key.ToString()
        };
    }

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;
    private const int WS_VISIBLE = 0x10000000;
    private const int SW_SHOW = 5;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_ALLCHILDREN = 0x0080;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_FRAME = 0x0400;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(IntPtr hWnd);

    private void ConfigureWindowPresenter()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.IsResizable = true;
                overlappedPresenter.IsMaximizable = true;
                overlappedPresenter.IsMinimizable = true;
            }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                var background = Windows.UI.Color.FromArgb(255, 26, 26, 26);
                var foreground = Windows.UI.Color.FromArgb(255, 236, 236, 236);
                var hoverBackground = Windows.UI.Color.FromArgb(255, 45, 45, 45);
                var pressedBackground = Windows.UI.Color.FromArgb(255, 65, 65, 65);

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

            FixWindowExtendedStyles(hwnd);
        }
        catch (Exception ex)
        {
            StartupLogger.Log("ConfigureWindowPresenter failed.", ex);
        }
    }

    private void FixWindowExtendedStyles(IntPtr hwnd)
    {
        try
        {
            int style = GetWindowLong(hwnd, GWL_STYLE);
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            StartupLogger.Log($"Window style: 0x{style:X8}, extended style: 0x{exStyle:X8}");

            bool hasTransparent = (exStyle & WS_EX_TRANSPARENT) != 0;
            bool hasLayered = (exStyle & WS_EX_LAYERED) != 0;
            bool hasNoRedirection = (exStyle & WS_EX_NOREDIRECTIONBITMAP) != 0;
            bool isVisible = (style & WS_VISIBLE) != 0;

            StartupLogger.Log($"Transparent={hasTransparent}, Layered={hasLayered}, NoRedirection={hasNoRedirection}, Visible={isVisible}");

            if (hasTransparent || hasLayered)
            {
                int newExStyle = exStyle & ~WS_EX_TRANSPARENT & ~WS_EX_LAYERED;
                SetWindowLong(hwnd, GWL_EXSTYLE, newExStyle);
                StartupLogger.Log($"Removed problematic extended styles, new: 0x{newExStyle:X8}");
            }

            ShowWindow(hwnd, SW_SHOW);
            RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW | RDW_FRAME);
            UpdateWindow(hwnd);
            StartupLogger.Log("Forced window show and redraw");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("FixWindowExtendedStyles failed.", ex);
        }
    }

    private void MainCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        if (!_loggedCanvasResources)
        {
            _loggedCanvasResources = true;
            StartupLogger.Log("MainCanvas CreateResources");
        }

        StopStartupDrawTimer();

        _canvasDevice = sender.Device;
        _renderer = new DocumentRenderer(_editorState.ViewTransform);
        ApplyPreferencesToRenderer();
        _ = RefreshPageListAsync();
    }

    private void MainCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_loggedFirstDraw)
        {
            _loggedFirstDraw = true;
            StartupLogger.Log("MainCanvas first draw");
        }

        StopStartupDrawTimer();

        var ds = args.DrawingSession;

        ds.TextAntialiasing = Microsoft.Graphics.Canvas.Text.CanvasTextAntialiasing.Grayscale;
        ds.Antialiasing = Microsoft.Graphics.Canvas.CanvasAntialiasing.Antialiased;

        _editorState.ViewTransform.ViewportSize = new Size2((float)sender.ActualWidth, (float)sender.ActualHeight);
        ApplyPendingInitialPageFitIfNeeded(sender);

        ds.Clear(GetWorkspaceBackgroundColor().ToWindowsColor());

        EnsureActiveBackgroundBitmapLoaded();

        _renderer?.Render(
            ds,
            _editorState.Document,
            _editorState.BackgroundImage,
            _editorState.EditingBalloonId,
            _editorState.EditingText,
            _editorState.EditingCursorPosition,
            _editorState.EditingSelectionStart,
            _editorState.EditingSelectionLength,
            _editorState.EditingTextStyleSpans,
            cursorBlinkState: _cursorBlinkState,
            selectedBalloonIds: _editorState.SelectedBalloonIds,
            primarySelectedBalloonId: _editorState.Document?.SelectedBalloonId,
            smartGuides: _editorState.SmartGuides,
            panelImageResolver: GetPanelImage,
            floatingImageResolver: GetFloatingImage,
            textFillImageResolver: GetTextFillImage,
            selectedFloatingImageId: _editorState.SelectedFloatingImageId,
            selectedFloatingImageIds: _editorState.SelectedFloatingImageIds,
            selectedPanelId: _editorState.SelectedPanelId,
            selectedPanelIds: _editorState.SelectedPanelIds,
            panelBoundaryVisibilityMode: _editorState.PanelBoundaryVisibilityMode,
            hoveredPanelId: _editorState.HoveredPanelId,
            panelSafeGuideHints: GetPanelSafeGuideHints(),
            showPanels: _editorState.Mode == EditorMode.PanelLayout,
            showPanelGutters: _editorState.ShowPanelGutters,
            panelPreview: _panelPreviewBounds,
            snapFeedback: _activeSnapFeedback,
            showTypesettingDiagnostics: _editorState.ShowTypesettingDiagnostics);

        RenderPanelCustomPreview(ds);

        if (_renderer != null &&
            _editorState.Mode == EditorMode.CreateBalloon &&
            _isPointerOverCanvas &&
            _editorState.Document != null)
        {
            var worldPos = _editorState.ViewTransform.ScreenToWorld(_lastPointerPosition);
            var docBounds = new Rect(0, 0, _editorState.Document.Size.Width, _editorState.Document.Size.Height);
            if (docBounds.Contains(worldPos))
            {
                ds.Transform = _editorState.ViewTransform.GetTransformMatrix();
                _renderer.RenderBalloonPreview(ds, worldPos, BalloonShape.Oval, BalloonStyle.Default, "Text");
            }
        }

        if (_editorState.IsMarqueeSelecting)
        {
            var start = _editorState.MarqueeStartScreen;
            var current = _editorState.MarqueeCurrentScreen;
            var left = Math.Min(start.X, current.X);
            var top = Math.Min(start.Y, current.Y);
            var width = Math.Abs(start.X - current.X);
            var height = Math.Abs(start.Y - current.Y);

            if (width > 0 && height > 0)
            {
                ds.Transform = Matrix3x2.Identity;
                using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
                var outline = Windows.UI.Color.FromArgb(255, 0, 120, 215);
                ds.DrawRectangle(left, top, width, height, outline, 1.5f, strokeStyle);
            }
        }

        UpdateStatusBar();
    }

    private void ApplyPendingInitialPageFitIfNeeded(CanvasControl sender)
    {
        if (!_pendingInitialPageFit) return;
        if (sender.ActualWidth <= 1 || sender.ActualHeight <= 1) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            _pendingInitialPageFit = false;
            return;
        }

        if (page.Size.Width <= 0 || page.Size.Height <= 0)
        {
            _pendingInitialPageFit = false;
            return;
        }

        _pendingInitialPageFit = false;
        _editorState.ViewTransform.ZoomToFit(new Rect(0, 0, page.Size.Width, page.Size.Height));
    }

    private CanvasBitmap? GetPanelImage(Guid panelId)
    {
        return _panelImages.TryGetValue(panelId, out var bitmap) ? bitmap : null;
    }

    private void EnsureActiveBackgroundBitmapLoaded()
    {
        if (_canvasDevice == null) return;

        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null) return;
        if (string.IsNullOrWhiteSpace(page.BackgroundImagePath)) return;
        if (_editorState.GetBackgroundImageForPage(page.Id) != null) return;

        var resolvedPath = ResolveBackgroundPath(page.BackgroundImagePath);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(resolvedPath);
            using var randomAccess = stream.AsRandomAccessStream();
            var bitmap = CanvasBitmap.LoadAsync(_canvasDevice, randomAccess).AsTask().GetAwaiter().GetResult();
            _editorState.SetBackgroundImageForPage(page.Id, bitmap);
        }
        catch
        {
        }
    }

    private CanvasBitmap? GetFloatingImage(Guid imageId)
    {
        return _floatingImageBitmaps.TryGetValue(imageId, out var bitmap) ? bitmap : null;
    }

    private CanvasBitmap? GetTextFillImage(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (_textFillBitmaps.TryGetValue(path, out var cached)) return cached;
        if (_textFillBitmapFailures.Contains(path)) return null;
        if (_canvasDevice == null || !File.Exists(path))
        {
            _textFillBitmapFailures.Add(path);
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var randomAccessStream = stream.AsRandomAccessStream();
            var bitmap = CanvasBitmap.LoadAsync(_canvasDevice, randomAccessStream).AsTask().GetAwaiter().GetResult();
            _textFillBitmaps[path] = bitmap;
            return bitmap;
        }
        catch
        {
            _textFillBitmapFailures.Add(path);
            return null;
        }
    }

    private void RenderPanelCustomPreview(CanvasDrawingSession ds)
    {
        if (_editorState.Document == null) return;
        if (!_isPanelPolygonDrawing && !_isPanelFreeformDrawing && !_isEditingPanelShape) return;

        var points = new List<Point2>();
        var closeShape = false;

        if (_isPanelPolygonDrawing)
        {
            points.AddRange(_panelPolygonPoints);
            if (_panelPolygonPreviewPoint.HasValue)
            {
                points.Add(_panelPolygonPreviewPoint.Value);
            }
        }
        else if (_isPanelFreeformDrawing)
        {
            points.AddRange(_panelFreeformPoints);
        }
        else if (_isEditingPanelShape)
        {
            closeShape = true;
        }

        if (!_isEditingPanelShape && points.Count < 2) return;

        ds.Transform = _editorState.ViewTransform.GetTransformMatrix();

        var strokeColor = Windows.UI.Color.FromArgb(230, 0, 120, 215);
        var fillColor = Windows.UI.Color.FromArgb(40, 0, 120, 215);
        var strokeWidth = 1f / MathF.Max(0.1f, _editorState.ViewTransform.Zoom);

        if (_isEditingPanelShape)
        {
            if (_editingPanelAnchors.Count >= 2)
            {
                using var pathBuilder = new CanvasPathBuilder(ds);
                var first = _editingPanelAnchors[0];
                pathBuilder.BeginFigure(first.Position.ToVector2());
                for (var i = 0; i < _editingPanelAnchors.Count - 1; i++)
                {
                    var start = _editingPanelAnchors[i];
                    var end = _editingPanelAnchors[i + 1];
                    if (start.OutHandle.HasValue || end.InHandle.HasValue)
                    {
                        var c1 = (start.OutHandle ?? start.Position).ToVector2();
                        var c2 = (end.InHandle ?? end.Position).ToVector2();
                        pathBuilder.AddCubicBezier(c1, c2, end.Position.ToVector2());
                    }
                    else
                    {
                        pathBuilder.AddLine(end.Position.ToVector2());
                    }
                }

                if (_editingPanelClosed && _editingPanelAnchors.Count > 2)
                {
                    var last = _editingPanelAnchors[^1];
                    var end = _editingPanelAnchors[0];
                    if (last.OutHandle.HasValue || end.InHandle.HasValue)
                    {
                        var c1 = (last.OutHandle ?? last.Position).ToVector2();
                        var c2 = (end.InHandle ?? end.Position).ToVector2();
                        pathBuilder.AddCubicBezier(c1, c2, end.Position.ToVector2());
                    }
                    else
                    {
                        pathBuilder.AddLine(end.Position.ToVector2());
                    }
                    pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                }
                else
                {
                    pathBuilder.EndFigure(CanvasFigureLoop.Open);
                }

                using var geometry = CanvasGeometry.CreatePath(pathBuilder);
                ds.FillGeometry(geometry, fillColor);
                ds.DrawGeometry(geometry, strokeColor, strokeWidth);
            }

            var handleRadius = PanelVertexHandleSize / MathF.Max(0.1f, _editorState.ViewTransform.Zoom);
            foreach (var anchor in _editingPanelAnchors)
            {
                if (anchor.InHandle.HasValue)
                {
                    var handle = anchor.InHandle.Value;
                    ds.DrawLine(anchor.Position.ToVector2(), handle.ToVector2(), strokeColor, strokeWidth);
                    ds.FillCircle(handle.ToVector2(), handleRadius * 0.85f, Windows.UI.Color.FromArgb(200, 240, 240, 240));
                    ds.DrawCircle(handle.ToVector2(), handleRadius * 0.85f, strokeColor, strokeWidth);
                }
                if (anchor.OutHandle.HasValue)
                {
                    var handle = anchor.OutHandle.Value;
                    ds.DrawLine(anchor.Position.ToVector2(), handle.ToVector2(), strokeColor, strokeWidth);
                    ds.FillCircle(handle.ToVector2(), handleRadius * 0.85f, Windows.UI.Color.FromArgb(200, 240, 240, 240));
                    ds.DrawCircle(handle.ToVector2(), handleRadius * 0.85f, strokeColor, strokeWidth);
                }

                ds.FillCircle(anchor.Position.ToVector2(), handleRadius, Windows.UI.Color.FromArgb(230, 255, 255, 255));
                ds.DrawCircle(anchor.Position.ToVector2(), handleRadius, strokeColor, strokeWidth);
            }
        }
        else
        {
            using var pathBuilder = new CanvasPathBuilder(ds);
            pathBuilder.BeginFigure(points[0].ToVector2());
            for (var i = 1; i < points.Count; i++)
            {
                pathBuilder.AddLine(points[i].ToVector2());
            }

            if (closeShape && points.Count >= 3)
            {
                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            }
            else
            {
                pathBuilder.EndFigure(CanvasFigureLoop.Open);
            }

            using var geometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(geometry, fillColor);
            ds.DrawGeometry(geometry, strokeColor, strokeWidth);
        }

        ds.Transform = Matrix3x2.Identity;
    }

    private IReadOnlyList<PanelSafeGuideHint>? GetPanelSafeGuideHints()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return null;

        var hints = new Dictionary<Guid, PanelSafeGuideHintKind>();

        void AddHint(Guid panelId, PanelSafeGuideHintKind kind)
        {
            if (!hints.TryGetValue(panelId, out var existing) || GetHintPriority(kind) > GetHintPriority(existing))
            {
                hints[panelId] = kind;
            }
        }

        if (_editorState.ShowPanelSafeGuides)
        {
            foreach (var panel in page.Panels)
            {
                if (panel.IsVisible && panel.SafeMargin > 0f)
                {
                    AddHint(panel.Id, PanelSafeGuideHintKind.Normal);
                }
            }
        }

        if (_editorState.IsDragging && _editorState.CurrentDragType == DragType.MoveBalloon)
        {
            var balloonIds = new List<Guid>();
            if (_editorState.SelectedBalloonIds.Count > 0)
            {
                balloonIds.AddRange(_editorState.SelectedBalloonIds);
            }
            else
            {
                var dragBalloonId = _editorState.DragBalloonId;
                if (dragBalloonId.HasValue)
                {
                    balloonIds.Add(dragBalloonId.Value);
                }
            }

            foreach (var balloonId in balloonIds)
            {
                var balloon = page.FindBalloon(balloonId);
                if (balloon == null) continue;

                var panelId = balloon.PanelId;
                if (!panelId.HasValue) continue;

                var panel = page.FindPanel(panelId.Value);
                if (panel == null || panel.SafeMargin <= 0f) continue;

                var safeBounds = panel.Bounds.Inflate(-panel.SafeMargin, -panel.SafeMargin);
                if (safeBounds.Width <= 1f || safeBounds.Height <= 1f) continue;

                var balloonBounds = MathF.Abs(balloon.Rotation) > 0.01f
                    ? BalloonGeometry.GetRotatedBounds(balloon)
                    : balloon.Bounds;
                var hintKind = safeBounds.Contains(balloonBounds)
                    ? PanelSafeGuideHintKind.Inside
                    : PanelSafeGuideHintKind.Outside;

                AddHint(panel.Id, hintKind);
            }
        }

        return hints.Count > 0
            ? hints.Select(entry => new PanelSafeGuideHint(entry.Key, entry.Value)).ToList()
            : null;
    }

    private static int GetHintPriority(PanelSafeGuideHintKind kind)
    {
        return kind switch
        {
            PanelSafeGuideHintKind.Outside => 3,
            PanelSafeGuideHintKind.Inside => 2,
            _ => 1
        };
    }

    private void CaptureLastUsedTailFromBalloon(Balloon? balloon)
    {
        var tail = balloon?.Tail;
        if (tail == null)
        {
            return;
        }

        _lastUsedTailStyle = tail.Style;
        _lastUsedTailWidth = Math.Clamp(tail.BaseWidth, 1f, 200f);
        _lastUsedTailCurvature = Math.Clamp(tail.Curvature, -2f, 2f);
        _lastUsedTailCurveCenter = Math.Clamp(tail.CurveCenter, 0f, 1f);
        _lastUsedTailInset = Math.Clamp(tail.Inset, 0f, 64f);
    }

    private (TailStyle style, float width, float curvature, float curveCenter, float inset) ResolvePreferredTailSettings()
    {
        var style = _lastUsedTailStyle ?? GetDefaultTailStyle();
        var width = _lastUsedTailWidth ?? GetDefaultTailWidth();
        var curvature = _lastUsedTailCurvature ?? GetDefaultTailCurvature();
        var curveCenter = _lastUsedTailCurveCenter ?? 0.5f;
        var inset = _lastUsedTailInset ?? 0f;

        return (
            style,
            Math.Clamp(width, 1f, 200f),
            Math.Clamp(curvature, -2f, 2f),
            Math.Clamp(curveCenter, 0f, 1f),
            Math.Clamp(inset, 0f, 64f));
    }

    private void ExecuteCreateTailWithSettings(
        Guid balloonId,
        Point2 targetPoint,
        TailStyle style,
        float width,
        float curvature,
        float curveCenter,
        float inset,
        string transactionDescription = "Add tail")
    {
        var clampedWidth = Math.Clamp(width, 1f, 200f);
        var clampedCurvature = Math.Clamp(curvature, -2f, 2f);
        var clampedCurveCenter = Math.Clamp(curveCenter, 0f, 1f);
        var clampedInset = Math.Clamp(inset, 0f, 64f);
        var tailId = Guid.NewGuid();

        var commands = new List<ICommand>
        {
            new CreateTailCommand(balloonId, targetPoint, style, clampedWidth, tailId)
        };

        if (MathF.Abs(clampedCurvature - 0.3f) > 0.001f)
        {
            commands.Add(new SetTailCurvatureCommand(balloonId, clampedCurvature, tailId));
        }

        if (MathF.Abs(clampedCurveCenter - 0.5f) > 0.001f)
        {
            commands.Add(new SetTailCurveCenterCommand(balloonId, clampedCurveCenter, tailId));
        }

        if (MathF.Abs(clampedInset) > 0.001f)
        {
            commands.Add(new SetTailInsetCommand(balloonId, clampedInset, tailId));
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction(transactionDescription, commands);
        }

        _lastUsedTailStyle = style;
        _lastUsedTailWidth = clampedWidth;
        _lastUsedTailCurvature = clampedCurvature;
        _lastUsedTailCurveCenter = clampedCurveCenter;
        _lastUsedTailInset = clampedInset;
    }

    private void UpdateStatusBar()
    {
        if (_editorState == null)
        {
            return;
        }

        var zoom = _editorState.ViewTransform.ZoomPercent;
        var doc = _editorState.Document;
        UpdateWindowTitle();

        if (doc != null)
        {
            var balloonCount = GetStatusBalloonCount(doc);
            var layerCount = doc.Layers.Count;
            var pageIndex = doc.IndexOfPage(doc.ActivePageId);
            var pageText = pageIndex >= 0
                ? LF("status.page_with_index", pageIndex + 1, doc.Pages.Count)
                : L("status.page");
            var layoutSuffix = _editorState.Mode == EditorMode.PanelLayout ? L("status.panel_layout_suffix") : string.Empty;
            if (TryGetStatusOverride(out var overrideText))
            {
                StatusText.Text = overrideText;
            }
            else
            {
                StatusText.Text = LF(
                    "status.document_summary",
                    pageText,
                    doc.Size.Width,
                    doc.Size.Height,
                    layerCount,
                    balloonCount,
                    layoutSuffix);
            }

            if (!string.Equals(doc.ActiveLanguage, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
            {
                ActiveTranslationIndicatorText.Text = LF("status.translation_language_indicator", doc.ActiveLanguage);
                ActiveTranslationIndicatorText.Visibility = Visibility.Visible;
            }
            else
            {
                ActiveTranslationIndicatorText.Text = string.Empty;
                ActiveTranslationIndicatorText.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            StatusText.Text = TryGetStatusOverride(out var overrideText)
                ? overrideText
                : L("app.open_or_create_document");
            ActiveTranslationIndicatorText.Text = string.Empty;
            ActiveTranslationIndicatorText.Visibility = Visibility.Collapsed;
        }

        if (ToolbarZoomBox != null && (ToolbarZoomBox.FocusState == FocusState.Unfocused || _isUpdatingToolbarZoomBox))
        {
            _isUpdatingToolbarZoomBox = true;
            ToolbarZoomBox.Text = $"{zoom:F0}%";
            _isUpdatingToolbarZoomBox = false;
        }
    }

    private int GetStatusBalloonCount(Document doc)
    {
        if (_statusBalloonCountPageId == doc.ActivePageId && _statusBalloonCount >= 0)
        {
            return _statusBalloonCount;
        }

        _statusBalloonCountPageId = doc.ActivePageId;
        _statusBalloonCount = doc.AllBalloons.Count();
        return _statusBalloonCount;
    }

    private void InvalidateStatusBalloonCountCache()
    {
        _statusBalloonCountPageId = null;
        _statusBalloonCount = -1;
    }

    private void ToolbarZoomBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        _ = HideTextBoxDeleteButton(textBox);
    }

    private static bool HideTextBoxDeleteButton(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button &&
                string.Equals(button.Name, "DeleteButton", StringComparison.OrdinalIgnoreCase))
            {
                button.Visibility = Visibility.Collapsed;
                button.IsEnabled = false;
                return true;
            }

            if (HideTextBoxDeleteButton(child))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateWindowTitle()
    {
        var appTitle = L("app.title");
        var doc = _editorState?.Document;
        if (doc == null)
        {
            Title = LF("app.window_title", appTitle, L("app.untitled"), string.Empty);
            return;
        }

        var documentName = string.IsNullOrWhiteSpace(doc.Name) ? L("app.untitled") : doc.Name;
        var dirtyMark = doc.IsDirty ? "*" : string.Empty;
        Title = LF("app.window_title", appTitle, documentName, dirtyMark);
    }

    private static void AttachEscapeToCloseWindow(Window window, UIElement keyScope)
    {
        keyScope.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Escape)
            {
                return;
            }

            e.Handled = true;
            window.Close();
        };
    }

    private static void AttachEscapeToCloseDialog(ContentDialog dialog, UIElement keyScope)
    {
        keyScope.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Escape)
            {
                return;
            }

            e.Handled = true;
            try
            {
                dialog.Hide();
            }
            catch
            {
            }
        };
    }

    private void SetStatusMessage(string message, TimeSpan? duration = null)
    {
        _statusOverrideText = message;
        _statusOverrideExpiresUtc = DateTime.UtcNow + (duration ?? TimeSpan.FromSeconds(3));
        StatusText.Text = message;
    }

    private bool TryGetStatusOverride(out string text)
    {
        if (!string.IsNullOrWhiteSpace(_statusOverrideText) && DateTime.UtcNow < _statusOverrideExpiresUtc)
        {
            text = _statusOverrideText!;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private void ApplyTextSelectionHighlightStyle()
    {
        if (_renderer == null) return;
        _renderer.TextSelectionHighlightColor = _selectionHighlightColor.ToWindowsColor();
    }

    private void UpdateSearchHighlightStyle(Model.Color color)
    {
        _selectionHighlightColor = color;
        ApplyTextSelectionHighlightStyle();
        MainCanvas.Invalidate();
    }

    private void StartCursorBlinkTimer()
    {
        _cursorBlinkTimer?.Stop();

        _cursorBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CursorBlinkInterval),
        };
        _cursorBlinkTimer.Tick += CursorBlinkTimer_Tick;
        _cursorBlinkTimer.Start();
        _cursorBlinkState = true;
    }

    private void StopCursorBlinkTimer()
    {
        _cursorBlinkTimer?.Stop();
        _cursorBlinkTimer = null;
        _cursorBlinkState = false;
    }

    private void CursorBlinkTimer_Tick(object? sender, object e)
    {
        _cursorBlinkState = !_cursorBlinkState;
        MainCanvas.Invalidate();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        UiLocalizationService.LanguageChanged -= UiLocalizationService_LanguageChanged;
        _autosaveTimer?.Stop();

        if (_editorState.Document is { IsDirty: false } doc)
        {
            ClearAutosaveForDocument(doc.Id);
        }

        CloseSubordinateWindows();

        ReleaseTemporaryDocumentFolder();
        _serverCts?.Cancel();
        _httpListener?.Stop();
        _httpListener?.Close();
    }

    private void CloseSubordinateWindows()
    {
        try { _exportWindow?.Close(); } catch { }
        try { _translationWindow?.Close(); } catch { }
        try { _templatesWindow?.Close(); } catch { }
        try { _helpWindow?.Close(); } catch { }
        try { _balloonStyleEditorWindow?.Close(); } catch { }
        try { _findWindow?.Close(); } catch { }
        try { _replaceWindow?.Close(); } catch { }
        try { _preferencesWindow?.Close(); } catch { }
    }




}
