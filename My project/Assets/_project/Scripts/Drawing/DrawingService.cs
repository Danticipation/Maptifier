using System.Collections.Generic;
using UnityEngine;

namespace Maptifier.Drawing
{
    public class DrawingService : IDrawingService
    {
        private const int BrushResolution = 128;
        private const int MaxStrokePoints = 1024;
        private const float CatmullRomTension = 0.5f;
        private const float VelocityPressureScale = 800f;
        private const float CurvatureSubdivisionThreshold = 0.02f;
        private const int MinSubdivisions = 2;
        private const int MaxSubdivisions = 16;
        private const float PredictionFrames = 1.5f;

        private readonly List<StrokePoint> _strokePoints = new List<StrokePoint>(MaxStrokePoints);
        private Texture2D _brushTexture;
        private Material _brushMaterial;
        private Material _eraserMaterial;
        private int _canvasWidth;
        private int _canvasHeight;
        private bool _initialized;
        private Vector2 _lastPosition;
        private Vector2 _lastVelocity;
        private float _lastPressure;

        private const string PressureSensitivityKey = "Maptifier_PressureSensitivity";
        private const string DefaultBrushSizeKey = "Maptifier_DefaultBrushSize";

        public Color BrushColor { get; set; } = Color.white;
        public float BrushSize { get; set; } = 24f;
        public float BrushOpacity { get; set; } = 1f;
        public bool IsEraser { get; set; }
        public bool IsDrawing { get; private set; }

        private struct StrokePoint
        {
            public Vector2 Position;
            public float Pressure;
            public Vector2 Velocity;
        }

        public void Initialize(int width, int height)
        {
            if (_initialized && _canvasWidth == width && _canvasHeight == height)
                return;

            _canvasWidth = width;
            _canvasHeight = height;
            _initialized = true;
            BrushSize = PlayerPrefs.GetFloat(DefaultBrushSizeKey, 24f);

            CreateBrushTexture();
            CreateMaterials();
        }

        private void CreateBrushTexture()
        {
            if (_brushTexture != null)
            {
                Object.Destroy(_brushTexture);
            }

            _brushTexture = new Texture2D(BrushResolution, BrushResolution);
            _brushTexture.wrapMode = TextureWrapMode.Clamp;
            _brushTexture.filterMode = FilterMode.Bilinear;

            var center = new Vector2(BrushResolution * 0.5f, BrushResolution * 0.5f);
            var sigma = BrushResolution * 0.25f;
            var sigmaSq = sigma * sigma;

            var pixels = new Color[BrushResolution * BrushResolution];
            for (int y = 0; y < BrushResolution; y++)
            {
                for (int x = 0; x < BrushResolution; x++)
                {
                    var dx = x - center.x;
                    var dy = y - center.y;
                    var distSq = dx * dx + dy * dy;
                    var gaussian = Mathf.Exp(-distSq / (2f * sigmaSq));
                    pixels[y * BrushResolution + x] = new Color(1f, 1f, 1f, gaussian);
                }
            }
            _brushTexture.SetPixels(pixels);
            _brushTexture.Apply();
        }

        private void CreateMaterials()
        {
            if (_brushMaterial != null) Object.Destroy(_brushMaterial);
            if (_eraserMaterial != null) Object.Destroy(_eraserMaterial);

            var brushShader = Shader.Find("Maptifier/Drawing/BrushStamp");
            if (brushShader == null)
            {
                Debug.LogError("[DrawingService] BrushStamp shader not found. Ensure Maptifier/Drawing/BrushStamp exists.");
                return;
            }

            _brushMaterial = new Material(brushShader);

            _eraserMaterial = new Material(brushShader);
        }

        public void BeginStroke(RenderTexture canvas, Vector2 position, float pressure)
        {
            if (canvas == null) return;

            EnsureInitialized(canvas.width, canvas.height);

            IsDrawing = true;
            _strokePoints.Clear();

            pressure = ResolvePressure(pressure, Vector2.zero);
            _lastPosition = position;
            _lastVelocity = Vector2.zero;
            _lastPressure = pressure;

            _strokePoints.Add(new StrokePoint
            {
                Position = position,
                Pressure = pressure,
                Velocity = Vector2.zero
            });

            StampBrush(canvas, position, BrushSize * pressure, BrushOpacity * pressure);
        }

        public void ContinueStroke(RenderTexture canvas, Vector2 position, float pressure)
        {
            if (canvas == null || !IsDrawing) return;

            var velocity = (position - _lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
            pressure = ResolvePressure(pressure, velocity);

            _strokePoints.Add(new StrokePoint
            {
                Position = position,
                Pressure = pressure,
                Velocity = velocity
            });

            if (_strokePoints.Count > MaxStrokePoints)
            {
                _strokePoints.RemoveAt(0);
            }

            var predictedPosition = position + velocity * (Time.deltaTime * PredictionFrames);
            RenderStrokeSegment(canvas, _lastPosition, position, _lastPressure, pressure, _lastVelocity, velocity, predictedPosition);

            _lastPosition = position;
            _lastVelocity = velocity;
            _lastPressure = pressure;
        }

        public void EndStroke(RenderTexture canvas)
        {
            if (!IsDrawing) return;
            IsDrawing = false;
        }

        public void Clear(RenderTexture canvas)
        {
            if (canvas == null) return;
            RenderTexture.active = canvas;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;
        }

        public RenderTexture CaptureSnapshot(RenderTexture canvas)
        {
            if (canvas == null) return null;

            var snapshot = new RenderTexture(canvas.width, canvas.height, 0, canvas.format);
            snapshot.Create();
            Graphics.Blit(canvas, snapshot);
            return snapshot;
        }

        public void RestoreSnapshot(RenderTexture canvas, RenderTexture snapshot)
        {
            if (canvas == null || snapshot == null) return;
            Graphics.Blit(snapshot, canvas);
        }

        public void Dispose()
        {
            if (_brushTexture != null)
            {
                Object.Destroy(_brushTexture);
                _brushTexture = null;
            }
            if (_brushMaterial != null)
            {
                Object.Destroy(_brushMaterial);
                _brushMaterial = null;
            }
            if (_eraserMaterial != null)
            {
                Object.Destroy(_eraserMaterial);
                _eraserMaterial = null;
            }
            _initialized = false;
        }

        private void EnsureInitialized(int width, int height)
        {
            if (!_initialized || _canvasWidth != width || _canvasHeight != height)
            {
                Initialize(width, height);
            }
        }

        private float ResolvePressure(float pressure, Vector2 velocity)
        {
            float raw;
            if (pressure > 0f && pressure < 1f)
                raw = Mathf.Clamp01(pressure);
            else
            {
                var velMag = velocity.magnitude;
                raw = Mathf.Clamp01(1f - velMag / VelocityPressureScale);
            }
            var sensitivity = PlayerPrefs.GetFloat(PressureSensitivityKey, 1f);
            return Mathf.Clamp01(raw * sensitivity);
        }

        private float VelocityCurve(Vector2 velocity)
        {
            var velMag = velocity.magnitude;
            return Mathf.Clamp01(1f - velMag / VelocityPressureScale);
        }

        private void RenderStrokeSegment(RenderTexture canvas, Vector2 p0, Vector2 p1, float pressure0, float pressure1, Vector2 vel0, Vector2 vel1, Vector2 predictedNext)
        {
            int pointCount = _strokePoints.Count;
            if (pointCount < 2) return;

            Vector2 pPrev, pCurr, pNext, pNextNext;
            if (pointCount == 2)
            {
                pPrev = p0;
                pCurr = p0;
                pNext = p1;
                pNextNext = predictedNext;
            }
            else if (pointCount == 3)
            {
                pPrev = _strokePoints[0].Position;
                pCurr = p0;
                pNext = p1;
                pNextNext = predictedNext;
            }
            else
            {
                pPrev = _strokePoints[pointCount - 4].Position;
                pCurr = _strokePoints[pointCount - 3].Position;
                pNext = _strokePoints[pointCount - 2].Position;
                pNextNext = p1;
            }

            var curvature = EstimateCurvature(pPrev, pCurr, pNext, pNextNext);
            var subdivisions = Mathf.Clamp(Mathf.RoundToInt(curvature * MaxSubdivisions), MinSubdivisions, MaxSubdivisions);

            for (int i = 0; i <= subdivisions; i++)
            {
                var t = i / (float)subdivisions;
                var pos = CatmullRomPoint(pPrev, pCurr, pNext, pNextNext, t);
                var pressure = Mathf.Lerp(pressure0, pressure1, t);
                var velocity = Vector2.Lerp(vel0, vel1, t);
                var size = BrushSize * pressure * VelocityCurve(velocity);
                var opacity = BrushOpacity * pressure;

                StampBrush(canvas, pos, size, opacity);
            }
        }

        private float EstimateCurvature(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var mid = CatmullRomPoint(p0, p1, p2, p3, 0.5f);
            var chordStart = p1;
            var chordEnd = p2;
            var chordLen = Vector2.Distance(chordStart, chordEnd);
            if (chordLen < 0.001f) return 0f;

            var distToChord = Vector2.Distance(mid, (chordStart + chordEnd) * 0.5f);
            return Mathf.Clamp01(distToChord / chordLen / CurvatureSubdivisionThreshold);
        }

        private static Vector2 CatmullRomPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var s = CatmullRomTension;

            var m1 = s * (p2 - p0);
            var m2 = s * (p3 - p1);

            var a = 2f * p1 - 2f * p2 + m1 + m2;
            var b = -3f * p1 + 3f * p2 - 2f * m1 - m2;
            var c = m1;
            var d = p1;

            return a * t3 + b * t2 + c * t + d;
        }

        private void StampBrush(RenderTexture canvas, Vector2 center, float size, float opacity)
        {
            if (_brushTexture == null || _brushMaterial == null) return;

            var halfSize = size * 0.5f;
            var rect = new Rect(
                center.x - halfSize,
                center.y - halfSize,
                size,
                size
            );

            var material = IsEraser ? _eraserMaterial : _brushMaterial;
            var pass = IsEraser ? 1 : 0;
            material.SetTexture("_MainTex", _brushTexture);
            material.SetColor("_Color", BrushColor);
            material.SetFloat("_Opacity", opacity);

            var prevActive = RenderTexture.active;
            RenderTexture.active = canvas;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, canvas.width, canvas.height, 0);

            material.SetPass(pass);
            Graphics.DrawTexture(rect, _brushTexture, material);

            GL.PopMatrix();

            RenderTexture.active = prevActive;
        }
    }
}
