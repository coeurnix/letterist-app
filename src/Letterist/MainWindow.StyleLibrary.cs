using Letterist.Commands;
using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Letterist;

public sealed partial class MainWindow : Window
{
    private enum StyleImportMode
    {
        Merge,
        Replace
    }

    private void ImportStyleLibrary_Click(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log("Style library import clicked");
        _ = RunAfterMenuAsync(ImportStyleLibraryAsync, L("style_library.label.import"));
    }

    private void ExportStyleLibrary_Click(object sender, RoutedEventArgs e)
    {
        StartupLogger.Log("Style library export clicked");
        _ = RunAfterMenuAsync(ExportStyleLibraryAsync, L("style_library.label.export"));
    }

    private async Task ExportStyleLibraryAsync()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        SetStatusMessage(L("style_library.status.opening_export"));

        string? filePath;
        try
        {
            filePath = await PickStyleLibrarySavePathAsync();
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("style_library.error.export_failed", ex.Message));
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetStatusMessage(L("style_library.status.export_canceled"));
            return;
        }

        try
        {
            await StyleLibraryStorage.SaveAsync(doc, filePath);
            SetStatusMessage(LF("style_library.status.exported", Path.GetFileName(filePath)));
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("style_library.error.export_failed", ex.Message));
        }
    }

    private async Task ImportStyleLibraryAsync()
    {
        var doc = _editorState.Document;
        if (doc == null) return;

        SetStatusMessage(L("style_library.status.opening_import"));

        string? filePath;
        try
        {
            filePath = await PickStyleLibraryOpenPathAsync();
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("style_library.error.import_failed", ex.Message));
            return;
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            SetStatusMessage(L("style_library.status.import_canceled"));
            return;
        }

        StyleLibraryFile library;
        try
        {
            library = await StyleLibraryStorage.LoadAsync(filePath);
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("style_library.error.import_failed", ex.Message));
            return;
        }

        var balloonCount = library.BalloonStyles?.Count ?? 0;
        var textCount = library.TextStyles?.Count ?? 0;
        if (balloonCount == 0 && textCount == 0)
        {
            SetStatusMessage(L("style_library.status.import_skipped"));
            return;
        }

        var mode = await PromptStyleImportModeAsync(balloonCount, textCount);
        if (mode == null) return;

        var commands = BuildStyleImportCommands(doc, library, mode.Value);
        if (commands.Count == 0)
        {
            SetStatusMessage(L("style_library.status.import_no_changes"));
            return;
        }

        try
        {
            _editorState.ExecuteTransactionSafe("Import style library", commands);
        }
        catch (Exception ex)
        {
            SetStatusMessage(LF("style_library.error.import_failed", ex.Message));
            return;
        }

        RefreshStylePresets();
        UpdatePropertiesPanel();
        SetStatusMessage(LF("style_library.status.imported_format", balloonCount, textCount));
    }

    private async Task RunAfterMenuAsync(Func<Task> action, string label)
    {
        var tcs = new TaskCompletionSource<object?>();
        var enqueued = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            try
            {
                await Task.Delay(80);
                await action();
            }
            catch (Exception ex)
            {
                SetStatusMessage(LF("style_library.error.action_failed", label, ex.Message));
            }
            finally
            {
                tcs.TrySetResult(null);
            }
        });

        if (!enqueued)
        {
            await action();
            return;
        }

        await tcs.Task;
    }

    private async Task<StyleImportMode?> PromptStyleImportModeAsync(int balloonCount, int textCount)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = LF("style_library.dialog.found_styles", balloonCount, textCount),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray)
        });
        panel.Children.Add(new TextBlock
        {
            Text = L("styles.dialog.import_hint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
        });

        var dialog = new ContentDialog
        {
            Title = L("styles.dialog.import_title"),
            Content = panel,
            PrimaryButtonText = L("common.merge"),
            SecondaryButtonText = L("styles.dialog.replace"),
            CloseButtonText = L("common.cancel"),
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => StyleImportMode.Merge,
            ContentDialogResult.Secondary => StyleImportMode.Replace,
            _ => null
        };
    }

    private static List<ICommand> BuildStyleImportCommands(Document doc, StyleLibraryFile library, StyleImportMode mode)
    {
        var commands = new List<ICommand>();
        var balloonImports = library.BalloonStyles ?? new List<NamedBalloonStyleFile>();
        var textImports = library.TextStyles ?? new List<NamedTextStyleFile>();

        var existingBalloonById = doc.BalloonStyles.ToDictionary(style => style.Id);
        var existingTextById = doc.TextStyles.ToDictionary(style => style.Id);
        var balloonNames = new HashSet<string>(doc.BalloonStyles.Select(style => style.Name), StringComparer.OrdinalIgnoreCase);
        var textNames = new HashSet<string>(doc.TextStyles.Select(style => style.Name), StringComparer.OrdinalIgnoreCase);

        if (mode == StyleImportMode.Replace)
        {
            if (balloonImports.Count > 0)
            {
                foreach (var style in doc.BalloonStyles.ToList())
                {
                    commands.Add(new DeleteNamedBalloonStyleCommand(style.Id));
                }

                existingBalloonById.Clear();
                balloonNames.Clear();
            }

            if (textImports.Count > 0)
            {
                foreach (var style in doc.TextStyles.ToList())
                {
                    commands.Add(new DeleteNamedTextStyleCommand(style.Id));
                }

                existingTextById.Clear();
                textNames.Clear();
            }
        }

        var knownBalloonIds = new HashSet<Guid>(existingBalloonById.Keys);
        foreach (var fileStyle in balloonImports)
        {
            var style = fileStyle.ToStyle();
            if (knownBalloonIds.Contains(style.Id))
            {
                if (existingBalloonById.TryGetValue(style.Id, out var existing))
                {
                    if (!AreEquivalent(existing, style))
                    {
                        commands.Add(new UpdateNamedBalloonStyleCommand(
                            existing.Id,
                            style.Style,
                            applyExtendedDetails: style.ApplyExtendedDetails,
                            shape: style.Shape,
                            customShapePathData: style.CustomShapePathData,
                            constrainToPanel: style.ConstrainToPanel,
                            textStyle: style.TextStyle,
                            textPath: style.TextPath?.Clone(),
                            tails: style.Tails.Select(tail => tail.Clone())));
                    }

                    if (existing.IsQuickSelect != style.IsQuickSelect)
                    {
                        commands.Add(new SetNamedBalloonStyleQuickSelectCommand(existing.Id, style.IsQuickSelect));
                    }

                    if (!string.Equals(existing.Name, style.Name, StringComparison.Ordinal))
                    {
                        balloonNames.Remove(existing.Name);
                        var uniqueName = GetUniqueStyleName(style.Name, balloonNames, "Balloon Style");
                        if (!string.Equals(existing.Name, uniqueName, StringComparison.Ordinal))
                        {
                            commands.Add(new RenameNamedBalloonStyleCommand(existing.Id, uniqueName));
                        }
                    }
                }
                continue;
            }

            var name = GetUniqueStyleName(style.Name, balloonNames, "Balloon Style");
            commands.Add(new CreateNamedBalloonStyleCommand(
                name,
                style.Style,
                style.Id,
                style.ParentStyleId,
                style.Overrides.Clone(),
                isQuickSelect: style.IsQuickSelect,
                applyExtendedDetails: style.ApplyExtendedDetails,
                shape: style.Shape,
                customShapePathData: style.CustomShapePathData,
                constrainToPanel: style.ConstrainToPanel,
                textStyle: style.TextStyle,
                textPath: style.TextPath?.Clone(),
                tails: style.Tails.Select(tail => tail.Clone())));
            knownBalloonIds.Add(style.Id);
        }

        var knownTextIds = new HashSet<Guid>(existingTextById.Keys);
        foreach (var fileStyle in textImports)
        {
            var style = fileStyle.ToStyle();
            if (knownTextIds.Contains(style.Id))
            {
                if (existingTextById.TryGetValue(style.Id, out var existing))
                {
                    if (!TextStyleUtilities.AreEquivalent(existing.Style, style.Style))
                    {
                        commands.Add(new UpdateNamedTextStyleCommand(existing.Id, style.Style));
                    }

                    if (!string.Equals(existing.Name, style.Name, StringComparison.Ordinal))
                    {
                        textNames.Remove(existing.Name);
                        var uniqueName = GetUniqueStyleName(style.Name, textNames, "Text Style");
                        if (!string.Equals(existing.Name, uniqueName, StringComparison.Ordinal))
                        {
                            commands.Add(new RenameNamedTextStyleCommand(existing.Id, uniqueName));
                        }
                    }
                }
                continue;
            }

            var name = GetUniqueStyleName(style.Name, textNames, "Text Style");
            commands.Add(new CreateNamedTextStyleCommand(name, style.Style, style.Id));
            knownTextIds.Add(style.Id);
        }

        return commands;
    }

    private static bool AreEquivalent(NamedBalloonStyle left, NamedBalloonStyle right)
    {
        if (!BalloonStyleUtilities.AreEquivalent(left.Style, right.Style))
        {
            return false;
        }

        if (left.ApplyExtendedDetails != right.ApplyExtendedDetails)
        {
            return false;
        }

        if (left.Shape != right.Shape)
        {
            return false;
        }

        if (!string.Equals(left.CustomShapePathData ?? string.Empty, right.CustomShapePathData ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.ConstrainToPanel != right.ConstrainToPanel)
        {
            return false;
        }

        if (!TextStyleUtilities.AreEquivalent(left.TextStyle, right.TextStyle))
        {
            return false;
        }

        if ((left.TextPath == null) != (right.TextPath == null))
        {
            return false;
        }

        if (left.TextPath != null && right.TextPath != null && !left.TextPath.Equals(right.TextPath))
        {
            return false;
        }

        if (left.Tails.Count != right.Tails.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Tails.Count; i++)
        {
            if (!AreEquivalent(left.Tails[i], right.Tails[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEquivalent(BalloonTemplateTail left, BalloonTemplateTail right)
    {
        return left.TargetOffset.Equals(right.TargetOffset)
            && left.Style == right.Style
            && Math.Abs(left.BaseWidth - right.BaseWidth) < 0.01f
            && Nullable.Equals(left.AttachmentDirection, right.AttachmentDirection)
            && Nullable.Equals(left.ControlPointOffset, right.ControlPointOffset)
            && Math.Abs(left.Curvature - right.Curvature) < 0.01f;
    }

    private static string GetUniqueStyleName(string? desired, HashSet<string> existingNames, string fallback)
    {
        var baseName = string.IsNullOrWhiteSpace(desired) ? fallback : desired.Trim();
        var candidate = baseName;
        var index = 2;
        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName} ({index})";
            index++;
        }

        existingNames.Add(candidate);
        return candidate;
    }

    private async Task<string?> PickStyleLibraryOpenPathAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".styles");
        picker.FileTypeFilter.Add(".json");
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        StartupLogger.Log($"Style library open picker hwnd=0x{hwnd.ToInt64():X}");
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickStyleLibrarySavePathAsync()
    {
        var picker = new FileSavePicker();
        picker.FileTypeChoices.Add("Letterist Style Library", new List<string> { ".styles" });
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        picker.SuggestedFileName = "style-library";
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        var hwnd = WindowNative.GetWindowHandle(this);
        StartupLogger.Log($"Style library save picker hwnd=0x{hwnd.ToInt64():X}");
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
