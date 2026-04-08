using Letterist.Model;

namespace Letterist;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum UnitSystemPreference
{
    Pixels,
    Inches,
    Centimeters,
    Millimeters,
    Points,
    Picas
}

public enum SmartQuotesPreference
{
    Off,
    Straight,
    Typographer
}

public sealed class AppPreferences
{
    public int SchemaVersion { get; set; } = 1;
    public GeneralPreferences General { get; set; } = new();
    public UnitsPreferences Units { get; set; } = new();
    public CanvasPreferences Canvas { get; set; } = new();
    public TextDefaultsPreferences TextDefaults { get; set; } = new();
    public BalloonDefaultsPreferences BalloonDefaults { get; set; } = new();
    public TailDefaultsPreferences TailDefaults { get; set; } = new();
    public PanelDefaultsPreferences PanelDefaults { get; set; } = new();
    public ExportDefaultsPreferences ExportDefaults { get; set; } = new();
    public AutomationPreferences Automation { get; set; } = new();
    public KeyboardShortcutsPreferences KeyboardShortcuts { get; set; } = new();
    public PerformancePreferences Performance { get; set; } = new();

    public AppPreferences Clone()
    {
        return new AppPreferences
        {
            SchemaVersion = SchemaVersion,
            General = General.Clone(),
            Units = Units.Clone(),
            Canvas = Canvas.Clone(),
            TextDefaults = TextDefaults.Clone(),
            BalloonDefaults = BalloonDefaults.Clone(),
            TailDefaults = TailDefaults.Clone(),
            PanelDefaults = PanelDefaults.Clone(),
            ExportDefaults = ExportDefaults.Clone(),
            Automation = Automation.Clone(),
            KeyboardShortcuts = KeyboardShortcuts.Clone(),
            Performance = Performance.Clone()
        };
    }

    public void Normalize()
    {
        SchemaVersion = Math.Max(1, SchemaVersion);
        General ??= new GeneralPreferences();
        Units ??= new UnitsPreferences();
        Canvas ??= new CanvasPreferences();
        TextDefaults ??= new TextDefaultsPreferences();
        BalloonDefaults ??= new BalloonDefaultsPreferences();
        TailDefaults ??= new TailDefaultsPreferences();
        PanelDefaults ??= new PanelDefaultsPreferences();
        ExportDefaults ??= new ExportDefaultsPreferences();
        Automation ??= new AutomationPreferences();
        KeyboardShortcuts ??= new KeyboardShortcutsPreferences();
        Performance ??= new PerformancePreferences();

        General.Normalize();
        Units.Normalize();
        Canvas.Normalize();
        TextDefaults.Normalize();
        BalloonDefaults.Normalize();
        TailDefaults.Normalize();
        PanelDefaults.Normalize();
        ExportDefaults.Normalize();
        Automation.Normalize();
        KeyboardShortcuts.Normalize();
        Performance.Normalize();
    }

    public static AppPreferences CreateDefault() => new();
}

public sealed class GeneralPreferences
{
    public string Language { get; set; } = "";
    public bool IsLanguageExplicitlySet { get; set; } = false;
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public Color AccentColor { get; set; } = new(0, 120, 215, 255);
    public float DefaultPageWidth { get; set; } = 0f;
    public float DefaultPageHeight { get; set; } = 0f;
    public bool IsPageSizeExplicitlySet { get; set; } = false;
    public int DefaultDpi { get; set; } = 300;
    public int RecentFilesCount { get; set; } = 10;
    public int AutosaveIntervalSeconds { get; set; } = 30;
    public int BackupCount { get; set; } = 5;

    public GeneralPreferences Clone()
    {
        return new GeneralPreferences
        {
            Language = Language,
            IsLanguageExplicitlySet = IsLanguageExplicitlySet,
            Theme = Theme,
            AccentColor = AccentColor,
            DefaultPageWidth = DefaultPageWidth,
            DefaultPageHeight = DefaultPageHeight,
            IsPageSizeExplicitlySet = IsPageSizeExplicitlySet,
            DefaultDpi = DefaultDpi,
            RecentFilesCount = RecentFilesCount,
            AutosaveIntervalSeconds = AutosaveIntervalSeconds,
            BackupCount = BackupCount
        };
    }

    public void Normalize()
    {
        Language = UiLocalizationService.NormalizeLanguageTag(Language);
        DefaultPageWidth = Math.Clamp(DefaultPageWidth, 0f, 20000f);
        DefaultPageHeight = Math.Clamp(DefaultPageHeight, 0f, 20000f);
        DefaultDpi = Math.Clamp(DefaultDpi, 72, 2400);
        RecentFilesCount = Math.Clamp(RecentFilesCount, 1, 50);
        AutosaveIntervalSeconds = Math.Clamp(AutosaveIntervalSeconds, 5, 3600);
        BackupCount = Math.Clamp(BackupCount, 0, 100);
    }

    public static (float Width, float Height) GetDefaultPageSizeForLanguage(string language)
    {
        var normalizedLanguage = language?.ToLowerInvariant() ?? "en";

        if (normalizedLanguage.StartsWith("zh") || normalizedLanguage.StartsWith("ja") || normalizedLanguage.StartsWith("ko"))
        {
            return (2150f, 3035f);
        }

        if (normalizedLanguage.StartsWith("en"))
        {
            return (1988f, 3075f);
        }

        return (2480f, 3508f);
    }

    public (float Width, float Height) GetEffectivePageSize()
    {
        if (IsPageSizeExplicitlySet && DefaultPageWidth > 0 && DefaultPageHeight > 0)
        {
            return (DefaultPageWidth, DefaultPageHeight);
        }
        return GetDefaultPageSizeForLanguage(Language);
    }
}

public sealed class UnitsPreferences
{
    public UnitSystemPreference UnitSystem { get; set; } = UnitSystemPreference.Pixels;
    public bool ShowRulers { get; set; } = true;
    public bool ShowUnitsInProperties { get; set; } = true;
    public bool ShowUnitsInStatusBar { get; set; } = true;
    public bool EnableDpiConversion { get; set; } = true;
    public bool UseFractionalDisplay { get; set; } = false;
    public int FractionalPrecision { get; set; } = 2;

    public UnitsPreferences Clone()
    {
        return new UnitsPreferences
        {
            UnitSystem = UnitSystem,
            ShowRulers = ShowRulers,
            ShowUnitsInProperties = ShowUnitsInProperties,
            ShowUnitsInStatusBar = ShowUnitsInStatusBar,
            EnableDpiConversion = EnableDpiConversion,
            UseFractionalDisplay = UseFractionalDisplay,
            FractionalPrecision = FractionalPrecision
        };
    }

    public void Normalize()
    {
        FractionalPrecision = Math.Clamp(FractionalPrecision, 0, 6);
    }
}

public sealed class CanvasPreferences
{
    public Color WorkspaceBackgroundColor { get; set; } = new(60, 60, 60, 255);
    public Color CheckerboardLightColor { get; set; } = new(240, 240, 240, 255);
    public Color CheckerboardDarkColor { get; set; } = new(220, 220, 220, 255);
    public Color GridColor { get; set; } = new(100, 100, 100, 100);
    public Color GuideColor { get; set; } = new(0, 180, 255, 200);
    public bool ShowGrid { get; set; } = false;
    public bool SnapToGrid { get; set; } = false;
    public float GridMinorSpacing { get; set; } = 16f;
    public float GridMajorSpacing { get; set; } = 64f;
    public float HandleSize { get; set; } = 8f;
    public float ZoomStepPercent { get; set; } = 10f;
    public float ScrollSpeed { get; set; } = 1f;

    public CanvasPreferences Clone()
    {
        return new CanvasPreferences
        {
            WorkspaceBackgroundColor = WorkspaceBackgroundColor,
            CheckerboardLightColor = CheckerboardLightColor,
            CheckerboardDarkColor = CheckerboardDarkColor,
            GridColor = GridColor,
            GuideColor = GuideColor,
            ShowGrid = ShowGrid,
            SnapToGrid = SnapToGrid,
            GridMinorSpacing = GridMinorSpacing,
            GridMajorSpacing = GridMajorSpacing,
            HandleSize = HandleSize,
            ZoomStepPercent = ZoomStepPercent,
            ScrollSpeed = ScrollSpeed
        };
    }

    public void Normalize()
    {
        GridMinorSpacing = Math.Clamp(GridMinorSpacing, 1f, 2000f);
        GridMajorSpacing = Math.Clamp(GridMajorSpacing, GridMinorSpacing, 20000f);
        HandleSize = Math.Clamp(HandleSize, 4f, 24f);
        ZoomStepPercent = Math.Clamp(ZoomStepPercent, 1f, 100f);
        ScrollSpeed = Math.Clamp(ScrollSpeed, 0.1f, 8f);
    }
}

public sealed class TextDefaultsPreferences
{
    public string FontFamily { get; set; } = TextStyle.Default.FontFamily;
    public float FontSize { get; set; } = TextStyle.Default.FontSize;
    public Color TextColor { get; set; } = TextStyle.Default.TextColor;
    public float Tracking { get; set; } = TextStyle.Default.Tracking;
    public float LineHeight { get; set; } = TextStyle.Default.LineHeight;
    public TextAlignment Alignment { get; set; } = TextStyle.Default.Alignment;
    public bool AllCaps { get; set; } = TextStyle.Default.AllCaps;
    public bool Bold { get; set; } = TextStyle.Default.Bold;
    public bool Italic { get; set; } = TextStyle.Default.Italic;
    public SmartQuotesPreference SmartQuotesStyle { get; set; } = SmartQuotesPreference.Typographer;

    public TextDefaultsPreferences Clone()
    {
        return new TextDefaultsPreferences
        {
            FontFamily = FontFamily,
            FontSize = FontSize,
            TextColor = TextColor,
            Tracking = Tracking,
            LineHeight = LineHeight,
            Alignment = Alignment,
            AllCaps = AllCaps,
            Bold = Bold,
            Italic = Italic,
            SmartQuotesStyle = SmartQuotesStyle
        };
    }

    public void Normalize()
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? TextStyle.Default.FontFamily : FontFamily.Trim();
        FontSize = Math.Clamp(FontSize, 4f, 500f);
        Tracking = Math.Clamp(Tracking, -1f, 1f);
        LineHeight = Math.Clamp(LineHeight, 0.5f, 5f);
    }

    public TextStyle ToTextStyle()
    {
        return TextStyle.Default.With(
            fontFamily: FontFamily,
            fontSize: FontSize,
            textColor: TextColor,
            tracking: Tracking,
            lineHeight: LineHeight,
            alignment: Alignment,
            allCaps: AllCaps,
            bold: Bold,
            italic: Italic);
    }
}

public sealed class BalloonDefaultsPreferences
{
    public BalloonShape Shape { get; set; } = BalloonShape.Oval;
    public Color FillColor { get; set; } = BalloonStyle.Default.FillColor;
    public Color StrokeColor { get; set; } = BalloonStyle.Default.StrokeColor;
    public float StrokeWidth { get; set; } = BalloonStyle.Default.StrokeWidth;
    public float PaddingHorizontal { get; set; } = 12f;
    public float PaddingVertical { get; set; } = 8f;
    public float CornerRadius { get; set; } = BalloonStyle.Default.CornerRadius;
    public float Opacity { get; set; } = BalloonStyle.Default.Opacity;

    public BalloonDefaultsPreferences Clone()
    {
        return new BalloonDefaultsPreferences
        {
            Shape = Shape,
            FillColor = FillColor,
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            PaddingHorizontal = PaddingHorizontal,
            PaddingVertical = PaddingVertical,
            CornerRadius = CornerRadius,
            Opacity = Opacity
        };
    }

    public void Normalize()
    {
        StrokeWidth = Math.Clamp(StrokeWidth, 0f, 100f);
        PaddingHorizontal = Math.Clamp(PaddingHorizontal, 0f, 200f);
        PaddingVertical = Math.Clamp(PaddingVertical, 0f, 200f);
        CornerRadius = Math.Clamp(CornerRadius, 0f, 300f);
        Opacity = Math.Clamp(Opacity, 0f, 1f);
    }

    public BalloonStyle ToBalloonStyle()
    {
        return BalloonStyle.Default.With(
            fillColor: FillColor,
            strokeColor: StrokeColor,
            strokeWidth: StrokeWidth,
            opacity: Opacity,
            cornerRadius: CornerRadius,
            paddingLeft: PaddingHorizontal,
            paddingRight: PaddingHorizontal,
            paddingTop: PaddingVertical,
            paddingBottom: PaddingVertical);
    }
}

public sealed class TailDefaultsPreferences
{
    public TailStyle Style { get; set; } = TailStyle.Pointer;
    public float Width { get; set; } = 16f;
    public float Curve { get; set; } = 0.3f;

    public TailDefaultsPreferences Clone()
    {
        return new TailDefaultsPreferences
        {
            Style = Style,
            Width = Width,
            Curve = Curve
        };
    }

    public void Normalize()
    {
        Width = Math.Clamp(Width, 1f, 200f);
        Curve = Math.Clamp(Curve, -1f, 1f);
    }
}

public sealed class PanelDefaultsPreferences
{
    public float Gutter { get; set; } = 10f;
    public float Margin { get; set; } = 0f;
    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;
    public Color BorderColor { get; set; } = PanelZone.DefaultBorderColor;
    public float BorderWidth { get; set; } = PanelZone.DefaultBorderWidth;
    public PanelBorderStyle BorderStyle { get; set; } = PanelBorderStyle.Solid;
    public string? PanelTemplateStorageFolder { get; set; }

    public PanelDefaultsPreferences Clone()
    {
        return new PanelDefaultsPreferences
        {
            Gutter = Gutter,
            Margin = Margin,
            ReadingDirection = ReadingDirection,
            BorderColor = BorderColor,
            BorderWidth = BorderWidth,
            BorderStyle = BorderStyle,
            PanelTemplateStorageFolder = PanelTemplateStorageFolder
        };
    }

    public void Normalize()
    {
        Gutter = Math.Clamp(Gutter, 0f, 500f);
        Margin = Math.Clamp(Margin, 0f, 500f);
        BorderWidth = Math.Clamp(BorderWidth, 0f, 100f);
        PanelTemplateStorageFolder = string.IsNullOrWhiteSpace(PanelTemplateStorageFolder)
            ? null
            : PanelTemplateStorageFolder.Trim();
    }
}

public sealed class ExportDefaultsPreferences
{
    public string Format { get; set; } = "png";
    public int Dpi { get; set; } = 300;
    public int Quality { get; set; } = 90;
    public bool Transparent { get; set; } = false;
    public bool OverlayOnly { get; set; } = false;
    public bool SelectionOnly { get; set; } = false;
    public bool VisibleLayersOnly { get; set; } = true;
    public bool DrawPanelBorders { get; set; } = true;
    public bool IncludeMetadata { get; set; } = false;
    public bool BatchExport { get; set; } = false;
    public bool CurrentPageOnly { get; set; } = false;
    public bool PerLayerExport { get; set; } = false;
    public bool ExportAllLanguages { get; set; } = false;
    public bool ExportVisibleLanguagesOnly { get; set; } = true;
    public bool PerLanguageFolders { get; set; } = true;
    public bool IncludeLanguageCode { get; set; } = false;
    public int PageNumberStart { get; set; } = 1;
    public int PageNumberPadding { get; set; } = 2;
    public string DefaultFolder { get; set; } = "";
    public string FilenamePattern { get; set; } = "{document}-page-{page}";
    public string RarExecutablePath { get; set; } = "";

    public ExportDefaultsPreferences Clone()
    {
        return new ExportDefaultsPreferences
        {
            Format = Format,
            Dpi = Dpi,
            Quality = Quality,
            Transparent = Transparent,
            OverlayOnly = OverlayOnly,
            SelectionOnly = SelectionOnly,
            VisibleLayersOnly = VisibleLayersOnly,
            DrawPanelBorders = DrawPanelBorders,
            IncludeMetadata = IncludeMetadata,
            BatchExport = BatchExport,
            CurrentPageOnly = CurrentPageOnly,
            PerLayerExport = PerLayerExport,
            ExportAllLanguages = ExportAllLanguages,
            ExportVisibleLanguagesOnly = ExportVisibleLanguagesOnly,
            PerLanguageFolders = PerLanguageFolders,
            IncludeLanguageCode = IncludeLanguageCode,
            PageNumberStart = PageNumberStart,
            PageNumberPadding = PageNumberPadding,
            DefaultFolder = DefaultFolder,
            FilenamePattern = FilenamePattern,
            RarExecutablePath = RarExecutablePath
        };
    }

    public void Normalize()
    {
        Format = string.IsNullOrWhiteSpace(Format) ? "png" : Format.Trim().ToLowerInvariant();
        Dpi = Math.Clamp(Dpi, 72, 2400);
        Quality = Math.Clamp(Quality, 1, 100);
        PageNumberStart = Math.Max(0, PageNumberStart);
        PageNumberPadding = Math.Clamp(PageNumberPadding, 1, 8);
        DefaultFolder = DefaultFolder?.Trim() ?? string.Empty;
        FilenamePattern = string.IsNullOrWhiteSpace(FilenamePattern) ? "{document}-page-{page}" : FilenamePattern.Trim();
        RarExecutablePath = RarExecutablePath?.Trim() ?? string.Empty;
    }
}

public sealed class AutomationPreferences
{
    public bool EnabledByDefault { get; set; }
    public int Port { get; set; } = 9221;
    public bool LocalhostOnly { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "";

    public AutomationPreferences Clone()
    {
        return new AutomationPreferences
        {
            EnabledByDefault = EnabledByDefault,
            Port = Port,
            LocalhostOnly = LocalhostOnly,
            EnableLogging = EnableLogging,
            LogFilePath = LogFilePath
        };
    }

    public void Normalize()
    {
        Port = Math.Clamp(Port, 1, 65535);
        LogFilePath = LogFilePath?.Trim() ?? string.Empty;
    }
}

public sealed class KeyboardShortcutsPreferences
{
    public Dictionary<string, string> Bindings { get; set; } = CreateDefaultBindings();

    public KeyboardShortcutsPreferences Clone()
    {
        return new KeyboardShortcutsPreferences
        {
            Bindings = new Dictionary<string, string>(Bindings, StringComparer.OrdinalIgnoreCase)
        };
    }

    public void Normalize()
    {
        Bindings ??= CreateDefaultBindings();
        if (Bindings.Count == 0)
        {
            Bindings = CreateDefaultBindings();
            return;
        }

        var defaults = CreateDefaultBindings();
        foreach (var (command, binding) in defaults)
        {
            if (!Bindings.TryGetValue(command, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                Bindings[command] = binding;
            }
        }

        if (Bindings.TryGetValue("Zoom to Selection", out var zoomSelectionBinding) &&
            string.Equals(zoomSelectionBinding.Trim(), "Ctrl+Shift+0", StringComparison.OrdinalIgnoreCase))
        {
            Bindings["Zoom to Selection"] = "Ctrl+1";
        }
    }

    public static Dictionary<string, string> CreateDefaultBindings()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Preferences"] = "Ctrl+,",
            ["Undo"] = "Ctrl+Z",
            ["Redo"] = "Ctrl+Y",
            ["Copy"] = "Ctrl+C",
            ["Cut"] = "Ctrl+X",
            ["Paste"] = "Ctrl+V",
            ["Duplicate"] = "Ctrl+D",
            ["Select All"] = "Ctrl+A",
            ["Zoom 100%"] = "Ctrl+0",
            ["Zoom to Selection"] = "Ctrl+1",
            ["Toggle Fullscreen Canvas"] = "Tab",
            ["Toggle Panel Layout"] = "P",
            ["Create Balloon"] = "B",
            ["Create SFX"] = "S",
            ["Select Tool"] = "V",
            ["Toggle Tail"] = "T",
            ["Add Page"] = "Ctrl+Alt+N",
            ["Add Horizontal Guide"] = "Ctrl+Alt+H",
            ["Add Vertical Guide"] = "Ctrl+Alt+V",
            ["Toggle Grid"] = "Ctrl+G",
            ["Toggle Snap to Grid"] = "Ctrl+Shift+G",
            ["Add Image"] = "I"
        };
    }
}

public sealed class PerformancePreferences
{
    public int UndoLimit { get; set; } = 100;
    public int ThumbnailCacheMb { get; set; } = 256;
    public bool HardwareAcceleration { get; set; } = true;
    public bool BackgroundRendering { get; set; } = true;
    public int MemoryLimitMb { get; set; } = 2048;

    public PerformancePreferences Clone()
    {
        return new PerformancePreferences
        {
            UndoLimit = UndoLimit,
            ThumbnailCacheMb = ThumbnailCacheMb,
            HardwareAcceleration = HardwareAcceleration,
            BackgroundRendering = BackgroundRendering,
            MemoryLimitMb = MemoryLimitMb
        };
    }

    public void Normalize()
    {
        UndoLimit = Math.Clamp(UndoLimit, 10, 5000);
        ThumbnailCacheMb = Math.Clamp(ThumbnailCacheMb, 32, 8192);
        MemoryLimitMb = Math.Clamp(MemoryLimitMb, 256, 65536);
    }
}
