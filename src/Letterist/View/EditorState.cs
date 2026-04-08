using Letterist.Commands;
using Letterist.Model;
using Letterist.Rendering;
using Microsoft.Graphics.Canvas;

namespace Letterist.View;

public sealed class EditorState
{
    private Document? _document;
    private readonly Dictionary<Guid, CanvasBitmap?> _backgroundImages = new();
    private readonly HashSet<Guid> _selectedBalloonIds = new();
    private readonly HashSet<Guid> _selectedPanelIds = new();
    private readonly HashSet<Guid> _selectedFloatingImageIds = new();
    private Guid? _selectedFloatingImageId;
    private readonly List<SmartGuideLine> _smartGuides = new();
    private readonly Stack<SelectionSnapshot> _selectionUndoHistory = new();
    private readonly Stack<SelectionSnapshot> _selectionRedoHistory = new();
    private bool _isApplyingSelectionHistory;
    private bool _deferRedrawRequired;
    private bool _deferLayersChanged;
    private bool _deferPagesChanged;
    private bool _deferSelectionChanged;
    private bool _deferStylesChanged;
    private bool _deferRefreshStyleCache;
    private bool _deferUpdateActivePageView;
    private bool _deferPruneSelection;

    private sealed record SelectionSnapshot(
        HashSet<Guid> BalloonIds,
        Guid? PrimaryBalloonId,
        HashSet<Guid> PanelIds,
        Guid? PrimaryPanelId,
        HashSet<Guid> FloatingImageIds,
        Guid? PrimaryFloatingImageId);

    public event EventHandler? DocumentChanged;

    public event EventHandler? BackgroundImageChanged;

    public event EventHandler? SelectionChanged;

    public event EventHandler? LayersChanged;

    public event EventHandler? PagesChanged;

    public event EventHandler? StylesChanged;

    public event EventHandler? RedrawRequired;
    public event EventHandler? CommandHistoryChanged;

    public EditorState()
    {
        ViewTransform = new ViewTransform();
        ViewTransform.TransformChanged += (s, e) => RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public ViewTransform ViewTransform { get; }

    public CommandDispatcher? CommandDispatcher { get; private set; }

    public Document? Document
    {
        get => _document;
        private set
        {
            if (_document != value)
            {
                _document = value;
                DocumentChanged?.Invoke(this, EventArgs.Empty);
                RedrawRequired?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public IReadOnlyCollection<Guid> SelectedBalloonIds => _selectedBalloonIds;
    public IReadOnlyCollection<Guid> SelectedPanelIds => _selectedPanelIds;
    public IReadOnlyCollection<Guid> SelectedFloatingImageIds => _selectedFloatingImageIds;
    public Guid? SelectedFloatingImageId => _selectedFloatingImageId;

    public IReadOnlyList<SmartGuideLine> SmartGuides => _smartGuides;

    public bool HasMultipleSelection => _selectedBalloonIds.Count > 1;

    public CanvasBitmap? BackgroundImage
    {
        get
        {
            var pageId = Document?.ActivePageId;
            if (!pageId.HasValue) return null;
            return _backgroundImages.TryGetValue(pageId.Value, out var image) ? image : null;
        }
        set
        {
            var pageId = Document?.ActivePageId;
            if (!pageId.HasValue) return;
            SetBackgroundImageForPage(pageId.Value, value);
        }
    }

    public CanvasBitmap? GetBackgroundImageForPage(Guid pageId)
    {
        return _backgroundImages.TryGetValue(pageId, out var image) ? image : null;
    }

    public void SetBackgroundImageForPage(Guid pageId, CanvasBitmap? image)
    {
        var hasExisting = _backgroundImages.TryGetValue(pageId, out var existing);
        if (hasExisting && existing == image) return;

        _backgroundImages[pageId] = image;

        if (Document?.ActivePageId == pageId)
        {
            BackgroundImageChanged?.Invoke(this, EventArgs.Empty);
            RedrawRequired?.Invoke(this, EventArgs.Empty);
        }
    }

    public EditorMode Mode { get; set; } = EditorMode.Select;

    public bool SnapToGuides { get; set; } = true;
    public PanelBoundaryVisibilityMode PanelBoundaryVisibilityMode { get; set; } = PanelBoundaryVisibilityMode.Always;
    public bool ShowPanelSafeGuides { get; set; }
    public bool ShowPanelGutters { get; set; }

    public Guid? SelectedPanelId { get; private set; }

    public bool ShowTypesettingDiagnostics { get; set; } = false;

    public bool IsDragging { get; set; }

    public Point2 DragStartScreen { get; set; }

    public Point2 DragStartWorld { get; set; }

    public Point2 DragCurrentScreen { get; set; }

    public DragType CurrentDragType { get; set; }

    public Guid? DragBalloonId { get; set; }

    public Guid? DragTailId { get; set; }

    public Point2 DragBalloonOriginalPosition { get; set; }

    public Size2 DragBalloonOriginalSize { get; set; }

    public float? DragBalloonOriginalMaxTextWidth { get; set; }

    public float DragBalloonOriginalRotation { get; set; }

    public float DragRotationStartAngle { get; set; }

    public Point2? DragTailOriginalAttachmentDirection { get; set; }

    public Dictionary<Guid, Point2> DragBalloonOriginalPositions { get; } = new();

    public Dictionary<(Guid balloonId, Guid tailId), Point2> DragTailOriginalTargets { get; } = new();

    internal Dictionary<Guid, BalloonResizeSnapshot> DragBalloonOriginalStates { get; } = new();

    public Rect DragSelectionBounds { get; set; }

    public Guid? DragGuideId { get; set; }

    public float DragGuideOriginalPosition { get; set; }

    public GuideOrientation DragGuideOrientation { get; set; }

    public Guid? HoveredPanelId { get; private set; }

    public Guid? DragPanelId { get; set; }

    public Guid? DragFloatingImageId { get; set; }

    public Rect DragFloatingImageOriginalBounds { get; set; }
    public Dictionary<Guid, Rect> DragFloatingImageOriginalBoundsMap { get; } = new();

    public Rect DragPanelOriginalBounds { get; set; }
    public Dictionary<Guid, Rect> DragPanelOriginalBoundsMap { get; } = new();

    public bool IsMarqueeSelecting { get; set; }

    public Point2 MarqueeStartScreen { get; set; }

    public Point2 MarqueeCurrentScreen { get; set; }

    public bool MarqueeIsAdditive { get; set; }

    public ResizeHandle CurrentResizeHandle { get; set; }

    public Guid? EditingBalloonId { get; private set; }

    public string EditingText { get; set; } = "";

    public int EditingCursorPosition { get; set; }

    public int EditingSelectionStart { get; private set; }

    public int EditingSelectionLength { get; private set; }

    private readonly List<TextStyleSpan> _editingTextStyleSpans = new();
    private TextStyle? _editingInsertionStyleOverride;

    public IReadOnlyList<TextStyleSpan> EditingTextStyleSpans => _editingTextStyleSpans;

    public bool EnableSmartPunctuation { get; set; } = true;

    public int EditingSelectionAnchor { get; private set; }

    public bool HasSelection => EditingSelectionLength > 0;

    public event EventHandler? TextEditingChanged;

    private readonly Stack<TextEditSnapshot> _textEditUndo = new();
    private readonly Stack<TextEditSnapshot> _textEditRedo = new();

    public void EnterTextEditMode(Guid balloonId)
    {
        var balloon = Document?.FindBalloon(balloonId);
        if (balloon == null) return;

        EditingBalloonId = balloonId;
        EditingText = balloon.Text;
        _editingTextStyleSpans.Clear();
        if (balloon.TextStyleSpans.Count > 0)
        {
            _editingTextStyleSpans.AddRange(balloon.TextStyleSpans.Select(span => span.Clone()));
        }
        EditingCursorPosition = balloon.Text.Length;
        EditingSelectionStart = 0;
        EditingSelectionLength = 0;
        EditingSelectionAnchor = EditingCursorPosition;
        ClearTextEditHistory();
        _editingInsertionStyleOverride = null;
        Mode = EditorMode.EditText;
        SelectBalloon(balloonId);
        TextEditingChanged?.Invoke(this, EventArgs.Empty);
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void ExitTextEditMode(bool saveChanges = true)
    {
        if (EditingBalloonId == null || Document == null) return;

        if (saveChanges)
        {
            var balloon = Document.FindBalloon(EditingBalloonId.Value);
            if (balloon != null)
            {
                var textChanged = balloon.Text != EditingText;
                var spansChanged = !AreTextStyleSpansEquivalent(balloon.TextStyleSpans, _editingTextStyleSpans);
                if (textChanged || spansChanged)
                {
                    Execute(new SetBalloonRichTextCommand(EditingBalloonId.Value, EditingText, _editingTextStyleSpans));
                }
            }
        }

        EditingBalloonId = null;
        EditingText = "";
        _editingTextStyleSpans.Clear();
        EditingCursorPosition = 0;
        EditingSelectionStart = 0;
        EditingSelectionLength = 0;
        EditingSelectionAnchor = 0;
        ClearTextEditHistory();
        _editingInsertionStyleOverride = null;
        Mode = EditorMode.Select;
        TextEditingChanged?.Invoke(this, EventArgs.Empty);
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void InsertText(string text)
    {
        if (EditingBalloonId == null) return;

        PushTextEditSnapshot();

        var insertIndex = EditingCursorPosition;
        TextStyle? inheritStyle = null;
        if (HasSelection)
        {
            insertIndex = EditingSelectionStart;
            DeleteSelectionInternal();
        }
        inheritStyle = GetStyleForInsertion(insertIndex);

        if (EnableSmartPunctuation)
        {
            text = ApplySmartPunctuation(text, insertIndex);
        }

        EditingText = EditingText.Insert(insertIndex, text);
        InsertRangeIntoSpans(insertIndex, text.Length, inheritStyle);
        EditingCursorPosition = insertIndex + text.Length;
        ClearSelectionInternal();
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    private string ApplySmartPunctuation(string text, int insertIndex)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            char? prevChar = null;

            if (i > 0)
            {
                prevChar = text[i - 1];
            }
            else if (insertIndex > 0 && EditingText.Length > 0)
            {
                prevChar = EditingText[insertIndex - 1];
            }

            char replacement = c;

            switch (c)
            {
                case '"':
                    replacement = IsOpeningPosition(prevChar) ? '\u201C' : '\u201D';
                    break;

                case '\'':
                    replacement = IsOpeningPosition(prevChar) ? '\u2018' : '\u2019';
                    break;

                case '-':
                    if (result.Length > 0 && result[result.Length - 1] == '-')
                    {
                        result.Length--; // Remove the previous hyphen
                        replacement = '\u2014'; // Em dash
                    }
                    break;

                case '.':
                    if (result.Length >= 2 &&
                        result[result.Length - 1] == '.' &&
                        result[result.Length - 2] == '.')
                    {
                        result.Length -= 2; // Remove the previous two dots
                        replacement = '\u2026'; // Ellipsis
                    }
                    break;
            }

            result.Append(replacement);
        }

        return result.ToString();
    }

    private static bool IsOpeningPosition(char? prevChar)
    {
        if (prevChar == null) return true; // Start of text

        char c = prevChar.Value;

        return char.IsWhiteSpace(c) ||
               c == '(' || c == '[' || c == '{' ||
               c == '\u201C' || c == '\u2018' || // After opening quotes
               c == '\n' || c == '\r';
    }

    public void DeleteCharacterBefore()
    {
        if (EditingBalloonId == null) return;

        if (HasSelection)
        {
            PushTextEditSnapshot();
            DeleteSelectionInternal();
            RedrawRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (EditingCursorPosition <= 0) return;

        PushTextEditSnapshot();
        EditingText = EditingText.Remove(EditingCursorPosition - 1, 1);
        RemoveRangeFromSpans(EditingCursorPosition - 1, 1);
        EditingCursorPosition--;
        ClearSelectionInternal();
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteCharacterAfter()
    {
        if (EditingBalloonId == null) return;

        if (HasSelection)
        {
            PushTextEditSnapshot();
            DeleteSelectionInternal();
            RedrawRequired?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (EditingCursorPosition >= EditingText.Length) return;

        PushTextEditSnapshot();
        EditingText = EditingText.Remove(EditingCursorPosition, 1);
        RemoveRangeFromSpans(EditingCursorPosition, 1);
        ClearSelectionInternal();
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void MoveCursorLeft(bool extendSelection = false)
    {
        if (!extendSelection && HasSelection)
        {
            SetCursorPosition(EditingSelectionStart, false);
            return;
        }

        SetCursorPosition(EditingCursorPosition - 1, extendSelection);
    }

    public void MoveCursorRight(bool extendSelection = false)
    {
        if (!extendSelection && HasSelection)
        {
            SetCursorPosition(EditingSelectionStart + EditingSelectionLength, false);
            return;
        }

        SetCursorPosition(EditingCursorPosition + 1, extendSelection);
    }

    public void MoveCursorHome(bool extendSelection = false)
    {
        SetCursorPosition(0, extendSelection);
    }

    public void MoveCursorEnd(bool extendSelection = false)
    {
        SetCursorPosition(EditingText.Length, extendSelection);
    }

    public void SetCursorPosition(int position, bool extendSelection = false)
    {
        position = Math.Clamp(position, 0, EditingText.Length);

        if (extendSelection)
        {
            if (!HasSelection)
            {
                EditingSelectionAnchor = EditingCursorPosition;
            }

            var start = Math.Min(EditingSelectionAnchor, position);
            var length = Math.Abs(position - EditingSelectionAnchor);
            EditingSelectionStart = start;
            EditingSelectionLength = length;
        }
        else
        {
            EditingSelectionStart = 0;
            EditingSelectionLength = 0;
            EditingSelectionAnchor = position;
        }

        EditingCursorPosition = position;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void SetTextSelection(int start, int length)
    {
        if (EditingBalloonId == null) return;

        start = Math.Clamp(start, 0, EditingText.Length);
        length = Math.Clamp(length, 0, EditingText.Length - start);

        EditingSelectionStart = start;
        EditingSelectionLength = length;
        EditingSelectionAnchor = start;
        EditingCursorPosition = start + length;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceEditingText(string newText, int selectionStart, int selectionLength)
    {
        if (EditingBalloonId == null) return;

        PushTextEditSnapshot();
        var oldLength = EditingText.Length;
        selectionStart = Math.Clamp(selectionStart, 0, EditingText.Length);
        selectionLength = Math.Clamp(selectionLength, 0, EditingText.Length - selectionStart);

        RemoveRangeFromSpans(selectionStart, selectionLength);
        var insertLength = newText.Length - (oldLength - selectionLength);
        var inheritStyle = GetStyleForInsertion(selectionStart);
        if (insertLength > 0)
        {
            InsertRangeIntoSpans(selectionStart, insertLength, inheritStyle);
        }

        EditingText = newText;
        selectionStart = Math.Clamp(selectionStart, 0, EditingText.Length);
        selectionLength = Math.Clamp(selectionLength, 0, EditingText.Length - selectionStart);

        EditingSelectionStart = selectionStart;
        EditingSelectionLength = selectionLength;
        EditingSelectionAnchor = selectionStart;
        EditingCursorPosition = selectionStart + selectionLength;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        EditingSelectionStart = 0;
        EditingSelectionLength = EditingText.Length;
        EditingSelectionAnchor = 0;
        EditingCursorPosition = EditingText.Length;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public string GetSelectedText()
    {
        if (!HasSelection) return "";
        return EditingText.Substring(EditingSelectionStart, EditingSelectionLength);
    }

    public TextStyle GetSelectionTextStyle()
    {
        if (EditingBalloonId == null || Document == null) return TextStyle.Default;

        if (EditingSelectionLength > 0)
        {
            return GetStyleAtIndex(EditingSelectionStart);
        }

        if (_editingInsertionStyleOverride != null)
        {
            return _editingInsertionStyleOverride;
        }

        if (EditingCursorPosition > 0)
        {
            return GetStyleAtIndex(EditingCursorPosition - 1);
        }

        return GetEditingBaseTextStyle();
    }

    public bool SetInsertionTextStyle(TextStyle style)
    {
        if (EditingBalloonId == null || HasSelection) return false;

        var current = GetSelectionTextStyle();
        if (TextStyleUtilities.AreInlineEquivalent(current, style))
        {
            return false;
        }

        PushTextEditSnapshot();
        _editingInsertionStyleOverride = style;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool DeleteSelection()
    {
        if (!HasSelection) return false;
        PushTextEditSnapshot();
        DeleteSelectionInternal();
        RedrawRequired?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool UndoTextEdit()
    {
        if (_textEditUndo.Count == 0) return false;

        _textEditRedo.Push(CaptureSnapshot());
        RestoreSnapshot(_textEditUndo.Pop());
        return true;
    }

    public bool RedoTextEdit()
    {
        if (_textEditRedo.Count == 0) return false;

        _textEditUndo.Push(CaptureSnapshot());
        RestoreSnapshot(_textEditRedo.Pop());
        return true;
    }

    public void NewDocument(string name = "Untitled", Size2? size = null)
    {
        Document = Model.Document.Create(name, size);
        ReplaceCommandDispatcher(new CommandDispatcher(Document));
        _backgroundImages.Clear();
        _selectedBalloonIds.Clear();
        _selectedPanelIds.Clear();
        _selectedFloatingImageIds.Clear();
        SelectedPanelId = null;
        _selectedFloatingImageId = null;
        if (Document.SelectedBalloonId.HasValue)
        {
            _selectedBalloonIds.Add(Document.SelectedBalloonId.Value);
        }
        BackgroundImage = null;
        ClearSelectionHistory();

        ViewTransform.ZoomToFit(new Rect(0, 0, Document.Size.Width, Document.Size.Height));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommandExecuted(object? sender, CommandEventArgs e)
    {
        QueueCommandNotificationEffects(e.Command.CommandType);

        if (CommandDispatcher?.History.IsInTransaction == true)
        {
            return;
        }

        FlushDeferredCommandNotificationEffects();
    }

    private void QueueCommandNotificationEffects(string cmdType)
    {
        _deferRedrawRequired = true;

        var isCopyBalloonsToPage = cmdType == "CopyBalloonsToPage";
        var isPageTemplateCommand = cmdType.Contains("PageTemplate");
        var isPanelCommand = cmdType.Contains("PanelZone") || cmdType.Contains("PanelLayout");
        var isFloatingImageCommand = cmdType.Contains("FloatingImage");
        if (cmdType.Contains("Layer") ||
            cmdType == "CreateBalloon" ||
            cmdType == "CreateBalloonFromTemplate" ||
            cmdType == "DeleteBalloon" ||
            cmdType == "ReorderBalloon" ||
            cmdType == "SetBalloonVisibility" ||
            cmdType == "SetBalloonLocked" ||
            isFloatingImageCommand ||
            isCopyBalloonsToPage ||
            isPanelCommand)
        {
            _deferLayersChanged = true;
        }

        if (cmdType.Contains("Link") || cmdType.Contains("OffPanelIndicator"))
        {
            _deferSelectionChanged = true;
        }

        var isBalloonPropertyCommand = cmdType is "MoveBalloon" or "ResizeBalloon" or "RotateBalloon" or
            "SetBalloonStyle" or "SetBalloonStyleReference" or "SetTextStyle" or "SetTextStyleReference" or "SetBalloonRichText" or "SetBalloonText" or
            "SetBalloonVisibility" or "SetBalloonLocked" or
            "MoveTail" or "SetTailStyle" or "SetTailWidth" or "SetTailCurvature" or "SetTailCurveCenter" or "SetTailInset" or "SetTailAttachment" or
            "SetBalloonTextPath" or "ApplyBalloonTemplate";
        if (isBalloonPropertyCommand || isFloatingImageCommand)
        {
            _deferSelectionChanged = true;
        }

        if (cmdType.Contains("Page") && !isCopyBalloonsToPage && !isPageTemplateCommand)
        {
            _deferPagesChanged = true;
            _deferLayersChanged = true;
            _deferSelectionChanged = true;
            _deferUpdateActivePageView = true;
        }

        if (isCopyBalloonsToPage)
        {
            _deferPagesChanged = true;
        }

        if (cmdType.Contains("NamedBalloonStyle") || cmdType.Contains("NamedTextStyle") || cmdType.Contains("BalloonTemplate"))
        {
            _deferRefreshStyleCache = true;
            _deferStylesChanged = true;
            _deferRedrawRequired = true;
        }

        _deferPruneSelection = true;
    }

    private void FlushDeferredCommandNotificationEffects()
    {
        if (!HasDeferredCommandNotificationEffects())
        {
            return;
        }

        if (_deferRefreshStyleCache)
        {
            Document?.RefreshStyleCache();
        }

        if (_deferRedrawRequired)
        {
            RedrawRequired?.Invoke(this, EventArgs.Empty);
        }

        if (_deferLayersChanged)
        {
            LayersChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_deferPagesChanged)
        {
            PagesChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_deferSelectionChanged)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_deferStylesChanged)
        {
            StylesChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_deferUpdateActivePageView)
        {
            UpdateActivePageView();
        }

        if (_deferPruneSelection)
        {
            PruneSelection();
        }

        ClearDeferredCommandNotificationEffects();
    }

    private bool HasDeferredCommandNotificationEffects()
    {
        return _deferRedrawRequired ||
               _deferLayersChanged ||
               _deferPagesChanged ||
               _deferSelectionChanged ||
               _deferStylesChanged ||
               _deferRefreshStyleCache ||
               _deferUpdateActivePageView ||
               _deferPruneSelection;
    }

    private void ClearDeferredCommandNotificationEffects()
    {
        _deferRedrawRequired = false;
        _deferLayersChanged = false;
        _deferPagesChanged = false;
        _deferSelectionChanged = false;
        _deferStylesChanged = false;
        _deferRefreshStyleCache = false;
        _deferUpdateActivePageView = false;
        _deferPruneSelection = false;
    }

    public void SetDocument(Document document)
    {
        Document = document;
        ReplaceCommandDispatcher(new CommandDispatcher(Document));
        _backgroundImages.Clear();
        _selectedBalloonIds.Clear();
        _selectedPanelIds.Clear();
        _selectedFloatingImageIds.Clear();
        SelectedPanelId = null;
        _selectedFloatingImageId = null;
        if (Document.SelectedBalloonId.HasValue)
        {
            _selectedBalloonIds.Add(Document.SelectedBalloonId.Value);
        }
        ClearSelectionHistory();

        ViewTransform.ZoomToFit(new Rect(0, 0, Document.Size.Width, Document.Size.Height));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateActivePageView()
    {
        var page = Document?.ActivePage;
        if (page == null) return;

        ViewTransform.ZoomToFit(new Rect(0, 0, page.Size.Width, page.Size.Height));
        BackgroundImageChanged?.Invoke(this, EventArgs.Empty);
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public void Execute(ICommand command)
    {
        ClearSelectionHistory();
        CommandDispatcher?.Execute(command);
    }

    public void ExecuteTransaction(string description, IEnumerable<ICommand> commands)
    {
        ClearSelectionHistory();
        CommandDispatcher?.ExecuteTransaction(description, commands);
    }

    public void ExecuteTransactionSafe(string description, IEnumerable<ICommand> commands)
    {
        ClearSelectionHistory();
        CommandDispatcher?.ExecuteTransactionSafe(description, commands);
    }

    public bool Undo()
    {
        if (TryUndoSelectionChange())
        {
            return true;
        }

        ClearSelectionHistory();
        return CommandDispatcher?.Undo() ?? false;
    }

    public bool Redo()
    {
        if (TryRedoSelectionChange())
        {
            return true;
        }

        ClearSelectionHistory();
        return CommandDispatcher?.Redo() ?? false;
    }

    private void ReplaceCommandDispatcher(CommandDispatcher dispatcher)
    {
        DetachCommandDispatcherEvents();
        ClearDeferredCommandNotificationEffects();
        CommandDispatcher = dispatcher;
        AttachCommandDispatcherEvents();
        CommandHistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachCommandDispatcherEvents()
    {
        if (CommandDispatcher == null) return;
        CommandDispatcher.CommandExecuted += OnCommandExecuted;
        CommandDispatcher.CommandUndone += OnCommandExecuted;
        CommandDispatcher.CommandRedone += OnCommandExecuted;
        CommandDispatcher.History.HistoryChanged += OnCommandHistoryChanged;
    }

    private void DetachCommandDispatcherEvents()
    {
        if (CommandDispatcher == null) return;
        CommandDispatcher.CommandExecuted -= OnCommandExecuted;
        CommandDispatcher.CommandUndone -= OnCommandExecuted;
        CommandDispatcher.CommandRedone -= OnCommandExecuted;
        CommandDispatcher.History.HistoryChanged -= OnCommandHistoryChanged;
    }

    private void OnCommandHistoryChanged(object? sender, EventArgs e)
    {
        CommandHistoryChanged?.Invoke(this, EventArgs.Empty);

        if (CommandDispatcher?.History.IsInTransaction == true)
        {
            return;
        }

        FlushDeferredCommandNotificationEffects();
    }

    private bool ClearBalloonSelectionInternal()
    {
        if (Document == null) return false;
        if (_selectedBalloonIds.Count == 0 && Document.SelectedBalloonId == null) return false;

        _selectedBalloonIds.Clear();
        Document.SetSelectedBalloonId(null);
        return true;
    }

    private bool ClearFloatingImageSelectionInternal()
    {
        if (!_selectedFloatingImageId.HasValue && _selectedFloatingImageIds.Count == 0) return false;
        _selectedFloatingImageIds.Clear();
        _selectedFloatingImageId = null;
        return true;
    }

    private bool ClearPanelSelectionInternal()
    {
        if (_selectedPanelIds.Count == 0 && SelectedPanelId == null) return false;

        _selectedPanelIds.Clear();
        SelectedPanelId = null;
        return true;
    }

    private SelectionSnapshot CaptureSelectionSnapshot()
    {
        return new SelectionSnapshot(
            new HashSet<Guid>(_selectedBalloonIds),
            Document?.SelectedBalloonId,
            new HashSet<Guid>(_selectedPanelIds),
            SelectedPanelId,
            new HashSet<Guid>(_selectedFloatingImageIds),
            _selectedFloatingImageId);
    }

    private static bool SelectionSnapshotsEqual(SelectionSnapshot a, SelectionSnapshot b)
    {
        return a.PrimaryBalloonId == b.PrimaryBalloonId &&
               a.PrimaryPanelId == b.PrimaryPanelId &&
               a.PrimaryFloatingImageId == b.PrimaryFloatingImageId &&
               a.BalloonIds.SetEquals(b.BalloonIds) &&
               a.PanelIds.SetEquals(b.PanelIds) &&
               a.FloatingImageIds.SetEquals(b.FloatingImageIds);
    }

    private void RecordSelectionChange(SelectionSnapshot before)
    {
        if (_isApplyingSelectionHistory)
        {
            return;
        }

        var after = CaptureSelectionSnapshot();
        if (SelectionSnapshotsEqual(before, after))
        {
            return;
        }

        _selectionUndoHistory.Push(before);
        _selectionRedoHistory.Clear();
    }

    private void NotifySelectionChanged(SelectionSnapshot before)
    {
        RecordSelectionChange(before);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    private void ClearSelectionHistory()
    {
        _selectionUndoHistory.Clear();
        _selectionRedoHistory.Clear();
    }

    private bool TryUndoSelectionChange()
    {
        if (_selectionUndoHistory.Count == 0)
        {
            return false;
        }

        var target = _selectionUndoHistory.Pop();
        _selectionRedoHistory.Push(CaptureSelectionSnapshot());
        ApplySelectionSnapshot(target);
        return true;
    }

    private bool TryRedoSelectionChange()
    {
        if (_selectionRedoHistory.Count == 0)
        {
            return false;
        }

        var target = _selectionRedoHistory.Pop();
        _selectionUndoHistory.Push(CaptureSelectionSnapshot());
        ApplySelectionSnapshot(target);
        return true;
    }

    private void ApplySelectionSnapshot(SelectionSnapshot snapshot)
    {
        _isApplyingSelectionHistory = true;
        try
        {
            _selectedBalloonIds.Clear();
            foreach (var id in snapshot.BalloonIds)
            {
                if (CanSelectBalloonId(id))
                {
                    _selectedBalloonIds.Add(id);
                }
            }

            Guid? primaryBalloonId = snapshot.PrimaryBalloonId;
            if (primaryBalloonId.HasValue && !_selectedBalloonIds.Contains(primaryBalloonId.Value))
            {
                primaryBalloonId = null;
            }
            if (!primaryBalloonId.HasValue && _selectedBalloonIds.Count > 0)
            {
                primaryBalloonId = _selectedBalloonIds.First();
            }
            Document?.SetSelectedBalloonId(primaryBalloonId);

            _selectedPanelIds.Clear();
            var page = Document?.ActivePage;
            if (page != null)
            {
                foreach (var id in snapshot.PanelIds)
                {
                    if (page.FindPanel(id) != null)
                    {
                        _selectedPanelIds.Add(id);
                    }
                }
            }

            var primaryPanelId = snapshot.PrimaryPanelId;
            if (primaryPanelId.HasValue && !_selectedPanelIds.Contains(primaryPanelId.Value))
            {
                primaryPanelId = null;
            }
            if (!primaryPanelId.HasValue && _selectedPanelIds.Count > 0)
            {
                primaryPanelId = _selectedPanelIds.First();
            }
            SelectedPanelId = primaryPanelId;

            _selectedFloatingImageIds.Clear();
            if (page != null)
            {
                foreach (var id in snapshot.FloatingImageIds)
                {
                    if (CanSelectFloatingImageId(id))
                    {
                        _selectedFloatingImageIds.Add(id);
                    }
                }
            }

            var primaryImageId = snapshot.PrimaryFloatingImageId;
            if (primaryImageId.HasValue && !_selectedFloatingImageIds.Contains(primaryImageId.Value))
            {
                primaryImageId = null;
            }
            if (!primaryImageId.HasValue && _selectedFloatingImageIds.Count > 0)
            {
                primaryImageId = _selectedFloatingImageIds.First();
            }
            _selectedFloatingImageId = primaryImageId;

            if (EnforceObjectGroupSelection())
            {
                PruneSelection();
                return;
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RedrawRequired?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isApplyingSelectionHistory = false;
        }
    }

    private bool CanSelectBalloonId(Guid balloonId)
    {
        if (Document == null) return false;
        var page = Document.ActivePage;
        if (page == null) return false;
        if (page.FindBalloon(balloonId) == null) return false;
        if (!page.IsBalloonEffectivelyVisible(balloonId)) return false;
        return !page.IsBalloonEffectivelyLocked(balloonId);
    }

    private bool CanSelectFloatingImageId(Guid imageId)
    {
        var page = Document?.ActivePage;
        if (page == null) return false;

        var image = page.FindFloatingImage(imageId);
        if (image == null) return false;
        if (!image.IsVisible || image.IsLocked) return false;

        var layer = page.FindLayerForFloatingImage(image);
        if (layer == null) return false;
        if (!page.IsLayerEffectivelyVisible(layer.Id) || page.IsLayerEffectivelyLocked(layer.Id)) return false;
        return true;
    }

    private bool EnforceObjectGroupSelection()
    {
        var page = Document?.ActivePage;
        if (page == null) return false;
        if (_selectedBalloonIds.Count == 0 && _selectedFloatingImageIds.Count == 0) return false;

        var nextBalloons = new HashSet<Guid>(_selectedBalloonIds);
        var nextImages = new HashSet<Guid>(_selectedFloatingImageIds);
        var expanded = true;
        while (expanded)
        {
            expanded = false;
            foreach (var group in page.ObjectGroups)
            {
                var touched = group.BalloonIds.Any(nextBalloons.Contains) || group.FloatingImageIds.Any(nextImages.Contains);
                if (!touched) continue;

                foreach (var balloonId in group.BalloonIds)
                {
                    if (CanSelectBalloonId(balloonId) && nextBalloons.Add(balloonId))
                    {
                        expanded = true;
                    }
                }

                foreach (var imageId in group.FloatingImageIds)
                {
                    if (CanSelectFloatingImageId(imageId) && nextImages.Add(imageId))
                    {
                        expanded = true;
                    }
                }
            }
        }

        var changed = false;
        if (!_selectedBalloonIds.SetEquals(nextBalloons))
        {
            _selectedBalloonIds.Clear();
            foreach (var id in nextBalloons)
            {
                _selectedBalloonIds.Add(id);
            }
            changed = true;
        }

        if (!_selectedFloatingImageIds.SetEquals(nextImages))
        {
            _selectedFloatingImageIds.Clear();
            foreach (var id in nextImages)
            {
                _selectedFloatingImageIds.Add(id);
            }
            changed = true;
        }

        var nextPrimaryBalloon = Document?.SelectedBalloonId;
        if (nextPrimaryBalloon.HasValue && !_selectedBalloonIds.Contains(nextPrimaryBalloon.Value))
        {
            nextPrimaryBalloon = null;
        }
        if (!nextPrimaryBalloon.HasValue && _selectedBalloonIds.Count > 0)
        {
            nextPrimaryBalloon = _selectedBalloonIds.First();
        }
        if (Document != null && Document.SelectedBalloonId != nextPrimaryBalloon)
        {
            Document.SetSelectedBalloonId(nextPrimaryBalloon);
            changed = true;
        }

        var nextPrimaryImage = _selectedFloatingImageId;
        if (nextPrimaryImage.HasValue && !_selectedFloatingImageIds.Contains(nextPrimaryImage.Value))
        {
            nextPrimaryImage = null;
        }
        if (!nextPrimaryImage.HasValue && _selectedFloatingImageIds.Count > 0)
        {
            nextPrimaryImage = _selectedFloatingImageIds.First();
        }
        if (_selectedFloatingImageId != nextPrimaryImage)
        {
            _selectedFloatingImageId = nextPrimaryImage;
            changed = true;
        }

        return changed;
    }

    public void SelectBalloon(Guid? balloonId, bool preserveFloatingImageSelection = false)
    {
        if (Document == null) return;
        var before = CaptureSelectionSnapshot();

        var panelCleared = ClearPanelSelectionInternal();
        var floatingCleared = preserveFloatingImageSelection ? false : ClearFloatingImageSelectionInternal();


        if (balloonId == null)
        {
            var balloonCleared = _selectedBalloonIds.Count > 0 || Document.SelectedBalloonId != null;
            if (balloonCleared)
            {
                _selectedBalloonIds.Clear();
                Document.SetSelectedBalloonId(null);
            }

            if (panelCleared || balloonCleared || floatingCleared)
            {
                NotifySelectionChanged(before);
            }
            return;
        }

        if (!CanSelectBalloonId(balloonId.Value))
        {
            var balloonCleared = _selectedBalloonIds.Count > 0 || Document.SelectedBalloonId != null;
            if (balloonCleared)
            {
                _selectedBalloonIds.Clear();
                Document.SetSelectedBalloonId(null);
                NotifySelectionChanged(before);
            }
            return;
        }

        if (_selectedBalloonIds.Count == 1 &&
            _selectedBalloonIds.Contains(balloonId.Value) &&
            Document.SelectedBalloonId == balloonId)
        {
            if (panelCleared || floatingCleared)
            {
                NotifySelectionChanged(before);
            }
            return;
        }

        _selectedBalloonIds.Clear();
        _selectedBalloonIds.Add(balloonId.Value);
        Document.SetSelectedBalloonId(balloonId);
        EnforceObjectGroupSelection();
        NotifySelectionChanged(before);
    }

    public void SetSelection(IEnumerable<Guid> balloonIds, Guid? primaryBalloonId = null, bool preserveFloatingImageSelection = false)
    {
        if (Document == null) return;
        var before = CaptureSelectionSnapshot();

        var panelCleared = ClearPanelSelectionInternal();
        var floatingCleared = preserveFloatingImageSelection ? false : ClearFloatingImageSelectionInternal();


        _selectedBalloonIds.Clear();
        foreach (var id in balloonIds)
        {
            if (CanSelectBalloonId(id))
            {
                _selectedBalloonIds.Add(id);
            }
        }

        Guid? primary = primaryBalloonId;
        if (primary.HasValue && !_selectedBalloonIds.Contains(primary.Value))
        {
            primary = null;
        }

        if (!primary.HasValue && _selectedBalloonIds.Count > 0)
        {
            primary = _selectedBalloonIds.First();
        }

        Document.SetSelectedBalloonId(primary);
        EnforceObjectGroupSelection();
        if (panelCleared || floatingCleared)
        {
            NotifySelectionChanged(before);
            return;
        }
        NotifySelectionChanged(before);
    }

    public void ToggleBalloonSelection(Guid balloonId, bool preserveFloatingImageSelection = false)
    {
        if (Document == null) return;
        var before = CaptureSelectionSnapshot();

        var panelCleared = ClearPanelSelectionInternal();
        var floatingCleared = preserveFloatingImageSelection ? false : ClearFloatingImageSelectionInternal();


        var changed = false;
        if (_selectedBalloonIds.Contains(balloonId))
        {
            var group = Document.ActivePage?.FindObjectGroupByBalloon(balloonId);
            if (group != null)
            {
                foreach (var memberId in group.BalloonIds)
                {
                    _selectedBalloonIds.Remove(memberId);
                }
                foreach (var imageId in group.FloatingImageIds)
                {
                    _selectedFloatingImageIds.Remove(imageId);
                }
            }
            else
            {
                _selectedBalloonIds.Remove(balloonId);
            }
            changed = true;

            if (!Document.SelectedBalloonId.HasValue || !_selectedBalloonIds.Contains(Document.SelectedBalloonId.Value))
            {
                Guid? newPrimary = null;
                if (_selectedBalloonIds.Count > 0)
                {
                    newPrimary = _selectedBalloonIds.First();
                }

                Document.SetSelectedBalloonId(newPrimary);
            }

            if (_selectedFloatingImageId.HasValue && !_selectedFloatingImageIds.Contains(_selectedFloatingImageId.Value))
            {
                _selectedFloatingImageId = _selectedFloatingImageIds.Count > 0 ? _selectedFloatingImageIds.First() : null;
            }
        }
        else
        {
            if (!CanSelectBalloonId(balloonId))
            {
                if (panelCleared || floatingCleared)
                {
                    NotifySelectionChanged(before);
                }
                return;
            }

            _selectedBalloonIds.Add(balloonId);
            Document.SetSelectedBalloonId(balloonId);
            changed = true;
        }

        if (changed && EnforceObjectGroupSelection())
        {
            changed = true;
        }

        if (changed)
        {
            NotifySelectionChanged(before);
        }
        else if (panelCleared || floatingCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void SetPrimarySelection(Guid balloonId, bool preserveFloatingImageSelection = false)
    {
        if (Document == null) return;
        if (!_selectedBalloonIds.Contains(balloonId))
        {
            SelectBalloon(balloonId, preserveFloatingImageSelection);
            return;
        }

        var before = CaptureSelectionSnapshot();
        var panelCleared = ClearPanelSelectionInternal();
        var floatingCleared = !preserveFloatingImageSelection && ClearFloatingImageSelectionInternal();

        if (Document.SelectedBalloonId != balloonId || panelCleared || floatingCleared)
        {
            Document.SetSelectedBalloonId(balloonId);
            NotifySelectionChanged(before);
        }
    }

    public Rect? GetSelectionBounds()
    {
        if (Document == null || _selectedBalloonIds.Count == 0) return null;

        Rect? bounds = null;
        foreach (var id in _selectedBalloonIds)
        {
            var balloon = Document.FindBalloon(id);
            if (balloon == null) continue;

            bounds = bounds.HasValue ? bounds.Value.Union(balloon.Bounds) : balloon.Bounds;
        }

        return bounds;
    }

    public Rect? GetPanelSelectionBounds()
    {
        var page = Document?.ActivePage;
        if (page == null || _selectedPanelIds.Count == 0) return null;

        Rect? bounds = null;
        foreach (var id in _selectedPanelIds)
        {
            var panel = page.FindPanel(id);
            if (panel == null) continue;

            bounds = bounds.HasValue ? bounds.Value.Union(panel.Bounds) : panel.Bounds;
        }

        return bounds;
    }

    private void PruneSelection()
    {
        if (Document == null) return;

        var activePage = Document.ActivePage;
        var existingIds = new HashSet<Guid>(Document.AllBalloons.Select(b => b.Id));
        var changed = _selectedBalloonIds.RemoveWhere(id =>
        {
            if (!existingIds.Contains(id)) return true;
            if (activePage == null) return true;
            if (activePage.FindBalloon(id) == null) return true;
            if (!activePage.IsBalloonEffectivelyVisible(id)) return true;
            return activePage.IsBalloonEffectivelyLocked(id);
        }) > 0;

        var primary = Document.SelectedBalloonId;
        if (primary.HasValue &&
            (!existingIds.Contains(primary.Value) ||
             activePage == null ||
             activePage.FindBalloon(primary.Value) == null ||
             !activePage.IsBalloonEffectivelyVisible(primary.Value) ||
             activePage.IsBalloonEffectivelyLocked(primary.Value)))
        {
            Document.SetSelectedBalloonId(null);
            primary = null;
            changed = true;
        }

        if (primary.HasValue)
        {
            if (!_selectedBalloonIds.Contains(primary.Value))
            {
                _selectedBalloonIds.Clear();
                _selectedBalloonIds.Add(primary.Value);
                changed = true;
            }
        }
        else if (_selectedBalloonIds.Count > 0)
        {
            Document.SetSelectedBalloonId(_selectedBalloonIds.First());
            changed = true;
        }

        if (PrunePanelSelectionInternal())
        {
            changed = true;
        }

        if (activePage == null)
        {
            if (ClearFloatingImageSelectionInternal())
            {
                changed = true;
            }
        }
        else
        {
            var existingImageIds = new HashSet<Guid>(activePage.FloatingImages.Select(image => image.Id));
            if (_selectedFloatingImageIds.RemoveWhere(id => !existingImageIds.Contains(id)) > 0)
            {
                changed = true;
            }

            if (_selectedFloatingImageId.HasValue && !_selectedFloatingImageIds.Contains(_selectedFloatingImageId.Value))
            {
                _selectedFloatingImageId = null;
                changed = true;
            }

            if (!_selectedFloatingImageId.HasValue && _selectedFloatingImageIds.Count > 0)
            {
                _selectedFloatingImageId = _selectedFloatingImageIds.First();
                changed = true;
            }
        }

        if (EnforceObjectGroupSelection())
        {
            changed = true;
        }

        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RedrawRequired?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool PrunePanelSelectionInternal()
    {
        var page = Document?.ActivePage;
        if (page == null)
        {
            if (_selectedPanelIds.Count == 0 && SelectedPanelId == null) return false;
            _selectedPanelIds.Clear();
            SelectedPanelId = null;
            return true;
        }

        var existingIds = new HashSet<Guid>(page.Panels.Select(p => p.Id));
        var changed = _selectedPanelIds.RemoveWhere(id => !existingIds.Contains(id)) > 0;

        if (SelectedPanelId.HasValue && !existingIds.Contains(SelectedPanelId.Value))
        {
            SelectedPanelId = null;
            changed = true;
        }

        if (SelectedPanelId.HasValue)
        {
            if (!_selectedPanelIds.Contains(SelectedPanelId.Value))
            {
                _selectedPanelIds.Clear();
                _selectedPanelIds.Add(SelectedPanelId.Value);
                changed = true;
            }
        }
        else if (_selectedPanelIds.Count > 0)
        {
            SelectedPanelId = _selectedPanelIds.First();
            changed = true;
        }

        return changed;
    }

    public void NotifyLayersChanged()
    {
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetSmartGuides(IEnumerable<SmartGuideLine>? guides)
    {
        _smartGuides.Clear();
        if (guides != null)
        {
            _smartGuides.AddRange(guides);
        }
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    internal void ClearSmartGuides()
    {
        if (_smartGuides.Count == 0) return;
        _smartGuides.Clear();
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public Balloon? HitTestBalloon(Point2 screenPoint)
    {
        if (Document == null) return null;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        var page = Document.ActivePage;

        for (int i = Document.Layers.Count - 1; i >= 0; i--)
        {
            var layer = Document.Layers[i];
            if (layer.Kind != LayerKind.Balloon) continue;
            if (page != null && (!page.IsLayerEffectivelyVisible(layer.Id) || page.IsLayerEffectivelyLocked(layer.Id))) continue;

            for (int j = layer.Balloons.Count - 1; j >= 0; j--)
            {
                var balloon = layer.Balloons[j];
                if (!balloon.IsVisible || balloon.IsLocked) continue;

                if (balloon.PanelId.HasValue && page != null)
                {
                    var panel = page.FindPanel(balloon.PanelId.Value);
                    if (panel != null && (!panel.IsVisible || panel.IsLocked)) continue;
                }

                if (MathF.Abs(balloon.Rotation) > 0.01f)
                {
                    var rotationRadians = balloon.Rotation * MathF.PI / 180f;
                    var localPoint = BalloonGeometry.RotatePointAround(worldPoint, balloon.Position, -rotationRadians);
                    if (balloon.Bounds.Contains(localPoint))
                    {
                        return balloon;
                    }
                }
                else if (balloon.Bounds.Contains(worldPoint))
                {
                    return balloon;
                }
            }
        }

        return null;
    }

    public Balloon? HitTestTailHandle(Point2 screenPoint)
    {
        if (Document == null) return null;
        if (_selectedBalloonIds.Count != 1) return null;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        const float handleRadius = 8f;

        var selectedBalloon = Document.SelectedBalloon;
        if (selectedBalloon?.Tail != null)
        {
            var renderedTarget = TailGeometry.GetRenderedTargetPoint(selectedBalloon, selectedBalloon.Tail);
            var distance = Point2.Distance(worldPoint, renderedTarget);
            if (distance <= handleRadius / ViewTransform.Zoom)
            {
                return selectedBalloon;
            }
        }

        return null;
    }

    public Balloon? HitTestRotationHandle(Point2 screenPoint)
    {
        if (Document == null) return null;
        if (_selectedBalloonIds.Count != 1) return null;

        var selectedBalloon = Document.SelectedBalloon;
        if (selectedBalloon == null) return null;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        var handlePoint = BalloonGeometry.GetRotationHandlePosition(selectedBalloon);
        var hitRadius = 8f / ViewTransform.Zoom;

        return Point2.Distance(worldPoint, handlePoint) <= hitRadius ? selectedBalloon : null;
    }

    public PanelZone? HitTestPanel(Point2 screenPoint)
    {
        var page = Document?.ActivePage;
        if (page == null) return null;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);

        for (int i = page.Panels.Count - 1; i >= 0; i--)
        {
            var panel = page.Panels[i];
            if (!panel.IsVisible || panel.IsLocked) continue;

            if (panel.Bounds.Contains(worldPoint))
            {
                return panel;
            }
        }

        return null;
    }

    public FloatingImage? HitTestFloatingImage(Point2 screenPoint)
    {
        var page = Document?.ActivePage;
        if (page == null) return null;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);

        for (int i = page.FloatingImages.Count - 1; i >= 0; i--)
        {
            var image = page.FloatingImages[i];
            var layer = page.FindLayerForFloatingImage(image);
            if (layer == null) continue;
            if (!layer.IsVisible || layer.IsLocked) continue;
            if (!image.IsVisible || image.IsLocked) continue;
            if (!image.Bounds.Contains(worldPoint)) continue;

            if (image.PanelId.HasValue)
            {
                var panel = page.FindPanel(image.PanelId.Value);
                if (panel != null && !panel.IsVisible) continue;

                if (image.ConstrainToPanel && panel != null && !IsPointInsidePanelClip(panel, worldPoint))
                {
                    continue;
                }
            }

            return image;
        }

        return null;
    }

    private static bool IsPointInsidePanelClip(PanelZone panel, Point2 worldPoint)
    {
        if (!panel.Bounds.Contains(worldPoint))
        {
            return false;
        }

        if (panel.Shape == PanelShape.Ellipse)
        {
            var radiusX = panel.Bounds.Width / 2f;
            var radiusY = panel.Bounds.Height / 2f;
            if (radiusX <= 0f || radiusY <= 0f)
            {
                return false;
            }

            var center = panel.Bounds.Center;
            var dx = (worldPoint.X - center.X) / radiusX;
            var dy = (worldPoint.Y - center.Y) / radiusY;
            return (dx * dx) + (dy * dy) <= 1f;
        }

        if (panel.Shape == PanelShape.RoundedRect)
        {
            var maxRadius = MathF.Min(panel.Bounds.Width, panel.Bounds.Height) / 2f;
            var radius = MathF.Min(Math.Max(0f, panel.CornerRadius), maxRadius);
            if (radius <= 0f)
            {
                return true;
            }

            var innerLeft = panel.Bounds.X + radius;
            var innerRight = panel.Bounds.X + panel.Bounds.Width - radius;
            var innerTop = panel.Bounds.Y + radius;
            var innerBottom = panel.Bounds.Y + panel.Bounds.Height - radius;

            if (worldPoint.X >= innerLeft && worldPoint.X <= innerRight)
            {
                return true;
            }

            if (worldPoint.Y >= innerTop && worldPoint.Y <= innerBottom)
            {
                return true;
            }

            var cornerX = worldPoint.X < innerLeft ? innerLeft : innerRight;
            var cornerY = worldPoint.Y < innerTop ? innerTop : innerBottom;
            var offsetX = worldPoint.X - cornerX;
            var offsetY = worldPoint.Y - cornerY;
            return (offsetX * offsetX) + (offsetY * offsetY) <= radius * radius;
        }

        return true;
    }

    public ResizeHandle HitTestFloatingImageResizeHandle(Point2 screenPoint)
    {
        var page = Document?.ActivePage;
        if (page == null || !_selectedFloatingImageId.HasValue || _selectedFloatingImageIds.Count != 1) return ResizeHandle.None;

        var image = page.FindFloatingImage(_selectedFloatingImageId.Value);
        if (image == null || image.IsLocked) return ResizeHandle.None;

        var layer = page.FindLayerForFloatingImage(image);
        if (layer == null || !layer.IsVisible || layer.IsLocked) return ResizeHandle.None;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        var bounds = image.Bounds.Inflate(4, 4);
        const float handleSize = 8f;
        var hitRadius = handleSize / ViewTransform.Zoom;

        return HitTestNearestResizeHandle(worldPoint, bounds, hitRadius);
    }

    public ResizeHandle HitTestPanelResizeHandle(Point2 screenPoint)
    {
        var page = Document?.ActivePage;
        if (page == null || !SelectedPanelId.HasValue) return ResizeHandle.None;

        var panel = page.FindPanel(SelectedPanelId.Value);
        if (panel == null) return ResizeHandle.None;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        var bounds = panel.Bounds;
        const float handleSize = 8f;
        var hitRadius = handleSize / ViewTransform.Zoom;

        return HitTestNearestResizeHandle(worldPoint, bounds, hitRadius);
    }

    public void SelectPanel(Guid? panelId)
    {
        var before = CaptureSelectionSnapshot();
        var balloonCleared = ClearBalloonSelectionInternal();
        var floatingCleared = ClearFloatingImageSelectionInternal();


        if (panelId == null)
        {
            var panelCleared = ClearPanelSelectionInternal();
            if (panelCleared || balloonCleared || floatingCleared)
            {
                NotifySelectionChanged(before);
            }
            return;
        }

        if (_selectedPanelIds.Count == 1 &&
            _selectedPanelIds.Contains(panelId.Value) &&
            SelectedPanelId == panelId)
        {
            if (balloonCleared || floatingCleared)
            {
                NotifySelectionChanged(before);
            }
            return;
        }

        _selectedPanelIds.Clear();
        _selectedPanelIds.Add(panelId.Value);
        SelectedPanelId = panelId;

        NotifySelectionChanged(before);
    }

    public void SetPanelSelection(IEnumerable<Guid> panelIds, Guid? primaryPanelId = null)
    {
        var before = CaptureSelectionSnapshot();
        var balloonCleared = ClearBalloonSelectionInternal();
        var floatingCleared = ClearFloatingImageSelectionInternal();


        _selectedPanelIds.Clear();

        var page = Document?.ActivePage;
        if (page != null)
        {
            foreach (var id in panelIds)
            {
                if (page.FindPanel(id) != null)
                {
                    _selectedPanelIds.Add(id);
                }
            }
        }

        Guid? primary = primaryPanelId;
        if (primary.HasValue && !_selectedPanelIds.Contains(primary.Value))
        {
            primary = null;
        }

        if (!primary.HasValue && _selectedPanelIds.Count > 0)
        {
            primary = _selectedPanelIds.First();
        }

        SelectedPanelId = primary;

        if (_selectedPanelIds.Count > 0 || balloonCleared || floatingCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void TogglePanelSelection(Guid panelId)
    {
        var before = CaptureSelectionSnapshot();
        var balloonCleared = ClearBalloonSelectionInternal();
        var floatingCleared = ClearFloatingImageSelectionInternal();


        var page = Document?.ActivePage;
        if (page == null || page.FindPanel(panelId) == null) return;

        var changed = false;
        if (_selectedPanelIds.Contains(panelId))
        {
            _selectedPanelIds.Remove(panelId);
            changed = true;

            if (SelectedPanelId == panelId)
            {
                SelectedPanelId = _selectedPanelIds.Count > 0 ? _selectedPanelIds.First() : null;
            }
        }
        else
        {
            _selectedPanelIds.Add(panelId);
            SelectedPanelId = panelId;
            changed = true;
        }

        if (changed || balloonCleared || floatingCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void SetPrimaryPanelSelection(Guid panelId)
    {
        if (!_selectedPanelIds.Contains(panelId))
        {
            SelectPanel(panelId);
            return;
        }

        var before = CaptureSelectionSnapshot();
        var floatingCleared = ClearFloatingImageSelectionInternal();

        if (SelectedPanelId != panelId || floatingCleared)
        {
            SelectedPanelId = panelId;
            NotifySelectionChanged(before);
        }
    }

    public void ClearPanelSelection()
    {
        var before = CaptureSelectionSnapshot();
        if (!ClearPanelSelectionInternal()) return;
        NotifySelectionChanged(before);
    }

    public void SelectFloatingImage(Guid? imageId, bool preserveBalloonSelection = false)
    {
        var before = CaptureSelectionSnapshot();
        var balloonCleared = preserveBalloonSelection ? false : ClearBalloonSelectionInternal();
        var panelCleared = ClearPanelSelectionInternal();


        bool changed;
        if (imageId == null)
        {
            changed = ClearFloatingImageSelectionInternal();
        }
        else
        {
            changed = SetFloatingImageSelectionInternal(new[] { imageId.Value }, imageId);
        }

        if (EnforceObjectGroupSelection())
        {
            changed = true;
        }

        if (changed || balloonCleared || panelCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void SetFloatingImageSelection(IEnumerable<Guid> imageIds, Guid? primaryImageId = null, bool preserveBalloonSelection = false)
    {
        var before = CaptureSelectionSnapshot();
        var balloonCleared = preserveBalloonSelection ? false : ClearBalloonSelectionInternal();
        var panelCleared = ClearPanelSelectionInternal();

        var changed = SetFloatingImageSelectionInternal(imageIds, primaryImageId);
        if (EnforceObjectGroupSelection())
        {
            changed = true;
        }

        if (changed || balloonCleared || panelCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void ToggleFloatingImageSelection(Guid imageId, bool preserveBalloonSelection = false)
    {
        var page = Document?.ActivePage;
        if (page == null || page.FindFloatingImage(imageId) == null) return;
        var before = CaptureSelectionSnapshot();

        var balloonCleared = preserveBalloonSelection ? false : ClearBalloonSelectionInternal();
        var panelCleared = ClearPanelSelectionInternal();


        var changed = false;
        if (_selectedFloatingImageIds.Contains(imageId))
        {
            var group = page.FindObjectGroupByFloatingImage(imageId);
            if (group != null)
            {
                foreach (var memberId in group.FloatingImageIds)
                {
                    _selectedFloatingImageIds.Remove(memberId);
                }
                foreach (var balloonId in group.BalloonIds)
                {
                    _selectedBalloonIds.Remove(balloonId);
                }
            }
            else
            {
                _selectedFloatingImageIds.Remove(imageId);
            }
            changed = true;

            if (!_selectedFloatingImageId.HasValue || !_selectedFloatingImageIds.Contains(_selectedFloatingImageId.Value))
            {
                _selectedFloatingImageId = _selectedFloatingImageIds.Count > 0 ? _selectedFloatingImageIds.First() : null;
            }

            if (Document != null &&
                (!Document.SelectedBalloonId.HasValue || !_selectedBalloonIds.Contains(Document.SelectedBalloonId.Value)))
            {
                Document.SetSelectedBalloonId(_selectedBalloonIds.Count > 0 ? _selectedBalloonIds.First() : null);
            }
        }
        else
        {
            if (!CanSelectFloatingImageId(imageId))
            {
                if (balloonCleared || panelCleared)
                {
                    NotifySelectionChanged(before);
                }
                return;
            }

            _selectedFloatingImageIds.Add(imageId);
            _selectedFloatingImageId = imageId;
            changed = true;
        }

        if (changed && EnforceObjectGroupSelection())
        {
            changed = true;
        }

        if (changed || balloonCleared || panelCleared)
        {
            NotifySelectionChanged(before);
        }
    }

    public void SetPrimaryFloatingImageSelection(Guid imageId, bool preserveBalloonSelection = false)
    {
        var page = Document?.ActivePage;
        if (page == null || page.FindFloatingImage(imageId) == null) return;

        if (!_selectedFloatingImageIds.Contains(imageId))
        {
            SelectFloatingImage(imageId, preserveBalloonSelection);
            return;
        }

        var before = CaptureSelectionSnapshot();
        var balloonCleared = preserveBalloonSelection ? false : ClearBalloonSelectionInternal();
        var panelCleared = ClearPanelSelectionInternal();

        if (_selectedFloatingImageId != imageId || balloonCleared || panelCleared)
        {
            _selectedFloatingImageId = imageId;
            NotifySelectionChanged(before);
        }
    }

    private bool SetFloatingImageSelectionInternal(IEnumerable<Guid> imageIds, Guid? primaryImageId = null)
    {
        var page = Document?.ActivePage;
        var nextSelection = new HashSet<Guid>();
        if (page != null)
        {
            foreach (var id in imageIds)
            {
                if (CanSelectFloatingImageId(id))
                {
                    nextSelection.Add(id);
                }
            }
        }

        var changed = !_selectedFloatingImageIds.SetEquals(nextSelection);
        if (changed)
        {
            _selectedFloatingImageIds.Clear();
            foreach (var id in nextSelection)
            {
                _selectedFloatingImageIds.Add(id);
            }
        }

        var nextPrimary = primaryImageId;
        if (nextPrimary.HasValue && !_selectedFloatingImageIds.Contains(nextPrimary.Value))
        {
            nextPrimary = null;
        }

        if (!nextPrimary.HasValue && _selectedFloatingImageIds.Count > 0)
        {
            nextPrimary = _selectedFloatingImageIds.First();
        }

        if (_selectedFloatingImageId != nextPrimary)
        {
            _selectedFloatingImageId = nextPrimary;
            changed = true;
        }

        return changed;
    }

    public void ClearFloatingImageSelection()
    {
        var before = CaptureSelectionSnapshot();
        if (!ClearFloatingImageSelectionInternal()) return;
        NotifySelectionChanged(before);
    }

    public void UpdateHoveredPanel(Guid? panelId)
    {
        if (HoveredPanelId == panelId) return;

        HoveredPanelId = panelId;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }

    public ResizeHandle HitTestResizeHandle(Point2 screenPoint)
    {
        if (Document == null) return ResizeHandle.None;

        Rect? selectionBounds = null;
        if (_selectedBalloonIds.Count > 1)
        {
            selectionBounds = GetSelectionBounds();
        }
        else if (Document.SelectedBalloon != null)
        {
            var balloon = Document.SelectedBalloon;
            selectionBounds = MathF.Abs(balloon.Rotation) > 0.01f
                ? BalloonGeometry.GetRotatedBounds(balloon)
                : balloon.Bounds;
        }

        if (!selectionBounds.HasValue) return ResizeHandle.None;

        var worldPoint = ViewTransform.ScreenToWorld(screenPoint);
        var bounds = selectionBounds.Value.Inflate(4, 4); // Same inflation as selection highlight
        const float handleSize = 8f;
        var hitRadius = Math.Max(handleSize, 12f / ViewTransform.Zoom);

        return HitTestNearestResizeHandle(worldPoint, bounds, hitRadius);
    }

    private static ResizeHandle HitTestNearestResizeHandle(Point2 worldPoint, Rect bounds, float hitRadius)
    {
        var candidates = new (ResizeHandle Handle, Point2 Point)[]
        {
            (ResizeHandle.TopLeft, bounds.TopLeft),
            (ResizeHandle.TopRight, bounds.TopRight),
            (ResizeHandle.BottomLeft, bounds.BottomLeft),
            (ResizeHandle.BottomRight, bounds.BottomRight)
        };

        var closestHandle = ResizeHandle.None;
        var closestDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Point2.Distance(worldPoint, candidate.Point);
            if (distance <= hitRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestHandle = candidate.Handle;
            }
        }

        return closestHandle;
    }

    private void DeleteSelectionInternal()
    {
        if (!HasSelection) return;

        EditingText = EditingText.Remove(EditingSelectionStart, EditingSelectionLength);
        RemoveRangeFromSpans(EditingSelectionStart, EditingSelectionLength);
        EditingCursorPosition = EditingSelectionStart;
        ClearSelectionInternal();
    }

    private void ClearSelectionInternal()
    {
        EditingSelectionStart = 0;
        EditingSelectionLength = 0;
        EditingSelectionAnchor = EditingCursorPosition;
    }

    public bool ApplyTextStyleToSelection(TextStyle style)
    {
        if (EditingBalloonId == null || EditingSelectionLength == 0) return false;

        PushTextEditSnapshot();
        ApplyStyleSpan(style, EditingSelectionStart, EditingSelectionLength);
        RedrawRequired?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private TextStyle GetEditingBaseTextStyle()
    {
        if (EditingBalloonId == null || Document == null) return TextStyle.Default;
        var balloon = Document.FindBalloon(EditingBalloonId.Value);
        return balloon?.TextStyle ?? TextStyle.Default;
    }

    private TextStyle GetStyleAtIndex(int index)
    {
        var baseStyle = GetEditingBaseTextStyle();
        if (_editingTextStyleSpans.Count == 0) return baseStyle;

        var safeIndex = Math.Clamp(index, 0, Math.Max(EditingText.Length - 1, 0));
        foreach (var span in _editingTextStyleSpans)
        {
            if (safeIndex >= span.Start && safeIndex < span.Start + span.Length)
            {
                return span.Style;
            }
        }

        return baseStyle;
    }

    private TextStyle GetStyleForInsertion(int insertIndex)
    {
        if (_editingInsertionStyleOverride != null)
        {
            return _editingInsertionStyleOverride;
        }

        if (EditingText.Length == 0) return GetEditingBaseTextStyle();
        var lookupIndex = Math.Clamp(insertIndex - 1, 0, EditingText.Length - 1);
        return GetStyleAtIndex(lookupIndex);
    }

    private void ApplyStyleSpan(TextStyle style, int start, int length)
    {
        if (length <= 0) return;

        var baseStyle = GetEditingBaseTextStyle();
        RemoveRangeFromSpans(start, length, shiftFollowingSpans: false);

        if (TextStyleUtilities.AreInlineEquivalent(style, baseStyle))
        {
            NormalizeSpans();
            return;
        }

        _editingTextStyleSpans.Add(new TextStyleSpan(start, length, style));
        NormalizeSpans();
    }

    private void RemoveRangeFromSpans(int start, int length, bool shiftFollowingSpans = true)
    {
        if (length <= 0 || _editingTextStyleSpans.Count == 0) return;

        var end = start + length;
        var updated = new List<TextStyleSpan>(_editingTextStyleSpans.Count);

        foreach (var span in _editingTextStyleSpans)
        {
            var spanStart = span.Start;
            var spanEnd = span.Start + span.Length;

            if (spanEnd <= start)
            {
                updated.Add(span.Clone());
                continue;
            }

            if (spanStart >= end)
            {
                var shiftedStart = shiftFollowingSpans ? spanStart - length : spanStart;
                updated.Add(new TextStyleSpan(shiftedStart, span.Length, span.Style));
                continue;
            }

            if (spanStart < start)
            {
                updated.Add(new TextStyleSpan(spanStart, start - spanStart, span.Style));
            }

            if (spanEnd > end)
            {
                var rightStart = shiftFollowingSpans ? start : end;
                updated.Add(new TextStyleSpan(rightStart, spanEnd - end, span.Style));
            }
        }

        _editingTextStyleSpans.Clear();
        _editingTextStyleSpans.AddRange(updated);
        NormalizeSpans();
    }

    private void InsertRangeIntoSpans(int insertIndex, int length, TextStyle? inheritStyle)
    {
        if (length <= 0) return;

        var updated = new List<TextStyleSpan>(_editingTextStyleSpans.Count + 1);
        foreach (var span in _editingTextStyleSpans)
        {
            var spanStart = span.Start;
            var spanEnd = span.Start + span.Length;

            if (spanEnd <= insertIndex)
            {
                updated.Add(span.Clone());
                continue;
            }

            if (spanStart >= insertIndex)
            {
                updated.Add(new TextStyleSpan(spanStart + length, span.Length, span.Style));
                continue;
            }

            var leftLength = insertIndex - spanStart;
            if (leftLength > 0)
            {
                updated.Add(new TextStyleSpan(spanStart, leftLength, span.Style));
            }

            var rightLength = spanEnd - insertIndex;
            if (rightLength > 0)
            {
                updated.Add(new TextStyleSpan(insertIndex + length, rightLength, span.Style));
            }
        }

        _editingTextStyleSpans.Clear();
        _editingTextStyleSpans.AddRange(updated);

        if (inheritStyle != null)
        {
            var baseStyle = GetEditingBaseTextStyle();
            if (!TextStyleUtilities.AreInlineEquivalent(inheritStyle, baseStyle))
            {
                _editingTextStyleSpans.Add(new TextStyleSpan(insertIndex, length, inheritStyle));
            }
        }

        NormalizeSpans();
    }

    private void NormalizeSpans()
    {
        if (_editingTextStyleSpans.Count == 0) return;

        _editingTextStyleSpans.RemoveAll(span => span.Length <= 0);
        if (_editingTextStyleSpans.Count <= 1) return;

        _editingTextStyleSpans.Sort((a, b) => a.Start.CompareTo(b.Start));
        var merged = new List<TextStyleSpan>(_editingTextStyleSpans.Count);

        foreach (var span in _editingTextStyleSpans)
        {
            if (merged.Count == 0)
            {
                merged.Add(span);
                continue;
            }

            var last = merged[^1];
            var lastEnd = last.Start + last.Length;
            if (lastEnd == span.Start && TextStyleUtilities.AreInlineEquivalent(last.Style, span.Style))
            {
                last.Length += span.Length;
            }
            else
            {
                merged.Add(span);
            }
        }

        _editingTextStyleSpans.Clear();
        _editingTextStyleSpans.AddRange(merged);
    }

    private static bool AreTextStyleSpansEquivalent(IReadOnlyList<TextStyleSpan> spansA, IReadOnlyList<TextStyleSpan> spansB)
    {
        if (spansA.Count != spansB.Count) return false;

        for (int i = 0; i < spansA.Count; i++)
        {
            var a = spansA[i];
            var b = spansB[i];
            if (a.Start != b.Start || a.Length != b.Length) return false;
            if (!TextStyleUtilities.AreInlineEquivalent(a.Style, b.Style)) return false;
        }

        return true;
    }

    private void ClearTextEditHistory()
    {
        _textEditUndo.Clear();
        _textEditRedo.Clear();
    }

    private void PushTextEditSnapshot()
    {
        _textEditUndo.Push(CaptureSnapshot());
        _textEditRedo.Clear();
    }

    private TextEditSnapshot CaptureSnapshot()
    {
        return new TextEditSnapshot(
            EditingText,
            EditingCursorPosition,
            EditingSelectionStart,
            EditingSelectionLength,
            EditingSelectionAnchor,
            _editingTextStyleSpans.Select(span => span.Clone()).ToList(),
            _editingInsertionStyleOverride);
    }

    private void RestoreSnapshot(TextEditSnapshot snapshot)
    {
        EditingText = snapshot.Text;
        EditingCursorPosition = snapshot.CursorPosition;
        EditingSelectionStart = snapshot.SelectionStart;
        EditingSelectionLength = snapshot.SelectionLength;
        EditingSelectionAnchor = snapshot.SelectionAnchor;
        _editingTextStyleSpans.Clear();
        if (snapshot.TextStyleSpans.Count > 0)
        {
            _editingTextStyleSpans.AddRange(snapshot.TextStyleSpans.Select(span => span.Clone()));
        }
        _editingInsertionStyleOverride = snapshot.InsertionStyleOverride;
        RedrawRequired?.Invoke(this, EventArgs.Empty);
    }
}

internal readonly struct TextEditSnapshot
{
    public TextEditSnapshot(
        string text,
        int cursorPosition,
        int selectionStart,
        int selectionLength,
        int selectionAnchor,
        IReadOnlyList<TextStyleSpan> textStyleSpans,
        TextStyle? insertionStyleOverride)
    {
        Text = text;
        CursorPosition = cursorPosition;
        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
        SelectionAnchor = selectionAnchor;
        TextStyleSpans = textStyleSpans;
        InsertionStyleOverride = insertionStyleOverride;
    }

    public string Text { get; }
    public int CursorPosition { get; }
    public int SelectionStart { get; }
    public int SelectionLength { get; }
    public int SelectionAnchor { get; }
    public IReadOnlyList<TextStyleSpan> TextStyleSpans { get; }
    public TextStyle? InsertionStyleOverride { get; }
}

internal readonly struct BalloonResizeSnapshot
{
    public BalloonResizeSnapshot(Point2 position, Size2 size, float? maxTextWidth, float? maxTextHeight)
    {
        Position = position;
        Size = size;
        MaxTextWidth = maxTextWidth;
        MaxTextHeight = maxTextHeight;
    }

    public Point2 Position { get; }
    public Size2 Size { get; }
    public float? MaxTextWidth { get; }
    public float? MaxTextHeight { get; }
}

public enum EditorMode
{
    Select,

    CreateBalloon,

    EditText,

    PanelLayout,

    Pan
}

public enum PanelLayoutToolMode
{
    Select,

    Draw
}

public enum DragType
{
    None,
    Pan,
    MoveBalloon,
    MoveTailTarget,
    MoveTailAttachment,
    CreateBalloon,
    CreatePanel,
    CreatePanelFreeform,
    MovePanel,
    ResizePanel,
    MoveFloatingImage,
    ResizeFloatingImage,
    MoveGuide,
    ResizeBalloon,
    RotateBalloon,
    TextSelection,
    MarqueeSelect,
    EditPanelVertex,
    MoveTextPathHandle
}

public enum ResizeHandle
{
    None,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
