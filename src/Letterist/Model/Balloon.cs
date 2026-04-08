namespace Letterist.Model;

public sealed class Balloon
{
    public Guid Id { get; }

    public Guid LayerId { get; private set; }

    public Guid? PanelId { get; private set; }

    public bool ConstrainToPanel { get; private set; }

    public bool IsVisible { get; private set; }

    public bool IsLocked { get; private set; }

    public Point2 Position { get; private set; }

    public BalloonShape Shape { get; private set; }

    public BalloonStyle BalloonStyle { get; private set; }
    public Guid? BalloonStyleId { get; private set; }
    public BalloonStyleOverride BalloonStyleOverrides { get; private set; }

    public string Text { get; private set; }

    public TextStyle TextStyle { get; private set; }
    public Guid? TextStyleId { get; private set; }
    public TextStyleOverride TextStyleOverrides { get; private set; }

    private readonly Dictionary<string, BalloonTranslation> _translations = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, BalloonTranslation> Translations => _translations;

    private readonly List<TextStyleSpan> _textStyleSpans = new();

    public IReadOnlyList<TextStyleSpan> TextStyleSpans => _textStyleSpans;

    public string? CustomShapePathData { get; private set; }

    public TextPath? TextPath { get; private set; }

    private readonly List<Tail> _tails = new();

    public IReadOnlyList<Tail> Tails => _tails;

    public Tail? Tail => _tails.Count > 0 ? _tails[0] : null;

    public Size2 ComputedSize { get; private set; }

    public float? MaxTextWidth { get; private set; }

    public float? MaxTextHeight { get; private set; }

    public float Rotation { get; private set; }

    public Balloon(
        Guid id,
        Guid layerId,
        Point2 position,
        BalloonShape shape,
        BalloonStyle balloonStyle,
        string text,
        TextStyle textStyle,
        IEnumerable<TextStyleSpan>? textStyleSpans = null,
        string? customShapePathData = null,
        Guid? panelId = null,
        bool constrainToPanel = false,
        bool isVisible = true,
        bool isLocked = false,
        TextPath? textPath = null,
        IReadOnlyDictionary<string, BalloonTranslation>? translations = null)
    {
        Id = id;
        LayerId = layerId;
        PanelId = panelId;
        ConstrainToPanel = constrainToPanel;
        IsVisible = isVisible;
        IsLocked = isLocked;
        Position = position;
        Shape = shape;
        BalloonStyle = balloonStyle;
        BalloonStyleOverrides = BalloonStyleOverride.FromStyle(balloonStyle);
        Text = text;
        TextStyle = textStyle;
        TextStyleOverrides = TextStyleOverride.FromStyle(textStyle);
        if (textStyleSpans != null)
        {
            _textStyleSpans.AddRange(textStyleSpans.Select(span => span.Clone()));
        }
        CustomShapePathData = customShapePathData;
        TextPath = textPath?.Clone();
        if (translations != null)
        {
            foreach (var pair in translations)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                _translations[pair.Key.Trim()] = pair.Value;
            }
        }
        ComputedSize = new Size2(balloonStyle.MinWidth, balloonStyle.MinHeight);
    }

    public static Balloon Create(
        Guid layerId,
        Point2 position,
        string text = "",
        BalloonShape shape = BalloonShape.Oval,
        BalloonStyle? balloonStyle = null,
        TextStyle? textStyle = null,
        Guid? panelId = null,
        bool constrainToPanel = false)
    {
        return new Balloon(
            Guid.NewGuid(),
            layerId,
            position,
            shape,
            balloonStyle ?? BalloonStyle.Default,
            text,
            textStyle ?? TextStyle.Default,
            panelId: panelId,
            constrainToPanel: constrainToPanel);
    }

    public Rect Bounds => Rect.FromCenterSize(Position, ComputedSize);

    public Rect TextBounds
    {
        get
        {
            var bounds = Bounds;

            const float ellipseInscribeFactor = 0.72f;

            const float burstInscribeFactor = 0.60f;

            float roundedRectFactor = 1f;
            if (Shape == BalloonShape.RoundedRect && BalloonStyle.CornerRadius > 0)
            {
                var cornerImpact = MathF.Min(BalloonStyle.CornerRadius / MathF.Min(bounds.Width, bounds.Height), 0.15f);
                roundedRectFactor = 1f - cornerImpact * 0.3f;
            }

            float inscribeFactor = Shape switch
            {
                BalloonShape.Oval or BalloonShape.Thought or BalloonShape.Splat or BalloonShape.Whisper => ellipseInscribeFactor,
                BalloonShape.Burst => burstInscribeFactor,
                BalloonShape.RoundedRect => roundedRectFactor,
                BalloonShape.None => 1f, // No balloon shape - text uses full bounds
                _ => 1f // Rectangle, Radio, Custom use full bounds
            };

            if (inscribeFactor < 1f)
            {
                var inscribedWidth = bounds.Width * inscribeFactor;
                var inscribedHeight = bounds.Height * inscribeFactor;
                var centerX = bounds.X + bounds.Width / 2f;
                var centerY = bounds.Y + bounds.Height / 2f;

                var textWidth = inscribedWidth - BalloonStyle.PaddingLeft - BalloonStyle.PaddingRight;
                var textHeight = inscribedHeight - BalloonStyle.PaddingTop - BalloonStyle.PaddingBottom;

                textWidth = MathF.Max(textWidth, 40f);
                textHeight = MathF.Max(textHeight, 16f);

                return new Rect(
                    centerX - inscribedWidth / 2f + BalloonStyle.PaddingLeft,
                    centerY - inscribedHeight / 2f + BalloonStyle.PaddingTop,
                    textWidth,
                    textHeight);
            }

            var rectTextWidth = bounds.Width - BalloonStyle.PaddingLeft - BalloonStyle.PaddingRight;
            var rectTextHeight = bounds.Height - BalloonStyle.PaddingTop - BalloonStyle.PaddingBottom;

            rectTextWidth = MathF.Max(rectTextWidth, 40f);
            rectTextHeight = MathF.Max(rectTextHeight, 16f);

            return new Rect(
                bounds.X + BalloonStyle.PaddingLeft,
                bounds.Y + BalloonStyle.PaddingTop,
                rectTextWidth,
                rectTextHeight);
        }
    }

    public Balloon Clone()
    {
        var clone = new Balloon(Id, LayerId, Position, Shape, BalloonStyle, Text, TextStyle, _textStyleSpans, CustomShapePathData, PanelId, ConstrainToPanel)
        {
            ComputedSize = ComputedSize,
            MaxTextWidth = MaxTextWidth,
            Rotation = Rotation,
            TextPath = TextPath?.Clone(),
            BalloonStyleId = BalloonStyleId,
            BalloonStyleOverrides = BalloonStyleOverrides.Clone(),
            TextStyleId = TextStyleId,
            TextStyleOverrides = TextStyleOverrides.Clone(),
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
        clone.SetTranslations(_translations);
        foreach (var tail in _tails)
        {
            clone._tails.Add(tail.Clone());
        }
        return clone;
    }

    public Balloon CloneWithNewId()
    {
        var clone = new Balloon(Guid.NewGuid(), LayerId, Position, Shape, BalloonStyle, Text, TextStyle, _textStyleSpans, CustomShapePathData, PanelId, ConstrainToPanel)
        {
            ComputedSize = ComputedSize,
            MaxTextWidth = MaxTextWidth,
            Rotation = Rotation,
            TextPath = TextPath?.Clone(),
            BalloonStyleId = BalloonStyleId,
            BalloonStyleOverrides = BalloonStyleOverrides.Clone(),
            TextStyleId = TextStyleId,
            TextStyleOverrides = TextStyleOverrides.Clone(),
            IsVisible = IsVisible,
            IsLocked = IsLocked
        };
        clone.SetTranslations(_translations);
        foreach (var tail in _tails)
        {
            clone._tails.Add(tail.CloneWithNewId());
        }
        return clone;
    }

    public Tail? FindTail(Guid tailId) => _tails.FirstOrDefault(t => t.Id == tailId);

    internal void SetLayerId(Guid layerId) => LayerId = layerId;
    internal void SetPanelId(Guid? panelId) => PanelId = panelId;
    internal void SetConstrainToPanel(bool constrain) => ConstrainToPanel = constrain;
    internal void SetVisible(bool visible) => IsVisible = visible;
    internal void SetLocked(bool locked) => IsLocked = locked;
    internal void SetPosition(Point2 position) => Position = position;
    internal void SetShape(BalloonShape shape) => Shape = shape;
    internal void SetBalloonStyle(BalloonStyle style) => BalloonStyle = style;
    internal void SetBalloonStyleReference(Guid? styleId, BalloonStyleOverride overrides, BalloonStyle resolvedStyle)
    {
        BalloonStyleId = styleId;
        BalloonStyleOverrides = overrides ?? BalloonStyleOverride.Empty;
        BalloonStyle = resolvedStyle;
    }
    internal void SetText(string text) => Text = text;
    internal void SetTextStyle(TextStyle style) => TextStyle = style;
    internal void SetTextStyleReference(Guid? styleId, TextStyleOverride overrides, TextStyle resolvedStyle)
    {
        TextStyleId = styleId;
        TextStyleOverrides = overrides ?? TextStyleOverride.Empty;
        TextStyle = resolvedStyle;
    }
    internal void SetTextStyleSpans(IEnumerable<TextStyleSpan> spans)
    {
        _textStyleSpans.Clear();
        _textStyleSpans.AddRange(spans.Select(span => span.Clone()));
    }
    internal void ClearTextStyleSpans() => _textStyleSpans.Clear();
    internal void SetComputedSize(Size2 size) => ComputedSize = size;
    internal void SetMaxTextWidth(float? width) => MaxTextWidth = width;

    internal void SetMaxTextHeight(float? height) => MaxTextHeight = height;
    internal void SetRotation(float rotation) => Rotation = rotation;
    internal void SetCustomShapePathData(string? data) => CustomShapePathData = data;
    internal void SetTextPath(TextPath? textPath) => TextPath = textPath?.Clone();
    internal void SetTranslation(string language, BalloonTranslation translation) => _translations[language] = translation;
    internal bool TryGetTranslation(string language, out BalloonTranslation translation) => _translations.TryGetValue(language, out translation);
    internal bool RemoveTranslation(string language) => _translations.Remove(language);
    internal void SetTranslations(IReadOnlyDictionary<string, BalloonTranslation> translations)
    {
        _translations.Clear();
        foreach (var pair in translations)
        {
            _translations[pair.Key] = pair.Value;
        }
    }
    internal void ClearTranslations() => _translations.Clear();

    internal void AddTail(Tail tail) => _tails.Add(tail);
    internal bool RemoveTail(Guid tailId)
    {
        var tail = FindTail(tailId);
        return tail != null && _tails.Remove(tail);
    }
    internal void ClearTails() => _tails.Clear();

    internal void SetTail(Tail? tail)
    {
        _tails.Clear();
        if (tail != null)
        {
            _tails.Add(tail);
        }
    }
}
