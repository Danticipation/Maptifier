using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Masking
{
    /// <summary>
    /// Static utility for ear-clipping polygon triangulation.
    /// Handles both CW and CCW winding.
    /// </summary>
    public static class EarClipTriangulator
    {
        public static int[] Triangulate(Vector2[] vertices)
        {
            if (vertices == null || vertices.Length < 3)
                return Array.Empty<int>();

            var n = vertices.Length;
            if (n == 3)
                return new[] { 0, 1, 2 };

            // Build linked list of vertex indices
            var prev = new int[n];
            var next = new int[n];
            for (var i = 0; i < n; i++)
            {
                prev[i] = (i - 1 + n) % n;
                next[i] = (i + 1) % n;
            }

            // Detect winding and normalize to CCW
            var signedArea = SignedArea(vertices);
            if (signedArea < 0)
                Array.Reverse(vertices);

            var triangles = new List<int>(Math.Max(0, (n - 2) * 3));
            var remaining = n;
            var index = 0;

            while (remaining > 3)
            {
                var earFound = false;
                var attempts = 0;
                var startIndex = index;

                while (attempts < remaining)
                {
                    var i0 = prev[index];
                    var i1 = index;
                    var i2 = next[index];

                    if (IsEar(vertices, prev, next, i0, i1, i2, n))
                    {
                        triangles.Add(i0);
                        triangles.Add(i1);
                        triangles.Add(i2);

                        next[i0] = i2;
                        prev[i2] = i0;
                        remaining--;
                        earFound = true;
                        index = i0;
                        break;
                    }

                    index = next[index];
                    attempts++;
                }

                if (!earFound)
                    break;
            }

            if (remaining == 3)
            {
                var i0 = index;
                var i1 = next[i0];
                var i2 = next[i1];
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
            }

            return triangles.ToArray();
        }

        private static float SignedArea(Vector2[] vertices)
        {
            var area = 0f;
            var n = vertices.Length;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                area += vertices[i].x * vertices[j].y;
                area -= vertices[j].x * vertices[i].y;
            }
            return area * 0.5f;
        }

        private static bool IsEar(Vector2[] vertices, int[] prev, int[] next, int i0, int i1, int i2, int n)
        {
            var v0 = vertices[i0];
            var v1 = vertices[i1];
            var v2 = vertices[i2];

            if (!IsConvex(v0, v1, v2))
                return false;

            var p = v1;
            var idx = next[i2];
            while (idx != i0)
            {
                if (IsPointInTriangle(vertices[idx], v0, v1, v2))
                    return false;
                idx = next[idx];
            }

            return true;
        }

        public static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var ab = Cross(b - a, p - a);
            var bc = Cross(c - b, p - b);
            var ca = Cross(a - c, p - c);

            return (ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0);
        }

        public static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, c - b) >= 0;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
