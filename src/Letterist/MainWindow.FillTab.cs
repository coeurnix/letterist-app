using Letterist.Commands;
using Letterist.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Letterist;

public sealed partial class MainWindow
{

    private async void BalloonTextColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;
        if (BalloonTextColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.TextStyle.TextColor);
            if (!customColor.HasValue)
            {
                RefreshFillTabControls(balloon);
                return;
            }
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        ApplyInlineTextStyle(style => style.With(textColor: color));
        RefreshFillTabControls(balloon);
    }



    private void BalloonTextFillTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (BalloonTextFillTypeComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (!Enum.TryParse<TextFillType>(tag, out var fillType)) return;

        UpdateFillModeUi(fillType);
        ApplyInlineTextStyle(style => style.With(fillType: fillType));
    }

    private async void BalloonFillSecondaryColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;
        if (BalloonFillSecondaryColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.TextStyle.FillSecondaryColor);
            if (!customColor.HasValue)
            {
                RefreshFillTabControls(balloon);
                return;
            }
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        ApplyInlineTextStyle(style => style.With(fillSecondaryColor: color));
    }

    private void BalloonTextFillAngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        var angle = (float)Math.Clamp(args.NewValue, -360d, 360d);
        BalloonTextFillAngleValueText.Text = $"{angle:F0}°";
        ApplyInlineTextStyle(style => style.With(fillAngle: angle));
    }

    private void BalloonTextFillPatternComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (BalloonTextFillPatternComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (!Enum.TryParse<TextFillPattern>(tag, out var pattern)) return;

        ApplyInlineTextStyle(style => style.With(fillPattern: pattern));
    }

    private void BalloonTextFillPatternScaleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;

        var scale = Math.Clamp((float)args.NewValue / 100f, 0.25f, 8f);
        BalloonTextFillPatternScaleValueText.Text = $"{scale:F2}x";
        ApplyInlineTextStyle(style => style.With(fillPatternScale: scale));
    }

    private async void BalloonTextFillImageBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        _textFillBitmapFailures.Remove(file.Path);
        ApplyInlineTextStyle(style => style.With(fillType: TextFillType.Image, fillImagePath: file.Path));
    }

    private void BalloonTextFillImageClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        ApplyInlineTextStyle(style => style.With(fillImagePath: string.Empty));
    }



    private async void BalloonOutlineColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;
        if (BalloonOutlineColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.TextStyle.OutlineColor);
            if (!customColor.HasValue)
            {
                RefreshFillTabControls(balloon);
                return;
            }
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        ApplyInlineTextStyle(style => style.With(outlineColor: color));
    }

    private void BalloonOutlineWidthSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        var width = Math.Clamp((float)args.NewValue, 0f, 32f);
        BalloonOutlineWidthValueText.Text = $"{width:F1}";
        SetOutlineToggleStateFromWidth(width);
        ApplyInlineTextStyle(style => style.With(outlineWidth: width));
    }

    private void BalloonOutlineWidthPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (BalloonOutlineWidthPresetComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
        if (!float.TryParse(tag, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var width)) return;

        width = Math.Clamp(width, 0f, 32f);
        SetOutlineToggleStateFromWidth(width);
        ApplyInlineTextStyle(style => style.With(outlineWidth: width));
    }

    private void BalloonOutlineToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var isEnabled = BalloonOutlineToggle.IsOn;
        BalloonOutlineOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

        ApplyInlineTextStyle(style =>
        {
            var currentWidth = Math.Max(0f, style.OutlineWidth);
            var outlineWidth = isEnabled
                ? (currentWidth > 0.01f ? currentWidth : 2f)
                : 0f;
            return style.With(outlineWidth: outlineWidth);
        });
    }

    private void SetOutlineToggleStateFromWidth(float width)
    {
        var isEnabled = width > 0.01f;
        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        try
        {
            BalloonOutlineToggle.IsOn = isEnabled;
            BalloonOutlineOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _isUpdatingProperties = wasUpdating;
        }
    }



    private void UpdateFillModeUi(TextFillType fillType)
    {
        var usesSecondaryColor = fillType is TextFillType.Linear or TextFillType.Radial or TextFillType.Pattern;
        var usesAngle = fillType is TextFillType.Linear or TextFillType.Radial or TextFillType.Pattern;
        var usesPattern = fillType == TextFillType.Pattern;
        var usesImage = fillType == TextFillType.Image;
        var isSolid = fillType == TextFillType.Solid;

        BalloonFillModeHintText.Visibility = isSolid ? Visibility.Collapsed : Visibility.Visible;
        BalloonFillSecondaryColorPanel.Visibility = usesSecondaryColor ? Visibility.Visible : Visibility.Collapsed;
        BalloonFillAnglePanel.Visibility = usesAngle ? Visibility.Visible : Visibility.Collapsed;
        BalloonFillPatternPanel.Visibility = usesPattern ? Visibility.Visible : Visibility.Collapsed;
        BalloonFillPatternScalePanel.Visibility = usesPattern ? Visibility.Visible : Visibility.Collapsed;
        BalloonFillImagePanel.Visibility = usesImage ? Visibility.Visible : Visibility.Collapsed;

        BalloonFillModeHintText.Text = fillType switch
        {
            TextFillType.Linear => L("fill.hint.gradient"),
            TextFillType.Radial => L("fill.hint.gradient"),
            TextFillType.Pattern => L("fill.hint.pattern"),
            TextFillType.Image => L("fill.hint.image"),
            _ => string.Empty
        };
    }

    private void RefreshFillTabControls(Balloon balloon)
    {
        var wasUpdating = _isUpdatingProperties;
        _isUpdatingProperties = true;
        try
        {
            var textStyle = GetActiveTextStyleForProperties(balloon);

            UpdateColorSelector(BalloonTextColorPreview, BalloonTextColorComboBox, textStyle.TextColor);

            SelectComboBoxItemByTag(BalloonTextFillTypeComboBox, textStyle.FillType.ToString());
            UpdateFillModeUi(textStyle.FillType);
            UpdateColorSelector(BalloonFillSecondaryColorPreview, BalloonFillSecondaryColorComboBox, textStyle.FillSecondaryColor);
            BalloonTextFillAngleSlider.Value = textStyle.FillAngle;
            BalloonTextFillAngleValueText.Text = $"{textStyle.FillAngle:F0}°";
            SelectComboBoxItemByTag(BalloonTextFillPatternComboBox, textStyle.FillPattern.ToString());
            BalloonTextFillPatternScaleSlider.Value = textStyle.FillPatternScale * 100f;
            BalloonTextFillPatternScaleValueText.Text = $"{textStyle.FillPatternScale:F2}x";
            BalloonTextFillImagePathBox.Text = textStyle.FillImagePath ?? "";

            UpdateColorSelector(BalloonOutlineColorPreview, BalloonOutlineColorComboBox, textStyle.OutlineColor);
            var outlineWidth = Math.Max(0f, textStyle.OutlineWidth);
            BalloonOutlineToggle.IsOn = outlineWidth > 0.01f;
            BalloonOutlineOptionsPanel.Visibility = outlineWidth > 0.01f ? Visibility.Visible : Visibility.Collapsed;
            BalloonOutlineWidthSlider.Value = outlineWidth;
            BalloonOutlineWidthValueText.Text = $"{outlineWidth:F1}";
        }
        finally
        {
            _isUpdatingProperties = wasUpdating;
        }
    }

    private void UpdateColorSelector(Border preview, ComboBox comboBox, Color color)
    {
        if (preview != null)
        {
            preview.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        if (!SelectComboBoxItemByTag(comboBox, hex))
        {
            SelectComboBoxItemByTag(comboBox, "custom");
        }
    }

}
