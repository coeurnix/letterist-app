using Letterist.Model;
using System.Numerics;

namespace Letterist.View;

public sealed class ViewTransform
{
    private Point2 _panOffset;
    private float _zoom;
    private Size2 _viewportSize;

    public const float MinZoom = 0.1f;

    public const float MaxZoom = 8.0f;

    public const float DefaultZoom = 1.0f;

    public event EventHandler? TransformChanged;

    public ViewTransform()
    {
        _panOffset = Point2.Zero;
        _zoom = DefaultZoom;
        _viewportSize = new Size2(800, 600);
    }

    public Point2 PanOffset
    {
        get => _panOffset;
        set
        {
            if (_panOffset != value)
            {
                _panOffset = value;
                TransformChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public float Zoom
    {
        get => _zoom;
        set
        {
            var clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (_zoom != clamped)
            {
                _zoom = clamped;
                TransformChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public float ZoomPercent
    {
        get => _zoom * 100f;
        set => Zoom = value / 100f;
    }

    public Size2 ViewportSize
    {
        get => _viewportSize;
        set
        {
            if (_viewportSize != value)
            {
                _viewportSize = value;
                TransformChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Point2 WorldToScreen(Point2 worldPoint)
    {
        return new Point2(
            worldPoint.X * _zoom + _panOffset.X,
            worldPoint.Y * _zoom + _panOffset.Y);
    }

    public Point2 ScreenToWorld(Point2 screenPoint)
    {
        return new Point2(
            (screenPoint.X - _panOffset.X) / _zoom,
            (screenPoint.Y - _panOffset.Y) / _zoom);
    }

    public Size2 WorldToScreenSize(Size2 worldSize)
    {
        return new Size2(worldSize.Width * _zoom, worldSize.Height * _zoom);
    }

    public Size2 ScreenToWorldSize(Size2 screenSize)
    {
        return new Size2(screenSize.Width / _zoom, screenSize.Height / _zoom);
    }

    public Rect WorldToScreenRect(Rect worldRect)
    {
        var topLeft = WorldToScreen(worldRect.TopLeft);
        var size = WorldToScreenSize(worldRect.Size);
        return Rect.FromPositionSize(topLeft, size);
    }

    public Rect ScreenToWorldRect(Rect screenRect)
    {
        var topLeft = ScreenToWorld(screenRect.TopLeft);
        var size = ScreenToWorldSize(screenRect.Size);
        return Rect.FromPositionSize(topLeft, size);
    }

    public Rect GetVisibleWorldRect()
    {
        var topLeft = ScreenToWorld(Point2.Zero);
        var bottomRight = ScreenToWorld(new Point2(_viewportSize.Width, _viewportSize.Height));
        return Rect.FromCorners(topLeft, bottomRight);
    }

    public void ZoomAt(float factor, Point2 centerScreenPoint)
    {
        var worldPoint = ScreenToWorld(centerScreenPoint);

        var newZoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (newZoom == _zoom) return;

        _zoom = newZoom;

        _panOffset = new Point2(
            centerScreenPoint.X - worldPoint.X * _zoom,
            centerScreenPoint.Y - worldPoint.Y * _zoom);

        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomToFit(Rect worldRect, float padding = 20f)
    {
        if (worldRect.Width <= 0 || worldRect.Height <= 0) return;

        var availableWidth = _viewportSize.Width - padding * 2;
        var availableHeight = _viewportSize.Height - padding * 2;

        if (availableWidth <= 0 || availableHeight <= 0) return;

        var scaleX = availableWidth / worldRect.Width;
        var scaleY = availableHeight / worldRect.Height;
        var newZoom = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);

        var contentScreenWidth = worldRect.Width * newZoom;
        var contentScreenHeight = worldRect.Height * newZoom;

        _zoom = newZoom;
        _panOffset = new Point2(
            (_viewportSize.Width - contentScreenWidth) / 2 - worldRect.X * newZoom,
            (_viewportSize.Height - contentScreenHeight) / 2 - worldRect.Y * newZoom);

        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CenterOn(Point2 worldPoint)
    {
        _panOffset = new Point2(
            _viewportSize.Width / 2 - worldPoint.X * _zoom,
            _viewportSize.Height / 2 - worldPoint.Y * _zoom);

        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pan(Point2 screenDelta)
    {
        _panOffset = _panOffset + screenDelta;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _zoom = DefaultZoom;
        _panOffset = Point2.Zero;
        TransformChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomTo100()
    {
        var centerWorld = ScreenToWorld(new Point2(_viewportSize.Width / 2, _viewportSize.Height / 2));
        _zoom = DefaultZoom;
        CenterOn(centerWorld);
    }

    public Matrix3x2 GetTransformMatrix()
    {
        return Matrix3x2.CreateScale(_zoom) * Matrix3x2.CreateTranslation(_panOffset.X, _panOffset.Y);
    }
}
