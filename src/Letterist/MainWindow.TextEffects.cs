using Letterist.Commands;
using Letterist.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Letterist;

public sealed partial class MainWindow
{

    private void RefreshTextEffectsControls(Balloon balloon)
    {
        var textStyle = GetActiveTextStyleForProperties(balloon);

        RefreshTextShadowControls(textStyle);

        RefreshTextGlowControls(textStyle);

        RefreshTextExtrusionControls(textStyle);

        RefreshTextMotionBlurControls(textStyle);
    }



    private void RefreshTextShadowControls(TextStyle style)
    {
        if (BalloonTextShadowToggle == null) return; // Controls not yet loaded

        var shadow = GetPrimaryShadow(style);
        var isEnabled = style.Shadows.Count > 0 && shadow.Opacity > 0.001f;

        BalloonTextShadowToggle.IsOn = isEnabled;
        BalloonTextShadowOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

        UpdateColorSelector(BalloonTextShadow1ColorPreview, BalloonTextShadow1ColorComboBox, shadow.Color);
        BalloonTextShadow1OffsetXSlider.Value = shadow.OffsetX;
        BalloonTextShadow1OffsetYSlider.Value = shadow.OffsetY;
        BalloonTextShadow1BlurSlider.Value = shadow.Blur;
        BalloonTextShadow1OpacitySlider.Value = shadow.Opacity * 100f;

        BalloonTextShadow1OffsetXValueText.Text = $"{shadow.OffsetX:F1}";
        BalloonTextShadow1OffsetYValueText.Text = $"{shadow.OffsetY:F1}";
        BalloonTextShadow1BlurValueText.Text = $"{shadow.Blur:F1}";
        BalloonTextShadow1OpacityValueText.Text = $"{shadow.Opacity * 100f:F0}%";
    }

    private void BalloonTextShadowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;

        var isEnabled = BalloonTextShadowToggle.IsOn;
        BalloonTextShadowOptionsPanel.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;

        ApplyInlineTextStyle(style =>
        {
            if (!isEnabled)
            {
                return style.With(shadows: Array.Empty<TextShadow>());
            }

            var shadow = GetPrimaryShadow(style);
            if (shadow.Opacity <= 0.001f)
            {
                shadow = new TextShadow
                {
                    Color = shadow.Color,
                    OffsetX = Math.Abs(shadow.OffsetX) < 0.01f ? 2f : shadow.OffsetX,
                    OffsetY = Math.Abs(shadow.OffsetY) < 0.01f ? 2f : shadow.OffsetY,
                    Blur = shadow.Blur,
                    Opacity = 0.45f
                };
            }

            return style.With(shadows: new List<TextShadow> { shadow });
        });
    }

    private async void BalloonTextShadowColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (BalloonTextShadow1ColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var currentColor = GetPrimaryShadow(balloon.TextStyle).Color;
            var customColor = await ShowColorPickerDialogAsync(currentColor);
            if (!customColor.HasValue) return;
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        ApplyInlineTextStyle(style => style.With(shadows: BuildSingleShadowUpdate(style, color: color)));
    }

    private void BalloonTextShadowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        if (sender is not Slider slider || slider.Tag is not string field) return;

        var value = (float)args.NewValue;
        UpdateShadowValueText(field, value);

        ApplyInlineTextStyle(style => field switch
        {
            "offsetX" => style.With(shadows: BuildSingleShadowUpdate(style, offsetX: value)),
            "offsetY" => style.With(shadows: BuildSingleShadowUpdate(style, offsetY: value)),
            "blur" => style.With(shadows: BuildSingleShadowUpdate(style, blur: value)),
            "opacity" => style.With(shadows: BuildSingleShadowUpdate(style, opacity: value / 100f)),
            _ => style
        });
    }

    private void UpdateShadowValueText(string field, float value)
    {
        switch (field)
        {
            case "offsetX":
                BalloonTextShadow1OffsetXValueText.Text = $"{value:F1}";
                break;
            case "offsetY":
                BalloonTextShadow1OffsetYValueText.Text = $"{value:F1}";
                break;
            case "blur":
                BalloonTextShadow1BlurValueText.Text = $"{value:F1}";
                break;
            case "opacity":
                BalloonTextShadow1OpacityValueText.Text = $"{value:F0}%";
                break;
        }
    }

    private static TextShadow GetPrimaryShadow(TextStyle style)
    {
        if (style.Shadows.Count > 0)
        {
            return style.Shadows[0];
        }

        return TextShadow.Default;
    }

    private static List<TextShadow> BuildSingleShadowUpdate(
        TextStyle style,
        Color? color = null,
        float? offsetX = null,
        float? offsetY = null,
        float? blur = null,
        float? opacity = null)
    {
        var current = GetPrimaryShadow(style);
        var updated = new TextShadow
        {
            Color = color ?? current.Color,
            OffsetX = offsetX ?? current.OffsetX,
            OffsetY = offsetY ?? current.OffsetY,
            Blur = blur ?? current.Blur,
            Opacity = opacity ?? current.Opacity
        };

        return new List<TextShadow> { updated };
    }



    private void RefreshTextGlowControls(TextStyle style)
    {
        if (BalloonTextOuterGlowToggle == null) return;

        BalloonTextOuterGlowToggle.IsOn = style.OuterGlowEnabled;
        BalloonTextOuterGlowOptionsPanel.Visibility = style.OuterGlowEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (style.OuterGlowEnabled)
        {
            UpdateColorSelector(BalloonTextOuterGlowColorPreview, BalloonTextOuterGlowColorComboBox, style.OuterGlowColor);
            BalloonTextOuterGlowSizeSlider.Value = style.OuterGlowSize;
            BalloonTextOuterGlowOpacitySlider.Value = style.OuterGlowOpacity * 100f;
            BalloonTextOuterGlowSizeValueText.Text = $"{style.OuterGlowSize:F1}";
            BalloonTextOuterGlowOpacityValueText.Text = $"{style.OuterGlowOpacity * 100f:F0}%";
        }

        BalloonTextInnerGlowToggle.IsOn = style.InnerGlowEnabled;
        BalloonTextInnerGlowOptionsPanel.Visibility = style.InnerGlowEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (style.InnerGlowEnabled)
        {
            UpdateColorSelector(BalloonTextInnerGlowColorPreview, BalloonTextInnerGlowColorComboBox, style.InnerGlowColor);
            BalloonTextInnerGlowSizeSlider.Value = style.InnerGlowSize;
            BalloonTextInnerGlowOpacitySlider.Value = style.InnerGlowOpacity * 100f;
            BalloonTextInnerGlowSizeValueText.Text = $"{style.InnerGlowSize:F1}";
            BalloonTextInnerGlowOpacityValueText.Text = $"{style.InnerGlowOpacity * 100f:F0}%";
        }
    }

    private void BalloonTextGlowToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (sender is not ToggleSwitch toggle || toggle.Tag is not string tag) return;

        var isOn = toggle.IsOn;
        if (tag == "outerGlow")
        {
            BalloonTextOuterGlowOptionsPanel.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
            ApplyInlineTextStyle(style => style.With(outerGlowEnabled: isOn));
        }
        else if (tag == "innerGlow")
        {
            BalloonTextInnerGlowOptionsPanel.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
            ApplyInlineTextStyle(style => style.With(innerGlowEnabled: isOn));
        }
    }

    private async void BalloonTextGlowColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (sender is not ComboBox comboBox) return;
        if (comboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;
        var glowType = comboBox.Tag as string;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var currentColor = glowType == "outerGlow" ? balloon.TextStyle.OuterGlowColor : balloon.TextStyle.InnerGlowColor;
            var customColor = await ShowColorPickerDialogAsync(currentColor);
            if (!customColor.HasValue) return;
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        if (glowType == "outerGlow")
            ApplyInlineTextStyle(style => style.With(outerGlowColor: color));
        else
            ApplyInlineTextStyle(style => style.With(innerGlowColor: color));
    }

    private void BalloonTextGlowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        if (sender is not Slider slider || slider.Tag is not string tagStr) return;

        var parts = tagStr.Split('|');
        if (parts.Length != 2) return;
        var glowType = parts[0];
        var field = parts[1];
        var value = (float)args.NewValue;
        var isOpacity = field == "opacity";

        if (glowType == "outerGlow")
        {
            if (field == "size") BalloonTextOuterGlowSizeValueText.Text = $"{value:F1}";
            if (isOpacity) BalloonTextOuterGlowOpacityValueText.Text = $"{value:F0}%";
        }
        else
        {
            if (field == "size") BalloonTextInnerGlowSizeValueText.Text = $"{value:F1}";
            if (isOpacity) BalloonTextInnerGlowOpacityValueText.Text = $"{value:F0}%";
        }

        ApplyInlineTextStyle(style =>
        {
            if (glowType == "outerGlow")
            {
                return field switch
                {
                    "size" => style.With(outerGlowSize: value),
                    "opacity" => style.With(outerGlowOpacity: value / 100f),
                    _ => style
                };
            }
            else
            {
                return field switch
                {
                    "size" => style.With(innerGlowSize: value),
                    "opacity" => style.With(innerGlowOpacity: value / 100f),
                    _ => style
                };
            }
        });
    }



    private void RefreshTextExtrusionControls(TextStyle style)
    {
        if (BalloonTextExtrusionToggle == null) return;

        BalloonTextExtrusionToggle.IsOn = style.ExtrusionEnabled;
        BalloonTextExtrusionOptionsPanel.Visibility = style.ExtrusionEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (style.ExtrusionEnabled)
        {
            UpdateColorSelector(BalloonTextExtrusionColorPreview, BalloonTextExtrusionColorComboBox, style.ExtrusionColor);
            BalloonTextExtrusionDepthSlider.Value = style.ExtrusionDepth;
            BalloonTextExtrusionAngleSlider.Value = style.ExtrusionAngle;
            BalloonTextExtrusionOpacitySlider.Value = style.ExtrusionOpacity * 100f;
            BalloonTextExtrusionDepthValueText.Text = $"{style.ExtrusionDepth:F1}";
            BalloonTextExtrusionAngleValueText.Text = $"{style.ExtrusionAngle:F0}°";
            BalloonTextExtrusionOpacityValueText.Text = $"{style.ExtrusionOpacity * 100f:F0}%";
        }
    }

    private void BalloonTextExtrusionToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        var isOn = BalloonTextExtrusionToggle.IsOn;
        BalloonTextExtrusionOptionsPanel.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        ApplyInlineTextStyle(style => style.With(extrusionEnabled: isOn));
    }

    private async void BalloonTextExtrusionColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        if (BalloonTextExtrusionColorComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        var balloon = _editorState.Document?.SelectedBalloon;
        if (balloon == null) return;

        Color color;
        if (string.Equals(tag, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var customColor = await ShowColorPickerDialogAsync(balloon.TextStyle.ExtrusionColor);
            if (!customColor.HasValue) return;
            color = customColor.Value;
        }
        else
        {
            color = ParseHexColor(tag);
        }

        ApplyInlineTextStyle(style => style.With(extrusionColor: color));
    }

    private void BalloonTextExtrusionSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        if (sender is not Slider slider || slider.Tag is not string tagStr) return;

        var parts = tagStr.Split('|');
        if (parts.Length != 2) return;
        var field = parts[1];
        var value = (float)args.NewValue;

        if (field == "depth") BalloonTextExtrusionDepthValueText.Text = $"{value:F1}";
        if (field == "angle") BalloonTextExtrusionAngleValueText.Text = $"{value:F0}°";
        if (field == "opacity") BalloonTextExtrusionOpacityValueText.Text = $"{value:F0}%";

        ApplyInlineTextStyle(style => field switch
        {
            "depth" => style.With(extrusionDepth: value),
            "angle" => style.With(extrusionAngle: value),
            "opacity" => style.With(extrusionOpacity: value / 100f),
            _ => style
        });
    }



    private void RefreshTextMotionBlurControls(TextStyle style)
    {
        if (BalloonTextMotionBlurToggle == null) return;

        BalloonTextMotionBlurToggle.IsOn = style.MotionBlurEnabled;
        BalloonTextMotionBlurOptionsPanel.Visibility = style.MotionBlurEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (style.MotionBlurEnabled)
        {
            BalloonTextMotionBlurDistanceSlider.Value = style.MotionBlurDistance;
            BalloonTextMotionBlurAngleSlider.Value = style.MotionBlurAngle;
            BalloonTextMotionBlurOpacitySlider.Value = style.MotionBlurOpacity * 100f;
            BalloonTextMotionBlurDistanceValueText.Text = $"{style.MotionBlurDistance:F1}";
            BalloonTextMotionBlurAngleValueText.Text = $"{style.MotionBlurAngle:F0}°";
            BalloonTextMotionBlurOpacityValueText.Text = $"{style.MotionBlurOpacity * 100f:F0}%";
        }
    }

    private void BalloonTextMotionBlurToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingProperties) return;
        var isOn = BalloonTextMotionBlurToggle.IsOn;
        BalloonTextMotionBlurOptionsPanel.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed;
        ApplyInlineTextStyle(style => style.With(motionBlurEnabled: isOn));
    }

    private void BalloonTextMotionBlurSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs args)
    {
        if (_isUpdatingProperties || double.IsNaN(args.NewValue)) return;
        if (sender is not Slider slider || slider.Tag is not string tagStr) return;

        var parts = tagStr.Split('|');
        if (parts.Length != 2) return;
        var field = parts[1];
        var value = (float)args.NewValue;

        if (field == "distance") BalloonTextMotionBlurDistanceValueText.Text = $"{value:F1}";
        if (field == "angle") BalloonTextMotionBlurAngleValueText.Text = $"{value:F0}°";
        if (field == "opacity") BalloonTextMotionBlurOpacityValueText.Text = $"{value:F0}%";

        ApplyInlineTextStyle(style => field switch
        {
            "distance" => style.With(motionBlurDistance: value),
            "angle" => style.With(motionBlurAngle: value),
            "opacity" => style.With(motionBlurOpacity: value / 100f),
            _ => style
        });
    }

}
