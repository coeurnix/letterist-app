using System.Linq;

namespace Letterist.Model;

public sealed class BalloonTemplate
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Category { get; private set; }
    public string PlaceholderText { get; private set; }
    public BalloonShape Shape { get; private set; }
    public string? CustomShapePathData { get; private set; }
    public BalloonStyle BalloonStyle { get; private set; }
    public Guid? BalloonStyleId { get; private set; }
    public BalloonStyleOverride BalloonStyleOverrides { get; private set; }
    public TextStyle TextStyle { get; private set; }
    public Guid? TextStyleId { get; private set; }
    public TextStyleOverride TextStyleOverrides { get; private set; }
    public BalloonTemplateTail? Tail { get; private set; }
    public bool IsFavorite { get; private set; }
    public int? HotkeySlot { get; private set; }
    public bool IsBuiltIn { get; private set; }

    private readonly List<string> _tags = new();
    public IReadOnlyList<string> Tags => _tags;

    public BalloonTemplate(
        Guid id,
        string name,
        BalloonShape shape,
        BalloonStyle balloonStyle,
        TextStyle textStyle,
        string? placeholderText = null,
        BalloonTemplateTail? tail = null,
        string? customShapePathData = null,
        Guid? balloonStyleId = null,
        BalloonStyleOverride? balloonStyleOverrides = null,
        Guid? textStyleId = null,
        TextStyleOverride? textStyleOverrides = null,
        string? description = null,
        IEnumerable<string>? tags = null,
        string? category = null,
        bool isFavorite = false,
        int? hotkeySlot = null,
        bool isBuiltIn = false)
    {
        Id = id;
        Name = NormalizeName(name);
        Description = NormalizeOptionalText(description);
        Category = NormalizeCategory(category);
        PlaceholderText = NormalizePlaceholderText(placeholderText);
        Shape = shape;
        CustomShapePathData = NormalizeOptionalText(customShapePathData);
        BalloonStyle = balloonStyle ?? BalloonStyle.Default;
        BalloonStyleId = balloonStyleId;
        BalloonStyleOverrides = (balloonStyleOverrides ?? BalloonStyleOverride.FromStyle(BalloonStyle)).Clone();
        TextStyle = textStyle ?? TextStyle.Default;
        TextStyleId = textStyleId;
        TextStyleOverrides = (textStyleOverrides ?? TextStyleOverride.FromStyle(TextStyle)).Clone();
        Tail = tail?.Clone();
        IsBuiltIn = isBuiltIn;
        SetTags(tags);
        SetHotkeySlot(hotkeySlot);
        SetFavorite(isFavorite || HotkeySlot.HasValue);
    }

    public static BalloonTemplate CreateFromBalloon(
        Balloon balloon,
        string name,
        string? description = null,
        IEnumerable<string>? tags = null,
        string? category = null,
        string? placeholderText = null,
        Guid? templateId = null,
        bool isFavorite = false,
        int? hotkeySlot = null,
        bool isBuiltIn = false)
    {
        if (balloon == null) throw new ArgumentNullException(nameof(balloon));

        var tail = balloon.Tail != null ? BalloonTemplateTail.FromTail(balloon.Tail, balloon.Position) : null;
        var placeholder = placeholderText ?? balloon.Text;

        return new BalloonTemplate(
            templateId ?? Guid.NewGuid(),
            name,
            balloon.Shape,
            balloon.BalloonStyle,
            balloon.TextStyle,
            placeholder,
            tail,
            balloon.CustomShapePathData,
            balloon.BalloonStyleId,
            balloon.BalloonStyleOverrides.Clone(),
            balloon.TextStyleId,
            balloon.TextStyleOverrides.Clone(),
            description,
            tags,
            category,
            isFavorite,
            hotkeySlot,
            isBuiltIn);
    }

    public BalloonTemplate Clone()
    {
        return new BalloonTemplate(
            Id,
            Name,
            Shape,
            BalloonStyle,
            TextStyle,
            PlaceholderText,
            Tail?.Clone(),
            CustomShapePathData,
            BalloonStyleId,
            BalloonStyleOverrides.Clone(),
            TextStyleId,
            TextStyleOverrides.Clone(),
            Description,
            _tags,
            Category,
            IsFavorite,
            HotkeySlot,
            IsBuiltIn);
    }

    public static IReadOnlyList<BalloonTemplate> CreateDefaults()
    {
        return new List<BalloonTemplate>
        {
            new(
                Guid.NewGuid(),
                "Dialogue",
                BalloonShape.Oval,
                BalloonStyle.Default,
                TextStyle.Default.With(allCaps: true),
                "Text",
                new BalloonTemplateTail(new Point2(0f, 120f), TailStyle.Pointer, 16f),
                category: "Speech",
                tags: new [] { "default", "speech" },
                isFavorite: true,
                hotkeySlot: 1,
                isBuiltIn: true),
            new(
                Guid.NewGuid(),
                "Shout",
                BalloonShape.Burst,
                BalloonStyle.Default.With(strokeWidth: 3.5f),
                TextStyle.Default.With(allCaps: true, bold: true, fontSize: 16f, tracking: 0.02f),
                "HEY!",
                new BalloonTemplateTail(new Point2(0f, 140f), TailStyle.Pointer, 20f),
                category: "Speech",
                tags: new [] { "emphasis", "action" },
                isFavorite: true,
                hotkeySlot: 2,
                isBuiltIn: true),
            new(
                Guid.NewGuid(),
                "Whisper",
                BalloonShape.Whisper,
                BalloonStyle.Default.With(strokeWidth: 1.2f, opacity: 0.95f),
                TextStyle.Default.With(allCaps: false, italic: true),
                "whisper...",
                new BalloonTemplateTail(new Point2(0f, 110f), TailStyle.Squiggly, 10f),
                category: "Speech",
                tags: new [] { "quiet", "soft" },
                isFavorite: true,
                hotkeySlot: 3,
                isBuiltIn: true),
            new(
                Guid.NewGuid(),
                "Thought",
                BalloonShape.Thought,
                BalloonStyle.Default.With(strokeWidth: 2f),
                TextStyle.Default.With(allCaps: false),
                "I wonder...",
                new BalloonTemplateTail(new Point2(0f, 100f), TailStyle.ThoughtBubbles, 14f),
                category: "Speech",
                tags: new [] { "thought", "internal" },
                isFavorite: true,
                hotkeySlot: 4,
                isBuiltIn: true),
            new(
                Guid.NewGuid(),
                "Caption",
                BalloonShape.Rectangle,
                BalloonStyle.Caption,
                TextStyle.Default.With(allCaps: false, alignment: TextAlignment.Left),
                "Narration",
                category: "Narration",
                tags: new [] { "caption", "box" },
                isBuiltIn: true),
            new(
                Guid.NewGuid(),
                "Radio",
                BalloonShape.Radio,
                BalloonStyle.Default.With(strokeWidth: 2.5f),
                TextStyle.Default.With(allCaps: true, tracking: 0.03f),
                "TRANSMISSION",
                new BalloonTemplateTail(new Point2(0f, 130f), TailStyle.Squiggly, 12f),
                category: "FX",
                tags: new [] { "electronic", "radio" },
                isBuiltIn: true)
        };
    }

    internal void SetFrom(BalloonTemplate template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (template.Id != Id)
        {
            throw new InvalidOperationException("Template IDs must match when updating.");
        }

        SetName(template.Name);
        SetDescription(template.Description);
        SetCategory(template.Category);
        SetPlaceholderText(template.PlaceholderText);
        SetContent(
            template.Shape,
            template.BalloonStyle,
            template.TextStyle,
            template.CustomShapePathData,
            template.BalloonStyleId,
            template.BalloonStyleOverrides,
            template.TextStyleId,
            template.TextStyleOverrides,
            template.Tail);
        SetTags(template.Tags);
        SetHotkeySlot(template.HotkeySlot);
        SetFavorite(template.IsFavorite || HotkeySlot.HasValue);
        SetBuiltIn(template.IsBuiltIn);
    }

    internal void SetName(string name)
    {
        Name = NormalizeName(name);
    }

    internal void SetDescription(string? description)
    {
        Description = NormalizeOptionalText(description);
    }

    internal void SetCategory(string? category)
    {
        Category = NormalizeCategory(category);
    }

    internal void SetPlaceholderText(string? text)
    {
        PlaceholderText = NormalizePlaceholderText(text);
    }

    internal void SetContent(
        BalloonShape shape,
        BalloonStyle balloonStyle,
        TextStyle textStyle,
        string? customShapePathData,
        Guid? balloonStyleId,
        BalloonStyleOverride? balloonStyleOverrides,
        Guid? textStyleId,
        TextStyleOverride? textStyleOverrides,
        BalloonTemplateTail? tail)
    {
        Shape = shape;
        BalloonStyle = balloonStyle ?? BalloonStyle.Default;
        TextStyle = textStyle ?? TextStyle.Default;
        CustomShapePathData = NormalizeOptionalText(customShapePathData);
        BalloonStyleId = balloonStyleId;
        BalloonStyleOverrides = (balloonStyleOverrides ?? BalloonStyleOverride.FromStyle(BalloonStyle)).Clone();
        TextStyleId = textStyleId;
        TextStyleOverrides = (textStyleOverrides ?? TextStyleOverride.FromStyle(TextStyle)).Clone();
        Tail = tail?.Clone();
    }

    internal void SetTags(IEnumerable<string>? tags)
    {
        _tags.Clear();
        _tags.AddRange(NormalizeTags(tags));
    }

    internal void SetFavorite(bool favorite)
    {
        IsFavorite = favorite;
        if (!favorite)
        {
            HotkeySlot = null;
        }
    }

    internal void SetHotkeySlot(int? hotkeySlot)
    {
        HotkeySlot = NormalizeHotkeySlot(hotkeySlot);
        if (HotkeySlot.HasValue)
        {
            IsFavorite = true;
        }
    }

    internal void SetBuiltIn(bool isBuiltIn)
    {
        IsBuiltIn = isBuiltIn;
    }

    private static string NormalizeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? "Balloon Template" : name.Trim();
    }

    private static string NormalizeCategory(string? category)
    {
        return string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
    }

    private static string NormalizePlaceholderText(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? "Text" : text.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static int? NormalizeHotkeySlot(int? slot)
    {
        if (!slot.HasValue) return null;
        return slot.Value is >= 1 and <= 9 ? slot : null;
    }

    private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in tags)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var tag = value.Trim();
            if (tag.Length == 0) continue;
            if (seen.Add(tag))
            {
                yield return tag;
            }
        }
    }
}

public sealed class BalloonTemplateTail
{
    public Point2 TargetOffset { get; }
    public TailStyle Style { get; }
    public float BaseWidth { get; }
    public Point2? AttachmentDirection { get; }
    public Point2? ControlPointOffset { get; }
    public float Curvature { get; }
    public float CurveCenter { get; }
    public float Inset { get; }

    public BalloonTemplateTail(
        Point2 targetOffset,
        TailStyle style = TailStyle.Pointer,
        float baseWidth = 16f,
        Point2? attachmentDirection = null,
        Point2? controlPointOffset = null,
        float curvature = 0.3f,
        float curveCenter = 0.5f,
        float inset = 0f)
    {
        TargetOffset = targetOffset;
        Style = style;
        BaseWidth = baseWidth;
        AttachmentDirection = attachmentDirection;
        ControlPointOffset = controlPointOffset;
        Curvature = Math.Clamp(curvature, -2f, 2f);
        CurveCenter = Math.Clamp(curveCenter, 0f, 1f);
        Inset = Math.Clamp(inset, 0f, 64f);
    }

    public static BalloonTemplateTail FromTail(Tail tail, Point2 balloonPosition)
    {
        if (tail == null) throw new ArgumentNullException(nameof(tail));
        var targetOffset = tail.TargetPoint - balloonPosition;
        Point2? controlOffset = tail.ControlPoint.HasValue
            ? tail.ControlPoint.Value - balloonPosition
            : null;

        return new BalloonTemplateTail(
            targetOffset,
            tail.Style,
            tail.BaseWidth,
            tail.AttachmentDirection,
            controlOffset,
            tail.Curvature,
            tail.CurveCenter,
            tail.Inset);
    }

    public Tail CreateTailAt(Point2 balloonPosition, Guid? tailId = null)
    {
        var target = balloonPosition + TargetOffset;
        var tail = new Tail(tailId ?? Guid.NewGuid(), target, Style, BaseWidth);
        tail.SetAttachmentDirection(AttachmentDirection);
        tail.SetControlPoint(ControlPointOffset.HasValue ? balloonPosition + ControlPointOffset.Value : null);
        tail.SetCurvature(Curvature);
        tail.SetCurveCenter(CurveCenter);
        tail.SetInset(Inset);
        return tail;
    }

    public BalloonTemplateTail Clone()
    {
        return new BalloonTemplateTail(TargetOffset, Style, BaseWidth, AttachmentDirection, ControlPointOffset, Curvature, CurveCenter, Inset);
    }
}
