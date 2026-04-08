using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Globalization;
using System.Numerics;

namespace Letterist.Rendering;

internal static class SvgPathParser
{
    public static CanvasGeometry? TryCreateGeometry(ICanvasResourceCreator resourceCreator, string pathData)
    {
        if (string.IsNullOrWhiteSpace(pathData)) return null;

        try
        {
            using var builder = new CanvasPathBuilder(resourceCreator);
            var parser = new PathParser(builder);
            if (!parser.Parse(pathData))
            {
                return null;
            }
            return CanvasGeometry.CreatePath(builder);
        }
        catch
        {
            return null;
        }
    }

    private sealed class PathParser
    {
        private readonly CanvasPathBuilder _builder;
        private string _data = string.Empty;
        private int _index;
        private bool _figureOpen;
        private Vector2 _current;
        private Vector2 _start;
        private Vector2? _lastCubicControl;
        private Vector2? _lastQuadraticControl;

        public PathParser(CanvasPathBuilder builder)
        {
            _builder = builder;
        }

        public bool Parse(string data)
        {
            _data = data;
            _index = 0;
            _figureOpen = false;
            _current = Vector2.Zero;
            _start = Vector2.Zero;
            _lastCubicControl = null;
            _lastQuadraticControl = null;

            char command = '\0';

            while (true)
            {
                SkipSeparators();
                if (_index >= _data.Length) break;

                var currentChar = _data[_index];
                if (IsCommand(currentChar))
                {
                    command = currentChar;
                    _index++;
                }
                else if (command == '\0')
                {
                    return false;
                }

                switch (command)
                {
                    case 'M':
                    case 'm':
                        if (!ReadPoint(command == 'm', out var movePoint)) return false;
                        BeginFigure(movePoint);
                        _current = movePoint;
                        _start = movePoint;
                        ResetControlPoints();
                        while (ReadPoint(command == 'm', out var linePoint))
                        {
                            EnsureFigure();
                            _builder.AddLine(linePoint);
                            _current = linePoint;
                        }
                        break;

                    case 'L':
                    case 'l':
                        while (ReadPoint(command == 'l', out var line))
                        {
                            EnsureFigure();
                            _builder.AddLine(line);
                            _current = line;
                        }
                        ResetControlPoints();
                        break;

                    case 'H':
                    case 'h':
                        while (ReadNumber(out var x))
                        {
                            var target = new Vector2(command == 'h' ? _current.X + x : x, _current.Y);
                            EnsureFigure();
                            _builder.AddLine(target);
                            _current = target;
                        }
                        ResetControlPoints();
                        break;

                    case 'V':
                    case 'v':
                        while (ReadNumber(out var y))
                        {
                            var target = new Vector2(_current.X, command == 'v' ? _current.Y + y : y);
                            EnsureFigure();
                            _builder.AddLine(target);
                            _current = target;
                        }
                        ResetControlPoints();
                        break;

                    case 'C':
                    case 'c':
                        while (ReadCubic(command == 'c', out var c1, out var c2, out var end))
                        {
                            EnsureFigure();
                            _builder.AddCubicBezier(c1, c2, end);
                            _current = end;
                            _lastCubicControl = c2;
                            _lastQuadraticControl = null;
                        }
                        break;

                    case 'S':
                    case 's':
                        while (ReadSmoothCubic(command == 's', out var sc1, out var sc2, out var send))
                        {
                            EnsureFigure();
                            _builder.AddCubicBezier(sc1, sc2, send);
                            _current = send;
                            _lastCubicControl = sc2;
                            _lastQuadraticControl = null;
                        }
                        break;

                    case 'Q':
                    case 'q':
                        while (ReadQuadratic(command == 'q', out var qc, out var qEnd))
                        {
                            EnsureFigure();
                            _builder.AddQuadraticBezier(qc, qEnd);
                            _current = qEnd;
                            _lastQuadraticControl = qc;
                            _lastCubicControl = null;
                        }
                        break;

                    case 'T':
                    case 't':
                        while (ReadSmoothQuadratic(command == 't', out var tc, out var tEnd))
                        {
                            EnsureFigure();
                            _builder.AddQuadraticBezier(tc, tEnd);
                            _current = tEnd;
                            _lastQuadraticControl = tc;
                            _lastCubicControl = null;
                        }
                        break;

                    case 'A':
                    case 'a':
                        while (ReadArc(command == 'a', out var radiusX, out var radiusY, out var rotation, out var largeArc, out var sweep, out var arcEnd))
                        {
                            EnsureFigure();
                            if (radiusX <= 0f || radiusY <= 0f)
                            {
                                _builder.AddLine(arcEnd);
                            }
                            else
                            {
                                var arcSize = largeArc ? CanvasArcSize.Large : CanvasArcSize.Small;
                                var sweepDirection = sweep ? CanvasSweepDirection.Clockwise : CanvasSweepDirection.CounterClockwise;
                                _builder.AddArc(arcEnd, radiusX, radiusY, rotation, sweepDirection, arcSize);
                            }
                            _current = arcEnd;
                            ResetControlPoints();
                        }
                        break;

                    case 'Z':
                    case 'z':
                        if (_figureOpen)
                        {
                            _builder.EndFigure(CanvasFigureLoop.Closed);
                            _figureOpen = false;
                            _current = _start;
                        }
                        ResetControlPoints();
                        break;

                    default:
                        return false;
                }
            }

            if (_figureOpen)
            {
                _builder.EndFigure(CanvasFigureLoop.Open);
                _figureOpen = false;
            }

            return true;
        }

        private static bool IsCommand(char value)
        {
            return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        }

        private void BeginFigure(Vector2 point)
        {
            if (_figureOpen)
            {
                _builder.EndFigure(CanvasFigureLoop.Open);
                _figureOpen = false;
            }
            _builder.BeginFigure(point);
            _figureOpen = true;
        }

        private void EnsureFigure()
        {
            if (_figureOpen) return;
            _builder.BeginFigure(_current);
            _figureOpen = true;
        }

        private void ResetControlPoints()
        {
            _lastCubicControl = null;
            _lastQuadraticControl = null;
        }

        private bool ReadPoint(bool relative, out Vector2 point)
        {
            point = default;
            if (!ReadNumber(out var x) || !ReadNumber(out var y)) return false;

            if (relative)
            {
                point = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                point = new Vector2(x, y);
            }
            return true;
        }

        private bool ReadCubic(bool relative, out Vector2 c1, out Vector2 c2, out Vector2 end)
        {
            c1 = default;
            c2 = default;
            end = default;
            if (!ReadNumber(out var x1) || !ReadNumber(out var y1) ||
                !ReadNumber(out var x2) || !ReadNumber(out var y2) ||
                !ReadNumber(out var x) || !ReadNumber(out var y))
            {
                return false;
            }

            if (relative)
            {
                c1 = new Vector2(_current.X + x1, _current.Y + y1);
                c2 = new Vector2(_current.X + x2, _current.Y + y2);
                end = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                c1 = new Vector2(x1, y1);
                c2 = new Vector2(x2, y2);
                end = new Vector2(x, y);
            }
            return true;
        }

        private bool ReadSmoothCubic(bool relative, out Vector2 c1, out Vector2 c2, out Vector2 end)
        {
            c1 = _lastCubicControl.HasValue ? Reflect(_lastCubicControl.Value) : _current;
            c2 = default;
            end = default;

            if (!ReadNumber(out var x2) || !ReadNumber(out var y2) ||
                !ReadNumber(out var x) || !ReadNumber(out var y))
            {
                return false;
            }

            if (relative)
            {
                c2 = new Vector2(_current.X + x2, _current.Y + y2);
                end = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                c2 = new Vector2(x2, y2);
                end = new Vector2(x, y);
            }
            return true;
        }

        private bool ReadQuadratic(bool relative, out Vector2 control, out Vector2 end)
        {
            control = default;
            end = default;
            if (!ReadNumber(out var x1) || !ReadNumber(out var y1) ||
                !ReadNumber(out var x) || !ReadNumber(out var y))
            {
                return false;
            }

            if (relative)
            {
                control = new Vector2(_current.X + x1, _current.Y + y1);
                end = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                control = new Vector2(x1, y1);
                end = new Vector2(x, y);
            }
            return true;
        }

        private bool ReadSmoothQuadratic(bool relative, out Vector2 control, out Vector2 end)
        {
            control = _lastQuadraticControl.HasValue ? Reflect(_lastQuadraticControl.Value) : _current;
            end = default;

            if (!ReadNumber(out var x) || !ReadNumber(out var y))
            {
                return false;
            }

            if (relative)
            {
                end = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                end = new Vector2(x, y);
            }
            return true;
        }

        private bool ReadArc(bool relative, out float radiusX, out float radiusY, out float rotation, out bool largeArc, out bool sweep, out Vector2 end)
        {
            radiusX = 0f;
            radiusY = 0f;
            rotation = 0f;
            largeArc = false;
            sweep = false;
            end = default;

            if (!ReadNumber(out radiusX) || !ReadNumber(out radiusY) ||
                !ReadNumber(out var rotationDegrees) || !ReadNumber(out var largeArcFlag) ||
                !ReadNumber(out var sweepFlag) || !ReadNumber(out var x) || !ReadNumber(out var y))
            {
                return false;
            }

            radiusX = MathF.Abs(radiusX);
            radiusY = MathF.Abs(radiusY);
            rotation = rotationDegrees * (MathF.PI / 180f);
            largeArc = MathF.Abs(largeArcFlag) > 0.5f;
            sweep = MathF.Abs(sweepFlag) > 0.5f;

            if (relative)
            {
                end = new Vector2(_current.X + x, _current.Y + y);
            }
            else
            {
                end = new Vector2(x, y);
            }

            return true;
        }

        private static Vector2 Reflect(Vector2 point, Vector2 origin)
        {
            return new Vector2(2 * origin.X - point.X, 2 * origin.Y - point.Y);
        }

        private Vector2 Reflect(Vector2 point)
        {
            return Reflect(point, _current);
        }

        private void SkipSeparators()
        {
            while (_index < _data.Length)
            {
                var c = _data[_index];
                if (char.IsWhiteSpace(c) || c == ',')
                {
                    _index++;
                    continue;
                }
                break;
            }
        }

        private bool ReadNumber(out float value)
        {
            value = 0f;
            SkipSeparators();
            if (_index >= _data.Length) return false;

            var start = _index;
            var hasDot = false;
            var hasExp = false;

            if (_data[_index] == '+' || _data[_index] == '-')
            {
                _index++;
            }

            while (_index < _data.Length)
            {
                var c = _data[_index];
                if (char.IsDigit(c))
                {
                    _index++;
                    continue;
                }
                if (c == '.' && !hasDot && !hasExp)
                {
                    hasDot = true;
                    _index++;
                    continue;
                }
                if ((c == 'e' || c == 'E') && !hasExp)
                {
                    hasExp = true;
                    _index++;
                    if (_index < _data.Length && (_data[_index] == '+' || _data[_index] == '-'))
                    {
                        _index++;
                    }
                    continue;
                }
                break;
            }

            if (start == _index)
            {
                return false;
            }

            var token = _data.Substring(start, _index - start);
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
