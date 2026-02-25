using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Media
{
    /// <summary>
    /// Software rasterizer that renders parsed SVG shapes to a pixel array.
    /// Uses viewBox mapping, scanline polygon fill with non-zero winding rule,
    /// and De Casteljau subdivision for Bezier curves.
    /// </summary>
    public static class SVGRasterizer
    {
        private const int BezierSubdivisions = 12;

        /// <summary>
        /// Main entry point. Rasterizes SVG data to the pixel array.
        /// </summary>
        public static void Rasterize(SVGData data, Color[] pixels, int width, int height)
        {
            if (data == null || pixels == null || pixels.Length < width * height) return;

            float vx = data.ViewBoxX;
            float vy = data.ViewBoxY;
            float vw = data.ViewBoxWidth > 0 ? data.ViewBoxWidth : data.DocumentWidth;
            float vh = data.ViewBoxHeight > 0 ? data.ViewBoxHeight : data.DocumentHeight;
            if (vw <= 0) vw = 100f;
            if (vh <= 0) vh = 100f;

            foreach (var shape in data.Shapes)
            {
                switch (shape.ShapeType)
                {
                    case SVGShapeType.Rect:
                        RasterizeRect(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Circle:
                        RasterizeCircle(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Ellipse:
                        RasterizeEllipse(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Line:
                        RasterizeLine(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Polyline:
                        RasterizePolyline(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Polygon:
                        RasterizePolygon(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                    case SVGShapeType.Path:
                        RasterizePath(shape, pixels, width, height, vx, vy, vw, vh);
                        break;
                }
            }
        }

        private static void SvgToPixel(float svgX, float svgY, int width, int height,
            float vx, float vy, float vw, float vh, out int px, out int py)
        {
            px = (int)Mathf.Round((svgX - vx) / vw * width);
            py = (int)Mathf.Round((svgY - vy) / vh * height);
        }

        private static float SvgToPixelX(float svgX, int width, float vx, float vw)
        {
            return (svgX - vx) / vw * width;
        }

        private static float SvgToPixelY(float svgY, int height, float vy, float vh)
        {
            return (svgY - vy) / vh * height;
        }

        private static void RasterizeRect(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            float rx = shape.RX;
            float ry = shape.RY > 0 ? shape.RY : shape.RX;
            float x = shape.X;
            float y = shape.Y;
            float w = shape.Width;
            float h = shape.Height;

            if (w <= 0 || h <= 0) return;

            float x2 = x + w;
            float y2 = y + h;

            if (rx <= 0 && ry <= 0)
            {
                // Simple rectangle
                int px0 = Mathf.Clamp((int)Mathf.Floor((x - vx) / vw * width), 0, width - 1);
                int py0 = Mathf.Clamp((int)Mathf.Floor((y - vy) / vh * height), 0, height - 1);
                int px1 = Mathf.Clamp((int)Mathf.Ceil((x2 - vx) / vw * width), 0, width);
                int py1 = Mathf.Clamp((int)Mathf.Ceil((y2 - vy) / vh * height), 0, height);

                var fill = Premultiply(shape.FillColor);
                for (int py = py0; py < py1; py++)
                {
                    for (int px = px0; px < px1; px++)
                    {
                        int idx = py * width + px;
                        BlendPixel(pixels, idx, fill);
                    }
                }
            }
            else
            {
                // Rounded rect: approximate with polygon (4 corners as quarter-ellipses)
                rx = Mathf.Min(rx, w / 2);
                ry = Mathf.Min(ry, h / 2);

                var pts = new List<Vector2>();
                int segs = 8;
                float cx1 = x + rx, cy1 = y + ry;
                for (int i = segs; i >= 0; i--)
                {
                    float t = (float)i / segs;
                    float angle = Mathf.PI * 0.5f * (1f - t);
                    pts.Add(new Vector2(cx1 - Mathf.Cos(angle) * rx, cy1 - Mathf.Sin(angle) * ry));
                }
                float cx2 = x2 - rx, cy2 = y + ry;
                for (int i = 1; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    float angle = Mathf.PI * 0.5f * (1f - t);
                    pts.Add(new Vector2(cx2 + Mathf.Cos(angle) * rx, cy2 - Mathf.Sin(angle) * ry));
                }
                float cx3 = x2 - rx, cy3 = y2 - ry;
                for (int i = 1; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    float angle = Mathf.PI * 0.5f * t;
                    pts.Add(new Vector2(cx3 + Mathf.Cos(angle) * rx, cy3 + Mathf.Sin(angle) * ry));
                }
                float cx4 = x + rx, cy4 = y2 - ry;
                for (int i = 1; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    float angle = Mathf.PI * 0.5f * t;
                    pts.Add(new Vector2(cx4 - Mathf.Cos(angle) * rx, cy4 + Mathf.Sin(angle) * ry));
                }

                var polyShape = new SVGShape
                {
                    ShapeType = SVGShapeType.Polygon,
                    Points = pts.ToArray(),
                    FillColor = shape.FillColor,
                    StrokeColor = shape.StrokeColor,
                    StrokeWidth = shape.StrokeWidth,
                    Opacity = shape.Opacity,
                    HasFill = shape.HasFill,
                    HasStroke = shape.HasStroke
                };
                RasterizePolygon(polyShape, pixels, width, height, vx, vy, vw, vh);
            }

            if (shape.HasStroke && shape.StrokeWidth > 0)
            {
                // Stroke as outline - draw 4 lines
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                DrawLine(x, y, x2, y, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                DrawLine(x2, y, x2, y2, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                DrawLine(x2, y2, x, y2, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                DrawLine(x, y2, x, y, sw, stroke, pixels, width, height, vx, vy, vw, vh);
            }
        }

        private static void RasterizeCircle(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            float cx = shape.CX;
            float cy = shape.CY;
            float r = shape.R;
            if (r <= 0) return;

            float cxPx = SvgToPixelX(cx, width, vx, vw);
            float cyPx = SvgToPixelY(cy, height, vy, vh);
            float rPx = r / vw * width;
            float rPxY = r / vh * height;

            int x0 = Mathf.Max(0, (int)Mathf.Floor(cxPx - rPx));
            int x1 = Mathf.Min(width - 1, (int)Mathf.Ceil(cxPx + rPx));
            int y0 = Mathf.Max(0, (int)Mathf.Floor(cyPx - rPxY));
            int y1 = Mathf.Min(height - 1, (int)Mathf.Ceil(cyPx + rPxY));

            if (shape.HasFill)
            {
                var fill = Premultiply(shape.FillColor);
                for (int py = y0; py <= y1; py++)
                {
                    float dy = (py - cyPx) / rPxY;
                    for (int px = x0; px <= x1; px++)
                    {
                        float dx = (px - cxPx) / rPx;
                        if (dx * dx + dy * dy <= 1f)
                        {
                            BlendPixel(pixels, py * width + px, fill);
                        }
                    }
                }
            }

            if (shape.HasStroke && shape.StrokeWidth > 0)
            {
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                float innerR = Mathf.Max(0, r - sw / vw * width);
                float outerR = r + sw / vw * width;
                for (int py = y0; py <= y1; py++)
                {
                    float dy = (py - cyPx) / rPxY;
                    for (int px = x0; px <= x1; px++)
                    {
                        float dx = (px - cxPx) / rPx;
                        float d = Mathf.Sqrt(dx * dx + dy * dy) * rPx;
                        if (d >= innerR && d <= outerR)
                            BlendPixel(pixels, py * width + px, stroke);
                    }
                }
            }
        }

        private static void RasterizeEllipse(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            float cx = shape.CX;
            float cy = shape.CY;
            float rx = shape.RX;
            float ry = shape.RY;
            if (rx <= 0 || ry <= 0) return;

            float cxPx = SvgToPixelX(cx, width, vx, vw);
            float cyPx = SvgToPixelY(cy, height, vy, vh);
            float rxPx = rx / vw * width;
            float ryPx = ry / vh * height;

            int x0 = Mathf.Max(0, (int)Mathf.Floor(cxPx - rxPx));
            int x1 = Mathf.Min(width - 1, (int)Mathf.Ceil(cxPx + rxPx));
            int y0 = Mathf.Max(0, (int)Mathf.Floor(cyPx - ryPx));
            int y1 = Mathf.Min(height - 1, (int)Mathf.Ceil(cyPx + ryPx));

            if (shape.HasFill)
            {
                var fill = Premultiply(shape.FillColor);
                for (int py = y0; py <= y1; py++)
                {
                    float dy = (py - cyPx) / ryPx;
                    for (int px = x0; px <= x1; px++)
                    {
                        float dx = (px - cxPx) / rxPx;
                        if (dx * dx + dy * dy <= 1f)
                        {
                            BlendPixel(pixels, py * width + px, fill);
                        }
                    }
                }
            }

            if (shape.HasStroke && shape.StrokeWidth > 0)
            {
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                float innerRx = Mathf.Max(0.1f, rx - sw);
                float innerRy = Mathf.Max(0.1f, ry - sw);
                float outerRx = rx + sw;
                float outerRy = ry + sw;
                float innerRxPx = innerRx / vw * width;
                float innerRyPx = innerRy / vh * height;
                float outerRxPx = outerRx / vw * width;
                float outerRyPx = outerRy / vh * height;
                for (int py = y0; py <= y1; py++)
                {
                    float dy = py - cyPx;
                    for (int px = x0; px <= x1; px++)
                    {
                        float dx = px - cxPx;
                        float outVal = dx * dx / (outerRxPx * outerRxPx) + dy * dy / (outerRyPx * outerRyPx);
                        float inVal = dx * dx / (innerRxPx * innerRxPx) + dy * dy / (innerRyPx * innerRyPx);
                        if (outVal <= 1f && inVal >= 1f)
                            BlendPixel(pixels, py * width + px, stroke);
                    }
                }
            }
        }

        private static void RasterizeLine(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            if (!shape.HasStroke || shape.Points == null || shape.Points.Length < 2) return;
            var stroke = Premultiply(shape.StrokeColor);
            float sw = shape.StrokeWidth * 0.5f;
            DrawLine(shape.Points[0].x, shape.Points[0].y, shape.Points[1].x, shape.Points[1].y,
                sw, stroke, pixels, width, height, vx, vy, vw, vh);
        }

        private static void RasterizePolyline(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            if (shape.Points == null || shape.Points.Length < 2) return;
            if (shape.HasStroke)
            {
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                for (int i = 0; i < shape.Points.Length - 1; i++)
                {
                    var a = shape.Points[i];
                    var b = shape.Points[i + 1];
                    DrawLine(a.x, a.y, b.x, b.y, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                }
            }
        }

        private static void RasterizePolygon(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            if (shape.Points == null || shape.Points.Length < 3) return;

            var ptsPx = new List<Vector2>();
            foreach (var p in shape.Points)
            {
                ptsPx.Add(new Vector2(
                    SvgToPixelX(p.x, width, vx, vw),
                    SvgToPixelY(p.y, height, vy, vh)
                ));
            }

            if (shape.HasFill)
            {
                ScanlineFill(ptsPx, Premultiply(shape.FillColor), pixels, width, height);
            }

            if (shape.HasStroke && shape.StrokeWidth > 0)
            {
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                for (int i = 0; i < shape.Points.Length; i++)
                {
                    var a = shape.Points[i];
                    var b = shape.Points[(i + 1) % shape.Points.Length];
                    DrawLine(a.x, a.y, b.x, b.y, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                }
            }
        }

        private static void RasterizePath(SVGShape shape, Color[] pixels, int width, int height,
            float vx, float vy, float vw, float vh)
        {
            if (shape.PathCommands == null || shape.PathCommands.Count == 0) return;

            var polygon = FlattenPath(shape.PathCommands);
            if (polygon.Count < 3) return;

            var ptsPx = new List<Vector2>();
            foreach (var p in polygon)
            {
                ptsPx.Add(new Vector2(
                    SvgToPixelX(p.x, width, vx, vw),
                    SvgToPixelY(p.y, height, vy, vh)
                ));
            }

            if (shape.HasFill)
            {
                ScanlineFill(ptsPx, Premultiply(shape.FillColor), pixels, width, height);
            }

            if (shape.HasStroke && shape.StrokeWidth > 0)
            {
                var stroke = Premultiply(shape.StrokeColor);
                float sw = shape.StrokeWidth * 0.5f;
                for (int i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    DrawLine(a.x, a.y, b.x, b.y, sw, stroke, pixels, width, height, vx, vy, vw, vh);
                }
            }
        }

        private static List<Vector2> FlattenPath(List<SVGPathCommand> commands)
        {
            var result = new List<Vector2>();
            float curX = 0, curY = 0;
            float startX = 0, startY = 0;
            float ctrlX = 0, ctrlY = 0;

            foreach (var cmd in commands)
            {
                var c = char.ToUpperInvariant(cmd.Command);
                bool rel = char.IsLower(cmd.Command);
                float ox = rel ? curX : 0, oy = rel ? curY : 0;
                var args = cmd.Args ?? Array.Empty<float>();

                switch (c)
                {
                    case 'M':
                        if (args.Length >= 2)
                        {
                            curX = ox + args[0];
                            curY = oy + args[1];
                            startX = curX;
                            startY = curY;
                            ctrlX = curX;
                            ctrlY = curY;
                            result.Add(new Vector2(curX, curY));
                            for (int i = 2; i + 1 < args.Length; i += 2)
                            {
                                if (rel) { ox = curX; oy = curY; }
                                curX = ox + args[i];
                                curY = oy + args[i + 1];
                                result.Add(new Vector2(curX, curY));
                            }
                        }
                        break;

                    case 'L':
                        for (int i = 0; i + 1 < args.Length; i += 2)
                        {
                            if (rel && i > 0) { ox = curX; oy = curY; }
                            curX = ox + args[i];
                            curY = oy + args[i + 1];
                            result.Add(new Vector2(curX, curY));
                        }
                        break;

                    case 'H':
                        for (int i = 0; i < args.Length; i++)
                        {
                            if (rel && i > 0) ox = curX;
                            curX = ox + args[i];
                            result.Add(new Vector2(curX, curY));
                        }
                        break;

                    case 'V':
                        for (int i = 0; i < args.Length; i++)
                        {
                            if (rel && i > 0) oy = curY;
                            curY = oy + args[i];
                            result.Add(new Vector2(curX, curY));
                        }
                        break;

                    case 'Z':
                        if (result.Count > 0)
                        {
                            curX = startX;
                            curY = startY;
                            result.Add(new Vector2(curX, curY));
                        }
                        break;

                    case 'C':
                        for (int i = 0; i + 5 < args.Length; i += 6)
                        {
                            if (rel && i > 0) { ox = curX; oy = curY; }
                            float x1 = ox + args[i], y1 = oy + args[i + 1];
                            float x2 = ox + args[i + 2], y2 = oy + args[i + 3];
                            float x = ox + args[i + 4], y = oy + args[i + 5];
                            var curve = SubdivideCubic(curX, curY, x1, y1, x2, y2, x, y);
                            for (int j = 1; j < curve.Count; j++)
                                result.Add(curve[j]);
                            ctrlX = x2;
                            ctrlY = y2;
                            curX = x;
                            curY = y;
                        }
                        break;

                    case 'S':
                        for (int i = 0; i + 3 < args.Length; i += 4)
                        {
                            if (rel && i > 0) { ox = curX; oy = curY; }
                            float x2 = ox + args[i], y2 = oy + args[i + 1];
                            float x = ox + args[i + 2], y = oy + args[i + 3];
                            float x1 = curX + (curX - ctrlX);
                            float y1 = curY + (curY - ctrlY);
                            var curve = SubdivideCubic(curX, curY, x1, y1, x2, y2, x, y);
                            for (int j = 1; j < curve.Count; j++)
                                result.Add(curve[j]);
                            ctrlX = x2;
                            ctrlY = y2;
                            curX = x;
                            curY = y;
                        }
                        break;

                    case 'Q':
                        for (int i = 0; i + 3 < args.Length; i += 4)
                        {
                            if (rel && i > 0) { ox = curX; oy = curY; }
                            float x1 = ox + args[i], y1 = oy + args[i + 1];
                            float x = ox + args[i + 2], y = oy + args[i + 3];
                            ctrlX = x1;
                            ctrlY = y1;
                            var qCurve = SubdivideQuadratic(curX, curY, x1, y1, x, y);
                            for (int j = 1; j < qCurve.Count; j++)
                                result.Add(qCurve[j]);
                            curX = x;
                            curY = y;
                        }
                        break;

                    case 'T':
                        for (int i = 0; i + 1 < args.Length; i += 2)
                        {
                            if (rel && i > 0) { ox = curX; oy = curY; }
                            float x = ox + args[i], y = oy + args[i + 1];
                            float x1 = curX + (curX - ctrlX);
                            float y1 = curY + (curY - ctrlY);
                            ctrlX = x1;
                            ctrlY = y1;
                            var qCurve = SubdivideQuadratic(curX, curY, x1, y1, x, y);
                            for (int j = 1; j < qCurve.Count; j++)
                                result.Add(qCurve[j]);
                            curX = x;
                            curY = y;
                        }
                        break;

                    default:
                        break;
                }
            }

            return result;
        }

        private static List<Vector2> SubdivideCubic(float x0, float y0, float x1, float y1,
            float x2, float y2, float x3, float y3)
        {
            var result = new List<Vector2> { new Vector2(x0, y0) };
            for (int i = 1; i <= BezierSubdivisions; i++)
            {
                float t = (float)i / BezierSubdivisions;
                float u = 1f - t;
                float u2 = u * u;
                float u3 = u2 * u;
                float t2 = t * t;
                float t3 = t2 * t;

                float x = u3 * x0 + 3f * u2 * t * x1 + 3f * u * t2 * x2 + t3 * x3;
                float y = u3 * y0 + 3f * u2 * t * y1 + 3f * u * t2 * y2 + t3 * y3;
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        private static List<Vector2> SubdivideQuadratic(float x0, float y0, float x1, float y1, float x2, float y2)
        {
            var result = new List<Vector2> { new Vector2(x0, y0) };
            for (int i = 1; i <= BezierSubdivisions; i++)
            {
                float t = (float)i / BezierSubdivisions;
                float u = 1f - t;

                float x = u * u * x0 + 2f * u * t * x1 + t * t * x2;
                float y = u * u * y0 + 2f * u * t * y1 + t * t * y2;
                result.Add(new Vector2(x, y));
            }
            return result;
        }

        private static void ScanlineFill(List<Vector2> pts, Color fill, Color[] pixels, int width, int height)
        {
            if (pts.Count < 3) return;

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (var p in pts)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            int yStart = Mathf.Max(0, (int)Mathf.Floor(minY));
            int yEnd = Mathf.Min(height - 1, (int)Mathf.Ceil(maxY));

            for (int y = yStart; y <= yEnd; y++)
            {
                float fy = y + 0.5f;
                var intersections = new List<(float x, int winding)>();

                for (int i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];

                    int winding = b.y > a.y ? 1 : -1;

                    float ay = a.y, by = b.y;
                    if (ay > by) { (ay, by) = (by, ay); (a, b) = (b, a); }

                    if (fy <= ay || fy >= by) continue;
                    if (ay == by) continue;

                    float t = (fy - ay) / (by - ay);
                    float x = a.x + t * (b.x - a.x);

                    intersections.Add((x, winding));
                }

                intersections.Sort((a, b) => a.x.CompareTo(b.x));

                int wind = 0;
                for (int i = 0; i < intersections.Count; i++)
                {
                    wind += intersections[i].winding;
                    if (wind != 0 && i + 1 < intersections.Count)
                    {
                        int x0 = Mathf.Max(0, (int)Mathf.Floor(intersections[i].x));
                        int x1 = Mathf.Min(width - 1, (int)Mathf.Ceil(intersections[i + 1].x));
                        for (int x = x0; x <= x1; x++)
                        {
                            int idx = y * width + x;
                            if (idx >= 0 && idx < pixels.Length)
                                BlendPixel(pixels, idx, fill);
                        }
                    }
                }
            }
        }

        private static void DrawLine(float x1, float y1, float x2, float y2, float strokeWidth,
            Color stroke, Color[] pixels, int width, int height, float vx, float vy, float vw, float vh)
        {
            float x1p = SvgToPixelX(x1, width, vx, vw);
            float y1p = SvgToPixelY(y1, height, vy, vh);
            float x2p = SvgToPixelX(x2, width, vx, vw);
            float y2p = SvgToPixelY(y2, height, vy, vh);
            float swPx = strokeWidth / vw * width;

            float dx = x2p - x1p;
            float dy = y2p - y1p;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;

            int steps = Mathf.Max(2, (int)Mathf.Ceil(len * 2));
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                float cx = x1p + t * dx;
                float cy = y1p + t * dy;

                int r = (int)Mathf.Ceil(swPx);
                int ix = (int)Mathf.Round(cx);
                int iy = (int)Mathf.Round(cy);
                for (int oy = -r; oy <= r; oy++)
                {
                    for (int ox = -r; ox <= r; ox++)
                    {
                        int px = ix + ox;
                        int py = iy + oy;
                        if (px < 0 || px >= width || py < 0 || py >= height) continue;
                        float dist = Mathf.Sqrt(ox * ox + oy * oy);
                        if (dist <= swPx)
                        {
                            BlendPixel(pixels, py * width + px, stroke);
                        }
                    }
                }
            }
        }

        private static Color Premultiply(Color c)
        {
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }

        private static void BlendPixel(Color[] pixels, int index, Color src)
        {
            if (index < 0 || index >= pixels.Length) return;
            var dst = pixels[index];
            float sa = src.a;
            float da = dst.a;
            float outA = sa + da * (1f - sa);
            if (outA < 0.0001f)
            {
                pixels[index] = Color.clear;
                return;
            }
            float invOutA = 1f / outA;
            float r = (src.r + dst.r * (1f - sa)) * invOutA;
            float g = (src.g + dst.g * (1f - sa)) * invOutA;
            float b = (src.b + dst.b * (1f - sa)) * invOutA;
            pixels[index] = new Color(r, g, b, outA);
        }
    }
}
