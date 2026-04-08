using Letterist.Commands;
using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Persistence;
using Letterist.Rendering;
using Letterist.Rendering.Typesetting;
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

    private void MainCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(MainCanvas);
        var screenPos = new Point2((float)pointer.Position.X, (float)pointer.Position.Y);
        var worldPos = _editorState.ViewTransform.ScreenToWorld(screenPos);

        _isPointerOverCanvas = true;
        _lastPointerPosition = screenPos;

        if (IsTouchInput(pointer.PointerDeviceType))
        {
            _touchTapPending = true;
            _touchTapMoved = false;
            _touchTapStartScreen = screenPos;
            e.Handled = true;
            return;
        }

        if (pointer.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(screenPos, worldPos, pointer.Position);
            e.Handled = true;
            return;
        }

        if (pointer.Properties.IsMiddleButtonPressed)
        {
            StartPan(screenPos, e.Pointer);
            e.Handled = true;
            return;
        }

        if (pointer.Properties.IsLeftButtonPressed)
        {
            MainCanvas.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (_isBalloonTemplateEyedropperActive)
            {
                _ = HandleBalloonTemplateEyedropperPickAsync(screenPos);
                e.Handled = true;
                return;
            }

            if (TryStartGuideFromRuler(screenPos, worldPos, e.Pointer))
            {
                e.Handled = true;
                return;
            }

            if (_editorState.Mode == EditorMode.EditText)
            {
                var editingBalloon = _editorState.EditingBalloonId.HasValue
                    ? _editorState.Document?.FindBalloon(_editorState.EditingBalloonId.Value)
                    : null;

                if (editingBalloon == null || !editingBalloon.Bounds.Contains(worldPos))
                {
                    _editorState.ExitTextEditMode(saveChanges: true);
                }
                else
                {
                    var now = DateTime.Now;
                    var caretIndex = GetCaretIndexFromPoint(editingBalloon, worldPos);

                    if (_lastTextClickBalloonId == editingBalloon.Id &&
                        (now - _lastTextClickTime).TotalMilliseconds < DoubleClickTimeMs)
                    {
                        SelectWordAtIndex(caretIndex);
                        _lastTextClickBalloonId = null;
                        _textCaretDesiredX = null;
                        e.Handled = true;
                        return;
                    }

                    _lastTextClickTime = now;
                    _lastTextClickBalloonId = editingBalloon.Id;
                    _editorState.SetCursorPosition(caretIndex, extendSelection: shift);
                    _textCaretDesiredX = null;
                    StartTextSelectionDrag(editingBalloon, screenPos, worldPos, e.Pointer);
                    e.Handled = true;
                    return;
                }
            }

            if (TryStartGuideDrag(screenPos, worldPos, e.Pointer))
            {
                e.Handled = true;
                return;
            }

            if (_editorState.Mode == EditorMode.PanelLayout)
            {
                var additive = shift || ctrl;
                var polygonDrawClick = _panelLayoutTool == PanelLayoutToolMode.Draw &&
                                       _selectedPanelDrawTool == PanelDrawTool.Polygon;

                if (_isEditingPanelShape)
                {
                    if (TryStartPanelVertexDrag(screenPos, worldPos, e.Pointer))
                    {
                        e.Handled = true;
                        return;
                    }

                    var hitEditPanel = _editorState.HitTestPanel(screenPos);
                    if (hitEditPanel != null && _editingPanelId.HasValue && hitEditPanel.Id == _editingPanelId.Value)
                    {
                        var now = DateTime.Now;
                        if (_lastClickedPanelId == hitEditPanel.Id &&
                            (now - _lastPanelClickTime).TotalMilliseconds < DoubleClickTimeMs)
                        {
                            CommitPanelShapeEdit();
                        }
                        else
                        {
                            _lastPanelClickTime = now;
                            _lastClickedPanelId = hitEditPanel.Id;
                        }

                        e.Handled = true;
                        return;
                    }

                    e.Handled = true;
                    return;
                }

                if (polygonDrawClick)
                {
                    HandlePanelPolygonClick(screenPos, worldPos);
                    e.Handled = true;
                    return;
                }

                var panelResizeHandle = _editorState.HitTestPanelResizeHandle(screenPos);
                if (panelResizeHandle != ResizeHandle.None)
                {
                    StartPanelResizeDrag(panelResizeHandle, screenPos, worldPos, e.Pointer);
                    e.Handled = true;
                    return;
                }

                var hitPanel = _editorState.HitTestPanel(screenPos);
                if (hitPanel != null)
                {
                    var now = DateTime.Now;
                    if (hitPanel.Shape == PanelShape.Custom &&
                        _lastClickedPanelId == hitPanel.Id &&
                        (now - _lastPanelClickTime).TotalMilliseconds < DoubleClickTimeMs)
                    {
                        EnterPanelShapeEdit(hitPanel);
                        _lastClickedPanelId = null;
                        e.Handled = true;
                        return;
                    }

                    _lastPanelClickTime = now;
                    _lastClickedPanelId = hitPanel.Id;

                    if (ctrl)
                    {
                        _editorState.TogglePanelSelection(hitPanel.Id);
                        e.Handled = true;
                        return;
                    }

                    if (shift)
                    {
                        AddPanelToSelection(hitPanel.Id);
                        e.Handled = true;
                        return;
                    }

                    if (_editorState.SelectedPanelIds.Contains(hitPanel.Id))
                    {
                        _editorState.SetPrimaryPanelSelection(hitPanel.Id);
                    }
                    else
                    {
                        _editorState.SelectPanel(hitPanel.Id);
                    }

                    StartPanelMoveDrag(hitPanel, screenPos, worldPos, e.Pointer);
                    e.Handled = true;
                    return;
                }

                if (!additive)
                {
                    _editorState.SelectPanel(null);
                }

                if (_panelLayoutTool == PanelLayoutToolMode.Draw)
                {
                    switch (_selectedPanelDrawTool)
                    {
                        case PanelDrawTool.Polygon:
                            HandlePanelPolygonClick(screenPos, worldPos);
                            break;
                        case PanelDrawTool.Freeform:
                            StartPanelFreeformDrag(screenPos, worldPos, e.Pointer);
                            break;
                        default:
                            StartPanelDrag(screenPos, worldPos, e.Pointer);
                            break;
                    }
                }

                e.Handled = true;
                return;
            }

            if (_editorState.Mode == EditorMode.CreateBalloon)
            {
                CreateBalloonAtPosition(worldPos);
                e.Handled = true;
                return;
            }

            var resizeHandle = _editorState.HitTestResizeHandle(screenPos);
            if (resizeHandle != ResizeHandle.None)
            {
                StartResizeDrag(_editorState.Document!.SelectedBalloon!, resizeHandle, screenPos, worldPos, e.Pointer);
                e.Handled = true;
                return;
            }

            var floatingResizeHandle = _editorState.HitTestFloatingImageResizeHandle(screenPos);
            if (floatingResizeHandle != ResizeHandle.None)
            {
                var page = _editorState.Document?.ActivePage;
                var selectedImageId = _editorState.SelectedFloatingImageId;
                if (page != null && selectedImageId.HasValue)
                {
                    var image = page.FindFloatingImage(selectedImageId.Value);
                    if (image != null)
                    {
                        StartFloatingImageResizeDrag(image, floatingResizeHandle, screenPos, worldPos, e.Pointer);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (TryStartTextPathHandleDrag(screenPos, worldPos, e.Pointer))
            {
                e.Handled = true;
                return;
            }

            if (TryHitTailHandle(screenPos, out var tailBalloon, out var tail, out var handleType))
            {
                if (handleType == TailHandleType.Attachment)
                {
                    StartTailAttachmentDrag(tailBalloon, tail, screenPos, worldPos, e.Pointer);
                }
                else
                {
                    StartTailDrag(tailBalloon, tail, screenPos, worldPos, e.Pointer);
                }
                e.Handled = true;
                return;
            }

            if (TryStartRotationDrag(screenPos, worldPos, e.Pointer))
            {
                e.Handled = true;
                return;
            }

            var hitBalloon = _editorState.HitTestBalloon(screenPos);
            if (hitBalloon != null)
            {
                var now = DateTime.Now;
                if (_lastClickedBalloonId == hitBalloon.Id &&
                    (now - _lastClickTime).TotalMilliseconds < DoubleClickTimeMs)
                {
                    _editorState.EnterTextEditMode(hitBalloon.Id);
                    _lastClickedBalloonId = null;
                    MainCanvas.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
                    e.Handled = true;
                    return;
                }

                _lastClickTime = now;
                _lastClickedBalloonId = hitBalloon.Id;

                if (ctrl)
                {
                    _editorState.ToggleBalloonSelection(hitBalloon.Id, preserveFloatingImageSelection: true);
                    e.Handled = true;
                    return;
                }

                if (shift)
                {
                    AddBalloonToSelection(hitBalloon.Id);
                    e.Handled = true;
                    return;
                }

                if (_editorState.SelectedBalloonIds.Contains(hitBalloon.Id))
                {
                    _editorState.SetPrimarySelection(hitBalloon.Id, preserveFloatingImageSelection: _editorState.SelectedFloatingImageIds.Count > 0);
                }
                else
                {
                    _editorState.SelectBalloon(hitBalloon.Id);
                }

                StartBalloonDrag(hitBalloon, screenPos, worldPos, e.Pointer);
                e.Handled = true;
                return;
            }

            var hitFloatingImage = _editorState.HitTestFloatingImage(screenPos);
            if (hitFloatingImage != null)
            {
                _lastClickedBalloonId = null;

                if (ctrl)
                {
                    _editorState.ToggleFloatingImageSelection(hitFloatingImage.Id, preserveBalloonSelection: true);
                    e.Handled = true;
                    return;
                }

                if (shift)
                {
                    AddFloatingImageToSelection(hitFloatingImage.Id);
                    e.Handled = true;
                    return;
                }

                if (_editorState.SelectedFloatingImageIds.Contains(hitFloatingImage.Id))
                {
                    _editorState.SetPrimaryFloatingImageSelection(hitFloatingImage.Id, preserveBalloonSelection: _editorState.SelectedBalloonIds.Count > 0);
                }
                else
                {
                    _editorState.SelectFloatingImage(hitFloatingImage.Id);
                }

                StartFloatingImageDrag(hitFloatingImage, screenPos, worldPos, e.Pointer);
                e.Handled = true;
                return;
            }

            _lastClickedBalloonId = null;
            StartMarqueeSelect(screenPos, e.Pointer, shift || ctrl);
            e.Handled = true;
            return;
        }

    }

    private void AddBalloonToSelection(Guid balloonId)
    {
        var nextIds = _editorState.SelectedBalloonIds.ToHashSet();
        nextIds.Add(balloonId);
        _editorState.SetSelection(nextIds, balloonId, preserveFloatingImageSelection: true);
    }

    private void AddFloatingImageToSelection(Guid imageId)
    {
        var nextIds = _editorState.SelectedFloatingImageIds.ToHashSet();
        nextIds.Add(imageId);
        _editorState.SetFloatingImageSelection(nextIds, imageId, preserveBalloonSelection: true);
    }

    private void AddPanelToSelection(Guid panelId)
    {
        var nextIds = _editorState.SelectedPanelIds.ToHashSet();
        nextIds.Add(panelId);
        _editorState.SetPanelSelection(nextIds, panelId);
    }

    private void CreateBalloonAtPosition(Point2 worldPos)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var targetLayerId = doc.GetPreferredBalloonLayerId();
        if (targetLayerId == Guid.Empty) return;

        Guid? panelId = null;
        var page = doc.ActivePage;
        if (page != null)
        {
            foreach (var panel in page.Panels.OrderByDescending(p => p.Order))
            {
                if (panel.Bounds.Contains(worldPos))
                {
                    panelId = panel.Id;
                    break;
                }
            }
        }

        Guid createdBalloonId;

        if (_activeBalloonTemplateId.HasValue)
        {
            var activeTemplate = doc.FindBalloonTemplate(_activeBalloonTemplateId.Value);
            if (activeTemplate != null)
            {
                var fromTemplate = new CreateBalloonFromTemplateCommand(
                    activeTemplate.Id,
                    targetLayerId,
                    worldPos,
                    usePlaceholderText: true,
                    attachTail: true,
                    panelId: panelId);

                _editorState.Execute(fromTemplate);
                createdBalloonId = fromTemplate.CreatedBalloonId;
                RecordRecentBalloonTemplate(activeTemplate.Id);
            }
            else
            {
                _activeBalloonTemplateId = null;
                RefreshBalloonTemplateControls();
                createdBalloonId = CreateDefaultBalloonAtPosition(targetLayerId, worldPos, panelId);
            }
        }
        else
        {
            createdBalloonId = CreateDefaultBalloonAtPosition(targetLayerId, worldPos, panelId);
        }

        _editorState.SelectBalloon(createdBalloonId);
        _lastCreatedBalloonId = createdBalloonId;
        _lastCreatedPanelId = null;

        var createdBalloon = doc.FindBalloon(createdBalloonId);
        if (createdBalloon != null)
        {
            _lastUsedBalloonShape = createdBalloon.Shape;
            _lastUsedBalloonStyle = createdBalloon.BalloonStyle;
            _lastUsedTextStyle = createdBalloon.TextStyle;
        }

        _editorState.Mode = EditorMode.Select;
        UpdateToolButtonStates();

        MainCanvas.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }

    private Guid CreateDefaultBalloonAtPosition(Guid targetLayerId, Point2 worldPos, Guid? panelId)
    {
        var shape = _lastUsedBalloonShape ?? GetDefaultBalloonShape();
        var balloonStyle = _lastUsedBalloonStyle ?? GetDefaultBalloonStyle();
        var textStyle = _lastUsedTextStyle ?? GetDefaultTextStyle();

        var cmd = new CreateBalloonCommand(
            targetLayerId,
            worldPos,
            "Text",
            shape,
            balloonStyle,
            textStyle,
            panelId: panelId);

        _editorState.Execute(cmd);
        return cmd.CreatedBalloonId;
    }

    private void MainCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(MainCanvas);
        var screenPos = new Point2((float)pointer.Position.X, (float)pointer.Position.Y);
        var delta = screenPos - _lastPointerPosition;

        _isPointerOverCanvas = true;

        if (IsTouchInput(pointer.PointerDeviceType) && _touchTapPending)
        {
            if (Point2.Distance(_touchTapStartScreen, screenPos) > TouchTapThreshold)
            {
                _touchTapMoved = true;
            }
        }

        if (_isPanning)
        {
            _editorState.ViewTransform.Pan(delta);
            _lastPointerPosition = screenPos;
            e.Handled = true;
            return;
        }

        if (_editorState.IsDragging)
        {
            var worldPos = _editorState.ViewTransform.ScreenToWorld(screenPos);
            _editorState.DragCurrentScreen = screenPos;

            switch (_editorState.CurrentDragType)
            {
                case DragType.MoveBalloon:
                    UpdateBalloonDrag(worldPos);
                    break;

                case DragType.MoveFloatingImage:
                    UpdateFloatingImageDrag(worldPos);
                    break;

                case DragType.MoveTailTarget:
                    UpdateTailDrag(worldPos);
                    break;

                case DragType.MoveTailAttachment:
                    UpdateTailAttachmentDrag(worldPos);
                    break;

                case DragType.ResizeBalloon:
                    UpdateResizeDrag(worldPos);
                    break;

                case DragType.ResizeFloatingImage:
                    UpdateFloatingImageResizeDrag(worldPos);
                    break;

                case DragType.RotateBalloon:
                    UpdateRotationDrag(worldPos);
                    break;

                case DragType.TextSelection:
                    UpdateTextSelectionDrag(worldPos);
                    break;

                case DragType.MoveGuide:
                    UpdateGuideDrag(worldPos);
                    break;

                case DragType.CreatePanel:
                    UpdatePanelDrag(worldPos);
                    break;

                case DragType.CreatePanelFreeform:
                    UpdatePanelFreeformDrag(worldPos);
                    break;

                case DragType.MovePanel:
                    UpdatePanelMoveDrag(worldPos);
                    break;

                case DragType.ResizePanel:
                    UpdatePanelResizeDrag(worldPos);
                    break;

                case DragType.MarqueeSelect:
                    UpdateMarqueeDrag(screenPos);
                    break;

                case DragType.EditPanelVertex:
                    UpdatePanelEditVertexDrag(worldPos);
                    break;

                case DragType.MoveTextPathHandle:
                    UpdateTextPathHandleDrag(worldPos);
                    break;
            }

            e.Handled = true;
            return;
        }

        UpdateHoveredPanel(screenPos);
        _lastPointerPosition = screenPos;
        if (_isPanelPolygonDrawing && _editorState.Mode == EditorMode.PanelLayout)
        {
            var doc = _editorState.Document;
            if (doc != null)
            {
                _panelPolygonPreviewPoint = ClampPointToPage(_editorState.ViewTransform.ScreenToWorld(screenPos), doc.Size);
                MainCanvas.Invalidate();
            }
        }
        if (_editorState.Mode == EditorMode.CreateBalloon)
        {
            MainCanvas.Invalidate();
        }
    }

    private void MainCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(MainCanvas);
        if (IsTouchInput(pointer.PointerDeviceType))
        {
            var screenPos = new Point2((float)pointer.Position.X, (float)pointer.Position.Y);
            var worldPos = _editorState.ViewTransform.ScreenToWorld(screenPos);

            if (_touchTapPending && !_touchTapMoved)
            {
                HandleTouchTap(screenPos, worldPos);
            }

            _touchTapPending = false;
            _touchTapMoved = false;
            _isManipulating = false;
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            EndPan();
            e.Handled = true;
            return;
        }

        if (_editorState.IsDragging)
        {
            EndDrag();
            e.Handled = true;
            return;
        }
    }

    private void MainCanvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverCanvas = true;
        var pointer = e.GetCurrentPoint(MainCanvas);
        _lastPointerPosition = new Point2((float)pointer.Position.X, (float)pointer.Position.Y);
        if (_editorState.Mode == EditorMode.CreateBalloon)
        {
            MainCanvas.Invalidate();
        }
    }

    private void MainCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverCanvas = false;
        _editorState.UpdateHoveredPanel(null);
        if (_editorState.Mode == EditorMode.CreateBalloon)
        {
            MainCanvas.Invalidate();
        }
    }

    private void MainCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(MainCanvas);
        var screenPos = new Point2((float)pointer.Position.X, (float)pointer.Position.Y);
        var delta = pointer.Properties.MouseWheelDelta;

        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var factor = delta > 0 ? 1.1f : 0.9f;
            _editorState.ViewTransform.ZoomAt(factor, screenPos);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            _editorState.ViewTransform.Pan(new Point2(delta, 0));
            e.Handled = true;
            return;
        }

        _editorState.ViewTransform.Pan(new Point2(0, delta));
        e.Handled = true;
    }

    private void MainCanvas_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (!IsTouchInput(e.PointerDeviceType)) return;
        _isManipulating = true;
        e.Handled = true;
    }

    private void MainCanvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (!_isManipulating) return;

        var scale = (float)e.Delta.Scale;
        if (MathF.Abs(scale - 1f) > 0.001f)
        {
            var zoomCenter = new Point2((float)e.Position.X, (float)e.Position.Y);
            _editorState.ViewTransform.ZoomAt(scale, zoomCenter);
        }

        var translation = new Point2((float)e.Delta.Translation.X, (float)e.Delta.Translation.Y);
        if (translation.X != 0f || translation.Y != 0f)
        {
            _editorState.ViewTransform.Pan(translation);
        }

        if (_touchTapPending)
        {
            var cumulativeTranslation = new Point2(
                (float)e.Cumulative.Translation.X,
                (float)e.Cumulative.Translation.Y);
            if (cumulativeTranslation.Length > TouchTapThreshold ||
                MathF.Abs((float)e.Cumulative.Scale - 1f) > 0.01f)
            {
                _touchTapMoved = true;
            }
        }

        e.Handled = true;
    }

    private void MainCanvas_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (!IsTouchInput(e.PointerDeviceType)) return;
        _isManipulating = false;
        e.Handled = true;
    }

    private void HandleTouchTap(Point2 screenPos, Point2 worldPos)
    {
        if (_editorState.Mode == EditorMode.EditText)
        {
            var editingBalloon = _editorState.EditingBalloonId.HasValue
                ? _editorState.Document?.FindBalloon(_editorState.EditingBalloonId.Value)
                : null;

            if (editingBalloon == null || !editingBalloon.Bounds.Contains(worldPos))
            {
                _editorState.ExitTextEditMode(saveChanges: true);
            }
            else
            {
                var caretIndex = GetCaretIndexFromPoint(editingBalloon, worldPos);
                _editorState.SetCursorPosition(caretIndex, extendSelection: false);
                return;
            }
        }

        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            return;
        }

        if (_editorState.Mode == EditorMode.CreateBalloon)
        {
            CreateBalloonAtPosition(worldPos);
            return;
        }

        var hitBalloon = _editorState.HitTestBalloon(screenPos);
        if (hitBalloon != null)
        {
            _editorState.SelectBalloon(hitBalloon.Id);
            return;
        }

        var hitFloatingImage = _editorState.HitTestFloatingImage(screenPos);
        if (hitFloatingImage != null)
        {
            _editorState.SelectFloatingImage(hitFloatingImage.Id);
            return;
        }

        _editorState.SelectBalloon(null);
        _editorState.ClearFloatingImageSelection();
    }

    private static bool IsTouchInput(PointerDeviceType deviceType)
    {
        return deviceType == PointerDeviceType.Touch || deviceType == PointerDeviceType.Pen;
    }

    private void StartPan(Point2 screenPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _isPanning = true;
        _lastPointerPosition = screenPos;
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void EndPan()
    {
        _isPanning = false;
        MainCanvas.ReleasePointerCaptures();
    }

    private void StartBalloonDrag(Balloon balloon, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveBalloon;
        _moveDragBaselinePending = true;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        _editorState.DragBalloonOriginalPosition = balloon.Position;
        _editorState.DragBalloonOriginalPositions.Clear();
        _editorState.DragTailOriginalTargets.Clear();
        _editorState.DragFloatingImageOriginalBoundsMap.Clear();
        _editorState.DragFloatingImageId = null;
        _editorState.DragFloatingImageOriginalBounds = default;

        if (_editorState.Document != null)
        {
            var selectedIds = _editorState.SelectedBalloonIds;
            if (selectedIds.Count > 0)
            {
                foreach (var id in selectedIds)
                {
                    var selectedBalloon = _editorState.Document.FindBalloon(id);
                    if (selectedBalloon != null)
                    {
                        _editorState.DragBalloonOriginalPositions[id] = selectedBalloon.Position;
                        foreach (var tail in selectedBalloon.Tails)
                        {
                            _editorState.DragTailOriginalTargets[(id, tail.Id)] = tail.TargetPoint;
                        }
                    }
                }
            }
            else
            {
                _editorState.DragBalloonOriginalPositions[balloon.Id] = balloon.Position;
                foreach (var tail in balloon.Tails)
                {
                    _editorState.DragTailOriginalTargets[(balloon.Id, tail.Id)] = tail.TargetPoint;
                }
            }

            var page = _editorState.Document.ActivePage;
            if (page != null)
            {
                foreach (var id in _editorState.SelectedFloatingImageIds)
                {
                    var image = page.FindFloatingImage(id);
                    if (image == null) continue;
                    _editorState.DragFloatingImageOriginalBoundsMap[id] = image.Bounds;
                }

                if (_editorState.DragFloatingImageOriginalBoundsMap.Count > 0)
                {
                    var primaryImageId = _editorState.SelectedFloatingImageId;
                    if (!primaryImageId.HasValue || !_editorState.DragFloatingImageOriginalBoundsMap.ContainsKey(primaryImageId.Value))
                    {
                        primaryImageId = _editorState.DragFloatingImageOriginalBoundsMap.Keys.First();
                    }

                    _editorState.DragFloatingImageId = primaryImageId;
                    _editorState.DragFloatingImageOriginalBounds = _editorState.DragFloatingImageOriginalBoundsMap[primaryImageId!.Value];
                }
            }
        }

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartPanelDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.CreatePanel;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = ClampPointToPage(worldPos, doc.Size);
        _editorState.DragCurrentScreen = screenPos;

        _panelPreviewBounds = new Rect(_editorState.DragStartWorld.X, _editorState.DragStartWorld.Y, 0, 0);
        MainCanvas.Invalidate();

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void UpdatePanelDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        _panelPreviewBounds = BuildPanelRect(_editorState.DragStartWorld, worldPos, doc.Size);
        MainCanvas.Invalidate();
    }

    private void HandlePanelPolygonClick(Point2 screenPos, Point2 worldPos)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        var clamped = ClampPointToPage(worldPos, doc.Size);
        var now = DateTime.Now;
        if (!_isPanelPolygonDrawing)
        {
            _panelPolygonPoints.Clear();
            _panelPolygonPoints.Add(clamped);
            _panelPolygonPreviewPoint = clamped;
            _isPanelPolygonDrawing = true;
            _lastPanelPolygonClickTime = now;
            _lastPanelPolygonClickScreen = screenPos;
            _panelPreviewBounds = null;
            MainCanvas.Invalidate();
            return;
        }

        if (_panelPolygonPoints.Count >= 3 &&
            _lastPanelPolygonClickScreen.HasValue &&
            (now - _lastPanelPolygonClickTime).TotalMilliseconds <= DoubleClickTimeMs &&
            Point2.Distance(_lastPanelPolygonClickScreen.Value, screenPos) <= 12f)
        {
            FinishPanelPolygon();
            return;
        }

        if (_panelPolygonPoints.Count >= 3)
        {
            var firstScreen = _editorState.ViewTransform.WorldToScreen(_panelPolygonPoints[0]);
            if (Point2.Distance(firstScreen, screenPos) <= 10f)
            {
                FinishPanelPolygon();
                return;
            }
        }

        _panelPolygonPoints.Add(clamped);
        _panelPolygonPreviewPoint = clamped;
        _lastPanelPolygonClickTime = now;
        _lastPanelPolygonClickScreen = screenPos;
        MainCanvas.Invalidate();
    }

    private void FinishPanelPolygon()
    {
        if (_panelPolygonPoints.Count < 3)
        {
            CancelPanelDrawing();
            return;
        }

        CreateCustomPanelFromPoints(_panelPolygonPoints, smooth: false);
        CancelPanelDrawing();
    }

    private void StartPanelFreeformDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.CreatePanelFreeform;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = ClampPointToPage(worldPos, doc.Size);

        _panelFreeformPoints.Clear();
        _panelFreeformPoints.Add(_editorState.DragStartWorld);
        _isPanelFreeformDrawing = true;
        _panelPreviewBounds = null;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }

        MainCanvas.Invalidate();
    }

    private void UpdatePanelFreeformDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        var clamped = ClampPointToPage(worldPos, doc.Size);
        var constrainVertical = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (constrainVertical && _panelFreeformPoints.Count > 0)
        {
            clamped = new Point2(_panelFreeformPoints[0].X, clamped.Y);
        }

        if (_panelFreeformPoints.Count == 0 ||
            Point2.Distance(_panelFreeformPoints[^1], clamped) > 3f)
        {
            _panelFreeformPoints.Add(clamped);
            MainCanvas.Invalidate();
        }
    }

    private void EndPanelFreeformDrag()
    {
        if (_panelFreeformPoints.Count < 3)
        {
            CancelPanelDrawing();
            return;
        }

        CreateCustomPanelFromPoints(_panelFreeformPoints, smooth: true);
        CancelPanelDrawing();
    }

    private void CreateCustomPanelFromPoints(IReadOnlyList<Point2> points, bool smooth)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var bounds = ComputeBoundsFromPoints(points);
        bounds = ClampRectToPage(bounds, doc.Size);
        bounds = EnsureMinimumPanelBounds(bounds);
        if (bounds.Width < PanelMinSize || bounds.Height < PanelMinSize) return;

        var pathData = smooth
            ? BuildSmoothPanelPathData(points, bounds)
            : BuildCustomPanelPathData(points, bounds);
        var nextOrder = page.Panels.Count + 1;
        var panelName = $"Panel {nextOrder}";

        var cmd = new CreatePanelZoneCommand(
            page.Id,
            panelName,
            bounds,
            nextOrder,
            safeMargin: GetPanelDefaultSafeMargin(),
            borderColor: GetPanelDefaultBorderColor(),
            borderWidth: GetPanelDefaultBorderWidth(),
            borderStyle: GetPanelDefaultBorderStyle(),
            shape: PanelShape.Custom,
            customShapePathData: pathData);

        _editorState.Execute(cmd);
        var createdPanel = page.FindPanel(cmd.CreatedPanelId);
        if (createdPanel != null)
        {
            _lastCreatedPanelId = createdPanel.Id;
            _lastCreatedBalloonId = null;
            _editorState.SelectPanel(createdPanel.Id);
        }

        SetStatusMessage(LF("input.status.created_panel_named", panelName));
    }

    private static Rect ComputeBoundsFromPoints(IReadOnlyList<Point2> points)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        foreach (var point in points)
        {
            minX = MathF.Min(minX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxX = MathF.Max(maxX, point.X);
            maxY = MathF.Max(maxY, point.Y);
        }

        return new Rect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
    }

    private static Rect EnsureMinimumPanelBounds(Rect bounds)
    {
        var width = MathF.Max(bounds.Width, PanelMinSize);
        var height = MathF.Max(bounds.Height, PanelMinSize);
        var center = bounds.Center;
        return new Rect(center.X - width / 2f, center.Y - height / 2f, width, height);
    }

    private static Rect ClampRectToPage(Rect rect, Size2 pageSize)
    {
        var left = Math.Clamp(rect.Left, 0f, pageSize.Width);
        var top = Math.Clamp(rect.Top, 0f, pageSize.Height);
        var right = Math.Clamp(rect.Right, 0f, pageSize.Width);
        var bottom = Math.Clamp(rect.Bottom, 0f, pageSize.Height);
        return Rect.FromCorners(new Point2(left, top), new Point2(right, bottom));
    }

    private static string BuildCustomPanelPathData(IReadOnlyList<Point2> points, Rect bounds)
    {
        if (points.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var first = points[0];
        AppendPathCommand(sb, "M", first.X - bounds.X, first.Y - bounds.Y);
        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
            AppendPathCommand(sb, "L", point.X - bounds.X, point.Y - bounds.Y);
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static string BuildSmoothPanelPathData(IReadOnlyList<Point2> points, Rect bounds)
    {
        if (points.Count < 3) return BuildCustomPanelPathData(points, bounds);

        var preparedPoints = PrepareSmoothPanelPoints(points);
        if (preparedPoints.Count < 3) return BuildCustomPanelPathData(preparedPoints, bounds);

        var sb = new StringBuilder();
        var count = preparedPoints.Count;
        var first = preparedPoints[0];
        AppendPathCommand(sb, "M", first.X - bounds.X, first.Y - bounds.Y);

        for (var i = 0; i < count; i++)
        {
            var p0 = preparedPoints[(i - 1 + count) % count];
            var p1 = preparedPoints[i];
            var p2 = preparedPoints[(i + 1) % count];
            var p3 = preparedPoints[(i + 2) % count];

            var c1 = p1 + (p2 - p0) / 6f;
            var c2 = p2 - (p3 - p1) / 6f;

            AppendPathCommand(sb, "C",
                c1.X - bounds.X, c1.Y - bounds.Y,
                c2.X - bounds.X, c2.Y - bounds.Y,
                p2.X - bounds.X, p2.Y - bounds.Y);
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static List<Point2> PrepareSmoothPanelPoints(IReadOnlyList<Point2> points)
    {
        var uniquePoints = new List<Point2>(points.Count);
        foreach (var point in points)
        {
            if (uniquePoints.Count == 0 || Point2.Distance(uniquePoints[^1], point) >= 2f)
            {
                uniquePoints.Add(point);
            }
        }

        if (uniquePoints.Count < 3)
        {
            return uniquePoints;
        }

        var working = uniquePoints;
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var smoothed = new List<Point2>(working.Count * 2);
            for (var i = 0; i < working.Count; i++)
            {
                var current = working[i];
                var next = working[(i + 1) % working.Count];
                var q = (current * 0.75f) + (next * 0.25f);
                var r = (current * 0.25f) + (next * 0.75f);
                smoothed.Add(q);
                smoothed.Add(r);
            }
            working = smoothed;
        }

        return working;
    }

    private static void AppendPathCommand(StringBuilder sb, string command, float x, float y)
    {
        sb.Append(command)
            .Append(' ')
            .Append(x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ');
    }

    private static void AppendPathCommand(StringBuilder sb, string command, float x1, float y1, float x2, float y2, float x, float y)
    {
        sb.Append(command)
            .Append(' ')
            .Append(x1.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(y1.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(x2.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(y2.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
            .Append(' ');
    }

    private void CancelPanelDrawing()
    {
        _panelPolygonPoints.Clear();
        _panelPolygonPreviewPoint = null;
        _isPanelPolygonDrawing = false;
        _lastPanelPolygonClickScreen = null;
        _panelFreeformPoints.Clear();
        _isPanelFreeformDrawing = false;
        _panelPreviewBounds = null;
        MainCanvas.Invalidate();
    }

    private string GetPanelToolHintText(bool inDrawMode)
    {
        if (_isEditingPanelShape)
        {
            return "Editing shape: drag points, Enter to apply, Esc to cancel";
        }

        if (!inDrawMode)
        {
            return "Click to select panels";
        }

        return _selectedPanelDrawTool switch
        {
            PanelDrawTool.Polygon => "Click to add points, double-click to finish or click the first point",
            PanelDrawTool.Freeform => "Drag to draw freeform panel (hold Shift to constrain vertical)",
            _ => "Drag to draw panels"
        };
    }

    private void EnterPanelShapeEdit(PanelZone panel)
    {
        if (panel.Shape != PanelShape.Custom || string.IsNullOrWhiteSpace(panel.CustomShapePathData))
        {
            SetStatusMessage(L("input.status.no_editable_shape"));
            return;
        }

        if (!TryParsePanelPathData(panel.CustomShapePathData, out var anchors, out var closed) || anchors.Count < 2)
        {
            SetStatusMessage(L("input.status.no_editable_points"));
            return;
        }

        foreach (var anchor in anchors)
        {
            anchor.Position = new Point2(anchor.Position.X + panel.Bounds.X, anchor.Position.Y + panel.Bounds.Y);
            if (anchor.InHandle.HasValue)
            {
                var handle = anchor.InHandle.Value;
                anchor.InHandle = new Point2(handle.X + panel.Bounds.X, handle.Y + panel.Bounds.Y);
            }
            if (anchor.OutHandle.HasValue)
            {
                var handle = anchor.OutHandle.Value;
                anchor.OutHandle = new Point2(handle.X + panel.Bounds.X, handle.Y + panel.Bounds.Y);
            }
        }

        _editingPanelAnchors = anchors;
        _editingPanelClosed = closed;
        _editingPanelOriginalBounds = panel.Bounds;
        _editingPanelOriginalPathData = panel.CustomShapePathData;
        _editingPanelId = panel.Id;
        _editingPanelHandleAnchorIndex = -1;
        _editingPanelHandleType = PanelEditHandleType.Anchor;
        _isEditingPanelShape = true;
        CancelPanelDrawing();
        UpdatePanelToolButtonStates();
        MainCanvas.Invalidate();
        SetStatusMessage(L("input.status.editing_shape"));
    }

    private void CommitPanelShapeEdit()
    {
        if (!_isEditingPanelShape || !_editingPanelId.HasValue) return;

        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null)
        {
            CancelPanelShapeEdit();
            return;
        }

        var panel = page.FindPanel(_editingPanelId.Value);
        if (panel == null)
        {
            CancelPanelShapeEdit();
            return;
        }

        var newBounds = panel.Bounds;
        var newPath = panel.CustomShapePathData;
        panel.SetBounds(_editingPanelOriginalBounds);
        panel.SetCustomShapePathData(_editingPanelOriginalPathData);
        panel.SetShape(PanelShape.Custom);

        var commands = new List<ICommand>();
        if (newBounds != _editingPanelOriginalBounds)
        {
            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds, moveBalloons: false));
        }
        if (!string.Equals(newPath, _editingPanelOriginalPathData, StringComparison.Ordinal))
        {
            commands.Add(new SetPanelZoneShapeCommand(page.Id, panel.Id, PanelShape.Custom, customShapePathData: newPath));
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            _editorState.ExecuteTransaction("Edit panel shape", commands);
        }

        ExitPanelShapeEditMode();
        MainCanvas.Invalidate();
    }

    private void CancelPanelShapeEdit()
    {
        if (!_isEditingPanelShape || !_editingPanelId.HasValue) return;

        var page = _editorState.Document?.ActivePage;
        var panel = page?.FindPanel(_editingPanelId.Value);
        if (panel != null)
        {
            panel.SetBounds(_editingPanelOriginalBounds);
            panel.SetCustomShapePathData(_editingPanelOriginalPathData);
            panel.SetShape(PanelShape.Custom);
        }

        ExitPanelShapeEditMode();
        MainCanvas.Invalidate();
        SetStatusMessage(L("input.status.cancelled_shape"));
    }

    private void ExitPanelShapeEditMode()
    {
        _isEditingPanelShape = false;
        _editingPanelId = null;
        _editingPanelHandleAnchorIndex = -1;
        _editingPanelAnchors = new List<PanelPathAnchor>();
        UpdatePanelToolButtonStates();
    }

    private bool TryStartPanelVertexDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        if (!_isEditingPanelShape || _editingPanelAnchors.Count == 0) return false;

        var handle = HitTestPanelEditHandle(screenPos);
        if (!handle.HasValue) return false;

        _editingPanelHandleAnchorIndex = handle.Value.AnchorIndex;
        _editingPanelHandleType = handle.Value.HandleType;
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.EditPanelVertex;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }

        return true;
    }

    private (int AnchorIndex, PanelEditHandleType HandleType)? HitTestPanelEditHandle(Point2 screenPos)
    {
        var threshold = PanelVertexHandleSize * 2f;
        var thresholdSq = threshold * threshold;

        for (var i = 0; i < _editingPanelAnchors.Count; i++)
        {
            var anchor = _editingPanelAnchors[i];
            if (anchor.InHandle.HasValue)
            {
                var handleScreen = _editorState.ViewTransform.WorldToScreen(anchor.InHandle.Value);
                if (Point2.DistanceSquared(screenPos, handleScreen) <= thresholdSq)
                {
                    return (i, PanelEditHandleType.InHandle);
                }
            }

            if (anchor.OutHandle.HasValue)
            {
                var handleScreen = _editorState.ViewTransform.WorldToScreen(anchor.OutHandle.Value);
                if (Point2.DistanceSquared(screenPos, handleScreen) <= thresholdSq)
                {
                    return (i, PanelEditHandleType.OutHandle);
                }
            }

            var screenPoint = _editorState.ViewTransform.WorldToScreen(anchor.Position);
            if (Point2.DistanceSquared(screenPos, screenPoint) <= thresholdSq)
            {
                return (i, PanelEditHandleType.Anchor);
            }
        }

        return null;
    }

    private void UpdatePanelEditVertexDrag(Point2 worldPos)
    {
        if (!_isEditingPanelShape || _editingPanelHandleAnchorIndex < 0) return;

        var doc = _editorState.Document;
        if (doc == null) return;

        var clamped = ClampPointToPage(worldPos, doc.Size);
        var anchor = _editingPanelAnchors[_editingPanelHandleAnchorIndex];
        switch (_editingPanelHandleType)
        {
            case PanelEditHandleType.Anchor:
                var delta = clamped - anchor.Position;
                anchor.Position = clamped;
                if (anchor.InHandle.HasValue)
                {
                    anchor.InHandle = anchor.InHandle.Value + delta;
                }
                if (anchor.OutHandle.HasValue)
                {
                    anchor.OutHandle = anchor.OutHandle.Value + delta;
                }
                break;
            case PanelEditHandleType.InHandle:
                anchor.InHandle = clamped;
                break;
            case PanelEditHandleType.OutHandle:
                anchor.OutHandle = clamped;
                break;
        }

        _editingPanelAnchors[_editingPanelHandleAnchorIndex] = anchor;
        UpdatePanelEditPreview();
    }

    private void EndPanelEditVertexDrag()
    {
        _editingPanelHandleAnchorIndex = -1;
    }

    private void UpdatePanelEditPreview()
    {
        if (!_editingPanelId.HasValue) return;
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var panel = page.FindPanel(_editingPanelId.Value);
        if (panel == null) return;

        var bounds = ComputeBoundsFromAnchors(_editingPanelAnchors);
        bounds = ClampRectToPage(bounds, _editorState.Document!.Size);
        bounds = EnsureMinimumPanelBounds(bounds);
        if (bounds.Width < PanelMinSize || bounds.Height < PanelMinSize) return;

        var pathData = BuildPanelPathDataFromAnchors(_editingPanelAnchors, bounds, _editingPanelClosed);
        panel.SetBounds(bounds);
        panel.SetShape(PanelShape.Custom);
        panel.SetCustomShapePathData(pathData);
        MainCanvas.Invalidate();
    }

    private static Rect ComputeBoundsFromAnchors(IReadOnlyList<PanelPathAnchor> anchors)
    {
        if (anchors.Count == 0) return new Rect(0, 0, 0, 0);

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        void IncludePoint(Point2 point)
        {
            minX = MathF.Min(minX, point.X);
            minY = MathF.Min(minY, point.Y);
            maxX = MathF.Max(maxX, point.X);
            maxY = MathF.Max(maxY, point.Y);
        }

        foreach (var anchor in anchors)
        {
            IncludePoint(anchor.Position);
            if (anchor.InHandle.HasValue) IncludePoint(anchor.InHandle.Value);
            if (anchor.OutHandle.HasValue) IncludePoint(anchor.OutHandle.Value);
        }

        return new Rect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
    }

    private static string BuildPanelPathDataFromAnchors(IReadOnlyList<PanelPathAnchor> anchors, Rect bounds, bool closed)
    {
        if (anchors.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        var first = anchors[0];
        AppendPathCommand(sb, "M", first.Position.X - bounds.X, first.Position.Y - bounds.Y);

        for (var i = 0; i < anchors.Count - 1; i++)
        {
            AppendPanelSegment(sb, anchors[i], anchors[i + 1], bounds);
        }

        if (closed && anchors.Count > 2)
        {
            AppendPanelSegment(sb, anchors[^1], anchors[0], bounds);
            sb.Append(" Z");
        }

        return sb.ToString();
    }

    private static void AppendPanelSegment(StringBuilder sb, PanelPathAnchor start, PanelPathAnchor end, Rect bounds)
    {
        if (start.OutHandle.HasValue || end.InHandle.HasValue)
        {
            var c1 = start.OutHandle ?? start.Position;
            var c2 = end.InHandle ?? end.Position;
            AppendPathCommand(sb, "C",
                c1.X - bounds.X, c1.Y - bounds.Y,
                c2.X - bounds.X, c2.Y - bounds.Y,
                end.Position.X - bounds.X, end.Position.Y - bounds.Y);
        }
        else
        {
            AppendPathCommand(sb, "L", end.Position.X - bounds.X, end.Position.Y - bounds.Y);
        }
    }

    private static bool TryParsePanelPathData(string pathData, out List<PanelPathAnchor> anchors, out bool closed)
    {
        anchors = new List<PanelPathAnchor>();
        closed = false;
        if (string.IsNullOrWhiteSpace(pathData)) return false;

        var index = 0;
        var command = '\0';
        var current = Point2.Zero;
        var start = Point2.Zero;

        bool ReadNumber(out float value)
        {
            value = 0f;
            while (index < pathData.Length && (char.IsWhiteSpace(pathData[index]) || pathData[index] == ','))
            {
                index++;
            }
            if (index >= pathData.Length) return false;

            var startIndex = index;
            if (pathData[index] == '+' || pathData[index] == '-')
            {
                index++;
            }
            var hasDot = false;
            var hasExp = false;
            while (index < pathData.Length)
            {
                var c = pathData[index];
                if (char.IsDigit(c))
                {
                    index++;
                    continue;
                }
                if (c == '.' && !hasDot && !hasExp)
                {
                    hasDot = true;
                    index++;
                    continue;
                }
                if ((c == 'e' || c == 'E') && !hasExp)
                {
                    hasExp = true;
                    index++;
                    if (index < pathData.Length && (pathData[index] == '+' || pathData[index] == '-'))
                    {
                        index++;
                    }
                    continue;
                }
                break;
            }

            if (startIndex == index) return false;
            var token = pathData.Substring(startIndex, index - startIndex);
            return float.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        bool ReadPoint(bool relative, out Point2 point)
        {
            point = default;
            if (!ReadNumber(out var x) || !ReadNumber(out var y)) return false;
            point = relative ? new Point2(current.X + x, current.Y + y) : new Point2(x, y);
            return true;
        }

        while (index < pathData.Length)
        {
            while (index < pathData.Length && char.IsWhiteSpace(pathData[index]))
            {
                index++;
            }
            if (index >= pathData.Length) break;

            var c = pathData[index];
            if (char.IsLetter(c))
            {
                command = c;
                index++;
            }
            else if (command == '\0')
            {
                return false;
            }

            switch (command)
            {
                case 'M':
                case 'm':
                {
                    var relative = command == 'm';
                    if (!ReadPoint(relative, out var movePoint)) return false;
                    current = movePoint;
                    start = movePoint;
                    anchors.Add(new PanelPathAnchor(movePoint));

                    while (ReadPoint(relative, out var linePoint))
                    {
                        anchors.Add(new PanelPathAnchor(linePoint));
                        current = linePoint;
                    }
                    break;
                }
                case 'L':
                case 'l':
                {
                    var relative = command == 'l';
                    while (ReadPoint(relative, out var linePoint))
                    {
                        anchors.Add(new PanelPathAnchor(linePoint));
                        current = linePoint;
                    }
                    break;
                }
                case 'C':
                case 'c':
                {
                    var relative = command == 'c';
                    while (true)
                    {
                        if (!ReadPoint(relative, out var c1) ||
                            !ReadPoint(relative, out var c2) ||
                            !ReadPoint(relative, out var end))
                        {
                            break;
                        }

                        if (anchors.Count > 0)
                        {
                            anchors[^1].OutHandle = c1;
                        }

                        if (anchors.Count > 0 && Point2.DistanceSquared(end, start) < 0.25f)
                        {
                            anchors[0].InHandle = c2;
                            current = start;
                        }
                        else
                        {
                            var anchor = new PanelPathAnchor(end)
                            {
                                InHandle = c2
                            };
                            anchors.Add(anchor);
                            current = end;
                        }
                    }
                    break;
                }
                case 'Z':
                case 'z':
                    closed = true;
                    current = start;
                    break;
                default:
                    return false;
            }
        }

        return anchors.Count > 0;
    }

    private static Point2 ClampPointToPage(Point2 point, Size2 pageSize)
    {
        return new Point2(
            Math.Clamp(point.X, 0f, pageSize.Width),
            Math.Clamp(point.Y, 0f, pageSize.Height));
    }

    private static Rect BuildPanelRect(Point2 start, Point2 end, Size2 pageSize)
    {
        var clampedStart = ClampPointToPage(start, pageSize);
        var clampedEnd = ClampPointToPage(end, pageSize);

        var left = MathF.Min(clampedStart.X, clampedEnd.X);
        var top = MathF.Min(clampedStart.Y, clampedEnd.Y);
        var right = MathF.Max(clampedStart.X, clampedEnd.X);
        var bottom = MathF.Max(clampedStart.Y, clampedEnd.Y);

        return new Rect(left, top, right - left, bottom - top);
    }

    private void StartPanelMoveDrag(PanelZone panel, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MovePanel;
        _panelMoveDragBaselinePending = true;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragPanelId = panel.Id;
        _editorState.DragPanelOriginalBounds = panel.Bounds;
        _editorState.DragPanelOriginalBoundsMap.Clear();

        var selectedIds = _editorState.SelectedPanelIds;
        if (selectedIds.Count > 1)
        {
            foreach (var id in selectedIds)
            {
                var selectedPanel = doc.ActivePage.FindPanel(id);
                if (selectedPanel != null)
                {
                    _editorState.DragPanelOriginalBoundsMap[id] = selectedPanel.Bounds;
                }
            }
        }
        else
        {
            _editorState.DragPanelOriginalBoundsMap[panel.Id] = panel.Bounds;
        }

        if (!_editorState.DragPanelOriginalBoundsMap.ContainsKey(panel.Id))
        {
            _editorState.DragPanelOriginalBoundsMap[panel.Id] = panel.Bounds;
        }

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartFloatingImageDrag(FloatingImage image, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveFloatingImage;
        _moveDragBaselinePending = true;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragFloatingImageOriginalBoundsMap.Clear();
        _editorState.DragBalloonOriginalPositions.Clear();
        _editorState.DragTailOriginalTargets.Clear();

        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc != null && page != null)
        {
            var selectedImageIds = _editorState.SelectedFloatingImageIds;
            if (selectedImageIds.Count > 0)
            {
                foreach (var selectedImageId in selectedImageIds)
                {
                    var selectedImage = page.FindFloatingImage(selectedImageId);
                    if (selectedImage == null) continue;
                    _editorState.DragFloatingImageOriginalBoundsMap[selectedImageId] = selectedImage.Bounds;
                }
            }
            else
            {
                _editorState.DragFloatingImageOriginalBoundsMap[image.Id] = image.Bounds;
            }

            if (!_editorState.DragFloatingImageOriginalBoundsMap.ContainsKey(image.Id))
            {
                _editorState.DragFloatingImageOriginalBoundsMap[image.Id] = image.Bounds;
            }

            foreach (var selectedBalloonId in _editorState.SelectedBalloonIds)
            {
                var selectedBalloon = doc.FindBalloon(selectedBalloonId);
                if (selectedBalloon == null) continue;

                _editorState.DragBalloonOriginalPositions[selectedBalloonId] = selectedBalloon.Position;
                foreach (var tail in selectedBalloon.Tails)
                {
                    _editorState.DragTailOriginalTargets[(selectedBalloonId, tail.Id)] = tail.TargetPoint;
                }
            }
        }

        _editorState.DragFloatingImageId = image.Id;
        _editorState.DragFloatingImageOriginalBounds = _editorState.DragFloatingImageOriginalBoundsMap.TryGetValue(image.Id, out var originalBounds)
            ? originalBounds
            : image.Bounds;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartFloatingImageResizeDrag(FloatingImage image, ResizeHandle handle, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.ResizeFloatingImage;
        _editorState.CurrentResizeHandle = handle;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragFloatingImageOriginalBoundsMap.Clear();
        _editorState.DragFloatingImageOriginalBoundsMap[image.Id] = image.Bounds;
        _editorState.DragFloatingImageId = image.Id;
        _editorState.DragFloatingImageOriginalBounds = image.Bounds;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void UpdatePanelMoveDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || !_editorState.DragPanelId.HasValue) return;

        if (_panelMoveDragBaselinePending)
        {
            _panelMoveDragBaselinePending = false;
            _editorState.DragStartWorld = worldPos;
            return;
        }

        var panel = page.FindPanel(_editorState.DragPanelId.Value);
        if (panel == null) return;

        var delta = worldPos - _editorState.DragStartWorld;
        var effectiveDelta = delta;

        var originalMap = _editorState.DragPanelOriginalBoundsMap;
        if (originalMap.Count == 0)
        {
            originalMap[panel.Id] = _editorState.DragPanelOriginalBounds;
        }

        if (IsGuideSnappingActive())
        {
            var primaryId = _editorState.SelectedPanelId ?? panel.Id;
            if (originalMap.TryGetValue(primaryId, out var primaryOriginal))
            {
                var primaryBounds = new Rect(
                    primaryOriginal.X + effectiveDelta.X,
                    primaryOriginal.Y + effectiveDelta.Y,
                    primaryOriginal.Width,
                    primaryOriginal.Height);

                var snap = GetPanelMoveSnapResult(primaryBounds, primaryId, page, originalMap.Keys);
                effectiveDelta = new Point2(effectiveDelta.X + snap.Offset.X, effectiveDelta.Y + snap.Offset.Y);
                _editorState.SetSmartGuides(snap.Guides);
                UpdateSnapFeedback(snap.Offset, primaryBounds.Offset(snap.Offset).Center, snap.Guides);
            }
            else
            {
                _editorState.ClearSmartGuides();
                ClearSnapFeedback();
            }
        }
        else
        {
            _editorState.ClearSmartGuides();
            ClearSnapFeedback();
        }

        var selectionBounds = GetPanelBoundsWithDelta(originalMap.Values, effectiveDelta);
        var clampOffset = ClampPanelDeltaToPage(selectionBounds, doc.Size, effectiveDelta);
        effectiveDelta = new Point2(effectiveDelta.X + clampOffset.X, effectiveDelta.Y + clampOffset.Y);

        if (Math.Abs(clampOffset.X) > 0.01f || Math.Abs(clampOffset.Y) > 0.01f)
        {
            _editorState.ClearSmartGuides();
            ClearSnapFeedback();
        }

        foreach (var entry in originalMap)
        {
            var movingPanel = page.FindPanel(entry.Key);
            if (movingPanel == null) continue;

            var originalBounds = entry.Value;
            movingPanel.SetBounds(new Rect(
                originalBounds.X + effectiveDelta.X,
                originalBounds.Y + effectiveDelta.Y,
                originalBounds.Width,
                originalBounds.Height));
        }

        MainCanvas.Invalidate();
    }

    private void EndPanelMoveDrag()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null || !_editorState.DragPanelId.HasValue) return;

        var commands = new List<ICommand>();
        var originalMap = _editorState.DragPanelOriginalBoundsMap;
        if (originalMap.Count == 0)
        {
            originalMap[_editorState.DragPanelId.Value] = _editorState.DragPanelOriginalBounds;
        }

        foreach (var entry in originalMap)
        {
            var panel = page.FindPanel(entry.Key);
            if (panel == null) continue;

            var newBounds = panel.Bounds;
            var originalBounds = entry.Value;

            panel.SetBounds(originalBounds);

            if (newBounds != originalBounds)
            {
                commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
            }
        }

        ExecutePanelBoundsCommands("Move panels", commands);
    }

    private static Rect GetPanelBoundsWithDelta(IEnumerable<Rect> bounds, Point2 delta)
    {
        var enumerated = bounds.ToList();
        if (enumerated.Count == 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        var first = enumerated[0];
        var union = new Rect(
            first.X + delta.X,
            first.Y + delta.Y,
            first.Width,
            first.Height);

        for (int i = 1; i < enumerated.Count; i++)
        {
            var boundsRect = enumerated[i];
            var moved = new Rect(
                boundsRect.X + delta.X,
                boundsRect.Y + delta.Y,
                boundsRect.Width,
                boundsRect.Height);
            union = union.Union(moved);
        }

        return union;
    }

    private static Point2 ClampPanelDeltaToPage(Rect selectionBounds, Size2 pageSize, Point2 delta)
    {
        var offsetX = 0f;
        var offsetY = 0f;

        if (selectionBounds.Left < 0f)
        {
            offsetX = -selectionBounds.Left;
        }
        else if (selectionBounds.Right > pageSize.Width)
        {
            offsetX = pageSize.Width - selectionBounds.Right;
        }

        if (selectionBounds.Top < 0f)
        {
            offsetY = -selectionBounds.Top;
        }
        else if (selectionBounds.Bottom > pageSize.Height)
        {
            offsetY = pageSize.Height - selectionBounds.Bottom;
        }

        return new Point2(offsetX, offsetY);
    }

    private void StartPanelResizeDrag(ResizeHandle handle, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null || !_editorState.SelectedPanelId.HasValue) return;

        var panel = page.FindPanel(_editorState.SelectedPanelId.Value);
        if (panel == null) return;

        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.ResizePanel;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragPanelId = panel.Id;
        _editorState.DragPanelOriginalBounds = panel.Bounds;
        _editorState.CurrentResizeHandle = handle;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void UpdatePanelResizeDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || !_editorState.DragPanelId.HasValue) return;

        var panel = page.FindPanel(_editorState.DragPanelId.Value);
        if (panel == null) return;

        var original = _editorState.DragPanelOriginalBounds;
        var handle = _editorState.CurrentResizeHandle;

        float? aspectRatio = null;
        if (TryGetPanelAspectRatio(panel, out var ratio))
        {
            aspectRatio = ratio;
        }

        var shiftHeld = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (shiftHeld && !aspectRatio.HasValue && original.Width > 0 && original.Height > 0)
        {
            aspectRatio = original.Width / original.Height;
        }

        var newBounds = ComputeResizedBounds(original, worldPos, handle, doc.Size, aspectRatio, clampToPage: true);
        if (newBounds.Width >= PanelMinSize && newBounds.Height >= PanelMinSize)
        {
            panel.SetBounds(newBounds);
            MainCanvas.Invalidate();
        }
    }

    private void UpdateFloatingImageResizeDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || !_editorState.DragFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.DragFloatingImageId.Value);
        if (image == null) return;

        var original = _editorState.DragFloatingImageOriginalBounds;
        var handle = _editorState.CurrentResizeHandle;

        var shiftHeld = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        float? aspectRatio = null;
        if (shiftHeld && original.Width > 0 && original.Height > 0)
        {
            aspectRatio = original.Width / original.Height;
        }

        var newBounds = ComputeResizedBounds(original, worldPos, handle, doc.Size, aspectRatio, clampToPage: false);
        if (newBounds.Width >= PanelMinSize && newBounds.Height >= PanelMinSize)
        {
            image.SetBounds(newBounds);
            MainCanvas.Invalidate();
        }
    }

    private static Rect ComputeResizedBounds(Rect original, Point2 worldPos, ResizeHandle handle, Size2 pageSize, float? aspectRatio, bool clampToPage)
    {
        var left = original.Left;
        var top = original.Top;
        var right = original.Right;
        var bottom = original.Bottom;

        switch (handle)
        {
            case ResizeHandle.TopLeft:
                left = clampToPage
                    ? Math.Clamp(worldPos.X, 0f, right - PanelMinSize)
                    : Math.Min(worldPos.X, right - PanelMinSize);
                top = clampToPage
                    ? Math.Clamp(worldPos.Y, 0f, bottom - PanelMinSize)
                    : Math.Min(worldPos.Y, bottom - PanelMinSize);
                break;
            case ResizeHandle.TopRight:
                right = clampToPage
                    ? Math.Clamp(worldPos.X, left + PanelMinSize, pageSize.Width)
                    : Math.Max(worldPos.X, left + PanelMinSize);
                top = clampToPage
                    ? Math.Clamp(worldPos.Y, 0f, bottom - PanelMinSize)
                    : Math.Min(worldPos.Y, bottom - PanelMinSize);
                break;
            case ResizeHandle.BottomLeft:
                left = clampToPage
                    ? Math.Clamp(worldPos.X, 0f, right - PanelMinSize)
                    : Math.Min(worldPos.X, right - PanelMinSize);
                bottom = clampToPage
                    ? Math.Clamp(worldPos.Y, top + PanelMinSize, pageSize.Height)
                    : Math.Max(worldPos.Y, top + PanelMinSize);
                break;
            case ResizeHandle.BottomRight:
                right = clampToPage
                    ? Math.Clamp(worldPos.X, left + PanelMinSize, pageSize.Width)
                    : Math.Max(worldPos.X, left + PanelMinSize);
                bottom = clampToPage
                    ? Math.Clamp(worldPos.Y, top + PanelMinSize, pageSize.Height)
                    : Math.Max(worldPos.Y, top + PanelMinSize);
                break;
        }

        if (aspectRatio.HasValue && aspectRatio.Value > 0.01f)
        {
            var ratio = aspectRatio.Value;
            var anchor = handle switch
            {
                ResizeHandle.TopLeft => original.BottomRight,
                ResizeHandle.TopRight => original.BottomLeft,
                ResizeHandle.BottomLeft => original.TopRight,
                _ => original.TopLeft
            };

            var width = MathF.Abs(right - left);
            var height = MathF.Abs(bottom - top);
            var targetWidth = height * ratio;
            var targetHeight = width / ratio;

            if (MathF.Abs(width - targetWidth) < MathF.Abs(height - targetHeight))
            {
                width = targetWidth;
            }
            else
            {
                height = targetHeight;
            }

            width = MathF.Max(width, PanelMinSize);
            height = MathF.Max(height, PanelMinSize);

            if (clampToPage)
            {
                var maxWidth = handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomLeft
                    ? anchor.X
                    : pageSize.Width - anchor.X;
                var maxHeight = handle == ResizeHandle.TopLeft || handle == ResizeHandle.TopRight
                    ? anchor.Y
                    : pageSize.Height - anchor.Y;

                if (maxWidth > 0f && maxHeight > 0f)
                {
                    var fitWidth = MathF.Min(width, maxWidth);
                    var fitHeight = fitWidth / ratio;
                    if (fitHeight > maxHeight)
                    {
                        fitHeight = maxHeight;
                        fitWidth = fitHeight * ratio;
                    }

                    width = MathF.Max(fitWidth, PanelMinSize);
                    height = MathF.Max(fitHeight, PanelMinSize);
                }
            }

            switch (handle)
            {
                case ResizeHandle.TopLeft:
                    right = anchor.X;
                    bottom = anchor.Y;
                    left = right - width;
                    top = bottom - height;
                    break;
                case ResizeHandle.TopRight:
                    left = anchor.X;
                    bottom = anchor.Y;
                    right = left + width;
                    top = bottom - height;
                    break;
                case ResizeHandle.BottomLeft:
                    right = anchor.X;
                    top = anchor.Y;
                    left = right - width;
                    bottom = top + height;
                    break;
                case ResizeHandle.BottomRight:
                    left = anchor.X;
                    top = anchor.Y;
                    right = left + width;
                    bottom = top + height;
                    break;
            }
        }

        return new Rect(left, top, right - left, bottom - top);
    }

    private void EndPanelResizeDrag()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null || !_editorState.DragPanelId.HasValue) return;

        var panel = page.FindPanel(_editorState.DragPanelId.Value);
        if (panel == null) return;

        var newBounds = panel.Bounds;
        var originalBounds = _editorState.DragPanelOriginalBounds;

        panel.SetBounds(originalBounds);

        if (newBounds != originalBounds)
        {
            _editorState.Execute(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
        }
    }

    private readonly struct TextPathHandleHit
    {
        public TextPathHandleHit(Guid ownerId, TextPathHandleType handleType)
        {
            OwnerId = ownerId;
            HandleType = handleType;
        }

        public Guid OwnerId { get; }
        public TextPathHandleType HandleType { get; }
    }

    private bool TryStartTextPathHandleDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? pointer)
    {
        if (!TryHitTextPathHandle(screenPos, out var hit))
        {
            return false;
        }

        var doc = _editorState.Document;
        if (doc == null) return false;

        var balloon = doc.FindBalloon(hit.OwnerId);
        if (balloon == null || balloon.TextPath == null) return false;
        var path = balloon.TextPath;
        _dragTextPathBalloonId = balloon.Id;

        _dragTextPathOriginal = path.Clone();
        _dragTextPathHandleType = hit.HandleType;

        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveTextPathHandle;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragCurrentScreen = screenPos;

        if (pointer != null)
        {
            MainCanvas.CapturePointer(pointer);
        }

        return true;
    }

    private bool TryHitTextPathHandle(Point2 screenPos, out TextPathHandleHit hit)
    {
        hit = default;
        var doc = _editorState.Document;
        if (doc == null) return false;

        var worldPos = _editorState.ViewTransform.ScreenToWorld(screenPos);
        var hitRadius = 8f / _editorState.ViewTransform.Zoom;

        if (_editorState.SelectedBalloonIds.Count == 1 && doc.SelectedBalloon != null && doc.SelectedBalloon.TextPath != null)
        {
            var balloon = doc.SelectedBalloon;
            if (TryFindPathHandle(worldPos, balloon.TextPath, balloon.Position, balloon.Rotation, hitRadius, out var handle))
            {
                hit = new TextPathHandleHit(balloon.Id, handle);
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPathHandle(
        Point2 worldPos,
        TextPath path,
        Point2 ownerPosition,
        float ownerRotation,
        float radius,
        out TextPathHandleType handleType)
    {
        var start = TextPath.LocalToWorld(path.Start, ownerPosition, ownerRotation);
        var control1 = TextPath.LocalToWorld(path.Control1, ownerPosition, ownerRotation);
        var control2 = TextPath.LocalToWorld(path.Control2, ownerPosition, ownerRotation);
        var end = TextPath.LocalToWorld(path.End, ownerPosition, ownerRotation);

        var hits = new[]
        {
            (Type: TextPathHandleType.Start, Point: start),
            (Type: TextPathHandleType.Control1, Point: control1),
            (Type: TextPathHandleType.Control2, Point: control2),
            (Type: TextPathHandleType.End, Point: end)
        };

        var bestDistance = float.MaxValue;
        handleType = TextPathHandleType.None;
        foreach (var candidate in hits)
        {
            var distance = Point2.Distance(worldPos, candidate.Point);
            if (distance <= radius && distance < bestDistance)
            {
                bestDistance = distance;
                handleType = candidate.Type;
            }
        }

        return handleType != TextPathHandleType.None;
    }

    private void UpdateTextPathHandleDrag(Point2 worldPos)
    {
        var doc = _editorState.Document;
        if (doc == null || _dragTextPathHandleType == TextPathHandleType.None || _dragTextPathOriginal == null)
        {
            return;
        }

        if (_dragTextPathBalloonId.HasValue)
        {
            var balloon = doc.FindBalloon(_dragTextPathBalloonId.Value);
            if (balloon?.TextPath == null) return;

            var local = TextPath.WorldToLocal(worldPos, balloon.Position, balloon.Rotation);
            var updated = UpdatePathHandlePoint(balloon.TextPath, _dragTextPathHandleType, local);
            balloon.SetTextPath(updated);
            MainCanvas.Invalidate();
            return;
        }
    }

    private void EndTextPathHandleDrag()
    {
        var doc = _editorState.Document;
        if (doc == null || _dragTextPathOriginal == null || _dragTextPathHandleType == TextPathHandleType.None)
        {
            return;
        }

        if (_dragTextPathBalloonId.HasValue)
        {
            var balloon = doc.FindBalloon(_dragTextPathBalloonId.Value);
            if (balloon?.TextPath == null) return;

            var updated = balloon.TextPath.Clone();
            balloon.SetTextPath(_dragTextPathOriginal);
            if (!updated.Equals(_dragTextPathOriginal))
            {
                _editorState.Execute(new SetBalloonTextPathCommand(balloon.Id, updated));
            }
            return;
        }
    }

    private static TextPath UpdatePathHandlePoint(TextPath path, TextPathHandleType handleType, Point2 localPoint)
    {
        return handleType switch
        {
            TextPathHandleType.Start => path.With(start: localPoint),
            TextPathHandleType.Control1 => path.With(control1: localPoint),
            TextPathHandleType.Control2 => path.With(control2: localPoint),
            TextPathHandleType.End => path.With(end: localPoint),
            _ => path
        };
    }

    private enum TailHandleType
    {
        Target,
        Attachment
    }

    private bool TryHitTailHandle(Point2 screenPos, out Balloon balloon, out Tail tail, out TailHandleType handleType)
    {
        balloon = null!;
        tail = null!;
        handleType = TailHandleType.Target;

        if (_editorState.Document == null || _editorState.SelectedBalloonIds.Count != 1)
        {
            return false;
        }

        var selectedBalloon = _editorState.Document.SelectedBalloon;
        if (selectedBalloon == null) return false;

        var worldPoint = _editorState.ViewTransform.ScreenToWorld(screenPos);
        var zoom = Math.Max(_editorState.ViewTransform.Zoom, 0.001f);
        var minScreenHitRadius = 8f / zoom;

        var targetHandleHitRadius = Math.Max(minScreenHitRadius, 7f);
        var attachmentHandleHitRadius = Math.Max(minScreenHitRadius, 6f);

        foreach (var candidate in selectedBalloon.Tails)
        {
            var renderedTargetPoint = TailGeometry.GetRenderedTargetPoint(selectedBalloon, candidate);
            if (Point2.Distance(worldPoint, renderedTargetPoint) <= targetHandleHitRadius)
            {
                balloon = selectedBalloon;
                tail = candidate;
                handleType = TailHandleType.Target;
                return true;
            }

            var attachPoint = TailGeometry.GetRenderedAttachmentPoint(selectedBalloon, candidate);
            if (Point2.Distance(worldPoint, attachPoint) <= attachmentHandleHitRadius)
            {
                balloon = selectedBalloon;
                tail = candidate;
                handleType = TailHandleType.Attachment;
                return true;
            }
        }

        return false;
    }

    private void UpdateBalloonDrag(Point2 worldPos)
    {
        if (_editorState.Document == null) return;

        if (_moveDragBaselinePending)
        {
            _moveDragBaselinePending = false;
            _editorState.DragStartWorld = worldPos;
            return;
        }

        var doc = _editorState.Document;
        var delta = worldPos - _editorState.DragStartWorld;
        if (IsSnappingEnabled())
        {
            var selectionBounds = ComputeSelectionBoundsForMove(doc, delta);
            if (selectionBounds.HasValue)
            {
                var snap = GetMoveSnapResult(selectionBounds.Value, new HashSet<Guid>(_editorState.SelectedBalloonIds));
                delta += snap.Offset;
                _editorState.SetSmartGuides(snap.Guides);
                UpdateSnapFeedback(snap.Offset, selectionBounds.Value.Offset(snap.Offset).Center, snap.Guides);
            }
            else
            {
                _editorState.ClearSmartGuides();
                ClearSnapFeedback();
            }
        }
        else
        {
            _editorState.ClearSmartGuides();
            ClearSnapFeedback();
        }

        if (_editorState.DragBalloonOriginalPositions.Count > 0)
        {
            foreach (var entry in _editorState.DragBalloonOriginalPositions)
            {
                var balloon = doc.FindBalloon(entry.Key);
                if (balloon == null) continue;
                var newPosition = entry.Value + delta;
                newPosition = ConstrainBalloonPositionToPanel(doc, balloon, newPosition);
                balloon.SetPosition(newPosition);
            }
        }
        else if (_editorState.DragBalloonId != null)
        {
            var balloon = doc.FindBalloon(_editorState.DragBalloonId.Value);
            if (balloon != null)
            {
                var newPosition = _editorState.DragBalloonOriginalPosition + delta;
                newPosition = ConstrainBalloonPositionToPanel(doc, balloon, newPosition);
                balloon.SetPosition(newPosition);
            }
        }

        var page = doc.ActivePage;
        if (page != null && _editorState.DragFloatingImageOriginalBoundsMap.Count > 0)
        {
            foreach (var entry in _editorState.DragFloatingImageOriginalBoundsMap)
            {
                var image = page.FindFloatingImage(entry.Key);
                if (image == null) continue;
                image.SetBounds(entry.Value.Offset(delta));
            }
        }

        var altHeld = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!altHeld)
        {
            foreach (var entry in _editorState.DragTailOriginalTargets)
            {
                var balloon = doc.FindBalloon(entry.Key.balloonId);
                var tail = balloon?.FindTail(entry.Key.tailId);
                if (tail != null)
                {
                    tail.SetTargetPoint(entry.Value + delta);
                }
            }
        }
        else
        {
            foreach (var entry in _editorState.DragTailOriginalTargets)
            {
                var balloon = doc.FindBalloon(entry.Key.balloonId);
                var tail = balloon?.FindTail(entry.Key.tailId);
                if (tail != null)
                {
                    tail.SetTargetPoint(entry.Value);
                }
            }
        }

        MainCanvas.Invalidate();
    }

    private void UpdateFloatingImageDrag(Point2 worldPos)
    {
        UpdateBalloonDrag(worldPos);
    }

    private void UpdateHoveredPanel(Point2 screenPos)
    {
        if (_editorState.Document?.ActivePage == null) return;
        if (_editorState.IsDragging || _isPanning) return;

        var hovered = _editorState.HitTestPanel(screenPos);
        _editorState.UpdateHoveredPanel(hovered?.Id);
    }

    private static Point2 ConstrainBalloonPositionToPanel(Document doc, Balloon balloon, Point2 position)
    {
        return ConstrainBalloonPositionToPanel(doc, balloon, position, balloon.ComputedSize);
    }

    private static Point2 ConstrainBalloonPositionToPanel(Document doc, Balloon balloon, Point2 position, Size2 size)
    {
        if (!balloon.ConstrainToPanel || !balloon.PanelId.HasValue) return position;

        var panel = doc.FindPanel(balloon.PanelId.Value);
        if (panel == null) return position;

        var bounds = panel.Bounds;
        _ = size;

        var x = Math.Clamp(position.X, bounds.Left, bounds.Right);
        var y = Math.Clamp(position.Y, bounds.Top, bounds.Bottom);

        return new Point2(x, y);
    }

    private void StartTailDrag(Balloon balloon, Tail tail, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveTailTarget;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        _editorState.DragTailId = tail.Id;
        _editorState.DragBalloonOriginalPosition = tail.TargetPoint;
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartTailAttachmentDrag(Balloon balloon, Tail tail, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveTailAttachment;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        _editorState.DragTailId = tail.Id;
        _editorState.DragTailOriginalAttachmentDirection = tail.AttachmentDirection;
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartResizeDrag(Balloon balloon, ResizeHandle handle, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.ResizeBalloon;
        _editorState.CurrentResizeHandle = handle;
        _resizeDragBaselinePending = true;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        _editorState.DragBalloonOriginalPosition = balloon.Position;
        _editorState.DragBalloonOriginalSize = balloon.ComputedSize;
        _editorState.DragBalloonOriginalMaxTextWidth = balloon.MaxTextWidth;
        _editorState.DragBalloonOriginalStates.Clear();

        if (_editorState.Document != null)
        {
            var selectionBounds = _editorState.GetSelectionBounds();
            _editorState.DragSelectionBounds = selectionBounds ?? balloon.Bounds;

            var selectedIds = _editorState.SelectedBalloonIds;
            if (selectedIds.Count > 0)
            {
                foreach (var id in selectedIds)
                {
                    var selectedBalloon = _editorState.Document.FindBalloon(id);
                    if (selectedBalloon != null)
                    {
                        _editorState.DragBalloonOriginalStates[id] = new BalloonResizeSnapshot(
                            selectedBalloon.Position,
                            selectedBalloon.ComputedSize,
                            selectedBalloon.MaxTextWidth,
                            selectedBalloon.MaxTextHeight);
                    }
                }
            }
            else
            {
                _editorState.DragBalloonOriginalStates[balloon.Id] = new BalloonResizeSnapshot(
                    balloon.Position,
                    balloon.ComputedSize,
                    balloon.MaxTextWidth,
                    balloon.MaxTextHeight);
            }
        }
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private bool TryStartRotationDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var balloon = _editorState.HitTestRotationHandle(screenPos);
        if (balloon == null) return false;

        StartRotationDrag(balloon, screenPos, worldPos, ptrDevice);
        return true;
    }

    private void StartRotationDrag(Balloon balloon, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.RotateBalloon;
        _rotationDragBaselinePending = true;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        _editorState.DragBalloonOriginalRotation = balloon.Rotation;
        _editorState.DragRotationStartAngle = GetRotationAngle(balloon.Position, worldPos);

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void UpdateRotationDrag(Point2 worldPos)
    {
        if (_editorState.Document == null || _editorState.DragBalloonId == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null) return;

        if (_rotationDragBaselinePending)
        {
            _rotationDragBaselinePending = false;
            _editorState.DragStartWorld = worldPos;
            _editorState.DragBalloonOriginalRotation = balloon.Rotation;
            _editorState.DragRotationStartAngle = GetRotationAngle(balloon.Position, worldPos);
            return;
        }

        var angle = GetRotationAngle(balloon.Position, worldPos);
        var delta = NormalizeRadians(angle - _editorState.DragRotationStartAngle);
        var newRotation = _editorState.DragBalloonOriginalRotation + delta * 180f / MathF.PI;
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (shift)
        {
            const float snap = 15f;
            newRotation = MathF.Round(newRotation / snap) * snap;
        }

        newRotation = NormalizeRotation(newRotation);

        if (Math.Abs(newRotation - balloon.Rotation) < 0.01f) return;

        balloon.SetRotation(newRotation);
        UpdateRotationControls(newRotation);
        MainCanvas.Invalidate();
    }

    private void EndRotationDrag()
    {
        if (_editorState.Document == null || _editorState.DragBalloonId == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null) return;

        var newRotation = balloon.Rotation;
        balloon.SetRotation(_editorState.DragBalloonOriginalRotation);

        if (Math.Abs(newRotation - _editorState.DragBalloonOriginalRotation) > 0.01f)
        {
            _editorState.Execute(new RotateBalloonCommand(balloon.Id, newRotation));
        }
    }

    private void UpdateRotationControls(float rotation)
    {
        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        try
        {
            RotationSlider.Value = rotation;
            RotationValueText.Text = $"{rotation:F0}°";
        }
        finally
        {
            _isUpdatingProperties = wasUpdating;
        }
    }

    private static float GetRotationAngle(Point2 center, Point2 point)
    {
        var direction = point - center;
        if (direction.Length <= 0.0001f)
        {
            return 0f;
        }

        return MathF.Atan2(direction.Y, direction.X);
    }

    private static float NormalizeRotation(float rotation)
    {
        var normalized = rotation % 360f;
        if (normalized > 180f)
        {
            normalized -= 360f;
        }
        else if (normalized < -180f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private static float NormalizeRadians(float radians)
    {
        const float twoPi = MathF.PI * 2f;
        if (radians > MathF.PI)
        {
            radians -= twoPi;
        }
        else if (radians < -MathF.PI)
        {
            radians += twoPi;
        }

        return radians;
    }

    private void StartTextSelectionDrag(Balloon balloon, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.TextSelection;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;
        _editorState.DragBalloonId = balloon.Id;
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void StartMarqueeSelect(Point2 screenPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice, bool additive)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MarqueeSelect;
        _editorState.MarqueeStartScreen = screenPos;
        _editorState.MarqueeCurrentScreen = screenPos;
        _editorState.MarqueeIsAdditive = additive;
        _editorState.IsMarqueeSelecting = true;
        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
        MainCanvas.Invalidate();
    }

    private void UpdateMarqueeDrag(Point2 screenPos)
    {
        _editorState.MarqueeCurrentScreen = screenPos;
        _editorState.IsMarqueeSelecting = true;
        MainCanvas.Invalidate();
    }

    private void UpdateResizeDrag(Point2 worldPos)
    {
        if (_editorState.Document == null) return;

        if (_resizeDragBaselinePending)
        {
            _resizeDragBaselinePending = false;
            _editorState.DragStartWorld = worldPos;
            return;
        }

        var dragDelta = worldPos - _editorState.DragStartWorld;

        if (_editorState.DragBalloonOriginalStates.Count > 1)
        {
            var selectionBounds = _editorState.DragSelectionBounds;
            float newLeft = selectionBounds.Left;
            float newTop = selectionBounds.Top;
            float newRight = selectionBounds.Right;
            float newBottom = selectionBounds.Bottom;

            switch (_editorState.CurrentResizeHandle)
            {
                case ResizeHandle.TopLeft:
                    newLeft = selectionBounds.Left + dragDelta.X;
                    newTop = selectionBounds.Top + dragDelta.Y;
                    break;
                case ResizeHandle.TopRight:
                    newRight = selectionBounds.Right + dragDelta.X;
                    newTop = selectionBounds.Top + dragDelta.Y;
                    break;
                case ResizeHandle.BottomLeft:
                    newLeft = selectionBounds.Left + dragDelta.X;
                    newBottom = selectionBounds.Bottom + dragDelta.Y;
                    break;
                case ResizeHandle.BottomRight:
                    newRight = selectionBounds.Right + dragDelta.X;
                    newBottom = selectionBounds.Bottom + dragDelta.Y;
                    break;
            }

            if (IsSnappingEnabled())
            {
                var unsnappedLeft = newLeft;
                var unsnappedTop = newTop;
                var unsnappedRight = newRight;
                var unsnappedBottom = newBottom;
                var snap = GetResizeSnapResult(newLeft, newTop, newRight, newBottom, _editorState.CurrentResizeHandle, new HashSet<Guid>(_editorState.SelectedBalloonIds));
                newLeft = snap.Left;
                newTop = snap.Top;
                newRight = snap.Right;
                newBottom = snap.Bottom;
                _editorState.SetSmartGuides(snap.Guides);
                var snapOffset = new Point2(
                    _editorState.CurrentResizeHandle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft
                        ? snap.Left - unsnappedLeft
                        : snap.Right - unsnappedRight,
                    _editorState.CurrentResizeHandle is ResizeHandle.TopLeft or ResizeHandle.TopRight
                        ? snap.Top - unsnappedTop
                        : snap.Bottom - unsnappedBottom);
                UpdateSnapFeedback(snapOffset, new Rect(snap.Left, snap.Top, snap.Right - snap.Left, snap.Bottom - snap.Top).Center, snap.Guides);
            }
            else
            {
                _editorState.ClearSmartGuides();
                ClearSnapFeedback();
            }

            var minSize = 1f;
            if (newRight - newLeft < minSize)
            {
                if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.BottomLeft)
                    newLeft = newRight - minSize;
                else
                    newRight = newLeft + minSize;
            }
            if (newBottom - newTop < minSize)
            {
                if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.TopRight)
                    newTop = newBottom - minSize;
                else
                    newBottom = newTop + minSize;
            }

            var newBounds = new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
            var selectionWidth = MathF.Max(selectionBounds.Width, 1f);
            var selectionHeight = MathF.Max(selectionBounds.Height, 1f);

            foreach (var entry in _editorState.DragBalloonOriginalStates)
            {
                var balloon = _editorState.Document.FindBalloon(entry.Key);
                if (balloon == null) continue;

                var snapshot = entry.Value;
                var originalBalloonBounds = Rect.FromCenterSize(snapshot.Position, snapshot.Size);

                var relLeft = (originalBalloonBounds.Left - selectionBounds.Left) / selectionWidth;
                var relRight = (originalBalloonBounds.Right - selectionBounds.Left) / selectionWidth;
                var relTop = (originalBalloonBounds.Top - selectionBounds.Top) / selectionHeight;
                var relBottom = (originalBalloonBounds.Bottom - selectionBounds.Top) / selectionHeight;

                var scaledLeft = newBounds.Left + relLeft * newBounds.Width;
                var scaledRight = newBounds.Left + relRight * newBounds.Width;
                var scaledTop = newBounds.Top + relTop * newBounds.Height;
                var scaledBottom = newBounds.Top + relBottom * newBounds.Height;

                var newWidth = scaledRight - scaledLeft;
                var newHeight = scaledBottom - scaledTop;
                var newCenter = new Point2(scaledLeft + newWidth / 2, scaledTop + newHeight / 2);

                var style = balloon.BalloonStyle;
                balloon.SetComputedSize(new Size2(newWidth, newHeight));
                balloon.SetMaxTextWidth(newWidth - style.PaddingLeft - style.PaddingRight);
                balloon.SetMaxTextHeight(newHeight - style.PaddingTop - style.PaddingBottom);
                balloon.SetPosition(newCenter);
            }

            MainCanvas.Invalidate();
            return;
        }

        if (_editorState.DragBalloonId == null) return;

        var singleBalloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (singleBalloon == null) return;

        var originalBounds = Rect.FromCenterSize(_editorState.DragBalloonOriginalPosition, _editorState.DragBalloonOriginalSize);

        float singleLeft = originalBounds.X;
        float singleTop = originalBounds.Y;
        float singleRight = originalBounds.Right;
        float singleBottom = originalBounds.Bottom;

        switch (_editorState.CurrentResizeHandle)
        {
            case ResizeHandle.TopLeft:
                singleLeft = originalBounds.Left + dragDelta.X;
                singleTop = originalBounds.Top + dragDelta.Y;
                break;
            case ResizeHandle.TopRight:
                singleRight = originalBounds.Right + dragDelta.X;
                singleTop = originalBounds.Top + dragDelta.Y;
                break;
            case ResizeHandle.BottomLeft:
                singleLeft = originalBounds.Left + dragDelta.X;
                singleBottom = originalBounds.Bottom + dragDelta.Y;
                break;
            case ResizeHandle.BottomRight:
                singleRight = originalBounds.Right + dragDelta.X;
                singleBottom = originalBounds.Bottom + dragDelta.Y;
                break;
        }

        var shiftHeld = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (shiftHeld && _editorState.DragBalloonOriginalSize.Width > 0 && _editorState.DragBalloonOriginalSize.Height > 0)
        {
            var aspectRatio = _editorState.DragBalloonOriginalSize.Width / _editorState.DragBalloonOriginalSize.Height;
            var newWidth = singleRight - singleLeft;
            var newHeight = singleBottom - singleTop;

            switch (_editorState.CurrentResizeHandle)
            {
                case ResizeHandle.TopLeft:
                case ResizeHandle.BottomLeft:
                case ResizeHandle.TopRight:
                case ResizeHandle.BottomRight:
                    var widthChange = Math.Abs(newWidth - _editorState.DragBalloonOriginalSize.Width);
                    var heightChange = Math.Abs(newHeight - _editorState.DragBalloonOriginalSize.Height);
                    if (widthChange > heightChange)
                    {
                        var constrainedHeight = newWidth / aspectRatio;
                        if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.TopRight)
                            singleTop = singleBottom - constrainedHeight;
                        else
                            singleBottom = singleTop + constrainedHeight;
                    }
                    else
                    {
                        var constrainedWidth = newHeight * aspectRatio;
                        if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.BottomLeft)
                            singleLeft = singleRight - constrainedWidth;
                        else
                            singleRight = singleLeft + constrainedWidth;
                    }
                    break;
            }
        }

        if (IsSnappingEnabled())
        {
            var unsnappedLeft = singleLeft;
            var unsnappedTop = singleTop;
            var unsnappedRight = singleRight;
            var unsnappedBottom = singleBottom;
            var snap = GetResizeSnapResult(singleLeft, singleTop, singleRight, singleBottom, _editorState.CurrentResizeHandle, new HashSet<Guid>(_editorState.SelectedBalloonIds));
            singleLeft = snap.Left;
            singleTop = snap.Top;
            singleRight = snap.Right;
            singleBottom = snap.Bottom;
            _editorState.SetSmartGuides(snap.Guides);
            var snapOffset = new Point2(
                _editorState.CurrentResizeHandle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft
                    ? snap.Left - unsnappedLeft
                    : snap.Right - unsnappedRight,
                _editorState.CurrentResizeHandle is ResizeHandle.TopLeft or ResizeHandle.TopRight
                    ? snap.Top - unsnappedTop
                    : snap.Bottom - unsnappedBottom);
            UpdateSnapFeedback(snapOffset, new Rect(snap.Left, snap.Top, snap.Right - snap.Left, snap.Bottom - snap.Top).Center, snap.Guides);
        }
        else
        {
            _editorState.ClearSmartGuides();
            ClearSnapFeedback();
        }

        var minWidth = singleBalloon.BalloonStyle.MinWidth;
        var minHeight = singleBalloon.BalloonStyle.MinHeight;
        if (singleRight - singleLeft < minWidth)
        {
            if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.BottomLeft)
                singleLeft = singleRight - minWidth;
            else
                singleRight = singleLeft + minWidth;
        }
        if (singleBottom - singleTop < minHeight)
        {
            if (_editorState.CurrentResizeHandle == ResizeHandle.TopLeft || _editorState.CurrentResizeHandle == ResizeHandle.TopRight)
                singleTop = singleBottom - minHeight;
            else
                singleBottom = singleTop + minHeight;
        }

        var width = singleRight - singleLeft;
        var height = singleBottom - singleTop;
        var center = new Point2(singleLeft + width / 2, singleTop + height / 2);

        var singleStyle = singleBalloon.BalloonStyle;
        singleBalloon.SetComputedSize(new Size2(width, height));
        singleBalloon.SetMaxTextWidth(width - singleStyle.PaddingLeft - singleStyle.PaddingRight);
        singleBalloon.SetMaxTextHeight(height - singleStyle.PaddingTop - singleStyle.PaddingBottom);
        singleBalloon.SetPosition(center);
        MainCanvas.Invalidate();
    }

    private void UpdateTextSelectionDrag(Point2 worldPos)
    {
        if (_editorState.EditingBalloonId == null) return;

        var balloon = _editorState.Document?.FindBalloon(_editorState.EditingBalloonId.Value);
        if (balloon == null) return;

        var caretIndex = GetCaretIndexFromPoint(balloon, worldPos);
        _editorState.SetCursorPosition(caretIndex, extendSelection: true);
    }

    private void UpdateTailDrag(Point2 worldPos)
    {
        if (_editorState.DragBalloonId == null || _editorState.Document == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null || _editorState.DragTailId == null) return;

        var tail = balloon.FindTail(_editorState.DragTailId.Value);
        if (tail == null) return;

        tail.SetTargetPoint(TailGeometry.ToTailSpacePoint(balloon, worldPos));
        MainCanvas.Invalidate();
    }

    private void UpdateTailAttachmentDrag(Point2 worldPos)
    {
        if (_editorState.DragBalloonId == null || _editorState.Document == null || _editorState.DragTailId == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null) return;

        var tail = balloon.FindTail(_editorState.DragTailId.Value);
        if (tail == null) return;

        var tailSpacePoint = TailGeometry.ToTailSpacePoint(balloon, worldPos);
        var direction = tailSpacePoint - balloon.Position;
        if (direction.Length <= 0.0001f)
        {
            direction = _editorState.DragTailOriginalAttachmentDirection ?? (tail.TargetPoint - balloon.Position);
        }

        if (direction.Length <= 0.0001f) return;

        tail.SetAttachmentDirection(direction.Normalized());
        MainCanvas.Invalidate();
    }

    private bool TryStartGuideFromRuler(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return false;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("input.status.guides_locked"));
            return false;
        }

        if (screenPos.X <= RulerThickness && screenPos.Y <= RulerThickness) return false;

        GuideOrientation? orientation = null;
        if (screenPos.Y <= RulerThickness)
        {
            orientation = GuideOrientation.Horizontal;
        }
        else if (screenPos.X <= RulerThickness)
        {
            orientation = GuideOrientation.Vertical;
        }
        else
        {
            return false;
        }

        var position = orientation == GuideOrientation.Horizontal ? worldPos.Y : worldPos.X;
        var cmd = new CreateGuideCommand(page.Id, orientation.Value, position);
        _editorState.Execute(cmd);

        var guide = page.FindGuide(cmd.CreatedGuideId);
        if (guide == null) return false;

        StartGuideDrag(guide, screenPos, worldPos, ptrDevice);
        return true;
    }

    private bool TryStartGuideDrag(Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        if (screenPos.X <= RulerThickness || screenPos.Y <= RulerThickness) return false;
        if (_editorState.Document?.ActivePage?.GuidesLocked == true)
        {
            SetStatusMessage(L("input.status.guides_locked_edit"));
            return false;
        }
        if (!TryHitGuide(screenPos, out var guide)) return false;

        StartGuideDrag(guide, screenPos, worldPos, ptrDevice);
        return true;
    }

    private bool TryHitGuide(Point2 screenPos, out Guide guide)
    {
        guide = null!;
        var page = _editorState.Document?.ActivePage;
        if (page == null) return false;

        var bestDistance = float.MaxValue;
        Guide? bestGuide = null;

        foreach (var candidate in page.Guides)
        {
            float distance;
            if (candidate.Orientation == GuideOrientation.Horizontal)
            {
                var screenY = _editorState.ViewTransform.WorldToScreen(new Point2(0, candidate.Position)).Y;
                distance = MathF.Abs(screenPos.Y - screenY);
            }
            else
            {
                var screenX = _editorState.ViewTransform.WorldToScreen(new Point2(candidate.Position, 0)).X;
                distance = MathF.Abs(screenPos.X - screenX);
            }

            if (distance <= GuideHitThreshold && distance < bestDistance)
            {
                bestDistance = distance;
                bestGuide = candidate;
            }
        }

        if (bestGuide == null) return false;
        guide = bestGuide;
        return true;
    }

    private void StartGuideDrag(Guide guide, Point2 screenPos, Point2 worldPos, Microsoft.UI.Xaml.Input.Pointer? ptrDevice)
    {
        _editorState.IsDragging = true;
        _editorState.CurrentDragType = DragType.MoveGuide;
        _editorState.DragGuideId = guide.Id;
        _editorState.DragGuideOrientation = guide.Orientation;
        _editorState.DragGuideOriginalPosition = guide.Position;
        _editorState.DragStartScreen = screenPos;
        _editorState.DragStartWorld = worldPos;

        if (ptrDevice != null)
        {
            MainCanvas.CapturePointer(ptrDevice);
        }
    }

    private void UpdateGuideDrag(Point2 worldPos)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null || _editorState.DragGuideId == null) return;
        if (page.GuidesLocked) return;

        var guide = page.FindGuide(_editorState.DragGuideId.Value);
        if (guide == null) return;

        var newPosition = guide.Orientation == GuideOrientation.Horizontal ? worldPos.Y : worldPos.X;
        guide.SetPosition(newPosition);
        RefreshGuideValueEditors(page);
        MainCanvas.Invalidate();
    }

    private void EndGuideDrag()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || _editorState.DragGuideId == null) return;

        var guide = page.FindGuide(_editorState.DragGuideId.Value);
        if (guide == null) return;
        if (page.GuidesLocked)
        {
            guide.SetPosition(_editorState.DragGuideOriginalPosition);
            return;
        }

        var newPosition = guide.Position;
        var originalPosition = _editorState.DragGuideOriginalPosition;
        guide.SetPosition(originalPosition);

        var outOfBounds = guide.Orientation == GuideOrientation.Horizontal
            ? newPosition < 0f || newPosition > doc.Size.Height
            : newPosition < 0f || newPosition > doc.Size.Width;

        if (outOfBounds)
        {
            _editorState.Execute(new DeleteGuideCommand(page.Id, guide.Id));
            UpdateGuideList();
            return;
        }

        if (Math.Abs(newPosition - originalPosition) > 0.01f)
        {
            _editorState.Execute(new MoveGuideCommand(page.Id, guide.Id, newPosition));
            RefreshGuideValueEditors(page);
        }
    }

    private void EndDrag()
    {
        if (_editorState.CurrentDragType == DragType.MarqueeSelect)
        {
            CompleteMarqueeSelection();
            ResetDragState();
            return;
        }

        if (_editorState.Document == null)
        {
            ResetDragState();
            return;
        }

        switch (_editorState.CurrentDragType)
        {
            case DragType.MoveBalloon:
                EndMoveDrag();
                break;

            case DragType.MoveFloatingImage:
                EndFloatingImageMoveDrag();
                break;

            case DragType.MoveTailTarget:
                EndTailDrag();
                break;

            case DragType.MoveTailAttachment:
                EndTailAttachmentDrag();
                break;

            case DragType.ResizeBalloon:
                EndResizeDrag();
                break;

            case DragType.ResizeFloatingImage:
                EndFloatingImageResizeDrag();
                break;

            case DragType.RotateBalloon:
                EndRotationDrag();
                break;

            case DragType.TextSelection:
                break;

            case DragType.MoveGuide:
                EndGuideDrag();
                break;

            case DragType.CreatePanel:
                EndPanelDrag();
                break;

            case DragType.CreatePanelFreeform:
                EndPanelFreeformDrag();
                break;

            case DragType.MovePanel:
                EndPanelMoveDrag();
                break;

            case DragType.ResizePanel:
                EndPanelResizeDrag();
                break;

            case DragType.EditPanelVertex:
                EndPanelEditVertexDrag();
                break;

            case DragType.MoveTextPathHandle:
                EndTextPathHandleDrag();
                break;
        }

        var refreshProperties = _editorState.CurrentDragType is
            DragType.MoveBalloon or DragType.ResizeBalloon or DragType.RotateBalloon or
            DragType.MoveTailTarget or DragType.MoveTailAttachment or
            DragType.MoveFloatingImage or DragType.ResizeFloatingImage or
            DragType.MoveTextPathHandle;

        ResetDragState();

        if (refreshProperties)
        {
            UpdatePropertiesPanel();
        }
    }

    private void EndPanelDrag()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        if (!_panelPreviewBounds.HasValue) return;

        var bounds = _panelPreviewBounds.Value;
        if (bounds.Width < PanelMinSize || bounds.Height < PanelMinSize) return;

        var nextOrder = page.Panels.Count + 1;
        var panelName = $"Panel {nextOrder}";

        var cornerRadius = _selectedPanelShape == PanelShape.RoundedRect ? 16f : 0f;
        _editorState.Execute(new CreatePanelZoneCommand(
            page.Id, panelName, bounds, nextOrder,
            safeMargin: GetPanelDefaultSafeMargin(),
            borderColor: GetPanelDefaultBorderColor(),
            borderWidth: GetPanelDefaultBorderWidth(),
            borderStyle: GetPanelDefaultBorderStyle(),
            shape: _selectedPanelShape,
            cornerRadius: cornerRadius));

        SetStatusMessage(LF("input.status.created_panel_named", panelName));
        var createdPanel = page.Panels.LastOrDefault();
        if (createdPanel != null)
        {
            _lastCreatedPanelId = createdPanel.Id;
            _lastCreatedBalloonId = null;
        }
    }

    private void EndMoveDrag()
    {
        if (_editorState.Document == null) return;

        var doc = _editorState.Document;
        var page = doc.ActivePage;
        var commands = new List<ICommand>();
        foreach (var entry in _editorState.DragBalloonOriginalPositions)
        {
            var balloon = doc.FindBalloon(entry.Key);
            if (balloon == null) continue;

            var newPos = balloon.Position;
            balloon.SetPosition(entry.Value);

            if (newPos != entry.Value)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        foreach (var entry in _editorState.DragTailOriginalTargets)
        {
            var balloon = doc.FindBalloon(entry.Key.balloonId);
            var tail = balloon?.FindTail(entry.Key.tailId);
            if (tail == null) continue;

            var newTailPos = tail.TargetPoint;
            tail.SetTargetPoint(entry.Value);

            if (newTailPos != entry.Value)
            {
                commands.Add(new MoveTailTargetCommand(balloon!.Id, newTailPos, tail.Id));
            }
        }

        if (page != null)
        {
            foreach (var entry in _editorState.DragFloatingImageOriginalBoundsMap)
            {
                var image = page.FindFloatingImage(entry.Key);
                if (image == null) continue;

                var newBounds = image.Bounds;
                image.SetBounds(entry.Value);

                if (newBounds != entry.Value)
                {
                    commands.Add(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
                }
            }
        }

        var movedBalloonCount = _editorState.DragBalloonOriginalPositions.Count;
        var movedImageCount = _editorState.DragFloatingImageOriginalBoundsMap.Count;
        var isMultiObjectMove = movedBalloonCount + movedImageCount > 1;

        if (commands.Count == 1 && !isMultiObjectMove)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            var hasBalloonMoves = movedBalloonCount > 0;
            var hasImageMoves = movedImageCount > 0;
            var description = hasBalloonMoves && hasImageMoves
                ? "Move objects"
                : hasImageMoves
                    ? "Move floating images"
                    : "Move balloons";
            _editorState.ExecuteTransaction(description, commands);
        }
        else if (commands.Count == 1)
        {
            var hasBalloonMoves = movedBalloonCount > 0;
            var hasImageMoves = movedImageCount > 0;
            var description = hasBalloonMoves && hasImageMoves
                ? "Move objects"
                : hasImageMoves
                    ? "Move floating images"
                    : "Move balloons";
            _editorState.ExecuteTransaction(description, commands);
        }
    }

    private void EndFloatingImageMoveDrag()
    {
        EndMoveDrag();
    }

    private void EndFloatingImageResizeDrag()
    {
        CommitFloatingImageBoundsChange();
    }

    private void CommitFloatingImageBoundsChange()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null || !_editorState.DragFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.DragFloatingImageId.Value);
        if (image == null) return;

        var newBounds = image.Bounds;
        var originalBounds = _editorState.DragFloatingImageOriginalBounds;
        image.SetBounds(originalBounds);

        if (newBounds != originalBounds)
        {
            _editorState.Execute(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
        }
    }

    private void EndTailDrag()
    {
        if (_editorState.Document == null || _editorState.DragBalloonId == null || _editorState.DragTailId == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null) return;

        var tail = balloon.FindTail(_editorState.DragTailId.Value);
        if (tail == null) return;

        var newTailPos = tail.TargetPoint;
        tail.SetTargetPoint(_editorState.DragBalloonOriginalPosition);

        if (newTailPos != _editorState.DragBalloonOriginalPosition)
        {
            _editorState.Execute(new MoveTailTargetCommand(balloon.Id, newTailPos, tail.Id));
        }
    }

    private void EndTailAttachmentDrag()
    {
        if (_editorState.Document == null || _editorState.DragBalloonId == null || _editorState.DragTailId == null) return;

        var balloon = _editorState.Document.FindBalloon(_editorState.DragBalloonId.Value);
        if (balloon == null) return;

        var tail = balloon.FindTail(_editorState.DragTailId.Value);
        if (tail == null) return;

        var newDirection = tail.AttachmentDirection;
        tail.SetAttachmentDirection(_editorState.DragTailOriginalAttachmentDirection);

        if (newDirection != _editorState.DragTailOriginalAttachmentDirection)
        {
            _editorState.Execute(new SetTailAttachmentDirectionCommand(balloon.Id, newDirection, tail.Id));
        }
    }

    private void EndResizeDrag()
    {
        if (_editorState.Document == null) return;

        var commands = new List<ICommand>();
        foreach (var entry in _editorState.DragBalloonOriginalStates)
        {
            var balloon = _editorState.Document.FindBalloon(entry.Key);
            if (balloon == null) continue;

            var snapshot = entry.Value;
            var newSize = balloon.ComputedSize;
            var newPos = balloon.Position;
            var newMaxTextWidth = balloon.MaxTextWidth; // Capture current MaxTextWidth before restore
            var newMaxTextHeight = balloon.MaxTextHeight; // Capture current MaxTextHeight before restore

            balloon.SetComputedSize(snapshot.Size);
            balloon.SetPosition(snapshot.Position);
            balloon.SetMaxTextWidth(snapshot.MaxTextWidth);
            balloon.SetMaxTextHeight(snapshot.MaxTextHeight);

            if (newSize != snapshot.Size || newPos != snapshot.Position)
            {
                var wasManualFitting = snapshot.MaxTextWidth.HasValue || snapshot.MaxTextHeight.HasValue;
                commands.Add(new ResizeBalloonCommand(
                    balloon.Id,
                    newSize,
                    newPos,
                    wasManualFitting,
                    newMaxTextWidth,
                    newMaxTextHeight));
            }
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            _editorState.ExecuteTransaction("Resize balloons", commands);
        }
    }

    private void ResetDragState()
    {
        _editorState.IsDragging = false;
        _editorState.IsMarqueeSelecting = false;
        _editorState.MarqueeIsAdditive = false;
        _editorState.CurrentDragType = DragType.None;
        _editorState.DragBalloonId = null;
        _editorState.DragBalloonOriginalMaxTextWidth = null;
        _editorState.DragBalloonOriginalRotation = 0f;
        _editorState.DragRotationStartAngle = 0f;
        _editorState.DragTailId = null;
        _editorState.DragTailOriginalAttachmentDirection = null;
        _editorState.DragBalloonOriginalPositions.Clear();
        _editorState.DragBalloonOriginalStates.Clear();
        _editorState.DragTailOriginalTargets.Clear();
        _editorState.DragGuideId = null;
        _editorState.DragPanelId = null;
        _editorState.DragPanelOriginalBounds = default;
        _editorState.DragPanelOriginalBoundsMap.Clear();
        _editorState.DragFloatingImageId = null;
        _editorState.DragFloatingImageOriginalBounds = default;
        _editorState.DragFloatingImageOriginalBoundsMap.Clear();
        _editorState.CurrentResizeHandle = ResizeHandle.None;
        _resizeDragBaselinePending = false;
        _moveDragBaselinePending = false;
        _panelMoveDragBaselinePending = false;
        _rotationDragBaselinePending = false;
        _dragTextPathBalloonId = null;
        _dragTextPathOriginal = null;
        _dragTextPathHandleType = TextPathHandleType.None;
        _editorState.ClearSmartGuides();
        ClearSnapFeedback();
        _panelPreviewBounds = null;
        MainCanvas.ReleasePointerCaptures();
        MainCanvas.Invalidate();
    }

    private void CompleteMarqueeSelection()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var start = _editorState.MarqueeStartScreen;
        var current = _editorState.MarqueeCurrentScreen;
        var left = Math.Min(start.X, current.X);
        var top = Math.Min(start.Y, current.Y);
        var width = Math.Abs(start.X - current.X);
        var height = Math.Abs(start.Y - current.Y);

        const float minDrag = 3f;
        if (width < minDrag && height < minDrag)
        {
            if (!_editorState.MarqueeIsAdditive)
            {
                _editorState.SelectBalloon(null);
            }
            return;
        }

        var worldTopLeft = _editorState.ViewTransform.ScreenToWorld(new Point2(left, top));
        var worldBottomRight = _editorState.ViewTransform.ScreenToWorld(new Point2(left + width, top + height));
        var selectionRect = Rect.FromCorners(
            new Point2(MathF.Min(worldTopLeft.X, worldBottomRight.X), MathF.Min(worldTopLeft.Y, worldBottomRight.Y)),
            new Point2(MathF.Max(worldTopLeft.X, worldBottomRight.X), MathF.Max(worldTopLeft.Y, worldBottomRight.Y)));
        _lastMarqueeSelectionBounds = selectionRect;

        var hits = new List<Guid>();
        var imageHits = new List<Guid>();
        var page = doc.ActivePage;

        if (page != null)
        {
            foreach (var image in page.FloatingImages)
            {
                var ownerLayer = page.FindLayerForFloatingImage(image);
                if (ownerLayer == null || !ownerLayer.IsVisible || ownerLayer.IsLocked) continue;
                if (!image.IsVisible || image.IsLocked) continue;

                if (selectionRect.Intersects(image.Bounds))
                {
                    imageHits.Add(image.Id);
                }
            }
        }

        foreach (var layer in doc.Layers)
        {
            if (!layer.IsVisible || layer.IsLocked) continue;

            foreach (var balloon in layer.Balloons)
            {
                if (!balloon.IsVisible || balloon.IsLocked) continue;

                if (balloon.PanelId.HasValue && page != null)
                {
                    var panel = page.FindPanel(balloon.PanelId.Value);
                    if (panel != null && (!panel.IsVisible || panel.IsLocked)) continue;
                }

                if (selectionRect.Intersects(balloon.Bounds))
                {
                    hits.Add(balloon.Id);
                }
            }
        }

        var nextBalloonSelection = new HashSet<Guid>(hits);
        var nextImageSelection = new HashSet<Guid>(imageHits);
        Guid? nextPrimaryBalloon = hits.Count > 0 ? hits[^1] : null;
        Guid? nextPrimaryImage = imageHits.Count > 0 ? imageHits[^1] : null;

        if (_editorState.MarqueeIsAdditive)
        {
            nextBalloonSelection = new HashSet<Guid>(_editorState.SelectedBalloonIds);
            foreach (var id in hits)
            {
                nextBalloonSelection.Add(id);
            }

            nextImageSelection = new HashSet<Guid>(_editorState.SelectedFloatingImageIds);
            foreach (var id in imageHits)
            {
                nextImageSelection.Add(id);
            }

            nextPrimaryBalloon = doc.SelectedBalloonId;
            if (!nextPrimaryBalloon.HasValue || !nextBalloonSelection.Contains(nextPrimaryBalloon.Value))
            {
                nextPrimaryBalloon = hits.Count > 0
                    ? hits[^1]
                    : nextBalloonSelection.Count > 0
                        ? nextBalloonSelection.First()
                        : null;
            }

            nextPrimaryImage = _editorState.SelectedFloatingImageId;
            if (!nextPrimaryImage.HasValue || !nextImageSelection.Contains(nextPrimaryImage.Value))
            {
                nextPrimaryImage = imageHits.Count > 0
                    ? imageHits[^1]
                    : nextImageSelection.Count > 0
                        ? nextImageSelection.First()
                        : null;
            }
        }

        _editorState.SetSelection(nextBalloonSelection, nextPrimaryBalloon, preserveFloatingImageSelection: true);
        _editorState.SetFloatingImageSelection(nextImageSelection, nextPrimaryImage, preserveBalloonSelection: true);
    }


    private readonly struct SnapCandidate
    {
        public SnapCandidate(float delta, SmartGuideLine guide)
        {
            Delta = delta;
            Guide = guide;
        }

        public float Delta { get; }
        public SmartGuideLine Guide { get; }
        public float Distance => MathF.Abs(Delta);
        public SmartGuideKind Kind => Guide.Kind;
    }

    private readonly struct SnapResult
    {
        public SnapResult(Point2 offset, List<SmartGuideLine> guides)
        {
            Offset = offset;
            Guides = guides;
        }

        public Point2 Offset { get; }
        public List<SmartGuideLine> Guides { get; }
    }

    private readonly struct ResizeSnapResult
    {
        public ResizeSnapResult(float left, float top, float right, float bottom, List<SmartGuideLine> guides)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Guides = guides;
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }
        public List<SmartGuideLine> Guides { get; }
    }

    private void UpdateSnapFeedback(Point2 offset, Point2 anchor, IReadOnlyList<SmartGuideLine> guides)
    {
        if (MathF.Abs(offset.X) <= 0.01f && MathF.Abs(offset.Y) <= 0.01f)
        {
            _activeSnapFeedback = null;
            return;
        }

        var hasGuideSnap = guides.Any(g => g.Kind == SmartGuideKind.Guide);
        _activeSnapFeedback = new SnapFeedback(anchor, offset, hasGuideSnap);
    }

    private void ClearSnapFeedback()
    {
        _activeSnapFeedback = null;
    }

    private bool IsSnappingEnabled()
    {
        return (IsGuideSnappingEnabled() || IsGridSnappingEnabled()) && !IsSnapTemporarilyDisabled();
    }

    private bool IsGuideSnappingEnabled()
    {
        return _editorState.SnapToGuides;
    }

    private bool IsGridSnappingEnabled()
    {
        return _preferences.Canvas.SnapToGrid;
    }

    private bool IsGuideSnappingActive()
    {
        return IsGuideSnappingEnabled() && !IsSnapTemporarilyDisabled();
    }

    private static bool IsSnapTemporarilyDisabled()
    {
        return InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private Rect? ComputeSelectionBoundsForMove(Document doc, Point2 delta)
    {
        Rect? bounds = null;
        if (_editorState.DragBalloonOriginalPositions.Count > 0)
        {
            foreach (var entry in _editorState.DragBalloonOriginalPositions)
            {
                var balloon = doc.FindBalloon(entry.Key);
                if (balloon == null) continue;
                var newPos = entry.Value + delta;
                var newBounds = Rect.FromCenterSize(newPos, balloon.ComputedSize);
                bounds = bounds.HasValue ? bounds.Value.Union(newBounds) : newBounds;
            }
        }
        else if (_editorState.DragBalloonId.HasValue)
        {
            var balloon = doc.FindBalloon(_editorState.DragBalloonId.Value);
            if (balloon != null)
            {
                var newPos = _editorState.DragBalloonOriginalPosition + delta;
                bounds = Rect.FromCenterSize(newPos, balloon.ComputedSize);
            }
        }

        return bounds;
    }

    private SnapResult GetMoveSnapResult(Rect selectionBounds, HashSet<Guid> selectedIds)
    {
        var threshold = SnapThresholdScreen / _editorState.ViewTransform.Zoom;
        var anchorsX = new[] { selectionBounds.Left, selectionBounds.Center.X, selectionBounds.Right };
        var anchorsY = new[] { selectionBounds.Top, selectionBounds.Center.Y, selectionBounds.Bottom };

        var guideSnapEnabled = IsGuideSnappingEnabled();
        var gridSnapEnabled = IsGridSnappingEnabled();

        var guideTargetsX = new List<float>();
        var guideTargetsY = new List<float>();
        var alignTargetsX = new List<float>();
        var alignTargetsY = new List<float>();

        if (guideSnapEnabled)
        {
            CollectGuideTargets(_editorState.Document?.ActivePage, guideTargetsX, guideTargetsY);
            if (_editorState.Document != null)
            {
                CollectAlignmentTargets(_editorState.Document, selectedIds, alignTargetsX, alignTargetsY);
            }
        }

        var bestX = ChooseBestCandidate(
            guideSnapEnabled ? GetAxisSnapCandidate(anchorsX, guideTargetsX, threshold, GuideOrientation.Vertical, SmartGuideKind.Guide) : null,
            guideSnapEnabled ? GetAxisSnapCandidate(anchorsX, alignTargetsX, threshold, GuideOrientation.Vertical, SmartGuideKind.Alignment) : null,
            gridSnapEnabled ? GetGridSnapCandidate(anchorsX, threshold, GuideOrientation.Vertical) : null);

        var bestY = ChooseBestCandidate(
            guideSnapEnabled ? GetAxisSnapCandidate(anchorsY, guideTargetsY, threshold, GuideOrientation.Horizontal, SmartGuideKind.Guide) : null,
            guideSnapEnabled ? GetAxisSnapCandidate(anchorsY, alignTargetsY, threshold, GuideOrientation.Horizontal, SmartGuideKind.Alignment) : null,
            gridSnapEnabled ? GetGridSnapCandidate(anchorsY, threshold, GuideOrientation.Horizontal) : null);

        var guides = new List<SmartGuideLine>();
        var offsetX = 0f;
        var offsetY = 0f;

        if (bestX.HasValue)
        {
            offsetX = bestX.Value.Delta;
            guides.Add(bestX.Value.Guide);
        }

        if (bestY.HasValue)
        {
            offsetY = bestY.Value.Delta;
            guides.Add(bestY.Value.Guide);
        }

        return new SnapResult(new Point2(offsetX, offsetY), guides);
    }

    private SnapResult GetPanelMoveSnapResult(Rect panelBounds, Guid movingPanelId, DocumentPage page, IReadOnlyCollection<Guid>? excludedPanelIds = null)
    {
        var threshold = SnapThresholdScreen / _editorState.ViewTransform.Zoom;
        var gutter = MathF.Max(0f, page.PanelGutterWidth);
        var movingPanel = page.FindPanel(movingPanelId);

        float ResolveGutter(float? movingOverride, float? targetOverride)
        {
            var movingValue = movingOverride ?? gutter;
            var targetValue = targetOverride ?? gutter;
            return MathF.Max(movingValue, targetValue);
        }

        var leftTargets = new List<float>();
        var rightTargets = new List<float>();
        var topTargets = new List<float>();
        var bottomTargets = new List<float>();

        foreach (var panel in page.Panels)
        {
            if (panel.Id == movingPanelId) continue;
            if (excludedPanelIds != null && excludedPanelIds.Contains(panel.Id)) continue;
            if (!panel.IsVisible) continue;

            var bounds = panel.Bounds;
            leftTargets.Add(bounds.Right + ResolveGutter(movingPanel?.GutterLeftOverride, panel.GutterRightOverride));
            rightTargets.Add(bounds.Left - ResolveGutter(movingPanel?.GutterRightOverride, panel.GutterLeftOverride));
            topTargets.Add(bounds.Bottom + ResolveGutter(movingPanel?.GutterTopOverride, panel.GutterBottomOverride));
            bottomTargets.Add(bounds.Top - ResolveGutter(movingPanel?.GutterBottomOverride, panel.GutterTopOverride));
        }

        var guides = new List<SmartGuideLine>();
        var offsetX = 0f;
        var offsetY = 0f;

        var leftCandidate = GetAxisSnapCandidate(
            new[] { panelBounds.Left },
            leftTargets,
            threshold,
            GuideOrientation.Vertical,
            SmartGuideKind.Alignment);

        var rightCandidate = GetAxisSnapCandidate(
            new[] { panelBounds.Right },
            rightTargets,
            threshold,
            GuideOrientation.Vertical,
            SmartGuideKind.Alignment);

        var bestX = ChooseBestCandidate(leftCandidate, rightCandidate);
        if (bestX.HasValue)
        {
            offsetX = bestX.Value.Delta;
            guides.Add(bestX.Value.Guide);
        }

        var topCandidate = GetAxisSnapCandidate(
            new[] { panelBounds.Top },
            topTargets,
            threshold,
            GuideOrientation.Horizontal,
            SmartGuideKind.Alignment);

        var bottomCandidate = GetAxisSnapCandidate(
            new[] { panelBounds.Bottom },
            bottomTargets,
            threshold,
            GuideOrientation.Horizontal,
            SmartGuideKind.Alignment);

        var bestY = ChooseBestCandidate(topCandidate, bottomCandidate);
        if (bestY.HasValue)
        {
            offsetY = bestY.Value.Delta;
            guides.Add(bestY.Value.Guide);
        }

        return new SnapResult(new Point2(offsetX, offsetY), guides);
    }

    private ResizeSnapResult GetResizeSnapResult(float left, float top, float right, float bottom, ResizeHandle handle, HashSet<Guid> selectedIds)
    {
        var threshold = SnapThresholdScreen / _editorState.ViewTransform.Zoom;
        var moveLeft = handle == ResizeHandle.TopLeft || handle == ResizeHandle.BottomLeft;
        var moveRight = handle == ResizeHandle.TopRight || handle == ResizeHandle.BottomRight;
        var moveTop = handle == ResizeHandle.TopLeft || handle == ResizeHandle.TopRight;
        var moveBottom = handle == ResizeHandle.BottomLeft || handle == ResizeHandle.BottomRight;

        var guideSnapEnabled = IsGuideSnappingEnabled();
        var gridSnapEnabled = IsGridSnappingEnabled();

        var guideTargetsX = new List<float>();
        var guideTargetsY = new List<float>();
        var alignTargetsX = new List<float>();
        var alignTargetsY = new List<float>();
        if (guideSnapEnabled)
        {
            CollectGuideTargets(_editorState.Document?.ActivePage, guideTargetsX, guideTargetsY);
            if (_editorState.Document != null)
            {
                CollectAlignmentTargets(_editorState.Document, selectedIds, alignTargetsX, alignTargetsY);
            }
        }

        var guides = new List<SmartGuideLine>();

        if (moveLeft || moveRight)
        {
            var anchorX = moveLeft ? left : right;
            var anchorsX = new[] { anchorX };
            var bestX = ChooseBestCandidate(
                guideSnapEnabled ? GetAxisSnapCandidate(anchorsX, guideTargetsX, threshold, GuideOrientation.Vertical, SmartGuideKind.Guide) : null,
                guideSnapEnabled ? GetAxisSnapCandidate(anchorsX, alignTargetsX, threshold, GuideOrientation.Vertical, SmartGuideKind.Alignment) : null,
                gridSnapEnabled ? GetGridSnapCandidate(anchorsX, threshold, GuideOrientation.Vertical) : null);

            if (bestX.HasValue)
            {
                if (moveLeft)
                {
                    left += bestX.Value.Delta;
                }
                else
                {
                    right += bestX.Value.Delta;
                }
                guides.Add(bestX.Value.Guide);
            }
        }

        if (moveTop || moveBottom)
        {
            var anchorY = moveTop ? top : bottom;
            var anchorsY = new[] { anchorY };
            var bestY = ChooseBestCandidate(
                guideSnapEnabled ? GetAxisSnapCandidate(anchorsY, guideTargetsY, threshold, GuideOrientation.Horizontal, SmartGuideKind.Guide) : null,
                guideSnapEnabled ? GetAxisSnapCandidate(anchorsY, alignTargetsY, threshold, GuideOrientation.Horizontal, SmartGuideKind.Alignment) : null,
                gridSnapEnabled ? GetGridSnapCandidate(anchorsY, threshold, GuideOrientation.Horizontal) : null);

            if (bestY.HasValue)
            {
                if (moveTop)
                {
                    top += bestY.Value.Delta;
                }
                else
                {
                    bottom += bestY.Value.Delta;
                }
                guides.Add(bestY.Value.Guide);
            }
        }

        return new ResizeSnapResult(left, top, right, bottom, guides);
    }

    private static SnapCandidate? GetAxisSnapCandidate(IReadOnlyList<float> anchors, IReadOnlyList<float> targets, float threshold, GuideOrientation orientation, SmartGuideKind kind)
    {
        if (anchors.Count == 0 || targets.Count == 0) return null;

        SnapCandidate? best = null;
        foreach (var anchor in anchors)
        {
            foreach (var target in targets)
            {
                var delta = target - anchor;
                var distance = MathF.Abs(delta);
                if (distance > threshold) continue;

                if (!best.HasValue || distance < best.Value.Distance)
                {
                    best = new SnapCandidate(delta, new SmartGuideLine(orientation, target, kind));
                }
            }
        }

        return best;
    }

    private SnapCandidate? GetGridSnapCandidate(IReadOnlyList<float> anchors, float threshold, GuideOrientation orientation)
    {
        if (anchors.Count == 0) return null;
        var gridStep = GetGridMinorSpacingPixels();
        if (gridStep <= 0.001f) return null;

        SnapCandidate? best = null;
        foreach (var anchor in anchors)
        {
            var snapped = MathF.Round(anchor / gridStep) * gridStep;
            var delta = snapped - anchor;
            var distance = MathF.Abs(delta);
            if (distance > threshold) continue;

            if (!best.HasValue || distance < best.Value.Distance)
            {
                best = new SnapCandidate(delta, new SmartGuideLine(orientation, snapped, SmartGuideKind.Grid));
            }
        }

        return best;
    }

    private static SnapCandidate? ChooseBestCandidate(params SnapCandidate?[] candidates)
    {
        SnapCandidate? best = null;
        const float epsilon = 0.0001f;

        foreach (var candidate in candidates)
        {
            if (!candidate.HasValue) continue;

            if (!best.HasValue)
            {
                best = candidate;
                continue;
            }

            var distance = candidate.Value.Distance;
            var bestDistance = best.Value.Distance;

            if (distance + epsilon < bestDistance)
            {
                best = candidate;
                continue;
            }

            if (MathF.Abs(distance - bestDistance) <= epsilon &&
                GetSnapPriority(candidate.Value.Kind) < GetSnapPriority(best.Value.Kind))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static int GetSnapPriority(SmartGuideKind kind)
    {
        return kind switch
        {
            SmartGuideKind.Guide => 0,
            SmartGuideKind.Alignment => 1,
            _ => 2
        };
    }

    private static void CollectGuideTargets(DocumentPage? page, List<float> guideTargetsX, List<float> guideTargetsY)
    {
        if (page == null) return;

        foreach (var guide in page.Guides)
        {
            if (guide.Orientation == GuideOrientation.Horizontal)
            {
                guideTargetsY.Add(guide.Position);
            }
            else
            {
                guideTargetsX.Add(guide.Position);
            }
        }
    }

    private static void CollectAlignmentTargets(Document doc, HashSet<Guid> selectedIds, List<float> alignTargetsX, List<float> alignTargetsY)
    {
        foreach (var layer in doc.Layers)
        {
            if (!layer.IsVisible) continue;

            foreach (var balloon in layer.Balloons)
            {
                if (!balloon.IsVisible) continue;
                if (selectedIds.Contains(balloon.Id)) continue;

                var bounds = balloon.Bounds;
                alignTargetsX.Add(bounds.Left);
                alignTargetsX.Add(bounds.Center.X);
                alignTargetsX.Add(bounds.Right);
                alignTargetsY.Add(bounds.Top);
                alignTargetsY.Add(bounds.Center.Y);
                alignTargetsY.Add(bounds.Bottom);
            }
        }

        CollectPanelSnapTargets(doc, selectedIds, alignTargetsX, alignTargetsY);
    }

    private static void CollectPanelSnapTargets(Document doc, HashSet<Guid> selectedIds, List<float> alignTargetsX, List<float> alignTargetsY)
    {
        var page = doc.ActivePage;
        if (page == null || selectedIds.Count == 0) return;

        var panelIds = new HashSet<Guid>();
        foreach (var id in selectedIds)
        {
            var balloon = page.FindBalloon(id);
            if (balloon?.PanelId.HasValue == true)
            {
                panelIds.Add(balloon.PanelId.Value);
            }
        }

        if (panelIds.Count == 0) return;

        foreach (var panelId in panelIds)
        {
            var panel = page.FindPanel(panelId);
            if (panel == null) continue;

            AddPanelTargets(panel.Bounds, alignTargetsX, alignTargetsY);

            if (panel.SafeMargin > 0f)
            {
                var safeBounds = panel.Bounds.Inflate(-panel.SafeMargin, -panel.SafeMargin);
                if (safeBounds.Width > 1f && safeBounds.Height > 1f)
                {
                    AddPanelTargets(safeBounds, alignTargetsX, alignTargetsY);
                }
            }
        }
    }

    private static void AddPanelTargets(Rect bounds, List<float> alignTargetsX, List<float> alignTargetsY)
    {
        alignTargetsX.Add(bounds.Left);
        alignTargetsX.Add(bounds.Center.X);
        alignTargetsX.Add(bounds.Right);
        alignTargetsY.Add(bounds.Top);
        alignTargetsY.Add(bounds.Center.Y);
        alignTargetsY.Add(bounds.Bottom);
    }





    private void MainCanvas_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (_editorState.Mode == EditorMode.EditText)
        {
            if (HandleTextEditingKey(e, ctrl))
            {
                e.Handled = true;
                return;
            }

            return;
        }

        if (TryHandleConfiguredCanvasShortcut(e.Key, ctrl, shift, alt))
        {
            e.Handled = true;
            return;
        }

        if (ctrl && shift)
        {
            switch (e.Key)
            {
                case VirtualKey.C:
                    RepeatLastCreate();
                    e.Handled = true;
                    return;

                case VirtualKey.R:
                    RepeatLastAction();
                    e.Handled = true;
                    return;
            }
        }

        if (alt && !ctrl && !shift && _editorState.Mode != EditorMode.EditText)
        {
            if (TryGetPresetHotkeySlot(e.Key, out var templateSlot) && TryApplyBalloonTemplateHotkey(templateSlot))
            {
                e.Handled = true;
                return;
            }
        }

        if (_editorState.Mode != EditorMode.EditText)
        {
            if (TryHandleArrowNudgeResize(e.Key, ctrl, shift))
            {
                e.Handled = true;
                return;
            }
        }

        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            if (e.Key == VirtualKey.Escape)
            {
                _ = SetPanelLayoutModeAsync(false);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Delete)
            {
                TryDeleteFromCurrentContext();
                e.Handled = true;
                return;
            }
        }

        if (_editorState.Mode != EditorMode.EditText && _editorState.Mode != EditorMode.PanelLayout)
        {
            switch (e.Key)
            {
                case VirtualKey.Delete:
                    TryDeleteFromCurrentContext();
                    e.Handled = true;
                    return;

                case VirtualKey.Escape:
                    _editorState.SelectBalloon(null);
                    _editorState.ClearFloatingImageSelection();
                    MainCanvas.Invalidate();
                    e.Handled = true;
                    return;
            }
        }
    }

    private void MainCanvas_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_editorState.Mode != EditorMode.EditText) return;

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl || alt)
        {
            return;
        }

        var keyCode = (int)e.Character;
        if (keyCode < 0 || keyCode > 0x10FFFF)
        {
            return;
        }

        if (keyCode == '\r')
        {
            e.Handled = true;
            return;
        }

        var isControl = keyCode <= 0x1F || keyCode == 0x7F;
        if (!isControl || keyCode == '\n' || keyCode == '\t')
        {
            try
            {
                var text = char.ConvertFromUtf32(keyCode);
                _textCaretDesiredX = null;
                _editorState.InsertText(text);
                e.Handled = true;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }

    private static bool TryGetPresetHotkeySlot(VirtualKey key, out int slot)
    {
        slot = key switch
        {
            VirtualKey.Number1 or VirtualKey.NumberPad1 => 1,
            VirtualKey.Number2 or VirtualKey.NumberPad2 => 2,
            VirtualKey.Number3 or VirtualKey.NumberPad3 => 3,
            VirtualKey.Number4 or VirtualKey.NumberPad4 => 4,
            VirtualKey.Number5 or VirtualKey.NumberPad5 => 5,
            VirtualKey.Number6 or VirtualKey.NumberPad6 => 6,
            VirtualKey.Number7 or VirtualKey.NumberPad7 => 7,
            VirtualKey.Number8 or VirtualKey.NumberPad8 => 8,
            VirtualKey.Number9 or VirtualKey.NumberPad9 => 9,
            _ => 0
        };

        return slot >= 1;
    }

    private void ToggleTailOnSelectedBalloon()
    {
        if (_editorState.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;

        if (balloon.Tail != null)
        {
            _editorState.Execute(new DeleteTailCommand(balloon.Id));
        }
        else
        {
            Point2 tailTarget;
            if (_isPointerOverCanvas)
            {
                var worldPos = _editorState.ViewTransform.ScreenToWorld(_lastPointerPosition);
                var docBounds = new Rect(0, 0, _editorState.Document.Size.Width, _editorState.Document.Size.Height);
                if (docBounds.Contains(worldPos))
                {
                    tailTarget = worldPos;
                }
                else
                {
                    tailTarget = new Point2(balloon.Position.X, balloon.Position.Y + balloon.Bounds.Height + 50);
                }
            }
            else
            {
                tailTarget = new Point2(balloon.Position.X, balloon.Position.Y + balloon.Bounds.Height + 50);
            }
            var preferredTail = ResolvePreferredTailSettings();
            ExecuteCreateTailWithSettings(
                balloon.Id,
                tailTarget,
                preferredTail.style,
                preferredTail.width,
                preferredTail.curvature,
                preferredTail.curveCenter,
                preferredTail.inset);
        }

        UpdatePropertiesPanel();
    }

    private bool HandleTextEditingKey(KeyRoutedEventArgs e, bool ctrl)
    {
        if (ctrl)
        {
            switch (e.Key)
            {
                case VirtualKey.A:
                    _editorState.SelectAll();
                    return true;

                case VirtualKey.C:
                    CopySelectionToClipboard();
                    return true;

                case VirtualKey.X:
                    if (CopySelectionToClipboard())
                    {
                        _editorState.DeleteSelection();
                    }
                    return true;

                case VirtualKey.V:
                    _ = PasteTextFromClipboardAsync();
                    return true;

                case VirtualKey.Z:
                    _editorState.UndoTextEdit();
                    return true;

                case VirtualKey.Y:
                    _editorState.RedoTextEdit();
                    return true;

                case VirtualKey.B:
                    BoldToggleButton_Click(this, new RoutedEventArgs());
                    return true;

                case VirtualKey.I:
                    ItalicToggleButton_Click(this, new RoutedEventArgs());
                    return true;

                case VirtualKey.U:
                    UnderlineToggleButton_Click(this, new RoutedEventArgs());
                    return true;
            }
        }

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case VirtualKey.Escape:
                _editorState.ExitTextEditMode(saveChanges: true);
                return true;

            case VirtualKey.Enter:
                _editorState.InsertText("\n");
                return true;

            case VirtualKey.Back:
                _textCaretDesiredX = null;
                _editorState.DeleteCharacterBefore();
                return true;

            case VirtualKey.Delete:
                _textCaretDesiredX = null;
                _editorState.DeleteCharacterAfter();
                return true;

            case VirtualKey.Left:
                _textCaretDesiredX = null;
                _editorState.MoveCursorLeft(shift);
                return true;

            case VirtualKey.Right:
                _textCaretDesiredX = null;
                _editorState.MoveCursorRight(shift);
                return true;

            case VirtualKey.Up:
                MoveCursorVertical(moveDown: false, extendSelection: shift);
                return true;

            case VirtualKey.Down:
                MoveCursorVertical(moveDown: true, extendSelection: shift);
                return true;

            case VirtualKey.Home:
                _textCaretDesiredX = null;
                _editorState.MoveCursorHome(shift);
                return true;

            case VirtualKey.End:
                _textCaretDesiredX = null;
                _editorState.MoveCursorEnd(shift);
                return true;

            case VirtualKey.Tab:
                _textCaretDesiredX = null;
                _editorState.ExitTextEditMode(saveChanges: true);
                return true;

            default:
                break;
        }
        return false;
    }

    private static char? GetPunctuationChar(VirtualKey key)
    {
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        return key switch
        {
            (VirtualKey)190 => shift ? '>' : '.', // Period
            (VirtualKey)188 => shift ? '<' : ',', // Comma
            (VirtualKey)191 => shift ? '?' : '/', // Slash
            (VirtualKey)186 => shift ? ':' : ';', // Semicolon
            (VirtualKey)222 => shift ? '"' : '\'', // Quote
            (VirtualKey)219 => shift ? '{' : '[', // Left bracket
            (VirtualKey)221 => shift ? '}' : ']', // Right bracket
            (VirtualKey)220 => shift ? '|' : '\\', // Backslash
            (VirtualKey)189 => shift ? '_' : '-', // Minus
            (VirtualKey)187 => shift ? '+' : '=', // Equals
            (VirtualKey)192 => shift ? '~' : '`', // Backtick
            _ => null
        };
    }

    private bool CopySelectionToClipboard()
    {
        var selectedText = _editorState.GetSelectedText();
        if (string.IsNullOrEmpty(selectedText))
        {
            return false;
        }

        var data = new DataPackage();
        data.SetText(selectedText);
        Clipboard.SetContent(data);
        Clipboard.Flush();
        return true;
    }

    private async Task PasteTextFromClipboardAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
            {
                var text = await content.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    _editorState.InsertText(text);
                }
            }
        }
        catch
        {
        }
    }

    private int GetCaretIndexFromPoint(Balloon balloon, Point2 worldPoint)
    {
        if (_canvasDevice == null) return _editorState.EditingText.Length;

        var rawText = _editorState.EditingText;
        var displayText = balloon.TextStyle.AllCaps ? rawText.ToUpperInvariant() : rawText;
        if (displayText.Length == 0) return 0;

        if (ShouldUseShapeAwareLayout(balloon.Shape))
        {
            return GetCaretIndexFromPointShapeAware(balloon, worldPoint, displayText);
        }

        return GetCaretIndexFromPointStandard(balloon, worldPoint, displayText);
    }

    private int GetCaretIndexFromPointStandard(Balloon balloon, Point2 worldPoint, string displayText)
    {
        if (_canvasDevice == null) return 0;
        
        var textBounds = balloon.TextBounds;
        var spans = _editorState.EditingBalloonId == balloon.Id
            ? _editorState.EditingTextStyleSpans
            : balloon.TextStyleSpans;
        var allowFit = balloon.MaxTextWidth.HasValue;
        using var fitted = TextLayoutUtilities.CreateFittedTextLayout(
            _canvasDevice,
            displayText,
            balloon.TextStyle,
            spans,
            textBounds.Width,
            textBounds.Height,
            allowFit);
        var textLayout = fitted.Layout;
        var origin = TextLayoutUtilities.GetTextOrigin(textBounds, textLayout, balloon.TextStyle.VerticalOffset);
        var localPoint = new System.Numerics.Vector2(
            worldPoint.X - origin.X,
            worldPoint.Y - origin.Y);

        var regions = textLayout.GetCharacterRegions(0, displayText.Length);
        var regionList = regions as CanvasTextLayoutRegion[] ?? regions.ToArray();
        var fallbackHeight = fitted.EffectiveStyle.FontSize * 1.2f;
        var bestIndex = 0;
        var bestScore = float.MaxValue;

        for (int i = 0; i <= displayText.Length; i++)
        {
            if (!TextLayoutUtilities.TryGetCaretPosition(textLayout, displayText.Length, i, out var caretPos))
            {
                continue;
            }

            var caretHeight = fallbackHeight;
            if (regionList.Length > 0)
            {
                var regionIndex = i >= displayText.Length ? displayText.Length - 1 : i;
                regionIndex = Math.Clamp(regionIndex, 0, regionList.Length - 1);
                caretHeight = MathF.Max(1f, (float)regionList[regionIndex].LayoutBounds.Height);
            }

            var caretCenterY = (float)caretPos.Y + caretHeight * 0.5f;
            var dy = MathF.Abs(caretCenterY - localPoint.Y);
            var dx = MathF.Abs((float)caretPos.X - localPoint.X);
            var score = dy * 1000f + dx;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return Math.Clamp(bestIndex, 0, displayText.Length);
    }

    private int GetCaretIndexFromPointShapeAware(Balloon balloon, Point2 worldPoint, string displayText)
    {
        if (_canvasDevice == null) return 0;
        
        var balloonBounds = balloon.Bounds;
        var balloonStyle = balloon.BalloonStyle;
        var textStyle = balloon.TextStyle;
        var spans = _editorState.EditingBalloonId == balloon.Id
            ? _editorState.EditingTextStyleSpans
            : balloon.TextStyleSpans;
        var typographySettings = TextLayoutUtilities.CreateTypographySettings(textStyle);

        var shapeLayout = Letterist.Rendering.Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
            _canvasDevice,
            displayText,
            textStyle,
            balloon.Shape,
            balloonBounds,
            balloonStyle,
            typographySettings,
            spans);

        if (shapeLayout.Lines.Length == 0) return 0;

        var centerY = balloonBounds.Y + balloonBounds.Height / 2f;
        var textStartY = centerY - shapeLayout.TotalHeight / 2f + textStyle.VerticalOffset;

        var lineFormat = new CanvasTextFormat
        {
            FontFamily = textStyle.FontFamily,
            FontSize = textStyle.FontSize,
            FontWeight = textStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = textStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.NoWrap
        };

        if (!string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            lineFormat.LocaleName = textStyle.HyphenationLocale;
        }

        var bestIndex = 0;
        var bestScore = float.MaxValue;
        int charOffset = 0;
        float y = textStartY;

        foreach (var line in shapeLayout.Lines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                var lineLength = line.Text.Length;

                using var lineLayout = new CanvasTextLayout(_canvasDevice, line.Text, lineFormat, line.Width, line.Height);
                TextLayoutUtilities.ApplyTracking(lineLayout, textStyle, lineLength);
                TextLayoutUtilities.ApplyTypographyFeatures(lineLayout, lineLength, typographySettings);

                var lineX = TextLayoutUtilities.GetManualAlignedLineOriginX(
                    lineLayout,
                    balloonBounds.X + line.X,
                    line.Width,
                    textStyle.Alignment);

                var lineOrigin = new System.Numerics.Vector2(lineX, y);
                var localPoint = new System.Numerics.Vector2(
                    worldPoint.X - lineOrigin.X,
                    worldPoint.Y - lineOrigin.Y);

                for (int i = 0; i <= lineLength; i++)
                {
                    if (!TextLayoutUtilities.TryGetCaretPosition(lineLayout, lineLength, i, out var caretPos))
                    {
                        continue;
                    }

                    var caretHeight = textStyle.FontSize * 1.2f;
                    if (TextLayoutUtilities.TryGetCaretRegion(lineLayout, lineLength, i, out var caretBounds))
                    {
                        var caretRect = caretBounds.LayoutBounds;
                        caretHeight = MathF.Max(1f, (float)caretRect.Height);
                    }

                    var caretCenterY = (float)caretPos.Y + caretHeight * 0.5f;
                    var dy = MathF.Abs(caretCenterY - localPoint.Y);
                    var dx = MathF.Abs((float)caretPos.X - localPoint.X);
                    var score = dy * 1000f + dx;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestIndex = charOffset + i;
                    }
                }
            }

            charOffset += line.CharacterCount;
            y += line.Height;
        }

        return Math.Clamp(bestIndex, 0, displayText.Length);
    }

    private static bool ShouldUseShapeAwareLayout(BalloonShape shape)
    {
        return shape switch
        {
            BalloonShape.Oval => true,
            BalloonShape.Thought => true,
            BalloonShape.Splat => true,
            BalloonShape.Whisper => true,
            BalloonShape.Burst => true,
            _ => false
        };
    }

    private void MoveCursorVertical(bool moveDown, bool extendSelection)
    {
        if (_canvasDevice == null) return;
        if (_editorState.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;

        if (ShouldUseShapeAwareLayout(balloon.Shape))
        {
            MoveCursorVerticalShapeAware(moveDown, extendSelection);
        }
        else
        {
            MoveCursorVerticalStandard(moveDown, extendSelection);
        }
    }

    private void MoveCursorVerticalStandard(bool moveDown, bool extendSelection)
    {
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        var rawText = _editorState.EditingText;
        var displayText = balloon.TextStyle.AllCaps ? rawText.ToUpperInvariant() : rawText;
        var length = displayText.Length;
        if (length == 0)
        {
            _editorState.SetCursorPosition(0, extendSelection);
            return;
        }

        var textBounds = balloon.TextBounds;
        var spans = _editorState.EditingBalloonId == balloon.Id
            ? _editorState.EditingTextStyleSpans
            : balloon.TextStyleSpans;
        var allowFit = balloon.MaxTextWidth.HasValue;

        if (_canvasDevice == null) return;

        using var fitted = TextLayoutUtilities.CreateFittedTextLayout(
            _canvasDevice,
            displayText,
            balloon.TextStyle,
            spans,
            textBounds.Width,
            textBounds.Height,
            allowFit);

        var textLayout = fitted.Layout;
        var origin = TextLayoutUtilities.GetTextOrigin(textBounds, textLayout, balloon.TextStyle.VerticalOffset);
        var caretIndex = Math.Clamp(_editorState.EditingCursorPosition, 0, length);
        if (!TextLayoutUtilities.TryGetCaretPosition(textLayout, length, caretIndex, out var caretPos))
        {
            return;
        }

        var caretX = origin.X + caretPos.X;
        var caretY = origin.Y + caretPos.Y;

        if (!_textCaretDesiredX.HasValue)
        {
            _textCaretDesiredX = caretX;
        }

        var fallbackIndex = moveDown ? length : 0;
        var bestIndex = fallbackIndex;
        var bestVerticalDistance = float.MaxValue;
        var bestHorizontalDistance = float.MaxValue;
        var foundCandidate = false;
        var desiredX = _textCaretDesiredX.Value;

        for (int i = 0; i <= length; i++)
        {
            if (!TextLayoutUtilities.TryGetCaretPosition(textLayout, length, i, out var candidatePos))
            {
                continue;
            }

            var candidateX = origin.X + candidatePos.X;
            var candidateY = origin.Y + candidatePos.Y;

            if (TryPromoteVerticalCaretCandidate(
                moveDown,
                caretY,
                desiredX,
                i,
                candidateX,
                candidateY,
                ref bestVerticalDistance,
                ref bestHorizontalDistance,
                ref bestIndex))
            {
                foundCandidate = true;
            }
        }

        _editorState.SetCursorPosition(foundCandidate ? bestIndex : fallbackIndex, extendSelection);
    }

    private void MoveCursorVerticalShapeAware(bool moveDown, bool extendSelection)
    {
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        var rawText = _editorState.EditingText;
        var displayText = balloon.TextStyle.AllCaps ? rawText.ToUpperInvariant() : rawText;
        var length = displayText.Length;
        if (length == 0)
        {
            _editorState.SetCursorPosition(0, extendSelection);
            return;
        }

        var balloonBounds = balloon.Bounds;
        var balloonStyle = balloon.BalloonStyle;
        var textStyle = balloon.TextStyle;
        var spans = _editorState.EditingBalloonId == balloon.Id
            ? _editorState.EditingTextStyleSpans
            : balloon.TextStyleSpans;
        var typographySettings = TextLayoutUtilities.CreateTypographySettings(textStyle);

        if (_canvasDevice == null)
        {
            _editorState.SetCursorPosition(0, extendSelection);
            return;
        }

        var shapeLayout = Letterist.Rendering.Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
            _canvasDevice,
            displayText,
            textStyle,
            balloon.Shape,
            balloonBounds,
            balloonStyle,
            typographySettings,
            spans);

        if (shapeLayout.Lines.Length == 0)
        {
            _editorState.SetCursorPosition(0, extendSelection);
            return;
        }

        var centerY = balloonBounds.Y + balloonBounds.Height / 2f;
        var textStartY = centerY - shapeLayout.TotalHeight / 2f + textStyle.VerticalOffset;

        var lineFormat = new CanvasTextFormat
        {
            FontFamily = textStyle.FontFamily,
            FontSize = textStyle.FontSize,
            FontWeight = textStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = textStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.NoWrap
        };

        if (!string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            lineFormat.LocaleName = textStyle.HyphenationLocale;
        }

        var caretIndex = Math.Clamp(_editorState.EditingCursorPosition, 0, length);
        var currentCaretX = 0f;
        var currentCaretY = 0f;
        bool foundCurrent = false;

        int charOffset = 0;
        float y = textStartY;

        foreach (var line in shapeLayout.Lines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                var lineLength = line.Text.Length;
                var lineEnd = charOffset + lineLength;

                if (caretIndex >= charOffset && caretIndex <= lineEnd)
                {
                    using var lineLayout = new CanvasTextLayout(_canvasDevice, line.Text, lineFormat, line.Width, line.Height);
                    TextLayoutUtilities.ApplyTracking(lineLayout, textStyle, lineLength);
                    TextLayoutUtilities.ApplyTypographyFeatures(lineLayout, lineLength, typographySettings);

                    var lineX = TextLayoutUtilities.GetManualAlignedLineOriginX(
                        lineLayout,
                        balloonBounds.X + line.X,
                        line.Width,
                        textStyle.Alignment);

                    var lineOrigin = new System.Numerics.Vector2(lineX, y);
                    var localCaretPos = caretIndex - charOffset;

                    if (TextLayoutUtilities.TryGetCaretPosition(lineLayout, lineLength, localCaretPos, out var caretPos))
                    {
                        currentCaretX = lineOrigin.X + caretPos.X;
                        currentCaretY = lineOrigin.Y + caretPos.Y;
                        foundCurrent = true;
                    }

                    break;
                }

                charOffset = lineEnd;
            }

            y += line.Height;
        }

        if (!foundCurrent)
        {
            _editorState.SetCursorPosition(moveDown ? length : 0, extendSelection);
            return;
        }

        if (!_textCaretDesiredX.HasValue)
        {
            _textCaretDesiredX = currentCaretX;
        }

        var fallbackIndex = moveDown ? length : 0;
        var bestIndex = fallbackIndex;
        var bestVerticalDistance = float.MaxValue;
        var bestHorizontalDistance = float.MaxValue;
        var foundCandidate = false;
        var desiredX = _textCaretDesiredX.Value;

        charOffset = 0;
        y = textStartY;

        foreach (var line in shapeLayout.Lines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                var lineLength = line.Text.Length;

                using var lineLayout = new CanvasTextLayout(_canvasDevice, line.Text, lineFormat, line.Width, line.Height);
                TextLayoutUtilities.ApplyTracking(lineLayout, textStyle, lineLength);
                TextLayoutUtilities.ApplyTypographyFeatures(lineLayout, lineLength, typographySettings);

                var lineX = TextLayoutUtilities.GetManualAlignedLineOriginX(
                    lineLayout,
                    balloonBounds.X + line.X,
                    line.Width,
                    textStyle.Alignment);

                var lineOrigin = new System.Numerics.Vector2(lineX, y);

                for (int i = 0; i <= lineLength; i++)
                {
                    if (!TextLayoutUtilities.TryGetCaretPosition(lineLayout, lineLength, i, out var candidatePos))
                    {
                        continue;
                    }

                    var candidateX = lineOrigin.X + candidatePos.X;
                    var candidateY = lineOrigin.Y + candidatePos.Y;
                    var candidateIndex = charOffset + i;

                    if (TryPromoteVerticalCaretCandidate(
                        moveDown,
                        currentCaretY,
                        desiredX,
                        candidateIndex,
                        candidateX,
                        candidateY,
                        ref bestVerticalDistance,
                        ref bestHorizontalDistance,
                        ref bestIndex))
                    {
                        foundCandidate = true;
                    }
                }

                charOffset += lineLength;
            }

            y += line.Height;
        }

        _editorState.SetCursorPosition(foundCandidate ? bestIndex : fallbackIndex, extendSelection);
    }

    private static bool TryPromoteVerticalCaretCandidate(
        bool moveDown,
        float currentCaretY,
        float desiredX,
        int candidateIndex,
        float candidateX,
        float candidateY,
        ref float bestVerticalDistance,
        ref float bestHorizontalDistance,
        ref int bestIndex)
    {
        const float sameLineTolerance = 0.5f;
        const float tieTolerance = 0.01f;

        var verticalDelta = candidateY - currentCaretY;
        if (moveDown)
        {
            if (verticalDelta <= sameLineTolerance)
            {
                return false;
            }
        }
        else
        {
            if (verticalDelta >= -sameLineTolerance)
            {
                return false;
            }

            verticalDelta = -verticalDelta;
        }

        var horizontalDistance = MathF.Abs(candidateX - desiredX);
        var betterVertical = verticalDelta < bestVerticalDistance - tieTolerance;
        var tiedVertical = MathF.Abs(verticalDelta - bestVerticalDistance) <= tieTolerance;
        var betterHorizontal = horizontalDistance < bestHorizontalDistance - tieTolerance;
        var tiedHorizontal = MathF.Abs(horizontalDistance - bestHorizontalDistance) <= tieTolerance;

        if (!betterVertical &&
            !(tiedVertical && (betterHorizontal || (tiedHorizontal && candidateIndex < bestIndex))))
        {
            return false;
        }

        bestVerticalDistance = verticalDelta;
        bestHorizontalDistance = horizontalDistance;
        bestIndex = candidateIndex;
        return true;
    }

    private void SelectWordAtIndex(int index)
    {
        var text = _editorState.EditingText;
        if (string.IsNullOrEmpty(text)) return;

        index = Math.Clamp(index, 0, text.Length - 1);
        if (!IsWordChar(text[index])) return;

        var start = index;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var end = index;
        while (end < text.Length - 1 && IsWordChar(text[end + 1]))
        {
            end++;
        }

        _editorState.SetTextSelection(start, end - start + 1);
    }

    private static bool IsWordChar(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_' || value == '\'';
    }

    private async void DeleteSelectedBalloon()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var balloonIds = _editorState.SelectedBalloonIds.ToList();
        var imageIds = _editorState.SelectedFloatingImageIds.ToList();
        if (balloonIds.Count == 0 && imageIds.Count == 0) return;
        var selectionCount = balloonIds.Count + imageIds.Count;
        if (!await ConfirmMultiDeleteAsync(MultiDeleteItemKind.Objects, selectionCount))
        {
            return;
        }

        var commands = new List<ICommand>();
        foreach (var balloonId in balloonIds)
        {
            commands.Add(new DeleteBalloonCommand(balloonId));
        }

        foreach (var imageId in imageIds)
        {
            commands.Add(new DeleteFloatingImageCommand(page.Id, imageId));
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Delete objects", commands);
        }

        foreach (var imageId in imageIds)
        {
            _floatingImageBitmaps.Remove(imageId);
        }

        _editorState.SelectBalloon(null);
        _editorState.ClearFloatingImageSelection();
    }

    private void DeleteSelectedFloatingImage()
    {
        DeleteSelectedBalloon();
        MainCanvas.Invalidate();
    }

    private async void DeleteSelectedPanel()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var selectedIds = _editorState.SelectedPanelIds.ToList();
        if (selectedIds.Count == 0) return;

        var commands = new List<ICommand>();
        foreach (var id in selectedIds)
        {
            var panel = page.FindPanel(id);
            if (panel == null || panel.IsLocked) continue;
            commands.Add(new DeletePanelZoneCommand(page.Id, id));
        }

        if (commands.Count == 0) return;
        if (!await ConfirmMultiDeleteAsync(MultiDeleteItemKind.Panels, commands.Count))
        {
            return;
        }

        _editorState.ClearPanelSelection();
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Delete panels", commands);
        }

        SetStatusMessage(LF("input.status.deleted_panels", commands.Count));
    }

    private void RepeatLastAction()
    {
        if (_repeatLastAction == null)
        {
            SetStatusMessage(L("input.status.no_repeat"));
            return;
        }

        _repeatLastAction();
    }

    private void SetRepeatableAction(Action action)
    {
        _repeatLastAction = action;
    }

    private void RepeatLastCreate()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null)
        {
            SetStatusMessage(L("input.status.no_document"));
            return;
        }

        if (_lastCreatedBalloonId.HasValue)
        {
            var balloon = doc.FindBalloon(_lastCreatedBalloonId.Value);
            if (balloon == null)
            {
                _lastCreatedBalloonId = null;
            }
            else
            {
                var offset = new Point2(20f, 20f);
                var newId = Guid.NewGuid();
                var newPosition = balloon.Position + offset;

                var commands = new List<ICommand>
                {
                    new CreateBalloonCommand(
                        balloon.LayerId,
                        newPosition,
                        balloon.Text,
                        balloon.Shape,
                        balloon.BalloonStyle,
                        balloon.TextStyle,
                        newId,
                        panelId: balloon.PanelId,
                        constrainToPanel: balloon.ConstrainToPanel,
                        textPath: balloon.TextPath),
                    new ResizeBalloonCommand(newId, balloon.ComputedSize, newPosition)
                };

                if (balloon.TextStyleSpans.Count > 0)
                {
                    commands.Add(new SetBalloonRichTextCommand(newId, balloon.Text, balloon.TextStyleSpans));
                }

                if (!string.IsNullOrWhiteSpace(balloon.CustomShapePathData))
                {
                    commands.Add(new SetBalloonCustomShapeCommand(newId, balloon.CustomShapePathData));
                }

                if (balloon.Tail != null)
                {
                    commands.Add(new CreateTailCommand(
                        newId,
                        balloon.Tail.TargetPoint + offset,
                        balloon.Tail.Style,
                        balloon.Tail.BaseWidth));
                }

                _editorState.ExecuteTransaction("Repeat balloon create", commands);
                _editorState.SelectBalloon(newId);
                _lastCreatedBalloonId = newId;
                SetStatusMessage(L("input.status.repeated_balloon"));
                return;
            }
        }

        if (_lastCreatedPanelId.HasValue)
        {
            var panel = page.FindPanel(_lastCreatedPanelId.Value);
            if (panel == null)
            {
                _lastCreatedPanelId = null;
                SetStatusMessage(L("input.status.no_panel_repeat"));
                return;
            }

            var offset = new Point2(20f, 20f);
            var bounds = panel.Bounds;
            var newBounds = new Rect(
                bounds.X + offset.X,
                bounds.Y + offset.Y,
                bounds.Width,
                bounds.Height);
            newBounds = ClampPanelBoundsToPage(newBounds, page.Size);

            var nextOrder = page.Panels.Count + 1;
            var panelName = $"Panel {nextOrder}";
            var newPanelId = Guid.NewGuid();
            var commands = new List<ICommand>
            {
                new CreatePanelZoneCommand(
                    page.Id,
                    panelName,
                    newBounds,
                    nextOrder,
                    panel.Color,
                    newPanelId,
                    safeMargin: panel.SafeMargin,
                    borderColor: panel.BorderColor,
                    borderWidth: panel.BorderWidth,
                    borderStyle: panel.BorderStyle,
                    shape: panel.Shape,
                    cornerRadius: panel.CornerRadius,
                    customShapePathData: panel.CustomShapePathData)
            };

            if (panel.GutterLeftOverride.HasValue || panel.GutterTopOverride.HasValue ||
                panel.GutterRightOverride.HasValue || panel.GutterBottomOverride.HasValue)
            {
                commands.Add(new SetPanelGutterOverridesCommand(
                    page.Id,
                    newPanelId,
                    panel.GutterLeftOverride,
                    panel.GutterTopOverride,
                    panel.GutterRightOverride,
                    panel.GutterBottomOverride));
            }

            if (panel.BleedLeft > 0f || panel.BleedTop > 0f || panel.BleedRight > 0f || panel.BleedBottom > 0f)
            {
                commands.Add(new SetPanelBleedCommand(
                    page.Id,
                    newPanelId,
                    panel.BleedLeft,
                    panel.BleedTop,
                    panel.BleedRight,
                    panel.BleedBottom));
            }

            _editorState.ExecuteTransaction("Repeat panel create", commands);
            _editorState.SetPanelSelection(new[] { newPanelId }, newPanelId);
            _lastCreatedPanelId = newPanelId;
            SetStatusMessage(L("input.status.repeated_panel"));
            return;
        }

        SetStatusMessage(L("input.status.no_recent_create"));
    }

    private static Rect ClampPanelBoundsToPage(Rect bounds, Size2 pageSize)
    {
        var x = bounds.X;
        var y = bounds.Y;
        if (bounds.Left < 0f) x = 0f;
        if (bounds.Top < 0f) y = 0f;
        if (bounds.Right > pageSize.Width) x = pageSize.Width - bounds.Width;
        if (bounds.Bottom > pageSize.Height) y = pageSize.Height - bounds.Height;

        return new Rect(x, y, bounds.Width, bounds.Height);
    }

    private bool TryHandleArrowNudgeResize(VirtualKey key, bool ctrl, bool shift)
    {
        if (key != VirtualKey.Left && key != VirtualKey.Right && key != VirtualKey.Up && key != VirtualKey.Down)
        {
            return false;
        }

        var step = shift ? 10f : 1f;
        var deltaX = key == VirtualKey.Left ? -step : key == VirtualKey.Right ? step : 0f;
        var deltaY = key == VirtualKey.Up ? -step : key == VirtualKey.Down ? step : 0f;

        if (ctrl)
        {
            if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelIds.Count > 0)
            {
                return ResizeSelectedPanels(deltaX, deltaY);
            }

            if (_editorState.SelectedFloatingImageId.HasValue)
            {
                return ResizeSelectedFloatingImage(deltaX, deltaY);
            }

            if (_editorState.SelectedBalloonIds.Count > 0)
            {
                return ResizeSelectedBalloons(deltaX, deltaY);
            }

            return false;
        }

        if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelIds.Count > 0)
        {
            return NudgeSelectedPanels(new Point2(deltaX, deltaY));
        }

        if (_editorState.SelectedFloatingImageId.HasValue)
        {
            return NudgeSelectedFloatingImage(new Point2(deltaX, deltaY));
        }

        if (_editorState.SelectedBalloonIds.Count > 0)
        {
            return NudgeSelectedBalloons(new Point2(deltaX, deltaY));
        }

        return false;
    }

    private bool NudgeSelectedBalloons(Point2 delta)
    {
        var doc = _editorState.Document;
        if (doc == null || delta == Point2.Zero) return false;

        var ids = _editorState.SelectedBalloonIds.ToList();
        if (ids.Count == 0) return false;

        var commands = new List<ICommand>();
        foreach (var id in ids)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon == null) continue;

            var newPosition = balloon.Position + delta;
            newPosition = ConstrainBalloonPositionToPanel(doc, balloon, newPosition, balloon.ComputedSize);

            if (newPosition != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPosition));
            }
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            _editorState.ExecuteTransaction("Nudge balloons", commands);
        }
        SetRepeatableAction(() => NudgeSelectedBalloons(delta));
        MainCanvas.Invalidate();
        UpdatePropertiesPanel();
        return commands.Count > 0;
    }

    private bool NudgeSelectedFloatingImage(Point2 delta)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        var imageId = _editorState.SelectedFloatingImageId;
        if (doc == null || page == null || !imageId.HasValue || delta == Point2.Zero) return false;

        var image = page.FindFloatingImage(imageId.Value);
        if (image == null) return false;

        var newBounds = image.Bounds.Offset(delta);
        if (newBounds != image.Bounds)
        {
            _editorState.Execute(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
            SetRepeatableAction(() => NudgeSelectedFloatingImage(delta));
            MainCanvas.Invalidate();
            UpdatePropertiesPanel();
            return true;
        }

        return false;
    }

    private bool ResizeSelectedBalloons(float deltaWidth, float deltaHeight)
    {
        var doc = _editorState.Document;
        if (doc == null) return false;

        var ids = _editorState.SelectedBalloonIds.ToList();
        if (ids.Count == 0) return false;

        var commands = new List<ICommand>();
        foreach (var id in ids)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon == null) continue;

            var style = balloon.BalloonStyle;
            var minWidth = MathF.Max(4f, style.MinWidth);
            var minHeight = MathF.Max(4f, style.MinHeight);
            var newWidth = MathF.Max(minWidth, balloon.ComputedSize.Width + deltaWidth);
            var newHeight = MathF.Max(minHeight, balloon.ComputedSize.Height + deltaHeight);

            var newSize = new Size2(newWidth, newHeight);
            var newPosition = ConstrainBalloonPositionToPanel(doc, balloon, balloon.Position, newSize);

            if (newSize != balloon.ComputedSize || newPosition != balloon.Position)
            {
                commands.Add(new ResizeBalloonCommand(balloon.Id, newSize, newPosition));
            }
        }

        if (commands.Count > 0)
        {
            if (commands.Count == 1)
            {
                _editorState.Execute(commands[0]);
            }
            else
            {
                _editorState.ExecuteTransaction("Resize balloons", commands);
            }
            SetRepeatableAction(() => ResizeSelectedBalloons(deltaWidth, deltaHeight));
            MainCanvas.Invalidate();
            UpdatePropertiesPanel();
            return true;
        }

        return false;
    }

    private bool ResizeSelectedFloatingImage(float deltaWidth, float deltaHeight)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        var imageId = _editorState.SelectedFloatingImageId;
        if (doc == null || page == null || !imageId.HasValue) return false;

        var image = page.FindFloatingImage(imageId.Value);
        if (image == null) return false;

        var minSize = PanelMinSize;
        var newWidth = MathF.Max(minSize, image.Bounds.Width + deltaWidth);
        var newHeight = MathF.Max(minSize, image.Bounds.Height + deltaHeight);
        var newBounds = new Rect(image.Bounds.X, image.Bounds.Y, newWidth, newHeight);

        if (newBounds != image.Bounds)
        {
            _editorState.Execute(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
            SetRepeatableAction(() => ResizeSelectedFloatingImage(deltaWidth, deltaHeight));
            MainCanvas.Invalidate();
            UpdatePropertiesPanel();
            return true;
        }

        return false;
    }

    private bool NudgeSelectedPanels(Point2 delta)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null || delta == Point2.Zero) return false;

        var ids = _editorState.SelectedPanelIds.ToList();
        if (ids.Count == 0) return false;

        var panels = ids.Select(id => page.FindPanel(id)).Where(panel => panel != null).ToList();
        if (panels.Count == 0) return false;

        var boundsList = panels.Select(panel => panel!.Bounds).ToList();
        var selectionBounds = GetPanelBoundsWithDelta(boundsList, delta);
        var clamp = ClampPanelDeltaToPage(selectionBounds, page.Size, delta);
        var effectiveDelta = new Point2(delta.X + clamp.X, delta.Y + clamp.Y);

        var commands = new List<ICommand>();
        foreach (var panel in panels)
        {
            if (panel == null) continue;
            var newBounds = new Rect(
                panel.Bounds.X + effectiveDelta.X,
                panel.Bounds.Y + effectiveDelta.Y,
                panel.Bounds.Width,
                panel.Bounds.Height);
            if (newBounds != panel.Bounds)
            {
                commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
            }
        }

        ExecutePanelBoundsCommands("Nudge panels", commands);
        SetRepeatableAction(() => NudgeSelectedPanels(delta));
        MainCanvas.Invalidate();
        return commands.Count > 0;
    }

    private bool ResizeSelectedPanels(float deltaWidth, float deltaHeight)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null) return false;

        var ids = _editorState.SelectedPanelIds.ToList();
        if (ids.Count == 0) return false;

        var commands = new List<ICommand>();
        foreach (var id in ids)
        {
            var panel = page.FindPanel(id);
            if (panel == null) continue;

            var width = panel.Bounds.Width + deltaWidth;
            var height = panel.Bounds.Height + deltaHeight;

            if (TryGetPanelAspectRatio(panel, out var ratio))
            {
                if (Math.Abs(deltaWidth) > Math.Abs(deltaHeight))
                {
                    height = width / ratio;
                }
                else if (Math.Abs(deltaHeight) > 0f)
                {
                    width = height * ratio;
                }
            }

            width = MathF.Max(PanelMinSize, width);
            height = MathF.Max(PanelMinSize, height);

            var newBounds = Rect.FromCenterSize(panel.Bounds.Center, new Size2(width, height));
            newBounds = ClampPanelBoundsToPage(newBounds, page.Size);

            if (newBounds != panel.Bounds)
            {
                commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
            }
        }

        ExecutePanelBoundsCommands("Resize panels", commands);
        SetRepeatableAction(() => ResizeSelectedPanels(deltaWidth, deltaHeight));
        MainCanvas.Invalidate();
        return commands.Count > 0;
    }

    private bool CopySelectedBalloonsToClipboard()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return false;

        var balloons = GetSelectedBalloonClipboardItems(doc);
        var floatingImages = GetSelectedFloatingImageClipboardItems(page);
        if (balloons.Count == 0 && floatingImages.Count == 0) return false;

        var payload = new BalloonClipboardData
        {
            Balloons = balloons,
            FloatingImages = floatingImages
        };
        var json = JsonSerializer.Serialize(payload, ClipboardJsonOptions);

        var package = new DataPackage();
        package.SetData(BalloonClipboardFormat, json);
        package.Properties.ApplicationName = "Letterist";
        Clipboard.SetContent(package);

        _pasteOffsetIndex = 0;
        return true;
    }

    private void CutSelectedBalloonsToClipboard()
    {
        if (CopySelectedBalloonsToClipboard())
        {
            DeleteSelectedBalloon();
        }
    }

    private async Task PasteBalloonsFromClipboardAsync()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var content = Clipboard.GetContent();
        if (!content.Contains(BalloonClipboardFormat)) return;

        try
        {
            if (await content.GetDataAsync(BalloonClipboardFormat) is not string json)
            {
                return;
            }

            var payload = JsonSerializer.Deserialize<BalloonClipboardData>(json, ClipboardJsonOptions);
            if (payload == null) return;
            var balloons = payload.Balloons ?? new List<BalloonClipboardItem>();
            var floatingImages = payload.FloatingImages ?? new List<FloatingImageClipboardItem>();
            if (balloons.Count == 0 && floatingImages.Count == 0) return;

            _pasteOffsetIndex++;
            var offset = new Point2(20f * _pasteOffsetIndex, 20f * _pasteOffsetIndex);
            PasteObjectItems(balloons, floatingImages, offset);
        }
        catch
        {
        }
    }

    private void DuplicateSelectedBalloons()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var balloons = GetSelectedBalloonClipboardItems(doc);
        var floatingImages = GetSelectedFloatingImageClipboardItems(page);
        if (balloons.Count == 0 && floatingImages.Count == 0) return;

        PasteObjectItems(balloons, floatingImages, new Point2(20f, 20f));
        SetRepeatableAction(DuplicateSelectedBalloons);
    }

    private bool CopySelectedPanelsToClipboard()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return false;

        var panelIds = _editorState.SelectedPanelIds;
        if (panelIds.Count == 0) return false;

        var items = new List<PanelClipboardItem>();
        foreach (var id in panelIds)
        {
            var panel = page.FindPanel(id);
            if (panel != null)
            {
                items.Add(PanelClipboardItem.FromPanel(panel));
            }
        }

        if (items.Count == 0) return false;

        var payload = new PanelClipboardData { Panels = items };
        var json = JsonSerializer.Serialize(payload, ClipboardJsonOptions);

        var package = new DataPackage();
        package.SetData(PanelClipboardFormat, json);
        package.Properties.ApplicationName = "Letterist";
        Clipboard.SetContent(package);

        _pasteOffsetIndex = 0;
        return true;
    }

    private void CutSelectedPanelsToClipboard()
    {
        if (CopySelectedPanelsToClipboard())
        {
            DeleteSelectedPanel();
        }
    }

    private async Task PastePanelsFromClipboardAsync()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var content = Clipboard.GetContent();
        if (!content.Contains(PanelClipboardFormat)) return;

        try
        {
            if (await content.GetDataAsync(PanelClipboardFormat) is not string json) return;

            var payload = JsonSerializer.Deserialize<PanelClipboardData>(json, ClipboardJsonOptions);
            if (payload?.Panels == null || payload.Panels.Count == 0) return;

            _pasteOffsetIndex++;
            var offset = 20f * _pasteOffsetIndex;
            var commands = new List<ICommand>();
            var nextOrder = page.Panels.Count + 1;

            foreach (var item in payload.Panels)
            {
                var newBounds = new Rect(item.Bounds.X + offset, item.Bounds.Y + offset,
                                          item.Bounds.Width, item.Bounds.Height);
                commands.Add(new CreatePanelZoneCommand(
                    page.Id,
                    $"Panel {nextOrder}",
                    newBounds,
                    nextOrder++,
                    safeMargin: item.SafeMargin,
                    borderColor: item.BorderColor,
                    borderWidth: item.BorderWidth,
                    borderStyle: item.BorderStyle,
                    shape: item.Shape,
                    cornerRadius: item.CornerRadius,
                    customShapePathData: item.CustomShapePathData));
            }

            if (commands.Count == 1)
            {
                _editorState.Execute(commands[0]);
            }
            else
            {
                _editorState.ExecuteTransaction("Paste panels", commands);
            }

            var lastPanel = page.Panels.LastOrDefault();
            if (lastPanel != null)
            {
                _editorState.SelectPanel(lastPanel.Id);
            }

            RefreshLayerList();
            MainCanvas.Invalidate();
        }
        catch
        {
        }
    }

    private void SelectBalloonsInPanel(Guid panelId)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var balloons = page.AllBalloons.Where(b => b.PanelId == panelId).ToList();
        _editorState.SelectPanel(null);

        if (balloons.Count == 0)
        {
            _editorState.SelectBalloon(null);
            SetStatusMessage(L("input.status.no_balloons_in_panel"));
            return;
        }

        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            _ = SetPanelLayoutModeAsync(false);
        }

        var ids = balloons.Select(b => b.Id).ToList();
        _editorState.SetSelection(ids, ids[^1]);
        MainCanvas.Invalidate();
        SetStatusMessage(LF("input.status.selected_balloons_in_panel", ids.Count));
    }

    private void DuplicateSelectedBalloonsToPanel(Guid targetPanelId)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var targetPanel = page.FindPanel(targetPanelId);
        if (targetPanel == null) return;

        var balloons = GetSelectedBalloons();
        if (balloons.Count == 0) return;

        var selectionBounds = _editorState.GetSelectionBounds();
        var selectionCenter = selectionBounds?.Center ?? targetPanel.Bounds.Center;

        var commands = new List<ICommand>();
        var newIds = new List<Guid>();

        foreach (var balloon in balloons)
        {
            var sourcePanel = balloon.PanelId.HasValue ? page.FindPanel(balloon.PanelId.Value) : null;
            Point2 newPosition;

            if (sourcePanel != null)
            {
                var relative = balloon.Position - sourcePanel.Bounds.TopLeft;
                newPosition = targetPanel.Bounds.TopLeft + relative;
            }
            else
            {
                var relative = balloon.Position - selectionCenter;
                newPosition = targetPanel.Bounds.Center + relative;
            }

            var targetLayer = doc.FindLayer(balloon.LayerId);
            var targetLayerId = targetLayer != null && targetLayer.Kind == LayerKind.Balloon
                ? balloon.LayerId
                : doc.GetPreferredBalloonLayerId();
            if (targetLayerId == Guid.Empty) continue;

            var newId = Guid.NewGuid();
            newIds.Add(newId);

            commands.Add(new CreateBalloonCommand(
                targetLayerId,
                newPosition,
                balloon.Text,
                balloon.Shape,
                balloon.BalloonStyle,
                balloon.TextStyle,
                newId,
                panelId: targetPanelId,
                constrainToPanel: balloon.ConstrainToPanel,
                textPath: balloon.TextPath));

            if (balloon.BalloonStyleId.HasValue || balloon.BalloonStyleOverrides != null)
            {
                commands.Add(new SetBalloonStyleReferenceCommand(newId, balloon.BalloonStyleId, balloon.BalloonStyleOverrides));
            }

            if (balloon.TextStyleId.HasValue || balloon.TextStyleOverrides != null)
            {
                commands.Add(new SetTextStyleReferenceCommand(newId, balloon.TextStyleId, balloon.TextStyleOverrides));
            }

            commands.Add(new ResizeBalloonCommand(newId, balloon.ComputedSize, newPosition));

            if (balloon.TextStyleSpans.Count > 0)
            {
                commands.Add(new SetBalloonRichTextCommand(newId, balloon.Text, balloon.TextStyleSpans));
            }

            if (!string.IsNullOrWhiteSpace(balloon.CustomShapePathData))
            {
                commands.Add(new SetBalloonCustomShapeCommand(newId, balloon.CustomShapePathData));
            }

            if (balloon.Tail != null)
            {
                var offset = newPosition - balloon.Position;
                var newTarget = balloon.Tail.TargetPoint + offset;
                commands.Add(new CreateTailCommand(newId, newTarget, balloon.Tail.Style, balloon.Tail.BaseWidth));
            }
        }

        if (commands.Count == 0) return;

        _editorState.ExecuteTransaction("Duplicate balloons to panel", commands);
        _editorState.SetSelection(newIds, newIds[^1]);
        SetStatusMessage(LF("input.status.duplicated_to_panel", newIds.Count, targetPanel.Name));
    }

    private void SelectAllBalloonsOnActiveLayer()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        var activeLayer = doc?.ActiveLayer;
        if (doc == null || page == null || activeLayer == null) return;

        var balloonIds = activeLayer.Balloons.Select(b => b.Id).ToList();
        var imageIds = page.FloatingImages
            .Where(image => image.LayerId == activeLayer.Id)
            .Select(image => image.Id)
            .ToList();

        if (balloonIds.Count == 0 && imageIds.Count == 0)
        {
            _editorState.SelectBalloon(null);
            _editorState.ClearFloatingImageSelection();
            return;
        }

        if (balloonIds.Count > 0)
        {
            _editorState.SetSelection(balloonIds, balloonIds[^1], preserveFloatingImageSelection: true);
        }
        else
        {
            _editorState.SelectBalloon(null);
        }

        if (imageIds.Count > 0)
        {
            _editorState.SetFloatingImageSelection(imageIds, imageIds[^1], preserveBalloonSelection: true);
        }
        else
        {
            _editorState.ClearFloatingImageSelection();
        }
    }

    private void SelectAllPanelsOnActivePage()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var ids = page.Panels
            .Where(panel => panel.IsVisible && !panel.IsLocked)
            .OrderBy(panel => panel.Order)
            .Select(panel => panel.Id)
            .ToList();

        if (ids.Count == 0)
        {
            _editorState.ClearPanelSelection();
            return;
        }

        _editorState.SetPanelSelection(ids, ids[^1]);
    }

    private List<BalloonClipboardItem> GetSelectedBalloonClipboardItems(Document doc)
    {
        var items = new List<BalloonClipboardItem>();
        foreach (var id in _editorState.SelectedBalloonIds)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon != null)
            {
                items.Add(BalloonClipboardItem.FromBalloon(balloon));
            }
        }

        return items;
    }

    private List<FloatingImageClipboardItem> GetSelectedFloatingImageClipboardItems(DocumentPage page)
    {
        var items = new List<FloatingImageClipboardItem>();
        foreach (var id in _editorState.SelectedFloatingImageIds)
        {
            var image = page.FindFloatingImage(id);
            if (image != null)
            {
                items.Add(FloatingImageClipboardItem.FromFloatingImage(image));
            }
        }

        return items;
    }

    private void PasteObjectItems(
        IReadOnlyList<BalloonClipboardItem> balloonItems,
        IReadOnlyList<FloatingImageClipboardItem> floatingImageItems,
        Point2 offset)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;
        if (balloonItems.Count == 0 && floatingImageItems.Count == 0) return;

        var targetLayerId = doc.GetPreferredBalloonLayerId();
        if (balloonItems.Count > 0 && targetLayerId == Guid.Empty) return;

        var commands = new List<ICommand>();
        var newBalloonIds = new List<Guid>();
        var newImageIds = new List<Guid>();

        foreach (var item in balloonItems)
        {
            var newId = Guid.NewGuid();
            newBalloonIds.Add(newId);

            var newPosition = item.Position + offset;
            commands.Add(new CreateBalloonCommand(
                targetLayerId,
                newPosition,
                item.Text,
                item.Shape,
                item.BalloonStyle,
                item.TextStyle,
                newId,
                textPath: item.TextPath));

            if (item.BalloonStyleId.HasValue || item.BalloonStyleOverrides != null)
            {
                commands.Add(new SetBalloonStyleReferenceCommand(newId, item.BalloonStyleId, item.BalloonStyleOverrides));
            }

            if (item.TextStyleId.HasValue || item.TextStyleOverrides != null)
            {
                commands.Add(new SetTextStyleReferenceCommand(newId, item.TextStyleId, item.TextStyleOverrides));
            }

            commands.Add(new ResizeBalloonCommand(newId, item.ComputedSize, newPosition));

            if (item.TextStyleSpans.Count > 0)
            {
                commands.Add(new SetBalloonRichTextCommand(newId, item.Text, item.TextStyleSpans));
            }

            if (!string.IsNullOrWhiteSpace(item.CustomShapePathData))
            {
                commands.Add(new SetBalloonCustomShapeCommand(newId, item.CustomShapePathData));
            }

            if (item.Tail != null)
            {
                var newTarget = item.Tail.TargetPoint + offset;
                commands.Add(new CreateTailCommand(newId, newTarget, item.Tail.Style, item.Tail.BaseWidth));
            }
        }

        foreach (var item in floatingImageItems)
        {
            var newId = Guid.NewGuid();
            newImageIds.Add(newId);

            var shiftedBounds = new Rect(
                item.Bounds.X + offset.X,
                item.Bounds.Y + offset.Y,
                item.Bounds.Width,
                item.Bounds.Height);

            commands.Add(new CreateFloatingImageCommand(
                page.Id,
                item.ImagePath,
                shiftedBounds,
                item.Opacity,
                item.IsVisible,
                item.IsLocked,
                item.LayerId,
                newId,
                insertIndex: -1,
                name: item.Name,
                source: item.Source,
                rotation: item.Rotation,
                shadowEnabled: item.ShadowEnabled,
                shadowColor: item.ShadowColor,
                shadowOpacity: item.ShadowOpacity,
                shadowOffsetX: item.ShadowOffsetX,
                shadowOffsetY: item.ShadowOffsetY,
                shadowFalloff: item.ShadowFalloff,
                glowEnabled: item.GlowEnabled,
                glowColor: item.GlowColor,
                glowOpacity: item.GlowOpacity,
                glowSize: item.GlowSize));
        }

        if (commands.Count > 0)
        {
            _editorState.ExecuteTransaction("Paste objects", commands);

            if (newBalloonIds.Count > 0)
            {
                _editorState.SetSelection(newBalloonIds, newBalloonIds[^1], preserveFloatingImageSelection: true);
                _lastCreatedBalloonId = newBalloonIds[^1];
                _lastCreatedPanelId = null;
            }
            else
            {
                _editorState.SelectBalloon(null);
            }

            if (newImageIds.Count > 0)
            {
                _editorState.SetFloatingImageSelection(newImageIds, newImageIds[^1], preserveBalloonSelection: true);
                _ = LoadPastedFloatingImageBitmapsAsync(page);
            }
            else
            {
                _editorState.ClearFloatingImageSelection();
            }
        }
    }

    private async Task LoadPastedFloatingImageBitmapsAsync(DocumentPage page)
    {
        await EnsureFloatingImagesLoadedAsync(page);
        MainCanvas.Invalidate();
    }

    private List<Balloon> GetSelectedBalloons()
    {
        var doc = _editorState.Document;
        if (doc == null) return new List<Balloon>();

        var balloons = new List<Balloon>();
        foreach (var id in _editorState.SelectedBalloonIds)
        {
            var balloon = doc.FindBalloon(id);
            if (balloon != null)
            {
                balloons.Add(balloon);
            }
        }

        return balloons;
    }

    private void ExecuteMoveCommands(string description, List<ICommand> commands)
    {
        if (commands.Count == 0) return;
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction(description, commands);
        }
        MainCanvas.Invalidate();
        UpdatePropertiesPanel();
    }

    private void AlignSelectedLeft()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var left = balloons.Min(b => b.Bounds.Left);
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            var newX = left + balloon.Bounds.Width / 2;
            var newPos = new Point2(newX, balloon.Position.Y);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align left", commands);
    }

    private void AlignSelectedCenter()
    {
        var bounds = _editorState.GetSelectionBounds();
        if (!bounds.HasValue) return;

        var centerX = bounds.Value.Center.X;
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var commands = new List<ICommand>();
        foreach (var balloon in balloons)
        {
            var newPos = new Point2(centerX, balloon.Position.Y);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align center", commands);
    }

    private void AlignSelectedRight()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var right = balloons.Max(b => b.Bounds.Right);
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            var newX = right - balloon.Bounds.Width / 2;
            var newPos = new Point2(newX, balloon.Position.Y);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align right", commands);
    }

    private void AlignSelectedTop()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var top = balloons.Min(b => b.Bounds.Top);
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            var newY = top + balloon.Bounds.Height / 2;
            var newPos = new Point2(balloon.Position.X, newY);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align top", commands);
    }

    private void AlignSelectedMiddle()
    {
        var bounds = _editorState.GetSelectionBounds();
        if (!bounds.HasValue) return;

        var centerY = bounds.Value.Center.Y;
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var commands = new List<ICommand>();
        foreach (var balloon in balloons)
        {
            var newPos = new Point2(balloon.Position.X, centerY);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align middle", commands);
    }

    private void AlignSelectedBottom()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var bottom = balloons.Max(b => b.Bounds.Bottom);
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            var newY = bottom - balloon.Bounds.Height / 2;
            var newPos = new Point2(balloon.Position.X, newY);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Align bottom", commands);
    }

    private void DistributeSelectedHorizontally()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 3) return;

        var sorted = balloons.OrderBy(b => b.Position.X).ToList();
        var min = sorted.First().Position.X;
        var max = sorted.Last().Position.X;
        if (Math.Abs(max - min) < 0.01f) return;

        var step = (max - min) / (sorted.Count - 1);
        var commands = new List<ICommand>();

        for (int i = 0; i < sorted.Count; i++)
        {
            var balloon = sorted[i];
            var newPos = new Point2(min + step * i, balloon.Position.Y);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Distribute horizontally", commands);
    }

    private void DistributeSelectedVertically()
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count < 3) return;

        var sorted = balloons.OrderBy(b => b.Position.Y).ToList();
        var min = sorted.First().Position.Y;
        var max = sorted.Last().Position.Y;
        if (Math.Abs(max - min) < 0.01f) return;

        var step = (max - min) / (sorted.Count - 1);
        var commands = new List<ICommand>();

        for (int i = 0; i < sorted.Count; i++)
        {
            var balloon = sorted[i];
            var newPos = new Point2(balloon.Position.X, min + step * i);
            if (newPos != balloon.Position)
            {
                commands.Add(new MoveBalloonCommand(balloon.Id, newPos));
            }
        }

        ExecuteMoveCommands("Distribute vertically", commands);
    }

    private enum PanelArrangeOperation
    {
        AlignLeft,
        AlignCenter,
        AlignRight,
        AlignTop,
        AlignMiddle,
        AlignBottom,
        DistributeHorizontal,
        DistributeVertical,
        MatchWidth,
        MatchHeight,
        MatchSize
    }

    private bool TryGetPanelArrangeTargets(out DocumentPage page, out List<PanelZone> panels, int minCount = 2,
        IReadOnlyCollection<Guid>? panelIds = null, bool allowFallbackToAll = true, DocumentPage? pageOverride = null)
    {
        page = pageOverride ?? _editorState.Document?.ActivePage!;
        panels = new List<PanelZone>();
        if (page == null) return false;

        IReadOnlyCollection<Guid>? targetIds = panelIds;
        if (targetIds == null && _editorState.SelectedPanelIds.Count >= minCount)
        {
            targetIds = _editorState.SelectedPanelIds;
        }

        if (targetIds != null && targetIds.Count > 0)
        {
            foreach (var id in targetIds)
            {
                var panel = page.FindPanel(id);
                if (panel != null && panel.IsVisible && !panel.IsLocked)
                {
                    panels.Add(panel);
                }
            }
        }

        if (panels.Count < minCount && allowFallbackToAll && panelIds == null)
        {
            panels = page.Panels
                .Where(p => p.IsVisible && !p.IsLocked)
                .OrderBy(p => p.Order)
                .ToList();
        }

        return panels.Count >= minCount;
    }

    private static Rect GetPanelsBounds(IReadOnlyList<PanelZone> panels)
    {
        var bounds = panels[0].Bounds;
        for (int i = 1; i < panels.Count; i++)
        {
            bounds = bounds.Union(panels[i].Bounds);
        }
        return bounds;
    }

    private static PanelZone GetPanelArrangeReference(IReadOnlyList<PanelZone> panels, Guid? referencePanelId)
    {
        if (referencePanelId.HasValue)
        {
            var match = panels.FirstOrDefault(p => p.Id == referencePanelId.Value);
            if (match != null) return match;
        }

        return panels[0];
    }

    private static List<ICommand> BuildPanelArrangeCommands(DocumentPage page, IReadOnlyList<PanelZone> panels,
        PanelArrangeOperation operation, Guid? referencePanelId = null)
    {
        var commands = new List<ICommand>();
        if (panels.Count == 0) return commands;

        switch (operation)
        {
            case PanelArrangeOperation.AlignLeft:
                {
                    var left = panels.Min(p => p.Bounds.Left);
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newBounds = new Rect(left, bounds.Y, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.AlignCenter:
                {
                    var union = GetPanelsBounds(panels);
                    var centerX = union.Center.X;
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newX = centerX - bounds.Width / 2f;
                        var newBounds = new Rect(newX, bounds.Y, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.AlignRight:
                {
                    var right = panels.Max(p => p.Bounds.Right);
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newX = right - bounds.Width;
                        var newBounds = new Rect(newX, bounds.Y, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.AlignTop:
                {
                    var top = panels.Min(p => p.Bounds.Top);
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newBounds = new Rect(bounds.X, top, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.AlignMiddle:
                {
                    var union = GetPanelsBounds(panels);
                    var centerY = union.Center.Y;
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newY = centerY - bounds.Height / 2f;
                        var newBounds = new Rect(bounds.X, newY, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.AlignBottom:
                {
                    var bottom = panels.Max(p => p.Bounds.Bottom);
                    foreach (var panel in panels)
                    {
                        var bounds = panel.Bounds;
                        var newY = bottom - bounds.Height;
                        var newBounds = new Rect(bounds.X, newY, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.DistributeHorizontal:
                {
                    var sorted = panels.OrderBy(p => p.Bounds.Left).ToList();
                    var minLeft = sorted.First().Bounds.Left;
                    var maxRight = sorted.Last().Bounds.Right;
                    var totalWidth = sorted.Sum(p => p.Bounds.Width);
                    var available = MathF.Max(0f, maxRight - minLeft - totalWidth);
                    var gap = available / (sorted.Count - 1);

                    var x = minLeft;
                    foreach (var panel in sorted)
                    {
                        var bounds = panel.Bounds;
                        var newBounds = new Rect(x, bounds.Y, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                        x += bounds.Width + gap;
                    }
                    break;
                }
            case PanelArrangeOperation.DistributeVertical:
                {
                    var sorted = panels.OrderBy(p => p.Bounds.Top).ToList();
                    var minTop = sorted.First().Bounds.Top;
                    var maxBottom = sorted.Last().Bounds.Bottom;
                    var totalHeight = sorted.Sum(p => p.Bounds.Height);
                    var available = MathF.Max(0f, maxBottom - minTop - totalHeight);
                    var gap = available / (sorted.Count - 1);

                    var y = minTop;
                    foreach (var panel in sorted)
                    {
                        var bounds = panel.Bounds;
                        var newBounds = new Rect(bounds.X, y, bounds.Width, bounds.Height);
                        if (newBounds != bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                        y += bounds.Height + gap;
                    }
                    break;
                }
            case PanelArrangeOperation.MatchWidth:
                {
                    var reference = GetPanelArrangeReference(panels, referencePanelId);
                    var targetWidth = MathF.Max(PanelMinSize, reference.Bounds.Width);
                    foreach (var panel in panels)
                    {
                        if (panel.Id == reference.Id) continue;
                        var size = new Size2(targetWidth, MathF.Max(PanelMinSize, panel.Bounds.Height));
                        var newBounds = Rect.FromCenterSize(panel.Bounds.Center, size);
                        if (newBounds != panel.Bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.MatchHeight:
                {
                    var reference = GetPanelArrangeReference(panels, referencePanelId);
                    var targetHeight = MathF.Max(PanelMinSize, reference.Bounds.Height);
                    foreach (var panel in panels)
                    {
                        if (panel.Id == reference.Id) continue;
                        var size = new Size2(MathF.Max(PanelMinSize, panel.Bounds.Width), targetHeight);
                        var newBounds = Rect.FromCenterSize(panel.Bounds.Center, size);
                        if (newBounds != panel.Bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
            case PanelArrangeOperation.MatchSize:
                {
                    var reference = GetPanelArrangeReference(panels, referencePanelId);
                    var targetWidth = MathF.Max(PanelMinSize, reference.Bounds.Width);
                    var targetHeight = MathF.Max(PanelMinSize, reference.Bounds.Height);
                    foreach (var panel in panels)
                    {
                        if (panel.Id == reference.Id) continue;
                        var size = new Size2(targetWidth, targetHeight);
                        var newBounds = Rect.FromCenterSize(panel.Bounds.Center, size);
                        if (newBounds != panel.Bounds)
                        {
                            commands.Add(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));
                        }
                    }
                    break;
                }
        }

        return commands;
    }

    private void ExecutePanelBoundsCommands(string description, List<ICommand> commands)
    {
        if (commands.Count == 0) return;
        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction(description, commands);
        }
    }

    private void AlignPanelsLeft()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignLeft);
        ExecutePanelBoundsCommands("Align panels left", commands);
        MainCanvas.Invalidate();
    }

    private void AlignPanelsCenter()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignCenter);
        ExecutePanelBoundsCommands("Align panels center", commands);
        MainCanvas.Invalidate();
    }

    private void AlignPanelsRight()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignRight);
        ExecutePanelBoundsCommands("Align panels right", commands);
        MainCanvas.Invalidate();
    }

    private void AlignPanelsTop()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignTop);
        ExecutePanelBoundsCommands("Align panels top", commands);
        MainCanvas.Invalidate();
    }

    private void AlignPanelsMiddle()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignMiddle);
        ExecutePanelBoundsCommands("Align panels middle", commands);
        MainCanvas.Invalidate();
    }

    private void AlignPanelsBottom()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.AlignBottom);
        ExecutePanelBoundsCommands("Align panels bottom", commands);
        MainCanvas.Invalidate();
    }

    private void DistributePanelsHorizontally()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels, minCount: 3)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.DistributeHorizontal);
        ExecutePanelBoundsCommands("Distribute panels horizontally", commands);
        MainCanvas.Invalidate();
    }

    private void DistributePanelsVertically()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels, minCount: 3)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.DistributeVertical);
        ExecutePanelBoundsCommands("Distribute panels vertically", commands);
        MainCanvas.Invalidate();
    }

    private void MatchPanelWidths()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.MatchWidth, _editorState.SelectedPanelId);
        ExecutePanelBoundsCommands("Match panel widths", commands);
        MainCanvas.Invalidate();
    }

    private void MatchPanelHeights()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.MatchHeight, _editorState.SelectedPanelId);
        ExecutePanelBoundsCommands("Match panel heights", commands);
        MainCanvas.Invalidate();
    }

    private void MatchPanelSizes()
    {
        if (!TryGetPanelArrangeTargets(out var page, out var panels)) return;
        var commands = BuildPanelArrangeCommands(page, panels, PanelArrangeOperation.MatchSize, _editorState.SelectedPanelId);
        ExecutePanelBoundsCommands("Match panel sizes", commands);
        MainCanvas.Invalidate();
    }

    private void LinkSelectedBalloons()
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var primaryId = doc.SelectedBalloonId ?? balloons[0].Id;
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            if (balloon.Id == primaryId) continue;
            if (!doc.ActivePage.AreBalloonsLinked(primaryId, balloon.Id))
            {
                commands.Add(new LinkBalloonsCommand(primaryId, balloon.Id));
            }
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            _editorState.ExecuteTransaction("Link balloons", commands);
        }
    }

    private void UnlinkSelectedBalloons()
    {
        var doc = _editorState.Document;
        if (doc?.ActivePage == null) return;

        var balloons = GetSelectedBalloons();
        if (balloons.Count < 2) return;

        var primaryId = doc.SelectedBalloonId ?? balloons[0].Id;
        var commands = new List<ICommand>();

        foreach (var balloon in balloons)
        {
            if (balloon.Id == primaryId) continue;
            if (doc.ActivePage.AreBalloonsLinked(primaryId, balloon.Id))
            {
                commands.Add(new UnlinkBalloonsCommand(primaryId, balloon.Id));
            }
        }

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else if (commands.Count > 1)
        {
            _editorState.ExecuteTransaction("Unlink balloons", commands);
        }
    }



    private bool HasSelectionForPanel()
    {
        return _editorState.GetSelectionBounds().HasValue || _lastMarqueeSelectionBounds.HasValue;
    }

    private void CreatePanelFromSelectionBounds()
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var bounds = _editorState.GetSelectionBounds() ?? _lastMarqueeSelectionBounds;
        if (!bounds.HasValue)
        {
            SetStatusMessage(L("input.status.no_selection_bounds"));
            return;
        }

        var clamped = ClampRectToPage(bounds.Value, doc.Size);
        clamped = EnsureMinimumPanelBounds(clamped);
        if (clamped.Width < PanelMinSize || clamped.Height < PanelMinSize)
        {
            SetStatusMessage(L("input.status.selection_too_small"));
            return;
        }

        var nextOrder = page.Panels.Count + 1;
        var panelName = $"Panel {nextOrder}";
        var cmd = new CreatePanelZoneCommand(
            page.Id,
            panelName,
            clamped,
            nextOrder,
            safeMargin: GetPanelDefaultSafeMargin(),
            borderColor: GetPanelDefaultBorderColor(),
            borderWidth: GetPanelDefaultBorderWidth(),
            borderStyle: GetPanelDefaultBorderStyle());
        _editorState.Execute(cmd);

        var created = page.FindPanel(cmd.CreatedPanelId);
        if (created != null)
        {
            _editorState.SelectPanel(created.Id);
            _lastCreatedPanelId = created.Id;
            _lastCreatedBalloonId = null;
        }

        SetStatusMessage(L("input.status.created_panel"));
        RefreshPanelList();
        MainCanvas.Invalidate();
    }

    private async Task ImportPanelMaskAsync(Point2 worldPos)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (doc == null || page == null) return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var pixels = pixelData.DetachPixelData();
            var width = (int)decoder.PixelWidth;
            var height = (int)decoder.PixelHeight;

            if (width <= 0 || height <= 0)
            {
                SetStatusMessage(L("input.status.mask_empty"));
                return;
            }

            var mask = new bool[width, height];
            var hasAny = false;
            const byte alphaThreshold = 16;
            for (var y = 0; y < height; y++)
            {
                var rowOffset = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var alpha = pixels[rowOffset + x * 4 + 3];
                    if (alpha > alphaThreshold)
                    {
                        mask[x, y] = true;
                        hasAny = true;
                    }
                }
            }

            if (!hasAny)
            {
                SetStatusMessage(L("input.status.mask_no_pixels"));
                return;
            }

            var outline = ExtractMaskOutline(mask, width, height);
            if (outline.Count < 3)
            {
                SetStatusMessage(L("input.status.mask_no_outline"));
                return;
            }

            var scale = 1f;
            if (width > doc.Size.Width || height > doc.Size.Height)
            {
                scale = MathF.Min(doc.Size.Width / width, doc.Size.Height / height);
            }

            var points = outline.Select(point => new Point2(
                worldPos.X + point.X * scale,
                worldPos.Y + point.Y * scale)).ToList();

            CreateCustomPanelFromPoints(points, smooth: false);
            RefreshPanelList();
            MainCanvas.Invalidate();
            SetStatusMessage(L("input.status.mask_created"));
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("input.status.mask_failed", ex.Message));
        }
    }

    private static List<Point2> ExtractMaskOutline(bool[,] mask, int width, int height)
    {
        var edges = new Dictionary<IntPoint, IntPoint>();

        bool IsInside(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height && mask[x, y];
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!mask[x, y]) continue;

                if (!IsInside(x, y - 1))
                {
                    edges[new IntPoint(x, y)] = new IntPoint(x + 1, y);
                }
                if (!IsInside(x + 1, y))
                {
                    edges[new IntPoint(x + 1, y)] = new IntPoint(x + 1, y + 1);
                }
                if (!IsInside(x, y + 1))
                {
                    edges[new IntPoint(x + 1, y + 1)] = new IntPoint(x, y + 1);
                }
                if (!IsInside(x - 1, y))
                {
                    edges[new IntPoint(x, y + 1)] = new IntPoint(x, y);
                }
            }
        }

        if (edges.Count == 0) return new List<Point2>();

        var loops = new List<List<IntPoint>>();
        var visited = new HashSet<IntPoint>();

        foreach (var start in edges.Keys.ToList())
        {
            if (visited.Contains(start)) continue;

            var loop = new List<IntPoint>();
            var current = start;
            while (!visited.Contains(current) && edges.TryGetValue(current, out var next))
            {
                visited.Add(current);
                loop.Add(current);
                current = next;
                if (current.Equals(start))
                {
                    break;
                }
            }

            if (loop.Count > 0)
            {
                loops.Add(loop);
            }
        }

        var largest = loops.OrderByDescending(loop => loop.Count).FirstOrDefault();
        if (largest == null) return new List<Point2>();

        return largest.Select(point => new Point2(point.X, point.Y)).ToList();
    }

    private readonly struct IntPoint : IEquatable<IntPoint>
    {
        public IntPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(IntPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is IntPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    private void ShowContextMenu(Point2 screenPos, Point2 worldPos, Windows.Foundation.Point pointerPos)
    {
        var menu = new MenuFlyout();

        if (_editorState.Mode == EditorMode.PanelLayout)
        {
            var hitPanel = _editorState.HitTestPanel(screenPos);
            if (hitPanel != null)
            {
                if (_editorState.SelectedPanelIds.Contains(hitPanel.Id))
                {
                    _editorState.SetPrimaryPanelSelection(hitPanel.Id);
                }
                else
                {
                    _editorState.SelectPanel(hitPanel.Id);
                }

                var duplicateItem = new MenuFlyoutItem { Text = L("ctx.panel.duplicate"), Icon = new FontIcon { Glyph = "\uE8C8" } };
                duplicateItem.Click += (s, e) => DuplicateSelectedPanel();
                menu.Items.Add(duplicateItem);

                if (hitPanel.Shape == PanelShape.Custom)
                {
                    var editShapeItem = new MenuFlyoutItem { Text = L("ctx.panel.edit_shape"), Icon = new FontIcon { Glyph = "\uE8E3" } };
                    editShapeItem.Click += (s, e) => EnterPanelShapeEdit(hitPanel);
                    menu.Items.Add(editShapeItem);
                }

                var deleteItem = new MenuFlyoutItem { Text = L("ctx.panel.delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
                deleteItem.Click += (s, e) => DeleteSelectedPanel();
                menu.Items.Add(deleteItem);

                var selectBalloonsItem = new MenuFlyoutItem { Text = L("ctx.panel.select_balloons"), Icon = new FontIcon { Glyph = "\uE8EF" } };
                selectBalloonsItem.Click += (s, e) => SelectBalloonsInPanel(hitPanel.Id);
                menu.Items.Add(selectBalloonsItem);

                menu.Items.Add(new MenuFlyoutSeparator());

                var loadImageItem = new MenuFlyoutItem { Text = L("ctx.panel.load_image"), Icon = new FontIcon { Glyph = "\uE8B9" } };
                loadImageItem.Click += async (s, e) => await LoadImageIntoPanelAsync(hitPanel.Id);
                menu.Items.Add(loadImageItem);

                menu.Items.Add(new MenuFlyoutSeparator());

                var orderSubMenu = new MenuFlyoutSubItem { Text = L("ctx.panel.reading_order") };

                var bringForwardItem = new MenuFlyoutItem { Text = L("ctx.panel.bring_forward") };
                bringForwardItem.Click += (s, e) => ChangePanelOrder(hitPanel.Id, delta: 1);
                orderSubMenu.Items.Add(bringForwardItem);

                var sendBackwardItem = new MenuFlyoutItem { Text = L("ctx.panel.send_backward") };
                sendBackwardItem.Click += (s, e) => ChangePanelOrder(hitPanel.Id, delta: -1);
                orderSubMenu.Items.Add(sendBackwardItem);

                menu.Items.Add(orderSubMenu);

                menu.Items.Add(new MenuFlyoutSeparator());

                var splitItem = new MenuFlyoutItem { Text = L("ctx.panel.split") };
                splitItem.Click += async (s, e) => await ShowSplitPanelDialogAsync(hitPanel);
                menu.Items.Add(splitItem);

                var mergeItem = new MenuFlyoutItem { Text = L("ctx.panel.merge_adjacent") };
                mergeItem.Click += async (s, e) => await ShowMergePanelDialogAsync(hitPanel);
                menu.Items.Add(mergeItem);

                menu.Items.Add(new MenuFlyoutSeparator());

                var alignSubMenu = new MenuFlyoutSubItem { Text = L("ctx.panel.align") };
                var alignLeft = new MenuFlyoutItem { Text = L("align.left") };
                alignLeft.Click += (s, e) => AlignPanelsLeft();
                alignSubMenu.Items.Add(alignLeft);
                var alignCenter = new MenuFlyoutItem { Text = L("align.center") };
                alignCenter.Click += (s, e) => AlignPanelsCenter();
                alignSubMenu.Items.Add(alignCenter);
                var alignRight = new MenuFlyoutItem { Text = L("align.right") };
                alignRight.Click += (s, e) => AlignPanelsRight();
                alignSubMenu.Items.Add(alignRight);
                alignSubMenu.Items.Add(new MenuFlyoutSeparator());
                var alignTop = new MenuFlyoutItem { Text = L("align.top") };
                alignTop.Click += (s, e) => AlignPanelsTop();
                alignSubMenu.Items.Add(alignTop);
                var alignMiddle = new MenuFlyoutItem { Text = L("align.middle") };
                alignMiddle.Click += (s, e) => AlignPanelsMiddle();
                alignSubMenu.Items.Add(alignMiddle);
                var alignBottom = new MenuFlyoutItem { Text = L("align.bottom") };
                alignBottom.Click += (s, e) => AlignPanelsBottom();
                alignSubMenu.Items.Add(alignBottom);
                menu.Items.Add(alignSubMenu);

                var distributeSubMenu = new MenuFlyoutSubItem { Text = L("ctx.panel.distribute") };
                var distributeH = new MenuFlyoutItem { Text = L("distribute.horizontally") };
                distributeH.Click += (s, e) => DistributePanelsHorizontally();
                distributeSubMenu.Items.Add(distributeH);
                var distributeV = new MenuFlyoutItem { Text = L("distribute.vertically") };
                distributeV.Click += (s, e) => DistributePanelsVertically();
                distributeSubMenu.Items.Add(distributeV);
                menu.Items.Add(distributeSubMenu);

                var matchSubMenu = new MenuFlyoutSubItem { Text = L("ctx.panel.match_size") };
                var matchWidth = new MenuFlyoutItem { Text = L("common.width") };
                matchWidth.Click += (s, e) => MatchPanelWidths();
                matchSubMenu.Items.Add(matchWidth);
                var matchHeight = new MenuFlyoutItem { Text = L("common.height") };
                matchHeight.Click += (s, e) => MatchPanelHeights();
                matchSubMenu.Items.Add(matchHeight);
                var matchBoth = new MenuFlyoutItem { Text = L("common.width_and_height") };
                matchBoth.Click += (s, e) => MatchPanelSizes();
                matchSubMenu.Items.Add(matchBoth);
                menu.Items.Add(matchSubMenu);

                menu.Items.Add(new MenuFlyoutSeparator());

                var selectAllPanelsItem = new MenuFlyoutItem { Text = L("ctx.panel.select_all") };
                selectAllPanelsItem.Click += (s, e) => SelectAllPanelsOnActivePage();
                menu.Items.Add(selectAllPanelsItem);

                var clearPanelsItem = new MenuFlyoutItem { Text = L("ctx.panel.clear_selection") };
                clearPanelsItem.Click += (s, e) => _editorState.ClearPanelSelection();
                menu.Items.Add(clearPanelsItem);
            }
            else
            {
                var exitItem = new MenuFlyoutItem { Text = L("ctx.panel.exit_layout"), Icon = new FontIcon { Glyph = "\uE8BB" } };
                exitItem.Click += async (s, e) => await SetPanelLayoutModeAsync(false);
                menu.Items.Add(exitItem);

                if (HasSelectionForPanel())
                {
                    var fromSelectionItem = new MenuFlyoutItem { Text = L("ctx.panel.create_from_selection"), Icon = new FontIcon { Glyph = "\uE8A1" } };
                    fromSelectionItem.Click += (s, e) => CreatePanelFromSelectionBounds();
                    menu.Items.Add(fromSelectionItem);
                }

                var importMaskItem = new MenuFlyoutItem { Text = L("ctx.panel.import_mask"), Icon = new FontIcon { Glyph = "\uE8B9" } };
                importMaskItem.Click += async (s, e) => await ImportPanelMaskAsync(worldPos);
                menu.Items.Add(importMaskItem);

                AddGuideContextItems(menu);
                menu.Items.Add(new MenuFlyoutSeparator());

                var selectAllPanelsItem = new MenuFlyoutItem { Text = L("ctx.panel.select_all") };
                selectAllPanelsItem.Click += (s, e) => SelectAllPanelsOnActivePage();
                menu.Items.Add(selectAllPanelsItem);

                var clearPanelsItem = new MenuFlyoutItem { Text = L("ctx.panel.clear_selection") };
                clearPanelsItem.Click += (s, e) => _editorState.ClearPanelSelection();
                menu.Items.Add(clearPanelsItem);
            }
        }
        else
        {
            var hitBalloon = _editorState.HitTestBalloon(screenPos);
            if (hitBalloon != null)
            {
                if (!_editorState.SelectedBalloonIds.Contains(hitBalloon.Id))
                {
                    _editorState.SelectBalloon(hitBalloon.Id);
                }

                var duplicateItem = new MenuFlyoutItem { Text = L("ctx.balloon.duplicate"), Icon = new FontIcon { Glyph = "\uE8C8" } };
                duplicateItem.Click += (s, e) => DuplicateSelectedBalloons();
                menu.Items.Add(duplicateItem);

                var deleteItem = new MenuFlyoutItem { Text = L("ctx.balloon.delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
                deleteItem.Click += (s, e) => DeleteSelectedBalloon();
                menu.Items.Add(deleteItem);

                var copyStyleItem = new MenuFlyoutItem { Text = L("ctx.balloon.copy_style"), Icon = new FontIcon { Glyph = "\uE8C8" } };
                copyStyleItem.Click += (s, e) => CopySelectedBalloonStyle();
                menu.Items.Add(copyStyleItem);

                var pasteStyleItem = new MenuFlyoutItem
                {
                    Text = L("ctx.balloon.paste_style"),
                    Icon = new FontIcon { Glyph = "\uE77F" },
                    IsEnabled = _copiedBalloonStyle != null
                };
                pasteStyleItem.Click += (s, e) => PasteStyleToSelectedBalloons();
                menu.Items.Add(pasteStyleItem);

                AddObjectGroupingContextItems(menu);
                menu.Items.Add(new MenuFlyoutSeparator());

                var tailItem = new MenuFlyoutItem
                {
                    Text = hitBalloon.Tails.Count > 0 ? L("ctx.balloon.remove_tail") : L("ctx.balloon.add_tail"),
                    Icon = new FontIcon { Glyph = "\uE81C" }
                };
                tailItem.Click += (s, e) => ToggleTailOnSelectedBalloon();
                menu.Items.Add(tailItem);

                if (HasSelectionForPanel())
                {
                    menu.Items.Add(new MenuFlyoutSeparator());
                    var selectionPanelItem = new MenuFlyoutItem { Text = L("ctx.balloon.create_panel"), Icon = new FontIcon { Glyph = "\uE8A1" } };
                    selectionPanelItem.Click += (s, e) => CreatePanelFromSelectionBounds();
                    menu.Items.Add(selectionPanelItem);
                }

                if (_editorState.SelectedBalloonIds.Count >= 2)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());

                    var linkItem = new MenuFlyoutItem { Text = L("ctx.balloon.link"), Icon = new FontIcon { Glyph = "\uE71B" } };
                    linkItem.Click += (s, e) => LinkSelectedBalloons();
                    menu.Items.Add(linkItem);

                    var unlinkItem = new MenuFlyoutItem { Text = L("ctx.balloon.unlink") };
                    unlinkItem.Click += (s, e) => UnlinkSelectedBalloons();
                    menu.Items.Add(unlinkItem);
                }

                var page = _editorState.Document?.ActivePage;
                if (page != null && page.Panels.Count > 0)
                {
                    menu.Items.Add(new MenuFlyoutSeparator());

                    var panelSubMenu = new MenuFlyoutSubItem { Text = L("ctx.balloon.attach_panel"), Icon = new FontIcon { Glyph = "\uE8A1" } };

                    var noneItem = new MenuFlyoutItem { Text = L("common.none") };
                    noneItem.Click += (s, e) =>
                    {
                        _editorState.Execute(new SetBalloonPanelCommand(hitBalloon.Id, null));
                        UpdatePropertiesPanel();
                        MainCanvas.Invalidate();
                    };
                    if (hitBalloon.PanelId == null)
                    {
                        noneItem.Icon = new FontIcon { Glyph = "\uE73E" }; // Checkmark
                    }
                    panelSubMenu.Items.Add(noneItem);

                    panelSubMenu.Items.Add(new MenuFlyoutSeparator());

                    foreach (var panel in page.Panels.OrderBy(p => p.Order))
                    {
                        var panelItem = new MenuFlyoutItem { Text = panel.Name };
                        var capturedPanelId = panel.Id;
                        panelItem.Click += (s, e) =>
                        {
                            _editorState.Execute(new SetBalloonPanelCommand(hitBalloon.Id, capturedPanelId));
                            UpdatePropertiesPanel();
                            MainCanvas.Invalidate();
                        };
                        if (hitBalloon.PanelId == panel.Id)
                        {
                            panelItem.Icon = new FontIcon { Glyph = "\uE73E" }; // Checkmark
                        }
                        panelSubMenu.Items.Add(panelItem);
                    }

                    menu.Items.Add(panelSubMenu);

                    var duplicatePanelMenu = new MenuFlyoutSubItem { Text = L("ctx.balloon.duplicate_to_panel"), Icon = new FontIcon { Glyph = "\uE8C8" } };
                    foreach (var panel in page.Panels.OrderBy(p => p.Order))
                    {
                        var panelItem = new MenuFlyoutItem { Text = panel.Name };
                        var capturedPanelId = panel.Id;
                        panelItem.Click += (s, e) => DuplicateSelectedBalloonsToPanel(capturedPanelId);
                        duplicatePanelMenu.Items.Add(panelItem);
                    }
                    menu.Items.Add(duplicatePanelMenu);
                }
            }
            else
            {
                var hitFloatingImage = _editorState.HitTestFloatingImage(screenPos);
                if (hitFloatingImage != null)
                {
                    if (_editorState.SelectedFloatingImageId != hitFloatingImage.Id)
                    {
                        _editorState.SelectFloatingImage(hitFloatingImage.Id);
                    }

                    var deleteImageItem = new MenuFlyoutItem
                    {
                        Text = L("ctx.image.delete"),
                        Icon = new FontIcon { Glyph = "\uE74D" }
                    };
                    deleteImageItem.Click += (s, e) => DeleteSelectedFloatingImage();
                    menu.Items.Add(deleteImageItem);

                    AddObjectGroupingContextItems(menu);

                    var imgPage = _editorState.Document?.ActivePage;
                    if (imgPage != null && imgPage.Panels.Count > 0)
                    {
                        menu.Items.Add(new MenuFlyoutSeparator());

                        var panelSubMenu = new MenuFlyoutSubItem { Text = L("ctx.image.attach_panel"), Icon = new FontIcon { Glyph = "\uE8A1" } };

                        var noneItem = new MenuFlyoutItem { Text = L("common.none") };
                        noneItem.Click += (s, e) =>
                        {
                            _editorState.Execute(new SetFloatingImagePanelCommand(imgPage.Id, hitFloatingImage.Id, null));
                            UpdatePropertiesPanel();
                            RefreshLayerList();
                            MainCanvas.Invalidate();
                        };
                        if (hitFloatingImage.PanelId == null)
                        {
                            noneItem.Icon = new FontIcon { Glyph = "\uE73E" };
                        }
                        panelSubMenu.Items.Add(noneItem);
                        panelSubMenu.Items.Add(new MenuFlyoutSeparator());

                        foreach (var panel in imgPage.Panels.OrderBy(p => p.Order))
                        {
                            var panelItem = new MenuFlyoutItem { Text = panel.Name };
                            var capturedPanelId = panel.Id;
                            panelItem.Click += (s, e) =>
                            {
                                _editorState.Execute(new SetFloatingImagePanelCommand(imgPage.Id, hitFloatingImage.Id, capturedPanelId));
                                UpdatePropertiesPanel();
                                RefreshLayerList();
                                MainCanvas.Invalidate();
                            };
                            if (hitFloatingImage.PanelId == panel.Id)
                            {
                                panelItem.Icon = new FontIcon { Glyph = "\uE73E" };
                            }
                            panelSubMenu.Items.Add(panelItem);
                        }

                        menu.Items.Add(panelSubMenu);
                    }
                }
                else
                {
                    var createBalloonItem = new MenuFlyoutItem { Text = L("ctx.canvas.create_balloon"), Icon = new FontIcon { Glyph = "\uE710" } };
                    createBalloonItem.Click += (s, e) => CreateBalloonAtPosition(worldPos);
                    menu.Items.Add(createBalloonItem);

                    var addImageItem = new MenuFlyoutItem { Text = L("menu.file.import_decoration"), Icon = new FontIcon { Glyph = "\uE8B9" } };
                    addImageItem.Click += (s, e) => ImportFloatingImage_Click(s, e);
                    menu.Items.Add(addImageItem);

                    if (ClipboardHasImageData())
                    {
                        var pasteImageItem = new MenuFlyoutItem { Text = L("menu.file.paste_image"), Icon = new FontIcon { Glyph = "\uE77F" } };
                        pasteImageItem.Click += async (s, e) => await PasteImageFromClipboardAsync();
                        menu.Items.Add(pasteImageItem);
                    }

                    menu.Items.Add(new MenuFlyoutSeparator());

                    var panelModeItem = new MenuFlyoutItem { Text = L("ctx.canvas.enter_panel_layout"), Icon = new FontIcon { Glyph = "\uE8A1" } };
                    panelModeItem.Click += async (s, e) => await SetPanelLayoutModeAsync(true);
                    menu.Items.Add(panelModeItem);

                    AddGuideContextItems(menu);
                }
            }
        }

        menu.ShowAt(MainCanvas, new Windows.Foundation.Point(pointerPos.X, pointerPos.Y));
    }

    private void AddGuideContextItems(MenuFlyout menu)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            return;
        }

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        var canEdit = !page.GuidesLocked;

        var addHorizontalGuide = new MenuFlyoutItem
        {
            Text = L("menu.guides.add_horizontal"),
            Icon = new FontIcon { Glyph = "\uE8FD" },
            IsEnabled = canEdit
        };
        addHorizontalGuide.Click += AddHorizontalGuide_Click;
        menu.Items.Add(addHorizontalGuide);

        var addVerticalGuide = new MenuFlyoutItem
        {
            Text = L("menu.guides.add_vertical"),
            Icon = new FontIcon { Glyph = "\uE8FC" },
            IsEnabled = canEdit
        };
        addVerticalGuide.Click += AddVerticalGuide_Click;
        menu.Items.Add(addVerticalGuide);
    }

    private void AddObjectGroupingContextItems(MenuFlyout menu)
    {
        var groupingState = GetObjectGroupingState();
        if (!groupingState.CanGroup && !groupingState.CanUngroup)
        {
            return;
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        if (groupingState.CanGroup)
        {
            var groupItem = new MenuFlyoutItem { Text = L("menu.objects.group") };
            groupItem.Click += GroupObjects_Click;
            menu.Items.Add(groupItem);
        }

        if (groupingState.CanUngroup)
        {
            var ungroupItem = new MenuFlyoutItem { Text = L("menu.objects.ungroup") };
            ungroupItem.Click += UngroupObjects_Click;
            menu.Items.Add(ungroupItem);
        }
    }

    private void CopyStyle_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedBalloonStyle();
    }

    private static bool ClipboardHasImageData()
    {
        try
        {
            var clipboard = Clipboard.GetContent();
            return clipboard.Contains(StandardDataFormats.Bitmap) || clipboard.Contains(StandardDataFormats.StorageItems);
        }
        catch
        {
            return false;
        }
    }

    private void PasteStyle_Click(object sender, RoutedEventArgs e)
    {
        PasteStyleToSelectedBalloons();
    }

    private void CopySelectedBalloonStyle()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            return;
        }

        var source = doc.SelectedBalloon;
        if (source == null)
        {
            var selected = GetSelectedBalloons().FirstOrDefault();
            if (selected != null)
            {
                source = selected;
            }
        }

        if (source == null)
        {
            SetStatusMessage(L("input.status.copy_style_select_balloon"));
            return;
        }

        _copiedBalloonStyle = new BalloonStyleClipboardData(
            source.Shape,
            source.CustomShapePathData,
            source.BalloonStyle,
            source.TextStyle,
            source.ConstrainToPanel,
            BuildNamedStyleTailSnapshots(source));
        SetStatusMessage(L("input.status.copy_style_copied"));
    }

    private void PasteStyleToSelectedBalloons()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            return;
        }

        var copied = _copiedBalloonStyle;
        if (copied == null)
        {
            SetStatusMessage(L("input.status.copy_style_copy_first"));
            return;
        }

        var targetIds = _editorState.SelectedBalloonIds
            .Where(id => doc.FindBalloon(id) != null)
            .Distinct()
            .ToList();
        if (targetIds.Count == 0 && doc.SelectedBalloonId.HasValue)
        {
            var selected = doc.FindBalloon(doc.SelectedBalloonId.Value);
            if (selected != null)
            {
                targetIds.Add(selected.Id);
            }
        }

        if (targetIds.Count == 0)
        {
            SetStatusMessage(L("input.status.copy_style_select_targets"));
            return;
        }

        var commands = new List<ICommand>();
        foreach (var id in targetIds)
        {
            commands.Add(new SetBalloonShapeCommand(id, copied.Shape));
            commands.Add(new SetBalloonCustomShapeCommand(id, copied.CustomShapePathData));
            commands.Add(new SetBalloonStyleCommand(id, copied.BalloonStyle));
            commands.Add(new SetTextStyleCommand(id, copied.TextStyle));
            commands.Add(new SetBalloonConstrainToPanelCommand(id, copied.ConstrainToPanel));
            commands.Add(new SetBalloonTailsFromTemplatesCommand(id, copied.Tails, preservePlacement: true));
        }

        _editorState.ExecuteTransaction("Paste balloon style", commands);
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
        SetStatusMessage(
            targetIds.Count == 1
                ? L("input.status.copy_style_pasted_single")
                : LF("input.status.copy_style_pasted_multiple", targetIds.Count));
    }

    private async Task LoadImageIntoPanelAsync(Guid panelId)
    {
        var page = _editorState.Document?.ActivePage;
        var panel = page?.FindPanel(panelId);
        if (panel == null) return;

        var picker = new FileOpenPicker();
        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await CreateFloatingImageFromFileInPanelAsync(file, page!, panel);
            MainCanvas.Invalidate();
            SetStatusMessage(L("image.status.loaded_into_panel"));
        }
    }

    private void ChangePanelOrder(Guid panelId, int delta)
    {
        var page = _editorState.Document?.ActivePage;
        var panel = page?.FindPanel(panelId);
        if (panel == null) return;

        var newOrder = panel.Order + delta;
        if (newOrder < 1) newOrder = 1;
        if (newOrder > page!.Panels.Count) newOrder = page.Panels.Count;

        if (newOrder != panel.Order)
        {
            _editorState.Execute(new SetPanelZoneOrderCommand(page.Id, panelId, newOrder));
            RefreshLayerList();
            MainCanvas.Invalidate();
        }
    }

    private async Task ShowSplitPanelDialogAsync(PanelZone panel)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var orientationCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        orientationCombo.Items.Add(new ComboBoxItem { Content = L("input.dialog.split_horizontal"), Tag = PanelSplitOrientation.Horizontal });
        orientationCombo.Items.Add(new ComboBoxItem { Content = L("input.dialog.split_vertical"), Tag = PanelSplitOrientation.Vertical });
        orientationCombo.SelectedIndex = 0;

        var modeCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        modeCombo.Items.Add(new ComboBoxItem { Content = L("input.dialog.split_percentage"), Tag = "percent" });
        modeCombo.Items.Add(new ComboBoxItem { Content = L("input.dialog.split_pixels"), Tag = "pixels" });
        modeCombo.SelectedIndex = 0;

        var valueBox = new NumberBox
        {
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Minimum = 10,
            Maximum = 90,
            Value = 50
        };

        void UpdateValueBox()
        {
            var orientation = (orientationCombo.SelectedItem as ComboBoxItem)?.Tag as PanelSplitOrientation? ?? PanelSplitOrientation.Horizontal;
            var isPercent = (modeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "percent";
            var available = orientation == PanelSplitOrientation.Horizontal
                ? panel.Bounds.Height - page.PanelGutterWidth
                : panel.Bounds.Width - page.PanelGutterWidth;
            var maxSplit = Math.Max(PanelMinSize, available - PanelMinSize);

            if (isPercent)
            {
                valueBox.Header = L("input.dialog.split_percentage_header");
                valueBox.Minimum = 10;
                valueBox.Maximum = 90;
                if (valueBox.Value <= 0 || valueBox.Value > 100)
                {
                    valueBox.Value = 50;
                }
            }
            else
            {
                valueBox.Header = L("input.dialog.split_pixels_header");
                valueBox.Minimum = PanelMinSize;
                valueBox.Maximum = Math.Max(PanelMinSize, maxSplit);
                if (valueBox.Value <= 0 || valueBox.Value > valueBox.Maximum)
                {
                    valueBox.Value = Math.Max(PanelMinSize, available / 2f);
                }
            }
        }

        orientationCombo.SelectionChanged += (_, _) => UpdateValueBox();
        modeCombo.SelectionChanged += (_, _) => UpdateValueBox();

        UpdateValueBox();

        var panelRoot = new StackPanel { Spacing = 10 };
        panelRoot.Children.Add(new TextBlock { Text = L("input.dialog.orientation"), Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray) });
        panelRoot.Children.Add(orientationCombo);
        panelRoot.Children.Add(new TextBlock { Text = L("input.dialog.split_position"), Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray) });
        panelRoot.Children.Add(modeCombo);
        panelRoot.Children.Add(valueBox);

        var dialog = new ContentDialog
        {
            Title = $"{L("input.dialog.split_title")} {panel.Name}",
            Content = panelRoot,
            PrimaryButtonText = L("input.dialog.split_button"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var selectedOrientation = (orientationCombo.SelectedItem as ComboBoxItem)?.Tag as PanelSplitOrientation?
            ?? PanelSplitOrientation.Horizontal;
        var isPercentage = (modeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "percent";
        var position = (float)valueBox.Value;

        if (isPercentage)
        {
            position = position / 100f;
        }

        _editorState.Execute(new SplitPanelZoneCommand(page.Id, panel.Id, selectedOrientation, position, isPercentage));
        RefreshPanelList();
        MainCanvas.Invalidate();
        SetStatusMessage(LF("input.status.split_panel", panel.Name));
    }

    private async Task ShowMergePanelDialogAsync(PanelZone panel)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var options = GetAdjacentPanelOptions(page, panel);
        if (options.Count == 0)
        {
            var noAdjDialog = new ContentDialog
            {
                Title = L("input.dialog.merge_title"),
                Content = new TextBlock
                {
                    Text = L("input.dialog.merge_no_adjacent"),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
                },
                CloseButtonText = L("common.ok"),
                XamlRoot = Content.XamlRoot
            };
            await noAdjDialog.ShowAsync();
            return;
        }

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = options,
            SelectedIndex = 0,
            DisplayMemberPath = nameof(AdjacentPanelOption.Label)
        };

        var dialog = new ContentDialog
        {
            Title = $"{L("input.dialog.merge_title")} {panel.Name}",
            Content = combo,
            PrimaryButtonText = L("input.dialog.merge_button"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (combo.SelectedItem is not AdjacentPanelOption selected) return;

        _editorState.Execute(new MergePanelZonesCommand(page.Id, panel.Id, selected.PanelId));
        RefreshPanelList();
        MainCanvas.Invalidate();
        SetStatusMessage(LF("input.status.merged_panels", panel.Name, selected.Label));
    }

    private static List<AdjacentPanelOption> GetAdjacentPanelOptions(DocumentPage page, PanelZone panel)
    {
        var results = new List<AdjacentPanelOption>();
        var threshold = MathF.Max(4f, page.PanelGutterWidth * 1.5f);

        static float Overlap(float aStart, float aEnd, float bStart, float bEnd)
        {
            return MathF.Min(aEnd, bEnd) - MathF.Max(aStart, bStart);
        }

        foreach (var other in page.Panels)
        {
            if (other.Id == panel.Id) continue;
            if (!other.IsVisible) continue;

            var overlapX = Overlap(panel.Bounds.Left, panel.Bounds.Right, other.Bounds.Left, other.Bounds.Right);
            var overlapY = Overlap(panel.Bounds.Top, panel.Bounds.Bottom, other.Bounds.Top, other.Bounds.Bottom);

            var candidates = new List<(string Label, float Distance)>();

            if (overlapY > 1f)
            {
                var leftGap = panel.Bounds.Left - other.Bounds.Right;
                if (MathF.Abs(leftGap) <= threshold)
                {
                    candidates.Add(("Left", MathF.Abs(leftGap)));
                }

                var rightGap = other.Bounds.Left - panel.Bounds.Right;
                if (MathF.Abs(rightGap) <= threshold)
                {
                    candidates.Add(("Right", MathF.Abs(rightGap)));
                }
            }

            if (overlapX > 1f)
            {
                var topGap = panel.Bounds.Top - other.Bounds.Bottom;
                if (MathF.Abs(topGap) <= threshold)
                {
                    candidates.Add(("Above", MathF.Abs(topGap)));
                }

                var bottomGap = other.Bounds.Top - panel.Bounds.Bottom;
                if (MathF.Abs(bottomGap) <= threshold)
                {
                    candidates.Add(("Below", MathF.Abs(bottomGap)));
                }
            }

            if (candidates.Count == 0) continue;

            var best = candidates.OrderBy(c => c.Distance).First();
            results.Add(new AdjacentPanelOption(other.Id, $"{other.Name} ({best.Label})", best.Distance));
        }

        return results.OrderBy(option => option.Distance).ToList();
    }

    private sealed class AdjacentPanelOption
    {
        public AdjacentPanelOption(Guid panelId, string label, float distance)
        {
            PanelId = panelId;
            Label = label;
            Distance = distance;
        }

        public Guid PanelId { get; }
        public string Label { get; }
        public float Distance { get; }
    }



    private void RootGrid_ProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
    {
        if (_editorState.Mode != EditorMode.EditText)
        {
            return;
        }

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (!ctrl && !alt)
        {
            return;
        }

        if (ctrl && !alt)
        {
            switch (args.Key)
            {
                case VirtualKey.A:
                    _editorState.SelectAll();
                    break;
                case VirtualKey.C:
                    CopySelectionToClipboard();
                    break;
                case VirtualKey.X:
                    if (CopySelectionToClipboard())
                    {
                        _editorState.DeleteSelection();
                    }
                    break;
                case VirtualKey.V:
                    _ = PasteTextFromClipboardAsync();
                    break;
                case VirtualKey.Z:
                    _editorState.UndoTextEdit();
                    break;
                case VirtualKey.Y:
                    _editorState.RedoTextEdit();
                    break;
                case VirtualKey.B:
                    BoldToggleButton_Click(this, new RoutedEventArgs());
                    break;
                case VirtualKey.I:
                    ItalicToggleButton_Click(this, new RoutedEventArgs());
                    break;
                case VirtualKey.U:
                    UnderlineToggleButton_Click(this, new RoutedEventArgs());
                    break;
            }
        }

        args.Handled = true;
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (_editorState.Mode == EditorMode.EditText)
        {
            if (HandleTextEditingKey(e, ctrl))
            {
                e.Handled = true;
            }
            return;
        }

        if (TryHandleConfiguredRootShortcut(e.Key, ctrl, shift, alt))
        {
            e.Handled = true;
            return;
        }

        if (alt && !ctrl && !shift && TryGetPresetHotkeySlot(e.Key, out var templateSlot) && TryApplyBalloonTemplateHotkey(templateSlot))
        {
            e.Handled = true;
            return;
        }

        if (ctrl) return;

        switch (e.Key)
        {
            case VirtualKey.Tab:
                return;

            case VirtualKey.Enter:
                if (_isEditingPanelShape)
                {
                    CommitPanelShapeEdit();
                    e.Handled = true;
                    return;
                }
                if (_isPanelPolygonDrawing)
                {
                    FinishPanelPolygon();
                    e.Handled = true;
                    return;
                }
                if ((_editorState.Mode == EditorMode.Select || _editorState.Mode == EditorMode.PanelLayout) &&
                    _editorState.Document?.SelectedBalloon != null)
                {
                    if (_editorState.Mode == EditorMode.PanelLayout)
                    {
                        _ = SetPanelLayoutModeAsync(false);
                    }
                    var balloon = _editorState.Document.SelectedBalloon;
                    _editorState.EnterTextEditMode(balloon.Id);
                    MainCanvas.Invalidate();
                    e.Handled = true;
                }
                return;

            case VirtualKey.Escape:
                if (_isBalloonTemplateEyedropperActive)
                {
                    SetBalloonTemplateEyedropperMode(false);
                    e.Handled = true;
                    return;
                }
                if (_isEditingPanelShape)
                {
                    CancelPanelShapeEdit();
                    e.Handled = true;
                    return;
                }
                if (_isPanelPolygonDrawing || _isPanelFreeformDrawing)
                {
                    CancelPanelDrawing();
                    e.Handled = true;
                    return;
                }
                if (_editorState.Mode == EditorMode.PanelLayout)
                {
                    _ = SetPanelLayoutModeAsync(false);
                    e.Handled = true;
                }
                else if (_editorState.Mode == EditorMode.EditText)
                {
                    _editorState.ExitTextEditMode(saveChanges: true);
                    e.Handled = true;
                }
                return;
        }
    }

    private void ToggleCanvasFullscreen()
    {
        if (LeftSidebarColumn == null || RightSidebarColumn == null) return;

        if (!_isCanvasFullscreen)
        {
            _leftSidebarWidth = LeftSidebarColumn.Width;
            _rightSidebarWidth = RightSidebarColumn.Width;
            _leftSidebarMinWidth = LeftSidebarColumn.MinWidth;
            _rightSidebarMinWidth = RightSidebarColumn.MinWidth;

            LeftSidebarColumn.Width = new GridLength(0);
            RightSidebarColumn.Width = new GridLength(0);
            LeftSidebarColumn.MinWidth = 0;
            RightSidebarColumn.MinWidth = 0;

            if (LeftSidebarBorder != null) LeftSidebarBorder.Visibility = Visibility.Collapsed;
            if (RightSidebarBorder != null) RightSidebarBorder.Visibility = Visibility.Collapsed;
            if (LeftSidebarSplitter != null) LeftSidebarSplitter.Visibility = Visibility.Collapsed;
            if (RightSidebarSplitter != null) RightSidebarSplitter.Visibility = Visibility.Collapsed;

            _isCanvasFullscreen = true;
            SetStatusMessage(L("input.status.focus_panels_hidden"));
        }
        else
        {
            LeftSidebarColumn.Width = _leftSidebarWidth;
            RightSidebarColumn.Width = _rightSidebarWidth;
            LeftSidebarColumn.MinWidth = _leftSidebarMinWidth > 0 ? _leftSidebarMinWidth : 180;
            RightSidebarColumn.MinWidth = _rightSidebarMinWidth > 0 ? _rightSidebarMinWidth : 220;

            if (LeftSidebarBorder != null) LeftSidebarBorder.Visibility = Visibility.Visible;
            if (RightSidebarBorder != null) RightSidebarBorder.Visibility = Visibility.Visible;
            if (LeftSidebarSplitter != null) LeftSidebarSplitter.Visibility = Visibility.Visible;
            if (RightSidebarSplitter != null) RightSidebarSplitter.Visibility = Visibility.Visible;

            _isCanvasFullscreen = false;
            SetStatusMessage(L("input.status.focus_panels_shown"));
        }
    }


}
