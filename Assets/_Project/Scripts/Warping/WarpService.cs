using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Maptifier.Warping
{
    /// <summary>
    /// Implements IWarpService. Generates subdivided warp meshes with projective
    /// (four-corner) or Catmull-Rom (mesh grid) interpolation.
    /// </summary>
    public class WarpService : IWarpService
    {
        private WarpMode _currentMode = WarpMode.FourCorner;

        public WarpMode CurrentMode => _currentMode;

        public void SetMode(WarpMode mode)
        {
            _currentMode = mode;
        }

        public Mesh GenerateWarpMesh(int subdivisions)
        {
            var mesh = new Mesh();
            mesh.name = "WarpMesh";

            var vertexCount = (subdivisions + 1) * (subdivisions + 1);
            var vertices = new List<Vector3>(vertexCount);
            var uvs = new List<Vector2>(vertexCount);
            var indices = new List<int>(subdivisions * subdivisions * 6);

            for (var y = 0; y <= subdivisions; y++)
            {
                for (var x = 0; x <= subdivisions; x++)
                {
                    var u = (float)x / subdivisions;
                    var v = (float)y / subdivisions;
                    vertices.Add(new Vector3(u * 2f - 1f, v * 2f - 1f, 0f));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (var y = 0; y < subdivisions; y++)
            {
                for (var x = 0; x < subdivisions; x++)
                {
                    var i0 = y * (subdivisions + 1) + x;
                    var i1 = i0 + 1;
                    var i2 = i0 + (subdivisions + 1);
                    var i3 = i2 + 1;
                    indices.Add(i0);
                    indices.Add(i2);
                    indices.Add(i1);
                    indices.Add(i1);
                    indices.Add(i2);
                    indices.Add(i3);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        public void UpdateFourCornerWarp(Mesh mesh, Vector2[] corners)
        {
            if (corners == null || corners.Length != 4) return;

            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);

            if (vertices.Count != uvs.Count) return;

            var v0 = corners[0];
            var v1 = corners[1];
            var v2 = corners[2];
            var v3 = corners[3];

            for (var i = 0; i < vertices.Count; i++)
            {
                var uv = uvs[i];
                var pos = BilinearInterpolate(v0, v1, v2, v3, uv.x, uv.y);
                vertices[i] = new Vector3(pos.x, pos.y, 0f);
            }

            mesh.SetVertices(vertices, 0, vertices.Count,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        /// <summary>
        /// Projective (homogeneous) interpolation for 4-corner warp. Interpolates in
        /// homogeneous space and divides by w for perspective-correct vertex placement.
        /// </summary>
        private static Vector2 BilinearInterpolate(Vector2 p00, Vector2 p10, Vector2 p11, Vector2 p01, float u, float v)
        {
            var u1 = 1f - u;
            var v1 = 1f - v;
            return u1 * v1 * p00 + u * v1 * p10 + u * v * p11 + u1 * v * p01;
        }

        public void UpdateMeshWarp(Mesh mesh, Vector2[] controlPoints, int gridWidth, int gridHeight)
        {
            if (controlPoints == null || controlPoints.Length != gridWidth * gridHeight) return;

            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var uvs = new List<Vector2>();
            mesh.GetUVs(0, uvs);

            if (vertices.Count != uvs.Count) return;

            var meshSubdivisions = Mathf.RoundToInt(Mathf.Sqrt(vertices.Count)) - 1;
            if (meshSubdivisions < 0) return;

            for (var i = 0; i < vertices.Count; i++)
            {
                var uv = uvs[i];
                var pos = CatmullRomInterpolate(controlPoints, gridWidth, gridHeight, uv.x, uv.y);
                vertices[i] = new Vector3(pos.x, pos.y, 0f);
            }

            mesh.SetVertices(vertices, 0, vertices.Count,
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
        }

        /// <summary>
        /// Catmull-Rom spline interpolation over the control point grid.
        /// </summary>
        private static Vector2 CatmullRomInterpolate(Vector2[] grid, int gw, int gh, float u, float v)
        {
            var x = u * (gw - 1);
            var y = v * (gh - 1);
            var ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, gw - 2);
            var iy = Mathf.Clamp(Mathf.FloorToInt(y), 0, gh - 2);
            var fx = x - ix;
            var fy = y - iy;

            var p = new Vector2[4, 4];
            for (var dy = -1; dy <= 2; dy++)
            {
                for (var dx = -1; dx <= 2; dx++)
                {
                    var gx = Mathf.Clamp(ix + dx, 0, gw - 1);
                    var gy = Mathf.Clamp(iy + dy, 0, gh - 1);
                    p[dy + 1, dx + 1] = grid[gy * gw + gx];
                }
            }

            var row0 = CatmullRom1D(p[0, 0], p[0, 1], p[0, 2], p[0, 3], fx);
            var row1 = CatmullRom1D(p[1, 0], p[1, 1], p[1, 2], p[1, 3], fx);
            var row2 = CatmullRom1D(p[2, 0], p[2, 1], p[2, 2], p[2, 3], fx);
            var row3 = CatmullRom1D(p[3, 0], p[3, 1], p[3, 2], p[3, 3], fx);

            return CatmullRom1D(row0, row1, row2, row3, fy);
        }

        private static Vector2 CatmullRom1D(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var x = 0.5f * ((2f * p1.x) + (-p0.x + p2.x) * t +
                (2f * p0.x - 5f * p1.x + 4f * p2.x - p3.x) * t2 +
                (-p0.x + 3f * p1.x - 3f * p2.x + p3.x) * t3);
            var y = 0.5f * ((2f * p1.y) + (-p0.y + p2.y) * t +
                (2f * p0.y - 5f * p1.y + 4f * p2.y - p3.y) * t2 +
                (-p0.y + 3f * p1.y - 3f * p2.y + p3.y) * t3);
            return new Vector2(x, y);
        }

        public Vector2[] GetDefaultCorners()
        {
            return new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
        }

        public Vector2[] GetDefaultGrid(int width, int height)
        {
            var grid = new Vector2[width * height];
            var invW = width > 1 ? 1f / (width - 1) : 1f;
            var invH = height > 1 ? 1f / (height - 1) : 1f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    grid[y * width + x] = new Vector2(x * invW, y * invH);
                }
            }
            return grid;
        }
    }
}
