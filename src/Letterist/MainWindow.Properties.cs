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
using System.Xml.Linq;
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

    private bool _isUpdatingProperties;
    private bool _isUpdatingStylePresets;
    private bool _isEditingBalloonText;
    private Guid? _selectedBalloonStyleId;
    private Guid? _selectedTextStyleId;

    private TextStyle GetActiveTextStyleForProperties(Balloon balloon)
    {
        if (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId == balloon.Id)
        {
            return _editorState.GetSelectionTextStyle();
        }

        return balloon.TextStyle;
    }

    private void ApplyInlineTextStyle(Func<TextStyle, TextStyle> update, bool preferInsertionModeWhenEditing = false)
    {
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        if (_editorState.Mode == EditorMode.EditText &&
            _editorState.EditingBalloonId == balloon.Id &&
            _editorState.HasSelection)
        {
            var current = _editorState.GetSelectionTextStyle();
            var updated = update(current);
            if (!TextStyleUtilities.AreInlineEquivalent(current, updated))
            {
                _editorState.ApplyTextStyleToSelection(updated);
                MainCanvas.Invalidate(); // Redraw to show changes immediately
            }
            return;
        }

        if (preferInsertionModeWhenEditing &&
            _editorState.Mode == EditorMode.EditText &&
            _editorState.EditingBalloonId == balloon.Id)
        {
            var current = _editorState.GetSelectionTextStyle();
            var updated = update(current);
            if (!TextStyleUtilities.AreInlineEquivalent(current, updated) &&
                _editorState.SetInsertionTextStyle(updated))
            {
                MainCanvas.Invalidate(); // Redraw to show changes immediately
                UpdatePropertiesPanel();
            }
            return;
        }

        var newStyle = update(balloon.TextStyle);
        if (TextStyleUtilities.AreEquivalent(newStyle, balloon.TextStyle)) return;
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate(); // Redraw to show changes immediately
    }

    private static bool TryGetStrokeIndex(object? tag, out int index)
    {
        index = -1;
        if (tag is int indexValue)
        {
            index = indexValue;
            return index >= 0;
        }

        if (tag is string indexText &&
            int.TryParse(indexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out indexValue))
        {
            index = indexValue;
            return index >= 0;
        }

        return false;
    }

    private static TextStroke GetAdditionalStroke(IReadOnlyList<TextStroke>? strokes, int index, Color fallbackColor)
    {
        if (strokes != null && index >= 0 && index < strokes.Count)
        {
            return strokes[index].Clone();
        }

        return new TextStroke
        {
            Color = fallbackColor,
            Width = 0f
        };
    }

    private static List<TextStroke> BuildAdditionalStrokeUpdate(
        IReadOnlyList<TextStroke>? existing,
        int index,
        Color? color = null,
        float? width = null)
    {
        var list = existing?
            .Select(stroke => stroke.Clone())
            .ToList() ?? new List<TextStroke>();

        while (list.Count <= index)
        {
            list.Add(new TextStroke());
        }

        var current = list[index];
        var nextColor = color ?? current.Color;
        var nextWidth = width ?? current.Width;
        if (color.HasValue && !width.HasValue && nextWidth <= 0.01f)
        {
            nextWidth = 2f;
        }

        list[index] = new TextStroke
        {
            Color = nextColor,
            Width = Math.Max(0f, nextWidth)
        };

        return list;
    }

    private static TextShadow GetTextShadow(IReadOnlyList<TextShadow>? shadows, int index, Color fallbackColor)
    {
        if (shadows != null && index >= 0 && index < shadows.Count)
        {
            return shadows[index].Clone();
        }

        return new TextShadow
        {
            Color = fallbackColor,
            OffsetX = 2f,
            OffsetY = 2f,
            Blur = 0f,
            Opacity = 0.45f
        };
    }

    private static List<TextShadow> BuildTextShadowUpdate(
        IReadOnlyList<TextShadow>? existing,
        int index,
        Color? color = null,
        float? offsetX = null,
        float? offsetY = null,
        float? blur = null,
        float? opacity = null)
    {
        var list = existing?
            .Select(shadow => shadow.Clone())
            .ToList() ?? new List<TextShadow>();

        while (list.Count <= index)
        {
            list.Add(new TextShadow());
        }

        var current = list[index];
        list[index] = new TextShadow
        {
            Color = color ?? current.Color,
            OffsetX = offsetX ?? current.OffsetX,
            OffsetY = offsetY ?? current.OffsetY,
            Blur = Math.Max(0f, blur ?? current.Blur),
            Opacity = Math.Clamp(opacity ?? current.Opacity, 0f, 1f)
        };

        return list;
    }

    private static bool TryGetIndexedPropertyTag(object? tag, out int index, out string propertyName)
    {
        index = -1;
        propertyName = string.Empty;
        if (tag is not string tagText || string.IsNullOrWhiteSpace(tagText)) return false;

        var parts = tagText.Split('|');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out index)) return false;
        if (index < 0) return false;

        propertyName = parts[1];
        return !string.IsNullOrWhiteSpace(propertyName);
    }

    private static bool TryGetEffectPropertyTag(object? tag, out string effectName, out string propertyName)
    {
        effectName = string.Empty;
        propertyName = string.Empty;
        if (tag is not string tagText || string.IsNullOrWhiteSpace(tagText)) return false;

        var parts = tagText.Split('|');
        if (parts.Length != 2) return false;

        effectName = parts[0];
        propertyName = parts[1];
        return !string.IsNullOrWhiteSpace(effectName) && !string.IsNullOrWhiteSpace(propertyName);
    }

    private static void UpdateOutlineSelector(Border preview, ComboBox comboBox, Color color, string customTag)
    {
        preview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));

        comboBox.SelectedIndex = -1;
        var colorTag = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem colorItem &&
                string.Equals(colorItem.Tag?.ToString(), colorTag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                break;
            }
        }

        if (comboBox.SelectedIndex >= 0) return;

        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem colorItem &&
                string.Equals(colorItem.Tag?.ToString(), customTag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void UpdateCustomShapeStatus(Balloon balloon, string? message = null, bool isError = false)
    {
        var hasCustom = !string.IsNullOrWhiteSpace(balloon.CustomShapePathData);
        var status = message ?? (hasCustom ? L("props.custom_shape.loaded") : L("props.custom_shape.none"));
        CustomShapeStatusText.Text = status;
        CustomShapeStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            isError
                ? Microsoft.UI.ColorHelper.FromArgb(255, 200, 96, 96)
                : Microsoft.UI.ColorHelper.FromArgb(255, 136, 136, 136));
    }

    private static bool TryExtractSvgPathData(string svgContent, out string? pathData)
    {
        pathData = null;
        if (string.IsNullOrWhiteSpace(svgContent)) return false;

        var trimmed = svgContent.Trim();
        if (!trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            pathData = trimmed;
            return !string.IsNullOrWhiteSpace(pathData);
        }

        try
        {
            var doc = XDocument.Parse(svgContent);
            var paths = doc
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("d")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (paths.Count == 0) return false;

            pathData = string.Join(" ", paths);
            return !string.IsNullOrWhiteSpace(pathData);
        }
        catch
        {
            return false;
        }
    }

    private void UpdatePropertiesPanel()
    {
        if (_editorState.SelectedPanelId.HasValue)
        {
            PropertiesPanel.Visibility = Visibility.Collapsed;
            LayerPropertiesPanel.Visibility = Visibility.Collapsed;
            FloatingImagePropertiesPanel.Visibility = Visibility.Collapsed;
            PagePropertiesPanel.Visibility = Visibility.Collapsed;
            UpdatePanelZonePropertiesPanel();
            return;
        }

        PanelZonePropertiesPanel.Visibility = Visibility.Collapsed;
        FloatingImagePropertiesPanel.Visibility = Visibility.Collapsed;
        PagePropertiesPanel.Visibility = Visibility.Collapsed;

        Balloon? balloon = null;
        if (_editorState.SelectedBalloonIds.Count > 0 && _editorState.Document != null)
        {
            var primaryId = _editorState.Document.SelectedBalloonId ?? _editorState.SelectedBalloonIds.First();
            balloon = _editorState.Document.FindBalloon(primaryId);
        }

        if (balloon == null)
        {
            UpdateCjkFontRecommendationUi(balloon: null, activeTextStyle: null);
            PropertiesPanel.Visibility = Visibility.Collapsed;

            if (_editorState.SelectedFloatingImageId.HasValue && _editorState.Document?.ActivePage != null)
            {
                var image = _editorState.Document.ActivePage.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
                if (image != null)
                {
                    LayerPropertiesPanel.Visibility = Visibility.Collapsed;
                    PagePropertiesPanel.Visibility = Visibility.Collapsed;
                    UpdateFloatingImagePropertiesPanel(image);
                    return;
                }
            }

            if (ShouldShowPagePropertiesInInspector())
            {
                LayerPropertiesPanel.Visibility = Visibility.Collapsed;
                FloatingImagePropertiesPanel.Visibility = Visibility.Collapsed;
                RefreshPageSetup();
                PagePropertiesPanel.Visibility = Visibility.Visible;
                return;
            }

            var activeLayer = _editorState.Document?.ActiveLayer;
            if (activeLayer != null)
            {
                UpdateLayerPropertiesPanel();
            }
            else
            {
                LayerPropertiesPanel.Visibility = Visibility.Collapsed;
                FloatingImagePropertiesPanel.Visibility = Visibility.Collapsed;
                PagePropertiesPanel.Visibility = Visibility.Collapsed;
            }
            return;
        }

        LayerPropertiesPanel.Visibility = Visibility.Collapsed;
        FloatingImagePropertiesPanel.Visibility = Visibility.Collapsed;
        PagePropertiesPanel.Visibility = Visibility.Collapsed;
        PropertiesPanel.Visibility = Visibility.Visible;

        _isUpdatingProperties = true;
        try
        {
            for (int i = 0; i < ShapeComboBox.Items.Count; i++)
            {
                if (ShapeComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
                    item.Tag?.ToString() == balloon.Shape.ToString())
                {
                    ShapeComboBox.SelectedIndex = i;
                    break;
                }
            }

            var showCustomShapeControls = balloon.Shape == BalloonShape.Custom;
            var showThoughtSmoothness = balloon.Shape is BalloonShape.Thought or BalloonShape.Splat or BalloonShape.Burst;
            CustomShapePanel.Visibility = showCustomShapeControls ? Visibility.Visible : Visibility.Collapsed;
            ThoughtSmoothnessPanel.Visibility = showThoughtSmoothness ? Visibility.Visible : Visibility.Collapsed;
            ImportCustomShapeButton.IsEnabled = showCustomShapeControls;
            if (showCustomShapeControls)
            {
                UpdateCustomShapeStatus(balloon);
            }
            if (showThoughtSmoothness)
            {
                UpdateThoughtSliderLabel(balloon.Shape);
                var smoothnessPercent = Math.Clamp(balloon.BalloonStyle.ThoughtSmoothness * 100f, 0f, 100f);
                ThoughtSmoothnessSlider.Value = smoothnessPercent;
                ThoughtSmoothnessValueText.Text = $"{smoothnessPercent:F0}%";
            }

            var fillColor = balloon.BalloonStyle.FillColor;
            FillColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(fillColor.A, fillColor.R, fillColor.G, fillColor.B));
            FillColorComboBox.SelectedIndex = -1;
            var fillTag = $"#{fillColor.R:X2}{fillColor.G:X2}{fillColor.B:X2}";
            for (int i = 0; i < FillColorComboBox.Items.Count; i++)
            {
                if (FillColorComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem fillItem &&
                    string.Equals(fillItem.Tag?.ToString(), fillTag, StringComparison.OrdinalIgnoreCase))
                {
                    FillColorComboBox.SelectedIndex = i;
                    break;
                }
            }

            var strokeColor = balloon.BalloonStyle.StrokeColor;
            StrokeColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(strokeColor.A, strokeColor.R, strokeColor.G, strokeColor.B));
            StrokeColorComboBox.SelectedIndex = -1;
            var strokeTag = $"#{strokeColor.R:X2}{strokeColor.G:X2}{strokeColor.B:X2}";
            for (int i = 0; i < StrokeColorComboBox.Items.Count; i++)
            {
                if (StrokeColorComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem strokeItem &&
                    string.Equals(strokeItem.Tag?.ToString(), strokeTag, StringComparison.OrdinalIgnoreCase))
                {
                    StrokeColorComboBox.SelectedIndex = i;
                    break;
                }
            }

            var activeTextStyle = GetActiveTextStyleForProperties(balloon);
            UpdateFontChooserDisplay(activeTextStyle.FontFamily);
            UpdateCjkFontRecommendationUi(balloon, activeTextStyle);

            if (!_isEditingBalloonText)
            {
                BalloonTextEditBox.Text = balloon.Text ?? "";
            }

            BalloonXBox.Value = balloon.Position.X;
            BalloonYBox.Value = balloon.Position.Y;
            BalloonWidthBox.Value = balloon.ComputedSize.Width;
            BalloonHeightBox.Value = balloon.ComputedSize.Height;
            BalloonRotationSlider.Value = balloon.Rotation;
            BalloonRotationBox.Value = balloon.Rotation;

            StrokeWidthSlider.Value = balloon.BalloonStyle.StrokeWidth;
            var opacityPercent = balloon.BalloonStyle.Opacity * 100f;
            BalloonOpacitySlider.Value = opacityPercent;
            BalloonOpacityValueText.Text = $"{opacityPercent:F0}%";
            CornerRadiusSlider.Value = balloon.BalloonStyle.CornerRadius;
            RotationSlider.Value = balloon.Rotation;
            RotationValueText.Text = $"{balloon.Rotation:F0}°";
            PaddingLeftBox.Value = balloon.BalloonStyle.PaddingLeft;
            PaddingTopBox.Value = balloon.BalloonStyle.PaddingTop;
            PaddingRightBox.Value = balloon.BalloonStyle.PaddingRight;
            PaddingBottomBox.Value = balloon.BalloonStyle.PaddingBottom;
            MinWidthBox.Value = balloon.BalloonStyle.MinWidth;
            MinHeightBox.Value = balloon.BalloonStyle.MinHeight;
            MaxWidthBox.Value = balloon.BalloonStyle.MaxWidth;
            MaxHeightBox.Value = balloon.BalloonStyle.MaxHeight;
            FontSizeSlider.Value = activeTextStyle.FontSize;
            FontSizeValueText.Text = $"{activeTextStyle.FontSize:F0}px";
            TrackingSlider.Value = activeTextStyle.Tracking;
            TrackingValueText.Text = $"{activeTextStyle.Tracking:F2}";
            LineSpacingSlider.Value = balloon.TextStyle.LineHeight;
            LineSpacingValueText.Text = $"{balloon.TextStyle.LineHeight:F2}x";
            TextVerticalOffsetSlider.Value = balloon.TextStyle.VerticalOffset;
            VerticalOffsetValueText.Text = $"{balloon.TextStyle.VerticalOffset:F0}px";

            AllCapsToggle.IsOn = balloon.TextStyle.AllCaps;
            UpdateAllCapsButtonAppearance();


            var textColor = activeTextStyle.TextColor;
            TextColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B));
            TextColorComboBox.SelectedIndex = -1;
            var textColorTag = $"#{textColor.R:X2}{textColor.G:X2}{textColor.B:X2}";
            for (int i = 0; i < TextColorComboBox.Items.Count; i++)
            {
                if (TextColorComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem textColorItem &&
                    string.Equals(textColorItem.Tag?.ToString(), textColorTag, StringComparison.OrdinalIgnoreCase))
                {
                    TextColorComboBox.SelectedIndex = i;
                    break;
                }
            }

            var outlineColor = activeTextStyle.OutlineColor;
            UpdateOutlineSelector(OutlineColorPreview, OutlineColorComboBox, outlineColor, "CUSTOM");

            var outlineWidth = MathF.Max(0f, activeTextStyle.OutlineWidth);
            OutlineWidthBox.Value = outlineWidth;
            OutlineWidthSlider.Value = Math.Clamp(outlineWidth, 0, 8);
            OutlineWidthValueText.Text = outlineWidth.ToString("F1");
            SelectNumericPresetComboBoxItem(OutlineWidthPresetComboBox, outlineWidth, tolerance: 0.05f);

            var secondStroke = GetAdditionalStroke(activeTextStyle.AdditionalStrokes, 0, outlineColor);
            UpdateOutlineSelector(Outline2ColorPreview, Outline2ColorComboBox, secondStroke.Color, "CUSTOM");
            Outline2WidthBox.Value = Math.Max(0f, secondStroke.Width);

            var thirdStroke = GetAdditionalStroke(activeTextStyle.AdditionalStrokes, 1, outlineColor);
            UpdateOutlineSelector(Outline3ColorPreview, Outline3ColorComboBox, thirdStroke.Color, "CUSTOM");
            Outline3WidthBox.Value = Math.Max(0f, thirdStroke.Width);

            TextAlignmentComboBox.SelectedIndex = balloon.TextStyle.Alignment switch
            {
                Model.TextAlignment.Left => 0,
                Model.TextAlignment.Center => 1,
                Model.TextAlignment.Right => 2,
                _ => 1 // Default to Center
            };

            FitModeComboBox.SelectedIndex = balloon.TextStyle.FitMode switch
            {
                TextFitMode.GrowBalloon => 0,
                TextFitMode.None => 1,
                TextFitMode.ShrinkToFit => 2,
                _ => 0 // Default to Auto-Size
            };

            FillHeightCheckBox.IsChecked = balloon.TextStyle.FillHeight;

            var overflowTag = balloon.TextStyle.OverflowMode.ToString();
            TextOverflowComboBox.SelectedIndex = -1;
            for (int i = 0; i < TextOverflowComboBox.Items.Count; i++)
            {
                if (TextOverflowComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem overflowItem &&
                    overflowItem.Tag?.ToString() == overflowTag)
                {
                    TextOverflowComboBox.SelectedIndex = i;
                    break;
                }
            }

            UpdateBalloonTextPathControls(balloon);
            UpdateBalloonTextWarpControls(balloon.TextStyle);

            RefreshFillTabControls(balloon);


            UpdateEffectsTabUi(balloon);

            HasTailToggle.IsOn = balloon.Tail != null;
            if (balloon.Tail != null)
            {
                var tailStyleTag = balloon.Tail.Style.ToString();
                TailStyleComboBox.SelectedIndex = -1;
                for (int i = 0; i < TailStyleComboBox.Items.Count; i++)
                {
                    if (TailStyleComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem tailItem &&
                        tailItem.Tag?.ToString() == tailStyleTag)
                    {
                        TailStyleComboBox.SelectedIndex = i;
                        break;
                    }
                }

                TailWidthSlider.Value = balloon.Tail.BaseWidth;
                TailWidthValueText.Text = $"{balloon.Tail.BaseWidth:F0}";
                ResetTailAttachmentButton.IsEnabled = balloon.Tail.AttachmentDirection.HasValue;

                var showCurvedControls = balloon.Tail.Style == TailStyle.Curved;
                var showInsetControl = balloon.Tail.Style is TailStyle.Pointer or TailStyle.Curved;
                TailCurvaturePanel.Visibility = showCurvedControls ? Visibility.Visible : Visibility.Collapsed;
                TailCurveCenterPanel.Visibility = showCurvedControls ? Visibility.Visible : Visibility.Collapsed;
                TailInsetPanel.Visibility = showInsetControl ? Visibility.Visible : Visibility.Collapsed;

                if (showCurvedControls)
                {
                    var curvaturePercent = balloon.Tail.Curvature * 100f;
                    TailCurvatureSlider.Value = curvaturePercent;
                    TailCurvatureValueText.Text = $"{curvaturePercent:F0}%";

                    var centerPercent = balloon.Tail.CurveCenter * 100f;
                    TailCurveCenterSlider.Value = centerPercent;
                    TailCurveCenterValueText.Text = $"{centerPercent:F0}%";
                }

                if (showInsetControl)
                {
                    TailInsetSlider.Value = balloon.Tail.Inset;
                    TailInsetValueText.Text = $"{balloon.Tail.Inset:F0}";
                }
            }
            else
            {
                TailCurvaturePanel.Visibility = Visibility.Collapsed;
                TailCurveCenterPanel.Visibility = Visibility.Collapsed;
                TailInsetPanel.Visibility = Visibility.Collapsed;
                ResetTailAttachmentButton.IsEnabled = false;
            }

            var page = _editorState.Document?.ActivePage;
            if (page != null)
            {
                UpdateLinkPanel(page);
                UpdateGuideList();
                SnapToGuidesToggle.IsOn = _editorState.SnapToGuides;
                LockGuidesToggle.IsOn = page.GuidesLocked;
                PanelSafeGuidesToggle.IsOn = _editorState.ShowPanelSafeGuides;
                PanelGutterGuidesToggle.IsOn = _editorState.ShowPanelGutters;
                SetPanelGutterStyleControls(page);
                SetPanelBoundaryVisibilityCombo();
                SetReadingDirectionCombo(page);
                UpdateGuideLockUi(page);
            }

            UpdateBalloonPanelComboBox(balloon);
            BalloonVisibilityToggle.IsOn = balloon.IsVisible;
            BalloonLockToggle.IsOn = balloon.IsLocked;
            BalloonConstrainToggle.IsOn = balloon.PanelId.HasValue && balloon.ConstrainToPanel;
            BalloonConstrainToggle.IsEnabled = balloon.PanelId.HasValue;
            UpdateStylePresetSelection(balloon, activeTextStyle);
        }
        finally
        {
            _isUpdatingProperties = false;
        }
    }

    private void UpdateStylePresetSelection(Balloon balloon, TextStyle activeTextStyle)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        NamedBalloonStyle? matchingBalloonStyle = null;
        if (balloon.BalloonStyleId.HasValue)
        {
            matchingBalloonStyle = doc.FindBalloonStyle(balloon.BalloonStyleId.Value);
        }
        if (matchingBalloonStyle == null)
        {
            matchingBalloonStyle = doc.BalloonStyles
                .FirstOrDefault(style => BalloonStyleUtilities.AreEquivalent(style.Style, balloon.BalloonStyle));
        }

        var useActiveTextStyle = _editorState.Mode == EditorMode.EditText
            && _editorState.EditingBalloonId == balloon.Id
            && _editorState.HasSelection;

        NamedTextStyle? matchingTextStyle = null;
        if (!useActiveTextStyle && balloon.TextStyleId.HasValue)
        {
            matchingTextStyle = doc.FindTextStyle(balloon.TextStyleId.Value);
        }
        if (matchingTextStyle == null)
        {
            matchingTextStyle = doc.TextStyles
                .FirstOrDefault(style => TextStyleUtilities.AreEquivalent(style.Style, activeTextStyle));
        }

        var wasUpdating = _isUpdatingStylePresets;
        _isUpdatingStylePresets = true;
        try
        {
            _selectedBalloonStyleId = matchingBalloonStyle?.Id;
            _selectedTextStyleId = matchingTextStyle?.Id;
            SelectStylePreset(BalloonStylePresetComboBox, _selectedBalloonStyleId);
            SelectStylePreset(TextStylePresetComboBox, _selectedTextStyleId);
            UpdateStylePresetButtons();
        }
        finally
        {
            _isUpdatingStylePresets = wasUpdating;
        }
    }

    private void UpdateCjkFontRecommendationUi(Balloon? balloon, TextStyle? activeTextStyle)
    {
        if (FontCjkHintText == null || ApplyCjkFontRecommendationButton == null)
        {
            return;
        }

        if (balloon == null || activeTextStyle == null || _editorState.Document == null)
        {
            FontCjkHintText.Text = L("props.cjk.guidance_placeholder");
            ApplyCjkFontRecommendationButton.Visibility = Visibility.Collapsed;
            ApplyCjkFontRecommendationButton.Tag = null;
            return;
        }

        var doc = _editorState.Document;
        var languageTag = doc.ActiveLanguage;
        var previewText = doc.GetBalloonDisplayText(balloon, languageTag);
        var orientation = doc.ResolveBalloonTranslationOrientation(balloon, languageTag);
        var preferVertical = orientation == TranslationTextOrientation.Vertical;

        if (!CjkFontSupport.TryGetRecommendation(languageTag, previewText, preferVertical, out var recommendation))
        {
            FontCjkHintText.Text = L("props.cjk.auto_detection_active");
            ApplyCjkFontRecommendationButton.Visibility = Visibility.Collapsed;
            ApplyCjkFontRecommendationButton.Tag = null;
            return;
        }

        var chainPreview = string.Join(" -> ", recommendation.FallbackChain.Take(3));
        FontCjkHintText.Text = LF("props.cjk.recommendation", recommendation.RecommendedFont, chainPreview);

        var alreadyUsingRecommended = string.Equals(
            activeTextStyle.FontFamily,
            recommendation.RecommendedFont,
            StringComparison.OrdinalIgnoreCase);
        ApplyCjkFontRecommendationButton.Visibility = alreadyUsingRecommended ? Visibility.Collapsed : Visibility.Visible;
        ApplyCjkFontRecommendationButton.Tag = recommendation.RecommendedFont;
        ApplyCjkFontRecommendationButton.Content = LF("props.cjk.use_font", recommendation.RecommendedFont);
    }

    private void RestorePropertiesScrollOffset(double offset)
    {
        _propertiesScrollOffset = offset;
    }

    private void PropertiesScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;

        var delta = e.GetCurrentPoint(viewer).Properties.MouseWheelDelta;
        if (delta == 0) return;

        var nextOffset = Math.Clamp(viewer.VerticalOffset - delta, 0, viewer.ScrollableHeight);
        viewer.ChangeView(null, nextOffset, null);
        e.Handled = true;
    }

    private async void ImportCustomShape_Click(object sender, RoutedEventArgs e)
    {
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".svg");
        picker.FileTypeFilter.Add(".txt");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        string content;
        try
        {
            content = await FileIO.ReadTextAsync(file);
        }
        catch
        {
            UpdateCustomShapeStatus(balloon, L("props.custom_shape.read_failed"), isError: true);
            return;
        }

        if (!TryExtractSvgPathData(content, out var pathData) || string.IsNullOrWhiteSpace(pathData))
        {
            UpdateCustomShapeStatus(balloon, L("props.custom_shape.path_not_found"), isError: true);
            return;
        }

        _editorState.Execute(new SetBalloonCustomShapeCommand(balloon.Id, pathData));
        UpdatePropertiesPanel();
        UpdateCustomShapeStatus(balloon);
    }

    private void RefreshStylePresets()
    {
        if (BalloonStylePresetComboBox == null || TextStylePresetComboBox == null || StyleStripComboBox == null) return;

        var doc = _editorState.Document;
        _isUpdatingStylePresets = true;
        try
        {
            BalloonStylePresetComboBox.Items.Clear();
            TextStylePresetComboBox.Items.Clear();
            StyleStripComboBox.Items.Clear();

            if (doc == null)
            {
                StyleStripComboBox.IsEnabled = false;
                UpdateStylePresetButtons();
                return;
            }

            foreach (var style in doc.BalloonStyles)
            {
                BalloonStylePresetComboBox.Items.Add(new ComboBoxItem { Content = style.Name, Tag = style });
            }

            var stripStyles = doc.BalloonStyles
                .Where(style => style.IsQuickSelect)
                .ToList();

            foreach (var style in stripStyles.GroupBy(style => style.Id).Select(group => group.First()))
            {
                StyleStripComboBox.Items.Add(new ComboBoxItem { Content = style.Name, Tag = style.Id });
            }

            foreach (var style in doc.TextStyles)
            {
                TextStylePresetComboBox.Items.Add(new ComboBoxItem { Content = style.Name, Tag = style });
            }

            SelectStylePreset(BalloonStylePresetComboBox, _selectedBalloonStyleId);
            SelectStylePreset(TextStylePresetComboBox, _selectedTextStyleId);

            if (_selectedBalloonStyleId.HasValue)
            {
                for (int i = 0; i < StyleStripComboBox.Items.Count; i++)
                {
                    if (StyleStripComboBox.Items[i] is ComboBoxItem item &&
                        item.Tag is Guid styleId &&
                        styleId == _selectedBalloonStyleId.Value)
                    {
                        StyleStripComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!_selectedBalloonStyleId.HasValue && StyleStripComboBox.SelectedIndex < 0 && StyleStripComboBox.Items.Count > 0)
            {
                StyleStripComboBox.SelectedIndex = 0;
                if (StyleStripComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is Guid selectedId)
                {
                    _selectedBalloonStyleId = selectedId;
                    SelectStylePreset(BalloonStylePresetComboBox, selectedId);
                }
            }

            StyleStripComboBox.IsEnabled = StyleStripComboBox.Items.Count > 0;
        }
        finally
        {
            _isUpdatingStylePresets = false;
        }

        UpdateStylePresetButtons();
        RefreshBalloonStyleEditorWindow();
    }

    private void SelectStylePreset(ComboBox comboBox, Guid? styleId)
    {
        comboBox.SelectedIndex = -1;
        if (!styleId.HasValue) return;

        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                item.Tag is NamedBalloonStyle balloonStyle &&
                balloonStyle.Id == styleId)
            {
                comboBox.SelectedIndex = i;
                return;
            }

            if (comboBox.Items[i] is ComboBoxItem textItem &&
                textItem.Tag is NamedTextStyle textStyle &&
                textStyle.Id == styleId)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private NamedBalloonStyle? SelectedBalloonStylePreset()
    {
        var selected = (BalloonStylePresetComboBox.SelectedItem as ComboBoxItem)?.Tag as NamedBalloonStyle;
        if (selected != null)
        {
            return selected;
        }

        return _selectedBalloonStyleId.HasValue
            ? _editorState.Document?.FindBalloonStyle(_selectedBalloonStyleId.Value)
            : null;
    }

    private NamedTextStyle? SelectedTextStylePreset()
    {
        return (TextStylePresetComboBox.SelectedItem as ComboBoxItem)?.Tag as NamedTextStyle;
    }

    private void UpdateStylePresetButtons()
    {
        var doc = _editorState.Document;
        var hasBalloonStyle = SelectedBalloonStylePreset() != null;
        var hasTextStyle = SelectedTextStylePreset() != null;
        var hasSelection = doc?.SelectedBalloon != null;

        ApplyBalloonStyleButton.IsEnabled = hasBalloonStyle && hasSelection;
        UpdateBalloonStyleButton.IsEnabled = hasBalloonStyle && hasSelection;
        RenameBalloonStyleButton.IsEnabled = hasBalloonStyle;
        DeleteBalloonStyleButton.IsEnabled = hasBalloonStyle && (doc?.BalloonStyles.Count ?? 0) > 1;

        ApplyTextStyleButton.IsEnabled = hasTextStyle && hasSelection;
        UpdateTextStyleButton.IsEnabled = hasTextStyle && hasSelection;
        RenameTextStyleButton.IsEnabled = hasTextStyle;
        DeleteTextStyleButton.IsEnabled = hasTextStyle && (doc?.TextStyles.Count ?? 0) > 1;
        if (StyleStripEditButton != null)
        {
            StyleStripEditButton.IsEnabled = doc != null;
        }
        UpdateBalloonTemplateButtons();
    }

    private async Task<string?> PromptStyleNameAsync(string title, string placeholder, string? currentName = null)
    {
        var input = new TextBox
        {
            PlaceholderText = placeholder,
            Text = currentName ?? ""
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = input,
            PrimaryButtonText = L("common.save"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var name = input.Text?.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private void ShapeComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (ShapeComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string shapeStr &&
            Enum.TryParse<BalloonShape>(shapeStr, out var shape))
        {
            CustomShapePanel.Visibility = shape == BalloonShape.Custom ? Visibility.Visible : Visibility.Collapsed;
            ThoughtSmoothnessPanel.Visibility = shape is BalloonShape.Thought or BalloonShape.Splat or BalloonShape.Burst ? Visibility.Visible : Visibility.Collapsed;
            UpdateThoughtSliderLabel(shape);
            _editorState.Execute(new SetBalloonShapeCommand(
                _editorState.Document.SelectedBalloon.Id, shape));
        }
    }

    private void UpdateThoughtSliderLabel(BalloonShape? shape = null)
    {
        if (ThoughtSmoothnessLabelText == null) return;

        var activeShape = shape ?? _editorState?.Document?.SelectedBalloon?.Shape;
        ThoughtSmoothnessLabelText.Text = activeShape == BalloonShape.Thought
            ? L("props.label.bubble_size")
            : L("props.label.smoothness");
    }

    private void ThoughtSmoothnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(e.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var smoothness = Math.Clamp((float)e.NewValue / 100f, 0f, 1f);
        ThoughtSmoothnessValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(smoothness - balloon.BalloonStyle.ThoughtSmoothness) < 0.001f) return;

        _editorState.Execute(new SetBalloonStyleCommand(
            balloon.Id,
            balloon.BalloonStyle.With(thoughtSmoothness: smoothness)));
        MainCanvas.Invalidate();
    }

    private void BalloonPanelComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        Guid? newPanelId = null;

        if (BalloonPanelComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is Guid panelId)
        {
            var page = _editorState.Document.ActivePage;
            if (page?.FindPanel(panelId) != null)
            {
                newPanelId = panelId;
            }
        }

        if (balloon.PanelId == newPanelId) return;

        _isUpdatingProperties = true;
        try
        {
            _editorState.Execute(new SetBalloonPanelCommand(balloon.Id, newPanelId));
            RefreshLayerList();
            MainCanvas.Invalidate();
        }
        finally
        {
            _isUpdatingProperties = false;
        }
    }

    private void BalloonConstrainToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        if (!balloon.PanelId.HasValue)
        {
            _isUpdatingProperties = true;
            BalloonConstrainToggle.IsOn = false;
            _isUpdatingProperties = false;
            return;
        }

        var newValue = BalloonConstrainToggle.IsOn;
        if (balloon.ConstrainToPanel == newValue) return;

        _editorState.Execute(new SetBalloonConstrainToPanelCommand(balloon.Id, newValue));
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
    }

    private void BalloonVisibilityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var isVisible = BalloonVisibilityToggle.IsOn;
        if (balloon.IsVisible == isVisible) return;

        _editorState.Execute(new SetBalloonVisibilityCommand(balloon.Id, isVisible));
        RefreshLayerList();
        MainCanvas.Invalidate();
    }

    private void BalloonLockToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var isLocked = BalloonLockToggle.IsOn;
        if (balloon.IsLocked == isLocked) return;

        _editorState.Execute(new SetBalloonLockedCommand(balloon.Id, isLocked));
        RefreshLayerList();
        MainCanvas.Invalidate();
    }

    private void UpdateBalloonPanelComboBox(Balloon balloon)
    {
        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        try
        {
            BalloonPanelComboBox.Items.Clear();

            var noneItem = new Microsoft.UI.Xaml.Controls.ComboBoxItem
            {
                Content = L("common.none_parens"),
                Tag = null
            };
            BalloonPanelComboBox.Items.Add(noneItem);

            var page = _editorState.Document?.ActivePage;
            if (page != null)
            {
                foreach (var panel in page.Panels.OrderBy(p => p.Order))
                {
                    var panelItem = new Microsoft.UI.Xaml.Controls.ComboBoxItem
                    {
                        Content = panel.Name,
                        Tag = panel.Id
                    };
                    BalloonPanelComboBox.Items.Add(panelItem);
                }
            }

            if (balloon.PanelId.HasValue)
            {
                for (int i = 0; i < BalloonPanelComboBox.Items.Count; i++)
                {
                    if (BalloonPanelComboBox.Items[i] is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
                        item.Tag is Guid panelId && panelId == balloon.PanelId.Value)
                    {
                        BalloonPanelComboBox.SelectedIndex = i;
                        return;
                    }
                }
            }

            BalloonPanelComboBox.SelectedIndex = 0; // "(None)"
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
        finally
        {
            _isUpdatingProperties = wasUpdating;
        }
    }

    private async void FillColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (FillColorComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string colorStr)
        {
            Model.Color color;

            if (colorStr == "CUSTOM")
            {
                var balloon = _editorState.Document.SelectedBalloon;
                var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.FillColor);
                if (customColor.HasValue)
                {
                    color = customColor.Value;
                }
                else
                {
                    _isUpdatingProperties = true;
                    FillColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                color = ParseHexColor(colorStr);
            }

            var balloon2 = _editorState.Document.SelectedBalloon;
            var newStyle = balloon2.BalloonStyle.With(fillColor: color);
            _editorState.Execute(new SetBalloonStyleCommand(balloon2.Id, newStyle));

            FillColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private async void StrokeColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (StrokeColorComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string colorStr)
        {
            Model.Color color;

            if (colorStr == "CUSTOM")
            {
                var balloon = _editorState.Document.SelectedBalloon;
                var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.StrokeColor);
                if (customColor.HasValue)
                {
                    color = customColor.Value;
                }
                else
                {
                    _isUpdatingProperties = true;
                    StrokeColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                color = ParseHexColor(colorStr);
            }

            var balloon2 = _editorState.Document.SelectedBalloon;
            var newStyle = balloon2.BalloonStyle.With(strokeColor: color);
            _editorState.Execute(new SetBalloonStyleCommand(balloon2.Id, newStyle));

            StrokeColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private async void TextColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (TextColorComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string colorStr)
        {
            Model.Color color;

            if (colorStr == "CUSTOM")
            {
                var balloon = _editorState.Document.SelectedBalloon;
                var currentColor = balloon.TextStyle.TextColor;
                var customColor = await ShowColorPickerDialogAsync(currentColor);
                if (customColor.HasValue)
                {
                    color = customColor.Value;
                }
                else
                {
                    _isUpdatingProperties = true;
                    TextColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                color = ParseHexColor(colorStr);
            }

            var balloon2 = _editorState.Document.SelectedBalloon;

            if (_editorState.Mode == EditorMode.EditText &&
                _editorState.EditingBalloonId == balloon2.Id &&
                _editorState.EditingSelectionLength > 0)
            {
                var currentStyle = _editorState.GetSelectionTextStyle();
                var updatedStyle = currentStyle.With(textColor: color);

                if (!currentStyle.TextColor.Equals(color))
                {
                    _editorState.ApplyTextStyleToSelection(updatedStyle);
                    MainCanvas.Invalidate();
                }
            }
            else
            {
                var newStyle = balloon2.TextStyle.With(textColor: color);
                if (!balloon2.TextStyle.TextColor.Equals(color))
                {
                    _editorState.Execute(new SetTextStyleCommand(balloon2.Id, newStyle));
                    MainCanvas.Invalidate();
                }
            }

            TextColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private async void OutlineColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (OutlineColorComboBox.SelectedItem is not Microsoft.UI.Xaml.Controls.ComboBoxItem item ||
            item.Tag is not string colorStr)
        {
            return;
        }

        var balloon = _editorState.Document.SelectedBalloon;
        Model.Color color;

        if (colorStr == "CUSTOM")
        {
            var currentColor = GetActiveTextStyleForProperties(balloon).OutlineColor;
            var customColor = await ShowColorPickerDialogAsync(currentColor);
            if (!customColor.HasValue)
            {
                UpdatePropertiesPanel();
                return;
            }

            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(colorStr);
        }

        var activeStyle = GetActiveTextStyleForProperties(balloon);
        if (activeStyle.OutlineColor.Equals(color))
        {
            return;
        }

        ApplyInlineTextStyle(style => style.With(outlineColor: color));
        OutlineColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
    }

    private void OutlineWidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var width = Math.Clamp((float)args.NewValue, 0f, 32f);
        ApplyInlineTextStyle(style => style.With(outlineWidth: width));
    }

    private void OutlineWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var width = Math.Clamp((float)e.NewValue, 0f, 8f);
        OutlineWidthValueText.Text = width.ToString("F1");
        ApplyInlineTextStyle(style => style.With(outlineWidth: width));
    }

    private void OutlineWidthPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (OutlineWidthPresetComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (!float.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var outlineWidth)) return;

        outlineWidth = Math.Clamp(outlineWidth, 0f, 32f);
        ApplyInlineTextStyle(style => style.With(outlineWidth: outlineWidth));
    }

    private async void AdditionalOutlineColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (sender is not ComboBox comboBox) return;
        if (!TryGetStrokeIndex(comboBox.Tag, out var index)) return;
        if (comboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string colorTag) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var activeStyle = GetActiveTextStyleForProperties(balloon);
        var currentStroke = GetAdditionalStroke(activeStyle.AdditionalStrokes, index, activeStyle.OutlineColor);
        Color? selectedColor = null;

        if (string.Equals(colorTag, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(currentStroke.Color);
            if (!customColor.HasValue)
            {
                UpdatePropertiesPanel();
                return;
            }

            selectedColor = customColor.Value;
        }
        else
        {
            selectedColor = ParseHexColor(colorTag);
        }

        if (!selectedColor.HasValue || currentStroke.Color.Equals(selectedColor.Value))
        {
            return;
        }

        var color = selectedColor.Value;
        ApplyInlineTextStyle(style => style.With(
            additionalStrokes: BuildAdditionalStrokeUpdate(style.AdditionalStrokes, index, color: color)));

        switch (index)
        {
            case 0:
                UpdateOutlineSelector(Outline2ColorPreview, Outline2ColorComboBox, color, "CUSTOM");
                break;
            case 1:
                UpdateOutlineSelector(Outline3ColorPreview, Outline3ColorComboBox, color, "CUSTOM");
                break;
        }
    }

    private void AdditionalOutlineWidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;
        if (!TryGetStrokeIndex(sender.Tag, out var index)) return;

        var width = Math.Clamp((float)args.NewValue, 0f, 32f);
        ApplyInlineTextStyle(style => style.With(
            additionalStrokes: BuildAdditionalStrokeUpdate(style.AdditionalStrokes, index, width: width)));
    }

    private void StrokeWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.BalloonStyle.With(strokeWidth: (float)e.NewValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void BalloonOpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newOpacity = (float)(e.NewValue / 100.0);
        if (Math.Abs(newOpacity - balloon.BalloonStyle.Opacity) < 0.001f) return;

        var newStyle = balloon.BalloonStyle.With(opacity: newOpacity);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
        BalloonOpacityValueText.Text = $"{e.NewValue:F0}%";
    }

    private void CornerRadiusSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newRadius = (float)e.NewValue;
        if (Math.Abs(newRadius - balloon.BalloonStyle.CornerRadius) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(cornerRadius: newRadius);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void PaddingLeftBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.PaddingLeft) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(paddingLeft: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void PaddingRightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.PaddingRight) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(paddingRight: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void PaddingTopBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.PaddingTop) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(paddingTop: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void PaddingBottomBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.PaddingBottom) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(paddingBottom: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void TextMarginSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newMargin = (float)e.NewValue;

        TextMarginValueText.Text = $"{newMargin:F0}px";

        var style = balloon.BalloonStyle;
        if (Math.Abs(newMargin - style.PaddingLeft) < 0.01f &&
            Math.Abs(newMargin - style.PaddingRight) < 0.01f &&
            Math.Abs(newMargin - style.PaddingTop) < 0.01f &&
            Math.Abs(newMargin - style.PaddingBottom) < 0.01f)
        {
            return;
        }

        var newStyle = style.With(
            paddingLeft: newMargin,
            paddingRight: newMargin,
            paddingTop: newMargin,
            paddingBottom: newMargin);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));

        _isUpdatingProperties = true;
        try
        {
            PaddingLeftBox.Value = newMargin;
            PaddingRightBox.Value = newMargin;
            PaddingTopBox.Value = newMargin;
            PaddingBottomBox.Value = newMargin;
        }
        finally
        {
            _isUpdatingProperties = false;
        }
    }

    private void MinWidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.MinWidth) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(minWidth: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void MinHeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.MinHeight) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(minHeight: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void MaxWidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.MaxWidth) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(maxWidth: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }

    private void MaxHeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newValue = (float)args.NewValue;
        if (Math.Abs(newValue - balloon.BalloonStyle.MaxHeight) < 0.01f) return;

        var newStyle = balloon.BalloonStyle.With(maxHeight: newValue);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));
    }


    private void ApplyCjkFontRecommendationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (ApplyCjkFontRecommendationButton.Tag is not string recommendedFont || string.IsNullOrWhiteSpace(recommendedFont)) return;

        ApplyInlineTextStyle(style => style.With(fontFamily: recommendedFont));
        SetStatusMessage(LF("props.cjk.applied_font", recommendedFont));
    }

    private void BalloonTextEditBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isEditingBalloonText = true;
    }

    private void BalloonTextEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isEditingBalloonText = false;
    }

    private void BalloonTextEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newText = BalloonTextEditBox.Text ?? "";

        if (newText == balloon.Text) return;

        _editorState.Execute(new SetBalloonTextCommand(balloon.Id, newText));
        MainCanvas.Invalidate();
    }

    private void FontSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        FontSizeValueText.Text = $"{e.NewValue:F0}px";
        ApplyInlineTextStyle(style => style.With(fontSize: (float)e.NewValue));
    }

    private void BoldToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        ApplyInlineTextStyle(style => style.With(bold: !style.Bold), preferInsertionModeWhenEditing: true);
    }

    private void ItalicToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        ApplyInlineTextStyle(style => style.With(italic: !style.Italic), preferInsertionModeWhenEditing: true);
    }

    private void UnderlineToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        ApplyInlineTextStyle(style => style.With(underline: !style.Underline), preferInsertionModeWhenEditing: true);
    }

    private void SuperscriptToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var activeStyle = GetActiveTextStyleForProperties(balloon);

        var enable = activeStyle.Script != TextScript.Superscript;
        var script = enable ? TextScript.Superscript : TextScript.Normal;
        ApplyInlineTextStyle(style => style.With(script: script));
    }

    private void SubscriptToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var activeStyle = GetActiveTextStyleForProperties(balloon);

        var enable = activeStyle.Script != TextScript.Subscript;
        var script = enable ? TextScript.Subscript : TextScript.Normal;
        ApplyInlineTextStyle(style => style.With(script: script));
    }

    private void AllCapsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(allCaps: AllCapsToggle.IsOn);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        UpdateAllCapsButtonAppearance();
        MainCanvas.Invalidate();
    }

    private void AllCapsButton_Click(object sender, RoutedEventArgs e)
    {
        AllCapsToggle.IsOn = !AllCapsToggle.IsOn;
    }

    private void UpdateAllCapsButtonAppearance()
    {
        if (AllCapsButton != null)
        {
            AllCapsButton.Background = AllCapsToggle.IsOn
                ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }
    }

    private void BalloonStylePresetComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingStylePresets) return;
        var style = SelectedBalloonStylePreset();
        _selectedBalloonStyleId = style?.Id;
        UpdateStylePresetButtons();
    }

    private void TextStylePresetComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingStylePresets) return;
        var style = SelectedTextStylePreset();
        _selectedTextStyleId = style?.Id;
        UpdateStylePresetButtons();
    }

    private void ApplyBalloonStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var style = SelectedBalloonStylePreset();
        if (doc == null || style == null) return;

        TryApplyBalloonStyleToSelection(style, statusOnNoSelection: true);
    }

    private bool TryApplyBalloonStyleToSelection(NamedBalloonStyle style, bool statusOnNoSelection)
    {
        var balloons = GetSelectedBalloons();
        if (balloons.Count == 0)
        {
            if (statusOnNoSelection)
            {
                SetStatusMessage(L("style.error.select_targets"));
            }

            return false;
        }

        var commands = balloons
            .Select(balloon => new SetBalloonStyleReferenceCommand(balloon.Id, style.Id))
            .Cast<ICommand>()
            .ToList();

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Apply balloon style", commands);
        }

        return true;
    }

    private void UpdateBalloonStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        var style = SelectedBalloonStylePreset();
        if (balloon == null || style == null) return;

        _editorState.Execute(new UpdateNamedBalloonStyleCommand(
            style.Id,
            balloon.BalloonStyle,
            applyExtendedDetails: true,
            shape: balloon.Shape,
            customShapePathData: balloon.CustomShapePathData,
            constrainToPanel: balloon.ConstrainToPanel,
            textStyle: balloon.TextStyle,
            textPath: balloon.TextPath?.Clone(),
            tails: BuildNamedStyleTailSnapshots(balloon)));
    }

    private async void NewBalloonStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        if (balloon == null) return;

        var name = await PromptStyleNameAsync("New Balloon Style", "Style name");
        if (string.IsNullOrWhiteSpace(name)) return;

        var cmd = new CreateNamedBalloonStyleCommand(
            name,
            balloon.BalloonStyle,
            applyExtendedDetails: true,
            shape: balloon.Shape,
            customShapePathData: balloon.CustomShapePathData,
            constrainToPanel: balloon.ConstrainToPanel,
            textStyle: balloon.TextStyle,
            textPath: balloon.TextPath?.Clone(),
            tails: BuildNamedStyleTailSnapshots(balloon));
        _editorState.Execute(cmd);
        _selectedBalloonStyleId = cmd.CreatedStyleId;
        RefreshStylePresets();
    }

    private async void RenameBalloonStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = SelectedBalloonStylePreset();
        if (style == null) return;

        var name = await PromptStyleNameAsync("Rename Balloon Style", "Style name", style.Name);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, style.Name, StringComparison.Ordinal)) return;

        _editorState.Execute(new RenameNamedBalloonStyleCommand(style.Id, name));
    }

    private async void DeleteBalloonStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = SelectedBalloonStylePreset();
        if (style == null) return;

        if (!await ConfirmStyleDeleteAsync(style.Name, "balloon")) return;
        _editorState.Execute(new DeleteNamedBalloonStyleCommand(style.Id));
        _selectedBalloonStyleId = null;
    }

    private void ApplyTextStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var style = SelectedTextStylePreset();
        if (doc == null || style == null) return;

        var activeBalloon = doc.SelectedBalloon;
        if (activeBalloon == null) return;

        if (_editorState.Mode == EditorMode.EditText &&
            _editorState.EditingBalloonId == activeBalloon.Id &&
            _editorState.HasSelection)
        {
            _editorState.ApplyTextStyleToSelection(style.Style);
            return;
        }

        var balloons = GetSelectedBalloons();
        if (balloons.Count == 0) return;

        var commands = balloons
            .Select(balloon => new SetTextStyleReferenceCommand(balloon.Id, style.Id))
            .Cast<ICommand>()
            .ToList();

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Apply text style", commands);
        }
    }

    private void UpdateTextStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        var style = SelectedTextStylePreset();
        if (balloon == null || style == null) return;

        var sourceStyle = (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId == balloon.Id)
            ? _editorState.GetSelectionTextStyle()
            : balloon.TextStyle;

        _editorState.Execute(new UpdateNamedTextStyleCommand(style.Id, sourceStyle));
    }

    private async void NewTextStyle_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        if (balloon == null) return;

        var name = await PromptStyleNameAsync("New Text Style", "Style name");
        if (string.IsNullOrWhiteSpace(name)) return;

        var baseStyle = (_editorState.Mode == EditorMode.EditText && _editorState.EditingBalloonId == balloon.Id)
            ? _editorState.GetSelectionTextStyle()
            : balloon.TextStyle;

        var cmd = new CreateNamedTextStyleCommand(name, baseStyle);
        _editorState.Execute(cmd);
        _selectedTextStyleId = cmd.CreatedStyleId;
        RefreshStylePresets();
    }

    private async void RenameTextStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = SelectedTextStylePreset();
        if (style == null) return;

        var name = await PromptStyleNameAsync("Rename Text Style", "Style name", style.Name);
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, style.Name, StringComparison.Ordinal)) return;

        _editorState.Execute(new RenameNamedTextStyleCommand(style.Id, name));
    }

    private async void DeleteTextStyle_Click(object sender, RoutedEventArgs e)
    {
        var style = SelectedTextStylePreset();
        if (style == null) return;

        if (!await ConfirmStyleDeleteAsync(style.Name, "text")) return;
        _editorState.Execute(new DeleteNamedTextStyleCommand(style.Id));
        _selectedTextStyleId = null;
    }

    private async Task<bool> ConfirmStyleDeleteAsync(string name, string label)
    {
        var dialog = new ContentDialog
        {
            Title = LF("props.style.delete_title", label),
            Content = LF("props.style.delete_confirm", name),
            PrimaryButtonText = L("common.delete"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static IReadOnlyList<BalloonTemplateTail> BuildNamedStyleTailSnapshots(Balloon balloon)
    {
        return balloon.Tails
            .Select(tail => BalloonTemplateTail.FromTail(tail, balloon.Position))
            .ToList();
    }

    private void TrackingSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        TrackingValueText.Text = $"{e.NewValue:F2}";
        ApplyInlineTextStyle(style => style.With(tracking: (float)e.NewValue));
    }

    private void LineSpacingSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        LineSpacingValueText.Text = $"{e.NewValue:F2}x";
        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(lineHeight: (float)e.NewValue);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void TextVerticalOffsetSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        VerticalOffsetValueText.Text = $"{e.NewValue:F0}px";
        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(verticalOffset: (float)e.NewValue);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void JustificationStrengthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(justificationStrength: (int)e.NewValue);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void TextAlignmentComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (TextAlignmentComboBox.SelectedItem is not Microsoft.UI.Xaml.Controls.ComboBoxItem item) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var alignment = item.Tag?.ToString() switch
        {
            "Left" => Model.TextAlignment.Left,
            "Center" => Model.TextAlignment.Center,
            "Right" => Model.TextAlignment.Right,
            _ => Model.TextAlignment.Center
        };

        if (balloon.TextStyle.Alignment == alignment) return;

        var newStyle = balloon.TextStyle.With(alignment: alignment);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void FitModeComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (FitModeComboBox.SelectedItem is not Microsoft.UI.Xaml.Controls.ComboBoxItem item) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var fitMode = item.Tag?.ToString() switch
        {
            "GrowBalloon" => TextFitMode.GrowBalloon,
            "None" => TextFitMode.None,
            "ShrinkToFit" => TextFitMode.ShrinkToFit,
            _ => TextFitMode.GrowBalloon
        };

        if (balloon.TextStyle.FitMode == fitMode) return;

        var newStyle = balloon.TextStyle.With(fitMode: fitMode);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void FillHeightCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(fillHeight: true);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void FillHeightCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(fillHeight: false);
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void TextOverflowComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (TextOverflowComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string overflowStr &&
            Enum.TryParse<TextOverflowMode>(overflowStr, out var overflowMode))
        {
            var balloon = _editorState.Document.SelectedBalloon;
            var newStyle = balloon.TextStyle.With(overflowMode: overflowMode);
            _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
            MainCanvas.Invalidate();
        }
    }


    private void HasTailToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var hasTail = HasTailToggle.IsOn;

        if (hasTail && balloon.Tail == null)
        {
            var tailTarget = balloon.Position + new Point2(0, balloon.ComputedSize.Height / 2 + 50);
            var preferredTail = ResolvePreferredTailSettings();
            ExecuteCreateTailWithSettings(
                balloon.Id,
                tailTarget,
                preferredTail.style,
                preferredTail.width,
                preferredTail.curvature,
                preferredTail.curveCenter,
                preferredTail.inset);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
        }
        else if (!hasTail && balloon.Tail != null)
        {
            _editorState.Execute(new DeleteTailCommand(balloon.Id));
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
        }
    }

    private void TailStyleComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon?.Tail == null) return;

        if (TailStyleComboBox.SelectedItem is Microsoft.UI.Xaml.Controls.ComboBoxItem item &&
            item.Tag is string styleStr &&
            Enum.TryParse<TailStyle>(styleStr, out var style))
        {
            var balloon = _editorState.Document.SelectedBalloon;
            if (balloon.Tail!.Style == style) return;
            _editorState.Execute(new SetTailStyleCommand(balloon.Id, style));
            CaptureLastUsedTailFromBalloon(balloon);

            var showCurvedControls = style == TailStyle.Curved;
            var showInsetControl = style is TailStyle.Pointer or TailStyle.Curved;
            TailCurvaturePanel.Visibility = showCurvedControls ? Visibility.Visible : Visibility.Collapsed;
            TailCurveCenterPanel.Visibility = showCurvedControls ? Visibility.Visible : Visibility.Collapsed;
            TailInsetPanel.Visibility = showInsetControl ? Visibility.Visible : Visibility.Collapsed;
            if (showInsetControl)
            {
                TailInsetSlider.Value = balloon.Tail!.Inset;
                TailInsetValueText.Text = $"{balloon.Tail.Inset:F0}";
            }
        }
    }

    private void TailWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon?.Tail == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newWidth = (float)e.NewValue;
        TailWidthValueText.Text = $"{newWidth:F0}";

        if (Math.Abs(newWidth - balloon.Tail!.BaseWidth) < 0.01f) return;
        _editorState.Execute(new SetTailWidthCommand(balloon.Id, newWidth));
        CaptureLastUsedTailFromBalloon(balloon);
    }

    private void TailCurvatureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon?.Tail == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newCurvature = (float)(e.NewValue / 100.0); // Convert from -200..200 to -2..2
        TailCurvatureValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(newCurvature - balloon.Tail!.Curvature) < 0.01f) return;
        _editorState.Execute(new SetTailCurvatureCommand(balloon.Id, newCurvature));
        CaptureLastUsedTailFromBalloon(balloon);
    }

    private void TailCurveCenterSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon?.Tail == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newCenter = (float)(e.NewValue / 100.0);
        TailCurveCenterValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(newCenter - balloon.Tail!.CurveCenter) < 0.01f) return;
        _editorState.Execute(new SetTailCurveCenterCommand(balloon.Id, newCenter));
        CaptureLastUsedTailFromBalloon(balloon);
    }

    private void TailInsetSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon?.Tail == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newInset = (float)e.NewValue;
        TailInsetValueText.Text = $"{newInset:F0}";

        if (Math.Abs(newInset - balloon.Tail!.Inset) < 0.01f) return;
        _editorState.Execute(new SetTailInsetCommand(balloon.Id, newInset));
        CaptureLastUsedTailFromBalloon(balloon);
    }

    private void RotationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newRotation = (float)e.NewValue;
        RotationValueText.Text = $"{newRotation:F0}°";

        if (Math.Abs(newRotation - balloon.Rotation) < 0.1f) return;
        _editorState.Execute(new RotateBalloonCommand(balloon.Id, newRotation));
    }

    private void BalloonPositionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newX = (float)(BalloonXBox.Value);
        var newY = (float)(BalloonYBox.Value);
        var newPos = new Point2(newX, newY);

        if (Math.Abs(newPos.X - balloon.Position.X) < 0.1f &&
            Math.Abs(newPos.Y - balloon.Position.Y) < 0.1f) return;

        _editorState.Execute(new MoveBalloonCommand(balloon.Id, newPos));
        MainCanvas.Invalidate();
    }

    private void BalloonSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newWidth = (float)Math.Max(10, BalloonWidthBox.Value);
        var newHeight = (float)Math.Max(10, BalloonHeightBox.Value);
        var newSize = new Size2(newWidth, newHeight);

        if (Math.Abs(newSize.Width - balloon.ComputedSize.Width) < 0.1f &&
            Math.Abs(newSize.Height - balloon.ComputedSize.Height) < 0.1f) return;

        _editorState.Execute(new ResizeBalloonCommand(balloon.Id, newSize, balloon.Position));
        MainCanvas.Invalidate();
    }

    private void BalloonRotationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_editorState == null) return; // Guard during XAML initialization
        if (_isUpdatingProperties || _editorState.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newRotation = (float)e.NewValue;

        if (Math.Abs(newRotation - balloon.Rotation) < 0.1f) return;

        _editorState.Execute(new RotateBalloonCommand(balloon.Id, newRotation));
        BalloonRotationBox.Value = newRotation;
        MainCanvas.Invalidate();
    }

    private void BalloonRotationBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newRotation = (float)Math.Clamp(args.NewValue, -180, 180);

        if (Math.Abs(newRotation - balloon.Rotation) < 0.1f) return;

        _editorState.Execute(new RotateBalloonCommand(balloon.Id, newRotation));
        BalloonRotationSlider.Value = newRotation;
        MainCanvas.Invalidate();
    }

    private void ResetTailAttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState?.Document?.SelectedBalloon?.Tail == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        if (balloon.Tail!.AttachmentDirection == null) return;

        _editorState.Execute(new SetTailAttachmentDirectionCommand(balloon.Id, null));
        UpdatePropertiesPanel();
    }

    private void AddTailButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var offsetIndex = balloon.Tails.Count;
        var offsetX = 30f * offsetIndex;
        var tailTarget = new Point2(
            balloon.Position.X + offsetX,
            balloon.Position.Y + balloon.Bounds.Height + 50);

        var sourceTail = balloon.Tail;
        var settings = sourceTail != null
            ? (
                style: sourceTail.Style,
                width: sourceTail.BaseWidth,
                curvature: sourceTail.Curvature,
                curveCenter: sourceTail.CurveCenter,
                inset: sourceTail.Inset)
            : ResolvePreferredTailSettings();
        ExecuteCreateTailWithSettings(
            balloon.Id,
            tailTarget,
            settings.style,
            settings.width,
            settings.curvature,
            settings.curveCenter,
            settings.inset);
        UpdatePropertiesPanel();
    }


    private void UpdateEffectsTabUi(Balloon balloon)
    {
        var style = balloon.BalloonStyle;

        ShadowToggle.IsOn = style.ShadowEnabled;
        ShadowOptionsPanel.Visibility = style.ShadowEnabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateColorSelector(ShadowColorPreview, ShadowColorComboBox, style.ShadowColor);
        ShadowOpacitySlider.Value = style.ShadowOpacity * 100f;
        ShadowOpacityValueText.Text = $"{style.ShadowOpacity * 100f:F0}%";
        ShadowFalloffSlider.Value = style.ShadowFalloff;
        ShadowFalloffValueText.Text = $"{style.ShadowFalloff:F1}";
        ShadowOffsetXSlider.Value = style.ShadowOffsetX;
        ShadowOffsetYSlider.Value = style.ShadowOffsetY;
        ShadowOffsetXValueText.Text = $"{style.ShadowOffsetX:F1}";
        ShadowOffsetYValueText.Text = $"{style.ShadowOffsetY:F1}";

        GlowToggle.IsOn = style.GlowEnabled;
        GlowOptionsPanel.Visibility = style.GlowEnabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateColorSelector(GlowColorPreview, GlowColorComboBox, style.GlowColor);
        GlowRadiusSlider.Value = style.GlowSize;
        GlowRadiusValueText.Text = $"{style.GlowSize:F1}";
        GlowIntensitySlider.Value = style.GlowOpacity * 100f;
        GlowIntensityValueText.Text = $"{style.GlowOpacity * 100f:F0}%";

        GradientToggle.IsOn = style.GradientEnabled;
        GradientOptionsPanel.Visibility = style.GradientEnabled ? Visibility.Visible : Visibility.Collapsed;
        SelectComboBoxItemByTag(GradientTypeComboBox, style.GradientType.ToString());
        GradientAngleSlider.Value = style.GradientAngle;
        GradientAngleValueText.Text = $"{style.GradientAngle:F0}°";
        UpdateGradientAngleVisibility(style.GradientType);
        UpdateColorSelector(GradientStartColorPreview, GradientStartColorComboBox, style.GradientStartColor);
        UpdateColorSelector(GradientEndColorPreview, GradientEndColorComboBox, style.GradientEndColor);
        PatternToggle.IsOn = style.PatternEnabled;
        PatternOptionsPanel.Visibility = style.PatternEnabled ? Visibility.Visible : Visibility.Collapsed;
        SelectComboBoxItemByTag(PatternTypeComboBox, style.PatternType.ToString());
        PatternScaleSlider.Value = style.PatternScale * 100f;
        PatternScaleValueText.Text = $"{style.PatternScale:F2}x";
        PatternAngleSlider.Value = style.PatternAngle;
        PatternAngleValueText.Text = $"{style.PatternAngle:F0}°";
        UpdateColorSelector(PatternSecondaryColorPreview, PatternSecondaryColorComboBox, style.PatternSecondaryColor);
        BalloonPatternImagePathBox.Text = style.PatternImagePath ?? string.Empty;

        RefreshTextEffectsControls(balloon);
    }

    private void ShadowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.BalloonStyle.With(shadowEnabled: ShadowToggle.IsOn);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));

        ShadowOptionsPanel.Visibility = ShadowToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ShadowColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (ShadowColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document.SelectedBalloon;
        Model.Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.ShadowColor);
            if (!customColor.HasValue)
            {
                _isUpdatingProperties = true;
                UpdateColorSelector(ShadowColorPreview, ShadowColorComboBox, balloon.BalloonStyle.ShadowColor);
                _isUpdatingProperties = false;
                return;
            }

            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(shadowColor: color)));
    }

    private void ShadowStyleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(args.NewValue)) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var value = (float)args.NewValue;

        if (ReferenceEquals(sender, ShadowOpacitySlider))
        {
            ShadowOpacityValueText.Text = $"{value:F0}%";
            var opacity = value / 100f;
            if (Math.Abs(opacity - balloon.BalloonStyle.ShadowOpacity) < 0.001f) return;
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(shadowOpacity: opacity)));
            return;
        }

        if (ReferenceEquals(sender, ShadowFalloffSlider))
        {
            ShadowFalloffValueText.Text = $"{value:F1}";
            if (Math.Abs(value - balloon.BalloonStyle.ShadowFalloff) < 0.01f) return;
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(shadowFalloff: value)));
        }
    }

    private bool ShouldShowPagePropertiesInInspector()
    {
        if (!string.Equals(_activeLeftSidebarTab, "Pages", StringComparison.Ordinal))
        {
            return false;
        }

        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            return false;
        }

        if (_editorState.SelectedBalloonIds.Count > 0) return false;
        if (_editorState.SelectedPanelId.HasValue) return false;
        if (_editorState.SelectedFloatingImageId.HasValue) return false;
        return true;
    }

    private void ShadowOffsetSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(args.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var value = (float)args.NewValue;

        if (ReferenceEquals(sender, ShadowOffsetXSlider))
        {
            ShadowOffsetXValueText.Text = $"{value:F1}";
            if (Math.Abs(value - balloon.BalloonStyle.ShadowOffsetX) < 0.01f) return;

            var xStyle = balloon.BalloonStyle.With(shadowOffsetX: value);
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, xStyle));
            return;
        }

        if (ReferenceEquals(sender, ShadowOffsetYSlider))
        {
            ShadowOffsetYValueText.Text = $"{value:F1}";
            if (Math.Abs(value - balloon.BalloonStyle.ShadowOffsetY) < 0.01f) return;

            var yStyle = balloon.BalloonStyle.With(shadowOffsetY: value);
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, yStyle));
        }
    }

    private void GlowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.BalloonStyle.With(glowEnabled: GlowToggle.IsOn);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));

        GlowOptionsPanel.Visibility = GlowToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void GlowColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (GlowColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document.SelectedBalloon;
        Model.Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.GlowColor);
            if (!customColor.HasValue)
            {
                _isUpdatingProperties = true;
                UpdateColorSelector(GlowColorPreview, GlowColorComboBox, balloon.BalloonStyle.GlowColor);
                _isUpdatingProperties = false;
                return;
            }

            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(glowColor: color)));
    }

    private void GlowStyleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(e.NewValue)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var value = (float)e.NewValue;

        if (ReferenceEquals(sender, GlowRadiusSlider))
        {
            GlowRadiusValueText.Text = $"{value:F1}";
            if (Math.Abs(value - balloon.BalloonStyle.GlowSize) < 0.01f) return;
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(glowSize: value)));
            return;
        }

        if (ReferenceEquals(sender, GlowIntensitySlider))
        {
            GlowIntensityValueText.Text = $"{value:F0}%";
            var opacity = value / 100f;
            if (Math.Abs(opacity - balloon.BalloonStyle.GlowOpacity) < 0.001f) return;
            _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(glowOpacity: opacity)));
        }
    }

    private void GradientToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var enableGradient = GradientToggle.IsOn;
        var newStyle = balloon.BalloonStyle.With(
            gradientEnabled: enableGradient,
            patternEnabled: enableGradient ? false : balloon.BalloonStyle.PatternEnabled);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));

        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        GradientOptionsPanel.Visibility = enableGradient ? Visibility.Visible : Visibility.Collapsed;
        PatternOptionsPanel.Visibility = newStyle.PatternEnabled ? Visibility.Visible : Visibility.Collapsed;
        PatternToggle.IsOn = newStyle.PatternEnabled;
        _isUpdatingProperties = wasUpdating;
        UpdateGradientAngleVisibility(newStyle.GradientType);
    }

    private void GradientTypeComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (GradientTypeComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var gradientType = string.Equals(tag, "Radial", StringComparison.OrdinalIgnoreCase)
            ? BalloonGradientType.Radial
            : BalloonGradientType.Linear;
        UpdateGradientAngleVisibility(gradientType);

        var balloon = _editorState.Document.SelectedBalloon;
        if (balloon.BalloonStyle.GradientType == gradientType) return;
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(gradientType: gradientType)));
    }

    private void GradientAngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(e.NewValue)) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var value = (float)e.NewValue;
        GradientAngleValueText.Text = $"{value:F0}°";

        if (Math.Abs(value - balloon.BalloonStyle.GradientAngle) < 0.01f) return;
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(gradientAngle: value)));
    }

    private async void GradientStartColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (GradientStartColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorHex)
        {
            Model.Color color;

            if (string.Equals(colorHex, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var balloon = _editorState.Document.SelectedBalloon;
                var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.GradientStartColor);
                if (customColor.HasValue)
                {
                    color = customColor.Value;
                }
                else
                {
                    _isUpdatingProperties = true;
                    GradientStartColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                color = ParseHexColor(colorHex);
            }

            var balloon2 = _editorState.Document.SelectedBalloon;
            var newStyle = balloon2.BalloonStyle.With(gradientStartColor: color);
            _editorState.Execute(new SetBalloonStyleCommand(balloon2.Id, newStyle));
        }
    }

    private async void GradientEndColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        if (GradientEndColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorHex)
        {
            Model.Color color;

            if (string.Equals(colorHex, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var balloon = _editorState.Document.SelectedBalloon;
                var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.GradientEndColor);
                if (customColor.HasValue)
                {
                    color = customColor.Value;
                }
                else
                {
                    _isUpdatingProperties = true;
                    GradientEndColorComboBox.SelectedIndex = -1;
                    _isUpdatingProperties = false;
                    return;
                }
            }
            else
            {
                color = ParseHexColor(colorHex);
            }

            var balloon2 = _editorState.Document.SelectedBalloon;
            var newStyle = balloon2.BalloonStyle.With(gradientEndColor: color);
            _editorState.Execute(new SetBalloonStyleCommand(balloon2.Id, newStyle));
        }
    }

    private void UpdateGradientAngleVisibility(BalloonGradientType gradientType)
    {
        GradientAnglePanel.Visibility = gradientType == BalloonGradientType.Linear
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void PatternToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var enablePattern = PatternToggle.IsOn;
        var newStyle = balloon.BalloonStyle.With(
            patternEnabled: enablePattern,
            gradientEnabled: enablePattern ? false : balloon.BalloonStyle.GradientEnabled);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, newStyle));

        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        PatternOptionsPanel.Visibility = enablePattern ? Visibility.Visible : Visibility.Collapsed;
        GradientOptionsPanel.Visibility = newStyle.GradientEnabled ? Visibility.Visible : Visibility.Collapsed;
        GradientToggle.IsOn = newStyle.GradientEnabled;
        _isUpdatingProperties = wasUpdating;
    }

    private void PatternTypeComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (PatternTypeComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (!Enum.TryParse<TextFillPattern>(tag, out var patternType)) return;

        var balloon = _editorState.Document.SelectedBalloon;
        if (balloon.BalloonStyle.PatternType == patternType) return;
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(patternType: patternType)));
    }

    private void PatternScaleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(e.NewValue)) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var value = Math.Clamp((float)e.NewValue / 100f, 0.25f, 8f);
        PatternScaleValueText.Text = $"{value:F2}x";

        if (Math.Abs(value - balloon.BalloonStyle.PatternScale) < 0.001f) return;
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(patternScale: value)));
    }

    private void PatternAngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null || double.IsNaN(e.NewValue)) return;
        var balloon = _editorState.Document.SelectedBalloon;
        var value = Math.Clamp((float)e.NewValue, -360f, 360f);
        PatternAngleValueText.Text = $"{value:F0}°";

        if (Math.Abs(value - balloon.BalloonStyle.PatternAngle) < 0.01f) return;
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(patternAngle: value)));
    }

    private async void PatternSecondaryColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;
        if (PatternSecondaryColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document.SelectedBalloon;
        Model.Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.BalloonStyle.PatternSecondaryColor);
            if (!customColor.HasValue)
            {
                _isUpdatingProperties = true;
                UpdateColorSelector(PatternSecondaryColorPreview, PatternSecondaryColorComboBox, balloon.BalloonStyle.PatternSecondaryColor);
                _isUpdatingProperties = false;
                return;
            }

            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, balloon.BalloonStyle.With(patternSecondaryColor: color)));
    }

    private async void BalloonPatternImageBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var picker = new FileOpenPicker();
        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        _textFillBitmapFailures.Remove(file.Path);

        var balloon = _editorState.Document.SelectedBalloon;
        var style = balloon.BalloonStyle.With(
            patternEnabled: true,
            gradientEnabled: false,
            patternImagePath: file.Path);
        _editorState.Execute(new SetBalloonStyleCommand(balloon.Id, style));
        UpdatePropertiesPanel();
    }

    private void BalloonPatternImageClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        if (string.IsNullOrWhiteSpace(balloon.BalloonStyle.PatternImagePath))
        {
            return;
        }

        _editorState.Execute(new SetBalloonStyleCommand(
            balloon.Id,
            balloon.BalloonStyle.With(patternImagePath: string.Empty)));
        UpdatePropertiesPanel();
    }

    private void LinkColorComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (LinkColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorHex)
        {
            var baseColor = ParseHexColor(colorHex);
            var currentAlpha = page.BalloonLinkStyle.StrokeColor.A;
            var color = new Model.Color(baseColor.R, baseColor.G, baseColor.B, currentAlpha);
            var newStyle = page.BalloonLinkStyle.With(strokeColor: color);
            _editorState.Execute(new SetBalloonLinkStyleCommand(page.Id, newStyle));

            LinkColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private void LinkWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_editorState == null || _isUpdatingProperties) return; // Guard during XAML initialization

        var page = _editorState.Document?.ActivePage;
        if (page == null || double.IsNaN(e.NewValue)) return;

        var newStyle = page.BalloonLinkStyle.With(strokeWidth: (float)e.NewValue);
        _editorState.Execute(new SetBalloonLinkStyleCommand(page.Id, newStyle));
    }

    private void LinkConnectorWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_editorState == null || _isUpdatingProperties) return; // Guard during XAML initialization

        var page = _editorState.Document?.ActivePage;
        if (page == null || double.IsNaN(e.NewValue)) return;

        var newStyle = page.BalloonLinkStyle.With(connectorWidth: (float)e.NewValue);
        _editorState.Execute(new SetBalloonLinkStyleCommand(page.Id, newStyle));
    }

    private void LinkDashComboBox_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (LinkDashComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string dashTag &&
            Enum.TryParse<LinkDashStyle>(dashTag, out var dashStyle))
        {
            var newStyle = page.BalloonLinkStyle.With(dashStyle: dashStyle);
            _editorState.Execute(new SetBalloonLinkStyleCommand(page.Id, newStyle));
        }
    }

    private void ClearLinksButton_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null || page.BalloonLinks.Count == 0) return;

        _editorState.Execute(new ClearBalloonLinksCommand(page.Id));
    }


    private void SnapToGuidesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _editorState.SnapToGuides = SnapToGuidesToggle.IsOn;
        if (SnapToolbarToggle != null)
        {
            SnapToolbarToggle.IsChecked = _editorState.SnapToGuides;
        }
        if (SnapToGuidesMenuItem != null)
        {
            SnapToGuidesMenuItem.IsChecked = _editorState.SnapToGuides;
        }
        UpdateToolButtonStates();
    }

    private void LockGuidesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (page.GuidesLocked == LockGuidesToggle.IsOn) return;

        _editorState.Execute(new SetGuidesLockedCommand(page.Id, LockGuidesToggle.IsOn));
        UpdateGuideLockUi(page);
        UpdateGuideList();
        MainCanvas.Invalidate();
    }

    private void PanelBoundaryVisibilityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        if (PanelBoundaryVisibilityComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string modeTag &&
            Enum.TryParse<PanelBoundaryVisibilityMode>(modeTag, out var mode))
        {
            _editorState.PanelBoundaryVisibilityMode = mode;
            SetStatusMessage(mode switch
            {
                PanelBoundaryVisibilityMode.Always => L("props.guides.boundary_always"),
                PanelBoundaryVisibilityMode.LayoutOnly => L("props.guides.boundary_layout"),
                PanelBoundaryVisibilityMode.Hover => L("props.guides.boundary_hover"),
                _ => L("props.guides.boundary_hidden")
            });
            MainCanvas.Invalidate();
        }
    }

    private void PanelSafeGuidesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        _editorState.ShowPanelSafeGuides = PanelSafeGuidesToggle.IsOn;
        if (PanelSafeGuidesToggle.IsOn)
        {
            SetStatusMessage(L("props.guides.safe_enabled"));
        }
        MainCanvas.Invalidate();
    }

    private void PanelGutterGuidesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        _editorState.ShowPanelGutters = PanelGutterGuidesToggle.IsOn;
        if (PanelGutterGuidesToggle.IsOn)
        {
            SetStatusMessage(L("props.guides.gutter_enabled"));
        }
        MainCanvas.Invalidate();
    }

    private void PanelGutterFillToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        ApplyPanelGutterStyleFromControls();
    }

    private void PanelGutterColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        ApplyPanelGutterStyleFromControls();
    }

    private void PanelGutterStrokeStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        ApplyPanelGutterStyleFromControls();
    }

    private void ReadingDirectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        if (ReadingDirectionComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string directionTag &&
            Enum.TryParse<ReadingDirection>(directionTag, out var direction))
        {
            if (page.ReadingDirection == direction) return;
            _editorState.Execute(new SetPageReadingDirectionCommand(page.Id, direction));
            SetStatusMessage(direction switch
            {
                ReadingDirection.RightToLeft => "Reading direction set to right-to-left.",
                ReadingDirection.Manual => "Reading direction set to manual.",
                _ => "Reading direction set to left-to-right."
            });
            MainCanvas.Invalidate();
        }
    }

    private void SetPanelBoundaryVisibilityCombo()
    {
        var modeTag = _editorState.PanelBoundaryVisibilityMode.ToString();
        PanelBoundaryVisibilityComboBox.SelectedIndex = -1;
        for (int i = 0; i < PanelBoundaryVisibilityComboBox.Items.Count; i++)
        {
            if (PanelBoundaryVisibilityComboBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == modeTag)
            {
                PanelBoundaryVisibilityComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void SetReadingDirectionCombo(DocumentPage page)
    {
        var directionTag = page.ReadingDirection.ToString();
        ReadingDirectionComboBox.SelectedIndex = -1;
        for (int i = 0; i < ReadingDirectionComboBox.Items.Count; i++)
        {
            if (ReadingDirectionComboBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == directionTag)
            {
                ReadingDirectionComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void SetPanelGutterStyleControls(DocumentPage page)
    {
        PanelGutterFillToggle.IsOn = page.PanelGutterFillEnabled;

        var color = page.PanelGutterColor;
        PanelGutterColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        PanelGutterColorComboBox.SelectedIndex = -1;
        var colorTag = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        for (int i = 0; i < PanelGutterColorComboBox.Items.Count; i++)
        {
            if (PanelGutterColorComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), colorTag, StringComparison.OrdinalIgnoreCase))
            {
                PanelGutterColorComboBox.SelectedIndex = i;
                break;
            }
        }

        PanelGutterStrokeStyleComboBox.SelectedIndex = -1;
        var styleTag = page.PanelGutterStrokeStyle.ToString();
        for (int i = 0; i < PanelGutterStrokeStyleComboBox.Items.Count; i++)
        {
            if (PanelGutterStrokeStyleComboBox.Items[i] is ComboBoxItem item &&
                item.Tag?.ToString() == styleTag)
            {
                PanelGutterStrokeStyleComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private void UpdateGuideLockUi(DocumentPage page)
    {
        var editable = !page.GuidesLocked;
        if (AddHorizontalGuideButton != null) AddHorizontalGuideButton.IsEnabled = editable;
        if (AddVerticalGuideButton != null) AddVerticalGuideButton.IsEnabled = editable;
        if (ClearGuidesButton != null) ClearGuidesButton.IsEnabled = editable && page.Guides.Count > 0;
        if (GuideListEmptyText != null && page.GuidesLocked && page.Guides.Count == 0)
        {
            GuideListEmptyText.Text = L("props.guides.locked");
        }
        else if (GuideListEmptyText != null)
        {
            GuideListEmptyText.Text = L("props.guides.no_guides");
        }
    }

    private void ApplyPanelGutterStyleFromControls()
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        var color = page.PanelGutterColor;
        if (PanelGutterColorComboBox.SelectedItem is ComboBoxItem colorItem &&
            colorItem.Tag is string colorHex)
        {
            var baseColor = ParseHexColor(colorHex);
            color = new Model.Color(baseColor.R, baseColor.G, baseColor.B, page.PanelGutterColor.A);
            PanelGutterColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }

        var style = page.PanelGutterStrokeStyle;
        if (PanelGutterStrokeStyleComboBox.SelectedItem is ComboBoxItem styleItem &&
            styleItem.Tag is string styleTag &&
            Enum.TryParse<PanelBorderStyle>(styleTag, out var parsed))
        {
            style = parsed;
        }

        var fillEnabled = PanelGutterFillToggle.IsOn;
        if (color.Equals(page.PanelGutterColor) &&
            style == page.PanelGutterStrokeStyle &&
            fillEnabled == page.PanelGutterFillEnabled)
        {
            return;
        }

        _editorState.Execute(new SetPanelGutterStyleCommand(page.Id, color, style, fillEnabled));
        MainCanvas.Invalidate();
    }

    private void AddHorizontalGuide_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("props.guides.locked_unlock_add"));
            return;
        }

        var position = page.Size.Height / 2f;
        _editorState.Execute(new CreateGuideCommand(page.Id, GuideOrientation.Horizontal, position));
        UpdateGuideList();
        MainCanvas.Invalidate();
    }

    private void AddVerticalGuide_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("props.guides.locked_unlock_add"));
            return;
        }

        var position = page.Size.Width / 2f;
        _editorState.Execute(new CreateGuideCommand(page.Id, GuideOrientation.Vertical, position));
        UpdateGuideList();
        MainCanvas.Invalidate();
    }

    private void ClearGuidesButton_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null || page.Guides.Count == 0) return;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("props.guides.locked_unlock_clear"));
            return;
        }

        foreach (var guide in page.Guides.ToList())
        {
            _editorState.Execute(new DeleteGuideCommand(page.Id, guide.Id));
        }
        UpdateGuideList();
        MainCanvas.Invalidate();
    }

    private void UpdateGuideList()
    {
        GuideListPanel.Children.Clear();
        _guidePositionTextBoxes.Clear();

        var page = _editorState.Document?.ActivePage;
        if (page == null)
        {
            GuideListEmptyText.Visibility = Visibility.Visible;
            ClearGuidesButton.IsEnabled = false;
            if (AddHorizontalGuideButton != null) AddHorizontalGuideButton.IsEnabled = false;
            if (AddVerticalGuideButton != null) AddVerticalGuideButton.IsEnabled = false;
            return;
        }

        var hasGuides = page.Guides.Count > 0;
        GuideListEmptyText.Visibility = hasGuides ? Visibility.Collapsed : Visibility.Visible;
        ClearGuidesButton.IsEnabled = hasGuides && !page.GuidesLocked;
        UpdateGuideLockUi(page);

        if (!hasGuides) return;

        foreach (var guide in page.Guides)
        {
            var row = new Grid { ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var orientationColor = guide.Orientation == GuideOrientation.Horizontal
                ? Microsoft.UI.ColorHelper.FromArgb(255, 76, 128, 196)
                : Microsoft.UI.ColorHelper.FromArgb(255, 180, 122, 62);
            var orientationText = guide.Orientation == GuideOrientation.Horizontal
                ? L("guide.horizontal")
                : L("guide.vertical");

            var typeBadge = new Border
            {
                Background = new SolidColorBrush(orientationColor),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = orientationText,
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
                }
            };
            if (page.GuidesLocked)
            {
                var lockedPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6
                };
                lockedPanel.Children.Add(typeBadge);
                lockedPanel.Children.Add(new FontIcon
                {
                    Glyph = "\uE72E",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 190, 100)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(lockedPanel);
            }
            else
            {
                row.Children.Add(typeBadge);
            }

            var positionBox = new TextBox
            {
                Text = FormatGuidePosition(guide.Position),
                HorizontalContentAlignment = HorizontalAlignment.Right,
                IsEnabled = !page.GuidesLocked,
                Tag = guide.Id,
                MinWidth = 92,
                Padding = new Thickness(8, 4, 8, 4)
            };
            positionBox.LostFocus += GuidePositionTextBox_LostFocus;
            positionBox.KeyDown += GuidePositionTextBox_KeyDown;
            _guidePositionTextBoxes[guide.Id] = positionBox;
            Grid.SetColumn(positionBox, 1);
            row.Children.Add(positionBox);

            var deleteBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 },
                Padding = new Thickness(6, 4, 6, 4),
                Tag = guide.Id,
                IsEnabled = !page.GuidesLocked,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            deleteBtn.Click += GuideDeleteButton_Click;
            Grid.SetColumn(deleteBtn, 2);
            row.Children.Add(deleteBtn);

            GuideListPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 39, 39, 39)),
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 56, 56, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Child = row
            });
        }
    }

    private static string FormatGuidePosition(float position)
    {
        return position.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void GuidePositionTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitGuidePositionTextBox(textBox);
        }
    }

    private void GuidePositionTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        if (sender is not TextBox textBox) return;

        CommitGuidePositionTextBox(textBox);
        MainCanvas.Focus(FocusState.Programmatic);
        e.Handled = true;
    }

    private void CommitGuidePositionTextBox(TextBox textBox)
    {
        if (_isUpdatingGuidePositionInputs) return;
        if (textBox.Tag is not Guid guideId) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("props.guides.locked_unlock_edit"));
            RefreshGuideValueEditors(page);
            return;
        }

        var guide = page.FindGuide(guideId);
        if (guide == null) return;

        if (!TryParseGuidePosition(textBox.Text, out var parsed))
        {
            RefreshGuideValueEditors(page);
            return;
        }

        var max = guide.Orientation == GuideOrientation.Horizontal ? page.Size.Height : page.Size.Width;
        var clamped = Math.Clamp(parsed, 0f, max);
        if (Math.Abs(clamped - guide.Position) <= 0.01f)
        {
            RefreshGuideValueEditors(page);
            return;
        }

        _editorState.Execute(new MoveGuideCommand(page.Id, guideId, clamped));
        RefreshGuideValueEditors(page);
        MainCanvas.Invalidate();
    }

    private static bool TryParseGuidePosition(string? text, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        return float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ||
               float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out value);
    }

    private void RefreshGuideValueEditors(DocumentPage page)
    {
        _isUpdatingGuidePositionInputs = true;
        try
        {
            foreach (var guide in page.Guides)
            {
                if (!_guidePositionTextBoxes.TryGetValue(guide.Id, out var textBox))
                {
                    continue;
                }

                if (textBox.FocusState != FocusState.Unfocused)
                {
                    continue;
                }

                textBox.Text = FormatGuidePosition(guide.Position);
            }
        }
        finally
        {
            _isUpdatingGuidePositionInputs = false;
        }
    }

    private void GuideDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid guideId) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (page.GuidesLocked)
        {
            SetStatusMessage(L("props.guides.locked_unlock_delete"));
            return;
        }

        _editorState.Execute(new DeleteGuideCommand(page.Id, guideId));
        UpdateGuideList();
        MainCanvas.Invalidate();
    }


    private void LinkRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not BalloonLink link) return;

        _editorState.Execute(new UnlinkBalloonsCommand(link.FirstId, link.SecondId));
    }

    private void UpdateLinkPanel(DocumentPage page)
    {
        if (LinkPropertiesPanel != null) LinkPropertiesPanel.Visibility = Visibility.Visible;

        var style = page.BalloonLinkStyle;
        LinkWidthSlider.Value = style.StrokeWidth;
        LinkConnectorWidthSlider.Value = style.ConnectorWidth;

        var color = style.StrokeColor;
        LinkColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));

        LinkColorComboBox.SelectedIndex = -1;
        var colorTag = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        for (int i = 0; i < LinkColorComboBox.Items.Count; i++)
        {
            if (LinkColorComboBox.Items[i] is ComboBoxItem colorItem &&
                string.Equals(colorItem.Tag?.ToString(), colorTag, StringComparison.OrdinalIgnoreCase))
            {
                LinkColorComboBox.SelectedIndex = i;
                break;
            }
        }

        LinkDashComboBox.SelectedIndex = -1;
        var dashTag = style.DashStyle.ToString();
        for (int i = 0; i < LinkDashComboBox.Items.Count; i++)
        {
            if (LinkDashComboBox.Items[i] is ComboBoxItem dashItem &&
                dashItem.Tag?.ToString() == dashTag)
            {
                LinkDashComboBox.SelectedIndex = i;
                break;
            }
        }

        UpdateLinkList(page);
    }

    private void UpdateLinkList(DocumentPage page)
    {
        LinkListPanel.Children.Clear();

        var hasLinks = page.BalloonLinks.Count > 0;
        LinkListEmptyText.Visibility = hasLinks ? Visibility.Collapsed : Visibility.Visible;
        ClearLinksButton.IsEnabled = hasLinks;

        if (!hasLinks) return;

        foreach (var link in page.BalloonLinks)
        {
            var balloonA = page.FindBalloon(link.FirstId);
            var balloonB = page.FindBalloon(link.SecondId);
            var labelA = GetBalloonLinkLabel(balloonA, link.FirstId);
            var labelB = GetBalloonLinkLabel(balloonB, link.SecondId);

            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBlock = new TextBlock
            {
                Text = $"{labelA} <-> {labelB}",
                FontSize = 11,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray)
            };
            Grid.SetColumn(textBlock, 0);

            var removeButton = new Button
            {
                Content = L("props.link.unlink"),
                Padding = new Thickness(8, 2, 8, 2),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 58, 58, 58)),
                Tag = link
            };
            removeButton.Click += LinkRemoveButton_Click;
            Grid.SetColumn(removeButton, 1);

            row.Children.Add(textBlock);
            row.Children.Add(removeButton);
            LinkListPanel.Children.Add(row);
        }
    }

    private static string GetBalloonLinkLabel(Balloon? balloon, Guid id)
    {
        if (balloon == null)
        {
            return id.ToString("N")[..8];
        }

        var text = balloon.Text ?? "";
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (text.Length == 0)
        {
            return "Balloon";
        }

        const int maxLength = 28;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength).Trim() + "...";
        }

        return text;
    }


    private static Model.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            return new Model.Color(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }
        return Model.Color.White;
    }

    private async System.Threading.Tasks.Task<Model.Color?> ShowColorPickerDialogAsync(Model.Color currentColor)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = L("props.color.choose_custom"),
            CloseButtonText = L("common.cancel"),
            PrimaryButtonText = L("common.ok"),
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var colorPicker = new Microsoft.UI.Xaml.Controls.ColorPicker
        {
            Color = currentColor.ToWindowsColor(),
            IsColorSliderVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            IsAlphaEnabled = false,
            IsAlphaSliderVisible = false,
            IsAlphaTextInputVisible = false
        };

        dialog.Content = colorPicker;

        var result = await dialog.ShowAsync();
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            var winColor = colorPicker.Color;
            return new Model.Color(winColor.R, winColor.G, winColor.B, 255);
        }

        return null;
    }

    private static BalloonLinkStyle BuildLinkStyleFromData(CommandData data)
    {
        var defaults = BalloonLinkStyle.Default;
        var r = data.Parameters.ContainsKey("strokeR") ? (byte)data.Get<int>("strokeR") : defaults.StrokeColor.R;
        var g = data.Parameters.ContainsKey("strokeG") ? (byte)data.Get<int>("strokeG") : defaults.StrokeColor.G;
        var b = data.Parameters.ContainsKey("strokeB") ? (byte)data.Get<int>("strokeB") : defaults.StrokeColor.B;
        var a = data.Parameters.ContainsKey("strokeA") ? (byte)data.Get<int>("strokeA") : defaults.StrokeColor.A;

        var fillR = data.Parameters.ContainsKey("fillR") ? (byte)data.Get<int>("fillR") : defaults.FillColor.R;
        var fillG = data.Parameters.ContainsKey("fillG") ? (byte)data.Get<int>("fillG") : defaults.FillColor.G;
        var fillB = data.Parameters.ContainsKey("fillB") ? (byte)data.Get<int>("fillB") : defaults.FillColor.B;
        var fillA = data.Parameters.ContainsKey("fillA") ? (byte)data.Get<int>("fillA") : defaults.FillColor.A;

        var strokeWidth = data.Parameters.ContainsKey("strokeWidth") ? data.Get<float>("strokeWidth") : defaults.StrokeWidth;
        var connectorWidth = data.Parameters.ContainsKey("connectorWidth") ? data.Get<float>("connectorWidth") : defaults.ConnectorWidth;

        var dashStyle = defaults.DashStyle;
        if (data.Parameters.ContainsKey("dashStyle"))
        {
            var dashValue = data.Get<string>("dashStyle");
            if (Enum.TryParse<LinkDashStyle>(dashValue, out var parsed))
            {
                dashStyle = parsed;
            }
        }

        return new BalloonLinkStyle
        {
            StrokeColor = new Model.Color(r, g, b, a),
            FillColor = new Model.Color(fillR, fillG, fillB, fillA),
            StrokeWidth = strokeWidth,
            ConnectorWidth = connectorWidth,
            DashStyle = dashStyle
        };
    }

    private static OffPanelIndicatorStyle BuildOffPanelIndicatorStyleFromData(CommandData data)
    {
        var defaults = OffPanelIndicatorStyle.Default;
        var r = data.Parameters.ContainsKey("colorR") ? (byte)data.Get<int>("colorR") : defaults.Color.R;
        var g = data.Parameters.ContainsKey("colorG") ? (byte)data.Get<int>("colorG") : defaults.Color.G;
        var b = data.Parameters.ContainsKey("colorB") ? (byte)data.Get<int>("colorB") : defaults.Color.B;
        var a = data.Parameters.ContainsKey("colorA") ? (byte)data.Get<int>("colorA") : defaults.Color.A;
        var size = data.Parameters.ContainsKey("size") ? data.Get<float>("size") : defaults.Size;

        return new OffPanelIndicatorStyle(new Model.Color(r, g, b, a), size);
    }



    private bool _isUpdatingPanelProperties;
    private static readonly JsonSerializerOptions PanelTemplateJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private void UpdatePanelZonePropertiesPanel()
    {
        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;

        if (page == null || !panelId.HasValue)
        {
            PanelZonePropertiesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var panel = page.FindPanel(panelId.Value);
        if (panel == null)
        {
            PanelZonePropertiesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        PanelZonePropertiesPanel.Visibility = Visibility.Visible;

        _isUpdatingPanelProperties = true;
        try
        {
            PanelNameTextBox.Text = panel.Name;

            for (int i = 0; i < PanelShapeComboBox.Items.Count; i++)
            {
                if (PanelShapeComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == panel.Shape.ToString())
                {
                    PanelShapeComboBox.SelectedIndex = i;
                    break;
                }
            }

            PanelCornerRadiusPanel.Visibility = panel.Shape == PanelShape.RoundedRect
                ? Visibility.Visible
                : Visibility.Collapsed;
            PanelCornerRadiusSlider.Value = panel.CornerRadius;

            var borderColor = panel.BorderColor;
            PanelBorderColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(borderColor.A, borderColor.R, borderColor.G, borderColor.B));
            PanelBorderColorComboBox.SelectedIndex = -1;
            var borderTag = $"#{borderColor.R:X2}{borderColor.G:X2}{borderColor.B:X2}";
            for (int i = 0; i < PanelBorderColorComboBox.Items.Count; i++)
            {
                if (PanelBorderColorComboBox.Items[i] is ComboBoxItem colorItem &&
                    string.Equals(colorItem.Tag?.ToString(), borderTag, StringComparison.OrdinalIgnoreCase))
                {
                    PanelBorderColorComboBox.SelectedIndex = i;
                    break;
                }
            }
            PanelBorderWidthSlider.Value = panel.BorderWidth;
            PanelBorderStyleComboBox.SelectedIndex = -1;
            var styleTag = panel.BorderStyle.ToString();
            for (int i = 0; i < PanelBorderStyleComboBox.Items.Count; i++)
            {
                if (PanelBorderStyleComboBox.Items[i] is ComboBoxItem styleItem &&
                    styleItem.Tag?.ToString() == styleTag)
                {
                    PanelBorderStyleComboBox.SelectedIndex = i;
                    break;
                }
            }

            PanelXBox.Value = panel.Bounds.X;
            PanelYBox.Value = panel.Bounds.Y;
            PanelWidthBox.Value = panel.Bounds.Width;
            PanelHeightBox.Value = panel.Bounds.Height;
            PanelOrderBox.Value = panel.Order;
            PanelSafeMarginBox.Value = panel.SafeMargin;
            PanelAspectRatioLockToggle.IsOn = _panelAspectLocked.Contains(panel.Id);
            PanelSizePresetComboBox.SelectedIndex = 0;
            var gutterFallback = page.PanelGutterWidth;
            var hasGutterOverrides = panel.GutterLeftOverride.HasValue
                || panel.GutterTopOverride.HasValue
                || panel.GutterRightOverride.HasValue
                || panel.GutterBottomOverride.HasValue;
            PanelCustomGutterToggle.IsOn = hasGutterOverrides;
            SetPanelGutterOverrideInputsEnabled(hasGutterOverrides);
            PanelGutterLeftBox.Value = panel.GutterLeftOverride ?? gutterFallback;
            PanelGutterTopBox.Value = panel.GutterTopOverride ?? gutterFallback;
            PanelGutterRightBox.Value = panel.GutterRightOverride ?? gutterFallback;
            PanelGutterBottomBox.Value = panel.GutterBottomOverride ?? gutterFallback;
            PanelBleedLeftBox.Value = panel.BleedLeft;
            PanelBleedTopBox.Value = panel.BleedTop;
            PanelBleedRightBox.Value = panel.BleedRight;
            PanelBleedBottomBox.Value = panel.BleedBottom;
        }
        finally
        {
            _isUpdatingPanelProperties = false;
        }
    }

    private void PanelNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitPanelName();
    }

    private void PanelNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitPanelName();
            e.Handled = true;
        }
    }

    private void CommitPanelName()
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var newName = PanelNameTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(newName) || newName == panel.Name) return;

        _editorState.Execute(new SetPanelZoneNameCommand(page.Id, panelId.Value, newName));
    }

    private void PanelShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        if (PanelShapeComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string shapeStr &&
            Enum.TryParse<PanelShape>(shapeStr, out var shape))
        {
            _editorState.Execute(new SetPanelZoneShapeCommand(page.Id, panelId.Value, shape));
            UpdatePanelZonePropertiesPanel();
        }
    }

    private void PanelCornerRadiusSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        _editorState.Execute(new SetPanelZoneShapeCommand(page.Id, panelId.Value, panel.Shape, (float)e.NewValue));
    }

    private void PanelBorderColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        if (PanelBorderColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorHex)
        {
            var baseColor = ParseHexColor(colorHex);
            var color = new Model.Color(baseColor.R, baseColor.G, baseColor.B, 220);
            _editorState.Execute(new SetPanelBorderColorCommand(page.Id, panelId.Value, color));

            PanelBorderColorPreview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(color.A, color.R, color.G, color.B));
        }
    }

    private void PanelBorderWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_editorState == null || _isUpdatingPanelProperties) return; // Guard during XAML initialization

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue || double.IsNaN(e.NewValue)) return;

        _editorState.Execute(new SetPanelBorderWidthCommand(page.Id, panelId.Value, (float)e.NewValue));
    }

    private void PanelBorderStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        if (PanelBorderStyleComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string styleTag &&
            Enum.TryParse<PanelBorderStyle>(styleTag, out var style))
        {
            _editorState.Execute(new SetPanelBorderStyleCommand(page.Id, panelId.Value, style));
        }
    }

    private void PanelPositionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var newX = (float)(PanelXBox.Value);
        var newY = (float)(PanelYBox.Value);
        var newBounds = new Rect(newX, newY, panel.Bounds.Width, panel.Bounds.Height);

        if (newBounds != panel.Bounds)
        {
            _editorState.Execute(new SetPanelZoneBoundsCommand(page.Id, panelId.Value, newBounds));
        }
    }

    private void PanelSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var newWidth = Math.Max(10, (float)PanelWidthBox.Value);
        var newHeight = Math.Max(10, (float)PanelHeightBox.Value);

        if (TryGetPanelAspectRatio(panel, out var ratio))
        {
            if (sender == PanelWidthBox)
            {
                newHeight = Math.Max(10f, newWidth / ratio);
                _isUpdatingPanelProperties = true;
                PanelHeightBox.Value = newHeight;
                _isUpdatingPanelProperties = false;
            }
            else if (sender == PanelHeightBox)
            {
                newWidth = Math.Max(10f, newHeight * ratio);
                _isUpdatingPanelProperties = true;
                PanelWidthBox.Value = newWidth;
                _isUpdatingPanelProperties = false;
            }
        }

        var newBounds = new Rect(panel.Bounds.X, panel.Bounds.Y, newWidth, newHeight);

        if (newBounds != panel.Bounds)
        {
            _editorState.Execute(new SetPanelZoneBoundsCommand(page.Id, panelId.Value, newBounds));
        }
    }

    private void PanelAspectRatioLockToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        if (PanelAspectRatioLockToggle.IsOn)
        {
            var ratio = panel.Bounds.Width / Math.Max(1f, panel.Bounds.Height);
            _panelAspectRatios[panel.Id] = ratio;
            _panelAspectLocked.Add(panel.Id);
        }
        else
        {
            _panelAspectLocked.Remove(panel.Id);
        }
    }

    private async void PanelSizePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        if (PanelSizePresetComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string tag) return;

        float? ratio = tag switch
        {
            "1:1" => 1f,
            "16:9" => 16f / 9f,
            "4:3" => 4f / 3f,
            "3:4" => 3f / 4f,
            "1.618:1" => 1.618f,
            "custom-ratio" => await PromptForCustomPanelRatioAsync(),
            _ => null
        };

        if (!ratio.HasValue)
        {
            return;
        }

        ApplyPanelAspectRatio(page, panel, ratio.Value);
    }

    private async Task<float?> PromptForCustomPanelRatioAsync()
    {
        var input = new TextBox
        {
            PlaceholderText = L("props.ratio.placeholder"),
            Text = _panelCustomAspectRatio?.ToString("0.###") ?? ""
        };

        var dialog = new ContentDialog
        {
            Title = L("props.ratio.title"),
            Content = input,
            PrimaryButtonText = L("common.apply"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var text = input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;

        float ratio;
        if (text.Contains(':'))
        {
            var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !float.TryParse(parts[0], out var w) ||
                !float.TryParse(parts[1], out var h) ||
                w <= 0f || h <= 0f)
            {
                SetStatusMessage(L("props.ratio.invalid_format"));
                return null;
            }
            ratio = w / h;
        }
        else if (!float.TryParse(text, out ratio) || ratio <= 0f)
        {
            SetStatusMessage(L("props.ratio.invalid_value"));
            return null;
        }

        _panelCustomAspectRatio = ratio;
        return ratio;
    }

    private void ApplyPanelAspectRatio(DocumentPage page, PanelZone panel, float ratio)
    {
        var width = panel.Bounds.Width;
        var height = width / ratio;

        var maxWidth = page.Size.Width - panel.Bounds.X;
        var maxHeight = page.Size.Height - panel.Bounds.Y;

        if (width > maxWidth || height > maxHeight)
        {
            var widthScale = maxWidth > 0f ? maxWidth / width : 1f;
            var heightScale = maxHeight > 0f ? maxHeight / height : 1f;
            var scale = MathF.Min(widthScale, heightScale);
            width *= scale;
            height *= scale;
        }

        width = MathF.Max(10f, width);
        height = MathF.Max(10f, height);

        var newBounds = new Rect(panel.Bounds.X, panel.Bounds.Y, width, height);
        _editorState.Execute(new SetPanelZoneBoundsCommand(page.Id, panel.Id, newBounds));

        if (_panelAspectLocked.Contains(panel.Id))
        {
            _panelAspectRatios[panel.Id] = ratio;
        }

        _isUpdatingPanelProperties = true;
        PanelWidthBox.Value = newBounds.Width;
        PanelHeightBox.Value = newBounds.Height;
        _isUpdatingPanelProperties = false;
    }

    private bool TryGetPanelAspectRatio(PanelZone panel, out float ratio)
    {
        if (_panelAspectLocked.Contains(panel.Id) &&
            _panelAspectRatios.TryGetValue(panel.Id, out ratio) &&
            ratio > 0.01f)
        {
            return true;
        }

        ratio = 0f;
        return false;
    }

    private void PanelSafeMarginBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var margin = Math.Max(0f, (float)args.NewValue);
        if (Math.Abs(panel.SafeMargin - margin) < 0.01f) return;

        _editorState.Execute(new SetPanelSafeMarginCommand(page.Id, panelId.Value, margin));
        MainCanvas.Invalidate();
    }

    private void PanelCustomGutterToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        SetPanelGutterOverrideInputsEnabled(PanelCustomGutterToggle.IsOn);

        if (!PanelCustomGutterToggle.IsOn)
        {
            if (panel.GutterLeftOverride.HasValue || panel.GutterTopOverride.HasValue ||
                panel.GutterRightOverride.HasValue || panel.GutterBottomOverride.HasValue)
            {
                _editorState.Execute(new SetPanelGutterOverridesCommand(page.Id, panelId.Value, null, null, null, null));
                MainCanvas.Invalidate();
            }
            return;
        }

        ApplyPanelGutterOverrides(page, panelId.Value);
    }

    private void PanelGutterOverrideBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;
        if (!PanelCustomGutterToggle.IsOn) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        ApplyPanelGutterOverrides(page, panelId.Value);
    }

    private void ApplyPanelGutterOverrides(DocumentPage page, Guid panelId)
    {
        SetPanelGutterOverrideInputsEnabled(true);

        float GetValue(NumberBox box)
        {
            var value = (float)box.Value;
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return MathF.Max(0f, page.PanelGutterWidth);
            }
            return MathF.Max(0f, value);
        }

        var left = GetValue(PanelGutterLeftBox);
        var top = GetValue(PanelGutterTopBox);
        var right = GetValue(PanelGutterRightBox);
        var bottom = GetValue(PanelGutterBottomBox);

        _editorState.Execute(new SetPanelGutterOverridesCommand(page.Id, panelId, left, top, right, bottom));
        MainCanvas.Invalidate();
    }

    private void SetPanelGutterOverrideInputsEnabled(bool enabled)
    {
        PanelGutterLeftBox.IsEnabled = enabled;
        PanelGutterTopBox.IsEnabled = enabled;
        PanelGutterRightBox.IsEnabled = enabled;
        PanelGutterBottomBox.IsEnabled = enabled;
        PanelGutterOverrideGrid.Opacity = enabled ? 1f : 0.6f;
    }

    private void PanelBleedBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var left = Math.Max(0f, (float)PanelBleedLeftBox.Value);
        var top = Math.Max(0f, (float)PanelBleedTopBox.Value);
        var right = Math.Max(0f, (float)PanelBleedRightBox.Value);
        var bottom = Math.Max(0f, (float)PanelBleedBottomBox.Value);

        if (Math.Abs(panel.BleedLeft - left) < 0.01f &&
            Math.Abs(panel.BleedTop - top) < 0.01f &&
            Math.Abs(panel.BleedRight - right) < 0.01f &&
            Math.Abs(panel.BleedBottom - bottom) < 0.01f)
        {
            return;
        }

        _editorState.Execute(new SetPanelBleedCommand(page.Id, panelId.Value, left, top, right, bottom));
        MainCanvas.Invalidate();
    }

    private void PanelOrderBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingPanelProperties || double.IsNaN(args.NewValue)) return;

        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;

        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var newOrder = Math.Max(1, (int)args.NewValue);
        if (newOrder != panel.Order)
        {
            panel.SetOrder(newOrder);
            MainCanvas.Invalidate();
        }
    }

    private async void LoadPanelImage_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        var panelId = _editorState.SelectedPanelId;
        if (page == null || !panelId.HasValue) return;
        var panel = page.FindPanel(panelId.Value);
        if (panel == null) return;

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        AddSupportedImageFileTypes(picker, includeSvg: true);
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            await CreateFloatingImageFromFileInPanelAsync(file, page, panel);
            SetStatusMessage(L("image.status.loaded_into_panel"));
            UpdatePanelZonePropertiesPanel();
        }
        catch (Exception ex)
        {
            PanelImageStatusText.Text = LF("common.error_format", ex.Message);
            Diagnostics.StartupLogger.Log($"Panel image load error: {ex}");
        }
    }

    private void ClearPanelImage_Click(object sender, RoutedEventArgs e)
    {
        SetStatusMessage(L("props.panel_image.no_image"));
    }

    private void UpdatePanelImageStatus(PanelZone panel)
    {
        PanelImageStatusText.Text = L("props.panel_image.no_image");
        ClearPanelImageButton.IsEnabled = false;
        PanelImageOptionsPanel.Visibility = Visibility.Collapsed;
    }

    private void PanelImageFitMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void PanelImageOpacity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
    }

    private void PanelImageLock_Toggled(object sender, RoutedEventArgs e)
    {
    }

    private void PanelImageExport_Toggled(object sender, RoutedEventArgs e)
    {
    }

    private void ResetPanelImage_Click(object sender, RoutedEventArgs e)
    {
    }

    private void PopulatePanelTemplates()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        PanelTemplateComboBox.Items.Clear();
        foreach (var template in doc.PanelTemplates)
        {
            PanelTemplateComboBox.Items.Add(new ComboBoxItem
            {
                Content = template.Name,
                Tag = template.Id
            });
        }
    }

    private async void PanelTemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingPanelProperties) return;

        if (PanelTemplateComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not Guid templateId) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null) return;

        await ExecutePanelTemplateAsync(page, templateId, merge: false);

        _isUpdatingPanelProperties = true;
        PanelTemplateComboBox.SelectedIndex = -1;
        _isUpdatingPanelProperties = false;
    }

    private async Task RefreshPanelTemplateLibraryAsync()
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            if (PanelTemplateLibraryListView != null)
            {
                PanelTemplateLibraryListView.ItemsSource = null;
            }
            PanelTemplateCategoryFilterComboBox?.Items.Clear();
            if (PanelTemplateLibraryEmptyText != null)
            {
                PanelTemplateLibraryEmptyText.Visibility = Visibility.Visible;
            }
            UpdatePanelTemplateLibraryActions();
            return;
        }

        _panelTemplateLibraryItems = await BuildPanelTemplateViewModelsAsync(doc);
        UpdatePanelTemplateCategoryFilter();
        ApplyPanelTemplateLibraryFilter();
    }

    private void PanelTemplateSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyPanelTemplateLibraryFilter();
    }

    private void PanelTemplateCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyPanelTemplateLibraryFilter();
    }

    private void PanelTemplateLibraryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelTemplateLibraryListView.SelectedItem is PanelTemplateViewModel vm)
        {
            _lastPanelTemplateId = vm.Id;
        }
        UpdatePanelTemplateLibraryActions();
    }

    private async void PanelTemplateApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (PanelTemplateLibraryListView.SelectedItem is not PanelTemplateViewModel vm) return;

        await ExecutePanelTemplateAsync(page, vm.Id, merge: false);
    }

    private async void PanelTemplateMergeButton_Click(object sender, RoutedEventArgs e)
    {
        var page = _editorState.Document?.ActivePage;
        if (page == null) return;
        if (PanelTemplateLibraryListView.SelectedItem is not PanelTemplateViewModel vm) return;

        await ExecutePanelTemplateAsync(page, vm.Id, merge: true);
    }

    private enum PanelTemplateSizeDecision
    {
        Cancel,
        KeepCurrentPageSize,
        ResizePageToTemplate
    }

    private async Task<bool> ExecutePanelTemplateAsync(DocumentPage page, Guid templateId, bool merge)
    {
        var doc = _editorState.Document;
        if (doc == null) return false;

        var template = doc.PanelTemplates.FirstOrDefault(item => item.Id == templateId);
        if (template == null)
        {
            SetStatusMessage(L("props.template.not_found"));
            return false;
        }

        var decision = await ResolvePanelTemplateSizeDecisionAsync(template, page, merge);
        if (decision == PanelTemplateSizeDecision.Cancel)
        {
            return false;
        }

        var commands = new List<ICommand>();
        if (decision == PanelTemplateSizeDecision.ResizePageToTemplate)
        {
            commands.Add(new SetPageSizeCommand(page.Id, template.Size));
        }

        commands.Add(merge
            ? new MergePanelLayoutTemplateCommand(templateId, page.Id)
            : new ApplyPanelLayoutTemplateCommand(templateId, page.Id));

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction(
                merge ? "Merge panel template" : "Apply panel template",
                commands);
        }

        _lastPanelTemplateId = templateId;
        RefreshPanelList();
        MainCanvas.Invalidate();

        if (decision == PanelTemplateSizeDecision.ResizePageToTemplate)
        {
            SetStatusMessage(merge
                ? LF("panel_template.status.merged_resized", template.Name, FormatTemplateSize(template.Size))
                : LF("panel_template.status.applied_resized", template.Name, FormatTemplateSize(template.Size)));
        }
        else
        {
            SetStatusMessage(merge
                ? LF("panel_template.status.merged", template.Name)
                : LF("panel_template.status.applied", template.Name));
        }

        return true;
    }

    private async Task<PanelTemplateSizeDecision> ResolvePanelTemplateSizeDecisionAsync(
        PanelLayoutTemplate template,
        DocumentPage page,
        bool merge)
    {
        if (AreTemplateSizesEquivalent(template.Size, page.Size))
        {
            return PanelTemplateSizeDecision.KeepCurrentPageSize;
        }

        var action = merge ? "merge" : "apply";
        var templateSizeText = FormatTemplateSize(template.Size);
        var currentSizeText = FormatTemplateSize(page.Size);

        var dialog = new ContentDialog
        {
            Title = L("props.template.panel_templates"),
            Content = $"The template \"{template.Name}\" was created at {templateSizeText}, but the current page is {currentSizeText}. Resize the page to {templateSizeText} before {action}?",
            PrimaryButtonText = L("common.yes"),
            SecondaryButtonText = L("common.no"),
            CloseButtonText = L("common.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => PanelTemplateSizeDecision.ResizePageToTemplate,
            ContentDialogResult.Secondary => PanelTemplateSizeDecision.KeepCurrentPageSize,
            _ => PanelTemplateSizeDecision.Cancel
        };
    }

    private static bool AreTemplateSizesEquivalent(Size2 templateSize, Size2 pageSize, float tolerance = 0.5f)
    {
        return MathF.Abs(templateSize.Width - pageSize.Width) <= tolerance &&
               MathF.Abs(templateSize.Height - pageSize.Height) <= tolerance;
    }

    private static string FormatTemplateSize(Size2 size)
    {
        return $"{size.Width:F0} x {size.Height:F0}";
    }

    private async void PanelTemplateRenameButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (PanelTemplateLibraryListView.SelectedItem is not PanelTemplateViewModel vm) return;

        var template = doc.PanelTemplates.FirstOrDefault(t => t.Id == vm.Id);
        if (template == null) return;

        var metadata = await ShowPanelTemplateMetadataDialogAsync(template);
        if (metadata == null) return;

        var newName = GetUniquePanelTemplateName(metadata.Name, doc.PanelTemplates, vm.Id);
        if (newName != metadata.Name)
        {
            metadata = metadata with { Name = newName };
        }

        if (newName == template.Name &&
            string.Equals(metadata.Description ?? "", template.Description ?? "", StringComparison.Ordinal) &&
            string.Equals(metadata.Category ?? "", template.Category ?? "", StringComparison.Ordinal) &&
            metadata.Tags.SequenceEqual(template.Tags))
        {
            return;
        }

        _editorState.Execute(new UpdatePanelLayoutTemplateMetadataCommand(vm.Id, metadata.Name, metadata.Description, metadata.Tags, metadata.Category));
        PopulatePanelTemplates();
        await RefreshPanelTemplateLibraryAsync();
    }

    private async void PanelTemplateDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (PanelTemplateLibraryListView.SelectedItem is not PanelTemplateViewModel vm) return;

        var dialog = new ContentDialog
        {
            Title = L("props.template.delete_title"),
            Content = LF("props.template.delete_confirm", vm.Name),
            PrimaryButtonText = L("common.delete"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var template = doc.PanelTemplates.FirstOrDefault(t => t.Id == vm.Id);
        if (template == null) return;

        _editorState.Execute(new DeletePanelLayoutTemplateCommand(vm.Id));
        PopulatePanelTemplates();
        await RefreshPanelTemplateLibraryAsync();
    }

    private sealed record PanelTemplateMetadata(string Name, string? Description, List<string> Tags, string? Category);

    private async Task<PanelTemplateMetadata?> ShowPanelTemplateMetadataDialogAsync(PanelLayoutTemplate template)
    {
        var nameBox = new TextBox
        {
            Text = template.Name,
            MinWidth = 240
        };
        var descriptionBox = new TextBox
        {
            Text = template.Description ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80
        };
        var tagsBox = new TextBox
        {
            Text = template.Tags.Count > 0 ? string.Join(", ", template.Tags) : ""
        };
        var categoryBox = new TextBox
        {
            Text = template.Category ?? ""
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = L("props.template.label_name"), FontSize = 11, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        stack.Children.Add(nameBox);
        stack.Children.Add(new TextBlock { Text = L("props.template.label_description"), FontSize = 11, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        stack.Children.Add(descriptionBox);
        stack.Children.Add(new TextBlock { Text = L("props.template.label_tags"), FontSize = 11, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        stack.Children.Add(tagsBox);
        stack.Children.Add(new TextBlock { Text = L("props.template.label_category"), FontSize = 11, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) });
        stack.Children.Add(categoryBox);

        var dialog = new ContentDialog
        {
            Title = L("props.template.edit_metadata"),
            Content = stack,
            PrimaryButtonText = L("common.save"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        dialog.Opened += (_, _) =>
        {
            nameBox.Focus(FocusState.Programmatic);
            nameBox.SelectAll();
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;

        var name = nameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = L("props.template.default_name");
        }

        var description = NormalizeOptionalText(descriptionBox.Text);
        var category = NormalizeOptionalText(categoryBox.Text);
        var tags = ParseTemplateTags(tagsBox.Text);

        return new PanelTemplateMetadata(name, description, tags, category);
    }

    private async void PanelTemplateImportButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var imported = await ImportPanelTemplatesAsync(doc);
        if (imported > 0)
        {
            PopulatePanelTemplates();
            await RefreshPanelTemplateLibraryAsync();
            SetStatusMessage(LF("panel_template.status.imported", imported));
        }
    }

    private async void PanelTemplateExportButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (PanelTemplateLibraryListView.SelectedItem is not PanelTemplateViewModel vm) return;

        var template = doc.PanelTemplates.FirstOrDefault(t => t.Id == vm.Id);
        if (template == null) return;

        await ExportPanelTemplatesAsync(new[] { template });
    }

    private async void PanelTemplateExportAllButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null || doc.PanelTemplates.Count == 0) return;

        await ExportPanelTemplatesAsync(doc.PanelTemplates);
    }

    private async void PanelTemplateImportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var folder = await EnsurePanelTemplateStorageFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;

        var imported = await ImportPanelTemplatesFromFolderAsync(doc, folder);
        if (imported > 0)
        {
            PopulatePanelTemplates();
            await RefreshPanelTemplateLibraryAsync();
            SetStatusMessage(LF("panel_template.status.imported_folder", imported));
        }
    }

    private async void PanelTemplateExportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null || doc.PanelTemplates.Count == 0) return;

        var folder = await EnsurePanelTemplateStorageFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return;

        await ExportPanelTemplatesToFolderAsync(doc.PanelTemplates, folder);
    }

    private void ApplyPanelTemplateLibraryFilter()
    {
        if (PanelTemplateLibraryListView == null) return;

        var search = PanelTemplateSearchBox?.Text?.Trim() ?? "";
        var categoryFilter = GetSelectedPanelTemplateCategory();
        IEnumerable<PanelTemplateViewModel> filtered = _panelTemplateLibraryItems;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.Category) && item.Category.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                item.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            if (categoryFilter == "(Uncategorized)")
            {
                filtered = filtered.Where(item => string.IsNullOrWhiteSpace(item.Category));
            }
            else
            {
                filtered = filtered.Where(item => string.Equals(item.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }
        }

        var list = filtered.ToList();
        PanelTemplateLibraryListView.ItemsSource = list;

        if (PanelTemplateLibraryEmptyText != null)
        {
            PanelTemplateLibraryEmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdatePanelTemplateLibraryActions();
    }

    private void UpdatePanelTemplateCategoryFilter()
    {
        if (PanelTemplateCategoryFilterComboBox == null) return;

        var previous = GetSelectedPanelTemplateCategory(includeAllLabel: true);
        PanelTemplateCategoryFilterComboBox.Items.Clear();

        var allItem = new ComboBoxItem { Content = L("props.template.all_categories"), Tag = "__all__" };
        PanelTemplateCategoryFilterComboBox.Items.Add(allItem);

        var categories = _panelTemplateLibraryItems
            .Select(item => item.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasUncategorized = _panelTemplateLibraryItems.Any(item => string.IsNullOrWhiteSpace(item.Category));
        if (hasUncategorized)
        {
            PanelTemplateCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = L("props.template.uncategorized"), Tag = "(Uncategorized)" });
        }

        foreach (var category in categories)
        {
            PanelTemplateCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category, Tag = category });
        }

        ComboBoxItem? selection = null;
        if (!string.IsNullOrWhiteSpace(previous))
        {
            selection = PanelTemplateCategoryFilterComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, previous, StringComparison.OrdinalIgnoreCase));
        }

        PanelTemplateCategoryFilterComboBox.SelectedItem = selection ?? allItem;
    }

    private string? GetSelectedPanelTemplateCategory(bool includeAllLabel = false)
    {
        if (PanelTemplateCategoryFilterComboBox?.SelectedItem is not ComboBoxItem item) return includeAllLabel ? "__all__" : null;
        var tag = item.Tag as string;
        if (string.IsNullOrWhiteSpace(tag) || tag == "__all__") return includeAllLabel ? "__all__" : null;
        return tag;
    }

    private void UpdatePanelTemplateLibraryActions()
    {
        var hasSelection = PanelTemplateLibraryListView?.SelectedItem is PanelTemplateViewModel;
        if (PanelTemplateApplyButton != null) PanelTemplateApplyButton.IsEnabled = hasSelection;
        if (PanelTemplateMergeButton != null) PanelTemplateMergeButton.IsEnabled = hasSelection;
        if (PanelTemplateRenameButton != null) PanelTemplateRenameButton.IsEnabled = hasSelection;
        if (PanelTemplateDeleteButton != null) PanelTemplateDeleteButton.IsEnabled = hasSelection;
        if (PanelTemplateExportButton != null) PanelTemplateExportButton.IsEnabled = hasSelection;
    }

    private void SyncPanelTemplateStorageFolderFromPreferences()
    {
        var configured = _preferences.PanelDefaults.PanelTemplateStorageFolder;
        if (string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                var legacy = ApplicationData.Current.LocalSettings.Values[LegacyPanelTemplateStorageSettingsKey] as string;
                if (!string.IsNullOrWhiteSpace(legacy))
                {
                    configured = legacy.Trim();
                    _preferences.PanelDefaults.PanelTemplateStorageFolder = configured;
                    SavePreferences();
                }
            }
            catch
            {
            }
        }

        _panelTemplateStorageFolderPath = string.IsNullOrWhiteSpace(configured)
            ? null
            : configured.Trim();
    }

    private void SavePanelTemplateStorageFolder(string? path)
    {
        _panelTemplateStorageFolderPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        _preferences.PanelDefaults.PanelTemplateStorageFolder = _panelTemplateStorageFolderPath;
        SavePreferences();
    }

    private async Task<string?> PickPanelTemplateStorageFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async Task<string?> EnsurePanelTemplateStorageFolderAsync()
    {
        if (!string.IsNullOrWhiteSpace(_panelTemplateStorageFolderPath))
        {
            try
            {
                Directory.CreateDirectory(_panelTemplateStorageFolderPath);
                return _panelTemplateStorageFolderPath;
            }
            catch
            {
            }
        }

        var picked = await PickPanelTemplateStorageFolderAsync();
        if (string.IsNullOrWhiteSpace(picked)) return null;
        SavePanelTemplateStorageFolder(picked);
        return picked;
    }

    private async void SavePanelTemplate_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null || page.Panels.Count == 0)
        {
            SetStatusMessage(L("props.template.no_panels"));
            return;
        }

        var dialog = new ContentDialog
        {
            Title = L("props.template.save_title"),
            Content = new TextBox
            {
                PlaceholderText = L("props.template.name_placeholder"),
                Text = L("props.template.default_my_template")
            },
            PrimaryButtonText = L("common.save"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var textBox = dialog.Content as TextBox;
        var templateName = textBox?.Text?.Trim() ?? L("props.template.default_my_template");
        if (string.IsNullOrEmpty(templateName)) templateName = L("props.template.default_my_template");

        _editorState.Execute(new CreatePanelLayoutTemplateCommand(page.Id, templateName));

        PopulatePanelTemplates();
        _ = RefreshPanelTemplateLibraryAsync();
        SetStatusMessage(LF("panel_template.status.saved", templateName));
    }

    private async void MergePanelTemplate_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var page = doc?.ActivePage;
        if (page == null) return;

        var templateId = _lastPanelTemplateId;
        if (templateId == null)
        {
            SetStatusMessage(L("props.template.select_to_merge"));
            return;
        }

        var template = doc?.PanelTemplates.FirstOrDefault(t => t.Id == templateId.Value);
        if (template == null)
        {
            SetStatusMessage(L("props.template.not_found"));
            return;
        }

        await ExecutePanelTemplateAsync(page, templateId.Value, merge: true);
    }

    private async void ManagePanelTemplates_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var allTemplates = await BuildPanelTemplateViewModelsAsync(doc);
        var listView = new ListView
        {
            ItemTemplate = RootGrid.Resources["PanelTemplateItemTemplate"] as DataTemplate,
            ItemsSource = allTemplates,
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 360
        };

        if (allTemplates.Count > 0)
        {
            listView.SelectedIndex = 0;
        }

        var searchBox = new TextBox
        {
            PlaceholderText = L("props.template.search_placeholder")
        };

        var applyButton = new Button { Content = L("common.apply") };
        var mergeButton = new Button { Content = L("common.merge") };
        var renameButton = new Button { Content = L("common.edit") };
        var deleteButton = new Button { Content = L("common.delete") };
        var exportButton = new Button { Content = L("common.export") };
        var exportAllButton = new Button { Content = L("props.template.export_all") };
        var importButton = new Button { Content = L("props.template.import") };
        ToolTipService.SetToolTip(applyButton, L("common.apply"));
        ToolTipService.SetToolTip(mergeButton, L("common.merge"));
        ToolTipService.SetToolTip(renameButton, L("common.edit"));
        ToolTipService.SetToolTip(deleteButton, L("common.delete"));
        ToolTipService.SetToolTip(exportButton, L("common.export"));
        ToolTipService.SetToolTip(exportAllButton, L("props.template.export_all"));
        ToolTipService.SetToolTip(importButton, L("props.template.import"));

        void UpdateButtonStates()
        {
            var hasSelection = listView.SelectedItem is PanelTemplateViewModel;
            applyButton.IsEnabled = hasSelection;
            mergeButton.IsEnabled = hasSelection;
            renameButton.IsEnabled = hasSelection;
            deleteButton.IsEnabled = hasSelection;
            exportButton.IsEnabled = hasSelection;
        }

        void ApplyFilter()
        {
            var text = searchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                listView.ItemsSource = allTemplates;
                return;
            }

            var filtered = allTemplates
                .Where(template => template.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                .ToList();
            listView.ItemsSource = filtered;
        }

        async Task RefreshTemplateListAsync()
        {
            allTemplates = await BuildPanelTemplateViewModelsAsync(doc);
            ApplyFilter();
            UpdateButtonStates();
        }

        listView.SelectionChanged += (_, _) => UpdateButtonStates();
        searchBox.TextChanged += (_, _) => ApplyFilter();

        applyButton.Click += async (_, _) =>
        {
            if (listView.SelectedItem is not PanelTemplateViewModel vm) return;
            var page = doc.ActivePage;
            if (page == null) return;
            await ExecutePanelTemplateAsync(page, vm.Id, merge: false);
        };

        mergeButton.Click += async (_, _) =>
        {
            if (listView.SelectedItem is not PanelTemplateViewModel vm) return;
            var page = doc.ActivePage;
            if (page == null) return;
            await ExecutePanelTemplateAsync(page, vm.Id, merge: true);
        };

        renameButton.Click += async (_, _) =>
        {
            if (listView.SelectedItem is not PanelTemplateViewModel vm) return;
            var template = doc.PanelTemplates.FirstOrDefault(t => t.Id == vm.Id);
            if (template == null) return;

            var metadata = await ShowPanelTemplateMetadataDialogAsync(template);
            if (metadata == null) return;

            var newName = GetUniquePanelTemplateName(metadata.Name, doc.PanelTemplates, vm.Id);
            if (newName != metadata.Name)
            {
                metadata = metadata with { Name = newName };
            }

            if (newName == template.Name &&
                string.Equals(metadata.Description ?? "", template.Description ?? "", StringComparison.Ordinal) &&
                string.Equals(metadata.Category ?? "", template.Category ?? "", StringComparison.Ordinal) &&
                metadata.Tags.SequenceEqual(template.Tags))
            {
                return;
            }

            _editorState.Execute(new UpdatePanelLayoutTemplateMetadataCommand(vm.Id, metadata.Name, metadata.Description, metadata.Tags, metadata.Category));
            SetStatusMessage(LF("panel_template.status.metadata_updated", metadata.Name));
            await RefreshTemplateListAsync();
            PopulatePanelTemplates();
            await RefreshPanelTemplateLibraryAsync();
        };

        deleteButton.Click += async (_, _) =>
        {
            if (listView.SelectedItem is not PanelTemplateViewModel vm) return;

            var dialog = new ContentDialog
            {
                Title = L("props.template.delete_panel_title"),
                Content = new TextBlock { Text = LF("props.template.delete_panel_confirm", vm.Name) },
                PrimaryButtonText = L("common.delete"),
                CloseButtonText = L("common.cancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            _editorState.Execute(new DeletePanelLayoutTemplateCommand(vm.Id));
            SetStatusMessage(LF("panel_template.status.deleted", vm.Name));
            await RefreshTemplateListAsync();
            PopulatePanelTemplates();
            await RefreshPanelTemplateLibraryAsync();
        };

        exportButton.Click += async (_, _) =>
        {
            if (listView.SelectedItem is not PanelTemplateViewModel vm) return;
            var template = doc.PanelTemplates.FirstOrDefault(t => t.Id == vm.Id);
            if (template == null) return;
            await ExportPanelTemplatesAsync(new[] { template });
        };

        exportAllButton.Click += async (_, _) =>
        {
            if (doc.PanelTemplates.Count == 0)
            {
                SetStatusMessage(L("props.template.no_templates_export"));
                return;
            }
            await ExportPanelTemplatesAsync(doc.PanelTemplates);
        };

        importButton.Click += async (_, _) =>
        {
            var imported = await ImportPanelTemplatesAsync(doc);
            if (imported > 0)
            {
                await RefreshTemplateListAsync();
                PopulatePanelTemplates();
                await RefreshPanelTemplateLibraryAsync();
            }
        };

        UpdateButtonStates();

        var contentGrid = new Grid
        {
            RowSpacing = 8
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var topRow = new Grid { ColumnSpacing = 8 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.Children.Add(searchBox);
        Grid.SetColumn(importButton, 1);
        topRow.Children.Add(importButton);

        contentGrid.Children.Add(topRow);
        Grid.SetRow(listView, 1);
        contentGrid.Children.Add(listView);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonRow.Children.Add(applyButton);
        buttonRow.Children.Add(mergeButton);
        buttonRow.Children.Add(renameButton);
        buttonRow.Children.Add(deleteButton);
        buttonRow.Children.Add(exportButton);
        buttonRow.Children.Add(exportAllButton);

        Grid.SetRow(buttonRow, 2);
        contentGrid.Children.Add(buttonRow);

        var dialog = new ContentDialog
        {
            Title = L("props.template.panel_templates"),
            Content = contentGrid,
            CloseButtonText = L("common.close"),
            XamlRoot = Content.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private async Task<int> ImportPanelTemplatesAsync(Document doc)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".paneltemplate.json");
        picker.FileTypeFilter.Add(".json");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return 0;

        try
        {
            var templates = await LoadPanelTemplatesFromFileAsync(file.Path);
            if (templates.Count == 0)
            {
                SetStatusMessage(L("props.template.no_templates_in_file"));
                return 0;
            }

            var commands = new List<ICommand>();
            foreach (var template in templates)
            {
                var unique = EnsureUniquePanelTemplate(template, doc);
                commands.Add(new AddPanelLayoutTemplateCommand(unique));
            }

            _editorState.ExecuteTransaction("Import panel templates", commands);
            SetStatusMessage(LF("panel_template.status.imported", commands.Count));
            return commands.Count;
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("panel_template.error.import_failed", ex.Message));
            return 0;
        }
    }

    private async Task<int> ImportPanelTemplatesFromFolderAsync(Document doc, string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            SetStatusMessage(L("props.template.folder_not_found"));
            return 0;
        }

        var files = Directory.EnumerateFiles(folderPath, "*.paneltemplate.json", SearchOption.TopDirectoryOnly).ToList();
        if (files.Count == 0)
        {
            SetStatusMessage(L("props.template.no_files_in_folder"));
            return 0;
        }

        var templates = new List<PanelLayoutTemplate>();
        foreach (var file in files)
        {
            try
            {
                var loaded = await LoadPanelTemplatesFromFileAsync(file);
                templates.AddRange(loaded);
            }
            catch
            {
            }
        }

        if (templates.Count == 0)
        {
            SetStatusMessage(L("props.template.no_templates_loaded"));
            return 0;
        }

        var commands = new List<ICommand>();
        foreach (var template in templates)
        {
            var unique = EnsureUniquePanelTemplate(template, doc);
            commands.Add(new AddPanelLayoutTemplateCommand(unique));
        }

        _editorState.ExecuteTransaction("Import panel templates", commands);
        return commands.Count;
    }

    private async Task ExportPanelTemplatesAsync(IEnumerable<PanelLayoutTemplate> templates)
    {
        var templateList = templates.ToList();
        if (templateList.Count == 0) return;

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Panel Template", new List<string> { ".paneltemplate.json" });
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = templateList.Count == 1
            ? templateList[0].Name
            : "panel-templates";

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        var payload = templateList.Count == 1
            ? (object)PanelLayoutTemplateFile.FromTemplate(templateList[0])
            : templateList.Select(PanelLayoutTemplateFile.FromTemplate).ToList();

        var json = JsonSerializer.Serialize(payload, PanelTemplateJsonOptions);
        await File.WriteAllTextAsync(file.Path, json, Encoding.UTF8);
        SetStatusMessage(LF("panel_template.status.exported", templateList.Count));
    }

    private async Task ExportPanelTemplatesToFolderAsync(IEnumerable<PanelLayoutTemplate> templates, string folderPath)
    {
        var templateList = templates.ToList();
        if (templateList.Count == 0) return;

        Directory.CreateDirectory(folderPath);

        foreach (var template in templateList)
        {
            var payload = PanelLayoutTemplateFile.FromTemplate(template);
            var json = JsonSerializer.Serialize(payload, PanelTemplateJsonOptions);
            var filePath = BuildUniqueTemplateFilePath(folderPath, template.Name);
            await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        }

        SetStatusMessage(LF("panel_template.status.exported_to_folder", templateList.Count));
    }

    private async Task<List<PanelLayoutTemplate>> LoadPanelTemplatesFromFileAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json)) return new List<PanelLayoutTemplate>();

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("["))
        {
            var files = JsonSerializer.Deserialize<List<PanelLayoutTemplateFile>>(json, PanelTemplateJsonOptions) ?? new List<PanelLayoutTemplateFile>();
            return files.Select(file => file.ToTemplate()).ToList();
        }

        var fileSingle = JsonSerializer.Deserialize<PanelLayoutTemplateFile>(json, PanelTemplateJsonOptions);
        return fileSingle != null ? new List<PanelLayoutTemplate> { fileSingle.ToTemplate() } : new List<PanelLayoutTemplate>();
    }

    private static string BuildUniqueTemplateFilePath(string folderPath, string templateName)
    {
        var baseName = MakeSafeFileName(templateName);
        var candidate = Path.Combine(folderPath, $"{baseName}.paneltemplate.json");
        var index = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName} ({index}).paneltemplate.json");
            index++;
        }

        return candidate;
    }

    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "panel-template";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "panel-template" : cleaned;
    }

    private PanelLayoutTemplate EnsureUniquePanelTemplate(PanelLayoutTemplate template, Document doc)
    {
        var uniqueName = GetUniquePanelTemplateName(template.Name, doc.PanelTemplates);
        var uniqueId = doc.PanelTemplates.Any(existing => existing.Id == template.Id)
            ? Guid.NewGuid()
            : template.Id;

        if (uniqueName == template.Name && uniqueId == template.Id)
        {
            return template;
        }

        return new PanelLayoutTemplate(
            uniqueId,
            uniqueName,
            template.Size,
            template.Panels,
            template.Description,
            template.Tags,
            template.Category);
    }

    private static string GetUniquePanelTemplateName(string desiredName, IReadOnlyList<PanelLayoutTemplate> templates, Guid? excludeTemplateId = null)
    {
        var baseName = string.IsNullOrWhiteSpace(desiredName) ? "Panel Layout" : desiredName.Trim();
        var existing = new HashSet<string>(
            templates
                .Where(template => !excludeTemplateId.HasValue || template.Id != excludeTemplateId.Value)
                .Select(template => template.Name),
            StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseName)) return baseName;

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{baseName} ({index})";
            index++;
        }
        while (existing.Contains(candidate));

        return candidate;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static List<string> ParseTemplateTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        var tokens = raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
            {
                tags.Add(trimmed);
            }
        }
        return tags;
    }

    private async Task<List<PanelTemplateViewModel>> BuildPanelTemplateViewModelsAsync(Document doc)
    {
        var currentPageSize = doc.ActivePage?.Size ?? new Size2(0, 0);
        var list = new List<PanelTemplateViewModel>();

        var sortedTemplates = doc.PanelTemplates
            .OrderByDescending(template => AreTemplateSizesEquivalent(template.Size, currentPageSize))
            .ThenBy(template => template.Name);

        foreach (var template in sortedTemplates)
        {
            var matchesPageSize = AreTemplateSizesEquivalent(template.Size, currentPageSize);
            var thumbnail = await RenderPanelTemplateThumbnailAsync(template);
            list.Add(new PanelTemplateViewModel
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description ?? "",
                Category = template.Category,
                Tags = template.Tags.ToList(),
                SizeLabel = $"{template.Size.Width:F0} × {template.Size.Height:F0}",
                Thumbnail = thumbnail,
                MatchesCurrentPageSize = matchesPageSize
            });
        }

        return list;
    }

    private async Task<WriteableBitmap?> RenderPanelTemplateThumbnailAsync(PanelLayoutTemplate template)
    {
        if (_canvasDevice == null) return null;

        const int width = 144;
        const int height = 192;

        using var renderTarget = new CanvasRenderTarget(_canvasDevice, width, height, 96);
        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Windows.UI.Color.FromArgb(255, 24, 24, 24));

            var padding = 6f;
            var scale = Math.Min(
                (width - padding * 2f) / Math.Max(1f, template.Size.Width),
                (height - padding * 2f) / Math.Max(1f, template.Size.Height));

            var offsetX = (width - template.Size.Width * scale) / 2f;
            var offsetY = (height - template.Size.Height * scale) / 2f;

            ds.Transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);

            foreach (var panel in template.Panels)
            {
                using var geometry = PanelGeometry.CreateGeometry(ds, panel);
                var strokeColor = Windows.UI.Color.FromArgb(220, 220, 220, 220);
                var fillColor = Windows.UI.Color.FromArgb(40, 220, 220, 220);
                ds.FillGeometry(geometry, fillColor);
                ds.DrawGeometry(geometry, strokeColor, 1f / scale);
            }

            ds.Transform = Matrix3x2.Identity;
        }

        var bytes = renderTarget.GetPixelBytes();
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(bytes, 0, bytes.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private sealed class PanelTemplateViewModel
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string? Category { get; init; }
        public List<string> Tags { get; init; } = new();
        public string SizeLabel { get; init; } = "";
        public WriteableBitmap? Thumbnail { get; init; }
        public bool MatchesCurrentPageSize { get; init; }

        public string TagsLabel => Tags.Count > 0 ? string.Join(", ", Tags) : "";
        public string CategoryLabel => string.IsNullOrWhiteSpace(Category) ? "Uncategorized" : Category!;
        public Visibility MatchIconVisibility => MatchesCurrentPageSize ? Visibility.Visible : Visibility.Collapsed;
    }


    private static TextPath CreateDefaultTextPathForBalloon(Balloon balloon)
    {
        return TextPath.CreateDefault(balloon.ComputedSize);
    }

    private void UpdateBalloonTextPathControls(Balloon balloon)
    {
        var path = balloon.TextPath;
        var isEnabled = path != null;

        TextOnPathEffectToggle.IsOn = isEnabled;
        TextOnPathOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        TextOnPathReverseToggle.IsOn = path?.ReverseDirection ?? false;
        TextOnPathOffsetBox.Value = path?.Offset ?? 0d;
        TextOnPathStartBox.Value = (path?.StartPosition ?? 0f) * 100d;
        TextOnPathEndBox.Value = (path?.EndPosition ?? 1f) * 100d;
        TextOnPathStartXBox.Value = path?.Start.X ?? 0d;
        TextOnPathStartYBox.Value = path?.Start.Y ?? 0d;
        TextOnPathControl1XBox.Value = path?.Control1.X ?? 0d;
        TextOnPathControl1YBox.Value = path?.Control1.Y ?? 0d;
        TextOnPathControl2XBox.Value = path?.Control2.X ?? 0d;
        TextOnPathControl2YBox.Value = path?.Control2.Y ?? 0d;
        TextOnPathEndXBox.Value = path?.End.X ?? 0d;
        TextOnPathEndYBox.Value = path?.End.Y ?? 0d;

        TextOnPathReverseToggle.IsEnabled = isEnabled;
        TextOnPathOffsetBox.IsEnabled = isEnabled;
        TextOnPathStartBox.IsEnabled = isEnabled;
        TextOnPathEndBox.IsEnabled = isEnabled;
        TextOnPathStartXBox.IsEnabled = isEnabled;
        TextOnPathStartYBox.IsEnabled = isEnabled;
        TextOnPathControl1XBox.IsEnabled = isEnabled;
        TextOnPathControl1YBox.IsEnabled = isEnabled;
        TextOnPathControl2XBox.IsEnabled = isEnabled;
        TextOnPathControl2YBox.IsEnabled = isEnabled;
        TextOnPathEndXBox.IsEnabled = isEnabled;
        TextOnPathEndYBox.IsEnabled = isEnabled;
    }

    private void CommitBalloonTextPathFromControls()
    {
        if (_isUpdatingProperties) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;
        if (!TextOnPathEffectToggle.IsOn) return;
        if (double.IsNaN(TextOnPathOffsetBox.Value) ||
            double.IsNaN(TextOnPathStartBox.Value) ||
            double.IsNaN(TextOnPathEndBox.Value) ||
            double.IsNaN(TextOnPathStartXBox.Value) ||
            double.IsNaN(TextOnPathStartYBox.Value) ||
            double.IsNaN(TextOnPathControl1XBox.Value) ||
            double.IsNaN(TextOnPathControl1YBox.Value) ||
            double.IsNaN(TextOnPathControl2XBox.Value) ||
            double.IsNaN(TextOnPathControl2YBox.Value) ||
            double.IsNaN(TextOnPathEndXBox.Value) ||
            double.IsNaN(TextOnPathEndYBox.Value))
        {
            return;
        }

        var path = new TextPath(
            new Point2((float)TextOnPathStartXBox.Value, (float)TextOnPathStartYBox.Value),
            new Point2((float)TextOnPathControl1XBox.Value, (float)TextOnPathControl1YBox.Value),
            new Point2((float)TextOnPathControl2XBox.Value, (float)TextOnPathControl2YBox.Value),
            new Point2((float)TextOnPathEndXBox.Value, (float)TextOnPathEndYBox.Value),
            (float)TextOnPathOffsetBox.Value,
            (float)(TextOnPathStartBox.Value / 100.0),
            (float)(TextOnPathEndBox.Value / 100.0),
            TextOnPathReverseToggle.IsOn);

        if (balloon.TextPath?.Equals(path) == true) return;
        _editorState.Execute(new SetBalloonTextPathCommand(balloon.Id, path));
    }

    private void TextOnPathEffectToggle_Toggled(object sender, RoutedEventArgs e)
    {
        TextOnPathOptionsPanel.Visibility = TextOnPathEffectToggle.IsOn
            ? Visibility.Visible : Visibility.Collapsed;

        if (_isUpdatingProperties) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        if (!TextOnPathEffectToggle.IsOn)
        {
            if (balloon.TextPath != null)
            {
                _editorState.Execute(new SetBalloonTextPathCommand(balloon.Id, null));
            }
            return;
        }

        if (balloon.TextPath == null)
        {
            _editorState.Execute(new SetBalloonTextPathCommand(balloon.Id, CreateDefaultTextPathForBalloon(balloon)));
        }
    }

    private void TextOnPathReverseToggle_Toggled(object sender, RoutedEventArgs e)
    {
        CommitBalloonTextPathFromControls();
    }

    private void BalloonTextPathNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        CommitBalloonTextPathFromControls();
    }

    private void UpdateBalloonTextWarpControls(TextStyle style)
    {
        var isActive = style.WarpPreset != TextWarpPreset.None;
        TextWarpEffectToggle.IsOn = isActive;
        TextWarpOptionsPanel.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        var presetTag = style.WarpPreset.ToString();
        if (!SelectComboBoxItemByTag(TextWarpPresetComboBox, presetTag))
        {
            TextWarpPresetComboBox.SelectedIndex = 0;
        }

        TextWarpIntensityBox.Value = style.WarpIntensity;
        TextWarpHorizontalBox.Value = style.WarpHorizontalDistortion;
        TextWarpVerticalBox.Value = style.WarpVerticalDistortion;

        var mesh = style.WarpMesh ?? TextWarpMesh.Identity;
        TextWarpTopLeftXBox.Value = mesh.TopLeftOffset.X;
        TextWarpTopLeftYBox.Value = mesh.TopLeftOffset.Y;
        TextWarpTopRightXBox.Value = mesh.TopRightOffset.X;
        TextWarpTopRightYBox.Value = mesh.TopRightOffset.Y;
        TextWarpBottomRightXBox.Value = mesh.BottomRightOffset.X;
        TextWarpBottomRightYBox.Value = mesh.BottomRightOffset.Y;
        TextWarpBottomLeftXBox.Value = mesh.BottomLeftOffset.X;
        TextWarpBottomLeftYBox.Value = mesh.BottomLeftOffset.Y;
    }

    private bool TryGetBalloonTextWarpMesh(out TextWarpMesh mesh)
    {
        mesh = TextWarpMesh.Identity;
        if (double.IsNaN(TextWarpTopLeftXBox.Value) ||
            double.IsNaN(TextWarpTopLeftYBox.Value) ||
            double.IsNaN(TextWarpTopRightXBox.Value) ||
            double.IsNaN(TextWarpTopRightYBox.Value) ||
            double.IsNaN(TextWarpBottomRightXBox.Value) ||
            double.IsNaN(TextWarpBottomRightYBox.Value) ||
            double.IsNaN(TextWarpBottomLeftXBox.Value) ||
            double.IsNaN(TextWarpBottomLeftYBox.Value))
        {
            return false;
        }

        mesh = new TextWarpMesh
        {
            TopLeftOffset = new Point2((float)TextWarpTopLeftXBox.Value, (float)TextWarpTopLeftYBox.Value),
            TopRightOffset = new Point2((float)TextWarpTopRightXBox.Value, (float)TextWarpTopRightYBox.Value),
            BottomRightOffset = new Point2((float)TextWarpBottomRightXBox.Value, (float)TextWarpBottomRightYBox.Value),
            BottomLeftOffset = new Point2((float)TextWarpBottomLeftXBox.Value, (float)TextWarpBottomLeftYBox.Value)
        };
        return true;
    }

    private void CommitBalloonTextWarpFromControls()
    {
        if (_isUpdatingProperties || _editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        if (double.IsNaN(TextWarpIntensityBox.Value) ||
            double.IsNaN(TextWarpHorizontalBox.Value) ||
            double.IsNaN(TextWarpVerticalBox.Value))
        {
            return;
        }

        if (!TryGetBalloonTextWarpMesh(out var mesh))
        {
            return;
        }

        var preset = TextWarpPreset.None;
        if (TextWarpPresetComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string presetTag &&
            Enum.TryParse<TextWarpPreset>(presetTag, out var parsedPreset))
        {
            preset = parsedPreset;
        }

        var newStyle = balloon.TextStyle.With(
            warpPreset: preset,
            warpIntensity: Math.Clamp((float)TextWarpIntensityBox.Value, -1f, 1f),
            warpHorizontalDistortion: Math.Clamp((float)TextWarpHorizontalBox.Value, -1f, 1f),
            warpVerticalDistortion: Math.Clamp((float)TextWarpVerticalBox.Value, -1f, 1f),
            warpMesh: mesh);

        if (TextStyleUtilities.AreEquivalent(newStyle, balloon.TextStyle)) return;
        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }

    private void TextWarpEffectToggle_Toggled(object sender, RoutedEventArgs e)
    {
        TextWarpOptionsPanel.Visibility = TextWarpEffectToggle.IsOn
            ? Visibility.Visible : Visibility.Collapsed;

        if (_isUpdatingProperties) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        if (!TextWarpEffectToggle.IsOn)
        {
            var clearStyle = balloon.TextStyle.With(
                warpPreset: TextWarpPreset.None,
                warpIntensity: 0f,
                warpHorizontalDistortion: 0f,
                warpVerticalDistortion: 0f,
                warpMesh: TextWarpMesh.Identity);
            if (!TextStyleUtilities.AreEquivalent(clearStyle, balloon.TextStyle))
            {
                _editorState.Execute(new SetTextStyleCommand(balloon.Id, clearStyle));
                MainCanvas.Invalidate();
            }
        }
    }

    private void BalloonTextWarpPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitBalloonTextWarpFromControls();
    }

    private void BalloonTextWarpNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        CommitBalloonTextWarpFromControls();
    }

    private void BalloonTextWarpResetMeshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editorState?.Document?.SelectedBalloon == null) return;

        var balloon = _editorState.Document.SelectedBalloon;
        var newStyle = balloon.TextStyle.With(warpMesh: TextWarpMesh.Identity);
        if (TextStyleUtilities.AreEquivalent(newStyle, balloon.TextStyle)) return;

        _editorState.Execute(new SetTextStyleCommand(balloon.Id, newStyle));
        MainCanvas.Invalidate();
    }


    private bool _isUpdatingLayerProperties;

    private void UpdateLayerPropertiesPanel()
    {
        var layer = _editorState.Document?.ActiveLayer;
        if (layer == null)
        {
            LayerPropertiesPanel.Visibility = Visibility.Collapsed;
            return;
        }

        LayerPropertiesPanel.Visibility = Visibility.Visible;

        _isUpdatingLayerProperties = true;
        try
        {
            LayerNameTextBox.Text = layer.Name;
            LayerVisibilityToggle.IsOn = layer.IsVisible;
            LayerLockToggle.IsOn = layer.IsLocked;

            var percent = layer.Opacity * 100f;
            LayerDetailOpacitySlider.Value = percent;
            LayerDetailOpacityValueText.Text = $"{percent:F0}%";
            if (!SelectComboBoxItemByTag(LayerBlendModeComboBox, layer.BlendMode.ToString()))
            {
                LayerBlendModeComboBox.SelectedIndex = 0;
            }

            LayerBalloonCountText.Text = $"{layer.Balloons.Count} balloon{(layer.Balloons.Count == 1 ? "" : "s")}";
        }
        finally
        {
            _isUpdatingLayerProperties = false;
        }
    }

    private void LayerNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitLayerNameChange();
    }

    private void LayerNameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitLayerNameChange();
            e.Handled = true;
        }
    }

    private void CommitLayerNameChange()
    {
        if (_isUpdatingLayerProperties) return;

        var layer = _editorState.Document?.ActiveLayer;
        if (layer == null) return;

        var newName = LayerNameTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName)) newName = "Layer";
        if (newName == layer.Name) return;

        _editorState.Execute(new RenameLayerCommand(layer.Id, newName));
        RefreshLayerList();
    }

    private void LayerVisibilityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingLayerProperties) return;

        var layer = _editorState.Document?.ActiveLayer;
        if (layer == null) return;

        _editorState.Execute(new SetLayerVisibilityCommand(layer.Id, LayerVisibilityToggle.IsOn));
        RefreshLayerList();
        MainCanvas.Invalidate();
    }

    private void LayerLockToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingLayerProperties) return;

        var layer = _editorState.Document?.ActiveLayer;
        if (layer == null) return;

        _editorState.Execute(new SetLayerLockedCommand(layer.Id, LayerLockToggle.IsOn));
        RefreshLayerList();
    }

    private void LayerDetailOpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingLayerProperties || _editorState.Document?.ActiveLayer == null) return;

        var layer = _editorState.Document.ActiveLayer;
        var newOpacity = (float)(e.NewValue / 100.0);
        newOpacity = Math.Clamp(newOpacity, 0f, 1f);

        LayerDetailOpacityValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(newOpacity - layer.Opacity) < 0.001f) return;
        _editorState.Execute(new SetLayerOpacityCommand(layer.Id, newOpacity));
        MainCanvas.Invalidate();
    }

    private void LayerBlendModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayerProperties || _editorState.Document?.ActiveLayer == null) return;
        if (LayerBlendModeComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string blendTag) return;
        if (!Enum.TryParse<LayerBlendMode>(blendTag, out var blendMode)) return;

        var layer = _editorState.Document.ActiveLayer;
        if (layer.BlendMode == blendMode) return;

        _editorState.Execute(new SetLayerBlendModeCommand(layer.Id, blendMode));
        MainCanvas.Invalidate();
    }



    private bool _isUpdatingFloatingImageProperties = false;
    private bool _floatingImageDetailMerged;

    private void FloatingImageBasicTabButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureFloatingImageDetailSingleScrollView();
    }

    private void FloatingImageAdvancedTabButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureFloatingImageDetailSingleScrollView();
    }

    private void EnsureFloatingImageDetailSingleScrollView()
    {
        if (_floatingImageDetailMerged)
        {
            return;
        }

        if (FloatingImageBasicTabScrollViewer.Content is not StackPanel basicContent ||
            FloatingImageAdvancedTabScrollViewer.Content is not StackPanel advancedContent)
        {
            return;
        }

        var advancedChildren = advancedContent.Children.ToList();
        if (advancedChildren.Count == 0)
        {
            _floatingImageDetailMerged = true;
            return;
        }

        basicContent.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 8),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 49, 49, 49))
        });

        foreach (var child in advancedChildren)
        {
            advancedContent.Children.Remove(child);
            basicContent.Children.Add(child);
        }

        FloatingImageAdvancedTabScrollViewer.Visibility = Visibility.Collapsed;
        FloatingImageAdvancedTabScrollViewer.IsHitTestVisible = false;
        _floatingImageDetailMerged = true;
    }

    private void UpdateFloatingImagePropertiesPanel(FloatingImage image)
    {
        FloatingImagePropertiesPanel.Visibility = Visibility.Visible;

        _isUpdatingFloatingImageProperties = true;
        try
        {
            FloatingImagePathText.Text = string.IsNullOrEmpty(image.ImagePath)
                ? "(no file)"
                : System.IO.Path.GetFileName(image.ImagePath);

            var sourceText = image.Source;
            if (string.IsNullOrWhiteSpace(sourceText) && !string.IsNullOrWhiteSpace(image.ImagePath))
            {
                sourceText = ResolveBackgroundPath(image.ImagePath) ?? image.ImagePath;
            }
            FloatingImageSourceText.Text = string.IsNullOrWhiteSpace(sourceText) ? L("common.none") : sourceText;
            FloatingImageMediaInfoText.Text = BuildFloatingImageMediaInfoText(image);

            var page = _editorState.Document?.ActivePage;
            var layerName = "(none)";
            if (page != null && image.LayerId.HasValue)
            {
                layerName = page.FindLayer(image.LayerId.Value)?.Name ?? "(missing layer)";
            }

            FloatingImageLayerText.Text = string.Format(L("image.label.layer_format"), layerName);
            PopulateFloatingImagePanelComboBox(page, image.PanelId);
            FloatingImageConstrainToggle.IsEnabled = image.PanelId.HasValue;
            FloatingImageConstrainToggle.IsOn = image.PanelId.HasValue && image.ConstrainToPanel;

            FloatingImageXBox.Value = image.Bounds.X;
            FloatingImageYBox.Value = image.Bounds.Y;
            FloatingImageWidthBox.Value = image.Bounds.Width;
            FloatingImageHeightBox.Value = image.Bounds.Height;
            FloatingImageRotationSlider.Value = image.Rotation;
            FloatingImageRotationBox.Value = image.Rotation;

            var percent = image.Opacity * 100f;
            FloatingImageOpacitySlider.Value = percent;
            FloatingImageOpacityValueText.Text = $"{percent:F0}%";

            FloatingImageShadowToggle.IsOn = image.ShadowEnabled;
            FloatingImageShadowOptionsPanel.Visibility = image.ShadowEnabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateOutlineSelector(FloatingImageShadowColorPreview, FloatingImageShadowColorComboBox, image.ShadowColor, "CUSTOM");
            FloatingImageShadowOpacitySlider.Value = image.ShadowOpacity * 100f;
            FloatingImageShadowOpacityValueText.Text = $"{image.ShadowOpacity * 100f:F0}%";
            FloatingImageShadowFalloffSlider.Value = image.ShadowFalloff;
            FloatingImageShadowFalloffValueText.Text = $"{image.ShadowFalloff:F1}";
            FloatingImageShadowOffsetXSlider.Value = image.ShadowOffsetX;
            FloatingImageShadowOffsetXValueText.Text = $"{image.ShadowOffsetX:F0}";
            FloatingImageShadowOffsetYSlider.Value = image.ShadowOffsetY;
            FloatingImageShadowOffsetYValueText.Text = $"{image.ShadowOffsetY:F0}";

            FloatingImageGlowToggle.IsOn = image.GlowEnabled;
            FloatingImageGlowOptionsPanel.Visibility = image.GlowEnabled ? Visibility.Visible : Visibility.Collapsed;
            UpdateOutlineSelector(FloatingImageGlowColorPreview, FloatingImageGlowColorComboBox, image.GlowColor, "CUSTOM");
            FloatingImageGlowSizeSlider.Value = image.GlowSize;
            FloatingImageGlowSizeValueText.Text = $"{image.GlowSize:F1}";
            FloatingImageGlowIntensitySlider.Value = image.GlowOpacity * 100f;
            FloatingImageGlowIntensityValueText.Text = $"{image.GlowOpacity * 100f:F0}%";

            FloatingImageVisibilityToggle.IsOn = image.IsVisible;
            FloatingImageLockToggle.IsOn = image.IsLocked;
            EnsureFloatingImageDetailSingleScrollView();
            FloatingImageBasicTabScrollViewer.Visibility = Visibility.Visible;
            FloatingImageAdvancedTabScrollViewer.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _isUpdatingFloatingImageProperties = false;
        }
    }

    private void PopulateFloatingImagePanelComboBox(DocumentPage? page, Guid? selectedPanelId)
    {
        FloatingImagePanelComboBox.Items.Clear();

        var noneItem = new ComboBoxItem
        {
            Content = L("common.none"),
            Tag = string.Empty
        };
        FloatingImagePanelComboBox.Items.Add(noneItem);

        if (page != null)
        {
            foreach (var panel in page.Panels
                         .OrderBy(panel => panel.Order)
                         .ThenBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                FloatingImagePanelComboBox.Items.Add(new ComboBoxItem
                {
                    Content = panel.Name,
                    Tag = panel.Id.ToString()
                });
            }
        }

        FloatingImagePanelComboBox.SelectedItem = noneItem;
        if (!selectedPanelId.HasValue) return;

        var selectedTag = selectedPanelId.Value.ToString();
        foreach (var item in FloatingImagePanelComboBox.Items)
        {
            if (item is ComboBoxItem comboItem &&
                string.Equals(comboItem.Tag?.ToString(), selectedTag, StringComparison.OrdinalIgnoreCase))
            {
                FloatingImagePanelComboBox.SelectedItem = comboItem;
                return;
            }
        }
    }

    private void FloatingImagePanelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || !_editorState.SelectedFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
        if (image == null) return;
        if (FloatingImagePanelComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

        Guid? panelId = null;
        var panelTag = selectedItem.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(panelTag) && Guid.TryParse(panelTag, out var parsedPanelId))
        {
            panelId = parsedPanelId;
        }

        if (image.PanelId == panelId) return;

        _editorState.Execute(new SetFloatingImagePanelCommand(page.Id, image.Id, panelId));
        RefreshLayerList();
        UpdateFloatingImagePropertiesPanel(image);
        MainCanvas.Invalidate();
    }

    private void FloatingImageConstrainToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        if (!image.PanelId.HasValue)
        {
            _isUpdatingFloatingImageProperties = true;
            try
            {
                FloatingImageConstrainToggle.IsEnabled = false;
                FloatingImageConstrainToggle.IsOn = false;
            }
            finally
            {
                _isUpdatingFloatingImageProperties = false;
            }
            return;
        }

        var newValue = FloatingImageConstrainToggle.IsOn;
        if (image.ConstrainToPanel == newValue) return;

        _editorState.Execute(new SetFloatingImageConstrainToPanelCommand(page.Id, image.Id, newValue));
        MainCanvas.Invalidate();
    }

    private string BuildFloatingImageMediaInfoText(FloatingImage image)
    {
        var parts = new List<string>();
        var bitmap = GetFloatingImage(image.Id);
        if (bitmap != null)
        {
            parts.Add($"{bitmap.SizeInPixels.Width}x{bitmap.SizeInPixels.Height}px");
        }

        var resolvedPath = string.IsNullOrWhiteSpace(image.ImagePath)
            ? null
            : ResolveBackgroundPath(image.ImagePath) ?? image.ImagePath;
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            var ext = Path.GetExtension(resolvedPath);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                parts.Add(ext.TrimStart('.').ToUpperInvariant());
            }

            if (File.Exists(resolvedPath))
            {
                var fileInfo = new FileInfo(resolvedPath);
                parts.Add(FormatFileSize(fileInfo.Length));
            }
        }

        return parts.Count == 0 ? L("common.none") : string.Join(" • ", parts);
    }

    private static string FormatFileSize(long bytes)
    {
        const long kb = 1024;
        const long mb = 1024 * 1024;
        const long gb = 1024 * 1024 * 1024;

        if (bytes >= gb)
        {
            return $"{bytes / (double)gb:F2} GB";
        }
        if (bytes >= mb)
        {
            return $"{bytes / (double)mb:F2} MB";
        }
        if (bytes >= kb)
        {
            return $"{bytes / (double)kb:F1} KB";
        }
        return $"{bytes} B";
    }

    private bool TryGetSelectedFloatingImage(out DocumentPage page, out FloatingImage image)
    {
        page = null!;
        image = null!;

        var activePage = _editorState.Document?.ActivePage;
        if (activePage == null || !_editorState.SelectedFloatingImageId.HasValue)
        {
            return false;
        }

        var selectedImage = activePage.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
        if (selectedImage == null) return false;

        page = activePage;
        image = selectedImage;
        return true;
    }

    private void FloatingImagePositionBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingFloatingImageProperties || double.IsNaN(args.NewValue)) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var newBounds = new Rect((float)FloatingImageXBox.Value, (float)FloatingImageYBox.Value, image.Bounds.Width, image.Bounds.Height);
        if (newBounds.Equals(image.Bounds)) return;

        _editorState.Execute(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
        MainCanvas.Invalidate();
    }

    private void FloatingImageSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingFloatingImageProperties || double.IsNaN(args.NewValue)) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var width = Math.Max(1f, (float)FloatingImageWidthBox.Value);
        var height = Math.Max(1f, (float)FloatingImageHeightBox.Value);
        var newBounds = new Rect(image.Bounds.X, image.Bounds.Y, width, height);
        if (newBounds.Equals(image.Bounds)) return;

        _editorState.Execute(new SetFloatingImageBoundsCommand(page.Id, image.Id, newBounds));
        MainCanvas.Invalidate();
    }

    private void FloatingImageRotationSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var rotation = Math.Clamp((float)e.NewValue, -180f, 180f);

        _isUpdatingFloatingImageProperties = true;
        FloatingImageRotationBox.Value = rotation;
        _isUpdatingFloatingImageProperties = false;

        if (Math.Abs(rotation - image.Rotation) < 0.001f) return;
        _editorState.Execute(new SetFloatingImageRotationCommand(page.Id, image.Id, rotation));
        MainCanvas.Invalidate();
    }

    private void FloatingImageRotationBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingFloatingImageProperties || double.IsNaN(args.NewValue)) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var rotation = Math.Clamp((float)args.NewValue, -180f, 180f);

        _isUpdatingFloatingImageProperties = true;
        FloatingImageRotationSlider.Value = rotation;
        _isUpdatingFloatingImageProperties = false;

        if (Math.Abs(rotation - image.Rotation) < 0.001f) return;
        _editorState.Execute(new SetFloatingImageRotationCommand(page.Id, image.Id, rotation));
        MainCanvas.Invalidate();
    }

    private void FloatingImageShadowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        FloatingImageShadowOptionsPanel.Visibility = FloatingImageShadowToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        CommitFloatingImageShadowStyle();
    }

    private async void FloatingImageShadowColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;
        if (FloatingImageShadowColorComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string colorTag) return;

        Color color;
        if (string.Equals(colorTag, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            var custom = await ShowColorPickerDialogAsync(image.ShadowColor);
            if (!custom.HasValue)
            {
                _isUpdatingFloatingImageProperties = true;
                UpdateOutlineSelector(FloatingImageShadowColorPreview, FloatingImageShadowColorComboBox, image.ShadowColor, "CUSTOM");
                _isUpdatingFloatingImageProperties = false;
                return;
            }
            color = custom.Value;
        }
        else
        {
            color = ParseHexColor(colorTag);
        }

        var opacity = (float)(FloatingImageShadowOpacitySlider.Value / 100.0);
        var offsetX = (float)FloatingImageShadowOffsetXSlider.Value;
        var offsetY = (float)FloatingImageShadowOffsetYSlider.Value;
        var falloff = (float)FloatingImageShadowFalloffSlider.Value;
        _editorState.Execute(new SetFloatingImageShadowCommand(page.Id, image.Id, FloatingImageShadowToggle.IsOn, color, opacity, offsetX, offsetY, falloff));
        MainCanvas.Invalidate();
    }

    private void FloatingImageShadowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        FloatingImageShadowOpacityValueText.Text = $"{FloatingImageShadowOpacitySlider.Value:F0}%";
        FloatingImageShadowFalloffValueText.Text = $"{FloatingImageShadowFalloffSlider.Value:F1}";
        CommitFloatingImageShadowStyle();
    }

    private void FloatingImageShadowOffsetSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        FloatingImageShadowOffsetXValueText.Text = $"{FloatingImageShadowOffsetXSlider.Value:F0}";
        FloatingImageShadowOffsetYValueText.Text = $"{FloatingImageShadowOffsetYSlider.Value:F0}";
        CommitFloatingImageShadowStyle();
    }

    private void CommitFloatingImageShadowStyle()
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var color = image.ShadowColor;
        if (FloatingImageShadowColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorTag &&
            !string.Equals(colorTag, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            color = ParseHexColor(colorTag);
        }

        var enabled = FloatingImageShadowToggle.IsOn;
        var opacity = (float)(FloatingImageShadowOpacitySlider.Value / 100.0);
        var offsetX = (float)FloatingImageShadowOffsetXSlider.Value;
        var offsetY = (float)FloatingImageShadowOffsetYSlider.Value;
        var falloff = (float)FloatingImageShadowFalloffSlider.Value;

        if (image.ShadowEnabled == enabled &&
            image.ShadowColor.Equals(color) &&
            Math.Abs(image.ShadowOpacity - opacity) < 0.001f &&
            Math.Abs(image.ShadowOffsetX - offsetX) < 0.001f &&
            Math.Abs(image.ShadowOffsetY - offsetY) < 0.001f &&
            Math.Abs(image.ShadowFalloff - falloff) < 0.001f)
        {
            return;
        }

        _editorState.Execute(new SetFloatingImageShadowCommand(page.Id, image.Id, enabled, color, opacity, offsetX, offsetY, falloff));
        MainCanvas.Invalidate();
    }

    private void FloatingImageGlowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        FloatingImageGlowOptionsPanel.Visibility = FloatingImageGlowToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        CommitFloatingImageGlowStyle();
    }

    private async void FloatingImageGlowColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;
        if (FloatingImageGlowColorComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string colorTag) return;

        Color color;
        if (string.Equals(colorTag, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            var custom = await ShowColorPickerDialogAsync(image.GlowColor);
            if (!custom.HasValue)
            {
                _isUpdatingFloatingImageProperties = true;
                UpdateOutlineSelector(FloatingImageGlowColorPreview, FloatingImageGlowColorComboBox, image.GlowColor, "CUSTOM");
                _isUpdatingFloatingImageProperties = false;
                return;
            }
            color = custom.Value;
        }
        else
        {
            color = ParseHexColor(colorTag);
        }

        var opacity = (float)(FloatingImageGlowIntensitySlider.Value / 100.0);
        var size = (float)FloatingImageGlowSizeSlider.Value;
        _editorState.Execute(new SetFloatingImageGlowCommand(page.Id, image.Id, FloatingImageGlowToggle.IsOn, color, opacity, size));
        MainCanvas.Invalidate();
    }

    private void FloatingImageGlowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;
        FloatingImageGlowSizeValueText.Text = $"{FloatingImageGlowSizeSlider.Value:F1}";
        FloatingImageGlowIntensityValueText.Text = $"{FloatingImageGlowIntensitySlider.Value:F0}%";
        CommitFloatingImageGlowStyle();
    }

    private void CommitFloatingImageGlowStyle()
    {
        if (_isUpdatingFloatingImageProperties) return;
        if (!TryGetSelectedFloatingImage(out var page, out var image)) return;

        var color = image.GlowColor;
        if (FloatingImageGlowColorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string colorTag &&
            !string.Equals(colorTag, "CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            color = ParseHexColor(colorTag);
        }

        var enabled = FloatingImageGlowToggle.IsOn;
        var opacity = (float)(FloatingImageGlowIntensitySlider.Value / 100.0);
        var size = (float)FloatingImageGlowSizeSlider.Value;

        if (image.GlowEnabled == enabled &&
            image.GlowColor.Equals(color) &&
            Math.Abs(image.GlowOpacity - opacity) < 0.001f &&
            Math.Abs(image.GlowSize - size) < 0.001f)
        {
            return;
        }

        _editorState.Execute(new SetFloatingImageGlowCommand(page.Id, image.Id, enabled, color, opacity, size));
        MainCanvas.Invalidate();
    }

    private void FloatingImageOpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || !_editorState.SelectedFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
        if (image == null) return;

        var newOpacity = (float)(e.NewValue / 100.0);
        FloatingImageOpacityValueText.Text = $"{e.NewValue:F0}%";

        if (Math.Abs(newOpacity - image.Opacity) < 0.001f) return;

        _editorState.Execute(new SetFloatingImageOpacityCommand(page.Id, image.Id, newOpacity));
        MainCanvas.Invalidate();
    }

    private void FloatingImageVisibilityToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || !_editorState.SelectedFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
        if (image == null) return;

        var newVisibility = FloatingImageVisibilityToggle.IsOn;
        if (newVisibility == image.IsVisible) return;

        _editorState.Execute(new SetFloatingImageVisibilityCommand(page.Id, image.Id, newVisibility));
        MainCanvas.Invalidate();
    }

    private void FloatingImageLockToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFloatingImageProperties) return;

        var page = _editorState.Document?.ActivePage;
        if (page == null || !_editorState.SelectedFloatingImageId.HasValue) return;

        var image = page.FindFloatingImage(_editorState.SelectedFloatingImageId.Value);
        if (image == null) return;

        var newLocked = FloatingImageLockToggle.IsOn;
        if (newLocked == image.IsLocked) return;

        _editorState.Execute(new SetFloatingImageLockedCommand(page.Id, image.Id, newLocked));
        MainCanvas.Invalidate();
    }



    private void NumberBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is not NumberBox) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            MainCanvas.Focus(FocusState.Programmatic);
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            UpdatePropertiesPanel();
            UpdatePanelZonePropertiesPanel();
            MainCanvas.Focus(FocusState.Programmatic);
        }
    }


}

