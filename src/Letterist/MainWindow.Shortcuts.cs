using Letterist.Model;
using Letterist.View;
using Windows.System;

namespace Letterist;

public sealed partial class MainWindow
{
    private readonly record struct ShortcutGesture(VirtualKey Key, bool Ctrl, bool Shift, bool Alt);

    private void RefreshShortcutBindings()
    {
        _shortcutGestures.Clear();

        var defaults = KeyboardShortcutsPreferences.CreateDefaultBindings();
        foreach (var defaultBinding in defaults)
        {
            var binding = defaultBinding.Value;
            if (_preferences.KeyboardShortcuts.Bindings.TryGetValue(defaultBinding.Key, out var configured) &&
                !string.IsNullOrWhiteSpace(configured))
            {
                binding = configured;
            }

            if (!TryParseShortcutGesture(binding, out var gesture) &&
                !TryParseShortcutGesture(defaultBinding.Value, out gesture))
            {
                continue;
            }

            _shortcutGestures[defaultBinding.Key] = gesture;
        }
    }

    private bool IsShortcutPressed(string command, VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        if (!_shortcutGestures.TryGetValue(command, out var gesture))
        {
            return false;
        }

        return gesture.Ctrl == ctrl &&
               gesture.Shift == shift &&
               gesture.Alt == alt &&
               KeysMatch(gesture.Key, key);
    }

    private static bool KeysMatch(VirtualKey expected, VirtualKey actual)
    {
        if (expected == actual)
        {
            return true;
        }

        return expected switch
        {
            VirtualKey.Number0 => actual == VirtualKey.NumberPad0,
            VirtualKey.Number1 => actual == VirtualKey.NumberPad1,
            VirtualKey.Number2 => actual == VirtualKey.NumberPad2,
            VirtualKey.Number3 => actual == VirtualKey.NumberPad3,
            VirtualKey.Number4 => actual == VirtualKey.NumberPad4,
            VirtualKey.Number5 => actual == VirtualKey.NumberPad5,
            VirtualKey.Number6 => actual == VirtualKey.NumberPad6,
            VirtualKey.Number7 => actual == VirtualKey.NumberPad7,
            VirtualKey.Number8 => actual == VirtualKey.NumberPad8,
            VirtualKey.Number9 => actual == VirtualKey.NumberPad9,
            VirtualKey.Add => actual == (VirtualKey)187,
            VirtualKey.Subtract => actual == (VirtualKey)189,
            (VirtualKey)187 => actual == VirtualKey.Add,
            (VirtualKey)189 => actual == VirtualKey.Subtract,
            _ => false
        };
    }

    private static bool TryParseShortcutGesture(string text, out ShortcutGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var ctrl = false;
        var shift = false;
        var alt = false;
        VirtualKey? key = null;

        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (part.Equals("alt", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("menu", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (key.HasValue || !TryParseShortcutKey(part, out var parsedKey))
            {
                return false;
            }

            key = parsedKey;
        }

        if (!key.HasValue)
        {
            return false;
        }

        gesture = new ShortcutGesture(key.Value, ctrl, shift, alt);
        return true;
    }

    private static bool TryParseShortcutKey(string token, out VirtualKey key)
    {
        key = VirtualKey.None;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var text = token.Trim();
        if (text.Length == 1)
        {
            var c = text[0];
            if (c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z')
            {
                key = (VirtualKey)char.ToUpperInvariant(c);
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                key = (VirtualKey)((int)VirtualKey.Number0 + (c - '0'));
                return true;
            }

            if (c == ',')
            {
                key = (VirtualKey)188;
                return true;
            }

            if (c == '.')
            {
                key = (VirtualKey)190;
                return true;
            }
        }

        if (text.StartsWith("f", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[1..], out var fn) &&
            fn >= 1 && fn <= 24)
        {
            key = (VirtualKey)((int)VirtualKey.F1 + (fn - 1));
            return true;
        }

        switch (text.ToLowerInvariant())
        {
            case "tab":
                key = VirtualKey.Tab;
                return true;
            case "enter":
            case "return":
                key = VirtualKey.Enter;
                return true;
            case "escape":
            case "esc":
                key = VirtualKey.Escape;
                return true;
            case "delete":
            case "del":
                key = VirtualKey.Delete;
                return true;
            case "space":
                key = VirtualKey.Space;
                return true;
            case "plus":
            case "add":
                key = VirtualKey.Add;
                return true;
            case "minus":
            case "subtract":
                key = VirtualKey.Subtract;
                return true;
            case "comma":
                key = (VirtualKey)188;
                return true;
            case "period":
            case "dot":
                key = (VirtualKey)190;
                return true;
            default:
                if (Enum.TryParse<VirtualKey>(text, ignoreCase: true, out var parsed))
                {
                    key = parsed;
                    return true;
                }

                return false;
        }
    }

    private bool TryHandleConfiguredRootShortcut(VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        if (IsShortcutPressed("Preferences", key, ctrl, shift, alt))
        {
            _ = ShowPreferencesDialogAsync();
            return true;
        }

        if (_editorState.Mode == EditorMode.EditText)
        {
            return false;
        }

        if (IsShortcutPressed("Toggle Fullscreen Canvas", key, ctrl, shift, alt))
        {
            ToggleCanvasFullscreen();
            return true;
        }

        if (IsShortcutPressed("Toggle Grid", key, ctrl, shift, alt))
        {
            ToggleGridVisibility();
            return true;
        }

        if (IsShortcutPressed("Toggle Snap to Grid", key, ctrl, shift, alt))
        {
            ToggleSnapToGrid();
            return true;
        }

        if (IsShortcutPressed("Add Page", key, ctrl, shift, alt))
        {
            AddPage_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
            return true;
        }

        if (IsShortcutPressed("Add Horizontal Guide", key, ctrl, shift, alt))
        {
            AddHorizontalGuide_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
            return true;
        }

        if (IsShortcutPressed("Add Vertical Guide", key, ctrl, shift, alt))
        {
            AddVerticalGuide_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
            return true;
        }

        if (IsShortcutPressed("Create Balloon", key, ctrl, shift, alt) && _editorState.Mode != EditorMode.PanelLayout)
        {
            AddBalloonAtCursorOrViewportCenter();
            return true;
        }

        if (IsShortcutPressed("Select Tool", key, ctrl, shift, alt))
        {
            _editorState.Mode = EditorMode.Select;
            UpdateToolButtonStates();
            return true;
        }

        if (IsShortcutPressed("Toggle Panel Layout", key, ctrl, shift, alt))
        {
            TogglePanelLayoutMode();
            return true;
        }

        return false;
    }

    private bool TryHandleConfiguredCanvasShortcut(VirtualKey key, bool ctrl, bool shift, bool alt)
    {
        if (IsShortcutPressed("Preferences", key, ctrl, shift, alt))
        {
            _ = ShowPreferencesDialogAsync();
            return true;
        }

        if (_editorState.Mode == EditorMode.EditText)
        {
            return false;
        }

        if (IsShortcutPressed("Undo", key, ctrl, shift, alt))
        {
            UndoCommandAndRefreshVisuals();
            return true;
        }

        if (IsShortcutPressed("Redo", key, ctrl, shift, alt))
        {
            RedoCommandAndRefreshVisuals();
            return true;
        }

        if (IsShortcutPressed("Select All", key, ctrl, shift, alt))
        {
            if (_editorState.Mode == EditorMode.PanelLayout)
            {
                SelectAllPanelsOnActivePage();
            }
            else
            {
                SelectAllBalloonsOnActiveLayer();
            }

            return true;
        }

        if (IsShortcutPressed("Copy", key, ctrl, shift, alt))
        {
            if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelIds.Count > 0)
            {
                CopySelectedPanelsToClipboard();
            }
            else
            {
                CopySelectedBalloonsToClipboard();
            }
            return true;
        }

        if (IsShortcutPressed("Cut", key, ctrl, shift, alt))
        {
            if (_editorState.Mode == EditorMode.PanelLayout && _editorState.SelectedPanelIds.Count > 0)
            {
                CutSelectedPanelsToClipboard();
            }
            else
            {
                CutSelectedBalloonsToClipboard();
            }
            return true;
        }

        if (IsShortcutPressed("Paste", key, ctrl, shift, alt))
        {
            if (_editorState.Mode == EditorMode.PanelLayout)
            {
                _ = PastePanelsFromClipboardAsync();
            }
            else
            {
                _ = PasteBalloonsFromClipboardAsync();
            }
            return true;
        }

        if (IsShortcutPressed("Duplicate", key, ctrl, shift, alt))
        {
            TryDuplicateFromCurrentContext();
            return true;
        }

        if (IsShortcutPressed("Zoom to Selection", key, ctrl, shift, alt))
        {
            ZoomToSelection();
            return true;
        }

        if (IsShortcutPressed("Zoom 100%", key, ctrl, shift, alt))
        {
            _editorState.ViewTransform.ZoomTo100();
            return true;
        }

        if (IsShortcutPressed("Toggle Fullscreen Canvas", key, ctrl, shift, alt))
        {
            ToggleCanvasFullscreen();
            return true;
        }

        if (IsShortcutPressed("Toggle Grid", key, ctrl, shift, alt))
        {
            ToggleGridVisibility();
            return true;
        }

        if (IsShortcutPressed("Toggle Snap to Grid", key, ctrl, shift, alt))
        {
            ToggleSnapToGrid();
            return true;
        }

        if (IsShortcutPressed("Toggle Panel Layout", key, ctrl, shift, alt))
        {
            TogglePanelLayoutMode();
            return true;
        }

        if (IsShortcutPressed("Select Tool", key, ctrl, shift, alt))
        {
            _editorState.Mode = EditorMode.Select;
            UpdateToolButtonStates();
            return true;
        }

        if (_editorState.Mode != EditorMode.PanelLayout)
        {
            if (IsShortcutPressed("Create Balloon", key, ctrl, shift, alt))
            {
                AddBalloonAtCursorOrViewportCenter();
                return true;
            }

            if (IsShortcutPressed("Toggle Tail", key, ctrl, shift, alt))
            {
                ToggleTailOnSelectedBalloon();
                return true;
            }

            if (IsShortcutPressed("Add Image", key, ctrl, shift, alt))
            {
                ImportFloatingImage_Click(this, new Microsoft.UI.Xaml.RoutedEventArgs());
                return true;
            }
        }

        return false;
    }
}
