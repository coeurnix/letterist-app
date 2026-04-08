using Letterist.Model;
using Letterist.View;
using Letterist.Rendering.Typesetting;
using Letterist;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WinColor = Windows.UI.Color;

namespace Letterist.Rendering;

public sealed class DocumentRenderer
{
    private readonly record struct BalloonTextRenderSettings(
        bool IsVertical,
        bool IsRtl,
        bool MirrorTailTargets,
        string LanguageTag);

    private readonly ViewTransform _viewTransform;
    private readonly Dictionary<Guid, int> _balloonSizeSignatureCache = new();
    private Guid? _balloonSizeCacheDocumentId;
    private Document? _translationDocument;
    private Func<string, CanvasBitmap?>? _textFillImageResolver;
    private const float CheckerSize = 10f;
    private const float RulerThickness = 18f;
    private const float RulerTargetStepPixels = 80f;
    private const float TextPreviewOutlineThicknessScale = 0.12f;
    private const float TextPreviewOpacity = 0.65f;
    private const float VerticalColumnGapFactor = 0.2f;
    private static readonly WinColor DefaultTextSelectionColor = WinColor.FromArgb(90, 0, 120, 215);
    private static readonly WinColor DefaultCheckerboardLightColor = WinColor.FromArgb(255, 240, 240, 240);
    private static readonly WinColor DefaultCheckerboardDarkColor = WinColor.FromArgb(255, 220, 220, 220);
    private static readonly WinColor DefaultGridColor = WinColor.FromArgb(100, 100, 100, 100);
    private static readonly WinColor DiagnosticsBackgroundColor = WinColor.FromArgb(200, 40, 40, 40);
    private static readonly WinColor DiagnosticsTextColor = WinColor.FromArgb(255, 200, 200, 200);
    private static readonly WinColor DiagnosticsTextBoundsColor = WinColor.FromArgb(150, 100, 200, 255);
    private static readonly WinColor DiagnosticsLineBoundsColor = WinColor.FromArgb(100, 255, 200, 100);
    private static readonly Vector2[] TextPreviewRingOffsets =
    {
        new(-1f, 0f),
        new(1f, 0f),
        new(0f, -1f),
        new(0f, 1f),
        new(-1f, -1f),
        new(1f, -1f),
        new(-1f, 1f),
        new(1f, 1f)
    };

    public WinColor TextSelectionHighlightColor { get; set; } = DefaultTextSelectionColor;
    public WinColor CheckerboardLightColor { get; set; } = DefaultCheckerboardLightColor;
    public WinColor CheckerboardDarkColor { get; set; } = DefaultCheckerboardDarkColor;
    public WinColor GridBaseColor { get; set; } = DefaultGridColor;
    public bool ShowRulers { get; set; } = true;
    public bool ShowGrid { get; set; }
    public float GridMinorSpacing { get; set; } = 16f;
    public float GridMajorSpacing { get; set; } = 64f;
    public float SelectionHandleSize { get; set; } = 8f;

    private bool _showTypesettingDiagnostics;

    public DocumentRenderer(ViewTransform viewTransform)
    {
        _viewTransform = viewTransform;
    }

    private void EnsureBalloonSizeCacheDocument(Document? document)
    {
        var documentId = document?.Id;
        if (_balloonSizeCacheDocumentId == documentId)
        {
            return;
        }

        _balloonSizeCacheDocumentId = documentId;
        _balloonSizeSignatureCache.Clear();
    }

    public void Render(CanvasDrawingSession ds, Document? document, CanvasBitmap? backgroundImage,
        Guid? editingBalloonId = null,
        string? editingText = null,
        int editingCursorPos = 0,
        int editingSelectionStart = 0,
        int editingSelectionLength = 0,
        IReadOnlyList<TextStyleSpan>? editingTextStyleSpans = null,
        bool cursorBlinkState = true,
        IReadOnlyCollection<Guid>? selectedBalloonIds = null,
        Guid? primarySelectedBalloonId = null,
        IReadOnlyList<SmartGuideLine>? smartGuides = null,
        Func<Guid, CanvasBitmap?>? panelImageResolver = null,
        Func<Guid, CanvasBitmap?>? floatingImageResolver = null,
        Func<string, CanvasBitmap?>? textFillImageResolver = null,
        Guid? selectedFloatingImageId = null,
        IReadOnlyCollection<Guid>? selectedFloatingImageIds = null,
        Guid? selectedPanelId = null,
        IReadOnlyCollection<Guid>? selectedPanelIds = null,
        PanelBoundaryVisibilityMode panelBoundaryVisibilityMode = PanelBoundaryVisibilityMode.Always,
        Guid? hoveredPanelId = null,
        IReadOnlyList<PanelSafeGuideHint>? panelSafeGuideHints = null,
        bool showPanels = false,
        bool showPanelGutters = false,
        Rect? panelPreview = null,
        SnapFeedback? snapFeedback = null,
        bool showTypesettingDiagnostics = false)
    {
        _textFillImageResolver = textFillImageResolver;
        _showTypesettingDiagnostics = showTypesettingDiagnostics;
        _translationDocument = document;
        EnsureBalloonSizeCacheDocument(document);

        ds.Transform = _viewTransform.GetTransformMatrix();

        if (document == null)
        {
            RenderEmptyState(ds);
            _translationDocument = null;
            return;
        }

        RenderCheckerboard(ds, document.Size, document.ActivePage?.BackgroundColor);
        RenderDocumentBounds(ds, document.Size);

        var shouldRenderPanelBorders = panelBoundaryVisibilityMode switch
        {
            PanelBoundaryVisibilityMode.Always => true,
            PanelBoundaryVisibilityMode.LayoutOnly => showPanels || panelPreview.HasValue,
            PanelBoundaryVisibilityMode.Hover => hoveredPanelId.HasValue,
            _ => false
        };

        var shouldRenderPanelGutters = showPanels || (showPanelGutters && panelBoundaryVisibilityMode != PanelBoundaryVisibilityMode.Hidden);

        HashSet<Guid>? selectionSet = null;
        if (selectedBalloonIds != null)
        {
            selectionSet = new HashSet<Guid>(selectedBalloonIds);
        }
        else if (document.SelectedBalloonId.HasValue)
        {
            selectionSet = new HashSet<Guid> { document.SelectedBalloonId.Value };
        }

        var selectionCount = selectionSet?.Count ?? 0;
        var primarySelection = primarySelectedBalloonId ?? document.SelectedBalloonId;
        Rect? selectionBounds = null;

        if (document.ActivePage != null)
        {
            RenderPanelImages(ds, document.ActivePage, panelImageResolver, respectExportVisibility: false);
        }

        RenderGrid(ds, document.Size);

        if (document.ActivePage != null && shouldRenderPanelGutters)
        {
            RenderPanelGutters(ds, document.ActivePage, panelBoundaryVisibilityMode, hoveredPanelId, forceAll: showPanels);
        }

        var activePage = document.ActivePage;
        if (activePage != null)
        {
            RenderBalloonLinks(ds, activePage, drawFill: true, drawStroke: false);
        }

        var nonNormalLayers = new List<(CanvasCommandList image, LayerBlendMode mode)>();
        ICanvasImage? effectsComposed = null;
        try
        {
            foreach (var layer in document.Layers)
            {
                if (!layer.IsVisible) continue;

                var useDirectDraw = layer.BlendMode == LayerBlendMode.Normal;

                if (useDirectDraw)
                {
                    RenderLayerContent(ds, document, layer, activePage, backgroundImage,
                        selectionSet, selectionCount, primarySelection,
                        editingBalloonId, editingText, editingCursorPos,
                        editingSelectionStart, editingSelectionLength, editingTextStyleSpans,
                        floatingImageResolver, cursorBlinkState, ref selectionBounds);
                }
                else
                {
                    var layerImage = new CanvasCommandList(ds);
                    using (var layerDs = layerImage.CreateDrawingSession())
                    {
                        RenderLayerContent(layerDs, document, layer, activePage, backgroundImage,
                            selectionSet, selectionCount, primarySelection,
                            editingBalloonId, editingText, editingCursorPos,
                            editingSelectionStart, editingSelectionLength, editingTextStyleSpans,
                            floatingImageResolver, cursorBlinkState, ref selectionBounds);
                    }
                    nonNormalLayers.Add((layerImage, layer.BlendMode));

                    if (effectsComposed == null)
                    {
                        effectsComposed = layerImage;
                    }
                    else
                    {
                        effectsComposed = ComposeLayerImage(effectsComposed, layerImage, layer.BlendMode);
                    }
                    ds.DrawImage(effectsComposed);
                    effectsComposed = null;
                }
            }
        }
        finally
        {
            foreach (var (layerImage, _) in nonNormalLayers)
            {
                layerImage.Dispose();
            }
        }

        if (document.ActivePage != null && shouldRenderPanelBorders)
        {
            RenderPanelBorders(ds, document.ActivePage, panelBoundaryVisibilityMode, hoveredPanelId);
        }

        if (document.ActivePage != null)
        {
            RenderBalloonLinks(ds, document.ActivePage, drawFill: false, drawStroke: true);
        }

        HashSet<Guid>? floatingImageSelectionSet = null;
        if (selectedFloatingImageIds != null)
        {
            floatingImageSelectionSet = new HashSet<Guid>(selectedFloatingImageIds);
        }
        else if (selectedFloatingImageId.HasValue)
        {
            floatingImageSelectionSet = new HashSet<Guid> { selectedFloatingImageId.Value };
        }

        if (document.ActivePage != null && floatingImageSelectionSet != null && floatingImageSelectionSet.Count > 0)
        {
            Rect? floatingSelectionBounds = null;
            var drawHandles = floatingImageSelectionSet.Count == 1;

            foreach (var imageId in floatingImageSelectionSet)
            {
                var image = document.ActivePage.FindFloatingImage(imageId);
                if (image == null) continue;

                RenderSelectionHighlight(ds, image.Bounds, drawHandles: drawHandles);
                floatingSelectionBounds = floatingSelectionBounds.HasValue
                    ? floatingSelectionBounds.Value.Union(image.Bounds)
                    : image.Bounds;
            }

            if (!drawHandles && floatingSelectionBounds.HasValue)
            {
                RenderSelectionHighlight(ds, floatingSelectionBounds.Value, drawHandles: true);
            }
        }

        if (selectionCount > 1 && selectionBounds.HasValue)
        {
            RenderSelectionHighlight(ds, selectionBounds.Value, drawHandles: true);
        }

        if (document.ActivePage != null && panelSafeGuideHints != null && panelSafeGuideHints.Count > 0)
        {
            RenderPanelSafeGuides(ds, document.ActivePage, panelSafeGuideHints);
        }

        if (document.ActivePage != null && (showPanels || panelPreview.HasValue))
        {
            RenderPanelOverlay(ds, document.ActivePage, selectedPanelId, selectedPanelIds, panelPreview);
        }

        RenderSmartGuides(ds, document, smartGuides);
        RenderGuides(ds, document.ActivePage);
        if (snapFeedback.HasValue)
        {
            RenderSnapFeedback(ds, snapFeedback.Value);
        }
        if (ShowRulers)
        {
            RenderRulers(ds, document);
        }

        RenderOffPanelTailIndicators(ds, document, selectionSet, primarySelection);
        _translationDocument = null;
    }

    private void RenderLayerContent(
        CanvasDrawingSession targetDs,
        Document document,
        Layer layer,
        Page? activePage,
        CanvasBitmap? backgroundImage,
        HashSet<Guid>? selectionSet,
        int selectionCount,
        Guid? primarySelection,
        Guid? editingBalloonId,
        string? editingText,
        int editingCursorPos,
        int editingSelectionStart,
        int editingSelectionLength,
        IReadOnlyList<TextStyleSpan>? editingTextStyleSpans,
        Func<Guid, CanvasBitmap?>? floatingImageResolver,
        bool cursorBlinkState,
        ref Rect? selectionBounds)
    {
        if (activePage != null &&
            backgroundImage != null &&
            activePage.BackgroundLayer?.Id == layer.Id)
        {
            RenderBackgroundImage(targetDs, backgroundImage, document.Size, layer.Opacity, activePage.BackgroundImageFitMode);
        }

        if (activePage != null)
        {
            RenderFloatingImagesForLayer(
                targetDs,
                activePage,
                layer,
                floatingImageResolver,
                includeHidden: false);
        }

        if (layer.Kind == LayerKind.Balloon)
        {
            foreach (var balloon in layer.Balloons)
            {
                if (!balloon.IsVisible) continue;

                if (balloon.PanelId.HasValue)
                {
                    var panel = activePage?.FindPanel(balloon.PanelId.Value);
                    if (panel != null && !panel.IsVisible) continue;
                }

                var isSelected = selectionSet?.Contains(balloon.Id) ?? false;
                var isEditing = editingBalloonId == balloon.Id;
                var drawHandles = selectionCount <= 1 && isSelected && primarySelection == balloon.Id;
                var isLinked = activePage?.IsBalloonLinked(balloon.Id) ?? false;
                var clipPanel = GetConstrainClipPanel(activePage, balloon);
                if (clipPanel != null)
                {
                    using var clipGeometry = PanelGeometry.CreateGeometry(targetDs, clipPanel);
                    using var clipLayer = targetDs.CreateLayer(1f, clipGeometry);
                    RenderBalloon(
                        targetDs,
                        balloon,
                        isSelected,
                        layer.Opacity,
                        isEditing,
                        editingText,
                        editingCursorPos,
                        editingSelectionStart,
                        editingSelectionLength,
                        isEditing ? editingTextStyleSpans : null,
                        drawHandles,
                        skipStroke: isLinked,
                        cursorBlinkState);
                }
                else
                {
                    RenderBalloon(
                        targetDs,
                        balloon,
                        isSelected,
                        layer.Opacity,
                        isEditing,
                        editingText,
                        editingCursorPos,
                        editingSelectionStart,
                        editingSelectionLength,
                        isEditing ? editingTextStyleSpans : null,
                        drawHandles,
                        skipStroke: isLinked,
                        cursorBlinkState);
                }

                if (selectionCount > 1 && isSelected)
                {
                    selectionBounds = selectionBounds.HasValue
                        ? selectionBounds.Value.Union(balloon.Bounds)
                        : balloon.Bounds;
                }
            }
        }
    }

    public void RenderContent(
        CanvasDrawingSession ds,
        Document document,
        CanvasBitmap? backgroundImage,
        Func<Guid, CanvasBitmap?>? panelImageResolver = null,
        Func<Guid, CanvasBitmap?>? floatingImageResolver = null,
        Func<string, CanvasBitmap?>? textFillImageResolver = null)
    {
        _textFillImageResolver = textFillImageResolver;
        var page = document.ActivePage;
        if (page == null) return;

        RenderPageContent(
            ds,
            page,
            backgroundImage,
            includeHiddenLayers: false,
            panelImageResolver: panelImageResolver,
            floatingImageResolver: floatingImageResolver,
            textFillImageResolver: textFillImageResolver,
            translationDocument: document);
    }

    public void RenderBalloonPreview(CanvasDrawingSession ds, Point2 position, BalloonShape shape, BalloonStyle style, string text)
    {
        var previewBalloon = new Balloon(Guid.Empty, Guid.Empty, position, shape, style, text, TextStyle.Default);
        UpdateBalloonSize(ds, previewBalloon, text);

        var bounds = previewBalloon.Bounds;
        var baseOpacity = ClampOpacity(style.Opacity);
        var previewOpacity = MathF.Max(0.12f, baseOpacity * 0.35f);

        var fillColor = ApplyOpacity(style.FillColor.ToWindowsColor(), previewOpacity);
        var strokeColor = ApplyOpacity(style.StrokeColor.ToWindowsColor(), MathF.Min(1f, previewOpacity + 0.2f));

        RenderBalloonShape(ds, shape, bounds, fillColor, strokeColor, style);
    }

    public void RenderTextStylePreview(CanvasDrawingSession ds, Point2 position, string text, TextStyle style)
    {
        var displayText = style.AllCaps ? text.ToUpperInvariant() : text;
        var format = TextLayoutUtilities.CreateTextFormat(style, spans: null);
        format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
        format.VerticalAlignment = CanvasVerticalAlignment.Top;
        format.WordWrapping = CanvasWordWrapping.NoWrap;

        using var layout = new CanvasTextLayout(ds, displayText, format, 4000f, 1000f);
        if (displayText.Length > 0)
        {
            TextLayoutUtilities.ApplyTracking(layout, style, displayText.Length);
            TextLayoutUtilities.ApplyTypographyFeatures(layout, displayText.Length, TextLayoutUtilities.CreateTypographySettings(style));
        }

        var drawBounds = layout.DrawBounds;
        var origin = new Vector2(
            position.X - (float)drawBounds.Width / 2f - (float)drawBounds.X,
            position.Y - (float)drawBounds.Height / 2f - (float)drawBounds.Y + style.VerticalOffset);

        RenderTextStyleLayout(ds, layout, origin, style, TextPreviewOpacity);
    }

    public void RenderPageContent(
        CanvasDrawingSession ds,
        Page page,
        CanvasBitmap? backgroundImage,
        bool includeHiddenLayers,
        Guid? singleLayerId = null,
        Matrix3x2? transformOverride = null,
        Func<Guid, CanvasBitmap?>? panelImageResolver = null,
        Func<Guid, CanvasBitmap?>? floatingImageResolver = null,
        Func<string, CanvasBitmap?>? textFillImageResolver = null,
        Document? translationDocument = null,
        bool renderPanelBorders = false,
        bool renderPanelMembershipBadges = true)
    {
        var previousTranslationDocument = _translationDocument;
        _translationDocument = translationDocument ?? previousTranslationDocument;
        try
        {
            _textFillImageResolver = textFillImageResolver;
            ds.Transform = transformOverride ?? Matrix3x2.Identity;

            var backgroundLayer = page.BackgroundLayer;
            var renderBackground = backgroundLayer != null
                && (!singleLayerId.HasValue || backgroundLayer.Id == singleLayerId.Value)
                && (includeHiddenLayers || backgroundLayer.IsVisible);

            using var underlay = new CanvasCommandList(ds);
            using (var underlayDs = underlay.CreateDrawingSession())
            {
                RenderPanelImages(underlayDs, page, panelImageResolver, respectExportVisibility: true);
            }

            ICanvasImage composedPage = underlay;
            var layerImages = new List<CanvasCommandList>();
            try
            {
                foreach (var layer in page.Layers)
                {
                    if (singleLayerId.HasValue && layer.Id != singleLayerId.Value) continue;
                    if (!includeHiddenLayers && !layer.IsVisible) continue;

                    var layerImage = new CanvasCommandList(ds);
                    var drewLayerContent = false;
                    using (var layerDs = layerImage.CreateDrawingSession())
                    {
                        if (backgroundImage != null && renderBackground && backgroundLayer?.Id == layer.Id)
                        {
                            RenderBackgroundImage(layerDs, backgroundImage, page.Size, layer.Opacity, page.BackgroundImageFitMode);
                            drewLayerContent = true;
                        }

                        drewLayerContent |= RenderFloatingImagesForLayer(
                            layerDs,
                            page,
                            layer,
                            floatingImageResolver,
                            includeHiddenLayers);

                        if (layer.Kind == LayerKind.Balloon)
                        {
                            foreach (var balloon in layer.Balloons)
                            {
                                if (!includeHiddenLayers && !balloon.IsVisible) continue;

                                if (!includeHiddenLayers && balloon.PanelId.HasValue)
                                {
                                    var panel = page.FindPanel(balloon.PanelId.Value);
                                    if (panel != null && !panel.IsVisible) continue;
                                }

                                drewLayerContent = true;
                                var isLinked = page.IsBalloonLinked(balloon.Id);
                                var clipPanel = GetConstrainClipPanel(page, balloon);
                                if (clipPanel != null)
                                {
                                    using var clipGeometry = PanelGeometry.CreateGeometry(layerDs, clipPanel);
                                    using var clipLayer = layerDs.CreateLayer(1f, clipGeometry);
                                    RenderBalloon(layerDs, balloon, isSelected: false, layer.Opacity, skipStroke: isLinked, cursorBlinkState: false, renderPanelMembershipBadge: renderPanelMembershipBadges);
                                }
                                else
                                {
                                    RenderBalloon(layerDs, balloon, isSelected: false, layer.Opacity, skipStroke: isLinked, cursorBlinkState: false, renderPanelMembershipBadge: renderPanelMembershipBadges);
                                }
                            }
                        }
                    }

                    if (!drewLayerContent)
                    {
                        layerImage.Dispose();
                        continue;
                    }

                    layerImages.Add(layerImage);
                    composedPage = ComposeLayerImage(composedPage, layerImage, layer.BlendMode);
                }

                RenderBalloonLinks(ds, page, drawFill: true, drawStroke: false);
                ds.DrawImage(composedPage);
                if (renderPanelBorders)
                {
                    RenderPanelBorders(ds, page, PanelBoundaryVisibilityMode.Always, hoveredPanelId: null);
                }
            }
            finally
            {
                foreach (var layerImage in layerImages)
                {
                    layerImage.Dispose();
                }
            }

            RenderBalloonLinks(ds, page, drawFill: false, drawStroke: true);
        }
        finally
        {
            _translationDocument = previousTranslationDocument;
        }
    }

    private void RenderEmptyState(CanvasDrawingSession ds)
    {
        ds.Transform = Matrix3x2.Identity;

        var textFormat = new CanvasTextFormat
        {
            FontSize = 24,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };

        var viewport = _viewTransform.ViewportSize;
        ds.DrawText(
            "Letterist\nNo document loaded",
            new Vector2(viewport.Width / 2, viewport.Height / 2),
            WinColor.FromArgb(255, 128, 128, 128),
            textFormat);
    }

    private void RenderCheckerboard(CanvasDrawingSession ds, Size2 documentSize, Model.Color? backgroundColor = null)
    {
        if (backgroundColor.HasValue)
        {
            var bgColor = backgroundColor.Value;
            var winColor = WinColor.FromArgb(bgColor.A, bgColor.R, bgColor.G, bgColor.B);
            ds.FillRectangle(0, 0, documentSize.Width, documentSize.Height, winColor);
            return;
        }

        var cols = (int)Math.Ceiling(documentSize.Width / CheckerSize);
        var rows = (int)Math.Ceiling(documentSize.Height / CheckerSize);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                var color = (row + col) % 2 == 0 ? CheckerboardLightColor : CheckerboardDarkColor;
                var x = col * CheckerSize;
                var y = row * CheckerSize;
                var w = Math.Min(CheckerSize, documentSize.Width - x);
                var h = Math.Min(CheckerSize, documentSize.Height - y);

                ds.FillRectangle(x, y, w, h, color);
            }
        }
    }

    private void RenderDocumentBounds(CanvasDrawingSession ds, Size2 documentSize)
    {
        var borderColor = WinColor.FromArgb(100, 0, 0, 0);
        ds.DrawRectangle(0, 0, documentSize.Width, documentSize.Height, borderColor, 1f);
    }

    private void RenderBackgroundImage(
        CanvasDrawingSession ds,
        CanvasBitmap image,
        Size2 documentSize,
        float opacity,
        PanelImageFitMode fitMode)
    {
        if (!TryComputeBackgroundImageDrawRects(
            documentSize,
            (float)image.SizeInPixels.Width,
            (float)image.SizeInPixels.Height,
            fitMode,
            out var destination,
            out var source))
        {
            return;
        }

        ds.DrawImage(image, destination, source, ClampOpacity(opacity));
    }

    internal static bool TryComputeBackgroundImageDrawRects(
        Size2 documentSize,
        float imageWidth,
        float imageHeight,
        PanelImageFitMode fitMode,
        out Windows.Foundation.Rect destination,
        out Windows.Foundation.Rect source)
    {
        destination = default;
        source = default;

        var pageWidth = documentSize.Width;
        var pageHeight = documentSize.Height;
        if (pageWidth <= 0f || pageHeight <= 0f || imageWidth <= 0f || imageHeight <= 0f)
        {
            return false;
        }

        Windows.Foundation.Rect unboundedDestination;
        if (fitMode == PanelImageFitMode.Stretch)
        {
            unboundedDestination = new Windows.Foundation.Rect(0, 0, pageWidth, pageHeight);
        }
        else
        {
            var scale = fitMode switch
            {
                PanelImageFitMode.Fit => MathF.Min(pageWidth / imageWidth, pageHeight / imageHeight),
                PanelImageFitMode.Original => 1f,
                _ => MathF.Max(pageWidth / imageWidth, pageHeight / imageHeight)
            };

            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
            {
                scale = 1f;
            }

            var drawWidth = imageWidth * scale;
            var drawHeight = imageHeight * scale;
            var drawX = (pageWidth - drawWidth) * 0.5f;
            var drawY = (pageHeight - drawHeight) * 0.5f;
            unboundedDestination = new Windows.Foundation.Rect(drawX, drawY, drawWidth, drawHeight);
        }

        var pageRect = new Windows.Foundation.Rect(0, 0, pageWidth, pageHeight);
        if (!TryIntersectRects(unboundedDestination, pageRect, out destination))
        {
            return false;
        }

        if (unboundedDestination.Width <= 0d || unboundedDestination.Height <= 0d)
        {
            return false;
        }

        var u0 = (destination.X - unboundedDestination.X) / unboundedDestination.Width;
        var v0 = (destination.Y - unboundedDestination.Y) / unboundedDestination.Height;
        var u1 = (destination.X + destination.Width - unboundedDestination.X) / unboundedDestination.Width;
        var v1 = (destination.Y + destination.Height - unboundedDestination.Y) / unboundedDestination.Height;

        u0 = Math.Clamp(u0, 0d, 1d);
        v0 = Math.Clamp(v0, 0d, 1d);
        u1 = Math.Clamp(u1, 0d, 1d);
        v1 = Math.Clamp(v1, 0d, 1d);

        source = new Windows.Foundation.Rect(
            u0 * imageWidth,
            v0 * imageHeight,
            Math.Max(0d, (u1 - u0) * imageWidth),
            Math.Max(0d, (v1 - v0) * imageHeight));

        return destination.Width > 0d && destination.Height > 0d && source.Width > 0d && source.Height > 0d;
    }

    internal static bool TryIntersectRects(
        Windows.Foundation.Rect a,
        Windows.Foundation.Rect b,
        out Windows.Foundation.Rect intersection)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        if (right <= left || bottom <= top)
        {
            intersection = default;
            return false;
        }

        intersection = new Windows.Foundation.Rect(left, top, right - left, bottom - top);
        return true;
    }

    private void RenderPanelImages(CanvasDrawingSession ds, Page page, Func<Guid, CanvasBitmap?>? panelImageResolver, bool respectExportVisibility)
    {
        _ = ds;
        _ = page;
        _ = panelImageResolver;
        _ = respectExportVisibility;
    }

    private bool RenderFloatingImagesForLayer(
        CanvasDrawingSession ds,
        Page page,
        Layer layer,
        Func<Guid, CanvasBitmap?>? floatingImageResolver,
        bool includeHidden)
    {
        if (floatingImageResolver == null) return false;

        var drewAny = false;

        foreach (var image in page.FloatingImages)
        {
            var ownerLayer = page.FindLayerForFloatingImage(image);
            if (ownerLayer == null || ownerLayer.Id != layer.Id) continue;

            if (!includeHidden && !image.IsVisible) continue;
            if (image.Bounds.Width <= 0f || image.Bounds.Height <= 0f) continue;

            PanelZone? assignedPanel = null;
            if (image.PanelId.HasValue)
            {
                assignedPanel = page.FindPanel(image.PanelId.Value);
                if (!includeHidden && assignedPanel != null && !assignedPanel.IsVisible)
                {
                    continue;
                }
            }

            var panelClip = image.ConstrainToPanel ? assignedPanel : null;

            var bitmap = floatingImageResolver(image.Id);
            if (bitmap == null) continue;

            var effectiveOpacity = ClampOpacity(image.Opacity * ownerLayer.Opacity);

            if (panelClip != null)
            {
                using var geometry = PanelGeometry.CreateGeometry(ds, panelClip);
                using var clipLayer = ds.CreateLayer(1f, geometry);
                RenderFloatingImageWithEffects(ds, bitmap, image, effectiveOpacity);
            }
            else
            {
                RenderFloatingImageWithEffects(ds, bitmap, image, effectiveOpacity);
            }

            drewAny = true;
        }

        return drewAny;
    }

    private void RenderFloatingImageWithEffects(CanvasDrawingSession ds, CanvasBitmap bitmap, FloatingImage image, float effectiveOpacity)
    {
        var hasShadow = image.ShadowEnabled && image.ShadowOpacity > 0.001f;
        var hasGlow = image.GlowEnabled && image.GlowOpacity > 0.001f && image.GlowSize > 0.01f;
        if (!hasShadow && !hasGlow)
        {
            DrawFloatingImageBitmap(ds, bitmap, image, effectiveOpacity);
            return;
        }

        using var content = new CanvasCommandList(ds);
        using (var contentDs = content.CreateDrawingSession())
        {
            DrawFloatingImageBitmap(contentDs, bitmap, image, effectiveOpacity);
        }

        if (hasShadow)
        {
            ICanvasImage shadowImage = new ShadowEffect
            {
                Source = content,
                BlurAmount = MathF.Max(0f, image.ShadowFalloff),
                ShadowColor = ApplyOpacity(image.ShadowColor.ToWindowsColor(), Math.Clamp(image.ShadowOpacity, 0f, 1f))
            };

            if (MathF.Abs(image.ShadowOffsetX) > 0.001f || MathF.Abs(image.ShadowOffsetY) > 0.001f)
            {
                shadowImage = new Transform2DEffect
                {
                    Source = shadowImage,
                    TransformMatrix = Matrix3x2.CreateTranslation(image.ShadowOffsetX, image.ShadowOffsetY)
                };
            }

            ds.DrawImage(shadowImage);
        }

        if (hasGlow)
        {
            ICanvasImage glowImage = new ShadowEffect
            {
                Source = content,
                BlurAmount = MathF.Max(0f, image.GlowSize),
                ShadowColor = ApplyOpacity(image.GlowColor.ToWindowsColor(), Math.Clamp(image.GlowOpacity, 0f, 1f))
            };
            ds.DrawImage(glowImage);
        }

        ds.DrawImage(content);
    }

    private static void DrawFloatingImageBitmap(CanvasDrawingSession ds, CanvasBitmap bitmap, FloatingImage image, float opacity)
    {
        var destination = image.Bounds.ToWindowsRect();
        var source = new Windows.Foundation.Rect(0, 0, bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height);
        var originalTransform = ds.Transform;
        var rotation = image.Rotation;
        var hasRotation = MathF.Abs(rotation) > 0.01f;

        if (hasRotation)
        {
            var radians = rotation * MathF.PI / 180f;
            ds.Transform = Matrix3x2.CreateRotation(radians, image.Bounds.Center.ToVector2()) * originalTransform;
        }

        try
        {
            ds.DrawImage(bitmap, destination, source, ClampOpacity(opacity));
        }
        finally
        {
            if (hasRotation)
            {
                ds.Transform = originalTransform;
            }
        }
    }

    private static PanelZone? GetConstrainClipPanel(Page? page, Balloon balloon)
    {
        if (page == null || !balloon.ConstrainToPanel || !balloon.PanelId.HasValue)
        {
            return null;
        }

        return page.FindPanel(balloon.PanelId.Value);
    }

    private static ICanvasImage ComposeLayerImage(ICanvasImage background, ICanvasImage foreground, LayerBlendMode blendMode)
    {
        if (blendMode == LayerBlendMode.Normal)
        {
            return new CompositeEffect
            {
                Mode = CanvasComposite.SourceOver,
                Sources =
                {
                    background,
                    foreground
                }
            };
        }

        return new BlendEffect
        {
            Background = background,
            Foreground = foreground,
            Mode = ToBlendEffectMode(blendMode)
        };
    }

    private static BlendEffectMode ToBlendEffectMode(LayerBlendMode blendMode)
    {
        return blendMode switch
        {
            LayerBlendMode.Multiply => BlendEffectMode.Multiply,
            LayerBlendMode.Screen => BlendEffectMode.Screen,
            LayerBlendMode.Overlay => BlendEffectMode.Overlay,
            LayerBlendMode.SoftLight => BlendEffectMode.SoftLight,
            LayerBlendMode.HardLight => BlendEffectMode.HardLight,
            LayerBlendMode.Add => BlendEffectMode.LinearDodge,
            LayerBlendMode.Subtract => BlendEffectMode.Subtract,
            LayerBlendMode.Difference => BlendEffectMode.Difference,
            _ => BlendEffectMode.Multiply
        };
    }

    private static Rect ComputePanelImageRect(PanelZone panel, float imageWidth, float imageHeight, PanelImagePlacement placement)
    {
        var scale = placement.Scale;
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        return new Rect(
            panel.Bounds.X + placement.Offset.X,
            panel.Bounds.Y + placement.Offset.Y,
            imageWidth * scale,
            imageHeight * scale);
    }

    public static PanelImagePlacement ComputeDefaultPanelImagePlacement(PanelZone panel, float imageWidth, float imageHeight, PanelImageFitMode fitMode)
    {
        if (imageWidth <= 0f || imageHeight <= 0f)
        {
            return new PanelImagePlacement(new Point2(0f, 0f), 1f, fitMode);
        }

        float scale;
        switch (fitMode)
        {
            case PanelImageFitMode.Fill:
                scale = MathF.Max(panel.Bounds.Width / imageWidth, panel.Bounds.Height / imageHeight);
                break;
            case PanelImageFitMode.Fit:
                scale = MathF.Min(panel.Bounds.Width / imageWidth, panel.Bounds.Height / imageHeight);
                break;
            case PanelImageFitMode.Stretch:
                scale = 1f;
                break;
            case PanelImageFitMode.Original:
            default:
                scale = 1f;
                break;
        }

        var offsetX = (panel.Bounds.Width - imageWidth * scale) / 2f;
        var offsetY = (panel.Bounds.Height - imageHeight * scale) / 2f;

        return new PanelImagePlacement(new Point2(offsetX, offsetY), scale, fitMode);
    }

    private void RenderTextStyleLayout(CanvasDrawingSession ds, CanvasTextLayout layout, Vector2 origin, TextStyle style, float opacity)
    {
        var strokeLayers = BuildTextStrokeLayers(style, opacity, TextPreviewOutlineThicknessScale);

        if (TextWarpRenderer.IsWarpEnabled(style))
        {
            var maxEffectExtent = GetMaxTextVisualEffectExtent(style, TextPreviewOutlineThicknessScale);
            var contentBounds = GetLayoutDrawBounds(layout, origin).Inflate(maxEffectExtent + 2f, maxEffectExtent + 2f);
            using var source = new CanvasCommandList(ds);
            using (var sourceDs = source.CreateDrawingSession())
            {
                DrawTextStyleLayoutRaw(sourceDs, layout, origin, style, opacity, strokeLayers);
            }

            TextWarpRenderer.DrawWarpedImage(ds, source, contentBounds, style, opacity: 1f);
            return;
        }

        DrawTextStyleLayoutRaw(ds, layout, origin, style, opacity, strokeLayers);
    }

    private void DrawTextStyleLayoutRaw(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        TextStyle style,
        float opacity,
        IReadOnlyList<TextStrokeLayer> strokeLayers)
    {
        DrawTextLayoutWithStrokes(ds, layout, origin, style, opacity, strokeLayers);
    }

    private static float ResolveTextOutlineWidth(TextStyle style, float autoScale = 0f)
    {
        if (style.OutlineWidth >= 0f)
        {
            return MathF.Max(0f, style.OutlineWidth);
        }

        if (autoScale <= 0f)
        {
            return 0f;
        }

        return MathF.Max(1.5f, style.FontSize * autoScale);
    }

    private static float GetMaxTextStrokeWidth(TextStyle style, float autoScale = 0f)
    {
        var maxWidth = ResolveTextOutlineWidth(style, autoScale);
        var additional = style.AdditionalStrokes;
        if (additional == null) return maxWidth;

        for (int i = 0; i < additional.Count; i++)
        {
            maxWidth = MathF.Max(maxWidth, MathF.Max(0f, additional[i].Width));
        }

        return maxWidth;
    }

    private static List<TextStrokeLayer> BuildTextStrokeLayers(TextStyle style, float opacity, float autoScale = 0f)
    {
        var layers = new List<TextStrokeLayer>();

        var baseOutlineWidth = ResolveTextOutlineWidth(style, autoScale);
        if (baseOutlineWidth > 0.01f)
        {
            layers.Add(new TextStrokeLayer(
                ApplyOpacity(style.OutlineColor.ToWindowsColor(), opacity),
                baseOutlineWidth));
        }

        var additional = style.AdditionalStrokes;
        if (additional != null)
        {
            for (int i = 0; i < additional.Count; i++)
            {
                var stroke = additional[i];
                var width = MathF.Max(0f, stroke.Width);
                if (width <= 0.01f) continue;

                layers.Add(new TextStrokeLayer(
                    ApplyOpacity(stroke.Color.ToWindowsColor(), opacity),
                    width));
            }
        }

        if (layers.Count > 1)
        {
            layers.Sort((left, right) => right.Width.CompareTo(left.Width));
        }

        return layers;
    }

    private static List<TextShadowLayer> BuildTextShadowLayers(TextStyle style, float opacity)
    {
        var shadows = style.Shadows;
        var layers = new List<TextShadowLayer>(shadows?.Count ?? 0);
        if (shadows == null || shadows.Count == 0)
        {
            return layers;
        }

        for (int i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            var shadowOpacity = Math.Clamp(shadow.Opacity, 0f, 1f) * opacity;
            if (shadowOpacity <= 0.001f) continue;

            var color = ApplyOpacity(shadow.Color.ToWindowsColor(), shadowOpacity);
            layers.Add(new TextShadowLayer(
                color,
                shadow.OffsetX,
                shadow.OffsetY,
                MathF.Max(0f, shadow.Blur)));
        }

        return layers;
    }

    private static float GetMaxTextShadowExtent(TextStyle style)
    {
        var shadows = style.Shadows;
        if (shadows == null || shadows.Count == 0) return 0f;

        var maxExtent = 0f;
        for (int i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            if (shadow.Opacity <= 0.001f) continue;

            var offsetExtent = MathF.Max(MathF.Abs(shadow.OffsetX), MathF.Abs(shadow.OffsetY));
            maxExtent = MathF.Max(maxExtent, offsetExtent + MathF.Max(0f, shadow.Blur));
        }

        return maxExtent;
    }

    private static float GetMaxTextAdvancedEffectExtent(TextStyle style)
    {
        var outerGlow = style.OuterGlowEnabled ? MathF.Max(0f, style.OuterGlowSize) : 0f;
        var extrusion = style.ExtrusionEnabled ? MathF.Max(0f, style.ExtrusionDepth) : 0f;
        var motionBlur = style.MotionBlurEnabled ? MathF.Max(0f, style.MotionBlurDistance) : 0f;
        return MathF.Max(MathF.Max(outerGlow, extrusion), motionBlur);
    }

    private static float GetMaxTextVisualEffectExtent(TextStyle style, float autoScale = 0f)
    {
        var stroke = GetMaxTextStrokeWidth(style, autoScale);
        var shadow = GetMaxTextShadowExtent(style);
        var advanced = GetMaxTextAdvancedEffectExtent(style);
        return MathF.Max(stroke, MathF.Max(shadow, advanced));
    }

    private static void DrawTextLayoutShadows(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        IReadOnlyList<TextShadowLayer> shadowLayers)
    {
        if (shadowLayers == null || shadowLayers.Count == 0) return;

        for (int i = 0; i < shadowLayers.Count; i++)
        {
            var layer = shadowLayers[i];
            var shadowOrigin = origin + new Vector2(layer.OffsetX, layer.OffsetY);

            if (layer.Blur <= 0.01f)
            {
                ds.DrawTextLayout(layout, shadowOrigin, layer.Color);
                continue;
            }

            DrawTextLayoutSoftSpread(ds, layout, shadowOrigin, layer.Color, layer.Blur, maxSamples: 88);
        }
    }

    private static void DrawTextLayoutSoftSpread(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        WinColor color,
        float radius,
        int maxSamples)
    {
        if (radius <= 0.01f || color.A == 0)
        {
            ds.DrawTextLayout(layout, origin, color);
            return;
        }

        var sampleCount = Math.Clamp((int)MathF.Ceiling(radius * 7f), 12, maxSamples);
        var weights = new float[sampleCount];
        const float centerWeight = 1.35f;
        var totalWeight = centerWeight;

        for (int i = 0; i < sampleCount; i++)
        {
            var t = (i + 0.5f) / sampleCount;
            var sampleRadius = MathF.Sqrt(t) * radius;
            var normalized = sampleRadius / MathF.Max(radius, 0.0001f);
            var weight = MathF.Max(0.02f, 1f - (normalized * normalized));
            weights[i] = weight;
            totalWeight += weight;
        }

        ds.DrawTextLayout(layout, origin, ScaleColorAlpha(color, centerWeight / totalWeight));

        const float goldenAngle = 2.39996323f;
        for (int i = 0; i < sampleCount; i++)
        {
            var t = (i + 0.5f) / sampleCount;
            var sampleRadius = MathF.Sqrt(t) * radius;
            var angle = i * goldenAngle;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sampleRadius;
            ds.DrawTextLayout(layout, origin + offset, ScaleColorAlpha(color, weights[i] / totalWeight));
        }
    }

    private static WinColor ScaleColorAlpha(WinColor color, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return WinColor.FromArgb(
            (byte)Math.Clamp((int)MathF.Round(color.A * factor), 0, 255),
            color.R,
            color.G,
            color.B);
    }

    private void DrawTextLayoutWithStrokes(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        TextStyle style,
        float opacity,
        IReadOnlyList<TextStrokeLayer> strokeLayers)
    {
        var shadowLayers = BuildTextShadowLayers(style, opacity);
        DrawTextLayoutShadows(ds, layout, origin, shadowLayers);
        DrawTextLayoutOuterGlow(ds, layout, origin, style, opacity);
        DrawTextLayoutExtrusion(ds, layout, origin, style, opacity);
        DrawTextLayoutMotionBlur(ds, layout, origin, style, opacity, strokeLayers);

        var drawInnerGlow = style.InnerGlowEnabled
            && style.InnerGlowSize > 0.01f
            && style.InnerGlowOpacity > 0.001f;
        var hasStroke = strokeLayers != null && strokeLayers.Count > 0
            && strokeLayers.Any(l => l.Width > 0.01f);
        var needsGeometry = style.FillType != TextFillType.Solid || drawInnerGlow || hasStroke;
        CanvasGeometry? translatedGeometry = null;
        if (needsGeometry)
        {
            using var textGeometry = CanvasGeometry.CreateText(layout);
            translatedGeometry = textGeometry.Transform(Matrix3x2.CreateTranslation(origin));
        }

        if (strokeLayers != null && translatedGeometry != null)
        {
            for (int i = 0; i < strokeLayers.Count; i++)
            {
                var layer = strokeLayers[i];
                if (layer.Width <= 0.01f) continue;

                using var strokeStyle = new CanvasStrokeStyle { LineJoin = CanvasLineJoin.Round };
                ds.DrawGeometry(translatedGeometry, layer.Color, layer.Width * 2f, strokeStyle);
            }
        }

        try
        {
            if (style.FillType == TextFillType.Solid)
            {
                var textColor = ApplyOpacity(style.TextColor.ToWindowsColor(), opacity);
                ds.DrawTextLayout(layout, origin, textColor);
            }
            else if (translatedGeometry != null)
            {
                var bounds = GetLayoutDrawBounds(layout, origin);
                FillTextGeometry(ds, translatedGeometry, bounds, style, opacity);
            }

            if (drawInnerGlow && translatedGeometry != null)
            {
                DrawTextLayoutInnerGlow(ds, layout, origin, translatedGeometry, style, opacity);
            }
        }
        finally
        {
            translatedGeometry?.Dispose();
        }
    }

    private static void DrawTextLayoutOuterGlow(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        TextStyle style,
        float opacity)
    {
        if (!style.OuterGlowEnabled) return;

        var glowSize = MathF.Max(0f, style.OuterGlowSize);
        var glowOpacity = Math.Clamp(style.OuterGlowOpacity, 0f, 1f) * opacity;
        if (glowSize <= 0.01f || glowOpacity <= 0.001f) return;

        var glowColor = ApplyOpacity(style.OuterGlowColor.ToWindowsColor(), glowOpacity);
        DrawTextLayoutSoftSpread(ds, layout, origin, glowColor, glowSize, maxSamples: 96);
    }

    private static void DrawTextLayoutExtrusion(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        TextStyle style,
        float opacity)
    {
        if (!style.ExtrusionEnabled) return;

        var depth = MathF.Max(0f, style.ExtrusionDepth);
        var extrusionOpacity = Math.Clamp(style.ExtrusionOpacity, 0f, 1f) * opacity;
        if (depth <= 0.01f || extrusionOpacity <= 0.001f) return;

        var angleRadians = style.ExtrusionAngle * MathF.PI / 180f;
        var direction = new Vector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians));
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = new Vector2(1f, 1f);
        }

        direction = Vector2.Normalize(direction);
        var steps = Math.Clamp((int)MathF.Ceiling(depth * 4f), 2, 96);
        var extrusionColor = ApplyOpacity(style.ExtrusionColor.ToWindowsColor(), extrusionOpacity);

        var weights = new float[steps];
        var totalWeight = 0f;
        for (int i = 0; i < steps; i++)
        {
            var t = (i + 1f) / steps;
            var weight = 0.35f + (0.65f * t);
            weights[i] = weight;
            totalWeight += weight;
        }

        for (int step = steps; step >= 1; step--)
        {
            var t = step / (float)steps;
            var distance = depth * t;
            var color = ScaleColorAlpha(extrusionColor, weights[step - 1] / MathF.Max(totalWeight, 0.0001f));
            ds.DrawTextLayout(layout, origin + (direction * distance), color);
        }
    }

    private static void DrawTextLayoutMotionBlur(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        TextStyle style,
        float opacity,
        IReadOnlyList<TextStrokeLayer> strokeLayers)
    {
        if (!style.MotionBlurEnabled) return;

        var distance = MathF.Max(0f, style.MotionBlurDistance);
        var blurOpacity = Math.Clamp(style.MotionBlurOpacity, 0f, 1f) * opacity;
        if (distance <= 0.01f || blurOpacity <= 0.001f) return;

        var angleRadians = style.MotionBlurAngle * MathF.PI / 180f;
        var direction = new Vector2(MathF.Cos(angleRadians), MathF.Sin(angleRadians));
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = new Vector2(1f, 0f);
        }

        direction = Vector2.Normalize(direction);
        var steps = Math.Clamp((int)MathF.Ceiling(distance * 1.5f), 3, 30);

        for (int step = 1; step <= steps; step++)
        {
            var t = step / (float)steps;
            var sampleOrigin = origin + (direction * (distance * t));
            var alphaFactor = blurOpacity * (1f - t) * (2f / steps);
            if (alphaFactor <= 0.0005f) continue;

            if (strokeLayers != null)
            {
                for (int i = 0; i < strokeLayers.Count; i++)
                {
                    var stroke = strokeLayers[i];
                    if (stroke.Width <= 0.01f) continue;
                    var strokeColor = ScaleColorAlpha(stroke.Color, alphaFactor);
                    foreach (var offset in TextPreviewRingOffsets)
                    {
                        ds.DrawTextLayout(layout, sampleOrigin + (offset * stroke.Width), strokeColor);
                    }
                }
            }

            var blurColor = ApplyOpacity(style.TextColor.ToWindowsColor(), alphaFactor);
            ds.DrawTextLayout(layout, sampleOrigin, blurColor);
        }
    }

    private static void DrawTextLayoutInnerGlow(
        CanvasDrawingSession ds,
        CanvasTextLayout layout,
        Vector2 origin,
        CanvasGeometry geometry,
        TextStyle style,
        float opacity)
    {
        var glowSize = MathF.Max(0f, style.InnerGlowSize);
        var glowOpacity = Math.Clamp(style.InnerGlowOpacity, 0f, 1f) * opacity;
        if (glowSize <= 0.01f || glowOpacity <= 0.001f) return;

        var glowColor = ApplyOpacity(style.InnerGlowColor.ToWindowsColor(), glowOpacity);

        using var layer = ds.CreateLayer(1f, geometry);
        DrawTextLayoutSoftSpread(ds, layout, origin, glowColor, glowSize, maxSamples: 84);
    }

    private void FillTextGeometry(CanvasDrawingSession ds, CanvasGeometry geometry, Rect bounds, TextStyle style, float opacity)
    {
        switch (style.FillType)
        {
            case TextFillType.Linear:
                using (var brush = CreateLinearTextFillBrush(ds, bounds, style, opacity))
                {
                    ds.FillGeometry(geometry, brush);
                }
                break;

            case TextFillType.Radial:
                using (var brush = CreateRadialTextFillBrush(ds, bounds, style, opacity))
                {
                    ds.FillGeometry(geometry, brush);
                }
                break;

            case TextFillType.Pattern:
                using (var layer = ds.CreateLayer(1f, geometry))
                {
                    RenderPatternFill(ds, bounds, style, opacity);
                }
                break;

            case TextFillType.Image:
                if (RenderImageFill(ds, geometry, bounds, style, opacity))
                {
                    break;
                }

                ds.FillGeometry(geometry, ApplyOpacity(style.TextColor.ToWindowsColor(), opacity));
                break;

            case TextFillType.Solid:
            default:
                ds.FillGeometry(geometry, ApplyOpacity(style.TextColor.ToWindowsColor(), opacity));
                break;
        }
    }

    private CanvasLinearGradientBrush CreateLinearTextFillBrush(CanvasDrawingSession ds, Rect bounds, TextStyle style, float opacity)
    {
        var primary = ApplyOpacity(style.TextColor.ToWindowsColor(), opacity);
        var secondary = ApplyOpacity(style.FillSecondaryColor.ToWindowsColor(), opacity);
        var angle = style.FillAngle * MathF.PI / 180f;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = new Vector2(1f, 0f);
        }

        var center = bounds.Center.ToVector2();
        var halfLength = MathF.Max(bounds.Width, bounds.Height) * 0.75f;
        var start = center - direction * halfLength;
        var end = center + direction * halfLength;

        return new CanvasLinearGradientBrush(ds, new CanvasGradientStop[]
        {
            new() { Position = 0f, Color = primary },
            new() { Position = 1f, Color = secondary }
        })
        {
            StartPoint = start,
            EndPoint = end
        };
    }

    private CanvasRadialGradientBrush CreateRadialTextFillBrush(CanvasDrawingSession ds, Rect bounds, TextStyle style, float opacity)
    {
        var primary = ApplyOpacity(style.TextColor.ToWindowsColor(), opacity);
        var secondary = ApplyOpacity(style.FillSecondaryColor.ToWindowsColor(), opacity);

        return new CanvasRadialGradientBrush(ds, new CanvasGradientStop[]
        {
            new() { Position = 0f, Color = primary },
            new() { Position = 1f, Color = secondary }
        })
        {
            Center = bounds.Center.ToVector2(),
            RadiusX = MathF.Max(1f, bounds.Width * 0.5f),
            RadiusY = MathF.Max(1f, bounds.Height * 0.5f)
        };
    }

    private void RenderPatternFill(CanvasDrawingSession ds, Rect bounds, TextStyle style, float opacity)
    {
        var primary = ApplyOpacity(style.TextColor.ToWindowsColor(), opacity);
        var secondary = ApplyOpacity(style.FillSecondaryColor.ToWindowsColor(), opacity);
        RenderPatternFill(ds, bounds, primary, secondary, style.FillPattern, style.FillPatternScale, style.FillAngle);
    }

    private void RenderPatternFill(
        CanvasDrawingSession ds,
        Rect bounds,
        WinColor primary,
        WinColor secondary,
        TextFillPattern pattern,
        float scale,
        float angleDegrees = 0f)
    {
        scale = Math.Clamp(scale, 0.25f, 8f);
        ds.FillRectangle(bounds.ToWindowsRect(), secondary);

        var drawBounds = ExpandPatternBounds(bounds);
        var originalTransform = ds.Transform;
        if (MathF.Abs(angleDegrees) > 0.01f)
        {
            var radians = angleDegrees * MathF.PI / 180f;
            ds.Transform = Matrix3x2.CreateRotation(radians, bounds.Center.ToVector2()) * originalTransform;
        }

        try
        {
            switch (pattern)
            {
                case TextFillPattern.Dots:
                    {
                        var spacing = MathF.Max(4f, 10f * scale);
                        var radius = MathF.Max(1f, spacing * 0.22f);
                        var rowCount = Math.Max(1, (int)MathF.Ceiling(drawBounds.Height / spacing) + 2);
                        var colCount = Math.Max(1, (int)MathF.Ceiling(drawBounds.Width / spacing) + 2);
                        var startY = drawBounds.Top + spacing * 0.5f;
                        var startX = drawBounds.Left + spacing * 0.5f;

                        for (int row = 0; row < rowCount; row++)
                        {
                            var y = startY + (row * spacing);
                            for (int col = 0; col < colCount; col++)
                            {
                                var x = startX + (col * spacing);
                                ds.FillCircle(new Vector2(x, y), radius, primary);
                            }
                        }
                        break;
                    }

                case TextFillPattern.Checkerboard:
                    {
                        var cell = MathF.Max(4f, 9f * scale);
                        var rowCount = Math.Max(1, (int)MathF.Ceiling(drawBounds.Height / cell) + 1);
                        var colCount = Math.Max(1, (int)MathF.Ceiling(drawBounds.Width / cell) + 1);
                        var startY = drawBounds.Top;
                        var startX = drawBounds.Left;

                        for (int row = 0; row < rowCount; row++)
                        {
                            var y = startY + (row * cell);
                            for (int col = 0; col < colCount; col++)
                            {
                                if (((row + col) & 1) != 0) continue;
                                var x = startX + (col * cell);
                                ds.FillRectangle(x, y, cell, cell, primary);
                            }
                        }
                        break;
                    }

                case TextFillPattern.Crosshatch:
                    {
                        var spacing = MathF.Max(5f, 12f * scale);
                        var thickness = MathF.Max(1f, spacing * 0.12f);
                        var span = drawBounds.Width + drawBounds.Height;
                        var lineCount = Math.Max(1, (int)MathF.Ceiling((span * 2f) / spacing) + 1);
                        for (int i = 0; i < lineCount; i++)
                        {
                            var offset = -span + (i * spacing);
                            ds.DrawLine(
                                new Vector2(drawBounds.Left + offset, drawBounds.Top),
                                new Vector2(drawBounds.Left + offset + span, drawBounds.Bottom),
                                primary,
                                thickness);

                            ds.DrawLine(
                                new Vector2(drawBounds.Right - offset, drawBounds.Top),
                                new Vector2(drawBounds.Right - offset - span, drawBounds.Bottom),
                                primary,
                                thickness);
                        }
                        break;
                    }

                case TextFillPattern.DiagonalStripes:
                default:
                    {
                        var spacing = MathF.Max(5f, 12f * scale);
                        var thickness = MathF.Max(1f, spacing * 0.22f);
                        var span = drawBounds.Width + drawBounds.Height;
                        var lineCount = Math.Max(1, (int)MathF.Ceiling((span * 2f) / spacing) + 1);
                        for (int i = 0; i < lineCount; i++)
                        {
                            var offset = -span + (i * spacing);
                            ds.DrawLine(
                                new Vector2(drawBounds.Left + offset, drawBounds.Top),
                                new Vector2(drawBounds.Left + offset + span, drawBounds.Bottom),
                                primary,
                                thickness);
                        }
                        break;
                    }
            }
        }
        finally
        {
            ds.Transform = originalTransform;
        }
    }

    private static Rect ExpandPatternBounds(Rect bounds)
    {
        var diagonal = MathF.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height));
        var half = MathF.Max(1f, diagonal * 0.5f);
        var center = bounds.Center;
        return new Rect(center.X - half, center.Y - half, half * 2f, half * 2f);
    }

    private bool RenderImageFill(CanvasDrawingSession ds, CanvasGeometry geometry, Rect bounds, TextStyle style, float opacity)
    {
        return RenderImageFill(ds, geometry, bounds, style.FillImagePath, opacity);
    }

    private bool RenderImageFill(CanvasDrawingSession ds, CanvasGeometry geometry, Rect bounds, string? imagePath, float opacity)
    {
        if (_textFillImageResolver == null || string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        var bitmap = _textFillImageResolver(imagePath);
        if (bitmap == null)
        {
            return false;
        }

        using var layer = ds.CreateLayer(Math.Clamp(opacity, 0f, 1f), geometry);
        ds.DrawImage(bitmap, bounds.ToWindowsRect());
        return true;
    }

    private readonly struct TextStrokeLayer
    {
        public TextStrokeLayer(WinColor color, float width)
        {
            Color = color;
            Width = width;
        }

        public WinColor Color { get; }
        public float Width { get; }
    }

    private readonly struct TextShadowLayer
    {
        public TextShadowLayer(WinColor color, float offsetX, float offsetY, float blur)
        {
            Color = color;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Blur = blur;
        }

        public WinColor Color { get; }
        public float OffsetX { get; }
        public float OffsetY { get; }
        public float Blur { get; }
    }

    private static Rect GetLayoutDrawBounds(CanvasTextLayout layout, Vector2 origin)
    {
        var drawBounds = layout.DrawBounds;
        if (drawBounds.Width <= 0d && drawBounds.Height <= 0d)
        {
            drawBounds = layout.LayoutBounds;
        }

        return new Rect(
            origin.X + (float)drawBounds.X,
            origin.Y + (float)drawBounds.Y,
            MathF.Max(1f, (float)drawBounds.Width),
            MathF.Max(1f, (float)drawBounds.Height));
    }

    private readonly struct PathTextRenderResult
    {
        public PathTextRenderResult(Rect bounds, bool isOverflowing)
        {
            Bounds = bounds;
            IsOverflowing = isOverflowing;
        }

        public Rect Bounds { get; }
        public bool IsOverflowing { get; }
    }

    private readonly struct PathTextSample
    {
        public PathTextSample(Point2 point, Point2 tangent, float distance)
        {
            Point = point;
            Tangent = tangent;
            Distance = distance;
        }

        public Point2 Point { get; }
        public Point2 Tangent { get; }
        public float Distance { get; }
    }

    private PathTextRenderResult RenderTextOnPath(
        CanvasDrawingSession ds,
        string text,
        TextStyle style,
        TextPath path,
        Point2 origin,
        float opacity,
        bool applyOutline,
        float autoOutlineScale = 0f)
    {
        var displayText = (style.AllCaps ? text.ToUpperInvariant() : text)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        if (string.IsNullOrWhiteSpace(displayText))
        {
            var emptyBounds = new Rect(origin.X - 1f, origin.Y - 1f, 2f, 2f);
            return new PathTextRenderResult(emptyBounds, false);
        }

        var start = Math.Clamp(path.StartPosition, 0f, 1f);
        var end = Math.Clamp(path.EndPosition, 0f, 1f);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (MathF.Abs(end - start) < 0.0001f)
        {
            end = Math.Min(1f, start + 0.0001f);
        }

        var samples = BuildPathSamples(path, origin, start, end, path.ReverseDirection);
        var availableLength = samples[^1].Distance;
        if (availableLength <= 0.01f)
        {
            var emptyBounds = new Rect(origin.X - 1f, origin.Y - 1f, 2f, 2f);
            return new PathTextRenderResult(emptyBounds, false);
        }

        var format = TextLayoutUtilities.CreateTextFormat(style, spans: null);
        format.HorizontalAlignment = CanvasHorizontalAlignment.Left;
        format.VerticalAlignment = CanvasVerticalAlignment.Top;
        format.WordWrapping = CanvasWordWrapping.NoWrap;

        using var fullLayout = new CanvasTextLayout(ds, displayText, format, 4000f, 1000f);
        TextLayoutUtilities.ApplyTracking(fullLayout, style, displayText.Length);
        TextLayoutUtilities.ApplyTypographyFeatures(fullLayout, displayText.Length, TextLayoutUtilities.CreateTypographySettings(style));

        var clusters = fullLayout.ClusterMetrics;
        if (clusters == null || clusters.Length == 0)
        {
            var fallbackBounds = new Rect(origin.X - 1f, origin.Y - 1f, 2f, 2f);
            return new PathTextRenderResult(fallbackBounds, false);
        }

        var totalAdvance = clusters.Sum(metric => (float)metric.Width);
        var remaining = MathF.Max(0f, availableLength - totalAdvance);
        var anchorDistance = style.Alignment switch
        {
            TextAlignment.Center => remaining * 0.5f,
            TextAlignment.Right => remaining,
            _ => 0f
        };

        var isOverflowing = totalAdvance > availableLength + 0.5f;
        var baselineOffset = path.Offset + style.VerticalOffset;
        var strokeLayers = applyOutline
            ? BuildTextStrokeLayers(style, opacity, autoOutlineScale)
            : new List<TextStrokeLayer>();
        var maxEffectExtent = applyOutline
            ? GetMaxTextVisualEffectExtent(style, autoOutlineScale)
            : MathF.Max(GetMaxTextShadowExtent(style), GetMaxTextAdvancedEffectExtent(style));

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        var advanceCursor = anchorDistance;

        var runStartIndex = 0;
        foreach (var metric in clusters)
        {
            if (metric.CharacterCount <= 0)
            {
                continue;
            }

            var runLength = Math.Min(metric.CharacterCount, Math.Max(0, displayText.Length - runStartIndex));
            if (runLength <= 0)
            {
                break;
            }

            var runText = displayText.Substring(runStartIndex, runLength);
            if (string.IsNullOrEmpty(runText))
            {
                runStartIndex += runLength;
                continue;
            }

            var advance = (float)metric.Width;
            var centerDistance = advanceCursor + (advance * 0.5f);
            if (centerDistance < 0f || centerDistance > availableLength)
            {
                advanceCursor += advance;
                continue;
            }

            var sample = SamplePathAtDistance(samples, centerDistance);
            var tangent = sample.Tangent.Length > 0.0001f ? sample.Tangent.Normalized() : new Point2(1f, 0f);
            var normal = new Point2(-tangent.Y, tangent.X);
            var center = sample.Point + (normal * baselineOffset);

            if (TextWarpRenderer.IsWarpEnabled(style))
            {
                var progress = totalAdvance <= 0.0001f
                    ? 0f
                    : Math.Clamp((advanceCursor + (advance * 0.5f)) / totalAdvance, 0f, 1f);
                var normalizedOffset = TextWarpRenderer.GetNormalizedOffset(progress, 0.5f, style);
                var tangentShift = normalizedOffset.X * totalAdvance;
                var normalShift = normalizedOffset.Y * MathF.Max(style.FontSize * MathF.Max(1f, style.LineHeight), 1f);
                center += (tangent * tangentShift) + (normal * normalShift);
            }

            var angle = MathF.Atan2(tangent.Y, tangent.X);

            using var runLayout = new CanvasTextLayout(ds, runText, format, 2000f, 1000f);
            var runBounds = runLayout.DrawBounds;
            if (runBounds.Width <= 0f && runBounds.Height <= 0f)
            {
                runBounds = runLayout.LayoutBounds;
            }

            var width = MathF.Max(1f, (float)runBounds.Width);
            var height = MathF.Max(1f, (float)runBounds.Height);
            var drawOrigin = new Vector2(
                center.X - width * 0.5f - (float)runBounds.X,
                center.Y - height * 0.5f - (float)runBounds.Y);

            var previousTransform = ds.Transform;
            ds.Transform = Matrix3x2.CreateRotation(angle, center.ToVector2()) * previousTransform;
            DrawTextLayoutWithStrokes(ds, runLayout, drawOrigin, style, opacity, strokeLayers);
            ds.Transform = previousTransform;

            minX = MathF.Min(minX, center.X - width * 0.5f - maxEffectExtent);
            maxX = MathF.Max(maxX, center.X + width * 0.5f + maxEffectExtent);
            minY = MathF.Min(minY, center.Y - height * 0.5f - maxEffectExtent);
            maxY = MathF.Max(maxY, center.Y + height * 0.5f + maxEffectExtent);

            advanceCursor += advance;
            runStartIndex += runLength;
        }

        if (minX == float.MaxValue || minY == float.MaxValue)
        {
            var fallbackBounds = new Rect(origin.X - 1f, origin.Y - 1f, 2f, 2f);
            return new PathTextRenderResult(fallbackBounds, isOverflowing);
        }

        var bounds = new Rect(minX, minY, MathF.Max(1f, maxX - minX), MathF.Max(1f, maxY - minY));
        return new PathTextRenderResult(bounds, isOverflowing);
    }

    private static PathTextSample[] BuildPathSamples(TextPath path, Point2 origin, float startT, float endT, bool reverse)
    {
        const int sampleCount = 160;
        var samples = new PathTextSample[sampleCount + 1];
        var previousPoint = Point2.Zero;
        var accumulated = 0f;

        for (int i = 0; i <= sampleCount; i++)
        {
            var ratio = i / (float)sampleCount;
            var t = reverse
                ? endT - ((endT - startT) * ratio)
                : startT + ((endT - startT) * ratio);

            var localPoint = EvaluateBezier(path.Start, path.Control1, path.Control2, path.End, t);
            var localTangent = EvaluateBezierDerivative(path.Start, path.Control1, path.Control2, path.End, t);
            if (reverse)
            {
                localTangent = -localTangent;
            }

            var point = origin + localPoint;
            if (i > 0)
            {
                accumulated += Point2.Distance(previousPoint, point);
            }

            samples[i] = new PathTextSample(point, localTangent, accumulated);
            previousPoint = point;
        }

        return samples;
    }

    private static PathTextSample SamplePathAtDistance(PathTextSample[] samples, float distance)
    {
        if (samples.Length == 0)
        {
            return new PathTextSample(Point2.Zero, new Point2(1f, 0f), 0f);
        }

        if (distance <= 0f)
        {
            return samples[0];
        }

        if (distance >= samples[^1].Distance)
        {
            return samples[^1];
        }

        for (int i = 1; i < samples.Length; i++)
        {
            if (samples[i].Distance < distance) continue;

            var previous = samples[i - 1];
            var current = samples[i];
            var segmentLength = current.Distance - previous.Distance;
            if (segmentLength <= 0.0001f)
            {
                return current;
            }

            var t = (distance - previous.Distance) / segmentLength;
            var point = Point2.Lerp(previous.Point, current.Point, t);
            var tangent = Point2.Lerp(previous.Tangent, current.Tangent, t).Normalized();
            return new PathTextSample(point, tangent, distance);
        }

        return samples[^1];
    }

    private static Point2 EvaluateBezier(Point2 p0, Point2 p1, Point2 p2, Point2 p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        return (p0 * uuu)
             + (p1 * (3f * uu * t))
             + (p2 * (3f * u * tt))
             + (p3 * ttt);
    }

    private static Point2 EvaluateBezierDerivative(Point2 p0, Point2 p1, Point2 p2, Point2 p3, float t)
    {
        var u = 1f - t;
        return ((p1 - p0) * (3f * u * u))
             + ((p2 - p1) * (6f * u * t))
             + ((p3 - p2) * (3f * t * t));
    }

    private void RenderTextPathGuide(CanvasDrawingSession ds, Point2 ownerPosition, float ownerRotation, TextPath path)
    {
        var start = TextPath.LocalToWorld(path.Start, ownerPosition, ownerRotation);
        var control1 = TextPath.LocalToWorld(path.Control1, ownerPosition, ownerRotation);
        var control2 = TextPath.LocalToWorld(path.Control2, ownerPosition, ownerRotation);
        var end = TextPath.LocalToWorld(path.End, ownerPosition, ownerRotation);

        var pathColor = WinColor.FromArgb(220, 0, 180, 120);
        var helperColor = WinColor.FromArgb(180, 90, 90, 90);
        var handleFill = WinColor.FromArgb(255, 255, 255, 255);
        var handleBorder = pathColor;
        var controlFill = WinColor.FromArgb(255, 240, 240, 240);
        var controlBorder = WinColor.FromArgb(220, 70, 70, 70);

        using var helperStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        ds.DrawLine(start.ToVector2(), control1.ToVector2(), helperColor, 1.2f, helperStyle);
        ds.DrawLine(end.ToVector2(), control2.ToVector2(), helperColor, 1.2f, helperStyle);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(start.ToVector2());
        pathBuilder.AddCubicBezier(control1.ToVector2(), control2.ToVector2(), end.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Open);
        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.DrawGeometry(geometry, pathColor, 2f);

        DrawHandle(ds, start, 9f, handleFill, handleBorder);
        DrawHandle(ds, end, 9f, handleFill, handleBorder);
        DrawHandle(ds, control1, 7f, controlFill, controlBorder);
        DrawHandle(ds, control2, 7f, controlFill, controlBorder);
    }

    private void RenderBalloonLinks(CanvasDrawingSession ds, Page page, bool drawFill = true, bool drawStroke = true)
    {
        if (!drawFill && !drawStroke) return;
        if (page.BalloonLinks.Count == 0) return;

        var style = page.BalloonLinkStyle;
        var fillColor = style.FillColor.ToWindowsColor();
        var strokeColor = style.StrokeColor.ToWindowsColor();
        var strokeWidth = MathF.Max(0.5f, style.StrokeWidth);
        var connectorWidth = MathF.Max(8f, style.ConnectorWidth);

        CanvasStrokeStyle? strokeStyle = null;
        if (style.DashStyle != LinkDashStyle.Solid)
        {
            strokeStyle = new CanvasStrokeStyle { DashStyle = ToCanvasDashStyle(style.DashStyle) };
        }

        foreach (var link in page.BalloonLinks)
        {
            var balloonA = page.FindBalloon(link.FirstId);
            var balloonB = page.FindBalloon(link.SecondId);
            if (balloonA == null || balloonB == null) continue;
            if (!page.IsBalloonEffectivelyVisible(balloonA.Id) || !page.IsBalloonEffectivelyVisible(balloonB.Id)) continue;

            var direction = (balloonB.Position - balloonA.Position);
            if (direction.Length < 0.001f) continue;

            var normalizedDir = direction / direction.Length;

            var start = TailGeometry.ComputeAttachmentPoint(balloonA, direction);
            var end = TailGeometry.ComputeAttachmentPoint(balloonB, -direction);

            var extensionAmount = connectorWidth * 0.6f;
            var extendedStart = start - normalizedDir * extensionAmount;
            var extendedEnd = end + normalizedDir * extensionAmount;

            var connectorGeometry = CreateConnectorGeometry(ds, extendedStart, extendedEnd, connectorWidth);
            if (connectorGeometry == null) continue;

            var balloonGeomA = CreateBalloonGeometry(ds, balloonA, balloonA.Bounds, balloonA.BalloonStyle);
            var balloonGeomB = CreateBalloonGeometry(ds, balloonB, balloonB.Bounds, balloonB.BalloonStyle);

            var combined = connectorGeometry.CombineWith(balloonGeomA, Matrix3x2.Identity, CanvasGeometryCombine.Union);
            var finalCombined = combined.CombineWith(balloonGeomB, Matrix3x2.Identity, CanvasGeometryCombine.Union);

            var geometriesToDispose = new List<CanvasGeometry> { connectorGeometry, balloonGeomA, balloonGeomB, combined };

            foreach (var balloon in new[] { balloonA, balloonB })
            {
                var mergeTails = balloon.Tails.Where(t => t.Style == TailStyle.Pointer || t.Style == TailStyle.Curved).ToList();
                foreach (var tail in mergeTails)
                {
                    var tailGeometry = CreateTailGeometry(ds, balloon, tail);
                    if (tailGeometry != null)
                    {
                        var newCombined = finalCombined.CombineWith(tailGeometry, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                        geometriesToDispose.Add(finalCombined);
                        geometriesToDispose.Add(tailGeometry);
                        finalCombined = newCombined;
                    }
                }
            }

            if (drawFill)
            {
                ds.FillGeometry(connectorGeometry, fillColor);
            }

            if (drawStroke)
            {
                if (strokeStyle != null)
                {
                    ds.DrawGeometry(finalCombined, strokeColor, strokeWidth, strokeStyle);
                }
                else
                {
                    ds.DrawGeometry(finalCombined, strokeColor, strokeWidth);
                }
            }

            foreach (var geom in geometriesToDispose)
            {
                geom.Dispose();
            }
            finalCombined.Dispose();
        }

        strokeStyle?.Dispose();
    }

    private CanvasGeometry? CreateConnectorGeometry(CanvasDrawingSession ds, Point2 start, Point2 end, float width)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < 0.001f) return null;

        var normalized = direction / length;
        var perpendicular = new Point2(-normalized.Y, normalized.X);
        var halfWidth = width / 2f;

        var corner1 = start + perpendicular * halfWidth;
        var corner2 = start - perpendicular * halfWidth;
        var corner3 = end - perpendicular * halfWidth;
        var corner4 = end + perpendicular * halfWidth;

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(corner1.ToVector2());
        pathBuilder.AddLine(corner4.ToVector2());
        pathBuilder.AddLine(corner3.ToVector2());
        pathBuilder.AddLine(corner2.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        return CanvasGeometry.CreatePath(pathBuilder);
    }

    private static CanvasDashStyle ToCanvasDashStyle(LinkDashStyle dashStyle)
    {
        return dashStyle switch
        {
            LinkDashStyle.Dash => CanvasDashStyle.Dash,
            LinkDashStyle.Dot => CanvasDashStyle.Dot,
            LinkDashStyle.DashDot => CanvasDashStyle.DashDot,
            _ => CanvasDashStyle.Solid
        };
    }

    private void RenderGuides(CanvasDrawingSession ds, Page? page)
    {
        if (page == null || page.Guides.Count == 0) return;

        var strokeWidth = 1f / _viewTransform.Zoom;
        var guideColor = page.GuidesLocked
            ? WinColor.FromArgb(190, 255, 185, 85)
            : WinColor.FromArgb(180, 0, 120, 215);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        using var lockFormat = new CanvasTextFormat
        {
            FontFamily = "Segoe MDL2 Assets",
            FontSize = 10f / MathF.Max(0.1f, _viewTransform.Zoom),
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };

        foreach (var guide in page.Guides)
        {
            if (guide.Orientation == GuideOrientation.Horizontal)
            {
                ds.DrawLine(0, guide.Position, page.Size.Width, guide.Position, guideColor, strokeWidth, strokeStyle);
            }
            else
            {
                ds.DrawLine(guide.Position, 0, guide.Position, page.Size.Height, guideColor, strokeWidth, strokeStyle);
            }

            if (page.GuidesLocked)
            {
                RenderGuideLockBadge(ds, page, guide, lockFormat);
            }
        }
    }

    private void RenderGuideLockBadge(CanvasDrawingSession ds, Page page, Guide guide, CanvasTextFormat textFormat)
    {
        var zoom = MathF.Max(0.1f, _viewTransform.Zoom);
        var badgeSize = 14f / zoom;
        var margin = 3f / zoom;
        var badgeFill = WinColor.FromArgb(220, 28, 28, 28);
        var badgeStroke = WinColor.FromArgb(220, 255, 185, 85);
        var iconColor = WinColor.FromArgb(255, 255, 205, 130);

        Rect badge;
        if (guide.Orientation == GuideOrientation.Horizontal)
        {
            var y = Math.Clamp(guide.Position - badgeSize * 0.5f, margin, page.Size.Height - badgeSize - margin);
            badge = new Rect(margin, y, badgeSize, badgeSize);
        }
        else
        {
            var x = Math.Clamp(guide.Position - badgeSize * 0.5f, margin, page.Size.Width - badgeSize - margin);
            badge = new Rect(x, margin, badgeSize, badgeSize);
        }

        ds.FillRoundedRectangle(badge.ToWindowsRect(), 2f / zoom, 2f / zoom, badgeFill);
        ds.DrawRoundedRectangle(badge.ToWindowsRect(), 2f / zoom, 2f / zoom, badgeStroke, 1f / zoom);
        ds.DrawText("\uE72E", badge.ToWindowsRect(), iconColor, textFormat);
    }

    private void RenderPanelGutters(CanvasDrawingSession ds, Page page, PanelBoundaryVisibilityMode visibilityMode, Guid? hoveredPanelId, bool forceAll)
    {
        var gutterWidth = MathF.Max(0f, page.PanelGutterWidth);
        var showHoverOnly = visibilityMode == PanelBoundaryVisibilityMode.Hover && !forceAll;

        if (gutterWidth <= 0f && page.PanelGutterStrokeStyle == PanelBorderStyle.None && !page.PanelGutterFillEnabled)
        {
            return;
        }

        var gutterColor = page.PanelGutterColor.ToWindowsColor();
        var strokeWidth = MathF.Max(0.8f, 1.1f / _viewTransform.Zoom);

        CanvasStrokeStyle? strokeStyle = page.PanelGutterStrokeStyle switch
        {
            PanelBorderStyle.Dashed => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash },
            PanelBorderStyle.Dotted => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot },
            PanelBorderStyle.DashDot => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot },
            _ => null
        };

        foreach (var panel in page.Panels)
        {
            if (showHoverOnly && hoveredPanelId != panel.Id) continue;
            if (!panel.IsVisible) continue;

            using var geometry = PanelGeometry.CreateGeometry(ds, panel);

            if (page.PanelGutterFillEnabled && gutterWidth > 0.1f)
            {
                using var strokeGeometry = geometry.Stroke(gutterWidth);
                ds.FillGeometry(strokeGeometry, gutterColor);
            }

            if (page.PanelGutterStrokeStyle != PanelBorderStyle.None)
            {
                if (strokeStyle != null)
                {
                    ds.DrawGeometry(geometry, gutterColor, strokeWidth, strokeStyle);
                }
                else
                {
                    ds.DrawGeometry(geometry, gutterColor, strokeWidth);
                }
            }
        }

        strokeStyle?.Dispose();
    }

    private void RenderPanelBorders(CanvasDrawingSession ds, Page page, PanelBoundaryVisibilityMode visibilityMode, Guid? hoveredPanelId)
    {
        var zoom = _viewTransform.Zoom;
        var hoverOnly = visibilityMode == PanelBoundaryVisibilityMode.Hover;

        foreach (var panel in page.Panels)
        {
            if (hoverOnly && hoveredPanelId != panel.Id) continue;
            if (!panel.IsVisible) continue;
            if (panel.BorderStyle == PanelBorderStyle.None) continue;

            var borderColor = panel.BorderColor.ToWindowsColor();
            var minScreenWidth = 1.0f / zoom; // At least 1 pixel on screen
            var strokeWidth = MathF.Max(minScreenWidth, panel.BorderWidth);

            using var geometry = PanelGeometry.CreateGeometry(ds, panel);

            CanvasStrokeStyle? strokeStyle = panel.BorderStyle switch
            {
                PanelBorderStyle.Dashed => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash },
                PanelBorderStyle.Dotted => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot },
                PanelBorderStyle.DashDot => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot },
                _ => null
            };

            if (strokeStyle != null)
            {
                ds.DrawGeometry(geometry, borderColor, strokeWidth, strokeStyle);
                strokeStyle.Dispose();
            }
            else
            {
                ds.DrawGeometry(geometry, borderColor, strokeWidth);
            }
        }
    }

    private void RenderPanelSafeGuides(CanvasDrawingSession ds, Page page, IReadOnlyList<PanelSafeGuideHint> guides)
    {
        var strokeWidth = MathF.Max(1f, 1.1f / _viewTransform.Zoom);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot };
        var normalColor = WinColor.FromArgb(170, 90, 210, 210);
        var insideColor = WinColor.FromArgb(200, 90, 200, 120);
        var outsideColor = WinColor.FromArgb(220, 220, 80, 80);
        using var labelFormat = new CanvasTextFormat
        {
            FontSize = 11f / MathF.Max(0.1f, _viewTransform.Zoom),
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top
        };

        foreach (var guide in guides)
        {
            var panel = page.FindPanel(guide.PanelId);
            if (panel == null || !panel.IsVisible) continue;
            if (panel.SafeMargin <= 0f) continue;

            var safeBounds = panel.Bounds.Inflate(-panel.SafeMargin, -panel.SafeMargin);
            if (safeBounds.Width <= 1f || safeBounds.Height <= 1f) continue;

            var color = guide.Kind switch
            {
                PanelSafeGuideHintKind.Outside => outsideColor,
                PanelSafeGuideHintKind.Inside => insideColor,
                _ => normalColor
            };

            ds.DrawRectangle(safeBounds.ToWindowsRect(), color, strokeWidth, strokeStyle);

            var label = $"SAFE {panel.SafeMargin:F0}px";
            var zoom = MathF.Max(0.1f, _viewTransform.Zoom);
            var paddingX = 4f / zoom;
            var paddingY = 2f / zoom;
            using var layout = new CanvasTextLayout(ds, label, labelFormat, page.Size.Width, 1000f);
            var labelWidth = (float)layout.LayoutBounds.Width + paddingX * 2f;
            var labelHeight = (float)layout.LayoutBounds.Height + paddingY * 2f;
            var labelRect = new Rect(
                safeBounds.Left + paddingX,
                MathF.Max(0f, safeBounds.Top - labelHeight - 2f / zoom),
                labelWidth,
                labelHeight);

            var bg = WinColor.FromArgb(180, 24, 30, 36);
            ds.FillRoundedRectangle(labelRect.ToWindowsRect(), 2f / zoom, 2f / zoom, bg);
            ds.DrawText(label, new Vector2(labelRect.Left + paddingX, labelRect.Top + paddingY), color, labelFormat);
        }
    }

    private void RenderPanelOverlay(CanvasDrawingSession ds, Page page, Guid? selectedPanelId, IReadOnlyCollection<Guid>? selectedPanelIds, Rect? previewBounds)
    {
        var zoom = _viewTransform.Zoom;
        var strokeWidth = MathF.Max(1f, 1.25f / zoom);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        using var safeStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot };
        using var bleedStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot };

        HashSet<Guid>? selectedSet = null;
        if (selectedPanelIds != null)
        {
            selectedSet = new HashSet<Guid>(selectedPanelIds);
        }
        else if (selectedPanelId.HasValue)
        {
            selectedSet = new HashSet<Guid> { selectedPanelId.Value };
        }

        using var orderTextFormat = new CanvasTextFormat
        {
            FontSize = 24f / zoom, // Scale text with zoom
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };

        foreach (var panel in page.Panels)
        {
            if (!panel.IsVisible) continue;

            var strokeColor = panel.Color.ToWindowsColor();
            var fillAlpha = (byte)Math.Clamp(strokeColor.A / 5, 18, 80);
            var fillColor = WinColor.FromArgb(fillAlpha, strokeColor.R, strokeColor.G, strokeColor.B);
            using var geometry = PanelGeometry.CreateGeometry(ds, panel);
            ds.FillGeometry(geometry, fillColor);
            ds.DrawGeometry(geometry, strokeColor, strokeWidth, strokeStyle);

            if (panel.SafeMargin > 0f)
            {
                var safeBounds = panel.Bounds.Inflate(-panel.SafeMargin, -panel.SafeMargin);
                if (safeBounds.Width > 1f && safeBounds.Height > 1f)
                {
                    var safeColor = WinColor.FromArgb(160, 220, 220, 220);
                    ds.DrawRectangle(safeBounds.ToWindowsRect(), safeColor, strokeWidth, safeStrokeStyle);
                }
            }

            if (panel.BleedLeft > 0f || panel.BleedTop > 0f || panel.BleedRight > 0f || panel.BleedBottom > 0f)
            {
                var bleedBounds = new Rect(
                    panel.Bounds.X - panel.BleedLeft,
                    panel.Bounds.Y - panel.BleedTop,
                    panel.Bounds.Width + panel.BleedLeft + panel.BleedRight,
                    panel.Bounds.Height + panel.BleedTop + panel.BleedBottom);

                if (bleedBounds.Width > 1f && bleedBounds.Height > 1f)
                {
                    var bleedColor = WinColor.FromArgb(190, 210, 90, 90);
                    ds.DrawRectangle(bleedBounds.ToWindowsRect(), bleedColor, strokeWidth, bleedStrokeStyle);
                }
            }

            var orderBadgeSize = 28f / zoom;
            var orderBadgeMargin = 8f / zoom;
            var badgeX = (float)panel.Bounds.X + orderBadgeMargin;
            var badgeY = (float)panel.Bounds.Y + orderBadgeMargin;
            var badgeRect = new Windows.Foundation.Rect(badgeX, badgeY, orderBadgeSize, orderBadgeSize);

            var badgeBgColor = WinColor.FromArgb(220, strokeColor.R, strokeColor.G, strokeColor.B);
            ds.FillRoundedRectangle(badgeRect, orderBadgeSize / 4, orderBadgeSize / 4, badgeBgColor);

            var orderText = panel.Order.ToString();
            ds.DrawText(orderText, badgeRect, WinColor.FromArgb(255, 255, 255, 255), orderTextFormat);

            if (selectedSet != null && selectedSet.Contains(panel.Id))
            {
                var drawHandles = selectedPanelId.HasValue && panel.Id == selectedPanelId.Value;
                RenderSelectionHighlight(ds, panel.Bounds, drawHandles: drawHandles);
            }
        }

        var showWarning = RenderReadingFlowArrows(ds, page, zoom);
        if (showWarning)
        {
            var warningText = UiLocalizationService.GetString("render.warning.reading_order_unusual");
            using var warningFormat = new CanvasTextFormat
            {
                FontSize = 14f / zoom,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            using var layout = new CanvasTextLayout(ds, warningText, warningFormat, page.Size.Width, 1000f);
            var padding = 6f / zoom;
            var rect = new Rect(10f / zoom, 10f / zoom, (float)layout.LayoutBounds.Width + padding * 2f, (float)layout.LayoutBounds.Height + padding * 2f);
            var bgColor = WinColor.FromArgb(180, 80, 20, 20);
            var textColor = WinColor.FromArgb(255, 255, 235, 235);
            ds.FillRoundedRectangle(rect.ToWindowsRect(), 4f / zoom, 4f / zoom, bgColor);
            ds.DrawText(warningText, new Vector2(rect.X + padding, rect.Y + padding), textColor, warningFormat);
        }

        if (previewBounds.HasValue)
        {
            var previewColor = WinColor.FromArgb(200, 240, 240, 240);
            var previewFill = WinColor.FromArgb(25, 240, 240, 240);
            ds.FillRectangle(previewBounds.Value.ToWindowsRect(), previewFill);
            ds.DrawRectangle(previewBounds.Value.ToWindowsRect(), previewColor, strokeWidth, strokeStyle);
        }
    }

    private bool RenderReadingFlowArrows(CanvasDrawingSession ds, Page page, float zoom)
    {
        var panels = page.Panels.OrderBy(p => p.Order).ToList();
        if (panels.Count < 2) return false;

        var arrowWidth = MathF.Max(1f, 1.1f / zoom);
        var normalColor = WinColor.FromArgb(180, 80, 200, 255);
        var warningColor = WinColor.FromArgb(220, 220, 80, 80);
        var hasWarnings = false;
        var allowWarnings = page.ReadingDirection != ReadingDirection.Manual;

        for (int i = 0; i < panels.Count - 1; i++)
        {
            var from = panels[i].Bounds;
            var to = panels[i + 1].Bounds;
            var warn = allowWarnings && IsReadingOrderReverse(from, to, page.ReadingDirection);
            if (warn) hasWarnings = true;

            var color = warn ? warningColor : normalColor;
            DrawArrow(ds, from.Center, to.Center, color, arrowWidth, zoom);
        }

        return hasWarnings;
    }

    private static bool IsReadingOrderReverse(Rect from, Rect to, ReadingDirection direction)
    {
        var delta = to.Center - from.Center;
        var rowTolerance = MathF.Max(12f, MathF.Min(from.Height, to.Height) * 0.45f);
        var columnTolerance = MathF.Max(12f, MathF.Min(from.Width, to.Width) * 0.2f);

        if (delta.Y < -rowTolerance) return true;

        if (MathF.Abs(delta.Y) <= rowTolerance)
        {
            return direction == ReadingDirection.RightToLeft
                ? delta.X > columnTolerance
                : delta.X < -columnTolerance;
        }

        return false;
    }

    private static void DrawArrow(CanvasDrawingSession ds, Point2 from, Point2 to, WinColor color, float strokeWidth, float zoom)
    {
        var dir = new Vector2(to.X - from.X, to.Y - from.Y);
        if (dir.LengthSquared() < 0.001f) return;

        dir = Vector2.Normalize(dir);
        var start = new Vector2(from.X, from.Y);
        var end = new Vector2(to.X, to.Y);
        ds.DrawLine(start, end, color, strokeWidth);

        var headSize = 10f / zoom;
        var angle = MathF.PI / 6f;
        var left = Rotate(dir, -angle);
        var right = Rotate(dir, angle);
        var tip = end;
        var leftPoint = tip - left * headSize;
        var rightPoint = tip - right * headSize;

        ds.DrawLine(tip, leftPoint, color, strokeWidth);
        ds.DrawLine(tip, rightPoint, color, strokeWidth);
    }

    private static Vector2 Rotate(Vector2 vec, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(vec.X * cos - vec.Y * sin, vec.X * sin + vec.Y * cos);
    }

    private void RenderSmartGuides(CanvasDrawingSession ds, Document document, IReadOnlyList<SmartGuideLine>? guides)
    {
        if (guides == null || guides.Count == 0) return;

        var zoom = MathF.Max(0.1f, _viewTransform.Zoom);
        var strokeWidth = 1f / zoom;
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };

        foreach (var guide in guides)
        {
            var color = guide.Kind switch
            {
                SmartGuideKind.Grid => WinColor.FromArgb(160, 200, 200, 200),
                SmartGuideKind.Guide => WinColor.FromArgb(200, 0, 160, 240),
                _ => WinColor.FromArgb(200, 255, 0, 180)
            };
            var width = guide.Kind == SmartGuideKind.Guide
                ? MathF.Max(strokeWidth, 1.8f / zoom)
                : strokeWidth;

            if (guide.Orientation == GuideOrientation.Horizontal)
            {
                ds.DrawLine(0, guide.Position, document.Size.Width, guide.Position, color, width, strokeStyle);
            }
            else
            {
                ds.DrawLine(guide.Position, 0, guide.Position, document.Size.Height, color, width, strokeStyle);
            }
        }
    }

    private void RenderSnapFeedback(CanvasDrawingSession ds, SnapFeedback feedback)
    {
        _ = ds;
        _ = feedback;
    }

    private void RenderRulers(CanvasDrawingSession ds, Document document)
    {
        var viewport = _viewTransform.ViewportSize;
        if (viewport.Width <= 0 || viewport.Height <= 0) return;

        var zoom = _viewTransform.Zoom;
        var pan = _viewTransform.PanOffset;
        var visible = _viewTransform.GetVisibleWorldRect();

        ds.Transform = Matrix3x2.Identity;

        var background = WinColor.FromArgb(235, 32, 32, 32);
        var border = WinColor.FromArgb(255, 55, 55, 55);
        var tickColor = WinColor.FromArgb(200, 230, 230, 230);

        ds.FillRectangle(0, 0, viewport.Width, RulerThickness, background);
        ds.FillRectangle(0, 0, RulerThickness, viewport.Height, background);
        ds.DrawLine(0, RulerThickness, viewport.Width, RulerThickness, border, 1f);
        ds.DrawLine(RulerThickness, 0, RulerThickness, viewport.Height, border, 1f);

        using var textFormat = new CanvasTextFormat
        {
            FontSize = 9f,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };

        var unitLabel = string.IsNullOrWhiteSpace(document.DefaultUnits) ? "px" : document.DefaultUnits;
        ds.DrawText(unitLabel, new Vector2(4f, 2f), tickColor, textFormat);

        var step = GetRulerStep(zoom);
        var minorStep = step / 5f;
        var drawMinor = minorStep >= 1f && minorStep < step;

        var startX = MathF.Floor(visible.Left / (drawMinor ? minorStep : step)) * (drawMinor ? minorStep : step);
        var endX = visible.Right;
        for (var x = startX; x <= endX; x += drawMinor ? minorStep : step)
        {
            var screenX = x * zoom + pan.X;
            if (screenX < RulerThickness || screenX > viewport.Width) continue;

            var isMajor = MathF.Abs(x - MathF.Round(x / step) * step) < 0.01f;
            var tickLength = isMajor ? 10f : 6f;
            ds.DrawLine(screenX, RulerThickness, screenX, RulerThickness - tickLength, tickColor, 1f);

            if (isMajor)
            {
                var label = MathF.Round(x).ToString("0");
                ds.DrawText(label, new Vector2(screenX + 2f, 2f), tickColor, textFormat);
            }
        }

        var startY = MathF.Floor(visible.Top / (drawMinor ? minorStep : step)) * (drawMinor ? minorStep : step);
        var endY = visible.Bottom;
        for (var y = startY; y <= endY; y += drawMinor ? minorStep : step)
        {
            var screenY = y * zoom + pan.Y;
            if (screenY < RulerThickness || screenY > viewport.Height) continue;

            var isMajor = MathF.Abs(y - MathF.Round(y / step) * step) < 0.01f;
            var tickLength = isMajor ? 10f : 6f;
            ds.DrawLine(RulerThickness, screenY, RulerThickness - tickLength, screenY, tickColor, 1f);

            if (isMajor)
            {
                var label = MathF.Round(y).ToString("0");
                ds.DrawText(label, new Vector2(2f, screenY + 1f), tickColor, textFormat);
            }
        }
    }

    private static float GetRulerStep(float zoom)
    {
        var steps = new float[] { 1f, 2f, 5f, 10f, 20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f };
        foreach (var step in steps)
        {
            if (step * zoom >= RulerTargetStepPixels)
            {
                return step;
            }
        }

        return steps[^1];
    }

    private void RenderOffPanelTailIndicators(CanvasDrawingSession ds, Document document, HashSet<Guid>? selectionSet, Guid? primarySelection)
    {
        if (selectionSet == null || selectionSet.Count == 0) return;

        var visible = _viewTransform.GetVisibleWorldRect();
        var pageBounds = new Rect(0, 0, document.Size.Width, document.Size.Height);
        var viewport = _viewTransform.ViewportSize;
        var style = document.ActivePage?.OffPanelIndicatorStyle ?? OffPanelIndicatorStyle.Default;

        ds.Transform = Matrix3x2.Identity;

        foreach (var layer in document.Layers)
        {
            if (layer.Kind != LayerKind.Balloon) continue;
            if (!layer.IsVisible) continue;

            foreach (var balloon in layer.Balloons)
            {
                if (!balloon.IsVisible) continue;
                if (!selectionSet.Contains(balloon.Id)) continue;

                foreach (var tail in balloon.Tails)
                {
                    var target = tail.TargetPoint;

                    if (!pageBounds.Contains(target))
                    {
                        RenderOffPanelIndicator(ds, balloon.Position, target, pageBounds, viewport, style);
                    }
                }
            }
        }
    }

    private void RenderOffPanelIndicator(CanvasDrawingSession ds, Point2 balloonCenter, Point2 targetPoint, Rect visible, Size2 viewport, OffPanelIndicatorStyle style)
    {
        var edgePoint = ComputeViewportEdgeIntersection(balloonCenter, targetPoint, visible);
        if (!edgePoint.HasValue) return;

        var screenEdge = _viewTransform.WorldToScreen(edgePoint.Value);

        var baseSize = MathF.Max(8f, style.Size);
        var margin = MathF.Max(12f, baseSize + 6f);
        const float rulerOffset = RulerThickness + 4f;
        screenEdge = new Point2(
            MathF.Max(rulerOffset + margin, MathF.Min(viewport.Width - margin, screenEdge.X)),
            MathF.Max(rulerOffset + margin, MathF.Min(viewport.Height - margin, screenEdge.Y))
        );

        var screenTarget = _viewTransform.WorldToScreen(targetPoint);
        var direction = (screenTarget - screenEdge).Normalized();

        var indicatorColor = style.Color.ToWindowsColor();
        var fillAlpha = (byte)Math.Clamp(indicatorColor.A * 0.5f, 40f, 220f);
        var fillColor = WinColor.FromArgb(fillAlpha, indicatorColor.R, indicatorColor.G, indicatorColor.B);
        var arrowLength = baseSize;
        var arrowWidth = baseSize * 0.6f;
        var strokeWidth = MathF.Max(1.5f, baseSize * 0.12f);
        var circleRadius = baseSize * 0.25f;

        var tipPoint = screenEdge + direction * (arrowLength / 2);
        var basePoint = screenEdge - direction * (arrowLength / 2);

        var perp = new Point2(-direction.Y, direction.X);
        var leftWing = basePoint + perp * (arrowWidth / 2);
        var rightWing = basePoint - perp * (arrowWidth / 2);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(tipPoint.ToVector2());
        pathBuilder.AddLine(leftWing.ToVector2());
        pathBuilder.AddLine(rightWing.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, indicatorColor, strokeWidth);

        ds.FillCircle(basePoint.ToVector2(), circleRadius, fillColor);
        ds.DrawCircle(basePoint.ToVector2(), circleRadius, indicatorColor, strokeWidth * 0.9f);
    }

    private Point2? ComputeViewportEdgeIntersection(Point2 from, Point2 to, Rect visible)
    {
        var direction = to - from;
        if (direction.Length < 0.001f) return null;

        float? tMin = null;

        if (MathF.Abs(direction.X) > 0.001f)
        {
            var t = (visible.Left - from.X) / direction.X;
            if (t > 0 && t <= 1)
            {
                var y = from.Y + t * direction.Y;
                if (y >= visible.Top && y <= visible.Bottom)
                {
                    if (!tMin.HasValue || t < tMin.Value) tMin = t;
                }
            }
        }

        if (MathF.Abs(direction.X) > 0.001f)
        {
            var t = (visible.Right - from.X) / direction.X;
            if (t > 0 && t <= 1)
            {
                var y = from.Y + t * direction.Y;
                if (y >= visible.Top && y <= visible.Bottom)
                {
                    if (!tMin.HasValue || t < tMin.Value) tMin = t;
                }
            }
        }

        if (MathF.Abs(direction.Y) > 0.001f)
        {
            var t = (visible.Top - from.Y) / direction.Y;
            if (t > 0 && t <= 1)
            {
                var x = from.X + t * direction.X;
                if (x >= visible.Left && x <= visible.Right)
                {
                    if (!tMin.HasValue || t < tMin.Value) tMin = t;
                }
            }
        }

        if (MathF.Abs(direction.Y) > 0.001f)
        {
            var t = (visible.Bottom - from.Y) / direction.Y;
            if (t > 0 && t <= 1)
            {
                var x = from.X + t * direction.X;
                if (x >= visible.Left && x <= visible.Right)
                {
                    if (!tMin.HasValue || t < tMin.Value) tMin = t;
                }
            }
        }

        if (tMin.HasValue)
        {
            return from + direction * tMin.Value;
        }

        return GetNearestEdgePoint(to, visible);
    }

    private static Point2 GetNearestEdgePoint(Point2 target, Rect visible)
    {
        var x = MathF.Max(visible.Left, MathF.Min(visible.Right, target.X));
        var y = MathF.Max(visible.Top, MathF.Min(visible.Bottom, target.Y));
        return new Point2(x, y);
    }

    private void RenderGrid(CanvasDrawingSession ds, Size2 documentSize)
    {
        if (!ShowGrid) return;

        var zoom = MathF.Max(_viewTransform.Zoom, 0.0001f);
        var minorSpacing = MathF.Max(1f, GridMinorSpacing);
        var majorSpacing = MathF.Max(minorSpacing, GridMajorSpacing);
        if (minorSpacing <= 0.001f || majorSpacing <= 0.001f) return;

        const float minScreenSpacing = 8f;
        if (minorSpacing * zoom < minScreenSpacing)
        {
            var factor = MathF.Ceiling(minScreenSpacing / (minorSpacing * zoom));
            minorSpacing *= MathF.Max(1f, factor);
        }

        if (majorSpacing * zoom < minScreenSpacing * 1.5f)
        {
            var factor = MathF.Ceiling((minScreenSpacing * 1.5f) / (majorSpacing * zoom));
            majorSpacing *= MathF.Max(1f, factor);
        }

        var visible = _viewTransform.GetVisibleWorldRect();
        var left = MathF.Max(0f, visible.Left);
        var right = MathF.Min(documentSize.Width, visible.Right);
        var top = MathF.Max(0f, visible.Top);
        var bottom = MathF.Min(documentSize.Height, visible.Bottom);
        if (right <= left || bottom <= top) return;

        var majorColor = GridBaseColor;
        var minorColor = ScaleColorAlpha(GridBaseColor, 0.45f);
        var lineWidth = 1f / zoom;

        DrawGridAxisLines(ds, left, right, top, bottom, minorSpacing, majorSpacing, lineWidth, minorColor, majorColor, isVertical: true);
        DrawGridAxisLines(ds, top, bottom, left, right, minorSpacing, majorSpacing, lineWidth, minorColor, majorColor, isVertical: false);
    }

    private static void DrawGridAxisLines(
        CanvasDrawingSession ds,
        float axisStart,
        float axisEnd,
        float crossStart,
        float crossEnd,
        float minorSpacing,
        float majorSpacing,
        float lineWidth,
        WinColor minorColor,
        WinColor majorColor,
        bool isVertical)
    {
        var start = MathF.Floor(axisStart / minorSpacing) * minorSpacing;
        var maxLines = 12000;
        var lineCount = 0;

        for (var value = start; value <= axisEnd + 0.001f; value += minorSpacing)
        {
            if (value < axisStart - 0.001f) continue;
            var color = IsMajorGridLine(value, majorSpacing) ? majorColor : minorColor;
            if (isVertical)
            {
                ds.DrawLine(value, crossStart, value, crossEnd, color, lineWidth);
            }
            else
            {
                ds.DrawLine(crossStart, value, crossEnd, value, color, lineWidth);
            }

            lineCount++;
            if (lineCount >= maxLines)
            {
                break;
            }
        }
    }

    private static bool IsMajorGridLine(float value, float majorSpacing)
    {
        if (majorSpacing <= 0.001f) return false;
        var nearest = MathF.Round(value / majorSpacing) * majorSpacing;
        var tolerance = MathF.Max(0.001f, majorSpacing * 0.001f);
        return MathF.Abs(value - nearest) <= tolerance;
    }

    internal void RenderBalloon(
        CanvasDrawingSession ds,
        Balloon balloon,
        bool isSelected,
        float layerOpacity,
        bool isEditing = false,
        string? editingText = null,
        int editingCursorPos = 0,
        int editingSelectionStart = 0,
        int editingSelectionLength = 0,
        IReadOnlyList<TextStyleSpan>? editingTextStyleSpans = null,
        bool drawHandles = true,
        bool skipStroke = false,
        bool cursorBlinkState = true,
        bool renderPanelMembershipBadge = true)
    {
        UpdateBalloonSize(ds, balloon, isEditing ? editingText : null, isEditing ? editingTextStyleSpans : null);

        var bounds = balloon.Bounds;
        var style = balloon.BalloonStyle;

        var combinedOpacity = ClampOpacity(style.Opacity) * ClampOpacity(layerOpacity);
        var fillColor = ApplyOpacity(style.FillColor.ToWindowsColor(), combinedOpacity);
        var strokeColor = ApplyOpacity(style.StrokeColor.ToWindowsColor(), combinedOpacity);

        var originalTransform = ds.Transform;
        var hasRotation = MathF.Abs(balloon.Rotation) > 0.01f;

        if (hasRotation)
        {
            var center = balloon.Position;
            var rotationRadians = balloon.Rotation * MathF.PI / 180f;
            var rotationMatrix = Matrix3x2.CreateRotation(rotationRadians, center.ToVector2());
            ds.Transform = rotationMatrix * originalTransform;
        }

        if (style.ShadowEnabled)
        {
            RenderBalloonShadow(ds, balloon, style, combinedOpacity);
        }

        if (style.GlowEnabled)
        {
            RenderBalloonGlow(ds, balloon, style, combinedOpacity);
        }

        var fillBrush = CreateFillBrush(ds, balloon, style, combinedOpacity);
        var textRenderSettings = ResolveBalloonTextRenderSettings(balloon);
        var renderBalloon = textRenderSettings.MirrorTailTargets
            ? CreateTailMirroredRenderBalloon(balloon)
            : balloon;

        var effectiveStrokeColor = isEditing ? WinColor.FromArgb(255, 0, 120, 215) : strokeColor;
        RenderBalloonWithTailsUnified(ds, renderBalloon, fillBrush, effectiveStrokeColor, style, combinedOpacity, skipStroke);

        var textStyle = balloon.TextStyle;
        var isOverflowing = false;

        if (isEditing && editingText != null)
        {
            isOverflowing = RenderBalloonTextWithCursor(
                ds,
                balloon,
                layerOpacity,
                editingText,
                editingCursorPos,
                editingSelectionStart,
                editingSelectionLength,
                editingTextStyleSpans,
                cursorBlinkState);
        }
        else
        {
            isOverflowing = RenderBalloonText(ds, balloon, layerOpacity, textRenderSettings, cursorBlinkState);
        }

        if (textStyle.OverflowMode == TextOverflowMode.Warn && isOverflowing)
        {
            RenderOverflowHighlight(ds, bounds);
        }

        if (ShouldHighlightUntranslated(balloon))
        {
            using var untranslatedStroke = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
            ds.DrawRectangle(bounds.ToWindowsRect(), WinColor.FromArgb(220, 255, 196, 0), 2f, untranslatedStroke);
        }

        if (hasRotation)
        {
            ds.Transform = originalTransform;
        }

        if (renderPanelMembershipBadge && balloon.PanelId.HasValue)
        {
            RenderPanelMembershipBadge(ds, bounds, balloon.PanelId.Value);
        }

        if (isSelected && !isEditing)
        {
            var selectionBounds = hasRotation ? BalloonGeometry.GetRotatedBounds(balloon) : bounds;
            RenderSelectionHighlight(ds, selectionBounds, drawHandles);

            if (drawHandles)
            {
                RenderRotationHandle(ds, balloon);
                if (balloon.TextPath != null)
                {
                    RenderTextPathGuide(ds, balloon.Position, balloon.Rotation, balloon.TextPath);
                }
            }

            if (drawHandles)
            {
                foreach (var tail in balloon.Tails)
                {
                    RenderTailHandle(ds, TailGeometry.GetRenderedTargetPoint(balloon, tail));
                    RenderTailAttachmentHandle(ds, TailGeometry.GetRenderedAttachmentPoint(balloon, tail));
                }
            }
        }
    }

    private void RenderBalloonShape(
        CanvasDrawingSession ds,
        BalloonShape shape,
        Rect bounds,
        WinColor fillColor,
        WinColor strokeColor,
        BalloonStyle style)
    {
        switch (shape)
        {
            case BalloonShape.Oval:
                ds.FillEllipse(
                    bounds.Center.ToVector2(),
                    bounds.Width / 2,
                    bounds.Height / 2,
                    fillColor);
                ds.DrawEllipse(
                    bounds.Center.ToVector2(),
                    bounds.Width / 2,
                    bounds.Height / 2,
                    strokeColor,
                    style.StrokeWidth);
                break;

            case BalloonShape.RoundedRect:
                ds.FillRoundedRectangle(
                    bounds.ToWindowsRect(),
                    style.CornerRadius,
                    style.CornerRadius,
                    fillColor);
                ds.DrawRoundedRectangle(
                    bounds.ToWindowsRect(),
                    style.CornerRadius,
                    style.CornerRadius,
                    strokeColor,
                    style.StrokeWidth);
                break;

            case BalloonShape.Radio:
                RenderRadioBalloon(ds, bounds, fillColor, strokeColor, style);
                break;

            case BalloonShape.Rectangle:
                ds.FillRectangle(bounds.ToWindowsRect(), fillColor);
                ds.DrawRectangle(bounds.ToWindowsRect(), strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.Custom:
                ds.FillRectangle(bounds.ToWindowsRect(), fillColor);
                ds.DrawRectangle(bounds.ToWindowsRect(), strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.Thought:
                RenderThoughtBalloon(ds, bounds, fillColor, strokeColor, style);
                break;

            case BalloonShape.Splat:
                RenderSplatBalloon(ds, bounds, fillColor, strokeColor, style);
                break;

            case BalloonShape.Burst:
                RenderBurstBalloon(ds, bounds, fillColor, strokeColor, style);
                break;

            case BalloonShape.Whisper:
                RenderWhisperBalloon(ds, bounds, fillColor, strokeColor, style);
                break;

            case BalloonShape.None:
                break;
        }
    }

    private void RenderThoughtBalloon(CanvasDrawingSession ds, Rect bounds, WinColor fillColor, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateThoughtGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderSplatBalloon(CanvasDrawingSession ds, Rect bounds, WinColor fillColor, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateSplatGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderRadioBalloon(CanvasDrawingSession ds, Rect bounds, WinColor fillColor, WinColor strokeColor, BalloonStyle style)
    {
        ds.FillRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, fillColor);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot };
        ds.DrawRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, strokeColor, style.StrokeWidth, strokeStyle);
    }

    private void RenderBurstBalloon(CanvasDrawingSession ds, Rect bounds, WinColor fillColor, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateBurstGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderWhisperBalloon(CanvasDrawingSession ds, Rect bounds, WinColor fillColor, WinColor strokeColor, BalloonStyle style)
    {
        ds.FillEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, fillColor);

        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        ds.DrawEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, strokeColor, style.StrokeWidth, strokeStyle);
    }

    private void RenderTail(CanvasDrawingSession ds, Balloon balloon, Tail tail, WinColor strokeColor, WinColor fillColor, float strokeWidth)
    {
        var bounds = balloon.Bounds;

        var attachPoint = TailGeometry.ComputeAttachmentPoint(balloon, tail);

        switch (tail.Style)
        {
            case TailStyle.Pointer:
                RenderPointerTail(ds, attachPoint, tail.TargetPoint, tail.BaseWidth, fillColor, strokeColor, strokeWidth);
                break;

            case TailStyle.ThoughtBubbles:
                RenderThoughtBubbleTail(ds, attachPoint, tail.TargetPoint, fillColor, strokeColor, strokeWidth);
                break;

            case TailStyle.Curved:
                RenderCurvedTail(ds, attachPoint, tail.TargetPoint, tail.BaseWidth, fillColor, strokeColor, strokeWidth);
                break;

            case TailStyle.Squiggly:
                RenderSquigglyTail(ds, attachPoint, tail.TargetPoint, fillColor, strokeColor, strokeWidth);
                break;

            case TailStyle.None:
                break;
        }
    }

    private void RenderPointerTail(
        CanvasDrawingSession ds,
        Point2 attachPoint,
        Point2 targetPoint,
        float baseWidth,
        WinColor fillColor,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);

        var baseLeft = attachPoint + perpendicular * (baseWidth / 2);
        var baseRight = attachPoint - perpendicular * (baseWidth / 2);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(baseLeft.ToVector2());
        pathBuilder.AddLine(targetPoint.ToVector2());
        pathBuilder.AddLine(baseRight.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, strokeColor, strokeWidth);
    }

    private void RenderCurvedTail(
        CanvasDrawingSession ds,
        Point2 attachPoint,
        Point2 targetPoint,
        float baseWidth,
        WinColor fillColor,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);
        var distance = (targetPoint - attachPoint).Length;

        var baseLeft = attachPoint + perpendicular * (baseWidth / 2);
        var baseRight = attachPoint - perpendicular * (baseWidth / 2);

        var controlDistance = distance * 0.4f;
        var curveOffset = baseWidth * 0.6f;

        var controlLeft1 = baseLeft + direction * controlDistance + perpendicular * curveOffset;
        var controlLeft2 = targetPoint - direction * controlDistance * 0.5f + perpendicular * (curveOffset * 0.3f);

        var controlRight1 = baseRight + direction * controlDistance - perpendicular * curveOffset;
        var controlRight2 = targetPoint - direction * controlDistance * 0.5f - perpendicular * (curveOffset * 0.3f);

        using var pathBuilder = new CanvasPathBuilder(ds);

        pathBuilder.BeginFigure(baseLeft.ToVector2());

        pathBuilder.AddCubicBezier(
            controlLeft1.ToVector2(),
            controlLeft2.ToVector2(),
            targetPoint.ToVector2());

        pathBuilder.AddCubicBezier(
            controlRight2.ToVector2(),
            controlRight1.ToVector2(),
            baseRight.ToVector2());

        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, strokeColor, strokeWidth);
    }

    private void RenderThoughtBubbleTail(
        CanvasDrawingSession ds,
        Point2 attachPoint,
        Point2 targetPoint,
        WinColor fillColor,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = targetPoint - attachPoint;
        var distance = direction.Length;
        var normalized = direction.Normalized();

        int bubbleCount = 3;
        for (int i = 0; i < bubbleCount; i++)
        {
            float t = (i + 1f) / (bubbleCount + 1f);
            var pos = attachPoint + normalized * (distance * t);
            float radius = 6f * (1f - t * 0.5f); // Decreasing size

            ds.FillCircle(pos.ToVector2(), radius, fillColor);
            ds.DrawCircle(pos.ToVector2(), radius, strokeColor, strokeWidth);
        }
    }

    private void RenderSquigglyTail(
        CanvasDrawingSession ds,
        Point2 attachPoint,
        Point2 targetPoint,
        WinColor fillColor,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);
        var distance = (targetPoint - attachPoint).Length;

        var waveAmplitude = 6f; // How far the wave extends perpendicular to path
        var waveFrequency = distance / 20f; // Number of waves along the path
        var segmentsPerWave = 8; // How smooth each wave is
        var totalSegments = (int)(waveFrequency * segmentsPerWave);

        if (totalSegments < 4) totalSegments = 4; // Minimum segments

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(attachPoint.ToVector2());

        for (int i = 1; i <= totalSegments; i++)
        {
            float t = (float)i / totalSegments;
            var basePoint = attachPoint + direction * (distance * t);

            var waveOffset = MathF.Sin(t * waveFrequency * MathF.PI * 2) * waveAmplitude;

            var taperFactor = 1f - (t * 0.7f);
            waveOffset *= taperFactor;

            var finalPoint = basePoint + perpendicular * waveOffset;
            pathBuilder.AddLine(finalPoint.ToVector2());
        }

        pathBuilder.EndFigure(CanvasFigureLoop.Open);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.DrawGeometry(geometry, strokeColor, strokeWidth);
    }

    private void RenderBalloonWithTailsUnified(
        CanvasDrawingSession ds,
        Balloon balloon,
        ICanvasBrush fillBrush,
        WinColor strokeColor,
        BalloonStyle style,
        float combinedOpacity,
        bool skipStroke = false)
    {
        if (balloon.Shape == BalloonShape.None) return;

        var bounds = balloon.Bounds;

        var balloonGeometry = CreateBalloonGeometry(ds, balloon, bounds, style);

        var mergeTails = balloon.Tails.Where(t => t.Style == TailStyle.Pointer || t.Style == TailStyle.Curved || t.Style == TailStyle.ThoughtBubbles).ToList();
        var separateTails = balloon.Tails.Where(t => t.Style == TailStyle.Squiggly).ToList();

        CanvasStrokeStyle? strokeStyle = balloon.Shape switch
        {
            BalloonShape.Whisper => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash },
            BalloonShape.Radio => new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot },
            _ => null
        };

        if (mergeTails.Count == 0)
        {
            FillBalloonGeometry(ds, balloonGeometry, bounds, fillBrush, style, combinedOpacity);
            if (!skipStroke)
            {
                if (strokeStyle != null)
                {
                    ds.DrawGeometry(balloonGeometry, strokeColor, style.StrokeWidth, strokeStyle);
                }
                else
                {
                    ds.DrawGeometry(balloonGeometry, strokeColor, style.StrokeWidth);
                }
            }
        }
        else
        {
            var combinedGeometry = balloonGeometry;

            foreach (var tail in mergeTails)
            {
                var tailGeometry = CreateTailGeometry(ds, balloon, tail);
                if (tailGeometry != null)
                {
                    var newCombined = combinedGeometry.CombineWith(tailGeometry, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                    if (combinedGeometry != balloonGeometry)
                    {
                        combinedGeometry.Dispose();
                    }
                    combinedGeometry = newCombined;
                    tailGeometry.Dispose();
                }
            }

            var combinedBounds = GetGeometryBounds(combinedGeometry, bounds);
            FillBalloonGeometry(ds, combinedGeometry, combinedBounds, fillBrush, style, combinedOpacity);
            if (!skipStroke)
            {
                if (strokeStyle != null)
                {
                    ds.DrawGeometry(combinedGeometry, strokeColor, style.StrokeWidth, strokeStyle);
                }
                else
                {
                    ds.DrawGeometry(combinedGeometry, strokeColor, style.StrokeWidth);
                }
            }

            if (combinedGeometry != balloonGeometry)
            {
                combinedGeometry.Dispose();
            }
        }

        balloonGeometry.Dispose();
        strokeStyle?.Dispose();

        foreach (var tail in separateTails)
        {
            var attachPoint = TailGeometry.ComputeAttachmentPoint(balloon, tail);
            switch (tail.Style)
            {
                case TailStyle.Squiggly:
                    RenderSquigglyTail(ds, attachPoint, tail.TargetPoint, WinColor.FromArgb(255, 255, 255, 255), strokeColor, style.StrokeWidth);
                    break;
            }
        }
    }

    private void FillBalloonGeometry(
        CanvasDrawingSession ds,
        CanvasGeometry geometry,
        Rect bounds,
        ICanvasBrush fillBrush,
        BalloonStyle style,
        float opacity)
    {
        if (!style.PatternEnabled)
        {
            ds.FillGeometry(geometry, fillBrush);
            return;
        }

        if (RenderImageFill(ds, geometry, bounds, style.PatternImagePath, opacity))
        {
            return;
        }

        var primary = ApplyOpacity(style.FillColor.ToWindowsColor(), opacity);
        var secondary = ApplyOpacity(style.PatternSecondaryColor.ToWindowsColor(), opacity);
        using var layer = ds.CreateLayer(1f, geometry);
        RenderPatternFill(ds, bounds, primary, secondary, style.PatternType, style.PatternScale, style.PatternAngle);
    }

    private static Rect GetGeometryBounds(CanvasGeometry geometry, Rect fallback)
    {
        var bounds = geometry.ComputeBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return fallback;
        }

        return new Rect((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
    }

    private CanvasGeometry CreateBalloonGeometry(CanvasDrawingSession ds, Balloon balloon, Rect bounds, BalloonStyle style)
    {
        switch (balloon.Shape)
        {
            case BalloonShape.Oval:
            case BalloonShape.Whisper:
                return CanvasGeometry.CreateEllipse(ds, bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2);
            case BalloonShape.Thought:
                return CreateThoughtGeometry(ds, bounds, style.ThoughtSmoothness);

            case BalloonShape.Splat:
                return CreateSplatGeometry(ds, bounds, style.ThoughtSmoothness);

            case BalloonShape.Burst:
                return CreateBurstGeometry(ds, bounds, style.ThoughtSmoothness);

            case BalloonShape.RoundedRect:
            case BalloonShape.Radio:
                return CanvasGeometry.CreateRoundedRectangle(ds, bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius);

            case BalloonShape.Custom:
                var customGeometry = TryCreateCustomGeometry(ds, balloon, bounds);
                if (customGeometry != null)
                {
                    return customGeometry;
                }
                return CanvasGeometry.CreateRectangle(ds, bounds.ToWindowsRect());

            case BalloonShape.Rectangle:
            default:
                return CanvasGeometry.CreateRectangle(ds, bounds.ToWindowsRect());
        }
    }

    private CanvasGeometry CreateBurstGeometry(CanvasDrawingSession ds, Rect bounds, float smoothness)
    {
        var center = bounds.Center;
        var outerRadiusX = bounds.Width / 2f;
        var outerRadiusY = bounds.Height / 2f;
        var innerRadiusX = outerRadiusX * 0.6f;
        var innerRadiusY = outerRadiusY * 0.6f;

        var averageRadius = (outerRadiusX + outerRadiusY) * 0.5f;
        var clampedSmoothness = Math.Clamp(smoothness, 0f, 1f);
        var baseSpikeCount = Math.Clamp((int)(averageRadius / 6f), 10, 24);
        var smoothnessScale = 1.35f - (clampedSmoothness * 0.7f);
        var spikeCount = Math.Clamp((int)MathF.Round(baseSpikeCount * smoothnessScale), 8, 36);
        var totalPoints = spikeCount * 2;
        var angleStep = (MathF.PI * 2f) / totalPoints;

        using var pathBuilder = new CanvasPathBuilder(ds);
        for (int i = 0; i < totalPoints; i++)
        {
            var angle = angleStep * i;
            var useOuter = i % 2 == 0;
            var radiusX = useOuter ? outerRadiusX : innerRadiusX;
            var radiusY = useOuter ? outerRadiusY : innerRadiusY;
            var point = new Vector2(
                center.X + MathF.Cos(angle) * radiusX,
                center.Y + MathF.Sin(angle) * radiusY);

            if (i == 0)
            {
                pathBuilder.BeginFigure(point);
            }
            else
            {
                pathBuilder.AddLine(point);
            }
        }

        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(pathBuilder);
    }

    private CanvasGeometry CreateThoughtGeometry(CanvasDrawingSession ds, Rect bounds, float smoothness)
    {
        var bubbleSize = Math.Clamp(smoothness, 0f, 1f);
        var roughness = 1f - bubbleSize;

        var center = bounds.Center.ToVector2();
        var axisX = Math.Max(1f, bounds.Width * 0.5f);
        var axisY = Math.Max(1f, bounds.Height * 0.5f);
        var minAxis = Math.Max(1f, Math.Min(axisX, axisY));
        var averageAxis = (axisX + axisY) * 0.5f;

        var coreScale = 0.74f + (bubbleSize * 0.08f);
        var coreRadiusX = Math.Max(1f, axisX * coreScale);
        var coreRadiusY = Math.Max(1f, axisY * coreScale);

        var baseRadius = Math.Clamp(minAxis * (0.19f + (bubbleSize * 0.23f)), minAxis * 0.13f, minAxis * 0.44f);
        var perimeter = MathF.PI * (3f * (coreRadiusX + coreRadiusY) - MathF.Sqrt((3f * coreRadiusX + coreRadiusY) * (coreRadiusX + 3f * coreRadiusY)));
        var targetSpacing = baseRadius * (1.28f + (roughness * 0.10f));
        var lobeCount = Math.Clamp((int)MathF.Round(perimeter / Math.Max(6f, targetSpacing)), 10, 24);

        var angleJitterRange = 0.015f + (roughness * 0.03f);
        var radiusJitterRange = 0.045f + (roughness * 0.06f);

        var centerInset = 0.30f + (roughness * 0.10f);
        var combined = CanvasGeometry.CreateEllipse(ds, center, coreRadiusX, coreRadiusY);
        var fullCircle = MathF.PI * 2f;

        for (var i = 0; i < lobeCount; i++)
        {
            var baseT = fullCircle * i / lobeCount;
            var jitterT = LerpValue(-angleJitterRange, angleJitterRange, StableNoise01(i, bubbleSize, averageAxis));
            var t = baseT + jitterT;

            var jitterR = LerpValue(-radiusJitterRange, radiusJitterRange, StableNoise01(i + 101, averageAxis, bubbleSize));
            var radius = Math.Clamp(baseRadius * (1f + jitterR), baseRadius * 0.7f, baseRadius * 1.35f);

            var ringX = Math.Max(1f, coreRadiusX - (radius * centerInset));
            var ringY = Math.Max(1f, coreRadiusY - (radius * centerInset));
            var lobeCenter = new Vector2(
                center.X + (MathF.Cos(t) * ringX),
                center.Y + (MathF.Sin(t) * ringY));

            using var lobe = CanvasGeometry.CreateCircle(ds, lobeCenter, radius);
            var merged = combined.CombineWith(lobe, Matrix3x2.Identity, CanvasGeometryCombine.Union);
            combined.Dispose();
            combined = merged;
        }

        return combined;
    }

    private static float StableNoise01(int index, float seedA, float seedB)
    {
        var x = MathF.Sin((index * 12.9898f) + (seedA * 78.233f) + (seedB * 37.719f)) * 43758.5453f;
        return x - MathF.Floor(x);
    }

    private static float LerpValue(float a, float b, float t)
    {
        return a + ((b - a) * t);
    }

    private CanvasGeometry CreateSplatGeometry(CanvasDrawingSession ds, Rect bounds, float smoothness)
    {
        var clampedSmoothness = Math.Clamp(smoothness, 0f, 1f);
        var bumpCount = Math.Clamp((int)MathF.Round(18f - (clampedSmoothness * 10f)), 8, 22);
        var segments = Math.Clamp(bumpCount * 8, 40, 220);
        var amplitude = 0.28f + (0.12f - 0.28f) * clampedSmoothness;
        var tension = 0.65f + (0.55f * clampedSmoothness);

        var center = bounds.Center.ToVector2();
        var maxRadiusX = Math.Max(1f, bounds.Width * 0.5f);
        var maxRadiusY = Math.Max(1f, bounds.Height * 0.5f);
        var baseRadiusX = maxRadiusX / (1f + amplitude);
        var baseRadiusY = maxRadiusY / (1f + amplitude);

        var points = new List<Vector2>(segments);
        var fullCircle = MathF.PI * 2f;
        for (var i = 0; i < segments; i++)
        {
            var angle = fullCircle * i / segments;
            var primaryWave = (MathF.Sin(angle * bumpCount) + 1f) * 0.5f;
            var secondaryWave = (MathF.Sin((angle * bumpCount * 0.5f) + 1.7f) + 1f) * 0.5f;
            var wave = (primaryWave * 0.75f) + (secondaryWave * 0.25f);
            var radialScale = 1f + (amplitude * wave);

            points.Add(new Vector2(
                center.X + (MathF.Cos(angle) * baseRadiusX * radialScale),
                center.Y + (MathF.Sin(angle) * baseRadiusY * radialScale)));
        }

        return CreateSmoothClosedGeometry(ds, points, tension);
    }

    private static CanvasGeometry CreateSmoothClosedGeometry(CanvasDrawingSession ds, IReadOnlyList<Vector2> points, float tension)
    {
        if (points.Count < 3)
        {
            throw new ArgumentException("At least three points are required to create a closed geometry.", nameof(points));
        }

        var clampedTension = Math.Clamp(tension, 0.1f, 2f);
        var handleScale = clampedTension / 6f;
        var count = points.Count;

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(points[0]);
        for (var i = 0; i < count; i++)
        {
            var p0 = points[(i - 1 + count) % count];
            var p1 = points[i];
            var p2 = points[(i + 1) % count];
            var p3 = points[(i + 2) % count];

            var c1 = p1 + ((p2 - p0) * handleScale);
            var c2 = p2 - ((p3 - p1) * handleScale);
            pathBuilder.AddCubicBezier(c1, c2, p2);
        }

        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(pathBuilder);
    }

    private CanvasGeometry? TryCreateCustomGeometry(CanvasDrawingSession ds, Balloon balloon, Rect bounds)
    {
        var pathData = balloon.CustomShapePathData;
        if (string.IsNullOrWhiteSpace(pathData)) return null;

        var geometry = SvgPathParser.TryCreateGeometry(ds, pathData);
        if (geometry == null) return null;

        var pathBounds = geometry.ComputeBounds();
        if (pathBounds.Width <= 0 || pathBounds.Height <= 0)
        {
            geometry.Dispose();
            return null;
        }

        var scaleX = bounds.Width / (float)pathBounds.Width;
        var scaleY = bounds.Height / (float)pathBounds.Height;
        var translate = new Vector2(
            bounds.X - (float)pathBounds.X * scaleX,
            bounds.Y - (float)pathBounds.Y * scaleY);

        var transform = Matrix3x2.CreateScale(scaleX, scaleY) * Matrix3x2.CreateTranslation(translate);
        var transformed = geometry.Transform(transform);
        geometry.Dispose();
        return transformed;
    }

    private CanvasGeometry? CreateTailGeometry(CanvasDrawingSession ds, Balloon balloon, Tail tail)
    {
        var attachPoint = TailGeometry.ComputeAttachmentPoint(balloon, tail);

        switch (tail.Style)
        {
            case TailStyle.Pointer:
                return CreatePointerTailGeometry(ds, attachPoint, tail.TargetPoint, tail.BaseWidth);

            case TailStyle.Curved:
                return CreateCurvedTailGeometry(ds, attachPoint, tail.TargetPoint, tail.BaseWidth, tail.Curvature, tail.CurveCenter, tail.ControlPoint);

            case TailStyle.ThoughtBubbles:
                return CreateThoughtBubbleTailGeometry(ds, attachPoint, tail.TargetPoint);

            default:
                return null;
        }
    }

    private CanvasGeometry CreatePointerTailGeometry(CanvasDrawingSession ds, Point2 attachPoint, Point2 targetPoint, float baseWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);

        var inwardOffset = -direction * 8f;
        var extendedBase = attachPoint + inwardOffset;

        var baseLeft = extendedBase + perpendicular * (baseWidth / 2);
        var baseRight = extendedBase - perpendicular * (baseWidth / 2);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(baseLeft.ToVector2());
        pathBuilder.AddLine(targetPoint.ToVector2());
        pathBuilder.AddLine(baseRight.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        return CanvasGeometry.CreatePath(pathBuilder);
    }

    private CanvasGeometry CreateCurvedTailGeometry(CanvasDrawingSession ds, Point2 attachPoint, Point2 targetPoint, float baseWidth, float curvature, float curveCenter, Point2? controlPoint)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);

        var inwardOffset = -direction * 8f;
        var extendedBase = attachPoint + inwardOffset;

        var baseLeft = extendedBase + perpendicular * (baseWidth / 2);
        var baseRight = extendedBase - perpendicular * (baseWidth / 2);

        var midControl = GetTailCurveMidControl(attachPoint, targetPoint, direction, perpendicular, curvature, curveCenter, controlPoint);

        var controlLeft1 = attachPoint + (midControl - attachPoint) * 0.5f + perpendicular * (baseWidth * 0.4f);
        var controlLeft2 = midControl + (targetPoint - midControl) * 0.5f + perpendicular * (baseWidth * 0.15f);
        var controlRight1 = attachPoint + (midControl - attachPoint) * 0.5f - perpendicular * (baseWidth * 0.4f);
        var controlRight2 = midControl + (targetPoint - midControl) * 0.5f - perpendicular * (baseWidth * 0.15f);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(baseLeft.ToVector2());
        pathBuilder.AddCubicBezier(controlLeft1.ToVector2(), controlLeft2.ToVector2(), targetPoint.ToVector2());
        pathBuilder.AddCubicBezier(controlRight2.ToVector2(), controlRight1.ToVector2(), baseRight.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        return CanvasGeometry.CreatePath(pathBuilder);
    }

    private CanvasGeometry? CreateThoughtBubbleTailGeometry(CanvasDrawingSession ds, Point2 attachPoint, Point2 targetPoint)
    {
        var direction = targetPoint - attachPoint;
        var distance = direction.Length;
        if (distance <= 0.0001f)
        {
            return null;
        }

        var normalized = direction / distance;
        CanvasGeometry? combined = null;
        const int bubbleCount = 3;
        for (int i = 0; i < bubbleCount; i++)
        {
            var t = (i + 1f) / (bubbleCount + 1f);
            var pos = attachPoint + normalized * (distance * t);
            var radius = 6f * (1f - t * 0.5f);
            using var bubble = CanvasGeometry.CreateCircle(ds, pos.ToVector2(), radius);
            if (combined == null)
            {
                combined = bubble.Transform(Matrix3x2.Identity);
            }
            else
            {
                var union = combined.CombineWith(bubble, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                combined.Dispose();
                combined = union;
            }
        }

        return combined;
    }

    private void RenderTailSeamless(
        CanvasDrawingSession ds,
        Balloon balloon,
        Tail tail,
        WinColor strokeColor,
        ICanvasBrush fillBrush,
        float strokeWidth)
    {
        var attachPoint = TailGeometry.ComputeAttachmentPoint(balloon, tail);

        switch (tail.Style)
        {
            case TailStyle.Pointer:
                RenderPointerTailSeamless(ds, balloon, attachPoint, tail.TargetPoint, tail.BaseWidth, fillBrush, strokeColor, strokeWidth);
                break;

            case TailStyle.Curved:
                RenderCurvedTailSeamless(ds, balloon, attachPoint, tail.TargetPoint, tail.BaseWidth, tail.Curvature, tail.CurveCenter, tail.ControlPoint, fillBrush, strokeColor, strokeWidth);
                break;

            case TailStyle.ThoughtBubbles:
                RenderThoughtBubbleTailWithBrush(ds, attachPoint, tail.TargetPoint, fillBrush, strokeColor, strokeWidth);
                break;

            case TailStyle.Squiggly:
                RenderSquigglyTail(ds, attachPoint, tail.TargetPoint, WinColor.FromArgb(255, 255, 255, 255), strokeColor, strokeWidth);
                break;

            case TailStyle.None:
                break;
        }
    }

    private void RenderPointerTailSeamless(
        CanvasDrawingSession ds,
        Balloon balloon,
        Point2 attachPoint,
        Point2 targetPoint,
        float baseWidth,
        ICanvasBrush fillBrush,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);

        var baseLeft = attachPoint + perpendicular * (baseWidth / 2);
        var baseRight = attachPoint - perpendicular * (baseWidth / 2);

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(baseLeft.ToVector2());
        pathBuilder.AddLine(targetPoint.ToVector2());
        pathBuilder.AddLine(baseRight.ToVector2());
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.FillGeometry(geometry, fillBrush);

        ds.DrawLine(baseLeft.ToVector2(), targetPoint.ToVector2(), strokeColor, strokeWidth);
        ds.DrawLine(targetPoint.ToVector2(), baseRight.ToVector2(), strokeColor, strokeWidth);
    }

    private void RenderCurvedTailSeamless(
        CanvasDrawingSession ds,
        Balloon balloon,
        Point2 attachPoint,
        Point2 targetPoint,
        float baseWidth,
        float curvature,
        float curveCenter,
        Point2? controlPoint,
        ICanvasBrush fillBrush,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = (targetPoint - attachPoint).Normalized();
        var perpendicular = new Point2(-direction.Y, direction.X);
        var baseLeft = attachPoint + perpendicular * (baseWidth / 2);
        var baseRight = attachPoint - perpendicular * (baseWidth / 2);

        var midControl = GetTailCurveMidControl(attachPoint, targetPoint, direction, perpendicular, curvature, curveCenter, controlPoint);

        var controlLeft1 = attachPoint + (midControl - attachPoint) * 0.5f + perpendicular * (baseWidth * 0.4f);
        var controlLeft2 = midControl + (targetPoint - midControl) * 0.5f + perpendicular * (baseWidth * 0.15f);

        var controlRight1 = attachPoint + (midControl - attachPoint) * 0.5f - perpendicular * (baseWidth * 0.4f);
        var controlRight2 = midControl + (targetPoint - midControl) * 0.5f - perpendicular * (baseWidth * 0.15f);

        using var fillPathBuilder = new CanvasPathBuilder(ds);
        fillPathBuilder.BeginFigure(baseLeft.ToVector2());
        fillPathBuilder.AddCubicBezier(controlLeft1.ToVector2(), controlLeft2.ToVector2(), targetPoint.ToVector2());
        fillPathBuilder.AddCubicBezier(controlRight2.ToVector2(), controlRight1.ToVector2(), baseRight.ToVector2());
        fillPathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var fillGeometry = CanvasGeometry.CreatePath(fillPathBuilder);
        ds.FillGeometry(fillGeometry, fillBrush);

        using var leftPathBuilder = new CanvasPathBuilder(ds);
        leftPathBuilder.BeginFigure(baseLeft.ToVector2());
        leftPathBuilder.AddCubicBezier(controlLeft1.ToVector2(), controlLeft2.ToVector2(), targetPoint.ToVector2());
        leftPathBuilder.EndFigure(CanvasFigureLoop.Open);
        using var leftGeometry = CanvasGeometry.CreatePath(leftPathBuilder);
        ds.DrawGeometry(leftGeometry, strokeColor, strokeWidth);

        using var rightPathBuilder = new CanvasPathBuilder(ds);
        rightPathBuilder.BeginFigure(targetPoint.ToVector2());
        rightPathBuilder.AddCubicBezier(controlRight2.ToVector2(), controlRight1.ToVector2(), baseRight.ToVector2());
        rightPathBuilder.EndFigure(CanvasFigureLoop.Open);
        using var rightGeometry = CanvasGeometry.CreatePath(rightPathBuilder);
        ds.DrawGeometry(rightGeometry, strokeColor, strokeWidth);
    }

    private static Point2 GetTailCurveMidControl(
        Point2 attachPoint,
        Point2 targetPoint,
        Point2 direction,
        Point2 perpendicular,
        float curvature,
        float curveCenter,
        Point2? controlPoint)
    {
        if (controlPoint.HasValue)
        {
            return controlPoint.Value;
        }

        var distance = (targetPoint - attachPoint).Length;
        var clampedCenter = Math.Clamp(curveCenter, 0f, 1f);
        var midPoint = attachPoint + direction * (distance * clampedCenter);
        var curveOffset = distance * 0.5f * Math.Clamp(curvature, -2f, 2f);
        return midPoint + perpendicular * curveOffset;
    }

    private void RenderThoughtBubbleTailWithBrush(
        CanvasDrawingSession ds,
        Point2 attachPoint,
        Point2 targetPoint,
        ICanvasBrush fillBrush,
        WinColor strokeColor,
        float strokeWidth)
    {
        var direction = targetPoint - attachPoint;
        var distance = direction.Length;
        var normalized = direction.Normalized();

        int bubbleCount = 3;
        for (int i = 0; i < bubbleCount; i++)
        {
            float t = (i + 1f) / (bubbleCount + 1f);
            var pos = attachPoint + normalized * (distance * t);
            float radius = 6f * (1f - t * 0.5f);

            ds.FillCircle(pos.ToVector2(), radius, fillBrush);
            ds.DrawCircle(pos.ToVector2(), radius, strokeColor, strokeWidth);
        }
    }

    private ICanvasBrush CreateFillBrush(CanvasDrawingSession ds, Balloon balloon, BalloonStyle style, float opacity)
    {
        if (style.PatternEnabled)
        {
            var fillColor = ApplyOpacity(style.FillColor.ToWindowsColor(), opacity);
            return new CanvasSolidColorBrush(ds, fillColor);
        }

        if (style.GradientEnabled)
        {
            var startColor = ApplyOpacity(style.GradientStartColor.ToWindowsColor(), opacity);
            var endColor = ApplyOpacity(style.GradientEndColor.ToWindowsColor(), opacity);
            return style.GradientType == BalloonGradientType.Radial
                ? CreateRadialBalloonFillBrush(ds, balloon.Bounds, startColor, endColor)
                : CreateLinearBalloonFillBrush(ds, balloon.Bounds, startColor, endColor, style.GradientAngle);
        }
        else
        {
            var fillColor = ApplyOpacity(style.FillColor.ToWindowsColor(), opacity);
            return new CanvasSolidColorBrush(ds, fillColor);
        }
    }

    private static CanvasLinearGradientBrush CreateLinearBalloonFillBrush(
        CanvasDrawingSession ds,
        Rect bounds,
        WinColor startColor,
        WinColor endColor,
        float angleDegrees)
    {
        var angle = angleDegrees * MathF.PI / 180f;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        if (direction.LengthSquared() < 0.0001f)
        {
            direction = new Vector2(0f, 1f);
        }

        var center = bounds.Center.ToVector2();
        var halfLength = MathF.Max(bounds.Width, bounds.Height) * 0.75f;
        var start = center - direction * halfLength;
        var end = center + direction * halfLength;

        return new CanvasLinearGradientBrush(ds, new CanvasGradientStop[]
        {
            new() { Position = 0f, Color = startColor },
            new() { Position = 1f, Color = endColor }
        })
        {
            StartPoint = start,
            EndPoint = end
        };
    }

    private static CanvasRadialGradientBrush CreateRadialBalloonFillBrush(
        CanvasDrawingSession ds,
        Rect bounds,
        WinColor startColor,
        WinColor endColor)
    {
        return new CanvasRadialGradientBrush(ds, new CanvasGradientStop[]
        {
            new() { Position = 0f, Color = startColor },
            new() { Position = 1f, Color = endColor }
        })
        {
            Center = bounds.Center.ToVector2(),
            OriginOffset = Vector2.Zero,
            RadiusX = MathF.Max(bounds.Width * 0.6f, 1f),
            RadiusY = MathF.Max(bounds.Height * 0.6f, 1f)
        };
    }

    private void RenderBalloonShadow(CanvasDrawingSession ds, Balloon balloon, BalloonStyle style, float baseOpacity)
    {
        var shadowOpacity = Math.Clamp(style.ShadowOpacity, 0f, 1f) * baseOpacity;
        if (shadowOpacity <= 0.001f)
        {
            return;
        }

        var shadowColor = style.ShadowColor.ToWindowsColor();
        var falloff = MathF.Max(0f, style.ShadowFalloff);
        var layerCount = falloff > 0.01f ? Math.Clamp((int)MathF.Ceiling(falloff * 2.75f), 6, 28) : 1;
        var weights = BuildSoftLayerWeights(layerCount, centerWeighted: true);

        var bounds = balloon.Bounds;
        var shadowBounds = new Rect(
            bounds.X + style.ShadowOffsetX,
            bounds.Y + style.ShadowOffsetY,
            bounds.Width,
            bounds.Height);

        for (int i = layerCount - 1; i >= 0; i--)
        {
            var expansion = layerCount > 1 ? falloff * (i / (float)(layerCount - 1)) : 0f;
            var layerOpacity = shadowOpacity * weights[i];
            if (layerOpacity <= 0.0005f) continue;

            var color = ApplyOpacity(shadowColor, layerOpacity);
            DrawExpandedBalloonBody(ds, balloon, style, shadowBounds, expansion, color);
            DrawExpandedPointerTails(ds, balloon, style.ShadowOffsetX, style.ShadowOffsetY, expansion, color);
        }
    }

    private void RenderBalloonGlow(CanvasDrawingSession ds, Balloon balloon, BalloonStyle style, float baseOpacity)
    {
        var glowSize = MathF.Max(0f, style.GlowSize);
        var glowOpacity = Math.Clamp(style.GlowOpacity, 0f, 1f) * baseOpacity;
        if (glowSize <= 0.01f || glowOpacity <= 0.001f)
        {
            return;
        }

        var glowColor = style.GlowColor.ToWindowsColor();
        var layerCount = Math.Clamp((int)MathF.Ceiling(glowSize * 3f), 6, 32);
        var weights = BuildSoftLayerWeights(layerCount, centerWeighted: false);
        var bounds = balloon.Bounds;

        for (int i = layerCount - 1; i >= 0; i--)
        {
            var expansion = glowSize * ((i + 1f) / layerCount);
            var layerOpacity = glowOpacity * weights[i];
            if (layerOpacity <= 0.0005f) continue;

            var color = ApplyOpacity(glowColor, layerOpacity);
            DrawExpandedBalloonBody(ds, balloon, style, bounds, expansion, color);
            DrawExpandedPointerTails(ds, balloon, 0f, 0f, expansion, color);
        }
    }

    private void DrawExpandedBalloonBody(CanvasDrawingSession ds, Balloon balloon, BalloonStyle style, Rect bounds, float expansion, WinColor color)
    {
        var expandedBounds = bounds.Inflate(expansion, expansion);
        switch (balloon.Shape)
        {
            case BalloonShape.Oval:
            case BalloonShape.Whisper:
                ds.FillEllipse(expandedBounds.Center.ToVector2(), expandedBounds.Width / 2, expandedBounds.Height / 2, color);
                break;

            case BalloonShape.Thought:
                using (var geometry = CreateThoughtGeometry(ds, expandedBounds, style.ThoughtSmoothness))
                {
                    ds.FillGeometry(geometry, color);
                }
                break;

            case BalloonShape.Splat:
                using (var geometry = CreateSplatGeometry(ds, expandedBounds, style.ThoughtSmoothness))
                {
                    ds.FillGeometry(geometry, color);
                }
                break;

            case BalloonShape.Burst:
                using (var geometry = CreateBurstGeometry(ds, expandedBounds, style.ThoughtSmoothness))
                {
                    ds.FillGeometry(geometry, color);
                }
                break;

            case BalloonShape.RoundedRect:
            case BalloonShape.Radio:
                ds.FillRoundedRectangle(
                    expandedBounds.ToWindowsRect(),
                    MathF.Max(0f, style.CornerRadius + expansion),
                    MathF.Max(0f, style.CornerRadius + expansion),
                    color);
                break;

            case BalloonShape.Custom:
                using (var geometry = CreateBalloonGeometry(ds, balloon, expandedBounds, style))
                {
                    ds.FillGeometry(geometry, color);
                }
                break;

            case BalloonShape.Rectangle:
                if (expansion > 0.01f)
                {
                    ds.FillRoundedRectangle(expandedBounds.ToWindowsRect(), expansion, expansion, color);
                }
                else
                {
                    ds.FillRectangle(expandedBounds.ToWindowsRect(), color);
                }
                break;
        }
    }

    private static void DrawExpandedPointerTails(CanvasDrawingSession ds, Balloon balloon, float offsetX, float offsetY, float expansion, WinColor color)
    {
        foreach (var tail in balloon.Tails)
        {
            if (tail.Style is not (TailStyle.Pointer or TailStyle.Curved))
            {
                continue;
            }

            var attachPoint = TailGeometry.ComputeAttachmentPoint(balloon, tail);
            var attach = new Point2(attachPoint.X + offsetX, attachPoint.Y + offsetY);
            var target = new Point2(tail.TargetPoint.X + offsetX, tail.TargetPoint.Y + offsetY);

            var direction = target - attach;
            if (direction.LengthSquared <= 0.0001f)
            {
                continue;
            }

            direction = direction.Normalized();
            var perpendicular = new Point2(-direction.Y, direction.X);
            var halfWidth = MathF.Max(0.1f, (tail.BaseWidth * 0.5f) + expansion);
            var tip = target + (direction * (expansion * 0.6f));
            var baseLeft = attach + (perpendicular * halfWidth);
            var baseRight = attach - (perpendicular * halfWidth);

            using var pathBuilder = new CanvasPathBuilder(ds);
            pathBuilder.BeginFigure(baseLeft.ToVector2());
            pathBuilder.AddLine(tip.ToVector2());
            pathBuilder.AddLine(baseRight.ToVector2());
            pathBuilder.EndFigure(CanvasFigureLoop.Closed);
            using var geometry = CanvasGeometry.CreatePath(pathBuilder);
            ds.FillGeometry(geometry, color);
        }
    }

    private static float[] BuildSoftLayerWeights(int layerCount, bool centerWeighted)
    {
        var count = Math.Max(1, layerCount);
        var weights = new float[count];
        var totalWeight = 0f;

        for (int i = 0; i < count; i++)
        {
            var t = count == 1 ? 0f : i / (float)(count - 1);
            var weight = centerWeighted
                ? MathF.Exp(-2.8f * t * t)
                : MathF.Exp(-1.8f * t * t);
            weights[i] = weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0.0001f)
        {
            for (int i = 0; i < count; i++)
            {
                weights[i] = 1f / count;
            }
            return weights;
        }

        for (int i = 0; i < count; i++)
        {
            weights[i] /= totalWeight;
        }

        return weights;
    }

    private void RenderBalloonShapeWithBrush(
        CanvasDrawingSession ds,
        BalloonShape shape,
        Rect bounds,
        ICanvasBrush fillBrush,
        WinColor strokeColor,
        BalloonStyle style)
    {
        switch (shape)
        {
            case BalloonShape.Oval:
                ds.FillEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, fillBrush);
                ds.DrawEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.RoundedRect:
                ds.FillRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, fillBrush);
                ds.DrawRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.Radio:
                RenderRadioBalloonWithBrush(ds, bounds, fillBrush, strokeColor, style);
                break;

            case BalloonShape.Rectangle:
                ds.FillRectangle(bounds.ToWindowsRect(), fillBrush);
                ds.DrawRectangle(bounds.ToWindowsRect(), strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.Custom:
                ds.FillRectangle(bounds.ToWindowsRect(), fillBrush);
                ds.DrawRectangle(bounds.ToWindowsRect(), strokeColor, style.StrokeWidth);
                break;

            case BalloonShape.Thought:
                RenderThoughtBalloonWithBrush(ds, bounds, fillBrush, strokeColor, style);
                break;

            case BalloonShape.Splat:
                RenderSplatBalloonWithBrush(ds, bounds, fillBrush, strokeColor, style);
                break;

            case BalloonShape.Burst:
                RenderBurstBalloonWithBrush(ds, bounds, fillBrush, strokeColor, style);
                break;

            case BalloonShape.Whisper:
                RenderWhisperBalloonWithBrush(ds, bounds, fillBrush, strokeColor, style);
                break;
        }
    }

    private void RenderThoughtBalloonWithBrush(CanvasDrawingSession ds, Rect bounds, ICanvasBrush fillBrush, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateThoughtGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillBrush);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderSplatBalloonWithBrush(CanvasDrawingSession ds, Rect bounds, ICanvasBrush fillBrush, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateSplatGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillBrush);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderWhisperBalloonWithBrush(CanvasDrawingSession ds, Rect bounds, ICanvasBrush fillBrush, WinColor strokeColor, BalloonStyle style)
    {
        ds.FillEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, fillBrush);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        ds.DrawEllipse(bounds.Center.ToVector2(), bounds.Width / 2, bounds.Height / 2, strokeColor, style.StrokeWidth, strokeStyle);
    }

    private void RenderRadioBalloonWithBrush(CanvasDrawingSession ds, Rect bounds, ICanvasBrush fillBrush, WinColor strokeColor, BalloonStyle style)
    {
        ds.FillRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, fillBrush);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.DashDot };
        ds.DrawRoundedRectangle(bounds.ToWindowsRect(), style.CornerRadius, style.CornerRadius, strokeColor, style.StrokeWidth, strokeStyle);
    }

    private void RenderBurstBalloonWithBrush(CanvasDrawingSession ds, Rect bounds, ICanvasBrush fillBrush, WinColor strokeColor, BalloonStyle style)
    {
        using var geometry = CreateBurstGeometry(ds, bounds, style.ThoughtSmoothness);
        ds.FillGeometry(geometry, fillBrush);
        ds.DrawGeometry(geometry, strokeColor, style.StrokeWidth);
    }

    private void RenderRotationHandle(CanvasDrawingSession ds, Balloon balloon)
    {
        var handlePosition = BalloonGeometry.GetRotationHandlePosition(balloon);
        var anchorPosition = BalloonGeometry.GetRotationHandleAnchor(balloon);

        var lineColor = WinColor.FromArgb(255, 0, 120, 215);
        ds.DrawLine(anchorPosition.ToVector2(), handlePosition.ToVector2(), lineColor, 1f);

        var handleFill = WinColor.FromArgb(255, 255, 255, 255);
        var handleBorder = WinColor.FromArgb(255, 0, 120, 215);
        ds.FillCircle(handlePosition.ToVector2(), 6f, handleFill);
        ds.DrawCircle(handlePosition.ToVector2(), 6f, handleBorder, 2f);

        var arrowColor = WinColor.FromArgb(255, 0, 120, 215);
        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(new Vector2(handlePosition.X - 3, handlePosition.Y - 2));
        pathBuilder.AddArc(new Vector2(handlePosition.X + 3, handlePosition.Y - 2), 4, 4, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        pathBuilder.EndFigure(CanvasFigureLoop.Open);
        using var arcPath = CanvasGeometry.CreatePath(pathBuilder);
        ds.DrawGeometry(arcPath, arrowColor, 1.5f);
    }

    private string ResolveBalloonText(Balloon balloon, string? overrideText = null)
    {
        if (overrideText != null)
        {
            return overrideText;
        }

        if (_translationDocument == null)
        {
            return balloon.Text;
        }

        var primary = _translationDocument.GetBalloonDisplayText(balloon);
        if (_translationDocument.TranslationCompareMode == TranslationCompareMode.None)
        {
            return primary;
        }

        var compareLanguage = string.IsNullOrWhiteSpace(_translationDocument.CompareLanguage)
            ? _translationDocument.BaseLanguage
            : _translationDocument.CompareLanguage;
        var compare = _translationDocument.GetBalloonDisplayText(balloon, compareLanguage);
        if (string.Equals(primary, compare, StringComparison.Ordinal))
        {
            return primary;
        }

        return _translationDocument.TranslationCompareMode switch
        {
            TranslationCompareMode.SideBySide => $"{primary}  |  {compare}",
            TranslationCompareMode.Overlay => $"{primary}\n{compare}",
            _ => primary
        };
    }

    private BalloonTextRenderSettings ResolveBalloonTextRenderSettings(Balloon balloon)
    {
        if (_translationDocument == null || _translationDocument.TranslationCompareMode != TranslationCompareMode.None)
        {
            return new BalloonTextRenderSettings(
                IsVertical: false,
                IsRtl: false,
                MirrorTailTargets: false,
                LanguageTag: UiLocalizationService.CurrentLanguage);
        }

        var language = _translationDocument.ActiveLanguage;
        var orientation = _translationDocument.ResolveBalloonTranslationOrientation(balloon, language);
        var isVertical = orientation == TranslationTextOrientation.Vertical;
        var direction = _translationDocument.ResolveTranslationTextDirection(language, isVertical);
        var isRtl = direction == TranslationTextDirection.Rtl;
        var mirrorTailTargets = isRtl && _translationDocument.ShouldMirrorTailsForLanguage(language);

        return new BalloonTextRenderSettings(
            IsVertical: isVertical,
            IsRtl: isRtl,
            MirrorTailTargets: mirrorTailTargets,
            LanguageTag: language);
    }

    private static Balloon CreateTailMirroredRenderBalloon(Balloon source)
    {
        var mirrored = source.Clone();
        foreach (var tail in mirrored.Tails)
        {
            tail.SetTargetPoint(MirrorPointAroundCenterX(tail.TargetPoint, mirrored.Position.X));

            if (tail.AttachmentDirection.HasValue)
            {
                var direction = tail.AttachmentDirection.Value;
                tail.SetAttachmentDirection(new Point2(-direction.X, direction.Y));
            }

            if (tail.ControlPoint.HasValue)
            {
                tail.SetControlPoint(MirrorPointAroundCenterX(tail.ControlPoint.Value, mirrored.Position.X));
            }
        }

        return mirrored;
    }

    private static Point2 MirrorPointAroundCenterX(Point2 point, float centerX)
    {
        return new Point2(centerX - (point.X - centerX), point.Y);
    }

    private bool ShouldHighlightUntranslated(Balloon balloon)
    {
        if (_translationDocument == null || !_translationDocument.HighlightUntranslated)
        {
            return false;
        }

        if (string.Equals(_translationDocument.ActiveLanguage, _translationDocument.BaseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _translationDocument.IsBalloonUntranslated(balloon);
    }

    private void UpdateBalloonSize(CanvasDrawingSession ds, Balloon balloon, string? overrideText = null, IReadOnlyList<TextStyleSpan>? overrideSpans = null)
    {
        var text = ResolveBalloonText(balloon, overrideText);
        var signature = ComputeBalloonSizeSignature(balloon, text, overrideSpans);
        if (_balloonSizeSignatureCache.TryGetValue(balloon.Id, out var cachedSignature) && cachedSignature == signature)
        {
            return;
        }

        var balloonStyle = balloon.BalloonStyle;
        var textStyle = balloon.TextStyle;
        if (string.IsNullOrEmpty(text))
        {
            var width = balloonStyle.MinWidth;
            var height = balloonStyle.MinHeight;

            if (balloon.MaxTextWidth.HasValue)
            {
                width = MathF.Max(balloon.ComputedSize.Width, width);
                height = MathF.Max(balloon.ComputedSize.Height, height);
            }

            balloon.SetComputedSize(ConstrainBalloonSize(balloonStyle, width, height));
            _balloonSizeSignatureCache[balloon.Id] = signature;
            return;
        }

        if (balloon.MaxTextWidth.HasValue && textStyle.FitMode != TextFitMode.GrowBalloon)
        {
            var manualWidth = balloon.MaxTextWidth.Value + balloonStyle.PaddingLeft + balloonStyle.PaddingRight;
            var manualHeight = balloon.MaxTextHeight.HasValue
                ? balloon.MaxTextHeight.Value + balloonStyle.PaddingTop + balloonStyle.PaddingBottom
                : balloon.ComputedSize.Height;

            balloon.SetComputedSize(ConstrainBalloonSize(balloonStyle, manualWidth, manualHeight));
            _balloonSizeSignatureCache[balloon.Id] = signature;
            return;
        }

        var displayText = textStyle.AllCaps ? text.ToUpperInvariant() : text;
        var spans = overrideSpans ?? balloon.TextStyleSpans;

        var maxTextWidth = balloon.MaxTextWidth ?? 200f;
        using var textLayout = TextLayoutUtilities.CreateTextLayout(ds, textStyle, displayText, maxTextWidth, float.MaxValue, spans);

        var textWidth = (float)textLayout.LayoutBounds.Width;
        var textHeight = (float)textLayout.LayoutBounds.Height;

        var requiredWidth = textWidth + balloonStyle.PaddingLeft + balloonStyle.PaddingRight;
        var requiredHeight = textHeight + balloonStyle.PaddingTop + balloonStyle.PaddingBottom;

        var totalWidth = requiredWidth;
        var totalHeight = requiredHeight;

        if (balloon.Shape == BalloonShape.Oval || balloon.Shape == BalloonShape.Thought || balloon.Shape == BalloonShape.Splat || balloon.Shape == BalloonShape.Burst)
        {
            var sizeBoost = balloon.Shape == BalloonShape.Burst ? 1.25f : 1.2f;
            totalWidth *= sizeBoost;
            totalHeight *= sizeBoost;
        }

        balloon.SetComputedSize(ConstrainBalloonSize(balloonStyle, totalWidth, totalHeight));
        _balloonSizeSignatureCache[balloon.Id] = signature;
    }

    private static Size2 ConstrainBalloonSize(BalloonStyle style, float width, float height)
    {
        var constrainedWidth = MathF.Max(width, style.MinWidth);
        var constrainedHeight = MathF.Max(height, style.MinHeight);

        if (style.MaxWidth > 0)
        {
            constrainedWidth = MathF.Min(constrainedWidth, style.MaxWidth);
        }

        if (style.MaxHeight > 0)
        {
            constrainedHeight = MathF.Min(constrainedHeight, style.MaxHeight);
        }

        return new Size2(constrainedWidth, constrainedHeight);
    }

    private static int ComputeBalloonSizeSignature(Balloon balloon, string text, IReadOnlyList<TextStyleSpan>? overrideSpans)
    {
        var hash = new HashCode();
        hash.Add(text, StringComparer.Ordinal);
        hash.Add(balloon.Shape);
        hash.Add(balloon.MaxTextWidth.HasValue);
        hash.Add(balloon.MaxTextWidth.GetValueOrDefault());
        hash.Add(balloon.MaxTextHeight.HasValue);
        hash.Add(balloon.MaxTextHeight.GetValueOrDefault());

        AddBalloonStyleSizeSignature(ref hash, balloon.BalloonStyle);
        AddTextStyleLayoutSignature(ref hash, balloon.TextStyle);

        var spans = overrideSpans ?? balloon.TextStyleSpans;
        hash.Add(spans.Count);
        foreach (var span in spans)
        {
            hash.Add(span.Start);
            hash.Add(span.Length);
            AddTextStyleLayoutSignature(ref hash, span.Style);
        }

        return hash.ToHashCode();
    }

    private static void AddBalloonStyleSizeSignature(ref HashCode hash, BalloonStyle style)
    {
        hash.Add(style.PaddingLeft);
        hash.Add(style.PaddingTop);
        hash.Add(style.PaddingRight);
        hash.Add(style.PaddingBottom);
        hash.Add(style.MinWidth);
        hash.Add(style.MinHeight);
        hash.Add(style.MaxWidth);
        hash.Add(style.MaxHeight);
    }

    private static void AddTextStyleLayoutSignature(ref HashCode hash, TextStyle style)
    {
        hash.Add(style.FontFamily, StringComparer.OrdinalIgnoreCase);
        hash.Add(style.FontSize);
        hash.Add(style.AllCaps);
        hash.Add(style.Bold);
        hash.Add(style.Italic);
        hash.Add(style.Underline);
        hash.Add(style.Script);
        hash.Add(style.Tracking);
        hash.Add(style.LineHeight);
        hash.Add(style.Alignment);
        hash.Add(style.FitMode);
        hash.Add(style.RagMode);
        hash.Add(style.HyphenationLocale, StringComparer.OrdinalIgnoreCase);
        hash.Add(style.HyphenationLevel);
        hash.Add(style.JustificationStrength);
    }

    private bool RenderBalloonText(
        CanvasDrawingSession ds,
        Balloon balloon,
        float layerOpacity,
        BalloonTextRenderSettings renderSettings,
        bool cursorBlinkState = false)
    {
        var resolvedText = ResolveBalloonText(balloon);
        if (string.IsNullOrEmpty(resolvedText)) return false;

        var languageTag = renderSettings.LanguageTag;
        var textStyle = ResolveTextStyleForLanguage(
            balloon.TextStyle,
            languageTag,
            resolvedText,
            renderSettings.IsVertical);
        var spans = ResolveTextStyleSpansForLanguage(
            balloon.TextStyleSpans,
            languageTag,
            resolvedText,
            renderSettings.IsVertical);

        if (balloon.TextPath != null)
        {
            var pathResult = RenderTextOnPath(
                ds,
                resolvedText,
                textStyle,
                balloon.TextPath,
                balloon.Position,
                layerOpacity,
                applyOutline: true);
            return pathResult.IsOverflowing;
        }

        var textBounds = balloon.TextBounds;

        var displayText = textStyle.AllCaps ? resolvedText.ToUpperInvariant() : resolvedText;
        var isRtl = renderSettings.IsRtl;

        if (renderSettings.IsVertical)
        {
            return RenderVerticalBalloonText(ds, balloon, displayText, layerOpacity, isRtl, textStyle);
        }

        var typographySettings = TextLayoutUtilities.CreateTypographySettings(textStyle);

        if (ShouldUseShapeAwareLayout(balloon.Shape))
        {
            return RenderShapeAwareText(ds, balloon, displayText, layerOpacity, typographySettings, spans, cursorBlinkState, isRtl, textStyle);
        }

        var fitMode = textStyle.FitMode;
        var allowFit = (fitMode == TextFitMode.ShrinkToFit || fitMode == TextFitMode.TrackToFit) ||
                       (balloon.MaxTextWidth.HasValue && fitMode != TextFitMode.GrowBalloon && fitMode != TextFitMode.None);
        var overflowMode = textStyle.OverflowMode;
        var clipText = overflowMode == TextOverflowMode.Clip;

        using var fitted = TextLayoutUtilities.CreateFittedTextLayout(
            ds,
            displayText,
            textStyle,
            spans,
            textBounds.Width,
            textBounds.Height,
            allowFit,
            layerOpacity,
            computeDiagnostics: _showTypesettingDiagnostics,
            isRtl: isRtl);
        var origin = TextLayoutUtilities.GetTextOrigin(textBounds, fitted.Layout, textStyle.VerticalOffset, clampToBounds: clipText);
        var strokeLayers = BuildTextStrokeLayers(fitted.EffectiveStyle, layerOpacity);

        using var clipGeometry = clipText ? CanvasGeometry.CreateRectangle(ds, textBounds.ToWindowsRect()) : null;
        using var clipLayer = clipGeometry != null ? ds.CreateLayer(1f, clipGeometry) : null;
        if (TextWarpRenderer.IsWarpEnabled(textStyle))
        {
            var maxEffectExtent = GetMaxTextVisualEffectExtent(fitted.EffectiveStyle);
            var contentBounds = GetLayoutDrawBounds(fitted.Layout, origin).Inflate(maxEffectExtent + 2f, maxEffectExtent + 2f);
            using var source = new CanvasCommandList(ds);
            using (var sourceDs = source.CreateDrawingSession())
            {
                DrawTextLayoutWithStrokes(sourceDs, fitted.Layout, origin, fitted.EffectiveStyle, layerOpacity, strokeLayers);
            }

            TextWarpRenderer.DrawWarpedImage(ds, source, contentBounds, textStyle, opacity: 1f);
        }
        else
        {
            DrawTextLayoutWithStrokes(ds, fitted.Layout, origin, fitted.EffectiveStyle, layerOpacity, strokeLayers);
        }

        if (_showTypesettingDiagnostics && fitted.Diagnostics != null)
        {
            RenderTextDiagnostics(ds, textBounds, origin, fitted.Layout, fitted.Diagnostics, fitted.IsOverflowing);
        }

        return fitted.IsOverflowing;
    }

    private bool RenderVerticalBalloonText(
        CanvasDrawingSession ds,
        Balloon balloon,
        string displayText,
        float layerOpacity,
        bool isRtl,
        TextStyle textStyle)
    {
        var textBounds = balloon.TextBounds;
        var overflowMode = textStyle.OverflowMode;
        var clipText = overflowMode == TextOverflowMode.Clip;
        var strokeLayers = BuildTextStrokeLayers(textStyle, layerOpacity);

        var estimatedLineHeight = MathF.Max(1f, textStyle.FontSize * MathF.Max(0.8f, textStyle.LineHeight));
        var rowsPerColumn = Math.Max(1, (int)MathF.Floor(textBounds.Height / estimatedLineHeight));
        var columns = BuildVerticalColumns(displayText, rowsPerColumn);
        if (columns.Count == 0)
        {
            return false;
        }

        using var clipGeometry = clipText ? CanvasGeometry.CreateRectangle(ds, textBounds.ToWindowsRect()) : null;
        using var clipLayer = clipGeometry != null ? ds.CreateLayer(1f, clipGeometry) : null;

        using var lineFormat = new CanvasTextFormat
        {
            FontFamily = textStyle.FontFamily,
            FontSize = textStyle.FontSize,
            FontWeight = textStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = textStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.NoWrap,
            Direction = CanvasTextDirection.LeftToRightThenTopToBottom
        };

        if (!string.IsNullOrEmpty(textStyle.HyphenationLocale))
        {
            lineFormat.LocaleName = textStyle.HyphenationLocale;
        }

        var typographySettings = TextLayoutUtilities.CreateTypographySettings(textStyle);
        var columnGap = MathF.Max(1f, textStyle.FontSize * VerticalColumnGapFactor);
        var measuredColumns = new List<(CanvasTextLayout layout, float width, float height)>(columns.Count);
        var totalWidth = 0f;
        var maxHeight = 0f;
        foreach (var column in columns)
        {
            var layout = new CanvasTextLayout(ds, column, lineFormat, textBounds.Width, textBounds.Height);
            if (column.Length > 0)
            {
                TextLayoutUtilities.ApplyTracking(layout, textStyle, column.Length);
                TextLayoutUtilities.ApplyTypographyFeatures(layout, column.Length, typographySettings);
            }

            var drawBounds = layout.DrawBounds;
            if (drawBounds.Width <= 0d && drawBounds.Height <= 0d)
            {
                drawBounds = layout.LayoutBounds;
            }

            var width = MathF.Max(1f, (float)drawBounds.Width);
            var height = MathF.Max(1f, (float)drawBounds.Height);
            measuredColumns.Add((layout, width, height));
            totalWidth += width;
            maxHeight = MathF.Max(maxHeight, height);
        }

        if (measuredColumns.Count > 1)
        {
            totalWidth += columnGap * (measuredColumns.Count - 1);
        }

        var overflowWidth = totalWidth > textBounds.Width + 0.5f;
        var overflowHeight = maxHeight > textBounds.Height + 0.5f;
        var isOverflowing = overflowWidth || overflowHeight;

        if (isRtl)
        {
            var cursorX = textBounds.Right;
            foreach (var measured in measuredColumns)
            {
                var layout = measured.layout;
                var drawBounds = layout.DrawBounds;
                if (drawBounds.Width <= 0d && drawBounds.Height <= 0d)
                {
                    drawBounds = layout.LayoutBounds;
                }

                var originX = cursorX - measured.width - (float)drawBounds.X;
                var originY = textBounds.Y + (textBounds.Height - measured.height) * 0.5f - (float)drawBounds.Y + textStyle.VerticalOffset;
                DrawTextLayoutWithStrokes(ds, layout, new Vector2(originX, originY), textStyle, layerOpacity, strokeLayers);

                cursorX -= measured.width + columnGap;
                layout.Dispose();
            }
        }
        else
        {
            var cursorX = textBounds.X;
            foreach (var measured in measuredColumns)
            {
                var layout = measured.layout;
                var drawBounds = layout.DrawBounds;
                if (drawBounds.Width <= 0d && drawBounds.Height <= 0d)
                {
                    drawBounds = layout.LayoutBounds;
                }

                var originX = cursorX - (float)drawBounds.X;
                var originY = textBounds.Y + (textBounds.Height - measured.height) * 0.5f - (float)drawBounds.Y + textStyle.VerticalOffset;
                DrawTextLayoutWithStrokes(ds, layout, new Vector2(originX, originY), textStyle, layerOpacity, strokeLayers);

                cursorX += measured.width + columnGap;
                layout.Dispose();
            }
        }

        return isOverflowing;
    }

    private static List<string> BuildVerticalColumns(string text, int rowsPerColumn)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalized.Split('\n');
        for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var elements = GetTextElements(paragraphs[paragraphIndex]);
            if (elements.Count == 0)
            {
                result.Add("\u3000");
            }
            else
            {
                for (int i = 0; i < elements.Count; i += rowsPerColumn)
                {
                    var slice = elements.Skip(i).Take(rowsPerColumn);
                    result.Add(string.Join("\n", slice));
                }
            }

            if (paragraphIndex < paragraphs.Length - 1)
            {
                result.Add("\u3000");
            }
        }

        return result;
    }

    private static List<string> GetTextElements(string text)
    {
        var elements = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return elements;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            if (enumerator.GetTextElement() is string element)
            {
                elements.Add(element);
            }
        }

        return elements;
    }

    private static TextStyle ResolveTextStyleForLanguage(TextStyle style, string? languageTag, string displayText, bool preferVerticalLayout)
    {
        var resolvedFont = CjkFontSupport.ResolveFontFamily(
            style.FontFamily,
            languageTag,
            displayText,
            preferVerticalLayout);

        if (string.Equals(resolvedFont, style.FontFamily, StringComparison.OrdinalIgnoreCase))
        {
            return style.With(hyphenationLocale: "", hyphenationLevel: 0);
        }

        return style.With(fontFamily: resolvedFont, hyphenationLocale: "", hyphenationLevel: 0);
    }

    private static IReadOnlyList<TextStyleSpan> ResolveTextStyleSpansForLanguage(
        IReadOnlyList<TextStyleSpan>? spans,
        string? languageTag,
        string displayText,
        bool preferVerticalLayout)
    {
        if (spans == null || spans.Count == 0)
        {
            return Array.Empty<TextStyleSpan>();
        }

        List<TextStyleSpan>? resolved = null;
        for (int i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var resolvedStyle = ResolveTextStyleForLanguage(span.Style, languageTag, displayText, preferVerticalLayout);
            if (ReferenceEquals(resolvedStyle, span.Style))
            {
                if (resolved != null)
                {
                    resolved.Add(span.Clone());
                }

                continue;
            }

            resolved ??= spans.Take(i).Select(existing => existing.Clone()).ToList();
            resolved.Add(new TextStyleSpan(span.Start, span.Length, resolvedStyle));
        }

        return resolved ?? spans;
    }

    private static TextAlignment GetEffectiveAlignmentForDirection(TextAlignment alignment, bool isRtl)
    {
        if (!isRtl)
        {
            return alignment;
        }

        return alignment switch
        {
            TextAlignment.Left => TextAlignment.Right,
            TextAlignment.Right => TextAlignment.Left,
            _ => alignment
        };
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

    private bool RenderShapeAwareText(
        CanvasDrawingSession ds,
        Balloon balloon,
        string displayText,
        float layerOpacity,
        TypographySettings? typographySettings,
        IReadOnlyList<TextStyleSpan>? spans,
        bool cursorBlinkState,
        bool isRtl,
        TextStyle textStyle)
    {
        var balloonBounds = balloon.Bounds;
        var balloonStyle = balloon.BalloonStyle;
        var overflowMode = textStyle.OverflowMode;
        var clipText = overflowMode == TextOverflowMode.Clip;
        var fitMode = textStyle.FitMode;
        typographySettings ??= TextLayoutUtilities.CreateTypographySettings(textStyle);

        var effectiveStyle = textStyle;
        var containerHeight = balloonBounds.Height - balloonStyle.PaddingTop - balloonStyle.PaddingBottom;

        if (balloon.Shape == BalloonShape.Oval || balloon.Shape == BalloonShape.Thought || balloon.Shape == BalloonShape.Splat || balloon.Shape == BalloonShape.Whisper)
        {
            containerHeight = balloonBounds.Height * 0.72f;
        }
        else if (balloon.Shape == BalloonShape.Burst)
        {
            containerHeight = balloonBounds.Height * 0.55f;
        }

        var shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
            ds,
            displayText,
            effectiveStyle,
            balloon.Shape,
            balloonBounds,
            balloonStyle,
            typographySettings,
            spans,
            cursorBlinkState);

        if (shapeLayout.Lines.Length == 0)
        {
            return false;
        }

        if (shapeLayout.TotalHeight > containerHeight)
        {
            if (fitMode == TextFitMode.ShrinkToFit)
            {
                var scale = TryFindShapeAwareShrinkScale(ds, displayText, textStyle, balloon, typographySettings, containerHeight, spans);
                if (scale < 1f)
                {
                    effectiveStyle = textStyle.With(fontSize: textStyle.FontSize * scale);
                    typographySettings = TextLayoutUtilities.CreateTypographySettings(effectiveStyle);
                    shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                        ds,
                        displayText,
                        effectiveStyle,
                        balloon.Shape,
                        balloonBounds,
                        balloonStyle,
                        typographySettings,
                        spans);
                }
            }
            else if (fitMode == TextFitMode.TrackToFit)
            {
                var tracking = TryFindShapeAwareTracking(ds, displayText, textStyle, balloon, typographySettings, containerHeight, spans);
                if (MathF.Abs(tracking - textStyle.Tracking) > 0.005f)
                {
                    effectiveStyle = textStyle.With(tracking: tracking);
                    typographySettings = TextLayoutUtilities.CreateTypographySettings(effectiveStyle);
                    shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                        ds,
                        displayText,
                        effectiveStyle,
                        balloon.Shape,
                        balloonBounds,
                        balloonStyle,
                        typographySettings,
                        spans);
                }
            }
        }

        CanvasGeometry? clipGeometry = null;
        CanvasActiveLayer? clipLayer = null;

        if (clipText)
        {
            clipGeometry = CreateBalloonClipGeometry(ds, balloon);
            if (clipGeometry != null)
            {
                clipLayer = ds.CreateLayer(1f, clipGeometry);
            }
        }

        var centerY = balloonBounds.Y + balloonBounds.Height / 2f;
        var textStartY = centerY - shapeLayout.TotalHeight / 2f + effectiveStyle.VerticalOffset;

        var lineFormat = new CanvasTextFormat
        {
            FontFamily = effectiveStyle.FontFamily,
            FontSize = effectiveStyle.FontSize,
            FontWeight = effectiveStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = effectiveStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = CanvasHorizontalAlignment.Left, // Manual alignment for shape-aware
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.NoWrap, // Each line's text is already determined
            Direction = isRtl
                ? CanvasTextDirection.RightToLeftThenTopToBottom
                : CanvasTextDirection.LeftToRightThenTopToBottom
        };

        if (!string.IsNullOrEmpty(effectiveStyle.HyphenationLocale))
        {
            lineFormat.LocaleName = effectiveStyle.HyphenationLocale;
        }

        var strokeLayers = BuildTextStrokeLayers(effectiveStyle, layerOpacity);
        var maxEffectExtent = GetMaxTextVisualEffectExtent(effectiveStyle);

        var renderTarget = ds;
        var warpEnabled = TextWarpRenderer.IsWarpEnabled(effectiveStyle);
        CanvasCommandList? warpSource = null;
        CanvasDrawingSession? warpSourceDs = null;

        if (warpEnabled)
        {
            warpSource = new CanvasCommandList(ds);
            warpSourceDs = warpSource.CreateDrawingSession();
            renderTarget = warpSourceDs;
        }

        float minRenderedX = float.MaxValue;
        float minRenderedY = float.MaxValue;
        float maxRenderedX = float.MinValue;
        float maxRenderedY = float.MinValue;

        float y = textStartY;
        int charOffset = 0; // Track position in original text for span remapping
        foreach (var line in shapeLayout.Lines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                var lineLength = line.Text.Length;
                var lineSourceLength = Math.Max(0, line.CharacterCount);

                using var lineLayout = new CanvasTextLayout(ds, line.Text, lineFormat, line.Width, line.Height);

                TextLayoutUtilities.ApplyTracking(lineLayout, effectiveStyle, lineLength);

                TextLayoutUtilities.ApplyTypographyFeatures(lineLayout, lineLength, typographySettings);

                if (spans != null && spans.Count > 0)
                {
                    foreach (var span in spans)
                    {
                        if (span.Length <= 0) continue;

                        var spanEnd = span.Start + span.Length;
                        var lineEnd = charOffset + lineSourceLength;

                        if (span.Start < lineEnd && spanEnd > charOffset)
                        {
                            var localStart = Math.Max(span.Start - charOffset, 0);
                            var localEnd = Math.Min(spanEnd - charOffset, lineSourceLength);
                            var localLength = localEnd - localStart;

                            if (localLength > 0)
                            {
                                ApplySpanToLineLayout(lineLayout, effectiveStyle, span.Style, localStart, localLength, layerOpacity);
                            }
                        }
                    }
                }

                var lineX = TextLayoutUtilities.GetManualAlignedLineOriginX(
                    lineLayout,
                    balloonBounds.X + line.X,
                    line.Width,
                    GetEffectiveAlignmentForDirection(effectiveStyle.Alignment, isRtl));

                var lineOrigin = new System.Numerics.Vector2(lineX, y);
                DrawTextLayoutWithStrokes(renderTarget, lineLayout, lineOrigin, effectiveStyle, layerOpacity, strokeLayers);

                var lineDrawBounds = lineLayout.DrawBounds;
                if (lineDrawBounds.Width <= 0d && lineDrawBounds.Height <= 0d)
                {
                    lineDrawBounds = lineLayout.LayoutBounds;
                }

                minRenderedX = MathF.Min(minRenderedX, lineOrigin.X + (float)lineDrawBounds.X - maxEffectExtent);
                minRenderedY = MathF.Min(minRenderedY, lineOrigin.Y + (float)lineDrawBounds.Y - maxEffectExtent);
                maxRenderedX = MathF.Max(maxRenderedX, lineOrigin.X + (float)lineDrawBounds.X + MathF.Max(1f, (float)lineDrawBounds.Width) + maxEffectExtent);
                maxRenderedY = MathF.Max(maxRenderedY, lineOrigin.Y + (float)lineDrawBounds.Y + MathF.Max(1f, (float)lineDrawBounds.Height) + maxEffectExtent);

                charOffset += line.CharacterCount;
            }

            y += line.Height;
        }

        if (warpEnabled && warpSource != null && warpSourceDs != null)
        {
            warpSourceDs.Dispose();

            if (minRenderedX < float.MaxValue && minRenderedY < float.MaxValue)
            {
                var contentBounds = new Rect(
                    minRenderedX,
                    minRenderedY,
                    MathF.Max(1f, maxRenderedX - minRenderedX),
                    MathF.Max(1f, maxRenderedY - minRenderedY)).Inflate(2f, 2f);

                TextWarpRenderer.DrawWarpedImage(ds, warpSource, contentBounds, effectiveStyle, opacity: 1f);
            }

            warpSource.Dispose();
        }
        else
        {
            warpSourceDs?.Dispose();
            warpSource?.Dispose();
        }

        clipLayer?.Dispose();
        clipGeometry?.Dispose();

        if (_showTypesettingDiagnostics)
        {
            RenderShapeAwareDiagnostics(ds, balloon, shapeLayout, textStartY);
        }

        return shapeLayout.TotalHeight > containerHeight;
    }

    private bool RenderShapeAwareTextWithCursor(
        CanvasDrawingSession ds,
        Balloon balloon,
        string displayText,
        float layerOpacity,
        int cursorPos,
        int selectionStart,
        int selectionLength,
        IReadOnlyList<TextStyleSpan>? spans,
        bool cursorBlinkState,
        bool isRtl = false)
    {
        var textStyle = balloon.TextStyle;
        var balloonBounds = balloon.Bounds;
        var balloonStyle = balloon.BalloonStyle;
        var overflowMode = textStyle.OverflowMode;
        var clipText = overflowMode == TextOverflowMode.Clip;
        var fitMode = textStyle.FitMode;

        var typographySettings = TextLayoutUtilities.CreateTypographySettings(textStyle);
        var effectiveStyle = textStyle;
        var containerHeight = balloonBounds.Height - balloonStyle.PaddingTop - balloonStyle.PaddingBottom;

        if (balloon.Shape == BalloonShape.Oval || balloon.Shape == BalloonShape.Thought || balloon.Shape == BalloonShape.Splat || balloon.Shape == BalloonShape.Whisper)
        {
            containerHeight = balloonBounds.Height * 0.72f;
        }
        else if (balloon.Shape == BalloonShape.Burst)
        {
            containerHeight = balloonBounds.Height * 0.55f;
        }

        var shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
            ds,
            displayText,
            effectiveStyle,
            balloon.Shape,
            balloonBounds,
            balloonStyle,
            typographySettings,
            spans);

        if (shapeLayout.Lines.Length == 0)
        {
            return false;
        }

        if (shapeLayout.TotalHeight > containerHeight)
        {
            if (fitMode == TextFitMode.ShrinkToFit)
            {
                var scale = TryFindShapeAwareShrinkScale(ds, displayText, textStyle, balloon, typographySettings, containerHeight, spans);
                if (scale < 1f)
                {
                    effectiveStyle = textStyle.With(fontSize: textStyle.FontSize * scale);
                    typographySettings = TextLayoutUtilities.CreateTypographySettings(effectiveStyle);
                    shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                        ds,
                        displayText,
                        effectiveStyle,
                        balloon.Shape,
                        balloonBounds,
                        balloonStyle,
                        typographySettings,
                        spans);
                }
            }
            else if (fitMode == TextFitMode.TrackToFit)
            {
                var tracking = TryFindShapeAwareTracking(ds, displayText, textStyle, balloon, typographySettings, containerHeight, spans);
                if (MathF.Abs(tracking - textStyle.Tracking) > 0.005f)
                {
                    effectiveStyle = textStyle.With(tracking: tracking);
                    typographySettings = TextLayoutUtilities.CreateTypographySettings(effectiveStyle);
                    shapeLayout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                        ds,
                        displayText,
                        effectiveStyle,
                        balloon.Shape,
                        balloonBounds,
                        balloonStyle,
                        typographySettings,
                        spans);
                }
            }
        }

        CanvasGeometry? clipGeometry = null;
        CanvasActiveLayer? clipLayer = null;
        if (clipText)
        {
            clipGeometry = CreateBalloonClipGeometry(ds, balloon);
            if (clipGeometry != null)
            {
                clipLayer = ds.CreateLayer(1f, clipGeometry);
            }
        }

        var centerY = balloonBounds.Y + balloonBounds.Height / 2f;
        var textStartY = centerY - shapeLayout.TotalHeight / 2f + effectiveStyle.VerticalOffset;

        var lineFormat = new CanvasTextFormat
        {
            FontFamily = effectiveStyle.FontFamily,
            FontSize = effectiveStyle.FontSize,
            FontWeight = effectiveStyle.Bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontStyle = effectiveStyle.Italic ? Windows.UI.Text.FontStyle.Italic : Windows.UI.Text.FontStyle.Normal,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top,
            WordWrapping = CanvasWordWrapping.NoWrap,
            Direction = isRtl
                ? CanvasTextDirection.RightToLeftThenTopToBottom
                : CanvasTextDirection.LeftToRightThenTopToBottom
        };

        if (!string.IsNullOrEmpty(effectiveStyle.HyphenationLocale))
        {
            lineFormat.LocaleName = effectiveStyle.HyphenationLocale;
        }

        var strokeLayers = BuildTextStrokeLayers(effectiveStyle, layerOpacity);

        var charOffset = 0;
        float y = textStartY;

        int cursorLineIndex = -1;
        int cursorLineCharOffset = 0;
        var clampedCursorPos = Math.Min(cursorPos, displayText.Length);
        for (int i = 0; i < shapeLayout.Lines.Length; i++)
        {
            var line = shapeLayout.Lines[i];
            var lineEnd = charOffset + line.CharacterCount;

            if (clampedCursorPos >= charOffset && clampedCursorPos <= lineEnd)
            {
                cursorLineIndex = i;
                cursorLineCharOffset = charOffset;
                break;
            }

            charOffset = lineEnd;
        }

        charOffset = 0;

        var selectionSafeStart = Math.Clamp(selectionStart, 0, displayText.Length);
        var selectionSafeLength = Math.Clamp(selectionLength, 0, displayText.Length - selectionSafeStart);
        var selectionEnd = selectionSafeStart + selectionSafeLength;

        for (int lineIndex = 0; lineIndex < shapeLayout.Lines.Length; lineIndex++)
        {
            var line = shapeLayout.Lines[lineIndex];
            var lineCharEnd = charOffset + line.CharacterCount;

            if (!string.IsNullOrEmpty(line.Text))
            {
                var lineLength = line.Text.Length;
                var lineSourceLength = Math.Max(0, line.CharacterCount);

                using var lineLayout = new CanvasTextLayout(ds, line.Text, lineFormat, line.Width, line.Height);
                TextLayoutUtilities.ApplyTracking(lineLayout, effectiveStyle, lineLength);
                TextLayoutUtilities.ApplyTypographyFeatures(lineLayout, lineLength, typographySettings);

                if (spans != null && spans.Count > 0)
                {
                    foreach (var span in spans)
                    {
                        if (span.Length <= 0) continue;

                        var spanEnd = span.Start + span.Length;
                        if (span.Start < lineCharEnd && spanEnd > charOffset)
                        {
                            var localStart = Math.Max(span.Start - charOffset, 0);
                            var localEnd = Math.Min(spanEnd - charOffset, lineSourceLength);
                            var localLength = localEnd - localStart;

                            if (localLength > 0)
                            {
                                ApplySpanToLineLayout(lineLayout, effectiveStyle, span.Style, localStart, localLength, layerOpacity);
                            }
                        }
                    }
                }

                var lineX = TextLayoutUtilities.GetManualAlignedLineOriginX(
                    lineLayout,
                    balloonBounds.X + line.X,
                    line.Width,
                    GetEffectiveAlignmentForDirection(effectiveStyle.Alignment, isRtl));

                if (selectionSafeLength > 0 && lineCharEnd > selectionSafeStart && charOffset < selectionEnd)
                {
                    var selStart = Math.Max(selectionSafeStart, charOffset);
                    var selEnd = Math.Min(selectionEnd, lineCharEnd);
                    var localSelStart = selStart - charOffset;
                    var localSelLength = selEnd - selStart;

                    if (localSelLength > 0)
                    {
                        var regions = lineLayout.GetCharacterRegions(localSelStart, localSelLength);
                        foreach (var region in regions)
                        {
                            var regionRect = new Rect(
                                lineX + (float)region.LayoutBounds.X,
                                y + (float)region.LayoutBounds.Y,
                                (float)region.LayoutBounds.Width,
                                (float)region.LayoutBounds.Height);
                            ds.FillRectangle(regionRect.ToWindowsRect(), TextSelectionHighlightColor);
                        }
                    }
                }

                DrawTextLayoutWithStrokes(ds, lineLayout, new System.Numerics.Vector2(lineX, y), effectiveStyle, layerOpacity, strokeLayers);

                if (lineIndex == cursorLineIndex)
                {
                    var cursorLinePos = clampedCursorPos - cursorLineCharOffset;
                    var localCursorPos = Math.Clamp(cursorLinePos, 0, lineLength);

                    var cursorColor = WinColor.FromArgb(255, 0, 120, 215);
                    float cursorX = lineX;
                    float cursorY = y;
                    float cursorHeight = effectiveStyle.FontSize * 1.2f;

                    if (TextLayoutUtilities.TryGetCaretPosition(lineLayout, lineLength, localCursorPos, out var caretPos))
                    {
                        cursorX += caretPos.X;
                        cursorY += caretPos.Y;
                    }

                    if (TextLayoutUtilities.TryGetCaretRegion(lineLayout, lineLength, localCursorPos, out var caretBounds))
                    {
                        var caretRect = caretBounds.LayoutBounds;
                        cursorHeight = MathF.Max(1f, (float)caretRect.Height);
                    }

                    if (cursorBlinkState)
                    {
                        ds.DrawLine(
                            new Vector2(cursorX, cursorY),
                            new Vector2(cursorX, cursorY + cursorHeight),
                            cursorColor,
                            2f);
                    }
                }

                charOffset += line.CharacterCount;
            }

            y += line.Height;
        }

        clipLayer?.Dispose();
        clipGeometry?.Dispose();

        return shapeLayout.TotalHeight > containerHeight;
    }

    private static void ApplySpanToLineLayout(
        CanvasTextLayout layout,
        TextStyle baseStyle,
        TextStyle spanStyle,
        int start,
        int length,
        float opacity)
    {
        if (layout == null || length <= 0 || spanStyle == null) return;

        if (!string.Equals(spanStyle.FontFamily, baseStyle.FontFamily, StringComparison.OrdinalIgnoreCase))
        {
            layout.SetFontFamily(start, length, spanStyle.FontFamily);
        }

        if (MathF.Abs(spanStyle.FontSize - baseStyle.FontSize) > 0.001f)
        {
            layout.SetFontSize(start, length, spanStyle.FontSize);
        }

        if (spanStyle.Bold != baseStyle.Bold)
        {
            layout.SetFontWeight(start, length, spanStyle.Bold
                ? Microsoft.UI.Text.FontWeights.Bold
                : Microsoft.UI.Text.FontWeights.Normal);
        }

        if (spanStyle.Italic != baseStyle.Italic)
        {
            layout.SetFontStyle(start, length, spanStyle.Italic
                ? Windows.UI.Text.FontStyle.Italic
                : Windows.UI.Text.FontStyle.Normal);
        }

        if (spanStyle.Underline != baseStyle.Underline)
        {
            layout.SetUnderline(start, length, spanStyle.Underline);
        }

        if (!spanStyle.TextColor.Equals(baseStyle.TextColor))
        {
            var color = spanStyle.TextColor.ToWindowsColor();
            var adjustedColor = WinColor.FromArgb(
                (byte)(color.A * opacity),
                color.R,
                color.G,
                color.B);
            TextLayoutUtilities.ApplyFillColor(layout, adjustedColor, start, length);
        }

        if (MathF.Abs(spanStyle.Tracking - baseStyle.Tracking) > 0.0001f)
        {
            TextLayoutUtilities.ApplyTracking(layout, spanStyle, start, length);
        }
    }

    private float TryFindShapeAwareShrinkScale(
        CanvasDrawingSession ds,
        string text,
        TextStyle baseStyle,
        Balloon balloon,
        Typesetting.TypographySettings typographySettings,
        float targetHeight,
        IReadOnlyList<TextStyleSpan>? spans = null)
    {
        const float MinScale = 0.25f;
        const int MaxIterations = 8;

        float low = MinScale;
        float high = 1f;
        float bestScale = MinScale;

        for (int i = 0; i < MaxIterations; i++)
        {
            var mid = (low + high) / 2f;
            var testStyle = baseStyle.With(fontSize: baseStyle.FontSize * mid);
            var testTypography = TextLayoutUtilities.CreateTypographySettings(testStyle);

            var layout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                ds,
                text,
                testStyle,
                balloon.Shape,
                balloon.Bounds,
                balloon.BalloonStyle,
                testTypography,
                spans);

            if (layout.TotalHeight <= targetHeight)
            {
                bestScale = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return MathF.Max(MinScale, bestScale * 0.98f);
    }

    private float TryFindShapeAwareTracking(
        CanvasDrawingSession ds,
        string text,
        TextStyle baseStyle,
        Balloon balloon,
        Typesetting.TypographySettings typographySettings,
        float targetHeight,
        IReadOnlyList<TextStyleSpan>? spans = null)
    {
        const float MinTracking = -0.15f;
        const int MaxIterations = 8;

        float low = MinTracking;
        float high = baseStyle.Tracking;
        float bestTracking = MinTracking;

        for (int i = 0; i < MaxIterations; i++)
        {
            var mid = (low + high) / 2f;
            var testStyle = baseStyle.With(tracking: mid);
            var testTypography = TextLayoutUtilities.CreateTypographySettings(testStyle);

            var layout = Typesetting.ShapeTextLayout.CreateShapeAwareLayout(
                ds,
                text,
                testStyle,
                balloon.Shape,
                balloon.Bounds,
                balloon.BalloonStyle,
                testTypography,
                spans);

            if (layout.TotalHeight <= targetHeight)
            {
                bestTracking = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return bestTracking;
    }

    private CanvasGeometry? CreateBalloonClipGeometry(CanvasDrawingSession ds, Balloon balloon)
    {
        var bounds = balloon.Bounds;

        return balloon.Shape switch
        {
            BalloonShape.Oval or BalloonShape.Whisper =>
                CanvasGeometry.CreateEllipse(ds,
                    bounds.X + bounds.Width / 2f,
                    bounds.Y + bounds.Height / 2f,
                    bounds.Width / 2f,
                    bounds.Height / 2f),

            BalloonShape.Thought =>
                CreateThoughtGeometry(ds, bounds, balloon.BalloonStyle.ThoughtSmoothness),

            BalloonShape.Splat =>
                CreateSplatGeometry(ds, bounds, balloon.BalloonStyle.ThoughtSmoothness),

            BalloonShape.RoundedRect =>
                CanvasGeometry.CreateRoundedRectangle(ds,
                    bounds.ToWindowsRect(),
                    balloon.BalloonStyle.CornerRadius,
                    balloon.BalloonStyle.CornerRadius),

            _ => CanvasGeometry.CreateRectangle(ds, bounds.ToWindowsRect())
        };
    }

    private void RenderShapeAwareDiagnostics(
        CanvasDrawingSession ds,
        Balloon balloon,
        Typesetting.ShapeAwareLayoutResult shapeLayout,
        float textStartY)
    {
        var bounds = balloon.Bounds;

        float y = textStartY;
        foreach (var line in shapeLayout.Lines)
        {
            var lineRect = new Windows.Foundation.Rect(
                bounds.X + line.X,
                y,
                line.Width,
                line.Height);
            ds.DrawRectangle(lineRect, DiagnosticsLineBoundsColor, 0.5f);
            y += line.Height;
        }

        using var boundsStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot };
        ds.DrawRectangle(balloon.TextBounds.ToWindowsRect(), DiagnosticsTextBoundsColor, 1f, boundsStrokeStyle);

        if (shapeLayout.IsOverflowing)
        {
            var overflowColor = WinColor.FromArgb(180, 255, 80, 80);
            using var overflowStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
            ds.DrawRectangle(balloon.TextBounds.ToWindowsRect(), overflowColor, 2f, overflowStrokeStyle);
        }

        var characterCount = ResolveBalloonText(balloon).Length;
        var diagText = $"Lines: {shapeLayout.Lines.Length}\nCharacters: {characterCount}\nText: {shapeLayout.MaxLineWidth:F0}×{shapeLayout.TotalHeight:F0}";
        using var diagFormat = new CanvasTextFormat
        {
            FontFamily = "Segoe UI",
            FontSize = 9f,
            WordWrapping = CanvasWordWrapping.NoWrap
        };

        using var diagLayout = new CanvasTextLayout(ds, diagText, diagFormat, 200, 100);
        var diagWidth = (float)diagLayout.LayoutBounds.Width + 8;
        var diagHeight = (float)diagLayout.LayoutBounds.Height + 6;

        var diagX = balloon.TextBounds.Right - diagWidth - 2;
        var diagY = balloon.TextBounds.Top + 2;

        ds.FillRoundedRectangle(
            new Windows.Foundation.Rect(diagX, diagY, diagWidth, diagHeight),
            3, 3,
            DiagnosticsBackgroundColor);

        ds.DrawTextLayout(diagLayout, new System.Numerics.Vector2(diagX + 4, diagY + 3), DiagnosticsTextColor);
    }

    private void RenderTextDiagnostics(
        CanvasDrawingSession ds,
        Rect textBounds,
        System.Numerics.Vector2 origin,
        CanvasTextLayout layout,
        TextLayoutUtilities.TextLayoutDiagnostics diagnostics,
        bool isOverflowing)
    {
        using var boundsStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dot };
        ds.DrawRectangle(textBounds.ToWindowsRect(), DiagnosticsTextBoundsColor, 1f, boundsStrokeStyle);

        var lineMetrics = layout.LineMetrics;
        float y = origin.Y;
        for (int i = 0; i < lineMetrics.Length; i++)
        {
            var lineHeight = lineMetrics[i].Height;
            var lineWidth = diagnostics.LineWidths[i];

            var lineRect = new Windows.Foundation.Rect(
                textBounds.X,
                y,
                lineWidth,
                lineHeight);
            ds.DrawRectangle(lineRect, DiagnosticsLineBoundsColor, 0.5f);

            y += lineHeight;
        }

        if (isOverflowing)
        {
            var overflowColor = WinColor.FromArgb(180, 255, 80, 80);
            using var overflowStrokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
            ds.DrawRectangle(textBounds.ToWindowsRect(), overflowColor, 2f, overflowStrokeStyle);
        }

        var diagText = diagnostics.DiagnosticText;
        using var diagFormat = new CanvasTextFormat
        {
            FontFamily = "Segoe UI",
            FontSize = 9f,
            WordWrapping = CanvasWordWrapping.NoWrap
        };

        using var diagLayout = new CanvasTextLayout(ds, diagText, diagFormat, 200, 100);
        var diagWidth = (float)diagLayout.LayoutBounds.Width + 8;
        var diagHeight = (float)diagLayout.LayoutBounds.Height + 6;

        var diagX = textBounds.Right - diagWidth - 2;
        var diagY = textBounds.Top + 2;

        ds.FillRoundedRectangle(
            new Windows.Foundation.Rect(diagX, diagY, diagWidth, diagHeight),
            3, 3,
            DiagnosticsBackgroundColor);

        ds.DrawTextLayout(diagLayout, new System.Numerics.Vector2(diagX + 4, diagY + 3), DiagnosticsTextColor);
    }

    private bool RenderBalloonTextWithCursor(
        CanvasDrawingSession ds,
        Balloon balloon,
        float layerOpacity,
        string editingText,
        int cursorPos,
        int selectionStart,
        int selectionLength,
        IReadOnlyList<TextStyleSpan>? editingTextStyleSpans,
        bool cursorBlinkState)
    {
        var textStyle = balloon.TextStyle;
        var displayText = textStyle.AllCaps ? editingText.ToUpperInvariant() : editingText;
        var spans = editingTextStyleSpans ?? balloon.TextStyleSpans;

        if (ShouldUseShapeAwareLayout(balloon.Shape))
        {
            return RenderShapeAwareTextWithCursor(
                ds,
                balloon,
                displayText,
                layerOpacity,
                cursorPos,
                selectionStart,
                selectionLength,
                spans,
                cursorBlinkState);
        }

        return RenderStandardTextWithCursor(
            ds,
            balloon,
            displayText,
            layerOpacity,
            cursorPos,
            selectionStart,
            selectionLength,
            spans,
            cursorBlinkState);
    }

    private bool RenderStandardTextWithCursor(
        CanvasDrawingSession ds,
        Balloon balloon,
        string displayText,
        float layerOpacity,
        int cursorPos,
        int selectionStart,
        int selectionLength,
        IReadOnlyList<TextStyleSpan>? spans,
        bool cursorBlinkState)
    {
        var textStyle = balloon.TextStyle;
        var textBounds = balloon.TextBounds;

        var fitMode = textStyle.FitMode;
        var allowFit = (fitMode == TextFitMode.ShrinkToFit || fitMode == TextFitMode.TrackToFit) ||
                       (balloon.MaxTextWidth.HasValue && fitMode != TextFitMode.GrowBalloon && fitMode != TextFitMode.None);
        var overflowMode = textStyle.OverflowMode;
        var clipText = overflowMode == TextOverflowMode.Clip;

        using var fitted = TextLayoutUtilities.CreateFittedTextLayout(
            ds,
            displayText,
            textStyle,
            spans,
            textBounds.Width,
            textBounds.Height,
            allowFit,
            layerOpacity);
        var textLayout = fitted.Layout;
        var origin = TextLayoutUtilities.GetTextOrigin(textBounds, textLayout, textStyle.VerticalOffset, clampToBounds: clipText);
        var layoutOffset = TextLayoutUtilities.GetLayoutAlignmentOffset(textLayout);
        var strokeLayers = BuildTextStrokeLayers(fitted.EffectiveStyle, layerOpacity);
        using var clipGeometry = clipText ? CanvasGeometry.CreateRectangle(ds, textBounds.ToWindowsRect()) : null;
        using var clipLayer = clipGeometry != null ? ds.CreateLayer(1f, clipGeometry) : null;

        var safeStart = Math.Clamp(selectionStart, 0, displayText.Length);
        var safeLength = Math.Clamp(selectionLength, 0, displayText.Length - safeStart);
        if (safeLength > 0)
        {
            var highlightColor = TextSelectionHighlightColor;
            var regions = textLayout.GetCharacterRegions(safeStart, safeLength);
            foreach (var region in regions)
            {
                var regionRect = new Rect(
                    origin.X + layoutOffset.X + (float)region.LayoutBounds.X,
                    origin.Y + layoutOffset.Y + (float)region.LayoutBounds.Y,
                    (float)region.LayoutBounds.Width,
                    (float)region.LayoutBounds.Height);

                ds.FillRectangle(regionRect.ToWindowsRect(), highlightColor);
            }
        }

        DrawTextLayoutWithStrokes(ds, textLayout, origin, fitted.EffectiveStyle, layerOpacity, strokeLayers);

        var cursorColor = WinColor.FromArgb(255, 0, 120, 215); // Blue cursor

        var clampedCursorPos = Math.Min(cursorPos, displayText.Length);
        var cursorX = origin.X;
        var cursorY = origin.Y;
        var cursorHeight = fitted.EffectiveStyle.FontSize * 1.2f;

        if (TextLayoutUtilities.TryGetCaretPosition(textLayout, displayText.Length, clampedCursorPos, out var caretPos))
        {
            cursorX = origin.X + caretPos.X;
            cursorY = origin.Y + caretPos.Y;
        }

        if (TextLayoutUtilities.TryGetCaretRegion(textLayout, displayText.Length, clampedCursorPos, out var caretBounds))
        {
            var caretRect = caretBounds.LayoutBounds;
            cursorHeight = MathF.Max(1f, (float)caretRect.Height);
        }

        if (cursorBlinkState)
        {
            ds.DrawLine(
                new Vector2(cursorX, cursorY),
                new Vector2(cursorX, cursorY + cursorHeight),
                cursorColor,
                2f);
        }

        return fitted.IsOverflowing;
    }

    private void RenderPanelMembershipBadge(CanvasDrawingSession ds, Rect bounds, Guid panelId)
    {
        var badgeSize = 8f;
        var badgeX = (float)bounds.Right - badgeSize - 4;
        var badgeY = (float)bounds.Top + 4;

        var hash = panelId.GetHashCode();
        var hue = Math.Abs(hash % 360);
        var badgeColor = HslToColor(hue, 0.7f, 0.5f);

        ds.FillEllipse(new Vector2(badgeX + badgeSize / 2, badgeY + badgeSize / 2), badgeSize / 2, badgeSize / 2, badgeColor);
        ds.DrawEllipse(new Vector2(badgeX + badgeSize / 2, badgeY + badgeSize / 2), badgeSize / 2, badgeSize / 2, WinColor.FromArgb(180, 255, 255, 255), 1f);
    }

    private static WinColor HslToColor(float h, float s, float l)
    {
        float c = (1 - Math.Abs(2 * l - 1)) * s;
        float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        float m = l - c / 2;

        float r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return WinColor.FromArgb(255,
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    private void RenderOverflowHighlight(CanvasDrawingSession ds, Rect bounds)
    {
        var overflowColor = WinColor.FromArgb(200, 200, 30, 30);
        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        ds.DrawRectangle(bounds.ToWindowsRect(), overflowColor, 2f, strokeStyle);
    }

    private void RenderSelectionHighlight(CanvasDrawingSession ds, Rect bounds, bool drawHandles = true)
    {
        var selectionColor = WinColor.FromArgb(255, 0, 120, 215); // Windows blue
        var inflated = bounds.Inflate(4, 4);

        using var strokeStyle = new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash };
        ds.DrawRectangle(inflated.ToWindowsRect(), selectionColor, 2f, strokeStyle);

        if (!drawHandles) return;

        var handleSize = Math.Clamp(SelectionHandleSize, 4f, 24f);
        var handleColor = WinColor.FromArgb(255, 255, 255, 255);
        var handleBorder = selectionColor;

        DrawHandle(ds, inflated.TopLeft, handleSize, handleColor, handleBorder);
        DrawHandle(ds, inflated.TopRight, handleSize, handleColor, handleBorder);
        DrawHandle(ds, inflated.BottomLeft, handleSize, handleColor, handleBorder);
        DrawHandle(ds, inflated.BottomRight, handleSize, handleColor, handleBorder);
    }

    private void DrawHandle(CanvasDrawingSession ds, Point2 center, float size, WinColor fill, WinColor stroke)
    {
        var halfSize = size / 2;
        ds.FillRectangle(center.X - halfSize, center.Y - halfSize, size, size, fill);
        ds.DrawRectangle(center.X - halfSize, center.Y - halfSize, size, size, stroke, 1f);
    }

    private void RenderTailHandle(CanvasDrawingSession ds, Point2 targetPoint)
    {
        var handleColor = WinColor.FromArgb(255, 0, 120, 215);
        var fillColor = WinColor.FromArgb(255, 255, 255, 255);

        ds.FillCircle(targetPoint.ToVector2(), 6f, fillColor);
        ds.DrawCircle(targetPoint.ToVector2(), 6f, handleColor, 2f);
    }

    private void RenderTailAttachmentHandle(CanvasDrawingSession ds, Point2 attachPoint)
    {
        var handleColor = WinColor.FromArgb(255, 0, 120, 215);
        var fillColor = WinColor.FromArgb(255, 255, 255, 255);
        var size = 8f;
        var half = size / 2;

        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(new Vector2(attachPoint.X, attachPoint.Y - half));
        pathBuilder.AddLine(new Vector2(attachPoint.X + half, attachPoint.Y));
        pathBuilder.AddLine(new Vector2(attachPoint.X, attachPoint.Y + half));
        pathBuilder.AddLine(new Vector2(attachPoint.X - half, attachPoint.Y));
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        using var geometry = CanvasGeometry.CreatePath(pathBuilder);
        ds.FillGeometry(geometry, fillColor);
        ds.DrawGeometry(geometry, handleColor, 2f);
    }

    private static WinColor ApplyOpacity(WinColor color, float opacity)
    {
        var clamped = ClampOpacity(opacity);
        return WinColor.FromArgb((byte)(color.A * clamped), color.R, color.G, color.B);
    }

    private static float ClampOpacity(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }
}
