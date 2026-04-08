using Letterist.Commands;
using Letterist.Model;
using Letterist.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Letterist;

public sealed partial class MainWindow : Window
{
    private Border? BalloonTemplateQuickPaletteBorder => null;
    private TextBlock? BalloonTemplateQuickPaletteTitleText => null;
    private TextBlock? BalloonTemplateQuickPaletteEmptyText => null;
    private StackPanel? BalloonTemplateQuickPaletteStrip => null;
    private ToggleButton? ToolbarBalloonTemplateEyedropperToggleButton => null;

    private const int MaxRecentBalloonTemplates = 12;
    private const int MaxToolbarQuickPaletteTemplates = 8;

    private BalloonTemplate? SelectedBalloonTemplate()
    {
        var doc = _editorState.Document;
        if (doc == null) return null;

        if (BalloonTemplatePresetComboBox?.SelectedItem is ComboBoxItem item && item.Tag is Guid templateId)
        {
            return doc.FindBalloonTemplate(templateId);
        }

        if (_selectedBalloonTemplateId.HasValue)
        {
            return doc.FindBalloonTemplate(_selectedBalloonTemplateId.Value);
        }

        return null;
    }

    private void RefreshBalloonTemplateControls()
    {
        if (BalloonTemplatePresetComboBox == null ||
            BalloonTemplateFavoriteToggleButton == null ||
            BalloonTemplateHotkeyComboBox == null)
        {
            return;
        }

        var doc = _editorState.Document;

        _isUpdatingBalloonTemplateUi = true;
        try
        {
            BalloonTemplatePresetComboBox.Items.Clear();

            if (doc == null)
            {
                _selectedBalloonTemplateId = null;
                _activeBalloonTemplateId = null;
                _recentBalloonTemplateIds.Clear();
                SetBalloonTemplateEyedropperMode(false, announceCancellation: false);
                BalloonTemplateFavoriteToggleButton.IsChecked = false;
                BalloonTemplateFavoriteToggleButton.IsEnabled = false;
                BalloonTemplateHotkeyComboBox.IsEnabled = false;
                SelectComboBoxItemByTag(BalloonTemplateHotkeyComboBox, "none");
                return;
            }

            if (_activeBalloonTemplateId.HasValue &&
                doc.FindBalloonTemplate(_activeBalloonTemplateId.Value) == null)
            {
                _activeBalloonTemplateId = null;
            }

            _recentBalloonTemplateIds.RemoveAll(id => doc.FindBalloonTemplate(id) == null);

            var templates = doc.BalloonTemplates
                .OrderByDescending(template => template.IsFavorite)
                .ThenBy(template => template.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var template in templates)
            {
                var favoritePrefix = template.IsFavorite ? "[Fav] " : string.Empty;
                var hotkeySuffix = template.HotkeySlot.HasValue
                    ? $" [Ctrl+{template.HotkeySlot.Value}]"
                    : string.Empty;
                var activeSuffix = _activeBalloonTemplateId == template.Id ? " [New]" : string.Empty;

                BalloonTemplatePresetComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{favoritePrefix}{template.Name} ({template.Category}){hotkeySuffix}{activeSuffix}",
                    Tag = template.Id
                });
            }

            var selectedTemplate = _selectedBalloonTemplateId.HasValue
                ? doc.FindBalloonTemplate(_selectedBalloonTemplateId.Value)
                : null;
            selectedTemplate ??= _activeBalloonTemplateId.HasValue
                ? doc.FindBalloonTemplate(_activeBalloonTemplateId.Value)
                : null;
            selectedTemplate ??= templates.FirstOrDefault();

            _selectedBalloonTemplateId = selectedTemplate?.Id;
            if (selectedTemplate != null)
            {
                SelectComboBoxItemByTag(BalloonTemplatePresetComboBox, selectedTemplate.Id.ToString());
            }
            else
            {
                BalloonTemplatePresetComboBox.SelectedIndex = -1;
            }

            BalloonTemplateFavoriteToggleButton.IsEnabled = selectedTemplate != null;
            BalloonTemplateFavoriteToggleButton.IsChecked = selectedTemplate?.IsFavorite ?? false;

            BalloonTemplateHotkeyComboBox.IsEnabled = selectedTemplate != null;
            var hotkeyTag = selectedTemplate?.HotkeySlot?.ToString(CultureInfo.InvariantCulture) ?? "none";
            if (!SelectComboBoxItemByTag(BalloonTemplateHotkeyComboBox, hotkeyTag))
            {
                SelectComboBoxItemByTag(BalloonTemplateHotkeyComboBox, "none");
            }
        }
        finally
        {
            _isUpdatingBalloonTemplateUi = false;
        }

        UpdateBalloonTemplateButtons();
        RefreshBalloonTemplateQuickPalette();
    }

    private void UpdateBalloonTemplateButtons()
    {
        var doc = _editorState.Document;
        var selectedTemplate = SelectedBalloonTemplate();
        var hasTemplate = selectedTemplate != null;
        var hasBalloonSelection = GetSelectedBalloons().Count > 0;
        var hasSourceBalloon = doc?.SelectedBalloon != null;

        if (ApplyBalloonTemplateButton != null)
        {
            ApplyBalloonTemplateButton.IsEnabled = hasTemplate && hasBalloonSelection;
        }

        if (UseBalloonTemplateForNewButton != null)
        {
            UseBalloonTemplateForNewButton.IsEnabled = hasTemplate;
            var isActive = hasTemplate && _activeBalloonTemplateId == selectedTemplate!.Id;
            UseBalloonTemplateForNewButton.Content = isActive
                ? L("balloon_template.button.use_for_new_active")
                : L("balloon_template.button.use_for_new");
            ToolTipService.SetToolTip(
                UseBalloonTemplateForNewButton,
                isActive
                    ? L("balloon_template.tooltip.use_for_new_active")
                    : L("balloon_template.tooltip.use_for_new"));
        }

        if (SaveBalloonTemplateButton != null)
        {
            SaveBalloonTemplateButton.IsEnabled = hasSourceBalloon;
        }

        if (UpdateBalloonTemplateButton != null)
        {
            UpdateBalloonTemplateButton.IsEnabled = hasTemplate && hasSourceBalloon;
        }

        if (RenameBalloonTemplateButton != null)
        {
            RenameBalloonTemplateButton.IsEnabled = hasTemplate;
        }

        if (DeleteBalloonTemplateButton != null)
        {
            DeleteBalloonTemplateButton.IsEnabled = hasTemplate &&
                (doc?.BalloonTemplates.Count ?? 0) > 1 &&
                !(selectedTemplate?.IsBuiltIn ?? false);
        }

        if (BalloonTemplateFavoriteToggleButton != null)
        {
            BalloonTemplateFavoriteToggleButton.IsEnabled = hasTemplate;
        }

        if (BalloonTemplateHotkeyComboBox != null)
        {
            BalloonTemplateHotkeyComboBox.IsEnabled = hasTemplate;
        }

        if (ImportBalloonTemplatePackButton != null)
        {
            ImportBalloonTemplatePackButton.IsEnabled = doc != null;
        }

        if (ExportBalloonTemplatePackButton != null)
        {
            ExportBalloonTemplatePackButton.IsEnabled = (doc?.BalloonTemplates.Count ?? 0) > 0;
        }

        var canUseEyedropper = doc != null;
        if (BalloonTemplateEyedropperToggleButton != null)
        {
            BalloonTemplateEyedropperToggleButton.IsEnabled = canUseEyedropper;
        }

        if (ToolbarBalloonTemplateEyedropperToggleButton != null)
        {
            ToolbarBalloonTemplateEyedropperToggleButton.IsEnabled = canUseEyedropper;
        }

        SyncBalloonTemplateEyedropperButtons();
    }

    private void BalloonTemplatePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingBalloonTemplateUi) return;

        _selectedBalloonTemplateId = BalloonTemplatePresetComboBox?.SelectedItem is ComboBoxItem item && item.Tag is Guid templateId
            ? templateId
            : null;

        UpdateBalloonTemplateButtons();
        RefreshBalloonTemplateQuickPalette();
    }

    private void ApplyBalloonTemplate_Click(object sender, RoutedEventArgs e)
    {
        var template = SelectedBalloonTemplate();
        if (template == null)
        {
            SetStatusMessage(L("balloon_template.error.select_template"));
            return;
        }

        if (!TryApplyTemplateToSelectedBalloons(template))
        {
            SetStatusMessage(L("balloon_template.error.select_balloons"));
            return;
        }

        RecordRecentBalloonTemplate(template.Id);
        RefreshBalloonTemplateControls();
    }

    private bool TryApplyTemplateToSelectedBalloons(BalloonTemplate template, string? statusSuffix = null)
    {
        var selectedBalloons = GetSelectedBalloons();
        if (selectedBalloons.Count == 0)
        {
            return false;
        }

        var commands = selectedBalloons
            .Select(balloon => new ApplyBalloonTemplateCommand(template.Id, balloon.Id, applyPlaceholderText: false, replaceTail: true))
            .Cast<ICommand>()
            .ToList();

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Apply balloon template", commands);
        }

        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
        SetStatusMessage(
            selectedBalloons.Count == 1
                ? $"{LF("balloon_template.status.applied_single", template.Name)}{statusSuffix ?? string.Empty}"
                : $"{LF("balloon_template.status.applied_multiple", template.Name, selectedBalloons.Count)}{statusSuffix ?? string.Empty}");
        return true;
    }

    private void UseBalloonTemplateForNewButton_Click(object sender, RoutedEventArgs e)
    {
        var template = SelectedBalloonTemplate();
        if (template == null)
        {
            SetStatusMessage(L("balloon_template.error.select_template"));
            return;
        }

        if (_activeBalloonTemplateId == template.Id)
        {
            _activeBalloonTemplateId = null;
            SetStatusMessage(L("balloon_template.status.template_cleared"));
        }
        else
        {
            _activeBalloonTemplateId = template.Id;
            RecordRecentBalloonTemplate(template.Id);
            SetStatusMessage(LF("balloon_template.status.will_use", template.Name));
        }

        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
    }

    private async void SaveBalloonTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        if (doc == null || balloon == null)
        {
            SetStatusMessage(L("balloon_template.error.select_balloon_save"));
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(balloon.Text) ? L("balloon_template.default_name") : balloon.Text.Trim();
        var name = await PromptStyleNameAsync(L("balloon_template.dialog.save_title"), L("props.label.name"), suggestedName);
        if (string.IsNullOrWhiteSpace(name)) return;

        var uniqueName = GetUniqueBalloonTemplateName(name, doc.BalloonTemplates);
        var command = new CreateBalloonTemplateCommand(
            balloon.Id,
            uniqueName,
            category: L("balloon_template.category.general"),
            placeholderText: balloon.Text);

        _editorState.Execute(command);
        _selectedBalloonTemplateId = command.CreatedTemplateId;
        RecordRecentBalloonTemplate(command.CreatedTemplateId);
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
        SetStatusMessage(LF("balloon_template.status.saved", uniqueName));
    }

    private void UpdateBalloonTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var balloon = doc?.SelectedBalloon;
        var template = SelectedBalloonTemplate();
        if (doc == null || balloon == null || template == null)
        {
            SetStatusMessage(L("balloon_template.error.select_template_source"));
            return;
        }

        var updatedTemplate = BalloonTemplate.CreateFromBalloon(
            balloon,
            template.Name,
            template.Description,
            template.Tags,
            template.Category,
            string.IsNullOrWhiteSpace(balloon.Text) ? template.PlaceholderText : balloon.Text,
            template.Id,
            template.IsFavorite,
            template.HotkeySlot,
            template.IsBuiltIn);

        _editorState.Execute(new UpdateBalloonTemplateCommand(template.Id, updatedTemplate));
        RecordRecentBalloonTemplate(template.Id);
        RefreshBalloonTemplateControls();
        SetStatusMessage(LF("balloon_template.status.updated", template.Name));
    }

    private async void RenameBalloonTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var template = SelectedBalloonTemplate();
        if (doc == null || template == null) return;

        var desiredName = await PromptStyleNameAsync(L("balloon_template.dialog.rename_title"), L("props.label.name"), template.Name);
        if (string.IsNullOrWhiteSpace(desiredName)) return;

        var uniqueName = GetUniqueBalloonTemplateName(desiredName, doc.BalloonTemplates, template.Id);
        if (string.Equals(template.Name, uniqueName, StringComparison.Ordinal)) return;

        var updated = template.Clone();
        updated.SetName(uniqueName);

        _editorState.Execute(new UpdateBalloonTemplateCommand(template.Id, updated));
        _selectedBalloonTemplateId = template.Id;
        RefreshBalloonTemplateControls();
        SetStatusMessage(LF("balloon_template.status.renamed", uniqueName));
    }

    private async void DeleteBalloonTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        var template = SelectedBalloonTemplate();
        if (doc == null || template == null) return;

        if (template.IsBuiltIn)
        {
            SetStatusMessage(L("balloon_template.error.builtin_delete"));
            return;
        }

        if (doc.BalloonTemplates.Count <= 1)
        {
            SetStatusMessage(L("balloon_template.error.minimum_required"));
            return;
        }

        if (!await ConfirmBalloonTemplateDeleteAsync(template.Name)) return;

        _editorState.Execute(new DeleteBalloonTemplateCommand(template.Id));
        if (_activeBalloonTemplateId == template.Id)
        {
            _activeBalloonTemplateId = null;
        }

        _recentBalloonTemplateIds.RemoveAll(id => id == template.Id);
        _selectedBalloonTemplateId = doc.BalloonTemplates.FirstOrDefault()?.Id;
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
        SetStatusMessage(LF("balloon_template.status.deleted", template.Name));
    }

    private void BalloonTemplateFavoriteToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingBalloonTemplateUi) return;
        if (sender is not ToggleButton toggle) return;

        if (TryUpdateSelectedBalloonTemplateFavoriteAndHotkey(toggle.IsChecked == true, null, L("balloon_template.status.favorite_updated")))
        {
            return;
        }

        RefreshBalloonTemplateControls();
    }

    private void BalloonTemplateHotkeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingBalloonTemplateUi) return;
        if (BalloonTemplateHotkeyComboBox?.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

        int? slot = null;
        if (!string.Equals(tag, "none", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSlot))
        {
            slot = parsedSlot;
        }

        if (TryUpdateSelectedBalloonTemplateFavoriteAndHotkey(null, slot, "Updated template hotkey."))
        {
            return;
        }

        RefreshBalloonTemplateControls();
    }

    private void BalloonTemplateEyedropperToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var shouldEnable = sender is ToggleButton toggle && toggle.IsChecked == true;
        SetBalloonTemplateEyedropperMode(shouldEnable);
    }

    private void ToolbarBalloonTemplateEyedropperToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var shouldEnable = sender is ToggleButton toggle && toggle.IsChecked == true;
        SetBalloonTemplateEyedropperMode(shouldEnable);
    }

    private void SetBalloonTemplateEyedropperMode(bool enabled, bool announceCancellation = true)
    {
        if (enabled && _editorState.Document == null)
        {
            enabled = false;
        }

        var changed = _isBalloonTemplateEyedropperActive != enabled;
        _isBalloonTemplateEyedropperActive = enabled;
        SyncBalloonTemplateEyedropperButtons();

        if (!changed)
        {
            return;
        }

        if (enabled)
        {
            SetStatusMessage(L("balloon_template.eyedropper.status_active"));
        }
        else if (announceCancellation)
        {
            SetStatusMessage(L("balloon_template.eyedropper.status_canceled"));
        }
    }

    private void SyncBalloonTemplateEyedropperButtons()
    {
        var isActive = _isBalloonTemplateEyedropperActive;
        var label = isActive ? L("balloon_template.eyedropper.button_active") : L("balloon_template.button.eyedropper");
        var toolbarLabel = isActive ? L("balloon_template.eyedropper.button_picking") : L("toolbar.style_pick");
        var tooltip = isActive
            ? L("balloon_template.eyedropper.tooltip_active")
            : L("balloon_template.eyedropper.tooltip_inactive");

        if (BalloonTemplateEyedropperToggleButton != null)
        {
            BalloonTemplateEyedropperToggleButton.IsChecked = isActive;
            BalloonTemplateEyedropperToggleButton.Content = label;
            ToolTipService.SetToolTip(BalloonTemplateEyedropperToggleButton, tooltip);
        }

        if (ToolbarBalloonTemplateEyedropperToggleButton != null)
        {
            ToolbarBalloonTemplateEyedropperToggleButton.IsChecked = isActive;
            ToolbarBalloonTemplateEyedropperToggleButton.Content = toolbarLabel;
            ToolTipService.SetToolTip(ToolbarBalloonTemplateEyedropperToggleButton, tooltip);
        }
    }

    private bool TryUpdateSelectedBalloonTemplateFavoriteAndHotkey(bool? favoriteOverride, int? hotkeySlotOverride, string statusMessage)
    {
        var doc = _editorState.Document;
        var template = SelectedBalloonTemplate();
        if (doc == null || template == null) return false;

        var targetHotkey = hotkeySlotOverride ?? template.HotkeySlot;
        targetHotkey = targetHotkey is >= 1 and <= 9 ? targetHotkey : null;

        var targetFavorite = favoriteOverride ?? template.IsFavorite;
        if (!targetFavorite)
        {
            targetHotkey = null;
        }
        else if (hotkeySlotOverride.HasValue)
        {
            targetFavorite = true;
        }

        var conflicts = targetHotkey.HasValue
            ? doc.BalloonTemplates.Where(item => item.Id != template.Id && item.HotkeySlot == targetHotkey).ToList()
            : new List<BalloonTemplate>();

        if (template.IsFavorite == targetFavorite && template.HotkeySlot == targetHotkey && conflicts.Count == 0)
        {
            return false;
        }

        var commands = new List<ICommand>();
        foreach (var conflict in conflicts)
        {
            var conflictUpdate = conflict.Clone();
            conflictUpdate.SetHotkeySlot(null);
            commands.Add(new UpdateBalloonTemplateCommand(conflict.Id, conflictUpdate));
        }

        var updated = template.Clone();
        if (!targetFavorite)
        {
            updated.SetFavorite(false);
        }
        else
        {
            updated.SetFavorite(true);
            updated.SetHotkeySlot(targetHotkey);
        }

        commands.Add(new UpdateBalloonTemplateCommand(template.Id, updated));

        if (commands.Count == 1)
        {
            _editorState.Execute(commands[0]);
        }
        else
        {
            _editorState.ExecuteTransaction("Update balloon template shortcuts", commands);
        }

        _selectedBalloonTemplateId = template.Id;
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
        SetStatusMessage(statusMessage);
        return true;
    }

    private async void ImportBalloonTemplatePackButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".letterist-balloonpack.json");
        picker.FileTypeFilter.Add(".json");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var loadedTemplates = await BalloonTemplatePackStorage.LoadAsync(file.Path);
            if (loadedTemplates.Count == 0)
            {
                SetStatusMessage(L("balloon_template.error.no_templates"));
                return;
            }

            var existingIds = new HashSet<Guid>(doc.BalloonTemplates.Select(template => template.Id));
            var existingNames = new HashSet<string>(doc.BalloonTemplates.Select(template => template.Name), StringComparer.OrdinalIgnoreCase);
            var usedHotkeys = new HashSet<int>(doc.BalloonTemplates
                .Where(template => template.HotkeySlot is >= 1 and <= 9)
                .Select(template => template.HotkeySlot!.Value));

            var commands = new List<ICommand>();
            Guid? firstImportedId = null;
            foreach (var template in loadedTemplates)
            {
                var imported = BuildImportedBalloonTemplate(template, existingIds, existingNames, usedHotkeys);
                commands.Add(new AddBalloonTemplateCommand(imported));
                firstImportedId ??= imported.Id;
            }

            if (commands.Count == 1)
            {
                _editorState.Execute(commands[0]);
            }
            else
            {
                _editorState.ExecuteTransaction("Import balloon templates", commands);
            }

            _selectedBalloonTemplateId = firstImportedId;
            if (firstImportedId.HasValue)
            {
                RecordRecentBalloonTemplate(firstImportedId.Value);
            }

            RefreshBalloonTemplateControls();
            UpdateToolButtonStates();
            SetStatusMessage(LF("balloon_template.status.imported", commands.Count));
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("balloon_template.error.import_failed", ex.Message));
        }
    }

    private async void ExportBalloonTemplatePackButton_Click(object sender, RoutedEventArgs e)
    {
        var doc = _editorState.Document;
        if (doc == null) return;
        if (doc.BalloonTemplates.Count == 0)
        {
            SetStatusMessage(L("balloon_template.error.no_templates_export"));
            return;
        }

        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Balloon Template Pack", new List<string> { ".letterist-balloonpack.json" });
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "balloon-templates";

        var hwnd = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file == null) return;

        await BalloonTemplatePackStorage.SaveAsync(doc.BalloonTemplates, file.Path);
        SetStatusMessage(LF("balloon_template.status.exported", doc.BalloonTemplates.Count));
    }

    private bool TryApplyBalloonTemplateHotkey(int slot)
    {
        var doc = _editorState.Document;
        if (doc == null || slot < 1 || slot > 9) return false;

        var template = doc.BalloonTemplates.FirstOrDefault(item => item.HotkeySlot == slot);
        if (template == null)
        {
            SetStatusMessage(LF("balloon_template.error.no_hotkey_format", slot));
            return false;
        }

        _selectedBalloonTemplateId = template.Id;
        RecordRecentBalloonTemplate(template.Id);

        if (!TryApplyTemplateToSelectedBalloons(template, LF("balloon_template.status.hotkey_suffix", slot)))
        {
            _activeBalloonTemplateId = template.Id;
            RefreshBalloonTemplateControls();
            UpdateToolButtonStates();
            SetStatusMessage(LF("balloon_template.status.selected_for_new", template.Name, slot));
            return true;
        }

        RefreshBalloonTemplateControls();
        return true;
    }

    private void RefreshBalloonTemplateQuickPalette()
    {
        if (BalloonTemplateQuickPaletteBorder == null ||
            BalloonTemplateQuickPaletteStrip == null ||
            BalloonTemplateQuickPaletteEmptyText == null)
        {
            return;
        }

        BalloonTemplateQuickPaletteStrip.Children.Clear();

        var doc = _editorState.Document;
        if (doc == null)
        {
            BalloonTemplateQuickPaletteBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var templates = GetToolbarQuickPaletteTemplates(doc);
        BalloonTemplateQuickPaletteBorder.Visibility = Visibility.Visible;

        if (templates.Count == 0)
        {
            BalloonTemplateQuickPaletteEmptyText.Visibility = Visibility.Visible;
            return;
        }

        BalloonTemplateQuickPaletteEmptyText.Visibility = Visibility.Collapsed;

        foreach (var template in templates)
        {
            var button = BuildBalloonTemplateQuickPaletteButton(template);
            BalloonTemplateQuickPaletteStrip.Children.Add(button);
        }
    }

    private IReadOnlyList<BalloonTemplate> GetToolbarQuickPaletteTemplates(Document doc)
    {
        var seen = new HashSet<Guid>();
        var ordered = new List<BalloonTemplate>();

        void AddTemplate(Guid? id)
        {
            if (!id.HasValue || ordered.Count >= MaxToolbarQuickPaletteTemplates) return;
            if (!seen.Add(id.Value)) return;
            var template = doc.FindBalloonTemplate(id.Value);
            if (template != null)
            {
                ordered.Add(template);
            }
        }

        foreach (var id in _recentBalloonTemplateIds)
        {
            AddTemplate(id);
        }

        AddTemplate(_activeBalloonTemplateId);
        AddTemplate(_selectedBalloonTemplateId);

        foreach (var template in doc.BalloonTemplates
                     .OrderByDescending(item => item.IsFavorite)
                     .ThenBy(item => item.HotkeySlot ?? int.MaxValue)
                     .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            AddTemplate(template.Id);
            if (ordered.Count >= MaxToolbarQuickPaletteTemplates)
            {
                break;
            }
        }

        return ordered;
    }

    private Button BuildBalloonTemplateQuickPaletteButton(BalloonTemplate template)
    {
        var isActive = _activeBalloonTemplateId == template.Id;
        var isSelected = _selectedBalloonTemplateId == template.Id;

        var button = new Button
        {
            Tag = template.Id,
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 68, 68, 68)),
            Background = new SolidColorBrush(isActive
                ? Microsoft.UI.ColorHelper.FromArgb(255, 55, 84, 55)
                : isSelected
                    ? Microsoft.UI.ColorHelper.FromArgb(255, 48, 66, 90)
                    : Microsoft.UI.ColorHelper.FromArgb(255, 45, 45, 45))
        };

        var label = template.Name;
        if (template.HotkeySlot.HasValue)
        {
            label = $"{label} [{template.HotkeySlot.Value}]";
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var fillSwatch = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30)),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(
                template.BalloonStyle.FillColor.A,
                template.BalloonStyle.FillColor.R,
                template.BalloonStyle.FillColor.G,
                template.BalloonStyle.FillColor.B)),
            VerticalAlignment = VerticalAlignment.Center
        };

        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        };

        content.Children.Add(fillSwatch);
        content.Children.Add(text);
        button.Content = content;

        var actionText = GetSelectedBalloons().Count > 0
            ? "Click to apply to selected balloons."
            : "Click to use for new balloons.";
        ToolTipService.SetToolTip(button, $"{template.Name} ({template.Category}). {actionText}");

        button.Click += BalloonTemplateQuickPaletteButton_Click;
        return button;
    }

    private void BalloonTemplateQuickPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid templateId) return;

        var doc = _editorState.Document;
        var template = doc?.FindBalloonTemplate(templateId);
        if (template == null) return;

        _selectedBalloonTemplateId = template.Id;
        RecordRecentBalloonTemplate(template.Id);

        if (!TryApplyTemplateToSelectedBalloons(template))
        {
            _activeBalloonTemplateId = template.Id;
            SetStatusMessage(LF("balloon_template.status.palette_selected", template.Name));
        }

        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
    }

    private async Task HandleBalloonTemplateEyedropperPickAsync(Point2 screenPos)
    {
        if (!_isBalloonTemplateEyedropperActive) return;

        var sourceBalloon = _editorState.HitTestBalloon(screenPos);
        if (sourceBalloon == null)
        {
            SetStatusMessage(L("balloon_template.eyedropper.status_no_target"));
            return;
        }

        SetBalloonTemplateEyedropperMode(false, announceCancellation: false);

        var selectedTargets = GetSelectedBalloons()
            .Where(balloon => balloon.Id != sourceBalloon.Id)
            .Select(balloon => balloon.Id)
            .Distinct()
            .ToList();

        if (selectedTargets.Count > 0)
        {
            var tempTemplateId = Guid.NewGuid();
            var commands = new List<ICommand>
            {
                new CreateBalloonTemplateCommand(
                    sourceBalloon.Id,
                    "_eyedropper",
                    category: L("balloon_template.category.eyedropper"),
                    placeholderText: sourceBalloon.Text,
                    templateId: tempTemplateId),
            };

            foreach (var targetId in selectedTargets)
            {
                commands.Add(new ApplyBalloonTemplateCommand(tempTemplateId, targetId, applyPlaceholderText: false, replaceTail: true));
            }

            commands.Add(new DeleteBalloonTemplateCommand(tempTemplateId));
            _editorState.ExecuteTransaction("Eyedropper apply balloon style", commands);
            UpdatePropertiesPanel();
            MainCanvas.Invalidate();
            SetStatusMessage(
                selectedTargets.Count == 1
                    ? "Sampled style and applied it to 1 balloon."
                    : $"Sampled style and applied it to {selectedTargets.Count} balloons.");
        }

        var savePrompt = selectedTargets.Count > 0
            ? "Save sampled style as a reusable balloon template?"
            : "No balloons were selected. Save sampled style as a template?";

        if (!await ConfirmSaveEyedropperTemplateAsync(savePrompt))
        {
            RefreshBalloonTemplateControls();
            return;
        }

        var doc = _editorState.Document;
        if (doc == null) return;

        var suggestedName = GetEyedropperTemplateSuggestedName(sourceBalloon);
        var templateName = await PromptStyleNameAsync(L("balloon_template.dialog.save_eyedropper_title"), L("props.label.name"), suggestedName);
        if (string.IsNullOrWhiteSpace(templateName))
        {
            RefreshBalloonTemplateControls();
            return;
        }

        var uniqueName = GetUniqueBalloonTemplateName(templateName, doc.BalloonTemplates);
        var createTemplate = new CreateBalloonTemplateCommand(
            sourceBalloon.Id,
            uniqueName,
            category: L("balloon_template.category.eyedropper"),
            placeholderText: sourceBalloon.Text);

        _editorState.Execute(createTemplate);
        _selectedBalloonTemplateId = createTemplate.CreatedTemplateId;
        RecordRecentBalloonTemplate(createTemplate.CreatedTemplateId);
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
        SetStatusMessage(LF("balloon_template.status.saved_eyedropper", uniqueName));
    }

    private async Task<bool> ConfirmSaveEyedropperTemplateAsync(string prompt)
    {
        var dialog = new ContentDialog
        {
            Title = L("templates.dialog.eyedropper"),
            Content = new TextBlock
            {
                Text = prompt,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = L("templates.dialog.save_template"),
            CloseButtonText = L("templates.dialog.skip"),
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string GetEyedropperTemplateSuggestedName(Balloon sourceBalloon)
    {
        var text = sourceBalloon.Text?.Trim() ?? string.Empty;
        if (text.Length > 28)
        {
            text = $"{text[..28].TrimEnd()}...";
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return sourceBalloon.Shape switch
        {
            BalloonShape.Burst => "Burst Style",
            BalloonShape.Thought => "Thought Style",
            BalloonShape.Splat => "Splat Style",
            BalloonShape.Rectangle => "Rectangle Style",
            BalloonShape.Whisper => "Whisper Style",
            BalloonShape.Custom => "Custom Shape Style",
            _ => "Dialogue Style"
        };
    }

    private void RecordRecentBalloonTemplate(Guid templateId)
    {
        _recentBalloonTemplateIds.RemoveAll(id => id == templateId);
        _recentBalloonTemplateIds.Insert(0, templateId);
        if (_recentBalloonTemplateIds.Count > MaxRecentBalloonTemplates)
        {
            _recentBalloonTemplateIds.RemoveRange(MaxRecentBalloonTemplates, _recentBalloonTemplateIds.Count - MaxRecentBalloonTemplates);
        }

        _lastUsedBalloonTemplateId = templateId;

        RefreshBalloonTemplateQuickPalette();
        RefreshBalloonTemplateGallery();
    }

    private Guid? _lastUsedBalloonTemplateId;

    private void RefreshBalloonTemplateGallery()
    {
        if (BalloonTemplateGallery == null) return;

        var doc = _editorState.Document;
        BalloonTemplateGallery.ItemsSource = null;

        UpdateLastUsedTemplateDisplay(doc);

        if (doc == null || doc.BalloonTemplates.Count == 0)
        {
            return;
        }

        var templates = doc.BalloonTemplates
            .OrderByDescending(t => t.IsFavorite)
            .ThenBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<FrameworkElement>();
        string? currentCategory = null;

        foreach (var template in templates)
        {
            if (!string.Equals(template.Category, currentCategory, StringComparison.OrdinalIgnoreCase))
            {
                currentCategory = template.Category;
                if (items.Count > 0)
                {
                    items.Add(new Border { Height = 1, Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 45, 45, 45)), Margin = new Thickness(0, 4, 0, 4) });
                }
                items.Add(new TextBlock
                {
                    Text = currentCategory ?? "General",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 136, 136, 136)),
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            items.Add(BuildBalloonTemplateGalleryItem(template));
        }

        BalloonTemplateGallery.ItemsSource = items;
    }

    private void UpdateLastUsedTemplateDisplay(Document? doc)
    {
        if (LastUsedTemplateBorder == null || NoLastUsedTemplateText == null) return;

        BalloonTemplate? lastUsed = null;
        if (_lastUsedBalloonTemplateId.HasValue && doc != null)
        {
            lastUsed = doc.FindBalloonTemplate(_lastUsedBalloonTemplateId.Value);
        }

        if (lastUsed == null)
        {
            LastUsedTemplateBorder.Visibility = Visibility.Collapsed;
            NoLastUsedTemplateText.Visibility = Visibility.Visible;
            return;
        }

        LastUsedTemplateBorder.Visibility = Visibility.Visible;
        NoLastUsedTemplateText.Visibility = Visibility.Collapsed;

        if (LastUsedTemplateNameText != null) LastUsedTemplateNameText.Text = lastUsed.Name;
        if (LastUsedTemplateCategoryText != null) LastUsedTemplateCategoryText.Text = lastUsed.Category;

        if (LastUsedTemplatePreview != null)
        {
            var fillColor = lastUsed.BalloonStyle.FillColor;
            var strokeColor = lastUsed.BalloonStyle.StrokeColor;
            LastUsedTemplatePreview.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(fillColor.A, fillColor.R, fillColor.G, fillColor.B));
            LastUsedTemplatePreview.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(strokeColor.A, strokeColor.R, strokeColor.G, strokeColor.B));
        }
    }

    private FrameworkElement BuildBalloonTemplateGalleryItem(BalloonTemplate template)
    {
        var isActive = _activeBalloonTemplateId == template.Id;
        var isSelected = _selectedBalloonTemplateId == template.Id;

        var fillColor = template.BalloonStyle.FillColor;
        var strokeColor = template.BalloonStyle.StrokeColor;
        var textColor = template.TextStyle.TextColor;

        var shapeText = template.Shape switch
        {
            BalloonShape.Oval => "\u2B2D",        // Oval
            BalloonShape.Rectangle => "\u25AD",     // Rectangle
            BalloonShape.RoundedRect => "\u25AD",    // Rounded rect
            BalloonShape.Thought => "\u2601",       // Cloud
            BalloonShape.Splat => "\u2738",         // Sparkle/splat
            BalloonShape.Burst => "\u2605",         // Star
            BalloonShape.Whisper => "\u2026",       // Ellipsis
            BalloonShape.Custom => "\u2B1F",        // Pentagon
            _ => "\u2B2D"
        };

        var previewBorder = new Border
        {
            Width = 48,
            Height = 40,
            CornerRadius = template.Shape == BalloonShape.Rectangle ? new CornerRadius(2) : new CornerRadius(8),
            BorderThickness = new Thickness(Math.Max(1, Math.Min(3, template.BalloonStyle.StrokeWidth))),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(strokeColor.A, strokeColor.R, strokeColor.G, strokeColor.B)),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(fillColor.A, fillColor.R, fillColor.G, fillColor.B)),
            Child = new TextBlock
            {
                Text = shapeText,
                FontSize = 16,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(textColor.A, textColor.R, textColor.G, textColor.B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (template.IsFavorite)
        {
            nameRow.Children.Add(new FontIcon { Glyph = "\uE735", FontSize = 10, Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 200, 50)) });
        }
        nameRow.Children.Add(new TextBlock
        {
            Text = template.Name,
            FontSize = 12,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 221, 221, 221)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 160
        });
        namePanel.Children.Add(nameRow);

        var infoText = template.Shape.ToString();
        if (template.HotkeySlot.HasValue) infoText += $" \u2022 Ctrl+{template.HotkeySlot.Value}";
        if (isActive) infoText += " \u2022 Active";

        namePanel.Children.Add(new TextBlock
        {
            Text = infoText,
            FontSize = 10,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 136, 136, 136)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(previewBorder, 0);
        Grid.SetColumn(namePanel, 1);
        grid.Children.Add(previewBorder);
        grid.Children.Add(namePanel);

        var button = new Button
        {
            Tag = template.Id,
            Content = grid,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            BorderBrush = new SolidColorBrush(isSelected
                ? Microsoft.UI.ColorHelper.FromArgb(255, 80, 120, 200)
                : Microsoft.UI.ColorHelper.FromArgb(255, 60, 60, 60)),
            Background = new SolidColorBrush(isActive
                ? Microsoft.UI.ColorHelper.FromArgb(255, 55, 84, 55)
                : Microsoft.UI.ColorHelper.FromArgb(255, 42, 42, 42))
        };

        ToolTipService.SetToolTip(button, $"{template.Name} ({template.Category})\nShape: {template.Shape}\n{(template.IsFavorite ? "Favorite" : "")}{(template.HotkeySlot.HasValue ? $"\nHotkey: Ctrl+{template.HotkeySlot.Value}" : "")}");

        button.Click += BalloonTemplateGalleryItem_Click;
        return button;
    }

    private void BalloonTemplateGalleryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid templateId) return;

        var doc = _editorState.Document;
        var template = doc?.FindBalloonTemplate(templateId);
        if (template == null) return;

        _selectedBalloonTemplateId = template.Id;

        if (BalloonTemplatePresetComboBox != null)
        {
            _isUpdatingBalloonTemplateUi = true;
            SelectComboBoxItemByTag(BalloonTemplatePresetComboBox, template.Id.ToString());
            _isUpdatingBalloonTemplateUi = false;
        }

        if (!TryApplyTemplateToSelectedBalloons(template))
        {
            _activeBalloonTemplateId = template.Id;
            SetStatusMessage(LF("balloon_template.status.palette_selected", template.Name));
        }

        RecordRecentBalloonTemplate(template.Id);
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
    }

    private static string GetUniqueBalloonTemplateName(string baseName, IEnumerable<BalloonTemplate> templates, Guid? excludeId = null)
    {
        var existingNames = new HashSet<string>(
            templates
                .Where(template => !excludeId.HasValue || template.Id != excludeId.Value)
                .Select(template => template.Name),
            StringComparer.OrdinalIgnoreCase);

        return GetUniqueBalloonTemplateName(baseName, existingNames);
    }

    private static string GetUniqueBalloonTemplateName(string baseName, ISet<string> existingNames)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseName) ? "Balloon Template" : baseName.Trim();
        if (!existingNames.Contains(trimmed))
        {
            return trimmed;
        }

        var index = 2;
        while (existingNames.Contains($"{trimmed} {index}"))
        {
            index++;
        }

        return $"{trimmed} {index}";
    }

    private static BalloonTemplate BuildImportedBalloonTemplate(
        BalloonTemplate source,
        HashSet<Guid> existingIds,
        ISet<string> existingNames,
        HashSet<int> usedHotkeys)
    {
        var id = source.Id;
        if (!existingIds.Add(id))
        {
            id = Guid.NewGuid();
            existingIds.Add(id);
        }

        var uniqueName = GetUniqueBalloonTemplateName(source.Name, existingNames);
        existingNames.Add(uniqueName);

        var slot = source.HotkeySlot;
        if (slot is < 1 or > 9 || (slot.HasValue && !usedHotkeys.Add(slot.Value)))
        {
            slot = null;
        }

        return new BalloonTemplate(
            id,
            uniqueName,
            source.Shape,
            source.BalloonStyle,
            source.TextStyle,
            source.PlaceholderText,
            source.Tail?.Clone(),
            source.CustomShapePathData,
            source.BalloonStyleId,
            source.BalloonStyleOverrides.Clone(),
            source.TextStyleId,
            source.TextStyleOverrides.Clone(),
            source.Description,
            source.Tags,
            source.Category,
            source.IsFavorite,
            slot,
            source.IsBuiltIn);
    }

    private async Task<bool> ConfirmBalloonTemplateDeleteAsync(string templateName)
    {
        var dialog = new ContentDialog
        {
            Title = L("templates.dialog.delete_template"),
            Content = $"Delete \"{templateName}\"?",
            PrimaryButtonText = L("common.delete"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
