using System;
using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Masking
{
    public class MaskService : IMaskService, IDisposable
    {
        private const int BrushTextureSize = 64;

        private MaskMode _currentMode = MaskMode.Polygon;
        private readonly List<Vector2> _currentPolygonVertices = new List<Vector2>(64);
        private readonly List<PolygonData> _completedPolygons = new List<PolygonData>();

        private RenderTexture _cachedMaskRT;
        private int _cachedWidth;
        private int _cachedHeight;

        private Texture2D _brushSoft;
        private Texture2D _brushHard;
        private Material _brushAdditiveMaterial;
        private Material _brushEraserMaterial;

        private bool _brushStrokeActive;
        private Vector2 _lastBrushPosition;
        private float _brushRadius;
        private float _brushHardness;
        private bool _brushEraser;

        private bool _disposed;

        public MaskMode CurrentMode => _currentMode;

        public MaskService()
        {
            InitializeBrushTextures();
            InitializeBrushMaterials();
        }

        public void SetMode(MaskMode mode)
        {
            _currentMode = mode;
        }

        public RenderTexture GetMaskRT(int width, int height)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MaskService));

            if (_cachedMaskRT != null && _cachedWidth == width && _cachedHeight == height)
                return _cachedMaskRT;

            ReleaseCachedRT();

            _cachedMaskRT = new RenderTexture(width, height, 0, RenderTextureFormat.R8);
            _cachedMaskRT.Create();
            _cachedWidth = width;
            _cachedHeight = height;

            ClearMask(_cachedMaskRT);
            return _cachedMaskRT;
        }

        public void ClearMask(RenderTexture maskRT)
        {
            if (maskRT == null) return;

            var prev = RenderTexture.active;
            RenderTexture.active = maskRT;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = prev;
        }

        public void AddPolygonVertex(RenderTexture maskRT, Vector2 vertex)
        {
            _currentPolygonVertices.Add(vertex);
        }

        public void ClosePolygon(RenderTexture maskRT, bool inverted = false)
        {
            if (_currentPolygonVertices.Count < 3)
            {
                CancelPolygon();
                return;
            }

            var vertices = _currentPolygonVertices.ToArray();
            var triangles = EarClipTriangulator.Triangulate(vertices);

            if (triangles.Length < 3)
            {
                CancelPolygon();
                return;
            }

            var prev = RenderTexture.active;
            RenderTexture.active = maskRT;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, maskRT.width, 0, maskRT.height);

            if (inverted)
            {
                GL.Clear(true, true, Color.black);
                GL.Begin(GL.TRIANGLES);
                GL.Color(Color.white);
                RenderTriangles(vertices, triangles, maskRT.width, maskRT.height);
                GL.End();
            }
            else
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(Color.black);
                RenderTriangles(vertices, triangles, maskRT.width, maskRT.height);
                GL.End();
            }

            GL.PopMatrix();
            RenderTexture.active = prev;

            _completedPolygons.Add(new PolygonData
            {
                Vertices = vertices,
                IsSubtractive = !inverted
            });

            _currentPolygonVertices.Clear();
        }

        private void RenderTriangles(Vector2[] vertices, int[] triangles, int width, int height)
        {
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var i0 = triangles[i];
                var i1 = triangles[i + 1];
                var i2 = triangles[i + 2];

                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];

                var x0 = v0.x * width;
                var y0 = v0.y * height;
                var x1 = v1.x * width;
                var y1 = v1.y * height;
                var x2 = v2.x * width;
                var y2 = v2.y * height;

                GL.Vertex3(x0, y0, 0);
                GL.Vertex3(x1, y1, 0);
                GL.Vertex3(x2, y2, 0);
            }
        }

        public void CancelPolygon()
        {
            _currentPolygonVertices.Clear();
        }

        public List<Vector2> GetCurrentPolygonVertices()
        {
            return _currentPolygonVertices;
        }

        public void BeginBrushStroke(RenderTexture maskRT, Vector2 position, float radius, float hardness, bool eraser)
        {
            _brushStrokeActive = true;
            _lastBrushPosition = position;
            _brushRadius = radius;
            _brushHardness = Mathf.Clamp01(hardness);
            _brushEraser = eraser;

            StampBrush(maskRT, position);
        }

        public void ContinueBrushStroke(RenderTexture maskRT, Vector2 position)
        {
            if (!_brushStrokeActive) return;

            var step = _brushRadius / 3f;
            var dist = Vector2.Distance(_lastBrushPosition, position);

            if (dist <= step)
            {
                StampBrush(maskRT, position);
                _lastBrushPosition = position;
                return;
            }

            var steps = Mathf.CeilToInt(dist / step);
            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var p = Vector2.Lerp(_lastBrushPosition, position, t);
                StampBrush(maskRT, p);
            }

            _lastBrushPosition = position;
        }

        public void EndBrushStroke()
        {
            _brushStrokeActive = false;
        }

        private void StampBrush(RenderTexture maskRT, Vector2 position)
        {
            if (maskRT == null) return;

            var brushTex = _brushHardness >= 0.5f ? _brushHard : _brushSoft;
            var mat = _brushEraser ? _brushEraserMaterial : _brushAdditiveMaterial;

            var sizePx = 2f * _brushRadius * Mathf.Min(maskRT.width, maskRT.height);
            var x = position.x * maskRT.width - sizePx * 0.5f;
            var y = position.y * maskRT.height - sizePx * 0.5f;

            var prev = RenderTexture.active;
            RenderTexture.active = maskRT;

            Graphics.DrawTexture(
                new Rect(x, y, sizePx, sizePx),
                brushTex,
                mat,
                0, 0, 1, 1,
                _brushEraser ? Color.black : Color.white
            );

            RenderTexture.active = prev;
        }

        public MaskData Serialize()
        {
            byte[] brushPng = null;
            if (_cachedMaskRT != null && _currentMode == MaskMode.Brush)
            {
                var tex = new Texture2D(_cachedMaskRT.width, _cachedMaskRT.height, TextureFormat.R8, false);
                var prev = RenderTexture.active;
                RenderTexture.active = _cachedMaskRT;
                tex.ReadPixels(new Rect(0, 0, _cachedMaskRT.width, _cachedMaskRT.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                brushPng = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
            }

            var polygons = new List<PolygonData>(_completedPolygons);

            return new MaskData
            {
                Mode = _currentMode,
                Polygons = polygons,
                BrushMaskPng = brushPng,
                Inverted = false
            };
        }

        public void Deserialize(MaskData data, RenderTexture maskRT)
        {
            if (maskRT == null) return;

            _completedPolygons.Clear();
            _currentPolygonVertices.Clear();

            if (data.Polygons != null && data.Polygons.Count > 0)
            {
                var hasSubtractive = false;
                var hasAdditive = false;
                foreach (var p in data.Polygons)
                {
                    if (p.IsSubtractive) hasSubtractive = true;
                    else hasAdditive = true;
                }

                if (hasSubtractive && !hasAdditive)
                {
                    ClearMask(maskRT);
                    foreach (var poly in data.Polygons)
                    {
                        if (poly.Vertices == null || poly.Vertices.Length < 3) continue;
                        DrawPolygon(maskRT, poly.Vertices, true);
                        _completedPolygons.Add(poly);
                    }
                }
                else if (hasAdditive && !hasSubtractive)
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = maskRT;
                    GL.Clear(true, true, Color.black);
                    RenderTexture.active = prev;
                    foreach (var poly in data.Polygons)
                    {
                        if (poly.Vertices == null || poly.Vertices.Length < 3) continue;
                        DrawPolygon(maskRT, poly.Vertices, false);
                        _completedPolygons.Add(poly);
                    }
                }
                else
                {
                    ClearMask(maskRT);
                    foreach (var poly in data.Polygons)
                    {
                        if (poly.Vertices == null || poly.Vertices.Length < 3) continue;
                        DrawPolygon(maskRT, poly.Vertices, poly.IsSubtractive);
                        _completedPolygons.Add(poly);
                    }
                }
            }
            else if (data.BrushMaskPng != null && data.BrushMaskPng.Length > 0)
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(data.BrushMaskPng))
                {
                    var prev = RenderTexture.active;
                    RenderTexture.active = maskRT;
                    Graphics.Blit(tex, maskRT);
                    RenderTexture.active = prev;
                }
                UnityEngine.Object.Destroy(tex);
            }
            else
            {
                ClearMask(maskRT);
            }
        }

        private void DrawPolygon(RenderTexture maskRT, Vector2[] vertices, bool subtractive)
        {
            var triangles = EarClipTriangulator.Triangulate(vertices);
            if (triangles.Length < 3) return;

            var prev = RenderTexture.active;
            RenderTexture.active = maskRT;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, maskRT.width, 0, maskRT.height);
            GL.Begin(GL.TRIANGLES);
            GL.Color(subtractive ? Color.black : Color.white);
            RenderTriangles(vertices, triangles, maskRT.width, maskRT.height);
            GL.End();
            GL.PopMatrix();

            RenderTexture.active = prev;
        }

        private void InitializeBrushTextures()
        {
            _brushSoft = CreateBrushTexture(0f);
            _brushHard = CreateBrushTexture(1f);
        }

        private Texture2D CreateBrushTexture(float hardness)
        {
            var tex = new Texture2D(BrushTextureSize, BrushTextureSize, TextureFormat.RGBA32, false);
            var center = BrushTextureSize / 2f;
            var radius = center - 0.5f;

            for (var y = 0; y < BrushTextureSize; y++)
            {
                for (var x = 0; x < BrushTextureSize; x++)
                {
                    var dx = (x - center) / radius;
                    var dy = (y - center) / radius;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha;
                    if (dist >= 1f)
                    {
                        alpha = 0f;
                    }
                    else if (hardness >= 1f)
                    {
                        alpha = 1f;
                    }
                    else if (hardness <= 0f)
                    {
                        alpha = 1f - Mathf.Clamp01(dist);
                        alpha = Mathf.Exp(-alpha * alpha * 4f);
                    }
                    else
                    {
                        var inner = 1f - hardness;
                        alpha = dist <= inner ? 1f : Mathf.Exp(-Mathf.Pow((dist - inner) / (1f - inner), 2f) * 4f);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return tex;
        }

        private void InitializeBrushMaterials()
        {
            var addShader = Shader.Find("Hidden/MaptifierBrushAdd");
            _brushAdditiveMaterial = addShader != null
                ? new Material(addShader)
                : new Material(Shader.Find("Unlit/Transparent"));

            var eraserShader = Shader.Find("Hidden/MaptifierBrushEraser");
            _brushEraserMaterial = eraserShader != null
                ? new Material(eraserShader)
                : new Material(Shader.Find("Unlit/Transparent"));
        }

        private void ReleaseCachedRT()
        {
            if (_cachedMaskRT != null)
            {
                _cachedMaskRT.Release();
                _cachedMaskRT = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            ReleaseCachedRT();

            if (_brushSoft != null)
            {
                UnityEngine.Object.Destroy(_brushSoft);
                _brushSoft = null;
            }

            if (_brushHard != null)
            {
                UnityEngine.Object.Destroy(_brushHard);
                _brushHard = null;
            }

            if (_brushAdditiveMaterial != null)
            {
                UnityEngine.Object.Destroy(_brushAdditiveMaterial);
                _brushAdditiveMaterial = null;
            }

            if (_brushEraserMaterial != null)
            {
                UnityEngine.Object.Destroy(_brushEraserMaterial);
                _brushEraserMaterial = null;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
