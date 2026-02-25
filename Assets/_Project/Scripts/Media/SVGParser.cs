using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

namespace Maptifier.Media
{
    /// <summary>
    /// Parsed SVG data containing shapes, viewBox, and dimensions.
    /// </summary>
    public class SVGData
    {
        public float ViewBoxX, ViewBoxY, ViewBoxWidth, ViewBoxHeight;
        public float DocumentWidth, DocumentHeight;
        public bool HasViewBox;
        public List<SVGShape> Shapes = new();
    }

    public enum SVGShapeType { Rect, Circle, Ellipse, Line, Polyline, Polygon, Path }

    public class SVGShape
    {
        public SVGShapeType ShapeType;
        public Color FillColor = Color.white;
        public Color StrokeColor = Color.clear;
        public float StrokeWidth;
        public float Opacity = 1f;
        public bool HasFill = true;
        public bool HasStroke;

        // Shape-specific data
        public float X, Y, Width, Height;      // Rect
        public float CX, CY, R;                // Circle
        public float RX, RY;                    // Ellipse (also used for rounded rect)
        public Vector2[] Points;                // Polyline, Polygon
        public List<SVGPathCommand> PathCommands; // Path
    }

    public struct SVGPathCommand
    {
        public char Command;
        public float[] Args;
    }

    /// <summary>
    /// Parses SVG XML into SVGData with shapes. Handles basic SVG elements:
    /// rect, circle, ellipse, line, polyline, polygon, and path (M/L/C/Q/Z commands).
    /// </summary>
    public static class SVGParser
    {
        public static SVGData Parse(string svgContent)
        {
            var data = new SVGData();
            var doc = new XmlDocument();
            doc.LoadXml(svgContent);

            var svgNode = doc.DocumentElement;
            if (svgNode == null || svgNode.Name != "svg")
            {
                Debug.LogWarning("[SVGParser] Root element is not <svg>.");
                return data;
            }

            ParseViewBox(svgNode, data);
            ParseDimensions(svgNode, data);
            ParseChildren(svgNode, data, Color.black, 1f);

            return data;
        }

        private static void ParseViewBox(XmlElement node, SVGData data)
        {
            var vb = node.GetAttribute("viewBox");
            if (string.IsNullOrEmpty(vb)) return;

            var parts = vb.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                data.ViewBoxX = ParseFloat(parts[0]);
                data.ViewBoxY = ParseFloat(parts[1]);
                data.ViewBoxWidth = ParseFloat(parts[2]);
                data.ViewBoxHeight = ParseFloat(parts[3]);
                data.HasViewBox = true;
            }
        }

        private static void ParseDimensions(XmlElement node, SVGData data)
        {
            data.DocumentWidth = ParseDimension(node.GetAttribute("width"), 100f);
            data.DocumentHeight = ParseDimension(node.GetAttribute("height"), 100f);

            if (!data.HasViewBox)
            {
                data.ViewBoxWidth = data.DocumentWidth;
                data.ViewBoxHeight = data.DocumentHeight;
                data.HasViewBox = true;
            }
        }

        private static void ParseChildren(XmlNode parent, SVGData data, Color inheritFill, float inheritOpacity)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child is not XmlElement elem) continue;

                var fill = ParseColorAttribute(elem, "fill", inheritFill);
                var opacity = ParseFloatAttribute(elem, "opacity", inheritOpacity);
                var fillOpacity = ParseFloatAttribute(elem, "fill-opacity", 1f);
                fill.a = opacity * fillOpacity;

                var stroke = ParseColorAttribute(elem, "stroke", Color.clear);
                var strokeWidth = ParseFloatAttribute(elem, "stroke-width", 0f);

                SVGShape shape = null;

                switch (elem.Name)
                {
                    case "rect":
                        shape = new SVGShape
                        {
                            ShapeType = SVGShapeType.Rect,
                            X = ParseFloatAttribute(elem, "x", 0),
                            Y = ParseFloatAttribute(elem, "y", 0),
                            Width = ParseFloatAttribute(elem, "width", 0),
                            Height = ParseFloatAttribute(elem, "height", 0),
                            RX = ParseFloatAttribute(elem, "rx", 0),
                            RY = ParseFloatAttribute(elem, "ry", 0)
                        };
                        break;

                    case "circle":
                        shape = new SVGShape
                        {
                            ShapeType = SVGShapeType.Circle,
                            CX = ParseFloatAttribute(elem, "cx", 0),
                            CY = ParseFloatAttribute(elem, "cy", 0),
                            R = ParseFloatAttribute(elem, "r", 0)
                        };
                        break;

                    case "ellipse":
                        shape = new SVGShape
                        {
                            ShapeType = SVGShapeType.Ellipse,
                            CX = ParseFloatAttribute(elem, "cx", 0),
                            CY = ParseFloatAttribute(elem, "cy", 0),
                            RX = ParseFloatAttribute(elem, "rx", 0),
                            RY = ParseFloatAttribute(elem, "ry", 0)
                        };
                        break;

                    case "line":
                        shape = new SVGShape
                        {
                            ShapeType = SVGShapeType.Line,
                            Points = new[]
                            {
                                new Vector2(ParseFloatAttribute(elem, "x1", 0), ParseFloatAttribute(elem, "y1", 0)),
                                new Vector2(ParseFloatAttribute(elem, "x2", 0), ParseFloatAttribute(elem, "y2", 0))
                            },
                            HasFill = false,
                            HasStroke = true
                        };
                        break;

                    case "polyline":
                    case "polygon":
                        var pts = ParsePointsAttribute(elem.GetAttribute("points"));
                        if (pts != null && pts.Length >= 2)
                        {
                            shape = new SVGShape
                            {
                                ShapeType = elem.Name == "polygon" ? SVGShapeType.Polygon : SVGShapeType.Polyline,
                                Points = pts
                            };
                        }
                        break;

                    case "path":
                        var d = elem.GetAttribute("d");
                        if (!string.IsNullOrEmpty(d))
                        {
                            shape = new SVGShape
                            {
                                ShapeType = SVGShapeType.Path,
                                PathCommands = ParsePathData(d)
                            };
                        }
                        break;

                    case "g":
                        ParseChildren(elem, data, fill, opacity);
                        continue;

                    default:
                        continue;
                }

                if (shape != null)
                {
                    shape.FillColor = fill;
                    shape.StrokeColor = stroke;
                    shape.StrokeWidth = strokeWidth;
                    shape.Opacity = opacity;
                    shape.HasStroke = strokeWidth > 0 && stroke.a > 0;

                    var fillAttr = elem.GetAttribute("fill");
                    if (fillAttr == "none") shape.HasFill = false;

                    data.Shapes.Add(shape);
                }
            }
        }

        private static Vector2[] ParsePointsAttribute(string points)
        {
            if (string.IsNullOrEmpty(points)) return null;
            var nums = points.Split(new[] { ' ', ',', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<Vector2>();
            for (int i = 0; i + 1 < nums.Length; i += 2)
                result.Add(new Vector2(ParseFloat(nums[i]), ParseFloat(nums[i + 1])));
            return result.ToArray();
        }

        public static List<SVGPathCommand> ParsePathData(string d)
        {
            var commands = new List<SVGPathCommand>();
            var i = 0;
            while (i < d.Length)
            {
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
                if (i >= d.Length) break;

                if (char.IsLetter(d[i]))
                {
                    var cmd = d[i];
                    i++;
                    var args = new List<float>();
                    while (i < d.Length)
                    {
                        while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
                        if (i >= d.Length || (char.IsLetter(d[i]) && d[i] != 'e' && d[i] != 'E')) break;

                        var start = i;
                        if (d[i] == '-' || d[i] == '+') i++;
                        while (i < d.Length && (char.IsDigit(d[i]) || d[i] == '.')) i++;
                        if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
                        {
                            i++;
                            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                            while (i < d.Length && char.IsDigit(d[i])) i++;
                        }

                        if (i > start)
                            args.Add(ParseFloat(d.Substring(start, i - start)));
                        else
                            break;
                    }
                    commands.Add(new SVGPathCommand { Command = cmd, Args = args.ToArray() });
                }
                else
                {
                    i++;
                }
            }
            return commands;
        }

        private static Color ParseColorAttribute(XmlElement elem, string attr, Color fallback)
        {
            var val = elem.GetAttribute(attr);
            if (string.IsNullOrEmpty(val) || val == "inherit") return fallback;
            if (val == "none") return Color.clear;
            return ParseColor(val, fallback);
        }

        public static Color ParseColor(string val, Color fallback)
        {
            if (string.IsNullOrEmpty(val)) return fallback;
            val = val.Trim();

            if (val.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(val, out var c)) return c;
            }
            if (val.StartsWith("rgb"))
            {
                var inner = val.Substring(val.IndexOf('(') + 1).TrimEnd(')');
                var parts = inner.Split(',');
                if (parts.Length >= 3)
                {
                    return new Color(
                        ParseFloat(parts[0].Trim()) / 255f,
                        ParseFloat(parts[1].Trim()) / 255f,
                        ParseFloat(parts[2].Trim()) / 255f,
                        1f
                    );
                }
            }

            // Named colors (common subset)
            switch (val.ToLower())
            {
                case "black": return Color.black;
                case "white": return Color.white;
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "gray": case "grey": return Color.gray;
                case "orange": return new Color(1f, 0.647f, 0f);
                case "purple": return new Color(0.5f, 0f, 0.5f);
                case "pink": return new Color(1f, 0.753f, 0.796f);
                case "transparent": return Color.clear;
                default: return fallback;
            }
        }

        private static float ParseFloatAttribute(XmlElement elem, string attr, float fallback)
        {
            var val = elem.GetAttribute(attr);
            return string.IsNullOrEmpty(val) ? fallback : ParseFloat(val);
        }

        private static float ParseDimension(string val, float fallback)
        {
            if (string.IsNullOrEmpty(val)) return fallback;
            val = val.Trim().Replace("px", "").Replace("pt", "").Replace("%", "");
            return ParseFloat(val, fallback);
        }

        private static float ParseFloat(string val, float fallback = 0f)
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;
            return fallback;
        }
    }
}
