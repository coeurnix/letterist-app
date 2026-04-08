using Letterist.Commands;
using Letterist.Diagnostics;
using Letterist.Model;
using Letterist.Publishing;
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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Linq;
using System.Numerics;
using System.Globalization;
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

    private void StartAutomationServer()
    {
        _serverCts = new CancellationTokenSource();
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://localhost:{App.AutomationPort}/");

        try
        {
            _httpListener.Start();
            _ = Task.Run(() => ListenForRequests(_serverCts.Token));

            DispatcherQueue.TryEnqueue(() =>
            {
                AutomationStatus.Text = $"Automation: localhost:{App.AutomationPort}";
            });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AutomationStatus.Text = $"Automation failed: {ex.Message}";
            });
        }
    }

    private async Task ListenForRequests(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener?.IsListening == true)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            object? result;
            if (TryParseGuidFromPath(path, "/state/balloon/", out var balloonId))
            {
                result = await HandleBalloonStateRequest(balloonId);
            }
            else if (TryParseGuidFromPath(path, "/screenshot/balloon/", out var screenshotBalloonId))
            {
                result = await HandleScreenshotBalloonRequest(response, screenshotBalloonId);
            }
            else if (TryParseGuidFromPath(path, "/screenshot/layer/", out var layerId))
            {
                result = await HandleScreenshotLayerRequest(response, layerId);
            }
            else
            {
                result = path switch
                {
                    "/state" => await HandleStateRequest(),
                    "/screenshot" => await HandleScreenshotRequest(response),
                    "/screenshot-ui" => await HandleScreenshotUiRequest(response),
                    "/commands" => await HandleCommandsRequest(request),
                    "/commands/transaction" => await HandleCommandsTransactionRequest(request),
                    "/publishing/preflight" => await HandlePublishingPreflightRequest(request),
                    "/publishing/preflight/fix" => await HandlePublishingPreflightFixRequest(request),
                    "/publishing/web-presets" => HandlePublishingWebPresetsRequest(request),
                    "/publishing/distribution" => HandlePublishingDistributionRequest(request),
                    "/printing/plan" => HandlePrintingPlanRequest(request),
                    "/printing/preview" => await HandlePrintPreviewRequest(response, request),
                    "/translations/export" => await HandleTranslationsExportRequest(request),
                    "/translations/import" => await HandleTranslationsImportRequest(request),
                    "/translations/qa" => await HandleTranslationsQaRequest(request),
                    "/undo" => await HandleUndoRequest(),
                    "/redo" => await HandleRedoRequest(),
                    _ => new { success = false, error = $"Unknown endpoint: {path}" }
                };
            }

            if (result != null)
            {
                await WriteJsonResponse(response, result);
            }
        }
        catch (Exception ex)
        {
            await WriteJsonResponse(response, new { success = false, error = ex.Message });
        }
        finally
        {
            response.Close();
        }
    }

    private static bool TryParseGuidFromPath(string path, string prefix, out Guid id)
    {
        id = Guid.Empty;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        var raw = path.Substring(prefix.Length).Trim('/');
        return Guid.TryParse(raw, out id);
    }

    private Task<object> HandleStateRequest()
    {
        var doc = _editorState.Document;

        if (doc == null)
        {
            return Task.FromResult<object>(new
            {
                success = true,
                data = new
                {
                    documentLoaded = false,
                    pages = Array.Empty<object>(),
                    layers = Array.Empty<object>(),
                    balloons = Array.Empty<object>(),
                    balloonTemplates = Array.Empty<object>(),
                    selectedBalloonTemplateId = (Guid?)null,
                    activeBalloonTemplateId = (Guid?)null,
                    recentBalloonTemplateIds = Array.Empty<Guid>(),
                    quickBalloonTemplates = Array.Empty<object>(),
                    balloonTemplateEyedropperActive = false,
                    translation = new
                    {
                        baseLanguage = "en",
                        activeLanguage = "en",
                        compareMode = TranslationCompareMode.None.ToString(),
                        compareLanguage = (string?)null,
                        highlightUntranslated = true,
                        knownLanguages = new[] { "en" },
                        exportVisibility = Array.Empty<object>(),
                        languageLayouts = Array.Empty<object>()
                    },
                    selection = (object?)null
                }
            });
        }

        var pages = doc.Pages.Select((p, index) => new
        {
            id = p.Id,
            name = p.Name,
            index,
            width = p.Size.Width,
            height = p.Size.Height,
            backgroundImagePath = p.BackgroundImagePath,
            backgroundImageFitMode = p.BackgroundImageFitMode.ToString(),
            readingDirection = p.ReadingDirection.ToString(),
            panelGutterWidth = p.PanelGutterWidth,
            panelGutterColor = p.PanelGutterColor.ToString(),
            panelGutterStrokeStyle = p.PanelGutterStrokeStyle.ToString(),
            panelGutterFillEnabled = p.PanelGutterFillEnabled,
            guidesLocked = p.GuidesLocked,
            layerCount = p.Layers.Count,
            balloonCount = p.AllBalloons.Count()
        }).ToArray();

        var pageTemplates = doc.PageTemplates.Select(template => new
        {
            id = template.Id,
            name = template.Name,
            width = template.Size.Width,
            height = template.Size.Height,
            layerCount = template.Layers.Count,
            guideCount = template.Guides.Count
        }).ToArray();

        var layers = doc.Layers.Select(l => new
        {
            id = l.Id,
            name = l.Name,
            visible = l.IsVisible,
            locked = l.IsLocked,
            opacity = l.Opacity,
            blendMode = l.BlendMode.ToString(),
            kind = l.Kind.ToString(),
            imagePath = l.ImagePath,
            balloonCount = l.Balloons.Count
        }).ToArray();

        var activePage = doc.ActivePage;

        var balloons = doc.AllBalloons.Select(b => new
        {
            id = b.Id,
            layerId = b.LayerId,
            panelId = b.PanelId,
            constrainToPanel = b.ConstrainToPanel,
            visible = b.IsVisible,
            locked = b.IsLocked,
            readingOrder = activePage?.GetBalloonReadingOrder(b),
            x = b.Position.X,
            y = b.Position.Y,
            width = b.ComputedSize.Width,
            height = b.ComputedSize.Height,
            shape = b.Shape.ToString(),
            text = b.Text,
            activeLanguageText = doc.GetBalloonDisplayText(b),
            untranslated = doc.IsBalloonUntranslated(b),
            staleTranslation = doc.IsBalloonTranslationStale(b, doc.ActiveLanguage),
            translations = b.Translations
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new
                {
                    language = pair.Key,
                    text = pair.Value.Text,
                    sourceTextSnapshot = pair.Value.SourceTextSnapshot,
                    updatedUtc = pair.Value.UpdatedUtc,
                    orientation = pair.Value.Orientation.ToString()
                })
                .ToArray(),
            balloonStyleId = b.BalloonStyleId,
            textStyleId = b.TextStyleId,
            textPath = b.TextPath,
            hasTail = b.Tail != null,
            tail = b.Tail != null ? new
            {
                id = b.Tail.Id,
                targetX = b.Tail.TargetPoint.X,
                targetY = b.Tail.TargetPoint.Y,
                style = b.Tail.Style.ToString(),
                baseWidth = b.Tail.BaseWidth
            } : null
        }).ToArray();

        var balloonTemplates = doc.BalloonTemplates.Select(template => new
        {
            id = template.Id,
            name = template.Name,
            description = template.Description,
            category = template.Category,
            tags = template.Tags.ToArray(),
            placeholderText = template.PlaceholderText,
            shape = template.Shape.ToString(),
            hasTail = template.Tail != null,
            isFavorite = template.IsFavorite,
            hotkeySlot = template.HotkeySlot,
            isBuiltIn = template.IsBuiltIn
        }).ToArray();

        var quickBalloonTemplates = GetToolbarQuickPaletteTemplates(doc)
            .Select((template, index) => new
            {
                index,
                id = template.Id,
                name = template.Name,
                category = template.Category,
                hotkeySlot = template.HotkeySlot,
                isActive = _activeBalloonTemplateId == template.Id,
                isSelected = _selectedBalloonTemplateId == template.Id
            })
            .ToArray();

        var links = doc.ActivePage?.BalloonLinks.Select(link => new
        {
            balloonAId = link.FirstId,
            balloonBId = link.SecondId
        }).ToArray() ?? Array.Empty<object>();

        var panels = doc.ActivePage?.Panels.Select(panel => new
        {
            id = panel.Id,
            name = panel.Name,
            x = panel.Bounds.X,
            y = panel.Bounds.Y,
            width = panel.Bounds.Width,
            height = panel.Bounds.Height,
            order = panel.Order,
            color = panel.Color.ToString(),
            visible = panel.IsVisible,
            locked = panel.IsLocked,
            safeMargin = panel.SafeMargin,
            gutterLeftOverride = panel.GutterLeftOverride,
            gutterTopOverride = panel.GutterTopOverride,
            gutterRightOverride = panel.GutterRightOverride,
            gutterBottomOverride = panel.GutterBottomOverride,
            bleedLeft = panel.BleedLeft,
            bleedTop = panel.BleedTop,
            bleedRight = panel.BleedRight,
            bleedBottom = panel.BleedBottom
        }).ToArray() ?? Array.Empty<object>();

        var floatingImages = doc.ActivePage?.FloatingImages.Select(image => new
        {
            id = image.Id,
            layerId = image.LayerId,
            panelId = image.PanelId,
            name = image.Name,
            source = image.Source,
            x = image.Bounds.X,
            y = image.Bounds.Y,
            width = image.Bounds.Width,
            height = image.Bounds.Height,
            imagePath = image.ImagePath,
            rotation = image.Rotation,
            opacity = image.Opacity,
            visible = image.IsVisible,
            locked = image.IsLocked,
            constrainToPanel = image.ConstrainToPanel,
            shadowEnabled = image.ShadowEnabled,
            shadowColor = image.ShadowColor.ToString(),
            shadowOpacity = image.ShadowOpacity,
            shadowOffsetX = image.ShadowOffsetX,
            shadowOffsetY = image.ShadowOffsetY,
            shadowFalloff = image.ShadowFalloff,
            glowEnabled = image.GlowEnabled,
            glowColor = image.GlowColor.ToString(),
            glowOpacity = image.GlowOpacity,
            glowSize = image.GlowSize
        }).ToArray() ?? Array.Empty<object>();

        var objectGroups = doc.ActivePage?.ObjectGroups.Select(group => new
        {
            id = group.Id,
            balloonIds = group.BalloonIds.ToArray(),
            floatingImageIds = group.FloatingImageIds.ToArray()
        }).ToArray() ?? Array.Empty<object>();

        return Task.FromResult<object>(new
            {
                success = true,
                data = new
                {
                    documentLoaded = true,
                    name = doc.Name,
                    width = doc.Size.Width,
                    height = doc.Size.Height,
                    defaultPageWidth = doc.DefaultPageSize.Width,
                    defaultPageHeight = doc.DefaultPageSize.Height,
                    defaultPageBackgroundColor = doc.DefaultPageBackgroundColor?.ToString(),
                    defaultPageBackgroundImagePath = doc.DefaultPageBackgroundImagePath,
                    activePageId = doc.ActivePageId,
                    activeLayerId = doc.ActiveLayerId,
                    translation = new
                    {
                        baseLanguage = doc.BaseLanguage,
                        activeLanguage = doc.ActiveLanguage,
                        compareMode = doc.TranslationCompareMode.ToString(),
                        compareLanguage = doc.CompareLanguage,
                        highlightUntranslated = doc.HighlightUntranslated,
                        knownLanguages = doc.GetKnownLanguages(),
                        exportVisibility = doc.TranslationLanguageExportVisibility
                            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(pair => new { language = pair.Key, visible = pair.Value })
                            .ToArray(),
                        languageLayouts = doc.TranslationLanguageLayouts
                            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                            .Select(pair => new
                            {
                                language = pair.Key,
                                direction = pair.Value.Direction.ToString(),
                                orientation = pair.Value.Orientation.ToString(),
                                mirrorTailsForRtl = pair.Value.MirrorTailsForRtl
                            })
                            .ToArray()
                    },
                    selectedBalloonId = doc.SelectedBalloonId,
                    selectedFloatingImageId = _editorState.SelectedFloatingImageId,
                    selectedFloatingImageIds = _editorState.SelectedFloatingImageIds.ToArray(),
                    selectedBalloonTemplateId = _selectedBalloonTemplateId,
                    activeBalloonTemplateId = _activeBalloonTemplateId,
                    recentBalloonTemplateIds = _recentBalloonTemplateIds.ToArray(),
                    quickBalloonTemplates,
                    balloonTemplateEyedropperActive = _isBalloonTemplateEyedropperActive,
                    pages,
                    pageTemplates,
                    layers,
                    balloons,
                    balloonTemplates,
                    balloonLinks = links,
                    panels,
                    floatingImages,
                    objectGroups
                }
            });
    }

    private Task<object> HandleBalloonStateRequest(Guid balloonId)
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            return Task.FromResult<object>(new { success = false, error = "No document loaded" });
        }

        var balloon = doc.FindBalloonAnywhere(balloonId);
        if (balloon == null)
        {
            return Task.FromResult<object>(new { success = false, error = $"Balloon {balloonId} not found" });
        }
        var ownerPage = doc.FindPageContainingBalloon(balloonId) ?? doc.ActivePage;

        var style = balloon.BalloonStyle;
        var textStyle = balloon.TextStyle;

        var data = new
        {
            id = balloon.Id,
            layerId = balloon.LayerId,
            panelId = balloon.PanelId,
            constrainToPanel = balloon.ConstrainToPanel,
            visible = balloon.IsVisible,
            locked = balloon.IsLocked,
            readingOrder = ownerPage?.GetBalloonReadingOrder(balloon),
            x = balloon.Position.X,
            y = balloon.Position.Y,
            width = balloon.ComputedSize.Width,
            height = balloon.ComputedSize.Height,
            shape = balloon.Shape.ToString(),
            text = balloon.Text,
            activeLanguageText = doc.GetBalloonDisplayText(balloon),
            untranslated = doc.IsBalloonUntranslated(balloon),
            staleTranslation = doc.IsBalloonTranslationStale(balloon, doc.ActiveLanguage),
            translations = balloon.Translations
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new
                {
                    language = pair.Key,
                    text = pair.Value.Text,
                    sourceTextSnapshot = pair.Value.SourceTextSnapshot,
                    updatedUtc = pair.Value.UpdatedUtc,
                    orientation = pair.Value.Orientation.ToString()
                })
                .ToArray(),
            maxTextWidth = balloon.MaxTextWidth,
            textPath = balloon.TextPath,
            balloonStyleId = balloon.BalloonStyleId,
            textStyleId = balloon.TextStyleId,
            style = new
            {
                fillColor = style.FillColor.ToString(),
                strokeColor = style.StrokeColor.ToString(),
                strokeWidth = style.StrokeWidth,
                cornerRadius = style.CornerRadius,
                thoughtSmoothness = style.ThoughtSmoothness,
                opacity = style.Opacity,
                gradientEnabled = style.GradientEnabled,
                gradientType = style.GradientType.ToString(),
                gradientAngle = style.GradientAngle,
                gradientStartColor = style.GradientStartColor.ToString(),
                gradientEndColor = style.GradientEndColor.ToString(),
                patternEnabled = style.PatternEnabled,
                patternType = style.PatternType.ToString(),
                patternSecondaryColor = style.PatternSecondaryColor.ToString(),
                patternScale = style.PatternScale,
                patternAngle = style.PatternAngle,
                patternImagePath = style.PatternImagePath,
                shadowEnabled = style.ShadowEnabled,
                shadowColor = style.ShadowColor.ToString(),
                shadowOpacity = style.ShadowOpacity,
                shadowOffsetX = style.ShadowOffsetX,
                shadowOffsetY = style.ShadowOffsetY,
                shadowFalloff = style.ShadowFalloff,
                glowEnabled = style.GlowEnabled,
                glowColor = style.GlowColor.ToString(),
                glowOpacity = style.GlowOpacity,
                glowSize = style.GlowSize,
                padding = new
                {
                    left = style.PaddingLeft,
                    top = style.PaddingTop,
                    right = style.PaddingRight,
                    bottom = style.PaddingBottom
                },
                minWidth = style.MinWidth,
                minHeight = style.MinHeight,
                maxWidth = style.MaxWidth,
                maxHeight = style.MaxHeight
            },
            textStyle = new
            {
                fontFamily = textStyle.FontFamily,
                fontSize = textStyle.FontSize,
                bold = textStyle.Bold,
                italic = textStyle.Italic,
                underline = textStyle.Underline,
                allCaps = textStyle.AllCaps,
                textColor = textStyle.TextColor.ToString(),
                fillType = textStyle.FillType.ToString(),
                fillSecondaryColor = textStyle.FillSecondaryColor.ToString(),
                fillAngle = textStyle.FillAngle,
                fillPattern = textStyle.FillPattern.ToString(),
                fillPatternScale = textStyle.FillPatternScale,
                fillImagePath = textStyle.FillImagePath,
                outlineColor = textStyle.OutlineColor.ToString(),
                outlineWidth = textStyle.OutlineWidth,
                additionalStrokes = textStyle.AdditionalStrokes.Select(stroke => new
                {
                    color = stroke.Color.ToString(),
                    width = stroke.Width
                }).ToArray(),
                shadows = textStyle.Shadows.Select(shadow => new
                {
                    color = shadow.Color.ToString(),
                    offsetX = shadow.OffsetX,
                    offsetY = shadow.OffsetY,
                    blur = shadow.Blur,
                    opacity = shadow.Opacity
                }).ToArray(),
                outerGlowEnabled = textStyle.OuterGlowEnabled,
                outerGlowColor = textStyle.OuterGlowColor.ToString(),
                outerGlowSize = textStyle.OuterGlowSize,
                outerGlowOpacity = textStyle.OuterGlowOpacity,
                innerGlowEnabled = textStyle.InnerGlowEnabled,
                innerGlowColor = textStyle.InnerGlowColor.ToString(),
                innerGlowSize = textStyle.InnerGlowSize,
                innerGlowOpacity = textStyle.InnerGlowOpacity,
                extrusionEnabled = textStyle.ExtrusionEnabled,
                extrusionDepth = textStyle.ExtrusionDepth,
                extrusionAngle = textStyle.ExtrusionAngle,
                extrusionColor = textStyle.ExtrusionColor.ToString(),
                extrusionOpacity = textStyle.ExtrusionOpacity,
                motionBlurEnabled = textStyle.MotionBlurEnabled,
                motionBlurDistance = textStyle.MotionBlurDistance,
                motionBlurAngle = textStyle.MotionBlurAngle,
                motionBlurOpacity = textStyle.MotionBlurOpacity,
                tracking = textStyle.Tracking,
                lineHeight = textStyle.LineHeight,
                alignment = textStyle.Alignment.ToString(),
                script = textStyle.Script.ToString(),
                fitMode = textStyle.FitMode.ToString(),
                overflowMode = textStyle.OverflowMode.ToString(),
                verticalOffset = textStyle.VerticalOffset,
                ragMode = textStyle.RagMode.ToString(),
                hyphenationLocale = textStyle.HyphenationLocale,
                justificationStrength = textStyle.JustificationStrength,
                hyphenationLevel = textStyle.HyphenationLevel,
                fillHeight = textStyle.FillHeight,
                warpPreset = textStyle.WarpPreset.ToString(),
                warpIntensity = textStyle.WarpIntensity,
                warpHorizontalDistortion = textStyle.WarpHorizontalDistortion,
                warpVerticalDistortion = textStyle.WarpVerticalDistortion,
                warpMesh = new
                {
                    topLeftOffset = textStyle.WarpMesh.TopLeftOffset,
                    topRightOffset = textStyle.WarpMesh.TopRightOffset,
                    bottomRightOffset = textStyle.WarpMesh.BottomRightOffset,
                    bottomLeftOffset = textStyle.WarpMesh.BottomLeftOffset
                }
            },
            textStyleSpans = balloon.TextStyleSpans.Select(span => new
            {
                start = span.Start,
                length = span.Length,
                style = new
                {
                    fontFamily = span.Style.FontFamily,
                    fontSize = span.Style.FontSize,
                    bold = span.Style.Bold,
                    italic = span.Style.Italic,
                    underline = span.Style.Underline,
                    allCaps = span.Style.AllCaps,
                    textColor = span.Style.TextColor.ToString(),
                    fillType = span.Style.FillType.ToString(),
                    fillSecondaryColor = span.Style.FillSecondaryColor.ToString(),
                    fillAngle = span.Style.FillAngle,
                    fillPattern = span.Style.FillPattern.ToString(),
                    fillPatternScale = span.Style.FillPatternScale,
                    fillImagePath = span.Style.FillImagePath,
                    outlineColor = span.Style.OutlineColor.ToString(),
                    outlineWidth = span.Style.OutlineWidth,
                    additionalStrokes = span.Style.AdditionalStrokes.Select(stroke => new
                    {
                        color = stroke.Color.ToString(),
                        width = stroke.Width
                    }).ToArray(),
                    shadows = span.Style.Shadows.Select(shadow => new
                    {
                        color = shadow.Color.ToString(),
                        offsetX = shadow.OffsetX,
                        offsetY = shadow.OffsetY,
                        blur = shadow.Blur,
                        opacity = shadow.Opacity
                    }).ToArray(),
                    outerGlowEnabled = span.Style.OuterGlowEnabled,
                    outerGlowColor = span.Style.OuterGlowColor.ToString(),
                    outerGlowSize = span.Style.OuterGlowSize,
                    outerGlowOpacity = span.Style.OuterGlowOpacity,
                    innerGlowEnabled = span.Style.InnerGlowEnabled,
                    innerGlowColor = span.Style.InnerGlowColor.ToString(),
                    innerGlowSize = span.Style.InnerGlowSize,
                    innerGlowOpacity = span.Style.InnerGlowOpacity,
                    extrusionEnabled = span.Style.ExtrusionEnabled,
                    extrusionDepth = span.Style.ExtrusionDepth,
                    extrusionAngle = span.Style.ExtrusionAngle,
                    extrusionColor = span.Style.ExtrusionColor.ToString(),
                    extrusionOpacity = span.Style.ExtrusionOpacity,
                    motionBlurEnabled = span.Style.MotionBlurEnabled,
                    motionBlurDistance = span.Style.MotionBlurDistance,
                    motionBlurAngle = span.Style.MotionBlurAngle,
                    motionBlurOpacity = span.Style.MotionBlurOpacity,
                    tracking = span.Style.Tracking,
                    lineHeight = span.Style.LineHeight,
                    alignment = span.Style.Alignment.ToString(),
                    script = span.Style.Script.ToString(),
                    fitMode = span.Style.FitMode.ToString(),
                    overflowMode = span.Style.OverflowMode.ToString(),
                    verticalOffset = span.Style.VerticalOffset,
                    ragMode = span.Style.RagMode.ToString(),
                    hyphenationLocale = span.Style.HyphenationLocale,
                    justificationStrength = span.Style.JustificationStrength,
                    hyphenationLevel = span.Style.HyphenationLevel,
                    fillHeight = span.Style.FillHeight,
                    warpPreset = span.Style.WarpPreset.ToString(),
                    warpIntensity = span.Style.WarpIntensity,
                    warpHorizontalDistortion = span.Style.WarpHorizontalDistortion,
                    warpVerticalDistortion = span.Style.WarpVerticalDistortion,
                    warpMesh = new
                    {
                        topLeftOffset = span.Style.WarpMesh.TopLeftOffset,
                        topRightOffset = span.Style.WarpMesh.TopRightOffset,
                        bottomRightOffset = span.Style.WarpMesh.BottomRightOffset,
                        bottomLeftOffset = span.Style.WarpMesh.BottomLeftOffset
                    }
                }
            }).ToArray(),
            customShapePathData = balloon.CustomShapePathData,
            tails = balloon.Tails.Select(t => new
            {
                id = t.Id,
                targetX = t.TargetPoint.X,
                targetY = t.TargetPoint.Y,
                style = t.Style.ToString(),
                baseWidth = t.BaseWidth
            }).ToArray()
        };

        return Task.FromResult<object>(new { success = true, data });
    }

    private async Task<object?> HandleScreenshotRequest(HttpListenerResponse response)
    {
        try
        {
            var tcs = new TaskCompletionSource<byte[]?>();

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var doc = _editorState.Document;
                    var width = doc != null ? (int)doc.Size.Width : (int)Math.Max(MainCanvas.ActualWidth, 100);
                    var height = doc != null ? (int)doc.Size.Height : (int)Math.Max(MainCanvas.ActualHeight, 100);

                    using var renderTarget = new CanvasRenderTarget(MainCanvas.Device, width, height, 96);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

                        if (doc != null && _renderer != null)
                        {
                            _renderer.RenderContent(
                                ds,
                                doc,
                                _editorState.BackgroundImage,
                                GetPanelImage,
                                GetFloatingImage,
                                GetTextFillImage);
                        }
                    }

                    using var stream = new InMemoryRandomAccessStream();
                    await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

                    stream.Seek(0);
                    var bytes = new byte[stream.Size];
                    await stream.AsStreamForRead().ReadExactlyAsync(bytes);

                    tcs.SetResult(bytes);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            var pngBytes = await tcs.Task;

            if (pngBytes != null)
            {
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                await response.OutputStream.WriteAsync(pngBytes);
                return null;
            }

            return new { success = false, error = "Failed to capture screenshot" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object?> HandleScreenshotBalloonRequest(HttpListenerResponse response, Guid balloonId)
    {
        try
        {
            var doc = _editorState.Document;
            if (doc == null)
            {
                return new { success = false, error = "No document loaded" };
            }

            var balloon = doc.FindBalloon(balloonId);
            if (balloon == null)
            {
                return new { success = false, error = $"Balloon {balloonId} not found" };
            }

            var tcs = new TaskCompletionSource<byte[]?>();

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    const int padding = 20;
                    var bounds = balloon.Bounds;

                    foreach (var tail in balloon.Tails)
                    {
                        var tailRect = new Rect(
                            MathF.Min(bounds.X, tail.TargetPoint.X) - padding,
                            MathF.Min(bounds.Y, tail.TargetPoint.Y) - padding,
                            MathF.Max(bounds.Right, tail.TargetPoint.X) - MathF.Min(bounds.X, tail.TargetPoint.X) + padding * 2,
                            MathF.Max(bounds.Bottom, tail.TargetPoint.Y) - MathF.Min(bounds.Y, tail.TargetPoint.Y) + padding * 2);
                        bounds = bounds.Union(tailRect);
                    }

                    var renderX = (int)MathF.Max(0, bounds.X - padding);
                    var renderY = (int)MathF.Max(0, bounds.Y - padding);
                    var renderWidth = (int)bounds.Width + padding * 2;
                    var renderHeight = (int)bounds.Height + padding * 2;

                    using var renderTarget = new CanvasRenderTarget(MainCanvas.Device, renderWidth, renderHeight, 96);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

                        ds.Transform = System.Numerics.Matrix3x2.CreateTranslation(-renderX, -renderY);

                        if (_renderer != null)
                        {
                            var layer = doc.FindLayer(balloon.LayerId);
                            var layerOpacity = layer?.Opacity ?? 1f;
                            _renderer.RenderBalloon(ds, balloon, isSelected: false, layerOpacity);
                        }
                    }

                    using var stream = new InMemoryRandomAccessStream();
                    await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

                    stream.Seek(0);
                    var bytes = new byte[stream.Size];
                    await stream.AsStreamForRead().ReadExactlyAsync(bytes);

                    tcs.SetResult(bytes);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            var pngBytes = await tcs.Task;

            if (pngBytes != null)
            {
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                await response.OutputStream.WriteAsync(pngBytes);
                return null;
            }

            return new { success = false, error = "Failed to capture balloon screenshot" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object?> HandleScreenshotLayerRequest(HttpListenerResponse response, Guid layerId)
    {
        try
        {
            var doc = _editorState.Document;
            if (doc == null)
            {
                return new { success = false, error = "No document loaded" };
            }

            var layer = doc.FindLayer(layerId);
            if (layer == null)
            {
                return new { success = false, error = $"Layer {layerId} not found" };
            }

            var tcs = new TaskCompletionSource<byte[]?>();

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var width = (int)doc.Size.Width;
                    var height = (int)doc.Size.Height;

                    using var renderTarget = new CanvasRenderTarget(MainCanvas.Device, width, height, 96);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

                        if (_renderer != null && doc.ActivePage != null)
                        {
                            _renderer.RenderPageContent(
                                ds,
                                doc.ActivePage,
                                _editorState.BackgroundImage,
                                includeHiddenLayers: true,
                                singleLayerId: layerId,
                                panelImageResolver: GetPanelImage,
                                floatingImageResolver: GetFloatingImage,
                                textFillImageResolver: GetTextFillImage,
                                translationDocument: doc);
                        }
                    }

                    using var stream = new InMemoryRandomAccessStream();
                    await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);

                    stream.Seek(0);
                    var bytes = new byte[stream.Size];
                    await stream.AsStreamForRead().ReadExactlyAsync(bytes);

                    tcs.SetResult(bytes);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            var pngBytes = await tcs.Task;

            if (pngBytes != null)
            {
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                await response.OutputStream.WriteAsync(pngBytes);
                return null;
            }

            return new { success = false, error = "Failed to capture layer screenshot" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object?> HandleScreenshotUiRequest(HttpListenerResponse response)
    {
        try
        {
            var tcs = new TaskCompletionSource<byte[]?>();

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var rootGrid = Content as Microsoft.UI.Xaml.Controls.Grid;
                    if (rootGrid == null)
                    {
                        tcs.SetResult(null);
                        return;
                    }

                    var renderTarget = new RenderTargetBitmap();
                    await renderTarget.RenderAsync(rootGrid);

                    var pixelBuffer = await renderTarget.GetPixelsAsync();
                    var pixels = pixelBuffer.ToArray();

                    using var stream = new InMemoryRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        (uint)renderTarget.PixelWidth,
                        (uint)renderTarget.PixelHeight,
                        96, 96,
                        pixels);
                    await encoder.FlushAsync();

                    stream.Seek(0);
                    var bytes = new byte[stream.Size];
                    await stream.AsStreamForRead().ReadExactlyAsync(bytes);

                    tcs.SetResult(bytes);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            var pngBytes = await tcs.Task;

            if (pngBytes != null)
            {
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                await response.OutputStream.WriteAsync(pngBytes);
                return null;
            }

            return new { success = false, error = "Failed to capture UI screenshot" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> HandleCommandsRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "POST")
        {
            return new { success = false, error = "Commands endpoint requires POST method" };
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();

            if (!TryParseCommandBatch(body, out var commands, out var useTransaction, out var error))
            {
                return new { success = false, error = error ?? "Invalid command payload" };
            }

            if (commands.Length == 0)
            {
                return new { success = true, data = new { executed = 0 } };
            }

            var tcs = new TaskCompletionSource<object>();

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var createdIds = new List<Guid>();
                    int executedCount = 0;

                    if (useTransaction)
                    {
                        var commandList = new List<ICommand>();
                        foreach (var cmdData in commands)
                        {
                            if (IsAutomationUiCommandType(cmdData.Type))
                            {
                                throw new InvalidOperationException($"Automation UI command '{cmdData.Type}' is not supported in transaction batches. Use /commands without transaction.");
                            }

                            var cmd = CreateCommandFromData(cmdData);
                            if (cmd == null)
                            {
                                throw new InvalidOperationException($"Unknown command type: {cmdData.Type}");
                            }

                            if (cmd is CreateBalloonCommand createBalloon)
                                createdIds.Add(createBalloon.CreatedBalloonId);
                            else if (cmd is CreateBalloonFromTemplateCommand createBalloonFromTemplate)
                                createdIds.Add(createBalloonFromTemplate.CreatedBalloonId);
                            else if (cmd is CreateBalloonTemplateCommand createBalloonTemplate)
                                createdIds.Add(createBalloonTemplate.CreatedTemplateId);
                            else if (cmd is CreatePageCommand createPage)
                                createdIds.Add(createPage.CreatedPageId);
                            else if (cmd is CreatePageFromTemplateCommand createPageFromTemplate)
                                createdIds.Add(createPageFromTemplate.CreatedPageId);
                            else if (cmd is DuplicatePageCommand duplicatePage)
                                createdIds.Add(duplicatePage.CreatedPageId);
                            else if (cmd is CreateLayerCommand createLayer)
                                createdIds.Add(createLayer.CreatedLayerId);
                            else if (cmd is CreateTailCommand createTail)
                                createdIds.Add(createTail.CreatedTailId);
                            else if (cmd is CreateGuideCommand createGuide)
                                createdIds.Add(createGuide.CreatedGuideId);

                            commandList.Add(cmd);
                        }

                        _editorState.ExecuteTransactionSafe("Automation batch", commandList);
                        executedCount = commandList.Count;
                    }
                    else
                    {
                        foreach (var cmdData in commands)
                        {
                            if (ExecuteAutomationUiCommand(cmdData, createdIds))
                            {
                                executedCount++;
                                continue;
                            }

                            var cmd = CreateCommandFromData(cmdData);
                            if (cmd != null)
                            {
                                _editorState.Execute(cmd);
                                executedCount++;

                                if (cmd is CreateBalloonCommand createBalloon)
                                    createdIds.Add(createBalloon.CreatedBalloonId);
                                else if (cmd is CreateBalloonFromTemplateCommand createBalloonFromTemplate)
                                    createdIds.Add(createBalloonFromTemplate.CreatedBalloonId);
                                else if (cmd is CreateBalloonTemplateCommand createBalloonTemplate)
                                    createdIds.Add(createBalloonTemplate.CreatedTemplateId);
                                else if (cmd is CreatePageCommand createPage)
                                    createdIds.Add(createPage.CreatedPageId);
                                else if (cmd is CreatePageFromTemplateCommand createPageFromTemplate)
                                    createdIds.Add(createPageFromTemplate.CreatedPageId);
                                else if (cmd is DuplicatePageCommand duplicatePage)
                                    createdIds.Add(duplicatePage.CreatedPageId);
                                else if (cmd is CreateLayerCommand createLayer)
                                    createdIds.Add(createLayer.CreatedLayerId);
                                else if (cmd is CreateTailCommand createTail)
                                    createdIds.Add(createTail.CreatedTailId);
                                else if (cmd is CreateGuideCommand createGuide)
                                    createdIds.Add(createGuide.CreatedGuideId);
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unknown command type: {cmdData.Type}");
                            }
                        }
                    }

                    tcs.SetResult(new
                    {
                        success = true,
                        data = new
                        {
                            executed = executedCount,
                            createdIds = createdIds.ToArray()
                        }
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new { success = false, error = ex.Message });
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private static bool TryParseCommandBatch(string body, out CommandData[] commands, out bool useTransaction, out string? error)
    {
        commands = Array.Empty<CommandData>();
        useTransaction = false;
        error = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            error = "Empty command payload";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                commands = JsonSerializer.Deserialize<CommandData[]>(body, AutomationCommandJsonOptions) ?? Array.Empty<CommandData>();
                return true;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (!document.RootElement.TryGetProperty("commands", out var commandsElement) ||
                    commandsElement.ValueKind != JsonValueKind.Array)
                {
                    error = "Missing commands array";
                    return false;
                }

                commands = JsonSerializer.Deserialize<CommandData[]>(commandsElement.GetRawText(), AutomationCommandJsonOptions) ?? Array.Empty<CommandData>();

                if (document.RootElement.TryGetProperty("transaction", out var transactionElement) &&
                    (transactionElement.ValueKind == JsonValueKind.True || transactionElement.ValueKind == JsonValueKind.False))
                {
                    useTransaction = transactionElement.GetBoolean();
                }

                return true;
            }

            error = "Invalid command payload";
            return false;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private async Task<object> HandleCommandsTransactionRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "POST")
        {
            return new { success = false, error = "Commands transaction endpoint requires POST method" };
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();

            var commands = JsonSerializer.Deserialize<CommandData[]>(body, AutomationCommandJsonOptions);
            if (commands == null || commands.Length == 0)
            {
                return new { success = true, data = new { executed = 0 } };
            }

            var tcs = new TaskCompletionSource<object>();

            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var createdIds = new List<Guid>();
                    var commandList = new List<ICommand>();

                    foreach (var cmdData in commands)
                    {
                        if (IsAutomationUiCommandType(cmdData.Type))
                        {
                            throw new InvalidOperationException($"Automation UI command '{cmdData.Type}' is not supported in /commands/transaction. Use /commands instead.");
                        }

                        var cmd = CreateCommandFromData(cmdData);
                        if (cmd == null)
                        {
                            throw new InvalidOperationException($"Unknown command type: {cmdData.Type}");
                        }

                        if (cmd is CreateBalloonCommand createBalloon)
                            createdIds.Add(createBalloon.CreatedBalloonId);
                        else if (cmd is CreateBalloonFromTemplateCommand createBalloonFromTemplate)
                            createdIds.Add(createBalloonFromTemplate.CreatedBalloonId);
                        else if (cmd is CreateBalloonTemplateCommand createBalloonTemplate)
                            createdIds.Add(createBalloonTemplate.CreatedTemplateId);
                        else if (cmd is CreatePageCommand createPage)
                            createdIds.Add(createPage.CreatedPageId);
                        else if (cmd is CreatePageFromTemplateCommand createPageFromTemplate)
                            createdIds.Add(createPageFromTemplate.CreatedPageId);
                        else if (cmd is DuplicatePageCommand duplicatePage)
                            createdIds.Add(duplicatePage.CreatedPageId);
                        else if (cmd is CreateLayerCommand createLayer)
                            createdIds.Add(createLayer.CreatedLayerId);
                        else if (cmd is CreateTailCommand createTail)
                            createdIds.Add(createTail.CreatedTailId);
                        else if (cmd is CreateGuideCommand createGuide)
                            createdIds.Add(createGuide.CreatedGuideId);

                        commandList.Add(cmd);
                    }

                    _editorState.ExecuteTransactionSafe("Automation transaction", commandList);

                    tcs.SetResult(new
                    {
                        success = true,
                        data = new
                        {
                            executed = commandList.Count,
                            createdIds = createdIds.ToArray()
                        }
                    });
                }
                catch (Exception ex)
                {
                    tcs.SetResult(new { success = false, error = ex.Message });
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private static bool IsAutomationUiCommandType(string type)
    {
        return type switch
        {
            "SetActiveBalloonTemplate" => true,
            "SetSelectedBalloonTemplate" => true,
            "ApplyBalloonTemplateQuickPalette" => true,
            "SetBalloonTemplateEyedropperMode" => true,
            "RunBalloonTemplateEyedropper" => true,
            _ => false
        };
    }

    private bool ExecuteAutomationUiCommand(CommandData data, List<Guid> createdIds)
    {
        var doc = _editorState.Document;
        if (doc == null)
        {
            throw new InvalidOperationException("No document loaded");
        }

        switch (data.Type)
        {
            case "SetActiveBalloonTemplate":
            {
                var clear = data.Parameters.ContainsKey("clear") && data.Get<bool>("clear");
                var templateId = clear ? null : GetOptionalGuidParam(data, "templateId");
                if (!clear && !templateId.HasValue)
                {
                    throw new InvalidOperationException("SetActiveBalloonTemplate requires 'templateId' or clear=true.");
                }

                if (templateId.HasValue && doc.FindBalloonTemplate(templateId.Value) == null)
                {
                    throw new InvalidOperationException($"Balloon template {templateId.Value} not found");
                }

                _activeBalloonTemplateId = templateId;
                if (templateId.HasValue)
                {
                    _selectedBalloonTemplateId = templateId.Value;
                    RecordRecentBalloonTemplate(templateId.Value);
                }

                RefreshBalloonTemplateControls();
                UpdateToolButtonStates();
                return true;
            }

            case "SetSelectedBalloonTemplate":
            {
                var templateId = GetRequiredGuidParam(data, "templateId");
                if (doc.FindBalloonTemplate(templateId) == null)
                {
                    throw new InvalidOperationException($"Balloon template {templateId} not found");
                }

                _selectedBalloonTemplateId = templateId;
                if (data.Parameters.ContainsKey("setAsNew") && data.Get<bool>("setAsNew"))
                {
                    _activeBalloonTemplateId = templateId;
                    RecordRecentBalloonTemplate(templateId);
                }

                RefreshBalloonTemplateControls();
                UpdateToolButtonStates();
                return true;
            }

            case "ApplyBalloonTemplateQuickPalette":
            {
                var quickTemplates = GetToolbarQuickPaletteTemplates(doc);
                if (quickTemplates.Count == 0)
                {
                    throw new InvalidOperationException("Quick palette is empty.");
                }

                var index = data.Parameters.ContainsKey("index") ? data.Get<int>("index") : 0;
                if (index < 0 || index >= quickTemplates.Count)
                {
                    throw new InvalidOperationException($"Quick palette index {index} is out of range (0..{quickTemplates.Count - 1}).");
                }

                var template = quickTemplates[index];
                _selectedBalloonTemplateId = template.Id;
                RecordRecentBalloonTemplate(template.Id);

                if (!TryApplyTemplateToSelectedBalloons(template, " (Quick palette)"))
                {
                    _activeBalloonTemplateId = template.Id;
                    SetStatusMessage(LF("balloon_template.status.palette_selected", template.Name));
                }

                RefreshBalloonTemplateControls();
                UpdateToolButtonStates();
                return true;
            }

            case "SetBalloonTemplateEyedropperMode":
            {
                var enabled = !data.Parameters.ContainsKey("enabled") || data.Get<bool>("enabled");
                SetBalloonTemplateEyedropperMode(enabled);
                RefreshBalloonTemplateControls();
                return true;
            }

            case "RunBalloonTemplateEyedropper":
            {
                ExecuteBalloonTemplateEyedropperAutomation(data, createdIds);
                return true;
            }

            default:
                return false;
        }
    }

    private void ExecuteBalloonTemplateEyedropperAutomation(CommandData data, List<Guid> createdIds)
    {
        var doc = _editorState.Document ?? throw new InvalidOperationException("No document loaded");
        var source = ResolveEyedropperSourceBalloon(data, doc)
            ?? throw new InvalidOperationException("Could not resolve source balloon. Provide sourceBalloonId or x/y coordinates.");

        var applyToSelection = !data.Parameters.ContainsKey("applyToSelection") || data.Get<bool>("applyToSelection");
        var explicitTargets = data.Parameters.ContainsKey("targetBalloonIds")
            ? data.Get<List<Guid>>("targetBalloonIds") ?? new List<Guid>()
            : new List<Guid>();

        var targetIds = explicitTargets.Count > 0
            ? explicitTargets
            : applyToSelection
                ? GetSelectedBalloons().Select(balloon => balloon.Id).ToList()
                : new List<Guid>();

        targetIds = targetIds
            .Where(id => id != source.Id && doc.FindBalloon(id) != null)
            .Distinct()
            .ToList();

        if (targetIds.Count > 0)
        {
            var tempTemplateId = Guid.NewGuid();
            var applyCommands = new List<ICommand>
            {
                new CreateBalloonTemplateCommand(
                    source.Id,
                    "_automation-eyedropper",
                    category: "Eyedropper",
                    placeholderText: source.Text,
                    templateId: tempTemplateId)
            };

            applyCommands.AddRange(targetIds.Select(targetId =>
                (ICommand)new ApplyBalloonTemplateCommand(tempTemplateId, targetId, applyPlaceholderText: false, replaceTail: true)));
            applyCommands.Add(new DeleteBalloonTemplateCommand(tempTemplateId));

            _editorState.ExecuteTransaction("Automation eyedropper apply", applyCommands);
            SetStatusMessage(
                targetIds.Count == 1
                    ? L("balloon_template.status.sampled_single")
                    : LF("balloon_template.status.sampled_multiple", targetIds.Count));
        }
        else
        {
            SetStatusMessage(L("balloon_template.eyedropper.status_no_target"));
        }

        var saveAsTemplate = data.Parameters.ContainsKey("saveAsTemplate") && data.Get<bool>("saveAsTemplate");
        if (saveAsTemplate)
        {
            var requestedName = data.Get<string>("templateName");
            var templateName = string.IsNullOrWhiteSpace(requestedName)
                ? GetEyedropperTemplateSuggestedName(source)
                : requestedName!;
            var uniqueName = GetUniqueBalloonTemplateName(templateName, doc.BalloonTemplates);
            var category = data.Get<string>("category") ?? "Eyedropper";

            var createTemplate = new CreateBalloonTemplateCommand(
                source.Id,
                uniqueName,
                category: category,
                placeholderText: source.Text);

            _editorState.Execute(createTemplate);
            createdIds.Add(createTemplate.CreatedTemplateId);
            _selectedBalloonTemplateId = createTemplate.CreatedTemplateId;
            RecordRecentBalloonTemplate(createTemplate.CreatedTemplateId);
            SetStatusMessage(LF("balloon_template.status.saved_eyedropper", uniqueName));
        }

        SetBalloonTemplateEyedropperMode(false, announceCancellation: false);
        RefreshBalloonTemplateControls();
        UpdateToolButtonStates();
        UpdatePropertiesPanel();
        MainCanvas.Invalidate();
    }

    private Balloon? ResolveEyedropperSourceBalloon(CommandData data, Document doc)
    {
        if (data.Parameters.ContainsKey("sourceBalloonId"))
        {
            var sourceId = GetRequiredGuidParam(data, "sourceBalloonId");
            return doc.FindBalloon(sourceId);
        }

        if (data.Parameters.ContainsKey("balloonId"))
        {
            var sourceId = GetRequiredGuidParam(data, "balloonId");
            return doc.FindBalloon(sourceId);
        }

        if (!data.Parameters.ContainsKey("x") || !data.Parameters.ContainsKey("y"))
        {
            return null;
        }

        var point = new Point2(data.Get<float>("x"), data.Get<float>("y"));
        var coordinates = data.Get<string>("coordinates") ?? data.Get<string>("coordinateSpace") ?? "world";
        var screenPoint = coordinates.Equals("screen", StringComparison.OrdinalIgnoreCase)
            ? point
            : _editorState.ViewTransform.WorldToScreen(point);

        return _editorState.HitTestBalloon(screenPoint);
    }

    private static Guid GetRequiredGuidParam(CommandData data, string key)
    {
        if (!data.Parameters.ContainsKey(key))
        {
            throw new InvalidOperationException($"Required parameter '{key}' not found");
        }

        var value = data.Get<Guid>(key);
        if (value == Guid.Empty)
        {
            throw new InvalidOperationException($"Parameter '{key}' is invalid");
        }

        return value;
    }

    private static Guid? GetOptionalGuidParam(CommandData data, string key)
    {
        if (!data.Parameters.ContainsKey(key))
        {
            return null;
        }

        var nullable = data.Get<Guid?>(key);
        if (nullable.HasValue)
        {
            return nullable.Value;
        }

        var value = data.Get<Guid>(key);
        return value == Guid.Empty ? null : value;
    }

    private ICommand? CreateCommandFromData(CommandData data)
    {
        var doc = _editorState.Document;
        if (doc == null) return null;

        return data.Type switch
        {
            "CreateBalloon" => new CreateBalloonCommand(
                data.Get<Guid>("layerId") != Guid.Empty ? data.Get<Guid>("layerId") : doc.GetPreferredBalloonLayerId(),
                new Point2(data.Get<float>("x"), data.Get<float>("y")),
                data.Get<string>("text") ?? "",
                Enum.TryParse<BalloonShape>(data.Get<string>("shape"), out var shape) ? shape : BalloonShape.Oval,
                panelId: data.Parameters.ContainsKey("panelId") ? data.Get<Guid?>("panelId") : null,
                constrainToPanel: data.Parameters.ContainsKey("constrainToPanel") && data.Get<bool>("constrainToPanel"),
                textPath: data.Get<TextPath>("textPath")),

            "CreateBalloonFromTemplate" => new CreateBalloonFromTemplateCommand(
                data.GetRequired<Guid>("templateId"),
                data.Get<Guid>("layerId") != Guid.Empty ? data.Get<Guid>("layerId") : doc.GetPreferredBalloonLayerId(),
                new Point2(data.Get<float>("x"), data.Get<float>("y")),
                data.Parameters.ContainsKey("usePlaceholderText") ? data.Get<bool>("usePlaceholderText") : true,
                data.Parameters.ContainsKey("attachTail") ? data.Get<bool>("attachTail") : true,
                data.Parameters.ContainsKey("balloonId") ? data.Get<Guid>("balloonId") : null,
                data.Parameters.ContainsKey("panelId") ? data.Get<Guid?>("panelId") : null,
                data.Parameters.ContainsKey("constrainToPanel") ? data.Get<bool>("constrainToPanel") : false),

            "CreateBalloonTemplate" => new CreateBalloonTemplateCommand(
                data.Parameters.ContainsKey("sourceBalloonId") ? data.Get<Guid>("sourceBalloonId") : data.GetRequired<Guid>("balloonId"),
                data.Get<string>("name") ?? "Balloon Template",
                data.Get<string>("description"),
                data.Get<List<string>>("tags") ?? new List<string>(),
                data.Get<string>("category"),
                data.Get<string>("placeholderText"),
                data.Parameters.ContainsKey("templateId") ? data.Get<Guid>("templateId") : null,
                data.Parameters.ContainsKey("isFavorite") && data.Get<bool>("isFavorite"),
                data.Parameters.ContainsKey("hotkeySlot") ? data.Get<int?>("hotkeySlot") : null,
                data.Parameters.ContainsKey("isBuiltIn") && data.Get<bool>("isBuiltIn")),

            "AddBalloonTemplate" => BuildBalloonTemplateFromData(data) is BalloonTemplate addTemplate
                ? new AddBalloonTemplateCommand(addTemplate)
                : null,

            "UpdateBalloonTemplate" => BuildBalloonTemplateFromData(data) is BalloonTemplate updateTemplate
                ? new UpdateBalloonTemplateCommand(
                    data.Parameters.ContainsKey("templateId") ? data.Get<Guid>("templateId") : updateTemplate.Id,
                    updateTemplate)
                : null,

            "DeleteBalloonTemplate" => new DeleteBalloonTemplateCommand(
                data.GetRequired<Guid>("templateId")),

            "ApplyBalloonTemplate" => new ApplyBalloonTemplateCommand(
                data.GetRequired<Guid>("templateId"),
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("applyPlaceholderText") && data.Get<bool>("applyPlaceholderText"),
                data.Parameters.ContainsKey("replaceTail") ? data.Get<bool>("replaceTail") : true),

            "MoveBalloon" => new MoveBalloonCommand(
                data.GetRequired<Guid>("balloonId"),
                new Point2(data.Get<float>("x"), data.Get<float>("y"))),

            "ReorderBalloon" => new ReorderBalloonCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<int>("newIndex")),

            "DeleteBalloon" => new DeleteBalloonCommand(data.GetRequired<Guid>("balloonId")),

            "SetBalloonText" => new SetBalloonTextCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("text") ?? data.Get<string>("newText") ?? ""),

            "SetBalloonTranslation" => new SetBalloonTranslationCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("language") ?? doc.ActiveLanguage,
                data.Get<string>("text") ?? string.Empty,
                data.Parameters.ContainsKey("sourceTextSnapshot") ? data.Get<string>("sourceTextSnapshot") : null,
                data.Parameters.ContainsKey("orientation") &&
                Enum.TryParse<TranslationTextOrientation>(data.Get<string>("orientation"), ignoreCase: true, out var translationOrientation)
                    ? translationOrientation
                    : null),

            "SetBalloonTranslationOrientation" => new SetBalloonTranslationOrientationCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("language") ?? doc.ActiveLanguage,
                Enum.TryParse<TranslationTextOrientation>(data.Get<string>("orientation"), ignoreCase: true, out var balloonOrientation)
                    ? balloonOrientation
                    : TranslationTextOrientation.Auto),

            "DeleteBalloonTranslation" => new DeleteBalloonTranslationCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("language") ?? doc.ActiveLanguage),

            "SetBalloonRichText" => new SetBalloonRichTextCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("text") ?? data.Get<string>("newText") ?? "",
                data.Get<List<TextStyleSpan>>("spans") ?? new List<TextStyleSpan>()),

            "SetBalloonCustomShape" => new SetBalloonCustomShapeCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<string>("pathData")),

            "SetBalloonTextPath" => new SetBalloonTextPathCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("clear") && data.Get<bool>("clear")
                    ? null
                    : data.Get<TextPath>("path") ?? data.Get<TextPath>("textPath")),

            "SetBalloonConstrainToPanel" => new SetBalloonConstrainToPanelCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("constrainToPanel") ? data.Get<bool>("constrainToPanel") : data.Get<bool>("value")),

            "SetBalloonVisibility" => new SetBalloonVisibilityCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("visible") ? data.Get<bool>("visible") : data.Get<bool>("value")),

            "SetBalloonLocked" => new SetBalloonLockedCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("locked") ? data.Get<bool>("locked") : data.Get<bool>("value")),

            "CreateTail" => new CreateTailCommand(
                data.GetRequired<Guid>("balloonId"),
                new Point2(data.Get<float>("x"), data.Get<float>("y"))),

            "MoveTailTarget" => new MoveTailTargetCommand(
                data.GetRequired<Guid>("balloonId"),
                new Point2(data.Get<float>("x"), data.Get<float>("y"))),

            "DeleteTail" => new DeleteTailCommand(data.GetRequired<Guid>("balloonId")),

            "SetTailAttachment" => new SetTailAttachmentDirectionCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("directionX") && data.Parameters.ContainsKey("directionY")
                    ? new Point2(data.Get<float>("directionX"), data.Get<float>("directionY"))
                    : null,
                data.Parameters.ContainsKey("tailId") ? data.Get<Guid>("tailId") : null),

            "SetTailStyle" => Enum.TryParse<TailStyle>(data.Get<string>("style") ?? data.Get<string>("newStyle"), ignoreCase: true, out var tailStyle)
                ? new SetTailStyleCommand(
                    data.GetRequired<Guid>("balloonId"),
                    tailStyle,
                    GetOptionalGuidParam(data, "tailId"))
                : null,

            "SetTailWidth" => new SetTailWidthCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("width") ? data.Get<float>("width") : data.Get<float>("newWidth"),
                GetOptionalGuidParam(data, "tailId")),

            "SetTailCurvature" => new SetTailCurvatureCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<float>("curvature"),
                GetOptionalGuidParam(data, "tailId")),

            "SetTailCurveCenter" => new SetTailCurveCenterCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<float>("curveCenter"),
                GetOptionalGuidParam(data, "tailId")),

            "SetTailInset" => new SetTailInsetCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<float>("inset"),
                GetOptionalGuidParam(data, "tailId")),

            "SetBalloonTailsFromTemplates" => new SetBalloonTailsFromTemplatesCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<List<BalloonTemplateTail>>("tails") ?? new List<BalloonTemplateTail>(),
                data.Parameters.ContainsKey("preservePlacement") ? data.Get<bool>("preservePlacement") : true),

            "LinkBalloons" => new LinkBalloonsCommand(
                data.GetRequired<Guid>("balloonAId"),
                data.GetRequired<Guid>("balloonBId")),

            "UnlinkBalloons" => new UnlinkBalloonsCommand(
                data.GetRequired<Guid>("balloonAId"),
                data.GetRequired<Guid>("balloonBId")),

            "SetBalloonLinkStyle" => new SetBalloonLinkStyleCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                BuildLinkStyleFromData(data)),

            "SetOffPanelIndicatorStyle" => new SetOffPanelIndicatorStyleCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                BuildOffPanelIndicatorStyleFromData(data)),

            "ClearBalloonLinks" => new ClearBalloonLinksCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId),

            "SetDocumentName" => new SetDocumentNameCommand(
                data.Get<string>("name") ?? doc.Name),

            "SetDocumentBaseLanguage" => new SetDocumentBaseLanguageCommand(
                data.Get<string>("language") ?? doc.BaseLanguage),

            "SetDocumentActiveLanguage" => new SetDocumentActiveLanguageCommand(
                data.Get<string>("language") ?? doc.ActiveLanguage),

            "SetDocumentTranslationCompare" => new SetDocumentTranslationCompareCommand(
                Enum.TryParse<TranslationCompareMode>(data.Get<string>("mode"), ignoreCase: true, out var compareMode)
                    ? compareMode
                    : TranslationCompareMode.None,
                data.Parameters.ContainsKey("compareLanguage") ? data.Get<string>("compareLanguage") : null),

            "SetDocumentHighlightUntranslated" => new SetDocumentHighlightUntranslatedCommand(
                data.Parameters.ContainsKey("enabled")
                    ? data.Get<bool>("enabled")
                    : data.Parameters.ContainsKey("highlight")
                        ? data.Get<bool>("highlight")
                        : true),

            "SetTranslationLanguageExportVisibility" => new SetTranslationLanguageExportVisibilityCommand(
                data.Get<string>("language") ?? doc.ActiveLanguage,
                data.Parameters.ContainsKey("visible") ? data.Get<bool>("visible") : true),

            "RemoveTranslationLanguageExportVisibility" => new RemoveTranslationLanguageExportVisibilityCommand(
                data.Get<string>("language") ?? doc.ActiveLanguage),

            "SetTranslationLanguageLayout" => new SetTranslationLanguageLayoutCommand(
                data.Get<string>("language") ?? doc.ActiveLanguage,
                Enum.TryParse<TranslationTextDirection>(data.Get<string>("direction"), ignoreCase: true, out var textDirection)
                    ? textDirection
                    : TranslationTextDirection.Auto,
                Enum.TryParse<TranslationTextOrientation>(data.Get<string>("orientation"), ignoreCase: true, out var textOrientation)
                    ? textOrientation
                    : TranslationTextOrientation.Auto,
                data.Parameters.ContainsKey("mirrorTailsForRtl")
                    ? data.Get<bool>("mirrorTailsForRtl")
                    : true),

            "SetDocumentDpi" => new SetDocumentDpiCommand(
                data.Parameters.ContainsKey("dpi") ? data.Get<float>("dpi") : doc.DefaultDpi),

            "SetDocumentUnits" => new SetDocumentUnitsCommand(
                data.Get<string>("units") ?? doc.DefaultUnits),

            "SetDocumentDefaultPageSize" => new SetDocumentDefaultPageSizeCommand(
                new Size2(
                    data.Parameters.ContainsKey("width") ? data.Get<float>("width") : doc.DefaultPageSize.Width,
                    data.Parameters.ContainsKey("height") ? data.Get<float>("height") : doc.DefaultPageSize.Height)),

            "SetDocumentDefaultBackgroundColor" => new SetDocumentDefaultBackgroundColorCommand(
                ParseOptionalColor(data)),

            "SetDocumentDefaultBackgroundImage" => new SetDocumentDefaultBackgroundImageCommand(
                data.Parameters.ContainsKey("path") ? data.Get<string>("path") : null),

            "SetPageSize" => new SetPageSizeCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                new Size2(
                    data.Parameters.ContainsKey("width") ? data.Get<float>("width") : doc.Size.Width,
                    data.Parameters.ContainsKey("height") ? data.Get<float>("height") : doc.Size.Height)),

            "SetPageBackgroundImage" => new SetPageBackgroundImageCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Parameters.ContainsKey("imagePath")
                    ? data.Get<string>("imagePath")
                    : data.Parameters.ContainsKey("path")
                        ? data.Get<string>("path")
                        : null),

            "SetPageBackgroundImageFitMode" => new SetPageBackgroundImageFitModeCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                Enum.TryParse<PanelImageFitMode>(data.Get<string>("fitMode"), ignoreCase: true, out var backgroundFitMode)
                    ? backgroundFitMode
                    : PanelImageFitMode.Fill),

            "CreateLayer" => new CreateLayerCommand(data.Get<string>("name") ?? "New Layer"),

            "SetLayerBlendMode" => new SetLayerBlendModeCommand(
                data.GetRequired<Guid>("layerId"),
                Enum.TryParse<LayerBlendMode>(
                    data.Parameters.ContainsKey("blendMode") ? data.Get<string>("blendMode") : data.Get<string>("mode"),
                    ignoreCase: true,
                    out var blendMode)
                    ? blendMode
                    : LayerBlendMode.Normal),

            "CreatePage" => new CreatePageCommand(
                data.Get<string>("name") ?? $"Page {doc.Pages.Count + 1}",
                new Size2(
                    data.Parameters.ContainsKey("width") ? data.Get<float>("width") : doc.DefaultPageSize.Width,
                    data.Parameters.ContainsKey("height") ? data.Get<float>("height") : doc.DefaultPageSize.Height),
                data.Parameters.ContainsKey("insertIndex") ? data.Get<int>("insertIndex") : -1,
                data.Parameters.ContainsKey("setActive") ? data.Get<bool>("setActive") : true),

            "CreatePageTemplate" => new CreatePageTemplateCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<string>("name") ?? "Page Template",
                data.Parameters.ContainsKey("templateId") ? data.Get<Guid>("templateId") : null),

            "CreatePageFromTemplate" => new CreatePageFromTemplateCommand(
                data.GetRequired<Guid>("templateId"),
                data.Get<string>("name") ?? $"Page {doc.Pages.Count + 1}",
                data.Parameters.ContainsKey("insertIndex") ? data.Get<int>("insertIndex") : -1,
                data.Parameters.ContainsKey("setActive") ? data.Get<bool>("setActive") : true,
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : null),

            "RenamePageTemplate" => new RenamePageTemplateCommand(
                data.GetRequired<Guid>("templateId"),
                data.Get<string>("name") ?? "Page Template"),

            "DeletePageTemplate" => new DeletePageTemplateCommand(
                data.GetRequired<Guid>("templateId")),

            "CreatePanelZone" => new CreatePanelZoneCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<string>("name") ?? "Panel",
                new Rect(
                    data.Get<float>("x"),
                    data.Get<float>("y"),
                    data.Get<float>("width"),
                    data.Get<float>("height")),
                data.Parameters.ContainsKey("order") ? data.Get<int>("order") : -1,
                BuildPanelColorFromData(data),
                data.Parameters.ContainsKey("panelId") ? data.Get<Guid>("panelId") : null,
                data.Parameters.ContainsKey("insertIndex") ? data.Get<int>("insertIndex") : -1),

            "SetPanelZoneOrders" => new SetPanelZoneOrdersCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<List<Guid>>("orderedPanelIds") ?? new List<Guid>()),

            "MergePanelLayoutTemplate" => new MergePanelLayoutTemplateCommand(
                data.GetRequired<Guid>("templateId"),
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId),

            "AddPanelLayoutTemplate" => BuildPanelLayoutTemplateFromData(data) is PanelLayoutTemplate template
                ? new AddPanelLayoutTemplateCommand(template)
                : null,

            "UpdatePanelLayoutTemplateMetadata" => new UpdatePanelLayoutTemplateMetadataCommand(
                data.GetRequired<Guid>("templateId"),
                data.Get<string>("name") ?? "Panel Layout",
                data.Get<string>("description"),
                data.Get<List<string>>("tags") ?? new List<string>(),
                data.Get<string>("category")),

            "CreateFloatingImage" => new CreateFloatingImageCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<string>("imagePath"),
                new Rect(
                    data.Get<float>("x"),
                    data.Get<float>("y"),
                    data.Get<float>("width"),
                    data.Get<float>("height")),
                data.Parameters.ContainsKey("opacity") ? data.Get<float>("opacity") : 1f,
                data.Parameters.ContainsKey("isVisible") ? data.Get<bool>("isVisible") : true,
                data.Parameters.ContainsKey("isLocked") ? data.Get<bool>("isLocked") : false,
                data.Parameters.ContainsKey("layerId") ? data.Get<Guid?>("layerId") : null,
                data.Parameters.ContainsKey("imageId") ? data.Get<Guid>("imageId") : null,
                data.Parameters.ContainsKey("insertIndex") ? data.Get<int>("insertIndex") : -1,
                data.Parameters.ContainsKey("name") ? data.Get<string>("name") : null,
                data.Parameters.ContainsKey("source") ? data.Get<string>("source") : null,
                data.Parameters.ContainsKey("rotation") ? data.Get<float>("rotation") : 0f,
                data.Parameters.ContainsKey("shadowEnabled") && data.Get<bool>("shadowEnabled"),
                data.Parameters.ContainsKey("shadowColor") ? ParseHexColor(data.Get<string>("shadowColor") ?? "#000000") : Color.Black,
                data.Parameters.ContainsKey("shadowOpacity") ? data.Get<float>("shadowOpacity") : 0.35f,
                data.Parameters.ContainsKey("shadowOffsetX") ? data.Get<float>("shadowOffsetX") : 4f,
                data.Parameters.ContainsKey("shadowOffsetY") ? data.Get<float>("shadowOffsetY") : 4f,
                data.Parameters.ContainsKey("shadowFalloff") ? data.Get<float>("shadowFalloff") : 8f,
                data.Parameters.ContainsKey("glowEnabled") && data.Get<bool>("glowEnabled"),
                data.Parameters.ContainsKey("glowColor") ? ParseHexColor(data.Get<string>("glowColor") ?? "#FFFF00") : Color.Yellow,
                data.Parameters.ContainsKey("glowOpacity") ? data.Get<float>("glowOpacity") : 0.5f,
                data.Parameters.ContainsKey("glowSize") ? data.Get<float>("glowSize") : 6f,
                data.Parameters.ContainsKey("constrainToPanel") ? data.Get<bool>("constrainToPanel") : true),

            "SetFloatingImageBounds" => new SetFloatingImageBoundsCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                new Rect(
                    data.Get<float>("x"),
                    data.Get<float>("y"),
                    data.Get<float>("width"),
                    data.Get<float>("height"))),

            "SetFloatingImagePanel" => new SetFloatingImagePanelCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Parameters.ContainsKey("panelId")
                    ? GetOptionalGuidParam(data, "panelId")
                    : data.Parameters.ContainsKey("value")
                        ? GetOptionalGuidParam(data, "value")
                        : null),

            "SetFloatingImageConstrainToPanel" => new SetFloatingImageConstrainToPanelCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Parameters.ContainsKey("constrainToPanel")
                    ? data.Get<bool>("constrainToPanel")
                    : data.Get<bool>("value")),

            "SetFloatingImageRotation" => new SetFloatingImageRotationCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Parameters.ContainsKey("rotation") ? data.Get<float>("rotation") : data.Get<float>("value")),

            "SetFloatingImageShadow" => new SetFloatingImageShadowCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Parameters.ContainsKey("enabled") ? data.Get<bool>("enabled") : data.Get<bool>("shadowEnabled"),
                data.Parameters.ContainsKey("color")
                    ? (ParseOptionalColor(data) ?? Color.Black)
                    : data.Parameters.ContainsKey("shadowColor")
                        ? ParseHexColor(data.Get<string>("shadowColor") ?? "#000000")
                        : Color.Black,
                data.Parameters.ContainsKey("opacity") ? data.Get<float>("opacity") : data.Get<float>("shadowOpacity"),
                data.Parameters.ContainsKey("offsetX") ? data.Get<float>("offsetX") : data.Get<float>("shadowOffsetX"),
                data.Parameters.ContainsKey("offsetY") ? data.Get<float>("offsetY") : data.Get<float>("shadowOffsetY"),
                data.Parameters.ContainsKey("falloff") ? data.Get<float>("falloff") : data.Get<float>("shadowFalloff")),

            "SetFloatingImageGlow" => new SetFloatingImageGlowCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Parameters.ContainsKey("enabled") ? data.Get<bool>("enabled") : data.Get<bool>("glowEnabled"),
                data.Parameters.ContainsKey("color")
                    ? (ParseOptionalColor(data) ?? Color.Yellow)
                    : data.Parameters.ContainsKey("glowColor")
                        ? ParseHexColor(data.Get<string>("glowColor") ?? "#FFFF00")
                        : Color.Yellow,
                data.Parameters.ContainsKey("opacity") ? data.Get<float>("opacity") : data.Get<float>("glowOpacity"),
                data.Parameters.ContainsKey("size") ? data.Get<float>("size") : data.Get<float>("glowSize")),

            "ReorderFloatingImage" => new ReorderFloatingImageCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Get<int>("newIndex")),

            "DeleteFloatingImage" => new DeleteFloatingImageCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId")),

            "RenameFloatingImage" => new RenameFloatingImageCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("imageId"),
                data.Get<string>("name")),

            "GroupObjects" => new GroupObjectsCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<List<Guid>>("balloonIds") ?? new List<Guid>(),
                data.Get<List<Guid>>("floatingImageIds") ?? new List<Guid>(),
                data.Parameters.ContainsKey("groupId") ? data.Get<Guid>("groupId") : null),

            "UngroupObjects" => new UngroupObjectsCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<List<Guid>>("balloonIds") ?? new List<Guid>(),
                data.Get<List<Guid>>("floatingImageIds") ?? new List<Guid>()),

            "SetPanelSafeMargin" => new SetPanelSafeMarginCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("panelId"),
                data.Parameters.ContainsKey("margin") ? data.Get<float>("margin") : data.Get<float>("value")),

            "SplitPanelZone" => new SplitPanelZoneCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("panelId"),
                Enum.TryParse<PanelSplitOrientation>(data.Get<string>("orientation"), out var splitOrientation)
                    ? splitOrientation
                    : PanelSplitOrientation.Horizontal,
                data.Parameters.ContainsKey("position") ? data.Get<float>("position") : data.Get<float>("value"),
                data.Parameters.ContainsKey("isPercentage") ? data.Get<bool>("isPercentage") : data.Get<bool>("percent")),

            "MergePanelZones" => new MergePanelZonesCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Parameters.ContainsKey("primaryPanelId") ? data.Get<Guid>("primaryPanelId")
                    : data.Parameters.ContainsKey("panelAId") ? data.Get<Guid>("panelAId")
                    : data.GetRequired<Guid>("panelId"),
                data.Parameters.ContainsKey("secondaryPanelId") ? data.Get<Guid>("secondaryPanelId")
                    : data.Parameters.ContainsKey("panelBId") ? data.Get<Guid>("panelBId")
                    : data.GetRequired<Guid>("otherPanelId")),

            "SetPanelGutterOverrides" => new SetPanelGutterOverridesCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("panelId"),
                data.Parameters.ContainsKey("left") ? data.Get<float?>("left") : data.Get<float?>("gutterLeft"),
                data.Parameters.ContainsKey("top") ? data.Get<float?>("top") : data.Get<float?>("gutterTop"),
                data.Parameters.ContainsKey("right") ? data.Get<float?>("right") : data.Get<float?>("gutterRight"),
                data.Parameters.ContainsKey("bottom") ? data.Get<float?>("bottom") : data.Get<float?>("gutterBottom")),

            "SetPanelBleed" => new SetPanelBleedCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("panelId"),
                data.Parameters.ContainsKey("left") ? data.Get<float>("left") : data.Get<float>("bleedLeft"),
                data.Parameters.ContainsKey("top") ? data.Get<float>("top") : data.Get<float>("bleedTop"),
                data.Parameters.ContainsKey("right") ? data.Get<float>("right") : data.Get<float>("bleedRight"),
                data.Parameters.ContainsKey("bottom") ? data.Get<float>("bottom") : data.Get<float>("bleedBottom")),

            "SetPanelGutterStyle" => new SetPanelGutterStyleCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                BuildGutterColorFromData(data, doc),
                Enum.TryParse<PanelBorderStyle>(data.Get<string>("style"), out var gutterStyle)
                    ? gutterStyle
                    : PanelBorderStyle.None,
                data.Parameters.ContainsKey("fillEnabled") ? data.Get<bool>("fillEnabled") : data.Get<bool>("fill")),

            "AlignPanels" => BuildPanelArrangeCommand(data, ParsePanelAlignOperation(data)),

            "DistributePanels" => BuildPanelArrangeCommand(data, ParsePanelDistributeOperation(data)),

            "MatchPanelSizes" => BuildPanelArrangeCommand(data, ParsePanelMatchOperation(data)),

            "CopyBalloonsToPage" => new CopyBalloonsToPageCommand(
                data.GetRequired<Guid>("sourcePageId"),
                data.GetRequired<Guid>("targetPageId"),
                data.Get<List<Guid>>("balloonIds") ?? new List<Guid>()),

            "DeletePage" => new DeletePageCommand(data.GetRequired<Guid>("pageId")),

            "DuplicatePage" => new DuplicatePageCommand(
                data.Parameters.ContainsKey("sourcePageId")
                    ? data.Get<Guid>("sourcePageId")
                    : data.Parameters.ContainsKey("pageId")
                        ? data.Get<Guid>("pageId")
                        : doc.ActivePageId,
                data.Parameters.ContainsKey("newName") ? data.Get<string>("newName") : null,
                GetOptionalGuidParam(data, "newPageId")),

            "ReorderPage" => new ReorderPageCommand(
                data.GetRequired<Guid>("pageId"),
                data.Get<int>("newIndex")),

            "RenamePage" => new RenamePageCommand(
                data.GetRequired<Guid>("pageId"),
                data.Get<string>("name") ?? "Page"),

            "SetActivePage" => new SetActivePageCommand(data.GetRequired<Guid>("pageId")),

            "SetPageReadingDirection" => new SetPageReadingDirectionCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                Enum.TryParse<ReadingDirection>(data.Get<string>("direction"), out var direction)
                    ? direction
                    : ReadingDirection.LeftToRight),

            "CreateGuide" => new CreateGuideCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                Enum.TryParse<GuideOrientation>(data.Get<string>("orientation"), out var orientation)
                    ? orientation
                    : GuideOrientation.Horizontal,
                data.Get<float>("position")),

            "MoveGuide" => new MoveGuideCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("guideId"),
                data.Get<float>("position")),

            "DeleteGuide" => new DeleteGuideCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.GetRequired<Guid>("guideId")),

            "SetGuidesLocked" => new SetGuidesLockedCommand(
                data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId,
                data.Get<bool>("locked")),

            "CreateNamedBalloonStyle" => new CreateNamedBalloonStyleCommand(
                data.Get<string>("name") ?? "Balloon Style",
                data.Get<BalloonStyle>("style") ?? BalloonStyle.Default,
                data.Parameters.ContainsKey("styleId") ? data.Get<Guid>("styleId") : null,
                data.Parameters.ContainsKey("parentStyleId") ? data.Get<Guid>("parentStyleId") : null,
                data.Get<BalloonStyleOverride>("overrides"),
                data.Parameters.ContainsKey("isQuickSelect") ? data.Get<bool>("isQuickSelect") : true,
                data.Parameters.ContainsKey("applyExtendedDetails") ? data.Get<bool>("applyExtendedDetails") : true,
                data.Get<BalloonShape>("shape"),
                data.Get<string>("customShapePathData"),
                data.Parameters.ContainsKey("constrainToPanel") && data.Get<bool>("constrainToPanel"),
                data.Get<TextStyle>("textStyle"),
                data.Get<TextPath>("textPath"),
                data.Get<List<BalloonTemplateTail>>("tails")),

            "UpdateNamedBalloonStyle" => new UpdateNamedBalloonStyleCommand(
                data.GetRequired<Guid>("styleId"),
                data.Get<BalloonStyle>("style") ?? BalloonStyle.Default,
                data.Parameters.ContainsKey("applyExtendedDetails") ? data.Get<bool>("applyExtendedDetails") : null,
                data.Parameters.ContainsKey("shape") ? data.Get<BalloonShape>("shape") : null,
                data.Parameters.ContainsKey("customShapePathData") ? data.Get<string>("customShapePathData") : null,
                data.Parameters.ContainsKey("customShapePathData"),
                data.Parameters.ContainsKey("constrainToPanel") ? data.Get<bool>("constrainToPanel") : null,
                data.Get<TextStyle>("textStyle"),
                data.Parameters.ContainsKey("textStyle"),
                data.Get<TextPath>("textPath"),
                data.Parameters.ContainsKey("textPath"),
                data.Get<List<BalloonTemplateTail>>("tails"),
                data.Parameters.ContainsKey("tails")),

            "RenameNamedBalloonStyle" => new RenameNamedBalloonStyleCommand(
                data.GetRequired<Guid>("styleId"),
                data.Get<string>("name") ?? "Balloon Style"),

            "DeleteNamedBalloonStyle" => new DeleteNamedBalloonStyleCommand(
                data.GetRequired<Guid>("styleId")),
            "SetNamedBalloonStyleQuickSelect" => new SetNamedBalloonStyleQuickSelectCommand(
                data.GetRequired<Guid>("styleId"),
                data.Get<bool>("isQuickSelect")),
            "SetNamedBalloonStyleParent" => new SetNamedBalloonStyleParentCommand(
                data.GetRequired<Guid>("styleId"),
                data.Parameters.ContainsKey("parentStyleId") ? data.Get<Guid>("parentStyleId") : null),

            "CreateNamedTextStyle" => new CreateNamedTextStyleCommand(
                data.Get<string>("name") ?? "Text Style",
                data.Get<TextStyle>("style") ?? TextStyle.Default,
                data.Parameters.ContainsKey("styleId") ? data.Get<Guid>("styleId") : null,
                data.Parameters.ContainsKey("parentStyleId") ? data.Get<Guid>("parentStyleId") : null,
                data.Get<TextStyleOverride>("overrides")),

            "UpdateNamedTextStyle" => new UpdateNamedTextStyleCommand(
                data.GetRequired<Guid>("styleId"),
                data.Get<TextStyle>("style") ?? TextStyle.Default),

            "RenameNamedTextStyle" => new RenameNamedTextStyleCommand(
                data.GetRequired<Guid>("styleId"),
                data.Get<string>("name") ?? "Text Style"),

            "DeleteNamedTextStyle" => new DeleteNamedTextStyleCommand(
                data.GetRequired<Guid>("styleId")),
            "SetNamedTextStyleParent" => new SetNamedTextStyleParentCommand(
                data.GetRequired<Guid>("styleId"),
                data.Parameters.ContainsKey("parentStyleId") ? data.Get<Guid>("parentStyleId") : null),

            "SetBalloonStyle" => new SetBalloonStyleCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<BalloonStyle>("style") ?? BalloonStyle.Default),

            "SetTextStyle" => new SetTextStyleCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Get<TextStyle>("style") ?? TextStyle.Default),

            "SetBalloonStyleReference" => new SetBalloonStyleReferenceCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("styleId") ? data.Get<Guid>("styleId") : null,
                data.Get<BalloonStyleOverride>("overrides")),

            "SetTextStyleReference" => new SetTextStyleReferenceCommand(
                data.GetRequired<Guid>("balloonId"),
                data.Parameters.ContainsKey("styleId") ? data.Get<Guid>("styleId") : null,
                data.Get<TextStyleOverride>("overrides")),

            _ => null
        };
    }

    private sealed class TranslationImportPayload
    {
        public string? Language { get; set; }
        public string? Format { get; set; }
        public string? Strategy { get; set; }
        public bool PreviewOnly { get; set; }
        public string? Content { get; set; }
        public List<TranslationImportEntryPayload>? Entries { get; set; }
    }

    private sealed class TranslationImportEntryPayload
    {
        public Guid BalloonId { get; set; }
        public string? Text { get; set; }
        public string? SourceTextSnapshot { get; set; }
        public string? Orientation { get; set; }
    }

    private sealed class ParsedTranslationImportEntry
    {
        public Guid BalloonId { get; init; }
        public string Text { get; init; } = string.Empty;
        public string? SourceTextSnapshot { get; init; }
        public TranslationTextOrientation? Orientation { get; init; }
    }

    private sealed class TranslationExportRow
    {
        public Guid BalloonId { get; init; }
        public string SourceText { get; init; } = string.Empty;
        public string TargetText { get; init; } = string.Empty;
        public bool Stale { get; init; }
        public TranslationTextOrientation Orientation { get; init; } = TranslationTextOrientation.Auto;
        public Guid? PageId { get; init; }
        public string? PageName { get; init; }
        public Guid? LayerId { get; init; }
        public string? LayerName { get; init; }
        public Guid? PanelId { get; init; }
        public int? ReadingOrder { get; init; }
    }

    private sealed class PublishingPreflightFixPayload
    {
        public string? FixId { get; set; }
        public List<string>? Languages { get; set; }
        public bool? IncludePrintPreparationChecks { get; set; }
        public string? ColorMode { get; set; }
        public string? IccProfileName { get; set; }
    }

    private async Task<object> HandlePublishingPreflightRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return new { success = false, error = "Publishing preflight endpoint requires GET method" };
        }

        var doc = _editorState.Document;
        if (doc == null)
        {
            return new { success = false, error = "No document loaded" };
        }

        var options = BuildPreflightOptionsFromRequest(request, null);
        var report = PublishingPreflightService.Analyze(doc, options);

        return new
        {
            success = true,
            data = new
            {
                languages = report.Languages,
                summary = new
                {
                    total = report.Issues.Count,
                    errors = report.Issues.Count(issue => issue.Severity == PublishingIssueSeverity.Error),
                    warnings = report.Issues.Count(issue => issue.Severity == PublishingIssueSeverity.Warning),
                    info = report.Issues.Count(issue => issue.Severity == PublishingIssueSeverity.Info)
                },
                issues = report.Issues.Select(issue => new
                {
                    code = issue.Code,
                    severity = issue.Severity.ToString(),
                    category = issue.Category.ToString(),
                    message = issue.Message,
                    suggestion = issue.Suggestion,
                    fixId = issue.FixId,
                    context = issue.Context
                }).ToArray(),
                fixes = report.FixSuggestions.Select(fix => new
                {
                    id = fix.FixId,
                    title = fix.Title,
                    estimatedCommandCount = fix.EstimatedCommandCount
                }).ToArray()
            }
        };
    }

    private async Task<object> HandlePublishingPreflightFixRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "POST")
        {
            return new { success = false, error = "Publishing preflight fix endpoint requires POST method" };
        }

        var doc = _editorState.Document;
        if (doc == null)
        {
            return new { success = false, error = "No document loaded" };
        }

        PublishingPreflightFixPayload payload;
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            payload = string.IsNullOrWhiteSpace(body)
                ? new PublishingPreflightFixPayload()
                : JsonSerializer.Deserialize<PublishingPreflightFixPayload>(body, AutomationCommandJsonOptions)
                    ?? new PublishingPreflightFixPayload();
        }
        catch (Exception ex)
        {
            return new { success = false, error = $"Invalid fix payload: {ex.Message}" };
        }

        var fixId = payload.FixId?.Trim();
        if (string.IsNullOrWhiteSpace(fixId))
        {
            fixId = request.QueryString["fixId"];
        }

        if (string.IsNullOrWhiteSpace(fixId))
        {
            return new { success = false, error = "Missing fixId" };
        }

        var options = BuildPreflightOptionsFromRequest(request, payload);
        var commands = PublishingPreflightService.BuildFixCommands(doc, fixId, options);
        if (commands.Count == 0)
        {
            return new { success = true, data = new { applied = false, fixId, commandCount = 0 } };
        }

        _editorState.ExecuteTransactionSafe($"Preflight fix ({fixId})", commands);

        return new
        {
            success = true,
            data = new
            {
                applied = true,
                fixId,
                commandCount = commands.Count
            }
        };
    }

    private Task<object> HandlePublishingWebPresetsRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return Task.FromResult<object>(new { success = false, error = "Web presets endpoint requires GET method" });
        }

        var preset = WebExportService.ResolvePreset(request.QueryString["preset"]);
        var width = (int)Math.Clamp(ParseQueryFloat(request.QueryString["width"], 0f), 0f, 12000f);
        var height = (int)Math.Clamp(ParseQueryFloat(request.QueryString["height"], 0f), 0f, 12000f);
        if (width <= 0 || height <= 0)
        {
            var doc = _editorState.Document;
            var activePage = doc?.ActivePage;
            width = activePage != null ? (int)Math.Max(1, Math.Round(activePage.Size.Width)) : 0;
            height = activePage != null ? (int)Math.Max(1, Math.Round(activePage.Size.Height)) : 0;
        }

        var targets = width > 0 && height > 0
            ? WebExportService.BuildResponsiveTargets(width, height, preset)
            : Array.Empty<WebResponsiveTarget>();

        return Task.FromResult<object>(new
        {
            success = true,
            data = new
            {
                presets = WebExportService.GetPresets().Select(item => new
                {
                    name = item.Name,
                    description = item.Description,
                    format = item.Format,
                    quality = item.Quality,
                    widths = item.Widths,
                    targetKilobytes = item.TargetKilobytes
                }).ToArray(),
                selected = new
                {
                    name = preset.Name,
                    description = preset.Description,
                    format = preset.Format,
                    quality = preset.Quality,
                    widths = preset.Widths
                },
                source = new { width, height },
                targets = targets.Select(target => new
                {
                    width = target.Width,
                    height = target.Height,
                    suffix = target.Suffix,
                    quality = target.Quality,
                    format = target.Format,
                    estimatedKilobytes = target.EstimatedKilobytes
                }).ToArray()
            }
        });
    }

    private Task<object> HandlePublishingDistributionRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return Task.FromResult<object>(new { success = false, error = "Distribution endpoint requires GET method" });
        }

        var doc = _editorState.Document;
        if (doc == null || doc.ActivePage == null)
        {
            return Task.FromResult<object>(new { success = false, error = "No document loaded" });
        }

        var language = Document.NormalizeLanguageTag(request.QueryString["language"], doc.ActiveLanguage);
        var guidedView = DigitalDistributionService.BuildGuidedViewManifest(doc.ActivePage, language);

        var presetName = request.QueryString["webtoonPreset"];
        var webtoonPreset = DigitalDistributionService.GetWebtoonPresets()
            .FirstOrDefault(item => string.Equals(item.Name, presetName, StringComparison.OrdinalIgnoreCase))
            ?? DigitalDistributionService.GetWebtoonPresets().First();
        var webtoonPlan = DigitalDistributionService.BuildWebtoonStripPlan(doc.Pages, webtoonPreset);

        var platform = request.QueryString["platform"] ?? "webtoon";
        var template = DigitalDistributionService.ResolveTemplate(platform);
        var packageEntries = (request.QueryString["files"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var packageIssues = DigitalDistributionService.ValidatePackage(platform, packageEntries);

        return Task.FromResult<object>(new
        {
            success = true,
            data = new
            {
                guidedView = new
                {
                    pageId = guidedView.PageId,
                    pageName = guidedView.PageName,
                    language = guidedView.Language,
                    panels = guidedView.Panels.Select(panel => new
                    {
                        panelId = panel.PanelId,
                        order = panel.Order,
                        x = panel.Bounds.X,
                        y = panel.Bounds.Y,
                        width = panel.Bounds.Width,
                        height = panel.Bounds.Height
                    }).ToArray()
                },
                webtoon = new
                {
                    preset = webtoonPlan.PresetName,
                    totalWidth = webtoonPlan.TotalWidth,
                    totalHeight = webtoonPlan.TotalHeight,
                    segmentCount = webtoonPlan.SegmentCount,
                    placements = webtoonPlan.Placements.Select(placement => new
                    {
                        pageId = placement.PageId,
                        pageName = placement.PageName,
                        y = placement.Y,
                        width = placement.Width,
                        height = placement.Height,
                        segmentIndex = placement.SegmentIndex
                    }).ToArray()
                },
                platformTemplate = template == null
                    ? null
                    : new
                    {
                        platform = template.Platform,
                        container = template.ContainerFormat,
                        requiredFiles = template.RequiredFiles,
                        metadataTemplate = template.MetadataTemplate
                    },
                packageValidation = new
                {
                    issueCount = packageIssues.Count,
                    issues = packageIssues.Select(issue => new
                    {
                        code = issue.Code,
                        severity = issue.Severity.ToString(),
                        message = issue.Message,
                        suggestion = issue.Suggestion
                    }).ToArray()
                }
            }
        });
    }

    private Task<object> HandlePrintingPlanRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return Task.FromResult<object>(new { success = false, error = "Printing plan endpoint requires GET method" });
        }

        var doc = _editorState.Document;
        if (doc == null || doc.ActivePage == null)
        {
            return Task.FromResult<object>(new { success = false, error = "No document loaded" });
        }

        var bleed = ParseQueryFloat(request.QueryString["bleed"], 12f);
        var safe = ParseQueryFloat(request.QueryString["safe"], 18f);
        var pagesPerSheet = (int)Math.Clamp(ParseQueryFloat(request.QueryString["pagesPerSheet"], 2f), 1f, 16f);

        var boxes = PrintPreparationService.BuildPageBoxes(doc.ActivePage, bleed, safe);
        var imposition = PrintPreparationService.BuildImposition(doc, pagesPerSheet);

        return Task.FromResult<object>(new
        {
            success = true,
            data = new
            {
                page = new
                {
                    id = doc.ActivePage.Id,
                    name = doc.ActivePage.Name,
                    trim = new { x = boxes.Trim.X, y = boxes.Trim.Y, width = boxes.Trim.Width, height = boxes.Trim.Height },
                    bleed = new { x = boxes.Bleed.X, y = boxes.Bleed.Y, width = boxes.Bleed.Width, height = boxes.Bleed.Height },
                    safe = new { x = boxes.Safe.X, y = boxes.Safe.Y, width = boxes.Safe.Width, height = boxes.Safe.Height }
                },
                imposition = imposition.Select(sheet => new
                {
                    sheetNumber = sheet.SheetNumber,
                    placements = sheet.Placements.Select(placement => new
                    {
                        slot = placement.Slot,
                        pageId = placement.PageId,
                        pageName = placement.PageName
                    }).ToArray()
                }).ToArray()
            }
        });
    }

    private PublishingPreflightOptions BuildPreflightOptionsFromRequest(HttpListenerRequest request, PublishingPreflightFixPayload? payload)
    {
        IReadOnlyList<string> languages = payload?.Languages ?? ParseLanguageList(request.QueryString["languages"]);
        var includePrint = payload?.IncludePrintPreparationChecks ?? ParseQueryBool(request.QueryString["includePrint"], true);
        var colorModeRaw = payload?.ColorMode ?? request.QueryString["colorMode"];
        var colorMode = string.Equals(colorModeRaw, "cmyk", StringComparison.OrdinalIgnoreCase)
            ? PdfColorMode.Cmyk
            : PdfColorMode.Rgb;
        var icc = payload?.IccProfileName ?? request.QueryString["icc"];

        return new PublishingPreflightOptions
        {
            Languages = languages,
            IncludePrintPreparationChecks = includePrint,
            PrintColorMode = colorMode,
            IccProfileName = icc ?? string.Empty,
            MinimumTextPointSize = ParseQueryFloat(request.QueryString["minTextPt"], 8f),
            MinimumStrokeWidth = ParseQueryFloat(request.QueryString["minStrokePx"], 0.7f),
            RecommendedSafeMargin = ParseQueryFloat(request.QueryString["safeMargin"], 18f),
            MinimumBleed = ParseQueryFloat(request.QueryString["minBleed"], 12f),
            InkCoverageWarningThreshold = ParseQueryFloat(request.QueryString["inkCoverage"], 280f)
        };
    }

    private Task<object> HandleTranslationsExportRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return Task.FromResult<object>(new { success = false, error = "Translations export endpoint requires GET method" });
        }

        var doc = _editorState.Document;
        if (doc == null)
        {
            return Task.FromResult<object>(new { success = false, error = "No document loaded" });
        }

        var format = (request.QueryString["format"] ?? "json").Trim().ToLowerInvariant();
        var language = Document.NormalizeLanguageTag(request.QueryString["language"], doc.ActiveLanguage);
        var includeContext = ParseQueryBool(request.QueryString["includeContext"], true);

        var rows = BuildTranslationExportRows(doc, language, includeContext).ToArray();
        var summary = new
        {
            total = rows.Length,
            untranslated = rows.Count(row => string.IsNullOrWhiteSpace(row.TargetText)),
            stale = rows.Count(row => row.Stale)
        };

        return format switch
        {
            "json" => Task.FromResult<object>(new
            {
                success = true,
                data = new
                {
                    format,
                    language,
                    summary,
                    entries = rows.Select(row => new
                    {
                        balloonId = row.BalloonId,
                        sourceText = row.SourceText,
                        targetText = row.TargetText,
                        stale = row.Stale,
                        orientation = row.Orientation,
                        pageId = row.PageId,
                        pageName = row.PageName,
                        layerId = row.LayerId,
                        layerName = row.LayerName,
                        panelId = row.PanelId,
                        readingOrder = row.ReadingOrder
                    }).ToArray()
                }
            }),
            "csv" => Task.FromResult<object>(new
            {
                success = true,
                data = new
                {
                    format,
                    language,
                    summary,
                    content = BuildTranslationCsv(rows, includeContext)
                }
            }),
            "xliff" or "xlf" => Task.FromResult<object>(new
            {
                success = true,
                data = new
                {
                    format = "xliff",
                    language,
                    summary,
                    content = BuildTranslationXliff(rows, doc.BaseLanguage, language)
                }
            }),
            _ => Task.FromResult<object>(new { success = false, error = $"Unsupported translation export format: {format}" })
        };
    }

    private async Task<object> HandleTranslationsImportRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "POST")
        {
            return new { success = false, error = "Translations import endpoint requires POST method" };
        }

        var doc = _editorState.Document;
        if (doc == null)
        {
            return new { success = false, error = "No document loaded" };
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                return new { success = false, error = "Empty import payload" };
            }

            var payload = JsonSerializer.Deserialize<TranslationImportPayload>(body, AutomationCommandJsonOptions)
                ?? throw new InvalidOperationException("Invalid import payload");
            var language = Document.NormalizeLanguageTag(payload.Language, doc.ActiveLanguage);
            if (string.Equals(language, doc.BaseLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return new { success = false, error = "Translation import language cannot match the base language." };
            }

            var mergeStrategy = (payload.Strategy ?? "replace").Trim().ToLowerInvariant();
            if (mergeStrategy is not ("replace" or "keepexisting" or "fillmissing"))
            {
                return new { success = false, error = $"Unsupported merge strategy: {mergeStrategy}" };
            }

            var entriesResult = TryParseTranslationImportEntries(payload, out var parsedEntries, out var detectedFormat, out var parseError);
            if (!entriesResult)
            {
                return new { success = false, error = parseError ?? "Failed to parse import entries" };
            }

            var balloonMap = doc.Pages
                .SelectMany(page => page.Layers)
                .Where(layer => layer.CanContainBalloons)
                .SelectMany(layer => layer.Balloons)
                .ToDictionary(balloon => balloon.Id, balloon => balloon);

            var duplicateIds = parsedEntries
                .GroupBy(entry => entry.BalloonId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            var unknownIds = parsedEntries
                .Where(entry => !balloonMap.ContainsKey(entry.BalloonId))
                .Select(entry => entry.BalloonId)
                .Distinct()
                .ToArray();

            var validEntries = parsedEntries
                .Where(entry => balloonMap.ContainsKey(entry.BalloonId))
                .GroupBy(entry => entry.BalloonId)
                .Select(group => group.Last())
                .ToArray();

            var plannedChanges = new List<object>();
            var commands = new List<ICommand>();
            foreach (var entry in validEntries)
            {
                var balloon = balloonMap[entry.BalloonId];
                var oldText = doc.GetBalloonTranslationText(balloon, language) ?? string.Empty;
                var isMissing = string.IsNullOrWhiteSpace(oldText);
                var shouldApply = mergeStrategy switch
                {
                    "keepexisting" => isMissing,
                    "fillmissing" => isMissing,
                    _ => true
                };

                var willChange = shouldApply && !string.Equals(oldText, entry.Text, StringComparison.Ordinal);
                plannedChanges.Add(new
                {
                    balloonId = entry.BalloonId,
                    oldText,
                    newText = entry.Text,
                    orientation = entry.Orientation,
                    willChange,
                    skippedByStrategy = !shouldApply
                });

                if (willChange)
                {
                    commands.Add(new SetBalloonTranslationCommand(
                        entry.BalloonId,
                        language,
                        entry.Text,
                        entry.SourceTextSnapshot ?? balloon.Text,
                        entry.Orientation));
                }
            }

            if (!payload.PreviewOnly && commands.Count > 0)
            {
                _editorState.ExecuteTransactionSafe(
                    $"Import translations ({language})",
                    commands);
            }

            return new
            {
                success = true,
                data = new
                {
                    format = detectedFormat,
                    language,
                    mergeStrategy,
                    previewOnly = payload.PreviewOnly,
                    importedEntries = validEntries.Length,
                    changesApplied = payload.PreviewOnly ? 0 : commands.Count,
                    changesPlanned = commands.Count,
                    duplicateIds,
                    unknownIds,
                    changes = plannedChanges
                }
            };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private Task<object> HandleTranslationsQaRequest(HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return Task.FromResult<object>(new { success = false, error = "Translations QA endpoint requires GET method" });
        }

        var doc = _editorState.Document;
        if (doc == null)
        {
            return Task.FromResult<object>(new { success = false, error = "No document loaded" });
        }

        var language = Document.NormalizeLanguageTag(request.QueryString["language"], doc.ActiveLanguage);
        var expansionThreshold = ParseQueryFloat(request.QueryString["expansionThreshold"], 1.35f);
        var maxItems = (int)Math.Clamp(ParseQueryFloat(request.QueryString["maxItems"], 100f), 1f, 1000f);

        var qaRows = BuildTranslationExportRows(doc, language, includeContext: true)
            .Select(row =>
            {
                var sourceLength = Math.Max(1, row.SourceText.Length);
                var targetLength = row.TargetText.Length;
                var ratio = targetLength / (float)sourceLength;
                var placeholderMismatch = HasPlaceholderMismatch(row.SourceText, row.TargetText);
                return new
                {
                    balloonId = row.BalloonId,
                    pageId = row.PageId,
                    pageName = row.PageName,
                    layerId = row.LayerId,
                    layerName = row.LayerName,
                    sourceText = row.SourceText,
                    targetText = row.TargetText,
                    missing = string.IsNullOrWhiteSpace(row.TargetText),
                    stale = row.Stale,
                    sourceLength = row.SourceText.Length,
                    targetLength = row.TargetText.Length,
                    lengthDelta = row.TargetText.Length - row.SourceText.Length,
                    expansionRatio = ratio,
                    overflowRisk = ratio > expansionThreshold,
                    placeholderMismatch
                };
            })
            .ToArray();

        var missing = qaRows.Where(row => row.missing).Take(maxItems).ToArray();
        var stale = qaRows.Where(row => row.stale).Take(maxItems).ToArray();
        var overflowRisk = qaRows.Where(row => row.overflowRisk).Take(maxItems).ToArray();
        var placeholderIssues = qaRows.Where(row => row.placeholderMismatch).Take(maxItems).ToArray();

        return Task.FromResult<object>(new
        {
            success = true,
            data = new
            {
                language,
                threshold = expansionThreshold,
                totals = new
                {
                    balloons = qaRows.Length,
                    missing = qaRows.Count(row => row.missing),
                    stale = qaRows.Count(row => row.stale),
                    overflowRisk = qaRows.Count(row => row.overflowRisk),
                    placeholderMismatch = qaRows.Count(row => row.placeholderMismatch)
                },
                missing,
                stale,
                overflowRisk,
                placeholderIssues,
                translationMemory = new
                {
                    status = "future",
                    note = "Translation memory integration is not implemented yet."
                }
            }
        });
    }

    private static IEnumerable<TranslationExportRow> BuildTranslationExportRows(Document doc, string language, bool includeContext)
    {
        foreach (var page in doc.Pages)
        {
            foreach (var layer in page.Layers)
            {
                if (!layer.CanContainBalloons) continue;
                foreach (var balloon in layer.Balloons)
                {
                    var targetText = doc.GetBalloonTranslationText(balloon, language) ?? string.Empty;
                    var orientation = balloon.Translations.TryGetValue(language, out var translation)
                        ? translation.Orientation
                        : TranslationTextOrientation.Auto;
                    yield return new TranslationExportRow
                    {
                        BalloonId = balloon.Id,
                        SourceText = balloon.Text,
                        TargetText = targetText,
                        Stale = doc.IsBalloonTranslationStale(balloon, language),
                        Orientation = orientation,
                        PageId = includeContext ? page.Id : null,
                        PageName = includeContext ? page.Name : null,
                        LayerId = includeContext ? layer.Id : null,
                        LayerName = includeContext ? layer.Name : null,
                        PanelId = includeContext ? balloon.PanelId : null,
                        ReadingOrder = includeContext ? page.GetBalloonReadingOrder(balloon) : null
                    };
                }
            }
        }
    }

    private static string BuildTranslationCsv(IEnumerable<TranslationExportRow> rows, bool includeContext)
    {
        var columns = includeContext
            ? new[] { "balloonId", "sourceText", "targetText", "stale", "orientation", "pageId", "pageName", "layerId", "layerName", "panelId", "readingOrder" }
            : new[] { "balloonId", "sourceText", "targetText", "stale", "orientation" };
        var lines = new List<string> { string.Join(",", columns) };

        foreach (var row in rows)
        {
            var values = includeContext
                ? new[]
                {
                    row.BalloonId.ToString(),
                    row.SourceText,
                    row.TargetText,
                    row.Stale.ToString(),
                    row.Orientation.ToString(),
                    row.PageId?.ToString() ?? string.Empty,
                    row.PageName ?? string.Empty,
                    row.LayerId?.ToString() ?? string.Empty,
                    row.LayerName ?? string.Empty,
                    row.PanelId?.ToString() ?? string.Empty,
                    row.ReadingOrder?.ToString() ?? string.Empty
                }
                : new[]
                {
                    row.BalloonId.ToString(),
                    row.SourceText,
                    row.TargetText,
                    row.Stale.ToString(),
                    row.Orientation.ToString()
                };

            lines.Add(string.Join(",", values.Select(EscapeCsv)));
        }

        return string.Join("\n", lines);
    }

    private static string BuildTranslationXliff(IEnumerable<TranslationExportRow> rows, string sourceLanguage, string targetLanguage)
    {
        var units = rows.Select(row =>
        {
            var transUnit = new XElement(
                "trans-unit",
                new XAttribute("id", row.BalloonId),
                new XAttribute("x-letterist-orientation", row.Orientation.ToString()),
                new XElement("source", row.SourceText),
                new XElement("target", row.TargetText));

            if (!string.IsNullOrWhiteSpace(row.PageName) || !string.IsNullOrWhiteSpace(row.LayerName) || row.PanelId.HasValue)
            {
                transUnit.Add(
                    new XElement(
                        "context-group",
                        new XElement("context", new XAttribute("context-type", "x-page"), row.PageName ?? string.Empty),
                        new XElement("context", new XAttribute("context-type", "x-layer"), row.LayerName ?? string.Empty),
                        new XElement("context", new XAttribute("context-type", "x-panel"), row.PanelId?.ToString() ?? string.Empty)));
            }

            return transUnit;
        });

        var document = new XDocument(
            new XElement(
                "xliff",
                new XAttribute("version", "1.2"),
                new XElement(
                    "file",
                    new XAttribute("source-language", sourceLanguage),
                    new XAttribute("target-language", targetLanguage),
                    new XAttribute("datatype", "plaintext"),
                    new XAttribute("original", "letterist"),
                    new XElement("body", units))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static bool TryParseTranslationImportEntries(
        TranslationImportPayload payload,
        out List<ParsedTranslationImportEntry> entries,
        out string format,
        out string? error)
    {
        entries = new List<ParsedTranslationImportEntry>();
        error = null;
        format = (payload.Format ?? string.Empty).Trim().ToLowerInvariant();

        if (payload.Entries != null && payload.Entries.Count > 0)
        {
            entries.AddRange(payload.Entries.Select(entry => new ParsedTranslationImportEntry
            {
                BalloonId = entry.BalloonId,
                Text = entry.Text ?? string.Empty,
                SourceTextSnapshot = entry.SourceTextSnapshot,
                Orientation = TryParseTranslationOrientation(entry.Orientation, out var orientation)
                    ? (TranslationTextOrientation?)orientation
                    : null
            }));
            format = string.IsNullOrWhiteSpace(format) ? "json" : format;
            return true;
        }

        if (string.IsNullOrWhiteSpace(payload.Content))
        {
            error = "Import payload must include either entries[] or content.";
            return false;
        }

        var content = payload.Content;
        if (string.IsNullOrWhiteSpace(format))
        {
            format = DetectImportFormat(content);
        }

        try
        {
            switch (format)
            {
                case "json":
                {
                    var parsed = JsonSerializer.Deserialize<List<TranslationImportEntryPayload>>(content, AutomationCommandJsonOptions)
                        ?? new List<TranslationImportEntryPayload>();
                    entries.AddRange(parsed.Select(entry => new ParsedTranslationImportEntry
                    {
                        BalloonId = entry.BalloonId,
                        Text = entry.Text ?? string.Empty,
                        SourceTextSnapshot = entry.SourceTextSnapshot,
                        Orientation = TryParseTranslationOrientation(entry.Orientation, out var orientation)
                            ? (TranslationTextOrientation?)orientation
                            : null
                    }));
                    return true;
                }
                case "csv":
                {
                    var lines = content
                        .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                    if (lines.Count == 0)
                    {
                        return true;
                    }

                    var headers = ParseCsvLine(lines[0])
                        .Select((value, index) => new { value = value.Trim(), index })
                        .ToDictionary(item => item.value, item => item.index, StringComparer.OrdinalIgnoreCase);
                    if (!headers.TryGetValue("balloonId", out var balloonIdIndex) || !headers.TryGetValue("targetText", out var targetTextIndex))
                    {
                        error = "CSV import requires 'balloonId' and 'targetText' columns.";
                        return false;
                    }

                    var sourceSnapshotIndex = -1;
                    headers.TryGetValue("sourceTextSnapshot", out sourceSnapshotIndex);
                    var orientationIndex = -1;
                    headers.TryGetValue("orientation", out orientationIndex);

                    for (int i = 1; i < lines.Count; i++)
                    {
                        var columns = ParseCsvLine(lines[i]);
                        if (balloonIdIndex >= columns.Count || !Guid.TryParse(columns[balloonIdIndex], out var balloonId))
                        {
                            continue;
                        }

                        var text = targetTextIndex < columns.Count ? columns[targetTextIndex] : string.Empty;
                        var sourceTextSnapshot = sourceSnapshotIndex >= 0 && sourceSnapshotIndex < columns.Count
                            ? columns[sourceSnapshotIndex]
                            : null;
                        var orientation = orientationIndex >= 0 && orientationIndex < columns.Count &&
                                          TryParseTranslationOrientation(columns[orientationIndex], out var parsedOrientation)
                            ? (TranslationTextOrientation?)parsedOrientation
                            : null;
                        entries.Add(new ParsedTranslationImportEntry
                        {
                            BalloonId = balloonId,
                            Text = text,
                            SourceTextSnapshot = sourceTextSnapshot,
                            Orientation = orientation
                        });
                    }

                    return true;
                }
                case "xliff":
                case "xlf":
                {
                    var doc = XDocument.Parse(content);
                    var units = doc.Descendants()
                        .Where(node => string.Equals(node.Name.LocalName, "trans-unit", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    foreach (var unit in units)
                    {
                        var idValue = unit.Attribute("id")?.Value;
                        if (!Guid.TryParse(idValue, out var balloonId))
                        {
                            continue;
                        }

                        var target = unit.Descendants()
                            .FirstOrDefault(node => string.Equals(node.Name.LocalName, "target", StringComparison.OrdinalIgnoreCase))
                            ?.Value ?? string.Empty;
                        var source = unit.Descendants()
                            .FirstOrDefault(node => string.Equals(node.Name.LocalName, "source", StringComparison.OrdinalIgnoreCase))
                            ?.Value;
                        var orientation = TryParseTranslationOrientation(unit.Attribute("x-letterist-orientation")?.Value, out var parsedOrientation)
                            ? (TranslationTextOrientation?)parsedOrientation
                            : null;

                        entries.Add(new ParsedTranslationImportEntry
                        {
                            BalloonId = balloonId,
                            Text = target,
                            SourceTextSnapshot = source,
                            Orientation = orientation
                        });
                    }

                    return true;
                }
                default:
                    error = $"Unsupported import format: {format}";
                    return false;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string DetectImportFormat(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return "xliff";
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return "json";
        }

        return "csv";
    }

    private static bool TryParseTranslationOrientation(string? value, out TranslationTextOrientation orientation)
    {
        if (Enum.TryParse<TranslationTextOrientation>(value ?? string.Empty, true, out orientation))
        {
            return true;
        }

        orientation = TranslationTextOrientation.Auto;
        return false;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        if (line == null)
        {
            return values;
        }

        var current = new StringBuilder();
        var inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString());
        return values;
    }

    private static bool ParseQueryBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return bool.TryParse(raw, out var parsed) ? parsed : defaultValue;
    }

    private static float ParseQueryFloat(string? raw, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        return float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)
            ? current
            : defaultValue;
    }

    private static IReadOnlyList<string> ParseLanguageList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<object?> HandlePrintPreviewRequest(HttpListenerResponse response, HttpListenerRequest request)
    {
        if (request.HttpMethod != "GET")
        {
            return new { success = false, error = "Print preview endpoint requires GET method" };
        }

        var doc = _editorState.Document;
        if (doc == null || doc.ActivePage == null)
        {
            return new { success = false, error = "No document loaded" };
        }

        var drawBoxes = ParseQueryBool(request.QueryString["boxes"], true);
        var cmykPreview = ParseQueryBool(request.QueryString["cmykPreview"], false);
        var bleed = ParseQueryFloat(request.QueryString["bleed"], 12f);
        var safe = ParseQueryFloat(request.QueryString["safe"], 18f);

        try
        {
            var tcs = new TaskCompletionSource<byte[]?>();

            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var page = doc.ActivePage!;
                    await EnsureBackgroundLoadedAsync(page);
                    await EnsureFloatingImagesLoadedAsync(page);

                    var width = Math.Max(1, (int)Math.Ceiling(page.Size.Width));
                    var height = Math.Max(1, (int)Math.Ceiling(page.Size.Height));
                    using var renderTarget = new CanvasRenderTarget(MainCanvas.Device, width, height, 96);
                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Windows.UI.Color.FromArgb(255, 255, 255, 255));

                        if (_renderer != null)
                        {
                            _renderer.RenderPageContent(
                                ds,
                                page,
                                _editorState.GetBackgroundImageForPage(page.Id),
                                includeHiddenLayers: false,
                                singleLayerId: null,
                                panelImageResolver: GetPanelImage,
                                floatingImageResolver: GetFloatingImage,
                                textFillImageResolver: GetTextFillImage,
                                translationDocument: doc);
                        }

                        if (cmykPreview)
                        {
                            ds.FillRectangle(
                                0,
                                0,
                                width,
                                height,
                                Windows.UI.Color.FromArgb(32, 25, 18, 8));
                        }

                        if (drawBoxes)
                        {
                            var boxes = PrintPreparationService.BuildPageBoxes(page, bleed, safe);
                            DrawPrintPreviewBox(ds, boxes.Trim, Windows.UI.Color.FromArgb(220, 220, 40, 40), 2f);
                            DrawPrintPreviewBox(ds, boxes.Safe, Windows.UI.Color.FromArgb(220, 30, 150, 60), 2f);
                            DrawPrintPreviewBox(ds, boxes.Bleed, Windows.UI.Color.FromArgb(220, 45, 110, 220), 2f);
                        }
                    }

                    using var stream = new InMemoryRandomAccessStream();
                    await renderTarget.SaveAsync(stream, CanvasBitmapFileFormat.Png);
                    stream.Seek(0);
                    var bytes = new byte[stream.Size];
                    await stream.AsStreamForRead().ReadExactlyAsync(bytes);
                    tcs.SetResult(bytes);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            var pngBytes = await tcs.Task;
            if (pngBytes != null)
            {
                response.ContentType = "image/png";
                response.ContentLength64 = pngBytes.Length;
                await response.OutputStream.WriteAsync(pngBytes);
                return null;
            }

            return new { success = false, error = "Failed to render print preview" };
        }
        catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private static void DrawPrintPreviewBox(Microsoft.Graphics.Canvas.CanvasDrawingSession ds, Rect rect, Windows.UI.Color color, float strokeWidth)
    {
        ds.DrawRectangle(
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            color,
            strokeWidth);
    }

    private static bool HasPlaceholderMismatch(string source, string target)
    {
        var sourceTokens = ExtractPlaceholders(source);
        var targetTokens = ExtractPlaceholders(target);
        if (sourceTokens.Count != targetTokens.Count)
        {
            return true;
        }

        for (int i = 0; i < sourceTokens.Count; i++)
        {
            if (!string.Equals(sourceTokens[i], targetTokens[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ExtractPlaceholders(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        var pattern = @"(\{[^\}]+\}|%\d*\$?[a-zA-Z]|<[^>]+>)";
        return Regex.Matches(text, pattern)
            .Select(match => match.Value)
            .ToList();
    }

    private Task<object> HandleUndoRequest()
    {
        var tcs = new TaskCompletionSource<object>();

        DispatcherQueue.TryEnqueue(() =>
        {
            var success = _editorState.Undo();
            tcs.SetResult(new { success, data = new { undone = success } });
        });

        return tcs.Task;
    }

    private Task<object> HandleRedoRequest()
    {
        var tcs = new TaskCompletionSource<object>();

        DispatcherQueue.TryEnqueue(() =>
        {
            var success = _editorState.Redo();
            tcs.SetResult(new { success, data = new { redone = success } });
        });

        return tcs.Task;
    }

    private static async Task WriteJsonResponse(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static Model.Color BuildPanelColorFromData(CommandData data)
    {
        var defaults = PanelZone.DefaultColor;
        var r = data.Parameters.ContainsKey("colorR") ? (byte)data.Get<int>("colorR") : defaults.R;
        var g = data.Parameters.ContainsKey("colorG") ? (byte)data.Get<int>("colorG") : defaults.G;
        var b = data.Parameters.ContainsKey("colorB") ? (byte)data.Get<int>("colorB") : defaults.B;
        var a = data.Parameters.ContainsKey("colorA") ? (byte)data.Get<int>("colorA") : defaults.A;
        return new Model.Color(r, g, b, a);
    }

    private static Model.Color? ParseOptionalColor(CommandData data)
    {
        if (!data.Parameters.ContainsKey("color")) return null;

        var typedColor = data.Get<Model.Color?>("color");
        if (typedColor.HasValue)
        {
            return typedColor;
        }

        var colorString = data.Get<string>("color");
        if (string.IsNullOrWhiteSpace(colorString) ||
            string.Equals(colorString, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ParseHexColor(colorString);
    }

    private static Model.Color BuildGutterColorFromData(CommandData data, Document doc)
    {
        var page = doc.ActivePage;
        var defaults = page?.PanelGutterColor ?? new Model.Color(30, 30, 30, 200);
        var r = data.Parameters.ContainsKey("colorR") ? (byte)data.Get<int>("colorR") : defaults.R;
        var g = data.Parameters.ContainsKey("colorG") ? (byte)data.Get<int>("colorG") : defaults.G;
        var b = data.Parameters.ContainsKey("colorB") ? (byte)data.Get<int>("colorB") : defaults.B;
        var a = data.Parameters.ContainsKey("colorA") ? (byte)data.Get<int>("colorA") : defaults.A;
        return new Model.Color(r, g, b, a);
    }

    private static PanelLayoutTemplate? BuildPanelLayoutTemplateFromData(CommandData data)
    {
        if (!data.Parameters.TryGetValue("template", out var raw) || raw == null) return null;

        if (raw is PanelLayoutTemplateFile templateFile)
        {
            return templateFile.ToTemplate();
        }

        if (raw is JsonElement element)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            var parsed = JsonSerializer.Deserialize<PanelLayoutTemplateFile>(element.GetRawText(), options);
            return parsed?.ToTemplate();
        }

        return null;
    }

    private static BalloonTemplate? BuildBalloonTemplateFromData(CommandData data)
    {
        if (!data.Parameters.TryGetValue("template", out var raw) || raw == null) return null;

        if (raw is BalloonTemplateFile templateFile)
        {
            return templateFile.ToTemplate();
        }

        if (raw is JsonElement element)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            };
            var parsed = JsonSerializer.Deserialize<BalloonTemplateFile>(element.GetRawText(), options);
            return parsed?.ToTemplate();
        }

        return null;
    }

    private ICommand BuildPanelArrangeCommand(CommandData data, PanelArrangeOperation operation)
    {
        var doc = _editorState.Document ?? throw new InvalidOperationException("No document loaded");
        var pageId = data.Parameters.ContainsKey("pageId") ? data.Get<Guid>("pageId") : doc.ActivePageId;
        var page = doc.FindPage(pageId) ?? throw new InvalidOperationException($"Page {pageId} not found");

        IReadOnlyCollection<Guid>? panelIds = null;
        if (data.Parameters.ContainsKey("panelIds"))
        {
            panelIds = data.Get<List<Guid>>("panelIds") ?? new List<Guid>();
        }
        else if (data.Parameters.ContainsKey("panelId"))
        {
            panelIds = new List<Guid> { data.GetRequired<Guid>("panelId") };
        }

        if (panelIds != null && panelIds.Count == 0)
        {
            throw new InvalidOperationException("panelIds cannot be empty for panel arrange commands.");
        }

        var minCount = operation switch
        {
            PanelArrangeOperation.DistributeHorizontal or PanelArrangeOperation.DistributeVertical => 3,
            _ => 2
        };

        if (!TryGetPanelArrangeTargets(out _, out var panels, minCount, panelIds, allowFallbackToAll: panelIds == null, pageOverride: page))
        {
            throw new InvalidOperationException("Not enough panels to arrange.");
        }

        var referencePanelId = data.Parameters.ContainsKey("referencePanelId")
            ? data.Get<Guid>("referencePanelId")
            : (Guid?)null;

        var commands = BuildPanelArrangeCommands(page, panels, operation, referencePanelId);
        return new CompositeCommand(GetPanelArrangeDescription(operation), commands.ToArray());
    }

    private static string GetPanelArrangeDescription(PanelArrangeOperation operation)
    {
        return operation switch
        {
            PanelArrangeOperation.AlignLeft => "Align panels left",
            PanelArrangeOperation.AlignCenter => "Align panels center",
            PanelArrangeOperation.AlignRight => "Align panels right",
            PanelArrangeOperation.AlignTop => "Align panels top",
            PanelArrangeOperation.AlignMiddle => "Align panels middle",
            PanelArrangeOperation.AlignBottom => "Align panels bottom",
            PanelArrangeOperation.DistributeHorizontal => "Distribute panels horizontally",
            PanelArrangeOperation.DistributeVertical => "Distribute panels vertically",
            PanelArrangeOperation.MatchWidth => "Match panel widths",
            PanelArrangeOperation.MatchHeight => "Match panel heights",
            PanelArrangeOperation.MatchSize => "Match panel sizes",
            _ => "Arrange panels"
        };
    }

    private static PanelArrangeOperation ParsePanelAlignOperation(CommandData data)
    {
        var raw = data.Get<string>("align") ?? data.Get<string>("alignment") ?? data.Get<string>("mode") ?? "left";
        return raw.ToLowerInvariant() switch
        {
            "left" => PanelArrangeOperation.AlignLeft,
            "center" => PanelArrangeOperation.AlignCenter,
            "right" => PanelArrangeOperation.AlignRight,
            "top" => PanelArrangeOperation.AlignTop,
            "middle" => PanelArrangeOperation.AlignMiddle,
            "bottom" => PanelArrangeOperation.AlignBottom,
            _ => throw new InvalidOperationException($"Unknown align mode: {raw}")
        };
    }

    private static PanelArrangeOperation ParsePanelDistributeOperation(CommandData data)
    {
        var raw = data.Get<string>("direction") ?? data.Get<string>("axis") ?? data.Get<string>("mode") ?? "horizontal";
        return raw.ToLowerInvariant() switch
        {
            "horizontal" => PanelArrangeOperation.DistributeHorizontal,
            "vertical" => PanelArrangeOperation.DistributeVertical,
            _ => throw new InvalidOperationException($"Unknown distribute direction: {raw}")
        };
    }

    private static PanelArrangeOperation ParsePanelMatchOperation(CommandData data)
    {
        var raw = data.Get<string>("dimension") ?? data.Get<string>("mode") ?? data.Get<string>("size") ?? "both";
        return raw.ToLowerInvariant() switch
        {
            "width" => PanelArrangeOperation.MatchWidth,
            "height" => PanelArrangeOperation.MatchHeight,
            "both" => PanelArrangeOperation.MatchSize,
            "size" => PanelArrangeOperation.MatchSize,
            _ => throw new InvalidOperationException($"Unknown match mode: {raw}")
        };
    }


}
