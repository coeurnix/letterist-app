namespace Letterist.Model;

internal static class BalloonStyleCatalog
{
    public static IReadOnlyList<NamedBalloonStyle> CreateBuiltInStyles()
    {
        return new List<NamedBalloonStyle>
        {
            Named(
                "2C59084A-FA7C-4707-AB4B-E9EA04CE2A7F",
                "Default Dialogue",
                BalloonStyle.Default,
                BalloonShape.Oval,
                TextStyle.Default.With(allCaps: true, lineHeight: 1.16f),
                false,
                true,
                Tail(0f, 118f, TailStyle.Pointer, 16f, attachX: 0f, attachY: 1f)),
            Named(
                "A5CCB2FC-EA2A-4450-A5CA-52EBFD7AE95A",
                "Narration Caption",
                BalloonStyle.Default.With(
                    fillColor: C(255, 249, 222),
                    strokeColor: C(35, 35, 35),
                    strokeWidth: 2.2f,
                    cornerRadius: 2f,
                    paddingLeft: 10f,
                    paddingTop: 7f,
                    paddingRight: 10f,
                    paddingBottom: 7f),
                BalloonShape.Rectangle,
                TextStyle.Default.With(allCaps: false, alignment: TextAlignment.Left, lineHeight: 1.12f),
                constrainToPanel: true),
            Named(
                "C1A0D173-35E0-4CA2-A7D1-91590B9690D6",
                "Whisper Light",
                BalloonStyle.Default.With(
                    fillColor: C(245, 248, 250),
                    strokeColor: C(92, 105, 120),
                    strokeWidth: 1.6f,
                    opacity: 0.92f),
                BalloonShape.Whisper,
                TextStyle.Default.With(allCaps: false, italic: true, fontSize: 13f, tracking: 0.01f, lineHeight: 1.18f),
                false,
                true,
                Tail(0f, 110f, TailStyle.Squiggly, 10f, attachX: 0f, attachY: 1f, curvature: 0.42f)),
            Named(
                "EA0D3DA1-917F-4E55-A7F8-B9506F93B3E2",
                "Radio Tech",
                BalloonStyle.Default.With(
                    fillColor: C(232, 245, 255),
                    strokeColor: C(35, 70, 120),
                    strokeWidth: 2.4f,
                    patternEnabled: true,
                    patternType: TextFillPattern.Crosshatch,
                    patternSecondaryColor: C(252, 252, 252),
                    patternScale: 1.1f),
                BalloonShape.Radio,
                TextStyle.Default.With(allCaps: true, tracking: 0.035f, fontSize: 13.5f),
                false,
                true,
                Tail(0f, 120f, TailStyle.Squiggly, 12f, attachX: 0f, attachY: 1f, curvature: -0.35f)),
            Named(
                "47CA92BA-F4EC-45DA-9A8C-037E4D440FBC",
                "Shout Impact",
                BalloonStyle.Default.With(
                    fillColor: C(255, 246, 226),
                    strokeColor: Color.Black,
                    strokeWidth: 4.2f,
                    glowEnabled: true,
                    glowColor: C(255, 214, 86),
                    glowOpacity: 0.36f,
                    glowSize: 5f),
                BalloonShape.Burst,
                TextStyle.Default.With(allCaps: true, bold: true, fontSize: 16f, tracking: 0.03f, lineHeight: 1.06f),
                false,
                true,
                Tail(0f, 136f, TailStyle.Pointer, 22f, attachX: 0f, attachY: 1f)),
            Named(
                "58D1C8AE-E0C8-4DDE-B1CA-45D2E858EBAF",
                "Thought Soft",
                BalloonStyle.Default.With(
                    fillColor: C(248, 248, 248),
                    strokeColor: C(128, 128, 128),
                    strokeWidth: 1.8f,
                    shadowEnabled: true,
                    shadowOpacity: 0.22f,
                    shadowOffsetX: 2f,
                    shadowOffsetY: 2f,
                    shadowFalloff: 5f),
                BalloonShape.Thought,
                TextStyle.Default.With(allCaps: false, lineHeight: 1.14f),
                false,
                true,
                Tail(6f, 96f, TailStyle.ThoughtBubbles, 14f, attachX: 0.08f, attachY: 1f)),
            Named(
                "EBC0B255-E6C5-42D7-BD96-EE3D08686AA7",
                "Flashback Sepia",
                BalloonStyle.Default.With(
                    fillColor: C(250, 236, 205),
                    strokeColor: C(120, 82, 52),
                    strokeWidth: 2f,
                    gradientEnabled: true,
                    gradientStartColor: C(255, 245, 220),
                    gradientEndColor: C(234, 210, 170),
                    gradientType: BalloonGradientType.Linear,
                    gradientAngle: 90f),
                BalloonShape.RoundedRect,
                TextStyle.Default.With(allCaps: false, italic: true, tracking: 0.01f),
                false,
                true,
                Tail(-12f, 122f, TailStyle.Curved, 16f, attachX: -0.1f, attachY: 1f, controlX: -36f, controlY: 70f, curvature: -0.12f)),
            Named(
                "39DFB4E1-29C2-45FC-86A2-5A3C93CC4A12",
                "Dark Monologue",
                BalloonStyle.Default.With(
                    fillColor: C(45, 45, 50),
                    strokeColor: C(220, 220, 220),
                    strokeWidth: 2.2f,
                    opacity: 0.95f),
                BalloonShape.Oval,
                TextStyle.Default.With(
                    allCaps: false,
                    textColor: C(242, 242, 248),
                    outlineColor: C(12, 12, 14),
                    outlineWidth: 1.2f,
                    lineHeight: 1.16f),
                false,
                true,
                Tail(0f, 120f, TailStyle.Curved, 14f, attachX: 0f, attachY: 1f, controlX: 26f, controlY: 78f, curvature: 0.22f)),
            Named(
                "69D8BEB4-B5A8-4A20-9313-49FA89A48AF1",
                "SFX Outline",
                BalloonStyle.Default.With(
                    fillColor: Color.Transparent,
                    strokeColor: Color.Black,
                    strokeWidth: 5.2f),
                BalloonShape.None,
                TextStyle.TextOnlyDefault.With(
                    textColor: C(250, 244, 220),
                    outlineColor: C(22, 22, 24),
                    outlineWidth: 6f,
                    tracking: 0.04f,
                    lineHeight: 1f)),
            Named(
                "F9096455-6D2D-4532-9600-423A88D5F86F",
                "Emphasis Red",
                BalloonStyle.Default.With(
                    fillColor: C(255, 232, 232),
                    strokeColor: C(140, 25, 25),
                    strokeWidth: 2.8f),
                BalloonShape.Burst,
                TextStyle.Default.With(allCaps: true, bold: true, tracking: 0.02f),
                false,
                true,
                Tail(0f, 128f, TailStyle.Pointer, 19f, attachX: 0f, attachY: 1f)),
            Named(
                "9D461A4D-E447-46FA-8CC2-3D34A4B7310F",
                "Sticky Note",
                BalloonStyle.Default.With(
                    fillColor: C(255, 250, 166),
                    strokeColor: C(110, 90, 30),
                    strokeWidth: 1.8f,
                    cornerRadius: 4f,
                    paddingLeft: 10f,
                    paddingTop: 6f,
                    paddingRight: 10f,
                    paddingBottom: 6f),
                BalloonShape.RoundedRect,
                TextStyle.Default.With(allCaps: false, alignment: TextAlignment.Left, lineHeight: 1.1f),
                constrainToPanel: true),
            Named(
                "91B85DD5-A74C-42A8-9E77-2C5FF7DFB8D9",
                "Dream Glow",
                BalloonStyle.Default.With(
                    fillColor: C(236, 244, 255),
                    strokeColor: C(94, 112, 150),
                    strokeWidth: 2f,
                    glowEnabled: true,
                    glowColor: C(173, 222, 255),
                    glowOpacity: 0.45f,
                    glowSize: 6f),
                BalloonShape.Thought,
                TextStyle.Default.With(allCaps: false, italic: true, fontSize: 13f),
                false,
                true,
                Tail(-8f, 98f, TailStyle.ThoughtBubbles, 13f, attachX: -0.08f, attachY: 1f)),

            Named(
                "20BC0A2E-6C84-4D8A-9B48-3B3570E58D52",
                "Manga Dialogue Soft",
                BalloonStyle.Default.With(
                    fillColor: C(255, 255, 255),
                    strokeColor: C(28, 28, 32),
                    strokeWidth: 1.7f,
                    paddingLeft: 13f,
                    paddingTop: 8f,
                    paddingRight: 13f,
                    paddingBottom: 8f),
                BalloonShape.Oval,
                TextStyle.Default.With(allCaps: false, fontSize: 13f, tracking: 0.006f, lineHeight: 1.22f),
                false,
                false,
                Tail(0f, 122f, TailStyle.Pointer, 14f, attachX: 0f, attachY: 1f)),
            Named(
                "67382F4B-29D6-44A8-B8F6-53C4A1E6A972",
                "Manga Whisper Breath",
                BalloonStyle.Default.With(
                    fillColor: C(250, 250, 252),
                    strokeColor: C(132, 138, 148),
                    strokeWidth: 1.4f,
                    opacity: 0.92f),
                BalloonShape.Whisper,
                TextStyle.Default.With(allCaps: false, italic: true, fontSize: 12.5f, tracking: 0.012f, lineHeight: 1.2f),
                false,
                false,
                Tail(0f, 106f, TailStyle.Squiggly, 9f, attachX: 0f, attachY: 1f, curvature: 0.45f)),
            Named(
                "E4319F8D-31AC-4AF5-A2EC-EA9C11AFAD31",
                "Manga Thought Cloud",
                BalloonStyle.Default.With(
                    fillColor: C(255, 255, 255),
                    strokeColor: C(106, 106, 110),
                    strokeWidth: 1.7f),
                BalloonShape.Thought,
                TextStyle.Default.With(allCaps: false, lineHeight: 1.16f),
                false,
                false,
                Tail(8f, 92f, TailStyle.ThoughtBubbles, 12f, attachX: 0.1f, attachY: 1f)),
            Named(
                "4C1806A2-CF16-402D-BEBB-EA58899D5A8A",
                "Manga Shout Jagged",
                BalloonStyle.Default.With(
                    fillColor: C(255, 255, 255),
                    strokeColor: Color.Black,
                    strokeWidth: 4.6f,
                    glowEnabled: true,
                    glowColor: C(255, 238, 150),
                    glowOpacity: 0.22f,
                    glowSize: 4f),
                BalloonShape.Burst,
                TextStyle.Default.With(allCaps: true, bold: true, fontSize: 17f, tracking: 0.03f, lineHeight: 1.04f),
                false,
                false,
                Tail(0f, 140f, TailStyle.Pointer, 24f, attachX: 0f, attachY: 1f)),
            Named(
                "F5D5B7D1-4F16-4F32-8E70-9ABF055D4E95",
                "Manga Narration Box",
                BalloonStyle.Default.With(
                    fillColor: C(246, 246, 246),
                    strokeColor: C(38, 38, 42),
                    strokeWidth: 2f,
                    cornerRadius: 0f,
                    paddingLeft: 10f,
                    paddingTop: 7f,
                    paddingRight: 10f,
                    paddingBottom: 7f),
                BalloonShape.Rectangle,
                TextStyle.Default.With(allCaps: false, alignment: TextAlignment.Left, lineHeight: 1.1f),
                constrainToPanel: true,
                isQuickSelect: false),
            Named(
                "AD16F114-5A1A-4A60-9A89-14432C39F8C7",
                "Manga SFX Heavy",
                BalloonStyle.Default.With(
                    fillColor: Color.Transparent,
                    strokeColor: Color.Black,
                    strokeWidth: 6f),
                BalloonShape.None,
                TextStyle.TextOnlyDefault.With(
                    fontSize: 52f,
                    textColor: C(255, 255, 255),
                    outlineColor: Color.Black,
                    outlineWidth: 7f,
                    tracking: 0.06f),
                isQuickSelect: false)
        };
    }

    private static NamedBalloonStyle Named(
        string id,
        string name,
        BalloonStyle style,
        BalloonShape shape,
        TextStyle textStyle,
        bool constrainToPanel = false,
        bool isQuickSelect = true,
        params BalloonTemplateTail[] tails)
    {
        return new NamedBalloonStyle(
            Guid.Parse(id),
            name,
            style,
            isQuickSelect: isQuickSelect,
            applyExtendedDetails: true,
            shape: shape,
            customShapePathData: null,
            constrainToPanel: constrainToPanel,
            textStyle: textStyle,
            textPath: null,
            tails: tails);
    }

    private static BalloonTemplateTail Tail(
        float x,
        float y,
        TailStyle style,
        float baseWidth,
        float? attachX = null,
        float? attachY = null,
        float? controlX = null,
        float? controlY = null,
        float curvature = 0.3f)
    {
        Point2? attachment = (attachX.HasValue && attachY.HasValue)
            ? new Point2(attachX.Value, attachY.Value)
            : null;
        Point2? control = (controlX.HasValue && controlY.HasValue)
            ? new Point2(controlX.Value, controlY.Value)
            : null;
        return new BalloonTemplateTail(new Point2(x, y), style, baseWidth, attachment, control, curvature);
    }

    private static Color C(byte r, byte g, byte b)
    {
        return new Color(r, g, b);
    }
}
