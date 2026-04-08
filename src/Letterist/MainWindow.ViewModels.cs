using Letterist.Model;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;

namespace Letterist;


public abstract class LayerListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? HoverToolTip { get; set; }
    public bool IsVisible { get; set; }
    public bool IsLocked { get; set; }
    public bool IsActive { get; set; }
    public bool IsGroup { get; set; }
    public bool IsBalloon { get; protected set; }
    public bool IsExpanded { get; set; }

    public string VisibilityGlyph => IsVisible ? "\uE7B3" : "\uED1A"; // Eye / Hidden
    public string LockGlyph => IsLocked ? "\uE72E" : "\uE785"; // Lock / Unlock
    public virtual string BalloonGlyph => "\uE90A"; // Comment icon
    public Microsoft.UI.Xaml.Media.Brush IndicatorIconForeground =>
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 110, 110, 110));
    public Microsoft.UI.Xaml.Media.Brush LockIconForeground => IsLocked
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 215, 140))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170));
    public Microsoft.UI.Xaml.Media.Brush LockButtonBackground => IsLocked
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 86, 58, 26))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public Microsoft.UI.Xaml.Media.Brush TextColor => IsActive
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170));

    public virtual bool CanExpand => false;
    public virtual Microsoft.UI.Xaml.Visibility GroupExpandVisibility => CanExpand ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public virtual string ExpandGlyph => IsExpanded ? "\uE70D" : "\uE76C"; // ChevronDown / ChevronRight
    public virtual Microsoft.UI.Xaml.Visibility GroupIconVisibility => Microsoft.UI.Xaml.Visibility.Collapsed;
    public virtual string GroupIconGlyph => "";
    public virtual Windows.UI.Text.FontWeight FontWeightValue => Microsoft.UI.Text.FontWeights.Normal;
    public virtual Microsoft.UI.Xaml.Thickness LeftMargin => new Microsoft.UI.Xaml.Thickness(0);
    public virtual Microsoft.UI.Xaml.Visibility LayerControlsVisibility => Microsoft.UI.Xaml.Visibility.Visible;
    public virtual Microsoft.UI.Xaml.Visibility BalloonIndicatorVisibility => IsBalloon
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
}

public class LayerViewModel : LayerListItemViewModel
{
    public Guid? GroupId { get; set; }
    public bool IsInGroup => GroupId.HasValue;
    public LayerKind Kind { get; set; } = LayerKind.Balloon;
    public bool IsBackground => Kind == LayerKind.Image;
    public int BalloonCount { get; set; }

    public override Microsoft.UI.Xaml.Thickness LeftMargin => IsInGroup
        ? new Microsoft.UI.Xaml.Thickness(20, 0, 0, 0)
        : new Microsoft.UI.Xaml.Thickness(0);

    public override Microsoft.UI.Xaml.Visibility GroupIconVisibility => IsBackground
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    public override string GroupIconGlyph => IsBackground ? "\uE91B" : "";

    public override bool CanExpand => BalloonCount > 0;

    public LayerViewModel()
    {
        IsGroup = false;
    }
}

public class LayerGroupViewModel : LayerListItemViewModel
{
    public int LayerCount { get; set; }
    public override bool CanExpand => true;

    public override Microsoft.UI.Xaml.Visibility GroupIconVisibility => Microsoft.UI.Xaml.Visibility.Visible;
    public override string GroupIconGlyph => "\uE8B7"; // Folder icon

    public override Windows.UI.Text.FontWeight FontWeightValue => Microsoft.UI.Text.FontWeights.SemiBold;

    public Microsoft.UI.Xaml.Media.Brush GroupBackground =>
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 45, 45, 45));

    public LayerGroupViewModel()
    {
        IsGroup = true;
    }
}

public class BalloonViewModel : LayerListItemViewModel
{
    public Guid LayerId { get; set; }
    public Guid? PanelId { get; set; }
    public string? PanelName { get; set; }
    public bool IsInGroup { get; set; }
    public bool IsInPanel => PanelId.HasValue;

    public override Microsoft.UI.Xaml.Thickness LeftMargin => IsInGroup
        ? new Microsoft.UI.Xaml.Thickness(40, 0, 0, 0)
        : new Microsoft.UI.Xaml.Thickness(24, 0, 0, 0);

    public Microsoft.UI.Xaml.Visibility PanelBadgeVisibility => IsInPanel
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;

    public BalloonViewModel()
    {
        IsBalloon = true;
        IsGroup = false;
    }
}

public class PanelSectionHeaderViewModel : LayerListItemViewModel
{
    public int PanelCount { get; set; }
    public override bool CanExpand => true;
    public override Microsoft.UI.Xaml.Visibility GroupIconVisibility => Microsoft.UI.Xaml.Visibility.Visible;
    public override string GroupIconGlyph => "\uE737"; // GridView icon (panels)
    public override Windows.UI.Text.FontWeight FontWeightValue => Microsoft.UI.Text.FontWeights.SemiBold;
    public Microsoft.UI.Xaml.Media.Brush GroupBackground =>
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 35, 50, 60));

    public bool IsPanelSection { get; } = true;

    public PanelSectionHeaderViewModel()
    {
        IsGroup = true;
        Name = "Panels";
    }
}

public class PanelZoneViewModel : LayerListItemViewModel
{
    public bool IsPanel { get; } = true;
    public int Order { get; set; }
    public Model.PanelShape PanelShape { get; set; }
    public int BalloonCount { get; set; }

    public override Microsoft.UI.Xaml.Thickness LeftMargin => new Microsoft.UI.Xaml.Thickness(20, 0, 0, 0);

    public override string BalloonGlyph => PanelShape switch
    {
        Model.PanelShape.Rectangle => "\uE739",    // Checkbox icon (rectangle)
        Model.PanelShape.RoundedRect => "\uE8B7",  // Folder icon (rounded)
        Model.PanelShape.Ellipse => "\uF158",      // Circle shape
        Model.PanelShape.Custom => "\uE8F1",       // Custom shape
        _ => "\uE739"
    };

    public override bool CanExpand => BalloonCount > 0;
    public override Microsoft.UI.Xaml.Visibility BalloonIndicatorVisibility => Microsoft.UI.Xaml.Visibility.Visible;
    public override Microsoft.UI.Xaml.Visibility LayerControlsVisibility => Microsoft.UI.Xaml.Visibility.Visible;

    public PanelZoneViewModel()
    {
        IsBalloon = false;
        IsGroup = false;
    }
}

public class PanelBalloonViewModel : LayerListItemViewModel
{
    public Guid LayerId { get; set; }
    public Guid PanelId { get; set; }

    public override Microsoft.UI.Xaml.Thickness LeftMargin => new Microsoft.UI.Xaml.Thickness(40, 0, 0, 0);
    public override Microsoft.UI.Xaml.Visibility LayerControlsVisibility => Microsoft.UI.Xaml.Visibility.Collapsed;

    public PanelBalloonViewModel()
    {
        IsBalloon = true;
        IsGroup = false;
    }
}

public class PanelFloatingImageViewModel : FloatingImageViewModel
{
    public Guid PanelId { get; set; }

    public override Microsoft.UI.Xaml.Thickness LeftMargin => new Microsoft.UI.Xaml.Thickness(40, 0, 0, 0);
    public override Microsoft.UI.Xaml.Visibility LayerControlsVisibility => Microsoft.UI.Xaml.Visibility.Collapsed;

    public PanelFloatingImageViewModel()
    {
        IsGroup = false;
    }
}

public class FloatingImageViewModel : LayerListItemViewModel
{
    public Guid LayerId { get; set; }
    public bool IsInGroup { get; set; }
    public string? ImagePath { get; set; }
    public bool IsFloatingImage { get; } = true;

    public override Microsoft.UI.Xaml.Thickness LeftMargin => IsInGroup
        ? new Microsoft.UI.Xaml.Thickness(40, 0, 0, 0)
        : new Microsoft.UI.Xaml.Thickness(24, 0, 0, 0);

    public override string BalloonGlyph => "\uEB9F"; // Image icon
    public override Microsoft.UI.Xaml.Visibility BalloonIndicatorVisibility => Microsoft.UI.Xaml.Visibility.Visible;

    public FloatingImageViewModel()
    {
        IsBalloon = false;
        IsGroup = false;
    }
}

public class PageViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string SizeText { get; set; } = "";
    public bool IsActive { get; set; }
    public WriteableBitmap? Thumbnail { get; set; }

    public Microsoft.UI.Xaml.Media.Brush TextColor => IsActive
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170));
}

public sealed class TargetLanguageViewModel
{
    public string Name { get; set; } = "";
    public int TranslatedCount { get; set; }
    public int TotalCount { get; set; }
    public string StatusText => TotalCount > 0 ? $"{TranslatedCount}/{TotalCount}" : "0";
    public Microsoft.UI.Xaml.Media.Brush StatusColor => TranslatedCount == TotalCount && TotalCount > 0
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 120, 220, 120))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170));
}

public sealed class TranslationBalloonListItemViewModel
{
    public Guid BalloonId { get; set; }
    public Guid PageId { get; set; }
    public int PageNumber { get; set; }
    public int BalloonNumber { get; set; }
    public string Title { get; set; } = "";
    public string Preview { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string TargetText { get; set; } = "";
    public bool IsTranslated { get; set; }
    public string StatusText => IsTranslated
        ? UiLocalizationService.GetString("translation.status.translated")
        : UiLocalizationService.GetString("translation.status.source");
    public Microsoft.UI.Xaml.Media.Brush StatusBrush => IsTranslated
        ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 120, 220, 120))
        : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 170, 170, 170));
}

internal sealed class RecentFileEntry
{
    public string Path { get; set; } = "";
    public bool IsPackage { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTime LastOpenedUtc { get; set; }
}

internal sealed class AutosaveInfo
{
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = "";
    public DateTime SavedAtUtc { get; set; }
    public string? SourceFolderPath { get; set; }
    public string? SourcePackagePath { get; set; }
}

internal sealed class AutosaveCandidate
{
    public AutosaveCandidate(string folderPath, AutosaveInfo info)
    {
        FolderPath = folderPath;
        Info = info;
    }

    public string FolderPath { get; }
    public AutosaveInfo Info { get; }
}

internal sealed class BalloonClipboardData
{
    public List<BalloonClipboardItem> Balloons { get; set; } = new();
    public List<FloatingImageClipboardItem> FloatingImages { get; set; } = new();
}

internal sealed class BalloonClipboardItem
{
    public Point2 Position { get; set; }
    public BalloonShape Shape { get; set; }
    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Default;
    public Guid? BalloonStyleId { get; set; }
    public BalloonStyleOverride? BalloonStyleOverrides { get; set; }
    public string Text { get; set; } = "";
    public TextStyle TextStyle { get; set; } = TextStyle.Default;
    public Guid? TextStyleId { get; set; }
    public TextStyleOverride? TextStyleOverrides { get; set; }
    public List<TextStyleSpan> TextStyleSpans { get; set; } = new();
    public string? CustomShapePathData { get; set; }
    public Size2 ComputedSize { get; set; }
    public float? MaxTextWidth { get; set; }
    public float? MaxTextHeight { get; set; }
    public TextPath? TextPath { get; set; }
    public ClipboardTail? Tail { get; set; }

    public static BalloonClipboardItem FromBalloon(Balloon balloon)
    {
        return new BalloonClipboardItem
        {
            Position = balloon.Position,
            Shape = balloon.Shape,
            BalloonStyle = balloon.BalloonStyle,
            BalloonStyleId = balloon.BalloonStyleId,
            BalloonStyleOverrides = balloon.BalloonStyleOverrides.Clone(),
            Text = balloon.Text,
            TextStyle = balloon.TextStyle,
            TextStyleId = balloon.TextStyleId,
            TextStyleOverrides = balloon.TextStyleOverrides.Clone(),
            TextStyleSpans = balloon.TextStyleSpans.Select(span => span.Clone()).ToList(),
            CustomShapePathData = balloon.CustomShapePathData,
            ComputedSize = balloon.ComputedSize,
            MaxTextWidth = balloon.MaxTextWidth,
            MaxTextHeight = balloon.MaxTextHeight,
            TextPath = balloon.TextPath?.Clone(),
            Tail = balloon.Tail != null ? ClipboardTail.FromTail(balloon.Tail) : null
        };
    }
}

internal sealed class ClipboardTail
{
    public Point2 TargetPoint { get; set; }
    public TailStyle Style { get; set; }
    public float BaseWidth { get; set; }

    public static ClipboardTail FromTail(Tail tail)
    {
        return new ClipboardTail
        {
            TargetPoint = tail.TargetPoint,
            Style = tail.Style,
            BaseWidth = tail.BaseWidth
        };
    }
}

internal sealed class FloatingImageClipboardItem
{
    public string? ImagePath { get; set; }
    public string? Source { get; set; }
    public Rect Bounds { get; set; }
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool ShadowEnabled { get; set; }
    public Color ShadowColor { get; set; } = Color.Black;
    public float ShadowOpacity { get; set; } = 0.35f;
    public float ShadowOffsetX { get; set; } = 4f;
    public float ShadowOffsetY { get; set; } = 4f;
    public float ShadowFalloff { get; set; } = 8f;
    public bool GlowEnabled { get; set; }
    public Color GlowColor { get; set; } = Color.Yellow;
    public float GlowOpacity { get; set; } = 0.5f;
    public float GlowSize { get; set; } = 6f;
    public Guid? LayerId { get; set; }
    public string? Name { get; set; }

    public static FloatingImageClipboardItem FromFloatingImage(FloatingImage image)
    {
        return new FloatingImageClipboardItem
        {
            ImagePath = image.ImagePath,
            Source = image.Source,
            Bounds = image.Bounds,
            Rotation = image.Rotation,
            Opacity = image.Opacity,
            IsVisible = image.IsVisible,
            IsLocked = image.IsLocked,
            ShadowEnabled = image.ShadowEnabled,
            ShadowColor = image.ShadowColor,
            ShadowOpacity = image.ShadowOpacity,
            ShadowOffsetX = image.ShadowOffsetX,
            ShadowOffsetY = image.ShadowOffsetY,
            ShadowFalloff = image.ShadowFalloff,
            GlowEnabled = image.GlowEnabled,
            GlowColor = image.GlowColor,
            GlowOpacity = image.GlowOpacity,
            GlowSize = image.GlowSize,
            LayerId = image.LayerId,
            Name = image.Name
        };
    }
}

internal sealed class PanelClipboardData
{
    public List<PanelClipboardItem> Panels { get; set; } = new();
}

internal sealed class PanelClipboardItem
{
    public Rect Bounds { get; set; }
    public PanelShape Shape { get; set; }
    public float CornerRadius { get; set; }
    public float SafeMargin { get; set; }
    public string? CustomShapePathData { get; set; }
    public Color BorderColor { get; set; }
    public float BorderWidth { get; set; }
    public PanelBorderStyle BorderStyle { get; set; }
    public string? ImagePath { get; set; }
    public PanelImagePlacement? ImagePlacement { get; set; }
    public bool IsImageVisibleInExport { get; set; }
    public float? GutterLeftOverride { get; set; }
    public float? GutterTopOverride { get; set; }
    public float? GutterRightOverride { get; set; }
    public float? GutterBottomOverride { get; set; }
    public float BleedLeft { get; set; }
    public float BleedTop { get; set; }
    public float BleedRight { get; set; }
    public float BleedBottom { get; set; }

    public static PanelClipboardItem FromPanel(PanelZone panel)
    {
        return new PanelClipboardItem
        {
            Bounds = panel.Bounds,
            Shape = panel.Shape,
            CornerRadius = panel.CornerRadius,
            SafeMargin = panel.SafeMargin,
            CustomShapePathData = panel.CustomShapePathData,
            BorderColor = panel.BorderColor,
            BorderWidth = panel.BorderWidth,
            BorderStyle = panel.BorderStyle,
            ImagePath = panel.ImagePath,
            ImagePlacement = panel.ImagePlacement,
            IsImageVisibleInExport = panel.IsImageVisibleInExport,
            GutterLeftOverride = panel.GutterLeftOverride,
            GutterTopOverride = panel.GutterTopOverride,
            GutterRightOverride = panel.GutterRightOverride,
            GutterBottomOverride = panel.GutterBottomOverride,
            BleedLeft = panel.BleedLeft,
            BleedTop = panel.BleedTop,
            BleedRight = panel.BleedRight,
            BleedBottom = panel.BleedBottom
        };
    }
}
