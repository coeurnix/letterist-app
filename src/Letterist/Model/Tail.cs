namespace Letterist.Model;

public sealed class Tail
{
    public Guid Id { get; }

    public Point2 TargetPoint { get; private set; }

    public TailStyle Style { get; private set; }

    public float BaseWidth { get; private set; }

    public Point2? AttachmentDirection { get; private set; }

    public Point2? ControlPoint { get; private set; }

    public float Curvature { get; private set; }

    public float CurveCenter { get; private set; }

    public float Inset { get; private set; }

    public Tail(Guid id, Point2 targetPoint, TailStyle style = TailStyle.Pointer, float baseWidth = 16f)
    {
        Id = id;
        TargetPoint = targetPoint;
        Style = style;
        BaseWidth = baseWidth;
        Curvature = 0.3f; // Default slight curve
        CurveCenter = 0.5f;
        Inset = 0f;
    }

    public static Tail Create(Point2 targetPoint, TailStyle style = TailStyle.Pointer, float baseWidth = 16f)
    {
        return new Tail(Guid.NewGuid(), targetPoint, style, baseWidth);
    }

    public Tail Clone()
    {
        var clone = new Tail(Id, TargetPoint, Style, BaseWidth);
        clone.SetAttachmentDirection(AttachmentDirection);
        clone.SetControlPoint(ControlPoint);
        clone.SetCurvature(Curvature);
        clone.SetCurveCenter(CurveCenter);
        clone.SetInset(Inset);
        return clone;
    }

    public Tail CloneWithNewId()
    {
        var clone = new Tail(Guid.NewGuid(), TargetPoint, Style, BaseWidth);
        clone.SetAttachmentDirection(AttachmentDirection);
        clone.SetControlPoint(ControlPoint);
        clone.SetCurvature(Curvature);
        clone.SetCurveCenter(CurveCenter);
        clone.SetInset(Inset);
        return clone;
    }

    internal void SetTargetPoint(Point2 point) => TargetPoint = point;
    internal void SetStyle(TailStyle style) => Style = style;
    internal void SetBaseWidth(float width) => BaseWidth = width;
    internal void SetAttachmentDirection(Point2? direction) => AttachmentDirection = direction;
    internal void SetControlPoint(Point2? point) => ControlPoint = point;
    internal void SetCurvature(float curvature) => Curvature = Math.Clamp(curvature, -2f, 2f);
    internal void SetCurveCenter(float curveCenter) => CurveCenter = Math.Clamp(curveCenter, 0f, 1f);
    internal void SetInset(float inset) => Inset = Math.Clamp(inset, 0f, 64f);
}
